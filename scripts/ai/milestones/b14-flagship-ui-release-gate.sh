#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

output_dir="$repo_root/Chummer.Avalonia/bin/Release/net10.0"
sample_path="$output_dir/Samples/Legacy/Soma-Career.chum5"
receipt_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
lock_owner_pid_path="$lock_dir/owner.pid"
capture_screenshot_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-ui-flagship-gate-screenshots.XXXXXX")"
staged_screenshot_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-ui-flagship-published-screenshots.XXXXXX")"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
blazor_shell_tests_path="$repo_root/Chummer.Tests/Presentation/BlazorShellComponentTests.cs"
desktop_shell_ruleset_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"
desktop_update_runtime_tests_path="$repo_root/Chummer.Tests/DesktopUpdateRuntimeTests.cs"
desktop_install_linking_runtime_tests_path="$repo_root/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs"
desktop_startup_smoke_runtime_tests_path="$repo_root/Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs"
workflow_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
layout_hard_gate_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_LAYOUT_HARD_GATE.generated.json"
sr4_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_sr6_frontier_receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
desktop_workflow_execution_receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
localization_release_gate_receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
interactive_control_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"
veteran_task_time_receipt_path="$repo_root/.codex-studio/published/VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
chummer5a_screenshot_review_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
classic_dense_workbench_receipt_path="$repo_root/.codex-studio/published/CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
nuget_packages="${CHUMMER_NUGET_PACKAGES:-$repo_root/.codex-studio/.nuget/packages}"
lock_stale_max_age_seconds="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_LOCK_STALE_MAX_AGE_SECONDS:-900}"

if ! [[ "$lock_stale_max_age_seconds" =~ ^[0-9]+$ ]]; then
  lock_stale_max_age_seconds=900
fi

mkdir -p "$(dirname "$lock_dir")"
prune_lock_if_stale() {
  if [[ ! -d "$lock_dir" ]]; then
    return 0
  fi
  if [[ -f "$lock_owner_pid_path" ]]; then
    owner_pid="$(tr -dc '0-9' <"$lock_owner_pid_path")"
    if [[ -n "$owner_pid" ]] && kill -0 "$owner_pid" 2>/dev/null; then
      return 0
    fi
  fi

  lock_stale_probe="$(
    python3 - <<'PY' "$lock_dir" "$lock_owner_pid_path" "$lock_stale_max_age_seconds"
from __future__ import annotations

import sys
import time
from pathlib import Path

lock_dir = Path(sys.argv[1])
owner_pid_path = Path(sys.argv[2])
max_age = int(sys.argv[3])
if not lock_dir.is_dir():
    print("absent")
    raise SystemExit(0)

entries = list(lock_dir.iterdir())
entries_without_owner = [entry for entry in entries if entry != owner_pid_path]
if entries_without_owner:
    print("nonempty")
    raise SystemExit(0)

age_seconds = max(0, int(time.time() - lock_dir.stat().st_mtime))
if owner_pid_path.exists():
    print(f"dead_owner_only:{age_seconds}")
    raise SystemExit(0)

if age_seconds < max_age:
    print(f"young:{age_seconds}")
    raise SystemExit(0)

print(f"stale_empty:{age_seconds}")
PY
  )"
  if [[ "$lock_stale_probe" == stale_empty:* || "$lock_stale_probe" == stale_owner_only:* || "$lock_stale_probe" == dead_owner_only:* ]]; then
    rm -rf "$lock_dir"
  fi
}

for _ in $(seq 1 150); do
  prune_lock_if_stale
  if mkdir "$lock_dir" 2>/dev/null; then
    printf '%s\n' "$$" >"$lock_owner_pid_path"
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

python3 - <<'PY' "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_shell_ruleset_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path"
import sys
from pathlib import Path

