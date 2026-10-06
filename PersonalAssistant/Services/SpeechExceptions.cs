namespace PersonalAssistant.Services;

/// <summary>
/// Lançada quando o reconhecimento de voz ou a síntese de fala falham
/// (microfone indisponível, modelo Whisper em falta, erro a descarregar o modelo, etc.).
/// </summary>
public class SpeechServiceException : Exception
{
    public SpeechServiceException(string message) : base(message)
    {
    }

    public SpeechServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
