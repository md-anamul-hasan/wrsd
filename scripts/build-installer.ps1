<#
.SYNOPSIS  Builds a self-contained WRSD and packages it as dist\installer\WRSD-Setup-<version>.exe (needs Inno Setup 6).
#>
param(
    [string]$Version,
    [switch]$PublishOnly,  # stop after filling dist\stage (CI signs the binary there before packaging)
    [switch]$SkipPublish   # package an existing dist\stage as-is
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) { $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version }
$stage = Join-Path $root 'dist\stage'

if ($PublishOnly -and $SkipPublish) { throw '-PublishOnly and -SkipPublish cannot be combined' }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc -and -not $PublishOnly) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

if (-not $SkipPublish) {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    # Self-contained single file, so the installed app needs no separate .NET runtime
    dotnet publish (Join-Path $root 'src/Wrsd.UI/Wrsd.UI.csproj') -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:ContinuousIntegrationBuild=true -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=en `
        "-p:PathMap=$root=/_/" -o $stage --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

    # wrsd-cli.exe: small framework-dependent single file (the app offers a one-click install of the shared .NET 10 runtime)
    $cli = Join-Path $root 'dist\cli'
    if (Test-Path $cli) { Remove-Item $cli -Recurse -Force }
    dotnet publish (Join-Path $root 'src/Wrsd.Cli/Wrsd.Cli.csproj') -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:ContinuousIntegrationBuild=true -p:DebugType=none -p:DebugSymbols=false `
        "-p:PathMap=$root=/_/" -o $cli --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (cli) failed' }
    Copy-Item (Join-Path $cli 'Wrsd.Cli.exe') (Join-Path $stage 'wrsd-cli.exe')
}

if ($PublishOnly) { return }

& $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\wrsd.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }

Get-Item (Join-Path $root "dist\installer\WRSD-Setup-$Version.exe") | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
