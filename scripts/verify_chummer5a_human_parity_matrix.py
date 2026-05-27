#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json"
PARITY_AUDIT_ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
REQUIRED_ROW_FIELDS = [
    "family_id",
    "surface_id",
    "dialog_id",
    "element_id",
    "element_label",
    "present_in_chummer5a",
    "present_in_chummer6",
    "visual_parity",
    "behavioral_parity",
    "removable_if_not_in_chummer5a",
    "reason",
    "screenshot_refs",
    "runtime_receipt_refs",
    "test_refs",
]


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json is not passing")

    required_fields = payload.get("requiredFields")
    if required_fields != REQUIRED_ROW_FIELDS:
        raise SystemExit("requiredFields do not match the acceptance matrix contract")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("acceptance matrix rows are missing")
    if payload.get("rowCount") != len(rows):
        raise SystemExit("acceptance matrix rowCount does not match rows length")

    families = payload.get("families")
    if not isinstance(families, list) or not families:
        raise SystemExit("acceptance matrix families are missing")
    normalized_families = sorted(
        {
            str(value).strip()
            for value in families
            if str(value).strip()
        }
    )
    if payload.get("familyCount") != len(normalized_families):
        raise SystemExit("acceptance matrix familyCount does not match families length")

    supporting_receipts = payload.get("supportingReceipts")
    if not isinstance(supporting_receipts, dict):
        raise SystemExit("supportingReceipts are missing")
    for key in (
        "uiElementParityAudit",
        "flagshipGate",
        "visualFamiliarityGate",
        "workflowParityGate",
        "screenshotReviewGate",
        "veteranTaskTimeGate",
    ):
        receipt_path = supporting_receipts.get(key)
        if not isinstance(receipt_path, str) or not receipt_path.strip():
            raise SystemExit(f"supportingReceipts missing {key}")
        if not (WORKSPACE_ROOT / receipt_path).exists():
            raise SystemExit(f"supporting receipt path does not exist for {key}: {receipt_path}")

    audit_payload = json.loads(PARITY_AUDIT_ARTIFACT.read_text(encoding="utf-8"))
    audit_rows = audit_payload.get("rows")
    if not isinstance(audit_rows, list) or not audit_rows:
        raise SystemExit("parity audit rows are missing")

    row_ids = {
        str(row.get("element_id") or "").strip()
        for row in rows
        if isinstance(row, dict)
    }
    missing_audit_ids = sorted(
        str(row.get("id") or "").strip()
        for row in audit_rows
        if isinstance(row, dict) and str(row.get("id") or "").strip() not in row_ids
    )
    if missing_audit_ids:
        raise SystemExit(f"acceptance matrix is missing parity audit rows: {missing_audit_ids}")

    derived_families = sorted(
        {
            str(row.get("family_id") or "").strip()
            for row in rows
            if isinstance(row, dict) and str(row.get("family_id") or "").strip()
        }
    )
    if derived_families != normalized_families:
        raise SystemExit("acceptance matrix families do not match family_ids derived from rows")

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("acceptance matrix row is not an object")
        missing = [field for field in REQUIRED_ROW_FIELDS if field not in row]
        if missing:
            raise SystemExit(f"acceptance matrix row missing fields: {missing}")
        if row["present_in_chummer5a"] not in {"yes", "no"}:
            raise SystemExit(f"invalid present_in_chummer5a value for {row['element_id']}")
        if row["present_in_chummer6"] not in {"yes", "no"}:
            raise SystemExit(f"invalid present_in_chummer6 value for {row['element_id']}")
        if row["visual_parity"] not in {"yes", "no"}:
            raise SystemExit(f"invalid visual_parity value for {row['element_id']}")
        if row["behavioral_parity"] not in {"yes", "no"}:
            raise SystemExit(f"invalid behavioral_parity value for {row['element_id']}")
        if not isinstance(row["screenshot_refs"], list):
            raise SystemExit(f"screenshot_refs must be a list for {row['element_id']}")
        if not isinstance(row["runtime_receipt_refs"], list):
            raise SystemExit(f"runtime_receipt_refs must be a list for {row['element_id']}")
        if not isinstance(row["test_refs"], list) or not row["test_refs"]:
            raise SystemExit(f"test_refs must be a non-empty list for {row['element_id']}")
        if not str(row["reason"]).strip():
            raise SystemExit(f"reason is required for {row['element_id']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
