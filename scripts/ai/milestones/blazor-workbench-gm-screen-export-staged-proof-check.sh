#!/usr/bin/env bash
set -euo pipefail

script_dir_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root_physical="$(cd "$script_dir_physical/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi

python3 "$repo_root/scripts/materialize-blazor-workbench-gm-screen-export-staged-proof.py"
