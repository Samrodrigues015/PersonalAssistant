using System.Diagnostics;

namespace PersonalAssistant.Actions;

public class OpenCalculatorAction : IAction, IDirectCommand
{
    public string Name => "open_calculator";
    public string Description => "Abre a Calculadora (calc.exe) do Windows.";
    public bool RequiresConfirmation => false;

    public Task<string?> ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "calc.exe",
            UseShellExecute = true
        });

        return Task.FromResult<string?>(null);
    }

    /// <summary>Comando direto: "abre a calculadora" (precisa de um verbo de abrir, para não reagir a uma simples conversa).</summary>
    public bool TryMatch(string userInput, out string? argument)
    {
        argument = null;
        var normalized = TextNormalizer.Normalize(userInput);
        var words = System.Text.RegularExpressions.Regex.Split(normalized, @"[^\p{L}\p{N}]+");
        var hasOpenVerb = words.Any(w => w is "abre" or "abra" or "abrir" or "abres" or "lanca" or "inicia");

        return hasOpenVerb && (words.Contains("calculadora"));
    }

    public string DescribeSuccess(string? argument) => "Abri a calculadora.";
}
