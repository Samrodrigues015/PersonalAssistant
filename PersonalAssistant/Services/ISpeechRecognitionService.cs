namespace PersonalAssistant.Services;

/// <summary>
/// Abstrai o reconhecimento de voz (fala → texto), para que o resto da aplicação
/// não dependa da implementação concreta (Whisper, cloud, etc.).
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>
    /// Grava um pouco de áudio do microfone e devolve o texto reconhecido,
    /// ou null se não foi possível perceber nada.
    /// </summary>
    Task<string?> ListenAsync(CancellationToken cancellationToken = default);
}
