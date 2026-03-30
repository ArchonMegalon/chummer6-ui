#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

ARTIFACT_PATH="$(realpath "${1:?artifact path is required}")"
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

cleanup() {
  if [[ -n "$MOUNT_DIR" ]]; then
    hdiutil detach "$MOUNT_DIR" >/dev/null 2>&1 || true
  fi

  if [[ -n "$UNPACK_ROOT" && -d "$UNPACK_ROOT" ]]; then
    rm -rf "$UNPACK_ROOT"
  fi

  if [[ -n "$INSTALL_ROOT" && -d "$INSTALL_ROOT" ]]; then
    rm -rf "$INSTALL_ROOT"
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
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
    return
  fi

  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
    return
  fi

  python3 - "$1" <<'PY'
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

to_native_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
    return
  fi

  echo "$1"
}

run_head_smoke() {
  local launch_path="$1"
  local receipt_path="$RECEIPT_PATH"
  local packet_path="$PACKET_PATH"
  local artifact_sha
  artifact_sha="$(sha256_file "$ARTIFACT_PATH")"

  if [[ ! -f "$launch_path" ]]; then
    echo "Launch target missing for startup smoke: $launch_path" >&2
    return 1
  fi

  if command -v cygpath >/dev/null 2>&1; then
    receipt_path="$(to_native_path "$receipt_path")"
    packet_path="$(to_native_path "$packet_path")"
  fi

  CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$receipt_path" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$packet_path" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="$HOST_CLASS" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
  "$launch_path" --startup-smoke >>"$LOG_PATH" 2>&1
}

run_windows_smoke() {
  INSTALL_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-win-smoke.XXXXXX")"
  local native_install_root
  native_install_root="$(to_native_path "$INSTALL_ROOT")"
  "$ARTIFACT_PATH" --smoke-install "$native_install_root" >>"$LOG_PATH" 2>&1
  run_head_smoke "$INSTALL_ROOT/$LAUNCH_TARGET"
}

run_linux_smoke() {
  UNPACK_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-linux-smoke.XXXXXX")"
  dpkg-deb -x "$ARTIFACT_PATH" "$UNPACK_ROOT" >>"$LOG_PATH" 2>&1
  run_head_smoke "$UNPACK_ROOT/opt/chummer6/$APP_KEY-$RID/$LAUNCH_TARGET"
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
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))

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

main() {
  : >"$LOG_PATH"
  rm -f "$RECEIPT_PATH" "$PACKET_PATH"

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
    echo "Startup smoke completed without emitting a receipt." >&2
    return 1
  fi
}

status=0
main || status=$?

if [[ "$status" -ne 0 ]]; then
  emit_release_regression_packet "$status" >>"$LOG_PATH"
  echo "startup smoke failed for $APP_KEY $RID; regression packet: $PACKET_PATH" >&2
  exit "$status"
fi

echo "startup smoke passed for $APP_KEY $RID; receipt: $RECEIPT_PATH"
