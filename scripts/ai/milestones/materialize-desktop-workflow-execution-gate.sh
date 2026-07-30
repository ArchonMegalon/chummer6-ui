#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
cd "$repo_root"

receipt_path="${CHUMMER_DESKTOP_WORKFLOW_EXECUTION_RECEIPT_PATH:-$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json}"
ui_workflow_parity_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_workflow_parity_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr_frontier_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
ruleset_ui_adaptation_path="${CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH:-$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json}"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
visual_familiarity_gate_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
chummer5a_screenshot_review_gate_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
next90_m141_direct_import_route_proof_path="$repo_root/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"
next90_m142_direct_workflow_proof_path="$repo_root/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"
sr4_ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
sr6_ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
flagship_product_readiness_materializer_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}"
human_side_rule_authority_approval_path="${CHUMMER_HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL_PATH:-/docker/chummercomplete/chummer-core-engine/.codex-studio/published/HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json}"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
# Deterministic release identity selection. An explicit path override wins at the
# assignment below; otherwise prefer the resolved hub-registry channel, then the
# verified local mirror, then the run-services portal copy, and finally the
# repo-bundled fallback. File mtimes must never change release identity.
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path_default="$verified_release_channel_path"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path_default="$run_services_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
refresh_dependency_receipts_override="${CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_RECEIPTS:-}"
skip_flagship_dependency_refresh="${CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_DEPENDENCY_REFRESH:-0}"
refresh_flagship_readiness="${CHUMMER_DESKTOP_WORKFLOW_REFRESH_FLAGSHIP_READINESS:-0}"
skip_flagship_readiness_refresh="${CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_READINESS_REFRESH:-0}"
if [[ -n "$refresh_dependency_receipts_override" ]]; then
  refresh_dependency_receipts="$refresh_dependency_receipts_override"
else
  refresh_dependency_receipts="0"
fi
if [[ "$skip_flagship_dependency_refresh" == "1" ]]; then
  refresh_dependency_receipts="0"
fi
if [[ "$skip_flagship_readiness_refresh" == "1" ]]; then
  refresh_flagship_readiness="0"
fi
dependency_refresh_timeout_seconds="${CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_TIMEOUT_SECONDS:-900}"
dependency_refresh_report_path="$(mktemp)"
dependency_refresh_timeout_seconds_requested="$dependency_refresh_timeout_seconds"
dependency_refresh_timeout_seconds_minimum=30
dependency_refresh_timeout_seconds_default=900
flagship_refresh_env=(
  "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS=0"
  "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS=1"
)
if [[ "$refresh_flagship_readiness" != "1" ]]; then
  flagship_refresh_env+=(
    "CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH=/dev/null"
  )
fi

mkdir -p "$(dirname "$receipt_path")"
trap 'rm -f "$dependency_refresh_report_path"' EXIT

if ! [[ "$dependency_refresh_timeout_seconds" =~ ^[0-9]+$ ]] || [[ "$dependency_refresh_timeout_seconds" -lt 1 ]]; then
  dependency_refresh_timeout_seconds="$dependency_refresh_timeout_seconds_default"
fi
if [[ "$dependency_refresh_timeout_seconds" -lt "$dependency_refresh_timeout_seconds_minimum" ]]; then
  dependency_refresh_timeout_seconds="$dependency_refresh_timeout_seconds_default"
fi

capture_receipt_generated_at() {
  local target_path="$1"
  python3 - <<'PY' "$target_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

target = Path(sys.argv[1])
if not target.is_file():
    raise SystemExit(0)

try:
    payload = json.loads(target.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

if isinstance(payload, dict):
    for key in ("generatedAt", "generated_at"):
        value = str(payload.get(key) or "").strip()
        if value:
            print(value)
            raise SystemExit(0)
PY
}

capture_receipt_mtime() {
  local target_path="$1"
  python3 - <<'PY' "$target_path"
from __future__ import annotations

import sys
from pathlib import Path

target = Path(sys.argv[1])
if not target.is_file():
    raise SystemExit(0)

print(int(target.stat().st_mtime))
PY
}

record_dependency_refresh_attempt() {
  local label="$1"
  local script_path="$2"
  local receipt_target="$3"
  local before_generated_at="$4"
  local after_generated_at="$5"
  local before_mtime="$6"
  local after_mtime="$7"
  local exit_code="$8"

  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
    "$label" \
    "$script_path" \
    "$receipt_target" \
    "$before_generated_at" \
    "$after_generated_at" \
    "$before_mtime" \
    "$after_mtime" \
    "$exit_code" >>"$dependency_refresh_report_path"
}

build_dependency_refresh_env() {
  local dependency_label="$1"
  local dependency_receipt_target="$2"

  local env_args=(
    "CHUMMER_HUB_REGISTRY_ROOT=$hub_registry_root"
    "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH=$release_channel_path"
    "CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_PATH=$release_channel_path"
    "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_ALLOW_VERIFY_RELEASE_CHANNEL_OVERRIDE=1"
    "CHUMMER_DESKTOP_WORKFLOW_ALLOW_VERIFY_RELEASE_CHANNEL_OVERRIDE=1"
  )

  case "$dependency_label" in
    desktop_visual_familiarity_gate)
      env_args+=(
        "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH=$release_channel_path"
        "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH=$dependency_receipt_target"
      )
      ;;
    ruleset_ui_adaptation)
      env_args+=(
        "CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH=$dependency_receipt_target"
      )
      ;;
    sr4_sr6_frontier)
      env_args+=(
        "CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=1"
      )
      ;;
    next90_m141_direct_import_route_proof)
      env_args+=(
        "CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH=$release_channel_path"
        "CHUMMER_NEXT90_M141_UI_RECEIPT_PATH=$dependency_receipt_target"
      )
      ;;
  esac

  printf '%s\n' "${env_args[@]}"
}

if [[ "$refresh_dependency_receipts" == "1" ]]; then
  while IFS='|' read -r dependency_label dependency_script dependency_receipt_target; do
    [[ -n "$dependency_label" && -n "$dependency_script" && -n "$dependency_receipt_target" ]] || continue
    if [[ ! -f "$dependency_script" ]]; then
      continue
    fi
    mapfile -t dependency_refresh_env < <(build_dependency_refresh_env "$dependency_label" "$dependency_receipt_target")
    before_generated_at="$(capture_receipt_generated_at "$dependency_receipt_target")"
    before_mtime="$(capture_receipt_mtime "$dependency_receipt_target")"
    dependency_exit_code=0
    set +e
    if command -v timeout >/dev/null 2>&1; then
      timeout --foreground "${dependency_refresh_timeout_seconds}s" env "${flagship_refresh_env[@]}" "${dependency_refresh_env[@]}" bash "$dependency_script" >/dev/null 2>&1
      dependency_exit_code=$?
    else
      env "${flagship_refresh_env[@]}" "${dependency_refresh_env[@]}" bash "$dependency_script" >/dev/null 2>&1
      dependency_exit_code=$?
    fi
    set -e
    after_generated_at="$(capture_receipt_generated_at "$dependency_receipt_target")"
    after_mtime="$(capture_receipt_mtime "$dependency_receipt_target")"
    record_dependency_refresh_attempt \
      "$dependency_label" \
      "$dependency_script" \
      "$dependency_receipt_target" \
      "$before_generated_at" \
      "$after_generated_at" \
      "$before_mtime" \
      "$after_mtime" \
      "$dependency_exit_code"
  done <<EOF
ui_flagship_release_gate|$repo_root/scripts/ai/milestones/b14-flagship-ui-release-gate.sh|$flagship_gate_path
desktop_visual_familiarity_gate|$repo_root/scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh|$visual_familiarity_gate_path
chummer5a_screenshot_review_gate|$repo_root/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh|$chummer5a_screenshot_review_gate_path
chummer5a_workflow_parity|$repo_root/scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh|$ui_workflow_parity_path
sr4_workflow_parity|$repo_root/scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh|$sr4_workflow_parity_path
sr6_workflow_parity|$repo_root/scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh|$sr6_workflow_parity_path
ruleset_ui_adaptation|$repo_root/scripts/ai/milestones/ruleset-ui-adaptation-check.sh|$ruleset_ui_adaptation_path
sr4_sr6_frontier|$repo_root/scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh|$sr_frontier_path
next90_m141_direct_import_route_proof|$repo_root/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh|$next90_m141_direct_import_route_proof_path
EOF
fi

python3 - <<'PY' "$receipt_path" "$ui_workflow_parity_path" "$sr4_workflow_parity_path" "$sr6_workflow_parity_path" "$sr_frontier_path" "$ruleset_ui_adaptation_path" "$flagship_gate_path" "$visual_familiarity_gate_path" "$chummer5a_screenshot_review_gate_path" "$next90_m141_direct_import_route_proof_path" "$next90_m142_direct_workflow_proof_path" "$sr4_ledger_path" "$sr6_ledger_path" "$repo_root" "$release_channel_path" "$dependency_refresh_report_path" "$dependency_refresh_timeout_seconds" "$dependency_refresh_timeout_seconds_requested" "$dependency_refresh_timeout_seconds_minimum" "$refresh_dependency_receipts" "$human_side_rule_authority_approval_path"
from __future__ import annotations

import hashlib
import json
import os
import stat
import sys
import tempfile
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple

