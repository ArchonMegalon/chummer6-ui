# Workbench Session Handoff

Last updated: 2026-04-17

## Scope

Bring Chummer6 desktop UX much closer to Chummer5a layout posture:

- no dashboard-style first-launch detour
- dense classic chrome
- compact secondary windows
- parity-minded Avalonia and Blazor workbench presentation
- no hidden startup work that can stall first paint
- current desktop packaging should ship the current icon payload
- release pipeline should consume the current pushed UI snapshot

## Last pushed baseline

- Branch: `safe-push-fix-windows-installer-payload-20260401`
- Pushed refs:
  - `origin/safe-push-fix-windows-installer-payload-20260401`
  - `origin/fleet/ui`
  - `origin/main`
- Last pushed UI commit before this uncommitted slice: `88032e88`
  - message: `Trim Blazor startup work and refresh desktop icon payload`

## Uncommitted current slice

Files changed locally:

- `Directory.Build.props`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Directory.Build.props`
  - introduces one shared compatibility-root property instead of repeating sibling repo probes inline
  - switches local compatibility-tree paths to cross-platform slash form
  - derives `ChummerUseLocalCompatibilityTree` from the normalized local project properties instead of duplicating raw path strings
  - restores local desktop-head builds from this split workspace on Linux/macOS

## Validation status

What passed:

- `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal`
- `dotnet msbuild Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -getProperty:ChummerCompatibilityRoot -getProperty:ChummerLocalContractsProject -getProperty:ChummerUseLocalCompatibilityTree`
  - returned `ChummerUseLocalCompatibilityTree=true` with normalized compatibility-root paths

What failed:

- no current blocker in this slice; the local desktop build regression is resolved

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Directory.Build.props docs/WORKBENCH_SESSION_HANDOFF.md
git status --short
```

Commit only the two files above:

```bash
git add Directory.Build.props
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Normalize local compatibility tree resolution"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Build and run the next mac preview from the pushed refs so the published artifact is no longer older than the current UI shell commits.
2. Audit Avalonia shell against a Chummer5a screenshot pass: menu/toolstrip density, tab strip posture, left rail width, and attribute layout.
3. Cut the remaining “jump” chrome in Blazor and Avalonia where a classic workbench surface should own the first glance.
4. Re-check Blazor runtime after removing hidden coach startup work; if loading still hangs, instrument the actual first-render exception path.
5. Run screenshot/audit pass and document only the intentional diffs from Chummer5a.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
