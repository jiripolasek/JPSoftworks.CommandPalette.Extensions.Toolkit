# Engineering

Build, package, version, and release the Toolkit. Run all commands from the repository root.

## Local development

Repository-local outputs are written beneath the ignored `artifacts` directory:

```powershell
.\eng\build.ps1
.\eng\test.ps1 -NoBuild -NoRestore
.\eng\pack.ps1
.\eng\publish-local.ps1
.\eng\publish-nuget.ps1
.\eng\verify-aot.ps1
```

Restore operations used by `build.ps1`, `test.ps1`, and `pack.ps1` run in locked mode. Package names, paths, and the
AOT smoke-test project are declared in `eng\Package.config.psd1`. `pack.ps1` produces every Toolkit `.nupkg` together
with a matching `.snupkg`, and `publish-local.ps1` copies both package types to `artifacts\local-feed`.

`test.ps1` also runs the managed COM lifetime and shutdown smoke tests for both target frameworks. Shutdown tests
cover hosted and legacy factories, window-close and end-session messages, and throwing disposal callbacks. Each
case runs in its own process and sends messages only to that process's monitor window; no system shutdown occurs.
Lifetime tests verify the lowered process priority, then restore normal priority before the timed COM checks.

`publish-nuget.ps1` validates the complete package set and asks for confirmation before publishing it to NuGet.org.
It accepts stable versions and exact lowercase `preview.N` prereleases; other prerelease suffixes are rejected.
It uses the `NUGET_API_KEY` environment variable or an API key already configured for NuGet.org. Each `.nupkg` and
`.snupkg` is pushed separately. Existing versions fail by default. Use `-NoPack -Resume` only after verifying
that the existing feed packages came from the interrupted upload, and reuse its original package outputs.
Environment keys and `-PromptForApiKey` require NuGet 7.6 or newer (`dotnet nuget --version`); older clients can use
configured NuGet API keys. Pass `-PromptForApiKey` to enter a key for packages and symbols without storing it in shell
history or NuGet configuration. The script restores the previous key environment variables when it finishes.

For `publish-nuget.ps1` and `publish-github.ps1`, `-WhatIf` previews the operation without building, packing,
or publishing. Add `-NoPack -WhatIf` to validate an existing package set without uploading it. GitHub publishing
requires an explicit `-Version` and rejects stable and `preview.N` release identities, including casing variants.

`verify-aot.ps1` treats warnings as errors, publishes both target frameworks for `win-x64` and `win-arm64` beneath
`artifacts\aot`, and verifies that every output is native rather than framework-dependent.
Use `-ArtifactsPath` to select a shorter output root when the checkout path would exceed the native linker's
path length limit. CI uses the runner's temporary directory for these outputs.

## Version management

All four packages share the version in `eng\Package.props`. The local build and publishing commands remain the
entry points for packaging; version management does not require GitHub Actions or MinVer.

```powershell
.\eng\get-version.ps1
.\eng\set-version.ps1 -Version 0.12.0-preview.2
.\eng\set-version.ps1 -Stable -WhatIf
.\eng\set-version.ps1 -Stable
```

`set-version.ps1` updates the version properties and matching internal project dependencies in the solution's
lock files. It preserves external dependency versions, hashes, encoding, and line endings, and does not restore,
commit, tag, or publish. Versions use `major.minor.patch` with an optional prerelease suffix; build metadata and
leading zeroes in numeric identifiers are rejected to keep package filenames and NuGet identities consistent.

Review and commit the stable version on `master`, then create its local annotated tag:

```powershell
.\eng\tag-release.ps1 -WhatIf
.\eng\tag-release.ps1
```

The tag command reads the committed version from a clean checkout and rejects existing tags. Stable versions
require `ReleaseBranch` (`master`) in `eng\Package.config.psd1`. Exact lowercase `preview.N` versions also allow
`DevelopmentBranch` (`next`). Other prerelease suffixes are rejected. The command does not push or publish.
Pushing the tag triggers the release workflow, which checks the committed version and allowed origin branches.

After a stable release, start the next development version on `next`. Incrementing resets lower version
components and uses `preview.1` by default. Other suffixes are available for local or GitHub Packages builds,
but cannot be tagged for release. NuGet.org releases accept only stable versions and `preview.N`.

```powershell
.\eng\set-version.ps1 -Increment Patch
.\eng\set-version.ps1 -Increment Minor -WhatIf
```

CI can generate a distinct development package version without editing the committed version or lock files:

```powershell
$version = .\eng\get-version.ps1 -CiBuild 147
.\eng\build.ps1 -Version $version
.\eng\test.ps1 -NoBuild -NoRestore
.\eng\pack.ps1 -Version $version -NoBuild -NoRestore
```

