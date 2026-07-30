#!/usr/bin/env python3
from __future__ import annotations

import ast
import binascii
import hashlib
import json
import os
import re
import struct
import subprocess
import tempfile
import unittest
import zlib
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
GATE_PATH = (
    REPO_ROOT
    / "scripts"
    / "ai"
    / "milestones"
    / "materialize-desktop-visual-familiarity-exit-gate.sh"
)
PRODUCER_PATH = (
    REPO_ROOT
    / "Chummer.Tests"
    / "Presentation"
    / "AvaloniaFlagshipUiGateTests.cs"
)
LOCAL_SCREENSHOT_COMPARISON_VERIFIER_PATH = (
    REPO_ROOT / "scripts" / "verify_pixefy_chummer5a_screenshot_comparison.py"
)
REQUIRED_SCREENSHOTS = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "07-loaded-runner-tabs-light.png",
    "08-cyberware-dialog-light.png",
    "09-vehicles-section-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "15-creation-section-light.png",
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
    "19-workflow-file-menu-loaded-light.png",
    "20-workflow-skills-section-light.png",
    "21-workflow-skill-add-dialog-light.png",
    "22-workflow-qualities-section-light.png",
    "23-workflow-quality-add-dialog-light.png",
    "24-workflow-gear-section-light.png",
    "25-workflow-gear-add-dialog-light.png",
    "26-workflow-weapons-section-light.png",
    "27-workflow-weapon-add-dialog-light.png",
    "28-workflow-armor-section-light.png",
    "29-workflow-armor-add-dialog-light.png",
    "30-workflow-cyberware-section-light.png",
    "31-workflow-powers-section-light.png",
    "32-workflow-adept-power-dialog-light.png",
    "33-workflow-complex-form-dialog-light.png",
    "34-workflow-validate-section-light.png",
    "35-workflow-rules-section-light.png",
    "36-workflow-new-character-dialog-light.png",
    "37-workflow-calendar-section-light.png",
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
    "41-horizons-hub-light.png",
    "42-horizon-karma-forge-light.png",
    "43-horizon-alice-light.png",
    "44-horizon-black-ledger-light.png",
    "45-horizon-run-control-light.png",
    "46-horizon-runsite-light.png",
    "47-horizon-jackpoint-light.png",
    "48-horizon-table-pulse-light.png",
    "49-horizon-community-hub-light.png",
    "50-horizon-nexus-pan-light.png",
    "51-horizon-quicksilver-light.png",
    "52-horizon-runner-passport-light.png",
    "53-horizon-runbook-press-light.png",
    "54-horizon-creator-os-light.png",
    "55-horizon-local-co-processor-light.png",
    "56-horizon-anarchy-light.png",
    "57-horizon-ghostwire-light.png",
    "58-horizon-ready-for-tonight-light.png",
    "60-horizon-knowledge-fabric-light.png",
]
CANONICAL_WORKFLOW_COVERAGE = {
    "create-open-import-save-save-as-print-export": [
        "19-workflow-file-menu-loaded-light.png",
        "36-workflow-new-character-dialog-light.png",
        "18-import-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png",
    ],
    "metatype-priorities-karma-entry": [
        "15-creation-section-light.png",
        "11-diary-dialog-light.png",
        "36-workflow-new-character-dialog-light.png",
    ],
    "attributes-skills-skill-groups-specializations-knowledge-languages": [
        "15-creation-section-light.png",
        "20-workflow-skills-section-light.png",
        "21-workflow-skill-add-dialog-light.png",
    ],
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources": [
        "10-contacts-section-light.png",
        "22-workflow-qualities-section-light.png",
        "23-workflow-quality-add-dialog-light.png",
        "37-workflow-calendar-section-light.png",
    ],
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers": [
        "09-vehicles-section-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
        "26-workflow-weapons-section-light.png",
        "27-workflow-weapon-add-dialog-light.png",
        "28-workflow-armor-section-light.png",
        "29-workflow-armor-add-dialog-light.png",
    ],
    "cyberware-bioware-modular-hierarchies-nested-plugins": [
        "08-cyberware-dialog-light.png",
        "30-workflow-cyberware-section-light.png",
    ],
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms": [
        "12-magic-dialog-light.png",
        "13-matrix-dialog-light.png",
        "14-advancement-dialog-light.png",
        "31-workflow-powers-section-light.png",
        "32-workflow-adept-power-dialog-light.png",
        "33-workflow-complex-form-dialog-light.png",
    ],
    "improvements-explain-result-parity": [
        "14-advancement-dialog-light.png",
        "16-master-index-dialog-light.png",
        "34-workflow-validate-section-light.png",
        "35-workflow-rules-section-light.png",
    ],
    "recovery-reload-migration-roundtrips": [
        "04-loaded-runner-light.png",
        "18-import-dialog-light.png",
        "19-workflow-file-menu-loaded-light.png",
    ],
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare": [
        "05-dense-section-light.png",
        "06-dense-section-dark.png",
        "07-loaded-runner-tabs-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
    ],
    "native-horizons-surface-catalog": [
        "41-horizons-hub-light.png",
        "42-horizon-karma-forge-light.png",
        "43-horizon-alice-light.png",
        "44-horizon-black-ledger-light.png",
        "45-horizon-run-control-light.png",
        "46-horizon-runsite-light.png",
        "47-horizon-jackpoint-light.png",
        "48-horizon-table-pulse-light.png",
        "49-horizon-community-hub-light.png",
        "50-horizon-nexus-pan-light.png",
        "51-horizon-quicksilver-light.png",
        "52-horizon-runner-passport-light.png",
        "53-horizon-runbook-press-light.png",
        "54-horizon-creator-os-light.png",
        "55-horizon-local-co-processor-light.png",
        "56-horizon-anarchy-light.png",
        "57-horizon-ghostwire-light.png",
        "58-horizon-ready-for-tonight-light.png",
        "60-horizon-knowledge-fabric-light.png",
    ],
}


