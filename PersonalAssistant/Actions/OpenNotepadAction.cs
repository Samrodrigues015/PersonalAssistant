using System.Diagnostics;

namespace PersonalAssistant.Actions;

public class OpenNotepadAction : IAction
{
    public string Name => "open_notepad";
    public string Description => "Abre o Bloco de Notas (notepad.exe) do Windows.";
    public bool RequiresConfirmation => false;

    public Task ExecuteAsync(string? argument, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
