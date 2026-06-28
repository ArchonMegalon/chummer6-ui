#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_GENERATED_DIALOG_ELEMENT_RECEIPT_PATH:-$repo_root/.codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import re
import subprocess
import sys
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

EXPECTED_COMMAND_IDS = [
    "runtime_inspector",
    "open_character",
    "open_for_printing",
    "open_for_export",
    "new_character",
    "new_character_origin",
    "print_setup",
    "dice_roller",
    "global_settings",
    "switch_ruleset",
    "character_settings",
    "translator",
    "xml_editor",
    "master_index",
    "character_roster",
    "data_exporter",
    "export_character",
    "report_bug",
    "about",
    "hero_lab_importer",
    "new_window",
    "close_window",
    "wiki",
    "discord",
    "show_login_video",
    "revision_history",
    "dumpshock",
    "print_character",
    "print_multiple",
    "update",
]

EXPECTED_CONTROL_IDS = [
    "create_entry",
    "edit_entry",
    "delete_entry",
    "open_notes",
    "identity_license_add",
    "identity_license_edit",
    "identity_license_delete",
    "move_up",
    "move_down",
    "toggle_free_paid",
    "show_source",
    "gear_add",
    "gear_edit",
    "gear_delete",
    "runner_benchmark",
    "runner_what_if",
    "runner_cohort_privacy",
    "gear_mount",
    "gear_source",
    "cyberware_add",
    "cyberware_edit",
    "cyberware_delete",
    "drug_add",
    "drug_delete",
    "magic_add",
    "magic_delete",
    "magic_bind",
    "magic_source",
    "spell_add",
    "adept_power_add",
    "complex_form_add",
    "initiation_add",
    "spirit_add",
    "critter_power_add",
    "matrix_program_add",
    "skill_add",
    "skill_specialize",
    "skill_remove",
    "skill_group",
    "combat_add_weapon",
    "combat_add_armor",
    "combat_reload",
    "combat_damage_track",
    "vehicle_add",
    "vehicle_edit",
    "vehicle_delete",
    "vehicle_mod_add",
    "contact_add",
    "contact_edit",
    "contact_remove",
    "contact_connection",
    "quality_add",
    "quality_delete",
]

EXPECTED_REBUILDABLE_DIALOG_IDS = [
    "dialog.global_settings",
    "dialog.new_character",
    "dialog.new_character.priority_workflow",
    "dialog.dice_roller",
    "dialog.character_roster",
    "dialog.master_index",
    "dialog.ui.quality_add",
    "dialog.ui.cyberware_add",
    "dialog.ui.gear_add",
    "dialog.ui.combat_add_weapon",
    "dialog.ui.combat_add_armor",
    "dialog.ui.skill_add",
    "dialog.ui.vehicle_add",
    "dialog.ui.cyberware_edit",
    "dialog.ui.gear_edit",
    "dialog.ui.vehicle_edit",
]

FACTORY_TEST_MARKERS = [
    "CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions",
    "CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions",
    "RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions",
    "BuildNewCharacterContinuationDialog_priority_route_surfaces_hidden_runtime_state_and_commit_fields",
    "RebuildDynamicDialog_priority_route_reconciles_duplicates_and_materializes_skill_choices",
]

PRESENTER_TEST_MARKERS = [
    "ExecuteCommandAsync_all_catalog_commands_are_handled",
    "HandleUiControlAsync_all_catalog_controls_are_non_generic",
]

WORKFLOW_TEST_MARKERS = [
    "Priority_workflow_duplicate_priority_selection_auto_reconciles_in_priority_mode",
    "Sum_to_ten_priority_workflow_preserves_duplicates_and_updates_live_total",
    "Priority_workflow_each_priority_letter_combobox_selection_change_records_the_changed_field",
    "Priority_workflow_category_metatype_and_metavariant_selection_changes_refresh_dependent_state",
    "Priority_workflow_talent_priority_selection_rebuilds_talent_choice_options_before_commit",
    "Priority_workflow_talent_selection_materializes_skill_choices_and_repairs_duplicates",
]

