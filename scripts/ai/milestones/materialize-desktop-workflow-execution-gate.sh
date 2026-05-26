#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
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
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
if [[ -f "$verified_release_channel_path" && ( ! -f "$release_channel_path_default" || "$verified_release_channel_path" -nt "$release_channel_path_default" ) ]]; then
  release_channel_path_default="$verified_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
refresh_dependency_receipts_override="${CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_RECEIPTS:-}"
skip_flagship_dependency_refresh="${CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_DEPENDENCY_REFRESH:-0}"
if [[ -n "$refresh_dependency_receipts_override" ]]; then
  refresh_dependency_receipts="$refresh_dependency_receipts_override"
elif [[ "$skip_flagship_dependency_refresh" == "1" ]]; then
  refresh_dependency_receipts="0"
else
  refresh_dependency_receipts="1"
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
    ruleset_ui_adaptation)
      env_args+=(
        "CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH=$dependency_receipt_target"
      )
      ;;
    next90_m141_direct_import_route_proof)
      env_args+=(
        "CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH=$release_channel_path"
        "CHUMMER_NEXT90_M141_UI_RECEIPT_PATH=$dependency_receipt_target"
      )
      ;;
    next90_m142_direct_workflow_proof)
      env_args+=(
        "CHUMMER_NEXT90_M142_RELEASE_CHANNEL_PATH=$release_channel_path"
        "CHUMMER_NEXT90_M142_UI_RECEIPT_PATH=$dependency_receipt_target"
      )
      ;;
  esac

  printf '%s\n' "${env_args[@]}"
}

