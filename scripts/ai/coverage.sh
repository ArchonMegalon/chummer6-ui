#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
source "$SCRIPT_DIR/_env.sh"

project_path="${1:-Chummer.Tests/Chummer.Tests.csproj}"
shift || true

framework="${CHUMMER_COVERAGE_FRAMEWORK:-net10.0}"
results_root="${CHUMMER_COVERAGE_RESULTS_ROOT:-$REPO_ROOT/.artifacts/coverage}"
summary_json="$results_root/summary.json"
coverage_output="$results_root/coverage.cobertura.xml"
tool_path="${CHUMMER_DOTNET_TOOLS:-$REPO_ROOT/.tmp/ai/tools}"
extra_args=("$@")

mkdir -p "$results_root"
rm -rf "$results_root"/*
mkdir -p "$tool_path"

ensure_dotnet_coverage() {
  if [[ -x "$tool_path/dotnet-coverage" ]]; then
    return
  fi

  echo "[coverage] installing dotnet-coverage into $tool_path"
  dotnet tool install dotnet-coverage --tool-path "$tool_path"
}

echo "[coverage] restore $project_path ($framework)"
bash "$SCRIPT_DIR/restore.sh" "$project_path" -p:TargetFramework="$framework"

ensure_dotnet_coverage
export PATH="$tool_path:$PATH"

echo "[coverage] run $project_path ($framework)"
dotnet-coverage collect \
  --nologo \
  --output-format cobertura \
  --output "$coverage_output" \
  -- \
  bash "$SCRIPT_DIR/test.sh" \
    "$project_path" \
    -f "$framework" \
    --no-restore \
    --results-directory "$results_root" \
    "${extra_args[@]}"

if ! find "$results_root" -name 'coverage.cobertura.xml' -print -quit | grep -q .; then
  echo "[coverage] FAIL: tests ran, but no Cobertura report was emitted." >&2
  echo "[coverage] The MSTest app-host coverage path is still not emitting a report on this host." >&2
  exit 3
fi

python3 "$SCRIPT_DIR/coverage-summary.py" "$results_root" "$summary_json" "$REPO_ROOT"
echo "[coverage] summary written to $summary_json"
