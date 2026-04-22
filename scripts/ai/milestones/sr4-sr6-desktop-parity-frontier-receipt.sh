#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
sr4_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
chummer5a_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
ruleset_ui_adaptation_receipt_path="$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json"
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

echo "[sr4-sr6-frontier] running SR4 parity gate..."
sr4_exit=0
bash scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh >/dev/null || sr4_exit=$?

echo "[sr4-sr6-frontier] running SR6 parity gate..."
sr6_exit=0
bash scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh >/dev/null || sr6_exit=$?

echo "[sr4-sr6-frontier] running Chummer5A parity gate..."
chummer5a_exit=0
bash scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh >/dev/null || chummer5a_exit=$?

echo "[sr4-sr6-frontier] running ruleset/UI adaptation gate..."
ruleset_ui_adaptation_exit=0
bash scripts/ai/milestones/ruleset-ui-adaptation-check.sh >/dev/null || ruleset_ui_adaptation_exit=$?

python3 - <<'PY' "$receipt_path" "$sr4_receipt_path" "$sr6_receipt_path" "$chummer5a_receipt_path" "$ruleset_ui_adaptation_receipt_path" "$sr4_exit" "$sr6_exit" "$chummer5a_exit" "$ruleset_ui_adaptation_exit" "$release_channel_path"
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path, sr4_receipt_path, sr6_receipt_path, chummer5a_receipt_path, ruleset_ui_adaptation_receipt_path = [Path(value) for value in sys.argv[1:6]]
sr4_exit = int(sys.argv[6])
sr6_exit = int(sys.argv[7])
chummer5a_exit = int(sys.argv[8])
ruleset_ui_adaptation_exit = int(sys.argv[9])
release_channel_path = Path(sys.argv[10])

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


payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.sr4_sr6_desktop_parity_frontier",
    "channelId": "",
    "status": "fail",
    "summary": "SR4/SR6 desktop parity frontier closure is not yet proven.",
    "reasons": [],
    "evidence": {
        "sr4ReceiptPath": str(sr4_receipt_path),
        "sr6ReceiptPath": str(sr6_receipt_path),
        "chummer5aReceiptPath": str(chummer5a_receipt_path),
        "rulesetUiAdaptationReceiptPath": str(ruleset_ui_adaptation_receipt_path),
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelExists": release_channel_path.is_file(),
        "releaseChannelMaxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
        "releaseChannelMaxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
    },
}

release_channel_reasons: list[str] = []
workflow_receipt_reasons: list[str] = []
workflow_coverage_reasons: list[str] = []
ruleset_adaptation_reasons: list[str] = []
channel_alignment_reasons: list[str] = []
gate_execution_reasons: list[str] = []


def append_reason(message: str, *buckets: list[str]) -> None:
    if message not in payload["reasons"]:
        payload["reasons"].append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)


def read_json_object(path: Path, label: str, *buckets: list[str]) -> dict:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except FileNotFoundError:
        append_reason(f"{label} receipt is missing: {path}", *buckets)
        return {}
    except json.JSONDecodeError as exc:
        append_reason(f"{label} receipt is not valid JSON: {path} ({exc})", *buckets)
        return {}
    if not isinstance(loaded, dict):
        append_reason(f"{label} receipt is not a JSON object: {path}", *buckets)
        return {}
    return loaded


def generated_at_fields(record: dict) -> tuple[str, datetime | None]:
    for key in ("generatedAt", "generated_at"):
        if key in record:
            raw = str(record.get(key) or "").strip()
            return raw, parse_iso(raw)
    return "", None


def count_mapping_entries(value: object) -> int:
    return len(value) if isinstance(value, dict) else 0


def sorted_mapping_keys(value: object) -> list[str]:
    if not isinstance(value, dict):
        return []
    return sorted(str(key) for key in value.keys())


release_channel = {}
if release_channel_path.is_file():
    release_channel = read_json_object(
        release_channel_path,
        "Release channel",
        release_channel_reasons,
        channel_alignment_reasons,
    )
release_channel_channel_id = normalize(
    release_channel.get("channelId") if isinstance(release_channel, dict) else ""
)
if not release_channel_channel_id:
    release_channel_channel_id = normalize(
        release_channel.get("channel") if isinstance(release_channel, dict) else ""
    )
