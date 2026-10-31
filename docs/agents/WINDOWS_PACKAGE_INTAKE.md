# Portable Windows package setup and desktop build

Use a controller-assigned Windows checkout and user-started Friday Code with its
existing provider. Git for Windows, Python 3.10+ (`python` or `py`) and the exact
Windows .NET SDK **10.0.103** are prerequisites. PowerShell entrypoints support
Windows PowerShell 5.1 and PowerShell 7. No agent/model is launched by these scripts.
See [WINDOWS_REMOTE.md](WINDOWS_REMOTE.md) for the feature-branch clone command,
nonsecret Teable binding and assignment handoff.

## Exact inputs

`config/package-plane.lock.json` fixes the IDs, versions, sizes and SHA-256 bytes
of 14 Chummer packages: eight Core, two UI-owner and four canonical-owner packages.
`config/windows-package-downloads.json` now accounts for all 14 with exact public
asset metadata and selected members; it has no unavailable packages. The setup
consumes that manifest generically and verifies downloaded/cached outer archives
and every selected nupkg. It never discovers latest releases, rebuilds sibling
packages, changes pins or accepts replacement bytes.

`config/agent-workspace.windows.json` also checks the SDK and MSBuild pins and the
canonical JSON digest of the lock:
`aa9927f51452a95e2a65b9df6e9979adfdf484c56c60d14a8e65a45148fb367f`.
`global.json` disables SDK roll-forward. Controllers own changes to these inputs.
The controller downloaded both public assets and verified all 14 package bytes on
Linux on 2026-10-31. Native Windows compilation and runtime remain unverified.

## Setup, check and build

From the checkout in Windows PowerShell, choose a private cache for the assignment:

```powershell
$cache = Join-Path $env:LOCALAPPDATA 'Chummer\friday\tasks\windows-desktop-001'
.\scripts\start-windows-worker.ps1 -Mode setup -CacheRoot $cache -Download
if ($LASTEXITCODE -ne 0) { throw 'Package setup failed; do not build from an incomplete feed.' }
.\scripts\start-windows-worker.ps1 -Mode check -CacheRoot $cache
.\scripts\start-windows-worker.ps1 -Mode next -CacheRoot $cache

# Only when a build is assigned: restore/build the primary Avalonia desktop head.
.\scripts\build-windows-desktop.ps1 -CacheRoot $cache
if ($LASTEXITCODE -ne 0) { throw 'Restore/build failed; inspect the reported local log.' }
```

`prepare`, `check` and `next` retain their previous roles. They do not fetch
packages or build. Assignment handoff works before build readiness. `setup` is
also usable without an installed SDK, so package preparation can precede SDK
installation. No SDK is installed automatically and no global settings change.
The native build requires Windows and confirms both installed and selected SDK
10.0.103 before starting restore. `-RepoRoot` supports another checkout location.

Without `-Download`, setup is offline: it uses existing cached assets or verifies
an existing complete feed. For exact local nupkg inputs use:

```powershell
.\scripts\start-windows-worker.ps1 -Mode setup -CacheRoot $cache -InputDir 'D:\ApprovedPackages'
```

A partial input directory can be combined with cached assets or explicit
`-Download`. `-PackageFeed` selects an existing or new flat feed outside Git;
pass the same cache/feed to subsequent check, next and build calls. The default
cache is per checkout under `%LOCALAPPDATA%\Chummer\friday\workspaces\<checkout-hash>`.
Setup refuses repository-local caches/outputs and never clears an existing cache.
Use a fresh cache for a new source policy; do not share it with unrelated tasks.

For an explicitly assigned compatibility build only:

```powershell
.\scripts\build-windows-desktop.ps1 -CacheRoot $cache -Project compatibility
```

That selects `Chummer.Blazor.Desktop` instead of the default `Chummer.Avalonia`.
No solution-wide build runs. If script execution policy prevents an entrypoint,
use the same Python helper: `python scripts/prepare-agent-workspace.py --mode
setup --cache-root CACHE --download`, or `--mode build --cache-root CACHE`.

## Restore isolation and execution evidence

Setup writes only the selected task feed, downloads, `NuGet.Config` and isolated
`nuget-packages`, `nuget-http`, `nuget-plugins` and `dotnet-home` directories.
The config clears inherited sources and fallback folders. Source mapping directs
`Chummer.*` only to the verified local feed; `*` uses official nuget.org.
The more specific Chummer prefix takes precedence over the wildcard.
Build children receive task-local NuGet/DOTNET caches, no Teable API key and no
inherited restore-source/fallback overrides. Conflicting Chummer package/version
or sibling-source overrides fail onboarding. Arguments contain no credentials.

The native commands are equivalent to the following, with all manifest package
version properties and `ChummerUseLocalCompatibilityTree=false` passed to both:

```powershell
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj --configfile TASK_NUGET_CONFIG --packages TASK_NUGET_PACKAGES --runtime win-x64 --disable-parallel
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj --no-restore --configuration Debug --runtime win-x64 --disable-build-servers -m:1
```

Restore failure prevents build. Each stage has a 15-minute timeout. Output is
captured into task-local `restore.log` / `build.log` after each stage and the
configured coordination token is redacted. JSON reports expose log paths,
selected project/RID, readiness blockers and per-invocation
`restore_executed`, `restore_succeeded`, `build_executed`, `build_succeeded`.
Timeout/launch errors report the failed stage without claiming success. A timeout
or keyboard interruption retains the build lock: the parent exiting does not
prove every MSBuild child has stopped. Check those processes before clearing the
task's lock and starting another build.
`build_ready` / `prerequisites_ready` mean local prerequisites were verified;
`native_runtime_validated` remains false until separate user/Friday acceptance.
A later check does not carry forward success from a prior build invocation.

The feed contains only the 14 Chummer packages, not the full third-party closure.
The modern projects have no NuGet `packages.lock.json` at this baseline; official
nuget.org third-party restore is not byte-locked or proven offline. Successful
intake does not certify package-delivered runtime data, native compilation,
startup, installation or character save/reopen. Report actual Windows commands,
OS/runtime and launch/open/import/save/close/reopen evidence separately.

## Preservation and interruption recovery

The reviewed `scripts/fetch-windows-package-feed.py` is stdlib-only. It checks
outer and nested sizes/SHA-256, nuspec identity/version, ZIP paths, duplicate and
special members, decompression bounds and local reparse points. Only selected
flat nupkg files are staged and renamed into a new complete output. Existing
incomplete, corrupt or extra output is rejected without repair. Corrupt cached
assets are preserved; choose a fresh path after diagnosis. Public downloads
require opt-in, use exact credential-free HTTPS assets and restrict redirects.

Setup/build share an exclusive per-checkout `build.lock` in the default local
workspace state, even when selecting another cache. Intake also has its own
sibling `.feed.intake-lock`. These are Windows-local locks, independent of Linux.
After interruption, first confirm no setup/build is still active, preserve its
logs/output, and reconcile surviving locks or staging directories before retry.
No stale lock is removed automatically. Nothing here changes assignments,
publishes artifacts or changes Friday Code routing/authentication.

## Validation limits

Focused workspace and intake tests use disposable repositories, synthetic pinned
archives and mocked dotnet invocations. They exercise setup/readiness and stage
failure behavior without SDK/product builds or live APIs. PowerShell execution
and native Windows restore/build/runtime acceptance remain the Windows host's
verification work; the controller's public-download check does not prove them.
