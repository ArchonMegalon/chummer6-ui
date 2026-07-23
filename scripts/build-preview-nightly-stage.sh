#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0 || {
  printf '%s\n' "[preview-nightly-stage] could not disable core dumps" >&2
  exit 2
}

TRUSTED_BASH_PATH="/bin/bash"
if [[ "${1:-}" == "prepare" ]]; then
  [[ "${BASH:-}" == "$TRUSTED_BASH_PATH" && -f "$TRUSTED_BASH_PATH" && ! -L "$TRUSTED_BASH_PATH" && -x "$TRUSTED_BASH_PATH" ]] || {
    printf '%s\n' "[preview-nightly-stage] trusted Bash interpreter is unavailable" >&2
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
SIGNING_HANDOFF_FORWARDED=0
export -n \
  SIGNING_SM_HOST \
  SIGNING_SM_API_KEY \
  SIGNING_SM_CLIENT_CERT_FILE \
  SIGNING_SM_CLIENT_CERT_PASSWORD

early_signing_die() {
  printf '%s\n' "[preview-nightly-stage] signing handoff is invalid" >&2
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
  local mode="${1-}"
  local signing_handoff_fd="${CHUMMER_WINDOWS_SIGNING_HANDOFF_FD:-}"
  unset CHUMMER_WINDOWS_SIGNING_HANDOFF_FD BASH_ENV ENV

  if [[ "$mode" != "prepare" ]]; then
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

consume_signing_handoff_before_external_commands "${1:-}"

validate_public_signing_environment_before_external_commands() {
  local mode="${1-}"
  local name=""
  for name in ${!SM_@}; do
    early_signing_die
  done
  for name in ${!CHUMMER_WINDOWS_@}; do
    case "$name" in
      CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH|\
      CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS|\
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256|\
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256|\
      CHUMMER_WINDOWS_SIGNING_BACKEND|\
      CHUMMER_WINDOWS_TIMESTAMP_URL)
        [[ "$mode" == "prepare" ]] || early_signing_die
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
        [[ "$mode" == "prepare" ]] || early_signing_die
        ;;
      *)
        early_signing_die
        ;;
    esac
  done
  if [[ "$mode" == "prepare" ]]; then
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

validate_public_signing_environment_before_external_commands "${1:-}"

# Canonical stage-only preview nightly lane.
#
# prepare: build/package/smoke Windows + Linux, preserve the incumbent shelf,
#          materialize manifests/evidence, and stop before native proof.
# seal:    consume exact native-Windows evidence, run all final gates, seal the
#          bundle, and atomically expose nightly-run-<version> for uploader dry-run.
# verify:  verify a previously sealed bundle without touching external state.

umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
CONTRACT_HELPER="$SCRIPT_DIR/preview_nightly_stage_contract.py"
SUPPLY_CHAIN_HELPER="$SCRIPT_DIR/preview_supply_chain.py"
PUBLICATION_SCOPE_HELPER="$SCRIPT_DIR/preview_nightly_publication_scope.py"
OSV_SCANNER_VERSION="2.3.8"
OSV_SCANNER_SHA256="bc98e15319ed0d515e3f9235287ba53cdc5535d576d24fd573978ecfe9ab92dc"
OSV_SCANNER_URL="https://github.com/google/osv-scanner/releases/download/v${OSV_SCANNER_VERSION}/osv-scanner_linux_amd64"
MODE="${1:-}"

usage() {
  cat >&2 <<'EOF'
usage: scripts/build-preview-nightly-stage.sh <prepare|seal|verify>

This command is intentionally stage-only. It never uploads, deploys, reads an
upload ticket, or contacts a release endpoint. See docs/PREVIEW_NIGHTLY_STAGE.md
for the complete exact-authority environment contract. Seal and verify replay
candidate-producer, native-capture, and finalization provenance through the
unauthenticated public GitHub Actions API.
EOF
}

die() {
  echo "[preview-nightly-stage] $*" >&2
  exit 2
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "required command is unavailable: $1"
}

case "$MODE" in
  prepare|seal|verify)
    ;;
  *)
    usage
    exit 2
    ;;
esac

[[ -x "$CONTRACT_HELPER" || -f "$CONTRACT_HELPER" ]] || die "missing stage contract helper"
[[ -x "$SUPPLY_CHAIN_HELPER" || -f "$SUPPLY_CHAIN_HELPER" ]] || die "missing supply-chain helper"
[[ -x "$PUBLICATION_SCOPE_HELPER" || -f "$PUBLICATION_SCOPE_HELPER" ]] || die "missing publication-scope helper"

