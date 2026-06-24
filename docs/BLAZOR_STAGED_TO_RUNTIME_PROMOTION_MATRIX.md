# Blazor Staged-to-Runtime Promotion Matrix

## Purpose

This matrix maps source-staged Blazor browser work to the runtime evidence that must eventually prove it.

A staged family is not complete because a source receipt exists. A staged family becomes runtime-promoted only when the matching Docker self-host proof and hosted public-edge execution proof are refreshed and passing with that family represented.

## Runtime Receipts Required For Promotion

Every staged family needs both runtime lanes refreshed:

```text
.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json
.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json
```

The browser-lane aggregate may only be refreshed after the runtime receipts are current:

```text
.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json
```

## Promotion Matrix

| Staged family | Source-staged receipt | Hosted execution family | Required route lane |
| --- | --- | --- | --- |
| `career_support` | `BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json` | `promoted_career_entry_execution`, `promoted_career_entry_committed_execution`, `promoted_career_log_continuity`, `promoted_career_entry_edit_execution`, `promoted_career_entry_delete_execution`, `promoted_career_entry_edit_committed_execution`, `promoted_career_entry_delete_committed_execution`, `promoted_runner_notes_execution`, `promoted_runner_notes_committed_execution`, `promoted_career_entry_reorder_execution` | `/blazor/workbench` |
| `identity_license` | `BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json` | `promoted_identity_license_execution` | `/blazor/workbench` |
| `combat_support` | `BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json` | `promoted_combat_support_execution` | `/blazor/workbench` |
| `skill_maintenance` | `BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json` | `promoted_skill_maintenance_execution` | `/blazor/workbench` |
| `magic_support` | `BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json` | `promoted_magic_support_execution` | `/blazor/workbench` |
| `gear_maintenance` | `BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json` | `promoted_gear_maintenance_execution` | `/blazor/workbench` |
| `source_gear_utility` | `BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json` | `promoted_source_gear_utility_execution` | `/blazor/workbench` |
| `magic_cleanup` | `BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json` | `promoted_magic_cleanup_utility_execution` | `/blazor/workbench` |
| `browser_output_handoff` | `BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json` | `promoted_result_continuations` | `/blazor/workbench` |

## Non-Promoting Source Guards

These source receipts help manage breadth and safety but do not promote workflow parity:

- `BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json`
- `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json`
- `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json`
- `BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json`
- `BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json`
- `BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json`
- `BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json`
- `BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_INLINE_EDITING_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF.generated.json`
- `BLAZOR_WORKBENCH_PRINT_LAYOUT_STAGED_PROOF.generated.json`

## Promotion Rule

Do not describe a staged family as browser-proven until:

- the hosted execution receipt includes the hosted workflow family, and
- the Docker self-host receipt includes the matching route/workflow coverage, and
- the aggregate browser-lane receipt is refreshed after those runtime receipts.

## Status Summary

`scripts/print_blazor_public_edge_proof_status.py` reports this matrix as `staged_to_runtime_promotion_matrix_*` when `.codex-studio/published/BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.generated.json` exists.

That status output is still source-plan evidence only. It must not be read as Docker self-host proof, hosted public-edge execution proof, or browser-lane release readiness.

Example receipt shape: `docs/examples/blazor-staged-to-runtime-promotion-matrix.receipt.example.json`.
