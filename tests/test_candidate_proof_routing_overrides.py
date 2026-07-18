from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "scripts" / "ai"))

import candidate_proof_routing as routing  # noqa: E402


PRODUCERS: dict[str, dict[str, object]] = {
    "b14": {
        "script": REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh",
        "plane": (
            "CHUMMER_B14_OUTPUT_PATH",
            "CHUMMER_B14_SCREENSHOT_OUTPUT_DIR",
            "CHUMMER_B14_PROOF_INPUT_ROOT",
            "CHUMMER_B14_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_B14_OUTPUT_PATH",
        "sidecar": "CHUMMER_B14_SCREENSHOT_OUTPUT_DIR",
        "input": "CHUMMER_B14_PROOF_INPUT_ROOT",
        "release": "CHUMMER_B14_RELEASE_CHANNEL_PATH",
        "default_output": "UI_FLAGSHIP_RELEASE_GATE.generated.json",
    },
    "desktop-workflow": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "materialize-desktop-workflow-execution-gate.sh",
        "plane": (
            "CHUMMER_DESKTOP_WORKFLOW_OUTPUT_PATH",
            "CHUMMER_DESKTOP_WORKFLOW_PROOF_INPUT_ROOT",
            "CHUMMER_DESKTOP_WORKFLOW_EXTERNAL_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_DESKTOP_WORKFLOW_OUTPUT_PATH",
        "input": "CHUMMER_DESKTOP_WORKFLOW_PROOF_INPUT_ROOT",
        "release": "CHUMMER_DESKTOP_WORKFLOW_EXTERNAL_RELEASE_CHANNEL_PATH",
        "default_output": "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
    },
    "chummer5a": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "chummer5a-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_OUTPUT_PATH",
        "release": "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
    "sr4": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "sr4-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_SR4_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_SR4_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
            "CHUMMER_SR4_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_SR4_WORKFLOW_PARITY_OUTPUT_PATH",
        "input": "CHUMMER_SR4_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
        "release": "CHUMMER_SR4_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "SR4_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
    "sr6": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "sr6-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_SR6_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_SR6_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
            "CHUMMER_SR6_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_SR6_WORKFLOW_PARITY_OUTPUT_PATH",
        "input": "CHUMMER_SR6_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
        "release": "CHUMMER_SR6_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "SR6_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
}

ALL_PLANE_VARIABLES = {
    variable
    for configuration in PRODUCERS.values()
    for variable in configuration["plane"]
}


class CandidateProofRoutingOverrideTests(unittest.TestCase):
    @staticmethod
    def _write_json(path: Path, payload: dict[str, object]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    def _fixture(
        self,
        root: Path,
        producer: str,
    ) -> tuple[dict[str, str], Path, Path, Path, list[tuple[routing.ReceiptSpec, Path]]]:
        configuration = PRODUCERS[producer]
        input_root = root / "proof-inputs"
        input_root.mkdir(parents=True)
        release_channel = root / "release" / "RELEASE_CHANNEL.generated.json"
        self._write_json(
            release_channel,
            {
                "contract_name": routing.RELEASE_CHANNEL_CONTRACT,
                "status": routing.RELEASE_CHANNEL_STATUS,
                "channelId": "candidate-fixture",
                "channel": "candidate-fixture",
                "version": "6.0.0-fixture",
                "releaseVersion": "6.0.0-fixture",
                "publishedAt": "2026-07-18T13:14:45Z",
                "published_at": "2026-07-18T13:14:45Z",
            },
        )

        routed_input_root = input_root if "input" in configuration else None
        required = routing.required_inputs(producer, REPO_ROOT, routed_input_root)
        for receipt_spec, receipt_path in required:
            self._write_json(
                receipt_path,
                {
                    "contract_name": receipt_spec.contract_name or "fixture.unconstrained",
                    "generatedAt": "2026-07-18T13:14:45Z",
                    "status": "pass",
                },
            )

        output_path = root / "candidate-output" / str(configuration["default_output"])
        environment = {
            str(configuration["output"]): str(output_path),
            str(configuration["release"]): str(release_channel),
        }
        if "input" in configuration:
            environment[str(configuration["input"])] = str(input_root)
        if "sidecar" in configuration:
            environment[str(configuration["sidecar"])] = str(
                root / "candidate-output" / "flagship-screenshots"
            )
        return environment, output_path, release_channel, input_root, required

    @staticmethod
    def _run(
        producer: str,
        environment: dict[str, str],
    ) -> subprocess.CompletedProcess[str]:
        process_environment = os.environ.copy()
        for variable in ALL_PLANE_VARIABLES:
            process_environment.pop(variable, None)
        process_environment["CHUMMER_CANDIDATE_PROOF_ROUTING_PREFLIGHT_ONLY"] = "1"
        process_environment.update(environment)
        return subprocess.run(
            ["bash", str(PRODUCERS[producer]["script"])],
            cwd=REPO_ROOT,
            env=process_environment,
            capture_output=True,
            text=True,
            check=False,
            timeout=20,
        )

    def test_every_producer_accepts_a_complete_plane_without_writing(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, _, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )

                completed = self._run(producer, environment)

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertFalse(output_path.exists())
                sidecar_variable = PRODUCERS[producer].get("sidecar")
                if sidecar_variable:
                    self.assertFalse(Path(environment[str(sidecar_variable)]).exists())

    def test_every_producer_rejects_a_partial_plane(self) -> None:
        for producer, configuration in PRODUCERS.items():
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                output_path = Path(temporary_directory) / "must-not-exist.json"
                environment = {str(configuration["output"]): str(output_path)}

                completed = self._run(producer, environment)

                self.assertEqual(64, completed.returncode, completed.stderr)
                self.assertIn("external plane requires non-blank", completed.stderr)
                self.assertFalse(output_path.exists())

    def test_missing_explicit_input_never_falls_back_to_tracked_defaults(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, required = self._fixture(
                    Path(temporary_directory), producer
                )
                missing_path = required[0][1] if required else release_channel
                missing_path.unlink()

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn(str(missing_path), completed.stderr)
                self.assertFalse(output_path.exists())

    def test_supplied_inputs_are_contract_and_status_validated(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, required = self._fixture(
                    Path(temporary_directory), producer
                )
                if required:
                    receipt_spec, receipt_path = required[0]
                    self._write_json(
                        receipt_path,
                        {
                            "contract_name": receipt_spec.contract_name or "fixture.unconstrained",
                            "status": "failed",
                        },
                    )
                    completed = self._run(producer, environment)
                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertIn("must be pass/passed/ready", completed.stderr)
                    self.assertFalse(output_path.exists())

                    if receipt_spec.contract_name:
                        self._write_json(
                            receipt_path,
                            {"contract_name": "wrong.contract", "status": "pass"},
                        )
                        completed = self._run(producer, environment)
                        self.assertEqual(65, completed.returncode, completed.stderr)
                        self.assertIn("proof input contract must be", completed.stderr)
                else:
                    self._write_json(
                        release_channel,
                        {
                            "contract_name": routing.RELEASE_CHANNEL_CONTRACT,
                            "status": routing.RELEASE_CHANNEL_STATUS,
                            "channelId": "candidate-fixture",
                            "publishedAt": "2026-07-18T13:14:45Z",
                        },
                    )
                    completed = self._run(producer, environment)
                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertIn("version/releaseVersion", completed.stderr)

    def test_release_channels_require_expected_contract_and_published_status(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                release_payload = json.loads(release_channel.read_text(encoding="utf-8"))
                release_payload["contract_name"] = "wrong.contract"
                self._write_json(release_channel, release_payload)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("release channel input contract must be", completed.stderr)
                self.assertFalse(output_path.exists())

                release_payload["contract_name"] = routing.RELEASE_CHANNEL_CONTRACT
                release_payload["status"] = "revoked"
                self._write_json(release_channel, release_payload)
                completed = self._run(producer, environment)
                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("release channel input status must be published", completed.stderr)
                self.assertFalse(output_path.exists())

    def test_explicit_input_roots_cannot_escape_through_symlinks(self) -> None:
        for producer, configuration in PRODUCERS.items():
            if "input" not in configuration:
                continue
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                environment, output_path, _, input_root, required = self._fixture(root, producer)
                nested = next(
                    (
                        receipt_path
                        for _, receipt_path in required
                        if len(receipt_path.relative_to(input_root).parts) > 1
                    ),
                    None,
                )
                if nested is not None:
                    relative_path = nested.relative_to(input_root)
                    routed_directory = input_root / relative_path.parts[0]
                    outside_directory = root / "tracked-stale-defaults"
                    routed_directory.rename(outside_directory)
                    routed_directory.symlink_to(outside_directory, target_is_directory=True)
                    protected_path = outside_directory.joinpath(*relative_path.parts[1:])
                else:
                    routed_input = required[0][1]
                    protected_path = root / "tracked-stale-default.json"
                    routed_input.rename(protected_path)
                    routed_input.symlink_to(protected_path)
                protected_bytes = protected_path.read_bytes()

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("symbolic link", completed.stderr)
                self.assertFalse(output_path.exists())
                self.assertEqual(protected_bytes, protected_path.read_bytes())

    def test_outputs_and_sidecars_must_be_disjoint_from_explicit_input_roots(self) -> None:
        for producer, configuration in PRODUCERS.items():
            if "input" not in configuration:
                continue
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                environment, _, _, input_root, required = self._fixture(root, producer)
                protected_bytes = required[0][1].read_bytes()
                nested_output = input_root / "new-candidate-output.generated.json"
                environment[str(configuration["output"])] = str(nested_output)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("output and explicit input root must not overlap", completed.stderr)
                self.assertFalse(nested_output.exists())
                self.assertEqual(protected_bytes, required[0][1].read_bytes())

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            environment, output_path, _, input_root, required = self._fixture(root, "b14")
            nested_sidecar = input_root / "new-candidate-sidecar"
            environment[str(PRODUCERS["b14"]["sidecar"])] = str(nested_sidecar)
            protected_bytes = required[0][1].read_bytes()

            completed = self._run("b14", environment)

            self.assertEqual(65, completed.returncode, completed.stderr)
            self.assertIn("sidecar output and explicit input root must not overlap", completed.stderr)
            self.assertFalse(output_path.exists())
            self.assertFalse(nested_sidecar.exists())
            self.assertEqual(protected_bytes, required[0][1].read_bytes())

    def test_hard_link_output_aliases_are_rejected_without_clobbering_inputs(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                output_path.parent.mkdir(parents=True)
                expected_release = release_channel.read_bytes()
                os.link(release_channel, output_path)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("must not alias proof input", completed.stderr)
                self.assertTrue(output_path.samefile(release_channel))
                self.assertEqual(expected_release, release_channel.read_bytes())
                self.assertEqual(expected_release, output_path.read_bytes())

    def test_symlink_and_resolved_output_aliases_are_rejected(self) -> None:
        for producer in PRODUCERS:
            for alias_kind in ("symlink", "resolved-parent"):
                with (
                    self.subTest(producer=producer, alias_kind=alias_kind),
                    tempfile.TemporaryDirectory() as temporary_directory,
                ):
                    root = Path(temporary_directory)
                    environment, output_path, release_channel, _, _ = self._fixture(root, producer)
                    expected_release = release_channel.read_bytes()
                    if alias_kind == "symlink":
                        output_path.parent.mkdir(parents=True)
                        output_path.symlink_to(release_channel)
                    else:
                        alias_parent = root / "resolved-release-parent"
                        alias_parent.symlink_to(release_channel.parent, target_is_directory=True)
                        output_path = alias_parent / release_channel.name
                        environment[str(PRODUCERS[producer]["output"])] = str(output_path)

                    completed = self._run(producer, environment)

                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertEqual(expected_release, release_channel.read_bytes())

    def test_atomic_writes_validate_output_contracts_for_every_producer(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                _, output_path, release_channel, input_root, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                routed_input_root = input_root if "input" in PRODUCERS[producer] else None
                payload = {
                    "contract_name": routing.OUTPUT_CONTRACTS[producer],
                    "status": "pass",
                }

                routing.atomic_write_json(
                    producer=producer,
                    output_path=output_path,
                    payload=payload,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    input_root=routed_input_root,
                )

                self.assertEqual(payload, json.loads(output_path.read_text(encoding="utf-8")))
                output_path.unlink()
                for invalid_payload in (
                    {"contract_name": "wrong.contract", "status": "pass"},
                    {"contract_name": routing.OUTPUT_CONTRACTS[producer], "status": "unknown"},
                ):
                    with self.assertRaises(routing.RoutingError):
                        routing.atomic_write_json(
                            producer=producer,
                            output_path=output_path,
                            payload=invalid_payload,
                            repo_root=REPO_ROOT,
                            release_channel_path=release_channel,
                            input_root=routed_input_root,
                        )
                    self.assertFalse(output_path.exists())

    def test_final_prewrite_revalidation_catches_a_late_hard_link_alias(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                _, output_path, release_channel, input_root, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                routed_input_root = input_root if "input" in PRODUCERS[producer] else None
                expected_release = release_channel.read_bytes()
                original_preflight = routing.preflight_external_plane
                call_count = 0

                def racing_preflight(**kwargs: object) -> list[Path]:
                    nonlocal call_count
                    call_count += 1
                    if call_count == 2:
                        output_path.parent.mkdir(parents=True, exist_ok=True)
                        os.link(release_channel, output_path)
                    return original_preflight(**kwargs)

                with mock.patch.object(
                    routing, "preflight_external_plane", side_effect=racing_preflight
                ):
                    with self.assertRaises(routing.RoutingError):
                        routing.atomic_write_json(
                            producer=producer,
                            output_path=output_path,
                            payload={
                                "contract_name": routing.OUTPUT_CONTRACTS[producer],
                                "status": "pass",
                            },
                            repo_root=REPO_ROOT,
                            release_channel_path=release_channel,
                            input_root=routed_input_root,
                        )

                self.assertEqual(2, call_count)
                self.assertTrue(output_path.samefile(release_channel))
                self.assertEqual(expected_release, release_channel.read_bytes())

    def test_b14_sidecar_replacement_is_atomic_and_cannot_replace_input_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            _, output_path, release_channel, input_root, required = self._fixture(root, "b14")
            source = root / "staged-screenshots"
            source.mkdir()
            (source / "new.png").write_bytes(b"new-proof")
            sidecar = root / "candidate-output" / "screenshots"
            sidecar.mkdir(parents=True)
            (sidecar / "old.png").write_bytes(b"old-proof")

            routing.atomic_replace_directory(
                producer="b14",
                source=source,
                output_path=sidecar,
                repo_root=REPO_ROOT,
                release_channel_path=release_channel,
                input_root=input_root,
            )

            self.assertEqual(b"new-proof", (sidecar / "new.png").read_bytes())
            self.assertFalse((sidecar / "old.png").exists())
            protected_bytes = required[0][1].read_bytes()
            with self.assertRaises(routing.RoutingError):
                routing.atomic_replace_directory(
                    producer="b14",
                    source=source,
                    output_path=input_root,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    input_root=input_root,
                )
            self.assertEqual(protected_bytes, required[0][1].read_bytes())
            self.assertFalse(output_path.exists())

    def test_integrations_retain_defaults_and_use_shared_atomic_router(self) -> None:
        for producer, configuration in PRODUCERS.items():
            with self.subTest(producer=producer):
                script_text = Path(configuration["script"]).read_text(encoding="utf-8")
                self.assertIn(str(configuration["default_output"]), script_text)
                self.assertIn("candidate_proof_preflight", script_text)
                self.assertIn("atomic_write_json", script_text)
                for variable in configuration["plane"]:
                    self.assertIn(str(variable), script_text)
        b14_text = Path(PRODUCERS["b14"]["script"]).read_text(encoding="utf-8")
        self.assertIn("replace-directory", b14_text)


if __name__ == "__main__":
    unittest.main()
