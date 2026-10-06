namespace PersonalAssistant.Actions;

/// <summary>
/// Contrato para uma ação que o assistente pode executar no computador.
/// Cada ação é responsável pela sua própria lógica de execução.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Nome único usado pelo modelo de IA (e pelo ActionService) para identificar a ação.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Descrição legível, incluída no prompt enviado ao modelo, para que este saiba quando usar a ação.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Se true, o AssistantService deve pedir confirmação ao utilizador antes de executar a ação.
    /// </summary>
    bool RequiresConfirmation { get; }

    Task ExecuteAsync(string? argument, CancellationToken cancellationToken = default);
}