A source version of `0.12.0-preview.1` produces `0.12.0-preview.1.ci.147`, which sorts after `preview.1` and before
`preview.2`. A stable source version of `0.12.0` produces `0.12.0-ci.147`, which sorts below `0.12.0`. After a
stable release, increment the source version before publishing further development builds; no prerelease can
sort above the stable release with the same numeric version. Use the CI workflow's run number as `CiBuild`.
Explicit `-Version` overrides remain available for local builds and packages. `pack.ps1 -NoBuild` rejects
assemblies built with a different version and removes the invalid packages. When reusing outputs with `-NoPack`, pass the version
that was packed if it differs from the current file version. Run `.\eng\test-versioning.ps1` to test version edits
and tagging in an isolated repository beneath `artifacts`.

Long preview CI versions can trigger [NU5123](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu5123)
for the `Logging.MicrosoftExtensions` package. If a client fails on path length, use `dotnet restore`, which
[supports long paths](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-long-path), or set
`NUGET_PACKAGES` to a shorter directory. The packing warning remains visible.

## GitHub Actions

| Trigger | Checks and output | Publishing |
| --- | --- | --- |
| Pull request to `master` or `next` | Locked restore, Release build, tests, packaging, and Native AOT | None |
| Push to `master` or `next` | Same checks; packages use the CI version described above | Four prerelease packages to GitHub Packages, unless the matching stable release is already tagged |
| Manual CI run | Same checks; packages available as workflow artifacts | None |
| Push a stable `vX.Y.Z` tag | Same checks; tag must match the source version and belong to `origin/master` | Four packages and symbols to NuGet.org, followed by a GitHub release |
| Push a `vX.Y.Z-preview.N` tag | Same checks; tag must match the source version and belong to `origin/master` or `origin/next` | Four packages and symbols to NuGet.org, followed by a GitHub prerelease |

For every source version, CI publishes to GitHub Packages only while the stable `vX.Y.Z` tag for its numeric
prefix is absent from `origin`. Once that tag exists, further pushes still validate and upload package
artifacts, but skip feed publication, including when `next` still has a prerelease suffix. Increment the
numeric version to start publishing the next development version. Preview tags do not block CI publishing.
A failed remote query fails the publishing check.

Only stable tags and exact lowercase `preview.N` prerelease tags are accepted for release publishing. `N` is a
nonnegative integer without leading zeroes. Tags such as `v0.12.0-beta.1`, `v0.12.0-rc.1`, `v0.12.0-ci.147`, and
`v0.12.0-preview.1.extra` are rejected. Automatic `ci.N` packages from branch pushes continue to use GitHub Packages.

Both workflows share `validate.yml` and the local `eng` scripts. Each run builds once with its resolved package
version, tests those assemblies, and packs without rebuilding. Tests cover both .NET 9 and .NET 10;
Native AOT publishing covers both frameworks on `win-x64` and `win-arm64`. Native output is inspected but not
executed. CI installs SDKs 9.0.318 and 10.0.400 in an isolated directory to match the lock files and provide
NuGet 7.6 environment-key support. Local `global.json` SDK selection is unchanged. Update the CI SDK pins
alongside dependency lock files when upgrading the toolchain. The build job and each AOT runtime use separate
NuGet caches, keyed by the lock files and SDK setup action, so one job cannot exclude another's runtime packs.
When caching is enabled, the setup action defaults `NUGET_PACKAGES` to `artifacts/nuget` in the workspace and
preserves an existing caller-supplied value. Restore and cache operations therefore use the same directory.

Validation also installs the Windows SDK 10.0.22621.0 metadata required by CsWinRT when it is missing from
the runner. The setup action uses the checksum-pinned 10.0.22621.5040 installer and its UWP Managed Apps
feature. Native AOT uses the runner's existing C++ build tools.

The `packages` artifact contains all four `.nupkg` and four `.snupkg` files and is retained for 30 days.
Publishing downloads that artifact without rebuilding. GitHub Packages receives `.nupkg` files; symbols
remain available in the artifact. TRX test results are retained for 14 days, including failed test runs.
Pull requests have read-only permissions; package and release write access is limited to the publishing jobs.
Third-party actions in workflows and local composite actions are pinned to commits and checked weekly by Dependabot.

### Initial publishing setup

GitHub Packages uses the repository's automatic `GITHUB_TOKEN` with `packages: write`. No separate publishing
secret is needed. If a package already exists, grant this repository Actions access in its package settings.

For NuGet.org, configure [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing):