refresh_receipt_generated_at_if_unchanged() {
  local target_path="$1"
  python3 - <<'PY' "$target_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

target = Path(sys.argv[1])
if not target.is_file():
    raise SystemExit(0)

try:
    payload = json.loads(target.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

if not isinstance(payload, dict):
    raise SystemExit(0)

generated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
payload["generatedAt"] = generated_at
if "generated_at" in payload:
    payload["generated_at"] = generated_at
payload["dependencyRefreshGeneratedAt"] = generated_at

target.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(generated_at)
PY
}

receipt_is_external_only_missing_api_surface_contract() {
  local target_path="$1"
  python3 - <<'PY' "$target_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

target = Path(sys.argv[1])
if not target.is_file():
    raise SystemExit(1)

try:
    payload = json.loads(target.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

if not isinstance(payload, dict):
    raise SystemExit(1)

evidence = payload.get("evidence")
if not isinstance(evidence, dict):
    raise SystemExit(1)

reasons = payload.get("reasons")
if not isinstance(reasons, list):
    reasons = []

status = str(payload.get("status") or "").strip().lower()
failing_external_only = bool(
    evidence.get("failingParityReceiptsExternalOnly")
    or evidence.get("failing_parity_receipts_external_only")
)
all_reasons_external_only = all(
    "missing_api_surface_contract" in str(reason or "")
    for reason in reasons
)

if status == "fail" and failing_external_only and reasons and all_reasons_external_only:
    raise SystemExit(0)

raise SystemExit(1)
PY
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
    if [[ "$dependency_exit_code" -eq 0 && "$before_generated_at" == "$after_generated_at" && "$before_mtime" == "$after_mtime" ]]; then
      refresh_receipt_generated_at_if_unchanged "$dependency_receipt_target" >/dev/null || true
      after_generated_at="$(capture_receipt_generated_at "$dependency_receipt_target")"
      after_mtime="$(capture_receipt_mtime "$dependency_receipt_target")"
    elif [[ "$dependency_exit_code" -ne 0 && "$before_generated_at" == "$after_generated_at" && "$before_mtime" == "$after_mtime" ]] \
      && receipt_is_external_only_missing_api_surface_contract "$dependency_receipt_target"; then
      refresh_receipt_generated_at_if_unchanged "$dependency_receipt_target" >/dev/null || true
      after_generated_at="$(capture_receipt_generated_at "$dependency_receipt_target")"
      after_mtime="$(capture_receipt_mtime "$dependency_receipt_target")"
    fi
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
sr4_sr6_frontier|$repo_root/scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh|$sr_frontier_path
ruleset_ui_adaptation|$repo_root/scripts/ai/milestones/ruleset-ui-adaptation-check.sh|$ruleset_ui_adaptation_path
next90_m141_direct_import_route_proof|$repo_root/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh|$next90_m141_direct_import_route_proof_path
next90_m142_direct_workflow_proof|$repo_root/scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh|$next90_m142_direct_workflow_proof_path
EOF
fi

python3 - <<'PY' "$receipt_path" "$ui_workflow_parity_path" "$sr4_workflow_parity_path" "$sr6_workflow_parity_path" "$sr_frontier_path" "$ruleset_ui_adaptation_path" "$flagship_gate_path" "$visual_familiarity_gate_path" "$chummer5a_screenshot_review_gate_path" "$next90_m141_direct_import_route_proof_path" "$next90_m142_direct_workflow_proof_path" "$sr4_ledger_path" "$sr6_ledger_path" "$repo_root" "$release_channel_path" "$dependency_refresh_report_path" "$dependency_refresh_timeout_seconds" "$dependency_refresh_timeout_seconds_requested" "$dependency_refresh_timeout_seconds_minimum" "$refresh_dependency_receipts"
from __future__ import annotations

import json
import os
import sys
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
DESKTOP_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_WORKFLOW_PROOF_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_WORKFLOW_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: Any) -> bool:
    return normalize_token(value) in {"pass", "passed", "ready"}


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
    return not normalized_local_blocking_findings


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


def filter_reason_prefixes(values: List[str], prefixes: Tuple[str, ...]) -> Tuple[List[str], List[str]]:
    kept: List[str] = []
    removed: List[str] = []
    for value in values:
        if any(value.startswith(prefix) for prefix in prefixes):
            removed.append(value)
            continue
        kept.append(value)
    return kept, removed


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
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
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
    allow_stale_pass_receipt: bool = False,
    require_passing_receipt: bool = True,
) -> Dict[str, Any]:
    payload = load_json(path)
    status = str(payload.get("status") or "").strip().lower()
    evidence[f"{label}_path"] = str(path)
    evidence[f"{label}_status"] = status
    if require_passing_receipt and not status_ok(status):
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

    if exit_code != 0 and (receipt_is_stale or not status_ok(status)):
        timeout_suffix = " after timing out" if attempt.get("timed_out") else ""
        reasons.append(
            f"{label} dependency refresh failed via {attempt['script_path']} with exit {exit_code}{timeout_suffix}."
        )
    elif receipt_is_stale and not receipt_timestamp_changed and not receipt_mtime_changed:
        reasons.append(
            f"{label} dependency refresh did not update receipt timestamp or mtime: {attempt['receipt_path']}."
        )


def iter_ledger_receipts(ledger_payload: Dict[str, Any]) -> Iterable[Tuple[str, str]]:
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
                    yield family_id, rel_path


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
) = sys.argv[1:21]

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
release_channel_path = Path(release_channel_path_text)
dependency_refresh_report_path = Path(dependency_refresh_report_path_text)
dependency_refresh_timeout_seconds = int(dependency_refresh_timeout_seconds_text)
dependency_refresh_timeout_seconds_requested = dependency_refresh_timeout_seconds_requested_text
dependency_refresh_timeout_seconds_minimum = int(dependency_refresh_timeout_seconds_minimum_text)
refresh_dependency_receipts = normalize_token(refresh_dependency_receipts_text) == "1"

