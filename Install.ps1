$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'CodexTokenPet.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (-not (Test-Path -LiteralPath $run)) { New-Item -Path $run -Force | Out-Null }
New-ItemProperty -LiteralPath $run -Name 'CodexTokenPet' -PropertyType String -Value ('"' + $exe + '"') -Force | Out-Null
Start-Process -FilePath $exe -WindowStyle Hidden
Write-Output 'Codex Token Pet is enabled for this Windows user.'
