#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER5A_MUSCLE_MEMORY_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE.generated.json}"
policy_path="${CHUMMER5A_MUSCLE_MEMORY_PARITY_POLICY_PATH:-$repo_root/docs/CHUMMER5A_MUSCLE_MEMORY_PARITY_POLICY.json}"
parity_oracle_path="$repo_root/docs/PARITY_ORACLE.json"
design_doc_path="$repo_root/docs/CHUMMER5A_MUSCLE_MEMORY_EXIT_TESTS.md"
dialog_factory_path="$repo_root/Chummer.Presentation/Overview/DesktopDialogFactory.cs"
dialog_factory_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
verify_script_path="$repo_root/scripts/ai/verify.sh"
visual_gate_path="$repo_root/scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh"
screenshot_review_gate_path="$repo_root/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh"
inventory_receipt_path="${CHUMMER5A_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_MUSCLE_MEMORY_INVENTORY.generated.json}"
local_screenshot_comparison_receipt_path="${CHUMMER5A_LOCAL_SCREENSHOT_COMPARISON_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_LOCAL_SCREENSHOT_COMPARISON_GATE.generated.json}"
# Guard markers: Runtime_backed_chummer5a_muscle_memory_inventory, Runtime_backed_mouse_only_.
mkdir -p "$(dirname "$receipt_path")"

bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces" -m:1 -v minimal >/dev/null
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Master_index_is_a_first_class_runtime_backed_workbench_route" -m:1 -v minimal >/dev/null
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj --filter "Name=Character_roster_is_a_first_class_runtime_backed_workbench_route" -m:1 -v minimal >/dev/null
CHUMMER5A_SCREENSHOT_COMPARISON_SCOPE=local_only \
CHUMMER5A_SCREENSHOT_COMPARISON_RECEIPT_PATH="$local_screenshot_comparison_receipt_path" \
python3 scripts/verify_pixefy_chummer5a_screenshot_comparison.py >/dev/null || true

python3 - <<'PY' \
  "$receipt_path" \
  "$policy_path" \
  "$parity_oracle_path" \
  "$design_doc_path" \
  "$dialog_factory_path" \
  "$dialog_factory_tests_path" \
  "$avalonia_gate_tests_path" \
  "$verify_script_path" \
  "$visual_gate_path" \
  "$screenshot_review_gate_path" \
  "$inventory_receipt_path" \
  "$local_screenshot_comparison_receipt_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


(
    receipt_path,
    policy_path,
    parity_oracle_path,
    design_doc_path,
    dialog_factory_path,
    dialog_factory_tests_path,
    avalonia_gate_tests_path,
    verify_script_path,
    visual_gate_path,
    screenshot_review_gate_path,
    inventory_receipt_path,
    local_screenshot_comparison_receipt_path
) = [Path(value) for value in sys.argv[1:13]]
REPO_ROOT = Path.cwd()


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


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def status_is_pass(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


required_paths = {
    "policy": policy_path,
    "parityOracle": parity_oracle_path,
    "designDoc": design_doc_path,
    "dialogFactory": dialog_factory_path,
    "dialogFactoryTests": dialog_factory_tests_path,
    "avaloniaGateTests": avalonia_gate_tests_path,
    "verifyScript": verify_script_path,
    "visualGate": visual_gate_path,
    "screenshotReviewGate": screenshot_review_gate_path,
    "inventoryReceipt": inventory_receipt_path,
    "localScreenshotComparisonReceipt": local_screenshot_comparison_receipt_path
}
missing_paths = [name for name, path in required_paths.items() if not path.is_file()]
if missing_paths:
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer5a_muscle_memory_parity_gate",
            "status": "fail",
            "summary": "Muscle-memory parity gate inputs are incomplete.",
            "reasons": [f"Missing required path: {required_paths[name]}" for name in missing_paths],
            "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing_paths}}
        }
    )
    raise SystemExit(71)

