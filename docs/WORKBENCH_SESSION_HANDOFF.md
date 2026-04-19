# Workbench Session Handoff

Last updated: 2026-04-17

## Scope

Drive Chummer6 desktop toward hard Chummer5a-style parity:

- classic menu-first shell, not a dashboard
- dense left rail and runner sheet posture
- startup-safe commands visible and usable on first launch
- Avalonia and Blazor kept in lockstep where the same shell affordance exists
- release builds must ship the current pushed UI snapshot, not a stale head

## Last pushed baseline

- Branch: `safe-push-fix-windows-installer-payload-20260401`
- Pushed refs:
  - `origin/safe-push-fix-windows-installer-payload-20260401`
  - `origin/fleet/ui`
  - `origin/main`
- Last pushed UI commit: `68edf04c`
  - message: `Populate classic menu roots`

## Current uncommitted slice

Files changed locally:

- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - rolls the baseline forward after the pushed menu-fidelity slice and records the next parity target for crash/OOM recovery

## Validation status

What passed:

- pushed commit `68edf04c` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`
- `git diff --check Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_special_and_windows_menus_surface_real_commands|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters"`
  - result: `Passed 4/4`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
  - result: `Build succeeded`

What is still flaky in this repo in general:

- a stale restore graph can still fall back into `NETSDK1064` for `Microsoft.Extensions.DependencyInjection 10.0.0`
- stable recovery is to rerun restore immediately before the build with the shared package cache:
  - `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
  - then rerun the same build command

Important note:

- Ctrl-C during these builds can emit bogus `MSB3202` project-not-found noise. Ignore those if they appear immediately after a manual cancel.

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
DOTNET_CLI_UI_LANGUAGE=en dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
git add Chummer.Avalonia/MainWindow.ShellFrameProjector.cs
git add Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs
git add Chummer.Presentation/UiKit/ShellChromeBoundary.cs
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Populate classic menu roots"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next slices after this commit

1. Rebuild and inspect the live mac desktop preview to confirm the shipped Avalonia head now exposes `Special` and `Windows` commands instead of dead roots.
2. Continue the Chummer5a parity pass on remaining first-glance drifts:
   - menu/toolstrip density
   - icon correctness
   - startup shell posture
   - runner sheet spacing
3. Keep release-train correctness tight so the next mac bootstrap pulls the just-pushed UI head.

## Resume after interruption

If the session dies from OOM, pruning, or host restart, resume exactly here:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
sed -n '1,220p' docs/WORKBENCH_SESSION_HANDOFF.md
```

Then continue immediately with the next parity slice instead of re-auditing older work:

- audit icon packaging for Avalonia/Windows/macOS
- verify the startup shell still has no dead chrome after the menu-root fix
- keep the next handoff update current before any long-running build/release pass

## Non-negotiables

- Do not commit unrelated dirty or generated files.
- Keep using `RestorePackagesPath=/home/tibor/.nuget/packages` with `UseChummerEngineContractsLocalFeed=false` for restore/build/test work.
- The user wants Chummer5a as the layout reference. If a visible drift stays, either fix it or document a real user-facing reason.
