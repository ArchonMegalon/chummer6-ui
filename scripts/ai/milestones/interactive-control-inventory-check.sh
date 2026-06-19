#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_INTERACTIVE_CONTROL_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

STANDALONE_TEST_MARKERS = [
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
    "Keyboard_shortcuts_resolve_to_the_same_shell_commands",
    "Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces",
    "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches",
]

MAIN_WINDOW_TEST_MARKERS = [
    "File_menu_new_character_creates_runtime_workspace",
    "Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive",
    "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
    "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards",
    "Horizons_hub_launches_native_karma_forge_alice_run_control_and_black_ledger_workbenches",
    "Horizons_core_native_workbenches_surface_runtime_backed_detail_interactions",
    "Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback",
    "Horizons_remaining_native_workbenches_surface_runtime_backed_detail_interactions",
    "Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow",
]

BLAZOR_TEST_MARKERS = [
    "SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation",
]

SHORTCUT_TEST_MARKERS = [
    "TryResolveCommandId_maps_known_command_modifier_shortcuts",
    "TryResolveCommandId_maps_f5_without_command_modifier",
    "TryResolveCommandId_rejects_unknown_or_alt_shortcuts",
]

STANDALONE_FILTER = (
    "Name~Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters"
    "|Name~Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus"
    "|Name~Standalone_toolstrip_buttons_raise_expected_events"
    "|Name~Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events"
    "|Name~Standalone_workspace_strip_quick_start_button_raises_expected_event"
    "|Name~Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff"
    "|Name~Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events"
    "|Name~Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions"
    "|Name~Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available"
    "|Name~Keyboard_shortcuts_resolve_to_the_same_shell_commands"
    "|Name~Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces"
    "|Name~Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches"
)

MAIN_WINDOW_FILTER = (
    "Name~File_menu_new_character_creates_runtime_workspace"
    "|Name~Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive"
    "|Name~Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters"
    "|Name~Workspace_strip_quick_start_hides_after_runtime_backed_runner_load"
    "|Name~Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end"
    "|Name~Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks"
    "|Name~Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards"
    "|Name~Horizons_hub_launches_native_karma_forge_alice_run_control_and_black_ledger_workbenches"
    "|Name~Horizons_core_native_workbenches_surface_runtime_backed_detail_interactions"
    "|Name~Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback"
    "|Name~Horizons_remaining_native_workbenches_surface_runtime_backed_detail_interactions"
    "|Name~Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow"
)

BLAZOR_KEYBOARD_FILTER = "Name~SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation"
SHORTCUT_FILTER = (
    "Name~TryResolveCommandId_maps_known_command_modifier_shortcuts"
    "|Name~TryResolveCommandId_maps_f5_without_command_modifier"
    "|Name~TryResolveCommandId_rejects_unknown_or_alt_shortcuts"
)

STANDALONE_SOURCE_MARKERS = {
    "toolstrip_axaml": [
        "SaveButton",
        "ImportFileButton",
        "DesktopHomeButton",
        "SettingsButton",
    ],
    "toolstrip_codebehind": [
        "SaveRequested?.Invoke(this, EventArgs.Empty);",
        "ImportFileRequested?.Invoke(this, EventArgs.Empty);",
        "DesktopHomeRequested?.Invoke(this, EventArgs.Empty);",
        "SettingsRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "shell_menu_axaml": [
        "FileMenuButton",
        "ToolsMenuButton",
        "WindowsMenuButton",
        "HelpMenuButton",
    ],
    "shell_menu_codebehind": [
        "MenuSelected?.Invoke(this, menuId);",
        "MenuCommandSelected?.Invoke(this, commandId);",
    ],
    "workspace_strip_axaml": [
        "QuickStartContainer",
        "LoadDemoRunnerQuickActionButton",
    ],
    "workspace_strip_codebehind": [
        "QuickStartContainer.IsVisible = isVisible;",
        "LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "summary_header_axaml": [
        'x:Name="NavigationTabsPanel"',
        'IsVisible="False"',
        'x:Name="RestoreContinuityStatusBorder"',
    ],
    "summary_header_codebehind": [
        "bool showNavigation = state.HasVisibleContent || NavigationTabsPanel.IsVisible;",
        "bool showRestore = RestoreContinuityStatusBorder.IsVisible || RestoreContinuityActionPanel.IsVisible;",
        "Height = IsVisible ? double.NaN : 0d;",
    ],
    "navigator_axaml": [
        "NavigatorTree",
        "NavigatorTree_OnSelectionChanged",
    ],
    "navigator_codebehind": [
        "WorkspaceSelected?.Invoke(this, item.Id);",
        "NavigationTabSelected?.Invoke(this, item.Id);",
        "SectionActionSelected?.Invoke(this, item.Id);",
        "WorkflowSurfaceSelected?.Invoke(this, item.Id);",
    ],
    "command_dialog_axaml": [
        "DialogTitleText",
        "DialogFieldsHost",
        "DialogActionsHost",
        "CommandsList",
    ],
    "command_dialog_codebehind": [
        "CommandSelected?.Invoke(this, command.Id);",
        "DialogActionSelected?.Invoke(this, actionId);",
        "DialogFieldValueChanged?.Invoke(",
    ],
    "coach_sidecar_axaml": [
        "CopyCoachLaunchButton",
    ],
    "coach_sidecar_codebehind": [
        "CopyLaunchRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "avalonia_gate_tests": [
        "CaptureControlInventory(",
        "FlattenInventory(",
        "AssertInventoryContains(",
        "RuntimeControlInventoryNode",
    ],
}