policy = load_json(policy_path)
oracle = load_json(parity_oracle_path)
design_doc_text = read_text(design_doc_path)
dialog_factory_text = read_text(dialog_factory_path)
dialog_factory_tests_text = read_text(dialog_factory_tests_path)
avalonia_gate_tests_text = read_text(avalonia_gate_tests_path)
verify_script_text = read_text(verify_script_path)
visual_gate_text = read_text(visual_gate_path)
screenshot_review_gate_text = read_text(screenshot_review_gate_path)
inventory_receipt = load_json(inventory_receipt_path)
local_screenshot_comparison_receipt = load_json(local_screenshot_comparison_receipt_path)

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer5a_muscle_memory_parity_gate",
    "status": "fail",
    "summary": "Chummer5A muscle-memory parity proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "policyPath": str(policy_path),
        "parityOraclePath": str(parity_oracle_path),
        "designDocPath": str(design_doc_path),
        "inventoryReceiptPath": str(inventory_receipt_path),
        "localScreenshotComparisonReceiptPath": str(local_screenshot_comparison_receipt_path),
        "workspaceActionCount": 0,
        "desktopControlCount": 0,
        "tabCount": 0,
        "detailedDialogCheckCount": 0,
        "runtimeShellSurfaceCount": 0,
        "runtimeMenuRootCount": 0,
        "runtimeDialogSurfaceCount": 0,
        "runtimeVisibleElementCount": 0,
        "localScreenshotComparisonScreenshotCount": int(local_screenshot_comparison_receipt.get("screenshot_count") or 0),
        "localScreenshotComparisonRequiredScreenshotCount": len(local_screenshot_comparison_receipt.get("required_screenshots") or [])
    },
    "reviews": {}
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]
scope_inventory_reasons: list[str] = []
dialog_widget_reasons: list[str] = []
design_reasons: list[str] = []
wiring_reasons: list[str] = []
inventory_runtime_reasons: list[str] = []

tabs = oracle.get("tabs") or []
workspace_actions = oracle.get("workspaceActions") or []
desktop_controls = oracle.get("desktopControls") or []
acknowledged_factory_only = oracle.get("acknowledgedDialogFactoryOnlyDesktopControls") or []

evidence["tabCount"] = len(tabs)
evidence["workspaceActionCount"] = len(workspace_actions)
evidence["desktopControlCount"] = len(desktop_controls) + len(acknowledged_factory_only)

if policy.get("contractName") != "chummer6-ui.chummer5a_muscle_memory_parity_policy":
    add_failure("Muscle-memory parity policy contractName is missing or incorrect.", scope_inventory_reasons)
if policy.get("scopeStrategy") != "all_oracle_tabs_workspace_actions_and_desktop_controls":
    add_failure("Muscle-memory parity policy must scope every oracle tab, workspace action, and desktop control.", scope_inventory_reasons)
if policy.get("runtimeInventoryContract") != "chummer6-ui.chummer5a_muscle_memory_inventory":
    add_failure("Muscle-memory parity policy must pin the runtime inventory contract.", scope_inventory_reasons)

full_scope = policy.get("fullScope") or {}
for key in ("tabsFromOracle", "workspaceActionsFromOracle", "desktopControlsFromOracle", "popupMenusInScope", "tooltipsInScope", "secondaryPointerHostTruthInScope", "middleClickTruthInScope"):
    if full_scope.get(key) is not True:
        add_failure(f"Muscle-memory parity policy must keep fullScope.{key}=true.", scope_inventory_reasons)

if not isinstance(policy.get("exitLayers"), list) or len(policy.get("exitLayers", [])) < 6:
    add_failure("Muscle-memory parity policy must declare the full exit-layer stack.", scope_inventory_reasons)
if len(tabs) == 0 or len(workspace_actions) == 0 or (len(desktop_controls) + len(acknowledged_factory_only)) == 0:
    add_failure("Parity oracle inventory is unexpectedly empty.", scope_inventory_reasons)
for marker in policy.get("requiredRuntimeTestMarkers") or []:
    if marker not in avalonia_gate_tests_text:
        add_failure(f"Runtime muscle-memory test marker missing from Avalonia gate tests: {marker}", inventory_runtime_reasons)

if inventory_receipt.get("contractName") != "chummer6-ui.chummer5a_muscle_memory_inventory":
    add_failure("Runtime muscle-memory inventory receipt contractName is missing or incorrect.", inventory_runtime_reasons)
if not status_is_pass(inventory_receipt.get("status")):
    add_failure("Runtime muscle-memory inventory receipt is missing or not passing.", inventory_runtime_reasons)

