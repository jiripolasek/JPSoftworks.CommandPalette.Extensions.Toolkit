[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "Versioning.psm1") -Force

$versionProperties = @()
if ($Version) {
    $versionProperties = @(Get-ToolkitVersionProperties -Version $Version)
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$solutionPath = Join-Path $repositoryRoot $repositoryConfig.SolutionPath

$commonProperties = @(
    "-p:UseArtifactsOutput=true",
    "-p:GeneratePackageOnBuild=false"
)

if (-not $NoRestore) {
    & dotnet restore $solutionPath --locked-mode @commonProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Local restore failed with exit code $LASTEXITCODE."
    }
}

$arguments = @(
    "build",
    $solutionPath,
    "--configuration",
    $Configuration,
    "--no-restore"
)

$arguments += $versionProperties
$arguments += $commonProperties

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local build failed with exit code $LASTEXITCODE."
}

Write-Host "Build output: $(Join-Path $repositoryRoot 'artifacts\bin')"
