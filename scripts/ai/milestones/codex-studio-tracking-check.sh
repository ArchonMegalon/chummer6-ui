#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
cd "$repo_root"

array_count() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || {
    printf '0\n'
    return 0
  }

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "set -- \"\${${array_name}[@]}\""
  local count="$#"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  printf '%s\n' "$count"
}

tracked_paths=()
while IFS= read -r path; do
  [[ -e "$path" ]] || continue
  tracked_paths+=("$path")
done < <(
  git ls-files .codex-studio | grep -E '^\.codex-studio/(locks/|generated/|tmp/)' || true
)

tracked_path_count="$(array_count tracked_paths)"
if (( tracked_path_count > 0 )); then
  echo "[codex-studio-tracking] FAIL: ephemeral .codex-studio lock/generated/tmp artifacts may not be tracked."
  printf ' - %s\n' "${tracked_paths[@]}"
  exit 1
fi

echo "[codex-studio-tracking] pass"
