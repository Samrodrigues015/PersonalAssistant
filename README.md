# PersonalAssistant — V1

Assistente pessoal para Windows, em C#/.NET, que conversa através do Ollama
(modelo local `qwen2.5-coder:7b`) e pode executar um conjunto controlado de
ações no computador (abrir o Bloco de Notas, a Calculadora, o navegador, etc.).

Esta é a **V1**: funcional, com arquitetura preparada para crescer, mas
propositadamente sem memória persistente, speech-to-text, interface gráfica,
Docker ou CI/CD — isso fica para versões seguintes.

## Estrutura da solução

```
PersonalAssistant.sln
│
├── PersonalAssistant/                  (projeto principal — consola)
│   ├── Configuration/
│   │   ├── OllamaSettings.cs
│   │   ├── AssistantSettings.cs
│   │   └── VoiceSettings.cs
│   ├── Models/
│   │   ├── ChatRole.cs
│   │   ├── ChatMessage.cs
│   │   └── AssistantDecision.cs
│   ├── Services/
│   │   ├── IOllamaService.cs / OllamaService.cs
│   │   ├── IConversationService.cs / ConversationService.cs
│   │   ├── IActionService.cs / ActionService.cs
│   │   ├── IAssistantService.cs / AssistantService.cs
│   │   ├── ISpeechRecognitionService.cs / WhisperSpeechRecognitionService.cs
│   │   ├── ITextToSpeechService.cs / SystemTextToSpeechService.cs
│   │   ├── OllamaUnavailableException.cs
│   │   ├── ActionExceptions.cs
│   │   └── SpeechExceptions.cs
│   ├── Actions/
│   │   ├── IAction.cs
│   │   ├── OpenNotepadAction.cs
│   │   ├── OpenCalculatorAction.cs
│   │   └── OpenBrowserAction.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── PersonalAssistant.csproj
│
└── PersonalAssistant.Tests/            (testes xUnit)
    ├── FakeAction.cs
    ├── AssistantDecisionTests.cs
    ├── ConversationServiceTests.cs
    ├── ActionServiceTests.cs
    ├── AssistantServiceTests.cs
    └── PersonalAssistant.Tests.csproj
```

## Pré-requisitos

