using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PersonalAssistant.Configuration;

namespace PersonalAssistant.Actions;

/// <summary>
/// Abre o WhatsApp. Sem argumento, abre só a app. Com um argumento no formato
/// "Contacto: mensagem", abre a conversa com esse contacto e deixa a mensagem já escrita.
///
/// Também é um IDirectCommand: reconhece sozinha frases como "abre o WhatsApp",
/// "abre a conversa com a Ester" ou "manda à Ester que chego às 8", sem depender do modelo.
///
/// Segurança:
///  - a mensagem NUNCA é enviada automaticamente — és sempre tu que carregas em Enviar;
///  - só funciona com contactos configurados no appsettings (a IA não pode inventar números).
/// </summary>
public class OpenWhatsAppAction : IAction, IDirectCommand
{
    private const string DesktopAppUri = "whatsapp://";
    private const string WebAppUri = "https://web.whatsapp.com/";

    private static readonly HashSet<string> WhatsAppWords = new() { "whatsapp", "whatsap", "watsapp", "whats", "wpp", "zap" };
    private static readonly HashSet<string> SendVerbs = new() { "manda", "mande", "mandar", "envia", "envie", "enviar", "escreve", "escreva", "escrever", "diz", "diga", "dizer" };
    private static readonly HashSet<string> OpenVerbs = new() { "abre", "abra", "abrir", "abres", "mostra", "mostrar", "lanca", "inicia", "vai" };
    private static readonly HashSet<string> Prepositions = new() { "a", "ao", "para", "com", "da", "do", "de" };
    private static readonly HashSet<string> Articles = new() { "a", "o" };

    // O que pode vir entre o nome e a mensagem: "manda à Ester [no WhatsApp] [a dizer] que chego às 8".
    private static readonly Regex MessageConnectors = new(
        @"^[\s,:;-]*(?:(?:no|pelo)\s+(?:whats\s?app|wpp|zap)\s*)?(?:(?:uma\s+)?mensagem\s+)?(?:a\s+dizer\s+que|a\s+dizer|que)?[\s,:;-]*",
        RegexOptions.IgnoreCase);

    private readonly WhatsAppSettings _settings;
    private readonly List<Contact> _contacts = new();

    public OpenWhatsAppAction(IOptions<WhatsAppSettings> settings)
    {
        _settings = settings.Value;

        foreach (var (name, phone) in _settings.Contacts)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

            if (!string.IsNullOrWhiteSpace(name) && digits.Length > 0)
            {
                _contacts.Add(new Contact(name.Trim(), TextNormalizer.Normalize(name), digits));
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
                : description + " Contactos conhecidos: " + string.Join(", ", _contacts.Select(c => c.DisplayName)) + ".";
        }
    }

    // Abrir o WhatsApp (mesmo com uma mensagem já escrita) não faz nada de irreversível,
    // por isso não pede confirmação — o "Enviar" continua a ser sempre teu.
    public bool RequiresConfirmation => false;

    public Task<string?> ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        var target = ResolveTarget(argument);

        if (_settings.UseDesktopApp)
        {
            try
            {
                OpenUri(target.DesktopUri);
                return Task.FromResult<string?>(null);
            }
            catch (Win32Exception)
            {
                // A app de desktop não está instalada (o Windows não conhece o protocolo "whatsapp://"):
                // continua para o WhatsApp Web.
            }
        }

