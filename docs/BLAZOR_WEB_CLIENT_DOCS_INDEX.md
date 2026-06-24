# Blazor Web Client Docs Index

## Purpose

This index defines the current documentation set for the Chummer6 browser-client lane.

The goal is straightforward: `Chummer.Blazor` should ship as a polished web client on `chummer.run`, preserve the same practical user workflow as the Avalonia desktop client, and remain self-hostable through Docker without splitting the product story.

## Primary Documents

- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`: main design spec, product posture, parity rules, workflow bar, and required proof standard
- `docs/MIGRATION_BACKLOG.md`: backlog contract for browser-workbench promotion, parity milestones, and remaining implementation gaps
- `docs/WORKBENCH_RELEASE_SIGNOFF.md`: release-signoff posture for promoted workbench routes and browser workflow proof

## Hosted/Public-Edge Proof Documents

- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`: contract for hosted `chummer.run` route-entry/workbench posture proof
- `scripts/verify_blazor_public_edge_workbench_proof.py`: structural verifier for the hosted route-entry/workbench receipt contract
- `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`: milestone-style wrapper for the hosted route-entry/workbench verifier
- `docs/examples/blazor-public-edge-workbench-proof.receipt.example.json`: example hosted route-entry/workbench receipt shape
- `docs/examples/blazor-public-edge-workbench-proof.expanded.receipt.example.json`: expanded example hosted route-entry/workbench receipt shape
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`: contract for hosted `chummer.run` browser workflow execution proof
- `scripts/verify_blazor_public_edge_execution_proof.py`: structural verifier for the hosted execution-proof receipt contract
- `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`: milestone-style wrapper for the hosted execution-proof verifier
- `docs/examples/blazor-public-edge-execution-proof.receipt.example.json`: example hosted execution-proof receipt shape
- `scripts/print_blazor_public_edge_proof_status.py`: shared status summary utility for self-host proof, hosted route-entry proof, hosted execution proof, analytics posture, connected-runtime posture, and external-host blocker receipts
- `scripts/print_blazor_public_edge_proof_status.py`: also reports the optional staged career/support source-alignment receipt when it has been generated; that status line is not browser execution proof
- `scripts/materialize-blazor-browser-lane-proof-set.py`: aggregate proof-set materializer that fails unless the required browser-lane receipts are all in their expected passing/ready states
- `scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh`: milestone-style wrapper for the aggregate browser-lane proof set
- `scripts/materialize-blazor-career-support-staged-proof.py`: source-structural staged proof materializer for the next career/support workflow refresh
- `scripts/ai/milestones/blazor-career-support-staged-proof-check.sh`: milestone-style wrapper for the staged career/support source-alignment check
- `docs/examples/blazor-career-support-staged-proof.receipt.example.json`: example receipt shape for the staged career/support source-alignment proof
- `.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json`: published aggregate browser-lane proof-set receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`: published hosted execution-proof receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`: published hosted route-entry/workbench proof receipt

## Self-Host and Operator Documents

- `docs/BLAZOR_SELF_HOST_RUNBOOK.md`: canonical Docker and operator runbook for the browser workbench lane
- `docs/examples/self-hosted-browser-workbench.env.example`: baseline environment defaults for self-hosted portal/API/browser deployments
- `docs/DESKTOP_RELEASE_PIPELINE.md`: release pipeline notes that still need to stay aligned with the promoted browser route model
- `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`: published Docker self-host browser workbench receipt included in the combined browser-lane proof status summary

## Analytics and Privacy Posture

- `scripts/materialize-blazor-analytics-posture-proof.py`: proof materializer for optional browser analytics wiring and privacy boundaries
- `scripts/ai/milestones/blazor-analytics-posture-check.sh`: milestone-style wrapper for the analytics posture proof
- `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`: published analytics posture receipt

Hosted `chummer.run` may enable the Rybbit adapter for the Blazor web client, but self-hosted Docker defaults keep analytics disabled with `CHUMMER_ANALYTICS_PROVIDER=none`.

The adapter is limited to route/workflow metadata such as route family, command id, tab id, control id, dialog action id, and boolean fixture/workspace presence. It must not emit character names, aliases, owner ids, workspace ids, file names, document contents, XML, payloads, hashes, or generated dossier text.

## Connected Runtime Posture

- `scripts/materialize-blazor-connected-runtime-posture-proof.py`: proof materializer for optional session, coach, and assistant forwarding posture
- `scripts/ai/milestones/blazor-connected-runtime-posture-check.sh`: milestone-style wrapper for connected-runtime posture proof
- `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`: published connected-runtime posture receipt

Connected runtime proof is deliberately narrower than workflow parity. It proves that optional session, coach, and assistant routes can remain behind the portal boundary and use the signed portal-owner forwarding seam when configured. It does not prove that every downstream connected-runtime workflow is complete.

The browser workbench also renders a connected-runtime posture card showing whether session, coach, and assistant lanes are configured or off. The card must not expose proxy URLs or owner secrets.

## Route and Workflow Canon

The browser lane currently uses this public route posture:

- `/blazor/`: stable public entry that should resolve into the browser workbench
- `/blazor/workbench`: promoted product-shaped browser client
- `/blazor/preview`: proof/support route, not the primary product promise

Parity claims should be evaluated against workflow families, not against visual resemblance alone:

- startup and recent-work recovery
- runner creation and origin/rules selection
- dense runner-sheet editing across core sections
- dialog-driven add/edit/commit loops
- browser result continuations for save, export, print, and download
- cross-route continuity between workbench, support, downloads, account, and optional coach/session surfaces

The hosted execution contract already spans more than startup posture alone. Its current promoted family set includes:

- startup command execution and dense startup utilities
- origin/rules continuity and build-lab continuity
- dense selectors on gear, qualities, magic, and cyberware lanes
- in-place edit and delete/recovery utilities across contacts, gear, qualities, magic, and cyberware
- resumed result, action, committed-action, and advanced-action families

The next hosted and self-host proof refresh is staged to add the career/support workflow family on the promoted workbench route. That staged family covers `tab-calendar` section resume, `create_entry`, `create_entry&dialog_action=add`, `edit_entry`, `edit_entry&dialog_action=apply`, `delete_entry`, `delete_entry&dialog_action=delete`, `open_notes`, `open_notes&dialog_action=save`, `move_up`, and `move_down`. It remains staged until the public-edge and Docker receipts are regenerated from the updated proof runners.

`scripts/ai/milestones/blazor-career-support-staged-proof-check.sh` can be used before the live proof refresh to verify that product UI, hosted route-entry probing, hosted execution probing, Docker self-host probing, receipt metadata, status reporting, and docs still agree about that staged route family.

## Current Documentation Truth

The documentation set is ahead of full proof completion by design.

Current docs already define the intended shipped posture and the evidence contract. The remaining work is to expand browser-specific proof and receipts until the hosted and self-hosted lanes both support the same release claim.

## Source-Staged Proof Set

- `docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md` documents the source-staged Blazor proof-set refresh path. This lane is source-alignment only and must remain separate from hosted execution proof, Docker self-host proof, and release-readiness aggregation.
- `docs/examples/blazor-source-staged-proof-set.receipt.example.json` shows the aggregate receipt shape for `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json`.
- `docs/BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.md` defines the source-staged browser save, save-as, export, print, and download handoff contract. It is not runtime proof.
- `docs/examples/blazor-browser-output-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.md` defines the source-staged workbench downloads/status/support/account handoff contract. It is not runtime proof.
- `docs/examples/blazor-workbench-portal-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.md` defines the source-staged promoted workbench polish contract for the task dock. It is not runtime proof.
- `docs/examples/blazor-workbench-polish-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.md` defines the source-staged promoted workbench session recovery contract. It is not runtime proof.
- `docs/examples/blazor-workbench-recovery-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.md` defines the source-staged promoted workbench hosting and privacy posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-hosting-privacy-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.md` defines the source-staged promoted workbench command-palette posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-command-palette-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.md` defines the source-staged promoted workbench density posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-density-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.md` defines the source-staged promoted workbench workflow-ledger posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-workflow-ledger-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.md` defines the source-staged promoted workbench file-intake posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-file-intake-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.md` defines the source-staged promoted workbench rules/data posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-rules-data-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.md` defines the source-staged promoted workbench settings/preferences posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-settings-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.md` defines the source-staged promoted workbench diagnostics posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-diagnostics-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.md` defines the source-staged promoted workbench connected-runtime posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-connected-runtime-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.md` defines the source-staged promoted workbench accessibility posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-accessibility-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.md` defines the source-staged promoted workbench section-rail posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-section-rail-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.md` defines the source-staged promoted workbench desktop install handoff contract. It is not runtime proof.
- `docs/examples/blazor-workbench-desktop-install-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.md` defines the source-staged promoted workbench desktop menu-bar posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-menu-bar-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.md` defines the source-staged promoted workbench workspace-tabs posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-workspace-tabs-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.md` defines the source-staged promoted workbench status-bar posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-status-bar-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.md` defines the source-staged promoted workbench inspector-rail posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-inspector-rail-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.md` defines the source-staged promoted workbench dialog-stack posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-dialog-stack-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.md` defines the source-staged promoted workbench context-actions posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-context-actions-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.md` defines the source-staged promoted workbench search/filter posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-search-filter-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.md` defines the source-staged promoted workbench layout-presets posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-layout-presets-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.md` defines the source-staged promoted workbench activity-feed posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-activity-feed-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.md` defines the source-staged promoted workbench keyboard-shortcuts posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-keyboard-shortcuts-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.md` defines the source-staged promoted workbench resource-meters posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-resource-meters-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.md` defines the source-staged promoted workbench tree-tools posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-tree-tools-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.md` defines the source-staged promoted workbench save-session posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-save-session-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.md` defines the source-staged promoted workbench output-handoff posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-output-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.md` defines the source-staged promoted workbench validation-queue posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-validation-queue-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.md` defines the source-staged promoted workbench history-undo posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-history-undo-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.md` defines the source-staged promoted workbench sync-presence posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-sync-presence-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.md` defines the source-staged promoted workbench data-packs posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-data-packs-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.md` defines the source-staged promoted workbench character-library posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-character-library-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.md` defines the source-staged promoted workbench campaign-session posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-campaign-session-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.md` defines the source-staged promoted workbench observability-privacy posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-observability-privacy-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.md` defines the source-staged promoted workbench first-run posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-first-run-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.md` defines the source-staged promoted workbench PWA-install posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-pwa-install-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.md` defines the source-staged promoted workbench Docker-operator posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-docker-operator-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.generated.json`.

