from __future__ import annotations

import hashlib
import json
import os
import subprocess
import tarfile
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"


def _artifact_under_host_like_root(tmp_path: Path) -> Path:
    artifact = tmp_path / "host-profile" / "Downloads" / "files" / "chummer-avalonia-win-arm64-installer.exe"
    artifact.parent.mkdir(parents=True)
    artifact.write_bytes(b"portable-receipt-fixture")
    return artifact


def _run_smoke(
    artifact: Path,
    output_dir: Path,
    rid: str,
    *,
    launch_target: str = "Chummer.Avalonia.exe",
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "bash",
            str(SCRIPT),
            str(artifact),
            "avalonia",
            rid,
            launch_target,
            str(output_dir),
            "run-portable-fixture",
        ],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
        env={
            **os.environ,
            "PROCESSOR_ARCHITECTURE": "AMD64",
            "PROCESSOR_ARCHITEW6432": "",
        },
    )


def _failing_linux_archive(tmp_path: Path, log_line: str) -> Path:
    archive_root = tmp_path / "archive-root"
    launch_path = archive_root / "Chummer.Avalonia"
    archive_root.mkdir()
    launch_path.write_text(
        "#!/usr/bin/env bash\n"
        f"printf '%s\\n' '{log_line}'\n"
        "exit 1\n",
        encoding="utf-8",
    )
    launch_path.chmod(0o755)
    artifact = tmp_path / "fixture-user" / "chummer-avalonia-linux-x64.tar.gz"
    artifact.parent.mkdir()
    with tarfile.open(artifact, "w:gz") as archive:
        archive.add(launch_path, arcname=launch_path.name)
    return artifact


def test_incompatible_host_receipt_uses_portable_artifact_relationship(tmp_path: Path) -> None:
    artifact = _artifact_under_host_like_root(tmp_path)
    output_dir = tmp_path / "host-profile" / "proofs"

    result = _run_smoke(artifact, output_dir, "win-arm64")

    assert result.returncode == 0, result.stderr
    receipt_path = output_dir / "startup-smoke-avalonia-win-arm64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    expected_relative_path = f"files/{artifact.name}"
    assert receipt["artifactPath"] == expected_relative_path
    assert receipt["artifactRelativePath"] == expected_relative_path
    assert receipt["artifactFileName"] == artifact.name
    assert receipt["artifactPathDisclosure"] == "artifact_shelf_relative_path"
    assert str(tmp_path) not in json.dumps(receipt)


def test_failure_packet_uses_portable_artifact_and_receipt_references(tmp_path: Path) -> None:
    artifact = _artifact_under_host_like_root(tmp_path)
    output_dir = tmp_path / "host-profile" / "proofs"

    result = _run_smoke(artifact, output_dir, "unsupported-x64")

    assert result.returncode != 0
    packet_path = output_dir / "release-regression-avalonia-unsupported-x64.json"
    packet = json.loads(packet_path.read_text(encoding="utf-8"))
    expected_relative_path = f"files/{artifact.name}"
    expected_receipt_name = "startup-smoke-avalonia-unsupported-x64.receipt.json"
    assert packet["artifactPath"] == expected_relative_path
    assert packet["artifactRelativePath"] == expected_relative_path
    assert packet["artifactFileName"] == artifact.name
    assert packet["artifactPathDisclosure"] == "artifact_shelf_relative_path"
    assert packet["startupReceiptPath"] == expected_receipt_name
    assert packet["startupReceiptName"] == expected_receipt_name
    assert packet["startupReceiptPathDisclosure"] == "file_name_only"
    assert str(tmp_path) not in json.dumps(packet)


@pytest.mark.parametrize(
    ("profile_path", "redaction_marker"),
    [
        pytest.param(
            "/home/fixture-user/.cache/chummer/runtime.log",
            "<redacted:linux-user-profile>/",
            id="linux-profile",
        ),
        pytest.param(
            "/Users/fixture-user/Library/Caches/Chummer/runtime.log",
            "<redacted:macos-user-profile>/",
            id="macos-profile",
        ),
        pytest.param(
            r"C:\Users\fixture-user\AppData\Local\Chummer\runtime.log",
            "<redacted:windows-user-profile>/",
            id="windows-profile",
        ),
    ],
)
def test_failure_packet_redacts_profile_paths_after_fingerprinting_raw_log(
    tmp_path: Path,
    profile_path: str,
    redaction_marker: str,
) -> None:
    artifact = _failing_linux_archive(tmp_path, profile_path)
    output_dir = tmp_path / "proofs"

    result = _run_smoke(
        artifact,
        output_dir,
        "linux-x64",
        launch_target="Chummer.Avalonia",
    )

    assert result.returncode != 0
    packet_path = output_dir / "release-regression-avalonia-linux-x64.json"
    packet = json.loads(packet_path.read_text(encoding="utf-8"))
    assert packet["artifactPath"] == artifact.name
    assert packet["artifactRelativePath"] == artifact.name
    assert packet["artifactPathDisclosure"] == "file_name_only"
    assert packet["logTailRedaction"] == "known_user_profile_paths"
    assert packet["logTailRedactionApplied"] is True
    published_tail = "\n".join(packet["logTail"])
    assert profile_path not in published_tail
    assert "fixture-user" not in published_tail
    assert redaction_marker in published_tail

    raw_fingerprint_source = "|".join(["avalonia", "linux-x64", "1", "", profile_path])
    expected_fingerprint = hashlib.sha256(raw_fingerprint_source.encode("utf-8")).hexdigest()[:16]
    assert packet["crashFingerprint"] == expected_fingerprint
