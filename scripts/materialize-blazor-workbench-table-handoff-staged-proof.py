#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_table_handoff",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client table handoff posture",
            "data-workbench-table-handoff=\"strip\"",
            "Package the dossier for play.",
            "data-workbench-table-handoff-action=\"gm-packet\"",
            "data-workbench-table-handoff-action=\"initiative-card\"",
            "data-workbench-table-handoff-action=\"condition-tracker\"",
            "data-workbench-table-handoff-action=\"public-handout\"",
            "data-workbench-table-handoff-action=\"private-notes\"",
            "data-workbench-table-handoff-action=\"table-export\"",
            "private const string TableGmPacketCommand = \"table_gm_packet\"",
            "private const string TableInitiativeCardCommand = \"table_initiative_card\"",
            "private const string TableConditionTrackerCommand = \"table_condition_tracker\"",
            "private const string TablePublicHandoutCommand = \"table_public_handout\"",
            "private const string TablePrivateNotesCommand = \"table_private_notes\"",
            "private const string TableExportCommand = \"table_export\"",
            "command: TableGmPacketCommand",
            "command: TableInitiativeCardCommand",
            "command: TableConditionTrackerCommand",
            "command: TablePublicHandoutCommand",
            "command: TablePrivateNotesCommand",
            "command: TableExportCommand",
            "data-workbench-table-handoff-action=\"help\"",
            "href=\"@HelpHref\"",
        ],
    },
    {
        "id": "scoped_table_handoff_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-table-handoff-strip",
            ".browser-workbench-table-handoff-copy",
            ".browser-workbench-table-handoff-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench table-handoff posture",
            "blazor-workbench-table-handoff-staged-proof-check.sh",
            "not hosted or Docker browser execution receipts",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench table-handoff posture",
            "GM packet, initiative card, condition tracker, public handout, private notes, table export, and help",
            "not yet claiming packet generation, live GM sharing, condition synchronization, handout publication, table export persistence parity, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_TABLE_HANDOFF_STAGED_PROOF",
            "workbench_table_handoff_staged_status",
            "workbench_table_handoff_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_table_handoff_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route table-handoff source, style, status, and docs agree.",
            "It is not a substitute for hosted browser execution, packet generation, initiative-card rendering, condition export, handout filtering, private-note partitioning, table export parity, or portal help runtime proof.",
            "Do not use this receipt to claim complete table handoff, persistence, portal help runtime behavior, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_table_handoff_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
