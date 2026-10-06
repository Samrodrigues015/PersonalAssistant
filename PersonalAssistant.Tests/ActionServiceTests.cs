using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Actions;
using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

public class ActionServiceTests
{
    [Fact]
    public void GetAvailableActions_ReturnsAllRegisteredActions()
    {
        var actions = new IAction[]
        {
            new FakeAction("action_a", requiresConfirmation: false),
            new FakeAction("action_b", requiresConfirmation: false)
        };

        var sut = new ActionService(actions, NullLogger<ActionService>.Instance);

        Assert.Equal(2, sut.GetAvailableActions().Count);
    }

    [Fact]
    public void TryGetAction_WhenActionExists_ReturnsTrue()
    {
        var actions = new IAction[] { new FakeAction("action_a", requiresConfirmation: false) };
        var sut = new ActionService(actions, NullLogger<ActionService>.Instance);

        var found = sut.TryGetAction("action_a", out var action);

        Assert.True(found);
        Assert.NotNull(action);
    }

    [Fact]
    public void TryGetAction_WhenActionDoesNotExist_ReturnsFalse()
    {
        var sut = new ActionService(Array.Empty<IAction>(), NullLogger<ActionService>.Instance);

        var found = sut.TryGetAction("nao_existe", out var action);

        Assert.False(found);
        Assert.Null(action);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionExists_CallsExecuteOnce()
    {
        var fakeAction = new FakeAction("action_a", requiresConfirmation: false);
        var sut = new ActionService(new IAction[] { fakeAction }, NullLogger<ActionService>.Instance);

        await sut.ExecuteAsync("action_a", "arg");

        Assert.True(fakeAction.WasExecuted);
        Assert.Equal("arg", fakeAction.LastArgument);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionDoesNotExist_ThrowsActionNotFoundException()
    {
        var sut = new ActionService(Array.Empty<IAction>(), NullLogger<ActionService>.Instance);

        await Assert.ThrowsAsync<ActionNotFoundException>(() => sut.ExecuteAsync("nao_existe", null));
    }
}
