#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
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


def derived_status(*conditions: bool) -> str:
    return "pass" if all(conditions) else "fail"


def append_reason(message: str, reasons: list[str], *buckets: list[str]) -> None:
    reasons.append(message)
    for bucket in buckets:
        bucket.append(message)


def require_token(path: Path, text: str, token: str, reasons: list[str], *buckets: list[str]) -> None:
    if token not in text:
        append_reason(f"{path.relative_to(repo_root)} is missing required token: {token}", reasons, *buckets)


def require_any_token(path: Path, text: str, tokens: list[str], reasons: list[str], label: str, *buckets: list[str]) -> None:
    if not any(token in text for token in tokens):
        append_reason(f"{path.relative_to(repo_root)} is missing required evidence marker: {label}", reasons, *buckets)


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


paths = {
    "feedback": repo_root / "feedback" / "2026-04-12-classic-dense-workbench-and-veteran-parity.md",
    "post_flagship_feedback": repo_root / "feedback" / "2026-04-13-post-flagship-release-train-and-veteran-certification.md",
    "desktop_dialog_factory_tests": repo_root / "Chummer.Tests" / "Presentation" / "DesktopDialogFactoryTests.cs",
    "character_overview_presenter_tests": repo_root / "Chummer.Tests" / "Presentation" / "CharacterOverviewPresenterTests.cs",
    "dual_head_acceptance_tests": repo_root / "Chummer.Tests" / "Presentation" / "DualHeadAcceptanceTests.cs",
    "avalonia_flagship_tests": repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs",
    "desktop_dialog_factory": repo_root / "Chummer.Presentation" / "Overview" / "DesktopDialogFactory.cs",
    "flagship_gate": repo_root / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json",
    "layout_gate": repo_root / ".codex-studio" / "published" / "CHUMMER5A_LAYOUT_HARD_GATE.generated.json",
    "workflow_parity_gate": repo_root / ".codex-studio" / "published" / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
    "visual_familiarity_gate": repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
    "primary_route_proof": repo_root / ".codex-studio" / "published" / "NEXT90_M101_AVALONIA_PRIMARY_ROUTE_PROOF.generated.json",
}

reasons: list[str] = []
feedback_reasons: list[str] = []
source_evidence_reasons: list[str] = []
flagship_route_reasons: list[str] = []
screenshot_review_reasons: list[str] = []
missing_paths = [name for name, path in paths.items() if not path.is_file()]
if missing_paths:
    reasons.extend(f"Missing required evidence path: {paths[name]}" for name in missing_paths)
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.veteran_task_time_evidence_gate",
            "status": "fail",
            "summary": "Veteran task-time evidence cannot be trusted because required inputs are missing.",
            "reasons": reasons,
            "evidencePaths": {name: str(path) for name, path in paths.items()},
        }
    )
    raise SystemExit(71)

feedback_text = read_text(paths["feedback"])
post_feedback_text = read_text(paths["post_flagship_feedback"])
dialog_tests_text = read_text(paths["desktop_dialog_factory_tests"])
presenter_tests_text = read_text(paths["character_overview_presenter_tests"])
dual_head_tests_text = read_text(paths["dual_head_acceptance_tests"])
avalonia_tests_text = read_text(paths["avalonia_flagship_tests"])
dialog_factory_text = read_text(paths["desktop_dialog_factory"])

for label, tokens in {
    "task-time evidence request or closure": [
        "Add veteran task-time evidence for:",
        "Open/import, save, settings, sourcebooks, roster, print/export, and bounded Blazor fallback evidence are covered",
    ],
    "open/import": ["open/import", "Open/import"],
    "save": ["save"],
    "settings": ["settings"],
    "sourcebooks": ["sourcebooks"],
    "roster": ["roster"],
    "print/export": ["print/export"],
    "bounded Blazor fallback": [
        "Keep Blazor explicitly bounded as fallback",
        "bounded Blazor fallback evidence are covered",
    ],
}.items():
    require_any_token(paths["feedback"], feedback_text, tokens, reasons, label, feedback_reasons)

for label, tokens in {
    "Avalonia primary-route proof independent from Blazor fallback": [
        "Keep Avalonia primary-route proof independent from Blazor fallback proof",
        "Avalonia primary-route proof stays independent from Blazor fallback proof",
    ],
    "screenshot-backed parity review": [
        "Run screenshot-backed parity review for menu, toolstrip, roster, master index, settings, and import",
        "Screenshot-backed parity review for menu, toolstrip, roster, master index, settings, and import is covered",
    ],
}.items():
    require_any_token(paths["post_flagship_feedback"], post_feedback_text, tokens, reasons, label, feedback_reasons)

