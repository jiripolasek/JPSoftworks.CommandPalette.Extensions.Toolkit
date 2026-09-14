<#
.SYNOPSIS
Verifies a stable or preview.N release tag against HEAD, the version file, and allowed origin branches.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Tag)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $Tag.StartsWith('v', [StringComparison]::Ordinal)) {
    throw "Release tag '$Tag' must start with lowercase v."
}
$release = Get-ToolkitReleaseInfo -Version $Tag.Substring(1)
$version = Get-ToolkitVersion
if ($Tag -cne "v$($version.Version)") {
    throw "Release tag '$Tag' does not match the source version '$($version.Version)'."
}

$commit = & git -C $repositoryRoot rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve HEAD.' }
$tagCommit = & git -C $repositoryRoot rev-parse --verify "refs/tags/$Tag^{commit}"
if ($LASTEXITCODE -ne 0 -or $tagCommit -cne $commit) {
    throw "Release tag '$Tag' must point to HEAD."
}
Assert-ToolkitCommittedVersion -Commit $commit -Expected $version.Version

$allowedRefs = @($release.Branches | ForEach-Object { 'refs/remotes/origin/' + $_ })
$belongsToBranch = $false
foreach ($releaseRef in $allowedRefs) {
    & git -C $repositoryRoot show-ref --verify --quiet $releaseRef
    if ($LASTEXITCODE -eq 1) { continue }
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect '$releaseRef'." }
    & git -C $repositoryRoot merge-base --is-ancestor $commit $releaseRef
    if ($LASTEXITCODE -eq 0) {
        $belongsToBranch = $true
        break
    }
    if ($LASTEXITCODE -ne 1) { throw "Could not check release ancestry against '$releaseRef'." }
}
if (-not $belongsToBranch) {
    throw "Release commit must belong to '$($allowedRefs -join "' or '")'. Fetch the allowed branches and full history first."
}

$version.Version
