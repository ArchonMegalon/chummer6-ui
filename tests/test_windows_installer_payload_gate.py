from __future__ import annotations

import json
import struct
import subprocess
import zipfile
import hashlib
import shutil
import socket
import threading
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "verify-windows-installer-payloads.py"
VERIFY_RELEASES_MANIFEST_SCRIPT = REPO_ROOT / "scripts" / "verify-releases-manifest.sh"
RESOLVE_HUB_REGISTRY_ROOT_SCRIPT = REPO_ROOT / "scripts" / "resolve-hub-registry-root.sh"
CHECK_HOST_GATE_PREREQS_SCRIPT = REPO_ROOT / "scripts" / "check-host-gate-prereqs.sh"
RUNBOOK_SCRIPT = REPO_ROOT / "scripts" / "runbook.sh"
PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
HTTP_PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle-http.sh"
S3_PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh"
PUBLISH_LATEST_NIGHTLY_SCRIPT = REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh"
APPENDED_PAYLOAD_MAGIC = b"CHUMMER6PAYLOAD1"
BOOTSTRAP_METADATA_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"


def _publish_env(tmp_path: Path, **overrides: str) -> dict[str, str]:
    return {
        "PATH": "/usr/bin:/bin",
        "QUARANTINE_PROMOTION_EVIDENCE_PATH": str(tmp_path / "QUARANTINED_INSTALLER_PROMOTION.generated.json"),
        **overrides,
    }


def _write_bootstrap_payload(payload_path: Path, *, launch_executable: str = "Chummer.Avalonia.exe") -> bytes:
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(launch_executable, b"placeholder")
        archive.writestr("Samples/Legacy/Soma-Career.chum5", b"sample")
    return payload_path.read_bytes()


def _write_bootstrap_installer(
    installer_path: Path,
    *,
    payload_download_url: str,
    payload_sha256: str,
    payload_size_bytes: int,
) -> None:
    installer_path.write_bytes(
        b"installer-stub\n"
        + (b"installer-padding" * 200)
        + BOOTSTRAP_METADATA_MARKER
        + f"payloadDownloadUrl={payload_download_url}\n".encode("utf-8")
        + f"payloadSha256={payload_sha256}\n".encode("utf-8")
        + f"payloadSizeBytes={payload_size_bytes}\n".encode("utf-8")
    )


def _write_bundle_manifest(
    manifest_path: Path,
    *,
    installer_name: str,
    installer_sha256: str = "installer-sha-placeholder",
    installer_size_bytes: int = 1,
    payload_name: str = "",
    payload_sha256: str = "",
    payload_size_bytes: int = 0,
    installer_mode: str = "bootstrap",
    payload_download_url: str | None = None,
) -> None:
    payload = {
        "version": "run-test",
        "channel": "preview",
        "publishedAt": "2026-06-24T00:00:00Z",
        "downloads": [
            {
                "artifactId": "avalonia-win-x64-installer",
                "fileName": installer_name,
                "url": f"https://example.invalid/downloads/files/{installer_name}",
                "sha256": installer_sha256,
                "sizeBytes": installer_size_bytes,
                "kind": "installer",
                "platform": "windows",
                "installerMode": installer_mode,
                "payloadFileName": payload_name,
                "payloadDownloadUrl": payload_download_url or (f"https://example.invalid/downloads/files/{payload_name}" if payload_name else ""),
                "payloadSha256": payload_sha256,
                "payloadSizeBytes": payload_size_bytes,
            }
        ],
    }
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _write_windows_bootstrap_release_bundle(bundle_dir: Path, *, release_version: str = "run-test") -> None:
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": release_version,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )


