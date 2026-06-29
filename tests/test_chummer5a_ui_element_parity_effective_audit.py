from __future__ import annotations

import importlib.util
import json
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "ai" / "materialize_chummer5a_ui_element_parity_effective_audit.py"
spec = importlib.util.spec_from_file_location("effective_audit", MODULE_PATH)
assert spec and spec.loader
effective_audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(effective_audit)


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def test_standard_verify_materializes_effective_ui_audit_before_direct_guards() -> None:
    verify_script = (Path(__file__).resolve().parents[1] / "scripts" / "ai" / "verify.sh").read_text(
        encoding="utf-8"
    )

    materializer_index = verify_script.index("materialize_chummer5a_ui_element_parity_effective_audit.py")
    m142_index = verify_script.index("next90-m142-ui-direct-workflow-proof-check.sh")
    m143_index = verify_script.index("next90-m143-ui-direct-output-proof-check.sh")

    assert materializer_index < m142_index
    assert materializer_index < m143_index


def test_route_local_rows_reconcile_only_when_flagship_and_inventory_pass(tmp_path, monkeypatch) -> None:
    published = tmp_path / ".codex-studio" / "published"
    audit_path = published / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
    markdown_path = published / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.md"
    flagship_path = published / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
    inventory_path = published / "PARITY_INVENTORY.generated.json"

    monkeypatch.setattr(effective_audit, "PUBLISHED_ROOT", published)
    monkeypatch.setattr(effective_audit, "AUDIT_PATH", audit_path)
    monkeypatch.setattr(effective_audit, "AUDIT_MARKDOWN_PATH", markdown_path)
    monkeypatch.setattr(effective_audit, "FLAGSHIP_PATH", flagship_path)
    monkeypatch.setattr(effective_audit, "PARITY_INVENTORY_PATH", inventory_path)

    rows = [
        {
            "id": "family:dice_initiative_and_table_utilities",
            "label": "Dice Initiative And Table Utilities",
            "category": "workflow_family",
            "visual_parity": "no",
            "behavioral_parity": "no",
            "present_in_chummer5a": "yes",
            "present_in_chummer6": "yes",
            "removable_if_not_in_chummer5a": "no",
            "reason": "old gap",
            "evidence": ["old.json"],
        },
        {
            "id": "family:unproven",
            "label": "Unproven",
            "category": "workflow_family",
            "visual_parity": "no",
            "behavioral_parity": "no",
            "present_in_chummer5a": "yes",
            "present_in_chummer6": "yes",
            "removable_if_not_in_chummer5a": "no",
            "reason": "still missing",
            "evidence": [],
        },
    ]
    write_json(
        audit_path,
        {
            "status": "fail",
            "summary": {
                "total_elements": 2,
                "visual_yes_count": 0,
                "visual_no_count": 2,
                "behavioral_yes_count": 0,
                "behavioral_no_count": 2,
                "coverage_gap_keys": ["desktop_client"],
            },
            "visualNoCount": 2,
            "behavioralNoCount": 2,
            "releaseBlockingNoCount": 4,
            "coverageGapKeys": ["desktop_client"],
            "findings": [
                {
                    "severity": "high",
                    "category": "ui_parity_gap",
                    "summary": "Dice Initiative And Table Utilities is not directly parity-proven.",
                    "detail": "family:dice_initiative_and_table_utilities",
                },
                {
                    "severity": "high",
                    "category": "ui_parity_gap",
                    "summary": "Unproven is not directly parity-proven.",
                    "detail": "family:unproven",
                },
            ],
            "rows": rows,
            "elements": rows,
        },
    )
    write_json(
        flagship_path,
        {
            "status": "pass",
            "uiElementParityAuditProof": {
                "effectiveStatus": "pass",
                "routeLocalRowProofs": {
                    "family:dice_initiative_and_table_utilities": True,
                    "family:unproven": True,
                },
                "directWorkflowRouteProofReceiptPath": "/proof/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json",
            },
        },
    )
    write_json(
        inventory_path,
        {
            "items": [
                {
                    "id": "family:dice_initiative_and_table_utilities",
                    "current_status": "pass",
                    "expected_behavior": "Route-local dice proof is closed.",
                    "oracle_source": [
                        "/proof/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
                        "/docker/chummercomplete/chummer-core-engine/docs/NEXT90_M142_DENSE_WORKBENCH_RECEIPTS.md",
                    ],
                },
                {
                    "id": "family:unproven",
                    "current_status": "fail",
                    "expected_behavior": "Do not green this row.",
                    "oracle_source": [],
                },
            ]
        },
    )

    assert effective_audit.main() == 1

    payload = json.loads(audit_path.read_text(encoding="utf-8"))
    by_id = {row["id"]: row for row in payload["rows"]}
    assert by_id["family:dice_initiative_and_table_utilities"]["visual_parity"] == "yes"
    assert by_id["family:dice_initiative_and_table_utilities"]["behavioral_parity"] == "yes"
    assert by_id["family:dice_initiative_and_table_utilities"]["reason"] == "Route-local dice proof is closed."
    assert all(
        "NEXT90_M142_DENSE_WORKBENCH_RECEIPTS.md" not in item
        for item in by_id["family:dice_initiative_and_table_utilities"]["evidence"]
    )
    assert by_id["family:unproven"]["visual_parity"] == "no"
    assert payload["summary"]["visual_no_count"] == 1
    assert payload["summary"]["behavioral_no_count"] == 1
    assert "desktop_client" in payload["coverageGapKeys"]
    assert len(payload["findings"]) == 1
    assert payload["findings"][0]["detail"] == "family:unproven"


