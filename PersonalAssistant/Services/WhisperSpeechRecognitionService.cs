using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using PersonalAssistant.Configuration;
using Whisper.net;
using Whisper.net.Ggml;

namespace PersonalAssistant.Services;

/// <summary>
/// Reconhecimento de voz local, sem depender de nenhum serviço externo:
/// grava alguns segundos de áudio do microfone (NAudio) e transcreve-os com Whisper (Whisper.net).
/// O modelo ggml é descarregado automaticamente na primeira utilização, se ainda não existir em disco.
/// </summary>
public class WhisperSpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private readonly VoiceSettings _settings;
    private readonly ILogger<WhisperSpeechRecognitionService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private WhisperFactory? _whisperFactory;

    public WhisperSpeechRecognitionService(IOptions<VoiceSettings> settings, ILogger<WhisperSpeechRecognitionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
    {
        var factory = await GetOrCreateFactoryAsync(cancellationToken);
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"personalassistant-{Guid.NewGuid():N}.wav");

        try
        {
            _logger.LogInformation("A gravar áudio do microfone durante {Seconds}s...", _settings.RecordingSeconds);

            await Task.Run(
                () => RecordAudioToFile(tempFilePath, _settings.RecordingSeconds, cancellationToken),
                cancellationToken);

            using var processor = factory.CreateBuilder()
                .WithLanguage(_settings.Language)
                .Build();

            await using var audioStream = File.OpenRead(tempFilePath);

            var transcript = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(audioStream).WithCancellation(cancellationToken))
            {
                transcript.Append(segment.Text);
            }

            var result = transcript.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex) when (ex is not SpeechServiceException)
        {
            _logger.LogError(ex, "Erro ao reconhecer voz");
            throw new SpeechServiceException("Ocorreu um erro ao tentar reconhecer a tua voz.", ex);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    /// <summary>
    /// Cria o WhisperFactory na primeira utilização (é caro — carrega o modelo para memória),
    /// e reutiliza-o nas chamadas seguintes. Descarrega o modelo automaticamente se necessário.
    /// </summary>
    private async Task<WhisperFactory> GetOrCreateFactoryAsync(CancellationToken cancellationToken)
    {
        if (_whisperFactory is not null)
        {
            return _whisperFactory;
        }

        await _initLock.WaitAsync(cancellationToken);

        try
        {
            if (_whisperFactory is not null)
            {
                return _whisperFactory;
            }

            await EnsureModelDownloadedAsync(cancellationToken);
            _whisperFactory = WhisperFactory.FromPath(_settings.ModelPath);
            return _whisperFactory;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureModelDownloadedAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_settings.ModelPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_settings.ModelPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!Enum.TryParse<GgmlType>(_settings.GgmlModelType, ignoreCase: true, out var ggmlType))
        {
            ggmlType = GgmlType.Base;
        }

        _logger.LogInformation(
            "Modelo Whisper não encontrado em '{ModelPath}'. A descarregar o modelo '{GgmlType}' do Hugging Face (só acontece uma vez)...",
            _settings.ModelPath,
            ggmlType);

        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
            using var fileWriter = File.OpenWrite(_settings.ModelPath);
            await modelStream.CopyToAsync(fileWriter, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new SpeechServiceException(
                $"Não foi possível descarregar automaticamente o modelo Whisper. Descarrega manualmente um modelo ggml (ex.: ggml-base.bin de https://huggingface.co/ggerganov/whisper.cpp) e coloca-o em '{_settings.ModelPath}'.",
                ex);
        }
    }

    /// <summary>
    /// Grava áudio mono a 16kHz (formato que o Whisper espera) diretamente para um ficheiro WAV,
    /// parando ao fim de <paramref name="seconds"/> segundos ou se o cancellationToken for acionado.
    /// </summary>
    private static void RecordAudioToFile(string filePath, int seconds, CancellationToken cancellationToken)
    {
        var waveFormat = new WaveFormat(16000, 16, 1);

        using var waveIn = new WaveInEvent { WaveFormat = waveFormat };
        using var writer = new WaveFileWriter(filePath, waveFormat);

        var recordingFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        waveIn.DataAvailable += (_, e) => writer.Write(e.Buffer, 0, e.BytesRecorded);
        waveIn.RecordingStopped += (_, _) => recordingFinished.TrySetResult();

        waveIn.StartRecording();

        using var timer = new Timer(_ => waveIn.StopRecording(), null, TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
        using var registration = cancellationToken.Register(() => waveIn.StopRecording());

        recordingFinished.Task.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _whisperFactory?.Dispose();
        _initLock.Dispose();
    }
}
