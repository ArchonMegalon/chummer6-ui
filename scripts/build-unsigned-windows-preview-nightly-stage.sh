#!/usr/bin/env bash
set -euo pipefail

# Prepare or replay the unsigned Windows-only preview-nightly candidate.
# This script has no upload, deployment, signing, or publication capability.

umask 077

# Do not let caller-selected programs or Python import paths participate in a
# release-authoritative build. Network proxy and certificate variables are
# admitted only at the isolated child boundary below; accepted bytes remain
# bound by the committed SHA-256 authorities.
export PATH="/usr/bin:/bin"
export PYTHONNOUSERSITE=1
export PYTHONSAFEPATH=1
unset CDPATH PYTHONHOME PYTHONPATH

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
PACKAGE_VERIFIER="$SCRIPT_DIR/ai/verify_fresh_checkout_package_plane.py"
MATERIALIZER="$SCRIPT_DIR/preview_nightly_unsigned_stage.py"
COMPOSITION_HELPER="$SCRIPT_DIR/preview_nightly_unsigned_composition.py"
PACKAGE_LOCK="$REPO_ROOT/config/package-plane.lock.json"
NATIVE_LOCK="$REPO_ROOT/config/windows-native-bootstrap-toolchain.lock.json"
SAMPLE_SOURCE="$REPO_ROOT/Chummer.Tests/TestFiles/Soma (Career).chum5"
COMPOSITION_NAME="PREVIEW_NIGHTLY_UNSIGNED_COMPOSITION.proposed.json"
MODE="${1:-}"

TRUSTED_BASH="/bin/bash"
TRUSTED_GIT="/usr/bin/git"
TRUSTED_PYTHON="/usr/bin/python3"
TRUSTED_ENV="/usr/bin/env"
TRUSTED_PATH="/usr/bin:/bin"
PUBLIC_DOWNLOADS_PREFIX="https://chummer.run/downloads/files"
BOOTSTRAP_PAYLOAD_URL="$PUBLIC_DOWNLOADS_PREFIX/chummer-avalonia-win-x64-payload.zip"
SEVENZIP_EXTRA_URL="https://github.com/ip7z/7zip/releases/download/26.02/7z2602-extra.7z"
SEVENZIP_EXTRA_SHA256="081df9e9311dfd9c9e0e98c1c80180b99bb51e4cb24156b5f3057fe3c259d70a"
CURL_WINDOWS_URL="https://curl.se/windows/dl-8.21.0_1/curl-8.21.0_1-win64-mingw.zip"
CURL_WINDOWS_SHA256="157068447d5b0b178dcc650f29d4746049fa4c7cc12db5f2bc050c0b84e48e7a"
CANONICAL_SOURCE_DATE_EPOCH="315532800"

die() {
  echo "[unsigned-windows-preview-nightly] $*" >&2
  exit 2
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "required command is unavailable: $1"
}

