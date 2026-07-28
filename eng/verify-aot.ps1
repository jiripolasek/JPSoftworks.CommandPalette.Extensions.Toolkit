[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Configuration = "Release",

    [ValidateSet(
        "net9.0-windows10.0.22621.0",
        "net10.0-windows10.0.22621.0")]
    [string[]] $Frameworks = @(
        "net9.0-windows10.0.22621.0",
        "net10.0-windows10.0.22621.0"),

    [ValidateSet("win-x64", "win-arm64")]
    [string[]] $RuntimeIdentifiers = @("win-x64", "win-arm64"),

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryConfig = Import-PowerShellDataFile (Join-Path $PSScriptRoot "Package.config.psd1")
$projectPath = Join-Path $repositoryRoot $repositoryConfig.AotProjectPath
$projectName = $repositoryConfig.AotProjectName

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $runtimeArtifactsPath = Join-Path $repositoryRoot "artifacts\aot\$runtimeIdentifier"
    $artifactsProperty = "-p:ArtifactsPath=$runtimeArtifactsPath\"

    if (-not $NoRestore) {
        & dotnet restore $projectPath `
            --runtime $runtimeIdentifier `
            -p:UseArtifactsOutput=true `
            $artifactsProperty `
            -p:IsAotVerificationBuild=true `
            -p:PublishAot=true `
            -p:RestorePackagesWithLockFile=true `
            -p:RestoreLockedMode=false

        if ($LASTEXITCODE -ne 0) {
            throw "Native AOT restore for '$runtimeIdentifier' failed with exit code $LASTEXITCODE."
        }
    }

    foreach ($framework in $Frameworks) {
        & dotnet publish $projectPath `
            --configuration $Configuration `
            --framework $framework `
            --runtime $runtimeIdentifier `
            --self-contained true `
            --no-restore `
            -p:UseArtifactsOutput=true `
            $artifactsProperty `
            -p:IsAotVerificationBuild=true `
            -p:GeneratePackageOnBuild=false `
            -p:PublishAot=true `
            -p:PublishTrimmed=true `
            -p:PublishSingleFile=false `
            -p:TrimmerSingleWarn=false `
            -warnaserror

        if ($LASTEXITCODE -ne 0) {
            throw "Native AOT publish for '$framework/$runtimeIdentifier' failed with exit code $LASTEXITCODE."
        }

        $publishPath = Join-Path `
            $runtimeArtifactsPath `
            "publish\$projectName\$Configuration\$framework\$runtimeIdentifier"
        $executablePath = Join-Path $publishPath "$projectName.exe"
        if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
            throw "Native AOT publish for '$framework/$runtimeIdentifier' did not produce '$executablePath'."
        }

        $managedRuntimeFiles = @(
            "coreclr.dll",
            "hostfxr.dll",
            "$projectName.deps.json"
        )
        $unexpectedRuntimeFiles = @(
            $managedRuntimeFiles |
                ForEach-Object { Join-Path $publishPath $_ } |
                Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
        )
        if ($unexpectedRuntimeFiles.Count -ne 0) {
            throw "Native AOT publish for '$framework/$runtimeIdentifier' contains managed runtime files: $($unexpectedRuntimeFiles -join ', ')."
        }
    }
}

Write-Host "Native AOT verification succeeded:"
foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    foreach ($framework in $Frameworks) {
        Write-Host "  $framework / $runtimeIdentifier"
    }
}