# Force every child into the non-publication posture. These are policy values,
# not caller-overridable release switches.
export CHUMMER_RELEASE_MANIFEST_STAGE_ONLY=1
export CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS=1
export CHUMMER_ALLOW_REMOTE_RELEASE_PROOF_INPUTS=0
export CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS=0
export CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS=0
export CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION=1
export CHUMMER_RELEASE_REQUIRE_STARTUP_SMOKE_PROOF=1
export CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE=1
export CHUMMER_DESKTOP_RELEASE_CHANNEL=preview
# Windows-only publication is never an unsigned-preview exception.  The
# installer and its payload binding must be complete before candidate export
# or native execution can begin.
export CHUMMER_WINDOWS_SIGNING_REQUIRED=1
export CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED=1
unset CHUMMER_FORCE_NIGHTLY_PUBLISH
unset CHUMMER_ALLOW_PROOF_ONLY_WINDOWS_VISUAL_HANDOFF
unset CHUMMER_ALLOW_SKIPPED_WINDOWS_STARTUP_SMOKE
unset \
  CHUMMER_PUBLISHED_FEED_ROOT \
  CHUMMER_PUBLISHED_FEED_SHA256 \
  CHUMMER_PUBLISHED_FEED_SOURCES \
  CHUMMER_PUBLISHED_NUGET_CONFIG \
  CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256

CANDIDATE_DIR="${CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR:-}"
STAGE_DIR="${CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR:-}"
VERSION="${CHUMMER_PREVIEW_NIGHTLY_VERSION:-}"
PUBLISHED_AT="${CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT:-}"
PACKAGE_PLANE_LOCK_FD=""

if [[ "$MODE" == "verify" ]]; then
  [[ -n "$STAGE_DIR" ]] || die "CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR is required"
  [[ -d "$STAGE_DIR" ]] || die "sealed stage does not exist: $STAGE_DIR"
  require_command python3
  unset GH_TOKEN GITHUB_TOKEN
  python3 "$CONTRACT_HELPER" verify --stage-dir "$STAGE_DIR" >/dev/null
  read -r VERSION SOURCE_COMMIT < <(
    python3 - "$STAGE_DIR/release-evidence/PREVIEW_SUPPLY_CHAIN_GATE.generated.json" <<'PY'
import json
import sys
from pathlib import Path

gate = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
print(gate["release"]["version"], gate["sourceCommit"])
PY
  )
  python3 "$SUPPLY_CHAIN_HELPER" verify \
    --stage-root "$STAGE_DIR" \
    --version "$VERSION" \
    --source-commit "$SOURCE_COMMIT" \
    --structural-only >/dev/null
  echo "[preview-nightly-stage] sealed stage structural replay passed (non-release-authoritative): $STAGE_DIR"
  exit 0
fi

[[ -n "$CANDIDATE_DIR" ]] || die "CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR is required"
[[ -n "$STAGE_DIR" ]] || die "CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR is required"
[[ -n "$VERSION" ]] || die "CHUMMER_PREVIEW_NIGHTLY_VERSION is required"
[[ -n "$PUBLISHED_AT" ]] || die "CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT is required"

require_command git
require_command python3

