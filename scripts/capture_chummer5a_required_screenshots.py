#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
RECEIPT = PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
GATE_SCRIPT = REPO_ROOT / "scripts" / "ai" / "milestones" / "chummer5a-screenshot-review-gate.sh"
PASS_STATUSES = {"pass", "passed", "ready"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify-only", action="store_true")
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
    if not args.verify_only:
        subprocess.run(["bash", str(GATE_SCRIPT)], cwd=REPO_ROOT, check=True)

    payload = load_json(RECEIPT)
    ensure_status("Chummer5A screenshot review gate", payload)

    asset_review = payload.get("screenshotAssetReview")
    if not isinstance(asset_review, dict):
        raise SystemExit("screenshotAssetReview is missing")
    ensure_status("screenshotAssetReview", asset_review)

    required_screenshots = asset_review.get("requiredScreenshots")
    if not isinstance(required_screenshots, list) or not required_screenshots:
        raise SystemExit("requiredScreenshots is missing or empty")

    screenshot_dir_value = str(asset_review.get("screenshotDirectory") or payload.get("screenshotDirectory") or "").strip()
    if not screenshot_dir_value:
        raise SystemExit("screenshotDirectory is missing")
    screenshot_dir = Path(screenshot_dir_value)
    if not screenshot_dir.is_dir():
        raise SystemExit(f"screenshot directory does not exist: {screenshot_dir}")

    missing_files = [name for name in required_screenshots if not (screenshot_dir / str(name)).is_file()]
    if missing_files:
        raise SystemExit("required screenshot files are missing: " + ", ".join(sorted(missing_files)))

    review_jobs = payload.get("reviewJobs")
    if not isinstance(review_jobs, dict) or not review_jobs:
        raise SystemExit("reviewJobs is missing or empty")
    for job_id, job_payload in sorted(review_jobs.items()):
        if not isinstance(job_payload, dict):
            raise SystemExit(f"review job is not an object: {job_id}")
        ensure_status(f"review job {job_id}", job_payload)
        screenshots = job_payload.get("screenshots")
        if not isinstance(screenshots, list) or not screenshots:
            raise SystemExit(f"review job is missing screenshots: {job_id}")
        for screenshot_name in screenshots:
            screenshot_path = screenshot_dir / str(screenshot_name)
            if not screenshot_path.is_file():
                raise SystemExit(f"review job screenshot is missing on disk: {screenshot_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