release_channel_generated_at_raw, release_channel_generated_at = generated_at_fields(release_channel)
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
release_channel_age_seconds = None
release_channel_future_skew_seconds = None
if not release_channel_channel_id:
    append_reason("Release channel receipt is missing channelId/channel.", release_channel_reasons, channel_alignment_reasons)
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp.",
        release_channel_reasons,
        channel_alignment_reasons,
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
            channel_alignment_reasons,
        )
    if release_channel_age_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
        append_reason(
            f"Release channel receipt is stale ({release_channel_age_seconds} seconds old).",
            release_channel_reasons,
            channel_alignment_reasons,
        )

payload["evidence"]["releaseChannelAgeSeconds"] = release_channel_age_seconds
payload["evidence"]["releaseChannelFutureSkewSeconds"] = release_channel_future_skew_seconds

sr4_receipt = read_json_object(sr4_receipt_path, "SR4 parity", workflow_receipt_reasons, workflow_coverage_reasons, channel_alignment_reasons)
sr6_receipt = read_json_object(sr6_receipt_path, "SR6 parity", workflow_receipt_reasons, workflow_coverage_reasons, channel_alignment_reasons)
chummer5a_receipt = read_json_object(chummer5a_receipt_path, "Chummer5a parity", workflow_receipt_reasons, workflow_coverage_reasons, channel_alignment_reasons)
ruleset_ui_adaptation_receipt = read_json_object(
    ruleset_ui_adaptation_receipt_path,
    "Ruleset/UI adaptation",
    workflow_receipt_reasons,
    ruleset_adaptation_reasons,
    channel_alignment_reasons,
)

receipt_specs = {
    "sr4": ("SR4 parity", sr4_receipt),
    "sr6": ("SR6 parity", sr6_receipt),
    "chummer5a": ("Chummer5a parity", chummer5a_receipt),
    "rulesetUiAdaptation": ("Ruleset/UI adaptation", ruleset_ui_adaptation_receipt),
}
receipt_statuses: dict[str, str] = {}
receipt_generated_at_values: dict[str, str] = {}
receipt_channel_ids: dict[str, str] = {}
receipt_release_channel_paths: dict[str, str] = {}
receipt_release_channel_statuses: dict[str, str] = {}
receipt_release_channel_generated_ats: dict[str, str] = {}

for key, (label, receipt) in receipt_specs.items():
    if not receipt:
        continue
    status = normalize(receipt.get("status"))
    receipt_statuses[key] = status
    if not status_ok(status):
        append_reason(
            f"{label} receipt is not passing: " + ", ".join(receipt.get("reasons") or ["missing reason"]),
            workflow_receipt_reasons,
        )

    generated_at_raw, generated_at = generated_at_fields(receipt)
    receipt_generated_at_values[key] = generated_at_raw
    if not generated_at_raw or generated_at is None:
        append_reason(
            f"{label} receipt is missing a valid generatedAt/generated_at timestamp.",
            workflow_receipt_reasons,
        )
    elif release_channel_generated_at is not None and generated_at < release_channel_generated_at:
        append_reason(
            f"{label} receipt predates the release channel generatedAt timestamp.",
            workflow_receipt_reasons,
            channel_alignment_reasons,
        )

    channel_id = normalize(receipt.get("channelId") or receipt.get("channel"))
    receipt_channel_ids[key] = channel_id
    if not channel_id:
        append_reason(f"{label} receipt is missing channelId/channel.", workflow_receipt_reasons, channel_alignment_reasons)
    elif release_channel_channel_id and channel_id != release_channel_channel_id:
        append_reason(f"{label} receipt channelId does not match release channel.", workflow_receipt_reasons, channel_alignment_reasons)

    evidence = receipt.get("evidence") if isinstance(receipt.get("evidence"), dict) else {}
    release_channel_receipt_path = str(evidence.get("releaseChannelPath") or "").strip()
    receipt_release_channel_paths[key] = release_channel_receipt_path
    if release_channel_receipt_path and release_channel_receipt_path != str(release_channel_path):
        append_reason(
            f"{label} receipt releaseChannelPath does not match the frontier release channel path.",
            channel_alignment_reasons,
        )
    recorded_release_channel_status = normalize(evidence.get("releaseChannelChannelId"))
    receipt_release_channel_statuses[key] = recorded_release_channel_status
    if recorded_release_channel_status and release_channel_channel_id and recorded_release_channel_status != release_channel_channel_id:
        append_reason(
            f"{label} receipt releaseChannelChannelId does not match the frontier release channel channelId.",
            channel_alignment_reasons,
        )
    recorded_release_channel_generated_at = str(evidence.get("releaseChannelGeneratedAt") or "").strip()
    receipt_release_channel_generated_ats[key] = recorded_release_channel_generated_at
    if recorded_release_channel_generated_at and release_channel_generated_at_raw and recorded_release_channel_generated_at != release_channel_generated_at_raw:
        append_reason(
            f"{label} receipt releaseChannelGeneratedAt does not match the frontier release channel timestamp.",
            channel_alignment_reasons,
        )

