#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
TMP = REPO_ROOT / ".codex-studio" / "tmp"
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON_PATH",
        PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json",
    )
)
EXECUTION_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH",
        PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json",
    )
)
FAILED_EXECUTION_PROOF_PATH = Path(
    os.environ.get(
        "CHUMMER_PUBLIC_EDGE_FAILED_EXECUTION_PROOF_PATH",
        TMP / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.failed.generated.json",
    )
)
VERIFIER_PATH = REPO_ROOT / "scripts" / "verify_blazor_public_edge_execution_proof.py"
CONTRACT_NAME = "chummer6-ui.blazor_public_edge_execution_horizon"


def load_json(path: Path) -> tuple[dict[str, Any], str | None]:
    if not path.is_file():
        return {}, f"missing JSON artifact: {path}"
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        return {}, f"invalid JSON in {path}: {exc}"
    if not isinstance(loaded, dict):
        return {}, f"JSON root must be an object: {path}"
    return loaded, None


def load_verifier_module() -> Any:
    spec = importlib.util.spec_from_file_location("blazor_public_edge_execution_verifier", VERIFIER_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load verifier module: {VERIFIER_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def workflow_family_ids(payload: dict[str, Any]) -> set[str]:
    families = payload.get("workflow_families") or []
    if not isinstance(families, list):
        return set()
    return {
        str(item.get("id") or "").strip()
        for item in families
        if isinstance(item, dict) and str(item.get("id") or "").strip()
    }


def summarize_failed_sidecar(path: Path) -> dict[str, Any]:
    payload, error = load_json(path)
    if error is not None:
        return {
            "path": str(path),
            "present": False,
            "status": "not_present",
            "error": "",
            "workflow_family_count": 0,
        }

    return {
        "path": str(path),
        "present": True,
        "status": str(payload.get("status") or "unknown").strip() or "unknown",
        "playwright_scope": str(payload.get("playwright_scope") or "").strip() or "unknown",
        "error": str(payload.get("error") or "").strip(),
        "workflow_family_count": len(workflow_family_ids(payload)),
    }


def main() -> int:
    verifier = load_verifier_module()
    smoke_required = set(verifier.SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS)
    full_required = set(verifier.AVAILABLE_WORKFLOW_FAMILY_IDS)
    pass_statuses = {"pass", "passed", "ready"}

    proof, proof_error = load_json(EXECUTION_PROOF_PATH)
    failures: list[str] = []
    if proof_error is not None:
        failures.append(proof_error)
        proof = {}

    current_status = str(proof.get("status") or "").strip().lower()
    current_scope = str(proof.get("playwright_scope") or "").strip().lower()
    current_ids = workflow_family_ids(proof)
    smoke_missing = sorted(smoke_required - current_ids)
    full_missing = sorted(full_required - current_ids)
    smoke_proven = current_status in pass_statuses and current_scope in {"smoke", "full"} and not smoke_missing
    full_proven = current_status in pass_statuses and current_scope == "full" and not full_missing

    if not smoke_proven:
        failures.append("published hosted execution proof does not cover every smoke-required workflow family")
    if current_status in pass_statuses and current_scope == "full" and not full_proven:
        failures.append("published full-scope hosted execution proof is missing full-required workflow families")

    payload = {
        "contract_name": CONTRACT_NAME,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "execution_proof_path": str(EXECUTION_PROOF_PATH),
        "failed_execution_sidecar_path": str(FAILED_EXECUTION_PROOF_PATH),
        "current_published_execution": {
            "status": current_status or "missing",
            "playwright_scope": current_scope or "missing",
            "workflow_family_count": len(current_ids),
            "required_workflow_family_count": len(proof.get("required_workflow_family_ids") or []),
            "base_url": str(proof.get("base_url") or "").strip(),
            "promoted_route_base": str(proof.get("promoted_route_base") or "").strip(),
        },
        "horizons": [
            {
                "id": "near_term_hosted_smoke_execution",
                "status": "proven" if smoke_proven else "not_proven",
                "required_workflow_family_count": len(smoke_required),
                "covered_workflow_family_count": len(smoke_required - set(smoke_missing)),
                "missing_workflow_family_ids": smoke_missing,
            },
            {
                "id": "mid_term_full_live_public_edge_execution_matrix",
                "status": "proven" if full_proven else "not_proven",
                "required_workflow_family_count": len(full_required),
                "covered_workflow_family_count": len(full_required - set(full_missing)),
                "missing_workflow_family_ids": full_missing,
                "promotion_rule": "requires a passing full-scope BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json",
            },
            {
                "id": "long_term_full_browser_desktop_parity_breadth",
                "status": "not_claimed" if not full_proven else "requires_separate_parity_gates",
                "promotion_rule": "hosted execution proof alone is not full desktop parity; keep UI parity gates distinct",
            },
        ],
        "failed_execution_sidecar": summarize_failed_sidecar(FAILED_EXECUTION_PROOF_PATH),
        "boundary": {
            "does_not_upgrade_smoke_to_full": True,
            "does_not_treat_failed_sidecar_as_published_proof": True,
            "full_scope_requires_current_passing_full_receipt": True,
        },
        "failures": failures,
        "notes": [
            "This receipt makes the hosted execution horizon explicit; it is not a browser execution proof by itself.",
            "Smoke execution can be release evidence only for the smoke-required workflow families.",
            "The full live public-edge matrix remains unproven until the published hosted execution receipt is current, passing, full-scope, and covers every available workflow family id.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if failures:
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 1

    print(f"blazor_public_edge_execution_horizon:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
