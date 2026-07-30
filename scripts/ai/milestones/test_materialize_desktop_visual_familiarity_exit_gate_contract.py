#!/usr/bin/env python3
from __future__ import annotations

import ast
import binascii
import hashlib
import json
import os
import struct
import subprocess
import tempfile
import unittest
import zlib
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Callable


SCRIPT_PATH = Path(__file__).with_name(
    "materialize-desktop-visual-familiarity-exit-gate.sh"
)
REPO_ROOT = SCRIPT_PATH.parents[3]
CONTROL_NAME = "SCREENSHOT_CONTROL_EVIDENCE.generated.json"


def now_iso(*, delta: timedelta = timedelta()) -> str:
    return (datetime.now(timezone.utc) + delta).isoformat().replace("+00:00", "Z")


def write_json(path: Path, payload: dict[str, Any]) -> bytes:
    rendered = (json.dumps(payload, indent=2) + "\n").encode()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(rendered)
    return rendered


def png_bytes(width: int = 1280, height: int = 800) -> bytes:
    signature = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    scanline = b"\x00" + (b"\x35\x72\xa1" * width)
    idat = zlib.compress(scanline * height, level=9)

    def chunk(chunk_type: bytes, data: bytes) -> bytes:
        crc = binascii.crc32(chunk_type)
        crc = binascii.crc32(data, crc) & 0xFFFFFFFF
        return (
            struct.pack(">I", len(data))
            + chunk_type
            + data
            + struct.pack(">I", crc)
        )

    return (
        signature
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", idat)
        + chunk(b"IEND", b"")
    )


