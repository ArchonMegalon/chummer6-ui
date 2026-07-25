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
WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"

BUNDLE_DIR="${1:-$REPO_ROOT/dist}"
DEPLOY_DIR="${2:-$REPO_ROOT/Docker/Downloads}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-}"
DEPLOY_MODE="${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED:-false}"
LIVE_VERIFY_TARGET="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}"
REQUIRE_EXTERNAL_PUBLISH="${CHUMMER_DOWNLOADS_REQUIRE_EXTERNAL_PUBLISH:-false}"
MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
FILES_SOURCE="$BUNDLE_DIR/files"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"
STARTUP_SMOKE_SOURCE_CONFIGURED="${STARTUP_SMOKE_SOURCE:-}"
STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE:-$BUNDLE_DIR/startup-smoke}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-}"
SYNC_LIVE_DOWNLOADS_MIRRORS="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS:-auto}"
FORCE_NIGHTLY_PUBLISH="${CHUMMER_FORCE_NIGHTLY_PUBLISH:-0}"
SCOPE_TO_STAGE_ARTIFACTS="${CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS:-0}"
ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$WORKSPACE_ROOT/RELEASE_BLOCKERS.generated.json}"
PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS:-86400}"
ALLOW_BUNDLE_FILES_SOURCE_FALLBACK="${CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK:-0}"
BUILD_PROVENANCE_VALIDATOR="${CHUMMER_RELEASE_BUILD_PROVENANCE_VALIDATOR:-}"
BUILD_PROVENANCE_MANIFEST_SOURCE="$BUNDLE_DIR/RELEASE_CHANNEL.generated.json"
BUILD_PROVENANCE_REQUIRED=0
BUILD_PROVENANCE_STAGE_ROOT=""
BUILD_PROVENANCE_VALIDATOR_RESOLVED=""
RELEASE_CANDIDATE_STAGE_ONLY="${CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY:-0}"
RELEASE_CANDIDATE_OUTPUT_DIR="${CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR:-}"
WINDOWS_ONLY_PUBLICATION_STAGE_ROOT="${CHUMMER_WINDOWS_ONLY_PUBLICATION_STAGE_ROOT:-}"
WINDOWS_RUN_UPLOAD_RECEIPT_PATH="${CHUMMER_WINDOWS_RUN_UPLOAD_RECEIPT_PATH:-}"
WINDOWS_RUN_UPLOAD_RECEIPT_SHA256="${CHUMMER_WINDOWS_RUN_UPLOAD_RECEIPT_SHA256:-}"
WINDOWS_RUN_UPLOAD_API_ORIGIN="${CHUMMER_WINDOWS_RUN_UPLOAD_API_ORIGIN:-}"
WINDOWS_RUN_UPLOAD_SESSION_ID="${CHUMMER_WINDOWS_RUN_UPLOAD_SESSION_ID:-}"
WINDOWS_HUB_POSTDEPLOY_RECEIPT_PATH="${CHUMMER_WINDOWS_HUB_POSTDEPLOY_RECEIPT_PATH:-}"
WINDOWS_HUB_POSTDEPLOY_RECEIPT_SHA256="${CHUMMER_WINDOWS_HUB_POSTDEPLOY_RECEIPT_SHA256:-}"
WINDOWS_REGISTRY_ROOT="${CHUMMER_HUB_REGISTRY_ROOT:-}"
WINDOWS_ONLY_PUBLICATION_MODE=false
WINDOWS_ONLY_PUBLICATION_SNAPSHOT=""
WINDOWS_ONLY_TRANSACTION_ID=""
WINDOWS_ONLY_TRANSACTION_PREPARED=""
WINDOWS_ONLY_TRANSACTION_JOURNAL=""
WINDOWS_ONLY_TRANSACTION_COMMIT=""
WINDOWS_ONLY_TRANSACTION_ROLLBACK=""
WINDOWS_ONLY_TRANSACTION_PROOF_DIR=""
WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR=""
WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=false
windows_only_transaction_roots=()
windows_only_transaction_targets=()
windows_only_transaction_generations=()
windows_only_transaction_generation_receipts=()
windows_only_transaction_activation_receipts=()
windows_only_transaction_lock_dirs=()
windows_only_transaction_lock_fds=()
windows_only_transaction_incumbent_inventories=()
windows_only_transaction_prepared_inventories=()
windows_only_transaction_run_versions=()
windows_only_transaction_run_manifest_sha256s=()
windows_only_transaction_run_inventory_sha256s=()
windows_only_transaction_run_file_counts=()
windows_only_transaction_run_total_bytes=()
windows_only_transaction_activated=()
publication_receipt_output=""
publication_abort_output=""
publication_receipt_current=""
if [[ -n "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT" ]]; then
  WINDOWS_ONLY_PUBLICATION_MODE=true
  STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE_CONFIGURED:-$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/startup-smoke}"
  export CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"
fi

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] \
  && { to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" || [[ -n "$RELEASE_CANDIDATE_OUTPUT_DIR" ]]; }; then
  echo "Windows-only publication cannot be combined with the generic release-candidate stage-only lane." >&2
  exit 1
fi

assert_legacy_release_shelf_target() {
  local target_dir="$1"
  local layout_marker="$target_dir/.release-shelf-layout-v1"
  local active_pointer="$target_dir/current.json"
  local writer_policy="$target_dir/.release-shelf-writer-policy.json"

  if [[ -e "$writer_policy" || -L "$writer_policy" ]]; then
    echo "Refusing filesystem publication into $target_dir: server-journal-v1 owns this shelf." >&2
    echo "Use the staged HTTP upload API." >&2
    return 1
  fi
  if [[ -e "$layout_marker" || -L "$layout_marker" || -e "$active_pointer" || -L "$active_pointer" ]]; then
    echo "Refusing legacy fixed-path release publication into $target_dir: immutable release shelf layout v1 is active." >&2
    echo "Use the generation-aware publisher; this writer must not mutate paths behind current.json." >&2
    return 1
  fi
}

RELEASE_CANDIDATE_FS_HELPER="$SCRIPT_DIR/release_candidate_fs.py"

run_release_candidate_fs() {
  if [[ ! -f "$RELEASE_CANDIDATE_FS_HELPER" || -L "$RELEASE_CANDIDATE_FS_HELPER" ]]; then
    echo "Release candidate filesystem helper is unavailable: $RELEASE_CANDIDATE_FS_HELPER" >&2
    return 1
  fi
  python3 "$RELEASE_CANDIDATE_FS_HELPER" "$@"
}

reject_lexical_symlink_components() {
  run_release_candidate_fs reject-symlinks "$@"
}

resolve_release_candidate_output_dir() {
  run_release_candidate_fs resolve-output \
    "$1" \
    "$BUNDLE_DIR" \
    "$DEPLOY_DIR" \
    "$PORTAL_DOWNLOADS_DIR"
}

rewrite_release_candidate_stage_paths() {
  run_release_candidate_fs rewrite-stage-paths "$1" "$2"
}

atomically_publish_release_candidate_stage_only() {
  run_release_candidate_fs publish-stage-only "$1" "$2"
}

resolve_release_build_provenance_validator() {
  local configured="$BUILD_PROVENANCE_VALIDATOR"
  local candidate=""
  local governed_validator=""

  for candidate in \
    "$REPO_ROOT/../chummer.run-services/scripts/release/verify_release_build_provenance_bundle.py" \
    "$REPO_ROOT/../chummer6-hub/scripts/release/verify_release_build_provenance_bundle.py"
  do
    if [[ -f "$candidate" && ! -L "$candidate" ]]; then
      governed_validator="$candidate"
      break
    fi
  done

  if [[ -z "$governed_validator" ]]; then
    echo "Mac publication requires the governed portable release build provenance validator." >&2
    return 1
  fi
  reject_lexical_symlink_components \
    "$governed_validator" \
    "$(dirname "$governed_validator")/build_provenance_support.py"
  if [[ ! -f "$(dirname "$governed_validator")/build_provenance_support.py" ]]; then
    echo "Governed build provenance support module is missing beside: $governed_validator" >&2
    return 1
  fi

  if [[ -z "$configured" ]]; then
    printf '%s\n' "$governed_validator"
    return 0
  fi
  reject_lexical_symlink_components \
    "$configured" \
    "$(dirname "$configured")/build_provenance_support.py"
  if [[ ! -f "$configured" || ! -f "$(dirname "$configured")/build_provenance_support.py" ]]; then
    echo "Configured release build provenance validator or support module is missing: $configured" >&2
    return 1
  fi
  if ! run_release_candidate_fs compare-validator \
    "$governed_validator" \
    "$configured" \
    "$(dirname "$governed_validator")/build_provenance_support.py" \
    "$(dirname "$configured")/build_provenance_support.py"
  then
    return 1
  fi
  printf '%s\n' "$governed_validator"
}

classify_release_build_provenance_requirement() {
  run_release_candidate_fs classify-provenance \
    "$BUILD_PROVENANCE_MANIFEST_SOURCE" \
    "$FILES_SOURCE"
}

copy_regular_tree_exact() {
  run_release_candidate_fs copy-tree "$1" "$2" "${3:-$1}"
}

stage_governed_build_provenance_validator() {
  run_release_candidate_fs stage-validator "$1" "$2"
}

compare_regular_tree_bytes() {
  run_release_candidate_fs compare-trees "$1" "$2"
}

verify_candidate_manifest_mac_identity_agreement() {
  run_release_candidate_fs verify-mac-identity "$1" "$2" "$3"
}

verify_release_candidate_shelf_invariants() {
  local candidate_root="$1"
  local requested_channel="$2"
  shift 2
  run_release_candidate_fs verify-shelf "$candidate_root" "$requested_channel" "$@"
}
prepare_release_build_provenance() {
  local requirement_status=0
  local validator=""
  local source_root="$BUNDLE_DIR/proof/build-provenance/v1"
  local proof_root="$BUNDLE_DIR/proof"
  local build_provenance_root="$BUNDLE_DIR/proof/build-provenance"

  reject_lexical_symlink_components \
    "$BUNDLE_DIR" \
    "$BUILD_PROVENANCE_MANIFEST_SOURCE" \
    "$proof_root" \
    "$build_provenance_root" \
    "$source_root"
  if classify_release_build_provenance_requirement; then
    BUILD_PROVENANCE_REQUIRED=1
  else
    requirement_status=$?
    if (( requirement_status == 1 )); then
      BUILD_PROVENANCE_REQUIRED=0
      BUILD_PROVENANCE_STAGE_ROOT=""
      return 0
    fi
    return "$requirement_status"
  fi

  validator="$(resolve_release_build_provenance_validator)" || return 1
  for path in "$proof_root" "$build_provenance_root" "$source_root"; do
    if [[ -L "$path" ]]; then
      echo "Build provenance path cannot be a symlink: $path" >&2
      return 1
    fi
  done
  if [[ ! -d "$source_root" ]]; then
    echo "Mac publication requires governed proof/build-provenance/v1 evidence: $source_root" >&2
    return 1
  fi

  validator="$(stage_governed_build_provenance_validator \
    "$validator" \
    "$sync_source_dir/governed-build-provenance-validator")" || return 1
  BUILD_PROVENANCE_VALIDATOR_RESOLVED="$validator"
  BUILD_PROVENANCE_STAGE_ROOT="$sync_source_dir/build-provenance-v1"
  copy_regular_tree_exact "$source_root" "$BUILD_PROVENANCE_STAGE_ROOT" "$proof_root"
  python3 -I "$validator" "$BUNDLE_DIR"
  compare_regular_tree_bytes "$source_root" "$BUILD_PROVENANCE_STAGE_ROOT"
}

preflight_release_build_provenance_target() {
  local target_root="$1"
  local path=""
  reject_lexical_symlink_components \
    "$target_root" \
    "$target_root/proof" \
    "$target_root/proof/build-provenance" \
    "$target_root/proof/build-provenance/v1"
  for path in \
    "$target_root/proof" \
    "$target_root/proof/build-provenance" \
    "$target_root/proof/build-provenance/v1"
  do
    if [[ -L "$path" ]]; then
      echo "Refusing to mutate a symlinked build provenance target: $path" >&2
      return 1
    fi
    if [[ -e "$path" && ! -d "$path" ]]; then
      echo "Refusing to mutate a non-directory build provenance target: $path" >&2
      return 1
    fi
  done
}

preflight_managed_release_target() {
  run_release_candidate_fs preflight-managed "$1"
}
sync_release_build_provenance_namespace() {
  local target_root="$1"
  local namespace_root="$target_root/proof/build-provenance"
  local target_v1="$namespace_root/v1"
  local staged_copy=""
  local backup=""

  preflight_release_build_provenance_target "$target_root"
  if (( BUILD_PROVENANCE_REQUIRED == 0 )); then
    rm -rf -- "$target_v1"
    rmdir "$namespace_root" 2>/dev/null || true
    return 0
  fi

  mkdir -p "$namespace_root"
  staged_copy="$namespace_root/.v1.publish-stage.$$"
  backup="$namespace_root/.v1.publish-backup.$$"
  rm -rf -- "$staged_copy" "$backup"
  if ! copy_regular_tree_exact "$BUILD_PROVENANCE_STAGE_ROOT" "$staged_copy"; then
    rm -rf -- "$staged_copy"
    return 1
  fi
  if ! compare_regular_tree_bytes "$BUILD_PROVENANCE_STAGE_ROOT" "$staged_copy"; then
    rm -rf -- "$staged_copy"
    return 1
  fi

  if [[ -e "$target_v1" ]]; then
    mv "$target_v1" "$backup"
  fi
  if ! mv "$staged_copy" "$target_v1"; then
    rm -rf -- "$staged_copy"
    if [[ -e "$backup" ]]; then
      mv "$backup" "$target_v1"
    fi
    return 1
  fi
  if ! compare_regular_tree_bytes "$BUILD_PROVENANCE_STAGE_ROOT" "$target_v1"; then
    rm -rf -- "$target_v1"
    if [[ -e "$backup" ]]; then
      mv "$backup" "$target_v1"
    fi
    return 1
  fi
  rm -rf -- "$backup"
}

transactionally_publish_release_candidate() {
  local candidate_root="$1"
  local validator_path="$2"
  local target_dir=""
  shift 2
  for target_dir in "$@"; do
    assert_legacy_release_shelf_target "$target_dir"
  done
  run_release_candidate_fs transaction "$candidate_root" "$validator_path" "$@"
}
require_mutable_release_shelf() {
  local deploy_dir="$1"
  if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] \
    && to_bool "${CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY:-0}"; then
    return 0
  fi
  if [[ -e "$deploy_dir/.release-shelf-layout-v1" \
    || -L "$deploy_dir/.release-shelf-layout-v1" \
    || -e "$deploy_dir/current.json" \
    || -L "$deploy_dir/current.json" ]]; then
    echo "Refusing legacy in-place downloads publication: immutable release shelf layout v1 is active at $deploy_dir." >&2
    echo "Use the governed .release-shelf-layout-v1 generation lane and current.json pointer; this writer must not mutate that shelf." >&2
    exit 78
  fi
}

