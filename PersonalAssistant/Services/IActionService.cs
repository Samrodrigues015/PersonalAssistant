using PersonalAssistant.Actions;

namespace PersonalAssistant.Services;

/// <summary>
/// Regista as ações disponíveis (recebidas via Dependency Injection) e é responsável
/// por validar e executar ações pedidas pelo modelo de IA. É a "fronteira de segurança":
/// só ações aqui registadas podem ser executadas.
/// </summary>
public interface IActionService
{
    IReadOnlyCollection<IAction> GetAvailableActions();

    bool TryGetAction(string name, out IAction? action);

    /// <exception cref="ActionNotFoundException">A ação pedida não existe.</exception>
    /// <exception cref="ActionExecutionException">A ação existe mas falhou ao executar.</exception>
    Task<string> ExecuteAsync(string name, string? argument, CancellationToken cancellationToken = default);
}