payload["evidence"]["sr4Status"] = receipt_statuses.get("sr4", "")
payload["evidence"]["sr6Status"] = receipt_statuses.get("sr6", "")
payload["evidence"]["chummer5aStatus"] = receipt_statuses.get("chummer5a", "")
payload["evidence"]["rulesetUiAdaptationStatus"] = receipt_statuses.get("rulesetUiAdaptation", "")
payload["evidence"]["sr4ClosureCounts"] = ((sr4_receipt.get("evidence") if sr4_receipt else {}) or {}).get("closureCounts")
payload["evidence"]["sr6ClosureCounts"] = ((sr6_receipt.get("evidence") if sr6_receipt else {}) or {}).get("closureCounts")
payload["evidence"]["sr4ChannelId"] = receipt_channel_ids.get("sr4", "")
payload["evidence"]["sr6ChannelId"] = receipt_channel_ids.get("sr6", "")
payload["evidence"]["chummer5aChannelId"] = receipt_channel_ids.get("chummer5a", "")
payload["evidence"]["rulesetUiAdaptationChannelId"] = receipt_channel_ids.get("rulesetUiAdaptation", "")

if sr4_receipt:
    sr4_evidence = sr4_receipt.get("evidence") if isinstance(sr4_receipt.get("evidence"), dict) else {}
    sr4_missing_family_ids = list(sr4_evidence.get("missingFamilyIds") or [])
    sr4_non_ready_family_ids = list(sr4_evidence.get("nonReadyFamilyIds") or [])
    sr4_missing_test_refs = sorted_mapping_keys(sr4_evidence.get("missingTestRefs"))
    sr4_missing_parity_receipts = sorted_mapping_keys(sr4_evidence.get("missingParityReceipts"))
    sr4_failing_parity_receipts = sorted_mapping_keys(sr4_evidence.get("failingParityReceipts"))
    if sr4_missing_family_ids:
        append_reason(
            "SR4 parity receipt is missing canonical workflow families: " + ", ".join(sr4_missing_family_ids),
            workflow_coverage_reasons,
        )
    if sr4_non_ready_family_ids:
        append_reason(
            "SR4 parity receipt has non-ready workflow families: " + ", ".join(sr4_non_ready_family_ids),
            workflow_coverage_reasons,
        )
    if sr4_missing_test_refs:
        append_reason(
            "SR4 parity receipt is missing workflow test references for: " + ", ".join(sr4_missing_test_refs),
            workflow_coverage_reasons,
        )
    if sr4_missing_parity_receipts:
        append_reason(
            "SR4 parity receipt is missing parity receipt proofs for: " + ", ".join(sr4_missing_parity_receipts),
            workflow_coverage_reasons,
        )
    if sr4_failing_parity_receipts:
        append_reason(
            "SR4 parity receipt has failing parity receipt proofs for: " + ", ".join(sr4_failing_parity_receipts),
            workflow_coverage_reasons,
        )
else:
    sr4_evidence = {}
    sr4_missing_family_ids = []
    sr4_non_ready_family_ids = []
    sr4_missing_test_refs = []
    sr4_missing_parity_receipts = []
    sr4_failing_parity_receipts = []

