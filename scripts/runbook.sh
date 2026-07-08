#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
SCRIPT_DIR="$REPO_ROOT/scripts"

if [[ -z "${COMPOSE_FILE:-}" ]]; then
  COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"
  if [[ "${CHUMMER_RUNBOOK_INCLUDE_LOCAL_COMPOSE_OVERRIDE:-0}" == "1" && -f "$REPO_ROOT/docker-compose.override.yml" ]]; then
    COMPOSE_FILE="${COMPOSE_FILE}:$REPO_ROOT/docker-compose.override.yml"
  fi
  export COMPOSE_FILE
fi

RUNBOOK_MODE="${RUNBOOK_MODE:-${1:-tunnel}}"
RUNBOOK_ARG_FRAMEWORK="${2:-}"
RUNBOOK_ARG_FILTER="${3:-}"
TUNNEL_CONTAINER="${TUNNEL_CONTAINER:-cloudflared_v2}"
DOCKER_NETWORK="${DOCKER_NETWORK:-arr_net_v2}"
UPSTREAM_PRIMARY="${UPSTREAM_PRIMARY:-http://172.17.0.1:8088}"
UPSTREAM_UI="${UPSTREAM_UI:-http://172.17.0.1:8089}"
UPSTREAM_LEGACY="${UPSTREAM_LEGACY:-http://chummer-web:8080}"
UPSTREAM_UI_SERVICE="${UPSTREAM_UI_SERVICE:-http://chummer-blazor:8080}"
UPSTREAM_HOST_INTERNAL="${UPSTREAM_HOST_INTERNAL:-http://host.docker.internal:8088}"

if ! command -v rg >/dev/null 2>&1; then
  echo "ripgrep (rg) is required for this runbook." >&2
  exit 1
fi

resolve_runbook_log_file() {
  local base_name="$1"
  local uid_suffix
  uid_suffix="$(id -u 2>/dev/null || echo user)"
  local candidates=()
  if [[ -n "${RUNBOOK_LOG_DIR:-}" ]]; then
    candidates+=("${RUNBOOK_LOG_DIR}/${base_name}.${uid_suffix}.log")
  fi
  if [[ -n "${XDG_RUNTIME_DIR:-}" ]]; then
    candidates+=("${XDG_RUNTIME_DIR}/${base_name}.${uid_suffix}.log")
  fi
  if [[ -n "${TMPDIR:-}" ]]; then
    candidates+=("${TMPDIR}/${base_name}.${uid_suffix}.log")
  fi
  candidates+=("$REPO_ROOT/.tmp/${base_name}.${uid_suffix}.log")
  if [[ -n "${HOME:-}" ]]; then
    candidates+=("${HOME}/.cache/chummer/${base_name}.${uid_suffix}.log")
  fi
  candidates+=("$REPO_ROOT/${base_name}.${uid_suffix}.log")

  for candidate in "${candidates[@]}"; do
    local dir
    dir="$(dirname "$candidate")"
    if mkdir -p "$dir" 2>/dev/null && touch "$candidate" 2>/dev/null; then
      echo "$candidate"
      return 0
    fi
  done

  echo "/dev/null"
}

resolve_runbook_dir() {
  local base_name="$1"
  local candidates=()
  if [[ -n "${RUNBOOK_STATE_DIR:-}" ]]; then
    candidates+=("${RUNBOOK_STATE_DIR}/${base_name}")
  fi
  if [[ -n "${XDG_STATE_HOME:-}" ]]; then
    candidates+=("${XDG_STATE_HOME}/chummer/${base_name}")
  fi
  if [[ -n "${XDG_CACHE_HOME:-}" ]]; then
    candidates+=("${XDG_CACHE_HOME}/chummer/${base_name}")
  fi
  candidates+=("$REPO_ROOT/.tmp/${base_name}")
  if [[ -n "${HOME:-}" ]]; then
    candidates+=("${HOME}/.cache/chummer/${base_name}")
  fi

  for candidate in "${candidates[@]}"; do
    if mkdir -p "$candidate" 2>/dev/null && [[ -w "$candidate" ]]; then
      echo "$candidate"
      return 0
    fi
  done

  echo "$REPO_ROOT/.tmp/${base_name}"
}

join_pipe_delimited() {
  local IFS='|'
  printf '%s' "$*"
}

