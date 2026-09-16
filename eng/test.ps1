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

$smokeProjectName = $repositoryConfig.AotProjectName
$smokeConfiguration = $Configuration.ToLowerInvariant()
foreach ($framework in @("net9.0-windows10.0.22621.0", "net10.0-windows10.0.22621.0")) {
    $smokePath = Join-Path $repositoryRoot "artifacts/bin/$smokeProjectName/${smokeConfiguration}_$framework/$smokeProjectName.dll"
    foreach ($mode in @("--exercise-com-lifetime", "--exercise-com-lifetime-legacy")) {
        & dotnet $smokePath $mode --enable-efficiency-mode
        if ($LASTEXITCODE -ne 0) {
            throw "COM lifetime smoke test '$framework/$mode' failed with exit code $LASTEXITCODE."
        }
    }

    foreach ($legacy in @($false, $true)) {
        foreach ($endSession in @($false, $true)) {
            foreach ($throwOnDispose in @($false, $true)) {
                $smokeArguments = @("--exercise-com-shutdown")
                if ($legacy) { $smokeArguments += "--legacy-factory" }
                if ($endSession) { $smokeArguments += "--end-session" }
                if ($throwOnDispose) { $smokeArguments += "--throw-on-dispose" }
                & dotnet $smokePath @smokeArguments
                if ($LASTEXITCODE -ne 0) {
                    throw "COM shutdown smoke test '$framework/$($smokeArguments -join ' ')' failed with exit code $LASTEXITCODE."
                }
            }
        }
    }
}
