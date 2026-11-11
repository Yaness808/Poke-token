$ErrorActionPreference = 'Stop'
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$entry = Get-ItemProperty -LiteralPath $run -Name 'CodexTokenPet' -ErrorAction SilentlyContinue
if ($null -ne $entry) { Remove-ItemProperty -LiteralPath $run -Name 'CodexTokenPet' }
$exe = Join-Path $PSScriptRoot 'CodexTokenPet.exe'
Get-Process -Name CodexTokenPet -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe } | Stop-Process
Write-Output 'Codex Token Pet autostart is disabled.'