FLAGSHIP_UI_TEST_MARKERS = [
    "Standalone_priority_workflow_dialog_renders_real_combo_list_and_skill_choice_controls",
]

TEST_FILTER_COMMANDS = [
    "Name~CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions",
    "Name~CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions",
    "Name~RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions",
    "Name~ExecuteCommandAsync_all_catalog_commands_are_handled",
    "Name~HandleUiControlAsync_all_catalog_controls_are_non_generic",
    "Name~BuildNewCharacterContinuationDialog_priority_route_surfaces_hidden_runtime_state_and_commit_fields",
    "Name~RebuildDynamicDialog_priority_route_reconciles_duplicates_and_materializes_skill_choices",
    "Name~Priority_workflow_duplicate_priority_selection_auto_reconciles_in_priority_mode",
    "Name~Sum_to_ten_priority_workflow_preserves_duplicates_and_updates_live_total",
    "Name~Priority_workflow_each_priority_letter_combobox_selection_change_records_the_changed_field",
    "Name~Priority_workflow_category_metatype_and_metavariant_selection_changes_refresh_dependent_state",
    "Name~Priority_workflow_talent_priority_selection_rebuilds_talent_choice_options_before_commit",
    "Name~Priority_workflow_talent_selection_materializes_skill_choices_and_repairs_duplicates",
    "Name~Standalone_priority_workflow_dialog_renders_real_combo_list_and_skill_choice_controls",
]

PATHS = {
    "dialog_factory": repo_root / "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
    "control_catalog": repo_root / "Chummer.Presentation/Overview/LegacyUiControlCatalog.cs",
    "dialog_window": repo_root / "Chummer.Avalonia/DesktopDialogWindow.axaml.cs",
    "factory_tests": repo_root / "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs",
    "presenter_tests": repo_root / "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
    "workflow_tests": repo_root / "Chummer.Tests/Presentation/WorkflowParityGateTests.cs",
    "flagship_ui_tests": repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "verify_script": repo_root / "scripts/ai/verify.sh",
    "m103_receipt": repo_root / ".codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json",
}

BUILD_TIMEOUT_SECONDS = int(os.environ.get("CHUMMER_GENERATED_DIALOG_BUILD_TIMEOUT_SECONDS", "1800"))
TEST_TIMEOUT_SECONDS = int(os.environ.get("CHUMMER_GENERATED_DIALOG_TEST_TIMEOUT_SECONDS", "300"))


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def unique_preserving_order(values: list[str]) -> list[str]:
    seen: set[str] = set()
    ordered: list[str] = []
    for value in values:
        if value not in seen:
            ordered.append(value)
            seen.add(value)
    return ordered


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.generated_dialog_element_parity",
    "status": "fail",
    "summary": "Generated dialog element parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "sourcePaths": {name: str(path.relative_to(repo_root)) for name, path in PATHS.items()},
        "expectedCommandIds": EXPECTED_COMMAND_IDS,
        "expectedControlIds": EXPECTED_CONTROL_IDS,
        "expectedRebuildableDialogIds": EXPECTED_REBUILDABLE_DIALOG_IDS,
        "factoryTests": {},
        "presenterTests": {},
        "workflowTests": {},
        "flagshipUiTests": {},
        "priorityWorkflowMarkers": {},
        "interactiveControlMarkers": {},
        "interactiveBuilderContracts": {},
        "m103DialogEvidence": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence = payload["evidence"]
source_artifact_failures: list[str] = []
inventory_failures: list[str] = []
test_marker_failures: list[str] = []
verify_wiring_failures: list[str] = []
m103_evidence_failures: list[str] = []
execution_failures: list[str] = []


def add_failure(message: str, *buckets: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)

