using System.Text.Json;
using PersonalAssistant.Models;
using Xunit;

namespace PersonalAssistant.Tests;

public class AssistantDecisionTests
{
    [Fact]
    public void Deserialize_ChatDecision_SetsIsChatTrue()
    {
        const string json = "{\"type\": \"chat\", \"message\": \"Olá!\"}";

        var decision = JsonSerializer.Deserialize<AssistantDecision>(json)!;

        Assert.True(decision.IsChat);
        Assert.False(decision.IsAction);
        Assert.Equal("Olá!", decision.Message);
    }

    [Fact]
    public void Deserialize_ActionDecision_SetsIsActionTrue()
    {
        const string json = "{\"type\": \"action\", \"action\": \"open_notepad\", \"argument\": null, \"message\": \"A abrir.\"}";

        var decision = JsonSerializer.Deserialize<AssistantDecision>(json)!;

        Assert.True(decision.IsAction);
        Assert.False(decision.IsChat);
        Assert.Equal("open_notepad", decision.Action);
        Assert.Null(decision.Argument);
    }

    [Fact]
    public void DefaultConstructor_HasChatTypeAndEmptyMessage()
    {
        var decision = new AssistantDecision();

        Assert.Equal("chat", decision.Type);
        Assert.Equal(string.Empty, decision.Message);
    }
}
