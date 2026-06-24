#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_touch_mobile",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench touch and mobile ergonomics lane",
            "data-workbench-touch-mobile=\"strip\"",
            "Make dense sheets usable on touch screens.",
            "data-workbench-touch-mobile-action=\"touch_mode\"",
            "data-workbench-touch-mobile-action=\"zoom\"",
            "data-workbench-touch-mobile-action=\"panel_dock\"",
            "data-workbench-touch-mobile-action=\"compact_actions\"",
            "data-workbench-touch-mobile-action=\"keyboard_safe\"",
            "data-workbench-touch-mobile-action=\"pointer_help\"",
        ],
    },
    {
        "id": "scoped_touch_mobile_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-touch-mobile",
            ".browser-workbench-touch-mobile-copy",
            ".browser-workbench-touch-mobile-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench touch-mobile posture",
            "blazor-workbench-touch-mobile-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench touch-mobile posture",
            "touch mode, zoom, panel dock, compact actions, keyboard-safe layout, and pointer help",
            "not yet claiming touch gesture, viewport, virtual-keyboard, or mobile browser parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_TOUCH_MOBILE_STAGED_PROOF",
            "workbench_touch_mobile_staged_status",
            "workbench_touch_mobile_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_touch_mobile_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that promoted workbench touch-mobile source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, touch-gesture proof, viewport proof, virtual-keyboard proof, or mobile browser proof.",
            "Do not use this receipt to claim touch gestures, viewport behavior, virtual keyboard handling, mobile browser execution, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_touch_mobile_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
