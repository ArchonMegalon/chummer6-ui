#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
sr4_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
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
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-execution-receipts.sh" sr6 >/dev/null || execution_exit=$?
verification_exit=0
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-verification-receipts.sh" sr6 >/dev/null || verification_exit=$?
materializer_exit=0
bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-receipts.sh" sr6 >/dev/null || materializer_exit=$?

python3 - <<'PY' "$repo_root" "$receipt_path" "$ledger_path" "$sr4_receipt_path" "$dual_head_tests_path" "$compliance_tests_path" "$ui_gate_tests_path" "$execution_exit" "$verification_exit" "$materializer_exit" "$release_channel_path"
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

repo_root, receipt_path, ledger_path, sr4_receipt_path, dual_head_tests_path, compliance_tests_path, ui_gate_tests_path = [
    Path(value) for value in sys.argv[1:8]
]
execution_exit = int(sys.argv[8])
verification_exit = int(sys.argv[9])
materializer_exit = int(sys.argv[10])
release_channel_path = Path(sys.argv[11])
RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS") or "86400"
)
RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def status_ok(value: object) -> bool:
    return normalize(value) in {"pass", "passed", "ready"}


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
    "contract_name": "chummer6-ui.sr6_desktop_workflow_parity",
    "channelId": "",
    "status": "fail",
    "summary": "SR6 desktop workflow carry-forward parity is not yet exhaustively proven.",
    "reasons": [],
    "evidence": {
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelExists": release_channel_path.is_file(),
        "ledgerPath": str(ledger_path),
        "sr4ReceiptPath": str(sr4_receipt_path),
        "dualHeadTestsPath": str(dual_head_tests_path),
        "complianceTestsPath": str(compliance_tests_path),
        "uiGateTestsPath": str(ui_gate_tests_path),
        "releaseChannelMaxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
        "releaseChannelMaxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
        "executionExit": execution_exit,
        "verificationExit": verification_exit,
        "materializerExit": materializer_exit,
    },
}

source_artifact_reasons: list[str] = []
release_channel_reasons: list[str] = []
sr4_baseline_reasons: list[str] = []
workflow_family_reasons: list[str] = []
test_reference_reasons: list[str] = []
parity_receipt_reasons: list[str] = []
materialization_reasons: list[str] = []


def append_reason(message: str, *buckets: list[str]) -> None:
    if message not in payload["reasons"]:
        payload["reasons"].append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)


def require_file(path: Path, label: str) -> bool:
    if path.is_file():
        return True
    append_reason(f"{label} is missing: {path}", source_artifact_reasons)
    return False


ledger_exists = require_file(ledger_path, "SR6 workflow parity ledger")
sr4_receipt_exists = require_file(sr4_receipt_path, "SR4 workflow parity receipt")
dual_head_tests_exist = require_file(dual_head_tests_path, "Dual-head acceptance tests")
compliance_tests_exist = require_file(compliance_tests_path, "Migration compliance tests")
ui_gate_tests_exist = require_file(ui_gate_tests_path, "Flagship UI gate tests")

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

release_channel_age_seconds = None
release_channel_future_skew_seconds = None
if not release_channel_path.is_file():
    append_reason(f"Release channel receipt is missing: {release_channel_path}", release_channel_reasons)
elif not isinstance(release_channel, dict) or not release_channel:
    append_reason(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}"
        , release_channel_reasons
    )
if not release_channel_channel_id:
    append_reason("Release channel receipt is missing channelId/channel.", release_channel_reasons)
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
        , release_channel_reasons
    )
else:
    now = datetime.now(timezone.utc)
    release_channel_delta_seconds = (now - release_channel_generated_at).total_seconds()
    release_channel_age_seconds = int(max(release_channel_delta_seconds, 0))
    release_channel_future_skew_seconds = int(max(-release_channel_delta_seconds, 0))
    if release_channel_future_skew_seconds > RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS:
        append_reason(
            f"Release channel receipt generatedAt is in the future by {release_channel_future_skew_seconds} seconds.",
            release_channel_reasons,
        )
    if release_channel_age_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
        append_reason(
            f"Release channel receipt is stale ({release_channel_age_seconds} seconds old).",
            release_channel_reasons,
        )

