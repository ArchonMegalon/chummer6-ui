#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Callable


REPO_ROOT = Path(__file__).absolute().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
PASS_STATUSES = {"pass", "passed", "ready"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--family")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"Expected JSON object in {path}")
    return payload


def ensure_status(name: str, payload: dict[str, Any]) -> None:
    status = str(payload.get("status") or "").strip().lower()
    if status not in PASS_STATUSES:
        raise SystemExit(f"{name} is not passed")


def require_all_true(mapping: object, label: str) -> None:
    if not isinstance(mapping, dict) or not mapping:
        raise SystemExit(f"{label} is missing or empty")
    failing = [key for key, value in mapping.items() if value is not True]
    if failing:
        raise SystemExit(f"{label} has failing checks: {', '.join(sorted(failing))}")


def runtime_specs() -> dict[str, tuple[Path, Callable[[dict[str, Any]], None]]]:
    m141 = PUBLISHED / "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"
    m142 = PUBLISHED / "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"
    m143 = PUBLISHED / "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json"

    def translator_xml_bridge(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M141 evidence is missing")
        route_checks = evidence.get("routeReceiptChecks")
        if not isinstance(route_checks, dict):
            raise SystemExit("M141 routeReceiptChecks is missing")
        require_all_true(route_checks.get("translator_xml_custom_data"), "M141 translator_xml_custom_data")

    def legacy_and_adjacent_import_oracles(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M141 evidence is missing")
        route_checks = evidence.get("routeReceiptChecks")
        if not isinstance(route_checks, dict):
            raise SystemExit("M141 routeReceiptChecks is missing")
        require_all_true(route_checks.get("hero_lab_import_oracle"), "M141 hero_lab_import_oracle")

    def dense_builder_and_career(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M142 evidence is missing")
        family_checks = evidence.get("familyChecks")
        receipt_checks = evidence.get("receiptChecks")
        if not isinstance(family_checks, dict) or not isinstance(receipt_checks, dict):
            raise SystemExit("M142 familyChecks or receiptChecks is missing")
        require_all_true(
            family_checks.get("family:dense_builder_and_career_workflows"),
            "M142 family:dense_builder_and_career_workflows",
        )
        if receipt_checks.get("workflow_dense_builder_career_pass") is not True:
            raise SystemExit("M142 workflow_dense_builder_career_pass is not true")

    def dice_initiative_and_table_utilities(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M142 evidence is missing")
        family_checks = evidence.get("familyChecks")
        receipt_checks = evidence.get("receiptChecks")
        if not isinstance(family_checks, dict) or not isinstance(receipt_checks, dict):
            raise SystemExit("M142 familyChecks or receiptChecks is missing")
        require_all_true(
            family_checks.get("family:dice_initiative_and_table_utilities"),
            "M142 family:dice_initiative_and_table_utilities",
        )
        if receipt_checks.get("workflow_initiative_utility_pass") is not True:
            raise SystemExit("M142 workflow_initiative_utility_pass is not true")

    def identity_contacts_lifestyles_history(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M142 evidence is missing")
        family_checks = evidence.get("familyChecks")
        receipt_checks = evidence.get("receiptChecks")
        if not isinstance(family_checks, dict) or not isinstance(receipt_checks, dict):
            raise SystemExit("M142 familyChecks or receiptChecks is missing")
        require_all_true(
            family_checks.get("family:identity_contacts_lifestyles_history"),
            "M142 family:identity_contacts_lifestyles_history",
        )
        if receipt_checks.get("workflow_contacts_lifestyles_notes_pass") is not True:
            raise SystemExit("M142 workflow_contacts_lifestyles_notes_pass is not true")

    def sheet_export_print_viewer_exchange(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M143 evidence is missing")
        route_checks = evidence.get("routeReceiptChecks")
        parity_checks = evidence.get("parityAuditChecks")
        if not isinstance(route_checks, dict) or not isinstance(parity_checks, dict):
            raise SystemExit("M143 routeReceiptChecks or parityAuditChecks is missing")
        require_all_true(route_checks.get("print_export_exchange"), "M143 print_export_exchange")
        for key in (
            "sheet_export_print_viewer_and_exchange_row_present",
            "sheet_export_print_viewer_and_exchange_visual_yes",
            "sheet_export_print_viewer_and_exchange_behavioral_yes",
            "sheet_export_print_viewer_and_exchange_evidence_present",
        ):
            if parity_checks.get(key) is not True:
                raise SystemExit(f"M143 parity audit check is not true: {key}")

    def sr6_supplements_designers_house_rules(payload: dict[str, Any]) -> None:
        evidence = payload.get("evidence")
        if not isinstance(evidence, dict):
            raise SystemExit("M143 evidence is missing")
        route_checks = evidence.get("routeReceiptChecks")
        parity_checks = evidence.get("parityAuditChecks")
        if not isinstance(route_checks, dict) or not isinstance(parity_checks, dict):
            raise SystemExit("M143 routeReceiptChecks or parityAuditChecks is missing")
        require_all_true(route_checks.get("sr6_supplements_and_house_rules"), "M143 sr6_supplements_and_house_rules")
        for key in (
            "sr6_supplements_designers_and_house_rules_row_present",
            "sr6_supplements_designers_and_house_rules_visual_yes",
            "sr6_supplements_designers_and_house_rules_behavioral_yes",
            "sr6_supplements_designers_and_house_rules_evidence_present",
        ):
            if parity_checks.get(key) is not True:
                raise SystemExit(f"M143 parity audit check is not true: {key}")

    return {
        "translator_xml_bridge": (m141, translator_xml_bridge),
        "legacy_and_adjacent_import_oracles": (m141, legacy_and_adjacent_import_oracles),
        "dense_builder_and_career": (m142, dense_builder_and_career),
        "dice_initiative_and_table_utilities": (m142, dice_initiative_and_table_utilities),
        "identity_contacts_lifestyles_history": (m142, identity_contacts_lifestyles_history),
        "sheet_export_print_viewer_exchange": (m143, sheet_export_print_viewer_exchange),
        "sr6_supplements_designers_house_rules": (m143, sr6_supplements_designers_house_rules),
    }


def main() -> int:
    args = parse_args()
    recursive_gate = load_json(PUBLISHED / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json")
    ensure_status("recursive UI event exit gate", recursive_gate)

    specs = runtime_specs()
    family_ids = [args.family.strip()] if args.family else list(specs)
    for family_id in family_ids:
        if family_id not in specs:
            raise SystemExit(f"unknown desktop parity family: {family_id}")
        receipt_path, checker = specs[family_id]
        payload = load_json(receipt_path)
        ensure_status(receipt_path.name, payload)
        checker(payload)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
