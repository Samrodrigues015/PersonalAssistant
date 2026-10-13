using Microsoft.Extensions.Options;
using PersonalAssistant.Configuration;

namespace PersonalAssistant.Actions;

/// <summary>
/// Lê um documento (.txt, .md, .docx, .pdf) da pasta de documentos do assistente e devolve
/// o texto à IA, para ela o resumir, listar tarefas ou responder a perguntas sobre ele.
///
/// Segurança:
///  - só lê ficheiros DENTRO da pasta configurada (Documents:Folder); caminhos como
///    "..\..\Windows\..." ou "C:\outra\pasta" são recusados;
///  - só lê (nunca altera nem apaga), por isso não pede confirmação;
///  - o texto lido é tratado como informação, nunca como instruções (ver AssistantService).
/// </summary>
public class ReadDocumentAction : IAction
{
    private readonly DocumentSettings _settings;

    public ReadDocumentAction(IOptions<DocumentSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Name => "read_document";

    public string Description =>
        "Lê um documento (.txt, .md, .docx ou .pdf) da pasta de documentos do utilizador e devolve o texto, " +
        "para poderes resumi-lo, dizer que tarefas lá estão ou responder a perguntas sobre ele. " +
        "O argumento é o nome do ficheiro (ex.: \"tarefas.docx\" ou só \"tarefas\"). " +
        "Sem argumento, devolve a lista de documentos disponíveis.";

    public bool RequiresConfirmation => false;

    /// <summary>A pasta configurada, já com as variáveis de ambiente expandidas e em caminho absoluto.</summary>
    public string FolderPath => Path.GetFullPath(Environment.ExpandEnvironmentVariables(_settings.Folder));

    public Task<string?> ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        // Cria a pasta na primeira utilização, para ficar claro onde pôr os documentos.
        Directory.CreateDirectory(FolderPath);

        if (string.IsNullOrWhiteSpace(argument))
        {
            return Task.FromResult<string?>(BuildDocumentList());
        }

        var path = ResolveDocumentPath(argument);
        var text = DocumentTextExtractor.ExtractText(path).Trim();

        if (text.Length == 0)
        {
            return Task.FromResult<string?>(
                $"O documento '{Path.GetFileName(path)}' não tem texto que eu consiga ler " +
                "(se for um PDF digitalizado como imagem, não tem texto, só a imagem).");
        }

        if (text.Length > _settings.MaxCharacters)
        {
            text = text[.._settings.MaxCharacters] +
                   $"\n[... documento cortado: só foram lidos os primeiros {_settings.MaxCharacters} caracteres]";
        }

        return Task.FromResult<string?>($"Documento: {Path.GetFileName(path)}\n\n{text}");
    }

    /// <summary>
    /// Descobre que ficheiro o utilizador quer, SEM sair da pasta configurada.
    /// Primeiro tenta o nome exato; se não existir, procura por nome parecido
    /// ("tarefas" → "Tarefas da semana.docx"), útil quando o pedido vem por voz.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">O caminho pedido sai da pasta permitida.</exception>
    /// <exception cref="FileNotFoundException">Nenhum documento corresponde ao nome (ou há vários).</exception>
    public string ResolveDocumentPath(string argument)
    {
        var folder = FolderPath;
        var requested = argument.Trim().Trim('"', '\'');

        // 1) Nome/caminho exato, mas obrigatoriamente dentro da pasta.
        var candidate = Path.GetFullPath(Path.Combine(folder, requested));

        if (!IsInsideFolder(candidate, folder))
        {
            throw new UnauthorizedAccessException(
                $"Só posso ler documentos dentro da pasta '{folder}'.");
        }

        if (File.Exists(candidate) && DocumentTextExtractor.IsSupported(candidate))
        {
            return candidate;
        }

        // 2) Procura tolerante: sem maiúsculas, sem acentos, com ou sem extensão.
        var wanted = TextNormalizer.Normalize(Path.GetFileNameWithoutExtension(requested));
        var documents = GetDocuments(folder);

        var exactMatches = documents
            .Where(d => TextNormalizer.Normalize(Path.GetFileNameWithoutExtension(d)) == wanted)
            .ToList();

        var matches = exactMatches.Count > 0
            ? exactMatches
            : documents.Where(d => TextNormalizer.Normalize(Path.GetFileNameWithoutExtension(d)).Contains(wanted)).ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new FileNotFoundException(
                $"Não encontrei nenhum documento chamado '{requested}'. {DescribeAvailable(documents)}"),
            _ => throw new FileNotFoundException(
                $"Há vários documentos parecidos com '{requested}': " +
                $"{string.Join(", ", matches.Select(Path.GetFileName))}. Diz qual queres.")
        };
    }

    private string BuildDocumentList()
    {
        var documents = GetDocuments(FolderPath);

        return documents.Count == 0
            ? $"A pasta de documentos ('{FolderPath}') está vazia."
            : $"Documentos disponíveis na pasta '{FolderPath}':\n" +
              string.Join("\n", documents.Select(d => "- " + Path.GetRelativePath(FolderPath, d)));
    }

    private string DescribeAvailable(IReadOnlyCollection<string> documents) =>
        documents.Count == 0
            ? $"A pasta '{FolderPath}' está vazia."
            : "Documentos disponíveis: " + string.Join(", ", documents.Select(Path.GetFileName)) + ".";

    private static List<string> GetDocuments(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(DocumentTextExtractor.IsSupported)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    /// <summary>
    /// true se <paramref name="path"/> está dentro de <paramref name="folder"/> (ou subpastas).
    /// Compara com o separador no fim para "C:\Docs\Assistente2" não passar por "C:\Docs\Assistente".
    /// </summary>
    private static bool IsInsideFolder(string path, string folder)
    {
        var folderWithSeparator = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        return path.StartsWith(folderWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
