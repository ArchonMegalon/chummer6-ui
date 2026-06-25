#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/downloads/",
    "/status",
    "/contact",
    "/help",
    "/account/work",
    "/blazor/home",
    "/blazor/app",
    "/blazor/workbench",
]

CHECKS = [
    {
        "id": "product_workbench_affordances",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client portal handoff",
            "Open desktop downloads",
            "Check current release status",
            "Open product support",
            "Open portal help",
            "Continue account work",
            "href=\"@DownloadsHref\"",
            "private const string DownloadsHref = \"/downloads/\"",
            "href=\"@StatusHref\"",
            "private const string StatusHref = \"/status\"",
            "href=\"@ContactHref\"",
            "private const string ContactHref = \"/contact\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
            "href=\"@AccountWorkHref\"",
            "private const string AccountWorkHref = \"/account/work\"",
            "/blazor/home",
            "/blazor/app",
        ],
    },
    {
        "id": "hosted_public_edge_route_probe",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": [
            "/downloads/",
            "/status",
            "/contact",
            "/help",
            "/hub",
            "Product bug",
            "Current release",
            "Download the current Windows installer.",
        ],
    },
    {
        "id": "portal_installer_contract",
        "path": "docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
        "tokens": [
            "/downloads/",
            "/contact",
            "/status",
            "/help",
            "/blazor/home",
            "/blazor/app",
            "/blazor/workbench",
            "same-origin through `Chummer.Portal`",
        ],
    },
    {
        "id": "account_support_contract",
        "path": "docs/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md",
        "tokens": [
            "/account/work",
            "/contact",
            "/status",
            "/help",
            "/blazor/home",
            "/blazor/app",
            "/blazor/workbench",
            "same-origin through `Chummer.Portal`",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench portal handoff posture",
            "blazor-workbench-portal-handoff-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench portal handoff posture",
            "/downloads/",
            "/status",
            "/contact",
            "/help",
            "/account/work",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF",
            "workbench_portal_handoff_staged_status",
            "workbench_portal_handoff_staged_route_count",
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
        "contract_name": "chummer6-ui.blazor_workbench_portal_handoff_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "portal_backed_blazor_workbench_handoff",
        "expected_routes": EXPECTED_ROUTES,
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that source, route-probe, and documentation staging agree.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim account, support, installer, or portal route runtime behavior.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_portal_handoff_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
