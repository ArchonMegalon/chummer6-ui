#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_help_recovery_guidance",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench help and recovery guidance posture",
            "data-workbench-help-recovery-guidance=\"strip\"",
            "Explain the next useful move.",
            "data-workbench-help-recovery-guidance-action=\"context-help\"",
            "data-workbench-help-recovery-guidance-action=\"shortcut-hints\"",
            "data-workbench-help-recovery-guidance-action=\"error-explain\"",
            "data-workbench-help-recovery-guidance-action=\"recovery-suggest\"",
            "data-workbench-help-recovery-guidance-action=\"portal-help\"",
            "/help",
            "data-workbench-help-recovery-guidance-action=\"docs-link\"",
            "data-workbench-help-recovery-guidance-action=\"support-handoff\"",
        ],
    },
    {
        "id": "scoped_help_recovery_guidance_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-help-recovery-guidance-strip",
            ".browser-workbench-help-recovery-guidance-copy",
            ".browser-workbench-help-recovery-guidance-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench help/recovery guidance posture",
            "blazor-workbench-help-recovery-guidance-staged-proof-check.sh",
            "not hosted or Docker browser execution receipts",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench help/recovery guidance posture",
            "context help, shortcut hints, error explanations, recovery suggestions, same-origin portal help, docs links, and support handoff affordances",
            "not yet claiming contextual help resolution, keyboard shortcut execution, portal help runtime, support-ticket creation, docs search, or error remediation parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF",
            "workbench_help_recovery_guidance_staged_status",
            "workbench_help_recovery_guidance_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_help_recovery_guidance_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and proof-compatible workbench help and recovery source, style, status, and docs agree, including the same-origin /help handoff action.",
            "It is not a substitute for hosted browser execution, contextual help resolution, keyboard shortcut execution, portal help runtime, support-ticket creation, docs search, or error remediation parity.",
            "Do not use this receipt to claim complete help, support, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_help_recovery_guidance_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
