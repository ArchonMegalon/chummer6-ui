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
    "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN",
    "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
    "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF",
    "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
    "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF",
    "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json",
    "BLAZOR_WORKBENCH_IMPORT_RECONCILE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_COMPARE_MERGE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF",
    "BLAZOR_WORKBENCH_OFFLINE_CACHE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SESSION_LOCKING_STAGED_PROOF",
    "BLAZOR_WORKBENCH_SHARE_EXPORT_PRIVACY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF",
    "BLAZOR_WORKBENCH_RULES_CITATION_STAGED_PROOF",
    "BLAZOR_WORKBENCH_LOCALIZATION_TERMINOLOGY_STAGED_PROOF",
    "BLAZOR_WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF",
    "BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF",
    "BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF",
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
    "BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF",
]

REQUIRED_SEPARATION_DOC_TOKENS = {
    "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md": [
        "Do not add `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json` to release-readiness aggregate proof.",
        "Do not add `BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json` to release-readiness aggregate proof.",
        "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
        "`BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json` keeps the portal-backed Docker self-host operator contract aligned",
        "It is source-only and not Docker runtime evidence.",
        "Do not add `BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json` to release-readiness aggregate proof.",
        "docs/examples/blazor-source-staged-release-boundary.receipt.example.json",
        "forbidden_staged_token_examples",
        "full forbidden staged/source-plan/source-calculation token list",
        "Use this guard when changing release proof aggregation, staged proof receipts, or source-plan receipts:",
        "The `forbidden_staged_tokens` field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.",
        "source-staged, source-plan, and source-calculation receipt names are not referenced by browser release-readiness aggregation sources",
        "source_staged_release_boundary_note=source_policy_staged_and_source_plan_receipts_not_release_evidence",
    ],
    "docs/WORKBENCH_RELEASE_SIGNOFF.md": [
        "must stay outside release-readiness aggregation",
        "not hosted or Docker browser execution evidence",
        "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
        "The aggregate browser-lane proof set may require `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json` as source-policy input",
        "that guard only proves source-staged, source-plan, and source-calculation receipts stayed out of release-readiness aggregation",
        "It must not be treated as hosted workflow execution, Docker workflow execution, parity breadth, or polished-product proof.",
        "operators must also check the combined proof status lines `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`",
        "Those lines confirm whether the regenerated aggregate receipt carries the source-policy-only guard note and the `MIG-106` through `MIG-109` open-until-refreshed-proof posture.",
    ],
    "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md": [
        "aggregate source-staged proof set",
        "deliberately separate from hosted execution proof and Docker self-host proof",
        "runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation",
    ],
    "docs/MIGRATION_BACKLOG.md": [
        "Browser-client release evidence boundary",
        "MIG-106 through MIG-109",
        "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary",
        "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates",
        "docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers",
        "Source-staged workflow breadth, source-plan receipts, and source-calculation receipts keep the implementation and proof refresh aligned, but they do not close MIG-106 through MIG-109 by themselves.",
        "Closing those items requires refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts",
    ],
    "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md": [
        "Extended Goal Refresh Gates",
        "docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary",
        "MIG-106` through `MIG-109",
        "refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim",
        "The aggregate proof-set boundary notes are exposed separately as `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`.",
        "Those lines show whether the regenerated aggregate receipt carries the source-policy-only `source_staged_release_boundary` note and the `MIG-106` through `MIG-109` open-until-refreshed-proof posture before anyone treats aggregate browser-lane readiness as release evidence.",
        "Do not use source-staged receipts as release evidence.",
    ],
    "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md": [
        "Source-Staged Release Boundary",
        "scripts/verify-blazor-source-staged-proof-release-boundary.py",
        "source-staged and source-plan receipts stay out of browser release-readiness aggregation",
        "scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh",
        "docs/examples/blazor-source-staged-release-boundary.receipt.example.json",
        "Docker self-host operator staged receipt as forbidden release evidence",
        "runtime refresh plan receipt as forbidden release evidence",
        "docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary",
        "keeps `MIG-106` through `MIG-109` open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim.",
        "also requires `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json` as source-policy input only",
        "aggregate browser-lane receipt proves staged/source-plan/source-calculation receipts stayed out of release-readiness aggregation without treating that guard as hosted or Docker workflow execution",
        "generated aggregate receipts also carry notes that `source_staged_release_boundary` is source-policy evidence only",
        "`MIG-106` through `MIG-109` remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim",
        "aggregate browser-lane boundary visibility through `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`",
        "regenerated aggregate receipts carry the source-policy-only guard and `MIG-106` through `MIG-109` open-until-refreshed-proof posture",
        "source-staged release-boundary guard input that keeps `MIG-106` through `MIG-109` open until refreshed runtime proof exists",
        "The `forbidden_staged_tokens` field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.",
        "reports `source_staged_release_boundary_*` status lines",
    ],
    "docs/examples/blazor-source-staged-release-boundary.receipt.example.json": [
        '"contract_name": "chummer6-ui.blazor_source_staged_release_boundary"',
        '"proof_tier": "source_policy_no_browser_execution"',
        '"scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation"',
        '"forbidden_staged_tokens": "(full generated list omitted from compact example)"',
        '"forbidden_staged_token_examples"',
        '"status_reporting_sources"',
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN",
        "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF",
        "Docker self-host operator staged receipts are source-only and must stay out of release-readiness aggregation.",
        "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
        "The forbidden_staged_tokens field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.",
        "Release signoff states that BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json may be required by the aggregate browser-lane proof set only as source-policy input, not hosted workflow execution, Docker workflow execution, parity breadth, or polished-product proof.",
        "Generated aggregate browser-lane receipts carry notes that source_staged_release_boundary is source-policy evidence only and that MIG-106 through MIG-109 remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim.",
        "Release signoff requires operators to check aggregate_note_count, aggregate_source_boundary_policy_note, and aggregate_migration_boundary_note before interpreting aggregate browser-lane readiness.",
        "The runtime refresh plan documents portable proof-tooling preflight for CHUMMER_PUBLIC_EDGE_COMPOSE_PATH, CHUMMER_DESIGN_PRODUCT_ROOT, CHUMMER5A_ORACLE_ROOT, CHUMMER5A_REPO_PATH, CHUMMER5A_PARITY_LAB_ROOT, CHUMMER_PLAYWRIGHT_NODE_PATH, and CHUMMER_PLAYWRIGHT_ROOT while keeping the chummer-presentation repository root script-relative.",
        "CHUMMER5A_REPO_PATH is the canonical local filesystem path override; path-like CHUMMER5A_REPO_URL values remain compatibility-only and URL-shaped values are not treated as filesystem paths.",
        "chummer5aRepoPathSource=cli_argument is emitted only when --chummer5a-repo is actually present, not merely when the resolved path differs from the default.",
        "Hosted execution proof discovers Playwright through NODE_PATH, CHUMMER_PLAYWRIGHT_NODE_PATH, CHUMMER_PLAYWRIGHT_ROOT/node_modules, sibling chummer.run-services/node_modules, sibling node_modules, then scripts/node_modules instead of hardcoding /docker/chummercomplete.",
        "Docker self-host portal proof derives compose, route-probe, and Playwright script defaults from the script-relative chummer-presentation repo root and uses the same workspace-relative Playwright lookup including CHUMMER_PLAYWRIGHT_NODE_PATH and CHUMMER_PLAYWRIGHT_ROOT.",
        "Passing means staged proof, source-plan, and source-calculation receipts are not wired into release-readiness aggregation sources and docs retain the boundary language.",
    ],
    "docs/examples/blazor-browser-lane-proof-set.receipt.example.json": [
        '"contract_name": "chummer6-ui.blazor_browser_lane_proof_set"',
        '"id": "source_staged_release_boundary"',
        '"contract_name": "chummer6-ui.blazor_source_staged_release_boundary"',
        '"scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation"',
        "The source_staged_release_boundary receipt is required as source-policy evidence only; it does not execute hosted or Docker browser workflows.",
        "MIG-106 through MIG-109 remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim.",
    ],
}

