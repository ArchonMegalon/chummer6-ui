#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_SOURCE_STAGED_PROOF_SET_PATH",
        PUBLISHED / "BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json",
    )
)

REQUIRED_RECEIPTS = [
    {
        "id": "career_support",
        "path": PUBLISHED / "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_career_support_staged_proof",
    },
    {
        "id": "identity_license",
        "path": PUBLISHED / "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_identity_license_staged_proof",
    },
    {
        "id": "combat_support",
        "path": PUBLISHED / "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_combat_support_staged_proof",
    },
    {
        "id": "skill_maintenance",
        "path": PUBLISHED / "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_skill_maintenance_staged_proof",
    },
    {
        "id": "magic_support",
        "path": PUBLISHED / "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_magic_support_staged_proof",
    },
    {
        "id": "gear_maintenance",
        "path": PUBLISHED / "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_gear_maintenance_staged_proof",
    },
    {
        "id": "runner_intelligence",
        "path": PUBLISHED / "BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_runner_intelligence_staged_proof",
    },
    {
        "id": "source_gear_utility",
        "path": PUBLISHED / "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_source_gear_utility_staged_proof",
    },
    {
        "id": "magic_cleanup",
        "path": PUBLISHED / "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_magic_cleanup_staged_proof",
    },
    {
        "id": "browser_output_handoff",
        "path": PUBLISHED / "BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_browser_output_handoff_staged_proof",
    },
    {
        "id": "workbench_portal_handoff",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_portal_handoff_staged_proof",
    },
    {
        "id": "workbench_polish",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_polish_staged_proof",
    },
    {
        "id": "workbench_recovery",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_recovery_staged_proof",
    },
    {
        "id": "workbench_hosting_privacy",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_hosting_privacy_staged_proof",
    },
    {
        "id": "workbench_command_palette",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_command_palette_staged_proof",
    },
    {
        "id": "workbench_density",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_density_staged_proof",
    },
    {
        "id": "workbench_workflow_ledger",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_workflow_ledger_staged_proof",
    },
    {
        "id": "workbench_file_intake",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_file_intake_staged_proof",
    },
    {
        "id": "workbench_rules_data",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_rules_data_staged_proof",
    },
    {
        "id": "workbench_settings",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_settings_staged_proof",
    },
    {
        "id": "workbench_diagnostics",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_diagnostics_staged_proof",
    },
    {
        "id": "workbench_connected_runtime",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_connected_runtime_staged_proof",
    },
    {
        "id": "workbench_accessibility",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_accessibility_staged_proof",
    },
    {
        "id": "workbench_section_rail",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_section_rail_staged_proof",
    },
    {
        "id": "workbench_desktop_install",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_desktop_install_staged_proof",
    },
    {
        "id": "workbench_menu_bar",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_menu_bar_staged_proof",
    },
    {
        "id": "workbench_workspace_tabs",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_workspace_tabs_staged_proof",
    },
    {
        "id": "workbench_status_bar",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_status_bar_staged_proof",
    },
    {
        "id": "workbench_inspector_rail",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_inspector_rail_staged_proof",
    },
    {
        "id": "workbench_dialog_stack",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_dialog_stack_staged_proof",
    },
    {
        "id": "workbench_context_actions",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_context_actions_staged_proof",
    },
    {
        "id": "workbench_search_filter",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_search_filter_staged_proof",
    },
    {
        "id": "workbench_layout_presets",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_layout_presets_staged_proof",
    },
    {
        "id": "workbench_activity_feed",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_activity_feed_staged_proof",
    },
    {
        "id": "workbench_keyboard_shortcuts",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_keyboard_shortcuts_staged_proof",
    },
    {
        "id": "workbench_resource_meters",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_resource_meters_staged_proof",
    },
    {
        "id": "workbench_tree_tools",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_tree_tools_staged_proof",
    },
    {
        "id": "workbench_save_session",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_save_session_staged_proof",
    },
    {
        "id": "workbench_output_handoff",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_output_handoff_staged_proof",
    },
    {
        "id": "workbench_validation_queue",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_validation_queue_staged_proof",
    },
    {
        "id": "workbench_history_undo",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_history_undo_staged_proof",
    },
    {
        "id": "workbench_sync_presence",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_sync_presence_staged_proof",
    },
    {
        "id": "workbench_data_packs",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_data_packs_staged_proof",
    },
    {
        "id": "workbench_character_library",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_character_library_staged_proof",
    },
    {
        "id": "workbench_campaign_session",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_campaign_session_staged_proof",
    },
    {
        "id": "workbench_observability_privacy",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_observability_privacy_staged_proof",
    },
    {
        "id": "workbench_first_run",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_first_run_staged_proof",
    },
    {
        "id": "workbench_pwa_install",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_pwa_install_staged_proof",
    },
    {
        "id": "workbench_docker_operator",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_docker_operator_staged_proof",
    },
    {
        "id": "workbench_security_access",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_security_access_staged_proof",
    },
    {
        "id": "workbench_notifications_jobs",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_notifications_jobs_staged_proof",
    },
    {
        "id": "workbench_touch_mobile",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_touch_mobile_staged_proof",
    },
    {
        "id": "workbench_navigation_deeplink",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_navigation_deeplink_staged_proof",
    },
    {
        "id": "workbench_inline_editing",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_INLINE_EDITING_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_inline_editing_staged_proof",
    },
    {
        "id": "workbench_performance_virtualization",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_performance_virtualization_staged_proof",
    },
    {
        "id": "workbench_print_layout",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PRINT_LAYOUT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_print_layout_staged_proof",
    },
    {
        "id": "workbench_portrait_attachments",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_portrait_attachments_staged_proof",
    },
    {
        "id": "workbench_windowing_panes",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_windowing_panes_staged_proof",
    },
    {
        "id": "workbench_calculation_provenance",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_CALCULATION_PROVENANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_calculation_provenance_staged_proof",
    },
    {
        "id": "workbench_lifecycle_calendar",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_LIFECYCLE_CALENDAR_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_lifecycle_calendar_staged_proof",
    },
    {
        "id": "workbench_progression_ledger",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_progression_ledger_staged_proof",
    },
    {
        "id": "workbench_import_reconcile",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_IMPORT_RECONCILE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_import_reconcile_staged_proof",
    },
    {
        "id": "workbench_compare_merge",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_COMPARE_MERGE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_compare_merge_staged_proof",
    },
    {
        "id": "workbench_restore_checkpoint",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_restore_checkpoint_staged_proof",
    },
    {
        "id": "workbench_offline_cache",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_OFFLINE_CACHE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_offline_cache_staged_proof",
    },
    {
        "id": "workbench_session_locking",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SESSION_LOCKING_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_session_locking_staged_proof",
    },
    {
        "id": "workbench_share_export_privacy",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_SHARE_EXPORT_PRIVACY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_share_export_privacy_staged_proof",
    },
    {
        "id": "workbench_table_handoff",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_table_handoff_staged_proof",
    },
    {
        "id": "workbench_rules_citation",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RULES_CITATION_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_rules_citation_staged_proof",
    },
    {
        "id": "workbench_localization_terminology",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_LOCALIZATION_TERMINOLOGY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_localization_terminology_staged_proof",
    },
    {
        "id": "workbench_help_recovery_guidance",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_help_recovery_guidance_staged_proof",
    },
    {
        "id": "workbench_gm_screen_export",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_gm_screen_export_staged_proof",
    },
    {
        "id": "workbench_roster_hierarchy",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_roster_hierarchy_staged_proof",
    },
    {
        "id": "legacy_control_coverage",
        "path": PUBLISHED / "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_legacy_control_coverage_staged_proof",
    },
]

