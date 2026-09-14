function ConvertTo-ToolkitVersion {
    param([Parameter(Mandatory)][string] $Version)

    $match = [regex]::Match($Version, '\A(?<prefix>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z')
    if (-not $match.Success) {
        throw "Invalid package version '$Version'. Use major.minor.patch with an optional prerelease suffix and no build metadata."
    }

    $prefix = $match.Groups['prefix'].Value
    $suffix = $match.Groups['suffix'].Value
    foreach ($identifier in $suffix.Split('.')) {
        if ($identifier -match '^0[0-9]+$') {
            throw "Invalid package version '$Version': numeric prerelease identifiers cannot have leading zeroes."
        }
    }

    $numbers = $null
    if (-not [version]::TryParse($prefix, [ref] $numbers)) {
        throw "Package version components are out of range: '$Version'."
    }

    [pscustomobject]@{
        Version = $Version
        Prefix = $prefix
        Suffix = $suffix
        Major = $numbers.Major
        Minor = $numbers.Minor
        Patch = $numbers.Build
    }
}

function Get-ToolkitVersion {
    param([xml] $PropertiesXml)

    if ($null -eq $PropertiesXml) {
        $repositoryRoot = Split-Path -Parent $PSScriptRoot
        $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
        $path = Join-Path $repositoryRoot $config.PackagePropsPath
        $PropertiesXml = [IO.File]::ReadAllText($path)
    }
    $prefix = $PropertiesXml.SelectNodes('/Project/PropertyGroup/VersionPrefix')
    $suffix = $PropertiesXml.SelectNodes('/Project/PropertyGroup/VersionSuffix')
    if ($prefix.Count -ne 1 -or $suffix.Count -ne 1) {
        throw 'Expected one VersionPrefix and one VersionSuffix in the package properties.'
    }

    $version = $prefix[0].InnerText.Trim()
    if ($suffix[0].InnerText.Trim()) {
        $version += '-' + $suffix[0].InnerText.Trim()
    }

    ConvertTo-ToolkitVersion $version
}

function Get-ToolkitReleaseInfo {
    param([Parameter(Mandatory)][string] $Version)

    $versionInfo = ConvertTo-ToolkitVersion $Version
    if ($versionInfo.Suffix -and $versionInfo.Suffix -cnotmatch '\Apreview\.(?:0|[1-9][0-9]*)\z') {
        throw "Release version '$Version' must be stable or use the exact lowercase preview.N suffix."
    }

    $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
    $branches = @($config.ReleaseBranch)
    if ($versionInfo.Suffix) { $branches += $config.DevelopmentBranch }
    foreach ($branch in $branches) {
        if ([string]::IsNullOrWhiteSpace($branch)) { throw 'The allowed release branches are not configured.' }
    }

    [pscustomobject]@{
        Version = $versionInfo.Version
        Tag = 'v' + $versionInfo.Version
        IsPrerelease = [bool] $versionInfo.Suffix
        Branches = $branches
    }
}

function Assert-ToolkitCommittedVersion {
    param(
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][string] $Expected
    )

    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Package.config.psd1')
    $propertiesRef = $Commit + ':' + $config.PackagePropsPath.Replace('\', '/')
    $committedText = (& git -C $repositoryRoot show $propertiesRef) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the committed package version.' }
    $committed = Get-ToolkitVersion -PropertiesXml ([xml] $committedText.TrimStart([char] 0xFEFF))
    if ($committed.Version -cne $Expected) {
        throw "The working version '$Expected' differs from the committed version '$($committed.Version)'."
    }
}

function Get-ToolkitVersionProperties {
    param([Parameter(Mandatory)][string] $Version)

    $info = ConvertTo-ToolkitVersion $Version
    "-p:Version=$($info.Version)"
    "-p:VersionPrefix=$($info.Prefix)"
    "-p:VersionSuffix=$($info.Suffix)"
}

function Test-ToolkitCiPublishAllowed {
    param([ValidateNotNullOrEmpty()][string] $Remote = 'origin')

    $version = Get-ToolkitVersion
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $PSNativeCommandUseErrorActionPreference = $false
    & git -C $repositoryRoot ls-remote --exit-code --refs --tags -- $Remote "refs/tags/v$($version.Prefix)" | Out-Null
    $exitCode = $LASTEXITCODE
    $global:LASTEXITCODE = 0
    switch ($exitCode) {
        0 { $false }
        2 { $true }
        default { throw "Could not check the stable release tag on '$Remote'." }
    }
}

Export-ModuleMember -Function ConvertTo-ToolkitVersion, Get-ToolkitVersion, Get-ToolkitReleaseInfo, Assert-ToolkitCommittedVersion, Get-ToolkitVersionProperties, Test-ToolkitCiPublishAllowed