validate_host_port_endpoint() {
  local value="$1"
  local label="$2"
  local host="${value%:*}"
  local port="${value##*:}"
  local port_number=0

  if [[ -z "$host" || -z "$port" || "$host" == "$port" ]]; then
    echo "Invalid ${label} value: '$value' (expected host:port with numeric port 1-65535)." >&2
    return 1
  fi
  if [[ ! "$port" =~ ^[0-9]+$ ]]; then
    echo "Invalid ${label} value: '$value' (expected host:port with numeric port 1-65535)." >&2
    return 1
  fi

  port_number=$((10#$port))
  if (( port_number < 1 || port_number > 65535 )); then
    echo "Invalid ${label} value: '$value' (expected host:port with numeric port 1-65535)." >&2
    return 1
  fi

  return 0
}

is_microsoft_testing_platform_runner() {
  local global_json_path="${1:-$REPO_ROOT/global.json}"
  [[ -f "$global_json_path" ]] || return 1

  python3 - "$global_json_path" <<'PY'
import json
import sys

path = sys.argv[1]
try:
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
except Exception:
    raise SystemExit(1)

runner = payload.get("test", {}).get("runner")
raise SystemExit(0 if runner == "Microsoft.Testing.Platform" else 1)
PY
}

resolve_mtp_test_runner() {
  local project_path="$1"
  local configuration="$2"
  local framework="$3"
  local project_dir
  local project_name
  local candidate

  if [[ "$project_path" != /* ]]; then
    project_path="$REPO_ROOT/$project_path"
  fi

  project_dir="$(cd "$(dirname "$project_path")" && pwd -P)"
  project_name="$(basename "$project_path" .csproj)"

  if [[ -n "$framework" ]]; then
    candidate="$project_dir/bin/$configuration/$framework/$project_name"
    if [[ -f "$candidate" ]]; then
      echo "$candidate"
      return 0
    fi
    candidate="$project_dir/bin/$configuration/$framework/$project_name.exe"
    if [[ -f "$candidate" ]]; then
      echo "$candidate"
      return 0
    fi
  fi

  while IFS= read -r candidate; do
    echo "$candidate"
    return 0
  done < <(find "$project_dir/bin/$configuration" -mindepth 2 -maxdepth 2 -type f \
    \( -name "$project_name" -o -name "$project_name.exe" \) | sort)

  return 1
}

if [[ "$RUNBOOK_MODE" == "migration" ]]; then
  LOOPS="${MIGRATION_LOOPS:-1}"
  LOG_FILE="${RUNBOOK_LOG_FILE:-$(resolve_runbook_log_file migration-loop-runbook)}"
  set +e
  bash scripts/migration-loop.sh "$LOOPS" 2>&1 | tee "$LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== migration failure extract =="
  rg -n "Failed|failed|\\[xUnit.net\\]|error|Test summary|Stack Trace" "$LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "local-tests" ]]; then
  TEST_PROJECT="${TEST_PROJECT:-Chummer.Tests/Chummer.Tests.csproj}"
  TEST_CONFIGURATION="${TEST_CONFIGURATION:-Release}"
  TEST_FRAMEWORK="${TEST_FRAMEWORK:-$RUNBOOK_ARG_FRAMEWORK}"
  TEST_FILTER="${TEST_FILTER:-$RUNBOOK_ARG_FILTER}"
  TEST_MAX_CPU="${TEST_MAX_CPU:-1}"
  TEST_DISABLE_BUILD_SERVERS="${TEST_DISABLE_BUILD_SERVERS:-1}"
  TEST_NO_RESTORE="${TEST_NO_RESTORE:-0}"
  TEST_NO_BUILD="${TEST_NO_BUILD:-0}"
  TEST_NUGET_PREFLIGHT="${TEST_NUGET_PREFLIGHT:-1}"
  TEST_NUGET_SOFT_FAIL="${TEST_NUGET_SOFT_FAIL:-}"
  TEST_NUGET_ENDPOINT="${TEST_NUGET_ENDPOINT:-api.nuget.org:443}"
  TEST_LOG_FILE="${TEST_LOG_FILE:-$(resolve_runbook_log_file chummer-local-tests)}"
  TEST_MTP_DIRECT_RUNNER="${TEST_MTP_DIRECT_RUNNER:-auto}"
  export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$(resolve_runbook_dir dotnet-cli-home)}"
  export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"
  export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
  export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"
  export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="${DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER:-1}"
  export MSBUILDDISABLENODEREUSE="${MSBUILDDISABLENODEREUSE:-1}"
  if [[ -z "${CHUMMER_API_BASE_URL:-}" ]] && command -v docker >/dev/null 2>&1; then
    set +e
    detected_api_binding="$(docker compose port chummer-api 8080 2>/dev/null | tail -n 1)"
    detected_api_status=$?
    set -e
    if [[ "$detected_api_status" -eq 0 && -n "$detected_api_binding" ]]; then
      detected_api_port="${detected_api_binding##*:}"
      if [[ "$detected_api_port" =~ ^[0-9]+$ ]]; then
        export CHUMMER_API_BASE_URL="http://127.0.0.1:${detected_api_port}"
      fi
    fi
  fi
  framework_args=()
  filter_args=()
  restore_args=()
  build_args=()
  disable_build_servers_args=()
  if [[ -z "$TEST_NUGET_SOFT_FAIL" ]]; then
    if [[ "${CI:-}" == "true" || "${CI:-}" == "1" ]]; then
      TEST_NUGET_SOFT_FAIL=0
    else
      TEST_NUGET_SOFT_FAIL=1
    fi
  fi
  if [[ -n "$TEST_FRAMEWORK" ]]; then
    framework_args=(-f "$TEST_FRAMEWORK")
  fi
  if [[ -n "$TEST_FILTER" ]]; then
    filter_args=(--filter "$TEST_FILTER")
  fi
  if [[ "$TEST_NO_RESTORE" == "1" || "$TEST_NO_RESTORE" == "true" || "$TEST_NO_RESTORE" == "TRUE" ]]; then
    restore_args=(--no-restore)
  fi
  if [[ "$TEST_NO_BUILD" == "1" || "$TEST_NO_BUILD" == "true" || "$TEST_NO_BUILD" == "TRUE" ]]; then
    build_args=(--no-build)
  fi
  if [[ "$TEST_DISABLE_BUILD_SERVERS" == "1" || "$TEST_DISABLE_BUILD_SERVERS" == "true" || "$TEST_DISABLE_BUILD_SERVERS" == "TRUE" ]]; then
    disable_build_servers_args=(--disable-build-servers)
  fi
  if [[ "$TEST_NO_RESTORE" != "1" && "$TEST_NO_RESTORE" != "true" && "$TEST_NO_RESTORE" != "TRUE" ]]; then
    if [[ "$TEST_NUGET_PREFLIGHT" == "1" || "$TEST_NUGET_PREFLIGHT" == "true" || "$TEST_NUGET_PREFLIGHT" == "TRUE" ]]; then
      if ! validate_host_port_endpoint "$TEST_NUGET_ENDPOINT" "TEST_NUGET_ENDPOINT"; then
        exit 1
      fi
      host="${TEST_NUGET_ENDPOINT%:*}"
      port="${TEST_NUGET_ENDPOINT##*:}"
      set +e
      python3 - "$host" "$port" <<'PY' >/dev/null 2>&1
import socket
import sys

host = sys.argv[1]
port = int(sys.argv[2])
with socket.create_connection((host, port), timeout=3):
    pass
PY
      preflight_status=$?
      set -e
      if [[ "$preflight_status" -ne 0 ]]; then
        if [[ "$TEST_NUGET_SOFT_FAIL" == "1" || "$TEST_NUGET_SOFT_FAIL" == "true" || "$TEST_NUGET_SOFT_FAIL" == "TRUE" ]]; then
          echo "skipping local-tests due NuGet preflight failure for $TEST_NUGET_ENDPOINT (set TEST_NUGET_SOFT_FAIL=0 to enforce failure)." >&2
          exit 0
        fi
        echo "NuGet preflight failed for $TEST_NUGET_ENDPOINT; set TEST_NO_RESTORE=1 for offline assets or enable outbound network." >&2
        exit 1
      fi
    fi
  fi
  if [[ -n "${CHUMMER_API_BASE_URL:-}" ]]; then
    echo "local-tests using CHUMMER_API_BASE_URL=$CHUMMER_API_BASE_URL"
  fi
  : > "$TEST_LOG_FILE"
  use_mtp_direct_runner=0
  if [[ "$TEST_MTP_DIRECT_RUNNER" == "1" || "$TEST_MTP_DIRECT_RUNNER" == "true" || "$TEST_MTP_DIRECT_RUNNER" == "TRUE" ]]; then
    use_mtp_direct_runner=1
  elif [[ "$TEST_MTP_DIRECT_RUNNER" != "0" && "$TEST_MTP_DIRECT_RUNNER" != "false" && "$TEST_MTP_DIRECT_RUNNER" != "FALSE" ]] \
    && is_microsoft_testing_platform_runner "$REPO_ROOT/global.json"; then
    use_mtp_direct_runner=1
  fi

  if [[ "$use_mtp_direct_runner" == "1" ]]; then
    runner_args=(--output Normal)
    if [[ -n "$TEST_FILTER" ]]; then
      runner_args=(--filter "$TEST_FILTER" "${runner_args[@]}")
    fi

    if [[ "$TEST_NO_BUILD" != "1" && "$TEST_NO_BUILD" != "true" && "$TEST_NO_BUILD" != "TRUE" ]]; then
      echo "local-tests using Microsoft.Testing.Platform direct runner"
      set +e
      dotnet build "$TEST_PROJECT" -c "$TEST_CONFIGURATION" "${framework_args[@]}" "${restore_args[@]}" "${disable_build_servers_args[@]}" -v minimal 2>&1 | tee -a "$TEST_LOG_FILE"
      status=${PIPESTATUS[0]}
      set -e
      if [[ "$status" -ne 0 ]]; then
        echo
        echo "== local test failure extract =="
        rg -n "^\\s*Failed\\s|\\[xUnit.net\\]|Total tests:|Passed!|Failed!|Stack Trace|Error Message" "$TEST_LOG_FILE" | tail -n 200 || true
        exit "$status"
      fi
    fi

    TEST_RUNNER_PATH="$(resolve_mtp_test_runner "$TEST_PROJECT" "$TEST_CONFIGURATION" "$TEST_FRAMEWORK")"
    if [[ -z "$TEST_RUNNER_PATH" || ! -f "$TEST_RUNNER_PATH" ]]; then
      echo "Unable to resolve Microsoft.Testing.Platform runner for $TEST_PROJECT." >&2
      exit 1
    fi

    set +e
    "$TEST_RUNNER_PATH" "${runner_args[@]}" 2>&1 | tee -a "$TEST_LOG_FILE"
    status=${PIPESTATUS[0]}
    set -e
  else
    set +e
    dotnet test --project "$TEST_PROJECT" -c "$TEST_CONFIGURATION" "${framework_args[@]}" "${filter_args[@]}" "${restore_args[@]}" "${build_args[@]}" "${disable_build_servers_args[@]}" --output Normal 2>&1 | tee -a "$TEST_LOG_FILE"
    status=${PIPESTATUS[0]}
    set -e
  fi
  if [[ "$status" -ne 0 ]]; then
    echo
    echo "== local test failure extract =="
    rg -n "^\\s*Failed\\s|\\[xUnit.net\\]|Total tests:|Passed!|Failed!|Stack Trace|Error Message" "$TEST_LOG_FILE" | tail -n 200 || true
  fi
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "focused-presentation-tests" ]]; then
  FOCUSED_TEST_PROJECT="${FOCUSED_TEST_PROJECT:-Chummer.Tests/Chummer.Tests.csproj}"
  FOCUSED_TEST_CONFIGURATION="${FOCUSED_TEST_CONFIGURATION:-Release}"
  FOCUSED_TEST_FRAMEWORK="${FOCUSED_TEST_FRAMEWORK:-net10.0}"
  FOCUSED_TEST_FILE="${FOCUSED_TEST_FILE:-${RUNBOOK_ARG_FRAMEWORK:-}}"
  FOCUSED_TEST_SUPPORT_FILE="${FOCUSED_TEST_SUPPORT_FILE:-${RUNBOOK_ARG_FILTER:-}}"
  FOCUSED_TEST_SUPPORT_FILE2="${FOCUSED_TEST_SUPPORT_FILE2:-${4:-}}"
  FOCUSED_TEST_SUPPORT_FILE3="${FOCUSED_TEST_SUPPORT_FILE3:-${5:-}}"
  FOCUSED_TEST_SUPPORT_FILES="${FOCUSED_TEST_SUPPORT_FILES:-}"
  FOCUSED_TEST_PREREQUISITE_PROJECTS="${FOCUSED_TEST_PREREQUISITE_PROJECTS:-${FOCUSED_TEST_PREREQUISITE_PROJECT:-Chummer.Presentation/Chummer.Presentation.csproj}}"
  FOCUSED_TEST_NO_RESTORE="${FOCUSED_TEST_NO_RESTORE:-1}"
  FOCUSED_TEST_LOG_FILE="${FOCUSED_TEST_LOG_FILE:-$(resolve_runbook_log_file chummer-focused-presentation-tests)}"
  export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$(resolve_runbook_dir dotnet-cli-home)}"
  export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"
  export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
  export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"
  export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="${DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER:-1}"
  export MSBUILDDISABLENODEREUSE="${MSBUILDDISABLENODEREUSE:-1}"

  if [[ -z "$FOCUSED_TEST_FILE" ]]; then
    echo "Usage: bash scripts/runbook.sh focused-presentation-tests Presentation/<TestFile>.cs [Presentation/<SupportFile>.cs ...]" >&2
    exit 1
  fi

  if [[ -z "$FOCUSED_TEST_SUPPORT_FILES" ]]; then
    focused_test_support_args=()
    if [[ -n "$FOCUSED_TEST_SUPPORT_FILE" ]]; then
      focused_test_support_args+=("$FOCUSED_TEST_SUPPORT_FILE")
    fi
    if [[ -n "$FOCUSED_TEST_SUPPORT_FILE2" ]]; then
      focused_test_support_args+=("$FOCUSED_TEST_SUPPORT_FILE2")
    fi
    if [[ -n "$FOCUSED_TEST_SUPPORT_FILE3" ]]; then
      focused_test_support_args+=("$FOCUSED_TEST_SUPPORT_FILE3")
    fi
    if (( $# > 5 )); then
      focused_test_support_args+=("${@:6}")
    fi
    if (( ${#focused_test_support_args[@]} > 0 )); then
      FOCUSED_TEST_SUPPORT_FILES="$(join_pipe_delimited "${focused_test_support_args[@]}")"
    fi
  fi

  focused_test_prerequisite_projects=()
  if [[ -n "$FOCUSED_TEST_PREREQUISITE_PROJECTS" ]]; then
    IFS='|' read -r -a focused_test_prerequisite_projects <<< "$FOCUSED_TEST_PREREQUISITE_PROJECTS"
  fi

  : > "$FOCUSED_TEST_LOG_FILE"
  if (( ${#focused_test_prerequisite_projects[@]} > 0 )); then
    for prerequisite_project in "${focused_test_prerequisite_projects[@]}"; do
      if [[ -z "$prerequisite_project" ]]; then
        continue
      fi

      prerequisite_build_args=(
        dotnet build "$prerequisite_project"
        -c "$FOCUSED_TEST_CONFIGURATION"
        -f "$FOCUSED_TEST_FRAMEWORK"
        -v minimal
        -m:1
        -p:UseSharedCompilation=false
      )
      if [[ "$FOCUSED_TEST_NO_RESTORE" == "1" || "$FOCUSED_TEST_NO_RESTORE" == "true" || "$FOCUSED_TEST_NO_RESTORE" == "TRUE" ]]; then
        prerequisite_build_args+=(--no-restore)
      fi

      set +e
      "${prerequisite_build_args[@]}" 2>&1 | tee -a "$FOCUSED_TEST_LOG_FILE"
      status=${PIPESTATUS[0]}
      set -e
      if [[ "$status" -ne 0 ]]; then
        echo
        echo "== focused presentation prerequisite build failure extract =="
        rg -n "Build FAILED|error CS|error MSB|warning CS|warning MSB" "$FOCUSED_TEST_LOG_FILE" | tail -n 200 || true
        exit "$status"
      fi
    done
  fi

  build_args=(
    dotnet build "$FOCUSED_TEST_PROJECT"
    -c "$FOCUSED_TEST_CONFIGURATION"
    -f "$FOCUSED_TEST_FRAMEWORK"
    -v minimal
    -m:1
    -p:UseSharedCompilation=false
    -p:BuildProjectReferences=false
    "-p:FocusedTestCompileFiles=$FOCUSED_TEST_FILE"
  )
  if [[ "$FOCUSED_TEST_NO_RESTORE" == "1" || "$FOCUSED_TEST_NO_RESTORE" == "true" || "$FOCUSED_TEST_NO_RESTORE" == "TRUE" ]]; then
    build_args+=(--no-restore)
  fi
  if [[ -n "$FOCUSED_TEST_SUPPORT_FILES" ]]; then
    build_args+=("-p:FocusedTestSupportFiles=$FOCUSED_TEST_SUPPORT_FILES")
  fi

  set +e
  "${build_args[@]}" 2>&1 | tee -a "$FOCUSED_TEST_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  if [[ "$status" -ne 0 ]]; then
    echo
    echo "== focused presentation build failure extract =="
    rg -n "Build FAILED|error CS|error MSB|warning CS|warning MSB" "$FOCUSED_TEST_LOG_FILE" | tail -n 200 || true
    exit "$status"
  fi

  TEST_RUNNER_PATH="$(resolve_mtp_test_runner "$FOCUSED_TEST_PROJECT" "$FOCUSED_TEST_CONFIGURATION" "$FOCUSED_TEST_FRAMEWORK")"
  if [[ -z "$TEST_RUNNER_PATH" || ! -f "$TEST_RUNNER_PATH" ]]; then
    echo "Unable to resolve Microsoft.Testing.Platform runner for $FOCUSED_TEST_PROJECT." >&2
    exit 1
  fi

  set +e
  "$TEST_RUNNER_PATH" --output Detailed 2>&1 | tee -a "$FOCUSED_TEST_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  if [[ "$status" -ne 0 ]]; then
    echo
    echo "== focused presentation test failure extract =="
    rg -n "^\\s*Failed\\s|Total:|total:|Passed!|Failed!|Stack Trace|Error Message" "$FOCUSED_TEST_LOG_FILE" | tail -n 200 || true
  fi
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "refresh-local-api" ]]; then
  API_REFRESH_LOG_FILE="${API_REFRESH_LOG_FILE:-$(resolve_runbook_log_file chummer-refresh-local-api)}"
  set +e
  docker compose up -d --build chummer-api 2>&1 | tee "$API_REFRESH_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  if [[ "$status" -ne 0 ]]; then
    echo
    echo "== refresh-local-api failure extract =="
    rg -n "error|failed|denied|exception" "$API_REFRESH_LOG_FILE" | tail -n 200 || true
    exit "$status"
  fi

  detected_api_binding="$(docker compose port chummer-api 8080 2>/dev/null | tail -n 1)"
  detected_api_port="${detected_api_binding##*:}"
  if [[ -z "$detected_api_port" || ! "$detected_api_port" =~ ^[0-9]+$ ]]; then
    echo "Unable to resolve chummer-api published port after rebuild." >&2
    exit 1
  fi

  api_probe_url="http://127.0.0.1:${detected_api_port}/api/info"
  python3 - "$api_probe_url" <<'PY'
import sys
import time
import urllib.error
import urllib.request

url = sys.argv[1]
deadline = time.time() + 60
last_error = None
while time.time() < deadline:
    try:
        with urllib.request.urlopen(url, timeout=3) as response:
            if 200 <= response.status < 500:
                print(url)
                sys.exit(0)
    except Exception as exc:  # pragma: no cover - runbook probe path
        last_error = exc
    time.sleep(1)

print(f"API probe failed for {url}: {last_error}", file=sys.stderr)
sys.exit(1)
PY
  exit 0
fi

if [[ "$RUNBOOK_MODE" == "host-prereqs" ]]; then
  PREREQ_LOG_FILE="${PREREQ_LOG_FILE:-$(resolve_runbook_log_file chummer-host-prereqs)}"
  PREREQ_LOG_DIR="$(dirname "$PREREQ_LOG_FILE")"
  set +e
  PREREQ_LOG_DIR="$PREREQ_LOG_DIR" bash scripts/check-host-gate-prereqs.sh 2>&1 | tee "$PREREQ_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== host prerequisite summary =="
  if [[ "$PREREQ_LOG_FILE" == "/dev/null" ]]; then
    echo "host-prereqs log capture disabled (no writable log path found)."
  else
    rg -n "\\[PASS\\]|\\[FAIL\\]|\\[SKIP\\]|Strict host gates are" "$PREREQ_LOG_FILE" | tail -n 200 || true
  fi
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "desktop-gate" ]]; then
  status=0

  require_path() {
    local path="$1"
    if [[ ! -e "$path" ]]; then
      echo "missing path: $path" >&2
      status=1
    fi
  }

  require_match() {
    local pattern="$1"
    local path="$2"
    if ! rg -q -- "$pattern" "$path"; then
      echo "missing pattern '$pattern' in $path" >&2
      status=1
    fi
  }

  require_no_match() {
    local pattern="$1"
    local path="$2"
    if rg -q -- "$pattern" "$path"; then
      echo "forbidden pattern '$pattern' found in $path" >&2
      status=1
    fi
  }

  require_path "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj"
  require_path "Chummer.Blazor.Desktop/Program.cs"
  require_path "Chummer.Desktop.Runtime/ServiceCollectionDesktopRuntimeExtensions.cs"
  require_path "Chummer.Blazor.Desktop/wwwroot/index.html"
  require_path "scripts/validate-amend-manifests.sh"
  require_path "scripts/run-desktop-startup-smoke.sh"
  require_path "scripts/generate-parity-checklist.sh"
  require_path "scripts/check-host-gate-prereqs.sh"
  require_path "scripts/runbook-strict-host-gates.sh"
  require_path "scripts/publish-download-bundle-http.sh"
  require_path "docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md"

  require_match "Chummer.Blazor.Desktop\\\\Chummer.Blazor.Desktop.csproj" "Chummer.sln"
  require_match "Photino.Blazor" "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj"
  require_match "builder.RootComponents.Add<DesktopAppHost>\\(\"app\"\\)" "Chummer.Blazor.Desktop/Program.cs"
  require_match "AddChummerLocalRuntimeClient" "Chummer.Blazor.Desktop/Program.cs"
  require_match "DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync" "Chummer.Blazor.Desktop/Program.cs"
  require_match "DesktopInstallLinkingRuntime.InitializeForStartupAsync" "Chummer.Blazor.Desktop/Program.cs"
  require_match "CHUMMER_CLIENT_MODE" "Chummer.Desktop.Runtime/ServiceCollectionDesktopRuntimeExtensions.cs"
  require_match "CHUMMER_DESKTOP_CLIENT_MODE" "Chummer.Desktop.Runtime/ServiceCollectionDesktopRuntimeExtensions.cs"
  require_match "CHUMMER_API_BASE_URL" "Chummer.Desktop.Runtime/ServiceCollectionDesktopRuntimeExtensions.cs"
  require_match "Chummer.Blazor.Desktop" "Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs"
  require_match "\\{92C5A638-B7DB-4D42-BC96-C11A063D0EF5\\}\\.Release\\|Any CPU\\.Build\\.0" "Chummer.sln"
  require_match "release-regression" "scripts/run-desktop-startup-smoke.sh"
  require_match "avalonia" "scripts/generate-releases-manifest.sh"
  require_match "chummer-\\*-installer\\.deb" "scripts/generate-releases-manifest.sh"
  require_match "-installer\\.exe" "scripts/generate-releases-manifest.sh"
  require_match "chummer-\\*-installer\\.dmg" "scripts/generate-releases-manifest.sh"
  require_match "Task<ShellBootstrapSnapshot> GetShellBootstrapAsync\\(string\\? rulesetId, CancellationToken ct\\);" "Chummer.Presentation/IChummerClient.cs"
  require_no_match "GetShellBootstrapAsync\\(string\\? rulesetId, CancellationToken ct\\)\\s*\\{" "Chummer.Presentation/IChummerClient.cs"
  require_no_match "GetShellBootstrapAsync\\(string\\? rulesetId, CancellationToken ct\\)\\s*=>" "Chummer.Presentation/IChummerClient.cs"
  require_match "RUNBOOK_MODE\" == \"downloads-manifest\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"host-prereqs\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"downloads-sync\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"downloads-sync-s3\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"downloads-upload-http\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE=publish-latest-nightly" "scripts/runbook.sh"
  require_match "CHUMMER_FORCE_NIGHTLY_PUBLISH" "scripts/runbook.sh"
  require_match "08:00 Europe/Vienna" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"parity-checklist\"" "scripts/runbook.sh"
  require_match "RUNBOOK_MODE\" == \"amend-checksums\"" "scripts/runbook.sh"
  require_match "bash scripts/generate-releases-manifest.sh" "scripts/runbook.sh"
  require_match "bash scripts/generate-parity-checklist.sh" "scripts/runbook.sh"
  require_match "bash scripts/publish-download-bundle.sh" "scripts/runbook.sh"
  require_match "bash scripts/publish-download-bundle-http.sh" "scripts/runbook.sh"
  require_match "bash scripts/publish-download-bundle-s3.sh" "scripts/runbook.sh"
  require_match "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR" "docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md"
  require_match "CHUMMER_PORTAL_DOWNLOADS_S3_URI" "docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md"
  require_match "CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID" "docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md"
  require_match "CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS" "docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md"
  require_match "DOCKER_TESTS_SOFT_FAIL=0" "scripts/runbook-strict-host-gates.sh"
  require_match "TEST_NUGET_SOFT_FAIL=0" "scripts/runbook-strict-host-gates.sh"
  require_match "RUNBOOK_MODE=docker-tests" "scripts/runbook-strict-host-gates.sh"
  require_match "RUNBOOK_MODE=local-tests" "scripts/runbook-strict-host-gates.sh"
  require_match "check-host-gate-prereqs.sh" "scripts/runbook-strict-host-gates.sh"
  require_match "strict host prerequisite gate" "scripts/runbook-strict-host-gates.sh"
  require_match "Strict host gates are" "scripts/check-host-gate-prereqs.sh"
  require_match "\\[PASS\\]" "scripts/check-host-gate-prereqs.sh"
  require_match "\\[FAIL\\]" "scripts/check-host-gate-prereqs.sh"
  require_match "bash scripts/validate-amend-manifests.sh" "scripts/runbook.sh"
  require_match "Docker/Downloads/releases.json" "scripts/generate-releases-manifest.sh"
  require_match "Chummer.Portal/downloads/releases.json" "scripts/generate-releases-manifest.sh"
  if [[ "$status" -ne 0 ]]; then
    echo "desktop-gate checks failed" >&2
    exit "$status"
  fi

  echo "desktop-gate checks passed"
  exit 0
fi

if [[ "$RUNBOOK_MODE" == "desktop-build" ]]; then
  DESKTOP_PROJECT="${DESKTOP_PROJECT:-${RUNBOOK_ARG_FRAMEWORK:-Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj}}"
  DESKTOP_FRAMEWORK="${DESKTOP_FRAMEWORK:-${RUNBOOK_ARG_FILTER:-net10.0}}"
  DESKTOP_LOG_FILE="${DESKTOP_LOG_FILE:-$(resolve_runbook_log_file chummer-desktop-build)}"
  DESKTOP_BUILD_PACKAGE="${DESKTOP_BUILD_PACKAGE:-0}"
  DESKTOP_PUBLISH_DIR="${DESKTOP_PUBLISH_DIR:-}"
  DESKTOP_APP_KEY="${DESKTOP_APP_KEY:-}"
  DESKTOP_RID="${DESKTOP_RID:-}"
  DESKTOP_LAUNCH_TARGET="${DESKTOP_LAUNCH_TARGET:-}"
  DESKTOP_DIST_DIR="${DESKTOP_DIST_DIR:-$REPO_ROOT/dist}"
  DESKTOP_RELEASE_VERSION="${DESKTOP_RELEASE_VERSION:-local}"
  desktop_build_package_requested=0
  if [[ "$DESKTOP_BUILD_PACKAGE" == "1" || "$DESKTOP_BUILD_PACKAGE" == "true" || "$DESKTOP_BUILD_PACKAGE" == "TRUE" ]]; then
    desktop_build_package_requested=1
  elif [[ -n "$DESKTOP_PUBLISH_DIR" || -n "$DESKTOP_APP_KEY" || -n "$DESKTOP_RID" || -n "$DESKTOP_LAUNCH_TARGET" ]]; then
    desktop_build_package_requested=1
  fi

  if [[ "$desktop_build_package_requested" == "1" ]]; then
    if [[ -z "$DESKTOP_PUBLISH_DIR" || -z "$DESKTOP_APP_KEY" || -z "$DESKTOP_RID" || -z "$DESKTOP_LAUNCH_TARGET" ]]; then
      echo "desktop-build packaging mode requires DESKTOP_PUBLISH_DIR, DESKTOP_APP_KEY, DESKTOP_RID, and DESKTOP_LAUNCH_TARGET." >&2
      echo "Use scripts/build-desktop-installer.sh through this wrapper by setting DESKTOP_BUILD_PACKAGE=1 and the required packaging inputs." >&2
      echo "Use the project build path when you only need a compile." >&2
      exit 1
    fi

    set +e
    bash scripts/build-desktop-installer.sh \
      "$DESKTOP_PUBLISH_DIR" \
      "$DESKTOP_APP_KEY" \
      "$DESKTOP_RID" \
      "$DESKTOP_LAUNCH_TARGET" \
      "$DESKTOP_DIST_DIR" \
      "$DESKTOP_RELEASE_VERSION" 2>&1 | tee "$DESKTOP_LOG_FILE"
    status=${PIPESTATUS[0]}
    set -e
    echo
    echo "== desktop packaging extract =="
    rg -n "built installer|Refusing to package public desktop artifacts|Launch target not found|Unsupported installer target RID|ERROR:" "$DESKTOP_LOG_FILE" | tail -n 200 || true
    exit "$status"
  fi

  set +e
  docker compose run --build --rm chummer-tests sh -lc \
    "cd /src && dotnet build '$DESKTOP_PROJECT' -c Release -f '$DESKTOP_FRAMEWORK' --nologo" \
    2>&1 | tee "$DESKTOP_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== desktop build extract =="
  rg -n "Build succeeded|Build FAILED|error CS|error NU|error :" "$DESKTOP_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "amend-checksums" ]]; then
  AMEND_TARGET="${AMEND_TARGET:-${RUNBOOK_ARG_FRAMEWORK:-Docker/Amends}}"
  AMEND_CHECKSUM_LOG_FILE="${AMEND_CHECKSUM_LOG_FILE:-$(resolve_runbook_log_file chummer-amend-checksums)}"
  set +e
  bash scripts/validate-amend-manifests.sh "$AMEND_TARGET" 2>&1 | tee "$AMEND_CHECKSUM_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== amend checksum validation summary =="
  rg -n "Validated|ERROR:" "$AMEND_CHECKSUM_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "parity-checklist" ]]; then
  PARITY_CHECKLIST_LOG_FILE="${PARITY_CHECKLIST_LOG_FILE:-$(resolve_runbook_log_file chummer-parity-checklist)}"
  set +e
  bash scripts/generate-parity-checklist.sh 2>&1 | tee "$PARITY_CHECKLIST_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== parity checklist summary =="
  rg -n "Wrote parity checklist|Summary:" "$PARITY_CHECKLIST_LOG_FILE" | tail -n 50 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-manifest" ]]; then
  MANIFEST_LOG_FILE="${MANIFEST_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-manifest)}"
  set +e
  bash scripts/generate-releases-manifest.sh 2>&1 | tee "$MANIFEST_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== manifest preview =="
  if [[ -f Docker/Downloads/releases.json ]]; then
    cat Docker/Downloads/releases.json
  else
    echo "Docker/Downloads/releases.json not found"
  fi
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-sync" ]]; then
  DOWNLOAD_BUNDLE_DIR="${DOWNLOAD_BUNDLE_DIR:-${RUNBOOK_ARG_FRAMEWORK:-$REPO_ROOT/dist}}"
  DOWNLOAD_DEPLOY_DIR="${DOWNLOAD_DEPLOY_DIR:-${RUNBOOK_ARG_FILTER:-$REPO_ROOT/Docker/Downloads}}"
  DOWNLOADS_SYNC_DEPLOY_MODE="${DOWNLOADS_SYNC_DEPLOY_MODE:-0}"
  DOWNLOADS_SYNC_VERIFY_LINKS="${DOWNLOADS_SYNC_VERIFY_LINKS:-}"
  DOWNLOADS_SYNC_VERIFY_TARGET="${DOWNLOADS_SYNC_VERIFY_TARGET:-${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}}"
  SYNC_LOG_FILE="${SYNC_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-sync)}"
  if [[ -z "$DOWNLOADS_SYNC_VERIFY_LINKS" ]]; then
    if [[ "$DOWNLOADS_SYNC_DEPLOY_MODE" == "1" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "true" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "TRUE" ]]; then
      DOWNLOADS_SYNC_VERIFY_LINKS=true
    else
      DOWNLOADS_SYNC_VERIFY_LINKS=false
    fi
  fi
  if [[ "$DOWNLOADS_SYNC_DEPLOY_MODE" == "1" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "true" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "TRUE" ]]; then
    if [[ -z "$DOWNLOADS_SYNC_VERIFY_TARGET" ]]; then
      echo "downloads-sync deploy mode requires DOWNLOADS_SYNC_VERIFY_TARGET or CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL." >&2
      exit 1
    fi
    export CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED=true
    export CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION=true
    export CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL="$DOWNLOADS_SYNC_VERIFY_TARGET"
  fi
  if [[ "$DOWNLOADS_SYNC_VERIFY_LINKS" == "1" || "$DOWNLOADS_SYNC_VERIFY_LINKS" == "true" || "$DOWNLOADS_SYNC_VERIFY_LINKS" == "TRUE" ]]; then
    export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS=true
  else
    unset CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS || true
  fi
  set +e
  bash scripts/publish-download-bundle.sh "$DOWNLOAD_BUNDLE_DIR" "$DOWNLOAD_DEPLOY_DIR" 2>&1 | tee "$SYNC_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== synced manifest =="
  if [[ -f "$DOWNLOAD_DEPLOY_DIR/releases.json" ]]; then
    cat "$DOWNLOAD_DEPLOY_DIR/releases.json"
  else
    echo "$DOWNLOAD_DEPLOY_DIR/releases.json not found"
  fi
  if [[ "$DOWNLOADS_SYNC_DEPLOY_MODE" == "1" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "true" || "$DOWNLOADS_SYNC_DEPLOY_MODE" == "TRUE" ]]; then
    echo
    echo "== deployment-mode verification summary =="
    rg -n "Verified manifest at|Verified artifact links/files|failed artifact verification" "$SYNC_LOG_FILE" | tail -n 40 || true
  fi
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-sync-s3" ]]; then
  DOWNLOAD_BUNDLE_DIR="${DOWNLOAD_BUNDLE_DIR:-${RUNBOOK_ARG_FRAMEWORK:-$REPO_ROOT/dist}}"
  DOWNLOADS_SYNC_S3_VERIFY_LINKS="${DOWNLOADS_SYNC_S3_VERIFY_LINKS:-true}"
  SYNC_S3_LOG_FILE="${SYNC_S3_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-sync-s3)}"
  if [[ "$DOWNLOADS_SYNC_S3_VERIFY_LINKS" == "1" || "$DOWNLOADS_SYNC_S3_VERIFY_LINKS" == "true" || "$DOWNLOADS_SYNC_S3_VERIFY_LINKS" == "TRUE" ]]; then
    export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS=true
  else
    unset CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS || true
  fi
  set +e
  bash scripts/publish-download-bundle-s3.sh "$DOWNLOAD_BUNDLE_DIR" 2>&1 | tee "$SYNC_S3_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== object storage sync summary =="
  rg -n "Published|Verified manifest|Verified artifact links/files|failed artifact verification|Set CHUMMER|Expected desktop-download-bundle|aws CLI" "$SYNC_S3_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-upload-http" ]]; then
  DOWNLOAD_BUNDLE_DIR="${DOWNLOAD_BUNDLE_DIR:-${RUNBOOK_ARG_FRAMEWORK:-$REPO_ROOT/Chummer.Portal/downloads}}"
  DOWNLOADS_UPLOAD_HTTP_LOG_FILE="${DOWNLOADS_UPLOAD_HTTP_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-upload-http)}"
  set +e
  bash scripts/publish-download-bundle-http.sh "$DOWNLOAD_BUNDLE_DIR" 2>&1 | tee "$DOWNLOADS_UPLOAD_HTTP_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== http upload summary =="
  rg -n "Publishing|Upload accepted|Verified route|Verified manifest|failed with HTTP|Dry run only|Exact live publish command" "$DOWNLOADS_UPLOAD_HTTP_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "publish-latest-nightly" ]]; then
  NIGHTLY_DOWNLOAD_DEPLOY_DIR="${NIGHTLY_DOWNLOAD_DEPLOY_DIR:-${RUNBOOK_ARG_FRAMEWORK:-$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads}}"
  NIGHTLY_PUBLISH_LOG_FILE="${NIGHTLY_PUBLISH_LOG_FILE:-$(resolve_runbook_log_file chummer-publish-latest-nightly)}"
  set +e
  bash scripts/publish-latest-nightly-to-downloads.sh "$NIGHTLY_DOWNLOAD_DEPLOY_DIR" 2>&1 | tee "$NIGHTLY_PUBLISH_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== latest nightly publish summary =="
  rg -n "Publishing latest nightly stage|Target downloads shelf|Redeploying public edge|Published latest nightly" "$NIGHTLY_PUBLISH_LOG_FILE" | tail -n 50 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-verify" ]]; then
  DOWNLOADS_VERIFY_TARGET="${DOWNLOADS_VERIFY_TARGET:-${RUNBOOK_ARG_FRAMEWORK:-${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}}}"
  DOWNLOADS_VERIFY_LINKS="${DOWNLOADS_VERIFY_LINKS:-0}"
  VERIFY_LOG_FILE="${VERIFY_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-verify)}"
  if [[ -z "$DOWNLOADS_VERIFY_TARGET" ]]; then
    echo "Set DOWNLOADS_VERIFY_TARGET, CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL, or pass a URL/path as arg #2." >&2
    exit 1
  fi
  if [[ "$DOWNLOADS_VERIFY_LINKS" == "1" || "$DOWNLOADS_VERIFY_LINKS" == "true" || "$DOWNLOADS_VERIFY_LINKS" == "TRUE" ]]; then
    export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS=true
  else
    unset CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS || true
  fi
  set +e
  bash scripts/verify-releases-manifest.sh "$DOWNLOADS_VERIFY_TARGET" 2>&1 | tee "$VERIFY_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== manifest verification summary =="
  rg -n "Verified manifest|Verified artifact links/files|has no downloads|failed artifact verification|not found|empty" "$VERIFY_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "downloads-smoke" ]]; then
  DOWNLOADS_SMOKE_ROOT="${DOWNLOADS_SMOKE_ROOT:-$REPO_ROOT/.tmp/downloads-smoke}"
  DOWNLOADS_SMOKE_RUN_DIR="${DOWNLOADS_SMOKE_RUN_DIR:-$DOWNLOADS_SMOKE_ROOT/run-$$}"
  DOWNLOADS_SMOKE_BUNDLE_DIR="${DOWNLOADS_SMOKE_BUNDLE_DIR:-$DOWNLOADS_SMOKE_RUN_DIR/bundle}"
  DOWNLOADS_SMOKE_DEPLOY_DIR="${DOWNLOADS_SMOKE_DEPLOY_DIR:-$DOWNLOADS_SMOKE_RUN_DIR/deploy}"
  DOWNLOADS_SMOKE_LOG_FILE="${DOWNLOADS_SMOKE_LOG_FILE:-$(resolve_runbook_log_file chummer-downloads-smoke)}"
  DOWNLOADS_SMOKE_VERSION="${DOWNLOADS_SMOKE_VERSION:-smoke-0001}"
  mkdir -p "$DOWNLOADS_SMOKE_BUNDLE_DIR/files" "$DOWNLOADS_SMOKE_BUNDLE_DIR/startup-smoke" "$DOWNLOADS_SMOKE_DEPLOY_DIR"

  artifact_path="$DOWNLOADS_SMOKE_BUNDLE_DIR/files/chummer-avalonia-linux-x64-installer.deb"
  installer_path="$DOWNLOADS_SMOKE_BUNDLE_DIR/files/chummer-avalonia-win-x64-installer.exe"
  payload_path="$DOWNLOADS_SMOKE_BUNDLE_DIR/files/chummer-avalonia-win-x64-payload.zip"
  payload_metadata_path="${payload_path}.json"
  startup_smoke_dir="$DOWNLOADS_SMOKE_BUNDLE_DIR/startup-smoke"
  printf 'downloads smoke installer (linux deb)\n' > "$artifact_path"
  python3 - "$artifact_path" "$installer_path" "$payload_path" "$payload_metadata_path" "$DOWNLOADS_SMOKE_BUNDLE_DIR/releases.json" "$startup_smoke_dir" "$DOWNLOADS_SMOKE_VERSION" <<'PY'
import hashlib
import json
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path


BOOTSTRAP_METADATA_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"
PUBLIC_BASE_URL = "https://example.invalid/downloads/files"


def sha256_for(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


linux_installer = Path(sys.argv[1])
windows_installer = Path(sys.argv[2])
payload_zip = Path(sys.argv[3])
payload_metadata = Path(sys.argv[4])
manifest_path = Path(sys.argv[5])
startup_smoke_dir = Path(sys.argv[6])
version = sys.argv[7]
published_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")

linux_file_name = linux_installer.name
windows_file_name = windows_installer.name
payload_file_name = payload_zip.name
linux_download_url = f"{PUBLIC_BASE_URL}/{linux_file_name}"
windows_download_url = f"{PUBLIC_BASE_URL}/{windows_file_name}"
payload_download_url = f"{PUBLIC_BASE_URL}/{payload_file_name}"

with zipfile.ZipFile(payload_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("Chummer.Avalonia.exe", b"downloads smoke avalonia binary\n")
    archive.writestr("Samples/Legacy/Soma-Career.chum5", b"downloads smoke sample character\n")

payload_sha256 = sha256_for(payload_zip)
payload_size = payload_zip.stat().st_size

payload_metadata.write_text(
    json.dumps(
        {
            "contractName": "chummer6-ui.windows_bootstrap_payload",
            "fileName": payload_file_name,
            "installerFileName": windows_file_name,
            "downloadUrl": payload_download_url,
            "sha256": payload_sha256,
            "sizeBytes": payload_size,
        },
        indent=2,
    )
    + "\n",
    encoding="utf-8",
)

windows_installer.write_bytes(
    b"downloads smoke bootstrap installer\n"
    + BOOTSTRAP_METADATA_MARKER
    + (
        f"payloadDownloadUrl={payload_download_url}\n"
        f"payloadSha256={payload_sha256}\n"
        f"payloadSizeBytes={payload_size}\n"
    ).encode("utf-8")
)

manifest_path.write_text(
    json.dumps(
        {
            "version": version,
            "channel": "preview",
            "channelId": "preview",
            "status": "published",
            "rolloutState": "preview",
            "publishedAt": published_at,
            "downloads": [
                {
                    "artifactId": "avalonia-linux-x64-installer",
                    "fileName": linux_file_name,
                    "url": linux_download_url,
                    "downloadUrl": linux_download_url,
                    "sha256": sha256_for(linux_installer),
                    "sizeBytes": linux_installer.stat().st_size,
                    "kind": "installer",
                    "platformId": "linux-x64",
                    "head": "avalonia",
                },
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "fileName": windows_file_name,
                    "url": windows_download_url,
                    "downloadUrl": windows_download_url,
                    "sha256": sha256_for(windows_installer),
                    "sizeBytes": windows_installer.stat().st_size,
                    "kind": "installer",
                    "platformId": "win-x64",
                    "head": "avalonia",
                    "installerMode": "bootstrap",
                    "payloadFileName": payload_file_name,
                    "payloadDownloadUrl": payload_download_url,
                    "payloadSha256": payload_sha256,
                    "payloadSizeBytes": payload_size,
                },
            ],
        },
        indent=2,
    )
    + "\n",
    encoding="utf-8",
)

startup_smoke_dir.mkdir(parents=True, exist_ok=True)
for receipt_name, artifact_id, installer_name, artifact_sha256, platform, rid, host_class, operating_system in (
    (
        "startup-smoke-avalonia-linux-x64.receipt.json",
        "avalonia-linux-x64-installer",
        linux_file_name,
        sha256_for(linux_installer),
        "linux",
        "linux-x64",
        "linux-smoke-host",
        "linux",
    ),
    (
        "startup-smoke-avalonia-win-x64.receipt.json",
        "avalonia-win-x64-installer",
        windows_file_name,
        sha256_for(windows_installer),
        "windows",
        "win-x64",
        "windows-smoke-host",
        "windows-11",
    ),
):
    (startup_smoke_dir / receipt_name).write_text(
        json.dumps(
            {
                "status": "pass",
                "readyCheckpoint": "pre_ui_event_loop",
                "headId": "avalonia",
                "head": "avalonia",
                "platform": platform,
                "rid": rid,
                "arch": "x64",
                "artifactId": artifact_id,
                "artifactFileName": installer_name,
                "artifactRelativePath": f"files/{installer_name}",
                "artifactPath": f"files/{installer_name}",
                "artifactDigest": f"sha256:{artifact_sha256}",
                "channelId": "preview",
                "channel": "preview",
                "releaseVersion": version,
                "hostClass": host_class,
                "operatingSystem": operating_system,
                "completedAtUtc": published_at,
                "bootstrapPayloadAcquisitionMode": "download" if platform == "windows" else "",
                "bootstrapPayloadFileName": payload_file_name if platform == "windows" else "",
                "bootstrapPayloadSha256": payload_sha256 if platform == "windows" else "",
                "bootstrapPayloadSizeBytes": payload_size if platform == "windows" else 0,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

(startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
    "\n".join(
        [
            "Bootstrap temp root: C:\\Temp\\chummer-bootstrap",
            f"Payload download target: C:\\Temp\\chummer-bootstrap\\{payload_file_name}",
            "Downloading application files",
            "Downloading application files - 100% at 12.3 MB/s",
            "Verifying payload size",
            "Verifying payload checksum",
            "Extracting application files",
            "Install complete",
        ]
    )
    + "\n",
    encoding="utf-8",
)
PY

  set +e
  {
    echo "== downloads smoke fixture =="
    echo "bundle: $DOWNLOADS_SMOKE_BUNDLE_DIR"
    echo "deploy: $DOWNLOADS_SMOKE_DEPLOY_DIR"
    RUNBOOK_MODE=downloads-sync \
      DOWNLOAD_BUNDLE_DIR="$DOWNLOADS_SMOKE_BUNDLE_DIR" \
      DOWNLOAD_DEPLOY_DIR="$DOWNLOADS_SMOKE_DEPLOY_DIR" \
      CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH=1 \
      DOWNLOADS_SYNC_VERIFY_LINKS=1 \
      bash "$SCRIPT_DIR/runbook.sh"
    sync_status=$?
    RUNBOOK_MODE=downloads-verify \
      DOWNLOADS_VERIFY_TARGET="$DOWNLOADS_SMOKE_DEPLOY_DIR/releases.json" \
      DOWNLOADS_VERIFY_LINKS=1 \
      bash "$SCRIPT_DIR/runbook.sh"
    verify_status=$?
    echo "downloads-smoke sync_status=$sync_status verify_status=$verify_status"
    exit $(( sync_status != 0 || verify_status != 0 ))
  } 2>&1 | tee "$DOWNLOADS_SMOKE_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== downloads smoke summary =="
  rg -n "downloads-smoke sync_status|Verified manifest|Verified artifact links/files|failed artifact verification" "$DOWNLOADS_SMOKE_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "ui-e2e" ]]; then
  UI_E2E_LOG_FILE="${UI_E2E_LOG_FILE:-$(resolve_runbook_log_file chummer-ui-e2e)}"
  export CHUMMER_UI_PLAYWRIGHT=1
  set +e
  bash scripts/e2e-ui.sh 2>&1 | tee "$UI_E2E_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== ui e2e summary =="
  rg -n "running playwright ui e2e|playwright ui e2e failed|ui E2E completed|Timed out waiting|request failed|skipping ui e2e|skipping playwright ui e2e|permission denied while trying to connect to the Docker daemon socket|operation not permitted" "$UI_E2E_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "portal-e2e" ]]; then
  PORTAL_E2E_LOG_FILE="${PORTAL_E2E_LOG_FILE:-$(resolve_runbook_log_file chummer-portal-e2e)}"
  export CHUMMER_PORTAL_PLAYWRIGHT=1
  set +e
  bash scripts/e2e-portal.sh 2>&1 | tee "$PORTAL_E2E_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== portal e2e summary =="
  rg -n "running portal playwright e2e|portal playwright e2e failed|portal e2e completed|skipping portal e2e|skipping portal playwright e2e|permission denied while trying to connect to the Docker daemon socket|operation not permitted" "$PORTAL_E2E_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "docker-tests" ]]; then
  TEST_PROJECT="${TEST_PROJECT:-Chummer.Tests/Chummer.Tests.csproj}"
  TEST_FRAMEWORK="${TEST_FRAMEWORK:-${RUNBOOK_ARG_FRAMEWORK:-net10.0}}"
  TEST_FILTER="${TEST_FILTER:-$RUNBOOK_ARG_FILTER}"
  TEST_LOG_FILE="${TEST_LOG_FILE:-$(resolve_runbook_log_file chummer-docker-tests)}"
  DOCKER_TESTS_BUILD="${DOCKER_TESTS_BUILD:-1}"
  DOCKER_TESTS_SOFT_FAIL="${DOCKER_TESTS_SOFT_FAIL:-}"
  DOCKER_TESTS_PREFLIGHT_LOG="${DOCKER_TESTS_PREFLIGHT_LOG:-$(resolve_runbook_log_file chummer-docker-tests-preflight)}"
  framework_arg=""
  filter_arg=""
  build_arg=""
  if [[ -z "$DOCKER_TESTS_SOFT_FAIL" ]]; then
    if [[ "${CI:-}" == "true" || "${CI:-}" == "1" ]]; then
      DOCKER_TESTS_SOFT_FAIL=0
    else
      DOCKER_TESTS_SOFT_FAIL=1
    fi
  fi
  if [[ -n "$TEST_FRAMEWORK" ]]; then
    framework_arg="-f $TEST_FRAMEWORK"
  fi
  if [[ -n "$TEST_FILTER" ]]; then
    filter_arg="--filter \"$TEST_FILTER\""
  fi
  if [[ "$DOCKER_TESTS_BUILD" == "1" || "$DOCKER_TESTS_BUILD" == "true" || "$DOCKER_TESTS_BUILD" == "TRUE" ]]; then
    build_arg="--build"
  fi
  set +e
  docker ps >"$DOCKER_TESTS_PREFLIGHT_LOG" 2>&1
  preflight_status=$?
  set -e
  if [[ "$preflight_status" -ne 0 ]]; then
    if rg -qi "permission denied while trying to connect to the Docker daemon socket|permission denied while trying to connect to the docker API|operation not permitted" "$DOCKER_TESTS_PREFLIGHT_LOG"; then
      if [[ "$DOCKER_TESTS_SOFT_FAIL" == "1" || "$DOCKER_TESTS_SOFT_FAIL" == "true" || "$DOCKER_TESTS_SOFT_FAIL" == "TRUE" ]]; then
        echo "skipping docker-tests due docker daemon permissions (set DOCKER_TESTS_SOFT_FAIL=0 to enforce failure)." >&2
        cat "$DOCKER_TESTS_PREFLIGHT_LOG" >&2
        exit 0
      fi
    fi
    cat "$DOCKER_TESTS_PREFLIGHT_LOG" >&2
    exit "$preflight_status"
  fi
  set +e
  docker compose run $build_arg --rm chummer-tests sh -lc \
      "cd /src && dotnet test '$TEST_PROJECT' -c Release $framework_arg $filter_arg --logger \"console;verbosity=normal\"" \
    2>&1 | tee "$TEST_LOG_FILE"
  status=${PIPESTATUS[0]}
  set -e
  echo
  echo "== docker test failure extract =="
  rg -n "^\\s*Failed\\s|\\[xUnit.net\\]|Total tests:|Passed!|Failed!|Stack Trace|Error Message|Test Run Failed|permission denied while trying to connect to the Docker daemon socket|permission denied while trying to connect to the docker API|operation not permitted|skipping docker-tests due docker daemon permissions" "$TEST_LOG_FILE" | tail -n 200 || true
  exit "$status"
fi

if [[ "$RUNBOOK_MODE" == "push" ]]; then
  RUNBOOK_PUSH_ENABLE="${RUNBOOK_PUSH_ENABLE:-0}"
  if [[ "$RUNBOOK_PUSH_ENABLE" != "1" && "$RUNBOOK_PUSH_ENABLE" != "true" && "$RUNBOOK_PUSH_ENABLE" != "TRUE" ]]; then
    echo "push mode is disabled by default."
    echo "Set RUNBOOK_PUSH_ENABLE=1 to run an explicit push from this runbook."
    echo "Example: RUNBOOK_MODE=push RUNBOOK_PUSH_ENABLE=1 bash scripts/runbook.sh"
    exit 2
  fi

  git_cmd=(git --git-dir="$REPO_ROOT/.git" --work-tree="$REPO_ROOT")
  RUNBOOK_PUSH_REMOTE="${RUNBOOK_PUSH_REMOTE:-origin}"
  RUNBOOK_PUSH_REF="${RUNBOOK_PUSH_REF:-}"
  BRANCH_NAME="$(${git_cmd[@]} rev-parse --abbrev-ref HEAD)"
  REF_SPEC="${RUNBOOK_PUSH_REF:-$BRANCH_NAME}"

  echo "== push mode =="
  echo "branch: $BRANCH_NAME"
  echo "status: $(${git_cmd[@]} status --short --branch | head -n 1)"
  echo "remote: $RUNBOOK_PUSH_REMOTE"
  echo "refspec: $REF_SPEC"

  "${git_cmd[@]}" push "$RUNBOOK_PUSH_REMOTE" "$REF_SPEC"
  echo "push completed"
  exit "$?"
fi

echo "== docker ps (chummer/cloudflared) =="
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | rg -i 'chummer|cloudflared' || true

echo
echo "== recent cloudflared config/events for chummer (24h) =="
docker logs --since 24h "$TUNNEL_CONTAINER" 2>&1 \
  | rg -n 'Updated to new configuration|chummer\.girschele\.com|originService=.*chummer|lookup chummer-web|Unable to reach the origin service' -i \
  | tail -n 200 || true

echo
echo "== network probe from Docker network: $DOCKER_NETWORK =="
docker run --rm \
  --network "$DOCKER_NETWORK" \
  -e U1="$UPSTREAM_PRIMARY" \
  -e U2="$UPSTREAM_UI" \
  -e U3="$UPSTREAM_LEGACY" \
  -e U4="$UPSTREAM_UI_SERVICE" \
  -e U5="$UPSTREAM_HOST_INTERNAL" \
  busybox sh -lc '
for u in "$U1" "$U2" "$U3" "$U4" "$U5"; do
  echo "--- origin: $u"
  for p in / /api/health /api/info; do
    echo "GET $p"
    wget -qSO- --timeout=3 "$u$p" -O - 2>&1 || true
    echo
  done
  echo
 done
'
