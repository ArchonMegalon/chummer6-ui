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

receipt_path="${CHUMMER_DESIGN_AUTHORIZED_PARITY_SOFTENING_RECEIPT_PATH:-$repo_root/.codex-studio/published/DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json}"
implementation_scope_path="$repo_root/.codex-design/repo/IMPLEMENTATION_SCOPE.md"
review_context_path="$repo_root/.codex-design/review/REVIEW_CONTEXT.md"
product_readme_path="$repo_root/.codex-design/product/README.md"
visual_difference_ledger_path="$repo_root/docs/CHUMMER5A_VISUAL_DIFFERENCE_LEDGER.json"
ruleset_adaptation_receipt_path="$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json"
interactive_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"
chummer5a_legacy_ui_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json"
chummer4_legacy_ui_receipt_path="$repo_root/.codex-studio/published/CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json"
verify_script_path="$repo_root/scripts/ai/verify.sh"
b14_script_path="$repo_root/scripts/ai/milestones/b14-flagship-ui-release-gate.sh"
compliance_tests_path="$repo_root/Chummer.Tests/Compliance/MigrationComplianceTests.cs"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' \
  "$receipt_path" \
  "$implementation_scope_path" \
  "$review_context_path" \
  "$product_readme_path" \
  "$visual_difference_ledger_path" \
  "$ruleset_adaptation_receipt_path" \
  "$interactive_inventory_receipt_path" \
  "$chummer5a_legacy_ui_receipt_path" \
  "$chummer4_legacy_ui_receipt_path" \
  "$verify_script_path" \
  "$b14_script_path" \
  "$compliance_tests_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

(
    receipt_path,
    implementation_scope_path,
    review_context_path,
    product_readme_path,
    visual_difference_ledger_path,
    ruleset_adaptation_receipt_path,
    interactive_inventory_receipt_path,
    chummer5a_legacy_ui_receipt_path,
    chummer4_legacy_ui_receipt_path,
    verify_script_path,
    b14_script_path,
    compliance_tests_path,
) = [Path(value) for value in sys.argv[1:13]]


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


required_paths = {
    "implementationScope": implementation_scope_path,
    "reviewContext": review_context_path,
    "productReadme": product_readme_path,
    "visualDifferenceLedger": visual_difference_ledger_path,
    "rulesetAdaptationReceipt": ruleset_adaptation_receipt_path,
    "interactiveInventoryReceipt": interactive_inventory_receipt_path,
    "chummer5aLegacyUiReceipt": chummer5a_legacy_ui_receipt_path,
    "chummer4LegacyUiReceipt": chummer4_legacy_ui_receipt_path,
    "verifyScript": verify_script_path,
    "b14Script": b14_script_path,
    "complianceTests": compliance_tests_path,
}

missing = [name for name, path in required_paths.items() if not path.is_file()]
if missing:
    payload = {
        "generatedAt": now_iso(),
        "contractName": "chummer6-ui.design_authorized_parity_softening",
        "status": "fail",
        "summary": "Design-authorized parity softening inputs are incomplete.",
        "reasons": [f"Missing required path: {required_paths[name]}" for name in missing],
        "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing}},
    }
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(71)

implementation_scope_text = read_text(implementation_scope_path)
review_context_text = read_text(review_context_path)
product_readme_text = read_text(product_readme_path)
visual_difference_ledger = load_json(visual_difference_ledger_path)
ruleset_adaptation_receipt = load_json(ruleset_adaptation_receipt_path)
interactive_inventory_receipt = load_json(interactive_inventory_receipt_path)
chummer5a_legacy_ui_receipt = load_json(chummer5a_legacy_ui_receipt_path)
chummer4_legacy_ui_receipt = load_json(chummer4_legacy_ui_receipt_path)
verify_script_text = read_text(verify_script_path)
b14_script_text = read_text(b14_script_path)
compliance_tests_text = read_text(compliance_tests_path)

reasons: list[str] = []
design_reasons: list[str] = []
receipt_reasons: list[str] = []
wiring_reasons: list[str] = []
ledger_reasons: list[str] = []


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


for marker in (
    "in-app bug/feedback/crash entry points",
    "source-linked tooltips and detail drawers",
    "authored SR4, SR5, and SR6 UX where edition differences materially change how builders reason about the character",
    "dense-data comfort and visual polish",
):
    if marker not in implementation_scope_text:
        add_failure(f"Implementation scope is missing required design marker: {marker}", design_reasons)

for marker in (
    "Flag rules math or engine authority logic in UI as P1.",
    "scope fit: pass/fail",
    "boundary fit: pass/fail",
    "contract fit: pass/fail",
):
    if marker not in review_context_text:
        add_failure(f"Review context is missing required design marker: {marker}", design_reasons)

for marker in (
    "CHUMMER5A_FAMILIARITY_BRIDGE.md",
    "LEGACY_CLIENT_AND_ADJACENT_PARITY.md",
    "FLAGSHIP_PARITY_REGISTRY.yaml",
    "FEEDBACK_AND_CRASH_REPORTING_SYSTEM.md",
    "BUILD_GHOST_MVP_001.md",
):
    if marker not in product_readme_text:
        add_failure(f"Product README is missing canonical design-read marker: {marker}", design_reasons)

