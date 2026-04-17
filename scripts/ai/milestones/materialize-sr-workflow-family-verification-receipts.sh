#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_verification_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_verification_receipt"
    proof_kind="sr6_family_carry_forward"
    ;;
  *)
    echo "usage: $0 <sr4|sr6>" >&2
    exit 64
    ;;
esac

python3 - <<'PY' "$edition" "$ledger_path" "$oracle_path" "$repo_root" "$contract_name" "$proof_kind"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

edition = sys.argv[1].strip().lower()
ledger_path = Path(sys.argv[2])
oracle_path = Path(sys.argv[3])
repo_root = Path(sys.argv[4])
contract_name = sys.argv[5].strip()
proof_kind = sys.argv[6].strip().lower()
expected_execution_proof_kind = "sr4_family_oracle" if edition == "sr4" else "sr6_family_release_gated_execution"

if not ledger_path.is_file():
    raise SystemExit(f"missing ledger: {ledger_path}")
if not oracle_path.is_file():
    raise SystemExit(f"missing oracle: {oracle_path}")

ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
families = [item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)]

test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path in [
        repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
        repo_root / "Chummer.Tests/Compliance/MigrationComplianceTests.cs",
        repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    ]
    if path.is_file()
)

sr4_oracle_families = {str(value).strip() for value in (oracle.get("workflowFamilies") or []) if str(value).strip()}
sr6_oracle_map = {
    str(item.get("id") or "").strip(): item
    for item in (oracle.get("requiredFamilies") or [])
    if isinstance(item, dict) and str(item.get("id") or "").strip()
}

any_fail = False
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue

    reasons = []
    status = str(family.get("status") or "").strip().lower()
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    verification_receipts = [
        str(value).strip() for value in (family.get("verificationReceipts") or []) if str(value).strip()
    ]
    execution_receipts = [
        str(value).strip() for value in (family.get("executionReceipts") or []) if str(value).strip()
    ]

    if status != "ready":
        reasons.append(f"Ledger family is not ready: {status or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    else:
        unresolved = [name for name in audit_tests if name not in test_corpus]
        if unresolved:
            reasons.append("Missing executable test references: " + ", ".join(unresolved))
    if not execution_receipts:
        reasons.append(
            "Missing executionReceipts for family. Verification receipts must be backed by executed family-scoped proof."
        )

    oracle_detail = {}
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
            classification = str(oracle_entry.get("classification") or "").strip()
            rationale = str(oracle_entry.get("rationale") or "").strip()
            release_gate_tests = [
                str(value).strip() for value in (oracle_entry.get("releaseGateTests") or []) if str(value).strip()
            ]
            if not classification:
                reasons.append("SR6 carry-forward oracle entry is missing classification.")
            if not rationale:
                reasons.append("SR6 carry-forward oracle entry is missing rationale.")
            if not release_gate_tests:
                reasons.append("SR6 carry-forward oracle entry is missing releaseGateTests.")
            oracle_detail = {
                "classification": classification,
                "rationale": rationale,
                "releaseGateTests": release_gate_tests,
            }

    if not verification_receipts:
        verification_receipts = [
            f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"
        ]

    execution_failures = []
    validated_execution_receipts = []
    execution_external_blockers = []
    for execution_ref in execution_receipts:
        execution_ref = execution_ref.replace("{familyId}", family_id)
        execution_path = Path(execution_ref)
        if not execution_path.is_absolute():
            execution_path = repo_root / execution_path
        if not execution_path.is_file():
            execution_failures.append(f"{execution_path} (missing)")
            continue
        execution_data = json.loads(execution_path.read_text(encoding="utf-8"))
        execution_status = str(execution_data.get("status") or "").strip().lower()
        execution_evidence = dict(execution_data.get("evidence") or {})
        execution_external_blocker = str(execution_evidence.get("external_blocker") or "").strip().lower()
        if execution_status not in {"pass", "passed", "ready"}:
            if execution_external_blocker:
                execution_external_blockers.append(execution_external_blocker)
                execution_failures.append(
                    f"{execution_path} ({execution_status or 'missing status'}; external_blocker={execution_external_blocker})"
                )
            else:
                execution_failures.append(f"{execution_path} ({execution_status or 'missing status'})")
            continue
        execution_edition = str(execution_evidence.get("edition") or "").strip().lower()
        execution_family = str(execution_evidence.get("familyId") or "").strip()
        execution_proof_kind = str(execution_evidence.get("proofKind") or "").strip().lower()
        if execution_edition != edition:
            execution_failures.append(f"{execution_path} (edition={execution_edition or 'missing'})")
            continue
        if execution_family != family_id:
            execution_failures.append(f"{execution_path} (familyId={execution_family or 'missing'})")
            continue
        if execution_proof_kind != expected_execution_proof_kind:
            execution_failures.append(
                f"{execution_path} (proofKind={execution_proof_kind or 'missing'}, expected={expected_execution_proof_kind})"
            )
            continue
        if (
            execution_evidence.get("upstreamReceipts")
            or execution_evidence.get("baselineReceipts")
            or execution_evidence.get("sourceReceipts")
        ):
            execution_failures.append(
                f"{execution_path} (uses upstream/baseline/source receipts instead of executed family evidence)"
            )
            continue
        validated_execution_receipts.append(str(execution_path))

    if execution_failures:
        reasons.append("Execution receipts are missing or not passing: " + ", ".join(execution_failures))

    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family verification evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family verification evidence is incomplete for {family_id}."
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
            "executionReceipts": validated_execution_receipts,
            "executionFailures": execution_failures,
            "executionExternalBlockers": sorted(set(execution_external_blockers)),
        },
    }

    for receipt_ref in verification_receipts:
        receipt_ref = receipt_ref.replace("{familyId}", family_id)
        output_path = Path(receipt_ref)
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        any_fail = True

if any_fail:
    raise SystemExit(43)
PY

echo "[materialize-${edition}-workflow-family-verification-receipts] PASS"
