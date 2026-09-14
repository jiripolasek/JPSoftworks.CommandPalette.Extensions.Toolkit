[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [string] $Version,

    [switch] $NoPack,

    [switch] $PromptForApiKey
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$packagePath = Join-Path $repositoryRoot $repositoryConfig.PackageOutputPath
$packagePropsPath = Join-Path $repositoryRoot $repositoryConfig.PackagePropsPath
$nugetSource = $repositoryConfig.NuGetSource

if (-not $Version) {
    [xml] $packageProps = Get-Content -Raw -LiteralPath $packagePropsPath
    $versionPrefix = [string](
        $packageProps.Project.PropertyGroup |
            ForEach-Object { $_.VersionPrefix } |
            Where-Object { $_ } |
            Select-Object -First 1)
    $versionSuffix = [string](
        $packageProps.Project.PropertyGroup |
            ForEach-Object { $_.VersionSuffix } |
            Where-Object { $_ } |
            Select-Object -First 1)
    $Version = if ($versionSuffix) {
        "$versionPrefix-$versionSuffix"
    } else {
        $versionPrefix
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Package version could not be resolved from '$packagePropsPath'."
}

if ([string]::IsNullOrWhiteSpace($nugetSource)) {
    throw "The NuGet publishing source is not configured."
}

if ($PromptForApiKey -or $env:NUGET_API_KEY -or $env:NUGET_SYMBOL_API_KEY) {
    $nugetVersionOutput = & dotnet nuget --version
    if ($LASTEXITCODE -ne 0) {
        throw "Could not determine the NuGet client version."
    }

    $nugetVersionMatch = [regex]::Match(($nugetVersionOutput -join "`n"), "(?m)^\s*(?:NuGet Command Line\s+)?(\d+\.\d+\.\d+)")
    if (-not $nugetVersionMatch.Success -or [version] $nugetVersionMatch.Groups[1].Value -lt [version] "7.6.0") {
        throw "Environment and prompted API keys require NuGet 7.6 or newer. Update the .NET SDK, or use a configured NuGet API key without these options."
    }
}

if (-not $NoPack) {
    $packArguments = @{
        Configuration = $Configuration
        Version = $Version
    }

    & (Join-Path $PSScriptRoot "pack.ps1") @packArguments
}

$packages = @(
    foreach ($packageId in $repositoryConfig.PackageIds) {
        foreach ($extension in ".nupkg", ".snupkg") {
            [pscustomobject]@{
                Id = $packageId
                Path = Join-Path $packagePath "$packageId.$Version$extension"
                IsSymbolPackage = $extension -eq ".snupkg"
            }
        }
    }
)
$missingOutputs = @(
    $packages |
        ForEach-Object { $_.Path } |
        Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
)
if ($missingOutputs.Count -ne 0) {
    throw "Expected package outputs were not found: $($missingOutputs -join ', ')."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.Path)
    try {
        $nuspecEntry = @($archive.Entries | Where-Object { $_.FullName -like "*.nuspec" })
        if ($nuspecEntry.Count -ne 1) {
            throw "Expected exactly one .nuspec in '$($package.Path)'."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }

        $packageId = [string] $nuspec.package.metadata.id
        $packageVersion = [string] $nuspec.package.metadata.version
        if ($packageId -ne $package.Id -or $packageVersion -ne $Version) {
            throw "Unexpected package identity '$packageId $packageVersion' in '$($package.Path)'. Expected '$($package.Id) $Version'."
        }
    } finally {
        $archive.Dispose()
    }
}

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
            "600",
            "--skip-duplicate"
        )

        if (-not $package.IsSymbolPackage) {
            $arguments += "--no-symbols"
        }

        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet.org publish failed for '$($package.Path)' with exit code $LASTEXITCODE. Re-run the script to resume; already published versions will be skipped."
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