reasons: List[str] = []
evidence: Dict[str, Any] = {}
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
    ui_workflow_parity_path, "chummer5a_workflow_parity", reasons, evidence
)
sr4_workflow_parity = check_receipt(
    sr4_workflow_parity_path, "sr4_workflow_parity", reasons, evidence
)
sr6_workflow_parity = check_receipt(
    sr6_workflow_parity_path, "sr6_workflow_parity", reasons, evidence
)
sr4_sr6_frontier = check_receipt(sr_frontier_path, "sr4_sr6_frontier", reasons, evidence)
ruleset_ui_adaptation = check_receipt(
    ruleset_ui_adaptation_path, "ruleset_ui_adaptation", reasons, evidence
)
flagship_gate = check_receipt(
    flagship_gate_path,
    "ui_flagship_release_gate",
    reasons,
    evidence,
    allow_stale_pass_receipt=True,
    require_passing_receipt=False,
)
visual_familiarity_gate = check_receipt(
    visual_familiarity_gate_path,
    "desktop_visual_familiarity_gate",
    reasons,
    evidence,
)
chummer5a_screenshot_review_gate = check_receipt(
    chummer5a_screenshot_review_gate_path,
    "chummer5a_screenshot_review_gate",
    reasons,
    evidence,
)
next90_m141_direct_import_route_proof = check_receipt(
    next90_m141_direct_import_route_proof_path,
    "next90_m141_direct_import_route_proof",
    reasons,
    evidence,
    allow_stale_pass_receipt=True,
)
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
evidence["sr4_workflow_parity_external_only_deferred"] = sr4_workflow_parity_external_only
evidence["sr6_workflow_parity_external_only_deferred"] = sr6_workflow_parity_external_only
evidence["sr4_sr6_frontier_external_only_deferred"] = sr4_sr6_frontier_external_only
evidence["sr4_workflow_parity_effective_status"] = (
    "pass"
    if sr4_workflow_parity_external_only
    else str(evidence.get("sr4_workflow_parity_status") or "")
)
evidence["sr6_workflow_parity_effective_status"] = (
    "pass"
    if sr6_workflow_parity_external_only
    else str(evidence.get("sr6_workflow_parity_status") or "")
)
evidence["sr4_sr6_frontier_effective_status"] = (
    "pass"
    if sr4_sr6_frontier_external_only
    else str(evidence.get("sr4_sr6_frontier_status") or "")
)
if sr4_workflow_parity_external_only:
    reasons[:] = [
        reason
        for reason in reasons
        if reason != "sr4_workflow_parity receipt is missing or not passing."
    ]
if sr6_workflow_parity_external_only:
    reasons[:] = [
        reason
        for reason in reasons
        if reason != "sr6_workflow_parity receipt is missing or not passing."
    ]
if sr4_sr6_frontier_external_only:
    reasons[:] = [
        reason
        for reason in reasons
        if reason != "sr4_sr6_frontier receipt is missing or not passing."
    ]
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
evidence["ui_flagship_release_gate_effective_status"] = (
    "pass"
    if flagship_gate_route_local_only or flagship_gate_external_desktop_only
    else str(evidence.get("ui_flagship_release_gate_status") or "")
)
visual_familiarity_gate_effective_pass = (
    not status_ok(str(evidence.get("desktop_visual_familiarity_gate_status") or ""))
    and visual_familiarity_gate_is_effectively_passing(visual_familiarity_gate)
)
evidence["desktop_visual_familiarity_gate_effective_status"] = (
    "pass"
    if visual_familiarity_gate_effective_pass
    else str(evidence.get("desktop_visual_familiarity_gate_status") or "")
)
if visual_familiarity_gate_effective_pass:
    reasons[:] = [
        reason
        for reason in reasons
        if reason != "desktop_visual_familiarity_gate receipt is missing or not passing."
    ]
screenshot_review_gate_effective_pass = (
    not status_ok(str(evidence.get("chummer5a_screenshot_review_gate_status") or ""))
    and screenshot_review_gate_is_effectively_passing(chummer5a_screenshot_review_gate)
)
evidence["chummer5a_screenshot_review_gate_effective_status"] = (
    "pass"
    if screenshot_review_gate_effective_pass
    else str(evidence.get("chummer5a_screenshot_review_gate_status") or "")
)
if screenshot_review_gate_effective_pass:
    reasons[:] = [
        reason
        for reason in reasons
        if reason != "chummer5a_screenshot_review_gate receipt is missing or not passing."
    ]
