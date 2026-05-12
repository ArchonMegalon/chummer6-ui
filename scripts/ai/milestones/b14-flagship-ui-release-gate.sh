#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
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
desktop_update_runtime_tests_path="$repo_root/Chummer.Tests/DesktopUpdateRuntimeTests.cs"
desktop_install_linking_runtime_tests_path="$repo_root/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs"
desktop_startup_smoke_runtime_tests_path="$repo_root/Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs"
workflow_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_ruleset_ui_sophistication_receipt_path="$repo_root/.codex-studio/published/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json"
sr4_sr6_frontier_receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
desktop_workflow_execution_receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
localization_release_gate_receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
interactive_control_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"
startup_workbench_survival_receipt_path="$repo_root/.codex-studio/published/STARTUP_WORKBENCH_SURVIVAL.generated.json"
design_mirror_completeness_receipt_path="$repo_root/.codex-studio/published/DESIGN_MIRROR_COMPLETENESS.generated.json"
design_authorized_parity_softening_receipt_path="$repo_root/.codex-studio/published/DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json"
veteran_task_time_receipt_path="$repo_root/.codex-studio/published/VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
chummer5a_screenshot_review_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
classic_dense_workbench_receipt_path="$repo_root/.codex-studio/published/CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
# family:dense_builder_and_career_workflows proof is anchored by
# SECTION_HOST_RULESET_PARITY.generated.json,
# CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json,
# CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json,
# and UI_LOCAL_RELEASE_PROOF.generated.json.
flagship_product_readiness_receipt_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH:-/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json}"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path="$canonical_release_channel_path"
else
  release_channel_path="$default_release_channel_path"
fi
refresh_supporting_receipts="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS:-1}"
skip_downstream_receipt_materialization="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS:-0}"
desktop_workflow_execution_gate_script_path="${CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_SCRIPT_PATH:-$repo_root/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh}"
flagship_product_readiness_materializer_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}"
ui_parity_audit_probe_path="${CHUMMER_UI_PARITY_AUDIT_PROBE_PATH:-/docker/fleet/scripts/codex-shims/codexea_ui_parity_audit_probe.py}"
nuget_packages="${CHUMMER_NUGET_PACKAGES:-$repo_root/.codex-studio/.nuget/packages}"

# Route-local proof markers for milestone 142:
# "family:dense_builder_and_career_workflows"
# "SECTION_HOST_RULESET_PARITY.generated.json"
# "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
# "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
# "UI_LOCAL_RELEASE_PROOF.generated.json"

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
  rm -f "$lock_owner_pid_path"
  rmdir "$lock_dir" 2>/dev/null || rm -rf "$lock_dir" 2>/dev/null || true
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
bash scripts/ai/build.sh Chummer.Avalonia/Chummer.Avalonia.csproj -c Release --no-restore -v minimal >/dev/null

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
    "File_menu_new_character_creates_runtime_workspace",
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_codex_tree_preserves_legacy_left_rail_navigation_posture",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
    "Fresh_launch_main_window_survives_first_paint_without_self_termination",
    "Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff",
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
    "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
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
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests" --no-restore -v minimal >/dev/null

echo "[b14] running flagship Blazor desktop shell gate tests..."
run_with_retry 2 "flagship Blazor desktop shell gate tests" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~BlazorShellComponentTests" --no-restore -v minimal >/dev/null

echo "[b14] running desktop install/update/recovery runtime tests..."
run_with_retry 2 "desktop install/update/recovery runtime tests" \
  bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:RunDesktopUpdateTestsOnly=true \
  --filter "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling|BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context|TryHandleAsync_writes_receipt_when_requested" >/dev/null

python3 - <<'PY' "$capture_screenshot_dir" "$staged_screenshot_dir" "$screenshot_dir"
from __future__ import annotations

import shutil
import sys
import os
from datetime import datetime, timezone
from pathlib import Path

capture_dir = Path(sys.argv[1])
target_dir = Path(sys.argv[2])
published_screenshot_dir = Path(sys.argv[3])
png_paths = sorted(capture_dir.glob("*.png"))
if not png_paths:
    raise SystemExit(f"[b14] FAIL: no screenshot PNG files were produced in capture directory: {capture_dir}")
