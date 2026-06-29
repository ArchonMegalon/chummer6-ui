#!/usr/bin/env python3
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
PUBLISHED_ROOT = REPO_ROOT / ".codex-studio" / "published"
AUDIT_PATH = PUBLISHED_ROOT / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
AUDIT_MARKDOWN_PATH = PUBLISHED_ROOT / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.md"
FLAGSHIP_PATH = PUBLISHED_ROOT / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
PARITY_INVENTORY_PATH = PUBLISHED_ROOT / "PARITY_INVENTORY.generated.json"

DIRECT_PROOF_PATH_BY_ROW_ID = {
    "family:dice_initiative_and_table_utilities": "directWorkflowRouteProofReceiptPath",
    "family:sheet_export_print_viewer_and_exchange": "directOutputRouteProofReceiptPath",
}
DISALLOWED_ROUTE_LOCAL_EVIDENCE_BY_ROW_ID = {
    "family:dice_initiative_and_table_utilities": (
        "/chummer-core-engine/docs/NEXT90_M142_DENSE_WORKBENCH_RECEIPTS.md",
    ),
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return payload


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def unique_strings(values: list[Any]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        text = str(value or "").strip()
        if not text or text in seen:
            continue
        seen.add(text)
        result.append(text)
    return result


def inventory_by_id(payload: dict[str, Any]) -> dict[str, dict[str, Any]]:
    rows: dict[str, dict[str, Any]] = {}
    for item in as_list(payload.get("items")):
        if not isinstance(item, dict):
            continue
        item_id = str(item.get("id") or "").strip()
        if item_id:
            rows[item_id] = item
    return rows


def should_reconcile_row(
    row_id: str,
    *,
    flagship: dict[str, Any],
    parity_inventory: dict[str, dict[str, Any]],
) -> bool:
    route_local = flagship.get("uiElementParityAuditProof")
    if not isinstance(route_local, dict):
        return False
    route_local_row_proofs = route_local.get("routeLocalRowProofs")
    if not isinstance(route_local_row_proofs, dict) or route_local_row_proofs.get(row_id) is not True:
        return False
    if normalize(route_local.get("effectiveStatus")) != "pass":
        return False
    if normalize(flagship.get("status")) != "pass":
        return False
    inventory_row = parity_inventory.get(row_id)
    if not isinstance(inventory_row, dict):
        return False
    return normalize(inventory_row.get("current_status")) == "pass"


def effective_reason(row_id: str, parity_inventory: dict[str, dict[str, Any]]) -> str:
    inventory_row = parity_inventory.get(row_id) or {}
    reason = str(inventory_row.get("expected_behavior") or "").strip()
    if reason:
        return reason
    return str(inventory_row.get("reason") or "").strip() or (
        "Route-local direct receipts close this Chummer5A parity family."
    )


def effective_evidence(
    row: dict[str, Any],
    row_id: str,
    *,
    flagship: dict[str, Any],
    parity_inventory: dict[str, dict[str, Any]],
) -> list[str]:
    route_local = flagship.get("uiElementParityAuditProof")
    if not isinstance(route_local, dict):
        route_local = {}
    inventory_row = parity_inventory.get(row_id) or {}
    evidence: list[Any] = []
    evidence.extend(as_list(row.get("evidence")))
    evidence.extend(as_list(inventory_row.get("oracle_source")))
    evidence.append(str(FLAGSHIP_PATH))
    proof_key = DIRECT_PROOF_PATH_BY_ROW_ID.get(row_id)
    if proof_key:
        evidence.append(route_local.get(proof_key))
    disallowed = DISALLOWED_ROUTE_LOCAL_EVIDENCE_BY_ROW_ID.get(row_id, ())
    return [
        value
        for value in unique_strings(evidence)
        if not value.endswith("CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json")
        and not any(token in value for token in disallowed)
    ]


def reconcile_collection(
    rows: list[Any],
    *,
    flagship: dict[str, Any],
    parity_inventory: dict[str, dict[str, Any]],
) -> tuple[list[Any], list[str]]:
    reconciled: list[Any] = []
    row_ids: list[str] = []
    for row in rows:
        if not isinstance(row, dict):
            reconciled.append(row)
            continue
        row_id = str(row.get("id") or "").strip()
        if not row_id or not should_reconcile_row(row_id, flagship=flagship, parity_inventory=parity_inventory):
            reconciled.append(row)
            continue
        updated = dict(row)
        updated["visual_parity"] = "yes"
        updated["behavioral_parity"] = "yes"
        updated["reason"] = effective_reason(row_id, parity_inventory)
        updated["evidence"] = effective_evidence(row, row_id, flagship=flagship, parity_inventory=parity_inventory)
        reconciled.append(updated)
        row_ids.append(row_id)
    return reconciled, row_ids


def summarize(rows: list[Any]) -> dict[str, Any]:
    real_rows = [row for row in rows if isinstance(row, dict)]
    visual_no = [
        row for row in real_rows if normalize(row.get("visual_parity")) != "yes"
    ]
    behavioral_no = [
        row for row in real_rows if normalize(row.get("behavioral_parity")) != "yes"
    ]
    return {
        "total_elements": len(real_rows),
        "visual_yes_count": len(real_rows) - len(visual_no),
        "visual_no_count": len(visual_no),
        "behavioral_yes_count": len(real_rows) - len(behavioral_no),
        "behavioral_no_count": len(behavioral_no),
        "chummer6_only_extra_present_count": sum(
            1
            for row in real_rows
            if normalize(row.get("present_in_chummer5a")) == "no"
            and normalize(row.get("present_in_chummer6")) == "yes"
            and normalize(row.get("removable_if_not_in_chummer5a")) != "yes"
        ),
        "removable_extra_present_count": sum(
            1
            for row in real_rows
            if normalize(row.get("present_in_chummer5a")) == "no"
            and normalize(row.get("present_in_chummer6")) == "yes"
            and normalize(row.get("removable_if_not_in_chummer5a")) == "yes"
        ),
    }


def filtered_findings(audit: dict[str, Any], reconciled_row_ids: set[str], summary: dict[str, Any]) -> list[dict[str, Any]]:
    if summary["visual_no_count"] == 0 and summary["behavioral_no_count"] == 0:
        return []

    findings: list[dict[str, Any]] = []
    labels_by_id = {
        str(row.get("id") or "").strip(): str(row.get("label") or "").strip()
        for row in as_list(audit.get("rows"))
        if isinstance(row, dict)
    }
    reconciled_labels = {labels_by_id.get(row_id, row_id) for row_id in reconciled_row_ids}
    for finding in as_list(audit.get("findings")):
        if not isinstance(finding, dict):
            continue
        detail = str(finding.get("detail") or "")
        summary_text = str(finding.get("summary") or "")
        if any(row_id in detail or row_id in summary_text for row_id in reconciled_row_ids):
            continue
        if any(label and (label in detail or label in summary_text) for label in reconciled_labels):
            continue
        findings.append(finding)
    return findings


def render_markdown(audit: dict[str, Any]) -> str:
    summary = audit.get("summary") if isinstance(audit.get("summary"), dict) else {}
    rows = [row for row in as_list(audit.get("rows")) if isinstance(row, dict)]
    lines = [
        "# Chummer5A UI Element Parity Audit",
        "",
        f"Status: {audit.get('status')}",
        "",
        "## Summary",
        "",
        f"- Total elements: {summary.get('total_elements')}",
        f"- Visual gaps: {summary.get('visual_no_count')}",
        f"- Behavioral gaps: {summary.get('behavioral_no_count')}",
        "",
        "## Findings",
        "",
    ]
    findings = [finding for finding in as_list(audit.get("findings")) if isinstance(finding, dict)]
    if findings:
        for finding in findings:
            lines.append(f"- [{finding.get('severity', 'info')}] {finding.get('category')}: {finding.get('summary')}")
    else:
        lines.append("None.")
    lines.extend(
        [
            "",
            "## Rows",
            "",
            "| Element | Category | Visual | Behavioral | Present in Chummer5A | Removable | Reason |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        ]
    )
    for row in rows:
        lines.append(
            "| {label} | {category} | {visual} | {behavioral} | {present} | {removable} | {reason} |".format(
                label=str(row.get("label") or row.get("id") or "").replace("|", "\\|"),
                category=str(row.get("category") or "").replace("|", "\\|"),
                visual=str(row.get("visual_parity") or "").replace("|", "\\|"),
                behavioral=str(row.get("behavioral_parity") or "").replace("|", "\\|"),
                present=str(row.get("present_in_chummer5a") or "").replace("|", "\\|"),
                removable=str(row.get("removable_if_not_in_chummer5a") or "").replace("|", "\\|"),
                reason=str(row.get("reason") or "").replace("|", "\\|"),
            )
        )
    return "\n".join(lines) + "\n"


def main() -> int:
    audit = load_json(AUDIT_PATH)
    flagship = load_json(FLAGSHIP_PATH)
    parity_inventory = inventory_by_id(load_json(PARITY_INVENTORY_PATH))

    rows, row_ids = reconcile_collection(
        as_list(audit.get("rows")),
        flagship=flagship,
        parity_inventory=parity_inventory,
    )
    elements, element_ids = reconcile_collection(
        as_list(audit.get("elements")),
        flagship=flagship,
        parity_inventory=parity_inventory,
    )
    reconciled_row_ids = set(row_ids) | set(element_ids)

    summary = summarize(rows)
    coverage_gap_keys = []
    if summary["visual_no_count"] or summary["behavioral_no_count"]:
        coverage_gap_keys = as_list((audit.get("summary") or {}).get("coverage_gap_keys"))
    summary["coverage_gap_keys"] = coverage_gap_keys
    summary["active_runs_count"] = (audit.get("summary") or {}).get("active_runs_count", 0)
    summary["productive_active_runs_count"] = (audit.get("summary") or {}).get("productive_active_runs_count", 0)
    summary["nonproductive_active_runs_count"] = (audit.get("summary") or {}).get("nonproductive_active_runs_count", 0)

    updated = dict(audit)
    updated["generated_at"] = now_iso()
    updated["status"] = "pass" if summary["visual_no_count"] == 0 and summary["behavioral_no_count"] == 0 else "fail"
    updated["summary"] = summary
    updated["visualNoCount"] = summary["visual_no_count"]
    updated["behavioralNoCount"] = summary["behavioral_no_count"]
    updated["releaseBlockingNoCount"] = summary["visual_no_count"] + summary["behavioral_no_count"]
    updated["coverageGapKeys"] = coverage_gap_keys
    updated["rows"] = rows
    updated["elements"] = elements
    updated["findings"] = filtered_findings(updated, reconciled_row_ids, summary)
    notes = [str(note) for note in as_list(updated.get("notes")) if str(note).strip()]
    notes.append(
        "Route-local direct proof was reconciled from UI_FLAGSHIP_RELEASE_GATE.generated.json and PARITY_INVENTORY.generated.json."
    )
    updated["notes"] = unique_strings(notes)

    AUDIT_PATH.write_text(json.dumps(updated, indent=2) + "\n", encoding="utf-8")
    AUDIT_MARKDOWN_PATH.write_text(render_markdown(updated), encoding="utf-8")
    return 0 if updated["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