REQUIRED_WORKFLOW_FAMILY_IDS = {
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
}
DIRECT_FLAGSHIP_WORKFLOW_FAMILY_IDS = {
    "metatype-priorities-karma-entry",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
}
DESKTOP_PROOF_MAX_AGE_SECONDS = 86400
DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS = 300
DESKTOP_EXECUTION_EPOCH_MAX_SPAN_SECONDS = 21600
MAX_REGULAR_INPUT_BYTES = 64 * 1024 * 1024
CANONICAL_LEDGER_SHA256 = {
    "sr4": "76267549b18bd866a7776f9d2792da6a613e1c47c2797ff1142d8b7f4531723d",
    "sr6": "f8bfb1cf834bd0f7679ca8336fe1e934d3906546521caa314655d59fbc4620c3",
}
CANONICAL_ORACLE_SHA256 = {
    "sr4": "c3d64935f7dd74ac4967ab8dd055daca825578279fc8fa2fe2ffdf9e0d7a5088",
    "sr6": "fbaf455e245219f0ff7f7fc0d82ee52ce3893fa1ddcdca6b61fc9a683ec8d587",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    try:
        loaded, _ = load_regular_json(path, "JSON receipt")
    except ValueError:
        return {}
    return loaded


def read_regular_bytes(path: Path, label: str) -> bytes:
    if path.is_symlink():
        raise ValueError(f"{label} must not be a symlink: {path}")
    try:
        fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    except OSError as exc:
        raise ValueError(f"{label} is missing or unreadable: {path}: {exc}") from exc
    try:
        before = os.fstat(fd)
        if not stat.S_ISREG(before.st_mode):
            raise ValueError(f"{label} is not a regular file: {path}")
        if before.st_size > MAX_REGULAR_INPUT_BYTES:
            raise ValueError(f"{label} exceeds the {MAX_REGULAR_INPUT_BYTES}-byte safety limit: {path}")
        chunks = []
        total_bytes = 0
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            total_bytes += len(chunk)
            if total_bytes > MAX_REGULAR_INPUT_BYTES:
                raise ValueError(
                    f"{label} exceeds the {MAX_REGULAR_INPUT_BYTES}-byte safety limit while reading: {path}"
                )
            chunks.append(chunk)
        after = os.fstat(fd)
    finally:
        os.close(fd)
    data = b"".join(chunks)
    if (
        before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or before.st_size != after.st_size
        or before.st_mtime_ns != after.st_mtime_ns
        or len(data) != after.st_size
    ):
        raise ValueError(f"{label} changed while it was being read: {path}")
    return data


def load_regular_json(path: Path, label: str) -> tuple[Dict[str, Any], bytes]:
    raw = read_regular_bytes(path, label)
    try:
        loaded = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{label} is not valid JSON: {path}") from exc
    if not isinstance(loaded, dict):
        raise ValueError(f"{label} root must be an object: {path}")
    return loaded, raw


def binding_for_bytes(path: Path, raw: bytes) -> Dict[str, Any]:
    return {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sizeBytes": len(raw),
    }


def file_binding(path: Path, label: str) -> Dict[str, Any]:
    return binding_for_bytes(path, read_regular_bytes(path, label))


def status_ok(value: Any) -> bool:
    return normalize_token(value) in {"pass", "passed", "ready"}


def human_side_rule_authority_approved(path: Path) -> tuple[bool, Dict[str, Any]]:
    receipt = load_json(path)
    rulesets = {
        str(item or "").strip().lower()
        for item in receipt.get("rulesets", [])
        if str(item or "").strip()
    }
    approved = status_ok(receipt.get("status")) and {"sr4", "sr6"}.issubset(rulesets)
    return approved, {
        "status": str(receipt.get("status") or "").strip() if receipt else "missing",
        "path": str(path),
        "reviewer": str(receipt.get("reviewer") or "").strip(),
        "rulesets": sorted(rulesets),
        "generatedAt": str(receipt.get("generated_at_utc") or receipt.get("generatedAt") or "").strip(),
    }


def pass_marker(value: Any) -> bool:
    return normalize_token(value) in {"pass", "passed", "ready", "true", "yes", "1"}


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def collect_external_blockers(evidence_payload: Dict[str, Any]) -> List[str]:
    return [
        normalize_token(value)
        for value in (
            (evidence_payload.get("verificationExternalBlockers") or [])
            + (evidence_payload.get("executionExternalBlockers") or [])
            + ([evidence_payload.get("external_blocker")] if evidence_payload.get("external_blocker") else [])
        )
        if normalize_token(value)
    ]


def external_blockers_are_only_missing_api_surface_contract(external_blockers: List[str]) -> bool:
    return bool(external_blockers) and all(
        blocker == "missing_api_surface_contract" for blocker in external_blockers
    )


def reason_targets_sr4_sr6_workflow_oracle_backlog(reason: str) -> bool:
    normalized = normalize_token(reason)
    if not normalized:
        return False
    if "missing_api_surface_contract" in normalized:
        return True
    if "workflow parity receipts require a chummer-api host" in normalized:
        return True
    if "family-level workflow receipts require a chummer-api host" in normalized:
        return True
    if "family-level execution receipts require a chummer-api host" in normalized:
        return True
    return False


def desktop_parity_receipt_is_external_only_missing_api_surface_contract(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    evidence_payload = (
        payload.get("evidence") if isinstance(payload.get("evidence"), dict) else {}
    )
    for field_name in ("failingParityReceiptsExternal", "failingParityReceipts"):
        field_value = evidence_payload.get(field_name)
        if isinstance(field_value, dict) and field_value:
            failure_tokens = [
                normalize_token(item)
                for values in field_value.values()
                if isinstance(values, list)
                for item in values
                if normalize_token(item)
            ]
            if failure_tokens and all(
                "external_blocker=missing_api_surface_contract" in token
                for token in failure_tokens
            ):
                return True
    reason_tokens = []
    if str(payload.get("reason") or "").strip():
        reason_tokens.append(normalize_token(payload.get("reason")))
    reason_tokens.extend(
        normalize_token(item)
        for item in (payload.get("reasons") or [])
        if normalize_token(item)
    )
    return bool(reason_tokens) and all(
        reason_targets_sr4_sr6_workflow_oracle_backlog(token)
        for token in reason_tokens
    )


def desktop_frontier_receipt_is_external_only_missing_api_surface_contract(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    evidence_payload = (
        payload.get("evidence") if isinstance(payload.get("evidence"), dict) else {}
    )
    sr4_status = normalize_token(evidence_payload.get("sr4Status"))
    sr6_status = normalize_token(evidence_payload.get("sr6Status"))
    sr4_is_external_only = any(
        "external blocker: missing_api_surface_contract" in normalize_token(reason)
        or "external_blocker=missing_api_surface_contract" in normalize_token(reason)
        for reason in (payload.get("reasons") or [])
    )
    sr4_pass_or_external_only = (
        not sr4_status
        or status_ok(sr4_status)
        or (sr4_status == "fail" and sr4_is_external_only)
    )
    if not sr4_pass_or_external_only:
        return False
    if status_ok(sr6_status):
        return False
    reason_tokens = [
        normalize_token(item)
        for item in (payload.get("reasons") or [])
        if normalize_token(item)
    ]
    allowed_reason_fragments = (
        "missing_api_surface_contract",
        "sr4 parity receipt has failing parity receipt proofs for",
        "sr4 parity gate exited non-zero",
        "sr4 parity receipt is not passing",
        "sr6 parity receipt has failing parity receipt proofs for",
        "sr6 parity gate exited non-zero",
        "sr6 parity receipt is not passing",
        "ruleset/ui adaptation receipt is not passing",
        "ruleset/ui adaptation receipt reports failurecount=",
        "ruleset/ui adaptation gate exited non-zero",
        "chummer5a parity receipt is not passing",
        "chummer5a parity gate exited non-zero",
    )
    return bool(reason_tokens) and all(
        any(fragment in token for fragment in allowed_reason_fragments)
        for token in reason_tokens
    )


def flagship_gate_is_route_local_only(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    blocking_findings = payload.get("blockingFindings")
    if not isinstance(blocking_findings, list) or not blocking_findings:
        return False
    allowed_findings = {
        "Top-level release gate cannot pass while flagship readiness is not passed.",
        "Top-level release gate cannot pass while flagship readiness coverage.desktop_client is not ready.",
        "Top-level release gate cannot pass while flagship readiness still has open coverage keys: desktop_client.",
    }
    return all(str(finding).strip() in allowed_findings for finding in blocking_findings)


def flagship_gate_is_external_desktop_only(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    blocking_findings = payload.get("blockingFindings")
    if not isinstance(blocking_findings, list) or not blocking_findings:
        return False
    allowed_findings = {
        "Top-level release gate cannot pass while desktop executable exit gate is not passed.",
        "Top-level release gate cannot pass while flagship readiness is not passed.",
        "Top-level release gate cannot pass while flagship readiness coverage.desktop_client is not ready.",
        "Top-level release gate cannot pass while flagship readiness still has open coverage keys: desktop_client.",
    }
    if any(str(finding).strip() not in allowed_findings for finding in blocking_findings):
        return False
    desktop_executable_proof = payload.get("desktopExecutableProof")
    if not isinstance(desktop_executable_proof, dict):
        return False
    local_blocking_findings = desktop_executable_proof.get("localBlockingFindings")
    if not isinstance(local_blocking_findings, list):
        return False
    normalized_local_blocking_findings = [
        str(finding).strip() for finding in local_blocking_findings if str(finding).strip()
    ]
    if not normalized_local_blocking_findings:
        return True
    allowed_local_findings = {
        "Windows desktop exit gate requires a Windows-capable host; current host cannot run promoted Windows installer smoke.",
        "Windows gate reason: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",
    }
    return all(finding in allowed_local_findings for finding in normalized_local_blocking_findings)


def screenshot_review_gate_is_effectively_passing(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False

    reasons = [
        str(reason).strip()
        for reason in (payload.get("reasons") or [])
        if str(reason).strip()
    ]
    if not reasons or any(reason != "UI flagship release gate is not passing." for reason in reasons):
        return False

    supporting_receipt_review = (
        payload.get("supportingReceiptReview")
        if isinstance(payload.get("supportingReceiptReview"), dict)
        else {}
    )
    supporting_reasons = [
        str(reason).strip()
        for reason in (supporting_receipt_review.get("reasons") or [])
        if str(reason).strip()
    ]
    if any(reason != "UI flagship release gate is not passing." for reason in supporting_reasons):
        return False

    review_jobs_summary = (
        payload.get("reviewJobsSummary")
        if isinstance(payload.get("reviewJobsSummary"), dict)
        else {}
    )
    screenshot_asset_review = (
        payload.get("screenshotAssetReview")
        if isinstance(payload.get("screenshotAssetReview"), dict)
        else {}
    )
    feedback_closure_review = (
        payload.get("feedbackClosureReview")
        if isinstance(payload.get("feedbackClosureReview"), dict)
        else {}
    )
    if (
        not status_ok(review_jobs_summary.get("status"))
        or not status_ok(screenshot_asset_review.get("status"))
        or not status_ok(feedback_closure_review.get("status"))
    ):
        return False

    visual_review_statuses = (
        supporting_receipt_review.get("visualReviewStatuses")
        if isinstance(supporting_receipt_review.get("visualReviewStatuses"), dict)
        else {}
    )
    return all(status_ok(value) for value in visual_review_statuses.values())


def visual_familiarity_gate_is_effectively_passing(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False

    reasons = [
        str(reason).strip()
        for reason in (payload.get("reasons") or [])
        if str(reason).strip()
    ]
    if not reasons or any(reason != "Flagship UI release gate is missing or not passing." for reason in reasons):
        return False

    reviews = payload.get("reviews") if isinstance(payload.get("reviews"), dict) else {}
    required_pass_reviews = (
        "headProofReview",
        "interactionProofReview",
        "sourceAnchorReview",
        "screenCaptureReview",
        "legacyFamiliarityReview",
    )
    return all(
        isinstance(reviews.get(review_name), dict)
        and status_ok(reviews[review_name].get("status"))
        for review_name in required_pass_reviews
    )


def normalize_head_proof_statuses(
    values: Any,
    field_label: str,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> Dict[str, str]:
    if values is None:
        return {}
    if not isinstance(values, dict):
        reasons.append(f"{field_label} must be an object when present.")
        return {}
    normalized: Dict[str, str] = {}
    malformed_entries: List[str] = []
    non_canonical_keys: List[str] = []
    duplicate_normalized_keys: List[str] = []
    for raw_key, raw_proof in values.items():
        if not isinstance(raw_key, str):
            malformed_entries.append("<non-string-key>")
            reasons.append(f"{field_label} contains a non-string key.")
            continue
        if raw_key != raw_key.strip():
            malformed_entries.append(raw_key)
            reasons.append(f"{field_label} contains a key with leading/trailing whitespace: {raw_key!r}.")
            continue
        key = normalize_token(raw_key)
        if not key:
            malformed_entries.append(raw_key)
            reasons.append(f"{field_label} contains a blank key.")
            continue
        if raw_key != key:
            malformed_entries.append(raw_key)
            non_canonical_keys.append(raw_key)
            reasons.append(
                f"{field_label} contains a non-canonical key '{raw_key}' (expected '{key}')."
            )
            continue
        if key in normalized:
            malformed_entries.append(key)
            duplicate_normalized_keys.append(key)
            reasons.append(f"{field_label} contains duplicate normalized key '{key}'.")
            continue
        if not isinstance(raw_proof, dict):
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a non-object proof payload for key '{key}'.")
            continue
        raw_status = raw_proof.get("status")
        if raw_status is None:
            normalized[key] = ""
            continue
        if not isinstance(raw_status, str):
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a non-string status for key '{key}'.")
            continue
        if raw_status != raw_status.strip():
            malformed_entries.append(key)
            reasons.append(
                f"{field_label} contains a status with leading/trailing whitespace for key '{key}'."
            )
            continue
        normalized[key] = normalize_token(raw_status)
    evidence[f"{field_label}_normalized"] = normalized
    evidence[f"{field_label}_malformed_entries"] = sorted(set(malformed_entries))
    evidence[f"{field_label}_non_canonical_keys"] = sorted(set(non_canonical_keys))
    evidence[f"{field_label}_duplicate_normalized_keys"] = sorted(set(duplicate_normalized_keys))
    return normalized


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def workflow_receipt_targets_direct_flagship_slice(entry: str) -> bool:
    lowered = normalize_token(entry)
    return any(family_id in lowered for family_id in DIRECT_FLAGSHIP_WORKFLOW_FAMILY_IDS)


def parse_iso(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    return parsed.astimezone(timezone.utc)


def payload_generated_at(payload: Dict[str, Any]) -> tuple[str, datetime | None]:
    for key in ("generated_at", "generatedAt"):
        if key in payload:
            raw = str(payload.get(key) or "").strip()
            return raw, parse_iso(raw)
    return "", None


def validate_receipt_freshness(
    label: str,
    payload: Dict[str, Any],
    reasons: List[str],
    evidence: Dict[str, Any],
    *,
    allow_stale_pass_receipt: bool = False,
) -> None:
    generated_at_raw, generated_at = payload_generated_at(payload)
    evidence[f"{label}_generated_at"] = generated_at_raw
    if not generated_at_raw or generated_at is None:
        reasons.append(f"{label} receipt is missing a valid generatedAt/generated_at timestamp.")
        return
    age_seconds = int((datetime.now(timezone.utc) - generated_at).total_seconds())
    if age_seconds < 0:
        future_skew_seconds = abs(age_seconds)
        evidence[f"{label}_future_skew_seconds"] = future_skew_seconds
        if future_skew_seconds > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
            reasons.append(
                f"{label} receipt generatedAt is in the future ({future_skew_seconds}s ahead; max {DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS}s)."
            )
        age_seconds = 0
    evidence[f"{label}_age_seconds"] = age_seconds
    if age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        status = str(payload.get("status") or "").strip().lower()
        evidence[f"{label}_stale_pass_receipt_allowed"] = allow_stale_pass_receipt and status_ok(status)
        if not (allow_stale_pass_receipt and status_ok(status)):
            reasons.append(
                f"{label} receipt is stale ({age_seconds}s old; max {DESKTOP_PROOF_MAX_AGE_SECONDS}s)."
            )


def check_receipt(
    path: Path,
    label: str,
    reasons: List[str],
    evidence: Dict[str, Any],
    *,
    expected_contract: str,
    allow_stale_pass_receipt: bool = False,
    require_passing_receipt: bool = True,
) -> Dict[str, Any]:
    try:
        payload, receipt_bytes = load_regular_json(path, f"{label} receipt")
    except ValueError as exc:
        payload = {}
        evidence[f"{label}_load_error"] = str(exc)
    else:
        binding = binding_for_bytes(path, receipt_bytes)
        upstream_receipt_bindings[str(path.resolve())] = binding
        evidence[f"{label}_binding"] = binding
    status = str(payload.get("status") or "").strip().lower()
    contract_aliases = {
        key: str(payload.get(key) or "").strip()
        for key in ("contract_name", "contractName")
        if key in payload
    }
    contract_name = next(iter(contract_aliases.values()), "")
    evidence[f"{label}_path"] = str(path)
    evidence[f"{label}_status"] = status
    evidence[f"{label}_contract_name"] = contract_name
    if (
        not contract_aliases
        or any(value != expected_contract for value in contract_aliases.values())
    ):
        reasons.append(
            f"{label} receipt contract identity must equal {expected_contract}."
        )
    if require_passing_receipt and status != "pass":
        reasons.append(f"{label} receipt is missing or not passing.")
    validate_receipt_freshness(
        label,
        payload,
        reasons,
        evidence,
        allow_stale_pass_receipt=allow_stale_pass_receipt,
    )
    return payload


def add_dependency_refresh_failure_reason(
    label: str,
    payload: Dict[str, Any],
    reasons: List[str],
) -> None:
    attempt = dependency_refresh_attempts.get(label)
    if not attempt:
        return

    exit_code = int(attempt.get("exit_code") or 0)
    receipt_timestamp_changed = bool(attempt.get("receipt_timestamp_changed"))
    receipt_mtime_changed = bool(attempt.get("receipt_mtime_changed"))
    generated_at_raw, generated_at = payload_generated_at(payload)
    status = str(payload.get("status") or "").strip().lower()
    receipt_is_stale = False
    if generated_at_raw and generated_at is not None:
        receipt_is_stale = int((datetime.now(timezone.utc) - generated_at).total_seconds()) > DESKTOP_PROOF_MAX_AGE_SECONDS

    if exit_code != 0 and (
        label in {"sr4_workflow_parity", "sr6_workflow_parity"}
        or receipt_is_stale
        or not status_ok(status)
    ):
        timeout_suffix = " after timing out" if attempt.get("timed_out") else ""
        reasons.append(
            f"{label} dependency refresh failed via {attempt['script_path']} with exit {exit_code}{timeout_suffix}."
        )
    elif receipt_is_stale and not receipt_timestamp_changed and not receipt_mtime_changed:
        reasons.append(
            f"{label} dependency refresh did not update receipt timestamp or mtime: {attempt['receipt_path']}."
        )


def iter_ledger_receipts(ledger_payload: Dict[str, Any]) -> Iterable[Tuple[str, str, str]]:
    for family in ledger_payload.get("requiredFamilies") or []:
        if not isinstance(family, dict):
            continue
        family_id = str(family.get("id") or "").strip()
        if not family_id:
            continue
        for key in ("parityReceipts", "verificationReceipts", "executionReceipts"):
            values = family.get(key)
            if not isinstance(values, list):
                continue
            for raw in values:
                rel_path = str(raw or "").strip().replace("{familyId}", family_id)
                if rel_path:
                    yield family_id, key, rel_path


def iter_execution_receipts(ledger_payload: Dict[str, Any]) -> Iterable[Tuple[str, List[str], str]]:
    for family in ledger_payload.get("requiredFamilies") or []:
        if not isinstance(family, dict):
            continue
        family_id = str(family.get("id") or "").strip()
        if not family_id:
            continue
        audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
        for raw in family.get("executionReceipts") or []:
            rel_path = str(raw or "").strip().replace("{familyId}", family_id)
            if rel_path:
                yield family_id, audit_tests, rel_path


def collect_family_state(ledger_payload: Dict[str, Any]) -> Dict[str, Dict[str, Any]]:
    family_state: Dict[str, Dict[str, Any]] = {}
    for family in ledger_payload.get("requiredFamilies") or []:
        if not isinstance(family, dict):
            continue
        family_id = str(family.get("id") or "").strip()
        if not family_id:
            continue
        family_state[family_id] = family
    return family_state


def collect_release_channel_head_requirements(release_channel_payload: Dict[str, Any]) -> Dict[str, List[str]]:
    tuple_coverage = (
        release_channel_payload.get("desktopTupleCoverage")
        if isinstance(release_channel_payload.get("desktopTupleCoverage"), dict)
        else {}
    )
    required_heads: set[str] = set()
    primary_heads: set[str] = set()
    promoted_non_fallback_heads: set[str] = set()
    for raw_head in tuple_coverage.get("requiredDesktopHeads") or []:
        head = normalize_token(raw_head)
        if head:
            required_heads.add(head)
    for route_truth_row in tuple_coverage.get("desktopRouteTruth") or []:
        if not isinstance(route_truth_row, dict):
            continue
        head = normalize_token(route_truth_row.get("head"))
        if not head:
            continue
        route_role = normalize_token(route_truth_row.get("routeRole"))
        parity_posture = normalize_token(route_truth_row.get("parityPosture"))
        promotion_state = normalize_token(route_truth_row.get("promotionState"))
        if route_role == "primary" or parity_posture == "flagship_primary":
            required_heads.add(head)
            primary_heads.add(head)
            continue
        if promotion_state == "promoted" and route_role != "fallback" and parity_posture != "explicit_fallback":
            required_heads.add(head)
            promoted_non_fallback_heads.add(head)
    return {
        "required_heads": sorted(required_heads),
        "primary_heads": sorted(primary_heads),
        "promoted_non_fallback_heads": sorted(promoted_non_fallback_heads),
    }


(
    receipt_path_text,
    ui_workflow_parity_path_text,
    sr4_workflow_parity_path_text,
    sr6_workflow_parity_path_text,
    sr_frontier_path_text,
    ruleset_ui_adaptation_path_text,
    flagship_gate_path_text,
    visual_familiarity_gate_path_text,
    chummer5a_screenshot_review_gate_path_text,
    next90_m141_direct_import_route_proof_path_text,
    next90_m142_direct_workflow_proof_path_text,
    sr4_ledger_path_text,
    sr6_ledger_path_text,
    repo_root_text,
    release_channel_path_text,
    dependency_refresh_report_path_text,
    dependency_refresh_timeout_seconds_text,
    dependency_refresh_timeout_seconds_requested_text,
    dependency_refresh_timeout_seconds_minimum_text,
    refresh_dependency_receipts_text,
    human_side_rule_authority_approval_path_text,
) = sys.argv[1:22]

receipt_path = Path(receipt_path_text)
ui_workflow_parity_path = Path(ui_workflow_parity_path_text)
sr4_workflow_parity_path = Path(sr4_workflow_parity_path_text)
sr6_workflow_parity_path = Path(sr6_workflow_parity_path_text)
sr_frontier_path = Path(sr_frontier_path_text)
ruleset_ui_adaptation_path = Path(ruleset_ui_adaptation_path_text)
flagship_gate_path = Path(flagship_gate_path_text)
visual_familiarity_gate_path = Path(visual_familiarity_gate_path_text)
chummer5a_screenshot_review_gate_path = Path(chummer5a_screenshot_review_gate_path_text)
next90_m141_direct_import_route_proof_path = Path(next90_m141_direct_import_route_proof_path_text)
next90_m142_direct_workflow_proof_path = Path(next90_m142_direct_workflow_proof_path_text)
sr4_ledger_path = Path(sr4_ledger_path_text)
sr6_ledger_path = Path(sr6_ledger_path_text)
repo_root = Path(repo_root_text)
trx_contract_source_path = (
    repo_root / "scripts" / "ai" / "milestones" / "workflow_family_trx_contract.py"
)
sys.path.insert(0, str(trx_contract_source_path.parent))
from workflow_family_trx_contract import (
    build_desktop_execution_epoch,
    snapshot_output_tree,
    validate_api_probe_contract,
    validate_trx_contract,
    validate_trx_record_contract,
    validate_workflow_stage_manifest,
    workflow_stage_manifest_path,
)
release_channel_path = Path(release_channel_path_text)
dependency_refresh_report_path = Path(dependency_refresh_report_path_text)
dependency_refresh_timeout_seconds = int(dependency_refresh_timeout_seconds_text)
dependency_refresh_timeout_seconds_requested = dependency_refresh_timeout_seconds_requested_text
dependency_refresh_timeout_seconds_minimum = int(dependency_refresh_timeout_seconds_minimum_text)
refresh_dependency_receipts = normalize_token(refresh_dependency_receipts_text) == "1"
human_side_rule_authority_approval_path = Path(human_side_rule_authority_approval_path_text)

reasons: List[str] = []
evidence: Dict[str, Any] = {}
upstream_receipt_bindings: Dict[str, Dict[str, Any]] = {}
evidence["proof_freshness_max_age_seconds"] = DESKTOP_PROOF_MAX_AGE_SECONDS
evidence["proof_freshness_max_future_skew_seconds"] = DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS
evidence["release_channel_path"] = str(release_channel_path)
evidence["dependency_refresh_enabled"] = refresh_dependency_receipts
evidence["dependency_refresh_timeout_seconds"] = dependency_refresh_timeout_seconds
evidence["dependency_refresh_timeout_seconds_requested"] = dependency_refresh_timeout_seconds_requested
evidence["dependency_refresh_timeout_seconds_minimum"] = dependency_refresh_timeout_seconds_minimum
evidence["dependency_refresh_timeout_seconds_was_clamped"] = (
    dependency_refresh_timeout_seconds != int(dependency_refresh_timeout_seconds_requested)
    if str(dependency_refresh_timeout_seconds_requested).isdigit()
    else True
)
human_side_rule_authority_is_approved, human_side_rule_authority_receipt = human_side_rule_authority_approved(
    human_side_rule_authority_approval_path
)
evidence["human_side_rule_authority_approved"] = human_side_rule_authority_is_approved
evidence["human_side_rule_authority_approval"] = human_side_rule_authority_receipt
evidence["human_side_rule_authority_execution_waiver_enabled"] = False

dependency_refresh_attempts: Dict[str, Dict[str, Any]] = {}
if dependency_refresh_report_path.is_file():
    for raw_line in dependency_refresh_report_path.read_text(encoding="utf-8").splitlines():
        if not raw_line.strip():
            continue
        parts = raw_line.split("\t")
        if len(parts) != 8:
            continue
        (
            label,
            script_path,
            receipt_refresh_path,
            before_generated_at,
            after_generated_at,
            before_mtime,
            after_mtime,
            exit_code_text,
        ) = parts
        try:
            exit_code = int(exit_code_text)
        except ValueError:
            exit_code = 255
        dependency_refresh_attempts[label] = {
            "script_path": script_path,
            "receipt_path": receipt_refresh_path,
            "before_generated_at": before_generated_at,
            "after_generated_at": after_generated_at,
            "before_mtime": before_mtime,
            "after_mtime": after_mtime,
            "exit_code": exit_code,
            "timed_out": exit_code == 124,
            "receipt_timestamp_changed": before_generated_at != after_generated_at,
            "receipt_mtime_changed": before_mtime != after_mtime,
        }
evidence["dependency_refresh_attempts"] = dependency_refresh_attempts

chummer5a_workflow_parity = check_receipt(
    ui_workflow_parity_path,
    "chummer5a_workflow_parity",
    reasons,
    evidence,
    expected_contract="chummer6-ui.chummer5a_desktop_workflow_parity",
)
sr4_workflow_parity = check_receipt(
    sr4_workflow_parity_path,
    "sr4_workflow_parity",
    reasons,
    evidence,
    expected_contract="chummer6-ui.sr4_desktop_workflow_parity",
)
sr6_workflow_parity = check_receipt(
    sr6_workflow_parity_path,
    "sr6_workflow_parity",
    reasons,
    evidence,
    expected_contract="chummer6-ui.sr6_desktop_workflow_parity",
)
sr4_sr6_frontier = check_receipt(
    sr_frontier_path,
    "sr4_sr6_frontier",
    reasons,
    evidence,
    expected_contract="chummer6-ui.sr4_sr6_desktop_parity_frontier",
)
ruleset_ui_adaptation = check_receipt(
    ruleset_ui_adaptation_path,
    "ruleset_ui_adaptation",
    reasons,
    evidence,
    expected_contract="chummer6-ui.ruleset_ui_adaptation_frontier",
)
flagship_gate = check_receipt(
    flagship_gate_path,
    "ui_flagship_release_gate",
    reasons,
    evidence,
    expected_contract="chummer6-ui.flagship_ui_release_gate",
    require_passing_receipt=False,
)
visual_familiarity_gate = check_receipt(
    visual_familiarity_gate_path,
    "desktop_visual_familiarity_gate",
    reasons,
    evidence,
    expected_contract="chummer6-ui.desktop_visual_familiarity_exit_gate",
)
chummer5a_screenshot_review_gate = check_receipt(
    chummer5a_screenshot_review_gate_path,
    "chummer5a_screenshot_review_gate",
    reasons,
    evidence,
    expected_contract="chummer6-ui.chummer5a_screenshot_review_gate",
)
next90_m141_direct_import_route_proof = check_receipt(
    next90_m141_direct_import_route_proof_path,
    "next90_m141_direct_import_route_proof",
    reasons,
    evidence,
    expected_contract="chummer6-ui.next90_m141_ui_direct_import_route_proof",
)
m142_observation_reasons: List[str] = []
next90_m142_direct_workflow_proof = check_receipt(
    next90_m142_direct_workflow_proof_path,
    "next90_m142_direct_workflow_proof",
    m142_observation_reasons,
    evidence,
    expected_contract="chummer6-ui.next90_m142_ui_direct_workflow_proof",
    require_passing_receipt=False,
)
next90_m142_generated_at = str(
    evidence.get("next90_m142_direct_workflow_proof_generated_at") or ""
).strip()
next90_m142_age_seconds = evidence.get(
    "next90_m142_direct_workflow_proof_age_seconds"
)
next90_m142_future_skew_seconds = evidence.get(
    "next90_m142_direct_workflow_proof_future_skew_seconds"
)
next90_m142_direct_workflow_proof_is_fresh_pass = (
    status_ok(next90_m142_direct_workflow_proof.get("status"))
    and bool(next90_m142_generated_at)
    and isinstance(next90_m142_age_seconds, int)
    and next90_m142_age_seconds <= DESKTOP_PROOF_MAX_AGE_SECONDS
    and (
        not isinstance(next90_m142_future_skew_seconds, int)
        or next90_m142_future_skew_seconds
        <= DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS
    )
)
evidence["next90_m142_direct_workflow_proof_is_fresh_pass"] = (
    next90_m142_direct_workflow_proof_is_fresh_pass
)
evidence["downstream_receipt_observations"] = {
    "next90_m142_direct_workflow_proof": {
        "path": str(next90_m142_direct_workflow_proof_path),
        "status": str(next90_m142_direct_workflow_proof.get("status") or "").strip(),
        "freshPass": next90_m142_direct_workflow_proof_is_fresh_pass,
        "reasons": m142_observation_reasons,
    }
}
for dependency_label, dependency_payload in (
    ("chummer5a_workflow_parity", chummer5a_workflow_parity),
    ("sr4_workflow_parity", sr4_workflow_parity),
    ("sr6_workflow_parity", sr6_workflow_parity),
    ("sr4_sr6_frontier", sr4_sr6_frontier),
    ("ruleset_ui_adaptation", ruleset_ui_adaptation),
    ("desktop_visual_familiarity_gate", visual_familiarity_gate),
    ("chummer5a_screenshot_review_gate", chummer5a_screenshot_review_gate),
    ("next90_m141_direct_import_route_proof", next90_m141_direct_import_route_proof),
):
    add_dependency_refresh_failure_reason(
        dependency_label,
        dependency_payload,
        reasons,
    )
sr6_workflow_parity_external_only = (
    not status_ok(str(evidence.get("sr6_workflow_parity_status") or ""))
    and desktop_parity_receipt_is_external_only_missing_api_surface_contract(sr6_workflow_parity)
)
sr4_workflow_parity_external_only = (
    not status_ok(str(evidence.get("sr4_workflow_parity_status") or ""))
    and desktop_parity_receipt_is_external_only_missing_api_surface_contract(sr4_workflow_parity)
)
sr4_sr6_frontier_external_only = (
    not status_ok(str(evidence.get("sr4_sr6_frontier_status") or ""))
    and desktop_frontier_receipt_is_external_only_missing_api_surface_contract(sr4_sr6_frontier)
)
evidence["sr4_workflow_parity_external_only_detected"] = sr4_workflow_parity_external_only
evidence["sr6_workflow_parity_external_only_detected"] = sr6_workflow_parity_external_only
evidence["sr4_sr6_frontier_external_only_detected"] = sr4_sr6_frontier_external_only
evidence["sr4_workflow_parity_external_only_deferred"] = False
evidence["sr6_workflow_parity_external_only_deferred"] = False
evidence["sr4_sr6_frontier_external_only_deferred"] = False
evidence["sr4_workflow_parity_effective_status"] = str(
    evidence.get("sr4_workflow_parity_status") or ""
)
evidence["sr6_workflow_parity_effective_status"] = str(
    evidence.get("sr6_workflow_parity_status") or ""
)
evidence["sr4_sr6_frontier_effective_status"] = str(
    evidence.get("sr4_sr6_frontier_status") or ""
)
flagship_gate_route_local_only = (
    not status_ok(str(evidence.get("ui_flagship_release_gate_status") or ""))
    and flagship_gate_is_route_local_only(flagship_gate)
)
flagship_gate_external_desktop_only = (
    not status_ok(str(evidence.get("ui_flagship_release_gate_status") or ""))
    and flagship_gate_is_external_desktop_only(flagship_gate)
)
evidence["ui_flagship_release_gate_route_local_only"] = flagship_gate_route_local_only
evidence["ui_flagship_release_gate_external_desktop_only"] = flagship_gate_external_desktop_only
evidence["ui_flagship_release_gate_external_desktop_only_deferred"] = False
evidence["ui_flagship_release_gate_effective_status"] = str(
    evidence.get("ui_flagship_release_gate_status") or ""
)
visual_familiarity_gate_effective_pass = (
    not status_ok(str(evidence.get("desktop_visual_familiarity_gate_status") or ""))
    and visual_familiarity_gate_is_effectively_passing(visual_familiarity_gate)
)
evidence["desktop_visual_familiarity_gate_effective_status"] = str(
    evidence.get("desktop_visual_familiarity_gate_status") or ""
)
screenshot_review_gate_effective_pass = (
    not status_ok(str(evidence.get("chummer5a_screenshot_review_gate_status") or ""))
    and screenshot_review_gate_is_effectively_passing(chummer5a_screenshot_review_gate)
)
evidence["chummer5a_screenshot_review_gate_effective_status"] = str(
    evidence.get("chummer5a_screenshot_review_gate_status") or ""
)
release_channel: Dict[str, Any] = {}
release_channel_bytes = b""
release_channel_load_error = ""
try:
    release_channel, release_channel_bytes = load_regular_json(
        release_channel_path, "desktop workflow release channel receipt"
    )
except ValueError as exc:
    release_channel_load_error = str(exc)
release_channel_exists = bool(release_channel_bytes)
release_channel_contract_name = str(release_channel.get("contract_name") or "").strip()
release_channel_status = normalize_token(release_channel.get("status"))
release_channel_channel_id_value = str(release_channel.get("channelId") or "").strip()
release_channel_channel_alias = str(release_channel.get("channel") or "").strip()
if (
    release_channel_channel_id_value
    and release_channel_channel_alias
    and release_channel_channel_id_value.lower() != release_channel_channel_alias.lower()
):
    reasons.append("Desktop workflow execution gate release channel carries conflicting channelId/channel aliases.")
release_channel_channel_id = normalize_token(
    release_channel_channel_id_value or release_channel_channel_alias
)
release_channel_version_value = str(release_channel.get("releaseVersion") or "").strip()
release_channel_version_alias = str(release_channel.get("version") or "").strip()
if (
    release_channel_version_value
    and release_channel_version_alias
    and release_channel_version_value != release_channel_version_alias
):
    reasons.append("Desktop workflow execution gate release channel carries conflicting releaseVersion/version aliases.")
release_channel_version = release_channel_version_value or release_channel_version_alias
release_generated_at_value = release_channel.get("generatedAt")
release_generated_at_alias = release_channel.get("generated_at")
if (
    release_generated_at_value is not None
    and release_generated_at_alias is not None
    and release_generated_at_value != release_generated_at_alias
):
    reasons.append("Desktop workflow execution gate release channel carries conflicting generatedAt/generated_at aliases.")
release_channel_generated_at_raw = str(
    release_generated_at_value or release_generated_at_alias or ""
).strip()
release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
release_identity: Dict[str, Any] = {}
if (
    release_channel_exists
    and release_channel_channel_id
    and release_channel_version
    and release_channel_generated_at_raw
    and release_channel_generated_at is not None
):
    release_identity = {
        "channelId": release_channel_channel_id,
        "releaseVersion": release_channel_version,
        "generatedAt": release_channel_generated_at_raw,
        **binding_for_bytes(release_channel_path, release_channel_bytes),
    }
evidence["release_channel_receipt_exists"] = release_channel_exists
evidence["release_channel_contract_name"] = release_channel_contract_name
evidence["release_channel_status"] = release_channel_status
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version
evidence["release_channel_generated_at"] = release_channel_generated_at_raw
if release_channel_load_error:
    reasons.append(
        "Desktop workflow execution gate release channel receipt is unsafe or unreadable: "
        + release_channel_load_error
    )
if release_channel_contract_name != "Chummer.Hub.Registry.Contracts":
    reasons.append(
        "Desktop workflow execution gate release channel contract_name is not recognized."
    )
if release_channel_status != "published":
    reasons.append(
        "Desktop workflow execution gate release channel status is not published."
    )
if not release_channel_channel_id:
    reasons.append(
        "Desktop workflow execution gate release channel receipt is missing channelId/channel."
    )
if not release_channel_version:
    reasons.append(
        "Desktop workflow execution gate release channel receipt is missing version."
    )
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    reasons.append(
        "Desktop workflow execution gate release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )
if release_channel_generated_at is not None:
    release_channel_age_seconds = int(
        (datetime.now(timezone.utc) - release_channel_generated_at).total_seconds()
    )
    if release_channel_age_seconds < 0:
        release_channel_future_skew_seconds = abs(release_channel_age_seconds)
        evidence["release_channel_future_skew_seconds"] = release_channel_future_skew_seconds
        if release_channel_future_skew_seconds > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
            reasons.append(
                "Desktop workflow execution gate release channel receipt generatedAt is in the future "
                f"({release_channel_future_skew_seconds}s ahead; max {DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS}s)."
            )
        release_channel_age_seconds = 0
    evidence["release_channel_age_seconds"] = release_channel_age_seconds
    # This is the immutable release publication timestamp. Nested releaseProof
    # evidence owns freshness; publication age remains diagnostic-only.
release_channel_head_requirements = collect_release_channel_head_requirements(release_channel)
evidence["release_channel_required_desktop_heads"] = (
    release_channel_head_requirements["required_heads"]
)
evidence["release_channel_primary_desktop_heads"] = (
    release_channel_head_requirements["primary_heads"]
)
evidence["release_channel_promoted_non_fallback_desktop_heads"] = (
    release_channel_head_requirements["promoted_non_fallback_heads"]
)

receipt_channel_ids: Dict[str, str] = {}
receipt_release_versions: Dict[str, str] = {}
for label, payload in (
    ("chummer5a_workflow_parity", chummer5a_workflow_parity),
    ("sr4_workflow_parity", sr4_workflow_parity),
    ("sr6_workflow_parity", sr6_workflow_parity),
    ("sr4_sr6_frontier", sr4_sr6_frontier),
    ("ruleset_ui_adaptation", ruleset_ui_adaptation),
    ("ui_flagship_release_gate", flagship_gate),
    ("desktop_visual_familiarity_gate", visual_familiarity_gate),
    ("chummer5a_screenshot_review_gate", chummer5a_screenshot_review_gate),
    ("next90_m141_direct_import_route_proof", next90_m141_direct_import_route_proof),
):
    channel_aliases = {
        key: str(payload.get(key) or "").strip()
        for key in ("channelId", "channel")
        if key in payload
    }
    if (
        not channel_aliases
        or any(not value for value in channel_aliases.values())
        or len({value.lower() for value in channel_aliases.values()}) != 1
    ):
        channel_id = ""
        reasons.append(f"{label} receipt channelId/channel aliases are missing or conflicting.")
    else:
        channel_id = normalize_token(next(iter(channel_aliases.values())))
    receipt_channel_ids[label] = channel_id
    if not channel_id:
        reasons.append(f"{label} receipt is missing channelId/channel.")
    elif release_channel_channel_id and channel_id != release_channel_channel_id:
        reasons.append(
            f"{label} receipt channelId does not match desktop workflow execution release-channel channelId."
        )
    version_aliases = {
        key: str(payload.get(key) or "").strip()
        for key in ("releaseVersion", "version")
        if key in payload
    }
    if (
        not version_aliases
        or any(not value for value in version_aliases.values())
        or len(set(version_aliases.values())) != 1
    ):
        receipt_version = ""
        reasons.append(f"{label} receipt releaseVersion/version aliases are missing or conflicting.")
    else:
        receipt_version = next(iter(version_aliases.values()))
    receipt_release_versions[label] = receipt_version
    if release_channel_version and receipt_version != release_channel_version:
        reasons.append(
            f"{label} receipt releaseVersion does not match desktop workflow execution release-channel version."
        )
evidence["workflow_parity_receipt_channel_ids"] = receipt_channel_ids
evidence["workflow_parity_receipt_release_versions"] = receipt_release_versions
evidence["upstream_receipt_bindings"] = upstream_receipt_bindings
flagship_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs"
dual_head_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "DualHeadAcceptanceTests.cs"
catalog_ruleset_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "CatalogOnlyRulesetShellCatalogResolverTests.cs"
direct_source_bindings: Dict[str, Dict[str, Any]] = {}


def load_direct_source_text(path: Path, label: str, encoding: str = "utf-8") -> str:
    try:
        raw = read_regular_bytes(path, label)
        decoded = raw.decode(encoding)
    except (ValueError, UnicodeError) as exc:
        reasons.append(f"{label} is unsafe or unreadable: {exc}")
        return ""
    direct_source_bindings[str(path.resolve())] = binding_for_bytes(path, raw)
    return decoded


flagship_tests_text = load_direct_source_text(flagship_tests_path, "flagship UI gate test source")
dual_head_tests_text = load_direct_source_text(dual_head_tests_path, "dual-head acceptance test source")
catalog_ruleset_tests_text = load_direct_source_text(
    catalog_ruleset_tests_path, "catalog-only ruleset shell test source"
)
chummer5a_screenshot_review_gate_text = load_direct_source_text(
    chummer5a_screenshot_review_gate_path,
    "Chummer5a screenshot review receipt source",
    "utf-8-sig",
)
evidence["direct_source_bindings"] = direct_source_bindings
screenshot_route_receipts = (
    chummer5a_screenshot_review_gate.get("routeLocalReceipts")
    if isinstance(chummer5a_screenshot_review_gate.get("routeLocalReceipts"), dict)
    else {}
)
dense_initiative_route = (
    screenshot_route_receipts.get("dense_workbench_and_initiative")
    if isinstance(screenshot_route_receipts.get("dense_workbench_and_initiative"), dict)
    else {}
)
required_direct_screenshot_files = [
    "05-dense-section-light.png",
    "07-loaded-runner-tabs-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "14-advancement-dialog-light.png",
]
required_direct_runtime_markers = {
    "dense_builder_career": [
        "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
        "Character_creation_preserves_familiar_dense_builder_rhythm",
        "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
    ],
    "initiative_utility": [
        "menu:dice_roller_or_workflow:initiative_screenshot",
        "\"initiative\": \"11 + 2d6\"",
    ],
    "contacts_lifestyles_notes": [
        "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
        "\"tab-lifestyle.lifestyles\"",
        "\"tab-notes.metadata\"",
    ],
}
direct_workflow_marker_results: Dict[str, Dict[str, Any]] = {}
for marker_group, markers in required_direct_runtime_markers.items():
    missing_markers: List[str] = []
    for marker in markers:
        if marker in flagship_tests_text:
            continue
        if marker_group == "initiative_utility":
            if marker == "menu:dice_roller_or_workflow:initiative_screenshot" and pass_marker(
                dense_initiative_route.get("status")
            ):
                continue
            if marker == "\"initiative\": \"11 + 2d6\"" and "\"initiative\": \"11 + 2d6\"" in chummer5a_screenshot_review_gate_text:
                continue
        if marker_group == "contacts_lifestyles_notes":
            if marker == "\"tab-lifestyle.lifestyles\"" and (
                "\"tab-lifestyle\"" in flagship_tests_text
                or "\"tab-lifestyle.lifestyles\"" in dual_head_tests_text
            ):
                continue
            if marker == "\"tab-notes.metadata\"" and (
                "\"tab-notes\"" in flagship_tests_text
                or "\"tab-notes.metadata\"" in catalog_ruleset_tests_text
            ):
                continue
        missing_markers.append(marker)
    direct_workflow_marker_results[marker_group] = {
        "required": markers,
        "missing": missing_markers,
        "status": "pass" if not missing_markers else "fail",
    }
evidence["direct_workflow_runtime_marker_checks"] = direct_workflow_marker_results

visual_required_screenshots = set(
    str(item).strip()
    for item in (
        ((visual_familiarity_gate.get("evidence") or {}).get("required_screenshots"))
        or ((visual_familiarity_gate.get("evidence") or {}).get("visual_familiarity_required_screenshots"))
        or []
    )
    if str(item).strip()
)
missing_direct_screenshot_files = [
    screenshot for screenshot in required_direct_screenshot_files
    if screenshot not in visual_required_screenshots
]
evidence["direct_workflow_required_screenshot_files"] = required_direct_screenshot_files
evidence["direct_workflow_missing_screenshot_files"] = missing_direct_screenshot_files

evidence["direct_workflow_dense_initiative_route_status"] = str(dense_initiative_route.get("status") or "")
evidence["direct_workflow_dense_initiative_route_ids"] = list(dense_initiative_route.get("routeIds") or [])
evidence["direct_workflow_dense_initiative_screenshots"] = list(dense_initiative_route.get("screenshots") or [])

required_review_jobs = {
    "dense_builder",
    "master_index",
    "roster",
    "settings",
}
available_review_jobs = (
    chummer5a_screenshot_review_gate.get("reviewJobs")
    if isinstance(chummer5a_screenshot_review_gate.get("reviewJobs"), dict)
    else {}
)
missing_required_review_jobs = sorted(
    job_name for job_name in required_review_jobs if job_name not in available_review_jobs
)
failing_required_review_jobs = sorted(
    job_name
    for job_name in required_review_jobs
    if isinstance(available_review_jobs.get(job_name), dict)
    and not status_ok(available_review_jobs[job_name].get("status"))
)
evidence["direct_workflow_missing_review_jobs"] = missing_required_review_jobs
evidence["direct_workflow_failing_review_jobs"] = failing_required_review_jobs

flagship_head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
flagship_primary_desktop_heads = {
    normalize_token(item)
    for item in (
        flagship_gate.get("desktopHeads")
        if isinstance(flagship_gate.get("desktopHeads"), list)
        else []
    )
    + ([flagship_gate.get("desktopHead")] if flagship_gate.get("desktopHead") else [])
    if normalize_token(item)
}
flagship_declared_desktop_fallback_heads = sorted(
    {
        normalize_token(item)
        for item in (
            flagship_gate.get("desktopFallbackHeads")
            if isinstance(flagship_gate.get("desktopFallbackHeads"), list)
            else []
        )
        if normalize_token(item)
    }
)
required_desktop_heads = sorted(
    flagship_primary_desktop_heads.union(release_channel_head_requirements["required_heads"])
)
canonical_required_desktop_heads = ["avalonia"]
missing_canonical_required_desktop_heads = [
    head for head in canonical_required_desktop_heads
    if head not in required_desktop_heads
]
flagship_head_proof_statuses = normalize_head_proof_statuses(
    flagship_head_proofs,
    "flagship_gate.headProofs.status",
    evidence,
    reasons,
)
required_head_contract_markers = {
    "avalonia": [
        "status",
        "visualReview",
        "themeReadabilityContrast",
        "bundledDemoRunner",
        "releaseLifecycle",
        "requiredRuntimeBackedTests",
        "requiredLifecycleTests",
        "sourceTestFile",
        "testSuites",
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
        "releaseLifecycle",
        "requiredShellTests",
        "requiredLifecycleTests",
        "sourceTestFile",
        "testSuites",
    ],
}
required_head_status_markers = {
    "avalonia": [
        "status",
        "visualReview",
        "themeReadabilityContrast",
        "bundledDemoRunner",
        "releaseLifecycle",
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
        "releaseLifecycle",
    ],
}
required_head_list_markers = {
    "avalonia": [
        "requiredRuntimeBackedTests",
        "requiredLifecycleTests",
        "testSuites",
    ],
    "blazor-desktop": [
        "requiredShellTests",
        "requiredLifecycleTests",
        "testSuites",
    ],
}
flagship_head_contract_marker_statuses: Dict[str, Dict[str, str]] = {}
flagship_head_missing_contract_markers: Dict[str, List[str]] = {}
flagship_head_source_test_file_paths: Dict[str, str] = {}
flagship_head_source_test_file_exists: Dict[str, bool] = {}
flagship_head_source_test_file_within_repo_root: Dict[str, bool] = {}
for required_head in required_desktop_heads:
    proof_payload = (
        flagship_head_proofs.get(required_head)
        if isinstance(flagship_head_proofs.get(required_head), dict)
        else {}
    )
    required_markers = required_head_contract_markers.get(
        required_head, ["status", "sourceTestFile", "testSuites"]
    )
    status_markers = set(required_head_status_markers.get(required_head, ["status"]))
    list_markers = set(required_head_list_markers.get(required_head, ["testSuites"]))
    marker_statuses: Dict[str, str] = {}
    missing_markers: List[str] = []
    source_test_file_value = str(proof_payload.get("sourceTestFile") or "").strip()
    source_test_file_path = Path(source_test_file_value) if source_test_file_value else None
    source_test_file_exists = source_test_file_path is not None and source_test_file_path.is_file()
    source_test_file_within_repo_root = (
        path_within_root(source_test_file_path, repo_root)
        if source_test_file_path is not None
        else False
    )
    flagship_head_source_test_file_paths[required_head] = source_test_file_value
    flagship_head_source_test_file_exists[required_head] = source_test_file_exists
    flagship_head_source_test_file_within_repo_root[required_head] = (
        source_test_file_within_repo_root
    )
    for marker in required_markers:
        marker_value = proof_payload.get(marker)
        marker_ok = False
        if marker == "sourceTestFile":
            marker_ok = source_test_file_exists and source_test_file_within_repo_root
        elif marker in list_markers:
            marker_ok = (
                isinstance(marker_value, list)
                and any(str(item).strip() for item in marker_value)
            )
        elif marker in status_markers:
            marker_ok = status_ok(str(marker_value or "").strip().lower())
        else:
            marker_ok = bool(str(marker_value or "").strip())
        marker_statuses[marker] = "pass" if marker_ok else "fail"
        if not marker_ok:
            missing_markers.append(marker)
    flagship_head_contract_marker_statuses[required_head] = marker_statuses
    flagship_head_missing_contract_markers[required_head] = missing_markers
    if missing_markers:
        reasons.append(
            f"Flagship UI release gate head proof for required desktop head '{required_head}' is missing required workflow contract marker(s): "
            + ", ".join(missing_markers)
        )
    if source_test_file_value and source_test_file_exists and not source_test_file_within_repo_root:
        reasons.append(
            f"Flagship UI release gate sourceTestFile for required desktop head '{required_head}' is outside this repo root."
        )
    if source_test_file_value and not source_test_file_exists:
        reasons.append(
            f"Flagship UI release gate sourceTestFile for required desktop head '{required_head}' is missing/unreadable on disk."
        )
evidence["flagship_primary_desktop_heads"] = sorted(flagship_primary_desktop_heads)
evidence["flagship_declared_desktop_fallback_heads"] = (
    flagship_declared_desktop_fallback_heads
)
evidence["flagship_required_desktop_heads"] = required_desktop_heads
evidence["canonical_required_desktop_heads"] = canonical_required_desktop_heads
evidence["flagship_missing_canonical_required_desktop_heads"] = (
    missing_canonical_required_desktop_heads
)
evidence["flagship_head_proof_statuses"] = flagship_head_proof_statuses
evidence["required_head_contract_markers"] = required_head_contract_markers
evidence["flagship_head_contract_marker_statuses"] = (
    flagship_head_contract_marker_statuses
)
evidence["flagship_head_missing_contract_markers"] = (
    flagship_head_missing_contract_markers
)
evidence["flagship_head_source_test_file_paths"] = (
    flagship_head_source_test_file_paths
)
evidence["flagship_head_source_test_file_exists"] = (
    flagship_head_source_test_file_exists
)
evidence["flagship_head_source_test_file_within_repo_root"] = (
    flagship_head_source_test_file_within_repo_root
)
if not required_desktop_heads:
    reasons.append("Flagship UI release gate is missing required desktopHeads inventory for per-head workflow execution proof.")
if missing_canonical_required_desktop_heads:
    reasons.append(
        "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head workflow execution proof: "
        + ", ".join(missing_canonical_required_desktop_heads)
    )
missing_or_not_ready_heads = [
    head
    for head in required_desktop_heads
    if not status_ok(flagship_head_proof_statuses.get(head, ""))
]
evidence["flagship_missing_or_not_ready_desktop_heads"] = missing_or_not_ready_heads
if missing_or_not_ready_heads:
    reasons.append(
        "Flagship UI release gate is missing passing headProofs for required desktop heads: "
        + ", ".join(missing_or_not_ready_heads)
    )

def load_family_contract_input(path: Path, label: str) -> tuple[Dict[str, Any], bytes]:
    try:
        return load_regular_json(path, label)
    except ValueError as exc:
        reasons.append(str(exc))
        return {}, b""


def validate_family_ledger_contract(edition: str, ledger_payload: Dict[str, Any]) -> None:
    if type(ledger_payload.get("version")) is not int or ledger_payload.get("version") != 1:
        reasons.append(f"{edition.upper()} workflow parity ledger version must be integer 1.")
    if ledger_payload.get("scope") != f"{edition}_desktop_head":
        reasons.append(f"{edition.upper()} workflow parity ledger scope must equal {edition}_desktop_head.")
    raw_families = ledger_payload.get("requiredFamilies")
    if not isinstance(raw_families, list) or not all(isinstance(item, dict) for item in raw_families):
        reasons.append(f"{edition.upper()} workflow parity ledger requiredFamilies must be an object array.")
        return
    family_ids = [item.get("id") for item in raw_families]
    if (
        any(not isinstance(value, str) or not value or value != value.strip() for value in family_ids)
        or len(family_ids) != len(set(family_ids))
        or set(family_ids) != REQUIRED_WORKFLOW_FAMILY_IDS
    ):
        reasons.append(f"{edition.upper()} workflow parity ledger canonical family inventory is not exact.")
    for family in raw_families:
        family_id = family.get("id")
        if not isinstance(family_id, str) or not family_id:
            continue
        expected_execution = [
            f".codex-studio/published/workflow-family-parity/executed/{edition}/{{familyId}}.generated.json"
        ]
        expected_verification = [
            f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"
        ]
        expected_parity = [
            f".codex-studio/published/workflow-family-parity/{edition.upper()}_WORKFLOW_FAMILY_{family_id}.generated.json"
        ]
        if family.get("executionReceipts") != expected_execution:
            reasons.append(f"{edition.upper()} family {family_id} executionReceipts is not the exact canonical target.")
        if family.get("verificationReceipts") != expected_verification:
            reasons.append(f"{edition.upper()} family {family_id} verificationReceipts is not the exact canonical target.")
        if family.get("parityReceipts") != expected_parity:
            reasons.append(f"{edition.upper()} family {family_id} parityReceipts is not the exact canonical target.")
        audit_tests = family.get("auditTests")
        if (
            not isinstance(audit_tests, list)
            or not audit_tests
            or any(not isinstance(value, str) or not value or value != value.strip() for value in audit_tests)
            or len(audit_tests) != len(set(audit_tests))
        ):
            reasons.append(f"{edition.upper()} family {family_id} auditTests is not a unique canonical test list.")


def validate_family_oracle_contract(edition: str, oracle_payload: Dict[str, Any]) -> None:
    if type(oracle_payload.get("version")) is not int or oracle_payload.get("version") != 1:
        reasons.append(f"{edition.upper()} workflow parity oracle version must be integer 1.")
    if oracle_payload.get("scope") != f"{edition}_desktop_head":
        reasons.append(f"{edition.upper()} workflow parity oracle scope must equal {edition}_desktop_head.")
    if edition == "sr4":
        raw_families = oracle_payload.get("workflowFamilies")
        source_repo = oracle_payload.get("sourceRepo")
        if not isinstance(source_repo, dict):
            reasons.append("SR4 workflow parity oracle sourceRepo must be an object.")
        else:
            source_path = str(source_repo.get("path") or "").strip()
            source_head = str(source_repo.get("head") or "").strip()
            if (
                not source_path
                or len(source_head) != 40
                or any(character not in "0123456789abcdef" for character in source_head)
            ):
                reasons.append("SR4 workflow parity oracle sourceRepo binding is invalid.")
    else:
        raw_items = oracle_payload.get("requiredFamilies")
        raw_families = (
            [item.get("id") for item in raw_items]
            if isinstance(raw_items, list) and all(isinstance(item, dict) for item in raw_items)
            else []
        )
        if isinstance(raw_items, list):
            for item in raw_items:
                if not isinstance(item, dict):
                    continue
                release_tests = item.get("releaseGateTests")
                if (
                    not str(item.get("classification") or "").strip()
                    or not str(item.get("rationale") or "").strip()
                    or not isinstance(release_tests, list)
                    or not release_tests
                    or any(
                        not isinstance(value, str) or not value or value != value.strip()
                        for value in release_tests
                    )
                    or len(release_tests) != len(set(release_tests))
                ):
                    reasons.append(
                        f"SR6 workflow parity oracle family {item.get('id') or '<missing>'} has an invalid release contract."
                    )
    if (
        not isinstance(raw_families, list)
        or any(not isinstance(value, str) or not value or value != value.strip() for value in raw_families)
        or len(raw_families) != len(set(raw_families))
        or set(raw_families) != REQUIRED_WORKFLOW_FAMILY_IDS
    ):
        reasons.append(f"{edition.upper()} workflow parity oracle canonical family inventory is not exact.")


sr4_ledger, sr4_ledger_bytes = load_family_contract_input(sr4_ledger_path, "SR4 workflow parity ledger")
sr6_ledger, sr6_ledger_bytes = load_family_contract_input(sr6_ledger_path, "SR6 workflow parity ledger")
for edition, ledger_bytes in (("sr4", sr4_ledger_bytes), ("sr6", sr6_ledger_bytes)):
    if not ledger_bytes or hashlib.sha256(ledger_bytes).hexdigest() != CANONICAL_LEDGER_SHA256[edition]:
        reasons.append(
            f"{edition.upper()} workflow parity ledger bytes are not the reviewed canonical contract."
        )
validate_family_ledger_contract("sr4", sr4_ledger)
validate_family_ledger_contract("sr6", sr6_ledger)
sr4_family_state = collect_family_state(sr4_ledger)
sr6_family_state = collect_family_state(sr6_ledger)

oracle_paths = {
    "sr4": repo_root / "docs/CHUMMER4_SR4_PARITY_ORACLE.json",
    "sr6": repo_root / "docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json",
}
oracle_payloads: Dict[str, Dict[str, Any]] = {}
oracle_bytes_by_edition: Dict[str, bytes] = {}
for edition, oracle_path in oracle_paths.items():
    oracle_payloads[edition], oracle_bytes_by_edition[edition] = load_family_contract_input(
        oracle_path, f"{edition.upper()} workflow parity oracle"
    )
    if (
        not oracle_bytes_by_edition[edition]
        or hashlib.sha256(oracle_bytes_by_edition[edition]).hexdigest()
        != CANONICAL_ORACLE_SHA256[edition]
    ):
        reasons.append(
            f"{edition.upper()} workflow parity oracle bytes are not the reviewed canonical contract."
        )
    validate_family_oracle_contract(edition, oracle_payloads[edition])

family_test_source_paths = [
    repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    repo_root / "Chummer.Tests/Compliance/MigrationComplianceTests.cs",
    repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    repo_root / "Chummer.Tests/Presentation/WorkflowParityGateTests.cs",
]
family_test_assembly_path = repo_root / "Chummer.Tests/bin/Release/net10.0/Chummer.Tests.dll"
family_candidate_input_error = ""
family_test_source_bindings: List[Dict[str, Any]] = []
family_test_assembly_binding: Dict[str, Any] = {}
family_trx_contract_source_binding: Dict[str, Any] = {}
family_build_project_bindings: List[Dict[str, Any]] = []
family_api_project_binding: Dict[str, Any] = {}
family_dotnet_host_binding: Dict[str, Any] = {}
family_build_output_bindings: Dict[str, List[Dict[str, Any]]] = {}
family_test_build_projects = [
    ("Chummer.Avalonia", repo_root / "Chummer.Avalonia/Chummer.Avalonia.csproj"),
    ("Chummer.Portal", repo_root / "Chummer.Portal/Chummer.Portal.csproj"),
    ("Chummer.Tests", repo_root / "Chummer.Tests/Chummer.Tests.csproj"),
]
family_api_project_path = repo_root / "Chummer.Api/Chummer.Api.csproj"
family_dotnet_host_path = Path("/usr/bin/dotnet").resolve(strict=True)
if not str(family_dotnet_host_path).startswith("/usr/"):
    reasons.append("Canonical dotnet host must resolve under /usr.")
family_build_output_roots = {
    "Chummer.Api": repo_root / "Chummer.Api/bin/Release/net10.0",
    "Chummer.Avalonia": repo_root / "Chummer.Avalonia/bin/Release/net10.0",
    "Chummer.Portal": repo_root / "Chummer.Portal/bin/Release/net10.0",
    "Chummer.Tests": repo_root / "Chummer.Tests/bin/Release/net10.0",
}
try:
    family_test_source_bindings = [
        file_binding(path, "workflow parity test source") for path in family_test_source_paths
    ]
    family_test_assembly_binding = file_binding(
        family_test_assembly_path, "workflow parity test assembly"
    )
    family_trx_contract_source_binding = file_binding(
        trx_contract_source_path, "workflow-family TRX validator source"
    )
    family_build_project_bindings = [
        file_binding(project_path, f"{project_label} project contract")
        for project_label, project_path in family_test_build_projects
    ]
    family_api_project_binding = file_binding(
        family_api_project_path, "canonical API autostart project"
    )
    family_dotnet_host_binding = file_binding(
        family_dotnet_host_path, "canonical dotnet host"
    )
    family_build_output_bindings = {
        label: snapshot_output_tree(root, f"{label} release build output")
        for label, root in family_build_output_roots.items()
    }
except ValueError as exc:
    family_candidate_input_error = str(exc)
    reasons.append(family_candidate_input_error)

ledger_bindings = {
    "sr4": binding_for_bytes(sr4_ledger_path, sr4_ledger_bytes) if sr4_ledger_bytes else {},
    "sr6": binding_for_bytes(sr6_ledger_path, sr6_ledger_bytes) if sr6_ledger_bytes else {},
}
oracle_bindings = {
    edition: binding_for_bytes(oracle_paths[edition], oracle_bytes_by_edition[edition])
    if oracle_bytes_by_edition[edition]
    else {}
    for edition in ("sr4", "sr6")
}
candidate_identities: Dict[str, Dict[str, Any]] = {}
candidate_digests: Dict[str, str] = {}
candidate_snapshot_ids: Dict[str, str] = {}
if release_identity and not family_candidate_input_error:
    for edition in ("sr4", "sr6"):
        candidate_identity = {
            "edition": edition,
            "ledger": ledger_bindings[edition],
            "oracle": oracle_bindings[edition],
            "testSources": family_test_source_bindings,
            "trxContractSource": family_trx_contract_source_binding,
            "buildProjects": family_build_project_bindings,
            "apiProject": family_api_project_binding,
            "toolchain": family_dotnet_host_binding,
            "buildOutputs": family_build_output_bindings,
            "testAssembly": family_test_assembly_binding,
        }
        candidate_identities[edition] = candidate_identity
        candidate_digests[edition] = hashlib.sha256(
            json.dumps(
                {"releaseIdentity": release_identity, "candidateIdentity": candidate_identity},
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        candidate_snapshot_ids[edition] = hashlib.sha256(
            json.dumps(
                {
                    "releaseIdentity": release_identity,
                    "testSources": family_test_source_bindings,
                    "trxContractSource": family_trx_contract_source_binding,
                    "buildProjects": family_build_project_bindings,
                    "apiProject": family_api_project_binding,
                    "toolchain": family_dotnet_host_binding,
                    "buildOutputs": family_build_output_bindings,
                    "testAssembly": family_test_assembly_binding,
                },
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()

missing_family_receipts: List[str] = []
failing_family_receipts: List[str] = []
failing_family_receipts_external: List[str] = []
weak_family_receipts: List[str] = []
checked_family_receipts = 0
workflow_family_receipts_outside_repo_root: List[str] = []
missing_execution_receipts: List[str] = []
failing_execution_receipts: List[str] = []
failing_execution_receipts_external: List[str] = []
weak_execution_receipts: List[str] = []
checked_execution_receipts = 0
workflow_execution_receipts_outside_repo_root: List[str] = []
missing_required_family_ids: Dict[str, List[str]] = {}
not_ready_required_family_ids: Dict[str, List[str]] = {}
missing_required_family_audit_tests: Dict[str, List[str]] = {}
family_receipt_run_ids: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
family_receipt_candidate_digests: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
family_receipt_candidate_snapshot_ids: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
family_receipt_execution_run_digests: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
family_receipt_execution_started_at: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
family_receipt_execution_completed_at: Dict[str, set[str]] = {"sr4": set(), "sr6": set()}
validated_family_receipt_bindings: Dict[str, Dict[str, Any]] = {}
validated_trx_test_by_path: Dict[Tuple[str, str], str] = {}
workflow_stage_manifest_payloads: Dict[str, Dict[str, Dict[str, Any]]] = {
    "sr4": {},
    "sr6": {},
}
workflow_stage_manifest_bindings: Dict[str, Dict[str, Dict[str, Any]]] = {
    "sr4": {},
    "sr6": {},
}
workflow_stage_expected_receipts: Dict[str, Dict[str, Dict[str, Path]]] = {
    "sr4": {},
    "sr6": {},
}
workflow_stage_manifest_errors: List[str] = []


def expected_stage_receipts(
    ledger_payload: Dict[str, Any],
    edition: str,
    stage: str,
) -> Dict[str, Path]:
    receipt_key_by_stage = {
        "execution": "executionReceipts",
        "verification": "verificationReceipts",
        "parity": "parityReceipts",
    }
    receipt_key = receipt_key_by_stage[stage]
    expected: Dict[str, Path] = {}
    for family_id, observed_key, rel_path in iter_ledger_receipts(ledger_payload):
        if observed_key != receipt_key:
            continue
        if family_id in expected:
            raise ValueError(
                f"{edition}:{stage} ledger contains multiple receipt targets for {family_id}"
            )
        candidate = repo_root / rel_path
        if not path_within_root(candidate, repo_root):
            raise ValueError(
                f"{edition}:{stage} ledger receipt target escapes the repository: {rel_path}"
            )
        expected[family_id] = candidate
    if set(expected) != REQUIRED_WORKFLOW_FAMILY_IDS:
        missing = sorted(REQUIRED_WORKFLOW_FAMILY_IDS.difference(expected))
        unexpected = sorted(set(expected).difference(REQUIRED_WORKFLOW_FAMILY_IDS))
        raise ValueError(
            f"{edition}:{stage} ledger family inventory is not exact "
            f"(missing={missing}, unexpected={unexpected})"
        )
    return expected


for stage_edition, stage_ledger in (("sr4", sr4_ledger), ("sr6", sr6_ledger)):
    upstream_stage_bindings: List[Dict[str, Any]] = []
    for stage_name in ("execution", "verification", "parity"):
        try:
            stage_expected = expected_stage_receipts(
                stage_ledger, stage_edition, stage_name
            )
            workflow_stage_expected_receipts[stage_edition][stage_name] = stage_expected
            stage_validation = validate_workflow_stage_manifest(
                manifest_path=workflow_stage_manifest_path(
                    repo_root, stage_edition, stage_name
                ),
                repo_root=repo_root,
                edition=stage_edition,
                stage=stage_name,
                expected_receipts=stage_expected,
                expected_release_identity=release_identity,
                expected_upstream_stage_manifests=list(upstream_stage_bindings),
                require_pass=True,
            )
            stage_payload = stage_validation["manifest"]
            stage_binding = stage_validation["binding"]
            workflow_stage_manifest_payloads[stage_edition][stage_name] = stage_payload
            workflow_stage_manifest_bindings[stage_edition][stage_name] = stage_binding
            upstream_stage_bindings.append(stage_binding)
        except (ValueError, OSError) as exc:
            workflow_stage_manifest_errors.append(
                f"{stage_edition}:{stage_name}={exc}"
            )
            break

for desktop_edition, desktop_receipt in (
    ("sr4", sr4_workflow_parity),
    ("sr6", sr6_workflow_parity),
):
    try:
        parity_manifest = workflow_stage_manifest_payloads.get(
            desktop_edition, {}
        ).get("parity")
        parity_binding = workflow_stage_manifest_bindings.get(
            desktop_edition, {}
        ).get("parity")
        if not isinstance(parity_manifest, dict) or not isinstance(parity_binding, dict):
            raise ValueError("committed parity manifest is unavailable")
        desktop_evidence = desktop_receipt.get("evidence")
        if not isinstance(desktop_evidence, dict):
            raise ValueError("desktop parity receipt evidence is invalid")
        expected_desktop_identity = {
            "producerRunId": parity_manifest.get("producerRunId"),
            "candidateSnapshotId": parity_manifest.get("candidateSnapshotId"),
            "workflowEpochId": parity_manifest.get("candidateSnapshotId"),
            "executionRunDigest": parity_manifest.get("executionRunDigest"),
            "workflowFamilyParityEpochCommitId": parity_manifest.get(
                "epochCommitId"
            ),
        }
        if any(
            desktop_receipt.get(key) != expected_value
            for key, expected_value in expected_desktop_identity.items()
        ):
            raise ValueError("desktop parity receipt identity does not bind its parity manifest")
        if (
            desktop_evidence.get("producerRunId")
            != parity_manifest.get("producerRunId")
            or desktop_evidence.get("candidateSnapshotId")
            != parity_manifest.get("candidateSnapshotId")
            or desktop_evidence.get("workflowEpochId")
            != parity_manifest.get("candidateSnapshotId")
            or desktop_evidence.get("executionRunDigest")
            != parity_manifest.get("executionRunDigest")
            or desktop_evidence.get("workflowFamilyParityEpochManifest")
            != parity_binding
            or desktop_evidence.get("workflowFamilyParityEpochCommitId")
            != parity_manifest.get("epochCommitId")
            or desktop_evidence.get("workflowFamilyEpochCommitted") is not True
        ):
            raise ValueError("desktop parity receipt evidence does not bind its parity manifest")
    except (ValueError, OSError) as exc:
        workflow_stage_manifest_errors.append(
            f"{desktop_edition}:desktopAnchor={exc}"
        )

for stage_error in workflow_stage_manifest_errors:
    weak_family_receipts.append("stageManifest:" + stage_error)


def validate_family_timestamp(payload: Dict[str, Any], label: str) -> None:
    generated_at = payload.get("generatedAt")
    generated_at_alias = payload.get("generated_at")
    if generated_at is not None and generated_at_alias is not None and generated_at != generated_at_alias:
        raise ValueError(f"{label} carries conflicting generatedAt/generated_at aliases")
    raw = generated_at if generated_at is not None else generated_at_alias
    if not isinstance(raw, str) or not raw or raw != raw.strip():
        raise ValueError(f"{label} generatedAt must be a canonical nonblank offset timestamp")
    parsed = parse_iso(raw)
    if parsed is None:
        raise ValueError(f"{label} generatedAt must include a valid UTC offset")
    delta_seconds = (datetime.now(timezone.utc) - parsed).total_seconds()
    if delta_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        raise ValueError(f"{label} is stale ({int(delta_seconds)}s old)")
    if delta_seconds < -DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
        raise ValueError(f"{label} is too far in the future ({int(-delta_seconds)}s ahead)")


def validate_current_binding(binding: Any, expected_path: Path, label: str) -> None:
    if not isinstance(binding, dict):
        raise ValueError(f"{label} binding must be an object")
    if binding.get("path") != str(expected_path.resolve()):
        raise ValueError(f"{label} binding path is missing or misplaced")
    if binding != file_binding(expected_path, label):
        raise ValueError(f"{label} binding does not match current bytes")


def validate_upstream_family_receipt(
    upstream_path: Path,
    upstream_binding: Any,
    edition: str,
    family_id: str,
    receipt_kind: str,
    producer_run_id: str,
) -> Dict[str, Any]:
    stage_manifest = workflow_stage_manifest_payloads.get(edition, {}).get(receipt_kind)
    if not isinstance(stage_manifest, dict):
        raise ValueError(f"{receipt_kind} stage manifest is unavailable or invalid")
    validate_current_binding(upstream_binding, upstream_path, f"{receipt_kind} receipt")
    upstream_payload, _ = load_regular_json(upstream_path, f"{receipt_kind} receipt")
    expected_contract = f"chummer6-ui.{edition}_workflow_family_{receipt_kind}_receipt"
    if type(upstream_payload.get("schemaVersion")) is not int or upstream_payload.get("schemaVersion") != 1:
        raise ValueError(f"{receipt_kind} schemaVersion must equal integer 1")
    if upstream_payload.get("contract_name") != expected_contract or upstream_payload.get("status") != "pass":
        raise ValueError(f"{receipt_kind} contract/status is invalid")
    validate_family_timestamp(upstream_payload, f"{receipt_kind} receipt")
    if upstream_payload.get("producerRunId") != producer_run_id:
        raise ValueError(f"{receipt_kind} producerRunId does not match the family chain")
    candidate_snapshot_id = upstream_payload.get("candidateSnapshotId")
    if (
        candidate_snapshot_id != candidate_snapshot_ids.get(edition)
        or upstream_payload.get("workflowEpochId") != candidate_snapshot_id
        or stage_manifest.get("candidateSnapshotId") != candidate_snapshot_id
    ):
        raise ValueError(
            f"{receipt_kind} candidateSnapshotId does not match the committed candidate"
        )
    execution_run_digest = upstream_payload.get("executionRunDigest")
    if execution_run_digest != stage_manifest.get("executionRunDigest"):
        raise ValueError(
            f"{receipt_kind} executionRunDigest does not match its committed stage manifest"
        )
    upstream_evidence = upstream_payload.get("evidence")
    if not isinstance(upstream_evidence, dict):
        raise ValueError(f"{receipt_kind} evidence must be an object")
    expected_proof_kind = (
        "sr4_family_oracle"
        if edition == "sr4"
        else (
            "sr6_family_release_gated_execution"
            if receipt_kind == "execution"
            else "sr6_family_carry_forward"
        )
    )
    if (
        upstream_evidence.get("edition") != edition
        or upstream_evidence.get("familyId") != family_id
        or upstream_evidence.get("proofKind") != expected_proof_kind
        or upstream_evidence.get("producerRunId") != producer_run_id
        or upstream_evidence.get("candidateSnapshotId") != candidate_snapshot_id
        or upstream_evidence.get("workflowEpochId") != candidate_snapshot_id
        or upstream_evidence.get("executionRunDigest") != execution_run_digest
    ):
        raise ValueError(f"{receipt_kind} edition/family/proof/run identity is invalid")
    if upstream_evidence.get("releaseIdentity") != release_identity:
        raise ValueError(f"{receipt_kind} releaseIdentity does not match the selected release")
    if upstream_evidence.get("candidateIdentity") != candidate_identities.get(edition):
        raise ValueError(f"{receipt_kind} candidateIdentity does not match current bytes")
    if upstream_evidence.get("candidateDigest") != candidate_digests.get(edition):
        raise ValueError(f"{receipt_kind} candidateDigest is invalid")
    expected_started_at = stage_manifest.get("executionStartedAt")
    expected_completed_at = stage_manifest.get("executionCompletedAt")
    observed_started_at = upstream_evidence.get(
        "runStartedAt" if receipt_kind == "execution" else "executionStartedAt"
    )
    observed_completed_at = upstream_evidence.get(
        "runCompletedAt" if receipt_kind == "execution" else "executionCompletedAt"
    )
    if (
        observed_started_at != expected_started_at
        or observed_completed_at != expected_completed_at
    ):
        raise ValueError(
            f"{receipt_kind} execution bounds do not match its committed stage manifest"
        )
    return upstream_evidence


def validate_family_receipt_contract(
    payload: Dict[str, Any],
    edition: str,
    family_id: str,
    receipt_key: str,
    audit_tests: List[str],
) -> tuple[str, str, str, str, str, str]:
    receipt_kind_by_key = {
        "executionReceipts": "execution",
        "verificationReceipts": "verification",
        "parityReceipts": "parity",
    }
    receipt_kind = receipt_kind_by_key[receipt_key]
    stage_manifest = workflow_stage_manifest_payloads.get(edition, {}).get(receipt_kind)
    if not isinstance(stage_manifest, dict):
        raise ValueError(f"{receipt_kind} stage manifest is unavailable or invalid")
    if type(payload.get("schemaVersion")) is not int or payload.get("schemaVersion") != 1:
        raise ValueError("schemaVersion must equal integer 1")
    expected_contract = f"chummer6-ui.{edition}_workflow_family_{receipt_kind}_receipt"
    if payload.get("contract_name") != expected_contract:
        raise ValueError(f"contract_name must equal {expected_contract}")
    if payload.get("status") != "pass":
        raise ValueError("status must equal pass")
    validate_family_timestamp(payload, f"{edition}:{family_id}:{receipt_kind}")
    producer_run_id = payload.get("producerRunId")
    if not isinstance(producer_run_id, str) or str(uuid.UUID(producer_run_id)) != producer_run_id:
        raise ValueError("producerRunId must be a canonical UUID")
    if producer_run_id != stage_manifest.get("producerRunId"):
        raise ValueError("producerRunId does not match the committed stage manifest")
    candidate_snapshot_id = payload.get("candidateSnapshotId")
    if (
        candidate_snapshot_id != candidate_snapshot_ids.get(edition)
        or payload.get("workflowEpochId") != candidate_snapshot_id
        or stage_manifest.get("candidateSnapshotId") != candidate_snapshot_id
    ):
        raise ValueError("candidateSnapshotId does not match the current candidate")
    execution_run_digest = payload.get("executionRunDigest")
    if execution_run_digest != stage_manifest.get("executionRunDigest"):
        raise ValueError("executionRunDigest does not match the committed stage manifest")
    execution_started_at = stage_manifest.get("executionStartedAt")
    execution_completed_at = stage_manifest.get("executionCompletedAt")
    payload_evidence = payload.get("evidence")
    if not isinstance(payload_evidence, dict):
        raise ValueError("evidence must be an object")
    expected_proof_kind = (
        "sr4_family_oracle"
        if edition == "sr4"
        else (
            "sr6_family_release_gated_execution"
            if receipt_kind == "execution"
            else "sr6_family_carry_forward"
        )
    )
    if (
        payload_evidence.get("edition") != edition
        or payload_evidence.get("familyId") != family_id
        or payload_evidence.get("proofKind") != expected_proof_kind
        or payload_evidence.get("producerRunId") != producer_run_id
        or payload_evidence.get("candidateSnapshotId") != candidate_snapshot_id
        or payload_evidence.get("workflowEpochId") != candidate_snapshot_id
        or payload_evidence.get("executionRunDigest") != execution_run_digest
    ):
        raise ValueError("edition/family/proof/run identity is invalid")
    if payload_evidence.get("releaseIdentity") != release_identity:
        raise ValueError("releaseIdentity does not match the selected release bytes")
    if payload_evidence.get("candidateIdentity") != candidate_identities.get(edition):
        raise ValueError("candidateIdentity does not match current source/assembly/ledger/oracle bytes")
    candidate_digest = payload_evidence.get("candidateDigest")
    if (
        candidate_digest != candidate_digests.get(edition)
        or stage_manifest.get("candidateDigest") != candidate_digest
    ):
        raise ValueError("candidateDigest is invalid")
    if payload_evidence.get("auditTests") != audit_tests:
        raise ValueError("auditTests do not exactly match the ledger family")

    execution_path = (
        repo_root
        / ".codex-studio/published/workflow-family-parity/executed"
        / edition
        / f"{family_id}.generated.json"
    )
    verification_path = (
        repo_root
        / ".codex-studio/published/workflow-family-parity"
        / edition
        / f"{family_id}.generated.json"
    )
    if receipt_kind == "execution":
        if (
            payload_evidence.get("runStartedAt") != execution_started_at
            or payload_evidence.get("runCompletedAt") != execution_completed_at
        ):
            raise ValueError("execution run bounds do not match the stage manifest")
        if payload_evidence.get("sourceBindings") != family_test_source_bindings:
            raise ValueError("execution sourceBindings do not match current source bytes")
        if payload_evidence.get("testAssembly") != family_test_assembly_binding:
            raise ValueError("execution testAssembly does not match current assembly bytes")
        validate_api_probe_contract(
            payload_evidence.get("apiProbe"),
            family_dotnet_host_path,
            family_api_project_path,
        )
        test_executions = payload_evidence.get("testExecutions")
        if not isinstance(test_executions, dict) or set(test_executions) != set(audit_tests):
            raise ValueError("execution testExecutions must exactly cover auditTests")
        run_root = (
            repo_root
            / ".codex-studio/out/workflow-family-parity/executed"
            / edition
            / producer_run_id
        ).resolve()
        dotnet_test = payload_evidence.get("dotnetTest")
        if not isinstance(dotnet_test, dict):
            raise ValueError("execution dotnetTest must be an object")
        per_test_trx_paths = dotnet_test.get("perTestTrxPaths")
        if not isinstance(per_test_trx_paths, dict) or set(per_test_trx_paths) != set(audit_tests):
            raise ValueError("execution perTestTrxPaths must exactly cover auditTests")
        observed_family_trx_paths: set[str] = set()
        for test_name in audit_tests:
            record = test_executions.get(test_name)
            if not isinstance(record, dict) or record.get("testName") != test_name:
                raise ValueError(f"execution test identity is invalid for {test_name}")
            if type(record.get("exitCode")) is not int or record.get("exitCode") != 0:
                raise ValueError(f"execution test exit is not zero for {test_name}")
            if record.get("attemptCount") != 1:
                raise ValueError(f"execution test was not a first-attempt pass for {test_name}")
            if record.get("outcomes") != ["Passed"] or record.get("resultCount") != 1:
                raise ValueError(f"execution outcome is not exactly Passed for {test_name}")
            if record.get("unexpectedTestNames") != []:
                raise ValueError(f"execution TRX contains unrelated or substring-only tests for {test_name}")
            if (
                record.get("summaryValid") is not True
                or record.get("summaryOutcome") != "Completed"
                or not isinstance(record.get("counters"), dict)
            ):
                raise ValueError(f"execution TRX completed-run summary is invalid for {test_name}")
            trx_binding = record.get("trx")
            if not isinstance(trx_binding, dict) or not isinstance(trx_binding.get("path"), str):
                raise ValueError(f"execution TRX binding is missing for {test_name}")
            trx_path = Path(trx_binding["path"])
            if per_test_trx_paths.get(test_name) != str(trx_path):
                raise ValueError(f"execution perTestTrxPaths disagrees for {test_name}")
            if str(trx_path) in observed_family_trx_paths:
                raise ValueError("distinct audit tests must not share one TRX path")
            observed_family_trx_paths.add(str(trx_path))
            trx_key = (edition, str(trx_path.resolve()))
            previous_test_name = validated_trx_test_by_path.get(trx_key)
            if previous_test_name is not None and previous_test_name != test_name:
                raise ValueError(
                    f"TRX path is reused for different tests: {previous_test_name}, {test_name}"
                )
            validated_trx_test_by_path[trx_key] = test_name
            validated_trx = validate_trx_contract(
                trx_path,
                test_name,
                trx_binding,
                run_root,
                execution_started_at,
                execution_completed_at,
            )
            if (
                record.get("testMethodClassName") != validated_trx["className"]
                or record.get("testId") != validated_trx["testId"]
                or record.get("outcomes") != [validated_trx["outcome"]]
                or record.get("summaryOutcome") != validated_trx["summaryOutcome"]
                or record.get("counters") != validated_trx["counters"]
            ):
                raise ValueError(
                    f"execution JSON claims do not match bound TRX bytes for {test_name}"
                )
        if not isinstance(dotnet_test, dict) or type(dotnet_test.get("exitCode")) is not int or dotnet_test.get("exitCode") != 0:
            raise ValueError("execution dotnetTest exitCode must be integer zero")
        if dotnet_test.get("runnerCommand") != [
            str(family_dotnet_host_path),
            str(family_test_assembly_path),
        ]:
            raise ValueError("execution runnerCommand must use the bound test assembly")
        if payload_evidence.get("matchedPassedTests") != audit_tests:
            raise ValueError("execution matchedPassedTests must exactly equal auditTests")
        if payload_evidence.get("missingAuditTests") != [] or payload_evidence.get("failedAuditTests") != {}:
            raise ValueError("execution receipt still reports missing or failed audit tests")
    elif receipt_kind == "verification":
        if (
            payload_evidence.get("executionStartedAt") != execution_started_at
            or payload_evidence.get("executionCompletedAt") != execution_completed_at
            or payload_evidence.get("upstreamExecutionEpochManifest")
            != workflow_stage_manifest_bindings[edition].get("execution")
        ):
            raise ValueError("verification execution epoch chain is invalid")
        if payload_evidence.get("executionReceipts") != [str(execution_path.resolve())]:
            raise ValueError("verification executionReceipts path is missing or misplaced")
        execution_bindings = payload_evidence.get("upstreamExecutionBindings")
        if not isinstance(execution_bindings, list) or len(execution_bindings) != 1:
            raise ValueError("verification must carry exactly one upstream execution binding")
        validate_upstream_family_receipt(
            execution_path, execution_bindings[0], edition, family_id, "execution", producer_run_id
        )
    elif receipt_kind == "parity":
        if (
            payload_evidence.get("executionStartedAt") != execution_started_at
            or payload_evidence.get("executionCompletedAt") != execution_completed_at
            or payload_evidence.get("upstreamExecutionEpochManifest")
            != workflow_stage_manifest_bindings[edition].get("execution")
            or payload_evidence.get("upstreamVerificationEpochManifest")
            != workflow_stage_manifest_bindings[edition].get("verification")
        ):
            raise ValueError("parity execution epoch chain is invalid")
        if payload_evidence.get("verificationReceipts") != [str(verification_path.resolve())]:
            raise ValueError("parity verificationReceipts path is missing or misplaced")
        verification_bindings = payload_evidence.get("upstreamVerificationBindings")
        execution_bindings = payload_evidence.get("upstreamExecutionBindings")
        if not isinstance(verification_bindings, list) or len(verification_bindings) != 1:
            raise ValueError("parity must carry exactly one upstream verification binding")
        if not isinstance(execution_bindings, list) or len(execution_bindings) != 1:
            raise ValueError("parity must carry exactly one upstream execution binding")
        verification_evidence = validate_upstream_family_receipt(
            verification_path,
            verification_bindings[0],
            edition,
            family_id,
            "verification",
            producer_run_id,
        )
        if verification_evidence.get("upstreamExecutionBindings") != execution_bindings:
            raise ValueError("parity execution binding does not match the verification chain")
        validate_upstream_family_receipt(
            execution_path, execution_bindings[0], edition, family_id, "execution", producer_run_id
        )
    return (
        producer_run_id,
        str(candidate_digest),
        str(candidate_snapshot_id),
        str(execution_run_digest),
        str(execution_started_at),
        str(execution_completed_at),
    )

for edition, family_state in (("sr4", sr4_family_state), ("sr6", sr6_family_state)):
    available_family_ids = set(family_state.keys())
    missing_ids = sorted(REQUIRED_WORKFLOW_FAMILY_IDS.difference(available_family_ids))
    if missing_ids:
        missing_required_family_ids[edition] = missing_ids
    non_ready = sorted(
        family_id
        for family_id in REQUIRED_WORKFLOW_FAMILY_IDS.intersection(available_family_ids)
        if str((family_state.get(family_id) or {}).get("status") or "").strip().lower()
        not in {"ready", "pass", "passed"}
    )
    if non_ready:
        not_ready_required_family_ids[edition] = non_ready
    missing_audit_tests = sorted(
        family_id
        for family_id in REQUIRED_WORKFLOW_FAMILY_IDS.intersection(available_family_ids)
        if not any(str(value).strip() for value in ((family_state.get(family_id) or {}).get("auditTests") or []))
    )
    if missing_audit_tests:
        missing_required_family_audit_tests[edition] = missing_audit_tests

for edition, ledger_payload in (("sr4", sr4_ledger), ("sr6", sr6_ledger)):
    seen: set[str] = set()
    family_state = collect_family_state(ledger_payload)
    for family_id, receipt_key, rel_path in iter_ledger_receipts(ledger_payload):
        key = f"{edition}:{family_id}:{receipt_key}:{rel_path}"
        if key in seen:
            continue
        seen.add(key)
        candidate = repo_root / rel_path
        checked_family_receipts += 1
        if not path_within_root(candidate, repo_root):
            workflow_family_receipts_outside_repo_root.append(
                f"{edition}:{family_id}:{rel_path}->{candidate.resolve()}"
            )
            continue
        try:
            payload, receipt_bytes = load_regular_json(candidate, "workflow family receipt")
        except ValueError as exc:
            if not candidate.exists() and not candidate.is_symlink():
                missing_family_receipts.append(f"{edition}:{family_id}:{rel_path}")
            else:
                weak_family_receipts.append(f"{edition}:{family_id}:{receipt_key}:{rel_path}={exc}")
            continue
        status = payload.get("status")
        if status != "pass":
            failing_family_receipts.append(f"{edition}:{family_id}:{rel_path}={status or 'missing'}")
            payload_evidence = (
                payload.get("evidence")
                if isinstance(payload.get("evidence"), dict)
                else {}
            )
            external_blockers = collect_external_blockers(payload_evidence)
            if external_blockers_are_only_missing_api_surface_contract(external_blockers):
                failing_family_receipts_external.append(
                    f"{edition}:{family_id}:{rel_path}=external_blocker:missing_api_surface_contract"
                )
            continue
        audit_tests = [
            str(value).strip()
            for value in ((family_state.get(family_id) or {}).get("auditTests") or [])
            if str(value).strip()
        ]
        try:
            (
                producer_run_id,
                candidate_digest,
                candidate_snapshot_id,
                execution_run_digest,
                execution_started_at,
                execution_completed_at,
            ) = validate_family_receipt_contract(
                payload, edition, family_id, receipt_key, audit_tests
            )
        except (ValueError, OSError) as exc:
            weak_family_receipts.append(
                f"{edition}:{family_id}:{receipt_key}:{rel_path}={exc}"
            )
            continue
        family_receipt_run_ids[edition].add(producer_run_id)
        family_receipt_candidate_digests[edition].add(candidate_digest)
        family_receipt_candidate_snapshot_ids[edition].add(candidate_snapshot_id)
        family_receipt_execution_run_digests[edition].add(execution_run_digest)
        family_receipt_execution_started_at[edition].add(execution_started_at)
        family_receipt_execution_completed_at[edition].add(execution_completed_at)
        validated_family_receipt_bindings[str(candidate)] = binding_for_bytes(
            candidate, receipt_bytes
        )

for edition in ("sr4", "sr6"):
    if len(family_receipt_run_ids[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=producerRunIdCount:{len(family_receipt_run_ids[edition])}"
        )
    if len(family_receipt_candidate_digests[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=candidateDigestCount:{len(family_receipt_candidate_digests[edition])}"
        )
    if len(family_receipt_candidate_snapshot_ids[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=candidateSnapshotIdCount:{len(family_receipt_candidate_snapshot_ids[edition])}"
        )
    if len(family_receipt_execution_run_digests[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=executionRunDigestCount:{len(family_receipt_execution_run_digests[edition])}"
        )
    if len(family_receipt_execution_started_at[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=executionStartedAtCount:{len(family_receipt_execution_started_at[edition])}"
        )
    if len(family_receipt_execution_completed_at[edition]) != 1:
        weak_family_receipts.append(
            f"{edition}:familyChain=executionCompletedAtCount:{len(family_receipt_execution_completed_at[edition])}"
        )

if (
    len(family_receipt_candidate_snapshot_ids["sr4"]) == 1
    and len(family_receipt_candidate_snapshot_ids["sr6"]) == 1
    and family_receipt_candidate_snapshot_ids["sr4"]
    != family_receipt_candidate_snapshot_ids["sr6"]
):
    weak_family_receipts.append("crossEdition:candidateSnapshotIdMismatch")

if (
    len(family_receipt_execution_run_digests["sr4"]) == 1
    and len(family_receipt_execution_run_digests["sr6"]) == 1
    and family_receipt_execution_run_digests["sr4"]
    == family_receipt_execution_run_digests["sr6"]
):
    weak_family_receipts.append("crossEdition:executionRunDigestReused")

for edition, ledger_payload, expected_proof_kind in (
    ("sr4", sr4_ledger, "sr4_family_oracle"),
    ("sr6", sr6_ledger, "sr6_family_release_gated_execution"),
):
    seen: set[str] = set()
    for family_id, audit_tests, rel_path in iter_execution_receipts(ledger_payload):
        key = f"{edition}:{family_id}:{rel_path}"
        if key in seen:
            continue
        seen.add(key)
        checked_execution_receipts += 1
        candidate = repo_root / rel_path
        if not path_within_root(candidate, repo_root):
            workflow_execution_receipts_outside_repo_root.append(
                f"{edition}:{family_id}:{rel_path}->{candidate.resolve()}"
            )
            continue
        try:
            payload, _ = load_regular_json(candidate, "workflow family execution receipt")
        except ValueError as exc:
            if not candidate.exists() and not candidate.is_symlink():
                missing_execution_receipts.append(f"{edition}:{family_id}:{rel_path}")
            else:
                weak_execution_receipts.append(f"{edition}:{family_id}:{rel_path}={exc}")
            continue
        status = payload.get("status")
        evidence_payload = payload.get("evidence") if isinstance(payload.get("evidence"), dict) else {}
        matched_passed_tests = {
            str(value).strip()
            for value in (evidence_payload.get("matchedPassedTests") or [])
            if str(value).strip()
        }
        missing_audit_tests = [
            str(value).strip()
            for value in (evidence_payload.get("missingAuditTests") or [])
            if str(value).strip()
        ]
        failed_audit_tests = evidence_payload.get("failedAuditTests") if isinstance(evidence_payload.get("failedAuditTests"), dict) else {}
        dotnet_test = evidence_payload.get("dotnetTest") if isinstance(evidence_payload.get("dotnetTest"), dict) else {}
        proof_kind = str(evidence_payload.get("proofKind") or "").strip()

        if status != "pass":
            failing_execution_receipts.append(f"{edition}:{family_id}:{rel_path}={status or 'missing'}")
            external_blockers = collect_external_blockers(evidence_payload)
            if external_blockers_are_only_missing_api_surface_contract(external_blockers):
                failing_execution_receipts_external.append(
                    f"{edition}:{family_id}:{rel_path}=external_blocker:missing_api_surface_contract"
                )
            continue

        if proof_kind != expected_proof_kind:
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=proofKind:{proof_kind or 'missing'}"
            )
        if any(test_name not in matched_passed_tests for test_name in audit_tests):
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=matchedPassedTests:{len(matched_passed_tests)}/{len(audit_tests)}"
            )
        if missing_audit_tests:
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=missingAuditTests:{','.join(sorted(missing_audit_tests))}"
            )
        if failed_audit_tests:
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=failedAuditTests"
            )
        dotnet_exit_code = dotnet_test.get("exitCode")
        if type(dotnet_exit_code) is not int or dotnet_exit_code != 0:
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=dotnetExit:{dotnet_exit_code!r}"
            )

legacy_execution_receipt_paths = sorted(
    str(path.resolve())
    for path in (repo_root / ".codex-studio" / "published" / "workflow-family-parity" / "execution").glob(
        "**/*.generated.json"
    )
    if path.is_file()
)

evidence["workflow_family_receipt_count_checked"] = checked_family_receipts
evidence["workflow_family_missing_receipts"] = missing_family_receipts
evidence["workflow_family_failing_receipts"] = failing_family_receipts
evidence["workflow_family_failing_receipts_external"] = (
    failing_family_receipts_external
)
evidence["workflow_family_weak_receipts"] = weak_family_receipts
evidence["workflow_family_producer_run_ids"] = {
    edition: sorted(values) for edition, values in family_receipt_run_ids.items()
}
evidence["workflow_family_candidate_digests"] = {
    edition: sorted(values) for edition, values in family_receipt_candidate_digests.items()
}
evidence["workflow_family_receipts_outside_repo_root"] = (
    workflow_family_receipts_outside_repo_root
)
evidence["workflow_execution_receipt_count_checked"] = checked_execution_receipts
evidence["workflow_execution_missing_receipts"] = missing_execution_receipts
evidence["workflow_execution_failing_receipts"] = failing_execution_receipts
evidence["workflow_execution_failing_receipts_external"] = (
    failing_execution_receipts_external
)
evidence["workflow_execution_receipts_outside_repo_root"] = (
    workflow_execution_receipts_outside_repo_root
)
evidence["workflow_execution_weak_receipts"] = weak_execution_receipts
evidence["legacy_execution_receipt_paths"] = legacy_execution_receipt_paths
evidence["required_workflow_family_ids"] = sorted(REQUIRED_WORKFLOW_FAMILY_IDS)
evidence["direct_flagship_workflow_family_ids"] = sorted(DIRECT_FLAGSHIP_WORKFLOW_FAMILY_IDS)
evidence["missing_required_workflow_family_ids"] = missing_required_family_ids
evidence["not_ready_required_workflow_family_ids"] = not_ready_required_family_ids
evidence["missing_required_workflow_family_audit_tests"] = missing_required_family_audit_tests
evidence["workflow_family_failing_receipts_direct_slice"] = sorted(
    entry for entry in failing_family_receipts if workflow_receipt_targets_direct_flagship_slice(entry)
)
evidence["workflow_execution_failing_receipts_direct_slice"] = sorted(
    entry for entry in failing_execution_receipts if workflow_receipt_targets_direct_flagship_slice(entry)
)
evidence["workflow_execution_weak_receipts_direct_slice"] = sorted(
    entry for entry in weak_execution_receipts if workflow_receipt_targets_direct_flagship_slice(entry)
)

if checked_family_receipts == 0:
    reasons.append("No SR4/SR6 family-level workflow receipts were discovered from ledgers.")
if missing_required_family_ids:
    reasons.append(
        "SR4/SR6 ledgers are missing required canonical workflow families: "
        + ", ".join(
            f"{edition}:{'|'.join(family_ids)}"
            for edition, family_ids in sorted(missing_required_family_ids.items())
        )
    )
if not_ready_required_family_ids:
    reasons.append(
        "SR4/SR6 required canonical workflow families are not ready: "
        + ", ".join(
            f"{edition}:{'|'.join(family_ids)}"
            for edition, family_ids in sorted(not_ready_required_family_ids.items())
        )
    )
if missing_required_family_audit_tests:
    reasons.append(
        "SR4/SR6 required canonical workflow families are missing audit tests: "
        + ", ".join(
            f"{edition}:{'|'.join(family_ids)}"
            for edition, family_ids in sorted(missing_required_family_audit_tests.items())
        )
    )
if missing_family_receipts:
    reasons.append(
        "Missing SR4/SR6 family-level workflow receipts: " + ", ".join(sorted(missing_family_receipts))
    )
if workflow_family_receipts_outside_repo_root:
    reasons.append(
        "SR4/SR6 family-level workflow receipts resolve outside this repo root: "
        + ", ".join(sorted(workflow_family_receipts_outside_repo_root))
    )
all_failing_family_receipts_external = bool(failing_family_receipts) and (
    len(failing_family_receipts_external) == len(failing_family_receipts)
)
evidence["workflow_family_failures_external_only"] = (
    all_failing_family_receipts_external
)
evidence["workflow_family_external_only_deferred"] = False
if failing_family_receipts:
    reasons.append(
        "SR4/SR6 family-level workflow receipts are not passing: "
        + ", ".join(sorted(failing_family_receipts))
    )
if weak_family_receipts:
    reasons.append(
        "SR4/SR6 family-level workflow receipt provenance is invalid: "
        + ", ".join(sorted(weak_family_receipts))
    )
if checked_execution_receipts == 0:
    reasons.append("No SR4/SR6 family-level execution receipts were discovered from ledgers.")
if missing_execution_receipts:
    reasons.append(
        "Missing SR4/SR6 family-level execution receipts: " + ", ".join(sorted(missing_execution_receipts))
    )
if workflow_execution_receipts_outside_repo_root:
    reasons.append(
        "SR4/SR6 family-level execution receipts resolve outside this repo root: "
        + ", ".join(sorted(workflow_execution_receipts_outside_repo_root))
    )
all_failing_execution_receipts_external = bool(failing_execution_receipts) and (
    len(failing_execution_receipts_external) == len(failing_execution_receipts)
)
evidence["workflow_execution_failures_external_only"] = (
    all_failing_execution_receipts_external
)
evidence["workflow_execution_external_only_deferred"] = False
if failing_execution_receipts:
    reasons.append(
        "SR4/SR6 family-level execution receipts are not passing: "
        + ", ".join(sorted(failing_execution_receipts))
    )
if weak_execution_receipts:
    reasons.append(
        "SR4/SR6 family-level execution receipts are not explicitly grounded: "
        + ", ".join(sorted(weak_execution_receipts))
    )
if legacy_execution_receipt_paths:
    reasons.append(
        "Legacy workflow-family execution receipts still exist under deprecated path "
        "`.codex-studio/published/workflow-family-parity/execution`; only `.../executed/...` "
        "paths are canonical: "
        + ", ".join(legacy_execution_receipt_paths)
    )

upstream_receipt_review_reasons: List[str] = []
for label in (
    "chummer5a_workflow_parity",
    "sr4_workflow_parity",
    "sr6_workflow_parity",
    "sr4_sr6_frontier",
    "ruleset_ui_adaptation",
    "desktop_visual_familiarity_gate",
    "chummer5a_screenshot_review_gate",
    "ui_flagship_release_gate",
    "next90_m141_direct_import_route_proof",
):
    effective_status = str(evidence.get(f"{label}_effective_status") or evidence.get(f"{label}_status") or "")
    if not status_ok(effective_status):
        upstream_receipt_review_reasons.append(f"{label}:status")
    generated_at_raw = str(evidence.get(f"{label}_generated_at") or "").strip()
    if not generated_at_raw:
        upstream_receipt_review_reasons.append(f"{label}:generated_at")
    age_seconds_value = evidence.get(f"{label}_age_seconds")
    if isinstance(age_seconds_value, int) and age_seconds_value > DESKTOP_PROOF_MAX_AGE_SECONDS:
        upstream_receipt_review_reasons.append(f"{label}:stale")
    future_skew_value = evidence.get(f"{label}_future_skew_seconds")
    if isinstance(future_skew_value, int) and future_skew_value > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
        upstream_receipt_review_reasons.append(f"{label}:future_skew")

release_channel_review_reasons: List[str] = []
if not release_channel_exists:
    release_channel_review_reasons.append("release_channel:missing")
if release_channel_contract_name != "Chummer.Hub.Registry.Contracts":
    release_channel_review_reasons.append("release_channel:contract_name")
if release_channel_status != "published":
    release_channel_review_reasons.append("release_channel:status")
if not release_channel_channel_id:
    release_channel_review_reasons.append("release_channel:channel_id")
if not release_channel_version:
    release_channel_review_reasons.append("release_channel:version")
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    release_channel_review_reasons.append("release_channel:generated_at")
release_channel_age_value = evidence.get("release_channel_age_seconds")
if isinstance(release_channel_age_value, int) and release_channel_age_value > DESKTOP_PROOF_MAX_AGE_SECONDS:
    release_channel_review_reasons.append("release_channel:stale")
release_channel_future_skew_value = evidence.get("release_channel_future_skew_seconds")
if isinstance(release_channel_future_skew_value, int) and release_channel_future_skew_value > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
    release_channel_review_reasons.append("release_channel:future_skew")
for label, channel_id in receipt_channel_ids.items():
    if not channel_id:
        release_channel_review_reasons.append(f"{label}:channel_id")
    elif release_channel_channel_id and channel_id != release_channel_channel_id:
        release_channel_review_reasons.append(f"{label}:channel_alignment")
for label, receipt_version in receipt_release_versions.items():
    if not receipt_version:
        release_channel_review_reasons.append(f"{label}:release_version")
    elif release_channel_version and receipt_version != release_channel_version:
        release_channel_review_reasons.append(f"{label}:release_version_alignment")

flagship_head_review_reasons: List[str] = []
for head in missing_canonical_required_desktop_heads:
    flagship_head_review_reasons.append(f"missing_canonical_head:{head}")
for head in required_desktop_heads:
    if not status_ok(flagship_head_proof_statuses.get(head, "")):
        flagship_head_review_reasons.append(f"{head}:status")
    if not flagship_head_source_test_file_exists.get(head, False):
        flagship_head_review_reasons.append(f"{head}:source_test_file_exists")
    if not flagship_head_source_test_file_within_repo_root.get(head, False):
        flagship_head_review_reasons.append(f"{head}:source_test_file_within_repo_root")
    for marker in flagship_head_missing_contract_markers.get(head, []):
        flagship_head_review_reasons.append(f"{head}:marker:{marker}")

workflow_family_review_reasons: List[str] = []
if checked_family_receipts == 0:
    workflow_family_review_reasons.append("checked_family_receipts")
workflow_family_review_reasons.extend(
    [f"missing_required_family:{family_id}" for family_id in sorted(missing_required_family_ids)]
)
workflow_family_review_reasons.extend(
    [f"not_ready_required_family:{family_id}" for family_id in sorted(not_ready_required_family_ids)]
)
workflow_family_review_reasons.extend(
    [
        f"missing_audit_tests:{edition}:{'|'.join(family_ids)}"
        for edition, family_ids in sorted(missing_required_family_audit_tests.items())
    ]
)
workflow_family_review_reasons.extend(
    [f"missing_receipt:{entry}" for entry in sorted(missing_family_receipts)]
)
workflow_family_review_reasons.extend(
    [f"outside_repo_root:{entry}" for entry in sorted(workflow_family_receipts_outside_repo_root)]
)
workflow_family_review_reasons.extend(
    [f"failing_receipt:{entry}" for entry in sorted(failing_family_receipts)]
)
workflow_family_review_reasons.extend(
    [f"weak_receipt:{entry}" for entry in sorted(weak_family_receipts)]
)

workflow_execution_review_reasons: List[str] = []
if checked_execution_receipts == 0:
    workflow_execution_review_reasons.append("checked_execution_receipts")
workflow_execution_review_reasons.extend(
    [f"missing_execution:{entry}" for entry in sorted(missing_execution_receipts)]
)
workflow_execution_review_reasons.extend(
    [f"outside_repo_root:{entry}" for entry in sorted(workflow_execution_receipts_outside_repo_root)]
)
workflow_execution_review_reasons.extend(
    [f"failing_execution:{entry}" for entry in sorted(failing_execution_receipts)]
)
workflow_execution_review_reasons.extend(
    [f"weak_execution:{entry}" for entry in sorted(weak_execution_receipts)]
)
workflow_execution_review_reasons.extend(
    [f"legacy_execution_path:{entry}" for entry in legacy_execution_receipt_paths]
)

direct_flagship_slice_review_reasons: List[str] = []
for marker_group, marker_result in direct_workflow_marker_results.items():
    for marker in marker_result["missing"]:
        direct_flagship_slice_review_reasons.append(f"{marker_group}:runtime_marker:{marker}")
if missing_direct_screenshot_files:
    direct_flagship_slice_review_reasons.extend(
        [f"required_screenshot:{screenshot}" for screenshot in missing_direct_screenshot_files]
    )
if not status_ok(dense_initiative_route.get("status")):
    direct_flagship_slice_review_reasons.append("dense_workbench_and_initiative:route_status")
if missing_required_review_jobs:
    direct_flagship_slice_review_reasons.extend(
        [f"review_job_missing:{job_name}" for job_name in missing_required_review_jobs]
    )
if failing_required_review_jobs:
    direct_flagship_slice_review_reasons.extend(
        [f"review_job_not_ready:{job_name}" for job_name in failing_required_review_jobs]
    )

direct_flagship_slice_runtime_proof_closes_direct_workflow_gate = (
    not direct_flagship_slice_review_reasons
)
evidence["direct_flagship_slice_runtime_proof_closes_direct_workflow_gate"] = (
    direct_flagship_slice_runtime_proof_closes_direct_workflow_gate
)
evidence["direct_flagship_slice_waives_blockers"] = False
evidence["direct_flagship_slice_deferred_reason_items"] = []
evidence["upstream_receipt_review_deferred_reasons"] = []
evidence["workflow_family_review_deferred_reasons"] = []
evidence["workflow_execution_review_deferred_reasons"] = []
for reason in direct_flagship_slice_review_reasons:
    reasons.append(f"Direct flagship workflow proof is incomplete: {reason}.")

family_publication_snapshot_error = ""
try:
    if release_identity and file_binding(
        release_channel_path, "desktop workflow release channel receipt"
    ) != {key: release_identity[key] for key in ("path", "sha256", "sizeBytes")}:
        raise ValueError("release channel bytes changed after family-chain validation")
    for receipt_path_text, expected_binding in upstream_receipt_bindings.items():
        upstream_path = Path(receipt_path_text)
        if file_binding(upstream_path, "desktop workflow upstream receipt") != expected_binding:
            raise ValueError(f"upstream receipt changed after validation: {upstream_path}")
    for source_path_text, expected_binding in direct_source_bindings.items():
        source_path = Path(source_path_text)
        if file_binding(source_path, "desktop workflow direct source") != expected_binding:
            raise ValueError(f"direct proof source changed after validation: {source_path}")
    for edition, ledger_path in (("sr4", sr4_ledger_path), ("sr6", sr6_ledger_path)):
        if ledger_bindings.get(edition) and file_binding(
            ledger_path, f"{edition.upper()} workflow parity ledger"
        ) != ledger_bindings[edition]:
            raise ValueError(f"{edition.upper()} ledger bytes changed after family-chain validation")
        if oracle_bindings.get(edition) and file_binding(
            oracle_paths[edition], f"{edition.upper()} workflow parity oracle"
        ) != oracle_bindings[edition]:
            raise ValueError(f"{edition.upper()} oracle bytes changed after family-chain validation")
    if not family_candidate_input_error:
        if [
            file_binding(path, "workflow parity test source")
            for path in family_test_source_paths
        ] != family_test_source_bindings:
            raise ValueError("workflow parity test source bytes changed after family-chain validation")
        if file_binding(
            trx_contract_source_path, "workflow-family TRX validator source"
        ) != family_trx_contract_source_binding:
            raise ValueError("workflow-family TRX validator changed after family-chain validation")
        if [
            file_binding(project_path, f"{project_label} project contract")
            for project_label, project_path in family_test_build_projects
        ] != family_build_project_bindings:
            raise ValueError("workflow parity build projects changed after family-chain validation")
        if file_binding(
            family_api_project_path, "canonical API autostart project"
        ) != family_api_project_binding:
            raise ValueError("canonical API project changed after family-chain validation")
        if file_binding(
            family_dotnet_host_path, "canonical dotnet host"
        ) != family_dotnet_host_binding:
            raise ValueError("canonical dotnet host changed after family-chain validation")
        if {
            label: snapshot_output_tree(root, f"{label} release build output")
            for label, root in family_build_output_roots.items()
        } != family_build_output_bindings:
            raise ValueError("workflow parity build outputs changed after family-chain validation")
        if file_binding(
            family_test_assembly_path, "workflow parity test assembly"
        ) != family_test_assembly_binding:
            raise ValueError("workflow parity test assembly bytes changed after family-chain validation")
    for stage_edition in ("sr4", "sr6"):
        snapshot_upstream_bindings: List[Dict[str, Any]] = []
        for stage_name in ("execution", "verification", "parity"):
            stage_expected = workflow_stage_expected_receipts.get(
                stage_edition, {}
            ).get(stage_name)
            if not isinstance(stage_expected, dict):
                raise ValueError(
                    f"{stage_edition}:{stage_name} expected receipt inventory is unavailable"
                )
            stage_snapshot = validate_workflow_stage_manifest(
                manifest_path=workflow_stage_manifest_path(
                    repo_root, stage_edition, stage_name
                ),
                repo_root=repo_root,
                edition=stage_edition,
                stage=stage_name,
                expected_receipts=stage_expected,
                expected_release_identity=release_identity,
                expected_upstream_stage_manifests=list(snapshot_upstream_bindings),
                require_pass=True,
            )
            if (
                stage_snapshot["manifest"]
                != workflow_stage_manifest_payloads.get(stage_edition, {}).get(stage_name)
                or stage_snapshot["binding"]
                != workflow_stage_manifest_bindings.get(stage_edition, {}).get(stage_name)
            ):
                raise ValueError(
                    f"{stage_edition}:{stage_name} manifest changed after family-chain validation"
                )
            snapshot_upstream_bindings.append(stage_snapshot["binding"])
    for receipt_path_text, expected_binding in validated_family_receipt_bindings.items():
        family_receipt_path = Path(receipt_path_text)
        family_receipt, family_receipt_bytes = load_regular_json(
            family_receipt_path, "workflow family receipt"
        )
        if binding_for_bytes(family_receipt_path, family_receipt_bytes) != expected_binding:
            raise ValueError(f"workflow family receipt changed after validation: {family_receipt_path}")
        if str(family_receipt.get("contract_name") or "").endswith("_execution_receipt"):
            family_evidence = family_receipt.get("evidence")
            test_executions = (
                family_evidence.get("testExecutions")
                if isinstance(family_evidence, dict)
                else None
            )
            if not isinstance(test_executions, dict):
                raise ValueError(f"execution testExecutions changed after validation: {family_receipt_path}")
            producer_run_id = family_receipt.get("producerRunId")
            if not isinstance(producer_run_id, str):
                raise ValueError(
                    f"execution producerRunId changed after validation: {family_receipt_path}"
                )
            family_edition = str((family_evidence or {}).get("edition") or "")
            execution_manifest = workflow_stage_manifest_payloads.get(
                family_edition, {}
            ).get("execution")
            if not isinstance(execution_manifest, dict):
                raise ValueError(
                    f"execution stage manifest changed after validation: {family_receipt_path}"
                )
            expected_run_root = (
                repo_root
                / ".codex-studio/out/workflow-family-parity/executed"
                / family_edition
                / producer_run_id
            )
            for test_name, record in test_executions.items():
                trx_binding = record.get("trx") if isinstance(record, dict) else None
                if not isinstance(trx_binding, dict) or not isinstance(trx_binding.get("path"), str):
                    raise ValueError(f"TRX binding changed after validation: {test_name}")
                validated_trx = validate_trx_contract(
                    Path(trx_binding["path"]),
                    test_name,
                    trx_binding,
                    expected_run_root,
                    execution_manifest.get("executionStartedAt"),
                    execution_manifest.get("executionCompletedAt"),
                )
                if (
                    record.get("testMethodClassName") != validated_trx["className"]
                    or record.get("testId") != validated_trx["testId"]
                    or record.get("counters") != validated_trx["counters"]
                ):
                    raise ValueError(
                        f"TRX claims changed after validation: {test_name}"
                    )
except (ValueError, OSError) as exc:
    family_publication_snapshot_error = str(exc)

if family_publication_snapshot_error:
    reasons.append(
        "Workflow-family provenance inputs changed before desktop gate publication: "
        + family_publication_snapshot_error
    )
    workflow_family_review_reasons.append(
        "publication_snapshot:" + family_publication_snapshot_error
    )
    workflow_execution_review_reasons.append(
        "publication_snapshot:" + family_publication_snapshot_error
    )
evidence["workflow_family_publication_snapshot_error"] = family_publication_snapshot_error

non_deferred_nested_review_reasons = {
    "upstreamReceiptReview": upstream_receipt_review_reasons,
    "releaseChannelReview": release_channel_review_reasons,
    "flagshipHeadReview": flagship_head_review_reasons,
    "workflowFamilyReview": workflow_family_review_reasons,
    "workflowExecutionReview": workflow_execution_review_reasons,
    "directFlagshipSliceReview": direct_flagship_slice_review_reasons,
}
non_deferred_nested_review_failure_count = sum(
    len(review_reasons)
    for review_reasons in non_deferred_nested_review_reasons.values()
)

producer_run_id = str(uuid.uuid4())
aggregate_candidate_snapshot_ids = set(candidate_snapshot_ids.values())
aggregate_candidate_snapshot_id = (
    next(iter(aggregate_candidate_snapshot_ids))
    if len(aggregate_candidate_snapshot_ids) == 1
    else ""
)
if not aggregate_candidate_snapshot_id:
    reasons.append("A single cross-edition candidate snapshot could not be established.")

execution_epoch_core: Dict[str, Any] = {}
execution_epoch_id = ""
execution_epoch_span_seconds: int | None = None
execution_epoch_error = ""
try:
    if not aggregate_candidate_snapshot_id:
        raise ValueError("candidate snapshot is unavailable")
    for edition in ("sr4", "sr6"):
        execution_manifest = workflow_stage_manifest_payloads.get(edition, {}).get(
            "execution"
        )
        if not isinstance(execution_manifest, dict):
            raise ValueError(f"{edition} execution manifest is unavailable")
        receipt_identity_sets = {
            "producerRunId": family_receipt_run_ids[edition],
            "candidateSnapshotId": family_receipt_candidate_snapshot_ids[edition],
            "executionRunDigest": family_receipt_execution_run_digests[edition],
            "executionStartedAt": family_receipt_execution_started_at[edition],
            "executionCompletedAt": family_receipt_execution_completed_at[edition],
        }
        if any(
            observed_values != {execution_manifest.get(identity_key)}
            for identity_key, observed_values in receipt_identity_sets.items()
        ):
            raise ValueError(
                f"{edition} receipt identities do not match the authoritative execution manifest"
            )
    execution_epoch_result = build_desktop_execution_epoch(
        release_identity=release_identity,
        candidate_snapshot_id=aggregate_candidate_snapshot_id,
        stage_manifests=workflow_stage_manifest_payloads,
        stage_bindings=workflow_stage_manifest_bindings,
    )
    execution_epoch_id = execution_epoch_result["executionEpochId"]
    execution_epoch_core = execution_epoch_result["executionEpoch"]
    execution_epoch_span_seconds = execution_epoch_result[
        "executionEpochSpanSeconds"
    ]
    if (
        execution_epoch_result["executionEpochMaxSpanSeconds"]
        != DESKTOP_EXECUTION_EPOCH_MAX_SPAN_SECONDS
    ):
        raise ValueError("execution epoch fixed span policy differs from the aggregate")
except (ValueError, OSError) as exc:
    execution_epoch_error = str(exc)
if not execution_epoch_id:
    reasons.append(
        "A bounded, distinct SR4/SR6 execution epoch could not be established: "
        + (execution_epoch_error or "unknown epoch validation failure")
    )

evidence["producerRunId"] = producer_run_id
evidence["candidateSnapshotId"] = aggregate_candidate_snapshot_id
evidence["workflowEpochId"] = aggregate_candidate_snapshot_id
evidence["executionEpochId"] = execution_epoch_id
evidence["executionEpoch"] = execution_epoch_core
evidence["executionEpochMaxSpanSeconds"] = DESKTOP_EXECUTION_EPOCH_MAX_SPAN_SECONDS
evidence["executionEpochSpanSeconds"] = execution_epoch_span_seconds
evidence["executionEpochError"] = execution_epoch_error
evidence["workflow_family_expected_candidate_snapshot_ids"] = candidate_snapshot_ids
evidence["workflow_family_expected_workflow_epoch_ids"] = candidate_snapshot_ids
evidence["workflow_family_observed_candidate_snapshot_ids"] = {
    edition: sorted(values)
    for edition, values in family_receipt_candidate_snapshot_ids.items()
}
evidence["workflow_family_observed_workflow_epoch_ids"] = evidence[
    "workflow_family_observed_candidate_snapshot_ids"
]
evidence["workflow_family_observed_execution_run_digests"] = {
    edition: sorted(values)
    for edition, values in family_receipt_execution_run_digests.items()
}
evidence["workflow_stage_manifest_bindings"] = workflow_stage_manifest_bindings
evidence["workflow_stage_manifest_errors"] = workflow_stage_manifest_errors

status = (
    "pass"
    if not reasons and non_deferred_nested_review_failure_count == 0
    else "fail"
)
generated_at = now_iso()
payload = {
    "schemaVersion": 1,
    "generatedAt": generated_at,
    "producerRunId": producer_run_id,
    "candidateSnapshotId": aggregate_candidate_snapshot_id,
    "workflowEpochId": aggregate_candidate_snapshot_id,
    "executionEpochId": execution_epoch_id,
    "contract_name": "chummer6-ui.desktop_workflow_execution_gate",
    "channelId": release_channel_channel_id,
    "channel": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "version": release_channel_version,
    "status": status,
    "summary": (
        "Desktop workflow execution gate is proven by passing Chummer5a/SR4/SR6 parity receipts and a byte-bound, cross-edition family execution epoch."
        if status == "pass"
        else "Desktop workflow execution gate is not fully proven."
    ),
    "reasons": reasons,
    "reviews": {
        review_name: {
            "status": "pass" if not review_reasons else "fail",
            "reasons": review_reasons,
        }
        for review_name, review_reasons in non_deferred_nested_review_reasons.items()
    },
    "evidence": evidence,
}
payload["evidence"]["rawReasonCount"] = len(reasons)
payload["evidence"]["nestedReviewFailureCount"] = (
    non_deferred_nested_review_failure_count
)
payload["evidence"]["failureCount"] = (
    len(reasons) + non_deferred_nested_review_failure_count
)


def write_json_atomic(path: Path, value: Dict[str, Any]) -> None:
    encoded = (json.dumps(value, indent=2) + "\n").encode("utf-8")
    parent = path.parent
    if not parent.is_dir():
        raise ValueError(f"receipt parent is not a directory: {parent}")
    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=str(parent)
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        directory_fd = os.open(
            parent,
            os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_CLOEXEC", 0),
        )
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        try:
            temporary_path.unlink()
        except FileNotFoundError:
            pass


write_json_atomic(receipt_path, payload)
if status != "pass":
    raise SystemExit(43)
PY

if [[ "$refresh_flagship_readiness" == "1" ]]; then
  python3 "$flagship_product_readiness_materializer_path" >/dev/null
fi

echo "[desktop-workflow-execution-gate] PASS"
