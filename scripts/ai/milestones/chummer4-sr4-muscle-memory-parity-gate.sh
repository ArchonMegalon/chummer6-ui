#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
cd "$repo_root"

receipt_path="${CHUMMER4_SR4_MUSCLE_MEMORY_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER4_SR4_MUSCLE_MEMORY_PARITY_GATE.generated.json}"
policy_path="${CHUMMER4_SR4_MUSCLE_MEMORY_PARITY_POLICY_PATH:-$repo_root/docs/CHUMMER4_SR4_MUSCLE_MEMORY_PARITY_POLICY.json}"
oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
design_doc_path="$repo_root/docs/CHUMMER4_SR4_MUSCLE_MEMORY_EXIT_TESTS.md"
sr4_workflow_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
inventory_receipt_path="${CHUMMER4_SR4_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER4_SR4_MUSCLE_MEMORY_INVENTORY.generated.json}"
dialog_factory_path="$repo_root/Chummer.Presentation/Overview/DesktopDialogFactory.cs"
desktop_dialog_window_path="$repo_root/Chummer.Avalonia/DesktopDialogWindow.axaml.cs"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
verify_script_path="$repo_root/scripts/ai/verify.sh"
# Guard marker: Runtime_backed_sr4_.
mkdir -p "$(dirname "$receipt_path")"

bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks" -m:1 -v minimal >/dev/null
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Runtime_loaded_runner_quick_action_workflows_materialize_dialog_contracts_and_continuations_across_sr4_sr5_and_sr6" -m:1 -v minimal >/dev/null

python3 - <<'PY' \
  "$receipt_path" \
  "$policy_path" \
  "$oracle_path" \
  "$design_doc_path" \
  "$sr4_workflow_receipt_path" \
  "$inventory_receipt_path" \
  "$dialog_factory_path" \
  "$desktop_dialog_window_path" \
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
    oracle_path,
    design_doc_path,
    sr4_workflow_receipt_path,
    inventory_receipt_path,
    dialog_factory_path,
    desktop_dialog_window_path,
    avalonia_gate_tests_path,
    verify_script_path,
) = [Path(value) for value in sys.argv[1:11]]


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
    "oracle": oracle_path,
    "designDoc": design_doc_path,
    "sr4WorkflowReceipt": sr4_workflow_receipt_path,
    "inventoryReceipt": inventory_receipt_path,
    "dialogFactory": dialog_factory_path,
    "desktopDialogWindow": desktop_dialog_window_path,
    "avaloniaGateTests": avalonia_gate_tests_path,
    "verifyScript": verify_script_path,
}

missing_paths = [name for name, path in required_paths.items() if not path.is_file()]
if missing_paths:
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer4_sr4_muscle_memory_parity_gate",
            "status": "fail",
            "summary": "Chummer4/SR4 muscle-memory gate inputs are incomplete.",
            "reasons": [f"Missing required path: {required_paths[name]}" for name in missing_paths],
            "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing_paths}},
        }
    )
    raise SystemExit(71)

policy = load_json(policy_path)
oracle = load_json(oracle_path)
design_doc_text = read_text(design_doc_path)
sr4_workflow_receipt = load_json(sr4_workflow_receipt_path)
inventory_receipt = load_json(inventory_receipt_path)
dialog_factory_text = read_text(dialog_factory_path)
desktop_dialog_window_text = read_text(desktop_dialog_window_path)
avalonia_gate_tests_text = read_text(avalonia_gate_tests_path)
verify_script_text = read_text(verify_script_path)
current_source_text = dialog_factory_text + "\n" + desktop_dialog_window_text

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer4_sr4_muscle_memory_parity_gate",
    "status": "fail",
    "summary": "Chummer4/SR4 muscle-memory parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "policyPath": str(policy_path),
        "oraclePath": str(oracle_path),
        "designDocPath": str(design_doc_path),
        "sr4WorkflowReceiptPath": str(sr4_workflow_receipt_path),
        "inventoryReceiptPath": str(inventory_receipt_path),
        "detailedDialogCheckCount": 0,
        "runtimeDialogSurfaceCount": 0,
        "runtimeVisibleElementCount": 0,
    },
    "reviews": {},
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]
policy_reasons: list[str] = []
workflow_reasons: list[str] = []
detailed_dialog_reasons: list[str] = []
runtime_reasons: list[str] = []
inventory_reasons: list[str] = []
wiring_reasons: list[str] = []


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


