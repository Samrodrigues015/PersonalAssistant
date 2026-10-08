using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using PersonalAssistant.Configuration;

namespace PersonalAssistant.Actions;

/// <summary>
/// Abre o WhatsApp. Sem argumento, abre só a app. Com um argumento no formato
/// "Contacto: mensagem", abre a conversa com esse contacto e deixa a mensagem já escrita.
///
/// Segurança:
///  - a mensagem NUNCA é enviada automaticamente — és sempre tu que carregas em Enviar;
///  - só funciona com contactos configurados no appsettings (a IA não pode inventar números).
/// </summary>
public class OpenWhatsAppAction : IAction
{
    private const string DesktopAppUri = "whatsapp://";
    private const string WebAppUri = "https://web.whatsapp.com/";

    private readonly WhatsAppSettings _settings;

    // Chave = nome do contacto "normalizado" (sem acentos, minúsculas), para que
    // "Mãe", "mae" e "MÃE" (comum quando o texto vem do reconhecimento de voz) deem o mesmo contacto.
    private readonly Dictionary<string, (string DisplayName, string Phone)> _contacts;

    public OpenWhatsAppAction(IOptions<WhatsAppSettings> settings)
    {
        _settings = settings.Value;
        _contacts = new Dictionary<string, (string, string)>();

        foreach (var (name, phone) in _settings.Contacts)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

            if (!string.IsNullOrWhiteSpace(name) && digits.Length > 0)
            {
                _contacts[NormalizeName(name)] = (name.Trim(), digits);
            }
        }
    }

    public string Name => "open_whatsapp";

    public string Description
    {
        get
        {
            var description =
                "Abre o WhatsApp. Sem argumento abre só a aplicação. " +
                "Para preparar uma mensagem a um contacto, usa o argumento no formato \"Contacto: mensagem\" " +
                "(ex.: \"Ana: chego às 8\"). Para abrir só a conversa, o argumento é apenas o nome do contacto. " +
                "A mensagem não é enviada sozinha: o utilizador confirma no WhatsApp.";

            return _contacts.Count == 0
                ? description + " Não há contactos configurados, por isso só é possível abrir a aplicação."
                : description + " Contactos conhecidos: " + string.Join(", ", _contacts.Values.Select(c => c.DisplayName)) + ".";
        }
    }

    // Abrir o WhatsApp (mesmo com uma mensagem já escrita) não faz nada de irreversível,
    // por isso não pede confirmação — o "Enviar" continua a ser sempre teu.
    public bool RequiresConfirmation => false;

    public Task ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        var target = ResolveTarget(argument);

        if (_settings.UseDesktopApp)
        {
            try
            {
                OpenUri(target.DesktopUri);
                return Task.CompletedTask;
            }
            catch (Win32Exception)
            {
                // A app de desktop não está instalada (o Windows não conhece o protocolo "whatsapp://"):
                // continua para o WhatsApp Web.
            }
        }

        OpenUri(target.WebUri);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Decide QUE endereços abrir, sem abrir nada. Está separado do ExecuteAsync de propósito:
    /// assim os testes conseguem verificar a lógica toda sem lançar o WhatsApp de verdade.
    /// </summary>
    /// <exception cref="InvalidOperationException">O contacto pedido não está configurado.</exception>
    public WhatsAppTarget ResolveTarget(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return new WhatsAppTarget(DesktopAppUri, WebAppUri);
        }

        // "Ana: chego às 8" → contacto "Ana", mensagem "chego às 8".
        // Só se divide no PRIMEIRO ":" — a mensagem pode ter ":" lá dentro (ex.: horas "20:30").
        var separatorIndex = argument.IndexOf(':');
        var contactName = separatorIndex >= 0 ? argument[..separatorIndex] : argument;
        var message = separatorIndex >= 0 ? argument[(separatorIndex + 1)..].Trim() : string.Empty;

        if (!_contacts.TryGetValue(NormalizeName(contactName), out var contact))
        {
            var known = _contacts.Count == 0
                ? "Não há contactos configurados no appsettings.local.json."
                : "Contactos conhecidos: " + string.Join(", ", _contacts.Values.Select(c => c.DisplayName)) + ".";

            throw new InvalidOperationException($"Não conheço o contacto '{contactName.Trim()}'. {known}");
        }

        var textQuery = message.Length > 0 ? $"&text={Uri.EscapeDataString(message)}" : string.Empty;

        return new WhatsAppTarget(
            DesktopUri: $"whatsapp://send?phone={contact.Phone}{textQuery}",
            WebUri: $"https://wa.me/{contact.Phone}" + (message.Length > 0 ? $"?text={Uri.EscapeDataString(message)}" : string.Empty));
    }

    private static void OpenUri(string uri)
    {
        // UseShellExecute = true: o Windows decide que programa abre o endereço
        // (a app do WhatsApp para "whatsapp://", o navegador predefinido para "https://").
        Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// "  Mãe " → "mae". Remove acentos (decompõe "ã" em "a" + til e descarta o til) e espaços.
    /// </summary>
    private static string NormalizeName(string name)
    {
        var decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>
/// Os dois endereços possíveis para o mesmo pedido: o da app de desktop e o do WhatsApp Web (alternativa).
/// </summary>
public sealed record WhatsAppTarget(string DesktopUri, string WebUri);
