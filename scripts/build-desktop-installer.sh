#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0 || {
  printf '%s\n' "Could not disable core dumps for Windows signing." >&2
  exit 2
}

TRUSTED_BASH_PATH="/bin/bash"
if [[ "${3:-}" == win-* ]]; then
  [[ -n "${BASH:-}" \
    && -f "$BASH" \
    && ! -L "$BASH" \
    && -x "$BASH" \
    && -f "$TRUSTED_BASH_PATH" \
    && ! -L "$TRUSTED_BASH_PATH" \
    && -x "$TRUSTED_BASH_PATH" \
    && "$BASH" -ef "$TRUSTED_BASH_PATH" ]] || {
    printf '%s\n' "Trusted Bash interpreter is unavailable for Windows signing." >&2
    exit 2
  }
fi

SIGNING_HANDOFF_MAGIC="chummer6-ui.windows-signing-handoff.v1"
SIGNING_HANDOFF_MAX_BYTES=16384
SIGNING_HOST_MAX_BYTES=2048
SIGNING_API_KEY_MAX_BYTES=4096
SIGNING_CLIENT_CERT_FILE_MAX_BYTES=4096
SIGNING_CLIENT_CERT_PASSWORD_MAX_BYTES=4096
SIGNING_APPROVED_JAVA_SHA256="fd85538801d8ca61d3558c87a57a600e1868d8ac9e918d0860dd64281b548643"
SIGNING_APPROVED_JAVA_TREE_SHA256="3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5"
SIGNING_APPROVED_JSIGN_SHA256="602a51c3545a6dc4fb99bd2ea7152b26d1345916d0c93ddfbd5936cb735af91c"
SIGNING_APPROVED_JAVA_HOME="/home/tibor/.local/share/ea-tools/chummer-signing/java/temurin-21.0.11+10"
SIGNING_APPROVED_JAVA_BIN="$SIGNING_APPROVED_JAVA_HOME/bin/java"
SIGNING_APPROVED_JSIGN_JAR="/home/tibor/.local/share/ea-tools/chummer-signing/jsign/7.5/jsign-7.5.jar"
SIGNING_APPROVED_DOTNET_ROOT="/usr/lib/dotnet"
SIGNING_APPROVED_DOTNET_BIN="/usr/lib/dotnet/dotnet"
SIGNING_APPROVED_DOTNET_SHA256="a2e03e682b5ba32303077bc5ed95ca3dd6b57b6d55d09491b67444644e211940"
SIGNING_APPROVED_DOTNET_TREE_SHA256="ba27f662b28bfe7b938b8c862c41e07739db8182a42481a6a0cc5b385ec5f2be"
SIGNING_APPROVED_SIGNER_DLL_NAME="Chummer.KeyLockerSigner.dll"
SIGNING_APPROVED_SIGNER_SDK_PIN_SHA256="878939d8aec1375674ef0508026fc15101ac15f31807d97651c6f38b99feb5dd"
SIGNING_SM_HOST=""
SIGNING_SM_API_KEY=""
SIGNING_SM_CLIENT_CERT_FILE=""
SIGNING_SM_CLIENT_CERT_PASSWORD=""
SIGNING_HANDOFF_CAPTURED=0
export -n \
  SIGNING_SM_HOST \
  SIGNING_SM_API_KEY \
  SIGNING_SM_CLIENT_CERT_FILE \
  SIGNING_SM_CLIENT_CERT_PASSWORD

early_signing_die() {
  printf '%s\n' "Windows signing handoff is invalid." >&2
  exit 2
}

require_bounded_handoff_field() {
  local value="${1-}"
  local maximum="${2-0}"
  local forbid_pipe="${3-0}"
  local LC_ALL=C
  [[ -n "$value" && "${#value}" -le "$maximum" ]] || early_signing_die
  [[ "$value" != *[[:cntrl:]]* ]] || early_signing_die
  if [[ "$forbid_pipe" == "1" && "$value" == *"|"* ]]; then
    early_signing_die
  fi
}

consume_signing_handoff_before_external_commands() {
  local rid="${1-}"
  local signing_handoff_fd="${CHUMMER_WINDOWS_SIGNING_HANDOFF_FD:-}"
  unset CHUMMER_WINDOWS_SIGNING_HANDOFF_FD BASH_ENV ENV

  if [[ "$rid" != win-* || "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" != "digicert_keylocker_linux_jsign" ]]; then
    [[ -z "$signing_handoff_fd" ]] || early_signing_die
    return 0
  fi
  [[ "$signing_handoff_fd" =~ ^[0-9]+$ ]] || early_signing_die

  local magic=""
  local trailing=""
  local LC_ALL=C
  IFS= read -r -d '' -u "$signing_handoff_fd" magic || early_signing_die
  IFS= read -r -d '' -u "$signing_handoff_fd" SIGNING_SM_HOST || early_signing_die
  IFS= read -r -d '' -u "$signing_handoff_fd" SIGNING_SM_API_KEY || early_signing_die
  IFS= read -r -d '' -u "$signing_handoff_fd" SIGNING_SM_CLIENT_CERT_FILE || early_signing_die
  IFS= read -r -d '' -u "$signing_handoff_fd" SIGNING_SM_CLIENT_CERT_PASSWORD || early_signing_die
  if IFS= read -r -d '' -u "$signing_handoff_fd" trailing || [[ -n "$trailing" ]]; then
    early_signing_die
  fi
  exec {signing_handoff_fd}<&-

  [[ "$magic" == "$SIGNING_HANDOFF_MAGIC" ]] || early_signing_die
  require_bounded_handoff_field "$SIGNING_SM_HOST" "$SIGNING_HOST_MAX_BYTES" 0
  require_bounded_handoff_field "$SIGNING_SM_API_KEY" "$SIGNING_API_KEY_MAX_BYTES" 1
  require_bounded_handoff_field "$SIGNING_SM_CLIENT_CERT_FILE" "$SIGNING_CLIENT_CERT_FILE_MAX_BYTES" 1
  require_bounded_handoff_field "$SIGNING_SM_CLIENT_CERT_PASSWORD" "$SIGNING_CLIENT_CERT_PASSWORD_MAX_BYTES" 1
  [[ "$SIGNING_SM_HOST" == "https://clientauth.one.digicert.com" ]] || early_signing_die
  local total_bytes=$((
    ${#magic}
    + ${#SIGNING_SM_HOST}
    + ${#SIGNING_SM_API_KEY}
    + ${#SIGNING_SM_CLIENT_CERT_FILE}
    + ${#SIGNING_SM_CLIENT_CERT_PASSWORD}
    + 5
  ))
  [[ "$total_bytes" -le "$SIGNING_HANDOFF_MAX_BYTES" ]] || early_signing_die
  SIGNING_HANDOFF_CAPTURED=1
}

consume_signing_handoff_before_external_commands "${3:-}"

validate_public_signing_environment_before_external_commands() {
  local rid="${1-}"
  local name=""
  for name in ${!SM_@}; do
    early_signing_die
  done
  for name in ${!CHUMMER_WINDOWS_@}; do
    case "$name" in
      CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE|\
      CHUMMER_WINDOWS_INSTALLER_MODE|\
      CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH|\
      CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS|\
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256|\
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256|\
      CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED|\
      CHUMMER_WINDOWS_SIGNING_BACKEND|\
      CHUMMER_WINDOWS_SIGN_CERT_PASSWORD|\
      CHUMMER_WINDOWS_SIGN_PFX_BASE64|\
      CHUMMER_WINDOWS_SIGN_PFX_PASSWORD|\
      CHUMMER_WINDOWS_SIGN_PFX_PATH|\
      CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH|\
      CHUMMER_WINDOWS_SIGNING_REQUIRED|\
      CHUMMER_WINDOWS_TIMESTAMP_URL)
        ;;
      CHUMMER_WINDOWS_KEYLOCKER*|\
      CHUMMER_WINDOWS_JSIGN*|\
      CHUMMER_WINDOWS_SIGN*)
        early_signing_die
        ;;
      *)
        ;;
    esac
  done
  for name in ${!CHUMMER_KEYLOCKER_@}; do
    case "$name" in
      CHUMMER_KEYLOCKER_DOTNET_ROOT|\
      CHUMMER_KEYLOCKER_DOTNET_BIN|\
      CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256|\
      CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256|\
      CHUMMER_KEYLOCKER_JAVA_HOME|\
      CHUMMER_KEYLOCKER_JAVA_BIN|\
      CHUMMER_KEYLOCKER_JAVA_BIN_SHA256|\
      CHUMMER_KEYLOCKER_JAVA_TREE_SHA256|\
      CHUMMER_KEYLOCKER_JSIGN_JAR|\
      CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256|\
      CHUMMER_KEYLOCKER_SIGNER_DLL|\
      CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256|\
      CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256|\
      CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256|\
      CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256|\
      CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256)
        ;;
      *)
        early_signing_die
        ;;
    esac
  done
  if [[ "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" != "digicert_keylocker_linux_jsign" ]]; then
    for name in ${!CHUMMER_KEYLOCKER_@}; do
      early_signing_die
    done
  fi
  if [[ "$rid" == win-* && "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" == "digicert_keylocker_linux_jsign" ]]; then
    [[ -z "${CHUMMER_WINDOWS_SIGN_PFX_BASE64:-}" ]] || early_signing_die
    [[ -z "${CHUMMER_WINDOWS_SIGN_PFX_PATH:-}" ]] || early_signing_die
    [[ -z "${CHUMMER_WINDOWS_SIGN_PFX_PASSWORD:-}" ]] || early_signing_die
    [[ -z "${CHUMMER_WINDOWS_SIGN_CERT_PASSWORD:-}" ]] || early_signing_die
    [[ "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" == "digicert_keylocker_linux_jsign" ]] || early_signing_die
    require_bounded_handoff_field "${CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS:-}" 512 0
    require_bounded_handoff_field "${CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH:-}" 4096 0
    require_bounded_handoff_field "${CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256:-}" 64 0
    require_bounded_handoff_field "${CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256:-}" 64 0
    [[ "${CHUMMER_KEYLOCKER_JAVA_HOME:-}" == "$SIGNING_APPROVED_JAVA_HOME" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_JAVA_BIN:-}" == "$SIGNING_APPROVED_JAVA_BIN" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_JAVA_BIN_SHA256:-}" == "$SIGNING_APPROVED_JAVA_SHA256" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_JAVA_TREE_SHA256:-}" == "$SIGNING_APPROVED_JAVA_TREE_SHA256" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_JSIGN_JAR:-}" == "$SIGNING_APPROVED_JSIGN_JAR" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256:-}" == "$SIGNING_APPROVED_JSIGN_SHA256" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_DOTNET_ROOT:-}" == "$SIGNING_APPROVED_DOTNET_ROOT" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_DOTNET_BIN:-}" == "$SIGNING_APPROVED_DOTNET_BIN" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256:-}" == "$SIGNING_APPROVED_DOTNET_SHA256" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256:-}" == "$SIGNING_APPROVED_DOTNET_TREE_SHA256" ]] || early_signing_die
    require_bounded_handoff_field "${CHUMMER_KEYLOCKER_SIGNER_DLL:-}" 4096 0
    [[ "${CHUMMER_KEYLOCKER_SIGNER_DLL}" == /*"/$SIGNING_APPROVED_SIGNER_DLL_NAME" ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    [[ "${CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256:-}" == "$SIGNING_APPROVED_SIGNER_SDK_PIN_SHA256" ]] || early_signing_die
    [[ "${CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    [[ "${CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256}" =~ ^[0-9a-f]{64}$ ]] || early_signing_die
    if [[ -n "${CHUMMER_WINDOWS_TIMESTAMP_URL:-}" ]]; then
      [[ "${CHUMMER_WINDOWS_TIMESTAMP_URL}" == "http://timestamp.digicert.com" ]] || early_signing_die
    fi
    [[ "$SIGNING_HANDOFF_CAPTURED" == "1" ]] || early_signing_die
  fi
}

validate_public_signing_environment_before_external_commands "${3:-}"

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

# Usage:
# bash scripts/build-desktop-installer.sh <publish_dir> <app_key> <rid> <launch_target> [dist_dir] [version]
# Example:
# bash scripts/build-desktop-installer.sh out/avalonia/osx-arm64 avalonia osx-arm64 Chummer.Avalonia
#
# macOS packaging resolves the icon automatically:
# - use CHUMMER_MACOS_ICON_SOURCE when you want to override it with a .icns or .ico file
# - otherwise the script uses chummer.icns when present
# - otherwise it generates chummer.icns from an existing chummer.ico in the publish directory or Chummer root
#
# Local preflight (recommended on macOS):
# bash scripts/preflight-macos-packaging.sh out/avalonia/osx-arm64 osx-arm64 avalonia Chummer.Avalonia

PUBLISH_DIR="${1:?publish directory is required}"
APP_KEY="${2:?app key is required}"
RID="${3:?rid is required}"
LAUNCH_TARGET="${4:?launch target name is required}"
DIST_DIR="${5:-$REPO_ROOT/dist}"
VERSION="${6:-local}"

env_truthy() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
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

normalize_release_version() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "$value"
}

is_placeholder_release_version() {
  case "$(normalize_release_version "$1")" in
    ""|local|local-rebuild|run-local|run-local-rebuild|unpublished)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

require_publishable_release_version() {
  if env_truthy "${CHUMMER_ALLOW_LOCAL_RELEASE_VERSION:-0}"; then
    return 0
  fi

  if is_placeholder_release_version "$VERSION"; then
    echo "Refusing to package public desktop artifacts with placeholder release version '$VERSION'." >&2
    echo "Set a real release identifier or export CHUMMER_ALLOW_LOCAL_RELEASE_VERSION=1 for deliberate local-only packaging." >&2
    exit 1
  fi
}

prune_release_symbols() {
  if env_truthy "${CHUMMER_RELEASE_INCLUDE_PDBS:-0}"; then
    return 0
  fi

  local removed=0
  while IFS= read -r -d '' pdb_path; do
    rm -f "$pdb_path"
    removed=$((removed + 1))
  done < <(find "$PUBLISH_DIR" -type f -name '*.pdb' -print0)

  if (( removed > 0 )); then
    echo "pruned $removed public release symbol file(s) from $PUBLISH_DIR" >&2
  fi
}

abspath() {
  "$PYTHON_BIN" - "$1" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
}

PUBLISH_DIR="$(abspath "$PUBLISH_DIR")"
DIST_DIR="$(abspath "$DIST_DIR")"

if [[ "$(basename "$DIST_DIR")" == "files" ]]; then
  echo "Refusing to use a downloads files/ directory as the desktop installer dist root: $DIST_DIR" >&2
  echo "Pass the release stage root (for example nightly-run-*/ or dist/), not its files/ child." >&2
  exit 1
fi

case "$APP_KEY" in
  avalonia)
    APP_DISPLAY="Chummer6 Avalonia Desktop"
    INSTALL_DIR_NAME="AvaloniaDesktop"
    SHORTCUT_NAME="Chummer6 Avalonia"
    ;;
  blazor-desktop)
    APP_DISPLAY="Chummer6 Blazor Desktop"
    INSTALL_DIR_NAME="BlazorDesktop"
    SHORTCUT_NAME="Chummer6 Blazor Desktop"
    ;;
  *)
    echo "Unsupported app key: $APP_KEY" >&2
    exit 1
    ;;
