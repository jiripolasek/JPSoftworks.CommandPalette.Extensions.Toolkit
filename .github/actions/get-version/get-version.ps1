param(
  [string] $GitHubEventName = $env:GITHUB_EVENT_NAME,
  [string] $GitHubRef       = $env:GITHUB_REF,
  [string] $InputsVersion   = $env:GITHUB_EVENT_INPUTS_VERSION
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

# Determine version based on event type
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

# Return version
Write-Host "Version: $version"
return $version