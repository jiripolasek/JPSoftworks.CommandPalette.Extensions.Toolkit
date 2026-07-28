[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoPack
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packagePath = Join-Path $repositoryRoot "artifacts\packages"
$feedPath = Join-Path $repositoryRoot "artifacts\local-feed"

if (-not $NoPack) {
    $packArguments = @{
        Configuration = $Configuration
    }

    if ($Version) {
        $packArguments.Version = $Version
    }

    & (Join-Path $PSScriptRoot "pack.ps1") @packArguments
}

$packages = @(Get-ChildItem -LiteralPath $packagePath -Filter "*.nupkg" -File)
if ($packages.Count -eq 0) {
    throw "No NuGet packages were found in '$packagePath'. Run eng\pack.ps1 first or omit -NoPack."
}

$symbolPackages = @(Get-ChildItem -LiteralPath $packagePath -Filter "*.snupkg" -File)
$missingSymbolPackages = @(
    $packages | Where-Object {
        -not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($_.FullName, ".snupkg")) -PathType Leaf)
    }
)

if ($missingSymbolPackages.Count -ne 0) {
    $missingPackageNames = $missingSymbolPackages.Name -join ", "
    throw "Matching symbol packages were not found for: $missingPackageNames."
}

New-Item -ItemType Directory -Path $feedPath -Force | Out-Null

foreach ($package in $packages) {
    Copy-Item -LiteralPath $package.FullName -Destination $feedPath -Force
    Write-Host "Published package: $($package.Name)"
}

foreach ($symbolPackage in $symbolPackages) {
    Copy-Item -LiteralPath $symbolPackage.FullName -Destination $feedPath -Force
    Write-Host "Published symbols: $($symbolPackage.Name)"
}

Write-Host "Local NuGet feed: $feedPath"
