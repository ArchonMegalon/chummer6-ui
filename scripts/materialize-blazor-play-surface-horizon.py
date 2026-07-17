#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PLAY_SURFACE_HORIZON_PATH",
        PUBLISHED / "BLAZOR_PLAY_SURFACE_HORIZON.generated.json",
    )
)
BROWSER_LANE_PROOF_SET_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH",
        PUBLISHED / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json",
    )
)
PUBLIC_EDGE_EXECUTION_HORIZON_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON_PATH",
        PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json",
    )
)
PWA_PUBLIC_EDGE_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PWA_PUBLIC_EDGE_PROOF_PATH",
        PUBLISHED / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json",
    )
)
WORKBENCH_TOUCH_MOBILE_STAGED_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF_PATH",
        PUBLISHED / "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json",
    )
)
WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF_PATH",
        PUBLISHED / "BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json",
    )
)
WORKBENCH_TABLE_HANDOFF_STAGED_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF_PATH",
        PUBLISHED / "BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.generated.json",
    )
)
WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF_PATH",
        PUBLISHED / "BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json",
    )
)
WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF_PATH",
        PUBLISHED / "BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF.generated.json",
    )
)
TABLE_PULSE_SHOWCASE_PATH = Path(
    os.environ.get(
        "CHUMMER_TABLE_PULSE_FLAGSHIP_SHOWCASE_PATH",
        REPO_ROOT / "docs" / "TABLE_PULSE_FLAGSHIP_SHOWCASE.md",
    )
)
TABLE_PULSE_MINIGAMES_PATH = Path(
    os.environ.get(
        "CHUMMER_TABLE_PULSE_REMOTE_REACTION_MINIGAMES_PATH",
        REPO_ROOT / "docs" / "TABLE_PULSE_REMOTE_REACTION_MINIGAMES.md",
    )
)

PUBLIC_DOWNLOAD_RELATIVE_ROOT = "release-evidence/browser-lane"
CONTRACT_NAME = "chummer6-ui.blazor_play_surface_horizon"
PUBLIC_ENTRY_ROUTE = "/app"
PUBLIC_ROSTER_ENTRY_ROUTE = "/app?command=character_roster"
PUBLIC_BLAZOR_ROOT_ROUTE = "/blazor/"
HOSTED_APP_ROUTE = "/blazor/app"
COMPATIBILITY_ROUTE_BASE = "/blazor/workbench"


def load_json(path: Path) -> tuple[dict[str, Any], str | None]:
    if not path.is_file():
        return {}, f"missing receipt: {path}"

    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        return {}, f"invalid JSON in {path}: {exc}"

    if not isinstance(loaded, dict):
        return {}, f"receipt root must be an object: {path}"

    return loaded, None


def normalize_status(payload: dict[str, Any]) -> str:
    return str(payload.get("status") or "").strip().lower()


def public_relative_path(path: Path) -> str:
    return f"{PUBLIC_DOWNLOAD_RELATIVE_ROOT}/{path.name}"


def evaluate_receipt(
    *,
    receipt_id: str,
    label: str,
    path: Path,
    evidence_class: str,
    allowed_statuses: set[str],
) -> dict[str, Any]:
    payload, load_error = load_json(path)
    status = normalize_status(payload) if payload else "missing"
    passed = load_error is None and status in allowed_statuses
    return {
        "id": receipt_id,
        "label": label,
        "path": str(path),
        "public_download_relative_path": public_relative_path(path),
        "contract_name": str(payload.get("contract_name") or "").strip(),
        "status": status or "missing",
        "passed": passed,
        "evidence_class": evidence_class,
        "proof_tier": str(payload.get("proof_tier") or "").strip(),
        "route_lane": str(payload.get("route_lane") or "").strip(),
        "error": load_error or "",
        "payload": payload,
    }


def evaluate_document(
    *,
    document_id: str,
    label: str,
    path: Path,
    required_tokens: list[str],
) -> dict[str, Any]:
    if not path.is_file():
        return {
            "id": document_id,
            "label": label,
            "path": str(path),
            "status": "missing",
            "passed": False,
            "missing_tokens": required_tokens,
        }

    content = path.read_text(encoding="utf-8")
    missing_tokens = [token for token in required_tokens if token not in content]
    return {
        "id": document_id,
        "label": label,
        "path": str(path),
        "status": "passed" if not missing_tokens else "failed",
        "passed": not missing_tokens,
        "missing_tokens": missing_tokens,
    }


def find_execution_horizon(payload: dict[str, Any], horizon_id: str) -> dict[str, Any]:
    horizons = payload.get("horizons") or []
    if not isinstance(horizons, list):
        return {}
    for item in horizons:
        if isinstance(item, dict) and str(item.get("id") or "").strip() == horizon_id:
            return item
    return {}


def summarize_items(items: list[dict[str, Any]]) -> list[dict[str, Any]]:
    summary: list[dict[str, Any]] = []
    for item in items:
        summary.append(
            {
                "id": item["id"],
                "label": item["label"],
                "status": item["status"],
                "public_download_relative_path": item.get("public_download_relative_path", ""),
            }
        )
    return summary


