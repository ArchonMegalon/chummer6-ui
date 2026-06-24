#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json",
    )
)

RELEASE_AGGREGATION_SOURCES = [
    "scripts/materialize-blazor-browser-lane-proof-set.py",
    "scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh",
]

STAGED_ONLY_TOKENS = [
    "BLAZOR_SOURCE_STAGED_PROOF_SET",
    "BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json",
    "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF",
    "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json",
    "BLAZOR_CAREER_SUPPORT_STAGED_PROOF",
    "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF",
    "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF",
    "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF",
    "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF",
    "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF",
    "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF",
    "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF",
    "BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF",
    "BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF",
    "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF",
    "BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF",
    "BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF",
    "BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF",
    "BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF",
    "BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF",
    "BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF",
    "BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF",
    "BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF",
    "BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF",
    "BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF",
    "BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF",
    "BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF",
    "BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF",
    "BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF",
    "BLAZOR_WORKBENCH_INLINE_EDITING_STAGED_PROOF",
    "BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF",
    "BLAZOR_WORKBENCH_PRINT_LAYOUT_STAGED_PROOF",
    "BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF",
    "BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF",
    "BLAZOR_WORKBENCH_CALCULATION_PROVENANCE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_LIFECYCLE_CALENDAR_STAGED_PROOF",
]

REQUIRED_SEPARATION_DOC_TOKENS = {
    "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md": [
        "Do not add `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json` to release-readiness aggregate proof.",
        "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
    ],
    "docs/WORKBENCH_RELEASE_SIGNOFF.md": [
        "must stay outside release-readiness aggregation",
        "not hosted or Docker browser execution evidence",
    ],
    "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md": [
        "aggregate source-staged proof set",
        "deliberately separate from hosted execution proof and Docker self-host proof",
    ],
}


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    failures: list[str] = []
    release_source_rows = []

    for relative_path in RELEASE_AGGREGATION_SOURCES:
        try:
            text = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"release aggregation source missing: {relative_path}")
            release_source_rows.append({"path": relative_path, "status": "missing", "forbidden_tokens": STAGED_ONLY_TOKENS})
            continue

        forbidden_hits = [token for token in STAGED_ONLY_TOKENS if token in text]
        if forbidden_hits:
            failures.append(f"{relative_path}: forbidden staged-proof token(s): {', '.join(forbidden_hits)}")
        release_source_rows.append(
            {
                "path": relative_path,
                "status": "failed" if forbidden_hits else "passed",
                "forbidden_tokens": forbidden_hits,
            }
        )

    doc_rows = []
    for relative_path, required_tokens in REQUIRED_SEPARATION_DOC_TOKENS.items():
        try:
            text = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"boundary documentation source missing: {relative_path}")
            doc_rows.append({"path": relative_path, "status": "missing", "missing_tokens": required_tokens})
            continue

        missing_tokens = [token for token in required_tokens if token not in text]
        if missing_tokens:
            failures.append(f"{relative_path}: missing boundary token(s): {', '.join(missing_tokens)}")
        doc_rows.append(
            {
                "path": relative_path,
                "status": "failed" if missing_tokens else "passed",
                "missing_tokens": missing_tokens,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_source_staged_release_boundary",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_policy_no_browser_execution",
        "scope": "staged_receipts_must_not_enter_release_readiness_aggregation",
        "release_aggregation_sources": release_source_rows,
        "documentation_sources": doc_rows,
        "forbidden_staged_tokens": STAGED_ONLY_TOKENS,
        "failures": failures,
        "notes": [
            "This receipt is a source-policy guard only.",
            "It does not execute hosted or Docker browser workflows.",
            "Passing means staged proof receipts are not wired into release-readiness aggregation sources and docs retain the boundary language.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_source_staged_release_boundary:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
