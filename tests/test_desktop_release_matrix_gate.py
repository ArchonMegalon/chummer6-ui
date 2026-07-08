from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_DESKTOP_RELEASE_MATRIX = REPO_ROOT / "scripts" / "release" / "verify_desktop_release_matrix.sh"


def test_desktop_release_matrix_verifies_windows_installer_payloads_against_public_downloads_tree() -> None:
    text = VERIFY_DESKTOP_RELEASE_MATRIX.read_text(encoding="utf-8")
    assert 'repo_root="/docker/chummercomplete/chummer-presentation"' in text
    assert 'cd "$repo_root"' in text
    assert "dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false" in text
    assert "dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "verify-windows-installer-payloads.py" in text
    assert 'public_downloads_root="/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads"' in text
    assert '--files-dir "$public_downloads_root/files"' in text
    assert '--manifest "$public_downloads_root/releases.json"' in text
    assert "--require-embedded-bootstrap-metadata" in text
    assert "payload_gate_args+=(--require-manifest-row)" in text
    assert "payload_gate_args+=(--allow-empty)" in text
    assert 'if python3 - "$public_downloads_root/releases.json"' in text
    assert "external_blockers" in text
    assert "windows review" in text
    assert "blocking mode" in text
    assert "blockedByExternalConstraintsOnly" in text
    assert "blocking_mode == 'external_only'" in text
    assert "local_count == 0" in text
    assert "external findings" in text
    assert "local findings" in text