esac

mkdir -p "$DIST_DIR"

resolve_head_display_name() {
  case "$1" in
    avalonia) echo "Chummer6 Avalonia Desktop" ;;
    blazor-desktop) echo "Chummer6 Blazor Desktop" ;;
    *)
      echo "Unsupported app key: $1" >&2
      exit 1
      ;;
  esac
}

resolve_head_shortcut_name() {
  case "$1" in
    avalonia) echo "Chummer6 Avalonia" ;;
    blazor-desktop) echo "Chummer6 Blazor Desktop" ;;
    *)
      echo "Unsupported app key: $1" >&2
      exit 1
      ;;
  esac
}

WINDOWS_SECONDARY_HEAD_KEY="${CHUMMER_WINDOWS_SECONDARY_HEAD_KEY:-}"
WINDOWS_SECONDARY_HEAD_PUBLISH_DIR="${CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR:-}"
WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET="${CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET:-}"
WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT="${CHUMMER_WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT:-$WINDOWS_SECONDARY_HEAD_KEY}"

if [[ -n "$WINDOWS_SECONDARY_HEAD_PUBLISH_DIR" ]]; then
  WINDOWS_SECONDARY_HEAD_PUBLISH_DIR="$(abspath "$WINDOWS_SECONDARY_HEAD_PUBLISH_DIR")"
fi

desktop_release_channel() {
  local value="${CHUMMER_DESKTOP_RELEASE_CHANNEL:-${CHUMMER_RELEASE_CHANNEL:-docker}}"
  value="$(echo "$value" | tr '[:upper:]' '[:lower:]')"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  if [[ -z "$value" ]]; then
    value="docker"
  fi
  printf '%s' "$value"
}

is_preview_release_channel() {
  case "$(desktop_release_channel)" in
    preview|docker)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

allow_unsigned_public_release() {
  env_truthy "${CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE:-0}"
}

unsigned_public_release_status() {
  if allow_unsigned_public_release && ! is_preview_release_channel; then
    printf '%s' "unsigned_public_release"
  else
    printf '%s' "skipped_preview"
  fi
}

unsigned_public_release_reason() {
  if allow_unsigned_public_release && ! is_preview_release_channel; then
    printf '%s' "Unsigned public release posture is explicitly allowed for this lane."
  else
    printf '%s' "Preview channel does not require Authenticode signing."
  fi
}

windows_signing_required() {
  if allow_unsigned_public_release && ! is_preview_release_channel; then
    return 1
  fi

  if [[ -n "${CHUMMER_WINDOWS_SIGNING_REQUIRED:-}" ]]; then
    env_truthy "${CHUMMER_WINDOWS_SIGNING_REQUIRED}"
    return
  fi

  if is_preview_release_channel; then
    return 1
  fi

  return 0
}

macos_signing_required() {
  if allow_unsigned_public_release && ! is_preview_release_channel; then
    return 1
  fi

  if [[ -n "${CHUMMER_MAC_SIGNING_REQUIRED:-}" ]]; then
    env_truthy "${CHUMMER_MAC_SIGNING_REQUIRED}"
    return
  fi

  if is_preview_release_channel; then
    return 1
  fi

  return 0
}

macos_notarization_required() {
  if allow_unsigned_public_release && ! is_preview_release_channel; then
    return 1
  fi

  if [[ -n "${CHUMMER_MAC_NOTARIZATION_REQUIRED:-}" ]]; then
    env_truthy "${CHUMMER_MAC_NOTARIZATION_REQUIRED}"
    return
  fi

  if is_preview_release_channel; then
    return 1
  fi

  return 0
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

file_size_bytes() {
  python3 - "$1" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).stat().st_size)
PY
}

signing_receipt_path() {
  if [[ "$RID" == win-* ]]; then
    printf '%s' "${CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH:-$DIST_DIR/signing/signing-$APP_KEY-$RID.receipt.json}"
    return
  fi

  if [[ "$RID" == osx-* ]]; then
    printf '%s' "${CHUMMER_MAC_SIGNING_RECEIPT_PATH:-$DIST_DIR/signing/signing-$APP_KEY-$RID.receipt.json}"
    return
  fi

  printf '%s' ""
}