SOURCE_CALCULATION_RECEIPTS = [
    {
        "id": "runner_intelligence_calculation",
        "path": PUBLISHED / "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_runner_intelligence_calculation_proof",
        "proof_tier": "source_calculation_no_browser_execution",
    },
]

SOURCE_CONTRACT_CHECKS = [
    {
        "id": "runbook_route_roles",
        "path": REPO_ROOT / "docs" / "BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md",
        "tokens": [
            "/app remains the clean public browser client path.",
            "/blazor/app remains the hosted app path.",
            "/blazor/workbench remains the proof-compatible route.",
            "/blazor/preview remains the preview tools/result-state route.",
            "source_contract_check_count",
            "non-receipt source contract checks",
            "source_staged_proof_set_route_lane",
            "source_staged_proof_set_source_contract_checks",
            "aggregate_source_alignment_only_not_browser_execution_route_role_source_contracts",
            "source_contract_checks.docs_index_route_roles",
            "chummer_app_proof_compatible_workbench_preview_tools",
            "native installer amber/slate/mint progress chrome and high-contrast fallback source alignment",
            "compatibility-route `/blazor/workbench` task-dock and slate/amber/mint/blue Chummer Online theme-layer contract",
        ],
    },
    {
        "id": "docs_index_route_roles",
        "path": REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "route roles for `/app`, `/blazor/app`, `/blazor/workbench`, and `/blazor/preview`",
            "chummer_app_proof_compatible_workbench_preview_tools",
            "source_contract_check_count",
            "non-receipt source contract checks",
            "source_staged_proof_set_route_lane",
            "source_staged_proof_set_source_contract_checks",
            "source_contract_checks.runbook_route_roles",
            "source_contract_checks.docs_index_route_roles",
        ],
    },
]


def load_json(path: Path) -> dict:
    if not path.is_file():
        return {}
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {"_invalid_json": True}
    return loaded if isinstance(loaded, dict) else {"_invalid_json": True}


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig")
    except FileNotFoundError:
        return ""


