#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_CHUMMER6_ONLY_CONTROL_JUSTIFICATION.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("Chummer6-only control justification artifact is not passing")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("control justification rows are missing")

    seen_controls = set()
    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("control justification row is not an object")
        element_id = str(row.get("element_id") or "").strip()
        if not element_id:
            raise SystemExit("control justification row is missing element_id")
        seen_controls.add(element_id)
        if not str(row.get("reason") or "").strip():
            raise SystemExit(f"control {element_id} is missing a row-level reason")

    required_controls = payload.get("requiredHiddenCatalogControls")
    if not isinstance(required_controls, list) or not required_controls:
        raise SystemExit("requiredHiddenCatalogControls are missing")

    normalized_required_controls = {
        str(value).strip()
        for value in required_controls
        if str(value).strip()
    }
    if not normalized_required_controls:
        raise SystemExit("requiredHiddenCatalogControls are empty")

    missing = sorted(normalized_required_controls - seen_controls)
    if missing:
        raise SystemExit(f"required catalog-only controls missing from justification artifact: {missing}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("control justification reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
