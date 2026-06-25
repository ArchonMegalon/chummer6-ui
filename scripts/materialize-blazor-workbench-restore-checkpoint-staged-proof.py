#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_restore_checkpoint",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench restore and checkpoint posture",
            "data-workbench-restore-checkpoint=\"strip\"",
            "Keep recovery close to risky edits.",
            "data-workbench-restore-checkpoint-action=\"autosave\"",
            "data-workbench-restore-checkpoint-action=\"named-checkpoint\"",
            "data-workbench-restore-checkpoint-action=\"backup-download\"",
            "data-workbench-restore-checkpoint-action=\"restore-preview\"",
            "data-workbench-restore-checkpoint-action=\"rollback\"",
            "data-workbench-restore-checkpoint-action=\"retention\"",
            "data-workbench-restore-checkpoint-action=\"help\"",
            "href=\"/help\"",
        ],
    },
    {
        "id": "scoped_restore_checkpoint_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-restore-checkpoint-strip",
            ".browser-workbench-restore-checkpoint-copy",
            ".browser-workbench-restore-checkpoint-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench restore/checkpoint posture",
            "blazor-workbench-restore-checkpoint-staged-proof-check.sh",
            "not hosted or Docker browser execution receipts",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench restore/checkpoint posture",
            "autosave, named checkpoint, backup, preview, rollback, retention, and help",
            "not yet claiming snapshot persistence, restore execution, backup generation, rollback mutation, retention policy parity, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF",
            "workbench_restore_checkpoint_staged_status",
            "workbench_restore_checkpoint_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_restore_checkpoint_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and proof-compatible workbench restore/checkpoint source, style, status, and docs agree.",
            "It is not a substitute for hosted browser execution, snapshot persistence, restore execution, backup generation, rollback mutation, retention policy parity, or portal help runtime proof.",
            "Do not use this receipt to claim complete recovery, persistence, portal help runtime behavior, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_restore_checkpoint_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
