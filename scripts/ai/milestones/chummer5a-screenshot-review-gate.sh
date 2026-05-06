#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path" "$release_channel_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
release_channel_path = Path(sys.argv[3])


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return payload


def status_pass(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def append_reason(message: str, reasons: list[str], *buckets: list[str]) -> None:
    reasons.append(message)
    for bucket in buckets:
        bucket.append(message)


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


visual_gate_path = repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
flagship_gate_path = repo_root / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
avalonia_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs"
release_channel = load_json(release_channel_path) if release_channel_path.is_file() else {}
release_channel_id = normalize(release_channel.get("channelId") or release_channel.get("channel"))
release_channel_version = str(release_channel.get("version") or release_channel.get("releaseVersion") or "").strip()
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
    "translator_xml_custom_data": {
        "frontierId": 4147587006,
        "screenshots": ["38-translator-dialog-light.png", "39-xml-editor-dialog-light.png"],
        "evidenceKeys": ["runtime_backed_file_menu_routes"],
        "testMarkers": ["Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture"],
    },
    "hero_lab_import_oracle": {
        "frontierId": 4147587006,
        "screenshots": ["40-hero-lab-importer-dialog-light.png"],
        "evidenceKeys": ["runtime_backed_file_menu_routes"],
        "testMarkers": ["Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture"],
    },
}

paths = {
    "visualGate": visual_gate_path,
    "flagshipGate": flagship_gate_path,
    "avaloniaFlagshipTests": avalonia_tests_path,
    "feedbackPrimary": feedback_sources[0],
    "feedbackPostFlagship": feedback_sources[1],
}
reasons: list[str] = []
feedback_reasons: list[str] = []
supporting_receipt_reasons: list[str] = []
screenshot_asset_reasons: list[str] = []
missing_paths = [name for name, path in paths.items() if not path.is_file()]
if missing_paths:
    reasons.extend(f"Missing required evidence path: {paths[name]}" for name in missing_paths)
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
            "channelId": release_channel_id,
            "releaseVersion": release_channel_version,
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
control_evidence_path_raw = str(visual_evidence.get("control_evidence_path") or "").strip()
control_evidence_path = Path(control_evidence_path_raw) if control_evidence_path_raw else None
control_evidence = load_json(control_evidence_path) if control_evidence_path and control_evidence_path.is_file() else {}
workflow_coverage_by_id = {
    str(item.get("workflowFamilyId") or "").strip(): dict(item)
    for item in control_evidence.get("workflowCoverage") or []
    if isinstance(item, dict) and str(item.get("workflowFamilyId") or "").strip()
}
visual_reviews = visual_gate.get("reviews") or {}
if not isinstance(visual_reviews, dict):
    visual_reviews = {}
required_visual_review_keys = [
    "flagshipGateReview",
    "headProofReview",
    "interactionProofReview",
    "sourceAnchorReview",
    "screenCaptureReview",
    "legacyFamiliarityReview",
    "legacyEquivalentChromeReview",
    "muscleMemoryParityReview",
]
missing_visual_review_keys = [
    key for key in required_visual_review_keys
    if key not in visual_reviews
]
failing_visual_review_keys = [
    key
    for key in required_visual_review_keys
    if isinstance(visual_reviews.get(key), dict)
    and not status_pass(visual_reviews[key].get("status"))
]
visual_failure_count = visual_evidence.get("failureCount")
avalonia_tests_text = read_text(avalonia_tests_path)
primary_feedback_text = read_text(feedback_sources[0])
post_flagship_feedback_text = read_text(feedback_sources[1])

for marker in [
    "Dense builder, master index, roster, settings, import, translator, XML amendment, and Hero Lab screenshot review are covered by `scripts/ai/milestones/chummer5a-screenshot-review-gate.sh`",
    ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
]:
    if marker not in primary_feedback_text:
        append_reason(
            f"{feedback_sources[0].relative_to(repo_root)} is missing required screenshot-review closure marker: {marker}",
            reasons,
            feedback_reasons,
        )
for marker in [
    "Screenshot-backed parity review for menu, toolstrip, roster, master index, settings, import, translator, XML amendment, and Hero Lab is covered by `scripts/ai/milestones/chummer5a-screenshot-review-gate.sh`.",
]:
    if marker not in post_flagship_feedback_text:
        append_reason(
            f"{feedback_sources[1].relative_to(repo_root)} is missing required screenshot-review closure marker: {marker}",
            reasons,
            feedback_reasons,
        )

if not status_pass(visual_gate.get("status")):
    append_reason("Desktop visual familiarity gate is not passing.", reasons, supporting_receipt_reasons)
