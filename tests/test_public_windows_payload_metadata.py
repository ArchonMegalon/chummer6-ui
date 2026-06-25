from __future__ import annotations

import json
import subprocess
from pathlib import Path


RUN_SERVICES_DOWNLOADS = Path("/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads")
PRESENTATION_DOWNLOADS = Path("/docker/chummercomplete/chummer-presentation/Chummer.Portal/downloads")
PRESENTATION_GENERATOR = Path("/docker/chummercomplete/chummer-presentation/scripts/generate-releases-manifest.sh")
PRESENTATION_PAYLOAD_GATE = Path("/docker/chummercomplete/chummer-presentation/scripts/verify-windows-installer-payloads.py")


def test_manifest_generator_enriches_windows_payload_metadata_for_artifacts_and_downloads() -> None:
    text = PRESENTATION_GENERATOR.read_text(encoding="utf-8")
    assert 'for collection_name in ("artifacts", "downloads")' in text
    assert '"payloadFileName": payload_name' in text
    assert '"payloadDownloadUrl": payload_url' in text
    assert '"payloadSha256": payload_sha256' in text
    assert '"payloadSizeBytes": payload_size' in text


def test_public_downloads_manifest_exposes_windows_payload_metadata() -> None:
    payload = json.loads((RUN_SERVICES_DOWNLOADS / "releases.json").read_text(encoding="utf-8-sig"))
    row = next(
        item
        for item in payload.get("downloads", [])
        if isinstance(item, dict) and item.get("fileName") == "chummer-avalonia-win-x64-installer.exe"
    )
    assert row["installerMode"] == "bootstrap"
    assert row["payloadFileName"] == "chummer-avalonia-win-x64-payload.zip"
    assert row["payloadDownloadUrl"] == "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip"
    assert isinstance(row["payloadSha256"], str) and len(row["payloadSha256"]) == 64
    assert int(row["payloadSizeBytes"]) > 0


def test_public_downloads_tree_contains_windows_payload_sidecar_metadata() -> None:
    sidecar_path = RUN_SERVICES_DOWNLOADS / "files" / "chummer-avalonia-win-x64-payload.zip.json"
    payload = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
    assert payload["contractName"] == "chummer6-ui.windows_bootstrap_payload"
    assert payload["fileName"] == "chummer-avalonia-win-x64-payload.zip"
    assert payload["installerFileName"] == "chummer-avalonia-win-x64-installer.exe"
    assert payload["downloadUrl"] == "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip"
    assert isinstance(payload["sha256"], str) and len(payload["sha256"]) == 64
    assert int(payload["sizeBytes"]) > 0


def test_presentation_portal_downloads_tree_passes_windows_payload_gate() -> None:
    result = subprocess.run(
        [
            "python3",
            str(PRESENTATION_PAYLOAD_GATE),
            "--files-dir",
            str(PRESENTATION_DOWNLOADS / "files"),
            "--manifest",
            str(PRESENTATION_DOWNLOADS / "releases.json"),
            "--manifest",
            str(PRESENTATION_DOWNLOADS / "RELEASE_CHANNEL.generated.json"),
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout
