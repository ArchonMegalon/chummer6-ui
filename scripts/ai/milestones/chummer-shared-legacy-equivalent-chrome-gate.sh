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

receipt_path="${CHUMMER_SHARED_LEGACY_EQUIVALENT_CHROME_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER_SHARED_LEGACY_EQUIVALENT_CHROME_GATE.generated.json}"
policy_path="${CHUMMER_SHARED_LEGACY_EQUIVALENT_CHROME_POLICY_PATH:-$repo_root/docs/CHUMMER_SHARED_LEGACY_EQUIVALENT_CHROME_POLICY.json}"
design_doc_path="$repo_root/docs/CHUMMER_SHARED_LEGACY_EQUIVALENT_CHROME_EXIT_TESTS.md"
verify_script_path="$repo_root/scripts/ai/verify.sh"
chummer5a_inventory_path="${CHUMMER5A_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_MUSCLE_MEMORY_INVENTORY.generated.json}"
sr4_inventory_path="${CHUMMER4_SR4_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER4_SR4_MUSCLE_MEMORY_INVENTORY.generated.json}"
sr6_inventory_path="${CHUMMER_SR6_SHARED_MUSCLE_MEMORY_INVENTORY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER_SR6_SHARED_MUSCLE_MEMORY_INVENTORY.generated.json}"
toolstrip_source_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"
home_source_path="$repo_root/Chummer.Avalonia/DesktopHomeWindow.cs"
organizer_source_path="$repo_root/Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' \
  "$receipt_path" \
  "$policy_path" \
  "$design_doc_path" \
  "$verify_script_path" \
  "$chummer5a_inventory_path" \
  "$sr4_inventory_path" \
  "$sr6_inventory_path" \
  "$toolstrip_source_path" \
  "$home_source_path" \
  "$organizer_source_path"
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

(
    receipt_path,
    policy_path,
    design_doc_path,
    verify_script_path,
    chummer5a_inventory_path,
    sr4_inventory_path,
    sr6_inventory_path,
    toolstrip_source_path,
    home_source_path,
    organizer_source_path,
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


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def add_failure(message: str, bucket: list[str]) -> None:
    if message not in reasons:
        reasons.append(message)
    if message not in bucket:
        bucket.append(message)


def iter_receipt_strings(receipt: dict[str, Any]):
    evidence = receipt.get("evidence") or {}
    if not isinstance(evidence, dict):
        return
    for group_name in ("shellSurfaces", "menuRootSurfaces", "workspaceActionSurfaces", "dialogSurfaces"):
        for surface in evidence.get(group_name) or []:
            if not isinstance(surface, dict):
                continue
            surface_id = str(surface.get("surfaceId") or "<surface>").strip()
            for text in surface.get("visibleTextSamples") or []:
                if isinstance(text, str) and text.strip():
                    yield surface_id, f"{group_name}.visibleTextSamples", text
            for element in surface.get("elements") or []:
                if not isinstance(element, dict):
                    continue
                text = str(element.get("text") or "").strip()
                if text:
                    yield surface_id, "elements.text", text
                tooltip = str(element.get("toolTip") or "").strip()
                if tooltip:
                    yield surface_id, "elements.toolTip", tooltip
            for field in surface.get("dialogFields") or []:
                if not isinstance(field, dict) or not field.get("isVisible"):
                    continue
                label = str(field.get("runtimeLabelText") or "").strip()
                if label:
                    yield surface_id, "dialogFields.runtimeLabelText", label
                tooltip = str(field.get("runtimeToolTip") or "").strip()
                if tooltip:
                    yield surface_id, "dialogFields.runtimeToolTip", tooltip
            for action in surface.get("dialogActions") or []:
                if not isinstance(action, dict) or not action.get("isVisible"):
                    continue
                label = str(action.get("runtimeLabelText") or "").strip()
                if label:
                    yield surface_id, "dialogActions.runtimeLabelText", label
                tooltip = str(action.get("runtimeToolTip") or "").strip()
                if tooltip:
                    yield surface_id, "dialogActions.runtimeToolTip", tooltip


required_paths = {
    "policy": policy_path,
    "designDoc": design_doc_path,
    "verifyScript": verify_script_path,
    "chummer5aInventory": chummer5a_inventory_path,
    "sr4Inventory": sr4_inventory_path,
    "sr6Inventory": sr6_inventory_path,
    "toolStripSource": toolstrip_source_path,
    "homeSource": home_source_path,
    "organizerSource": organizer_source_path,
}
missing_paths = [name for name, path in required_paths.items() if not path.is_file()]
if missing_paths:
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.chummer_shared_legacy_equivalent_chrome_gate",
            "status": "fail",
            "summary": "Shared legacy-equivalent chrome gate inputs are incomplete.",
            "reasons": [f"Missing required path: {required_paths[name]}" for name in missing_paths],
            "evidence": {"missingPaths": {name: str(required_paths[name]) for name in missing_paths}},
        }
    )
    raise SystemExit(71)

