# Blazor Runtime Proof Refresh Plan

## Purpose

This plan describes how to promote the staged Blazor browser work into real runtime evidence.

The current staged source lanes widen the browser-client contract, but source-staged receipts are not enough to claim browser/Desktop parity. Runtime promotion requires refreshing Docker self-host proof, hosted route-entry proof, hosted execution proof, and the browser-lane aggregate after the source stage is aligned. Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.

## Order of Operations

Run these lanes in order when preparing a browser proof refresh.

### 0. Source-Plan Preflight

```bash
bash scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json
```

This confirms the refresh plan, command sources, source-only visibility blocks, career/support workflow family source-only status lines, identity/SIN/license utility posture source-only status lines, combat support utility posture source-only status lines, skill maintenance utility posture source-only status lines, magic/resonance support utility posture source-only status lines, gear maintenance utility posture source-only status lines, Runner Intelligence source-only status lines, Runner Intelligence calculation source-only status lines, roster hierarchy source-only status lines, legacy control coverage source-only status lines, source-staged release-boundary visibility, and no-execution boundaries are documented. The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.

### 1. Source-Staged Family Set

```bash
bash scripts/ai/milestones/blazor-source-staged-proof-set-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json
```

This confirms source staging is internally aligned. It is not runtime proof.

The career/support staged lane exposes the first planned workflow refresh separately as `career_support_staged_status`, `career_support_staged_route_count`, `career_support_staged_source_checks`, and `career_support_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, dialog action execution, persistence, or committed browser mutations.

The identity/SIN/license staged lane exposes restored `tab-info` utility posture separately as `identity_license_staged_status`, `identity_license_staged_route_count`, `identity_license_staged_source_checks`, and `identity_license_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, dialog action execution, legal identity mutation, or persistence.

The combat support staged lane exposes restored `tab-combat` utility posture separately as `combat_support_staged_status`, `combat_support_staged_route_count`, `combat_support_staged_source_checks`, and `combat_support_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, combat-state mutation, reload mutation, damage-track mutation, or rules-engine calculations.

The skill maintenance staged lane exposes restored `tab-skills` utility posture separately as `skill_maintenance_staged_status`, `skill_maintenance_staged_route_count`, `skill_maintenance_staged_source_checks`, and `skill_maintenance_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, skill-state mutation, specialization persistence, group-edit mutation, or rules-engine calculations.

The magic/resonance support staged lane exposes restored adept, magician, critter, and technomancer utility posture separately as `magic_support_staged_status`, `magic_support_staged_route_count`, `magic_support_staged_source_checks`, and `magic_support_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, magic/resonance mutation, spirit creation, Matrix program mutation, or rules-engine calculations.

The gear maintenance staged lane exposes restored `tab-gear` utility posture separately as `gear_maintenance_staged_status`, `gear_maintenance_staged_route_count`, `gear_maintenance_staged_source_checks`, and `gear_maintenance_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, gear-state mutation, inventory persistence, pricing, availability, or rules-engine calculations.

The Runner Intelligence staged lane exposes character-statistics posture separately as `runner_intelligence_staged_status`, `runner_intelligence_staged_route_count`, `runner_intelligence_staged_source_checks`, and `runner_intelligence_staged_note=source_alignment_only_not_statistical_engine_or_browser_execution`. Those lines are source-plan visibility only; they do not prove statistical-engine execution, hosted execution, Docker execution, percentile calculations, what-if spell/drug/gear calculations, hosted cohort aggregation, or rules-engine calculations.

The Runner Intelligence calculation lane exposes the shared calculator seam separately as `runner_intelligence_calculation_status`, `runner_intelligence_calculation_tier`, and `runner_intelligence_calculation_note=shared_calculation_source_only_not_rules_engine_or_browser_execution`. Those lines are source-plan visibility only; they do not prove authoritative SR rules-engine validation, hosted execution, Docker execution, hosted cohort aggregation, or browser runtime parity.

### 2. Source-Staged Release Boundary

```bash
bash scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json
```

This confirms staged receipts are not wired into release-readiness aggregation.

The status summary exposes this guard separately as `source_staged_release_boundary_status`, `source_staged_release_boundary_scope`, `source_staged_release_boundary_forbidden_token_count`, `source_staged_release_boundary_release_aggregation_source_count`, `source_staged_release_boundary_documentation_source_count`, and `source_staged_release_boundary_note=source_policy_staged_and_source_plan_receipts_not_release_evidence`. Those lines are source-policy visibility only; they do not make source-staged or source-plan receipts release evidence.

