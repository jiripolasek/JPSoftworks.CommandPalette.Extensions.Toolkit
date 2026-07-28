param(
  [string] $ProjectPath,
  [string] $Version,
  [string] $Workspace,
  [string] $Configuration    = 'Release'
)

# Pack the already-built project or solution
$artifactsFolder = Join-Path $Workspace "artifacts"
dotnet pack $ProjectPath `
    --configuration $Configuration `
    --no-build `
    --no-restore `
    -p:PackageVersion=$Version `
    --output $artifactsFolder

# Verify package creation
$packages = @(Get-ChildItem "$artifactsFolder/*.nupkg" -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    Write-Error "No NuGet packages were created. Check dotnet pack logs for errors."
    exit 1
}

$symbolPackages = @(Get-ChildItem "$artifactsFolder/*.snupkg" -ErrorAction SilentlyContinue)
$missingSymbolPackages = @(
    $packages | Where-Object {
        -not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($_.FullName, ".snupkg")) -PathType Leaf)
    }
)

if ($missingSymbolPackages.Count -ne 0) {
    $missingPackageNames = $missingSymbolPackages.Name -join ", "
    Write-Error "Matching symbol packages were not created for: $missingPackageNames."
    exit 1
}

# Output results
Write-Host "Packages created:"
$packages.FullName | ForEach-Object { Write-Host "  $_" }

Write-Host "Symbol packages created:"
$symbolPackages.FullName | ForEach-Object { Write-Host "  $_" }

"nupkg-paths=$($packages.FullName -join ';')" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"snupkg-paths=$($symbolPackages.FullName -join ';')" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
"version=$Version" | Out-File -Append -FilePath $env:GITHUB_OUTPUT
