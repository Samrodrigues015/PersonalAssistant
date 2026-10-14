namespace PersonalAssistant.Configuration;

public class VoiceSettings
{
    // ---------------------------------------------------------------
    // Reconhecimento de voz (Whisper)
    // ---------------------------------------------------------------

    /// <summary>
    /// Caminho para o ficheiro do modelo ggml do Whisper. Se não existir, é descarregado
    /// automaticamente (uma única vez) a partir do Hugging Face, usando GgmlModelType.
    /// </summary>
    public string ModelPath { get; set; } = "Models/ggml-small.bin";

    /// <summary>
    /// Nome do GgmlType (enum do Whisper.net) a descarregar caso ModelPath não exista.
    /// Ex.: "Tiny", "Base", "Small", "Medium". "Small" percebe português muito melhor que "Base",
    /// à custa de ser mais lento e maior (~470 MB).
    /// </summary>
    public string GgmlModelType { get; set; } = "Small";

    /// <summary>
    /// Código de idioma passado ao Whisper (ex.: "pt", "en", "auto").
    /// </summary>
    public string Language { get; set; } = "pt";

    /// <summary>
    /// Texto de "contexto" dado ao Whisper antes de transcrever. Ajuda-o a acertar no idioma,
    /// na pontuação e em palavras que vais dizer muitas vezes (nomes, "WhatsApp"...).
    /// </summary>
    public string InitialPrompt { get; set; } =
        "Pedido em português de Portugal a um assistente pessoal. Abre o WhatsApp, manda uma mensagem, lê o documento.";

    // ---------------------------------------------------------------
    // Gravação (deteção de quando começas e acabas de falar)
    // ---------------------------------------------------------------

    /// <summary>Quanto tempo esperar que comeces a falar antes de desistir (segundos).</summary>
    public double SpeechStartTimeoutSeconds { get; set; } = 6;

    /// <summary>Silêncio seguido, depois de falares, que indica que acabaste (segundos).</summary>
    public double EndSilenceSeconds { get; set; } = 1.2;

    /// <summary>Duração máxima de uma gravação, mesmo que continues a falar (segundos).</summary>
    public double MaxRecordingSeconds { get; set; } = 20;

    /// <summary>
    /// Volume mínimo (0 a 1) para contar como fala. Se ele não te ouvir, baixa (ex.: 0.01);
    /// se parar de gravar por causa de ruído de fundo, sobe (ex.: 0.03).
    /// </summary>
    public float SilenceThreshold { get; set; } = 0.015f;

    // ---------------------------------------------------------------
    // Síntese de fala
    // ---------------------------------------------------------------

    /// <summary>
    /// Motor de voz: "Piper" (voz neural, mais natural, descarregada na 1.ª utilização)
    /// ou "System" (voz nativa do Windows). Se o Piper falhar, usa-se o do Windows.
    /// </summary>
    public string TtsEngine { get; set; } = "Piper";
}