if policy.get("contractName") != "chummer6-ui.chummer4_sr4_muscle_memory_parity_policy":
    add_failure("Chummer4/SR4 policy contractName is missing or incorrect.", policy_reasons)
if policy.get("scopeStrategy") != "promoted_sr4_surfaces_with_chummer4_dialog_oracles":
    add_failure("Chummer4/SR4 policy must target the promoted SR4 surfaces plus Chummer4 dialog oracles.", policy_reasons)
if policy.get("sr4WorkflowParityContract") != "chummer6-ui.sr4_desktop_workflow_parity":
    add_failure("Chummer4/SR4 policy must pin the SR4 workflow parity contract.", policy_reasons)
if policy.get("runtimeInventoryContract") != "chummer6-ui.chummer4_sr4_muscle_memory_inventory":
    add_failure("Chummer4/SR4 policy must pin the runtime SR4 muscle-memory inventory contract.", policy_reasons)
full_scope = policy.get("fullScope") or {}
for key in ("promotedDialogsAndPanelsInScope", "menusInScope", "tooltipsInScope", "sharedBaselineComparisonInScope", "secondaryPointerHostTruthInScope"):
    if full_scope.get(key) is not True:
        add_failure(f"Chummer4/SR4 policy must keep fullScope.{key}=true.", policy_reasons)
if str(oracle.get("scope") or "").strip() != "sr4_desktop_head":
    add_failure("Chummer4/SR4 parity oracle scope must stay sr4_desktop_head.", policy_reasons)

if not status_ok(sr4_workflow_receipt.get("status")):
    add_failure("SR4 desktop workflow parity receipt is missing or not passing.", workflow_reasons)
if str(sr4_workflow_receipt.get("contract_name") or sr4_workflow_receipt.get("contractName") or "").strip() != "chummer6-ui.sr4_desktop_workflow_parity":
    add_failure("SR4 desktop workflow parity receipt contract name is missing or incorrect.", workflow_reasons)

if str(inventory_receipt.get("contractName") or "").strip() != "chummer6-ui.chummer4_sr4_muscle_memory_inventory":
    add_failure("SR4 runtime inventory receipt contract name is missing or incorrect.", inventory_reasons)
if not status_ok(inventory_receipt.get("status")):
    add_failure("SR4 runtime inventory receipt is missing or not passing.", inventory_reasons)
inventory_evidence = inventory_receipt.get("evidence") or {}
if not isinstance(inventory_evidence, dict):
    add_failure("SR4 runtime inventory evidence is missing.", inventory_reasons)
    inventory_evidence = {}
evidence["runtimeDialogSurfaceCount"] = int(inventory_evidence.get("dialogSurfaceCount") or 0)
evidence["runtimeVisibleElementCount"] = int(inventory_evidence.get("totalVisibleElementCount") or 0)
if evidence["runtimeDialogSurfaceCount"] <= 0:
    add_failure("SR4 runtime inventory did not capture dialog surfaces.", inventory_reasons)
if evidence["runtimeVisibleElementCount"] <= 0:
    add_failure("SR4 runtime inventory captured zero visible UI elements.", inventory_reasons)
inventory_reviews = inventory_receipt.get("reviews") or {}
if not isinstance(inventory_reviews, dict):
    add_failure("SR4 runtime inventory reviews are missing.", inventory_reasons)
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
            add_failure(f"SR4 runtime inventory review failed: {review_key}", inventory_reasons)

