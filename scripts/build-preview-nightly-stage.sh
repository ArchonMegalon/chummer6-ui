#!/usr/bin/env bash
set -euo pipefail

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

  CHUMMER_WINDOWS_INSTALLER_MODE=bootstrap \
  CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE=download \
    bash "$SCRIPT_DIR/build-desktop-installer.sh" \
      "$publish_dir" "$app_key" "$rid" "$launch_target" "$CANDIDATE_DIR" "$VERSION"
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