write_signing_receipt() {
  local receipt_path="${1:-}"
  local platform="${2:-}"
  local signing_status="${3:-}"
  local notarization_status="${4:-}"
  local reason="${5:-}"
  shift 5 || true

  if [[ -z "$receipt_path" ]]; then
    return 0
  fi

  python3 - "$receipt_path" "$platform" "$APP_KEY" "$RID" "$VERSION" "$(desktop_release_channel)" "$signing_status" "$notarization_status" "$reason" "$@" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path


def kind_for_name(file_name: str) -> str:
    lowered = file_name.lower()
    if lowered.endswith(("-installer.exe", "-installer.dmg", "-installer.pkg", ".msix")):
        return "installer"
    if lowered.endswith(".exe"):
        return "portable"
    if lowered.endswith(".tar.gz"):
        return "archive"
    return "artifact"


def sha256_file(path: Path) -> str:
    import hashlib

    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


receipt_path = Path(sys.argv[1])
platform = sys.argv[2]
app = sys.argv[3]
rid = sys.argv[4]
release_version = sys.argv[5]
release_channel = sys.argv[6]
signing_status = sys.argv[7]
notarization_status = sys.argv[8]
reason = sys.argv[9]
artifact_args = sys.argv[10:]

artifacts = []
for raw_path in artifact_args:
    path = Path(raw_path)
    if not path.is_file():
        continue
    artifacts.append(
        {
            "fileName": path.name,
            "sha256": sha256_file(path),
            "kind": kind_for_name(path.name),
            "signingStatus": signing_status,
            "notarizationStatus": notarization_status if platform == "macos" else None,
        }
    )

payload = {
    "contractName": "chummer6-ui.desktop_artifact_signing",
    "contractVersion": 2,
    "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "platform": platform,
    "app": app,
    "rid": rid,
    "releaseChannel": release_channel,
    "releaseVersion": release_version,
    "signingStatus": signing_status,
    "notarizationStatus": notarization_status if platform == "macos" else None,
    "reason": reason,
    "artifacts": artifacts,
}

receipt_path.parent.mkdir(parents=True, exist_ok=True)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

bind_windows_publication_candidate_to_signing_receipt() {
  if ! env_truthy "${CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED:-0}"; then
    return 0
  fi

  local receipt_path
  receipt_path="$(signing_receipt_path)"
  local installer_path="$DIST_DIR/chummer-$APP_KEY-$RID-installer.exe"
  local payload_path="$DIST_DIR/files/chummer-$APP_KEY-$RID-payload.zip"
  "$PYTHON_BIN" - "$receipt_path" "$installer_path" "$payload_path" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


receipt_path, installer, payload = map(Path, sys.argv[1:])
for path, label in ((receipt_path, "signing receipt"), (installer, "installer"), (payload, "payload")):
    if path.is_symlink() or not path.is_file():
        raise SystemExit(f"Windows-only publication requires a regular {label}: {path}")

receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if (
    not isinstance(receipt, dict)
    or receipt.get("contractName") != "chummer6-ui.desktop_artifact_signing"
    or receipt.get("contractVersion") != 2
    or receipt.get("signingStatus") != "pass"
):
    raise SystemExit("Windows-only publication requires one passing v2 Authenticode receipt")

installer_sha = digest(installer)
installer_rows = [
    row
    for row in receipt.get("artifacts") or []
    if isinstance(row, dict)
    and row.get("fileName") == installer.name
    and row.get("sha256") == installer_sha
    and row.get("signingStatus") == "pass"
]
if len(installer_rows) != 1:
    raise SystemExit("AuthentiCode receipt does not bind the exact final installer bytes")

receipt["candidateBindings"] = [
    {
        "artifactRole": "installer",
        "authenticodeStatus": "pass",
        "fileName": installer.name,
        "sha256": installer_sha,
        "sizeBytes": installer.stat().st_size,
    },
    {
        "artifactRole": "payload",
        "authenticodeStatus": "not_applicable_payload",
        "fileName": payload.name,
        "sha256": digest(payload),
        "sizeBytes": payload.stat().st_size,
    },
]
temporary = receipt_path.with_name(f".{receipt_path.name}.scope-{os.getpid()}")
with temporary.open("x", encoding="utf-8") as handle:
    handle.write(json.dumps(receipt, indent=2, sort_keys=True) + "\n")
    handle.flush()
    os.fsync(handle.fileno())
os.replace(temporary, receipt_path)
PY
}

has_windows_signing_configuration() {
  if [[ "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" == "digicert_keylocker_linux_jsign" ]]; then
    [[ "$SIGNING_HANDOFF_CAPTURED" == "1" ]]
    return
  fi
  [[ -n "${CHUMMER_WINDOWS_SIGN_PFX_BASE64:-}" || -n "${CHUMMER_WINDOWS_SIGN_PFX_PATH:-}" ]]
}

run_linux_keylocker_signing() {
  local receipt_path="${1:-}"
  shift || true
  local -a artifacts=("$@")
  local wrapper_source=""
  local handoff_path=""
  local handoff_source_fd=""
  local handoff_writer_pid=""
  local handoff_writer_status=0
  local signing_status=0
  local digicertone_storepass=""
  local release_channel=""
  export -n digicertone_storepass
  IFS= read -r -d '' wrapper_source <<'BASH' || :
builtin set +a +b +e +f +h +m +n +u +v +x
builtin trap - EXIT ERR DEBUG RETURN HUP INT QUIT TERM
builtin shopt -u \
  autocd cdable_vars cdspell checkhash checkjobs checkwinsize cmdhist \
  compat31 compat40 compat41 compat42 compat43 compat44 direxpand dirspell \
  dotglob execfail expand_aliases extdebug extglob failglob force_fignore \
  globasciiranges globskipdots globstar gnu_errfmt histappend histreedit \
  histverify hostcomplete huponexit inherit_errexit interactive_comments \
  lastpipe lithist localvar_inherit localvar_unset login_shell mailwarn \
  no_empty_cmd_completion nocaseglob nocasematch noexpand_translation \
  nullglob patsub_replacement progcomp progcomp_alias promptvars restricted_shell \
  shift_verbose sourcepath syslog varredir_close xpg_echo 2>/dev/null || :

environment_names="$(builtin compgen -e 3<&-)"
while IFS= read -r environment_name; do
  [[ -n "$environment_name" ]] || continue
  builtin unset "$environment_name" 2>/dev/null || :
done <<< "$environment_names"
environment_names=""
builtin export -n BASHOPTS SHELLOPTS 2>/dev/null || :

function_names="$(builtin compgen -A function 3<&-)"
while IFS= read -r function_name; do
  [[ -n "$function_name" ]] || continue
  builtin unset -f "$function_name" 2>/dev/null || :
done <<< "$function_names"
function_names=""

for fd_path in /proc/self/fd/*; do
  descriptor="${fd_path##*/}"
  [[ "$descriptor" =~ ^[0-9]+$ ]] || exit 2
  case "$descriptor" in
    0|1|2|3) ;;
    *) builtin eval "exec ${descriptor}>&-" ;;
  esac
done

builtin set -euo pipefail
receipt_path="${1-}"
app_key="${2-}"
rid="${3-}"
release_channel="${4-}"
release_version="${5-}"
allow_unsigned="${6-}"
backend="${7-}"
timestamp_url="${8-}"
key_alias="${9-}"
public_certificate="${10-}"
signer_certificate_sha="${11-}"
signer_spki_sha="${12-}"
java_home="${13-}"
java_bin="${14-}"
java_bin_sha="${15-}"
java_tree_sha="${16-}"
jsign_jar="${17-}"
jsign_sha="${18-}"
dotnet_root="${19-}"
dotnet_bin="${20-}"
dotnet_bin_sha="${21-}"
dotnet_tree_sha="${22-}"
signer_dll="${23-}"
signer_dll_sha="${24-}"
signer_tree_sha="${25-}"
runtime_config_sha="${26-}"
deps_sha="${27-}"
sdk_pin_sha="${28-}"
builtin shift 28
artifacts=("$@")

[[ "$backend" == "digicert_keylocker_linux_jsign" ]] || exit 2
[[ "$timestamp_url" == "http://timestamp.digicert.com" ]] || exit 2
[[ "$dotnet_root" == "/usr/lib/dotnet" ]] || exit 2
[[ "$dotnet_bin" == "/usr/lib/dotnet/dotnet" ]] || exit 2
[[ "$signer_dll" == /*"/Chummer.KeyLockerSigner.dll" ]] || exit 2
[[ "${artifacts[0]+present}" == "present" ]] || exit 2
for digest in \
  "$signer_certificate_sha" "$signer_spki_sha" \
  "$java_bin_sha" "$java_tree_sha" "$jsign_sha" \
  "$dotnet_bin_sha" "$dotnet_tree_sha" "$signer_dll_sha" \
  "$signer_tree_sha" "$runtime_config_sha" "$deps_sha" "$sdk_pin_sha"; do
  [[ "$digest" =~ ^[0-9a-f]{64}$ ]] || exit 2
done

signer_root="${signer_dll%/*}"
runtime_config="$signer_root/Chummer.KeyLockerSigner.runtimeconfig.json"
deps="$signer_root/Chummer.KeyLockerSigner.deps.json"
sdk_pin="$signer_root/global.json"
[[ "$signer_root" == /* && "$signer_root" != "/" ]] || exit 2

unsafe_dotnet_entry="$(
  /usr/bin/find "$dotnet_root" -xdev \
    \( ! -user root -o \( ! -type l -perm /022 \) \) \
    -print -quit \
    3<&-
)"
[[ -z "$unsafe_dotnet_entry" ]] || exit 2
while IFS= read -r -d '' link_path; do
  link_target="$(/usr/bin/readlink -f -- "$link_path" 3<&-)"
  [[ "$link_target" == "$dotnet_root"/* ]] || exit 2
done < <(/usr/bin/find "$dotnet_root" -xdev -type l -print0 3<&-)

unsafe_signer_entry="$(
  /usr/bin/find "$signer_root" -xdev \
    \( \
      ! -user "$EUID" -o \
      \( -type d ! -perm 500 \) -o \
      \( -type f \( ! -perm 400 -o -links +1 \) \) -o \
      \( ! -type d ! -type f \) \
    \) -print -quit \
    3<&-
)"
[[ -z "$unsafe_signer_entry" ]] || exit 2

hash_file() {
  local output=""
  output="$(/usr/bin/sha256sum -- "$1" 3<&-)"
  REPLY="${output%% *}"
  [[ "$REPLY" =~ ^[0-9a-f]{64}$ ]] || exit 2
}
hash_tree() {
  local parent="${1-}"
  local root_name="${2-}"
  local output=""
  output="$(
    /usr/bin/tar \
      --sort=name \
      --mtime="UTC 1970-01-01" \
      --owner=0 \
      --group=0 \
      --numeric-owner \
      -C "$parent" \
      -cf - \
      "$root_name" \
      3<&- |
      /usr/bin/sha256sum 3<&-
  )"
  REPLY="${output%% *}"
  [[ "$REPLY" =~ ^[0-9a-f]{64}$ ]] || exit 2
}

hash_file "$dotnet_bin"; [[ "$REPLY" == "$dotnet_bin_sha" ]] || exit 2
hash_tree "/usr/lib" "dotnet"; [[ "$REPLY" == "$dotnet_tree_sha" ]] || exit 2
hash_file "$java_bin"; [[ "$REPLY" == "$java_bin_sha" ]] || exit 2
hash_tree "${java_home%/*}" "${java_home##*/}"; [[ "$REPLY" == "$java_tree_sha" ]] || exit 2
hash_file "$jsign_jar"; [[ "$REPLY" == "$jsign_sha" ]] || exit 2
hash_file "$signer_dll"; [[ "$REPLY" == "$signer_dll_sha" ]] || exit 2
hash_file "$runtime_config"; [[ "$REPLY" == "$runtime_config_sha" ]] || exit 2
hash_file "$deps"; [[ "$REPLY" == "$deps_sha" ]] || exit 2
hash_file "$sdk_pin"; [[ "$REPLY" == "$sdk_pin_sha" ]] || exit 2
hash_tree "${signer_root%/*}" "${signer_root##*/}"; [[ "$REPLY" == "$signer_tree_sha" ]] || exit 2

magic=""
sm_host=""
sm_api_key=""
sm_client_cert_file=""
sm_client_cert_password=""
trailing=""
IFS= read -r -d '' -u 3 magic || exit 2
IFS= read -r -d '' -u 3 sm_host || exit 2
IFS= read -r -d '' -u 3 sm_api_key || exit 2
IFS= read -r -d '' -u 3 sm_client_cert_file || exit 2
IFS= read -r -d '' -u 3 sm_client_cert_password || exit 2
if IFS= read -r -d '' -u 3 trailing || [[ -n "$trailing" ]]; then
  exit 2
fi
exec 3<&-
[[ "$magic" == "chummer6-ui.windows-signing-handoff.v1" ]] || exit 2
[[ "$sm_host" == "https://clientauth.one.digicert.com" ]] || exit 2
for secret_field in "$sm_api_key" "$sm_client_cert_file" "$sm_client_cert_password"; do
  [[ -n "$secret_field" && "$secret_field" != *"|"* && "$secret_field" != *[[:cntrl:]]* ]] || exit 2
done
digicertone_storepass="${sm_api_key}|${sm_client_cert_file}|${sm_client_cert_password}"
sm_api_key=""
sm_client_cert_file=""
sm_client_cert_password=""

builtin export \
  CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE="$allow_unsigned" \
  CHUMMER_DESKTOP_APP_KEY="$app_key" \
  CHUMMER_DESKTOP_RELEASE_CHANNEL="$release_channel" \
  CHUMMER_DESKTOP_RELEASE_VERSION="$release_version" \
  CHUMMER_DESKTOP_RID="$rid" \
  CHUMMER_KEYLOCKER_JAVA_BIN="$java_bin" \
  CHUMMER_KEYLOCKER_JAVA_BIN_SHA256="$java_bin_sha" \
  CHUMMER_KEYLOCKER_JAVA_HOME="$java_home" \
  CHUMMER_KEYLOCKER_JAVA_TREE_SHA256="$java_tree_sha" \
  CHUMMER_KEYLOCKER_JSIGN_JAR="$jsign_jar" \
  CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256="$jsign_sha" \
  CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS="$digicertone_storepass" \
  CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH="$public_certificate" \
  CHUMMER_WINDOWS_KEYLOCKER_HOST="$sm_host" \
  CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS="$key_alias" \
  CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256="$signer_certificate_sha" \
  CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256="$signer_spki_sha" \
  CHUMMER_WINDOWS_SIGNING_BACKEND="$backend" \
  CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH="$receipt_path" \
  CHUMMER_WINDOWS_TIMESTAMP_URL="$timestamp_url" \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  DOTNET_EnableDiagnostics=0 \
  DOTNET_EnableDiagnostics_Debugger=0 \
  DOTNET_EnableDiagnostics_IPC=0 \
  DOTNET_EnableDiagnostics_Profiler=0 \
  DOTNET_MULTILEVEL_LOOKUP=0 \
  DOTNET_NOLOGO=1 \
  DOTNET_ROOT="$dotnet_root"

signer_arguments=()
for artifact in "${artifacts[@]}"; do
  signer_arguments+=(--artifact "$artifact")
done
builtin exec "$dotnet_bin" "$signer_dll" "${signer_arguments[@]}"
BASH

  release_channel="$(desktop_release_channel)"
  handoff_path=<(
    printf '%s\0%s\0%s\0%s\0%s\0' \
      "$SIGNING_HANDOFF_MAGIC" \
      "$SIGNING_SM_HOST" \
      "$SIGNING_SM_API_KEY" \
      "$SIGNING_SM_CLIENT_CERT_FILE" \
      "$SIGNING_SM_CLIENT_CERT_PASSWORD"
  )
  handoff_writer_pid="$!"
  handoff_source_fd="${handoff_path##*/}"
  [[ "$handoff_source_fd" =~ ^[0-9]+$ ]] || early_signing_die
  exec 3<"$handoff_path"
  exec {handoff_source_fd}<&-
  export -n BASHOPTS SHELLOPTS 2>/dev/null || :
  "$TRUSTED_BASH_PATH" --noprofile --norc -c "$wrapper_source" chummer-keylocker-direct \
    "$receipt_path" \
    "$APP_KEY" \
    "$RID" \
    "$release_channel" \
    "$VERSION" \
    "${CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE:-0}" \
    "$CHUMMER_WINDOWS_SIGNING_BACKEND" \
    "${CHUMMER_WINDOWS_TIMESTAMP_URL:-http://timestamp.digicert.com}" \
    "$CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS" \
    "$CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH" \
    "$CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256" \
    "$CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256" \
    "$CHUMMER_KEYLOCKER_JAVA_HOME" \
    "$CHUMMER_KEYLOCKER_JAVA_BIN" \
    "$CHUMMER_KEYLOCKER_JAVA_BIN_SHA256" \
    "$CHUMMER_KEYLOCKER_JAVA_TREE_SHA256" \
    "$CHUMMER_KEYLOCKER_JSIGN_JAR" \
    "$CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256" \
    "$CHUMMER_KEYLOCKER_DOTNET_ROOT" \
    "$CHUMMER_KEYLOCKER_DOTNET_BIN" \
    "$CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256" \
    "$CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256" \
    "$CHUMMER_KEYLOCKER_SIGNER_DLL" \
    "$CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256" \
    "$CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256" \
    "$CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256" \
    "$CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256" \
    "$CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256" \
    "${artifacts[@]}" \
    3<&3 || signing_status=$?
  exec 3<&-
  wait "$handoff_writer_pid" || handoff_writer_status=$?
  digicertone_storepass=""
  (( handoff_writer_status == 0 )) || return "$handoff_writer_status"
  (( signing_status == 0 )) || return "$signing_status"
}

