using System.Diagnostics;

namespace PersonalAssistant.Actions;

public class OpenCalculatorAction : IAction
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
}
