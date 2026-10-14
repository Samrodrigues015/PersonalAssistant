using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

/// <summary>
/// Testes do tratamento de texto à volta da voz: o que o Whisper ouviu (TranscriptFilter)
/// e o que o Piper vai ler (PiperTextToSpeechService.PrepareText / BuildVoiceUrl).
/// </summary>
public class VoiceTextProcessingTests
{
    [Theory]
    [InlineData("Legendas pela comunidade Amara.org")]
    [InlineData("Obrigado por assistir!")]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("[Música]")]
    [InlineData("...")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TranscriptFilter_KnownHallucinationsOrEmpty_ReturnsNull(string? transcript)
    {
        Assert.Null(TranscriptFilter.Clean(transcript));
    }

    [Theory]
    [InlineData(" Abre o WhatsApp. ", "Abre o WhatsApp.")]
    [InlineData("[Música] Manda à Ester que chego às 8", "Manda à Ester que chego às 8")]
    [InlineData("Obrigado", "Obrigado")] // "obrigado" sozinho pode ser real — não é descartado
    public void TranscriptFilter_RealSpeech_IsKeptAndTrimmed(string transcript, string expected)
    {
        Assert.Equal(expected, TranscriptFilter.Clean(transcript));
    }

    [Fact]
    public void PrepareText_RemovesMarkdownAndJoinsLines()
    {
        var text = "Tens **3 tarefas**:\n- Enviar o relatório\n- Marcar consulta\n# Fim";

        var result = PiperTextToSpeechService.PrepareText(text);

        Assert.Equal("Tens 3 tarefas: Enviar o relatório. Marcar consulta. Fim", result);
    }

    [Fact]
    public void PrepareText_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PiperTextToSpeechService.PrepareText("  \n "));
    }

    [Fact]
    public void BuildVoiceUrl_BuildsHuggingFacePathWithEncodedAccents()
    {
        var url = PiperTextToSpeechService.BuildVoiceUrl(
            "https://huggingface.co/rhasspy/piper-voices/resolve/main",
            "pt_PT-tugão-medium",
            ".onnx");

        Assert.Equal(
            "https://huggingface.co/rhasspy/piper-voices/resolve/main/pt/pt_PT/tug%C3%A3o/medium/pt_PT-tug%C3%A3o-medium.onnx",
            url);
    }

    [Fact]
    public void BuildVoiceUrl_InvalidName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PiperTextToSpeechService.BuildVoiceUrl("https://x", "voz-invalida", ".onnx"));
    }
}