1. Create the GitHub environment `nuget`, allowing release tags matching `v*` to deploy.
2. Set the repository or environment variable `NUGET_USER` to your NuGet.org profile name, not your email.
3. Add a NuGet.org trusted publishing policy for owner `jiripolasek`, repository
   `JPSoftworks.CommandPalette.Extensions.Toolkit`, workflow `release.yml`, and environment `nuget`.
   Grant it access to all four package IDs (for example, `JPSoftworks.CommandPalette.Extensions.Toolkit*`).

The workflow obtains a temporary NuGet key through OpenID Connect immediately before publishing. Local
`publish-nuget.ps1` still supports configured or prompted API keys. No long-lived NuGet secret is required by CI.

### Releasing and retrying

For a stable release, prepare the version with `set-version.ps1 -Stable` and commit it to `master`.
For a preview, use an exact version such as `set-version.ps1 -Version 0.12.0-preview.2` and commit it to `master`
or `next`. Push the commit and wait for CI to publish its version from `get-version.ps1 -CiBuild` to GitHub
Packages. Install that exact CI version in your consuming application and check it before tagging.

Create the release tag from a clean checkout of the allowed branch at the same commit you tested:

```powershell
.\eng\tag-release.ps1
$version = .\eng\get-version.ps1
git push origin "v$version"
```

The release workflow rebuilds the tagged commit with the version in `eng\Package.props`. The source commit
matches the tested CI build; its package and assembly versions change to the release version and are tested again.

Wait for Release to succeed, or complete the manual recovery below, before announcing the version.
The workflow publishes all packages and symbols before
publishing the GitHub release with generated notes and the same eight package assets. Preview releases are
marked as prereleases and never as the latest release, including when resuming a draft.

After a stable release, start the next version on `next` using `set-version.ps1 -Increment Patch` or
`-Increment Minor` and commit that change. For another preview of the same version, increment only `N`, for
example `set-version.ps1 -Version 0.12.0-preview.3`, before committing and tagging again.

If publication is interrupted, rerun the same workflow while its upload artifacts are available. Before
the first push, the workflow saves the original packages with their feed, version, and SHA-256 hashes.
Both **Re-run failed jobs** and **Re-run all jobs** restore those exact bytes, even if validation rebuilt
the `packages` artifact. The GitHub release also attaches the saved packages.

