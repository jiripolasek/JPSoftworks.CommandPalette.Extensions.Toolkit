<#
.SYNOPSIS
Tests version changes and local release tags in an isolated repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\versioning-tests'))
$testRoot = Join-Path $testParent ([guid]::NewGuid().ToString('N'))
$testEng = Join-Path $testRoot 'eng'
New-Item -ItemType Directory -Path $testEng -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot 'src\Core') -Force | Out-Null
$passed = 0
$completed = $false

function Assert-Equal($Expected, $Actual) {
    if ($Actual -cne $Expected) {
        throw "Expected '$Expected', got '$Actual'."
    }
}

function Assert-Throws([scriptblock] $Action, [string] $Message) {
    try {
        & $Action
    } catch {
        if ($_.Exception.Message -notlike "*$Message*") {
            throw
        }
        return
    }
    throw "Expected a failure containing '$Message'."
}

function Invoke-Test([string] $Name, [scriptblock] $Action) {
    & $Action
    $script:passed++
    Write-Host "PASS $Name"
}

function Invoke-TestGit([string[]] $Arguments) {
    $output = & git -C $testRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git failed: $Arguments"
    }
    $output
}

function Assert-CiPolicyStep([bool] $Expected) {
    $stepPath = Join-Path $testRoot '.git\ci-policy.ps1'
    $step = @'
$ErrorActionPreference = 'stop'
Import-Module (Join-Path $PSScriptRoot '../eng/Versioning.psm1') -Force
$allowed = Test-ToolkitCiPublishAllowed
"allowed=$($allowed.ToString().ToLowerInvariant())"
if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }
'@
    [IO.File]::WriteAllText($stepPath, $step)
    $command = ". '" + $stepPath.Replace("'", "''") + "'"
    $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -Command $command
    Assert-Equal 0 $LASTEXITCODE
    Assert-Equal "allowed=$($Expected.ToString().ToLowerInvariant())" ($output -join "`n")
}

