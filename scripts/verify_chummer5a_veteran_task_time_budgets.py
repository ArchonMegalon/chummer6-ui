#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_VETERAN_TASK_TIME_BUDGETS.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("veteran task-time budgets artifact is not passing")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("veteran task-time rows are missing")

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("veteran task-time row is not an object")
        required_numeric_fields = [
            "click_count_chummer5a_baseline",
            "click_count_chummer6_current",
            "keystroke_count_chummer5a_baseline",
            "keystroke_count_chummer6_current",
            "elapsed_seconds_budget",
            "elapsed_seconds_chummer5a_baseline",
            "elapsed_seconds_chummer6_current",
        ]
        for field in required_numeric_fields:
            if not isinstance(row.get(field), int):
                raise SystemExit(f"{field} must be numeric for task {row.get('task_id')}")
        if row.get("pass") is not True:
            raise SystemExit(f"task-time budget did not pass for task {row.get('task_id')}")
        if not str(row.get("reason") or "").strip():
            raise SystemExit(f"task-time reason missing for task {row.get('task_id')}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("veteran task-time reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
