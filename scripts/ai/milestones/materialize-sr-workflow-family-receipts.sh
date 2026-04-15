#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_parity_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_parity_receipt"
    proof_kind="sr6_family_carry_forward"
    ;;
  *)
    echo "usage: $0 <sr4|sr6>" >&2
    exit 64
    ;;
esac

fallback_out_dir="$repo_root/.codex-studio/published/workflow-family-parity/$edition"
mkdir -p "$fallback_out_dir"

python3 - <<'PY' "$edition" "$ledger_path" "$fallback_out_dir" "$contract_name" "$repo_root" "$oracle_path" "$proof_kind"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

edition = sys.argv[1].strip().lower()
ledger_path = Path(sys.argv[2])
fallback_out_dir = Path(sys.argv[3])
contract_name = sys.argv[4]
repo_root = Path(sys.argv[5])
oracle_path = Path(sys.argv[6])
proof_kind = sys.argv[7].strip().lower()

dual_head_tests_path = repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
compliance_tests_path = repo_root / "Chummer.Tests/Compliance/MigrationComplianceTests.cs"
ui_gate_tests_path = repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"

if not ledger_path.is_file():
    raise SystemExit(f"missing ledger: {ledger_path}")
if not oracle_path.is_file():
    raise SystemExit(f"missing oracle: {oracle_path}")

ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
families = [item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)]

test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path in [dual_head_tests_path, compliance_tests_path, ui_gate_tests_path]
    if path.is_file()
)

oracle_family_index = {}
for item in oracle.get("requiredFamilies") or []:
    if isinstance(item, dict):
        family_id = str(item.get("id") or "").strip()
        if family_id:
            oracle_family_index[family_id] = item
oracle_family_set = {str(item).strip() for item in (oracle.get("workflowFamilies") or []) if str(item).strip()}

any_fail = False
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue
    status = str(family.get("status") or "").strip().lower()
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    parity_receipts = [str(value).strip() for value in (family.get("parityReceipts") or []) if str(value).strip()]
    verification_receipts = [
        str(value).strip() for value in (family.get("verificationReceipts") or []) if str(value).strip()
    ]

    reasons = []
    if status != "ready":
        reasons.append(f"Ledger family is not ready: {status or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    else:
        unresolved = [name for name in audit_tests if name not in test_corpus]
        if unresolved:
            reasons.append("Missing executable test references: " + ", ".join(unresolved))
    if not parity_receipts:
        reasons.append("Missing parityReceipts for family.")
    if not verification_receipts:
        reasons.append(
            "Missing verificationReceipts for family. Static ledger/oracle/test-name materialization is scaffolding only."
        )

    oracle_detail = {}
    if edition == "sr4":
        if family_id not in oracle_family_set:
            reasons.append(f"Family is missing from SR4 oracle workflowFamilies: {family_id}")
        oracle_detail = {
            "sourceRepoPath": str((oracle.get("sourceRepo") or {}).get("path") or ""),
            "sourceRepoHead": str((oracle.get("sourceRepo") or {}).get("head") or ""),
        }
    else:
        oracle_entry = oracle_family_index.get(family_id)
        if not oracle_entry:
            reasons.append(f"Family is missing from SR6 carry-forward oracle requiredFamilies: {family_id}")
        else:
            oracle_detail = {
                "classification": str(oracle_entry.get("classification") or ""),
                "rationale": str(oracle_entry.get("rationale") or ""),
                "releaseGateTests": [
                    str(value).strip()
                    for value in (oracle_entry.get("releaseGateTests") or [])
                    if str(value).strip()
                ],
            }

    verification_failures = []
    verified_receipts = []
    verification_external_blockers = []
    for verification_ref in verification_receipts:
        verification_ref = verification_ref.replace("{familyId}", family_id)
        verification_path = Path(verification_ref)
        if not verification_path.is_absolute():
            verification_path = repo_root / verification_path
        if not verification_path.is_file():
            verification_failures.append(f"{verification_path} (missing)")
            continue
        verification_data = json.loads(verification_path.read_text(encoding="utf-8"))
        verification_status = str(verification_data.get("status") or "").strip().lower()
        if verification_status not in {"pass", "passed", "ready"}:
            verification_evidence = dict(verification_data.get("evidence") or {})
            execution_external_blockers = sorted(
                {
                    str(value).strip().lower()
                    for value in (verification_evidence.get("executionExternalBlockers") or [])
                    if str(value).strip()
                }
            )
            verification_external_blockers.extend(execution_external_blockers)
            if execution_external_blockers:
                verification_failures.append(
                    f"{verification_path} ({verification_status or 'missing status'}; "
                    + ", ".join(
                        f"external_blocker={blocker}"
                        for blocker in execution_external_blockers
                    )
                    + ")"
                )
            else:
                verification_failures.append(f"{verification_path} ({verification_status or 'missing status'})")
            continue
        verification_evidence = dict(verification_data.get("evidence") or {})
        verification_edition = str(verification_evidence.get("edition") or "").strip().lower()
        verification_family = str(verification_evidence.get("familyId") or "").strip()
        verification_proof_kind = str(verification_evidence.get("proofKind") or "").strip().lower()
        if verification_edition != edition:
            verification_failures.append(f"{verification_path} (edition={verification_edition or 'missing'})")
            continue
        if verification_family != family_id:
            verification_failures.append(f"{verification_path} (familyId={verification_family or 'missing'})")
            continue
        if verification_proof_kind != proof_kind:
            verification_failures.append(
                f"{verification_path} (proofKind={verification_proof_kind or 'missing'})"
            )
            continue
        if (
            verification_evidence.get("upstreamReceipts")
            or verification_evidence.get("baselineReceipts")
            or verification_evidence.get("sourceReceipts")
        ):
            verification_failures.append(
                f"{verification_path} (uses upstream/baseline/source receipts instead of executed family verification)"
            )
            continue
        if not (verification_evidence.get("executionReceipts") or []):
            verification_failures.append(
                f"{verification_path} (missing executionReceipts for executed family verification)"
            )
            continue
        verified_receipts.append(str(verification_path))

    if verification_failures:
        reasons.append("Verification receipts are missing or not passing: " + ", ".join(verification_failures))

    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family parity evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family parity evidence is incomplete for {family_id}."
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
            "verificationReceipts": verified_receipts,
            "verificationFailures": verification_failures,
            "verificationExternalBlockers": sorted(set(verification_external_blockers)),
        },
    }
    output_targets = parity_receipts or [str(fallback_out_dir / f"{family_id}.generated.json")]
    for receipt_ref in output_targets:
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

echo "[materialize-${edition}-workflow-family-receipts] PASS"
