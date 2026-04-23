#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_SECTION_HOST_RULESET_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

EXPECTED_STANDARD_SECTION_IDS = [
    "gear",
    "inventory",
    "gearlocations",
    "weapons",
    "weaponaccessories",
    "weaponlocations",
    "armors",
    "armormods",
    "armorlocations",
    "cyberwares",
    "drugs",
    "spells",
    "powers",
    "complexforms",
    "initiationgrades",
    "spirits",
    "critterpowers",
    "aiprograms",
    "vehicles",
    "contacts",
    "skills",
    "qualities",
    "profile",
]

EXPECTED_SR6_ADAPTED_SECTION_IDS = []

EXPECTED_COMMAND_IDS = [
    "file",
    "tools",
    "windows",
    "help",
    "new_character",
    "new_critter",
    "open_character",
    "open_for_printing",
    "open_for_export",
    "save_character",
    "save_character_as",
    "print_character",
    "export_character",
    "copy",
    "paste",
    "dice_roller",
    "global_settings",
    "character_settings",
    "update",
    "restart",
    "switch_ruleset",
    "translator",
    "xml_editor",
    "hero_lab_importer",
    "master_index",
    "character_roster",
    "data_exporter",
    "report_bug",
    "print_setup",
    "print_multiple",
    "exit",
    "new_window",
    "close_window",
    "close_all",
    "wiki",
    "discord",
    "revision_history",
    "dumpshock",
    "about",
]

EXPECTED_TAB_IDS = [
    "tab-info",
    "tab-attributes",
    "tab-skills",
    "tab-qualities",
    "tab-magician",
    "tab-combat",
    "tab-gear",
    "tab-contacts",
    "tab-rules",
    "tab-notes",
]

EXPECTED_WORKSPACE_ACTION_IDS = [
    "tab-info.summary",
    "tab-info.validate",
    "tab-info.profile",
    "tab-info.rules",
    "tab-info.attributes",
    "tab-skills.skills",
    "tab-qualities.qualities",
    "tab-magician.spells",
    "tab-combat.weapons",
    "tab-gear.inventory",
    "tab-contacts.contacts",
    "tab-rules.rules",
    "tab-notes.metadata",
]

EXPECTED_ACTIONS_BY_TAB = {
    "tab-info": [
        "tab-info.summary",
        "tab-info.validate",
        "tab-info.profile",
        "tab-info.rules",
        "tab-info.attributes",
    ],
    "tab-skills": ["tab-skills.skills"],
    "tab-qualities": ["tab-qualities.qualities"],
    "tab-magician": ["tab-magician.spells"],
    "tab-combat": ["tab-combat.weapons"],
    "tab-gear": ["tab-gear.inventory"],
    "tab-contacts": ["tab-contacts.contacts"],
    "tab-rules": ["tab-rules.rules"],
    "tab-notes": ["tab-notes.metadata"],
}

SECTION_TEST_MARKERS = [
    "SectionQuickActionCatalog_backed_sections_keep_only_real_primary_actions",
    "SectionQuickActionCatalog_unbacked_sections_stay_hidden",
]

SHELL_CATALOG_TEST_MARKERS = [
    "ResolveCommands_and_navigation_tabs_clone_requested_ruleset",
    "ResolveWorkspaceActionsForTab_returns_ruleset_cloned_tab_scoped_inventory",
    "ResolveWorkspaceActionsForTab_falls_back_to_tab_info_when_requested_tab_is_unknown",
]

PROJECTOR_TEST_MARKERS = [
    "Project_projects_standard_section_quick_actions_into_section_host_state",
    "Project_hides_unbacked_section_quick_actions",
    "Project_projects_runtime_backed_magic_and_aug_section_quick_actions",
    "Project_formats_ruleset_conditioned_navigator_section_action_labels",
]

DIRECTIVE_TEST_MARKERS = [
    "BuildSectionNotice_uses_ruleset_specific_copy_for_rules_and_build_lab_surfaces",
    "ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset",
    "FormatDialogNotice_applies_ruleset_specific_dialog_prefixes",
]