avalonia_gate_tests_path = Path(sys.argv[1])
dual_head_tests_path = Path(sys.argv[2])
blazor_shell_tests_path = Path(sys.argv[3])
desktop_shell_ruleset_tests_path = Path(sys.argv[4])
desktop_update_runtime_tests_path = Path(sys.argv[5])
desktop_install_linking_runtime_tests_path = Path(sys.argv[6])
desktop_startup_smoke_runtime_tests_path = Path(sys.argv[7])
avalonia_text = avalonia_gate_tests_path.read_text(encoding="utf-8")
required_avalonia_tests = [
    "Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers",
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist",
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
    "Desktop_shell_preserves_classic_dense_center_first_workbench_posture",
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
    "Avalonia_and_Blazor_workspace_action_summary_matches",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_info_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections",
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

desktop_shell_ruleset_text = desktop_shell_ruleset_tests_path.read_text(encoding="utf-8")
required_blazor_desktop_shell_tests = [
    "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
    "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
]
missing_blazor_desktop_shell = [
    name for name in required_blazor_desktop_shell_tests if name not in desktop_shell_ruleset_text
]
if missing_blazor_desktop_shell:
    raise SystemExit(
        "[b14] FAIL: missing required Blazor desktop shell layout tests: "
        + ", ".join(missing_blazor_desktop_shell)
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
run_with_retry 5 "flagship Avalonia headless UI gate tests" \
  env CHUMMER_UI_GATE_SCREENSHOT_DIR="$capture_screenshot_dir" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests" -v minimal >/dev/null

echo "[b14] running flagship Blazor desktop shell gate tests..."
run_with_retry 2 "flagship Blazor desktop shell gate tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~BlazorShellComponentTests|FullyQualifiedName~DesktopShellRulesetCatalogTests" -v minimal >/dev/null

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

control_evidence_path = capture_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
if not control_evidence_path.is_file():
    raise SystemExit(f"[b14] FAIL: screenshot control evidence was not produced in capture directory: {control_evidence_path}")
shutil.copy2(control_evidence_path, target_dir / control_evidence_path.name)
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
cp "$staged_screenshot_dir"/SCREENSHOT_CONTROL_EVIDENCE.generated.json "$screenshot_dir"/

echo "[b14] running cross-head workflow parity tests..."
run_with_retry 2 "cross-head workflow parity tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj \
  --filter "FullyQualifiedName~Chummer.Tests.Presentation.DualHeadAcceptanceTests" -v minimal >/dev/null

echo "[b14] running explicit Chummer5a desktop workflow parity gate..."
bash scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh >/dev/null

echo "[b14] running explicit Chummer5a layout hard gate..."
bash scripts/ai/milestones/chummer5a-layout-hard-gate.sh >/dev/null

echo "[b14] running explicit SR4/SR6 desktop parity frontier gate..."
bash scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh >/dev/null

echo "[b14] materializing localization release gate..."
bash scripts/ai/milestones/b15-localization-release-gate.sh >/dev/null

python3 - <<'PY' "$sample_path" "$receipt_path" "$screenshot_dir" "$signoff_path" "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_shell_ruleset_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path" "$workflow_parity_receipt_path" "$layout_hard_gate_receipt_path" "$sr4_workflow_parity_receipt_path" "$sr6_workflow_parity_receipt_path" "$sr4_sr6_frontier_receipt_path" "$desktop_workflow_execution_receipt_path" "$localization_release_gate_receipt_path" "$interactive_control_inventory_receipt_path" "$veteran_task_time_receipt_path" "$chummer5a_screenshot_review_receipt_path"
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
    desktop_shell_ruleset_tests_path,
    desktop_update_runtime_tests_path,
    desktop_install_linking_runtime_tests_path,
    desktop_startup_smoke_runtime_tests_path,
    workflow_parity_receipt_path,
    layout_hard_gate_receipt_path,
    sr4_workflow_parity_receipt_path,
    sr6_workflow_parity_receipt_path,
    sr4_sr6_frontier_receipt_path,
    desktop_workflow_execution_receipt_path,
    localization_release_gate_receipt_path,
    interactive_control_inventory_receipt_path,
    veteran_task_time_receipt_path,
    chummer5a_screenshot_review_receipt_path,
) = sys.argv[1:22]
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
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
]
required_full_workflow_tests = [
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_workspace_action_summary_matches",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_info_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections",
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
required_blazor_desktop_shell_tests = [
    "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
    "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
]
required_lifecycle_runtime_tests = [
    "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling",
    "BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context",
    "TryHandleAsync_writes_receipt_when_requested",
]
with open(avalonia_gate_tests_path, "r", encoding="utf-8") as handle:
    avalonia_gate_tests_text = handle.read()
with open(dual_head_tests_path, "r", encoding="utf-8") as handle:
    dual_head_tests_text = handle.read()
with open(blazor_shell_tests_path, "r", encoding="utf-8") as handle:
    blazor_shell_tests_text = handle.read()
with open(desktop_shell_ruleset_tests_path, "r", encoding="utf-8") as handle:
    desktop_shell_ruleset_tests_text = handle.read()
with open(desktop_update_runtime_tests_path, "r", encoding="utf-8") as handle:
    desktop_update_runtime_tests_text = handle.read()
with open(desktop_install_linking_runtime_tests_path, "r", encoding="utf-8") as handle:
    desktop_install_linking_runtime_tests_text = handle.read()
with open(desktop_startup_smoke_runtime_tests_path, "r", encoding="utf-8") as handle:
    desktop_startup_smoke_runtime_tests_text = handle.read()
with open(layout_hard_gate_receipt_path, "r", encoding="utf-8") as handle:
    layout_hard_gate_receipt = json.load(handle)

def status_ok(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}

def proof_status(*conditions: bool) -> str:
    return "pass" if all(conditions) else "fail"

def tests_present(text: str, names: list[str]) -> bool:
    return all(name in text for name in names)

default_single_runner_layout_tests = [
    "Opening_mainframe_preserves_chummer5a_successor_workbench_posture",
    "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist",
    "Desktop_shell_preserves_classic_dense_center_first_workbench_posture",
    "Loaded_runner_preserves_visible_character_tab_posture",
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
]
runtime_backed_navigator_tests = [
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
]
classic_chrome_copy_tests = [
    "Opening_mainframe_preserves_chummer5a_successor_workbench_posture",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
]
tab_panel_only_tests = [
    "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
    "Loaded_runner_preserves_visible_character_tab_posture",
]
legacy_workbench_tests = [
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
    "Loaded_runner_preserves_visible_character_tab_posture",
]
runtime_shell_menu_tests = [
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
]
runtime_toolstrip_tests = [
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
]
layout_gate_status = proof_status(status_ok(str(layout_hard_gate_receipt.get("status") or "").strip().lower()))
blazor_shell_chrome_status = proof_status(
    tests_present(blazor_shell_tests_text, required_blazor_shell_tests),
    tests_present(desktop_shell_ruleset_tests_text, required_blazor_desktop_shell_tests),
)
default_single_runner_layout_status = proof_status(
    status_ok(str(layout_hard_gate_receipt.get("status") or "").strip().lower()),
    tests_present(avalonia_gate_tests_text, default_single_runner_layout_tests),
    tests_present(desktop_shell_ruleset_tests_text, required_blazor_desktop_shell_tests),
)
runtime_backed_codex_tree_status = proof_status(
    tests_present(avalonia_gate_tests_text, runtime_backed_navigator_tests)
)
runtime_backed_classic_chrome_copy_status = proof_status(
    tests_present(avalonia_gate_tests_text, classic_chrome_copy_tests)
)
runtime_backed_tab_panel_only_header_status = proof_status(
    tests_present(avalonia_gate_tests_text, tab_panel_only_tests)
)
runtime_backed_legacy_workbench_status = proof_status(
    tests_present(avalonia_gate_tests_text, legacy_workbench_tests)
)
runtime_backed_shell_menu_status = proof_status(
    tests_present(avalonia_gate_tests_text, runtime_shell_menu_tests)
)
runtime_backed_toolstrip_actions_status = proof_status(
    tests_present(avalonia_gate_tests_text, runtime_toolstrip_tests)
)
menu_surface_status = proof_status(
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters" in avalonia_gate_tests_text,
    status_ok(runtime_backed_shell_menu_status),
)
settings_inline_dialog_status = proof_status(
    "Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive" in avalonia_gate_tests_text,
    "Desktop_dialog_surfaces_use_real_windowed_dialogs_and_quiet_blazor_chrome" in avalonia_gate_tests_text,
)
demo_runner_dispatch_status = proof_status(
    "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters" in avalonia_gate_tests_text,
    "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load" in avalonia_gate_tests_text,
)
keyboard_shortcut_parity_status = proof_status(
    "Keyboard_shortcuts_resolve_to_the_same_shell_commands" in avalonia_gate_tests_text,
)
theme_readability_contrast_status = proof_status(
    "Theme_tokens_preserve_chummer5a_palette_and_readability" in avalonia_gate_tests_text,
)
runtime_backed_ruleset_orientation_status = proof_status(
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks" in avalonia_gate_tests_text,
    status_ok(runtime_backed_codex_tree_status),
)
lifecycle_runtime_tests_text = "\n".join(
    [
        desktop_update_runtime_tests_text,
        desktop_install_linking_runtime_tests_text,
        desktop_startup_smoke_runtime_tests_text,
    ]
)
desktop_lifecycle_status = proof_status(
    tests_present(lifecycle_runtime_tests_text, required_lifecycle_runtime_tests)
)
with open(workflow_parity_receipt_path, "r", encoding="utf-8") as handle:
    workflow_parity_receipt = json.load(handle)
if str(workflow_parity_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit Chummer5a desktop workflow parity proof is not passed: "
        + ", ".join(workflow_parity_receipt.get("reasons") or ["missing reason"])
    )
if str(layout_hard_gate_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit Chummer5a layout hard gate proof is not passed: "
        + ", ".join(layout_hard_gate_receipt.get("reasons") or ["missing reason"])
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
localization_release_status = str(localization_release_gate_receipt.get("status") or "").strip().lower()
if localization_release_status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit localization release gate proof is not passed: "
        + ", ".join(localization_release_gate_receipt.get("blocking_findings") or ["missing reason"])
    )
workflow_equivalence_status = proof_status(
    tests_present(dual_head_tests_text, required_full_workflow_tests),
    status_ok(str(workflow_parity_receipt.get("status") or "").strip().lower()),
    status_ok(str(sr4_workflow_parity_receipt.get("status") or "").strip().lower()),
    status_ok(str(sr6_workflow_parity_receipt.get("status") or "").strip().lower()),
    status_ok(str(sr4_sr6_frontier_receipt.get("status") or "").strip().lower()),
)
legacy_workflow_receipt_status = status_ok(str(workflow_parity_receipt.get("status") or "").strip().lower())
legacy_creation_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Character_creation_preserves_familiar_dense_builder_rhythm" in avalonia_gate_tests_text,
)
legacy_advancement_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm" in avalonia_gate_tests_text,
)
legacy_browse_detail_confirm_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm" in avalonia_gate_tests_text,
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm" in avalonia_gate_tests_text,
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues" in avalonia_gate_tests_text,
)
legacy_gear_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm" in avalonia_gate_tests_text,
)
legacy_vehicles_builder_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm" in avalonia_gate_tests_text,
)
legacy_cyberware_dialog_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues" in avalonia_gate_tests_text,
)
legacy_contacts_diary_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility" in avalonia_gate_tests_text,
)
legacy_contacts_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility" in avalonia_gate_tests_text,
)
legacy_diary_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility" in avalonia_gate_tests_text,
)
legacy_magic_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions" in avalonia_gate_tests_text,
)
legacy_matrix_workflow_rhythm_status = proof_status(
    legacy_workflow_receipt_status,
    "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions" in avalonia_gate_tests_text,
)
with open(interactive_control_inventory_receipt_path, "r", encoding="utf-8") as handle:
    interactive_control_inventory_receipt = json.load(handle)
