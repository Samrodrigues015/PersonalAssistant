namespace PersonalAssistant.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// "Criatividade" do modelo, de 0 a 1. Valores baixos (ex.: 0.2) dão respostas mais
    /// previsíveis e fiéis às instruções — o que queremos num assistente que executa ações.
    /// </summary>
    public double Temperature { get; set; } = 0.2;
}
