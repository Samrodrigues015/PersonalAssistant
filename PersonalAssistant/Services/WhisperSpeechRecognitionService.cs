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
/// grava do microfone (NAudio) até acabares de falar e transcreve com Whisper (Whisper.net).
///  - a gravação para sozinha quando te calas (VoiceActivityDetector), em vez de um tempo fixo;
///  - se ninguém falar, nem sequer chama o Whisper (é com silêncio que ele "inventa" frases);
///  - o texto final passa pelo TranscriptFilter, que descarta "alucinações" conhecidas.
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
            _logger.LogDebug("A gravar do microfone até acabares de falar...");

            var speechDetected = await Task.Run(
                () => RecordAudioToFile(tempFilePath, _settings, cancellationToken),
                cancellationToken);

            if (!speechDetected)
            {
                _logger.LogInformation("Não foi detetada fala no microfone — nada para transcrever.");
                return null;
            }

            var builder = factory.CreateBuilder().WithLanguage(_settings.Language);

            if (!string.IsNullOrWhiteSpace(_settings.InitialPrompt))
            {
                builder = builder.WithPrompt(_settings.InitialPrompt);
            }

            using var processor = builder.Build();

            await using var audioStream = File.OpenRead(tempFilePath);

            var transcript = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(audioStream).WithCancellation(cancellationToken))
            {
                transcript.Append(segment.Text);
            }

            var result = TranscriptFilter.Clean(transcript.ToString());

            if (result is null)
            {
                _logger.LogInformation("Transcrição descartada (vazia ou \"alucinação\" conhecida do Whisper): {Raw}", transcript.ToString().Trim());
            }

            return result;
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
            // Descarrega para ".part" e só no fim renomeia: se a ligação cair a meio, não fica um
            // modelo "meio descarregado" com o nome certo (que da próxima vez parecia instalado).
            var partialPath = _settings.ModelPath + ".part";

            await using (var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType))
            await using (var fileWriter = File.Create(partialPath))
            {
                await modelStream.CopyToAsync(fileWriter, cancellationToken);
            }

            File.Move(partialPath, _settings.ModelPath, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new SpeechServiceException(
                $"Não foi possível descarregar automaticamente o modelo Whisper. Descarrega manualmente um modelo ggml (ex.: ggml-small.bin de https://huggingface.co/ggerganov/whisper.cpp) e coloca-o em '{_settings.ModelPath}'.",
                ex);
        }
    }

    /// <summary>
    /// Grava áudio mono a 16kHz (formato que o Whisper espera) para um ficheiro WAV, até o
    /// VoiceActivityDetector decidir parar (acabaste de falar, ninguém falou, ou tempo máximo).
    /// </summary>
    /// <returns>true se foi detetada fala na gravação.</returns>
    private static bool RecordAudioToFile(string filePath, VoiceSettings settings, CancellationToken cancellationToken)
    {
        var waveFormat = new WaveFormat(16000, 16, 1);

        var detector = new VoiceActivityDetector(
            settings.SilenceThreshold,
            TimeSpan.FromSeconds(settings.EndSilenceSeconds),
            TimeSpan.FromSeconds(settings.SpeechStartTimeoutSeconds),
            TimeSpan.FromSeconds(settings.MaxRecordingSeconds));

        // BufferMilliseconds = 100 → recebemos o áudio aos pedaços de 0,1s, o que dá
        // uma reação rápida quando paras de falar.
        using var waveIn = new WaveInEvent { WaveFormat = waveFormat, BufferMilliseconds = 100 };
        using var writer = new WaveFileWriter(filePath, waveFormat);

        var recordingFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRequested = false;

        waveIn.DataAvailable += (_, e) =>
        {
            if (stopRequested)
            {
                return;
            }

            writer.Write(e.Buffer, 0, e.BytesRecorded);

            var chunkDuration = TimeSpan.FromSeconds((double)e.BytesRecorded / waveFormat.AverageBytesPerSecond);
            var state = detector.Process(e.Buffer.AsSpan(0, e.BytesRecorded), chunkDuration);

            if (state != VoiceActivityState.Continue)
            {
                stopRequested = true;
                waveIn.StopRecording();
            }
        };

        waveIn.RecordingStopped += (_, args) =>
        {
            if (args.Exception is not null)
            {
                recordingFinished.TrySetException(args.Exception);
            }
            else
            {
                recordingFinished.TrySetResult();
            }
        };

        waveIn.StartRecording();

        using var registration = cancellationToken.Register(() => waveIn.StopRecording());

        recordingFinished.Task.GetAwaiter().GetResult();
        return detector.SpeechDetected;
    }

    public void Dispose()
    {
        _whisperFactory?.Dispose();
        _initLock.Dispose();
    }
}
