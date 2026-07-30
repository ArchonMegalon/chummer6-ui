#!/usr/bin/env bash
set +x
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
AUTHORITATIVE_PUBLISHER="$REPO_ROOT/../chummer.run-services/scripts/publish-download-bundle-http.sh"

if [[ ! -f "$AUTHORITATIVE_PUBLISHER" || -L "$AUTHORITATIVE_PUBLISHER" ]]; then
  echo "The governed HTTP release publisher is unavailable or unsafe: $AUTHORITATIVE_PUBLISHER" >&2
  echo "Run this lane from the connected Chummer workspace; no legacy direct-upload fallback is permitted." >&2
  exit 1
fi

if [[ "$AUTHORITATIVE_PUBLISHER" -ef "${BASH_SOURCE[0]}" ]]; then
  echo "Refusing recursive HTTP release publisher delegation." >&2
  exit 1
fi

exec bash "$AUTHORITATIVE_PUBLISHER" "$@"
