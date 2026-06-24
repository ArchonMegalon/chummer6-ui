#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"

ROUTE_PROOF = PUBLISHED / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
EXECUTION_PROOF = PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json"
SELF_HOST_PROOF = PUBLISHED / "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"
ANALYTICS_PROOF = PUBLISHED / "BLAZOR_ANALYTICS_POSTURE.generated.json"
CONNECTED_RUNTIME_PROOF = PUBLISHED / "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json"
AGGREGATE_PROOF_SET = PUBLISHED / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
CAREER_SUPPORT_STAGED_PROOF = PUBLISHED / "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json"
IDENTITY_LICENSE_STAGED_PROOF = PUBLISHED / "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json"
COMBAT_SUPPORT_STAGED_PROOF = PUBLISHED / "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json"
SKILL_MAINTENANCE_STAGED_PROOF = PUBLISHED / "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json"
MAGIC_SUPPORT_STAGED_PROOF = PUBLISHED / "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json"
GEAR_MAINTENANCE_STAGED_PROOF = PUBLISHED / "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json"
SOURCE_GEAR_UTILITY_STAGED_PROOF = PUBLISHED / "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json"
MAGIC_CLEANUP_STAGED_PROOF = PUBLISHED / "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json"
BROWSER_OUTPUT_HANDOFF_STAGED_PROOF = PUBLISHED / "BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json"
WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json"
WORKBENCH_POLISH_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json"
WORKBENCH_RECOVERY_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json"
WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json"
WORKBENCH_COMMAND_PALETTE_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json"
WORKBENCH_DENSITY_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json"
WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json"
WORKBENCH_FILE_INTAKE_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json"
WORKBENCH_RULES_DATA_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.generated.json"
WORKBENCH_SETTINGS_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json"
WORKBENCH_DIAGNOSTICS_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json"
WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json"
WORKBENCH_ACCESSIBILITY_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json"
WORKBENCH_SECTION_RAIL_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json"
WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json"
WORKBENCH_MENU_BAR_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json"
WORKBENCH_WORKSPACE_TABS_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json"
WORKBENCH_STATUS_BAR_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.generated.json"
WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json"
WORKBENCH_DIALOG_STACK_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json"
WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json"
WORKBENCH_SEARCH_FILTER_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json"
WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json"
WORKBENCH_ACTIVITY_FEED_STAGED_PROOF = PUBLISHED / "BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.generated.json"
LEGACY_CONTROL_COVERAGE_STAGED_PROOF = PUBLISHED / "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json"
SOURCE_STAGED_PROOF_SET = PUBLISHED / "BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json"
PORTAL_INSTALLER_HANDOFF_STAGED_PROOF = PUBLISHED / "BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json"
DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF = PUBLISHED / "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json"
ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF = PUBLISHED / "BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json"
RUNTIME_PROOF_REFRESH_PLAN = PUBLISHED / "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json"
STAGED_TO_RUNTIME_PROMOTION_MATRIX = PUBLISHED / "BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.generated.json"
BLOCKERS = PUBLISHED / "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json"
EXPANDED_ROUTE_PROOF_MARKERS = {
    "public_startup_workbench_command_routes",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
}


def classify_route_shape(route: dict) -> str:
    explicit_shape = str(route.get("proof_shape") or "").strip()
    if explicit_shape:
        return explicit_shape
    marker_ids = {
        str(item).strip()
        for item in (route.get("route_proof_markers") or [])
        if str(item).strip()
    }
    if EXPANDED_ROUTE_PROOF_MARKERS.issubset(marker_ids):
        return "expanded"
    if EXPANDED_ROUTE_PROOF_MARKERS & marker_ids:
        return "partial-expanded"
    if marker_ids:
        return "core"
    return "missing"


def load_json(path: Path) -> dict:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def count_execution_checks(execution: dict) -> int:
    workflow_families = execution.get("workflow_families") or []
    total = 0
    for family in workflow_families:
        if isinstance(family, dict):
            checks = family.get("checks") or []
            if isinstance(checks, list):
                total += len(checks)
    return total


def count_staged_source_checks(staged: dict) -> int:
    checks = staged.get("checks") or []
    return len(checks) if isinstance(checks, list) else 0


