#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_SR6_SHARED_MUSCLE_MEMORY_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_PARITY_GATE.generated.json}"
policy_path="${CHUMMER_SR6_SHARED_MUSCLE_MEMORY_POLICY_PATH:-$repo_root/docs/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_POLICY.json}"
design_doc_path="$repo_root/docs/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_EXIT_TESTS.md"
sr6_workflow_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
inventory_receipt_path="${CHUMMER_SR6_SHARED_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_INVENTORY.generated.json}"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
verify_script_path="$repo_root/scripts/ai/verify.sh"
mkdir -p "$(dirname "$receipt_path")"

bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_roster_landmarks" -m:1 -v minimal >/dev/null
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces" -m:1 -v minimal >/dev/null

python3 - <<'PY' \
  "$receipt_path" \
  "$policy_path" \
  "$design_doc_path" \
  "$sr6_workflow_receipt_path" \
  "$inventory_receipt_path" \
  "$avalonia_gate_tests_path" \
  "$verify_script_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

(
    receipt_path,
    policy_path,
    design_doc_path,
    sr6_workflow_receipt_path,
    inventory_receipt_path,
    avalonia_gate_tests_path,
    verify_script_path,
) = [Path(value) for value in sys.argv[1:8]]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return payload


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


required_paths = {
    "policy": policy_path,
    "designDoc": design_doc_path,
    "sr6WorkflowReceipt": sr6_workflow_receipt_path,
    "inventoryReceipt": inventory_receipt_path,
    "avaloniaGateTests": avalonia_gate_tests_path,
    "verifyScript": verify_script_path,
}

missing_paths = [name for name, path in required_paths.items() if not path.is_file()]
if missing_paths:
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer_sr6_shared_muscle_memory_parity_gate",
            "status": "fail",
            "summary": "SR6 shared muscle-memory gate inputs are incomplete.",
            "reasons": [f"Missing required path: {required_paths[name]}" for name in missing_paths],
            "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing_paths}},
        }
    )
    raise SystemExit(71)

policy = load_json(policy_path)
design_doc_text = read_text(design_doc_path)
sr6_workflow_receipt = load_json(sr6_workflow_receipt_path)
inventory_receipt = load_json(inventory_receipt_path)
avalonia_gate_tests_text = read_text(avalonia_gate_tests_path)
verify_script_text = read_text(verify_script_path)

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer_sr6_shared_muscle_memory_parity_gate",
    "status": "fail",
    "summary": "SR6 shared muscle-memory parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "policyPath": str(policy_path),
        "designDocPath": str(design_doc_path),
        "sr6WorkflowReceiptPath": str(sr6_workflow_receipt_path),
        "inventoryReceiptPath": str(inventory_receipt_path),
        "runtimeDialogSurfaceCount": 0,
        "runtimeVisibleElementCount": 0,
    },
    "reviews": {},
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]
policy_reasons: list[str] = []
workflow_reasons: list[str] = []
inventory_reasons: list[str] = []
runtime_reasons: list[str] = []
wiring_reasons: list[str] = []


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


if policy.get("contractName") != "chummer6-ui.chummer_sr6_shared_muscle_memory_policy":
    add_failure("SR6 shared muscle-memory policy contractName is missing or incorrect.", policy_reasons)
if policy.get("scopeStrategy") != "shared_promoted_desktop_posture_against_chummer5a_baseline":
    add_failure("SR6 shared muscle-memory policy must target the promoted desktop baseline posture.", policy_reasons)
if policy.get("sr6WorkflowParityContract") != "chummer6-ui.sr6_desktop_workflow_parity":
    add_failure("SR6 shared muscle-memory policy must pin the SR6 workflow parity contract.", policy_reasons)
if policy.get("runtimeInventoryContract") != "chummer6-ui.sr6_shared_muscle_memory_inventory":
    add_failure("SR6 shared muscle-memory policy must pin the SR6 runtime inventory contract.", policy_reasons)
full_scope = policy.get("fullScope") or {}
for key in ("sharedShellSurfacesInScope", "sharedDialogsAndPanelsInScope", "menusInScope", "tooltipsInScope", "sharedBaselineComparisonInScope", "dialogLabelReviewInScope", "secondaryPointerHostTruthInScope"):
    if full_scope.get(key) is not True:
        add_failure(f"SR6 shared muscle-memory policy must keep fullScope.{key}=true.", policy_reasons)

if not status_ok(sr6_workflow_receipt.get("status")):
    add_failure("SR6 desktop workflow parity receipt is missing or not passing.", workflow_reasons)
