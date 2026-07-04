# Blazor Web Client Docs Index

## Purpose

This index defines the current documentation set for the Chummer6 browser-client lane.

The goal is straightforward: `Chummer.Blazor` should ship as a polished web client on `chummer.run`, preserve the same practical user workflow as the Avalonia desktop client, and remain self-hostable through Docker without splitting the product story.

## Primary Documents

- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`: main design spec, product posture, Chummer Online route alias, parity rules, workflow bar, and required proof standard
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#extended-goal-scope`: extended remaining-goal ledger for regenerated receipts, validation gates, runtime/Docker proof refresh, Avalonia parity breadth, reusable character-statistics calculations, public `/app` route posture, privacy-safe analytics, and cross-surface visual polish
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md#release-evidence-boundary`: parity-goal release boundary that keeps source-staged, source-plan, and source-calculation receipts as planning evidence only until refreshed hosted route-entry, hosted execution, Docker self-host, analytics, connected-runtime, source-boundary, and aggregate browser-lane receipts back the claim
- `docs/MIGRATION_BACKLOG.md`: backlog contract for browser-client promotion, parity milestones, and remaining implementation gaps
- `docs/WORKBENCH_RELEASE_SIGNOFF.md`: release-signoff posture for Chummer Online routes, /blazor/workbench compatibility routes, and browser workflow proof
- `docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers`: release blocker ledger that prevents source-staged, source-plan, or source-calculation receipts from being treated as release evidence before hosted route-entry, hosted execution, Docker self-host, aggregate browser-lane, Rybbit privacy, Runner Intelligence, roster hierarchy, and cross-surface visual polish proof is refreshed

## Hosted/Public-Edge Proof Documents

- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`: contract for hosted `chummer.run` route-entry posture proof, including clean public `/app`, roster-start `/app?command=character_roster` with the `public_chummer_app_roster_route` marker, roster-first `/blazor/` redirect with the `public_blazor_root_redirect` marker, hosted `/blazor/app`, `/blazor/home` roster-first orientation route with the `public_blazor_home_roster_entry` marker, and canonical proof-compatible `/blazor/workbench` route family
- `scripts/verify_blazor_public_edge_workbench_proof.py`: structural verifier for the hosted route-entry receipt contract covering clean public `/app`, hosted `/blazor/app`, and proof-compatible `/blazor/workbench`
- `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`: milestone-style wrapper for the hosted route-entry verifier covering clean public `/app`, hosted `/blazor/app`, and proof-compatible `/blazor/workbench`
- `docs/examples/blazor-public-edge-workbench-proof.receipt.example.json`: example hosted route-entry receipt shape covering clean public `/app`, hosted `/blazor/app`, roster-first `/blazor/home`, and proof-compatible `/blazor/workbench`
- `docs/examples/blazor-public-edge-workbench-proof.expanded.receipt.example.json`: expanded example hosted route-entry receipt shape covering clean public `/app`, hosted `/blazor/app`, roster-first `/blazor/home`, and proof-compatible `/blazor/workbench`
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`: contract for hosted `chummer.run` browser workflow execution proof
- `scripts/verify_blazor_public_edge_execution_proof.py`: structural verifier for the hosted execution-proof receipt contract
- `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`: milestone-style wrapper for the hosted execution-proof verifier
- `docs/examples/blazor-public-edge-execution-proof.receipt.example.json`: example hosted execution-proof receipt shape
- `scripts/materialize-blazor-public-edge-execution-horizon.py`: generated horizon receipt that makes current smoke execution proof, unproven full live public-edge matrix breadth, failed-run sidecar posture, and no-smoke-to-full promotion boundaries explicit
- `scripts/materialize-blazor-pwa-public-edge-proof.py`: live public-edge materializer for the clean `/app` entry plus hosted `/blazor` installable PWA shell, `/pwa` player companion alias, direct `/mobile` player shell, player manifest, manifest route targets, mobile living-world opt-in boundary, account ledger notifications opt-in boundary, static-cache privacy boundary, offline copy, app-head service-worker registration, static deployed assets, and mobile viewport posture
- `scripts/verify_blazor_pwa_public_edge_proof.py`: structural verifier for `.codex-studio/published/BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json`
- `scripts/materialize-chummer6-public-edge-flagship-proof.py`: live public-edge integration materializer tying the current downloads shelf, public navigation routes, Blazor runtime health, Player/GM PWA role shells, Black Ledger/heat opt-in boundary, static asset paths, and working horizons into `.codex-studio/published/CHUMMER6_PUBLIC_EDGE_FLAGSHIP_INTEGRATION.generated.json` without claiming stable/gold or authenticated living-world execution
- `scripts/print_blazor_public_edge_proof_status.py`: shared status summary utility for self-host proof, hosted route-entry proof including `route_public_chummer_app`, `route_public_chummer_app_roster`, `route_public_blazor_root_redirect`, and `route_public_blazor_home_roster_entry`, hosted execution proof, analytics posture including session-replay and autocapture policy lines, connected-runtime posture, aggregate source-check counts, and external-host blocker receipts
- `scripts/print_blazor_public_edge_proof_status.py`: also reports the hosted `/blazor` PWA proof receipt, base URL, public entry URL, `/pwa` player companion alias, `/mobile` player shell URL, proof tier, route lane, and required manifest/service-worker/offline/app-head/clean-public-entry/mobile-player-shell/player-manifest/manifest-route-targets/mobile-living-world/account-ledger-opt-in/static-asset/mobile-viewport check ids so operators can confirm the Play shell is installable without treating runner, workspace, API, Black Ledger, heat, or session data as offline cacheable
- `scripts/print_blazor_public_edge_proof_status.py`: also reports the public-edge flagship integration receipt, scope, status, check count, and route/download/PWA/living-world/static/horizon check ids so operators can distinguish deployed preview coherence from stable/gold promotion
- `scripts/print_blazor_public_edge_proof_status.py`: also exposes aggregate browser-lane boundary visibility through `aggregate_note_count`, `aggregate_source_boundary_policy_note`, and `aggregate_migration_boundary_note`, so operators can see whether regenerated aggregate receipts carry the source-policy-only guard and `MIG-106` through `MIG-109` open-until-refreshed-proof posture
- `scripts/print_blazor_public_edge_proof_status.py`: also reports the optional staged career/support source-alignment receipt when it has been generated; that status line is not browser execution proof
- `scripts/materialize-blazor-browser-lane-proof-set.py`: aggregate proof-set materializer that fails unless the required browser-lane receipts are all in their expected passing/ready states and the aggregate example receipt source shape still documents hosted route-marker and analytics policy checks
- `scripts/materialize-blazor-browser-lane-proof-set.py`: treats hosted execution breadth as scope-aware evidence, requiring every smoke-required family for smoke receipts and every full-required family for full receipts before aggregate acceptance
- `scripts/materialize-blazor-browser-lane-proof-set.py`: also requires `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json` as source-policy input only, so the aggregate browser-lane receipt proves staged/source-plan/source-calculation receipts stayed out of release-readiness aggregation without treating that guard as hosted or Docker workflow execution
- `scripts/materialize-blazor-browser-lane-proof-set.py`: generated aggregate receipts also carry notes that `source_staged_release_boundary` is source-policy evidence only and that `MIG-106` through `MIG-109` remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim
- `scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh`: milestone-style wrapper for the aggregate browser-lane proof set
- `docs/examples/blazor-browser-lane-proof-set.receipt.example.json`: compact aggregate proof-set receipt shape, including the hosted route-entry marker minimum, analytics replay/autocapture policy checks, and the source-staged release-boundary guard input that keeps `MIG-106` through `MIG-109` open until refreshed runtime proof exists
- `docs/BLAZOR_CAREER_SUPPORT_STAGED_PROOF.md`: source-staged contract for the next career/support workflow refresh, including restored calendar continuity, career entry add/edit/delete, dossier notes, and move up/down utilities
- `scripts/materialize-blazor-career-support-staged-proof.py`: source-structural staged proof materializer for the next career/support workflow refresh
- `scripts/ai/milestones/blazor-career-support-staged-proof-check.sh`: milestone-style wrapper for the staged career/support source-alignment check
- `docs/examples/blazor-career-support-staged-proof.receipt.example.json`: example receipt shape for the staged career/support source-alignment proof
- `docs/BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.md`: source-staged contract for identity/SIN/license utility posture under restored `tab-info`
- `scripts/materialize-blazor-identity-license-staged-proof.py`: source-structural staged proof materializer for identity/SIN/license utility posture
- `docs/examples/blazor-identity-license-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json`
- `docs/BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.md`: source-staged contract for combat support utility posture under restored `tab-combat`
- `scripts/materialize-blazor-combat-support-staged-proof.py`: source-structural staged proof materializer for combat support utility posture
- `docs/examples/blazor-combat-support-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json`
- `docs/BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.md`: source-staged contract for skill maintenance utility posture under restored `tab-skills`
- `scripts/materialize-blazor-skill-maintenance-staged-proof.py`: source-structural staged proof materializer for skill maintenance utility posture
- `docs/examples/blazor-skill-maintenance-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json`
- `docs/BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.md`: source-staged contract for magic/resonance support utility posture across restored adept, magician, critter, and technomancer lanes
- `scripts/materialize-blazor-magic-support-staged-proof.py`: source-structural staged proof materializer for magic/resonance support utility posture
- `docs/examples/blazor-magic-support-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json`
- `docs/BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.md`: source-staged contract for gear maintenance utility posture under restored `tab-gear`
- `scripts/materialize-blazor-gear-maintenance-staged-proof.py`: source-structural staged proof materializer for gear maintenance utility posture
- `docs/examples/blazor-gear-maintenance-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json`
- `docs/BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.md`: source-staged contract for Runner Intelligence percentile benchmarks, spell/drug/gear what-if stacks, inventory synergy, and privacy-safe cohort posture
- `Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs`: shared Runner Intelligence calculation contract for Blazor and Avalonia clients, including reusable percentile and drain/stun risk methods on `IRunnerIntelligenceCalculator`
- `Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs`: also owns `RunnerIntelligencePrivacy.DefaultExcludedFields` and named sensitive-field constants for shared Blazor/Avalonia cohort privacy posture
- `Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceSampleFactory.cs`: reusable Increase Initiative scenario and sample fixtures for shared Runner Intelligence calculation posture, including named sample constants for default runner/ruleset/cohort values, scenario id, Initiative stat key, Initiative delta, Jazz inventory key, resistance pool, threshold, incoming severity, the staged 87.3% risk percentage, and the DI-friendly scenario catalog used by Blazor and Avalonia so UI heads do not duplicate the exact value
- `Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceBridge.cs`: Avalonia-facing bridge that delegates report calculation, percentile ranking, risk estimation, and Increase Initiative scenario construction to the shared Runner Intelligence calculator and scenario catalog
- `Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligenceServiceCollectionExtensions.cs`: Blazor DI registration helper for the shared Runner Intelligence calculator
- `Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligencePreviewService.cs`: Blazor preview service that renders the shared Increase Initiative sample through the shared calculator
- `Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceServiceCollectionExtensions.cs`: desktop-runtime DI registration helper for the shared Runner Intelligence calculator and Avalonia bridge
- `scripts/materialize-blazor-runner-intelligence-staged-proof.py`: source-structural staged proof materializer for Runner Intelligence source posture
- `scripts/ai/milestones/blazor-runner-intelligence-staged-proof-check.sh`: milestone-style wrapper for the Runner Intelligence source-alignment check
- `docs/examples/blazor-runner-intelligence-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json`
- `docs/BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.md`: source-calculation proof contract for the shared Runner Intelligence calculator seam and Increase Initiative sample semantics
- `scripts/materialize-blazor-runner-intelligence-calculation-proof.py`: source-calculation proof materializer for shared Runner Intelligence calculation posture
- `scripts/ai/milestones/blazor-runner-intelligence-calculation-proof-check.sh`: milestone-style wrapper for the Runner Intelligence source-calculation check
- `docs/examples/blazor-runner-intelligence-calculation-proof.receipt.example.json`: example receipt shape for `BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json`
- `scripts/materialize-blazor-workbench-import-reconcile-staged-proof.py`: source-structural staged proof materializer for import/reconcile workbench posture
- `scripts/ai/milestones/blazor-workbench-import-reconcile-staged-proof-check.sh`: milestone-style wrapper for the import/reconcile source-alignment check
- `docs/examples/blazor-workbench-import-reconcile-staged-proof.receipt.example.json`: example receipt shape for the import/reconcile source-alignment proof
- `scripts/materialize-blazor-workbench-compare-merge-staged-proof.py`: source-structural staged proof materializer for compare/merge workbench posture
- `scripts/ai/milestones/blazor-workbench-compare-merge-staged-proof-check.sh`: milestone-style wrapper for the compare/merge source-alignment check
- `docs/examples/blazor-workbench-compare-merge-staged-proof.receipt.example.json`: example receipt shape for the compare/merge source-alignment proof
- `scripts/materialize-blazor-workbench-restore-checkpoint-staged-proof.py`: source-structural staged proof materializer for restore/checkpoint workbench posture
- `scripts/ai/milestones/blazor-workbench-restore-checkpoint-staged-proof-check.sh`: milestone-style wrapper for the restore/checkpoint source-alignment check
- `docs/examples/blazor-workbench-restore-checkpoint-staged-proof.receipt.example.json`: example receipt shape for the restore/checkpoint source-alignment proof
- `scripts/materialize-blazor-workbench-offline-cache-staged-proof.py`: source-structural staged proof materializer for offline/cache workbench posture
- `scripts/ai/milestones/blazor-workbench-offline-cache-staged-proof-check.sh`: milestone-style wrapper for the offline/cache source-alignment check
- `docs/examples/blazor-workbench-offline-cache-staged-proof.receipt.example.json`: example receipt shape for the offline/cache source-alignment proof
- `scripts/materialize-blazor-workbench-session-locking-staged-proof.py`: source-structural staged proof materializer for session-locking workbench posture
- `scripts/ai/milestones/blazor-workbench-session-locking-staged-proof-check.sh`: milestone-style wrapper for the session-locking source-alignment check
- `docs/examples/blazor-workbench-session-locking-staged-proof.receipt.example.json`: example receipt shape for the session-locking source-alignment proof
- `scripts/materialize-blazor-workbench-share-export-privacy-staged-proof.py`: source-structural staged proof materializer for share/export privacy workbench posture
- `scripts/ai/milestones/blazor-workbench-share-export-privacy-staged-proof-check.sh`: milestone-style wrapper for the share/export privacy source-alignment check
- `docs/examples/blazor-workbench-share-export-privacy-staged-proof.receipt.example.json`: example receipt shape for the share/export privacy source-alignment proof
- `scripts/materialize-blazor-workbench-table-handoff-staged-proof.py`: source-structural staged proof materializer for table-handoff workbench posture
- `scripts/ai/milestones/blazor-workbench-table-handoff-staged-proof-check.sh`: milestone-style wrapper for the table-handoff source-alignment check
- `docs/examples/blazor-workbench-table-handoff-staged-proof.receipt.example.json`: example receipt shape for the table-handoff source-alignment proof
- `scripts/materialize-blazor-workbench-rules-citation-staged-proof.py`: source-structural staged proof materializer for rules-citation workbench posture
- `scripts/ai/milestones/blazor-workbench-rules-citation-staged-proof-check.sh`: milestone-style wrapper for the rules-citation source-alignment check
- `docs/examples/blazor-workbench-rules-citation-staged-proof.receipt.example.json`: example receipt shape for the rules-citation source-alignment proof
- `scripts/materialize-blazor-workbench-localization-terminology-staged-proof.py`: source-structural staged proof materializer for localization/terminology workbench posture
- `scripts/ai/milestones/blazor-workbench-localization-terminology-staged-proof-check.sh`: milestone-style wrapper for the localization/terminology source-alignment check
- `docs/examples/blazor-workbench-localization-terminology-staged-proof.receipt.example.json`: example receipt shape for the localization/terminology source-alignment proof
- `scripts/materialize-blazor-workbench-help-recovery-guidance-staged-proof.py`: source-structural staged proof materializer for help/recovery guidance workbench posture
- `scripts/ai/milestones/blazor-workbench-help-recovery-guidance-staged-proof-check.sh`: milestone-style wrapper for the help/recovery guidance source-alignment check
- `docs/examples/blazor-workbench-help-recovery-guidance-staged-proof.receipt.example.json`: example receipt shape for the help/recovery guidance source-alignment proof
- `scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py`: source-structural staged proof materializer for Character Roster custom hierarchy posture
- `scripts/ai/milestones/blazor-workbench-roster-hierarchy-staged-proof-check.sh`: milestone-style wrapper for the roster hierarchy source-alignment check
- `docs/BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.md`: source-staged contract for custom dossier roster hierarchy posture, custom roster directories, and directory-backed roster organization on the primary `/blazor/app` route plus `/blazor/workbench` compatibility, nested hierarchy of the user's choosing, drag/drop move intent, keyboard-accessible row handling, explicit `data-roster-line-kind` and `data-roster-folder-scope` selectors, DialogHost-local roster field-id, folder-scope, and drag/drop mutation command constants, system library buckets as drop targets but not draggable source directories, polished amber/mint/blue hierarchy treatment, pending organization status, roster-first public home hero route pills derived from `AppRoute`, `HomeRoute`, `CharacterRosterCommand`, `RosterRoute`, and `PublicRosterRoute`, reduced-motion-safe reveal, high-contrast affordances, mobile-softened grid density, non-destructive metadata staging, and runtime boundary language
- `Chummer.Presentation/Overview/RosterHierarchyState.cs`: shared roster hierarchy state plus `RosterHierarchyStateJson` normalization/validation and `RosterHierarchyMetadata` constants for Avalonia and web-client reuse, with the roster dialog factory and mutation coordinator using those constants for generated/staged source, Active Table, Saved Runners, Inbox, Watch Folder Links, User Directories, and System Directories semantics
- `Chummer.Blazor/Components/Pages/Preview.razor`: Chummer Online and `/blazor/workbench` restored-workspace route source, including centralized `WorkspaceQueryName` generation for `workspace=...` links plus `LegacyRunnerQueryName` acceptance for older `runner=...` links
- `docs/examples/blazor-workbench-roster-hierarchy-staged-proof.receipt.example.json`: example receipt shape for the roster hierarchy source-alignment proof
- `docs/BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.md`: source-staged breadth guard for known legacy UI control IDs mapped into hosted execution baseline coverage or staged source-alignment families
- `scripts/materialize-blazor-legacy-control-coverage-staged-proof.py`: source-structural materializer for legacy control coverage breadth
- `docs/examples/blazor-legacy-control-coverage-staged-proof.receipt.example.json`: example receipt shape for `BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json`
- `.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json`: published aggregate browser-lane proof-set receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json`: published hosted execution horizon receipt showing current smoke proof status and whether the full live public-edge execution matrix is actually proven
- `.codex-studio/published/BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json`: published hosted `/blazor` PWA install-shell receipt covering manifest, service worker, offline living-world boundary copy, app-head registration, clean `/app` entry, `/pwa` player companion alias, `/mobile` player shell, player manifest, manifest route targets, mobile living-world opt-in boundary, account ledger notifications opt-in boundary, static deployed assets, mobile viewport posture
- `.codex-studio/published/CHUMMER6_PUBLIC_EDGE_FLAGSHIP_INTEGRATION.generated.json`: published live integration receipt covering current release manifests, downloads page/install routes, public navigation, Blazor runtime health, Player/GM PWA role shells, Black Ledger/heat opt-in boundary, static asset paths, and near/mid/long horizon boundaries for preview deployment
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`: published hosted execution-proof receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`: published hosted route-entry proof receipt covering clean public `/app`, roster-start `/app?command=character_roster`, hosted `/blazor/app`, and proof-compatible `/blazor/workbench`

## Self-Host and Operator Documents

- `docs/BLAZOR_SELF_HOST_RUNBOOK.md`: canonical Docker and operator runbook for Chummer Online and the browser-client lane
- `docs/examples/self-hosted-browser-workbench.env.example`: baseline environment defaults for self-hosted portal/API/browser deployments
- `docs/DESKTOP_RELEASE_PIPELINE.md`: release pipeline notes that still need to stay aligned with the promoted browser route model
- `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`: published Docker self-host browser-client receipt included in the combined browser-lane proof status summary

## Analytics and Privacy Posture

- `scripts/materialize-blazor-analytics-posture-proof.py`: proof materializer for optional browser analytics wiring and privacy boundaries
- `scripts/ai/milestones/blazor-analytics-posture-check.sh`: milestone-style wrapper for the analytics posture proof
- `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`: published analytics posture receipt
- `docs/examples/blazor-analytics-posture.receipt.example.json`: compact example receipt shape showing the route metadata policy, self-host default, hosted Rybbit posture, and disabled replay/autocapture fields

Hosted `chummer.run` may enable the Rybbit adapter for the Blazor web client, but self-hosted Docker defaults keep analytics disabled with `CHUMMER_ANALYTICS_PROVIDER=none`. Chummer Online route telemetry uses the sanitized `chummer_app` route family and route/workflow metadata only, with explicit no session replay and no autocapture posture. The browser bridge enforces an allowlist for host class, analytics policy, route family, command, tab, control, dialog action, and boolean fixture/workspace/dossier presence. The Blazor `/health` contract exposes non-secret `allowedMetadataFields` and `excludedDataClasses` so operators can see the Rybbit boundary without receiving dossier metadata, owner identifiers, workspace or dossier identifiers, XML, payloads, hashes, or dossier content.

The adapter is limited to route/workflow metadata such as host class, route family, command id, tab id, control id, dialog action id, boolean fixture/workspace/dossier presence, and the analytics scope/session replay/autocapture policy fields. Browser event prefix, browser route event name, route-family values, route path markers, event property keys, and query-parameter inputs are named inside the client event bridge so Chummer Online, preview, showcase, downloads, docs, fallback traffic, and emitted Rybbit payload keys do not drift into ad-hoc analytics strings. The positive allowlist owns safe boolean presence keys such as `has_workspace` and `has_dossier`, and those presence fields remain booleans in the emitted payload, while the sensitive-key denylist remains a guard for non-allowlisted keys. It must not emit runner names, aliases, owner identifiers, workspace or dossier identifiers, file names, document contents, XML, payloads, hashes, or generated dossier text.

The Blazor health endpoint exposes the non-secret posture fields `selfHostDefault`, `hostedPublicEdge`, `sensitiveDataPolicy`, `sessionReplayPolicy`, `autocapturePolicy`, `allowedMetadataFields`, and `excludedDataClasses` so operators can confirm analytics defaults and the metadata/privacy boundary without exposing credentials. Provider names, posture values, and `allowedMetadataFields` are built from named analytics constants, while `excludedDataClasses` is built from `RunnerIntelligencePrivacy.DefaultExcludedFields` plus named analytics-specific payload and hash exclusions.

Runner Intelligence cohort/privacy surfaces should render sensitive-field policy from `RunnerIntelligencePrivacy.DefaultExcludedFields` so character-statistics privacy, hosted Rybbit posture, and self-host docs stay aligned.

## Connected Runtime Posture

- `scripts/materialize-blazor-connected-runtime-posture-proof.py`: proof materializer for optional session, coach, and assistant forwarding posture
- `scripts/ai/milestones/blazor-connected-runtime-posture-check.sh`: milestone-style wrapper for connected-runtime posture proof
- `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`: published connected-runtime posture receipt

Connected runtime proof is deliberately narrower than workflow parity. It proves that optional session, coach, and assistant routes can remain behind the portal boundary and use the signed portal-owner forwarding seam when configured. It does not prove that every downstream connected-runtime workflow is complete.

The browser client also renders a connected-runtime posture card showing whether session, coach, and assistant lanes are configured or off. The card must not expose proxy URLs or owner secrets.

## Route and Workflow Canon

The browser lane currently uses this public route posture:

- `/blazor/`: stable hosted entry that resolves into Chummer Online and immediately opens the Character Roster workflow via `app?command=character_roster`
- `/blazor/home`: explicit product/orientation page for public copy and self-host evaluation
- `/app`: clean public Chummer Online route for public CTAs and product navigation
- `/blazor/app`: hosted Blazor app path for the dense operational shell
- `/blazor/workbench`: /blazor/workbench compatibility route for the same promoted Chummer Online client
- `/blazor/preview`: proof/support route, not the primary product promise

Parity claims should be evaluated against workflow families, not against visual resemblance alone:

- startup and recent-work recovery
- runner creation and origin/rules selection
- dense runner dossier editing across core sections
- dialog-driven add/edit/commit loops
- browser result continuations for save, export, print, and download
- cross-route continuity between workbench, support, downloads, account, and optional coach/session surfaces

The hosted execution contract already spans more than startup posture alone. Its current promoted family set includes:

- startup command execution and dense startup utilities
- origin/rules continuity and build-lab continuity
- dense selectors on gear, qualities, magic, and cyberware lanes
- in-place edit and delete/recovery utilities across contacts, gear, qualities, magic, and cyberware
- resumed result, action, committed-action, and advanced-action families

The next hosted and self-host proof refresh is staged to add the career/support workflow family through the canonical proof-compatible route. That staged family covers `tab-calendar` section resume, `create_entry`, `create_entry&dialog_action=add`, `edit_entry`, `edit_entry&dialog_action=apply`, `delete_entry`, `delete_entry&dialog_action=delete`, `open_notes`, `open_notes&dialog_action=save`, `move_up`, and `move_down`. It remains staged until the public-edge and Docker receipts are regenerated from the updated proof runners.

`scripts/ai/milestones/blazor-career-support-staged-proof-check.sh` can be used before the live proof refresh to verify that product UI, hosted route-entry probing, hosted execution probing, Docker self-host probing, receipt metadata, status reporting, and docs still agree about that staged route family.

## Current Documentation Truth

The documentation set is ahead of full proof completion by design.

Current docs already define the intended shipped posture and the evidence contract. The remaining work is to expand browser-specific proof and receipts until the hosted and self-hosted lanes both support the same release claim.

## Portable Proof Tooling

- `CHUMMER_PUBLIC_EDGE_COMPOSE_PATH`: override the public-edge compose file used by analytics/public-edge posture proof tooling when `chummer.run-services` is not checked out next to `chummer-presentation`.
- `CHUMMER_DESIGN_PRODUCT_ROOT`: override the Chummer5A design product root used by `scripts/ai/verify_chummer5a_human_parity.py` and `scripts/materialize_chummer5a_full_ui_parity_artifacts.py`.
- `CHUMMER5A_ORACLE_ROOT`: override the Chummer5A oracle docs root used by `scripts/materialize_chummer5a_full_ui_parity_artifacts.py`.
- `CHUMMER5A_REPO_PATH`: canonical override for the legacy Chummer5A local repo path used by `scripts/chummer5a_parity_tester.py`; path-like `CHUMMER5A_REPO_URL` values remain accepted only for compatibility, while URL-shaped values are not treated as filesystem paths.
- `CHUMMER5A_PARITY_LAB_ROOT`: override the EA parity-lab fixture, oracle-baseline, and veteran-workflow pack root used by `scripts/chummer5a_parity_tester.py`.
- `CHUMMER_PLAYWRIGHT_NODE_PATH`: optional explicit Playwright `node_modules` path checked by self-host and hosted browser proof runners before workspace-relative fallback locations.
- `CHUMMER_PLAYWRIGHT_ROOT`: optional Playwright workspace root; browser proof runners check `$CHUMMER_PLAYWRIGHT_ROOT/node_modules` after `CHUMMER_PLAYWRIGHT_NODE_PATH`.
- `scripts/chummer5a_parity_tester.py` records `chummer5aRepoDefaultSource` and `chummer5aRepoPathSource` in its metadata so operators can see whether the legacy Chummer5A repo path came from `CHUMMER5A_REPO_PATH`, compatibility `CHUMMER5A_REPO_URL`, the sibling default, or a CLI argument. `chummer5aRepoPathSource=cli_argument` is emitted only when `--chummer5a-repo` is actually present, not merely when the resolved path differs from the default.

The proof/status scripts should derive the `chummer-presentation` repository root from their own file location rather than from a machine-local checkout path. Environment variables are reserved for adjacent external inputs such as public-edge compose, design product data, and oracle docs.

Hosted execution proof uses portable Playwright lookup in `scripts/e2e-public-edge-execution.sh`: `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`. Do not hardcode `/docker/chummercomplete` for Playwright discovery in new browser proof runners.

Docker self-host portal proof uses the same portability rule in `scripts/e2e-portal.sh`: compose, route-probe, and Playwright script defaults are derived from the script-relative `chummer-presentation` repo root, while local Playwright lookup checks `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `$CHUMMER_PLAYWRIGHT_ROOT/node_modules`, sibling `chummer.run-services/node_modules`, sibling `node_modules`, then `scripts/node_modules`.

