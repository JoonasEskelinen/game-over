# Windows Release -export asiakasesittelyyn (Godot 4.x .NET).
# Pi/ARM-build: export_presets.cfg preset "Linux" + docs/RASPBERRY_PI5_KOTIKONSOLI.md
param(
    [string]$GodotExe = "",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ExportPreset = "Windows"
$OutputDir = Join-Path $ProjectRoot "versiot"

function Find-GodotExe {
    if ($GodotExe -and (Test-Path $GodotExe)) {
        return (Resolve-Path $GodotExe).Path
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Godot\Godot_v4.6.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }

    $fromPath = Get-Command godot -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "Godot .NET -editoria ei löytynyt. Anna polku: .\build-windows.ps1 -GodotExe 'C:\polku\Godot_v4.6.1-stable_mono_win64.exe'"
}

$godot = Find-GodotExe
Write-Host "Godot: $godot"
Write-Host "Projekti: $ProjectRoot"
Write-Host "Preset: $ExportPreset ($Configuration)"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$exportFlag = if ($Configuration -eq "Release") { "--export-release" } else { "--export-debug" }

$proc = Start-Process -FilePath $godot `
    -ArgumentList "--headless", "--path", $ProjectRoot, $exportFlag, $ExportPreset `
    -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    throw "Godot-export epäonnistui (exit $($proc.ExitCode))."
}

$exePath = Join-Path $OutputDir "GameOver.exe"
if (-not (Test-Path $exePath)) {
    throw "Export valmistui, mutta $exePath puuttuu."
}

$zipPath = Join-Path $OutputDir "GameOver-windows-demo.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$staging = Join-Path $env:TEMP "GameOver-windows-demo"
if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Copy-Item $exePath $staging
Copy-Item (Join-Path $OutputDir "GameOver.pck") $staging -ErrorAction Stop
Copy-Item (Join-Path $OutputDir "data_GameOver_windows_x86_64") $staging -Recurse -ErrorAction Stop
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -Force
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "Valmis. Käynnistä esittely:"
Write-Host "  $exePath"
Write-Host ""
Write-Host "Asiakkaalle jaettava zip:"
Write-Host "  $zipPath"
Write-Host "(sisältää exe + pck + data_GameOver_windows_x86_64)."