ledger = json.loads(ledger_path.read_text(encoding="utf-8")) if ledger_exists else {}
families = {str(item.get("id") or "").strip(): item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)}
test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path, exists in [
        (dual_head_tests_path, dual_head_tests_exist),
        (compliance_tests_path, compliance_tests_exist),
        (ui_gate_tests_path, ui_gate_tests_exist),
    ]
    if exists
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
external_only_failing_parity_receipts = {}

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
            receipt_evidence = (
                receipt_data.get("evidence")
                if isinstance(receipt_data.get("evidence"), dict)
                else {}
            )
            verification_failures = [
                str(value).strip().lower()
                for value in (receipt_evidence.get("verificationFailures") or [])
                if str(value).strip()
            ]
            external_only = bool(verification_failures) and all(
                "external_blocker=missing_api_surface_contract" in failure
                for failure in verification_failures
            )
            if external_only:
                external_only_failing_parity_receipts[family_id] = verification_failures
                receipt_failures.append(
                    f"{receipt_file} ({receipt_status or 'missing status'}; external_blocker=missing_api_surface_contract)"
                )
            else:
                receipt_failures.append(f"{receipt_file} ({receipt_status or 'missing status'})")
            continue
        evidence = dict(receipt_data.get("evidence") or {})
        receipt_edition = str(evidence.get("edition") or "").strip().lower()
        receipt_family = str(evidence.get("familyId") or "").strip()
        proof_kind = str(evidence.get("proofKind") or "").strip().lower()
        if receipt_edition != "sr6":
            receipt_failures.append(f"{receipt_file} (edition={receipt_edition or 'missing'})")
            continue
        if receipt_family != family_id:
            receipt_failures.append(f"{receipt_file} (familyId={receipt_family or 'missing'})")
            continue
        if proof_kind != "sr6_family_carry_forward":
            receipt_failures.append(f"{receipt_file} (proofKind={proof_kind or 'missing'})")
            continue
        if evidence.get("baselineReceipts") or evidence.get("sourceReceipts"):
            receipt_failures.append(f"{receipt_file} (uses generic release receipts instead of family carry-forward proof)")
    if receipt_failures:
        failing_parity_receipts[family_id] = receipt_failures

if missing_family_ids:
    append_reason(
        "SR6 workflow parity ledger is missing required families: " + ", ".join(missing_family_ids),
        workflow_family_reasons,
    )
if non_ready_family_ids:
    append_reason(
        "SR6 workflow parity ledger has unresolved families: "
        + ", ".join(f"{family_id}={families[family_id].get('status', 'missing')}" for family_id in non_ready_family_ids)
        , workflow_family_reasons
    )

sr4_receipt_channel_id = ""
sr4_receipt_generated_at_raw = ""
sr4_receipt_generated_at = None
if not sr4_receipt_exists:
    append_reason(f"SR4 parity receipt is missing: {sr4_receipt_path}", sr4_baseline_reasons)
else:
    sr4_receipt = json.loads(sr4_receipt_path.read_text(encoding="utf-8"))
    if not status_ok(sr4_receipt.get("status")):
        append_reason("SR4 desktop workflow parity must pass before SR6 carry-forward parity can close.", sr4_baseline_reasons)
    sr4_receipt_channel_id = normalize(sr4_receipt.get("channelId") or sr4_receipt.get("channel"))
    if not sr4_receipt_channel_id:
        append_reason("SR4 desktop workflow parity receipt is missing channelId/channel.", sr4_baseline_reasons)
    elif release_channel_channel_id and sr4_receipt_channel_id != release_channel_channel_id:
        append_reason("SR4 desktop workflow parity receipt channelId does not match release channel.", sr4_baseline_reasons)
    for key in ("generatedAt", "generated_at"):
        if key in sr4_receipt:
            sr4_receipt_generated_at_raw = str(sr4_receipt.get(key) or "").strip()
            sr4_receipt_generated_at = parse_iso(sr4_receipt_generated_at_raw)
            break
    if not sr4_receipt_generated_at_raw or sr4_receipt_generated_at is None:
        append_reason("SR4 desktop workflow parity receipt is missing a valid generatedAt/generated_at timestamp.", sr4_baseline_reasons)
    elif release_channel_generated_at is not None and sr4_receipt_generated_at < release_channel_generated_at:
        append_reason("SR4 desktop workflow parity receipt predates the release channel generatedAt timestamp.", sr4_baseline_reasons)
