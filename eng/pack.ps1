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
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$solutionPath = Join-Path $repositoryRoot $repositoryConfig.SolutionPath
$packagePath = Join-Path $repositoryRoot $repositoryConfig.PackageOutputPath
$packagePropsPath = Join-Path $repositoryRoot $repositoryConfig.PackagePropsPath

if ($NoBuild -and $Version) {
    throw "-NoBuild cannot be combined with -Version because the existing assemblies may have a different version."
}

if (-not $Version) {
    [xml] $packageProps = Get-Content -Raw -LiteralPath $packagePropsPath
    $Version = [string](
        $packageProps.Project.PropertyGroup |
            ForEach-Object { $_.VersionPrefix } |
            Where-Object { $_ } |
            Select-Object -First 1)
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Package version could not be resolved from '$packagePropsPath'."
}

New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
Get-ChildItem -LiteralPath $packagePath -File |
    Where-Object { $_.Extension -in ".nupkg", ".snupkg" } |
    Remove-Item -Force

$commonProperties = @(
    "-p:UseArtifactsOutput=true",
    "-p:GeneratePackageOnBuild=false"
)

if (-not $NoRestore) {
    & dotnet restore $solutionPath --locked-mode @commonProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Local pack restore failed with exit code $LASTEXITCODE."
    }
}

$arguments = @(
    "pack",
    $solutionPath,
    "--configuration",
    $Configuration,
    "--no-restore",
    "-p:Version=$Version"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$arguments += $commonProperties

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local pack failed with exit code $LASTEXITCODE."
}

$expectedPackages = @(
    $repositoryConfig.PackageIds |
        ForEach-Object { Join-Path $packagePath "$_.$Version.nupkg" }
)
$expectedSymbolPackages = @(
    $repositoryConfig.PackageIds |
        ForEach-Object { Join-Path $packagePath "$_.$Version.snupkg" }
)

$missingOutputs = @(
    $expectedPackages + $expectedSymbolPackages |
        Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
)
if ($missingOutputs.Count -ne 0) {
    throw "Local pack did not produce the expected outputs: $($missingOutputs -join ', ')."
}

Write-Host "Packages:"
$expectedPackages | ForEach-Object { Write-Host "  $_" }

Write-Host "Symbol packages:"
$expectedSymbolPackages | ForEach-Object { Write-Host "  $_" }
