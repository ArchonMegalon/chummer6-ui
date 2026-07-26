from __future__ import annotations

import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
STARTUP_SMOKE = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"
PUBLISH_LATEST = REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh"
VERIFY_WINDOWS_BOOTSTRAP = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"
WINDOWS_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"
WINDOWS_BOOTSTRAP_INSTALLER = REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi"


def test_windows_startup_smoke_supports_bootstrap_payload_download_mode() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert 'WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE="${CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-auto}"' in text
    assert 'WINDOWS_WINE_HOST_TEMP_ROOT=""' in text
    assert "start_windows_payload_http_server()" in text
    assert 'if [[ -n "$local_payload_path" ]]; then' in text
    assert 'configured_payload_mode="download"' in text
    assert 'windows_binary_env_prefix=(env "TEMP=$windows_binary_temp_root" "TMP=$windows_binary_temp_root")' in text
    assert 'windows_host_temp_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-wine-temp.XXXXXX")"' in text
    assert 'local -a installer_args=("/smoke-install=$native_install_root")' in text
    assert 'CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" \\' in text
    assert 'local installer_trace_root="${WINDOWS_WINE_HOST_TEMP_ROOT:-$wine_temp_dir}"' in text
    assert 'resolved_wine_temp_dir="$(resolve_wine_temp_dir || true)"' in text
    assert '"$resolved_wine_temp_dir/Chummer6/installer-temp/chummer-desktop-installer-progress.log"' in text
    assert 'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="download"' in text


def test_windows_bootstrap_binding_is_written_and_checked_after_final_metadata() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")
    final_block = (
        'if [[ "$RID" == win-* && -n '
        '"$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE" ]]; then\n'
        '  attach_windows_bootstrap_verification_to_receipt'
    )
    final_metadata = "fi\nattach_release_artifact_metadata_to_receipt\n"
    final_validation = (
        'if [[ "$RID" == win-* && -n '
        '"$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE" ]]; then\n'
        '  validate_windows_bootstrap_verification_receipt \\\n'
        '    "$ARTIFACT_PATH"'
    )

    assert final_block in text
    assert final_metadata in text
    assert final_validation in text
    assert text.index(final_block) < text.index(final_metadata)
    assert text.index(final_metadata) < text.index(final_validation)
    assert text.index(final_validation) < text.index(
        'if [[ "$(receipt_status 2>/dev/null || true)" == "skipped" ]]'
    )
    run_smoke_tail = text[
        text.index(
            'CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" '
            'run_head_smoke'
        ) : text.index("\nseed_dpkg_admin_dir()")
    ]
    assert "attach_windows_bootstrap_verification_to_receipt" not in run_smoke_tail


