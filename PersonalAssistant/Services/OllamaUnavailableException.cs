namespace PersonalAssistant.Services;

/// <summary>
/// Lançada quando não é possível comunicar com o Ollama ou quando este devolve
/// uma resposta inválida/inesperada (serviço desligado, modelo inexistente, JSON inválido, etc.).
/// </summary>
public class OllamaUnavailableException : Exception
{
    public OllamaUnavailableException(string message) : base(message)
    {
    }

    public OllamaUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
