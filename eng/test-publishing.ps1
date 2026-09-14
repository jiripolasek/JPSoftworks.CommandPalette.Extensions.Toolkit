<#
.SYNOPSIS
Tests publisher validation and retries with fixture packages and a fake NuGet client.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\publishing-tests'))
$testRoot = Join-Path $testParent ([guid]::NewGuid().ToString('N'))
$testEng = Join-Path $testRoot 'eng'
New-Item -ItemType Directory -Path $testEng -Force | Out-Null
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
$packagePath = Join-Path $testRoot $config.PackageOutputPath
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
$version = '1.2.3-ci.17'
$previewVersion = '1.2.3-preview.2'
$passed = 0
$completed = $false
$previousDotnet = Get-Item Function:dotnet -ErrorAction SilentlyContinue
$previousGh = Get-Item Function:gh -ErrorAction SilentlyContinue
$previousEnvironment = @{}
foreach ($name in 'GITHUB_TOKEN', 'NUGET_API_KEY', 'NUGET_SYMBOL_API_KEY') {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$global:ToolkitPublishingTestState = @{
    Calls = [Collections.Generic.List[object]]::new()
    ClientVersion = '7.6.0'
    FailAt = 0
    AcceptThenFailAt = 0
    Duplicate = $false
    Feed = @{}
    GitHubCalls = [Collections.Generic.List[object]]::new()
    ReleaseExists = $false
    ReleaseIsDraft = $false
    FailUpload = $false
}

function global:dotnet {
    $global:LASTEXITCODE = 0
    if (($args -join ' ') -eq 'nuget --version') {
        "NuGet Command Line $($global:ToolkitPublishingTestState.ClientVersion)"
        return
    }
    if ($args[0] -ne 'nuget' -or $args[1] -ne 'push') { throw 'Unexpected fake client call.' }
    $configPath = $null
    $configText = $null
    $configIndex = [array]::IndexOf($args, '--configfile')
    if ($configIndex -ge 0) {
        $configPath = $args[$configIndex + 1]
        $configText = [IO.File]::ReadAllText($configPath)
    }
    $global:ToolkitPublishingTestState.Calls.Add([pscustomobject]@{
        Arguments = @($args)
        Key = $env:NUGET_API_KEY
        ConfigPath = $configPath
        ConfigText = $configText
    })
    if ($global:ToolkitPublishingTestState.Calls.Count -eq $global:ToolkitPublishingTestState.FailAt -or
        ($global:ToolkitPublishingTestState.Duplicate -and $args -notcontains '--skip-duplicate')) {
        $global:LASTEXITCODE = 1
        return
    }
    $source = $args[[array]::IndexOf($args, '--source') + 1]
    $key = "$source|$(Split-Path -Leaf $args[2])"
    if ($global:ToolkitPublishingTestState.Feed.ContainsKey($key)) {
        if ($args -notcontains '--skip-duplicate') { $global:LASTEXITCODE = 1 }
        return
    }
    $global:ToolkitPublishingTestState.Feed[$key] = (Get-FileHash -LiteralPath $args[2]).Hash
    if ($global:ToolkitPublishingTestState.Calls.Count -eq $global:ToolkitPublishingTestState.AcceptThenFailAt) {
        $global:LASTEXITCODE = 1
    }
}

function global:gh {
    $state = $global:ToolkitPublishingTestState
    $state.GitHubCalls.Add(@($args))
    $global:LASTEXITCODE = 0
    if ($args[0] -ne 'release') { throw 'Unexpected fake GitHub client call.' }
    switch ($args[1]) {
        'view' {
            if ($state.ReleaseExists) {
                @{ isDraft = $state.ReleaseIsDraft } | ConvertTo-Json -Compress
            } else { $global:LASTEXITCODE = 1 }
        }
        'create' {
            if ($state.ReleaseExists -or $args -notcontains '--draft' -or $args -notcontains '--verify-tag') {
                throw 'Expected creation of a draft for an existing tag.'
            }
            $state.ReleaseExists = $true
            $state.ReleaseIsDraft = $true
        }
        'upload' {
            if (-not $state.ReleaseIsDraft) { throw 'Cannot upload assets to a published release.' }
            if ($state.FailUpload) { $global:LASTEXITCODE = 1 }
        }
        'edit' {
            if ($args -notcontains '--draft=false') { throw 'Expected publication of a draft.' }
            $state.ReleaseIsDraft = $false
        }
        default { throw 'Unexpected fake GitHub release operation.' }
    }
}

function Assert-Equal($Expected, $Actual) {
    if ($Expected -cne $Actual) { throw "Expected '$Expected', got '$Actual'." }
}

function Assert-Throws([scriptblock] $Action, [string] $Message) {
    try { & $Action } catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw }
        return
    }
    throw "Expected a failure containing '$Message'."
}

