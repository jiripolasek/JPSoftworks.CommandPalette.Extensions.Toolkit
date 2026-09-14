<#
.SYNOPSIS
Publishes prerelease packages to GitHub Packages using GITHUB_TOKEN.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = 'Release',
    [Parameter(Mandatory)][string] $Version,
    [string] $Username = $env:GITHUB_ACTOR,
    [switch] $NoPack,
    [switch] $Resume,
    [string] $ProgressPath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Packaging.psm1') -Force
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
$versionInfo = ConvertTo-ToolkitVersion $Version
if (-not $versionInfo.Suffix) { throw 'GitHub Packages publishing requires a prerelease version.' }
if ($versionInfo.Suffix -match '\Apreview\.(?:0|[1-9][0-9]*)\z') {
    throw "'$Version' is reserved for NuGet.org releases. Use a CI or local prerelease for GitHub Packages."
}
if ($Resume -and -not $NoPack) { throw 'Resuming requires -NoPack to reuse the original packages.' }
if ($ProgressPath -and -not $NoPack) { throw 'Upload progress requires -NoPack to reuse the original packages.' }
if ($Resume -and $ProgressPath) { throw 'Do not combine -Resume with -ProgressPath.' }
if ([string]::IsNullOrWhiteSpace($config.GitHubSource)) { throw 'GitHubSource is not configured.' }

if (-not $NoPack -and $WhatIfPreference) {
    [void] $PSCmdlet.ShouldProcess($Version, 'Build, pack, and publish packages')
    return
}

if (-not $NoPack) {
    & (Join-Path $PSScriptRoot 'pack.ps1') -Configuration $Configuration -Version $Version
}
$packages = @(Get-ToolkitPackages -Version $Version)
$progress = if ($ProgressPath) { Read-ToolkitPublishProgress -Version $Version -Target github -Packages $packages -Paths $ProgressPath }
$target = "$($config.GitHubSource) ($Version)"
if (-not $PSCmdlet.ShouldProcess($target, 'Publish prerelease packages')) { return }
if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) { throw 'Set GITHUB_TOKEN to a token with write:packages access.' }
Assert-ToolkitNuGetEnvironmentKeySupport

if (-not $Username) { $Username = $config.GitHubRepository.Split('/')[0] }
$source = [Security.SecurityElement]::Escape($config.GitHubSource)
$user = [Security.SecurityElement]::Escape($Username)
$nugetConfigPath = Join-Path ([IO.Path]::GetTempPath()) "toolkit-nuget-$([guid]::NewGuid().ToString('N')).config"
$previousKey = [Environment]::GetEnvironmentVariable('NUGET_API_KEY', 'Process')
try {
    $nugetConfig = @"
<configuration>
  <packageSources><clear /><add key="github" value="$source" /></packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="$user" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
      <add key="ValidAuthenticationTypes" value="Basic" />
    </github>
  </packageSourceCredentials>
</configuration>
"@
    [IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, [Text.UTF8Encoding]::new($false))
    $env:NUGET_API_KEY = $env:GITHUB_TOKEN
    foreach ($package in $packages) {
        $arguments = @('nuget', 'push', $package.Path, '--source', $config.GitHubSource, '--configfile', $nugetConfigPath, '--no-symbols', '--timeout', '600')
        $name = Split-Path -Leaf $package.Path
        if ($Resume -or ($progress -and $progress.PushedPackages.Contains($name))) { $arguments += '--skip-duplicate' }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub Packages publish failed for '$($package.Path)' with exit code $LASTEXITCODE. Check the client error and resolve any version conflict before retrying."
        }
        if ($progress) {
            [void] $progress.PushedPackages.Add($name)
            Save-ToolkitPublishProgress -Progress $progress -Path $ProgressPath
        }
    }
} finally {
    [Environment]::SetEnvironmentVariable('NUGET_API_KEY', $previousKey, 'Process')
    if (Test-Path -LiteralPath $nugetConfigPath) { Remove-Item -LiteralPath $nugetConfigPath -Force }
}
Write-Host "Published $($packages.Count) prerelease packages to $($config.GitHubSource)."
