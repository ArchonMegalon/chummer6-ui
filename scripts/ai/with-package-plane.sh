#!/usr/bin/env bash
set -euo pipefail

if [[ $# -eq 0 ]]; then
  echo "usage: $0 <dotnet-args...>" >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$script_dir/_env.sh"

repo_root="$(cd "$script_dir/../.." && pwd)"
published_feed_sources="${CHUMMER_PUBLISHED_FEED_SOURCES:-}"
contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-5.225.0.0}"
campaign_contracts_version="${CHUMMER_CAMPAIGN_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
run_contracts_version="${CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
hub_registry_contracts_version="${CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
ui_kit_version="${CHUMMER_UI_KIT_PACKAGE_VERSION:-0.1.0-preview}"

workspace_root="$(cd "$repo_root/.." && pwd)"
contracts_project="$workspace_root/chummer-core-engine/Chummer.Contracts/Chummer.Contracts.csproj"
campaign_contracts_project="$workspace_root/chummer.run-services/Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj"
run_contracts_project="$workspace_root/chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj"
hub_registry_contracts_project="$workspace_root/chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj"
ui_kit_project="$workspace_root/chummer-ui-kit/src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj"

restore_args=()

if [[ -n "$published_feed_sources" ]]; then
  restore_args+=(-p:RestoreAdditionalProjectSources="$published_feed_sources" -p:RestoreIgnoreFailedSources=false)
else
  required_projects=(
    "$contracts_project"
    "$campaign_contracts_project"
    "$run_contracts_project"
    "$hub_registry_contracts_project"
    "$ui_kit_project"
  )

  missing_projects=()
  for project_path in "${required_projects[@]}"; do
    if [[ ! -f "$project_path" ]]; then
      missing_projects+=("$project_path")
    fi
  done

  if (( ${#missing_projects[@]} > 0 )); then
    printf 'missing local compatibility-tree owner projects:\n' >&2
    printf '  %s\n' "${missing_projects[@]}" >&2
    echo "set CHUMMER_PUBLISHED_FEED_SOURCES to published package feeds or mount the sibling compatibility tree so repo-local helpers can pass -p:ChummerUseLocalCompatibilityTree=true explicitly." >&2
    exit 2
  fi

  restore_args+=(-p:ChummerUseLocalCompatibilityTree=true)
fi

restore_args+=(
  -p:ChummerContractsPackageVersion="$contracts_version"
  -p:ChummerCampaignContractsPackageVersion="$campaign_contracts_version"
  -p:ChummerRunContractsPackageVersion="$run_contracts_version"
  -p:ChummerHubRegistryContractsPackageVersion="$hub_registry_contracts_version"
  -p:ChummerUiKitPackageVersion="$ui_kit_version"
)

exec dotnet "$@" "${restore_args[@]}"
