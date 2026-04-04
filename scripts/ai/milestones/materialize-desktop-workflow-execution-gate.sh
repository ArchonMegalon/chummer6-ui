#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
ui_workflow_parity_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_workflow_parity_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr_frontier_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
sr4_ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
sr6_ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$ui_workflow_parity_path" "$sr4_workflow_parity_path" "$sr6_workflow_parity_path" "$sr_frontier_path" "$flagship_gate_path" "$sr4_ledger_path" "$sr6_ledger_path" "$repo_root"
from __future__ import annotations

import json
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


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def check_receipt(path: Path, label: str, reasons: List[str], evidence: Dict[str, Any]) -> Dict[str, Any]:
    payload = load_json(path)
    status = str(payload.get("status") or "").strip().lower()
    evidence[f"{label}_path"] = str(path)
    evidence[f"{label}_status"] = status
    if not status_ok(status):
        reasons.append(f"{label} receipt is missing or not passing.")
    return payload


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


(
    receipt_path_text,
    ui_workflow_parity_path_text,
    sr4_workflow_parity_path_text,
    sr6_workflow_parity_path_text,
    sr_frontier_path_text,
    flagship_gate_path_text,
    sr4_ledger_path_text,
    sr6_ledger_path_text,
    repo_root_text,
) = sys.argv[1:10]

receipt_path = Path(receipt_path_text)
ui_workflow_parity_path = Path(ui_workflow_parity_path_text)
sr4_workflow_parity_path = Path(sr4_workflow_parity_path_text)
sr6_workflow_parity_path = Path(sr6_workflow_parity_path_text)
sr_frontier_path = Path(sr_frontier_path_text)
flagship_gate_path = Path(flagship_gate_path_text)
sr4_ledger_path = Path(sr4_ledger_path_text)
sr6_ledger_path = Path(sr6_ledger_path_text)
repo_root = Path(repo_root_text)

reasons: List[str] = []
evidence: Dict[str, Any] = {}

check_receipt(ui_workflow_parity_path, "chummer5a_workflow_parity", reasons, evidence)
check_receipt(sr4_workflow_parity_path, "sr4_workflow_parity", reasons, evidence)
check_receipt(sr6_workflow_parity_path, "sr6_workflow_parity", reasons, evidence)
check_receipt(sr_frontier_path, "sr4_sr6_frontier", reasons, evidence)
flagship_gate = check_receipt(flagship_gate_path, "ui_flagship_release_gate", reasons, evidence)
flagship_head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
required_desktop_heads = sorted(
    {
        normalize_token(item)
        for item in (
            flagship_gate.get("desktopHeads")
            if isinstance(flagship_gate.get("desktopHeads"), list)
            else [flagship_gate.get("desktopHead")] if flagship_gate.get("desktopHead") else []
        )
        if normalize_token(item)
    }
)
flagship_head_proof_statuses = {
    normalize_token(head): normalize_token((proof or {}).get("status"))
    for head, proof in flagship_head_proofs.items()
    if normalize_token(head) and isinstance(proof, dict)
}
required_head_contract_markers = {
    "avalonia": [
        "status",
        "visualReview",
        "themeReadabilityContrast",
        "bundledDemoRunner",
        "requiredRuntimeBackedTests",
        "sourceTestFile",
        "testSuites",
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
        "requiredShellTests",
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
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
    ],
}
required_head_list_markers = {
    "avalonia": [
        "requiredRuntimeBackedTests",
        "testSuites",
    ],
    "blazor-desktop": [
        "requiredShellTests",
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
evidence["flagship_required_desktop_heads"] = required_desktop_heads
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
checked_family_receipts = 0
missing_execution_receipts: List[str] = []
failing_execution_receipts: List[str] = []
weak_execution_receipts: List[str] = []
checked_execution_receipts = 0
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
        if not candidate.is_file():
            missing_family_receipts.append(f"{edition}:{family_id}:{rel_path}")
            continue
        payload = load_json(candidate)
        status = str(payload.get("status") or "").strip().lower()
        if not status_ok(status):
            failing_family_receipts.append(f"{edition}:{family_id}:{rel_path}={status or 'missing'}")

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
evidence["workflow_execution_receipt_count_checked"] = checked_execution_receipts
evidence["workflow_execution_missing_receipts"] = missing_execution_receipts
evidence["workflow_execution_failing_receipts"] = failing_execution_receipts
evidence["workflow_execution_weak_receipts"] = weak_execution_receipts
evidence["legacy_execution_receipt_paths"] = legacy_execution_receipt_paths
evidence["required_workflow_family_ids"] = sorted(REQUIRED_WORKFLOW_FAMILY_IDS)
evidence["missing_required_workflow_family_ids"] = missing_required_family_ids
evidence["not_ready_required_workflow_family_ids"] = not_ready_required_family_ids
evidence["missing_required_workflow_family_audit_tests"] = missing_required_family_audit_tests

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
if failing_family_receipts:
    reasons.append(
        "SR4/SR6 family-level workflow receipts are not passing: " + ", ".join(sorted(failing_family_receipts))
    )
if checked_execution_receipts == 0:
    reasons.append("No SR4/SR6 family-level execution receipts were discovered from ledgers.")
if missing_execution_receipts:
    reasons.append(
        "Missing SR4/SR6 family-level execution receipts: " + ", ".join(sorted(missing_execution_receipts))
    )
if failing_execution_receipts:
    reasons.append(
        "SR4/SR6 family-level execution receipts are not passing: " + ", ".join(sorted(failing_execution_receipts))
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

status = "pass" if not reasons else "fail"
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_workflow_execution_gate",
    "status": status,
    "summary": (
        "Desktop workflow execution gate is proven by passing Chummer5a/SR4/SR6 parity receipts and explicitly grounded family-level SR4/SR6 execution receipts."
        if status == "pass"
        else "Desktop workflow execution gate is not fully proven."
    ),
    "reasons": reasons,
    "evidence": evidence,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    raise SystemExit(43)
PY

echo "[desktop-workflow-execution-gate] PASS"
