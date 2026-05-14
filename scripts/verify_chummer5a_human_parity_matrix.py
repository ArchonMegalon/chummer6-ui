#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
PROOF = PUBLISHED / "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json"
REQUIRED_TOP_LEVEL_FIELDS = (
    "contract_name",
    "status",
    "generated_at",
    "summary",
    "matrix",
    "ui_audit_summary",
    "family_results",
    "screenshot_review",
    "strict_failure_reasons",
    "evidence_sources",
)
REQUIRED_MATRIX_FIELDS = (
    "path",
    "row_count",
    "surface_count",
    "family_count",
    "family_ids",
)
REQUIRED_UI_AUDIT_SUMMARY_FIELDS = (
    "path",
    "total_elements",
    "visual_yes_count",
    "behavioral_yes_count",
    "visual_no_count",
    "behavioral_no_count",
)
REQUIRED_SCREENSHOT_REVIEW_FIELDS = (
    "path",
    "required_jobs",
    "results",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-fields", action="store_true")
    parser.add_argument("--family")
    return parser.parse_args()


def require_mapping(payload: object, label: str) -> dict:
    if not isinstance(payload, dict):
        raise SystemExit(f"{label} is missing or not an object")
    return payload


def require_sequence(payload: object, label: str) -> list:
    if not isinstance(payload, list):
        raise SystemExit(f"{label} is missing or not a list")
    return payload


def require_fields(mapping: dict, fields: tuple[str, ...], label: str) -> None:
    missing = [field for field in fields if field not in mapping]
    if missing:
        raise SystemExit(f"{label} is missing required fields: {', '.join(missing)}")


def main() -> int:
    args = parse_args()
    payload = json.loads(PROOF.read_text(encoding="utf-8"))
    payload = require_mapping(payload, "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json")
    status = str(payload.get("status") or "").strip().lower()
    if status not in {"pass", "passed", "ready"}:
        raise SystemExit("Chummer5A human parity matrix proof is not passed")

    if args.check_fields:
        require_fields(payload, REQUIRED_TOP_LEVEL_FIELDS, "parity matrix proof")

        matrix = require_mapping(payload.get("matrix"), "matrix")
        require_fields(matrix, REQUIRED_MATRIX_FIELDS, "matrix")
        matrix_family_ids = require_sequence(matrix.get("family_ids"), "matrix.family_ids")
        if not matrix_family_ids:
            raise SystemExit("matrix.family_ids is empty")

        ui_audit_summary = require_mapping(payload.get("ui_audit_summary"), "ui_audit_summary")
        require_fields(ui_audit_summary, REQUIRED_UI_AUDIT_SUMMARY_FIELDS, "ui_audit_summary")

        family_results = require_sequence(payload.get("family_results"), "family_results")
        if not family_results:
            raise SystemExit("family_results is empty")

        screenshot_review = require_mapping(payload.get("screenshot_review"), "screenshot_review")
        require_fields(screenshot_review, REQUIRED_SCREENSHOT_REVIEW_FIELDS, "screenshot_review")
        required_jobs = require_sequence(screenshot_review.get("required_jobs"), "screenshot_review.required_jobs")
        results = require_sequence(screenshot_review.get("results"), "screenshot_review.results")
        if not required_jobs:
            raise SystemExit("screenshot_review.required_jobs is empty")
        if not results:
            raise SystemExit("screenshot_review.results is empty")

        failure_reasons = require_sequence(payload.get("strict_failure_reasons"), "strict_failure_reasons")
        if failure_reasons:
            raise SystemExit("strict_failure_reasons must be empty for a passed parity proof")

        evidence_sources = require_sequence(payload.get("evidence_sources"), "evidence_sources")
        if not evidence_sources:
            raise SystemExit("evidence_sources is empty")

    if args.family:
        family_id = args.family.strip()
        family_results = require_sequence(payload.get("family_results"), "family_results")
        match = next(
            (
                row for row in family_results
                if isinstance(row, dict) and str(row.get("matrix_family_id") or "").strip() == family_id
            ),
            None,
        )
        if match is None:
            raise SystemExit(f"matrix family is missing from the parity proof: {family_id}")
        family_status = str(match.get("status") or "").strip().lower()
        if family_status not in {"pass", "passed", "ready"}:
            raise SystemExit(f"matrix family is not passed: {family_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
