namespace PersonalAssistant.Services;

/// <summary>
/// Orquestra o fluxo principal: recebe texto do utilizador, decide (com ajuda do Ollama)
/// se deve responder normalmente ou executar uma ação, e devolve a resposta final a mostrar.
/// </summary>
public interface IAssistantService
{
    Task<string> ProcessAsync(string userInput, CancellationToken cancellationToken = default);
}
