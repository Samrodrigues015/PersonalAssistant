using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using PersonalAssistant.Configuration;

namespace PersonalAssistant.Services;

/// <summary>
/// Síntese de fala com o Piper (voz neural, offline, gratuita).
///
/// Como funciona:
///  1. Na primeira utilização, descarrega o piper.exe (GitHub) e a voz escolhida (Hugging Face).
///  2. Para cada resposta, corre o piper.exe: o texto entra pelo "stdin" e sai um ficheiro WAV.
///  3. Toca o WAV com o NAudio (a mesma biblioteca que usamos para gravar o microfone).
///
/// Implementa a mesma interface ITextToSpeechService que a voz do Windows — por isso o resto
/// do programa não sabe (nem precisa de saber) qual dos dois motores está a falar.
/// </summary>
public class PiperTextToSpeechService : ITextToSpeechService
{
    private readonly PiperSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PiperTextToSpeechService> _logger;
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private bool _installed;

    public PiperTextToSpeechService(
        IOptions<PiperSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<PiperTextToSpeechService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseFolder => Path.Combine(AppContext.BaseDirectory, _settings.Folder);
    private string ExecutablePath => Path.Combine(BaseFolder, "piper", "piper.exe");
    private string VoiceModelPath => Path.Combine(BaseFolder, "voices", _settings.VoiceName + ".onnx");

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        var cleanText = PrepareText(text);

        if (cleanText.Length == 0)
        {
            return;
        }

        await EnsureInstalledAsync(cancellationToken);

        var wavPath = Path.Combine(Path.GetTempPath(), $"personalassistant-tts-{Guid.NewGuid():N}.wav");

        try
        {
            await SynthesizeToFileAsync(cleanText, wavPath, cancellationToken);
            await PlayAsync(wavPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
            }
        }
    }

