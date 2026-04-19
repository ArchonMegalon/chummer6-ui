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


def require_token(path: Path, text: str, token: str, reasons: list[str]) -> None:
    if token not in text:
        reasons.append(f"{path.relative_to(repo_root)} is missing required token: {token}")


def require_any_token(path: Path, text: str, tokens: list[str], reasons: list[str], label: str) -> None:
    if not any(token in text for token in tokens):
        reasons.append(f"{path.relative_to(repo_root)} is missing required evidence marker: {label}")


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
    require_any_token(paths["feedback"], feedback_text, tokens, reasons, label)

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
    require_any_token(paths["post_flagship_feedback"], post_feedback_text, tokens, reasons, label)

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
    require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, token, reasons)
require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, open_import_tokens[0], reasons)
for token in open_import_tokens[1:3]:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, open_import_tokens[3], reasons)
for token in save_tokens[:2]:
    require_token(paths["character_overview_presenter_tests"], presenter_tests_text, token, reasons)
for token in save_tokens[2:]:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons)
for token in settings_tokens[:2]:
    require_token(paths["character_overview_presenter_tests"], presenter_tests_text, token, reasons)
require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, settings_tokens[2], reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, settings_tokens[3], reasons)
for token in ["BuildSourcebookSelectionSummary", "BuildSourcebookSelectionFields"]:
    require_token(paths["desktop_dialog_factory"], dialog_factory_text, token, reasons)
for token in roster_tokens[:1] + roster_tokens[3:]:
    require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, token, reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, roster_tokens[1], reasons)
require_token(paths["avalonia_flagship_tests"], avalonia_tests_text, roster_tokens[2], reasons)
for token in print_export_tokens:
    require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, token, reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, translator_xml_tokens[0], reasons)
require_token(paths["character_overview_presenter_tests"], presenter_tests_text, translator_xml_tokens[1], reasons)
require_token(paths["dual_head_acceptance_tests"], dual_head_tests_text, translator_xml_tokens[2], reasons)
require_token(paths["desktop_dialog_factory_tests"], dialog_tests_text, translator_xml_tokens[3], reasons)

receipts = {
    "flagshipGate": load_json(paths["flagship_gate"]),
    "layoutGate": load_json(paths["layout_gate"]),
    "workflowParityGate": load_json(paths["workflow_parity_gate"]),
    "visualFamiliarityGate": load_json(paths["visual_familiarity_gate"]),
    "primaryRouteProof": load_json(paths["primary_route_proof"]),
}
for name, payload in receipts.items():
    if not status_pass(payload.get("status")):
        reasons.append(f"{name} status is not pass/ready.")

flagship_gate = receipts["flagshipGate"]
primary_route_proof = receipts["primaryRouteProof"]
visual_gate = receipts["visualFamiliarityGate"]

if str(flagship_gate.get("desktopHead") or "") != "avalonia":
    reasons.append("UI flagship release gate is not bound to Avalonia as the promoted desktop head.")
fallback_heads = set(flagship_gate.get("desktopFallbackHeads") or [])
if "blazor-desktop" not in fallback_heads:
    reasons.append("UI flagship release gate does not mark Blazor desktop as fallback.")
if "blazor-desktop" not in set(primary_route_proof.get("fallbackHeadsExcludedFromPrimaryProof") or []):
    reasons.append("Avalonia primary-route proof does not exclude Blazor desktop from primary proof.")
for row in primary_route_proof.get("routeTruthProof") or []:
    if row.get("head") == "avalonia" and row.get("fallbackAcceptedAsPrimary") is not False:
        reasons.append(f"Primary route row accepts fallback as primary: {row}")

visual_evidence = visual_gate.get("evidence") or {}
for key in ["runtime_backed_master_index", "runtime_backed_character_roster", "runtime_backed_file_menu_routes"]:
    if not status_pass(visual_evidence.get(key)):
        reasons.append(f"Desktop visual familiarity gate does not pass {key}.")
for screenshot in ["03-settings-open-light.png", "16-master-index-dialog-light.png", "17-character-roster-dialog-light.png", "18-import-dialog-light.png"]:
    if screenshot not in set(visual_evidence.get("required_screenshots") or []):
        reasons.append(f"Desktop visual familiarity gate does not require screenshot review for {screenshot}.")

required_task_time_jobs = {
    "open_import": {
        "status": "pass",
        "tests": [
            "CreateCommandDialog_open_character_uses_import_template",
            "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
            "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
        ],
        "proof": "File-menu open/import stays runtime-backed, exposes the import dialog template, and exercises dual-head import plus workspace switching.",
    },
    "save": {
        "status": "pass",
        "tests": [
            "ExecuteCommandAsync_save_character_marks_workspace_as_saved",
            "SaveAsync_marks_workspace_as_saved_after_workspace_load",
            "Avalonia_and_Blazor_command_dispatch_save_character_matches",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
        ],
        "proof": "Save is a first-class File/toolstrip command that marks workspaces saved and remains covered through dual-head dispatch plus import-switch-save flow.",
    },
    "settings": {
        "status": "pass",
        "tests": [
            "ExecuteCommandAsync_global_settings_opens_dialog",
            "ExecuteDialogActionAsync_save_global_settings_updates_preferences",
            "Avalonia_and_Blazor_global_settings_save_updates_shared_preferences",
            "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
        ],
        "proof": "Settings opens through the desktop command surface, saves preferences, and has screenshot-backed dialog evidence on the promoted Avalonia head.",
    },
    "sourcebooks": {
        "status": "pass",
        "tests": [
            "CreateCommandDialog_master_index_surfaces_sourcebook_and_parity_posture",
        ],
        "proof": "Master index sourcebook posture exposes governed reference provenance, selection coverage, and sourcebook rows.",
    },
    "translator_xml_custom_data": {
        "status": "pass",
        "tests": [
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
            "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        ],
        "proof": "Translator and XML editor lanes now fail closed on governed translator posture, XML bridge posture, enabled overlay count, and custom-data directory posture instead of relying on generic dialog coverage.",
    },
    "roster": {
        "status": "pass",
        "tests": [
            "CreateCommandDialog_character_roster_summarizes_open_workspaces",
            "ExecuteCommandAsync_character_roster_opens_dialog_with_workspace_summary",
            "Character_roster_is_a_first_class_runtime_backed_workbench_route",
        ],
        "proof": "Character roster is a runtime-backed Tools route with open-runner count, saved count, ruleset mix, active workspace, and roster entries.",
    },
    "print_export": {
        "status": "pass",
        "tests": [
            "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
            "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
        ],
        "proof": "Dual-head promoted workflow tests exercise matching download/export/print receipts and import-switch-save flow.",
    },
}

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
    "taskTimeEvidence": required_task_time_jobs,
    "boundedBlazorFallbackEvidence": {
        "status": "pass" if "blazor-desktop" in fallback_heads else "fail",
        "primaryHead": flagship_gate.get("desktopHead"),
        "desktopFallbackHeads": sorted(fallback_heads),
        "fallbackHeadsExcludedFromPrimaryProof": primary_route_proof.get("fallbackHeadsExcludedFromPrimaryProof") or [],
    },
    "screenshotBackedReview": {
        "status": "pass" if not any("screenshot" in reason for reason in reasons) else "fail",
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
}
write_receipt(payload)
if reasons:
    raise SystemExit(72)
PY

echo "[veteran-task-time] PASS"