if missing_test_refs:
    append_reason(
        "SR6 workflow parity ledger references missing executable tests: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_test_refs.items()))
        , test_reference_reasons
    )
if missing_parity_receipts:
    append_reason(
        "SR6 workflow parity ledger is missing edition-specific parity receipts: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_parity_receipts.items()))
        , parity_receipt_reasons
    )
if failing_parity_receipts:
    external_only_fail = (
        len(external_only_failing_parity_receipts) == len(failing_parity_receipts)
    )
    if external_only_fail:
        append_reason(
            "SR6 workflow parity receipts require a chummer-api host exposing /api/workspaces and /api/shell/bootstrap "
            "(external blocker: missing_api_surface_contract): "
            + ", ".join(
                f"{family_id}: {', '.join(names)}"
                for family_id, names in sorted(failing_parity_receipts.items())
            )
            , parity_receipt_reasons
        )
    else:
        append_reason(
            "SR6 workflow parity receipts are missing or not passing: "
            + ", ".join(
                f"{family_id}: {', '.join(names)}"
                for family_id, names in sorted(failing_parity_receipts.items())
            )
            , parity_receipt_reasons
        )
if materializer_exit not in {0, 43}:
    append_reason(
        f"SR6 family receipt materialization exited unexpectedly: {materializer_exit}",
        materialization_reasons,
    )
if verification_exit not in {0, 43}:
    append_reason(
        f"SR6 verification receipt materialization exited unexpectedly: {verification_exit}",
        materialization_reasons,
    )
if execution_exit not in {0, 43}:
    append_reason(
        f"SR6 execution receipt materialization exited unexpectedly: {execution_exit}",
        materialization_reasons,
    )

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "SR6 desktop workflow carry-forward parity is explicitly proven across source artifacts, release-channel identity, "
        "SR4 baseline proof, workflow-family readiness, executable test references, receipt proof, and materialization."
    )

