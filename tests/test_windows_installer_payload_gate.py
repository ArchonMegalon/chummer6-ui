from __future__ import annotations

import json
import struct
import subprocess
import zipfile
import hashlib
from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "verify-windows-installer-payloads.py"
PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
APPENDED_PAYLOAD_MAGIC = b"CHUMMER6PAYLOAD1"


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
        + f"ChummerInstallerPayloadUrl={payload_download_url}\n".encode("utf-8")
        + f"ChummerInstallerPayloadSha256={payload_sha256}\n".encode("utf-8")
        + f"ChummerInstallerPayloadSizeBytes={payload_size_bytes}\n".encode("utf-8")
        + (b"installer-padding" * 200)
    )


def _write_bundle_manifest(
    manifest_path: Path,
    *,
    installer_name: str,
    installer_sha256: str = "installer-sha-placeholder",
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
                "sizeBytes": 1,
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
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr


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
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr
