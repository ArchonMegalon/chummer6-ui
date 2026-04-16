#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

mapfile -t tracked_paths < <(
  git ls-files .codex-studio | grep -vE '^\.codex-studio/published/(QUEUE|WORKPACKAGES)\.generated\.yaml$' || true
)

if (( ${#tracked_paths[@]} > 0 )); then
  echo "[codex-studio-tracking] FAIL: only .codex-studio/published/QUEUE.generated.yaml and WORKPACKAGES.generated.yaml may be tracked."
  printf ' - %s\n' "${tracked_paths[@]}"
  exit 1
fi

echo "[codex-studio-tracking] pass"
