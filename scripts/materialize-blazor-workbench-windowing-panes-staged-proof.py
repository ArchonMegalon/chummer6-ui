#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_windowing_panes",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client windowing and pane management lane",
            "data-workbench-windowing-panes=\"strip\"",
            "Keep panes easy to arrange.",
            "data-workbench-windowing-panes-action=\"split_view\"",
            "data-workbench-windowing-panes-action=\"popout\"",
            "data-workbench-windowing-panes-action=\"pinned_inspector\"",
            "data-workbench-windowing-panes-action=\"focus_mode\"",
            "data-workbench-windowing-panes-action=\"second_screen\"",
            "data-workbench-windowing-panes-action=\"restore_layout\"",
            "private const string SplitWorkbenchViewCommand = \"split_workbench_view\"",
            "private const string PopoutDetailPaneCommand = \"popout_detail_pane\"",
            "private const string PinInspectorCommand = \"pin_inspector\"",
            "private const string FocusModeCommand = \"focus_mode\"",
            "private const string SecondScreenCommand = \"second_screen\"",
            "private const string RestoreLayoutCommand = \"restore_layout\"",
            "command: SplitWorkbenchViewCommand",
            "command: PopoutDetailPaneCommand",
            "command: PinInspectorCommand",
            "command: FocusModeCommand",
            "command: SecondScreenCommand",
            "command: RestoreLayoutCommand",
            "data-workbench-windowing-panes-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_windowing_panes_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-windowing-panes",
            ".browser-workbench-windowing-panes-copy",
            ".browser-workbench-windowing-panes-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench windowing-panes posture",
            "blazor-workbench-windowing-panes-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench windowing-panes posture",
            "split view, pop-out, pinned inspector, focus mode, second screen, restore layout, and help",
            "not yet claiming multi-window, focus handling, second-screen routing, layout persistence, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_WINDOWING_PANES_STAGED_PROOF",
            "workbench_windowing_panes_staged_status",
            "workbench_windowing_panes_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8")


def evaluate_check(check: dict) -> dict:
    content = read_text(check["path"])
    missing_tokens = [token for token in check["tokens"] if token not in content]
    return {
        "id": check["id"],
        "path": check["path"],
        "status": "passed" if not missing_tokens else "failed",
        "required_token_count": len(check["tokens"]),
        "missing_tokens": missing_tokens,
    }


def main() -> int:
    evaluated_checks = [evaluate_check(check) for check in CHECKS]
    failures = [check for check in evaluated_checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_windowing_panes_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route windowing-panes source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, multi-window proof, focus-handling proof, second-screen proof, layout-persistence proof, or portal help runtime proof.",
            "Do not use this receipt to claim multi-window behavior, focus handling, second-screen routing, layout persistence, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_windowing_panes_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
