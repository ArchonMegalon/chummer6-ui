#!/usr/bin/env bash
set -euo pipefail

repo_root="${1:-}"
if [[ -z "$repo_root" ]]; then
  echo "usage: run-workflow-parity-gate-tests.sh <repo-root>" >&2
  exit 2
fi

repo_root="$(cd "$repo_root" && pwd -P)"
cd "$repo_root"
export CHUMMER_REPO_ROOT="$repo_root"

configuration="Release"
framework="net10.0"
test_filter="FullyQualifiedName=Chummer.Tests.Presentation.WorkflowParityGateTests.Menu_dialog_workflows_are_exhaustively_classified|FullyQualifiedName=Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_are_exhaustively_classified|FullyQualifiedName=Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName=Chummer.Tests.Presentation.WorkflowParityGateTests.Menu_dialog_workflows_keep_recursive_parity|FullyQualifiedName=Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity"
test_project="Chummer.Tests/Chummer.Tests.csproj"
test_assembly="Chummer.Tests/bin/$configuration/$framework/Chummer.Tests.dll"
result_dir="$(mktemp -d)"
trx_path="$result_dir/workflow-parity-gate.trx"

cleanup() {
  rm -rf -- "$result_dir"
}
trap cleanup EXIT

/usr/bin/dotnet build "$test_project" \
  --no-restore \
  --no-incremental \
  --framework "$framework" \
  --configuration "$configuration" \
  --nologo \
  --verbosity minimal \
  -p:UseSharedCompilation=false \
  -p:BuildInParallel=false \
  -maxcpucount:1 \
  >/dev/null

if [[ ! -f "$test_assembly" ]]; then
  echo "workflow parity test assembly not found: $test_assembly" >&2
  exit 1
fi

test_exit=0
/usr/bin/dotnet "$test_assembly" \
  --filter "$test_filter" \
  --results-directory "$result_dir" \
  --report-trx \
  --report-trx-filename "$(basename "$trx_path")" \
  --output Normal \
  --no-progress \
  >/dev/null || test_exit=$?

python3 - <<'PY' "$trx_path"
from __future__ import annotations

import os
import stat
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

trx_path = Path(sys.argv[1])
max_bytes = 32 * 1024 * 1024
expected_tests = {
    "Menu_dialog_workflows_are_exhaustively_classified",
    "Legacy_ui_controls_are_exhaustively_classified",
    "Quick_action_roots_are_exhaustively_classified",
    "Menu_dialog_workflows_keep_recursive_parity",
    "Legacy_ui_controls_keep_recursive_parity",
}
expected_class = "Chummer.Tests.Presentation.WorkflowParityGateTests"

if trx_path.is_symlink():
    raise SystemExit(f"workflow parity TRX must not be a symlink: {trx_path}")
try:
    descriptor = os.open(
        trx_path,
        os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0),
    )
except OSError as exc:
    raise SystemExit(f"workflow parity TRX is missing or unreadable: {trx_path}: {exc}") from exc
try:
    before = os.fstat(descriptor)
    if not stat.S_ISREG(before.st_mode):
        raise SystemExit(f"workflow parity TRX is not a regular file: {trx_path}")
    if before.st_size <= 0 or before.st_size > max_bytes:
        raise SystemExit(
            f"workflow parity TRX size is outside 1..{max_bytes} bytes: {before.st_size}"
        )
    chunks: list[bytes] = []
    total = 0
    while True:
        chunk = os.read(descriptor, min(1024 * 1024, max_bytes + 1 - total))
        if not chunk:
            break
        chunks.append(chunk)
        total += len(chunk)
        if total > max_bytes:
            raise SystemExit(f"workflow parity TRX exceeds {max_bytes} bytes")
    after = os.fstat(descriptor)
finally:
    os.close(descriptor)

if (
    before.st_dev != after.st_dev
    or before.st_ino != after.st_ino
    or before.st_size != after.st_size
    or before.st_mtime_ns != after.st_mtime_ns
    or total != after.st_size
):
    raise SystemExit("workflow parity TRX changed while being read")

try:
    root = ET.fromstring(b"".join(chunks))
except ET.ParseError as exc:
    raise SystemExit(f"workflow parity TRX is malformed: {exc}") from exc

namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results = root.findall(".//t:UnitTestResult", namespace)
definition_nodes = root.findall(".//t:UnitTest", namespace)
definition_ids = [
    (definition.attrib.get("id") or "").strip() for definition in definition_nodes
]
if (
    any(not test_id for test_id in definition_ids)
    or len(set(definition_ids)) != len(definition_ids)
):
    raise SystemExit("workflow parity TRX definitions require unique nonblank test IDs")
definitions = dict(zip(definition_ids, definition_nodes))
observed_names = []
outcomes = [(result.attrib.get("outcome") or "").strip() for result in results]
for result in results:
    test_id = (result.attrib.get("testId") or "").strip()
    if not test_id:
        raise SystemExit("workflow parity TRX results require nonblank test IDs")
    definition = definitions.get(test_id)
    method = definition.find("t:TestMethod", namespace) if definition is not None else None
    if method is None:
        raise SystemExit("workflow parity TRX result is missing bound TestMethod metadata")
    method_name = (method.attrib.get("name") or "").strip()
    class_name = (method.attrib.get("className") or "").strip()
    result_name = (result.attrib.get("testName") or "").strip()
    if (
        method_name not in expected_tests
        or class_name != expected_class
        or result_name not in {method_name, f"{expected_class}.{method_name}"}
    ):
        raise SystemExit("workflow parity TRX contains a substituted test identity")
    observed_names.append(method_name)
if set(observed_names) != expected_tests or len(observed_names) != len(expected_tests):
    raise SystemExit(
        "workflow parity TRX does not contain each canonical test exactly once"
    )
if not results or any(outcome != "Passed" for outcome in outcomes):
    raise SystemExit("workflow parity TRX does not contain only passing test results")

summaries = root.findall(".//t:ResultSummary", namespace)
if len(summaries) != 1:
    raise SystemExit("workflow parity TRX must contain exactly one run summary")
summary = summaries[0]
counters_nodes = summary.findall("t:Counters", namespace)
if len(counters_nodes) != 1:
    raise SystemExit("workflow parity TRX must contain exactly one counters node")
counters = dict(counters_nodes[0].attrib)
try:
    total_count = int(counters.get("total", "-1"))
    executed_count = int(counters.get("executed", "-1"))
    passed_count = int(counters.get("passed", "-1"))
except ValueError as exc:
    raise SystemExit("workflow parity TRX counters are not integers") from exc
zero_counter_names = (
    "failed",
    "error",
    "timeout",
    "aborted",
    "inconclusive",
    "notExecuted",
    "notRunnable",
    "disconnected",
    "warning",
)
if (
    summary.attrib.get("outcome") != "Completed"
    or total_count != len(results)
    or executed_count != total_count
    or passed_count != total_count
    or total_count != len(expected_tests)
    or any(name not in counters or counters[name] != "0" for name in zero_counter_names)
):
    raise SystemExit("workflow parity TRX completed-run summary is invalid")
PY

if [[ "$test_exit" -ne 0 ]]; then
  exit "$test_exit"
fi
