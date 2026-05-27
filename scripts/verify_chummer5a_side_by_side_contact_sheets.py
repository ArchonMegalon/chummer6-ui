#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("side-by-side contact sheets artifact is not passing")

    parity_lab_capture_pack = payload.get("parityLabCapturePack")
    if not isinstance(parity_lab_capture_pack, str) or not parity_lab_capture_pack.strip():
        raise SystemExit("parityLabCapturePack is missing")
    if not (WORKSPACE_ROOT / parity_lab_capture_pack).exists():
        raise SystemExit("parityLabCapturePack path does not exist")

    contact_sheet_directory = payload.get("contactSheetDirectory")
    if not isinstance(contact_sheet_directory, str) or not contact_sheet_directory.strip():
        raise SystemExit("contactSheetDirectory is missing")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("contact sheet rows are missing")
    if payload.get("rowCount") != len(rows):
        raise SystemExit("contact sheet rowCount does not match rows length")

    seen_sheet_ids = set()

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("contact sheet row is not an object")
        sheet_id = str(row.get("sheetId") or "").strip()
        if not sheet_id:
            raise SystemExit("contact sheet row is missing sheetId")
        if sheet_id in seen_sheet_ids:
            raise SystemExit(f"duplicate contact sheet id: {sheet_id}")
        seen_sheet_ids.add(sheet_id)
        if str(row.get("status") or "").strip().lower() != "pass":
            raise SystemExit(f"contact sheet row is not passing: {sheet_id}")
        sheet_path = WORKSPACE_ROOT / str(row.get("sheetPath") or "")
        if not sheet_path.exists():
            raise SystemExit(f"contact sheet missing: {sheet_path}")
        if not str(row.get("sheetPath") or "").startswith(contact_sheet_directory.rstrip("/") + "/"):
            raise SystemExit(f"contact sheet path escapes contactSheetDirectory: {row}")
        legacy_anchor_refs = row.get("legacyAnchorRefs")
        if not isinstance(legacy_anchor_refs, list) or not legacy_anchor_refs:
            raise SystemExit(f"contact sheet missing legacy anchor refs: {row}")
        current_refs = row.get("currentScreenshotRefs")
        if not isinstance(current_refs, list) or not current_refs:
            raise SystemExit(f"contact sheet missing current screenshot refs: {row}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("contact sheet reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
