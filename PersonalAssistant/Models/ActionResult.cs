namespace PersonalAssistant.Models;

/// <summary>
/// Resultado da execução de uma ação pelo ActionService.
/// </summary>
/// <param name="Message">Mensagem curta para o utilizador (ex.: "Ação 'open_notepad' executada com sucesso.").</param>
/// <param name="Output">
/// Texto que a ação devolveu para a IA usar (ex.: conteúdo de um documento), ou <c>null</c>
/// se a ação não tem nada para devolver.
/// </param>
public sealed record ActionResult(string Message, string? Output)
{
    public bool HasOutput => !string.IsNullOrWhiteSpace(Output);
}
