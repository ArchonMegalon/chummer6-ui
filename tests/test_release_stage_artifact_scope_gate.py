from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "verify-release-stage-artifact-scope.py"


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def base_manifest() -> dict:
    return {
        "version": "run-test",
        "channel": "preview",
        "artifacts": [
            {
                "artifactId": "avalonia-linux-x64-installer",
                "fileName": "chummer-avalonia-linux-x64-installer.deb",
                "platform": "linux",
                "kind": "installer",
            },
            {
                "artifactId": "avalonia-win-x64-installer",
                "fileName": "chummer-avalonia-win-x64-installer.exe",
                "platform": "windows",
                "kind": "installer",
                "installerMode": "bootstrap",
                "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
            },
        ],
    }


def run_scope_gate(stage: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest",
            str(stage / "RELEASE_CHANNEL.generated.json"),
            "--files-dir",
            str(stage / "files"),
            "--startup-smoke-dir",
            str(stage / "startup-smoke"),
        ],
        text=True,
        capture_output=True,
        check=False,
    )


def write_valid_stage(stage: Path) -> None:
    files_dir = stage / "files"
    startup_smoke_dir = stage / "startup-smoke"
    files_dir.mkdir(parents=True)
    startup_smoke_dir.mkdir(parents=True)
    write_json(stage / "RELEASE_CHANNEL.generated.json", base_manifest())

    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"linux")
    (files_dir / "chummer-avalonia-win-x64-installer.exe").write_bytes(b"windows")
    (files_dir / "chummer-avalonia-win-x64-payload.zip").write_bytes(b"payload")
    write_json(
        files_dir / "chummer-avalonia-win-x64-payload.zip.json",
        {"fileName": "chummer-avalonia-win-x64-payload.zip"},
    )

    write_json(
        startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json",
        {"status": "pass", "artifactFileName": "chummer-avalonia-linux-x64-installer.deb"},
    )
    write_json(
        startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {"status": "pass", "artifactFileName": "chummer-avalonia-win-x64-installer.exe"},
    )


def test_release_stage_artifact_scope_accepts_manifest_artifacts_and_windows_payload_sidecars(
    tmp_path: Path,
) -> None:
    write_valid_stage(tmp_path)

    result = run_scope_gate(tmp_path)

    assert result.returncode == 0, result.stderr
    assert "release_stage_artifact_scope:ok" in result.stdout
    assert "checked_files=4" in result.stdout
    assert "checked_receipts=2" in result.stdout


def test_release_stage_artifact_scope_rejects_unmanifested_desktop_artifacts_and_receipts(
    tmp_path: Path,
) -> None:
    write_valid_stage(tmp_path)
    (tmp_path / "files" / "chummer-avalonia-osx-arm64-installer.dmg").write_bytes(b"macos")
    write_json(
        tmp_path / "startup-smoke" / "startup-smoke-avalonia-osx-arm64.receipt.json",
        {"status": "pass", "artifactFileName": "chummer-avalonia-osx-arm64-installer.dmg"},
    )

    result = run_scope_gate(tmp_path)

    assert result.returncode == 1
    assert "release_stage_artifact_scope:fail" in result.stderr
    assert "unmanifested staged desktop artifact: files/chummer-avalonia-osx-arm64-installer.dmg" in result.stderr
    assert (
        "startup-smoke receipt references an unmanifested artifact: "
        "startup-smoke-avalonia-osx-arm64.receipt.json -> chummer-avalonia-osx-arm64-installer.dmg"
    ) in result.stderr


def test_latest_nightly_publisher_runs_release_stage_scope_gate_before_windows_gates() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "verify_latest_stage_artifact_scope_gate()" in publisher
    assert "verify-release-stage-artifact-scope.py" in publisher
    assert "Nightly stage failed release artifact scope preflight." in publisher
    assert 'verify_latest_stage_artifact_scope_gate "$latest_stage"' in publisher
    assert publisher.index('verify_latest_stage_artifact_scope_gate "$latest_stage"') < publisher.index(
        'verify_latest_stage_windows_payload_gate "$latest_stage"'
    )
    assert 'local publication_dir="$stage_dir/publication"' in publisher
    assert "--require-windows-only-publication-scope" in publisher
    assert '--publication-scope "$scope_receipt"' in publisher
    assert '--publication-proposal "$scope_proposal"' in publisher
    assert 'CHUMMER_WINDOWS_ONLY_PUBLICATION_STAGE_ROOT="$latest_stage"' in publisher
    assert '"$latest_stage/publication" "$DEPLOY_DIR"' in publisher
