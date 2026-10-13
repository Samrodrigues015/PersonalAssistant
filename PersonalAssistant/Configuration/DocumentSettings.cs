namespace PersonalAssistant.Configuration;

/// <summary>
/// Configuração da leitura de documentos (secção "Documents" do appsettings).
/// </summary>
public class DocumentSettings
{
    /// <summary>
    /// A ÚNICA pasta de onde o assistente pode ler documentos (e as subpastas dela).
    /// Aceita variáveis de ambiente, ex.: "%USERPROFILE%\\Documents\\Assistente".
    /// Pedidos para ler ficheiros fora desta pasta são recusados.
    /// </summary>
    public string Folder { get; set; } = @"%USERPROFILE%\Documents\Assistente";

    /// <summary>
    /// Máximo de caracteres do documento enviados ao modelo. Modelos locais têm um limite
    /// de contexto; um documento enorme seria cortado de qualquer forma (e ficaria muito lento).
    /// </summary>
    public int MaxCharacters { get; set; } = 12000;
}
