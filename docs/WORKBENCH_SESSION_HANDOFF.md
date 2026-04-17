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
- Last pushed UI commit before this uncommitted slice: `89c4aa91`
  - message: `Synthesize classic menu roots for sparse rulesets`

## Uncommitted current slice

Files changed locally:

- `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
- `Chummer/chummer.ico`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `DesktopShell.razor.cs`
  - removes hidden coach-sidecar API loading from startup and normal shell-refresh paths
  - keeps coach activity opt-in instead of part of first paint
- `Chummer/chummer.ico`
  - replaces the stale UI repo icon with the newer 12-size Windows icon payload already present in `chummer-core-engine`
  - intended to fix the wrong/missing Windows desktop icon problem without changing packager logic

## Validation status

What passed:

- `dotnet build Chummer.Presentation/Chummer.Presentation.csproj -v minimal`
  - passed after the menu-compatibility slice that is already pushed
- icon drift audit:
  - `Chummer/chummer.ico` in this repo had SHA `d80d29d4...`
  - newer icon in `chummer-core-engine/Chummer/chummer.ico` had SHA `0454048d...`
  - the current local slice now uses the newer payload

What failed:

- direct desktop-head build from this split checkout:
  - `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal`

Failure is workspace-topology related, not caused by the current Blazor startup/icon slice. In this checkout, desktop-head project resolution falls into missing sibling repo paths such as:

- `../../chummer-core-engine/...`
- `../../chummer.run-services/...`
- `../../chummer-hub-registry/...`
- `../../../fleet/repos/chummer-media-factory/...`

The release bootstrap workspace has already proven both desktop heads can build when the integrated repo topology is present.

## Next exact commands

Run from repo root in an integrated workspace (the same topology the mac release bootstrap uses):

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor/Components/Layout/DesktopShell.razor.cs Chummer/chummer.ico docs/WORKBENCH_SESSION_HANDOFF.md
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal
git status --short
```

If the integrated workspace build passes, commit only the three files above:

```bash
git add Chummer.Blazor/Components/Layout/DesktopShell.razor.cs
git add Chummer/chummer.ico
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Trim Blazor startup work and refresh desktop icon payload"
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
