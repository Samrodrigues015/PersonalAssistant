using Microsoft.Extensions.Options;
using PersonalAssistant.Actions;
using PersonalAssistant.Configuration;
using Xunit;

namespace PersonalAssistant.Tests;

/// <summary>
/// Testa só a lógica de "que endereço abrir" (ResolveTarget) — nunca o ExecuteAsync,
/// para que correr os testes (no teu PC ou no CI) não abra o WhatsApp de verdade.
/// </summary>
public class OpenWhatsAppActionTests
{
    private static OpenWhatsAppAction CreateSut(Dictionary<string, string>? contacts = null) =>
        new(Options.Create(new WhatsAppSettings
        {
            Contacts = contacts ?? new Dictionary<string, string>
            {
                ["Ana"] = "351910000000",
                ["Mãe"] = "+351 920 000 000"
            }
        }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveTarget_WithoutArgument_OpensOnlyTheApp(string? argument)
    {
        var sut = CreateSut();

        var target = sut.ResolveTarget(argument);

        Assert.Equal("whatsapp://", target.DesktopUri);
        Assert.Equal("https://web.whatsapp.com/", target.WebUri);
    }

    [Fact]
    public void ResolveTarget_WithContactAndMessage_BuildsChatWithEncodedMessage()
    {
        var sut = CreateSut();

        var target = sut.ResolveTarget("Ana: chego às 8");

        Assert.Equal("whatsapp://send?phone=351910000000&text=chego%20%C3%A0s%208", target.DesktopUri);
        Assert.Equal("https://wa.me/351910000000?text=chego%20%C3%A0s%208", target.WebUri);
    }

    [Fact]
    public void ResolveTarget_WithOnlyContactName_OpensChatWithoutText()
    {
        var sut = CreateSut();

        var target = sut.ResolveTarget("Ana");

        Assert.Equal("whatsapp://send?phone=351910000000", target.DesktopUri);
        Assert.Equal("https://wa.me/351910000000", target.WebUri);
    }

    [Fact]
    public void ResolveTarget_MessageWithColon_SplitsOnlyOnFirstColon()
    {
        var sut = CreateSut();

        var target = sut.ResolveTarget("Ana: jantar às 20:30");

        Assert.Contains("text=jantar%20%C3%A0s%2020%3A30", target.DesktopUri);
    }

    [Theory]
    [InlineData("mae: liga-me")]
    [InlineData("MÃE: liga-me")]
    [InlineData("  Mãe  : liga-me")]
    public void ResolveTarget_ContactName_IgnoresCaseAccentsAndSpaces(string argument)
    {
        var sut = CreateSut();

        var target = sut.ResolveTarget(argument);

        // O número configurado tinha "+" e espaços — ficam só os dígitos.
        Assert.StartsWith("whatsapp://send?phone=351920000000", target.DesktopUri);
    }

    [Fact]
    public void ResolveTarget_UnknownContact_ThrowsWithKnownContacts()
    {
        var sut = CreateSut();

        var ex = Assert.Throws<InvalidOperationException>(() => sut.ResolveTarget("Joaquim: olá"));

        Assert.Contains("Joaquim", ex.Message);
        Assert.Contains("Ana", ex.Message);
    }

    [Fact]
    public void Description_ListsConfiguredContacts()
    {
        var sut = CreateSut();

        Assert.Contains("Ana", sut.Description);
        Assert.Contains("Mãe", sut.Description);
    }

    [Fact]
    public void Description_WithoutContacts_SaysOnlyTheAppCanBeOpened()
    {
        var sut = CreateSut(new Dictionary<string, string>());

        Assert.Contains("Não há contactos configurados", sut.Description);
    }
}
