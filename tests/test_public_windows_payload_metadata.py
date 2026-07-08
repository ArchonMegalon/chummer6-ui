from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
CANONICAL_DOWNLOADS = REPO_ROOT / "Docker" / "Downloads"
PORTAL_DOWNLOADS = REPO_ROOT / "Chummer.Portal" / "downloads"
GENERATOR = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"
PAYLOAD_GATE = REPO_ROOT / "scripts" / "verify-windows-installer-payloads.py"


def _read_manifest(root: Path, name: str = "releases.json") -> dict:
    return json.loads((root / name).read_text(encoding="utf-8-sig"))


def _download_rows(root: Path) -> list[dict]:
    payload = _read_manifest(root)
    return [item for item in payload.get("downloads", []) if isinstance(item, dict)]


def _row_by_file_name(root: Path, file_name: str) -> dict | None:
    for item in _download_rows(root):
        if str(item.get("fileName") or "").strip() == file_name:
            return item
    return None


def test_manifest_generator_enriches_windows_payload_metadata_for_artifacts_and_downloads() -> None:
    text = GENERATOR.read_text(encoding="utf-8")
    assert 'for collection_name in ("artifacts", "downloads")' in text
    assert '"payloadFileName": payload_name' in text
    assert '"payloadDownloadUrl": payload_url' in text
    assert '"payloadSha256": payload_sha256' in text
    assert '"payloadSizeBytes": payload_size' in text


def test_canonical_downloads_windows_payload_metadata_tracks_promoted_surface() -> None:
    row = _row_by_file_name(CANONICAL_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")
    sidecar_path = CANONICAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json"
    if row is None:
        assert not sidecar_path.exists()
        return

    assert row["installerMode"] == "bootstrap"
    assert row["payloadFileName"] == "chummer-avalonia-win-x64-payload.zip"
    assert row["payloadDownloadUrl"] == "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip"
    assert isinstance(row["payloadSha256"], str) and len(row["payloadSha256"]) == 64
    assert int(row["payloadSizeBytes"]) > 0
    payload = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
    assert payload["contractName"] == "chummer6-ui.windows_bootstrap_payload"
    assert payload["fileName"] == "chummer-avalonia-win-x64-payload.zip"
    assert payload["installerFileName"] == "chummer-avalonia-win-x64-installer.exe"
    assert payload["downloadUrl"] == "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip"
    assert isinstance(payload["sha256"], str) and len(payload["sha256"]) == 64
    assert int(payload["sizeBytes"]) > 0


def test_presentation_portal_downloads_surface_matches_current_canonical_snapshot() -> None:
    canonical_rows = _download_rows(CANONICAL_DOWNLOADS)
    portal_rows = _download_rows(PORTAL_DOWNLOADS)
    assert [row["fileName"] for row in portal_rows] == [row["fileName"] for row in canonical_rows]
    canonical_windows_row = _row_by_file_name(CANONICAL_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")
    portal_windows_row = _row_by_file_name(PORTAL_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")

    if canonical_windows_row is None:
        assert portal_windows_row is None
        assert not (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-installer.exe").exists()
        assert not (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip").exists()
        assert not (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json").exists()
        return

    assert portal_windows_row is not None
    assert portal_windows_row["installerMode"] == canonical_windows_row["installerMode"]
    assert portal_windows_row["payloadFileName"] == canonical_windows_row["payloadFileName"]
    assert str(portal_windows_row["payloadDownloadUrl"]).endswith(f"/{portal_windows_row['payloadFileName']}")
    assert str(canonical_windows_row["payloadDownloadUrl"]).endswith(f"/{canonical_windows_row['payloadFileName']}")
    assert isinstance(portal_windows_row["payloadSha256"], str) and len(portal_windows_row["payloadSha256"]) == 64
    assert isinstance(canonical_windows_row["payloadSha256"], str) and len(canonical_windows_row["payloadSha256"]) == 64
    assert int(portal_windows_row["payloadSizeBytes"]) > 0
    assert int(canonical_windows_row["payloadSizeBytes"]) > 0
    if str(portal_windows_row.get("releaseVersion") or "") == str(canonical_windows_row.get("releaseVersion") or ""):
        assert portal_windows_row["payloadDownloadUrl"] == canonical_windows_row["payloadDownloadUrl"]
        assert portal_windows_row["payloadSha256"] == canonical_windows_row["payloadSha256"]
        assert int(portal_windows_row["payloadSizeBytes"]) == int(canonical_windows_row["payloadSizeBytes"])
    assert (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-installer.exe").exists()
    assert (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip").exists()
    assert (PORTAL_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json").exists()


def test_presentation_portal_downloads_tree_passes_windows_payload_gate_for_current_surface() -> None:
    args = [
        "python3",
        str(PAYLOAD_GATE),
        "--files-dir",
        str(PORTAL_DOWNLOADS / "files"),
        "--manifest",
        str(PORTAL_DOWNLOADS / "releases.json"),
        "--manifest",
        str(PORTAL_DOWNLOADS / "RELEASE_CHANNEL.generated.json"),
        "--require-embedded-bootstrap-metadata",
    ]
    if _row_by_file_name(PORTAL_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe") is None:
        args.append("--allow-empty")
    else:
        args.append("--require-manifest-row")

    result = subprocess.run(
        args,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    if "--allow-empty" in args:
        assert "windows_installer_payload_gate:ok no_windows_installers" in result.stdout
    else:
        assert "windows_installer_payload_gate:ok checked=1" in result.stdout
