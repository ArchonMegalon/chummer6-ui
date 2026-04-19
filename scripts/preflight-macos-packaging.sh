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

configured="${CHUMMER_MACOS_ICON_SOURCE:-}"
icon_source=""

if [[ -n "$configured" ]]; then
  if [[ ! -f "$configured" ]]; then
    echo "macOS packaging preflight failed: CHUMMER_MACOS_ICON_SOURCE is set but not a file: $configured" >&2
    exit 1
  fi
  icon_source="$configured"
elif [[ -f "$PUBLISH_DIR/chummer.icns" ]]; then
  icon_source="$PUBLISH_DIR/chummer.icns"
elif [[ -f "$REPO_ROOT/Chummer/chummer.icns" ]]; then
  icon_source="$REPO_ROOT/Chummer/chummer.icns"
fi

if [[ -z "$icon_source" ]]; then
  echo "macOS packaging preflight failed: chummer.icns not found for ${APP_KEY}/${RID}." >&2
  echo "Set CHUMMER_MACOS_ICON_SOURCE, place chummer.icns in $PUBLISH_DIR, or add Chummer/chummer.icns." >&2
  exit 1
fi

if [[ "$icon_source" != *.icns ]]; then
  echo "macOS packaging preflight failed: icon source is not an .icns file: $icon_source" >&2
  echo "Set CHUMMER_MACOS_ICON_SOURCE to an .icns file." >&2
  exit 1
fi

echo "macOS packaging preflight: app=$APP_KEY rid=$RID launch=$LAUNCH_TARGET icon=$icon_source"
