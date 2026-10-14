namespace PersonalAssistant.Configuration;

/// <summary>
/// Configuração do Piper (secção "Piper" do appsettings): um motor de voz neural, gratuito e
/// offline, com vozes muito mais naturais do que as do Windows. Tanto o programa como a voz
/// são descarregados automaticamente na primeira utilização.
/// </summary>
public class PiperSettings
{
    /// <summary>Pasta onde ficam o piper.exe e as vozes (relativa à pasta do programa).</summary>
    public string Folder { get; set; } = "Piper";

    /// <summary>
    /// Nome da voz, no formato "idioma_PAÍS-nome-qualidade". Lista de vozes:
    /// https://huggingface.co/rhasspy/piper-voices (pasta pt/pt_PT para português de Portugal).
    /// </summary>
    public string VoiceName { get; set; } = "pt_PT-tugão-medium";

    /// <summary>Velocidade da fala: 1.0 = normal; maior = mais devagar (ex.: 1.2); menor = mais rápido.</summary>
    public double LengthScale { get; set; } = 1.0;

    /// <summary>De onde descarregar o Piper para Windows.</summary>
    public string ExecutableUrl { get; set; } =
        "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";

    /// <summary>Repositório de onde descarregar as vozes.</summary>
    public string VoicesBaseUrl { get; set; } = "https://huggingface.co/rhasspy/piper-voices/resolve/main";
}