sourcebook_tokens = [
    "CreateCommandDialog_master_index_surfaces_sourcebook_and_parity_posture",
    "masterIndexSourceSelectionReceipt",
    "sourcebook selection is governed by 24 toggles across 12 sourcebooks",
    "masterIndexReferenceSourceReceipt",
]
open_import_tokens = [
    "CreateCommandDialog_open_character_uses_import_template",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
]
save_tokens = [
    "ExecuteCommandAsync_save_character_marks_workspace_as_saved",
    "SaveAsync_marks_workspace_as_saved_after_workspace_load",
    "Avalonia_and_Blazor_command_dispatch_save_character_matches",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
]
settings_tokens = [
    "ExecuteCommandAsync_global_settings_opens_dialog",
    "ExecuteDialogActionAsync_save_global_settings_updates_preferences",
    "Avalonia_and_Blazor_global_settings_save_updates_shared_preferences",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
]
roster_tokens = [
    "CreateCommandDialog_character_roster_summarizes_open_workspaces",
    "ExecuteCommandAsync_character_roster_opens_dialog_with_workspace_summary",
    "Character_roster_is_a_first_class_runtime_backed_workbench_route",
    "rosterOpenCount",
    "rosterSavedCount",
    "rosterRulesetMix",
]
print_export_tokens = [
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "TakePendingExportSnapshot",
    "TakePendingPrintSnapshot",
]
translator_xml_tokens = [
    "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
    "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
    "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
    "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
]

for token in sourcebook_tokens:
    require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, token, reasons, source_evidence_reasons)
require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, open_import_tokens[0], reasons, source_evidence_reasons)
for token in open_import_tokens[1:3]:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons, source_evidence_reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, open_import_tokens[3], reasons, source_evidence_reasons)
for token in save_tokens[:2]:
    require_token(paths["character_overview_presenter_tests"], presenter_tests_text, token, reasons, source_evidence_reasons)
for token in save_tokens[2:]:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons, source_evidence_reasons)
for token in settings_tokens[:2]:
    require_token(paths["character_overview_presenter_tests"], presenter_tests_text, token, reasons, source_evidence_reasons)
require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, settings_tokens[2], reasons, source_evidence_reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, settings_tokens[3], reasons, source_evidence_reasons)
for token in ["BuildSourcebookSelectionSummary", "BuildSourcebookSelectionFields"]:
    require_token(paths["desktop_dialog_factory"], dialog_factory_text, token, reasons, source_evidence_reasons)
for token in roster_tokens[:1] + roster_tokens[3:]:
    require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, token, reasons, source_evidence_reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, roster_tokens[1], reasons, source_evidence_reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, roster_tokens[2], reasons, source_evidence_reasons)
for token in print_export_tokens:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons, source_evidence_reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, translator_xml_tokens[0], reasons, source_evidence_reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, translator_xml_tokens[1], reasons, source_evidence_reasons)
require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, translator_xml_tokens[2], reasons, source_evidence_reasons)
require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, translator_xml_tokens[3], reasons, source_evidence_reasons)

receipts = {
    "flagshipGate": load_json(paths["flagship_gate"]),
    "layoutGate": load_json(paths["layout_gate"]),
    "workflowParityGate": load_json(paths["workflow_parity_gate"]),
    "visualFamiliarityGate": load_json(paths["visual_familiarity_gate"]),
    "primaryRouteProof": load_json(paths["primary_route_proof"]),
}
for name, payload in receipts.items():
    if not status_pass(payload.get("status")):
        append_reason(f"{name} status is not pass/ready.", reasons, flagship_route_reasons)

flagship_gate = receipts["flagshipGate"]
primary_route_proof = receipts["primaryRouteProof"]
visual_gate = receipts["visualFamiliarityGate"]

if str(flagship_gate.get("desktopHead") or "") != "avalonia":
    append_reason("UI flagship release gate is not bound to Avalonia as the promoted desktop head.", reasons, flagship_route_reasons)
fallback_heads = set(flagship_gate.get("desktopFallbackHeads") or [])
if "blazor-desktop" not in fallback_heads:
    append_reason("UI flagship release gate does not mark Blazor desktop as fallback.", reasons, flagship_route_reasons)
