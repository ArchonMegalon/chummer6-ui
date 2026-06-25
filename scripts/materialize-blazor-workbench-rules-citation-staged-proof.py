#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_RULES_CITATION_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_RULES_CITATION_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_rules_citation",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench rules citation posture",
            "data-workbench-rules-citation=\"strip\"",
            "Keep rule context near exports.",
            "data-workbench-rules-citation-action=\"source-packet\"",
            "data-workbench-rules-citation-action=\"citation-scope\"",
            "data-workbench-rules-citation-action=\"errata-note\"",
            "data-workbench-rules-citation-action=\"table-summary\"",
            "data-workbench-rules-citation-action=\"dispute-trail\"",
            "data-workbench-rules-citation-action=\"audit-export\"",
        ],
    },
    {
        "id": "scoped_rules_citation_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-rules-citation-strip",
            ".browser-workbench-rules-citation-copy",
            ".browser-workbench-rules-citation-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench rules-citation posture",
            "blazor-workbench-rules-citation-staged-proof-check.sh",
            "not hosted or Docker browser execution receipts",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench rules-citation posture",
            "source packet, citation scope, errata note, table summary, dispute trail, and audit export",
            "not yet claiming citation generation, source lookup, errata resolution, dispute persistence, or audit export generation parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_RULES_CITATION_STAGED_PROOF",
            "workbench_rules_citation_staged_status",
            "workbench_rules_citation_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8")


def evaluate_check(check: dict) -> dict:
    content = read_text(check["path"])
    missing_tokens = [token for token in check["tokens"] if token not in content]
    return {
        "id": check["id"],
        "path": check["path"],
        "status": "passed" if not missing_tokens else "failed",
        "required_token_count": len(check["tokens"]),
        "missing_tokens": missing_tokens,
    }


def main() -> int:
    evaluated_checks = [evaluate_check(check) for check in CHECKS]
    failures = [check for check in evaluated_checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_rules_citation_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that promoted workbench rules-citation source, style, status, and docs agree.",
            "It is not a substitute for hosted browser execution, citation generation, source lookup, errata resolution, dispute persistence, or audit export generation parity.",
            "Do not use this receipt to claim complete rules citation, persistence, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_rules_citation_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
