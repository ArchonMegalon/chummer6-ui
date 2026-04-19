#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return payload


def status_pass(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


visual_gate_path = repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
flagship_gate_path = repo_root / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
avalonia_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs"
feedback_sources = [
    repo_root / "feedback" / "2026-04-12-classic-dense-workbench-and-veteran-parity.md",
    repo_root / "feedback" / "2026-04-13-post-flagship-release-train-and-veteran-certification.md",
]
frontier_ids = [3782970110, 2714856833, 1186439541, 4871476959]
review_jobs = {
    "dense_builder": {
        "frontierId": 3782970110,
        "screenshots": ["05-dense-section-light.png", "06-dense-section-dark.png"],
        "evidenceKeys": ["legacy_dense_builder_rhythm"],
        "testMarkers": ["Character_creation_preserves_familiar_dense_builder_rhythm"],
    },
    "master_index": {
        "frontierId": 2714856833,
        "screenshots": ["16-master-index-dialog-light.png"],
        "evidenceKeys": ["runtime_backed_master_index"],
        "testMarkers": ["Master_index_is_a_first_class_runtime_backed_workbench_route"],
    },
    "roster": {
        "frontierId": 1186439541,
        "screenshots": ["17-character-roster-dialog-light.png"],
        "evidenceKeys": ["runtime_backed_character_roster"],
        "testMarkers": ["Character_roster_is_a_first_class_runtime_backed_workbench_route"],
    },
    "settings": {
        "frontierId": 4871476959,
        "screenshots": ["03-settings-open-light.png"],
        "evidenceKeys": ["runtime_backed_file_menu_routes"],
        "testMarkers": ["Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters"],
    },
}

paths = {
    "visualGate": visual_gate_path,
    "flagshipGate": flagship_gate_path,
    "avaloniaFlagshipTests": avalonia_tests_path,
}
reasons: list[str] = []
missing_paths = [name for name, path in paths.items() if not path.is_file()]
if missing_paths:
    reasons.extend(f"Missing required evidence path: {paths[name]}" for name in missing_paths)
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
            "status": "fail",
            "summary": "Chummer5a screenshot review cannot be trusted because required inputs are missing.",
            "reasons": reasons,
            "frontierIdsClosed": [],
            "evidencePaths": {name: str(path) for name, path in paths.items()},
        }
    )
    raise SystemExit(73)

visual_gate = load_json(visual_gate_path)
flagship_gate = load_json(flagship_gate_path)
visual_evidence = visual_gate.get("evidence") or {}
if not isinstance(visual_evidence, dict):
    visual_evidence = {}
avalonia_tests_text = avalonia_tests_path.read_text(encoding="utf-8")

if not status_pass(visual_gate.get("status")):
    reasons.append("Desktop visual familiarity gate is not passing.")
if not status_pass(flagship_gate.get("status")):
    reasons.append("UI flagship release gate is not passing.")

required_screenshots = set(visual_evidence.get("required_screenshots") or [])
missing_screenshots = set(visual_evidence.get("missing_screenshots") or [])
invalid_screenshots = set((visual_evidence.get("invalid_screenshots") or {}).keys())
undersized_screenshots = set((visual_evidence.get("undersized_screenshots") or {}).keys())
stale_screenshots = visual_evidence.get("stale_screenshots") or []
older_than_receipt = visual_evidence.get("screenshots_older_than_flagship_receipt") or []
screenshot_dir_raw = str(visual_evidence.get("screenshot_dir") or "").strip()
screenshot_dir = Path(screenshot_dir_raw) if screenshot_dir_raw else None
if screenshot_dir is None or not screenshot_dir.is_dir():
    reasons.append("Desktop visual familiarity gate does not expose an on-disk screenshot directory.")

job_results: dict[str, dict[str, Any]] = {}
for job_name, job in review_jobs.items():
    screenshots = list(job["screenshots"])
    job_reasons: list[str] = []
    for screenshot in screenshots:
        if screenshot not in required_screenshots:
            job_reasons.append(f"{screenshot} is not mandatory in DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.")
        if screenshot in missing_screenshots:
            job_reasons.append(f"{screenshot} is reported missing.")
        if screenshot in invalid_screenshots:
            job_reasons.append(f"{screenshot} is reported corrupt or unreadable.")
        if screenshot in undersized_screenshots:
            job_reasons.append(f"{screenshot} is below the review resolution floor.")
        if screenshot_dir is not None and not (screenshot_dir / screenshot).is_file():
            job_reasons.append(f"{screenshot} is absent from the screenshot directory.")
    for key in job["evidenceKeys"]:
        if not status_pass(visual_evidence.get(key)):
            job_reasons.append(f"Visual familiarity evidence key is not pass: {key}.")
    for marker in job["testMarkers"]:
        if marker not in avalonia_tests_text:
            job_reasons.append(f"Avalonia flagship tests are missing review marker: {marker}.")
    job_results[job_name] = {
        "frontierId": job["frontierId"],
        "status": "pass" if not job_reasons else "fail",
        "screenshots": screenshots,
        "evidenceKeys": list(job["evidenceKeys"]),
        "testMarkers": list(job["testMarkers"]),
        "reasons": job_reasons,
    }
    reasons.extend(f"{job_name}: {reason}" for reason in job_reasons)

if stale_screenshots:
    reasons.append("Desktop visual familiarity screenshots are stale: " + ", ".join(stale_screenshots))
if older_than_receipt:
    reasons.append(
        "Desktop visual familiarity screenshots predate the flagship receipt beyond allowed skew: "
        + ", ".join(older_than_receipt)
    )

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Chummer5a screenshot-based compare review is mandatory and passing for dense builder, master index, roster, and settings."
        if not reasons
        else "Chummer5a screenshot-based compare review still has blocking proof gaps."
    ),
    "reasons": reasons,
    "frontierIdsClosed": frontier_ids if not reasons else [],
    "feedbackSources": [str(path) for path in feedback_sources],
    "supportingReceipts": {
        "visualFamiliarityGate": str(visual_gate_path),
        "flagshipGate": str(flagship_gate_path),
    },
    "screenshotDirectory": screenshot_dir_raw,
    "reviewJobs": job_results,
}
write_receipt(payload)
if reasons:
    raise SystemExit(74)
PY

echo "[chummer5a-screenshot-review] PASS"
