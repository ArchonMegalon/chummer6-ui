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

## Uncommitted current slice

Files changed locally:

- `Chummer.Avalonia/App.axaml`
- `Chummer.Avalonia/MainWindow.axaml`
- `Chummer.Avalonia/Controls/NavigatorPaneControl.axaml`
- `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- `Chummer.Avalonia/Controls/SectionHostControl.axaml.cs`
- `Chummer.Avalonia/Controls/StatusStripControl.axaml`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Avalonia/App.axaml`
  - replaces the pastel shell palette with a denser classic desktop palette closer to old Chummer/WinForms posture
  - reduces default padding, spacing, list-row cardiness, and menu/button height so the shell wastes less space
- `Chummer.Avalonia/MainWindow.axaml`
  - tightens the overall shell footprint, narrows the left rail, and reduces chrome spacing
- `Chummer.Avalonia/Controls/NavigatorPaneControl.axaml`
  - surfaces a visible `Codex` heading/caption at the top of the left tree instead of leaving that landmark hidden
- `Chummer.Avalonia/Controls/SectionHostControl.axaml`
  - hides the idle notice band by default
  - tightens row spacing and padding
  - switches classic summary/attribute presentation away from the oversized card rhythm
  - makes the dense row list more table-like
- `Chummer.Avalonia/Controls/SectionHostControl.axaml.cs`
  - only shows the notice band when the notice is meaningfully different from the default ready state
  - increases useful row-list height for classic sections
  - compacts attribute cards into smaller stat boxes
- `Chummer.Avalonia/Controls/StatusStripControl.axaml`
  - keeps the required progress bar but compresses the status strip into a more classic single-line rhythm

## Validation status

What passed:

- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false`
- `git diff --check`
- manual structural sanity pass on the edited Avalonia XAML/code-behind files

What still needs direct verification:

- commit and push this Avalonia density slice
- launch Avalonia locally and compare against Chummer5a screenshots
- continue with the next parity jump in Blazor: remove right-rail/dashboard leftovers and keep only classic workbench chrome

What failed:

- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal`
  - workspace-level transitive build churn hit unrelated restore/project-reference problems (`NETSDK1064`, `MSB4181`, later interruption noise)
  - this failure did not point at a concrete syntax error in the edited Avalonia files, but the full project still needs a clean local build pass after the workspace dependency lane is stable

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/App.axaml Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Avalonia/Controls/StatusStripControl.axaml docs/WORKBENCH_SESSION_HANDOFF.md
```

Commit only the seven files above:

```bash
git add Chummer.Avalonia/App.axaml
git add Chummer.Avalonia/MainWindow.axaml
git add Chummer.Avalonia/Controls/NavigatorPaneControl.axaml
git add Chummer.Avalonia/Controls/SectionHostControl.axaml
git add Chummer.Avalonia/Controls/SectionHostControl.axaml.cs
git add Chummer.Avalonia/Controls/StatusStripControl.axaml
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Tighten Avalonia shell to classic dense posture"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this Avalonia density slice.
2. Launch Avalonia locally and compare against Chummer5a screenshots for menu/toolstrip density, tab strip posture, left rail width, status strip, and attribute layout.
3. Cut the remaining Blazor desktop jumps: remove the right-rail/dashboard posture and keep classic workbench chrome only.
4. Push the next parity slice before the next mac build so published preview artifacts stop lagging the actual UI repo state.
5. Run a screenshot/audit pass and document only intentional diffs from Chummer5a.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/App.axaml Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Avalonia/Controls/StatusStripControl.axaml docs/WORKBENCH_SESSION_HANDOFF.md
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false
git add Chummer.Avalonia/App.axaml Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Avalonia/Controls/StatusStripControl.axaml docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Tighten Avalonia shell to classic dense posture"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

Then continue immediately with the Blazor parity pass; Avalonia density will be materially improved, but the overall flagship scope is still blocked by the remaining desktop-shell jumps in Blazor.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
- The current highest-risk visible regressions after this slice are the remaining Blazor desktop right-rail/dashboard jumps and any last screenshot-level Avalonia drift from Chummer5a.