run_windows_signing() {
  local receipt_path="${1:-}"
  shift || true
  local -a artifacts=("$@")
  local artifact_count

  artifact_count="$(array_count artifacts)"
  if (( artifact_count == 0 )); then
    return 0
  fi
  has_windows_signing_configuration || {
    printf '%s\n' "Windows signing configuration is unavailable." >&2
    return 1
  }

  if [[ "${CHUMMER_WINDOWS_SIGNING_BACKEND:-}" == "digicert_keylocker_linux_jsign" ]]; then
    run_linux_keylocker_signing "$receipt_path" "${artifacts[@]}"
    return
  fi

  local powershell_bin="powershell"
  if command -v pwsh >/dev/null 2>&1; then
    powershell_bin="pwsh"
  fi
  CHUMMER_DESKTOP_APP_KEY="$APP_KEY" \
  CHUMMER_DESKTOP_RID="$RID" \
  CHUMMER_DESKTOP_RELEASE_CHANNEL="$(desktop_release_channel)" \
  CHUMMER_DESKTOP_RELEASE_VERSION="$VERSION" \
  CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE="${CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE:-0}" \
  CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH="$receipt_path" \
  "$powershell_bin" -NoLogo -NoProfile -File "$REPO_ROOT/scripts/sign-windows-artifacts.ps1" -ArtifactPaths "${artifacts[@]}"
}

pre_sign_windows_payloads_if_configured() {
  local -a payload_artifacts=("$PUBLISH_DIR/$LAUNCH_TARGET")
  if [[ -n "$WINDOWS_SECONDARY_HEAD_PUBLISH_DIR" && -n "$WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET" ]]; then
    payload_artifacts+=("$WINDOWS_SECONDARY_HEAD_PUBLISH_DIR/$WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET")
  fi

  if has_windows_signing_configuration; then
    run_windows_signing "" "${payload_artifacts[@]}"
    return 0
  fi

  if windows_signing_required; then
    local receipt_path
    receipt_path="$(signing_receipt_path)"
    write_signing_receipt \
      "$receipt_path" \
      "windows" \
      "fail" \
      "" \
      "Windows signing is required for release channel '$(desktop_release_channel)', but the fixed DIGICERTONE signer handoff was unavailable."
    echo "Windows signing is required for release channel '$(desktop_release_channel)', but the fixed DIGICERTONE signer handoff was unavailable." >&2
    exit 1
  fi
}

finalize_windows_signing_receipt() {
  local portable_exe="$DIST_DIR/chummer-$APP_KEY-$RID.exe"
  local installer_path="$DIST_DIR/chummer-$APP_KEY-$RID-installer.exe"
  local receipt_path
  receipt_path="$(signing_receipt_path)"

  if has_windows_signing_configuration; then
    local signing_status=0
    run_windows_signing "$receipt_path" "$portable_exe" "$installer_path" || signing_status=$?
    SIGNING_SM_HOST=""
    SIGNING_SM_API_KEY=""
    SIGNING_SM_CLIENT_CERT_FILE=""
    SIGNING_SM_CLIENT_CERT_PASSWORD=""
    SIGNING_HANDOFF_CAPTURED=0
    (( signing_status == 0 )) || return "$signing_status"
    return 0
  fi

  write_signing_receipt \
    "$receipt_path" \
    "windows" \
    "$(unsigned_public_release_status)" \
    "" \
    "$(unsigned_public_release_reason)" \
    "$portable_exe" \
    "$installer_path"
}

stage_installer_for_downloads_manifest() {
  local installer_name="$1"
  local installer_path="$DIST_DIR/$installer_name"
  local downloads_files_dir="$DIST_DIR/files"

  if [[ ! -f "$installer_path" ]]; then
    echo "Cannot stage missing installer for downloads manifest: $installer_path" >&2
    exit 1
  fi

  mkdir -p "$downloads_files_dir"
  cp -f "$installer_path" "$downloads_files_dir/$installer_name"
}

windows_build_provenance_required() {
  env_truthy "${CHUMMER_WINDOWS_BUILD_PROVENANCE_REQUIRED:-0}"
}

windows_provenance_project_path() {
  case "$APP_KEY" in
    avalonia) printf '%s\n' "Chummer.Avalonia/Chummer.Avalonia.csproj" ;;
    blazor-desktop) printf '%s\n' "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj" ;;
    *)
      echo "Unsupported Windows provenance app key: $APP_KEY" >&2
      return 1
      ;;
  esac
}

begin_windows_build_provenance() {
  if ! windows_build_provenance_required; then
    return 0
  fi
  if [[ "$APP_KEY" != "avalonia" || "$RID" != "win-x64" ]]; then
    echo "The Windows proof provenance contract currently admits only avalonia/win-x64." >&2
    exit 1
  fi
  "$PYTHON_BIN" - "$VERSION" <<'PY'
import re
import sys

value = sys.argv[1]
if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", value) is None or ".." in value:
    raise SystemExit("Windows proof provenance requires a portable release version")
PY

  local workspace_root="${CHUMMER_WORKSPACE_ROOT:-$(cd "$REPO_ROOT/.." && pwd -P)}"
  local generator="${CHUMMER_WINDOWS_BUILD_PROVENANCE_GENERATOR:-$workspace_root/scripts/release/materialize_build_provenance.py}"
  local support="${CHUMMER_WINDOWS_BUILD_PROVENANCE_SUPPORT:-$workspace_root/scripts/release/verify_supply_chain_evidence.py}"
  local project_path
  project_path="$(windows_provenance_project_path)"
  local invocation_id="$VERSION.avalonia.win-x64.installer"
  local governed_root="$DIST_DIR/proof/build-provenance/v1"
  local private_root="$DIST_DIR/.windows-build-provenance-private"
  local receipt_path="$governed_root/invocations/$invocation_id.json"
  local state_path="$private_root/$invocation_id.state.json"
  local sbom_path="$governed_root/sbom/desktop-avalonia.cdx.json"
  local artifact_path="$DIST_DIR/chummer-$APP_KEY-$RID-installer.exe"
  local core_root="${CHUMMER_WINDOWS_SOURCE_CORE_ROOT:-$workspace_root/chummer-core-engine}"
  local run_services_root="${CHUMMER_WINDOWS_SOURCE_RUN_SERVICES_ROOT:-$workspace_root/chummer.run-services}"
  local ui_kit_root="${CHUMMER_WINDOWS_SOURCE_UI_KIT_ROOT:-$workspace_root/chummer-ui-kit}"
  local registry_root="${CHUMMER_WINDOWS_SOURCE_REGISTRY_ROOT:-$workspace_root/chummer-hub-registry}"
  local media_root="${CHUMMER_WINDOWS_SOURCE_MEDIA_ROOT:-$workspace_root/chummer-media-factory}"
  local legacy_root="${CHUMMER_WINDOWS_SOURCE_LEGACY_ROOT:-$workspace_root/chummer5a}"

  [[ -f "$generator" && ! -L "$generator" ]] || {
    echo "Windows build provenance generator is unavailable: $generator" >&2
    exit 1
  }
  [[ -f "$support" && ! -L "$support" ]] || {
    echo "Windows build provenance support is unavailable: $support" >&2
    exit 1
  }
  [[ ! -e "$artifact_path" && ! -L "$artifact_path" ]] || {
    echo "Windows proof provenance requires a fresh installer output path: $artifact_path" >&2
    exit 1
  }
  mkdir -p "$(dirname "$receipt_path")" "$(dirname "$sbom_path")" "$private_root"

  "$PYTHON_BIN" "$generator" begin \
    --state "$state_path" \
    --output "$receipt_path" \
    --builder-id "chummer-windows-release-bootstrap" \
    --build-type "windows-desktop-release" \
    --invocation-id "$invocation_id" \
    --release-version "$VERSION" \
    --supply-chain-script "$support" \
    --source-repository "chummer-presentation" \
    --source-repo-root "$REPO_ROOT" \
    --source-material "chummer-core-engine=$core_root" \
    --source-material "chummer.run-services=$run_services_root" \
    --source-material "chummer-ui-kit=$ui_kit_root" \
    --source-material "chummer-hub-registry=$registry_root" \
    --source-material "chummer-media-factory=$media_root" \
    --source-material "chummer5a=$legacy_root" \
    --build-root "$REPO_ROOT" \
    --target-id "desktop-avalonia" \
    --project-path "$project_path" \
    --artifact-id "avalonia-win-x64-installer" \
    --artifact-kind "desktop_download" \
    --artifact-name "chummer-avalonia-win-x64-installer.exe" \
    --artifact-path "$artifact_path" \
    --sbom-path "$sbom_path" \
    --build-input "desktop-project=$REPO_ROOT/$project_path" \
    --build-input "desktop-installer-recipe=$REPO_ROOT/scripts/build-desktop-installer.sh" \
    --build-input "windows-bootstrap-recipe=$REPO_ROOT/scripts/build-native-windows-bootstrap-installer.sh" \
    --build-input "dotnet-sdk-selection=$REPO_ROOT/global.json"
}

