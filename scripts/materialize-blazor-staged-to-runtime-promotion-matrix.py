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
    {
        "id": "browser_output_handoff",
        "source_receipt": "BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json",
        "hosted_family_ids": ["promoted_result_continuations"],
    },
]

PLANNED_PROMOTION_ROWS = [
    {
        "id": "runner_intelligence",
        "source_receipts": [
            "BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json",
            "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
        ],
        "planned_hosted_family_ids": [
            "promoted_runner_benchmark_execution",
            "promoted_runner_what_if_execution",
            "promoted_runner_cohort_privacy_execution",
        ],
        "required_runtime_work": [
            "hosted public-edge execution for runner_benchmark, runner_what_if, and runner_cohort_privacy",
            "Docker self-host execution with local-only cohort mode",
            "authoritative rules-engine calculation fixtures for spell/drug/gear what-if results",
            "hosted cohort aggregation opt-in proof before any hosted percentile cohort claim",
        ],
        "promotion_blockers": [
            "source-calculation proof is not authoritative SR rules-engine validation",
            "source-staged route metadata is not browser execution",
            "hosted cohort aggregation and Docker local benchmark persistence are not proven",
        ],
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
    "docs_index": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
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

    planned_rows = []
    for row in PLANNED_PROMOTION_ROWS:
        missing_source_receipts = [receipt for receipt in row["source_receipts"] if receipt not in source_texts["docs"]]
        family_missing_from_docs = row["id"] not in source_texts["docs"]
        missing_planned_families = [
            family for family in row["planned_hosted_family_ids"] if family not in source_texts["docs"]
        ]
        missing_runtime_work = [
            item for item in row["required_runtime_work"] if item not in source_texts["docs"]
        ]
        missing_blockers = [
            item for item in row["promotion_blockers"] if item not in source_texts["docs"]
        ]
        if missing_source_receipts:
            failures.append(f"{row['id']}: docs missing planned source receipts {', '.join(missing_source_receipts)}")
        if family_missing_from_docs:
            failures.append(f"{row['id']}: docs missing planned family id")
        if missing_planned_families:
            failures.append(f"{row['id']}: docs missing planned hosted family ids {', '.join(missing_planned_families)}")
        if missing_runtime_work:
            failures.append(f"{row['id']}: docs missing required runtime work {', '.join(missing_runtime_work)}")
        if missing_blockers:
            failures.append(f"{row['id']}: docs missing promotion blockers {', '.join(missing_blockers)}")
        planned_rows.append(
            {
                **row,
                "required_runtime_receipts": REQUIRED_RUNTIME_RECEIPTS,
                "source_receipts_present_in_docs": not missing_source_receipts,
                "family_id_present_in_docs": not family_missing_from_docs,
                "planned_hosted_family_ids_present_in_docs": not missing_planned_families,
                "required_runtime_work_present_in_docs": not missing_runtime_work,
                "promotion_blockers_present_in_docs": not missing_blockers,
                "hosted_runner_family_ids_present": False,
                "promotion_state": "planned_not_runtime_promoted",
            }
        )

    docs_text = source_texts["docs"]
    for receipt in REQUIRED_RUNTIME_RECEIPTS:
        if receipt not in docs_text:
            failures.append(f"docs missing required runtime receipt {receipt}")

    non_promoting_tokens = [
        "BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json",
        "portal-boundary guard",
        "portal_installer_handoff_staged_*",
        "not a Chummer Online and /blazor/workbench compatibility route workflow family",
        "cannot promote installer availability without refreshed portal runtime evidence",
    ]
    docs_index_tokens = [
        "scripts/materialize-blazor-staged-to-runtime-promotion-matrix.py",
        "keeps non-promoting portal-boundary guards out of workbench workflow promotion",
    ]
    missing_non_promoting_tokens = [token for token in non_promoting_tokens if token not in docs_text]
    if missing_non_promoting_tokens:
        failures.append(
            "docs missing non-promoting portal installer boundary tokens "
            + ", ".join(missing_non_promoting_tokens)
        )
    missing_docs_index_tokens = [token for token in docs_index_tokens if token not in source_texts["docs_index"]]
    if missing_docs_index_tokens:
        failures.append(
            "docs index missing promotion matrix materializer tokens "
            + ", ".join(missing_docs_index_tokens)
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_staged_to_runtime_promotion_matrix",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_plan_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "promotion_family_count": len(PROMOTION_ROWS),
        "planned_promotion_family_count": len(PLANNED_PROMOTION_ROWS),
        "required_runtime_receipts": REQUIRED_RUNTIME_RECEIPTS,
        "non_promoting_boundary_tokens_present": not missing_non_promoting_tokens,
        "non_promoting_boundary_receipts": [
            "BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json",
        ],
        "promotion_rows": rows,
        "planned_promotion_rows": planned_rows,
        "failures": failures,
        "notes": [
            "This matrix is source-level planning only.",
            "It does not execute Docker self-host proof or hosted browser execution proof.",
            "A family is not promoted until the runtime receipts are refreshed and passing.",
            "Planned promotion rows name runtime work that is intentionally not added to required hosted execution families yet.",
            "Portal installer handoff is a non-promoting portal-boundary guard; it does not promote installer availability without refreshed portal runtime evidence.",
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
