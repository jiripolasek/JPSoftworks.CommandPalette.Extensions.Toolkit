[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoBuild,

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "Versioning.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "Packaging.psm1") -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$solutionPath = Join-Path $repositoryRoot $repositoryConfig.SolutionPath
$packagePath = Join-Path $repositoryRoot $repositoryConfig.PackageOutputPath

if (-not $Version) {
    $Version = & (Join-Path $PSScriptRoot "get-version.ps1")
}

$versionProperties = @(Get-ToolkitVersionProperties -Version $Version)

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
    "--no-restore"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$arguments += $versionProperties
$arguments += $commonProperties

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Local pack failed with exit code $LASTEXITCODE."
}

$packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols)
$expectedPackages = @($packages | Where-Object { -not $_.IsSymbolPackage } | ForEach-Object { $_.Path })
$expectedSymbolPackages = @($packages | Where-Object { $_.IsSymbolPackage } | ForEach-Object { $_.Path })

if ($NoBuild) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $inspectionPath = Join-Path $packagePath (([guid]::NewGuid().ToString('N')) + '.dll')
    try {
        foreach ($package in $expectedPackages) {
            $archive = [IO.Compression.ZipFile]::OpenRead($package)
            try {
                foreach ($entry in $archive.Entries) {
                    if ($entry.FullName -notlike 'lib/*.dll') {
                        continue
                    }
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $inspectionPath, $true)
                    $builtVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($inspectionPath).ProductVersion
                    if ([string]::IsNullOrWhiteSpace($builtVersion) -or $builtVersion.Split('+')[0] -cne $Version) {
                        throw "Assembly '$($entry.FullName)' was built as '$builtVersion', but the package version is '$Version'. Run pack.ps1 without -NoBuild."
                    }
                }
            } finally {
                $archive.Dispose()
            }
        }
    } catch {
        $expectedPackages + $expectedSymbolPackages | ForEach-Object { Remove-Item -LiteralPath $_ -Force }
        throw
    } finally {
        if (Test-Path -LiteralPath $inspectionPath) {
            Remove-Item -LiteralPath $inspectionPath -Force
        }
    }
}

Write-Host "Packages:"
$expectedPackages | ForEach-Object { Write-Host "  $_" }

Write-Host "Symbol packages:"
$expectedSymbolPackages | ForEach-Object { Write-Host "  $_" }
