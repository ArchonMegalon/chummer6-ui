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
- Last pushed UI commit: `e9f4880e`
  - message: `Densify classic runner sheet presentation`

## Uncommitted current slice

Files changed locally:

- `Chummer.Avalonia/MainWindow.axaml`
- `Chummer.Avalonia/Controls/NavigatorPaneControl.axaml`
- `Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor`
- `Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor`
- `Chummer.Blazor/wwwroot/app.css`
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Avalonia/MainWindow.axaml`
  - widens the fixed left rail to a Chummer5a-like workbench band instead of the narrower post-modern strip
- `Chummer.Avalonia/Controls/NavigatorPaneControl.axaml`
  - removes the visible navigator banner/caption row so the tree starts immediately and stops wasting vertical space
- `Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor`
  - hides the heading visually, removes visible workspace-id noise from dossier rows, and compacts the close affordance
- `Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor`
  - suppresses the secondary action/workflow panels until a real workspace is active so first paint stays closer to the classic manager/workbench posture
- `Chummer.Blazor/wwwroot/app.css`
  - adds a reusable visually-hidden utility, tightens left-rail spacing, and aligns the Blazor left column width with the Avalonia classic workbench band
- `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
  - hard-gates the hidden navigator banner and the continued classic left-rail posture in the desktop shell gate
- `Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
  - hard-gates that the left rail stays compact: no visible workspace ids in dossier rows and no secondary left-rail sections before a workspace exists
- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - records the new pushed baseline and the current compact-left-rail slice in case the session dies before the next command slice lands

## Validation status

What passed:

- `git diff --check`
- `dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Desktop_shell_preserves_classic_dense_three_pane_workbench_posture|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_hides_secondary_left_rail_sections_until_workspace_context_exists|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.OpenWorkspaceTree_renders_open_and_close_actions"`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet restore Chummer.Blazor/Chummer.Blazor.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Blazor/Chummer.Blazor.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- pushed commit `e9f4880e` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`

What still needs direct verification:

- commit and push this left-rail compaction slice
- inspect the live desktop shell command surface after first launch to confirm the enabled commands actually render as expected
- continue with the next parity jump after this guardrail: menu behavior, icon/signing path, and any remaining non-classic surfaces that still survive first launch

What failed:

- repo-local `.tmp/nuget/packages` validation remained flaky with `NETSDK1064` on `Microsoft.Extensions.DependencyInjection 10.0.0`
- the stable workaround for this slice is to force `RestorePackagesPath=/home/tibor/.nuget/packages` on restore and build/test invocations
- `Chummer.Blazor.Desktop` host builds still remain flaky under the same restore graph; this slice was validated against the Blazor surface project plus component/hard-gate coverage instead of the desktop wrapper host

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Desktop_shell_preserves_classic_dense_three_pane_workbench_posture|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_hides_secondary_left_rail_sections_until_workspace_context_exists|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.OpenWorkspaceTree_renders_open_and_close_actions"
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet restore Chummer.Blazor/Chummer.Blazor.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Blazor/Chummer.Blazor.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
```

Commit only the eight files above plus the handoff:

```bash
git add Chummer.Avalonia/MainWindow.axaml
git add Chummer.Avalonia/Controls/NavigatorPaneControl.axaml
git add Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor
git add Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor
git add Chummer.Blazor/wwwroot/app.css
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add Chummer.Tests/Presentation/BlazorShellComponentTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Compact classic left rail chrome"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this left-rail compaction slice.
2. Verify the first-launch menubar/toolstrip on the live desktop head instead of only at component level.
3. Fix any command surface that still feels dead on first launch even though startup-safe commands should be enabled.
4. Run the screenshot-level comparison against Chummer5a and list only remaining intentional diffs.
5. Continue with icon/signing and release-train correctness so mac builds stop lagging or surfacing stale-feeling snapshots.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet restore Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Desktop_shell_preserves_classic_dense_three_pane_workbench_posture|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.WorkspaceLeftPane_hides_secondary_left_rail_sections_until_workspace_context_exists|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.OpenWorkspaceTree_renders_open_and_close_actions"
dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet restore Chummer.Blazor/Chummer.Blazor.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
dotnet build Chummer.Blazor/Chummer.Blazor.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
git add Chummer.Avalonia/MainWindow.axaml Chummer.Avalonia/Controls/NavigatorPaneControl.axaml Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor Chummer.Blazor/wwwroot/app.css Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Compact classic left rail chrome"
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
