# Blazor Source-Staged Proof Runbook

## Purpose

This runbook covers the source-staged Blazor proof set for the user-facing Chummer Online route, the /blazor/workbench compatibility route, and the preview tools/result-state route.

`/app` remains the clean public browser client path, `/blazor/app` remains the hosted app path, `/blazor/workbench` remains the proof-compatible route, and `/blazor/preview` remains the preview tools/result-state route.

The aggregate route lane is `chummer_app_proof_compatible_workbench_preview_tools`.

The staged proof set is not browser execution evidence. It exists to keep source wiring, route families, proof runners, receipt metadata, status reporting, and docs aligned before hosted `chummer.run` and Docker self-host browser receipts are refreshed.

It is not a substitute for hosted Playwright execution proof or Docker self-host proof.

## Canonical Command

From the `chummer-presentation` repository root:

```bash
bash scripts/ai/milestones/blazor-source-staged-proof-set-check.sh
```

This command materializes each source-staged receipt and then writes the aggregate receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json
```

The aggregate receipt also includes `source_contract_check_count`, `source_contract_checks.runbook_route_roles`, and `source_contract_checks.docs_index_route_roles`, which keep the documented `/app`, `/blazor/app`, `/blazor/workbench`, and `/blazor/preview` route roles pinned to this runbook and the docs index.

The public proof status utility reports `source_staged_proof_set_route_lane`, `source_staged_proof_set_source_contract_checks`, and `source_staged_proof_set_note=aggregate_source_alignment_only_not_browser_execution_route_role_source_contracts` for this aggregate.

## Included Source-Staged Families

The aggregate currently covers:

- `career_support`
- `identity_license`
- `combat_support`
- `skill_maintenance`
- `magic_support`
- `gear_maintenance`
- `source_gear_utility`
- `magic_cleanup`
- `browser_output_handoff`
- `workbench_portal_handoff`
- `workbench_polish`
- `workbench_recovery`
- `workbench_hosting_privacy`
- `workbench_command_palette`
- `workbench_density`
- `workbench_workflow_ledger`
- `workbench_file_intake`
- `workbench_rules_data`
- `workbench_settings`
- `workbench_diagnostics`
- `workbench_connected_runtime`
- `workbench_accessibility`
- `workbench_section_rail`
- `workbench_desktop_install`
- `workbench_menu_bar`
- `workbench_workspace_tabs`
- `workbench_status_bar`

The `workbench_polish` member carries the clean public `/app`, hosted `/blazor/app`, and compatibility route `/blazor/workbench` task-dock and slate/amber/mint/blue Chummer Online theme-layer contract. It remains source-staged only; it does not prove screenshot rendering, accessibility, hosted route execution, or Docker browser execution.

The `workbench_desktop_install` member carries browser-to-desktop continuity plus native installer amber/slate/mint progress chrome and high-contrast fallback source alignment. It remains source-staged only; it does not prove installer runtime, release-download execution, hosted route execution, or Docker browser execution.

The `workbench_hosting_privacy` member carries the Chummer Online and /blazor/workbench compatibility route UI copy and status-utility contract for hosted/self-host posture, including the self-host default-off Rybbit boundary. It remains source-staged only; it does not prove Rybbit delivery, hosted route execution, or Docker browser execution.
- `workbench_inspector_rail`
- `workbench_dialog_stack`
- `workbench_context_actions`
- `workbench_search_filter`
- `workbench_layout_presets`
- `workbench_activity_feed`
- `workbench_keyboard_shortcuts`
- `workbench_resource_meters`
- `workbench_tree_tools`
- `workbench_save_session`
- `workbench_output_handoff`
- `workbench_validation_queue`
- `workbench_history_undo`
- `workbench_sync_presence`
- `workbench_data_packs`
- `workbench_character_library` (display label: Character Roster)
- `workbench_campaign_session`
- `workbench_observability_privacy`
- `workbench_first_run`
- `workbench_pwa_install`
- `workbench_docker_operator`
- `workbench_security_access`
- `workbench_notifications_jobs`
- `workbench_touch_mobile`
- `workbench_navigation_deeplink`
- `workbench_inline_editing`
- `workbench_performance_virtualization`
- `workbench_print_layout`
- `workbench_portrait_attachments`
- `workbench_windowing_panes`
- `workbench_calculation_provenance`
- `workbench_lifecycle_calendar`
- `workbench_progression_ledger`
- `workbench_import_reconcile`
- `workbench_compare_merge`
- `workbench_restore_checkpoint`
- `workbench_offline_cache`
- `workbench_session_locking`
- `workbench_share_export_privacy`
- `workbench_table_handoff`
- `workbench_rules_citation`
- `workbench_localization_terminology`
- `workbench_help_recovery_guidance`
- `workbench_gm_screen_export`
- `workbench_roster_hierarchy`
- `legacy_control_coverage`

`workbench_roster_hierarchy` also depends on the shared `RosterHierarchyStateJson` normalization and validation seam, so Avalonia and Blazor consume the same staged roster hierarchy metadata shape before runtime persistence is claimed.

Each family must keep `proof_tier` set to:

```text
source_staged_no_browser_execution
```

## What This Proves

The staged proof set proves only that source-level wiring agrees across:

- Chummer Online and /blazor/workbench compatibility route affordances
- hosted route-entry probe source
- hosted Playwright runner source
- Docker self-host Playwright runner source
- self-host receipt metadata source
- status reporting source
- parity and signoff docs

## What This Does Not Prove

The staged proof set does not prove:

- browser execution on `https://chummer.run`
- Docker self-host execution
- route reload/reconnect behavior
- committed workflow persistence
- full desktop-equivalent parity
- release readiness

