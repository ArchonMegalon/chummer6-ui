from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"


def _write_json(path: Path, payload: object) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _build_fixture(
    tmp_path: Path,
    *,
    payload_sha256: str = "abc123",
    receipt_payload_sha256: str | None = None,
    payload_acquisition_mode: str = "download",
) -> tuple[Path, Path, Path, Path]:
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
                    **(
                        {"payloadAcquisitionMode": payload_acquisition_mode}
                        if payload_acquisition_mode != "download"
                        else {}
                    ),
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
                    **(
                        {"payloadAcquisitionMode": payload_acquisition_mode}
                        if payload_acquisition_mode != "download"
                        else {}
                    ),
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
            "executionEnvironment": "wine_compatibility",
            "verificationScope": "windows_compatibility_startup",
            "nativeHostEvidence": {
                "contractName": "chummer6-ui.native_windows_host_evidence",
                "status": "not_native",
                "isNativeWindows": False,
                "hostPlatform": "linux",
                "hostKernel": "Linux",
                "runner": "wine64",
                "evidenceSource": "wine_runner_selection",
            },
            "bootstrapPayloadAcquisitionMode": payload_acquisition_mode,
            "bootstrapPayloadFileName": payload_path.name,
            "bootstrapPayloadSha256": receipt_payload_sha256 or payload_sha256,
            "bootstrapPayloadSizeBytes": payload_size,
            "recordedAtUtc": recorded_at,
        },
    )
    progress_lines = [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
    ]
    if payload_acquisition_mode == "embedded":
        progress_lines.extend(
            [
                "Payload acquisition mode: embedded",
                "Payload acquisition target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
                "Using embedded payload C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
            ]
        )
    else:
        progress_lines.extend(
            [
                "Payload download target: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Downloading application files - 12% - 5.4 / 45.0 MiB - 5.4 MiB/s",
                "Payload download completed with bundled curl",
                "Downloading application files - 100% - 45.0 / 45.0 MiB - 4.8 MiB/s",
            ]
        )
    progress_lines.extend(
        [
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
        ]
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(progress_lines)
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


def test_windows_bootstrap_startup_smoke_verifier_accepts_expected_embedded_mode(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(
        tmp_path,
        payload_acquisition_mode="embedded",
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

    assert result.returncode == 0, result.stderr
    assert "windows-bootstrap-startup-smoke:ok checked=1" in result.stdout


def test_windows_bootstrap_startup_smoke_verifier_rejects_download_receipt_for_embedded_artifact(
    tmp_path: Path,
) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(
        tmp_path,
        payload_acquisition_mode="embedded",
    )
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["bootstrapPayloadAcquisitionMode"] = "download"
    _write_json(receipt_path, receipt)

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
    assert "expected payload acquisition mode embedded" in result.stderr


def test_windows_bootstrap_startup_smoke_verifier_accepts_compound_platform_id(
    tmp_path: Path,
) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    releases_payload = json.loads(releases_manifest.read_text(encoding="utf-8"))
    downloads_row = releases_payload["downloads"][0]
    downloads_row["platform"] = "Avalonia Desktop Windows X64 Installer"
    downloads_row["platformId"] = "windows-x64"
    downloads_row["arch"] = "x64"
    downloads_row.pop("rid")
    _write_json(releases_manifest, releases_payload)

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


def test_windows_bootstrap_startup_smoke_verifier_rejects_mismatched_compound_platform_id(
    tmp_path: Path,
) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    releases_payload = json.loads(releases_manifest.read_text(encoding="utf-8"))
    downloads_row = releases_payload["downloads"][0]
    downloads_row["platform"] = "Avalonia Desktop Windows X64 Installer"
    downloads_row["platformId"] = "windows-arm64"
    downloads_row["arch"] = "x64"
    downloads_row.pop("rid")
    _write_json(releases_manifest, releases_payload)

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
    assert "releases.json omits the matching installer row" in result.stderr


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


def _run_verifier(
    release_channel_manifest: Path,
    releases_manifest: Path,
    files_dir: Path,
    startup_smoke_dir: Path,
    *extra_args: str,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
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
            *extra_args,
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )


def _set_release_channel(manifest_path: Path, channel: str) -> None:
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    payload["channel"] = channel
    _write_json(manifest_path, payload)


def _set_native_windows_evidence(startup_smoke_dir: Path) -> None:
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["executionEnvironment"] = "native_windows"
    receipt["verificationScope"] = "native_windows_startup"
    receipt["nativeHostEvidence"] = {
        "contractName": "chummer6-ui.native_windows_host_evidence",
        "status": "verified",
        "isNativeWindows": True,
        "hostPlatform": "windows",
        "hostKernel": "MINGW64_NT-10.0",
        "runner": "powershell.exe",
        "evidenceSource": "powershell_runtime_os_probe",
    }
    _write_json(receipt_path, receipt)


def test_preview_accepts_explicit_wine_compatibility_but_reports_non_native_scope(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 0, result.stderr


def test_explicit_native_requirement_rejects_wine_compatibility(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)

    result = _run_verifier(
        release_channel_manifest,
        releases_manifest,
        files_dir,
        startup_smoke_dir,
        "--require-native-windows",
    )

    assert result.returncode == 1
    assert "Native Windows startup proof is required; compatibility execution cannot satisfy this release" in result.stderr


def test_stable_release_automatically_rejects_wine_compatibility(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    _set_release_channel(release_channel_manifest, "public_stable")
    _set_release_channel(releases_manifest, "public_stable")
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["channel"] = "public_stable"
    _write_json(receipt_path, receipt)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 1
    assert "Native Windows startup proof is required; compatibility execution cannot satisfy this release" in result.stderr


def test_stable_release_accepts_consistent_native_windows_evidence(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    _set_release_channel(release_channel_manifest, "public_stable")
    _set_release_channel(releases_manifest, "public_stable")
    _set_native_windows_evidence(startup_smoke_dir)
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["channel"] = "public_stable"
    _write_json(receipt_path, receipt)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 0, result.stderr


def test_missing_execution_environment_fails_closed_even_for_preview(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt.pop("executionEnvironment")
    receipt.pop("nativeHostEvidence")
    _write_json(receipt_path, receipt)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 1
    assert "executionEnvironment is missing or unsupported" in result.stderr


def test_inconsistent_native_evidence_cannot_disguise_wine_as_native(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    _set_native_windows_evidence(startup_smoke_dir)
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["nativeHostEvidence"]["runner"] = "wine64"
    _write_json(receipt_path, receipt)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 1
    assert "cannot classify Wine as native Windows" in result.stderr


def test_native_claim_with_linux_host_kernel_fails_closed(tmp_path: Path) -> None:
    release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir = _build_fixture(tmp_path)
    _set_native_windows_evidence(startup_smoke_dir)
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["nativeHostEvidence"]["hostKernel"] = "Linux"
    _write_json(receipt_path, receipt)

    result = _run_verifier(release_channel_manifest, releases_manifest, files_dir, startup_smoke_dir)

    assert result.returncode == 1
    assert "native Windows evidence has a non-Windows host kernel" in result.stderr
