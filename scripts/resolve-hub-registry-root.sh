#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

resolve_path_allow_missing() {
  python3 - "$1" <<'PY'
import pathlib
import sys

print(pathlib.Path(sys.argv[1]).resolve(strict=False))
PY
}

declare -a candidates=()

if [[ -n "${CHUMMER_HUB_REGISTRY_ROOT:-}" ]]; then
  candidates+=("${CHUMMER_HUB_REGISTRY_ROOT}")
fi

if [[ -n "${GITHUB_WORKSPACE:-}" ]]; then
  candidates+=("${GITHUB_WORKSPACE}/chummer6-hub-registry")
  candidates+=("${GITHUB_WORKSPACE}/chummer-hub-registry")
  candidates+=("${GITHUB_WORKSPACE}/g")
fi

candidates+=(
  "${REPO_ROOT}/../chummer6-hub-registry"
  "${REPO_ROOT}/../chummer-hub-registry"
  "$(cd "${REPO_ROOT}/.." && pwd)/chummer6-hub-registry"
  "$(cd "${REPO_ROOT}/.." && pwd)/chummer-hub-registry"
  "/docker/chummercomplete/chummer6-hub-registry"
  "/docker/chummercomplete/chummer-hub-registry"
)

for candidate in "${candidates[@]}"; do
  [[ -n "${candidate}" ]] || continue
  if [[ -f "${candidate}/scripts/materialize_public_release_channel.py" ]] || [[ -f "${candidate}/scripts/verify_public_release_channel.py" ]]; then
    resolve_path_allow_missing "${candidate}"
    exit 0
  fi
done

echo "Unable to locate chummer-hub-registry/chummer6-hub-registry. Set CHUMMER_HUB_REGISTRY_ROOT or check out one of those repo names under GITHUB_WORKSPACE." >&2
exit 1
