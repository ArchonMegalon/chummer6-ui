#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REGISTRY_ROOT="$("$SCRIPT_DIR/resolve-hub-registry-root.sh")"

BUNDLE_DIR="${1:-${DOWNLOAD_BUNDLE_DIR:-$REPO_ROOT/dist}}"
MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
CANONICAL_MANIFEST_SOURCE="$BUNDLE_DIR/RELEASE_CHANNEL.generated.json"
FILES_SOURCE="$BUNDLE_DIR/files"
S3_TARGET_URI="${CHUMMER_PORTAL_DOWNLOADS_S3_URI:-}"
S3_LATEST_URI="${CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI:-}"
S3_ENDPOINT_URL="${CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL:-}"
VERIFY_URL="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}"
STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE:-$BUNDLE_DIR/startup-smoke}"

if [[ ! -f "$MANIFEST_SOURCE" || ! -d "$FILES_SOURCE" ]]; then
  echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2
  exit 1
fi

if [[ ! -f "$SCRIPT_DIR/generate-releases-manifest.sh" ]]; then
  echo "Missing manifest generator: $SCRIPT_DIR/generate-releases-manifest.sh" >&2
  exit 1
fi

if [[ ! -f "$SCRIPT_DIR/verify-windows-installer-payloads.py" ]]; then
  echo "Missing Windows installer payload gate: $SCRIPT_DIR/verify-windows-installer-payloads.py" >&2
  exit 1
fi

windows_payload_gate_args=(
  --files-dir "$FILES_SOURCE"
  --manifest "$MANIFEST_SOURCE"
  --require-embedded-bootstrap-metadata
  --require-manifest-row
)
while IFS= read -r installer_path; do
  [[ -n "$installer_path" ]] || continue
  windows_payload_gate_args+=(--installer "$installer_path")
done < <(find "$BUNDLE_DIR" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
while IFS= read -r installer_path; do
  [[ -n "$installer_path" ]] || continue
  windows_payload_gate_args+=(--installer "$installer_path")
done < <(find "$FILES_SOURCE" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
if [[ "${#windows_payload_gate_args[@]}" -eq 5 ]]; then
  windows_payload_gate_args+=(--allow-empty)
fi
python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${windows_payload_gate_args[@]}"

sync_source_dir="$(mktemp -d)"
cleanup() {
  rm -rf "$sync_source_dir"
}
trap cleanup EXIT

find "$FILES_SOURCE" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o \
     -name "chummer-avalonia-*.tar.gz" -o -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" \) \
  -exec cp {} "$sync_source_dir/" \;

DOWNLOADS_DIR="$sync_source_dir" \
MANIFEST_PATH="$MANIFEST_SOURCE" \
PORTAL_MANIFEST_PATH="$MANIFEST_SOURCE" \
PORTAL_DOWNLOADS_DIR="$BUNDLE_DIR" \
SOURCE_MANIFEST_PATH="$MANIFEST_SOURCE" \
STARTUP_SMOKE_DIR="$STARTUP_SMOKE_SOURCE" \
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-${CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH:-}}" \
CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH="${CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH:-}" \
CHUMMER_EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
bash "$SCRIPT_DIR/generate-releases-manifest.sh"

if [[ -z "$S3_TARGET_URI" ]]; then
  echo "Set CHUMMER_PORTAL_DOWNLOADS_S3_URI (for example: s3://bucket/path)." >&2
  exit 1
fi

if [[ -z "$VERIFY_URL" ]]; then
  echo "Set CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL to verify published manifest after object-storage sync." >&2
  exit 1
fi

export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS:-true}"

if ! command -v aws >/dev/null 2>&1; then
  echo "aws CLI is required for object-storage publish mode." >&2
  exit 1
fi

endpoint_args=()
if [[ -n "$S3_ENDPOINT_URL" ]]; then
  endpoint_args=(--endpoint-url "$S3_ENDPOINT_URL")
fi

copy_target() {
  local target_uri="$1"
  aws s3 cp "$FILES_SOURCE/" "$target_uri/files/" --recursive "${endpoint_args[@]}"
  aws s3 cp "$MANIFEST_SOURCE" "$target_uri/releases.json" "${endpoint_args[@]}"
  aws s3 cp "$CANONICAL_MANIFEST_SOURCE" "$target_uri/RELEASE_CHANNEL.generated.json" "${endpoint_args[@]}"
}

copy_target "$S3_TARGET_URI"
if [[ -n "$S3_LATEST_URI" ]]; then
  copy_target "$S3_LATEST_URI"
fi

bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$VERIFY_URL"

artifact_count="$(find "$FILES_SOURCE" -maxdepth 1 -type f \( \
  -name 'chummer-*.exe' -o \
  -name 'chummer-*.zip' -o \
  -name 'chummer-*.tar.gz' -o \
  -name 'chummer-*-installer.exe' -o \
  -name 'chummer-*-installer.deb' -o \
  -name 'chummer-*-installer.pkg' -o \
  -name 'chummer-*-installer.dmg' -o \
  -name 'chummer-*-installer.msix' \
\) | wc -l | tr -d ' ')"
echo "Published ${artifact_count} desktop artifact(s) to object storage target: $S3_TARGET_URI"
if [[ -n "$S3_LATEST_URI" ]]; then
  echo "Also published latest alias target: $S3_LATEST_URI"
fi
