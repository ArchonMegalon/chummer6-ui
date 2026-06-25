#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_connected_runtime",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client live lanes",
            "data-workbench-connected-runtime=\"strip\"",
            "Live lanes",
            "Use connected tools when they are available.",
            "Play, session, coach, assistant, and status links",
            "data-workbench-connected-runtime-action=\"play-session\"",
            "data-workbench-connected-runtime-action=\"session-runtime\"",
            "data-workbench-connected-runtime-action=\"coach-runtime\"",
            "data-workbench-connected-runtime-action=\"auto-alice\"",
            "data-workbench-connected-runtime-action=\"runtime-status\"",
        ],
    },
    {
        "id": "scoped_connected_runtime_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-connected-runtime",
            ".browser-workbench-connected-runtime-copy",
            ".browser-workbench-connected-runtime-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "connected_runtime_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "Connected runtime proof is deliberately narrower than workflow parity.",
            "session, coach, and assistant lanes",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench connected-runtime posture",
            "blazor-workbench-connected-runtime-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench connected-runtime posture",
            "play, session, coach, assistant, and status links",
            "not yet claiming connected-runtime execution, signed owner forwarding, or downstream service health",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF",
            "workbench_connected_runtime_staged_status",
            "workbench_connected_runtime_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_connected_runtime_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "public_chummer_app_and_promoted_blazor_workbench",
        "expected_routes": ["/blazor/app", "/blazor/workbench", "/play", "/session/", "/coach/", "/status"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that public Chummer Online/workbench connected-runtime source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, or connected-runtime posture proof.",
            "Do not use this receipt to claim connected-runtime execution, signed owner forwarding, or downstream service health.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_connected_runtime_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
