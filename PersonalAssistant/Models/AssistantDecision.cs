using System.Text.Json.Serialization;

namespace PersonalAssistant.Models;

/// <summary>
/// Estrutura devolvida (em JSON) pelo modelo de IA, que o AssistantService
/// interpreta para decidir se deve apenas responder ou executar uma ação.
/// </summary>
public class AssistantDecision
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "chat";

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("argument")]
    public string? Argument { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsAction => string.Equals(Type, "action", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsChat => string.Equals(Type, "chat", StringComparison.OrdinalIgnoreCase);
}