## Portal Installer Handoff

- `docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md` defines the source-staged browser-to-portal download/install/support handoff contract. It is not runtime proof.

## Docker Self-Host Operator Contract

- `docs/BLAZOR_DOCKER_SELF_HOST_OPERATOR_PROOF.md` defines the source-staged self-hosted Docker operator contract for the portal-backed Blazor browser client. It is source alignment only, not Docker runtime proof.

## Account and Support Handoff

- `docs/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md` defines the source-staged account/support/status handoff contract for the portal-backed Blazor browser client. It is not authentication, authorization, owner-propagation, or support-submission runtime proof.

## Runtime Proof Refresh Plan

- `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md` defines the ordered runtime proof refresh path from source-staged Blazor work to Docker self-host proof, hosted route-entry proof, hosted execution proof, and browser-lane aggregate proof. It is a plan, not runtime proof.
- `scripts/print_blazor_public_edge_proof_status.py` reports the runtime proof refresh plan separately as source-plan evidence only.
- `docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json`.

## Staged-to-Runtime Promotion Matrix

- `docs/BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.md` maps source-staged browser workflow families to the Docker and hosted runtime receipts required before they can be promoted from staged breadth to browser-proven parity.
- `scripts/print_blazor_public_edge_proof_status.py` reports the staged-to-runtime promotion matrix separately as source-plan evidence only.
- `docs/examples/blazor-staged-to-runtime-promotion-matrix.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.generated.json`.
