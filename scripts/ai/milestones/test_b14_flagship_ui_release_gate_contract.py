#!/usr/bin/env python3
from __future__ import annotations

import binascii
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib
from datetime import datetime, timezone
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("b14-flagship-ui-release-gate.sh")
CONTROL_NAME = "SCREENSHOT_CONTROL_EVIDENCE.generated.json"


class B14FlagshipUiReleaseGateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = SCRIPT_PATH.read_text(encoding="utf-8")
        marker = (
            "python3 - <<'PY' \"$capture_screenshot_dir\" "
            '"$staged_screenshot_dir" "$screenshot_dir" "$avalonia_gate_tests_path" '
            '"$screenshot_pack_transaction_path" "$receipt_path"\n'
        )
        start = cls.source.index(marker) + len(marker)
        end = cls.source.index("\nPY\n", start)
        cls.screenshot_pack_program = cls.source[start:end]
        transaction_marker = (
            "python3 - <<'PY' \"$action\" \"$screenshot_pack_transaction_path\" "
            '"$screenshot_dir" "$receipt_path" "$@"\n'
        )
        transaction_start = cls.source.index(transaction_marker) + len(transaction_marker)
        transaction_end = cls.source.index("\nPY\n", transaction_start)
        cls.transaction_program = cls.source[transaction_start:transaction_end]

    @staticmethod
    def png_bytes(seed: int) -> bytes:
        signature = b"\x89PNG\r\n\x1a\n"
        ihdr = struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)
        idat = zlib.compress(b"\x00" + bytes([seed, 0, 255 - seed, 255]))

        def chunk(chunk_type: bytes, data: bytes) -> bytes:
            # Producer hashes bind these exact current-run bytes. Deliberately
            # invalid zero CRCs prove b14 rebinds hashes after normalization.
            return (
                struct.pack(">I", len(data))
                + chunk_type
                + data
                + struct.pack(">I", 0)
            )

        return signature + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")

    @staticmethod
    def fixture_control(files: dict[str, bytes]) -> dict:
        names = sorted(files)
        return {
            "contract_name": "chummer6-ui.screenshot_control_evidence",
            "schemaVersion": 1,
            "generatedAt": "2026-07-18T10:00:00+00:00",
            "screenshotCount": len(names),
            "authority": {
                "visualBaseline": "Chummer5a",
                "designAuthorityPlatform": "windows",
                "captureHead": "avalonia",
                "captureMode": "avalonia_headless_test_harness",
                "actualCaptureOperatingSystem": "fixture-os",
                "actualCaptureArchitecture": "fixture-arch",
                "releaseCandidateBound": False,
                "producerOwned": {"preserve": True},
            },
            "workflowCoverage": [
                {
                    "workflowFamilyId": "fixture-family",
                    "legacyBehaviorLineage": "fixture lineage",
                    "screenshotFiles": names,
                    "screenshotCount": len(names),
                    "producerSemanticField": ["must", "survive"],
                }
            ],
            "entries": [
                {
                    "screenshot": name,
                    "sha256": hashlib.sha256(files[name]).hexdigest(),
                    "sizeBytes": len(files[name]),
                    "theme": "light",
                    "producerSemanticField": {
                        "name": name,
                        "preserve": [1, 2, 3],
                    },
                }
                for name in names
            ],
        }

    def run_pack_program(
        self,
        capture_dir: Path,
        stage_dir: Path,
        published_dir: Path,
        *,
        producer_screenshot_names: list[str] | None = None,
        producer_workflow_coverage: list[dict] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        producer_source_path = capture_dir.parent / "FixtureCaptureProducer.cs"
        control_path = capture_dir / CONTROL_NAME
        if control_path.is_file():
            control = json.loads(control_path.read_text(encoding="utf-8"))
            screenshot_names = [
                str(entry["screenshot"])
                for entry in control.get("entries", [])
                if isinstance(entry, dict) and entry.get("screenshot")
            ]
            workflow_coverage = [
                row
                for row in control.get("workflowCoverage", [])
                if isinstance(row, dict)
            ]
        else:
            screenshot_names = sorted(
                path.name
                for path in capture_dir.iterdir()
                if path.suffix.lower() == ".png"
            )
            workflow_coverage = [
                {
                    "workflowFamilyId": "fixture-family",
                    "screenshotFiles": screenshot_names,
                }
            ]
        if producer_screenshot_names is not None:
            screenshot_names = producer_screenshot_names
        if producer_workflow_coverage is not None:
            workflow_coverage = producer_workflow_coverage
        inventory_source = ", ".join(json.dumps(name) for name in screenshot_names)
        coverage_source = ",\n".join(
            "new("
            + json.dumps(str(row.get("workflowFamilyId") or ""))
            + ', "fixture lineage", ['
            + ", ".join(
                json.dumps(str(name)) for name in row.get("screenshotFiles", [])
            )
            + "])"
            for row in workflow_coverage
        )
        producer_source_path.write_text(
            "private static readonly string[] VeteranCertificationScreenshotFiles = ["
            + inventory_source
            + "];\nprivate static readonly object[] WorkflowScreenshotCoverage = [\n"
            + coverage_source
            + "\n];\n",
            encoding="utf-8",
        )
        return subprocess.run(
            [
                sys.executable,
                "-c",
                self.screenshot_pack_program,
                str(capture_dir),
                str(stage_dir),
                str(published_dir),
                str(producer_source_path),
                str(capture_dir.parent / ".ui-flagship-screenshot-transaction.json"),
                str(capture_dir.parent / "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
            ],
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )

    def run_transaction_program(
        self,
        action: str,
        root: Path,
        published_dir: Path,
        *action_paths: Path,
        failpoint: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        if failpoint is not None:
            environment["CHUMMER_B14_TRANSACTION_TEST_FAILPOINT"] = failpoint
        return subprocess.run(
            [
                sys.executable,
                "-c",
                self.transaction_program,
                action,
                str(root / ".ui-flagship-screenshot-transaction.json"),
                str(published_dir),
                str(root / "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
                *(str(path) for path in action_paths),
            ],
            env=environment,
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )

    @staticmethod
    def write_passing_receipt(root: Path, published_dir: Path) -> Path:
        control_path = published_dir / CONTROL_NAME
        control_bytes = control_path.read_bytes()
        control = json.loads(control_bytes.decode("utf-8"))
        release_path = (root / "RELEASE_CHANNEL.generated.json").resolve()
        release_payload = {
            "contract_name": "Chummer.Hub.Registry.Contracts",
            "status": "published",
            "channelId": "preview",
            "channel": "preview",
            "releaseVersion": "fixture-v1",
            "version": "fixture-v1",
            "generatedAt": "2026-07-18T10:00:00Z",
        }
        release_bytes = (json.dumps(release_payload, indent=2) + "\n").encode("utf-8")
        release_path.write_bytes(release_bytes)
        receipt = {
            "contract_name": "chummer6-ui.flagship_ui_release_gate",
            "status": "pass",
            "channelId": "preview",
            "channel": "preview",
            "releaseVersion": "fixture-v1",
            "version": "fixture-v1",
            "releaseChannelEvidence": {
                "path": str(release_path),
                "contract_name": "Chummer.Hub.Registry.Contracts",
                "status": "published",
                "channelId": "preview",
                "releaseVersion": "fixture-v1",
                "sha256": hashlib.sha256(release_bytes).hexdigest(),
                "sizeBytes": len(release_bytes),
                "generatedAt": release_payload["generatedAt"],
            },
            "visualReviewEvidence": {
                "screenshotControlSha256": hashlib.sha256(control_bytes).hexdigest(),
                "screenshotControlSizeBytes": len(control_bytes),
                "screenshotCount": control["screenshotCount"],
                "screenshotPackSha256": control["screenshotPackSha256"],
                "screenshotPackDigestAlgorithm": control["screenshotPackDigestAlgorithm"],
                "screenshotDirectory": str(published_dir),
            },
        }
        receipt_path = root / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
        receipt_path.write_text(json.dumps(receipt) + "\n", encoding="utf-8")
        return receipt_path

    def publish_fixture(
        self, root: Path, *, seed: int = 71
    ) -> tuple[Path, Path, dict[str, str], bytes]:
        capture_dir = root / "capture"
        stage_dir = root / ".ui-flagship-screenshot-stage.fixture"
        published_dir = root / "published"
        capture_dir.mkdir()
        stage_dir.mkdir()
        published_dir.mkdir()
        (published_dir / "old-pack-marker.txt").write_text("old\n", encoding="utf-8")
        receipt_path = root / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
        receipt_path.write_text('{"status":"old-pass"}\n', encoding="utf-8")
        previous_pack = self.snapshot(published_dir)
        previous_receipt = receipt_path.read_bytes()
        files = {"01-fixture.png": self.png_bytes(seed)}
        for name, data in files.items():
            (capture_dir / name).write_bytes(data)
        (capture_dir / CONTROL_NAME).write_text(
            json.dumps(self.fixture_control(files), indent=2) + "\n",
            encoding="utf-8",
        )
        swapped = self.run_pack_program(capture_dir, stage_dir, published_dir)
        self.assertEqual(0, swapped.returncode, msg=swapped.stderr)
        return published_dir, stage_dir, previous_pack, previous_receipt

    @staticmethod
    def snapshot(directory: Path) -> dict[str, str]:
        return {
            item.name: hashlib.sha256(item.read_bytes()).hexdigest()
            for item in directory.iterdir()
            if item.is_file()
        }

    def test_embedded_python_and_shell_syntax_are_valid(self) -> None:
        completed = subprocess.run(
            ["bash", "-n", str(SCRIPT_PATH)],
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )
        self.assertEqual(0, completed.returncode, msg=completed.stderr)

        programs = re.findall(r"<<'PY'[^\n]*\n(.*?)\nPY(?:\n|$)", self.source, re.DOTALL)
        self.assertGreaterEqual(len(programs), 8)
        for index, program in enumerate(programs):
            try:
                compile(program, f"{SCRIPT_PATH.name}:heredoc-{index}", "exec")
            except SyntaxError as exc:
                self.fail(f"embedded Python heredoc {index} does not compile: {exc}")

    def test_current_run_schema_and_byte_bindings_are_fail_closed(self) -> None:
        self.assertIn("current-run screenshot control evidence was not produced", self.source)
        self.assertIn('control_evidence.get("contract_name") != CONTROL_CONTRACT', self.source)
        self.assertIn('control_evidence["schemaVersion"] != 1', self.source)
        self.assertIn('hashlib.sha256(source_bytes).hexdigest() != declared_sha256', self.source)
        self.assertIn('set(declared_names) != set(capture_pngs)', self.source)
        self.assertIn('extract_producer_contract(', self.source)
        self.assertIn('declared_names != sorted(expected_screenshot_names)', self.source)
        self.assertIn('observed_workflow_coverage[family_id] != expected_files', self.source)
        self.assertIn('entry["sha256"] = final_sha256', self.source)
        self.assertIn('entry["sizeBytes"] = final_size', self.source)
        self.assertNotIn("published_control_evidence_path", self.source)
        self.assertNotIn("source_control_evidence_path", self.source)
        self.assertNotIn("normalized_entries", self.source)
        self.assertNotIn('control_evidence["workflowCoverage"] = [', self.source)
        self.assertNotIn("os.utime(", self.source)

    def test_pack_publish_is_recoverable_and_not_partial_copy(self) -> None:
        self.assertIn('rename_exchange(stage_dir, published_dir)', self.source)
        self.assertIn('"contract_name": JOURNAL_CONTRACT', self.source)
        self.assertIn('manage_screenshot_pack_transaction recover', self.source)
        self.assertIn('manage_screenshot_pack_transaction commit', self.source)
        self.assertIn('fsync_directory(published_parent)', self.source)
        self.assertNotIn('rm -rf "$screenshot_dir"', self.source)
        self.assertNotIn('cp "$staged_screenshot_dir"', self.source)
        self.assertNotIn("shutil.copy2", self.source)

    def test_valid_fixture_preserves_semantics_and_rebinds_normalized_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".ui-flagship-screenshot-stage.fixture"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            (published_dir / "old-pack-marker.txt").write_text("old\n", encoding="utf-8")

            files = {
                "01-fixture-light.png": self.png_bytes(25),
                "02-fixture-dark.png": self.png_bytes(200),
            }
            for name, data in files.items():
                (capture_dir / name).write_bytes(data)
            source_control = self.fixture_control(files)
            expected_authority = json.loads(json.dumps(source_control["authority"]))
            expected_workflow = json.loads(json.dumps(source_control["workflowCoverage"]))
            expected_semantics = {
                entry["screenshot"]: json.loads(json.dumps(entry["producerSemanticField"]))
                for entry in source_control["entries"]
            }
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(source_control, indent=2) + "\n",
                encoding="utf-8",
            )

            completed = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertEqual(
                0,
                completed.returncode,
                msg=f"stdout={completed.stdout}\nstderr={completed.stderr}",
            )
            self.assertTrue(stage_dir.is_dir())
            self.assertTrue((stage_dir / "old-pack-marker.txt").is_file())
            self.assertFalse((published_dir / "old-pack-marker.txt").exists())

            control = json.loads((published_dir / CONTROL_NAME).read_text(encoding="utf-8"))
            self.assertEqual(expected_authority, control["authority"])
            self.assertEqual(expected_workflow, control["workflowCoverage"])
            self.assertEqual("2026-07-18T10:00:00+00:00", control["captureGeneratedAt"])
            self.assertEqual(control["normalizedAt"], control["generatedAt"])
            self.assertEqual("sha256-canonical-inventory-v1", control["screenshotPackDigestAlgorithm"])

            expected_inventory = set(files) | {CONTROL_NAME}
            self.assertEqual(expected_inventory, {item.name for item in published_dir.iterdir()})
            pack_hasher = hashlib.sha256()
            for entry in sorted(control["entries"], key=lambda item: item["screenshot"]):
                name = entry["screenshot"]
                final_bytes = (published_dir / name).read_bytes()
                self.assertNotEqual(files[name], final_bytes)
                self.assertEqual(hashlib.sha256(final_bytes).hexdigest(), entry["sha256"])
                self.assertEqual(len(final_bytes), entry["sizeBytes"])
                self.assertEqual(expected_semantics[name], entry["producerSemanticField"])
                pack_hasher.update(
                    f"{name}\0{entry['sha256']}\0{entry['sizeBytes']}\n".encode("utf-8")
                )
            self.assertEqual(pack_hasher.hexdigest(), control["screenshotPackSha256"])

            # The normalizer must have written the canonical CRC for IEND.
            final_tail = (published_dir / sorted(files)[0]).read_bytes()[-4:]
            self.assertEqual(struct.pack(">I", binascii.crc32(b"IEND") & 0xFFFFFFFF), final_tail)

            self.write_passing_receipt(root, published_dir)
            committed = self.run_transaction_program("commit", root, published_dir)
            self.assertEqual(0, committed.returncode, msg=committed.stderr)
            self.assertFalse(stage_dir.exists())
            self.assertFalse(
                (root / ".ui-flagship-screenshot-transaction.json").exists()
            )

    def test_failed_post_swap_run_restores_previous_pack_and_receipt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".ui-flagship-screenshot-stage.fixture"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            (published_dir / "old-pack-marker.txt").write_text("old\n", encoding="utf-8")
            receipt_path = root / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
            receipt_path.write_text('{"status":"old-pass"}\n', encoding="utf-8")
            previous_pack = self.snapshot(published_dir)
            previous_receipt = receipt_path.read_bytes()

            files = {"01-fixture.png": self.png_bytes(33)}
            for name, data in files.items():
                (capture_dir / name).write_bytes(data)
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(self.fixture_control(files), indent=2) + "\n",
                encoding="utf-8",
            )
            swapped = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertEqual(0, swapped.returncode, msg=swapped.stderr)
            self.assertNotEqual(previous_pack, self.snapshot(published_dir))

            recovered = self.run_transaction_program("recover", root, published_dir)
            self.assertEqual(0, recovered.returncode, msg=recovered.stderr)
            self.assertEqual(previous_pack, self.snapshot(published_dir))
            self.assertEqual(previous_receipt, receipt_path.read_bytes())
            self.assertFalse(stage_dir.exists())
            self.assertFalse(
                (root / ".ui-flagship-screenshot-transaction.json").exists()
            )

    def test_commit_revalidates_png_bytes_and_recovery_restores_old_generation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            published_dir, stage_dir, previous_pack, previous_receipt = (
                self.publish_fixture(root)
            )
            self.write_passing_receipt(root, published_dir)
            screenshot_path = published_dir / "01-fixture.png"
            screenshot_path.write_bytes(screenshot_path.read_bytes() + b"tampered")

            committed = self.run_transaction_program("commit", root, published_dir)
            self.assertNotEqual(0, committed.returncode)
            self.assertIn("changed before commit", committed.stderr)
            recovered = self.run_transaction_program("recover", root, published_dir)
            self.assertEqual(0, recovered.returncode, msg=recovered.stderr)
            self.assertEqual(previous_pack, self.snapshot(published_dir))
            self.assertEqual(
                previous_receipt,
                (root / "UI_FLAGSHIP_RELEASE_GATE.generated.json").read_bytes(),
            )
            self.assertFalse(stage_dir.exists())

    def test_durable_commit_state_recovers_forward_after_failpoint(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            published_dir, stage_dir, previous_pack, _ = self.publish_fixture(root)
            new_pack = self.snapshot(published_dir)
            self.assertNotEqual(previous_pack, new_pack)
            self.write_passing_receipt(root, published_dir)

            interrupted = self.run_transaction_program(
                "commit",
                root,
                published_dir,
                failpoint="commit_after_state",
            )
            self.assertNotEqual(0, interrupted.returncode)
            journal = json.loads(
                (root / ".ui-flagship-screenshot-transaction.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual("committing", journal["state"])
            recovered = self.run_transaction_program("recover", root, published_dir)
            self.assertEqual(0, recovered.returncode, msg=recovered.stderr)
            self.assertEqual(new_pack, self.snapshot(published_dir))
            self.assertFalse(stage_dir.exists())
            self.assertFalse(
                (root / ".ui-flagship-screenshot-transaction.json").exists()
            )

    def test_mid_cleanup_crash_never_swaps_a_partial_old_pack_back(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            published_dir, stage_dir, previous_pack, _ = self.publish_fixture(root)
            new_pack = self.snapshot(published_dir)
            self.assertNotEqual(previous_pack, new_pack)
            self.write_passing_receipt(root, published_dir)

            interrupted = self.run_transaction_program(
                "commit",
                root,
                published_dir,
                failpoint="commit_during_stage_delete",
            )
            self.assertNotEqual(0, interrupted.returncode)
            self.assertEqual(new_pack, self.snapshot(published_dir))
            recovered = self.run_transaction_program("recover", root, published_dir)
            self.assertEqual(0, recovered.returncode, msg=recovered.stderr)
            self.assertEqual(new_pack, self.snapshot(published_dir))
            self.assertFalse(stage_dir.exists())

    def test_fanout_failure_restores_existing_and_absent_targets(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            published_dir, stage_dir, previous_pack, previous_receipt = (
                self.publish_fixture(root)
            )
            existing_target = root / "existing-downstream.json"
            absent_target = root / "new-downstream.json"
            existing_target.write_text("old downstream\n", encoding="utf-8")
            prepared = self.run_transaction_program(
                "prepare-fanout",
                root,
                published_dir,
                existing_target,
                absent_target,
            )
            self.assertEqual(0, prepared.returncode, msg=prepared.stderr)
            existing_target.write_text("new downstream\n", encoding="utf-8")
            absent_target.write_text("partial downstream\n", encoding="utf-8")

            recovered = self.run_transaction_program("recover", root, published_dir)
            self.assertEqual(0, recovered.returncode, msg=recovered.stderr)
            self.assertEqual(b"old downstream\n", existing_target.read_bytes())
            self.assertFalse(absent_target.exists())
            self.assertEqual(previous_pack, self.snapshot(published_dir))
            self.assertEqual(
                previous_receipt,
                (root / "UI_FLAGSHIP_RELEASE_GATE.generated.json").read_bytes(),
            )
            self.assertFalse(stage_dir.exists())
            self.assertFalse(
                (root / ".ui-flagship-screenshot-transaction.json.fanout-backups").exists()
            )

    def test_exact_capture_authority_is_required_before_swap(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".stage"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            files = {"01-fixture.png": self.png_bytes(81)}
            (capture_dir / "01-fixture.png").write_bytes(files["01-fixture.png"])
            control = self.fixture_control(files)
            control["authority"]["captureMode"] = "generic"
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(control, indent=2) + "\n", encoding="utf-8"
            )
            before = self.snapshot(published_dir)

            completed = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("authority captureMode is invalid", completed.stderr)
            self.assertEqual(before, self.snapshot(published_dir))

    def test_missing_current_run_control_never_falls_back_to_published_control(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".stage"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            (capture_dir / "01-fixture.png").write_bytes(self.png_bytes(10))
            (published_dir / CONTROL_NAME).write_text('{"stale": true}\n', encoding="utf-8")
            (published_dir / "old.png").write_bytes(self.png_bytes(11))
            before = self.snapshot(published_dir)

            completed = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("current-run screenshot control evidence was not produced", completed.stderr)
            self.assertEqual(before, self.snapshot(published_dir))

    def test_extra_current_run_png_rejects_the_entire_pack(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".stage"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            declared_files = {"01-declared.png": self.png_bytes(12)}
            for name, data in declared_files.items():
                (capture_dir / name).write_bytes(data)
            (capture_dir / "02-undeclared.PNG").write_bytes(self.png_bytes(13))
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(self.fixture_control(declared_files), indent=2) + "\n",
                encoding="utf-8",
            )
            (published_dir / "old-pack-marker.txt").write_text("old\n", encoding="utf-8")
            before = self.snapshot(published_dir)

            completed = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("entry/PNG inventory differs", completed.stderr)
            self.assertEqual(before, self.snapshot(published_dir))

    def test_producer_contract_mismatch_is_rejected_before_publication(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".stage"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            files = {
                "01-declared.png": self.png_bytes(12),
                "02-declared.png": self.png_bytes(13),
            }
            for name, data in files.items():
                (capture_dir / name).write_bytes(data)
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(self.fixture_control(files), indent=2) + "\n",
                encoding="utf-8",
            )
            (published_dir / "old-pack-marker.txt").write_text(
                "old\n", encoding="utf-8"
            )
            before = self.snapshot(published_dir)

            completed = self.run_pack_program(
                capture_dir,
                stage_dir,
                published_dir,
                producer_screenshot_names=["01-declared.png"],
                producer_workflow_coverage=[
                    {
                        "workflowFamilyId": "fixture-family",
                        "screenshotFiles": ["01-declared.png"],
                    }
                ],
            )
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("does not exactly match the capture producer contract", completed.stderr)
            self.assertEqual(before, self.snapshot(published_dir))

    def test_structurally_empty_png_is_rejected_before_publication(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            capture_dir = root / "capture"
            stage_dir = root / ".stage"
            published_dir = root / "published"
            capture_dir.mkdir()
            stage_dir.mkdir()
            published_dir.mkdir()
            empty_png = (
                b"\x89PNG\r\n\x1a\n"
                + struct.pack(">I", 0)
                + b"IEND"
                + struct.pack(">I", binascii.crc32(b"IEND") & 0xFFFFFFFF)
            )
            files = {"01-empty.png": empty_png}
            (capture_dir / "01-empty.png").write_bytes(empty_png)
            (capture_dir / CONTROL_NAME).write_text(
                json.dumps(self.fixture_control(files), indent=2) + "\n",
                encoding="utf-8",
            )
            (published_dir / "old-pack-marker.txt").write_text(
                "old\n", encoding="utf-8"
            )
            before = self.snapshot(published_dir)

            completed = self.run_pack_program(capture_dir, stage_dir, published_dir)
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("missing IHDR", completed.stderr)
            self.assertEqual(before, self.snapshot(published_dir))

    def test_flagship_receipt_emits_exact_visual_control_contract(self) -> None:
        field_names = [
            "screenshotControlEvidencePath",
            "screenshotControlSha256",
            "screenshotControlSizeBytes",
            "screenshotControlGeneratedAt",
            "screenshotControlSchemaVersion",
            "screenshotCount",
            "screenshotPackSha256",
            "screenshotPackDigestAlgorithm",
        ]
        visual_start = self.source.index('    "visualReviewEvidence": {')
        visual_end = self.source.index('    "signoffLane": {', visual_start)
        visual_block = self.source[visual_start:visual_end]
        for field_name in field_names:
            self.assertEqual(1, visual_block.count(f'"{field_name}"'))
        self.assertIn('pack_digest_algorithm = "sha256-canonical-inventory-v1"', self.source)
        self.assertIn("screenshot_control_path.resolve(strict=True)", self.source)
        self.assertIn('atomic_write_json(receipt_path, payload)', self.source)
        self.assertIn('if blocking_findings:\n    top_level_status = "fail"', self.source)

    def test_ui_source_failures_cannot_be_waived_and_aggregate_readiness_stays_visible(self) -> None:
        self.assertNotIn("and not human_side_rule_authority_is_approved", self.source)
        self.assertIn(
            "ui_element_parity_audit_effective_status = (\n"
            "    ui_element_parity_audit_source_status\n)",
            self.source,
        )
        self.assertIn(
            "desktop_executable_exit_gate_effective_status = (\n"
            "    desktop_executable_exit_gate_status\n)",
            self.source,
        )
        self.assertIn(
            "flagship_readiness_effective_status = (\n"
            "    flagship_readiness_status\n)",
            self.source,
        )
        self.assertIn("and bool(flagship_readiness_coverage)", self.source)
        self.assertIn("and bool(flagship_readiness_open_coverage_keys)", self.source)
        top_level_start = self.source.index("top_level_status = proof_status(")
        top_level_end = self.source.index("if blocking_findings:", top_level_start)
        top_level_block = self.source[top_level_start:top_level_end]
        self.assertNotIn("desktop_executable_exit_gate_effective_status", top_level_block)
        self.assertNotIn("flagship_readiness_effective_status", top_level_block)
        self.assertIn("aggregate_readiness_observations = []", self.source)
        self.assertIn(
            "Desktop executable exit gate is not passed; this remains release-blocking ",
            self.source,
        )
        self.assertIn(
            "Flagship product readiness is not passed; this remains release-blocking ",
            self.source,
        )

    def test_refresh_and_downstream_fanout_are_explicitly_gated(self) -> None:
        self.assertIn(
            'if [[ "$refresh_supporting_receipts" == "1" ]]; then',
            self.source,
        )
        supporting_start = self.source.index(
            'if [[ "$refresh_supporting_receipts" == "1" ]]; then'
        )
        supporting_end = self.source.index(
            'echo "[b14] supporting receipt refreshes disabled;', supporting_start
        )
        supporting_block = self.source[supporting_start:supporting_end]
        for command in (
            "chummer5a-legacy-ui-element-parity-check.sh",
            "chummer4-legacy-ui-element-parity-check.sh",
            "sr5-sr6-ui-parity-audit-check.sh",
            "blazor-browser-lane-proof-set-check.sh",
            "blazor-play-surface-horizon-check.sh",
            "chummer5a-desktop-workflow-parity-check.sh",
            "sr4-sr6-desktop-parity-frontier-receipt.sh",
            "ruleset-ui-adaptation-check.sh",
            "section-host-ruleset-parity-check.sh",
            "sr6-ruleset-ui-sophistication-gate.sh",
            "chummer5a-layout-hard-gate.sh",
            "design-authorized-parity-softening-check.sh",
            "design-mirror-completeness-check.sh",
            "startup-workbench-survival-check.sh",
            "interactive-control-inventory-check.sh",
            "b15-localization-release-gate.sh",
            'python3 "$ui_parity_audit_probe_path"',
            "recursive-ui-event-exit-gate.sh",
        ):
            self.assertIn(command, supporting_block)

        augmentation_guard = (
            'if [[ "$skip_downstream_receipt_materialization" == "0" ]]; then\n'
            'python3 - <<\'PY\' "$receipt_path" "$veteran_task_time_receipt_path"'
        )
        self.assertIn(augmentation_guard, self.source)
        self.assertIn(
            'refresh_flagship_readiness="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_FLAGSHIP_READINESS:-0}"',
            self.source,
        )
        self.assertIn(
            'skip_flagship_readiness_refresh="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_FLAGSHIP_READINESS_REFRESH:-0}"',
            self.source,
        )
        readiness_call = 'python3 "$flagship_product_readiness_materializer_path" >/dev/null'
        self.assertEqual(1, self.source.count(readiness_call))
        readiness_call_index = self.source.index(readiness_call)
        readiness_guard = self.source.rfind(
            'if [[ "$skip_downstream_receipt_materialization" == "0"',
            0,
            readiness_call_index,
        )
        self.assertGreaterEqual(readiness_guard, 0)
        self.assertLess(readiness_guard, readiness_call_index)

    def test_current_builds_are_mandatory_and_restore_is_never_implicit(self) -> None:
        self.assertNotIn("CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REUSE_EXISTING_BUILD_OUTPUT", self.source)
        self.assertNotIn("CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REUSE_EXISTING_TEST_BUILD", self.source)
        self.assertIn(
            "building the current Avalonia desktop head without restore",
            self.source,
        )
        self.assertIn(
            "building the current flagship test assembly without restore",
            self.source,
        )
        self.assertEqual(
            1,
            self.source.count(
                "bash scripts/ai/build.sh Chummer.Avalonia/Chummer.Avalonia.csproj"
            ),
        )
        self.assertNotIn("restore-enabled build", self.source)

    def test_failed_base_or_downstream_run_cannot_leave_new_receipt_committed(self) -> None:
        fail_check = self.source.index('if top_level_status != "pass":')
        base_write = self.source.index("atomic_write_json(receipt_path, payload)", fail_check)
        downstream = self.source.index(
            'if [[ "$skip_downstream_receipt_materialization" == "0" ]]; then',
            base_write,
        )
        commit = self.source.rindex("manage_screenshot_pack_transaction commit")
        final_pass = self.source.rindex('echo "[b14] PASS"')
        self.assertLess(fail_check, base_write)
        self.assertLess(base_write, downstream)
        self.assertLess(downstream, commit)
        self.assertLess(commit, final_pass)
        self.assertIn("previousReceiptSha256", self.source)
        self.assertIn("cannot restore the proven previous flagship receipt", self.source)
        self.assertIn("downstreamReceiptProofs", self.source)
        self.assertIn("receipt predates this flagship run", self.source)

    def test_status_bearing_inputs_require_fresh_regular_passing_receipts(self) -> None:
        required_labels = [
            "explicit Chummer5a desktop workflow parity proof",
            "explicit SR4 desktop workflow parity proof",
            "explicit SR6 desktop workflow parity proof",
            "Chummer5a UI element parity audit",
            "public-edge workbench proof",
            "Blazor browser-lane proof set",
            "Blazor play-surface horizon proof",
        ]
        for label in required_labels:
            self.assertIn(json.dumps(label), self.source)
        self.assertIn("receipt_path.is_symlink()", self.source)
        self.assertIn("receipt generatedAt/generated_at must include a UTC offset", self.source)
        self.assertIn("UI element parity audit rows are missing", self.source)
        self.assertIn("dense-builder route-local evidence", self.source)
        self.assertIn(
            "desktop_executable_exit_gate_receipt = load_json_if_present(",
            self.source,
        )
        self.assertIn(
            "flagship_product_readiness_receipt = load_json_if_present(",
            self.source,
        )

    def test_downstream_receipts_are_not_precommit_prerequisites(self) -> None:
        required_start = self.source.index(
            "required_dense_builder_route_local_evidence_suffixes = ["
        )
        required_end = self.source.index(
            "downstream_dense_builder_route_local_evidence_suffixes = [",
            required_start,
        )
        required_block = self.source[required_start:required_end]
        downstream_end = self.source.index(
            "\ndense_builder_contracts = {",
            required_end,
        )
        downstream_block = self.source[required_end:downstream_end]
        for suffix in (
            "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
            "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json",
            "UI_LOCAL_RELEASE_PROOF.generated.json",
            "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
            "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json",
        ):
            self.assertNotIn(suffix, required_block)
            self.assertIn(suffix, downstream_block)
        for suffix in (
            "SECTION_HOST_RULESET_PARITY.generated.json",
            "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json",
            "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json",
            "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json",
            "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json",
            "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json",
        ):
            self.assertIn(suffix, required_block)

    def test_generic_pass_receipt_cannot_launder_a_named_contract(self) -> None:
        function_start = self.source.index("def require_current_passing_receipt(")
        function_end = self.source.index("\n\ndef atomic_write_json", function_start)
        namespace = {
            "Path": Path,
            "json": json,
            "os": os,
            "datetime": datetime,
            "timezone": timezone,
            "release_channel_channel_id": "preview",
            "release_channel_version": "fixture-v1",
        }
        exec(self.source[function_start:function_end], namespace)
        require_receipt = namespace["require_current_passing_receipt"]
        with tempfile.TemporaryDirectory() as temporary_directory:
            receipt_path = Path(temporary_directory) / "receipt.json"
            base = {
                "status": "pass",
                "generatedAt": datetime.now(timezone.utc).isoformat(),
            }
            receipt_path.write_text(json.dumps(base), encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "contract identity"):
                require_receipt(
                    str(receipt_path),
                    "workflow",
                    "chummer6-ui.chummer5a_desktop_workflow_parity",
                    require_channel=True,
                )

            base.update(
                {
                    "contract_name": "chummer6-ui.chummer5a_desktop_workflow_parity",
                    "channelId": "other",
                }
            )
            receipt_path.write_text(json.dumps(base), encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "release channel"):
                require_receipt(
                    str(receipt_path),
                    "workflow",
                    "chummer6-ui.chummer5a_desktop_workflow_parity",
                    require_channel=True,
                )

    def test_release_channel_selection_is_deterministic(self) -> None:
        start = self.source.index('explicit_release_channel_path="${CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_PATH:-')
        end = self.source.index('refresh_supporting_receipts=', start)
        selection = self.source[start:end]
        self.assertNotIn(" -nt ", self.source)
        ordered_assignments = [
            'release_channel_path_default="$explicit_release_channel_path"',
            'release_channel_path_default="$canonical_release_channel_path"',
            'release_channel_path_default="$verified_release_channel_path"',
            'release_channel_path_default="$run_services_release_channel_path"',
            'release_channel_path_default="$default_release_channel_path"',
        ]
        offsets = [selection.index(value) for value in ordered_assignments]
        self.assertEqual(sorted(offsets), offsets)


if __name__ == "__main__":
    unittest.main()
