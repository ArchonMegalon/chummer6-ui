#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Sequence


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_RECEIPT_PATH = REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
EXPECTED_CONTRACT = "chummer6-ui.blazor_public_edge_workbench_proof"
ALLOWED_STATUSES = {"not_run", "pass", "passed", "ready"}
REQUIRED_ROUTE_PROOF_MARKERS = {
    "public_chummer_app_route",
    "public_chummer_app_roster_route",
    "public_blazor_root_redirect",
    "public_blazor_home_roster_entry",
    "public_blazor_health",
    "public_workbench_route",
    "public_workspace_restore_route",
    "public_startup_deep_link_route",
    "public_result_continuation_routes",
    "public_action_continuation_routes",
    "public_committed_action_route",
}
EXPANDED_ROUTE_PROOF_MARKERS = {
    "public_startup_workbench_command_routes",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
}
REQUIRED_WORKFLOW_PROOFS = {
    "blazor_root_redirect",
    "workbench_route",
    "workspace_resume_route_shape",
    "new_character_deep_link_route_shape",
    "result_continuation_route_shapes",
    "action_continuation_route_shapes",
    "committed_action_route_shape",
}
EXPANDED_WORKFLOW_PROOFS = {
    "startup_command_route_shapes",
    "advanced_action_route_shapes",
    "advanced_committed_action_route_shapes",
}
REQUIRED_PROOF_ROUTES = {
    "/blazor/",
    "/app",
    "/app?command=character_roster",
    "/blazor/health",
    "/blazor/home",
    "/blazor/app",
    "/blazor/workbench",
    "/blazor/workbench?workspace=ws-1",
    "/blazor/preview?command=new_character",
    "/blazor/workbench?workspace=ws-1&command=save_character_as",
    "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
    "/blazor/workbench?workspace=ws-1&command=print_character",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add",
}
EXPANDED_PROOF_ROUTES = {
    "/blazor/workbench?command=new_character",
    "/blazor/workbench?command=open_character",
    "/blazor/workbench?command=open_for_printing",
    "/blazor/workbench?command=open_for_export",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add",
}
ALLOWED_PROOF_SHAPES = {"core", "expanded"}
REQUIRED_ROUTE_MODEL_NOTE = (
    "Public product navigation remains /app, /blazor/ redirects into the roster-first app?command=character_roster browser workflow, /blazor/app is the hosted app path, /blazor/home carries the roster-first orientation entry, and /blazor/workbench is the canonical proof-compatible route base."
)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify a hosted Blazor route-entry receipt."
    )
    parser.add_argument(
        "--receipt-path",
        type=Path,
        default=Path(
            os.environ.get(
                "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH",
                str(DEFAULT_RECEIPT_PATH),
            )
        ),
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = ()) -> int:
    args = parse_args(argv)
    receipt_path = args.receipt_path
    reasons: list[str] = []

    if not receipt_path.is_file():
        print(f"missing receipt: {receipt_path}")
        return 1

    payload = load_json(receipt_path)
    contract = str(payload.get("contract_name") or "").strip()
    status = str(payload.get("status") or "").strip().lower()
    base_url = str(payload.get("base_url") or "").strip()
    proof_shape = str(payload.get("proof_shape") or "").strip().lower()

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
    if proof_shape and proof_shape not in ALLOWED_PROOF_SHAPES:
        reasons.append(
            f"proof_shape must be one of {sorted(ALLOWED_PROOF_SHAPES)!r} when present, got {proof_shape!r}"
        )

    if status in {"pass", "passed", "ready"}:
        runtime_required = payload.get("runtime_required")
        route_probe_executed = payload.get("route_probe_executed")
        route_proof_markers = payload.get("route_proof_markers")
        workflow_proofs = payload.get("workflow_proofs")
        proof_routes = payload.get("proof_routes")
        route_probes = payload.get("route_probes")
        route_probe_failures = payload.get("route_probe_failures")
        route_probe_count = payload.get("route_probe_count")
        notes = payload.get("notes")

        if runtime_required is not True:
            reasons.append("passing hosted route-entry receipt must set runtime_required=true")
        if route_probe_executed is not True:
            reasons.append("passing hosted route-entry receipt must set route_probe_executed=true")

        if not isinstance(route_proof_markers, list) or not route_proof_markers:
            reasons.append("passing hosted route-entry receipt must include route_proof_markers")
            route_marker_ids: set[str] = set()
        else:
            route_marker_ids = {str(item).strip() for item in route_proof_markers if str(item).strip()}
            missing_markers = sorted(REQUIRED_ROUTE_PROOF_MARKERS - route_marker_ids)
            if missing_markers:
                reasons.append(
                    "passing hosted route-entry receipt is missing required route proof markers: "
                    + ", ".join(missing_markers)
                )
            expanded_route_markers_present = bool(EXPANDED_ROUTE_PROOF_MARKERS & route_marker_ids)
            missing_expanded_route_markers = sorted(EXPANDED_ROUTE_PROOF_MARKERS - route_marker_ids)
            if expanded_route_markers_present and missing_expanded_route_markers:
                reasons.append(
                    "passing hosted route-entry receipt partially declares expanded route proof markers but is missing: "
                    + ", ".join(missing_expanded_route_markers)
                )

        if not isinstance(workflow_proofs, list) or not workflow_proofs:
            reasons.append("passing hosted route-entry receipt must include workflow_proofs")
            workflow_proof_ids: set[str] = set()
        else:
            workflow_proof_ids = {str(item).strip() for item in workflow_proofs if str(item).strip()}
            missing_workflows = sorted(REQUIRED_WORKFLOW_PROOFS - workflow_proof_ids)
            if missing_workflows:
                reasons.append(
                    "passing hosted route-entry receipt is missing required workflow proofs: "
                    + ", ".join(missing_workflows)
                )
            expanded_workflows_present = bool(EXPANDED_WORKFLOW_PROOFS & workflow_proof_ids)
            missing_expanded_workflows = sorted(EXPANDED_WORKFLOW_PROOFS - workflow_proof_ids)
            if expanded_workflows_present and missing_expanded_workflows:
                reasons.append(
                    "passing hosted route-entry receipt partially declares expanded workflow proofs but is missing: "
                    + ", ".join(missing_expanded_workflows)
                )

        if not isinstance(proof_routes, list) or not proof_routes:
            reasons.append("passing hosted route-entry receipt must include proof_routes")
            proof_route_ids: set[str] = set()
        else:
            proof_route_ids = {str(item).strip() for item in proof_routes if str(item).strip()}
            missing_routes = sorted(REQUIRED_PROOF_ROUTES - proof_route_ids)
            if missing_routes:
                reasons.append(
                    "passing hosted route-entry receipt is missing required proof routes: "
                    + ", ".join(missing_routes)
                )
            expanded_routes_present = bool(EXPANDED_PROOF_ROUTES & proof_route_ids)
            missing_expanded_routes = sorted(EXPANDED_PROOF_ROUTES - proof_route_ids)
            if expanded_routes_present and missing_expanded_routes:
                reasons.append(
                    "passing hosted route-entry receipt partially declares expanded proof routes but is missing: "
                    + ", ".join(missing_expanded_routes)
                )

        if not isinstance(route_probes, list) or not route_probes:
            reasons.append("passing hosted route-entry receipt must include route_probes")
        elif isinstance(proof_routes, list):
            expected_routes = [str(item).strip() for item in proof_routes]
            probe_routes: list[str] = []
            for index, probe in enumerate(route_probes):
                if not isinstance(probe, dict):
                    reasons.append(f"route_probes[{index}] must be an object")
                    continue
                route = str(probe.get("route") or "").strip()
                probe_routes.append(route)
                if probe.get("checked") is not True:
                    reasons.append(f"route probe {route or index!r} must set checked=true")
                http_status = probe.get("http_status")
                if (
                    not isinstance(http_status, int)
                    or isinstance(http_status, bool)
                    or not 200 <= http_status < 400
                ):
                    reasons.append(
                        f"route probe {route or index!r} must record a successful HTTP status"
                    )
                if probe.get("ok") is not True:
                    reasons.append(f"route probe {route or index!r} must set ok=true")
                if str(probe.get("error") or "").strip():
                    reasons.append(f"route probe {route or index!r} must have an empty error")
                expected_url = base_url.rstrip("/") + route
                actual_url = str(probe.get("url") or "").strip()
                if route and actual_url != expected_url:
                    reasons.append(
                        f"route probe {route!r} URL mismatch: expected {expected_url!r}, got {actual_url!r}"
                    )

            missing_probe_routes = sorted(set(expected_routes) - set(probe_routes))
            unexpected_probe_routes = sorted(set(probe_routes) - set(expected_routes))
            duplicate_probe_routes = sorted(
                route for route in set(probe_routes) if probe_routes.count(route) > 1
            )
            if missing_probe_routes:
                reasons.append(
                    "passing hosted route-entry receipt is missing route probes: "
                    + ", ".join(missing_probe_routes)
                )
            if unexpected_probe_routes:
                reasons.append(
                    "passing hosted route-entry receipt has unexpected route probes: "
                    + ", ".join(unexpected_probe_routes)
                )
            if duplicate_probe_routes:
                reasons.append(
                    "passing hosted route-entry receipt has duplicate route probes: "
                    + ", ".join(duplicate_probe_routes)
                )
        if route_probe_failures not in ([], None):
            reasons.append("passing hosted route-entry receipt must not contain route_probe_failures")
        if isinstance(route_probe_count, int) and isinstance(proof_routes, list):
            if route_probe_count != len(proof_routes):
                reasons.append(
                    "route_probe_count mismatch: "
                    f"expected {len(proof_routes)}, got {route_probe_count}"
                )
            if isinstance(route_probes, list) and route_probe_count != len(route_probes):
                reasons.append(
                    "route_probe_count does not match route_probes length: "
                    f"expected {len(route_probes)}, got {route_probe_count}"
                )
        else:
            reasons.append("passing hosted route-entry receipt must include integer route_probe_count")

        if not isinstance(notes, list) or REQUIRED_ROUTE_MODEL_NOTE not in {str(note).strip() for note in notes}:
            reasons.append(
                "passing hosted route-entry receipt must state the roster-first /blazor/ redirect, /blazor/app hosted route, and /blazor/workbench proof-base boundary"
            )

        expanded_declared = (
            EXPANDED_ROUTE_PROOF_MARKERS.issubset(route_marker_ids)
            or EXPANDED_WORKFLOW_PROOFS.issubset(workflow_proof_ids)
            or EXPANDED_PROOF_ROUTES.issubset(proof_route_ids)
        )
        if proof_shape == "core" and expanded_declared:
            reasons.append(
                "proof_shape='core' is inconsistent with expanded hosted route-entry markers, workflows, or routes"
            )
        if proof_shape == "expanded" and not expanded_declared:
            reasons.append(
                "proof_shape='expanded' requires the full expanded hosted route-entry marker/workflow/route set"
            )

    if reasons:
        print("\n".join(reasons))
        return 1

    print(f"blazor_public_edge_workbench_proof:ok {receipt_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(None))