## Source-Staged Proof Set

- `docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md` documents the source-staged Blazor proof-set refresh path, including the route roles for `/app`, `/blazor/app`, `/blazor/workbench`, and `/blazor/preview` under route lane `chummer_app_proof_compatible_workbench_preview_tools`. This lane is source-alignment only and must remain separate from hosted execution proof, Docker self-host proof, and release-readiness aggregation.
- `docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md#promotion-rule` links staged-proof promotion to the parity release boundary, runtime refresh gates, and release blockers so staged receipts can hand off to hosted/Docker proof refresh without becoming release evidence.
- `docs/MIGRATION_BACKLOG.md#browser-client-release-evidence-boundary` keeps `MIG-106` through `MIG-109` open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim.
- `docs/examples/blazor-source-staged-proof-set.receipt.example.json` shows the aggregate receipt shape for `BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json`, including `source_contract_check_count` for non-receipt source contract checks, `source_contract_checks.runbook_route_roles`, `source_contract_checks.docs_index_route_roles`, and explicitly separated source-calculation receipts such as Runner Intelligence calculation proof. The status utility reports `source_staged_proof_set_route_lane` and `source_staged_proof_set_source_contract_checks`.
- `docs/BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.md` defines the source-staged browser save, save-as, export, print, and download handoff contract. It is not runtime proof.
- `docs/examples/blazor-browser-output-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.md` defines the source-staged workbench downloads/status/support/help/account handoff contract. It is not runtime proof.
- `docs/examples/blazor-workbench-portal-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.md` defines the source-staged Chummer Online and compatibility route polish contract for the task dock, compatibility route boundary where `/app` remains the clean public Chummer Online route, `/blazor/app` remains the hosted app path, and `/blazor/preview` remains the preview tools/result-state route, primary Character Roster/New runner/Open-import startup actions, startup command links for roster/new/import/origin flows derived from local command constants, output command links for save/save-as/export/print flows derived from local command constants, setup and rules command links derived from local command constants, keyboard-visible focus, mobile touch posture, portal-handoff header nav treatment, keyboard-visible portal nav focus, slate/amber/mint/blue app theme, refined amber/mint/blue app theme pass, route-token app chrome treatment, mobile route-token wrapping, keyboard-visible route-token focus, high-contrast route-token affordances, route-aware status strip chrome, and route-state status pill styling. It is not runtime proof.
- `docs/examples/blazor-workbench-polish-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route session recovery contract. It is not runtime proof.
- `docs/examples/blazor-workbench-recovery-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route hosting and privacy posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-hosting-privacy-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route command-palette posture contract, including the same-origin `/help` command. It is not runtime proof.
- `docs/examples/blazor-workbench-command-palette-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route density posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-density-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route workflow-ledger posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-workflow-ledger-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route file-intake posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-file-intake-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route rules/data posture contract, including the same-origin `/help` rules/data guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-rules-data-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RULES_DATA_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route settings/preferences posture contract, including the same-origin `/help` settings guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-settings-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route diagnostics posture contract, including the same-origin `/help` diagnostics guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-diagnostics-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route connected-runtime posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-connected-runtime-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CONNECTED_RUNTIME_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route accessibility posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-accessibility-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route section-rail posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-section-rail-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route desktop install handoff contract, including the same-origin `/help` recovery shortcut, native desktop installer progress chrome with an amber accent bar, deep slate shell, mint progress fill, warm ink metadata, and amber hint text, and native installer high-contrast system-color fallback. It is not runtime proof.
- `docs/examples/blazor-workbench-desktop-install-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route desktop menu-bar posture contract, including the same-origin `/help` menu handoff. It is not runtime proof.
- `docs/examples/blazor-workbench-menu-bar-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route workspace-tabs posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-workspace-tabs-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route status-bar posture contract, including the same-origin `/help` recovery cue. It is not runtime proof.
- `docs/examples/blazor-workbench-status-bar-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_STATUS_BAR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route inspector-rail posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-inspector-rail-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route dialog-stack posture contract, including the same-origin `/help` recovery continuation. It is not runtime proof.
- `docs/examples/blazor-workbench-dialog-stack-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DIALOG_STACK_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route context-actions posture contract, including the same-origin `/help` selection guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-context-actions-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route search/filter posture contract, including the same-origin `/help` filter-recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-search-filter-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route layout-presets posture contract, including the same-origin `/help` layout-recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-layout-presets-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route activity-feed posture contract, including the same-origin `/help` recovery event. It is not runtime proof.
- `docs/examples/blazor-workbench-activity-feed-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_ACTIVITY_FEED_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route keyboard-shortcuts posture contract, including the same-origin `/help` keyboard-guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-keyboard-shortcuts-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route resource-meters posture contract. It is not runtime proof.
- `docs/examples/blazor-workbench-resource-meters-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_RESOURCE_METERS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route tree-tools posture contract, including the same-origin `/help` dense-list guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-tree-tools-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route save-session posture contract, including the same-origin `/help` save/session recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-save-session-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route output-handoff posture contract, including the same-origin `/help` output-recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-output-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_OUTPUT_HANDOFF_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route validation-queue posture contract, including the same-origin `/help` validation-guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-validation-queue-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_VALIDATION_QUEUE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route history-undo posture contract, including the same-origin `/help` rollback guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-history-undo-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_HISTORY_UNDO_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route sync-presence posture contract, including the same-origin `/help` sync/offline recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-sync-presence-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route data-packs posture contract, including the same-origin `/help` rules/data-pack guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-data-packs-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route Character Roster dossier-management posture contract, including the same-origin `/help` roster/import recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-character-library-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route campaign-session posture contract, including the same-origin `/help` campaign/table handoff recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-campaign-session-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route observability-privacy posture contract, including the same-origin `/help` analytics/privacy guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-observability-privacy-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route first-run posture contract, including the same-origin `/help` onboarding/setup recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-first-run-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route PWA-install posture contract, including the same-origin `/help` install/cache/permissions recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-pwa-install-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route Docker-operator posture contract, including the same-origin `/help` self-host operator recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-docker-operator-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_DOCKER_OPERATOR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route security-access posture contract, including the same-origin `/help` sign-in/access recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-security-access-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route notifications-jobs posture contract, including the same-origin `/help` async-work recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-notifications-jobs-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route touch-mobile posture contract, including the same-origin `/help` touch/mobile recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-touch-mobile-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route navigation-deeplink posture contract, including the same-origin `/help` route/deep-link recovery action. It is not runtime proof.
- `docs/examples/blazor-workbench-navigation-deeplink-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_INLINE_EDITING_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route inline-editing posture contract, including the same-origin `/help` edit guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-inline-editing-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_INLINE_EDITING_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route performance-virtualization posture contract, including the same-origin `/help` large-dossier performance guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-performance-virtualization-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PERFORMANCE_VIRTUALIZATION_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PRINT_LAYOUT_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route print-layout posture contract, including the same-origin `/help` dossier-output guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-print-layout-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PRINT_LAYOUT_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route portrait-attachments posture contract, including the same-origin `/help` media/storage guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-portrait-attachments-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PORTRAIT_ATTACHMENTS_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route windowing-panes posture contract, including the same-origin `/help` pane/window layout guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-windowing-panes-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_CALCULATION_PROVENANCE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route calculation-provenance posture contract, including the same-origin `/help` calculation-explainability guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-calculation-provenance-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_CALCULATION_PROVENANCE_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_LIFECYCLE_CALENDAR_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route lifecycle-calendar posture contract, including the same-origin `/help` downtime/upkeep guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-lifecycle-calendar-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_LIFECYCLE_CALENDAR_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route progression-ledger posture contract, including the same-origin `/help` advancement-accounting guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-progression-ledger-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF.generated.json`.
- `docs/BLAZOR_WORKBENCH_IMPORT_RECONCILE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route import/reconcile posture contract, including the same-origin `/help` existing-dossier import guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_COMPARE_MERGE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route compare/merge posture contract, including the same-origin `/help` merge-review guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route restore/checkpoint posture contract, including the same-origin `/help` recovery/checkpoint guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_OFFLINE_CACHE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route offline/cache posture contract, including the same-origin `/help` offline/cache guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_SESSION_LOCKING_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route session-locking posture contract, including the same-origin `/help` edit-ownership guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_SHARE_EXPORT_PRIVACY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route share/export privacy posture contract, including the same-origin `/help` private-handoff guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route table-handoff posture contract, including the same-origin `/help` table-output guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_RULES_CITATION_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route rules-citation posture contract, including the same-origin `/help` rules-explanation guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_LOCALIZATION_TERMINOLOGY_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route localization/terminology posture contract, including the same-origin `/help` table-language guidance action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_HELP_RECOVERY_GUIDANCE_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route help/recovery guidance posture contract, including the same-origin `/help` handoff action. It is not runtime proof.
- `docs/BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF.md` defines the source-staged Chummer Online and /blazor/workbench compatibility route GM-screen export posture contract, including the same-origin `/help` GM-screen/export guidance action. It is not runtime proof.
- `docs/examples/blazor-workbench-gm-screen-export-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_WORKBENCH_GM_SCREEN_EXPORT_STAGED_PROOF.generated.json`.

