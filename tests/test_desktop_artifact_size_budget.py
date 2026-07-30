from __future__ import annotations

import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
SCRIPT = REPO_ROOT / "scripts" / "verify_desktop_artifact_size_budget.py"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_fixture(root: Path) -> tuple[Path, Path]:
    files_dir = root / "files"
    files_dir.mkdir(parents=True)
    linux = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    windows = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload = files_dir / "chummer-avalonia-win-x64-payload.zip"
    linux.write_bytes(b"linux-installer-fixture")
    windows.write_bytes(b"windows-bootstrap-fixture")
    payload.write_bytes(b"windows-payload-fixture")
    manifest = {
        "status": "published",
        "version": "run-test",
        "releaseVersion": "run-test",
        "channel": "preview",
        "desktopTupleCoverage": {
            "promotedInstallerTuples": [
                {
                    "artifactId": "avalonia-linux-x64-installer",
                    "head": "avalonia",
                    "platform": "linux",
                    "rid": "linux-x64",
                    "arch": "x64",
                },
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "head": "avalonia",
                    "platform": "windows",
                    "rid": "win-x64",
                    "arch": "x64",
                },
            ]
        },
        "downloads": [
            {
                "id": "avalonia-linux-x64-installer",
                "artifactId": "avalonia-linux-x64-installer",
                "head": "avalonia",
                "platformId": "linux",
                "rid": "linux-x64",
                "arch": "x64",
                "kind": "installer",
                "fileName": linux.name,
                "sizeBytes": linux.stat().st_size,
                "sha256": _sha256(linux),
                "installerMode": None,
                "payloadFileName": None,
                "payloadSizeBytes": None,
                "payloadSha256": None,
            },
            {
                "id": "avalonia-win-x64-installer",
                "artifactId": "avalonia-win-x64-installer",
                "head": "avalonia",
                "platformId": "windows",
                "rid": "win-x64",
                "arch": "x64",
                "kind": "installer",
                "fileName": windows.name,
                "sizeBytes": windows.stat().st_size,
                "sha256": _sha256(windows),
                "installerMode": "bootstrap",
                "payloadFileName": payload.name,
                "payloadSizeBytes": payload.stat().st_size,
                "payloadSha256": _sha256(payload),
            },
        ],
    }
    manifest_path = root / "releases.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest_path, files_dir


def _run(
    manifest: Path,
    files_dir: Path,
    output: Path,
    *,
    check_only: bool = False,
) -> subprocess.CompletedProcess[str]:
    command = [
        "python3",
        str(SCRIPT),
        "--manifest",
        str(manifest),
        "--files-dir",
        str(files_dir),
        "--output",
        str(output),
    ]
    if check_only:
        command.append("--check-only")
    return subprocess.run(
        command,
        cwd=REPO_ROOT,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
        timeout=30,
    )


