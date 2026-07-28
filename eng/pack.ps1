[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoBuild,

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "JPSoftworks.CommandPalette.Extensions.Toolkit.slnx"
$packagePath = Join-Path $repositoryRoot "artifacts\packages"

$arguments = @(
    "pack",
    $solutionPath,
    "--configuration",
    $Configuration,
    "-p:UseArtifactsOutput=true",
    "-p:GeneratePackageOnBuild=false"
)

if ($Version) {
    $arguments += "-p:PackageVersion=$Version"
}

if ($NoBuild) {
    $arguments += "--no-build"
}

if ($NoRestore) {
    $arguments += "--no-restore"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local pack failed with exit code $LASTEXITCODE."
}

$packages = @(Get-ChildItem -LiteralPath $packagePath -Filter "*.nupkg" -File)
if ($packages.Count -eq 0) {
    throw "Local pack completed without producing a NuGet package in '$packagePath'."
}

$symbolPackages = @(Get-ChildItem -LiteralPath $packagePath -Filter "*.snupkg" -File)
$missingSymbolPackages = @(
    $packages | Where-Object {
        -not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($_.FullName, ".snupkg")) -PathType Leaf)
    }
)

if ($missingSymbolPackages.Count -ne 0) {
    $missingPackageNames = $missingSymbolPackages.Name -join ", "
    throw "Local pack completed without matching symbol packages for: $missingPackageNames."
}

Write-Host "Packages:"
$packages.FullName | ForEach-Object { Write-Host "  $_" }

Write-Host "Symbol packages:"
$symbolPackages.FullName | ForEach-Object { Write-Host "  $_" }