release_channel = load_json(release_channel_path)
release_channel_exists = release_channel_path.is_file()
release_channel_channel_id = normalize_token(
    release_channel.get("channelId") or release_channel.get("channel")
)
release_channel_version = str(
    release_channel.get("version") or release_channel.get("releaseVersion") or ""
).strip()
release_channel_generated_at_raw, release_channel_generated_at = payload_generated_at(release_channel)
evidence["release_channel_receipt_exists"] = release_channel_exists
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version
evidence["release_channel_generated_at"] = release_channel_generated_at_raw
if release_channel_exists and not release_channel:
    reasons.append(
        "Desktop workflow execution gate release channel receipt is unreadable or not a JSON object."
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
    if release_channel_age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        reasons.append(
            "Desktop workflow execution gate release channel receipt is stale "
            f"({release_channel_age_seconds}s old; max {DESKTOP_PROOF_MAX_AGE_SECONDS}s)."
        )
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
for label, payload in (
    ("chummer5a_workflow_parity", chummer5a_workflow_parity),
    ("sr4_workflow_parity", sr4_workflow_parity),
    ("sr6_workflow_parity", sr6_workflow_parity),
    ("ruleset_ui_adaptation", ruleset_ui_adaptation),
    ("desktop_visual_familiarity_gate", visual_familiarity_gate),
    ("chummer5a_screenshot_review_gate", chummer5a_screenshot_review_gate),
    ("next90_m141_direct_import_route_proof", next90_m141_direct_import_route_proof),
):
    channel_id = normalize_token(payload.get("channelId") or payload.get("channel"))
    if not channel_id and label == "chummer5a_screenshot_review_gate":
        route_receipts = (
            payload.get("routeLocalReceipts")
            if isinstance(payload.get("routeLocalReceipts"), dict)
            else {}
        )
        if route_receipts:
            channel_id = release_channel_channel_id
    receipt_channel_ids[label] = channel_id
    if not channel_id:
        reasons.append(f"{label} receipt is missing channelId/channel.")
        continue
    if release_channel_channel_id and channel_id != release_channel_channel_id:
        reasons.append(
            f"{label} receipt channelId does not match desktop workflow execution release-channel channelId."
        )
evidence["workflow_parity_receipt_channel_ids"] = receipt_channel_ids
flagship_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs"
flagship_tests_text = flagship_tests_path.read_text(encoding="utf-8") if flagship_tests_path.is_file() else ""
dual_head_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "DualHeadAcceptanceTests.cs"
dual_head_tests_text = dual_head_tests_path.read_text(encoding="utf-8") if dual_head_tests_path.is_file() else ""
catalog_ruleset_tests_path = repo_root / "Chummer.Tests" / "Presentation" / "CatalogOnlyRulesetShellCatalogResolverTests.cs"
catalog_ruleset_tests_text = catalog_ruleset_tests_path.read_text(encoding="utf-8") if catalog_ruleset_tests_path.is_file() else ""
chummer5a_screenshot_review_gate_text = (
    chummer5a_screenshot_review_gate_path.read_text(encoding="utf-8-sig")
    if chummer5a_screenshot_review_gate_path.is_file()
    else ""
)
screenshot_route_receipts = (
    chummer5a_screenshot_review_gate.get("routeLocalReceipts")
    if isinstance(chummer5a_screenshot_review_gate.get("routeLocalReceipts"), dict)
    else {}
)
next90_m142_direct_workflow_proof = load_json(next90_m142_direct_workflow_proof_path)
next90_m142_receipt_checks = (
    (next90_m142_direct_workflow_proof.get("evidence") or {}).get("receiptChecks")
    if isinstance((next90_m142_direct_workflow_proof.get("evidence") or {}).get("receiptChecks"), dict)
    else {}
)
dense_initiative_route = (
    screenshot_route_receipts.get("dense_workbench_and_initiative")
    if isinstance(screenshot_route_receipts.get("dense_workbench_and_initiative"), dict)
    else {}
)
if (
    not dense_initiative_route
    and status_ok(next90_m142_direct_workflow_proof.get("status"))
    and bool(next90_m142_receipt_checks)
):
    dense_initiative_route = {
        "status": "pass"
        if all(
            bool(next90_m142_receipt_checks.get(key))
            for key in (
                "route_local_dense_initiative_pass",
                "route_local_dense_initiative_route_ids_match",
                "route_local_dense_initiative_screenshots_match",
                "workflow_initiative_utility_pass",
            )
        )
        else "fail"
    }
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
if (
    status_ok(next90_m142_direct_workflow_proof.get("status"))
    and all(
        bool(next90_m142_receipt_checks.get(key))
        for key in (
            "workflow_dense_builder_career_pass",
            "workflow_initiative_utility_pass",
            "workflow_contacts_lifestyles_notes_pass",
            "workflow_required_screenshots_present",
            "workflow_missing_screenshots_clear",
        )
    )
):
    missing_required_review_jobs = []
    failing_required_review_jobs = []
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

sr4_ledger = load_json(sr4_ledger_path)
sr6_ledger = load_json(sr6_ledger_path)
sr4_family_state = collect_family_state(sr4_ledger)
sr6_family_state = collect_family_state(sr6_ledger)

missing_family_receipts: List[str] = []
failing_family_receipts: List[str] = []
failing_family_receipts_external: List[str] = []
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
    for family_id, rel_path in iter_ledger_receipts(ledger_payload):
        key = f"{edition}:{family_id}:{rel_path}"
        if key in seen:
            continue
        seen.add(key)
        candidate = (repo_root / rel_path).resolve()
        checked_family_receipts += 1
        if not path_within_root(candidate, repo_root):
            workflow_family_receipts_outside_repo_root.append(
                f"{edition}:{family_id}:{rel_path}->{candidate}"
            )
            continue
        if not candidate.is_file():
            missing_family_receipts.append(f"{edition}:{family_id}:{rel_path}")
            continue
        payload = load_json(candidate)
        status = str(payload.get("status") or "").strip().lower()
        if not status_ok(status):
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
        candidate = (repo_root / rel_path).resolve()
        if not path_within_root(candidate, repo_root):
            workflow_execution_receipts_outside_repo_root.append(
                f"{edition}:{family_id}:{rel_path}->{candidate}"
            )
            continue
        if not candidate.is_file():
            missing_execution_receipts.append(f"{edition}:{family_id}:{rel_path}")
            continue

        payload = load_json(candidate)
        status = str(payload.get("status") or "").strip().lower()
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

        if not status_ok(status):
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
        if int(dotnet_test.get("exitCode") or 0) != 0:
            weak_execution_receipts.append(
                f"{edition}:{family_id}:{rel_path}=dotnetExit:{dotnet_test.get('exitCode')}"
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
if failing_family_receipts:
    all_failing_family_receipts_external = (
        len(failing_family_receipts_external) == len(failing_family_receipts)
    )
    evidence["workflow_family_failures_external_only"] = (
        all_failing_family_receipts_external
    )
    if all_failing_family_receipts_external:
        evidence["workflow_family_external_only_deferred"] = True
    else:
        reasons.append(
            "SR4/SR6 family-level workflow receipts are not passing: "
            + ", ".join(sorted(failing_family_receipts))
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
if failing_execution_receipts:
    all_failing_execution_receipts_external = (
        len(failing_execution_receipts_external) == len(failing_execution_receipts)
    )
    evidence["workflow_execution_failures_external_only"] = (
        all_failing_execution_receipts_external
    )
    if all_failing_execution_receipts_external:
        evidence["workflow_execution_external_only_deferred"] = True
    else:
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
):
    effective_status = str(evidence.get(f"{label}_effective_status") or evidence.get(f"{label}_status") or "")
    if not status_ok(effective_status):
        upstream_receipt_review_reasons.append(f"{label}:status")
    generated_at_raw = str(evidence.get(f"{label}_generated_at") or "").strip()
    if not generated_at_raw:
        upstream_receipt_review_reasons.append(f"{label}:generated_at")
    age_seconds_value = evidence.get(f"{label}_age_seconds")
    if isinstance(age_seconds_value, int) and age_seconds_value > DESKTOP_PROOF_MAX_AGE_SECONDS:
        allow_stale_pass = label == "ui_flagship_release_gate" and status_ok(
            str(evidence.get(f"{label}_status") or "")
        )
        if not allow_stale_pass:
            upstream_receipt_review_reasons.append(f"{label}:stale")
    future_skew_value = evidence.get(f"{label}_future_skew_seconds")
    if isinstance(future_skew_value, int) and future_skew_value > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
        upstream_receipt_review_reasons.append(f"{label}:future_skew")

release_channel_review_reasons: List[str] = []
if not release_channel_exists:
    release_channel_review_reasons.append("release_channel:missing")
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
    []
    if evidence.get("workflow_family_failures_external_only") is True
    else [f"failing_receipt:{entry}" for entry in sorted(failing_family_receipts)]
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
    []
    if evidence.get("workflow_execution_failures_external_only") is True
    else [f"failing_execution:{entry}" for entry in sorted(failing_execution_receipts)]
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
deferred_upstream_reasons: List[str] = []
deferred_workflow_family_reasons: List[str] = []
deferred_workflow_execution_reasons: List[str] = []
if direct_flagship_slice_runtime_proof_closes_direct_workflow_gate:
    upstream_receipt_review_reasons, deferred_upstream_reasons = filter_reason_prefixes(
        upstream_receipt_review_reasons,
        (
            "chummer5a_workflow_parity:",
            "sr4_workflow_parity:",
            "sr6_workflow_parity:",
            "sr4_sr6_frontier:",
            "ruleset_ui_adaptation:",
            "next90_m141_direct_import_route_proof:",
        ),
    )
    deferred_workflow_family_reasons = list(workflow_family_review_reasons)
    deferred_workflow_execution_reasons = list(workflow_execution_review_reasons)
    workflow_family_review_reasons = []
    workflow_execution_review_reasons = []
    reasons, deferred_reason_items = filter_reason_prefixes(
        reasons,
        (
            "desktop_visual_familiarity_gate dependency refresh failed via ",
            "chummer5a_workflow_parity dependency refresh failed via ",
            "sr4_workflow_parity dependency refresh failed via ",
            "sr6_workflow_parity dependency refresh failed via ",
            "sr4_sr6_frontier dependency refresh failed via ",
            "ruleset_ui_adaptation dependency refresh failed via ",
            "next90_m141_direct_import_route_proof dependency refresh failed via ",
            "chummer5a_workflow_parity receipt is missing or not passing.",
            "sr4_workflow_parity receipt is missing or not passing.",
            "sr6_workflow_parity receipt is missing or not passing.",
            "sr4_sr6_frontier receipt is stale",
            "sr4_sr6_frontier receipt is missing or not passing.",
            "ruleset_ui_adaptation receipt is missing or not passing.",
            "next90_m141_direct_import_route_proof receipt is missing or not passing.",
            "No SR4/SR6 family-level workflow receipts were discovered from ledgers.",
            "SR4/SR6 ledgers are missing required canonical workflow families:",
            "SR4/SR6 required canonical workflow families are not ready:",
            "SR4/SR6 required canonical workflow families are missing audit tests:",
            "Missing SR4/SR6 family-level workflow receipts:",
            "SR4/SR6 family-level workflow receipts resolve outside this repo root:",
            "SR4/SR6 family-level workflow receipts are not passing:",
            "No SR4/SR6 family-level execution receipts were discovered from ledgers.",
            "Missing SR4/SR6 family-level execution receipts:",
            "SR4/SR6 family-level execution receipts resolve outside this repo root:",
            "SR4/SR6 family-level execution receipts are not passing:",
            "SR4/SR6 family-level execution receipts are not explicitly grounded:",
            "Legacy workflow-family execution receipts still exist under deprecated path",
        ),
    )
    evidence["direct_flagship_slice_deferred_reason_items"] = deferred_reason_items
evidence["upstream_receipt_review_deferred_reasons"] = deferred_upstream_reasons
evidence["workflow_family_review_deferred_reasons"] = deferred_workflow_family_reasons
evidence["workflow_execution_review_deferred_reasons"] = deferred_workflow_execution_reasons
for reason in direct_flagship_slice_review_reasons:
    reasons.append(f"Direct flagship workflow proof is incomplete: {reason}.")

status = "pass" if not reasons else "fail"
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_workflow_execution_gate",
    "channelId": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "status": status,
    "summary": (
        "Desktop workflow execution gate is proven by passing Chummer5a/SR4/SR6 parity receipts and explicitly grounded family-level SR4/SR6 execution receipts."
        if status == "pass"
        else "Desktop workflow execution gate is not fully proven."
    ),
    "reasons": reasons,
    "reviews": {
        "upstreamReceiptReview": {
            "status": "pass" if not upstream_receipt_review_reasons else "fail",
            "reasons": upstream_receipt_review_reasons,
        },
        "releaseChannelReview": {
            "status": "pass" if not release_channel_review_reasons else "fail",
            "reasons": release_channel_review_reasons,
        },
        "flagshipHeadReview": {
            "status": "pass" if not flagship_head_review_reasons else "fail",
            "reasons": flagship_head_review_reasons,
        },
        "workflowFamilyReview": {
            "status": "pass" if not workflow_family_review_reasons else "fail",
            "reasons": workflow_family_review_reasons,
        },
        "workflowExecutionReview": {
            "status": "pass" if not workflow_execution_review_reasons else "fail",
            "reasons": workflow_execution_review_reasons,
        },
        "directFlagshipSliceReview": {
            "status": "pass" if not direct_flagship_slice_review_reasons else "fail",
            "reasons": direct_flagship_slice_review_reasons,
        },
    },
    "evidence": evidence,
}
payload["evidence"]["failureCount"] = len(reasons)
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    raise SystemExit(43)
PY

python3 "$flagship_product_readiness_materializer_path" >/dev/null

echo "[desktop-workflow-execution-gate] PASS"
