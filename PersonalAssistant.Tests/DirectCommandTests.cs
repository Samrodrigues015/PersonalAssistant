using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalAssistant.Actions;
using PersonalAssistant.Configuration;
using PersonalAssistant.Services;
using Xunit;

namespace PersonalAssistant.Tests;

/// <summary>
/// Testes dos comandos diretos: frases reconhecidas SEM o modelo de IA.
/// Usam frases como as que chegam do Whisper (com pontuação, nomes mal escritos, etc.).
/// </summary>
public class DirectCommandTests
{
    private static OpenWhatsAppAction CreateWhatsApp() =>
        new(Options.Create(new WhatsAppSettings
        {
            Contacts = new Dictionary<string, string>
            {
                ["Ester"] = "351910000000",
                ["Ana"] = "351920000000",
                ["João Pedro"] = "351930000000"
            }
        }));

    // ------------------------------------------------------------------ WhatsApp

    [Theory]
    [InlineData("Manda à Ester que chego às 8.", "Ester: chego às 8.")]
    [InlineData("manda a Esther que chego às 8", "Ester: chego às 8")]                   // nome mal transcrito
    [InlineData("Envia uma mensagem à Ester a dizer olá", "Ester: olá")]
    [InlineData("Diz à Ana no WhatsApp que já saí de casa", "Ana: já saí de casa")]
    [InlineData("Manda mensagem ao João Pedro: o jantar é às 20:30", "João Pedro: o jantar é às 20:30")]
    [InlineData("Escreve à Ester, estou a caminho, no WhatsApp", "Ester: estou a caminho")]
    public void WhatsApp_SendMessagePhrases_AreRecognized(string phrase, string expectedArgument)
    {
        var sut = CreateWhatsApp();

        Assert.True(sut.TryMatch(phrase, out var argument));
        Assert.Equal(expectedArgument, argument);
    }

    [Theory]
    [InlineData("Abre o WhatsApp.")]
    [InlineData("abre o whatsapp por favor")]
    [InlineData("WhatsApp")]
    public void WhatsApp_OpenApp_IsRecognizedWithoutArgument(string phrase)
    {
        var sut = CreateWhatsApp();

        Assert.True(sut.TryMatch(phrase, out var argument));
        Assert.Null(argument);
    }

    [Theory]
    [InlineData("Abre a conversa com a Ester")]
    [InlineData("abre o WhatsApp da Esther")]
    [InlineData("Manda à Ester.")] // sem mensagem → só abre a conversa
    public void WhatsApp_OpenChat_IsRecognized(string phrase)
    {
        var sut = CreateWhatsApp();

        Assert.True(sut.TryMatch(phrase, out var argument));
        Assert.Equal("Ester", argument);
    }

    [Theory]
    [InlineData("O WhatsApp é seguro?")]                // pergunta, não é pedido
    [InlineData("Qual é a capital de França?")]
    [InlineData("O que é que a Ester te diz?")]         // "diz" mas nada a seguir
    public void WhatsApp_NormalConversation_IsNotRecognized(string phrase)
    {
        Assert.False(CreateWhatsApp().TryMatch(phrase, out _));
    }

    [Fact]
    public void WhatsApp_UnknownContact_IsRecognizedSoTheUserGetsAClearError()
    {
        var sut = CreateWhatsApp();

        Assert.True(sut.TryMatch("Manda uma mensagem no WhatsApp para o Joaquim", out var argument));
        Assert.Equal("Joaquim", argument);
        Assert.Throws<InvalidOperationException>(() => sut.ResolveTarget(argument));
    }

    [Fact]
    public void WhatsApp_ShortNamesMustMatchExactly()
    {
        // "Ana" tem 3 letras: "Ema" é outra pessoa, não um erro de transcrição.
        Assert.Throws<InvalidOperationException>(() => CreateWhatsApp().ResolveTarget("Ema: olá"));
    }

    [Fact]
    public void WhatsApp_ResolveTarget_AcceptsApproximateName()
    {
        var target = CreateWhatsApp().ResolveTarget("Esther: olá");

        Assert.StartsWith("whatsapp://send?phone=351910000000", target.DesktopUri);
    }

    [Fact]
    public void WhatsApp_DescribeSuccess_UsesTheConfiguredName()
    {
        var text = CreateWhatsApp().DescribeSuccess("Esther: olá");

        Assert.Contains("Ester", text);
        Assert.Contains("Enviar", text);
    }

    // ------------------------------------------------------------------ Calculadora / Bloco de notas

    [Theory]
    [InlineData("Abre a calculadora.", true)]
    [InlineData("A calculadora do Windows é boa?", false)] // sem verbo "abrir" → é conversa
    public void Calculator_DirectCommand(string phrase, bool expected)
    {
        Assert.Equal(expected, new OpenCalculatorAction().TryMatch(phrase, out _));
    }

    [Theory]
    [InlineData("Abre o bloco de notas", true)]
    [InlineData("abre o notepad", true)]
    [InlineData("Gosto do bloco de notas", false)]
    public void Notepad_DirectCommand(string phrase, bool expected)
    {
        Assert.Equal(expected, new OpenNotepadAction().TryMatch(phrase, out _));
    }

    // ------------------------------------------------------------------ NameMatcher

    [Theory]
    [InlineData("ester", "esther", 1)]
    [InlineData("ana", "ana", 0)]
    [InlineData("ana", "ema", 2)]
    [InlineData("", "abc", 3)]
    public void NameMatcher_Distance(string a, string b, int expected)
    {
        Assert.Equal(expected, NameMatcher.Distance(a, b));
    }

    // ------------------------------------------------------------------ AssistantService

    [Fact]
    public async Task AssistantService_DirectCommand_RunsWithoutCallingTheModel()
    {
        var ollama = new Moq.Mock<IOllamaService>(Moq.MockBehavior.Strict); // qualquer chamada ao modelo faz o teste falhar
        var command = new FakeDirectCommandAction();
        var actionService = new ActionService(new IAction[] { command }, NullLogger<ActionService>.Instance);

        var sut = new AssistantService(
            ollama.Object,
            new ConversationService(),
            actionService,
            Options.Create(new AssistantSettings()),
            NullLogger<AssistantService>.Instance);

        var result = await sut.ProcessAsync("faz o comando direto");

        Assert.True(command.WasExecuted);
        Assert.Equal("Feito!", result);
    }

    /// <summary>Ação de teste que se reconhece a si própria pela frase "comando direto".</summary>
    private sealed class FakeDirectCommandAction : IAction, IDirectCommand
    {
        public string Name => "fake_direct";
        public string Description => "Teste.";
        public bool RequiresConfirmation => false;
        public bool WasExecuted { get; private set; }

        public Task<string?> ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult<string?>(null);
        }

        public bool TryMatch(string userInput, out string? argument)
        {
            argument = null;
            return userInput.Contains("comando direto");
        }

        public string DescribeSuccess(string? argument) => "Feito!";
    }
}
