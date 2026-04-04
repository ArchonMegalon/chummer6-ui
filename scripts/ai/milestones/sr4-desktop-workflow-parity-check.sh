#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
compliance_tests_path="$repo_root/Chummer.Tests/Compliance/MigrationComplianceTests.cs"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"

mkdir -p "$(dirname "$receipt_path")"
execution_exit=0
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-execution-receipts.sh" sr4 >/dev/null || execution_exit=$?
verification_exit=0
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-verification-receipts.sh" sr4 >/dev/null || verification_exit=$?
materializer_exit=0
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-receipts.sh" sr4 >/dev/null || materializer_exit=$?

python3 - <<'PY' "$repo_root" "$receipt_path" "$oracle_path" "$ledger_path" "$dual_head_tests_path" "$compliance_tests_path" "$ui_gate_tests_path" "$execution_exit" "$verification_exit" "$materializer_exit" "$release_channel_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

repo_root, receipt_path, oracle_path, ledger_path, dual_head_tests_path, compliance_tests_path, ui_gate_tests_path = [
    Path(value) for value in sys.argv[1:8]
]
execution_exit = int(sys.argv[8])
verification_exit = int(sys.argv[9])
materializer_exit = int(sys.argv[10])
release_channel_path = Path(sys.argv[11])


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def parse_iso(value: object) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)
required_family_ids = [
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
]

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.sr4_desktop_workflow_parity",
    "channelId": "",
    "status": "fail",
    "summary": "SR4 desktop workflow parity is not yet exhaustively proven against the local Chummer4 oracle.",
    "reasons": [],
    "evidence": {},
}

if not oracle_path.is_file():
    payload["reasons"].append(f"Missing SR4 oracle: {oracle_path}")
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(43)

if not ledger_path.is_file():
    payload["reasons"].append(f"Missing SR4 workflow parity ledger: {ledger_path}")
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(43)

release_channel = {}
if release_channel_path.is_file():
    loaded = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
    if isinstance(loaded, dict):
        release_channel = loaded
release_channel_channel_id = normalize(
    release_channel.get("channelId") if isinstance(release_channel, dict) else ""
)
if not release_channel_channel_id:
    release_channel_channel_id = normalize(
        release_channel.get("channel") if isinstance(release_channel, dict) else ""
    )
release_channel_generated_at_raw = ""
release_channel_generated_at = None
for key in ("generatedAt", "generated_at"):
    if isinstance(release_channel, dict) and key in release_channel:
        release_channel_generated_at_raw = str(release_channel.get(key) or "").strip()
        release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
        break

if not release_channel_path.is_file():
    payload["reasons"].append(f"Release channel receipt is missing: {release_channel_path}")
elif not isinstance(release_channel, dict) or not release_channel:
    payload["reasons"].append(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}"
    )
if not release_channel_channel_id:
    payload["reasons"].append("Release channel receipt is missing channelId/channel.")
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    payload["reasons"].append(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )

oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
families = {str(item.get("id") or "").strip(): item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)}
test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path in [dual_head_tests_path, compliance_tests_path, ui_gate_tests_path]
    if path.is_file()
)

missing_family_ids = [family_id for family_id in required_family_ids if family_id not in families]
non_ready_family_ids = [
    family_id
    for family_id in required_family_ids
    if family_id in families and str(families[family_id].get("status") or "").strip().lower() != "ready"
]
missing_test_refs = {}
missing_parity_receipts = {}
failing_parity_receipts = {}

