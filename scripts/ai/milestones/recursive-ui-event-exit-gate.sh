#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_RECURSIVE_UI_EVENT_EXIT_GATE_RECEIPT_PATH:-$repo_root/.codex-studio/published/RECURSIVE_UI_EVENT_EXIT_GATE.generated.json}"
runtime_route_inventory_path="${CHUMMER_INTERACTIVE_RUNTIME_ROUTE_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_RUNTIME_ROUTE_INVENTORY.generated.json}"
interactive_control_inventory_path="${CHUMMER_INTERACTIVE_CONTROL_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json}"
parity_audit_path="${CHUMMER_UI_ELEMENT_PARITY_AUDIT_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json}"
screenshot_evidence_path="${CHUMMER_SCREENSHOT_CONTROL_EVIDENCE_PATH:-$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots/SCREENSHOT_CONTROL_EVIDENCE.generated.json}"
workflow_parity_path="${CHUMMER_DESKTOP_WORKFLOW_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$runtime_route_inventory_path" "$interactive_control_inventory_path" "$parity_audit_path" "$screenshot_evidence_path" "$workflow_parity_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path = Path(sys.argv[1])
runtime_route_inventory_path = Path(sys.argv[2])
interactive_control_inventory_path = Path(sys.argv[3])
parity_audit_path = Path(sys.argv[4])
screenshot_evidence_path = Path(sys.argv[5])
workflow_parity_path = Path(sys.argv[6])


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def entry_value(entry: dict, *keys: str, default=None):
    for key in keys:
        if key in entry:
            return entry[key]
    return default


runtime = load_json(runtime_route_inventory_path)
interactive = load_json(interactive_control_inventory_path)
parity = load_json(parity_audit_path)
screens = load_json(screenshot_evidence_path)
workflow = load_json(workflow_parity_path)

parity_rows = {row.get("id"): row for row in parity.get("rows", []) if isinstance(row, dict)}
entries = screens.get("entries", [])
workflow_coverage = {
    row.get("workflowFamilyId") or row.get("id"): row
    for row in screens.get("workflowCoverage", [])
    if isinstance(row, dict)
}
routes = runtime.get("routes", [])

branch_requirements = {
    "shell-startup": {
        "screenshots": ["01-initial-shell-light.png"],
        "texts": ["File", "Tools", "Windows", "Help", "Ruleset:"],
        "parity_rows": ["landmark:immediate_toolstrip", "landmark:file_menu", "landmark:tools_menu", "landmark:windows_menu", "landmark:help_menu"],
    },
    "popup-file-menu": {
        "screenshots": ["02-menu-open-light.png", "19-workflow-file-menu-loaded-light.png"],
        "texts": ["File"],
        "commands": ["new_character", "open_character", "save_character"],
        "parity_rows": ["landmark:file_menu", "non_negotiable:file_menu_live"],
    },
    "dialog-global-settings": {
        "screenshots": ["03-settings-open-light.png"],
        "dialog_title": "Global Settings",
        "parity_rows": ["baseline:menu_tools_settings_masterindex_roster"],
    },
    "shell-loaded-runner": {
        "screenshots": ["04-loaded-runner-light.png", "07-loaded-runner-tabs-light.png"],
        "texts": ["Profile"],
    },
    "section-attributes-editor": {
        "screenshots": ["07-loaded-runner-tabs-light.png", "20-workflow-skills-section-light.png"],
        "texts": ["Runner"],
        "workflow_family": "attributes-skills-skill-groups-specializations-knowledge-languages",
    },
    "dialog-new-character": {
        "screenshots": ["36-workflow-new-character-dialog-light.png"],
        "workflow_family": "create-open-import-save-save-as-print-export",
    },
    "dialog-priority-workflow-priority": {
        "screenshots": ["15-creation-section-light.png", "36-workflow-new-character-dialog-light.png"],
        "workflow_family": "metatype-priorities-karma-entry",
    },
    "section-active-surface": {
        "screenshots": ["05-dense-section-light.png", "24-workflow-gear-section-light.png", "31-workflow-powers-section-light.png"],
        "workflow_family": "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
    },
}

reasons: list[str] = []
branch_checks: dict[str, dict[str, object]] = {}

