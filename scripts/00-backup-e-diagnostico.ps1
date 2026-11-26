<#
.SYNOPSIS
    Cyl Win - Fase 0: diagnóstico do sistema e ponto de restauração.

.DESCRIPTION
    Este script NÃO altera configurações do Windows. Ele apenas:
      - coleta informações de hardware, sistema, programas, inicialização e serviços;
      - salva tudo em Documentos\CylWin\diagnostico-<data>\;
      - opcionalmente cria um ponto de restauração (parâmetro -CriarPontoRestauracao).

.EXAMPLE
    Set-ExecutionPolicy -Scope Process Bypass
    .\00-backup-e-diagnostico.ps1 -CriarPontoRestauracao
#>
[CmdletBinding()]
param(
    [switch]$CriarPontoRestauracao
)

$ErrorActionPreference = 'Continue'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$out = Join-Path ([Environment]::GetFolderPath('MyDocuments')) "CylWin\diagnostico-$stamp"
New-Item -ItemType Directory -Path $out -Force | Out-Null
Write-Host "Cyl Win - Fase 0. Salvando em: $out" -ForegroundColor Cyan

# 1. Ponto de restauração (opcional)
if ($CriarPontoRestauracao) {
    if (-not $isAdmin) {
        Write-Warning 'Ponto de restauração exige PowerShell como administrador. Pulando.'
    } else {
        try {
            Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction Stop
            Checkpoint-Computer -Description "CylWin Fase 0 $stamp" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            Write-Host 'Ponto de restauração criado.' -ForegroundColor Green
        } catch {
            Write-Warning "Não consegui criar o ponto de restauração: $($_.Exception.Message)"
            Write-Warning 'O Windows só permite um ponto a cada 24h por padrão. Se já existe um recente, tudo bem.'
        }
    }
}

# 2. Sistema e hardware
Write-Host 'Coletando sistema e hardware...'
$os   = Get-CimInstance Win32_OperatingSystem
$cs   = Get-CimInstance Win32_ComputerSystem
$cpu  = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpus = Get-CimInstance Win32_VideoController
$disks = Get-PhysicalDisk -ErrorAction SilentlyContinue | Select-Object FriendlyName, MediaType, BusType, @{n='TamanhoGB';e={[math]::Round($_.Size/1GB)}}
$vols = Get-Volume | Where-Object DriveLetter | Select-Object DriveLetter, FileSystemLabel, @{n='TotalGB';e={[math]::Round($_.Size/1GB,1)}}, @{n='LivreGB';e={[math]::Round($_.SizeRemaining/1GB,1)}}
$ver  = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'

$resumo = [ordered]@{
    Windows        = "$($os.Caption) $($ver.DisplayVersion) (build $($os.BuildNumber).$($ver.UBR))"
    Arquitetura    = $os.OSArchitecture
    Fabricante     = "$($cs.Manufacturer) $($cs.Model)"
    Tipo           = if ((Get-CimInstance Win32_SystemEnclosure).ChassisTypes -match '^(8|9|10|11|14|30|31|32)$') { 'Notebook' } else { 'Desktop' }
    CPU            = "$($cpu.Name.Trim()) - $($cpu.NumberOfCores) núcleos / $($cpu.NumberOfLogicalProcessors) threads"
    RAM_GB         = [math]::Round($cs.TotalPhysicalMemory/1GB,1)
    GPU            = ($gpus | ForEach-Object { "$($_.Name) ($([math]::Round($_.AdapterRAM/1GB,1)) GB reportados)" }) -join '; '
    Resolucao      = ($gpus | Where-Object CurrentHorizontalResolution | ForEach-Object { "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution) @ $($_.CurrentRefreshRate)Hz" }) -join '; '
    UltimoBoot     = $os.LastBootUpTime
    Admin          = $isAdmin
}
$resumo | ConvertTo-Json | Set-Content "$out\resumo.json" -Encoding UTF8
$disks | Format-Table -AutoSize | Out-String | Set-Content "$out\discos.txt" -Encoding UTF8
$vols  | Format-Table -AutoSize | Out-String | Add-Content "$out\discos.txt" -Encoding UTF8

# 3. Programas instalados
Write-Host 'Listando programas instalados...'
$regPaths = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'
Get-ItemProperty $regPaths -ErrorAction SilentlyContinue |
    Where-Object DisplayName |
    Select-Object DisplayName, DisplayVersion, Publisher, InstallDate |
    Sort-Object DisplayName -Unique |
    Export-Csv "$out\programas.csv" -NoTypeInformation -Encoding UTF8

Get-AppxPackage | Select-Object Name, Version | Sort-Object Name |
    Export-Csv "$out\apps-store.csv" -NoTypeInformation -Encoding UTF8

if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget export -o "$out\winget-apps.json" --accept-source-agreements 2>$null | Out-Null
}

# 4. Inicialização e serviços
Write-Host 'Listando inicialização e serviços...'
Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location, User |
    Export-Csv "$out\inicializacao.csv" -NoTypeInformation -Encoding UTF8
Get-Service | Where-Object Status -eq 'Running' | Select-Object Name, DisplayName, StartType |
    Sort-Object DisplayName | Export-Csv "$out\servicos-rodando.csv" -NoTypeInformation -Encoding UTF8

# 5. Processos que mais usam memória agora
Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 25 Name,
    @{n='MemoriaMB';e={[math]::Round($_.WorkingSet64/1MB)}}, Id |
    Export-Csv "$out\processos-top.csv" -NoTypeInformation -Encoding UTF8

# 6. Pontos de restauração existentes
if ($isAdmin) {
    Get-ComputerRestorePoint -ErrorAction SilentlyContinue |
        Select-Object SequenceNumber, Description, @{n='Data';e={$_.ConvertToDateTime($_.CreationTime)}} |
        Format-Table -AutoSize | Out-String | Set-Content "$out\pontos-restauracao.txt" -Encoding UTF8
}

Write-Host ''
Write-Host '===== Resumo =====' -ForegroundColor Cyan
$resumo.GetEnumerator() | ForEach-Object { '{0,-12} {1}' -f $_.Key, $_.Value }
Write-Host ''
Write-Host "Pronto. Arquivos em: $out" -ForegroundColor Green
Write-Host 'Mande o resumo.json (e se quiser os CSVs) para planejarmos a Fase 1.'
