using Microsoft.Extensions.Logging;

namespace PersonalAssistant.Services;

/// <summary>
/// Padrão "Decorator": embrulha DOIS motores de voz e apresenta-se ao resto do programa
/// como um só ITextToSpeechService.
///
/// Tenta sempre o principal (Piper). Se falhar (sem internet na 1.ª utilização, antivírus a
/// bloquear o piper.exe, etc.), fala com o de reserva (voz do Windows) — e a partir daí usa
/// diretamente o de reserva, para não repetir uma tentativa lenta que vai falhar outra vez.
///
/// O Program.cs e o resto do código não sabem que isto existe: só veem ITextToSpeechService.
/// </summary>
public class FallbackTextToSpeechService : ITextToSpeechService
{
    private readonly ITextToSpeechService _primary;
    private readonly ITextToSpeechService _fallback;
    private readonly ILogger<FallbackTextToSpeechService> _logger;
    private bool _primaryFailed;

    public FallbackTextToSpeechService(
        ITextToSpeechService primary,
        ITextToSpeechService fallback,
        ILogger<FallbackTextToSpeechService> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_primaryFailed)
        {
            try
            {
                await _primary.SpeakAsync(text, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                // Cancelar não é uma falha do motor: não muda para o de reserva.
                throw;
            }
            catch (Exception ex)
            {
                _primaryFailed = true;
                _logger.LogWarning(ex, "O motor de voz principal falhou; a partir de agora uso a voz do Windows.");
            }
        }

        await _fallback.SpeakAsync(text, cancellationToken);
    }
}