finalize_windows_build_provenance() {
  if ! windows_build_provenance_required; then
    return 0
  fi

  local workspace_root="${CHUMMER_WORKSPACE_ROOT:-$(cd "$REPO_ROOT/.." && pwd -P)}"
  local generator="${CHUMMER_WINDOWS_BUILD_PROVENANCE_GENERATOR:-$workspace_root/scripts/release/materialize_build_provenance.py}"
  local invocation_id="$VERSION.avalonia.win-x64.installer"
  local private_root="$DIST_DIR/.windows-build-provenance-private"
  local state_path="$private_root/$invocation_id.state.json"
  local receipt_path="$DIST_DIR/proof/build-provenance/v1/invocations/$invocation_id.json"

  "$PYTHON_BIN" "$generator" finalize \
    --state "$state_path" \
    --output "$receipt_path" \
    --builder-id "chummer-windows-release-bootstrap" \
    --build-type "windows-desktop-release" \
    --invocation-id "$invocation_id" \
    --release-version "$VERSION"
  rm -f "$state_path"
  rm -rf "$private_root/.$invocation_id.state.json.finalized"
  rmdir "$private_root" 2>/dev/null || true
}

has_macos_signing_identity() {
  [[ -n "${CHUMMER_MAC_APP_SIGN_IDENTITY:-}" ]]
}

has_macos_notary_profile() {
  [[ -n "${CHUMMER_MAC_NOTARY_PROFILE:-}" ]]
}

sign_macos_publish_binary_if_configured() {
  local target="$PUBLISH_DIR/$LAUNCH_TARGET"
  if [[ ! -f "$target" ]]; then
    echo "Launch target not found in macOS publish directory: $target" >&2
    exit 1
  fi

  if has_macos_signing_identity; then
    codesign --force --timestamp --options runtime --sign "${CHUMMER_MAC_APP_SIGN_IDENTITY}" "$target"
    codesign --verify --verbose=2 "$target"
    return 0
  fi

  if macos_signing_required; then
    local receipt_path
    receipt_path="$(signing_receipt_path)"
    write_signing_receipt \
      "$receipt_path" \
      "macos" \
      "fail" \
      "" \
      "macOS signing is required for release channel '$(desktop_release_channel)', but CHUMMER_MAC_APP_SIGN_IDENTITY is not configured." \
      "$target"
    echo "macOS signing is required for release channel '$(desktop_release_channel)', but CHUMMER_MAC_APP_SIGN_IDENTITY is not configured." >&2
    exit 1
  fi
}

sign_macos_app_bundle_if_configured() {
  local app_bundle="$1"

  if has_macos_signing_identity; then
    codesign --force --deep --options runtime --timestamp --sign "${CHUMMER_MAC_APP_SIGN_IDENTITY}" "$app_bundle"
    codesign --verify --deep --strict --verbose=2 "$app_bundle"
    return 0
  fi

  if macos_signing_required; then
    echo "macOS signing is required for release channel '$(desktop_release_channel)', but CHUMMER_MAC_APP_SIGN_IDENTITY is not configured." >&2
    exit 1
  fi
}

finalize_macos_signing_receipt() {
  local installer_path="$1"
  local receipt_path
  receipt_path="$(signing_receipt_path)"
  local signing_status
  signing_status="$(unsigned_public_release_status)"
  local notarization_status
  notarization_status="$(unsigned_public_release_status)"
  local reason=""
  if [[ "$signing_status" == "unsigned_public_release" || "$notarization_status" == "unsigned_public_release" ]]; then
    reason="Unsigned public release posture is explicitly allowed for this lane."
  fi

  if has_macos_signing_identity; then
    codesign --force --timestamp --sign "${CHUMMER_MAC_APP_SIGN_IDENTITY}" "$installer_path"
    codesign --verify --verbose=2 "$installer_path"
    signing_status="pass"
  elif macos_signing_required; then
    reason="macOS signing is required for release channel '$(desktop_release_channel)', but CHUMMER_MAC_APP_SIGN_IDENTITY is not configured."
    write_signing_receipt "$receipt_path" "macos" "fail" "fail" "$reason" "$installer_path"
    echo "$reason" >&2
    exit 1
  fi

  if has_macos_notary_profile; then
    if ! has_macos_signing_identity; then
      reason="macOS notarization was configured without a signing identity; configure CHUMMER_MAC_APP_SIGN_IDENTITY alongside CHUMMER_MAC_NOTARY_PROFILE."
      write_signing_receipt "$receipt_path" "macos" "fail" "fail" "$reason" "$installer_path"
      echo "$reason" >&2
      exit 1
    fi

    local notary_result_path="${CHUMMER_MAC_NOTARY_RESULT_PATH:-}"
    if [[ -n "$notary_result_path" ]]; then
      if [[ -e "$notary_result_path" || -L "$notary_result_path" ]]; then
        echo "Refusing to overwrite macOS notarization result: $notary_result_path" >&2
        exit 1
      fi
      mkdir -p "$(dirname "$notary_result_path")"
      local notary_result_tmp="${notary_result_path}.tmp.$$"
      xcrun notarytool submit \
        "$installer_path" \
        --keychain-profile "${CHUMMER_MAC_NOTARY_PROFILE}" \
        --wait \
        --output-format json >"$notary_result_tmp"
      "$PYTHON_BIN" - "$notary_result_tmp" <<'PY'
import json
import re
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
if (
    not isinstance(payload, dict)
    or payload.get("status") != "Accepted"
    or re.fullmatch(
        r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-"
        r"[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        str(payload.get("id") or ""),
    )
    is None
):
    raise SystemExit("notarytool result did not prove an accepted submission")
PY
      mv "$notary_result_tmp" "$notary_result_path"
    else
      xcrun notarytool submit "$installer_path" --keychain-profile "${CHUMMER_MAC_NOTARY_PROFILE}" --wait
    fi
    xcrun stapler staple "$installer_path"
    xcrun stapler validate "$installer_path"
    notarization_status="pass"
  elif macos_notarization_required; then
    reason="macOS notarization is required for release channel '$(desktop_release_channel)', but CHUMMER_MAC_NOTARY_PROFILE is not configured."
    write_signing_receipt "$receipt_path" "macos" "$signing_status" "fail" "$reason" "$installer_path"
    echo "$reason" >&2
    exit 1
  fi

  write_signing_receipt "$receipt_path" "macos" "$signing_status" "$notarization_status" "$reason" "$installer_path"
}

