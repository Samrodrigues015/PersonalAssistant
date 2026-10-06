using PersonalAssistant.Actions;

namespace PersonalAssistant.Tests;

/// <summary>
/// Ação de teste sem efeitos reais no sistema, usada para verificar o comportamento
/// do ActionService e do AssistantService sem abrir programas de verdade.
/// </summary>
public class FakeAction : IAction
{
    public FakeAction(string name, bool requiresConfirmation)
    {
        Name = name;
        RequiresConfirmation = requiresConfirmation;
    }

    public string Name { get; }
    public string Description => "Ação de teste, sem efeitos reais.";
    public bool RequiresConfirmation { get; }

    public bool WasExecuted { get; private set; }
    public string? LastArgument { get; private set; }

    public Task ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        WasExecuted = true;
        LastArgument = argument;
        return Task.CompletedTask;
    }
}