normalize_mirror_sync_mode() {
  local value
  value="$(echo "${1:-auto}" | tr '[:upper:]' '[:lower:]')"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  case "$value" in
    ""|auto)
      printf '%s\n' "auto"
      ;;
    1|true|yes|on)
      printf '%s\n' "true"
      ;;
    0|false|no|off)
      printf '%s\n' "false"
      ;;
    *)
      echo "Invalid CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS value: '$1' (expected auto|true|false)." >&2
      exit 1
      ;;
  esac
}

validate_absolute_http_url() {
  local value="$1"
  local label="$2"
  python3 - "$value" "$label" <<'PY'
import sys
from urllib.parse import urlparse

value = sys.argv[1].strip()
label = sys.argv[2]
parsed = urlparse(value)
if parsed.scheme.lower() not in {"http", "https"} or not parsed.netloc:
    print(
        f"Invalid {label}: {value!r} (expected absolute http:// or https:// URL).",
        file=sys.stderr,
    )
    raise SystemExit(1)
PY
}

forced_preview_nightly_visual_handoff_allowed() {
  local bundle_dir="$1"
  local deploy_dir="$2"
  local release_channel

  if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then
    return 1
  fi

  release_channel="$(echo "${RELEASE_CHANNEL:-preview}" | tr '[:upper:]' '[:lower:]')"
  if [[ "$release_channel" != "preview" ]]; then
    return 1
  fi
  if ! manifest_channel_is_preview "$deploy_dir/RELEASE_CHANNEL.generated.json"; then
    return 1
  fi

  python3 - "$bundle_dir" "$deploy_dir" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path


ALLOWED_BLOCKER = "Windows visual proof is still outstanding for the staged installer bytes."


def load_json(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


roots = [Path(item) for item in sys.argv[1:] if str(item or "").strip()]
handoff: dict = {}
for root in roots:
    candidate = load_json(root / "RELEASE_BUILD_HANDOFF.generated.json")
    if candidate:
        handoff = candidate
        break

if not handoff:
    raise SystemExit(1)

visual = handoff.get("windows_visual_proof_handoff")
if not isinstance(visual, dict):
    visual = {}
    for root in roots:
        visual = load_json(root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json")
        if visual:
            break

blockers = handoff.get("blockers")
if blockers != [ALLOWED_BLOCKER]:
    raise SystemExit(1)
if normalize(handoff.get("channel")) != "preview":
    raise SystemExit(1)
if handoff.get("stage_proof_complete") is not False:
    raise SystemExit(1)
if normalize(visual.get("status")) != "ready_for_windows_host":
    raise SystemExit(1)
if visual.get("only_blocker_is_visual_proof") is not True:
    raise SystemExit(1)

print("ok")
PY
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

array_values_nul() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || return 0

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "printf '%s\\0' \"\${${array_name}[@]}\""
  local status="$?"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  return "$status"
}

resolve_path_allow_missing() {
  python3 - "$1" <<'PY'
import pathlib
import sys

print(pathlib.Path(sys.argv[1]).resolve(strict=False))
PY
}

manifest_channel_is_preview() {
  local manifest_path="$1"
  python3 - "$manifest_path" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

channel = str(payload.get("channel") or payload.get("channelId") or "").strip().lower()
raise SystemExit(0 if channel == "preview" else 1)
PY
}

verify_bundle_layout() {
  local bundle_dir="$1"
  local files_dir="$2"
  local normalized_bundle_dir="${bundle_dir%/}"
  local parent_dir
  parent_dir="$(dirname "$normalized_bundle_dir")"
  local nested_files_dir="$files_dir/files"

  if [[ "$(basename "$normalized_bundle_dir")" == "files" ]] \
    && [[ -f "$parent_dir/releases.json" || -f "$parent_dir/RELEASE_CHANNEL.generated.json" ]]; then
    echo "Bundle root points at files/ directory: $normalized_bundle_dir" >&2
    echo "Publish from the stage or bundle root, not its files/ child." >&2
    exit 1
  fi

  if [[ -d "$nested_files_dir" ]] && find "$nested_files_dir" -mindepth 1 -maxdepth 1 | grep -q .; then
    echo "Bundle is malformed: found nested files directory under $nested_files_dir" >&2
    echo "Publish from the stage or bundle root, not its files/ child." >&2
    exit 1
  fi
}

verify_windows_only_publication_source() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local stage_root="$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT"
  local expected_bundle="$stage_root/publication"
  local helper="$SCRIPT_DIR/preview_nightly_publication_scope.py"
  local stage_helper="$SCRIPT_DIR/preview_nightly_stage_contract.py"

  if [[ ! -f "$helper" ]]; then
    echo "Missing Windows-only publication-scope helper: $helper" >&2
    exit 1
  fi
  if [[ "$(resolve_path_allow_missing "$BUNDLE_DIR")" != "$(resolve_path_allow_missing "$expected_bundle")" ]]; then
    echo "Windows-only publication must use the exact composed publication/ shelf: $expected_bundle" >&2
    exit 1
  fi
  python3 "$stage_helper" verify --stage-dir "$stage_root" >/dev/null
  python3 "$helper" verify \
    --scope "$stage_root/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    --proposal "$stage_root/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" \
    --publication-dir "$BUNDLE_DIR" \
    --evidence-root "$stage_root" >/dev/null
}

require_windows_only_registry_finalize_authority() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local scope_path="$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json"
  python3 - "$scope_path" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
try:
    scope = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception as exc:
    raise SystemExit(f"Registry FINALIZE gate cannot read the final UI scope: {exc}")

prepare = scope.get("registryPrepare")
if (
    scope.get("status") != "validated"
    or scope.get("registryFinalizeEligible") is not True
    or not isinstance(prepare, dict)
    or prepare.get("finalizeAvailable") is not True
    or "finalizeReceipt" not in prepare
    or prepare.get("finalizeReceipt") is not None
):
    raise SystemExit(
        "Registry FINALIZE authority is unavailable or unsealed; stopped before "
        "publication locks, generation, exchange, upload, and deployment"
    )
if any(
    scope.get(key) is not False
    for key in ("publicationEligible", "uploadAuthorized", "deployAuthorized")
):
    raise SystemExit(
        "final UI scope overclaims publication/upload/deploy authority; stopped "
        "before any publication mutation"
    )
for key in ("publicationEligible", "releaseUploadAuthority", "deployAuthority", "routeAuthority"):
    if prepare.get(key) is not False:
        raise SystemExit(
            f"Registry FINALIZE binding overclaims {key}; stopped before any publication mutation"
        )
PY
}

replay_windows_only_registry_prepare() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  if [[ -z "$WINDOWS_REGISTRY_ROOT" || ! -d "$WINDOWS_REGISTRY_ROOT" ]]; then
    echo "Windows-only activation requires CHUMMER_HUB_REGISTRY_ROOT for the pinned PREPARE replay." >&2
    exit 1
  fi
  python3 "$SCRIPT_DIR/preview_nightly_publication_scope.py" replay-registry-prepare \
    --scope "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    --evidence-root "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT" \
    --registry-root "$WINDOWS_REGISTRY_ROOT" >/dev/null
}

snapshot_windows_only_publication_source() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local stage_root="$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT"
  local helper="$SCRIPT_DIR/preview_nightly_publication_scope.py"
  local stage_helper="$SCRIPT_DIR/preview_nightly_stage_contract.py"
  local snapshot=""
  snapshot="$(mktemp -d)"
  cp -a "$stage_root/." "$snapshot/"
  python3 "$stage_helper" verify --stage-dir "$snapshot" >/dev/null
  python3 "$helper" verify \
    --scope "$snapshot/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    --proposal "$snapshot/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" \
    --publication-dir "$snapshot/publication" \
    --evidence-root "$snapshot" >/dev/null
  WINDOWS_ONLY_PUBLICATION_SNAPSHOT="$snapshot"
  WINDOWS_ONLY_PUBLICATION_STAGE_ROOT="$snapshot"
  BUNDLE_DIR="$snapshot/publication"
  MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
  FILES_SOURCE="$BUNDLE_DIR/files"
  STARTUP_SMOKE_SOURCE="$snapshot/startup-smoke"
  export CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$snapshot/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
}

verify_deployed_windows_only_publication_shelf() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  python3 - \
    "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    "$BUNDLE_DIR" \
    "$DEPLOY_DIR" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path

scope_path, source_root, deploy_root = map(Path, sys.argv[1:])
scope = json.loads(scope_path.read_text(encoding="utf-8-sig"))

def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()

for name in ("RELEASE_CHANNEL.generated.json", "releases.json"):
    source = source_root / name
    deployed = deploy_root / name
    if not deployed.is_file() or digest(deployed) != digest(source):
        raise SystemExit(f"deployed Windows-only shelf changed manifest bytes: {name}")

expected = {
    row["fileName"]: (row["sha256"], row["sizeBytes"])
    for row in scope["postPublicationShelfTuples"]
}
files_root = deploy_root / "files"
for name, (expected_sha, expected_size) in expected.items():
    path = files_root / name
    if (
        path.is_symlink()
        or not path.is_file()
        or path.stat().st_size != expected_size
        or digest(path) != expected_sha
    ):
        raise SystemExit(f"deployed Windows-only shelf changed artifact bytes: {name}")

