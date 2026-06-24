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
