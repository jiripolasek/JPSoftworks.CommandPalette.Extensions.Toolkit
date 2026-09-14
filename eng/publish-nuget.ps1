[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoPack,

    [switch] $Resume,

    [string] $ProgressPath,

    [switch] $PromptForApiKey
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "Versioning.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "Packaging.psm1") -Force

$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$nugetSource = $repositoryConfig.NuGetSource

if (-not $Version) {
    $Version = & (Join-Path $PSScriptRoot "get-version.ps1")
}

$Version = (Get-ToolkitReleaseInfo -Version $Version).Version
if ($Resume -and -not $NoPack) { throw 'Resuming requires -NoPack to reuse the original packages.' }
if ($ProgressPath -and -not $NoPack) { throw 'Upload progress requires -NoPack to reuse the original packages.' }
if ($Resume -and $ProgressPath) { throw 'Do not combine -Resume with -ProgressPath.' }

if ([string]::IsNullOrWhiteSpace($nugetSource)) {
    throw "The NuGet publishing source is not configured."
}

if ($PromptForApiKey -or $env:NUGET_API_KEY -or $env:NUGET_SYMBOL_API_KEY) {
    Assert-ToolkitNuGetEnvironmentKeySupport
}

if (-not $NoPack -and $WhatIfPreference) {
    [void] $PSCmdlet.ShouldProcess($Version, 'Build, pack, and publish packages')
    return
}

if (-not $NoPack) {
    $packArguments = @{
        Configuration = $Configuration
        Version = $Version
    }

    & (Join-Path $PSScriptRoot "pack.ps1") @packArguments
}

$packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols)
$progress = if ($ProgressPath) { Read-ToolkitPublishProgress -Version $Version -Target nuget -Packages $packages -Paths $ProgressPath }

$packageIdentities = @(
    $repositoryConfig.PackageIds |
        ForEach-Object { "$_ $Version" }
)

Write-Host "Packages to publish to $nugetSource (each with matching symbols):"
$packageIdentities | ForEach-Object { Write-Host "  $_" }

$publishTarget = $packageIdentities -join ", "
if (-not $PSCmdlet.ShouldProcess($publishTarget, "Publish to $nugetSource")) {
    return
}

$restoreApiKeys = $false
$previousApiKey = $null
$previousSymbolApiKey = $null
$secureApiKey = $null

try {
    if ($PromptForApiKey) {
        $secureApiKey = Read-Host "NuGet.org API key" -AsSecureString
        $plainTextApiKey = [System.Net.NetworkCredential]::new("", $secureApiKey).Password
        if ([string]::IsNullOrWhiteSpace($plainTextApiKey)) {
            throw "A NuGet.org API key was not provided."
        }

        $previousApiKey = [Environment]::GetEnvironmentVariable("NUGET_API_KEY", "Process")
        $previousSymbolApiKey = [Environment]::GetEnvironmentVariable("NUGET_SYMBOL_API_KEY", "Process")
        $restoreApiKeys = $true
        $env:NUGET_API_KEY = $plainTextApiKey
        $env:NUGET_SYMBOL_API_KEY = $plainTextApiKey
        $plainTextApiKey = $null
    }

    foreach ($package in $packages) {
        $arguments = @(
            "nuget",
            "push",
            $package.Path,
            "--source",
            $nugetSource,
            "--timeout",
            "600"
        )

        $name = Split-Path -Leaf $package.Path
        if ($Resume -or ($progress -and $progress.PushedPackages.Contains($name))) { $arguments += "--skip-duplicate" }
        if (-not $package.IsSymbolPackage) {
            $arguments += "--no-symbols"
        }

        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet.org publish failed for '$($package.Path)' with exit code $LASTEXITCODE. Check the client error and resolve any version conflict before retrying."
        }
        if ($progress) {
            [void] $progress.PushedPackages.Add($name)
            Save-ToolkitPublishProgress -Progress $progress -Path $ProgressPath
        }
    }

    Write-Host "Published packages to ${nugetSource}:"
    $packageIdentities | ForEach-Object { Write-Host "  $_" }
} finally {
    $plainTextApiKey = $null
    if ($null -ne $secureApiKey) {
        $secureApiKey.Dispose()
    }

    if ($restoreApiKeys) {
        if ($null -eq $previousApiKey) {
            Remove-Item Env:NUGET_API_KEY -ErrorAction SilentlyContinue
        } else {
            $env:NUGET_API_KEY = $previousApiKey
        }

        if ($null -eq $previousSymbolApiKey) {
            Remove-Item Env:NUGET_SYMBOL_API_KEY -ErrorAction SilentlyContinue
        } else {
            $env:NUGET_SYMBOL_API_KEY = $previousSymbolApiKey
        }
    }
}
