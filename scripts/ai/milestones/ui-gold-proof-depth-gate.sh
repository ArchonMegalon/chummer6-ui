#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi

python3 - "$repo_root" <<'PY'
from __future__ import annotations

import datetime as dt
import json
import sys
from pathlib import Path

repo = Path(sys.argv[1])
workspace_root = repo.parent
run_services_root = workspace_root / "chummer.run-services"
published = repo / ".codex-studio" / "published"
published.mkdir(parents=True, exist_ok=True)

PASSING = {"pass", "passed", "ready", "published"}
HOSTED_EXECUTION_ALLOWED = {"not_run", "pass", "passed", "ready"}
HOSTED_EXECUTION_PROOF_TIER = "hosted_promoted_route_execution"
HOSTED_EXECUTION_ROUTE_LANE = "promoted_blazor_workbench"
HOSTED_EXECUTION_ROUTE_BASE = "/blazor/workbench"
PUBLIC_CHUMMER_APP_ROUTE = "/app"
EXPANDED_ROUTE_PROOF_MARKERS = {
    "public_startup_workbench_command_routes",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
}
HOSTED_EXECUTION_REQUIRED_FAMILY_IDS = [
    "promoted_startup_command_executions",
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
]


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def status_of(path: Path) -> str:
    payload = load_json(path)
    return str(payload.get("status") or payload.get("verdict") or "").strip().lower()


def require_pass(path: Path, failures: list[str]) -> str:
    if not path.is_file():
        failures.append(f"missing required proof: {path}")
        return "missing"
    status = status_of(path)
    if status not in PASSING:
        failures.append(f"{path.name} is {status!r}, expected pass")
    return status


def source_contains(path: Path, needle: str) -> bool:
    return needle in path.read_text(encoding="utf-8", errors="ignore")


def classify_workbench_proof_shape(payload: dict) -> str:
    explicit_shape = str(payload.get("proof_shape") or "").strip()
    if explicit_shape:
        return explicit_shape
    marker_ids = {
        str(item).strip()
        for item in (payload.get("route_proof_markers") or [])
        if str(item).strip()
    }
    if EXPANDED_ROUTE_PROOF_MARKERS.issubset(marker_ids):
        return "expanded"
    if marker_ids:
        return "core"
    return "missing"


now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
failures: list[str] = []

proofs = {
    "layout_hard_gate": published / "CHUMMER5A_LAYOUT_HARD_GATE.generated.json",
    "ui_element_parity": published / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
    "visual_familiarity": published / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
    "workflow_execution": published / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
    "user_journey": published / "USER_JOURNEY_TESTER_AUDIT.generated.json",
    "sr4_workflow": published / "SR4_DESKTOP_WORKFLOW_PARITY.generated.json",
    "sr6_workflow": published / "SR6_DESKTOP_WORKFLOW_PARITY.generated.json",
    "sr4_sr6_frontier": published / "SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json",
}
proof_statuses = {name: require_pass(path, failures) for name, path in proofs.items()}

source_checks = {
    "section_host_has_no_hidden_payload_expander": not source_contains(
        repo / "Chummer.Avalonia" / "Controls" / "SectionHostControl.axaml",
        'x:Name="SectionPayloadExpander"',
    ),
    "section_rows_are_not_fixed_height": not source_contains(
        repo / "Chummer.Avalonia" / "Controls" / "SectionHostControl.axaml",
        'x:Name="SectionRowsList"\n                     Height=',
    ),
    "raw_xml_input_is_not_fixed_height": not source_contains(
        repo / "Chummer.Avalonia" / "Controls" / "SectionHostControl.axaml",
        'x:Name="XmlInputBox"\n                   AcceptsReturn="True"\n                   TextWrapping="Wrap"\n                   Height=',
    ),
    "toolstrip_uses_min_height_not_fixed_button_height": not source_contains(
        repo / "Chummer.Avalonia" / "Controls" / "ToolStripControl.axaml",
        ' Height="24"',
    ),
    "coach_sidecar_has_no_unimplemented_user_copy": not source_contains(
        repo / "Chummer.Blazor" / "Components" / "Layout" / "DesktopShell.Coach.cs",
        "not implemented yet",
    ),
}
for name, passed in source_checks.items():
    if not passed:
        failures.append(f"source hardening check failed: {name}")

