using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;

namespace PersonalAssistant.Services;

/// <summary>
/// Síntese de fala usando o motor de voz nativo do Windows (System.Speech.Synthesis, SAPI5).
/// Não precisa de internet nem de chaves de API.
///
/// NOTA: em Windows modernos, as vozes instaladas por "Definições → Hora e idioma → Voz" ficam
/// registadas no ramo "OneCore" do registo, que o System.Speech (SAPI5 clássico) não enxerga.
/// Por isso é comum não existir NENHUMA voz visível para esta classe, mesmo havendo vozes
/// instaladas no Windows. Ver README.md ("Se ele ouvir mas não falar") para a correção.
///
/// Este serviço NUNCA deve impedir o programa de arrancar: se não houver nenhuma voz utilizável,
/// regista um aviso e o resto do assistente (texto, reconhecimento de voz, ações) continua a funcionar.
/// </summary>
public class SystemTextToSpeechService : ITextToSpeechService, IDisposable
{
    private readonly SpeechSynthesizer? _synthesizer;
    private readonly ILogger<SystemTextToSpeechService> _logger;
    private readonly bool _hasVoice;

    public SystemTextToSpeechService(ILogger<SystemTextToSpeechService> logger)
    {
        _logger = logger;

        try
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            _hasVoice = TrySelectWorkingVoice(_synthesizer);
        }
        catch (Exception ex)
        {
            // O System.Speech pode lançar exceções internas (ex.: NullReferenceException) quando
            // o registo de vozes do Windows está num estado que ele não consegue ler.
            // Em vez de deitar abaixo o programa inteiro, desligamos só a síntese de fala.
            _logger.LogWarning(ex, "Não foi possível inicializar o motor de voz (TTS). O assistente vai responder só em texto.");
            _hasVoice = false;
        }

        if (!_hasVoice)
        {
            _logger.LogWarning(
                "Nenhuma voz SAPI5 utilizável encontrada para o System.Speech. A síntese de fala está desligada " +
                "até corrigires isto — consulta o README.md, secção 'Se ele ouvir mas não falar'.");
        }
    }

    /// <summary>
    /// Lista as vozes que o System.Speech vê e escolhe a primeira que funcione de facto,
    /// dando preferência a vozes em português. Cada voz é testada isoladamente, para que uma voz
    /// mal registada não impeça as outras de serem usadas.
    /// </summary>
    private bool TrySelectWorkingVoice(SpeechSynthesizer synthesizer)
    {
        var installedVoices = synthesizer.GetInstalledVoices()
            .Where(v => v.Enabled)
            .ToList();

        if (installedVoices.Count == 0)
        {
            _logger.LogWarning("O System.Speech não encontrou nenhuma voz instalada e ativa.");
            return false;
        }

        foreach (var installed in installedVoices)
        {
            _logger.LogInformation(
                "Voz encontrada: '{VoiceName}' ({Culture})",
                installed.VoiceInfo.Name,
                installed.VoiceInfo.Culture?.Name ?? "cultura desconhecida");
        }

        // Português primeiro; dentro do mesmo grupo mantém-se a ordem do Windows.
        var orderedVoices = installedVoices
            .OrderByDescending(v => v.VoiceInfo.Culture?.TwoLetterISOLanguageName == "pt")
            .ToList();

        foreach (var installed in orderedVoices)
        {
            try
            {
                synthesizer.SelectVoice(installed.VoiceInfo.Name);

                _logger.LogInformation(
                    "Motor de voz (TTS) pronto. Voz selecionada: '{VoiceName}' ({Culture}).",
                    installed.VoiceInfo.Name,
                    installed.VoiceInfo.Culture?.Name ?? "cultura desconhecida");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A voz '{VoiceName}' existe mas não pôde ser usada. A tentar a seguinte...", installed.VoiceInfo.Name);
            }
        }

        return false;
    }

    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        if (!_hasVoice || _synthesizer is null)
        {
            throw new SpeechServiceException(
                "Não existe nenhuma voz disponível para o System.Speech (SAPI5) neste computador, " +
                "mesmo que tenhas vozes instaladas nas Definições do Windows. Consulta o README.md, " +
                "secção 'Se ele ouvir mas não falar', para o corrigir.");
        }

        // SpeechSynthesizer.Speak(...) é síncrono: só volta quando acaba de falar.
        // Corre-se numa thread separada (Task.Run) para não bloquear o resto do programa
        // enquanto fala, mas sem a complexidade (e risco de falhas silenciosas) de lidar
        // com o evento SpeakCompleted da versão assíncrona (SpeakAsync).
        return Task.Run(
            () =>
            {
                try
                {
                    _synthesizer.Speak(text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao sintetizar voz");
                    throw new SpeechServiceException(
                        "Não foi possível falar a resposta. Verifica se tens colunas/auscultadores ligados " +
                        "e um dispositivo de áudio predefinido configurado no Windows (Definições → Som).",
                        ex);
                }
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _synthesizer?.Dispose();
    }
}