Each successful package or symbol push is recorded in a progress file. An always-run step saves that
progress in a separate artifact for each attempt. Retries combine earlier progress and pass `-ProgressPath`
to skip duplicates only for files with a confirmed successful push of the same bytes to the same feed.
A first-attempt conflict therefore remains an error on later attempts. If the server accepted a package
but the client failed before confirming success, or saving progress failed, follow
[manual recovery](#manual-recovery-after-an-unconfirmed-nuget-upload). The workflow cannot safely ignore
that conflict automatically.

For a local retry, pass `-NoPack -Resume` and the original `-Version` when it differs from the source version.
This explicit override skips duplicates without comparing feed contents, so use it only after verifying
that existing packages came from that interrupted upload. Workflows use confirmed progress instead.
GitHub releases stay in draft until their assets finish uploading. Do not move an already published tag or
reuse a published version for changed code. Protect `master` and release tags with repository rules as needed.

To preview or publish a local development build to GitHub Packages:

```powershell
$source = .\eng\get-version.ps1
$version = if ($source.Contains('-')) { "$source.local.1" } else { "$source-local.1" }
.\eng\pack.ps1 -Version $version
.\eng\publish-github.ps1 -Version $version -NoPack -WhatIf
# Set GITHUB_TOKEN securely to a token with write:packages access before publishing.
.\eng\publish-github.ps1 -Version $version -NoPack
```

Increment the local suffix for each changed local build. A source of `0.12.0-preview.1` produces
`0.12.0-preview.1.local.1`, which sorts below `preview.2` but above CI builds of `preview.1`. Reserve versions
ending in `.ci.N` for workflow run numbers; choosing an unused run number locally can collide with a future CI run.

The publisher defaults to `GITHUB_ACTOR` or the repository owner; pass `-Username` when using another account.
Its temporary NuGet configuration refers to the token environment variable and is removed after publishing.
GitHub NuGet consumers must authenticate even for public packages; see
[GitHub's NuGet registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).
Run `eng/test-publishing.ps1` to validate publisher preflight checks and retry behavior with fake clients.

### Manual recovery after an unconfirmed NuGet upload

Use this procedure when release validation succeeded but NuGet publication is blocked by an upload whose
success was not recorded. Let any active workflow attempt finish before starting. Manual recovery completes
NuGet publication and the GitHub release locally; the original Actions run stays failed and its
`github-release` job stays skipped. Local publishing does not update the run's progress artifacts.

1. Recover the original snapshot in a separate full clone. Use PowerShell 7, Git, a .NET SDK with NuGet 7.6
   or newer, and `gh` authenticated with permission to download workflow artifacts and publish releases
   (`gh auth login`). Replace the example run ID and tag with the failed Release run's values. The commands
   check that the run and tag identify the same commit, with the expected source version and branch ancestry.

   ```powershell
   $ErrorActionPreference = 'Stop'
   $repo = 'jiripolasek/JPSoftworks.CommandPalette.Extensions.Toolkit'
   $runId = '1234567890'
   $tag = 'v0.12.0-preview.2'
   $recoveryPath = Join-Path ([IO.Path]::GetTempPath()) ("toolkit-release-" + [guid]::NewGuid().ToString('N'))
   gh repo clone $repo $recoveryPath
   if ($LASTEXITCODE -ne 0) { throw 'Could not clone the release repository.' }
   Set-Location $recoveryPath
   git fetch origin --tags
   if ($LASTEXITCODE -ne 0) { throw 'Could not fetch release history.' }
   git switch --detach $tag
   if ($LASTEXITCODE -ne 0) { throw 'Could not check out the release tag.' }
   $version = .\eng\verify-release.ps1 -Tag $tag
   $runCommit = gh run view $runId --repo $repo --json headSha --jq .headSha
   if ($LASTEXITCODE -ne 0) { throw 'Could not read the failed run.' }
   $commit = git rev-parse HEAD
   if ($LASTEXITCODE -ne 0 -or $runCommit -cne $commit) { throw 'The run does not match the release tag.' }
   gh run download $runId --repo $repo --name upload-nuget --dir artifacts/upload-nuget
   if ($LASTEXITCODE -ne 0) { throw 'Could not download the original upload snapshot.' }
   Import-Module .\eng\Packaging.psm1 -Force
   Initialize-ToolkitPublishState -Version $version -Target nuget -StatePath artifacts/upload-nuget/state.json -RequireExisting | Out-Null
   .\eng\publish-nuget.ps1 -Version $version -NoPack -WhatIf
   .\eng\publish-release.ps1 -Tag $tag -WhatIf
   ```

   `Initialize-ToolkitPublishState` checks all eight saved archives against the snapshot's feed, version,
   identities, and SHA-256 hashes, then restores them to `artifacts/packages`. Keep those exact files for
   every remaining step. Do not repack or substitute the rebuilt `packages` artifact. If `upload-nuget`
   expired or is missing, recover a retained copy of that same snapshot before continuing.

2. Verify every package or symbol upload that the retry would skip. Use the run logs and
   `upload-nuget-progress-*` artifacts to identify confirmed pushes. For an unconfirmed `.nupkg` already on
   NuGet.org, download it from its package page and compare its extracted contents with the saved archive.
   Account for NuGet.org's added `.signature.p7s`; repository signing changes the archive hash but leaves
   the other contents intact. See [repository signing](https://devblogs.microsoft.com/dotnet/introducing-repository-signatures/).
   For an unconfirmed symbol upload, check its validation status on NuGet.org and compare the PDBs retrieved
   through the [symbol server](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg#nugetorg-symbol-server)
   with those in the saved `.snupkg`. A 409 alone does not establish that the contents match. Resolve any
   mismatch or unavailable evidence before proceeding; do not use `-Resume` to accept unrelated packages.

3. Create a [NuGet.org API key](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-an-api-key)
   with Push permission for `JPSoftworks.CommandPalette.Extensions.Toolkit*`, covering all four package IDs.
   This is a local recovery credential; the workflow's trusted-publishing key is not available for this
   procedure. Enter the key at the secure prompt, then confirm publication:

   ```powershell
   .\eng\publish-nuget.ps1 -Version $version -NoPack -Resume -PromptForApiKey
   ```

   This retries all four packages and all four symbol archives independently, skipping the duplicates
   verified in step 2. If it fails, resolve the reported error and repeat with the same saved files.
   Continue only after this command succeeds and NuGet.org reports successful package and symbol validation.

4. Run the same script used by the workflow's `github-release` job to attach the eight original archives
   and publish the release notes. It creates or resumes a draft and retains the stable/preview behavior:

   ```powershell
   .\eng\publish-release.ps1 -Tag $tag
   ```

   If asset upload fails, repeat this command with the same checkout and files. After success, verify the
   published assets and record the manual recovery with a link to the failed Actions run in the release
   notes. Revoke the recovery API key. The original run remains failed; rerunning it does not import the
   manual completion or make the skipped job successful. Preserve the release tag and published version.
