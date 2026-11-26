# Cyl Win

Meu Windows 10, refatorado: leve, bonito e do meu jeito.

O Cyl Win não é uma ISO nova. É **um app próprio** (a interface: barras, modal do PC, volume, IA) mais **scripts PowerShell** (limpeza e ajustes do sistema), aplicados por cima do Windows 10 que já está instalado, sempre com backup antes. Tudo feito do zero com o Claude Code, sem depender de apps de terceiros. Tudo que está aqui é versionado, então dá para refazer o setup em outro PC ou depois de uma formatação.

## Estrutura

| Pasta | O que tem |
|---|---|
| `docs/ROADMAP.md` | Visão, fases, ferramentas e riscos |
| `src/CylWin/` | O app Cyl Win (C# .NET 8 + WPF). Veja o README dele para rodar |
| `docs/design/` | Mockups da interface |
| `scripts/` | Scripts PowerShell numerados por fase |
| `configs/` | Exportações de configurações (Windhawk, Rainmeter, PowerToys, etc.) *(a criar)* |

## Regra de ouro

1. Ponto de restauração **antes** de qualquer fase.
2. Uma fase por vez. Usa o PC uns dias, depois segue.
3. Nada roda sem eu ler o script antes.

## Começando

Abra o PowerShell **como administrador** e rode a Fase 0 (só faz diagnóstico e backup, não muda nada no sistema além de criar o ponto de restauração se pedido):

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\00-backup-e-diagnostico.ps1 -CriarPontoRestauracao
```

Ele gera um relatório em `Documentos\CylWin\` com hardware, programas instalados, itens de inicialização e serviços.
