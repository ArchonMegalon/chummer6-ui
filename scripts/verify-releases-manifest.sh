#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REGISTRY_ROOT="$("$SCRIPT_DIR/resolve-hub-registry-root.sh")"
TARGET="${1:-${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}}"

if [[ -z "${TARGET}" ]]; then
  echo "Provide a portal base URL or manifest path as the first argument (or set CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL)." >&2
  exit 1
fi

if [[ ! -f "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" ]]; then
  echo "Missing registry verifier: $REGISTRY_ROOT/scripts/verify_public_release_channel.py" >&2
  exit 1
fi

python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "$TARGET"
