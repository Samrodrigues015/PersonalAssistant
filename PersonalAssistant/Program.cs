using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalAssistant.Actions;
using PersonalAssistant.Configuration;
using PersonalAssistant.Models;
using PersonalAssistant.Services;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Host.CreateApplicationBuilder já carrega automaticamente o appsettings.json
    // (e appsettings.{Environment}.json) da pasta de saída, por isso não é preciso configurá-lo à mão.

    // appsettings.local.json: valores só do TEU computador (ex.: contactos do WhatsApp).
    // Está no .gitignore, por isso nunca vai para o GitHub. É opcional — se não existir, é ignorado.
    // Carregado depois do appsettings.json, por isso o que estiver aqui sobrepõe-se ao de lá.
    builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

    builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
    builder.Services.Configure<AssistantSettings>(builder.Configuration.GetSection("Assistant"));
    builder.Services.Configure<VoiceSettings>(builder.Configuration.GetSection("Voice"));
    builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));

    // HttpClient "tipado": sempre que pedirmos um IOllamaService ao container,
    // recebemos um OllamaService já com um HttpClient configurado (BaseAddress, timeout).
    builder.Services.AddHttpClient<IOllamaService, OllamaService>((_, client) =>
    {
        var ollamaSettings = builder.Configuration.GetSection("Ollama").Get<OllamaSettings>() ?? new OllamaSettings();

        if (string.IsNullOrWhiteSpace(ollamaSettings.BaseUrl))
        {
            throw new InvalidOperationException("A configuração 'Ollama:BaseUrl' não está definida no appsettings.json.");
        }

        client.BaseAddress = new Uri(ollamaSettings.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(120);
    });

    builder.Services.AddSingleton<IConversationService, ConversationService>();
    builder.Services.AddSingleton<IActionService, ActionService>();
    builder.Services.AddSingleton<IAssistantService, AssistantService>();

    // Cada nova ação só precisa de ser registada aqui para ficar disponível ao assistente.
    builder.Services.AddSingleton<IAction, OpenNotepadAction>();
    builder.Services.AddSingleton<IAction, OpenCalculatorAction>();
    builder.Services.AddSingleton<IAction, OpenBrowserAction>();
    builder.Services.AddSingleton<IAction, OpenWhatsAppAction>();

    // Voz: reconhecimento (fala → texto) e síntese (texto → fala). Nenhuma delas
    // mexe no AssistantService — o texto reconhecido entra pelo mesmo ProcessAsync de sempre.
    builder.Services.AddSingleton<ISpeechRecognitionService, WhisperSpeechRecognitionService>();
    builder.Services.AddSingleton<ITextToSpeechService, SystemTextToSpeechService>();

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    using var host = builder.Build();

    var assistantService = host.Services.GetRequiredService<IAssistantService>();
    var conversationService = host.Services.GetRequiredService<IConversationService>();
    var assistantSettings = host.Services.GetRequiredService<IOptions<AssistantSettings>>().Value;
    var ollamaSettings = host.Services.GetRequiredService<IOptions<OllamaSettings>>().Value;
    var speechRecognitionService = host.Services.GetRequiredService<ISpeechRecognitionService>();
    var textToSpeechService = host.Services.GetRequiredService<ITextToSpeechService>();

    PrintBanner(assistantSettings.Name, ollamaSettings.Model);

    using var cts = new CancellationTokenSource();

    while (true)
    {
        Console.Write("Tu: ");
        var typedInput = Console.ReadLine();

        if (typedInput is null)
        {
            // Ctrl+Z / fim de input (ex.: input redirecionado de um ficheiro)
            break;
        }

        typedInput = typedInput.Trim();

        if (typedInput.Length == 0)
        {
            continue;
        }

        string input;
        var isVoiceInput = false;

        if (string.Equals(typedInput, "/voz", StringComparison.OrdinalIgnoreCase))
        {
            await RunVoiceConversationAsync(assistantService, speechRecognitionService, textToSpeechService, cts.Token);
            continue;
        }

        if (string.Equals(typedInput, "/ouvir", StringComparison.OrdinalIgnoreCase))
        {
            var recognized = await CaptureVoiceInputAsync(speechRecognitionService, cts.Token);

            if (recognized is null)
            {
                continue;
            }

            input = recognized;
            isVoiceInput = true;
        }
        else if (TryHandleCommand(typedInput, conversationService, out var shouldExit))
        {
            if (shouldExit)
            {
                break;
            }

            continue;
        }
        else
        {
            input = typedInput;
        }

        try
        {
            var response = await assistantService.ProcessAsync(input, cts.Token);
            Console.WriteLine();
            Console.WriteLine($"Assistente:\n{response}");
            Console.WriteLine();

            // Só responde por voz quando a pergunta também veio por voz — escrever continua a ser silencioso.
            if (isVoiceInput)
            {
                await SpeakSafelyAsync(textToSpeechService, response, cts.Token);
            }
        }
        catch (Exception ex)
        {
            // Rede de segurança final: nenhum erro inesperado deve fechar o programa.
            Console.WriteLine();
            Console.WriteLine($"Ocorreu um erro inesperado: {ex.Message}");
            Console.WriteLine();
        }
    }

    Console.WriteLine("Até já!");
}
catch (Exception ex)
{
    // Rede de segurança GLOBAL: qualquer erro não previsto durante o arranque (DI, configuração,
    // criação dos serviços de voz, etc.) ou durante a execução aparece aqui, por completo,
    // em vez de só uma mensagem genérica na janela de exceção do Visual Studio.
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("ERRO FATAL AO INICIAR OU CORRER O PROGRAMA");
    Console.WriteLine("========================================");
    Console.WriteLine(ex.ToString());
    Console.WriteLine();
    Console.WriteLine("Copia toda a mensagem acima (a partir de 'ERRO FATAL') para diagnosticarmos.");
    Console.WriteLine();
    Console.WriteLine("Prime Enter para sair...");
    Console.ReadLine();
}