if sr6_receipt:
    sr6_evidence = sr6_receipt.get("evidence") if isinstance(sr6_receipt.get("evidence"), dict) else {}
    sr6_missing_family_ids = list(sr6_evidence.get("missingFamilyIds") or [])
    sr6_non_ready_family_ids = list(sr6_evidence.get("nonReadyFamilyIds") or [])
    sr6_missing_test_refs = sorted_mapping_keys(sr6_evidence.get("missingTestRefs"))
    sr6_missing_parity_receipts = sorted_mapping_keys(sr6_evidence.get("missingParityReceipts"))
    sr6_failing_parity_receipts = sorted_mapping_keys(sr6_evidence.get("failingParityReceipts"))
    if sr6_missing_family_ids:
        append_reason(
            "SR6 parity receipt is missing canonical workflow families: " + ", ".join(sr6_missing_family_ids),
            workflow_coverage_reasons,
        )
    if sr6_non_ready_family_ids:
        append_reason(
            "SR6 parity receipt has non-ready workflow families: " + ", ".join(sr6_non_ready_family_ids),
            workflow_coverage_reasons,
        )
    if sr6_missing_test_refs:
        append_reason(
            "SR6 parity receipt is missing workflow test references for: " + ", ".join(sr6_missing_test_refs),
            workflow_coverage_reasons,
        )
    if sr6_missing_parity_receipts:
        append_reason(
            "SR6 parity receipt is missing parity receipt proofs for: " + ", ".join(sr6_missing_parity_receipts),
            workflow_coverage_reasons,
        )
    if sr6_failing_parity_receipts:
        append_reason(
            "SR6 parity receipt has failing parity receipt proofs for: " + ", ".join(sr6_failing_parity_receipts),
            workflow_coverage_reasons,
        )
else:
    sr6_evidence = {}
    sr6_missing_family_ids = []
    sr6_non_ready_family_ids = []
    sr6_missing_test_refs = []
    sr6_missing_parity_receipts = []
    sr6_failing_parity_receipts = []

if chummer5a_receipt:
    chummer5a_evidence = chummer5a_receipt.get("evidence") if isinstance(chummer5a_receipt.get("evidence"), dict) else {}
    chummer5a_required_family_count = int(chummer5a_evidence.get("requiredFamilyCount") or 0)
    chummer5a_ledger_family_count = int(chummer5a_evidence.get("ledgerFamilyCount") or 0)
    chummer5a_missing_family_ids = list(chummer5a_evidence.get("missingFamilyIds") or [])
    chummer5a_non_ready_family_ids = list(chummer5a_evidence.get("nonReadyFamilyIds") or [])
    chummer5a_missing_test_refs = sorted_mapping_keys(chummer5a_evidence.get("missingTestRefs"))
    chummer5a_tabs_missing_in_catalog = int(chummer5a_evidence.get("tabsMissingInCatalog") or 0)
    chummer5a_workspace_actions_missing_in_catalog = int(chummer5a_evidence.get("workspaceActionsMissingInCatalog") or 0)
    if chummer5a_required_family_count <= 0:
        append_reason(
            "Chummer5a parity receipt is missing a positive requiredFamilyCount.",
            workflow_coverage_reasons,
        )
    if chummer5a_required_family_count != chummer5a_ledger_family_count:
        append_reason(
            "Chummer5a parity receipt ledgerFamilyCount does not match requiredFamilyCount.",
            workflow_coverage_reasons,
        )
    if chummer5a_missing_family_ids:
        append_reason(
            "Chummer5a parity receipt is missing canonical workflow families: " + ", ".join(chummer5a_missing_family_ids),
            workflow_coverage_reasons,
        )
    if chummer5a_non_ready_family_ids:
        append_reason(
            "Chummer5a parity receipt has non-ready workflow families: " + ", ".join(chummer5a_non_ready_family_ids),
            workflow_coverage_reasons,
        )
    if chummer5a_missing_test_refs:
        append_reason(
            "Chummer5a parity receipt is missing workflow test references for: " + ", ".join(chummer5a_missing_test_refs),
            workflow_coverage_reasons,
        )
    if chummer5a_tabs_missing_in_catalog > 0:
        append_reason(
            f"Chummer5a parity receipt is missing {chummer5a_tabs_missing_in_catalog} tabs from the catalog.",
            workflow_coverage_reasons,
        )
    if chummer5a_workspace_actions_missing_in_catalog > 0:
        append_reason(
            f"Chummer5a parity receipt is missing {chummer5a_workspace_actions_missing_in_catalog} workspace actions from the catalog.",
            workflow_coverage_reasons,
        )