if not status_is_pass(local_screenshot_comparison_receipt.get("status")):
    add_failure("Chummer5A local screenshot comparison receipt is missing or not passing.", wiring_reasons)
if str(local_screenshot_comparison_receipt.get("provider") or "").strip().lower() != "local_authority_receipts":
    add_failure("Chummer5A local screenshot comparison receipt must declare provider 'local_authority_receipts'.", wiring_reasons)
if str(local_screenshot_comparison_receipt.get("scope") or "").strip().lower() != "local_only":
    add_failure("Chummer5A local screenshot comparison receipt must stay scoped to local_only.", wiring_reasons)
if not isinstance(local_screenshot_comparison_receipt.get("required_screenshots"), list) or not local_screenshot_comparison_receipt.get("required_screenshots"):
    add_failure("Chummer5A local screenshot comparison receipt is missing required screenshots data.", wiring_reasons)
if not isinstance(local_screenshot_comparison_receipt.get("receipts"), dict):
    add_failure("Chummer5A local screenshot comparison receipt is missing nested receipt references.", wiring_reasons)
if int(local_screenshot_comparison_receipt.get("screenshot_count") or 0) <= 0:
    add_failure("Chummer5A local screenshot comparison receipt did not capture any screenshots.", wiring_reasons)
if int(local_screenshot_comparison_receipt.get("current_ref_unique_count") or 0) <= 0:
    add_failure("Chummer5A local screenshot comparison receipt did not include any unique screenshot references.", wiring_reasons)
if int(local_screenshot_comparison_receipt.get("missing_required_count") or 0) != 0:
    add_failure("Chummer5A local screenshot comparison receipt is missing one or more required screenshots.", wiring_reasons)

local_screenshot_directory = Path(local_screenshot_comparison_receipt.get("screenshot_directory") or "")
if not local_screenshot_directory.is_absolute():
    local_screenshot_directory = (REPO_ROOT / local_screenshot_directory).resolve()
evidence["local_screenshot_directory"] = str(local_screenshot_directory)
if not local_screenshot_directory.is_dir():
    add_failure("Chummer5A local screenshot comparison receipt screenshot_directory is missing or not a directory.", wiring_reasons)
else:
    for screenshot in local_screenshot_comparison_receipt.get("required_screenshots") or []:
        screenshot_path = local_screenshot_directory / str(screenshot)
        if not screenshot_path.is_file():
            add_failure(f"Chummer5A local screenshot comparison required screenshot is missing: {screenshot}", wiring_reasons)

inventory_evidence = inventory_receipt.get("evidence") or {}
if not isinstance(inventory_evidence, dict):
    add_failure("Runtime muscle-memory inventory receipt evidence is missing.", inventory_runtime_reasons)
    inventory_evidence = {}

evidence["runtimeShellSurfaceCount"] = int(inventory_evidence.get("shellSurfaceCount") or 0)
evidence["runtimeMenuRootCount"] = int(inventory_evidence.get("menuRootCount") or 0)
evidence["runtimeDialogSurfaceCount"] = int(inventory_evidence.get("dialogSurfaceCount") or 0)
evidence["runtimeVisibleElementCount"] = int(inventory_evidence.get("totalVisibleElementCount") or 0)

if int(inventory_evidence.get("workspaceActionCount") or 0) != len(workspace_actions):
    add_failure(
        "Runtime muscle-memory inventory receipt does not cover every oracle workspace action.",
        inventory_runtime_reasons,
    )
expected_dialog_surface_count = len(desktop_controls) + len(acknowledged_factory_only)
if int(inventory_evidence.get("dialogSurfaceCount") or 0) < expected_dialog_surface_count:
    add_failure(
        "Runtime muscle-memory inventory receipt did not capture the full dialog surface set.",
        inventory_runtime_reasons,
    )
if int(inventory_evidence.get("totalVisibleElementCount") or 0) <= 0:
    add_failure("Runtime muscle-memory inventory receipt captured zero visible UI elements.", inventory_runtime_reasons)