static void PrintBanner(string name, string model)
{
    Console.WriteLine("========================================");
    Console.WriteLine($"        {name.ToUpperInvariant()}");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("Assistente iniciado.");
    Console.WriteLine($"Modelo: {model}");
    Console.WriteLine();
    Console.WriteLine("Escreve /help para ver os comandos.");
    Console.WriteLine();
}

static async Task RunVoiceConversationAsync(
    IAssistantService assistantService,
    ISpeechRecognitionService speechRecognitionService,
    ITextToSpeechService textToSpeechService,
    CancellationToken cancellationToken)
{
    Console.WriteLine();
    Console.WriteLine("Modo de conversa por voz. Fala à vontade — diz \"sair\" ou \"parar\" para voltar a escrever.");

    while (!cancellationToken.IsCancellationRequested)
    {
        var recognized = await CaptureVoiceInputAsync(speechRecognitionService, cancellationToken);

        if (recognized is null)
        {
            // Não percebeu nada desta vez — mantém-se em modo de voz e volta a ouvir.
            continue;
        }

        if (IsExitPhrase(recognized))
        {
            Console.WriteLine("A voltar ao modo de texto.");
            Console.WriteLine();
            return;
        }

        try
        {
            var response = await assistantService.ProcessAsync(recognized, cancellationToken);
            Console.WriteLine();
            Console.WriteLine($"Assistente:\n{response}");
            Console.WriteLine();

            await SpeakSafelyAsync(textToSpeechService, response, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Ocorreu um erro inesperado: {ex.Message}");
            Console.WriteLine();
        }
    }
}

static bool IsExitPhrase(string text)
{
    var normalized = text.Trim().ToLowerInvariant().TrimEnd('.', '!', '?');
    string[] exitPhrases = { "sair", "sai", "parar", "para", "exit", "stop" };

    return exitPhrases.Any(phrase =>
        normalized == phrase ||
        normalized.EndsWith($" {phrase}", StringComparison.Ordinal));
}

static async Task<string?> CaptureVoiceInputAsync(ISpeechRecognitionService speechRecognitionService, CancellationToken cancellationToken)
{
    Console.WriteLine();
    Console.WriteLine("A preparar o reconhecimento de voz (a primeira vez pode demorar, por ter de descarregar o modelo)...");
    Console.WriteLine("Fala agora.");

    try
    {
        var recognized = await speechRecognitionService.ListenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(recognized))
        {
            Console.WriteLine("Não percebi nada. Tenta outra vez.");
            Console.WriteLine();
            return null;
        }

        Console.WriteLine($"(reconhecido) {recognized}");
        return recognized;
    }
    catch (SpeechServiceException ex)
    {
        Console.WriteLine($"Erro ao reconhecer voz: {ex.Message}");
        Console.WriteLine();
        return null;
    }
}

static async Task SpeakSafelyAsync(ITextToSpeechService textToSpeechService, string text, CancellationToken cancellationToken)
{
    try
    {
        await textToSpeechService.SpeakAsync(text, cancellationToken);
    }
    catch (Exception ex)
    {
        // Falar a resposta é um extra — se falhar, a conversa em texto já correu bem e não deve parar por isto.
        Console.WriteLine($"(não foi possível falar a resposta: {ex.Message})");
    }
}

static bool TryHandleCommand(string input, IConversationService conversationService, out bool shouldExit)
{
    shouldExit = false;

    switch (input.ToLowerInvariant())
    {
        case "/help":
            Console.WriteLine();
            Console.WriteLine("Comandos disponíveis:");
            Console.WriteLine("  /help     - mostra esta ajuda");
            Console.WriteLine("  /clear    - limpa o histórico da conversa atual");
            Console.WriteLine("  /history  - mostra o histórico da conversa atual");
            Console.WriteLine("  /ouvir    - grava a tua voz uma vez (microfone) e fala a resposta");
            Console.WriteLine("  /voz      - entra em modo de conversa contínua por voz (\"sair\" para terminar)");
            Console.WriteLine("  /exit     - termina o programa");
            Console.WriteLine();
            return true;

        case "/clear":
            conversationService.Clear();
            Console.WriteLine();
            Console.WriteLine("Histórico limpo.");
            Console.WriteLine();
            return true;

        case "/history":
            Console.WriteLine();
            if (conversationService.History.Count == 0)
            {
                Console.WriteLine("(sem histórico)");
            }
            else
            {
                foreach (var message in conversationService.History)
                {
                    var role = message.Role == ChatRole.User ? "Tu" : "Assistente";
                    Console.WriteLine($"[{message.Timestamp:HH:mm:ss}] {role}: {message.Content}");
                }
            }
            Console.WriteLine();
            return true;

        case "/exit":
            shouldExit = true;
            return true;

        default:
            return false;
    }
}
