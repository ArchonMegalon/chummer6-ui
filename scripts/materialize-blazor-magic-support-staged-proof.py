#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_MAGIC_SUPPORT_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/blazor/workbench?workspace=ws-1&tab=tab-adept&control=adept_power_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=spirit_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-critter&control=critter_power_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=matrix_program_add",
]

CHECKS = [
    {
        "id": "product_workbench_affordances",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Add adept power for @PrimaryRecentWorkspace.ShortLabel",
            "Add spirit for @PrimaryRecentWorkspace.ShortLabel",
            "Add critter power for @PrimaryRecentWorkspace.ShortLabel",
            "Add Matrix program for @PrimaryRecentWorkspace.ShortLabel",
        ],
    },
    {
        "id": "shared_control_catalog",
        "path": "Chummer.Presentation/Overview/LegacyUiControlCatalog.cs",
        "tokens": [
            "adept_power_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
        ],
    },
    {
        "id": "desktop_shaped_dialogs",
        "path": "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
        "tokens": [
            "adept_power_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
            "Power",
            "Spirit",
            "Critter",
            "Program",
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
            "promoted_magic_support_execution",
            "magic_resonance_support_utility_execution",
            "adept_power_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
        ],
    },
    {
        "id": "self_host_playwright_runner",
        "path": "scripts/e2e-portal-playwright.cjs",
        "tokens": [
            "auditPortalRestoredMagicSupportRoute",
            "adept_power_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
        ],
    },
    {
        "id": "self_host_receipt_metadata",
        "path": "scripts/e2e-portal.sh",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "magic/resonance support utility posture",
            "blazor-magic-support-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted magic/resonance support posture",
            "adept_power_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "MAGIC_SUPPORT_STAGED_PROOF",
            "magic_support_staged_status",
            "magic_support_staged_route_count",
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
        "contract_name": "chummer6-ui.blazor_magic_support_staged_proof",
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
            "Do not use this receipt to claim magic/resonance workflow parity on chummer.run.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_magic_support_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
