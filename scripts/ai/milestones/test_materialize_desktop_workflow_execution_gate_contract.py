#!/usr/bin/env python3
from __future__ import annotations

import ast
import hashlib
import importlib.util
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace
from typing import Any, Dict
from unittest import mock


SCRIPT_PATH = Path(__file__).with_name(
    "materialize-desktop-workflow-execution-gate.sh"
)


class DesktopWorkflowExecutionGateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = SCRIPT_PATH.read_text(encoding="utf-8")

    @staticmethod
    def file_hash(path: Path) -> str:
        if not path.is_file():
            return "missing"
        return hashlib.sha256(path.read_bytes()).hexdigest()

    @staticmethod
    def load_workflow_contract_module() -> Any:
        contract_path = SCRIPT_PATH.with_name("workflow_family_trx_contract.py")
        spec = importlib.util.spec_from_file_location(
            "workflow_family_contract_under_test", contract_path
        )
        if spec is None or spec.loader is None:
            raise AssertionError("workflow-family contract module could not be loaded")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    @staticmethod
    def execution_epoch_fixture(
        reference_time: datetime,
    ) -> tuple[dict[str, Any], str, dict[str, Any], dict[str, Any]]:
        candidate_snapshot_id = "a" * 64
        release_identity = {
            "path": "/trusted/RELEASE_CHANNEL.generated.json",
            "sha256": "f" * 64,
            "sizeBytes": 123,
            "channelId": "stable",
            "releaseVersion": "6.0.0",
            "generatedAt": (reference_time - timedelta(hours=3))
            .isoformat()
            .replace("+00:00", "Z"),
        }
        stage_manifests: dict[str, dict[str, dict[str, Any]]] = {}
        stage_bindings: dict[str, dict[str, dict[str, Any]]] = {}
        bounds = {
            "sr4": (
                reference_time - timedelta(hours=4),
                reference_time - timedelta(hours=3),
            ),
            "sr6": (
                reference_time - timedelta(hours=2, minutes=45),
                reference_time - timedelta(hours=2),
            ),
        }
        for edition_index, edition in enumerate(("sr4", "sr6"), start=1):
            started_at, completed_at = bounds[edition]
            producer_run_id = (
                "11111111-1111-4111-8111-111111111111"
                if edition == "sr4"
                else "22222222-2222-4222-8222-222222222222"
            )
            execution_run_digest = ("b" if edition == "sr4" else "c") * 64
            candidate_digest = ("d" if edition == "sr4" else "e") * 64
            stage_manifests[edition] = {}
            stage_bindings[edition] = {}
            for stage_index, stage in enumerate(
                ("execution", "verification", "parity"), start=1
            ):
                generated_at = completed_at + timedelta(minutes=stage_index)
                stage_manifests[edition][stage] = {
                    "edition": edition,
                    "stage": stage,
                    "status": "pass",
                    "generatedAt": generated_at.isoformat().replace("+00:00", "Z"),
                    "producerRunId": producer_run_id,
                    "candidateSnapshotId": candidate_snapshot_id,
                    "workflowEpochId": candidate_snapshot_id,
                    "executionRunDigest": execution_run_digest,
                    "executionStartedAt": started_at.isoformat().replace("+00:00", "Z"),
                    "executionCompletedAt": completed_at.isoformat().replace("+00:00", "Z"),
                    "candidateDigest": candidate_digest,
                    "releaseIdentity": release_identity,
                    "epochCommitId": format(edition_index * 16 + stage_index, "064x"),
                }
                stage_bindings[edition][stage] = {
                    "path": f"/trusted/{edition}/{stage}.generated.json",
                    "sha256": format(edition_index * 32 + stage_index, "064x"),
                    "sizeBytes": 1000 + stage_index,
                }
        return (
            release_identity,
            candidate_snapshot_id,
            stage_manifests,
            stage_bindings,
        )

    def test_isolated_failure_writes_only_temp_receipt(self) -> None:
        repo_root = SCRIPT_PATH.parents[3]
        canonical_workflow_receipt = (
            repo_root
            / ".codex-studio"
            / "published"
            / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
        )
        fleet_readiness_receipt = Path(
            "/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json"
        )
        canonical_before = self.file_hash(canonical_workflow_receipt)
        fleet_before = self.file_hash(fleet_readiness_receipt)

        with tempfile.TemporaryDirectory() as temporary_directory:
            temporary_root = Path(temporary_directory)
            output_path = temporary_root / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
            release_channel_path = temporary_root / "RELEASE_CHANNEL.generated.json"
            release_channel_path.write_text(
                json.dumps(
                    {
                        "channelId": "isolated-contract-test",
                        "version": "0-test",
                        "generatedAt": datetime.now(timezone.utc)
                        .isoformat()
                        .replace("+00:00", "Z"),
                    }
                )
                + "\n",
                encoding="utf-8",
            )
            environment = os.environ.copy()
            environment.update(
                {
                    "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_RECEIPT_PATH": str(output_path),
                    "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH": str(
                        release_channel_path
                    ),
                    "CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_RECEIPTS": "0",
                    "CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_DEPENDENCY_REFRESH": "1",
                    "CHUMMER_DESKTOP_WORKFLOW_REFRESH_FLAGSHIP_READINESS": "0",
                    "CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_READINESS_REFRESH": "1",
                    "CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH": "/dev/null",
                }
            )
            completed = subprocess.run(
                ["bash", str(SCRIPT_PATH)],
                cwd=repo_root,
                env=environment,
                capture_output=True,
                text=True,
                timeout=60,
                check=False,
            )

            self.assertEqual(
                43,
                completed.returncode,
                msg=f"stdout={completed.stdout}\nstderr={completed.stderr}",
            )
            self.assertTrue(output_path.is_file())
            payload = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("fail", payload.get("status"))

        self.assertEqual(canonical_before, self.file_hash(canonical_workflow_receipt))
        self.assertEqual(fleet_before, self.file_hash(fleet_readiness_receipt))

    def test_receipt_and_downstream_writes_can_be_isolated(self) -> None:
        self.assertIn(
            'receipt_path="${CHUMMER_DESKTOP_WORKFLOW_EXECUTION_RECEIPT_PATH:-',
            self.source,
        )
        self.assertIn(
            'skip_flagship_readiness_refresh="${CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_READINESS_REFRESH:-0}"',
            self.source,
        )
        self.assertIn(
            'refresh_flagship_readiness="${CHUMMER_DESKTOP_WORKFLOW_REFRESH_FLAGSHIP_READINESS:-0}"',
            self.source,
        )
        self.assertIn(
            'if [[ "$refresh_flagship_readiness" != "1" ]]; then',
            self.source,
        )
        self.assertIn(
            '"CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH=/dev/null"',
            self.source,
        )
        self.assertIn(
            'if [[ "$refresh_flagship_readiness" == "1" ]]; then',
            self.source,
        )
        self.assertIn(
            'python3 "$flagship_product_readiness_materializer_path" >/dev/null',
            self.source,
        )

    def test_release_channel_selection_is_deterministic(self) -> None:
        selection_start = self.source.index(
            'if [[ -n "$canonical_release_channel_path"'
        )
        selection_end = self.source.index(
            'release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-',
            selection_start,
        )
        selection = self.source[selection_start:selection_end]
        self.assertNotIn(" -nt ", selection)
        self.assertLess(
            selection.index('release_channel_path_default="$canonical_release_channel_path"'),
            selection.index('release_channel_path_default="$verified_release_channel_path"'),
        )
        self.assertLess(
            selection.index('release_channel_path_default="$verified_release_channel_path"'),
            selection.index('release_channel_path_default="$run_services_release_channel_path"'),
        )
        self.assertLess(
            selection.index('release_channel_path_default="$run_services_release_channel_path"'),
            selection.index('release_channel_path_default="$default_release_channel_path"'),
        )

    def test_dependency_refresh_is_explicit_opt_in_without_timestamp_laundering(self) -> None:
        refresh_default = re.search(
            r'if \[\[ -n "\$refresh_dependency_receipts_override" \]\]; then\n'
            r'  refresh_dependency_receipts="\$refresh_dependency_receipts_override"\n'
            r"else\n"
            r'  refresh_dependency_receipts="(?P<default>[01])"\n'
            r"fi",
            self.source,
        )
        self.assertIsNotNone(refresh_default)
        self.assertEqual("0", refresh_default.group("default"))
        self.assertIn(
            'if [[ "$refresh_dependency_receipts" == "1" ]]; then',
            self.source,
        )
        self.assertNotIn("refresh_receipt_generated_at_if_unchanged", self.source)
        self.assertNotIn("dependencyRefreshGeneratedAt", self.source)
        self.assertNotIn(
            "receipt_is_external_only_missing_api_surface_contract() {",
            self.source,
        )
        self.assertIn("record_dependency_refresh_attempt", self.source)
        self.assertIn('"$before_generated_at"', self.source)
        self.assertIn('"$after_generated_at"', self.source)

    def test_stale_proof_inputs_are_top_level_failures_but_publication_age_is_diagnostic(self) -> None:
        self.assertNotIn("allow_stale_pass_receipt=True", self.source)
        self.assertNotIn(
            'and not reason.startswith("ui_flagship_release_gate receipt is stale ")',
            self.source,
        )
        self.assertNotIn('"sr4_sr6_frontier receipt is stale",', self.source)
        self.assertNotIn(
            "if release_channel_age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:",
            self.source,
        )
        self.assertNotIn(
            '"Desktop workflow execution gate release channel receipt is stale "',
            self.source,
        )
        self.assertIn(
            'upstream_receipt_review_reasons.append(f"{label}:stale")',
            self.source,
        )

    def test_naive_stale_and_future_family_timestamps_are_rejected(self) -> None:
        parse_start = self.source.index("def parse_iso(")
        parse_end = self.source.index("\n\ndef payload_generated_at", parse_start)
        timestamp_start = self.source.index("def validate_family_timestamp(")
        timestamp_end = self.source.index("\n\ndef validate_current_binding", timestamp_start)
        namespace = {
            "Any": Any,
            "Dict": Dict,
            "datetime": datetime,
            "timezone": timezone,
            "DESKTOP_PROOF_MAX_AGE_SECONDS": 60,
            "DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS": 5,
        }
        exec(self.source[parse_start:parse_end], namespace)
        exec(self.source[timestamp_start:timestamp_end], namespace)
        validate = namespace["validate_family_timestamp"]

        invalid_timestamps = (
            datetime.now().replace(microsecond=0).isoformat(),
            (datetime.now(timezone.utc) - timedelta(seconds=61)).isoformat(),
            (datetime.now(timezone.utc) + timedelta(seconds=6)).isoformat(),
        )
        for generated_at in invalid_timestamps:
            with self.subTest(generated_at=generated_at):
                with self.assertRaises(ValueError):
                    validate({"generatedAt": generated_at}, "fixture")

    def test_regular_file_snapshot_rejects_symlinks_and_changed_bindings(self) -> None:
        read_start = self.source.index("def read_regular_bytes(")
        read_end = self.source.index("\n\ndef load_regular_json", read_start)
        binding_start = self.source.index("def binding_for_bytes(")
        binding_end = self.source.index("\n\ndef status_ok", binding_start)
        validate_start = self.source.index("def validate_current_binding(")
        validate_end = self.source.index("\n\ndef validate_upstream_family_receipt", validate_start)
        namespace = {
            "Any": Any,
            "Dict": Dict,
            "Path": Path,
            "hashlib": hashlib,
            "os": os,
            "stat": stat,
            "MAX_REGULAR_INPUT_BYTES": 64 * 1024 * 1024,
        }
        exec(self.source[read_start:read_end], namespace)
        exec(self.source[binding_start:binding_end], namespace)
        exec(self.source[validate_start:validate_end], namespace)

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            receipt = root / "receipt.json"
            receipt.write_bytes(b"one")
            link = root / "copied.json"
            link.symlink_to(receipt)
            with self.assertRaises(ValueError):
                namespace["read_regular_bytes"](link, "fixture")

            binding = namespace["file_binding"](receipt, "fixture")
            receipt.write_bytes(b"two")
            with self.assertRaises(ValueError):
                namespace["validate_current_binding"](binding, receipt, "fixture")

    def test_family_chain_requires_exact_schema_identity_and_byte_bindings(self) -> None:
        required_contract_tokens = (
            'payload.get("schemaVersion")',
            'payload.get("producerRunId")',
            'payload_evidence.get("releaseIdentity") != release_identity',
            'payload_evidence.get("candidateIdentity") != candidate_identities.get(edition)',
            'payload_evidence.get("candidateDigest")',
            'payload_evidence.get("upstreamExecutionBindings")',
            'payload_evidence.get("upstreamVerificationBindings")',
            'status must equal pass',
            'execution outcome is not exactly Passed',
            'substring-only tests',
        )
        for token in required_contract_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.source)
        self.assertIn("family_receipt_run_ids", self.source)
        self.assertIn("family_receipt_candidate_digests", self.source)
        self.assertIn("binding path is missing or misplaced", self.source)

    def test_aggregate_receipt_publishes_versioned_release_identity(self) -> None:
        for token in (
            '"schemaVersion": 1',
            '"producerRunId": producer_run_id',
            '"channelId": release_channel_channel_id',
            '"channel": release_channel_channel_id',
            '"releaseVersion": release_channel_version',
            '"version": release_channel_version',
        ):
            with self.subTest(token=token):
                self.assertIn(token, self.source)

    def test_family_materializers_bind_one_fresh_run_without_partial_publication(self) -> None:
        scripts_root = SCRIPT_PATH.parent
        execution_source = (
            scripts_root / "materialize-sr-workflow-family-execution-receipts.sh"
        ).read_text(encoding="utf-8")
        verification_source = (
            scripts_root / "materialize-sr-workflow-family-verification-receipts.sh"
        ).read_text(encoding="utf-8")
        parity_source = (
            scripts_root / "materialize-sr-workflow-family-receipts.sh"
        ).read_text(encoding="utf-8")
        trx_contract_source = (
            scripts_root / "workflow_family_trx_contract.py"
        ).read_text(encoding="utf-8")

        self.assertIn("family_filter_ids != CANONICAL_FAMILY_IDS", execution_source)
        self.assertIn("producer_run_id = str(uuid.uuid4())", execution_source)
        self.assertIn('/ producer_run_id', execution_source)
        self.assertIn('outcomes != ["Passed"]', execution_source)
        self.assertNotIn("if test_name in observed_name", execution_source)
        self.assertIn('"--no-incremental"', execution_source)
        self.assertNotIn('run_command.append("--no-build")', execution_source)
        self.assertIn(
            "test_runner_command = [str(dotnet_host_path), str(test_runner_dll)]",
            execution_source,
        )
        self.assertIn("max_test_attempts = 1", execution_source)
        self.assertNotIn("CHUMMER_WORKFLOW_FAMILY_MAX_TEST_ATTEMPTS", execution_source)
        self.assertIn('record.get("attemptCount") != 1', execution_source)
        self.assertIn("validate_trx_contract(", execution_source)
        self.assertIn('if record.get("summaryValid") is True:', execution_source)
        self.assertIn('"trxValidationError": str(exc)', execution_source)
        self.assertIn(
            '"perTestTrxPaths": {\n'
            "                    test_name: per_test_trx_paths[test_name]\n"
            "                    for test_name in audit_tests",
            execution_source,
        )
        self.assertNotIn('"perTestTrxPaths": per_test_trx_paths', execution_source)
        self.assertIn('normalized_counters["total"] != 1', trx_contract_source)
        self.assertIn('normalized_counters["executed"] != 1', trx_contract_source)
        self.assertIn('normalized_counters["passed"] != 1', trx_contract_source)
        self.assertNotIn("CHUMMER_WORKFLOW_FAMILY_ALLOW_REMOTE_API", execution_source)
        self.assertNotIn("docker run", execution_source)
        self.assertIn('"PATH": "/usr/bin:/bin"', execution_source)
        self.assertIn("class NoRedirectHandler", execution_source)
        self.assertIn("atexit.register(terminate_local_api)", execution_source)

        for source, upstream_token in (
            (verification_source, '"upstreamExecutionBindings"'),
            (parity_source, '"upstreamVerificationBindings"'),
        ):
            with self.subTest(upstream_token=upstream_token):
                self.assertIn('"schemaVersion": SCHEMA_VERSION', source)
                self.assertIn('"producerRunId": producer_run_id', source)
                self.assertIn('"candidateDigest": candidate_digest', source)
                self.assertIn(upstream_token, source)
                self.assertIn("parse_strict_timestamp", source)
                self.assertIn("O_NOFOLLOW", source)
                self.assertIn("os.replace(temporary_path, path)", source)
                self.assertIn('record.get("attemptCount") != 1', source)
                self.assertNotIn("output_path.write_text(", source)

        for source in (execution_source, verification_source, parity_source):
            with self.subTest(release_contract_source=hashlib.sha256(source.encode()).hexdigest()):
                self.assertIn("Chummer.Hub.Registry.Contracts", source)
                self.assertIn("status", source)
                self.assertIn("published", source)
                self.assertIn("MAX_REGULAR_INPUT_BYTES", source)
                self.assertIn('"Chummer.Api": repo_root / "Chummer.Api', source)

        self.assertIn("api_base_url = CANONICAL_API_BASE_URL", execution_source)
        self.assertNotIn('os.environ.get("CHUMMER_API_BASE_URL")', execution_source)
        self.assertIn('initial_probe["untrustedPreexistingService"] = True', execution_source)
        self.assertIn('"--configuration",\n        "Release"', execution_source)
        self.assertIn("validate_api_probe_contract(api_probe", execution_source)
        self.assertIn("validate_api_probe_contract(", verification_source)
        self.assertIn("validate_api_probe_contract(", parity_source)
        self.assertIn("validate_api_probe_contract(", self.source)

    def test_sr4_sr6_wrappers_block_every_child_exit_and_alias_conflict(self) -> None:
        scripts_root = SCRIPT_PATH.parent
        for edition in ("sr4", "sr6"):
            source = (
                scripts_root / f"{edition}-desktop-workflow-parity-check.sh"
            ).read_text(encoding="utf-8")
            with self.subTest(edition=edition):
                self.assertNotIn(" -nt ", source)
                self.assertIn("parsed.tzinfo is None or parsed.utcoffset() is None", source)
                self.assertIn("RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS", source)
                self.assertIn("conflicting channelId/channel aliases", source)
                self.assertIn("conflicting releaseVersion/version aliases", source)
                self.assertIn("conflicting generatedAt/generated_at aliases", source)
                self.assertIn("Chummer.Hub.Registry.Contracts", source)
                self.assertIn("status must be published", source)
                self.assertIn("if execution_exit != 0:", source)
                self.assertIn("if verification_exit != 0:", source)
                self.assertIn("if materializer_exit != 0:", source)
                self.assertIn("os.replace(temporary_path, path)", source)
                self.assertNotIn("receipt_path.write_text(", source)
                self.assertIn(
                    "CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/"
                    "chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json",
                    source,
                )
                self.assertEqual(
                    3,
                    source.count(
                        'CHUMMER_WORKFLOW_FAMILY_RELEASE_CHANNEL_PATH="$release_channel_path" bash '
                    ),
                )

    def test_sr4_sr6_wrappers_snapshot_every_provenance_input_fail_closed(self) -> None:
        scripts_root = SCRIPT_PATH.parent
        for edition in ("sr4", "sr6"):
            source = (
                scripts_root / f"{edition}-desktop-workflow-parity-check.sh"
            ).read_text(encoding="utf-8")
            with self.subTest(edition=edition):
                for token in (
                    "JSON_INPUT_MAX_BYTES",
                    "TEXT_INPUT_MAX_BYTES",
                    "def snapshot_signature(",
                    "def read_regular_bytes(",
                    "def decode_regular_text(",
                    "def parse_regular_json(",
                    "def load_regular_json(",
                    "def load_regular_text(",
                    'getattr(os, "O_NOFOLLOW", 0)',
                    "os.lstat(path)",
                    "before = os.fstat(descriptor)",
                    "after = os.fstat(descriptor)",
                    "metadata.st_mtime_ns",
                    "path binding changed",
                    "contains invalid JSON at line",
                    "is not valid UTF-8 text at byte",
                    "snapshot limit",
                ):
                    self.assertIn(token, source)
                for unsafe_read in (
                    "release_channel_path.read_text(",
                    "ledger_path.read_text(",
                    "receipt_file.read_text(",
                    'path.read_text(encoding="utf-8")',
                ):
                    self.assertNotIn(unsafe_read, source)
                if edition == "sr4":
                    self.assertNotIn("oracle_path.read_text(", source)
                else:
                    self.assertNotIn("sr4_receipt_path.read_text(", source)

                helper_start = source.index("def snapshot_signature(")
                helper_end = source.index(
                    "\n\ndef write_receipt_atomically", helper_start
                )
                recorded_reasons: list[str] = []

                def append_reason(message: str, *buckets: list[str]) -> None:
                    recorded_reasons.append(message)
                    for bucket in buckets:
                        bucket.append(message)

                namespace = {
                    "Path": Path,
                    "SimpleNamespace": SimpleNamespace,
                    "append_reason": append_reason,
                    "json": json,
                    "os": os,
                    "stat": stat,
                    "JSON_INPUT_MAX_BYTES": 32,
                    "TEXT_INPUT_MAX_BYTES": 32,
                }
                exec(source[helper_start:helper_end], namespace)

                with tempfile.TemporaryDirectory() as temporary_directory:
                    root = Path(temporary_directory)
                    regular = root / "regular.json"
                    regular.write_text('{"status":"pass"}', encoding="utf-8")
                    linked = root / "linked.json"
                    linked.symlink_to(regular)
                    oversized = root / "oversized.json"
                    oversized.write_bytes(b"x" * 33)
                    malformed = root / "malformed.json"
                    malformed.write_bytes(b"{not-json")
                    non_unicode = root / "non-unicode.txt"
                    non_unicode.write_bytes(b"\xff")

                    self.assertEqual(
                        b'{"status":"pass"}',
                        namespace["read_regular_bytes"](
                            regular, "regular fixture", 32
                        ),
                    )
                    for invalid_path in (linked, root):
                        with self.assertRaisesRegex(ValueError, "not a regular file"):
                            namespace["read_regular_bytes"](
                                invalid_path, "invalid fixture", 32
                            )
                    with self.assertRaisesRegex(ValueError, "snapshot limit"):
                        namespace["read_regular_bytes"](
                            oversized, "oversized fixture", 32
                        )

                    json_reasons: list[str] = []
                    payload, loaded = namespace["load_regular_json"](
                        malformed, "malformed fixture", json_reasons
                    )
                    self.assertFalse(loaded)
                    self.assertEqual({}, payload)
                    self.assertTrue(
                        any("contains invalid JSON" in reason for reason in json_reasons)
                    )

                    text_reasons: list[str] = []
                    text, loaded = namespace["load_regular_text"](
                        non_unicode, "non-unicode fixture", text_reasons
                    )
                    self.assertFalse(loaded)
                    self.assertEqual("", text)
                    self.assertTrue(
                        any("not valid UTF-8 text" in reason for reason in text_reasons)
                    )

                    observed = regular.stat()
                    changed = SimpleNamespace(
                        st_dev=observed.st_dev,
                        st_ino=observed.st_ino,
                        st_mode=observed.st_mode,
                        st_size=observed.st_size,
                        st_mtime_ns=observed.st_mtime_ns + 1,
                    )
                    with mock.patch.object(
                        os, "fstat", side_effect=(observed, changed)
                    ):
                        with self.assertRaisesRegex(
                            ValueError, "changed while being snapshotted"
                        ):
                            namespace["read_regular_bytes"](
                                regular, "changing fixture", 32
                            )

    def test_workflow_parity_runner_requires_complete_trx_without_restore(self) -> None:
        runner_path = SCRIPT_PATH.with_name("run-workflow-parity-gate-tests.sh")
        runner_source = runner_path.read_text(encoding="utf-8")
        for token in (
            'configuration="Release"',
            'framework="net10.0"',
            "--no-restore",
            "--no-incremental",
            "--report-trx",
            'summary.attrib.get("outcome") != "Completed"',
            "does not contain each canonical test exactly once",
            "O_NOFOLLOW",
        ):
            with self.subTest(token=token):
                self.assertIn(token, runner_source)
        self.assertNotIn("CHUMMER_WORKFLOW_PARITY_GATE_SKIP_RESTORE", runner_source)
        self.assertNotIn("CHUMMER_WORKFLOW_PARITY_GATE_SKIP_BUILD", runner_source)

        marker = "python3 - <<'PY' \"$trx_path\"\n"
        validator_start = runner_source.index(marker) + len(marker)
        validator_end = runner_source.index("\nPY\n", validator_start)
        validator = runner_source[validator_start:validator_end]
        ast.parse(validator)
        expected_tests = (
            "Menu_dialog_workflows_are_exhaustively_classified",
            "Legacy_ui_controls_are_exhaustively_classified",
            "Quick_action_roots_are_exhaustively_classified",
            "Menu_dialog_workflows_keep_recursive_parity",
            "Legacy_ui_controls_keep_recursive_parity",
        )

        def trx_text(test_names: tuple[str, ...], *, total: int | None = None) -> str:
            definition_rows = "".join(
                "<UnitTest "
                f'id="test-{index}" name="{name}">'
                "<TestMethod "
                'className="Chummer.Tests.Presentation.WorkflowParityGateTests" '
                f'name="{name}" />'
                "</UnitTest>"
                for index, name in enumerate(test_names, start=1)
            )
            result_rows = "".join(
                "<UnitTestResult "
                f'testId="test-{index}" testName="{name}" outcome="Passed" />'
                for index, name in enumerate(test_names, start=1)
            )
            count = len(test_names) if total is None else total
            return (
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f"<TestDefinitions>{definition_rows}</TestDefinitions>"
                f"<Results>{result_rows}</Results>"
                '<ResultSummary outcome="Completed">'
                f'<Counters total="{count}" executed="{count}" passed="{count}" '
                'failed="0" error="0" timeout="0" aborted="0" inconclusive="0" '
                'notExecuted="0" notRunnable="0" disconnected="0" warning="0" />'
                "</ResultSummary></TestRun>"
            )

        with tempfile.TemporaryDirectory() as temporary_directory:
            trx_path = Path(temporary_directory) / "workflow.trx"
            trx_path.write_text(trx_text(expected_tests), encoding="utf-8")
            passing = subprocess.run(
                [sys.executable, "-c", validator, str(trx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(0, passing.returncode, msg=passing.stderr)

            trx_path.write_text(trx_text(expected_tests[:-1]), encoding="utf-8")
            missing = subprocess.run(
                [sys.executable, "-c", validator, str(trx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, missing.returncode)
            self.assertIn(
                "does not contain each canonical test exactly once", missing.stderr
            )

            trx_path.write_text(trx_text(expected_tests, total=6), encoding="utf-8")
            mismatched_summary = subprocess.run(
                [sys.executable, "-c", validator, str(trx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, mismatched_summary.returncode)
            self.assertIn("completed-run summary is invalid", mismatched_summary.stderr)

            valid_trx = trx_text(expected_tests)
            empty_id_trx = valid_trx.replace('id="test-1"', 'id=""', 1).replace(
                'testId="test-1"', 'testId=""', 1
            )
            trx_path.write_text(empty_id_trx, encoding="utf-8")
            empty_id = subprocess.run(
                [sys.executable, "-c", validator, str(trx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, empty_id.returncode)
            self.assertIn("unique nonblank test IDs", empty_id.stderr)

            extra_summary_trx = valid_trx.replace(
                "</TestRun>",
                '<ResultSummary outcome="Failed"><Counters total="0" /></ResultSummary>'
                "</TestRun>",
            )
            trx_path.write_text(extra_summary_trx, encoding="utf-8")
            extra_summary = subprocess.run(
                [sys.executable, "-c", validator, str(trx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, extra_summary.returncode)
            self.assertIn("exactly one run summary", extra_summary.stderr)

    def test_shared_trx_contract_rejects_forged_or_incomplete_results(self) -> None:
        contract_path = SCRIPT_PATH.with_name("workflow_family_trx_contract.py")
        spec = importlib.util.spec_from_file_location(
            "workflow_family_trx_contract_under_test", contract_path
        )
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        test_name = "Menu_dialog_workflows_are_exhaustively_classified"
        class_name = "Chummer.Tests.Presentation.WorkflowParityGateTests"
        attempt_started_at = "2026-07-27T12:00:00Z"
        attempt_completed_at = "2026-07-27T12:00:05Z"
        trx_run_id = "11111111-1111-1111-1111-111111111111"
        test_id = "22222222-2222-2222-2222-222222222222"
        execution_id = "33333333-3333-3333-3333-333333333333"

        def trx_bytes(
            *,
            method_class: str = class_name,
            include_warning_counter: bool = True,
        ) -> bytes:
            warning_counter = ' warning="0"' if include_warning_counter else ""
            return (
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" '
                f'id="{trx_run_id}">'
                '<Times start="2026-07-27T12:00:01Z" '
                'finish="2026-07-27T12:00:04Z" />'
                f'<TestDefinitions><UnitTest id="{test_id}">'
                f'<Execution id="{execution_id}" />'
                f'<TestMethod className="{method_class}" name="{test_name}" />'
                "</UnitTest></TestDefinitions>"
                f'<TestEntries><TestEntry testId="{test_id}" '
                f'executionId="{execution_id}" /></TestEntries>'
                "<Results>"
                f'<UnitTestResult testId="{test_id}" executionId="{execution_id}" '
                f'testName="{test_name}" outcome="Passed" '
                'startTime="2026-07-27T12:00:02Z" '
                'endTime="2026-07-27T12:00:03Z" />'
                "</Results>"
                '<ResultSummary outcome="Completed"><Counters '
                'total="1" executed="1" passed="1" failed="0" error="0" '
                'timeout="0" aborted="0" inconclusive="0" notExecuted="0" '
                f'notRunnable="0" disconnected="0"{warning_counter} />'
                "</ResultSummary></TestRun>"
            ).encode("utf-8")

        with tempfile.TemporaryDirectory() as temporary_directory:
            run_root = Path(temporary_directory)
            trx_path = run_root / "result.trx"

            trx_path.write_bytes(trx_bytes())
            binding = module.file_binding(trx_path, "valid TRX fixture")
            validated = module.validate_trx_contract(
                trx_path,
                test_name,
                binding,
                run_root,
                attempt_started_at,
                attempt_completed_at,
            )
            self.assertEqual(test_name, validated["testName"])
            self.assertEqual("Passed", validated["outcome"])

            trx_path.write_bytes(b"this is not XML")
            binding = module.file_binding(trx_path, "malformed TRX fixture")
            with self.assertRaisesRegex(ValueError, "TRX is malformed"):
                module.validate_trx_contract(
                    trx_path,
                    test_name,
                    binding,
                    run_root,
                    attempt_started_at,
                    attempt_completed_at,
                )

            trx_path.write_bytes(trx_bytes(method_class="Substituted.TestClass"))
            binding = module.file_binding(trx_path, "substituted TRX fixture")
            with self.assertRaisesRegex(ValueError, "TestMethod identity mismatch"):
                module.validate_trx_contract(
                    trx_path,
                    test_name,
                    binding,
                    run_root,
                    attempt_started_at,
                    attempt_completed_at,
                )

            trx_path.write_bytes(trx_bytes(include_warning_counter=False))
            binding = module.file_binding(trx_path, "incomplete TRX fixture")
            with self.assertRaisesRegex(ValueError, "counters are incomplete"):
                module.validate_trx_contract(
                    trx_path,
                    test_name,
                    binding,
                    run_root,
                    attempt_started_at,
                    attempt_completed_at,
                )

    def test_api_runtime_contract_requires_owned_canonical_process(self) -> None:
        contract_path = SCRIPT_PATH.with_name("workflow_family_trx_contract.py")
        spec = importlib.util.spec_from_file_location(
            "workflow_family_api_contract_under_test", contract_path
        )
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        dotnet_path = Path("/usr/bin/dotnet")
        project_path = Path("/repo/Chummer.Api/Chummer.Api.csproj")
        command = [
            str(dotnet_path),
            "run",
            "--project",
            str(project_path),
            "--configuration",
            "Release",
            "--no-launch-profile",
            "--no-restore",
            "--urls",
            module.CANONICAL_API_BASE_URL,
        ]
        probe = {
            "baseUrl": module.CANONICAL_API_BASE_URL,
            "autostarted": True,
            "autostartCommand": command,
            "autostartPid": 1234,
            "processAliveAtProof": True,
            "warmed": True,
            "results": [
                {"path": path, "ok": True, "statusCode": 200, "error": ""}
                for path in module.CANONICAL_API_PROBE_PATHS
            ],
        }
        module.validate_api_probe_contract(probe, dotnet_path, project_path)

        forged = dict(probe)
        forged["autostarted"] = False
        forged["untrustedPreexistingService"] = True
        with self.assertRaisesRegex(ValueError, "not started by the canonical producer"):
            module.validate_api_probe_contract(forged, dotnet_path, project_path)

    def test_stage_manifest_commits_exact_receipt_set_and_rejects_partial_swap(self) -> None:
        contract_path = SCRIPT_PATH.with_name("workflow_family_trx_contract.py")
        spec = importlib.util.spec_from_file_location(
            "workflow_family_manifest_contract_under_test", contract_path
        )
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        producer_run_id = "11111111-1111-4111-8111-111111111111"
        candidate_snapshot_id = "a" * 64
        execution_run_digest = "b" * 64
        candidate_digest = "c" * 64
        generated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        release_identity = {
            "path": "/trusted/release.json",
            "sha256": "d" * 64,
            "sizeBytes": 123,
            "channelId": "stable",
            "releaseVersion": "6.0.0",
        }
        upstream_stage_manifests = [
            {
                "path": "/trusted/execution.generated.json",
                "sha256": "e" * 64,
                "sizeBytes": 456,
            }
        ]

        with tempfile.TemporaryDirectory() as temporary_directory:
            repo_root = Path(temporary_directory)
            expected_receipts: dict[str, Path] = {}
            receipt_records: list[dict[str, Any]] = []
            for family_id in ("family-a", "family-b"):
                receipt_path = (
                    repo_root
                    / ".codex-studio/published/workflow-family-parity/sr4"
                    / f"{family_id}.generated.json"
                )
                receipt_path.parent.mkdir(parents=True, exist_ok=True)
                receipt_payload = {
                    "schemaVersion": 1,
                    "producerRunId": producer_run_id,
                    "candidateSnapshotId": candidate_snapshot_id,
                    "workflowEpochId": candidate_snapshot_id,
                    "executionRunDigest": execution_run_digest,
                    "generatedAt": generated_at,
                    "contract_name": "chummer6-ui.sr4_workflow_family_verification_receipt",
                    "status": "pass",
                    "summary": family_id,
                    "reasons": [],
                    "evidence": {
                        "edition": "sr4",
                        "familyId": family_id,
                        "producerRunId": producer_run_id,
                        "candidateSnapshotId": candidate_snapshot_id,
                        "workflowEpochId": candidate_snapshot_id,
                        "executionRunDigest": execution_run_digest,
                        "candidateDigest": candidate_digest,
                        "releaseIdentity": release_identity,
                    },
                }
                receipt_path.write_text(
                    json.dumps(receipt_payload, indent=2) + "\n", encoding="utf-8"
                )
                expected_receipts[family_id] = receipt_path
                receipt_records.append(
                    module.workflow_stage_receipt_record(
                        receipt_path, receipt_payload
                    )
                )

            manifest = module.build_workflow_stage_manifest(
                edition="sr4",
                stage="verification",
                status="pass",
                generated_at=generated_at,
                producer_run_id=producer_run_id,
                candidate_snapshot_id=candidate_snapshot_id,
                execution_run_digest=execution_run_digest,
                execution_started_at=generated_at,
                execution_completed_at=generated_at,
                candidate_digest=candidate_digest,
                release_identity=release_identity,
                receipt_records=receipt_records,
                upstream_stage_manifests=upstream_stage_manifests,
            )
            manifest_path = module.workflow_stage_manifest_path(
                repo_root, "sr4", "verification"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
            )
            validated = module.validate_workflow_stage_manifest(
                manifest_path=manifest_path,
                repo_root=repo_root,
                edition="sr4",
                stage="verification",
                expected_receipts=expected_receipts,
                expected_release_identity=release_identity,
                expected_upstream_stage_manifests=upstream_stage_manifests,
            )
            self.assertEqual("pass", validated["manifest"]["status"])

            partial_path = expected_receipts["family-a"]
            partial_payload = json.loads(partial_path.read_text(encoding="utf-8"))
            partial_payload["summary"] = "new partial epoch"
            partial_path.write_text(
                json.dumps(partial_payload, indent=2) + "\n", encoding="utf-8"
            )
            with self.assertRaisesRegex(ValueError, "does not bind current receipt bytes"):
                module.validate_workflow_stage_manifest(
                    manifest_path=manifest_path,
                    repo_root=repo_root,
                    edition="sr4",
                    stage="verification",
                    expected_receipts=expected_receipts,
                    expected_release_identity=release_identity,
                    expected_upstream_stage_manifests=upstream_stage_manifests,
                )

    def test_execution_epoch_requires_shared_candidate_and_distinct_runs(self) -> None:
        module = self.load_workflow_contract_module()
        reference_time = datetime(2026, 7, 18, 12, 0, tzinfo=timezone.utc)
        release_identity, candidate_snapshot_id, manifests, bindings = (
            self.execution_epoch_fixture(reference_time)
        )
        result = module.build_desktop_execution_epoch(
            release_identity=release_identity,
            candidate_snapshot_id=candidate_snapshot_id,
            stage_manifests=manifests,
            stage_bindings=bindings,
            reference_time=reference_time,
        )
        self.assertRegex(result["executionEpochId"], r"^[0-9a-f]{64}$")
        self.assertEqual(
            result,
            module.build_desktop_execution_epoch(
                release_identity=release_identity,
                candidate_snapshot_id=candidate_snapshot_id,
                stage_manifests=manifests,
                stage_bindings=bindings,
                reference_time=reference_time,
            ),
        )

        mutations = {
            "candidate": ("candidateSnapshotId", "9" * 64),
            "producer": (
                "producerRunId",
                manifests["sr4"]["execution"]["producerRunId"],
            ),
            "run_digest": (
                "executionRunDigest",
                manifests["sr4"]["execution"]["executionRunDigest"],
            ),
        }
        for label, (field, value) in mutations.items():
            with self.subTest(label=label):
                changed = json.loads(json.dumps(manifests))
                for stage in ("execution", "verification", "parity"):
                    changed["sr6"][stage][field] = value
                    if field == "candidateSnapshotId":
                        changed["sr6"][stage]["workflowEpochId"] = value
                with self.assertRaises(ValueError):
                    module.build_desktop_execution_epoch(
                        release_identity=release_identity,
                        candidate_snapshot_id=candidate_snapshot_id,
                        stage_manifests=changed,
                        stage_bindings=bindings,
                        reference_time=reference_time,
                    )

    def test_execution_epoch_rejects_bad_bounds(self) -> None:
        module = self.load_workflow_contract_module()
        reference_time = datetime(2026, 7, 18, 12, 0, tzinfo=timezone.utc)
        release_identity, candidate_snapshot_id, manifests, bindings = (
            self.execution_epoch_fixture(reference_time)
        )

        def changed_bounds(
            edition: str, started_at: datetime, completed_at: datetime
        ) -> dict[str, Any]:
            changed = json.loads(json.dumps(manifests))
            for stage_index, stage in enumerate(
                ("execution", "verification", "parity"), start=1
            ):
                changed[edition][stage]["executionStartedAt"] = (
                    started_at.isoformat().replace("+00:00", "Z")
                )
                changed[edition][stage]["executionCompletedAt"] = (
                    completed_at.isoformat().replace("+00:00", "Z")
                )
                changed[edition][stage]["generatedAt"] = (
                    (completed_at + timedelta(minutes=stage_index))
                    .isoformat()
                    .replace("+00:00", "Z")
                )
            return changed

        bad_cases = {
            "inverted": changed_bounds(
                "sr4", reference_time - timedelta(hours=3), reference_time - timedelta(hours=4)
            ),
            "overlap": changed_bounds(
                "sr6",
                reference_time - timedelta(hours=3, minutes=30),
                reference_time - timedelta(hours=2, minutes=30),
            ),
            "span_21601": changed_bounds(
                "sr4",
                reference_time - timedelta(hours=8, seconds=1),
                reference_time - timedelta(hours=7),
            ),
            "stale": changed_bounds(
                "sr4",
                reference_time - timedelta(hours=26),
                reference_time - timedelta(hours=25),
            ),
            "future": changed_bounds(
                "sr6",
                reference_time,
                reference_time + timedelta(seconds=301),
            ),
        }
        for label, changed in bad_cases.items():
            with self.subTest(label=label), self.assertRaises(ValueError):
                module.build_desktop_execution_epoch(
                    release_identity=release_identity,
                    candidate_snapshot_id=candidate_snapshot_id,
                    stage_manifests=changed,
                    stage_bindings=bindings,
                    reference_time=reference_time,
                )

    def test_execution_epoch_binds_every_stage_manifest(self) -> None:
        module = self.load_workflow_contract_module()
        reference_time = datetime(2026, 7, 18, 12, 0, tzinfo=timezone.utc)
        release_identity, candidate_snapshot_id, manifests, bindings = (
            self.execution_epoch_fixture(reference_time)
        )
        baseline = module.build_desktop_execution_epoch(
            release_identity=release_identity,
            candidate_snapshot_id=candidate_snapshot_id,
            stage_manifests=manifests,
            stage_bindings=bindings,
            reference_time=reference_time,
        )["executionEpochId"]
        for edition in ("sr4", "sr6"):
            for stage in ("execution", "verification", "parity"):
                with self.subTest(edition=edition, stage=stage):
                    changed_bindings = json.loads(json.dumps(bindings))
                    changed_bindings[edition][stage]["sha256"] = "9" * 64
                    changed = module.build_desktop_execution_epoch(
                        release_identity=release_identity,
                        candidate_snapshot_id=candidate_snapshot_id,
                        stage_manifests=manifests,
                        stage_bindings=changed_bindings,
                        reference_time=reference_time,
                    )["executionEpochId"]
                    self.assertNotEqual(baseline, changed)

    def test_aggregate_publishes_distinct_candidate_and_execution_identities(self) -> None:
        self.assertIn('"candidateSnapshotId": aggregate_candidate_snapshot_id', self.source)
        self.assertIn('"workflowEpochId": aggregate_candidate_snapshot_id', self.source)
        self.assertIn('"executionEpochId": execution_epoch_id', self.source)
        self.assertIn("build_desktop_execution_epoch(", self.source)
        self.assertNotIn("aggregate_workflow_epoch_ids", self.source)

    def test_ledgers_pin_exact_family_receipt_targets(self) -> None:
        repo_root = SCRIPT_PATH.parents[3]
        expected_family_ids = {
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
        }
        for edition in ("sr4", "sr6"):
            ledger = json.loads(
                (repo_root / f"docs/{edition.upper()}_WORKFLOW_PARITY_LEDGER.json").read_text(
                    encoding="utf-8"
                )
            )
            families = ledger.get("requiredFamilies")
            with self.subTest(edition=edition):
                self.assertEqual(1, ledger.get("version"))
                self.assertEqual(f"{edition}_desktop_head", ledger.get("scope"))
                self.assertEqual(expected_family_ids, {family.get("id") for family in families})
                for family in families:
                    family_id = family["id"]
                    self.assertEqual(
                        [f".codex-studio/published/workflow-family-parity/executed/{edition}/{{familyId}}.generated.json"],
                        family.get("executionReceipts"),
                    )
                    self.assertEqual(
                        [f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"],
                        family.get("verificationReceipts"),
                    )
                    self.assertEqual(
                        [f".codex-studio/published/workflow-family-parity/{edition.upper()}_WORKFLOW_FAMILY_{family_id}.generated.json"],
                        family.get("parityReceipts"),
                    )

    def test_top_level_status_requires_every_non_deferred_review_to_pass(self) -> None:
        self.assertIn(
            "non_deferred_nested_review_reasons = {",
            self.source,
        )
        self.assertIn(
            "non_deferred_nested_review_failure_count = sum(",
            self.source,
        )
        self.assertIn(
            "if not reasons and non_deferred_nested_review_failure_count == 0",
            self.source,
        )
        self.assertIn(
            'payload["evidence"]["rawReasonCount"] = len(reasons)',
            self.source,
        )
        self.assertIn(
            "len(reasons) + non_deferred_nested_review_failure_count",
            self.source,
        )

    def test_direct_flagship_slice_cannot_waive_upstream_or_family_failures(self) -> None:
        self.assertIn(
            'evidence["direct_flagship_slice_waives_blockers"] = False',
            self.source,
        )
        self.assertNotIn("filter_reason_prefixes", self.source)
        self.assertNotIn("workflow_family_review_reasons = []", self.source)
        self.assertNotIn("workflow_execution_review_reasons = []", self.source)
        self.assertNotIn("reasons, deferred_reason_items =", self.source)

    def test_m142_is_a_checked_downstream_observation_without_cycle(self) -> None:
        self.assertIn(
            "m142_observation_reasons: List[str] = []",
            self.source,
        )
        self.assertIn(
            "next90_m142_direct_workflow_proof = check_receipt(",
            self.source,
        )
        self.assertIn(
            "m142_observation_reasons,\n    evidence,",
            self.source,
        )
        self.assertIn(
            "next90_m142_direct_workflow_proof_is_fresh_pass = (",
            self.source,
        )
        self.assertIn(
            'evidence["downstream_receipt_observations"] = {',
            self.source,
        )
        self.assertNotIn(
            "next90_m142_direct_workflow_proof|$repo_root/scripts/ai/milestones/"
            "next90-m142-ui-direct-workflow-proof-check.sh",
            self.source,
        )
        upstream_review_start = self.source.index(
            "upstream_receipt_review_reasons: List[str] = []"
        )
        upstream_review_end = self.source.index(
            "release_channel_review_reasons: List[str] = []",
            upstream_review_start,
        )
        self.assertNotIn(
            '"next90_m142_direct_workflow_proof",',
            self.source[upstream_review_start:upstream_review_end],
        )

    def test_frontier_refresh_reuses_current_subgate_receipts_without_rebuilding(self) -> None:
        self.assertIn(
            'sr4_sr6_frontier)\n'
            '      env_args+=(\n'
            '        "CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=1"',
            self.source,
        )
        ruleset_index = self.source.index(
            "ruleset_ui_adaptation|$repo_root/scripts/ai/milestones/"
            "ruleset-ui-adaptation-check.sh"
        )
        frontier_index = self.source.index(
            "sr4_sr6_frontier|$repo_root/scripts/ai/milestones/"
            "sr4-sr6-desktop-parity-frontier-receipt.sh"
        )
        self.assertLess(
            ruleset_index,
            frontier_index,
            "The ruleset receipt must be current before the validation-only frontier runs.",
        )

    def test_candidate_sample_is_a_direct_deterministic_test_output(self) -> None:
        project_path = SCRIPT_PATH.parents[3] / "Chummer.Tests" / "Chummer.Tests.csproj"
        project_source = project_path.read_text(encoding="utf-8")
        self.assertIn(
            '<None Include="TestFiles\\Soma (Career).chum5"',
            project_source,
        )
        self.assertIn(
            'Link="Samples\\Legacy\\Soma-Career.chum5"',
            project_source,
        )
        self.assertIn(
            "<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>",
            project_source,
        )

    def test_m141_is_in_the_upstream_review(self) -> None:
        upstream_review_start = self.source.index(
            "upstream_receipt_review_reasons: List[str] = []"
        )
        upstream_review_end = self.source.index(
            "release_channel_review_reasons: List[str] = []",
            upstream_review_start,
        )
        upstream_review = self.source[upstream_review_start:upstream_review_end]
        self.assertIn('"next90_m141_direct_import_route_proof",', upstream_review)

    def test_channel_alignment_covers_frontier_and_flagship_receipts(self) -> None:
        channel_loop_start = self.source.index("receipt_channel_ids: Dict[str, str] = {}")
        channel_loop_end = self.source.index(
            'evidence["workflow_parity_receipt_channel_ids"] = receipt_channel_ids',
            channel_loop_start,
        )
        channel_loop = self.source[channel_loop_start:channel_loop_end]
        self.assertIn('(\"sr4_sr6_frontier\", sr4_sr6_frontier)', channel_loop)
        self.assertIn('(\"ui_flagship_release_gate\", flagship_gate)', channel_loop)

    def test_upstream_receipts_require_exact_contract_and_release_identity(self) -> None:
        expected_contracts = (
            "chummer6-ui.chummer5a_desktop_workflow_parity",
            "chummer6-ui.sr4_desktop_workflow_parity",
            "chummer6-ui.sr6_desktop_workflow_parity",
            "chummer6-ui.sr4_sr6_desktop_parity_frontier",
            "chummer6-ui.ruleset_ui_adaptation_frontier",
            "chummer6-ui.flagship_ui_release_gate",
            "chummer6-ui.desktop_visual_familiarity_exit_gate",
            "chummer6-ui.chummer5a_screenshot_review_gate",
            "chummer6-ui.next90_m141_ui_direct_import_route_proof",
            "chummer6-ui.next90_m142_ui_direct_workflow_proof",
        )
        for contract_name in expected_contracts:
            with self.subTest(contract_name=contract_name):
                self.assertIn(f'expected_contract="{contract_name}"', self.source)
        self.assertEqual(10, self.source.count("expected_contract=\"chummer6-ui."))
        for token in (
            '("contract_name", "contractName")',
            "receipt_release_versions",
            "release_version_alignment",
            "upstream_receipt_bindings",
            "upstream receipt changed after validation",
            "direct proof source changed after validation",
            'release_channel_review_reasons.append("release_channel:contract_name")',
            'release_channel_review_reasons.append("release_channel:status")',
        ):
            with self.subTest(token=token):
                self.assertIn(token, self.source)
        self.assertNotIn(
            'if not channel_id and label == "chummer5a_screenshot_review_gate":',
            self.source,
        )

        function_start = self.source.index("def check_receipt(")
        function_end = self.source.index("\n\ndef add_dependency_refresh_failure_reason", function_start)
        namespace = {
            "Any": Any,
            "Dict": Dict,
            "List": list,
            "Path": Path,
            "upstream_receipt_bindings": {},
            "load_regular_json": lambda path, _label: (
                json.loads(path.read_text(encoding="utf-8")),
                path.read_bytes(),
            ),
            "binding_for_bytes": lambda path, raw: {
                "path": str(path.resolve()),
                "sha256": hashlib.sha256(raw).hexdigest(),
                "sizeBytes": len(raw),
            },
            "validate_receipt_freshness": lambda *_args, **_kwargs: None,
        }
        exec(self.source[function_start:function_end], namespace)
        check_receipt = namespace["check_receipt"]
        with tempfile.TemporaryDirectory() as temporary_directory:
            receipt = Path(temporary_directory) / "receipt.json"
            receipt.write_text(
                json.dumps(
                    {
                        "status": "pass",
                        "contract_name": "laundered.generic.receipt",
                        "generatedAt": datetime.now(timezone.utc).isoformat(),
                    }
                ),
                encoding="utf-8",
            )
            reasons: list[str] = []
            evidence: dict[str, Any] = {}
            check_receipt(
                receipt,
                "fixture",
                reasons,
                evidence,
                expected_contract="chummer6-ui.expected",
            )
            self.assertTrue(any("contract identity" in reason for reason in reasons))
            self.assertIn(str(receipt.resolve()), namespace["upstream_receipt_bindings"])

    def test_aggregate_requires_first_attempt_execution_and_bounded_snapshots(self) -> None:
        self.assertIn('record.get("attemptCount") != 1', self.source)
        self.assertIn("MAX_REGULAR_INPUT_BYTES = 64 * 1024 * 1024", self.source)
        self.assertIn("direct_source_bindings", self.source)
        self.assertIn("load_direct_source_text", self.source)

    def test_trx_revalidation_binds_committed_execution_window(self) -> None:
        self.assertEqual(2, self.source.count("validate_trx_contract("))
        self.assertIn(
            "run_root,\n"
            "                execution_started_at,\n"
            "                execution_completed_at,\n"
            "            )",
            self.source,
        )
        self.assertIn(
            'execution_manifest = workflow_stage_manifest_payloads.get(\n'
            "                family_edition, {}\n"
            '            ).get("execution")',
            self.source,
        )
        self.assertIn(
            'execution_manifest.get("executionStartedAt"),\n'
            '                    execution_manifest.get("executionCompletedAt"),',
            self.source,
        )

    def test_visual_refresh_binds_the_selected_release_channel(self) -> None:
        self.assertIn(
            'desktop_visual_familiarity_gate)\n'
            '      env_args+=(\n'
            '        "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH=$release_channel_path"\n'
            '        "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH=$dependency_receipt_target"',
            self.source,
        )

    def test_human_rule_authority_is_evidence_only(self) -> None:
        self.assertIn(
            'evidence["human_side_rule_authority_execution_waiver_enabled"] = False',
            self.source,
        )
        self.assertNotIn(
            "require_passing_receipt=not human_side_rule_authority_is_approved",
            self.source,
        )
        self.assertNotIn(
            "or human_side_rule_authority_is_approved",
            self.source,
        )
        self.assertNotIn(
            "channel_alignment_recovered_from_human_side_rule_authority",
            self.source,
        )

    def test_execution_receipt_requires_explicit_integer_zero_exit_code(self) -> None:
        self.assertIn(
            'dotnet_exit_code = dotnet_test.get("exitCode")',
            self.source,
        )
        self.assertIn(
            "if type(dotnet_exit_code) is not int or dotnet_exit_code != 0:",
            self.source,
        )
        self.assertNotIn('dotnet_test.get("exitCode") or 0', self.source)

    def test_external_only_classification_never_waives_failed_receipts(self) -> None:
        self.assertIn(
            'evidence["workflow_family_external_only_deferred"] = False',
            self.source,
        )
        self.assertIn(
            'evidence["workflow_execution_external_only_deferred"] = False',
            self.source,
        )
        self.assertIn(
            'evidence["ui_flagship_release_gate_external_desktop_only_deferred"] = False',
            self.source,
        )
        self.assertNotIn(
            'if evidence.get("workflow_family_failures_external_only") is True',
            self.source,
        )
        self.assertNotIn(
            'if evidence.get("workflow_execution_failures_external_only") is True',
            self.source,
        )
        self.assertNotIn(
            "if flagship_gate_route_local_only or flagship_gate_external_desktop_only",
            self.source,
        )

    def test_load_json_fails_closed_for_malformed_or_non_unicode_receipts(self) -> None:
        function_start = self.source.index("def load_json(")
        function_end = self.source.index("\n\ndef status_ok", function_start)
        namespace = {
            "Any": Any,
            "Dict": Dict,
            "Path": Path,
            "json": json,
            "os": os,
            "stat": stat,
            "MAX_REGULAR_INPUT_BYTES": 64 * 1024 * 1024,
        }
        exec(self.source[function_start:function_end], namespace)
        load_json = namespace["load_json"]

        with tempfile.TemporaryDirectory() as temporary_directory:
            receipt = Path(temporary_directory) / "receipt.json"
            for invalid_content in (b"{not-json", b"\xff"):
                with self.subTest(invalid_content=invalid_content):
                    receipt.write_bytes(invalid_content)
                    self.assertEqual({}, load_json(receipt))

    def test_receipt_publication_is_atomic(self) -> None:
        self.assertIn("tempfile.mkstemp(", self.source)
        self.assertIn("os.fsync(handle.fileno())", self.source)
        self.assertIn("os.replace(temporary_path, path)", self.source)
        self.assertIn("os.fsync(directory_fd)", self.source)
        self.assertNotIn("receipt_path.write_text(", self.source)


if __name__ == "__main__":
    unittest.main()
