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
- Last pushed UI commit: `cac546a8`
  - message: `Trim demo action from default Avalonia toolbar`

## Uncommitted current slice

Files changed locally:

- `Chummer.Blazor/Components/Shell/SectionPane.razor`
- `Chummer.Blazor/wwwroot/app.css`
- `Chummer.Avalonia/Controls/SectionHostControl.axaml.cs`
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Blazor/Components/Shell/SectionPane.razor`
  - swaps the loose fact strips for denser classic summary and attribute grids so the runner sheet reads closer to Chummer5a at a glance
- `Chummer.Blazor/wwwroot/app.css`
  - styles the summary cells and attribute boxes as compact desktop sheet blocks instead of generic cards
- `Chummer.Avalonia/Controls/SectionHostControl.axaml.cs`
  - tightens the Avalonia summary/attribute card dimensions, colors, and typography toward a denser classic sheet posture
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
  - hard-gates the presence of the new classic summary/attribute grids in the Blazor sheet source so the denser runner-sheet posture cannot silently drift
- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - records the new pushed baseline and the current runner-sheet parity slice in case the session dies before the next command slice lands

## Validation status

What passed:

- `git diff --check`
- `dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers"`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false -tl:off`
- `dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions|FullyQualifiedName~Runtime_backed_shell_chrome_stays_enabled_after_runner_load|FullyQualifiedName~Standalone_tool_strip_buttons_raise_expected_events"`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false -tl:off`
- pushed commit `cac546a8` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`

What still needs direct verification:

- inspect the live desktop shell command surface after first launch to confirm the enabled commands actually render as expected
- continue with the next parity jump after this guardrail: menu behavior, icon/signing path, and any remaining non-classic surfaces that still survive first launch

What failed:

- repo-local `.tmp/nuget/packages` validation remained flaky with `NETSDK1064` on `Microsoft.Extensions.DependencyInjection 10.0.0`
- the stable workaround for this slice is to force `RestorePackagesPath=/home/tibor/.nuget/packages` on restore and build/test invocations

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor/Components/Shell/SectionPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers"
dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers"
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
```

Commit only the five files above:

```bash
git add Chummer.Blazor/Components/Shell/SectionPane.razor
git add Chummer.Blazor/wwwroot/app.css
git add Chummer.Avalonia/Controls/SectionHostControl.axaml.cs
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Densify classic runner sheet presentation"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this runner-sheet parity slice.
2. Verify the first-launch menubar/toolstrip on the live desktop head instead of only at component level.
3. Fix any command surface that still feels dead on first launch even though startup-safe commands should be enabled.
4. Run the screenshot-level comparison against Chummer5a and list only remaining intentional diffs.
5. Continue with icon/signing and release-train correctness so mac builds stop lagging or surfacing stale-feeling snapshots.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor/Components/Shell/SectionPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers"
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
git add Chummer.Blazor/Components/Shell/SectionPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Densify classic runner sheet presentation"
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