else:
    chummer5a_evidence = {}
    chummer5a_required_family_count = 0
    chummer5a_ledger_family_count = 0
    chummer5a_missing_family_ids = []
    chummer5a_non_ready_family_ids = []
    chummer5a_missing_test_refs = []
    chummer5a_tabs_missing_in_catalog = 0
    chummer5a_workspace_actions_missing_in_catalog = 0

if ruleset_ui_adaptation_receipt:
    ruleset_evidence = (
        ruleset_ui_adaptation_receipt.get("evidence")
        if isinstance(ruleset_ui_adaptation_receipt.get("evidence"), dict)
        else {}
    )
    ruleset_failure_count = int(ruleset_evidence.get("failureCount") or 0)
    required_directives = [str(value) for value in (ruleset_evidence.get("requiredDirectives") or []) if str(value).strip()]
    expected_directives = [f"UI-RS-{index:02d}" for index in range(1, 9)]
    missing_directives = [directive for directive in expected_directives if directive not in required_directives]
    unexpected_directives = [directive for directive in required_directives if directive not in expected_directives]
    ruleset_test_build_exit = int(ruleset_evidence.get("testBuildExit") or 0)
    if ruleset_failure_count != 0:
        append_reason(
            f"Ruleset/UI adaptation receipt reports failureCount={ruleset_failure_count}.",
            ruleset_adaptation_reasons,
        )
    if missing_directives:
        append_reason(
            "Ruleset/UI adaptation receipt is missing required directives: " + ", ".join(missing_directives),
            ruleset_adaptation_reasons,
        )
    if unexpected_directives:
        append_reason(
            "Ruleset/UI adaptation receipt reports unexpected directives: " + ", ".join(unexpected_directives),
            ruleset_adaptation_reasons,
        )
    if ruleset_test_build_exit != 0:
        append_reason(
            f"Ruleset/UI adaptation receipt reports non-zero testBuildExit={ruleset_test_build_exit}.",
            ruleset_adaptation_reasons,
        )
else:
    ruleset_evidence = {}
    ruleset_failure_count = 0
    required_directives = []
    missing_directives = []
    unexpected_directives = []
    ruleset_test_build_exit = 0

if sr4_exit != 0:
    append_reason(f"SR4 parity gate exited non-zero: {sr4_exit}", gate_execution_reasons)
if sr6_exit != 0:
    append_reason(f"SR6 parity gate exited non-zero: {sr6_exit}", gate_execution_reasons)
if chummer5a_exit != 0:
    append_reason(f"Chummer5a parity gate exited non-zero: {chummer5a_exit}", gate_execution_reasons)
if ruleset_ui_adaptation_exit != 0:
    append_reason(f"Ruleset/UI adaptation gate exited non-zero: {ruleset_ui_adaptation_exit}", gate_execution_reasons)

