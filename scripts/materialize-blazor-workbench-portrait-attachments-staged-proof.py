#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_portrait_attachments",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench portrait and attachments lane",
            "data-workbench-portrait-attachments=\"strip\"",
            "Keep character media and notes attached to the sheet.",
            "data-workbench-portrait-attachments-action=\"portrait\"",
            "data-workbench-portrait-attachments-action=\"token_art\"",
            "data-workbench-portrait-attachments-action=\"notes\"",
            "data-workbench-portrait-attachments-action=\"attachments\"",
            "data-workbench-portrait-attachments-action=\"import_media\"",
            "data-workbench-portrait-attachments-action=\"cleanup\"",
        ],
    },
    {
        "id": "scoped_portrait_attachments_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-portrait-attachments",
            ".browser-workbench-portrait-attachments-copy",
            ".browser-workbench-portrait-attachments-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench portrait-attachments posture",
            "blazor-workbench-portrait-attachments-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench portrait-attachments posture",
            "portrait, token art, notes, attachments, import media, and cleanup",
            "not yet claiming file upload, storage persistence, thumbnail generation, or attachment cleanup parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF",
            "workbench_portrait_attachments_staged_status",
            "workbench_portrait_attachments_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_portrait_attachments_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that promoted workbench portrait-attachments source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, file-upload proof, storage-persistence proof, thumbnail-generation proof, or attachment-cleanup proof.",
            "Do not use this receipt to claim file upload, storage persistence, thumbnail generation, attachment cleanup, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_portrait_attachments_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