### 3. Portal and Operator Source Contracts

```bash
bash scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh
bash scripts/ai/milestones/blazor-docker-self-host-operator-staged-proof-check.sh
bash scripts/ai/milestones/blazor-account-support-handoff-staged-proof-check.sh
```

Expected receipts:

```text
.codex-studio/published/BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json
.codex-studio/published/BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json
.codex-studio/published/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json
```

These confirm source contracts for portal/download/install/account/support/status/help handoff with user-facing route rail labels. Published artifact rows should sit in a labelled published-artifacts list with a visible artifact count and explicit empty-state marker, keep direct artifact links for public downloads, and expose same-origin install handoff metadata when `publicInstallRoute` is present. They should render the artifact id visibly. They should expose platform metadata as well. The visible platform label should have a stable marker. Route probes should assert the rendered direct-download link mode, visible direct-download text, platform markers, raw URL metadata, and retained install handoff metadata. They should preserve the raw manifest URL as metadata. They should also expose the selected install handoff route as metadata. They should expose `raw-url` link-mode metadata and render the direct-download mode visibly too. They are not runtime proof. The portal installer handoff receipt explicitly includes the Blazor desktop compatibility installer routes `/downloads/install/blazor-desktop-linux-x64-installer` and `/downloads/install/blazor-desktop-win-x64-installer`, plus same-origin `/status`, `/contact`, and `/help` portal guidance surfaces, but that remains source-staged route coverage rather than promoted installer runtime proof. Known unpublished compatibility routes are expected to return to the downloads shelf with a URL-encoded `next` route and `installState=proof_required` until artifact bytes and startup proof exist. The shelf should also expose a styled, labelled compatibility-handoff route list with a visible count and an explicit empty-state marker and list known compatibility handoff routes and their install posture plus promotion state and artifact availability before a route is clicked. The list should be limited to proof-required fallback routes not already represented by published artifacts. Listed fallback routes should link into their guarded installer routes. The shelf should keep a direct Chummer Online recovery action visible for that state. Route probes should assert that rendered proof-required panel directly. That recovery action must honor the configured portal Blazor public path.

The status summary exposes this lane separately as `portal_installer_handoff_staged_status`, `portal_installer_handoff_staged_route_count`, `portal_installer_handoff_staged_source_checks`, `portal_installer_handoff_staged_note=source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution`, and `portal_installer_handoff_staged_visual_contract=source_alignment_only_chummer_app_amber_mint_blue_palette_shared_grid_mobile_softened_high_contrast_motion_user_facing_route_rail_downloads_docs_cards_and_labelled_recovery_rails_not_runtime_visual_proof`. The visual contract line also records shared ambient grid and mobile-softened grid and high-contrast portal affordance and user-facing route rail label source alignment for portal pages. Those lines are useful for refresh planning only; they do not prove installer availability, status/help/support runtime behavior, portal runtime behavior, hosted execution, or Docker self-host execution.

The Docker self-host operator source contract also records the default-off Rybbit analytics boundary. `CHUMMER_ANALYTICS_PROVIDER=none` remains the sanitized self-host default, and Rybbit is only part of the runtime path when the operator explicitly configures the provider and site variables before refreshing Docker self-host proof.

The status summary exposes that operator lane separately as `docker_self_host_operator_staged_status`, `docker_self_host_operator_staged_service_count`, `docker_self_host_operator_staged_source_checks`, and `docker_self_host_operator_staged_note=source_alignment_only_default_off_rybbit_not_docker_runtime`. Those lines confirm source wiring and default-off analytics posture only; they do not start Docker or prove the browser client renders.

The workbench hosting/privacy staged lane exposes the user-facing hosted/self-host privacy posture separately as `workbench_hosting_privacy_staged_status`, `workbench_hosting_privacy_staged_route_count`, `workbench_hosting_privacy_staged_source_checks`, and `workbench_hosting_privacy_staged_note=source_alignment_only_default_off_rybbit_not_browser_execution`. Those lines confirm source wiring for the default-off Rybbit privacy copy plus explicit no session replay and no autocapture posture only; they do not prove Rybbit delivery, hosted route execution, Docker browser execution, session-replay delivery, or autocapture behavior. This source visibility does not prove Rybbit delivery, hosted route execution, Docker browser execution, session-replay delivery, or autocapture behavior.

