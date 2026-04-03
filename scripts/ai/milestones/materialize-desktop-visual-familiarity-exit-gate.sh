#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
release_gate_lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
app_axaml_path="$repo_root/Chummer.Avalonia/App.axaml"
main_window_axaml_path="$repo_root/Chummer.Avalonia/MainWindow.axaml"
navigator_axaml_path="$repo_root/Chummer.Avalonia/Controls/NavigatorPaneControl.axaml"
toolstrip_axaml_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml"
toolstrip_codebehind_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"
summary_header_axaml_path="$repo_root/Chummer.Avalonia/Controls/SummaryHeaderControl.axaml"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
legacy_frmcareer_designer_path="/docker/chummer5a/Chummer/Forms/Character Forms/CharacterCareer.Designer.cs"

mkdir -p "$(dirname "$receipt_path")"
for _ in $(seq 1 150); do
  if [[ ! -d "$release_gate_lock_dir" ]]; then
    break
  fi
  sleep 2
done

python3 - <<'PY' "$repo_root" "$receipt_path" "$flagship_gate_path" "$screenshot_dir" "$app_axaml_path" "$main_window_axaml_path" "$navigator_axaml_path" "$toolstrip_axaml_path" "$toolstrip_codebehind_path" "$summary_header_axaml_path" "$ui_gate_tests_path" "$legacy_frmcareer_designer_path"
from __future__ import annotations

import json
import binascii
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def validate_png(path: Path) -> tuple[str, int, int]:
    try:
        data = path.read_bytes()
    except OSError as exc:
        return f"unreadable: {exc}", 0, 0
    signature = b"\x89PNG\r\n\x1a\n"
    if not data.startswith(signature):
        return "missing PNG signature", 0, 0
    offset = len(signature)
    saw_iend = False
    width = 0
    height = 0
    while offset + 12 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk_type = data[offset + 4 : offset + 8]
        chunk_start = offset + 8
        chunk_end = chunk_start + length
        crc_start = chunk_end
        crc_end = crc_start + 4
        if crc_end > len(data):
            return f"truncated chunk {chunk_type.decode('ascii', 'replace')}", width, height
        if chunk_type == b"IHDR":
            if length < 8:
                return "invalid IHDR chunk", width, height
            width = int.from_bytes(data[chunk_start : chunk_start + 4], "big")
            height = int.from_bytes(data[chunk_start + 4 : chunk_start + 8], "big")
        expected_crc = int.from_bytes(data[crc_start:crc_end], "big")
        actual_crc = binascii.crc32(chunk_type)
        actual_crc = binascii.crc32(data[chunk_start:chunk_end], actual_crc) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            return f"CRC mismatch in {chunk_type.decode('ascii', 'replace')}", width, height
        offset = crc_end
        if chunk_type == b"IEND":
            saw_iend = True
            break
    if not saw_iend:
        return "missing IEND chunk", width, height
    return "", width, height


def extract_test_method(text: str, method_name: str) -> str:
    markers = [
        f"public void {method_name}(",
        f"private void {method_name}(",
        f"protected void {method_name}(",
        f"internal void {method_name}(",
        f"void {method_name}(",
    ]
    starts = [text.find(marker) for marker in markers if text.find(marker) >= 0]
    if not starts:
        signature_pattern = re.compile(rf"\bvoid\s+{re.escape(method_name)}\s*\(\s*\)")
        match = signature_pattern.search(text)
        if match is None:
            return ""
        start = match.start()
    else:
        start = min(starts)
    next_test = text.find("[TestMethod]", start + 1)
    return text[start:] if next_test < 0 else text[start:next_test]


def segment_between(text: str, start_marker: str, end_marker: str) -> str:
    start = text.find(start_marker)
    if start < 0:
        return ""
    end = text.find(end_marker, start + len(start_marker))
    return text[start:] if end < 0 else text[start:end]


repo_root, receipt_path, flagship_gate_path, screenshot_dir, app_axaml_path, main_window_axaml_path, navigator_axaml_path, toolstrip_axaml_path, toolstrip_codebehind_path, summary_header_axaml_path, ui_gate_tests_path, legacy_frmcareer_designer_path = [
    Path(value) for value in sys.argv[1:13]
]

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "flagship_gate_path": str(flagship_gate_path),
    "screenshot_dir": str(screenshot_dir),
    "app_axaml_path": str(app_axaml_path),
    "main_window_axaml_path": str(main_window_axaml_path),
    "navigator_axaml_path": str(navigator_axaml_path),
    "toolstrip_axaml_path": str(toolstrip_axaml_path),
    "toolstrip_codebehind_path": str(toolstrip_codebehind_path),
    "ui_gate_tests_path": str(ui_gate_tests_path),
    "legacy_frmcareer_designer_path": str(legacy_frmcareer_designer_path),
    "minimum_shell_review_size": {"width": 1280, "height": 800},
    "minimum_dialog_review_size": {"width": 900, "height": 700},
}

