#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_env.sh"

repo_root="$(cd "$SCRIPT_DIR/../.." && pwd)"
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

mapfile -t existing_projects < <(dotnet sln "$solution_path" list | tail -n +3 | sed 's/\r$//')

declare -A desired_lookup=()
for project in "${desired_projects[@]}"; do
  desired_lookup["$project"]=1
done

declare -a projects_to_remove=()
for project in "${existing_projects[@]}"; do
  if [[ -n "$project" ]] && [[ -z "${desired_lookup[$project]:-}" ]]; then
    projects_to_remove+=("$project")
  fi
done

if [[ "${#projects_to_remove[@]}" -gt 0 ]]; then
  dotnet sln "$solution_path" remove "${projects_to_remove[@]}"
fi

mapfile -t existing_projects < <(dotnet sln "$solution_path" list | tail -n +3 | sed 's/\r$//')

declare -A existing_lookup=()
for project in "${existing_projects[@]}"; do
  if [[ -n "$project" ]]; then
    existing_lookup["$project"]=1
  fi
done

declare -a projects_to_add=()
for project in "${desired_projects[@]}"; do
  if [[ -z "${existing_lookup[$project]:-}" ]]; then
    projects_to_add+=("$project")
  fi
done

if [[ "${#projects_to_add[@]}" -gt 0 ]]; then
  dotnet sln "$solution_path" add "${projects_to_add[@]}"
fi
