namespace PersonalAssistant.Services;

/// <summary>
/// Abstrai a síntese de fala (texto → voz), para que o resto da aplicação
/// não dependa da implementação concreta (motor nativo do Windows, cloud, etc.).
/// </summary>
public interface ITextToSpeechService
{
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