def test_all_route_local_gaps_clear_status_and_coverage(tmp_path, monkeypatch) -> None:
    published = tmp_path / ".codex-studio" / "published"
    audit_path = published / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
    markdown_path = published / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.md"
    flagship_path = published / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
    inventory_path = published / "PARITY_INVENTORY.generated.json"

    monkeypatch.setattr(effective_audit, "PUBLISHED_ROOT", published)
    monkeypatch.setattr(effective_audit, "AUDIT_PATH", audit_path)
    monkeypatch.setattr(effective_audit, "AUDIT_MARKDOWN_PATH", markdown_path)
    monkeypatch.setattr(effective_audit, "FLAGSHIP_PATH", flagship_path)
    monkeypatch.setattr(effective_audit, "PARITY_INVENTORY_PATH", inventory_path)

    rows = [
        {
            "id": "family:sheet_export_print_viewer_and_exchange",
            "label": "Sheet Export Print Viewer And Exchange",
            "category": "workflow_family",
            "visual_parity": "no",
            "behavioral_parity": "no",
            "present_in_chummer5a": "yes",
            "present_in_chummer6": "yes",
            "removable_if_not_in_chummer5a": "no",
            "reason": "old gap",
            "evidence": [],
        }
    ]
    write_json(
        audit_path,
        {
            "status": "fail",
            "summary": {
                "total_elements": 1,
                "visual_yes_count": 0,
                "visual_no_count": 1,
                "behavioral_yes_count": 0,
                "behavioral_no_count": 1,
                "coverage_gap_keys": ["desktop_client"],
            },
            "findings": [{"summary": "Sheet Export Print Viewer And Exchange is not directly parity-proven."}],
            "rows": rows,
            "elements": rows,
        },
    )
    write_json(
        flagship_path,
        {
            "status": "pass",
            "uiElementParityAuditProof": {
                "effectiveStatus": "pass",
                "routeLocalRowProofs": {"family:sheet_export_print_viewer_and_exchange": True},
                "directOutputRouteProofReceiptPath": "/proof/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json",
            },
        },
    )
    write_json(
        inventory_path,
        {
            "items": [
                {
                    "id": "family:sheet_export_print_viewer_and_exchange",
                    "current_status": "pass",
                    "expected_behavior": "Route-local print/export proof is closed.",
                    "oracle_source": ["/proof/SECTION_HOST_RULESET_PARITY.generated.json"],
                }
            ]
        },
    )

    assert effective_audit.main() == 0
    payload = json.loads(audit_path.read_text(encoding="utf-8"))
    assert payload["status"] == "pass"
    assert payload["summary"]["visual_no_count"] == 0
    assert payload["summary"]["behavioral_no_count"] == 0
    assert payload["coverageGapKeys"] == []
    assert payload["findings"] == []
    assert "Route-local print/export proof is closed." in markdown_path.read_text(encoding="utf-8")
