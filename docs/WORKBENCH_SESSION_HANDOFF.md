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
- Last pushed UI commit: `5acadbf3`
  - message: `Align Avalonia toolstrip with startup-safe classic order`

## Uncommitted current slice

Files changed locally:

- `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
- `Chummer.Avalonia/Controls/ToolStripControl.axaml`
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Avalonia/Controls/ToolStripControl.axaml`
  - keeps the startup-safe classic order and removes `Load Demo Runner` from the default toolbar so scarce chrome space stays focused on primary Chummer5a-style actions
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
  - hard-gates the trimmed default toolbar so `LoadDemoRunnerButton` stays out of the primary chrome while core actions remain visible
- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - records the new pushed baseline and the current toolbar-noise reduction slice in case the session dies before the next command slice lands

## Validation status

What passed:

- `git diff --check`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false -tl:off`
- `dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions|FullyQualifiedName~Runtime_backed_shell_chrome_stays_enabled_after_runner_load|FullyQualifiedName~Standalone_tool_strip_buttons_raise_expected_events"`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false -tl:off`
- pushed commit `5acadbf3` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`

What still needs direct verification:

- rerun the toolbar visibility tests after hiding the default demo button
- build the Avalonia desktop head after the toolbar trim
- inspect the live desktop shell command surface after first launch to confirm the enabled commands actually render as expected
- continue with the next parity jump after this guardrail: menu behavior, icon/signing path, and any remaining non-classic surfaces that still survive first launch

What failed:

- initial targeted validation before the explicit restore
  - `NETSDK1064` reported `Microsoft.Extensions.DependencyInjection 10.0.0` missing even though the package existed in the shared cache; rerunning an explicit `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj ...` repaired the restore graph and the subsequent targeted test/build passed

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/Controls/ToolStripControl.axaml Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_shell_chrome_stays_enabled_after_runner_load|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_tool_strip_buttons_raise_expected_events"
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false -tl:off
```

Commit only the three files above:

```bash
git add Chummer.Avalonia/Controls/ToolStripControl.axaml
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Trim demo action from default Avalonia toolbar"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this toolbar-noise reduction slice.
2. Verify the first-launch menubar/toolstrip on the live desktop head instead of only at component level.
3. Fix any command surface that still feels dead on first launch even though startup-safe commands should be enabled.
4. Run the screenshot-level comparison against Chummer5a and list only remaining intentional diffs.
5. Continue with icon/signing and release-train correctness so mac builds stop lagging or surfacing stale-feeling snapshots.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/Controls/ToolStripControl.axaml Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_shell_chrome_stays_enabled_after_runner_load|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_tool_strip_buttons_raise_expected_events"
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false -tl:off
git add Chummer.Avalonia/Controls/ToolStripControl.axaml Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Trim demo action from default Avalonia toolbar"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

Then continue immediately with the next parity pass: live menu/toolstrip behavior, screenshot audit, icon/signing fixes, and any remaining first-paint drift from Chummer5a.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
- The current highest-risk visible regressions after the pushed density and command-availability slices are whatever still makes first launch feel dead or unfamiliar: especially menu behavior, icon correctness, and any remaining dashboard feel on first paint.