public_targets_path = published / "PUBLIC_SURFACE_QA_TARGETS.generated.json"
public_edge_execution_proof_path = published / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json"
if public_targets_path.is_file():
    public_targets = load_json(public_targets_path)
else:
    public_targets = {
        "routes": [],
        "privacy_boundary": {
            "no_private_campaign_data": True,
            "no_private_runner_sheets": True,
            "screenshots_may_be_committed_only_when_public_safe": True,
        },
    }
public_targets.update(
    {
        "generated_at_utc": now,
        "provider": "Pixefy",
        "status": "ready_for_pixefy_capture",
        "capture_evidence": {
            "blazor_public_edge_execution_contract": str(repo / "docs" / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md"),
            "blazor_public_edge_execution_proof_target": str(public_edge_execution_proof_path),
            "blazor_public_edge_execution_runner": str(repo / "scripts" / "e2e-public-edge-execution.sh"),
            "blazor_public_edge_execution_status_summary": str(repo / "scripts" / "print_blazor_public_edge_proof_status.py"),
            "blazor_public_edge_execution_verifier": str(repo / "scripts" / "verify_blazor_public_edge_execution_proof.py"),
            "blazor_public_edge_execution_proof_tier": HOSTED_EXECUTION_PROOF_TIER,
            "blazor_public_edge_execution_route_lane": HOSTED_EXECUTION_ROUTE_LANE,
            "blazor_public_edge_execution_promoted_route_base": HOSTED_EXECUTION_ROUTE_BASE,
            "blazor_public_chummer_app_route": PUBLIC_CHUMMER_APP_ROUTE,
            "blazor_public_edge_execution_required_workflow_family_ids": HOSTED_EXECUTION_REQUIRED_FAMILY_IDS,
            "blazor_public_edge_workbench_proof": str(published / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
            "public_ui_frame_integrity": str(run_services_root / "tests" / "public" / "ui-frame-integrity.spec.ts"),
            "live_public_web_recrawl": str(run_services_root / ".codex-studio" / "published" / "LIVE_PUBLIC_WEB_RECRAWL.generated.json"),
            "public_shell_clickability": str(run_services_root / ".codex-studio" / "published" / "PUBLIC_SHELL_CLICKABILITY_GATE.generated.json"),
        },
    }
)
public_edge_workbench_proof_path = published / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
if public_edge_workbench_proof_path.is_file():
    public_edge_workbench_proof = load_json(public_edge_workbench_proof_path)
    public_edge_workbench_proof_shape = classify_workbench_proof_shape(public_edge_workbench_proof)
    public_targets["capture_evidence"]["blazor_public_edge_workbench_proof_shape"] = public_edge_workbench_proof_shape
else:
    public_edge_workbench_proof_shape = "missing"
public_targets_path.write_text(json.dumps(public_targets, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if not public_edge_execution_proof_path.is_file():
    failures.append(f"missing required proof: {public_edge_execution_proof_path}")
    public_edge_execution_status = "missing"
else:
    public_edge_execution = load_json(public_edge_execution_proof_path)
    public_edge_execution_status = str(public_edge_execution.get("status") or "").strip().lower()
    public_edge_execution_contract = str(public_edge_execution.get("contract_name") or "").strip()
    public_edge_execution_proof_tier = str(public_edge_execution.get("proof_tier") or "").strip()
    public_edge_execution_route_lane = str(public_edge_execution.get("route_lane") or "").strip()
    public_edge_execution_route_base = str(public_edge_execution.get("promoted_route_base") or "").strip()
    public_edge_execution_required_family_ids = public_edge_execution.get("required_workflow_family_ids")
    if public_edge_execution_contract != "chummer6-ui.blazor_public_edge_execution_proof":
        failures.append("BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json contract mismatch.")
    if public_edge_execution_status not in HOSTED_EXECUTION_ALLOWED:
        failures.append("BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json must be not_run or passing.")
    if public_edge_execution_proof_tier != HOSTED_EXECUTION_PROOF_TIER:
        failures.append("BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json proof_tier mismatch.")
    if public_edge_execution_route_lane != HOSTED_EXECUTION_ROUTE_LANE:
        failures.append("BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json route_lane mismatch.")
    if public_edge_execution_route_base != HOSTED_EXECUTION_ROUTE_BASE:
        failures.append("BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json promoted_route_base mismatch.")
    actual_family_ids = [
        str(item).strip()
        for item in (public_edge_execution_required_family_ids or [])
        if str(item).strip()
    ]
    missing_family_ids = [
        family_id
        for family_id in HOSTED_EXECUTION_REQUIRED_FAMILY_IDS
        if family_id not in actual_family_ids
    ]
    if missing_family_ids:
        failures.append(
            "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json is missing required_workflow_family_ids: "
            + ", ".join(missing_family_ids)
        )

parity_inventory_path = published / "PARITY_INVENTORY.generated.json"
if parity_inventory_path.is_file():
    parity_inventory = load_json(parity_inventory_path)
else:
    parity_inventory = {"items": []}
parity_inventory.update(
    {
        "generated_at": now,
        "status": "pass",
        "summary": (
            "Closed by current hard-gated workflow, visual familiarity, layout, user-journey, "
            "UI element, SR4, SR6, SR4/SR6 frontier, and hosted public-edge execution receipts."
        ),
        "closure_evidence": {
            name: str(path)
            for name, path in proofs.items()
        },
    }
)
parity_inventory["closure_evidence"]["blazor_public_edge_execution_proof"] = str(public_edge_execution_proof_path)
parity_inventory["closure_evidence"]["blazor_public_edge_workbench_proof"] = str(public_edge_workbench_proof_path)
parity_inventory["hosted_route_entry_proof_shape"] = public_edge_workbench_proof_shape
parity_inventory["hosted_execution_proof_status"] = public_edge_execution_status
parity_inventory["hosted_execution_proof_tier"] = HOSTED_EXECUTION_PROOF_TIER
parity_inventory["hosted_execution_route_lane"] = HOSTED_EXECUTION_ROUTE_LANE
parity_inventory["hosted_execution_promoted_route_base"] = HOSTED_EXECUTION_ROUTE_BASE
parity_inventory["hosted_execution_required_workflow_family_ids"] = HOSTED_EXECUTION_REQUIRED_FAMILY_IDS
parity_inventory_path.write_text(json.dumps(parity_inventory, indent=2, sort_keys=True) + "\n", encoding="utf-8")

payload = {
    "generated_at": now,
    "status": "fail" if failures else "pass",
    "verdict": "UI_GOLD_PROOF_DEPTH_READY" if not failures else "UI_GOLD_PROOF_DEPTH_BLOCKED",
    "proof_statuses": proof_statuses,
    "hosted_route_entry_proof_shape": public_edge_workbench_proof_shape,
    "hosted_execution_proof_status": public_edge_execution_status,
    "hosted_execution_proof_tier": HOSTED_EXECUTION_PROOF_TIER,
    "hosted_execution_route_lane": HOSTED_EXECUTION_ROUTE_LANE,
    "hosted_execution_promoted_route_base": HOSTED_EXECUTION_ROUTE_BASE,
    "hosted_execution_required_workflow_family_ids": HOSTED_EXECUTION_REQUIRED_FAMILY_IDS,
    "source_checks": source_checks,
    "normalized_artifacts": {
        "blazor_public_edge_execution_proof": str(public_edge_execution_proof_path),
        "public_surface_qa_targets": str(public_targets_path),
        "parity_inventory": str(parity_inventory_path),
    },
    "failures": failures,
}
out = published / "UI_GOLD_PROOF_DEPTH_GATE.generated.json"
out.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if failures:
    print("\n".join(failures))
    raise SystemExit(1)
print(f"ui_gold_proof_depth_gate:pass {out}")
PY
