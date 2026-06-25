#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_validation_queue",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client validation and build readiness",
            "data-workbench-validation-queue=\"strip\"",
            "Keep validation pressure visible before output.",
            "data-workbench-validation-queue-action=\"rule_issues\"",
            "data-workbench-validation-queue-action=\"missing_fields\"",
            "data-workbench-validation-queue-action=\"cost_checks\"",
            "data-workbench-validation-queue-action=\"availability\"",
            "data-workbench-validation-queue-action=\"build_gate\"",
            "private const string ValidateCharacterCommand = \"validate_character\"",
            "private const string FinalizeCharacterCommand = \"finalize_character\"",
            "private const string ResolveNextIssueCommand = \"resolve_next_issue\"",
            "command: ValidateCharacterCommand",
            "command: FinalizeCharacterCommand",
            "command: ResolveNextIssueCommand",
            "data-workbench-validation-queue-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
            "data-workbench-validation-queue-action=\"fix_next\"",
        ],
    },
    {
        "id": "scoped_validation_queue_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-validation-queue",
            ".browser-workbench-validation-queue-copy",
            ".browser-workbench-validation-queue-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench validation-queue posture",
            "blazor-workbench-validation-queue-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench validation-queue posture",
            "rule issues, missing fields, cost checks, availability limits, build gate, help, and fix-next navigation",
            "not yet claiming rules-engine execution, portal help runtime, or validation-result parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF",
            "workbench_validation_queue_staged_status",
            "workbench_validation_queue_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_validation_queue_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route validation-queue source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, rules-engine execution proof, portal help runtime, or validation-result proof.",
            "Do not use this receipt to claim rules-engine execution, portal help runtime, validation-result, build-finalization, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_validation_queue_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
