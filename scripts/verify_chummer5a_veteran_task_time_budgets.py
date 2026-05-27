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

    source_veteran_pack = payload.get("sourceVeteranPack")
    if not isinstance(source_veteran_pack, str) or not source_veteran_pack.strip():
        raise SystemExit("sourceVeteranPack is missing")
    veteran_pack_path = REPO_ROOT.parent / source_veteran_pack
    if not veteran_pack_path.exists():
        raise SystemExit("sourceVeteranPack path does not exist")

    required_task_ids = payload.get("requiredTaskIds")
    if not isinstance(required_task_ids, list) or not required_task_ids:
        raise SystemExit("requiredTaskIds are missing")
    normalized_required_task_ids = {
        str(value).strip()
        for value in required_task_ids
        if str(value).strip()
    }
    if not normalized_required_task_ids:
        raise SystemExit("requiredTaskIds are empty")

    rows = payload.get("rows")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("veteran task-time rows are missing")
    if payload.get("rowCount") != len(rows):
        raise SystemExit("veteran task-time rowCount does not match rows length")

    seen_task_ids = set()

    for row in rows:
        if not isinstance(row, dict):
            raise SystemExit("veteran task-time row is not an object")
        task_id = str(row.get("task_id") or "").strip()
        if not task_id:
            raise SystemExit("veteran task-time row is missing task_id")
        seen_task_ids.add(task_id)
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
                raise SystemExit(f"{field} must be numeric for task {task_id}")
        if row.get("pass") is not True:
            raise SystemExit(f"task-time budget did not pass for task {task_id}")
        if not str(row.get("reason") or "").strip():
            raise SystemExit(f"task-time reason missing for task {task_id}")

    missing_task_ids = sorted(normalized_required_task_ids - seen_task_ids)
    if missing_task_ids:
        raise SystemExit(f"veteran task-time artifact is missing required tasks: {missing_task_ids}")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("veteran task-time reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
