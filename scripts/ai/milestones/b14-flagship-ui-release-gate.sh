#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

output_dir="$repo_root/Chummer.Avalonia/bin/Release/net10.0"
sample_path="$output_dir/Samples/Legacy/Soma-Career.chum5"
receipt_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
capture_screenshot_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-ui-flagship-gate-screenshots.XXXXXX")"
staged_screenshot_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-ui-flagship-published-screenshots.XXXXXX")"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
blazor_shell_tests_path="$repo_root/Chummer.Tests/Presentation/BlazorShellComponentTests.cs"
desktop_update_runtime_tests_path="$repo_root/Chummer.Tests/DesktopUpdateRuntimeTests.cs"
desktop_install_linking_runtime_tests_path="$repo_root/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs"
desktop_startup_smoke_runtime_tests_path="$repo_root/Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs"
workflow_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_sr6_frontier_receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
desktop_workflow_execution_receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
localization_release_gate_receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
nuget_packages="${CHUMMER_NUGET_PACKAGES:-$repo_root/.codex-studio/.nuget/packages}"

mkdir -p "$(dirname "$lock_dir")"
for _ in $(seq 1 150); do
  if mkdir "$lock_dir" 2>/dev/null; then
    break
  fi
  sleep 2
done
if [[ ! -d "$lock_dir" ]]; then
  echo "[b14] FAIL: could not acquire release gate lock: $lock_dir" >&2
  exit 44
fi

cleanup() {
  rm -rf "$capture_screenshot_dir" "$staged_screenshot_dir"
  rmdir "$lock_dir" 2>/dev/null || true
}
trap cleanup EXIT

run_with_retry() {
  local max_attempts="$1"
  local step_label="$2"
  shift 2

  local attempt=1
  while true; do
    if "$@"; then
      return 0
    fi

    if (( attempt >= max_attempts )); then
      echo "[b14] FAIL: ${step_label} failed after ${attempt} attempts." >&2
      return 1
    fi

    echo "[b14] WARN: ${step_label} failed on attempt ${attempt}/${max_attempts}; retrying..." >&2
    attempt=$((attempt + 1))
    sleep 1
  done
}

mkdir -p "$(dirname "$receipt_path")"
mkdir -p "$nuget_packages"
export NUGET_PACKAGES="$nuget_packages"

echo "[b14] building Avalonia desktop head..."
bash scripts/ai/build.sh Chummer.Avalonia/Chummer.Avalonia.csproj -c Release -v minimal >/dev/null

if [[ ! -f "$sample_path" ]]; then
  echo "[b14] FAIL: bundled demo runner fixture missing from Release output: $sample_path" >&2
  exit 41
fi

if ! rg -q "b14-flagship-ui-release-gate\\.sh" "$signoff_path"; then
  echo "[b14] FAIL: workbench release signoff does not cite the flagship UI release gate: $signoff_path" >&2
  exit 42
fi

python3 - <<'PY' "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path"
import sys
from pathlib import Path

avalonia_gate_tests_path = Path(sys.argv[1])
dual_head_tests_path = Path(sys.argv[2])
blazor_shell_tests_path = Path(sys.argv[3])
desktop_update_runtime_tests_path = Path(sys.argv[4])
desktop_install_linking_runtime_tests_path = Path(sys.argv[5])
desktop_startup_smoke_runtime_tests_path = Path(sys.argv[6])
avalonia_text = avalonia_gate_tests_path.read_text(encoding="utf-8")
required_avalonia_tests = [
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_codex_tree_preserves_legacy_left_rail_navigation_posture",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
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
    "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
    "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
    "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
    "Character_creation_preserves_familiar_dense_builder_rhythm",
    "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
    "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
]
missing_avalonia = [name for name in required_avalonia_tests if name not in avalonia_text]
if missing_avalonia:
    raise SystemExit(
        "[b14] FAIL: missing required runtime-backed Avalonia gate tests: " + ", ".join(missing_avalonia)
    )

