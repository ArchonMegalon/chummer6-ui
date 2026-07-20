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
REGISTRY_ROOT="$("$SCRIPT_DIR/resolve-hub-registry-root.sh")"
TARGET=""
CLI_REQUIRE_COMPLETE_DESKTOP_COVERAGE=0
CLI_SKIP_STARTUP_SMOKE_FILTER=0

while (( $# > 0 )); do
  case "$1" in
    --require-complete-desktop-coverage)
      CLI_REQUIRE_COMPLETE_DESKTOP_COVERAGE=1
      ;;
    --skip-startup-smoke-filter)
      CLI_SKIP_STARTUP_SMOKE_FILTER=1
      ;;
    --)
      shift
      if (( $# > 1 )); then
        echo "Expected exactly one verification target after --." >&2
        exit 1
      fi
      if (( $# == 1 )); then
        TARGET="$1"
      fi
      break
      ;;
    -*)
      echo "Unknown verification option: $1" >&2
      exit 1
      ;;
    *)
      if [[ -n "$TARGET" ]]; then
        echo "Provide exactly one portal base URL or manifest path." >&2
        exit 1
      fi
      TARGET="$1"
      ;;
  esac
  shift
done

TARGET="${TARGET:-${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}}"

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

if [[ -z "${TARGET}" ]]; then
  echo "Provide a portal base URL or manifest path as the first argument (or set CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL)." >&2
  exit 1
fi

if [[ -d "$TARGET" ]]; then
  normalized_target="${TARGET%/}"
  if [[ "$(basename "$normalized_target")" == "files" ]]; then
    echo "Verification target points at downloads files/ directory: $normalized_target" >&2
    echo "Verify the downloads shelf root or its releases.json manifest, not its files/ child." >&2
    exit 1
  fi

  target_manifest_path="$normalized_target/releases.json"
  if [[ ! -f "$target_manifest_path" ]]; then
    echo "Local downloads shelf directory is missing releases.json: $target_manifest_path" >&2
    exit 1
  fi

  TARGET="$target_manifest_path"
fi

if [[ ! -f "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" ]]; then
  echo "Missing registry verifier: $REGISTRY_ROOT/scripts/verify_public_release_channel.py" >&2
  exit 1
fi

VERIFY_ARGS=()
if (( CLI_REQUIRE_COMPLETE_DESKTOP_COVERAGE == 1 )) || [[ "${CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" != "0" ]]; then
  VERIFY_ARGS+=(--require-complete-desktop-coverage)
fi
if (( CLI_SKIP_STARTUP_SMOKE_FILTER == 1 )) || [[ "${CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER:-${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}}" =~ ^([Tt][Rr][Uu][Ee]|1|[Yy][Ee][Ss]|[Oo][Nn])$ ]]; then
  VERIFY_ARGS+=(--skip-startup-smoke-filter)
fi

verify_arg_count="$(array_count VERIFY_ARGS)"
if (( verify_arg_count > 0 )); then
  python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "${VERIFY_ARGS[@]}" "$TARGET"
else
  python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "$TARGET"
fi
