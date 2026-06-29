from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"


def _write_json(path: Path, payload: object) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _build_fixture(tmp_path: Path, *, payload_sha256: str = "abc123", receipt_payload_sha256: str | None = None) -> tuple[Path, Path, Path, Path]:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    startup_smoke_dir = tmp_path / "startup-smoke"
    startup_smoke_dir.mkdir()

    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"bootstrap-installer-bytes")
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_path.write_bytes(b"payload-zip-bytes")

    installer_sha256 = __import__("hashlib").sha256(installer_path.read_bytes()).hexdigest()
    payload_size = payload_path.stat().st_size
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    release_channel_manifest = tmp_path / "RELEASE_CHANNEL.generated.json"
    _write_json(
        release_channel_manifest,
        {
            "version": "0.0.0.1",
            "channel": "nightly",
            "artifacts": [
                {
                    "platform": "windows",
                    "kind": "installer",
                    "head": "avalonia",
                    "rid": "win-x64",
                    "fileName": installer_path.name,
                    "installerMode": "bootstrap",
                    "payloadFileName": payload_path.name,
                    "payloadSha256": payload_sha256,
                    "payloadSizeBytes": payload_size,
                }
            ],
        },
    )

    releases_manifest = tmp_path / "releases.json"
    _write_json(
        releases_manifest,
        {
            "version": "0.0.0.1",
            "channel": "nightly",
            "downloads": [
                {
                    "platform": "windows",
                    "kind": "installer",
                    "head": "avalonia",
                    "rid": "win-x64",
                    "fileName": installer_path.name,
                    "installerMode": "bootstrap",
                }
            ],
        },
    )

    _write_json(
        startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {
            "status": "pass",
            "readyCheckpoint": "pre_ui_event_loop",
            "headId": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "arch": "x64",
            "releaseVersion": "0.0.0.1",
            "channel": "nightly",
            "artifactDigest": f"sha256:{installer_sha256}",
            "artifactFileName": installer_path.name,
            "bootstrapPayloadAcquisitionMode": "download",
            "bootstrapPayloadFileName": payload_path.name,
            "bootstrapPayloadSha256": receipt_payload_sha256 or payload_sha256,
            "bootstrapPayloadSizeBytes": payload_size,
            "recordedAtUtc": recorded_at,
        },
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download completed with bundled curl",
                "Downloading application files - 100% - 45.0 / 45.0 MiB - 4.8 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    return release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir


def test_windows_bootstrap_startup_smoke_verifier_passes_for_matching_bundle(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows-bootstrap-startup-smoke:ok checked=1" in result.stdout


def test_windows_bootstrap_startup_smoke_verifier_fails_when_receipt_is_missing(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").unlink()

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows installer startup-smoke receipt is missing" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_payload_sha_does_not_match(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(
        tmp_path,
        payload_sha256="expected-payload-sha",
        receipt_payload_sha256="wrong-payload-sha",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_progress_log_has_root_level_payload_target(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: \\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke progress log captured a root-level payload target" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_payload_target_is_outside_bootstrap_root(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\wrong-temp\\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke progress log payload target is outside the bootstrap temp root" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_payload_target_name_does_not_match_release(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\wrong-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke progress log payload target file name does not match release metadata" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_progress_log_contains_payload_download_failure(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download failed: Unable to open \\chummer-avalonia-win-x64-payload.zip",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke progress log contains failure marker 'Payload download failed:'" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_fails_when_progress_log_lacks_percent_and_speed_lines(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - working",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--release-channel",
            str(release_channel_manifest),
            "--downloads-manifest",
            str(releases_manifest),
            "--files-dir",
            str(files_dir),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 1
    assert "Windows bootstrap installer startup-smoke progress log is missing a percent-and-speed download line" in result.stderr
