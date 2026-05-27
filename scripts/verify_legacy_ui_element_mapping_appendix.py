#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_LEGACY_UI_ELEMENT_MAPPING_APPENDIX.generated.json"
MATRIX_ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("legacy UI element mapping appendix is not passing")

    source_artifacts = payload.get("sourceArtifacts")
    if not isinstance(source_artifacts, dict):
        raise SystemExit("sourceArtifacts are missing")
    for key in ("legacyUiElementParity", "uiElementParityAudit", "humanParityAcceptanceMatrix"):
        path = source_artifacts.get(key)
        if not isinstance(path, str) or not path.strip():
            raise SystemExit(f"sourceArtifacts missing {key}")
        if not (WORKSPACE_ROOT / path).exists():
            raise SystemExit(f"source artifact path does not exist for {key}: {path}")

    family_rows = payload.get("familyRows")
    if not isinstance(family_rows, list) or not family_rows:
        raise SystemExit("legacy UI element mapping appendix familyRows are missing")
    if payload.get("familyRowCount") != len(family_rows):
        raise SystemExit("familyRowCount does not match familyRows length")
    for row in family_rows:
        if not isinstance(row, dict):
            raise SystemExit("familyRows entry is not an object")
        if str(row.get("status") or "").strip().lower() != "pass":
            raise SystemExit(f"familyRows entry is not passing: {row.get('family_id')}")
        if int(row.get("legacy_event_count") or 0) <= 0 and int(row.get("legacy_dynamic_element_count") or 0) <= 0:
            raise SystemExit(f"familyRows entry has no legacy coverage counts: {row.get('family_id')}")
        if not isinstance(row.get("mapped_current_ids"), list) or not row.get("mapped_current_ids"):
            raise SystemExit(f"familyRows entry is missing mapped_current_ids: {row.get('family_id')}")
        if not isinstance(row.get("proof_markers"), list) or not row.get("proof_markers"):
            raise SystemExit(f"familyRows entry is missing proof_markers: {row.get('family_id')}")

    legacy_disposition = payload.get("legacyDispositionSummary")
    if not isinstance(legacy_disposition, dict):
        raise SystemExit("legacyDispositionSummary is missing")
    if int(legacy_disposition.get("missingLegacyElementDispositionCount") or 0) != 0:
        raise SystemExit("missingLegacyElementDispositionCount must be zero")
    if int(legacy_disposition.get("familyFallbackLegacyElementDispositionCount") or 0) != 0:
        raise SystemExit("familyFallbackLegacyElementDispositionCount must be zero")

    dialog_rows = payload.get("dialogRows")
    if not isinstance(dialog_rows, list) or not dialog_rows:
        raise SystemExit("dialogRows are missing")
    if payload.get("dialogRowCount") != len(dialog_rows):
        raise SystemExit("dialogRowCount does not match dialogRows length")
    dialog_ids = {str(row.get("dialog_id") or "").strip() for row in dialog_rows if isinstance(row, dict)}

    matrix_payload = json.loads(MATRIX_ARTIFACT.read_text(encoding="utf-8"))
    matrix_rows = matrix_payload.get("rows")
    if not isinstance(matrix_rows, list) or not matrix_rows:
        raise SystemExit("human parity matrix rows are missing")
    expected_dialog_ids = {
        str(row.get("dialog_id") or "").strip()
        for row in matrix_rows
        if isinstance(row, dict) and str(row.get("dialog_id") or "").strip()
    }
    if not expected_dialog_ids.issubset(dialog_ids):
        missing_dialog_ids = sorted(expected_dialog_ids - dialog_ids)
        raise SystemExit(f"dialogRows missing matrix dialog coverage: {missing_dialog_ids}")

    for row in dialog_rows:
        if not isinstance(row, dict):
            raise SystemExit("dialogRows entry is not an object")
        sample_rows = row.get("sample_rows")
        if not isinstance(sample_rows, list) or not sample_rows:
            raise SystemExit(f"dialogRows entry is missing sample_rows: {row.get('dialog_id')}")

    audit_row_count = int(payload.get("auditRowCount") or 0)
    if audit_row_count <= 0:
        raise SystemExit("auditRowCount must be positive")
    parity_audit_payload = json.loads((WORKSPACE_ROOT / str(source_artifacts["uiElementParityAudit"])).read_text(encoding="utf-8-sig"))
    parity_audit_rows = parity_audit_payload.get("rows")
    if not isinstance(parity_audit_rows, list) or not parity_audit_rows:
        raise SystemExit("parity audit source rows are missing")
    if audit_row_count != len(parity_audit_rows):
        raise SystemExit("auditRowCount does not match parity audit source rows")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("legacy UI element mapping appendix reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
