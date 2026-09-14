[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][ValidateSet('github', 'nuget')][string] $Target
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Packaging.psm1') -Force
$restored = Initialize-ToolkitPublishState -Version $Version -Target $Target -StatePath "artifacts/upload-$Target/state.json"
& (Join-Path $PSScriptRoot "publish-$Target.ps1") -Version $Version -NoPack -WhatIf
$historyPath = "artifacts/upload-$Target-history"
$paths = @(
    if (Test-Path -LiteralPath $historyPath) {
        Get-ChildItem -LiteralPath $historyPath -Filter progress.json -File -Recurse | Select-Object -ExpandProperty FullName
    }
)
$packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols:($Target -eq 'nuget'))
$progress = Read-ToolkitPublishProgress -Version $Version -Target $Target -Packages $packages -Paths $paths
$progressPath = "artifacts/upload-$Target-progress/progress.json"
Save-ToolkitPublishProgress -Progress $progress -Path $progressPath
[pscustomobject]@{ Restored = $restored; ProgressPath = $progressPath }
