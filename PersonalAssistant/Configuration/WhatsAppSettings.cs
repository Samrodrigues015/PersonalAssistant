namespace PersonalAssistant.Configuration;

/// <summary>
/// Configuração da ação do WhatsApp (secção "WhatsApp" do appsettings).
///
/// IMPORTANTE: os contactos (nomes + números) são dados pessoais. Não os ponhas no
/// appsettings.json (que vai para o GitHub) — põe-nos no appsettings.local.json, que está
/// no .gitignore. Ver README, secção "WhatsApp".
/// </summary>
public class WhatsAppSettings
{
    /// <summary>
    /// Se true, tenta abrir a app de desktop do WhatsApp (protocolo "whatsapp://").
    /// Se a app não estiver instalada (ou se puseres false), usa o WhatsApp Web no navegador.
    /// </summary>
    public bool UseDesktopApp { get; set; } = true;

    /// <summary>
    /// Contactos conhecidos: nome → número com indicativo do país, só dígitos (ex.: "351912345678").
    /// A IA só consegue preparar mensagens para contactos desta lista — nunca para números inventados.
    /// </summary>
    public Dictionary<string, string> Contacts { get; set; } = new();
}
