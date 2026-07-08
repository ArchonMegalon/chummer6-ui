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

receipt_path="${CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE.generated.json}"
policy_path="${CHUMMER5A_LEGACY_EQUIVALENT_CHROME_POLICY_PATH:-$repo_root/docs/CHUMMER5A_LEGACY_EQUIVALENT_CHROME_POLICY.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path" "$policy_path"
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
policy_path = Path(sys.argv[3])

REQUIRED_FIELDS = [
    "family_id",
    "surface_id",
    "dialog_id",
    "element_id",
    "element_label",
    "present_in_chummer5a",
    "present_in_chummer6",
    "visual_parity",
    "behavioral_parity",
    "removable_if_not_in_chummer5a",
    "reason",
]
YES_NO_FIELDS = [
    "present_in_chummer5a",
    "present_in_chummer6",
    "visual_parity",
    "behavioral_parity",
    "removable_if_not_in_chummer5a",
]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def append_reason(message: str, bucket: list[str]) -> None:
    reasons.append(message)
    bucket.append(message)


def load_json(path: Path) -> dict[str, Any]:
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(loaded, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return loaded


def row_label(row: dict[str, Any], index: int) -> str:
    family = str(row.get("family_id") or "<family>").strip()
    surface = str(row.get("surface_id") or "<surface>").strip()
    element = str(row.get("element_id") or f"row-{index}").strip()
    return f"{family}/{surface}/{element}"


reasons: list[str] = []
policy_reasons: list[str] = []
source_absence_reasons: list[str] = []
tester_wiring_reasons: list[str] = []
evidence: dict[str, Any] = {
    "policyPath": str(policy_path),
    "checkedRows": 0,
    "removableRowsChecked": 0,
    "forbiddenLiteralHits": [],
    "missingRequiredMarkers": [],
    "missingPolicyPaths": [],
}

if not policy_path.is_file():
    payload = {
        "generatedAt": now_iso(),
        "contract_name": "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
        "status": "fail",
        "summary": "Legacy-equivalent chrome gate is missing its policy file.",
        "reasons": [f"Missing required policy path: {policy_path}"],
        "evidence": evidence,
    }
    evidence["missingPolicyPaths"] = [str(policy_path)]
    evidence["failureCount"] = 1
    write_receipt(payload)
    raise SystemExit(67)

policy = load_json(policy_path)
rows = policy.get("rows")
if not isinstance(rows, list):
    append_reason("Policy rows must be a JSON array.", policy_reasons)
    rows = []

policy_contract_name = str(policy.get("contractName") or "").strip()
if policy_contract_name != "chummer6-ui.chummer5a_legacy_equivalent_chrome_policy":
    append_reason(
        "Legacy-equivalent chrome policy contractName drifted from chummer6-ui.chummer5a_legacy_equivalent_chrome_policy.",
        policy_reasons,
    )

policy_required_fields = policy.get("auditRequiredFields")
if policy_required_fields != REQUIRED_FIELDS:
    append_reason(
        "Legacy-equivalent chrome policy auditRequiredFields drifted from the canonical Chummer5A parity field list.",
        policy_reasons,
    )

policy_yes_no_fields = policy.get("allowedYesNoFields")
if policy_yes_no_fields != YES_NO_FIELDS:
    append_reason(
        "Legacy-equivalent chrome policy allowedYesNoFields drifted from the canonical yes/no parity field list.",
        policy_reasons,
    )

for index, row in enumerate(rows):
    if not isinstance(row, dict):
        append_reason(f"Policy row {index} is not an object.", policy_reasons)
        continue

    evidence["checkedRows"] += 1
    label = row_label(row, index)

    for field_name in REQUIRED_FIELDS:
        if field_name not in row or str(row.get(field_name) or "").strip() == "":
            append_reason(f"{label} is missing required field '{field_name}'.", policy_reasons)

    for field_name in YES_NO_FIELDS:
        value = str(row.get(field_name) or "").strip().lower()
        if value not in {"yes", "no"}:
            append_reason(f"{label} field '{field_name}' must be yes or no, got {value or '<blank>'}.", policy_reasons)

    if str(row.get("removable_if_not_in_chummer5a") or "").strip().lower() != "yes":
        append_reason(
            f"{label} must stay removable_if_not_in_chummer5a=yes because this gate only inventories removable legacy-equivalent chrome.",
            policy_reasons,
        )

    if str(row.get("present_in_chummer6") or "").strip().lower() != "no":
        append_reason(
            f"{label} must stay present_in_chummer6=no because removable legacy-equivalent chrome should remain stripped.",
            policy_reasons,
        )

    evidence["removableRowsChecked"] += 1

    for check in row.get("source_absence_checks") or []:
        if not isinstance(check, dict):
            append_reason(f"{label} contains a non-object source_absence_checks row.", source_absence_reasons)
            continue
        relative_path = str(check.get("path") or "").strip()
        mode = str(check.get("mode") or "").strip()
        value = str(check.get("value") or "")
        if not relative_path or not mode or not value:
            append_reason(f"{label} has an incomplete source absence check.", source_absence_reasons)
            continue
        source_path = repo_root / relative_path
        if not source_path.is_file():
            append_reason(f"{label} source absence check path is missing: {source_path}", source_absence_reasons)
            continue
        text = source_path.read_text(encoding="utf-8")
        matched = False
        if mode == "literal_absence":
            matched = value in text
        elif mode == "regex_absence":
            matched = re.search(value, text, re.MULTILINE) is not None
        else:
            append_reason(f"{label} uses unsupported source absence mode '{mode}'.", source_absence_reasons)
            continue
        if matched:
            evidence["forbiddenLiteralHits"].append(
                {
                    "row": label,
                    "path": relative_path,
                    "mode": mode,
                    "value": value,
                }
            )
            append_reason(
                f"{label} reintroduced removable legacy-incompatible chrome in {relative_path}: {value}",
                source_absence_reasons,
            )

    for check in row.get("required_marker_checks") or []:
        if not isinstance(check, dict):
            append_reason(f"{label} contains a non-object required_marker_checks row.", tester_wiring_reasons)
            continue
        relative_path = str(check.get("path") or "").strip()
        literals = check.get("literals") or []
        if not relative_path or not isinstance(literals, list) or not literals:
            append_reason(f"{label} has an incomplete required marker check.", tester_wiring_reasons)
            continue
        source_path = repo_root / relative_path
        if not source_path.is_file():
            append_reason(f"{label} required marker path is missing: {source_path}", tester_wiring_reasons)
            continue
        text = source_path.read_text(encoding="utf-8")
        for literal in literals:
            token = str(literal or "")
            if token and token not in text:
                evidence["missingRequiredMarkers"].append(
                    {
                        "row": label,
                        "path": relative_path,
                        "literal": token,
                    }
                )
                append_reason(
                    f"{label} is missing required marker '{token}' in {relative_path}.",
                    tester_wiring_reasons,
                )

status = "pass" if not reasons else "fail"
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
    "status": status,
    "summary": (
        "Legacy-equivalent chrome stays stripped when Chummer5A already had the same function."
        if status == "pass"
        else "Legacy-equivalent chrome drift was detected."
    ),
    "reasons": reasons,
    "reviews": {
        "policyReview": {
            "status": "pass" if not policy_reasons else "fail",
            "reasonCount": len(policy_reasons),
            "reasons": policy_reasons,
            "policyContractName": policy_contract_name,
        },
        "sourceAbsenceReview": {
            "status": "pass" if not source_absence_reasons else "fail",
            "reasonCount": len(source_absence_reasons),
            "reasons": source_absence_reasons,
        },
        "testerWiringReview": {
            "status": "pass" if not tester_wiring_reasons else "fail",
            "reasonCount": len(tester_wiring_reasons),
            "reasons": tester_wiring_reasons,
        },
    },
    "evidence": evidence,
}
payload["evidence"]["failureCount"] = len(reasons)
write_receipt(payload)
if status != "pass":
    raise SystemExit(68)
PY

echo "[chummer5a-legacy-equivalent-chrome-gate] PASS"
