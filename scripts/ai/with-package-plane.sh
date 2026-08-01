#!/usr/bin/env bash
set -euo pipefail

if [[ $# -eq 0 ]]; then
  echo "usage: $0 <dotnet-args...>" >&2
  exit 1
fi

declare -a dotnet_args=("$@")
has_produce_reference_assembly_override=0
has_restore_packages_path_override=0
test_project_invocation_dir=""

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$script_dir/_env.sh"

repo_root="$(cd "$script_dir/../.." && pwd)"
cd "$repo_root"
published_feed_sources="${CHUMMER_PUBLISHED_FEED_SOURCES:-}"
# MSBuild treats semicolons in command-line property values as property
# separators even when the shell preserves the value as one argument. Escape
# the documented NuGet source-list separator so a second feed cannot become an
# invalid command-line switch (MSB1006). MSBuild decodes %3B before NuGet sees
# RestoreAdditionalProjectSources.
published_feed_sources_msbuild="${published_feed_sources//;/%3B}"
contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-5.225.0.0}"
campaign_contracts_version="${CHUMMER_CAMPAIGN_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
run_contracts_version="${CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
hub_registry_contracts_version="${CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
ui_kit_version="${CHUMMER_UI_KIT_PACKAGE_VERSION:-0.1.0-preview}"
bootstrap_engine_contracts_feed="${CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED:-1}"

workspace_root="$(cd "$repo_root/.." && pwd)"
package_plane_lock_root="${CHUMMER_PACKAGE_PLANE_LOCK_ROOT:-$workspace_root/.tmp/ai}"
package_plane_lock_file="${CHUMMER_PACKAGE_PLANE_LOCK_FILE:-$package_plane_lock_root/with-package-plane.lock}"
package_plane_lock_wait_seconds="${CHUMMER_PACKAGE_PLANE_LOCK_WAIT_SECONDS:-10}"

if [[ "${CHUMMER_PACKAGE_PLANE_SERIALIZE:-1}" == "1" ]] && [[ -z "${CHUMMER_PACKAGE_PLANE_LOCK_HELD:-}" ]]; then
  if command -v flock >/dev/null 2>&1; then
    mkdir -p "$package_plane_lock_root"
    if ! [[ "$package_plane_lock_wait_seconds" =~ ^[0-9]+$ ]] || [[ "$package_plane_lock_wait_seconds" -lt 1 ]]; then
      package_plane_lock_wait_seconds=10
    fi

    echo "[with-package-plane] waiting for package-plane lock: $package_plane_lock_file"
    exec env CHUMMER_PACKAGE_PLANE_LOCK_HELD=1 \
      flock -w "$package_plane_lock_wait_seconds" -o "$package_plane_lock_file" \
      "$0" "$@"
  fi
fi

contracts_project="${CHUMMER_LOCAL_CONTRACTS_PROJECT:-$workspace_root/chummer-core-engine/Chummer.Contracts/Chummer.Contracts.csproj}"
engine_contracts_root="$(cd "$(dirname "$contracts_project")/.." && pwd)"
engine_contracts_bootstrap_script="${CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_SCRIPT:-$engine_contracts_root/scripts/ai/bootstrap-contracts-feed.sh}"
engine_contracts_feed_root="${CHUMMER_ENGINE_CONTRACTS_FEED:-$engine_contracts_root/.tmp/ai/local-nuget}"
campaign_contracts_project="${CHUMMER_LOCAL_CAMPAIGN_CONTRACTS_PROJECT:-$workspace_root/chummer.run-services/Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj}"
play_contracts_project="${CHUMMER_LOCAL_PLAY_CONTRACTS_PROJECT:-$workspace_root/chummer.run-services/Chummer.Play.Contracts/Chummer.Play.Contracts.csproj}"
run_contracts_project="${CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT:-$workspace_root/chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj}"
hub_registry_contracts_project="${CHUMMER_LOCAL_HUB_REGISTRY_CONTRACTS_PROJECT:-$workspace_root/chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj}"
ui_kit_project="${CHUMMER_LOCAL_UI_KIT_PROJECT:-$workspace_root/chummer-ui-kit/src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj}"
media_contracts_project="${CHUMMER_LOCAL_MEDIA_CONTRACTS_PROJECT:-$workspace_root/fleet/repos/chummer-media-factory/src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj}"
presentation_project="$repo_root/Chummer.Presentation/Chummer.Presentation.csproj"
desktop_runtime_project="$repo_root/Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj"

restore_args=()

if [[ -n "$published_feed_sources" ]]; then
  restore_args+=(-p:RestoreAdditionalProjectSources="$published_feed_sources_msbuild" -p:RestoreIgnoreFailedSources=false)
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
  -p:RestorePackagesPath="$NUGET_PACKAGES"
  -p:ChummerContractsPackageVersion="$contracts_version"
  -p:ChummerCampaignContractsPackageVersion="$campaign_contracts_version"
  -p:ChummerRunContractsPackageVersion="$run_contracts_version"
  -p:ChummerHubRegistryContractsPackageVersion="$hub_registry_contracts_version"
  -p:ChummerUiKitPackageVersion="$ui_kit_version"
)

if [[ -n "${NUGET_PACKAGES:-}" ]]; then
  restore_args+=(-p:RestorePackagesPath="$NUGET_PACKAGES")
fi

