from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = REPO_ROOT / "scripts" / "ai" / "milestones" / "b15-localization-release-gate.sh"
DEFAULT_RECEIPT_PATH = (
    REPO_ROOT / ".codex-studio" / "published" / "UI_LOCALIZATION_RELEASE_GATE.generated.json"
)
RUNNER_DIR = REPO_ROOT / "Chummer.Tests" / "Presentation" / "bin" / "Debug" / "net10.0"


class B15LocalizationReleaseGateOverrideTests(unittest.TestCase):
    def run_gate(
        self,
        *arguments: str,
        environment: dict[str, str] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        process_environment = os.environ.copy()
        process_environment.pop("CHUMMER_B15_OUTPUT_PATH", None)
        process_environment.pop("CHUMMER_B15_LOCAL_RELEASE_PROOF_PATH", None)
        if environment:
            process_environment.update(environment)
        return subprocess.run(
            ["bash", str(SCRIPT_PATH), *arguments],
            cwd=REPO_ROOT,
            env=process_environment,
            capture_output=True,
            text=True,
            check=False,
        )

    @staticmethod
    def write_local_release_proof(path: Path, *, status: str = "passed") -> None:
        path.write_text(
            json.dumps(
                {
                    "contract_name": "chummer6-ui.local_release_proof",
                    "generated_at": "2026-07-18T13:14:45Z",
                    "status": status,
                    "evidence": {"fixture": True},
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )

    def test_help_is_side_effect_free_and_documents_both_override_planes(self) -> None:
        before = DEFAULT_RECEIPT_PATH.read_bytes() if DEFAULT_RECEIPT_PATH.is_file() else None

        completed = self.run_gate("--help")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("--output PATH --local-release-proof PATH", completed.stdout)
        self.assertIn("CHUMMER_B15_OUTPUT_PATH", completed.stdout)
        self.assertIn("CHUMMER_B15_LOCAL_RELEASE_PROOF_PATH", completed.stdout)
        after = DEFAULT_RECEIPT_PATH.read_bytes() if DEFAULT_RECEIPT_PATH.is_file() else None
        self.assertEqual(before, after)

    def test_output_and_local_proof_overrides_are_required_as_a_pair(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            output_path = root / "localization.json"
            proof_path = root / "local-proof.json"
            self.write_local_release_proof(proof_path)

            output_only = self.run_gate("--output", str(output_path))
            proof_only = self.run_gate("--local-release-proof", str(proof_path))

            self.assertEqual(64, output_only.returncode)
            self.assertEqual(64, proof_only.returncode)
            self.assertIn("must be supplied together", output_only.stderr)
            self.assertIn("must be supplied together", proof_only.stderr)
            self.assertFalse(output_path.exists())

    def test_explicit_local_proof_must_be_regular_valid_and_passing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            output_path = root / "localization.json"

            missing = self.run_gate(
                "--output",
                str(output_path),
                "--local-release-proof",
                str(root / "missing.json"),
            )
            self.assertEqual(65, missing.returncode)

            invalid_path = root / "invalid.json"
            invalid_path.write_text("not-json\n", encoding="utf-8")
            invalid = self.run_gate(
                "--output",
                str(output_path),
                "--local-release-proof",
                str(invalid_path),
            )
            self.assertEqual(65, invalid.returncode)
            self.assertIn("not valid JSON", invalid.stderr)

            failed_path = root / "failed.json"
            self.write_local_release_proof(failed_path, status="failed")
            failed = self.run_gate(
                "--output",
                str(output_path),
                "--local-release-proof",
                str(failed_path),
            )
            self.assertEqual(65, failed.returncode)
            self.assertIn("must be pass/passed/ready", failed.stderr)

            same_path = self.run_gate(
                "--output",
                str(failed_path),
                "--local-release-proof",
                str(failed_path),
            )
            self.assertEqual(65, same_path.returncode)
            self.assertIn("paths must differ", same_path.stderr)
            self.assertFalse(output_path.exists())

    def test_hard_link_output_alias_is_rejected_without_overwriting_input(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            proof_path = root / "local-proof.json"
            output_path = root / "hard-linked-output.json"
            self.write_local_release_proof(proof_path)
            expected_proof = proof_path.read_bytes()
            os.link(proof_path, output_path)

            completed = self.run_gate(
                "--output",
                str(output_path),
                "--local-release-proof",
                str(proof_path),
            )

            self.assertEqual(65, completed.returncode)
            self.assertIn("paths must differ", completed.stderr)
            self.assertTrue(proof_path.samefile(output_path))
            self.assertEqual(expected_proof, proof_path.read_bytes())
            self.assertEqual(expected_proof, output_path.read_bytes())

    def test_cli_pair_overrides_environment_and_routes_receipt_externally(self) -> None:
        runner_dir_existed = RUNNER_DIR.exists()
        default_before = DEFAULT_RECEIPT_PATH.read_bytes() if DEFAULT_RECEIPT_PATH.is_file() else None
        try:
            with tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                tools_dir = root / "tools"
                tools_dir.mkdir()
                dotnet_path = tools_dir / "dotnet"
                dotnet_path.write_text(
                    "#!/usr/bin/env bash\n"
                    "set -euo pipefail\n"
                    "if [[ ${1:-} == build ]]; then exit 0; fi\n"
                    "printf '%s\\n' 'Test run summary: Passed!' '  total: 1' '  failed: 0' "
                    "'  succeeded: 1' '  skipped: 0' '  duration: 1ms'\n",
                    encoding="utf-8",
                )
                dotnet_path.chmod(0o755)

                proof_path = root / "local-proof.json"
                environment_output_path = root / "environment" / "localization.json"
                output_path = root / "nested" / "localization.json"
                self.write_local_release_proof(proof_path)
                environment_completed = self.run_gate(
                    environment={
                        "PATH": f"{tools_dir}{os.pathsep}{os.environ['PATH']}",
                        "CHUMMER_B15_OUTPUT_PATH": str(environment_output_path),
                        "CHUMMER_B15_LOCAL_RELEASE_PROOF_PATH": str(proof_path),
                    }
                )
                self.assertEqual(
                    0,
                    environment_completed.returncode,
                    environment_completed.stdout + environment_completed.stderr,
                )
                self.assertTrue(environment_output_path.is_file())

                environment = {
                    "PATH": f"{tools_dir}{os.pathsep}{os.environ['PATH']}",
                    "CHUMMER_B15_OUTPUT_PATH": str(root / "ignored-output.json"),
                    "CHUMMER_B15_LOCAL_RELEASE_PROOF_PATH": str(root / "ignored-missing-proof.json"),
                }

                completed = self.run_gate(
                    "--output",
                    str(output_path),
                    "--local-release-proof",
                    str(proof_path),
                    environment=environment,
                )

                self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
                self.assertIn("[b15] PASS", completed.stdout)
                self.assertTrue(output_path.is_file())
                self.assertFalse((root / "ignored-output.json").exists())
                payload = json.loads(output_path.read_text(encoding="utf-8"))
                self.assertEqual("pass", payload["status"])
                self.assertEqual("passed", payload["local_release_proof"]["status"])
                self.assertTrue(payload["local_release_proof"]["evidence"]["fixture"])
        finally:
            if not runner_dir_existed:
                shutil.rmtree(RUNNER_DIR, ignore_errors=True)
                for parent in (RUNNER_DIR.parent, RUNNER_DIR.parent.parent, RUNNER_DIR.parent.parent.parent):
                    try:
                        parent.rmdir()
                    except OSError:
                        break
            default_after = DEFAULT_RECEIPT_PATH.read_bytes() if DEFAULT_RECEIPT_PATH.is_file() else None
            self.assertEqual(default_before, default_after)


if __name__ == "__main__":
    unittest.main()
