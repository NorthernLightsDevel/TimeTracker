[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [string]$OutputName = "TimeTracker.msi",
    [string]$Wix = "wix"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot '..' '..')
$publishRoot = Join-Path $scriptRoot 'publish'

if (-not (Get-Command $Wix -ErrorAction SilentlyContinue)) {
    throw "The WiX CLI ('$Wix') could not be found. Install WiX v4 and make sure 'wix.exe' is on PATH or pass -Wix <path>."
}

if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot | Out-Null

function Publish-Project {
    param(
        [string]$Project,
        [string]$Output,
        [switch]$SingleFile
    )

    $args = @(
        'publish',
        $Project,
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', 'true',
        '-o', $Output
    )

    if ($SingleFile) {
        $args += '-p:PublishSingleFile=true'
        $args += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }

    dotnet @args
}

$desktopPublish = Join-Path $publishRoot 'desktop'
$cliPublish = Join-Path $publishRoot 'cli'
$apiPublish = Join-Path $publishRoot 'api'

Publish-Project (Join-Path $repoRoot 'src/TimeTracker.Desktop/TimeTracker.Desktop.csproj') $desktopPublish
Publish-Project (Join-Path $repoRoot 'src/TimeTracker.Cli/TimeTracker.Cli.csproj') $cliPublish
Publish-Project (Join-Path $repoRoot 'src/TimeTracker.Api/TimeTracker.Api.csproj') $apiPublish -SingleFile

Copy-Item (Join-Path $repoRoot 'src/TimeTracker.Api/appsettings.json') $apiPublish -Force
if (Test-Path (Join-Path $repoRoot 'src/TimeTracker.Api/appsettings.Windows.json')) {
    Copy-Item (Join-Path $repoRoot 'src/TimeTracker.Api/appsettings.Windows.json') $apiPublish -Force
}

function Invoke-Harvest {
    param(
        [string]$Source,
        [string]$Fragment,
        [string]$GroupId,
        [string]$DirectoryId,
        [string]$BindVariable
    )

    & $Wix harvest `
        $Source `
        -var "var.$BindVariable" `
        -cg $GroupId `
        -dr $DirectoryId `
        -out $Fragment `
        -srd `
        -sfrag
}

$desktopFragment = Join-Path $scriptRoot 'DesktopFiles.wxs'
$cliFragment = Join-Path $scriptRoot 'CliFiles.wxs'

Invoke-Harvest $desktopPublish $desktopFragment 'DesktopFiles' 'DesktopDir' 'DesktopPublish'
Invoke-Harvest $cliPublish $cliFragment 'CliFiles' 'CliDir' 'CliPublish'

$wxs = Join-Path $scriptRoot 'TimeTracker.wxs'

& $Wix build `
    $wxs `
    $desktopFragment `
    $cliFragment `
    -dDesktopPublish=$desktopPublish `
    -dCliPublish=$cliPublish `
    -dApiPublish=$apiPublish `
    -dProductVersion=$Version `
    -ext WixToolset.Util.wixext `
    -out (Join-Path $scriptRoot $OutputName)

Write-Host "MSI written to" (Join-Path $scriptRoot $OutputName)