missing_files = [str(path.relative_to(repo_root)) for path in PATHS.values() if not path.is_file()]
evidence["missingFiles"] = missing_files
if missing_files:
    add_failure("Required generated-dialog proof files are missing.", source_artifact_failures)

texts = {name: read_text(path) for name, path in PATHS.items() if path.is_file()}

dialog_factory_text = texts.get("dialog_factory", "")
dialog_window_text = texts.get("dialog_window", "")
factory_tests_text = texts.get("factory_tests", "")
presenter_tests_text = texts.get("presenter_tests", "")
workflow_tests_text = texts.get("workflow_tests", "")
flagship_ui_tests_text = texts.get("flagship_ui_tests", "")

command_switch_start = dialog_factory_text.find("return HumanizeVisibleDialog(commandId switch")
command_switch_end = dialog_factory_text.find("private static DesktopDialogState CreateRuntimeInspectorDialog")
command_switch_text = dialog_factory_text[command_switch_start:command_switch_end] if command_switch_start >= 0 and command_switch_end > command_switch_start else ""

command_ids: list[str] = []
if "OverviewCommandPolicy.RuntimeInspectorCommandId when runtimeInspector is not null" in command_switch_text:
    command_ids.append("runtime_inspector")
command_ids.extend(re.findall(r'\n\s*"([a-z0-9_]+)"\s*=>', command_switch_text))
command_ids = unique_preserving_order(command_ids)
evidence["commandIdsFound"] = command_ids
evidence["commandDialogCount"] = len(command_ids)
if command_ids != EXPECTED_COMMAND_IDS:
    add_failure(
        "Desktop command-dialog inventory drifted from the fail-closing parity contract.",
        inventory_failures,
    )

control_catalog_text = texts.get("control_catalog", "")
control_list_match = re.search(r"All\s*\{\s*get;\s*\}\s*=\s*\[(.*?)\];", control_catalog_text, re.S)
control_ids = re.findall(r'"([a-z0-9_]+)"', control_list_match.group(1)) if control_list_match else []
evidence["controlIdsFound"] = control_ids
evidence["legacyControlCount"] = len(control_ids)
if control_ids != EXPECTED_CONTROL_IDS:
    add_failure(
        "Legacy UI control catalog drifted from the generated-dialog parity contract.",
        inventory_failures,
    )

rebuild_start = dialog_factory_text.find("internal static DesktopDialogState RebuildDynamicDialog(")
rebuild_end = dialog_factory_text.find("internal static string ReadGlobalSettingsActivePane(")
rebuild_text = dialog_factory_text[rebuild_start:rebuild_end] if rebuild_start >= 0 and rebuild_end > rebuild_start else ""
rebuildable_dialog_ids: list[str] = []
if 'string.Equals(dialog.Id, "dialog.global_settings"' in rebuild_text:
    rebuildable_dialog_ids.append("dialog.global_settings")
rebuildable_dialog_ids.extend(re.findall(r'"(dialog\.[a-z0-9_\.]+)"', rebuild_text))
if "NewCharacterPriorityWorkflowDialogId =>" in rebuild_text:
    if "dialog.new_character" in rebuildable_dialog_ids:
        insert_at = rebuildable_dialog_ids.index("dialog.new_character") + 1
        rebuildable_dialog_ids.insert(insert_at, "dialog.new_character.priority_workflow")
    else:
        rebuildable_dialog_ids.append("dialog.new_character.priority_workflow")
rebuildable_dialog_ids = unique_preserving_order(rebuildable_dialog_ids)
evidence["rebuildableDialogIdsFound"] = rebuildable_dialog_ids
evidence["rebuildableDialogCount"] = len(rebuildable_dialog_ids)
if rebuildable_dialog_ids != EXPECTED_REBUILDABLE_DIALOG_IDS:
    add_failure(
        "Rebuildable generated-dialog inventory drifted from the parity contract.",
        inventory_failures,
    )

