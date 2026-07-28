param(
  [string] $ProjectPath,
  [string] $Version,
  [string] $Workspace,
  [string] $Configuration    = 'Release'
)

# Pack the already-built project
$artifactsFolder = Join-Path $Workspace "artifacts"
dotnet pack $ProjectPath `
    --configuration $Configuration `
    --no-build `
    --no-restore `
    -p:PackageVersion=$Version `
    --output $artifactsFolder

# Verify package creation
$nupkg = Get-ChildItem "$artifactsFolder/*.nupkg" -ErrorAction SilentlyContinue
if (-not $nupkg) {
    Write-Error "NuGet package was not created. Check dotnet pack logs for errors."
    exit 1
}

# Output results
Write-Host "Package created: $($nupkg.FullName)"
"nupkg-path=$($nupkg.FullName)" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"version=$Version" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