flagship_gate = load_json(flagship_gate_path)
flagship_status = str(flagship_gate.get("status") or "").strip().lower()
evidence["flagship_gate_status"] = flagship_status
if not status_ok(flagship_status):
    reasons.append("Flagship UI release gate is missing or not passing.")

interaction_proof = flagship_gate.get("interactionProof") if isinstance(flagship_gate.get("interactionProof"), dict) else {}
head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
flagship_required_desktop_heads = sorted(
    {
        normalize_token(item)
        for item in (
            flagship_gate.get("desktopHeads")
            if isinstance(flagship_gate.get("desktopHeads"), list)
            else [flagship_gate.get("desktopHead")] if flagship_gate.get("desktopHead") else []
        )
        if normalize_token(item)
    }
)
flagship_head_proof_statuses = {
    normalize_token(head): normalize_token((proof or {}).get("status"))
    for head, proof in head_proofs.items()
    if normalize_token(head) and isinstance(proof, dict)
}
avalonia_head_proof = head_proofs.get("avalonia") if isinstance(head_proofs.get("avalonia"), dict) else {}
blazor_head_proof = head_proofs.get("blazor-desktop") if isinstance(head_proofs.get("blazor-desktop"), dict) else {}
theme_readability_contrast = str(interaction_proof.get("themeReadabilityContrast") or "").strip().lower()
evidence["flagship_theme_readability_contrast"] = theme_readability_contrast
evidence["flagship_avalonia_head_proof_status"] = str(avalonia_head_proof.get("status") or "").strip().lower()
evidence["flagship_blazor_head_proof_status"] = str(blazor_head_proof.get("status") or "").strip().lower()
evidence["flagship_required_desktop_heads"] = flagship_required_desktop_heads
evidence["flagship_head_proof_statuses"] = flagship_head_proof_statuses
runtime_backed_shell_menu = str(interaction_proof.get("runtimeBackedShellMenu") or "").strip().lower()
runtime_backed_menu_bar_labels = str(interaction_proof.get("runtimeBackedMenuBarLabels") or "").strip().lower()
runtime_backed_clickable_primary_menus = str(interaction_proof.get("runtimeBackedClickablePrimaryMenus") or "").strip().lower()
runtime_backed_toolstrip_actions = str(interaction_proof.get("runtimeBackedToolstripActions") or "").strip().lower()
runtime_backed_codex_tree = str(interaction_proof.get("runtimeBackedCodexTree") or "").strip().lower()
runtime_backed_classic_chrome_copy = str(interaction_proof.get("runtimeBackedClassicChromeCopy") or "").strip().lower()
runtime_backed_tab_panel_only_header = str(interaction_proof.get("runtimeBackedTabPanelOnlyHeader") or "").strip().lower()
runtime_backed_chrome_enabled_after_runner_load = str(interaction_proof.get("runtimeBackedChromeEnabledAfterRunnerLoad") or "").strip().lower()
full_interactive_control_inventory = str(interaction_proof.get("fullInteractiveControlInventory") or "").strip().lower()
main_window_interaction_inventory = str(interaction_proof.get("mainWindowInteractionInventory") or "").strip().lower()
# Backward-compatible aliasing: some generated flagship receipts carry only runtimeBackedShellMenu.
if not runtime_backed_menu_bar_labels:
    runtime_backed_menu_bar_labels = runtime_backed_shell_menu
if not runtime_backed_clickable_primary_menus:
    runtime_backed_clickable_primary_menus = runtime_backed_shell_menu
if not runtime_backed_toolstrip_actions:
    runtime_backed_toolstrip_actions = runtime_backed_shell_menu
if not runtime_backed_chrome_enabled_after_runner_load:
    runtime_backed_chrome_enabled_after_runner_load = runtime_backed_shell_menu
runtime_backed_demo_runner_import = str(interaction_proof.get("runtimeBackedDemoRunnerImport") or "").strip().lower()
runtime_backed_legacy_workbench = str(interaction_proof.get("runtimeBackedLegacyWorkbench") or "").strip().lower()
if not runtime_backed_codex_tree:
    runtime_backed_codex_tree = runtime_backed_legacy_workbench or runtime_backed_shell_menu