if str(sr6_workflow_receipt.get("contract_name") or sr6_workflow_receipt.get("contractName") or "").strip() != "chummer6-ui.sr6_desktop_workflow_parity":
    add_failure("SR6 desktop workflow parity receipt contract name is missing or incorrect.", workflow_reasons)

if str(inventory_receipt.get("contractName") or "").strip() != "chummer6-ui.sr6_shared_muscle_memory_inventory":
    add_failure("SR6 shared runtime inventory receipt contract name is missing or incorrect.", inventory_reasons)
if not status_ok(inventory_receipt.get("status")):
    add_failure("SR6 shared runtime inventory receipt is missing or not passing.", inventory_reasons)
inventory_evidence = inventory_receipt.get("evidence") or {}
if not isinstance(inventory_evidence, dict):
    add_failure("SR6 shared runtime inventory evidence is missing.", inventory_reasons)
    inventory_evidence = {}
evidence["runtimeDialogSurfaceCount"] = int(inventory_evidence.get("dialogSurfaceCount") or 0)
evidence["runtimeVisibleElementCount"] = int(inventory_evidence.get("totalVisibleElementCount") or 0)
if evidence["runtimeDialogSurfaceCount"] <= 0:
    add_failure("SR6 shared runtime inventory did not capture dialog surfaces.", inventory_reasons)
if evidence["runtimeVisibleElementCount"] <= 0:
    add_failure("SR6 shared runtime inventory captured zero visible UI elements.", inventory_reasons)

inventory_reviews = inventory_receipt.get("reviews") or {}
if not isinstance(inventory_reviews, dict):
    add_failure("SR6 shared runtime inventory reviews are missing.", inventory_reasons)
    inventory_reviews = {}
else:
    for review_key in (
        "surfaceCoverageReview",
        "dialogWidgetClassReview",
        "dialogLabelReview",
        "dialogLayoutSlotReview",
        "dialogFieldOrderReview",
        "dialogActionOrderReview",
        "pointerRouteReview",
        "auxiliaryPointerRouteReview",
        "auxiliaryPointerHostTruthReview",
        "tooltipCoverageReview",
        "dialogGeometryReview",
        "sharedBaselineParityReview",
    ):
        review = inventory_reviews.get(review_key) or {}
        if not status_ok(review.get("status")):
            add_failure(f"SR6 shared runtime inventory review failed: {review_key}", inventory_reasons)

for marker in policy.get("requiredRuntimeTestMarkers") or []:
    if marker not in avalonia_gate_tests_text:
        add_failure(f"Missing runtime SR6 marker: {marker}", runtime_reasons)

for marker in [
    "promoted desktop baseline",
    "SR6 workflow parity receipt",
    "runtime inventory receipt",
    "shared shell, workspace, dialog, and action routes",
    "zero hosts",
]:
    if marker not in design_doc_text:
        add_failure(f"SR6 shared muscle-memory design doc is missing marker: {marker}", policy_reasons)

for marker in [
    "checking SR6 shared muscle-memory parity gate",
    "bash scripts/ai/milestones/sr6-shared-muscle-memory-gate.sh",
]:
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing SR6 shared muscle-memory gate wiring marker: {marker}", wiring_reasons)

payload["reviews"] = {
    "policyReview": {
        "status": "pass" if not policy_reasons else "fail",
        "reasonCount": len(policy_reasons),
        "reasons": policy_reasons,
    },
    "workflowParityReview": {
        "status": "pass" if not workflow_reasons else "fail",
        "reasonCount": len(workflow_reasons),
        "reasons": workflow_reasons,
    },
    "inventoryReview": {
        "status": "pass" if not inventory_reasons else "fail",
        "reasonCount": len(inventory_reasons),
        "reasons": inventory_reasons,
    },
    "runtimeReview": {
        "status": "pass" if not runtime_reasons else "fail",
        "reasonCount": len(runtime_reasons),
        "reasons": runtime_reasons,
    },
    "wiringReview": {
        "status": "pass" if not wiring_reasons else "fail",
        "reasonCount": len(wiring_reasons),
        "reasons": wiring_reasons,
    },
}
evidence["reasonCount"] = len(reasons)
evidence["failureCount"] = len(reasons)
payload["status"] = "pass" if not reasons else "fail"
payload["summary"] = (
    "SR6 shared muscle-memory policy, workflow parity receipt, runtime inventory, runtime markers, and verify wiring are present."
    if not reasons
    else "SR6 shared muscle-memory parity proof is incomplete."
)
write_receipt(payload)
raise SystemExit(0 if not reasons else 1)
PY
