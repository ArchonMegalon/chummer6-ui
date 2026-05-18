#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_env.sh"

declare -a args=("$@")
is_solution_build=0
has_parallelism_override=0

normalize_projectish_args() {
  local index
  for index in "${!args[@]}"; do
    case "${args[$index]}" in
      *.csproj|*.fsproj|*.vbproj|*.sln|*.slnx)
        if [[ -e "${args[$index]}" ]]; then
          args[$index]="$(realpath "${args[$index]}")"
        fi
        ;;
    esac
  done
}

normalize_projectish_args

for arg in "${args[@]}"; do
  case "$arg" in
    *.sln|*.slnx)
      is_solution_build=1
      ;;
    -m|-m:*|-maxcpucount|-maxcpucount:*|/m|/m:*|/maxcpucount|/maxcpucount:*|--maxcpucount|--maxcpucount=*)
      has_parallelism_override=1
      ;;
  esac
done

if [[ "$is_solution_build" -eq 1 ]] && [[ "$has_parallelism_override" -eq 0 ]]; then
  # The presentation solution intermittently fails under the default parallel scheduler in sandboxed runs.
  exec "$SCRIPT_DIR/with-package-plane.sh" build "${args[@]}" --nologo -m:1
fi

exec "$SCRIPT_DIR/with-package-plane.sh" build "${args[@]}" --nologo
