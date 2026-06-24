#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json",
    )
)

CONTROL_FAMILIES = {
    "career_support": [
        "create_entry",
        "edit_entry",
        "delete_entry",
        "open_notes",
        "move_up",
        "move_down",
    ],
    "identity_license": [
        "identity_license_add",
        "identity_license_edit",
        "identity_license_delete",
    ],
    "source_gear_utility": [
        "toggle_free_paid",
        "show_source",
        "gear_mount",
        "gear_source",
    ],
    "gear_maintenance": [
        "gear_add",
        "gear_edit",
        "gear_delete",
    ],
    "cyberware": [
        "cyberware_add",
        "cyberware_edit",
        "cyberware_delete",
    ],
    "drug": [
        "drug_add",
        "drug_delete",
    ],
    "magic_cleanup": [
        "magic_add",
        "magic_delete",
        "magic_bind",
        "magic_source",
    ],
    "magic_support": [
        "spell_add",
        "adept_power_add",
        "complex_form_add",
        "initiation_add",
        "spirit_add",
        "critter_power_add",
        "matrix_program_add",
    ],
    "skill_maintenance": [
        "skill_add",
        "skill_specialize",
        "skill_remove",
        "skill_group",
    ],
    "combat_support": [
        "combat_add_weapon",
        "combat_add_armor",
        "combat_reload",
        "combat_damage_track",
    ],
    "vehicle": [
        "vehicle_add",
        "vehicle_edit",
        "vehicle_delete",
        "vehicle_mod_add",
    ],
    "contacts": [
        "contact_add",
        "contact_edit",
        "contact_remove",
        "contact_connection",
    ],
    "qualities": [
        "quality_add",
        "quality_delete",
    ],
}

# These are already in the hosted execution-proof contract before the source-only slices added here.
HOSTED_EXECUTION_BASELINE = {
    "cyberware_add",
    "cyberware_edit",
    "cyberware_delete",
    "drug_add",
    "magic_delete",
    "spell_add",
    "complex_form_add",
    "initiation_add",
    "skill_add",
    "combat_add_weapon",
    "vehicle_add",
    "vehicle_edit",
    "vehicle_delete",
    "vehicle_mod_add",
    "contact_add",
    "contact_edit",
    "contact_remove",
    "contact_connection",
    "quality_add",
    "quality_delete",
}

SOURCE_STAGED_FAMILIES = {
    "career_support": "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json",
    "identity_license": "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json",
    "source_gear_utility": "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json",
    "gear_maintenance": "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json",
    "drug": "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json",
    "magic_cleanup": "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json",
    "magic_support": "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json",
    "skill_maintenance": "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json",
    "combat_support": "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json",
}

SOURCE_FILES = [
    "Chummer.Presentation/Overview/LegacyUiControlCatalog.cs",
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
    "Chummer.Blazor/Components/Pages/Preview.razor",
    "scripts/e2e-public-edge.cjs",
    "scripts/e2e-public-edge-playwright.cjs",
    "scripts/e2e-portal-playwright.cjs",
    "scripts/e2e-portal.sh",
    "scripts/print_blazor_public_edge_proof_status.py",
    "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
    "docs/MIGRATION_BACKLOG.md",
    "docs/WORKBENCH_RELEASE_SIGNOFF.md",
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def control_coverage_kind(family_id: str, control_id: str) -> str:
    if control_id in HOSTED_EXECUTION_BASELINE:
        return "hosted_execution_baseline_or_staged_refresh"
    if family_id in SOURCE_STAGED_FAMILIES:
        return "source_staged_no_browser_execution"
    return "unclassified"


def main() -> int:
    source_texts = {}
    failures: list[str] = []
    for relative_path in SOURCE_FILES:
        try:
            source_texts[relative_path] = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"{relative_path}: missing file")
            source_texts[relative_path] = ""

    catalog_text = source_texts["Chummer.Presentation/Overview/LegacyUiControlCatalog.cs"]
    joined_runner_text = "\n".join(
        source_texts[path]
        for path in [
            "Chummer.Blazor/Components/Pages/Preview.razor",
            "scripts/e2e-public-edge.cjs",
            "scripts/e2e-public-edge-playwright.cjs",
            "scripts/e2e-portal-playwright.cjs",
            "scripts/e2e-portal.sh",
        ]
    )
    docs_text = "\n".join(
        source_texts[path]
        for path in [
            "scripts/print_blazor_public_edge_proof_status.py",
            "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
            "docs/MIGRATION_BACKLOG.md",
            "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        ]
    )

    family_rows = []
    covered_count = 0
    control_count = 0
    unclassified_controls = []

    for family_id, controls in CONTROL_FAMILIES.items():
        rows = []
        for control_id in controls:
            control_count += 1
            coverage_kind = control_coverage_kind(family_id, control_id)
            in_catalog = control_id in catalog_text
            in_runner_sources = control_id in joined_runner_text
            in_docs_or_status = (
                control_id in docs_text
                or family_id in docs_text
                or SOURCE_STAGED_FAMILIES.get(family_id, "") in docs_text
            )
            covered = coverage_kind != "unclassified" and in_catalog and in_runner_sources
            if coverage_kind == "unclassified":
                unclassified_controls.append(control_id)
            if not in_catalog:
                failures.append(f"{control_id}: missing from LegacyUiControlCatalog")
            if not in_runner_sources:
                failures.append(f"{control_id}: missing from workbench/proof runner source staging")
            if family_id in SOURCE_STAGED_FAMILIES and not in_docs_or_status:
                failures.append(f"{family_id}: missing docs/status coverage for staged family")
            if covered:
                covered_count += 1
            rows.append(
                {
                    "control_id": control_id,
                    "coverage_kind": coverage_kind,
                    "catalog_token_present": in_catalog,
                    "runner_source_token_present": in_runner_sources,
                    "docs_or_status_token_present": in_docs_or_status,
                    "covered_by_source_alignment": covered,
                }
            )
        family_rows.append(
            {
                "family_id": family_id,
                "staged_receipt": SOURCE_STAGED_FAMILIES.get(family_id),
                "controls": rows,
                "control_count": len(rows),
                "covered_count": sum(1 for row in rows if row["covered_by_source_alignment"]),
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_legacy_control_coverage_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "control_count": control_count,
        "covered_control_count": covered_count,
        "unclassified_controls": unclassified_controls,
        "family_rows": family_rows,
        "failures": failures,
        "notes": [
            "This receipt is a source-level coverage guard for known legacy UI control IDs.",
            "It combines hosted execution baseline controls with source-staged route families.",
            "It is not browser execution evidence and must not be treated as a release-passing proof.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_legacy_control_coverage_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
