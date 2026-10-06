using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

/// <summary>
/// Guarda o histórico da conversa atual (em memória, não persistido) e sabe
/// formatá-lo como texto para ser incluído no prompt enviado ao modelo.
/// </summary>
public interface IConversationService
{
    IReadOnlyList<ChatMessage> History { get; }

    void AddUserMessage(string content);
    void AddAssistantMessage(string content);
    void Clear();

    /// <summary>
    /// Devolve as últimas <paramref name="maxMessages"/> mensagens formatadas como texto simples.
    /// </summary>
    string BuildHistoryText(int maxMessages = 20);
}
