#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
source "$SCRIPT_DIR/_env.sh"

repo_root="$REPO_ROOT"
cd "$repo_root"

solution_path="Chummer.Presentation.sln"
bootstrap_solution="Chummer.sln"

declare -a desired_projects=(
  "Chummer.Presentation/Chummer.Presentation.csproj"
  "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj"
  "Chummer.Avalonia/Chummer.Avalonia.csproj"
  "Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj"
  "Chummer.Blazor/Chummer.Blazor.csproj"
  "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj"
)

if [[ ! -f "$solution_path" ]]; then
  cp -f "$bootstrap_solution" "$solution_path"
fi

collect_solution_projects() {
  dotnet sln "$solution_path" list | tail -n +3 | sed 's/\r$//'
}

array_contains_exact() {
  local needle="$1"
  shift || true

  local candidate=""
  for candidate in "$@"; do
    if [[ "$candidate" == "$needle" ]]; then
      return 0
    fi
  done

  return 1
}

existing_projects=()
while IFS= read -r existing_project; do
  [[ -n "$existing_project" ]] || continue
  existing_projects+=("$existing_project")
done < <(collect_solution_projects)

declare -a projects_to_remove=()
for project in "${existing_projects[@]}"; do
  if [[ -n "$project" ]] && ! array_contains_exact "$project" "${desired_projects[@]}"; then
    projects_to_remove+=("$project")
  fi
done

if [[ "${#projects_to_remove[@]}" -gt 0 ]]; then
  dotnet sln "$solution_path" remove "${projects_to_remove[@]}"
fi

existing_projects=()
while IFS= read -r existing_project; do
  [[ -n "$existing_project" ]] || continue
  existing_projects+=("$existing_project")
done < <(collect_solution_projects)

declare -a projects_to_add=()
for project in "${desired_projects[@]}"; do
  if ! array_contains_exact "$project" "${existing_projects[@]}"; then
    projects_to_add+=("$project")
  fi
done

if [[ "${#projects_to_add[@]}" -gt 0 ]]; then
  dotnet sln "$solution_path" add "${projects_to_add[@]}"
fi