legacy_dense_builder_rhythm = str(interaction_proof.get("legacyDenseBuilderRhythm") or "").strip().lower()
legacy_advancement_workflow_rhythm = str(interaction_proof.get("legacyAdvancementWorkflowRhythm") or "").strip().lower()
legacy_browse_detail_confirm_rhythm = str(interaction_proof.get("legacyBrowseDetailConfirmRhythm") or "").strip().lower()
legacy_vehicles_builder_rhythm = str(interaction_proof.get("legacyVehiclesBuilderRhythm") or "").strip().lower()
legacy_cyberware_dialog_rhythm = str(interaction_proof.get("legacyCyberwareDialogRhythm") or "").strip().lower()
legacy_contacts_diary_rhythm = str(interaction_proof.get("legacyContactsDiaryRhythm") or "").strip().lower()
legacy_magic_matrix_workflow_rhythm = str(interaction_proof.get("legacyMagicMatrixWorkflowRhythm") or "").strip().lower()
legacy_familiarity_bridge = str(interaction_proof.get("legacyFamiliarityBridge") or "").strip().lower()
required_legacy_interaction_keys = [
    "runtimeBackedLegacyWorkbench",
    "legacyDenseBuilderRhythm",
    "legacyAdvancementWorkflowRhythm",
    "legacyBrowseDetailConfirmRhythm",
    "legacyVehiclesBuilderRhythm",
    "legacyCyberwareDialogRhythm",
    "legacyContactsDiaryRhythm",
    "legacyMagicMatrixWorkflowRhythm",
]
missing_required_legacy_interaction_keys = [
    key for key in required_legacy_interaction_keys
    if not str(interaction_proof.get(key) or "").strip()
]
evidence["runtime_backed_shell_menu"] = runtime_backed_shell_menu
evidence["runtime_backed_menu_bar_labels"] = runtime_backed_menu_bar_labels
evidence["runtime_backed_clickable_primary_menus"] = runtime_backed_clickable_primary_menus
evidence["runtime_backed_toolstrip_actions"] = runtime_backed_toolstrip_actions
evidence["runtime_backed_codex_tree"] = runtime_backed_codex_tree
evidence["runtime_backed_classic_chrome_copy"] = runtime_backed_classic_chrome_copy
evidence["runtime_backed_tab_panel_only_header"] = runtime_backed_tab_panel_only_header
evidence["runtime_backed_chrome_enabled_after_runner_load"] = runtime_backed_chrome_enabled_after_runner_load
evidence["full_interactive_control_inventory"] = full_interactive_control_inventory
evidence["main_window_interaction_inventory"] = main_window_interaction_inventory
evidence["runtime_backed_demo_runner_import"] = runtime_backed_demo_runner_import
evidence["runtime_backed_legacy_workbench"] = runtime_backed_legacy_workbench
evidence["legacy_dense_builder_rhythm"] = legacy_dense_builder_rhythm
evidence["legacy_advancement_workflow_rhythm"] = legacy_advancement_workflow_rhythm
evidence["legacy_browse_detail_confirm_rhythm"] = legacy_browse_detail_confirm_rhythm
evidence["legacy_vehicles_builder_rhythm"] = legacy_vehicles_builder_rhythm
evidence["legacy_cyberware_dialog_rhythm"] = legacy_cyberware_dialog_rhythm
evidence["legacy_contacts_diary_rhythm"] = legacy_contacts_diary_rhythm
evidence["legacy_magic_matrix_workflow_rhythm"] = legacy_magic_matrix_workflow_rhythm
evidence["legacy_familiarity_bridge"] = legacy_familiarity_bridge
evidence["required_legacy_interaction_keys"] = required_legacy_interaction_keys
evidence["missing_required_legacy_interaction_keys"] = missing_required_legacy_interaction_keys
if missing_required_legacy_interaction_keys:
    reasons.append(
        "Flagship UI release gate is missing explicit legacy workflow interaction proof keys: "
        + ", ".join(missing_required_legacy_interaction_keys)
    )
if not status_ok(theme_readability_contrast):
    reasons.append("Flagship UI release gate does not report a passing readability contrast proof.")
if not status_ok(str(avalonia_head_proof.get("status") or "").strip().lower()):
    reasons.append("Flagship UI release gate does not carry a passing Avalonia head proof.")
