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
bootstrap_engine_contracts_feed="${CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED:-1}"

workspace_root="$(cd "$repo_root/.." && pwd)"
package_plane_lock_root="${CHUMMER_PACKAGE_PLANE_LOCK_ROOT:-$workspace_root/.tmp/ai}"
package_plane_lock_file="${CHUMMER_PACKAGE_PLANE_LOCK_FILE:-$package_plane_lock_root/with-package-plane.lock}"

if [[ "${CHUMMER_PACKAGE_PLANE_SERIALIZE:-1}" == "1" ]] && [[ -z "${CHUMMER_PACKAGE_PLANE_LOCK_HELD:-}" ]]; then
  if command -v flock >/dev/null 2>&1; then
    mkdir -p "$package_plane_lock_root"
    export CHUMMER_PACKAGE_PLANE_LOCK_HELD=1
    exec flock --exclusive --close "$package_plane_lock_file" "${BASH:-bash}" "$0" "$@"
  fi
fi

contracts_project="$workspace_root/chummer-core-engine/Chummer.Contracts/Chummer.Contracts.csproj"
engine_contracts_bootstrap_script="$workspace_root/chummer-core-engine/scripts/ai/bootstrap-contracts-feed.sh"
engine_contracts_feed_root="${CHUMMER_ENGINE_CONTRACTS_FEED:-$workspace_root/chummer-core-engine/.tmp/ai/local-nuget}"
campaign_contracts_project="$workspace_root/chummer.run-services/Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj"
play_contracts_project="$workspace_root/chummer.run-services/Chummer.Play.Contracts/Chummer.Play.Contracts.csproj"
run_contracts_project="$workspace_root/chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj"
hub_registry_contracts_project="$workspace_root/chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj"
ui_kit_project="$workspace_root/chummer-ui-kit/src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj"
media_contracts_project="$workspace_root/fleet/repos/chummer-media-factory/src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj"

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

  if [[ "$bootstrap_engine_contracts_feed" == "1" ]]; then
    if [[ ! -x "$engine_contracts_bootstrap_script" ]]; then
      echo "missing core contracts bootstrap helper: $engine_contracts_bootstrap_script" >&2
      exit 2
    fi

    CHUMMER_ENGINE_CONTRACTS_FEED="$engine_contracts_feed_root" \
      bash "$engine_contracts_bootstrap_script" >/dev/null
    restore_args+=(-p:RestoreAdditionalProjectSources="$engine_contracts_feed_root")
  fi
fi

restore_args+=(
  -p:ChummerContractsPackageVersion="$contracts_version"
  -p:ChummerCampaignContractsPackageVersion="$campaign_contracts_version"
  -p:ChummerRunContractsPackageVersion="$run_contracts_version"
  -p:ChummerHubRegistryContractsPackageVersion="$hub_registry_contracts_version"
  -p:ChummerUiKitPackageVersion="$ui_kit_version"
)

ensure_ref_assembly() {
  local project_path="$1"
  local ref_path="$2"

  if [[ -f "$ref_path" ]]; then
    return
  fi

  dotnet build "$project_path" --nologo -v minimal -m:1 "${restore_args[@]}" >/dev/null
}

should_prebuild_local_owners=0
case "${1:-}" in
  build|test|publish)
    should_prebuild_local_owners=1
    ;;
  run)
    should_prebuild_local_owners=1
    for arg in "$@"; do
      if [[ "$arg" == "--no-build" ]]; then
        should_prebuild_local_owners=0
        break
      fi
    done
    ;;
esac

if [[ -z "$published_feed_sources" ]] && [[ "$should_prebuild_local_owners" == "1" ]]; then
  ensure_ref_assembly \
    "$contracts_project" \
    "$workspace_root/chummer-core-engine/Chummer.Contracts/obj/Debug/net10.0/ref/Chummer.Engine.Contracts.dll"
  ensure_ref_assembly \
    "$hub_registry_contracts_project" \
    "$workspace_root/chummer-hub-registry/Chummer.Hub.Registry.Contracts/obj/Debug/net10.0/ref/Chummer.Hub.Registry.Contracts.dll"
  ensure_ref_assembly \
    "$play_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Play.Contracts/obj/Debug/net10.0/ref/Chummer.Play.Contracts.dll"
  ensure_ref_assembly \
    "$campaign_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Campaign.Contracts/obj/Debug/net10.0/ref/Chummer.Campaign.Contracts.dll"

  if [[ -f "$media_contracts_project" ]]; then
    ensure_ref_assembly \
      "$media_contracts_project" \
      "$workspace_root/fleet/repos/chummer-media-factory/src/Chummer.Media.Contracts/obj/Debug/net10.0/ref/Chummer.Media.Contracts.dll"
  fi

  ensure_ref_assembly \
    "$run_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Run.Contracts/obj/Debug/net10.0/ref/Chummer.Run.Contracts.dll"
  ensure_ref_assembly \
    "$ui_kit_project" \
    "$workspace_root/chummer-ui-kit/src/Chummer.Ui.Kit/obj/Debug/net10.0/ref/Chummer.Ui.Kit.dll"
fi

dotnet "$@" "${restore_args[@]}"
