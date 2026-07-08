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

PUBLISH_DIR="${1:?publish directory is required}"
RID="${2:?RID is required}"
APP_KEY="${3:-unknown}"
LAUNCH_TARGET="${4:-unknown}"

if [[ "$RID" != osx-* ]]; then
  echo "macOS packaging preflight skipped for RID $RID."
  exit 0
fi

icon_source="$("$REPO_ROOT/scripts/ensure-macos-icon.sh" "$PUBLISH_DIR" "$REPO_ROOT")"

echo "macOS packaging preflight: app=$APP_KEY rid=$RID launch=$LAUNCH_TARGET icon=$icon_source"
