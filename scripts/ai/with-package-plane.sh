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

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
source "$script_dir/_env.sh"

repo_root="$REPO_ROOT"
repo_root_physical="$(cd "$repo_root" && pwd -P)"
cd "$repo_root"
published_feed_sources="${CHUMMER_PUBLISHED_FEED_SOURCES:-}"
published_feed_root="${CHUMMER_PUBLISHED_FEED_ROOT:-}"
published_nuget_config="${CHUMMER_PUBLISHED_NUGET_CONFIG:-}"
published_nuget_config_sha256="${CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256:-}"
published_feed_sha256="${CHUMMER_PUBLISHED_FEED_SHA256:-}"
strict_package_cache_parent="${CHUMMER_STRICT_PACKAGE_CACHE_PARENT:-}"
verify_mode="${CHUMMER_VERIFY_MODE:-slice}"
use_local_compatibility_tree="${CHUMMER_USE_LOCAL_COMPATIBILITY_TREE:-0}"
configured_contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-}"
configured_run_contracts_version="${CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION:-}"
configured_hub_registry_contracts_version="${CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION:-}"
contracts_version="${configured_contracts_version:-5.225.0}"
campaign_contracts_version="${CHUMMER_CAMPAIGN_CONTRACTS_PACKAGE_VERSION:-0.1.0-preview}"
run_contracts_version="${configured_run_contracts_version:-0.1.0-preview}"
hub_registry_contracts_version="${configured_hub_registry_contracts_version:-0.1.0-preview}"
ui_kit_version="${CHUMMER_UI_KIT_PACKAGE_VERSION:-0.1.0-preview}"
core_runtime_version="${CHUMMER_CORE_RUNTIME_PACKAGE_VERSION:-0.1.0-preview}"
bootstrap_engine_contracts_feed="${CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED:-1}"

workspace_root="$(cd "$repo_root_physical/.." && pwd -P)"
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
engine_contracts_root="$(dirname "$contracts_project")/.."
if [[ "$use_local_compatibility_tree" == "1" ]]; then
  engine_contracts_root="$(cd "$engine_contracts_root" && pwd -P)"
fi
engine_contracts_bootstrap_script="${CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_SCRIPT:-$engine_contracts_root/scripts/ai/bootstrap-contracts-feed.sh}"
owner_contracts_bootstrap_script="$engine_contracts_root/scripts/ai/bootstrap-owner-contracts-feed.py"
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

case "$verify_mode" in
  scaffold|slice|integration|release)
    ;;
  *)
    echo "CHUMMER_VERIFY_MODE must be scaffold, slice, integration, or release." >&2
    exit 2
    ;;
esac

case "$use_local_compatibility_tree" in
  0|1)
    ;;
  *)
    echo "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE must be exactly 0 or 1." >&2
    exit 2
    ;;
esac

if [[ -n "$published_feed_sources" && "$use_local_compatibility_tree" == "1" ]]; then
  echo "choose exactly one package authority: CHUMMER_PUBLISHED_FEED_SOURCES or CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1." >&2
  exit 2
fi

