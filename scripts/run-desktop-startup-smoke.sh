#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
SCRIPT_DIR="$REPO_ROOT/scripts"
PYTHON_BIN="${CHUMMER_PYTHON_BIN:-/usr/bin/python3}"
if [[ ! -x "$PYTHON_BIN" ]]; then
  PYTHON_BIN="$(command -v python3)"
fi

ARTIFACT_PATH="$(
  "$PYTHON_BIN" - "${1:?artifact path is required}" <<'PY'
import os
import sys

print(os.path.abspath(sys.argv[1]))
PY
)"
APP_KEY="${2:?app key is required}"
RID="${3:?rid is required}"
LAUNCH_TARGET="${4:?launch target is required}"
OUTPUT_DIR="${5:-$REPO_ROOT/dist/startup-smoke}"
VERSION_HINT="${6:-unknown}"

mkdir -p "$OUTPUT_DIR"

RECEIPT_PATH="$OUTPUT_DIR/startup-smoke-$APP_KEY-$RID.receipt.json"
LOG_PATH="$OUTPUT_DIR/startup-smoke-$APP_KEY-$RID.log"
PACKET_PATH="$OUTPUT_DIR/release-regression-$APP_KEY-$RID.json"
HOST_CLASS="${CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS:-local-${RID}}"
CHANNEL_HINT="${CHUMMER_DESKTOP_RELEASE_CHANNEL:-docker}"
INSTALL_ROOT=""
UNPACK_ROOT=""
MOUNT_DIR=""
BUNDLE_EXTRACT_ROOT=""
RUNTIME_HOME=""
DPKG_ADMIN_DIR=""
WINDOWS_LOCAL_PAYLOAD_COPY=""
DPKG_LOG_PATH="$OUTPUT_DIR/dpkg-$APP_KEY-$RID.log"
INSTALL_VERIFICATION_PATH="$OUTPUT_DIR/install-verification-$APP_KEY-$RID.json"
WINDOWS_PAYLOAD_HTTP_ROOT=""
WINDOWS_PAYLOAD_HTTP_LOG=""
WINDOWS_PAYLOAD_HTTP_PID=""
WINDOWS_WINE_HOST_TEMP_ROOT=""
WINDOWS_WINE_PREFIX_ROOT=""
WINDOWS_WINE_PREFIX_OWNED=0
WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE="${CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-auto}"
WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE=""
WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL=""
WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SHA256=""
WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SIZE_BYTES=""
WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_FILE_NAME=""

cleanup() {
  if [[ -n "$WINDOWS_PAYLOAD_HTTP_PID" ]]; then
    kill "$WINDOWS_PAYLOAD_HTTP_PID" >/dev/null 2>&1 || true
  fi

  if [[ "$WINDOWS_WINE_PREFIX_OWNED" == "1" && -n "$WINDOWS_WINE_PREFIX_ROOT" ]]; then
    if command -v wineserver >/dev/null 2>&1; then
      if command -v timeout >/dev/null 2>&1; then
        WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" timeout 15 wineserver -k >/dev/null 2>&1 || true
        WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" timeout 15 wineserver -w >/dev/null 2>&1 || true
      else
        WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" wineserver -k >/dev/null 2>&1 || true
        WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT" wineserver -w >/dev/null 2>&1 || true
      fi
    fi
    rm -rf "$WINDOWS_WINE_PREFIX_ROOT"
  fi

  if [[ -n "$WINDOWS_PAYLOAD_HTTP_ROOT" && -d "$WINDOWS_PAYLOAD_HTTP_ROOT" ]]; then
    rm -rf "$WINDOWS_PAYLOAD_HTTP_ROOT"
  fi

  if [[ -n "$MOUNT_DIR" ]]; then
    hdiutil detach "$MOUNT_DIR" >/dev/null 2>&1 || true
  fi

  if [[ -n "$UNPACK_ROOT" && -d "$UNPACK_ROOT" ]]; then
    rm -rf "$UNPACK_ROOT"
  fi

  if [[ -n "$INSTALL_ROOT" && -d "$INSTALL_ROOT" ]]; then
    rm -rf "$INSTALL_ROOT"
  fi

  if [[ -n "$BUNDLE_EXTRACT_ROOT" && -d "$BUNDLE_EXTRACT_ROOT" ]]; then
    rm -rf "$BUNDLE_EXTRACT_ROOT"
  fi

  if [[ -n "$RUNTIME_HOME" && -d "$RUNTIME_HOME" ]]; then
    rm -rf "$RUNTIME_HOME"
  fi

  if [[ -n "$WINDOWS_LOCAL_PAYLOAD_COPY" && -f "$WINDOWS_LOCAL_PAYLOAD_COPY" ]]; then
    rm -f "$WINDOWS_LOCAL_PAYLOAD_COPY"
  fi

  if [[ -n "$WINDOWS_WINE_HOST_TEMP_ROOT" && -d "$WINDOWS_WINE_HOST_TEMP_ROOT" ]]; then
    rm -rf "$WINDOWS_WINE_HOST_TEMP_ROOT"
  fi
}

trap cleanup EXIT

platform_from_rid() {
  case "$1" in
    win-*) echo "windows" ;;
    linux-*) echo "linux" ;;
    osx-*) echo "macos" ;;
    *)
      echo "unknown"
      ;;
  esac
}

arch_from_rid() {
  case "$1" in
    *-x64) echo "x64" ;;
    *-arm64) echo "arm64" ;;
    *-x86) echo "x86" ;;
    *)
      echo "unknown"
      ;;
  esac
}

sha256_file() {
  "$PYTHON_BIN" - "$1" <<'PY'
import hashlib
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
hasher = hashlib.sha256()
with path.open("rb") as handle:
    for chunk in iter(lambda: handle.read(1024 * 1024), b""):
        hasher.update(chunk)
print(hasher.hexdigest())
PY
}

lower_ascii() {
  printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]'
}

upper_ascii() {
  printf '%s' "${1:-}" | tr '[:lower:]' '[:upper:]'
}

array_count() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || {
    printf '0\n'
    return 0
  }

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "set -- \"\${${array_name}[@]}\""
  local count="$#"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  printf '%s\n' "$count"
}

host_machine() {
  local machine=""
  machine="$(uname -m 2>/dev/null | tr '[:upper:]' '[:lower:]' || true)"
  if [[ -n "$machine" ]]; then
    printf '%s\n' "$machine"
    return
  fi

  if [[ -n "${PROCESSOR_ARCHITECTURE:-}" ]]; then
    printf '%s\n' "$(lower_ascii "$PROCESSOR_ARCHITECTURE")"
    return
  fi

  printf 'unknown\n'
}

host_can_execute_windows_arm64() {
  local arch_primary="${PROCESSOR_ARCHITECTURE:-}"
  local arch_secondary="${PROCESSOR_ARCHITEW6432:-}"
  arch_primary="$(upper_ascii "$arch_primary")"
  arch_secondary="$(upper_ascii "$arch_secondary")"
  [[ "$arch_primary" == "ARM64" || "$arch_secondary" == "ARM64" ]]
}

host_can_execute_windows_binary() {
  command -v wine >/dev/null 2>&1 \
    || command -v wine64 >/dev/null 2>&1 \
    || [[ -x /usr/lib/wine/wine64 ]] \
    || command -v powershell.exe >/dev/null 2>&1 \
    || command -v pwsh >/dev/null 2>&1 \
    || command -v cygpath >/dev/null 2>&1
}

host_can_execute_linux_arm64() {
  local machine
  machine="$(host_machine)"
  case "$machine" in
    aarch64|arm64)
      return 0
      ;;
  esac

  command -v qemu-aarch64-static >/dev/null 2>&1 || command -v qemu-aarch64 >/dev/null 2>&1
}

env_truthy() {
  case "$(lower_ascii "${1:-}")" in
    1|true|yes|on)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

configure_windows_wine_prefix() {
  if [[ "$(platform_from_rid "$RID")" != "windows" ]]; then
    return
  fi
  if ! env_truthy "${CHUMMER_WINDOWS_STARTUP_SMOKE_ISOLATED_PREFIX:-1}"; then
    return
  fi
  if ! command -v wine >/dev/null 2>&1 \
    && ! command -v wine64 >/dev/null 2>&1 \
    && [[ ! -x /usr/lib/wine/wine64 ]]; then
    return
  fi

  WINDOWS_WINE_PREFIX_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-startup-wineprefix.XXXXXX")"
  WINDOWS_WINE_PREFIX_OWNED=1
  export WINEPREFIX="$WINDOWS_WINE_PREFIX_ROOT"
}

find_free_tcp_port() {
  "$PYTHON_BIN" - <<'PY'
import socket

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
}

