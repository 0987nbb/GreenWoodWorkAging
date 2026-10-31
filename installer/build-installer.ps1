<#
.SYNOPSIS
    Builds the single Setup.exe that a customer installs Green Wood Works from.

.DESCRIPTION
    Publishes the app as a self-contained win-x64 folder (so the customer needs no .NET,
    no SQLite and no OCR setup of their own) and compiles installer\GreenWoodWorkAging.iss
    against it. The version stamped into the setup comes from <Version> in the csproj, so
    the two never drift apart.

    Requires Inno Setup 6. If it is missing, install it with:
        winget install --id JRSoftware.InnoSetup -e --scope user

.EXAMPLE
    powershell -File installer\build-installer.ps1
#>
[CmdletBinding()]
param(
    # Overrides the csproj version, e.g. for a one-off build.
    [string] $Version,

    # Reuse the folder from a previous run instead of publishing again.
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $PSScriptRoot
$csproj     = Join-Path $root 'GreenWoodWorkAging.csproj'
$publishDir = Join-Path $root 'publish\GreenWoodWorkAging'
$script     = Join-Path $PSScriptRoot 'GreenWoodWorkAging.iss'

if (-not $Version) {
    $match = [regex]::Match((Get-Content -Raw $csproj), '<Version>([^<]+)</Version>')
    if (-not $match.Success) { throw "GreenWoodWorkAging.csproj has no <Version> to build the installer from." }
    $Version = $match.Groups[1].Value
}

if (-not $SkipPublish) {
    Write-Host "Publishing $csproj (self-contained, win-x64) ..."
    dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir
    if ($LASTEXITCODE) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
}

# The per-user winget install lands beside the user's own programs, not in Program Files.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup -e --scope user"
}

Write-Host "Compiling installer with $iscc ..."
& $iscc "/DAppVersion=$Version" $script
if ($LASTEXITCODE) { throw "ISCC failed with exit code $LASTEXITCODE." }

Get-ChildItem (Join-Path $PSScriptRoot 'Output\*.exe') |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
    ForEach-Object { Write-Host ("Setup built: {0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
