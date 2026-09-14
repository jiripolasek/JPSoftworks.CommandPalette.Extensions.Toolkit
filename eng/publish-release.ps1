<#
.SYNOPSIS
Attaches validated packages to a GitHub release and publishes its generated notes.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param([Parameter(Mandatory)][string] $Tag)

$ErrorActionPreference = 'Stop'
$version = & (Join-Path $PSScriptRoot 'verify-release.ps1') -Tag $Tag
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Packaging.psm1') -Force
$release = Get-ToolkitReleaseInfo -Version $version
$releaseFlags = @('--prerelease=' + $release.IsPrerelease.ToString().ToLowerInvariant())
if ($release.IsPrerelease) { $releaseFlags += '--latest=false' }
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
$packages = @(Get-ToolkitPackages -Version $version -IncludeSymbols)
if (-not $PSCmdlet.ShouldProcess("$($config.GitHubRepository) $Tag", 'Publish GitHub release with packages and symbols')) { return }

$releaseJson = & gh release view $Tag --repo $config.GitHubRepository --json isDraft
if ($LASTEXITCODE -ne 0) {
    & gh release create $Tag --repo $config.GitHubRepository --verify-tag --draft --generate-notes --title $Tag @releaseFlags
    if ($LASTEXITCODE -ne 0) { throw "Could not create the draft release '$Tag'." }
} elseif (-not ($releaseJson | ConvertFrom-Json).isDraft) {
    Write-Host "GitHub release '$Tag' is already published."
    return
}

$paths = @($packages.Path)
& gh release upload $Tag @paths --repo $config.GitHubRepository --clobber
if ($LASTEXITCODE -ne 0) { throw "Could not upload release assets. Re-run to resume the draft release '$Tag'." }
& gh release edit $Tag --repo $config.GitHubRepository --draft=false @releaseFlags
if ($LASTEXITCODE -ne 0) { throw "Could not publish the draft release '$Tag'." }