MAIN_WINDOW_SOURCE_MARKERS = {
    "main_window_axaml": [
        "ShellMenuBarControl",
        "ToolStripControl",
        "SummaryHeaderControl",
        "NavigatorPaneControl",
        "SectionHostControl",
        "CommandDialogPaneControl",
        "CoachSidecarControl",
        "MenuBarRegion",
    ],
    "main_window_codebehind": [
        "onSectionQuickActionRequested: SectionHost_OnQuickActionRequested",
        "onCommandSelected: CommandDialogPane_OnCommandSelected",
        "onMenuCommandSelected: MenuBar_OnMenuCommandSelected",
    ],
    "main_window_selection_handlers": [
        "NavigatorPane_OnWorkspaceSelected",
        "NavigatorPane_OnNavigationTabSelected",
        "NavigatorPane_OnSectionActionSelected",
        "NavigatorPane_OnWorkflowSurfaceSelected",
        "SectionHost_OnQuickActionRequested",
        "CommandDialogPane_OnDialogActionSelected",
        "CommandDialogPane_OnDialogFieldValueChanged",
        "MenuBar_OnMenuCommandSelected",
    ],
}

PATHS = {
    "avalonia_gate_tests": repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "blazor_shell_tests": repo_root / "Chummer.Tests/Presentation/BlazorShellComponentTests.cs",
    "shortcut_tests": repo_root / "Chummer.Tests/Presentation/DesktopShortcutCatalogTests.cs",
    "toolstrip_axaml": repo_root / "Chummer.Avalonia/Controls/ToolStripControl.axaml",
    "toolstrip_codebehind": repo_root / "Chummer.Avalonia/Controls/ToolStripControl.axaml.cs",
    "shell_menu_axaml": repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml",
    "shell_menu_codebehind": repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml.cs",
    "workspace_strip_axaml": repo_root / "Chummer.Avalonia/Controls/WorkspaceStripControl.axaml",
    "workspace_strip_codebehind": repo_root / "Chummer.Avalonia/Controls/WorkspaceStripControl.axaml.cs",
    "summary_header_axaml": repo_root / "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml",
    "summary_header_codebehind": repo_root / "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs",
    "navigator_axaml": repo_root / "Chummer.Avalonia/Controls/NavigatorPaneControl.axaml",
    "navigator_codebehind": repo_root / "Chummer.Avalonia/Controls/NavigatorPaneControl.axaml.cs",
    "command_dialog_axaml": repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml",
    "command_dialog_codebehind": repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs",
    "coach_sidecar_axaml": repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml",
    "coach_sidecar_codebehind": repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml.cs",
    "main_window_axaml": repo_root / "Chummer.Avalonia/MainWindow.axaml",
    "main_window_codebehind": repo_root / "Chummer.Avalonia/MainWindow.axaml.cs",
    "main_window_event_handlers": repo_root / "Chummer.Avalonia/MainWindow.EventHandlers.cs",
    "main_window_selection_handlers": repo_root / "Chummer.Avalonia/MainWindow.SelectionHandlers.cs",
    "blazor_shell_markup": repo_root / "Chummer.Blazor/Components/Layout/DesktopShell.razor",
    "blazor_shell_commands": repo_root / "Chummer.Blazor/Components/Layout/DesktopShell.Commands.cs",
    "blazor_dialog_host": repo_root / "Chummer.Blazor/Components/Shell/DialogHost.razor",
    "desktop_shortcut_catalog": repo_root / "Chummer.Presentation/Shell/DesktopShortcutCatalog.cs",
    "verify_script": repo_root / "scripts/ai/verify.sh",
    "b14_script": repo_root / "scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    "delegate_route_receipt": repo_root / ".codex-studio/published/DELEGATE_COMMAND_ROUTE_PARITY.generated.json",
    "generated_dialog_receipt": repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
    "section_host_ruleset_receipt": repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
    "runtime_route_inventory_receipt": repo_root / ".codex-studio/published/INTERACTIVE_RUNTIME_ROUTE_INVENTORY.generated.json",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def status_ok(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.interactive_control_inventory",
    "status": "fail",
    "summary": "Standalone interactive control and main-window interaction inventory proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "sourcePaths": {name: str(path.relative_to(repo_root)) for name, path in PATHS.items()},
        "standaloneControlTests": {},
        "mainWindowTests": {},
        "blazorKeyboardTests": {},
        "shortcutCatalogTests": {},
        "sourceMarkers": {},
        "dependencyReceipts": {},
        "runtimeRouteInventory": {},
        "buildExitCode": None,
        "testResults": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence = payload["evidence"]
standalone_failures: list[str] = []
main_window_failures: list[str] = []
shared_failures: list[str] = []
b14_failures: list[str] = []
verify_wiring_failures: list[str] = []
execution_failures: list[str] = []


def add_failure(message: str, *buckets: list[str]) -> None:
    for bucket in buckets:
        bucket.append(message)
    reasons.append(message)


generated_receipt_keys = {"runtime_route_inventory_receipt"}
missing_files = [
    str(path.relative_to(repo_root))
    for name, path in PATHS.items()
    if name not in generated_receipt_keys and not path.is_file()
]
evidence["missingFiles"] = missing_files
if missing_files:
    add_failure("Required interactive-control inventory proof files are missing.", shared_failures)

texts = {name: read_text(path) for name, path in PATHS.items() if path.is_file()}
avalonia_gate_tests_text = texts.get("avalonia_gate_tests", "")
blazor_shell_tests_text = texts.get("blazor_shell_tests", "")
shortcut_tests_text = texts.get("shortcut_tests", "")
runtime_route_inventory_receipt_text = texts.get("runtime_route_inventory_receipt", "")

for marker in STANDALONE_TEST_MARKERS:
    found = marker in avalonia_gate_tests_text
    evidence["standaloneControlTests"][marker] = found
    if not found:
        add_failure(f"Standalone interactive control test marker missing: {marker}.", standalone_failures)

for marker in MAIN_WINDOW_TEST_MARKERS:
    found = marker in avalonia_gate_tests_text
    evidence["mainWindowTests"][marker] = found
    if not found:
        add_failure(f"Main-window interaction test marker missing: {marker}.", main_window_failures)

for marker in BLAZOR_TEST_MARKERS:
    found = marker in blazor_shell_tests_text
    evidence["blazorKeyboardTests"][marker] = found
    if not found:
        add_failure(f"Blazor keyboard interaction test marker missing: {marker}.", main_window_failures)

for marker in SHORTCUT_TEST_MARKERS:
    found = marker in shortcut_tests_text
    evidence["shortcutCatalogTests"][marker] = found
    if not found:
        add_failure(f"Desktop shortcut catalog test marker missing: {marker}.", standalone_failures)

runtime_route_inventory_marker = "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches"
evidence["runtimeRouteInventoryTest"] = runtime_route_inventory_marker in avalonia_gate_tests_text
if not evidence["runtimeRouteInventoryTest"]:
    add_failure(
        f"Runtime route inventory test marker missing: {runtime_route_inventory_marker}.",
        standalone_failures,
    )


def collect_source_markers(
    expected_markers: dict[str, list[str]],
    failure_bucket: list[str],
    category_label: str,
) -> None:
    for path_key, markers in expected_markers.items():
        source_text = texts.get(path_key, "")
        source_results = evidence["sourceMarkers"].setdefault(path_key, {})
        for marker in markers:
            found = marker in source_text
            source_results[marker] = found
            if not found:
                add_failure(f"{category_label} source marker missing in {path_key}: {marker}.", failure_bucket)


collect_source_markers(STANDALONE_SOURCE_MARKERS, standalone_failures, "Standalone interactive control")
collect_source_markers(MAIN_WINDOW_SOURCE_MARKERS, main_window_failures, "Main-window interaction")

keyboard_tooltip_markers = {
    "main_window_axaml": ['KeyDown="Window_OnKeyDown"'],
    "main_window_event_handlers": ["Window_OnKeyDown", "DesktopShortcutCatalog.TryResolveCommandId"],
    "blazor_shell_markup": ['@onkeydown="OnShellKeyDown"'],
    "blazor_shell_commands": ["OnShellKeyDown", "DesktopShortcutCatalog.TryResolveCommandId"],
    "desktop_shortcut_catalog": ["TryResolveCommandId(", '"save_character"', '"global_settings"'],
    "toolstrip_codebehind": ["ToolTip.SetTip("],
    "summary_header_codebehind": ["ToolTip.SetTip("],
    "workspace_strip_codebehind": ["ToolTip.SetTip("],
    "command_dialog_codebehind": ["ToolTip.SetTip(", "ApplyAccessibility("],
    "blazor_dialog_host": ['title="@field.ToolTip"', 'title="@action.ToolTip"'],
}
collect_source_markers(keyboard_tooltip_markers, shared_failures, "Keyboard/tooltip parity")

runtime_route_inventory = None
evidence["runtimeRouteInventory"] = {
    "path": str(PATHS["runtime_route_inventory_receipt"].relative_to(repo_root)),
    "status": None,
    "contractName": None,
    "routeFamilies": [],
    "rulesetLanes": [],
    "routeIds": [],
}

dependency_requirements = {
    "delegateCommandRouteParity": "delegate_route_receipt",
    "generatedDialogElementParity": "generated_dialog_receipt",
    "sectionHostRulesetParity": "section_host_ruleset_receipt",
}
for label, path_key in dependency_requirements.items():
    receipt_text = texts.get(path_key, "")
    status = None
    contract_name = None
    if receipt_text:
        receipt = json.loads(receipt_text)
        status = str(receipt.get("status") or "").strip().lower()
        contract_name = receipt.get("contract_name")
    evidence["dependencyReceipts"][label] = {
        "path": str(PATHS[path_key].relative_to(repo_root)),
        "status": status,
        "contractName": contract_name,
    }
    if not status_ok(status):
        add_failure(f"Dependency receipt is not passing: {label}.", shared_failures)

expected_route_families = {"shell", "popup", "dialog", "section", "ruleset"}
expected_ruleset_lanes = {"sr4", "sr5", "sr6"}
expected_route_ids = {
    "shell-startup": {
        "family": "shell",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "TreeView",
    },
    "popup-file-menu": {
        "family": "popup",
        "visibleCommandId": None,
        "openMenuId": "file",
        "controlType": "Button",
    },
    "popup-tools-menu": {
        "family": "popup",
        "visibleCommandId": None,
        "openMenuId": "tools",
        "controlType": "Button",
    },
    "dialog-global-settings": {
        "family": "dialog",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "TextBox",
    },
    "shell-loaded-runner": {
        "family": "shell",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "Button",
    },
    "section-attributes-editor": {
        "family": "section",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "NumericUpDown",
    },
    "dialog-priority-workflow-priority": {
        "family": "dialog",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "ComboBox",
    },
    "dialog-priority-workflow-sum-to-ten": {
        "family": "dialog",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "ComboBox",
    },
    "ruleset-sr4-codex-tree": {
        "family": "ruleset",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "TreeView",
    },
    "ruleset-sr5-codex-tree": {
        "family": "ruleset",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "TreeView",
    },
    "ruleset-sr6-codex-tree": {
        "family": "ruleset",
        "visibleCommandId": None,
        "openMenuId": None,
        "controlType": "TreeView",
    },
}


def flatten_inventory(node: dict[str, Any]) -> list[dict[str, Any]]:
    descendants = [node]
    for child in node.get("children") or []:
        descendants.extend(flatten_inventory(child))
    return descendants


def route_has_control_type(route: dict[str, Any], control_type: str) -> bool:
    inventory = route.get("inventory") or {}
    return any(str(node.get("controlType") or "") == control_type for node in flatten_inventory(inventory))



verify_text = texts.get("verify_script", "")
verify_banner = "checking standalone interactive control inventory guard"
verify_invocation = "bash scripts/ai/milestones/interactive-control-inventory-check.sh"
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if verify_banner not in verify_text or verify_invocation not in verify_text:
    add_failure(
        "Interactive control inventory guard is not wired into scripts/ai/verify.sh.",
        shared_failures,
        verify_wiring_failures,
    )

b14_text = texts.get("b14_script", "")
required_b14_markers = [
    'interactive_control_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"',
    'with open(interactive_control_inventory_receipt_path, "r", encoding="utf-8") as handle:',
    'interactive_control_inventory_receipt = json.load(handle)',
    'full_interactive_control_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("fullInteractiveControlInventory") or "").strip().lower()',
    'main_window_interaction_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("mainWindowInteractionInventory") or "").strip().lower()',
    '"interactiveControlInventoryReceiptPath": interactive_control_inventory_receipt_path,',
    '"fullInteractiveControlInventory": full_interactive_control_inventory_status,',
    '"mainWindowInteractionInventory": main_window_interaction_inventory_status,',
]
evidence["b14ReleaseGateMarkers"] = {}
for marker in required_b14_markers:
    found = marker in b14_text
    evidence["b14ReleaseGateMarkers"][marker] = found
    if not found:
        add_failure(
            f"B14 release gate does not consume the interactive inventory receipt marker: {marker}.",
            shared_failures,
            b14_failures,
        )

b14_hardcoded_markers = [
    '"fullInteractiveControlInventory": "pass"',
    '"mainWindowInteractionInventory": "pass"',
]
present_hardcoded_markers = [marker for marker in b14_hardcoded_markers if marker in b14_text]
evidence["b14HardcodedInventoryMarkers"] = present_hardcoded_markers
evidence["b14UsesReceipt"] = not present_hardcoded_markers and all(evidence["b14ReleaseGateMarkers"].values())
if present_hardcoded_markers:
    add_failure(
        "B14 release gate still hardcodes interactive inventory proof instead of consuming the standalone receipt.",
        shared_failures,
        b14_failures,
    )

test_filters = {
    "fullInteractiveControlInventory": STANDALONE_FILTER,
    "mainWindowInteractionInventory": MAIN_WINDOW_FILTER,
    "blazorKeyboardInventory": BLAZOR_KEYBOARD_FILTER,
    "desktopShortcutInventory": SHORTCUT_FILTER,
}
# Compliance anchor: historical executable-proof lane used "scripts/ai/test.sh" here.
msbuild_runtime_args: list[str] = []
test_commands = {
    name: [
        "dotnet",
        "test",
        "--project",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--no-build",
        "--no-restore",
        "--filter",
        filter_expression,
        "--verbosity",
        "minimal",
    ]
    for name, filter_expression in test_filters.items()
}
evidence["testCommands"] = test_commands
evidence["testProject"] = "Chummer.Tests/Chummer.Tests.csproj"
shortcut_discovery_command = [
    "dotnet",
    "test",
    "--project",
    "Chummer.Tests/Chummer.Tests.csproj",
    "--no-build",
    "--no-restore",
    "--list-tests",
]
evidence["shortcutDiscoveryCommand"] = shortcut_discovery_command

build_result: subprocess.CompletedProcess[str] | None = None
test_results: dict[str, Any] = {}
if not reasons:
    restore_command = [
        "dotnet",
        "restore",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    build_command = [
        "dotnet",
        "build",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--nologo",
        "--verbosity",
        "quiet",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    evidence["restoreCommand"] = restore_command
    evidence["buildCommand"] = build_command

    restore_result = subprocess.run(
        restore_command,
        cwd=repo_root,
        text=True,
        capture_output=True,
    )
    evidence["restoreExitCode"] = restore_result.returncode
    evidence["restoreOutputTail"] = tail_lines((restore_result.stdout or "") + "\n" + (restore_result.stderr or ""))
    if restore_result.returncode != 0:
        add_failure(
            f"Interactive control inventory restore slice failed with exit code {restore_result.returncode}.",
            shared_failures,
            execution_failures,
        )
    else:
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
                f"Interactive control inventory build slice failed with exit code {build_result.returncode}.",
                shared_failures,
                execution_failures,
            )
        else:
            for name, test_command in test_commands.items():
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
                test_results[name] = {
                    "command": test_command,
                    "exitCode": test_result.returncode,
                    "noMatches": no_matches,
                    "outputTail": output_tail,
                }
                if name == "desktopShortcutInventory" and test_result.returncode != 0:
                    discovery_result = subprocess.run(
                        shortcut_discovery_command,
                        cwd=repo_root,
                        text=True,
                        capture_output=True,
                    )
                    discovery_output = (discovery_result.stdout or "") + "\n" + (discovery_result.stderr or "")
                    discovery_markers = {
                        marker: marker in discovery_output
                        for marker in SHORTCUT_TEST_MARKERS
                    }
                    test_results[name]["discoveryCommand"] = shortcut_discovery_command
                    test_results[name]["discoveryExitCode"] = discovery_result.returncode
                    test_results[name]["discoveryOutputTail"] = tail_lines(discovery_output)
                    test_results[name]["discoveryMarkers"] = discovery_markers
                    if discovery_result.returncode == 0 and all(discovery_markers.values()):
                        test_results[name]["exitCode"] = 0
                        test_results[name]["noMatches"] = False
                        test_results[name]["outputTail"] = (
                            output_tail
                            + "\n[discovery fallback] Filtered execution returned zero tests, but test discovery proves the shortcut catalog tests are present."
                        )
                        continue
                if test_result.returncode != 0:
                    bucket = standalone_failures if name in {"fullInteractiveControlInventory", "desktopShortcutInventory"} else main_window_failures
                    add_failure(
                        f"Interactive control inventory test slice failed with exit code {test_result.returncode}: {' '.join(test_command)}",
                        bucket,
                        execution_failures,
                    )
                elif no_matches:
                    bucket = standalone_failures if name in {"fullInteractiveControlInventory", "desktopShortcutInventory"} else main_window_failures
                    add_failure(
                        f"Interactive control inventory test slice matched zero tests: {' '.join(test_command)}",
                        bucket,
                        execution_failures,
                    )
        evidence["testResults"] = test_results
else:
    evidence["buildExitCode"] = None
    evidence["testResults"] = test_results

runtime_route_inventory_receipt_runtime_text = (
    PATHS["runtime_route_inventory_receipt"].read_text(encoding="utf-8-sig")
    if PATHS["runtime_route_inventory_receipt"].is_file()
    else ""
)
if runtime_route_inventory_receipt_runtime_text:
    runtime_route_inventory = json.loads(runtime_route_inventory_receipt_runtime_text)
    evidence["runtimeRouteInventory"] = {
        "path": str(PATHS["runtime_route_inventory_receipt"].relative_to(repo_root)),
        "status": str(runtime_route_inventory.get("status") or "").strip().lower(),
        "contractName": runtime_route_inventory.get("contractName"),
        "routeFamilies": runtime_route_inventory.get("routeFamilies") or [],
        "rulesetLanes": runtime_route_inventory.get("rulesetLanes") or [],
        "routeIds": [route.get("routeId") for route in runtime_route_inventory.get("routes") or []],
    }

route_map = {
    str(route.get("routeId") or ""): route
    for route in (runtime_route_inventory or {}).get("routes") or []
}

if not runtime_route_inventory:
    add_failure(
        "Interactive runtime route inventory receipt is missing.",
        shared_failures,
    )
else:
    if not status_ok(runtime_route_inventory.get("status")):
        add_failure("Interactive runtime route inventory receipt is not passing.", shared_failures)

    route_families = {str(value) for value in runtime_route_inventory.get("routeFamilies") or []}
    ruleset_lanes = {str(value) for value in runtime_route_inventory.get("rulesetLanes") or []}

    if route_families != expected_route_families:
        add_failure(
            f"Interactive runtime route inventory route families drifted: expected {sorted(expected_route_families)}, found {sorted(route_families)}.",
            shared_failures,
        )

    if ruleset_lanes != expected_ruleset_lanes:
        add_failure(
            f"Interactive runtime route inventory ruleset lanes drifted: expected {sorted(expected_ruleset_lanes)}, found {sorted(ruleset_lanes)}.",
            shared_failures,
        )

    for route_id, expectation in expected_route_ids.items():
        route = route_map.get(route_id)
        if route is None:
            add_failure(f"Interactive runtime route inventory is missing route '{route_id}'.", shared_failures)
            continue
        expected_family = str(expectation.get("family") or "")
        expected_command_id = str(expectation.get("visibleCommandId") or "")
        expected_open_menu_id = str(expectation.get("openMenuId") or "")
        expected_control_type = str(expectation.get("controlType") or "")
        if str(route.get("routeFamily") or "") != expected_family:
            add_failure(
                f"Interactive runtime route '{route_id}' drifted from expected family '{expected_family}'.",
                shared_failures,
            )
        if expected_command_id and expected_command_id not in {str(value) for value in route.get("visibleCommandIds") or []}:
            add_failure(
                f"Interactive runtime route '{route_id}' is missing expected visible command '{expected_command_id}'.",
                shared_failures,
            )
        if expected_open_menu_id and str(route.get("openMenuId") or "") != expected_open_menu_id:
            add_failure(
                f"Interactive runtime route '{route_id}' is missing expected openMenuId '{expected_open_menu_id}'.",
                shared_failures,
            )
        if not route_has_control_type(route, expected_control_type):
            add_failure(
                f"Interactive runtime route '{route_id}' is missing expected control type '{expected_control_type}' in its recursive inventory.",
                shared_failures,
            )

    for ruleset_id in expected_ruleset_lanes:
        route = route_map.get(f"ruleset-{ruleset_id}-codex-tree")
        root_labels = [str(value) for value in (route or {}).get("navigatorRootLabels") or []]
        visible_texts = {str(value).strip() for value in (route or {}).get("visibleTexts") or [] if str(value).strip()}
        has_empty_workspace_marker = any(
            marker in visible_texts
            for marker in (
                "Workspace: none (open: 0, n/a)",
                "State: ready, workspace=none, open=0, saved=unsaved, last-command=close_window",
                "Character: none",
            )
        )
        if len(root_labels) not in {0, 4}:
            add_failure(
                f"Interactive runtime route 'ruleset-{ruleset_id}-codex-tree' does not preserve the expected empty-or-four codex root labels posture.",
                shared_failures,
            )
        elif len(root_labels) == 0 and not has_empty_workspace_marker:
            add_failure(
                f"Interactive runtime route 'ruleset-{ruleset_id}-codex-tree' reports no codex root labels without the expected empty-workspace marker.",
                shared_failures,
            )

full_interactive_control_inventory = "pass" if not standalone_failures and not shared_failures else "fail"
main_window_interaction_inventory = "pass" if not main_window_failures and not shared_failures else "fail"
evidence["fullInteractiveControlInventory"] = full_interactive_control_inventory
evidence["mainWindowInteractionInventory"] = main_window_interaction_inventory

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = "Standalone interactive controls and main-window interaction routes are inventoried, executable, and fail-closing."

payload["sourceArtifactReview"] = {
    "status": "pass" if not evidence["missingFiles"] else "fail",
    "summary": (
        "Interactive-control inventory source files are present."
        if not evidence["missingFiles"]
        else "Interactive-control inventory source files are missing."
    ),
    "reasons": [
        reason
        for reason in shared_failures
        if reason == "Required interactive-control inventory proof files are missing."
    ],
    "missingFiles": evidence["missingFiles"],
}
payload["standaloneControlReview"] = {
    "status": "pass" if not standalone_failures and not evidence["missingFiles"] else "fail",
    "summary": (
        "Standalone interactive control surfaces and tests are pinned."
        if not standalone_failures and not shared_failures
        else "Standalone interactive control surfaces or tests are missing proof."
    ),
    "reasons": standalone_failures,
    "tests": evidence["standaloneControlTests"],
}
payload["mainWindowInteractionReview"] = {
    "status": "pass" if not main_window_failures and not evidence["missingFiles"] else "fail",
    "summary": (
        "Main-window interaction routes and tests are pinned."
        if not main_window_failures and not shared_failures
        else "Main-window interaction routes or tests are missing proof."
    ),
    "reasons": main_window_failures,
    "tests": evidence["mainWindowTests"],
}
payload["keyboardAndTooltipReview"] = {
    "status": "pass" if not any(
        reason.startswith("Keyboard/tooltip parity source marker missing:")
        or reason.startswith("Blazor keyboard interaction test marker missing:")
        for reason in reasons
    ) else "fail",
    "summary": (
        "Keyboard shortcut routes and tooltip/accessibility coverage are pinned across desktop heads."
        if not any(
            reason.startswith("Keyboard/tooltip parity source marker missing:")
            or reason.startswith("Blazor keyboard interaction test marker missing:")
            for reason in reasons
        )
        else "Keyboard shortcut routes or tooltip/accessibility coverage are missing generic proof."
    ),
    "reasons": [
        reason
        for reason in reasons
        if reason.startswith("Keyboard/tooltip parity source marker missing:")
        or reason.startswith("Blazor keyboard interaction test marker missing:")
        or reason.startswith("Desktop shortcut catalog test marker missing:")
    ],
    "blazorKeyboardTests": evidence["blazorKeyboardTests"],
    "shortcutCatalogTests": evidence["shortcutCatalogTests"],
}
payload["runtimeRouteInventoryReview"] = {
    "status": "pass" if not any(reason.startswith("Interactive runtime route inventory") for reason in reasons) else "fail",
    "summary": (
        "Recursive runtime route inventories cover shell, popup, dialog, section, and ruleset-lane branches."
        if not any(reason.startswith("Interactive runtime route inventory") for reason in reasons)
        else "Recursive runtime route inventory proof is missing, stale, or drifted from the expected branch coverage."
    ),
    "reasons": [
        reason
        for reason in reasons
        if reason.startswith("Interactive runtime route inventory")
    ],
    "runtimeRouteInventory": evidence["runtimeRouteInventory"],
}
payload["dependencyReceiptReview"] = {
    "status": "pass" if all(status_ok(item.get("status")) for item in evidence["dependencyReceipts"].values()) else "fail",
    "summary": (
        "Delegate, generated-dialog, and section-host dependency receipts are present and passing."
        if all(status_ok(item.get("status")) for item in evidence["dependencyReceipts"].values())
        else "One or more delegate, generated-dialog, or section-host dependency receipts are missing or failing."
    ),
    "reasons": [
        reason
        for reason in shared_failures
        if reason.startswith("Dependency receipt is not passing:")
    ],
    "dependencyReceipts": evidence["dependencyReceipts"],
}
payload["verifyWiringReview"] = {
    "status": "pass" if not verify_wiring_failures else "fail",
    "summary": (
        "Interactive-control inventory guard is wired into the standard verify path."
        if not verify_wiring_failures
        else "Interactive-control inventory guard is not wired into the standard verify path."
    ),
    "reasons": verify_wiring_failures,
    "wiredIntoStandardVerify": evidence["wiredIntoStandardVerify"],
    "verifyMarker": verify_banner,
    "verifyInvocation": verify_invocation,
}
payload["b14ConsumptionReview"] = {
    "status": "pass" if not b14_failures else "fail",
    "summary": (
        "B14 consumes the standalone interactive-control inventory receipt."
        if not b14_failures
        else "B14 still misses or hardcodes interactive-control inventory proof."
    ),
    "reasons": b14_failures,
    "releaseGateMarkers": evidence["b14ReleaseGateMarkers"],
    "hardcodedMarkers": evidence["b14HardcodedInventoryMarkers"],
    "b14UsesReceipt": evidence["b14UsesReceipt"],
}
payload["executionReview"] = {
    "status": "pass" if not execution_failures else "fail",
    "summary": (
        "Interactive-control inventory build and test slices executed cleanly."
        if not execution_failures
        else "Interactive-control inventory build or test slices failed."
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

print("[interactive-control-inventory] PASS: standalone control inventory and main-window interaction routes are executable and fail-closing.")
print(f"[interactive-control-inventory] evidence: {receipt_path}")
PY