def _write_root_release_blockers_receipt(path: Path, *, generated_at: str, blocker_ids: list[str]) -> None:
    path.write_text(
        json.dumps(
            {
                "generated_at": generated_at,
                "blockers": [{"blocker_id": blocker_id} for blocker_id in blocker_ids],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def _write_fake_registry_verifier(registry_root: Path, capture_path: Path) -> None:
    scripts_dir = registry_root / "scripts"
    scripts_dir.mkdir(parents=True, exist_ok=True)
    (scripts_dir / "verify_public_release_channel.py").write_text(
        "\n".join(
            [
                "#!/usr/bin/env python3",
                "from __future__ import annotations",
                "",
                "import json",
                "import sys",
                "from pathlib import Path",
                "",
                f"capture_path = Path({str(capture_path)!r})",
                "capture_path.write_text(json.dumps(sys.argv[1:], indent=2) + \"\\n\", encoding=\"utf-8\")",
                "print(\"fake registry verifier ok\")",
            ]
        )
        + "\n",
        encoding="utf-8",
    )


def _write_release_channel_manifest(manifest_path: Path, *, version: str, channel: str) -> None:
    manifest_path.write_text(
        json.dumps(
            {
                "channelId": channel,
                "channel": channel,
                "version": version,
                "publishedAt": "2026-07-08T00:00:00Z",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def _write_publish_ready_preview_release_bundle(
    bundle_dir: Path,
    tmp_path: Path,
    *,
    release_version: str = "run-test",
) -> tuple[Path, Path]:
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": release_version,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["version"] = release_version
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")

    progress_screenshot = tmp_path / "windows-installer-progress.png"
    completion_screenshot = tmp_path / "windows-installer-completion.png"
    progress_screenshot.write_bytes(b"progress-image")
    completion_screenshot.write_bytes(b"completion-image")

    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": recorded_at,
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                    "/account/work",
                    "/account/support",
                    "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    visual_proof_path = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    visual_proof_path.write_text(
        json.dumps(
            {
                "contract_name": "chummer6-ui.windows_installer_visual_proof",
                "contractName": "chummer6-ui.windows_installer_visual_proof",
                "status": "pass",
                "generated_at": recorded_at,
                "generatedAt": recorded_at,
                "recordedAtUtc": recorded_at,
                "channelId": "preview",
                "releaseVersion": release_version,
                "version": release_version,
                "headId": "avalonia",
                "head": "avalonia",
                "platform": "windows",
                "rid": "win-x64",
                "artifactDigest": f"sha256:{installer_sha256}",
                "screenshots": [
                    {
                        "role": "progress",
                        "path": str(progress_screenshot),
                        "sha256": hashlib.sha256(progress_screenshot.read_bytes()).hexdigest(),
                    },
                    {
                        "role": "completion",
                        "path": str(completion_screenshot),
                        "sha256": hashlib.sha256(completion_screenshot.read_bytes()).hexdigest(),
                    },
                ],
                "readabilityReview": {"status": "pass"},
                "contrastReview": {"status": "pass"},
                "clippingReview": {"status": "pass"},
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "wine64-linux-x64-container",
                "operatingSystem": "Microsoft Windows 10.0.19043",
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    return release_proof_path, visual_proof_path


def test_windows_installer_verifier_accepts_bootstrap_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


def test_windows_installer_verifier_rejects_bootstrap_installer_with_malformed_embedded_payload_url(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"\\{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=payload_url,
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer embedded payloadDownloadUrl must be an absolute file, http, or https URL" in result.stderr
    assert "bootstrap installer embedded payloadDownloadUrl does not match manifest/sidecar metadata" in result.stderr


def test_windows_installer_verifier_rejects_oversized_bootstrap_installer(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    with installer_path.open("ab") as handle:
        handle.truncate((15 * 1024 * 1024) + 1)
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer is too large" in result.stderr


def test_windows_installer_verifier_rejects_installer_without_manifest_row_when_required(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name="chummer-blazor-desktop-win-x64-installer.exe",
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
            "--require-manifest-row",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows installer is missing from the supplied release manifest" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_payload_without_sidecar_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"https://example.invalid/downloads/files/{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata is missing" in result.stderr


def test_windows_installer_verifier_rejects_mismatched_bootstrap_sidecar_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"https://example.invalid/downloads/files/{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": f"https://example.invalid/downloads/files/{payload_path.name}",
                "sha256": "wrong",
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata sha256 does not match payload bytes" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_installer_without_embedded_payload_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer does not contain embedded payloadDownloadUrl metadata" in result.stderr


def test_windows_installer_verifier_uses_sidecar_as_embedded_metadata_truth_before_manifest_exists(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer does not contain embedded payloadDownloadUrl metadata" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_sidecar_with_non_https_download_url_without_manifest(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"http://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata downloadUrl must be an absolute HTTPS URL" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_manifest_with_bad_payload_url(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"http://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=payload_url,
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir), "--manifest", str(manifest_path)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "manifest payloadDownloadUrl must be an absolute HTTPS URL" in result.stderr


def test_windows_installer_verifier_accepts_appended_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_zip_path = tmp_path / "payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_zip_path)
    installer_path.write_bytes(
        (b"installer-stub" * 200)
        + payload_bytes
        + struct.pack("<q", len(payload_bytes))
        + APPENDED_PAYLOAD_MAGIC
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


def test_windows_installer_verifier_rejects_missing_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_fails_before_promotion_when_windows_payload_is_missing(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_fails_closed_when_bundle_files_directory_is_missing(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    bundle_dir.mkdir()
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-win-x64-installer.exe")

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle is missing files directory:" in result.stderr
    assert "Expected desktop-download-bundle layout: releases.json + files/chummer-*" in result.stderr
    assert "Refusing to fall back to unrelated downloads/files roots unless CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK=true is set explicitly." in result.stderr


def test_check_host_gate_prereqs_rejects_invalid_nuget_endpoint_without_traceback(tmp_path: Path) -> None:
    result = subprocess.run(
        ["bash", str(CHECK_HOST_GATE_PREREQS_SCRIPT)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHECK_DOCKER": "0",
            "CHECK_NUGET": "1",
            "NUGET_ENDPOINT": "api.nuget.org:notaport",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "[FAIL] invalid NUGET_ENDPOINT value 'api.nuget.org:notaport' (expected host:port with numeric port 1-65535)." in result.stdout
    assert "Strict host gates are NOT ready." in result.stdout
    assert "Traceback" not in result.stdout
    assert "Traceback" not in result.stderr


def test_runbook_local_tests_rejects_invalid_nuget_endpoint_before_network_probe(tmp_path: Path) -> None:
    result = subprocess.run(
        ["bash", str(RUNBOOK_SCRIPT)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "RUNBOOK_MODE": "local-tests",
            "TEST_PROJECT": "Chummer.Tests/Chummer.Tests.csproj",
            "TEST_NUGET_ENDPOINT": "api.nuget.org:70000",
            "TEST_NUGET_SOFT_FAIL": "0",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid TEST_NUGET_ENDPOINT value: 'api.nuget.org:70000' (expected host:port with numeric port 1-65535)." in result.stderr
    assert "NuGet preflight failed for api.nuget.org:70000" not in result.stderr


def test_verify_releases_manifest_reports_missing_local_downloads_root_manifest(tmp_path: Path) -> None:
    downloads_dir = tmp_path / "downloads"
    downloads_dir.mkdir()
    (downloads_dir / "files").mkdir()

    result = subprocess.run(
        ["bash", str(VERIFY_RELEASES_MANIFEST_SCRIPT), str(downloads_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Local downloads shelf directory is missing releases.json: {downloads_dir / 'releases.json'}" in result.stderr
    assert "Traceback" not in result.stderr


def test_verify_releases_manifest_rejects_downloads_files_child_target(tmp_path: Path) -> None:
    downloads_dir = tmp_path / "downloads"
    files_dir = downloads_dir / "files"
    files_dir.mkdir(parents=True)

    result = subprocess.run(
        ["bash", str(VERIFY_RELEASES_MANIFEST_SCRIPT), str(files_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Verification target points at downloads files/ directory: {files_dir}" in result.stderr
    assert "Verify the downloads shelf root or its releases.json manifest, not its files/ child." in result.stderr
    assert "Traceback" not in result.stderr


def test_verify_releases_manifest_accepts_local_downloads_root_and_normalizes_to_releases_json(tmp_path: Path) -> None:
    downloads_dir = tmp_path / "downloads"
    files_dir = downloads_dir / "files"
    files_dir.mkdir(parents=True)
    manifest_path = downloads_dir / "releases.json"
    _write_bundle_manifest(manifest_path, installer_name="chummer-avalonia-win-x64-installer.exe")

    registry_root = tmp_path / "chummer-hub-registry"
    capture_path = tmp_path / "verify-args.json"
    _write_fake_registry_verifier(registry_root, capture_path)

    result = subprocess.run(
        ["bash", str(VERIFY_RELEASES_MANIFEST_SCRIPT), str(downloads_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_HUB_REGISTRY_ROOT": str(registry_root),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == "fake registry verifier ok"
    assert result.stderr == ""
    assert json.loads(capture_path.read_text(encoding="utf-8")) == [
        "--require-complete-desktop-coverage",
        str(manifest_path),
    ]


def test_verify_releases_manifest_propagates_optional_skip_startup_smoke_filter_without_required_coverage_flag(tmp_path: Path) -> None:
    downloads_dir = tmp_path / "downloads"
    files_dir = downloads_dir / "files"
    files_dir.mkdir(parents=True)
    manifest_path = downloads_dir / "releases.json"
    _write_bundle_manifest(manifest_path, installer_name="chummer-avalonia-win-x64-installer.exe")

    registry_root = tmp_path / "chummer-hub-registry"
    capture_path = tmp_path / "verify-args.json"
    _write_fake_registry_verifier(registry_root, capture_path)

    result = subprocess.run(
        ["bash", str(VERIFY_RELEASES_MANIFEST_SCRIPT), str(manifest_path)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_HUB_REGISTRY_ROOT": str(registry_root),
            "CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER": "true",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == "fake registry verifier ok"
    assert result.stderr == ""
    assert json.loads(capture_path.read_text(encoding="utf-8")) == [
        "--skip-startup-smoke-filter",
        str(manifest_path),
    ]


def test_resolve_hub_registry_root_rejects_missing_explicit_override(tmp_path: Path) -> None:
    missing_registry_root = tmp_path / "missing-registry"

    result = subprocess.run(
        ["bash", str(RESOLVE_HUB_REGISTRY_ROOT_SCRIPT)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_HUB_REGISTRY_ROOT": str(missing_registry_root),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Configured CHUMMER_HUB_REGISTRY_ROOT does not exist: {missing_registry_root}" in result.stderr


def test_resolve_hub_registry_root_rejects_non_registry_explicit_override(tmp_path: Path) -> None:
    fake_registry_root = tmp_path / "not-a-registry"
    fake_registry_root.mkdir()
    (fake_registry_root / "scripts").mkdir()

    result = subprocess.run(
        ["bash", str(RESOLVE_HUB_REGISTRY_ROOT_SCRIPT)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_HUB_REGISTRY_ROOT": str(fake_registry_root),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Configured CHUMMER_HUB_REGISTRY_ROOT is not a hub registry repo root: {fake_registry_root}" in result.stderr
    assert "Expected scripts/materialize_public_release_channel.py or scripts/verify_public_release_channel.py under that directory." in result.stderr


def test_resolve_hub_registry_root_accepts_explicit_registry_override(tmp_path: Path) -> None:
    registry_root = tmp_path / "chummer-hub-registry"
    scripts_dir = registry_root / "scripts"
    scripts_dir.mkdir(parents=True)
    (scripts_dir / "verify_public_release_channel.py").write_text("#!/usr/bin/env python3\n", encoding="utf-8")

    result = subprocess.run(
        ["bash", str(RESOLVE_HUB_REGISTRY_ROOT_SCRIPT)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_HUB_REGISTRY_ROOT": str(registry_root),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == str(registry_root.resolve())
    assert result.stderr == ""


def test_resolve_hub_registry_root_discovers_sibling_registry_repo_from_workspace_root(tmp_path: Path) -> None:
    workspace_root = tmp_path / "workspace"
    repo_root = workspace_root / "chummer-presentation-sr6-origin-dialog-clean"
    repo_scripts = repo_root / "scripts"
    repo_scripts.mkdir(parents=True)
    resolver_copy = repo_scripts / RESOLVE_HUB_REGISTRY_ROOT_SCRIPT.name
    resolver_copy.write_text(RESOLVE_HUB_REGISTRY_ROOT_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    registry_root = workspace_root / "chummer-hub-registry"
    registry_scripts = registry_root / "scripts"
    registry_scripts.mkdir(parents=True)
    (registry_scripts / "materialize_public_release_channel.py").write_text("#!/usr/bin/env python3\n", encoding="utf-8")

    result = subprocess.run(
        ["bash", str(resolver_copy)],
        cwd=repo_root,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == str(registry_root.resolve())
    assert result.stderr == ""


def test_check_host_gate_prereqs_reports_ready_when_nuget_probe_succeeds_and_docker_check_disabled(tmp_path: Path) -> None:
    ready = threading.Event()
    accepted = threading.Event()

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.bind(("127.0.0.1", 0))
        server.listen(1)
        host, port = server.getsockname()

        def accept_once() -> None:
            ready.set()
            conn, _ = server.accept()
            with conn:
                accepted.set()

        thread = threading.Thread(target=accept_once, daemon=True)
        thread.start()
        ready.wait(timeout=1)

        result = subprocess.run(
            ["bash", str(CHECK_HOST_GATE_PREREQS_SCRIPT)],
            cwd=REPO_ROOT,
            env={
                **_publish_env(tmp_path),
                "CHECK_DOCKER": "0",
                "CHECK_NUGET": "1",
                "NUGET_ENDPOINT": f"{host}:{port}",
            },
            text=True,
            capture_output=True,
            check=False,
        )

        thread.join(timeout=1)

    assert accepted.is_set()
    assert result.returncode == 0, result.stderr
    assert "[SKIP] docker prerequisite check disabled." in result.stdout
    assert f"[PASS] nuget endpoint reachable: {host}:{port}" in result.stdout
    assert "Strict host gates are ready." in result.stdout


def test_publish_download_bundle_fails_when_root_installer_has_no_matching_payload_sidecar(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = bundle_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    (files_dir / "chummer-avalonia-win-x64.zip").write_bytes(b"portable-placeholder")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_rejects_nested_files_stage_layout(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    nested_files_dir = files_dir / "files"
    nested_files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    (nested_files_dir / "chummer-avalonia-win-x64-payload.zip").write_bytes(b"payload-placeholder")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle is malformed: found nested files directory under" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_download_bundle_http_rejects_nested_files_stage_layout(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    nested_files_dir = files_dir / "files"
    nested_files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    (nested_files_dir / "chummer-avalonia-win-x64-payload.zip").write_bytes(b"payload-placeholder")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    result = subprocess.run(
        ["bash", str(HTTP_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle is malformed: found nested files directory under" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_download_bundle_http_rejects_invalid_upload_url_before_dry_run_output(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(HTTP_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_RELEASE_UPLOAD_DRY_RUN": "1",
            "CHUMMER_RELEASE_UPLOAD_URL": "not-a-url",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_RELEASE_UPLOAD_URL: 'not-a-url' (expected absolute http:// or https:// URL)." in result.stderr
    assert "Dry run only." not in result.stdout


def test_publish_download_bundle_http_rejects_invalid_verify_url_before_dry_run_output(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(HTTP_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_RELEASE_UPLOAD_DRY_RUN": "1",
            "CHUMMER_RELEASE_UPLOAD_URL": "https://example.invalid/api/internal/releases/bundles",
            "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL": "https://example.invalid/api/internal/releases/upload-sessions",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "bad verify",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL: 'bad verify' (expected absolute http:// or https:// URL)." in result.stderr
    assert "Dry run only." not in result.stdout


def test_publish_download_bundle_http_rejects_invalid_sessions_url_before_dry_run_output(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(HTTP_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_RELEASE_UPLOAD_DRY_RUN": "1",
            "CHUMMER_RELEASE_UPLOAD_URL": "https://example.invalid/api/internal/releases/bundles",
            "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL": "not-a-url",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_RELEASE_UPLOAD_SESSIONS_URL: 'not-a-url' (expected absolute http:// or https:// URL)." in result.stderr
    assert "Dry run only." not in result.stdout


def test_publish_download_bundle_s3_rejects_invalid_target_uri_before_manifest_regeneration(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_S3_URI: 'bucket/path' (expected s3://bucket/path URI)." in result.stderr
    assert "missing startup-smoke receipt directory" not in result.stderr


def test_publish_download_bundle_s3_rejects_invalid_verify_url_before_manifest_regeneration(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "s3://bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "bad verify",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL: 'bad verify' (expected absolute http:// or https:// URL)." in result.stderr
    assert "missing startup-smoke receipt directory" not in result.stderr


def test_publish_download_bundle_s3_rejects_invalid_latest_uri_before_manifest_regeneration(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "s3://bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI": "latest-bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI: 'latest-bucket/path' (expected s3://bucket/path URI)." in result.stderr
    assert "missing startup-smoke receipt directory" not in result.stderr


def test_publish_download_bundle_s3_rejects_invalid_endpoint_url_before_manifest_regeneration(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name="chummer-avalonia-linux-x64-installer.deb")
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name="chummer-avalonia-linux-x64-installer.deb")

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "s3://bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json",
            "CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL": "not-a-url",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL: 'not-a-url' (expected absolute http:// or https:// URL)." in result.stderr
    assert "aws CLI is required" not in result.stderr


def test_publish_download_bundle_rejects_invalid_live_verify_url_before_deploy_mutation(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    (files_dir / "chummer-avalonia-linux-x64-installer.deb").write_bytes(b"deb")
    (bundle_dir / "releases.json").write_text(
        json.dumps(
            {
                "version": "run-test",
                "channel": "preview",
                "publishedAt": "2026-06-24T00:00:00Z",
                "downloads": [],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED": "true",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "bad verify",
            "CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS": "0",
            "CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH": "1",
            "CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "true",
            "RELEASE_VERSION": "run-test",
            "RELEASE_PUBLISHED_AT": "2026-06-24T00:00:00Z",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL: 'bad verify' (expected absolute http:// or https:// URL)." in result.stderr
    assert "missing startup-smoke receipt directory" not in result.stderr
    assert not (deploy_dir / "releases.json").exists()
    assert not (deploy_dir / "RELEASE_CHANNEL.generated.json").exists()


def test_publish_latest_nightly_rejects_invalid_public_edge_verify_base_url_before_stage_or_publish_work(tmp_path: Path) -> None:
    fake_workspace = tmp_path / "workspace"
    fake_repo = fake_workspace / "chummer-presentation-sr6-origin-dialog-clean"
    fake_scripts = fake_repo / "scripts"
    fake_scripts.mkdir(parents=True)
    script_copy = fake_scripts / PUBLISH_LATEST_NIGHTLY_SCRIPT.name
    script_copy.write_text(PUBLISH_LATEST_NIGHTLY_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    deploy_dir = fake_workspace / "chummer.run-services" / "Chummer.Portal" / "downloads"
    env = {
        "PATH": "/usr/bin:/bin",
        "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR": str(deploy_dir),
        "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "true",
        "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL": "bad base",
    }

    result = subprocess.run(
        ["bash", str(script_copy)],
        cwd=fake_repo,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL: 'bad base' (expected absolute http:// or https:// URL)." in result.stderr
    assert "Nightly staging root not found" not in result.stderr
    assert not deploy_dir.exists()


def test_publish_latest_nightly_rejects_invalid_public_edge_forwarded_proto_before_stage_or_publish_work(tmp_path: Path) -> None:
    fake_workspace = tmp_path / "workspace"
    fake_repo = fake_workspace / "chummer-presentation-sr6-origin-dialog-clean"
    fake_scripts = fake_repo / "scripts"
    fake_scripts.mkdir(parents=True)
    script_copy = fake_scripts / PUBLISH_LATEST_NIGHTLY_SCRIPT.name
    script_copy.write_text(PUBLISH_LATEST_NIGHTLY_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    deploy_dir = fake_workspace / "chummer.run-services" / "Chummer.Portal" / "downloads"
    env = {
        "PATH": "/usr/bin:/bin",
        "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR": str(deploy_dir),
        "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "true",
        "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL": "http://127.0.0.1:8091",
        "CHUMMER_PUBLIC_EDGE_VERIFY_HOST": "chummer.run",
        "CHUMMER_PUBLIC_EDGE_VERIFY_PROTO": "gopher",
    }

    result = subprocess.run(
        ["bash", str(script_copy)],
        cwd=fake_repo,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PUBLIC_EDGE_VERIFY_PROTO: 'gopher' (expected 'http' or 'https')." in result.stderr
    assert "Nightly staging root not found" not in result.stderr
    assert not deploy_dir.exists()


def test_publish_latest_nightly_rejects_invalid_public_edge_host_header_before_stage_or_publish_work(tmp_path: Path) -> None:
    fake_workspace = tmp_path / "workspace"
    fake_repo = fake_workspace / "chummer-presentation-sr6-origin-dialog-clean"
    fake_scripts = fake_repo / "scripts"
    fake_scripts.mkdir(parents=True)
    script_copy = fake_scripts / PUBLISH_LATEST_NIGHTLY_SCRIPT.name
    script_copy.write_text(PUBLISH_LATEST_NIGHTLY_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    deploy_dir = fake_workspace / "chummer.run-services" / "Chummer.Portal" / "downloads"
    env = {
        "PATH": "/usr/bin:/bin",
        "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR": str(deploy_dir),
        "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "true",
        "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL": "http://127.0.0.1:8091",
        "CHUMMER_PUBLIC_EDGE_VERIFY_HOST": "https://chummer.run",
        "CHUMMER_PUBLIC_EDGE_VERIFY_PROTO": "https",
    }

    result = subprocess.run(
        ["bash", str(script_copy)],
        cwd=fake_repo,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PUBLIC_EDGE_VERIFY_HOST: 'https://chummer.run' (expected bare host header value)." in result.stderr
    assert "Nightly staging root not found" not in result.stderr
    assert not deploy_dir.exists()


def test_publish_latest_nightly_refuses_stable_channel_before_stage_selection(tmp_path: Path) -> None:
    missing_staging_root = tmp_path / "missing-nightly-stage"
    deploy_dir = tmp_path / "deploy"

    result = subprocess.run(
        ["bash", str(PUBLISH_LATEST_NIGHTLY_SCRIPT), str(deploy_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_STAGING_ROOT": str(missing_staging_root),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
            "CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL": "public_stable",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." in result.stderr
    assert "Nightly staging root not found" not in result.stderr


def test_publish_download_bundle_s3_rejects_nested_files_stage_layout(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    nested_files_dir = files_dir / "files"
    nested_files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    (nested_files_dir / "chummer-avalonia-win-x64-payload.zip").write_bytes(b"payload-placeholder")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(bundle_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle is malformed: found nested files directory under" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_download_bundle_s3_reports_missing_bundle_root(tmp_path: Path) -> None:
    missing_bundle_dir = tmp_path / "missing-bundle"

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(missing_bundle_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Bundle directory not found: {missing_bundle_dir}" in result.stderr


def test_publish_download_bundle_rejects_files_child_stage_root(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(files_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle root points at files/ directory:" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_latest_nightly_reports_missing_staging_root(tmp_path: Path) -> None:
    missing_staging_root = tmp_path / "missing-staging"
    deploy_dir = tmp_path / "deploy"

    result = subprocess.run(
        ["bash", str(PUBLISH_LATEST_NIGHTLY_SCRIPT), str(deploy_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_STAGING_ROOT": str(missing_staging_root),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Nightly staging root not found: {missing_staging_root}" in result.stderr


def test_publish_latest_nightly_accepts_direct_stage_root_before_child_search(tmp_path: Path) -> None:
    stage_dir = tmp_path / "nightly-run-direct"
    files_dir = stage_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(stage_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(stage_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    missing_handoff_script = tmp_path / "missing-handoff.py"
    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_LATEST_NIGHTLY_SCRIPT), str(deploy_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_STAGING_ROOT": str(stage_dir),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
            "CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH": str(missing_handoff_script),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Missing release build handoff materializer: {missing_handoff_script}" in result.stderr
    assert "No publishable nightly stage found under" not in result.stderr


def test_publish_latest_nightly_rejects_files_child_stage_root(tmp_path: Path) -> None:
    stage_dir = tmp_path / "nightly-run-direct"
    files_dir = stage_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(stage_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(stage_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_LATEST_NIGHTLY_SCRIPT), str(deploy_dir)],
        cwd=REPO_ROOT,
        env={
            **_publish_env(tmp_path),
            "CHUMMER_STAGING_ROOT": str(files_dir),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert f"Nightly staging root points at files/ directory: {files_dir}" in result.stderr
    assert "Build the nightly stage root, not its files/ child, before publishing." in result.stderr


def test_publish_latest_nightly_publishes_preview_child_stage_through_stubbed_lane(tmp_path: Path) -> None:
    fake_workspace = tmp_path / "workspace"
    fake_repo = fake_workspace / "chummer-presentation-sr6-origin-dialog-clean"
    fake_scripts = fake_repo / "scripts"
    fake_scripts.mkdir(parents=True)
    script_copy = fake_scripts / PUBLISH_LATEST_NIGHTLY_SCRIPT.name
    script_copy.write_text(PUBLISH_LATEST_NIGHTLY_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    staging_root = fake_workspace / "_staging"
    ignored_stage = staging_root / "nightly-run-000-stable"
    ignored_stage_files = ignored_stage / "files"
    ignored_stage_files.mkdir(parents=True)
    _write_bundle_manifest(ignored_stage / "releases.json", installer_name="ignored-installer.exe")
    _write_release_channel_manifest(
        ignored_stage / "RELEASE_CHANNEL.generated.json",
        version="ignored-stable-2026-07-08",
        channel="public_stable",
    )

    selected_stage = staging_root / "nightly-run-111-preview"
    selected_stage_files = selected_stage / "files"
    selected_stage_files.mkdir(parents=True)
    _write_bundle_manifest(selected_stage / "releases.json", installer_name="selected-installer.exe")
    _write_release_channel_manifest(
        selected_stage / "RELEASE_CHANNEL.generated.json",
        version="nightly-2026-07-08",
        channel="preview",
    )

    publish_capture_path = tmp_path / "publish-capture.json"
    (fake_scripts / "materialize_release_candidate_handoff.py").write_text(
        "\n".join(
            [
                "#!/usr/bin/env python3",
                "from __future__ import annotations",
                "",
                "import json",
                "import sys",
                "from pathlib import Path",
                "",
                "stage_dir = Path(sys.argv[1])",
                "(stage_dir / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(",
                "    json.dumps({'status': 'refreshed', 'stage': str(stage_dir)}, indent=2) + '\\n',",
                "    encoding='utf-8',",
                ")",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-windows-installer-payloads.py").write_text(
        "print('windows_installer_payload_gate:ok checked=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-release-stage-artifact-scope.py").write_text(
        "print('release_stage_artifact_scope:ok checked_files=0 checked_receipts=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-windows-bootstrap-startup-smoke.py").write_text(
        "print('windows_startup_smoke_gate:ok checked=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "materialize-windows-desktop-exit-gate.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                ': > "${CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH:?}"',
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (fake_scripts / "publish-download-bundle.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                'stage_dir="$1"',
                'deploy_dir="$2"',
                f'capture_path={str(publish_capture_path)!r}',
                'python3 - "$stage_dir" "$deploy_dir" "$RELEASE_CHANNEL" "$CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER" "$CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE" "$capture_path" <<\'PY\'',
                "from __future__ import annotations",
                "",
                "import json",
                "import sys",
                "from pathlib import Path",
                "",
                "stage_dir, deploy_dir, release_channel, skip_filter, require_coverage, capture_path = sys.argv[1:]",
                "Path(capture_path).write_text(",
                "    json.dumps(",
                "        {",
                "            'stage_dir': stage_dir,",
                "            'deploy_dir': deploy_dir,",
                "            'release_channel': release_channel,",
                "            'skip_startup_smoke_filter': skip_filter,",
                "            'require_complete_desktop_coverage': require_coverage,",
                "        },",
                "        indent=2,",
                "    ) + '\\n',",
                "    encoding='utf-8',",
                ")",
                "PY",
                'mkdir -p "$deploy_dir"',
                'cp "$stage_dir/RELEASE_CHANNEL.generated.json" "$deploy_dir/RELEASE_CHANNEL.generated.json"',
                'cp "$stage_dir/releases.json" "$deploy_dir/releases.json"',
                'echo "stub publish complete"',
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(script_copy), str(deploy_dir)],
        cwd=fake_repo,
        env={
            "PATH": "/usr/bin:/bin",
            "CHUMMER_STAGING_ROOT": str(staging_root),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
            "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "false",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "true",
            "CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "1",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "ALLOW manual force override" in result.stdout
    assert f"Publishing latest nightly stage: {selected_stage}" in result.stdout
    assert f"Target downloads shelf: {deploy_dir}" in result.stdout
    assert "Public release channel: preview" in result.stdout
    assert "Verified published downloads shelf version: nightly-2026-07-08" in result.stdout
    assert "Published latest nightly to downloads shelf." in result.stdout
    assert not (ignored_stage / "RELEASE_BUILD_HANDOFF.generated.json").exists()
    assert (selected_stage / "RELEASE_BUILD_HANDOFF.generated.json").exists()
    assert json.loads(publish_capture_path.read_text(encoding="utf-8")) == {
        "stage_dir": str(selected_stage),
        "deploy_dir": str(deploy_dir),
        "release_channel": "preview",
        "skip_startup_smoke_filter": "true",
        "require_complete_desktop_coverage": "1",
    }
    published_manifest = json.loads((deploy_dir / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    assert published_manifest["version"] == "nightly-2026-07-08"
    assert published_manifest["channel"] == "preview"


def test_publish_latest_nightly_requires_actionable_windows_visual_proof_handoff_for_preview_continuation(tmp_path: Path) -> None:
    fake_workspace = tmp_path / "workspace"
    fake_repo = fake_workspace / "chummer-presentation-sr6-origin-dialog-clean"
    fake_scripts = fake_repo / "scripts"
    fake_scripts.mkdir(parents=True)
    script_copy = fake_scripts / PUBLISH_LATEST_NIGHTLY_SCRIPT.name
    script_copy.write_text(PUBLISH_LATEST_NIGHTLY_SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    staging_root = fake_workspace / "_staging"
    selected_stage = staging_root / "nightly-run-111-preview"
    selected_stage_files = selected_stage / "files"
    selected_stage_files.mkdir(parents=True)
    _write_bundle_manifest(selected_stage / "releases.json", installer_name="selected-installer.exe")
    _write_release_channel_manifest(
        selected_stage / "RELEASE_CHANNEL.generated.json",
        version="nightly-2026-07-08",
        channel="preview",
    )

    publish_capture_path = tmp_path / "publish-capture.json"
    (fake_scripts / "materialize_release_candidate_handoff.py").write_text(
        "\n".join(
            [
                "#!/usr/bin/env python3",
                "from __future__ import annotations",
                "",
                "import json",
                "import sys",
                "from pathlib import Path",
                "",
                "stage_dir = Path(sys.argv[1])",
                "handoff_path = stage_dir / 'WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json'",
                "payload = {",
                "  'status': 'needs_review',",
                "  'summary': 'Windows startup smoke receipt version does not match the current Windows release candidate.',",
                "  'json_path': str(handoff_path),",
                "  'blockers': ['Windows startup smoke receipt version does not match the current Windows release candidate.'],",
                "  'next_actions': ['Refresh the staged Windows startup smoke before attempting preview publication.']",
                "}",
                "handoff_path.write_text(json.dumps(payload, indent=2) + '\\n', encoding='utf-8')",
                "(stage_dir / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(json.dumps({'windows_visual_proof_handoff': payload}, indent=2) + '\\n', encoding='utf-8')",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-windows-installer-payloads.py").write_text(
        "print('windows_installer_payload_gate:ok checked=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-release-stage-artifact-scope.py").write_text(
        "print('release_stage_artifact_scope:ok checked_files=0 checked_receipts=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "verify-windows-bootstrap-startup-smoke.py").write_text(
        "print('windows_startup_smoke_gate:ok checked=0')\n",
        encoding="utf-8",
    )
    (fake_scripts / "materialize-windows-desktop-exit-gate.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                ': > "${CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH:?}"',
                "exit 1",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (fake_scripts / "publish-download-bundle.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                f"printf '%s\\n' called > {str(publish_capture_path)!r}",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(script_copy), str(deploy_dir)],
        cwd=fake_repo,
        env={
            "PATH": "/usr/bin:/bin",
            "CHUMMER_STAGING_ROOT": str(staging_root),
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
            "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "false",
            "CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH": "1",
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows visual proof handoff:" in result.stderr
    assert "Windows visual proof status: needs_review" in result.stderr
    assert "Nightly stage is carrying a Windows visual proof handoff instead of a passable Windows visual proof." not in result.stderr
    assert "Nightly stage failed Windows desktop exit gate preflight. Use the Windows visual proof handoff above before publishing." in result.stderr
    assert not publish_capture_path.exists()


def test_publish_download_bundle_http_rejects_files_child_stage_root(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    result = subprocess.run(
        ["bash", str(HTTP_PUBLISH_SCRIPT), str(files_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle root points at files/ directory:" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_download_bundle_s3_rejects_files_child_stage_root(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)
    _write_bundle_manifest(bundle_dir / "RELEASE_CHANNEL.generated.json", installer_name=installer_path.name)

    result = subprocess.run(
        ["bash", str(S3_PUBLISH_SCRIPT), str(files_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Bundle root points at files/ directory:" in result.stderr
    assert "Publish from the stage or bundle root, not its files/ child." in result.stderr


def test_publish_download_bundle_promotes_bootstrap_payload_zip_with_installer(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    progress_screenshot = tmp_path / "windows-installer-progress.png"
    completion_screenshot = tmp_path / "windows-installer-completion.png"
    progress_screenshot.write_bytes(b"progress-image")
    completion_screenshot.write_bytes(b"completion-image")
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                    "/account/work",
                    "/account/support",
                    "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    visual_proof_path = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    visual_proof_path.write_text(
        json.dumps(
            {
                "contract_name": "chummer6-ui.windows_installer_visual_proof",
                "contractName": "chummer6-ui.windows_installer_visual_proof",
                "status": "pass",
                "generated_at": recorded_at,
                "generatedAt": recorded_at,
                "recordedAtUtc": recorded_at,
                "channelId": "preview",
                "releaseVersion": "run-test",
                "version": "run-test",
                "headId": "avalonia",
                "head": "avalonia",
                "platform": "windows",
                "rid": "win-x64",
                "artifactDigest": f"sha256:{installer_sha256}",
                "screenshots": [
                    {
                        "role": "progress",
                        "path": str(progress_screenshot),
                        "sha256": hashlib.sha256(progress_screenshot.read_bytes()).hexdigest(),
                    },
                    {
                        "role": "completion",
                        "path": str(completion_screenshot),
                        "sha256": hashlib.sha256(completion_screenshot.read_bytes()).hexdigest(),
                    },
                ],
                "readabilityReview": {"status": "pass"},
                "contrastReview": {"status": "pass"},
                "clippingReview": {"status": "pass"},
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "wine64-linux-x64-container",
                "operatingSystem": "Microsoft Windows 10.0.19043",
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=str(visual_proof_path),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert (deploy_dir / "files" / installer_path.name).is_file()
    assert (deploy_dir / "files" / payload_path.name).is_file()
    assert (deploy_dir / "files" / payload_sidecar.name).is_file()


def test_publish_download_bundle_keeps_detached_worktree_clean_for_sibling_live_shelf_in_auto_mode(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    release_proof_path, visual_proof_path = _write_publish_ready_preview_release_bundle(bundle_dir, tmp_path)

    workspace_root = tmp_path / "workspace"
    worktree_path = workspace_root / "ui-worktree"
    deploy_dir = workspace_root / "chummer.run-services" / "Chummer.Portal" / "downloads"
    sibling_presentation_mirror = workspace_root / "chummer-presentation" / "Docker" / "Downloads"
    sibling_registry_mirror = workspace_root / "chummer-hub-registry" / ".codex-studio" / "published"
    workspace_root.mkdir()

    add_result = subprocess.run(
        ["git", "-C", str(REPO_ROOT), "worktree", "add", "--detach", str(worktree_path), "HEAD"],
        text=True,
        capture_output=True,
        check=False,
    )
    assert add_result.returncode == 0, add_result.stderr
    shutil.copy2(REPO_ROOT / "scripts" / "publish-download-bundle.sh", worktree_path / "scripts" / "publish-download-bundle.sh")
    shutil.copy2(REPO_ROOT / "scripts" / "generate-releases-manifest.sh", worktree_path / "scripts" / "generate-releases-manifest.sh")

    baseline_status_result = subprocess.run(
        ["git", "-C", str(worktree_path), "status", "--short"],
        text=True,
        capture_output=True,
        check=False,
    )
    assert baseline_status_result.returncode == 0, baseline_status_result.stderr
    baseline_status = baseline_status_result.stdout.strip()

    try:
        result = subprocess.run(
            ["bash", str(worktree_path / "scripts" / "publish-download-bundle.sh"), str(bundle_dir), str(deploy_dir)],
            cwd=worktree_path,
            env=_publish_env(
                tmp_path,
                CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
                CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
                CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=str(visual_proof_path),
                RELEASE_PROOF_PATH=str(release_proof_path),
            ),
            text=True,
            capture_output=True,
            check=False,
        )

        assert result.returncode == 0, result.stderr
        combined_output = f"{result.stdout}\n{result.stderr}"
        assert "public-edge mirror" not in combined_output
        assert (deploy_dir / "RELEASE_CHANNEL.generated.json").is_file()
        assert (deploy_dir / "PUBLICATION_SCOPE.generated.json").is_file()
        assert not sibling_presentation_mirror.exists()
        assert not sibling_registry_mirror.exists()

        status_result = subprocess.run(
            ["git", "-C", str(worktree_path), "status", "--short"],
            text=True,
            capture_output=True,
            check=False,
        )
        assert status_result.returncode == 0, status_result.stderr
        assert status_result.stdout.strip() == baseline_status
    finally:
        remove_result = subprocess.run(
            ["git", "-C", str(REPO_ROOT), "worktree", "remove", "--force", str(worktree_path)],
            text=True,
            capture_output=True,
            check=False,
        )
        assert remove_result.returncode == 0, remove_result.stderr


def test_publish_download_bundle_does_not_require_gnu_realpath_dash_m_for_preview_sync(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    deploy_dir = tmp_path / "deploy"
    release_proof_path, visual_proof_path = _write_publish_ready_preview_release_bundle(bundle_dir, tmp_path)

    fake_bin = tmp_path / "fake-bin"
    fake_bin.mkdir()
    fake_realpath = fake_bin / "realpath"
    fake_realpath.write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "if [[ \"${1:-}\" == \"-m\" ]]; then",
                "  echo \"realpath: illegal option -- m\" >&2",
                "  exit 1",
                "fi",
                "exec /usr/bin/realpath \"$@\"",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    fake_realpath.chmod(0o755)

    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            PATH=f"{fake_bin}:/usr/bin:/bin",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=str(visual_proof_path),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert (deploy_dir / "RELEASE_CHANNEL.generated.json").is_file()
    assert (deploy_dir / "files" / "chummer-avalonia-win-x64-installer.exe").is_file()
    assert "realpath: illegal option -- m" not in result.stderr


def test_publish_download_bundle_refreshes_windows_visual_proof_handoff_before_exit_gate_failure(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                    "/account/work",
                    "/account/support",
                    "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "wine64-linux-x64-container",
                "operatingSystem": "Microsoft Windows 10.0.19043",
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    handoff_stub = tmp_path / "handoff_stub.py"
    handoff_stub.write_text(
        "\n".join(
            [
                "from __future__ import annotations",
                "import json, sys",
                "from pathlib import Path",
                "root = Path(sys.argv[1])",
                "handoff_path = root / 'WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json'",
                "payload = {",
                "  'status': 'ready_for_windows_host',",
                "  'summary': 'Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.',",
                "  'json_path': str(handoff_path),",
                "  'next_actions': ['Run the stage-local Windows visual capture lane.']",
                "}",
                "handoff_path.write_text(json.dumps(payload, indent=2) + '\\n', encoding='utf-8')",
                "(root / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(json.dumps({'windows_visual_proof_handoff': payload}, indent=2) + '\\n', encoding='utf-8')",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH=str(handoff_stub),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows visual proof handoff:" in result.stderr
    assert str(deploy_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json") in result.stderr
    assert "Windows visual proof status: ready_for_windows_host" in result.stderr
    assert "Windows visual proof next action: Run the stage-local Windows visual capture lane." in result.stderr


def test_publish_download_bundle_requires_actionable_windows_visual_proof_handoff_for_preview_continuation(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    deploy_dir = tmp_path / "deploy"
    release_proof_path, visual_proof_path = _write_publish_ready_preview_release_bundle(bundle_dir, tmp_path)
    visual_proof_path.unlink()

    handoff_stub = tmp_path / "handoff_stub.py"
    handoff_stub.write_text(
        "\n".join(
            [
                "from __future__ import annotations",
                "import json, sys",
                "from pathlib import Path",
                "root = Path(sys.argv[1])",
                "handoff_path = root / 'WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json'",
                "payload = {",
                "  'status': 'needs_review',",
                "  'summary': 'Windows startup smoke receipt version does not match the current Windows release candidate.',",
                "  'json_path': str(handoff_path),",
                "  'blockers': ['Windows startup smoke receipt version does not match the current Windows release candidate.'],",
                "  'next_actions': ['Refresh the staged Windows startup smoke before attempting preview publication.']",
                "}",
                "handoff_path.write_text(json.dumps(payload, indent=2) + '\\n', encoding='utf-8')",
                "(root / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(json.dumps({'windows_visual_proof_handoff': payload}, indent=2) + '\\n', encoding='utf-8')",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH=str(handoff_stub),
            CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH="1",
            CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=str(visual_proof_path),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows visual proof handoff:" in result.stderr
    assert "Windows visual proof status: needs_review" in result.stderr
    assert "Published preview downloads shelf is carrying a Windows visual proof handoff instead of a passable Windows visual proof." not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." in result.stderr


def test_stable_publish_download_bundle_refuses_non_posture_root_blockers(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    _write_root_release_blockers_receipt(
        root_release_blockers,
        generated_at=datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        blocker_ids=[
            "release_posture:non_flagship_channel",
            "release_truth:windows_installer_visual_audit",
        ],
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication is blocked by root release truth." in result.stderr
    assert "release_truth:windows_installer_visual_audit" in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr


def test_stable_publish_download_bundle_refuses_stale_root_release_truth(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    _write_root_release_blockers_receipt(
        root_release_blockers,
        generated_at="2000-01-01T00:00:00Z",
        blocker_ids=["release_posture:non_flagship_channel"],
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication requires fresh root release blocker truth." in result.stderr
    assert "max_age_seconds=86400" in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr
    assert not (deploy_dir / "releases.json").exists()


def test_stable_publish_download_bundle_rejects_invalid_blocker_max_age_env(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    _write_root_release_blockers_receipt(
        root_release_blockers,
        generated_at=datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        blocker_ids=["release_posture:non_flagship_channel"],
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="not-a-number",
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS value: 'not-a-number' (expected integer max age in seconds)." in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr
    assert not (deploy_dir / "releases.json").exists()


def test_stable_publish_download_bundle_rejects_negative_blocker_max_age_env(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    _write_root_release_blockers_receipt(
        root_release_blockers,
        generated_at=datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        blocker_ids=["release_posture:non_flagship_channel"],
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="-1",
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Invalid CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS value: '-1' (expected integer max age in seconds)." in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr
    assert not (deploy_dir / "releases.json").exists()


def test_stable_publish_download_bundle_requires_generated_at_in_root_release_truth(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    root_release_blockers.write_text(
        json.dumps(
            {
                "blockers": [{"blocker_id": "release_posture:non_flagship_channel"}],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication requires fresh root release blocker truth, but generated_at is missing:" in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr
    assert not (deploy_dir / "releases.json").exists()


def test_stable_publish_download_bundle_requires_parseable_generated_at_in_root_release_truth(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    _write_windows_bootstrap_release_bundle(bundle_dir, release_version="run-stable-test")

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    _write_root_release_blockers_receipt(
        root_release_blockers,
        generated_at="not-a-timestamp",
        blocker_ids=["release_posture:non_flagship_channel"],
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication requires parseable generated_at in root release blocker truth:" in result.stderr
    assert "'not-a-timestamp'" in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr
    assert not (deploy_dir / "releases.json").exists()


def test_publish_download_bundle_fails_when_windows_bootstrap_receipt_payload_proof_is_wrong(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                    "/account/work",
                    "/account/support",
                    "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "wine64-linux-x64-container",
                "operatingSystem": "Microsoft Windows 10.0.19043",
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": "wrong-payload-sha",
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in result.stderr