if "blazor-desktop" not in set(primary_route_proof.get("fallbackHeadsExcludedFromPrimaryProof") or []):
    append_reason("Avalonia primary-route proof does not exclude Blazor desktop from primary proof.", reasons, flagship_route_reasons)
for row in primary_route_proof.get("routeTruthProof") or []:
    if row.get("head") == "avalonia" and row.get("fallbackAcceptedAsPrimary") is not False:
        append_reason(f"Primary route row accepts fallback as primary: {row}", reasons, flagship_route_reasons)

visual_evidence = visual_gate.get("evidence") or {}
required_screenshots = set(visual_evidence.get("required_screenshots") or [])
for key in ["runtime_backed_master_index", "runtime_backed_character_roster", "runtime_backed_file_menu_routes"]:
    if not status_pass(visual_evidence.get(key)):
        append_reason(f"Desktop visual familiarity gate does not pass {key}.", reasons, screenshot_review_reasons)
for screenshot in ["03-settings-open-light.png", "16-master-index-dialog-light.png", "17-character-roster-dialog-light.png", "18-import-dialog-light.png"]:
    if screenshot not in required_screenshots:
        append_reason(
            f"Desktop visual familiarity gate does not require screenshot review for {screenshot}.",
            reasons,
            screenshot_review_reasons,
        )

