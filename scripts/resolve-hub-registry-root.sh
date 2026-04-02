#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

declare -a candidates=()

if [[ -n "${CHUMMER_HUB_REGISTRY_ROOT:-}" ]]; then
  candidates+=("${CHUMMER_HUB_REGISTRY_ROOT}")
fi

if [[ -n "${GITHUB_WORKSPACE:-}" ]]; then
  candidates+=("${GITHUB_WORKSPACE}/chummer-hub-registry")
  candidates+=("${GITHUB_WORKSPACE}/g")
fi

candidates+=(
  "${REPO_ROOT}/../chummer-hub-registry"
  "$(cd "${REPO_ROOT}/.." && pwd)/chummer-hub-registry"
  "/docker/chummercomplete/chummer-hub-registry"
)

for candidate in "${candidates[@]}"; do
  [[ -n "${candidate}" ]] || continue
  if [[ -f "${candidate}/scripts/materialize_public_release_channel.py" ]] || [[ -f "${candidate}/scripts/verify_public_release_channel.py" ]]; then
    realpath -m "${candidate}"
    exit 0
  fi
done

echo "Unable to locate chummer-hub-registry. Set CHUMMER_HUB_REGISTRY_ROOT or check out the repo at GITHUB_WORKSPACE/chummer-hub-registry." >&2
exit 1