    /// <summary>
    /// Prepara o texto para ser lido em voz alta: tira a formatação "markdown" que o modelo
    /// às vezes usa (asteriscos, cardinais...) e junta tudo numa só linha.
    /// </summary>
    public static string PrepareText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // "**negrito**" → "negrito" (sem deixar espaços); separadores de tabelas/citações → espaço.
        var result = Regex.Replace(text, @"[*#_`]", string.Empty);
        result = Regex.Replace(result, @"[>|]", " ");
        // "- tarefa" no início de uma linha (listas) → só "tarefa", com uma pausa antes.
        result = Regex.Replace(result, @"(^|\n)\s*[-•]\s+", "$1");
        // Mudança de linha = pausa: se a linha não acabar já em pontuação, acrescenta um ponto.
        result = Regex.Replace(
            result,
            @"([.!?:;,])?\s*\n\s*",
            m => m.Groups[1].Success ? m.Groups[1].Value + " " : ". ");
        result = Regex.Replace(result, @"\.(\s*\.)+", ".");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        return result;
    }

    /// <summary>
    /// Endereço de download de um ficheiro de voz. Ex.: "pt_PT-tugão-medium" →
    /// .../pt/pt_PT/tug%C3%A3o/medium/pt_PT-tug%C3%A3o-medium.onnx
    /// </summary>
    public static string BuildVoiceUrl(string voicesBaseUrl, string voiceName, string extension)
    {
        // Formato: "<idioma_PAÍS>-<nome>-<qualidade>", ex.: "pt_PT-tugão-medium".
        var parts = voiceName.Split('-');

        if (parts.Length < 3)
        {
            throw new ArgumentException($"Nome de voz inválido: '{voiceName}'. Formato esperado: 'pt_PT-nome-medium'.", nameof(voiceName));
        }

        var languageCountry = parts[0];
        var quality = parts[^1];
        var speaker = string.Join('-', parts[1..^1]);
        var family = languageCountry.Split('_')[0];

        static string E(string segment) => Uri.EscapeDataString(segment);

        return $"{voicesBaseUrl.TrimEnd('/')}/{E(family)}/{E(languageCountry)}/{E(speaker)}/{E(quality)}/{E(voiceName)}{extension}";
    }

    private async Task EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        if (_installed)
        {
            return;
        }

        await _installLock.WaitAsync(cancellationToken);

        try
        {
            if (_installed)
            {
                return;
            }

            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(10);

            if (!File.Exists(ExecutablePath))
            {
                _logger.LogInformation("A descarregar o Piper (motor de voz) — só acontece uma vez...");
                var zipPath = Path.Combine(Path.GetTempPath(), $"piper-{Guid.NewGuid():N}.zip");

                try
                {
                    await DownloadAsync(http, _settings.ExecutableUrl, zipPath, cancellationToken);
                    Directory.CreateDirectory(BaseFolder);
                    // O zip já tem uma pasta "piper/" lá dentro → fica BaseFolder/piper/piper.exe
                    ZipFile.ExtractToDirectory(zipPath, BaseFolder, overwriteFiles: true);
                }
                finally
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                }
            }

            if (!File.Exists(VoiceModelPath) || !File.Exists(VoiceModelPath + ".json"))
            {
                _logger.LogInformation("A descarregar a voz '{Voice}' — só acontece uma vez...", _settings.VoiceName);
                Directory.CreateDirectory(Path.GetDirectoryName(VoiceModelPath)!);
                await DownloadAsync(http, BuildVoiceUrl(_settings.VoicesBaseUrl, _settings.VoiceName, ".onnx.json"), VoiceModelPath + ".json", cancellationToken);
                await DownloadAsync(http, BuildVoiceUrl(_settings.VoicesBaseUrl, _settings.VoiceName, ".onnx"), VoiceModelPath, cancellationToken);
            }

            _installed = true;
            _logger.LogInformation("Voz Piper pronta: '{Voice}'.", _settings.VoiceName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SpeechServiceException(
                "Não foi possível preparar o Piper (descarregar o programa ou a voz). Verifica a ligação à internet.", ex);
        }
        finally
        {
            _installLock.Release();
        }
    }

    /// <summary>
    /// Descarrega para um ficheiro temporário e só no fim o renomeia: se a ligação cair a meio,
    /// não fica um ficheiro "meio descarregado" com o nome certo (que depois parecia instalado).
    /// </summary>
    private static async Task DownloadAsync(HttpClient http, string url, string destinationPath, CancellationToken cancellationToken)
    {
        var partialPath = destinationPath + ".part";

        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(partialPath);
            await source.CopyToAsync(target, cancellationToken);
        }

        File.Move(partialPath, destinationPath, overwrite: true);
    }

    private async Task SynthesizeToFileAsync(string text, string wavPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            // O texto tem acentos: tem de ir em UTF-8 (sem "BOM"), senão o Piper lê "Ã£" em vez de "ã".
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(VoiceModelPath);
        startInfo.ArgumentList.Add("--output_file");
        startInfo.ArgumentList.Add(wavPath);
        startInfo.ArgumentList.Add("--length_scale");
        startInfo.ArgumentList.Add(_settings.LengthScale.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new SpeechServiceException("Não foi possível arrancar o piper.exe.");

        // Lê o stderr em paralelo (se não for lido, o processo pode bloquear com o buffer cheio).
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.StandardInput.WriteLineAsync(text.AsMemory(), cancellationToken);
        process.StandardInput.Close();

        await process.WaitForExitAsync(cancellationToken);
        var errors = await errorTask;

        if (process.ExitCode != 0 || !File.Exists(wavPath))
        {
            throw new SpeechServiceException($"O Piper falhou ao gerar a voz (código {process.ExitCode}). {errors.Trim()}");
        }
    }

    private static async Task PlayAsync(string wavPath, CancellationToken cancellationToken)
    {
        using var reader = new WaveFileReader(wavPath);
        using var output = new WaveOutEvent();

        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, args) =>
        {
            if (args.Exception is not null)
            {
                finished.TrySetException(args.Exception);
            }
            else
            {
                finished.TrySetResult();
            }
        };

        output.Init(reader);
        output.Play();

        using var registration = cancellationToken.Register(() => output.Stop());
        await finished.Task;
    }
}
