from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import yaml


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
MODULE_PATH = REPO_ROOT / "scripts" / "chummer5a_parity_tester.py"
SPEC = importlib.util.spec_from_file_location("chummer5a_parity_tester", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise ImportError(f"Unable to load module from {MODULE_PATH}")
tester = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = tester
SPEC.loader.exec_module(tester)


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def write_yaml(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(yaml.safe_dump(payload, sort_keys=False), encoding="utf-8")


def minimal_chum5(path: Path, name: str, *, include_cyberware: bool = False) -> None:
    cyberware_block = "<cyberwares><cyberware><name>Wired Reflexes</name></cyberware></cyberwares>" if include_cyberware else "<cyberwares />"
    content = f"""<character>
  <name>{name}</name>
  <prioritymetatype>A</prioritymetatype>
  <priorityattributes>B</priorityattributes>
  <priorityspecial>C</priorityspecial>
  <priorityskills>D</priorityskills>
  <priorityresources>E</priorityresources>
  <attributes><attribute><name>BOD</name></attribute></attributes>
  <skills><skill><name>Automatics</name></skill></skills>
  <qualities><quality><name>Focused Concentration</name></quality></qualities>
  <contacts><contact><name>Fixer</name></contact></contacts>
  <lifestyles><lifestyle><name>Low</name></lifestyle></lifestyles>
  <notes>note</notes>
  <gears><gear><name>Armor Jacket</name></gear></gears>
  <improvements><improvement><name>Test</name></improvement></improvements>
  {cyberware_block}
</character>
"""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


class SelectDefaultFixturesTests(unittest.TestCase):
    def test_select_default_fixtures_uses_preferred_names(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            repo = Path(tmpdir)
            fixture_dir = repo / "Chummer.Tests" / "TestFiles"
            for filename in (
                "Bastion.chum5",
                "Fuzzy-chargen.chum5",
                "Soma.chum5",
                "Popstar.chum5",
                "Wesson.chum5",
            ):
                minimal_chum5(fixture_dir / filename, filename.replace(".chum5", ""), include_cyberware=filename == "Soma.chum5")

            fixtures = tester.select_default_fixtures(repo)

        self.assertEqual(
            [fixture.fixture_name for fixture in fixtures],
            ["Bastion.chum5", "Fuzzy-chargen.chum5", "Soma.chum5", "Popstar.chum5", "Wesson.chum5"],
        )
        self.assertIn("cyberware-bioware-modular-hierarchies-nested-plugins", fixtures[2].workflow_family_ids)


class ImmutableJsonLoaderTests(unittest.TestCase):
    def test_rejects_oversized_json_before_parsing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            path = Path(tmpdir) / "oversized.json"
            path.write_bytes(b"{" + (b" " * tester.IMMUTABLE_JSON_MAX_BYTES) + b"}")

            payload, raw, reasons = tester.try_load_immutable_json(path, "test evidence")

        self.assertEqual(payload, {})
        self.assertEqual(raw, b"")
        self.assertTrue(any("byte safety limit" in reason for reason in reasons))

    def test_rejects_non_regular_input(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            path = Path(tmpdir) / "directory.json"
            path.mkdir()

            payload, raw, reasons = tester.try_load_immutable_json(path, "test evidence")

        self.assertEqual(payload, {})
        self.assertEqual(raw, b"")
        self.assertTrue(any("stable regular non-symlink file" in reason for reason in reasons))

    def test_rejects_path_replacement_during_descriptor_read(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            root = Path(tmpdir)
            path = root / "evidence.json"
            replacement = root / "replacement.json"
            write_json(path, {"status": "pass"})
            write_json(replacement, {"status": "fail"})
            real_read = tester.os.read
            replaced = False

            def replacing_read(descriptor: int, size: int) -> bytes:
                nonlocal replaced
                chunk = real_read(descriptor, size)
                if chunk and not replaced:
                    os.replace(replacement, path)
                    replaced = True
                return chunk

            with mock.patch.object(tester.os, "read", side_effect=replacing_read):
                payload, raw, reasons = tester.try_load_immutable_json(path, "test evidence")

        self.assertTrue(replaced)
        self.assertEqual(payload, {})
        self.assertEqual(raw, b"")
        self.assertTrue(any("changed while being read" in reason for reason in reasons))


class RunGateTests(unittest.TestCase):
    def build_args(self, tmpdir: str, *, visual_status: str = "pass", omit_workflow: bool = False, stale_screenshot_root: bool = False) -> SimpleNamespace:
        root = Path(tmpdir)
        repo = root / "chummer5a"
        fixture_dir = repo / "Chummer.Tests" / "TestFiles"
        fixture_names = ("Bastion.chum5", "Fuzzy-chargen.chum5", "Soma.chum5", "Wesson.chum5", "Popstar.chum5")
        for filename in fixture_names:
            minimal_chum5(fixture_dir / filename, filename.replace(".chum5", ""), include_cyberware=filename == "Soma.chum5")
        subprocess_git = __import__("subprocess")
        subprocess_git.run(["git", "-C", str(repo), "init"], check=True, capture_output=True)
        subprocess_git.run(["git", "-C", str(repo), "config", "user.email", "tester@example.com"], check=True, capture_output=True)
        subprocess_git.run(["git", "-C", str(repo), "config", "user.name", "Tester"], check=True, capture_output=True)
        subprocess_git.run(["git", "-C", str(repo), "add", "."], check=True, capture_output=True)
        subprocess_git.run(["git", "-C", str(repo), "commit", "-m", "fixtures"], check=True, capture_output=True)

        screenshot_dir = root / "screenshots"
        screenshot_dir.mkdir(parents=True, exist_ok=True)
        screenshot_evidence_path = screenshot_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
        for filename in (
            "01-initial-shell-light.png",
            "02-menu-open-light.png",
            "03-settings-open-light.png",
            "04-loaded-runner-light.png",
            "05-dense-section-light.png",
            "07-loaded-runner-tabs-light.png",
            "10-contacts-section-light.png",
            "14-advancement-dialog-light.png",
            "15-creation-section-light.png",
            "16-master-index-dialog-light.png",
            "17-character-roster-dialog-light.png",
            "18-import-dialog-light.png",
            "19-workflow-file-menu-loaded-light.png",
            "20-workflow-skills-section-light.png",
            "24-workflow-gear-section-light.png",
            "30-workflow-cyberware-section-light.png",
            "34-workflow-validate-section-light.png",
        ):
            (screenshot_dir / filename).write_bytes(b"png")

        entries = [
            {
                "screenshot": "01-initial-shell-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["File", "Tools", "Windows", "Help", "New Character", "Section Payload", "Service: online", "Ruleset:", "Time:"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "02-menu-open-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["File", "Tools", "Windows", "Help", "Open Character", "Save Character", "Service: online", "Ruleset:", "Time:"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "03-settings-open-light.png",
                "dialogTitle": "Global Settings",
                "visibleTextSamples": ["Global Settings", "Modify..."],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "04-loaded-runner-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["File", "Tools", "Windows", "Help", "Profile"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "05-dense-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Windows", "Help", "Service: online", "Ruleset:", "Time:"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "07-loaded-runner-tabs-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Windows", "Help", "Service: online", "Ruleset:", "Time:"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": ["Runner"],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "10-contacts-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Contacts"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "14-advancement-dialog-light.png",
                "dialogTitle": "Advancement",
                "visibleTextSamples": ["Advancement"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "15-creation-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Creation"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "16-master-index-dialog-light.png",
                "dialogTitle": "Master Index",
                "visibleTextSamples": ["Master Index"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "17-character-roster-dialog-light.png",
                "dialogTitle": "Character Roster",
                "visibleTextSamples": ["Character Roster", "Tools"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "18-import-dialog-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Open Character", "Import"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "19-workflow-file-menu-loaded-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["File"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "20-workflow-skills-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Skills"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "24-workflow-gear-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Gear"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "30-workflow-cyberware-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Cyberware"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
            {
                "screenshot": "34-workflow-validate-section-light.png",
                "dialogTitle": "",
                "visibleTextSamples": ["Validate"],
                "visibleMenuCommandIds": [],
                "visibleTabLabels": [],
                "visibleSectionQuickActionIds": [],
                "visibleNamedControlIds": [],
                "selectedListRowTexts": [],
                "previewText": "",
            },
        ]
        workflow_coverage = [
            {
                "workflowFamilyId": "create-open-import-save-save-as-print-export",
                "legacyBehaviorLineage": "File menu lineage",
                "screenshotFiles": ["19-workflow-file-menu-loaded-light.png", "18-import-dialog-light.png"],
            },
            {
                "workflowFamilyId": "attributes-skills-skill-groups-specializations-knowledge-languages",
                "legacyBehaviorLineage": "Skills lineage",
                "screenshotFiles": ["20-workflow-skills-section-light.png"],
            },
            {
                "workflowFamilyId": "recovery-reload-migration-roundtrips",
                "legacyBehaviorLineage": "Reload lineage",
                "screenshotFiles": ["04-loaded-runner-light.png"],
            },
            {
                "workflowFamilyId": "metatype-priorities-karma-entry",
                "legacyBehaviorLineage": "Creation lineage",
                "screenshotFiles": ["15-creation-section-light.png"],
            },
            {
                "workflowFamilyId": "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
                "legacyBehaviorLineage": "Gear lineage",
                "screenshotFiles": ["24-workflow-gear-section-light.png"],
            },
            {
                "workflowFamilyId": "cyberware-bioware-modular-hierarchies-nested-plugins",
                "legacyBehaviorLineage": "Cyberware lineage",
                "screenshotFiles": ["30-workflow-cyberware-section-light.png"],
            },
            {
                "workflowFamilyId": "improvements-explain-result-parity",
                "legacyBehaviorLineage": "Validation lineage",
                "screenshotFiles": ["34-workflow-validate-section-light.png"],
            },
            {
                "workflowFamilyId": "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
                "legacyBehaviorLineage": "Dense workbench lineage",
                "screenshotFiles": ["05-dense-section-light.png"],
            },
            {
                "workflowFamilyId": "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
                "legacyBehaviorLineage": "Contacts lineage",
                "screenshotFiles": ["10-contacts-section-light.png"],
            },
        ]
        if not omit_workflow:
            workflow_coverage.append(
                {
                    "workflowFamilyId": "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
                    "legacyBehaviorLineage": "Magic lineage",
                    "screenshotFiles": ["14-advancement-dialog-light.png"],
                }
            )
        screenshot_directory_value = str(screenshot_dir)
        if stale_screenshot_root:
            screenshot_directory_value = str(root / "stale-screenshot-root")
        write_json(
            screenshot_evidence_path,
            {
                "contractName": "test.screenshot.evidence",
                "screenshotDirectory": screenshot_directory_value,
                "entries": entries,
                "workflowCoverage": workflow_coverage,
            },
        )
        write_yaml(
            root / "import_export_fixture_inventory.yaml",
            {
                "counts": {"tabs": 17, "workspace_actions": 47, "desktop_controls": 29},
            },
        )
        write_yaml(
            root / "oracle_baselines.yaml",
            {
                "screenshot_baselines": [
                    {"id": "initial_shell", "filename": "01-initial-shell-light.png"},
                    {"id": "loaded_runner", "filename": "04-loaded-runner-light.png"},
                    {"id": "creation_section", "filename": "15-creation-section-light.png"},
                    {"id": "menu_open", "filename": "02-menu-open-light.png"},
                    {"id": "settings_open", "filename": "03-settings-open-light.png"},
                    {"id": "master_index_dialog", "filename": "16-master-index-dialog-light.png"},
                    {"id": "character_roster_dialog", "filename": "17-character-roster-dialog-light.png"},
                    {"id": "loaded_runner_tabs", "filename": "07-loaded-runner-tabs-light.png"},
                    {"id": "dense_section_light", "filename": "05-dense-section-light.png"},
                    {"id": "contacts_section", "filename": "10-contacts-section-light.png"},
                    {"id": "advancement_dialog", "filename": "14-advancement-dialog-light.png"},
                ]
            },
        )
        write_yaml(
            root / "workflow_pack.yaml",
            {
                "desktop_non_negotiables_asserted": {
                    "no_generic_shell_or_dashboard_first": True,
                    "startup_is_workbench_or_restore": True,
                    "file_menu_live": True,
                },
                "task_packs": [
                    {
                        "task_id": "reach_real_workbench",
                        "landmarks": ["File menu", "Immediate toolstrip", "Bottom status strip"],
                        "screenshot_baseline_ids": ["initial_shell", "loaded_runner", "creation_section"],
                    },
                    {
                        "task_id": "locate_save_import_settings",
                        "landmarks": ["Save or open route", "Settings route"],
                        "screenshot_baseline_ids": ["menu_open", "settings_open"],
                    },
                    {
                        "task_id": "locate_master_index_and_roster",
                        "landmarks": ["Master index route", "Character roster route", "Tools menu"],
                        "screenshot_baseline_ids": ["master_index_dialog", "character_roster_dialog"],
                    },
                    {
                        "task_id": "recover_section_rhythm",
                        "landmarks": ["Windows menu", "Help menu", "Bottom status strip"],
                        "screenshot_baseline_ids": ["loaded_runner_tabs", "dense_section_light", "contacts_section", "advancement_dialog"],
                    },
                ],
            },
        )
        for filename in (
            "create-open-import-save-save-as-print-export.generated.json",
            "attributes-skills-skill-groups-specializations-knowledge-languages.generated.json",
            "recovery-reload-migration-roundtrips.generated.json",
            "metatype-priorities-karma-entry.generated.json",
            "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers.generated.json",
            "cyberware-bioware-modular-hierarchies-nested-plugins.generated.json",
            "improvements-explain-result-parity.generated.json",
            "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare.generated.json",
            "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources.generated.json",
            "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms.generated.json",
        ):
            if omit_workflow and filename.startswith("magic-adept-resonance"):
                continue
            family_id = filename.removesuffix(".generated.json")
            executed_receipt_path = root / "workflow-family-parity" / "executed" / "sr6" / filename
            write_json(
                root / "workflow-family-parity" / "sr6" / filename,
                {
                    "generatedAt": "2026-05-02T00:00:00Z",
                    "contract_name": tester.WORKFLOW_VERIFICATION_CONTRACT,
                    "status": "pass",
                    "summary": f"SR6 workflow-family verification evidence is explicitly grounded for {family_id}.",
                    "reasons": [],
                    "evidence": {
                        "edition": "sr6",
                        "familyId": family_id,
                        "proofKind": "sr6_family_carry_forward",
                        "ledgerPath": str(root / "SR6_WORKFLOW_PARITY_LEDGER.json"),
                        "oraclePath": str(root / "SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"),
                        "auditTests": [f"{family_id}.AuditTest"],
                        "executionReceipts": [str(executed_receipt_path)],
                        "executionFailures": [],
                        "executionExternalBlockers": [],
                    },
                },
            )
        executed_dir = root / "workflow-family-parity" / "executed" / "sr6"
        for family_id in (
            "create-open-import-save-save-as-print-export",
            "attributes-skills-skill-groups-specializations-knowledge-languages",
            "recovery-reload-migration-roundtrips",
            "metatype-priorities-karma-entry",
            "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
            "cyberware-bioware-modular-hierarchies-nested-plugins",
            "improvements-explain-result-parity",
            "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
            "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
            "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
        ):
            if omit_workflow and family_id.startswith("magic-adept-resonance"):
                continue
            write_json(
                executed_dir / f"{family_id}.generated.json",
                {
                    "generatedAt": "2026-05-02T00:00:00Z",
                    "contract_name": tester.EXECUTED_WORKFLOW_CONTRACT,
                    "status": "pass",
                    "summary": f"SR6 workflow-family execution evidence is explicitly grounded for {family_id}.",
                    "reasons": [],
                    "evidence": {
                        "edition": "sr6",
                        "familyId": family_id,
                        "proofKind": "sr6_family_release_gated_execution",
                        "dotnetTest": {"exitCode": 0},
                        "matchedPassedTests": [f"{family_id}.AuditTest"],
                        "missingAuditTests": [],
                        "failedAuditTests": {},
                        "external_blocker": "",
                    },
                },
            )

        journey_screenshot_dir = root / "user-journey-tester-screenshots"
        png_bytes = b"\x89PNG\r\n\x1a\nproof"
        journey_workflows = [
            {
                "id": "master_index_search_focus_stability",
                "assertions": {
                    "focus_preserved_after_typing": True,
                    "search_text_accumulates_keyboard_input": True,
                },
            },
            {
                "id": "file_new_character_visible_workspace",
                "assertions": {
                    "new_character_action_opened_visible_workspace": True,
                    "visible_workspace_nonblank": True,
                    "starter_attributes_match_seeded_workspace": True,
                    "section_preview_omits_review_copy": True,
                },
            },
            {
                "id": "minimal_character_build_save_reload",
                "assertions": {
                    "character_created_saved_reloaded": True,
                    "reload_preserved_character_identity": True,
                },
            },
            {
                "id": "major_navigation_sanity",
                "assertions": {
                    "primary_navigation_clicks_change_visible_content": True,
                    "no_unhandled_errors": True,
                },
            },
            {
                "id": "validation_or_export_smoke",
                "assertions": {
                    "validation_or_export_action_completed": True,
                    "result_visible_or_file_created": True,
                },
            },
        ]
        workflow_rows = []
        for row in journey_workflows:
            screenshots = [f"{row['id']}-before.png", f"{row['id']}-after.png"]
            for screenshot in screenshots:
                path = journey_screenshot_dir / screenshot
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(png_bytes)
            workflow_rows.append(
                {
                    "id": row["id"],
                    "status": "pass",
                    "screenshots": screenshots,
                    "assertions": row["assertions"],
                }
            )
        trace_path = root / "USER_JOURNEY_TESTER_TRACE.generated.json"
        trace_generated_at_utc = (
            datetime.now(timezone.utc)
            .replace(microsecond=0)
            .isoformat()
            .replace("+00:00", "Z")
        )
        write_json(
            trace_path,
            {
                "contract_name": tester.USER_JOURNEY_TRACE_CONTRACT,
                "status": "pass",
                "generated_at_utc": trace_generated_at_utc,
                "tester_shard_id": "tester-shard",
                "fix_shard_id": "fixer-shard",
                "linux_binary_under_test": True,
                "used_internal_apis": False,
                "open_blocking_findings": [],
                "workflows": workflow_rows,
            },
        )
        trace_sha256 = hashlib.sha256(trace_path.read_bytes()).hexdigest()
        write_json(
            root / "USER_JOURNEY_TESTER_AUDIT.generated.json",
            {
                "contract_name": tester.USER_JOURNEY_AUDIT_CONTRACT,
                "status": "pass",
                "generated_at": trace_generated_at_utc,
                "generatedAt": trace_generated_at_utc,
                "reasons": [],
                "open_blocking_findings_count": 0,
                "linux_binary_under_test": True,
                "used_internal_apis": False,
                "fix_shard_separate": True,
                "trace_mutation_requested": False,
                "trace_mutation_performed": False,
                "evidence": {
                    "trace_path": str(trace_path),
                    "trace_sha256": trace_sha256,
                    "trace_sha256_after_audit": trace_sha256,
                    "trace_bytes_unchanged_during_audit": True,
                    "trace_mutation_requested": False,
                    "trace_mutation_allowed": False,
                    "trace_mutation_performed": False,
                    "trace_generated_at_utc": trace_generated_at_utc,
                    "trace_max_age_hours": tester.USER_JOURNEY_TRACE_MAX_AGE_HOURS,
                    "trace_future_skew_minutes": tester.USER_JOURNEY_TRACE_FUTURE_SKEW_MINUTES,
                    "linux_gate_path": str(root / "UI_LINUX_DESKTOP_EXIT_GATE.generated.json"),
                    "screenshot_dir": str(journey_screenshot_dir),
                    "linux_gate_status": "pass",
                    "tester_shard_id": "tester-shard",
                    "fix_shard_id": "fixer-shard",
                    "required_workflows": list(tester.USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS),
                    "required_workflow_assertions": {
                        workflow_id: list(assertions)
                        for workflow_id, assertions in tester.USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS.items()
                    },
                    "workflows": [
                        {
                            "id": row["id"],
                            "status": row["status"],
                            "assertions": row["assertions"],
                            "screenshots": row["screenshots"],
                            "screenshotReview": [
                                {
                                    "path": str(journey_screenshot_dir / screenshot),
                                    "exists": True,
                                    "within_repo_root": True,
                                    "is_png": True,
                                    "sha256": f"{row['id']}-{index}",
                                }
                                for index, screenshot in enumerate(row["screenshots"], start=1)
                            ],
                        }
                        for row in workflow_rows
                    ],
                    "missing_workflows": [],
                    "nonpassing_workflows": [],
                    "insufficient_screenshot_workflows": [],
                    "missing_assertion_workflows": {},
                    "open_blocking_findings_count": 0,
                    "used_internal_apis": False,
                    "fix_shard_separate": True,
                    "linux_binary_under_test": True,
                    "run_linux_gate_requested": False,
                },
            },
        )
        reconstruction_dir = root / "chummer5a-fixture-ui-reconstruction"
        for fixture_name in fixture_names:
            screenshots = [
                f"{fixture_name}-opened.png",
                f"{fixture_name}-export-dialog.png",
                f"{fixture_name}-printed.png",
                f"{fixture_name}-reloaded.png",
            ]
            for screenshot in screenshots:
                (reconstruction_dir / screenshot).parent.mkdir(parents=True, exist_ok=True)
                (reconstruction_dir / screenshot).write_bytes(png_bytes)
            saved_file = reconstruction_dir / f"{fixture_name}.roundtrip.chum5"
            export_file = reconstruction_dir / f"{fixture_name}.export.json"
            print_preview_file = reconstruction_dir / f"{fixture_name}.print.html"
            pdf_artifact_file = reconstruction_dir / f"{fixture_name}.print.pdf"
            saved_file.write_text("<character />", encoding="utf-8")
            export_file.write_text("{\"summary\":true}", encoding="utf-8")
            print_preview_file.write_text("<html><body>Runner</body></html>", encoding="utf-8")
            pdf_artifact_file.write_bytes(b"%PDF-1.4\n%mock\n")
            write_json(
                reconstruction_dir / f"{fixture_name}.generated.json",
                {
                    "contract_name": tester.FIXTURE_RECONSTRUCTION_CONTRACT,
                    "status": "pass",
                    "fixtureName": fixture_name,
                    "characterName": fixture_name.replace(".chum5", ""),
                    "linux_binary_under_test": True,
                    "used_internal_apis": False,
                    "screenshots": screenshots,
                    "assertions": {
                        "openedByUi": True,
                        "savedByUi": True,
                        "exportedByUi": True,
                        "printedByUi": True,
                        "pdfArtifactProducedByUiPrintRoute": True,
                        "outputArtifactsProducedByUi": True,
                        "reloadedByUi": True,
                        "roundTripPreservedIdentity": True,
                    },
                    "evidence": {
                        "savedFilePath": str(saved_file),
                        "exportFilePath": str(export_file),
                        "printPreviewFilePath": str(print_preview_file),
                        "pdfArtifactPath": str(pdf_artifact_file),
                    },
                },
            )

        write_json(root / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json", {"status": visual_status, "summary": "visual"})
        write_json(root / "UI_FLAGSHIP_RELEASE_GATE.generated.json", {"status": "pass", "summary": "flagship"})
        write_json(root / "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json", {"status": "pass", "summary": "executable"})
        write_json(root / "UI_LINUX_DESKTOP_EXIT_GATE.generated.json", {"status": "pass", "summary": "linux"})
        write_json(
            root / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
            {
                "generatedAt": "2026-05-02T00:00:00Z",
                "contract_name": tester.CHUMMER5A_DESKTOP_WORKFLOW_PARITY_CONTRACT,
                "status": "pass",
                "summary": "Chummer5a desktop workflow parity is explicitly proven.",
                "reasons": [],
                "evidence": {
                    "failureCount": 0,
                    "requiredFamilyCount": 10,
                },
                "workflowFamilyReview": {"status": "pass", "summary": "families ready"},
                "recursiveWorkflowGateReview": {"status": "pass", "summary": "recursive parity ready"},
                "checklistCoverageReview": {"status": "pass", "summary": "coverage ready"},
            },
        )
        write_json(
            root / "GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
            {
                "generatedAt": "2026-05-02T00:00:00Z",
                "contract_name": tester.GENERATED_DIALOG_ELEMENT_PARITY_CONTRACT,
                "status": "pass",
                "summary": "Generated dialog command/control inventories are locked and tested.",
                "reasons": [],
                "inventoryReview": {"status": "pass", "summary": "inventory ready"},
                "executionReview": {"status": "pass", "summary": "execution ready"},
                "verifyWiringReview": {"status": "pass", "summary": "verify wiring ready"},
            },
        )
        write_json(
            root / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
            {
                "generated_at": "2026-05-02T00:00:00Z",
                "status": "pass",
                "summary": {
                    "total_elements": 84,
                    "visual_no_count": 0,
                    "behavioral_no_count": 0,
                    "coverage_gap_keys": [],
                },
            },
        )

        return SimpleNamespace(
            chummer5a_repo=repo,
            chummer5a_ref="HEAD",
            fixture_inventory=root / "import_export_fixture_inventory.yaml",
            oracle_baselines=root / "oracle_baselines.yaml",
            workflow_pack=root / "workflow_pack.yaml",
            screenshot_evidence=screenshot_evidence_path,
            workflow_parity_dir=root / "workflow-family-parity" / "sr6",
            executed_workflow_parity_dir=executed_dir,
            visual_gate=root / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
            flagship_gate=root / "UI_FLAGSHIP_RELEASE_GATE.generated.json",
            executable_gate=root / "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json",
            user_journey_trace=root / "USER_JOURNEY_TESTER_TRACE.generated.json",
            user_journey_audit=root / "USER_JOURNEY_TESTER_AUDIT.generated.json",
            user_journey_screenshot_dir=journey_screenshot_dir,
            reconstruction_receipts_dir=reconstruction_dir,
            desktop_workflow_parity_receipt=root / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
            generated_dialog_element_parity_receipt=root / "GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
            ui_element_parity_audit_receipt=root / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
            artifacts=root / "artifacts",
            resolution="1920x1080",
            scale="1.0",
            fixture=None,
        )

    def rebind_user_journey_audit_to_current_trace(self, args: SimpleNamespace) -> None:
        trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
        trace_sha256 = hashlib.sha256(args.user_journey_trace.read_bytes()).hexdigest()
        trace_generated_at_utc = str(
            trace.get("generated_at_utc")
            or trace.get("generated_at")
            or trace.get("generatedAt")
            or ""
        ).strip()
        audit = json.loads(args.user_journey_audit.read_text(encoding="utf-8"))
        evidence = audit["evidence"]
        evidence["trace_sha256"] = trace_sha256
        evidence["trace_sha256_after_audit"] = trace_sha256
        evidence["trace_generated_at_utc"] = trace_generated_at_utc
        write_json(args.user_journey_audit, audit)

    def user_journey_failure_actuals(
        self,
        args: SimpleNamespace,
        checkpoint: str,
    ) -> list[str]:
        failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
        return [
            str(item["actual"])
            for item in failures["failures"]
            if item["checkpoint"] == checkpoint
        ]

    def test_run_gate_passes_with_complete_proof(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.PASS_EXIT)
            metadata = json.loads((args.artifacts / "run-metadata.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["status"], "pass")
            self.assertEqual(metadata["proofScope"]["fixtureScope"], "first_slice_default")
            self.assertTrue(metadata["proofScope"]["uiReconstructionExecuted"])
            self.assertTrue(metadata["proofScope"]["perFixtureOutputRoutesExecuted"])
            self.assertTrue(metadata["proofScope"]["perFixturePdfArtifactsProduced"])
            self.assertTrue(metadata["proofScope"]["certifiesSelectedFixturesCanBeRebuiltOnlyUsingUi"])
            self.assertTrue(
                any("PDF-route artifact materialization" in claim for claim in metadata["proofClaims"])
            )
            self.assertIn(
                "A passing result certifies only the explicitly selected fixture set, not the entire Chummer5a fixture corpus.",
                metadata["proofLimitations"],
            )
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertEqual(failures["failures"], [])

    def test_run_gate_rejects_when_visual_gate_is_red(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir, visual_status="fail")

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "desktop_visual_familiarity_exit_gate" for item in failures["failures"]))

    def test_run_gate_rejects_missing_workflow_receipt(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir, omit_workflow=True)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms" for item in failures["failures"]))

    def test_run_gate_falls_back_to_screenshot_evidence_sibling_directory(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir, stale_screenshot_root=True)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.PASS_EXIT)
            metadata = json.loads((args.artifacts / "run-metadata.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["status"], "pass")

    def test_run_gate_passes_with_recursive_settings_and_element_proof_without_reconstruction_receipts(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            args.reconstruction_receipts_dir = None

            result = tester.run_gate(args)

            self.assertEqual(result, tester.PASS_EXIT)
            metadata = json.loads((args.artifacts / "run-metadata.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["status"], "pass")
            self.assertFalse(metadata["proofScope"]["uiReconstructionExecuted"])
            self.assertTrue(metadata["proofScope"]["recursiveSettingsAndElementsCertified"])
            self.assertEqual(
                metadata["summary"],
                "Parity gate passed for the default first-slice Chummer5a fixture set with exhaustive recursive settings/element parity proof.",
            )

    def test_run_gate_labels_explicit_all_fixture_scope(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            fixture_root = Path(args.chummer5a_repo) / "Chummer.Tests" / "TestFiles"
            args.fixture = [str(path) for path in sorted(fixture_root.glob("*.chum5"))]

            result = tester.run_gate(args)

            self.assertEqual(result, tester.PASS_EXIT)
            metadata = json.loads((args.artifacts / "run-metadata.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["proofScope"]["fixtureScope"], "all_available_fixtures_explicit")
            self.assertEqual(metadata["proofScope"]["selectedFixtureCount"], 5)
            self.assertEqual(metadata["proofScope"]["availableFixtureCount"], 5)
            self.assertTrue(metadata["proofScope"]["certifiesEveryFixtureCanBeRebuiltOnlyUsingUi"])
            self.assertEqual(
                metadata["summary"],
                "Parity gate passed for the explicitly selected all-fixtures Chummer5a set with per-fixture UI reconstruction proof.",
            )

    def test_run_gate_labels_explicit_all_fixture_scope_with_recursive_settings_and_element_proof(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            fixture_root = Path(args.chummer5a_repo) / "Chummer.Tests" / "TestFiles"
            args.fixture = [str(path) for path in sorted(fixture_root.glob("*.chum5"))]
            args.reconstruction_receipts_dir = None

            result = tester.run_gate(args)

            self.assertEqual(result, tester.PASS_EXIT)
            metadata = json.loads((args.artifacts / "run-metadata.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["proofScope"]["fixtureScope"], "all_available_fixtures_explicit")
            self.assertFalse(metadata["proofScope"]["certifiesEveryFixtureCanBeRebuiltOnlyUsingUi"])
            self.assertTrue(metadata["proofScope"]["recursiveSettingsAndElementsCertified"])
            self.assertEqual(
                metadata["summary"],
                "Parity gate passed for the explicitly selected all-fixtures Chummer5a set with exhaustive recursive settings/element parity proof.",
            )

    def test_run_gate_rejects_explicit_fixtures_without_reconstruction_receipts_or_recursive_proof(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            fixture_root = Path(args.chummer5a_repo) / "Chummer.Tests" / "TestFiles"
            args.fixture = [str(path) for path in sorted(fixture_root.glob("*.chum5"))]
            args.reconstruction_receipts_dir = None
            args.desktop_workflow_parity_receipt = Path(tmpdir) / "MISSING_CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
            args.generated_dialog_element_parity_receipt = Path(tmpdir) / "MISSING_GENERATED_DIALOG_ELEMENT_PARITY.generated.json"
            args.ui_element_parity_audit_receipt = Path(tmpdir) / "MISSING_CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "fixture_ui_reconstruction_receipts" for item in failures["failures"]))

    def test_run_gate_rejects_default_scope_without_reconstruction_receipts_or_recursive_proof(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            args.reconstruction_receipts_dir = None
            args.desktop_workflow_parity_receipt = Path(tmpdir) / "MISSING_CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
            args.generated_dialog_element_parity_receipt = Path(tmpdir) / "MISSING_GENERATED_DIALOG_ELEMENT_PARITY.generated.json"
            args.ui_element_parity_audit_receipt = Path(tmpdir) / "MISSING_CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "fixture_ui_reconstruction_receipts" for item in failures["failures"]))

    def test_run_gate_rejects_missing_user_journey_trace_timestamp_even_with_bound_pass_audit(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace.pop("generated_at_utc", None)
            trace.pop("generated_at", None)
            trace.pop("generatedAt", None)
            write_json(args.user_journey_trace, trace)
            self.rebind_user_journey_audit_to_current_trace(args)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "offset-aware generated_at_utc" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )
            self.assertTrue(
                any(
                    "offset-aware generated_at_utc" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_stale_user_journey_trace_using_audit_policy(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace["generated_at_utc"] = (
                (datetime.now(timezone.utc) - timedelta(hours=25))
                .replace(microsecond=0)
                .isoformat()
                .replace("+00:00", "Z")
            )
            write_json(args.user_journey_trace, trace)
            self.rebind_user_journey_audit_to_current_trace(args)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "trace is stale" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )
            self.assertTrue(
                any(
                    "trace is stale" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_enforces_stricter_trace_age_recorded_by_owning_audit(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace["generated_at_utc"] = (
                (datetime.now(timezone.utc) - timedelta(hours=2))
                .replace(microsecond=0)
                .isoformat()
                .replace("+00:00", "Z")
            )
            write_json(args.user_journey_trace, trace)
            audit = json.loads(args.user_journey_audit.read_text(encoding="utf-8"))
            audit["evidence"]["trace_max_age_hours"] = 1
            write_json(args.user_journey_audit, audit)
            self.rebind_user_journey_audit_to_current_trace(args)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertEqual(
                self.user_journey_failure_actuals(args, "user_journey_tester_trace"),
                [],
            )
            self.assertTrue(
                any(
                    "older than 1 hours" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_naive_user_journey_trace_timestamp(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace["generated_at_utc"] = datetime.now().replace(microsecond=0).isoformat()
            write_json(args.user_journey_trace, trace)
            self.rebind_user_journey_audit_to_current_trace(args)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "offset-aware generated_at_utc" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )

    def test_run_gate_rejects_future_user_journey_trace_using_audit_policy(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace["generated_at_utc"] = (
                (datetime.now(timezone.utc) + timedelta(minutes=6))
                .replace(microsecond=0)
                .isoformat()
                .replace("+00:00", "Z")
            )
            write_json(args.user_journey_trace, trace)
            self.rebind_user_journey_audit_to_current_trace(args)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "generated_at_utc is in the future" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )
            self.assertTrue(
                any(
                    "generated_at_utc is in the future" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_user_journey_trace_changed_after_audit(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            trace["post_audit_change"] = "bytes changed after immutable audit"
            write_json(args.user_journey_trace, trace)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            audit_actuals = self.user_journey_failure_actuals(args, "user_journey_tester_audit")
            self.assertTrue(any("trace_sha256 does not match current trace bytes" in actual for actual in audit_actuals))
            self.assertTrue(
                any("trace_sha256_after_audit does not match current trace bytes" in actual for actual in audit_actuals)
            )

    def test_run_gate_rejects_pass_shaped_audit_that_denies_immutable_trace_proof(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            audit = json.loads(args.user_journey_audit.read_text(encoding="utf-8"))
            audit["status"] = "pass"
            audit["reasons"] = []
            audit["evidence"]["trace_bytes_unchanged_during_audit"] = False
            audit["trace_mutation_performed"] = True
            write_json(args.user_journey_audit, audit)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            audit_actuals = self.user_journey_failure_actuals(args, "user_journey_tester_audit")
            self.assertTrue(any("trace bytes were unchanged during audit" in actual for actual in audit_actuals))
            self.assertTrue(any("trace_mutation_performed=false" in actual for actual in audit_actuals))

    def test_run_gate_rejects_malformed_user_journey_trace_without_crashing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            args.user_journey_trace.write_text("{not-json\n", encoding="utf-8")

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "Unreadable user journey trace" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )
            self.assertTrue(
                any(
                    "Unreadable user journey trace" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_symlinked_user_journey_trace_as_unsafe(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace_target = args.user_journey_trace.with_name("user-journey-trace-target.json")
            trace_target.write_bytes(args.user_journey_trace.read_bytes())
            args.user_journey_trace.unlink()
            args.user_journey_trace.symlink_to(trace_target)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "regular non-symlink file" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_trace")
                )
            )
            self.assertTrue(
                any(
                    "regular non-symlink file" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_symlinked_user_journey_audit_as_unsafe(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            audit_target = args.user_journey_audit.with_name("user-journey-audit-target.json")
            audit_target.write_bytes(args.user_journey_audit.read_bytes())
            args.user_journey_audit.unlink()
            args.user_journey_audit.symlink_to(audit_target)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            self.assertTrue(
                any(
                    "regular non-symlink file" in actual
                    for actual in self.user_journey_failure_actuals(args, "user_journey_tester_audit")
                )
            )

    def test_run_gate_rejects_missing_user_journey_assertion(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            trace = json.loads(args.user_journey_trace.read_text(encoding="utf-8"))
            for row in trace["workflows"]:
                if row["id"] == "file_new_character_visible_workspace":
                    row["assertions"].pop("starter_attributes_match_seeded_workspace", None)
            write_json(args.user_journey_trace, trace)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "user_journey_tester_trace" for item in failures["failures"]))

    def test_run_gate_rejects_failed_user_journey_audit(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            args = self.build_args(tmpdir)
            audit = json.loads(args.user_journey_audit.read_text(encoding="utf-8"))
            audit["status"] = "fail"
            audit["reasons"] = ["missing screenshots"]
            write_json(args.user_journey_audit, audit)

            result = tester.run_gate(args)

            self.assertEqual(result, tester.FAIL_EXIT)
            failures = json.loads((args.artifacts / "failures.json").read_text(encoding="utf-8"))
            self.assertTrue(any(item["checkpoint"] == "user_journey_tester_audit" for item in failures["failures"]))


if __name__ == "__main__":
    unittest.main()
