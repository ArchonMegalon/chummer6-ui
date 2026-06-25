#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_docker_operator",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Workbench Docker self-host operator lane",
            "data-workbench-docker-operator=\"strip\"",
            "Keep Docker operations visible.",
            "data-workbench-docker-operator-action=\"container_health\"",
            "data-workbench-docker-operator-action=\"env_check\"",
            "data-workbench-docker-operator-action=\"volume_mounts\"",
            "data-workbench-docker-operator-action=\"backup\"",
            "data-workbench-docker-operator-action=\"image_update\"",
            "data-workbench-docker-operator-action=\"support_bundle\"",
            "data-workbench-docker-operator-action=\"help\"",
            "/help",
        ],
    },
    {
        "id": "scoped_docker_operator_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-docker-operator",
            ".browser-workbench-docker-operator-copy",
            ".browser-workbench-docker-operator-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench Docker-operator posture",
            "blazor-workbench-docker-operator-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench Docker-operator posture",
            "container health, env check, volume mounts, backup, image update, support bundle, and help",
            "not yet claiming live container inspection, env validation, backup, image update, log-bundle, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF",
            "workbench_docker_operator_staged_status",
            "workbench_docker_operator_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_docker_operator_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and proof-compatible workbench Docker-operator source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, live container-inspection proof, env-validation proof, backup proof, image-update proof, log-bundle proof, or portal help runtime proof.",
            "Do not use this receipt to claim live container inspection, env validation, backup, image update, log bundle, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_docker_operator_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