def main() -> int:
    route = load_json(ROUTE_PROOF)
    execution = load_json(EXECUTION_PROOF)
    self_host = load_json(SELF_HOST_PROOF)
    analytics = load_json(ANALYTICS_PROOF)
    connected_runtime = load_json(CONNECTED_RUNTIME_PROOF)
    aggregate = load_json(AGGREGATE_PROOF_SET)
    career_support_staged = load_json(CAREER_SUPPORT_STAGED_PROOF)
    identity_license_staged = load_json(IDENTITY_LICENSE_STAGED_PROOF)
    combat_support_staged = load_json(COMBAT_SUPPORT_STAGED_PROOF)
    skill_maintenance_staged = load_json(SKILL_MAINTENANCE_STAGED_PROOF)
    magic_support_staged = load_json(MAGIC_SUPPORT_STAGED_PROOF)
    gear_maintenance_staged = load_json(GEAR_MAINTENANCE_STAGED_PROOF)
    source_gear_utility_staged = load_json(SOURCE_GEAR_UTILITY_STAGED_PROOF)
    magic_cleanup_staged = load_json(MAGIC_CLEANUP_STAGED_PROOF)
    browser_output_handoff_staged = load_json(BROWSER_OUTPUT_HANDOFF_STAGED_PROOF)
    workbench_portal_handoff_staged = load_json(WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF)
    workbench_polish_staged = load_json(WORKBENCH_POLISH_STAGED_PROOF)
    workbench_recovery_staged = load_json(WORKBENCH_RECOVERY_STAGED_PROOF)
    workbench_hosting_privacy_staged = load_json(WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF)
    workbench_command_palette_staged = load_json(WORKBENCH_COMMAND_PALETTE_STAGED_PROOF)
    workbench_density_staged = load_json(WORKBENCH_DENSITY_STAGED_PROOF)
    workbench_workflow_ledger_staged = load_json(WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF)
    workbench_file_intake_staged = load_json(WORKBENCH_FILE_INTAKE_STAGED_PROOF)
    workbench_rules_data_staged = load_json(WORKBENCH_RULES_DATA_STAGED_PROOF)
    workbench_settings_staged = load_json(WORKBENCH_SETTINGS_STAGED_PROOF)
    workbench_diagnostics_staged = load_json(WORKBENCH_DIAGNOSTICS_STAGED_PROOF)
    workbench_connected_runtime_staged = load_json(WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF)
    workbench_accessibility_staged = load_json(WORKBENCH_ACCESSIBILITY_STAGED_PROOF)
    workbench_section_rail_staged = load_json(WORKBENCH_SECTION_RAIL_STAGED_PROOF)
    workbench_desktop_install_staged = load_json(WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF)
    workbench_menu_bar_staged = load_json(WORKBENCH_MENU_BAR_STAGED_PROOF)
    workbench_workspace_tabs_staged = load_json(WORKBENCH_WORKSPACE_TABS_STAGED_PROOF)
    workbench_status_bar_staged = load_json(WORKBENCH_STATUS_BAR_STAGED_PROOF)
    workbench_inspector_rail_staged = load_json(WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF)
    workbench_dialog_stack_staged = load_json(WORKBENCH_DIALOG_STACK_STAGED_PROOF)
    workbench_context_actions_staged = load_json(WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF)
    workbench_search_filter_staged = load_json(WORKBENCH_SEARCH_FILTER_STAGED_PROOF)
    workbench_layout_presets_staged = load_json(WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF)
    workbench_activity_feed_staged = load_json(WORKBENCH_ACTIVITY_FEED_STAGED_PROOF)
    legacy_control_coverage_staged = load_json(LEGACY_CONTROL_COVERAGE_STAGED_PROOF)
    source_staged_proof_set = load_json(SOURCE_STAGED_PROOF_SET)
    portal_installer_handoff_staged = load_json(PORTAL_INSTALLER_HANDOFF_STAGED_PROOF)
    docker_self_host_operator_staged = load_json(DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF)
    account_support_handoff_staged = load_json(ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF)
    runtime_proof_refresh_plan = load_json(RUNTIME_PROOF_REFRESH_PLAN)
    staged_to_runtime_promotion_matrix = load_json(STAGED_TO_RUNTIME_PROMOTION_MATRIX)
    blockers = load_json(BLOCKERS)

    print("Blazor public-edge proof status")
    print(f"route_proof_receipt={ROUTE_PROOF}")
    print(f"route_proof_status={str(route.get('status') or '').strip() or 'missing'}")
    print(f"route_proof_contract={str(route.get('contract_name') or '').strip() or 'missing'}")
    print(f"route_runtime_required={route.get('runtime_required', 'unknown')}")
    print(f"route_probe_executed={route.get('route_probe_executed', 'unknown')}")
    print(f"route_proof_shape={classify_route_shape(route)}")
    print(f"route_probe_count={route.get('route_probe_count', 'unknown')}")
    print(f"route_proof_markers={len(route.get('route_proof_markers') or [])}")
    print(
        "route_proof_marker_ids="
        + ",".join(str(item).strip() for item in (route.get("route_proof_markers") or []) if str(item).strip())
    )
    print(f"workflow_shape_markers={len(route.get('workflow_proofs') or [])}")
    print(
        "route_workflow_shape_ids="
        + ",".join(str(item).strip() for item in (route.get("workflow_proofs") or []) if str(item).strip())
    )
    print(f"execution_proof_receipt={EXECUTION_PROOF}")
    print(f"execution_proof_status={str(execution.get('status') or '').strip() or 'missing'}")
    print(f"execution_proof_tier={str(execution.get('proof_tier') or '').strip() or 'missing'}")
    print(f"execution_route_lane={str(execution.get('route_lane') or '').strip() or 'missing'}")
    print(f"execution_promoted_route_base={str(execution.get('promoted_route_base') or '').strip() or 'missing'}")
    print(f"execution_workflow_families={len(execution.get('workflow_families') or [])}")
    print(f"execution_browser_checks={count_execution_checks(execution)}")
    print(f"execution_error={str(execution.get('error') or '').strip() or 'missing'}")
    print(
        "execution_workflow_family_ids="
        + ",".join(
            str(item.get("id") or "").strip()
            for item in (execution.get("workflow_families") or [])
            if isinstance(item, dict) and str(item.get("id") or "").strip()
        )
    )
    print(
        "execution_workflow_check_counts="
        + ",".join(
            f"{str(item.get('id') or '').strip()}:{len(item.get('checks') or [])}"
            for item in (execution.get("workflow_families") or [])
            if isinstance(item, dict) and str(item.get("id") or "").strip()
        )
    )
    print(f"self_host_proof_receipt={SELF_HOST_PROOF}")
    print(f"self_host_proof_status={str(self_host.get('status') or '').strip() or 'missing'}")
    print(f"self_host_proof_contract={str(self_host.get('contract_name') or '').strip() or 'missing'}")
    print(f"self_host_base_url={str(self_host.get('base_url') or '').strip() or 'missing'}")
    print(f"self_host_runtime_required={self_host.get('runtime_required', 'unknown')}")
    print(f"self_host_route_probe_executed={self_host.get('route_probe_executed', 'unknown')}")
    print(f"self_host_route_count={len(self_host.get('proof_routes') or [])}")
    print(f"self_host_workflow_proofs={len(self_host.get('workflow_proofs') or [])}")
    print(f"self_host_compose_file={str(self_host.get('compose_file') or '').strip() or 'missing'}")
    print(f"analytics_proof_receipt={ANALYTICS_PROOF}")
    print(f"analytics_proof_status={str(analytics.get('status') or '').strip() or 'missing'}")
    print(f"analytics_proof_contract={str(analytics.get('contract_name') or '').strip() or 'missing'}")
    print(f"analytics_live_url={str(analytics.get('live_url') or '').strip() or 'missing'}")
    print(f"analytics_health_url={str(analytics.get('health_url') or '').strip() or 'missing'}")
    print(f"analytics_self_host_default={str(analytics.get('self_host_default') or '').strip() or 'missing'}")
    print(f"analytics_hosted_public_edge={str(analytics.get('hosted_public_edge') or '').strip() or 'missing'}")
    print(f"analytics_sensitive_data_policy={str(analytics.get('sensitive_data_policy') or '').strip() or 'missing'}")
    print(f"connected_runtime_proof_receipt={CONNECTED_RUNTIME_PROOF}")
    print(f"connected_runtime_proof_status={str(connected_runtime.get('status') or '').strip() or 'missing'}")
    print(f"connected_runtime_proof_contract={str(connected_runtime.get('contract_name') or '').strip() or 'missing'}")
    print(f"connected_runtime_live_url={str(connected_runtime.get('live_url') or '').strip() or 'missing'}")
    print(f"connected_runtime_owner_context_boundary={str(connected_runtime.get('owner_context_boundary') or '').strip() or 'missing'}")
    print(f"connected_runtime_scope={str(connected_runtime.get('scope') or '').strip() or 'missing'}")
    print(f"aggregate_proof_set_receipt={AGGREGATE_PROOF_SET}")
    print(f"aggregate_proof_set_status={str(aggregate.get('status') or '').strip() or 'missing'}")
    print(f"aggregate_proof_set_contract={str(aggregate.get('contract_name') or '').strip() or 'missing'}")
    print(f"aggregate_required_receipts={aggregate.get('required_receipt_count', 'unknown')}")
    print(f"aggregate_passed_receipts={aggregate.get('passed_receipt_count', 'unknown')}")
    print(f"aggregate_scope={str(aggregate.get('scope') or '').strip() or 'missing'}")
    print(f"career_support_staged_receipt={CAREER_SUPPORT_STAGED_PROOF}")
    print(f"career_support_staged_status={str(career_support_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"career_support_staged_contract={str(career_support_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"career_support_staged_tier={str(career_support_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"career_support_staged_route_count={len(career_support_staged.get('expected_routes') or [])}")
    print(f"career_support_staged_source_checks={count_staged_source_checks(career_support_staged)}")
    print(
        "career_support_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"identity_license_staged_receipt={IDENTITY_LICENSE_STAGED_PROOF}")
    print(f"identity_license_staged_status={str(identity_license_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"identity_license_staged_contract={str(identity_license_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"identity_license_staged_tier={str(identity_license_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"identity_license_staged_route_count={len(identity_license_staged.get('expected_routes') or [])}")
    print(f"identity_license_staged_source_checks={count_staged_source_checks(identity_license_staged)}")
    print(
        "identity_license_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"combat_support_staged_receipt={COMBAT_SUPPORT_STAGED_PROOF}")
    print(f"combat_support_staged_status={str(combat_support_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"combat_support_staged_contract={str(combat_support_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"combat_support_staged_tier={str(combat_support_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"combat_support_staged_route_count={len(combat_support_staged.get('expected_routes') or [])}")
    print(f"combat_support_staged_source_checks={count_staged_source_checks(combat_support_staged)}")
    print(
        "combat_support_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"skill_maintenance_staged_receipt={SKILL_MAINTENANCE_STAGED_PROOF}")
    print(f"skill_maintenance_staged_status={str(skill_maintenance_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"skill_maintenance_staged_contract={str(skill_maintenance_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"skill_maintenance_staged_tier={str(skill_maintenance_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"skill_maintenance_staged_route_count={len(skill_maintenance_staged.get('expected_routes') or [])}")
    print(f"skill_maintenance_staged_source_checks={count_staged_source_checks(skill_maintenance_staged)}")
    print(
        "skill_maintenance_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"magic_support_staged_receipt={MAGIC_SUPPORT_STAGED_PROOF}")
    print(f"magic_support_staged_status={str(magic_support_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"magic_support_staged_contract={str(magic_support_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"magic_support_staged_tier={str(magic_support_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"magic_support_staged_route_count={len(magic_support_staged.get('expected_routes') or [])}")
    print(f"magic_support_staged_source_checks={count_staged_source_checks(magic_support_staged)}")
    print(
        "magic_support_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"gear_maintenance_staged_receipt={GEAR_MAINTENANCE_STAGED_PROOF}")
    print(f"gear_maintenance_staged_status={str(gear_maintenance_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"gear_maintenance_staged_contract={str(gear_maintenance_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"gear_maintenance_staged_tier={str(gear_maintenance_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"gear_maintenance_staged_route_count={len(gear_maintenance_staged.get('expected_routes') or [])}")
    print(f"gear_maintenance_staged_source_checks={count_staged_source_checks(gear_maintenance_staged)}")
    print(
        "gear_maintenance_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"source_gear_utility_staged_receipt={SOURCE_GEAR_UTILITY_STAGED_PROOF}")
    print(f"source_gear_utility_staged_status={str(source_gear_utility_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"source_gear_utility_staged_contract={str(source_gear_utility_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"source_gear_utility_staged_tier={str(source_gear_utility_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"source_gear_utility_staged_route_count={len(source_gear_utility_staged.get('expected_routes') or [])}")
    print(f"source_gear_utility_staged_source_checks={count_staged_source_checks(source_gear_utility_staged)}")
    print(
        "source_gear_utility_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"magic_cleanup_staged_receipt={MAGIC_CLEANUP_STAGED_PROOF}")
    print(f"magic_cleanup_staged_status={str(magic_cleanup_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"magic_cleanup_staged_contract={str(magic_cleanup_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"magic_cleanup_staged_tier={str(magic_cleanup_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"magic_cleanup_staged_route_count={len(magic_cleanup_staged.get('expected_routes') or [])}")
    print(f"magic_cleanup_staged_source_checks={count_staged_source_checks(magic_cleanup_staged)}")
    print(
        "magic_cleanup_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"browser_output_handoff_staged_receipt={BROWSER_OUTPUT_HANDOFF_STAGED_PROOF}")
    print(f"browser_output_handoff_staged_status={str(browser_output_handoff_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"browser_output_handoff_staged_contract={str(browser_output_handoff_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"browser_output_handoff_staged_tier={str(browser_output_handoff_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"browser_output_handoff_staged_route_count={len(browser_output_handoff_staged.get('expected_routes') or [])}")
    print(f"browser_output_handoff_staged_source_checks={count_staged_source_checks(browser_output_handoff_staged)}")
    print(
        "browser_output_handoff_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_portal_handoff_staged_receipt={WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF}")
    print(f"workbench_portal_handoff_staged_status={str(workbench_portal_handoff_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_portal_handoff_staged_contract={str(workbench_portal_handoff_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_portal_handoff_staged_tier={str(workbench_portal_handoff_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_portal_handoff_staged_route_count={len(workbench_portal_handoff_staged.get('expected_routes') or [])}")
    print(f"workbench_portal_handoff_staged_source_checks={count_staged_source_checks(workbench_portal_handoff_staged)}")
    print(
        "workbench_portal_handoff_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_polish_staged_receipt={WORKBENCH_POLISH_STAGED_PROOF}")
    print(f"workbench_polish_staged_status={str(workbench_polish_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_polish_staged_contract={str(workbench_polish_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_polish_staged_tier={str(workbench_polish_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_polish_staged_route_count={len(workbench_polish_staged.get('expected_routes') or [])}")
    print(f"workbench_polish_staged_source_checks={count_staged_source_checks(workbench_polish_staged)}")
    print(
        "workbench_polish_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_recovery_staged_receipt={WORKBENCH_RECOVERY_STAGED_PROOF}")
    print(f"workbench_recovery_staged_status={str(workbench_recovery_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_recovery_staged_contract={str(workbench_recovery_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_recovery_staged_tier={str(workbench_recovery_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_recovery_staged_route_count={len(workbench_recovery_staged.get('expected_routes') or [])}")
    print(f"workbench_recovery_staged_source_checks={count_staged_source_checks(workbench_recovery_staged)}")
    print(
        "workbench_recovery_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_hosting_privacy_staged_receipt={WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF}")
    print(f"workbench_hosting_privacy_staged_status={str(workbench_hosting_privacy_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_hosting_privacy_staged_contract={str(workbench_hosting_privacy_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_hosting_privacy_staged_tier={str(workbench_hosting_privacy_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_hosting_privacy_staged_route_count={len(workbench_hosting_privacy_staged.get('expected_routes') or [])}")
    print(f"workbench_hosting_privacy_staged_source_checks={count_staged_source_checks(workbench_hosting_privacy_staged)}")
    print(
        "workbench_hosting_privacy_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_command_palette_staged_receipt={WORKBENCH_COMMAND_PALETTE_STAGED_PROOF}")
    print(f"workbench_command_palette_staged_status={str(workbench_command_palette_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_command_palette_staged_contract={str(workbench_command_palette_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_command_palette_staged_tier={str(workbench_command_palette_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_command_palette_staged_route_count={len(workbench_command_palette_staged.get('expected_routes') or [])}")
    print(f"workbench_command_palette_staged_source_checks={count_staged_source_checks(workbench_command_palette_staged)}")
    print(
        "workbench_command_palette_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_density_staged_receipt={WORKBENCH_DENSITY_STAGED_PROOF}")
    print(f"workbench_density_staged_status={str(workbench_density_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_density_staged_contract={str(workbench_density_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_density_staged_tier={str(workbench_density_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_density_staged_route_count={len(workbench_density_staged.get('expected_routes') or [])}")
    print(f"workbench_density_staged_source_checks={count_staged_source_checks(workbench_density_staged)}")
    print(
        "workbench_density_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_workflow_ledger_staged_receipt={WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF}")
    print(f"workbench_workflow_ledger_staged_status={str(workbench_workflow_ledger_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_workflow_ledger_staged_contract={str(workbench_workflow_ledger_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_workflow_ledger_staged_tier={str(workbench_workflow_ledger_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_workflow_ledger_staged_route_count={len(workbench_workflow_ledger_staged.get('expected_routes') or [])}")
    print(f"workbench_workflow_ledger_staged_source_checks={count_staged_source_checks(workbench_workflow_ledger_staged)}")
    print(
        "workbench_workflow_ledger_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_file_intake_staged_receipt={WORKBENCH_FILE_INTAKE_STAGED_PROOF}")
    print(f"workbench_file_intake_staged_status={str(workbench_file_intake_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_file_intake_staged_contract={str(workbench_file_intake_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_file_intake_staged_tier={str(workbench_file_intake_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_file_intake_staged_route_count={len(workbench_file_intake_staged.get('expected_routes') or [])}")
    print(f"workbench_file_intake_staged_source_checks={count_staged_source_checks(workbench_file_intake_staged)}")
    print(
        "workbench_file_intake_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_rules_data_staged_receipt={WORKBENCH_RULES_DATA_STAGED_PROOF}")
    print(f"workbench_rules_data_staged_status={str(workbench_rules_data_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_rules_data_staged_contract={str(workbench_rules_data_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_rules_data_staged_tier={str(workbench_rules_data_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_rules_data_staged_route_count={len(workbench_rules_data_staged.get('expected_routes') or [])}")
    print(f"workbench_rules_data_staged_source_checks={count_staged_source_checks(workbench_rules_data_staged)}")
    print(
        "workbench_rules_data_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_settings_staged_receipt={WORKBENCH_SETTINGS_STAGED_PROOF}")
    print(f"workbench_settings_staged_status={str(workbench_settings_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_settings_staged_contract={str(workbench_settings_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_settings_staged_tier={str(workbench_settings_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_settings_staged_route_count={len(workbench_settings_staged.get('expected_routes') or [])}")
    print(f"workbench_settings_staged_source_checks={count_staged_source_checks(workbench_settings_staged)}")
    print(
        "workbench_settings_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_diagnostics_staged_receipt={WORKBENCH_DIAGNOSTICS_STAGED_PROOF}")
    print(f"workbench_diagnostics_staged_status={str(workbench_diagnostics_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_diagnostics_staged_contract={str(workbench_diagnostics_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_diagnostics_staged_tier={str(workbench_diagnostics_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_diagnostics_staged_route_count={len(workbench_diagnostics_staged.get('expected_routes') or [])}")
    print(f"workbench_diagnostics_staged_source_checks={count_staged_source_checks(workbench_diagnostics_staged)}")
    print(
        "workbench_diagnostics_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_connected_runtime_staged_receipt={WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF}")
    print(f"workbench_connected_runtime_staged_status={str(workbench_connected_runtime_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_connected_runtime_staged_contract={str(workbench_connected_runtime_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_connected_runtime_staged_tier={str(workbench_connected_runtime_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_connected_runtime_staged_route_count={len(workbench_connected_runtime_staged.get('expected_routes') or [])}")
    print(f"workbench_connected_runtime_staged_source_checks={count_staged_source_checks(workbench_connected_runtime_staged)}")
    print(
        "workbench_connected_runtime_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_accessibility_staged_receipt={WORKBENCH_ACCESSIBILITY_STAGED_PROOF}")
    print(f"workbench_accessibility_staged_status={str(workbench_accessibility_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_accessibility_staged_contract={str(workbench_accessibility_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_accessibility_staged_tier={str(workbench_accessibility_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_accessibility_staged_route_count={len(workbench_accessibility_staged.get('expected_routes') or [])}")
    print(f"workbench_accessibility_staged_source_checks={count_staged_source_checks(workbench_accessibility_staged)}")
    print(
        "workbench_accessibility_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_section_rail_staged_receipt={WORKBENCH_SECTION_RAIL_STAGED_PROOF}")
    print(f"workbench_section_rail_staged_status={str(workbench_section_rail_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_section_rail_staged_contract={str(workbench_section_rail_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_section_rail_staged_tier={str(workbench_section_rail_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_section_rail_staged_route_count={len(workbench_section_rail_staged.get('expected_routes') or [])}")
    print(f"workbench_section_rail_staged_source_checks={count_staged_source_checks(workbench_section_rail_staged)}")
    print(
        "workbench_section_rail_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_desktop_install_staged_receipt={WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF}")
    print(f"workbench_desktop_install_staged_status={str(workbench_desktop_install_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_desktop_install_staged_contract={str(workbench_desktop_install_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_desktop_install_staged_tier={str(workbench_desktop_install_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_desktop_install_staged_route_count={len(workbench_desktop_install_staged.get('expected_routes') or [])}")
    print(f"workbench_desktop_install_staged_source_checks={count_staged_source_checks(workbench_desktop_install_staged)}")
    print(
        "workbench_desktop_install_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_menu_bar_staged_receipt={WORKBENCH_MENU_BAR_STAGED_PROOF}")
    print(f"workbench_menu_bar_staged_status={str(workbench_menu_bar_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_menu_bar_staged_contract={str(workbench_menu_bar_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_menu_bar_staged_tier={str(workbench_menu_bar_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_menu_bar_staged_route_count={len(workbench_menu_bar_staged.get('expected_routes') or [])}")
    print(f"workbench_menu_bar_staged_source_checks={count_staged_source_checks(workbench_menu_bar_staged)}")
    print(
        "workbench_menu_bar_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_workspace_tabs_staged_receipt={WORKBENCH_WORKSPACE_TABS_STAGED_PROOF}")
    print(f"workbench_workspace_tabs_staged_status={str(workbench_workspace_tabs_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_workspace_tabs_staged_contract={str(workbench_workspace_tabs_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_workspace_tabs_staged_tier={str(workbench_workspace_tabs_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_workspace_tabs_staged_route_count={len(workbench_workspace_tabs_staged.get('expected_routes') or [])}")
    print(f"workbench_workspace_tabs_staged_source_checks={count_staged_source_checks(workbench_workspace_tabs_staged)}")
    print(
        "workbench_workspace_tabs_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_status_bar_staged_receipt={WORKBENCH_STATUS_BAR_STAGED_PROOF}")
    print(f"workbench_status_bar_staged_status={str(workbench_status_bar_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_status_bar_staged_contract={str(workbench_status_bar_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_status_bar_staged_tier={str(workbench_status_bar_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_status_bar_staged_route_count={len(workbench_status_bar_staged.get('expected_routes') or [])}")
    print(f"workbench_status_bar_staged_source_checks={count_staged_source_checks(workbench_status_bar_staged)}")
    print(
        "workbench_status_bar_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_inspector_rail_staged_receipt={WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF}")
    print(f"workbench_inspector_rail_staged_status={str(workbench_inspector_rail_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_inspector_rail_staged_contract={str(workbench_inspector_rail_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_inspector_rail_staged_tier={str(workbench_inspector_rail_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_inspector_rail_staged_route_count={len(workbench_inspector_rail_staged.get('expected_routes') or [])}")
    print(f"workbench_inspector_rail_staged_source_checks={count_staged_source_checks(workbench_inspector_rail_staged)}")
    print(
        "workbench_inspector_rail_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_dialog_stack_staged_receipt={WORKBENCH_DIALOG_STACK_STAGED_PROOF}")
    print(f"workbench_dialog_stack_staged_status={str(workbench_dialog_stack_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_dialog_stack_staged_contract={str(workbench_dialog_stack_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_dialog_stack_staged_tier={str(workbench_dialog_stack_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_dialog_stack_staged_route_count={len(workbench_dialog_stack_staged.get('expected_routes') or [])}")
    print(f"workbench_dialog_stack_staged_source_checks={count_staged_source_checks(workbench_dialog_stack_staged)}")
    print(
        "workbench_dialog_stack_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_context_actions_staged_receipt={WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF}")
    print(f"workbench_context_actions_staged_status={str(workbench_context_actions_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_context_actions_staged_contract={str(workbench_context_actions_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_context_actions_staged_tier={str(workbench_context_actions_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_context_actions_staged_route_count={len(workbench_context_actions_staged.get('expected_routes') or [])}")
    print(f"workbench_context_actions_staged_source_checks={count_staged_source_checks(workbench_context_actions_staged)}")
    print(
        "workbench_context_actions_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_search_filter_staged_receipt={WORKBENCH_SEARCH_FILTER_STAGED_PROOF}")
    print(f"workbench_search_filter_staged_status={str(workbench_search_filter_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_search_filter_staged_contract={str(workbench_search_filter_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_search_filter_staged_tier={str(workbench_search_filter_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_search_filter_staged_route_count={len(workbench_search_filter_staged.get('expected_routes') or [])}")
    print(f"workbench_search_filter_staged_source_checks={count_staged_source_checks(workbench_search_filter_staged)}")
    print(
        "workbench_search_filter_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_layout_presets_staged_receipt={WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF}")
    print(f"workbench_layout_presets_staged_status={str(workbench_layout_presets_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_layout_presets_staged_contract={str(workbench_layout_presets_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_layout_presets_staged_tier={str(workbench_layout_presets_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_layout_presets_staged_route_count={len(workbench_layout_presets_staged.get('expected_routes') or [])}")
    print(f"workbench_layout_presets_staged_source_checks={count_staged_source_checks(workbench_layout_presets_staged)}")
    print(
        "workbench_layout_presets_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"workbench_activity_feed_staged_receipt={WORKBENCH_ACTIVITY_FEED_STAGED_PROOF}")
    print(f"workbench_activity_feed_staged_status={str(workbench_activity_feed_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"workbench_activity_feed_staged_contract={str(workbench_activity_feed_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"workbench_activity_feed_staged_tier={str(workbench_activity_feed_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"workbench_activity_feed_staged_route_count={len(workbench_activity_feed_staged.get('expected_routes') or [])}")
    print(f"workbench_activity_feed_staged_source_checks={count_staged_source_checks(workbench_activity_feed_staged)}")
    print(
        "workbench_activity_feed_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"legacy_control_coverage_staged_receipt={LEGACY_CONTROL_COVERAGE_STAGED_PROOF}")
    print(f"legacy_control_coverage_staged_status={str(legacy_control_coverage_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"legacy_control_coverage_staged_contract={str(legacy_control_coverage_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"legacy_control_coverage_staged_tier={str(legacy_control_coverage_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"legacy_control_coverage_staged_control_count={legacy_control_coverage_staged.get('control_count', 'unknown')}")
    print(f"legacy_control_coverage_staged_covered_count={legacy_control_coverage_staged.get('covered_control_count', 'unknown')}")
    print(
        "legacy_control_coverage_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"source_staged_proof_set_receipt={SOURCE_STAGED_PROOF_SET}")
    print(f"source_staged_proof_set_status={str(source_staged_proof_set.get('status') or '').strip() or 'not_generated'}")
    print(f"source_staged_proof_set_contract={str(source_staged_proof_set.get('contract_name') or '').strip() or 'missing'}")
    print(f"source_staged_proof_set_tier={str(source_staged_proof_set.get('proof_tier') or '').strip() or 'missing'}")
    print(f"source_staged_proof_set_required_receipts={source_staged_proof_set.get('required_receipt_count', 'unknown')}")
    print(f"source_staged_proof_set_passed_receipts={source_staged_proof_set.get('passed_receipt_count', 'unknown')}")
    print(f"source_staged_proof_set_expected_routes={source_staged_proof_set.get('expected_route_count', 'unknown')}")
    print(
        "source_staged_proof_set_note="
        "aggregate_source_alignment_only_not_browser_execution"
    )
    print(f"portal_installer_handoff_staged_receipt={PORTAL_INSTALLER_HANDOFF_STAGED_PROOF}")
    print(f"portal_installer_handoff_staged_status={str(portal_installer_handoff_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"portal_installer_handoff_staged_contract={str(portal_installer_handoff_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"portal_installer_handoff_staged_tier={str(portal_installer_handoff_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"portal_installer_handoff_staged_route_count={len(portal_installer_handoff_staged.get('expected_routes') or [])}")
    print(
        "portal_installer_handoff_staged_note="
        "source_alignment_only_not_browser_execution"
    )
    print(f"docker_self_host_operator_staged_receipt={DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF}")
    print(f"docker_self_host_operator_staged_status={str(docker_self_host_operator_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"docker_self_host_operator_staged_contract={str(docker_self_host_operator_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"docker_self_host_operator_staged_tier={str(docker_self_host_operator_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"docker_self_host_operator_staged_service_count={len(docker_self_host_operator_staged.get('expected_services') or [])}")
    print(
        "docker_self_host_operator_staged_note="
        "source_alignment_only_not_docker_runtime"
    )
    print(f"account_support_handoff_staged_receipt={ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF}")
    print(f"account_support_handoff_staged_status={str(account_support_handoff_staged.get('status') or '').strip() or 'not_generated'}")
    print(f"account_support_handoff_staged_contract={str(account_support_handoff_staged.get('contract_name') or '').strip() or 'missing'}")
    print(f"account_support_handoff_staged_tier={str(account_support_handoff_staged.get('proof_tier') or '').strip() or 'missing'}")
    print(f"account_support_handoff_staged_route_count={len(account_support_handoff_staged.get('expected_routes') or [])}")
    print(
        "account_support_handoff_staged_note="
        "source_alignment_only_not_auth_or_support_runtime"
    )
    print(f"runtime_proof_refresh_plan_receipt={RUNTIME_PROOF_REFRESH_PLAN}")
    print(f"runtime_proof_refresh_plan_status={str(runtime_proof_refresh_plan.get('status') or '').strip() or 'not_generated'}")
    print(f"runtime_proof_refresh_plan_contract={str(runtime_proof_refresh_plan.get('contract_name') or '').strip() or 'missing'}")
    print(f"runtime_proof_refresh_plan_tier={str(runtime_proof_refresh_plan.get('proof_tier') or '').strip() or 'missing'}")
    print(f"runtime_proof_refresh_plan_scope={str(runtime_proof_refresh_plan.get('scope') or '').strip() or 'missing'}")
    print(f"runtime_proof_refresh_plan_command_source_count={len(runtime_proof_refresh_plan.get('command_sources') or [])}")
    print(f"runtime_proof_refresh_plan_documentation_source_count={len(runtime_proof_refresh_plan.get('documentation_sources') or [])}")
    print(
        "runtime_proof_refresh_plan_note="
        "source_plan_only_not_browser_execution"
    )
    print(f"staged_to_runtime_promotion_matrix_receipt={STAGED_TO_RUNTIME_PROMOTION_MATRIX}")
    print(f"staged_to_runtime_promotion_matrix_status={str(staged_to_runtime_promotion_matrix.get('status') or '').strip() or 'not_generated'}")
    print(f"staged_to_runtime_promotion_matrix_contract={str(staged_to_runtime_promotion_matrix.get('contract_name') or '').strip() or 'missing'}")
    print(f"staged_to_runtime_promotion_matrix_tier={str(staged_to_runtime_promotion_matrix.get('proof_tier') or '').strip() or 'missing'}")
    print(f"staged_to_runtime_promotion_matrix_family_count={staged_to_runtime_promotion_matrix.get('promotion_family_count', 'unknown')}")
    print(
        "staged_to_runtime_promotion_matrix_note="
        "source_plan_only_not_browser_execution"
    )
    print(f"blocker_receipt={BLOCKERS}")
    print(f"blocker_status={str(blockers.get('status') or '').strip() or 'missing'}")
    print(
        "blocker_route_entry_shape="
        f"{str(blockers.get('browser_route_entry_proof_shape') or '').strip() or 'missing'}"
    )
    print(
        "blocker_execution_summary="
        f"{str(blockers.get('browser_execution_proof_status') or '').strip() or 'missing'}"
    )
    print(
        "blocker_execution_error="
        f"{str(blockers.get('browser_execution_proof_error') or '').strip() or 'missing'}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
