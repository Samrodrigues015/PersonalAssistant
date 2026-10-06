<#
.SYNOPSIS
    Torna as vozes de "Definições → Hora e idioma → Voz" (OneCore) visíveis para o
    System.Speech.Synthesis (SAPI5 clássico), usado pelo PersonalAssistant.

.DESCRIPTION
    Em Windows 10/11, as vozes de texto-para-fala instaladas pelas Definições ficam registadas em
    HKLM\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens, mas o System.Speech.Synthesis (a API
    clássica usada por aplicações .NET como esta) só vê vozes em
    HKLM\SOFTWARE\Microsoft\Speech\Voices\Tokens. Este script copia os registos de um sítio
    para o outro, para as vozes ficarem visíveis também ao System.Speech.

    Não desinstala nem apaga nada — só COPIA entradas do registo. É seguro de reverter: basta
    apagar as chaves copiadas em HKLM\SOFTWARE\Microsoft\Speech\Voices\Tokens.

.NOTES
    Tens de correr este script como Administrador (botão direito no PowerShell → "Executar como
    Administrador"), porque escreve em HKEY_LOCAL_MACHINE.
#>

#Requires -RunAsAdministrator

$oneCorePath = 'HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens'
$sapi5Path   = 'HKLM:\SOFTWARE\Microsoft\Speech\Voices\Tokens'

if (-not (Test-Path $oneCorePath)) {
    Write-Error "Não foram encontradas vozes OneCore em '$oneCorePath'. Instala pelo menos uma voz em Definições -> Hora e idioma -> Voz antes de correres este script."
    exit 1
}

if (-not (Test-Path $sapi5Path)) {
    New-Item -Path $sapi5Path -Force | Out-Null
}

$voices = Get-ChildItem $oneCorePath

if ($voices.Count -eq 0) {
    Write-Error "A pasta '$oneCorePath' existe mas está vazia. Instala pelo menos uma voz nas Definições do Windows."
    exit 1
}

foreach ($voice in $voices) {
    $tokenName = $voice.PSChildName
    Write-Host "A copiar voz: $tokenName"
    Copy-Item -Path $voice.PSPath -Destination $sapi5Path -Recurse -Force
}

Write-Host ""
Write-Host "Concluído. $($voices.Count) voz(es) copiada(s) para o registo do SAPI5."
Write-Host "Fecha e volta a abrir o Visual Studio (ou reinicia o computador, se ainda não funcionar) antes de testares o PersonalAssistant outra vez."