desktop = re.compile(
    r"^chummer-.*(?:\.exe|\.zip|\.tar\.gz|\.deb|\.pkg|\.dmg|\.msix|\.zip\.json)$",
    re.IGNORECASE,
)
actual_desktop = {
    path.name
    for path in files_root.iterdir()
    if path.is_file() and not path.is_symlink() and desktop.fullmatch(path.name)
}
if actual_desktop != set(expected):
    raise SystemExit(
        "deployed Windows-only shelf has missing or unexplained desktop bytes: "
        f"expected={sorted(expected)} actual={sorted(actual_desktop)}"
    )
PY
}

prepare_windows_only_publication_target() {
  local target="$1"
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local resolved_target=""
  local transaction_root=""
  local generation=""
  local receipt=""
  local -a inventory_bindings=()
  local lock_dir=""
  local lock_fd=""
  local lock_already_held=false

  [[ -f "$helper" ]] || {
    echo "Missing Windows-only transaction helper: $helper" >&2
    exit 1
  }
  if [[ ! -d "$target" || -L "$target" ]]; then
    echo "Windows-only publication requires an existing non-symlink incumbent shelf: $target" >&2
    exit 1
  fi
  resolved_target="$(cd "$target" && pwd -P)"
  for existing in "${windows_only_transaction_targets[@]}"; do
    if [[ "$existing" == "$resolved_target" ]]; then
      echo "Windows-only publication target is duplicated: $resolved_target" >&2
      exit 1
    fi
  done
  lock_dir="$(dirname "$resolved_target")/.chummer-windows-publication.lock"
  for existing in "${windows_only_transaction_lock_dirs[@]}"; do
    if [[ "$existing" == "$lock_dir" ]]; then
      lock_already_held=true
      break
    fi
  done
  if [[ "$lock_already_held" == "false" ]]; then
    if ! command -v flock >/dev/null 2>&1; then
      echo "Windows-only publication requires flock for SIGKILL-safe target leases." >&2
      exit 1
    fi
    if [[ -L "$lock_dir" ]]; then
      echo "Windows-only publication lock directory must not be a symlink: $lock_dir" >&2
      exit 1
    fi
    mkdir -m 0700 -p "$lock_dir"
    if [[ ! -d "$lock_dir" || -L "$lock_dir" ]]; then
      echo "Windows-only publication lock directory is unsafe: $lock_dir" >&2
      exit 1
    fi
    exec {lock_fd}>"$lock_dir/lease"
    chmod 0600 "$lock_dir/lease"
    if ! flock -n "$lock_fd"; then
      exec {lock_fd}>&-
      echo "Another Windows-only publication transaction holds $lock_dir; retry after it exits." >&2
      exit 1
    fi
    windows_only_transaction_lock_dirs+=("$lock_dir")
    windows_only_transaction_lock_fds+=("$lock_fd")
  fi
  transaction_root="$(mktemp -d "$(dirname "$resolved_target")/.chummer-windows-publication.XXXXXX")"
  python3 "$helper" ensure-directory --directory "$transaction_root" >/dev/null
  windows_only_transaction_roots+=("$transaction_root")
  generation="$transaction_root/generation"
  receipt="$transaction_root/generation.receipt.json"
  python3 "$helper" prepare \
    --scope "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json" \
    --proposal "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json" \
    --evidence-root "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT" \
    --publication-dir "$BUNDLE_DIR" \
    --incumbent "$resolved_target" \
    --output-dir "$generation" \
    --receipt "$receipt" >/dev/null
  while IFS= read -r value; do
    inventory_bindings+=("$value")
  done < <(python3 - "$receipt" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
for key in ("incumbentInventorySha256", "preparedInventorySha256"):
    value = payload.get(key)
    if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{64}", value) is None:
        raise SystemExit(f"generation receipt has invalid {key}")
    print(value)
candidate = payload.get("runUploadCandidate")
expected_keys = {
    "version", "canonicalManifestSha256", "inventorySha256",
    "fileCount", "totalBytes", "bundleIdentitySha256",
}
if not isinstance(candidate, dict) or set(candidate) != expected_keys:
    raise SystemExit("generation receipt has invalid runUploadCandidate")
version = candidate.get("version")
if (
    not isinstance(version, str)
    or not (1 <= len(version) <= 160)
    or any(ord(character) < 0x21 or ord(character) > 0x7e for character in version)
):
    raise SystemExit("generation receipt has invalid Run version")
for key in ("canonicalManifestSha256", "inventorySha256", "bundleIdentitySha256"):
    if not isinstance(candidate.get(key), str) or re.fullmatch(r"[0-9a-f]{64}", candidate[key]) is None:
        raise SystemExit(f"generation receipt has invalid Run {key}")
for key, minimum in (("fileCount", 1), ("totalBytes", 0)):
    value = candidate.get(key)
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise SystemExit(f"generation receipt has invalid Run {key}")
identity = {
    key: candidate[key]
    for key in (
        "version", "canonicalManifestSha256", "inventorySha256",
        "fileCount", "totalBytes",
    )
}
identity_bytes = json.dumps(identity, sort_keys=True, separators=(",", ":")).encode("utf-8")
if hashlib.sha256(identity_bytes).hexdigest() != candidate["bundleIdentitySha256"]:
    raise SystemExit("generation receipt has invalid Run bundle identity")
for key in ("version", "canonicalManifestSha256", "inventorySha256", "fileCount", "totalBytes"):
    print(candidate[key])
PY
  )
  if [[ "${#inventory_bindings[@]}" -ne 7 ]]; then
    echo "Windows-only generation receipt did not emit exact inventory and Run bindings." >&2
    exit 1
  fi
  windows_only_transaction_targets+=("$resolved_target")
  windows_only_transaction_generations+=("$generation")
  windows_only_transaction_generation_receipts+=("$receipt")
  windows_only_transaction_activation_receipts+=("$transaction_root/activation.receipt.json")
  windows_only_transaction_incumbent_inventories+=("${inventory_bindings[0]}")
  windows_only_transaction_prepared_inventories+=("${inventory_bindings[1]}")
  windows_only_transaction_run_versions+=("${inventory_bindings[2]}")
  windows_only_transaction_run_manifest_sha256s+=("${inventory_bindings[3]}")
  windows_only_transaction_run_inventory_sha256s+=("${inventory_bindings[4]}")
  windows_only_transaction_run_file_counts+=("${inventory_bindings[5]}")
  windows_only_transaction_run_total_bytes+=("${inventory_bindings[6]}")
}

prepare_windows_only_publication_targets() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  prepare_windows_only_publication_target "$DEPLOY_DIR"
  local mirror=""
  while IFS= read -r -d '' mirror; do
    prepare_windows_only_publication_target "$mirror"
  done < <(array_values_nul live_downloads_mirror_dirs)
}

initialize_windows_only_publication_transaction() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local transaction_id="${CHUMMER_WINDOWS_PUBLICATION_TRANSACTION_ID:-}"
  local publication_receipt_run_id=""
  if [[ -z "$transaction_id" ]]; then
    transaction_id="$(python3 - <<'PY'
import secrets
print(f"windows-nightly-{secrets.token_hex(16)}")
PY
)"
  fi
  WINDOWS_ONLY_TRANSACTION_ID="$transaction_id"
  publication_receipt_current="${CHUMMER_DOWNLOADS_PUBLICATION_RECEIPT_PATH:-${DEPLOY_DIR%/}.PUBLICATION_SCOPE.generated.json}"
  WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR="${CHUMMER_DOWNLOADS_PUBLICATION_RECEIPT_DIR:-${publication_receipt_current}.d}"
  publication_receipt_run_id="$(python3 - "$transaction_id" <<'PY'
import hashlib
import sys
print(hashlib.sha256(sys.argv[1].encode("utf-8")).hexdigest())
PY
)"
  python3 "$helper" ensure-directory \
    --directory "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR" >/dev/null
  publication_receipt_output="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.committed.json"
  publication_abort_output="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.aborted.json"
  WINDOWS_ONLY_TRANSACTION_PREPARED="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.transaction.prepared.json"
  WINDOWS_ONLY_TRANSACTION_JOURNAL="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.transaction.json"
  WINDOWS_ONLY_TRANSACTION_COMMIT="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.transaction.committed.json"
  WINDOWS_ONLY_TRANSACTION_ROLLBACK="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.transaction.rolled-back.json"
  WINDOWS_ONLY_TRANSACTION_PROOF_DIR="$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR/${publication_receipt_run_id}.activation-proofs"
}

reconcile_discovered_windows_only_publication_transactions() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  if [[ -z "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR" ]]; then
    echo "Windows-only transaction receipt directory was not initialized." >&2
    return 1
  fi
  if ! python3 "$helper" recover-discovered \
      --receipt-dir "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR" >/dev/null
  then
    echo "CRITICAL: discovered Windows-only transaction state is active or ambiguous; preserving durable records and generations for retry/manual reconciliation." >&2
    WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
    return 1
  fi
}

prepare_windows_only_publication_transaction_record() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local index=0
  local -a prepared_args=(
    prepare-transaction
    --transaction-id "$WINDOWS_ONLY_TRANSACTION_ID"
    --activation-journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL"
    --output "$WINDOWS_ONLY_TRANSACTION_PREPARED"
  )
  for index in "${!windows_only_transaction_targets[@]}"; do
    prepared_args+=(
      --target "${windows_only_transaction_targets[$index]}"
      --prepared "${windows_only_transaction_generations[$index]}"
      --generation-receipt "${windows_only_transaction_generation_receipts[$index]}"
      --activation-receipt "${windows_only_transaction_activation_receipts[$index]}"
    )
  done
  python3 "$helper" "${prepared_args[@]}" >/dev/null
}

activate_windows_only_publication_targets() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local index=0
  local activated_count=0
  local fail_after="${CHUMMER_WINDOWS_ONLY_FAIL_AFTER_ACTIVATION_COUNT:-0}"
  local transaction_id="$WINDOWS_ONLY_TRANSACTION_ID"
  local inject_after_child="${CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_ACTIVATION_CHILD_COUNT:-0}"
  if [[ ! "$fail_after" =~ ^[0-9]+$ ]]; then
    echo "CHUMMER_WINDOWS_ONLY_FAIL_AFTER_ACTIVATION_COUNT must be a non-negative integer." >&2
    exit 1
  fi
  if [[ -z "$transaction_id" ]]; then
    echo "Windows-only transaction ID was not initialized before activation." >&2
    exit 1
  fi
  if [[ ! "$inject_after_child" =~ ^[0-9]+$ ]]; then
    echo "CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_ACTIVATION_CHILD_COUNT must be a non-negative integer." >&2
    exit 1
  fi
  for index in "${!windows_only_transaction_targets[@]}"; do
    if ! python3 "$helper" activate \
        --target "${windows_only_transaction_targets[$index]}" \
        --prepared "${windows_only_transaction_generations[$index]}" \
        --generation-receipt "${windows_only_transaction_generation_receipts[$index]}" \
        --transaction-id "$transaction_id" \
        --receipt "${windows_only_transaction_activation_receipts[$index]}" >/dev/null
    then
      if ! python3 "$helper" recover-activation \
          --target "${windows_only_transaction_targets[$index]}" \
          --prepared "${windows_only_transaction_generations[$index]}" \
          --incumbent-inventory "${windows_only_transaction_incumbent_inventories[$index]}" \
          --prepared-inventory "${windows_only_transaction_prepared_inventories[$index]}" \
          --activation-receipt "${windows_only_transaction_activation_receipts[$index]}" >/dev/null
      then
        echo "CRITICAL: failed to recover interrupted Windows-only activation for ${windows_only_transaction_targets[$index]}" >&2
        windows_only_transaction_activated+=("$index")
      fi
      return 1
    fi
    if (( inject_after_child > 0 && activated_count + 1 == inject_after_child )); then
      echo "Injected exit after activation child success and before shell bookkeeping for target $((activated_count + 1))." >&2
      exit 96
    fi
    windows_only_transaction_activated+=("$index")
    activated_count=$((activated_count + 1))
    if (( fail_after > 0 && activated_count == fail_after )); then
      echo "Injected Windows-only activation failure after target $activated_count." >&2
      return 1
    fi
  done
}

