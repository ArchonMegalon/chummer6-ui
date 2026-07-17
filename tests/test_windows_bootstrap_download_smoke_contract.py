from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
STARTUP_SMOKE = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"
PUBLISH_LATEST = REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh"
VERIFY_WINDOWS_BOOTSTRAP = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"
WINDOWS_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"
VERIFY_WINDOWS_EVIDENCE = REPO_ROOT / "scripts" / "verify-windows-release-evidence.py"


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
    assert "Windows bootstrap installer startup-smoke receipt did not exercise expected payload" in verifier
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke receipt payloadSizeBytes mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke progress log is missing a percent-and-speed download line" in verifier
    assert "did not prove " in verifier
    assert "bootstrap payload acquisition mode" in verifier
    assert 'return norm(row.get("payloadAcquisitionMode")) or "download"' in verifier
    assert 'norm(receipt.get("bootstrapPayloadAcquisitionMode")) != expected_acquisition_mode' in verifier


def test_publish_latest_nightly_binds_all_windows_evidence_before_publication() -> None:
    publisher = PUBLISH_LATEST.read_text(encoding="utf-8")
    evidence_start = publisher.index("verify_latest_stage_windows_release_evidence() {")
    evidence_end = publisher.index("is_publishable_nightly_stage() {")
    evidence_function = publisher[evidence_start:evidence_end]

    payload_call = 'verify_latest_stage_windows_payload_gate "$latest_stage"'
    smoke_call = 'verify_latest_stage_windows_startup_smoke_gate "$latest_stage"'
    exit_call = 'verify_latest_stage_windows_exit_gate "$latest_stage"'
    evidence_call = 'verify_latest_stage_windows_release_evidence "$latest_stage"'
    publish_marker = 'echo "Publishing latest nightly stage: $latest_stage"'

    assert VERIFY_WINDOWS_EVIDENCE.is_file()
    assert '--files-dir "$files_dir"\n    --allow-empty' not in publisher
    assert 'python3 "$SCRIPT_DIR/verify-windows-release-evidence.py"' in publisher
    assert '--release-channel "$release_channel_manifest"' in publisher
    assert '--downloads-manifest "$releases_manifest"' in publisher
    assert '--signing-dir "$signing_dir"' in publisher
    assert '--startup-smoke-dir "$startup_smoke_dir"' in publisher
    assert '--windows-exit-gate "$windows_exit_gate"' in publisher
    assert 'evidence_args+=(--require-authenticode --require-native-windows)' in publisher
    assert 'if forced_preview_nightly_visual_handoff_allowed "$stage_dir" >/dev/null; then' in evidence_function
    assert '--windows-visual-proof-handoff "$stage_dir/WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"' in evidence_function
    assert "--allow-proof-only-visual-handoff" in evidence_function
    assert evidence_function.index("forced_preview_nightly_visual_handoff_allowed") < evidence_function.index(
        "--allow-proof-only-visual-handoff"
    )
    assert evidence_function.index("--allow-proof-only-visual-handoff") < evidence_function.index(
        'python3 "$SCRIPT_DIR/verify-windows-release-evidence.py"'
    )
    assert publisher.index(payload_call) < publisher.index(smoke_call)
    assert publisher.index(smoke_call) < publisher.index(exit_call)
    assert publisher.index(exit_call) < publisher.index(evidence_call)
    assert publisher.index(evidence_call) < publisher.index(publish_marker)


def test_windows_exit_gate_requires_bootstrap_download_mode_receipts() -> None:
    text = WINDOWS_EXIT_GATE.read_text(encoding="utf-8")

    assert 'artifact_installer_mode = normalize_token(windows_artifact.get("installerMode"))' in text
    assert 'evidence["expected_windows_installer_mode"] = artifact_installer_mode' in text
    assert 'startup_smoke_bootstrap_payload_mode = normalize_token(startup_smoke_payload.get("bootstrapPayloadAcquisitionMode"))' in text
    assert 'evidence["startup_smoke_bootstrap_payload_acquisition_mode"] = startup_smoke_bootstrap_payload_mode' in text
    assert "Windows startup smoke receipt did not exercise expected bootstrap payload acquisition mode" in text
    assert "Windows startup smoke receipt bootstrap payload SHA-256 does not match release-channel metadata." in text
    assert "Windows startup smoke receipt bootstrap payload size does not match release-channel metadata." in text
