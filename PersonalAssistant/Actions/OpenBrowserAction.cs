using System.Diagnostics;

namespace PersonalAssistant.Actions;

/// <summary>
/// Abre o navegador predefinido. Se receber um argumento que já seja um URL válido,
/// abre esse URL diretamente; caso contrário, faz uma pesquisa no Google com o texto recebido.
/// </summary>
public class OpenBrowserAction : IAction
{
    public string Name => "open_browser";
    public string Description => "Abre o navegador predefinido, opcionalmente numa pesquisa do Google ou num URL indicado no argumento.";
    public bool RequiresConfirmation => false;

    public Task ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(argument);

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    private static string BuildUrl(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return "https://www.google.com";
        }

        if (Uri.TryCreate(argument, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return argument;
        }

        var query = Uri.EscapeDataString(argument);
        return $"https://www.google.com/search?q={query}";
    }
}
