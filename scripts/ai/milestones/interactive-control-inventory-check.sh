#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_INTERACTIVE_CONTROL_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

STANDALONE_TEST_MARKERS = [
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_tab_buttons_raise_expected_events",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
]

MAIN_WINDOW_TEST_MARKERS = [
    "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
    "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
]

STANDALONE_FILTER = (
    "Name~Standalone_toolstrip_buttons_raise_expected_events"
    "|Name~Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events"
    "|Name~Standalone_workspace_strip_quick_start_button_raises_expected_event"
    "|Name~Standalone_summary_header_tab_buttons_raise_expected_events"
    "|Name~Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events"
    "|Name~Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions"
    "|Name~Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available"
)

MAIN_WINDOW_FILTER = (
    "Name~Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters"
    "|Name~Workspace_strip_quick_start_hides_after_runtime_backed_runner_load"
    "|Name~Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end"
)

STANDALONE_SOURCE_MARKERS = {
    "toolstrip_axaml": [
        "SaveButton",
        "ImportFileButton",
        "DesktopHomeButton",
        "SettingsButton",
    ],
    "toolstrip_codebehind": [
        "SaveRequested?.Invoke(this, EventArgs.Empty);",
        "ImportFileRequested?.Invoke(this, EventArgs.Empty);",
        "DesktopHomeRequested?.Invoke(this, EventArgs.Empty);",
        "SettingsRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "shell_menu_axaml": [
        "FileMenuButton",
        "EditMenuButton",
        "SpecialMenuButton",
        "ToolsMenuButton",
        "WindowsMenuButton",
        "HelpMenuButton",
    ],
    "shell_menu_codebehind": [
        "MenuSelected?.Invoke(this, menuId);",
        "MenuCommandSelected?.Invoke(this, commandId);",
    ],
    "workspace_strip_axaml": [
        "QuickStartContainer",
        "LoadDemoRunnerQuickActionButton",
    ],
    "workspace_strip_codebehind": [
        "QuickStartContainer.IsVisible = isVisible;",
        "LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "summary_header_axaml": [
        "LoadedRunnerTabStripBorder",
        "LoadedRunnerTabStrip",
    ],
    "summary_header_codebehind": [
        "LoadedRunnerTabStrip_OnSelectionChanged",
        "NavigationTabSelected?.Invoke(this, tab.Id);",
    ],
    "navigator_axaml": [
        "NavigatorTree",
        "NavigatorTree_OnSelectionChanged",
    ],
    "navigator_codebehind": [
        "WorkspaceSelected?.Invoke(this, item.Id);",
        "NavigationTabSelected?.Invoke(this, item.Id);",
        "SectionActionSelected?.Invoke(this, item.Id);",
        "WorkflowSurfaceSelected?.Invoke(this, item.Id);",
    ],
    "command_dialog_axaml": [
        "DialogTitleText",
        "DialogFieldsHost",
        "DialogActionsHost",
        "CommandsList",
    ],
    "command_dialog_codebehind": [
        "CommandSelected?.Invoke(this, command.Id);",
        "DialogActionSelected?.Invoke(this, actionId);",
        "DialogFieldValueChanged?.Invoke(",
    ],
    "coach_sidecar_axaml": [
        "CopyCoachLaunchButton",
    ],
    "coach_sidecar_codebehind": [
        "CopyLaunchRequested?.Invoke(this, EventArgs.Empty);",
    ],
}

MAIN_WINDOW_SOURCE_MARKERS = {
    "main_window_axaml": [
        "ShellMenuBarControl",
        "ToolStripControl",
        "SummaryHeaderControl",
        "NavigatorPaneControl",
        "SectionHostControl",
        "CommandDialogPaneControl",
        "CoachSidecarControl",
    ],
    "main_window_codebehind": [
        "onSectionQuickActionRequested: SectionHost_OnQuickActionRequested",
        "onCommandSelected: CommandDialogPane_OnCommandSelected",
        "onMenuCommandSelected: MenuBar_OnMenuCommandSelected",
    ],
    "main_window_selection_handlers": [
        "NavigatorPane_OnWorkspaceSelected",
        "NavigatorPane_OnNavigationTabSelected",
        "NavigatorPane_OnSectionActionSelected",
        "NavigatorPane_OnWorkflowSurfaceSelected",
        "SectionHost_OnQuickActionRequested",
        "CommandDialogPane_OnDialogActionSelected",
        "CommandDialogPane_OnDialogFieldValueChanged",
        "MenuBar_OnMenuCommandSelected",
    ],
}

