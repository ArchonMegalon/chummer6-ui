#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_navigation_deeplink",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench navigation and deep-link lane",
            "data-workbench-navigation-deeplink=\"strip\"",
            "Make links reopen the same place.",
            "data-workbench-navigation-deeplink-action=\"breadcrumbs\"",
            "data-workbench-navigation-deeplink-action=\"url_state\"",
            "data-workbench-navigation-deeplink-action=\"history_nav\"",
            "data-workbench-navigation-deeplink-action=\"copy_route\"",
            "data-workbench-navigation-deeplink-action=\"tab_restore\"",
            "data-workbench-navigation-deeplink-action=\"shared_anchor\"",
        ],
    },
    {
        "id": "scoped_navigation_deeplink_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-navigation-deeplink",
            ".browser-workbench-navigation-deeplink-copy",
            ".browser-workbench-navigation-deeplink-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench navigation-deeplink posture",
            "blazor-workbench-navigation-deeplink-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench navigation-deeplink posture",
            "breadcrumbs, URL state, back/forward, copy route, tab restore, and shared anchor",
            "not yet claiming router-state, browser-history, route-copy, or deep-link restore parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF",
            "workbench_navigation_deeplink_staged_status",
            "workbench_navigation_deeplink_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_navigation_deeplink_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that promoted workbench navigation-deeplink source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, router-state proof, browser-history proof, route-copy proof, or deep-link restore proof.",
            "Do not use this receipt to claim router state, browser history, route copy, deep-link restore, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_navigation_deeplink_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