for marker in FACTORY_TEST_MARKERS:
    found = marker in factory_tests_text
    evidence["factoryTests"][marker] = found
    if not found:
        add_failure(f"Generated dialog factory test marker missing: {marker}.", test_marker_failures)

for marker in PRESENTER_TEST_MARKERS:
    found = marker in presenter_tests_text
    evidence["presenterTests"][marker] = found
    if not found:
        add_failure(f"Presenter generated-dialog routing test marker missing: {marker}.", test_marker_failures)

for marker in WORKFLOW_TEST_MARKERS:
    found = marker in workflow_tests_text
    evidence["workflowTests"][marker] = found
    if not found:
        add_failure(f"Generated dialog workflow parity test marker missing: {marker}.", test_marker_failures)

for marker in FLAGSHIP_UI_TEST_MARKERS:
    found = marker in flagship_ui_tests_text
    evidence["flagshipUiTests"][marker] = found
    if not found:
        add_failure(f"Generated dialog flagship UI parity test marker missing: {marker}.", test_marker_failures)

priority_workflow_markers = {
    "dialog_factory_priority_id": ('dialog.new_character.priority_workflow', dialog_factory_text),
    "dialog_factory_runtime_state_field": ("newCharacterPriorityWorkflowState", dialog_factory_text),
    "dialog_factory_can_commit_field": ("newCharacterPriorityWorkflowCanCommit", dialog_factory_text),
    "avalonia_priority_special_case": ('string.Equals(BoundDialogId, "dialog.new_character.priority_workflow", StringComparison.Ordinal)', dialog_window_text),
    "avalonia_priority_pane": ("CreateLegacyPriorityWorkflowPane(", dialog_window_text),
    "avalonia_select_combobox_selectionchanged": ("comboBox.SelectionChanged += (_, _) =>", dialog_window_text),
    "avalonia_select_listbox_selectionchanged": ("listBox.SelectionChanged += (_, _) =>", dialog_window_text),
    "avalonia_priority_metatype_doubletap_commit": ("metatypeList.DoubleTapped += async (_, _) =>", dialog_window_text),
    "dialog_factory_priority_last_changed_field": ("newCharacterPriorityLastChangedFieldId", dialog_factory_text),
    "dialog_factory_priority_reconcile": ("ReconcilePriorityLetters(", dialog_factory_text),
    "dialog_factory_priority_skill_choices": ("BuildPrioritySkillChoiceStates(", dialog_factory_text),
}
for name, (marker, haystack) in priority_workflow_markers.items():
    found = marker in haystack
    evidence["priorityWorkflowMarkers"][name] = {"marker": marker, "found": found}
    if not found:
        add_failure(
            f"Priority workflow generated-dialog parity marker missing: {name}.",
            inventory_failures,
        )

interactive_control_markers = {
    "generic_select_widget_class": ("ComboBox comboBox = new()", dialog_window_text),
    "generic_select_selectionchanged": ("comboBox.SelectionChanged += (_, _) =>", dialog_window_text),
    "generic_list_widget_class": ("ListBox listBox = new()", dialog_window_text),
    "generic_list_selectionchanged": ("listBox.SelectionChanged += (_, _) =>", dialog_window_text),
    "generic_tree_selectionchanged": ("treeView.SelectionChanged += (_, _) =>", dialog_window_text),
    "generic_checkbox_widget_class": ("CheckBox checkBox = new()", dialog_window_text),
    "generic_checkbox_checkedchanged": ("checkBox.IsCheckedChanged += (_, _) =>", dialog_window_text),
    "generic_textbox_widget_class": ("TextBox textBox = new()", dialog_window_text),
    "generic_textbox_textchanged": ("textBox.TextChanged += (_, _) =>", dialog_window_text),
}
for name, (marker, haystack) in interactive_control_markers.items():
    found = marker in haystack
    evidence["interactiveControlMarkers"][name] = {"marker": marker, "found": found}
    if not found:
        add_failure(
            f"Generated dialog interactive control parity marker missing: {name}.",
            inventory_failures,
        )

