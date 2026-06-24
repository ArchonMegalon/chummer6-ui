#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

export CHUMMER_PORTAL_BASE_URL="${CHUMMER_PORTAL_BASE_URL:-https://chummer.run}"
export CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH="${CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json}"

detect_local_playwright() {
  if ! command -v node >/dev/null 2>&1; then
    return 1
  fi

  local candidate
  for candidate in \
    "${NODE_PATH:-}" \
    "/docker/chummercomplete/chummer.run-services/node_modules" \
    "/docker/chummercomplete/node_modules" \
    "$SCRIPT_DIR/node_modules"
  do
    if [ -z "$candidate" ]; then
      continue
    fi
    if NODE_PATH="$candidate" node -e "require('playwright');" >/dev/null 2>&1; then
      LOCAL_PLAYWRIGHT_NODE_PATH="$candidate"
      return 0
    fi
  done

  return 1
}

if detect_local_playwright; then
  NODE_PATH="$LOCAL_PLAYWRIGHT_NODE_PATH" node "$SCRIPT_DIR/e2e-public-edge-playwright.cjs"
else
  echo "Hosted public-edge execution proof requires Playwright. No local installation was detected." >&2
  exit 1
fi

python3 "$SCRIPT_DIR/verify_blazor_public_edge_execution_proof.py"
