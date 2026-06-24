#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_RUNTIME_PROOF_REFRESH_PLAN_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
    )
)

REQUIRED_COMMAND_SOURCES = [
    "scripts/ai/milestones/blazor-source-staged-proof-set-check.sh",
    "scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh",
    "scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh",
    "scripts/ai/milestones/blazor-docker-self-host-operator-staged-proof-check.sh",
    "scripts/ai/milestones/blazor-account-support-handoff-staged-proof-check.sh",
    "scripts/e2e-portal.sh",
    "scripts/e2e-public-edge.cjs",
    "scripts/e2e-public-edge-playwright.cjs",
    "scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh",
]

REQUIRED_DOC_TOKENS = {
    "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md": [
        "Source-Staged Family Set",
        "Source-Staged Release Boundary",
        "Docker Self-Host Runtime Proof",
        "Hosted Public-Edge Route Proof",
        "Hosted Public-Edge Execution Proof",
        "Browser-Lane Aggregate",
        "Do not use source-staged receipts as release evidence.",
    ],
    "docs/WORKBENCH_RELEASE_SIGNOFF.md": [
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md",
        "runtime proof refresh",
    ],
    "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md": [
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md",
        "runtime proof refresh",
    ],
}


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    failures: list[str] = []
    command_rows = []

    for relative_path in REQUIRED_COMMAND_SOURCES:
        path = REPO_ROOT / relative_path
        exists = path.is_file()
        executable = os.access(path, os.X_OK) if exists and relative_path.endswith(".sh") else None
        if not exists:
            failures.append(f"missing command source: {relative_path}")
        elif relative_path.endswith(".sh") and executable is False:
            failures.append(f"command source is not executable: {relative_path}")
        command_rows.append(
            {
                "path": relative_path,
                "exists": exists,
                "executable": executable,
            }
        )

    doc_rows = []
    for relative_path, tokens in REQUIRED_DOC_TOKENS.items():
        try:
            text = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"missing doc source: {relative_path}")
            doc_rows.append({"path": relative_path, "status": "missing", "missing_tokens": tokens})
            continue

        missing_tokens = [token for token in tokens if token not in text]
        if missing_tokens:
            failures.append(f"{relative_path}: missing {', '.join(missing_tokens)}")
        doc_rows.append(
            {
                "path": relative_path,
                "status": "failed" if missing_tokens else "passed",
                "missing_tokens": missing_tokens,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_runtime_proof_refresh_plan",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_plan_no_browser_execution",
        "scope": "ordered_refresh_plan_for_source_staged_to_runtime_browser_proof",
        "command_sources": command_rows,
        "documentation_sources": doc_rows,
        "failures": failures,
        "notes": [
            "This receipt proves only that the proof refresh plan is documented and command sources exist.",
            "It does not execute Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof.",
            "Runtime proof remains owned by the receipts produced by those commands when explicitly run.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_runtime_proof_refresh_plan:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
