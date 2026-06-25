#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_dialog_stack",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench dialog stack",
            "data-workbench-dialog-stack=\"strip\"",
            "Keep modal work and committed results visible.",
            "data-workbench-dialog-stack-action=\"active-dialog\"",
            "data-workbench-dialog-stack-action=\"committed-result\"",
            "data-workbench-dialog-stack-action=\"retry\"",
            "data-workbench-dialog-stack-action=\"cancel-back\"",
            "data-workbench-dialog-stack-action=\"help\"",
            "/help",
            "data-workbench-dialog-stack-action=\"support\"",
        ],
    },
    {
        "id": "scoped_dialog_stack_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-dialog-stack",
            ".browser-workbench-dialog-stack-copy",
            ".browser-workbench-dialog-stack-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench dialog-stack posture",
            "blazor-workbench-dialog-stack-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench dialog-stack posture",
            "active dialog, committed result, retry, back-to-sheet, help, and support",
            "not yet claiming modal execution, portal help runtime, or committed-action runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_DIALOG_STACK_STAGED_PROOF",
            "workbench_dialog_stack_staged_status",
            "workbench_dialog_stack_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    checks = []
    failures = []

    for check in CHECKS:
        path = check["path"]
        tokens = check["tokens"]
        try:
            text = read_text(path)
        except FileNotFoundError:
            failures.append(f"{path}: missing file")
            checks.append({**check, "status": "failed", "missing_tokens": tokens})
            continue

        missing_tokens = [token for token in tokens if token not in text]
        status = "failed" if missing_tokens else "passed"
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": status,
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_dialog_stack_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help", "/contact"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and proof-compatible workbench dialog-stack source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, modal execution proof, portal help runtime, or committed-action runtime proof.",
            "Do not use this receipt to claim modal execution, portal help runtime, committed-action runtime behavior, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_dialog_stack_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
