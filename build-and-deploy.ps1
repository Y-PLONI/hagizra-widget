# Builds the provider and registers it as a loose (unsigned) package for local use.
# Needs Visual Studio 2022 (its MSBuild has the packaging tasks WindowsAppSDK requires) and Developer Mode.
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$rid = @{ x64 = 'win-x64'; x86 = 'win-x86'; ARM64 = 'win-arm64' }[$Platform]
$layout = Join-Path $root "artifacts\layout\$Platform"

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio 2022 with MSBuild was not found.' }

# A running provider or notifier locks its exe; Windows starts the provider again on the next widget activation.
Get-Process HagizraWidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
$publishDir = Join-Path $layout 'HagizraWidgetProvider'

& $msbuild (Join-Path $root 'HagizraWidgetProvider\HagizraWidgetProvider.csproj') -restore -t:Publish `
    -p:Configuration=Release -p:RuntimeIdentifier=$rid -p:SelfContained=true "-p:PublishDir=$publishDir\" -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Copy-Item (Join-Path $root 'Package\ProviderAssets') (Join-Path $layout 'ProviderAssets') -Recurse
Copy-Item (Join-Path $root 'Package\Public') (Join-Path $layout 'Public') -Recurse
(Get-Content (Join-Path $root 'Package\AppxManifest.template.xml') -Raw -Encoding utf8).Replace('@ARCH@', $Platform.ToLower()) |
    Set-Content (Join-Path $layout 'AppxManifest.xml') -Encoding utf8

# Registering another layout under the same identity keeps the old one, so remove it first.
$existing = Get-AppxPackage HagizraWidget
if ($existing) { Remove-AppxPackage $existing.PackageFullName }

Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml') -ForceApplicationShutdown
Write-Host "Registered from $layout. Press Win+W, click +, and add the Hagizra widget."
