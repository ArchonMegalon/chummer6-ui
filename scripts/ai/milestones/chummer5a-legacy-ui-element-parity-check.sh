#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

legacy_ui_parity_subject="${LEGACY_UI_PARITY_SUBJECT:-Chummer5a}"
legacy_ui_parity_subject_slug="${LEGACY_UI_PARITY_SUBJECT_SLUG:-chummer5a}"
legacy_ui_parity_script_label="${LEGACY_UI_PARITY_SCRIPT_LABEL:-chummer5a-legacy-ui-element-parity}"
legacy_ui_parity_legacy_roots="${LEGACY_UI_PARITY_LEGACY_ROOTS:-}"
legacy_ui_parity_verify_banner="${LEGACY_UI_PARITY_VERIFY_BANNER:-checking Chummer5a legacy UI element parity guard}"
legacy_ui_parity_verify_invocation="${LEGACY_UI_PARITY_VERIFY_INVOCATION:-bash scripts/ai/milestones/chummer5a-legacy-ui-element-parity-check.sh}"
legacy_ui_parity_b14_markers="${LEGACY_UI_PARITY_B14_MARKERS:-CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json|chummer5a-legacy-ui-element-parity-check.sh|chummer5a_legacy_ui_element_parity_receipt_path}"
legacy_ui_parity_contract_name="${LEGACY_UI_PARITY_CONTRACT_NAME:-chummer6-ui.chummer5a_legacy_ui_element_parity}"

receipt_path="${LEGACY_UI_PARITY_RECEIPT_PATH:-${CHUMMER5A_LEGACY_UI_ELEMENT_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json}}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' \
  "$repo_root" \
  "$receipt_path" \
  "$legacy_ui_parity_subject" \
  "$legacy_ui_parity_subject_slug" \
  "$legacy_ui_parity_script_label" \
  "$legacy_ui_parity_legacy_roots" \
  "$legacy_ui_parity_verify_banner" \
  "$legacy_ui_parity_verify_invocation" \
  "$legacy_ui_parity_b14_markers" \
  "$legacy_ui_parity_contract_name"
from __future__ import annotations

import json
import re
import subprocess
import sys
import os
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
legacy_subject = sys.argv[3]
legacy_subject_slug = sys.argv[4]
script_label = sys.argv[5]
legacy_roots_arg = sys.argv[6]
verify_banner_override = sys.argv[7]
verify_invocation_override = sys.argv[8]
b14_markers_override = sys.argv[9]
contract_name = sys.argv[10]
reuse_existing_test_build = str(
    os.environ.get("CHUMMER_LEGACY_UI_PARITY_REUSE_EXISTING_TEST_BUILD") or "1"
).strip().lower() in {"1", "true", "yes", "on"}


def resolve_path(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else repo_root / path


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(repo_root))
    except ValueError:
        return str(path)

EVENT_NAMES = (
    "Click|CheckedChanged|SelectedIndexChanged|SelectedValueChanged|TextChanged|TextUpdate|"
    "ValueChanged|KeyDown|KeyUp|KeyPress|MouseDown|MouseUp|MouseClick|DoubleClick|"
    "AfterSelect|NodeMouseClick|NodeMouseDoubleClick|SelectionChanged|SelectionChangeCommitted|"
    "ItemCheck|AfterCheck|BeforeCheck|ItemAdded|ItemRemoved|Opening|LinkClicked|"
    "DocumentCompleted|Enter|Leave|Resize|Layout|VisibleChanged|EnabledChanged|SizeChanged|"
    "DragDrop|DragEnter|DragOver|ItemDrag|ColumnClick|ControlStateChanged"
)
DESIGNER_EVENT_RE = re.compile(
    r"this\.(?P<control>[A-Za-z_]\w*)\.(?P<event>"
    + EVENT_NAMES
    + r")\s*\+=\s*new\s+[A-Za-z0-9_.<>]+\((?:this\.)?(?P<handler>[A-Za-z_]\w*)\)",
    re.MULTILINE,
)
RUNTIME_EVENT_RE = re.compile(
    r"(?P<target>\b[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?)\.(?P<event>"
    + EVENT_NAMES
    + r")\s*\+=\s*(?P<handler>[^;]+);",
    re.MULTILINE,
)
DYNAMIC_INTERACTIVE_RE = re.compile(
    r"new\s+(?P<type>(?:System\.Windows\.Forms\.)?"
    r"(?:Button|ToolStripMenuItem|ToolStripButton|ToolStripSplitButton|SplitButton|"
    r"CheckBox|ComboBox|TextBox|ListBox|TreeView|NumericUpDown|NumericUpDownEx|"
    r"DataGridView|TabControl|TabPage|RadioButton|LinkLabel|ContextMenuStrip|MenuStrip|ToolStrip)"
    r")\b"
)
AVALONIA_DYNAMIC_INTERACTIVE_RE = re.compile(
    r"new\s+(?P<type>"
    r"(?:Button|MenuItem|CheckBox|ComboBox|TextBox|ListBox|TreeView|TabControl|TabItem|"
    r"ToggleButton|RadioButton|NumericUpDown|ContextMenu|Menu|TabStrip|Expander)"
    r")\b"
)
AVALONIA_TARGET_TYPED_INTERACTIVE_RE = re.compile(
    r"\b(?P<type>Button|MenuItem|CheckBox|ComboBox|TextBox|ListBox|TreeView|TabControl|TabItem|"
    r"ToggleButton|RadioButton|NumericUpDown|ContextMenu|Menu|TabStrip|Expander)"
    r"\s+[A-Za-z_]\w*\s*=\s*new\s*\("
)
AVALONIA_NAMED_AXAML_RE = re.compile(
    r"<(?P<type>Button|MenuItem|CheckBox|ComboBox|TextBox|ListBox|TreeView|TabControl|"
    r"ToggleButton|RadioButton|TabStrip|Expander)\b[^>]*(?:x:Name|Name)=\"(?P<name>[^\"]+)\"",
    re.MULTILINE,
)

LEGACY_SOURCE_ROOTS = (
    [resolve_path(root.strip()) for root in legacy_roots_arg.split("|") if root.strip()]
    if legacy_roots_arg.strip()
    else [
        repo_root / "Chummer" / "Forms",
        repo_root / "Chummer" / "Controls",
    ]
)
CURRENT_SOURCE_ROOTS = [
    repo_root / "Chummer.Avalonia",
    repo_root / "Chummer.Presentation",
]
SOURCE_PATHS = {
    **{f"legacyRoot{index + 1}": path for index, path in enumerate(LEGACY_SOURCE_ROOTS)},
    "catalogResolver": repo_root / "Chummer.Presentation" / "Shell" / "CatalogOnlyRulesetShellCatalogResolver.cs",
    "legacyUiControlCatalog": repo_root / "Chummer.Presentation" / "Overview" / "LegacyUiControlCatalog.cs",
    "sectionQuickActionCatalog": repo_root / "Chummer.Presentation" / "Rulesets" / "SectionQuickActionCatalog.cs",
    "desktopDialogFactory": repo_root / "Chummer.Presentation" / "Overview" / "DesktopDialogFactory.cs",
    "desktopDialogWindow": repo_root / "Chummer.Avalonia" / "DesktopDialogWindow.axaml.cs",
    "avaloniaGateTests": repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs",
    "dualHeadTests": repo_root / "Chummer.Tests" / "Presentation" / "DualHeadAcceptanceTests.cs",
    "desktopDialogFactoryTests": repo_root / "Chummer.Tests" / "Presentation" / "DesktopDialogFactoryTests.cs",
    "desktopInstallLinkingTests": repo_root / "Chummer.Tests" / "Presentation" / "DesktopInstallLinkingShellChromeTests.cs",
    "presenterTests": repo_root / "Chummer.Tests" / "Presentation" / "CharacterOverviewPresenterTests.cs",
    "verifyScript": repo_root / "scripts" / "ai" / "verify.sh",
    "b14Script": repo_root / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh",
}