payload["channelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
payload["evidence"]["releaseChannelAgeSeconds"] = release_channel_age_seconds
payload["evidence"]["releaseChannelFutureSkewSeconds"] = release_channel_future_skew_seconds
payload["evidence"]["requiredFamilyCount"] = len(required_family_ids)
payload["evidence"]["ledgerFamilyCount"] = len(families)
payload["evidence"]["missingFamilyIds"] = missing_family_ids
payload["evidence"]["nonReadyFamilyIds"] = non_ready_family_ids
payload["evidence"]["missingTestRefs"] = missing_test_refs
payload["evidence"]["missingParityReceipts"] = missing_parity_receipts
payload["evidence"]["failingParityReceipts"] = failing_parity_receipts
payload["evidence"]["failingParityReceiptsExternalOnly"] = (
    len(external_only_failing_parity_receipts) == len(failing_parity_receipts)
    and bool(failing_parity_receipts)
)
payload["evidence"]["failingParityReceiptsExternal"] = external_only_failing_parity_receipts
payload["evidence"]["sourceArtifactChecks"] = {
    "ledger": ledger_exists,
    "sr4Receipt": sr4_receipt_exists,
    "dualHeadTests": dual_head_tests_exist,
    "complianceTests": compliance_tests_exist,
    "uiGateTests": ui_gate_tests_exist,
}
payload["evidence"]["sr4ReceiptChannelId"] = sr4_receipt_channel_id
payload["evidence"]["sr4ReceiptGeneratedAt"] = sr4_receipt_generated_at_raw
payload["evidence"]["failureCount"] = len(payload["reasons"])

payload["sourceArtifactReview"] = {
    "status": "pass" if not source_artifact_reasons else "fail",
    "summary": (
        "SR6 ledger, SR4 baseline receipt, and executable test sources are present."
        if not source_artifact_reasons
        else "One or more SR6 ledger, SR4 baseline, or executable test sources are missing."
    ),
    "reasons": source_artifact_reasons,
    "checks": payload["evidence"]["sourceArtifactChecks"],
}
payload["releaseChannelReview"] = {
    "status": "pass" if not release_channel_reasons else "fail",
    "summary": (
        "SR6 workflow parity proof is aligned to a current release-channel identity."
        if not release_channel_reasons
        else "SR6 workflow parity proof is missing or drifting from release-channel identity."
    ),
    "reasons": release_channel_reasons,
    "path": str(release_channel_path),
    "channelId": release_channel_channel_id,
    "generatedAt": release_channel_generated_at_raw,
    "ageSeconds": release_channel_age_seconds,
    "futureSkewSeconds": release_channel_future_skew_seconds,
    "maxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
    "maxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
}
payload["sr4BaselineReview"] = {
    "status": "pass" if not sr4_baseline_reasons else "fail",
    "summary": (
        "SR4 baseline workflow parity proof is present and aligned for SR6 carry-forward closure."
        if not sr4_baseline_reasons
        else "SR4 baseline workflow parity proof is missing, stale, or misaligned for SR6 carry-forward closure."
    ),
    "reasons": sr4_baseline_reasons,
    "path": str(sr4_receipt_path),
    "channelId": sr4_receipt_channel_id,
    "generatedAt": sr4_receipt_generated_at_raw,
}
payload["workflowFamilyReview"] = {
    "status": "pass" if not workflow_family_reasons else "fail",
    "summary": (
        "All required SR6 workflow families are present and ready."
        if not workflow_family_reasons
        else "One or more required SR6 workflow families are missing or non-ready."
    ),
    "reasons": workflow_family_reasons,
    "requiredFamilyCount": len(required_family_ids),
    "ledgerFamilyCount": len(families),
    "missingFamilyIds": missing_family_ids,
    "nonReadyFamilyIds": non_ready_family_ids,
}
payload["testReferenceReview"] = {
    "status": "pass" if not test_reference_reasons else "fail",
    "summary": (
        "SR6 workflow parity audit tests resolve to executable test sources."
        if not test_reference_reasons
        else "SR6 workflow parity ledger still references missing executable tests."
    ),
    "reasons": test_reference_reasons,
    "missingTestRefs": missing_test_refs,
}
payload["parityReceiptReview"] = {
    "status": "pass" if not parity_receipt_reasons else "fail",
    "summary": (
        "SR6 family-specific carry-forward receipts are present and passing."
        if not parity_receipt_reasons
        else "SR6 family-specific carry-forward receipts are missing, failing, or externally blocked."
    ),
    "reasons": parity_receipt_reasons,
    "missingParityReceipts": missing_parity_receipts,
    "failingParityReceipts": failing_parity_receipts,
    "failingParityReceiptsExternalOnly": payload["evidence"]["failingParityReceiptsExternalOnly"],
    "failingParityReceiptsExternal": external_only_failing_parity_receipts,
}
payload["materializationReview"] = {
    "status": "pass" if not materialization_reasons else "fail",
    "summary": (
        "SR6 family execution, verification, and receipt materializers exited within allowed bounds."
        if not materialization_reasons
        else "One or more SR6 family materializers exited unexpectedly."
    ),
    "reasons": materialization_reasons,
    "executionExit": execution_exit,
    "verificationExit": verification_exit,
    "materializerExit": materializer_exit,
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[sr6-workflow-parity] PASS"
