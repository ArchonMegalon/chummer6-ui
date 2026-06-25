#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_gm_screen_export",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench GM screen export posture",
            "data-workbench-gm-screen-export=\"strip\"",
            "Build table panels from this runner.",
            "data-workbench-gm-screen-export-action=\"screen-cards\"",
            "data-workbench-gm-screen-export-action=\"player-safe-view\"",
            "data-workbench-gm-screen-export-action=\"initiative-panel\"",
            "data-workbench-gm-screen-export-action=\"condition-rail\"",
            "data-workbench-gm-screen-export-action=\"scene-notes\"",
            "data-workbench-gm-screen-export-action=\"export-bundle\"",
            "data-workbench-gm-screen-export-action=\"help\"",
            "href=\"/help\"",
        ],
    },
    {
        "id": "scoped_gm_screen_export_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-gm-screen-export-strip",
            ".browser-workbench-gm-screen-export-copy",
            ".browser-workbench-gm-screen-export-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench GM-screen export posture",
            "blazor-workbench-gm-screen-export-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench GM-screen export posture",
            "Cards, player view, initiative, conditions, notes, export bundle, and help",
            "not yet claiming GM-screen rendering, player-view routing, initiative sync, export-bundle parity, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF",
            "workbench_gm_screen_export_staged_status",
            "workbench_gm_screen_export_staged_source_checks",
            "source_alignment_only_not_gm_screen_or_export_execution_proof",
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
        "contract_name": "chummer6-ui.blazor_workbench_gm_screen_export_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and proof-compatible workbench GM-screen export source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, GM-screen rendering proof, player-view routing proof, initiative sync proof, export-bundle proof, or portal help runtime proof.",
            "Do not use this receipt to claim GM-screen rendering, player-view routing, initiative sync, export-bundle behavior, portal help runtime behavior, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_gm_screen_export_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
