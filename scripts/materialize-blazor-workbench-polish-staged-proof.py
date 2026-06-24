#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_POLISH_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_task_dock",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Promoted workbench task dock",
            "data-workbench-polish=\"task-dock\"",
            "Desktop-style task dock",
            "data-workbench-dock-action=\"start-new\"",
            "data-workbench-dock-action=\"open-existing\"",
            "data-workbench-dock-action=\"build-lab\"",
            "data-workbench-dock-action=\"gear\"",
            "data-workbench-dock-action=\"save-as\"",
            "data-workbench-dock-action=\"export\"",
            "data-workbench-dock-action=\"print\"",
            "data-workbench-dock-action=\"downloads\"",
            "data-workbench-dock-action=\"support\"",
        ],
    },
    {
        "id": "scoped_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-task-dock",
            ".browser-workbench-task-dock-copy",
            ".browser-workbench-task-dock-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench polish posture",
            "blazor-workbench-polish-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench polish posture",
            "Desktop-style task dock",
            "start, edit, output, and portal handoff",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_POLISH_STAGED_PROOF",
            "workbench_polish_staged_status",
            "workbench_polish_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_polish_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that promoted workbench polish source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim desktop-equivalent browser workflow parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_polish_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
