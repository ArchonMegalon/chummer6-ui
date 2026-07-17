#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
source "$SCRIPT_DIR/_env.sh"

declare -a args=("$@")
has_parallelism_override=0
test_target_path=""
use_mstest_runner=0

find_mstest_runner_binary() {
  local project_path="$1"
  local configuration="$2"
  local framework="$3"
  local project_dir project_name candidate
  local -a search_roots=()

  project_dir="$(cd "$(dirname "$project_path")" && pwd)"
  project_name="$(basename "$project_path")"
  project_name="${project_name%.*}"

  if [[ -n "$framework" ]]; then
    search_roots+=("$project_dir/bin/$configuration/$framework")
  fi
  search_roots+=("$project_dir/bin/$configuration")

  for search_root in "${search_roots[@]}"; do
    if [[ ! -d "$search_root" ]]; then
      continue
    fi

    while IFS= read -r candidate; do
      case "$(basename "$candidate")" in
        "$project_name"|"$project_name".exe)
          echo "$candidate"
          return 0
          ;;
      esac
    done < <(find "$search_root" -maxdepth 2 -type f \( -perm -111 -o -name '*.exe' \) | sort)
  done

  return 1
}

run_mstest_runner() {
  local project_path="$test_target_path"
  local configuration="Debug"
  local framework=""
  local skip_build=0
  local -a build_args=(build "$project_path")
  local -a runner_args=()
  local index=0
  local target_path=""
  local normalized_project_path=""

  if [[ -n "$project_path" && -e "$project_path" ]]; then
    normalized_project_path="$(realpath "$project_path")"
  else
    normalized_project_path="$project_path"
  fi

  while (( index < ${#args[@]} )); do
    case "${args[$index]}" in
      --project)
        ((index += 2))
        continue
        ;;
      --project=*)
        ((index += 1))
        continue
        ;;
      -c|--configuration)
        if (( index + 1 < ${#args[@]} )); then
          configuration="${args[$((index + 1))]}"
          build_args+=("${args[$index]}" "${args[$((index + 1))]}")
          ((index += 2))
          continue
        fi
        ;;
      -c:*|/c:*|--configuration=*)
        configuration="${args[$index]#*=}"
        configuration="${configuration#*:}"
        build_args+=("${args[$index]}")
        ((index += 1))
        continue
        ;;
      -f|--framework)
        if (( index + 1 < ${#args[@]} )); then
          framework="${args[$((index + 1))]}"
          build_args+=("${args[$index]}" "${args[$((index + 1))]}")
          ((index += 2))
          continue
        fi
        ;;
      -f:*|/f:*|--framework=*)
        framework="${args[$index]#*=}"
        framework="${framework#*:}"
        build_args+=("${args[$index]}")
        ((index += 1))
        continue
        ;;
      -p:*|/p:*|--property:*)
        case "${args[$index]}" in
          -p:ProduceReferenceAssembly=*|/p:ProduceReferenceAssembly=*|--property:ProduceReferenceAssembly=*)
            ;;
          *)
            build_args+=("${args[$index]}")
            ;;
        esac
        ((index += 1))
        continue
        ;;
      --no-build)
        skip_build=1
        ((index += 1))
        continue
        ;;
      --no-restore)
        build_args+=("${args[$index]}")
        ((index += 1))
        continue
        ;;
      test)
        ((index += 1))
        continue
        ;;
    esac

    case "${args[$index]}" in
      *.csproj|*.fsproj|*.vbproj|*.sln|*.slnx)
        local positional_target="${args[$index]}"
        if [[ -e "$positional_target" ]]; then
          positional_target="$(realpath "$positional_target")"
        fi

        if [[ -n "$normalized_project_path" && "$positional_target" == "$normalized_project_path" ]]; then
          ((index += 1))
          continue
        fi
        ;;
    esac

    runner_args+=("${args[$index]}")
    ((index += 1))
  done

  if [[ "$skip_build" -eq 0 ]]; then
    "$SCRIPT_DIR/with-package-plane.sh" "${build_args[@]}"
  fi

  target_path="$(find_mstest_runner_binary "$project_path" "$configuration" "$framework" || true)"
  if [[ -z "$target_path" ]]; then
    echo "unable to locate MSTest runner output for $project_path (configuration=$configuration framework=${framework:-default})" >&2
    return 1
  fi

  (
    cd "$(dirname "$target_path")"
    "./$(basename "$target_path")" "${runner_args[@]}"
  )
}