def test_windows_bootstrap_final_receipt_binding_runtime(tmp_path: Path) -> None:
    source = STARTUP_SMOKE.read_text(encoding="utf-8")
    helper_end = source.index("\nresolve_public_web_base_url() {")
    harness = tmp_path / "final-bootstrap-receipt-binding.sh"
    harness.write_text(
        source[:helper_end]
        + r'''

payload_sha256="$(sha256_file "$ARTIFACT_PATH")"
payload_size_bytes="$(wc -c < "$ARTIFACT_PATH" | tr -d '[:space:]')"
payload_file_name="$(basename "$ARTIFACT_PATH")"
payload_url="http://127.0.0.1:43210/$payload_file_name"

"$PYTHON_BIN" - "$RECEIPT_PATH" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
path.write_text(json.dumps({"status": "pass"}) + "\n", encoding="utf-8")
PY

attach_windows_bootstrap_verification_to_receipt \
  "download" "$payload_url" "$payload_sha256" "$payload_size_bytes" "$payload_file_name"
"$PYTHON_BIN" - "$RECEIPT_PATH" "$ARTIFACT_PATH" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
artifact = pathlib.Path(sys.argv[2])
payload = json.loads(path.read_text(encoding="utf-8"))
payload["artifactPath"] = artifact.name
payload["artifactPathDisclosure"] = "file_name_only"
path.write_text(json.dumps(payload) + "\n", encoding="utf-8")
PY
validate_windows_bootstrap_verification_receipt \
  "$ARTIFACT_PATH" "download" "$payload_url" "$payload_sha256" "$payload_size_bytes" "$payload_file_name"

"$PYTHON_BIN" - "$RECEIPT_PATH" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
payload = json.loads(path.read_text(encoding="utf-8"))
payload["bootstrapPayloadAcquisitionMode"] = "local_handoff"
path.write_text(json.dumps(payload) + "\n", encoding="utf-8")
PY

if validate_windows_bootstrap_verification_receipt \
  "$ARTIFACT_PATH" "download" "$payload_url" "$payload_sha256" "$payload_size_bytes" "$payload_file_name"; then
  exit 1
fi
''',
        encoding="utf-8",
    )
    payload = tmp_path / "chummer-avalonia-win-x64-payload.zip"
    payload.write_bytes(b"exact-bootstrap-payload")
    output = tmp_path / "output"

    completed = subprocess.run(
        [
            "bash",
            str(harness),
            str(payload),
            "avalonia",
            "win-x64",
            "Chummer.Avalonia.exe",
            str(output),
            "run-final-receipt-fixture",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
        timeout=15,
    )

    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert (
        "Windows startup receipt final "
        "bootstrapPayloadAcquisitionMode binding differs."
        in completed.stderr
    )


def test_windows_payload_http_server_is_loopback_only_and_cleanup_owned() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert 'local payload_host="127.0.0.1"' in text
    assert '--bind "$payload_host"' in text
    assert 'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL="http://${payload_host}:' in text
    assert 'start_windows_payload_http_server "$local_payload_path"' in text
    assert (
        'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL="$(start_windows_payload_http_server'
        not in text
    )
    assert "resolve_windows_payload_http_host()" not in text
    assert 'bind_host="0.0.0.0"' not in text
    assert 'wait "$WINDOWS_PAYLOAD_HTTP_PID" >/dev/null 2>&1 || true' in text
    assert 'rm -rf "$WINDOWS_PAYLOAD_HTTP_ROOT"' in text


def test_windows_payload_http_server_runtime_lifecycle(tmp_path: Path) -> None:
    source = STARTUP_SMOKE.read_text(encoding="utf-8")
    helper_end = source.index(
        "\nattach_windows_bootstrap_verification_to_receipt() {"
    )
    harness = tmp_path / "payload-http-lifecycle.sh"
    harness.write_text(
        source[:helper_end]
        + r'''

unset HTTP_PROXY HTTPS_PROXY ALL_PROXY http_proxy https_proxy all_proxy
export NO_PROXY=127.0.0.1
export no_proxy=127.0.0.1
start_windows_payload_http_server "$ARTIFACT_PATH"
server_pid="$WINDOWS_PAYLOAD_HTTP_PID"
server_root="$WINDOWS_PAYLOAD_HTTP_ROOT"
server_url="$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL"
server_parent_pid="$(ps -o ppid= -p "$server_pid" | tr -d '[:space:]')"
server_command="$(ps -ww -o command= -p "$server_pid")"

[[ "$server_pid" =~ ^[0-9]+$ ]]
[[ "$server_parent_pid" == "$BASHPID" ]]
[[ "$server_url" == http://127.0.0.1:* ]]
[[ "$server_command" == *"-m http.server"* ]]
[[ "$server_command" == *"--bind 127.0.0.1"* ]]
[[ "$server_command" == *"--directory $server_root"* ]]
[[ -d "$server_root" ]]
[[ -f "$server_root/$(basename "$ARTIFACT_PATH")" ]]

"$PYTHON_BIN" - "$server_url" "$ARTIFACT_PATH" <<'PY'
import pathlib
import sys
import urllib.request

with urllib.request.urlopen(sys.argv[1], timeout=2) as response:
    served = response.read()
expected = pathlib.Path(sys.argv[2]).read_bytes()
raise SystemExit(0 if response.status == 200 and served == expected else 1)
PY

cleanup_kill_pid=""
cleanup_wait_pid=""
kill() {
  cleanup_kill_pid="${1:-}"
  builtin kill "$@"
}
wait() {
  cleanup_wait_pid="${1:-}"
  builtin wait "$@"
}

cleanup

[[ "$cleanup_kill_pid" == "$server_pid" ]]
[[ "$cleanup_wait_pid" == "$server_pid" ]]
[[ -z "$WINDOWS_PAYLOAD_HTTP_PID" ]]
[[ ! -e "$server_root" ]]
if builtin kill -0 "$server_pid" >/dev/null 2>&1; then
  exit 1
fi

"$PYTHON_BIN" - "$server_url" <<'PY'
import sys
import urllib.request

try:
    urllib.request.urlopen(sys.argv[1], timeout=0.5)
except Exception:
    raise SystemExit(0)
raise SystemExit(1)
PY

printf 'payload-http-lifecycle-pass pid=%s parent=%s url=%s\n' \
  "$server_pid" "$server_parent_pid" "$server_url"
''',
        encoding="utf-8",
    )
    payload = tmp_path / "payload.bin"
    payload.write_bytes(b"loopback-only-payload")
    output = tmp_path / "output"

    completed = subprocess.run(
        [
            "bash",
            str(harness),
            str(payload),
            "avalonia",
            "win-x64",
            "fixture.exe",
            str(output),
            "run-lifecycle-fixture",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
        timeout=15,
    )

    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert "payload-http-lifecycle-pass" in completed.stdout


def test_windows_startup_smoke_none_mode_reports_embedded_payload_without_override() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert 'configured_payload_mode="$(lower_ascii "${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-}")"' in text
    assert "local|download|none)" in text
    assert (
        '  else\n'
        '    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="embedded"\n'
        '    CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" \\\n'
        '    run_windows_binary "$ARTIFACT_PATH" "${installer_args[@]}" >>"$LOG_PATH" 2>&1\n'
        '  fi'
    ) in text
    assert 'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="embedded_metadata"' not in text


def test_windows_startup_smoke_owns_and_stops_an_isolated_wine_prefix_by_default() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert 'WINDOWS_WINE_PREFIX_ROOT=""' in text
    assert 'WINDOWS_WINE_PREFIX_OWNED=0' in text
    assert 'configure_windows_wine_prefix()' in text
    assert 'CHUMMER_WINDOWS_STARTUP_SMOKE_ISOLATED_PREFIX:-1' in text
    assert 'export WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT"' in text
    assert 'WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" timeout 15 wineserver -k' in text
    assert 'WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" timeout 15 wineserver -w' in text
    assert 'rm -rf "$WINDOWS_WINE_PREFIX_ROOT"' in text
    assert text.index('configure_windows_wine_prefix') < text.index('case "$RID" in')
    assert 'CHUMMER_INSTALLER_PAYLOAD_URL="$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL"' in text
    assert (
        'wait_for_local_http_url "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL"'
        in text
    )
    assert 'CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_TIMEOUT_SECONDS:-180' in text
    assert 'CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_POLL_SECONDS:-1' in text
    assert "wait_for_windows_installed_relative_path()" in text
    assert 'resolved_launch_relative_path="$(wait_for_windows_installed_relative_path "$launch_relative_path")"' in text
    assert 'local -a installer_trace_candidates=(' in text
    assert '"$installer_trace_root/Chummer6/installer-temp/chummer-desktop-installer-progress.log"' in text
    assert '"$installer_trace_root/chummer-desktop-installer-progress.log"' in text
    assert 'installer_trace_capture_path="$OUTPUT_DIR/windows-installer-progress-$APP_KEY-$RID.log"' in text
    assert 'payload["bootstrapPayloadAcquisitionMode"] = payload_mode' in text
    assert 'payload["bootstrapPayloadSha256"] = payload_sha256' in text
    assert 'payload["bootstrapPayloadSizeBytes"] = int(payload_size_bytes)' in text
    assert 'payload["bootstrapPayloadFileName"] = payload_file_name' in text


def test_windows_bootstrap_smoke_install_uses_value_option_delimiter() -> None:
    text = WINDOWS_BOOTSTRAP_INSTALLER.read_text(encoding="utf-8")

    assert '${GetOptions} "$CommandLine" "/smoke-install=" $SmokeInstallPath' in text
    assert '${GetOptions} "$CommandLine" "--smoke-install" $SmokeInstallPath' not in text
    assert '${GetOptions} "$CommandLine" "--smoke-install=" $SmokeInstallPath' not in text
    assert 'Push "Smoke install target: $SmokeInstallPath"' in text


def test_startup_smoke_avoids_bash4_case_conversion_expansions() -> None:
    text = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert "array_count()" in text
    assert "lower_ascii()" in text
    assert "upper_ascii()" in text
    assert '${PROCESSOR_ARCHITECTURE,,}' not in text
    assert '${arch_primary^^}' not in text
    assert '${arch_secondary^^}' not in text
    assert 'case "${1,,}" in' not in text
    assert '${drive^^}' not in text
    assert '${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE,,}' not in text
    assert '${#missing_paths[@]}' not in text
    assert 'if (( $(array_count timeout_prefix) > 0 )); then' in text


def test_publish_latest_nightly_requires_download_mode_receipts_for_bootstrap_installers() -> None:
    publisher = PUBLISH_LATEST.read_text(encoding="utf-8")
    verifier = VERIFY_WINDOWS_BOOTSTRAP.read_text(encoding="utf-8")

    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py"' in publisher
    assert (
        "Windows bootstrap installer startup-smoke receipt did not exercise expected payload "
        in verifier
    )
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke receipt payloadSizeBytes mismatch" in verifier
    assert "Windows bootstrap installer startup-smoke progress log is missing a percent-and-speed download line" in verifier
    assert (
        "did not prove "
        in verifier
        and "bootstrap payload acquisition mode"
        in verifier
    )
    assert (
        'norm(receipt.get("bootstrapPayloadAcquisitionMode"))'
        " != expected_acquisition_mode"
        in verifier
    )


def test_windows_exit_gate_requires_bootstrap_download_mode_receipts() -> None:
    text = WINDOWS_EXIT_GATE.read_text(encoding="utf-8")

    assert 'artifact_installer_mode = normalize_token(windows_artifact.get("installerMode"))' in text
    assert 'evidence["expected_windows_installer_mode"] = artifact_installer_mode' in text
    assert 'startup_smoke_bootstrap_payload_mode = normalize_token(startup_smoke_payload.get("bootstrapPayloadAcquisitionMode"))' in text
    assert 'evidence["startup_smoke_bootstrap_payload_acquisition_mode"] = startup_smoke_bootstrap_payload_mode' in text
    assert "Windows startup smoke receipt did not exercise bootstrap payload download mode." in text
    assert "Windows startup smoke receipt bootstrap payload SHA-256 does not match release-channel metadata." in text
    assert "Windows startup smoke receipt bootstrap payload size does not match release-channel metadata." in text