function Invoke-Test([string] $Name, [scriptblock] $Action) {
    $global:ToolkitPublishingTestState.Calls.Clear()
    $global:ToolkitPublishingTestState.FailAt = 0
    $global:ToolkitPublishingTestState.AcceptThenFailAt = 0
    $global:ToolkitPublishingTestState.Duplicate = $false
    $global:ToolkitPublishingTestState.Feed.Clear()
    & $Action
    $script:passed++
    Write-Host "PASS $Name"
}

function Write-TestPackage([string] $Path, [string] $Id, [string] $PackageVersion, [switch] $DuplicateManifest, [DateTimeOffset] $Timestamp = '2026-01-01T00:00:00Z') {
    $stream = [IO.File]::Create($Path)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $names = @('package.nuspec')
        if ($DuplicateManifest) { $names += 'duplicate.nuspec' }
        foreach ($name in $names) {
            $entry = $archive.CreateEntry($name)
            $entry.LastWriteTime = $Timestamp
            $writer = [IO.StreamWriter]::new($entry.Open())
            try {
                $writer.Write("<package><metadata><id>$Id</id><version>$PackageVersion</version></metadata></package>")
            } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
}

function New-TestProgress([string] $Target, [string] $PackageVersion, [string] $Name) {
    $packages = @(Get-ToolkitPackages -Version $PackageVersion -IncludeSymbols:($Target -eq 'nuget'))
    $progress = Read-ToolkitPublishProgress -Version $PackageVersion -Target $Target -Packages $packages
    $path = Join-Path $testRoot "$Name\progress.json"
    Save-ToolkitPublishProgress -Progress $progress -Path $path
    $path
}

try {
    foreach ($name in 'Versioning.psm1', 'Packaging.psm1', 'prepare-publish.ps1', 'publish-github.ps1', 'publish-nuget.ps1', 'publish-release.ps1', 'publish-local.ps1', 'pack.ps1', 'Package.config.psd1') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $testEng
    }
    $github = Join-Path $testEng 'publish-github.ps1'
    $nuget = Join-Path $testEng 'publish-nuget.ps1'
    $release = Join-Path $testEng 'publish-release.ps1'
    $local = Join-Path $testEng 'publish-local.ps1'
    $prepare = Join-Path $testEng 'prepare-publish.ps1'
    $feedPath = Join-Path $testRoot $config.LocalFeedPath
    # The real version and ancestry guard is covered by test-versioning.ps1.
    [IO.File]::WriteAllText((Join-Path $testEng 'verify-release.ps1'), 'param([string] $Tag) $Tag.Substring(1)')
    foreach ($id in $config.PackageIds) {
        foreach ($extension in '.nupkg', '.snupkg') {
            Write-TestPackage (Join-Path $packagePath "$id.$version$extension") $id $version
            Write-TestPackage (Join-Path $packagePath "$id.$previewVersion$extension") $id $previewVersion
            Write-TestPackage (Join-Path $packagePath "$id.1.2.3$extension") $id '1.2.3'
        }
    }
    $lastId = $config.PackageIds[-1]
    $lastPackage = Join-Path $packagePath "$lastId.$version.nupkg"
    $lastSymbols = Join-Path $packagePath "$lastId.$previewVersion.snupkg"
    Import-Module (Join-Path $testEng 'Packaging.psm1') -Force
    $env:NUGET_API_KEY = 'original-test-key'
    $env:NUGET_SYMBOL_API_KEY = 'original-test-symbol-key'
    $env:GITHUB_TOKEN = 'github-test-token'

    Invoke-Test 'WhatIf performs validation without publishing' {
        & $github -Version $version -NoPack -WhatIf
        & $nuget -Version $previewVersion -NoPack -WhatIf
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'WhatIf without NoPack leaves packages unchanged and does not build' {
        $before = @(Get-ChildItem -LiteralPath $packagePath -File | Get-FileHash | ForEach-Object { $_.Path + ':' + $_.Hash }) -join "`n"
        & $github -Version '2.0.0-preview.1.ci.18' -WhatIf
        & $nuget -Version '2.0.0-preview.1' -WhatIf
        $after = @(Get-ChildItem -LiteralPath $packagePath -File | Get-FileHash | ForEach-Object { $_.Path + ':' + $_.Hash }) -join "`n"
        Assert-Equal $before $after
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'A corrupt snapshot cannot replace the current packages' {
        $statePath = Join-Path $testRoot 'corrupt-snapshot\state.json'
        Assert-Equal $false (Initialize-ToolkitPublishState -Version $previewVersion -Target nuget -StatePath $statePath)
        $savedSymbol = Join-Path (Split-Path -Parent $statePath) "packages\$lastId.$previewVersion.snupkg"
        $symbolBytes = [IO.File]::ReadAllBytes($savedSymbol)
        $before = (Get-FileHash -LiteralPath $lastSymbols).Hash
        try {
            [IO.File]::WriteAllBytes($savedSymbol, [byte[]]($symbolBytes + 0))
            Assert-Throws { Initialize-ToolkitPublishState -Version $previewVersion -Target nuget -StatePath $statePath } 'differ from the original upload snapshot'
            Assert-Equal $before (Get-FileHash -LiteralPath $lastSymbols).Hash
        } finally {
            [IO.File]::WriteAllBytes($savedSymbol, $symbolBytes)
        }
        Assert-Throws { Initialize-ToolkitPublishState -Version $previewVersion -Target github -StatePath $statePath } 'differ from the original upload snapshot'
        Assert-Throws { Initialize-ToolkitPublishState -Version $version -Target nuget -StatePath $statePath } 'outputs were not found'
        $missing = Join-Path $testRoot 'missing-snapshot\state.json'
        Assert-Throws { Initialize-ToolkitPublishState -Version $previewVersion -Target nuget -StatePath $missing -RequireExisting } 'snapshot is required'
        Assert-Equal $false (Test-Path -LiteralPath $missing)
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'A new NuGet snapshot requires every package and symbol archive' {
        $symbolBytes = [IO.File]::ReadAllBytes($lastSymbols)
        try {
            Remove-Item -LiteralPath $lastSymbols
            $newStatePath = Join-Path $testRoot 'missing-symbols\state.json'
            Assert-Throws { Initialize-ToolkitPublishState -Version $previewVersion -Target nuget -StatePath $newStatePath } 'outputs were not found'
            Assert-Equal $false (Test-Path -LiteralPath $newStatePath)
        } finally {
            [IO.File]::WriteAllBytes($lastSymbols, $symbolBytes)
        }
    }

    Invoke-Test 'Local publishing validates every package before copying anything' {
        Write-TestPackage $lastPackage 'Wrong.Id' $version
        Assert-Throws { & $local -Version $version -NoPack } 'Unexpected package identity'
        Assert-Equal $false (Test-Path -LiteralPath $feedPath)
        Write-TestPackage $lastPackage $lastId $version
        Remove-Item -LiteralPath $lastSymbols
        Assert-Throws { & $local -Version $previewVersion -NoPack } 'outputs were not found'
        Assert-Equal $false (Test-Path -LiteralPath $feedPath)
        Write-TestPackage $lastSymbols $lastId $previewVersion
    }

    Invoke-Test 'Local publishing copies validated packages and symbols unchanged' {
        & $local -Version $previewVersion -NoPack
        Assert-Equal 8 @(Get-ChildItem -LiteralPath $feedPath -File).Count
        foreach ($copy in Get-ChildItem -LiteralPath $feedPath -File) {
            Assert-Equal (Get-FileHash -LiteralPath $copy.FullName).Hash (Get-FileHash -LiteralPath (Join-Path $packagePath $copy.Name)).Hash
        }
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'Reject stable GitHub packages and missing credentials' {
        Assert-Throws { & $github -Version '1.2.3' -NoPack -Confirm:$false } 'requires a prerelease'
        $env:GITHUB_TOKEN = ''
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'Set GITHUB_TOKEN'
        $env:GITHUB_TOKEN = 'github-test-token'
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'GitHub requires an explicit version and reserves release identities for NuGet' {
        $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -File $github -NoPack -WhatIf 2>&1
        if ($LASTEXITCODE -eq 0 -or ($output -join "`n") -notmatch '(?s)missing mandatory parameters.*Version') {
            throw 'Expected GitHub publishing to require an explicit version.'
        }
        foreach ($reserved in '1.2.3-preview.2', '1.2.3-Preview.2', '1.2.3-PREVIEW.2') {
            Assert-Throws { & $github -Version $reserved -WhatIf } 'reserved for NuGet.org'
        }
        & $github -Version '1.2.3-preview.local.1' -WhatIf
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'Resume requires the original package outputs' {
        Assert-Throws { & $github -Version $version -Resume -WhatIf } 'requires -NoPack'
        Assert-Throws { & $nuget -Version $previewVersion -Resume -WhatIf } 'requires -NoPack'
        Assert-Throws { & $github -Version $version -ProgressPath progress.json -WhatIf } 'requires -NoPack'
        Assert-Throws { & $nuget -Version $previewVersion -ProgressPath progress.json -WhatIf } 'requires -NoPack'
        Assert-Throws { & $github -Version $version -NoPack -Resume -ProgressPath progress.json -WhatIf } 'Do not combine'
        Assert-Throws { & $nuget -Version $previewVersion -NoPack -Resume -ProgressPath progress.json -WhatIf } 'Do not combine'
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'A duplicate version fails unless explicitly resuming existing packages' {
        $global:ToolkitPublishingTestState.Duplicate = $true
        foreach ($target in @(@{ Script = $github; Version = $version; Count = 4 }, @{ Script = $nuget; Version = $previewVersion; Count = 8 })) {
            $global:ToolkitPublishingTestState.Calls.Clear()
            Assert-Throws { & $target.Script -Version $target.Version -NoPack -Confirm:$false } 'publish failed'
            Assert-Equal 1 $global:ToolkitPublishingTestState.Calls.Count
            Assert-Equal $false ($global:ToolkitPublishingTestState.Calls[0].Arguments -contains '--skip-duplicate')
            $global:ToolkitPublishingTestState.Calls.Clear()
            & $target.Script -Version $target.Version -NoPack -Resume -Confirm:$false
            Assert-Equal $target.Count $global:ToolkitPublishingTestState.Calls.Count
            foreach ($call in $global:ToolkitPublishingTestState.Calls) {
                Assert-Equal $true ($call.Arguments -contains '--skip-duplicate')
            }
        }
    }

    Invoke-Test 'NuGet rejects all other prereleases before packing or calling the client' {
        foreach ($suffix in 'ci.17', 'alpha.1', 'beta.1', 'rc.1', 'preview', 'Preview.1', 'preview.one', 'preview.1.2', 'preview.1.ci.17', 'preview.1-ci', 'preview.01', 'preview.1+metadata') {
            Assert-Throws { & $nuget -Version "1.2.3-$suffix" -Confirm:$false } 'version'
        }
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'Validate the entire package set before the first upload' {
        Remove-Item -LiteralPath $lastPackage
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'outputs were not found'
        Write-TestPackage $lastPackage 'Wrong.Id' $version
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'Unexpected package identity'
        Write-TestPackage $lastPackage $lastId '1.2.3-ci.18'
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'Unexpected package identity'
        Write-TestPackage $lastPackage $lastId $version -DuplicateManifest
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'exactly one .nuspec'
        Write-TestPackage $lastPackage $lastId $version
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'NuGet requires symbols but GitHub does not' {
        Remove-Item -LiteralPath $lastSymbols
        Assert-Throws { & $nuget -Version $previewVersion -NoPack -Confirm:$false } 'outputs were not found'
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
        $ciSymbols = Join-Path $packagePath "$lastId.$version.snupkg"
        Remove-Item -LiteralPath $ciSymbols
        & $github -Version $version -NoPack -Confirm:$false
        Assert-Equal 4 $global:ToolkitPublishingTestState.Calls.Count
        Write-TestPackage $ciSymbols $lastId $version
        Write-TestPackage $lastSymbols $lastId $previewVersion
    }

    Invoke-Test 'Reject old clients before uploading or replacing a key' {
        $global:ToolkitPublishingTestState.ClientVersion = '7.5.0'
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'require NuGet 7.6'
        Assert-Throws { & $nuget -Version $previewVersion -NoPack -Confirm:$false } 'require NuGet 7.6'
        Assert-Equal 0 $global:ToolkitPublishingTestState.Calls.Count
        Assert-Equal 'original-test-key' $env:NUGET_API_KEY
        $global:ToolkitPublishingTestState.ClientVersion = '7.6.0'
    }

    Invoke-Test 'GitHub uploads only packages and restores the NuGet key' {
        & $github -Version $version -NoPack -Confirm:$false
        Assert-Equal 4 $global:ToolkitPublishingTestState.Calls.Count
        foreach ($call in $global:ToolkitPublishingTestState.Calls) {
            Assert-Equal 'github-test-token' $call.Key
            Assert-Equal $false ($call.Arguments -contains '--skip-duplicate')
            Assert-Equal $true ($call.Arguments -contains '--no-symbols')
            Assert-Equal $true ($call.Arguments -contains $config.GitHubSource)
            Assert-Equal $false ($call.Arguments -contains 'github-test-token')
            Assert-Equal $true ($call.ConfigText.Contains('%GITHUB_TOKEN%'))
            Assert-Equal $false ($call.ConfigText.Contains('github-test-token'))
            Assert-Equal $false (Test-Path -LiteralPath $call.ConfigPath)
        }
        Assert-Equal 'original-test-key' $env:NUGET_API_KEY
        Assert-Equal 'original-test-symbol-key' $env:NUGET_SYMBOL_API_KEY
    }

    Invoke-Test 'Interrupted uploads stop and can resume the same package set' {
        $global:ToolkitPublishingTestState.FailAt = 2
        Assert-Throws { & $github -Version $version -NoPack -Confirm:$false } 'publish failed'
        Assert-Equal 2 $global:ToolkitPublishingTestState.Calls.Count
        Assert-Equal 'original-test-key' $env:NUGET_API_KEY
        $global:ToolkitPublishingTestState.Calls.Clear()
        $global:ToolkitPublishingTestState.FailAt = 0
        & $github -Version $version -NoPack -Resume -Confirm:$false
        Assert-Equal 4 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'NuGet uploads all packages and symbols separately without ignoring conflicts' {
        & $nuget -Version $previewVersion -NoPack -Confirm:$false
        Assert-Equal 8 $global:ToolkitPublishingTestState.Calls.Count
        foreach ($call in $global:ToolkitPublishingTestState.Calls) {
            Assert-Equal $false ($call.Arguments -contains '--skip-duplicate')
            Assert-Equal $true ($call.Arguments -contains $config.NuGetSource)
            $isPackage = [string] $call.Arguments[2] -like '*.nupkg'
            Assert-Equal $isPackage ($call.Arguments -contains '--no-symbols')
        }
    }

    Invoke-Test 'GitHub release WhatIf does not call the GitHub client' {
        & $release -Tag 'v1.2.3' -WhatIf
        Assert-Equal 0 $global:ToolkitPublishingTestState.GitHubCalls.Count
    }

    Invoke-Test 'An interrupted GitHub release stays draft until all assets are uploaded' {
        $state = $global:ToolkitPublishingTestState
        $state.FailUpload = $true
        Assert-Throws { & $release -Tag 'v1.2.3' -Confirm:$false } 'Re-run to resume the draft'
        Assert-Equal 'view,create,upload' (($state.GitHubCalls | ForEach-Object { $_[1] }) -join ',')
        Assert-Equal $true ($state.GitHubCalls[1] -contains '--prerelease=false')
        Assert-Equal $false ($state.GitHubCalls[1] -contains '--latest=false')
        Assert-Equal $true $state.ReleaseIsDraft
        $state.GitHubCalls.Clear()
        $state.FailUpload = $false
        & $release -Tag 'v1.2.3' -Confirm:$false
        Assert-Equal 'view,upload,edit' (($state.GitHubCalls | ForEach-Object { $_[1] }) -join ',')
        Assert-Equal $true ($state.GitHubCalls[2] -contains '--prerelease=false')
        $assets = @($state.GitHubCalls[1] | Where-Object { $_ -like '*.nupkg' -or $_ -like '*.snupkg' })
        Assert-Equal 8 $assets.Count
        Assert-Equal $false $state.ReleaseIsDraft
    }

    Invoke-Test 'Retrying a published GitHub release leaves it unchanged' {
        $state = $global:ToolkitPublishingTestState
        $state.GitHubCalls.Clear()
        & $release -Tag 'v1.2.3' -Confirm:$false
        Assert-Equal 1 $state.GitHubCalls.Count
        Assert-Equal 'view' $state.GitHubCalls[0][1]
    }

    Invoke-Test 'NuGet still publishes stable versions' {
        & $nuget -Version '1.2.3' -NoPack -Confirm:$false
        Assert-Equal 8 $global:ToolkitPublishingTestState.Calls.Count
    }

    Invoke-Test 'Preview GitHub releases are marked prerelease and never latest, including retries' {
        $state = $global:ToolkitPublishingTestState
        $state.GitHubCalls.Clear()
        $state.ReleaseExists = $false
        $state.FailUpload = $true
        Assert-Throws { & $release -Tag "v$previewVersion" -Confirm:$false } 'Re-run to resume the draft'
        $create = $state.GitHubCalls[1]
        Assert-Equal $true ($create -contains '--prerelease=true')
        Assert-Equal $true ($create -contains '--latest=false')
        $state.GitHubCalls.Clear()
        $state.FailUpload = $false
        & $release -Tag "v$previewVersion" -Confirm:$false
        $edit = $state.GitHubCalls[2]
        Assert-Equal $true ($edit -contains '--prerelease=true')
        Assert-Equal $true ($edit -contains '--latest=false')
        $assets = @($state.GitHubCalls[1] | Where-Object { $_ -like '*.nupkg' -or $_ -like '*.snupkg' })
        Assert-Equal 8 $assets.Count
        foreach ($asset in $assets) { Assert-Equal $true ($asset.Contains(".$previewVersion.")) }
    }

    foreach ($target in @(
        @{ Name = 'github'; Script = $github; Version = $version; Source = $config.GitHubSource; Count = 4; FailAt = 3 },
        @{ Name = 'nuget'; Script = $nuget; Version = $previewVersion; Source = $config.NuGetSource; Count = 8; FailAt = 4 }
    )) {
        Invoke-Test "$($target.Name): a first-attempt conflict remains an error on every retry" {
            $client = $global:ToolkitPublishingTestState
            $statePath = Join-Path $testRoot "$($target.Name)-conflict\state.json"
            $progressPath = Join-Path $testRoot "$($target.Name)-conflict-progress\progress.json"
            $history = [Collections.Generic.List[string]]::new()
            $packages = @(Get-ToolkitPackages -Version $target.Version -IncludeSymbols:($target.Name -eq 'nuget'))
            $key = "$($target.Source)|$(Split-Path -Leaf $packages[0].Path)"
            $client.Feed[$key] = 'previously-published-bytes'
            foreach ($attempt in 1..3) {
                Assert-Equal ($attempt -gt 1) (Initialize-ToolkitPublishState -Version $target.Version -Target $target.Name -StatePath $statePath)
                $progress = Read-ToolkitPublishProgress -Version $target.Version -Target $target.Name -Packages $packages -Paths $history.ToArray()
                Assert-Equal 0 $progress.PushedPackages.Count
                Assert-Equal $target.Count $progress.Manifest.Packages.Count
                Save-ToolkitPublishProgress -Progress $progress -Path $progressPath
                $client.Calls.Clear()
                Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'version conflict'
                Assert-Equal 1 $client.Calls.Count
                Assert-Equal $false ($client.Calls[0].Arguments -contains '--skip-duplicate')
                $historyPath = Join-Path $testRoot "$($target.Name)-conflict-progress-$attempt.json"
                Copy-Item -LiteralPath $progressPath -Destination $historyPath
                $history.Add($historyPath)
            }
            Assert-Equal 'previously-published-bytes' $client.Feed[$key]
            Assert-Equal 1 $client.Feed.Count
        }

        Invoke-Test "$($target.Name): workflow preparation restores flat and nested progress artifacts" {
            Push-Location $testRoot
            try {
                $client = $global:ToolkitPublishingTestState
                $initial = & $prepare -Version $target.Version -Target $target.Name
                Assert-Equal $false $initial.Restored
                $client.FailAt = 2
                Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $initial.ProgressPath -Confirm:$false } 'publish failed'
                $historyPath = "artifacts/upload-$($target.Name)-history"
                New-Item -ItemType Directory -Path $historyPath | Out-Null
                $flatPath = Join-Path $historyPath 'progress.json'
                Copy-Item -LiteralPath $initial.ProgressPath -Destination $flatPath
                $packages = @(Get-ToolkitPackages -Version $target.Version -IncludeSymbols:($target.Name -eq 'nuget'))
                $firstHash = (Get-FileHash -LiteralPath $packages[0].Path).Hash
                foreach ($package in $packages) {
                    Write-TestPackage $package.Path $package.Id $target.Version -Timestamp '2026-05-01T00:00:00Z'
                }
                $second = & $prepare -Version $target.Version -Target $target.Name
                Assert-Equal $true $second.Restored
                Assert-Equal $firstHash (Get-FileHash -LiteralPath $packages[0].Path).Hash
                $record = [IO.File]::ReadAllText((Join-Path $testRoot $second.ProgressPath)) | ConvertFrom-Json
                Assert-Equal 1 $record.PushedPackages.Count
                $client.Calls.Clear()
                $client.FailAt = 3
                Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $second.ProgressPath -Confirm:$false } 'publish failed'
                foreach ($attempt in 1..2) {
                    $directory = Join-Path $historyPath "upload-$($target.Name)-progress-$attempt"
                    New-Item -ItemType Directory -Path $directory | Out-Null
                    $source = if ($attempt -eq 1) { $flatPath } else { $second.ProgressPath }
                    Copy-Item -LiteralPath $source -Destination (Join-Path $directory 'progress.json')
                }
                Remove-Item -LiteralPath $flatPath
                $third = & $prepare -Version $target.Version -Target $target.Name
                Assert-Equal $true $third.Restored
                $record = [IO.File]::ReadAllText((Join-Path $testRoot $third.ProgressPath)) | ConvertFrom-Json
                Assert-Equal 2 $record.PushedPackages.Count
                $client.Calls.Clear()
                $client.FailAt = 0
                & $target.Script -Version $target.Version -NoPack -ProgressPath $third.ProgressPath -Confirm:$false
                Assert-Equal $target.Count $client.Feed.Count
            } finally { Pop-Location }
        }

        Invoke-Test "$($target.Name): retries restore original bytes and merge only confirmed successes" {
            $client = $global:ToolkitPublishingTestState
            $statePath = Join-Path $testRoot "$($target.Name)-partial\state.json"
            Assert-Equal $false (Initialize-ToolkitPublishState -Version $target.Version -Target $target.Name -StatePath $statePath)
            $snapshot = [IO.File]::ReadAllText($statePath)
            $manifest = $snapshot | ConvertFrom-Json
            $progressPath = New-TestProgress $target.Name $target.Version "$($target.Name)-partial-progress"
            $packages = @(Get-ToolkitPackages -Version $target.Version -IncludeSymbols:($target.Name -eq 'nuget'))
            $client.FailAt = $target.FailAt
            Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'publish failed'
            $confirmed = $target.FailAt - 1
            Assert-Equal $confirmed $client.Feed.Count
            $history = @((Join-Path $testRoot "$($target.Name)-partial-progress-1.json"))
            Copy-Item -LiteralPath $progressPath -Destination $history[0]

            foreach ($attempt in 2..3) {
                foreach ($package in $packages) {
                    Write-TestPackage $package.Path $package.Id $target.Version -Timestamp ([DateTimeOffset]::Parse('2026-02-01T00:00:00Z').AddDays($attempt))
                }
                Assert-Equal $false ($manifest.Packages[0].SHA256 -ceq (Get-FileHash -LiteralPath $packages[0].Path).Hash)
                if ($attempt -eq 3) { Remove-Item -LiteralPath $packages[-1].Path }
                Assert-Equal $true (Initialize-ToolkitPublishState -Version $target.Version -Target $target.Name -StatePath $statePath)
                Assert-Equal $snapshot ([IO.File]::ReadAllText($statePath))
                $progress = Read-ToolkitPublishProgress -Version $target.Version -Target $target.Name -Packages $packages -Paths @($history + $history[0])
                Assert-Equal $confirmed $progress.PushedPackages.Count
                Save-ToolkitPublishProgress -Progress $progress -Path $progressPath
                $client.Calls.Clear()
                $client.FailAt = if ($attempt -eq 2) { $confirmed + 2 } else { 0 }
                if ($attempt -eq 2) {
                    Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'publish failed'
                } else {
                    & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false
                }
                for ($index = 0; $index -lt $client.Calls.Count; $index++) {
                    Assert-Equal ($index -lt $confirmed) ($client.Calls[$index].Arguments -contains '--skip-duplicate')
                }
                $confirmed = if ($attempt -eq 2) { $confirmed + 1 } else { $target.Count }
                $progress = Read-ToolkitPublishProgress -Version $target.Version -Target $target.Name -Packages $packages -Paths $progressPath
                Assert-Equal $confirmed $progress.PushedPackages.Count
                if ($attempt -eq 2) {
                    $history += Join-Path $testRoot "$($target.Name)-partial-progress-2.json"
                    Copy-Item -LiteralPath $progressPath -Destination $history[-1]
                }
            }
            Assert-Equal $target.Count $client.Feed.Count
            foreach ($package in $manifest.Packages) {
                Assert-Equal $package.SHA256 $client.Feed["$($target.Source)|$($package.Name)"]
            }

            if ($target.Name -eq 'nuget') {
                foreach ($package in $packages) {
                    Write-TestPackage $package.Path $package.Id $target.Version -Timestamp '2026-03-01T00:00:00Z'
                }
                Initialize-ToolkitPublishState -Version $target.Version -Target nuget -StatePath $statePath -RequireExisting | Out-Null
                $client.GitHubCalls.Clear()
                $client.ReleaseExists = $false
                & $release -Tag "v$($target.Version)" -Confirm:$false
                $upload = @($client.GitHubCalls | Where-Object { $_[1] -eq 'upload' })
                $assets = @($upload[0] | Where-Object { $_ -like '*.nupkg' -or $_ -like '*.snupkg' })
                Assert-Equal 8 $assets.Count
                foreach ($asset in $assets) {
                    Assert-Equal $client.Feed["$($target.Source)|$(Split-Path -Leaf $asset)"] (Get-FileHash -LiteralPath $asset).Hash
                }
            }
        }

        Invoke-Test "$($target.Name): an upload without client confirmation cannot bypass a later conflict" {
            $client = $global:ToolkitPublishingTestState
            $progressPath = New-TestProgress $target.Name $target.Version "$($target.Name)-unconfirmed-progress"
            $client.AcceptThenFailAt = 1
            Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'publish failed'
            Assert-Equal 1 $client.Feed.Count
            $record = [IO.File]::ReadAllText($progressPath) | ConvertFrom-Json
            Assert-Equal 0 $record.PushedPackages.Count
            $client.AcceptThenFailAt = 0
            $client.Calls.Clear()
            Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'version conflict'
            Assert-Equal 1 $client.Calls.Count
            Assert-Equal $false ($client.Calls[0].Arguments -contains '--skip-duplicate')
        }

        Invoke-Test "$($target.Name): progress validates package bytes, version, feed, and file names before publishing" {
            $client = $global:ToolkitPublishingTestState
            $progressPath = New-TestProgress $target.Name $target.Version "$($target.Name)-validation-progress"
            $before = [IO.File]::ReadAllText($progressPath)
            & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -WhatIf
            Assert-Equal $before ([IO.File]::ReadAllText($progressPath))
            foreach ($field in 'Source', 'Version') {
                $record = $before | ConvertFrom-Json
                $record.Manifest.$field = 'different-target'
                [IO.File]::WriteAllText($progressPath, ($record | ConvertTo-Json -Depth 5 -Compress))
                Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'does not match'
            }
            $record = $before | ConvertFrom-Json
            $record.PushedPackages = @('Unknown.Package.nupkg')
            [IO.File]::WriteAllText($progressPath, ($record | ConvertTo-Json -Depth 5 -Compress))
            Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'Unknown package'
            [IO.File]::WriteAllText($progressPath, $before)
            $packages = @(Get-ToolkitPackages -Version $target.Version -IncludeSymbols:($target.Name -eq 'nuget'))
            $last = $packages[-1]
            $bytes = [IO.File]::ReadAllBytes($last.Path)
            try {
                Write-TestPackage $last.Path $last.Id $target.Version -Timestamp '2026-04-01T00:00:00Z'
                Assert-Throws { & $target.Script -Version $target.Version -NoPack -ProgressPath $progressPath -Confirm:$false } 'does not match'
            } finally {
                [IO.File]::WriteAllBytes($last.Path, $bytes)
            }
            Assert-Equal 0 $client.Calls.Count
        }
    }

    $completed = $true
    Write-Host "$passed publishing tests passed."
} finally {
    if ($previousDotnet) {
        Set-Item Function:global:dotnet $previousDotnet.ScriptBlock
    } else {
        Remove-Item Function:dotnet
    }
    if ($previousGh) {
        Set-Item Function:global:gh $previousGh.ScriptBlock
    } else {
        Remove-Item Function:gh
    }
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    Remove-Variable ToolkitPublishingTestState -Scope Global
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
