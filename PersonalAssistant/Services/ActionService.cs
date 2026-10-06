using Microsoft.Extensions.Logging;
using PersonalAssistant.Actions;

namespace PersonalAssistant.Services;

public class ActionService : IActionService
{
    private readonly Dictionary<string, IAction> _actions;
    private readonly ILogger<ActionService> _logger;

    public ActionService(IEnumerable<IAction> actions, ILogger<ActionService> logger)
    {
        _actions = actions.ToDictionary(a => a.Name, a => a, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public IReadOnlyCollection<IAction> GetAvailableActions() => _actions.Values.ToList().AsReadOnly();

    public bool TryGetAction(string name, out IAction? action) => _actions.TryGetValue(name, out action);

    public async Task<string> ExecuteAsync(string name, string? argument, CancellationToken cancellationToken = default)
    {
        if (!_actions.TryGetValue(name, out var action))
        {
            _logger.LogWarning("Foi pedida a ação '{Action}', que não existe ou não está registada", name);
            throw new ActionNotFoundException($"A ação '{name}' não existe ou não está registada.");
        }

        try
        {
            _logger.LogInformation("A executar a ação '{Action}' com argumento '{Argument}'", name, argument);
            await action.ExecuteAsync(argument, cancellationToken);
            return $"Ação '{name}' executada com sucesso.";
        }
        catch (Exception ex) when (ex is not ActionNotFoundException)
        {
            _logger.LogError(ex, "Erro ao executar a ação '{Action}'", name);
            throw new ActionExecutionException($"Ocorreu um erro ao executar a ação '{name}'.", ex);
        }
    }
}
