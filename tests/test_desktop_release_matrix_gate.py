from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_DESKTOP_RELEASE_MATRIX = REPO_ROOT / "scripts" / "release" / "verify_desktop_release_matrix.sh"


def test_desktop_release_matrix_verifies_windows_installer_payloads_against_public_downloads_tree() -> None:
    text = VERIFY_DESKTOP_RELEASE_MATRIX.read_text(encoding="utf-8")
    assert "dotnet build /docker/chummercomplete/chummer-presentation/Chummer.Tests/Chummer.Tests.csproj -v minimal" in text
    assert "dotnet test --project /docker/chummercomplete/chummer-presentation/Chummer.Tests/Chummer.Tests.csproj" in text
    assert "--no-build" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "verify-windows-installer-payloads.py" in text
    assert "--files-dir /docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/files" in text
    assert "--manifest /docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/releases.json" in text
    assert "--allow-empty" not in text
    assert "external_blockers" in text
    assert "windows review" in text
    assert "blocking mode" in text
    assert "blockedByExternalConstraintsOnly" in text
    assert "blocking_mode == 'external_only'" in text
    assert "local_count == 0" in text
    assert "external findings" in text
    assert "local findings" in text