- Visual Studio 2022 (17.12+) ou .NET SDK 10 instalado.
- [Ollama](https://ollama.com) instalado e em execução localmente.
- O modelo `qwen2.5-coder:7b` disponível:

  ```
  ollama pull qwen2.5-coder:7b
  ollama serve
  ```

  (se já correste `ollama run qwen2.5-coder:7b` antes, o serviço já deve estar
  a correr em `http://localhost:11434`).
- Para a parte de voz: **Microsoft Visual C++ Redistributable** (normalmente já vem com o
  Windows 11) e ligação à internet só na primeira vez que usares `/ouvir` (para descarregar o
  modelo Whisper). Ver a secção "Voz" mais abaixo.

## Como abrir e executar

1. Abre `PersonalAssistant.sln` no Visual Studio.
2. Garante que o Ollama está em execução (ver acima).
3. Define `PersonalAssistant` como o projeto de arranque (já deve estar,
   por ser o único executável) e corre com `F5` ou `Ctrl+F5`.

Ou pela linha de comandos, a partir da pasta da solução:

```
cd PersonalAssistant
dotnet restore
dotnet run
```

## Como correr os testes

```
cd PersonalAssistant.Tests
dotnet test
```

Ou no Visual Studio: **Test Explorer → Run All Tests**.

## Experimentar

```
========================================
        PERSONALASSISTANT
========================================

Assistente iniciado.
Modelo: qwen2.5-coder:7b

Escreve /help para ver os comandos.

Tu: Olá

Assistente:
Olá! Como posso ajudar?

Tu: O meu nome é Samara.

Assistente:
Prazer, Samara!

Tu: Qual é o meu nome?

Assistente:
O teu nome é Samara.

Tu: Abre a calculadora.

Assistente:
Vou abrir a calculadora. Ação 'open_calculator' executada com sucesso.

Tu: /history
Tu: /clear
Tu: /exit
```

## Comandos especiais

- `/help` — mostra a lista de comandos.
- `/clear` — limpa o histórico da conversa atual (em memória).
- `/history` — mostra o histórico da conversa atual.
- `/ouvir` — grava a tua voz uma vez, transcreve com Whisper e fala a resposta.
- `/voz` — entra em **modo de conversa contínua por voz**: fala à vontade, o assistente responde
  sempre por voz, e volta a ouvir automaticamente. Diz "sair" ou "parar" para voltar ao modo de
  texto (não precisas de escrever nada entre perguntas).
- `/exit` — termina o programa.

Qualquer outro texto é tratado como linguagem natural e enviado ao assistente.

## Voz (reconhecimento e síntese)

A V1 já inclui voz, como camada adicional por cima do texto — o `AssistantService`,
`OllamaService` e `ActionService` continuam exatamente iguais; só o `Program.cs` ganhou
um comando novo.

- **Reconhecimento de voz (fala → texto)**: local, com [Whisper.net](https://github.com/sandrohanea/whisper.net)
  (bindings .NET para o Whisper da OpenAI/whisper.cpp). Grava o microfone com **NAudio**
  durante `Voice:RecordingSeconds` segundos (5 por omissão) e transcreve.
- **Síntese de fala (texto → voz)**: usa o motor nativo do Windows, `System.Speech.Synthesis`.
- **`/ouvir` vs `/voz`**: `/ouvir` faz uma única troca (falas uma vez, ele responde uma vez, e
  volta ao modo de texto). `/voz` entra num ciclo — como falar com a Alexa — onde ele ouve,
  responde por voz, e volta a ouvir sozinho, até dizeres "sair" ou "parar".

### Primeira utilização

Na primeira vez que usares `/ouvir`, o programa **descarrega automaticamente** o modelo
Whisper configurado (por omissão, `ggml-base.bin`, ~140 MB) para a pasta `Models/`, a partir
do Hugging Face. Isto só acontece uma vez — precisa de internet nesse momento, mas depois o
reconhecimento corre 100% local e offline.

Se preferires descarregar o modelo tu próprio, ou já o tiveres de outro projeto, basta colocá-lo
no caminho definido em `Voice:ModelPath` no `appsettings.json`.

### Configuração (`appsettings.json`)

```json
"Voice": {
  "ModelPath": "Models/ggml-base.bin",
  "GgmlModelType": "Base",
  "Language": "pt",
  "RecordingSeconds": 5
}
```

- `ModelPath` — onde o modelo Whisper está (ou vai ser descarregado).
- `GgmlModelType` — que modelo descarregar se `ModelPath` não existir (`Tiny`, `Base`, `Small`,
  `Medium`, `Large`). Modelos maiores são mais precisos, mas mais lentos e maiores.
- `Language` — código de idioma passado ao Whisper (`pt` funciona bem; `auto` deteta sozinho).
- `RecordingSeconds` — quantos segundos gravar de cada vez que usas `/ouvir`.

### Notas importantes

- O CPU precisa de suportar as instruções **AVX/AVX2/FMA/F16C** (praticamente qualquer CPU
  Intel/AMD dos últimos ~10 anos). Se o `Whisper.net.Runtime` falhar a carregar por causa disso,
  troca o pacote no `.csproj` para `Whisper.net.Runtime.NoAvx` (mesma versão).
- Requer o **Microsoft Visual C++ Redistributable** (normalmente já instalado no Windows 11).
- `/ouvir` só existe como comando adicional — escrever continua a funcionar exatamente como
  antes, e as respostas só são faladas quando a pergunta também veio por voz.
- Os serviços de voz não têm testes automáticos (`WhisperSpeechRecognitionService` e
  `SystemTextToSpeechService`), porque dependem de hardware real (microfone/colunas) — algo a
  melhorar mais tarde, por exemplo isolando a parte de gravação/reprodução atrás de uma interface
  mais pequena e mockável.

### Se ele ouvir mas não falar (ou der `NullReferenceException` no arranque)

Isto é um problema **conhecido** do `System.Speech` em Windows 10/11, não um bug específico
deste projeto: as vozes que instalas em **Definições → Hora e idioma → Voz** ficam registadas
no ramo "OneCore" do registo do Windows, mas o `System.Speech.Synthesis` (a API SAPI5 clássica,
usada por aplicações .NET) só vê vozes registadas noutro sítio. Resultado: em muitos Windows
modernos, não existe **nenhuma** voz visível para o `System.Speech`, mesmo havendo vozes
instaladas.

**Correção (recomendada):**

1. Abre o PowerShell **como Administrador**.
2. Corre o script incluído no projeto:
   ```
   cd PersonalAssistant\tools
   .\Register-SapiVoicesFromOneCore.ps1
   ```
3. Fecha e reabre o Visual Studio (ou reinicia o PC, se ainda não resolver).
4. Corre o `PersonalAssistant` outra vez — no arranque deve aparecer
   `Motor de voz (TTS) pronto. Voz selecionada: '...' (...)`.

O script só **copia** entradas do registo (não apaga nada) — é seguro e reversível: basta
apagar as chaves copiadas em `HKLM\SOFTWARE\Microsoft\Speech\Voices\Tokens` se quiseres desfazer.

**Se preferires não mexer no registo**, também podes instalar um pacote de idioma clássico:
Definições → Hora e idioma → Idioma e região → Adicionar um idioma → escolhe um idioma e garante
que a opção de "conversão de texto em voz" fica marcada durante a instalação. Isto às vezes
regista também uma voz no sítio clássico, sem precisares do script.

**Atenção ao `InvariantGlobalization`:** o `PersonalAssistant.csproj` **não** deve ter
`<InvariantGlobalization>true</InvariantGlobalization>`. Com essa opção ligada, o .NET não
consegue converter o código de idioma de cada voz (ex.: `816` → `pt-PT`), o `System.Speech`
fica com o idioma da voz a `null` e rebenta com `NullReferenceException` dentro de
`SpeechSynthesizer.Voice`, mesmo com as vozes bem registadas.

Se, depois disto, ainda tiveres erro, a mensagem agora é sempre clara e aparece no terminal como
`(não foi possível falar a resposta: ...)` — outras causas possíveis: dispositivo de saída de
som errado ou mudo (Definições → Som), ou o processo `PersonalAssistant.exe` silenciado no
Misturador de Volume do Windows.

## Configuração (`appsettings.json`)

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder:7b"
  },
  "Assistant": {
    "Name": "PersonalAssistant",
    "Language": "pt-PT"
  }
}
```

Podes trocar o modelo (ex.: `qwen2.5-coder:14b`) ou o `BaseUrl` sem tocar em código.

## Como funciona (resumo)

```
Program.cs
    ↓ (Dependency Injection)