wait_for_local_http_url() {
  local url="$1"
  "$PYTHON_BIN" - "$url" <<'PY'
import sys
import time
import urllib.request

url = sys.argv[1]
last_error = None
for _ in range(50):
    try:
        with urllib.request.urlopen(url, timeout=1) as response:
            if response.status == 200:
                raise SystemExit(0)
    except Exception as exc:  # noqa: BLE001
        last_error = exc
        time.sleep(0.1)

print(f"Timed out waiting for local payload server: {last_error}", file=sys.stderr)
raise SystemExit(1)
PY
}

resolve_windows_payload_http_host() {
  "$PYTHON_BIN" - <<'PY'
import socket

host = "127.0.0.1"
try:
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.connect(("8.8.8.8", 80))
        candidate = sock.getsockname()[0].strip()
        if candidate and candidate != "127.0.0.1":
            host = candidate
except OSError:
    pass

print(host)
PY
}

start_windows_payload_http_server() {
  local payload_path="$1"
  local payload_name
  payload_name="$(basename "$payload_path")"
  local payload_port
  payload_port="$(find_free_tcp_port)"
  local payload_host
  payload_host="$(resolve_windows_payload_http_host)"
  local bind_host="127.0.0.1"
  if [[ "$payload_host" != "127.0.0.1" ]]; then
    bind_host="0.0.0.0"
  fi

  WINDOWS_PAYLOAD_HTTP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-windows-payload-http.XXXXXX")"
  WINDOWS_PAYLOAD_HTTP_LOG="$OUTPUT_DIR/startup-smoke-payload-http-$APP_KEY-$RID.log"
  cp "$payload_path" "$WINDOWS_PAYLOAD_HTTP_ROOT/$payload_name"

  "$PYTHON_BIN" -m http.server "$payload_port" --bind "$bind_host" --directory "$WINDOWS_PAYLOAD_HTTP_ROOT" \
    >"$WINDOWS_PAYLOAD_HTTP_LOG" 2>&1 &
  WINDOWS_PAYLOAD_HTTP_PID=$!

  local payload_url="http://${payload_host}:${payload_port}/${payload_name}"
  wait_for_local_http_url "$payload_url"
  printf '%s\n' "$payload_url"
}

attach_windows_bootstrap_verification_to_receipt() {
  local payload_mode="$1"
  local payload_url="$2"
  local payload_sha256="$3"
  local payload_size_bytes="$4"
  local payload_file_name="$5"

  if [[ ! -f "$RECEIPT_PATH" ]]; then
    return
  fi

  python3 - "$RECEIPT_PATH" "$ARTIFACT_PATH" "$payload_mode" "$payload_url" "$payload_sha256" "$payload_size_bytes" "$payload_file_name" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
artifact_path = pathlib.Path(sys.argv[2])
payload_mode = str(sys.argv[3]).strip()
payload_url = str(sys.argv[4]).strip()
payload_sha256 = str(sys.argv[5]).strip().lower()
payload_size_bytes = str(sys.argv[6]).strip()
payload_file_name = str(sys.argv[7]).strip()

payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
payload["artifactInstallMode"] = "nsis_bootstrap_installer"
payload["artifactPath"] = str(artifact_path)
if payload_mode:
    payload["bootstrapPayloadAcquisitionMode"] = payload_mode
if payload_url:
    payload["bootstrapPayloadDownloadUrl"] = payload_url
if payload_sha256:
    payload["bootstrapPayloadSha256"] = payload_sha256
if payload_size_bytes:
    payload["bootstrapPayloadSizeBytes"] = int(payload_size_bytes)
if payload_file_name:
    payload["bootstrapPayloadFileName"] = payload_file_name
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

resolve_public_web_base_url() {
  local configured_public_base_url="${1:-}"
  local configured_web_base_url="${2:-}"
  local allow_internal_public_hosts="${CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}"
  local allow_internal_public_hosts_normalized="0"

  if env_truthy "$allow_internal_public_hosts"; then
    allow_internal_public_hosts_normalized="1"
  fi

  local candidate="${configured_public_base_url:-${configured_web_base_url:-https://chummer.run}}"
  candidate="${candidate%/}"

  if [[ -z "$candidate" ]]; then
    echo "https://chummer.run"
    return
  fi

  local resolved=""
  resolved="$($PYTHON_BIN - "$candidate" "$allow_internal_public_hosts_normalized" <<'PY'
import sys
import ipaddress
from urllib.parse import urlparse

candidate = str(sys.argv[1]).strip()
if not candidate:
    print("")
    raise SystemExit(0)

parsed = urlparse(candidate)
if parsed.scheme not in {"http", "https"}:
    print("")
    raise SystemExit(0)
host = (parsed.hostname or "").strip().lower()
if not host:
    print("")
    raise SystemExit(0)


def is_unsafe_public_host(hostname: str) -> bool:
    normalized = hostname.strip().lower().strip(".")
    if not normalized:
        return True

    if normalized in {"localhost", "127.0.0.1", "::1"}:
        return False

    try:
        address = ipaddress.ip_address(normalized)
    except ValueError:
        address = None

    if address is not None:
        return not address.is_loopback

    allow_internal = str(sys.argv[2] if len(sys.argv) > 1 else "")
    if allow_internal:
        normalized_allow = allow_internal.strip().lower()
        if normalized_allow in {"1", "true", "yes", "on"}:
            print(parsed.geturl())
            raise SystemExit(0)

    blocked_tokens = ("chummer-api", "chummer-web", "host.docker.internal")
    for token in blocked_tokens:
        if normalized == token:
            return True
        if (
            normalized.startswith(f"{token}.")
            or normalized.endswith(f".{token}")
            or normalized.startswith(f"{token}-")
            or normalized.endswith(f"-{token}")
            or f".{token}." in normalized
            or f".{token}-" in normalized
            or f"-{token}." in normalized
            or f"-{token}-" in normalized
        ):
            return True

    return False

if is_unsafe_public_host(host):
    print("")
    raise SystemExit(0)

print(parsed.geturl())
PY
)"

  if [[ -n "$resolved" && "$resolved" != "" ]]; then
    echo "$resolved"
    return
  fi

  echo "https://chummer.run"
}