def main() -> int:
    browser_lane = evaluate_receipt(
        receipt_id="browser_lane_proof_set",
        label="Aggregate browser-lane proof set",
        path=BROWSER_LANE_PROOF_SET_PATH,
        evidence_class="runtime_aggregate",
        allowed_statuses={"passed"},
    )
    execution_horizon = evaluate_receipt(
        receipt_id="public_edge_execution_horizon",
        label="Hosted public-edge execution horizon",
        path=PUBLIC_EDGE_EXECUTION_HORIZON_PATH,
        evidence_class="runtime_horizon",
        allowed_statuses={"passed"},
    )
    pwa = evaluate_receipt(
        receipt_id="pwa_public_edge",
        label="Hosted /blazor PWA shell",
        path=PWA_PUBLIC_EDGE_PROOF_PATH,
        evidence_class="runtime_proven",
        allowed_statuses={"passed"},
    )
    touch_mobile = evaluate_receipt(
        receipt_id="touch_mobile_staged",
        label="Touch/mobile session utility",
        path=WORKBENCH_TOUCH_MOBILE_STAGED_PROOF_PATH,
        evidence_class="source_staged",
        allowed_statuses={"passed"},
    )
    campaign_session = evaluate_receipt(
        receipt_id="campaign_session_staged",
        label="Campaign/session handoff utility",
        path=WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF_PATH,
        evidence_class="source_staged",
        allowed_statuses={"passed"},
    )
    table_handoff = evaluate_receipt(
        receipt_id="table_handoff_staged",
        label="Table handoff posture",
        path=WORKBENCH_TABLE_HANDOFF_STAGED_PROOF_PATH,
        evidence_class="source_staged",
        allowed_statuses={"passed"},
    )
    workflow_ledger = evaluate_receipt(
        receipt_id="workflow_ledger_staged",
        label="Workflow ledger posture",
        path=WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF_PATH,
        evidence_class="source_staged",
        allowed_statuses={"passed"},
    )
    progression_ledger = evaluate_receipt(
        receipt_id="progression_ledger_staged",
        label="Progression ledger posture",
        path=WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF_PATH,
        evidence_class="source_staged",
        allowed_statuses={"passed"},
    )
    showcase_doc = evaluate_document(
        document_id="table_pulse_showcase",
        label="Table Pulse flagship showcase",
        path=TABLE_PULSE_SHOWCASE_PATH,
        required_tokens=["Table Pulse", "Runner Passport", "Black Ledger", "living-world stack"],
    )
    minigames_doc = evaluate_document(
        document_id="table_pulse_minigames",
        label="Table Pulse remote reaction minigames",
        path=TABLE_PULSE_MINIGAMES_PATH,
        required_tokens=["Table Pulse", "heat shift", "receipt", "public-safe consequence"],
    )

    failures: list[str] = []
    evaluated_receipts = [
        browser_lane,
        execution_horizon,
        pwa,
        touch_mobile,
        campaign_session,
        table_handoff,
        workflow_ledger,
        progression_ledger,
    ]
    for receipt in evaluated_receipts:
        if not receipt["passed"]:
            detail = receipt["error"] or f"unexpected status {receipt['status']}"
            failures.append(f"{receipt['id']}: {detail}")

    for document in [showcase_doc, minigames_doc]:
        if not document["passed"]:
            failures.append(
                f"{document['id']}: missing required documentation tokens "
                + ", ".join(document["missing_tokens"])
            )

    execution_payload = execution_horizon["payload"]
    near_term_runtime = find_execution_horizon(execution_payload, "near_term_hosted_smoke_execution")
    mid_term_runtime = find_execution_horizon(execution_payload, "mid_term_full_live_public_edge_execution_matrix")
    long_term_runtime = find_execution_horizon(execution_payload, "long_term_full_browser_desktop_parity_breadth")
    current_published_execution = execution_payload.get("current_published_execution") or {}
    if not isinstance(current_published_execution, dict):
        current_published_execution = {}

    near_term_proven = (
        browser_lane["passed"]
        and execution_horizon["passed"]
        and str(near_term_runtime.get("status") or "").strip() == "proven"
    )
    mid_term_sources_ready = all(
        receipt["passed"] for receipt in [touch_mobile, campaign_session, table_handoff]
    )
    long_term_sources_ready = all(
        receipt["passed"] for receipt in [workflow_ledger, progression_ledger, table_handoff]
    ) and showcase_doc["passed"] and minigames_doc["passed"]

    horizons = [
        {
            "id": "near_term_stabilization",
            "title": "Near-term stabilization",
            "status": "proven" if near_term_proven else "not_proven",
            "evidence_tier": "runtime_proven",
            "headline": "Hosted route entry, browser-lane release posture, and smoke execution are currently proven.",
            "summary": (
                "The public app and promoted /blazor workbench routes are release-backed, and the published "
                f"hosted execution scope is {str(current_published_execution.get('playwright_scope') or 'unknown')}."
            ),
            "runtime_proven_receipts": summarize_items([browser_lane, execution_horizon]),
            "source_staged_receipts": [],
            "documentation_sources": [],
            "unproven_claims": [
                "full live public-edge workflow execution breadth remains a separate horizon",
                "desktop parity breadth remains outside hosted browser execution proof",
            ],
            "execution_horizon": {
                "near_term": near_term_runtime,
                "mid_term": mid_term_runtime,
                "long_term": long_term_runtime,
            },
        },
        {
            "id": "mid_term_pwa_session_utility",
            "title": "Mid-term PWA and session utility",
            "status": "mixed" if pwa["passed"] and mid_term_sources_ready else "not_ready",
            "evidence_tier": "runtime_pwa_plus_source_staged_session_utility",
            "headline": "The installable /blazor shell is runtime-proven, but in-session utility remains source-staged.",
            "summary": (
                "The public PWA shell is proven on the hosted edge, while touch/mobile, campaign/session, and "
                "table-handoff utility are staged for release truth without claiming browser runtime parity."
            ),
            "runtime_proven_receipts": summarize_items([pwa]),
            "source_staged_receipts": summarize_items([touch_mobile, campaign_session, table_handoff]),
            "documentation_sources": [],
            "unproven_claims": [
                "campaign persistence",
                "GM approval",
                "reward mutation",
                "table share",
                "portal help runtime parity",
                "mobile browser execution parity",
            ],
            "server_bound_boundaries": [
                "runner data",
                "workspace data",
                "API traffic",
                "Black Ledger state",
                "heat state",
                "session state",
            ],
        },
        {
            "id": "long_term_living_world_expansion",
            "title": "Long-term living-world expansion",
            "status": "staged" if long_term_sources_ready else "not_ready",
            "evidence_tier": "source_staged_and_docs_only",
            "headline": "Living-world expansion is defined and source-staged, but not runtime-proven on the public edge.",
            "summary": (
                "Table Pulse, Runner Passport, Black Ledger, progression/workflow ledgers, and live heat continuity "
                "exist as product/docs posture and staged browser lanes without claiming live public-edge execution."
            ),
            "runtime_proven_receipts": [],
            "source_staged_receipts": summarize_items([workflow_ledger, progression_ledger, table_handoff]),
            "documentation_sources": [
                {
                    "id": showcase_doc["id"],
                    "label": showcase_doc["label"],
                    "path": showcase_doc["path"],
                    "status": showcase_doc["status"],
                },
                {
                    "id": minigames_doc["id"],
                    "label": minigames_doc["label"],
                    "path": minigames_doc["path"],
                    "status": minigames_doc["status"],
                },
            ],
            "unproven_claims": [
                "live Black Ledger mutation",
                "heat propagation runtime",
                "Runner Passport continuity runtime",
                "living-world inbox or newsroom runtime",
                "public-edge living-world execution parity",
            ],
        },
    ]

    receipt = {
        "contract_name": CONTRACT_NAME,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "scope": "public_release_shelf_play_surface_horizons",
        "public_download_relative_root": PUBLIC_DOWNLOAD_RELATIVE_ROOT,
        "current_release_truth": {
            "browser_lane_status": browser_lane["status"],
            "execution_horizon_status": execution_horizon["status"],
            "pwa_public_edge_status": pwa["status"],
            "current_execution_scope": str(current_published_execution.get("playwright_scope") or "").strip() or "unknown",
            "smoke_execution_scope": str(current_published_execution.get("playwright_scope") or "").strip() or "unknown",
            "public_base_url": str(current_published_execution.get("base_url") or "").strip(),
            "public_entry_route": PUBLIC_ENTRY_ROUTE,
            "public_roster_entry_route": PUBLIC_ROSTER_ENTRY_ROUTE,
            "public_blazor_root_route": PUBLIC_BLAZOR_ROOT_ROUTE,
            "hosted_app_route": HOSTED_APP_ROUTE,
            "compatibility_route_base": COMPATIBILITY_ROUTE_BASE,
            "execution_route_base": str(current_published_execution.get("promoted_route_base") or "").strip(),
            "promoted_route_base": str(current_published_execution.get("promoted_route_base") or "").strip(),
        },
        "horizons": horizons,
        "supporting_receipts": summarize_items(evaluated_receipts),
        "documentation_sources": [
            {
                "id": showcase_doc["id"],
                "label": showcase_doc["label"],
                "path": showcase_doc["path"],
                "status": showcase_doc["status"],
            },
            {
                "id": minigames_doc["id"],
                "label": minigames_doc["label"],
                "path": minigames_doc["path"],
                "status": minigames_doc["status"],
            },
        ],
        "failures": failures,
        "notes": [
            "This receipt is the public release-shelf horizon summary for the browser/PWA play surface.",
            "Near-term stabilization is runtime proof only; mid-term and long-term lanes stay explicitly separated by proof tier.",
            "Public route truth is split between /app as the clean entry, /blazor/app as the hosted app path, and /blazor/workbench as the proof-compatible compatibility lane.",
            "PWA installability must not be treated as offline runner, workspace, API, Black Ledger, heat, or session parity.",
            "Living-world expansion remains docs-plus-source-staged evidence until refreshed runtime receipts prove it on the public edge.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_play_surface_horizon:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