TEST_FILTER_COMMANDS = [
    "Name~SectionQuickActionCatalog_",
    "Name~ResolveCommands_and_navigation_tabs_clone_requested_ruleset",
    "Name~ResolveWorkspaceActionsForTab_",
    "Name~Project_projects_standard_section_quick_actions_into_section_host_state",
    "Name~Project_hides_unbacked_section_quick_actions",
    "Name~Project_projects_runtime_backed_magic_and_aug_section_quick_actions",
    "Name~Project_formats_ruleset_conditioned_navigator_section_action_labels",
    "Name~ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset",
    "Name~BuildSectionNotice_uses_ruleset_specific_copy_for_rules_and_build_lab_surfaces",
    "Name~FormatDialogNotice_applies_ruleset_specific_dialog_prefixes",
]

PATHS = {
    "section_catalog": repo_root / "Chummer.Presentation/Rulesets/SectionQuickActionCatalog.cs",
    "legacy_control_catalog": repo_root / "Chummer.Presentation/Overview/LegacyUiControlCatalog.cs",
    "shell_catalog": repo_root / "Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs",
    "projector": repo_root / "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs",
    "section_tests": repo_root / "Chummer.Tests/Presentation/SectionQuickActionCatalogTests.cs",
    "shell_catalog_tests": repo_root / "Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs",
    "projector_tests": repo_root / "Chummer.Tests/Presentation/MainWindowShellFrameProjectorTests.cs",
    "directive_tests": repo_root / "Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs",
    "verify_script": repo_root / "scripts/ai/verify.sh",
    "ruleset_receipt": repo_root / ".codex-studio/published/RULESET_UI_ADAPTATION.generated.json",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def unique_preserving_order(values: list[str]) -> list[str]:
    seen: set[str] = set()
    ordered: list[str] = []
    for value in values:
        if value not in seen:
            ordered.append(value)
            seen.add(value)
    return ordered


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.section_host_ruleset_parity",
    "status": "fail",
    "summary": "Section-host quick-action and ruleset-conditioned subflow parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "sourcePaths": {name: str(path.relative_to(repo_root)) for name, path in PATHS.items()},
        "expectedStandardSectionIds": EXPECTED_STANDARD_SECTION_IDS,
        "expectedSr6AdaptedSectionIds": EXPECTED_SR6_ADAPTED_SECTION_IDS,
        "expectedCommandIds": EXPECTED_COMMAND_IDS,
        "expectedTabIds": EXPECTED_TAB_IDS,
        "expectedWorkspaceActionIds": EXPECTED_WORKSPACE_ACTION_IDS,
        "expectedActionsByTab": EXPECTED_ACTIONS_BY_TAB,
        "sectionTests": {},
        "shellCatalogTests": {},
        "projectorTests": {},
        "directiveTests": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence = payload["evidence"]
source_artifact_failures: list[str] = []
section_inventory_failures: list[str] = []
shell_inventory_failures: list[str] = []
test_marker_failures: list[str] = []
projector_failures: list[str] = []
verify_wiring_failures: list[str] = []
ruleset_receipt_failures: list[str] = []
execution_failures: list[str] = []


def add_failure(message: str, *buckets: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)

missing_files = [str(path.relative_to(repo_root)) for path in PATHS.values() if not path.is_file()]
evidence["missingFiles"] = missing_files
if missing_files:
    add_failure("Required section-host/ruleset parity proof files are missing.", source_artifact_failures)

texts = {name: read_text(path) for name, path in PATHS.items() if path.is_file()}

section_catalog_text = texts.get("section_catalog", "")
standard_section_ids_found: list[str] = []
for match in re.finditer(r'((?:"[^"]+"\s+or\s+)*"[^"]+")\s*=>\s*PrimaryOnly\(', section_catalog_text):
    standard_section_ids_found.extend(re.findall(r'"([^"]+)"', match.group(1)))
evidence["standardSectionIdsFound"] = standard_section_ids_found
evidence["standardSectionCount"] = len(standard_section_ids_found)
if standard_section_ids_found != EXPECTED_STANDARD_SECTION_IDS:
    add_failure(
        "Section quick-action standard section inventory drifted from the parity contract.",
        section_inventory_failures,
    )

sr6_adapted_section_ids_found: list[str] = []
for match in re.finditer(r'((?:"[^"]+"\s+or\s+)*"[^"]+")\s*=>\s*([A-Za-z0-9_]+)\(', section_catalog_text):
    if match.group(2) == "PrimaryOnly":
        continue
    sr6_adapted_section_ids_found.extend(re.findall(r'"([^"]+)"', match.group(1)))
evidence["sr6AdaptedSectionIdsFound"] = sr6_adapted_section_ids_found
evidence["sr6AdaptedSectionCount"] = len(sr6_adapted_section_ids_found)
if sr6_adapted_section_ids_found != EXPECTED_SR6_ADAPTED_SECTION_IDS:
    add_failure("SR6-adapted section inventory drifted from the parity contract.", section_inventory_failures)

quick_action_control_ids_found = unique_preserving_order(re.findall(r'PrimaryOnly\("([^"]+)"', section_catalog_text))
evidence["quickActionControlIdsFound"] = quick_action_control_ids_found
evidence["quickActionControlCount"] = len(quick_action_control_ids_found)

legacy_control_catalog_text = texts.get("legacy_control_catalog", "")
known_control_ids = re.findall(r'"([a-z0-9_]+)"', legacy_control_catalog_text)
unknown_quick_action_controls = [control_id for control_id in quick_action_control_ids_found if control_id not in known_control_ids]
evidence["unknownQuickActionControls"] = unknown_quick_action_controls
if unknown_quick_action_controls:
    add_failure("Section quick actions reference controls outside the legacy dialog catalog.", section_inventory_failures)

shell_catalog_text = texts.get("shell_catalog", "")
command_ids_found = re.findall(r'Command\("([^"]+)"', shell_catalog_text)
tab_ids_found = re.findall(r'Tab\("([^"]+)"', shell_catalog_text)
workspace_action_matches = re.findall(r'Action\("([^"]+)",\s*"[^"]+",\s*"([^"]+)",', shell_catalog_text)
workspace_action_ids_found = [action_id for action_id, _tab_id in workspace_action_matches]
actions_by_tab_found: dict[str, list[str]] = {}
for action_id, tab_id in workspace_action_matches:
    actions_by_tab_found.setdefault(tab_id, []).append(action_id)

evidence["commandIdsFound"] = command_ids_found
evidence["commandCount"] = len(command_ids_found)
evidence["tabIdsFound"] = tab_ids_found
evidence["tabCount"] = len(tab_ids_found)
evidence["workspaceActionIdsFound"] = workspace_action_ids_found
evidence["workspaceActionCount"] = len(workspace_action_ids_found)
evidence["actionsByTabFound"] = actions_by_tab_found

if command_ids_found != EXPECTED_COMMAND_IDS:
    add_failure("Ruleset shell command inventory drifted from the parity contract.", shell_inventory_failures)
if tab_ids_found != EXPECTED_TAB_IDS:
    add_failure("Ruleset navigation tab inventory drifted from the parity contract.", shell_inventory_failures)
if workspace_action_ids_found != EXPECTED_WORKSPACE_ACTION_IDS:
    add_failure("Ruleset workspace action inventory drifted from the parity contract.", shell_inventory_failures)
if actions_by_tab_found != EXPECTED_ACTIONS_BY_TAB:
    add_failure("Ruleset tab-to-action mapping drifted from the parity contract.", shell_inventory_failures)

for marker in SECTION_TEST_MARKERS:
    found = marker in texts.get("section_tests", "")
    evidence["sectionTests"][marker] = found
    if not found:
        add_failure(f"Section quick-action parity test marker missing: {marker}.", test_marker_failures)

for marker in SHELL_CATALOG_TEST_MARKERS:
    found = marker in texts.get("shell_catalog_tests", "")
    evidence["shellCatalogTests"][marker] = found
    if not found:
        add_failure(f"Ruleset shell catalog parity test marker missing: {marker}.", test_marker_failures)

for marker in PROJECTOR_TEST_MARKERS:
    found = marker in texts.get("projector_tests", "")
    evidence["projectorTests"][marker] = found
    if not found:
        add_failure(f"Projector parity test marker missing: {marker}.", test_marker_failures)

for marker in DIRECTIVE_TEST_MARKERS:
    found = marker in texts.get("directive_tests", "")
    evidence["directiveTests"][marker] = found
    if not found:
        add_failure(f"Ruleset directive test marker missing: {marker}.", test_marker_failures)

projector_text = texts.get("projector", "")
projector_markers = {
    "section_host_state_projection": "SectionHostState: new SectionHostState(",
    "quick_action_projection": "QuickActions: ProjectSectionQuickActions(shellSurface.ActiveRulesetId, state.ActiveSectionId),",
    "section_action_label_projection": "RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(",
}
evidence["projectorMarkers"] = {}
for name, marker in projector_markers.items():
    found = marker in projector_text
    evidence["projectorMarkers"][name] = {"marker": marker, "found": found}
    if not found:
        add_failure(f"Projector marker missing: {name}.", projector_failures)

verify_text = texts.get("verify_script", "")
verify_banner = "checking section host and ruleset parity guard"
verify_invocation = "bash scripts/ai/milestones/section-host-ruleset-parity-check.sh"
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if verify_banner not in verify_text or verify_invocation not in verify_text:
    add_failure(
        "Section-host/ruleset parity guard is not wired into scripts/ai/verify.sh.",
        verify_wiring_failures,
    )

ruleset_receipt_text = texts.get("ruleset_receipt", "")
if ruleset_receipt_text:
    ruleset_receipt = json.loads(ruleset_receipt_text)
    evidence["rulesetAdaptationStatus"] = ruleset_receipt.get("status")
    evidence["rulesetAdaptationSummary"] = ruleset_receipt.get("summary")
    if ruleset_receipt.get("status") != "pass":
        add_failure("RULESET_UI_ADAPTATION receipt is not passing.", ruleset_receipt_failures)
else:
    evidence["rulesetAdaptationStatus"] = None
    evidence["rulesetAdaptationSummary"] = None
    add_failure("RULESET_UI_ADAPTATION receipt is unavailable.", ruleset_receipt_failures)

test_commands = [
    [
        "bash",
        "scripts/ai/test.sh",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--no-build",
        "--filter",
        filter_expression,
        "-v",
        "minimal",
    ]
    for filter_expression in TEST_FILTER_COMMANDS
]
evidence["testCommands"] = test_commands
evidence["testProject"] = "Chummer.Tests/Chummer.Tests.csproj"

build_result: subprocess.CompletedProcess[str] | None = None
test_results: list[dict[str, Any]] = []
if not reasons:
    build_command = [
        "bash",
        "scripts/ai/with-package-plane.sh",
        "build",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--nologo",
        "--verbosity",
        "quiet",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    evidence["buildCommand"] = build_command

    build_result = subprocess.run(
        build_command,
        cwd=repo_root,
        text=True,
        capture_output=True,
    )
    evidence["buildExitCode"] = build_result.returncode
    evidence["buildOutputTail"] = tail_lines((build_result.stdout or "") + "\n" + (build_result.stderr or ""))
    if build_result.returncode != 0:
        add_failure(
            f"Section-host/ruleset parity build slice failed with exit code {build_result.returncode}.",
            execution_failures,
        )
    else:
        for test_command in test_commands:
            test_result = subprocess.run(
                test_command,
                cwd=repo_root,
                text=True,
                capture_output=True,
            )
            combined_output = (test_result.stdout or "") + "\n" + (test_result.stderr or "")
            output_tail = tail_lines(combined_output)
            output_lower = combined_output.lower()
            no_matches = "no test matches the given testcase filter" in output_lower
            test_results.append(
                {
                    "command": test_command,
                    "exitCode": test_result.returncode,
                    "noMatches": no_matches,
                    "outputTail": output_tail,
                }
            )
            if test_result.returncode != 0:
                add_failure(
                    f"Section-host/ruleset parity test slice failed with exit code {test_result.returncode}: {' '.join(test_command)}",
                    execution_failures,
                )
            elif no_matches:
                add_failure(
                    f"Section-host/ruleset parity test slice matched zero tests: {' '.join(test_command)}",
                    execution_failures,
                )
        evidence["testResults"] = test_results
else:
    evidence["buildExitCode"] = None
    evidence["testResults"] = test_results

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = "Section-host quick actions and ruleset-conditioned shell subflows are inventoried, projected, and fail-closing."

payload["sourceArtifactReview"] = {
    "status": "pass" if not source_artifact_failures else "fail",
    "summary": (
        "Section-host parity source files are present."
        if not source_artifact_failures
        else "Section-host parity source files are missing."
    ),
    "reasons": source_artifact_failures,
    "missingFiles": missing_files,
}
payload["sectionInventoryReview"] = {
    "status": "pass" if not section_inventory_failures else "fail",
    "summary": (
        "Section quick-action inventories and legacy control bindings match the parity contract."
        if not section_inventory_failures
        else "Section quick-action inventories or legacy control bindings drifted from the parity contract."
    ),
    "reasons": section_inventory_failures,
    "standardSectionIdsFound": standard_section_ids_found,
    "sr6AdaptedSectionIdsFound": sr6_adapted_section_ids_found,
    "quickActionControlIdsFound": quick_action_control_ids_found,
    "unknownQuickActionControls": unknown_quick_action_controls,
}
payload["shellInventoryReview"] = {
    "status": "pass" if not shell_inventory_failures else "fail",
    "summary": (
        "Ruleset shell command, tab, and workspace-action inventories match the parity contract."
        if not shell_inventory_failures
        else "Ruleset shell command, tab, or workspace-action inventories drifted from the parity contract."
    ),
    "reasons": shell_inventory_failures,
    "commandIdsFound": command_ids_found,
    "tabIdsFound": tab_ids_found,
    "workspaceActionIdsFound": workspace_action_ids_found,
    "actionsByTabFound": actions_by_tab_found,
}
payload["testMarkerReview"] = {
    "status": "pass" if not test_marker_failures else "fail",
    "summary": (
        "Section-host, shell-catalog, projector, and directive test markers are pinned."
        if not test_marker_failures
        else "One or more section-host, shell-catalog, projector, or directive test markers are missing."
    ),
    "reasons": test_marker_failures,
    "sectionTests": evidence["sectionTests"],
    "shellCatalogTests": evidence["shellCatalogTests"],
    "projectorTests": evidence["projectorTests"],
    "directiveTests": evidence["directiveTests"],
}
payload["projectorReview"] = {
    "status": "pass" if not projector_failures else "fail",
    "summary": (
        "Main-window projector markers are present for section-host parity."
        if not projector_failures
        else "Main-window projector markers are missing for section-host parity."
    ),
    "reasons": projector_failures,
    "projectorMarkers": evidence["projectorMarkers"],
}
payload["verifyWiringReview"] = {
    "status": "pass" if not verify_wiring_failures else "fail",
    "summary": (
        "Section-host parity guard is wired into the standard verify path."
        if not verify_wiring_failures
        else "Section-host parity guard is not wired into the standard verify path."
    ),
    "reasons": verify_wiring_failures,
    "wiredIntoStandardVerify": evidence["wiredIntoStandardVerify"],
    "verifyMarker": verify_banner,
    "verifyInvocation": verify_invocation,
}
payload["rulesetReceiptReview"] = {
    "status": "pass" if not ruleset_receipt_failures else "fail",
    "summary": (
        "The ruleset UI adaptation receipt is present and passing."
        if not ruleset_receipt_failures
        else "The ruleset UI adaptation receipt is missing or not passing."
    ),
    "reasons": ruleset_receipt_failures,
    "statusValue": evidence["rulesetAdaptationStatus"],
    "summaryValue": evidence["rulesetAdaptationSummary"],
}
payload["executionReview"] = {
    "status": "pass" if not execution_failures else "fail",
    "summary": (
        "Section-host parity build and test slices executed cleanly."
        if not execution_failures
        else "Section-host parity build or test slices failed."
    ),
    "reasons": execution_failures,
    "buildExitCode": evidence["buildExitCode"],
    "testResults": evidence["testResults"],
}
evidence["failureCount"] = len(reasons)
evidence["reasonCount"] = len(reasons)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if payload["status"] != "pass":
    raise SystemExit(47)

print("[section-host-ruleset] PASS: section-host quick actions and ruleset-conditioned subflows are locked and executable.")
print(f"[section-host-ruleset] evidence: {receipt_path}")
PY