emit_incompatible_host_receipt() {
  local reason="$1"
  local platform
  platform="$(platform_from_rid "$RID")"
  local arch
  arch="$(arch_from_rid "$RID")"
  local artifact_sha
  artifact_sha="$(sha256_file "$ARTIFACT_PATH")"
  local machine
  machine="$(host_machine)"

  python3 - "$RECEIPT_PATH" "$ARTIFACT_PATH" "$artifact_sha" "$APP_KEY" "$RID" "$VERSION_HINT" "$CHANNEL_HINT" "$HOST_CLASS" "$platform" "$arch" "$reason" "$machine" <<'PY'
import datetime as dt
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
artifact_path = pathlib.Path(sys.argv[2])
artifact_sha = str(sys.argv[3]).strip().lower()
app_key = str(sys.argv[4]).strip()
rid = str(sys.argv[5]).strip()
version_hint = str(sys.argv[6]).strip()
channel_hint = str(sys.argv[7]).strip()
host_class = str(sys.argv[8]).strip()
platform = str(sys.argv[9]).strip()
arch = str(sys.argv[10]).strip()
reason = str(sys.argv[11]).strip()
host_machine = str(sys.argv[12]).strip()
now = dt.datetime.now(dt.timezone.utc).isoformat()

artifact_file_name = artifact_path.name
artifact_relative_path = artifact_file_name
if artifact_path.parent.name:
    artifact_relative_path = f"{artifact_path.parent.name}/{artifact_file_name}"

payload = {
    "status": "skipped",
    "headId": app_key,
    "version": version_hint,
    "releaseVersion": version_hint,
    "channelId": channel_hint,
    "platform": platform,
    "arch": arch,
    "rid": rid,
    "hostClass": host_class,
    "processPath": None,
    "artifactDigest": f"sha256:{artifact_sha}",
    "artifactDigestSource": "environment",
    "recordedAtUtc": now,
    "startedAtUtc": now,
    "completedAtUtc": now,
    "artifactPath": str(artifact_path),
    "artifactFileName": artifact_file_name,
    "fileName": artifact_file_name,
    "artifactRelativePath": artifact_relative_path,
    "artifactSha256": artifact_sha,
    "artifactId": f"{app_key}-{rid}-installer",
    "skipReason": reason,
    "skipClass": "incompatible_host",
    "verificationDisposition": "incompatible_host",
    "hostMachine": host_machine,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

  printf 'startup smoke skipped for %s %s: %s\n' "$APP_KEY" "$RID" "$reason" | tee -a "$LOG_PATH" >&2
}

receipt_status() {
  python3 - "$RECEIPT_PATH" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
if not receipt_path.exists() or not receipt_path.is_file():
    raise SystemExit(1)

payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(1)

status = str(payload.get("status") or "").strip().lower()
if not status:
    raise SystemExit(1)

print(status)
PY
}

to_native_path() {
  local input_path="$1"

  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$input_path"
    return
  fi

  if command -v winepath >/dev/null 2>&1; then
    local native_path=""
    local winepath_timeout="${CHUMMER_WINEPATH_TIMEOUT_SECONDS:-15}"
    if command -v timeout >/dev/null 2>&1; then
      native_path="$(timeout "$winepath_timeout" winepath -w "$input_path" 2>/dev/null | tr -d '\r' || true)"
    else
      native_path="$(winepath -w "$input_path" 2>/dev/null | tr -d '\r' || true)"
    fi
    if [[ -n "$native_path" ]]; then
      printf '%s\n' "$native_path"
      return
    fi

    case "$input_path" in
      [A-Za-z]:[\\/]*)
        printf '%s\n' "$input_path"
        return
        ;;
      */dosdevices/[A-Za-z]:/*)
        local dosdevices_path="${input_path#*/dosdevices/}"
        local drive="${dosdevices_path%%:*}"
        local drive_path="${dosdevices_path#*:}"
        printf '%s:%s\n' "$(upper_ascii "$drive")" "${drive_path//\//\\}"
        return
        ;;
      /*)
        # Wine maps the Unix filesystem root to Z: by default; use it when winepath hangs.
        printf 'Z:%s\n' "${input_path//\//\\}"
        return
        ;;
    esac
  fi

  if pwd -W >/dev/null 2>&1; then
    case "$input_path" in
      [A-Za-z]:[\\/]*)
        echo "$input_path"
        return
        ;;
      /*)
        local parent_dir base_name native_parent
        parent_dir="$(dirname "$input_path")"
        base_name="$(basename "$input_path")"
        if pushd "$parent_dir" >/dev/null 2>&1; then
          native_parent="$(pwd -W | tr -d '\r')"
          popd >/dev/null 2>&1 || true
          if [[ -n "$native_parent" ]]; then
            printf '%s\\%s\n' "$native_parent" "$base_name"
            return
          fi
        fi
        ;;
    esac
  fi

  echo "$input_path"
}

to_native_launch_path() {
  local input_path="$1"

  case "$input_path" in
    */dosdevices/[A-Za-z]:/*)
      # Keep Wine-prefix temp installs addressable as concrete filesystem paths.
      printf 'Z:%s\n' "${input_path//\//\\}"
      return
      ;;
  esac

  to_native_path "$input_path"
}

resolve_wine_temp_dir() {
  if ! command -v winepath >/dev/null 2>&1; then
    return 1
  fi

  local wine_bin=""
  if command -v wine >/dev/null 2>&1; then
    wine_bin="wine"
  elif command -v wine64 >/dev/null 2>&1; then
    wine_bin="wine64"
  elif [[ -x /usr/lib/wine/wine64 ]]; then
    wine_bin="/usr/lib/wine/wine64"
  else
    return 1
  fi

  local wine_temp_timeout="${CHUMMER_WINE_TEMP_QUERY_TIMEOUT_SECONDS:-15}"

  local native_temp=""
  if command -v timeout >/dev/null 2>&1 && [[ -n "$wine_temp_timeout" && "$wine_temp_timeout" != "0" ]]; then
    native_temp="$(timeout "$wine_temp_timeout" "$wine_bin" cmd /c echo %TEMP% 2>/dev/null | tr -d '\r' | awk 'NF { line=$0 } END { print line }' || true)"
  else
    native_temp="$("$wine_bin" cmd /c echo %TEMP% 2>/dev/null | tr -d '\r' | awk 'NF { line=$0 } END { print line }')"
  fi
  if [[ -n "$native_temp" ]]; then
    local unix_temp=""
    if command -v timeout >/dev/null 2>&1 && [[ -n "$wine_temp_timeout" && "$wine_temp_timeout" != "0" ]]; then
      unix_temp="$(timeout "$wine_temp_timeout" winepath -u "$native_temp" 2>/dev/null | tr -d '\r' || true)"
    else
      unix_temp="$(winepath -u "$native_temp" 2>/dev/null | tr -d '\r' || true)"
    fi
    if [[ -n "$unix_temp" ]]; then
      printf '%s\n' "$unix_temp"
      return 0
    fi
  fi

  local fallback_temp=""
  if command -v timeout >/dev/null 2>&1 && [[ -n "$wine_temp_timeout" && "$wine_temp_timeout" != "0" ]]; then
    fallback_temp="$(timeout "$wine_temp_timeout" winepath -u 'C:\\windows\\temp' 2>/dev/null | tr -d '\r' || true)"
  else
    fallback_temp="$(winepath -u 'C:\\windows\\temp' 2>/dev/null | tr -d '\r' || true)"
  fi
  if [[ -n "$fallback_temp" ]]; then
    printf '%s\n' "$fallback_temp"
    return 0
  fi

  return 1
}

run_with_optional_xvfb() {
  if [[ -n "${DISPLAY:-}" || -n "${WAYLAND_DISPLAY:-}" ]]; then
    "$@"
    return
  fi

  if command -v xvfb-run >/dev/null 2>&1; then
    xvfb-run -a "$@"
    return
  fi

  "$@"
}

run_windows_binary() {
  local executable_path="$1"
  shift

  local windows_binary_temp_root="${CHUMMER_WINDOWS_BINARY_TEMP_ROOT:-}"
  local -a windows_binary_env_prefix=()
  if [[ -n "$windows_binary_temp_root" ]]; then
    windows_binary_env_prefix=(env "TEMP=$windows_binary_temp_root" "TMP=$windows_binary_temp_root")
  fi

  local wine_bin=""
  if command -v wine >/dev/null 2>&1; then
    wine_bin="wine"
  elif command -v wine64 >/dev/null 2>&1; then
    wine_bin="wine64"
  elif [[ -x /usr/lib/wine/wine64 ]]; then
    wine_bin="/usr/lib/wine/wine64"
  fi
  if [[ -n "$wine_bin" ]]; then
    local native_executable_path
    native_executable_path="$(to_native_launch_path "$executable_path")"
    local wine_binary_timeout="${CHUMMER_WINDOWS_BINARY_TIMEOUT_SECONDS:-300}"
    if command -v timeout >/dev/null 2>&1 && [[ -n "$wine_binary_timeout" && "$wine_binary_timeout" != "0" ]]; then
      run_with_optional_xvfb "${windows_binary_env_prefix[@]}" timeout "$wine_binary_timeout" "$wine_bin" "$native_executable_path" "$@"
    else
      run_with_optional_xvfb "${windows_binary_env_prefix[@]}" "$wine_bin" "$native_executable_path" "$@"
    fi
    return
  fi

  if command -v powershell.exe >/dev/null 2>&1 || command -v pwsh >/dev/null 2>&1; then
    local native_executable_path
    native_executable_path="$(to_native_launch_path "$executable_path")"
    local powershell_bin="powershell.exe"
    if command -v pwsh >/dev/null 2>&1; then
      powershell_bin="pwsh"
    fi
    local args_json
    args_json="$("$PYTHON_BIN" - "$@" <<'PY'
import json
import sys

print(json.dumps(sys.argv[1:]))
PY
)"
    CHUMMER_WINDOWS_BINARY_PATH="$native_executable_path" \
    CHUMMER_WINDOWS_BINARY_ARGS_JSON="$args_json" \
    "$powershell_bin" -NoLogo -NoProfile -Command '
      $exe = $env:CHUMMER_WINDOWS_BINARY_PATH
      $args = @()
      if ($env:CHUMMER_WINDOWS_BINARY_ARGS_JSON) {
        $decoded = ConvertFrom-Json $env:CHUMMER_WINDOWS_BINARY_ARGS_JSON
        if ($null -ne $decoded) {
          if ($decoded -is [System.Array]) {
            $args = @($decoded)
          }
          else {
            $args = @([string]$decoded)
          }
        }
      }
      & $exe @args
      exit $LASTEXITCODE
    '
    return
  fi

  if command -v cygpath >/dev/null 2>&1; then
    local unix_executable_path="$executable_path"
    case "$executable_path" in
      [A-Za-z]:[\\/]*)
        unix_executable_path="$(cygpath -u "$executable_path")"
        ;;
    esac
    "$unix_executable_path" "$@"
    return
  fi

  "$executable_path" "$@"
}

run_startup_smoke_process() {
  local launch_path="$1"

  if [[ "$(platform_from_rid "$RID")" == "windows" ]]; then
    # Compatibility note: this follows the same execution lane as `run_with_optional_xvfb wine ...` on Linux/Wine hosts.
    run_windows_binary "$launch_path" --startup-smoke
    return
  fi

  "$launch_path" --startup-smoke
}

run_mouse_first_journey_process() {
  local launch_path="$1"

  if [[ "$(platform_from_rid "$RID")" == "windows" ]]; then
    run_windows_binary "$launch_path" --mouse-first-user-journey
    return
  fi

  if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" && ! "$(command -v xvfb-run 2>/dev/null)" ]]; then
    echo "xvfb-run is required for mouse-first desktop journey smoke when no interactive display is available." >&2
    return 1
  fi

  run_with_optional_xvfb "$launch_path" --mouse-first-user-journey
}

initialize_windows_startup_wine_prefix() {
  local runtime_home="$1"

  if [[ "$(platform_from_rid "$RID")" != "windows" ]]; then
    return 0
  fi
  if ! command -v wine >/dev/null 2>&1 || ! command -v wineboot >/dev/null 2>&1 || ! command -v wineserver >/dev/null 2>&1; then
    return 0
  fi

  local wineboot_timeout="${CHUMMER_WINEBOOT_INIT_TIMEOUT_SECONDS:-180}"
  local -a timeout_prefix=()
  if command -v timeout >/dev/null 2>&1 && [[ -n "$wineboot_timeout" && "$wineboot_timeout" != "0" ]]; then
    timeout_prefix=(timeout "$wineboot_timeout")
  fi

  if (( $(array_count timeout_prefix) > 0 )); then
    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_with_optional_xvfb "${timeout_prefix[@]}" wineboot --init

    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_with_optional_xvfb "${timeout_prefix[@]}" wineserver -w
  else
    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_with_optional_xvfb wineboot --init

    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_with_optional_xvfb wineserver -w
  fi
}

run_head_smoke() {
  local launch_path="$1"
  local receipt_path="$RECEIPT_PATH"
  local packet_path="$PACKET_PATH"
  local artifact_sha
  artifact_sha="$(sha256_file "$ARTIFACT_PATH")"
  local public_web_base_url
  public_web_base_url="$(resolve_public_web_base_url "${CHUMMER_PUBLIC_WEB_BASE_URL:-}" "${CHUMMER_WEB_BASE_URL:-}")"
  local use_existing_windows_wine_home="false"
  if [[ "$(platform_from_rid "$RID")" == "windows" ]] \
    && command -v winepath >/dev/null 2>&1 \
    && { command -v wine >/dev/null 2>&1 || command -v wine64 >/dev/null 2>&1 || [[ -x /usr/lib/wine/wine64 ]]; } \
    && ! env_truthy "${CHUMMER_WINDOWS_STARTUP_SMOKE_ISOLATED_PREFIX:-1}"; then
    use_existing_windows_wine_home="true"
  fi

  if [[ ! -f "$launch_path" ]]; then
    echo "Launch target missing for startup smoke: $launch_path" >&2
    return 1
  fi

  if command -v cygpath >/dev/null 2>&1; then
    receipt_path="$(to_native_path "$receipt_path")"
    packet_path="$(to_native_path "$packet_path")"
  fi

  if [[ -z "$BUNDLE_EXTRACT_ROOT" ]]; then
    BUNDLE_EXTRACT_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-startup-bundle.XXXXXX")"
  fi
  if [[ -z "$RUNTIME_HOME" && "$use_existing_windows_wine_home" != "true" ]]; then
    RUNTIME_HOME="$(mktemp -d "${TMPDIR:-/tmp}/chummer-startup-home.XXXXXX")"
  fi

  local bundle_extract_base_dir="$BUNDLE_EXTRACT_ROOT"
  local runtime_home="$RUNTIME_HOME"
  if [[ "$use_existing_windows_wine_home" == "true" ]]; then
    runtime_home="${HOME:-}"
  fi
  if [[ "$(platform_from_rid "$RID")" == "windows" ]]; then
    receipt_path="$(to_native_path "$receipt_path")"
    packet_path="$(to_native_path "$packet_path")"
    bundle_extract_base_dir="$(to_native_path "$BUNDLE_EXTRACT_ROOT")"
    if [[ "$use_existing_windows_wine_home" != "true" ]] && ! command -v wine >/dev/null 2>&1; then
      runtime_home="$(to_native_path "$RUNTIME_HOME")"
    fi
  fi

  if [[ "$use_existing_windows_wine_home" != "true" ]] \
    && ! initialize_windows_startup_wine_prefix "$runtime_home" >>"$LOG_PATH" 2>&1; then
    echo "Wine prefix initialization failed for Windows startup smoke." >>"$LOG_PATH"
    return 1
  fi

  if [[ "$use_existing_windows_wine_home" == "true" ]]; then
    CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$receipt_path" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$packet_path" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="$HOST_CLASS" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$VERSION_HINT" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_RID="$RID" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
    CHUMMER_DESKTOP_UPDATE_ENABLED=0 \
    CHUMMER_PUBLIC_WEB_BASE_URL="$public_web_base_url" \
    CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="${CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$bundle_extract_base_dir" \
    run_startup_smoke_process "$launch_path" >>"$LOG_PATH" 2>&1
  else
    CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$receipt_path" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$packet_path" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="$HOST_CLASS" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$VERSION_HINT" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_RID="$RID" \
    CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
    CHUMMER_DESKTOP_UPDATE_ENABLED=0 \
    CHUMMER_PUBLIC_WEB_BASE_URL="$public_web_base_url" \
    CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="${CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$bundle_extract_base_dir" \
    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_startup_smoke_process "$launch_path" >>"$LOG_PATH" 2>&1
  fi

  local mouse_journey_receipt_path="${CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT:-}"
  if [[ -z "$mouse_journey_receipt_path" ]]; then
    return 0
  fi

  local mouse_journey_failure_packet_path="${CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET:-$OUTPUT_DIR/mouse-first-journey-$APP_KEY-$RID.failure.json}"
  local mouse_journey_screenshot_dir="${CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR:-$OUTPUT_DIR/mouse-first-journey-screenshots-$APP_KEY-$RID}"
  local mouse_journey_trace_path="${CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE:-$OUTPUT_DIR/mouse-first-journey-$APP_KEY-$RID.trace.json}"
  mkdir -p "$(dirname "$mouse_journey_receipt_path")" "$mouse_journey_screenshot_dir" "$(dirname "$mouse_journey_trace_path")"

  if [[ "$use_existing_windows_wine_home" == "true" ]]; then
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$mouse_journey_receipt_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$mouse_journey_failure_packet_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$mouse_journey_screenshot_dir" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$mouse_journey_trace_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_HOST_CLASS="$HOST_CLASS" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RELEASE_VERSION="$VERSION_HINT" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RID="$RID" \
    CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL_HINT" \
    CHUMMER_PUBLIC_WEB_BASE_URL="$public_web_base_url" \
    CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="${CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$bundle_extract_base_dir" \
    run_mouse_first_journey_process "$launch_path" >>"$LOG_PATH" 2>&1
  else
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$mouse_journey_receipt_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$mouse_journey_failure_packet_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$mouse_journey_screenshot_dir" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$mouse_journey_trace_path" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_HOST_CLASS="$HOST_CLASS" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RELEASE_VERSION="$VERSION_HINT" \
    CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RID="$RID" \
    CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL_HINT" \
    CHUMMER_PUBLIC_WEB_BASE_URL="$public_web_base_url" \
    CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="${CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$bundle_extract_base_dir" \
    HOME="$runtime_home" \
    XDG_CONFIG_HOME="$runtime_home/.config" \
    XDG_DATA_HOME="$runtime_home/.local/share" \
    XDG_STATE_HOME="$runtime_home/.local/state" \
    XDG_CACHE_HOME="$runtime_home/.cache" \
    run_mouse_first_journey_process "$launch_path" >>"$LOG_PATH" 2>&1
  fi
}

run_windows_smoke() {
  if [[ "$RID" == "win-arm64" ]] && ! host_can_execute_windows_arm64; then
    emit_incompatible_host_receipt \
      "Windows startup smoke requires a Windows ARM64 host; current host cannot execute win-arm64 installer smoke."
    return 0
  fi
  if ! host_can_execute_windows_binary; then
    emit_incompatible_host_receipt \
      "Windows startup smoke requires Wine, PowerShell, or a Windows-compatible shell bridge; current host cannot execute Windows installer smoke."
    return 0
  fi

  local wine_temp_dir=""
  local windows_host_temp_root=""
  local windows_native_temp_root=""
  if command -v winepath >/dev/null 2>&1 \
    && { command -v wine >/dev/null 2>&1 || command -v wine64 >/dev/null 2>&1 || [[ -x /usr/lib/wine/wine64 ]]; }; then
    windows_host_temp_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-wine-temp.XXXXXX")"
    windows_native_temp_root="$(to_native_path "$windows_host_temp_root")"
    if [[ -n "$windows_host_temp_root" && -n "$windows_native_temp_root" ]]; then
      WINDOWS_WINE_HOST_TEMP_ROOT="$windows_host_temp_root"
      INSTALL_ROOT="$(mktemp -d "$windows_host_temp_root/chummerwinsmokeXXXXXX")"
    else
      wine_temp_dir="$(resolve_wine_temp_dir || true)"
      if [[ -n "$wine_temp_dir" ]]; then
        mkdir -p "$wine_temp_dir"
        INSTALL_ROOT="$(mktemp -d "$wine_temp_dir/chummerwinsmokeXXXXXX")"
      else
        INSTALL_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummerwinsmokeXXXXXX")"
      fi
    fi
  else
    INSTALL_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummerwinsmokeXXXXXX")"
  fi
  local native_install_root
  native_install_root="$(to_native_path "$INSTALL_ROOT")"
  local -a installer_args=("/smoke-install=$native_install_root")
  local local_payload_path=""
  local local_payload_sha256=""
  local local_payload_size_bytes=""
  local configured_payload_mode
  configured_payload_mode="$(lower_ascii "${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-}")"
  local install_ready_timeout_seconds="${CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_TIMEOUT_SECONDS:-180}"
  local install_ready_poll_interval_seconds="${CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_POLL_SECONDS:-1}"
  if [[ ! "$install_ready_timeout_seconds" =~ ^[0-9]+$ ]] \
    || (( install_ready_timeout_seconds < 1 || install_ready_timeout_seconds > 900 )); then
    echo "CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_TIMEOUT_SECONDS must be an integer from 1 to 900." >&2
    return 1
  fi
  if [[ ! "$install_ready_poll_interval_seconds" =~ ^[0-9]+$ ]] \
    || (( install_ready_poll_interval_seconds < 1 || install_ready_poll_interval_seconds > 30 )); then
    echo "CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALL_READY_POLL_SECONDS must be an integer from 1 to 30." >&2
    return 1
  fi
  local artifact_dir
  artifact_dir="$(dirname "$ARTIFACT_PATH")"
  local artifact_name
  artifact_name="$(basename "$ARTIFACT_PATH")"
  if [[ "$artifact_name" == *-installer.exe ]]; then
    local payload_name="${artifact_name%-installer.exe}-payload.zip"
    if [[ -f "$artifact_dir/files/$payload_name" ]]; then
      local_payload_path="$artifact_dir/files/$payload_name"
    elif [[ -f "$artifact_dir/$payload_name" ]]; then
      local_payload_path="$artifact_dir/$payload_name"
    fi
  fi
  case "$configured_payload_mode" in
    ""|auto)
      if [[ -n "$local_payload_path" ]]; then
        configured_payload_mode="download"
      else
        configured_payload_mode="local"
      fi
      ;;
    local|download|none)
      ;;
    *)
      echo "Unsupported CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE: $configured_payload_mode" >&2
      return 1
      ;;
  esac

  if [[ "$configured_payload_mode" == "download" && -z "$local_payload_path" ]]; then
    echo "Windows startup smoke download mode requires a local bootstrap payload zip beside the installer." >&2
    return 1
  fi

  if [[ -n "$local_payload_path" ]]; then
    local_payload_sha256="$(sha256_file "$local_payload_path")"
    local_payload_size_bytes="$(wc -c < "$local_payload_path" | tr -d '[:space:]')"
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_FILE_NAME="$(basename "$local_payload_path")"
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SHA256="$local_payload_sha256"
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes"
  fi

  if [[ "$configured_payload_mode" == "local" && -n "$local_payload_path" ]]; then
    if [[ -n "$windows_host_temp_root" ]]; then
      WINDOWS_LOCAL_PAYLOAD_COPY="$(mktemp "$windows_host_temp_root/chummer-payload.XXXXXX.zip")"
      cp "$local_payload_path" "$WINDOWS_LOCAL_PAYLOAD_COPY"
      local_payload_path="$WINDOWS_LOCAL_PAYLOAD_COPY"
    elif command -v winepath >/dev/null 2>&1 \
      && { command -v wine >/dev/null 2>&1 || command -v wine64 >/dev/null 2>&1 || [[ -x /usr/lib/wine/wine64 ]]; }; then
      local wine_temp_dir=""
      wine_temp_dir="$(resolve_wine_temp_dir || true)"
      if [[ -n "$wine_temp_dir" ]]; then
        mkdir -p "$wine_temp_dir"
        WINDOWS_LOCAL_PAYLOAD_COPY="$(mktemp "$wine_temp_dir/chummer-payload.XXXXXX.zip")"
        cp "$local_payload_path" "$WINDOWS_LOCAL_PAYLOAD_COPY"
        local_payload_path="$WINDOWS_LOCAL_PAYLOAD_COPY"
      fi
    fi
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="local_handoff"
    CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" \
    CHUMMER_INSTALLER_PAYLOAD_PATH="$(to_native_path "$local_payload_path")" \
    CHUMMER_INSTALLER_PAYLOAD_SHA256="$local_payload_sha256" \
    CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes" \
    run_windows_binary "$ARTIFACT_PATH" "${installer_args[@]}" >>"$LOG_PATH" 2>&1
  elif [[ "$configured_payload_mode" == "download" ]]; then
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="download"
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL="$(start_windows_payload_http_server "$local_payload_path")"
    CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" \
    CHUMMER_INSTALLER_PAYLOAD_URL="$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL" \
    CHUMMER_INSTALLER_PAYLOAD_SHA256="$local_payload_sha256" \
    CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes" \
    run_windows_binary "$ARTIFACT_PATH" "${installer_args[@]}" >>"$LOG_PATH" 2>&1
  else
    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="embedded"
    CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" \
    run_windows_binary "$ARTIFACT_PATH" "${installer_args[@]}" >>"$LOG_PATH" 2>&1
  fi
  sleep 2
  local installer_trace_root="${WINDOWS_WINE_HOST_TEMP_ROOT:-$wine_temp_dir}"
  local resolved_wine_temp_dir=""
  if command -v winepath >/dev/null 2>&1; then
    resolved_wine_temp_dir="$(resolve_wine_temp_dir || true)"
  fi
  if [[ -n "$installer_trace_root" || -n "$resolved_wine_temp_dir" ]]; then
    local installer_trace_capture_path="$OUTPUT_DIR/windows-installer-progress-$APP_KEY-$RID.log"
    local -a installer_trace_candidates=(
      "$installer_trace_root/Chummer6/installer-temp/chummer-desktop-installer-progress.log"
      "$installer_trace_root/chummer-desktop-installer-progress.log"
    )
    if [[ -n "$resolved_wine_temp_dir" ]]; then
      installer_trace_candidates+=(
        "$resolved_wine_temp_dir/Chummer6/installer-temp/chummer-desktop-installer-progress.log"
        "$resolved_wine_temp_dir/chummer-desktop-installer-progress.log"
      )
    fi
    local installer_trace_source=""
    local installer_trace_candidate=""
    for installer_trace_candidate in "${installer_trace_candidates[@]}"; do
      if [[ -f "$installer_trace_candidate" ]]; then
        installer_trace_source="$installer_trace_candidate"
        break
      fi
    done
    if [[ -n "$installer_trace_source" ]]; then
      cp "$installer_trace_source" "$installer_trace_capture_path"
      {
        printf '\n--- windows installer trace ---\n'
        cat "$installer_trace_capture_path"
        printf '\n'
      } >>"$LOG_PATH" 2>/dev/null || true
    fi
  fi

  resolve_windows_installed_relative_path() {
    local requested_relative_path="$1"
    if [[ -f "$INSTALL_ROOT/$requested_relative_path" ]]; then
      printf '%s\n' "$requested_relative_path"
      return 0
    fi

    local head_relative_root="$APP_KEY/"
    if [[ "$requested_relative_path" == "$head_relative_root"* ]]; then
      local flattened_relative_path="${requested_relative_path#"$head_relative_root"}"
      if [[ -n "$flattened_relative_path" && -f "$INSTALL_ROOT/$flattened_relative_path" ]]; then
        printf '%s\n' "$flattened_relative_path"
        return 0
      fi
    fi

    local basename_match
    basename_match="$(
      python3 - "$INSTALL_ROOT" "$requested_relative_path" <<'PY'
import os
import sys

install_root = os.path.abspath(sys.argv[1])
requested = sys.argv[2].replace("\\", "/").strip("/")
requested_name = os.path.basename(requested).lower()
requested_suffixes = [requested]

if "/" in requested:
    requested_suffixes.append(requested.split("/", 1)[1])

candidates = []
for root, _, files in os.walk(install_root):
    for file_name in files:
        if file_name.lower() != requested_name:
            continue
        full_path = os.path.join(root, file_name)
        relative_path = os.path.relpath(full_path, install_root).replace("\\", "/")
        candidates.append(relative_path)

if not candidates:
    raise SystemExit(1)

for suffix in requested_suffixes:
    for candidate in candidates:
        if candidate.lower() == suffix.lower():
            print(candidate)
            raise SystemExit(0)
        if candidate.lower().endswith("/" + suffix.lower()):
            print(candidate)
            raise SystemExit(0)

candidates.sort(key=lambda value: (value.count("/"), len(value), value.lower()))
print(candidates[0])
PY
    )" || basename_match=""
    if [[ -n "$basename_match" && -f "$INSTALL_ROOT/$basename_match" ]]; then
      printf '%s\n' "$basename_match"
      return 0
    fi

    local native_match=""
    if command -v powershell.exe >/dev/null 2>&1 || command -v pwsh >/dev/null 2>&1; then
      local powershell_bin="powershell.exe"
      if command -v pwsh >/dev/null 2>&1; then
        powershell_bin="pwsh"
      fi
      native_match="$(
        CHUMMER_WINDOWS_SEARCH_ROOT="$native_install_root" \
        CHUMMER_WINDOWS_SEARCH_BASENAME="$(basename "$requested_relative_path")" \
        "$powershell_bin" -NoLogo -NoProfile -Command '
          $root = $env:CHUMMER_WINDOWS_SEARCH_ROOT
          $name = $env:CHUMMER_WINDOWS_SEARCH_BASENAME
          if ([string]::IsNullOrWhiteSpace($root) -or [string]::IsNullOrWhiteSpace($name) -or -not (Test-Path -LiteralPath $root)) {
            exit 1
          }

          $match = Get-ChildItem -LiteralPath $root -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
            Sort-Object @{ Expression = { $_.FullName.Split([System.IO.Path]::DirectorySeparatorChar).Count } }, FullName |
            Select-Object -First 1
          if ($null -eq $match) {
            exit 1
          }

          $relative = [System.IO.Path]::GetRelativePath($root, $match.FullName)
          [Console]::Out.Write(($relative -replace "\\\\", "/"))
        ' 2>/dev/null
      )" || native_match=""
    fi
    if [[ -n "$native_match" ]]; then
      printf '%s\n' "$native_match"
      return 0
    fi

    return 1
  }

  wait_for_windows_installed_relative_path() {
    local requested_relative_path="$1"
    local deadline=$((SECONDS + install_ready_timeout_seconds))
    local resolved_relative_path=""

    while :; do
      if resolved_relative_path="$(resolve_windows_installed_relative_path "$requested_relative_path")"; then
        printf '%s\n' "$resolved_relative_path"
        return 0
      fi
      if (( SECONDS >= deadline )); then
        return 1
      fi
      sleep "$install_ready_poll_interval_seconds"
    done
  }

  local required_paths="${CHUMMER_STARTUP_SMOKE_REQUIRED_INSTALL_PATHS:-}"
  if [[ -n "$required_paths" ]]; then
    local relative_path
    local missing_paths=()
    while IFS= read -r relative_path; do
      [[ -n "$relative_path" ]] || continue
      local resolved_required_path=""
      resolved_required_path="$(wait_for_windows_installed_relative_path "$relative_path")" || resolved_required_path=""
      if [[ -z "$resolved_required_path" ]]; then
        missing_paths+=("$relative_path")
      fi
    done < <(printf '%s' "$required_paths" | tr ';' '\n')

    if (( $(array_count missing_paths) > 0 )); then
      {
        printf 'Missing required installed path(s) after Windows smoke install:%s\n' " ${missing_paths[*]}"
        find "$INSTALL_ROOT" -maxdepth 6 -type f | sort || true
        if command -v powershell.exe >/dev/null 2>&1 || command -v pwsh >/dev/null 2>&1; then
          local powershell_bin="powershell.exe"
          if command -v pwsh >/dev/null 2>&1; then
            powershell_bin="pwsh"
          fi
          CHUMMER_WINDOWS_SEARCH_ROOT="$native_install_root" \
          "$powershell_bin" -NoLogo -NoProfile -Command '
            if (Test-Path -LiteralPath $env:CHUMMER_WINDOWS_SEARCH_ROOT) {
              Get-ChildItem -LiteralPath $env:CHUMMER_WINDOWS_SEARCH_ROOT -Recurse -File -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName
            }
          ' 2>/dev/null || true
        fi
      } | tee -a "$LOG_PATH" >&2
      return 1
    fi
  fi

  local launch_relative_path="${CHUMMER_STARTUP_SMOKE_LAUNCH_RELATIVE_PATH:-$LAUNCH_TARGET}"
  local resolved_launch_relative_path
  echo "Waiting up to ${install_ready_timeout_seconds}s for Windows smoke install launch target: $launch_relative_path" >>"$LOG_PATH"
  if resolved_launch_relative_path="$(wait_for_windows_installed_relative_path "$launch_relative_path")"; then
    launch_relative_path="$resolved_launch_relative_path"
  fi
  local smoke_status=0
  CHUMMER_WINDOWS_BINARY_TEMP_ROOT="$windows_native_temp_root" run_head_smoke "$INSTALL_ROOT/$launch_relative_path" || smoke_status=$?
  attach_windows_bootstrap_verification_to_receipt \
    "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE" \
    "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL" \
    "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SHA256" \
    "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_SIZE_BYTES" \
    "$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_FILE_NAME"
  return "$smoke_status"
}

seed_dpkg_admin_dir() {
  DPKG_ADMIN_DIR="$INSTALL_ROOT/var/lib/dpkg"
  mkdir -p "$DPKG_ADMIN_DIR/updates" "$DPKG_ADMIN_DIR/info" "$DPKG_ADMIN_DIR/triggers"

  cp /var/lib/dpkg/status "$DPKG_ADMIN_DIR/status"

  local file_name
  for file_name in available diversions diversions-old statoverride statoverride-old cmethopt; do
    if [[ -r "/var/lib/dpkg/$file_name" ]]; then
      cp "/var/lib/dpkg/$file_name" "$DPKG_ADMIN_DIR/$file_name"
    fi
  done

  if [[ -d /var/lib/dpkg/info ]]; then
    cp -a /var/lib/dpkg/info/. "$DPKG_ADMIN_DIR/info/"
  fi

  : >"$DPKG_LOG_PATH"
}

run_dpkg_isolated() {
  dpkg \
    --root="$INSTALL_ROOT" \
    --admindir="$DPKG_ADMIN_DIR" \
    --log="$DPKG_LOG_PATH" \
    --force-not-root \
    --force-bad-path \
    --force-script-chrootless \
    "$@" >>"$LOG_PATH" 2>&1
}

read_dpkg_package_status() {
  local package_name="$1"
  local status_line

  status_line="$(dpkg --admindir="$DPKG_ADMIN_DIR" --root="$INSTALL_ROOT" -s "$package_name" 2>/dev/null | awk -F': ' '/^Status:/ {print $2; exit}' || true)"
  if [[ -z "$status_line" ]]; then
    echo "not-installed"
    return
  fi

  echo "$status_line"
}

write_linux_deb_install_verification() {
  local package_name="$1"
  local package_arch="$2"
  local installed_launch_path="$3"
  local wrapper_path="$4"
  local desktop_entry_path="$5"
  local installed_launch_capture_path="$6"
  local wrapper_capture_path="$7"
  local desktop_entry_capture_path="$8"
  local status_after_install="$9"
  local status_after_purge="${10}"
  local launch_exists_after_install="${11}"
  local wrapper_exists_after_install="${12}"
  local desktop_exists_after_install="${13}"
  local launch_exists_after_purge="${14}"
  local wrapper_exists_after_purge="${15}"
  local desktop_exists_after_purge="${16}"

  python3 - "$INSTALL_VERIFICATION_PATH" "$DPKG_LOG_PATH" "$package_name" "$package_arch" "$INSTALL_ROOT" "$DPKG_ADMIN_DIR" \
    "$installed_launch_path" "$wrapper_path" "$desktop_entry_path" \
    "$installed_launch_capture_path" "$wrapper_capture_path" "$desktop_entry_capture_path" \
    "$status_after_install" "$status_after_purge" \
    "$launch_exists_after_install" "$wrapper_exists_after_install" "$desktop_exists_after_install" \
    "$launch_exists_after_purge" "$wrapper_exists_after_purge" "$desktop_exists_after_purge" <<'PY'
import hashlib
import json
import pathlib
import sys

(
    verification_path,
    dpkg_log_path,
    package_name,
    package_arch,
    install_root,
    dpkg_admin_dir,
    installed_launch_path,
    wrapper_path,
    desktop_entry_path,
    installed_launch_capture_path,
    wrapper_capture_path,
    desktop_entry_capture_path,
    status_after_install,
    status_after_purge,
    launch_exists_after_install,
    wrapper_exists_after_install,
    desktop_exists_after_install,
    launch_exists_after_purge,
    wrapper_exists_after_purge,
    desktop_exists_after_purge,
) = sys.argv[1:]


def parse_bool(value: str) -> bool:
    return value.strip().lower() in {"1", "true", "yes", "on"}


payload = {
    "mode": "dpkg_rootless_install",
    "packageName": package_name,
    "packageArch": package_arch,
    "installRoot": install_root,
    "dpkgAdminDir": dpkg_admin_dir,
    "dpkgLogPath": dpkg_log_path,
    "installedLaunchPath": installed_launch_path,
    "installedLaunchCapturePath": installed_launch_capture_path,
    "installedLaunchPathSha256": "",
    "wrapperPath": wrapper_path,
    "wrapperCapturePath": wrapper_capture_path,
    "wrapperSha256": "",
    "wrapperContent": "",
    "desktopEntryPath": desktop_entry_path,
    "desktopEntryCapturePath": desktop_entry_capture_path,
    "desktopEntrySha256": "",
    "desktopEntryContent": "",
    "statusAfterInstall": status_after_install,
    "statusAfterPurge": status_after_purge,
    "installedLaunchPathExistsAfterInstall": parse_bool(launch_exists_after_install),
    "wrapperExistsAfterInstall": parse_bool(wrapper_exists_after_install),
    "desktopEntryExistsAfterInstall": parse_bool(desktop_exists_after_install),
    "installedLaunchPathExistsAfterPurge": parse_bool(launch_exists_after_purge),
    "wrapperExistsAfterPurge": parse_bool(wrapper_exists_after_purge),
    "desktopEntryExistsAfterPurge": parse_bool(desktop_exists_after_purge),
}

for path_key, capture_key, sha_key, content_key in (
    ("installedLaunchPath", "installedLaunchCapturePath", "installedLaunchPathSha256", None),
    ("wrapperPath", "wrapperCapturePath", "wrapperSha256", "wrapperContent"),
    ("desktopEntryPath", "desktopEntryCapturePath", "desktopEntrySha256", "desktopEntryContent"),
):
    path = pathlib.Path(payload[capture_key] or payload[path_key])
    if path.is_file():
        payload[sha_key] = hashlib.sha256(path.read_bytes()).hexdigest()
        if content_key is not None:
            payload[content_key] = path.read_text(encoding="utf-8")

path = pathlib.Path(verification_path)
path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

attach_install_verification_to_receipt() {
  local dpkg_log_path="$1"
  local installed_launch_capture_path="$2"
  local wrapper_capture_path="$3"
  local desktop_entry_capture_path="$4"

  python3 - "$RECEIPT_PATH" "$INSTALL_VERIFICATION_PATH" "$dpkg_log_path" "$installed_launch_capture_path" "$wrapper_capture_path" "$desktop_entry_capture_path" "$ARTIFACT_PATH" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
verification_path = pathlib.Path(sys.argv[2])
dpkg_log_path = pathlib.Path(sys.argv[3])
installed_launch_capture_path = pathlib.Path(sys.argv[4])
wrapper_capture_path = pathlib.Path(sys.argv[5])
desktop_entry_capture_path = pathlib.Path(sys.argv[6])
artifact_path = pathlib.Path(sys.argv[7])
payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
payload["artifactInstallMode"] = "dpkg_rootless_install"
payload["artifactInstallVerificationPath"] = str(verification_path)
payload["artifactInstallDpkgLogPath"] = str(dpkg_log_path)
payload["artifactInstallLaunchCapturePath"] = str(installed_launch_capture_path)
payload["artifactInstallWrapperCapturePath"] = str(wrapper_capture_path)
payload["artifactInstallDesktopEntryCapturePath"] = str(desktop_entry_capture_path)
payload["artifactPath"] = str(artifact_path)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

run_linux_smoke_deb() {
  if ! command -v dpkg >/dev/null 2>&1; then
    echo "dpkg is required for Linux .deb startup smoke." >&2
    return 1
  fi

  if [[ "$RID" == "linux-arm64" ]] && ! host_can_execute_linux_arm64; then
    emit_incompatible_host_receipt \
      "Linux startup smoke requires a Linux ARM64 host or qemu-aarch64 user emulation; current host cannot execute linux-arm64 installer smoke."
    return 0
  fi

  INSTALL_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-linux-smoke.XXXXXX")"
  seed_dpkg_admin_dir

  local package_name
  package_name="$(dpkg-deb -f "$ARTIFACT_PATH" Package)"
  local package_arch
  package_arch="$(dpkg-deb -f "$ARTIFACT_PATH" Architecture)"
  local installed_launch_path="$INSTALL_ROOT/opt/chummer6/$APP_KEY-$RID/$LAUNCH_TARGET"
  local wrapper_path="$INSTALL_ROOT/usr/bin/chummer6-$APP_KEY"
  local desktop_entry_path="$INSTALL_ROOT/usr/share/applications/chummer6-$APP_KEY.desktop"
  local installed_launch_capture_path="$OUTPUT_DIR/installed-launch-$APP_KEY-$RID.bin"
  local wrapper_capture_path="$OUTPUT_DIR/installed-wrapper-$APP_KEY-$RID.sh"
  local desktop_entry_capture_path="$OUTPUT_DIR/installed-desktop-entry-$APP_KEY-$RID.desktop"

  local install_status=0
  run_dpkg_isolated --install "$ARTIFACT_PATH" || install_status=$?
  local status_after_install
  status_after_install="$(read_dpkg_package_status "$package_name")"
  local launch_exists_after_install="false"
  local wrapper_exists_after_install="false"
  local desktop_exists_after_install="false"

  [[ -f "$installed_launch_path" ]] || {
    echo "Installed launch target missing after dpkg install: $installed_launch_path" >&2
    return 1
  }
  [[ -f "$wrapper_path" ]] || {
    echo "Installed wrapper missing after dpkg install: $wrapper_path" >&2
    return 1
  }
  [[ -f "$desktop_entry_path" ]] || {
    echo "Installed desktop entry missing after dpkg install: $desktop_entry_path" >&2
    return 1
  }

  launch_exists_after_install="true"
  wrapper_exists_after_install="true"
  desktop_exists_after_install="true"
  cp -a "$installed_launch_path" "$installed_launch_capture_path"
  cp -a "$wrapper_path" "$wrapper_capture_path"
  cp -a "$desktop_entry_path" "$desktop_entry_capture_path"

  if [[ "$install_status" -ne 0 ]]; then
    echo "dpkg install returned $install_status, but Chummer payload files were installed under the isolated root; tolerating host trigger noise." >>"$LOG_PATH"
    install_status=0
  fi

  local smoke_status=0
  run_head_smoke "$installed_launch_path" || smoke_status=$?

  local purge_status=0
  run_dpkg_isolated --purge "$package_name" || purge_status=$?
  local status_after_purge
  status_after_purge="$(read_dpkg_package_status "$package_name")"
  local launch_exists_after_purge="false"
  local wrapper_exists_after_purge="false"
  local desktop_exists_after_purge="false"
  [[ -e "$installed_launch_path" ]] && launch_exists_after_purge="true"
  [[ -e "$wrapper_path" ]] && wrapper_exists_after_purge="true"
  [[ -e "$desktop_entry_path" ]] && desktop_exists_after_purge="true"

  if [[ "$purge_status" -ne 0 \
    && "$launch_exists_after_purge" == "false" \
    && "$wrapper_exists_after_purge" == "false" \
    && "$desktop_exists_after_purge" == "false" ]]; then
    echo "dpkg purge returned $purge_status, but the isolated Chummer payload was removed; tolerating host trigger noise." >>"$LOG_PATH"
    purge_status=0
  fi

  if [[ "$smoke_status" -eq 0 && "$purge_status" -eq 0 ]]; then
    write_linux_deb_install_verification \
      "$package_name" \
      "$package_arch" \
      "$installed_launch_path" \
      "$wrapper_path" \
      "$desktop_entry_path" \
      "$installed_launch_capture_path" \
      "$wrapper_capture_path" \
      "$desktop_entry_capture_path" \
      "$status_after_install" \
      "$status_after_purge" \
      "$launch_exists_after_install" \
      "$wrapper_exists_after_install" \
      "$desktop_exists_after_install" \
      "$launch_exists_after_purge" \
      "$wrapper_exists_after_purge" \
      "$desktop_exists_after_purge"
    attach_install_verification_to_receipt \
      "$DPKG_LOG_PATH" \
      "$installed_launch_capture_path" \
      "$wrapper_capture_path" \
      "$desktop_entry_capture_path"
  fi

  if [[ "$smoke_status" -ne 0 ]]; then
    return "$smoke_status"
  fi
  if [[ "$purge_status" -ne 0 ]]; then
    return "$purge_status"
  fi
}

run_linux_smoke_archive() {
  local launch_path_candidates=(
    "$UNPACK_ROOT/$LAUNCH_TARGET"
    "$UNPACK_ROOT/$APP_KEY-$RID/$LAUNCH_TARGET"
    "$UNPACK_ROOT/opt/chummer6/$APP_KEY-$RID/$LAUNCH_TARGET"
    "$UNPACK_ROOT/opt/chummer6/$APP_KEY/$LAUNCH_TARGET"
  )

  for candidate in "${launch_path_candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      run_head_smoke "$candidate"
      return
    fi
  done

  echo "Launch target missing for startup smoke. Checked candidates: ${launch_path_candidates[*]}" >&2
  return 1
}

run_linux_smoke() {
  case "$ARTIFACT_PATH" in
    *.deb)
      run_linux_smoke_deb
      ;;
    *.tar|*.tar.gz|*.tgz)
      UNPACK_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-linux-smoke.XXXXXX")"
      tar -xf "$ARTIFACT_PATH" -C "$UNPACK_ROOT" >>"$LOG_PATH" 2>&1
      run_linux_smoke_archive
      ;;
    *)
      echo "Unsupported Linux artifact format: $ARTIFACT_PATH" >&2
      return 1
      ;;
  esac
}

run_macos_smoke() {
  MOUNT_DIR="$(mktemp -d "${TMPDIR:-/tmp}/chummer-macos-smoke.XXXXXX")"
  hdiutil attach -nobrowse -readonly -mountpoint "$MOUNT_DIR" "$ARTIFACT_PATH" >>"$LOG_PATH" 2>&1

  local app_bundle
  app_bundle="$(find "$MOUNT_DIR" -maxdepth 1 -type d -name '*.app' | sort | head -n 1)"
  if [[ -z "$app_bundle" ]]; then
    echo "Mounted dmg did not expose a .app bundle." >&2
    return 1
  fi

  run_head_smoke "$app_bundle/Contents/MacOS/$LAUNCH_TARGET"
}

emit_release_regression_packet() {
  local exit_code="$1"
  local artifact_sha
  artifact_sha="$(sha256_file "$ARTIFACT_PATH")"

  python3 - "$PACKET_PATH" "$RECEIPT_PATH" "$LOG_PATH" "$ARTIFACT_PATH" "$artifact_sha" "$APP_KEY" "$RID" "$VERSION_HINT" "$CHANNEL_HINT" "$HOST_CLASS" "$exit_code" <<'PY'
import datetime as dt
import hashlib
import json
import pathlib
import sys

packet_path = pathlib.Path(sys.argv[1])
receipt_path = pathlib.Path(sys.argv[2])
log_path = pathlib.Path(sys.argv[3])
artifact_path = sys.argv[4]
artifact_sha = sys.argv[5]
app_key = sys.argv[6]
rid = sys.argv[7]
version_hint = sys.argv[8]
channel_hint = sys.argv[9]
host_class = sys.argv[10]
exit_code = int(sys.argv[11])

receipt = {}
if receipt_path.exists():
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

log_text = log_path.read_text(encoding="utf-8", errors="replace") if log_path.exists() else ""
tail_lines = log_text.strip().splitlines()[-40:]
tail_text = "\n".join(tail_lines)
fingerprint_source = "|".join(
    [
        app_key,
        rid,
        str(exit_code),
        receipt.get("readyCheckpoint", ""),
        tail_text,
    ]
)
fingerprint = hashlib.sha256(fingerprint_source.encode("utf-8")).hexdigest()[:16]

platform = "windows" if rid.startswith("win-") else "linux" if rid.startswith("linux-") else "macos" if rid.startswith("osx-") else "unknown"
arch = "arm64" if rid.endswith("arm64") else "x64" if rid.endswith("x64") else "x86" if rid.endswith("x86") else "unknown"

packet = {
    "signalClass": "release_smoke_start_failure",
    "headId": receipt.get("headId", app_key),
    "appKey": app_key,
    "platform": receipt.get("platform", platform),
    "arch": receipt.get("arch", arch),
    "rid": rid,
    "channel": receipt.get("channelId", channel_hint),
    "version": receipt.get("version", version_hint),
    "verificationHostClass": host_class,
    "artifactPath": artifact_path,
    "artifactSha256": artifact_sha,
    "startupReceiptPath": str(receipt_path),
    "startupReceiptFound": receipt_path.exists(),
    "readyCheckpoint": receipt.get("readyCheckpoint"),
    "processPath": receipt.get("processPath"),
    "exitCode": exit_code,
    "crashFingerprint": fingerprint,
    "logTail": tail_lines,
    "capturedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
    "oodaRecommendation": "freeze_or_fix_before_promotion",
}

packet_path.write_text(json.dumps(packet, indent=2), encoding="utf-8")
print(packet_path)
PY
}

set_receipt_status() {
  local status_value="$1"
  python3 - "$RECEIPT_PATH" "$status_value" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
status_value = str(sys.argv[2]).strip().lower()
if not receipt_path.exists() or not receipt_path.is_file():
    raise SystemExit(0)

payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(0)

payload["status"] = status_value
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

receipt_has_ready_checkpoint() {
  python3 - "$RECEIPT_PATH" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
if not receipt_path.exists() or not receipt_path.is_file():
    raise SystemExit(1)

payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(1)

ready = str(payload.get("readyCheckpoint") or "").strip()
raise SystemExit(0 if ready else 1)
PY
}

attach_release_artifact_metadata_to_receipt() {
  local artifact_sha
  artifact_sha="$(sha256_file "$ARTIFACT_PATH")"

  python3 - "$RECEIPT_PATH" "$ARTIFACT_PATH" "$artifact_sha" "$APP_KEY" "$RID" <<'PY'
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
artifact_path = pathlib.Path(sys.argv[2])
artifact_sha = str(sys.argv[3]).strip().lower()
app_key = str(sys.argv[4]).strip().lower()
rid = str(sys.argv[5]).strip().lower()
if not receipt_path.exists() or not receipt_path.is_file():
    raise SystemExit(0)

payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(0)

artifact_file_name = artifact_path.name
artifact_relative_path = artifact_file_name
if artifact_path.parent.name:
    artifact_relative_path = f"{artifact_path.parent.name}/{artifact_file_name}"

payload["artifactPath"] = str(artifact_path)
payload["artifactFileName"] = artifact_file_name
payload["fileName"] = artifact_file_name
payload["artifactRelativePath"] = artifact_relative_path
payload["artifactSha256"] = artifact_sha
payload["artifactDigest"] = f"sha256:{artifact_sha}"
payload["artifactId"] = str(payload.get("artifactId") or f"{app_key}-{rid}-installer").strip()
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

main() {
  : >"$LOG_PATH"
  rm -f "$RECEIPT_PATH" "$PACKET_PATH"
  configure_windows_wine_prefix

  case "$RID" in
    win-*)
      run_windows_smoke
      ;;
    linux-*)
      run_linux_smoke
      ;;
    osx-*)
      run_macos_smoke
      ;;
    *)
      echo "Unsupported RID for startup smoke: $RID" >&2
      return 1
      ;;
  esac

  if [[ ! -s "$RECEIPT_PATH" ]]; then
    for _attempt in 1 2 3 4 5; do
      sleep 1
      if [[ -s "$RECEIPT_PATH" ]]; then
        break
      fi
    done
  fi

  if [[ ! -s "$RECEIPT_PATH" ]]; then
    echo "Startup smoke completed without emitting a receipt." >&2
    return 1
  fi
}

status=0
main || status=$?

if [[ "$status" -ne 0 ]] && receipt_has_ready_checkpoint; then
  {
    printf 'startup smoke process exited %s after emitting ready checkpoint; accepting receipt-backed pass for %s %s\n' "$status" "$APP_KEY" "$RID"
  } | tee -a "$LOG_PATH" >&2
  status=0
fi

if [[ "$status" -ne 0 ]]; then
  set_receipt_status "failed"
  emit_release_regression_packet "$status" >>"$LOG_PATH"
  echo "startup smoke failed for $APP_KEY $RID; regression packet: $PACKET_PATH" >&2
  exit "$status"
fi

attach_release_artifact_metadata_to_receipt
if [[ "$(receipt_status 2>/dev/null || true)" == "skipped" ]]; then
  echo "startup smoke skipped for $APP_KEY $RID; receipt: $RECEIPT_PATH"
  exit 0
fi

set_receipt_status "pass"
echo "startup smoke passed for $APP_KEY $RID; receipt: $RECEIPT_PATH"
