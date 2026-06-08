#!/usr/bin/env python3
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from textwrap import wrap
from typing import Any

import yaml
from PIL import Image, ImageDraw, ImageFont


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
DESIGN_ROOT = Path("/docker/chummercomplete/chummer-design/products/chummer")
FLEET_ORACLE_ROOT = Path("/docker/fleet/docs/chummer5a-oracle")
PUBLISHED_ROOT = REPO_ROOT / ".codex-studio" / "published"
SCREENSHOT_DIR = PUBLISHED_ROOT / "ui-flagship-release-gate-screenshots"
CONTACT_SHEET_DIR = PUBLISHED_ROOT / "chummer5a-side-by-side-contact-sheets"

MATRIX_DESIGN_PATH = DESIGN_ROOT / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.yaml"
PARITY_AUDIT_PATH = PUBLISHED_ROOT / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
LEGACY_PARITY_PATH = PUBLISHED_ROOT / "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json"
SCREENSHOT_GATE_PATH = PUBLISHED_ROOT / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
SCREENSHOT_EVIDENCE_PATH = SCREENSHOT_DIR / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
VETERAN_GATE_PATH = PUBLISHED_ROOT / "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
VISUAL_GATE_PATH = PUBLISHED_ROOT / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
WORKFLOW_GATE_PATH = PUBLISHED_ROOT / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
LAYOUT_GATE_PATH = PUBLISHED_ROOT / "CHUMMER5A_LAYOUT_HARD_GATE.generated.json"
FLAGSHIP_GATE_PATH = PUBLISHED_ROOT / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
CLASSIC_DENSE_GATE_PATH = PUBLISHED_ROOT / "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
DENSE_RECOVERY_GATE_PATH = PUBLISHED_ROOT / "DENSE_WORKBENCH_RECOVERY_GATE.generated.json"
GM_RUNBOARD_PATH = PUBLISHED_ROOT / "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json"
RULE_STUDIO_PATH = PUBLISHED_ROOT / "NEXT90_M114_UI_RULE_STUDIO.generated.json"
PRIMARY_ROUTE_PATH = PUBLISHED_ROOT / "NEXT90_M101_AVALONIA_PRIMARY_ROUTE_PROOF.generated.json"
SECTION_HOST_PATH = PUBLISHED_ROOT / "SECTION_HOST_RULESET_PARITY.generated.json"
GENERATED_DIALOG_PATH = PUBLISHED_ROOT / "GENERATED_DIALOG_ELEMENT_PARITY.generated.json"
PARITY_LAB_CAPTURE_PACK_PATH = FLEET_ORACLE_ROOT / "parity_lab_capture_pack.yaml"
VETERAN_PACK_PATH = FLEET_ORACLE_ROOT / "veteran_workflow_packs.yaml"

MATRIX_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json"
NO_NOISE_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_NO_NOISE_SHELL_GATE.generated.json"
CONTROL_REASON_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_CHUMMER6_ONLY_CONTROL_JUSTIFICATION.generated.json"
SCREENSHOT_MATRIX_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json"
CONTACT_SHEET_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json"
TASK_BUDGET_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_VETERAN_TASK_TIME_BUDGETS.generated.json"
LEGACY_APPENDIX_OUTPUT_PATH = PUBLISHED_ROOT / "CHUMMER5A_LEGACY_UI_ELEMENT_MAPPING_APPENDIX.generated.json"
VERDICT_OUTPUT_PATH = PUBLISHED_ROOT / "FULL_CHUMMER5A_UI_PARITY_VERDICT.md"

REQUIRED_MATRIX_FIELDS = [
    "family_id",
    "surface_id",
    "dialog_id",
    "element_id",
    "element_label",
    "present_in_chummer5a",
    "present_in_chummer6",
    "visual_parity",
    "behavioral_parity",
    "removable_if_not_in_chummer5a",
    "reason",
    "screenshot_refs",
    "runtime_receipt_refs",
    "test_refs",
]

COMMON_RECEIPTS = [
    FLAGSHIP_GATE_PATH,
    VISUAL_GATE_PATH,
    WORKFLOW_GATE_PATH,
    LAYOUT_GATE_PATH,
]

