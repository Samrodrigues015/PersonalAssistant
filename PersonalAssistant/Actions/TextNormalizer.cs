using System.Globalization;
using System.Text;

namespace PersonalAssistant.Actions;

/// <summary>
/// Normaliza texto para comparações "tolerantes": "  Mãe " → "mae".
/// Útil sobretudo quando o pedido vem do reconhecimento de voz, que nem sempre acerta
/// nos acentos ou nas maiúsculas. Usado pelas ações do WhatsApp e dos documentos.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string text)
    {
        // FormD decompõe "ã" em "a" + til; depois descartamos os acentos (NonSpacingMark).
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