if [[ "$verify_mode" == "integration" || "$verify_mode" == "release" ]]; then
  if [[ "$use_local_compatibility_tree" != "0" ]]; then
    echo "$verify_mode mode forbids the local compatibility tree." >&2
    exit 2
  fi
  if [[ -z "$published_feed_root" || -z "$published_nuget_config" || -z "$published_nuget_config_sha256" || -z "$published_feed_sha256" ]]; then
    echo "$verify_mode mode requires an exact NuGet.Config, feed root, and both authority digests." >&2
    exit 2
  fi
  if [[ -n "$published_feed_sources" && "$published_feed_sources" != "$published_feed_root" ]]; then
    echo "$verify_mode mode rejects CHUMMER_PUBLISHED_FEED_SOURCES that differs from the validated feed root." >&2
    exit 2
  fi
  python3 "$script_dir/verify_mode_contract.py" validate-feed-authority \
    --config "$published_nuget_config" \
    --feed-root "$published_feed_root" \
    --config-sha256 "$published_nuget_config_sha256" \
    --feed-sha256 "$published_feed_sha256"
  for argument in "${dotnet_args[@]}"; do
    normalized_argument="${argument,,}"
    case "$normalized_argument" in
      @*|--configfile|--configfile=*|--source|--source=*|-s|-s:*|/s:*|\
      *restoresources=*|*restoreadditionalprojectsources=*|\
      *restoreconfigfile=*|*restorefallbackfolders=*|*restoreignorefailedsources=*|\
      *restorepackagespath=*|\
      *baseintermediateoutputpath=*|*intermediateoutputpath=*|*outputpath=*|\
      *custombefore*props=*|*customafter*props=*|\
      *custombefore*targets=*|*customafter*targets=*|\
      *directorybuildpropspath=*|*directorybuildtargetspath=*|\
      *import*=*|*msbuildextensionspath*=*|*msbuildprojectextensionspath=*|\
      *msbuildsdkspath=*|*msbuilduserextensionspath=*|\
      --no-restore|--no-build|--no-dependencies|\
      *restore=false*|*buildprojectreferences=false*|*vstestnobuild=true*)
        echo "$verify_mode mode rejects caller-supplied restore-source/config overrides: $argument" >&2
        exit 2
        ;;
    esac
  done
  while IFS= read -r environment_name; do
    normalized_environment_name="${environment_name,,}"
    case "$normalized_environment_name" in
      custombefore*props|customafter*props|custombefore*targets|customafter*targets|\
      directorybuildpropspath|directorybuildtargetspath|import*|\
      msbuildextensionspath*|msbuildprojectextensionspath|msbuildsdkspath|\
      msbuilduserextensionspath)
        echo "$verify_mode mode rejects ambient MSBuild import/property authority: $environment_name" >&2
        exit 2
        ;;
    esac
  done < <(compgen -e)
  if [[ -z "$strict_package_cache_parent" || "$strict_package_cache_parent" != /* || ! -d "$strict_package_cache_parent" || -L "$strict_package_cache_parent" ]]; then
    echo "$verify_mode mode requires an absolute non-symlink CHUMMER_STRICT_PACKAGE_CACHE_PARENT." >&2
    exit 2
  fi
  strict_package_cache_parent_physical="$(cd "$strict_package_cache_parent" && pwd -P)"
  if [[ "$strict_package_cache_parent_physical" != "$strict_package_cache_parent" ]]; then
    echo "$verify_mode mode requires a physical canonical CHUMMER_STRICT_PACKAGE_CACHE_PARENT." >&2
    exit 2
  fi
  strict_cache_invocation="$(mktemp -d "$strict_package_cache_parent/package-cache.XXXXXXXX")"
  if [[ ! -d "$strict_cache_invocation" || -L "$strict_cache_invocation" || "$(dirname "$strict_cache_invocation")" != "$strict_package_cache_parent" ]]; then
    echo "$verify_mode mode could not create an exact per-invocation package cache." >&2
    exit 2
  fi
  cleanup_strict_cache() {
    local exit_code=$?
    trap - EXIT
    rm -rf -- "$strict_cache_invocation"
    exit "$exit_code"
  }
  trap cleanup_strict_cache EXIT
  export NUGET_PACKAGES="$strict_cache_invocation/nuget-packages"
  mkdir -m 700 "$NUGET_PACKAGES"
  published_feed_sources="$published_feed_root"
  restore_args+=(
    -p:RestoreSources="$published_feed_root"
    -p:RestoreAdditionalProjectSources=
    -p:RestoreConfigFile="$published_nuget_config"
    -p:RestoreFallbackFolders=
    -p:RestoreIgnoreFailedSources=false
  )
elif [[ -n "$published_feed_sources" ]]; then
  restore_args+=(-p:RestoreAdditionalProjectSources="$published_feed_sources" -p:RestoreIgnoreFailedSources=false)
elif [[ "$use_local_compatibility_tree" == "1" ]]; then
  if [[ ! -f "$owner_contracts_bootstrap_script" ]]; then
    echo "missing Core owner-contract package-plane helper: $owner_contracts_bootstrap_script" >&2
    exit 2
  fi

  owner_contracts_package_version="$(
    python3 "$owner_contracts_bootstrap_script" \
      --repo-root "$engine_contracts_root" \
      --print-version
  )" || {
    echo "could not resolve the exact Core owner-contract package version." >&2
    exit 2
  }
  if ! [[ "$owner_contracts_package_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "Core owner-contract package version is not one exact SemVer value: $owner_contracts_package_version" >&2
    exit 2
  fi

  for version_variable in \
    CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION \
    CHUMMER_CONTRACTS_PACKAGE_VERSION \
    CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION \
    CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION; do
    configured_version="${!version_variable:-}"
    if [[ -n "$configured_version" && "$configured_version" != "$owner_contracts_package_version" ]]; then
      echo "$version_variable must equal the exact Core owner-contract package version $owner_contracts_package_version." >&2
      exit 2
    fi
  done
  contracts_version="$owner_contracts_package_version"
  run_contracts_version="$owner_contracts_package_version"
  hub_registry_contracts_version="$owner_contracts_package_version"

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
    echo "explicit local compatibility-tree mode is incomplete; every owner project must exist." >&2
    exit 2
  fi

  # The checked-in lock files describe the published package plane. A local
  # ProjectReference graph has a different dependency shape, so keep its
  # generated lock files under each project's isolated intermediate output
  # instead of rewriting source-authority bytes.
  restore_args+=(
    -p:ChummerUseLocalCompatibilityTree=true
    -p:ChummerUseLockedOwnerContractPackages=true
    '-p:NuGetLockFilePath=$(BaseIntermediateOutputPath)packages.local-tree.lock.json'
  )

  if [[ "$bootstrap_engine_contracts_feed" == "1" ]]; then
    if [[ ! -x "$engine_contracts_bootstrap_script" ]]; then
      echo "missing core contracts bootstrap helper: $engine_contracts_bootstrap_script" >&2
      exit 2
    fi

    CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version" \
      CHUMMER_ENGINE_CONTRACTS_FEED="$engine_contracts_feed_root" \
      bash "$engine_contracts_bootstrap_script" >/dev/null
  fi

  if ! python3 "$owner_contracts_bootstrap_script" \
    --repo-root "$engine_contracts_root" \
    --feed "$engine_contracts_feed_root" \
    --validate-only >/dev/null; then
    echo "Core owner-contract package inventory validation failed." >&2
    exit 2
  fi
  restore_args+=(-p:RestoreAdditionalProjectSources="$engine_contracts_feed_root")
else
  echo "no package authority configured; set CHUMMER_PUBLISHED_FEED_SOURCES to a pinned feed or explicitly set CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1 for non-release development." >&2
  exit 2
fi

restore_args+=(
  -p:ChummerContractsPackageVersion="$contracts_version"
  -p:ChummerCampaignContractsPackageVersion="$campaign_contracts_version"
  -p:ChummerRunContractsPackageVersion="$run_contracts_version"
  -p:ChummerHubRegistryContractsPackageVersion="$hub_registry_contracts_version"
  -p:ChummerUiKitPackageVersion="$ui_kit_version"
  -p:ChummerCoreRuntimePackageVersion="$core_runtime_version"
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

if [[ "$use_local_compatibility_tree" == "1" ]] && [[ "$should_prebuild_local_owners" == "1" ]]; then
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
else
  dotnet "${dotnet_args[@]}" "${restore_args[@]}"
fi

if [[ "$verify_mode" == "integration" || "$verify_mode" == "release" ]]; then
  python3 "$script_dir/verify_mode_contract.py" validate-feed-authority \
    --config "$published_nuget_config" \
    --feed-root "$published_feed_root" \
    --config-sha256 "$published_nuget_config_sha256" \
    --feed-sha256 "$published_feed_sha256"
fi
