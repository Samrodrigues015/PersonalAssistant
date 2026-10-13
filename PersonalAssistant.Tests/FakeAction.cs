using PersonalAssistant.Actions;

namespace PersonalAssistant.Tests;

/// <summary>
/// Ação de teste sem efeitos reais no sistema, usada para verificar o comportamento
/// do ActionService e do AssistantService sem abrir programas de verdade.
/// </summary>
public class FakeAction : IAction
{
    private readonly string? _output;

    /// <param name="output">
    /// Texto que a ação "devolve" (simula, por exemplo, um documento lido). null = ação sem resultado.
    /// </param>
    public FakeAction(string name, bool requiresConfirmation, string? output = null)
    {
        Name = name;
        RequiresConfirmation = requiresConfirmation;
        _output = output;
    }

    public string Name { get; }
    public string Description => "Ação de teste, sem efeitos reais.";
    public bool RequiresConfirmation { get; }

    public bool WasExecuted { get; private set; }
    public string? LastArgument { get; private set; }

    public Task<string?> ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        WasExecuted = true;
        LastArgument = argument;
        return Task.FromResult(_output);
    }
}
