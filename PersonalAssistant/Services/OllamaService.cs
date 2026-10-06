using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalAssistant.Configuration;

namespace PersonalAssistant.Services;

/// <summary>
/// Implementação de IOllamaService que fala com a API local do Ollama
/// (POST /api/generate) através de HttpClient.
/// </summary>
public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient httpClient, IOptions<OllamaSettings> settings, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestBody = new OllamaGenerateRequest
        {
            Model = _settings.Model,
            Prompt = prompt,
            Stream = false
        };

        _logger.LogInformation("A enviar pedido para o Ollama (modelo: {Model})", _settings.Model);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Não foi possível contactar o Ollama em {BaseUrl}", _settings.BaseUrl);
            throw new OllamaUnavailableException(
                $"Não foi possível contactar o Ollama em '{_settings.BaseUrl}'. Verifica se o serviço 'ollama serve' está em execução.",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Pedido ao Ollama excedeu o tempo limite");
            throw new OllamaUnavailableException(
                "O pedido ao Ollama demorou demasiado tempo (timeout). O modelo pode estar a carregar ou o pedido é muito grande.",
                ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("O Ollama respondeu com o código {StatusCode}: {Body}", response.StatusCode, body);
            throw new OllamaUnavailableException(
                $"O Ollama respondeu com o código {(int)response.StatusCode}. Verifica se o modelo '{_settings.Model}' está disponível (ollama pull {_settings.Model}).");
        }

        OllamaGenerateResponse? parsed;

        try
        {
            parsed = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Resposta inválida (JSON) recebida do Ollama");
            throw new OllamaUnavailableException("O Ollama devolveu uma resposta em formato inesperado (JSON inválido).", ex);
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Response))
        {
            _logger.LogError("O Ollama devolveu uma resposta vazia");
            throw new OllamaUnavailableException("O Ollama devolveu uma resposta vazia.");
        }

        return parsed.Response;
    }

    private class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
}