resolve_demo_character_source() {
  local configured="${CHUMMER_RELEASE_SAMPLE_SOURCE:-}"
  local fixture_root="${CHUMMER_LEGACY_FIXTURE_ROOT:-}"
  local candidates=()

  if [[ -n "$configured" ]]; then
    candidates+=("$configured")
  fi
  if [[ -n "$fixture_root" ]]; then
    candidates+=("$fixture_root/Soma (Career).chum5")
  fi

  candidates+=(
    "$REPO_ROOT/../../chummer5a/Chummer.Tests/TestFiles/Soma (Career).chum5"
    "/docker/chummer5a/Chummer.Tests/TestFiles/Soma (Career).chum5"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      printf '%s' "$candidate"
      return 0
    fi
  done

  return 1
}

preflight_macos_packaging_requirements() {
  local icon_source
  icon_source="$("$REPO_ROOT/scripts/ensure-macos-icon.sh" "$PUBLISH_DIR" "$REPO_ROOT" || true)"
  if [[ -z "$icon_source" ]]; then
    echo "macOS packaging preflight: unable to resolve chummer.icns from publish or Chummer paths." >&2
    return 1
  fi
  if [[ "$icon_source" != *.icns ]]; then
    echo "macOS packaging preflight: icon source is not .icns: $icon_source" >&2
    return 1
  fi

  echo "macOS packaging preflight: publish=$PUBLISH_DIR launch=$LAUNCH_TARGET icon=$icon_source" >&2
  printf '%s' "$icon_source"
}

bundle_demo_character_fixture() {
  local source_path
  if ! source_path="$(resolve_demo_character_source)"; then
    echo "warning: bundled demo character fixture not found; release will not include the legacy sample." >&2
    return 0
  fi

  local samples_dir="$PUBLISH_DIR/Samples/Legacy"
  mkdir -p "$samples_dir"
  cp "$source_path" "$samples_dir/Soma-Career.chum5"
  cat > "$samples_dir/README.txt" <<'EOF'
Bundled legacy sample fixture:
- source repo: chummer5a
- source path: Chummer.Tests/TestFiles/Soma (Career).chum5
- purpose: load a completed SR5 runner in the desktop shell after install
EOF
}

build_payload_zip() {
  local target="$1"
  build_payload_zip_from_dir "$PUBLISH_DIR" "$target"
}

build_payload_zip_from_dir() {
  local source_dir="$1"
  local target="$2"
  "$PYTHON_BIN" "$SCRIPT_DIR/build-reproducible-zip.py" "$source_dir" "$target"
}

append_payload_zip_to_windows_installer() {
  local installer_path="$1"
  local payload_zip="$2"
  python3 - "$installer_path" "$payload_zip" <<'PY'
import struct
import sys
from pathlib import Path

installer = Path(sys.argv[1])
payload = Path(sys.argv[2])
magic = b"CHUMMER6PAYLOAD1"

if not installer.is_file():
    raise SystemExit(f"installer not found: {installer}")
if not payload.is_file():
    raise SystemExit(f"payload zip not found: {payload}")

payload_bytes = payload.read_bytes()
with installer.open("ab") as handle:
    handle.write(payload_bytes)
    handle.write(struct.pack("<q", len(payload_bytes)))
    handle.write(magic)

print(installer)
PY
}

verify_windows_installer_payload_gate() {
  local installer_path="$1"
  local payload_path="${2:-}"
  local -a gate_args=(
    --installer "$installer_path"
    --expected-launch "$LAUNCH_TARGET"
    --heads-json-base64 "$heads_json_base64"
  )

  if [[ -n "$primary_relative_root" ]]; then
    gate_args+=(--expected-entry "$primary_relative_root/$LAUNCH_TARGET")
  else
    gate_args+=(--expected-entry "$LAUNCH_TARGET")
  fi

  if [[ -n "$secondary_head_key" && -n "$secondary_launch_target" ]]; then
    gate_args+=(--expected-launch "$secondary_launch_target")
    gate_args+=(--expected-entry "$secondary_relative_root/$secondary_launch_target")
  fi

  if [[ -n "$payload_path" ]]; then
    gate_args+=(--payload "$payload_path" --files-dir "$(dirname "$payload_path")")
  fi
  if [[ "$installer_mode" == "bootstrap" ]]; then
    gate_args+=(--require-embedded-bootstrap-metadata)
  fi

  if ! "$PYTHON_BIN" "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${gate_args[@]}"; then
    if [[ -n "$payload_path" ]]; then
      echo "Windows bootstrap installer proof failed. Keep this artifact off the promoted shelf until the payload gate passes." >&2
    else
      echo "Windows bundled installer proof failed. Rebuild the installer payload before promotion." >&2
    fi
    return 1
  fi
}

build_payload_tar_gz() {
  local target="$1"
  python3 - "$PUBLISH_DIR" "$target" <<'PY'
import sys
import tarfile
from pathlib import Path

source = Path(sys.argv[1])
target = Path(sys.argv[2])
if not source.exists():
    raise SystemExit(f"publish directory not found: {source}")
if target.exists():
    target.unlink()
with tarfile.open(target, "w:gz") as tf:
    for file in sorted(source.rglob("*")):
        if file.is_file():
            tf.add(file, arcname=file.relative_to(source))
print(target)
PY
}

normalize_deb_version() {
  python3 - "$VERSION" <<'PY'
import re
import sys

raw = sys.argv[1].strip() or "0~local"
value = re.sub(r"[^0-9A-Za-z.+~:-]+", "-", raw)
value = value.strip(".-:+~") or "0~local"
if not value[0].isdigit():
    value = f"0~{value}"
print(value)
PY
}

macos_bundle_identifier() {
  python3 - "$APP_KEY" "$RID" <<'PY'
import re
import sys

app_key = re.sub(r"[^A-Za-z0-9]+", "-", sys.argv[1]).strip("-").lower() or "desktop"
rid = re.sub(r"[^A-Za-z0-9]+", "-", sys.argv[2]).strip("-").lower() or "local"
print(f"net.chummer6.{app_key}.{rid}")
PY
}

linux_deb_arch() {
  case "$RID" in
    linux-x64) echo "amd64" ;;
    linux-arm64) echo "arm64" ;;
    *)
      echo "Unsupported Linux RID for deb packaging: $RID" >&2
      exit 1
      ;;
  esac
}

linux_deb_depends() {
  case "$APP_KEY" in
    avalonia)
      cat <<'EOF'
libfontconfig1, libfreetype6, zlib1g
EOF
      ;;
    blazor-desktop)
      cat <<'EOF'
libwebkit2gtk-4.1-0, libnotify4, libnss3, libxss1, libasound2 | libasound2t64, xdg-utils
EOF
      ;;
    *)
      return 0
      ;;
  esac
}

ensure_self_contained_publish() {
  ensure_self_contained_publish_dir "$PUBLISH_DIR" "$LAUNCH_TARGET"
}

ensure_self_contained_publish_dir() {
  local publish_dir="$1"
  local launch_target="$2"
  local launch_stem
  launch_stem="$launch_target"
  if [[ "$launch_stem" == *.exe ]]; then
    launch_stem="${launch_stem%.exe}"
  fi
  local runtimeconfig_path="$publish_dir/$launch_stem.runtimeconfig.json"

  if [[ ! -f "$runtimeconfig_path" ]]; then
    return 0
  fi

  python3 - "$runtimeconfig_path" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
payload = json.loads(path.read_text(encoding="utf-8"))
runtime_options = payload.get("runtimeOptions") or {}

# Framework-dependent desktop publishes still carry framework/frameworks.
# Self-contained desktop publishes should not require a shared runtime here.
if runtime_options.get("framework") or runtime_options.get("frameworks"):
    raise SystemExit(
        f"framework-dependent desktop publish detected: {path}. "
        "Re-publish with --self-contained true before building installers."
    )
PY
}

public_windows_bootstrap_requires_expanded_apphost() {
  case "$(desktop_release_channel)" in
    preview|stable|public_stable)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

require_expanded_windows_apphost_dir() {
  local publish_dir="$1"
  local launch_target="$2"

  if [[ ! -f "$publish_dir/System.Private.CoreLib.dll" ]]; then
    echo "Public Windows bootstrap packaging requires an expanded self-contained apphost: $publish_dir/System.Private.CoreLib.dll is missing." >&2
    echo "Re-publish $launch_target with -p:PublishSingleFile=false before building preview or stable installers." >&2
    exit 1
  fi
}

build_portable_artifacts() {
  ensure_self_contained_publish

  case "$RID" in
    win-*)
      local portable_exe="$DIST_DIR/chummer-$APP_KEY-$RID.exe"
      local portable_zip="$DIST_DIR/chummer-$APP_KEY-$RID.zip"
      if [[ ! -f "$PUBLISH_DIR/$LAUNCH_TARGET" ]]; then
        echo "Launch target not found in Windows publish directory: $PUBLISH_DIR/$LAUNCH_TARGET" >&2
        exit 1
      fi
      cp "$PUBLISH_DIR/$LAUNCH_TARGET" "$portable_exe"
      build_payload_zip "$portable_zip"
      echo "built portable $portable_exe"
      echo "built archive $portable_zip"
      ;;
    linux-*|osx-*)
      local portable_archive="$DIST_DIR/chummer-$APP_KEY-$RID.tar.gz"
      build_payload_tar_gz "$portable_archive"
      echo "built archive $portable_archive"
      ;;
    *)
      echo "Unsupported portable target RID: $RID" >&2
      exit 1
      ;;
  esac
}

build_macos_installer() {
  ensure_self_contained_publish

  if ! command -v hdiutil >/dev/null 2>&1; then
    echo "hdiutil is required for macOS dmg packaging." >&2
    exit 1
  fi

  local installer_name="chummer-$APP_KEY-$RID-installer.dmg"
  local stage_root="$DIST_DIR/package-$APP_KEY-$RID"
  local app_bundle="$stage_root/$APP_DISPLAY.app"
  local contents_dir="$app_bundle/Contents"
  local macos_dir="$contents_dir/MacOS"
  local macos_icon_source
  local macos_icon_runtime_source
  local macos_icon_name
  local macos_icon_plist_name
  local plist_path="$contents_dir/Info.plist"
  local bundle_identifier
  local original_publish_dir
  local stage_size_kb
  local image_size_mb
  bundle_identifier="$(macos_bundle_identifier)"
  local hdiutil_tmp_root="${CHUMMER_DESKTOP_INSTALLER_TMPDIR:-${TMPDIR:-$DIST_DIR/tmp}}"
  local hdiutil_tmp_work="$hdiutil_tmp_root/hdiutil-$APP_KEY-$RID"

  cleanup_macos_installer_staging() {
    trap - RETURN
    rm -rf "$stage_root" "$hdiutil_tmp_work"
  }
  trap cleanup_macos_installer_staging RETURN

  rm -rf "$stage_root"
  rm -rf "$hdiutil_tmp_work"
  mkdir -p "$contents_dir/Resources"
  mkdir -p "$hdiutil_tmp_work"

  if ! macos_icon_source="$(preflight_macos_packaging_requirements)"; then
    echo "macOS packaging preflight failed." >&2
    exit 1
  fi

  original_publish_dir="$PUBLISH_DIR"
  mv "$PUBLISH_DIR" "$macos_dir"
  PUBLISH_DIR="$macos_dir"

  if [[ ! -f "$macos_dir/$LAUNCH_TARGET" ]]; then
    echo "Launch target not found in macOS publish directory: $macos_dir/$LAUNCH_TARGET" >&2
    exit 1
  fi
  chmod 0755 "$macos_dir/$LAUNCH_TARGET"

  macos_icon_runtime_source="$macos_icon_source"
  if [[ "$macos_icon_source" == "$original_publish_dir" ]]; then
    macos_icon_runtime_source="$macos_dir"
  elif [[ "$macos_icon_source" == "$original_publish_dir/"* ]]; then
    macos_icon_runtime_source="$macos_dir/${macos_icon_source#"$original_publish_dir/"}"
  fi

  macos_icon_name="$(basename "$macos_icon_source")"
  macos_icon_plist_name="${macos_icon_name%.icns}"
  cp "$macos_icon_runtime_source" "$contents_dir/Resources/$macos_icon_name"

  echo "Using macOS icon source: $macos_icon_source" >&2

  cat > "$plist_path" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_DISPLAY</string>
  <key>CFBundleExecutable</key>
  <string>$LAUNCH_TARGET</string>
  <key>CFBundleIdentifier</key>
  <string>$bundle_identifier</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$APP_DISPLAY</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>CFBundleIconFile</key>
  <string>$macos_icon_plist_name</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
EOF

  sign_macos_app_bundle_if_configured "$app_bundle"

  rm -f "$DIST_DIR/$installer_name"
  stage_size_kb="$(du -sk "$stage_root" | awk '{print $1}')"
  image_size_mb=$(( (stage_size_kb + 1023) / 1024 ))
  image_size_mb=$(( image_size_mb + image_size_mb / 2 + 256 ))
  echo "macOS dmg sizing: staged=${stage_size_kb}KiB image=${image_size_mb}MiB tmpdir=$hdiutil_tmp_work" >&2
  if ! TMPDIR="$hdiutil_tmp_work" hdiutil create \
    -volname "$APP_DISPLAY" \
    -srcfolder "$stage_root" \
    -fs HFS+ \
    -size "${image_size_mb}m" \
    -ov \
    -format UDZO \
    "$DIST_DIR/$installer_name" >/dev/null; then
    echo "hdiutil create failed for $installer_name (tmpdir=$hdiutil_tmp_work)." >&2
    echo "Set CHUMMER_DESKTOP_INSTALLER_TMPDIR to a workspace-backed path with sufficient free space and rerun." >&2
    exit 1
  fi

  finalize_macos_signing_receipt "$DIST_DIR/$installer_name"
  echo "built installer $DIST_DIR/$installer_name"
}

