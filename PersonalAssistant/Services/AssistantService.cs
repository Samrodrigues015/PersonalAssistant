using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalAssistant.Configuration;
using PersonalAssistant.Models;

namespace PersonalAssistant.Services;

public class AssistantService : IAssistantService
{
    private readonly IOllamaService _ollamaService;
    private readonly IConversationService _conversationService;
    private readonly IActionService _actionService;
    private readonly AssistantSettings _settings;
    private readonly ILogger<AssistantService> _logger;

    private static readonly string[] AffirmativeAnswers = { "sim", "s", "yes", "y", "ok", "okay", "força", "pode ser" };

    // Guarda uma ação que está à espera de confirmação do utilizador (pergunta "sim/não").
    // É estado de conversa, por isso o AssistantService é registado como singleton.
    private PendingAction? _pendingAction;

    public AssistantService(
        IOllamaService ollamaService,
        IConversationService conversationService,
        IActionService actionService,
        IOptions<AssistantSettings> settings,
        ILogger<AssistantService> logger)
    {
        _ollamaService = ollamaService;
        _conversationService = conversationService;
        _actionService = actionService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ProcessAsync(string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return "Não percebi. Podes repetir?";
        }

        _conversationService.AddUserMessage(userInput);

        if (_pendingAction is not null)
        {
            return await HandlePendingConfirmationAsync(userInput, cancellationToken);
        }

        var prompt = BuildPrompt(userInput);
        string rawResponse;

        try
        {
            rawResponse = await _ollamaService.GenerateAsync(prompt, cancellationToken);
        }
        catch (OllamaUnavailableException ex)
        {
            _logger.LogError(ex, "O Ollama não está disponível");
            var errorMessage = $"Não consegui contactar o modelo de IA. {ex.Message}";
            _conversationService.AddAssistantMessage(errorMessage);
            return errorMessage;
        }

        var decision = ParseDecision(rawResponse);
        string finalMessage;

        if (decision.IsAction && !string.IsNullOrWhiteSpace(decision.Action))
        {
            finalMessage = await HandleActionDecisionAsync(decision, cancellationToken);
        }
        else
        {
            finalMessage = string.IsNullOrWhiteSpace(decision.Message) ? rawResponse.Trim() : decision.Message;
        }

        _conversationService.AddAssistantMessage(finalMessage);
        return finalMessage;
    }

    private async Task<string> HandleActionDecisionAsync(AssistantDecision decision, CancellationToken cancellationToken)
    {
        var actionName = decision.Action!;

        if (!_actionService.TryGetAction(actionName, out var action) || action is null)
        {
            _logger.LogWarning("O modelo pediu a ação desconhecida '{Action}'", actionName);
            return $"O modelo tentou pedir a ação '{actionName}', que não está registada. Nada foi executado.";
        }

        if (action.RequiresConfirmation)
        {
            _pendingAction = new PendingAction(action.Name, decision.Argument);
            return string.IsNullOrWhiteSpace(decision.Message)
                ? $"Queres que execute a ação '{action.Name}'? (sim/não)"
                : $"{decision.Message} (sim/não)";
        }

        try
        {
            var result = await _actionService.ExecuteAsync(action.Name, decision.Argument, cancellationToken);
            var prefix = string.IsNullOrWhiteSpace(decision.Message) ? string.Empty : $"{decision.Message} ";
            return $"{prefix}{result}".Trim();
        }
        catch (ActionExecutionException ex)
        {
            _logger.LogError(ex, "Erro ao executar a ação '{Action}'", action.Name);
            return $"Tentei executar '{action.Name}', mas ocorreu um erro: {ex.Message}";
        }
    }

    private async Task<string> HandlePendingConfirmationAsync(string userInput, CancellationToken cancellationToken)
    {
        var pending = _pendingAction!;
        _pendingAction = null;

        var normalized = userInput.Trim().ToLowerInvariant();

        if (!AffirmativeAnswers.Contains(normalized))
        {
            const string cancelMessage = "Ok, não vou executar essa ação.";
            _conversationService.AddAssistantMessage(cancelMessage);
            return cancelMessage;
        }

        try
        {
            var result = await _actionService.ExecuteAsync(pending.ActionName, pending.Argument, cancellationToken);
            _conversationService.AddAssistantMessage(result);
            return result;
        }
        catch (ActionExecutionException ex)
        {
            _logger.LogError(ex, "Erro ao executar a ação confirmada '{Action}'", pending.ActionName);
            var errorMessage = $"Tentei executar '{pending.ActionName}', mas ocorreu um erro: {ex.Message}";
            _conversationService.AddAssistantMessage(errorMessage);
            return errorMessage;
        }
    }

    private string BuildPrompt(string userInput)
    {
        var actionsDescription = new StringBuilder();

        foreach (var action in _actionService.GetAvailableActions())
        {
            actionsDescription.AppendLine($"- \"{action.Name}\": {action.Description}");
        }

        var builder = new StringBuilder();

        builder.AppendLine($"Tu és o {_settings.Name}, um assistente pessoal para Windows. Respondes sempre em {_settings.Language}.");
        builder.AppendLine();
        builder.AppendLine("Tens de decidir se o pedido do utilizador é apenas conversa ou um pedido de ação.");
        builder.AppendLine("Responde SEMPRE apenas com um único objeto JSON válido, sem texto antes ou depois, num destes dois formatos:");
        builder.AppendLine();
        builder.AppendLine("Para conversa normal:");
        builder.AppendLine("{\"type\": \"chat\", \"message\": \"<a tua resposta>\"}");
        builder.AppendLine();
        builder.AppendLine("Para pedir a execução de uma ação:");
        builder.AppendLine("{\"type\": \"action\", \"action\": \"<nome_da_ação>\", \"argument\": \"<argumento ou null>\", \"message\": \"<mensagem para o utilizador>\"}");
        builder.AppendLine();
        builder.AppendLine("As únicas ações que podes pedir são estas (usa exatamente estes nomes):");
        builder.Append(actionsDescription);
        builder.AppendLine("Se o pedido não corresponder a nenhuma destas ações, responde com \"type\": \"chat\".");
        builder.AppendLine("Nunca inventes nomes de ações que não estejam na lista acima.");
        builder.AppendLine();
        builder.AppendLine("Histórico recente da conversa:");
        builder.AppendLine(_conversationService.BuildHistoryText());
        builder.AppendLine();
        builder.AppendLine($"Utilizador: {userInput}");

        return builder.ToString();
    }

    private AssistantDecision ParseDecision(string rawResponse)
    {
        var json = ExtractJson(rawResponse);

        if (json is null)
        {
            _logger.LogWarning("A resposta do modelo não continha JSON válido, a tratar como conversa: {Response}", rawResponse);
            return new AssistantDecision { Type = "chat", Message = rawResponse.Trim() };
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var decision = JsonSerializer.Deserialize<AssistantDecision>(json, options);

            if (decision is null || string.IsNullOrWhiteSpace(decision.Type))
            {
                return new AssistantDecision { Type = "chat", Message = rawResponse.Trim() };
            }

            return decision;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar o JSON devolvido pelo modelo, a tratar como conversa");
            return new AssistantDecision { Type = "chat", Message = rawResponse.Trim() };
        }
    }

    /// <summary>
    /// Extrai o primeiro objeto JSON encontrado no texto devolvido pelo modelo,
    /// caso este tenha acrescentado texto extra à volta do JSON pedido.
    /// </summary>
    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end < 0 || end < start)
        {
            return null;
        }

        return text.Substring(start, end - start + 1);
    }

    private sealed record PendingAction(string ActionName, string? Argument);
}