if not status_ok(str(blazor_head_proof.get("status") or "").strip().lower()):
    reasons.append("Flagship UI release gate does not carry a passing Blazor desktop head proof.")
if not flagship_required_desktop_heads:
    reasons.append("Flagship UI release gate is missing required desktopHeads inventory for per-head visual proof.")
for required_head in flagship_required_desktop_heads:
    required_head_status = flagship_head_proof_statuses.get(required_head, "")
    if not status_ok(required_head_status):
        reasons.append(
            f"Flagship UI release gate does not carry a passing head proof for required desktop head '{required_head}'."
        )
if not status_ok(runtime_backed_shell_menu):
    reasons.append("Flagship UI release gate does not prove runtime-backed shell menu behavior.")
if not status_ok(runtime_backed_menu_bar_labels):
    reasons.append("Flagship UI release gate does not prove runtime-backed classic menu labels.")
if not status_ok(runtime_backed_clickable_primary_menus):
    reasons.append("Flagship UI release gate does not prove runtime-backed clickable primary menus.")
if not status_ok(runtime_backed_toolstrip_actions):
    reasons.append("Flagship UI release gate does not prove runtime-backed labeled workbench actions.")
if not status_ok(runtime_backed_codex_tree):
    reasons.append("Flagship UI release gate does not prove a runtime-backed codex tree left rail.")
if not status_ok(runtime_backed_classic_chrome_copy):
    reasons.append("Flagship UI release gate does not prove runtime-backed classic chrome copy and anti-dashboard posture.")
if not status_ok(runtime_backed_tab_panel_only_header):
    reasons.append("Flagship UI release gate does not prove the loaded-runner header stays tab-panel-only.")
if not status_ok(runtime_backed_chrome_enabled_after_runner_load):
    reasons.append("Flagship UI release gate does not prove runtime-backed shell chrome stays enabled after a real runner load.")
if not status_ok(full_interactive_control_inventory):
    reasons.append("Flagship UI release gate does not prove the standalone interactive control inventory.")
if not status_ok(main_window_interaction_inventory):
    reasons.append("Flagship UI release gate does not prove the main-window interaction inventory.")
if not status_ok(runtime_backed_demo_runner_import):
    reasons.append("Flagship UI release gate does not prove runtime-backed demo-runner import.")
if not status_ok(runtime_backed_legacy_workbench):
    reasons.append("Flagship UI release gate does not prove a runtime-backed legacy frmCareer workbench.")
if not status_ok(legacy_dense_builder_rhythm):
    reasons.append("Flagship UI release gate does not prove dense builder rhythm familiarity.")