The workbench roster hierarchy staged lane exposes custom directory and drag intent posture separately as `workbench_roster_hierarchy_staged_status`, `workbench_roster_hierarchy_staged_route_lane`, `workbench_roster_hierarchy_staged_route_count`, `workbench_roster_hierarchy_staged_chummer_app_route`, `workbench_roster_hierarchy_staged_workbench_compat_route`, `workbench_roster_hierarchy_staged_source_checks`, and `workbench_roster_hierarchy_staged_note=source_alignment_only_roster_directories_not_drag_drop_execution_or_filesystem_mutation_proof`. Those lines are source-plan visibility only; they do not prove drag/drop execution, durable persistence, filesystem moves, or browser runtime parity.

The legacy control coverage staged lane exposes Avalonia-era control breadth separately as `legacy_control_coverage_staged_status`, `legacy_control_coverage_staged_control_count`, `legacy_control_coverage_staged_covered_count`, and `legacy_control_coverage_staged_note=source_alignment_only_not_browser_execution`. Those lines are source-plan visibility only; they do not prove hosted execution, Docker execution, dialog action execution, persistence, or mutation paths.

### 4. Docker Self-Host Runtime Proof

```bash
bash scripts/e2e-portal.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json
```

This is the Docker self-host browser runtime evidence.

### 5. Hosted Public-Edge Route Proof

```bash
node scripts/e2e-public-edge.cjs
```

Expected receipt source depends on the hosted route-proof materializer path already used by the public-edge proof lane.

This is route-entry evidence only. It is not workflow execution proof. The refreshed hosted route-entry receipt must include the `public_chummer_app_route` marker for clean `/app`, the `public_chummer_app_roster_route` marker for clean `/app?command=character_roster`, the `public_blazor_root_redirect` marker so `/blazor/` proves the roster-first `app?command=character_roster` redirect, and the `public_blazor_home_roster_entry` marker so `/blazor/home` proves the roster-first orientation pill alongside the hosted `/blazor/app` path.

### 6. Hosted Public-Edge Execution Proof

```bash
node scripts/e2e-public-edge-playwright.cjs
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json
```

This is hosted browser workflow execution evidence for the promoted `/blazor/workbench` lane. Public product navigation should prefer the clean `/app` Chummer Online alias, while `/blazor/app` remains the hosted app path and `/blazor/workbench` remains the canonical execution-proof base until the hosted receipts and verifiers are deliberately migrated together.

### 7. Browser-Lane Aggregate

```bash
bash scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json
```

This aggregate must consume runtime receipts, not source-staged receipts.

## Promotion Rule

A staged workflow family is promotable only after both of these are refreshed and passing:

- Docker self-host browser proof
- hosted public-edge execution proof

Do not use source-staged receipts as release evidence.

## Status Summary

After refresh, use:

```bash
python3 scripts/print_blazor_public_edge_proof_status.py
```

Before running the refresh on a non-default checkout layout, set the portable proof-tooling overrides documented in `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md#portable-proof-tooling`:

- `CHUMMER_PUBLIC_EDGE_COMPOSE_PATH` when the public-edge compose file is not available through the default sibling `chummer.run-services` checkout.
- `CHUMMER_DESIGN_PRODUCT_ROOT` when the Chummer5A design product data is not available through the default sibling `chummer-design/products/chummer` checkout.
- `CHUMMER5A_ORACLE_ROOT` when the Chummer5A oracle docs are not available through the default sibling oracle checkout.

Do not set a repository-root override for `chummer-presentation`; proof/status scripts should derive that root from their own file location.

Use `CHUMMER_PLAYWRIGHT_NODE_PATH` when Playwright dependencies are installed outside `NODE_PATH`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, or `scripts/node_modules`. Use `CHUMMER_PLAYWRIGHT_ROOT` when you want proof runners to check `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`.

Hosted execution proof discovers Playwright in `scripts/e2e-public-edge-execution.sh` through `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`. New browser proof runners should use the same workspace-relative discovery pattern instead of hardcoding `/docker/chummercomplete`.

