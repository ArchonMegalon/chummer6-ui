#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

tracked_paths=()
while IFS= read -r path; do
  [[ -e "$path" ]] || continue
  tracked_paths+=("$path")
done < <(
  git ls-files .codex-studio | grep -E '^\.codex-studio/(locks/|generated/|tmp/)' || true
)

if (( ${#tracked_paths[@]} > 0 )); then
  echo "[codex-studio-tracking] FAIL: ephemeral .codex-studio lock/generated/tmp artifacts may not be tracked."
  printf ' - %s\n' "${tracked_paths[@]}"
  exit 1
fi

echo "[codex-studio-tracking] pass"
