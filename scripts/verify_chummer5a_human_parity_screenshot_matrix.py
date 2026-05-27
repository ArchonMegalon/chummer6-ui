#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path
import yaml


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("human parity screenshot matrix is not passing")

    design_matrix_path = payload.get("designMatrixPath")
    if not isinstance(design_matrix_path, str) or not design_matrix_path.strip():
        raise SystemExit("designMatrixPath is missing")
    design_matrix = yaml.safe_load((REPO_ROOT.parent / design_matrix_path).read_text(encoding="utf-8")) or {}
    expected_tokens = {
        str(token).strip()
        for family in design_matrix.get("families") or []
        if isinstance(family, dict)
        for token in family.get("required_screenshots") or []
        if str(token).strip()
    }
    if not expected_tokens:
        raise SystemExit("design matrix required_screenshots are missing")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("screenshot matrix rows are missing")
    if payload.get("rowCount") != len(rows):
        raise SystemExit("screenshot matrix rowCount does not match rows length")

    seen_tokens = set()

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("screenshot matrix row is not an object")
        screenshot_token = str(row.get("screenshot_token") or "").strip()
        if not screenshot_token:
            raise SystemExit(f"screenshot matrix row missing screenshot_token: {row}")
        seen_tokens.add(screenshot_token)
        if str(row.get("status") or "").strip().lower() != "pass":
            raise SystemExit(f"screenshot matrix row is not passing: {screenshot_token}")
        screenshot_refs = row.get("screenshot_refs")
        if not isinstance(screenshot_refs, list) or not screenshot_refs:
            raise SystemExit(f"screenshot matrix row missing screenshot refs: {row}")
        runtime_receipt_refs = row.get("runtime_receipt_refs")
        if not isinstance(runtime_receipt_refs, list) or not runtime_receipt_refs:
            raise SystemExit(f"screenshot matrix row missing runtime receipt refs: {row}")
        test_refs = row.get("test_refs")
        if not isinstance(test_refs, list) or not test_refs:
            raise SystemExit(f"screenshot matrix row missing test refs: {row}")

    missing_tokens = sorted(expected_tokens - seen_tokens)
    if missing_tokens:
        raise SystemExit(f"screenshot matrix is missing required screenshot tokens: {missing_tokens}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("screenshot matrix reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
