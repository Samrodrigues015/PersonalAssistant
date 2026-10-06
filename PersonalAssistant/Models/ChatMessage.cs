namespace PersonalAssistant.Models;

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public ChatMessage()
    {
    }

    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = content;
        Timestamp = DateTime.Now;
    }
}
