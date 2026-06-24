#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.generated.json",
    )
)

PROMOTION_ROWS = [
    {
        "id": "career_support",
        "source_receipt": "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json",
        "hosted_family_ids": [
            "promoted_career_log_continuity",
            "promoted_career_entry_execution",
            "promoted_career_entry_committed_execution",
            "promoted_career_entry_edit_execution",
            "promoted_career_entry_delete_execution",
            "promoted_career_entry_edit_committed_execution",
            "promoted_career_entry_delete_committed_execution",
            "promoted_runner_notes_execution",
            "promoted_runner_notes_committed_execution",
            "promoted_career_entry_reorder_execution",
        ],
    },
    {
        "id": "identity_license",
        "source_receipt": "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_identity_license_execution"],
    },
    {
        "id": "combat_support",
        "source_receipt": "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_combat_support_execution"],
    },
    {
        "id": "skill_maintenance",
        "source_receipt": "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_skill_maintenance_execution"],
    },
    {
        "id": "magic_support",
        "source_receipt": "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_magic_support_execution"],
    },
    {
        "id": "gear_maintenance",
        "source_receipt": "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_gear_maintenance_execution"],
    },
    {
        "id": "source_gear_utility",
        "source_receipt": "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_source_gear_utility_execution"],
    },
    {
        "id": "magic_cleanup",
        "source_receipt": "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_magic_cleanup_utility_execution"],
    },
]

REQUIRED_RUNTIME_RECEIPTS = [
    "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
    "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json",
    "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json",
]

CHECK_SOURCES = {
    "hosted_execution_runner": "scripts/e2e-public-edge-playwright.cjs",
    "self_host_runner": "scripts/e2e-portal-playwright.cjs",
    "self_host_metadata": "scripts/e2e-portal.sh",
    "docs": "docs/BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.md",
}


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    failures: list[str] = []
    source_texts = {}
    for label, relative_path in CHECK_SOURCES.items():
        try:
            source_texts[label] = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"missing source: {relative_path}")
            source_texts[label] = ""

    rows = []
    for row in PROMOTION_ROWS:
        hosted_missing = [family for family in row["hosted_family_ids"] if family not in source_texts["hosted_execution_runner"]]
        source_receipt_missing_from_docs = row["source_receipt"] not in source_texts["docs"]
        family_missing_from_docs = row["id"] not in source_texts["docs"]
        if hosted_missing:
            failures.append(f"{row['id']}: hosted runner missing {', '.join(hosted_missing)}")
        if source_receipt_missing_from_docs:
            failures.append(f"{row['id']}: docs missing {row['source_receipt']}")
        if family_missing_from_docs:
            failures.append(f"{row['id']}: docs missing family id")
        rows.append(
            {
                **row,
                "required_runtime_receipts": REQUIRED_RUNTIME_RECEIPTS,
                "hosted_runner_family_ids_present": not hosted_missing,
                "hosted_missing_family_ids": hosted_missing,
                "source_receipt_present_in_docs": not source_receipt_missing_from_docs,
                "family_id_present_in_docs": not family_missing_from_docs,
            }
        )

    docs_text = source_texts["docs"]
    for receipt in REQUIRED_RUNTIME_RECEIPTS:
        if receipt not in docs_text:
            failures.append(f"docs missing required runtime receipt {receipt}")

    payload = {
        "contract_name": "chummer6-ui.blazor_staged_to_runtime_promotion_matrix",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_plan_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "promotion_family_count": len(PROMOTION_ROWS),
        "required_runtime_receipts": REQUIRED_RUNTIME_RECEIPTS,
        "promotion_rows": rows,
        "failures": failures,
        "notes": [
            "This matrix is source-level planning only.",
            "It does not execute Docker self-host proof or hosted browser execution proof.",
            "A family is not promoted until the runtime receipts are refreshed and passing.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_staged_to_runtime_promotion_matrix:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
