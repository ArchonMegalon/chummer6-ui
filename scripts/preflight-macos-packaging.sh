#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

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
