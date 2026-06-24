from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify.sh"
UPDATE_RUNTIME_TESTS = REPO_ROOT / "Chummer.Tests" / "DesktopUpdateRuntimeTests.cs"


def test_shared_verify_lane_keeps_desktop_update_runtime_suite_wired() -> None:
    text = VERIFY_SCRIPT.read_text(encoding="utf-8")
    assert "DesktopUpdateRuntimeTests" in text
    assert "desktop_runtime_test_filter" in text
    assert "RunDesktopUpdateTestsOnly=true" in text


def test_desktop_update_runtime_suite_keeps_windows_bootstrap_handoff_coverage() -> None:
    text = UPDATE_RUNTIME_TESTS.read_text(encoding="utf-8")
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert 'InvokePrivateStaticTask(' in text