prebuild_configuration="${CHUMMER_PACKAGE_PLANE_PREBUILD_CONFIGURATION:-Debug}"
parse_configuration_override=0
for arg in "$@"; do
  case "$arg" in
    -p:ProduceReferenceAssembly=*|/p:ProduceReferenceAssembly=*)
      has_produce_reference_assembly_override=1
      ;;
    -p:RestorePackagesPath=*|/p:RestorePackagesPath=*)
      has_restore_packages_path_override=1
      ;;
  esac

  if [[ "$parse_configuration_override" == "1" ]]; then
    if [[ -n "$arg" ]]; then
      prebuild_configuration="$arg"
    fi
    parse_configuration_override=0
    continue
  fi

  case "$arg" in
    -c|--configuration)
      parse_configuration_override=1
      ;;
    -c:*|/c:*|--configuration=*)
      prebuild_configuration="${arg#*=}"
      prebuild_configuration="${prebuild_configuration#*:}"
      ;;
  esac
done

if [[ "$has_restore_packages_path_override" == "1" ]]; then
  filtered_restore_args=()
  for arg in "${restore_args[@]}"; do
    case "$arg" in
      -p:RestorePackagesPath=*|/p:RestorePackagesPath=*)
        ;;
      *)
        filtered_restore_args+=("$arg")
        ;;
    esac
  done
  restore_args=("${filtered_restore_args[@]}")
fi

ensure_ref_assembly() {
  local project_path="$1"
  local ref_path="$2"
  local configuration="$3"

  if [[ -f "$ref_path" ]]; then
    return
  fi

  dotnet build "$project_path" -c "$configuration" --nologo -v minimal -m:1 -p:ProduceReferenceAssembly=true "${restore_args[@]}" >/dev/null
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
    "$workspace_root/chummer-core-engine/Chummer.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Engine.Contracts.dll" \
    "$prebuild_configuration"
  ensure_ref_assembly \
    "$hub_registry_contracts_project" \
    "$workspace_root/chummer-hub-registry/Chummer.Hub.Registry.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Hub.Registry.Contracts.dll" \
    "$prebuild_configuration"
  ensure_ref_assembly \
    "$play_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Play.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Play.Contracts.dll" \
    "$prebuild_configuration"
  ensure_ref_assembly \
    "$campaign_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Campaign.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Campaign.Contracts.dll" \
    "$prebuild_configuration"

  if [[ -f "$media_contracts_project" ]]; then
    ensure_ref_assembly \
      "$media_contracts_project" \
      "$workspace_root/fleet/repos/chummer-media-factory/src/Chummer.Media.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Media.Contracts.dll" \
      "$prebuild_configuration"
  fi

  ensure_ref_assembly \
    "$run_contracts_project" \
    "$workspace_root/chummer.run-services/Chummer.Run.Contracts/obj/$prebuild_configuration/net10.0/ref/Chummer.Run.Contracts.dll" \
    "$prebuild_configuration"
  ensure_ref_assembly \
    "$ui_kit_project" \
    "$workspace_root/chummer-ui-kit/src/Chummer.Ui.Kit/obj/$prebuild_configuration/net10.0/ref/Chummer.Ui.Kit.dll" \
    "$prebuild_configuration"

  if [[ -f "$presentation_project" ]]; then
    ensure_ref_assembly \
      "$presentation_project" \
      "$repo_root/Chummer.Presentation/obj/$prebuild_configuration/net10.0/ref/Chummer.Presentation.dll" \
      "$prebuild_configuration"
  fi

  if [[ -f "$desktop_runtime_project" ]]; then
    ensure_ref_assembly \
      "$desktop_runtime_project" \
      "$repo_root/Chummer.Desktop.Runtime/obj/$prebuild_configuration/net10.0/ref/Chummer.Desktop.Runtime.dll" \
      "$prebuild_configuration"
  fi
fi

if [[ "${dotnet_args[0]:-}" == "test" ]] \
  && [[ ${#dotnet_args[@]} -ge 2 ]] \
  && [[ "${dotnet_args[1]}" != -* ]] \
  && [[ "${dotnet_args[1]}" == *.csproj || "${dotnet_args[1]}" == *.fsproj || "${dotnet_args[1]}" == *.vbproj || "${dotnet_args[1]}" == *.sln || "${dotnet_args[1]}" == *.slnx ]]; then
  dotnet_args=("test" "--project" "${dotnet_args[1]}" "${dotnet_args[@]:2}")
fi

if [[ "${dotnet_args[0]:-}" == "test" ]]; then
  for (( index=1; index<${#dotnet_args[@]}; index++ )); do
    case "${dotnet_args[$index]}" in
      --project)
        if (( index + 1 < ${#dotnet_args[@]} )); then
          candidate="${dotnet_args[$((index + 1))]}"
          if [[ "$candidate" == *.csproj || "$candidate" == *.fsproj || "$candidate" == *.vbproj ]]; then
            test_project_invocation_dir="$(cd "$(dirname "$candidate")" && pwd)"
            dotnet_args=("${dotnet_args[@]:0:$index}" "${dotnet_args[@]:$((index + 2))}")
          fi
        fi
        break
        ;;
      --project=*)
        candidate="${dotnet_args[$index]#--project=}"
        if [[ "$candidate" == *.csproj || "$candidate" == *.fsproj || "$candidate" == *.vbproj ]]; then
          test_project_invocation_dir="$(cd "$(dirname "$candidate")" && pwd)"
          dotnet_args=("${dotnet_args[@]:0:$index}" "${dotnet_args[@]:$((index + 1))}")
        fi
        break
        ;;
    esac
  done
fi

if [[ "$has_produce_reference_assembly_override" == "0" ]]; then
  case "${dotnet_args[0]:-}" in
    build|publish|run|test)
      dotnet_args+=(-p:ProduceReferenceAssembly=true)
      ;;
  esac
fi

if [[ -n "$test_project_invocation_dir" ]]; then
  (
    cd "$test_project_invocation_dir"
    dotnet "${dotnet_args[@]}" "${restore_args[@]}"
  )
  exit $?
fi

dotnet "${dotnet_args[@]}" "${restore_args[@]}"