class VisualFixture:
    def __init__(
        self,
        root: Path,
        inventory: list[str],
        workflow_coverage: dict[str, list[str]],
    ) -> None:
        self.root = root
        self.output_path = root / "DESKTOP_VISUAL.generated.json"
        self.flagship_path = root / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
        self.release_path = root / "RELEASE_CHANNEL.generated.json"
        self.screenshot_dir = root / "screenshots"
        self.control_path = self.screenshot_dir / CONTROL_NAME
        self.prerequisites = {
            "layout": root / "CHUMMER5A_LAYOUT_HARD_GATE.generated.json",
            "chrome": root / "CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE.generated.json",
            "muscle": root / "CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE.generated.json",
        }
        self.inventory = inventory
        self.workflow_coverage = workflow_coverage
        self.png = png_bytes()
        self.screenshot_dir.mkdir(parents=True)
        for name in inventory:
            (self.screenshot_dir / name).write_bytes(self.png)
        self.write_control()
        self.write_release()
        self.write_flagship()
        self.write_prerequisites()

    @property
    def generated_at(self) -> str:
        return now_iso()

    @property
    def pack_sha256(self) -> str:
        entry_sha = hashlib.sha256(self.png).hexdigest()
        inventory_bytes = b"".join(
            name.encode()
            + b"\0"
            + entry_sha.encode()
            + b"\0"
            + str(len(self.png)).encode()
            + b"\n"
            for name in sorted(self.inventory)
        )
        return hashlib.sha256(inventory_bytes).hexdigest()

    def control_payload(self) -> dict[str, Any]:
        return {
            "schemaVersion": 1,
            "contract_name": "chummer6-ui.screenshot_control_evidence",
            "generatedAt": self.generated_at,
            "authority": {
                "visualBaseline": "Chummer5a",
                "designAuthorityPlatform": "windows",
                "captureHead": "avalonia",
                "captureMode": "avalonia_headless_test_harness",
                "actualCaptureOperatingSystem": "fixture-os",
                "actualCaptureArchitecture": "fixture-arch",
                "releaseCandidateBound": False,
            },
            "screenshotCount": len(self.inventory),
            "screenshotPackSha256": self.pack_sha256,
            "screenshotPackDigestAlgorithm": "sha256-canonical-inventory-v1",
            "entries": [
                {
                    "screenshot": name,
                    "sha256": hashlib.sha256(self.png).hexdigest(),
                    "sizeBytes": len(self.png),
                }
                for name in self.inventory
            ],
            "workflowCoverage": [
                {
                    "workflowFamilyId": family_id,
                    "screenshotFiles": names,
                    "screenshotCount": len(names),
                }
                for family_id, names in self.workflow_coverage.items()
            ],
        }

    def write_control(
        self, mutate: Callable[[dict[str, Any]], None] | None = None
    ) -> dict[str, Any]:
        payload = self.control_payload()
        if mutate:
            mutate(payload)
        write_json(self.control_path, payload)
        return payload

    def release_payload(self) -> dict[str, Any]:
        generated_at = self.generated_at
        return {
            "contract_name": "Chummer.Hub.Registry.Contracts",
            "status": "published",
            "channelId": "preview",
            "channel": "preview",
            "releaseVersion": "fixture-v1",
            "version": "fixture-v1",
            "generatedAt": generated_at,
            "generated_at": generated_at,
        }

    def write_release(
        self, mutate: Callable[[dict[str, Any]], None] | None = None
    ) -> dict[str, Any]:
        payload = self.release_payload()
        if mutate:
            mutate(payload)
        write_json(self.release_path, payload)
        return payload

    def flagship_payload(self) -> dict[str, Any]:
        generated_at = self.generated_at
        control_bytes = self.control_path.read_bytes()
        control = json.loads(control_bytes)
        release_bytes = self.release_path.read_bytes()
        release = json.loads(release_bytes)
        passing = "pass"
        interaction_keys = [
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
        release_path = str(self.release_path.resolve())
        control_path = str(self.control_path.resolve())
        return {
            "generatedAt": generated_at,
            "contract_name": "chummer6-ui.flagship_ui_release_gate",
            "contractName": "chummer6-ui.flagship_ui_release_gate",
            "status": passing,
            "channelId": release["channelId"],
            "channel": release["channel"],
            "releaseVersion": release["releaseVersion"],
            "version": release["version"],
            "blockingFindings": [],
            "desktopHeads": ["avalonia", "blazor-desktop"],
            "interactionProof": {key: passing for key in interaction_keys},
            "headProofs": {
                "avalonia": {
                    "status": passing,
                    "visualReview": passing,
                    "themeReadabilityContrast": passing,
                    "bundledDemoRunner": passing,
                    "requiredRuntimeBackedTests": ["fixture-test"],
                    "sourceTestFile": str(
                        REPO_ROOT
                        / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
                    ),
                    "testSuites": ["AvaloniaFlagshipUiGateTests"],
                },
                "blazor-desktop": {
                    "status": passing,
                    "shellChrome": passing,
                    "commandSurface": passing,
                    "dialogSurface": passing,
                    "journeyPanels": passing,
                    "requiredShellTests": ["fixture-test"],
                    "sourceTestFile": str(
                        REPO_ROOT
                        / "Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"
                    ),
                    "testSuites": ["DesktopShellRulesetCatalogTests"],
                },
            },
            "releaseChannelEvidence": {
                "path": release_path,
                "contract_name": release["contract_name"],
                "status": release["status"],
                "channelId": release["channelId"],
                "releaseVersion": release["releaseVersion"],
                "sha256": hashlib.sha256(release_bytes).hexdigest(),
                "sizeBytes": len(release_bytes),
                "generatedAt": release["generatedAt"],
            },
            "visualReviewEvidence": {
                "screenshotControlEvidencePath": control_path,
                "screenshotControlSha256": hashlib.sha256(control_bytes).hexdigest(),
                "screenshotControlSizeBytes": len(control_bytes),
                "screenshotControlGeneratedAt": control["generatedAt"],
                "screenshotControlSchemaVersion": control["schemaVersion"],
                "screenshotCount": control["screenshotCount"],
                "screenshotPackSha256": control["screenshotPackSha256"],
                "screenshotPackDigestAlgorithm": control[
                    "screenshotPackDigestAlgorithm"
                ],
            },
        }

    def write_flagship(
        self, mutate: Callable[[dict[str, Any]], None] | None = None
    ) -> dict[str, Any]:
        payload = self.flagship_payload()
        if mutate:
            mutate(payload)
        write_json(self.flagship_path, payload)
        return payload

    def write_prerequisites(self) -> None:
        contracts = {
            "layout": "chummer6-ui.chummer5a_layout_hard_gate",
            "chrome": "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
            "muscle": "chummer6-ui.chummer5a_muscle_memory_parity_gate",
        }
        for key, path in self.prerequisites.items():
            write_json(
                path,
                {
                    "generatedAt": self.generated_at,
                    "contract_name": contracts[key],
                    "contractName": contracts[key],
                    "status": "pass",
                },
            )

    def run(self) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        environment.update(
            {
                "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH": str(self.output_path),
                "CHUMMER_DESKTOP_VISUAL_FLAGSHIP_GATE_PATH": str(
                    self.flagship_path
                ),
                "CHUMMER_DESKTOP_VISUAL_SCREENSHOT_DIR": str(self.screenshot_dir),
                "CHUMMER_DESKTOP_VISUAL_SCREENSHOT_CONTROL_EVIDENCE_PATH": str(
                    self.control_path
                ),
                "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH": str(
                    self.release_path
                ),
                "CHUMMER5A_LAYOUT_HARD_GATE_PATH": str(
                    self.prerequisites["layout"]
                ),
                "CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE_PATH": str(
                    self.prerequisites["chrome"]
                ),
                "CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE_PATH": str(
                    self.prerequisites["muscle"]
                ),
                "CHUMMER_DESKTOP_VISUAL_SKIP_PREREQUISITE_RECEIPT_REFRESH": "1",
                "CHUMMER_DESKTOP_VISUAL_SKIP_DOWNSTREAM_READINESS": "1",
                "CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS": "0",
                "CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE": "0",
            }
        )
        return subprocess.run(
            ["bash", str(SCRIPT_PATH)],
            cwd=REPO_ROOT,
            env=environment,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )

    def result_payload(self) -> dict[str, Any]:
        return json.loads(self.output_path.read_text(encoding="utf-8"))


class DesktopVisualFamiliarityExitGateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = SCRIPT_PATH.read_text(encoding="utf-8")
        marker = "python3 - <<'PY' \\\n  \"$repo_root\" \\\n"
        start = cls.source.index("from __future__ import annotations", cls.source.index(marker))
        end = cls.source.index("\nPY\n", start)
        cls.validator_source = cls.source[start:end]
        module = ast.parse(cls.validator_source)
        constants: dict[str, Any] = {}
        for statement in module.body:
            if not isinstance(statement, ast.Assign) or len(statement.targets) != 1:
                continue
            target = statement.targets[0]
            if not isinstance(target, ast.Name):
                continue
            if target.id not in {
                "canonical_screenshot_inventory",
                "canonical_workflow_coverage",
            }:
                continue
            constants[target.id] = ast.literal_eval(statement.value)
        cls.inventory = constants["canonical_screenshot_inventory"]
        cls.workflow_coverage = constants["canonical_workflow_coverage"]

    def fixture(self, root: Path) -> VisualFixture:
        return VisualFixture(root, self.inventory, self.workflow_coverage)

    def assert_failed(
        self,
        fixture: VisualFixture,
        fragment: str,
    ) -> dict[str, Any]:
        result = fixture.run()
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        payload = fixture.result_payload()
        self.assertEqual(payload["status"], "fail")
        self.assertTrue(
            any(fragment in reason for reason in payload["reasons"]),
            f"missing {fragment!r} in {payload['reasons']!r}",
        )
        return payload

    # Static safety/contract checks keep mutation and recovery expectations local.
    def test_01_embedded_validator_is_valid_python(self) -> None:
        ast.parse(self.validator_source)

    def test_02_validator_requires_strict_png_chunk_structure(self) -> None:
        for marker in ("missing IEND chunk", "CRC mismatch", "trailing bytes after IEND"):
            self.assertIn(marker, self.validator_source)

    def test_03_validator_requires_exact_control_schema_and_contract(self) -> None:
        self.assertIn("SCREENSHOT_CONTROL_SCHEMA_VERSION = 1", self.validator_source)
        self.assertIn(
            'SCREENSHOT_CONTROL_CONTRACT_NAME = "chummer6-ui.screenshot_control_evidence"',
            self.validator_source,
        )

    def test_04_validator_requires_exact_capture_authority(self) -> None:
        for marker in (
            '"designAuthorityPlatform": "windows"',
            '"captureMode": "avalonia_headless_test_harness"',
            "release_candidate_bound is not False",
        ):
            self.assertIn(marker, self.validator_source)

    def test_05_validator_requires_release_contract_and_published_status(self) -> None:
        self.assertIn('!= "Chummer.Hub.Registry.Contracts"', self.validator_source)
        self.assertIn('release_channel_status != "published"', self.validator_source)

    def test_06_validator_requires_both_release_alias_pairs(self) -> None:
        for marker in (
            "missing required channelId alias",
            "missing required channel alias",
            "missing required releaseVersion alias",
            "missing required version alias",
        ):
            self.assertIn(marker, self.validator_source)

    def test_07_validator_binds_flagship_to_exact_release_bytes(self) -> None:
        for marker in ("releaseChannelEvidence", '"sha256"', '"sizeBytes"'):
            self.assertIn(marker, self.validator_source)

    def test_08_validator_requires_two_upstream_contracts_and_observes_muscle_memory(self) -> None:
        for marker in (
            "chummer6-ui.chummer5a_layout_hard_gate",
            "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
        ):
            self.assertIn(marker, self.source)
        self.assertIn(
            '"chummer5a_muscle_memory_parity_gate_role"] = (',
            self.source,
        )
        self.assertIn('"downstream_observation"', self.source)
        self.assertIn(
            "never make that downstream receipt a prerequisite",
            self.source,
        )

    def test_09_validator_enforces_receipt_freshness_and_future_skew(self) -> None:
        self.assertIn("validate_receipt_freshness(", self.validator_source)
        self.assertIn("max_future_skew_seconds", self.validator_source)

    def test_10_validator_requires_exact_inventory_and_workflow_coverage(self) -> None:
        self.assertIn("unexpected_control_entries", self.validator_source)
        self.assertIn("mismatched_workflow_family_screenshots", self.validator_source)

    def test_11_validator_rejects_symlinked_pack_components(self) -> None:
        self.assertIn("symlinked_path_components", self.validator_source)
        self.assertIn("Screenshot directory path contains symlinked component", self.validator_source)

    def test_12_validator_rechecks_all_authoritative_snapshots(self) -> None:
        for marker in (
            "release_channel_snapshot_recheck",
            "screenshot_snapshot_recheck",
            "changed_png_fingerprints",
        ):
            self.assertIn(marker, self.validator_source)

    def test_13_output_uses_atomic_replace_and_fsync(self) -> None:
        for marker in ("tempfile.mkstemp", "os.fsync", "os.replace"):
            self.assertIn(marker, self.validator_source)

    def test_14_arbitrary_historical_pack_discovery_is_absent(self) -> None:
        self.assertNotIn("find ", self.source)
        self.assertNotIn("glob(", self.validator_source)

    def test_15_explicit_refresh_delegates_only_to_b14_lane(self) -> None:
        self.assertIn("b14_flagship_ui_release_gate_script_path", self.source)
        self.assertIn("explicit b14 refresh only supports", self.source)

    def test_16_output_carries_contract_channel_and_version_aliases(self) -> None:
        for marker in (
            '"contract_name": "chummer6-ui.desktop_visual_familiarity_exit_gate"',
            '"channelId": release_channel_channel_id',
            '"channel": release_channel_channel_id',
            '"releaseVersion": release_channel_version',
            '"version": release_channel_version',
        ):
            self.assertIn(marker, self.validator_source)

    # Behavioral tests run the exact producer against a temp-only evidence shelf.
    def test_17_complete_current_fixture_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            result = fixture.run()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual(fixture.result_payload()["status"], "pass")

    def test_18_passing_output_preserves_release_aliases(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            write_json(fixture.output_path, {"sentinel": "preexisting-receipt"})
            result = fixture.run()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            payload = fixture.result_payload()
            self.assertEqual(payload["channelId"], payload["channel"])
            self.assertEqual(payload["releaseVersion"], payload["version"])
            self.assertNotIn("sentinel", payload)
            self.assertFalse(
                any(
                    path.name.startswith(f".{fixture.output_path.name}.")
                    and path.name.endswith(".tmp")
                    for path in fixture.output_path.parent.iterdir()
                )
            )

    def test_19_wrong_flagship_contract_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_flagship(lambda value: value.__setitem__("contract_name", "generic.pass"))
            self.assert_failed(fixture, "contract_name is not chummer6-ui.flagship_ui_release_gate")

    def test_20_nonpassing_flagship_with_generic_blocker_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            def mutate(value: dict[str, Any]) -> None:
                value["status"] = "fail"
                value["blockingFindings"] = ["generic blocker"]
            fixture.write_flagship(mutate)
            self.assert_failed(fixture, "blockers are not the tightly recognized external-desktop-only set")

    def test_21_stale_flagship_receipt_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_flagship(lambda value: value.__setitem__("generatedAt", now_iso(delta=timedelta(days=-2))))
            self.assert_failed(fixture, "flagship_ui_release_gate is stale")

    def test_22_future_flagship_receipt_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_flagship(lambda value: value.__setitem__("generatedAt", now_iso(delta=timedelta(hours=1))))
            self.assert_failed(fixture, "flagship_ui_release_gate generatedAt is in the future")

    def test_23_unpublished_release_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_release(lambda value: value.__setitem__("status", "pass"))
            self.assert_failed(fixture, "release channel status is not published")

    def test_24_conflicting_release_channel_alias_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_release(lambda value: value.__setitem__("channel", "stable"))
            self.assert_failed(fixture, "conflicting channelId/channel aliases")

    def test_25_conflicting_release_version_alias_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_release(lambda value: value.__setitem__("version", "fixture-v2"))
            self.assert_failed(fixture, "conflicting releaseVersion/version aliases")

    def test_26_stale_release_receipt_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            def mutate(value: dict[str, Any]) -> None:
                stale = now_iso(delta=timedelta(days=-2))
                value["generatedAt"] = stale
                value["generated_at"] = stale
            fixture.write_release(mutate)
            self.assert_failed(fixture, "release_channel is stale")

    def test_27_wrong_prerequisite_contract_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            payload = json.loads(fixture.prerequisites["layout"].read_text())
            payload["contract_name"] = "generic.pass"
            payload["contractName"] = "generic.pass"
            write_json(fixture.prerequisites["layout"], payload)
            self.assert_failed(fixture, "chummer5a_layout_hard_gate receipt contract is not recognized")

    def test_28_nonpassing_prerequisite_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            payload = json.loads(fixture.prerequisites["chrome"].read_text())
            payload["status"] = "fail"
            write_json(fixture.prerequisites["chrome"], payload)
            self.assert_failed(fixture, "chummer5a_legacy_equivalent_chrome_gate receipt is not passing")

    def test_29_stale_downstream_muscle_memory_receipt_is_observed_without_a_cycle(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            payload = json.loads(fixture.prerequisites["muscle"].read_text())
            payload["generatedAt"] = now_iso(delta=timedelta(days=-2))
            write_json(fixture.prerequisites["muscle"], payload)
            result = fixture.run()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            receipt = fixture.result_payload()
            self.assertEqual(receipt["status"], "pass")
            self.assertEqual(
                receipt["evidence"][
                    "chummer5a_muscle_memory_parity_gate_role"
                ],
                "downstream_observation",
            )

    def test_30_wrong_control_schema_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_control(lambda value: value.__setitem__("schemaVersion", 2))
            self.assert_failed(fixture, "schemaVersion must be 1")

    def test_31_wrong_capture_authority_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_control(
                lambda value: value["authority"].__setitem__("captureMode", "generic")
            )
            self.assert_failed(fixture, "authority does not match the release-authority contract")

    def test_32_png_trailing_bytes_after_iend_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            target = fixture.screenshot_dir / fixture.inventory[0]
            target.write_bytes(target.read_bytes() + b"laundered")
            self.assert_failed(fixture, "trailing bytes after IEND")

    def test_33_flagship_visual_binding_mismatch_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self.fixture(Path(temporary))
            fixture.write_flagship(
                lambda value: value["visualReviewEvidence"].__setitem__(
                    "screenshotPackSha256", "0" * 64
                )
            )
            self.assert_failed(
                fixture,
                "visualReviewEvidence does not match the validated screenshot control/pack",
            )


if __name__ == "__main__":
    unittest.main()
