using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

/// <summary>
/// Testa a deteção de voz com áudio "sintético": em vez do microfone, geramos pedaços de
/// 100 ms de silêncio ou de "fala" (um tom com volume alto) e vemos o que o detetor decide.
/// </summary>
public class VoiceActivityDetectorTests
{
    private static readonly TimeSpan Chunk = TimeSpan.FromMilliseconds(100);
    private const int SamplesPerChunk = 1600; // 16 000 amostras/s × 0,1 s

    private static VoiceActivityDetector CreateSut() => new(
        minimumThreshold: 0.015f,
        endSilence: TimeSpan.FromSeconds(1),
        speechStartTimeout: TimeSpan.FromSeconds(3),
        maxDuration: TimeSpan.FromSeconds(10));

    /// <summary>PCM 16 bits de uma onda quadrada com a amplitude dada (0 = silêncio, 1 = máximo).</summary>
    private static byte[] Audio(double amplitude)
    {
        var bytes = new byte[SamplesPerChunk * 2];

        for (var i = 0; i < SamplesPerChunk; i++)
        {
            var sample = (short)((i % 2 == 0 ? 1 : -1) * amplitude * short.MaxValue);
            bytes[i * 2] = (byte)(sample & 0xFF);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return bytes;
    }

    private static readonly byte[] Silence = Audio(0);
    private static readonly byte[] QuietNoise = Audio(0.003);
    private static readonly byte[] Speech = Audio(0.2);

    [Fact]
    public void CalculateRms_Silence_IsZero()
    {
        Assert.Equal(0f, VoiceActivityDetector.CalculateRms(Silence));
    }

    [Fact]
    public void CalculateRms_SquareWave_EqualsItsAmplitude()
    {
        // Numa onda quadrada, o RMS é igual à amplitude.
        Assert.Equal(0.2f, VoiceActivityDetector.CalculateRms(Speech), precision: 3);
    }

    [Fact]
    public void Process_OnlySilence_StopsWithNoSpeechAfterTimeout()
    {
        var sut = CreateSut();
        var state = VoiceActivityState.Continue;
        var chunks = 0;

        while (state == VoiceActivityState.Continue && chunks < 100)
        {
            state = sut.Process(QuietNoise, Chunk);
            chunks++;
        }

        Assert.Equal(VoiceActivityState.StopNoSpeech, state);
        Assert.False(sut.SpeechDetected);
        Assert.Equal(30, chunks); // 3 segundos de espera = 30 pedaços de 0,1 s
    }

    [Fact]
    public void Process_SpeechThenSilence_StopsAtEndOfSpeech()
    {
        var sut = CreateSut();

        // 0,5 s de silêncio, 2 s a falar...
        for (var i = 0; i < 5; i++) Assert.Equal(VoiceActivityState.Continue, sut.Process(Silence, Chunk));
        for (var i = 0; i < 20; i++) Assert.Equal(VoiceActivityState.Continue, sut.Process(Speech, Chunk));

        // ...e depois silêncio: deve parar ao fim de 1 s (10 pedaços), não antes.
        for (var i = 0; i < 9; i++) Assert.Equal(VoiceActivityState.Continue, sut.Process(Silence, Chunk));
        Assert.Equal(VoiceActivityState.StopEndOfSpeech, sut.Process(Silence, Chunk));
        Assert.True(sut.SpeechDetected);
    }

    [Fact]
    public void Process_ShortPauseWhileSpeaking_DoesNotStop()
    {
        var sut = CreateSut();

        for (var i = 0; i < 10; i++) sut.Process(Speech, Chunk);
        // Pausa de 0,5 s a meio da frase (respirar) — não pode parar.
        for (var i = 0; i < 5; i++) Assert.Equal(VoiceActivityState.Continue, sut.Process(Silence, Chunk));
        for (var i = 0; i < 10; i++) Assert.Equal(VoiceActivityState.Continue, sut.Process(Speech, Chunk));
    }

    [Fact]
    public void Process_SpeakingNonStop_StopsAtMaxDuration()
    {
        var sut = CreateSut();
        var state = VoiceActivityState.Continue;
        var chunks = 0;

        while (state == VoiceActivityState.Continue && chunks < 1000)
        {
            state = sut.Process(Speech, Chunk);
            chunks++;
        }

        Assert.Equal(VoiceActivityState.StopMaxDuration, state);
        Assert.Equal(100, chunks); // 10 s máximo
    }

    [Fact]
    public void Process_NoisyRoom_RaisesThresholdSoNoiseIsNotSpeech()
    {
        var sut = CreateSut();
        var noisyRoom = Audio(0.02); // acima do mínimo (0,015), mas é só ruído constante

        // Calibração (primeiros 300 ms) com o ruído da sala.
        for (var i = 0; i < 3; i++) sut.Process(noisyRoom, Chunk);

        Assert.True(sut.CurrentThreshold > 0.02f);
    }
}