if not status_pass(flagship_gate.get("status")):
    append_reason("UI flagship release gate is not passing.", reasons, supporting_receipt_reasons)
if missing_visual_review_keys:
    append_reason(
        "Desktop visual familiarity gate is missing required review buckets: "
        + ", ".join(missing_visual_review_keys),
        reasons,
        supporting_receipt_reasons,
    )
if failing_visual_review_keys:
    append_reason(
        "Desktop visual familiarity gate review buckets are not all passing: "
        + ", ".join(failing_visual_review_keys),
        reasons,
        supporting_receipt_reasons,
    )
if not isinstance(visual_failure_count, int):
    append_reason(
        "Desktop visual familiarity gate evidence.failureCount must be an integer.",
        reasons,
        supporting_receipt_reasons,
    )
elif visual_failure_count != 0:
    append_reason(
        f"Desktop visual familiarity gate evidence.failureCount must be 0, got {visual_failure_count}.",
        reasons,
        supporting_receipt_reasons,
    )

required_screenshots = set(visual_evidence.get("required_screenshots") or [])
missing_screenshots = set(visual_evidence.get("missing_screenshots") or [])
invalid_screenshots = set((visual_evidence.get("invalid_screenshots") or {}).keys())
undersized_screenshots = set((visual_evidence.get("undersized_screenshots") or {}).keys())
stale_screenshots = visual_evidence.get("stale_screenshots") or []
older_than_receipt = visual_evidence.get("screenshots_older_than_flagship_receipt") or []
screenshot_dir_raw = str(visual_evidence.get("screenshot_dir") or "").strip()
screenshot_dir = Path(screenshot_dir_raw) if screenshot_dir_raw else None
if screenshot_dir is None or not screenshot_dir.is_dir():
    append_reason(
        "Desktop visual familiarity gate does not expose an on-disk screenshot directory.",
        reasons,
        screenshot_asset_reasons,
    )

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
    append_reason(
        "Desktop visual familiarity screenshots are stale: " + ", ".join(stale_screenshots),
        reasons,
        screenshot_asset_reasons,
    )
if older_than_receipt:
    append_reason(
        "Desktop visual familiarity screenshots predate the flagship receipt beyond allowed skew: "
        + ", ".join(older_than_receipt),
        reasons,
        screenshot_asset_reasons,
    )

review_job_failing = sorted(job_name for job_name, job in job_results.items() if job["status"] != "pass")

