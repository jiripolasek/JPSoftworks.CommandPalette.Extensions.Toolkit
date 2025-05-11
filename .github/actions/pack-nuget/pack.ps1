param(
  [string] $ProjectPath,
  [string] $Version,
  [string] $Workspace,
  [string] $Configuration    = 'Release'
)

# Build and pack
$artifactsFolder = Join-Path $Workspace "artifacts"
msbuild $ProjectPath `
    /t:Pack /p:Configuration=$Configuration /p:NoBuild=true /p:NoRestore=true `
    /p:PackageVersion=$Version /p:PackageOutputPath=$artifactsFolder

# Verify package creation
$nupkg = Get-ChildItem "$artifactsFolder/*.nupkg" -ErrorAction SilentlyContinue
if (-not $nupkg) {
    Write-Error "NuGet package was not created. Check msbuild logs for Pack errors."
    exit 1
}

# Output results
Write-Host "Package created: $($nupkg.FullName)"
"nupkg-path=$($nupkg.FullName)" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"version=$Version" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
