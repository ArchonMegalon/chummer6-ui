#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

from desktop_hardware_wide_common import (
    PUBLISHED,
    ensure_completion_root,
    is_pass_status,
    load_json,
    utc_now,
    write_json,
)


OUTPUT = "DESKTOP_EVERY_CONTROL_RUNTIME_AUDIT.generated.json"


def main() -> int:
    interactive = load_json(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json")
    recursive = load_json(PUBLISHED / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json")
    workflow = load_json(PUBLISHED / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json")

    rows = [
        {
            "auditArea": "standalone_interactive_controls",
            "status": "pass" if is_pass_status(interactive) else "fail",
            "evidence": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "summary": interactive.get("standaloneControlReview", {}).get("summary"),
        },
        {
            "auditArea": "main_window_interaction_routes",
            "status": "pass" if is_pass_status(interactive) else "fail",
            "evidence": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "summary": interactive.get("mainWindowInteractionReview", {}).get("summary"),
        },
        {
            "auditArea": "keyboard_shortcuts_and_accessible_labels",
            "status": "pass" if is_pass_status(interactive) else "fail",
            "evidence": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "summary": interactive.get("keyboardAndTooltipReview", {}).get("summary"),
        },
        {
            "auditArea": "recursive_runtime_routes_and_generated_dialogs",
            "status": "pass" if is_pass_status(recursive) else "fail",
            "evidence": str(PUBLISHED / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json"),
            "summary": recursive.get("summary"),
        },
        {
            "auditArea": "workflow_click_through_families",
            "status": "pass" if is_pass_status(workflow) else "fail",
            "evidence": str(PUBLISHED / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"),
            "summary": workflow.get("summary"),
        },
        {
            "auditArea": "row_level_every_visible_control_certification",
            "status": "missing",
            "evidence": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "summary": "Current receipts prove strong inventories and workflow execution, but they do not enumerate one explicit row for every visible control in every runtime state, dialog, context menu, and disabled posture.",
        },
    ]

    blocking_findings = [
        "No fully recursive row-level artifact exists yet for every visible control across all loaded desktop states.",
        "Disabled-control explanation proof is strong in targeted lanes, but not globally enumerated as a row-per-control certification bundle.",
        "Context-menu and generated-dialog coverage is grounded by route inventories and workflow receipts, but not flattened into a single exhaustive table.",
    ]

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.desktop_every_control_runtime_audit",
        "status": "not_ready",
        "summary": "Desktop control wiring is strongly proven by inventory and workflow receipts, but hardware-wide gold still lacks a row-level every-control certification artifact.",
        "controlAuditRows": rows,
        "blockingFindings": blocking_findings,
        "evidence": {
            "interactiveControlInventory": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "recursiveUiEventExitGate": str(PUBLISHED / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json"),
            "desktopWorkflowExecutionGate": str(PUBLISHED / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"),
            "inventoryFailureCount": interactive.get("evidence", {}).get("failureCount"),
            "inventoryReasonCount": interactive.get("evidence", {}).get("reasonCount"),
        },
        "allowedClaim": "Strong desktop control receipts exist.",
        "disallowedClaim": "Every visible control across every runtime state is globally row-level certified.",
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
