#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("human parity screenshot matrix is not passing")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("screenshot matrix rows are missing")

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("screenshot matrix row is not an object")
        screenshot_refs = row.get("screenshot_refs")
        if not isinstance(screenshot_refs, list) or not screenshot_refs:
            raise SystemExit(f"screenshot matrix row missing screenshot refs: {row}")
        runtime_receipt_refs = row.get("runtime_receipt_refs")
        if not isinstance(runtime_receipt_refs, list) or not runtime_receipt_refs:
            raise SystemExit(f"screenshot matrix row missing runtime receipt refs: {row}")
        test_refs = row.get("test_refs")
        if not isinstance(test_refs, list) or not test_refs:
            raise SystemExit(f"screenshot matrix row missing test refs: {row}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("screenshot matrix reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
