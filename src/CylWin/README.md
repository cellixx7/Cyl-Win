# Cyl Win (o app)

Versão atual: **0.2**

O programa que desenha a interface do Cyl Win por cima do Windows 10. Feito do zero em C# (.NET 8 + WPF), sem depender de apps de terceiros.

## O que a versão 0.1 faz

- **Barra de vidro no topo**, centralizada, que fica escondida e desce quando o mouse encosta na borda de cima.
- **Botão "Cyl"**: passando o mouse abre o modal do PC com CPU, temperatura, GPU, RAM, discos C: e D: (espaço livre e barra de ocupação) e tempo ligado. Clicando abre o Gerenciador de Tarefas.
- **Rede, volume, notificações e relógio** na direita. Por enquanto o volume abre o mixer do Windows e o sino abre a Central de Ações; as versões próprias vêm nas próximas etapas.
- Botão direito na barra: **Fixar barra** (não esconder) e **Sair**.

Os números só são medidos enquanto o modal está aberto, para o app ficar leve.

## Limitações conhecidas

- **Temperatura**: o Windows só entrega a temperatura do Ryzen por um driver. O app usa a biblioteca do LibreHardwareMonitor (embutida) com o driver **PawnIO**, que é assinado e precisa ser instalado uma vez (`winget install namazso.PawnIO`). Além disso o Cyl Win precisa rodar **como administrador**. Se faltar algo, o modal mostra "—" e diz o motivo ao lado de "CPU".
- Por enquanto só no monitor principal.

## Como rodar

### Opção 1: pelo Claude Code (ou terminal), no seu PC

Precisa do **.NET 8 SDK** (gratuito):

```powershell
winget install Microsoft.DotNet.SDK.8
cd caminho\do\Cyl-Win
dotnet run --project src\CylWin
```

### Opção 2: baixar o .exe pronto

Cada push compila o app no GitHub Actions. Em **Actions → Build Cyl Win → último run → Artifacts → CylWin**, baixe e rode `CylWin.exe`. Precisa do **.NET 8 Desktop Runtime** (`winget install Microsoft.DotNet.DesktopRuntime.8`).

### Iniciar junto com o Windows

Crie um atalho para o `CylWin.exe` em `shell:startup` (Win+R → `shell:startup`).

## Arquivos

| Arquivo | O que faz |
|---|---|
| `TopBar.xaml` / `.cs` | A barra, o modal do PC e a lógica de esconder/mostrar |
| `SystemStats.cs` | Lê CPU, GPU, RAM, rede e temperatura do Windows |
| `Native.cs` | Chamadas Win32: vidro, cantos arredondados, mouse, memória |
| `App.xaml` | Cores e estilos do Cyl Win |
