#!/usr/bin/env python3
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
CORE_ROOT = Path("/docker/chummercomplete/chummer-core-engine")
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
CORE_PUBLISHED = CORE_ROOT / ".codex-studio" / "published"
OUT = PUBLISHED / "SR456_PARITY_FRONTIER.generated.json"


def load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    return payload if isinstance(payload, dict) else {}


def passing(path: Path) -> bool:
    return str(load_json(path).get("status", "")).strip().lower() == "pass"


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def table_import_summary(ruleset: str) -> dict[str, Any]:
    payload = load_json(CORE_PUBLISHED / f"{ruleset.upper()}_TABLE_IMPORTS.generated.json")
    if not payload:
        return {"status": "missing"}
    summary: dict[str, Any] = {"status": payload.get("status", "unknown")}
    if "file_count" in payload:
        summary["file_count"] = payload.get("file_count")
    if "row_count" in payload:
        summary["row_count"] = payload.get("row_count")
    if "sourcebook_count" in payload:
        summary["sourcebook_count"] = payload.get("sourcebook_count")
    if "remaining_gate" in payload:
        summary["remaining_gate"] = payload.get("remaining_gate")
    return summary


def ruleset_row(ruleset: str) -> dict[str, Any]:
    if ruleset == "sr4":
        authority = load_json(CORE_PUBLISHED / "SR4_RULE_AUTHORITY_INTEGRATION.generated.json")
        visual_receipts = [
            PUBLISHED / "CHUMMER4_SR4_MUSCLE_MEMORY_PARITY_GATE.generated.json",
            PUBLISHED / "SR4_DESKTOP_WORKFLOW_PARITY.generated.json",
            PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json",
        ]
        rule_status = "partial_seed_not_ready"
        blockers = [
            "legacy structured SR4 table evidence indexed; row-level mapping review still required",
            "errata profile application",
            "human rule review",
        ]
    elif ruleset == "sr5":
        authority = load_json(CORE_PUBLISHED / "SR5_ACCEPTANCE_PROOF.generated.json")
        visual_receipts = [
            PUBLISHED / "CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE.generated.json",
            PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json",
            PUBLISHED / "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json",
            PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json",
        ]
        rule_status = "accepted"
        blockers = []
    elif ruleset == "sr6":
        authority = load_json(CORE_PUBLISHED / "SR6_RULE_AUTHORITY_INTEGRATION.generated.json")
        visual_receipts = [
            PUBLISHED / "CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json",
            PUBLISHED / "CHUMMER_SR6_SHARED_MUSCLE_MEMORY_PARITY_GATE.generated.json",
            PUBLISHED / "SR6_DESKTOP_WORKFLOW_PARITY.generated.json",
            PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json",
        ]
        rule_status = "partial_seed_not_ready"
        blockers = [
            "private SR6 PDF row-hash import indexed; row-level normalized mapping review still required",
            "errata/profile review",
            "human rule review",
        ]
    else:
        raise ValueError(ruleset)

    visual_missing = [str(path) for path in visual_receipts if not passing(path)]
    rule_verdict = str(authority.get("final_verdict") or authority.get("serious_implementation_claim") or authority.get("status") or "")
    table_import = table_import_summary(ruleset)
    visual_status = "pass" if not visual_missing else "fail"
    full_ready = ruleset == "sr5" and visual_status == "pass" and rule_status == "accepted"
    if ruleset in {"sr4", "sr6"} and rule_verdict in {"SR4_RULE_AUTHORITY_READY", "SR6_RULE_AUTHORITY_READY"}:
        full_ready = visual_status == "pass"
        if full_ready:
            rule_status = "accepted"
            blockers = []
            table_import["legacy_table_import_status"] = table_import.get("status", "unknown")
            table_import["status"] = "covered_by_rule_authority_registry"
            table_import["remaining_gate"] = "none_for_current_full_rule_authority_claim"

    return {
        "ruleset": ruleset,
        "full_ready": full_ready,
        "rule_status": rule_status,
        "rule_verdict": rule_verdict,
        "table_import": table_import,
        "visual_mouse_parity_status": visual_status,
        "visual_receipts": [str(path) for path in visual_receipts],
        "missing_visual_receipts": visual_missing,
        "remaining_blockers": blockers if not full_ready else [],
    }


def main() -> int:
    rows = [ruleset_row("sr4"), ruleset_row("sr5"), ruleset_row("sr6")]
    ok = all(row["visual_mouse_parity_status"] == "pass" for row in rows)
    full_ready_count = sum(1 for row in rows if row["full_ready"])
    all_full_ready = full_ready_count == len(rows)
    payload = {
        "generatedAt": now_iso(),
        "contractName": "chummer6.sr456_parity_frontier",
        "status": "pass" if ok else "fail",
        "summary": (
            "SR4, SR5, and SR6 UI/mouse parity and rule-authority receipts are green."
            if ok and all_full_ready
            else (
                "SR4, SR5, and SR6 UI/mouse parity receipts are green; at least one ruleset still has a rule-authority blocker."
                if ok
                else "One or more SR4/SR5/SR6 UI/mouse parity receipts are missing or failing."
            )
        ),
        "fullReadyRulesetCount": full_ready_count,
        "rows": rows,
    }
    OUT.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(payload, indent=2))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
