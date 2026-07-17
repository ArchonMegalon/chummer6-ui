#!/usr/bin/env bash
set -euo pipefail

repo_root="${1:-}"
if [[ -z "$repo_root" ]]; then
  echo "usage: run-workflow-parity-gate-tests.sh <repo-root>" >&2
  exit 2
fi

cd "$repo_root"
export CHUMMER_REPO_ROOT="${CHUMMER_REPO_ROOT:-$repo_root}"

configuration="${CHUMMER_WORKFLOW_PARITY_GATE_CONFIGURATION:-Debug}"
framework="${CHUMMER_WORKFLOW_PARITY_GATE_FRAMEWORK:-net10.0}"
test_filter="${CHUMMER_WORKFLOW_PARITY_GATE_FILTER:-FullyQualifiedName~WorkflowParityGateTests}"
skip_build="${CHUMMER_WORKFLOW_PARITY_GATE_SKIP_BUILD:-0}"
skip_restore="${CHUMMER_WORKFLOW_PARITY_GATE_SKIP_RESTORE:-0}"
test_project="Chummer.Tests/Chummer.Tests.csproj"
test_host="Chummer.Tests/bin/$configuration/$framework/Chummer.Tests"
test_assembly="Chummer.Tests/bin/$configuration/$framework/Chummer.Tests.dll"

if [[ "$skip_build" != "1" ]]; then
  build_args=(dotnet build "$test_project")
  if [[ "$skip_restore" == "1" ]]; then
    build_args+=(--no-restore)
  fi
  build_args+=(
    --framework "$framework"
    --configuration "$configuration"
    --nologo
    --verbosity minimal
    -p:UseSharedCompilation=false
    -p:BuildInParallel=false
    -maxcpucount:1
  )
  "${build_args[@]}" >/dev/null
fi

if [[ ! -f "$test_assembly" ]]; then
  echo "workflow parity test assembly not found: $test_assembly" >&2
  exit 1
fi

if [[ -x "$test_host" ]]; then
  "$test_host" --filter "$test_filter" --output Normal --no-progress >/dev/null
else
  dotnet "$test_assembly" --filter "$test_filter" --output Normal --no-progress >/dev/null
fi
