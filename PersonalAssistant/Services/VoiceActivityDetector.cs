namespace PersonalAssistant.Services;

/// <summary>
/// O que a gravação deve fazer a seguir, depois de analisar mais um pedaço de áudio.
/// </summary>
public enum VoiceActivityState
{
    /// <summary>Continuar a gravar.</summary>
    Continue,

    /// <summary>Parar: passou o tempo de espera e ninguém falou (não vale a pena transcrever).</summary>
    StopNoSpeech,

    /// <summary>Parar: houve fala e depois um silêncio suficientemente longo (acabaste de falar).</summary>
    StopEndOfSpeech,

    /// <summary>Parar: atingiu a duração máxima de gravação.</summary>
    StopMaxDuration
}

/// <summary>
/// Deteção de voz (VAD — "Voice Activity Detection") simples, baseada no volume do áudio.
///
/// Recebe o áudio aos pedaços (como chega do microfone) e decide quando parar de gravar:
///  - se ninguém falar até <c>speechStartTimeout</c> → para, sem fala (evita transcrever ruído,
///    que é quando o Whisper "inventa" frases que ninguém disse);
///  - depois de começares a falar, para quando houver <c>endSilence</c> de silêncio seguido;
///  - nunca grava mais do que <c>maxDuration</c>.
///
/// Não depende do microfone nem do NAudio: recebe bytes e devolve decisões. Por isso é
/// totalmente testável com áudio "sintético" nos testes unitários.
/// </summary>
public class VoiceActivityDetector
{
    // Os primeiros instantes servem para medir o ruído de fundo da sala (ventoinha, PC...).
    private static readonly TimeSpan CalibrationDuration = TimeSpan.FromMilliseconds(300);

    private readonly float _minimumThreshold;
    private readonly TimeSpan _endSilence;
    private readonly TimeSpan _speechStartTimeout;
    private readonly TimeSpan _maxDuration;

    private TimeSpan _elapsed = TimeSpan.Zero;
    private TimeSpan _silenceSinceSpeech = TimeSpan.Zero;
    private double _calibrationSum;
    private int _calibrationChunks;
    private float _threshold;

    /// <param name="minimumThreshold">Volume mínimo (RMS, de 0 a 1) para contar como fala.</param>
    public VoiceActivityDetector(float minimumThreshold, TimeSpan endSilence, TimeSpan speechStartTimeout, TimeSpan maxDuration)
    {
        _minimumThreshold = minimumThreshold;
        _threshold = minimumThreshold;
        _endSilence = endSilence;
        _speechStartTimeout = speechStartTimeout;
        _maxDuration = maxDuration;
    }

    /// <summary>true se, em algum momento, o volume passou o limiar de fala.</summary>
    public bool SpeechDetected { get; private set; }

    /// <summary>Limiar de volume atualmente em uso (já ajustado ao ruído de fundo).</summary>
    public float CurrentThreshold => _threshold;

    /// <summary>
    /// Analisa mais um pedaço de áudio PCM de 16 bits (o formato que gravamos para o Whisper).
    /// </summary>
    public VoiceActivityState Process(ReadOnlySpan<byte> pcm16Audio, TimeSpan chunkDuration)
    {
        _elapsed += chunkDuration;
        var rms = CalculateRms(pcm16Audio);

        if (!SpeechDetected && _elapsed <= CalibrationDuration)
        {
            // Limiar = o maior entre o mínimo configurado e ~2,5x o ruído de fundo,
            // mas nunca mais do que 4x o mínimo (se começares a falar logo, a "calibração"
            // apanha a tua voz e o limiar ficaria alto demais).
            _calibrationSum += rms;
            _calibrationChunks++;
            var noiseFloor = (float)(_calibrationSum / _calibrationChunks);
            _threshold = Math.Clamp(noiseFloor * 2.5f, _minimumThreshold, _minimumThreshold * 4);
        }

        if (rms >= _threshold)
        {
            SpeechDetected = true;
            _silenceSinceSpeech = TimeSpan.Zero;
        }
        else if (SpeechDetected)
        {
            _silenceSinceSpeech += chunkDuration;
        }

        if (_elapsed >= _maxDuration)
        {
            return VoiceActivityState.StopMaxDuration;
        }

        if (!SpeechDetected && _elapsed >= _speechStartTimeout)
        {
            return VoiceActivityState.StopNoSpeech;
        }

        if (SpeechDetected && _silenceSinceSpeech >= _endSilence)
        {
            return VoiceActivityState.StopEndOfSpeech;
        }

        return VoiceActivityState.Continue;
    }

    /// <summary>
    /// "Volume médio" de um pedaço de áudio PCM 16 bits, entre 0 (silêncio) e 1 (máximo).
    /// RMS = raiz quadrada da média dos quadrados das amostras — a medida habitual de volume.
    /// </summary>
    public static float CalculateRms(ReadOnlySpan<byte> pcm16Audio)
    {
        var sampleCount = pcm16Audio.Length / 2;

        if (sampleCount == 0)
        {
            return 0;
        }

        double sumOfSquares = 0;

        for (var i = 0; i < sampleCount; i++)
        {
            // Cada amostra são 2 bytes, "little-endian" (byte menos significativo primeiro).
            short sample = (short)(pcm16Audio[i * 2] | (pcm16Audio[i * 2 + 1] << 8));
            var normalized = sample / 32768.0;
            sumOfSquares += normalized * normalized;
        }

        return (float)Math.Sqrt(sumOfSquares / sampleCount);
    }
}
