namespace PersonalAssistant.Services;

/// <summary>
/// Abstrai a comunicação com o Ollama, para que o resto da aplicação não dependa
/// diretamente de HttpClient nem do formato exato da API do Ollama.
/// Também torna possível mockar esta dependência nos testes.
/// </summary>
public interface IOllamaService
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
