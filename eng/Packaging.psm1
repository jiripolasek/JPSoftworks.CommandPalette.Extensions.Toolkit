function Get-ToolkitPackages {
    param(
        [Parameter(Mandatory)][string] $Version,
        [switch] $IncludeSymbols,
        [string] $PackagePath
    )

    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
    if (-not $PackagePath) { $PackagePath = Join-Path $repositoryRoot $config.PackageOutputPath }
    $extensions = @('.nupkg')
    if ($IncludeSymbols) { $extensions += '.snupkg' }
    $packages = @(
        foreach ($packageId in $config.PackageIds) {
            foreach ($extension in $extensions) {
                [pscustomobject]@{
                    Id = $packageId
                    Path = Join-Path $packagePath "$packageId.$Version$extension"
                    IsSymbolPackage = $extension -eq '.snupkg'
                }
            }
        }
    )
    $missing = @($packages | Where-Object { -not (Test-Path -LiteralPath $_.Path -PathType Leaf) })
    if ($missing.Count -ne 0) {
        throw "Expected package outputs were not found: $($missing.Path -join ', ')."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($package in $packages) {
        $archive = [IO.Compression.ZipFile]::OpenRead($package.Path)
        try {
            $entries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
            if ($entries.Count -ne 1) {
                throw "Expected exactly one .nuspec in '$($package.Path)'."
            }
            $reader = [IO.StreamReader]::new($entries[0].Open())
            try {
                [xml] $nuspec = $reader.ReadToEnd()
            } finally {
                $reader.Dispose()
            }
            $id = [string] $nuspec.package.metadata.id
            $packageVersion = [string] $nuspec.package.metadata.version
            if ($id -cne $package.Id -or $packageVersion -cne $Version) {
                throw "Unexpected package identity '$id $packageVersion' in '$($package.Path)'. Expected '$($package.Id) $Version'."
            }
        } finally {
            $archive.Dispose()
        }
    }
    $packages
}

function Assert-ToolkitNuGetEnvironmentKeySupport {
    $output = & dotnet nuget --version
    if ($LASTEXITCODE -ne 0) { throw 'Could not determine the NuGet client version.' }
    $match = [regex]::Match(($output -join "`n"), '(?m)^\s*(?:NuGet Command Line\s+)?(\d+\.\d+\.\d+)')
    if (-not $match.Success -or [version] $match.Groups[1].Value -lt [version] '7.6.0') {
        throw 'Environment and prompted API keys require NuGet 7.6 or newer. Update the .NET SDK, or use a configured NuGet API key without these options.'
    }
}

function Get-ToolkitPublishManifest {
    param(
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][ValidateSet('github', 'nuget')][string] $Target,
        [Parameter(Mandatory)][object[]] $Packages
    )

    $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
    $source = if ($Target -eq 'nuget') { $config.NuGetSource } else { $config.GitHubSource }
    [ordered]@{
        Source = $source
        Version = $Version
        Packages = @(
            foreach ($package in $packages) {
                [ordered]@{
                    Name = Split-Path -Leaf $package.Path
                    SHA256 = (Get-FileHash -LiteralPath $package.Path -Algorithm SHA256).Hash
                }
            }
        )
    }
}

function Initialize-ToolkitPublishState {
    param(
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][ValidateSet('github', 'nuget')][string] $Target,
        [Parameter(Mandatory)][string] $StatePath,
        [switch] $RequireExisting
    )

    $StatePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($StatePath)
    $savedPath = Join-Path (Split-Path -Parent $StatePath) 'packages'
    $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
    $outputPath = Join-Path (Split-Path -Parent $PSScriptRoot) $config.PackageOutputPath
    if (Test-Path -LiteralPath $StatePath) {
        $packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols:($Target -eq 'nuget') -PackagePath $savedPath)
        $state = Get-ToolkitPublishManifest -Version $Version -Target $Target -Packages $packages | ConvertTo-Json -Depth 4 -Compress
        if ([IO.File]::ReadAllText($StatePath) -cne $state) {
            throw 'The publishing target or saved packages differ from the original upload snapshot.'
        }
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
        foreach ($package in $packages) {
            Copy-Item -LiteralPath $package.Path -Destination $outputPath -Force
        }
        return $true
    }

    if ($RequireExisting) { throw 'The original upload snapshot is required.' }
    $packages = @(Get-ToolkitPackages -Version $Version -IncludeSymbols:($Target -eq 'nuget'))
    New-Item -ItemType Directory -Path $savedPath -Force | Out-Null
    foreach ($package in $packages) {
        Copy-Item -LiteralPath $package.Path -Destination $savedPath -Force
        $package.Path = Join-Path $savedPath (Split-Path -Leaf $package.Path)
    }
    $state = Get-ToolkitPublishManifest -Version $Version -Target $Target -Packages $packages | ConvertTo-Json -Depth 4 -Compress
    [IO.File]::WriteAllText($StatePath, $state, [Text.UTF8Encoding]::new($false))
    $false
}

function Read-ToolkitPublishProgress {
    param(
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][ValidateSet('github', 'nuget')][string] $Target,
        [Parameter(Mandatory)][object[]] $Packages,
        [string[]] $Paths = @()
    )

    $manifest = Get-ToolkitPublishManifest -Version $Version -Target $Target -Packages $Packages
    $manifestJson = $manifest | ConvertTo-Json -Depth 4 -Compress
    $pushedPackages = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in $Paths) {
        $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
        $record = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        if (($record.Manifest | ConvertTo-Json -Depth 4 -Compress) -cne $manifestJson) {
            throw "Upload progress in '$path' does not match the publishing target and packages."
        }
        foreach ($name in $record.PushedPackages) {
            if ($manifest.Packages.Name -cnotcontains $name) { throw "Unknown package '$name' in upload progress." }
            [void] $pushedPackages.Add($name)
        }
    }
    [pscustomobject]@{ Manifest = $manifest; PushedPackages = $pushedPackages }
}

function Save-ToolkitPublishProgress {
    param(
        [Parameter(Mandatory)][object] $Progress,
        [Parameter(Mandatory)][string] $Path
    )

    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    $json = [ordered]@{
        Manifest = $Progress.Manifest
        PushedPackages = @($Progress.PushedPackages | Sort-Object)
    } | ConvertTo-Json -Depth 5 -Compress
    [IO.File]::WriteAllText("$Path.tmp", $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move("$Path.tmp", $Path, $true)
}

Export-ModuleMember -Function Get-ToolkitPackages, Assert-ToolkitNuGetEnvironmentKeySupport, Initialize-ToolkitPublishState, Read-ToolkitPublishProgress, Save-ToolkitPublishProgress
