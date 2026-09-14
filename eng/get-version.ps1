<#
.SYNOPSIS
Reads the shared package version, or derives a CI version without editing files.
#>
[CmdletBinding()]
param(
    [ValidatePattern('\A(?:0|[1-9][0-9]*)\z')]
    [string] $CiBuild
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

$version = Get-ToolkitVersion
if ($PSBoundParameters.ContainsKey('CiBuild')) {
    if ($version.Suffix) {
        "$($version.Version).ci.$CiBuild"
    } else {
        "$($version.Prefix)-ci.$CiBuild"
    }
} else {
    $version.Version
}
