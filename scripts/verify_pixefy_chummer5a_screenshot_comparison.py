#!/usr/bin/env python3
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
RECEIPT = PUBLISHED / "PIXEFY_CHUMMER5A_SCREENSHOT_COMPARISON_GATE.generated.json"
SCREENSHOT_DIR = PUBLISHED / "ui-flagship-release-gate-screenshots"


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise SystemExit(f"JSON root is not an object: {path}")
    return payload


def status_is_pass(payload: dict[str, Any]) -> bool:
    return str(payload.get("status") or "").strip().lower() in {"pass", "passed", "ready"}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def main() -> int:
    PUBLISHED.mkdir(parents=True, exist_ok=True)
    reasons: list[str] = []

    pixefy_targets_path = PUBLISHED / "PUBLIC_SURFACE_QA_TARGETS.generated.json"
    screenshot_review_path = PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
    contact_sheets_path = PUBLISHED / "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json"
    flagship_gate_path = PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json"

    for path in [pixefy_targets_path, screenshot_review_path, contact_sheets_path, flagship_gate_path]:
        if not path.is_file():
            reasons.append(f"missing required receipt: {path.relative_to(REPO_ROOT)}")

    pixefy_targets = load_json(pixefy_targets_path) if pixefy_targets_path.is_file() else {}
    screenshot_review = load_json(screenshot_review_path) if screenshot_review_path.is_file() else {}
    contact_sheets = load_json(contact_sheets_path) if contact_sheets_path.is_file() else {}
    flagship_gate = load_json(flagship_gate_path) if flagship_gate_path.is_file() else {}

    if str(pixefy_targets.get("provider") or "").strip().lower() != "pixefy":
        reasons.append("PUBLIC_SURFACE_QA_TARGETS.generated.json must declare provider Pixefy.")

    if not status_is_pass(screenshot_review):
        reasons.append("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json is not passing.")
    if not status_is_pass(contact_sheets):
        reasons.append("CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json is not passing.")
    if not status_is_pass(flagship_gate):
        reasons.append("UI_FLAGSHIP_RELEASE_GATE.generated.json is not passing.")

    screenshot_files = sorted(path.name for path in SCREENSHOT_DIR.glob("*.png")) if SCREENSHOT_DIR.is_dir() else []
    if len(screenshot_files) < 40:
        reasons.append(f"expected at least 40 promoted screenshots, found {len(screenshot_files)}.")

    required_new_gate_screenshots = {
        "01-initial-shell-light.png",
        "15-creation-section-light.png",
        "36-workflow-new-character-dialog-light.png",
    }
    missing_required = sorted(required_new_gate_screenshots.difference(screenshot_files))
    if missing_required:
        reasons.append(f"missing required screenshot comparisons: {', '.join(missing_required)}")

    contact_rows = contact_sheets.get("rows")
    if not isinstance(contact_rows, list) or not contact_rows:
        reasons.append("side-by-side contact sheet rows are missing.")

    status = "pass" if not reasons else "fail"
    payload = {
        "contract_name": "chummer6-ui.pixefy_chummer5a_screenshot_comparison_gate",
        "generated_at": now_iso(),
        "status": status,
        "provider": "Pixefy",
        "comparison_baseline": "Chummer5a screenshot/contact-sheet receipts",
        "screenshot_directory": str(SCREENSHOT_DIR.relative_to(REPO_ROOT)),
        "screenshot_count": len(screenshot_files),
        "required_screenshots": sorted(required_new_gate_screenshots),
        "receipts": {
            "pixefy_targets": str(pixefy_targets_path.relative_to(REPO_ROOT)),
            "screenshot_review": str(screenshot_review_path.relative_to(REPO_ROOT)),
            "side_by_side_contact_sheets": str(contact_sheets_path.relative_to(REPO_ROOT)),
            "flagship_gate": str(flagship_gate_path.relative_to(REPO_ROOT)),
        },
        "reasons": reasons,
    }
    RECEIPT.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        raise SystemExit("; ".join(reasons))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
