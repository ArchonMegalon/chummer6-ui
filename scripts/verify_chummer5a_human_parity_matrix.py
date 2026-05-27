#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json"
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

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("acceptance matrix rows are missing")

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