for family_id in required_family_ids:
    family = families.get(family_id) or {}
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    if not audit_tests:
        missing_test_refs[family_id] = ["<missing auditTests>"]
    unresolved = [name for name in audit_tests if name not in test_corpus]
    if unresolved:
        missing_test_refs[family_id] = unresolved
    parity_receipts = [str(value).strip() for value in (family.get("parityReceipts") or []) if str(value).strip()]
    if not parity_receipts:
        missing_parity_receipts[family_id] = ["<missing parityReceipts>"]
        continue
    receipt_failures = []
    for receipt_ref in parity_receipts:
        receipt_ref = receipt_ref.replace("{familyId}", family_id)
        receipt_file = Path(receipt_ref)
        if not receipt_file.is_absolute():
            receipt_file = repo_root / receipt_file
        if not receipt_file.is_file():
            receipt_failures.append(f"{receipt_file} (missing)")
            continue
        receipt_data = json.loads(receipt_file.read_text(encoding="utf-8"))
        receipt_status = str(receipt_data.get("status") or "").strip().lower()
        if receipt_status not in {"pass", "passed", "ready"}:
            receipt_failures.append(f"{receipt_file} ({receipt_status or 'missing status'})")
            continue
        evidence = dict(receipt_data.get("evidence") or {})
        receipt_edition = str(evidence.get("edition") or "").strip().lower()
        receipt_family = str(evidence.get("familyId") or "").strip()
        proof_kind = str(evidence.get("proofKind") or "").strip().lower()
        if receipt_edition != "sr4":
            receipt_failures.append(f"{receipt_file} (edition={receipt_edition or 'missing'})")
            continue
        if receipt_family != family_id:
            receipt_failures.append(f"{receipt_file} (familyId={receipt_family or 'missing'})")
            continue
        if proof_kind != "sr4_family_oracle":
            receipt_failures.append(f"{receipt_file} (proofKind={proof_kind or 'missing'})")
            continue
        if evidence.get("baselineReceipts") or evidence.get("sourceReceipts"):
            receipt_failures.append(f"{receipt_file} (uses generic release receipts instead of family oracle proof)")
    if receipt_failures:
        failing_parity_receipts[family_id] = receipt_failures

if missing_family_ids:
    payload["reasons"].append("SR4 workflow parity ledger is missing required families: " + ", ".join(missing_family_ids))
if non_ready_family_ids:
    payload["reasons"].append(
        "SR4 workflow parity ledger has unresolved families: "
        + ", ".join(f"{family_id}={families[family_id].get('status', 'missing')}" for family_id in non_ready_family_ids)
    )

source_repo = dict(oracle.get("sourceRepo") or {})
source_repo_path = Path(str(source_repo.get("path") or "").strip()) if str(source_repo.get("path") or "").strip() else None
if source_repo_path is None or not source_repo_path.is_dir():
    payload["reasons"].append(f"SR4 oracle source repo is missing or not readable: {source_repo.get('path') or ''}")
if missing_test_refs:
    payload["reasons"].append(
        "SR4 workflow parity ledger references missing executable tests: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_test_refs.items()))
    )
if missing_parity_receipts:
    payload["reasons"].append(
        "SR4 workflow parity ledger is missing edition-specific parity receipts: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_parity_receipts.items()))
    )
if failing_parity_receipts:
    payload["reasons"].append(
        "SR4 workflow parity receipts are missing or not passing: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(failing_parity_receipts.items()))
    )
if materializer_exit not in {0, 43}:
    payload["reasons"].append(f"SR4 family receipt materialization exited unexpectedly: {materializer_exit}")
if verification_exit not in {0, 43}:
    payload["reasons"].append(f"SR4 verification receipt materialization exited unexpectedly: {verification_exit}")
if execution_exit not in {0, 43}:
    payload["reasons"].append(f"SR4 execution receipt materialization exited unexpectedly: {execution_exit}")

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = "SR4 desktop workflow parity is explicitly proven against the local Chummer4 oracle."

payload["channelId"] = release_channel_channel_id
payload["evidence"] = {
    "releaseChannelPath": str(release_channel_path),
    "releaseChannelExists": release_channel_path.is_file(),
    "releaseChannelChannelId": release_channel_channel_id,
    "releaseChannelGeneratedAt": release_channel_generated_at_raw,
    "oraclePath": str(oracle_path),
    "ledgerPath": str(ledger_path),
    "sourceRepoPath": str(source_repo.get("path") or ""),
    "sourceRepoHead": str(source_repo.get("head") or ""),
    "missingFamilyIds": missing_family_ids,
    "nonReadyFamilyIds": non_ready_family_ids,
    "missingTestRefs": missing_test_refs,
    "missingParityReceipts": missing_parity_receipts,
    "failingParityReceipts": failing_parity_receipts,
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[sr4-workflow-parity] PASS"
