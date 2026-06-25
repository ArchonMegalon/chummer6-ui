#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_performance_virtualization",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench performance and virtualization lane",
            "data-workbench-performance-virtualization=\"strip\"",
            "Keep large sheets responsive.",
            "data-workbench-performance-virtualization-action=\"lazy_sections\"",
            "data-workbench-performance-virtualization-action=\"virtual_lists\"",
            "data-workbench-performance-virtualization-action=\"render_budget\"",
            "data-workbench-performance-virtualization-action=\"memory_posture\"",
            "data-workbench-performance-virtualization-action=\"degraded_mode\"",
            "data-workbench-performance-virtualization-action=\"profiler\"",
            "data-workbench-performance-virtualization-action=\"help\"",
            "/help",
        ],
    },
    {
        "id": "scoped_performance_virtualization_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-performance-virtualization",
            ".browser-workbench-performance-virtualization-copy",
            ".browser-workbench-performance-virtualization-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench performance-virtualization posture",
            "blazor-workbench-performance-virtualization-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench performance-virtualization posture",
            "lazy sections, virtual lists, render budget, memory posture, degraded mode, profiler, and help",
            "not yet claiming virtualized rendering, lazy loading, memory control, profiler, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF",
            "workbench_performance_virtualization_staged_status",
            "workbench_performance_virtualization_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_performance_virtualization_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and proof-compatible workbench performance-virtualization source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, virtualized-rendering proof, lazy-loading proof, memory-control proof, profiler proof, or portal help runtime proof.",
            "Do not use this receipt to claim virtualized rendering, lazy loading, memory control, profiler behavior, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_performance_virtualization_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
