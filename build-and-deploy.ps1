# Builds the package layout and registers it as a loose (unsigned) package for local use. Needs Developer Mode.
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$layout = Join-Path $PSScriptRoot "artifacts\layout\$Platform"

# A running provider or notifier locks its exe; Windows starts the provider again on the next widget activation.
Get-Process HagizraWidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force

& (Join-Path $PSScriptRoot 'tools\build-layout.ps1') -Platform $Platform -Layout $layout

# Registering another layout under the same identity keeps the old one, so remove it first.
$existing = Get-AppxPackage HagizraWidget
if ($existing) { Remove-AppxPackage $existing.PackageFullName }

Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml') -ForceApplicationShutdown
Write-Host "Registered from $layout. Press Win+W, click +, and add the Hagizra widget."
