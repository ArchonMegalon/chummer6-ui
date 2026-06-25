#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKSPACE_ROOT = REPO_ROOT.parent
DESIGN_ROOT = Path(
    os.environ.get(
        "CHUMMER_DESIGN_PRODUCT_ROOT",
        WORKSPACE_ROOT / "chummer-design" / "products" / "chummer",
    )
)
PUBLISHED_ROOT = REPO_ROOT / ".codex-studio" / "published"
MATRIX_PATH = DESIGN_ROOT / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.yaml"
UI_AUDIT_PATH = PUBLISHED_ROOT / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
SCREENSHOT_GATE_PATH = PUBLISHED_ROOT / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json"

FAMILY_ROW_MAP = {
    "translator_xml_bridge": "family:custom_data_xml_and_translator_bridge",
    "dense_builder_and_career": "family:dense_builder_and_career_workflows",
    "dice_initiative_and_table_utilities": "family:dice_initiative_and_table_utilities",
    "identity_contacts_lifestyles_history": "family:identity_contacts_lifestyles_history",
    "legacy_and_adjacent_import_oracles": "family:legacy_and_adjacent_import_oracles",
    "sheet_export_print_viewer_exchange": "family:sheet_export_print_viewer_and_exchange",
    "sr6_supplements_designers_house_rules": "family:sr6_supplements_designers_and_house_rules",
}

REQUIRED_SCREENSHOT_JOBS = (
    "dense_builder",
    "master_index",
    "roster",
    "settings",
    "translator",
    "xml_editor",
    "hero_lab_importer",
)


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def rel(path: Path) -> str:
    resolved = path.resolve()
    for root in (REPO_ROOT, WORKSPACE_ROOT):
        try:
            return str(resolved.relative_to(root))
        except ValueError:
            continue
    return str(path)


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise ValueError(f"Expected JSON object in {path}")
    return payload


def load_yaml(path: Path) -> Any:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def main() -> int:
    matrix_payload = load_yaml(MATRIX_PATH)
    ui_audit = load_json(UI_AUDIT_PATH)
    screenshot_gate = load_json(SCREENSHOT_GATE_PATH)

    families = matrix_payload.get("families") or []
    if not isinstance(families, list):
        families = []
    matrix_family_ids = sorted(
        {
            str(family.get("id", "")).strip()
            for family in families
            if isinstance(family, dict) and str(family.get("id", "")).strip()
        }
    )
    derived_row_count = 0
    derived_surface_count = 0
    for family in families:
        if not isinstance(family, dict):
            continue
        surfaces = family.get("surfaces") or []
        if not isinstance(surfaces, list):
            continue
        for surface in surfaces:
            if not isinstance(surface, dict):
                continue
            derived_surface_count += 1
            elements = surface.get("must_remain_first_class") or []
            if isinstance(elements, list):
                derived_row_count += len(elements)

    ui_rows = ui_audit.get("rows") or []
    ui_row_by_id = {
        str(row.get("id")).strip(): row
        for row in ui_rows
        if isinstance(row, dict) and str(row.get("id")).strip()
    }
    screenshot_jobs = screenshot_gate.get("reviewJobs") or {}
    if not isinstance(screenshot_jobs, dict):
        screenshot_jobs = {}

    failures: list[str] = []
    family_results: list[dict[str, Any]] = []
    for matrix_family_id in matrix_family_ids:
        audit_row_id = FAMILY_ROW_MAP.get(matrix_family_id)
        audit_row = ui_row_by_id.get(audit_row_id or "")
        if audit_row is None:
            failures.append(f"Missing UI parity family row for matrix family '{matrix_family_id}'.")
            family_results.append(
                {
                    "matrix_family_id": matrix_family_id,
                    "audit_row_id": audit_row_id,
                    "status": "missing",
                }
            )
            continue

        visual = str(audit_row.get("visual_parity", "")).strip().lower()
        behavioral = str(audit_row.get("behavioral_parity", "")).strip().lower()
        status = "pass" if visual == "yes" and behavioral == "yes" else "fail"
        if status != "pass":
            failures.append(
                f"UI parity family row '{audit_row_id}' is visual={visual!r} behavioral={behavioral!r}."
            )

        family_results.append(
            {
                "matrix_family_id": matrix_family_id,
                "audit_row_id": audit_row_id,
                "visual_parity": visual,
                "behavioral_parity": behavioral,
                "status": status,
            }
        )

    screenshot_job_results: list[dict[str, Any]] = []
    for job_id in REQUIRED_SCREENSHOT_JOBS:
        job = screenshot_jobs.get(job_id)
        if not isinstance(job, dict):
            failures.append(f"Missing screenshot review job '{job_id}'.")
            screenshot_job_results.append({"job_id": job_id, "status": "missing"})
            continue

        status = str(job.get("status", "")).strip().lower()
        if status != "pass":
            failures.append(f"Screenshot review job '{job_id}' is not passing.")
        screenshot_job_results.append(
            {
                "job_id": job_id,
                "status": status,
                "screenshots": job.get("screenshots") or [],
                "test_markers": job.get("testMarkers") or [],
            }
        )

    ui_summary = ui_audit.get("summary") or {}
    if int(ui_summary.get("visual_no_count", 0)) != 0:
        failures.append(f"UI audit visual_no_count must be 0, got {ui_summary.get('visual_no_count')}.")
    if int(ui_summary.get("behavioral_no_count", 0)) != 0:
        failures.append(f"UI audit behavioral_no_count must be 0, got {ui_summary.get('behavioral_no_count')}.")
    if int(ui_summary.get("removable_extra_present_count", 0)) != 0:
        failures.append(
            f"UI audit removable_extra_present_count must be 0, got {ui_summary.get('removable_extra_present_count')}."
        )
    if not matrix_family_ids:
        failures.append("The human parity matrix did not expose any families.")
    if derived_surface_count == 0:
        failures.append("The human parity matrix did not expose any surfaces.")
    if derived_row_count == 0:
        failures.append("The human parity matrix did not expose any first-class controls.")

    status = "pass" if not failures else "fail"
    payload = {
        "contract_name": "chummer6-ui.chummer5a_human_parity_matrix_proof",
        "status": status,
        "generated_at": now_iso(),
        "summary": (
            "The Chummer5A human parity matrix is fully covered by passing family-level UI parity rows and mandatory screenshot-backed review jobs."
            if status == "pass"
            else "The Chummer5A human parity matrix still has missing or non-passing family/screenshot evidence."
        ),
        "matrix": {
            "path": rel(MATRIX_PATH),
            "row_count": derived_row_count,
            "surface_count": derived_surface_count,
            "family_count": len(matrix_family_ids),
            "family_ids": matrix_family_ids,
        },
        "ui_audit_summary": {
            "path": rel(UI_AUDIT_PATH),
            "total_elements": ui_summary.get("total_elements"),
            "visual_yes_count": ui_summary.get("visual_yes_count"),
            "behavioral_yes_count": ui_summary.get("behavioral_yes_count"),
            "visual_no_count": ui_summary.get("visual_no_count"),
            "behavioral_no_count": ui_summary.get("behavioral_no_count"),
        },
        "family_results": family_results,
        "screenshot_review": {
            "path": rel(SCREENSHOT_GATE_PATH),
            "required_jobs": list(REQUIRED_SCREENSHOT_JOBS),
            "results": screenshot_job_results,
        },
        "strict_failure_reasons": failures,
        "evidence_sources": [
            rel(MATRIX_PATH),
            rel(UI_AUDIT_PATH),
            rel(SCREENSHOT_GATE_PATH),
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0 if status == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
