#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_DELEGATE_COMMAND_ROUTE_RECEIPT_PATH:-$repo_root/.codex-studio/published/DELEGATE_COMMAND_ROUTE_PARITY.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

EXPECTED_CONTRACT_METHODS = [
    "InitializeAsync",
    "ImportAsync",
    "LoadAsync",
    "SwitchWorkspaceAsync",
    "CloseWorkspaceAsync",
    "ExecuteCommandAsync",
    "HandleUiControlAsync",
    "ExecuteWorkspaceActionAsync",
    "UpdateDialogFieldAsync",
    "ExecuteDialogActionAsync",
    "CloseDialogAsync",
    "SelectTabAsync",
    "UpdateMetadataAsync",
    "SaveAsync",
]

BRIDGE_EVENT_TESTS = [
    "State_change_publishes_new_snapshot_to_callback",
    "Dispose_unsubscribes_from_presenter_events",
]

ADAPTER_EVENT_TESTS = [
    "Updated_event_is_raised_when_presenter_state_changes",
    "Dispose_unsubscribes_from_presenter_events",
]

PRESENTER_ROUTE_MARKERS = {
    "command_dispatcher": "_commandDispatcher.DispatchAsync(commandId, context, ct);",
    "dialog_coordinator": "_dialogCoordinator.CoordinateAsync(actionId, context, ct);",
    "ui_control_dialog_factory": "ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, State.Preferences)",
    "dynamic_dialog_rebuild": "updatedDialog = DesktopDialogFactory.RebuildDynamicDialog(updatedDialog, State.Preferences);",
    "workspace_command_relay": "await ExecuteCommandAsync(action.TargetId, ct);",
    "select_tab_default_action_relay": "await ExecuteWorkspaceActionAsync(defaultAction, ct);",
}

PRESENTER_TEST_MARKERS = [
    "ExecuteCommandAsync_all_catalog_commands_are_handled",
    "HandleUiControlAsync_all_catalog_controls_are_non_generic",
    "ExecuteDialogActionAsync_import_imports_workspace_from_open_character_dialog",
    "ExecuteDialogActionAsync_apply_ruleset_calls_shell_presenter_and_closes_dialog",
    "ExecuteWorkspaceActionAsync_summary_sets_active_summary_payload",
    "ExecuteWorkspaceActionAsync_metadata_applies_profile_updates_from_dialog",
    "ExecuteDialogActionAsync_roll_updates_dice_dialog_result_field",
    "ExecuteDialogActionAsync_save_global_settings_updates_preferences",
    "SelectTabAsync_loads_active_section_preview_after_workspace_load",
    "SwitchWorkspaceAsync_restores_workspace_specific_tab_and_section_context",
    "CloseWorkspaceAsync_closes_active_workspace_and_switches_to_recent_workspace",
]

DIALOG_COORDINATOR_TEST_MARKERS = [
    "CoordinateAsync_save_global_settings_updates_preferences_and_closes_dialog",
    "CoordinateAsync_apply_metadata_calls_update_delegate_and_closes_dialog_on_success",
    "CoordinateAsync_roll_adds_result_field_to_dice_dialog",
    "CoordinateAsync_derive_initiative_updates_preview_without_closing_dialog",
    "CoordinateAsync_import_imports_workspace_and_closes_dialog_on_success",
    "CoordinateAsync_apply_ruleset_calls_delegate_and_closes_dialog_on_success",
    "CoordinateAsync_add_more_gear_keeps_dialog_open_and_rebuilds_preview",
    "CoordinateAsync_add_more_gear_uses_sr4_authored_notice_when_state_is_sr4",
    "CoordinateAsync_add_matrix_program_uses_sr6_authored_notice_when_state_is_sr6",
    "CoordinateAsync_open_runner_character_roster_switches_workspace_and_closes_dialog",
    "CoordinateAsync_switch_file_master_index_cycles_active_file_and_rebuilds_dialog",
]

DUAL_HEAD_TEST_MARKERS = [
    "Avalonia_and_Blazor_command_dispatch_save_character_matches",
    "Avalonia_and_Blazor_command_dialog_dispatch_matches",
    "Avalonia_and_Blazor_dialog_field_updates_match",
    "Avalonia_and_Blazor_dialog_workflow_keeps_shell_regions_in_parity",
    "Avalonia_and_Blazor_workspace_action_summary_matches",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_non_dialog_shared_commands_preserve_matching_state_transitions",
    "Avalonia_and_Blazor_shell_surfaces_expose_identical_ids",
]

