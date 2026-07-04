#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_SR6_RULESET_UI_SOPHISTICATION_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json}"
policy_path="${CHUMMER_SR6_RULESET_UI_SOPHISTICATION_POLICY_PATH:-$repo_root/docs/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_POLICY.json}"
design_doc_path="$repo_root/docs/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_EXIT_TESTS.md"
ruleset_adaptation_receipt_path="${CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH:-$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json}"
sr6_workflow_receipt_path="${CHUMMER_SR6_WORKFLOW_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json}"
sr6_shared_muscle_receipt_path="${CHUMMER_SR6_SHARED_MUSCLE_MEMORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_PARITY_GATE.generated.json}"
interactive_inventory_receipt_path="${CHUMMER_INTERACTIVE_CONTROL_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json}"
runtime_route_inventory_receipt_path="${CHUMMER_INTERACTIVE_RUNTIME_ROUTE_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/INTERACTIVE_RUNTIME_ROUTE_INVENTORY.generated.json}"
directive_catalog_path="$repo_root/Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs"
ruleset_tests_path="$repo_root/Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs"
desktop_shell_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
verify_script_path="$repo_root/scripts/ai/verify.sh"
b14_script_path="$repo_root/scripts/ai/milestones/b14-flagship-ui-release-gate.sh"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' \
  "$receipt_path" \
  "$policy_path" \
  "$design_doc_path" \
  "$ruleset_adaptation_receipt_path" \
  "$sr6_workflow_receipt_path" \
  "$sr6_shared_muscle_receipt_path" \
  "$interactive_inventory_receipt_path" \
  "$runtime_route_inventory_receipt_path" \
  "$directive_catalog_path" \
  "$ruleset_tests_path" \
  "$desktop_shell_tests_path" \
  "$avalonia_gate_tests_path" \
  "$verify_script_path" \
  "$b14_script_path"
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
    ruleset_adaptation_receipt_path,
    sr6_workflow_receipt_path,
    sr6_shared_muscle_receipt_path,
    interactive_inventory_receipt_path,
    runtime_route_inventory_receipt_path,
    directive_catalog_path,
    ruleset_tests_path,
    desktop_shell_tests_path,
    avalonia_gate_tests_path,
    verify_script_path,
    b14_script_path,
) = [Path(value) for value in sys.argv[1:15]]


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
    "rulesetAdaptationReceipt": ruleset_adaptation_receipt_path,
    "sr6WorkflowReceipt": sr6_workflow_receipt_path,
    "sr6SharedMuscleReceipt": sr6_shared_muscle_receipt_path,
    "interactiveInventoryReceipt": interactive_inventory_receipt_path,
    "runtimeRouteInventoryReceipt": runtime_route_inventory_receipt_path,
    "directiveCatalog": directive_catalog_path,
    "rulesetTests": ruleset_tests_path,
    "desktopShellTests": desktop_shell_tests_path,
    "avaloniaGateTests": avalonia_gate_tests_path,
    "verifyScript": verify_script_path,
    "b14Script": b14_script_path,
}

missing_paths = [name for name, path in required_paths.items() if not path.is_file()]
if missing_paths:
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_gate",
            "status": "fail",
            "summary": "SR6 ruleset UI sophistication inputs are incomplete.",
            "reasons": [f"Missing required path: {required_paths[name]}" for name in missing_paths],
            "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing_paths}},
        }
    )
    raise SystemExit(71)

policy = load_json(policy_path)
ruleset_adaptation_receipt = load_json(ruleset_adaptation_receipt_path)
sr6_workflow_receipt = load_json(sr6_workflow_receipt_path)
sr6_shared_muscle_receipt = load_json(sr6_shared_muscle_receipt_path)
interactive_inventory_receipt = load_json(interactive_inventory_receipt_path)
runtime_route_inventory_receipt = load_json(runtime_route_inventory_receipt_path)
design_doc_text = read_text(design_doc_path)
directive_catalog_text = read_text(directive_catalog_path)
ruleset_tests_text = read_text(ruleset_tests_path)
desktop_shell_tests_text = read_text(desktop_shell_tests_path)
avalonia_gate_tests_text = read_text(avalonia_gate_tests_path)
verify_script_text = read_text(verify_script_path)
b14_script_text = read_text(b14_script_path)

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_gate",
    "status": "fail",
    "summary": "SR6 ruleset-specific UI sophistication parity is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "policyPath": str(policy_path),
        "designDocPath": str(design_doc_path),
        "rulesetAdaptationReceiptPath": str(ruleset_adaptation_receipt_path),
        "sr6WorkflowReceiptPath": str(sr6_workflow_receipt_path),
        "sr6SharedMuscleReceiptPath": str(sr6_shared_muscle_receipt_path),
        "interactiveInventoryReceiptPath": str(interactive_inventory_receipt_path),
        "runtimeRouteInventoryReceiptPath": str(runtime_route_inventory_receipt_path),
        "runtimeRouteIdCount": 0,
        "runtimeRouteFamilyCount": 0,
        "runtimeRulesetLaneCount": 0,
    },
    "reviews": {},
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]
policy_reasons: list[str] = []
receipt_reasons: list[str] = []
runtime_reasons: list[str] = []
source_reasons: list[str] = []
wiring_reasons: list[str] = []


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


