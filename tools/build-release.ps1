# Produces the release files (unsigned): the .msix, the Windows App Runtime dependency, install.ps1 and the .appinstaller.
# The GitHub workflow signs the .msix afterwards; run it locally to check that a release can be assembled.
param(
    [Parameter(Mandatory)]
    [string]$Version,        # four parts, e.g. 1.0.7.0
    [Parameter(Mandatory)]
    [string]$Repository,     # owner/name on GitHub
    [Parameter(Mandatory)]
    [string]$OutDir,
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rid = @{ x64 = 'win10-x64'; x86 = 'win10-x86'; ARM64 = 'win10-arm64' }[$Platform]
$tag = 'v' + $Version.Substring(0, $Version.LastIndexOf('.'))
$latestUrl = "https://github.com/$Repository/releases/latest/download"
$tagUrl = "https://github.com/$Repository/releases/download/$tag"

New-Item -ItemType Directory -Force $OutDir | Out-Null
$layout = Join-Path ([IO.Path]::GetTempPath()) "hagizra-release-$([guid]::NewGuid().ToString('N'))"
try {
    & (Join-Path $PSScriptRoot 'build-layout.ps1') -Platform $Platform -Layout $layout -Version $Version

    $makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $makeappx) { throw 'makeappx.exe (Windows SDK) was not found.' }

    $msix = Join-Path $OutDir "HagizraWidget_${Version}_$Platform.msix"
    & $makeappx.FullName pack /d $layout /p $msix /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'makeappx failed.' }
}
finally {
    Remove-Item $layout -Recurse -Force -ErrorAction SilentlyContinue
}

# The Windows App Runtime framework, straight from the NuGet package the provider is built against.
$sdkVersion = ([xml](Get-Content (Join-Path $root 'HagizraWidgetProvider\HagizraWidgetProvider.csproj'))).Project.ItemGroup.PackageReference |
    Where-Object { $_.Include -eq 'Microsoft.WindowsAppSDK' } | ForEach-Object { $_.Version } | Select-Object -First 1
$runtime = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windowsappsdk\$sdkVersion\tools\MSIX\$rid\Microsoft.WindowsAppRuntime.1.6.msix"
if (-not (Test-Path $runtime)) { throw "Windows App Runtime package not found: $runtime" }
Copy-Item $runtime (Join-Path $OutDir 'Dependency_Microsoft.WindowsAppRuntime.1.6.msix')

Copy-Item (Join-Path $root '.github\scripts\install.ps1') $OutDir

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-Identity($file) {
    $zip = [IO.Compression.ZipFile]::OpenRead($file)
    try {
        $reader = New-Object IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
        return ([xml]$reader.ReadToEnd()).Package.Identity
    }
    finally { $zip.Dispose() }
}
function Esc($s) { [Security.SecurityElement]::Escape($s) }

$main = Get-Identity $msix
$deps = foreach ($d in Get-ChildItem $OutDir -Filter 'Dependency_*') {
    $id = Get-Identity $d.FullName
    "    <Package Name=""$(Esc $id.Name)"" Publisher=""$(Esc $id.Publisher)"" Version=""$($id.Version)"" ProcessorArchitecture=""$($id.ProcessorArchitecture)"" Uri=""$tagUrl/$($d.Name)"" />"
}

# Windows re-reads this file from the "latest" address; the packages it lists live under the release's own tag.
$xml = @(
    '<?xml version="1.0" encoding="utf-8"?>'
    "<AppInstaller xmlns=""http://schemas.microsoft.com/appx/appinstaller/2021"" Version=""$Version"" Uri=""$latestUrl/HagizraWidget.appinstaller"">"
    "  <MainPackage Name=""$(Esc $main.Name)"" Publisher=""$(Esc $main.Publisher)"" Version=""$Version"" ProcessorArchitecture=""$Platform"" Uri=""$tagUrl/$(Split-Path $msix -Leaf)"" />"
    if ($deps) { '  <Dependencies>'; $deps; '  </Dependencies>' }
    '  <UpdateSettings>'
    '    <OnLaunch HoursBetweenUpdateChecks="0" />'
    '    <AutomaticBackgroundTask />'
    '  </UpdateSettings>'
    '</AppInstaller>'
) -join "`n"
Set-Content (Join-Path $OutDir 'HagizraWidget.appinstaller') $xml -Encoding utf8 -NoNewline
