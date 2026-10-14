using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

public class FallbackTextToSpeechServiceTests
{
    private static FallbackTextToSpeechService CreateSut(Mock<ITextToSpeechService> primary, Mock<ITextToSpeechService> fallback) =>
        new(primary.Object, fallback.Object, NullLogger<FallbackTextToSpeechService>.Instance);

    [Fact]
    public async Task SpeakAsync_PrimaryWorks_FallbackIsNotUsed()
    {
        var primary = new Mock<ITextToSpeechService>();
        var fallback = new Mock<ITextToSpeechService>();

        await CreateSut(primary, fallback).SpeakAsync("olá");

        primary.Verify(p => p.SpeakAsync("olá", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(f => f.SpeakAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SpeakAsync_PrimaryFails_UsesFallback_AndStopsTryingPrimary()
    {
        var primary = new Mock<ITextToSpeechService>();
        primary
            .Setup(p => p.SpeakAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SpeechServiceException("sem internet"));
        var fallback = new Mock<ITextToSpeechService>();
        var sut = CreateSut(primary, fallback);

        await sut.SpeakAsync("um");
        await sut.SpeakAsync("dois");

        primary.Verify(p => p.SpeakAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(f => f.SpeakAsync("um", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(f => f.SpeakAsync("dois", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_Cancelled_DoesNotSwitchToFallback()
    {
        var primary = new Mock<ITextToSpeechService>();
        primary
            .Setup(p => p.SpeakAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var fallback = new Mock<ITextToSpeechService>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut(primary, fallback).SpeakAsync("olá"));

        fallback.Verify(f => f.SpeakAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