rollback_windows_only_publication_targets() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local offset=0
  local index=0
  local rollback_failed=0
  for ((offset=${#windows_only_transaction_activated[@]} - 1; offset >= 0; offset--)); do
    index="${windows_only_transaction_activated[$offset]}"
    if ! python3 "$helper" exchange \
      --left "${windows_only_transaction_targets[$index]}" \
      --right "${windows_only_transaction_generations[$index]}" \
      --expected-left-inventory "${windows_only_transaction_prepared_inventories[$index]}" \
      --expected-right-inventory "${windows_only_transaction_incumbent_inventories[$index]}" >/dev/null
    then
      echo "CRITICAL: failed to roll back Windows-only publication target ${windows_only_transaction_targets[$index]}" >&2
      rollback_failed=1
    fi
  done
  windows_only_transaction_activated=()
  return "$rollback_failed"
}

recover_windows_only_publication_transaction() {
  [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || return 0
  local helper="$SCRIPT_DIR/windows_only_publication_transaction.py"
  local state=""

  if [[ -z "$WINDOWS_ONLY_TRANSACTION_JOURNAL" || ! -f "$WINDOWS_ONLY_TRANSACTION_JOURNAL" ]]; then
    if [[ -n "$WINDOWS_ONLY_TRANSACTION_PREPARED" && -f "$WINDOWS_ONLY_TRANSACTION_PREPARED" ]]; then
      if ! python3 "$helper" recover-prepared \
          --prepared-record "$WINDOWS_ONLY_TRANSACTION_PREPARED" \
          --activation-journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
          --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" \
          --rollback "$WINDOWS_ONLY_TRANSACTION_ROLLBACK" >/dev/null
      then
        echo "CRITICAL: durable prepared transaction could not reconcile every target; preserving locks and generations." >&2
        WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
        return 1
      fi
      return 0
    fi
    if ! rollback_windows_only_publication_targets; then
      echo "CRITICAL: pre-journal Windows-only rollback failed; preserving locks and generations." >&2
      WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
      return 1
    fi
    return 0
  fi
  if ! state="$(
    python3 "$helper" transaction-status \
      --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
      --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" \
      --rollback "$WINDOWS_ONLY_TRANSACTION_ROLLBACK" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])'
  )"; then
    echo "CRITICAL: Windows-only transaction journal is not safely recoverable; preserving locks and generations." >&2
    WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
    return 1
  fi
  case "$state" in
    committed)
      if ! python3 "$helper" install-current \
          --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
          --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" >/dev/null
      then
        echo "CRITICAL: committed Windows-only transaction could not repair its current receipt; preserving locks and generations." >&2
        WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
        return 1
      fi
      return 0
      ;;
    rolled_back)
      return 0
      ;;
    activated|partially_rolled_back|rolled_back_pending_marker)
      if ! python3 "$helper" discard-uncommitted \
          --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
          --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" >/dev/null
      then
        echo "CRITICAL: could not discard an uncommitted Windows-only receipt; preserving locks and generations." >&2
        WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
        return 1
      fi
      if ! python3 "$helper" resume-rollback \
          --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
          --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" >/dev/null
      then
        echo "CRITICAL: Windows-only transaction rollback could not be resumed exactly; preserving locks and generations." >&2
        WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
        return 1
      fi
      if ! python3 "$helper" mark-rolled-back \
          --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
          --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" \
          --rollback "$WINDOWS_ONLY_TRANSACTION_ROLLBACK" >/dev/null
      then
        echo "CRITICAL: rolled-back Windows-only shelves could not be durably reconciled; preserving locks and generations." >&2
        WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
        return 1
      fi
      return 0
      ;;
    *)
      echo "CRITICAL: unexpected Windows-only transaction state: $state" >&2
      WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE=true
      return 1
      ;;
  esac
}

refresh_release_build_handoff() {
  local bundle_dir="$1"
  local handoff_script="${CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH:-$SCRIPT_DIR/materialize_release_candidate_handoff.py}"

  if [[ ! -f "$bundle_dir/RELEASE_CHANNEL.generated.json" ]]; then
    return 0
  fi

  if [[ ! -f "$handoff_script" ]]; then
    echo "Skipping release build handoff refresh because the materializer is missing: $handoff_script" >&2
    return 0
  fi

  if ! python3 "$handoff_script" "$bundle_dir" >/dev/null; then
    echo "Skipping release build handoff refresh because materialization failed for bundle: $bundle_dir" >&2
    return 0
  fi
}

persist_windows_visual_proof_handoff_to_bundle() {
  local candidate_root="$1"
  if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] \
    || to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" \
    || [[ "$(resolve_path_allow_missing "$candidate_root")" == "$(resolve_path_allow_missing "$BUNDLE_DIR")" ]]; then
    return 0
  fi
  run_release_candidate_fs persist-handoff "$candidate_root" "$BUNDLE_DIR"
}

emit_windows_visual_proof_handoff_guidance() {
  python3 - "$@" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path


def load_json(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def normalize(value: object) -> str:
    return str(value or "").strip()


roots = [Path(item) for item in sys.argv[1:] if normalize(item)]
handoff_payload = {}
handoff_path = None

for root in roots:
    direct_path = root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    if direct_path.is_file():
        handoff_payload = load_json(direct_path)
        handoff_path = direct_path
        break

for root in roots:
    if handoff_payload:
        break
    release_handoff_path = root / "RELEASE_BUILD_HANDOFF.generated.json"
    if release_handoff_path.is_file():
        release_handoff = load_json(release_handoff_path)
        candidate = release_handoff.get("windows_visual_proof_handoff")
        if isinstance(candidate, dict):
            handoff_payload = candidate
            candidate_path = normalize(candidate.get("json_path"))
            if candidate_path:
                handoff_path = Path(candidate_path)
            break

if not handoff_payload:
    raise SystemExit(1)

status = normalize(handoff_payload.get("status"))
summary = normalize(handoff_payload.get("summary"))
next_actions = handoff_payload.get("next_actions") if isinstance(handoff_payload.get("next_actions"), list) else []
json_path = normalize(handoff_payload.get("json_path")) or str(handoff_path or "")
json_artifact_path = None
if handoff_path and handoff_path.is_file():
    json_artifact_path = handoff_path
elif json_path:
    candidate = Path(json_path)
    if candidate.is_file():
        json_artifact_path = candidate
blockers = handoff_payload.get("blockers")
blockers_present = isinstance(blockers, list) and any(str(item).strip() for item in blockers)
actionable = status in {"ready", "ready_for_windows_host"} and not blockers_present and json_artifact_path is not None

if json_path:
    print(f"Windows visual proof handoff: {json_path}", file=sys.stderr)
if status:
    print(f"Windows visual proof status: {status}", file=sys.stderr)
if summary:
    print(f"Windows visual proof summary: {summary}", file=sys.stderr)
if next_actions:
    first_action = normalize(next_actions[0])
    if first_action:
        print(f"Windows visual proof next action: {first_action}", file=sys.stderr)
raise SystemExit(0 if actionable else 2)
PY
}

require_public_stable_root_blocker_clearance() {
  local release_channel="${1:-}"
  local normalized_release_channel=""

  normalized_release_channel="$(echo "$release_channel" | tr '[:upper:]' '[:lower:]')"
  if [[ "$normalized_release_channel" != "public_stable" ]]; then
    return 0
  fi

  python3 - "$ROOT_RELEASE_BLOCKERS_PATH" "$PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path


ALLOWED_BLOCKERS = {"release_posture:non_flagship_channel"}
MAX_AGE_ENV_LABEL = "CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS"


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


path = Path(sys.argv[1])
if not path.is_file():
    fail(f"Public stable publication requires root release blocker truth, but the receipt is missing: {path}")

raw_max_age_seconds = str(sys.argv[2]).strip()
try:
    max_age_seconds = int(raw_max_age_seconds)
except ValueError:
    fail(
        f"Invalid {MAX_AGE_ENV_LABEL} value: {sys.argv[2]!r} "
        "(expected integer max age in seconds)."
    )
if max_age_seconds < 0:
    fail(
        f"Invalid {MAX_AGE_ENV_LABEL} value: {sys.argv[2]!r} "
        "(expected integer max age in seconds)."
    )

try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception as exc:  # pragma: no cover - surfaced via stderr
    fail(f"Public stable publication requires readable root release blocker truth: {path} ({exc})")

generated_at = str(payload.get("generated_at") or "").strip()
if not generated_at:
    fail(f"Public stable publication requires fresh root release blocker truth, but generated_at is missing: {path}")
try:
    normalized_generated_at = generated_at[:-1] + "+00:00" if generated_at.endswith("Z") else generated_at
    generated_at_dt = datetime.fromisoformat(normalized_generated_at)
except ValueError as exc:
    fail(
        "Public stable publication requires parseable generated_at in root release blocker truth: "
        f"{path} ({generated_at!r}; {exc})"
    )
if generated_at_dt.tzinfo is None:
    generated_at_dt = generated_at_dt.replace(tzinfo=timezone.utc)
generated_at_utc = generated_at_dt.astimezone(timezone.utc)
age_seconds = (datetime.now(timezone.utc) - generated_at_utc).total_seconds()
if age_seconds > max_age_seconds:
    fail(
        "Public stable publication requires fresh root release blocker truth. "
        f"source={path} generated_at={generated_at} "
        f"age_seconds={int(age_seconds)} max_age_seconds={max_age_seconds}"
    )

blockers = payload.get("blockers")
if not isinstance(blockers, list):
    blockers = payload.get("root_blockers") if isinstance(payload.get("root_blockers"), list) else None
if not isinstance(blockers, list):
    fail(f"Public stable publication requires a blockers list in root release blocker truth: {path}")

blocker_ids: list[str] = []
for entry in blockers:
    if not isinstance(entry, dict):
        continue
    blocker_id = str(entry.get("blocker_id") or entry.get("id") or "").strip()
    if blocker_id:
        blocker_ids.append(blocker_id)

disallowed = [blocker_id for blocker_id in blocker_ids if blocker_id not in ALLOWED_BLOCKERS]
if disallowed:
    fail(
        "Public stable publication is blocked by root release truth. "
        f"source={path} generated_at={generated_at or 'unknown'} "
        f"root_blocker_ids={','.join(blocker_ids) or '(none)'} "
        f"disallowed_blockers={','.join(disallowed)}"
    )
PY
}

is_public_artifact() {
  local artifact_name
  artifact_name="$(basename "$1")"
  case "$artifact_name" in
    chummer-*-win-*-payload.zip.json)
      return 0
      ;;
    chummer-*-win-*-payload.zip)
      return 0
      ;;
    chummer-*-win-*.zip|chummer-*-win-*.tar.gz|chummer-*-win-*.exe)
      if [[ "$artifact_name" != *-installer.exe ]]; then
        return 1
      fi
      ;;
  esac
  return 0
}

verify_windows_installer_payload_gate() {
  if [[ ! -f "$SCRIPT_DIR/verify-windows-installer-payloads.py" ]]; then
    echo "Missing Windows installer payload gate: $SCRIPT_DIR/verify-windows-installer-payloads.py" >&2
    exit 1
  fi

  local -a gate_args=(--files-dir "$FILES_SOURCE" --require-embedded-bootstrap-metadata --require-manifest-row)
  local -a installer_candidates=()
  [[ -f "$MANIFEST_SOURCE" ]] && gate_args+=(--manifest "$MANIFEST_SOURCE")
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$BUNDLE_DIR" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$FILES_SOURCE" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  local installer_candidate_count
  installer_candidate_count="$(array_count installer_candidates)"
  if (( installer_candidate_count == 0 )); then
    gate_args+=(--allow-empty)
  else
    local installer_path=""
    for installer_path in "${installer_candidates[@]}"; do
      gate_args+=(--installer "$installer_path")
    done
  fi
  python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${gate_args[@]}"
}

verify_windows_desktop_exit_gate() {
  local gate_output
  local visual_proof_path="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-${BUNDLE_DIR}/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"
  gate_output="$(mktemp)"

  if [[ ! -f "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" ]]; then
    echo "Missing Windows desktop exit gate: $SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >&2
    rm -f "$gate_output"
    exit 1
  fi

  if ! CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEPLOY_DIR/files" \
    CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path" \
    CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output" \
    bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null
  then
    rm -f "$gate_output"
    persist_windows_visual_proof_handoff_to_bundle "$DEPLOY_DIR" || true
    emit_windows_visual_proof_handoff_guidance "$BUNDLE_DIR" "$DEPLOY_DIR" || true
    if forced_preview_nightly_visual_handoff_allowed "$BUNDLE_DIR" "$DEPLOY_DIR" >/dev/null; then
      echo "Forced preview nightly publication continuing with Windows visual proof handoff only; stable promotion remains blocked." >&2
      return 0
    fi
    echo "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." >&2
    exit 1
  fi

  rm -f "$gate_output"
}