interactive_builder_contracts = {
    "BuildSelectComboBox": [
        "private ComboBox BuildSelectComboBox(",
        "ComboBox comboBox = new()",
        "comboBox.SelectionChanged += (_, _) =>",
    ],
    "BuildSelectListBox": [
        "private ListBox BuildSelectListBox(",
        "ListBox listBox = new()",
        "listBox.SelectionChanged += (_, _) =>",
    ],
    "BuildRosterTree": [
        "private TreeView BuildRosterTree(",
        "TreeView treeView = new()",
        "treeView.SelectionChanged += (_, _) =>",
    ],
    "BuildLegacyInlineCheckBox": [
        "private CheckBox BuildLegacyInlineCheckBox(",
        "CheckBox checkBox = new()",
        "checkBox.IsCheckedChanged += (_, _) =>",
    ],
    "BuildLegacyInlineTextBox": [
        "private TextBox BuildLegacyInlineTextBox(",
        "TextBox textBox = new()",
        "textBox.TextChanged += (_, _) =>",
    ],
}
for builder_name, markers in interactive_builder_contracts.items():
    found_markers = {marker: (marker in dialog_window_text) for marker in markers}
    evidence["interactiveBuilderContracts"][builder_name] = found_markers
    missing = [marker for marker, found in found_markers.items() if not found]
    if missing:
        add_failure(
            f"Generated dialog interactive builder contract drifted: {builder_name}.",
            inventory_failures,
        )

verify_text = texts.get("verify_script", "")
verify_banner = "checking generated dialog element parity guard"
verify_invocation = "bash scripts/ai/milestones/generated-dialog-element-parity-check.sh"
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if verify_banner not in verify_text or verify_invocation not in verify_text:
    add_failure(
        "Generated dialog element parity guard is not wired into scripts/ai/verify.sh.",
        verify_wiring_failures,
    )

m103_text = texts.get("m103_receipt", "")
if m103_text:
    m103_receipt = json.loads(m103_text)
    screenshot_control_evidence = m103_receipt.get("evidence", {}).get("screenshotControlEvidence", [])
    dialog_surface_count = 0
    dialog_field_total = 0
    dialog_action_total = 0
    if screenshot_control_evidence:
        for entry in screenshot_control_evidence:
            field_ids = entry.get("dialogFieldIds", [])
            action_control_ids = entry.get("dialogActionControlIds", [])
            if field_ids or action_control_ids:
                dialog_surface_count += 1
                dialog_field_total += len(field_ids)
                dialog_action_total += len(action_control_ids)
    else:
        surface_results = m103_receipt.get("evidence", {}).get("surfaceResults", {})
        for entry in surface_results.values():
            workflow_role = str(entry.get("workflowMap", {}).get("screenshotEvidenceRole", ""))
            if "dialog" not in workflow_role:
                continue

            source_checks = entry.get("sourceFileChecks", [])
            if source_checks:
                dialog_surface_count += 1
                dialog_field_total += sum(int(check.get("markerCount", 0)) for check in source_checks)
                dialog_action_total += len(source_checks)
    evidence["m103DialogEvidence"] = {
        "auditedDialogSurfaceCount": dialog_surface_count,
        "auditedDialogFieldCount": dialog_field_total,
        "auditedDialogActionControlCount": dialog_action_total,
    }
    if dialog_surface_count == 0:
        add_failure(
            "M103 receipt no longer records any dialog field/action screenshot evidence.",
            m103_evidence_failures,
        )
else:
    evidence["m103DialogEvidence"] = {
        "auditedDialogSurfaceCount": 0,
        "auditedDialogFieldCount": 0,
        "auditedDialogActionControlCount": 0,
    }
    add_failure("M103 receipt is unavailable for generated dialog evidence linkage.", m103_evidence_failures)

