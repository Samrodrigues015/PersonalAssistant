using PersonalAssistant.Actions;

namespace PersonalAssistant.Services;

/// <summary>
/// Limpa o texto devolvido pelo Whisper e descarta as "alucinações" mais conhecidas.
///
/// O Whisper foi treinado com muitas legendas de vídeos. Quando recebe silêncio ou ruído,
/// tende a "inventar" frases típicas de legendas ("Legendas pela comunidade Amara.org",
/// "Obrigado por assistirem", "[Música]"...). Estas frases quase nunca são o que disseste.
/// </summary>
public static class TranscriptFilter
{
    private static readonly string[] KnownHallucinations =
    {
        "amara.org",
        "legendas pela comunidade",
        "legendado por",
        "legendas por",
        "obrigado por assistir",
        "obrigada por assistir",
        "inscreva-se",
        "subscreve o canal",
        "subtitles by",
        "thanks for watching",
        "thank you for watching"
    };

    /// <summary>
    /// Devolve o texto limpo, ou null se não sobrar nada que pareça fala real.
    /// </summary>
    public static string? Clean(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return null;
        }

        // Remove marcações que o Whisper põe entre parênteses retos, ex.: "[BLANK_AUDIO]", "[Música]".
        var text = System.Text.RegularExpressions.Regex.Replace(transcript, @"\[[^\]]*\]|\([^\)]*\)", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        // Só pontuação (ex.: "...", "!") não é fala.
        if (!text.Any(char.IsLetterOrDigit))
        {
            return null;
        }

        var normalized = TextNormalizer.Normalize(text);

        if (KnownHallucinations.Any(h => normalized.Contains(TextNormalizer.Normalize(h))))
        {
            return null;
        }

        return text;
    }
}