configure_exact_package_plane() {
  # Candidate production intentionally consumes the seven exact, clean source
  # authorities validated by prepare-inputs. This is a pinned local-tree slice,
  # never package-plane integration/release evidence or publication authority.
  local owner_contracts_package_version=""
  owner_contracts_package_version="$(
    python3 "$CHUMMER_CORE_ROOT/scripts/ai/bootstrap-owner-contracts-feed.py" \
      --repo-root "$CHUMMER_CORE_ROOT" \
      --print-version
  )" || {
    echo "could not resolve the pinned owner-contract package version" >&2
    return 2
  }
  if ! [[ "$owner_contracts_package_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "pinned owner-contract package version is invalid: $owner_contracts_package_version" >&2
    return 2
  fi
  export CHUMMER_VERIFY_MODE=slice
  export CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1
  export CHUMMER_ALLOW_STUB_PACKAGES=0
  # bootstrap-contracts-feed.sh replaces its provisional local package with
  # the exact four-package feed declared by Core's package-plane lock. Keep
  # every PackageReference override on that same immutable version.
  export CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"
  export CHUMMER_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"
  export CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"
  export CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"
  unset \
    CHUMMER_PUBLISHED_FEED_ROOT \
    CHUMMER_PUBLISHED_FEED_SHA256 \
    CHUMMER_PUBLISHED_FEED_SOURCES \
    CHUMMER_PUBLISHED_NUGET_CONFIG \
    CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256
  export CHUMMER_LOCAL_CONTRACTS_PROJECT="$CHUMMER_CORE_ROOT/Chummer.Contracts/Chummer.Contracts.csproj"
  export CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_SCRIPT="$CHUMMER_CORE_ROOT/scripts/ai/bootstrap-contracts-feed.sh"
  export CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED=1
  export CHUMMER_LOCAL_CAMPAIGN_CONTRACTS_PROJECT="$CHUMMER_RUN_ROOT/Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj"
  export CHUMMER_LOCAL_PLAY_CONTRACTS_PROJECT="$CHUMMER_RUN_ROOT/Chummer.Play.Contracts/Chummer.Play.Contracts.csproj"
  export CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT="$CHUMMER_RUN_ROOT/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj"
  export CHUMMER_LOCAL_HUB_REGISTRY_CONTRACTS_PROJECT="$CHUMMER_HUB_REGISTRY_ROOT/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj"
  export CHUMMER_LOCAL_UI_KIT_PROJECT="$CHUMMER_UI_KIT_ROOT/src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj"
  export CHUMMER_LOCAL_MEDIA_CONTRACTS_PROJECT="$CHUMMER_MEDIA_FACTORY_ROOT/src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj"
  export CHUMMER_ENGINE_CONTRACTS_FEED="$CANDIDATE_DIR/work/engine-contracts-feed"
  export CHUMMER_PACKAGE_PLANE_LOCK_ROOT="$REPO_ROOT/../.tmp/ai"
  export CHUMMER_PACKAGE_PLANE_LOCK_FILE="$CHUMMER_PACKAGE_PLANE_LOCK_ROOT/with-package-plane.lock"
  export NUGET_PACKAGES="$CANDIDATE_DIR/work/nuget-packages"
  export DOTNET_CLI_HOME="$CANDIDATE_DIR/work/dotnet-home"
  mkdir -p \
    "$CHUMMER_ENGINE_CONTRACTS_FEED" \
    "$CHUMMER_PACKAGE_PLANE_LOCK_ROOT" \
    "$NUGET_PACKAGES" \
    "$DOTNET_CLI_HOME"
}

acquire_package_plane_lock() {
  require_command flock
  mkdir -p "$CHUMMER_PACKAGE_PLANE_LOCK_ROOT"
  exec {PACKAGE_PLANE_LOCK_FD}>"$CHUMMER_PACKAGE_PLANE_LOCK_FILE"
  flock -w 60 "$PACKAGE_PLANE_LOCK_FD" || die "timed out waiting for the shared package-plane authority lock"
  export CHUMMER_PACKAGE_PLANE_LOCK_HELD=1
}

invalidate_reference_assembly_caches() {
  # with-package-plane skips owner rebuilds when these ignored outputs exist.
  # Remove only the exact generated reference assemblies after authority
  # validation so every subsequent publish consumes this run's pinned commits.
  rm -f -- \
    "$CHUMMER_CORE_ROOT/Chummer.Contracts/obj/Release/net10.0/ref/Chummer.Engine.Contracts.dll" \
    "$CHUMMER_HUB_REGISTRY_ROOT/Chummer.Hub.Registry.Contracts/obj/Release/net10.0/ref/Chummer.Hub.Registry.Contracts.dll" \
    "$CHUMMER_RUN_ROOT/Chummer.Play.Contracts/obj/Release/net10.0/ref/Chummer.Play.Contracts.dll" \
    "$CHUMMER_RUN_ROOT/Chummer.Campaign.Contracts/obj/Release/net10.0/ref/Chummer.Campaign.Contracts.dll" \
    "$CHUMMER_RUN_ROOT/Chummer.Run.Contracts/obj/Release/net10.0/ref/Chummer.Run.Contracts.dll" \
    "$CHUMMER_UI_KIT_ROOT/src/Chummer.Ui.Kit/obj/Release/net10.0/ref/Chummer.Ui.Kit.dll" \
    "$CHUMMER_MEDIA_FACTORY_ROOT/src/Chummer.Media.Contracts/obj/Release/net10.0/ref/Chummer.Media.Contracts.dll" \
    "$REPO_ROOT/Chummer.Presentation/obj/Release/net10.0/ref/Chummer.Presentation.dll" \
    "$REPO_ROOT/Chummer.Desktop.Runtime/obj/Release/net10.0/ref/Chummer.Desktop.Runtime.dll"
}

configure_staged_proof_inputs() {
  CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/HUB_LOCAL_RELEASE_PROOF.generated.json"
  CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH="$CANDIDATE_DIR/proof/inputs/UI_LOCALIZATION_RELEASE_GATE.generated.json"
  CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/UI_LOCAL_RELEASE_PROOF.generated.json"
  CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"
  CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
  CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH="$CANDIDATE_DIR/proof/inputs/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
  CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH="$CANDIDATE_DIR/proof/inputs/UI_FLAGSHIP_RELEASE_GATE.generated.json"
  CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH="$CANDIDATE_DIR/proof/inputs/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
  CHUMMER_UI_WORKFLOW_PARITY_PATH="$CANDIDATE_DIR/proof/inputs/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
  CHUMMER_SR4_WORKFLOW_PARITY_PATH="$CANDIDATE_DIR/proof/inputs/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
  CHUMMER_SR6_WORKFLOW_PARITY_PATH="$CANDIDATE_DIR/proof/inputs/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
  export CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH
  export CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH
  export CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH
  export CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH
  export CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH
  export CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH
  export CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH
  export CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH
  export CHUMMER_UI_WORKFLOW_PARITY_PATH
  export CHUMMER_SR4_WORKFLOW_PARITY_PATH
  export CHUMMER_SR6_WORKFLOW_PARITY_PATH
}

publish_project() {
  local app_key="$1"
  local project="$2"
  local rid="$3"
  local launch_target="$4"
  local publish_dir="$CANDIDATE_DIR/work/publish/$app_key/$rid"

  mkdir -p "$publish_dir"
  bash "$SCRIPT_DIR/ai/with-package-plane.sh" publish "$project" \
    -c Release \
    -f net10.0 \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:ChummerDesktopReleaseVersion="$VERSION" \
    -p:ChummerDesktopReleaseChannel=preview \
    -o "$publish_dir"

  if [[ "$rid" == win-* ]]; then
    [[ "$SIGNING_HANDOFF_CAPTURED" == "1" && "$SIGNING_HANDOFF_FORWARDED" == "0" ]] || early_signing_die
    local handoff_path=""
    local handoff_source_fd=""
    local handoff_writer_pid=""
    local handoff_writer_status=0
    local installer_status=0
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
    CHUMMER_WINDOWS_SIGNING_HANDOFF_FD=3 \
    CHUMMER_WINDOWS_INSTALLER_MODE=bootstrap \
    CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE=download \
      "$TRUSTED_BASH_PATH" --noprofile --norc "$SCRIPT_DIR/build-desktop-installer.sh" \
        "$publish_dir" "$app_key" "$rid" "$launch_target" "$CANDIDATE_DIR" "$VERSION" \
        3<&3 {PACKAGE_PLANE_LOCK_FD}>&- || installer_status=$?
    exec 3<&-
    wait "$handoff_writer_pid" || handoff_writer_status=$?
    SIGNING_SM_HOST=""
    SIGNING_SM_API_KEY=""
    SIGNING_SM_CLIENT_CERT_FILE=""
    SIGNING_SM_CLIENT_CERT_PASSWORD=""
    SIGNING_HANDOFF_CAPTURED=0
    SIGNING_HANDOFF_FORWARDED=1
    unset \
      CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH \
      CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS \
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256 \
      CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256 \
      CHUMMER_WINDOWS_SIGNING_BACKEND \
      CHUMMER_WINDOWS_TIMESTAMP_URL \
      CHUMMER_KEYLOCKER_JAVA_HOME \
      CHUMMER_KEYLOCKER_JAVA_BIN \
      CHUMMER_KEYLOCKER_JAVA_BIN_SHA256 \
      CHUMMER_KEYLOCKER_JAVA_TREE_SHA256 \
      CHUMMER_KEYLOCKER_JSIGN_JAR \
      CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256 \
      CHUMMER_KEYLOCKER_DOTNET_ROOT \
      CHUMMER_KEYLOCKER_DOTNET_BIN \
      CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256 \
      CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256 \
      CHUMMER_KEYLOCKER_SIGNER_DLL \
      CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256 \
      CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256 \
      CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256 \
      CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256 \
      CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256
    (( handoff_writer_status == 0 )) || return "$handoff_writer_status"
    (( installer_status == 0 )) || return "$installer_status"
  else
    CHUMMER_WINDOWS_INSTALLER_MODE=bootstrap \
    CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE=download \
      "$TRUSTED_BASH_PATH" --noprofile --norc "$SCRIPT_DIR/build-desktop-installer.sh" \
        "$publish_dir" "$app_key" "$rid" "$launch_target" "$CANDIDATE_DIR" "$VERSION"
  fi
}

install_pinned_osv_scanner() {
  local configured="${CHUMMER_OSV_SCANNER_PATH:-}"
  local tools_dir="$CANDIDATE_DIR/work/tools"
  local download="$tools_dir/osv-scanner.download"
  OSV_SCANNER_PATH="$tools_dir/osv-scanner"
  mkdir -m 0700 -p "$tools_dir"
  [[ ! -e "$download" && ! -e "$OSV_SCANNER_PATH" ]] || die "OSV scanner tool path is not fresh"
  if [[ -n "$configured" ]]; then
    [[ "$configured" == /* ]] || die "CHUMMER_OSV_SCANNER_PATH must be absolute"
    [[ -f "$configured" && ! -L "$configured" ]] || die "configured OSV scanner must be a regular non-symlink file"
    cp --no-preserve=mode,ownership,timestamps -- "$configured" "$download"
  else
    require_command curl
    [[ "$(uname -s)" == "Linux" && "$(uname -m)" == "x86_64" ]] || \
      die "automatic pinned OSV scanner acquisition supports only Linux x86_64"
    curl --fail --location --proto '=https' --tlsv1.2 \
      --output "$download" \
      "$OSV_SCANNER_URL"
  fi

  require_command sha256sum
  printf '%s  %s\n' "$OSV_SCANNER_SHA256" "$download" | sha256sum --check --strict -
  chmod 0700 "$download"
  mv -T "$download" "$OSV_SCANNER_PATH"
  export OSV_SCANNER_PATH
}

generate_supply_chain_evidence() {
  local rid="$1"
  shift
  python3 "$SUPPLY_CHAIN_HELPER" generate \
    --stage-root "$CANDIDATE_DIR" \
    --project-assets "$REPO_ROOT/Chummer.Avalonia/obj/project.assets.json" \
    --scanner "$OSV_SCANNER_PATH" \
    --rid "$rid" \
    --version "$VERSION" \
    --source-commit "$CHUMMER_UI_EXPECTED_COMMIT" \
    "$@"
}

smoke_artifact() {
  local app_key="$1"
  local rid="$2"
  local launch_target="$3"
  local artifact_path="$4"
  if [[ "$rid" == win-* ]]; then
    CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download \
      bash "$SCRIPT_DIR/run-desktop-startup-smoke.sh" \
        "$artifact_path" "$app_key" "$rid" "$launch_target" "$CANDIDATE_DIR/startup-smoke" "$VERSION"
  else
    bash "$SCRIPT_DIR/run-desktop-startup-smoke.sh" \
      "$artifact_path" "$app_key" "$rid" "$launch_target" "$CANDIDATE_DIR/startup-smoke" "$VERSION"
  fi
}

prepare_stage() (
  require_command dotnet
  require_command docker
  require_command dpkg-deb
  [[ ! -e "$CANDIDATE_DIR" ]] || die "candidate path already exists: $CANDIDATE_DIR"
  [[ ! -e "$STAGE_DIR" ]] || die "sealed stage path already exists: $STAGE_DIR"
  mkdir -p "$(dirname "$CANDIDATE_DIR")"
  mkdir -m 0700 "$CANDIDATE_DIR"
  local prepared=0
  local candidate_device=""
  local candidate_inode=""
  local candidate_cleanup_quarantine="$CANDIDATE_DIR.cleanup.$$"
  read -r candidate_device candidate_inode < <(
    python3 "$CONTRACT_HELPER" directory-identity --root "$CANDIDATE_DIR" |
      python3 -c 'import json,sys; value=json.load(sys.stdin); print(value["device"], value["inode"])'
  )
  [[ -n "$candidate_device" && -n "$candidate_inode" ]] || die "could not record candidate ownership identity"
  cleanup_incomplete_candidate() {
    if [[ "$prepared" != "1" ]]; then
      python3 "$CONTRACT_HELPER" consume-owned-dir \
        --source "$CANDIDATE_DIR" \
        --quarantine "$candidate_cleanup_quarantine" \
        --expected-device "$candidate_device" \
        --expected-inode "$candidate_inode" >/dev/null 2>&1 || true
    fi
  }
  trap cleanup_incomplete_candidate EXIT

  python3 "$CONTRACT_HELPER" prepare-inputs \
    --presentation-root "$REPO_ROOT" \
    --candidate-dir "$CANDIDATE_DIR" >/dev/null
  configure_exact_package_plane
  acquire_package_plane_lock
  invalidate_reference_assembly_caches
  configure_staged_proof_inputs
  install_pinned_osv_scanner

  publish_project avalonia "$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj" win-x64 Chummer.Avalonia.exe
  generate_supply_chain_evidence win-x64 \
    --artifact "files/chummer-avalonia-win-x64-installer.exe=$CANDIDATE_DIR/files/chummer-avalonia-win-x64-installer.exe" \
    --artifact "files/chummer-avalonia-win-x64-payload.zip=$CANDIDATE_DIR/files/chummer-avalonia-win-x64-payload.zip"
  publish_project avalonia "$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj" linux-x64 Chummer.Avalonia
  generate_supply_chain_evidence linux-x64 \
    --artifact "files/chummer-avalonia-linux-x64-installer.deb=$CANDIDATE_DIR/files/chummer-avalonia-linux-x64-installer.deb"

  smoke_artifact avalonia win-x64 Chummer.Avalonia.exe "$CANDIDATE_DIR/files/chummer-avalonia-win-x64-installer.exe"
  smoke_artifact avalonia linux-x64 Chummer.Avalonia "$CANDIDATE_DIR/files/chummer-avalonia-linux-x64-installer.deb"

  mkdir -p "$CANDIDATE_DIR/release-evidence"
  DOWNLOADS_DIR="$CANDIDATE_DIR/files" \
  MANIFEST_PATH="$CANDIDATE_DIR/releases.json" \
  CANONICAL_MANIFEST_PATH="$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
  CANONICAL_FILES_DIR="$CANDIDATE_DIR/files" \
  PORTAL_MANIFEST_PATH="$CANDIDATE_DIR/releases.json" \
  PORTAL_CANONICAL_MANIFEST_PATH="$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
  PORTAL_DOWNLOADS_DIR="$CANDIDATE_DIR" \
  STARTUP_SMOKE_DIR="$CANDIDATE_DIR/startup-smoke" \
  SIGNING_RECEIPTS_DIR="$CANDIDATE_DIR/signing" \
  PROMOTION_EVIDENCE_PATH="$CANDIDATE_DIR/release-evidence/public-promotion.json" \
  QUARANTINE_PROMOTION_EVIDENCE_PATH="$CANDIDATE_DIR/QUARANTINED_INSTALLER_PROMOTION.generated.json" \
  CHUMMER_UI_EXTERNAL_HOST_PROOF_BLOCKERS_PATH="$CANDIDATE_DIR/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json" \
  CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json" \
  SOURCE_MANIFEST_PATH="$CANDIDATE_DIR/retained-source/RELEASE_CHANNEL.generated.json" \
  RELEASE_PROOF_PATH="$CANDIDATE_DIR/proof/inputs/HUB_LOCAL_RELEASE_PROOF.generated.json" \
  RELEASE_VERSION="$VERSION" \
  RELEASE_CHANNEL=preview \
  RELEASE_PUBLISHED_AT="$PUBLISHED_AT" \
  REGISTRY_CANONICAL_MANIFEST_PATH="$CANDIDATE_DIR/retained-source/RELEASE_CHANNEL.generated.json" \
  REGISTRY_RELEASES_MANIFEST_PATH="$CANDIDATE_DIR/retained-source/releases.json" \
  REGISTRY_FILES_DIR="$CANDIDATE_DIR/files" \
    bash "$SCRIPT_DIR/generate-releases-manifest.sh"

  python3 "$PUBLICATION_SCOPE_HELPER" prepare \
    --build-manifest "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --build-releases "$CANDIDATE_DIR/releases.json" \
    --build-files-dir "$CANDIDATE_DIR/files" \
    --incumbent-manifest "$CANDIDATE_DIR/retained-source/RELEASE_CHANNEL.generated.json" \
    --incumbent-releases "$CANDIDATE_DIR/retained-source/releases.json" \
    --incumbent-files-dir "$CANDIDATE_DIR/retained-source/files" \
    --incumbent-shelf-dir "$CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT" \
    --incumbent-snapshot-dir "$CANDIDATE_DIR/retained-full-source" \
    --signing-receipt "$CANDIDATE_DIR/signing/signing-avalonia-win-x64.receipt.json" \
    --consumer-commit "$CHUMMER_UI_EXPECTED_COMMIT" \
    --desktop-commit "$CHUMMER_DESKTOP_EXPECTED_COMMIT" \
    --registry-root "$CHUMMER_HUB_REGISTRY_ROOT" \
    --registry-prepare-root "$CANDIDATE_DIR/registry-prepare" \
    --publication-dir "$CANDIDATE_DIR/publication" \
    --output "$CANDIDATE_DIR/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" >/dev/null

  bash "$SCRIPT_DIR/verify-releases-manifest.sh" \
    --require-complete-desktop-coverage \
    --skip-startup-smoke-filter \
    "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json"
  python3 "$SCRIPT_DIR/verify-release-stage-artifact-scope.py" \
    --manifest "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --manifest "$CANDIDATE_DIR/releases.json" \
    --files-dir "$CANDIDATE_DIR/files" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke"
  python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" \
    --files-dir "$CANDIDATE_DIR/files" \
    --manifest "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --require-embedded-bootstrap-metadata \
    --require-manifest-row
  python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$CANDIDATE_DIR/releases.json" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke" \
    --files-dir "$CANDIDATE_DIR/files" \
    --output "$CANDIDATE_DIR/WINDOWS_BOOTSTRAP_COMPATIBILITY_SMOKE.generated.json"

  python3 "$SUPPLY_CHAIN_HELPER" finalize \
    --stage-root "$CANDIDATE_DIR" \
    --version "$VERSION" \
    --source-commit "$CHUMMER_UI_EXPECTED_COMMIT" \
    --scanner "$OSV_SCANNER_PATH"
  python3 "$SUPPLY_CHAIN_HELPER" verify \
    --stage-root "$CANDIDATE_DIR" \
    --version "$VERSION" \
    --source-commit "$CHUMMER_UI_EXPECTED_COMMIT" \
    --scanner "$OSV_SCANNER_PATH"

  python3 "$SCRIPT_DIR/materialize_release_candidate_handoff.py" "$CANDIDATE_DIR"
  python3 "$CONTRACT_HELPER" mark-candidate \
    --presentation-root "$REPO_ROOT" \
    --stage-dir "$CANDIDATE_DIR" >/dev/null

  rm -rf -- "$CANDIDATE_DIR/work"
  prepared=1
  trap - EXIT
  echo "[preview-nightly-stage] candidate prepared without publication: $CANDIDATE_DIR"
  echo "[preview-nightly-stage] native Windows evidence is required before seal"
)

seal_stage() {
  [[ -d "$CANDIDATE_DIR" ]] || die "prepared candidate does not exist: $CANDIDATE_DIR"
  [[ ! -e "$STAGE_DIR" ]] || die "sealed stage path already exists: $STAGE_DIR"
  local evidence_archive="${CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE:-}"
  [[ -n "$evidence_archive" ]] || die "CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE is required"
  unset GH_TOKEN GITHUB_TOKEN
  python3 "$CONTRACT_HELPER" validate-candidate \
    --presentation-root "$REPO_ROOT" \
    --stage-dir "$CANDIDATE_DIR" >/dev/null

  local original_candidate="$CANDIDATE_DIR"
  local sealing_work="$STAGE_DIR.sealing.$$"
  local cleanup_quarantine="$original_candidate.cleanup.$$"
  local seal_committed=0
  local candidate_tree_sha=""
  local candidate_tree_sha_after_copy=""
  local sealing_tree_sha=""
  local sealed_tree_sha=""
  local original_candidate_device=""
  local original_candidate_inode=""
  local sealing_work_device=""
  local sealing_work_inode=""
  [[ ! -e "$sealing_work" ]] || die "transactional seal work path already exists: $sealing_work"
  read -r original_candidate_device original_candidate_inode < <(
    python3 "$CONTRACT_HELPER" directory-identity --root "$original_candidate" |
      python3 -c 'import json,sys; value=json.load(sys.stdin); print(value["device"], value["inode"])'
  )
  [[ -n "$original_candidate_device" && -n "$original_candidate_inode" ]] || die "could not record candidate ownership identity"
  cleanup_failed_seal() {
    if [[ "$seal_committed" != "1" && -n "$sealing_work_device" && -n "$sealing_work_inode" ]]; then
      python3 "$CONTRACT_HELPER" consume-owned-dir \
        --source "$sealing_work" \
        --quarantine "$sealing_work.cleanup" \
        --expected-device "$sealing_work_device" \
        --expected-inode "$sealing_work_inode" >/dev/null 2>&1 || true
    fi
  }
  trap cleanup_failed_seal EXIT
  candidate_tree_sha="$(python3 "$CONTRACT_HELPER" digest-tree \
    --root "$original_candidate" \
    --expected-device "$original_candidate_device" \
    --expected-inode "$original_candidate_inode" | \
    python3 -c 'import json,sys; print(json.load(sys.stdin)["treeSha256"])')"
  mkdir -m 0700 "$sealing_work"
  read -r sealing_work_device sealing_work_inode < <(
    python3 "$CONTRACT_HELPER" directory-identity --root "$sealing_work" |
      python3 -c 'import json,sys; value=json.load(sys.stdin); print(value["device"], value["inode"])'
  )
  cp -a -- "$original_candidate/." "$sealing_work/"
  candidate_tree_sha_after_copy="$(python3 "$CONTRACT_HELPER" digest-tree \
    --root "$original_candidate" \
    --expected-device "$original_candidate_device" \
    --expected-inode "$original_candidate_inode" | \
    python3 -c 'import json,sys; print(json.load(sys.stdin)["treeSha256"])')"
  [[ "$candidate_tree_sha" == "$candidate_tree_sha_after_copy" ]] || die "candidate changed while creating transactional seal copy"
  sealing_tree_sha="$(python3 "$CONTRACT_HELPER" digest-tree \
    --root "$sealing_work" \
    --expected-device "$sealing_work_device" \
    --expected-inode "$sealing_work_inode" | \
    python3 -c 'import json,sys; print(json.load(sys.stdin)["treeSha256"])')"
  [[ "$candidate_tree_sha" == "$sealing_tree_sha" ]] || die "candidate changed while creating transactional seal copy"
  CANDIDATE_DIR="$sealing_work"
  configure_staged_proof_inputs

  python3 "$CONTRACT_HELPER" stage-native-evidence \
    --stage-dir "$CANDIDATE_DIR" \
    --evidence-archive "$evidence_archive" >/dev/null

  local candidate_producer_actor=""
  local capture_actor=""
  read -r candidate_producer_actor capture_actor < <(
    python3 - "$CANDIDATE_DIR/NATIVE_WINDOWS_EVIDENCE.generated.json" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
candidate = payload.get("candidateProvenance", {}).get("candidate", {})
capture = payload.get("captureSource", {})
print(candidate.get("actor", ""), capture.get("actor", ""))
PY
  )
  [[ -n "$candidate_producer_actor" && -n "$capture_actor" ]] || \
    die "native evidence did not expose candidate/capture actors for approval independence"
  python3 "$PUBLICATION_SCOPE_HELPER" finalize \
    --proposal "$CANDIDATE_DIR/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" \
    --approval "$CANDIDATE_DIR/proof/windows-native/PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json" \
    --approval-receipt-path "proof/windows-native/PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json" \
    --native-evidence "$CANDIDATE_DIR/NATIVE_WINDOWS_EVIDENCE.generated.json" \
    --visual-approval "$CANDIDATE_DIR/WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json" \
    --disallowed-actor "$candidate_producer_actor" \
    --disallowed-actor "$capture_actor" \
    --output "$CANDIDATE_DIR/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" >/dev/null
  python3 "$PUBLICATION_SCOPE_HELPER" verify \
    --scope "$CANDIDATE_DIR/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    --proposal "$CANDIDATE_DIR/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" \
    --publication-dir "$CANDIDATE_DIR/publication" \
    --evidence-root "$CANDIDATE_DIR" >/dev/null

  local visual_reviewer_ids=""
  visual_reviewer_ids="$(python3 - "$CANDIDATE_DIR/NATIVE_WINDOWS_EVIDENCE.generated.json" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
reviewers = payload.get("visualReviewers")
if not isinstance(reviewers, dict):
    raise SystemExit(2)
values = {str(value).strip() for value in reviewers.values() if str(value).strip()}
if len(values) != 1:
    raise SystemExit(2)
print(next(iter(values)))
PY
)"
  [[ -n "$visual_reviewer_ids" ]] || die "authenticated upstream visual reviewer is missing"

  export CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json"
  export CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$CANDIDATE_DIR/files"
  export CHUMMER_WINDOWS_VISUAL_AUTHORIZED_REVIEWER_IDS="$visual_reviewer_ids"
  local head=""
  for head in avalonia; do
    export CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$CANDIDATE_DIR/WINDOWS_INSTALLER_VISUAL_PROOF-$head-win-x64.generated.json"
    export CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE-$head-win-x64.generated.json"
    export CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY="$head"
    export CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID=win-x64
    export CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH="$CANDIDATE_DIR/startup-smoke/startup-smoke-$head-win-x64.receipt.json"
    export CHUMMER_WINDOWS_STARTUP_SMOKE_PROGRESS_LOG_PATH="$CANDIDATE_DIR/startup-smoke/windows-installer-progress-$head-win-x64.log"
    bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh"
  done
  cp -- \
    "$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json" \
    "$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"

  python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$CANDIDATE_DIR/releases.json" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke" \
    --files-dir "$CANDIDATE_DIR/files" \
    --require-native-windows \
    --output "$CANDIDATE_DIR/WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json"
  python3 "$SCRIPT_DIR/generate-public-promotion-evidence.py" \
    --manifest "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke" \
    --signing-receipts-dir "$CANDIDATE_DIR/signing" \
    --output "$CANDIDATE_DIR/release-evidence/public-promotion.json" \
    --channel preview \
    --generated-at "$PUBLISHED_AT"
  python3 "$SCRIPT_DIR/verify-windows-release-evidence.py" \
    --release-channel "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$CANDIDATE_DIR/releases.json" \
    --files-dir "$CANDIDATE_DIR/files" \
    --signing-dir "$CANDIDATE_DIR/signing" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke" \
    --windows-exit-gate "$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json" \
    --require-native-windows \
    --output "$CANDIDATE_DIR/WINDOWS_RELEASE_EVIDENCE.generated.json"
  python3 "$CHUMMER_RUN_ROOT/scripts/verify_release_shelf_replacement.py" \
    --existing "$CANDIDATE_DIR/retained-source/RELEASE_CHANNEL.generated.json" \
    --incoming "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --selected-files-dir "$CANDIDATE_DIR/files" \
    --exact-incoming-tuple avalonia:windows:win-x64 \
    --exact-incoming-tuple avalonia:linux:linux-x64
  python3 "$SCRIPT_DIR/verify-release-stage-artifact-scope.py" \
    --manifest "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json" \
    --manifest "$CANDIDATE_DIR/releases.json" \
    --files-dir "$CANDIDATE_DIR/files" \
    --startup-smoke-dir "$CANDIDATE_DIR/startup-smoke"
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" \
    --require-complete-desktop-coverage \
    --skip-startup-smoke-filter \
    "$CANDIDATE_DIR/RELEASE_CHANNEL.generated.json"

  python3 "$SCRIPT_DIR/materialize_release_candidate_handoff.py" "$CANDIDATE_DIR"
  python3 "$CONTRACT_HELPER" seal \
    --presentation-root "$REPO_ROOT" \
    --stage-dir "$CANDIDATE_DIR" >/dev/null
  python3 "$CONTRACT_HELPER" verify --stage-dir "$CANDIDATE_DIR" >/dev/null
  sealed_tree_sha="$(python3 "$CONTRACT_HELPER" digest-tree \
    --root "$CANDIDATE_DIR" \
    --expected-device "$sealing_work_device" \
    --expected-inode "$sealing_work_inode" | \
    python3 -c 'import json,sys; print(json.load(sys.stdin)["treeSha256"])')"
  python3 "$CONTRACT_HELPER" install-verified-sealed-dir-no-replace \
    --source "$CANDIDATE_DIR" \
    --destination "$STAGE_DIR" \
    --expected-device "$sealing_work_device" \
    --expected-inode "$sealing_work_inode" \
    --expected-tree-sha256 "$sealed_tree_sha" >/dev/null
  seal_committed=1
  trap - EXIT
  if ! python3 "$CONTRACT_HELPER" consume-owned-dir \
    --source "$original_candidate" \
    --quarantine "$cleanup_quarantine" \
    --expected-device "$original_candidate_device" \
    --expected-inode "$original_candidate_inode" >/dev/null; then
    echo "[preview-nightly-stage] sealed stage installed; original candidate cleanup was safely skipped" >&2
  fi
  echo "[preview-nightly-stage] sealed stage ready for uploader dry-run: $STAGE_DIR"
}

case "$MODE" in
  prepare)
    prepare_stage
    ;;
  seal)
    seal_stage
    ;;
esac