build_installed_heads_json_base64() {
  local primary_head_key="$1"
  local primary_launch_target="$2"
  local primary_relative_root="$3"
  local secondary_head_key="${4:-}"
  local secondary_launch_target="${5:-}"
  local secondary_relative_root="${6:-}"

  python3 - \
    "$primary_head_key" \
    "$primary_launch_target" \
    "$primary_relative_root" \
    "$secondary_head_key" \
    "$secondary_launch_target" \
    "$secondary_relative_root" <<'PY'
import base64
import json
import sys

primary_head_key, primary_launch_target, primary_relative_root, secondary_head_key, secondary_launch_target, secondary_relative_root = sys.argv[1:7]

DISPLAY_NAMES = {
    "avalonia": "Chummer6 Avalonia Desktop",
    "blazor-desktop": "Chummer6 Blazor Desktop",
}

SHORTCUT_NAMES = {
    "avalonia": "Chummer6 Avalonia",
    "blazor-desktop": "Chummer6 Blazor Desktop",
}

heads = [
    {
        "headId": primary_head_key,
        "displayName": DISPLAY_NAMES[primary_head_key],
        "launchExecutable": primary_launch_target,
        "shortcutName": "Chummer6 Desktop" if secondary_head_key and primary_head_key == "avalonia" else SHORTCUT_NAMES[primary_head_key],
        "relativeRoot": primary_relative_root,
    }
]

if secondary_head_key:
    heads.append(
        {
            "headId": secondary_head_key,
            "displayName": DISPLAY_NAMES[secondary_head_key],
            "launchExecutable": secondary_launch_target,
            "shortcutName": SHORTCUT_NAMES[secondary_head_key],
            "relativeRoot": secondary_relative_root,
        }
    )

payload = json.dumps(heads, separators=(",", ":")).encode("utf-8")
print(base64.b64encode(payload).decode("ascii"))
PY
}

escape_nsis_define() {
  python3 - "$1" <<'PY'
import sys

value = sys.argv[1]
value = value.replace("$", "$$")
value = value.replace('"', '$\\"')
print(value)
PY
}

write_windows_bootstrap_config() {
  local config_path="$1"
  local stage_dir="$2"
  local icon_path="$3"
  local installer_display_name="$4"
  local installer_install_dir_name="$5"
  local installer_output_name="$6"
  local payload_file_name="$7"
  local payload_url="$8"
  local payload_sha256="$9"
  local payload_size_bytes="${10}"
  local head_count="${11}"
  local head1_id="${12}"
  local head1_display_name="${13}"
  local head1_launch_executable="${14}"
  local head1_shortcut_name="${15}"
  local head1_relative_root="${16}"
  local head2_id="${17:-}"
  local head2_display_name="${18:-}"
  local head2_launch_executable="${19:-}"
  local head2_shortcut_name="${20:-}"
  local head2_relative_root="${21:-}"
  local payload_acquisition_mode="${22:-download}"
  local embedded_payload_path="${23:-}"

  local rid_suffix="${RID#win-}"

  python3 - "$config_path" "$stage_dir" "$icon_path" "$APP_KEY" "$RID" "$rid_suffix" "$installer_display_name" "$installer_install_dir_name" "$VERSION" "ArchonMegalon" "$SHORTCUT_NAME" "$installer_output_name" "$payload_file_name" "$payload_url" "$payload_sha256" "$payload_size_bytes" "$head_count" "$head1_id" "$head1_display_name" "$head1_launch_executable" "$head1_shortcut_name" "$head1_relative_root" "$head2_id" "$head2_display_name" "$head2_launch_executable" "$head2_shortcut_name" "$head2_relative_root" "$payload_acquisition_mode" "$embedded_payload_path" <<'PY'
from pathlib import Path
import sys

(
    config_path,
    stage_dir,
    icon_path,
    app_id,
    rid,
    rid_suffix,
    display_name,
    install_dir_name,
    version,
    publisher,
    shortcut_name,
    installer_output_name,
    payload_file_name,
    payload_url,
    payload_sha256,
    payload_size_bytes,
    head_count,
    head1_id,
    head1_display_name,
    head1_launch_executable,
    head1_shortcut_name,
    head1_relative_root,
    head2_id,
    head2_display_name,
    head2_launch_executable,
    head2_shortcut_name,
    head2_relative_root,
    payload_acquisition_mode,
    embedded_payload_path,
) = sys.argv[1:]


def esc(value: str) -> str:
    return value.replace("$", "$$").replace('"', '$\\"')


lines = [
    '!define CHUMMER_STAGE_DIR "/work"',
    f'!define CHUMMER_ICON_PATH "/work/{esc(Path(icon_path).name)}"',
    f'!define CHUMMER_APP_ID "{esc(app_id)}"',
    f'!define CHUMMER_RID "{esc(rid)}"',
    f'!define CHUMMER_RID_SUFFIX "{esc(rid_suffix)}"',
    f'!define CHUMMER_DISPLAY_NAME "{esc(display_name)}"',
    f'!define CHUMMER_INSTALL_DIR_NAME "{esc(install_dir_name)}"',
    f'!define CHUMMER_VERSION "{esc(version)}"',
    f'!define CHUMMER_PUBLISHER "{esc(publisher)}"',
    f'!define CHUMMER_SHORTCUT_NAME "{esc(shortcut_name)}"',
    f'!define CHUMMER_INSTALLER_OUTPUT_NAME "{esc(installer_output_name)}"',
    f'!define CHUMMER_PAYLOAD_FILE_NAME "{esc(payload_file_name)}"',
    f'!define CHUMMER_PAYLOAD_URL "{esc(payload_url)}"',
    f'!define CHUMMER_PAYLOAD_SHA256 "{esc(payload_sha256)}"',
    f'!define CHUMMER_PAYLOAD_SIZE_BYTES "{esc(payload_size_bytes)}"',
    f'!define CHUMMER_PAYLOAD_ACQUISITION_MODE "{esc(payload_acquisition_mode)}"',
    f'!define CHUMMER_ARCH "{esc(rid.split("-")[-1])}"',
    f'!define CHUMMER_HEAD_COUNT "{esc(head_count)}"',
    f'!define CHUMMER_HEAD_1_ID "{esc(head1_id)}"',
    f'!define CHUMMER_HEAD_1_DISPLAY_NAME "{esc(head1_display_name)}"',
    f'!define CHUMMER_HEAD_1_LAUNCH_EXECUTABLE "{esc(head1_launch_executable)}"',
    f'!define CHUMMER_HEAD_1_SHORTCUT_NAME "{esc(head1_shortcut_name)}"',
    f'!define CHUMMER_HEAD_1_RELATIVE_ROOT "{esc(head1_relative_root)}"',
    f'!define CHUMMER_HEAD_2_ID "{esc(head2_id)}"',
    f'!define CHUMMER_HEAD_2_DISPLAY_NAME "{esc(head2_display_name)}"',
    f'!define CHUMMER_HEAD_2_LAUNCH_EXECUTABLE "{esc(head2_launch_executable)}"',
    f'!define CHUMMER_HEAD_2_SHORTCUT_NAME "{esc(head2_shortcut_name)}"',
    f'!define CHUMMER_HEAD_2_RELATIVE_ROOT "{esc(head2_relative_root)}"',
]

if embedded_payload_path:
    lines.append(f'!define CHUMMER_EMBEDDED_PAYLOAD_PATH "{esc(embedded_payload_path)}"')

Path(config_path).write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
}