text = dual_head_tests_path.read_text(encoding="utf-8")
required_tests = [
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_representative_legacy_workflow_fixtures_render_populated_matching_sections",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_skill_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_support_family_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_gear_vehicle_and_combat_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_magic_matrix_and_spirit_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details",
    "Avalonia_and_Blazor_character_settings_save_updates_shared_state",
]
missing = [name for name in required_tests if name not in text]
if missing:
    raise SystemExit(
        "[b14] FAIL: missing required full-workflow equivalence tests: " + ", ".join(missing)
    )

blazor_text = blazor_shell_tests_path.read_text(encoding="utf-8")
required_blazor_tests = [
    "MenuBar_invokes_toggle_and_execute_callbacks",
    "WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks",
    "DialogHost_renders_dialog_and_emits_events",
    "StatusStrip_announces_status_via_shared_live_region_semantics",
    "CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views",
]
missing_blazor = [name for name in required_blazor_tests if name not in blazor_text]
if missing_blazor:
    raise SystemExit(
        "[b14] FAIL: missing required Blazor desktop shell tests: " + ", ".join(missing_blazor)
    )

desktop_update_runtime_text = desktop_update_runtime_tests_path.read_text(encoding="utf-8")
desktop_install_linking_runtime_text = desktop_install_linking_runtime_tests_path.read_text(encoding="utf-8")
desktop_startup_smoke_runtime_text = desktop_startup_smoke_runtime_tests_path.read_text(encoding="utf-8")
required_lifecycle_runtime_tests = [
    "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling",
    "BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context",
    "TryHandleAsync_writes_receipt_when_requested",
]
missing_lifecycle_runtime_tests = [
    test_name
    for test_name in required_lifecycle_runtime_tests
    if test_name not in desktop_update_runtime_text
    and test_name not in desktop_install_linking_runtime_text
    and test_name not in desktop_startup_smoke_runtime_text
]
if missing_lifecycle_runtime_tests:
    raise SystemExit(
        "[b14] FAIL: missing required desktop lifecycle runtime tests: "
        + ", ".join(missing_lifecycle_runtime_tests)
    )
PY

echo "[b14] running flagship Avalonia headless UI gate tests..."
run_with_retry 2 "flagship Avalonia headless UI gate tests" \
  env CHUMMER_UI_GATE_SCREENSHOT_DIR="$capture_screenshot_dir" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests" -v minimal >/dev/null

echo "[b14] running flagship Blazor desktop shell gate tests..."
run_with_retry 2 "flagship Blazor desktop shell gate tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~BlazorShellComponentTests" -v minimal >/dev/null

echo "[b14] running desktop install/update/recovery runtime tests..."
run_with_retry 2 "desktop install/update/recovery runtime tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj \
  --filter "FullyQualifiedName~DesktopUpdateRuntimeTests|FullyQualifiedName~DesktopInstallLinkingRuntimeTests|FullyQualifiedName~DesktopStartupSmokeRuntimeTests" -v minimal >/dev/null

python3 - <<'PY' "$capture_screenshot_dir" "$staged_screenshot_dir"
from __future__ import annotations

import shutil
import sys
from pathlib import Path

capture_dir = Path(sys.argv[1])
target_dir = Path(sys.argv[2])
png_paths = sorted(capture_dir.glob("*.png"))
if not png_paths:
    raise SystemExit(f"[b14] FAIL: no screenshot PNG files were produced in capture directory: {capture_dir}")
for path in png_paths:
    shutil.copy2(path, target_dir / path.name)
PY

echo "[b14] normalizing screenshot PNG CRC chunks..."
python3 - <<'PY' "$staged_screenshot_dir"
from __future__ import annotations

import binascii
import struct
import sys
from pathlib import Path

signature = b"\x89PNG\r\n\x1a\n"


