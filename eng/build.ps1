[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "JPSoftworks.CommandPalette.Extensions.Toolkit.slnx"

$arguments = @(
    "build",
    $solutionPath,
    "--configuration",
    $Configuration,
    "-p:UseArtifactsOutput=true",
    "-p:GeneratePackageOnBuild=false"
)

if ($NoRestore) {
    $arguments += "--no-restore"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local build failed with exit code $LASTEXITCODE."
}

Write-Host "Build output: $(Join-Path $repositoryRoot 'artifacts\bin')"