for marker in policy.get("requiredRuntimeTestMarkers") or []:
    if marker not in avalonia_gate_tests_text:
        add_failure(f"Missing runtime SR4 marker: {marker}", runtime_reasons)

detailed_dialog_checks = policy.get("detailedDialogChecks") or []
evidence["detailedDialogCheckCount"] = len(detailed_dialog_checks)
if not isinstance(detailed_dialog_checks, list) or len(detailed_dialog_checks) == 0:
    add_failure("Chummer4/SR4 policy must seed at least one strict dialog check.", detailed_dialog_reasons)
else:
    detailed_results: list[dict[str, Any]] = []
    for check in detailed_dialog_checks:
        dialog_id = str(check.get("dialogId") or "").strip()
        legacy_designer_path = Path(str(check.get("legacyDesignerPath") or "").strip())
        check_reasons: list[str] = []
        legacy_text = ""
        if not dialog_id:
            check_reasons.append("dialogId is blank.")
        if not legacy_designer_path.is_file():
            check_reasons.append(f"legacy designer path is missing: {legacy_designer_path}")
        else:
            legacy_text = read_text(legacy_designer_path)
        for pattern in check.get("legacyRequiredPatterns") or []:
            if legacy_text and pattern not in legacy_text:
                check_reasons.append(f"legacy pattern missing: {pattern}")
        for pattern in check.get("currentRequiredPatterns") or []:
            if pattern not in current_source_text:
                check_reasons.append(f"current source pattern missing: {pattern}")
        for marker in check.get("requiredTestMarkers") or []:
            if marker not in avalonia_gate_tests_text:
                check_reasons.append(f"required runtime marker missing: {marker}")
        if check_reasons:
            add_failure(
                f"Chummer4/SR4 detailed dialog parity failed for '{dialog_id}': " + "; ".join(check_reasons),
                detailed_dialog_reasons,
            )
        detailed_results.append(
            {
                "dialogId": dialog_id,
                "legacyDesignerPath": str(legacy_designer_path),
                "status": "pass" if not check_reasons else "fail",
                "reasonCount": len(check_reasons),
                "reasons": check_reasons,
            }
        )
    evidence["detailedDialogResults"] = detailed_results

for marker in [
    "promoted SR4 surfaces",
    "Chummer4 as the legacy oracle",
    "switch-ruleset chooser",
    "SR4 starter-runner follow-through",
    "full promoted SR4 dialog/panel/menu/tooltip surface inventory",
    "within-slot field order",
    "right-click secondary-menu posture",
    "zero hosts",
    "spinner posture",
    "two-pane geography",
]:
    if marker not in design_doc_text:
        add_failure(f"Chummer4/SR4 design doc is missing marker: {marker}", policy_reasons)

for marker in [
    "checking Chummer4/SR4 muscle-memory parity gate",
    "bash scripts/ai/milestones/chummer4-sr4-muscle-memory-parity-gate.sh",
]:
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing Chummer4/SR4 gate wiring marker: {marker}", wiring_reasons)

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
    "detailedDialogReview": {
        "status": "pass" if not detailed_dialog_reasons else "fail",
        "reasonCount": len(detailed_dialog_reasons),
        "reasons": detailed_dialog_reasons,
    },
    "runtimeReview": {
        "status": "pass" if not runtime_reasons else "fail",
        "reasonCount": len(runtime_reasons),
        "reasons": runtime_reasons,
    },
    "inventoryReview": {
        "status": "pass" if not inventory_reasons else "fail",
        "reasonCount": len(inventory_reasons),
        "reasons": inventory_reasons,
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
    "Chummer4/SR4 muscle-memory policy, workflow parity receipt, runtime inventory, dialog seeds, runtime markers, and verify wiring are present."
    if not reasons
    else "Chummer4/SR4 muscle-memory parity proof is incomplete."
)
write_receipt(payload)
raise SystemExit(0 if not reasons else 1)
PY