PATHS = {
    "avalonia_gate_tests": repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "toolstrip_axaml": repo_root / "Chummer.Avalonia/Controls/ToolStripControl.axaml",
    "toolstrip_codebehind": repo_root / "Chummer.Avalonia/Controls/ToolStripControl.axaml.cs",
    "shell_menu_axaml": repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml",
    "shell_menu_codebehind": repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml.cs",
    "workspace_strip_axaml": repo_root / "Chummer.Avalonia/Controls/WorkspaceStripControl.axaml",
    "workspace_strip_codebehind": repo_root / "Chummer.Avalonia/Controls/WorkspaceStripControl.axaml.cs",
    "summary_header_axaml": repo_root / "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml",
    "summary_header_codebehind": repo_root / "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs",
    "navigator_axaml": repo_root / "Chummer.Avalonia/Controls/NavigatorPaneControl.axaml",
    "navigator_codebehind": repo_root / "Chummer.Avalonia/Controls/NavigatorPaneControl.axaml.cs",
    "command_dialog_axaml": repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml",
    "command_dialog_codebehind": repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs",
    "coach_sidecar_axaml": repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml",
    "coach_sidecar_codebehind": repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml.cs",
    "main_window_axaml": repo_root / "Chummer.Avalonia/MainWindow.axaml",
    "main_window_codebehind": repo_root / "Chummer.Avalonia/MainWindow.axaml.cs",
    "main_window_selection_handlers": repo_root / "Chummer.Avalonia/MainWindow.SelectionHandlers.cs",
    "verify_script": repo_root / "scripts/ai/verify.sh",
    "b14_script": repo_root / "scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    "delegate_route_receipt": repo_root / ".codex-studio/published/DELEGATE_COMMAND_ROUTE_PARITY.generated.json",
    "generated_dialog_receipt": repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
    "section_host_ruleset_receipt": repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def status_ok(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.interactive_control_inventory",
    "status": "fail",
    "summary": "Standalone interactive control and main-window interaction inventory proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "sourcePaths": {name: str(path.relative_to(repo_root)) for name, path in PATHS.items()},
        "standaloneControlTests": {},
        "mainWindowTests": {},
        "sourceMarkers": {},
        "dependencyReceipts": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence = payload["evidence"]
standalone_failures: list[str] = []
main_window_failures: list[str] = []
shared_failures: list[str] = []
b14_failures: list[str] = []
verify_wiring_failures: list[str] = []
execution_failures: list[str] = []


def add_failure(message: str, bucket: list[str]) -> None:
    bucket.append(message)
    reasons.append(message)


missing_files = [str(path.relative_to(repo_root)) for path in PATHS.values() if not path.is_file()]
evidence["missingFiles"] = missing_files
if missing_files:
    add_failure("Required interactive-control inventory proof files are missing.", shared_failures)

texts = {name: read_text(path) for name, path in PATHS.items() if path.is_file()}
avalonia_gate_tests_text = texts.get("avalonia_gate_tests", "")

for marker in STANDALONE_TEST_MARKERS:
    found = marker in avalonia_gate_tests_text
    evidence["standaloneControlTests"][marker] = found
    if not found:
        add_failure(f"Standalone interactive control test marker missing: {marker}.", standalone_failures)

for marker in MAIN_WINDOW_TEST_MARKERS:
    found = marker in avalonia_gate_tests_text
    evidence["mainWindowTests"][marker] = found
    if not found:
        add_failure(f"Main-window interaction test marker missing: {marker}.", main_window_failures)


def collect_source_markers(
    expected_markers: dict[str, list[str]],
    failure_bucket: list[str],
    category_label: str,
) -> None:
    for path_key, markers in expected_markers.items():
        source_text = texts.get(path_key, "")
        source_results = evidence["sourceMarkers"].setdefault(path_key, {})
        for marker in markers:
            found = marker in source_text
            source_results[marker] = found
            if not found:
                add_failure(f"{category_label} source marker missing in {path_key}: {marker}.", failure_bucket)


collect_source_markers(STANDALONE_SOURCE_MARKERS, standalone_failures, "Standalone interactive control")
collect_source_markers(MAIN_WINDOW_SOURCE_MARKERS, main_window_failures, "Main-window interaction")

dependency_requirements = {
    "delegateCommandRouteParity": "delegate_route_receipt",
    "generatedDialogElementParity": "generated_dialog_receipt",
    "sectionHostRulesetParity": "section_host_ruleset_receipt",
}
for label, path_key in dependency_requirements.items():
    receipt_text = texts.get(path_key, "")
    status = None
    contract_name = None
    if receipt_text:
        receipt = json.loads(receipt_text)
        status = str(receipt.get("status") or "").strip().lower()
        contract_name = receipt.get("contract_name")
    evidence["dependencyReceipts"][label] = {
        "path": str(PATHS[path_key].relative_to(repo_root)),
        "status": status,
        "contractName": contract_name,
    }
    if not status_ok(status):
        add_failure(f"Dependency receipt is not passing: {label}.", shared_failures)

verify_text = texts.get("verify_script", "")
verify_banner = "checking standalone interactive control inventory guard"
verify_invocation = "bash scripts/ai/milestones/interactive-control-inventory-check.sh"
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if verify_banner not in verify_text or verify_invocation not in verify_text:
    add_failure(
        "Interactive control inventory guard is not wired into scripts/ai/verify.sh.",
        shared_failures,
        verify_wiring_failures,
    )

b14_text = texts.get("b14_script", "")
required_b14_markers = [
    'interactive_control_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"',
    'with open(interactive_control_inventory_receipt_path, "r", encoding="utf-8") as handle:',
    'interactive_control_inventory_receipt = json.load(handle)',
    'full_interactive_control_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("fullInteractiveControlInventory") or "").strip().lower()',
    'main_window_interaction_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("mainWindowInteractionInventory") or "").strip().lower()',
    '"interactiveControlInventoryReceiptPath": interactive_control_inventory_receipt_path,',
    '"fullInteractiveControlInventory": full_interactive_control_inventory_status,',
    '"mainWindowInteractionInventory": main_window_interaction_inventory_status,',
]
evidence["b14ReleaseGateMarkers"] = {}
for marker in required_b14_markers:
    found = marker in b14_text
    evidence["b14ReleaseGateMarkers"][marker] = found
    if not found:
        add_failure(
            f"B14 release gate does not consume the interactive inventory receipt marker: {marker}.",
            shared_failures,
            b14_failures,
        )

b14_hardcoded_markers = [
    '"fullInteractiveControlInventory": "pass"',
    '"mainWindowInteractionInventory": "pass"',
]
present_hardcoded_markers = [marker for marker in b14_hardcoded_markers if marker in b14_text]
evidence["b14HardcodedInventoryMarkers"] = present_hardcoded_markers
evidence["b14UsesReceipt"] = not present_hardcoded_markers and all(evidence["b14ReleaseGateMarkers"].values())
if present_hardcoded_markers:
    add_failure(
        "B14 release gate still hardcodes interactive inventory proof instead of consuming the standalone receipt.",
        shared_failures,
        b14_failures,
    )

test_filters = {
    "fullInteractiveControlInventory": STANDALONE_FILTER,
    "mainWindowInteractionInventory": MAIN_WINDOW_FILTER,
}
test_commands = {
    name: [
        "bash",
        "scripts/ai/test.sh",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--no-build",
        "--filter",
        filter_expression,
        "-v",
        "minimal",
    ]
    for name, filter_expression in test_filters.items()
}
evidence["testCommands"] = test_commands
evidence["testProject"] = "Chummer.Tests/Chummer.Tests.csproj"

build_result: subprocess.CompletedProcess[str] | None = None
test_results: dict[str, Any] = {}
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
        add_failure(
            f"Interactive control inventory build slice failed with exit code {build_result.returncode}.",
            shared_failures,
            execution_failures,
        )
    else:
        for name, test_command in test_commands.items():
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
            test_results[name] = {
                "command": test_command,
                "exitCode": test_result.returncode,
                "noMatches": no_matches,
                "outputTail": output_tail,
            }
            if test_result.returncode != 0:
                bucket = standalone_failures if name == "fullInteractiveControlInventory" else main_window_failures
                add_failure(
                    f"Interactive control inventory test slice failed with exit code {test_result.returncode}: {' '.join(test_command)}",
                    bucket,
                    execution_failures,
                )
            elif no_matches:
                bucket = standalone_failures if name == "fullInteractiveControlInventory" else main_window_failures
                add_failure(
                    f"Interactive control inventory test slice matched zero tests: {' '.join(test_command)}",
                    bucket,
                    execution_failures,
                )
        evidence["testResults"] = test_results
else:
    evidence["buildExitCode"] = None
    evidence["testResults"] = test_results

full_interactive_control_inventory = "pass" if not standalone_failures and not shared_failures else "fail"
main_window_interaction_inventory = "pass" if not main_window_failures and not shared_failures else "fail"
evidence["fullInteractiveControlInventory"] = full_interactive_control_inventory
evidence["mainWindowInteractionInventory"] = main_window_interaction_inventory

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = "Standalone interactive controls and main-window interaction routes are inventoried, executable, and fail-closing."

payload["sourceArtifactReview"] = {
    "status": "pass" if not evidence["missingFiles"] else "fail",
    "summary": (
        "Interactive-control inventory source files are present."
        if not evidence["missingFiles"]
        else "Interactive-control inventory source files are missing."
    ),
    "reasons": [
        reason
        for reason in shared_failures
        if reason == "Required interactive-control inventory proof files are missing."
    ],
    "missingFiles": evidence["missingFiles"],
}
payload["standaloneControlReview"] = {
    "status": "pass" if not standalone_failures and not evidence["missingFiles"] else "fail",
    "summary": (
        "Standalone interactive control surfaces and tests are pinned."
        if not standalone_failures and not shared_failures
        else "Standalone interactive control surfaces or tests are missing proof."
    ),
    "reasons": standalone_failures,
    "tests": evidence["standaloneControlTests"],
}
payload["mainWindowInteractionReview"] = {
    "status": "pass" if not main_window_failures and not evidence["missingFiles"] else "fail",
    "summary": (
        "Main-window interaction routes and tests are pinned."
        if not main_window_failures and not shared_failures
        else "Main-window interaction routes or tests are missing proof."
    ),
    "reasons": main_window_failures,
    "tests": evidence["mainWindowTests"],
}
payload["dependencyReceiptReview"] = {
    "status": "pass" if all(status_ok(item.get("status")) for item in evidence["dependencyReceipts"].values()) else "fail",
    "summary": (
        "Delegate, generated-dialog, and section-host dependency receipts are present and passing."
        if all(status_ok(item.get("status")) for item in evidence["dependencyReceipts"].values())
        else "One or more delegate, generated-dialog, or section-host dependency receipts are missing or failing."
    ),
    "reasons": [
        reason
        for reason in shared_failures
        if reason.startswith("Dependency receipt is not passing:")
    ],
    "dependencyReceipts": evidence["dependencyReceipts"],
}
payload["verifyWiringReview"] = {
    "status": "pass" if not verify_wiring_failures else "fail",
    "summary": (
        "Interactive-control inventory guard is wired into the standard verify path."
        if not verify_wiring_failures
        else "Interactive-control inventory guard is not wired into the standard verify path."
    ),
    "reasons": verify_wiring_failures,
    "wiredIntoStandardVerify": evidence["wiredIntoStandardVerify"],
    "verifyMarker": verify_banner,
    "verifyInvocation": verify_invocation,
}
payload["b14ConsumptionReview"] = {
    "status": "pass" if not b14_failures else "fail",
    "summary": (
        "B14 consumes the standalone interactive-control inventory receipt."
        if not b14_failures
        else "B14 still misses or hardcodes interactive-control inventory proof."
    ),
    "reasons": b14_failures,
    "releaseGateMarkers": evidence["b14ReleaseGateMarkers"],
    "hardcodedMarkers": evidence["b14HardcodedInventoryMarkers"],
    "b14UsesReceipt": evidence["b14UsesReceipt"],
}
payload["executionReview"] = {
    "status": "pass" if not execution_failures else "fail",
    "summary": (
        "Interactive-control inventory build and test slices executed cleanly."
        if not execution_failures
        else "Interactive-control inventory build or test slices failed."
    ),
    "reasons": execution_failures,
    "buildExitCode": evidence["buildExitCode"],
    "testResults": evidence["testResults"],
}
evidence["failureCount"] = len(reasons)
evidence["reasonCount"] = len(reasons)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if payload["status"] != "pass":
    raise SystemExit(47)

print("[interactive-control-inventory] PASS: standalone control inventory and main-window interaction routes are executable and fail-closing.")
print(f"[interactive-control-inventory] evidence: {receipt_path}")
PY