runtime_reviews = inventory_receipt.get("reviews") or {}
if not isinstance(runtime_reviews, dict):
    add_failure("Runtime muscle-memory inventory receipt reviews are missing.", inventory_runtime_reasons)
    runtime_reviews = {}
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
    ):
        review = runtime_reviews.get(review_key) or {}
        if str(review.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
            add_failure(f"Runtime muscle-memory review failed: {review_key}", inventory_runtime_reasons)

detailed_dialog_checks = policy.get("detailedDialogChecks") or []
evidence["detailedDialogCheckCount"] = len(detailed_dialog_checks)
if not isinstance(detailed_dialog_checks, list) or len(detailed_dialog_checks) == 0:
    add_failure("Muscle-memory parity policy must seed at least one strict dialog check.", dialog_widget_reasons)
else:
    combined_test_text = dialog_factory_tests_text + "\n" + avalonia_gate_tests_text
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
            if pattern not in dialog_factory_text:
                check_reasons.append(f"current source pattern missing: {pattern}")
        for marker in check.get("requiredTestMarkers") or []:
            if marker not in combined_test_text:
                check_reasons.append(f"required test marker missing: {marker}")
        if check_reasons:
            add_failure(
                f"Detailed muscle-memory parity check failed for '{dialog_id}': " + "; ".join(check_reasons),
                dialog_widget_reasons,
            )
        detailed_results.append(
            {
                "dialogId": dialog_id,
                "legacyDesignerPath": str(legacy_designer_path),
                "status": "pass" if not check_reasons else "fail",
                "reasonCount": len(check_reasons),
                "reasons": check_reasons
            }
        )
    evidence["detailedDialogResults"] = detailed_results

required_design_markers = [
    "every menu strip and toolbar route",
    "every dialog and utility form",
    "every workspace panel, grid, tab strip, list, tree, and preview pane",
    "every popup menu, flyout, context menu, and tooltip",
    "Widget-class parity",
    "Visible copy parity",
    "Geography parity",
    "Pointer-route parity",
    "Mouse-only macro replay",
    "loop through every UI element",
    "right click",
    "middle click",
    "zero hosts"
]
missing_design_markers = [marker for marker in required_design_markers if marker not in design_doc_text]
evidence["missingDesignMarkers"] = missing_design_markers
if missing_design_markers:
    add_failure(
        "Muscle-memory design doc is missing required exit-test markers: " + ", ".join(missing_design_markers),
        design_reasons,
    )

for marker in [
    "checking Chummer5a muscle-memory parity gate",
    "bash scripts/ai/milestones/chummer5a-muscle-memory-parity-gate.sh"
]:
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing muscle-memory gate wiring marker: {marker}", wiring_reasons)
for marker in [
    "chummer5a-muscle-memory-parity-gate.sh",
    "muscleMemoryParityReview"
]:
    if marker not in visual_gate_text:
        add_failure(f"Visual familiarity gate is missing muscle-memory review wiring marker: {marker}", wiring_reasons)
if "muscleMemoryParityReview" not in screenshot_review_gate_text:
    add_failure("Screenshot review gate is missing muscle-memory review marker.", wiring_reasons)

payload["reviews"] = {
    "scopeInventoryReview": {
        "status": "pass" if not scope_inventory_reasons else "fail",
        "reasonCount": len(scope_inventory_reasons),
        "reasons": scope_inventory_reasons
    },
    "dialogWidgetClassReview": {
        "status": "pass" if not dialog_widget_reasons else "fail",
        "reasonCount": len(dialog_widget_reasons),
        "reasons": dialog_widget_reasons
    },
    "designReview": {
        "status": "pass" if not design_reasons else "fail",
        "reasonCount": len(design_reasons),
        "reasons": design_reasons
    },
    "inventoryRuntimeReview": {
        "status": "pass" if not inventory_runtime_reasons else "fail",
        "reasonCount": len(inventory_runtime_reasons),
        "reasons": inventory_runtime_reasons
    },
    "wiringReview": {
        "status": "pass" if not wiring_reasons else "fail",
        "reasonCount": len(wiring_reasons),
        "reasons": wiring_reasons
    }
}
evidence["reasonCount"] = len(reasons)
evidence["failureCount"] = len(reasons)
payload["status"] = "pass" if not reasons else "fail"
payload["summary"] = (
    "Muscle-memory parity policy, full-scope design, detailed seed checks, and release wiring are all present."
    if not reasons
    else "Muscle-memory parity proof is incomplete."
)
write_receipt(payload)
raise SystemExit(0 if not reasons else 1)
PY
