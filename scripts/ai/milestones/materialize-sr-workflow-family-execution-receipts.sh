#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_execution_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_execution_receipt"
    proof_kind="sr6_family_release_gated_execution"
    ;;
  *)
    echo "usage: $0 <sr4|sr6>" >&2
    exit 64
    ;;
esac

python3 - <<'PY' "$edition" "$ledger_path" "$oracle_path" "$repo_root" "$contract_name" "$proof_kind"
from __future__ import annotations

import json
import fcntl
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
import xml.etree.ElementTree as ET

edition = sys.argv[1].strip().lower()
ledger_path = Path(sys.argv[2])
oracle_path = Path(sys.argv[3])
repo_root = Path(sys.argv[4])
contract_name = sys.argv[5].strip()
proof_kind = sys.argv[6].strip().lower()

if not ledger_path.is_file():
    raise SystemExit(f"missing ledger: {ledger_path}")
if not oracle_path.is_file():
    raise SystemExit(f"missing oracle: {oracle_path}")

ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
families = [item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)]

run_root = repo_root / ".codex-studio" / "out" / "workflow-family-parity" / "executed" / edition
run_root.mkdir(parents=True, exist_ok=True)
trx_path = run_root / f"{edition}-workflow-family-execution.trx"
if trx_path.exists():
    trx_path.unlink()
lock_dir = repo_root / ".codex-studio" / "locks"
lock_dir.mkdir(parents=True, exist_ok=True)
lock_path = lock_dir / "workflow-family-dotnet-test.lock"

unique_tests: list[str] = []
for family in families:
    for test_name in family.get("auditTests") or []:
        value = str(test_name).strip()
        if value and value not in unique_tests:
            unique_tests.append(value)

run_error = ""
run_exit = 0
if unique_tests:
    filter_clause = "|".join(f"FullyQualifiedName~{name}" for name in unique_tests)
    cmd = [
        "bash",
        "scripts/ai/test.sh",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--configuration",
        "Release",
        "--filter",
        filter_clause,
        "--results-directory",
        str(run_root),
        "--logger",
        f"trx;LogFileName={trx_path.name}",
        "-v",
        "minimal",
        "-m:1",
    ]
    with lock_path.open("w", encoding="utf-8") as lock_handle:
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_EX)
        proc = subprocess.run(
            cmd,
            cwd=repo_root,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            check=False,
        )
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_UN)
    run_exit = int(proc.returncode)
    if run_exit != 0:
        output = (proc.stdout or "").strip().splitlines()
        run_error = output[-1] if output else "dotnet test failed"

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results_by_name: dict[str, list[str]] = {}
if trx_path.is_file():
    root = ET.fromstring(trx_path.read_text(encoding="utf-8"))
    for node in root.findall(".//t:UnitTestResult", ns):
        test_name = (node.attrib.get("testName") or "").strip()
        outcome = (node.attrib.get("outcome") or "").strip()
        if test_name:
            results_by_name.setdefault(test_name, []).append(outcome)

sr4_oracle_families = {str(value).strip() for value in (oracle.get("workflowFamilies") or []) if str(value).strip()}
sr6_oracle_map = {
    str(item.get("id") or "").strip(): item
    for item in (oracle.get("requiredFamilies") or [])
    if isinstance(item, dict) and str(item.get("id") or "").strip()
}

execution_signal_tokens = (
    "save",
    "workflow",
    "execute",
    "dialog",
    "download",
    "export",
    "print",
    "roundtrip",
    "click",
)
execution_optional_family_ids = {
    "improvements-explain-result-parity",
}

any_fail = False
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue

    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    output_refs = [str(value).strip() for value in (family.get("executionReceipts") or []) if str(value).strip()]
    if not output_refs:
        output_refs = [
            f".codex-studio/published/workflow-family-parity/executed/{edition}/{family_id}.generated.json"
        ]

    reasons = []
    if str(family.get("status") or "").strip().lower() != "ready":
        reasons.append(f"Ledger family is not ready: {family.get('status') or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    elif family_id not in execution_optional_family_ids and not any(
        any(token in test_name.lower() for token in execution_signal_tokens)
        for test_name in audit_tests
    ):
        reasons.append("Audit tests do not include any execution-oriented workflow proof.")

    oracle_detail: dict[str, object] = {}
    if edition == "sr4":
        if family_id not in sr4_oracle_families:
            reasons.append(f"Family is missing from SR4 oracle workflowFamilies: {family_id}")
        source_repo = dict(oracle.get("sourceRepo") or {})
        oracle_detail = {
            "sourceRepoPath": str(source_repo.get("path") or ""),
            "sourceRepoHead": str(source_repo.get("head") or ""),
        }
    else:
        oracle_entry = sr6_oracle_map.get(family_id)
        if not oracle_entry:
            reasons.append(f"Family is missing from SR6 carry-forward oracle requiredFamilies: {family_id}")
        else:
            oracle_detail = {
                "classification": str(oracle_entry.get("classification") or ""),
                "rationale": str(oracle_entry.get("rationale") or ""),
                "releaseGateTests": [str(value).strip() for value in (oracle_entry.get("releaseGateTests") or []) if str(value).strip()],
            }

    if run_exit != 0:
        reasons.append(f"dotnet test execution failed (exit {run_exit}): {run_error or 'see TRX/log output'}")

    missing_tests: list[str] = []
    failed_tests: dict[str, list[str]] = {}
    passed_tests: list[str] = []
    for test_name in audit_tests:
        outcomes: list[str] = []
        for observed_name, observed_outcomes in results_by_name.items():
            if test_name in observed_name:
                outcomes.extend(observed_outcomes)
        if not outcomes:
            missing_tests.append(test_name)
            continue
        lowered = [value.lower() for value in outcomes]
        if any(value not in {"passed", "completed", "passedbutrunaborted"} for value in lowered):
            failed_tests[test_name] = outcomes
        else:
            passed_tests.append(test_name)

    if missing_tests:
        reasons.append("Audit tests not present in executed TRX results: " + ", ".join(missing_tests))
    if failed_tests:
        reasons.append(
            "Audit tests did not pass in executed TRX results: "
            + ", ".join(f"{name}={','.join(values)}" for name, values in sorted(failed_tests.items()))
        )

    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family execution evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family execution evidence is incomplete for {family_id}."
        ),
        "reasons": reasons,
        "evidence": {
            "edition": edition,
            "familyId": family_id,
            "proofKind": proof_kind,
            "ledgerPath": str(ledger_path),
            "oraclePath": str(oracle_path),
            "auditTests": audit_tests,
            "oracle": oracle_detail,
            "dotnetTest": {
                "project": "Chummer.Tests/Chummer.Tests.csproj",
                "configuration": "Release",
                "trxPath": str(trx_path),
                "exitCode": run_exit,
            },
            "matchedPassedTests": passed_tests,
            "missingAuditTests": missing_tests,
            "failedAuditTests": failed_tests,
        },
    }

    for output_ref in output_refs:
        output_ref = output_ref.replace("{familyId}", family_id)
        output_path = Path(output_ref)
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        any_fail = True

if any_fail:
    raise SystemExit(43)
PY

echo "[materialize-${edition}-workflow-family-execution-receipts] PASS"
