from __future__ import annotations

import json
import subprocess
from pathlib import Path


RUN_SERVICES_DOWNLOADS = Path("/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads")
PRESENTATION_CANONICAL_DOWNLOADS = Path("/docker/chummercomplete/chummer-presentation/Docker/Downloads")
PRESENTATION_DOWNLOADS = Path("/docker/chummercomplete/chummer-presentation/Chummer.Portal/downloads")
PRESENTATION_GENERATOR = Path("/docker/chummercomplete/chummer-presentation/scripts/generate-releases-manifest.sh")
PRESENTATION_PAYLOAD_GATE = Path("/docker/chummercomplete/chummer-presentation/scripts/verify-windows-installer-payloads.py")


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
    text = PRESENTATION_GENERATOR.read_text(encoding="utf-8")
    assert 'for collection_name in ("artifacts", "downloads")' in text
    assert '"payloadFileName": payload_name' in text
    assert '"payloadDownloadUrl": payload_url' in text
    assert '"payloadSha256": payload_sha256' in text
    assert '"payloadSizeBytes": payload_size' in text
    assert '[[ "$file_name" == "${promoted_file_name%-installer.exe}-payload.zip" ]]' in text
    assert "windows installer payload artifact missing from all local/registry sources" in text


def test_public_downloads_windows_payload_metadata_tracks_promoted_surface() -> None:
    row = _row_by_file_name(RUN_SERVICES_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")
    sidecar_path = RUN_SERVICES_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json"
    if row is None:
        assert not sidecar_path.exists()
        return

    assert row["installerMode"] == "bootstrap"
    assert row["payloadFileName"] == "chummer-avalonia-win-x64-payload.zip"
    active_pointer = _read_manifest(RUN_SERVICES_DOWNLOADS, "current.json")
    generation_id = active_pointer["generationId"]
    assert row["payloadDownloadUrl"] == (
        f"/downloads/g/{generation_id}/files/"
        "chummer-avalonia-win-x64-payload.zip"
    )
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
    canonical_rows = _download_rows(PRESENTATION_CANONICAL_DOWNLOADS)
    portal_rows = _download_rows(PRESENTATION_DOWNLOADS)
    assert [row["fileName"] for row in portal_rows] == [row["fileName"] for row in canonical_rows]
    canonical_windows_row = _row_by_file_name(PRESENTATION_CANONICAL_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")
    portal_windows_row = _row_by_file_name(PRESENTATION_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe")

    if canonical_windows_row is None:
        assert portal_windows_row is None
        assert not (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-installer.exe").exists()
        assert not (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip").exists()
        assert not (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json").exists()
        return

    assert portal_windows_row is not None
    assert portal_windows_row["installerMode"] == canonical_windows_row["installerMode"]
    assert portal_windows_row["payloadFileName"] == canonical_windows_row["payloadFileName"]
    assert portal_windows_row["payloadDownloadUrl"] == canonical_windows_row["payloadDownloadUrl"]
    assert portal_windows_row["payloadSha256"] == canonical_windows_row["payloadSha256"]
    assert int(portal_windows_row["payloadSizeBytes"]) == int(canonical_windows_row["payloadSizeBytes"])
    assert (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-installer.exe").exists()
    assert (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip").exists()
    assert (PRESENTATION_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json").exists()


def test_presentation_portal_downloads_tree_passes_windows_payload_gate_for_current_surface() -> None:
    args = [
        "python3",
        str(PRESENTATION_PAYLOAD_GATE),
        "--files-dir",
        str(PRESENTATION_DOWNLOADS / "files"),
        "--manifest",
        str(PRESENTATION_DOWNLOADS / "releases.json"),
        "--manifest",
        str(PRESENTATION_DOWNLOADS / "RELEASE_CHANNEL.generated.json"),
        "--require-embedded-bootstrap-metadata",
    ]
    if _row_by_file_name(PRESENTATION_DOWNLOADS, "chummer-avalonia-win-x64-installer.exe") is None:
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
