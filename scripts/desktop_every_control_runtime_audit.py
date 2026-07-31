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
    certification = load_json(ensure_completion_root() / "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json")

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
            "status": "pass" if is_pass_status(certification) else "fail",
            "evidence": str(ensure_completion_root() / "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json"),
            "summary": certification.get("summary"),
        },
    ]

    blocking_findings = [
        f"{row['auditArea']} is not passing"
        for row in rows
        if row.get("status") != "pass"
    ]
    status = "pass" if rows and not blocking_findings else "fail"

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.desktop_every_control_runtime_audit",
        "scope": "windows_linux_public_release_only",
        "status": status,
        "summary": (
            "Desktop control wiring is proven by inventory, workflow, recursive-route, and row-level control certification receipts for the active Windows/Linux public heads."
            if status == "pass"
            else "Desktop every-control runtime certification is blocked by one or more failing upstream control audits."
        ),
        "controlAuditRows": rows,
        "blockingFindings": blocking_findings,
        "evidence": {
            "interactiveControlInventory": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
            "visibleControlCertification": str(ensure_completion_root() / "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json"),
            "recursiveUiEventExitGate": str(PUBLISHED / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json"),
            "desktopWorkflowExecutionGate": str(PUBLISHED / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"),
            "inventoryFailureCount": interactive.get("evidence", {}).get("failureCount"),
            "inventoryReasonCount": interactive.get("evidence", {}).get("reasonCount"),
            "rowLevelCertificationCount": certification.get("rowCount"),
        },
        "allowedClaim": "Windows/Linux desktop control coverage is release-ready for the active public heads.",
        "disallowedClaim": "Every visible control across every platform and every hypothetical display topology is globally certified.",
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
