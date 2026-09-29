# Builds the provider and assembles the loose package layout (exe, images, manifest, resource index).
# Needs Visual Studio 2022 (its MSBuild has the packaging tasks WindowsAppSDK requires) and the Windows SDK (makepri).
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64',
    [Parameter(Mandatory)]
    [string]$Layout,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rid = @{ x64 = 'win-x64'; x86 = 'win-x86'; ARM64 = 'win-arm64' }[$Platform]

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio 2022 with MSBuild was not found.' }

if (Test-Path $Layout) { Remove-Item $Layout -Recurse -Force }
$publishDir = Join-Path $Layout 'HagizraWidgetProvider'

& $msbuild (Join-Path $root 'HagizraWidgetProvider\HagizraWidgetProvider.csproj') -restore -t:Publish `
    -p:Configuration=Release -p:RuntimeIdentifier=$rid -p:SelfContained=true "-p:PublishDir=$publishDir\" -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Copy-Item (Join-Path $root 'Package\ProviderAssets') (Join-Path $Layout 'ProviderAssets') -Recurse
Copy-Item (Join-Path $root 'Package\Public') (Join-Path $Layout 'Public') -Recurse

$manifest = (Get-Content (Join-Path $root 'Package\AppxManifest.template.xml') -Raw -Encoding utf8).Replace('@ARCH@', $Platform.ToLower())
if ($Version) {
    $identity = [regex]'(<Identity\b[^>]*?\bVersion=")[^"]*(")'
    if (-not $identity.IsMatch($manifest)) { throw 'Identity Version not found in the manifest template.' }
    $manifest = $identity.Replace($manifest, "`${1}$Version`${2}", 1)
}
$manifestPath = Join-Path $Layout 'AppxManifest.xml'
Set-Content $manifestPath $manifest -Encoding utf8

# Resource index for the images, like a Visual Studio build produces; only the asset folders are indexed, not the runtime.
$makepri = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makepri.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makepri) { throw 'makepri.exe (Windows SDK) was not found.' }
$priStage = Join-Path ([IO.Path]::GetTempPath()) "hagizra-pri-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $priStage | Out-Null
try {
    Copy-Item (Join-Path $root 'Package\ProviderAssets') (Join-Path $priStage 'ProviderAssets') -Recurse
    $priConfig = Join-Path $priStage 'priconfig.xml'
    & $makepri.FullName createconfig /cf $priConfig /dq he-IL /pv 10.0.0 /o | Out-Null
    & $makepri.FullName new /pr $priStage /cf $priConfig /mn $manifestPath /of (Join-Path $Layout 'resources.pri') /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'makepri failed.' }
}
finally {
    Remove-Item $priStage -Recurse -Force -ErrorAction SilentlyContinue
}