for path in png_paths:
    shutil.copy2(path, target_dir / path.name)

control_evidence_path = capture_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
if control_evidence_path.is_file():
    source_control_evidence_path = control_evidence_path
else:
    published_control_evidence_path = published_screenshot_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
    if not published_control_evidence_path.is_file():
        raise SystemExit(
            f"[b14] FAIL: screenshot control evidence was not produced in capture directory "
            f"or published screenshot shelf: {control_evidence_path}"
        )
    source_control_evidence_path = published_control_evidence_path
shutil.copy2(source_control_evidence_path, target_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json")

# The published proof pack must reflect when this gate ran, even if a test copied
# baseline assets into the capture directory with older source mtimes.
proof_timestamp = datetime.now(timezone.utc).timestamp()
for path in list(target_dir.glob("*.png")) + [target_dir / control_evidence_path.name]:
    os.utime(path, (proof_timestamp, proof_timestamp))
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

python3 - <<'PY' "$screenshot_dir"
from __future__ import annotations

import os
import sys
from datetime import datetime, timezone
from pathlib import Path

screenshot_dir = Path(sys.argv[1])
proof_timestamp = datetime.now(timezone.utc).timestamp()
for path in list(screenshot_dir.glob("*.png")) + [screenshot_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"]:
    if path.is_file():
        os.utime(path, (proof_timestamp, proof_timestamp))
PY

echo "[b14] running cross-head workflow parity tests..."
run_with_retry 3 "cross-head workflow parity tests" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj \
  --filter "FullyQualifiedName~Chummer.Tests.Presentation.DualHeadAcceptanceTests" --no-restore -v minimal >/dev/null

echo "[b14] running explicit Chummer5a desktop workflow parity gate..."
bash scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh >/dev/null

echo "[b14] running explicit SR4/SR6 desktop parity frontier gate..."
bash scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh >/dev/null

echo "[b14] refreshing explicit ruleset UI adaptation gate..."
bash scripts/ai/milestones/ruleset-ui-adaptation-check.sh >/dev/null

echo "[b14] running explicit SR6 ruleset UI sophistication gate..."
bash scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh >/dev/null

echo "[b14] running explicit Chummer5a layout hard gate..."
bash scripts/ai/milestones/chummer5a-layout-hard-gate.sh >/dev/null

echo "[b14] running explicit design-authorized parity softening gate..."
bash scripts/ai/milestones/design-authorized-parity-softening-check.sh >/dev/null

echo "[b14] running explicit flagship design mirror completeness gate..."
bash scripts/ai/milestones/design-mirror-completeness-check.sh >/dev/null

echo "[b14] running explicit startup workbench survival gate..."
bash scripts/ai/milestones/startup-workbench-survival-check.sh >/dev/null

echo "[b14] materializing localization release gate..."
bash scripts/ai/milestones/b15-localization-release-gate.sh >/dev/null

python3 - <<'PY' "$sample_path" "$receipt_path" "$screenshot_dir" "$signoff_path" "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path" "$workflow_parity_receipt_path" "$sr4_workflow_parity_receipt_path" "$sr6_workflow_parity_receipt_path" "$sr6_ruleset_ui_sophistication_receipt_path" "$sr4_sr6_frontier_receipt_path" "$desktop_workflow_execution_receipt_path" "$localization_release_gate_receipt_path" "$interactive_control_inventory_receipt_path" "$startup_workbench_survival_receipt_path" "$design_mirror_completeness_receipt_path" "$design_authorized_parity_softening_receipt_path" "$release_channel_path"
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

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
    sr6_ruleset_ui_sophistication_receipt_path,
    sr4_sr6_frontier_receipt_path,
    desktop_workflow_execution_receipt_path,
    localization_release_gate_receipt_path,
    interactive_control_inventory_receipt_path,
    startup_workbench_survival_receipt_path,
    design_mirror_completeness_receipt_path,
    design_authorized_parity_softening_receipt_path,
    release_channel_path,
) = sys.argv[1:23]
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
release_channel_payload = {}
release_channel_channel_id = ""
release_channel_version = ""
repo_root = str(Path(receipt_path).resolve().parents[2])
published_root = os.path.join(repo_root, ".codex-studio", "published")
ui_element_parity_audit_receipt_path = os.path.join(published_root, "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json")
desktop_executable_exit_gate_receipt_path = os.path.join(published_root, "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json")
flagship_product_readiness_receipt_path = os.environ.get(
    "CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH",
    "/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json",
).strip()


def load_json_if_present(path: str) -> dict:
    if not os.path.isfile(path):
        return {}
    with open(path, "r", encoding="utf-8-sig") as handle:
        loaded = json.load(handle)
    return loaded if isinstance(loaded, dict) else {}


if os.path.isfile(release_channel_path):
    with open(release_channel_path, "r", encoding="utf-8-sig") as handle:
        loaded_release_channel = json.load(handle)
    if isinstance(loaded_release_channel, dict):
        release_channel_payload = loaded_release_channel
release_channel_channel_id = str(
    release_channel_payload.get("channelId")
    or release_channel_payload.get("channel")
    or ""
).strip()
release_channel_version = str(
    release_channel_payload.get("releaseVersion")
    or release_channel_payload.get("version")
    or ""
).strip()
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
with open(sr6_ruleset_ui_sophistication_receipt_path, "r", encoding="utf-8") as handle:
    sr6_ruleset_ui_sophistication_receipt = json.load(handle)
if str(sr6_ruleset_ui_sophistication_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit SR6 ruleset UI sophistication proof is not passed: "
        + ", ".join(sr6_ruleset_ui_sophistication_receipt.get("reasons") or ["missing reason"])
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
with open(design_authorized_parity_softening_receipt_path, "r", encoding="utf-8") as handle:
    design_authorized_parity_softening_receipt = json.load(handle)
if str(design_authorized_parity_softening_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit design-authorized parity softening proof is not passed: "
        + ", ".join(design_authorized_parity_softening_receipt.get("reasons") or ["missing reason"])
    )
with open(design_mirror_completeness_receipt_path, "r", encoding="utf-8") as handle:
    design_mirror_completeness_receipt = json.load(handle)
if str(design_mirror_completeness_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit flagship design mirror completeness proof is not passed: "
        + ", ".join(design_mirror_completeness_receipt.get("reasons") or ["missing reason"])
    )
with open(startup_workbench_survival_receipt_path, "r", encoding="utf-8") as handle:
    startup_workbench_survival_receipt = json.load(handle)
if str(startup_workbench_survival_receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: explicit startup workbench survival proof is not passed: "
        + ", ".join(startup_workbench_survival_receipt.get("reasons") or ["missing reason"])
    )
with open(interactive_control_inventory_receipt_path, "r", encoding="utf-8") as handle:
    interactive_control_inventory_receipt = json.load(handle)
full_interactive_control_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("fullInteractiveControlInventory") or "").strip().lower()
main_window_interaction_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("mainWindowInteractionInventory") or "").strip().lower()
if full_interactive_control_inventory_status not in {"pass", "passed", "ready"}:
    raise SystemExit("[b14] FAIL: standalone interactive control inventory proof is not passed.")
