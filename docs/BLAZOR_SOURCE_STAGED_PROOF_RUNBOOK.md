# Blazor Source-Staged Proof Runbook

## Purpose

This runbook covers the source-staged Blazor proof set for the promoted browser workbench route.

The staged proof set is not browser execution evidence. It exists to keep source wiring, route families, proof runners, receipt metadata, status reporting, and docs aligned before hosted `chummer.run` and Docker self-host browser receipts are refreshed.

It is not a substitute for hosted Playwright execution proof or Docker self-host proof.

## Canonical Command

From `/docker/chummercomplete/chummer-presentation`:

```bash
bash scripts/ai/milestones/blazor-source-staged-proof-set-check.sh
```

This command materializes each source-staged receipt and then writes the aggregate receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json
```

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
- `workbench_character_library`
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
- `legacy_control_coverage`

Each family must keep `proof_tier` set to:

```text
source_staged_no_browser_execution
```

## What This Proves

The staged proof set proves only that source-level wiring agrees across:

- promoted workbench affordances
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
```

## Promotion Rule

Do not add `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json` to release-readiness aggregate proof. It is an operator staging receipt only.

A staged family can be promoted into runtime proof only after the matching hosted and Docker self-host browser proof lanes execute and publish refreshed passing receipts.

## Release Boundary Guard

Use this guard when changing release proof aggregation or staged proof receipts:

```bash
bash scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh
```

It writes:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json
```

The guard checks that staged-only receipt names are not referenced by browser release-readiness aggregation sources and that the docs still state the source-only boundary. It is a source-policy guard, not browser execution evidence.