if not status_ok(ruleset_adaptation_receipt.get("status")):
    add_failure("Ruleset UI adaptation receipt is missing or not passing.", receipt_reasons)
if not status_ok(interactive_inventory_receipt.get("status")):
    add_failure("Interactive control inventory receipt is missing or not passing.", receipt_reasons)
if not status_ok(chummer5a_legacy_ui_receipt.get("status")):
    add_failure("Chummer5a legacy UI element parity receipt is missing or not passing.", receipt_reasons)
if not status_ok(chummer4_legacy_ui_receipt.get("status")):
    add_failure("Chummer4 legacy UI element parity receipt is missing or not passing.", receipt_reasons)

ledger_entries = visual_difference_ledger.get("entries") or visual_difference_ledger.get("differences") or []
if not isinstance(ledger_entries, list) or not ledger_entries:
    add_failure("Chummer5a visual difference ledger is missing or empty.", ledger_reasons)
else:
    ledger_text = json.dumps(visual_difference_ledger, ensure_ascii=False)
    for marker in (
        "whyItDiffers",
        "parityIntent",
        "currentPosture",
        "legacyPosture",
    ):
        if marker not in ledger_text:
            add_failure(f"Visual difference ledger is missing required authored-difference marker: {marker}", ledger_reasons)
    for allowed_softening in (
        "Global Settings is rendered inside the shared shell dialog host",
        "Current parity prioritizes explicit context over reproducing the exact old arrangement of textboxes and labels.",
        "Dark mode is allowed to improve materially",
        "Visible runner tabs are required; full legacy window-management chrome is not.",
    ):
        if allowed_softening not in ledger_text:
            add_failure(f"Visual difference ledger is missing expected authorized softening example: {allowed_softening}", ledger_reasons)

build_ghost_text = ""
build_ghost_path = implementation_scope_path.parent.parent / "product" / "BUILD_GHOST_MVP_001.md"
if build_ghost_path.is_file():
    build_ghost_text = read_text(build_ghost_path)
else:
    add_failure(f"Missing Build Ghost design source: {build_ghost_path}", design_reasons)

for marker in (
    "Build Ghost",
    "explain",
    "guided",
):
    if build_ghost_text and marker not in build_ghost_text:
        add_failure(f"Build Ghost design source is missing marker: {marker}", design_reasons)

for marker in (
    "checking design-authorized parity softening gate",
    "bash scripts/ai/milestones/design-authorized-parity-softening-check.sh",
):
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing design-authorized parity softening wiring marker: {marker}", wiring_reasons)

for marker in (
    "design_authorized_parity_softening_receipt_path",
    "designAuthorizedParitySofteningReceiptPath",
):
    if marker not in b14_script_text:
        add_failure(f"B14 is missing design-authorized parity softening marker: {marker}", wiring_reasons)

for marker in (
    "Design_authorized_parity_softening_gate_requires_explicit_design_backing_for_any_intentional_divergence",
    "design-authorized-parity-softening-check.sh",
):
    if marker not in compliance_tests_text:
        add_failure(f"Compliance coverage is missing design-authorized parity softening marker: {marker}", wiring_reasons)

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.design_authorized_parity_softening",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Parity softening is only allowed when the local Chummer6 design explicitly authorizes and explains the divergence."
        if not reasons
        else "Design-authorized parity softening proof is incomplete."
    ),
    "reasons": reasons,
    "evidence": {
        "implementationScopePath": str(implementation_scope_path),
        "reviewContextPath": str(review_context_path),
        "productReadmePath": str(product_readme_path),
        "visualDifferenceLedgerPath": str(visual_difference_ledger_path),
        "rulesetAdaptationReceiptPath": str(ruleset_adaptation_receipt_path),
        "interactiveInventoryReceiptPath": str(interactive_inventory_receipt_path),
        "chummer5aLegacyUiReceiptPath": str(chummer5a_legacy_ui_receipt_path),
        "chummer4LegacyUiReceiptPath": str(chummer4_legacy_ui_receipt_path),
        "failureCount": len(reasons),
        "reasonCount": len(reasons),
    },
    "reviews": {
        "designMirrorReview": {
            "status": "pass" if not design_reasons else "fail",
            "reasonCount": len(design_reasons),
            "reasons": design_reasons,
        },
        "receiptReview": {
            "status": "pass" if not receipt_reasons else "fail",
            "reasonCount": len(receipt_reasons),
            "reasons": receipt_reasons,
        },
        "differenceLedgerReview": {
            "status": "pass" if not ledger_reasons else "fail",
            "reasonCount": len(ledger_reasons),
            "reasons": ledger_reasons,
        },
        "wiringReview": {
            "status": "pass" if not wiring_reasons else "fail",
            "reasonCount": len(wiring_reasons),
            "reasons": wiring_reasons,
        },
    },
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
raise SystemExit(0 if not reasons else 1)
PY
