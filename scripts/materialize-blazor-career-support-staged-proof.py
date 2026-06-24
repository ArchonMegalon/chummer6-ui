#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_CAREER_SUPPORT_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry",
    "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete",
]

CHECKS = [
    {
        "id": "product_workbench_affordances",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Resume @PrimaryRecentWorkspace.ShortLabel on career log",
            "Add and keep career entry",
            "Add career entry",
            "Apply career entry edit",
            "Edit career entry",
            "Remove and keep career entry result",
            "Remove career entry",
        ],
    },
    {
        "id": "hosted_route_entry_probe",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "hosted_execution_runner",
        "path": "scripts/e2e-public-edge-playwright.cjs",
        "tokens": [
            "promoted_career_log_continuity",
            "promoted_career_entry_execution",
            "promoted_career_entry_committed_execution",
            "promoted_career_entry_edit_execution",
            "promoted_career_entry_delete_execution",
            "promoted_career_entry_edit_committed_execution",
            "promoted_career_entry_delete_committed_execution",
            "Entry 'New entry' added.",
            "Entry renamed to 'Current Entry'.",
            "Entry 'Current Entry' removed.",
        ],
    },
    {
        "id": "self_host_playwright_runner",
        "path": "scripts/e2e-portal-playwright.cjs",
        "tokens": [
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry",
            "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete",
            "Entry 'New entry' added.",
            "Entry renamed to 'Current Entry'.",
            "Entry 'Current Entry' removed.",
        ],
    },
    {
        "id": "self_host_receipt_metadata",
        "path": "scripts/e2e-portal.sh",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "operator_docs",
        "path": "docs/BLAZOR_SELF_HOST_RUNBOOK.md",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "career/support workflow family",
            "not yet current published receipt evidence",
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
        "contract_name": "chummer6-ui.blazor_career_support_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": EXPECTED_ROUTES,
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that source, runner, metadata, and documentation staging agree.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim the career/support browser workflow has passed on chummer.run.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_career_support_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
