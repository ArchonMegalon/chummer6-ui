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
    "scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh",
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
        "Source-Plan Preflight",
        "Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.",
        "bash scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh",
        "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
        "Source-Staged Family Set",
        "Source-Staged Release Boundary",
        "Portal and Operator Source Contracts",
        "portal_installer_handoff_staged_status",
        "portal_installer_handoff_staged_source_checks",
        "source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution",
        "portal_installer_handoff_staged_visual_contract=source_alignment_only_chummer_app_amber_mint_blue_palette_shared_grid_mobile_softened_high_contrast_motion_user_facing_route_rail_downloads_docs_cards_and_labelled_recovery_rails_not_runtime_visual_proof",
        "they do not prove installer availability, portal runtime behavior, hosted execution, or Docker self-host execution",
        "default-off Rybbit analytics boundary",
        "CHUMMER_ANALYTICS_PROVIDER=none",
        "operator explicitly configures the provider and site variables",
        "docker_self_host_operator_staged_status",
        "docker_self_host_operator_staged_service_count",
        "docker_self_host_operator_staged_source_checks",
        "docker_self_host_operator_staged_note=source_alignment_only_default_off_rybbit_not_docker_runtime",
        "they do not start Docker or prove the browser client renders",
        "workbench_hosting_privacy_staged_note=source_alignment_only_default_off_rybbit_not_browser_execution",
        "does not prove Rybbit delivery, hosted route execution, Docker browser execution, session-replay delivery, or autocapture behavior",
        "Docker Self-Host Runtime Proof",
        "Hosted Public-Edge Route Proof",
        "Hosted Public-Edge Execution Proof",
        "Browser-Lane Aggregate",
        "Extended Goal Refresh Gates",
        "Before running the refresh on a non-default checkout layout, set the portable proof-tooling overrides documented in `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md#portable-proof-tooling`",
        "`CHUMMER_PUBLIC_EDGE_COMPOSE_PATH` when the public-edge compose file is not available through the default sibling `chummer.run-services` checkout.",
        "`CHUMMER_DESIGN_PRODUCT_ROOT` when the Chummer5A design product data is not available through the default sibling `chummer-design/products/chummer` checkout.",
        "`CHUMMER5A_ORACLE_ROOT` when the Chummer5A oracle docs are not available through the default sibling oracle checkout.",
        "`CHUMMER5A_REPO_PATH`: canonical override for the legacy Chummer5A local repo path used by `scripts/chummer5a_parity_tester.py`",
        "path-like `CHUMMER5A_REPO_URL` values remain accepted only for compatibility, while URL-shaped values are not treated as filesystem paths.",
        "`CHUMMER5A_PARITY_LAB_ROOT`: override the EA parity-lab fixture, oracle-baseline, and veteran-workflow pack root used by `scripts/chummer5a_parity_tester.py`.",
        "Use `CHUMMER_PLAYWRIGHT_NODE_PATH` when Playwright dependencies are installed outside `NODE_PATH`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, or `scripts/node_modules`. Use `CHUMMER_PLAYWRIGHT_ROOT` when you want proof runners to check `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`.",
        "Do not set a repository-root override for `chummer-presentation`; proof/status scripts should derive that root from their own file location.",
        "Hosted execution proof discovers Playwright in `scripts/e2e-public-edge-execution.sh` through `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.",
        "local Playwright lookup checks `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.",
        "New browser proof runners should use the same workspace-relative discovery pattern instead of hardcoding `/docker/chummercomplete`.",
        "keep `docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers` aligned with this plan so release signoff keeps source-staged, source-plan, and source-calculation evidence out of hosted, Docker, aggregate, and polished-product claims",
        "keep `docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary` aligned with this plan so `MIG-106` through `MIG-109` stay open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the browser-client release claim",
        "regenerate affected source-staged, source-plan, source-calculation, hosted runtime, Docker self-host, and aggregate browser-lane receipts",
        "run hosted public-edge route-entry proof for clean `/app`, hosted `/blazor/app`, roster-first `/blazor/home`, `/blazor/health`, and proof-compatible `/blazor/workbench`",
        "promote Runner Intelligence only after shared `Chummer.Presentation` calculation seams",
        "keep the slate/amber/mint/blue visual treatment consistent across Blazor app chrome, public home, portal recovery pages, downloads/install handoff, docs explorer, and native installer progress surfaces",
        "Do not use source-staged receipts as release evidence.",
    ],
    "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md": [
        "Extended Goal Scope",
        "Release Evidence Boundary",
        "docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers",
        "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates",
        "Source-staged, source-plan, and source-calculation receipts are planning evidence only.",
        "They do not prove hosted route-entry, hosted execution, Docker self-host execution, aggregate browser-lane readiness, analytics runtime posture, connected-runtime behavior, installer/download behavior, durable roster hierarchy persistence, or Runner Intelligence runtime/statistical correctness.",
        "The release claim must be backed by separate hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts.",
        "`/app` remains the clean public Chummer Online route, `/blazor/app` remains the hosted app path, and `/blazor/workbench` remains the proof-compatible execution lane",
    ],
    "docs/MIGRATION_BACKLOG.md": [
        "Browser-client release evidence boundary",
        "MIG-106 through MIG-109",
        "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary",
        "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates",
        "docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers",
        "Source-staged workflow breadth, source-plan receipts, and source-calculation receipts keep the implementation and proof refresh aligned, but they do not close MIG-106 through MIG-109 by themselves.",
        "Closing those items requires refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts",
        "`/app` kept as the clean public Chummer Online route, `/blazor/app` kept as the hosted app path, and `/blazor/workbench` kept as the proof-compatible execution lane",
    ],
    "docs/WORKBENCH_RELEASE_SIGNOFF.md": [
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md",
        "runtime proof refresh",
        "Extended Goal Release Blockers",
        "Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.",
        "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
        "source_plan_only_with_visibility_blocks_not_browser_execution",
        "route-entry posture for clean public `/app`, clean public `/app?command=character_roster`, roster-first `/blazor/`, hosted `/blazor/app`, and `/blazor/workbench`",
        "canonical `https://chummer.run/blazor/workbench` proof base",
        "public product navigation remains `https://chummer.run/app`",
        "`/app` remains the clean public Chummer Online route",
        "generated receipts are refreshed after the /app route",
        "hosted route-entry, hosted execution, Docker self-host, and aggregate browser-lane receipts",
        "Runner Intelligence and roster hierarchy remain source/staged until runtime proof",
        "Rybbit remains metadata-only and self-host default-off",
        "session replay disabled, autocapture disabled",
        "The aggregate browser-lane proof set may require `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json` as source-policy input",
        "that guard only proves source-staged, source-plan, and source-calculation receipts stayed out of release-readiness aggregation",
        "It must not be treated as hosted workflow execution, Docker workflow execution, parity breadth, or polished-product proof.",
        "operators must also check the combined proof status lines `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`",
        "Those lines confirm whether the regenerated aggregate receipt carries the source-policy-only guard note and the `MIG-106` through `MIG-109` open-until-refreshed-proof posture.",
        "slate/amber/mint/blue visual treatment must stay consistent across Blazor app chrome, public home, portal recovery pages, downloads/install handoff, docs explorer, and native installer progress surfaces",
        "Do not use source-staged receipts as release evidence.",
        "Do not use source-plan receipts as release evidence.",
        "Do not use source-calculation receipts as hosted, Docker, or aggregate browser-lane proof.",
        "source-only visibility blocks for career/support, identity/SIN/license, combat support, skill maintenance, magic/resonance support, gear maintenance, Runner Intelligence, portal installer handoff, workbench hosting/privacy, Docker self-host operator posture, workbench roster hierarchy posture, legacy control coverage posture, and source-staged release boundary posture",
    ],
    "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md": [
        "Runtime Refresh Preflight Handoff",
        "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md",
        "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary",
        "docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates",
        "docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers",
        "Those anchors keep source-staged, source-plan, and source-calculation receipts out of hosted route-entry, hosted execution, Docker self-host, analytics runtime, connected-runtime, aggregate browser-lane, and polished-product release claims until refreshed runtime receipts prove the claim.",
        "scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh",
        "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
        ".codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
    ],
    "scripts/verify-blazor-source-staged-proof-release-boundary.py": [
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN",
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
        "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
    ],
    "docs/examples/blazor-source-staged-release-boundary.receipt.example.json": [
        '"scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation"',
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN",
        "The forbidden_staged_tokens field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.",
    ],
    "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md": [
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md",
        "Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.",
        "scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh",
        ".codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json",
        "source-plan preflight wrapper",
        "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
        "scripts/materialize-blazor-runtime-proof-refresh-plan.py",
        "runtime proof refresh",
        "Portable Proof Tooling",
        "CHUMMER_PUBLIC_EDGE_COMPOSE_PATH",
        "CHUMMER_DESIGN_PRODUCT_ROOT",
        "CHUMMER5A_ORACLE_ROOT",
        "CHUMMER5A_REPO_PATH",
        "CHUMMER5A_PARITY_LAB_ROOT",
        "CHUMMER_PLAYWRIGHT_NODE_PATH",
        "CHUMMER_PLAYWRIGHT_ROOT",
        "chummer5aRepoDefaultSource",
        "chummer5aRepoPathSource",
        "chummer5aRepoPathSource=cli_argument is emitted only when `--chummer5a-repo` is actually present, not merely when the resolved path differs from the default.",
        "proof/status scripts should derive the `chummer-presentation` repository root from their own file location",
        "Environment variables are reserved for adjacent external inputs such as public-edge compose, design product data, and oracle docs.",
        "Hosted execution proof uses workspace-relative Playwright lookup in `scripts/e2e-public-edge-execution.sh`: `NODE_PATH`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.",
        "Do not hardcode `/docker/chummercomplete` for Playwright discovery in new browser proof runners.",
        "Docker self-host portal proof uses the same portability rule in `scripts/e2e-portal.sh`",
        "compose, route-probe, and Playwright script defaults are derived from the script-relative `chummer-presentation` repo root",
        "local Playwright lookup checks `NODE_PATH`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.",
        "public_blazor_home_roster_entry",
        "roster-first home route pill",
        "aggregate browser-lane boundary visibility through `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`",
        "regenerated aggregate receipts carry the source-policy-only guard and `MIG-106` through `MIG-109` open-until-refreshed-proof posture",
        "also requires `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json` as source-policy input only",
        "aggregate browser-lane receipt proves staged/source-plan/source-calculation receipts stayed out of release-readiness aggregation without treating that guard as hosted or Docker workflow execution",
        "generated aggregate receipts also carry notes that `source_staged_release_boundary` is source-policy evidence only",
        "`MIG-106` through `MIG-109` remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the browser-client release claim",
        "source-staged release-boundary guard input that keeps `MIG-106` through `MIG-109` open until refreshed runtime proof exists",
        "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary",
        "parity-goal release boundary that keeps source-staged, source-plan, and source-calculation receipts as planning evidence only until refreshed hosted route-entry, hosted execution, Docker self-host, analytics, connected-runtime, source-boundary, and aggregate browser-lane receipts back the claim",
        "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md#promotion-rule",
        "links staged-proof promotion to the parity release boundary, runtime refresh gates, and release blockers so staged receipts can hand off to hosted/Docker proof refresh without becoming release evidence",
        "docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary",
        "keeps `MIG-106` through `MIG-109` open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the browser-client release claim.",
        "`/app`: clean public Chummer Online route",
        "`/blazor/app`: hosted Blazor app path",
        "`/blazor/workbench`: explicit /blazor/workbench compatibility route",
        "docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers",
        "release blocker ledger that prevents source-staged, source-plan, or source-calculation receipts from being treated as release evidence before hosted route-entry, hosted execution, Docker self-host, aggregate browser-lane, Rybbit privacy, Runner Intelligence, roster hierarchy, and cross-surface visual polish proof is refreshed",
        "BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates",
        "remaining validation gates for regenerated receipts, hosted and Docker proof refresh, aggregate proof readiness, Avalonia-equivalent workflow breadth, Runner Intelligence calculation reuse, roster hierarchy runtime evidence, Rybbit privacy posture, and cross-surface visual polish",
        "without executing Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof",
        "source_plan_only_with_visibility_blocks_not_browser_execution",
        "career_support_status_visibility",
        "identity_license_status_visibility",
        "combat_support_status_visibility",
        "skill_maintenance_status_visibility",
        "magic_support_status_visibility",
        "gear_maintenance_status_visibility",
        "runner_intelligence_status_visibility",
        "runner_intelligence_calculation_status_visibility",
        "portal_installer_handoff_status_visibility",
        "workbench_hosting_privacy_status_visibility",
        "docker_self_host_operator_status_visibility",
        "workbench_roster_hierarchy_status_visibility",
        "legacy_control_coverage_status_visibility",
        "source_staged_release_boundary_status_visibility",
        "source-only status lines",
        "career/support workflow family",
        "identity/SIN/license utility posture",
        "combat support utility posture",
        "skill maintenance utility posture",
        "magic/resonance support utility posture",
        "gear maintenance utility posture",
        "Runner Intelligence",
        "default-off Rybbit posture",
        "Rybbit delivery, hosted route execution, Docker browser execution, session-replay delivery, or autocapture behavior",
        "runtime installer proof",
        "portal/download/install/account/support/status/help handoff with user-facing route rail labels",
        "Docker runtime proof",
        "source-policy evidence",
        "roster hierarchy source-only status lines",
        "legacy control coverage source-only status lines",
    ],
    "scripts/print_blazor_public_edge_proof_status.py": [
        "route_public_chummer_app",
        "route_public_chummer_app_roster",
        "route_public_blazor_root_redirect",
        "route_public_blazor_home_roster_entry",
        "aggregate_source_checks",
        "aggregate_passed_source_checks",
        "aggregate_note_count",
        "aggregate_source_boundary_policy_note",
        "aggregate_migration_boundary_note",
        "The aggregate proof-set boundary notes are exposed separately as `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`.",
        "Those lines show whether the regenerated aggregate receipt carries the source-policy-only `source_staged_release_boundary` note and the `MIG-106` through `MIG-109` open-until-refreshed-proof posture before anyone treats aggregate browser-lane readiness as release evidence.",
        "runtime_proof_refresh_plan_career_support_status_visibility",
        "runtime_proof_refresh_plan_career_support_status_line_count",
        "runtime_proof_refresh_plan_identity_license_status_visibility",
        "runtime_proof_refresh_plan_identity_license_status_line_count",
        "runtime_proof_refresh_plan_combat_support_status_visibility",
        "runtime_proof_refresh_plan_combat_support_status_line_count",
        "runtime_proof_refresh_plan_skill_maintenance_status_visibility",
        "runtime_proof_refresh_plan_skill_maintenance_status_line_count",
        "runtime_proof_refresh_plan_magic_support_status_visibility",
        "runtime_proof_refresh_plan_magic_support_status_line_count",
        "runtime_proof_refresh_plan_gear_maintenance_status_visibility",
        "runtime_proof_refresh_plan_gear_maintenance_status_line_count",
        "runtime_proof_refresh_plan_runner_intelligence_status_visibility",
        "runtime_proof_refresh_plan_runner_intelligence_status_line_count",
        "runtime_proof_refresh_plan_runner_intelligence_calculation_status_visibility",
        "runtime_proof_refresh_plan_runner_intelligence_calculation_status_line_count",
        "runtime_proof_refresh_plan_portal_installer_handoff_status_visibility",
        "runtime_proof_refresh_plan_workbench_hosting_privacy_status_visibility",
        "runtime_proof_refresh_plan_docker_self_host_operator_status_visibility",
        "runtime_proof_refresh_plan_workbench_roster_hierarchy_status_visibility",
        "runtime_proof_refresh_plan_legacy_control_coverage_status_visibility",
        "workbench_character_library_staged_label=Character Roster",
        "runtime_proof_refresh_plan_source_staged_release_boundary_status_visibility",
        "source_plan_only_with_visibility_blocks_not_browser_execution",
        "runtime_proof_refresh_plan_workbench_hosting_privacy_status_line_count",
        "runtime_proof_refresh_plan_workbench_roster_hierarchy_status_line_count",
        "runtime_proof_refresh_plan_legacy_control_coverage_status_line_count",
        "runtime_proof_refresh_plan_source_staged_release_boundary_status_line_count",
    ],
        "docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json": [
        "scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh",
        "scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh",
        "scripts/e2e-public-edge.cjs",
        "scripts/e2e-public-edge-playwright.cjs",
        "career_support_status_visibility",
        "identity_license_status_visibility",
        "combat_support_status_visibility",
        "skill_maintenance_status_visibility",
        "magic_support_status_visibility",
        "gear_maintenance_status_visibility",
        "runner_intelligence_status_visibility",
        "runner_intelligence_calculation_status_visibility",
        "portal_installer_handoff_status_visibility",
        "workbench_hosting_privacy_status_visibility",
        "workbench_roster_hierarchy_status_visibility",
        "legacy_control_coverage_status_visibility",
        "source_staged_release_boundary_status_visibility",
        "portal_installer_handoff_staged_source_checks",
        "source_plan_only_with_visibility_blocks_not_browser_execution",
        "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
        "Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.",
        "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
        "Career/support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, persistence, or committed browser mutations.",
        "Identity/SIN/license status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, legal identity mutation, or persistence.",
        "Combat support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, combat-state mutation, reload mutation, damage-track mutation, or rules-engine calculations.",
        "Skill maintenance status lines are source-plan visibility only and do not prove hosted execution, Docker execution, skill-state mutation, specialization persistence, group-edit mutation, or rules-engine calculations.",
        "Magic/resonance support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, magic/resonance mutation, spirit creation, Matrix program mutation, or rules-engine calculations.",
        "Gear maintenance status lines are source-plan visibility only and do not prove hosted execution, Docker execution, gear-state mutation, inventory persistence, pricing, availability, or rules-engine calculations.",
        "Runner Intelligence status lines are source-plan visibility only and do not prove statistical-engine execution, hosted execution, Docker execution, percentile calculations, what-if spell/drug/gear calculations, hosted cohort aggregation, or rules-engine calculations.",
        "Runner Intelligence calculation status lines are source-plan visibility only and do not prove authoritative SR rules-engine validation, hosted execution, Docker execution, hosted cohort aggregation, or browser runtime parity.",
        "Portal installer handoff status lines are source-plan visibility only and do not prove installer, status, help, support, or portal runtime behavior.",
        "Status summary keeps stable workbench_character_library_staged_* keys for automation while exposing workbench_character_library_staged_label=Character Roster for product-facing readability.",
        "Workbench roster hierarchy status lines are source-plan visibility only and do not prove drag/drop execution, durable persistence, filesystem moves, or browser runtime parity.",
        "Legacy control coverage status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, persistence, or mutation paths.",
        "Source-staged release-boundary status lines are source-plan visibility only and do not make source-policy receipts release evidence.",
        "The docs index links the runtime refresh materializer and states the plan receipt does not execute Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof.",
        "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md",
        "scripts/verify-blazor-source-staged-proof-release-boundary.py",
        "docs/examples/blazor-source-staged-release-boundary.receipt.example.json",
        "source-plan visibility only",
        "Release signoff states that BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json may be required by the aggregate browser-lane proof set only as source-policy input, not hosted workflow execution, Docker workflow execution, parity breadth, or polished-product proof.",
        "Release signoff requires operators to check aggregate_note_count, aggregate_source_boundary_policy_note, and aggregate_migration_boundary_note before interpreting aggregate browser-lane readiness.",
        "The runtime refresh plan documents aggregate_note_count, aggregate_source_boundary_policy_note, and aggregate_migration_boundary_note so operators can see whether regenerated aggregate receipts carry the source-policy-only and MIG-open notes before treating aggregate readiness as release evidence.",
        "The runtime refresh plan documents portable proof-tooling preflight for CHUMMER_PUBLIC_EDGE_COMPOSE_PATH, CHUMMER_DESIGN_PRODUCT_ROOT, CHUMMER5A_ORACLE_ROOT, CHUMMER5A_REPO_PATH, CHUMMER5A_PARITY_LAB_ROOT, CHUMMER_PLAYWRIGHT_NODE_PATH, and CHUMMER_PLAYWRIGHT_ROOT while keeping the chummer-presentation repository root script-relative.",
        "CHUMMER5A_REPO_PATH is the canonical local filesystem path override; path-like CHUMMER5A_REPO_URL values remain compatibility-only and URL-shaped values are not treated as filesystem paths.",
        "chummer5aRepoPathSource=cli_argument is emitted only when --chummer5a-repo is actually present, not merely when the resolved path differs from the default.",
        "Hosted execution proof discovers Playwright through NODE_PATH, CHUMMER_PLAYWRIGHT_NODE_PATH, CHUMMER_PLAYWRIGHT_ROOT/node_modules, sibling chummer.run-services/node_modules, sibling node_modules, then scripts/node_modules instead of hardcoding /docker/chummercomplete.",
        "Docker self-host portal proof derives compose, route-probe, and Playwright script defaults from the script-relative chummer-presentation repo root and uses the same workspace-relative Playwright lookup including CHUMMER_PLAYWRIGHT_NODE_PATH and CHUMMER_PLAYWRIGHT_ROOT.",
        "docs_index_materializer_link_present",
        "docs_index_no_execution_boundary_present",
        "docs index links the runtime refresh materializer",
        "Release signoff links the extended goal release blockers and keeps source-staged, source-plan, and source-calculation evidence out of hosted, Docker, aggregate, and polished-product claims.",
        "Source-staged runbook promotion links the parity, runtime refresh, and release blocker anchors so staged receipts hand off to proof refresh without becoming release evidence.",
        "Runtime refresh gates keep the migration backlog boundary aligned so MIG-106 through MIG-109 remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the browser-client release claim.",
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
            "The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.",
            "Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.",
            "Runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation.",
            "It does not execute Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof.",
            "Hosted route-entry refresh expects public_blazor_root_redirect so /blazor/ proves the roster-first app?command=character_roster redirect and public_blazor_home_roster_entry so /blazor/home proves the roster-first orientation pill.",
            "Runtime proof remains owned by the receipts produced by those commands when explicitly run.",
            "Status output uses source_plan_only_with_visibility_blocks_not_browser_execution for the runtime refresh plan summary.",
            "Career/support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, persistence, or committed browser mutations.",
            "Identity/SIN/license status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, legal identity mutation, or persistence.",
            "Combat support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, combat-state mutation, reload mutation, damage-track mutation, or rules-engine calculations.",
            "Skill maintenance status lines are source-plan visibility only and do not prove hosted execution, Docker execution, skill-state mutation, specialization persistence, group-edit mutation, or rules-engine calculations.",
            "Magic/resonance support status lines are source-plan visibility only and do not prove hosted execution, Docker execution, magic/resonance mutation, spirit creation, Matrix program mutation, or rules-engine calculations.",
            "Workbench hosting/privacy status lines are source-plan visibility only and do not prove Rybbit delivery, browser execution, session-replay delivery, or autocapture behavior.",
            "Portal installer handoff status lines are source-plan visibility only and do not prove installer, status, help, support, or portal runtime behavior.",
            "Workbench roster hierarchy status lines are source-plan visibility only and do not prove drag/drop execution, durable persistence, filesystem moves, or browser runtime parity.",
            "Legacy control coverage status lines are source-plan visibility only and do not prove hosted execution, Docker execution, dialog action execution, persistence, or mutation paths.",
            "Source-staged release-boundary status lines are source-plan visibility only and do not make source-policy receipts release evidence.",
            "Docker self-host refresh keeps Rybbit default-off unless the operator explicitly configures provider and site variables.",
            "The docs index links the runtime refresh materializer and states the plan receipt does not execute Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof.",
        ],
        "career_support_status_visibility": {
            "source_only": True,
            "status_lines": [
                "career_support_staged_status",
                "career_support_staged_route_count",
                "career_support_staged_source_checks",
                "career_support_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, dialog action execution, persistence, or committed browser mutations.",
        },
        "identity_license_status_visibility": {
            "source_only": True,
            "status_lines": [
                "identity_license_staged_status",
                "identity_license_staged_route_count",
                "identity_license_staged_source_checks",
                "identity_license_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, dialog action execution, legal identity mutation, or persistence.",
        },
        "combat_support_status_visibility": {
            "source_only": True,
            "status_lines": [
                "combat_support_staged_status",
                "combat_support_staged_route_count",
                "combat_support_staged_source_checks",
                "combat_support_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, combat-state mutation, reload mutation, damage-track mutation, or rules-engine calculations.",
        },
        "skill_maintenance_status_visibility": {
            "source_only": True,
            "status_lines": [
                "skill_maintenance_staged_status",
                "skill_maintenance_staged_route_count",
                "skill_maintenance_staged_source_checks",
                "skill_maintenance_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, skill-state mutation, specialization persistence, group-edit mutation, or rules-engine calculations.",
        },
        "magic_support_status_visibility": {
            "source_only": True,
            "status_lines": [
                "magic_support_staged_status",
                "magic_support_staged_route_count",
                "magic_support_staged_source_checks",
                "magic_support_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, magic/resonance mutation, spirit creation, Matrix program mutation, or rules-engine calculations.",
        },
        "gear_maintenance_status_visibility": {
            "source_only": True,
            "status_lines": [
                "gear_maintenance_staged_status",
                "gear_maintenance_staged_route_count",
                "gear_maintenance_staged_source_checks",
                "gear_maintenance_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, gear-state mutation, inventory persistence, pricing, availability, or rules-engine calculations.",
        },
        "runner_intelligence_status_visibility": {
            "source_only": True,
            "status_lines": [
                "runner_intelligence_staged_status",
                "runner_intelligence_staged_route_count",
                "runner_intelligence_staged_source_checks",
                "runner_intelligence_staged_note=source_alignment_only_not_statistical_engine_or_browser_execution",
            ],
            "boundary": "Does not prove statistical-engine execution, hosted execution, Docker execution, percentile calculations, what-if spell/drug/gear calculations, hosted cohort aggregation, or rules-engine calculations.",
        },
        "runner_intelligence_calculation_status_visibility": {
            "source_only": True,
            "status_lines": [
                "runner_intelligence_calculation_status",
                "runner_intelligence_calculation_tier",
                "runner_intelligence_calculation_note=shared_calculation_source_only_not_rules_engine_or_browser_execution",
            ],
            "boundary": "Does not prove authoritative SR rules-engine validation, hosted execution, Docker execution, hosted cohort aggregation, or browser runtime parity.",
        },
        "portal_installer_handoff_status_visibility": {
            "source_only": True,
            "status_lines": [
                "portal_installer_handoff_staged_status",
                "portal_installer_handoff_staged_route_count",
                "portal_installer_handoff_staged_source_checks",
                "portal_installer_handoff_staged_note=source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution",
                "portal_installer_handoff_staged_visual_contract=source_alignment_only_chummer_app_amber_mint_blue_palette_shared_grid_mobile_softened_high_contrast_motion_user_facing_route_rail_downloads_docs_cards_and_labelled_recovery_rails_not_runtime_visual_proof",
            ],
            "boundary": "Does not prove installer availability, status/help/support runtime behavior, portal runtime behavior, hosted execution, or Docker self-host execution.",
        },
        "workbench_hosting_privacy_status_visibility": {
            "source_only": True,
            "status_lines": [
                "workbench_hosting_privacy_staged_status",
                "workbench_hosting_privacy_staged_route_count",
                "workbench_hosting_privacy_staged_source_checks",
                "workbench_hosting_privacy_staged_note=source_alignment_only_default_off_rybbit_not_browser_execution",
            ],
            "boundary": "Does not prove Rybbit delivery, hosted route execution, Docker browser execution, session-replay delivery, or autocapture behavior.",
        },
        "docker_self_host_operator_status_visibility": {
            "source_only": True,
            "status_lines": [
                "docker_self_host_operator_staged_status",
                "docker_self_host_operator_staged_service_count",
                "docker_self_host_operator_staged_source_checks",
                "docker_self_host_operator_staged_note=source_alignment_only_default_off_rybbit_not_docker_runtime",
            ],
            "boundary": "Does not start Docker or prove the browser client renders.",
        },
        "workbench_roster_hierarchy_status_visibility": {
            "source_only": True,
            "status_lines": [
                "workbench_roster_hierarchy_staged_status",
                "workbench_roster_hierarchy_staged_route_count",
                "workbench_roster_hierarchy_staged_source_checks",
                "workbench_roster_hierarchy_staged_note=source_alignment_only_roster_directories_not_drag_drop_execution_or_filesystem_mutation_proof",
            ],
            "boundary": "Does not prove drag/drop execution, durable persistence, filesystem moves, or browser runtime parity.",
        },
        "legacy_control_coverage_status_visibility": {
            "source_only": True,
            "status_lines": [
                "legacy_control_coverage_staged_status",
                "legacy_control_coverage_staged_control_count",
                "legacy_control_coverage_staged_covered_count",
                "legacy_control_coverage_staged_note=source_alignment_only_not_browser_execution",
            ],
            "boundary": "Does not prove hosted execution, Docker execution, dialog action execution, persistence, or mutation paths.",
        },
        "source_staged_release_boundary_status_visibility": {
            "source_only": True,
            "status_lines": [
                "source_staged_release_boundary_status",
                "source_staged_release_boundary_scope",
                "source_staged_release_boundary_forbidden_token_count",
                "source_staged_release_boundary_release_aggregation_source_count",
                "source_staged_release_boundary_documentation_source_count",
                "source_staged_release_boundary_note=source_policy_staged_and_source_plan_receipts_not_release_evidence",
            ],
            "boundary": "Does not make staged proof or source-plan receipts release evidence.",
        },
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
