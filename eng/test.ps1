[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [switch] $NoBuild,

    [switch] $NoRestore,

    [string] $ResultsDirectory
)

$ErrorActionPreference = "Stop"

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
        throw "Local test restore failed with exit code $LASTEXITCODE."
    }
}

$arguments = @(
    "test",
    $solutionPath,
    "--configuration",
    $Configuration,
    "--no-restore"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

if ($ResultsDirectory) {
    $arguments += @("--logger", "trx", "--results-directory", $ResultsDirectory)
}

$arguments += $commonProperties

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local tests failed with exit code $LASTEXITCODE."
}