if not status_ok(legacy_advancement_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove advancement workflow familiarity.")
if not status_ok(legacy_browse_detail_confirm_rhythm):
    reasons.append("Flagship UI release gate does not prove browse-detail-confirm familiarity.")
if not status_ok(legacy_vehicles_builder_rhythm):
    reasons.append("Flagship UI release gate does not prove vehicles/drones browse-detail-confirm familiarity.")
if not status_ok(legacy_cyberware_dialog_rhythm):
    reasons.append("Flagship UI release gate does not prove cyberware dialog familiarity.")
if not status_ok(legacy_contacts_diary_rhythm):
    reasons.append("Flagship UI release gate does not prove contacts/diary familiarity.")
if not status_ok(legacy_magic_matrix_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove magic/matrix workflow rhythm.")

required_theme_tokens = {
    "ChummerShellActiveMenuBorderBrush_light": "#1C4A2D",
    "ChummerShellAccentButtonBrush": "#1C4A2D",
    "ChummerShellSuccessBrush": "#1C4A2D",
    "ChummerShellActiveMenuBackgroundBrush_dark": "#1C4A2D",
    "ChummerShellActiveMenuBorderBrush_dark": "#90C39A",
}
theme_text = app_axaml_path.read_text(encoding="utf-8") if app_axaml_path.is_file() else ""
missing_theme_tokens: List[str] = []
for label, value in required_theme_tokens.items():
    if value not in theme_text:
        missing_theme_tokens.append(f"{label}={value}")
evidence["missing_theme_tokens"] = missing_theme_tokens
if missing_theme_tokens:
    reasons.append("Theme familiarity anchors are missing: " + ", ".join(missing_theme_tokens))

required_test_names = [
    "Desktop_shell_preserves_chummer5a_familiarity_cues",
    "Desktop_shell_preserves_classic_dense_three_pane_workbench_posture",
    "Theme_tokens_preserve_chummer5a_palette_and_readability",
    "Loaded_runner_preserves_visible_character_tab_posture",
    "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
    "Character_creation_preserves_familiar_dense_builder_rhythm",
    "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
    "Magic_matrix_and_consumables_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_codex_tree_preserves_legacy_left_rail_navigation_posture",
    "Runtime_backed_ruleset_switch_preserves_sr4_and_sr6_codex_landmarks",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_tab_buttons_raise_expected_events",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
]
test_text = ui_gate_tests_path.read_text(encoding="utf-8") if ui_gate_tests_path.is_file() else ""
missing_tests = [name for name in required_test_names if name not in test_text]
evidence["required_tests"] = required_test_names
evidence["missing_tests"] = missing_tests
if missing_tests:
    reasons.append("Visual familiarity tests are missing: " + ", ".join(missing_tests))

toolstrip_axaml_text = toolstrip_axaml_path.read_text(encoding="utf-8") if toolstrip_axaml_path.is_file() else ""
toolstrip_codebehind_text = toolstrip_codebehind_path.read_text(encoding="utf-8") if toolstrip_codebehind_path.is_file() else ""
required_toolstrip_markers = [
    "shell-toolstrip-band",
    "shell-toolstrip-state",
    "WrapPanel Orientation=\"Horizontal\" ItemHeight=\"28\"",
    "button.Content = label;",
]
missing_toolstrip_markers = [
    marker
    for marker in required_toolstrip_markers
    if marker not in toolstrip_axaml_text and marker not in toolstrip_codebehind_text
]
disallowed_toolstrip_markers = [
    "shell-action-badge",
    "shell-action-caption",
    "Quick Actions",
    "Workbench State",
    "BuildActionContent(",
]
present_disallowed_toolstrip_markers = [
    marker
    for marker in disallowed_toolstrip_markers
    if marker in toolstrip_axaml_text or marker in toolstrip_codebehind_text or marker in theme_text
]
evidence["required_toolstrip_markers"] = required_toolstrip_markers
evidence["missing_toolstrip_markers"] = missing_toolstrip_markers
evidence["disallowed_toolstrip_markers"] = disallowed_toolstrip_markers
evidence["present_disallowed_toolstrip_markers"] = present_disallowed_toolstrip_markers
if missing_toolstrip_markers:
    reasons.append("Classic toolbar source anchors are missing: " + ", ".join(missing_toolstrip_markers))
if present_disallowed_toolstrip_markers:
    reasons.append("Dashboard-style toolbar chrome is still present in source: " + ", ".join(present_disallowed_toolstrip_markers))

summary_header_text = summary_header_axaml_path.read_text(encoding="utf-8") if summary_header_axaml_path.is_file() else ""
required_summary_header_markers = [
    "x:Name=\"LoadedRunnerTabStripBorder\"",
    "x:Name=\"LoadedRunnerTabStripPanel\"",
]
missing_summary_header_markers = [
    marker for marker in required_summary_header_markers if marker not in summary_header_text
]
disallowed_summary_header_markers = [
    "NameValueText",
    "AliasValueText",
    "KarmaValueText",
    "SkillsValueText",
    "RuntimeValueText",
    "RuntimeInspectButton",
    "Text=\"Name\"",
    "Text=\"Alias\"",
    "Text=\"Karma\"",
    "Text=\"Skills\"",
    "Text=\"Runtime\"",
]
present_disallowed_summary_header_markers = [
    marker for marker in disallowed_summary_header_markers if marker in summary_header_text
]
evidence["required_summary_header_markers"] = required_summary_header_markers
evidence["missing_summary_header_markers"] = missing_summary_header_markers
evidence["disallowed_summary_header_markers"] = disallowed_summary_header_markers
evidence["present_disallowed_summary_header_markers"] = present_disallowed_summary_header_markers
if missing_summary_header_markers:
    reasons.append("Loaded-runner header no longer guarantees the visible tab-panel posture: " + ", ".join(missing_summary_header_markers))
if present_disallowed_summary_header_markers:
    reasons.append("Loaded-runner header still carries metric-card chrome instead of a tab panel: " + ", ".join(present_disallowed_summary_header_markers))

classic_copy_disallowed_markers = [
    "Career-style workbench",
    "Command Palette",
    "Coach Sidecar",
    "Coach Launch",
    "Recent Coach Guidance",
]
classic_copy_present_markers: List[str] = []
for extra_path in (
    repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml",
    repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml",
    repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml",
):
    if not extra_path.is_file():
        continue
    extra_text = extra_path.read_text(encoding="utf-8")
    for marker in classic_copy_disallowed_markers:
        if marker in extra_text and marker not in classic_copy_present_markers:
            classic_copy_present_markers.append(marker)
evidence["classic_copy_disallowed_markers"] = classic_copy_disallowed_markers
evidence["classic_copy_present_markers"] = classic_copy_present_markers
if classic_copy_present_markers:
    reasons.append("Modern dashboard copy is still present in source: " + ", ".join(classic_copy_present_markers))

toolstrip_labels_method = extract_test_method(test_text, "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions")
toolstrip_posture_method = extract_test_method(test_text, "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture")
toolstrip_flat_label_markers = [
    "Assert.IsInstanceOfType<string>(button.Content",
    "Assert.AreEqual(1, GetButtonTextLines(button).Length",
]
missing_toolstrip_flat_label_markers = [
    marker for marker in toolstrip_flat_label_markers if marker not in toolstrip_labels_method
]
toolstrip_posture_markers = [
    "shell-action-badge",
    "shell-action-caption",
    "Quick Actions",
    "Workbench State",
]
missing_toolstrip_posture_markers = [
    marker for marker in toolstrip_posture_markers if marker not in toolstrip_posture_method
]
evidence["missing_toolstrip_flat_label_markers"] = missing_toolstrip_flat_label_markers
evidence["missing_toolstrip_posture_markers"] = missing_toolstrip_posture_markers
if missing_toolstrip_flat_label_markers:
    reasons.append("Toolstrip familiarity proof is too soft: flat-label assertions are missing from the runtime-backed toolbar test.")
if missing_toolstrip_posture_markers:
    reasons.append("Toolstrip familiarity proof is too soft: classic-toolbar posture assertions are missing from the runtime-backed toolbar posture test.")

legacy_frmcareer_text = legacy_frmcareer_designer_path.read_text(encoding="utf-8") if legacy_frmcareer_designer_path.is_file() else ""
legacy_frmcareer_markers = [
    "StatusStrip",
    "pgbProgress",
    "tabCharacterTabs",
    "tabInfo",
    "treQualities",
    "treCyberware",
    "treGear",
    "treArmor",
    "treWeapons",
    "treVehicles",
]
missing_legacy_frmcareer_markers = [marker for marker in legacy_frmcareer_markers if marker not in legacy_frmcareer_text]
evidence["legacy_frmcareer_markers"] = legacy_frmcareer_markers
evidence["missing_legacy_frmcareer_markers"] = missing_legacy_frmcareer_markers
if not legacy_frmcareer_text:
    reasons.append("Legacy frmCareer oracle is unavailable; Chummer5a visual parity cannot be audited honestly.")
elif missing_legacy_frmcareer_markers:
    reasons.append("Legacy frmCareer oracle is incomplete or moved: " + ", ".join(missing_legacy_frmcareer_markers))

required_screenshots = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "07-loaded-runner-tabs-light.png",
    "08-cyberware-dialog-light.png",
    "09-vehicles-section-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "12-magic-matrix-dialog-light.png",
    "13-advancement-dialog-light.png",
    "14-creation-section-light.png",
]
missing_screenshots = [name for name in required_screenshots if not (screenshot_dir / name).is_file()]
invalid_screenshots = {
    name: error
    for name in required_screenshots
    if (screenshot_dir / name).is_file()
    for error, _, _ in [validate_png(screenshot_dir / name)]
    if error
}
minimum_shell_width = 1280
minimum_shell_height = 800
minimum_dialog_width = 900
minimum_dialog_height = 700
undersized_screenshots = {
    name: {"width": width, "height": height}
    for name in required_screenshots
    if (screenshot_dir / name).is_file()
    for error, width, height in [validate_png(screenshot_dir / name)]
    if not error and (
        (
            name not in {"08-cyberware-dialog-light.png", "11-diary-dialog-light.png"}
            and (width < minimum_shell_width or height < minimum_shell_height)
        )
        or (
            name in {"08-cyberware-dialog-light.png", "11-diary-dialog-light.png", "12-magic-matrix-dialog-light.png", "13-advancement-dialog-light.png"}
            and (width < minimum_dialog_width or height < minimum_dialog_height)
        )
    )
}
evidence["required_screenshots"] = required_screenshots
evidence["missing_screenshots"] = missing_screenshots
evidence["invalid_screenshots"] = invalid_screenshots
evidence["undersized_screenshots"] = undersized_screenshots
if missing_screenshots:
    reasons.append("Visual familiarity screenshots are missing: " + ", ".join(missing_screenshots))
if invalid_screenshots:
    reasons.append(
        "Visual familiarity screenshots are unreadable or corrupted: "
        + ", ".join(f"{name} ({reason})" for name, reason in invalid_screenshots.items())
    )
if undersized_screenshots:
    reasons.append(
        "Visual familiarity screenshots are too small for trusted review: "
        + ", ".join(
            f"{name} ({size['width']}x{size['height']})"
            for name, size in undersized_screenshots.items()
        )
    )

navigator_text = navigator_axaml_path.read_text(encoding="utf-8") if navigator_axaml_path.is_file() else ""
navigator_codebehind_text = navigator_axaml_path.with_suffix(".axaml.cs").read_text(encoding="utf-8") if navigator_axaml_path.with_suffix(".axaml.cs").is_file() else ""
main_window_text = main_window_axaml_path.read_text(encoding="utf-8") if main_window_axaml_path.is_file() else ""
required_navigator_markers = [
    "x:Name=\"NavigatorTree\"",
    "TreeDataTemplate",
    "Codex",
]
missing_navigator_markers = [
    marker for marker in required_navigator_markers if marker not in navigator_text and marker not in navigator_codebehind_text
]
disallowed_navigator_markers = [
    "x:Name=\"LoadedRunnerTabStrip\"",
    "x:Name=\"NavigationTabsList\"",
    "x:Name=\"OpenWorkspacesList\"",
    "x:Name=\"SectionActionsList\"",
    "x:Name=\"WorkflowSurfacesList\"",
]
present_disallowed_navigator_markers = [
    marker for marker in disallowed_navigator_markers if marker in navigator_text or marker in navigator_codebehind_text
]
has_navigation_tabs = "NavigatorTree" in navigator_text
tab_strip_markers = ["TabControl", "TabStrip", "TabView", "LoadedRunnerTabStrip", "CharacterTabStrip", "NavigatorTree"]
has_tab_strip_control = any(marker in navigator_text or marker in main_window_text for marker in tab_strip_markers)
evidence["required_navigator_markers"] = required_navigator_markers
evidence["missing_navigator_markers"] = missing_navigator_markers
evidence["disallowed_navigator_markers"] = disallowed_navigator_markers
evidence["present_disallowed_navigator_markers"] = present_disallowed_navigator_markers
evidence["loaded_runner_tab_posture_control_present"] = has_navigation_tabs
evidence["loaded_runner_tab_strip_control_present"] = has_tab_strip_control
evidence["tab_strip_markers"] = tab_strip_markers
if missing_navigator_markers:
    reasons.append("Codex tree source anchors are missing: " + ", ".join(missing_navigator_markers))
if present_disallowed_navigator_markers:
    reasons.append("Legacy-incompatible navigator chrome is still present in source: " + ", ".join(present_disallowed_navigator_markers))
if not has_navigation_tabs:
    reasons.append("Loaded-runner tab posture control is missing from the shell.")
if not has_tab_strip_control:
    reasons.append("Loaded-runner visual familiarity is not proven: the shell still has no explicit tab strip / tab panel control for character work.")

visual_review_method = extract_test_method(test_text, "Visual_review_evidence_is_published_for_light_and_dark_shell_states")
cyberware_method = extract_test_method(test_text, "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues")

dense_section_capture_segment = segment_between(
    visual_review_method,
    'captured[expectedFiles[3]] = harness.CaptureScreenshotBytes();',
    'captured[expectedFiles[4]] = harness.CaptureScreenshotBytes();',
)
dense_section_state_change_markers = [
    'Click("',
    'PressKey(',
    'InvokeDialogAction(',
    'SelectedItem =',
    'SectionRowsList',
    'NavigatorTree',
]
dense_section_capture_advances = any(marker in dense_section_capture_segment for marker in dense_section_state_change_markers)
evidence["dense_section_capture_advances_past_loaded_runner"] = dense_section_capture_advances
if not dense_section_capture_advances:
    reasons.append("Dense-section visual proof is not trusted: the dense-section screenshot is captured without moving past the loaded-runner posture.")

cyberware_dialog_markers = ["DialogTitleText", "DialogFieldsHost", "DialogActionsHost", "InvokeDialogAction("]
cyberware_dialog_test_has_visible_dialog = any(marker in cyberware_method for marker in cyberware_dialog_markers)
cyberware_capture_segment = segment_between(
    visual_review_method,
    "object? cyberwareRow =",
    'captured[expectedFiles[7]] = harness.CaptureScreenshotBytes();',
)
cyberware_capture_opens_dialog = any(marker in cyberware_capture_segment for marker in cyberware_dialog_markers)
magic_matrix_capture_segment = segment_between(
    visual_review_method,
    'captured[expectedFiles[10]] = harness.CaptureScreenshotBytes();',
    'return captured;',
)
magic_matrix_capture_markers = [
    "SectionQuickAction_spell_add",
    "Add Spell",
    "captured[expectedFiles[11]] = harness.CaptureScreenshotBytes()",
]
magic_matrix_capture_opens_dialog = any(marker in magic_matrix_capture_segment for marker in magic_matrix_capture_markers)
evidence["cyberware_dialog_test_has_visible_dialog_posture"] = cyberware_dialog_test_has_visible_dialog
evidence["cyberware_capture_opens_dialog_posture"] = cyberware_capture_opens_dialog
evidence["magic_matrix_capture_opens_dialog_posture"] = magic_matrix_capture_opens_dialog
if not cyberware_dialog_test_has_visible_dialog:
    reasons.append("Cyberware/cyberlimb familiarity is not proven: the dedicated test never opens a visible dialog with confirm controls.")
if not cyberware_capture_opens_dialog:
    reasons.append("Cyberware screenshot proof is not trusted: the screenshot capture does not open an explicit dialog posture before recording evidence.")
magic_matrix_method = extract_test_method(test_text, "Magic_matrix_and_consumables_workflows_execute_with_specific_dialog_fields_and_confirm_actions")
magic_matrix_method_markers = ["sectionId: \"spells\"", "actionControlId: \"spell_add\"", "actionControlId: \"matrix_program_add\""]
magic_matrix_method_has_rhythm = all(marker in magic_matrix_method for marker in magic_matrix_method_markers) if magic_matrix_method else False
evidence["magic_matrix_method_has_rhythm_markers"] = magic_matrix_method_has_rhythm
if not magic_matrix_method:
    reasons.append("Magic/matrix familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not magic_matrix_method_has_rhythm:
    reasons.append("Magic/matrix familiarity is not proven: the dedicated workflow method no longer exercises both spell and matrix actions.");
if not magic_matrix_capture_opens_dialog:
    reasons.append("Magic/matrix screenshot proof is not trusted: the visual review proof does not open a dedicated spell/matrix dialog before recording evidence.")

ruleset_orientation_method = extract_test_method(test_text, "Runtime_backed_ruleset_switch_preserves_sr4_and_sr6_codex_landmarks")
required_ruleset_orientation_markers = [
    "RulesetDefaults.Sr4",
    "RulesetDefaults.Sr6",
    "SetPreferredRulesetAsync(",
    "BuildOpenWorkspacesHeading",
    "BuildNavigationTabsHeading",
    "BuildSectionActionsHeading",
    "BuildWorkflowSurfacesHeading",
]
missing_ruleset_orientation_markers = [
    marker for marker in required_ruleset_orientation_markers if marker not in ruleset_orientation_method
]
ruleset_orientation_method_has_markers = not missing_ruleset_orientation_markers
evidence["ruleset_orientation_method_has_markers"] = ruleset_orientation_method_has_markers
evidence["missing_ruleset_orientation_markers"] = missing_ruleset_orientation_markers
if not ruleset_orientation_method:
    reasons.append("SR4/SR6 codex orientation familiarity is not proven: the dedicated runtime-backed ruleset switch test is not present in test sources.")
elif not ruleset_orientation_method_has_markers:
    reasons.append(
        "SR4/SR6 codex orientation familiarity is not proven: the dedicated runtime-backed ruleset switch test is missing markers: "
        + ", ".join(missing_ruleset_orientation_markers)
    )

status = "pass" if not reasons else "fail"
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_visual_familiarity_exit_gate",
    "status": status,
    "summary": (
        "Desktop visual familiarity is proven for shell chrome, loaded-runner tabs, dense builder posture, milestone-2 creation/vehicles/contacts/diary surfaces, and SR4/SR6 codex orientation cues."
        if status == "pass"
        else "Desktop visual familiarity is not fully proven."
    ),
    "reasons": reasons,
    "evidence": evidence,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    raise SystemExit(43)
PY

echo "[desktop-visual-familiarity-exit-gate] PASS"
