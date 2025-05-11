param(
  [string] $GitHubEventName = $env:GITHUB_EVENT_NAME,
  [string] $GitHubRef       = $env:GITHUB_REF,
  [string] $InputsVersion   = $env:GITHUB_EVENT_INPUTS_VERSION
)


function Get-LastTag {
    $tag = & git describe --tags --abbrev=0 2>$null

    if ($LASTEXITCODE -ne 0 -or -not $tag) {
        $tag = '0.0.0'
    }

    $global:LASTEXITCODE = 0
    return $tag -replace '^v', ''
}

function Get-CommitCount {
    param([string]$fromTag)

    if ($fromTag -eq '0.0.0') {
        $count = & git rev-list HEAD --count
    } else {
        $count = & git rev-list "v$fromTag..HEAD" --count
    }

    if ($LASTEXITCODE -ne 0) {
        $count = 0
    }

    $global:LASTEXITCODE = 0
    return $count
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