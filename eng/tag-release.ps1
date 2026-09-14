<#
.SYNOPSIS
Creates a local annotated tag for a committed stable or preview.N package version.
.DESCRIPTION
Requires an allowed branch and a clean checkout. Does not push or publish.
#>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
$version = Get-ToolkitVersion
$release = Get-ToolkitReleaseInfo -Version $version.Version

$branch = & git -C $repositoryRoot branch --show-current
if ($LASTEXITCODE -ne 0 -or $release.Branches -cnotcontains $branch) {
    throw "Create release tags from '$($release.Branches -join "' or '")'; current branch is '$branch'."
}

$status = & git -C $repositoryRoot status --porcelain --untracked-files=normal
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the working tree.'
}
if ($status) {
    throw 'Commit or stash pending changes before creating a release tag.'
}

& git -C $repositoryRoot ls-files --error-unmatch -- $config.PackagePropsPath | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'The package version file must be tracked by Git.'
}
$commit = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve the release commit.'
}
Assert-ToolkitCommittedVersion -Commit $commit -Expected $version.Version

$tag = $release.Tag
& git -C $repositoryRoot show-ref --verify --quiet "refs/tags/$tag"
if ($LASTEXITCODE -eq 0) {
    throw "Tag '$tag' already exists."
}
if ($LASTEXITCODE -ne 1) {
    throw "Could not check whether '$tag' exists."
}

if ($PSCmdlet.ShouldProcess($commit, "Create local release tag $tag")) {
    & git -C $repositoryRoot tag --annotate $tag --message "Release $($version.Version)" $commit
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create release tag '$tag'."
    }
    Write-Host "Created $tag at $commit. Push this tag when ready to release."
}
