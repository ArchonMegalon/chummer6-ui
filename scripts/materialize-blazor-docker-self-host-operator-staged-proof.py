#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json",
    )
)

EXPECTED_SERVICES = [
    "chummer-api",
    "chummer-blazor-portal",
    "chummer-hub-web-portal",
    "chummer-avalonia-browser",
    "chummer-portal",
]

CHECKS = [
    {
        "id": "portal_e2e_runtime_script",
        "path": "scripts/e2e-portal.sh",
        "tokens": [
            "PORTAL_COMPOSE_PROFILE",
            "portal",
            "PORTAL_EDGE_SERVICES",
            "chummer-api chummer-blazor-portal chummer-hub-web-portal chummer-avalonia-browser chummer-portal",
            "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
            "CHUMMER_PORTAL_BASE_URL",
        ],
    },
    {
        "id": "docker_compose_portal_profile",
        "path": "docker-compose.yml",
        "tokens": EXPECTED_SERVICES + [
            "CHUMMER_BLAZOR_PATH_BASE",
            "/blazor",
        ],
    },
    {
        "id": "self_host_runbook",
        "path": "docs/BLAZOR_SELF_HOST_RUNBOOK.md",
        "tokens": [
            "Docker",
            "/blazor/workbench",
            "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
        ],
    },
    {
        "id": "operator_contract_doc",
        "path": "docs/BLAZOR_DOCKER_SELF_HOST_OPERATOR_PROOF.md",
        "tokens": EXPECTED_SERVICES + [
            "Raw Blazor hosting is not the product shape for self-host users.",
            "bash scripts/e2e-portal.sh",
            "source-only and not runtime evidence",
        ],
    },
    {
        "id": "status_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF",
            "docker_self_host_operator_staged_status",
            "source_alignment_only_not_docker_runtime",
        ],
    },
    {
        "id": "docs_index",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "BLAZOR_DOCKER_SELF_HOST_OPERATOR_PROOF.md",
            "self-hosted Docker",
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
        "contract_name": "chummer6-ui.blazor_docker_self_host_operator_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_docker_runtime",
        "route_lane": "portal_backed_blazor_workbench_self_host",
        "expected_services": EXPECTED_SERVICES,
        "expected_path_base": "/blazor",
        "expected_workbench_route": "/blazor/workbench",
        "runtime_proof_receipt": ".codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
        "checks": checks,
        "failures": failures,
        "notes": [
            "This proof is source alignment only for Docker self-host operator posture.",
            "It does not start Docker, probe routes, or prove browser rendering.",
            "Runtime evidence remains BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json from scripts/e2e-portal.sh.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_docker_self_host_operator_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