def main() -> int:
    rows = []
    calculation_rows = []
    failures = []
    passed_count = 0
    calculation_passed_count = 0
    expected_route_count = 0
    source_check_count = 0
    source_calculation_check_count = 0
    source_contract_rows = []

    for check in SOURCE_CONTRACT_CHECKS:
        text = read_text(check["path"])
        missing_tokens = [token for token in check["tokens"] if token not in text]
        if not text:
            failures.append(f"{check['id']}: missing {check['path']}")
        elif missing_tokens:
            failures.append(f"{check['id']}: missing {', '.join(missing_tokens)}")
        source_contract_rows.append(
            {
                "id": check["id"],
                "path": str(check["path"]),
                "status": "failed" if not text or missing_tokens else "passed",
                "required_token_count": len(check["tokens"]),
                "missing_tokens": missing_tokens,
            }
        )

    for receipt in REQUIRED_RECEIPTS:
        path = receipt["path"]
        payload = load_json(path)
        status = str(payload.get("status") or "missing").strip().lower() if payload else "missing"
        contract_name = str(payload.get("contract_name") or "").strip()
        proof_tier = str(payload.get("proof_tier") or "").strip()
        expected_contract = receipt["contract_name"]
        route_count = len(payload.get("expected_routes") or []) if isinstance(payload.get("expected_routes"), list) else 0
        checks = payload.get("checks") or []
        checks_count = len(checks) if isinstance(checks, list) else 0
        control_count = payload.get("control_count") if receipt["id"] == "legacy_control_coverage" else None
        covered_control_count = payload.get("covered_control_count") if receipt["id"] == "legacy_control_coverage" else None

        if not payload:
            failures.append(f"{receipt['id']}: missing {path}")
        elif payload.get("_invalid_json"):
            failures.append(f"{receipt['id']}: invalid JSON at {path}")
        elif contract_name != expected_contract:
            failures.append(f"{receipt['id']}: contract mismatch {contract_name or 'missing'}")
        elif status != "passed":
            failures.append(f"{receipt['id']}: status {status or 'missing'}")
        elif proof_tier != "source_staged_no_browser_execution":
            failures.append(f"{receipt['id']}: proof_tier {proof_tier or 'missing'}")
        else:
            passed_count += 1

        expected_route_count += route_count
        source_check_count += checks_count
        rows.append(
            {
                "id": receipt["id"],
                "path": str(path),
                "expected_contract_name": expected_contract,
                "contract_name": contract_name or "missing",
                "status": status or "missing",
                "proof_tier": proof_tier or "missing",
                "expected_route_count": route_count,
                "source_check_count": checks_count,
                "control_count": control_count,
                "covered_control_count": covered_control_count,
            }
        )

    for receipt in SOURCE_CALCULATION_RECEIPTS:
        path = receipt["path"]
        payload = load_json(path)
        status = str(payload.get("status") or "missing").strip().lower() if payload else "missing"
        contract_name = str(payload.get("contract_name") or "").strip()
        proof_tier = str(payload.get("proof_tier") or "").strip()
        expected_contract = receipt["contract_name"]
        expected_tier = receipt["proof_tier"]
        checks = payload.get("checks") or []
        checks_count = len(checks) if isinstance(checks, list) else 0

        if not payload:
            failures.append(f"{receipt['id']}: missing {path}")
        elif payload.get("_invalid_json"):
            failures.append(f"{receipt['id']}: invalid JSON at {path}")
        elif contract_name != expected_contract:
            failures.append(f"{receipt['id']}: contract mismatch {contract_name or 'missing'}")
        elif status != "passed":
            failures.append(f"{receipt['id']}: status {status or 'missing'}")
        elif proof_tier != expected_tier:
            failures.append(f"{receipt['id']}: proof_tier {proof_tier or 'missing'}")
        else:
            calculation_passed_count += 1

        source_calculation_check_count += checks_count
        calculation_rows.append(
            {
                "id": receipt["id"],
                "path": str(path),
                "expected_contract_name": expected_contract,
                "contract_name": contract_name or "missing",
                "status": status or "missing",
                "proof_tier": proof_tier or "missing",
                "source_calculation_check_count": checks_count,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_source_staged_proof_set",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "chummer_app_proof_compatible_workbench_preview_tools",
        "required_receipt_count": len(REQUIRED_RECEIPTS),
        "passed_receipt_count": passed_count,
        "source_calculation_receipt_count": len(SOURCE_CALCULATION_RECEIPTS),
        "source_calculation_passed_count": calculation_passed_count,
        "expected_route_count": expected_route_count,
        "source_check_count": source_check_count,
        "source_calculation_check_count": source_calculation_check_count,
        "source_contract_check_count": len(source_contract_rows),
        "source_contract_checks": source_contract_rows,
        "required_receipts": rows,
        "source_calculation_receipts": calculation_rows,
        "failures": failures,
        "notes": [
            "This aggregate summarizes source-staged receipts plus explicitly separated source-calculation receipts.",
            "/app remains the clean public browser client path.",
            "/blazor/app remains the hosted app path.",
            "/blazor/workbench remains the proof-compatible route.",
            "/blazor/preview remains the preview tools and result-state route.",
            "source_contract_check_count summarizes non-receipt source contract checks such as route-role documentation.",
            "Source-calculation receipts are not authoritative SR rules-engine validation and are not browser execution evidence.",
            "It is not a hosted Playwright execution receipt and is not Docker self-host browser execution evidence.",
            "Keep this aggregate separate from BLAZOR_BROWSER_LANE_PROOF_SET.generated.json release readiness.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_source_staged_proof_set:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
