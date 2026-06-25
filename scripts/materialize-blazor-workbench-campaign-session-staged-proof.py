#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_campaign_session",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client campaign and session lane",
            "data-workbench-campaign-session=\"strip\"",
            "Keep table workflow beside the active dossier.",
            "campaign roster, GM review, session notes, rewards, table sharing, and run handoff",
            "active runner dossier",
            "Campaign Roster",
            "table's runner dossier list",
            "dossier approval and audit handoff",
            "table context near the active dossier",
            "karma and nuyen awards beside the dossier",
            "player and GM dossier co-view links",
            "runner dossier from build work into live play",
            "Setup help",
            "private const string OpenCampaignRosterCommand = \"open_campaign_roster\"",
            "private const string RequestGmReviewCommand = \"request_gm_review\"",
            "private const string OpenSessionNotesCommand = \"open_session_notes\"",
            "private const string ApplyRewardsCommand = \"apply_rewards\"",
            "private const string ShareToTableCommand = \"share_to_table\"",
            "private const string HandoffToRunCommand = \"handoff_to_run\"",
            "command: OpenCampaignRosterCommand",
            "command: RequestGmReviewCommand",
            "command: OpenSessionNotesCommand",
            "command: ApplyRewardsCommand",
            "command: ShareToTableCommand",
            "command: HandoffToRunCommand",
            "data-workbench-campaign-session-action=\"roster\"",
            "data-workbench-campaign-session-action=\"gm_review\"",
            "data-workbench-campaign-session-action=\"session_notes\"",
            "data-workbench-campaign-session-action=\"rewards\"",
            "data-workbench-campaign-session-action=\"table_share\"",
            "data-workbench-campaign-session-action=\"run_handoff\"",
            "data-workbench-campaign-session-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_campaign_session_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-campaign-session",
            ".browser-workbench-campaign-session-copy",
            ".browser-workbench-campaign-session-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench campaign-session posture",
            "blazor-workbench-campaign-session-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench campaign-session posture",
            "roster, GM review, session notes, rewards, table share, run handoff, and help",
            "not yet claiming campaign persistence, GM approval, reward mutation, table-share, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF",
            "workbench_campaign_session_staged_status",
            "workbench_campaign_session_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_campaign_session_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route campaign-session source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, campaign-persistence proof, GM-approval proof, reward-mutation proof, table-share proof, or portal help runtime proof.",
            "Do not use this receipt to claim campaign persistence, GM approval, reward mutation, table share, run handoff, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_campaign_session_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