class DesktopArtifactSizeBudgetTests(unittest.TestCase):
    def test_current_promoted_artifacts_are_manifest_bound_and_within_budget(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output = Path(temp_dir) / "receipt.json"
            completed = _run(
                WORKSPACE_ROOT / "chummer.run-services" / "Chummer.Portal" / "downloads" / "releases.json",
                WORKSPACE_ROOT / "chummer.run-services" / "Chummer.Portal" / "downloads" / "files",
                output,
            )

            self.assertEqual(completed.returncode, 0, completed.stdout)
            payload = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(payload["status"], "pass")
            self.assertEqual(payload["failures"], [])
            self.assertEqual(payload["startup_time_budget"]["status"], "not_enforced")
            self.assertEqual(
                payload["startup_time_budget"]["reason_code"],
                "receipt_timer_starts_inside_smoke_handler_after_process_entry",
            )
            self.assertLessEqual(
                payload["aggregate_observed_bytes"], payload["aggregate_max_bytes"]
            )
            for artifact in payload["artifacts"].values():
                self.assertTrue(artifact["installer"]["manifest_identity_matches"])
                if artifact["payload"] is not None:
                    self.assertTrue(artifact["payload"]["manifest_identity_matches"])

    def test_missing_artifact_fails_and_materializes_diagnostics(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest, files_dir = _write_fixture(root)
            (files_dir / "chummer-avalonia-linux-x64-installer.deb").unlink()
            output = root / "receipt.json"

            completed = _run(manifest, files_dir, output)

            self.assertEqual(completed.returncode, 1, completed.stdout)
            payload = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(payload["status"], "fail")
            self.assertTrue(
                any("artifact is missing" in failure for failure in payload["failures"]),
                payload["failures"],
            )

    def test_oversized_installer_fails_even_when_manifest_identity_matches(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest, files_dir = _write_fixture(root)
            windows = files_dir / "chummer-avalonia-win-x64-installer.exe"
            windows.write_bytes(b"x" * (8 * 1024 * 1024 + 1))
            payload = json.loads(manifest.read_text(encoding="utf-8"))
            row = payload["downloads"][1]
            row["sizeBytes"] = windows.stat().st_size
            row["sha256"] = _sha256(windows)
            manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            output = root / "receipt.json"

            completed = _run(manifest, files_dir, output)

            self.assertEqual(completed.returncode, 1, completed.stdout)
            receipt = json.loads(output.read_text(encoding="utf-8"))
            self.assertTrue(
                any("installer size" in failure and "exceeds budget" in failure for failure in receipt["failures"]),
                receipt["failures"],
            )
            self.assertTrue(
                receipt["artifacts"]["avalonia-win-x64-installer"]["installer"]["manifest_identity_matches"]
            )

    def test_manifest_digest_mismatch_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest, files_dir = _write_fixture(root)
            payload = json.loads(manifest.read_text(encoding="utf-8"))
            payload["downloads"][0]["sha256"] = "0" * 64
            manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            output = root / "receipt.json"

            completed = _run(manifest, files_dir, output)

            self.assertEqual(completed.returncode, 1, completed.stdout)
            receipt = json.loads(output.read_text(encoding="utf-8"))
            self.assertTrue(
                any("sha256 does not match manifest" in failure for failure in receipt["failures"]),
                receipt["failures"],
            )

    def test_new_desktop_artifact_requires_an_explicit_budget(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest, files_dir = _write_fixture(root)
            extra = files_dir / "chummer-avalonia-osx-arm64-installer.pkg"
            extra.write_bytes(b"macos-fixture")
            payload = json.loads(manifest.read_text(encoding="utf-8"))
            payload["downloads"].append(
                {
                    "id": "avalonia-osx-arm64-installer",
                    "head": "avalonia",
                    "platformId": "macos",
                    "rid": "osx-arm64",
                    "arch": "arm64",
                    "kind": "installer",
                    "fileName": extra.name,
                    "sizeBytes": extra.stat().st_size,
                    "sha256": _sha256(extra),
                }
            )
            manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            output = root / "receipt.json"

            completed = _run(manifest, files_dir, output)

            self.assertEqual(completed.returncode, 1, completed.stdout)
            receipt = json.loads(output.read_text(encoding="utf-8"))
            self.assertTrue(
                any("unbudgeted=['avalonia-osx-arm64-installer']" in failure for failure in receipt["failures"]),
                receipt["failures"],
            )

    def test_check_only_does_not_mutate_a_receipt(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest, files_dir = _write_fixture(root)
            output = root / "receipt.json"

            completed = _run(manifest, files_dir, output, check_only=True)

            self.assertEqual(completed.returncode, 0, completed.stdout)
            self.assertFalse(output.exists())

    def test_desktop_and_root_release_gates_execute_the_budget(self) -> None:
        desktop_release = (
            REPO_ROOT / "scripts" / "release" / "verify_desktop_release_matrix.sh"
        ).read_text(encoding="utf-8")
        root_release = (
            WORKSPACE_ROOT / "scripts" / "release" / "verify_chummer6_desktop_gold.sh"
        ).read_text(encoding="utf-8")
        release_ready = (
            WORKSPACE_ROOT / "scripts" / "release" / "verify_chummer6_release_ready.sh"
        ).read_text(encoding="utf-8")
        release_ready_materializer = (
            WORKSPACE_ROOT
            / "chummer.run-services"
            / "scripts"
            / "materialize_release_ready_receipt.py"
        ).read_text(encoding="utf-8")

        self.assertIn(
            'python3 "$repo_root/scripts/verify_desktop_artifact_size_budget.py" --check-only',
            desktop_release,
        )
        self.assertIn(
            "verify_desktop_artifact_size_budget:python3 $root/chummer-presentation/scripts/verify_desktop_artifact_size_budget.py",
            root_release,
        )
        self.assertLess(
            root_release.index("verify_desktop_artifact_size_budget:"),
            root_release.index("verify_desktop_release_matrix:"),
        )
        self.assertIn("materialize_release_ready_receipt.py", release_ready)
        self.assertIn('"verify_chummer6_desktop_gold"', release_ready_materializer)
        self.assertIn(
            "verify_desktop_artifact_size_budget.py",
            release_ready_materializer,
        )

    def test_existing_startup_timestamps_are_not_treated_as_process_launch_latency(self) -> None:
        runtime_source = (
            REPO_ROOT / "Chummer.Desktop.Runtime" / "DesktopStartupSmokeRuntime.cs"
        ).read_text(encoding="utf-8")

        handler_start = runtime_source.index("public static async Task<int?> TryHandleAsync(")
        process_entry_check = runtime_source.index("StartupSmokeSwitch", handler_start)
        yield_point = runtime_source.index("await Task.Yield();", process_entry_check)
        timer_start = runtime_source.index(
            "DateTimeOffset startedAt = DateTimeOffset.UtcNow;", yield_point
        )
        receipt_completion = runtime_source.index(
            "CompletedAtUtc: DateTimeOffset.UtcNow", timer_start
        )

        self.assertLess(process_entry_check, yield_point)
        self.assertLess(yield_point, timer_start)
        self.assertLess(timer_start, receipt_completion)


if __name__ == "__main__":
    unittest.main()
