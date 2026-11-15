param([switch]$Test)

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'
$source = Join-Path $PSScriptRoot 'src'
$output = Join-Path $PSScriptRoot 'CodexTokenPet.exe'
$sources = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File -Recurse |
    Sort-Object FullName | Select-Object -ExpandProperty FullName)
$target = '/target:winexe'
$entryPoint = @()
if ($Test) {
    $output = Join-Path ([IO.Path]::GetTempPath()) ('jieni-token-test-' + [guid]::NewGuid() + '.exe')
    $sources += Join-Path $PSScriptRoot 'tests\SmokeTests.cs'
    $target = '/target:exe'
    $entryPoint = @('/main:TokenPet.Tests.SmokeTests')
}

& $compiler /nologo $target @entryPoint /platform:x64 /optimize+ /codepage:65001 /langversion:5 `
    "/out:$output" "/win32manifest:$source\Desktop\TokenPet.manifest" `
    "/resource:$source\Desktop\Views\UsageDetails.xaml,TokenPet.UsageDetails.xaml" `
    "/resource:$source\Desktop\Pokemon\Picker.xaml,TokenPet.Picker.xaml" `
    "/resource:$PSScriptRoot\assets\kanto.json,TokenPet.Kanto.json" `
    "/r:$wpf\WindowsBase.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\PresentationFramework.dll" `
    "/r:$wpf\UIAutomationTypes.dll" "/r:$framework\System.Web.Extensions.dll" "/r:$framework\System.Xaml.dll" `
    "/r:$framework\System.Net.Http.dll" `
    @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Exit the running overlay before rebuilding.' }
if ($Test) {
    try {
        & $output
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    } finally {
        Remove-Item -LiteralPath $output -ErrorAction SilentlyContinue
    }
    return
}
Write-Output $output