AssistantService  ←→  ConversationService (histórico em memória)
    ↓
OllamaService  →  HTTP  →  Ollama (qwen2.5-coder:7b)
    ↓ (JSON: {"type": "chat"/"action", ...})
ActionService  →  Actions/ (OpenNotepadAction, OpenCalculatorAction, OpenBrowserAction, ...)
```

1. O utilizador escreve uma frase.
2. O `AssistantService` monta um *prompt* que inclui: as instruções do
   sistema, a lista de ações permitidas e o histórico recente, e pede ao
   Ollama para responder **apenas em JSON** (`{"type": "chat", ...}` ou
   `{"type": "action", ...}`).
3. O `OllamaService` trata só da parte HTTP (falar com `/api/generate`).
4. O `AssistantService` interpreta o JSON devolvido. **Nunca confia
   cegamente**: se o `type` for `action`, verifica no `ActionService` se a
   ação existe antes de a executar. Se não existir, avisa e não executa nada.
5. Se a ação pedida tiver `RequiresConfirmation = true`, o assistente
   pergunta "sim/não" antes de executar — fica à espera da próxima frase do
   utilizador para confirmar ou cancelar.
6. O `ActionService` é a única porta de entrada para executar código no
   sistema operativo — só as ações explicitamente registadas em `Program.cs`
   podem correr. Não há execução arbitrária de comandos.

Vamos rever este fluxo ficheiro a ficheiro depois de confirmares que tudo
compila e corre no teu ambiente.

## Roteiro (próximas versões)

- **V2** — arquitetura/refactoring
- **V3** — testes avançados
- **V4** — memória persistente
- **V5** — mais ferramentas/ações
- **V6** — speech-to-text
- **V7** — text-to-speech
- **V8** — aplicação Windows com interface gráfica
- **V9** — Docker
- **V10** — CI/CD
- **V11** — funcionalidades avançadas de agente pessoal
