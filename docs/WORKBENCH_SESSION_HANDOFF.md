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
- Last pushed UI commit: `228d0d0f`
  - message: `Align Blazor desktop shell with classic density`

## Uncommitted current slice

Files changed locally:

- `Chummer.Tests/Chummer.Tests.csproj`
- `Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Tests/Chummer.Tests.csproj`
  - adds `Presentation/CommandAvailabilityEvaluatorTests.cs` to the non-Windows explicit compile list so the guardrail actually enters the Linux/macOS test assembly
- `Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs`
  - locks the startup desktop shell posture: `new_character`, `open_character`, `global_settings`, and `report_bug` must stay enabled with no active workspace, while `save_character` remains correctly gated
- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - records the new pushed baseline, the missing-compile-item root cause, and the next menu/toolstrip investigation path in case the session dies before the next command slice lands

## Validation status

What passed:

- `git diff --check`
- `dotnet restore Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal -p:UseChummerEngineContractsLocalFeed=false`
- `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal --no-restore -p:UseChummerEngineContractsLocalFeed=false`
- pushed commit `228d0d0f` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`

What still needs direct verification:

- rerun the targeted command availability test now that the csproj actually compiles it on non-Windows
- inspect the live desktop shell command surface after first launch to confirm the enabled commands actually render as expected
- continue with the next parity jump after this guardrail: menu behavior, icon/signing path, and any remaining non-classic surfaces that still survive first launch

What failed:

- `dotnet test ... --filter CommandAvailabilityEvaluatorTests`
  - the filter returned no matches because `Chummer.Tests.csproj` disables default compile items on non-Windows and the new test file was not in the explicit compile list

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Chummer.Tests.Presentation.CommandAvailabilityEvaluatorTests"
sed -n '1,220p' Chummer.Blazor/Components/Shell/MenuBar.razor
sed -n '1,220p' Chummer.Blazor/Components/Shell/ToolStrip.razor
sed -n '1,220p' Chummer.Presentation/Shell/CommandAvailabilityEvaluator.cs
```

Commit only the three files above:

```bash
git add Chummer.Tests/Chummer.Tests.csproj
git add Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Lock startup desktop command availability"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Commit/push this command-availability guardrail.
2. Verify the first-launch menubar/toolstrip on the live desktop head instead of only at component level.
3. Fix any command surface that still feels dead on first launch even though startup-safe commands should be enabled.
4. Run the screenshot-level comparison against Chummer5a and list only remaining intentional diffs.
5. Continue with icon/signing and release-train correctness so mac builds stop lagging or surfacing stale-feeling snapshots.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Tests/Chummer.Tests.csproj Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check
dotnet test Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --filter "FullyQualifiedName~Chummer.Tests.Presentation.CommandAvailabilityEvaluatorTests"
git add Chummer.Tests/Chummer.Tests.csproj Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Lock startup desktop command availability"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

Then continue immediately with the next parity pass: live menu/toolstrip behavior, screenshot audit, icon/signing fixes, and any remaining first-paint drift from Chummer5a.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
- The current highest-risk visible regressions after the pushed density slice are whatever still makes first launch feel dead or unfamiliar: especially menu behavior, icon correctness, and any remaining dashboard feel on first paint.
