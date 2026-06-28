from __future__ import annotations

import json
import struct
import subprocess
import zipfile
import hashlib
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "verify-windows-installer-payloads.py"
PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
APPENDED_PAYLOAD_MAGIC = b"CHUMMER6PAYLOAD1"
BOOTSTRAP_METADATA_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"


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
        env={
            "PATH": "/usr/bin:/bin",
            "CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS": "false",
            "CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "true",
            "CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH": str(visual_proof_path),
            "RELEASE_PROOF_PATH": str(release_proof_path),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert (deploy_dir / "files" / installer_path.name).is_file()
    assert (deploy_dir / "files" / payload_path.name).is_file()
    assert (deploy_dir / "files" / payload_sidecar.name).is_file()


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
        env={
            "PATH": "/usr/bin:/bin",
            "CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS": "false",
            "CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "true",
            "CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH": str(handoff_stub),
            "RELEASE_PROOF_PATH": str(release_proof_path),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows visual proof handoff:" in result.stderr
    assert str(deploy_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json") in result.stderr
    assert "Windows visual proof status: ready_for_windows_host" in result.stderr
    assert "Windows visual proof next action: Run the stage-local Windows visual capture lane." in result.stderr


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
        env={
            "PATH": "/usr/bin:/bin",
            "CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS": "false",
            "CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "true",
            "RELEASE_PROOF_PATH": str(release_proof_path),
        },
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in result.stderr
