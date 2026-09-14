[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoPack
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "Versioning.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "Packaging.psm1") -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$feedPath = Join-Path $repositoryRoot $repositoryConfig.LocalFeedPath

if (-not $Version) {
    $Version = & (Join-Path $PSScriptRoot "get-version.ps1")
}

$Version = (ConvertTo-ToolkitVersion $Version).Version

if (-not $NoPack) {
    $packArguments = @{
        Configuration = $Configuration
        Version = $Version
    }

    & (Join-Path $PSScriptRoot "pack.ps1") @packArguments
}

$packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols)

New-Item -ItemType Directory -Path $feedPath -Force | Out-Null

foreach ($package in $packages) {
    Copy-Item -LiteralPath $package.Path -Destination $feedPath -Force
    Write-Host "Published: $(Split-Path -Leaf $package.Path)"
}

Write-Host "Local NuGet feed: $feedPath"
