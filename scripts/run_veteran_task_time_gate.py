#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
PASS_STATUSES = {"pass", "passed", "ready"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--family")
    parser.add_argument("--visual-density", action="store_true")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"Expected JSON object in {path}")
    return payload


def ensure_status(name: str, payload: dict[str, Any]) -> None:
    status = str(payload.get("status") or "").strip().lower()
    if status not in PASS_STATUSES:
        raise SystemExit(f"{name} is not passed")


def main() -> int:
    args = parse_args()

    veteran_gate = load_json(PUBLISHED / "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json")
    screenshot_gate = load_json(PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json")
    dense_posture_gate = load_json(PUBLISHED / "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json")
    legacy_chrome_gate = load_json(PUBLISHED / "CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE.generated.json")

    ensure_status("veteran task-time evidence gate", veteran_gate)

    if args.visual_density:
        ensure_status("classic dense workbench posture gate", dense_posture_gate)
        ensure_status("legacy equivalent chrome gate", legacy_chrome_gate)

    if args.family:
        family_id = args.family.strip()
        if family_id != "dense_builder_and_career":
            raise SystemExit(f"unsupported veteran task-time family scope: {family_id}")
        ensure_status("classic dense workbench posture gate", dense_posture_gate)
        ensure_status("Chummer5A screenshot review gate", screenshot_gate)
        review_jobs = screenshot_gate.get("reviewJobs")
        if not isinstance(review_jobs, dict):
            raise SystemExit("screenshot review jobs are missing")
        dense_builder_job = review_jobs.get("dense_builder")
        if not isinstance(dense_builder_job, dict):
            raise SystemExit("dense_builder screenshot review job is missing")
        ensure_status("dense_builder screenshot review job", dense_builder_job)

    covered_jobs = veteran_gate.get("taskTimeCoverageReview", {}).get("coveredJobs")
    if not isinstance(covered_jobs, list) or not covered_jobs:
        raise SystemExit("veteran task-time coveredJobs is missing or empty")

    required_jobs = {
        "open_import",
        "save",
        "settings",
        "sourcebooks",
        "roster",
        "print_export",
        "translator_xml_custom_data",
    }
    missing_jobs = sorted(required_jobs.difference(str(job) for job in covered_jobs))
    if missing_jobs:
        raise SystemExit("veteran task-time coveredJobs is missing: " + ", ".join(missing_jobs))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
