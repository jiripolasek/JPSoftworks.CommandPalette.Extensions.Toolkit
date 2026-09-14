<#
.SYNOPSIS
Sets the shared version and updates internal project dependencies in lock files.
.DESCRIPTION
Increment starts the next development version with preview.1 unless Prerelease is specified.
Stable removes the current prerelease suffix. No packages are restored or published.
#>
[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Explicit')]
param(
    [Parameter(Mandatory, Position = 0, ParameterSetName = 'Explicit')]
    [string] $Version,

    [Parameter(Mandatory, ParameterSetName = 'Increment')]
    [ValidateSet('Patch', 'Minor', 'Major')]
    [string] $Increment,

    [Parameter(ParameterSetName = 'Increment')]
    [string] $Prerelease = 'preview.1',

    [Parameter(Mandatory, ParameterSetName = 'Stable')]
    [switch] $Stable
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
$current = Get-ToolkitVersion
switch ($PSCmdlet.ParameterSetName) {
    'Increment' {
        $Version = switch ($Increment) {
            'Patch' { '{0}.{1}.{2}' -f $current.Major, $current.Minor, ($current.Patch + 1) }
            'Minor' { '{0}.{1}.0' -f $current.Major, ($current.Minor + 1) }
            'Major' { '{0}.0.0' -f ($current.Major + 1) }
        }
        if ($Prerelease) {
            $Version += '-' + $Prerelease
        }
    }
    'Stable' {
        if (-not $Stable) {
            throw 'Specify -Stable to remove the prerelease suffix.'
        }
        $Version = $current.Prefix
    }
}

$next = ConvertTo-ToolkitVersion $Version
if ($next.Version -ceq $current.Version) {
    Write-Host "Version is already $Version."
    return
}

$changes = [Collections.Generic.List[object]]::new()
$propertiesPath = Join-Path $repositoryRoot $config.PackagePropsPath
$propertiesText = [IO.File]::ReadAllText($propertiesPath)
foreach ($property in @(
    @{ Name = 'VersionPrefix'; Value = $next.Prefix },
    @{ Name = 'VersionSuffix'; Value = $next.Suffix }
)) {
    $pattern = '<' + $property.Name + '(?:\s*/>|>[^<]*</' + $property.Name + '>)'
    $regex = [regex]::new($pattern)
    if ($regex.Matches($propertiesText).Count -ne 1) {
        throw "Expected one editable $($property.Name) element in '$propertiesPath'."
    }
    $replacement = '<' + $property.Name + '>' + $property.Value + '</' + $property.Name + '>'
    $propertiesText = $regex.Replace($propertiesText, $replacement)
}
$changes.Add(@{ Path = $propertiesPath; Text = $propertiesText })

[xml] $solution = [IO.File]::ReadAllText((Join-Path $repositoryRoot $config.SolutionPath))
foreach ($project in $solution.SelectNodes('//Project[@Path]')) {
    $projectPath = Join-Path $repositoryRoot $project.GetAttribute('Path')
    $lockPath = Join-Path (Split-Path -Parent $projectPath) 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $lockPath)) {
        continue
    }

    $lockText = [IO.File]::ReadAllText($lockPath)
    $lock = ConvertFrom-Json $lockText
    $references = @{}
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($dependency in $framework.Value.PSObject.Properties) {
            if ($dependency.Value.type -ne 'Project') {
                continue
            }
            foreach ($reference in $dependency.Value.dependencies.PSObject.Properties) {
                if ($config.PackageIds -notcontains $reference.Name) {
                    continue
                }
                if ($reference.Value -cne "[$($current.Version), )") {
                    throw "Unexpected internal dependency '$($reference.Name): $($reference.Value)' in '$lockPath'. Refresh the lock file before changing versions."
                }
                $references[$reference.Name] = 1 + $references[$reference.Name]
            }
        }
    }

    foreach ($id in $references.Keys) {
        $pattern = '("' + [regex]::Escape($id) + '"\s*:\s*")' + [regex]::Escape("[$($current.Version), )") + '(")'
        $regex = [regex]::new($pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($regex.Matches($lockText).Count -ne $references[$id]) {
            throw "Cannot update '$id' without changing unrelated entries in '$lockPath'."
        }
        $lockText = $regex.Replace($lockText, {
            param($match)
            $match.Groups[1].Value + "[$($next.Version), )" + $match.Groups[2].Value
        })
    }
    if ($references.Count -ne 0) {
        $changes.Add(@{ Path = $lockPath; Text = $lockText })
    }
}

foreach ($change in $changes) {
    $change.Bytes = [IO.File]::ReadAllBytes($change.Path)
    $hasBom = $change.Bytes.Length -ge 3 -and $change.Bytes[0] -eq 239 -and $change.Bytes[1] -eq 187 -and $change.Bytes[2] -eq 191
    $change.Encoding = [Text.UTF8Encoding]::new($hasBom)
}

if (-not $PSCmdlet.ShouldProcess($repositoryRoot, "Set version $($current.Version) -> $Version and update $($changes.Count - 1) lock files")) {
    return
}

$written = [Collections.Generic.List[object]]::new()
try {
    foreach ($change in $changes) {
        $written.Add($change)
        [IO.File]::WriteAllText($change.Path, $change.Text, $change.Encoding)
    }
} catch {
    foreach ($change in $written) {
        [IO.File]::WriteAllBytes($change.Path, $change.Bytes)
    }
    throw
}

Write-Host "Version: $($current.Version) -> $Version"
Write-Host "Updated $($changes.Count - 1) lock files; external dependencies are unchanged."