test_commands = [
    [
        "dotnet",
        "test",
        "--project",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--no-build",
        "--no-restore",
        "--filter",
        filter_expression,
        "--verbosity",
        "minimal",
    ]
    for filter_expression in TEST_FILTER_COMMANDS
]
evidence["testCommands"] = test_commands
evidence["testProject"] = "Chummer.Tests/Chummer.Tests.csproj"
evidence["buildTimeoutSeconds"] = BUILD_TIMEOUT_SECONDS
evidence["testTimeoutSeconds"] = TEST_TIMEOUT_SECONDS

build_result: subprocess.CompletedProcess[str] | None = None
test_results: list[dict[str, Any]] = []
evidence["buildExitCode"] = None
evidence["testResults"] = test_results
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

    try:
        build_result = subprocess.run(
            build_command,
            cwd=repo_root,
            text=True,
            capture_output=True,
            timeout=BUILD_TIMEOUT_SECONDS,
        )
        evidence["buildExitCode"] = build_result.returncode
        evidence["buildOutputTail"] = tail_lines((build_result.stdout or "") + "\n" + (build_result.stderr or ""))
        if build_result.returncode != 0:
            add_failure(
                f"Generated dialog parity build slice failed with exit code {build_result.returncode}.",
                execution_failures,
            )
        else:
            for test_command in test_commands:
                try:
                    test_result = subprocess.run(
                        test_command,
                        cwd=repo_root,
                        text=True,
                        capture_output=True,
                        timeout=TEST_TIMEOUT_SECONDS,
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
                            "timedOut": False,
                        }
                    )
                    if test_result.returncode != 0:
                        add_failure(
                            f"Generated dialog parity test slice failed with exit code {test_result.returncode}: {' '.join(test_command)}",
                            execution_failures,
                        )
                    elif no_matches:
                        add_failure(
                            f"Generated dialog parity test slice matched zero tests: {' '.join(test_command)}",
                            execution_failures,
                        )
                except subprocess.TimeoutExpired as exc:
                    combined_output = (exc.stdout or "") + "\n" + (exc.stderr or "")
                    test_results.append(
                        {
                            "command": test_command,
                            "exitCode": None,
                            "noMatches": False,
                            "outputTail": tail_lines(combined_output),
                            "timedOut": True,
                        }
                    )
                    add_failure(
                        f"Generated dialog parity test slice timed out after {TEST_TIMEOUT_SECONDS}s: {' '.join(test_command)}",
                        execution_failures,
                    )
            evidence["testResults"] = test_results
    except subprocess.TimeoutExpired as exc:
        combined_output = (exc.stdout or "") + "\n" + (exc.stderr or "")
        evidence["buildExitCode"] = None
        evidence["buildTimedOut"] = True
        evidence["buildOutputTail"] = tail_lines(combined_output)
        add_failure(
            f"Generated dialog parity build slice timed out after {BUILD_TIMEOUT_SECONDS}s.",
            execution_failures,
        )

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = "Generated dialog command/control inventories are locked, exhaustively shape-tested, and wired into standard verification."

