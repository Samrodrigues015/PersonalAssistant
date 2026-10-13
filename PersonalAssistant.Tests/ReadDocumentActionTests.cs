using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using PersonalAssistant.Actions;
using PersonalAssistant.Configuration;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace PersonalAssistant.Tests;

/// <summary>
/// Cada teste usa uma pasta temporária própria (criada no construtor e apagada no Dispose),
/// para não depender dos teus documentos reais nem deixar lixo no disco.
/// O xUnit cria uma instância nova da classe para cada teste, por isso as pastas nunca se misturam.
/// </summary>
public class ReadDocumentActionTests : IDisposable
{
    private readonly string _folder;

    public ReadDocumentActionTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "PersonalAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ReadDocumentAction CreateSut(int maxCharacters = 12000) =>
        new(Options.Create(new DocumentSettings { Folder = _folder, MaxCharacters = maxCharacters }));

    private string CreateTextFile(string name, string content)
    {
        var path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task ExecuteAsync_TextFile_ReturnsItsContent()
    {
        CreateTextFile("tarefas.txt", "- Comprar pão\n- Ligar à Ana");

        var output = await CreateSut().ExecuteAsync("tarefas.txt");

        Assert.NotNull(output);
        Assert.Contains("tarefas.txt", output);
        Assert.Contains("Comprar pão", output);
    }

    [Theory]
    [InlineData("tarefas")]           // sem extensão
    [InlineData("TAREFAS")]           // maiúsculas
    [InlineData("tarefas da semana")] // nome completo sem extensão
    [InlineData("semana")]            // parte do nome
    public void ResolveDocumentPath_FindsDocumentByApproximateName(string requested)
    {
        var expected = CreateTextFile("Tarefas da Semana.md", "x");

        var path = CreateSut().ResolveDocumentPath(requested);

        Assert.Equal(expected, path);
    }

    [Fact]
    public void ResolveDocumentPath_IgnoresAccents()
    {
        var expected = CreateTextFile("Reunião.txt", "x");

        var path = CreateSut().ResolveDocumentPath("reuniao");

        Assert.Equal(expected, path);
    }

    [Theory]
    [InlineData(@"..\segredo.txt")]
    [InlineData("../segredo.txt")]
    [InlineData(@"C:\Windows\win.ini")]
    public void ResolveDocumentPath_PathOutsideFolder_IsRefused(string requested)
    {
        Assert.Throws<UnauthorizedAccessException>(() => CreateSut().ResolveDocumentPath(requested));
    }

    [Fact]
    public void ResolveDocumentPath_UnknownDocument_ListsAvailableOnes()
    {
        CreateTextFile("compras.txt", "x");

        var ex = Assert.Throws<FileNotFoundException>(() => CreateSut().ResolveDocumentPath("férias"));

        Assert.Contains("compras.txt", ex.Message);
    }

    [Fact]
    public void ResolveDocumentPath_SeveralSimilarDocuments_AsksWhichOne()
    {
        CreateTextFile("notas reuniao 1.txt", "x");
        CreateTextFile("notas reuniao 2.txt", "x");

        var ex = Assert.Throws<FileNotFoundException>(() => CreateSut().ResolveDocumentPath("notas"));

        Assert.Contains("vários", ex.Message);
    }

    [Fact]
    public void ResolveDocumentPath_UnsupportedFilesAreIgnored()
    {
        CreateTextFile("tarefas.exe", "x");

        Assert.Throws<FileNotFoundException>(() => CreateSut().ResolveDocumentPath("tarefas"));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutArgument_ListsDocuments_IncludingSubfolders()
    {
        CreateTextFile("a.txt", "x");
        CreateTextFile(Path.Combine("trabalho", "b.md"), "x");

        var output = await CreateSut().ExecuteAsync(null);

        Assert.Contains("a.txt", output);
        Assert.Contains("b.md", output);
    }

    [Fact]
    public async Task ExecuteAsync_LongDocument_IsCutToMaxCharacters()
    {
        CreateTextFile("longo.txt", new string('a', 500));

        var output = await CreateSut(maxCharacters: 100).ExecuteAsync("longo.txt");

        Assert.Contains("documento cortado", output);
        Assert.DoesNotContain(new string('a', 101), output);
    }

    [Fact]
    public async Task ExecuteAsync_DocxFile_ReturnsParagraphText()
    {
        var path = Path.Combine(_folder, "plano.docx");

        // Cria um .docx de verdade com dois parágrafos.
        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Preparar apresentação"))),
                new Paragraph(new Run(new Text("Enviar relatório à equipa")))));
            mainPart.Document.Save();
        }

        var output = await CreateSut().ExecuteAsync("plano");

        Assert.Contains("Preparar apresentação", output);
        Assert.Contains("Enviar relatório à equipa", output);
    }

    [Fact]
    public async Task ExecuteAsync_PdfFile_ReturnsText()
    {
        var path = Path.Combine(_folder, "lista.pdf");

        // Cria um PDF simples com texto, usando o "escritor" do próprio PdfPig.
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Pagar a renda", 12, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());

        var output = await CreateSut().ExecuteAsync("lista.pdf");

        Assert.Contains("Pagar", output);
        Assert.Contains("renda", output);
    }
}