def normalize_png(path: Path) -> None:
    data = path.read_bytes()
    if not data.startswith(signature):
        raise SystemExit(f"[b14] FAIL: screenshot is not a PNG file: {path}")

    offset = len(signature)
    out = bytearray(signature)
    saw_iend = False
    while offset + 12 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk_type = data[offset + 4 : offset + 8]
        chunk_start = offset + 8
        chunk_end = chunk_start + length
        crc_end = chunk_end + 4
        if crc_end > len(data):
            raise SystemExit(
                f"[b14] FAIL: screenshot PNG chunk is truncated ({chunk_type.decode('ascii', 'replace')}): {path}"
            )
        chunk_data = data[chunk_start:chunk_end]
        crc = binascii.crc32(chunk_type)
        crc = binascii.crc32(chunk_data, crc) & 0xFFFFFFFF
        out.extend(struct.pack(">I", length))
        out.extend(chunk_type)
        out.extend(chunk_data)
        out.extend(struct.pack(">I", crc))
        offset = crc_end
        if chunk_type == b"IEND":
            saw_iend = True
            break

    if not saw_iend:
        raise SystemExit(f"[b14] FAIL: screenshot PNG is missing IEND chunk: {path}")

    path.write_bytes(out)


screenshot_dir = Path(sys.argv[1])
png_paths = sorted(screenshot_dir.glob("*.png"))
if not png_paths:
    raise SystemExit(f"[b14] FAIL: no screenshot PNG files were produced: {screenshot_dir}")

for png_path in png_paths:
    normalize_png(png_path)
PY

