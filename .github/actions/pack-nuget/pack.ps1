param(
  [string] $ProjectPath,
  [string] $GitHubEventName  = $env:GITHUB_EVENT_NAME,
  [string] $GitHubRef        = $env:GITHUB_REF,
  [string] $InputsVersion    = $env:GITHUB_EVENT_INPUTS_VERSION,
  [string] $Workspace        = $env:GITHUB_WORKSPACE,
  [string] $Configuration    = 'Release'
)

function Get-LastTag {
    try {
        $tag = git describe --tags --abbrev=0 2>$null
        return if ($tag) { $tag -replace '^v', '' } else { '0.0.0' }
    } catch {
        return '0.0.0'
    }
}

function Get-CommitCount {
    param([string]$fromTag)
    
    try {
        if ($fromTag -eq '0.0.0') {
            return git rev-list HEAD --count
        } else {
            return git rev-list "v$fromTag..HEAD" --count
        }
    } catch {
        return 0
    }
}

# Determine version: manual > tag-driven > feature-preview
switch ($GitHubEventName) {
    'workflow_dispatch' {
        $version = if ($InputsVersion) { $InputsVersion } else { Get-LastTag }
    }
    default {
        if ($GitHubRef -match '^refs/heads/feature/(.+)$') {
            # Feature branch - generate preview version
            $safeBranch = $matches[1] -replace '/', '-'
            $lastTag = Get-LastTag
            $commitCount = Get-CommitCount $lastTag
            $version = "$lastTag-ci-$safeBranch.$commitCount"
        } else {
            # Standard branch - use tag
            $version = Get-LastTag
        }
    }
}

Write-Host "Using version $version"

# Build and pack
$artifactsFolder = Join-Path $Workspace "artifacts"
msbuild $ProjectPath `
    /t:Pack /p:Configuration=$Configuration /p:NoBuild=true /p:NoRestore=true `
    /p:PackageVersion=$version /p:PackageOutputPath=$artifactsFolder

# Verify package creation
$nupkg = Get-ChildItem "$artifactsFolder/*.nupkg" -ErrorAction SilentlyContinue
if (-not $nupkg) {
    Write-Error "NuGet package was not created. Check msbuild logs for Pack errors."
    exit 1
}

# Output results
Write-Host "Package created: $($nupkg.FullName)"
"nupkg-path=$($nupkg.FullName)" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"version=$version" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