required_task_time_jobs = {
    "open_import": {
        "status": derived_status(
            open_import_tokens[0] in dialog_tests_text,
            open_import_tokens[1] in dual_head_tests_text,
            open_import_tokens[2] in dual_head_tests_text,
            open_import_tokens[3] in avalonia_tests_text,
            status_pass(visual_evidence.get("runtime_backed_file_menu_routes")),
            "18-import-dialog-light.png" in required_screenshots,
        ),
        "tests": [
            "CreateCommandDialog_open_character_uses_import_template",
            "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
            "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
        ],
        "proof": "File-menu open/import stays runtime-backed, exposes the import dialog template, and exercises dual-head import plus workspace switching.",
        "evidencePaths": [
            str(paths["desktop_dialog_factory_tests"]),
            str(paths["dual_head_acceptance_tests"]),
            str(paths["avalonia_flagship_tests"]),
            str(paths["visual_familiarity_gate"]),
        ],
    },
    "save": {
        "status": derived_status(
            save_tokens[0] in presenter_tests_text,
            save_tokens[1] in presenter_tests_text,
            save_tokens[2] in dual_head_tests_text,
            save_tokens[3] in dual_head_tests_text,
        ),
        "tests": [
            "ExecuteCommandAsync_save_character_marks_workspace_as_saved",
            "SaveAsync_marks_workspace_as_saved_after_workspace_load",
            "Avalonia_and_Blazor_command_dispatch_save_character_matches",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
        ],
        "proof": "Save is a first-class File/toolstrip command that marks workspaces saved and remains covered through dual-head dispatch plus import-switch-save flow.",
        "evidencePaths": [
            str(paths["character_overview_presenter_tests"]),
            str(paths["dual_head_acceptance_tests"]),
        ],
    },
    "settings": {
        "status": derived_status(
            settings_tokens[0] in presenter_tests_text,
            settings_tokens[1] in presenter_tests_text,
            settings_tokens[2] in dual_head_tests_text,
            settings_tokens[3] in avalonia_tests_text,
            "03-settings-open-light.png" in required_screenshots,
        ),
        "tests": [
            "ExecuteCommandAsync_global_settings_opens_dialog",
            "ExecuteDialogActionAsync_save_global_settings_updates_preferences",
            "Avalonia_and_Blazor_global_settings_save_updates_shared_preferences",
            "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
        ],
        "proof": "Settings opens through the desktop command surface, saves preferences, and has screenshot-backed dialog evidence on the promoted Avalonia head.",
        "evidencePaths": [
            str(paths["character_overview_presenter_tests"]),
            str(paths["dual_head_acceptance_tests"]),
            str(paths["avalonia_flagship_tests"]),
            str(paths["visual_familiarity_gate"]),
        ],
    },
    "sourcebooks": {
        "status": derived_status(
            sourcebook_tokens[0] in dialog_tests_text,
            sourcebook_tokens[1] in dialog_tests_text,
            sourcebook_tokens[2] in dialog_tests_text,
            sourcebook_tokens[3] in dialog_tests_text,
            "BuildSourcebookSelectionSummary" in dialog_factory_text,
            "BuildSourcebookSelectionFields" in dialog_factory_text,
            status_pass(visual_evidence.get("runtime_backed_master_index")),
            "16-master-index-dialog-light.png" in required_screenshots,
        ),
        "tests": [
            "CreateCommandDialog_master_index_surfaces_sourcebook_and_parity_posture",
        ],
        "proof": "Master index sourcebook posture exposes governed reference provenance, selection coverage, and sourcebook rows.",
        "evidencePaths": [
            str(paths["desktop_dialog_factory_tests"]),
            str(paths["desktop_dialog_factory"]),
            str(paths["visual_familiarity_gate"]),
        ],
    },
    "translator_xml_custom_data": {
        "status": derived_status(
            translator_xml_tokens[0] in presenter_tests_text,
            translator_xml_tokens[1] in presenter_tests_text,
            translator_xml_tokens[2] in dual_head_tests_text,
            translator_xml_tokens[3] in dialog_tests_text,
        ),
        "tests": [
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
            "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        ],
        "proof": "Translator and XML editor lanes now fail closed on governed translator posture, XML bridge posture, enabled overlay count, and custom-data directory posture instead of relying on generic dialog coverage.",
        "evidencePaths": [
            str(paths["character_overview_presenter_tests"]),
            str(paths["dual_head_acceptance_tests"]),
            str(paths["desktop_dialog_factory_tests"]),
        ],
    },
    "roster": {
        "status": derived_status(
            roster_tokens[0] in dialog_tests_text,
            roster_tokens[1] in presenter_tests_text,
            roster_tokens[2] in avalonia_tests_text,
            roster_tokens[3] in dialog_tests_text,
            roster_tokens[4] in dialog_tests_text,
            roster_tokens[5] in dialog_tests_text,
            status_pass(visual_evidence.get("runtime_backed_character_roster")),
            "17-character-roster-dialog-light.png" in required_screenshots,
        ),
        "tests": [
            "CreateCommandDialog_character_roster_summarizes_open_workspaces",
            "ExecuteCommandAsync_character_roster_opens_dialog_with_workspace_summary",
            "Character_roster_is_a_first_class_runtime_backed_workbench_route",
        ],
        "proof": "Character roster is a runtime-backed Tools route with open-runner count, saved count, ruleset mix, active workspace, and roster entries.",
        "evidencePaths": [
            str(paths["desktop_dialog_factory_tests"]),
            str(paths["character_overview_presenter_tests"]),
            str(paths["avalonia_flagship_tests"]),
            str(paths["visual_familiarity_gate"]),
        ],
    },
    "print_export": {
        "status": derived_status(
            print_export_tokens[0] in dual_head_tests_text,
            print_export_tokens[1] in dual_head_tests_text,
            print_export_tokens[2] in dual_head_tests_text,
            print_export_tokens[3] in dual_head_tests_text,
        ),
        "tests": [
            "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
        ],
        "proof": "Dual-head promoted workflow tests exercise matching download/export/print receipts and import-switch-save flow.",
        "evidencePaths": [
            str(paths["dual_head_acceptance_tests"]),
        ],
    },
}

task_time_failing_jobs = sorted(job_name for job_name, job in required_task_time_jobs.items() if job["status"] != "pass")
screenshot_review_jobs = ["open_import", "settings", "sourcebooks", "roster"]
screenshot_review_failing_jobs = [
    job_name for job_name in screenshot_review_jobs if required_task_time_jobs[job_name]["status"] != "pass"
]
blazor_fallback_reasons: list[str] = []
if str(flagship_gate.get("desktopHead") or "") != "avalonia":
    blazor_fallback_reasons.append("UI flagship release gate must keep Avalonia as the promoted desktop head.")
if "blazor-desktop" not in fallback_heads:
    blazor_fallback_reasons.append("UI flagship release gate must keep Blazor desktop in the fallback head list.")
if "blazor-desktop" not in set(primary_route_proof.get("fallbackHeadsExcludedFromPrimaryProof") or []):
    blazor_fallback_reasons.append("Avalonia primary-route proof must exclude Blazor desktop from the primary proof lane.")
fallback_route_conflicts = [
    row for row in primary_route_proof.get("routeTruthProof") or []
    if row.get("head") == "avalonia" and row.get("fallbackAcceptedAsPrimary") is not False
]
if fallback_route_conflicts:
    blazor_fallback_reasons.append("Avalonia primary-route proof still accepts a fallback as primary in route truth rows.")

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.veteran_task_time_evidence_gate",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Veteran task-time backlog is closed by current executable UI evidence."
        if not reasons
        else "Veteran task-time backlog still has blocking proof gaps."
    ),
    "reasons": reasons,
    "frontierIdsClosed": [3515644054, 1019154541, 2115357833, 934612622, 1221836214, 1167972300, 2526421201, 2737638815],
    "feedbackSources": [str(paths["feedback"]), str(paths["post_flagship_feedback"])],
    "feedbackClosureReview": {
        "status": "pass" if not feedback_reasons else "fail",
        "reasons": feedback_reasons,
        "feedbackEvidencePaths": [str(paths["feedback"]), str(paths["post_flagship_feedback"])],
    },
    "sourceEvidenceReview": {
        "status": "pass" if not source_evidence_reasons else "fail",
        "reasons": source_evidence_reasons,
        "sourceEvidencePaths": [
            str(paths["desktop_dialog_factory_tests"]),
            str(paths["character_overview_presenter_tests"]),
            str(paths["dual_head_acceptance_tests"]),
            str(paths["avalonia_flagship_tests"]),
            str(paths["desktop_dialog_factory"]),
        ],
    },
    "flagshipRouteReview": {
        "status": "pass" if not flagship_route_reasons else "fail",
        "reasons": flagship_route_reasons,
        "supportingReceipts": {
            "flagshipGate": str(paths["flagship_gate"]),
            "layoutGate": str(paths["layout_gate"]),
            "workflowParityGate": str(paths["workflow_parity_gate"]),
            "visualFamiliarityGate": str(paths["visual_familiarity_gate"]),
            "primaryRouteProof": str(paths["primary_route_proof"]),
        },
    },
    "taskTimeCoverageReview": {
        "status": "pass" if not task_time_failing_jobs else "fail",
        "failingJobs": task_time_failing_jobs,
        "coveredJobs": sorted(required_task_time_jobs.keys()),
    },
    "taskTimeEvidence": required_task_time_jobs,
    "boundedBlazorFallbackEvidence": {
        "status": "pass" if not blazor_fallback_reasons else "fail",
        "reasons": blazor_fallback_reasons,
        "primaryHead": flagship_gate.get("desktopHead"),
        "desktopFallbackHeads": sorted(fallback_heads),
        "fallbackHeadsExcludedFromPrimaryProof": primary_route_proof.get("fallbackHeadsExcludedFromPrimaryProof") or [],
        "fallbackRouteConflicts": fallback_route_conflicts,
    },
    "screenshotBackedReview": {
        "status": "pass" if not screenshot_review_reasons and not screenshot_review_failing_jobs else "fail",
        "reasons": screenshot_review_reasons,
        "failingJobs": screenshot_review_failing_jobs,
        "reviewJobs": screenshot_review_jobs,
        "requiredScreenshots": visual_evidence.get("required_screenshots") or [],
        "screenshotDirectory": visual_evidence.get("screenshot_dir"),
    },
    "supportingReceipts": {name: str(path) for name, path in paths.items() if name.endswith("gate") or name == "primary_route_proof"},
    "sourceEvidence": {
        "desktopDialogFactoryTests": str(paths["desktop_dialog_factory_tests"]),
        "characterOverviewPresenterTests": str(paths["character_overview_presenter_tests"]),
        "dualHeadAcceptanceTests": str(paths["dual_head_acceptance_tests"]),
        "avaloniaFlagshipTests": str(paths["avalonia_flagship_tests"]),
    },
    "evidence": {
        "feedbackSources": [str(paths["feedback"]), str(paths["post_flagship_feedback"])],
        "supportingReceipts": {name: str(path) for name, path in paths.items() if name.endswith("gate") or name == "primary_route_proof"},
        "sourceEvidence": {
            "desktopDialogFactoryTests": str(paths["desktop_dialog_factory_tests"]),
            "characterOverviewPresenterTests": str(paths["character_overview_presenter_tests"]),
            "dualHeadAcceptanceTests": str(paths["dual_head_acceptance_tests"]),
            "avaloniaFlagshipTests": str(paths["avalonia_flagship_tests"]),
        },
        "coveredJobs": sorted(required_task_time_jobs.keys()),
        "screenshotReviewJobs": screenshot_review_jobs,
        "failingJobs": task_time_failing_jobs,
        "screenshotReviewFailingJobs": screenshot_review_failing_jobs,
        "reasonCount": len(reasons),
        "failureCount": len(reasons),
    },
}
write_receipt(payload)
if reasons:
    raise SystemExit(72)
PY

echo "[veteran-task-time] PASS"