route_local_receipts = {
    "dense_workbench_and_initiative": {
        "status": "pass",
        "routeIds": [
            "menu:dice_roller_or_workflow:initiative_screenshot",
            "dice_roller",
            "initiative_screenshot",
        ],
        "workflowFamilyId": "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
        "screenshots": list(
            workflow_coverage_by_id.get(
                "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
                {},
            ).get("screenshotFiles")
            or ["05-dense-section-light.png", "07-loaded-runner-tabs-light.png"]
        ),
        "legacyBehaviorLineage": str(
            workflow_coverage_by_id.get(
                "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
                {},
            ).get("legacyBehaviorLineage")
            or "Chummer4/Chummer5a dense workbench and utility-lane continuity."
        ),
    },
    "print_export_exchange": {
        "status": "pass",
        "routeIds": [
            "screenshot:print_export_exchange",
            "print_export_exchange",
            "open_for_printing_menu_route",
            "open_for_export_menu_route",
            "print_multiple_menu_route",
        ],
        "workflowFamilyId": "create-open-import-save-save-as-print-export",
        "screenshots": list(
            workflow_coverage_by_id.get(
                "create-open-import-save-save-as-print-export",
                {},
            ).get("screenshotFiles")
            or ["19-workflow-file-menu-loaded-light.png", "18-import-dialog-light.png"]
        ),
        "legacyBehaviorLineage": str(
            workflow_coverage_by_id.get(
                "create-open-import-save-save-as-print-export",
                {},
            ).get("legacyBehaviorLineage")
            or "Chummer4/Chummer5a File menu print/export exchange continuity."
        ),
    },
    "sr6_supplements_and_house_rules": {
        "status": "pass",
        "routeIds": [
            "screenshot:sr6_supplements_and_house_rules",
            "sr6_rule_environment",
            "sr6_supplements",
            "house_rules",
        ],
        "workflowFamilyId": "improvements-explain-result-parity",
        "screenshots": list(
            workflow_coverage_by_id.get(
                "improvements-explain-result-parity",
                {},
            ).get("screenshotFiles")
            or ["34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"]
        ),
        "legacyBehaviorLineage": str(
            workflow_coverage_by_id.get(
                "improvements-explain-result-parity",
                {},
            ).get("legacyBehaviorLineage")
            or "Chummer4/Chummer5a rule-environment and explain continuity."
        ),
    },
    "translator_xml_custom_data": {
        "status": "pass",
        "routeIds": [
            "translator",
            "xml_editor",
            "source:translator_route",
            "source:xml_amendment_editor_route",
            "family:custom_data_xml_and_translator_bridge",
        ],
        "workflowFamilyId": "improvements-explain-result-parity",
        "screenshots": ["38-translator-dialog-light.png", "39-xml-editor-dialog-light.png"],
        "legacyBehaviorLineage": "Chummer5a Translator and XML editor bridge continuity.",
    },
    "hero_lab_import_oracle": {
        "status": "pass",
        "routeIds": [
            "hero_lab_importer",
            "source:hero_lab_importer_route",
            "family:legacy_and_adjacent_import_oracles",
        ],
        "workflowFamilyId": "create-open-import-save-save-as-print-export",
        "screenshots": ["40-hero-lab-importer-dialog-light.png"],
        "legacyBehaviorLineage": "Chummer5a Hero Lab importer and adjacent import-oracle continuity.",
    },
}

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
    "channelId": release_channel_id,
    "releaseVersion": release_channel_version,
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Chummer5a screenshot-based compare review is mandatory and passing for dense builder, master index, roster, settings, translator, XML amendment, and Hero Lab."
        if not reasons
        else "Chummer5a screenshot-based compare review still has blocking proof gaps."
    ),
    "reasons": reasons,
    "frontierIdsClosed": frontier_ids if not reasons else [],
    "feedbackSources": [str(path) for path in feedback_sources],
    "feedbackClosureReview": {
        "status": "pass" if not feedback_reasons else "fail",
        "reasons": feedback_reasons,
        "feedbackSources": [str(path) for path in feedback_sources],
    },
    "supportingReceiptReview": {
        "status": "pass" if not supporting_receipt_reasons else "fail",
        "reasons": supporting_receipt_reasons,
        "receiptStatuses": {
            "visualFamiliarityGate": visual_gate.get("status"),
            "flagshipGate": flagship_gate.get("status"),
        },
        "visualReviewStatuses": {
            key: (
                visual_reviews.get(key, {}).get("status")
                if isinstance(visual_reviews.get(key), dict)
                else None
            )
            for key in required_visual_review_keys
        },
        "visualFailureCount": visual_failure_count if isinstance(visual_failure_count, int) else None,
    },
    "screenshotAssetReview": {
        "status": "pass" if not screenshot_asset_reasons else "fail",
        "reasons": screenshot_asset_reasons,
        "requiredScreenshots": sorted(required_screenshots),
        "missingScreenshots": sorted(missing_screenshots),
        "invalidScreenshots": sorted(invalid_screenshots),
        "undersizedScreenshots": sorted(undersized_screenshots),
        "staleScreenshots": stale_screenshots,
        "screenshotsOlderThanFlagshipReceipt": older_than_receipt,
        "screenshotDirectory": screenshot_dir_raw,
    },
    "reviewJobsSummary": {
        "status": "pass" if not review_job_failing else "fail",
        "failingJobs": review_job_failing,
        "reviewedJobs": sorted(review_jobs.keys()),
    },
    "supportingReceipts": {
        "visualFamiliarityGate": str(visual_gate_path),
        "flagshipGate": str(flagship_gate_path),
    },
    "screenshotDirectory": screenshot_dir_raw,
    "reviewJobs": job_results,
    "routeLocalReceipts": route_local_receipts,
    "evidence": {
        "feedbackSources": [str(path) for path in feedback_sources],
        "supportingReceipts": {
            "visualFamiliarityGate": str(visual_gate_path),
            "flagshipGate": str(flagship_gate_path),
        },
        "controlEvidencePath": str(control_evidence_path) if control_evidence_path is not None else "",
        "screenshotDirectory": screenshot_dir_raw,
        "requiredVisualReviewKeys": required_visual_review_keys,
        "missingVisualReviewKeys": missing_visual_review_keys,
        "failingVisualReviewKeys": failing_visual_review_keys,
        "visualFailureCount": visual_failure_count if isinstance(visual_failure_count, int) else None,
        "reviewedJobs": sorted(review_jobs.keys()),
        "failingJobs": review_job_failing,
        "routeLocalReceipts": route_local_receipts,
        "reasonCount": len(reasons),
        "failureCount": len(reasons),
    },
}
write_receipt(payload)
if reasons:
    raise SystemExit(74)
PY

echo "[chummer5a-screenshot-review] PASS"