if [[ ${#args[@]} -gt 0 && "${args[0]}" == "test" ]]; then
  args=("${args[@]:1}")
fi

rewrite_dotnet_test_target_args() {
  local index
  for index in "${!args[@]}"; do
    case "${args[$index]}" in
      -*)
        ;;
      *.sln|*.slnx)
        args=("${args[@]:0:$index}" --solution "${args[$index]}" "${args[@]:$((index + 1))}")
        return
        ;;
      *.csproj|*.fsproj|*.vbproj)
        args=("${args[@]:0:$index}" --project "${args[$index]}" "${args[@]:$((index + 1))}")
        return
        ;;
    esac
  done
}

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

capture_test_target_path() {
  local index
  for index in "${!args[@]}"; do
    case "${args[$index]}" in
      --project|--solution)
        if (( index + 1 < ${#args[@]} )); then
          test_target_path="${args[$((index + 1))]}"
          return
        fi
        ;;
      *.csproj|*.fsproj|*.vbproj|*.sln|*.slnx)
        test_target_path="${args[$index]}"
        return
        ;;
    esac
  done
}

detect_mstest_runner() {
  if [[ -z "$test_target_path" ]]; then
    return
  fi

  case "$test_target_path" in
    *.csproj|*.fsproj|*.vbproj)
      if rg -q '<EnableMSTestRunner>\s*true\s*</EnableMSTestRunner>' "$test_target_path"; then
        use_mstest_runner=1
      fi
      ;;
  esac
}

rewrite_mstest_runner_args() {
  local rewritten=()
  local index=0

  while (( index < ${#args[@]} )); do
    case "${args[$index]}" in
      --logger)
        if (( index + 1 < ${#args[@]} )) && [[ "${args[$((index + 1))]}" =~ ^trx\;LogFileName=(.+)$ ]]; then
          rewritten+=(--report-trx --report-trx-filename "${BASH_REMATCH[1]}")
          ((index += 2))
          continue
        fi
        ;;
      --logger=trx\;LogFileName=*)
        rewritten+=(--report-trx --report-trx-filename "${args[$index]#--logger=trx;LogFileName=}")
        ((index += 1))
        continue
        ;;
      --nologo|--disable-build-servers)
        ((index += 1))
        continue
        ;;
      -v|--verbosity)
        if (( index + 1 < ${#args[@]} )); then
          ((index += 2))
          continue
        fi
        ;;
      -v:*|/v:*|--verbosity=*)
        ((index += 1))
        continue
        ;;
      -m|-m:*|-maxcpucount|-maxcpucount:*|/m|/m:*|/maxcpucount|/maxcpucount:*|--maxcpucount|--maxcpucount=*)
        if [[ "${args[$index]}" == "-m" || "${args[$index]}" == "-maxcpucount" || "${args[$index]}" == "/m" || "${args[$index]}" == "/maxcpucount" || "${args[$index]}" == "--maxcpucount" ]]; then
          ((index += 2))
        else
          ((index += 1))
        fi
        continue
        ;;
    esac

    rewritten+=("${args[$index]}")
    ((index += 1))
  done

  args=("${rewritten[@]}")
}

capture_test_target_path
detect_mstest_runner
normalize_projectish_args

if [[ "$use_mstest_runner" -eq 0 ]]; then
  rewrite_dotnet_test_target_args
fi

if [[ "$use_mstest_runner" -eq 1 ]]; then
  rewrite_mstest_runner_args
fi

for arg in "${args[@]}"; do
  case "$arg" in
    -m|-m:*|-maxcpucount|-maxcpucount:*|/m|/m:*|/maxcpucount|/maxcpucount:*|--maxcpucount|--maxcpucount=*)
      has_parallelism_override=1
      ;;
  esac
done

if [[ "$use_mstest_runner" -eq 1 ]]; then
  run_mstest_runner
  exit $?
fi

if [[ "$has_parallelism_override" -eq 0 ]]; then
  exec "$SCRIPT_DIR/with-package-plane.sh" test "${args[@]}" --nologo --disable-build-servers -m:1
fi

exec "$SCRIPT_DIR/with-package-plane.sh" test "${args[@]}" --nologo --disable-build-servers