if policy.get("contractName") != "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_policy":
    add_failure("SR6 sophistication policy contractName is missing or incorrect.", policy_reasons)
if policy.get("scopeStrategy") != "sr6_ruleset_specific_surface_depth_must_match_sr5_editor_grade_richness":
    add_failure("SR6 sophistication policy scopeStrategy is missing or incorrect.", policy_reasons)
if policy.get("sr5ComparisonContract") != "chummer6-ui.ruleset_ui_adaptation_frontier":
    add_failure("SR6 sophistication policy must pin the ruleset UI adaptation contract.", policy_reasons)
if policy.get("sr6WorkflowParityContract") != "chummer6-ui.sr6_desktop_workflow_parity":
    add_failure("SR6 sophistication policy must pin the SR6 workflow parity contract.", policy_reasons)
if policy.get("sr6SharedMuscleMemoryContract") != "chummer6-ui.chummer_sr6_shared_muscle_memory_parity_gate":
    add_failure("SR6 sophistication policy must pin the SR6 shared muscle-memory gate.", policy_reasons)
if policy.get("interactiveInventoryContract") != "chummer6-ui.interactive_control_inventory":
    add_failure("SR6 sophistication policy must pin the interactive inventory contract.", policy_reasons)
if policy.get("runtimeRouteInventoryContract") != "chummer6-ui.interactive_runtime_route_inventory":
    add_failure("SR6 sophistication policy must pin the runtime route inventory contract.", policy_reasons)

full_scope = policy.get("fullScope") or {}
for key in (
    "sharedShellSurfaceDepthParityInScope",
    "rulesetAuthoredLabelsInScope",
    "rulesetSpecificCommandsInScope",
    "rulesetSpecificDialogsAndPanelsInScope",
    "rulesetSpecificWorkflowSurfacesInScope",
    "widgetClassParityInScope",
    "eventParityInScope",
    "keyboardRouteParityInScope",
    "tooltipAndSecondaryRouteParityInScope",
    "runtimeBranchParityInScope",
    "zeroFallbackHostsInScope",
    "noThinSharedShellSubstituteInScope",
    "equalUiSophisticationAgainstSr5InScope",
):
    if full_scope.get(key) is not True:
        add_failure(f"SR6 sophistication policy must keep fullScope.{key}=true.", policy_reasons)

for receipt, label, expected_contract in (
    (ruleset_adaptation_receipt, "ruleset adaptation receipt", "chummer6-ui.ruleset_ui_adaptation_frontier"),
    (sr6_workflow_receipt, "SR6 workflow receipt", "chummer6-ui.sr6_desktop_workflow_parity"),
    (sr6_shared_muscle_receipt, "SR6 shared muscle-memory receipt", "chummer6-ui.chummer_sr6_shared_muscle_memory_parity_gate"),
    (interactive_inventory_receipt, "interactive inventory receipt", "chummer6-ui.interactive_control_inventory"),
    (runtime_route_inventory_receipt, "runtime route inventory receipt", "chummer6-ui.interactive_runtime_route_inventory"),
):
    if not status_ok(receipt.get("status")):
        add_failure(f"{label} is missing or not passing.", receipt_reasons)
    contract_name = str(receipt.get("contract_name") or receipt.get("contractName") or "").strip()
    if contract_name != expected_contract:
        add_failure(f"{label} contract name is missing or incorrect.", receipt_reasons)

runtime_evidence = runtime_route_inventory_receipt.get("evidence") or {}
route_ids = [str(value).strip() for value in (runtime_evidence.get("routeIds") or []) if str(value).strip()]
route_families = [str(value).strip() for value in (runtime_evidence.get("routeFamilies") or []) if str(value).strip()]
ruleset_lanes = [str(value).strip() for value in (runtime_evidence.get("rulesetLanes") or []) if str(value).strip()]
if not route_ids:
    route_ids = [
        str(route.get("routeId") or "").strip()
        for route in (runtime_route_inventory_receipt.get("routes") or [])
        if str(route.get("routeId") or "").strip()
    ]
if not route_families:
    route_families = [str(value).strip() for value in (runtime_route_inventory_receipt.get("routeFamilies") or []) if str(value).strip()]
if not ruleset_lanes:
    ruleset_lanes = [str(value).strip() for value in (runtime_route_inventory_receipt.get("rulesetLanes") or []) if str(value).strip()]