try {
    foreach ($name in 'Versioning.psm1', 'get-version.ps1', 'set-version.ps1', 'tag-release.ps1', 'verify-release.ps1') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $testEng
    }
    Import-Module (Join-Path $testEng 'Versioning.psm1') -Force
    $get = Join-Path $testEng 'get-version.ps1'
    $set = Join-Path $testEng 'set-version.ps1'
    $tag = Join-Path $testEng 'tag-release.ps1'
    $verify = Join-Path $testEng 'verify-release.ps1'
    $propsPath = Join-Path $testEng 'Package.props'
    $lockPath = Join-Path $testRoot 'src\Core\packages.lock.json'
    $utf8 = [Text.UTF8Encoding]::new($false)
    $props = "<Project>`r`n  <PropertyGroup>`r`n    <VersionPrefix>2.3.4</VersionPrefix>`r`n    <VersionSuffix>preview.1</VersionSuffix>`r`n  </PropertyGroup>`r`n</Project>`r`n"
    [IO.File]::WriteAllText($propsPath, $props, [Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText((Join-Path $testEng 'Package.config.psd1'), @'
@{
    PackagePropsPath = 'eng\Package.props'
    SolutionPath = 'Test.slnx'
    ReleaseBranch = 'master'
    DevelopmentBranch = 'next'
    PackageIds = @('Toolkit.Core', 'Toolkit.Logging')
}
'@, $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'Test.slnx'), '<Solution><Project Path="src/Core/Core.csproj" /></Solution>', $utf8)
    $lockText = @'
{
  "version": 2,
  "dependencies": {
    "net10.0": {
      "toolkit.core": {
        "type": "Project",
        "dependencies": {
          "Toolkit.Logging": "[2.3.4-preview.1, )",
          "External.Package": "[2.3.4-preview.1, )"
        }
      },
      "External.Package": {
        "type": "Direct",
        "requested": "[2.3.4-preview.1, )",
        "resolved": "2.3.4-preview.1",
        "contentHash": "unchanged"
      }
    }
  }
}
'@
    $lockText = $lockText.Replace("`r`n", "`n") + "`n"
    [IO.File]::WriteAllText($lockPath, $lockText, $utf8)

    Invoke-Test 'Read the current version and derive CI versions without edits' {
        Assert-Equal '2.3.4-preview.1' (& $get)
        Assert-Equal '2.3.4-preview.1.ci.147' (& $get -CiBuild 147)
        Assert-Equal $props ([IO.File]::ReadAllText($propsPath))
        Assert-Equal $lockText ([IO.File]::ReadAllText($lockPath))
        Assert-Throws { & $get -CiBuild '01' } 'CiBuild'
        Assert-Throws { & $get -CiBuild '' } 'CiBuild'
    }

    Invoke-Test 'CI versions sort after their preview and below the next preview or stable release' {
        $sdk = (& dotnet --list-sdks | Select-Object -Last 1)
        if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '\A(\S+) \[(.+)\]\z') {
            throw 'Could not locate the SDK NuGet version comparer.'
        }
        Add-Type -LiteralPath (Join-Path $Matches[2] "$($Matches[1])/NuGet.Versioning.dll")
        $ordered = @('2.3.4-preview.1', (& $get -CiBuild 9), (& $get -CiBuild 10), '2.3.4-preview.2', '2.3.4')
        for ($i = 1; $i -lt $ordered.Count; $i++) {
            $previous = [NuGet.Versioning.NuGetVersion]::Parse($ordered[$i - 1])
            $current = [NuGet.Versioning.NuGetVersion]::Parse($ordered[$i])
            if ($previous.CompareTo($current) -ge 0) {
                throw "Expected '$previous' to sort below '$current'."
            }
        }
    }

    Invoke-Test 'Build and pack version properties clear stable suffixes and preserve CI suffixes' {
        Assert-Equal '-p:Version=2.3.4|-p:VersionPrefix=2.3.4|-p:VersionSuffix=' ((Get-ToolkitVersionProperties '2.3.4') -join '|')
        Assert-Equal '-p:Version=2.3.4-preview.1.ci.147|-p:VersionPrefix=2.3.4|-p:VersionSuffix=preview.1.ci.147' ((Get-ToolkitVersionProperties '2.3.4-preview.1.ci.147') -join '|')
        Assert-Throws { Get-ToolkitVersionProperties '2.3.4;Injected=true' } 'Invalid package version'
    }

    Invoke-Test 'Reject invalid versions before editing any files' {
        foreach ($invalid in '1.2', 'v1.2.3', '01.2.3', '1.2.3-preview.01', '1.2.3+sha', '1.2.3-') {
            Assert-Throws { & $set -Version $invalid } 'version'
        }
        Assert-Equal $props ([IO.File]::ReadAllText($propsPath))
        Assert-Equal $lockText ([IO.File]::ReadAllText($lockPath))
    }

    Invoke-Test 'WhatIf and unchanged versions preserve the original bytes' {
        $before = [Convert]::ToBase64String([IO.File]::ReadAllBytes($propsPath))
        & $set -Increment Minor -WhatIf
        & $set -Version '2.3.4-preview.1'
        Assert-Equal $before ([Convert]::ToBase64String([IO.File]::ReadAllBytes($propsPath)))
        Assert-Equal $lockText ([IO.File]::ReadAllText($lockPath))
    }

    Invoke-Test 'Increment minor while preserving external dependencies and file formatting' {
        & $set -Increment Minor
        Assert-Equal '2.4.0-preview.1' (& $get)
        $expected = $lockText.Replace('"Toolkit.Logging": "[2.3.4-preview.1, )"', '"Toolkit.Logging": "[2.4.0-preview.1, )"')
        Assert-Equal $expected ([IO.File]::ReadAllText($lockPath))
        Assert-Equal $props.Replace('2.3.4', '2.4.0') ([IO.File]::ReadAllText($propsPath))
        Assert-Equal '239,187,191' (([IO.File]::ReadAllBytes($propsPath)[0..2]) -join ',')
        Assert-Equal 123 ([IO.File]::ReadAllBytes($lockPath)[0])
    }

    Invoke-Test 'Promote to stable and start subsequent patch and major versions' {
        & $set -Stable
        Assert-Equal '2.4.0' (& $get)
        Assert-Equal '2.4.0-ci.147' (& $get -CiBuild 147)
        & $set -Increment Patch -Prerelease 'beta.2'
        Assert-Equal '2.4.1-beta.2' (& $get)
        & $set -Increment Major
        Assert-Equal '3.0.0-preview.1' (& $get)
        & $set -Version '1.2.3'
        Assert-Equal '1.2.3' (& $get)
        Assert-Throws { & $set -Stable:$false } 'Specify -Stable'
    }

    Invoke-Test 'A stale internal lock dependency leaves the version file untouched' {
        $before = [IO.File]::ReadAllText($propsPath)
        $validLock = [IO.File]::ReadAllText($lockPath)
        $staleLock = $validLock.Replace('"Toolkit.Logging": "[1.2.3, )"', '"Toolkit.Logging": "[0.0.0, )"')
        [IO.File]::WriteAllText($lockPath, $staleLock, $utf8)
        Assert-Throws { & $set -Version '1.2.4' } 'Unexpected internal dependency'
        Assert-Equal $before ([IO.File]::ReadAllText($propsPath))
        Assert-Equal $staleLock ([IO.File]::ReadAllText($lockPath))
        [IO.File]::WriteAllText($lockPath, $validLock, $utf8)
    }

    Invoke-TestGit @('init', '--initial-branch=master') | Out-Null
    Invoke-TestGit @('config', 'user.name', 'Versioning tests')
    Invoke-TestGit @('config', 'user.email', 'version-tests@example.invalid')
    Invoke-TestGit @('config', 'commit.gpgSign', 'false')
    Invoke-TestGit @('config', 'tag.gpgSign', 'false')
    Invoke-TestGit @('config', 'core.fsmonitor', 'false')
    Invoke-TestGit @('config', 'core.autocrlf', 'false')
    Invoke-TestGit @('config', 'core.excludesFile', (Join-Path $testRoot '.git\info\exclude'))
    Invoke-TestGit @('config', 'core.hooksPath', (Join-Path $testRoot 'no-hooks'))
    Invoke-TestGit @('add', '.')
    Invoke-TestGit @('commit', '--message', 'Test release') | Out-Null

    Invoke-Test 'CI publishes an untagged stable version and fails if the remote cannot be queried' {
        Invoke-TestGit @('remote', 'add', 'origin', $testRoot)
        Assert-Equal $true (Test-ToolkitCiPublishAllowed)
        Assert-Throws { Test-ToolkitCiPublishAllowed -Remote (Join-Path $testRoot 'missing-remote') 2>$null } 'Could not check the stable release tag'
    }

    Invoke-Test 'An allowed CI policy exits successfully through the GitHub pwsh wrapper' {
        Assert-CiPolicyStep $true
    }

    Invoke-Test 'Tag WhatIf leaves Git refs unchanged' {
        & $tag -WhatIf
        Assert-Equal '' ((Invoke-TestGit @('tag', '--list')) -join '')
    }

    Invoke-Test 'Create one annotated tag at HEAD and reject duplicate tags' {
        & $tag
        Assert-Equal 'tag' (Invoke-TestGit @('cat-file', '-t', 'refs/tags/v1.2.3'))
        Assert-Equal (Invoke-TestGit @('rev-parse', 'HEAD')) (Invoke-TestGit @('rev-parse', 'v1.2.3^{}'))
        Assert-Throws { & $tag } 'already exists'
    }

    Invoke-Test 'CI does not publish a stable version whose tag exists on the remote' {
        Assert-Equal $false (Test-ToolkitCiPublishAllowed)
        Assert-CiPolicyStep $false
    }

    Invoke-Test 'Reject stable release tags from the development branch' {
        Invoke-TestGit @('checkout', '-b', 'next') | Out-Null
        Assert-Throws { & $tag } "Create release tags from 'master'"
        Invoke-TestGit @('checkout', 'master') | Out-Null
    }

    Invoke-Test 'Validate stable tags against the source version and release history' {
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/master', 'HEAD')
        Assert-Equal '1.2.3' (& $verify -Tag 'v1.2.3')
        Assert-Throws { & $verify -Tag 'v1.2.3-preview.1' } 'does not match the source version'
        Assert-Throws { & $verify -Tag 'v1.2.4' } 'does not match the source version'
        Invoke-TestGit @('checkout', '-b', 'unmerged-release') | Out-Null
        & $set -Version '1.2.4'
        Invoke-TestGit @('add', '.')
        Invoke-TestGit @('commit', '--message', 'Unmerged release') | Out-Null
        Invoke-TestGit @('tag', 'v1.2.4')
        Assert-Throws { & $verify -Tag 'v1.2.4' } 'must belong to'
        Invoke-TestGit @('checkout', 'master') | Out-Null
    }

    Invoke-Test 'Require the tagged checkout while allowing master to advance' {
        Invoke-TestGit @('commit', '--allow-empty', '--message', 'After release') | Out-Null
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/master', 'HEAD')
        Assert-Throws { & $verify -Tag 'v1.2.3' } 'must point to HEAD'
        Invoke-TestGit @('checkout', '--detach', 'v1.2.3') | Out-Null
        Assert-Equal '1.2.3' (& $verify -Tag 'v1.2.3')
        Invoke-TestGit @('checkout', 'master') | Out-Null
        Invoke-TestGit @('tag', '--delete', 'v1.2.4') | Out-Null
    }

    Invoke-Test 'Reject dirty checkouts and unsupported prerelease suffixes' {
        [IO.File]::AppendAllText($propsPath, "`r`n")
        Assert-Throws { & $tag } 'pending changes'
        & $set -Version '1.3.0-preview.1'
        Assert-Throws { & $tag } 'pending changes'
        & $set -Version '1.3.0-beta.1'
        Assert-Throws { & $tag } 'exact lowercase preview.N suffix'
        Assert-Equal 'v1.2.3' (Invoke-TestGit @('tag', '--list'))
    }

    Invoke-Test 'Reject an uncommitted version hidden from Git status' {
        Invoke-TestGit @('restore', '.')
        $originalBytes = [IO.File]::ReadAllBytes($propsPath)
        $tagged = $false
        try {
            Invoke-TestGit @('update-index', '--assume-unchanged', 'eng/Package.props')
            $changedProps = [IO.File]::ReadAllText($propsPath).Replace('1.2.3', '1.2.4')
            [IO.File]::WriteAllText($propsPath, $changedProps, [Text.UTF8Encoding]::new($true))
            Assert-Throws { & $tag } 'differs from the committed version'
            Invoke-TestGit @('tag', 'v1.2.4')
            $tagged = $true
            Assert-Throws { & $verify -Tag 'v1.2.4' } 'differs from the committed version'
        } finally {
            [IO.File]::WriteAllBytes($propsPath, $originalBytes)
            Invoke-TestGit @('update-index', '--no-assume-unchanged', 'eng/Package.props')
            if ($tagged) { Invoke-TestGit @('tag', '--delete', 'v1.2.4') | Out-Null }
        }
    }

    Invoke-Test 'Reject every prerelease tag shape except exact lowercase preview.N' {
        foreach ($suffix in 'ci.17', 'alpha.1', 'beta.1', 'rc.1', 'preview', 'Preview.1', 'preview.one', 'preview.1.2', 'preview.1.ci.17', 'preview.1-ci') {
            Assert-Throws { & $verify -Tag "v1.3.0-$suffix" } 'exact lowercase preview.N suffix'
        }
        foreach ($invalid in 'v1.3.0-preview.01', 'v1.3.0-preview.1+metadata', 'v01.3.0-preview.1', "v1.3.0-preview.1`n") {
            Assert-Throws { & $verify -Tag $invalid } 'version'
        }
        Assert-Throws { & $verify -Tag 'V1.3.0-preview.1' } 'lowercase v'
    }

    Invoke-Test 'Create and validate a preview tag belonging only to next' {
        Invoke-TestGit @('update-index', '--no-assume-unchanged', 'eng/Package.props')
        Invoke-TestGit @('restore', '.')
        Invoke-TestGit @('checkout', 'next') | Out-Null
        & $set -Version '1.3.0-preview.2'
        Invoke-TestGit @('add', '.')
        Invoke-TestGit @('commit', '--message', 'Next preview') | Out-Null
        & $tag -WhatIf
        Assert-Equal '' ((Invoke-TestGit @('tag', '--list', 'v1.3.0-preview.2')) -join '')
        & $tag
        Assert-Equal 'tag' (Invoke-TestGit @('cat-file', '-t', 'refs/tags/v1.3.0-preview.2'))
        Assert-Throws { & $verify -Tag 'v1.3.0-preview.2' } 'must belong to'
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/next', 'HEAD')
        Assert-Equal '1.3.0-preview.2' (& $verify -Tag 'v1.3.0-preview.2')
        Assert-Throws { & $tag } 'already exists'
        Invoke-TestGit @('update-ref', '-d', 'refs/remotes/origin/master')
        Assert-Equal '1.3.0-preview.2' (& $verify -Tag 'v1.3.0-preview.2')
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/master', 'master')
    }

    Invoke-Test 'CI continues publishing after a preview tag when the stable tag is absent' {
        Assert-Equal $true (Test-ToolkitCiPublishAllowed)
        Assert-CiPolicyStep $true
        Assert-Throws { Test-ToolkitCiPublishAllowed -Remote (Join-Path $testRoot 'missing-remote') 2>$null } 'Could not check the stable release tag'
    }

    Invoke-Test 'Reject preview tags outside both allowed branch histories' {
        Invoke-TestGit @('checkout', '-b', 'feature/preview-test') | Out-Null
        & $set -Version '1.3.0-preview.4'
        Invoke-TestGit @('add', '.')
        Invoke-TestGit @('commit', '--message', 'Unmerged preview') | Out-Null
        Assert-Throws { & $tag } "Create release tags from 'master' or 'next'"
        Invoke-TestGit @('tag', 'v1.3.0-preview.4')
        Assert-Throws { & $verify -Tag 'v1.3.0-preview.4' } 'must belong to'
        Invoke-TestGit @('checkout', 'next') | Out-Null
    }

    Invoke-Test 'Stable releases still require master even when next contains the commit' {
        & $set -Stable
        Invoke-TestGit @('add', '.')
        Invoke-TestGit @('commit', '--message', 'Stable version on next') | Out-Null
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/next', 'HEAD')
        Assert-Throws { & $tag } "Create release tags from 'master'"
        Invoke-TestGit @('tag', 'v1.3.0')
        Assert-Throws { & $verify -Tag 'v1.3.0' } 'must belong to'
    }

    Invoke-Test 'Create and validate a preview tag belonging only to master' {
        Invoke-TestGit @('checkout', 'master') | Out-Null
        & $set -Version '1.3.0-preview.3'
        Invoke-TestGit @('add', '.')
        Invoke-TestGit @('commit', '--message', 'Master preview') | Out-Null
        & $tag
        Invoke-TestGit @('update-ref', 'refs/remotes/origin/master', 'HEAD')
        Assert-Equal '1.3.0-preview.3' (& $verify -Tag 'v1.3.0-preview.3')
    }

    Invoke-Test 'CI stops publishing a prerelease source after its stable version is tagged' {
        Assert-Equal '1.3.0-preview.3' (& $get)
        Assert-Equal $false (Test-ToolkitCiPublishAllowed)
        Assert-CiPolicyStep $false
    }

    $completed = $true
    Write-Host "$passed versioning tests passed."
} finally {
    if ($completed) {
        $resolvedRoot = (Resolve-Path -LiteralPath $testRoot).Path
        if (-not $resolvedRoot.StartsWith($testParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected test directory '$resolvedRoot'."
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    } else {
        Write-Host "Test fixture retained at $testRoot"
    }
}