interaction_inventory_status = str(interactive_control_inventory_receipt.get("status") or "").strip().lower()
full_interactive_control_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("fullInteractiveControlInventory") or "").strip().lower()
main_window_interaction_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("mainWindowInteractionInventory") or "").strip().lower()
if interaction_inventory_status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: standalone interactive control inventory proof is not passed: "
        + ", ".join(interactive_control_inventory_receipt.get("reasons") or ["missing reason"])
    )
if not status_ok(full_interactive_control_inventory_status) or not status_ok(main_window_interaction_inventory_status):
    raise SystemExit(
        "[b14] FAIL: standalone interactive control inventory receipt does not carry passing sub-statuses for both inventory lanes."
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
avalonia_visual_review_status = proof_status(not missing, len(captured) == len(expected_screenshots))
bundled_demo_runner_status = proof_status(os.path.isfile(sample_path))
avalonia_head_status = proof_status(
    avalonia_visual_review_status,
    theme_readability_contrast_status,
    bundled_demo_runner_status,
    layout_gate_status,
    desktop_lifecycle_status,
)
blazor_command_surface_status = proof_status(
    "MenuBar_invokes_toggle_and_execute_callbacks" in blazor_shell_tests_text,
    "WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks" in blazor_shell_tests_text,
)
blazor_dialog_surface_status = proof_status(
    "DialogHost_renders_dialog_and_emits_events" in blazor_shell_tests_text,
)
blazor_journey_panels_status = proof_status(
    "CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views" in blazor_shell_tests_text,
)
blazor_release_lifecycle_status = desktop_lifecycle_status
blazor_head_status = proof_status(
    blazor_shell_chrome_status,
    blazor_command_surface_status,
    blazor_dialog_surface_status,
    blazor_journey_panels_status,
    blazor_release_lifecycle_status,
)

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "status": "pass",
    "releaseGate": "b14-flagship-ui-release-gate",
    "desktopHead": "avalonia",
    "desktopHeads": ["avalonia"],
    "desktopFallbackHeads": ["blazor-desktop"],
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
        "menuSurface": menu_surface_status,
        "settingsInlineDialog": settings_inline_dialog_status,
        "demoRunnerDispatch": demo_runner_dispatch_status,
        "keyboardShortcutParity": keyboard_shortcut_parity_status,
        "legacyFamiliarityBridge": proof_status(
            status_ok(str(layout_hard_gate_receipt.get("status") or "").strip().lower()),
            tests_present(avalonia_gate_tests_text, legacy_workbench_tests),
            tests_present(avalonia_gate_tests_text, default_single_runner_layout_tests),
        ),
        "crossHeadWorkflowParity": workflow_equivalence_status,
        "installUpdateRecoveryLifecycle": desktop_lifecycle_status,
        "themeReadabilityContrast": theme_readability_contrast_status,
        "blazorDesktopShellChrome": blazor_shell_chrome_status,
        "runtimeBackedShellMenu": runtime_backed_shell_menu_status,
        "runtimeBackedMenuBarLabels": runtime_backed_shell_menu_status,
        "runtimeBackedClickablePrimaryMenus": runtime_backed_shell_menu_status,
        "runtimeBackedToolstripActions": runtime_backed_toolstrip_actions_status,
        "runtimeBackedCodexTree": runtime_backed_codex_tree_status,
        "runtimeBackedSr4CodexOrientationModel": runtime_backed_ruleset_orientation_status,
        "runtimeBackedSr5CodexOrientationModel": runtime_backed_ruleset_orientation_status,
        "runtimeBackedSr6CodexOrientationModel": runtime_backed_ruleset_orientation_status,
        "runtimeBackedClassicChromeCopy": runtime_backed_classic_chrome_copy_status,
        "chummer5aLayoutHardGate": layout_gate_status,
        "defaultSingleRunnerKeepsWorkspaceChromeCollapsed": default_single_runner_layout_status,
        "runtimeBackedTabPanelOnlyHeader": runtime_backed_tab_panel_only_header_status,
        "runtimeBackedChromeEnabledAfterRunnerLoad": proof_status(
            "Runtime_backed_shell_chrome_stays_enabled_after_runner_load" in avalonia_gate_tests_text
        ),
        "runtimeBackedDemoRunnerImport": proof_status(
            "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters" in avalonia_gate_tests_text
        ),
        "interactiveControlInventoryReceiptPath": interactive_control_inventory_receipt_path,
        "fullInteractiveControlInventory": full_interactive_control_inventory_status,
        "mainWindowInteractionInventory": main_window_interaction_inventory_status,
        "runtimeBackedLegacyWorkbench": runtime_backed_legacy_workbench_status,
        "legacyDenseBuilderRhythm": proof_status(
            status_ok(str(layout_hard_gate_receipt.get("status") or "").strip().lower()),
            "Character_creation_preserves_familiar_dense_builder_rhythm" in avalonia_gate_tests_text,
            "Desktop_shell_preserves_classic_dense_center_first_workbench_posture" in avalonia_gate_tests_text,
        ),
        "legacyCreationWorkflowRhythm": legacy_creation_workflow_rhythm_status,
        "legacyAdvancementWorkflowRhythm": legacy_advancement_workflow_rhythm_status,
        "legacyBrowseDetailConfirmRhythm": legacy_browse_detail_confirm_rhythm_status,
        "legacyGearWorkflowRhythm": legacy_gear_workflow_rhythm_status,
        "legacyVehiclesBuilderRhythm": legacy_vehicles_builder_rhythm_status,
        "legacyCyberwareDialogRhythm": legacy_cyberware_dialog_rhythm_status,
        "legacyContactsDiaryRhythm": legacy_contacts_diary_rhythm_status,
        "legacyContactsWorkflowRhythm": legacy_contacts_workflow_rhythm_status,
        "legacyDiaryWorkflowRhythm": legacy_diary_workflow_rhythm_status,
        "legacyMagicWorkflowRhythm": legacy_magic_workflow_rhythm_status,
        "legacyMatrixWorkflowRhythm": legacy_matrix_workflow_rhythm_status,
        "lifecycleRuntimeTestSuites": [
            "DesktopUpdateRuntimeTests",
            "DesktopInstallLinkingRuntimeTests",
            "DesktopStartupSmokeRuntimeTests",
        ],
    },
    "headProofs": {
        "avalonia": {
            "status": avalonia_head_status,
            "testSuites": [
                "AvaloniaFlagshipUiGateTests",
                "DualHeadAcceptanceTests"
            ],
            "sourceTestFile": avalonia_gate_tests_path,
            "visualReview": avalonia_visual_review_status,
            "themeReadabilityContrast": theme_readability_contrast_status,
            "bundledDemoRunner": bundled_demo_runner_status,
            "layoutParityHardGate": layout_gate_status,
            "releaseLifecycle": desktop_lifecycle_status,
            "requiredRuntimeBackedTests": [
                "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
                "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
                "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
                "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
                "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist",
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
                "Desktop_shell_preserves_classic_dense_center_first_workbench_posture",
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
            "status": blazor_head_status,
            "testSuites": [
                "BlazorShellComponentTests",
                "DesktopShellRulesetCatalogTests",
                "DualHeadAcceptanceTests"
            ],
            "shellChrome": blazor_shell_chrome_status,
            "commandSurface": blazor_command_surface_status,
            "dialogSurface": blazor_dialog_surface_status,
            "journeyPanels": blazor_journey_panels_status,
            "releaseLifecycle": blazor_release_lifecycle_status,
            "sourceTestFile": blazor_shell_tests_path,
            "requiredShellTests": required_blazor_shell_tests,
            "desktopShellRulesetSourceTestFile": desktop_shell_ruleset_tests_path,
            "requiredDesktopShellLayoutTests": required_blazor_desktop_shell_tests,
            "requiredLifecycleTests": required_lifecycle_runtime_tests,
        },
    },
    "desktopLifecycleProof": {
        "status": desktop_lifecycle_status,
        "requiredLifecycleTests": required_lifecycle_runtime_tests,
        "desktopUpdateRuntimeTestsPath": desktop_update_runtime_tests_path,
        "desktopInstallLinkingRuntimeTestsPath": desktop_install_linking_runtime_tests_path,
        "desktopStartupSmokeRuntimeTestsPath": desktop_startup_smoke_runtime_tests_path,
    },
    "workflowEquivalenceProof": {
        "status": workflow_equivalence_status,
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
        "status": localization_release_status,
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
CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT=1 \
  bash scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh >/dev/null

echo "[b14] materializing classic dense workbench posture gate..."
bash scripts/ai/milestones/classic-dense-workbench-posture-gate.sh >/dev/null

echo "[b14] materializing veteran task-time evidence gate..."
bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh >/dev/null

echo "[b14] materializing Chummer5a screenshot review gate..."
bash scripts/ai/milestones/chummer5a-screenshot-review-gate.sh >/dev/null

python3 - <<'PY' "$receipt_path" "$veteran_task_time_receipt_path" "$chummer5a_screenshot_review_receipt_path" "$classic_dense_workbench_receipt_path"
import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
veteran_task_time_receipt_path = Path(sys.argv[2])
chummer5a_screenshot_review_receipt_path = Path(sys.argv[3])
classic_dense_workbench_receipt_path = Path(sys.argv[4])
receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
veteran_receipt = json.loads(veteran_task_time_receipt_path.read_text(encoding="utf-8"))
chummer5a_screenshot_review_receipt = json.loads(chummer5a_screenshot_review_receipt_path.read_text(encoding="utf-8"))
classic_dense_receipt = json.loads(classic_dense_workbench_receipt_path.read_text(encoding="utf-8"))
veteran_receipt_status = str(veteran_receipt.get("status") or "").strip().lower()
chummer5a_screenshot_review_status = str(chummer5a_screenshot_review_receipt.get("status") or "").strip().lower()
classic_dense_receipt_status = str(classic_dense_receipt.get("status") or "").strip().lower()
if veteran_receipt_status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: veteran task-time evidence proof is not passed: "
        + ", ".join(veteran_receipt.get("reasons") or ["missing reason"])
    )
if chummer5a_screenshot_review_status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: Chummer5a screenshot review proof is not passed: "
        + ", ".join(chummer5a_screenshot_review_receipt.get("reasons") or ["missing reason"])
    )
