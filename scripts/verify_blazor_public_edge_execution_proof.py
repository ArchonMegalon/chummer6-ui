#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
RECEIPT_PATH = REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json"
EXPECTED_CONTRACT = "chummer6-ui.blazor_public_edge_execution_proof"
ALLOWED_STATUSES = {"not_run", "pass", "passed", "ready"}
EXPECTED_PROOF_TIER = "hosted_promoted_route_execution"
EXPECTED_ROUTE_LANE = "promoted_blazor_workbench"
EXPECTED_PROMOTED_ROUTE_BASE = "/blazor/workbench"
SUPPORTED_PLAYWRIGHT_SCOPES = {"smoke", "full"}
AVAILABLE_WORKFLOW_FAMILY_IDS = {
    "promoted_startup_command_executions",
    "promoted_dense_tool_surfaces",
    "promoted_origin_rules_continuity",
    "promoted_build_lab_continuity",
    "promoted_weapon_selection_execution",
    "promoted_skill_selection_execution",
    "promoted_vehicle_selection_execution",
    "promoted_vehicle_mod_selection_execution",
    "promoted_quality_selection_execution",
    "promoted_quality_delete_execution",
    "promoted_spell_selection_execution",
    "promoted_magic_delete_execution",
    "promoted_cyberware_selection_execution",
    "promoted_cyberware_edit_execution",
    "promoted_cyberware_delete_execution",
    "promoted_drug_selection_execution",
    "promoted_contact_connection_execution",
    "promoted_vehicle_edit_execution",
    "promoted_vehicle_delete_execution",
    "promoted_contact_delete_execution",
    "promoted_contact_edit_execution",
    "promoted_resumed_workspace",
    "promoted_recent_work_affordances",
    "promoted_restored_section_continuations",
    "promoted_restored_tab_landings",
    "promoted_restored_section_content",
    "promoted_result_continuations",
    "promoted_action_continuations",
    "promoted_advanced_action_affordances",
    "promoted_advanced_action_executions",
    "promoted_committed_actions",
    "promoted_advanced_committed_actions",
}
FULL_ONLY_WORKFLOW_FAMILY_IDS = {
    "promoted_advanced_committed_actions",
}
SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS = {
    "promoted_startup_command_executions",
    "promoted_dense_tool_surfaces",
    "promoted_origin_rules_continuity",
    "promoted_build_lab_continuity",
    "promoted_resumed_workspace",
    "promoted_result_continuations",
    "promoted_action_continuations",
    "promoted_committed_actions",
    "promoted_advanced_action_executions",
}
ALLOWED_CHECK_STATUSES = {"pass", "passed", "ready"}


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def required_workflow_family_ids_for_scope(scope: str) -> set[str]:
    if scope == "full":
        return set(AVAILABLE_WORKFLOW_FAMILY_IDS)
    return set(SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS)


