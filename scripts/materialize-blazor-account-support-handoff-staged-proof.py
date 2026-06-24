#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/hub",
    "/hub/",
    "/contact",
    "/status",
    "/home/access",
    "/home/work",
    "/account/work",
    "/account/support",
    "/blazor/workbench",
]

CHECKS = [
    {
        "id": "hosted_public_edge_account_support_routes",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": [
            "/hub",
            "/hub/",
            "/contact",
            "/status",
            "Sign in",
            "Product bug",
            "Current release",
        ],
    },
    {
        "id": "self_host_portal_account_support_routes",
        "path": "scripts/e2e-portal.sh",
        "tokens": [
            "/home/access",
            "/home/work",
            "/account/work",
            "/account/support",
            "/contact",
            "signed owner propagation enabled",
        ],
    },
    {
        "id": "portal_playwright_account_context",
        "path": "scripts/e2e-portal-playwright.cjs",
        "tokens": [
            "implicit self-host sign-in",
            "signed owner propagation enabled",
            "portal home",
        ],
    },
    {
        "id": "account_support_contract_doc",
        "path": "docs/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md",
        "tokens": EXPECTED_ROUTES + [
            "source-only",
            "same-origin through `Chummer.Portal`",
            "must not be treated as authentication or authorization runtime proof",
        ],
    },
    {
        "id": "web_client_parity_goal",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "account/owner context",
            "support",
            "same portal origin",
        ],
    },
    {
        "id": "release_signoff_boundary",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md",
            "source-staged",
            "not authentication or support runtime proof",
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
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": "failed" if missing_tokens else "passed",
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_account_support_handoff_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_auth_or_support_runtime",
        "route_lane": "portal_backed_blazor_account_support_handoff",
        "expected_routes": EXPECTED_ROUTES,
        "checks": checks,
        "failures": failures,
        "notes": [
            "This proof is source alignment only for account/support handoff posture.",
            "It does not prove authentication, authorization, owner propagation, or support submission runtime behavior.",
            "Runtime evidence remains owned by local portal proof, hosted route-entry proof, hosted execution proof, and connected-runtime posture receipts.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_account_support_handoff_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
