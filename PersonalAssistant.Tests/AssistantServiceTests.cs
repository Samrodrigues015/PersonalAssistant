using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PersonalAssistant.Actions;
using PersonalAssistant.Configuration;
using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

public class AssistantServiceTests
{
    private static IOptions<AssistantSettings> DefaultAssistantSettings => Options.Create(new AssistantSettings
    {
        Name = "TestAssistant",
        Language = "pt-PT"
    });

    [Fact]
    public async Task ProcessAsync_WhenModelReturnsChatDecision_ReturnsMessage()
    {
        var ollamaMock = new Mock<IOllamaService>();
        ollamaMock
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"type\": \"chat\", \"message\": \"Olá! Como posso ajudar?\"}");

        var conversationService = new ConversationService();
        var actionService = new ActionService(Array.Empty<IAction>(), NullLogger<ActionService>.Instance);

        var sut = new AssistantService(
            ollamaMock.Object,
            conversationService,
            actionService,
            DefaultAssistantSettings,
            NullLogger<AssistantService>.Instance);

        var result = await sut.ProcessAsync("Olá");

        Assert.Equal("Olá! Como posso ajudar?", result);
        Assert.Equal(2, conversationService.History.Count);
    }

    [Fact]
    public async Task ProcessAsync_WhenModelRequestsKnownAction_ExecutesAction()
    {
        var ollamaMock = new Mock<IOllamaService>();
        ollamaMock
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"type\": \"action\", \"action\": \"fake_action\", \"argument\": null, \"message\": \"Vou executar.\"}");

        var fakeAction = new FakeAction("fake_action", requiresConfirmation: false);
        var actionService = new ActionService(new IAction[] { fakeAction }, NullLogger<ActionService>.Instance);
        var conversationService = new ConversationService();

        var sut = new AssistantService(
            ollamaMock.Object,
            conversationService,
            actionService,
            DefaultAssistantSettings,
            NullLogger<AssistantService>.Instance);

        await sut.ProcessAsync("Faz a ação falsa");

        Assert.True(fakeAction.WasExecuted);
    }

    [Fact]
    public async Task ProcessAsync_WhenModelRequestsUnknownAction_DoesNotThrow()
    {
        var ollamaMock = new Mock<IOllamaService>();
        ollamaMock
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"type\": \"action\", \"action\": \"acao_inexistente\", \"argument\": null, \"message\": \"\"}");

        var actionService = new ActionService(Array.Empty<IAction>(), NullLogger<ActionService>.Instance);
        var conversationService = new ConversationService();

        var sut = new AssistantService(
            ollamaMock.Object,
            conversationService,
            actionService,
            DefaultAssistantSettings,
            NullLogger<AssistantService>.Instance);

        var result = await sut.ProcessAsync("Faz algo estranho");

        Assert.Contains("não está registada", result);
    }

    [Fact]
    public async Task ProcessAsync_WhenActionRequiresConfirmationAndUserConfirms_ExecutesAction()
    {
        var ollamaMock = new Mock<IOllamaService>();
        ollamaMock
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"type\": \"action\", \"action\": \"fake_action\", \"argument\": null, \"message\": \"Confirmas?\"}");

        var fakeAction = new FakeAction("fake_action", requiresConfirmation: true);
        var actionService = new ActionService(new IAction[] { fakeAction }, NullLogger<ActionService>.Instance);
        var conversationService = new ConversationService();

        var sut = new AssistantService(
            ollamaMock.Object,
            conversationService,
            actionService,
            DefaultAssistantSettings,
            NullLogger<AssistantService>.Instance);

        var firstResponse = await sut.ProcessAsync("Faz a ação falsa");
        Assert.Contains("sim/não", firstResponse);
        Assert.False(fakeAction.WasExecuted);

        await sut.ProcessAsync("sim");
        Assert.True(fakeAction.WasExecuted);
    }

    [Fact]
    public async Task ProcessAsync_WhenActionRequiresConfirmationAndUserDeclines_DoesNotExecute()
    {
        var ollamaMock = new Mock<IOllamaService>();
        ollamaMock
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"type\": \"action\", \"action\": \"fake_action\", \"argument\": null, \"message\": \"Confirmas?\"}");

        var fakeAction = new FakeAction("fake_action", requiresConfirmation: true);
        var actionService = new ActionService(new IAction[] { fakeAction }, NullLogger<ActionService>.Instance);
        var conversationService = new ConversationService();

        var sut = new AssistantService(
            ollamaMock.Object,
            conversationService,
            actionService,
            DefaultAssistantSettings,
            NullLogger<AssistantService>.Instance);

        await sut.ProcessAsync("Faz a ação falsa");
        await sut.ProcessAsync("não");

        Assert.False(fakeAction.WasExecuted);
    }
}