policy = load_json(policy_path)
design_doc_text = read_text(design_doc_path)
verify_script_text = read_text(verify_script_path)
current_shared_source_text = "\n".join(
    [
        read_text(toolstrip_source_path),
        read_text(home_source_path),
        read_text(organizer_source_path),
    ]
)
inventories = {
    "chummer5a": load_json(chummer5a_inventory_path),
    "sr4": load_json(sr4_inventory_path),
    "sr6": load_json(sr6_inventory_path),
}

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.chummer_shared_legacy_equivalent_chrome_gate",
    "status": "fail",
    "summary": "Shared legacy-equivalent chrome proof is incomplete.",
    "reasons": [],
    "evidence": {
        "receiptPath": str(receipt_path),
        "policyPath": str(policy_path),
        "designDocPath": str(design_doc_path),
        "inventoryPaths": {
            "chummer5a": str(chummer5a_inventory_path),
            "sr4": str(sr4_inventory_path),
            "sr6": str(sr6_inventory_path),
        },
        "currentSharedSourcePaths": {
            "toolStrip": str(toolstrip_source_path),
            "home": str(home_source_path),
            "organizer": str(organizer_source_path),
        },
        "runtimeHitCount": 0,
        "runtimeHits": [],
        "ignoredStaleRuntimeHitCount": 0,
        "ignoredStaleRuntimeHits": [],
    },
    "reviews": {},
}
reasons: list[str] = payload["reasons"]
evidence: dict[str, Any] = payload["evidence"]
policy_reasons: list[str] = []
inventory_reasons: list[str] = []
runtime_reasons: list[str] = []
wiring_reasons: list[str] = []

if policy.get("contractName") != "chummer6-ui.chummer_shared_legacy_equivalent_chrome_policy":
    add_failure("Shared legacy-equivalent chrome policy contractName is missing or incorrect.", policy_reasons)

expected_contracts = policy.get("inventoryContracts") or {}
for lane, expected_contract in (
    ("chummer5a", "chummer6-ui.chummer5a_muscle_memory_inventory"),
    ("sr4", "chummer6-ui.chummer4_sr4_muscle_memory_inventory"),
    ("sr6", "chummer6-ui.sr6_shared_muscle_memory_inventory"),
):
    if expected_contracts.get(lane) != expected_contract:
        add_failure(f"Shared chrome policy must pin the {lane} inventory contract.", policy_reasons)

for lane, receipt in inventories.items():
    expected_contract = expected_contracts.get(lane)
    if str(receipt.get("contractName") or "").strip() != str(expected_contract or "").strip():
        add_failure(f"{lane} runtime inventory receipt contract name is missing or incorrect.", inventory_reasons)
    if not status_ok(receipt.get("status")):
        add_failure(f"{lane} runtime inventory receipt is missing or not passing.", inventory_reasons)

forbidden_tokens = policy.get("forbiddenRuntimeTokens") or []
if not isinstance(forbidden_tokens, list) or len(forbidden_tokens) == 0:
    add_failure("Shared chrome policy must seed at least one forbidden runtime token.", policy_reasons)
else:
    runtime_hits: list[dict[str, Any]] = []
    ignored_runtime_hits: list[dict[str, Any]] = []
    for token in forbidden_tokens:
        if not isinstance(token, dict):
            add_failure("Shared chrome policy contains a non-object forbiddenRuntimeTokens row.", policy_reasons)
            continue
        token_id = str(token.get("id") or "").strip()
        mode = str(token.get("mode") or "").strip()
        value = str(token.get("value") or "")
        if not token_id or not mode or not value:
            add_failure("Shared chrome policy has an incomplete forbidden runtime token row.", policy_reasons)
            continue
        for lane, receipt in inventories.items():
            for surface_id, source_kind, text in iter_receipt_strings(receipt) or []:
                matched = False
                if mode == "literal_absence":
                    matched = value in text
                elif mode == "regex_absence":
                    matched = re.search(value, text) is not None
                else:
                    add_failure(f"Shared chrome policy uses unsupported mode '{mode}' for token '{token_id}'.", policy_reasons)
                    break
                if matched:
                    hit = {
                        "lane": lane,
                        "tokenId": token_id,
                        "surfaceId": surface_id,
                        "sourceKind": source_kind,
                        "text": text,
                    }
                    if value not in current_shared_source_text:
                        ignored_runtime_hits.append(hit)
                        continue
                    runtime_hits.append(hit)
                    add_failure(
                        f"{lane} shared runtime surface '{surface_id}' reintroduced forbidden chrome '{token_id}' via {source_kind}: {text}",
                        runtime_reasons,
                    )

    evidence["runtimeHits"] = runtime_hits[:50]
    evidence["runtimeHitCount"] = len(runtime_hits)
    evidence["ignoredStaleRuntimeHits"] = ignored_runtime_hits[:50]
    evidence["ignoredStaleRuntimeHitCount"] = len(ignored_runtime_hits)

for marker in [
    "runtime inventory receipts from Chummer5A, SR4, and SR6",
    "review framing is forbidden",
    "Runner Summary, Build Lab, Browse Workspace, NPC Persona Studio, Contact Graph, and Downtime Planner are forbidden",
]:
    if marker not in design_doc_text:
        add_failure(f"Shared legacy-equivalent chrome design doc is missing marker: {marker}", policy_reasons)

for marker in [
    "checking shared legacy-equivalent chrome gate",
    "bash scripts/ai/milestones/chummer-shared-legacy-equivalent-chrome-gate.sh",
]:
    if marker not in verify_script_text:
        add_failure(f"verify.sh is missing shared legacy-equivalent chrome gate wiring marker: {marker}", wiring_reasons)

payload["reviews"] = {
    "policyReview": {
        "status": "pass" if not policy_reasons else "fail",
        "reasonCount": len(policy_reasons),
        "reasons": policy_reasons,
    },
    "inventoryReview": {
        "status": "pass" if not inventory_reasons else "fail",
        "reasonCount": len(inventory_reasons),
        "reasons": inventory_reasons,
    },
    "runtimeChromeReview": {
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
    "Shared legacy-equivalent runtime chrome stays stripped across the Chummer5A, SR4, and SR6 promoted surfaces."
    if not reasons
    else "Shared legacy-equivalent runtime chrome drift was detected."
)
write_receipt(payload)
raise SystemExit(0 if not reasons else 1)
PY
