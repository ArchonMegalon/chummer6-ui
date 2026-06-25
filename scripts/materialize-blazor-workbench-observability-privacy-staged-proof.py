#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_observability_privacy",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client observability and privacy lane",
            "data-workbench-observability-privacy=\"strip\"",
            "Make analytics privacy explicit for Chummer Run.",
            "private const string AnalyticsConsentCommand = \"analytics_consent\"",
            "private const string AnalyticsStatusCommand = \"analytics_status\"",
            "private const string RouteEventAuditCommand = \"route_event_audit\"",
            "private const string ErrorTraceReviewCommand = \"error_trace_review\"",
            "private const string PrivacyLogCommand = \"privacy_log\"",
            "private const string SelfHostTelemetryCommand = \"self_host_telemetry\"",
            "command: AnalyticsConsentCommand",
            "command: AnalyticsStatusCommand",
            "command: RouteEventAuditCommand",
            "command: ErrorTraceReviewCommand",
            "command: PrivacyLogCommand",
            "command: SelfHostTelemetryCommand",
            "data-workbench-observability-privacy-action=\"consent\"",
            "data-workbench-observability-privacy-action=\"analytics_status\"",
            "data-workbench-observability-privacy-action=\"route_events\"",
            "data-workbench-observability-privacy-action=\"error_traces\"",
            "data-workbench-observability-privacy-action=\"privacy_log\"",
            "data-workbench-observability-privacy-action=\"self_host_toggle\"",
            "data-workbench-observability-privacy-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_observability_privacy_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-observability-privacy",
            ".browser-workbench-observability-privacy-copy",
            ".browser-workbench-observability-privacy-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench observability-privacy posture",
            "blazor-workbench-observability-privacy-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench observability-privacy posture",
            "consent, Rybbit status, route events, error traces, privacy log, self-host telemetry toggle, and help",
            "not yet claiming Rybbit deployment, event delivery, consent persistence, telemetry runtime, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF",
            "workbench_observability_privacy_staged_status",
            "workbench_observability_privacy_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_observability_privacy_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route observability-privacy source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, Rybbit deployment proof, analytics event-delivery proof, consent-persistence proof, telemetry runtime proof, or portal help runtime proof.",
            "Do not use this receipt to claim Rybbit deployment, analytics event delivery, consent persistence, telemetry runtime, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_observability_privacy_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