REQUIRED_STATUS_REPORTING_TOKENS = {
    "scripts/print_blazor_public_edge_proof_status.py": [
        "SOURCE_STAGED_RELEASE_BOUNDARY",
        "source_staged_release_boundary_status=",
        "source_staged_release_boundary_forbidden_token_count=",
        "source_staged_release_boundary_release_aggregation_source_count=",
        "source_staged_release_boundary_documentation_source_count=",
        "source_staged_release_boundary_note=source_policy_staged_and_source_plan_receipts_not_release_evidence",
        "aggregate_note_count=",
        "aggregate_source_boundary_policy_note=",
        "aggregate_migration_boundary_note=",
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

    status_reporting_rows = []
    for relative_path, required_tokens in REQUIRED_STATUS_REPORTING_TOKENS.items():
        try:
            text = read_text(relative_path)
        except FileNotFoundError:
            failures.append(f"boundary status reporting source missing: {relative_path}")
            status_reporting_rows.append({"path": relative_path, "status": "missing", "missing_tokens": required_tokens})
            continue

        missing_tokens = [token for token in required_tokens if token not in text]
        if missing_tokens:
            failures.append(f"{relative_path}: missing boundary status token(s): {', '.join(missing_tokens)}")
        status_reporting_rows.append(
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
        "scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation",
        "release_aggregation_sources": release_source_rows,
        "documentation_sources": doc_rows,
        "status_reporting_sources": status_reporting_rows,
        "forbidden_staged_tokens": STAGED_ONLY_TOKENS,
        "forbidden_staged_token_examples": [
            "BLAZOR_SOURCE_STAGED_PROOF_SET",
            "BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json",
            "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN",
            "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
            "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF",
            "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
            "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF",
            "BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json",
        ],
        "failures": failures,
        "notes": [
            "This receipt is a source-policy guard only.",
            "It does not execute hosted or Docker browser workflows.",
            "Docker self-host operator staged receipts are source-only and must stay out of release-readiness aggregation.",
            "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
            "The forbidden_staged_tokens field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.",
            "Example shape is documented at docs/examples/blazor-source-staged-release-boundary.receipt.example.json.",
            "Passing means staged proof, source-plan, and source-calculation receipts are not wired into release-readiness aggregation sources and docs retain the boundary language.",
            "The public edge status utility reports the boundary receipt as source-policy evidence only.",
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