Docker self-host portal proof follows the same rule in `scripts/e2e-portal.sh`: compose, route-probe, and Playwright script defaults are script-relative to `chummer-presentation`, and local Playwright lookup checks `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.

The summary should show source-staged lanes separately from Docker, hosted route-entry, hosted execution, connected-runtime, analytics, and aggregate browser-lane proof.

`scripts/print_blazor_public_edge_proof_status.py` also reports this plan as `runtime_proof_refresh_plan_*` when `.codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json` exists. Those lines are source-plan visibility only and do not replace the Docker, hosted, or aggregate browser receipts. The summary note is `source_plan_only_with_visibility_blocks_not_browser_execution`. The summary also exposes `runtime_proof_refresh_plan_career_support_status_visibility`, `runtime_proof_refresh_plan_identity_license_status_visibility`, `runtime_proof_refresh_plan_combat_support_status_visibility`, `runtime_proof_refresh_plan_skill_maintenance_status_visibility`, `runtime_proof_refresh_plan_magic_support_status_visibility`, `runtime_proof_refresh_plan_gear_maintenance_status_visibility`, `runtime_proof_refresh_plan_runner_intelligence_status_visibility`, `runtime_proof_refresh_plan_runner_intelligence_calculation_status_visibility`, `runtime_proof_refresh_plan_portal_installer_handoff_status_visibility`, `runtime_proof_refresh_plan_workbench_hosting_privacy_status_visibility`, `runtime_proof_refresh_plan_docker_self_host_operator_status_visibility`, `runtime_proof_refresh_plan_workbench_roster_hierarchy_status_visibility`, `runtime_proof_refresh_plan_legacy_control_coverage_status_visibility`, and `runtime_proof_refresh_plan_source_staged_release_boundary_status_visibility` so operators can see which source-only status blocks are present without treating them as runtime proof. The status utility keeps the stable `workbench_character_library_staged_*` keys for existing automation but also emits `workbench_character_library_staged_label=Character Roster` so humans see the current product terminology.

The same status utility exposes `route_public_chummer_app`, `route_public_chummer_app_roster`, `route_public_blazor_root_redirect`, and `route_public_blazor_home_roster_entry` from the hosted route-entry receipt so operators can see whether the refreshed public edge still proves clean `/app`, clean `/app?command=character_roster`, the roster-first `/blazor/` redirect, and the roster-first `/blazor/home` orientation pill. It also exposes `aggregate_source_checks` and `aggregate_passed_source_checks` so aggregate proof-set source checks stay visible separately from runtime receipt counts.

The aggregate proof-set boundary notes are exposed separately as `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`. Those lines show whether the regenerated aggregate receipt carries the source-policy-only `source_staged_release_boundary` note and the `MIG-106` through `MIG-109` open-until-refreshed-proof posture before anyone treats aggregate browser-lane readiness as release evidence.

Example receipt shape: `docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json`.

## Extended Goal Refresh Gates

Before the Blazor web client can be described as a polished web-delivered desktop client, the refresh sequence must clear these gates without collapsing them into one receipt:

- keep `docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers` aligned with this plan so release signoff keeps source-staged, source-plan, and source-calculation evidence out of hosted, Docker, aggregate, and polished-product claims
- keep `docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary` aligned with this plan so `MIG-106` through `MIG-109` stay open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim
- regenerate affected source-staged, source-plan, source-calculation, hosted runtime, Docker self-host, and aggregate browser-lane receipts after the `/app` route, Chummer Online marker, Rybbit privacy, portal handoff, and theme-contract source changes land
- run source materializers before runtime claims so stale example receipts or source-token counts cannot hide drift
- run hosted public-edge route-entry proof for clean `/app`, hosted `/blazor/app`, roster-first `/blazor/home`, `/blazor/health`, and proof-compatible `/blazor/workbench`
- run hosted execution proof through the canonical `/blazor/workbench` lane until the execution verifier is deliberately migrated to another base
- run Docker self-host proof separately from hosted proof, including portal, API, Blazor, downloads, docs, owner/session posture, and default-off analytics posture
- refresh the aggregate browser-lane proof only after the required hosted, Docker, analytics, connected-runtime, and source-boundary receipts are in their expected states
- keep source-staged workflow breadth separate from runtime parity; visible affordance families do not prove mutation, persistence, import/export, validation, connected-runtime, installer, or help/support execution
- promote Runner Intelligence only after shared `Chummer.Presentation` calculation seams, Avalonia bridge reuse, Blazor DI reuse, percentile/what-if fixtures, drain/stun risk fixtures, and privacy-safe cohort posture are all covered by source-calculation and runtime proof
- promote character roster hierarchy only after custom folders, nesting, drag/drop intent, keyboard operation, non-destructive metadata, cycle prevention, shared serialization, and owner-scoped persistence have stronger runtime evidence than source posture alone
- keep hosted Rybbit proof metadata-only, with session replay and autocapture disabled, and keep Docker self-host analytics default-off unless explicitly configured by the operator
- keep the slate/amber/mint/blue visual treatment consistent across Blazor app chrome, public home, portal recovery pages, downloads/install handoff, docs explorer, and native installer progress surfaces before claiming polished product readiness
