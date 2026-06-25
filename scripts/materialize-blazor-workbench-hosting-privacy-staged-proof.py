#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_hosting_privacy_strip",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench hosting and privacy posture",
            "Chummer App hosting and privacy posture",
            "data-workbench-hosting=\"strip\"",
            "Hosting and privacy",
            "HostedRouteLabel",
            "/blazor/app",
            "/blazor/workbench",
            "data-workbench-hosting-card=\"public-edge\"",
            "data-workbench-hosting-card=\"docker-self-host\"",
            "data-workbench-hosting-card=\"analytics-privacy\"",
            "Rybbit is optional and metadata-only",
            "CHUMMER_ANALYTICS_PROVIDER=none",
            "self-host defaults stay off unless the operator opts into Rybbit",
            "must not send character, owner, runner file, XML, or dossier content",
        ],
    },
    {
        "id": "scoped_hosting_privacy_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-hosting-strip",
            ".browser-workbench-hosting-copy",
            ".browser-workbench-hosting-cards",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench hosting/privacy posture",
            "blazor-workbench-hosting-privacy-staged-proof-check.sh",
            "self-host default-off Rybbit boundary",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "contract_doc",
        "path": "docs/BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.md",
        "tokens": [
            "hosted route posture for clean public `/app`, hosted `/blazor/app`, and proof-compatible `/blazor/workbench`",
            "self-host default-off analytics copy, including `CHUMMER_ANALYTICS_PROVIDER=none`",
            "status reporting note `source_alignment_only_default_off_rybbit_not_browser_execution`",
            "default-off Rybbit boundary for self-hosted deployments",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench hosting/privacy posture",
            "Rybbit is optional and metadata-only",
            "CHUMMER_ANALYTICS_PROVIDER=none",
            "hosted route, Docker self-host, and analytics privacy",
        ],
    },
    {
        "id": "docs_index_visibility",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "Hosted `chummer.run` may enable the Rybbit adapter for the Blazor web client",
            "self-hosted Docker defaults keep analytics disabled with `CHUMMER_ANALYTICS_PROVIDER=none`",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF",
            "workbench_hosting_privacy_staged_status",
            "workbench_hosting_privacy_staged_source_checks",
            "source_alignment_only_default_off_rybbit_not_browser_execution",
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
        "contract_name": "chummer6-ui.blazor_workbench_hosting_privacy_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/app", "/blazor/workbench"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and proof-compatible workbench hosting/privacy source, style, status, and docs agree, including the self-host default-off Rybbit boundary.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim Docker runtime, hosted route availability, Rybbit service health, or analytics delivery.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_hosting_privacy_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
