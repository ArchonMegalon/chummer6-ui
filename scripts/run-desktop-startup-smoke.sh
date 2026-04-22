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
BUNDLE_EXTRACT_ROOT=""
RUNTIME_HOME=""
DPKG_ADMIN_DIR=""
DPKG_LOG_PATH="$OUTPUT_DIR/dpkg-$APP_KEY-$RID.log"
INSTALL_VERIFICATION_PATH="$OUTPUT_DIR/install-verification-$APP_KEY-$RID.json"

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

  if [[ -n "$BUNDLE_EXTRACT_ROOT" && -d "$BUNDLE_EXTRACT_ROOT" ]]; then
    rm -rf "$BUNDLE_EXTRACT_ROOT"
  fi

  if [[ -n "$RUNTIME_HOME" && -d "$RUNTIME_HOME" ]]; then
    rm -rf "$RUNTIME_HOME"
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

  if [[ -z "$BUNDLE_EXTRACT_ROOT" ]]; then
    BUNDLE_EXTRACT_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-startup-bundle.XXXXXX")"
  fi
  if [[ -z "$RUNTIME_HOME" ]]; then
    RUNTIME_HOME="$(mktemp -d "${TMPDIR:-/tmp}/chummer-startup-home.XXXXXX")"
  fi

  CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$receipt_path" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$packet_path" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:${artifact_sha}" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="$HOST_CLASS" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$VERSION_HINT" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_RID="$RID" \
  CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
  DOTNET_BUNDLE_EXTRACT_BASE_DIR="$BUNDLE_EXTRACT_ROOT" \
  HOME="$RUNTIME_HOME" \
  XDG_CONFIG_HOME="$RUNTIME_HOME/.config" \
  XDG_DATA_HOME="$RUNTIME_HOME/.local/share" \
  XDG_STATE_HOME="$RUNTIME_HOME/.local/state" \
  XDG_CACHE_HOME="$RUNTIME_HOME/.cache" \
  "$launch_path" --startup-smoke >>"$LOG_PATH" 2>&1
}

run_windows_smoke() {
  INSTALL_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/chummer-win-smoke.XXXXXX")"
  local native_install_root
  native_install_root="$(to_native_path "$INSTALL_ROOT")"
  "$ARTIFACT_PATH" --smoke-install "$native_install_root" >>"$LOG_PATH" 2>&1

  local required_paths="${CHUMMER_STARTUP_SMOKE_REQUIRED_INSTALL_PATHS:-}"
  if [[ -n "$required_paths" ]]; then
    local relative_path
    local missing_paths=()
    while IFS= read -r relative_path; do
      [[ -n "$relative_path" ]] || continue
      if [[ ! -f "$INSTALL_ROOT/$relative_path" ]]; then
        missing_paths+=("$relative_path")
      fi
    done < <(printf '%s' "$required_paths" | tr ';' '\n')

    if (( ${#missing_paths[@]} > 0 )); then
      printf 'Missing required installed path(s) after Windows smoke install:%s\n' " ${missing_paths[*]}" >&2
      return 1
    fi
  fi

  run_head_smoke "$INSTALL_ROOT/$LAUNCH_TARGET"
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

  run_dpkg_isolated --install "$ARTIFACT_PATH"
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
  set_receipt_status "failed"
  emit_release_regression_packet "$status" >>"$LOG_PATH"
  echo "startup smoke failed for $APP_KEY $RID; regression packet: $PACKET_PATH" >&2
  exit "$status"
fi

set_receipt_status "pass"
echo "startup smoke passed for $APP_KEY $RID; receipt: $RECEIPT_PATH"