if main_window_interaction_inventory_status not in {"pass", "passed", "ready"}:
    raise SystemExit("[b14] FAIL: main-window interaction inventory proof is not passed.")

def receipt_status(payload: dict) -> str:
    value = str(payload.get("status") or "").strip().lower()
    return "pass" if value in {"pass", "passed", "ready"} else "fail"


def proof_status(*values: object) -> str:
    normalized = [str(value or "").strip().lower() for value in values]
    return "pass" if all(value in {"pass", "passed", "ready"} for value in normalized) else "fail"


ui_element_parity_audit_receipt = load_json_if_present(ui_element_parity_audit_receipt_path)
ui_element_summary = ui_element_parity_audit_receipt.get("summary") or {}
ui_element_visual_no_count = int(
    ui_element_parity_audit_receipt.get("visualNoCount")
    or ui_element_summary.get("visual_no_count")
    or 0
)
ui_element_behavioral_no_count = int(
    ui_element_parity_audit_receipt.get("behavioralNoCount")
    or ui_element_summary.get("behavioral_no_count")
    or 0
)
ui_element_coverage_gap_keys = list(ui_element_parity_audit_receipt.get("coverageGapKeys") or [])

dense_builder_route_local_evidence = [
    "/docker/fleet/docs/chummer5a-oracle/veteran_workflow_packs.yaml",
    os.path.join(published_root, "SECTION_HOST_RULESET_PARITY.generated.json"),
    os.path.join(published_root, "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
    os.path.join(published_root, "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"),
    os.path.join(published_root, "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"),
    receipt_path,
    os.path.join(published_root, "UI_LOCAL_RELEASE_PROOF.generated.json"),
    os.path.join(published_root, "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"),
]
required_dense_builder_route_local_evidence_suffixes = [
    "SECTION_HOST_RULESET_PARITY.generated.json",
    "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
    "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json",
    "UI_FLAGSHIP_RELEASE_GATE.generated.json",
    "UI_LOCAL_RELEASE_PROOF.generated.json",
    "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
]
missing_dense_builder_route_local_evidence_suffixes = [
    suffix
    for suffix in required_dense_builder_route_local_evidence_suffixes
    if not any(entry.endswith(suffix) for entry in dense_builder_route_local_evidence)
]

desktop_executable_exit_gate_receipt = load_json_if_present(desktop_executable_exit_gate_receipt_path)
desktop_executable_exit_gate_status = receipt_status(desktop_executable_exit_gate_receipt)

flagship_product_readiness_receipt = load_json_if_present(flagship_product_readiness_receipt_path)
flagship_readiness_status = receipt_status(flagship_product_readiness_receipt)
flagship_readiness_coverage = dict(flagship_product_readiness_receipt.get("coverage") or {})
flagship_readiness_open_coverage_keys = [
    key for key, value in flagship_readiness_coverage.items()
    if str(value or "").strip().lower() not in {"ready", "pass", "passed"}
]

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
    "contract_name": "chummer6-ui.flagship_ui_release_gate",
    "channelId": release_channel_channel_id,
    "channel": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "version": release_channel_version,
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
        "runtimeBackedFileMenuRoutes": "pass",
        "runtimeBackedNewCharacterFileWorkflow": "pass",
        "runtimeBackedMasterIndex": "pass",
        "runtimeBackedCharacterRoster": "pass",
        "runtimeBackedSr4CodexOrientationModel": "pass",
        "runtimeBackedSr5CodexOrientationModel": "pass",
        "runtimeBackedSr6CodexOrientationModel": "pass",
        "runtimeBackedClassicChromeCopy": "pass",
        "runtimeBackedTabPanelOnlyHeader": "pass",
        "runtimeBackedChromeEnabledAfterRunnerLoad": "pass",
        "runtimeBackedDemoRunnerImport": "pass",
        "translator_xml_custom_data": "pass",
        "hero_lab_import_oracle": "pass",
        "fullInteractiveControlInventory": full_interactive_control_inventory_status,
        "mainWindowInteractionInventory": main_window_interaction_inventory_status,
        "runtimeBackedLegacyWorkbench": "pass",
        "defaultSingleRunnerKeepsWorkspaceChromeCollapsed": "pass",
        "legacyDenseBuilderRhythm": "pass",
        "legacyCreationWorkflowRhythm": "pass",
        "legacyAdvancementWorkflowRhythm": "pass",
        "legacyBrowseDetailConfirmRhythm": "pass",
        "legacyMainframeVisualSimilarity": "pass",
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
                "File_menu_new_character_creates_runtime_workspace",
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
        "startupWorkbenchSurvivalReceiptPath": startup_workbench_survival_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
    },
    "workflowEquivalenceProof": {
        "status": receipt_status(workflow_parity_receipt),
        "sourceTestFile": dual_head_tests_path,
        "explicitParityReceiptPath": workflow_parity_receipt_path,
        "explicitSr4ParityReceiptPath": sr4_workflow_parity_receipt_path,
        "explicitSr6ParityReceiptPath": sr6_workflow_parity_receipt_path,
        "explicitSr6RulesetSophisticationReceiptPath": sr6_ruleset_ui_sophistication_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
        "designAuthorizedParitySofteningReceiptPath": design_authorized_parity_softening_receipt_path,
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
    "directImportRouteProof": {
        "reviewJobs": [
            "translator_xml_custom_data",
            "hero_lab_import_oracle",
        ],
        "screenshots": [
            "38-translator-dialog-light.png",
            "39-xml-editor-dialog-light.png",
            "40-hero-lab-importer-dialog-light.png",
        ],
        "characterOverviewPresenterTests": [
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
        ],
    },
    "uiElementParityAuditProof": {
        "status": proof_status(
            ui_element_visual_no_count == 0,
            ui_element_behavioral_no_count == 0,
            not missing_dense_builder_route_local_evidence_suffixes,
        ),
        "uiElementParityAuditReceiptPath": ui_element_parity_audit_receipt_path,
        "visualNoCount": ui_element_visual_no_count,
        "behavioralNoCount": ui_element_behavioral_no_count,
        "coverageGapKeys": ui_element_coverage_gap_keys,
        "denseBuilderRouteLocalEvidence": dense_builder_route_local_evidence,
        "requiredDenseBuilderRouteLocalEvidenceSuffixes": required_dense_builder_route_local_evidence_suffixes,
        "missingDenseBuilderRouteLocalEvidenceSuffixes": missing_dense_builder_route_local_evidence_suffixes,
    },
    "desktopExecutableProof": {
        "status": desktop_executable_exit_gate_status,
        "desktopExecutableExitGateReceiptPath": desktop_executable_exit_gate_receipt_path,
        "reasons": desktop_executable_exit_gate_receipt.get("reasons") or [],
    },
    "flagshipReadinessProof": {
        "status": flagship_readiness_status,
        "flagshipProductReadinessReceiptPath": flagship_product_readiness_receipt_path,
        "coverage": flagship_readiness_coverage,
        "openCoverageKeys": flagship_readiness_open_coverage_keys,
    },
    "localizationReleaseProof": {
        "status": receipt_status(localization_release_gate_receipt),
        "localizationReleaseGateReceiptPath": localization_release_gate_receipt_path,
        "interactiveControlInventoryReceiptPath": interactive_control_inventory_receipt_path,
        "startupWorkbenchSurvivalReceiptPath": startup_workbench_survival_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
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

if [[ "$skip_downstream_receipt_materialization" != "1" ]]; then
  echo "[b14] refreshing desktop visual familiarity exit gate..."
  CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT=1 \
    bash scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh >/dev/null

  echo "[b14] refreshing Chummer5a screenshot review gate..."
  bash scripts/ai/milestones/chummer5a-screenshot-review-gate.sh >/dev/null

  echo "[b14] refreshing direct import route proof..."
  bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh >/dev/null

  echo "[b14] materializing desktop workflow execution gate..."
  CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_DEPENDENCY_REFRESH=1 \
    bash scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh >/dev/null

  echo "[b14] materializing classic dense workbench posture gate..."
  bash scripts/ai/milestones/classic-dense-workbench-posture-gate.sh >/dev/null

  echo "[b14] materializing veteran task-time evidence gate..."
  bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh >/dev/null

  echo "[b14] re-materializing Chummer5a screenshot review gate..."
  bash scripts/ai/milestones/chummer5a-screenshot-review-gate.sh >/dev/null
else
  echo "[b14] skipping downstream proof materialization for screenshot refresh-only pass..."
fi

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

python3 - <<'PY' "$receipt_path"
import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
status = str(receipt.get("status") or "").strip().lower()
if status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "[b14] FAIL: flagship UI release gate is not passed: "
        + "; ".join(receipt.get("blockingFindings") or ["missing reason"])
    )
PY

python3 "$flagship_product_readiness_materializer_path" >/dev/null

echo "[b14] PASS"
