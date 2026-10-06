namespace PersonalAssistant.Configuration;

public class VoiceSettings
{
    /// <summary>
    /// Caminho para o ficheiro do modelo ggml do Whisper. Se não existir, é descarregado
    /// automaticamente (uma única vez) a partir do Hugging Face, usando GgmlModelType.
    /// </summary>
    public string ModelPath { get; set; } = "Models/ggml-base.bin";

    /// <summary>
    /// Nome do GgmlType (enum do Whisper.net) a descarregar caso ModelPath não exista.
    /// Ex.: "Tiny", "Base", "Small", "Medium", "Large".
    /// </summary>
    public string GgmlModelType { get; set; } = "Base";

    /// <summary>
    /// Código de idioma passado ao Whisper (ex.: "pt", "en", "auto").
    /// </summary>
    public string Language { get; set; } = "pt";

    /// <summary>
    /// Quantos segundos de áudio gravar do microfone de cada vez que o comando /ouvir é usado.
    /// </summary>
    public int RecordingSeconds { get; set; } = 5;
}
