# Workbench Session Handoff

Last updated: 2026-04-17

## Scope

Bring Chummer6 desktop UX much closer to Chummer5a layout posture:

- no dashboard-style first-launch detour
- dense classic chrome
- compact secondary windows
- parity-minded Avalonia and Blazor workbench presentation
- release pipeline should consume the current pushed UI snapshot

## Last pushed baseline

- Branch: `safe-push-fix-windows-installer-payload-20260401`
- Pushed refs:
  - `origin/safe-push-fix-windows-installer-payload-20260401`
  - `origin/fleet/ui`
  - `origin/main`
- Last pushed UI commit before this uncommitted slice: `8b10754e`
  - message: `Tighten desktop jump window chrome`

## Uncommitted current slice

Files changed locally:

- `Chummer.Avalonia/DesktopInstallLinkingWindow.cs`
- `Chummer.Avalonia/DesktopHomeWindow.cs`
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`

What this slice changes:

- `DesktopInstallLinkingWindow`
  - smaller shell
  - reduced padding/spacing/font noise
  - split giant action row into two tighter rows
  - lighter classic background
- `DesktopHomeWindow`
  - no longer auto-opens just because workspace count is zero
  - reduced dashboard hero treatment
  - flatter classic border/padding/corner/button sizing
  - denser overall content spacing
- `AvaloniaFlagshipUiGateTests`
  - adds a source-level hard gate to prevent the empty-workspace home-window detour from returning

## Validation status

What passed:

- targeted source-level tests:
  - `Avalonia_startup_enters_the_workbench_without_reopening_the_desktop_home_cockpit`
  - `Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state`

What failed:

- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj --no-restore -v minimal`

Failure was not from this UI patch. The workspace currently has restore/package state issues:

- `NETSDK1064: Package Microsoft.Extensions.DependencyInjection, version 10.0.0 was not found`
- this cascades into `MSB4181` in dependent projects

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal
dotnet test Chummer.Tests/Chummer.Tests.csproj --no-build --filter "Name~Avalonia_startup_enters_the_workbench_without_reopening_the_desktop_home_cockpit|Name~Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state|Name~Opening_mainframe_preserves_chummer5a_successor_workbench_posture" -v minimal
git status --short
```

If build passes, commit only the three files above plus this handoff file:

```bash
git add Chummer.Avalonia/DesktopInstallLinkingWindow.cs
git add Chummer.Avalonia/DesktopHomeWindow.cs
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Compact install linking and suppress desktop home detour"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Audit menu enablement in Avalonia shell and fix disabled menus.
2. Audit desktop app icon wiring in Avalonia/Windows packaging.
3. Re-check Blazor desktop startup/loading path against the current workbench shell.
4. Continue replacing remaining oversized “jump” surfaces with denser classic layouts.
5. Run screenshot/audit pass and document intentional diffs from Chummer5a.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