Those claims require refreshed hosted execution and Docker self-host receipts.

## Status Command

After the staged set is materialized, the summary printer exposes it separately:

```bash
python3 scripts/print_blazor_public_edge_proof_status.py
```

Look for:

```text
source_staged_proof_set_status=
source_staged_proof_set_required_receipts=
source_staged_proof_set_passed_receipts=
source_staged_proof_set_expected_routes=
source_staged_proof_set_note=aggregate_source_alignment_only_not_browser_execution
portal_installer_handoff_staged_status=
portal_installer_handoff_staged_route_count=
portal_installer_handoff_staged_source_checks=
portal_installer_handoff_staged_note=source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution
portal_installer_handoff_staged_visual_contract=source_alignment_only_chummer_app_amber_mint_blue_palette_shared_grid_mobile_softened_high_contrast_motion_route_rail_downloads_docs_cards_and_labelled_recovery_rails_not_runtime_visual_proof
source_staged_release_boundary_status=
source_staged_release_boundary_scope=
source_staged_release_boundary_forbidden_token_count=
source_staged_release_boundary_release_aggregation_source_count=
source_staged_release_boundary_documentation_source_count=
source_staged_release_boundary_note=source_policy_staged_and_source_plan_receipts_not_release_evidence
```

## Promotion Rule

Do not add `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json` to release-readiness aggregate proof. It is an operator staging receipt only.

The staged-proof promotion rule is also governed by `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary`, `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates`, and `docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers`. Those anchors keep source-staged, source-plan, and source-calculation receipts out of hosted route-entry, hosted execution, Docker self-host, analytics runtime, connected-runtime, aggregate browser-lane, and polished-product release claims until refreshed runtime receipts prove the claim.

The source-staged proof set may also summarize explicitly separated source-calculation receipts such as `BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json`. Those receipts keep shared calculation seams aligned, but they are not authoritative SR rules-engine validation, hosted browser execution proof, Docker self-host proof, hosted cohort aggregation proof, or release-readiness evidence.

Do not add `BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json` to release-readiness aggregate proof. It is a source-plan preflight receipt only.

Do not add `BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json` to release-readiness aggregate proof. It is a source-calculation receipt only and not authoritative SR rules-engine validation or browser runtime evidence.

A staged family can be promoted into runtime proof only after the matching hosted and Docker self-host browser proof lanes execute and publish refreshed passing receipts.

## Runtime Refresh Preflight Handoff

After source-staged receipts are aligned, use `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md` and `scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh` as the source-plan preflight before running Docker or hosted browser proof lanes. Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence. The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.

Expected preflight receipt:

```text
.codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json
```

## Adjacent Portal-Boundary Source Proofs

Portal-boundary staged proofs such as `BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json` are useful for keeping download, installer, support, status, and help handoff contracts aligned with the browser-client story. They are intentionally not members of the Chummer Online and /blazor/workbench compatibility route source-staged proof set because they cover portal boundary behavior rather than a workbench workflow family.

Keep those receipts documented from `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md`, and keep their runtime boundary explicit: they do not prove hosted execution, Docker self-host execution, or installer availability.

## Adjacent Docker Self-Host Operator Source Proof

`BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json` keeps the portal-backed Docker self-host operator contract aligned, including the default-off Rybbit analytics boundary, sanitized `.env` example, and session replay and autocapture disabled for Chummer surfaces. It is source-only and not Docker runtime evidence.

Do not add `BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json` to release-readiness aggregate proof. It is an operator source-alignment receipt only.

Keep that receipt documented from `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md`, `docs/WORKBENCH_RELEASE_SIGNOFF.md`, and `docs/examples/blazor-docker-self-host-operator-staged-proof.receipt.example.json`. Runtime evidence remains `BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json` from `scripts/e2e-portal.sh`.

## Release Boundary Guard

Use this guard when changing release proof aggregation, staged proof receipts, or source-plan receipts:

```bash
bash scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh
```

It writes:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json
```

The compact example shape is tracked at `docs/examples/blazor-source-staged-release-boundary.receipt.example.json`. Its `forbidden_staged_token_examples` field is illustrative; generated receipts still emit the full forbidden staged/source-plan/source-calculation token list, including the runtime refresh plan receipt as forbidden release evidence.

The `forbidden_staged_tokens` field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.

The guard checks that source-staged, source-plan, and source-calculation receipt names are not referenced by browser release-readiness aggregation sources, that the docs still state the source-only boundary, and that the public-edge status utility reports `source_staged_release_boundary_*` lines as source-policy evidence. It is a source-policy guard, not browser execution evidence.
