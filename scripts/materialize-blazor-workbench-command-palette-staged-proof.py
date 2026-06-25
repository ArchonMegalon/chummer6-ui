#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_command_palette",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench command palette posture",
            "data-workbench-command-palette=\"strip\"",
            "Command palette",
            "data-workbench-command-palette-panel=\"primary\"",
            "aria-keyshortcuts=\"Control+N\"",
            "aria-keyshortcuts=\"Control+O\"",
            "aria-keyshortcuts=\"Control+B\"",
            "aria-keyshortcuts=\"Control+G\"",
            "aria-keyshortcuts=\"Control+S\"",
            "aria-keyshortcuts=\"Control+P\"",
            "aria-keyshortcuts=\"F1\"",
            "data-workbench-command-palette-action=\"support\"",
            "aria-keyshortcuts=\"Shift+F1\"",
            "data-workbench-command-palette-action=\"help\"",
            "href=\"/help\"",
        ],
    },
    {
        "id": "scoped_command_palette_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-command-palette",
            ".browser-workbench-command-copy",
            ".browser-workbench-command-panel",
            ".browser-workbench-command-list",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench command-palette posture",
            "blazor-workbench-command-palette-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench command-palette posture",
            "keyboard-style hints",
            "reload-safe workbench links",
            "same-origin help",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_COMMAND_PALETTE_STAGED_PROOF",
            "workbench_command_palette_staged_status",
            "workbench_command_palette_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    checks = []
    failures = []

    for check in CHECKS:
        path = check["path"]
        tokens = check["tokens"]
        try:
            text = read_text(path)
        except FileNotFoundError:
            failures.append(f"{path}: missing file")
            checks.append({**check, "status": "failed", "missing_tokens": tokens})
            continue

        missing_tokens = [token for token in tokens if token not in text]
        status = "failed" if missing_tokens else "passed"
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": status,
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_command_palette_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and proof-compatible workbench command-palette source, style, status, and docs agree, including the same-origin /help command.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim actual keyboard-event handling, portal help runtime behavior, or command execution runtime behavior.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_command_palette_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