BRIDGE_FORWARDER_MARKERS = {
    "InitializeAsync": "return _presenter.InitializeAsync(ct);",
    "ImportAsync": "return _presenter.ImportAsync(",
    "LoadAsync": "return _presenter.LoadAsync(workspaceId, ct);",
    "SwitchWorkspaceAsync": "return _presenter.SwitchWorkspaceAsync(workspaceId, ct);",
    "CloseWorkspaceAsync": "return _presenter.CloseWorkspaceAsync(workspaceId, ct);",
    "ExecuteCommandAsync": "return _presenter.ExecuteCommandAsync(commandId, ct);",
    "HandleUiControlAsync": "return _presenter.HandleUiControlAsync(controlId, ct);",
    "ExecuteWorkspaceActionAsync": "return _presenter.ExecuteWorkspaceActionAsync(action, ct);",
    "UpdateDialogFieldAsync": "return _presenter.UpdateDialogFieldAsync(fieldId, value, ct);",
    "ExecuteDialogActionAsync": "return _presenter.ExecuteDialogActionAsync(actionId, ct);",
    "CloseDialogAsync": "return _presenter.CloseDialogAsync(ct);",
    "SelectTabAsync": "return _presenter.SelectTabAsync(tabId, ct);",
    "UpdateMetadataAsync": "return _presenter.UpdateMetadataAsync(command, ct);",
    "SaveAsync": "return _presenter.SaveAsync(ct);",
}

ADAPTER_FORWARDER_MARKERS = dict(BRIDGE_FORWARDER_MARKERS)

TEST_FILTER_COMMANDS = [
    "Name~delegates_to_presenter",
    "Name~State_change_publishes_new_snapshot_to_callback",
    "Name~Updated_event_is_raised_when_presenter_state_changes",
    "Name~Dispose_unsubscribes_from_presenter_events",
    "Name~CoordinateAsync_",
    "Name~ExecuteCommandAsync_all_catalog_commands_are_handled",
    "Name~HandleUiControlAsync_all_catalog_controls_are_non_generic",
    "Name~ExecuteDialogActionAsync_",
    "Name~ExecuteWorkspaceActionAsync_",
    "Name~SelectTabAsync_loads_active_section_preview_after_workspace_load",
    "Name~SwitchWorkspaceAsync_restores_workspace_specific_tab_and_section_context",
    "Name~CloseWorkspaceAsync_closes_active_workspace_and_switches_to_recent_workspace",
    "Name~Avalonia_and_Blazor_",
]