strip_non_public_manifest_rows() {
  local manifest_path="$1"
  python3 - "$manifest_path" "$REPO_ROOT" <<'PY'
import importlib.util
import json
import sys
from pathlib import Path


def file_name_for(row: object) -> str:
    if not isinstance(row, dict):
        return ""
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return file_name
    raw = str(row.get("downloadUrl") or row.get("url") or "").strip()
    return Path(raw).name if raw else ""


def is_public_file_name(file_name: str) -> bool:
    name = file_name.strip().lower()
    if not name:
        return False
    if name.endswith(
        (
            "-installer.deb",
            "-installer.exe",
            "-installer.msix",
            "-installer.dmg",
            "-installer.pkg",
        )
    ):
        return True
    if name.endswith(".tar.gz") and ("-osx-" in name or "-macos-" in name):
        return True
    if name.endswith((".zip", ".tar.gz")):
        return False
    if name.endswith(".exe") and not name.endswith("-installer.exe"):
        return False
    return False


path = Path(sys.argv[1])
repo_root = Path(sys.argv[2])
payload = json.loads(path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(0)

allowed_artifact_ids: set[str] = set()
for key in ("artifacts", "downloads"):
    rows = payload.get(key)
    if not isinstance(rows, list):
        continue
    filtered = []
    for row in rows:
        name = file_name_for(row)
        if not is_public_file_name(name):
            continue
        filtered.append(row)
        if isinstance(row, dict):
            artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
            if artifact_id:
                allowed_artifact_ids.add(artifact_id)
    payload[key] = filtered

for key in ("installAwareArtifactRegistry", "desktopSurfaceRefs", "artifactIdentityRegistry", "artifactPublicationBindings"):
    rows = payload.get(key)
    if not isinstance(rows, list) or not allowed_artifact_ids:
        continue
    payload[key] = [
        row for row in rows
        if isinstance(row, dict) and str(row.get("artifactId") or row.get("id") or "").strip() in allowed_artifact_ids
    ]

verifier_path = repo_root.parent / "chummer-hub-registry" / "scripts" / "verify_public_release_channel.py"
if verifier_path.is_file():
    spec = importlib.util.spec_from_file_location("verify_public_release_channel", verifier_path)
    if spec is not None and spec.loader is not None:
        verifier = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(verifier)
        payload["registryBoundaryCoverage"] = verifier.expected_registry_boundary_coverage(payload)

path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

if [[ -z "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" ]]; then
  if [[ "${RELEASE_CHANNEL:-preview}" =~ ^[Pp][Rr][Ee][Vv][Ii][Ee][Ww]$ ]]; then
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true"
  else
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="false"
  fi
fi

bundle_manifest_matches_files() {
  local manifest_path="$1"
  local files_root="$2"
  python3 - "$manifest_path" "$files_root" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
files_root = Path(sys.argv[2])

payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
downloads = payload.get("downloads") or []
failures: list[str] = []
seen: set[str] = set()
sidecars = {
    "aur-packages.json",
    "chummer6-bin-aur-source.tar.gz",
    "chummer6-bin.PKGBUILD",
    "chummer6-bin.SRCINFO",
}

for artifact in downloads:
    if not isinstance(artifact, dict):
        continue
    url = str(artifact.get("url") or "").strip()
    file_name = Path(url).name if url else ""
    if not file_name:
        continue
    seen.add(file_name)
    file_path = files_root / file_name
    if not file_path.is_file():
        failures.append(f"manifest references missing file {file_name}")
        continue
    actual_size = file_path.stat().st_size
    expected_size = int(artifact.get("sizeBytes") or 0)
    if expected_size and expected_size != actual_size:
        failures.append(f"{file_name}: size {actual_size} != manifest {expected_size}")
    expected_sha = str(artifact.get("sha256") or "").strip().lower()
    if expected_sha:
        digest = hashlib.sha256(file_path.read_bytes()).hexdigest()
        if digest != expected_sha:
            failures.append(f"{file_name}: sha256 {digest} != manifest {expected_sha}")

for file_path in sorted(files_root.iterdir()):
    if not file_path.is_file():
        continue
    if file_path.name in sidecars:
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip"):
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip.json"):
        continue
    if file_path.name not in seen:
        failures.append(f"bundle contains extra file not present in manifest: {file_path.name}")

if failures:
    print("false")
    for failure in failures:
        print(failure)
else:
    print("true")
PY
}

if [[ -z "$PORTAL_MANIFEST_PATH" ]]; then
  if [[ "$(resolve_path_allow_missing "$DEPLOY_DIR")" == "$(resolve_path_allow_missing "$REPO_ROOT/Docker/Downloads")" ]]; then
    PORTAL_MANIFEST_PATH="$REPO_ROOT/Chummer.Portal/downloads/releases.json"
  else
    PORTAL_MANIFEST_PATH="$DEPLOY_DIR/releases.json"
  fi
fi

if [[ -z "$PORTAL_DOWNLOADS_DIR" ]]; then
  PORTAL_DOWNLOADS_DIR="$(dirname "$PORTAL_MANIFEST_PATH")"
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    RELEASE_CANDIDATE_OUTPUT_DIR="$(resolve_release_candidate_output_dir "$RELEASE_CANDIDATE_OUTPUT_DIR")"
  elif [[ -n "$RELEASE_CANDIDATE_OUTPUT_DIR" ]]; then
    echo "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR requires CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY=1." >&2
    exit 1
  fi
  reject_lexical_symlink_components "$BUNDLE_DIR" "$DEPLOY_DIR" "$PORTAL_DOWNLOADS_DIR"
  if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    assert_legacy_release_shelf_target "$DEPLOY_DIR"
    if [[ "$(resolve_path_allow_missing "$PORTAL_DOWNLOADS_DIR")" != "$(resolve_path_allow_missing "$DEPLOY_DIR")" ]]; then
      assert_legacy_release_shelf_target "$PORTAL_DOWNLOADS_DIR"
    fi
  fi
fi

require_mutable_release_shelf "$DEPLOY_DIR"

if [[ ! -d "$BUNDLE_DIR" ]]; then
  echo "Bundle directory not found: $BUNDLE_DIR" >&2
  exit 1
fi

verify_windows_only_publication_source
require_windows_only_registry_finalize_authority
snapshot_windows_only_publication_source
if [[ -n "$WINDOWS_ONLY_PUBLICATION_SNAPSHOT" ]]; then
  trap 'rm -rf "$WINDOWS_ONLY_PUBLICATION_SNAPSHOT"' EXIT
fi
verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"

if [[ ! -d "$FILES_SOURCE" ]]; then
  if to_bool "$ALLOW_BUNDLE_FILES_SOURCE_FALLBACK"; then
    for fallback_files_source in \
      "$REPO_ROOT/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published/files"
    do
      if [[ -d "$fallback_files_source" ]]; then
        FILES_SOURCE="$fallback_files_source"
        break
      fi
    done
  fi
fi

if [[ ! -d "$FILES_SOURCE" ]]; then
  echo "Bundle is missing files directory: $FILES_SOURCE" >&2
  echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2
  echo "Refusing to fall back to unrelated downloads/files roots unless CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK=true is set explicitly." >&2
  exit 1
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  reject_lexical_symlink_components "$FILES_SOURCE"
fi

artifacts=()
while IFS= read -r artifact_path; do
  [[ -n "$artifact_path" ]] || continue
  artifacts+=("$artifact_path")
done < <(find "$FILES_SOURCE" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o \
     -name "chummer-avalonia-*.tar.gz" -o -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
     -name "chummer-avalonia-*-payload.zip.json" -o \
     -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" \) \
  | sort)

artifact_count="$(array_count artifacts)"
if (( artifact_count == 0 )); then
  echo "No desktop artifacts found under $FILES_SOURCE" >&2
  exit 1
fi

verify_windows_installer_payload_gate
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] && ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  refresh_release_build_handoff "$BUNDLE_DIR"
fi

if to_bool "$DEPLOY_MODE"; then
  export CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION="${CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION:-true}"
  export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS:-true}"
  if [[ -z "$LIVE_VERIFY_TARGET" ]]; then
    echo "Deployment mode requires CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL for live manifest verification." >&2
    exit 1
  fi
fi

if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  validate_absolute_http_url "$LIVE_VERIFY_TARGET" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] \
  && { to_bool "$DEPLOY_MODE" || [[ -n "$LIVE_VERIFY_TARGET" ]]; }; then
  echo "The legacy filesystem publisher cannot verify or claim external publication." >&2
  echo "Build a stage-only candidate, then use the governed staged HTTP publisher." >&2
  exit 1
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] && to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  sync_source_dir="$(mktemp -d "$(dirname "$RELEASE_CANDIDATE_OUTPUT_DIR")/.${RELEASE_CANDIDATE_OUTPUT_DIR##*/}.candidate-build.XXXXXXXX")"
else
  sync_source_dir="$(mktemp -d)"
fi
artifact_sync_source_dir="$sync_source_dir/artifacts"
mkdir -p "$artifact_sync_source_dir"
cleanup() {
  local exit_status=$?
  local transaction_root=""
  local lock_fd=""
  trap - EXIT
  set +e
  if ! recover_windows_only_publication_transaction; then
    exit_status=1
  fi
  if [[ "$WINDOWS_ONLY_TRANSACTION_PRESERVE_STATE" == "false" ]]; then
    for transaction_root in "${windows_only_transaction_roots[@]}"; do
      if [[ -n "$transaction_root" ]]; then
        rm -rf "$transaction_root"
      fi
    done
  fi
  for lock_fd in "${windows_only_transaction_lock_fds[@]}"; do
    if [[ "$lock_fd" =~ ^[0-9]+$ ]]; then
      flock -u "$lock_fd" 2>/dev/null || true
      exec {lock_fd}>&-
    fi
  done
  rm -rf "$sync_source_dir"
  if [[ -n "$WINDOWS_ONLY_PUBLICATION_SNAPSHOT" ]]; then
    rm -rf "$WINDOWS_ONLY_PUBLICATION_SNAPSHOT"
  fi
  exit "$exit_status"
}
trap cleanup EXIT

append_unique_downloads_mirror_dir() {
  local candidate="$1"
  local resolved_candidate=""
  local existing=""

  [[ -n "$candidate" ]] || return 0
  resolved_candidate="$(resolve_path_allow_missing "$candidate")"
  while IFS= read -r -d '' existing; do
    [[ -n "$existing" ]] || continue
    if [[ "$(resolve_path_allow_missing "$existing")" == "$resolved_candidate" ]]; then
      return 0
    fi
  done < <(array_values_nul live_downloads_mirror_dirs)
  live_downloads_mirror_dirs+=("$candidate")
}

deploy_dir_is_live_downloads_root() {
  local candidate="$1"
  local resolved_candidate=""
  local known_root=""

  resolved_candidate="$(resolve_path_allow_missing "$candidate")"
  for known_root in \
    "$REPO_ROOT/Docker/Downloads" \
    "$REPO_ROOT/Chummer.Portal/downloads" \
    "$REPO_ROOT/.codex-studio/published/portal" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ "$resolved_candidate" == "$(resolve_path_allow_missing "$known_root")" ]]; then
      return 0
    fi
  done

  return 1
}

deploy_dir_is_repo_owned_live_downloads_root() {
  local candidate="$1"
  local resolved_candidate=""
  local known_root=""

  resolved_candidate="$(resolve_path_allow_missing "$candidate")"
  for known_root in \
    "$REPO_ROOT/Docker/Downloads" \
    "$REPO_ROOT/Chummer.Portal/downloads" \
    "$REPO_ROOT/.codex-studio/published/portal"
  do
    if [[ "$resolved_candidate" == "$(resolve_path_allow_missing "$known_root")" ]]; then
      return 0
    fi
  done

  return 1
}