payload["releaseChannelReview"] = {
    "status": "pass" if not release_channel_reasons else "fail",
    "summary": (
        "Release channel identity and freshness are aligned to the SR4/SR6 desktop parity frontier."
        if not release_channel_reasons
        else "Release channel identity or freshness is blocking SR4/SR6 desktop parity frontier closure."
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
payload["workflowReceiptReview"] = {
    "status": "pass" if not workflow_receipt_reasons else "fail",
    "summary": (
        "SR4, SR6, Chummer5a, and ruleset-adaptation receipts are present, passing, and timestamped."
        if not workflow_receipt_reasons
        else "One or more workflow or ruleset receipts are missing, stale, or not passing."
    ),
    "reasons": workflow_receipt_reasons,
    "receiptStatuses": receipt_statuses,
    "receiptGeneratedAts": receipt_generated_at_values,
}
payload["workflowCoverageReview"] = {
    "status": "pass" if not workflow_coverage_reasons else "fail",
    "summary": (
        "SR4, SR6, and Chummer5a workflow-family coverage is explicit with no missing families, tests, or parity receipts."
        if not workflow_coverage_reasons
        else "Workflow-family coverage still has explicit SR4/SR6/Chummer5a gaps."
    ),
    "reasons": workflow_coverage_reasons,
    "sr4": {
        "missingFamilyIds": sr4_missing_family_ids,
        "nonReadyFamilyIds": sr4_non_ready_family_ids,
        "missingTestRefKeys": sr4_missing_test_refs,
        "missingParityReceiptKeys": sr4_missing_parity_receipts,
        "failingParityReceiptKeys": sr4_failing_parity_receipts,
        "failingParityReceiptsExternalOnly": bool(sr4_evidence.get("failingParityReceiptsExternalOnly")),
    },
    "sr6": {
        "missingFamilyIds": sr6_missing_family_ids,
        "nonReadyFamilyIds": sr6_non_ready_family_ids,
        "missingTestRefKeys": sr6_missing_test_refs,
        "missingParityReceiptKeys": sr6_missing_parity_receipts,
        "failingParityReceiptKeys": sr6_failing_parity_receipts,
        "failingParityReceiptsExternalOnly": bool(sr6_evidence.get("failingParityReceiptsExternalOnly")),
    },
    "chummer5a": {
        "requiredFamilyCount": chummer5a_required_family_count,
        "ledgerFamilyCount": chummer5a_ledger_family_count,
        "missingFamilyIds": chummer5a_missing_family_ids,
        "nonReadyFamilyIds": chummer5a_non_ready_family_ids,
        "missingTestRefKeys": chummer5a_missing_test_refs,
        "tabsMissingInCatalog": chummer5a_tabs_missing_in_catalog,
        "workspaceActionsMissingInCatalog": chummer5a_workspace_actions_missing_in_catalog,
    },
}
payload["rulesetAdaptationReview"] = {
    "status": "pass" if not ruleset_adaptation_reasons else "fail",
    "summary": (
        "Ruleset/UI adaptation directives and regression proof are explicit for SR4, SR5, and SR6."
        if not ruleset_adaptation_reasons
        else "Ruleset/UI adaptation proof still has directive or regression gaps."
    ),
    "reasons": ruleset_adaptation_reasons,
    "failureCount": ruleset_failure_count,
    "requiredDirectives": required_directives,
    "missingDirectives": missing_directives,
    "unexpectedDirectives": unexpected_directives,
    "testBuildExit": ruleset_test_build_exit,
}
payload["channelAlignmentReview"] = {
    "status": "pass" if not channel_alignment_reasons else "fail",
    "summary": (
        "All downstream parity receipts are aligned to the same release-channel identity and provenance."
        if not channel_alignment_reasons
        else "One or more downstream parity receipts drift from the frontier release-channel identity."
    ),
    "reasons": channel_alignment_reasons,
    "releaseChannelPath": str(release_channel_path),
    "receiptChannelIds": receipt_channel_ids,
    "receiptReleaseChannelPaths": receipt_release_channel_paths,
    "receiptReleaseChannelChannelIds": receipt_release_channel_statuses,
    "receiptReleaseChannelGeneratedAts": receipt_release_channel_generated_ats,
}
payload["gateExecutionReview"] = {
    "status": "pass" if not gate_execution_reasons else "fail",
    "summary": (
        "All frontier prerequisite gates executed successfully."
        if not gate_execution_reasons
        else "One or more frontier prerequisite gates exited non-zero."
    ),
    "reasons": gate_execution_reasons,
    "gateExitCodes": {
        "sr4": sr4_exit,
        "sr6": sr6_exit,
        "chummer5a": chummer5a_exit,
        "rulesetUiAdaptation": ruleset_ui_adaptation_exit,
    },
}

payload["channelId"] = release_channel_channel_id
if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "SR4 and SR6 desktop parity frontier closure is explicitly proven by executable workflow, coverage, "
        "ruleset-adaptation, release-channel, and gate-execution subreviews."
    )

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[sr4-sr6-frontier] PASS"
