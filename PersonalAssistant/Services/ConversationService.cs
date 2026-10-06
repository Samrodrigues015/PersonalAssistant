using System.Text;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public class ConversationService : IConversationService
{
    private readonly List<ChatMessage> _history = new();

    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

    public void AddUserMessage(string content) => _history.Add(new ChatMessage(ChatRole.User, content));

    public void AddAssistantMessage(string content) => _history.Add(new ChatMessage(ChatRole.Assistant, content));

    public void Clear() => _history.Clear();

    public string BuildHistoryText(int maxMessages = 20)
    {
        if (_history.Count == 0)
        {
            return "(sem histórico)";
        }

        var startIndex = Math.Max(0, _history.Count - maxMessages);
        var builder = new StringBuilder();

        for (var i = startIndex; i < _history.Count; i++)
        {
            var message = _history[i];
            var role = message.Role == ChatRole.User ? "Utilizador" : "Assistente";
            builder.AppendLine($"{role}: {message.Content}");
        }

        return builder.ToString().TrimEnd();
    }
}