payload["sourceArtifactReview"] = {
    "status": "pass" if not source_artifact_failures else "fail",
    "summary": (
        "Generated-dialog parity source files are present."
        if not source_artifact_failures
        else "Generated-dialog parity source files are missing."
    ),
    "reasons": source_artifact_failures,
    "missingFiles": missing_files,
}
payload["inventoryReview"] = {
    "status": "pass" if not inventory_failures else "fail",
    "summary": (
        "Command-dialog, legacy-control, and rebuildable-dialog inventories match the parity contract."
        if not inventory_failures
        else "Generated-dialog inventories drifted from the parity contract."
    ),
    "reasons": inventory_failures,
    "commandIdsFound": command_ids,
    "controlIdsFound": control_ids,
    "rebuildableDialogIdsFound": rebuildable_dialog_ids,
}
payload["testMarkerReview"] = {
    "status": "pass" if not test_marker_failures else "fail",
    "summary": (
        "Generated-dialog factory, presenter, workflow, and flagship UI test markers are pinned."
        if not test_marker_failures
        else "Generated-dialog factory, presenter, workflow, or flagship UI test markers are missing."
    ),
    "reasons": test_marker_failures,
    "factoryTests": evidence["factoryTests"],
    "presenterTests": evidence["presenterTests"],
    "workflowTests": evidence["workflowTests"],
    "flagshipUiTests": evidence["flagshipUiTests"],
}
payload["priorityWorkflowReview"] = {
    "status": (
        "pass"
        if not any("Priority workflow generated-dialog parity marker missing:" in reason for reason in inventory_failures)
        else "fail"
    ),
    "summary": (
        "Priority workflow remains locked to a dedicated parity pane with hidden runtime-state fields."
        if not any("Priority workflow generated-dialog parity marker missing:" in reason for reason in inventory_failures)
        else "Priority workflow dedicated-pane or hidden runtime-state markers drifted."
    ),
    "reasons": [
        reason
        for reason in inventory_failures
        if "Priority workflow generated-dialog parity marker missing:" in reason
    ],
    "priorityWorkflowMarkers": evidence["priorityWorkflowMarkers"],
}
payload["interactiveControlReview"] = {
    "status": (
        "pass"
        if not any("Generated dialog interactive control parity marker missing:" in reason for reason in inventory_failures)
        else "fail"
    ),
    "summary": (
        "Generic generated-dialog control builders still expose the expected widget classes and handler hooks."
        if not any("Generated dialog interactive control parity marker missing:" in reason for reason in inventory_failures)
        else "Generic generated-dialog control builders drifted away from the expected widget classes or handler hooks."
    ),
    "reasons": [
        reason
        for reason in inventory_failures
        if "Generated dialog interactive control parity marker missing:" in reason
    ],
    "interactiveControlMarkers": evidence["interactiveControlMarkers"],
}
payload["interactiveBuilderReview"] = {
    "status": (
        "pass"
        if not any("Generated dialog interactive builder contract drifted:" in reason for reason in inventory_failures)
        else "fail"
    ),
    "summary": (
        "Generated-dialog builder methods still preserve widget class plus event-hook contracts."
        if not any("Generated dialog interactive builder contract drifted:" in reason for reason in inventory_failures)
        else "One or more generated-dialog builder methods lost required widget class or event-hook contracts."
    ),
    "reasons": [
        reason
        for reason in inventory_failures
        if "Generated dialog interactive builder contract drifted:" in reason
    ],
    "interactiveBuilderContracts": evidence["interactiveBuilderContracts"],
}
payload["verifyWiringReview"] = {
    "status": "pass" if not verify_wiring_failures else "fail",
    "summary": (
        "Generated-dialog parity guard is wired into the standard verify path."
        if not verify_wiring_failures
        else "Generated-dialog parity guard is not wired into the standard verify path."
    ),
    "reasons": verify_wiring_failures,
    "wiredIntoStandardVerify": evidence["wiredIntoStandardVerify"],
    "verifyMarker": verify_banner,
    "verifyInvocation": verify_invocation,
}
payload["m103EvidenceReview"] = {
    "status": "pass" if not m103_evidence_failures else "fail",
    "summary": (
        "M103 screenshot evidence still covers generated dialog fields and actions."
        if not m103_evidence_failures
        else "M103 screenshot evidence is missing for generated dialog fields and actions."
    ),
    "reasons": m103_evidence_failures,
    "m103DialogEvidence": evidence["m103DialogEvidence"],
}
payload["executionReview"] = {
    "status": "pass" if not execution_failures else "fail",
    "summary": (
        "Generated-dialog parity build and test slices executed cleanly."
        if not execution_failures
        else "Generated-dialog parity build or test slices failed."
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

print("[generated-dialog] PASS: generated dialog command/control parity is inventoried, executable, and fail-closing.")
print(f"[generated-dialog] evidence: {receipt_path}")
PY