build_windows_installer() {
  ensure_self_contained_publish

  local payload_zip="$DIST_DIR/chummer-$APP_KEY-$RID-payload.zip"
  local payload_resource_name="ChummerInstaller.Payload.zip"
  local installer_name="chummer-$APP_KEY-$RID-installer.exe"
  local installer_out_dir="$DIST_DIR/installer-$APP_KEY-$RID"
  local native_bootstrap_stage_dir="$DIST_DIR/native-bootstrap-$APP_KEY-$RID"
  local payload_source_dir="$PUBLISH_DIR"
  local primary_relative_root=""
  local secondary_head_key="$WINDOWS_SECONDARY_HEAD_KEY"
  local secondary_publish_dir="$WINDOWS_SECONDARY_HEAD_PUBLISH_DIR"
  local secondary_launch_target="$WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET"
  local secondary_relative_root="$WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT"
  local installer_display_name="$APP_DISPLAY"
  local installer_install_dir_name="$INSTALL_DIR_NAME-$RID"
  local heads_json_base64=""
  local stage_root=""
  local installer_mode="${CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap}"
  local bootstrap_payload_url=""
  local bootstrap_payload_sha256=""
  local bootstrap_payload_size_bytes=""
  local bootstrap_payload_acquisition_mode="download"
  local bootstrap_embedded_payload_path=""
  local head_count="1"
  local head1_id="$APP_KEY"
  local head1_display_name="$installer_display_name"
  local head1_launch_executable="$LAUNCH_TARGET"
  local head1_shortcut_name="$SHORTCUT_NAME"
  local head1_relative_root=""
  local head2_id=""
  local head2_display_name=""
  local head2_launch_executable=""
  local head2_shortcut_name=""
  local head2_relative_root=""

  if [[ -n "$secondary_head_key" || -n "$secondary_publish_dir" || -n "$secondary_launch_target" ]]; then
    if [[ -z "$secondary_head_key" || -z "$secondary_publish_dir" || -z "$secondary_launch_target" ]]; then
      echo "Combined Windows installer packaging requires CHUMMER_WINDOWS_SECONDARY_HEAD_KEY, CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR, and CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET together." >&2
      exit 1
    fi

    ensure_self_contained_publish_dir "$secondary_publish_dir" "$secondary_launch_target"

    primary_relative_root="$APP_KEY"
    secondary_relative_root="${secondary_relative_root:-$secondary_head_key}"
    installer_display_name="Chummer6 Desktop"
    installer_install_dir_name="Desktop-$RID"
    stage_root="$DIST_DIR/package-$APP_KEY-$RID"
    rm -rf "$stage_root"
    mkdir -p "$stage_root/$primary_relative_root" "$stage_root/$secondary_relative_root"
    cp -a "$PUBLISH_DIR"/. "$stage_root/$primary_relative_root"/
    cp -a "$secondary_publish_dir"/. "$stage_root/$secondary_relative_root"/
    payload_source_dir="$stage_root"
    head_count="2"
    head1_id="$APP_KEY"
    head1_display_name="$(resolve_head_display_name "$APP_KEY")"
    head1_launch_executable="$LAUNCH_TARGET"
    head1_shortcut_name="Chummer6 Desktop"
    head1_relative_root="$primary_relative_root"
    head2_id="$secondary_head_key"
    head2_display_name="$(resolve_head_display_name "$secondary_head_key")"
    head2_launch_executable="$secondary_launch_target"
    head2_shortcut_name="$(resolve_head_shortcut_name "$secondary_head_key")"
    head2_relative_root="$secondary_relative_root"
    heads_json_base64="$(build_installed_heads_json_base64 \
      "$APP_KEY" \
      "$LAUNCH_TARGET" \
      "$primary_relative_root" \
      "$secondary_head_key" \
      "$secondary_launch_target" \
      "$secondary_relative_root")"
  fi

  build_payload_zip_from_dir "$payload_source_dir" "$payload_zip"
  bootstrap_payload_sha256="$(sha256_file "$payload_zip")"
  bootstrap_payload_size_bytes="$(file_size_bytes "$payload_zip")"

  case "$(echo "$installer_mode" | tr '[:upper:]' '[:lower:]')" in
    bootstrap)
      installer_mode="bootstrap"
      local downloads_prefix="${CHUMMER_PUBLIC_DOWNLOADS_PREFIX:-https://chummer.run/downloads/files}"
      bootstrap_payload_url="${CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL:-${downloads_prefix%/}/$(basename "$payload_zip")}"
      bootstrap_payload_acquisition_mode="$(echo "${CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE:-download}" | tr '[:upper:]' '[:lower:]')"
      case "$bootstrap_payload_acquisition_mode" in
        download|embedded)
          ;;
        *)
          echo "Unsupported CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE: $bootstrap_payload_acquisition_mode (expected download or embedded)." >&2
          exit 1
          ;;
      esac
      ;;
    bundled|append|appended)
      installer_mode="bundled"
      ;;
    *)
      echo "Unsupported CHUMMER_WINDOWS_INSTALLER_MODE: $installer_mode (expected bootstrap or bundled)." >&2
      exit 1
      ;;
  esac

  if [[ "$installer_mode" == "bootstrap" ]] && public_windows_bootstrap_requires_expanded_apphost; then
    require_expanded_windows_apphost_dir "$PUBLISH_DIR" "$LAUNCH_TARGET"
    if [[ -n "$secondary_publish_dir" ]]; then
      require_expanded_windows_apphost_dir "$secondary_publish_dir" "$secondary_launch_target"
    fi
  fi

    if [[ "$installer_mode" == "bootstrap" ]]; then
    rm -rf "$native_bootstrap_stage_dir"
    mkdir -p "$native_bootstrap_stage_dir"
    cp -f "$REPO_ROOT/Chummer/chummer.ico" "$native_bootstrap_stage_dir/chummer.ico"
    if [[ "$bootstrap_payload_acquisition_mode" == "embedded" ]]; then
      cp -f "$payload_zip" "$native_bootstrap_stage_dir/$(basename "$payload_zip")"
      bootstrap_embedded_payload_path="/work/$(basename "$payload_zip")"
    fi
    write_windows_bootstrap_config \
      "$native_bootstrap_stage_dir/bootstrap-config.nsh" \
      "$native_bootstrap_stage_dir" \
      "$native_bootstrap_stage_dir/chummer.ico" \
      "$installer_display_name" \
      "$installer_install_dir_name" \
      "Chummer6Installer-$APP_KEY-$RID" \
      "$(basename "$payload_zip")" \
      "$bootstrap_payload_url" \
      "$bootstrap_payload_sha256" \
      "$bootstrap_payload_size_bytes" \
      "$head_count" \
      "$head1_id" \
      "$head1_display_name" \
      "$head1_launch_executable" \
      "$head1_shortcut_name" \
      "$head1_relative_root" \
      "$head2_id" \
      "$head2_display_name" \
      "$head2_launch_executable" \
      "$head2_shortcut_name" \
      "$head2_relative_root" \
      "$bootstrap_payload_acquisition_mode" \
      "$bootstrap_embedded_payload_path"
    "$REPO_ROOT/scripts/build-native-windows-bootstrap-installer.sh" \
      "$native_bootstrap_stage_dir" \
      "$DIST_DIR/$installer_name"
    mkdir -p "$DIST_DIR/files"
    cp -f "$payload_zip" "$DIST_DIR/files/$(basename "$payload_zip")"
    cat > "$DIST_DIR/files/$(basename "$payload_zip").json" <<EOF
{
  "contractName": "chummer6-ui.windows_bootstrap_payload",
  "fileName": "$(basename "$payload_zip")",
  "downloadUrl": "$bootstrap_payload_url",
  "sha256": "$bootstrap_payload_sha256",
  "sizeBytes": $bootstrap_payload_size_bytes,
  "payloadAcquisitionMode": "$bootstrap_payload_acquisition_mode",
  "installerFileName": "$installer_name",
  "releaseVersion": "$VERSION"
}
EOF
    # Refresh the staged installer copy before running the payload gate so an
    # earlier shelf build cannot be mistaken for the installer we just emitted.
    cp -f "$DIST_DIR/$installer_name" "$DIST_DIR/files/$installer_name"
    verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"
  else
    rm -rf "$installer_out_dir"
    "$REPO_ROOT/scripts/ai/with-package-plane.sh" publish "$REPO_ROOT/Chummer.Desktop.Installer/Chummer.Desktop.Installer.csproj" \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:GenerateRuntimeConfigurationFiles=true \
      -p:PublishTrimmed=false \
      -p:EnableCompressionInSingleFile=false \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:ChummerInstallerPayloadRequired=false \
      -p:ChummerInstallerEmbedPayload=false \
      -p:ChummerInstallerIncludeSidecarPayload=false \
      -p:ChummerInstallerAssemblyName="Chummer6Installer-$APP_KEY-$RID" \
      -p:InstallerPayloadZip="$payload_zip" \
      -p:ChummerInstallerPayloadResourceName="$payload_resource_name" \
      -p:ChummerInstallerPayloadUrl="$bootstrap_payload_url" \
      -p:ChummerInstallerPayloadSha256="$bootstrap_payload_sha256" \
      -p:ChummerInstallerPayloadSizeBytes="$bootstrap_payload_size_bytes" \
      -p:ChummerInstallerAppId="$APP_KEY-$RID" \
      -p:ChummerInstallerHeadId="$APP_KEY" \
      -p:ChummerInstallerDisplayName="$installer_display_name" \
      -p:ChummerInstallerInstallDirName="$installer_install_dir_name" \
      -p:ChummerInstallerLaunchExecutable="$LAUNCH_TARGET" \
      -p:ChummerInstallerVersion="$VERSION" \
      -p:ChummerInstallerShortcutName="$SHORTCUT_NAME" \
      -p:ChummerInstallerHeadsJsonBase64="$heads_json_base64" \
      -p:ChummerInstallerOutputName="Chummer6Installer-$APP_KEY-$RID" \
      -o "$installer_out_dir"

    local installer_source
    installer_source="$(find "$installer_out_dir" -maxdepth 1 -type f -name '*.exe' | sort | head -n 1)"
    if [[ -z "$installer_source" ]]; then
      echo "Installer publish output did not produce a .exe in $installer_out_dir" >&2
      exit 1
    fi

    cp "$installer_source" "$DIST_DIR/$installer_name"
    append_payload_zip_to_windows_installer "$DIST_DIR/$installer_name" "$payload_zip"
    verify_windows_installer_payload_gate "$DIST_DIR/$installer_name"
  fi
  rm -f "$payload_zip"
  if [[ -n "$stage_root" ]]; then
    rm -rf "$stage_root"
  fi
  if [[ -d "$native_bootstrap_stage_dir" ]]; then
    rm -rf "$native_bootstrap_stage_dir"
  fi
  echo "built installer $DIST_DIR/$installer_name"
}

build_linux_installer() {
  ensure_self_contained_publish

  local deb_arch
  deb_arch="$(linux_deb_arch)"
  local deb_version
  deb_version="$(normalize_deb_version)"
  local installer_name="chummer-$APP_KEY-$RID-installer.deb"
  local stage_root="$DIST_DIR/package-$APP_KEY-$RID"
  local install_root="$stage_root/opt/chummer6/$APP_KEY-$RID"
  local wrapper_path="$stage_root/usr/bin/chummer6-$APP_KEY"
  local desktop_path="$stage_root/usr/share/applications/chummer6-$APP_KEY.desktop"
  local deb_depends
  deb_depends="$(linux_deb_depends || true)"

  rm -rf "$stage_root"
  mkdir -p "$stage_root/DEBIAN" "$install_root" "$(dirname "$wrapper_path")" "$(dirname "$desktop_path")"
  cp -a "$PUBLISH_DIR"/. "$install_root"/

  if [[ ! -f "$install_root/$LAUNCH_TARGET" ]]; then
    echo "Launch target not found in publish directory: $install_root/$LAUNCH_TARGET" >&2
    exit 1
  fi
  chmod 0755 "$install_root/$LAUNCH_TARGET"

  cat > "$stage_root/DEBIAN/control" <<EOF
Package: chummer6-$APP_KEY
Version: $deb_version
Section: games
Priority: optional
Architecture: $deb_arch
Maintainer: ArchonMegalon
Description: $APP_DISPLAY
 Installer package for the $APP_DISPLAY head.
EOF
  if [[ -n "$deb_depends" ]]; then
    printf 'Depends: %s\n' "$deb_depends" >> "$stage_root/DEBIAN/control"
  fi

  cat > "$wrapper_path" <<EOF
#!/usr/bin/env bash
set -euo pipefail
exec "/opt/chummer6/$APP_KEY-$RID/$LAUNCH_TARGET" "\$@"
EOF
  chmod 0755 "$wrapper_path"

  cat > "$desktop_path" <<EOF
[Desktop Entry]
Type=Application
Name=$APP_DISPLAY
Exec=/usr/bin/chummer6-$APP_KEY %u
Terminal=false
Categories=Game;
StartupNotify=true
MimeType=x-scheme-handler/chummer;
EOF

  cat > "$stage_root/DEBIAN/postinst" <<EOF
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
fi
if command -v xdg-mime >/dev/null 2>&1; then
  xdg-mime default chummer6-$APP_KEY.desktop x-scheme-handler/chummer >/dev/null 2>&1 || true
fi
exit 0
EOF
  find "$stage_root" -type d -exec chmod 0755 {} +
  chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"
  chmod 0755 "$stage_root/DEBIAN/postinst"

  if dpkg-deb --help 2>&1 | grep -q -- '--root-owner-group'; then
    dpkg-deb --root-owner-group --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  elif command -v fakeroot >/dev/null 2>&1; then
    fakeroot dpkg-deb --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  else
    dpkg-deb --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  fi

  rm -rf "$stage_root"
  echo "built installer $DIST_DIR/$installer_name"
}

case "$RID" in
  win-*)
    bundle_demo_character_fixture
    require_publishable_release_version
    prune_release_symbols
    pre_sign_windows_payloads_if_configured
    build_portable_artifacts
    begin_windows_build_provenance
    build_windows_installer
    finalize_windows_signing_receipt
    bind_windows_publication_candidate_to_signing_receipt
    finalize_windows_build_provenance
    stage_installer_for_downloads_manifest "chummer-$APP_KEY-$RID-installer.exe"
    ;;
  linux-*)
    bundle_demo_character_fixture
    require_publishable_release_version
    prune_release_symbols
    build_portable_artifacts
    build_linux_installer
    stage_installer_for_downloads_manifest "chummer-$APP_KEY-$RID-installer.deb"
    ;;
  osx-*)
    bundle_demo_character_fixture
    require_publishable_release_version
    prune_release_symbols
    sign_macos_publish_binary_if_configured
    build_portable_artifacts
    build_macos_installer
    ;;
  *)
    echo "Unsupported installer target RID: $RID" >&2
    exit 1
    ;;
esac
