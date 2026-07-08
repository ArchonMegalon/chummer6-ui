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

export CHUMMER_PORTAL_BASE_URL="${CHUMMER_PORTAL_BASE_URL:-https://chummer.run}"
export CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH="${CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json}"

detect_local_playwright() {
  if ! command -v node >/dev/null 2>&1; then
    return 1
  fi

  local candidate
  for candidate in \
    "${NODE_PATH:-}" \
    "${CHUMMER_PLAYWRIGHT_NODE_PATH:-}" \
    "${CHUMMER_PLAYWRIGHT_ROOT:+$CHUMMER_PLAYWRIGHT_ROOT/node_modules}" \
    "$WORKSPACE_ROOT/chummer.run-services/node_modules" \
    "$WORKSPACE_ROOT/node_modules" \
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