## Portal Installer Handoff

- `docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md` defines the source-staged browser-to-portal download/install/support handoff contract. It keeps published artifacts as direct raw downloads while listing Blazor desktop compatibility routes as proof-required handoff rows with stable route metadata, a self-host operator lane for Docker/downloads setup whose browser fallback points to the clean `/app?command=character_roster` Chummer Online Character Roster deep link, a hosted `/blazor/` root redirect that immediately opens the Character Roster workflow via `app?command=character_roster`, a release-manifest-backed `/status` page, a marked `/contact` support handoff fallback, a same-origin `/help` handoff guide, the product-first `Explore Chummer Online` portal root hero plus the Character Roster primary CTA selector `a.cta[href="/app?command=character_roster"][data-portal-home-action="explore-chummer-online"]`, the `Chummer Online routes` portal root rail with user-facing Open Character Roster, Open Chummer Online, Open Chummer Online overview, and the `Get desktop client` label, portal route values centralized through `PortalRoutes.PublicApp`, `PortalRoutes.PublicAppSlash`, `PortalRoutes.PublicAppRoster`, `PortalRoutes.CharacterRosterCommand`, `PortalRoutes.BlazorApp`, `PortalRoutes.BlazorHome`, `PortalRoutes.BlazorAppSegment`, `PortalRoutes.BlazorHomeSegment`, `BuildBlazorAppUrl`, and `BuildBlazorHomeUrl`, portal root rail hover/focus affordances with reduced-motion handling, the same polished Chummer Online slate/amber/mint/blue visual language for portal recovery pages, deep ink/surface contrast, restrained ambient glow, downloads action pills, download route rows, docs shortcut pills, OpenAPI endpoint cards, labelled help/contact/status recovery rails with pill-style keyboard-focus treatment, and help-card plus help/contact/status rail hover/focus affordances with reduced-motion guards with a shared ambient grid texture, mobile-softened grid density, high-contrast portal action affordances, and reduced-motion-safe portal panel reveal. It also keeps same-origin OpenAPI/docs discovery markers for clean public `/app`, hosted `/blazor/app`, `/blazor/home`, `/blazor/`, `/downloads/`, `/downloads/releases.json`, `/downloads/install/{artifactId}`, `/status`, `/contact`, and `/help`, including `data-openapi-chummer-app-route`, `data-openapi-chummer-home-route`, and `data-openapi-blazor-entry-route`. The same `/docs/` explorer is marked with `data-docs-panel="operator-openapi-explorer"`, carries a shortcut rail marked with `data-docs-shortcuts="operator-recovery"` and `aria-describedby="docs-shortcuts-description"`, marks the OpenAPI load state with `data-docs-summary="openapi-load-state"` as a polite status live region, and marks the generated route collection with `data-docs-endpoints="openapi-route-list"` plus `data-docs-endpoint-card="openapi-route"`, `data-docs-endpoint-route`, `data-docs-endpoint-family`, `data-docs-endpoint-methods`, and `data-docs-endpoint-summary` using list/listitem semantics plus direct operator shortcut markers such as `data-docs-action="open-chummer-app"` targeting the roster-start `/app?command=character_roster`, `data-docs-action="open-downloads"`, `data-docs-action="open-help"`, `data-docs-action="open-contact"`, and `data-docs-action="open-openapi-json"` so self-host users can recover into the Character Roster workflow, installer, status, help, support, or raw contract routes without reading generated cards first. It includes Avalonia installer handoff and Blazor desktop compatibility installer handoff routes. It is not runtime proof.
- `scripts/materialize-blazor-portal-installer-handoff-staged-proof.py`: source-structural staged proof materializer for the portal installer handoff contract
- `scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh`: milestone-style wrapper for the portal installer handoff source-alignment check
- `scripts/print_blazor_public_edge_proof_status.py`: reports `portal_installer_handoff_staged_*` status lines as source alignment for raw artifacts and proof-required handoffs only, not installer runtime proof
- `docs/examples/blazor-portal-installer-handoff-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json`.