def build_test_png(width: int = 1280, height: int = 800) -> bytes:
    def chunk(chunk_type: bytes, payload: bytes) -> bytes:
        checksum = binascii.crc32(chunk_type)
        checksum = binascii.crc32(payload, checksum) & 0xFFFFFFFF
        return (
            struct.pack(">I", len(payload))
            + chunk_type
            + payload
            + struct.pack(">I", checksum)
        )

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    scanline = b"\0" + (b"\x20\x40\x60" * width)
    pixels = scanline * height
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(pixels, level=9))
        + chunk(b"IEND", b"")
    )


class DesktopVisualFamiliarityExitGateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = GATE_PATH.read_text(encoding="utf-8")
        cls.test_png = build_test_png()

    @staticmethod
    def _write_json(path: Path, payload: dict[str, Any]) -> None:
        path.write_text(
            json.dumps(payload, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )

    def _create_fixture(
        self,
        temp_root: Path,
        *,
        control_generated_at: str | None = None,
        authority: dict[str, Any] | None = None,
        workflow_coverage: list[dict[str, Any]] | None = None,
    ) -> dict[str, Any]:
        screenshot_dir = temp_root / "screenshots"
        screenshot_dir.mkdir()
        for screenshot_name in REQUIRED_SCREENSHOTS:
            (screenshot_dir / screenshot_name).write_bytes(self.test_png)

        now = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        screenshot_sha256 = hashlib.sha256(self.test_png).hexdigest()
        entries = [
            {
                "screenshot": screenshot_name,
                "sha256": screenshot_sha256,
                "sizeBytes": len(self.test_png),
                "theme": "Light",
            }
            for screenshot_name in REQUIRED_SCREENSHOTS
        ]
        default_workflow_coverage = [
            {
                "workflowFamilyId": family_id,
                "screenshotFiles": list(screenshot_files),
                "screenshotCount": len(screenshot_files),
            }
            for family_id, screenshot_files in CANONICAL_WORKFLOW_COVERAGE.items()
        ]
        pack_inventory = b"".join(
            screenshot_name.encode("utf-8")
            + b"\0"
            + screenshot_sha256.encode("ascii")
            + b"\0"
            + str(len(self.test_png)).encode("ascii")
            + b"\n"
            for screenshot_name in sorted(REQUIRED_SCREENSHOTS)
        )
        control = {
            "schemaVersion": 1,
            "contract_name": "chummer6-ui.screenshot_control_evidence",
            "generatedAt": control_generated_at or now,
            "screenshotCount": len(entries),
            "screenshotPackSha256": hashlib.sha256(pack_inventory).hexdigest(),
            "screenshotPackDigestAlgorithm": "sha256-canonical-inventory-v1",
            "authority": authority
            if authority is not None
            else {
                "visualBaseline": "Chummer5a",
                "designAuthorityPlatform": "windows",
                "actualCaptureOperatingSystem": "isolated-test-os",
                "actualCaptureArchitecture": "isolated-test-arch",
                "captureHead": "avalonia",
                "captureMode": "avalonia_headless_test_harness",
                "releaseCandidateBound": False,
            },
            "workflowCoverage": workflow_coverage
            if workflow_coverage is not None
            else default_workflow_coverage,
            "entries": entries,
        }
        control_path = screenshot_dir / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
        self._write_json(control_path, control)
        control_bytes = control_path.read_bytes()

        visual_review_evidence = {
            "screenshotControlSha256": hashlib.sha256(control_bytes).hexdigest(),
            "screenshotControlSizeBytes": len(control_bytes),
            "screenshotControlGeneratedAt": control["generatedAt"],
            "screenshotControlSchemaVersion": 1,
            "screenshotCount": len(entries),
            "screenshotPackSha256": hashlib.sha256(pack_inventory).hexdigest(),
            "screenshotPackDigestAlgorithm": "sha256-canonical-inventory-v1",
            "screenshotControlEvidencePath": str(control_path),
        }
        release_channel = {
            "contract_name": "Chummer.Hub.Registry.Contracts",
            "status": "published",
            "generatedAt": now,
            "channelId": "isolated",
            "channel": "isolated",
            "releaseVersion": "isolated-contract-test",
            "version": "isolated-contract-test",
        }
        release_channel_path = temp_root / "release-channel.json"
        self._write_json(release_channel_path, release_channel)
        release_channel_bytes = release_channel_path.read_bytes()
        flagship = {
            "contract_name": "chummer6-ui.flagship_ui_release_gate",
            "status": "pass",
            "generatedAt": now,
            "channelId": "isolated",
            "channel": "isolated",
            "releaseVersion": "isolated-contract-test",
            "version": "isolated-contract-test",
            "releaseChannelEvidence": {
                "path": str(release_channel_path.resolve(strict=True)),
                "contract_name": "Chummer.Hub.Registry.Contracts",
                "status": "published",
                "channelId": "isolated",
                "releaseVersion": "isolated-contract-test",
                "sha256": hashlib.sha256(release_channel_bytes).hexdigest(),
                "sizeBytes": len(release_channel_bytes),
                "generatedAt": now,
            },
            "desktopHeads": ["avalonia", "blazor-desktop"],
            "interactionProof": {
                key: "pass"
                for key in [
                    "themeReadabilityContrast",
                    "menuSurface",
                    "settingsInlineDialog",
                    "demoRunnerDispatch",
                    "keyboardShortcutParity",
                    "crossHeadWorkflowParity",
                    "installUpdateRecoveryLifecycle",
                    "runtimeBackedSr4CodexOrientationModel",
                    "runtimeBackedSr5CodexOrientationModel",
                    "runtimeBackedSr6CodexOrientationModel",
                    "runtimeBackedShellMenu",
                    "runtimeBackedMenuBarLabels",
                    "runtimeBackedClickablePrimaryMenus",
                    "runtimeBackedToolstripActions",
                    "runtimeBackedCodexTree",
                    "defaultSingleRunnerKeepsWorkspaceChromeCollapsed",
                    "runtimeBackedClassicChromeCopy",
                    "runtimeBackedTabPanelOnlyHeader",
                    "runtimeBackedChromeEnabledAfterRunnerLoad",
                    "fullInteractiveControlInventory",
                    "mainWindowInteractionInventory",
                    "runtimeBackedDemoRunnerImport",
                    "runtimeBackedLegacyWorkbench",
                    "runtimeBackedFileMenuRoutes",
                    "runtimeBackedMasterIndex",
                    "runtimeBackedCharacterRoster",
                    "legacyMainframeVisualSimilarity",
                    "legacyDenseBuilderRhythm",
                    "legacyCreationWorkflowRhythm",
                    "legacyAdvancementWorkflowRhythm",
                    "legacyBrowseDetailConfirmRhythm",
                    "legacyGearWorkflowRhythm",
                    "legacyVehiclesBuilderRhythm",
                    "legacyCyberwareDialogRhythm",
                    "legacyContactsDiaryRhythm",
                    "legacyContactsWorkflowRhythm",
                    "legacyDiaryWorkflowRhythm",
                    "legacyMagicWorkflowRhythm",
                    "legacyMatrixWorkflowRhythm",
                ]
            },
            "headProofs": {
                "avalonia": {
                    "status": "pass",
                    "visualReview": "pass",
                    "themeReadabilityContrast": "pass",
                    "bundledDemoRunner": "pass",
                    "requiredRuntimeBackedTests": ["fixture"],
                    "sourceTestFile": str(PRODUCER_PATH),
                    "testSuites": ["AvaloniaFlagshipUiGateTests"],
                },
                "blazor-desktop": {
                    "status": "pass",
                    "shellChrome": "pass",
                    "commandSurface": "pass",
                    "dialogSurface": "pass",
                    "journeyPanels": "pass",
                    "requiredShellTests": ["fixture"],
                    "sourceTestFile": str(
                        REPO_ROOT
                        / "Chummer.Tests"
                        / "Presentation"
                        / "DesktopShellRulesetCatalogTests.cs"
                    ),
                    "testSuites": ["BlazorShellComponentTests"],
                },
            },
            "visualReviewEvidence": visual_review_evidence,
        }
        flagship_path = temp_root / "flagship.json"
        self._write_json(flagship_path, flagship)

        prerequisite_paths: list[Path] = []
        prerequisite_specs = [
            ("layout.json", "contract_name", "chummer6-ui.chummer5a_layout_hard_gate"),
            (
                "chrome.json",
                "contract_name",
                "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
            ),
            (
                "muscle.json",
                "contractName",
                "chummer6-ui.chummer5a_muscle_memory_parity_gate",
            ),
        ]
        for name, contract_key, contract_value in prerequisite_specs:
            path = temp_root / name
            self._write_json(
                path,
                {
                    "status": "pass",
                    "generatedAt": now,
                    contract_key: contract_value,
                },
            )
            prerequisite_paths.append(path)

        output_path = temp_root / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
        environment = os.environ.copy()
        environment.update(
            {
                "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH": str(output_path),
                "CHUMMER_DESKTOP_VISUAL_FLAGSHIP_GATE_PATH": str(flagship_path),
                "CHUMMER_DESKTOP_VISUAL_SCREENSHOT_DIR": str(screenshot_dir),
                "CHUMMER_DESKTOP_VISUAL_SCREENSHOT_CONTROL_EVIDENCE_PATH": str(control_path),
                "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH": str(release_channel_path),
                "CHUMMER5A_LAYOUT_HARD_GATE_PATH": str(prerequisite_paths[0]),
                "CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE_PATH": str(prerequisite_paths[1]),
                "CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE_PATH": str(prerequisite_paths[2]),
                "CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE": "0",
                "CHUMMER_DESKTOP_VISUAL_REFRESH_PREREQUISITE_RECEIPTS": "0",
                "CHUMMER_DESKTOP_VISUAL_FORCE_PREREQUISITE_RECEIPT_REFRESH": "0",
                "CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS": "0",
                "CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT": "1",
                "CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH": "/dev/null",
            }
        )
        return {
            "screenshot_dir": screenshot_dir,
            "control": control,
            "control_path": control_path,
            "flagship": flagship,
            "flagship_path": flagship_path,
            "release_channel": release_channel,
            "release_channel_path": release_channel_path,
            "prerequisite_paths": prerequisite_paths,
            "output_path": output_path,
            "environment": environment,
        }

    def _run_fixture(
        self,
        fixture: dict[str, Any],
        *,
        expected_returncode: int = 43,
    ) -> dict[str, Any]:
        result = subprocess.run(
            ["/bin/bash", str(GATE_PATH)],
            cwd=REPO_ROOT,
            env=fixture["environment"],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(expected_returncode, result.returncode, result.stderr)
        return json.loads(fixture["output_path"].read_text(encoding="utf-8"))

    def _replace_screenshot_and_rebind_control(
        self,
        fixture: dict[str, Any],
        screenshot_name: str,
        screenshot_bytes: bytes,
    ) -> None:
        (fixture["screenshot_dir"] / screenshot_name).write_bytes(screenshot_bytes)
        entry = next(
            item
            for item in fixture["control"]["entries"]
            if item["screenshot"] == screenshot_name
        )
        entry["sha256"] = hashlib.sha256(screenshot_bytes).hexdigest()
        entry["sizeBytes"] = len(screenshot_bytes)
        pack_inventory = b"".join(
            item["screenshot"].encode("utf-8")
            + b"\0"
            + item["sha256"].encode("ascii")
            + b"\0"
            + str(item["sizeBytes"]).encode("ascii")
            + b"\n"
            for item in sorted(
                fixture["control"]["entries"],
                key=lambda value: value["screenshot"],
            )
        )
        pack_sha256 = hashlib.sha256(pack_inventory).hexdigest()
        fixture["control"]["screenshotPackSha256"] = pack_sha256
        self._write_json(fixture["control_path"], fixture["control"])
        control_bytes = fixture["control_path"].read_bytes()
        visual_review = fixture["flagship"]["visualReviewEvidence"]
        visual_review["screenshotControlSha256"] = hashlib.sha256(
            control_bytes
        ).hexdigest()
        visual_review["screenshotControlSizeBytes"] = len(control_bytes)
        visual_review["screenshotPackSha256"] = pack_sha256
        self._write_json(fixture["flagship_path"], fixture["flagship"])

    def test_complete_valid_fixture_passes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-valid-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            receipt = self._run_fixture(fixture, expected_returncode=0)
        self.assertEqual("pass", receipt["status"])
        self.assertEqual([], receipt["reasons"])
        self.assertEqual(receipt["channelId"], receipt["channel"])
        self.assertEqual(receipt["releaseVersion"], receipt["version"])

    def test_b14_refresh_receives_the_exact_selected_release_channel(self) -> None:
        self.assertIn(
            'CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_PATH="$release_channel_path"',
            self.source,
        )

    def test_prerequisite_contract_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-prereq-contract-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            self._write_json(
                fixture["prerequisite_paths"][0],
                {
                    "status": "pass",
                    "generatedAt": datetime.now(timezone.utc).isoformat(),
                    "contract_name": "wrong.contract",
                },
            )
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason) == "chummer5a_layout_hard_gate receipt contract is not recognized."
                for reason in receipt["reasons"]
            )
        )

    def test_shell_syntax_is_valid(self) -> None:
        result = subprocess.run(
            ["bash", "-n", str(GATE_PATH)],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)

    def test_fail_closed_validation_can_run_without_canonical_writes(self) -> None:
        def snapshot(path: Path) -> tuple[int, str] | None:
            if not path.is_file():
                return None
            payload = path.read_bytes()
            return len(payload), hashlib.sha256(payload).hexdigest()

        canonical_paths = [
            REPO_ROOT
            / ".codex-studio"
            / "published"
            / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
            REPO_ROOT
            / ".codex-studio"
            / "published"
            / "ui-flagship-release-gate-screenshots"
            / "SCREENSHOT_CONTROL_EVIDENCE.generated.json",
            Path("/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json"),
        ]
        before = {path: snapshot(path) for path in canonical_paths}

        with tempfile.TemporaryDirectory(prefix="desktop-visual-gate-contract-") as temp_text:
            temp_root = Path(temp_text)
            screenshot_dir = temp_root / "screenshots"
            screenshot_dir.mkdir()
            output_path = temp_root / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
            now = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

            prerequisite_paths = []
            for name in ("layout.json", "chrome.json", "muscle.json"):
                path = temp_root / name
                path.write_text(
                    json.dumps({"status": "pass", "generatedAt": now}),
                    encoding="utf-8",
                )
                prerequisite_paths.append(path)

            flagship_path = temp_root / "flagship.json"
            flagship_path.write_text(
                json.dumps({"status": "fail", "generatedAt": now}),
                encoding="utf-8",
            )
            release_channel_path = temp_root / "release-channel.json"
            release_channel_path.write_text(
                json.dumps(
                    {
                        "status": "published",
                        "generatedAt": now,
                        "channelId": "isolated",
                        "version": "isolated-contract-test",
                    }
                ),
                encoding="utf-8",
            )

            environment = os.environ.copy()
            environment.update(
                {
                    "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH": str(output_path),
                    "CHUMMER_DESKTOP_VISUAL_FLAGSHIP_GATE_PATH": str(flagship_path),
                    "CHUMMER_DESKTOP_VISUAL_SCREENSHOT_DIR": str(screenshot_dir),
                    "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH": str(release_channel_path),
                    "CHUMMER5A_LAYOUT_HARD_GATE_PATH": str(prerequisite_paths[0]),
                    "CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE_PATH": str(prerequisite_paths[1]),
                    "CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE_PATH": str(prerequisite_paths[2]),
                    "CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE": "0",
                    "CHUMMER_DESKTOP_VISUAL_REFRESH_PREREQUISITE_RECEIPTS": "0",
                    "CHUMMER_DESKTOP_VISUAL_FORCE_PREREQUISITE_RECEIPT_REFRESH": "0",
                    "CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS": "0",
                }
            )
            result = subprocess.run(
                ["bash", str(GATE_PATH)],
                cwd=REPO_ROOT,
                env=environment,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(43, result.returncode, result.stderr)
            receipt = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("fail", receipt.get("status"))
            self.assertTrue(receipt.get("reasons"))
            self.assertEqual([], list(screenshot_dir.iterdir()))

        after = {path: snapshot(path) for path in canonical_paths}
        self.assertEqual(before, after)

    def test_valid_isolated_control_fixture_has_no_control_binding_failures(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-valid-control-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            receipt = self._run_fixture(fixture, expected_returncode=0)
        unexpected_prefixes = (
            "Screenshot control evidence",
            "Screenshot pack",
            "Screenshot directory",
            "Flagship UI release gate visualReviewEvidence",
            "Flagship UI release gate is missing visualReviewEvidence",
            "Screenshot control/PNG snapshot",
        )
        unexpected_reasons = [
            reason
            for reason in receipt["reasons"]
            if str(reason).startswith(unexpected_prefixes)
        ]
        self.assertEqual([], unexpected_reasons)

    def test_non_external_flagship_failure_is_top_level_blocker(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-flagship-status-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["flagship"]["status"] = "fail"
            fixture["flagship"]["blockingFindings"] = ["unexpected local blocker"]
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Flagship UI release gate status is not passing and its blockers are not"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_under_specified_readiness_failure_is_not_external_desktop_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-readiness-status-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["flagship"]["status"] = "fail"
            fixture["flagship"]["blockingFindings"] = [
                "Top-level release gate cannot pass while flagship readiness is not passed."
            ]
            fixture["flagship"]["desktopExecutableProof"] = {
                "localBlockingFindings": []
            }
            fixture["flagship"].pop("flagshipReadinessProof", None)
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Flagship UI release gate status is not passing and its blockers are not"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_bound_windows_executable_failure_is_external_desktop_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-windows-status-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["flagship"]["status"] = "fail"
            fixture["flagship"]["blockingFindings"] = [
                "Top-level release gate cannot pass while desktop executable exit gate is not passed."
            ]
            fixture["flagship"]["desktopExecutableProof"] = {
                "localBlockingFindings": [
                    "Windows desktop exit gate requires a Windows-capable host; current host cannot run promoted Windows installer smoke."
                ]
            }
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture, expected_returncode=0)
        self.assertFalse(
            any(
                str(reason).startswith(
                    "Flagship UI release gate status is not passing and its blockers are not"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_flagship_channel_and_version_must_match_selected_release(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-release-identity-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["flagship"]["channelId"] = "wrong-channel"
            fixture["flagship"]["releaseVersion"] = "wrong-version"
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Flagship UI release gate channelId does not match"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_release_channel_requires_both_identity_aliases(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-release-aliases-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["release_channel"].pop("channel")
            fixture["release_channel"]["version"] = "conflicting-version"
            self._write_json(
                fixture["release_channel_path"], fixture["release_channel"]
            )
            receipt = self._run_fixture(fixture)
        reasons = "\n".join(str(reason) for reason in receipt["reasons"])
        self.assertIn("missing required channel alias", reasons)
        self.assertIn("conflicting releaseVersion/version aliases", reasons)

    def test_flagship_requires_exact_contract_and_both_identity_aliases(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-flagship-aliases-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["flagship"]["contract_name"] = "wrong.contract"
            fixture["flagship"].pop("channel")
            fixture["flagship"]["version"] = "conflicting-version"
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        reasons = "\n".join(str(reason) for reason in receipt["reasons"])
        self.assertIn(
            "Flagship UI release gate contract_name is not chummer6-ui.flagship_ui_release_gate",
            reasons,
        )
        self.assertIn("Flagship UI release gate receipt is missing required channel alias", reasons)
        self.assertIn("conflicting releaseVersion/version aliases", reasons)

    def test_flagship_release_channel_evidence_binds_exact_file_and_identity(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-release-binding-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            release_evidence = fixture["flagship"]["releaseChannelEvidence"]
            release_evidence.update(
                {
                    "path": str(Path(temp_text) / "different-release.json"),
                    "contract_name": "wrong.contract",
                    "status": "draft",
                    "channelId": "wrong-channel",
                    "releaseVersion": "wrong-version",
                    "sha256": "0" * 64,
                    "sizeBytes": -1,
                    "generatedAt": "2000-01-01T00:00:00Z",
                }
            )
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        mismatch_evidence = receipt["evidence"][
            "flagship_release_channel_evidence_mismatches"
        ]
        self.assertEqual(
            {
                "channelId",
                "contract_name",
                "generatedAt",
                "path",
                "releaseVersion",
                "sha256",
                "sizeBytes",
                "status",
            },
            set(mismatch_evidence),
        )
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Flagship UI release gate releaseChannelEvidence does not bind"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_failed_or_timezone_naive_release_channel_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-release-status-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["release_channel"]["status"] = "fail"
            fixture["release_channel"]["generatedAt"] = "2026-07-18T10:00:00"
            self._write_json(
                fixture["release_channel_path"], fixture["release_channel"]
            )
            receipt = self._run_fixture(fixture)
        reasons = "\n".join(str(reason) for reason in receipt["reasons"])
        self.assertIn("release channel status is not published", reasons)
        self.assertIn("release channel receipt is missing a valid generatedAt", reasons)

    def test_stale_control_fails_even_when_png_mtimes_are_fresh(self) -> None:
        stale_generated_at = "2020-01-01T00:00:00Z"
        with tempfile.TemporaryDirectory(prefix="desktop-visual-stale-control-") as temp_text:
            fixture = self._create_fixture(
                Path(temp_text),
                control_generated_at=stale_generated_at,
            )
            for screenshot_name in REQUIRED_SCREENSHOTS:
                os.utime(fixture["screenshot_dir"] / screenshot_name, None)
            os.utime(fixture["control_path"], None)
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith("Screenshot control evidence is stale")
                for reason in receipt["reasons"]
            )
        )
        self.assertNotIn(
            "Visual familiarity screenshots are stale:",
            "\n".join(str(reason) for reason in receipt["reasons"]),
        )

    def test_control_byte_tamper_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-byte-tamper-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            tampered_path = fixture["screenshot_dir"] / REQUIRED_SCREENSHOTS[0]
            tampered_path.write_bytes(tampered_path.read_bytes() + b"tamper")
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Screenshot pack bytes do not match control evidence"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_non_visual_subset_png_must_still_be_structurally_reviewable(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-invalid-horizon-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            screenshot_name = REQUIRED_SCREENSHOTS[-1]
            invalid_png = b"\x89PNG\r\n\x1a\n" + struct.pack(
                ">I", 0
            ) + b"IEND" + struct.pack(">I", binascii.crc32(b"IEND") & 0xFFFFFFFF)
            (fixture["screenshot_dir"] / screenshot_name).write_bytes(invalid_png)
            entry = next(
                item
                for item in fixture["control"]["entries"]
                if item["screenshot"] == screenshot_name
            )
            entry["sha256"] = hashlib.sha256(invalid_png).hexdigest()
            entry["sizeBytes"] = len(invalid_png)
            pack_inventory = b"".join(
                item["screenshot"].encode("utf-8")
                + b"\0"
                + item["sha256"].encode("ascii")
                + b"\0"
                + str(item["sizeBytes"]).encode("ascii")
                + b"\n"
                for item in sorted(
                    fixture["control"]["entries"],
                    key=lambda value: value["screenshot"],
                )
            )
            pack_sha256 = hashlib.sha256(pack_inventory).hexdigest()
            fixture["control"]["screenshotPackSha256"] = pack_sha256
            self._write_json(fixture["control_path"], fixture["control"])
            control_bytes = fixture["control_path"].read_bytes()
            visual_review = fixture["flagship"]["visualReviewEvidence"]
            visual_review["screenshotControlSha256"] = hashlib.sha256(
                control_bytes
            ).hexdigest()
            visual_review["screenshotControlSizeBytes"] = len(control_bytes)
            visual_review["screenshotPackSha256"] = pack_sha256
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                screenshot_name in str(reason)
                and "unreadable or corrupted" in str(reason)
                for reason in receipt["reasons"]
            )
        )

    def test_png_iend_chunk_must_have_zero_length_payload(self) -> None:
        iend_payload = b"unexpected"
        iend_crc = binascii.crc32(b"IEND")
        iend_crc = binascii.crc32(iend_payload, iend_crc) & 0xFFFFFFFF
        nonempty_iend = (
            self.test_png[:-12]
            + struct.pack(">I", len(iend_payload))
            + b"IEND"
            + iend_payload
            + struct.pack(">I", iend_crc)
        )
        with tempfile.TemporaryDirectory(prefix="desktop-visual-iend-payload-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            screenshot_name = REQUIRED_SCREENSHOTS[-1]
            self._replace_screenshot_and_rebind_control(
                fixture,
                screenshot_name,
                nonempty_iend,
            )
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                screenshot_name in str(reason) and "invalid IEND chunk" in str(reason)
                for reason in receipt["reasons"]
            )
        )

    def test_control_pack_digest_fields_must_match_computed_inventory(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-pack-digest-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            fixture["control"]["screenshotPackSha256"] = "0" * 64
            fixture["control"]["screenshotPackDigestAlgorithm"] = "unsupported"
            self._write_json(fixture["control_path"], fixture["control"])
            control_bytes = fixture["control_path"].read_bytes()
            visual_review = fixture["flagship"]["visualReviewEvidence"]
            visual_review["screenshotControlSha256"] = hashlib.sha256(
                control_bytes
            ).hexdigest()
            visual_review["screenshotControlSizeBytes"] = len(control_bytes)
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        reasons = "\n".join(str(reason) for reason in receipt["reasons"])
        self.assertIn(
            "Screenshot control evidence screenshotPackSha256 does not match",
            reasons,
        )
        self.assertIn(
            "Screenshot control evidence screenshotPackDigestAlgorithm is missing or unsupported",
            reasons,
        )

    def test_extra_top_level_png_symlink_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-extra-symlink-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            (fixture["screenshot_dir"] / "99-extra.png").symlink_to(
                fixture["screenshot_dir"] / REQUIRED_SCREENSHOTS[0]
            )
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Screenshot directory contains symlinked, non-regular, or unreadable"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_malformed_authority_and_workflow_coverage_are_rejected(self) -> None:
        malformed_coverage = [
            {
                "workflowFamilyId": "duplicate-family",
                "screenshotFiles": [
                    REQUIRED_SCREENSHOTS[0],
                    REQUIRED_SCREENSHOTS[0],
                    "not-declared.png",
                ],
                "screenshotCount": 1,
            },
            {
                "workflowFamilyId": "DUPLICATE-FAMILY",
                "screenshotFiles": [REQUIRED_SCREENSHOTS[1]],
                "screenshotCount": 1,
            },
        ]
        with tempfile.TemporaryDirectory(prefix="desktop-visual-authority-coverage-") as temp_text:
            fixture = self._create_fixture(
                Path(temp_text),
                authority={
                    "visualBaseline": "Unknown",
                    "designAuthorityPlatform": "linux",
                    "actualCaptureOperatingSystem": "",
                    "actualCaptureArchitecture": "",
                    "captureHead": "blazor-desktop",
                    "captureMode": "unknown",
                    "releaseCandidateBound": True,
                },
                workflow_coverage=malformed_coverage,
            )
            receipt = self._run_fixture(fixture)
        reasons = "\n".join(str(reason) for reason in receipt["reasons"])
        self.assertIn(
            "Screenshot control evidence authority does not match",
            reasons,
        )
        self.assertIn("duplicate workflowFamilyId", reasons)
        self.assertIn("duplicate screenshot names", reasons)
        self.assertIn("references screenshots missing from entries", reasons)
        self.assertIn("workflowCoverage contains malformed rows", reasons)
        self.assertIn("workflowCoverage is missing canonical workflow families", reasons)

    def test_truncated_canonical_inventory_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-truncated-pack-") as temp_text:
            fixture = self._create_fixture(Path(temp_text))
            omitted_name = REQUIRED_SCREENSHOTS[-1]
            (fixture["screenshot_dir"] / omitted_name).unlink()
            fixture["control"]["entries"] = [
                entry
                for entry in fixture["control"]["entries"]
                if entry["screenshot"] != omitted_name
            ]
            fixture["control"]["screenshotCount"] = len(
                fixture["control"]["entries"]
            )
            for row in fixture["control"]["workflowCoverage"]:
                if omitted_name in row["screenshotFiles"]:
                    row["screenshotFiles"] = [
                        name for name in row["screenshotFiles"] if name != omitted_name
                    ]
                    row["screenshotCount"] = len(row["screenshotFiles"])
            pack_inventory = b"".join(
                entry["screenshot"].encode("utf-8")
                + b"\0"
                + entry["sha256"].encode("ascii")
                + b"\0"
                + str(entry["sizeBytes"]).encode("ascii")
                + b"\n"
                for entry in sorted(
                    fixture["control"]["entries"],
                    key=lambda item: item["screenshot"],
                )
            )
            pack_sha256 = hashlib.sha256(pack_inventory).hexdigest()
            fixture["control"]["screenshotPackSha256"] = pack_sha256
            self._write_json(fixture["control_path"], fixture["control"])
            control_bytes = fixture["control_path"].read_bytes()
            visual_review = fixture["flagship"]["visualReviewEvidence"]
            visual_review["screenshotControlSha256"] = hashlib.sha256(
                control_bytes
            ).hexdigest()
            visual_review["screenshotControlSizeBytes"] = len(control_bytes)
            visual_review["screenshotCount"] = len(fixture["control"]["entries"])
            visual_review["screenshotPackSha256"] = pack_sha256
            self._write_json(fixture["flagship_path"], fixture["flagship"])
            receipt = self._run_fixture(fixture)
        self.assertTrue(
            any(
                str(reason).startswith(
                    "Screenshot control evidence is missing canonical producer inventory entries"
                )
                for reason in receipt["reasons"]
            )
        )

    def test_isolated_output_and_input_paths_are_supported(self) -> None:
        self.assertIn("CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH", self.source)
        self.assertIn("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_DIR", self.source)
        self.assertIn("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_CONTROL_EVIDENCE_PATH", self.source)
        self.assertIn("CHUMMER_DESKTOP_VISUAL_FLAGSHIP_GATE_PATH", self.source)
        self.assertIn("CHUMMER5A_LAYOUT_HARD_GATE_PATH", self.source)
        self.assertIn("CHUMMER_DESKTOP_VISUAL_SKIP_DOWNSTREAM_READINESS", self.source)

    def test_refresh_and_downstream_mutation_are_explicit_opt_ins(self) -> None:
        self.assertIn(
            'CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE:-0',
            self.source,
        )
        self.assertIn(
            'CHUMMER_DESKTOP_VISUAL_REFRESH_PREREQUISITE_RECEIPTS:-0',
            self.source,
        )
        self.assertIn(
            'CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS:-0',
            self.source,
        )
        self.assertIn(
            'if [[ "$skip_release_gate_lock_wait" != "1" \\',
            self.source,
        )
        self.assertIn(
            '&& ( "$refresh_screenshot_pack_when_stale" == "1" \\',
            self.source,
        )
        self.assertIn(
            'if [[ "$refresh_downstream_readiness" == "1" '
            '&& "$skip_downstream_readiness" != "1" ]]; then',
            self.source,
        )
        self.assertIn('b14_flagship_readiness_materializer_path="/dev/null"', self.source)
        self.assertIn(
            'CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH='
            '"$b14_flagship_readiness_materializer_path"',
            self.source,
        )

    def test_skip_prerequisite_refresh_dominates_force_without_invoking_bash(self) -> None:
        with tempfile.TemporaryDirectory(prefix="desktop-visual-skip-force-") as temp_text:
            temp_root = Path(temp_text)
            fixture = self._create_fixture(temp_root)
            fake_bin = temp_root / "fake-bin"
            fake_bin.mkdir()
            invocation_marker = temp_root / "unexpected-bash-invocation"
            bash_wrapper = fake_bin / "bash"
            bash_wrapper.write_text(
                "#!/bin/sh\n"
                ": > \"$CHUMMER_DESKTOP_VISUAL_TEST_BASH_MARKER\"\n"
                "printf 'unexpected bash: %s\\n' \"$*\" >&2\n"
                "exit 97\n",
                encoding="utf-8",
            )
            bash_wrapper.chmod(0o755)
            fixture["environment"].update(
                {
                    "PATH": os.pathsep.join((str(fake_bin), "/usr/bin", "/bin")),
                    "CHUMMER_DESKTOP_VISUAL_TEST_BASH_MARKER": str(invocation_marker),
                    "CHUMMER_DESKTOP_VISUAL_SKIP_PREREQUISITE_RECEIPT_REFRESH": "1",
                    "CHUMMER_DESKTOP_VISUAL_FORCE_PREREQUISITE_RECEIPT_REFRESH": "1",
                    "CHUMMER_DESKTOP_VISUAL_REFRESH_PREREQUISITE_RECEIPTS": "1",
                }
            )
            receipt = self._run_fixture(fixture, expected_returncode=0)
        self.assertEqual("pass", receipt["status"])
        self.assertFalse(invocation_marker.exists())

    def test_skip_prerequisite_refresh_suppresses_force_lock_wait(self) -> None:
        self.assertIn(
            '( "$force_prerequisite_receipt_refresh" == "1" \\\n'
            '      && "$skip_prerequisite_receipt_refresh" != "1" )',
            self.source,
        )

    def test_timestamp_laundering_and_arbitrary_pack_promotion_are_absent(self) -> None:
        self.assertNotIn("os.utime", self.source)
        self.assertNotIn("republish_screenshot_pack_freshness_if_complete", self.source)
        self.assertNotIn("collect_runtime_screenshot_candidate_dirs", self.source)
        self.assertNotIn("promote_fresh_runtime_screenshot_pack", self.source)
        self.assertNotIn("allow_stale_pass_receipt", self.source)

    def test_release_channel_selection_is_deterministic(self) -> None:
        self.assertNotIn(' -nt ', self.source)
        ordered_markers = [
            'if [[ -n "$release_channel_path_override" ]]',
            'elif [[ -n "$canonical_release_channel_path" '
            '&& -f "$canonical_release_channel_path" ]]',
            'elif [[ -f "$verified_release_channel_path" ]]',
            'elif [[ -f "$run_services_release_channel_path" ]]',
            'elif [[ -f "$default_release_channel_path" ]]',
        ]
        positions = [self.source.index(marker) for marker in ordered_markers]
        self.assertEqual(sorted(positions), positions)
        self.assertIn(
            'validate_receipt_freshness(\n'
            '    "release_channel",',
            self.source,
        )

    def test_control_receipt_binds_the_complete_png_inventory(self) -> None:
        self.assertIn('SCREENSHOT_CONTROL_SCHEMA_VERSION = 1', self.source)
        self.assertIn(
            'SCREENSHOT_CONTROL_CONTRACT_NAME = '
            '"chummer6-ui.screenshot_control_evidence"',
            self.source,
        )
        self.assertIn(
            'control_screenshot_count != len(control_entries_by_name)',
            self.source,
        )
        self.assertIn(
            'required_control_entries_missing = sorted(',
            self.source,
        )
        self.assertIn('undeclared_pack_png_names', self.source)
        self.assertIn('declared_missing_png_names', self.source)
        self.assertIn('hashlib.sha256(screenshot_bytes).hexdigest()', self.source)
        self.assertIn('actual_size != expected_size', self.source)
        self.assertIn('actual_sha256 != expected_sha256', self.source)
        self.assertIn(r're.fullmatch(r"[0-9a-f]{64}"', self.source)
        self.assertIn('screenshot_control_receipt_sha256', self.source)
        self.assertIn('screenshot_control_receipt_size_bytes', self.source)
        self.assertIn('screenshot_dir_symlink_components', self.source)
        self.assertIn('screenshot_control_symlink_components', self.source)
        self.assertIn('invalid_top_level_png_entries', self.source)
        self.assertIn('screenshot_snapshot_recheck', self.source)
        self.assertIn('flagship_visual_binding_mismatches', self.source)
        self.assertIn('screenshot_control_pack_sha256', self.source)
        self.assertIn('screenshot_control_pack_digest_algorithm', self.source)
        self.assertIn('unexpected_control_entries = sorted(', self.source)
        self.assertIn('canonical_screenshot_inventory', self.source)

    def test_validator_canonical_inventory_matches_capture_producer(self) -> None:
        producer_source = PRODUCER_PATH.read_text(encoding="utf-8")
        producer_match = re.search(
            r"VeteranCertificationScreenshotFiles\s*=\s*\[(.*?)\];",
            producer_source,
            re.DOTALL,
        )
        self.assertIsNotNone(producer_match)
        producer_inventory = re.findall(
            r'"([^"]+\.png)"',
            producer_match.group(1) if producer_match else "",
        )
        validator_match = re.search(
            r"canonical_screenshot_inventory\s*=\s*\[(.*?)\]\ncanonical_workflow_coverage",
            self.source,
            re.DOTALL,
        )
        self.assertIsNotNone(validator_match)
        validator_inventory = re.findall(
            r'"([^"]+\.png)"',
            validator_match.group(1) if validator_match else "",
        )
        self.assertEqual(REQUIRED_SCREENSHOTS, producer_inventory)
        self.assertEqual(REQUIRED_SCREENSHOTS, validator_inventory)

        producer_coverage_match = re.search(
            r"WorkflowScreenshotCoverage\s*=\s*\[(.*?)\];",
            producer_source,
            re.DOTALL,
        )
        self.assertIsNotNone(producer_coverage_match)
        producer_workflow_coverage = {
            family_id: re.findall(r'"([^"]+\.png)"', screenshot_list)
            for family_id, screenshot_list in re.findall(
                r'new\("([^"]+)",\s*"[^"]*",\s*\[(.*?)\]\)',
                producer_coverage_match.group(1) if producer_coverage_match else "",
                re.DOTALL,
            )
        }
        validator_coverage_match = re.search(
            r"canonical_workflow_coverage\s*=\s*(\{.*?\})\nrequired_screenshots",
            self.source,
            re.DOTALL,
        )
        self.assertIsNotNone(validator_coverage_match)
        validator_workflow_coverage = ast.literal_eval(
            validator_coverage_match.group(1) if validator_coverage_match else "{}"
        )
        self.assertEqual(CANONICAL_WORKFLOW_COVERAGE, producer_workflow_coverage)
        self.assertEqual(CANONICAL_WORKFLOW_COVERAGE, validator_workflow_coverage)

    def test_capture_producer_emits_the_validator_control_contract(self) -> None:
        producer_source = PRODUCER_PATH.read_text(encoding="utf-8")
        self.assertIn(
            'contract_name = "chummer6-ui.screenshot_control_evidence"',
            producer_source,
        )
        self.assertIn("schemaVersion = 1", producer_source)
        self.assertIn("screenshotCount = screenshotControlEntries.Length", producer_source)
        self.assertIn("SHA256.HashData(pair.Value.PngBytes)", producer_source)
        self.assertIn("sizeBytes", producer_source)
        self.assertIn("entries = screenshotControlEntries", producer_source)
        self.assertIn('designAuthorityPlatform = "windows"', producer_source)
        self.assertIn('captureMode = "avalonia_headless_test_harness"', producer_source)
        self.assertIn("releaseCandidateBound = false", producer_source)
        self.assertNotIn("releaseAuthorityPlatform", producer_source)

        local_comparison_source = (
            LOCAL_SCREENSHOT_COMPARISON_VERIFIER_PATH.read_text(encoding="utf-8")
        )
        self.assertIn('authority.get("designAuthorityPlatform")', local_comparison_source)
        self.assertNotIn(
            'authority.get("releaseAuthorityPlatform")',
            local_comparison_source,
        )

    def test_control_timestamp_and_prerequisites_fail_closed_on_staleness(self) -> None:
        self.assertIn(
            'if parsed.tzinfo is None or parsed.utcoffset() is None:',
            self.source,
        )
        self.assertIn(
            'Screenshot control evidence generatedAt must be a valid '
            'offset-aware timestamp.',
            self.source,
        )
        self.assertIn('PREREQUISITE_PROOF_MAX_AGE_SECONDS', self.source)
        self.assertIn('PREREQUISITE_PROOF_MAX_FUTURE_SKEW_SECONDS', self.source)
        self.assertIn('prerequisite_receipt_review_reasons', self.source)

    def test_png_mtimes_are_diagnostic_only_and_receipt_write_is_atomic(self) -> None:
        self.assertIn('screenshot_mtime_age_diagnostics', self.source)
        self.assertNotIn('Visual familiarity screenshots are stale:', self.source)
        self.assertNotIn('screenshots_older_than_flagship_receipt', self.source)
        self.assertIn('control_older_than_flagship_receipt_seconds', self.source)
        self.assertIn('tempfile.mkstemp(', self.source)
        self.assertIn('os.fsync(handle.fileno())', self.source)
        self.assertIn('os.replace(temporary_path, path)', self.source)

    def test_every_required_dialog_uses_only_the_dialog_review_floor(self) -> None:
        expected_dialogs = {
            "03-settings-open-light.png",
            "08-cyberware-dialog-light.png",
            "11-diary-dialog-light.png",
            "12-magic-dialog-light.png",
            "13-matrix-dialog-light.png",
            "14-advancement-dialog-light.png",
            "16-master-index-dialog-light.png",
            "17-character-roster-dialog-light.png",
            "18-import-dialog-light.png",
            "38-translator-dialog-light.png",
            "39-xml-editor-dialog-light.png",
            "40-hero-lab-importer-dialog-light.png",
        }
        dialog_match = re.search(
            r"dialog_screenshot_names\s*=\s*(\{.*?\})\nundersized_screenshots",
            self.source,
            re.DOTALL,
        )
        self.assertIsNotNone(dialog_match)
        observed_dialogs = ast.literal_eval(
            dialog_match.group(1) if dialog_match else "{}"
        )
        self.assertEqual(expected_dialogs, observed_dialogs)
        self.assertIn("name not in dialog_screenshot_names", self.source)
        self.assertIn("name in dialog_screenshot_names", self.source)

    def test_muscle_memory_receipt_is_a_downstream_observation(self) -> None:
        readiness_specs = re.search(
            r"specs = \[(.*?)\]\nraise SystemExit",
            self.source,
            re.DOTALL,
        )
        self.assertIsNotNone(readiness_specs)
        readiness_block = readiness_specs.group(1) if readiness_specs else ""
        self.assertIn("chummer6-ui.chummer5a_layout_hard_gate", readiness_block)
        self.assertIn(
            "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
            readiness_block,
        )
        self.assertNotIn(
            "chummer6-ui.chummer5a_muscle_memory_parity_gate",
            readiness_block,
        )
        self.assertIn(
            '"chummer5a_muscle_memory_parity_gate_role"] = (',
            self.source,
        )
        self.assertIn('"downstream_observation"', self.source)


if __name__ == "__main__":
    unittest.main()