for route in routes:
    route_id = str(route.get("routeId") or "").strip()
    branch = branch_requirements.get(route_id)
    if branch is None:
        continue

    route_check: dict[str, object] = {}
    expected_screenshots = list(branch.get("screenshots", []))
    matching_entries = [
        entry for entry in entries
        if entry_value(entry, "screenshot", "Screenshot", default="") in expected_screenshots
    ]

    screenshots_present = len({
        entry_value(entry, "screenshot", "Screenshot", default="")
        for entry in matching_entries
    }) == len(expected_screenshots)
    route_check["screenshots_present"] = screenshots_present
    if not screenshots_present:
        reasons.append(f"{route_id} is missing one or more dedicated screenshot captures.")

    required_dialog_title = branch.get("dialog_title")
    if required_dialog_title:
        title_ok = any(
            str(entry_value(entry, "dialogTitle", "DialogTitle", default="") or "").strip() == required_dialog_title
            for entry in matching_entries
        )
        route_check["dialog_title_present"] = title_ok
        if not title_ok:
            reasons.append(f"{route_id} did not keep dialog title '{required_dialog_title}' in screenshot evidence.")

    required_texts = list(branch.get("texts", []))
    if required_texts:
        visible_texts = {
            str(text).strip()
            for entry in matching_entries
            for text in entry_value(entry, "visibleTextSamples", "VisibleTextSamples", default=[])
            if str(text).strip()
        }
        if not visible_texts:
            visible_texts = {
                str(text).strip()
                for text in route.get("visibleTexts", [])
                if str(text).strip()
            }
        missing_texts = [
            text for text in required_texts
            if not any(text in visible_text for visible_text in visible_texts)
        ]
        route_check["missing_texts"] = missing_texts
        if missing_texts:
            reasons.append(f"{route_id} is missing screenshot landmarks: {', '.join(missing_texts)}.")

    required_commands = list(branch.get("commands", []))
    if required_commands:
        visible_commands = {
            str(command).strip()
            for command in route.get("visibleCommandIds", [])
            if str(command).strip()
        }
        missing_commands = [command for command in required_commands if command not in visible_commands]
        route_check["missing_commands"] = missing_commands
        if missing_commands:
            reasons.append(f"{route_id} is missing recursive command ids: {', '.join(missing_commands)}.")

    required_rows = list(branch.get("parity_rows", []))
    if required_rows:
        missing_rows = []
        for row_id in required_rows:
            row = parity_rows.get(row_id)
            if not row:
                missing_rows.append(row_id)
                continue
            visual_yes = str(row.get("visual_parity") or "").strip().lower() == "yes"
            behavioral_yes = str(row.get("behavioral_parity") or "").strip().lower() == "yes"
            if not visual_yes or not behavioral_yes:
                missing_rows.append(row_id)
        route_check["missing_parity_rows"] = missing_rows
        if missing_rows:
            reasons.append(f"{route_id} is missing Chummer5a parity rows: {', '.join(missing_rows)}.")

        workflow_family = branch.get("workflow_family")
    if workflow_family:
        workflow_row = workflow_coverage.get(workflow_family)
        workflow_ok = bool(workflow_row and workflow_row.get("screenshotFiles"))
        route_check["workflow_coverage_present"] = workflow_ok
        if not workflow_ok:
            reasons.append(f"{route_id} is missing workflow screenshot coverage for family '{workflow_family}'.")

    branch_checks[route_id] = route_check

workflow_family_requirements = [
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
]

workflow_checks = {
    family: bool(workflow_coverage.get(family) and workflow_coverage[family].get("screenshotFiles"))
    for family in workflow_family_requirements
}
for family, ok in workflow_checks.items():
    if not ok:
        reasons.append(f"workflow family '{family}' is missing screenshot proof.")

upstream_ok = status_ok(runtime.get("status")) and status_ok(workflow.get("status"))

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.recursive_ui_event_exit_gate",
    "status": "pass" if upstream_ok and not reasons else "fail",
    "summary": "Recursive UI event exit gate verifies route recursion, screenshot proof, and Chummer5a parity landmarks." if upstream_ok and not reasons else "Recursive UI event exit gate found missing route-local screenshot or parity proof.",
    "reasons": reasons,
    "evidence": {
        "runtimeRouteInventoryPath": str(runtime_route_inventory_path),
        "interactiveControlInventoryPath": str(interactive_control_inventory_path),
        "uiElementParityAuditPath": str(parity_audit_path),
        "screenshotEvidencePath": str(screenshot_evidence_path),
        "workflowParityPath": str(workflow_parity_path),
        "routeCount": len(routes),
        "workflowFamilyCount": len(workflow_coverage),
        "branchChecks": branch_checks,
        "workflowChecks": workflow_checks,
        "runtimeRouteInventoryStatus": runtime.get("status"),
        "interactiveControlInventoryStatus": interactive.get("status"),
        "workflowParityStatus": workflow.get("status"),
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
if receipt["status"] != "pass":
    raise SystemExit("recursive UI event exit gate failed: " + "; ".join(reasons or ["upstream receipts are not passing"]))
PY