## Docker Self-Host Operator Contract

- `docs/BLAZOR_DOCKER_SELF_HOST_OPERATOR_PROOF.md` defines the source-staged self-hosted Docker operator contract for the portal-backed Chummer Online browser client, including default-off Rybbit analytics configuration through the sanitized `.env` example with metadata-only route/workflow fields, `has_workspace` and `has_dossier` boolean presence, and session replay plus autocapture disabled for Chummer surfaces. It is source alignment only, not Docker runtime proof.
- `docs/examples/blazor-docker-self-host-operator-staged-proof.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json`.

## Account and Support Handoff

- `docs/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md` defines the source-staged account/support/status/help handoff contract for the portal-backed Blazor browser client. It is not authentication, authorization, owner-propagation, portal-help-runtime, or support-submission runtime proof.
- `docs/examples/blazor-account-support-handoff-staged-proof.receipt.example.json` shows the account/support/status/help staged receipt shape.

## Runtime Proof Refresh Plan

- `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md` defines the ordered runtime proof refresh path from source-staged Blazor work to Docker self-host proof, hosted route-entry proof, hosted execution proof, and browser-lane aggregate proof. It is a plan, not runtime proof.
- `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates` pins the remaining validation gates for regenerated receipts, hosted and Docker proof refresh, aggregate proof readiness, Avalonia-equivalent workflow breadth, Runner Intelligence calculation reuse, roster hierarchy runtime evidence, Rybbit privacy posture, and cross-surface visual polish.
- Source-staged receipts hand off into the runtime refresh preflight but do not become runtime evidence.
- `scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh` is the source-plan preflight wrapper for that refresh plan. The runtime refresh plan preflight wrapper is source-plan validation only and does not execute runtime proof.
- `.codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json` is the source-plan preflight receipt for the runtime proof refresh plan.
- `scripts/materialize-blazor-runtime-proof-refresh-plan.py` materializes the source-plan receipt for the runtime proof refresh plan without executing Docker, hosted route-entry, hosted execution, or browser-lane aggregate proof.
- `scripts/print_blazor_public_edge_proof_status.py` reports the runtime proof refresh plan separately as source-plan evidence only with `source_plan_only_with_visibility_blocks_not_browser_execution`.
- The runtime refresh plan treats portal downloads/install/status/contact/help handoff visibility as source-plan only; it does not prove installer availability, portal runtime behavior, hosted execution, or Docker self-host execution.
- `docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json`.
- The refresh-plan example records `career_support_status_visibility` so operators can see career/support workflow family source-only status lines without treating hosted execution, Docker execution, dialog action execution, persistence, or committed browser mutations as proven.
- The refresh-plan example records `identity_license_status_visibility` so operators can see identity/SIN/license source-only status lines without treating hosted execution, Docker execution, dialog action execution, legal identity mutation, or persistence as proven.
- The refresh-plan example records `combat_support_status_visibility` so operators can see combat support source-only status lines without treating hosted execution, Docker execution, combat-state mutation, reload mutation, damage-track mutation, or rules-engine calculations as proven.
- The refresh-plan example records `skill_maintenance_status_visibility` so operators can see skill maintenance source-only status lines without treating hosted execution, Docker execution, skill-state mutation, specialization persistence, group-edit mutation, or rules-engine calculations as proven.
- The refresh-plan example records `magic_support_status_visibility` so operators can see magic/resonance support source-only status lines without treating hosted execution, Docker execution, magic/resonance mutation, spirit creation, Matrix program mutation, or rules-engine calculations as proven.
- The refresh-plan example records `gear_maintenance_status_visibility` so operators can see gear maintenance source-only status lines without treating hosted execution, Docker execution, gear-state mutation, inventory persistence, pricing, availability, or rules-engine calculations as proven.
- The refresh-plan example records `runner_intelligence_status_visibility` so operators can see Runner Intelligence source-only status lines without treating statistical-engine execution, hosted execution, Docker execution, percentile calculations, what-if spell/drug/gear calculations, hosted cohort aggregation, or rules-engine calculations as proven.
- The refresh-plan example records `runner_intelligence_calculation_status_visibility` so operators can see Runner Intelligence calculation source-only status lines without treating authoritative SR rules-engine validation, hosted execution, Docker execution, hosted cohort aggregation, or browser runtime parity as proven.
- The refresh-plan example records `portal_installer_handoff_status_visibility` so operators can see the portal/download installer handoff source-only status lines without treating them as runtime installer proof.
- The refresh-plan example also records `workbench_hosting_privacy_status_visibility` so operators can see the Chummer Online and /blazor/workbench compatibility route hosting/privacy source-only status lines and default-off Rybbit posture without treating them as Rybbit delivery, hosted route execution, or Docker browser execution.
- The refresh-plan example also records `docker_self_host_operator_status_visibility` so operators can see Docker self-host source-only status lines and default-off Rybbit posture without treating them as Docker runtime proof.
- The refresh-plan example also records `workbench_roster_hierarchy_status_visibility` so operators can see roster hierarchy source-only status lines without treating drag/drop intent, durable persistence, filesystem moves, or browser runtime parity as proven.
- The refresh-plan example also records `legacy_control_coverage_status_visibility` so operators can see legacy control coverage source-only status lines without treating hosted execution, Docker execution, dialog action execution, persistence, or mutation paths as proven.
- The refresh-plan example also records `source_staged_release_boundary_status_visibility` so operators can see source-policy evidence without treating staged or source-plan receipts as release evidence.

