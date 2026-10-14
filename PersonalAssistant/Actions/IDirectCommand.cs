namespace PersonalAssistant.Actions;

/// <summary>
/// Uma ação que também sabe reconhecer SOZINHA frases simples e frequentes
/// ("abre o WhatsApp", "manda à Ester que chego às 8"), sem precisar do modelo de IA.
///
/// Porquê: um modelo local pequeno às vezes "finge" que fez algo em vez de pedir a ação,
/// ou percebe mal pedidos simples. Os assistentes reais (Alexa, Siri) também reconhecem os
/// comandos mais comuns de forma direta e só usam a IA para o resto. Assim, o essencial é
/// rápido e funciona sempre; o modelo fica para o que é mesmo conversa.
///
/// O AssistantService testa estes comandos ANTES de chamar o modelo. Se nenhum reconhecer
/// a frase, segue o caminho normal (modelo de IA).
/// </summary>
public interface IDirectCommand
{
    /// <summary>
    /// true se a frase é claramente um pedido para esta ação; nesse caso devolve também
    /// o argumento a passar ao ExecuteAsync (pode ser null).
    /// </summary>
    bool TryMatch(string userInput, out string? argument);

    /// <summary>Frase curta, para dizer ao utilizador, depois de a ação correr bem.</summary>
    string DescribeSuccess(string? argument);
}
