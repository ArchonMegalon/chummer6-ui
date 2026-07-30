# Workbench Session Handoff
Last updated: 2026-07-09T13:16:36+02:00

## Cross-Codex Sync (2026-07-09T13:16:36+02:00)

- Shared blocker truth remains controlled by the release/controller lane:
  - `release_truth:public_edge_postdeploy_gate` is still the current root gate.
- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching publish-lane, route-proof, or blocker-receipt work from this repo.
- Imported status from `/docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean`:
  - focused parity slice remains green: `17 passed` for the four-test fast proof set.
  - full generated-dialog parity receipt remains stale (`status: fail`) and blocked by shared package-plane contention.
  - lock contention remains with `.linux-desktop-exit-gate-source.7Xkx0X` holding `.tmp/ai/with-package-plane.lock`.
- Next action for this repo:
  - rerun `bash scripts/ai/milestones/generated-dialog-element-parity-check.sh` once build contention clears, then re-sync publish-route evidence against the refreshed receipt.
- Hard stop:
  - do not claim stable/flagship-ready/merge-ready while the external root gate or stale parity evidence persists.

## Cross-Codex Refresh (2026-07-09T12:23:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains controlled by the release/controller lane.
- `release_truth:public_edge_postdeploy_gate` is still the active root gate; this repo is not the blocker-clearing path.
- Imported status from `/docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean`:
  - focused parity slice remains green:
    - `python3 -m pytest -q tests/test_generated_dialog_parity_timeout_contract.py tests/test_with_package_plane_bootstrap_cache.py tests/test_desktop_shell_dialog_chrome_check_contract.py tests/test_blazor_portal_route_probe_contract.py` -> `17 passed`
  - desktop-shell focused parity checks are passing on repaired filters.
  - full generated-dialog parity receipt still needs a fresh rerun because shared `Chummer.Tests` compile contention remains.
- Next action for this repo:
  - sync any publish-recipe or release-route work against the refreshed parity evidence before declaring merge-ready here.
- Hard stop:
  - do not claim stable/flagship-ready/merge-ready from this repo while external root blockers or stale parity evidence remain.

## Cross-Codex Refresh (2026-07-09T12:23:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is `Cross-Lane Sync (2026-07-09T12:23:54+02:00)`. Treat its blocker set as current release truth:
  - `release_truth:public_edge_postdeploy_gate` remains the active root gate; this repo is not the blocker-clearing path.
- Current release-script hardening work in this repo is complete on this pass:
  - added `scripts/verify-release-channel-is-authoritative-or-fixture.py` and wired it into `scripts/ai/verify.sh` for fixture/public manifest authority checks.
  - added `tests/test_verify_release_channel_authority.py` and expanded `tests/test_verified_release_channel_mirror.py` to include `Docker/Downloads` and startup-smoke file projection paths.
  - updated `scripts/materialize-verified-release-channel-mirror.py` to mirror portal `files/` trees and startup manifests along with release-manifest scope.
- Verification completed for this hardening slice:
  - `python3 -m pytest -q tests/test_verify_release_channel_authority.py tests/test_verified_release_channel_mirror.py` -> `11 passed`
  - `python3 -m pytest -q tests/test_startup_smoke_bash_portability.py tests/test_ai_test_runner_portability.py tests/test_audit_compliance_script.py tests/test_desktop_release_matrix_gate.py tests/test_public_windows_payload_metadata.py tests/test_windows_bootstrap_payload_gate_support.py tests/test_windows_installer_payload_gate.py tests/test_windows_installer_update_handoff_gate.py tests/test_desktop_downloads_local_release_policy.py tests/test_downloads_publication_scope.py tests/test_verified_release_channel_mirror.py tests/test_verify_release_channel_authority.py` -> `161 passed`
- Current repo design/release-slice state remains constrained:
  - `chummer-presentation-sr6-origin-dialog-clean/.codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json` is still `status: fail` and this lane is not yet merge-ready from parity perspective.

## Scope

Drive Chummer6 desktop toward hard Chummer5a-style parity:

- classic menu-first shell, not a dashboard
- dense left rail and runner dossier posture
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
- `dotnet test --project Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_special_and_windows_menus_surface_real_commands|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters"`
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
   - runner dossier spacing
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