## Source-Staged Release Boundary

- `scripts/verify-blazor-source-staged-proof-release-boundary.py` verifies that source-staged and source-plan receipts stay out of browser release-readiness aggregation.
- `scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh` is the milestone wrapper for that source-policy guard.
- `scripts/print_blazor_public_edge_proof_status.py` reports `source_staged_release_boundary_*` status lines as source-policy evidence that forbidden release tokens and source aggregation sources stay separated from browser release-readiness.
- `docs/examples/blazor-source-staged-release-boundary.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json`, including the Docker self-host operator staged receipt as forbidden release evidence and runtime refresh plan receipt as forbidden release evidence.
- The `forbidden_staged_tokens` field name is retained for compatibility but includes source-plan and source-calculation receipt tokens.

## Staged-to-Runtime Promotion Matrix

- `docs/BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.md` maps source-staged browser workflow families to the Docker and hosted runtime receipts required before they can be promoted from staged breadth to browser-proven parity.
- The staged-to-runtime matrix keeps Runner Intelligence as a planned promotion family until `promoted_runner_benchmark_execution`, `promoted_runner_what_if_execution`, and `promoted_runner_cohort_privacy_execution` are backed by refreshed hosted public-edge execution, Docker self-host execution, authoritative rules-engine calculation fixtures, and hosted cohort opt-in aggregation proof.
- `scripts/materialize-blazor-staged-to-runtime-promotion-matrix.py` materializes the source-plan receipt for the staged-to-runtime matrix and keeps non-promoting portal-boundary guards out of workbench workflow promotion.
- `scripts/print_blazor_public_edge_proof_status.py` reports the staged-to-runtime promotion matrix separately as source-plan evidence only.
- `docs/examples/blazor-staged-to-runtime-promotion-matrix.receipt.example.json` shows the compact generated receipt shape for `BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.generated.json`.

## Product Language Source Boundary

The Blazor web-client docs follow the parity-goal naming contract: public surfaces say Chummer Online, Character Roster, runner, dossier, and Build Lab, while `workbench`, `workspace`, route aliases, command IDs, selectors, and proof names remain hardcoded only where they preserve compatibility, source contracts, or receipt stability. Generic terms are reserved for shared Avalonia/Blazor architecture, analytics allowlists, Docker/operator configuration, and proof taxonomy.
