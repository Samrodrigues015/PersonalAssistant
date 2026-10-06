using PersonalAssistant.Models;
using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

public class ConversationServiceTests
{
    [Fact]
    public void AddUserMessage_AddsMessageWithUserRole()
    {
        var sut = new ConversationService();

        sut.AddUserMessage("Olá");

        Assert.Single(sut.History);
        Assert.Equal(ChatRole.User, sut.History[0].Role);
        Assert.Equal("Olá", sut.History[0].Content);
    }

    [Fact]
    public void AddAssistantMessage_AddsMessageWithAssistantRole()
    {
        var sut = new ConversationService();

        sut.AddAssistantMessage("Olá! Como posso ajudar?");

        Assert.Single(sut.History);
        Assert.Equal(ChatRole.Assistant, sut.History[0].Role);
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        var sut = new ConversationService();
        sut.AddUserMessage("Olá");
        sut.AddAssistantMessage("Oi!");

        sut.Clear();

        Assert.Empty(sut.History);
    }

    [Fact]
    public void BuildHistoryText_WhenEmpty_ReturnsPlaceholder()
    {
        var sut = new ConversationService();

        var text = sut.BuildHistoryText();

        Assert.Equal("(sem histórico)", text);
    }

    [Fact]
    public void BuildHistoryText_IncludesAllMessagesWithinLimit()
    {
        var sut = new ConversationService();
        sut.AddUserMessage("O meu nome é Samara.");
        sut.AddAssistantMessage("Prazer, Samara!");

        var text = sut.BuildHistoryText();

        Assert.Contains("O meu nome é Samara.", text);
        Assert.Contains("Prazer, Samara!", text);
    }
}