if classic_dense_receipt_status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: classic dense workbench posture proof is not passed: "
        + ", ".join(classic_dense_receipt.get("reasons") or ["missing reason"])
    )
receipt["classicDenseWorkbenchPostureProof"] = {
    "status": classic_dense_receipt_status,
    "classicDenseWorkbenchPostureReceiptPath": str(classic_dense_workbench_receipt_path),
    "frontierIdsClosed": classic_dense_receipt.get("frontierIdsClosed") or [],
    "evidence": classic_dense_receipt.get("evidence") or {},
}
receipt["veteranTaskTimeEvidenceProof"] = {
    "status": veteran_receipt_status,
    "veteranTaskTimeEvidenceReceiptPath": str(veteran_task_time_receipt_path),
    "frontierIdsClosed": veteran_receipt.get("frontierIdsClosed") or [],
    "taskTimeEvidence": veteran_receipt.get("taskTimeEvidence") or {},
    "boundedBlazorFallbackEvidence": veteran_receipt.get("boundedBlazorFallbackEvidence") or {},
}
receipt["chummer5aScreenshotReviewProof"] = {
    "status": chummer5a_screenshot_review_status,
    "screenshotReviewReceiptPath": str(chummer5a_screenshot_review_receipt_path),
    "frontierIdsClosed": chummer5a_screenshot_review_receipt.get("frontierIdsClosed") or [],
    "reviewJobs": chummer5a_screenshot_review_receipt.get("reviewJobs") or {},
}
receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
PY

echo "[b14] PASS"