        OpenUri(target.WebUri);
        return Task.FromResult<string?>(null);
    }

    // -----------------------------------------------------------------------
    // IDirectCommand: reconhecer pedidos sem o modelo de IA
    // -----------------------------------------------------------------------

    public bool TryMatch(string userInput, out string? argument)
    {
        argument = null;

        // Palavras com a posição no texto original (para recortar a mensagem sem estragar acentos/maiúsculas).
        var words = Regex.Matches(userInput, @"[\p{L}\p{N}]+(?:['-][\p{L}\p{N}]+)*").ToList();
        var normalized = words.Select(w => TextNormalizer.Normalize(w.Value)).ToList();

        var mentionsWhatsApp = normalized.Any(WhatsAppWords.Contains);
        var sendVerbIndex = normalized.FindIndex(SendVerbs.Contains);
        var hasOpenVerb = normalized.Any(OpenVerbs.Contains);
        var mentionsChat = normalized.Contains("conversa");

        // 1) "manda à Ester que chego às 8"
        if (sendVerbIndex >= 0)
        {
            var found = FindContactInWords(normalized, startIndex: sendVerbIndex + 1);

            if (found is not null)
            {
                var (contact, lastWordIndex) = found.Value;
                var afterName = words[lastWordIndex].Index + words[lastWordIndex].Length;
                var message = MessageConnectors.Replace(userInput[afterName..], string.Empty).Trim();
                message = Regex.Replace(message, @"[\s,]+(?:no|pelo)\s+(?:whats\s?app|wpp|zap)[\s.!?]*$", string.Empty, RegexOptions.IgnoreCase).Trim();

                // Só pontuação (ex.: o "." que o Whisper põe no fim de "manda à Ester.") não é mensagem.
                if (!message.Any(char.IsLetterOrDigit))
                {
                    message = string.Empty;
                }

                argument = message.Length > 0 ? $"{contact.DisplayName}: {message}" : contact.DisplayName;
                return true;
            }

            if (mentionsWhatsApp)
            {
                // Pediu para mandar mensagem no WhatsApp, mas a pessoa não está nos contactos:
                // passamos o nome na mesma, para o utilizador receber o aviso "Não conheço o contacto...".
                var unknownName = FindNameAfterPreposition(words, normalized, sendVerbIndex + 1);

                if (unknownName is not null)
                {
                    argument = unknownName;
                    return true;
                }
            }
        }

        // 2) "abre a conversa com a Ester" / "abre o WhatsApp da Ester"
        if ((hasOpenVerb || mentionsChat) && (mentionsWhatsApp || mentionsChat))
        {
            var found = FindContactInWords(normalized, startIndex: 0);

            if (found is not null)
            {
                argument = found.Value.Contact.DisplayName;
                return true;
            }
        }

        // 3) "abre o WhatsApp"
        if (mentionsWhatsApp && (hasOpenVerb || normalized.Count <= 2))
        {
            argument = null;
            return true;
        }

        return false;
    }

    public string DescribeSuccess(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return "Abri o WhatsApp.";
        }

        var (name, message) = SplitArgument(argument);
        var contactName = FindContact(name)?.DisplayName ?? name;

        return message.Length > 0
            ? $"Abri a conversa com {contactName}, com a mensagem já escrita. É só carregares em Enviar."
            : $"Abri a conversa com {contactName}.";
    }

    // -----------------------------------------------------------------------
    // Contactos e endereços
    // -----------------------------------------------------------------------

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

        var (contactName, message) = SplitArgument(argument);
        var contact = FindContact(contactName);

        if (contact is null)
        {
            var known = _contacts.Count == 0
                ? "Não há contactos configurados no appsettings.local.json."
                : "Contactos conhecidos: " + string.Join(", ", _contacts.Select(c => c.DisplayName)) + ".";

            throw new InvalidOperationException($"Não conheço o contacto '{contactName}'. {known}");
        }

        var textQuery = message.Length > 0 ? $"&text={Uri.EscapeDataString(message)}" : string.Empty;

        return new WhatsAppTarget(
            DesktopUri: $"whatsapp://send?phone={contact.Phone}{textQuery}",
            WebUri: $"https://wa.me/{contact.Phone}" + (message.Length > 0 ? $"?text={Uri.EscapeDataString(message)}" : string.Empty));
    }

    /// <summary>
    /// "Ana: chego às 8" → ("Ana", "chego às 8"). Só divide no PRIMEIRO ":" — a mensagem
    /// pode ter ":" lá dentro (ex.: horas "20:30").
    /// </summary>
    private static (string Name, string Message) SplitArgument(string argument)
    {
        var separatorIndex = argument.IndexOf(':');
        var name = (separatorIndex >= 0 ? argument[..separatorIndex] : argument).Trim();
        var message = separatorIndex >= 0 ? argument[(separatorIndex + 1)..].Trim() : string.Empty;
        return (name, message);
    }

    /// <summary>
    /// Procura um contacto pelo nome: primeiro igual (sem maiúsculas/acentos), depois aproximado
    /// ("Esther" → "Ester"). Se dois contactos estiverem igualmente perto, não escolhe nenhum —
    /// mais vale dizer "não conheço" do que abrir a conversa da pessoa errada.
    /// </summary>
    private Contact? FindContact(string name)
    {
        var normalized = TextNormalizer.Normalize(name);
        var exact = _contacts.FirstOrDefault(c => c.NormalizedName == normalized);

        if (exact is not null)
        {
            return exact;
        }

        var candidates = _contacts
            .Select(c => (Contact: c, Distance: NameMatcher.Distance(normalized, c.NormalizedName)))
            .Where(x => x.Distance <= NameMatcher.MaxDistanceFor(x.Contact.NormalizedName))
            .OrderBy(x => x.Distance)
            .ToList();

        if (candidates.Count == 0 || (candidates.Count > 1 && candidates[0].Distance == candidates[1].Distance))
        {
            return null;
        }

        return candidates[0].Contact;
    }

    /// <summary>
    /// Procura um contacto conhecido nas palavras da frase (a partir de <paramref name="startIndex"/>).
    /// Nome igual conta em qualquer sítio; nome só "parecido" só conta logo a seguir a uma
    /// preposição ("à Esther", "com a Esther") — senão "manda ESTE recado" parecia "Ester".
    /// </summary>
    private (Contact Contact, int LastWordIndex)? FindContactInWords(List<string> normalizedWords, int startIndex)
    {
        for (var i = startIndex; i < normalizedWords.Count; i++)
        {
            foreach (var contact in _contacts)
            {
                var nameWordCount = contact.NormalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (i + nameWordCount > normalizedWords.Count)
                {
                    continue;
                }

                var candidate = string.Join(' ', normalizedWords.Skip(i).Take(nameWordCount));
                var afterPreposition = i > 0 && Prepositions.Contains(normalizedWords[i - 1]);

                var isMatch = candidate == contact.NormalizedName ||
                              (afterPreposition && NameMatcher.IsMatch(candidate, contact.NormalizedName));

                if (isMatch)
                {
                    return (contact, i + nameWordCount - 1);
                }
            }
        }

        return null;
    }

    private static string? FindNameAfterPreposition(IReadOnlyList<Match> words, List<string> normalized, int startIndex)
    {
        for (var i = startIndex; i < normalized.Count - 1; i++)
        {
            if (!Prepositions.Contains(normalized[i]))
            {
                continue;
            }

            // "para o Joaquim" → salta o artigo "o".
            var nameIndex = Articles.Contains(normalized[i + 1]) && i + 2 < normalized.Count ? i + 2 : i + 1;

            if (!WhatsAppWords.Contains(normalized[nameIndex]) && !Articles.Contains(normalized[nameIndex]))
            {
                return words[nameIndex].Value;
            }
        }

        return null;
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

    private sealed record Contact(string DisplayName, string NormalizedName, string Phone);
}

/// <summary>
/// Os dois endereços possíveis para o mesmo pedido: o da app de desktop e o do WhatsApp Web (alternativa).
/// </summary>
public sealed record WhatsAppTarget(string DesktopUri, string WebUri);