evidence["runtimeRouteIdCount"] = len(route_ids)
evidence["runtimeRouteFamilyCount"] = len(route_families)
evidence["runtimeRulesetLaneCount"] = len(ruleset_lanes)
for expected_route_id in (
    "ruleset-sr4-codex-tree",
    "ruleset-sr5-codex-tree",
    "ruleset-sr6-codex-tree",
    "dialog-priority-workflow-priority",
    "dialog-priority-workflow-sum-to-ten",
    "section-attributes-editor",
):
    if expected_route_id not in route_ids:
        add_failure(f"Runtime route inventory is missing required route: {expected_route_id}", runtime_reasons)
for expected_family in ("shell", "popup", "dialog", "section", "ruleset"):
    if expected_family not in route_families:
        add_failure(f"Runtime route inventory is missing required route family: {expected_family}", runtime_reasons)
for expected_lane in ("sr4", "sr5", "sr6"):
    if expected_lane not in ruleset_lanes:
        add_failure(f"Runtime route inventory is missing required ruleset lane: {expected_lane}", runtime_reasons)

inventory_text = json.dumps(interactive_inventory_receipt, ensure_ascii=False)
for expected_text in (
    "\"ruleset-sr5-codex-tree\"",
    "\"ruleset-sr6-codex-tree\"",
    "\"Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks\": true",
):
    if expected_text not in inventory_text:
        add_failure(f"Interactive inventory receipt is missing required SR6/SR5 parity evidence: {expected_text}", runtime_reasons)

for marker in policy.get("requiredSourceMarkers") or []:
    if marker not in directive_catalog_text and marker not in ruleset_tests_text and marker not in desktop_shell_tests_text:
        add_failure(f"SR6 sophistication source marker is missing: {marker}", source_reasons)

for marker in (
    "Desktop Summary · SR6 Editor",
    "SR6 Editor Tabs",
    "SR6 Editor Actions",
    "SR6 Editor Flows",
    "No SR6 editor commands are currently available.",
    "SR6 Matrix Action",
    "DesktopShell_renders_ruleset_specific_flagship_posture_for_each_supported_lane",
    "DesktopShell_uses_active_ruleset_plugin_catalogs_for_actions_and_workflow_surfaces",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches",
):
    if (
        marker not in directive_catalog_text
        and marker not in ruleset_tests_text
        and marker not in desktop_shell_tests_text
        and marker not in avalonia_gate_tests_text
    ):
        add_failure(f"SR6 sophistication runtime/source marker is missing: {marker}", source_reasons)

for marker in (
    "equal UI sophistication against SR5",
    "thin shared-shell substitute",
    "zero hosts",
    "ruleset-specific action and workflow labels",
    "promoted SR4, SR5, and SR6 lanes",
):
    if marker not in design_doc_text:
        add_failure(f"SR6 sophistication design doc is missing marker: {marker}", policy_reasons)

for marker in (
    "checking SR6 ruleset UI sophistication gate",
    "bash scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh",
):
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing SR6 sophistication gate wiring marker: {marker}", wiring_reasons)

for marker in (
    "sr6_ruleset_ui_sophistication_receipt_path",
    "explicitSr6RulesetSophisticationReceiptPath",
):
    if marker not in b14_script_text:
        add_failure(f"B14 flagship UI gate is missing SR6 sophistication marker: {marker}", wiring_reasons)

payload["reviews"] = {
    "policyReview": {
        "status": "pass" if not policy_reasons else "fail",
        "reasonCount": len(policy_reasons),
        "reasons": policy_reasons,
    },
    "receiptReview": {
        "status": "pass" if not receipt_reasons else "fail",
        "reasonCount": len(receipt_reasons),
        "reasons": receipt_reasons,
    },
    "runtimeReview": {
        "status": "pass" if not runtime_reasons else "fail",
        "reasonCount": len(runtime_reasons),
        "reasons": runtime_reasons,
    },
    "sourceReview": {
        "status": "pass" if not source_reasons else "fail",
        "reasonCount": len(source_reasons),
        "reasons": source_reasons,
    },
    "wiringReview": {
        "status": "pass" if not wiring_reasons else "fail",
        "reasonCount": len(wiring_reasons),
        "reasons": wiring_reasons,
    },
  "equalSophisticationReview": {
        "status": "pass" if not reasons else "fail",
        "reasonCount": len(reasons),
        "reasons": reasons,
    },
}
evidence["reasonCount"] = len(reasons)
evidence["failureCount"] = len(reasons)
payload["status"] = "pass" if not reasons else "fail"
payload["summary"] = (
    "SR6 authored ruleset UI sophistication is pinned against SR5 richness and backed by runtime inventory, workflow parity, and shared muscle-memory proof."
    if not reasons
    else "SR6 authored ruleset UI sophistication is not yet proven equal to the SR5 lane."
)
write_receipt(payload)
raise SystemExit(0 if not reasons else 1)
PY
