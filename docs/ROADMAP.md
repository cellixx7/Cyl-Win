# Cyl Win — Visão e Roadmap

## A visão

Um Windows 10 que:

- **Liga rápido e fica leve**: sem bloatware, sem telemetria desnecessária, poucos serviços e processos em segundo plano.
- **Tem cara própria**: barra de tarefas, menu iniciar, bandeja, ícones, cores e widgets no meu estilo.
- **Organiza as janelas sozinho**: layouts de tela, atalhos para posicionar e trocar de área de trabalho.
- **Controla o áudio direito**: volume por app, troca rápida de saída, equalizador.
- **Conversa com APIs**: atalhos e widgets que puxam dados (clima, agenda, GitHub, o que eu quiser) e automações.
- **Tem uma IA local, offline e barata**: um modelo pequeno rodando na minha máquina, chamado por atalho.

## Fases (em ordem de prioridade)

Cada fase é independente e reversível. A ordem vai do mais seguro e com mais ganho para o mais arriscado.

### Fase 0 — Fundação e backup (obrigatória)
- Ponto de restauração do sistema.
- Backup de documentos importantes (nuvem ou HD externo). Ideal: imagem do disco com **Macrium Reflect Free** ou **Veeam Agent**.
- Diagnóstico: hardware, versão do Windows, programas, inicialização, serviços (`scripts/00-backup-e-diagnostico.ps1`).
- Criar um pendrive de recuperação do Windows 10 (Ferramenta de Criação de Mídia).

### Fase 1 — Limpeza e desempenho
- **Debloat** com script de confiança, em modo conservador:
  - [Win11Debloat](https://github.com/Raphire/Win11Debloat) (funciona no 10 também) ou
  - [Chris Titus WinUtil](https://github.com/ChrisTitusTech/winutil) (interface gráfica, ótimo para escolher item por item).
- Remover apps pré-instalados que não uso, desligar sugestões e anúncios do menu iniciar.
- Privacidade: reduzir telemetria com **O&O ShutUp10++** (usar só os itens "recomendados" no começo).
- Inicialização: tirar do boot o que não precisa (Gerenciador de Tarefas ou **Autoruns** da Sysinternals).
- Plano de energia: Alto desempenho / Desempenho máximo (desktop) ou equilibrado ajustado (notebook).
- Limpeza de disco e `DISM`/`sfc` para garantir que a base está saudável.
- Instalar programas daqui em diante pelo **winget** (lista versionada no repositório).

### Fase 2 — Shell: barra de tarefas, menu iniciar e bandeja
- **[Windhawk](https://windhawk.net/)**: mods pequenos e reversíveis para a barra de tarefas, bandeja, relógio, menu de contexto. É a ferramenta central do Cyl Win.
- **[ExplorerPatcher](https://github.com/valinet/ExplorerPatcher)**: opções avançadas da barra e do menu iniciar. (Atenção: alguns antivírus acusam falso positivo; baixar só do GitHub oficial.)
- **[Open-Shell](https://github.com/Open-Shell/Open-Shell-Menu)**: menu iniciar clássico e personalizável, se eu quiser trocar o do Windows.
- **[TranslucentTB](https://github.com/TranslucentTB/TranslucentTB)**: barra transparente ou com blur.
- Alternativa radical: **[Yasb](https://github.com/amnweb/yasb)** ou **Zebar** como barra superior customizada (estilo Linux "rice").

### Fase 3 — Visual
- Tema escuro, cor de destaque, fontes (ex.: Inter / JetBrains Mono / Segoe UI Variable).
- **[Rainmeter](https://www.rainmeter.net/)**: widgets na área de trabalho (relógio, CPU, RAM, rede, player de música, dados de APIs).
- Ícones e cursores personalizados.
- **Wallpaper Engine** (pago, barato) ou **Lively Wallpaper** (grátis) para papel de parede animado.
- Opcional e mais arriscado: temas de sistema com **SecureUxTheme** (mexe em arquivo de sistema, fica para depois).

### Fase 4 — Janelas e produtividade
- **[PowerToys](https://github.com/microsoft/PowerToys)**: FancyZones (zonas de janela), PowerToys Run (lançador), Keyboard Manager (remapear teclas), Always on Top, Color Picker.
- Para quem gosta de tiling estilo Linux: **[komorebi](https://github.com/LGUG2Z/komorebi)** ou **GlazeWM**.
- **AutoHotkey v2**: atalhos próprios do Cyl Win (abrir IA, trocar áudio, posicionar janelas, etc.).
- Áreas de trabalho virtuais com atalhos.

### Fase 5 — Áudio
- **[EarTrumpet](https://github.com/File-New-Project/EarTrumpet)**: volume por aplicativo direto na bandeja.
- **SoundSwitch**: trocar fone/caixa com um atalho.
- **Equalizer APO + Peace**: equalizador para o sistema todo.
- Opcional: **VoiceMeeter** para mixagem e roteamento (streams, chamadas).

### Fase 6 — Integração com APIs e automações
- Um pequeno "núcleo" do Cyl Win em **PowerShell ou Python** com comandos: `cyl clima`, `cyl agenda`, `cyl gh`, etc.
- Widgets do Rainmeter alimentados por essas APIs.
- Notificações nativas do Windows com **BurntToast** (PowerShell).
- Agendamentos pelo Agendador de Tarefas.

### Fase 7 — IA local, offline e barata
- **[Ollama](https://ollama.com/)** (mais fácil) ou **llama.cpp** (mais leve e controlável).
- Modelos pequenos que rodam bem em PC comum (escolha depende de RAM e GPU):
  - 4 a 8 GB de RAM livre, sem GPU: modelos de 1B a 4B quantizados (ex.: Llama 3.2 3B, Qwen 2.5 3B, Gemma 3 4B, Phi).
  - GPU com 8 GB+ de VRAM: modelos de 7B a 8B quantizados rodam com folga.
- Interface: atalho global (AutoHotkey ou PowerToys Run) que abre uma caixinha de pergunta; ou **Open WebUI** no navegador.
- Usos: explicar erros, resumir texto da área de transferência, gerar comandos, assistente do próprio Cyl Win.
- Voz offline (opcional): **whisper.cpp** para ditado e **Piper** para leitura em voz.

### Fase 8 — Empacotar o Cyl Win
- Script único `install.ps1` que reaplica tudo num Windows limpo.
- Configurações exportadas em `configs/`.
- Lista de apps no `winget` (`winget export` / `winget import`).

## Riscos e cuidados

- **Sempre** ponto de restauração antes de cada fase; para mudanças grandes, imagem do disco.
- **Debloat agressivo quebra coisas**: Microsoft Store, Windows Update, Xbox/Game Pass, impressoras, pesquisa. Começar no modo conservador.
- **Não desativar o Windows Update nem o Defender** de vez. Dá para controlar quando atualiza.
- **Atualizações do Windows** podem desfazer ou quebrar mods de shell (ExplorerPatcher, temas). O Windhawk costuma ser o mais resiliente.
- **Só baixar ferramentas das fontes oficiais** (GitHub do projeto ou site oficial). Nada de "Windows Lite" pronto de site aleatório.
- **Fim de suporte do Windows 10**: a Microsoft encerrou o suporte comum em 14/10/2025. Vale ativar o **ESU** (atualizações de segurança estendidas) se disponível para mim, e manter navegador e antivírus atualizados. O Cyl Win pode migrar para o Windows 11 no futuro, a maioria das ferramentas funciona lá também.
- Uma mudança por vez, e anotar no repositório o que foi feito (fica fácil saber o que causou um problema).