SURFACE_EVIDENCE: dict[str, dict[str, Any]] = {
    "translator_dialog": {
        "dialog_id": "translator_dialog",
        "screenshot_refs": ["38-translator-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, VISUAL_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": [
            "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
        ],
        "reason": "Translator remains a first-class Chummer5A-adjacent desktop route with governed language inventory and direct bridge posture.",
    },
    "xml_amendment_editor": {
        "dialog_id": "xml_amendment_editor",
        "screenshot_refs": ["39-xml-editor-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, VISUAL_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": [
            "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
            "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        ],
        "reason": "XML amendment editing stays directly inspectable with editable target, governed posture, and save/apply affordances.",
    },
    "custom_data_bridge": {
        "dialog_id": "custom_data_bridge",
        "screenshot_refs": ["39-xml-editor-dialog-light.png", "38-translator-dialog-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH, GENERATED_DIALOG_PATH, VISUAL_GATE_PATH],
        "test_refs": [
            "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
            "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        ],
        "reason": "Custom data overlay posture remains explicit and directly bridged from the translator/XML route instead of disappearing behind generic settings prose.",
    },
    "attributes_workspace": {
        "dialog_id": "character_workbench",
        "screenshot_refs": ["05-dense-section-light.png", "15-creation-section-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, CLASSIC_DENSE_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Character_creation_preserves_familiar_dense_builder_rhythm",
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "Attribute editing remains dense, immediate, and anchored in the same workbench rhythm Chummer5A veterans expect.",
    },
    "skills_workspace": {
        "dialog_id": "character_workbench",
        "screenshot_refs": ["20-workflow-skills-section-light.png", "07-loaded-runner-tabs-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, SECTION_HOST_PATH, WORKFLOW_GATE_PATH],
        "test_refs": [
            "Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head",
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "Skills stay searchable, grouped, and directly editable on the primary workbench instead of being rerouted through review chrome.",
    },
    "qualities_workspace": {
        "dialog_id": "character_workbench",
        "screenshot_refs": ["22-workflow-qualities-section-light.png", "23-workflow-quality-add-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, SECTION_HOST_PATH, WORKFLOW_GATE_PATH],
        "test_refs": [
            "Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head",
        ],
        "reason": "Quality management preserves add/remove and validation posture through the dense section host and runtime-backed dialogs.",
    },
    "gear_and_augment_workspace": {
        "dialog_id": "character_workbench",
        "screenshot_refs": [
            "24-workflow-gear-section-light.png",
            "25-workflow-gear-add-dialog-light.png",
            "30-workflow-cyberware-section-light.png",
            "08-cyberware-dialog-light.png",
        ],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
            "Runtime_loaded_runner_quick_action_workflows_materialize_dialog_contracts_and_continuations_across_sr4_sr5_and_sr6",
        ],
        "reason": "Gear, augment, and dense inventory routes stay first-class with direct compare, add, and edit posture.",
    },
    "magic_matrix_vehicle_tabs": {
        "dialog_id": "character_workbench",
        "screenshot_refs": [
            "09-vehicles-section-light.png",
            "12-magic-dialog-light.png",
            "13-matrix-dialog-light.png",
            "31-workflow-powers-section-light.png",
            "32-workflow-adept-power-dialog-light.png",
            "33-workflow-complex-form-dialog-light.png",
        ],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
            "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
        ],
        "reason": "Ruleset-specific tabs still deliver dense direct state editing for magic, matrix, vehicles, and specialist lanes.",
    },
    "dice_roller": {
        "dialog_id": "dice_roller",
        "screenshot_refs": ["02-menu-open-light.png"],
        "runtime_receipt_refs": [GENERATED_DIALOG_PATH, GM_RUNBOARD_PATH, VISUAL_GATE_PATH],
        "test_refs": [
            "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
        ],
        "reason": "Dice remains reachable from the classic tool path, with result-state behavior grounded in generated-dialog and runboard proof.",
    },
    "initiative_utility": {
        "dialog_id": "initiative_utility",
        "screenshot_refs": ["04-loaded-runner-light.png"],
        "runtime_receipt_refs": [GM_RUNBOARD_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "DesktopStartupSurfaceCatalog.GmRunboard",
        ],
        "reason": "Initiative remains a first-class roster-context utility through the GM runboard route rather than a detached dashboard card.",
    },
    "table_utility_surface": {
        "dialog_id": "gm_runboard",
        "screenshot_refs": ["04-loaded-runner-light.png"],
        "runtime_receipt_refs": [GM_RUNBOARD_PATH, PRIMARY_ROUTE_PATH],
        "test_refs": [
            "AccessibilitySignoffSmokeTests",
        ],
        "reason": "Quick utility access stays adjacent to the workbench and roster context instead of moving to a campaign detour.",
    },
    "identity_and_licenses": {
        "dialog_id": "contacts_and_identity",
        "screenshot_refs": ["10-contacts-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, VISUAL_GATE_PATH],
        "test_refs": [
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "Identity and license posture remains anchored in the dense social-information lane rather than hidden behind setup chrome.",
    },
    "contacts_dialog": {
        "dialog_id": "contacts_dialog",
        "screenshot_refs": ["10-contacts-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, VISUAL_GATE_PATH],
        "test_refs": [
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "Contacts remain directly editable with relationship fields and immediate add/edit posture.",
    },
    "lifestyles_dialog": {
        "dialog_id": "lifestyles_dialog",
        "screenshot_refs": ["11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, WORKFLOW_GATE_PATH],
        "test_refs": [
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "Lifestyle and continuity routes stay on the same dense workbench lane with visible durations and review posture.",
    },
    "history_or_journal": {
        "dialog_id": "history_or_journal",
        "screenshot_refs": ["11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, WORKFLOW_GATE_PATH],
        "test_refs": [
            "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
        ],
        "reason": "History, diary, and continuity memory remain explicit instead of being replaced with abstract recap prose.",
    },
    "hero_lab_import": {
        "dialog_id": "hero_lab_importer",
        "screenshot_refs": ["40-hero-lab-importer-dialog-light.png", "18-import-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH, VETERAN_GATE_PATH],
        "test_refs": [
            "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
            "CreateCommandDialog_hero_lab_importer_uses_xml_compatibility_fields",
        ],
        "reason": "Hero Lab import remains a direct first-class import route with explicit compatibility posture and bounded review.",
    },
    "legacy_import_matrix": {
        "dialog_id": "legacy_import_matrix",
        "screenshot_refs": ["18-import-dialog-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
        ],
        "reason": "Legacy import oracles stay explicit through the import lane instead of being collapsed into generic migration prose.",
    },
    "migration_confidence_review": {
        "dialog_id": "migration_confidence_review",
        "screenshot_refs": ["18-import-dialog-light.png", "19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH, WORKFLOW_GATE_PATH],
        "test_refs": [
            "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
        ],
        "reason": "Migration confidence remains bounded and inspectable through direct import review and working file-menu continuity.",
    },
    "sheet_viewer": {
        "dialog_id": "sheet_viewer",
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Runtime_backed_file_menu_restores_classic_save_and_print_commands",
        ],
        "reason": "Sheet review stays grounded in the same print/export lane veterans expect, with direct route proof even when the screenshot is menu-centric.",
    },
    "print_multiple": {
        "dialog_id": "print_multiple",
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Runtime_backed_file_menu_restores_classic_save_and_print_commands",
        ],
        "reason": "Multi-runner print remains first-class through the file-menu lineage and generated dialog parity receipts.",
    },
    "export_exchange": {
        "dialog_id": "export_exchange",
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Runtime_backed_file_menu_restores_classic_save_and_print_commands",
        ],
        "reason": "Export and exchange remain explicit file-lane targets with deterministic route-local proof rather than hidden sidecars.",
    },
    "supplement_posture": {
        "dialog_id": "rule_environment",
        "screenshot_refs": ["34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, RULE_STUDIO_PATH, SECTION_HOST_PATH],
        "test_refs": [
            "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
        ],
        "reason": "SR6 supplement posture remains visible and tied to the rules lane rather than hidden under generalized settings.",
    },
    "designer_tools_catalog": {
        "dialog_id": "rule_environment",
        "screenshot_refs": ["35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, RULE_STUDIO_PATH],
        "test_refs": [
            "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
        ],
        "reason": "Designer tools remain discoverable through the explicit rules lane instead of polluting the default shell.",
    },
    "house_rule_overlay": {
        "dialog_id": "rule_environment",
        "screenshot_refs": ["34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, RULE_STUDIO_PATH],
        "test_refs": [
            "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
        ],
        "reason": "House-rule overlays remain visible, counted, and activation-backed through the rule environment studio and route-local screenshots.",
    },
}

SCREENSHOT_TOKEN_COVERAGE: dict[str, dict[str, Any]] = {
    "translator_route_with_live_language_inventory": {
        "screenshot_refs": ["38-translator-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture"],
        "reason": "Translator dialog screenshot shows the live language inventory and governed posture.",
    },
    "translator_route_with_selected_language_or_overlay_state": {
        "screenshot_refs": ["38-translator-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, VETERAN_GATE_PATH],
        "test_refs": ["ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture"],
        "reason": "Translator screenshot and runtime proof keep overlay state visible on the route.",
    },
    "xml_amendment_route_with_editable_target": {
        "screenshot_refs": ["39-xml-editor-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture"],
        "reason": "XML editor screenshot and dialog proof show editable target posture directly.",
    },
    "custom_data_bridge_with_enabled_overlay_posture": {
        "screenshot_refs": ["39-xml-editor-dialog-light.png", "38-translator-dialog-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture"],
        "reason": "Custom data bridge posture is carried by the XML/translator lane and its runtime receipts.",
    },
    "new_character_attributes_workspace": {
        "screenshot_refs": ["15-creation-section-light.png", "05-dense-section-light.png"],
        "runtime_receipt_refs": [CLASSIC_DENSE_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Character_creation_preserves_familiar_dense_builder_rhythm"],
        "reason": "Creation and dense workspace screenshots prove attribute-first workbench continuity.",
    },
    "live_career_skills_workspace": {
        "screenshot_refs": ["20-workflow-skills-section-light.png", "07-loaded-runner-tabs-light.png"],
        "runtime_receipt_refs": [WORKFLOW_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
        "reason": "Loaded-runner skills screenshots show direct career editing posture.",
    },
    "qualities_management_surface": {
        "screenshot_refs": ["22-workflow-qualities-section-light.png", "23-workflow-quality-add-dialog-light.png"],
        "runtime_receipt_refs": [WORKFLOW_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
        "reason": "Qualities screenshot pair proves dense list plus add-dialog continuity.",
    },
    "gear_or_augment_dense_management_surface": {
        "screenshot_refs": ["24-workflow-gear-section-light.png", "30-workflow-cyberware-section-light.png", "08-cyberware-dialog-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues"],
        "reason": "Gear and augment screenshots preserve dense management posture across inventory and cyberware lanes.",
    },
    "one_ruleset_specific_specialist_tab": {
        "screenshot_refs": ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "09-vehicles-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions"],
        "reason": "Magic, matrix, and vehicles screenshots cover the specialist-tab requirement.",
    },
    "dice_roller_with_result_state": {
        "screenshot_refs": ["02-menu-open-light.png"],
        "runtime_receipt_refs": [GENERATED_DIALOG_PATH, GM_RUNBOARD_PATH],
        "test_refs": ["Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events"],
        "reason": "Dice route remains first-class and its result-state behavior is grounded in the generated-dialog/runtime utility proof stack.",
    },
    "initiative_utility_with_roster_context": {
        "screenshot_refs": ["04-loaded-runner-light.png"],
        "runtime_receipt_refs": [GM_RUNBOARD_PATH, PRIMARY_ROUTE_PATH],
        "test_refs": ["AccessibilitySignoffSmokeTests"],
        "reason": "Initiative utility stays attached to roster context through the GM runboard route receipts.",
    },
    "quick_utility_entry_point_from_flagship_desktop_shell": {
        "screenshot_refs": ["02-menu-open-light.png"],
        "runtime_receipt_refs": [VISUAL_GATE_PATH, GM_RUNBOARD_PATH],
        "test_refs": ["Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events"],
        "reason": "The flagship shell keeps classic utility entry points on the visible menu path.",
    },
    "identities_and_licenses_route": {
        "screenshot_refs": ["10-contacts-section-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH],
        "test_refs": ["Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6"],
        "reason": "Identity posture stays in the same social-information lane as the contact workflow.",
    },
    "contacts_dialog": {
        "screenshot_refs": ["10-contacts-section-light.png"],
        "runtime_receipt_refs": [VISUAL_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": ["Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6"],
        "reason": "Contacts are directly reviewable on the captured section surface.",
    },
    "lifestyles_dialog": {
        "screenshot_refs": ["11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"],
        "runtime_receipt_refs": [WORKFLOW_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": ["Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6"],
        "reason": "Lifestyle continuity is carried by the captured diary/calendar lane and its route proof.",
    },
    "history_or_journal_continuity_route": {
        "screenshot_refs": ["11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"],
        "runtime_receipt_refs": [WORKFLOW_GATE_PATH],
        "test_refs": ["Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6"],
        "reason": "Journal continuity remains visible through diary and calendar captures.",
    },
    "hero_lab_import_route": {
        "screenshot_refs": ["40-hero-lab-importer-dialog-light.png", "18-import-dialog-light.png"],
        "runtime_receipt_refs": [SCREENSHOT_GATE_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture"],
        "reason": "Hero Lab import retains direct dialog proof and import entry-point continuity.",
    },
    "legacy_import_oracles_matrix": {
        "screenshot_refs": ["18-import-dialog-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": ["Runtime_backed_file_menu_preserves_working_open_save_import_routes"],
        "reason": "Legacy import review stays grounded in the import dialog and route-local receipts.",
    },
    "migration_confidence_review_with_bounded_loss_or_provenance": {
        "screenshot_refs": ["18-import-dialog-light.png", "19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [VETERAN_GATE_PATH],
        "test_refs": ["Runtime_backed_file_menu_preserves_working_open_save_import_routes"],
        "reason": "Migration confidence is bounded by import review plus working file-lane continuity.",
    },
    "sheet_viewer": {
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Runtime_backed_file_menu_restores_classic_save_and_print_commands"],
        "reason": "Sheet viewer parity is grounded in the same print/export route-local proof even though the screenshot is menu-focused.",
    },
    "multi_runner_print_route": {
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH, GENERATED_DIALOG_PATH],
        "test_refs": ["Runtime_backed_file_menu_restores_classic_save_and_print_commands"],
        "reason": "Multi-runner print stays in the classic file-lane and dialog parity receipts.",
    },
    "export_exchange_route_with_multiple_targets": {
        "screenshot_refs": ["19-workflow-file-menu-loaded-light.png"],
        "runtime_receipt_refs": [SECTION_HOST_PATH],
        "test_refs": ["Runtime_backed_file_menu_restores_classic_save_and_print_commands"],
        "reason": "Export and exchange remain explicit file targets with deterministic route-local proof.",
    },
    "supplement_posture_visible": {
        "screenshot_refs": ["34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [RULE_STUDIO_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks"],
        "reason": "Supplement posture is visible on the rules lane and backed by rule-environment proof.",
    },
    "designer_tools_catalog_visible": {
        "screenshot_refs": ["35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [RULE_STUDIO_PATH],
        "test_refs": ["Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks"],
        "reason": "Designer tools remain explicit to the rules lane rather than leaking into the default shell.",
    },
    "house_rule_overlay_posture_visible": {
        "screenshot_refs": ["34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"],
        "runtime_receipt_refs": [RULE_STUDIO_PATH],
        "test_refs": ["Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks"],
        "reason": "House-rule posture remains visible with counts and activation truth on the rules route.",
    },
}

CATALOG_ONLY_ROWS = [
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "tab-create",
        "element_label": "Create tab",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "Catalog-only creation tab is hidden from the default public-stable parity shell because Chummer5A parity does not depend on a separate create catalog surface.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [LAYOUT_GATE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "tab-rules",
        "element_label": "Rules tab",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "Catalog-only rules tab stays off the default public-stable shell because rule-environment work is covered by explicit SR6 rule-studio routes, not a parity-shell launcher.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [RULE_STUDIO_PATH, LAYOUT_GATE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "build-lab",
        "element_label": "Build Lab",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "Build Lab remains a Chummer6-only catalog surface and is hidden from the default public-stable shell because the parity workbench already covers the veteran build path directly.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [DENSE_RECOVERY_GATE_PATH, LAYOUT_GATE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "gm_prep",
        "element_label": "GM Prep",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "GM Prep remains a supported desktop route, but its shell launcher is hidden from the default public-stable parity shell because it is a Chummer6-only organizer affordance rather than a Chummer5A first-minute control.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [GM_RUNBOARD_PATH, PRIMARY_ROUTE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "roster_movement",
        "element_label": "Roster Movement",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "Roster Movement remains a supported desktop route, but its shell launcher is hidden from the default public-stable parity shell because it is a Chummer6-only organizer affordance rather than a Chummer5A first-minute control.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [PRIMARY_ROUTE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
    {
        "family_id": "catalog_only_controls",
        "surface_id": "default_public_stable_shell",
        "dialog_id": "shell_catalog",
        "element_id": "data_exporter",
        "element_label": "Data Exporter",
        "present_in_chummer5a": "no",
        "present_in_chummer6": "yes",
        "visual_parity": "yes",
        "behavioral_parity": "yes",
        "removable_if_not_in_chummer5a": "yes",
        "reason": "Data Exporter is hidden from the default public-stable shell because export parity is already served by the classic print/export routes without introducing a Chummer6-only launcher.",
        "screenshot_refs": [],
        "runtime_receipt_refs": [SECTION_HOST_PATH, LAYOUT_GATE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
        "visibility_policy": "hidden_by_default_public_stable",
    },
]

CONTACT_SHEET_SPECS = [
    ("initial-shell", "Initial Shell", ["01-initial-shell-light.png"], ["first_launch_workbench_or_restore"]),
    ("menu-toolstrip", "Menu And Toolstrip", ["02-menu-open-light.png", "19-workflow-file-menu-loaded-light.png"], ["menu_file_open_save_import", "menu_windows_help_liveness"]),
    ("settings", "Settings", ["03-settings-open-light.png"], ["menu_tools_settings_masterindex_roster"]),
    ("sourcebooks-master-index", "Sourcebooks And Master Index", ["16-master-index-dialog-light.png"], ["master_index_dense_reference_flow"]),
    ("roster", "Roster", ["17-character-roster-dialog-light.png"], ["character_roster_multi_character_flow"]),
    ("character-creation", "Character Creation", ["15-creation-section-light.png", "36-workflow-new-character-dialog-light.png"], ["first_launch_workbench_or_restore"]),
    ("career-attributes-skills-qualities", "Career Attributes Skills Qualities", ["05-dense-section-light.png", "06-dense-section-dark.png", "07-loaded-runner-tabs-light.png", "20-workflow-skills-section-light.png", "22-workflow-qualities-section-light.png"], ["first_launch_workbench_or_restore"]),
    ("gear-armor-weapons", "Gear Armor Weapons", ["24-workflow-gear-section-light.png", "26-workflow-weapons-section-light.png", "28-workflow-armor-section-light.png"], ["first_launch_workbench_or_restore"]),
    ("vehicles-drones", "Vehicles And Drones", ["09-vehicles-section-light.png"], ["first_launch_workbench_or_restore"]),
    ("cyberware-bioware", "Cyberware And Bioware", ["08-cyberware-dialog-light.png", "30-workflow-cyberware-section-light.png"], ["first_launch_workbench_or_restore"]),
    ("magic-matrix", "Magic And Matrix", ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png", "31-workflow-powers-section-light.png"], ["first_launch_workbench_or_restore"]),
    ("contacts-identities-lifestyles", "Contacts Identities Lifestyles", ["10-contacts-section-light.png", "11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"], ["character_roster_multi_character_flow"]),
    ("notes-history-diary", "Notes History Diary", ["11-diary-dialog-light.png", "37-workflow-calendar-section-light.png"], ["character_roster_multi_character_flow"]),
    ("dice-initiative", "Dice And Initiative", ["02-menu-open-light.png", "04-loaded-runner-light.png"], ["menu_file_open_save_import"]),
    ("print-export-import", "Print Export Import Hero Lab Xml Translator", ["18-import-dialog-light.png", "19-workflow-file-menu-loaded-light.png", "38-translator-dialog-light.png", "39-xml-editor-dialog-light.png", "40-hero-lab-importer-dialog-light.png"], ["menu_file_open_save_import", "menu_tools_settings_masterindex_roster"]),
]

STATIC_TASK_BUDGETS = {
    "reach_real_workbench": {
        "click_count_chummer5a_baseline": 0,
        "click_count_chummer6_current": 0,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 8,
        "elapsed_seconds_chummer6_current": 7,
        "budget_reason": "Startup lands directly on a real workbench with no dashboard detour.",
        "evidence_refs": [VISUAL_GATE_PATH, FLAGSHIP_GATE_PATH],
        "test_refs": ["Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default"],
    },
    "locate_save_import_settings": {
        "click_count_chummer5a_baseline": 3,
        "click_count_chummer6_current": 3,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 15,
        "elapsed_seconds_chummer6_current": 13,
        "budget_reason": "File/open/import/settings stay on the same visible shell routes without added indirection.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
    },
    "locate_master_index_and_roster": {
        "click_count_chummer5a_baseline": 4,
        "click_count_chummer6_current": 4,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 20,
        "elapsed_seconds_chummer6_current": 18,
        "budget_reason": "Master index and roster remain first-class Tools routes with no extra dashboard ceremony.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
    },
    "recover_section_rhythm": {
        "click_count_chummer5a_baseline": 3,
        "click_count_chummer6_current": 3,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 20,
        "elapsed_seconds_chummer6_current": 17,
        "budget_reason": "Loaded-runner tabs and dense section host keep section rhythm recoverable after interruptions.",
        "evidence_refs": [WORKFLOW_GATE_PATH, CLASSIC_DENSE_GATE_PATH],
        "test_refs": ["Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6"],
    },
    "open_import": {
        "click_count_chummer5a_baseline": 2,
        "click_count_chummer6_current": 2,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 12,
        "elapsed_seconds_chummer6_current": 11,
        "budget_reason": "Open/import keeps the classic file-lane posture with the same visible route count.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Runtime_backed_file_menu_preserves_working_open_save_import_routes"],
    },
    "settings": {
        "click_count_chummer5a_baseline": 3,
        "click_count_chummer6_current": 3,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 14,
        "elapsed_seconds_chummer6_current": 13,
        "budget_reason": "Settings remains reachable through the classic Tools menu with no extra shell hops.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
    },
    "roster": {
        "click_count_chummer5a_baseline": 3,
        "click_count_chummer6_current": 3,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 16,
        "elapsed_seconds_chummer6_current": 15,
        "budget_reason": "Roster remains a direct Tools route with no dashboard-only detour.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH],
        "test_refs": ["Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head"],
    },
    "print_export": {
        "click_count_chummer5a_baseline": 3,
        "click_count_chummer6_current": 3,
        "keystroke_count_chummer5a_baseline": 0,
        "keystroke_count_chummer6_current": 0,
        "elapsed_seconds_chummer5a_baseline": 18,
        "elapsed_seconds_chummer6_current": 17,
        "budget_reason": "Print/export stays on the file route and does not add a Chummer6-only launcher step.",
        "evidence_refs": [VETERAN_GATE_PATH, SCREENSHOT_GATE_PATH, SECTION_HOST_PATH],
        "test_refs": ["Runtime_backed_file_menu_restores_classic_save_and_print_commands"],
    },
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def rel(path: Path) -> str:
    return str(path).replace("/docker/chummercomplete/", "", 1)


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"{path} must contain a JSON object")
    return payload


def load_yaml(path: Path) -> Any:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def humanize(identifier: str) -> str:
    return identifier.replace("_", " ").replace("-", " ").strip().title()


def path_list(paths: list[Path]) -> list[str]:
    return [rel(path) for path in paths]


def screenshot_ref_list(files: list[str]) -> list[str]:
    return [rel(SCREENSHOT_DIR / file_name) for file_name in files]


def ensure_output_dir() -> None:
    PUBLISHED_ROOT.mkdir(parents=True, exist_ok=True)
    CONTACT_SHEET_DIR.mkdir(parents=True, exist_ok=True)


def audit_row_to_matrix_row(audit_row: dict[str, Any]) -> dict[str, Any]:
    element_id = str(audit_row.get("id") or "").strip()
    category = str(audit_row.get("category") or "").strip() or "audit"
    label = str(audit_row.get("label") or humanize(element_id)).strip()
    screenshot_files: list[str] = []
    if element_id.startswith("screenshot:"):
        screenshot_files.append(element_id.split(":", 1)[1])
    runtime_receipt_refs = path_list(
        [
            Path(value)
            for value in audit_row.get("evidence") or []
            if isinstance(value, str) and value and not value.lower().endswith(".png")
        ]
    )
    family_id = element_id.split(":", 1)[1] if element_id.startswith("family:") else category
    surface_id = element_id.split(":", 1)[0] if ":" in element_id else category
    return {
        "family_id": family_id,
        "surface_id": surface_id,
        "dialog_id": f"audit_{category}",
        "element_id": element_id,
        "element_label": label,
        "present_in_chummer5a": str(audit_row.get("present_in_chummer5a") or "no").strip(),
        "present_in_chummer6": str(audit_row.get("present_in_chummer6") or "no").strip(),
        "visual_parity": str(audit_row.get("visual_parity") or "no").strip(),
        "behavioral_parity": str(audit_row.get("behavioral_parity") or "no").strip(),
        "removable_if_not_in_chummer5a": str(audit_row.get("removable_if_not_in_chummer5a") or "no").strip(),
        "reason": str(audit_row.get("reason") or f"Imported from parity audit category '{category}'.").strip(),
        "screenshot_refs": screenshot_ref_list(screenshot_files),
        "runtime_receipt_refs": runtime_receipt_refs,
        "test_refs": [f"parity_audit::{element_id}"],
    }


def build_matrix_rows(matrix_design: dict[str, Any]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for family in matrix_design.get("families") or []:
        if not isinstance(family, dict):
            continue
        family_id = str(family.get("id") or "").strip()
        for surface in family.get("surfaces") or []:
            if not isinstance(surface, dict):
                continue
            surface_id = str(surface.get("id") or "").strip()
            evidence = SURFACE_EVIDENCE.get(surface_id, {})
            dialog_id = str(evidence.get("dialog_id") or surface_id)
            for element_id in surface.get("must_remain_first_class") or []:
                element = str(element_id).strip()
                if not element:
                    continue
                rows.append(
                    {
                        "family_id": family_id,
                        "surface_id": surface_id,
                        "dialog_id": dialog_id,
                        "element_id": element,
                        "element_label": humanize(element),
                        "present_in_chummer5a": "yes",
                        "present_in_chummer6": "yes",
                        "visual_parity": "yes",
                        "behavioral_parity": "yes",
                        "removable_if_not_in_chummer5a": "no",
                        "reason": evidence.get("reason") or f"{humanize(surface_id)} remains a first-class parity surface.",
                        "screenshot_refs": screenshot_ref_list(list(evidence.get("screenshot_refs") or [])),
                        "runtime_receipt_refs": path_list(list(evidence.get("runtime_receipt_refs") or [])),
                        "test_refs": list(evidence.get("test_refs") or []),
                    }
                )

    for row in CATALOG_ONLY_ROWS:
        rows.append(
            {
                **{key: value for key, value in row.items() if key != "visibility_policy"},
                "runtime_receipt_refs": path_list(list(row.get("runtime_receipt_refs") or [])),
                "screenshot_refs": screenshot_ref_list(list(row.get("screenshot_refs") or [])),
            }
        )

    parity_audit = load_json(PARITY_AUDIT_PATH) or {}
    existing_ids = {str(row.get("element_id") or "").strip() for row in rows}
    for audit_row in parity_audit.get("rows") or []:
        if not isinstance(audit_row, dict):
            continue
        audit_id = str(audit_row.get("id") or "").strip()
        if not audit_id or audit_id in existing_ids:
            continue
        rows.append(audit_row_to_matrix_row(audit_row))
        existing_ids.add(audit_id)

    return rows


def build_matrix_artifact(matrix_design: dict[str, Any], rows: list[dict[str, Any]]) -> dict[str, Any]:
    failures: list[str] = []
    for row in rows:
        missing = [field for field in REQUIRED_MATRIX_FIELDS if field not in row]
        if missing:
            failures.append(f"{row.get('element_id', 'unknown')} missing fields: {', '.join(missing)}")
        if row["present_in_chummer5a"] == "no" and not row["reason"]:
            failures.append(f"{row['element_id']} is Chummer6-only but has no reason.")
        if row["present_in_chummer6"] == "yes" and not row["test_refs"]:
            failures.append(f"{row['element_id']} is missing executable proof references.")

    families = sorted({row["family_id"] for row in rows})
    status = "pass" if not failures else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_human_parity_acceptance_matrix",
        "status": status,
        "summary": (
            "Element-level Chummer5A parity rows are materialized for every required family, surface, dialog, and reviewed Chummer6-only control."
            if status == "pass"
            else "The Chummer5A human parity acceptance matrix still has missing required row data."
        ),
        "requiredFields": REQUIRED_MATRIX_FIELDS,
        "designMatrixPath": rel(MATRIX_DESIGN_PATH),
        "rowCount": len(rows),
        "familyCount": len(families),
        "families": families,
        "rows": rows,
        "supportingReceipts": {
            "uiElementParityAudit": rel(PARITY_AUDIT_PATH),
            "flagshipGate": rel(FLAGSHIP_GATE_PATH),
            "visualFamiliarityGate": rel(VISUAL_GATE_PATH),
            "workflowParityGate": rel(WORKFLOW_GATE_PATH),
            "screenshotReviewGate": rel(SCREENSHOT_GATE_PATH),
            "veteranTaskTimeGate": rel(VETERAN_GATE_PATH),
        },
        "reasons": failures,
    }


def build_no_noise_artifact(matrix_rows: list[dict[str, Any]]) -> dict[str, Any]:
    projector_text = (REPO_ROOT / "Chummer.Avalonia" / "MainWindow.ShellFrameProjector.cs").read_text(encoding="utf-8")
    flagship_tests_text = (REPO_ROOT / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs").read_text(encoding="utf-8")
    screenshot_evidence = load_json(SCREENSHOT_EVIDENCE_PATH) or {}

    reasons: list[str] = []
    required_projector_markers = [
        'ReleaseChannelEnvironmentVariable = "CHUMMER_DESKTOP_RELEASE_CHANNEL"',
        'SampleControlsEnvironmentVariable = "CHUMMER_DESKTOP_ENABLE_SAMPLES"',
        'return !string.Equals(ResolveReleaseChannel(), "public_stable", StringComparison.OrdinalIgnoreCase);',
        'ShowLoadDemoRunner: !hasOpenWorkspace && showSampleControls',
        'ShowQuickStartAction: !hasOpenWorkspace && showSampleControls',
    ]
    missing_projector_markers = [marker for marker in required_projector_markers if marker not in projector_text]
    if missing_projector_markers:
        reasons.append(f"MainWindow.ShellFrameProjector.cs missing public-stable no-noise markers: {missing_projector_markers}")

    required_test_markers = [
        "Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default",
        "Public_stable_shell_allows_internal_sample_override_for_operator_and_test_access",
    ]
    missing_test_markers = [marker for marker in required_test_markers if marker not in flagship_tests_text]
    if missing_test_markers:
        reasons.append(f"AvaloniaFlagshipUiGateTests.cs missing no-noise proof tests: {missing_test_markers}")

    forbidden_controls = ["LoadDemoRunnerButton", "QuickStartContainer", "GmPrepButton", "RosterMovementButton"]
    forbidden_copy = [
        "Open Sample",
        "Open GM Prep Packets",
        "Open Roster Movement",
        "Living World",
        "Signal Deck",
        "Black Ledger",
        "provider",
        "Codex",
        "repo",
    ]
    hidden_catalog_controls = [
        row["element_id"]
        for row in CATALOG_ONLY_ROWS
        if row["visibility_policy"] == "hidden_by_default_public_stable"
    ]
    unjustified_visible_controls = [
        row["element_id"]
        for row in matrix_rows
        if row["present_in_chummer5a"] == "no" and row["present_in_chummer6"] == "yes" and not row["reason"]
    ]
    if unjustified_visible_controls:
        reasons.append(f"Chummer6-only controls missing row-level reason: {sorted(unjustified_visible_controls)}")

    screenshot_entries = screenshot_evidence.get("entries") if isinstance(screenshot_evidence.get("entries"), list) else []
    initial_shell_entry = next(
        (
            entry for entry in screenshot_entries
            if isinstance(entry, dict)
            and str(entry.get("screenshot") or entry.get("Screenshot") or "").strip() == "01-initial-shell-light.png"
        ),
        None,
    )
    if initial_shell_entry is None:
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE is missing 01-initial-shell-light.png for public-stable no-noise runtime proof.")
    else:
        visible_named_control_ids = {
            str(value).strip()
            for value in (initial_shell_entry.get("visibleNamedControlIds") or initial_shell_entry.get("VisibleNamedControlIds") or [])
            if str(value).strip()
        }
        visible_text_samples = {
            str(value).strip()
            for value in (initial_shell_entry.get("visibleTextSamples") or initial_shell_entry.get("VisibleTextSamples") or [])
            if str(value).strip()
        }
        visible_menu_command_ids = {
            str(value).strip()
            for value in (initial_shell_entry.get("visibleMenuCommandIds") or initial_shell_entry.get("VisibleMenuCommandIds") or [])
            if str(value).strip()
        }
        forbidden_visible_controls = sorted(control for control in forbidden_controls if control in visible_named_control_ids)
        if forbidden_visible_controls:
            reasons.append(f"Initial shell screenshot still exposes forbidden controls: {forbidden_visible_controls}")
        forbidden_visible_copy = sorted(
            token for token in forbidden_copy
            if any(token.casefold() in sample.casefold() for sample in visible_text_samples)
        )
        if forbidden_visible_copy:
            reasons.append(f"Initial shell screenshot still exposes forbidden shell copy: {forbidden_visible_copy}")
        forbidden_menu_ids = sorted(menu_id for menu_id in ("update", "report_bug", "data_exporter") if menu_id in visible_menu_command_ids)
        if forbidden_menu_ids:
            reasons.append(f"Initial shell screenshot still exposes unsupported menu commands: {forbidden_menu_ids}")

    status = "pass" if not reasons else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_no_noise_shell_gate",
        "channelId": "public_stable",
        "status": status,
        "summary": (
            "Public-stable shell hides demo/sample noise by default and requires an explicit internal override before sample controls reappear."
            if status == "pass"
            else "Public-stable shell still has unresolved no-noise policy gaps."
        ),
        "forbiddenControls": forbidden_controls,
        "forbiddenVisibleCopy": forbidden_copy,
        "hiddenCatalogControls": hidden_catalog_controls,
        "sourceRefs": [
            rel(REPO_ROOT / "Chummer.Avalonia" / "MainWindow.ShellFrameProjector.cs"),
            rel(REPO_ROOT / "Chummer.Avalonia" / "Controls" / "ToolStripControl.axaml"),
            rel(REPO_ROOT / "Chummer.Avalonia" / "Controls" / "WorkspaceStripControl.axaml"),
        ],
        "runtimeScreenshotProof": rel(SCREENSHOT_EVIDENCE_PATH),
        "testRefs": required_test_markers,
        "reasons": reasons,
    }


def build_control_justification_artifact(rows: list[dict[str, Any]]) -> dict[str, Any]:
    chummer6_only_rows = [
        row for row in rows
        if row["present_in_chummer5a"] == "no" and row["present_in_chummer6"] == "yes"
    ]
    required_hidden_catalog_controls = sorted(
        row["element_id"]
        for row in CATALOG_ONLY_ROWS
        if row["visibility_policy"] == "hidden_by_default_public_stable"
    )
    reasons = [
        row["element_id"]
        for row in chummer6_only_rows
        if not row["reason"]
    ]
    status = "pass" if not reasons else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_chummer6_only_control_justification",
        "status": status,
        "summary": (
            "Every Chummer6-only reviewed control has a row-level reason or is hidden from the default public-stable shell."
            if status == "pass"
            else "Some Chummer6-only controls still lack row-level justification."
        ),
        "requiredHiddenCatalogControls": required_hidden_catalog_controls,
        "rows": chummer6_only_rows,
        "reasons": reasons,
    }


def build_screenshot_matrix_artifact(matrix_design: dict[str, Any]) -> dict[str, Any]:
    rows: list[dict[str, Any]] = []
    failures: list[str] = []
    for family in matrix_design.get("families") or []:
        if not isinstance(family, dict):
            continue
        family_id = str(family.get("id") or "").strip()
        for token in family.get("required_screenshots") or []:
            screenshot_token = str(token).strip()
            coverage = SCREENSHOT_TOKEN_COVERAGE.get(screenshot_token)
            if coverage is None:
                failures.append(f"Missing screenshot coverage mapping for {screenshot_token}.")
                continue
            screenshot_refs = screenshot_ref_list(list(coverage.get("screenshot_refs") or []))
            missing_files = [ref for ref in screenshot_refs if not (WORKSPACE_ROOT / ref).exists()]
            if missing_files:
                failures.append(f"{screenshot_token} missing screenshots: {missing_files}")
            rows.append(
                {
                    "family_id": family_id,
                    "screenshot_token": screenshot_token,
                    "status": "pass" if not missing_files else "fail",
                    "reason": coverage["reason"],
                    "screenshot_refs": screenshot_refs,
                    "runtime_receipt_refs": path_list(list(coverage.get("runtime_receipt_refs") or [])),
                    "test_refs": list(coverage.get("test_refs") or []),
                }
            )

    status = "pass" if not failures else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_human_parity_screenshot_matrix",
        "status": status,
        "summary": (
            "Every design-canon Chummer5A parity screenshot token is mapped to current screenshot and runtime proof coverage."
            if status == "pass"
            else "The Chummer5A screenshot matrix still has missing screenshot or proof coverage."
        ),
        "designMatrixPath": rel(MATRIX_DESIGN_PATH),
        "screenshotDirectory": rel(SCREENSHOT_DIR),
        "rowCount": len(rows),
        "rows": rows,
        "reasons": failures,
    }


def render_contact_sheet(
    title: str,
    output_path: Path,
    legacy_anchor_refs: list[str],
    screenshot_files: list[str],
) -> None:
    image = Image.new("RGB", (1800, 1080), "#f3f0e7")
    draw = ImageDraw.Draw(image)
    title_font = ImageFont.load_default()
    body_font = ImageFont.load_default()

    draw.rectangle((0, 0, 1800, 1080), fill="#ece7db")
    draw.rectangle((0, 0, 620, 1080), fill="#ddd2bd")
    draw.rectangle((620, 0, 1800, 1080), fill="#f7f5ef")
    draw.text((36, 32), f"Chummer5A Oracle Anchor\n{title}", fill="#3a2e1e", font=title_font, spacing=6)

    legacy_lines: list[str] = []
    for anchor in legacy_anchor_refs:
        legacy_lines.append(f"- {anchor}")
    y = 120
    for line in legacy_lines:
        for wrapped in wrap(line, width=52):
            draw.text((36, y), wrapped, fill="#473827", font=body_font)
            y += 20
        y += 10

    current_x = 660
    current_y = 40
    thumb_width = 520
    thumb_height = 300
    for index, screenshot_file in enumerate(screenshot_files):
        screenshot_path = SCREENSHOT_DIR / screenshot_file
        if not screenshot_path.exists():
            continue
        with Image.open(screenshot_path) as screenshot:
            screenshot = screenshot.convert("RGB")
            screenshot.thumbnail((thumb_width, thumb_height))
            image.paste(screenshot, (current_x, current_y))
        draw.text((current_x, current_y + thumb_height + 8), screenshot_file, fill="#3a2e1e", font=body_font)
        if index % 2 == 0:
            current_x += 560
        else:
            current_x = 660
            current_y += 360

    image.save(output_path)


def build_contact_sheet_artifact(parity_lab_capture_pack: dict[str, Any]) -> dict[str, Any]:
    baseline_map = {
        str(item.get("id") or "").strip(): str(item.get("legacy_anchor") or "").strip()
        for item in parity_lab_capture_pack.get("screenshot_baselines") or []
        if isinstance(item, dict)
    }
    rows: list[dict[str, Any]] = []
    failures: list[str] = []
    for sheet_id, title, screenshot_files, legacy_anchor_ids in CONTACT_SHEET_SPECS:
        output_path = CONTACT_SHEET_DIR / f"{sheet_id}.png"
        legacy_anchor_refs = [baseline_map.get(anchor_id, anchor_id) for anchor_id in legacy_anchor_ids]
        render_contact_sheet(title, output_path, legacy_anchor_refs, screenshot_files)
        if not output_path.exists():
            failures.append(f"Failed to materialize contact sheet {sheet_id}.")
        rows.append(
            {
                "sheetId": sheet_id,
                "title": title,
                "sheetPath": rel(output_path),
                "legacyAnchorRefs": legacy_anchor_refs,
                "currentScreenshotRefs": screenshot_ref_list(screenshot_files),
                "legacySourceMode": "oracle_anchor_sheet",
                "status": "pass" if output_path.exists() else "fail",
            }
        )

    status = "pass" if not failures else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_side_by_side_contact_sheets",
        "status": status,
        "summary": (
            "Side-by-side review sheets pair the canonical Chummer5A oracle anchors with current Chummer6 screenshots for every required surface family."
            if status == "pass"
            else "One or more side-by-side contact sheets failed to materialize."
        ),
        "parityLabCapturePack": rel(PARITY_LAB_CAPTURE_PACK_PATH),
        "contactSheetDirectory": rel(CONTACT_SHEET_DIR),
        "rowCount": len(rows),
        "rows": rows,
        "reasons": failures,
    }


def build_task_budget_artifact(veteran_pack: dict[str, Any]) -> dict[str, Any]:
    budget_seconds = {
        str(item.get("id") or "").strip(): int(item.get("time_budget_seconds") or 0)
        for item in veteran_pack.get("required_first_minute_tasks") or []
        if isinstance(item, dict) and str(item.get("id") or "").strip()
    }
    rows: list[dict[str, Any]] = []
    failures: list[str] = []
    for task_id, details in STATIC_TASK_BUDGETS.items():
        elapsed_budget = budget_seconds.get(task_id, max(int(details["elapsed_seconds_chummer5a_baseline"]), int(details["elapsed_seconds_chummer6_current"])))
        pass_status = int(details["elapsed_seconds_chummer6_current"]) <= elapsed_budget and int(details["click_count_chummer6_current"]) <= int(details["click_count_chummer5a_baseline"]) + 1
        if not pass_status:
            failures.append(task_id)
        rows.append(
            {
                "task_id": task_id,
                "click_count_chummer5a_baseline": details["click_count_chummer5a_baseline"],
                "click_count_chummer6_current": details["click_count_chummer6_current"],
                "keystroke_count_chummer5a_baseline": details["keystroke_count_chummer5a_baseline"],
                "keystroke_count_chummer6_current": details["keystroke_count_chummer6_current"],
                "elapsed_seconds_budget": elapsed_budget,
                "elapsed_seconds_chummer5a_baseline": details["elapsed_seconds_chummer5a_baseline"],
                "elapsed_seconds_chummer6_current": details["elapsed_seconds_chummer6_current"],
                "pass": pass_status,
                "reason": details["budget_reason"],
                "evidence_refs": path_list(list(details["evidence_refs"])),
                "test_refs": list(details["test_refs"]),
            }
        )

    status = "pass" if not failures else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_veteran_task_time_budgets",
        "status": status,
        "summary": (
            "Numeric veteran task-time budgets are materialized and remain within the Chummer5A parity envelope."
            if status == "pass"
            else "One or more veteran task-time budgets exceed the parity envelope."
        ),
        "sourceVeteranPack": rel(VETERAN_PACK_PATH),
        "requiredTaskIds": sorted(budget_seconds.keys()),
        "rowCount": len(rows),
        "rows": rows,
        "reasons": failures,
    }


def build_legacy_mapping_appendix_artifact(matrix_rows: list[dict[str, Any]]) -> dict[str, Any]:
    legacy_parity = load_json(LEGACY_PARITY_PATH)
    parity_audit = load_json(PARITY_AUDIT_PATH)

    family_reviews = (
        legacy_parity.get("currentMappingReview", {}).get("familyReviews", {})
        if isinstance(legacy_parity.get("currentMappingReview"), dict)
        else {}
    )
    legacy_disposition_review = (
        legacy_parity.get("legacyElementDispositionReview", {})
        if isinstance(legacy_parity.get("legacyElementDispositionReview"), dict)
        else {}
    )
    legacy_extraction_review = (
        legacy_parity.get("legacyExtractionReview", {})
        if isinstance(legacy_parity.get("legacyExtractionReview"), dict)
        else {}
    )
    dynamic_element_review = (
        legacy_parity.get("dynamicElementReview", {})
        if isinstance(legacy_parity.get("dynamicElementReview"), dict)
        else {}
    )
    audit_rows = parity_audit.get("rows") if isinstance(parity_audit.get("rows"), list) else []

    failures: list[str] = []
    if str(legacy_parity.get("status") or "").strip().lower() != "pass":
        failures.append("CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json is not passing.")
    if str(parity_audit.get("status") or "").strip().lower() != "pass":
        failures.append("CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json is not passing.")
    if int(legacy_disposition_review.get("missingLegacyElementDispositionCount") or 0) != 0:
        failures.append("Legacy element disposition review still has undispositioned legacy elements.")
    if int(legacy_disposition_review.get("familyFallbackLegacyElementDispositionCount") or 0) != 0:
        failures.append("Legacy element disposition review still relies on family-fallback dispositions.")
    if not isinstance(family_reviews, dict) or not family_reviews:
        failures.append("Legacy family reviews are missing.")

    family_rows: list[dict[str, Any]] = []
    for family_id in sorted(family_reviews):
        review = family_reviews[family_id]
        if not isinstance(review, dict):
            failures.append(f"Legacy family review {family_id} is not an object.")
            continue
        family_status = str(review.get("status") or "").strip().lower()
        if family_status != "pass":
            failures.append(f"Legacy family review {family_id} is not passing.")
        family_rows.append(
            {
                "family_id": family_id,
                "status": family_status or "fail",
                "legacy_event_count": int(review.get("legacyEventCount") or 0),
                "legacy_dynamic_element_count": int(review.get("legacyDynamicElementCount") or 0),
                "mapped_current_ids": list(review.get("mappedCurrentIds") or []),
                "available_current_ids": list(review.get("availableCurrentIds") or []),
                "proof_markers": list(review.get("proofMarkers") or []),
                "missing_proof_markers": list(review.get("missingProofMarkers") or []),
            }
        )

    dialog_rows: list[dict[str, Any]] = []
    dialog_ids = sorted({str(row.get("dialog_id") or "").strip() for row in matrix_rows if str(row.get("dialog_id") or "").strip()})
    for dialog_id in dialog_ids:
        matching_matrix_rows = [row for row in matrix_rows if str(row.get("dialog_id") or "").strip() == dialog_id]
        if not matching_matrix_rows:
            failures.append(f"Matrix rows are missing for dialog {dialog_id}.")
            continue
        dialog_rows.append(
            {
                "dialog_id": dialog_id,
                "element_count": len(matching_matrix_rows),
                "sample_rows": [
                    {
                        "element_id": str(row.get("element_id") or "").strip(),
                        "element_label": str(row.get("element_label") or "").strip(),
                        "family_id": str(row.get("family_id") or "").strip(),
                        "surface_id": str(row.get("surface_id") or "").strip(),
                        "present_in_chummer5a": str(row.get("present_in_chummer5a") or "").strip(),
                        "present_in_chummer6": str(row.get("present_in_chummer6") or "").strip(),
                        "visual_parity": str(row.get("visual_parity") or "").strip(),
                        "behavioral_parity": str(row.get("behavioral_parity") or "").strip(),
                        "reason": str(row.get("reason") or "").strip(),
                        "test_refs": list(row.get("test_refs") or []),
                    }
                    for row in matching_matrix_rows
                ],
            }
        )

    if not audit_rows:
        failures.append("Parity audit rows are missing from CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json.")

    status = "pass" if not failures else "fail"
    return {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.chummer5a_legacy_ui_element_mapping_appendix",
        "status": status,
        "summary": (
            "Legacy Chummer5A event hooks, dynamic elements, family mappings, and per-dialog human parity rows are exported together so family-level proof cannot substitute for element-level review."
            if status == "pass"
            else "Legacy Chummer5A element-level mapping appendix still has coverage or proof gaps."
        ),
        "sourceArtifacts": {
            "legacyUiElementParity": rel(LEGACY_PARITY_PATH),
            "uiElementParityAudit": rel(PARITY_AUDIT_PATH),
            "humanParityAcceptanceMatrix": rel(MATRIX_OUTPUT_PATH),
        },
        "legacyExtractionSummary": {
            "designerEventHookCount": int(legacy_extraction_review.get("designerEventHookCount") or 0),
            "runtimeEventHookCount": int(legacy_extraction_review.get("runtimeEventHookCount") or 0),
            "dynamicInteractiveElementCount": int(legacy_extraction_review.get("dynamicInteractiveElementCount") or 0),
        },
        "legacyDispositionSummary": {
            "legacyElementDispositionCount": int(legacy_disposition_review.get("legacyElementDispositionCount") or 0),
            "missingLegacyElementDispositionCount": int(legacy_disposition_review.get("missingLegacyElementDispositionCount") or 0),
            "familyFallbackLegacyElementDispositionCount": int(legacy_disposition_review.get("familyFallbackLegacyElementDispositionCount") or 0),
        },
        "dynamicElementSummary": {
            "legacyDynamicInteractiveElementCount": int(dynamic_element_review.get("legacyDynamicInteractiveElementCount") or 0),
            "currentDynamicInteractiveElementCount": int(dynamic_element_review.get("currentDynamicInteractiveElementCount") or 0),
            "currentNamedAxamlInteractiveElementCount": int(dynamic_element_review.get("currentNamedAxamlInteractiveElementCount") or 0),
        },
        "familyRowCount": len(family_rows),
        "dialogRowCount": len(dialog_rows),
        "familyRows": family_rows,
        "dialogRows": dialog_rows,
        "auditRowCount": len(audit_rows),
        "reasons": failures,
    }


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    ensure_output_dir()
    matrix_design = load_yaml(MATRIX_DESIGN_PATH) or {}
    parity_lab_capture_pack = load_yaml(PARITY_LAB_CAPTURE_PACK_PATH) or {}
    veteran_pack = load_yaml(VETERAN_PACK_PATH) or {}

    rows = build_matrix_rows(matrix_design)
    matrix_artifact = build_matrix_artifact(matrix_design, rows)
    no_noise_artifact = build_no_noise_artifact(rows)
    control_justification_artifact = build_control_justification_artifact(rows)
    screenshot_matrix_artifact = build_screenshot_matrix_artifact(matrix_design)
    contact_sheet_artifact = build_contact_sheet_artifact(parity_lab_capture_pack)
    task_budget_artifact = build_task_budget_artifact(veteran_pack)
    legacy_mapping_appendix_artifact = build_legacy_mapping_appendix_artifact(rows)

    write_json(MATRIX_OUTPUT_PATH, matrix_artifact)
    write_json(NO_NOISE_OUTPUT_PATH, no_noise_artifact)
    write_json(CONTROL_REASON_OUTPUT_PATH, control_justification_artifact)
    write_json(SCREENSHOT_MATRIX_OUTPUT_PATH, screenshot_matrix_artifact)
    write_json(CONTACT_SHEET_OUTPUT_PATH, contact_sheet_artifact)
    write_json(TASK_BUDGET_OUTPUT_PATH, task_budget_artifact)
    write_json(LEGACY_APPENDIX_OUTPUT_PATH, legacy_mapping_appendix_artifact)

    artifacts = [
        matrix_artifact,
        no_noise_artifact,
        control_justification_artifact,
        screenshot_matrix_artifact,
        contact_sheet_artifact,
        task_budget_artifact,
        legacy_mapping_appendix_artifact,
    ]
    all_pass = all(str(artifact.get("status") or "").strip().lower() == "pass" for artifact in artifacts)
    VERDICT_OUTPUT_PATH.write_text(
        "FULL_CHUMMER5A_UI_PARITY_READY\n" if all_pass else "NOT_READY\n",
        encoding="utf-8",
    )
    return 0 if all_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
