#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_keyboard_shortcuts",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench keyboard shortcuts",
            "data-workbench-keyboard-shortcuts=\"strip\"",
            "Keep keyboard help and accelerator intent visible.",
            "data-workbench-keyboard-shortcuts-action=\"command-help\"",
            "data-workbench-keyboard-shortcuts-action=\"save-output\"",
            "data-workbench-keyboard-shortcuts-action=\"section-jump\"",
            "data-workbench-keyboard-shortcuts-action=\"density-toggle\"",
            "data-workbench-keyboard-shortcuts-action=\"help\"",
            "/help",
            "data-workbench-keyboard-shortcuts-action=\"support-escape\"",
        ],
    },
    {
        "id": "scoped_keyboard_shortcuts_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-keyboard-shortcuts",
            ".browser-workbench-keyboard-shortcuts-copy",
            ".browser-workbench-keyboard-shortcuts-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench keyboard-shortcuts posture",
            "blazor-workbench-keyboard-shortcuts-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench keyboard-shortcuts posture",
            "command help, save/output, section jump, density toggle, help, and support escape",
            "not yet claiming key-event handling, portal help runtime, or accelerator execution parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF",
            "workbench_keyboard_shortcuts_staged_status",
            "workbench_keyboard_shortcuts_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_keyboard_shortcuts_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help", "/contact"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and proof-compatible workbench keyboard-shortcuts source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, key-event proof, portal help runtime, or accelerator execution proof.",
            "Do not use this receipt to claim key-event handling, portal help runtime, accelerator execution, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_keyboard_shortcuts_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
