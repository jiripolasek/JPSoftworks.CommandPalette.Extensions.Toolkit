[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoPack
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$packagePath = Join-Path $repositoryRoot $repositoryConfig.PackageOutputPath
$feedPath = Join-Path $repositoryRoot $repositoryConfig.LocalFeedPath
$packagePropsPath = Join-Path $repositoryRoot $repositoryConfig.PackagePropsPath

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

if (-not $NoPack) {
    $packArguments = @{
        Configuration = $Configuration
        Version = $Version
    }

    & (Join-Path $PSScriptRoot "pack.ps1") @packArguments
}

$packages = @(
    $repositoryConfig.PackageIds |
        ForEach-Object { Join-Path $packagePath "$_.$Version.nupkg" }
)
$symbolPackages = @(
    $repositoryConfig.PackageIds |
        ForEach-Object { Join-Path $packagePath "$_.$Version.snupkg" }
)
$missingOutputs = @(
    $packages + $symbolPackages |
        Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
)
if ($missingOutputs.Count -ne 0) {
    throw "Expected package outputs were not found: $($missingOutputs -join ', ')."
}

New-Item -ItemType Directory -Path $feedPath -Force | Out-Null

foreach ($package in $packages) {
    Copy-Item -LiteralPath $package -Destination $feedPath -Force
    Write-Host "Published package: $(Split-Path -Leaf $package)"
}

foreach ($symbolPackage in $symbolPackages) {
    Copy-Item -LiteralPath $symbolPackage -Destination $feedPath -Force
    Write-Host "Published symbols: $(Split-Path -Leaf $symbolPackage)"
}

Write-Host "Local NuGet feed: $feedPath"
