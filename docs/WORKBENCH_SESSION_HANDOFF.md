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
- Last pushed UI commit: `b3d66d77`
  - message: `Use component-only Blazor desktop host`

- Additional pushed UI commit after that baseline: `b2a48150`
  - message: `Tighten Avalonia shell to classic dense posture`

## Uncommitted current slice

Files changed locally:

- `Chummer.Blazor/wwwroot/app.css`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Blazor/wwwroot/app.css`
  - replaces the remaining modern/pastel Blazor shell palette with a denser classic desktop palette
  - narrows the workbench left rail and reduces overall chrome height
  - flattens menu/tool/tab/button styling back toward a WinForms-like rhythm
  - compresses the classic runner sheet, stat cards, browse shells, and table chrome so the first glance reads closer to Chummer5a instead of a web dashboard
  - converts the status strip into a flatter single-line classic bar instead of a rounded card
- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - records the current Blazor parity slice and exact resume commands in case the session dies before commit/push

## Validation status

What passed:

- manual inspection of the actual Blazor shell component mount points (`DesktopShell`, `SummaryHeader`, `WorkspaceLeftPane`, `SectionPane`, `StatusStrip`)

What still needs direct verification:

- `git diff --check`
- `dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false`
- `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false`
- commit and push this Blazor density slice
- rebuild on mac from the pushed snapshot and visually compare the shell against Chummer5a screenshots
- continue with the next parity jump after this slice: menu behavior, icon/signing path, and any remaining non-classic surfaces that still survive first launch

What failed:

- nothing in this current Blazor slice yet; validation commands still need to run after the edit

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor/wwwroot/app.css docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false
```

Commit only the two files above:

```bash
git add Chummer.Blazor/wwwroot/app.css
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Align Blazor desktop shell with classic density"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this Blazor density slice.
2. Rebuild Blazor Desktop locally and verify the shell no longer reads as a web dashboard on first paint.
3. Run the same screenshot-level comparison against Chummer5a that the user asked for and list only remaining intentional diffs.
4. Cut the next parity jumps that are not just skin: menu behavior, icon correctness, and first-launch/runtime chrome drift.
5. Push before the next mac build so the published preview stops lagging the actual UI repo state.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor/wwwroot/app.css docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false
git add Chummer.Blazor/wwwroot/app.css docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Align Blazor desktop shell with classic density"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

Then continue immediately with the next parity pass: screenshot audit, menu behavior fixes, icon/signing fixes, and any remaining first-paint drift from Chummer5a.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
- The current highest-risk visible regressions after this slice are whatever still differs at first glance from Chummer5a after the denser Blazor shell lands: especially menu behavior, icons, and any leftover dashboard feeling on first paint.