def main() -> int:
    reasons: list[str] = []

    if not RECEIPT_PATH.is_file():
      print(f"missing receipt: {RECEIPT_PATH}")
      return 1

    payload = load_json(RECEIPT_PATH)
    contract = str(payload.get("contract_name") or "").strip()
    status = str(payload.get("status") or "").strip().lower()
    base_url = str(payload.get("base_url") or "").strip()
    proof_tier = str(payload.get("proof_tier") or "").strip()
    route_lane = str(payload.get("route_lane") or "").strip()
    promoted_route_base = str(payload.get("promoted_route_base") or "").strip()
    playwright_scope = str(payload.get("playwright_scope") or "").strip().lower()
    required_workflow_family_ids = {
        str(item).strip()
        for item in (payload.get("required_workflow_family_ids") or [])
        if str(item).strip()
    }
    available_workflow_family_ids = {
        str(item).strip()
        for item in (payload.get("available_workflow_family_ids") or [])
        if str(item).strip()
    }
    expected_required_workflow_family_ids = required_workflow_family_ids_for_scope(playwright_scope)

    if contract != EXPECTED_CONTRACT:
        reasons.append(
            f"contract mismatch: expected {EXPECTED_CONTRACT!r}, got {contract!r}"
        )
    if status not in ALLOWED_STATUSES:
        reasons.append(
            f"status must be one of {sorted(ALLOWED_STATUSES)!r}, got {status!r}"
        )
    if not base_url:
        reasons.append("base_url is missing")
    if proof_tier != EXPECTED_PROOF_TIER:
        reasons.append(
            f"proof_tier mismatch: expected {EXPECTED_PROOF_TIER!r}, got {proof_tier!r}"
        )
    if route_lane != EXPECTED_ROUTE_LANE:
        reasons.append(
            f"route_lane mismatch: expected {EXPECTED_ROUTE_LANE!r}, got {route_lane!r}"
        )
    if promoted_route_base != EXPECTED_PROMOTED_ROUTE_BASE:
        reasons.append(
            "promoted_route_base mismatch: "
            f"expected {EXPECTED_PROMOTED_ROUTE_BASE!r}, got {promoted_route_base!r}"
        )
    if playwright_scope not in SUPPORTED_PLAYWRIGHT_SCOPES:
        reasons.append(
            f"playwright_scope must be one of {sorted(SUPPORTED_PLAYWRIGHT_SCOPES)!r}, got {playwright_scope!r}"
        )
    elif required_workflow_family_ids != expected_required_workflow_family_ids:
        reasons.append(
            "required_workflow_family_ids mismatch for scope "
            f"{playwright_scope!r}: expected {sorted(expected_required_workflow_family_ids)!r}, got {sorted(required_workflow_family_ids)!r}"
        )
    if available_workflow_family_ids and available_workflow_family_ids != AVAILABLE_WORKFLOW_FAMILY_IDS:
        reasons.append(
            "available_workflow_family_ids mismatch: expected "
            f"{sorted(AVAILABLE_WORKFLOW_FAMILY_IDS)!r}, got {sorted(available_workflow_family_ids)!r}"
        )

    if status in {"pass", "passed", "ready"}:
        workflow_families = payload.get("workflow_families")
        if not isinstance(workflow_families, list) or not workflow_families:
            reasons.append("passing hosted execution receipt must include workflow_families")
        else:
            workflow_ids = set()
            for item in workflow_families:
                if not isinstance(item, dict):
                    reasons.append("workflow family entries must be objects")
                    continue

                workflow_id = str(item.get("id") or "").strip()
                if workflow_id:
                    workflow_ids.add(workflow_id)
                else:
                    reasons.append("workflow family is missing id")
                    continue

                family_route_lane = str(item.get("route_lane") or "").strip()
                if family_route_lane != EXPECTED_ROUTE_LANE:
                    reasons.append(
                        "workflow family route_lane mismatch for "
                        f"{workflow_id!r}: expected {EXPECTED_ROUTE_LANE!r}, got {family_route_lane!r}"
                    )

                checks = item.get("checks")
                if not isinstance(checks, list) or not checks:
                    reasons.append(
                        f"workflow family {workflow_id!r} must include at least one browser-visible check"
                    )
                    continue

                for index, check in enumerate(checks, start=1):
                    if not isinstance(check, dict):
                        reasons.append(
                            f"workflow family {workflow_id!r} check #{index} must be an object"
                        )
                        continue

                    route = str(check.get("route") or "").strip()
                    assertion = str(check.get("assertion") or "").strip()
                    check_status = str(check.get("status") or "").strip().lower()

                    if not route:
                        reasons.append(
                            f"workflow family {workflow_id!r} check #{index} is missing route"
                        )
                    elif not route.startswith(f"{EXPECTED_PROMOTED_ROUTE_BASE}?") and route != EXPECTED_PROMOTED_ROUTE_BASE:
                        reasons.append(
                            "workflow family "
                            f"{workflow_id!r} check #{index} route must stay on the canonical proof-compatible lane; got {route!r}"
                        )

                    if not assertion:
                        reasons.append(
                            f"workflow family {workflow_id!r} check #{index} is missing assertion"
                        )

                    if check_status not in ALLOWED_CHECK_STATUSES:
                        reasons.append(
                            "workflow family "
                            f"{workflow_id!r} check #{index} status must be one of {sorted(ALLOWED_CHECK_STATUSES)!r}, got {check_status!r}"
                        )

            missing_workflows = sorted(expected_required_workflow_family_ids - workflow_ids)
            if missing_workflows:
                reasons.append(
                    "passing hosted execution receipt is missing required workflow families for "
                    f"scope {playwright_scope!r}: "
                    + ", ".join(missing_workflows)
                )

    if reasons:
        print("\n".join(reasons))
        return 1

    print(f"blazor_public_edge_execution_proof:ok {RECEIPT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