discover_live_downloads_mirror_dirs() {
  local mode="${1:-auto}"
  local configured="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS:-}"
  local deploy_dir_physical=""
  local canonical_downloads_physical=""
  local portal_downloads_physical=""
  local candidate=""
  local sibling_root=""

  if [[ -n "$configured" ]]; then
    IFS=',' read -r -a configured_dirs <<<"$configured"
    for candidate in "${configured_dirs[@]}"; do
      candidate="${candidate#"${candidate%%[![:space:]]*}"}"
      candidate="${candidate%"${candidate##*[![:space:]]}"}"
      [[ -n "$candidate" ]] || continue
      append_unique_downloads_mirror_dir "$candidate"
    done
  fi

  deploy_dir_physical="$(resolve_path_allow_missing "$DEPLOY_DIR")"
  canonical_downloads_physical="$(resolve_path_allow_missing "$REPO_ROOT/Docker/Downloads")"
  portal_downloads_physical="$(resolve_path_allow_missing "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads")"

  if [[ "$mode" == "auto" ]] && ! deploy_dir_is_repo_owned_live_downloads_root "$deploy_dir_physical"; then
    return 0
  fi

  if [[ "$mode" != "auto" ]] && ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then
    return 0
  fi

  if [[ "$deploy_dir_physical" != "$canonical_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/Docker/Downloads"
  fi

  if [[ "$deploy_dir_physical" != "$portal_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads"
  fi

  for sibling_root in \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ -d "$(dirname "$sibling_root")" ]]; then
      append_unique_downloads_mirror_dir "$sibling_root"
    fi
  done
}

resolve_aur_materializer() {
  local configured="${CHUMMER_AUR_MATERIALIZER:-}"
  local candidate=""

  if [[ -n "$configured" ]]; then
    if [[ -f "$configured" ]]; then
      printf '%s\n' "$configured"
      return 0
    fi
    echo "Configured AUR materializer is missing: $configured" >&2
    return 1
  fi

  for candidate in \
    "$REPO_ROOT/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer.run-services/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer6-hub/scripts/materialize-aur-package.py"
  do
    if [[ -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

remove_aur_sidecar() {
  rm -f \
    "$DEPLOY_DIR/aur-packages.json" \
    "$DEPLOY_DIR/files/chummer6-bin-aur-source.tar.gz" \
    "$DEPLOY_DIR/files/chummer6-bin.PKGBUILD" \
    "$DEPLOY_DIR/files/chummer6-bin.SRCINFO"
}

materialize_aur_sidecar() {
  local materializer=""

  if materializer="$(resolve_aur_materializer)"; then
    python3 "$materializer" \
      --manifest "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
      --files-root "$DEPLOY_DIR/files" \
      --output-root "$DEPLOY_DIR" \
      --downloads-prefix "${CHUMMER_PUBLIC_DOWNLOADS_PREFIX:-https://chummer.run/downloads/files}" \
      --optional >/dev/null
    return 0
  fi

  remove_aur_sidecar
  echo "AUR materializer not found; removed stale AUR sidecar files from $DEPLOY_DIR." >&2
}

sync_live_downloads_mirror_dir() {
  local target_dir="$1"
  local target_label="$2"
  local resolved_target_dir=""
  local resolved_deploy_dir=""
  local resolved_portal_dir=""
  local files_dir=""
  local startup_smoke_dir=""
  local source_path=""
  local file_name=""

  resolved_target_dir="$(resolve_path_allow_missing "$target_dir")"
  resolved_deploy_dir="$(resolve_path_allow_missing "$DEPLOY_DIR")"
  if [[ -n "$PORTAL_DOWNLOADS_DIR" ]]; then
    resolved_portal_dir="$(resolve_path_allow_missing "$PORTAL_DOWNLOADS_DIR")"
  else
    resolved_portal_dir="$resolved_deploy_dir"
  fi

  if [[ "$resolved_target_dir" == "$resolved_deploy_dir" || "$resolved_target_dir" == "$resolved_portal_dir" ]]; then
    return 0
  fi

  mkdir -p "$target_dir"
  cp "$DEPLOY_DIR/releases.json" "$target_dir/releases.json"
  cp "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$target_dir/RELEASE_CHANNEL.generated.json"
  if [[ -f "$DEPLOY_DIR/aur-packages.json" ]]; then
    cp "$DEPLOY_DIR/aur-packages.json" "$target_dir/aur-packages.json"
  else
    rm -f "$target_dir/aur-packages.json"
  fi

  startup_smoke_dir="$target_dir/startup-smoke"
  mkdir -p "$startup_smoke_dir"
  find "$startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
  if [[ -d "$DEPLOY_DIR/startup-smoke" ]] && find "$DEPLOY_DIR/startup-smoke" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp -f "$DEPLOY_DIR"/startup-smoke/* "$startup_smoke_dir"/
  fi

  files_dir="$target_dir/files"
  mkdir -p "$files_dir"
  rm -f \
    "$files_dir"/chummer6-bin-aur-source.tar.gz \
    "$files_dir"/chummer6-bin.PKGBUILD \
    "$files_dir"/chummer6-bin.SRCINFO
  find "$files_dir" -maxdepth 1 -type f \
    \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
       -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
       -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
       -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
       -name "chummer-avalonia-*-payload.zip.json" -o \
       -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
       -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
       -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
       -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
       -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" -o \
       -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
       -name "chummer-6-*-installer.deb" -o -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
       -name "chummer-6-*-installer.msix" -o -name "chummer-6-*-payload.zip" -o \
       -name "chummer-6-*-payload.zip.json" \) \
    -delete

  while IFS= read -r -d '' file_name; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ ! -f "$source_path" ]]; then
      echo "promoted artifact missing from deploy root for $target_label mirror: $source_path" >&2
      exit 1
    fi
    cp "$source_path" "$files_dir/"
  done < <(array_values_nul promoted_file_names)
  for file_name in chummer6-bin-aur-source.tar.gz chummer6-bin.PKGBUILD chummer6-bin.SRCINFO; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ -f "$source_path" ]]; then
      cp "$source_path" "$files_dir/"
    fi
  done

  sync_release_build_provenance_namespace "$target_dir"
  if [[ -f "${staged_promotion_evidence_path:-}" ]]; then
    mkdir -p "$target_dir/release-evidence"
    cp "$staged_promotion_evidence_path" "$target_dir/release-evidence/public-promotion.json"
  fi

  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$target_dir/RELEASE_CHANNEL.generated.json" >/dev/null
  echo "synced ${promoted_file_count} promoted artifact(s) -> $target_label mirror $target_dir"
}

while IFS= read -r -d '' artifact; do
  if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || is_public_artifact "$artifact"; then
    cp "$artifact" "$artifact_sync_source_dir/"
  fi
done < <(array_values_nul artifacts)

release_version="${RELEASE_VERSION:-}"
release_channel="${RELEASE_CHANNEL:-}"
release_published_at="${RELEASE_PUBLISHED_AT:-}"
default_published_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [[ -f "$MANIFEST_SOURCE" ]]; then
  manifest_meta=()
  while IFS= read -r line; do
    manifest_meta+=("$line")
  done < <(python3 - "$MANIFEST_SOURCE" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
print(str(data.get("version", "unpublished")))
print(str(data.get("channel", "docker")))
print(str(data.get("publishedAt", "")))
PY
)

  manifest_integrity=()
  while IFS= read -r line; do
    manifest_integrity+=("$line")
  done < <(bundle_manifest_matches_files "$MANIFEST_SOURCE" "$FILES_SOURCE")
  manifest_matches_files="${manifest_integrity[0]:-false}"

  if [[ "$manifest_matches_files" != "true" && -z "${RELEASE_VERSION:-}" ]]; then
    echo "Bundle files no longer match $MANIFEST_SOURCE, so reusing its release version would be dishonest." >&2
    printf '%s\n' "${manifest_integrity[@]:1}" >&2
    echo "Set RELEASE_VERSION and RELEASE_PUBLISHED_AT explicitly for this republish." >&2
    exit 1
  fi

  if [[ -z "$release_version" && -n "${manifest_meta[0]:-}" ]]; then
    release_version="${manifest_meta[0]}"
  fi
  if [[ -z "$release_channel" && -n "${manifest_meta[1]:-}" ]]; then
    release_channel="${manifest_meta[1]}"
  fi
  if [[ -z "$release_published_at" && -n "${manifest_meta[2]:-}" ]]; then
    release_published_at="${manifest_meta[2]}"
  fi
fi

release_version="${release_version:-unpublished}"
release_channel="${release_channel:-docker}"
release_published_at="${release_published_at:-$default_published_at}"
require_public_stable_root_blocker_clearance "$release_channel"
live_downloads_mirror_dirs=()
sync_live_downloads_mirrors_mode="$(normalize_mirror_sync_mode "$SYNC_LIVE_DOWNLOADS_MIRRORS")"
if [[ "$sync_live_downloads_mirrors_mode" != "false" ]]; then
  if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]] || ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    discover_live_downloads_mirror_dirs "$sync_live_downloads_mirrors_mode"
  fi
fi
live_downloads_mirror_dir_count="$(array_count live_downloads_mirror_dirs)"
transactional_publish_target_dirs=()
final_deploy_dir="$DEPLOY_DIR"
final_portal_downloads_dir="$PORTAL_DOWNLOADS_DIR"
staged_release_root=""
staged_manifest_path=""
staged_canonical_manifest_path=""
staged_promotion_evidence_path=""

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  prepare_release_build_provenance
  if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    transactional_publish_target_dirs=("$DEPLOY_DIR")
    if [[ "$(resolve_path_allow_missing "$PORTAL_DOWNLOADS_DIR")" != "$(resolve_path_allow_missing "$DEPLOY_DIR")" ]]; then
      transactional_publish_target_dirs+=("$PORTAL_DOWNLOADS_DIR")
    fi
    if (( live_downloads_mirror_dir_count > 0 )); then
      while IFS= read -r -d '' mirror_dir; do
        transactional_publish_target_dirs+=("$mirror_dir")
      done < <(array_values_nul live_downloads_mirror_dirs)
    fi
    while IFS= read -r -d '' target_dir; do
      assert_legacy_release_shelf_target "$target_dir"
      preflight_managed_release_target "$target_dir"
      preflight_release_build_provenance_target "$target_dir"
    done < <(array_values_nul transactional_publish_target_dirs)
  fi

  staged_release_root="$sync_source_dir/release-candidate"
  staged_manifest_path="$staged_release_root/releases.json"
  staged_canonical_manifest_path="$staged_release_root/RELEASE_CHANNEL.generated.json"
  staged_promotion_evidence_path="$staged_release_root/release-evidence/public-promotion.json"
  mkdir -p "$staged_release_root"
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  : # The complete generation is prepared and atomically exchanged below.
else
  DOWNLOADS_DIR="$artifact_sync_source_dir" \
  MANIFEST_PATH="$staged_manifest_path" \
  CANONICAL_MANIFEST_PATH="$staged_canonical_manifest_path" \
  CANONICAL_FILES_DIR="$staged_release_root/files" \
  PORTAL_MANIFEST_PATH="$staged_manifest_path" \
  PORTAL_CANONICAL_MANIFEST_PATH="$staged_canonical_manifest_path" \
  PORTAL_DOWNLOADS_DIR="$staged_release_root" \
  PROMOTION_EVIDENCE_PATH="$staged_promotion_evidence_path" \
  QUARANTINE_PROMOTION_EVIDENCE_PATH="$staged_release_root/QUARANTINED_INSTALLER_PROMOTION.generated.json" \
  RELEASE_VERSION="$release_version" \
  RELEASE_CHANNEL="$release_channel" \
  RELEASE_PUBLISHED_AT="$release_published_at" \
  SOURCE_MANIFEST_PATH="$MANIFEST_SOURCE" \
  RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH" \
  STARTUP_SMOKE_DIR="$STARTUP_SMOKE_SOURCE" \
  CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS:-}" \
  CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
  CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS="$SCOPE_TO_STAGE_ARTIFACTS" \
  CHUMMER_EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
  CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \
  CHUMMER_RELEASE_MANIFEST_STAGE_ONLY=1 \
  bash "$SCRIPT_DIR/generate-releases-manifest.sh"

  strip_non_public_manifest_rows "$staged_canonical_manifest_path"
  strip_non_public_manifest_rows "$staged_manifest_path"
  sync_release_build_provenance_namespace "$staged_release_root"
  if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
    verify_candidate_manifest_mac_identity_agreement \
      "$staged_canonical_manifest_path" \
      "$staged_manifest_path" \
      "$staged_release_root/files"
    python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
  fi
  verify_release_candidate_shelf_invariants \
    "$staged_release_root" \
    "$release_channel" \
    "${transactional_publish_target_dirs[@]}"
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE=1 \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$staged_release_root"

  if (( BUILD_PROVENANCE_REQUIRED == 1 )) \
    && { to_bool "$DEPLOY_MODE" || [[ -n "$LIVE_VERIFY_TARGET" ]]; }; then
    echo "Mac legacy filesystem publication requires rollback-safe local cutover; external deploy/live verification must use the staged HTTP publisher." >&2
    exit 1
  fi
  DEPLOY_DIR="$staged_release_root"
  PORTAL_DOWNLOADS_DIR="$staged_release_root"
  PORTAL_MANIFEST_PATH="$staged_manifest_path"
  live_downloads_mirror_dir_count=0
fi

promotion_manifest="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json"
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  promotion_manifest="$BUNDLE_DIR/RELEASE_CHANNEL.generated.json"
fi
promoted_file_names=()
while IFS= read -r file_name; do
  [[ -n "$file_name" ]] || continue
  promoted_file_names+=("$file_name")
done < <(python3 - "$promotion_manifest" "$artifact_sync_source_dir" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
source_root = Path(sys.argv[2])
seen = set()
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue
    names = []
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        file_name = Path(str(artifact.get("downloadUrl") or "").strip()).name
    names.append(file_name)
    payload_name = str(artifact.get("payloadFileName") or "").strip()
    if not payload_name:
        payload_name = Path(str(artifact.get("payloadDownloadUrl") or "").strip()).name
    names.append(payload_name)
    if payload_name:
        payload_metadata_name = payload_name + ".json"
        if (source_root / payload_metadata_name).is_file():
            names.append(payload_metadata_name)
    for candidate in names:
        if candidate and candidate not in seen:
            print(candidate)
            seen.add(candidate)
PY
)
promoted_file_count="$(array_count promoted_file_names)"

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  initialize_windows_only_publication_transaction
  reconcile_discovered_windows_only_publication_transactions
  prepare_windows_only_publication_targets
  prepare_windows_only_publication_transaction_record
  if to_bool "${CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_PREPARED_RECORD:-false}"; then
    echo "Injected exit after the durable Windows-only prepared transaction record." >&2
    exit 95
  fi
  replay_windows_only_registry_prepare
  activate_windows_only_publication_targets
  verify_deployed_windows_only_publication_shelf
else
  mkdir -p "$DEPLOY_DIR/files"
  find "$DEPLOY_DIR/files" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
     -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
     -name "chummer-avalonia-*-payload.zip.json" -o \
     -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" -o \
     -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
     -name "chummer-6-*-installer.deb" -o \
     -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
     -name "chummer-6-*-installer.msix" -o -name "chummer-6-*-payload.zip" -o \
     -name "chummer-6-*-payload.zip.json" \) \
    -delete

  while IFS= read -r -d '' file_name; do
    source_path="$artifact_sync_source_dir/$file_name"
    if [[ ! -f "$source_path" ]]; then
      echo "promoted artifact missing from bundle source: $source_path" >&2
      exit 1
    fi
    cp "$source_path" "$DEPLOY_DIR/files/"
  done < <(array_values_nul promoted_file_names)
  sync_release_build_provenance_namespace "$DEPLOY_DIR"
  materialize_aur_sidecar
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] && (( live_downloads_mirror_dir_count > 0 )); then
  while IFS= read -r -d '' mirror_dir; do
    sync_live_downloads_mirror_dir "$mirror_dir" "public-edge"
  done < <(array_values_nul live_downloads_mirror_dirs)
fi

if [[ -d "$STARTUP_SMOKE_SOURCE" && "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  verified_startup_smoke_tmp="$(mktemp)"
  if ! python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$STARTUP_SMOKE_SOURCE" "$DEPLOY_DIR/files" >"$verified_startup_smoke_tmp" <<'PY'
import os
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PASSING_STATUSES = {"pass", "passed", "ready"}
INSTALL_MEDIA_KINDS = {"installer", "dmg", "pkg", "msix"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
PUBLIC_SKIP_STARTUP_SMOKE_FILTER = (
    str(os.environ.get("CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)
ALLOW_SKIPPED_STARTUP_SMOKE = (
    str(os.environ.get("CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)

release_channel_path = Path(sys.argv[1])
startup_smoke_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = payload.get("artifacts") or []
errors: list[str] = []
verified_receipts: list[str] = []
seen: set[str] = set()

def normalize(value: Any) -> str:
    return str(value or "").strip().lower()

def expected_host_class_platform_tokens(platform: str) -> tuple[str, ...]:
    normalized = normalize(platform)
    if normalized == "windows":
        return ("win", "windows")
    if normalized == "macos":
        return ("osx", "macos")
    if normalized == "linux":
        return ("linux",)
    return (normalized,) if normalized else ()

def host_class_matches_platform(host_class: str, platform: str, operating_system: str = "") -> bool:
    normalized_host = normalize(host_class)
    normalized_os = normalize(operating_system)
    expected_tokens = expected_host_class_platform_tokens(platform)
    if not normalized_host or not expected_tokens:
        if normalize(platform) == "windows":
            return "windows" in normalized_os
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    if any(token in host_tokens for token in expected_tokens):
        return True
    if normalize(platform) == "windows":
        return "windows" in normalized_os and "wine" in normalized_host
    return False

def rid_to_arch(rid: str) -> str:
    token = normalize(rid)
    if token.startswith("win-") or token.startswith("linux-") or token.startswith("osx-"):
        _, _, arch = token.partition("-")
        return arch
    return token

def is_windows_incompatible_host_skip(receipt: dict[str, Any], platform: str, rid: str) -> bool:
    if normalize(receipt.get("status")) != "skipped":
        return False
    if normalize(platform) != "windows" and not normalize(rid).startswith("win-"):
        return False
    verification_disposition = normalize(receipt.get("verificationDisposition"))
    skip_class = normalize(receipt.get("skipClass"))
    return verification_disposition == "incompatible_host" or skip_class == "incompatible_host"

def parse_iso_utc(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)

for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    kind = normalize(artifact.get("kind"))
    if kind not in INSTALL_MEDIA_KINDS:
        continue
    head = normalize(artifact.get("head"))
    platform = normalize(artifact.get("platform"))
    rid = normalize(artifact.get("rid"))
    file_name = str(artifact.get("fileName") or "").strip()
    if not head or not platform or not rid or not file_name:
        errors.append(f"promoted install-medium artifact is missing required tuple fields (head/platform/rid/fileName): {artifact}")
        continue
    receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
    if receipt_name in seen:
        continue
    seen.add(receipt_name)
    receipt_path = startup_smoke_root / receipt_name
    if not receipt_path.is_file():
        errors.append(f"startup-smoke receipt missing for promoted install medium {head}/{platform}/{rid}: {receipt_name}")
        continue
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # pragma: no cover - shell guard
        errors.append(f"startup-smoke receipt is unreadable for promoted install medium {head}/{platform}/{rid}: {receipt_path} ({exc})")
        continue
    status = normalize(receipt.get("status"))
    incompatible_host_skip = is_windows_incompatible_host_skip(receipt, platform, rid)
    if status not in PASSING_STATUSES:
        if incompatible_host_skip or (ALLOW_SKIPPED_STARTUP_SMOKE and status == "skipped"):
            verified_receipts.append(str(receipt_path))
        else:
            errors.append(f"startup-smoke receipt status is not passing for promoted install medium {head}/{platform}/{rid}: {status or 'missing'}")
    checkpoint = normalize(receipt.get("readyCheckpoint"))
    if not incompatible_host_skip and checkpoint != "pre_ui_event_loop":
        errors.append(f"startup-smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted install medium {head}/{platform}/{rid}.")
    receipt_head = normalize(receipt.get("headId"))
    receipt_platform = normalize(receipt.get("platform"))
    receipt_arch = normalize(receipt.get("arch"))
    receipt_rid = normalize(receipt.get("rid"))
    receipt_host_class = normalize(receipt.get("hostClass"))
    receipt_operating_system = str(receipt.get("operatingSystem") or "").strip()
    expected_arch = rid_to_arch(rid)
    if receipt_head != head:
        errors.append(f"startup-smoke receipt headId mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_head or 'missing'}")
    if receipt_platform != platform:
        errors.append(f"startup-smoke receipt platform mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_platform or 'missing'}")
    if not incompatible_host_skip:
        if not receipt_host_class:
            errors.append(f"startup-smoke receipt hostClass is missing for promoted install medium {head}/{platform}/{rid}.")
        elif not host_class_matches_platform(receipt_host_class, platform, receipt_operating_system):
            errors.append(f"startup-smoke receipt hostClass does not identify the {platform} host for promoted install medium {head}/{platform}/{rid}.")
        if not receipt_operating_system:
            errors.append(f"startup-smoke receipt operatingSystem is missing for promoted install medium {head}/{platform}/{rid}.")
    if expected_arch and receipt_arch != expected_arch:
        errors.append(f"startup-smoke receipt arch mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_arch or 'missing'}")
    if not receipt_rid:
        errors.append(f"startup-smoke receipt rid is missing for promoted install medium {head}/{platform}/{rid}.")
    elif receipt_rid != rid:
        errors.append(f"startup-smoke receipt rid mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_rid}")
    promoted_file_path = files_root / file_name
    expected_sha = normalize(artifact.get("sha256"))
    if promoted_file_path.is_file():
        expected_sha = hashlib.sha256(promoted_file_path.read_bytes()).hexdigest().lower()
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    receipt_digest = normalize(receipt.get("artifactDigest"))
    if expected_digest and receipt_digest != expected_digest:
        errors.append(f"startup-smoke receipt artifactDigest mismatch for promoted install medium {head}/{platform}/{rid}.")
    recorded_at_raw = (
        receipt.get("completedAtUtc")
        or receipt.get("recordedAtUtc")
        or receipt.get("startedAtUtc")
    )
    recorded_at = parse_iso_utc(recorded_at_raw)
    if recorded_at is None:
        errors.append(
            f"startup-smoke receipt timestamp is missing/invalid for promoted install medium {head}/{platform}/{rid}."
        )
    else:
        now_utc = datetime.now(timezone.utc)
        age_delta_seconds = int((now_utc - recorded_at).total_seconds())
        if age_delta_seconds < 0:
            future_skew_seconds = abs(age_delta_seconds)
            if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                errors.append(
                    "startup-smoke receipt timestamp is in the future for promoted install medium "
                    f"{head}/{platform}/{rid}: {future_skew_seconds}s ahead (max {STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS}s)."
                )
        elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS and not PUBLIC_SKIP_STARTUP_SMOKE_FILTER:
            errors.append(
                "startup-smoke receipt is stale for promoted install medium "
                f"{head}/{platform}/{rid}: {age_delta_seconds}s old (max {STARTUP_SMOKE_MAX_AGE_SECONDS}s)."
            )
    verified_receipts.append(str(receipt_path))

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    raise SystemExit(1)

for verified in sorted(verified_receipts):
    print(verified)
PY
  then
    rm -f "$verified_startup_smoke_tmp"
    exit 1
  fi
  verified_startup_smoke_receipts=()
  while IFS= read -r receipt_path; do
    verified_startup_smoke_receipts+=("$receipt_path")
  done <"$verified_startup_smoke_tmp"
  rm -f "$verified_startup_smoke_tmp"

  if ! python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$DEPLOY_DIR/releases.json" \
    --startup-smoke-dir "$STARTUP_SMOKE_SOURCE" \
    --files-dir "$DEPLOY_DIR/files" >/dev/null
  then
    exit 1
  fi

  startup_smoke_deploy_dir="$DEPLOY_DIR/startup-smoke"
  startup_smoke_stage_dir="$(mktemp -d)"
  startup_smoke_deploy_dir_real="$(resolve_path_allow_missing "$startup_smoke_deploy_dir")"
  deploy_files_dir_real="$(resolve_path_allow_missing "$DEPLOY_DIR/files")"
  mkdir -p "$startup_smoke_deploy_dir"
  startup_smoke_fallback_dir="$PORTAL_DOWNLOADS_DIR/startup-smoke"
  run_services_startup_smoke_dir="$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/startup-smoke"
  python3 - "$startup_smoke_stage_dir" "$startup_smoke_deploy_dir_real" "$deploy_files_dir_real" "$release_channel" "$release_version" "$startup_smoke_fallback_dir" "$run_services_startup_smoke_dir" "${verified_startup_smoke_receipts[@]}" <<'PY'
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

stage_root = Path(sys.argv[1])
final_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])
release_channel = str(sys.argv[4]).strip()
release_version = str(sys.argv[5]).strip()
fallback_roots = [Path(item) for item in sys.argv[6:8] if str(item).strip()]
receipt_paths = [Path(item) for item in sys.argv[8:]]


def resolve_companion(source_root: Path, value: object) -> Path | None:
    raw = str(value or "").strip()
    if not raw:
        return None

    token = Path(raw)
    candidates: list[Path] = []
    if token.is_absolute():
        candidates.append(token)
    else:
        candidates.append(source_root / token)
    candidates.append(source_root / token.name)
    for fallback_root in fallback_roots:
        candidates.append(fallback_root / token.name)

    seen: set[Path] = set()
    for candidate in candidates:
        candidate = candidate.resolve(strict=False)
        if candidate in seen:
            continue
        seen.add(candidate)
        if candidate.is_file():
            return candidate
    return None


def copy_companion(source_root: Path, value: object) -> str:
    source_path = resolve_companion(source_root, value)
    if source_path is None:
        return ""

    stage_path = stage_root / source_path.name
    final_path = final_root / source_path.name
    if source_path.resolve() != stage_path.resolve():
        shutil.copy2(source_path, stage_path)
    return str(final_path)


def rewrite_install_verification(stage_verification_path: Path, source_root: Path) -> None:
    payload = json.loads(stage_verification_path.read_text(encoding="utf-8-sig"))
    for key in (
        "dpkgLogPath",
        "installedLaunchCapturePath",
        "wrapperCapturePath",
        "desktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied
    stage_verification_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


for receipt_path in receipt_paths:
    source_root = receipt_path.parent
    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

    if release_channel:
        payload["channelId"] = release_channel
        payload["channel"] = release_channel
    if release_version:
        payload["releaseVersion"] = release_version
        payload["version"] = release_version

    verification_dest = copy_companion(source_root, payload.get("artifactInstallVerificationPath"))
    if verification_dest:
        payload["artifactInstallVerificationPath"] = verification_dest
        rewrite_install_verification(stage_root / Path(verification_dest).name, source_root)

    for key in (
        "artifactInstallDpkgLogPath",
        "artifactInstallLaunchCapturePath",
        "artifactInstallWrapperCapturePath",
        "artifactInstallDesktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied

    artifact_name = Path(str(payload.get("artifactPath") or "").strip()).name
    if artifact_name:
        published_artifact = files_root / artifact_name
        if published_artifact.is_file():
            payload["artifactPath"] = str(published_artifact)

    (stage_root / receipt_path.name).write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )
PY
  find "$startup_smoke_deploy_dir" -maxdepth 1 -type f \( \
    -name "startup-smoke-*.receipt.json" -o \
    -name "install-verification-*.json" -o \
    -name "dpkg-*.log" -o \
    -name "installed-launch-*" -o \
    -name "installed-wrapper-*" -o \
    -name "installed-desktop-entry-*" -o \
    -name "windows-installer-progress-*.log" \
  \) -exec rm -f -- {} +
  if find "$startup_smoke_stage_dir" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp "$startup_smoke_stage_dir"/* "$startup_smoke_deploy_dir"/
  fi
  if [[ -d "$STARTUP_SMOKE_SOURCE" ]] && find "$STARTUP_SMOKE_SOURCE" -maxdepth 1 -type f -name 'windows-installer-progress-*.log' | grep -q .; then
    cp -f "$STARTUP_SMOKE_SOURCE"/windows-installer-progress-*.log "$startup_smoke_deploy_dir"/
  fi
  rm -rf "$startup_smoke_stage_dir"
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  refresh_release_build_handoff "$DEPLOY_DIR"
fi
verify_windows_desktop_exit_gate

CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$DEPLOY_DIR"

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" && -n "$LIVE_VERIFY_TARGET" ]]; then
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$LIVE_VERIFY_TARGET"
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  publication_receipt_output="$DEPLOY_DIR/PUBLICATION_SCOPE.generated.json"
fi
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  if to_bool "${CHUMMER_WINDOWS_ONLY_INJECT_EXIT_BEFORE_ACTIVATION_JOURNAL:-false}"; then
    echo "Injected exit after target activation and before the durable activation journal." >&2
    exit 94
  fi
  journal_args=(
    journal-activate
    --transaction-id "$WINDOWS_ONLY_TRANSACTION_ID"
    --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL"
    --proof-dir "$WINDOWS_ONLY_TRANSACTION_PROOF_DIR"
    --prepared-record "$WINDOWS_ONLY_TRANSACTION_PREPARED"
    --publication-receipt "$publication_receipt_output"
    --current-receipt "$publication_receipt_current"
  )
  for activation_receipt in "${windows_only_transaction_activation_receipts[@]}"; do
    journal_args+=(--activation-receipt "$activation_receipt")
  done
  python3 "$SCRIPT_DIR/windows_only_publication_transaction.py" "${journal_args[@]}" >/dev/null
fi
scope_args=(
  --output "$publication_receipt_output"
  --deploy-dir "$DEPLOY_DIR"
  --release-version "$release_version"
  --release-channel "$release_channel"
  --promoted-artifact-count "$promoted_file_count"
)
if to_bool "$DEPLOY_MODE"; then
  scope_args+=(--deploy-mode)
fi
if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  scope_args+=(--live-verify-target "$LIVE_VERIFY_TARGET")
fi
if to_bool "$REQUIRE_EXTERNAL_PUBLISH"; then
  scope_args+=(--require-external-publish)
fi
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  scope_args+=(
    --windows-publication-scope "$WINDOWS_ONLY_PUBLICATION_STAGE_ROOT/PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json"
    --windows-activation-journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL"
    --abort-output "$publication_abort_output"
  )
  if [[ -n "${WINDOWS_RUN_UPLOAD_RECEIPT_PATH}${WINDOWS_RUN_UPLOAD_RECEIPT_SHA256}${WINDOWS_RUN_UPLOAD_API_ORIGIN}${WINDOWS_RUN_UPLOAD_SESSION_ID}" ]]; then
    if [[ "${windows_only_transaction_run_versions[0]}" != "$release_version" ]]; then
      echo "Frozen Run upload candidate version differs from the publication version." >&2
      exit 1
    fi
    scope_args+=(
      --frozen-canonical-manifest-sha256 "${windows_only_transaction_run_manifest_sha256s[0]}"
      --frozen-inventory-sha256 "${windows_only_transaction_run_inventory_sha256s[0]}"
      --frozen-file-count "${windows_only_transaction_run_file_counts[0]}"
      --frozen-total-bytes "${windows_only_transaction_run_total_bytes[0]}"
    )
    [[ -z "$WINDOWS_RUN_UPLOAD_RECEIPT_PATH" ]] || scope_args+=(--run-upload-receipt "$WINDOWS_RUN_UPLOAD_RECEIPT_PATH")
    [[ -z "$WINDOWS_RUN_UPLOAD_RECEIPT_SHA256" ]] || scope_args+=(--expected-run-upload-receipt-sha256 "$WINDOWS_RUN_UPLOAD_RECEIPT_SHA256")
    [[ -z "$WINDOWS_RUN_UPLOAD_API_ORIGIN" ]] || scope_args+=(--expected-run-api-origin "$WINDOWS_RUN_UPLOAD_API_ORIGIN")
    [[ -z "$WINDOWS_RUN_UPLOAD_SESSION_ID" ]] || scope_args+=(--expected-run-session-id "$WINDOWS_RUN_UPLOAD_SESSION_ID")
  fi
  if [[ -n "${WINDOWS_HUB_POSTDEPLOY_RECEIPT_PATH}${WINDOWS_HUB_POSTDEPLOY_RECEIPT_SHA256}" ]]; then
    [[ -z "$WINDOWS_HUB_POSTDEPLOY_RECEIPT_PATH" ]] || scope_args+=(--hub-postdeploy-receipt "$WINDOWS_HUB_POSTDEPLOY_RECEIPT_PATH")
    [[ -z "$WINDOWS_HUB_POSTDEPLOY_RECEIPT_SHA256" ]] || scope_args+=(--expected-hub-postdeploy-receipt-sha256 "$WINDOWS_HUB_POSTDEPLOY_RECEIPT_SHA256")
  fi
fi
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  trap '' INT TERM HUP
fi
python3 "$SCRIPT_DIR/materialize-downloads-publication-scope.py" "${scope_args[@]}"
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]]; then
  verify_release_candidate_shelf_invariants \
    "$staged_release_root" \
    "$release_channel" \
    "${transactional_publish_target_dirs[@]}"
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE=1 \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$staged_release_root"
  if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
    verify_candidate_manifest_mac_identity_agreement \
      "$staged_canonical_manifest_path" \
      "$staged_manifest_path" \
      "$staged_release_root/files"
    python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
  fi

  if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    rewrite_release_candidate_stage_paths "$staged_release_root" "$RELEASE_CANDIDATE_OUTPUT_DIR"
    verify_release_candidate_shelf_invariants "$staged_release_root" "$release_channel"
    CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE=1 \
    CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
      bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$staged_release_root"
    if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
      verify_candidate_manifest_mac_identity_agreement \
        "$staged_canonical_manifest_path" \
        "$staged_manifest_path" \
        "$staged_release_root/files"
      python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
    fi
    atomically_publish_release_candidate_stage_only \
      "$staged_release_root" \
      "$RELEASE_CANDIDATE_OUTPUT_DIR"
    printf 'release_candidate_stage_only=pass\n'
    printf 'release_candidate_stage_only_path=%s\n' "$RELEASE_CANDIDATE_OUTPUT_DIR"
    exit 0
  fi

  while IFS= read -r -d '' target_dir; do
    preflight_managed_release_target "$target_dir"
    preflight_release_build_provenance_target "$target_dir"
  done < <(array_values_nul transactional_publish_target_dirs)
  transaction_validator="-"
  if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
    transaction_validator="$BUILD_PROVENANCE_VALIDATOR_RESOLVED"
  fi
  transactionally_publish_release_candidate \
    "$staged_release_root" \
    "$transaction_validator" \
    "${transactional_publish_target_dirs[@]}"
  DEPLOY_DIR="$final_deploy_dir"
  PORTAL_DOWNLOADS_DIR="$final_portal_downloads_dir"
  if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
    CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
    CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
      bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$LIVE_VERIFY_TARGET"
  fi
fi
if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  if to_bool "${CHUMMER_WINDOWS_ONLY_INJECT_EXIT_BEFORE_COMMIT_MARKER:-false}"; then
    echo "Injected exit before the Windows-only durable commit marker." >&2
    exit 97
  fi
  python3 "$SCRIPT_DIR/windows_only_publication_transaction.py" journal-commit \
    --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
    --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" >/dev/null
  if to_bool "${CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_COMMIT_MARKER:-false}"; then
    echo "Injected exit after the Windows-only durable commit marker." >&2
    exit 98
  fi
  python3 "$SCRIPT_DIR/windows_only_publication_transaction.py" install-current \
    --journal "$WINDOWS_ONLY_TRANSACTION_JOURNAL" \
    --commit "$WINDOWS_ONLY_TRANSACTION_COMMIT" >/dev/null
  trap - INT TERM HUP
fi

if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then
  echo "Activated the verified Windows-only shelf locally; authoritative Hub convergence is not enrolled."
elif to_bool "$DEPLOY_MODE"; then
  echo "Published ${promoted_file_count} desktop artifact(s) through verified external downloads lane: $LIVE_VERIFY_TARGET"
else
  echo "Updated local downloads shelf with ${promoted_file_count} desktop artifact(s): $DEPLOY_DIR"
fi