require_absolute() {
  [[ "$1" == /* ]] || die "$2 must be an absolute path"
}

reject_git_environment() {
  local variable_name
  while IFS= read -r variable_name; do
    if [[ "$variable_name" == GIT_* ]]; then
      die "ambient GIT_* variables are forbidden for source authority checks"
    fi
  done < <(compgen -e)
}

reject_ambient_builder_configuration() {
  # Every variable in this list can change accepted package bytes, embedded
  # metadata, the installer shape, or the authority used to produce them.
  # The unsigned preview lane establishes its own exact values below.
  local variable_name
  local -a forbidden=(
    SOURCE_DATE_EPOCH
    CHUMMER_UI_REPO_ROOT_ALIAS
    CHUMMER_PYTHON_BIN
    CHUMMER_RELEASE_CHANNEL
    CHUMMER_DESKTOP_RELEASE_CHANNEL
    CHUMMER_ALLOW_LOCAL_RELEASE_VERSION
    CHUMMER_RELEASE_INCLUDE_PDBS
    CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE
    CHUMMER_WINDOWS_SIGNING_REQUIRED
    CHUMMER_WINDOWS_SIGN_PFX_BASE64
    CHUMMER_WINDOWS_SIGN_PFX_PATH
    CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH
    CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED
    CHUMMER_WINDOWS_BUILD_PROVENANCE_REQUIRED
    CHUMMER_RELEASE_MANIFEST_STAGE_ONLY
    CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS
    CHUMMER_WINDOWS_SECONDARY_HEAD_KEY
    CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR
    CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET
    CHUMMER_WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT
    CHUMMER_RELEASE_SAMPLE_SOURCE
    CHUMMER_LEGACY_FIXTURE_ROOT
    CHUMMER_WINDOWS_INSTALLER_MODE
    CHUMMER_PUBLIC_DOWNLOADS_PREFIX
    CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL
    CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE
    CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_LOCK
    CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_CACHE_DIR
    CHUMMER_WINDOWS_7ZIP_EXTRA_URL
    CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256
    CHUMMER_WINDOWS_CURL_URL
    CHUMMER_WINDOWS_CURL_SHA256
    CHUMMER_WORKSPACE_ROOT
    CHUMMER_WINDOWS_BUILD_PROVENANCE_GENERATOR
    CHUMMER_WINDOWS_BUILD_PROVENANCE_SUPPORT
    CHUMMER_WINDOWS_SOURCE_CORE_ROOT
    CHUMMER_WINDOWS_SOURCE_RUN_SERVICES_ROOT
    CHUMMER_WINDOWS_SOURCE_UI_KIT_ROOT
    CHUMMER_WINDOWS_SOURCE_REGISTRY_ROOT
    CHUMMER_WINDOWS_SOURCE_MEDIA_ROOT
    CHUMMER_WINDOWS_SOURCE_LEGACY_ROOT
  )
  for variable_name in "${forbidden[@]}"; do
    if [[ -v "$variable_name" ]]; then
      die "ambient content-affecting builder variable is forbidden: $variable_name"
    fi
  done
}

reject_git_environment
reject_ambient_builder_configuration

case "$MODE" in
  prepare|verify)
    ;;
  *)
    echo "usage: scripts/build-unsigned-windows-preview-nightly-stage.sh <prepare|verify>" >&2
    exit 2
    ;;
esac

for path in "$PACKAGE_VERIFIER" "$MATERIALIZER" "$COMPOSITION_HELPER" "$PACKAGE_LOCK" "$NATIVE_LOCK" "$SAMPLE_SOURCE"; do
  [[ -f "$path" && ! -L "$path" ]] || die "required authority is missing or linked: $path"
done
for path in "$TRUSTED_BASH" "$TRUSTED_GIT" "$TRUSTED_PYTHON" "$TRUSTED_ENV"; do
  [[ -x "$path" ]] || die "trusted executable is unavailable: $path"
done

CANDIDATE_DIR="${CHUMMER_UNSIGNED_WINDOWS_PREVIEW_CANDIDATE_DIR:-}"
INCUMBENT_ROOT="${CHUMMER_UNSIGNED_WINDOWS_PREVIEW_INCUMBENT_ROOT:-}"
VERSION="${CHUMMER_UNSIGNED_WINDOWS_PREVIEW_VERSION:-}"
PUBLISHED_AT="${CHUMMER_UNSIGNED_WINDOWS_PREVIEW_PUBLISHED_AT:-}"

[[ -n "$CANDIDATE_DIR" ]] || die "CHUMMER_UNSIGNED_WINDOWS_PREVIEW_CANDIDATE_DIR is required"
[[ -n "$INCUMBENT_ROOT" ]] || die "CHUMMER_UNSIGNED_WINDOWS_PREVIEW_INCUMBENT_ROOT is required"
[[ -n "$VERSION" ]] || die "CHUMMER_UNSIGNED_WINDOWS_PREVIEW_VERSION is required"
require_absolute "$CANDIDATE_DIR" "candidate directory"
require_absolute "$INCUMBENT_ROOT" "incumbent root"
[[ -d "$INCUMBENT_ROOT" && ! -L "$INCUMBENT_ROOT" ]] || die "incumbent root must be one physical directory"

SOURCE_SHA="$("$TRUSTED_GIT" -C "$REPO_ROOT" rev-parse HEAD)"
[[ "$SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]] || die "repository HEAD is not an exact commit SHA"
if [[ -n "${CHUMMER_UNSIGNED_WINDOWS_PREVIEW_SOURCE_SHA:-}" ]]; then
  [[ "$SOURCE_SHA" == "$CHUMMER_UNSIGNED_WINDOWS_PREVIEW_SOURCE_SHA" ]] || die "repository HEAD differs from the required source SHA"
fi

PACKAGE_PLANE_RECEIPT="$CANDIDATE_DIR/provenance/UI_FRESH_PACKAGE_PLANE.generated.json"
RETAINED_MANIFEST="$CANDIDATE_DIR/provenance/retained-windows-publish-closure/manifest.json"
PACKAGE_LOCK_COPY="$CANDIDATE_DIR/provenance/config/package-plane.lock.json"
NATIVE_LOCK_COPY="$CANDIDATE_DIR/provenance/config/windows-native-bootstrap-toolchain.lock.json"
COMPOSITION_PATH="$CANDIDATE_DIR/$COMPOSITION_NAME"
PUBLICATION_ROOT="$CANDIDATE_DIR/publication"

verify_composition() {
  "$TRUSTED_PYTHON" -I "$COMPOSITION_HELPER" verify \
    --publication-root "$PUBLICATION_ROOT" \
    --incumbent-root "$INCUMBENT_ROOT" \
    --expected-version "$VERSION" \
    --source-sha "$SOURCE_SHA" \
    --package-plane-lock "$PACKAGE_LOCK_COPY" \
    --package-plane-receipt "$PACKAGE_PLANE_RECEIPT" \
    --retained-manifest "$RETAINED_MANIFEST" \
    --native-toolchain-lock "$NATIVE_LOCK_COPY" \
    --request "$COMPOSITION_PATH"
}

if [[ "$MODE" == "verify" ]]; then
  [[ -d "$CANDIDATE_DIR" && ! -L "$CANDIDATE_DIR" ]] || die "candidate directory does not exist"
  verify_composition >/dev/null
  echo "[unsigned-windows-preview-nightly] verified stage-only composition: $CANDIDATE_DIR"
  exit 0
fi

[[ -n "$PUBLISHED_AT" ]] || die "CHUMMER_UNSIGNED_WINDOWS_PREVIEW_PUBLISHED_AT is required for prepare"
[[ ! -e "$CANDIDATE_DIR" && ! -L "$CANDIDATE_DIR" ]] || die "candidate directory must be absent"
if [[ -n "$("$TRUSTED_GIT" -C "$REPO_ROOT" status --porcelain --untracked-files=normal)" ]]; then
  die "release-authoritative prepare requires a clean source checkout"
fi

candidate_parent="$(dirname "$CANDIDATE_DIR")"
mkdir -p "$candidate_parent"
"$TRUSTED_PYTHON" -I - "$candidate_parent" <<'PY'
import os
import stat
import sys
from pathlib import Path

path = Path(sys.argv[1])
metadata = path.lstat()
if (
    path.is_symlink()
    or not stat.S_ISDIR(metadata.st_mode)
    or metadata.st_uid != os.geteuid()
    or metadata.st_mode & (stat.S_IWGRP | stat.S_IWOTH)
):
    raise SystemExit("candidate parent must be one physical owner-controlled directory")
PY
candidate_staging="$(mktemp -d "$candidate_parent/.unsigned-windows-preview.XXXXXXXX")"
work_root="$(mktemp -d "$candidate_parent/.unsigned-windows-preview-work.XXXXXXXX")"
cleanup() {
  local status=$?
  if [[ -n "${candidate_staging:-}" && -d "$candidate_staging" ]]; then
    rm -rf -- "$candidate_staging"
  fi
  if [[ -n "${work_root:-}" && -d "$work_root" ]]; then
    rm -rf -- "$work_root"
  fi
  exit "$status"
}
trap cleanup EXIT

mkdir -p \
  "$candidate_staging/provenance/config" \
  "$candidate_staging/provenance/retained-windows-publish-closure"
package_receipt="$work_root/package-plane-receipt.json"
retained_bundle="$work_root/retained-windows-bundle"
"$TRUSTED_PYTHON" -I "$PACKAGE_VERIFIER" \
  --repo-root "$REPO_ROOT" \
  --lock "$PACKAGE_LOCK" \
  --retain-windows-bundle-output "$retained_bundle" \
  --windows-release-version "$VERSION" \
  --windows-release-channel preview \
  --receipt-output "$package_receipt"

installer_build="$work_root/installer-build"
native_cache="$work_root/native-toolchain-cache"
isolated_home="$work_root/home"
isolated_tmp="$work_root/tmp"
isolated_docker_config="$work_root/docker-config"
mkdir -p "$installer_build" "$isolated_home" "$isolated_tmp" "$isolated_docker_config"

# Keep only transport configuration from the caller. It cannot change accepted
# bytes because every remotely acquired input is checked against a committed
# digest before use.
transport_environment=()
for variable_name in \
  ALL_PROXY HTTP_PROXY HTTPS_PROXY NO_PROXY SSL_CERT_DIR SSL_CERT_FILE \
  all_proxy http_proxy https_proxy no_proxy; do
  if [[ -v "$variable_name" && -n "${!variable_name}" ]]; then
    transport_environment+=("$variable_name=${!variable_name}")
  fi
done

"$TRUSTED_ENV" -i \
  "${transport_environment[@]}" \
  PATH="$TRUSTED_PATH" \
  HOME="$isolated_home" \
  XDG_CACHE_HOME="$isolated_home/.cache" \
  XDG_CONFIG_HOME="$isolated_home/.config" \
  XDG_DATA_HOME="$isolated_home/.local/share" \
  DOCKER_CONFIG="$isolated_docker_config" \
  TMP="$isolated_tmp" \
  TEMP="$isolated_tmp" \
  TMPDIR="$isolated_tmp" \
  LANG=C.UTF-8 \
  LC_ALL=C.UTF-8 \
  TZ=UTC \
  PYTHONNOUSERSITE=1 \
  PYTHONSAFEPATH=1 \
  SOURCE_DATE_EPOCH="$CANONICAL_SOURCE_DATE_EPOCH" \
  CHUMMER_UI_REPO_ROOT_ALIAS="$REPO_ROOT" \
  CHUMMER_PYTHON_BIN="$TRUSTED_PYTHON" \
  CHUMMER_RELEASE_CHANNEL=preview \
  CHUMMER_DESKTOP_RELEASE_CHANNEL=preview \
  CHUMMER_ALLOW_LOCAL_RELEASE_VERSION=0 \
  CHUMMER_RELEASE_INCLUDE_PDBS=0 \
  CHUMMER_WINDOWS_SIGNING_REQUIRED=0 \
  CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED=0 \
  CHUMMER_WINDOWS_BUILD_PROVENANCE_REQUIRED=0 \
  CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE=0 \
  CHUMMER_RELEASE_MANIFEST_STAGE_ONLY=1 \
  CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS=1 \
  CHUMMER_WINDOWS_SECONDARY_HEAD_KEY= \
  CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR= \
  CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET= \
  CHUMMER_WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT= \
  CHUMMER_RELEASE_SAMPLE_SOURCE="$SAMPLE_SOURCE" \
  CHUMMER_LEGACY_FIXTURE_ROOT= \
  CHUMMER_WINDOWS_INSTALLER_MODE=bootstrap \
  CHUMMER_PUBLIC_DOWNLOADS_PREFIX="$PUBLIC_DOWNLOADS_PREFIX" \
  CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL="$BOOTSTRAP_PAYLOAD_URL" \
  CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE=download \
  CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_LOCK="$NATIVE_LOCK" \
  CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_CACHE_DIR="$native_cache" \
  CHUMMER_WINDOWS_7ZIP_EXTRA_URL="$SEVENZIP_EXTRA_URL" \
  CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256="$SEVENZIP_EXTRA_SHA256" \
  CHUMMER_WINDOWS_CURL_URL="$CURL_WINDOWS_URL" \
  CHUMMER_WINDOWS_CURL_SHA256="$CURL_WINDOWS_SHA256" \
  "$TRUSTED_BASH" "$SCRIPT_DIR/build-desktop-installer.sh" \
    "$retained_bundle/assets" \
    avalonia \
    win-x64 \
    Chummer.Avalonia.exe \
    "$installer_build" \
    "$VERSION"

install -m 0444 "$PACKAGE_LOCK" "$candidate_staging/provenance/config/package-plane.lock.json"
install -m 0444 "$package_receipt" "$candidate_staging/provenance/UI_FRESH_PACKAGE_PLANE.generated.json"
install -m 0444 "$retained_bundle/manifest.json" "$candidate_staging/provenance/retained-windows-publish-closure/manifest.json"
install -m 0444 "$NATIVE_LOCK" "$candidate_staging/provenance/config/windows-native-bootstrap-toolchain.lock.json"

"$TRUSTED_PYTHON" -I "$MATERIALIZER" materialize \
  --incumbent-root "$INCUMBENT_ROOT" \
  --build-root "$installer_build" \
  --expected-version "$VERSION" \
  --published-at "$PUBLISHED_AT" \
  --source-sha "$SOURCE_SHA" \
  --output "$candidate_staging/publication" >/dev/null

"$TRUSTED_PYTHON" -I "$COMPOSITION_HELPER" prepare \
  --publication-root "$candidate_staging/publication" \
  --incumbent-root "$INCUMBENT_ROOT" \
  --expected-version "$VERSION" \
  --source-sha "$SOURCE_SHA" \
  --package-plane-lock "$candidate_staging/provenance/config/package-plane.lock.json" \
  --package-plane-receipt "$candidate_staging/provenance/UI_FRESH_PACKAGE_PLANE.generated.json" \
  --retained-manifest "$candidate_staging/provenance/retained-windows-publish-closure/manifest.json" \
  --native-toolchain-lock "$candidate_staging/provenance/config/windows-native-bootstrap-toolchain.lock.json" \
  --output "$candidate_staging/$COMPOSITION_NAME" >/dev/null

# Move the whole prepared tree into place only after the replay gate passes.
"$TRUSTED_PYTHON" -I "$COMPOSITION_HELPER" verify \
  --publication-root "$candidate_staging/publication" \
  --incumbent-root "$INCUMBENT_ROOT" \
  --expected-version "$VERSION" \
  --source-sha "$SOURCE_SHA" \
  --package-plane-lock "$candidate_staging/provenance/config/package-plane.lock.json" \
  --package-plane-receipt "$candidate_staging/provenance/UI_FRESH_PACKAGE_PLANE.generated.json" \
  --retained-manifest "$candidate_staging/provenance/retained-windows-publish-closure/manifest.json" \
  --native-toolchain-lock "$candidate_staging/provenance/config/windows-native-bootstrap-toolchain.lock.json" \
  --request "$candidate_staging/$COMPOSITION_NAME" >/dev/null

"$TRUSTED_PYTHON" -I "$MATERIALIZER" publish-directory \
  --source "$candidate_staging" \
  --output "$CANDIDATE_DIR" >/dev/null
candidate_staging=""
echo "[unsigned-windows-preview-nightly] prepared unsigned Windows-only composition: $CANDIDATE_DIR"
