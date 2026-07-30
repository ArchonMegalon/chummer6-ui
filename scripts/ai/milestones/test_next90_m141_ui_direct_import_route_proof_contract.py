#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import struct
import subprocess
import tempfile
import unittest
import uuid
import zlib
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SCRIPT = Path(__file__).with_name("next90-m141-ui-direct-import-route-proof-check.sh")
PACKAGE_ID = "next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment"
TITLE = "Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."
DO_NOT_REOPEN = "M141 chummer6-ui translator, XML amendment, and Hero Lab direct route proof is complete; future shards must verify the closed-package receipt, focused guard test, runtime-backed screenshot gates, canonical registry row, and queue mirrors instead of reopening this slice."
SCREENSHOTS = [
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
]
CHANNEL = "test-stable"
VERSION = "run-test-m141"
TMP_ROOT = Path("/dev/shm") if Path("/dev/shm").is_dir() else None


SOURCE_MARKERS = {
    "Chummer.Presentation/Overview/OverviewCommandDispatcher.cs": [
        'if (string.Equals(commandId, "translator", StringComparison.Ordinal))',
        '|| string.Equals(commandId, "translator", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "xml_editor", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "hero_lab_importer", StringComparison.Ordinal)',
    ],
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs": [
        '"dialog.translator"',
        '"translatorLanePosture"',
        '"dialog.xml_editor"',
        '"xmlEditorLanePosture"',
        '"dialog.hero_lab_importer"',
        '"heroLabImportOracleLanePosture"',
        '"heroLabAdjacentSr6OracleReceipt"',
    ],
    "Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs": [
        'Command("translator", "command.translator", "tools", false)',
        'Command("xml_editor", "command.xml_editor", "tools", false)',
        'Command("hero_lab_importer", "command.hero_lab_importer", "tools", false)',
    ],
    "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs": [
        "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
        "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
        "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
    ],
    "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs": [
        "CreateCommandDialog_translator_prefers_catalog_languages_and_surfaces_lane_posture",
        "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        "CreateCommandDialog_hero_lab_importer_surfaces_import_oracle_and_adjacent_sr6_posture",
    ],
    "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs": [
        "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        "Avalonia_and_Blazor_hero_lab_importer_dialog_preserves_matching_import_oracle_posture",
    ],
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
        "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
        'GetImportRouteReviewStep("translator").ScreenshotFileName',
        'GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName',
        'GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName',
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh": [
        '"translator": {',
        '"xml_editor": {',
        '"hero_lab_importer": {',
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M141 direct import-route proof guard",
        "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
    ],
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def write_json(path: Path, payload: dict[str, Any]) -> None:
    write_text(path, json.dumps(payload, indent=2) + "\n")


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)


def valid_png() -> bytes:
    ihdr = struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)
    pixel = zlib.compress(b"\x00\x00\x00\x00\xff")
    return b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", ihdr) + png_chunk(b"IDAT", pixel) + png_chunk(b"IEND", b"")


class Fixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.script = root / "scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh"
        self.registry = root / ".codex-design/product/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml"
        self.queue = root / ".codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
        self.receipt = root / ".codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"
        self.screenshot_dir = root / ".codex-studio/published/ui-flagship-release-gate-screenshots"
        self.release = root / "release/RELEASE_CHANNEL.generated.json"
        self.frontier_root = root / "fleet/.codex-studio/published/full-product-frontiers"
        self.frontier = self.frontier_root / "shard-1.generated.yaml"
        self.flagship_queue = self.frontier_root.parent / "NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
        self.support = root / ".codex-studio/published"
        self._create()

    def proof_entries(self) -> list[str]:
        relative_files = [
            "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
            "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
            "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs",
            "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
            "Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs",
            "Chummer.Tests/Chummer.Tests.csproj",
            "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
            "scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
            "scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
            "scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
            "scripts/ai/verify.sh",
            ".codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json",
        ]
        return [str(self.root / item) for item in relative_files] + [
            "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
            'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests" --no-restore',
        ]

    def registry_evidence(self) -> list[str]:
        root = self.root
        return [
            f"{root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs, {root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs, {root}/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs, and {root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs keep the translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle flows bound to direct screenshot-backed and runtime-backed desktop route proof instead of broad family prose.",
            f"{root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, {root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh, {root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh, and {root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json keep the direct screenshot pack, runtime-backed route receipts, and published closure proof aligned for translator, XML amendment, Hero Lab importer, and adjacent import-oracle coverage.",
            f"{root}/Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs, {root}/Chummer.Tests/Chummer.Tests.csproj, {root}/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh, and {root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, verify wiring, or worker-safe flagship frontier evidence drift from the completed package contract.",
            f"{root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json records the closed-package receipt for `next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment`.",
        ]

    def _create_registry(self) -> None:
        markers = [
            "title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes",
            "source:translator_route",
            "source:xml_amendment_editor_route",
            "source:hero_lab_importer_route",
            "family:custom_data_xml_and_translator_bridge",
            "family:legacy_and_adjacent_import_oracles",
            "Direct screenshot-backed and runtime-backed receipts exist for `menu:translator`, `menu:xml_editor`, `menu:hero_lab_importer`,",
        ]
        evidence = "\n".join(f"        - {item}" for item in self.registry_evidence())
        registry = f"""milestones:
  - id: 141
    title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes
    markers:
      - {markers[1]}
      - {markers[2]}
      - {markers[3]}
      - {markers[4]}
      - {markers[5]}
    acceptance: {markers[6]}
    tasks:
    - id: '141.1'
      owner: chummer6-ui
      title: {TITLE}
      status: complete
      completion_action: verify_closed_package_only
      do_not_reopen_reason: {DO_NOT_REOPEN}
      evidence:
{evidence}
"""
        write_text(self.registry, registry)

    def _create_queue(self) -> None:
        proof = "\n".join(f"  - {item}" for item in self.proof_entries())
        queue = f"""- title: {TITLE}
  task: {TITLE}
  package_id: {PACKAGE_ID}
  frontier_id: 2354698282
  work_task_id: '141.1'
  status: complete
  wave: W22P
  repo: chummer6-ui
  completion_action: verify_closed_package_only
  do_not_reopen_reason: {DO_NOT_REOPEN}
  proof:
{proof}
  allowed_paths:
  - Chummer.Avalonia
  - Chummer.Desktop.Runtime
  - Chummer.Tests
  - scripts
  owned_surfaces:
  - capture_direct_screenshot_and_runtime_proof_for_translat:ui
"""
        write_text(self.queue, queue)

    def _create_sources(self) -> None:
        for relative, markers in SOURCE_MARKERS.items():
            write_text(self.root / relative, "\n".join(markers) + "\n")
        shutil.copy2(SCRIPT, self.script)
        self.script.chmod(0o755)
        for relative in [
            "Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs",
            "Chummer.Tests/Chummer.Tests.csproj",
            "scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
        ]:
            write_text(self.root / relative, f"fixture proof {relative}\n")

    def _create_frontier(self) -> None:
        write_text(
            self.frontier,
            """contract_name: fleet.full_product_frontier
schema_version: 1
mode: flagship_product
quality_policy:
  bar: top_flagship_grade
  whole_project_frontier: true
  accept_lowered_standards: false
frontier_count: 1
frontier_ids:
- 4066417069
frontier:
- id: 4066417069
  title: Current flagship desktop closeout
""",
        )
        proof = "\n".join(f"  - {item}" for item in self.proof_entries())
        write_text(
            self.flagship_queue,
            f"""mode: append
program_wave: next_90_day_product_advance
status: live_parallel_successor
source_registry_path: /docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml
activation_rule: Use these items immediately on shards whose current flagship closeout slice is empty; do not let them replace
  active shards already assigned to the current closeout frontier.
items:
- title: {TITLE}
  task: {TITLE}
  package_id: {PACKAGE_ID}
  frontier_id: 2354698282
  milestone_id: 141
  work_task_id: '141.1'
  status: complete
  wave: W22P
  repo: chummer6-ui
  completion_action: verify_closed_package_only
  do_not_reopen_reason: {DO_NOT_REOPEN}
  proof:
{proof}
  allowed_paths:
  - Chummer.Avalonia
  - Chummer.Desktop.Runtime
  - Chummer.Tests
  - scripts
  owned_surfaces:
  - capture_direct_screenshot_and_runtime_proof_for_translat:ui
""",
        )

    def _release_fields(self) -> dict[str, str]:
        return {
            "channelId": CHANNEL,
            "channel": CHANNEL,
            "version": VERSION,
            "releaseVersion": VERSION,
        }

    def _create_receipts(self) -> None:
        generated = utc_now()
        release_fields = self._release_fields()
        write_json(
            self.release,
            {
                "schemaVersion": 1,
                "contract_name": "Chummer.Hub.Registry.Contracts",
                "contractName": "Chummer.Hub.Registry.Contracts",
                "status": "published",
                "generatedAt": generated,
                **release_fields,
            },
        )
        write_json(
            self.support / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
            {
                "schemaVersion": 1,
                "contract_name": "chummer6-ui.desktop_visual_familiarity_exit_gate",
                "status": "pass",
                "generatedAt": generated,
                **release_fields,
                "evidence": {
                    "required_screenshots": SCREENSHOTS,
                    "missing_screenshots": [],
                    "screenshot_dir": str(self.screenshot_dir),
                },
            },
        )
        route_receipts = {
            "translator_xml_custom_data": {
                "status": "pass",
                "routeIds": [
                    "translator",
                    "xml_editor",
                    "source:translator_route",
                    "source:xml_amendment_editor_route",
                    "family:custom_data_xml_and_translator_bridge",
                ],
                "workflowFamilyId": "improvements-explain-result-parity",
                "screenshots": SCREENSHOTS[:2],
            },
            "hero_lab_import_oracle": {
                "status": "pass",
                "routeIds": [
                    "hero_lab_importer",
                    "source:hero_lab_importer_route",
                    "family:legacy_and_adjacent_import_oracles",
                ],
                "workflowFamilyId": "create-open-import-save-save-as-print-export",
                "screenshots": SCREENSHOTS[2:],
            },
        }
        write_json(
            self.support / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
            {
                "schemaVersion": 1,
                "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
                "status": "pass",
                "generatedAt": generated,
                **release_fields,
                "evidence": {
                    "reviewedJobs": ["translator", "xml_editor", "hero_lab_importer"],
                    "failingJobs": [],
                    "routeLocalReceipts": route_receipts,
                },
            },
        )
        write_json(
            self.support / "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
            {
                "schemaVersion": 1,
                "contractName": "chummer6-ui.veteran_task_time_evidence_gate",
                "status": "pass",
                "generatedAt": generated,
                "evidence": {
                    "coveredJobs": ["translator_xml_custom_data"],
                    "screenshotReviewJobs": [],
                },
            },
        )
        write_json(
            self.support / "UI_FLAGSHIP_RELEASE_GATE.generated.json",
            {
                "schemaVersion": 1,
                "contract_name": "chummer6-ui.flagship_ui_release_gate",
                "status": "pass",
                "generatedAt": generated,
                **release_fields,
                "directImportRouteProof": {
                    "reviewJobs": ["translator_xml_custom_data", "hero_lab_import_oracle"],
                    "screenshots": SCREENSHOTS,
                    "characterOverviewPresenterTests": [
                        "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
                        "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
                        "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
                    ],
                },
            },
        )

    def _create_screenshots(self) -> None:
        self.screenshot_dir.mkdir(parents=True, exist_ok=True)
        payload = valid_png()
        for name in SCREENSHOTS:
            (self.screenshot_dir / name).write_bytes(payload)

    def _create(self) -> None:
        self._create_registry()
        self._create_queue()
        self._create_sources()
        self._create_frontier()
        self._create_receipts()
        self._create_screenshots()
        self.receipt.parent.mkdir(parents=True, exist_ok=True)

    def load_support(self, name: str) -> dict[str, Any]:
        return json.loads((self.support / name).read_text(encoding="utf-8"))

    def write_support(self, name: str, payload: dict[str, Any]) -> None:
        write_json(self.support / name, payload)

    def run(self, *, extra_env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        environment.update(
            {
                "CHUMMER_UI_REPO_ROOT_ALIAS": str(self.root / "missing-alias"),
                "CHUMMER_NEXT90_REGISTRY_PATH": str(self.registry),
                "CHUMMER_NEXT90_QUEUE_PATH": str(self.queue),
                "CHUMMER_NEXT90_DESIGN_QUEUE_PATH": str(self.queue),
                "CHUMMER_NEXT90_M141_UI_RECEIPT_PATH": str(self.receipt),
                "CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH": str(self.release),
                "CHUMMER_FLAGSHIP_FRONTIER_ROOT": str(self.frontier_root),
                "CHUMMER_FLAGSHIP_FRONTIER_PATH": str(self.frontier),
                "CHUMMER_FLAGSHIP_QUEUE_PATH": str(self.flagship_queue),
                "CHUMMER_FLAGSHIP_FRONTIER_ID": "1922169755",
            }
        )
        if extra_env:
            environment.update(extra_env)
        return subprocess.run(
            ["bash", str(self.script)],
            cwd=self.root,
            env=environment,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=20,
            check=False,
        )


class M141ContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="m141-contract-",
            dir=str(TMP_ROOT) if TMP_ROOT is not None else None,
        )
        self.fixture = Fixture(Path(self.temporary.name) / "repo")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def assert_fails(self, expected: str | None = None, *, extra_env: dict[str, str] | None = None) -> None:
        result = self.fixture.run(extra_env=extra_env)
        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        combined = result.stdout + result.stderr
        if expected is not None:
            self.assertIn(expected, combined)

    def test_valid_fixture_passes_with_bound_atomic_receipt(self) -> None:
        result = self.fixture.run()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(self.fixture.receipt.read_text(encoding="utf-8"))
        self.assertEqual(1, receipt["schemaVersion"])
        uuid.UUID(receipt["producerRunId"])
        self.assertEqual("pass", receipt["status"])
        self.assertEqual(CHANNEL, receipt["channelId"])
        self.assertEqual(CHANNEL, receipt["channel"])
        self.assertEqual(VERSION, receipt["version"])
        self.assertEqual(VERSION, receipt["releaseVersion"])
        bindings = receipt["evidence"]["bindings"]
        self.assertEqual(set(SCREENSHOTS), set(bindings["screenshots"]))
        self.assertEqual(64, len(bindings["releaseChannel"]["sha256"]))
        self.assertNotIn(str(self.fixture.receipt), bindings["proofFiles"])
        self.assertEqual(str(self.fixture.receipt), receipt["evidence"]["informationalOutputPath"])
        temporary_prefix = f".{self.fixture.receipt.name}."
        leftovers = [
            path
            for path in self.fixture.receipt.parent.iterdir()
            if path.name.startswith(temporary_prefix) and path.name.endswith(".tmp")
        ]
        self.assertEqual([], leftovers)

    def test_wrong_supporting_contract_fails(self) -> None:
        name = "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
        payload = self.fixture.load_support(name)
        payload["contract_name"] = "chummer6-ui.unrelated_receipt"
        self.fixture.write_support(name, payload)
        self.assert_fails("visualFamiliarityGate:contract_exact")

    def test_non_exact_status_fails(self) -> None:
        name = "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
        payload = self.fixture.load_support(name)
        payload["status"] = "ready"
        self.fixture.write_support(name, payload)
        self.assert_fails("veteranTaskTimeGate:status_exact")

    def test_release_alias_disagreement_fails(self) -> None:
        payload = json.loads(self.fixture.release.read_text(encoding="utf-8"))
        payload["channel"] = "different-channel"
        write_json(self.fixture.release, payload)
        self.assert_fails("release_channel:channel_aliases_present_and_agree")

    def test_release_scoped_support_mismatch_fails(self) -> None:
        name = "UI_FLAGSHIP_RELEASE_GATE.generated.json"
        payload = self.fixture.load_support(name)
        payload["version"] = "wrong-release"
        payload["releaseVersion"] = "wrong-release"
        self.fixture.write_support(name, payload)
        self.assert_fails("uiFlagshipReleaseGate:release_aligned")

    def test_symlinked_authority_is_rejected(self) -> None:
        path = self.fixture.support / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
        target = path.with_name("visual-copy.json")
        path.replace(target)
        path.symlink_to(target.name)
        self.assert_fails("symlink component")

    def test_fallback_only_top_level_jobs_and_skip_flag_do_not_pass(self) -> None:
        name = "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
        payload = self.fixture.load_support(name)
        payload["evidence"]["routeLocalReceipts"] = {}
        payload["reviewJobs"] = {
            "translator": {"status": "pass"},
            "xml_editor": {"status": "pass"},
            "hero_lab_importer": {"status": "pass"},
        }
        payload["reasons"] = ["UI flagship release gate is not passing."]
        self.fixture.write_support(name, payload)
        self.assert_fails(
            "translator_xml_custom_data:exists",
            extra_env={"CHUMMER_NEXT90_M141_SKIP_FLAGSHIP_GATE_DEPENDENCY": "1"},
        )

    def test_missing_direct_route_receipt_fails(self) -> None:
        name = "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
        payload = self.fixture.load_support(name)
        del payload["evidence"]["routeLocalReceipts"]["hero_lab_import_oracle"]
        self.fixture.write_support(name, payload)
        self.assert_fails("hero_lab_import_oracle:exists")

    def test_missing_durable_flagship_queue_package_fails(self) -> None:
        text = self.fixture.flagship_queue.read_text(encoding="utf-8")
        self.fixture.flagship_queue.write_text(
            text.replace(f"package_id: {PACKAGE_ID}", "package_id: unrelated-package"),
            encoding="utf-8",
        )
        self.assert_fails(f"missing package row for {PACKAGE_ID}")

    def test_missing_screenshot_fails(self) -> None:
        (self.fixture.screenshot_dir / SCREENSHOTS[0]).unlink()
        self.assert_fails(SCREENSHOTS[0])

    def test_png_tamper_after_iend_fails(self) -> None:
        path = self.fixture.screenshot_dir / SCREENSHOTS[1]
        path.write_bytes(path.read_bytes() + b"tamper")
        self.assert_fails(SCREENSHOTS[1])


if __name__ == "__main__":
    unittest.main(verbosity=2)