PATHS = {
    "contract": repo_root / "Chummer.Presentation/Overview/ICharacterOverviewPresenter.cs",
    "bridge_source": repo_root / "Chummer.Blazor/CharacterOverviewStateBridge.cs",
    "adapter_source": repo_root / "Chummer.Avalonia/CharacterOverviewViewModelAdapter.cs",
    "bridge_tests": repo_root / "Chummer.Tests/Presentation/CharacterOverviewStateBridgeTests.cs",
    "adapter_tests": repo_root / "Chummer.Tests/Presentation/CharacterOverviewViewModelAdapterTests.cs",
    "presenter_commands": repo_root / "Chummer.Presentation/Overview/CharacterOverviewPresenter.Commands.cs",
    "presenter_dialogs": repo_root / "Chummer.Presentation/Overview/CharacterOverviewPresenter.Dialogs.cs",
    "presenter_tests": repo_root / "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
    "dialog_coordinator_tests": repo_root / "Chummer.Tests/Presentation/DialogCoordinatorTests.cs",
    "dual_head_tests": repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    "verify_script": repo_root / "scripts/ai/verify.sh",
    "compliance_tests": repo_root / "Chummer.Tests/Compliance/DelegateCommandRouteParityComplianceTests.cs",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def extract_task_methods(text: str) -> list[str]:
    return re.findall(r"\bTask\s+([A-Za-z0-9_]+)\s*\(", text)


def extract_public_task_methods(text: str) -> list[str]:
    return re.findall(r"\bpublic\s+Task\s+([A-Za-z0-9_]+)\s*\(", text)


def missing_markers(text: str, markers: list[str]) -> list[str]:
    return [marker for marker in markers if marker not in text]


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.delegate_command_route_parity",
    "status": "fail",
    "summary": "Delegate and command-route parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "sourcePaths": {name: str(path.relative_to(repo_root)) for name, path in PATHS.items()},
        "expectedContractMethods": EXPECTED_CONTRACT_METHODS,
        "bridgeEventTests": BRIDGE_EVENT_TESTS,
        "adapterEventTests": ADAPTER_EVENT_TESTS,
        "presenterRouteMarkers": {},
        "presenterRouteTests": {},
        "dialogCoordinatorTests": {},
        "dualHeadParityTests": {},
        "bridgeForwarders": {},
        "adapterForwarders": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence = payload["evidence"]
contract_surface_reasons: list[str] = []
bridge_adapter_reasons: list[str] = []
lifecycle_event_reasons: list[str] = []
presenter_route_reasons: list[str] = []
dual_head_reasons: list[str] = []
verify_wiring_reasons: list[str] = []
execution_reasons: list[str] = []


def append_reason(message: str, bucket: list[str]) -> None:
    reasons.append(message)
    bucket.append(message)

missing_files = [str(path.relative_to(repo_root)) for path in PATHS.values() if not path.is_file()]
evidence["missingFiles"] = missing_files
if missing_files:
    append_reason("Required delegate-route proof files are missing.", contract_surface_reasons)

texts = {name: read_text(path) for name, path in PATHS.items() if path.is_file()}

contract_methods = extract_task_methods(texts.get("contract", ""))
evidence["contractMethodsFound"] = contract_methods
evidence["contractMethodCount"] = len(contract_methods)
if contract_methods != EXPECTED_CONTRACT_METHODS:
    append_reason(
        "ICharacterOverviewPresenter task surface drifted from the delegate-route parity contract.",
        contract_surface_reasons,
    )

bridge_methods = extract_public_task_methods(texts.get("bridge_source", ""))
adapter_methods = extract_public_task_methods(texts.get("adapter_source", ""))
evidence["bridgeMethodCount"] = len(bridge_methods)
evidence["adapterMethodCount"] = len(adapter_methods)
evidence["bridgeMethodsFound"] = bridge_methods
evidence["adapterMethodsFound"] = adapter_methods

for label, methods in (("bridge", bridge_methods), ("adapter", adapter_methods)):
    missing = [method for method in EXPECTED_CONTRACT_METHODS if method not in methods]
    extra = [method for method in methods if method not in EXPECTED_CONTRACT_METHODS]
    evidence[f"{label}MissingMethods"] = missing
    evidence[f"{label}ExtraMethods"] = extra
    if missing:
        append_reason(
            f"{label.capitalize()} is missing presenter delegate methods: {', '.join(missing)}.",
            bridge_adapter_reasons,
        )
    if extra:
        append_reason(
            f"{label.capitalize()} exposes unexpected task delegate methods: {', '.join(extra)}.",
            bridge_adapter_reasons,
        )

for method, marker in BRIDGE_FORWARDER_MARKERS.items():
    found = marker in texts.get("bridge_source", "")
    evidence["bridgeForwarders"][method] = {
        "forwarderMarker": marker,
        "forwarderFound": found,
        "testFound": f"{method}_delegates_to_presenter" in texts.get("bridge_tests", ""),
    }
    if not found:
        append_reason(f"Blazor bridge forwarder marker missing for {method}.", bridge_adapter_reasons)
    if not evidence["bridgeForwarders"][method]["testFound"]:
        append_reason(f"Blazor bridge delegate test missing for {method}.", bridge_adapter_reasons)

for method, marker in ADAPTER_FORWARDER_MARKERS.items():
    found = marker in texts.get("adapter_source", "")
    evidence["adapterForwarders"][method] = {
        "forwarderMarker": marker,
        "forwarderFound": found,
        "testFound": f"{method}_delegates_to_presenter" in texts.get("adapter_tests", ""),
    }
    if not found:
        append_reason(f"Avalonia adapter forwarder marker missing for {method}.", bridge_adapter_reasons)
    if not evidence["adapterForwarders"][method]["testFound"]:
        append_reason(f"Avalonia adapter delegate test missing for {method}.", bridge_adapter_reasons)

bridge_event_missing = missing_markers(texts.get("bridge_tests", ""), BRIDGE_EVENT_TESTS)
adapter_event_missing = missing_markers(texts.get("adapter_tests", ""), ADAPTER_EVENT_TESTS)
evidence["bridgeLifecycleEventMissing"] = bridge_event_missing
evidence["adapterLifecycleEventMissing"] = adapter_event_missing
if bridge_event_missing:
    append_reason("Blazor bridge lifecycle event tests are incomplete.", lifecycle_event_reasons)
if adapter_event_missing:
    append_reason("Avalonia adapter lifecycle event tests are incomplete.", lifecycle_event_reasons)

presenter_source_text = "\n".join(
    [texts.get("presenter_commands", ""), texts.get("presenter_dialogs", "")]
)
for name, marker in PRESENTER_ROUTE_MARKERS.items():
    found = marker in presenter_source_text
    evidence["presenterRouteMarkers"][name] = {
        "marker": marker,
        "found": found,
    }
    if not found:
        append_reason(f"Presenter route marker missing: {name}.", presenter_route_reasons)

for marker in PRESENTER_TEST_MARKERS:
    found = marker in texts.get("presenter_tests", "")
    evidence["presenterRouteTests"][marker] = found
    if not found:
        append_reason(f"Presenter route test marker missing: {marker}.", presenter_route_reasons)

for marker in DIALOG_COORDINATOR_TEST_MARKERS:
    found = marker in texts.get("dialog_coordinator_tests", "")
    evidence["dialogCoordinatorTests"][marker] = found
    if not found:
        append_reason(f"Dialog coordinator test marker missing: {marker}.", presenter_route_reasons)

for marker in DUAL_HEAD_TEST_MARKERS:
    found = marker in texts.get("dual_head_tests", "")
    evidence["dualHeadParityTests"][marker] = found
    if not found:
        append_reason(f"Dual-head delegate parity test marker missing: {marker}.", dual_head_reasons)

verify_text = texts.get("verify_script", "")
verify_banner = "checking delegate and command-route parity guard"
verify_invocation = "bash scripts/ai/milestones/delegate-command-route-parity-check.sh"
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if verify_banner not in verify_text or verify_invocation not in verify_text:
    append_reason("Delegate-route parity guard is not wired into scripts/ai/verify.sh.", verify_wiring_reasons)

test_commands = [
    [
        "bash",
        "scripts/ai/test.sh",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--no-build",
        "--filter",
        filter_expression,
        "-v",
        "minimal",
    ]
    for filter_expression in TEST_FILTER_COMMANDS
]
evidence["testCommands"] = test_commands
evidence["testProject"] = "Chummer.Tests/Chummer.Tests.csproj"

build_result: subprocess.CompletedProcess[str] | None = None
test_results: list[dict[str, Any]] = []
if not reasons:
    build_command = [
        "bash",
        "scripts/ai/with-package-plane.sh",
        "build",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--nologo",
        "--verbosity",
        "quiet",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    evidence["buildCommand"] = build_command

    build_result = subprocess.run(
        build_command,
        cwd=repo_root,
        text=True,
        capture_output=True,
    )
    evidence["buildExitCode"] = build_result.returncode
    evidence["buildOutputTail"] = tail_lines((build_result.stdout or "") + "\n" + (build_result.stderr or ""))
    if build_result.returncode != 0:
        append_reason(
            f"Delegate-route parity build slice failed with exit code {build_result.returncode}.",
            execution_reasons,
        )
    else:
        for test_command in test_commands:
            test_result = subprocess.run(
                test_command,
                cwd=repo_root,
                text=True,
                capture_output=True,
            )
            combined_output = (test_result.stdout or "") + "\n" + (test_result.stderr or "")
            output_tail = tail_lines(combined_output)
            output_lower = combined_output.lower()
            no_matches = "no test matches the given testcase filter" in output_lower
            test_results.append(
                {
                    "command": test_command,
                    "exitCode": test_result.returncode,
                    "noMatches": no_matches,
                    "outputTail": output_tail,
                }
            )
            if test_result.returncode != 0:
                append_reason(
                    f"Delegate-route parity test slice failed with exit code {test_result.returncode}: {' '.join(test_command)}",
                    execution_reasons,
                )
            elif no_matches:
                append_reason(
                    f"Delegate-route parity test slice matched zero tests: {' '.join(test_command)}",
                    execution_reasons,
                )
        evidence["testResults"] = test_results
else:
    evidence["buildExitCode"] = None
    evidence["testResults"] = test_results

payload["reviews"] = {
    "contractSurfaceReview": {
        "status": "pass" if not contract_surface_reasons else "fail",
        "reasons": contract_surface_reasons,
    },
    "bridgeAdapterReview": {
        "status": "pass" if not bridge_adapter_reasons else "fail",
        "reasons": bridge_adapter_reasons,
    },
    "lifecycleEventReview": {
        "status": "pass" if not lifecycle_event_reasons else "fail",
        "reasons": lifecycle_event_reasons,
    },
    "presenterRouteReview": {
        "status": "pass" if not presenter_route_reasons else "fail",
        "reasons": presenter_route_reasons,
    },
    "dualHeadParityReview": {
        "status": "pass" if not dual_head_reasons else "fail",
        "reasons": dual_head_reasons,
    },
    "verifyWiringReview": {
        "status": "pass" if not verify_wiring_reasons else "fail",
        "reasons": verify_wiring_reasons,
    },
    "executionReview": {
        "status": "pass" if not execution_reasons else "fail",
        "reasons": execution_reasons,
    },
}

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = "Delegate and command-route parity is fail-closing, test-executed, and wired into standard verification."

evidence["reasonCount"] = len(reasons)
evidence["failureCount"] = len(reasons)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if payload["status"] != "pass":
    raise SystemExit(47)

print("[delegate-route] PASS: delegate and command-route parity is wired, covered, and executable.")
print(f"[delegate-route] evidence: {receipt_path}")
PY