PROOF_TEST_MARKERS = [
    "ExecuteCommandAsync_all_catalog_commands_are_handled",
    "HandleUiControlAsync_all_catalog_controls_are_non_generic",
    "CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions",
    "CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions",
    "RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions",
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_workspace_action_summary_matches",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "File_menu_new_character_creates_runtime_workspace",
    "File_menu_new_character_completes_into_visible_runtime_workspace",
    "Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome",
    "Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head",
    "Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces",
    "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
]

PROOF_FILTER = (
    "Name~ExecuteCommandAsync_all_catalog_commands_are_handled"
    "|Name~HandleUiControlAsync_all_catalog_controls_are_non_generic"
    "|Name~CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"
    "|Name~CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"
    "|Name~RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions"
    "|Name~Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections"
    "|Name~Avalonia_and_Blazor_workspace_action_summary_matches"
    "|Name~Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts"
    "|Name~File_menu_new_character_creates_runtime_workspace"
    "|Name~File_menu_new_character_completes_into_visible_runtime_workspace"
    "|Name~Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome"
    "|Name~Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"
    "|Name~Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces"
    "|Name~Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches"
    "|Name~Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end"
)

PARITY_FAMILIES: dict[str, dict[str, list[str]]] = {
    "file_new": {
        "currentIds": ["command:new_character", "command:new_critter"],
        "proofMarkers": ["File_menu_new_character_creates_runtime_workspace", "ExecuteCommandAsync_all_catalog_commands_are_handled"],
    },
    "file_open": {
        "currentIds": ["command:open_character", "command:open_for_printing", "command:open_for_export"],
        "proofMarkers": ["Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts"],
    },
    "file_save": {
        "currentIds": ["command:save_character", "command:save_character_as"],
        "proofMarkers": ["ExecuteCommandAsync_all_catalog_commands_are_handled"],
    },
    "file_print_export": {
        "currentIds": ["command:print_character", "command:export_character", "command:print_multiple", "command:print_setup"],
        "proofMarkers": ["CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "file_exit": {
        "currentIds": ["command:exit"],
        "proofMarkers": ["ExecuteCommandAsync_all_catalog_commands_are_handled"],
    },
    "window_management": {
        "currentIds": ["command:new_window", "command:close_window", "command:close_all"],
        "proofMarkers": ["ExecuteCommandAsync_all_catalog_commands_are_handled"],
    },
    "help_external": {
        "currentIds": ["command:wiki", "command:discord", "command:revision_history", "command:dumpshock", "command:about", "command:report_bug"],
        "proofMarkers": ["CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "tools_utilities": {
        "currentIds": ["command:dice_roller", "command:master_index", "command:character_roster", "command:global_settings", "command:character_settings"],
        "proofMarkers": ["CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "settings_global": {
        "currentIds": ["command:global_settings"],
        "proofMarkers": ["Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome", "Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
    },
    "settings_character": {
        "currentIds": ["command:character_settings"],
        "proofMarkers": ["CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "character_creation": {
        "currentIds": ["command:new_character", "action:tab-info.attributes", "action:tab-skills.skills"],
        "proofMarkers": ["File_menu_new_character_completes_into_visible_runtime_workspace", "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections"],
    },
    "selection_dialog": {
        "currentIds": ["ui:gear_add", "ui:combat_add_weapon", "ui:combat_add_armor", "ui:cyberware_add"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions"],
    },
    "search_filter_category": {
        "currentIds": ["ui:gear_add", "ui:combat_add_weapon", "ui:combat_add_armor", "command:master_index"],
        "proofMarkers": ["RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions", "Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces"],
    },
    "browse_select": {
        "currentIds": ["action:tab-gear.inventory", "action:tab-combat.weapons", "action:tab-contacts.contacts"],
        "proofMarkers": ["Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections", "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end"],
    },
    "field_update": {
        "currentIds": ["command:global_settings", "ui:create_entry", "ui:edit_entry"],
        "proofMarkers": ["Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome", "RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions"],
    },
    "toggle_state": {
        "currentIds": ["ui:toggle_free_paid", "command:global_settings"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome"],
    },
    "confirm_submit": {
        "currentIds": ["dialog-action:ok", "dialog-action:apply", "dialog-action:save_global_settings", "dialog-action:create_character"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "cancel_close": {
        "currentIds": ["dialog-action:cancel", "dialog-action:close"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions"],
    },
    "source_reference": {
        "currentIds": ["ui:show_source", "ui:gear_source", "ui:magic_source"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"],
    },
    "notes": {
        "currentIds": ["ui:open_notes", "action:tab-notes.metadata"],
        "proofMarkers": ["Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections", "CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"],
    },
    "add_create": {
        "currentIds": ["ui:create_entry", "ui:gear_add", "ui:quality_add"],
        "proofMarkers": ["HandleUiControlAsync_all_catalog_controls_are_non_generic"],
    },
    "edit_update": {
        "currentIds": ["ui:edit_entry", "ui:gear_edit", "ui:contact_edit"],
        "proofMarkers": ["HandleUiControlAsync_all_catalog_controls_are_non_generic"],
    },
    "delete_remove": {
        "currentIds": ["ui:delete_entry", "ui:gear_delete", "ui:quality_delete"],
        "proofMarkers": ["HandleUiControlAsync_all_catalog_controls_are_non_generic"],
    },
    "inventory_progression": {
        "currentIds": ["ui:gear_add", "ui:gear_edit", "ui:gear_delete", "ui:create_entry", "action:tab-gear.inventory"],
        "proofMarkers": ["Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections", "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches"],
    },
    "combat": {
        "currentIds": ["ui:combat_add_weapon", "ui:combat_add_armor", "ui:combat_reload", "ui:combat_damage_track"],
        "proofMarkers": ["Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections", "CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"],
    },
    "vehicles": {
        "currentIds": ["ui:vehicle_add", "ui:vehicle_edit", "ui:vehicle_delete", "ui:vehicle_mod_add"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"],
    },
    "contacts": {
        "currentIds": ["ui:contact_add", "ui:contact_edit", "ui:contact_remove", "ui:contact_connection"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions"],
    },
    "skills": {
        "currentIds": ["ui:skill_add", "ui:skill_specialize", "ui:skill_remove", "ui:skill_group", "action:tab-skills.skills"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections"],
    },
    "qualities": {
        "currentIds": ["ui:quality_add", "ui:quality_delete", "action:tab-qualities.qualities"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections"],
    },
    "magic_matrix": {
        "currentIds": ["ui:spell_add", "ui:adept_power_add", "ui:complex_form_add", "ui:spirit_add", "ui:matrix_program_add"],
        "proofMarkers": ["CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions", "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections"],
    },
    "session_dashboard": {
        "currentIds": ["command:dice_roller", "command:character_roster", "action:tab-info.summary"],
        "proofMarkers": ["CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions", "Avalonia_and_Blazor_workspace_action_summary_matches"],
    },
}

COMMAND_COUNTERPART_RULES: list[tuple[list[str], list[str]]] = [
    (["shownewform", "newcharacter", "tsbnewcharacter", "newtoolstripmenuitem"], ["command:new_character"]),
    (["newcritter", "mnuNewCritter".lower()], ["command:new_critter"]),
    (["openfile", "opencharacter", "openToolStripMenuItem".lower(), "tsbopen"], ["command:open_character"]),
    (["openforprinting", "tsbopenforprinting"], ["command:open_for_printing"]),
    (["openforexport", "tsbopenforexport"], ["command:open_for_export"]),
    (["mru", "stickymru"], ["command:open_character"]),
    (["saveas", "tssaveas"], ["command:save_character_as"]),
    (["save", "tssave", "mnufilesave"], ["command:save_character"]),
    (["printmultiple"], ["command:print_multiple"]),
    (["printsetup"], ["command:print_setup"]),
    (["print", "tsprint", "charactersheetviewer"], ["command:print_character"]),
    (["export"], ["command:export_character"]),
    (["copy", "tsbcopy", "menueditcopy"], ["command:copy"]),
    (["exit"], ["command:exit"]),
    (["newwindow"], ["command:new_window"]),
    (["closeall"], ["command:close_all"]),
    (["closewindow", "fileclose", "tsclose"], ["command:close_window"]),
    (["diceroller"], ["command:dice_roller"]),
    (["globalsettings"], ["command:global_settings"]),
    (["charactersettings", "editcharactersettings", "selectsetting"], ["command:character_settings"]),
    (["toolsupdate", "chummerupdater"], ["command:update"]),
    (["restart"], ["command:restart"]),
    (["translator"], ["command:translator"]),
    (["xmleditor", "editxmldata"], ["command:xml_editor"]),
    (["herolab"], ["command:hero_lab_importer"]),
    (["masterindex"], ["command:master_index"]),
    (["omae", "mnutoolsomae"], ["command:master_index"]),
    (["characterroster"], ["command:character_roster"]),
    (["dataexporter"], ["command:data_exporter"]),
    (["reportbug"], ["command:report_bug"]),
    (["wiki"], ["command:wiki"]),
    (["discord"], ["command:discord"]),
    (["revisionhistory", "versionhistory"], ["command:revision_history"]),
    (["dumpshock"], ["command:dumpshock"]),
    (["about"], ["command:about"]),
]

FORM_COUNTERPART_RULES: list[tuple[list[str], list[str]]] = [
    (["selectcyberware", "createcyberwaresuite"], ["ui:cyberware_add"]),
    (["selectgear"], ["ui:gear_add"]),
    (["selectarmor", "selectarmormod"], ["ui:combat_add_armor"]),
    (["selectweapon", "selectweaponaccessory", "selectweaponcategory", "createweaponmount"], ["ui:combat_add_weapon"]),
    (["selectvehicle", "selectvehiclemod"], ["ui:vehicle_add", "ui:vehicle_mod_add"]),
    (["selectquality"], ["ui:quality_add"]),
    (["selectspell", "createspell"], ["ui:spell_add"]),
    (["selectpower", "selectoptionalpower"], ["ui:adept_power_add"]),
    (["selectcomplexform"], ["ui:complex_form_add"]),
    (["selectcritterpower"], ["ui:critter_power_add"]),
    (["selectdrug", "createcustomdrug"], ["ui:drug_add"]),
    (["selectskillgroup"], ["ui:skill_group"]),
    (["selectskillspec"], ["ui:skill_specialize"]),
    (["selectexoticskill", "selectskill", "selectskillcategory"], ["ui:skill_add"]),
    (["selectmetamagic", "selectart", "selectmartialart", "selectmartialarttechnique", "selectmentorspirit"], ["ui:initiation_add"]),
    (["selectlifestyle", "selectlifestylequality", "selectlifestylestartingnuyen"], ["ui:create_entry"]),
    (["selectcalendarstart", "createexpense"], ["ui:create_entry"]),
    (["selectdicehits", "selectnumber"], ["command:dice_roller"]),
    (["selectlimit", "selectlimitmodifier", "selectattribute"], ["action:tab-info.attributes"]),
    (["selecttext", "editnotes", "rtfeditor"], ["ui:open_notes"]),
    (["reloadweapon"], ["ui:combat_reload"]),
    (["sellitem"], ["ui:gear_delete"]),
]

SOURCE_COUNTERPART_RULES: list[tuple[list[str], list[str]]] = [
    (["dpifriendlytoolstripbutton"], ["current-dynamic:Button"]),
    (["dpifriendlytoolstripmenuitem", "splitbutton"], ["current-dynamic:MenuItem", "current-dynamic:ContextMenu"]),
    (["attributecontrol"], ["action:tab-info.attributes"]),
    (["contactcontrol"], ["ui:contact_edit", "ui:contact_connection", "ui:contact_remove"]),
    (["petcontrol"], ["ui:contact_edit", "ui:contact_remove"]),
    (["spiritcontrol"], ["ui:spirit_add", "ui:magic_bind"]),
    (["skillstabusercontrol", "skillcontrol", "knowledgeskillcontrol"], ["action:tab-skills.skills", "ui:skill_add", "ui:skill_specialize", "ui:skill_remove"]),
    (["skillgroupcontrol"], ["ui:skill_group"]),
    (["powerstabusercontrol"], ["ui:adept_power_add"]),
    (["powercontrol"], ["ui:adept_power_add", "ui:initiation_add", "ui:edit_entry", "ui:delete_entry", "ui:toggle_free_paid"]),
    (["conditionmonitorusercontrol"], ["ui:combat_damage_track"]),
    (["initiativeusercontrol"], ["command:dice_roller", "action:tab-info.summary"]),
    (["dicepoolcontrol"], ["command:dice_roller"]),
    (["limittabusercontrol"], ["action:tab-info.attributes"]),
    (["sustainedobjectcontrol"], ["ui:spell_add", "ui:complex_form_add", "ui:spirit_add"]),
    (["bindinglistdisplay", "observablecollectiondisplay", "tableview", "tablerow", "tablecell", "headercell", "buttontablecell", "texttablecell"], ["action:tab-gear.inventory", "action:tab-combat.weapons", "action:tab-contacts.contacts"]),
    (["charactercareer"], ["action:tab-info.profile", "action:tab-info.summary", "action:tab-gear.inventory", "action:tab-combat.weapons", "action:tab-contacts.contacts"]),
    (["charactercreate"], ["command:new_character", "action:tab-info.attributes", "action:tab-skills.skills"]),
    (["selectlifemodule", "selectmetatypepriority", "selectmetatypekarma"], ["command:new_character", "action:tab-info.attributes", "action:tab-skills.skills"]),
    (["chummermainform"], ["command:new_character", "command:open_character", "command:save_character", "command:print_character"]),
    (["editglobalsettings"], ["command:global_settings"]),
    (["editcharactersettings"], ["command:character_settings"]),
    (["editnotes", "rtfeditor"], ["ui:open_notes", "action:tab-notes.metadata"]),
    (["desktopinstalllinkinggateform"], ["Windows_install_link_gate_copy_stays_fail_closed_until_user_links_in_browser"]),
    (["masterindex"], ["command:master_index"]),
    (["characterroster"], ["command:character_roster"]),
    (["diceroller", "initiativeroller"], ["command:dice_roller"]),
    (["gamemasterdashboard", "playerdashboard"], ["command:dice_roller", "command:character_roster", "action:tab-info.summary"]),
    (["exportcharacter", "printmultiplecharacters", "charactersheetviewer"], ["command:print_character", "command:export_character"]),
    (["scrollablemessagebox"], ["dialog-action:ok", "dialog-action:cancel", "dialog-action:close"]),
    (["selectpackskit", "createpackskit"], ["ui:gear_add"]),
    (["selectprogramoption"], ["ui:matrix_program_add"]),
    (["testdataentries"], ["command:xml_editor"]),
    (["omaerecord"], ["command:master_index", "ui:delete_entry"]),
    (["frmomae"], ["command:master_index", "ui:create_entry", "ui:edit_entry"]),
    (["frmomaeaccount"], ["command:master_index", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmomaecompress"], ["command:master_index", "ui:create_entry"]),
    (["frmomaeupload"], ["command:master_index", "ui:create_entry", "action:tab-gear.inventory"]),
    (["frmomaeuploadlanguage"], ["command:master_index", "ui:create_entry"]),
    (["frmomaeuploadsheet"], ["command:master_index", "ui:create_entry"]),
    (["frmmetatype"], ["command:new_character", "action:tab-info.profile", "action:tab-info.attributes", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmoptions"], ["command:global_settings", "command:character_settings", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectbp"], ["command:new_character", "action:tab-info.attributes", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectitem"], ["ui:create_entry", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectnexus"], ["ui:gear_add", "ui:matrix_program_add", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectnumber"], ["command:dice_roller", "ui:create_entry", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectpackskit"], ["ui:gear_add", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectprogram"], ["ui:matrix_program_add", "command:master_index", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectprogramoption"], ["ui:matrix_program_add", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmselectside"], ["ui:create_entry", "dialog-action:ok"]),
    (["frmselecttext"], ["ui:open_notes", "dialog-action:ok", "dialog-action:cancel"]),
    (["frmnaturalweapon"], ["ui:combat_add_weapon", "ui:edit_entry"]),
    (["frmviewer"], ["command:print_character", "command:export_character"]),
    (["frmtest"], ["command:xml_editor", "current-dynamic:Button", "current-dynamic:TextBox"]),
    (["frmcareer"], [
        "action:tab-info.profile",
        "action:tab-info.summary",
        "action:tab-info.attributes",
        "action:tab-skills.skills",
        "action:tab-qualities.qualities",
        "action:tab-magician.spells",
        "action:tab-gear.inventory",
        "action:tab-combat.weapons",
        "action:tab-contacts.contacts",
        "action:tab-notes.metadata",
        "ui:create_entry",
        "ui:edit_entry",
        "ui:delete_entry",
        "ui:open_notes",
        "ui:toggle_free_paid",
        "ui:gear_add",
        "ui:gear_edit",
        "ui:gear_delete",
        "ui:combat_add_weapon",
        "ui:combat_add_armor",
        "ui:combat_reload",
        "ui:combat_damage_track",
        "ui:vehicle_add",
        "ui:vehicle_edit",
        "ui:vehicle_delete",
        "ui:vehicle_mod_add",
        "ui:contact_add",
        "ui:contact_edit",
        "ui:contact_remove",
        "ui:quality_add",
        "ui:quality_delete",
        "ui:spell_add",
        "ui:adept_power_add",
        "ui:complex_form_add",
        "ui:spirit_add",
        "command:copy",
        "command:save_character",
        "command:print_character",
        "command:export_character",
    ]),
    (["frmcreate"], [
        "command:new_character",
        "action:tab-info.profile",
        "action:tab-info.attributes",
        "action:tab-skills.skills",
        "action:tab-qualities.qualities",
        "action:tab-magician.spells",
        "action:tab-gear.inventory",
        "ui:create_entry",
        "ui:edit_entry",
        "ui:delete_entry",
        "ui:toggle_free_paid",
        "ui:gear_add",
        "ui:quality_add",
        "ui:quality_delete",
        "ui:skill_add",
        "ui:skill_specialize",
        "ui:skill_group",
        "ui:spell_add",
        "ui:adept_power_add",
        "ui:complex_form_add",
        "ui:spirit_add",
    ]),
    (["frmmain"], [
        "command:new_character",
        "command:new_critter",
        "command:open_character",
        "command:save_character",
        "command:print_character",
        "command:export_character",
        "command:copy",
        "command:dice_roller",
        "command:global_settings",
        "command:character_settings",
        "command:update",
        "command:master_index",
        "command:new_window",
        "command:close_window",
        "command:close_all",
    ]),
]

TOKEN_COUNTERPART_RULES: list[tuple[list[str], list[str]]] = [
    (["cancel"], ["dialog-action:cancel"]),
    (["cmdok", "btnok", "okclick", "okadd"], ["dialog-action:ok"]),
    (["source"], ["ui:show_source", "ui:gear_source", "ui:magic_source"]),
    (["note"], ["ui:open_notes", "action:tab-notes.metadata"]),
    (["copy", "tsbcopy", "menueditcopy"], ["command:copy"]),
    (["omae", "mnutoolsomae"], ["command:master_index"]),
    (["bolthole", "safehouse", "advancedlifestyle", "lifestyle"], ["ui:create_entry", "action:tab-gear.inventory"]),
    (["reload"], ["ui:combat_reload"]),
    (["damage", "stuncm", "physicalcm", "condition"], ["ui:combat_damage_track"]),
    (["addweapon", "createweapon", "weaponadd"], ["ui:combat_add_weapon"]),
    (["addarmor", "armoradd"], ["ui:combat_add_armor"]),
    (["addgear", "gearadd"], ["ui:gear_add"]),
    (["editgear", "gearname", "renamegear"], ["ui:gear_edit"]),
    (["deletegear", "removegear"], ["ui:gear_delete"]),
    (["addcyberware", "cyberwareadd"], ["ui:cyberware_add"]),
    (["editcyberware"], ["ui:cyberware_edit"]),
    (["deletecyberware", "removecyberware"], ["ui:cyberware_delete"]),
    (["adddrug"], ["ui:drug_add"]),
    (["deletedrug"], ["ui:drug_delete"]),
    (["addvehicle", "vehicleadd"], ["ui:vehicle_add"]),
    (["editvehicle"], ["ui:vehicle_edit"]),
    (["deletevehicle", "removevehicle"], ["ui:vehicle_delete"]),
    (["vehiclemod", "addmod"], ["ui:vehicle_mod_add"]),
    (["addcontact", "addenemy", "addpet"], ["ui:contact_add"]),
    (["editcontact", "connection"], ["ui:contact_edit", "ui:contact_connection"]),
    (["removecontact", "deletecontact"], ["ui:contact_remove"]),
    (["addquality"], ["ui:quality_add"]),
    (["deletequality", "removequality"], ["ui:quality_delete"]),
    (["addskill"], ["ui:skill_add"]),
    (["specializ"], ["ui:skill_specialize"]),
    (["removeskill", "deleteskill"], ["ui:skill_remove"]),
    (["skillgroup"], ["ui:skill_group"]),
    (["createspell", "addspell", "spelladd"], ["ui:spell_add"]),
    (["complexform"], ["ui:complex_form_add"]),
    (["addspirit", "spiritadd"], ["ui:spirit_add"]),
    (["critterpower"], ["ui:critter_power_add"]),
    (["aiprogram", "matrixprogram"], ["ui:matrix_program_add"]),
    (["poweradd", "adeptpower"], ["ui:adept_power_add"]),
    (["initiation", "metamagic", "martialart", "mentor", "cyberzombie", "possess", "cloning"], ["ui:initiation_add"]),
    (["karma", "nuyen", "expense", "calendar", "week", "improvement", "improve", "streetcred", "edgegained", "edgespent", "burnedge"], ["ui:create_entry", "ui:edit_entry", "ui:delete_entry", "action:tab-info.attributes"]),
    (["freeitem", "freepaid"], ["ui:toggle_free_paid"]),
    (["up"], ["ui:move_up"]),
    (["down"], ["ui:move_down"]),
]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def normalize_token(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())


def contains_token(haystack: str, token: str) -> bool:
    return normalize_token(token) in haystack


def add_unique(target: list[str], values: list[str]) -> None:
    for value in values:
        if value not in target:
            target.append(value)


def classify_legacy_behavior(path: str, control: str, event: str, handler: str, kind: str = "") -> str | None:
    blob = " ".join([path, control, event, handler, kind]).lower()
    path_lower = path.lower()

    def has(*words: str) -> bool:
        return any(word in blob for word in words)

    if "editglobalsettings" in path_lower:
        if has("cancel"):
            return "cancel_close"
        if has("cmdok", " ok_click"):
            return "confirm_submit"
        return "settings_global"
    if "editcharactersettings" in path_lower or "selectsetting" in path_lower:
        if has("cancel"):
            return "cancel_close"
        if has("cmdok", " ok_click"):
            return "confirm_submit"
        return "settings_character"
    if "selectbuildmethod" in path_lower or "charactercreate" in path_lower or "character creation forms" in path_lower:
        if has("cancel"):
            return "cancel_close"
        if has("cmdok", " ok_click", "okadd"):
            return "confirm_submit"
        return "character_creation"
    if "selection forms" in path_lower or "creation forms" in path_lower or "reloadweapon" in path_lower or "sellitem" in path_lower:
        if has("cancel", "close"):
            return "cancel_close"
        if has("cmdok", "btnok", " ok_click", "okadd", "accept"):
            return "confirm_submit"
        if has("source"):
            return "source_reference"
        if has(
            "search",
            "filter",
            "category",
            "refresh",
            "rating",
            "grade",
            "markup",
            "cost",
            "capacity",
            "essence",
            "avail",
            "sort",
            "cbo",
            "combo",
            "nud",
            "numeric",
            "txt",
            "textchanged",
            "selectedindexchanged",
            "valuechanged",
            "checkedchanged",
        ):
            return "search_filter_category"
        if has("tree", "tre", "lst", "list", "doubleclick", "mousedown", "keydown", "keyup", "drag", "drop", "itemdrag", "columnclick", "afterselect", "node"):
            return "browse_select"
        if has("add", "create", "new"):
            return "add_create"
        if has("delete", "remove"):
            return "delete_remove"
        if has("edit", "rename", "change"):
            return "edit_update"
        return "selection_dialog"
    if "rtfeditor" in path_lower or "editnotes" in path_lower:
        if has("cancel", "close"):
            return "cancel_close"
        if has("cmdok", "ok"):
            return "confirm_submit"
        return "notes"
    if "dashboard" in path_lower or "initiative" in path_lower:
        return "session_dashboard"
    if has("shownewform", "newcharacter", "newcritter", "mnuNewCritter".lower(), "tsbnew"):
        return "file_new"
    if has("openfile", "open_character", "openforprinting", "openforexport", "tsbopen", "import", "mru"):
        return "file_open"
    if has("save", "saveas", "mnuFileSave".lower(), "tssave"):
        return "file_save"
    if has("print", "export", "charactersheetviewer", "webviewer", "documentcompleted"):
        return "file_print_export"
    if has("exit"):
        return "file_exit"
    if has("newwindow", "closewindow", "closeall", "tabforms", "fileclose", "tsclose"):
        return "window_management"
    if has("wiki", "discord", "revision", "dumpshock", "about", "reportbug", "support", "forcecrash"):
        return "help_external"
    if has("website", "browser", "portal", "openwebsite", "linkbutton"):
        return "help_external"
    if has("dice", "updat", "translator", "xmleditor", "herolab", "masterindex", "characterroster", "dataexporter", "globalsettings", "charactersettings", "roster", "cmdtest"):
        return "tools_utilities"
    if has("cmdok", "btnok", "okclick", "okadd", "accept"):
        return "confirm_submit"
    if has("cancel", "close"):
        return "cancel_close"
    if has("rolldrain", "rollfading", "roll"):
        return "tools_utilities"
    if has("omae", "download", "upload", "login", "register", "password", "account", "compress"):
        return "tools_utilities"
    if has("pdf", "verify", "restoredefaults", "language", "setting", "options"):
        return "settings_global"
    if has("bolthole", "safehouse", "lifestyle"):
        return "inventory_progression"
    if has("copy"):
        return "edit_update"
    if has("browse", "location", "path"):
        return "browse_select"
    if has("source"):
        return "source_reference"
    if has("note"):
        return "notes"
    if has("enemy", "enemies"):
        return "contacts"
    if has("search", "filter", "category", "refresh", "rating", "grade", "markup", "cost", "capacity", "essence", "avail", "cbo", "combo", "nud", "numeric", "txt", "textchanged", "selectedindexchanged", "valuechanged"):
        return "search_filter_category"
    if has("improve", "cmdimprove", "burnedge", "edgegained", "edgespent", "burnstreetcred", "streetcred"):
        return "inventory_progression"
    if has("tree", "tre", "lst", "list", "doubleclick", "mousedown", "keydown", "keyup", "drag", "drop", "itemdrag", "columnclick", "afterselect", "node"):
        return "browse_select"
    if has("attribute", "pnlattributes", "layout"):
        return "character_creation"
    if has("reload", "ammo", "damage", "stun", "physicalcm", "stuncm", "condition", "armor", "weapon", "combat", "initiative"):
        return "combat"
    if has("vehicle", "drone"):
        return "vehicles"
    if has("contact", "pet", "connection"):
        return "contacts"
    if has("skill", "specializ", "group"):
        return "skills"
    if has("quality"):
        return "qualities"
    if has("spell", "spirit", "mentor", "power", "adept", "magic", "complexform", "initiation", "metamagic", "art", "critter", "special", "cyberzombie", "possess", "cloning"):
        return "magic_matrix"
    if "controls/table" in path_lower or "splitbutton" in path_lower or has("onbuttonclick", "lbltext_click", "splitmenustrip_opening"):
        return "browse_select"
    if has("evtbuttonclickevent"):
        return "selection_dialog"
    if has("gear", "armor", "weapon", "lifestyle", "cyberware", "bioware", "drug", "improvement", "expense", "calendar", "week", "nuyen", "karma", "mugshot"):
        return "inventory_progression"
    if has("add", "create", "new"):
        return "add_create"
    if has("delete", "remove"):
        return "delete_remove"
    if has("edit", "rename", "change"):
        return "edit_update"
    if has("check", "checked", "toggle", "free", "paid", "active", "installed", "enabled", "visible", "collapsed"):
        return "toggle_state"
    if event.lower() in {"checkedchanged", "selectedindexchanged", "selectedvaluechanged", "textchanged", "textupdate", "valuechanged", "selectedvaluechanged", "controlstatechanged"}:
        return "field_update"
    return None


def all_source_files(roots: list[Path], suffix: str = ".cs") -> list[Path]:
    files: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        files.extend(
            path
            for path in root.rglob(f"*{suffix}")
            if "/bin/" not in path.as_posix()
            and "/obj/" not in path.as_posix()
            and not path.name.endswith(".g.cs")
        )
    return sorted(files)


payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": contract_name,
    "status": "fail",
    "summary": f"{legacy_subject} legacy UI element parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "legacySubject": legacy_subject,
        "legacySubjectSlug": legacy_subject_slug,
        "sourcePaths": {key: display_path(path) for key, path in SOURCE_PATHS.items()},
        "legacyDesignerEventHookCount": 0,
        "legacyRuntimeEventHookCount": 0,
        "legacyDynamicInteractiveElementCount": 0,
        "currentDynamicInteractiveElementCount": 0,
        "currentNamedAxamlInteractiveElementCount": 0,
        "legacyBehaviorFamilyCounts": {},
        "legacyDynamicBehaviorFamilyCounts": {},
        "currentInventoryCounts": {},
        "familyReviews": {},
        "proofMarkers": {},
        "wiredIntoStandardVerify": False,
        "b14ConsumesReceipt": False,
        "buildExitCode": None,
        "buildOutputTail": [],
        "testResult": {},
    },
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]


def add_reason(message: str) -> None:
    if message not in reasons:
        reasons.append(message)


missing_files = [display_path(path) for path in SOURCE_PATHS.values() if not path.exists()]
evidence["missingFiles"] = missing_files
if missing_files:
    add_reason("Required legacy UI element parity source files are missing: " + ", ".join(missing_files))

texts = {key: read_text(path) for key, path in SOURCE_PATHS.items() if path.exists() and path.is_file()}
test_corpus = "\n".join(
    texts.get(key, "")
    for key in ("avaloniaGateTests", "dualHeadTests", "desktopDialogFactoryTests", "desktopInstallLinkingTests", "presenterTests")
)

legacy_events: list[dict[str, Any]] = []
legacy_dynamic_elements: list[dict[str, Any]] = []
for path in all_source_files(LEGACY_SOURCE_ROOTS):
    relative = display_path(path)
    text = read_text(path)
    if path.name.endswith(".Designer.cs"):
        for match in DESIGNER_EVENT_RE.finditer(text):
            line_number = text.count("\n", 0, match.start()) + 1
            family = classify_legacy_behavior(
                relative,
                match.group("control"),
                match.group("event"),
                match.group("handler"),
                "designer-event",
            )
            legacy_events.append(
                {
                    "source": relative,
                    "line": line_number,
                    "control": match.group("control"),
                    "event": match.group("event"),
                    "handler": match.group("handler"),
                    "family": family,
                    "kind": "designer-event",
                }
            )
    else:
        for match in RUNTIME_EVENT_RE.finditer(text):
            line_number = text.count("\n", 0, match.start()) + 1
            family = classify_legacy_behavior(
                relative,
                match.group("target"),
                match.group("event"),
                match.group("handler"),
                "runtime-event",
            )
            legacy_events.append(
                {
                    "source": relative,
                    "line": line_number,
                    "control": match.group("target"),
                    "event": match.group("event"),
                    "handler": " ".join(match.group("handler").split())[:180],
                    "family": family,
                    "kind": "runtime-event",
                }
            )
    for match in DYNAMIC_INTERACTIVE_RE.finditer(text):
        start = match.start()
        line_number = text.count("\n", 0, start) + 1
        window = text[max(0, start - 240): min(len(text), start + 240)]
        family = classify_legacy_behavior(relative, match.group("type"), "new", window, "dynamic-element")
        legacy_dynamic_elements.append(
            {
                "source": relative,
                "line": line_number,
                "type": match.group("type"),
                "context": " ".join(window.split())[:300],
                "family": family,
                "kind": "dynamic-element",
            }
        )

designer_events = [event for event in legacy_events if event["kind"] == "designer-event"]
runtime_events = [event for event in legacy_events if event["kind"] == "runtime-event"]
unclassified_events = [event for event in legacy_events if not event.get("family")]
unclassified_dynamic = [element for element in legacy_dynamic_elements if not element.get("family")]

family_counts = Counter(str(event.get("family")) for event in legacy_events if event.get("family"))
dynamic_family_counts = Counter(str(element.get("family")) for element in legacy_dynamic_elements if element.get("family"))
observed_families = sorted(set(family_counts) | set(dynamic_family_counts))

evidence["legacyDesignerEventHookCount"] = len(designer_events)
evidence["legacyRuntimeEventHookCount"] = len(runtime_events)
evidence["legacyDynamicInteractiveElementCount"] = len(legacy_dynamic_elements)
evidence["legacyBehaviorFamilyCounts"] = dict(sorted(family_counts.items()))
evidence["legacyDynamicBehaviorFamilyCounts"] = dict(sorted(dynamic_family_counts.items()))
evidence["legacyEventSamples"] = legacy_events[:40]
evidence["legacyDynamicElementSamples"] = legacy_dynamic_elements[:20]
evidence["unclassifiedLegacyEvents"] = unclassified_events[:50]
evidence["unclassifiedLegacyDynamicElements"] = unclassified_dynamic[:50]

if not legacy_events:
    add_reason(f"No {legacy_subject} legacy event hooks were found; the gate is not inspecting the legacy UI.")
if unclassified_events:
    add_reason(
        f"Unclassified {legacy_subject} legacy event hooks remain: "
        + ", ".join(
            f"{item['source']}::{item['control']}.{item['event']}->{item['handler']}"
            for item in unclassified_events[:12]
        )
    )
if unclassified_dynamic:
    add_reason(
        f"Unclassified dynamically created {legacy_subject} interactive elements remain: "
        + ", ".join(f"{item['source']}:{item['line']} {item['type']}" for item in unclassified_dynamic[:12])
    )

current_commands = set(re.findall(r'Command\("([^"]+)"', texts.get("catalogResolver", "")))
current_actions = set(re.findall(r'Action\("([^"]+)"', texts.get("catalogResolver", "")))
current_ui_controls = set(re.findall(r'"([a-z0-9_]+)"', texts.get("legacyUiControlCatalog", "")))
current_quick_controls = set(re.findall(r'PrimaryOnly\("([^"]+)"', texts.get("sectionQuickActionCatalog", "")))
current_ui_controls.update(current_quick_controls)
current_dialog_actions = set(re.findall(r'DesktopDialogAction\("([^"]+)"', texts.get("desktopDialogFactory", "")))
current_dialog_actions.update(re.findall(r'new\("([^"]+)",\s*"[^"]*",\s*(?:true|false)\)', texts.get("desktopDialogFactory", "")))
current_inventory = {
    "command": current_commands,
    "action": current_actions,
    "ui": current_ui_controls,
    "dialog-action": current_dialog_actions,
}

current_dynamic_elements: list[dict[str, Any]] = []
current_named_axaml_elements: list[dict[str, Any]] = []
for path in all_source_files(CURRENT_SOURCE_ROOTS):
    relative = display_path(path)
    text = read_text(path)
    for match in AVALONIA_DYNAMIC_INTERACTIVE_RE.finditer(text):
        current_dynamic_elements.append(
            {
                "source": relative,
                "line": text.count("\n", 0, match.start()) + 1,
                "type": match.group("type"),
            }
        )
    for match in AVALONIA_TARGET_TYPED_INTERACTIVE_RE.finditer(text):
        current_dynamic_elements.append(
            {
                "source": relative,
                "line": text.count("\n", 0, match.start()) + 1,
                "type": match.group("type"),
            }
        )
for root in CURRENT_SOURCE_ROOTS:
    if not root.exists():
        continue
    for path in root.rglob("*.axaml"):
        if "/bin/" in path.as_posix() or "/obj/" in path.as_posix():
            continue
        text = read_text(path)
        for match in AVALONIA_NAMED_AXAML_RE.finditer(text):
            current_named_axaml_elements.append(
                {
                    "source": display_path(path),
                    "type": match.group("type"),
                    "name": match.group("name"),
                }
            )
evidence["currentDynamicInteractiveElementCount"] = len(current_dynamic_elements)
evidence["currentNamedAxamlInteractiveElementCount"] = len(current_named_axaml_elements)
evidence["currentDynamicElementSamples"] = current_dynamic_elements[:30]
evidence["currentNamedAxamlElementSamples"] = current_named_axaml_elements[:30]
current_inventory["current-dynamic"] = {str(item["type"]) for item in current_dynamic_elements}
current_inventory["current-named"] = {str(item["name"]) for item in current_named_axaml_elements}
evidence["currentInventoryCounts"] = {key: len(value) for key, value in current_inventory.items()}

if legacy_dynamic_elements and not current_dynamic_elements:
    add_reason("Legacy dynamic interactive elements were found, but no current dynamic Avalonia interactive elements were detected.")
if legacy_dynamic_elements and "Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces" not in test_corpus:
    add_reason("Dynamic generated dialog controls are not pinned by Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces.")

proof_marker_results: dict[str, bool] = {}
for marker in PROOF_TEST_MARKERS:
    found = marker in test_corpus
    proof_marker_results[marker] = found
    if not found:
        add_reason(f"Legacy UI element parity proof marker is missing: {marker}.")
evidence["proofMarkers"] = proof_marker_results


def current_id_available(current_id: str) -> bool:
    if ":" not in current_id:
        return current_id in test_corpus
    prefix, value = current_id.split(":", 1)
    if prefix in current_inventory:
        return value in current_inventory[prefix]
    return current_id in test_corpus


def legacy_element_id(item: dict[str, Any]) -> str:
    source = str(item.get("source") or "")
    line = str(item.get("line") or "0")
    kind = str(item.get("kind") or "")
    control = str(item.get("control") or item.get("type") or "")
    event = str(item.get("event") or "new")
    handler = str(item.get("handler") or "")
    return f"{source}:{line}:{kind}:{control}.{event}->{handler}"


def resolve_legacy_element_counterparts(item: dict[str, Any]) -> tuple[list[str], list[str]]:
    family = str(item.get("family") or "")
    source_token = normalize_token(str(item.get("source") or ""))
    control_token = normalize_token(str(item.get("control") or item.get("type") or ""))
    event_token = normalize_token(str(item.get("event") or ""))
    handler_token = normalize_token(str(item.get("handler") or ""))
    context_token = normalize_token(str(item.get("context") or ""))
    blob = source_token + control_token + event_token + handler_token + context_token
    current_ids: list[str] = []
    strategies: list[str] = []

    def apply_rules(
        rules: list[tuple[list[str], list[str]]],
        strategy_prefix: str,
        *,
        match_path_only: bool = False,
    ) -> None:
        haystack = source_token if match_path_only else blob
        for tokens, ids in rules:
            if any(contains_token(haystack, token) for token in tokens):
                add_unique(current_ids, ids)
                strategies.append(strategy_prefix + ":" + "+".join(tokens[:3]))

    apply_rules(COMMAND_COUNTERPART_RULES, "legacy-command-token")
    apply_rules(FORM_COUNTERPART_RULES, "legacy-form-token", match_path_only=True)
    apply_rules(SOURCE_COUNTERPART_RULES, "legacy-source-token", match_path_only=True)
    apply_rules(TOKEN_COUNTERPART_RULES, "legacy-control-token")

    if "editglobalsettings" in source_token:
        add_unique(current_ids, ["command:global_settings"])
        strategies.append("legacy-form:global-settings")
    if "editcharactersettings" in source_token or "selectsetting" in source_token:
        add_unique(current_ids, ["command:character_settings"])
        strategies.append("legacy-form:character-settings")
    if "selectbuildmethod" in source_token or "charactercreate" in source_token:
        add_unique(current_ids, ["command:new_character"])
        strategies.append("legacy-form:character-create")
    if "controls" in source_token and "table" in source_token:
        add_unique(current_ids, ["action:tab-gear.inventory", "action:tab-combat.weapons", "action:tab-contacts.contacts"])
        strategies.append("legacy-runtime:table-control")

    if not current_ids and family in PARITY_FAMILIES:
        add_unique(current_ids, PARITY_FAMILIES[family]["currentIds"])
        strategies.append(f"legacy-family:{family}")

    return current_ids, strategies


missing_family_mappings: list[str] = []
missing_family_ids: dict[str, list[str]] = {}
missing_family_tests: dict[str, list[str]] = {}
for family in observed_families:
    mapping = PARITY_FAMILIES.get(family)
    if mapping is None:
        missing_family_mappings.append(family)
        continue
    available_ids = [current_id for current_id in mapping["currentIds"] if current_id_available(current_id)]
    missing_tests = [marker for marker in mapping["proofMarkers"] if marker not in test_corpus]
    if not available_ids:
        missing_family_ids[family] = mapping["currentIds"]
    if missing_tests:
        missing_family_tests[family] = missing_tests
    evidence["familyReviews"][family] = {
        "legacyEventCount": family_counts.get(family, 0),
        "legacyDynamicElementCount": dynamic_family_counts.get(family, 0),
        "mappedCurrentIds": mapping["currentIds"],
        "availableCurrentIds": available_ids,
        "proofMarkers": mapping["proofMarkers"],
        "missingProofMarkers": missing_tests,
        "status": "pass" if available_ids and not missing_tests else "fail",
    }

if missing_family_mappings:
    add_reason("Observed legacy UI behavior families have no Chummer6 parity mapping: " + ", ".join(missing_family_mappings))
if missing_family_ids:
    add_reason(
        "Observed legacy UI behavior families have no live Chummer6 command/control/action IDs: "
        + ", ".join(f"{family}: {', '.join(ids)}" for family, ids in sorted(missing_family_ids.items()))
    )
if missing_family_tests:
    add_reason(
        "Observed legacy UI behavior families are missing executable proof markers: "
        + ", ".join(f"{family}: {', '.join(markers)}" for family, markers in sorted(missing_family_tests.items()))
    )

legacy_element_reviews: list[dict[str, Any]] = []
missing_element_reviews: list[dict[str, Any]] = []
family_fallback_reviews: list[dict[str, Any]] = []
all_legacy_elements = legacy_events + legacy_dynamic_elements
for item in all_legacy_elements:
    family = str(item.get("family") or "")
    mapped_current_ids, mapping_strategies = resolve_legacy_element_counterparts(item)
    available_current_ids = [current_id for current_id in mapped_current_ids if current_id_available(current_id)]
    proof_markers = PARITY_FAMILIES.get(family, {}).get("proofMarkers", [])
    missing_proof_markers = [marker for marker in proof_markers if marker not in test_corpus]
    review = {
        "legacyElementId": legacy_element_id(item),
        "source": item.get("source"),
        "line": item.get("line"),
        "kind": item.get("kind"),
        "control": item.get("control") or item.get("type"),
        "event": item.get("event") or "new",
        "handler": item.get("handler"),
        "family": family,
        "mappedCurrentIds": mapped_current_ids,
        "availableCurrentIds": available_current_ids,
        "mappingStrategies": mapping_strategies,
        "proofMarkers": proof_markers,
        "missingProofMarkers": missing_proof_markers,
        "status": "pass" if available_current_ids and not missing_proof_markers else "fail",
    }
    legacy_element_reviews.append(review)
    if mapping_strategies == [f"legacy-family:{family}"]:
        family_fallback_reviews.append(review)
    if review["status"] != "pass":
        missing_element_reviews.append(review)

evidence["legacyElementDispositionCount"] = len(legacy_element_reviews)
evidence["legacyElementDispositionSamples"] = legacy_element_reviews[:80]
evidence["missingLegacyElementDispositionCount"] = len(missing_element_reviews)
evidence["missingLegacyElementDispositions"] = missing_element_reviews[:80]
evidence["familyFallbackLegacyElementDispositionCount"] = len(family_fallback_reviews)
evidence["familyFallbackLegacyElementDispositionSamples"] = family_fallback_reviews[:40]
if missing_element_reviews:
    add_reason(
        f"Individual {legacy_subject} legacy UI elements lack concrete Chummer6 counterpart disposition: "
        + ", ".join(
            f"{item['legacyElementId']} -> {', '.join(item['mappedCurrentIds']) or 'none'}"
            for item in missing_element_reviews[:12]
        )
    )
if family_fallback_reviews:
    add_reason(
        f"Individual {legacy_subject} legacy UI elements still rely on behavior-family fallback instead of source/control-specific counterpart rules: "
        + ", ".join(item["legacyElementId"] for item in family_fallback_reviews[:12])
    )

verify_text = texts.get("verifyScript", "")
verify_banner = verify_banner_override
verify_invocation = verify_invocation_override
evidence["wiredIntoStandardVerify"] = verify_banner in verify_text and verify_invocation in verify_text
evidence["verifyMarker"] = verify_banner
evidence["verifyInvocation"] = verify_invocation
if not evidence["wiredIntoStandardVerify"]:
    add_reason(f"{legacy_subject} legacy UI element parity guard is not wired into scripts/ai/verify.sh.")

b14_text = texts.get("b14Script", "")
b14_markers = [marker for marker in b14_markers_override.split("|") if marker]
evidence["b14Markers"] = {marker: marker in b14_text for marker in b14_markers}
evidence["b14ConsumesReceipt"] = all(evidence["b14Markers"].values())
if not evidence["b14ConsumesReceipt"]:
    add_reason(f"B14 flagship UI release gate does not consume the {legacy_subject} legacy UI element parity receipt.")

test_command = [
    "bash",
    "scripts/ai/test.sh",
    "Chummer.Tests/Chummer.Tests.csproj",
    "--no-restore",
    "-f",
    "net10.0",
    "--filter",
    PROOF_FILTER,
    "-v",
    "minimal",
]
evidence["testCommand"] = test_command
evidence["testFilter"] = PROOF_FILTER
test_assembly_path = repo_root / "Chummer.Tests" / "bin" / "Debug" / "Chummer.Tests.dll"
evidence["testAssemblyPath"] = str(test_assembly_path)
evidence["reusedExistingTestBuild"] = bool(reuse_existing_test_build and test_assembly_path.is_file())

execution_failures: list[str] = []
if not reasons:
    if reuse_existing_test_build and test_assembly_path.is_file():
        evidence["buildCommand"] = []
        evidence["buildExitCode"] = 0
        evidence["buildOutputTail"] = []
    else:
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
        evidence["buildCommand"] = build_command
        build_result = subprocess.run(build_command, cwd=repo_root, text=True, capture_output=True)
        evidence["buildExitCode"] = build_result.returncode
        evidence["buildOutputTail"] = tail_lines((build_result.stdout or "") + "\n" + (build_result.stderr or ""))
        if build_result.returncode != 0:
            execution_failures.append(f"Legacy UI element parity build failed with exit code {build_result.returncode}.")
    if not execution_failures:
        test_result = subprocess.run(test_command, cwd=repo_root, text=True, capture_output=True)
        combined_output = (test_result.stdout or "") + "\n" + (test_result.stderr or "")
        no_matches = "no test matches the given testcase filter" in combined_output.lower()
        evidence["testResult"] = {
            "exitCode": test_result.returncode,
            "noMatches": no_matches,
            "outputTail": tail_lines(combined_output),
        }
        if test_result.returncode != 0:
            execution_failures.append(f"Legacy UI element parity proof tests failed with exit code {test_result.returncode}.")
        if no_matches:
            execution_failures.append("Legacy UI element parity proof filter matched zero tests.")
else:
    evidence["buildExitCode"] = None
    evidence["testResult"] = {}

for failure in execution_failures:
    add_reason(failure)

evidence["observedFamilyCount"] = len(observed_families)
evidence["observedFamilies"] = observed_families
evidence["unclassifiedLegacyEventCount"] = len(unclassified_events)
evidence["unclassifiedLegacyDynamicElementCount"] = len(unclassified_dynamic)
evidence["failureCount"] = len(reasons)
evidence["reasonCount"] = len(reasons)

payload["legacyExtractionReview"] = {
    "status": "pass" if legacy_events and not unclassified_events and not unclassified_dynamic else "fail",
    "summary": (
        "Legacy designer, runtime handler, and dynamic interactive element extraction is classified."
        if legacy_events and not unclassified_events and not unclassified_dynamic
        else "Legacy UI extraction has missing or unclassified controls."
    ),
    "designerEventHookCount": len(designer_events),
    "runtimeEventHookCount": len(runtime_events),
    "dynamicInteractiveElementCount": len(legacy_dynamic_elements),
    "unclassifiedLegacyEvents": unclassified_events[:50],
    "unclassifiedLegacyDynamicElements": unclassified_dynamic[:50],
}
payload["currentMappingReview"] = {
    "status": "pass" if not missing_family_mappings and not missing_family_ids and not missing_family_tests and not missing_element_reviews else "fail",
    "summary": (
        "Every observed legacy UI element and behavior family maps to live Chummer6 IDs and executable proof markers."
        if not missing_family_mappings and not missing_family_ids and not missing_family_tests and not missing_element_reviews
        else "One or more observed legacy UI elements or behavior families lack Chummer6 parity mapping."
    ),
    "familyReviews": evidence["familyReviews"],
}
payload["legacyElementDispositionReview"] = {
    "status": "pass" if not missing_element_reviews else "fail",
    "summary": (
        f"Every extracted {legacy_subject} UI event hook and dynamic interactive element has an individual Chummer6 counterpart disposition."
        if not missing_element_reviews
        else f"One or more extracted {legacy_subject} UI event hooks or dynamic elements lack individual Chummer6 counterpart disposition."
    ),
    "legacyElementDispositionCount": len(legacy_element_reviews),
    "missingLegacyElementDispositionCount": len(missing_element_reviews),
    "familyFallbackLegacyElementDispositionCount": len(family_fallback_reviews),
    "missingLegacyElementDispositions": missing_element_reviews[:80],
}
payload["dynamicElementReview"] = {
    "status": "pass" if (not legacy_dynamic_elements or current_dynamic_elements) else "fail",
    "summary": (
        "Dynamically created legacy and current interactive elements are included in the parity evidence."
        if not legacy_dynamic_elements or current_dynamic_elements
        else "Legacy dynamic interactive elements are present without current dynamic parity evidence."
    ),
    "legacyDynamicInteractiveElementCount": len(legacy_dynamic_elements),
    "currentDynamicInteractiveElementCount": len(current_dynamic_elements),
    "currentNamedAxamlInteractiveElementCount": len(current_named_axaml_elements),
}
payload["verifyWiringReview"] = {
    "status": "pass" if evidence["wiredIntoStandardVerify"] else "fail",
    "summary": (
        "Legacy UI element parity guard is wired into the standard verify path."
        if evidence["wiredIntoStandardVerify"]
        else "Legacy UI element parity guard is not wired into the standard verify path."
    ),
    "verifyMarker": verify_banner,
    "verifyInvocation": verify_invocation,
}
payload["b14ConsumptionReview"] = {
    "status": "pass" if evidence["b14ConsumesReceipt"] else "fail",
    "summary": (
        "B14 consumes the legacy UI element parity receipt."
        if evidence["b14ConsumesReceipt"]
        else "B14 does not consume the legacy UI element parity receipt."
    ),
    "markers": evidence["b14Markers"],
}
payload["executionReview"] = {
    "status": "pass" if not execution_failures else "fail",
    "summary": (
        "Legacy UI element parity proof tests executed cleanly."
        if not execution_failures
        else "Legacy UI element parity proof tests failed."
    ),
    "reasons": execution_failures,
    "buildExitCode": evidence["buildExitCode"],
    "testResult": evidence["testResult"],
}

if not reasons:
    payload["status"] = "pass"
    payload["summary"] = (
        f"Every extracted {legacy_subject} designer, runtime, and dynamically created interactive UI element "
        "has an individual live Chummer6 command/control/action counterpart disposition and executable proof coverage."
    )

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(48)

print(f"[{script_label}] PASS: legacy UI elements, dynamic handlers, and Chummer6 behavior counterparts are covered.")
print(f"[{script_label}] evidence: {receipt_path}")
PY
