using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PersonalAssistant.Actions;

/// <summary>
/// Extrai o texto simples de um documento, conforme a extensão:
///  - .txt / .md → lido diretamente;
///  - .docx      → DocumentFormat.OpenXml (biblioteca oficial da Microsoft para ficheiros Office);
///  - .pdf       → PdfPig (só PDFs com texto; PDFs digitalizados como imagem não têm texto para extrair).
/// </summary>
public static class DocumentTextExtractor
{
    public static readonly IReadOnlyCollection<string> SupportedExtensions = new[] { ".txt", ".md", ".docx", ".pdf" };

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <exception cref="NotSupportedException">A extensão do ficheiro não é suportada.</exception>
    public static string ExtractText(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".txt" or ".md" => File.ReadAllText(path),
            ".docx" => ExtractFromDocx(path),
            ".pdf" => ExtractFromPdf(path),
            _ => throw new NotSupportedException(
                $"Não sei ler ficheiros '{extension}'. Formatos suportados: {string.Join(", ", SupportedExtensions)}.")
        };
    }

    private static string ExtractFromDocx(string path)
    {
        // false = abrir só para leitura (nunca alteramos o documento).
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return string.Empty;
        }

        // Um parágrafo do Word = uma linha de texto. InnerText junta todos os "runs"
        // (pedaços com formatação diferente) do parágrafo.
        var builder = new StringBuilder();

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            builder.AppendLine(paragraph.InnerText);
        }

        return builder.ToString();
    }

    private static string ExtractFromPdf(string path)
    {
        using var document = PdfDocument.Open(path);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            // ContentOrderTextExtractor tenta respeitar a ordem de leitura e os espaços entre palavras
            // (page.Text, mais simples, às vezes cola as palavras umas às outras).
            builder.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        return builder.ToString();
    }
}