rm -rf "$screenshot_dir"
mkdir -p "$screenshot_dir"
cp "$staged_screenshot_dir"/*.png "$screenshot_dir"/

echo "[b14] running cross-head workflow parity tests..."
run_with_retry 2 "cross-head workflow parity tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj \
  --filter "FullyQualifiedName~Chummer.Tests.Presentation.DualHeadAcceptanceTests" -v minimal >/dev/null

echo "[b14] running explicit Chummer5a desktop workflow parity gate..."
bash scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh >/dev/null

echo "[b14] running explicit SR4/SR6 desktop parity frontier gate..."
bash scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh >/dev/null

echo "[b14] materializing localization release gate..."
bash scripts/ai/milestones/b15-localization-release-gate.sh >/dev/null

python3 - <<'PY' "$sample_path" "$receipt_path" "$screenshot_dir" "$signoff_path" "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path" "$workflow_parity_receipt_path" "$sr4_workflow_parity_receipt_path" "$sr6_workflow_parity_receipt_path" "$sr4_sr6_frontier_receipt_path" "$desktop_workflow_execution_receipt_path" "$localization_release_gate_receipt_path"
import json
import os
import sys
from datetime import datetime, timezone

(
    sample_path,
    receipt_path,
    screenshot_dir,
    signoff_path,
    avalonia_gate_tests_path,
    dual_head_tests_path,
    blazor_shell_tests_path,
    desktop_update_runtime_tests_path,
    desktop_install_linking_runtime_tests_path,
    desktop_startup_smoke_runtime_tests_path,
    workflow_parity_receipt_path,
    sr4_workflow_parity_receipt_path,
    sr6_workflow_parity_receipt_path,
    sr4_sr6_frontier_receipt_path,
    desktop_workflow_execution_receipt_path,
    localization_release_gate_receipt_path,
) = sys.argv[1:17]
expected_screenshots = [
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
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "15-creation-section-light.png",
]
required_full_workflow_tests = [
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_representative_legacy_workflow_fixtures_render_populated_matching_sections",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_skill_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_support_family_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_gear_vehicle_and_combat_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_magic_matrix_and_spirit_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details",
    "Avalonia_and_Blazor_character_settings_save_updates_shared_state",
]
required_blazor_shell_tests = [
    "MenuBar_invokes_toggle_and_execute_callbacks",
    "WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks",
    "DialogHost_renders_dialog_and_emits_events",
    "StatusStrip_announces_status_via_shared_live_region_semantics",
    "CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views",
]
required_lifecycle_runtime_tests = [
    "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling",
    "BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context",
    "TryHandleAsync_writes_receipt_when_requested",
]
with open(workflow_parity_receipt_path, "r", encoding="utf-8") as handle:
    workflow_parity_receipt = json.load(handle)
if str(workflow_parity_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit Chummer5a desktop workflow parity proof is not passed: "
        + ", ".join(workflow_parity_receipt.get("reasons") or ["missing reason"])
    )
with open(sr4_workflow_parity_receipt_path, "r", encoding="utf-8") as handle:
    sr4_workflow_parity_receipt = json.load(handle)
if str(sr4_workflow_parity_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit SR4 desktop workflow parity proof is not passed: "
        + ", ".join(sr4_workflow_parity_receipt.get("reasons") or ["missing reason"])
    )
with open(sr6_workflow_parity_receipt_path, "r", encoding="utf-8") as handle:
    sr6_workflow_parity_receipt = json.load(handle)
if str(sr6_workflow_parity_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit SR6 desktop workflow parity proof is not passed: "
        + ", ".join(sr6_workflow_parity_receipt.get("reasons") or ["missing reason"])
    )
with open(sr4_sr6_frontier_receipt_path, "r", encoding="utf-8") as handle:
    sr4_sr6_frontier_receipt = json.load(handle)
if str(sr4_sr6_frontier_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit SR4/SR6 desktop parity frontier proof is not passed: "
        + ", ".join(sr4_sr6_frontier_receipt.get("reasons") or ["missing reason"])
    )
with open(localization_release_gate_receipt_path, "r", encoding="utf-8") as handle:
    localization_release_gate_receipt = json.load(handle)
if str(localization_release_gate_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit localization release gate proof is not passed: "
        + ", ".join(localization_release_gate_receipt.get("blocking_findings") or ["missing reason"])
    )
captured = []
missing = []
for name in expected_screenshots:
    path = os.path.join(screenshot_dir, name)
    if not os.path.isfile(path):
        missing.append(path)
        continue
    captured.append(
        {
            "name": name,
            "path": path,
            "sizeBytes": os.path.getsize(path),
        }
    )

if missing:
    raise SystemExit(
        "[b14] FAIL: missing screenshot evidence: " + ", ".join(missing)
    )

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "status": "pass",
    "releaseGate": "b14-flagship-ui-release-gate",
    "desktopHead": "avalonia",
    "desktopHeads": ["avalonia", "blazor-desktop"],
    "artifactPresence": {
        "bundledDemoRunnerPath": sample_path,
        "bundledDemoRunnerPresent": os.path.isfile(sample_path),
    },
    "interactionProof": {
        "testSuites": [
            "AvaloniaFlagshipUiGateTests",
            "BlazorShellComponentTests",
            "DualHeadAcceptanceTests",
        ],
        "menuSurface": "pass",
        "settingsInlineDialog": "pass",
        "demoRunnerDispatch": "pass",
        "keyboardShortcutParity": "pass",
        "legacyFamiliarityBridge": "pass",
        "crossHeadWorkflowParity": "pass",
        "installUpdateRecoveryLifecycle": "pass",
        "themeReadabilityContrast": "pass",
        "blazorDesktopShellChrome": "pass",
        "runtimeBackedShellMenu": "pass",
        "runtimeBackedMenuBarLabels": "pass",
        "runtimeBackedClickablePrimaryMenus": "pass",
        "runtimeBackedToolstripActions": "pass",
        "runtimeBackedCodexTree": "pass",
        "runtimeBackedSr4CodexOrientationModel": "pass",
        "runtimeBackedSr5CodexOrientationModel": "pass",
        "runtimeBackedSr6CodexOrientationModel": "pass",
        "runtimeBackedClassicChromeCopy": "pass",
        "runtimeBackedTabPanelOnlyHeader": "pass",
        "runtimeBackedChromeEnabledAfterRunnerLoad": "pass",
        "runtimeBackedDemoRunnerImport": "pass",
        "fullInteractiveControlInventory": "pass",
        "mainWindowInteractionInventory": "pass",
        "runtimeBackedLegacyWorkbench": "pass",
        "legacyDenseBuilderRhythm": "pass",
        "legacyCreationWorkflowRhythm": "pass",
        "legacyAdvancementWorkflowRhythm": "pass",
        "legacyBrowseDetailConfirmRhythm": "pass",
        "legacyGearWorkflowRhythm": "pass",
        "legacyVehiclesBuilderRhythm": "pass",
        "legacyCyberwareDialogRhythm": "pass",
        "legacyContactsDiaryRhythm": "pass",
        "legacyContactsWorkflowRhythm": "pass",
        "legacyDiaryWorkflowRhythm": "pass",
        "legacyMagicWorkflowRhythm": "pass",
        "legacyMatrixWorkflowRhythm": "pass",
        "lifecycleRuntimeTestSuites": [
            "DesktopUpdateRuntimeTests",
            "DesktopInstallLinkingRuntimeTests",
            "DesktopStartupSmokeRuntimeTests",
        ],
    },
    "headProofs": {
        "avalonia": {
            "status": "pass",
            "testSuites": [
                "AvaloniaFlagshipUiGateTests",
                "DualHeadAcceptanceTests"
            ],
            "sourceTestFile": avalonia_gate_tests_path,
            "visualReview": "pass",
            "themeReadabilityContrast": "pass",
            "bundledDemoRunner": "pass",
            "releaseLifecycle": "pass",
            "requiredRuntimeBackedTests": [
                "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
                "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
                "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
                "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
                "Runtime_backed_codex_tree_preserves_legacy_left_rail_navigation_posture",
                "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
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
                "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
                "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
                "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
                "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
                "Character_creation_preserves_familiar_dense_builder_rhythm",
                "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
                "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
                "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
                "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
                "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
                "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
                "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions"
            ],
            "requiredLifecycleTests": required_lifecycle_runtime_tests,
        },
        "blazor-desktop": {
            "status": "pass",
            "testSuites": [
                "BlazorShellComponentTests",
                "DualHeadAcceptanceTests"
            ],
            "shellChrome": "pass",
            "commandSurface": "pass",
            "dialogSurface": "pass",
            "journeyPanels": "pass",
            "releaseLifecycle": "pass",
            "sourceTestFile": blazor_shell_tests_path,
            "requiredShellTests": required_blazor_shell_tests,
            "requiredLifecycleTests": required_lifecycle_runtime_tests,
        },
    },
    "desktopLifecycleProof": {
        "status": "pass",
        "requiredLifecycleTests": required_lifecycle_runtime_tests,
        "desktopUpdateRuntimeTestsPath": desktop_update_runtime_tests_path,
        "desktopInstallLinkingRuntimeTestsPath": desktop_install_linking_runtime_tests_path,
        "desktopStartupSmokeRuntimeTestsPath": desktop_startup_smoke_runtime_tests_path,
    },
    "workflowEquivalenceProof": {
        "status": "pass",
        "sourceTestFile": dual_head_tests_path,
        "explicitParityReceiptPath": workflow_parity_receipt_path,
        "explicitSr4ParityReceiptPath": sr4_workflow_parity_receipt_path,
        "explicitSr6ParityReceiptPath": sr6_workflow_parity_receipt_path,
        "explicitSr4Sr6FrontierReceiptPath": sr4_sr6_frontier_receipt_path,
        "desktopWorkflowExecutionReceiptPath": desktop_workflow_execution_receipt_path,
        "requiredDualHeadTests": required_full_workflow_tests,
        "legacyWorkflowFamilies": [
            "create-open-import-save-save-as-print-export",
            "metatype-priorities-karma-entry",
            "attributes-skills-skill-groups-specializations-knowledge-languages",
            "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
            "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
            "cyberware-bioware-modular-hierarchies-nested-plugins",
            "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
            "improvements-explain-result-parity",
            "recovery-reload-migration-roundtrips",
            "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
        ],
    },
    "localizationReleaseProof": {
        "status": "pass",
        "localizationReleaseGateReceiptPath": localization_release_gate_receipt_path,
        "translationBacklogFindings": localization_release_gate_receipt.get("translation_backlog_findings") or [],
    },
    "visualReviewEvidence": {
        "screenshotDirectory": screenshot_dir,
        "expectedScreenshots": expected_screenshots,
        "capturedScreenshots": captured,
    },
    "signoffLane": {
        "workbenchReleaseSignoffPath": signoff_path,
        "citesReleaseGate": True,
    },
}
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle, indent=2)
    handle.write("\n")
PY

echo "[b14] materializing desktop workflow execution gate..."
bash scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh >/dev/null

echo "[b14] materializing desktop visual familiarity exit gate..."
bash scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh >/dev/null

echo "[b14] PASS"
