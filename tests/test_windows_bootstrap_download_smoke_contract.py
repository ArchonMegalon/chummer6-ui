from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
STARTUP_SMOKE = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"
PUBLISH_LATEST = REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh"
VERIFY_WINDOWS_BOOTSTRAP = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"
WINDOWS_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"


def test_windows_startup_smoke_supports_bootstrap_payload_download_mode() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert 'WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE="${CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-local}"' in text
    assert "start_windows_payload_http_server()" in text
    assert 'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="download"' in text
    assert 'CHUMMER_INSTALLER_PAYLOAD_URL="$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL"' in text
    assert 'wait_for_local_http_url "$payload_url"' in text
    assert 'local -a installer_trace_candidates=(' in text
    assert '"$wine_temp_dir/Chummer6/installer-temp/chummer-desktop-installer-progress.log"' in text
    assert '"$wine_temp_dir/chummer-desktop-installer-progress.log"' in text
    assert 'installer_trace_capture_path="$OUTPUT_DIR/windows-installer-progress-$APP_KEY-$RID.log"' in text
    assert 'payload["bootstrapPayloadAcquisitionMode"] = payload_mode' in text
    assert 'payload["bootstrapPayloadSha256"] = payload_sha256' in text
    assert 'payload["bootstrapPayloadSizeBytes"] = int(payload_size_bytes)' in text
    assert 'payload["bootstrapPayloadFileName"] = payload_file_name' in text


def test_publish_latest_nightly_requires_download_mode_receipts_for_bootstrap_installers() -> None:
    publisher = PUBLISH_LATEST.read_text(encoding="utf-8")
    verifier = VERIFY_WINDOWS_BOOTSTRAP.read_text(encoding="utf-8")

    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py"' in publisher
    assert "Windows bootstrap installer startup-smoke receipt did not exercise payload download mode" in verifier
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke receipt payloadSizeBytes mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke progress log is missing a percent-and-speed download line" in verifier
    assert "did not prove bootstrap payload download mode" in verifier
    assert 'norm(receipt.get("bootstrapPayloadAcquisitionMode")) != "download"' in verifier


def test_windows_exit_gate_requires_bootstrap_download_mode_receipts() -> None:
    text = WINDOWS_EXIT_GATE.read_text(encoding="utf-8")

    assert 'artifact_installer_mode = normalize_token(windows_artifact.get("installerMode"))' in text
    assert 'evidence["expected_windows_installer_mode"] = artifact_installer_mode' in text
    assert 'startup_smoke_bootstrap_payload_mode = normalize_token(startup_smoke_payload.get("bootstrapPayloadAcquisitionMode"))' in text
    assert 'evidence["startup_smoke_bootstrap_payload_acquisition_mode"] = startup_smoke_bootstrap_payload_mode' in text
    assert "Windows startup smoke receipt did not exercise bootstrap payload download mode." in text
    assert "Windows startup smoke receipt bootstrap payload SHA-256 does not match release-channel metadata." in text
    assert "Windows startup smoke receipt bootstrap payload size does not match release-channel metadata." in text
