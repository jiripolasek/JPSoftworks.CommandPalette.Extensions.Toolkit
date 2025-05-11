param(
  [string] $GitHubEventName  = $env:GITHUB_EVENT_NAME,
  [string] $GitHubRef        = $env:GITHUB_REF,
  [string] $InputsVersion    = $env:GITHUB_EVENT_INPUTS_VERSION,
  [string] $Workspace        = $env:GITHUB_WORKSPACE
)

# Determine version: manual > tag-driven > feature-preview

if ($GitHubEventName -eq 'workflow_dispatch' -and $InputsVersion) {
    $version = $InputsVersion
}
elseif ($GitHubEventName -eq 'pull_request' -or $GitHubRef.StartsWith('refs/heads/feature/')) {
    $branchName = $GitHubRef -replace '^refs/heads/', ''
    $trimmedBranch = $branchName -replace '^[^/]+/',''
    $safeBranch = $trimmedBranch -replace '/','-'

    try {
        $lastTagRaw = git describe --tags --abbrev=0 2>$null
        if (-not $lastTagRaw) {
            $lastTag = "0.0.0"
            $commitsSince = (git rev-list HEAD --count)
        } else {
            $lastTag = $lastTagRaw -replace '^v', ''
            $commitsSince = (git rev-list "$lastTagRaw..HEAD" --count)
        }
    } catch {
        $lastTag = "0.0.0"
        $commitsSince = (git rev-list HEAD --count)
    }

    $version = "$lastTag-ci-$safeBranch.$commitsSince"
}
else {
    try {
        $lastTagRaw = git describe --tags --abbrev=0 2>$null        
        if (-not $lastTagRaw) {
            $lastTagRaw = "0.0.0"
        }
    }
    catch {
        Write-Host "No tags found. Using default version 0.0.0"
        $lastTagRaw = "0.0.0"
    }
    $version = $lastTagRaw -replace '^v', ''
}

Write-Host "Using version $version"

$artifactsFolder = Join-Path $Workspace "artifacts"
msbuild src/JPSoftworks.CommandPalette.Extensions.Toolkit/JPSoftworks.CommandPalette.Extensions.Toolkit.csproj `
    /t:Pack /p:Configuration=Release /p:NoBuild=true /p:NoRestore=true `
    /p:PackageVersion=$version /p:PackageOutputPath=$artifactsFolder

Write-Host "Checking contents of $artifactsFolder"
Get-ChildItem -Path $artifactsFolder -Recurse | Out-String | Write-Host

if (!(Test-Path "$artifactsFolder/*.nupkg")) {
    Write-Error "NuGet package was not created. Check msbuild logs for Pack errors."
    exit 1
}

$nupkgPath = Get-ChildItem "$artifactsFolder/*.nupkg" | Select-Object -ExpandProperty FullName
"nupkg-path=$nupkgPath" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"version=$version"     | Out-File -Append -FilePath $env:GITHUB_OUTPUT
