# Workbench Release Signoff

Purpose: close `WL-202` and `WL-203` with explicit, verifier-backed evidence instead of leaving workbench completion implied by older milestone notes.

Documentation map for the browser lane:

- `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the top-level browser-client docs index
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary design and parity contract
- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md` defines the hosted route-entry/workbench proof tier
- `docs/BLAZOR_SELF_HOST_RUNBOOK.md` defines the Docker/self-host operator lane
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` defines the stricter hosted execution-proof tier

## Workbench completion surface

`chummer6-ui` is treated as release-complete for the current workbench/browser/desktop scope when the following verification lanes all stay green:

- `scripts/ai/milestones/b3-build-lab-check.sh` for builder depth.
- `scripts/ai/milestones/b10-contact-network-check.sh` for relationship graph plus heat/faction/favor continuity.
- `scripts/ai/milestones/b9-campaign-journal-check.sh` for planner/calendar and journal depth.
- `scripts/ai/milestones/b8-runtime-inspector-check.sh` for diagnostics and richer Hub UX.
- `scripts/ai/milestones/b12-generated-asset-dispatch-check.sh` for publish, dispatch, review, and approval-aware generated-asset flows.
- `scripts/ai/milestones/b11-npc-persona-studio-check.sh` for operator-facing NPC/persona depth.
- `scripts/ai/milestones/b4-gm-board-spider-feed-check.sh` for moderation-adjacent Spider and board surfaces.
- `scripts/ai/milestones/ruleset-ui-adaptation-check.sh` for SR4/SR5/SR6 posture, unsupported-state honesty, and cross-head shell adaptation proof.
- `scripts/ai/milestones/b14-flagship-ui-release-gate.sh` for flagship desktop interaction, bundled demo-runner presence, and visibly reactive menu/settings proof.
- `scripts/ai/milestones/veteran-task-time-evidence-gate.sh` for veteran task-time evidence on sourcebooks, roster, print/export, and bounded Blazor fallback proof.
- `scripts/ai/milestones/chummer5a-screenshot-review-gate.sh` for mandatory Chummer5a screenshot-backed compare review on dense builder, master index, roster, and settings surfaces.
- `scripts/ai/milestones/dense-workbench-recovery-gate.sh` for compact classic workbench posture, reduced badge density, row-preserving padding, accessibility without oversized chrome, and screenshot-backed menu/toolstrip familiarity proof.
- `scripts/ai/milestones/classic-dense-workbench-posture-gate.sh` for Avalonia default dense posture, reduced section-header scale, flat form panels, and anti-dashboard workbench chrome proof.
- `scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh` for release-channel/install-media/startup-smoke truth across promoted desktop heads, including fail-honest blocker receipts when artifacts or platform proof are missing.
- `scripts/ai/milestones/b15-localization-release-gate.sh` for shipping-locale truth, explicit fallback honesty, explain localization, and support/install/update language coverage.

Those checks are all part of the normal `scripts/ai/verify.sh` path, so release truth does not depend on ad hoc manual demos.

For local docker-backed release proof, `scripts/e2e-portal.sh` is the canonical executable lane. It boots the repository `portal` Docker profile (`chummer-api`, `chummer-blazor-portal`, `chummer-hub-web-portal`, `chummer-avalonia-browser`, and `chummer-portal`) and materializes `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json` with the probed base URL, route coverage, and whether the route probe actually ran. The same lane also materializes `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json` so the Docker self-hosted browser workbench proof stays distinct from the broader local release receipt.

For hosted public-edge browser posture, `scripts/e2e-public-edge.cjs` and `scripts/materialize-external-host-proof-blockers.py` now also publish `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`. That hosted receipt proves route-entry posture for `/blazor/`, `/blazor/workbench`, workspace restore route shape, hosted startup-command route shape for `new_character_origin`, `character_roster`, and `master_index` alongside the other promoted startup routes, `/blazor/health`, resumed result-continuation route shapes, and resumed action/committed-action route shapes. The stricter hosted execution tier defined in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` is now also published as a real passing receipt at `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`, covering resumed workflow execution on `https://chummer.run/blazor/workbench` rather than only self-host Playwright posture.

That hosted execution tier is no longer limited to startup-command evidence. The current verifier-backed contract now spans:

- startup execution for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- dense startup-utility surfaces for `character_roster` and `master_index`
- restored origin/rules continuity, including `new_character_origin` structure and restored `tab-rules`
- restored build-lab continuity on `tab-create`
- dense selection/edit/delete families across gear, qualities, magic, cyberware, and contacts
- resumed result, action, committed-action, and advanced-action families on the promoted `/blazor/workbench` lane

That hosted route-entry tier is now enforced explicitly by `scripts/verify_blazor_public_edge_workbench_proof.py` and `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`, while the stricter hosted execution tier is enforced by `scripts/verify_blazor_public_edge_execution_proof.py` and `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`.

The promoted browser product shape is now explicit: `/blazor/` is the stable public entry, `/blazor/workbench` is the product-shaped Chummer6 browser client, and `/blazor/preview` is supporting proof surface rather than the primary user promise. The acceptance bar for that lane is tracked in `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`, and the current self-host operator path is documented in `docs/BLAZOR_SELF_HOST_RUNBOOK.md`.

`scripts/print_blazor_public_edge_proof_status.py` is the combined browser-lane proof summary. It reports Docker self-host proof, hosted route-entry proof, hosted execution proof, analytics posture, connected-runtime posture, aggregate proof-set status, and external-host blocker state from the current published receipts.

`scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh` materializes `.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json` and must fail if any required browser-lane receipt is missing, has the wrong contract, or is not in its expected passing/ready state. This aggregate receipt is a release-readiness convenience for the browser lane; it still does not claim full desktop parity beyond the receipts it aggregates.

Release truth for the browser lane therefore splits into three separate statements:

- Docker self-host browser workbench proof exists and is published as `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`
- hosted `chummer.run` route-entry posture exists and is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`
- hosted `chummer.run` workflow execution is a stricter proof tier, published separately as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`, and it now passes for the promoted `/blazor/workbench` browser workflow lane
- hosted/self-host browser analytics posture is published separately as `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`; it only proves optional Rybbit wiring and privacy boundaries, not workflow parity
- optional connected-runtime portal forwarding posture is published separately as `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`; it proves session/coach/AI routing boundary and signed owner forwarding, not full workflow parity

Hosted execution proof passing removes the old "route posture only" limit for the promoted browser lane, but it still does not by itself prove full browser/Desktop parity breadth outside the workflow families covered by the hosted execution contract and the broader parity ledger.

The next browser-lane proof refresh is staged to add the career/support workflow family across both hosted and Docker self-host proof runners. That staged family covers restored `tab-calendar` section continuity, add/edit/delete career-entry dialogs, add/edit/delete committed result continuations, runner notes editing, notes-save result continuations, and classic move up/down list utilities. This is not yet current published receipt evidence until the hosted and self-host proofs are rerun and their generated receipts are refreshed.

`scripts/ai/milestones/blazor-career-support-staged-proof-check.sh` is the pre-refresh source-alignment check for that staged family. It only proves that the staged route family is wired consistently across source, runners, receipt metadata, status reporting, and docs; it is not a browser execution receipt and must not be treated as a release-passing workflow proof.

The next staged browser-family slice after career/support is identity/SIN/license utility posture under restored `tab-info`. `scripts/ai/milestones/blazor-identity-license-staged-proof-check.sh` is its source-alignment check. It proves only that the workbench affordances, dedicated utility dialogs, hosted/self-host route shapes, status utility, and docs agree; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after identity/SIN/license is combat support utility posture under restored `tab-combat`. `scripts/ai/milestones/blazor-combat-support-staged-proof-check.sh` is its source-alignment check. It proves only that armor, reload, and damage-track routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after combat support is skill maintenance utility posture under restored `tab-skills`. `scripts/ai/milestones/blazor-skill-maintenance-staged-proof-check.sh` is its source-alignment check. It proves only that specialization, removal, and group-edit routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after skill maintenance is magic/resonance support utility posture across restored adept, magician, critter, and technomancer lanes. `scripts/ai/milestones/blazor-magic-support-staged-proof-check.sh` is its source-alignment check. It proves only that adept power, spirit, critter power, and Matrix-program routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after magic/resonance support is gear maintenance utility posture under restored `tab-gear`. `scripts/ai/milestones/blazor-gear-maintenance-staged-proof-check.sh` is its source-alignment check. It proves only that generic add, edit, and remove gear routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after gear maintenance is source/gear utility posture across restored profile and gear lanes. `scripts/ai/milestones/blazor-source-gear-utility-staged-proof-check.sh` is its source-alignment check. It proves only that source viewing, gear source, gear mount, and free/paid cost routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after source/gear utilities is magic cleanup utility posture across restored magician and gear lanes. `scripts/ai/milestones/blazor-magic-cleanup-staged-proof-check.sh` is its source-alignment check. It proves only that magic add, spirit binding, magic source, and drug-removal routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after magic cleanup is browser output handoff posture across restored save, save-as, export, print, and download routes. `scripts/ai/milestones/blazor-browser-output-handoff-staged-proof-check.sh` is its source-alignment check. It proves only that browser output routes are wired across restored workbench affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after browser output handoff is workbench portal handoff posture for downloads, status, support, and account work. `scripts/ai/milestones/blazor-workbench-portal-handoff-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes same-origin portal handoff affordances and route expectations; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench portal handoff is workbench polish posture for the promoted task dock. `scripts/ai/milestones/blazor-workbench-polish-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes dense start, edit, output, and portal handoff shortcuts with scoped responsive styling; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench polish is workbench recovery posture for the promoted session recovery strip. `scripts/ai/milestones/blazor-workbench-recovery-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes recent, Build Lab, profile, status, restored workspace, restored gear, and restored output affordances with scoped responsive styling; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench recovery is workbench hosting/privacy posture for hosted route, Docker self-host, and analytics privacy copy. `scripts/ai/milestones/blazor-workbench-hosting-privacy-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes hosted/self-host/Rybbit privacy posture with scoped responsive styling; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench hosting/privacy is workbench command-palette posture for keyboard-style command discovery. `scripts/ai/milestones/blazor-workbench-command-palette-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes keyboard-style hints and reload-safe workbench links for common commands; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench command-palette is workbench density posture for compact desktop, comfortable review, and mobile-safe display options. `scripts/ai/milestones/blazor-workbench-density-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes density posture controls with scoped responsive styling; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench density is workbench workflow-ledger posture for visible startup, editing, output, recovery, portal, and boundary rows. `scripts/ai/milestones/blazor-workbench-workflow-ledger-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes browser-client capability and desktop handoff boundaries with scoped responsive styling; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench workflow-ledger is workbench file-intake posture for browser-safe open/import, Hero Lab import, XML editor, native file-system handoff, and support. `scripts/ai/milestones/blazor-workbench-file-intake-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes file-intake affordances and shared import-dialog source alignment; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench file-intake is workbench rules/data posture for ruleset choice, sourcebook review, XML/custom data, and translation tools. `scripts/ai/milestones/blazor-workbench-rules-data-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes rules/reference affordances and shared dialog source alignment; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench rules/data is workbench settings posture for global settings, character settings, ruleset choice, update status, and support. `scripts/ai/milestones/blazor-workbench-settings-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes settings affordances and shared dialog source alignment; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench settings is workbench diagnostics posture for runtime inspector, About, health, status, and preview tools. `scripts/ai/milestones/blazor-workbench-diagnostics-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes diagnostics affordances and shared dialog source alignment; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench diagnostics is workbench connected-runtime posture for play, session, coach, assistant, and status links. `scripts/ai/milestones/blazor-workbench-connected-runtime-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes optional live-lane affordances and connected-runtime documentation boundaries; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench connected-runtime is workbench accessibility posture for keyboard order, dialog fit, readable density, reduced motion, and help links. `scripts/ai/milestones/blazor-workbench-accessibility-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes accessibility affordances and responsive source alignment; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench accessibility is workbench section-rail posture for character sheet navigation. `scripts/ai/milestones/blazor-workbench-section-rail-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes profile, build, skills, gear, combat, magic, matrix, contacts, and career sheet shortcuts; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench section-rail is workbench desktop install handoff posture for browser-to-desktop continuity. `scripts/ai/milestones/blazor-workbench-desktop-install-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes downloads, update channel, status, account, self-host notes, and support handoff affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench desktop install handoff is workbench menu-bar posture for File, Build, View, Character, Tools, and Help entry points. `scripts/ai/milestones/blazor-workbench-menu-bar-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes familiar menu affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench menu-bar is workbench workspace-tabs posture for active runner, build lab, print/export, and recent import lanes. `scripts/ai/milestones/blazor-workbench-workspace-tabs-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes runner task tab affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench workspace-tabs is workbench status-bar posture for save, rules, validation, session, privacy, and support state cues. `scripts/ai/milestones/blazor-workbench-status-bar-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes current-state affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench status-bar is workbench inspector-rail posture for summary, build checks, inventory, notes, and sources. `scripts/ai/milestones/blazor-workbench-inspector-rail-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes context-inspector affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench inspector-rail is workbench dialog-stack posture for active dialog, committed result, retry, back-to-sheet, and support continuations. `scripts/ai/milestones/blazor-workbench-dialog-stack-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes dialog-continuation affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench dialog-stack is workbench context-actions posture for add, edit, remove, duplicate, source lookup, and recover actions. `scripts/ai/milestones/blazor-workbench-context-actions-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes selection-style context actions without hidden right-click dependency; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench context-actions is workbench search/filter posture for roster, gear, skills, qualities, sources, and clear filter lanes. `scripts/ai/milestones/blazor-workbench-search-filter-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes search/filter affordances for dense lists; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench search/filter is workbench layout-presets posture for dense sheet, split review, output, mobile safe, and focus pane modes. `scripts/ai/milestones/blazor-workbench-layout-presets-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes layout mode affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench layout-presets is workbench activity-feed posture for save event, validation warning, output event, hosted status, and support escape entries. `scripts/ai/milestones/blazor-workbench-activity-feed-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes activity/history affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench activity-feed is workbench keyboard-shortcuts posture for command help, save/output, section jump, density toggle, and support escape affordances. `scripts/ai/milestones/blazor-workbench-keyboard-shortcuts-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes keyboard-help affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench keyboard-shortcuts is workbench resource-meters posture for karma, nuyen, essence, limits, wounds, and lifestyle context. `scripts/ai/milestones/blazor-workbench-resource-meters-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes resource-total affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench resource-meters is workbench tree-tools posture for expand, collapse, sort, reorder, pin, and selection tools. `scripts/ai/milestones/blazor-workbench-tree-tools-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes dense tree/list affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench tree-tools is workbench save-session posture for save, Save As, autosave, dirty state, recovery, and export lifecycle actions. `scripts/ai/milestones/blazor-workbench-save-session-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes save/session affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench save-session is workbench output-handoff posture for PDF packet, print sheet, HTML summary, share link, audit queue, and download bundle actions. `scripts/ai/milestones/blazor-workbench-output-handoff-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes output/export affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench output-handoff is workbench validation-queue posture for rule issues, missing fields, cost checks, availability limits, build gate, and fix-next navigation. `scripts/ai/milestones/blazor-workbench-validation-queue-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes validation/build-readiness affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench validation-queue is workbench history-undo posture for undo, redo, snapshot, compare, restore, and conflict review actions. `scripts/ai/milestones/blazor-workbench-history-undo-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes history/recovery affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench history-undo is workbench sync-presence posture for connection, offline, local cache, sync queue, presence, and handoff cues. `scripts/ai/milestones/blazor-workbench-sync-presence-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes hosted/self-hosted session affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench sync-presence is workbench data-packs posture for sourcebooks, errata, custom data, update packs, validation scope, and data-folder context. `scripts/ai/milestones/blazor-workbench-data-packs-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes rules/data-pack affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench data-packs is workbench character-library posture for open, recent, pin, clone, archive, and import actions. `scripts/ai/milestones/blazor-workbench-character-library-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes character-library affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench character-library is workbench campaign-session posture for roster, GM review, session notes, rewards, table share, and run handoff actions. `scripts/ai/milestones/blazor-workbench-campaign-session-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes campaign/session affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench campaign-session is workbench observability-privacy posture for consent, Rybbit status, route events, error traces, privacy log, and self-host telemetry toggle actions. `scripts/ai/milestones/blazor-workbench-observability-privacy-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes privacy-aware analytics affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench observability-privacy is workbench first-run posture for new runner, desktop import, sample runner, restore session, self-host setup, and docs actions. `scripts/ai/milestones/blazor-workbench-first-run-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes onboarding affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench first-run is workbench PWA-install posture for install prompt, offline cache, update available, browser permissions, release channel, and reset cache actions. `scripts/ai/milestones/blazor-workbench-pwa-install-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes install/update affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench PWA-install is workbench Docker-operator posture for container health, env check, volume mounts, backup, image update, and support bundle actions. `scripts/ai/milestones/blazor-workbench-docker-operator-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes Docker self-host operator affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench Docker-operator is workbench security-access posture for sign-in, workspace lock, roles, session expiry, key rotation, and access audit actions. `scripts/ai/milestones/blazor-workbench-security-access-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes hosted access-control affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench security-access is workbench notifications-jobs posture for job queue, retry, dismiss, settings, history, and support actions. `scripts/ai/milestones/blazor-workbench-notifications-jobs-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes async notification/job affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench notifications-jobs is workbench touch-mobile posture for touch mode, zoom, panel dock, compact actions, keyboard-safe layout, and pointer help actions. `scripts/ai/milestones/blazor-workbench-touch-mobile-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes touch/mobile ergonomics affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench touch-mobile is workbench navigation-deeplink posture for breadcrumbs, URL state, back/forward, copy route, tab restore, and shared anchor actions. `scripts/ai/milestones/blazor-workbench-navigation-deeplink-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes web navigation/deep-link affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench navigation-deeplink is workbench inline-editing posture for dirty fields, numeric steppers, commit, revert, formula preview, and bulk apply actions. `scripts/ai/milestones/blazor-workbench-inline-editing-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes controlled inline-editing affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench inline-editing is workbench performance-virtualization posture for lazy sections, virtual lists, render budget, memory posture, degraded mode, and profiler actions. `scripts/ai/milestones/blazor-workbench-performance-virtualization-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes large-sheet performance affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench performance-virtualization is workbench print-layout posture for sheet template, paper size, theme, sections, preview, and export profile actions. `scripts/ai/milestones/blazor-workbench-print-layout-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes sheet output profile affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench print-layout is workbench portrait-attachments posture for portrait, token art, notes, attachments, import media, and cleanup actions. `scripts/ai/milestones/blazor-workbench-portrait-attachments-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes character media/attachment affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench portrait-attachments is workbench windowing-panes posture for split view, pop-out, pinned inspector, focus mode, second screen, and restore layout actions. `scripts/ai/milestones/blazor-workbench-windowing-panes-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes desktop-like pane/window affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench windowing-panes is workbench calculation-provenance posture for derived breakdown, modifier stack, rule source, stale values, manual override, and dependency path actions. `scripts/ai/milestones/blazor-workbench-calculation-provenance-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes calculation-explainability affordances; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after workbench calculation-provenance is workbench lifecycle-calendar posture for downtime, lifestyle upkeep, subscriptions, reminders, recurring costs, and next session actions. `scripts/ai/milestones/blazor-workbench-lifecycle-calendar-staged-proof-check.sh` is its source-alignment check. It proves only that the promoted workbench exposes downtime/upkeep affordances; it is not a hosted or Docker browser execution receipt.

The staged browser-family slice after workbench lifecycle-calendar is workbench progression-ledger posture for karma spend, nuyen ledger, purchase queue, reputation, carryover, and audit trail actions. `scripts/ai/milestones/blazor-workbench-progression-ledger-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench import/reconcile posture for file selection, parse summary, rules mapping, custom data, conflict review, and final acceptance. `scripts/ai/milestones/blazor-workbench-import-reconcile-staged-proof-check.sh` is its source-alignment check. The follow-on staged slice is workbench compare/merge posture for diff view, conflict choice, source trace, dry run, apply, and rollback actions. `scripts/ai/milestones/blazor-workbench-compare-merge-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench restore/checkpoint posture for autosave, named checkpoint, backup download, restore preview, rollback, and retention actions. `scripts/ai/milestones/blazor-workbench-restore-checkpoint-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench offline/cache posture for cache status, queued edits, reconnect review, local export, stale data, and sync health actions. `scripts/ai/milestones/blazor-workbench-offline-cache-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench session-locking posture for lock status, owner handoff, read-only fallback, stale-session recovery, conflict owner, and takeover review actions. `scripts/ai/milestones/blazor-workbench-session-locking-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench share/export privacy posture for redaction, scope, expiry, revocation, history, and local-only export actions. `scripts/ai/milestones/blazor-workbench-share-export-privacy-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench table-handoff posture for GM packet, initiative card, condition tracker, public handout, private notes, and table export actions. `scripts/ai/milestones/blazor-workbench-table-handoff-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench rules-citation posture for source packet, citation scope, errata note, table summary, dispute trail, and audit export actions. `scripts/ai/milestones/blazor-workbench-rules-citation-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench localization/terminology posture for language, units, date format, currency, table terms, and source-title actions. `scripts/ai/milestones/blazor-workbench-localization-terminology-staged-proof-check.sh` is its source-alignment check. The next staged slice is workbench help/recovery guidance posture for context help, shortcut hints, error explanations, recovery suggestions, docs links, and support handoff actions. `scripts/ai/milestones/blazor-workbench-help-recovery-guidance-staged-proof-check.sh` is its source-alignment check. Character Roster now also carries custom hierarchy posture for user-created virtual folders, nested groups, drag/drop move intent, explicit Move Runner/Folder actions, watched-file links that do not move disk files by default, safe folder deletion that moves children to Inbox first, non-destructive metadata mutation that stages create/rename/move/reorder changes before any filesystem mutation, editable Folder Name and Target Folder fields for web workflow control, Blazor dialog markup that exposes roster hierarchy rows with virtual-folder data attributes, selected-row state, and drag-handle styling before drag execution is claimed, a shared `RosterHierarchyState` contract for folders, item links, move intent, safe filesystem-confirmation policy, `RosterHierarchyJson` preference staging for non-destructive layout metadata, safe staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, and hidden settings carriage for later owner-scoped persistence. These prove only that the promoted workbench exposes advancement accounting, existing-runner migration, merge-review, recovery, offline-continuity, edit-ownership, private-handoff, table-handoff, rules-explanation, localization, guided-help, and roster-organization affordances; they are not hosted or Docker browser execution receipts.

`/scripts/ai/milestones/blazor-legacy-control-coverage-staged-proof-check.sh` is the source-level breadth guard over all known `LegacyUiControlCatalog` controls. It maps controls into hosted execution baseline coverage or staged source-alignment families; it is not a hosted or Docker browser execution receipt.

`scripts/ai/milestones/blazor-source-staged-proof-set-check.sh` is the aggregate source-staged proof-set lane. It materializes the staged source receipts and summarizes their status, but it must stay outside release-readiness aggregation because it is not hosted or Docker browser execution evidence.

`scripts/print_blazor_public_edge_proof_status.py` reports that staged receipt separately as `career_support_staged_*` when present, with a source-alignment-only note. It must remain separate from the aggregate browser-lane proof set until hosted and self-host browser execution receipts are refreshed.

For the hard Linux desktop exit gate, `scripts/materialize-linux-desktop-exit-gate.sh` is the canonical executable lane. It must build the Linux Avalonia binary, package the primary `.deb` plus fallback archive, install and purge the primary `.deb` inside an isolated dpkg root while running startup smoke from the installed path, run startup smoke against the fallback archive, run the desktop runtime unit-test suite, and publish `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json`.

For the hard Windows desktop exit gate, `scripts/materialize-windows-desktop-exit-gate.sh` is the canonical executable lane. It must validate that the promoted Avalonia Windows installer is present on the active release shelf, require release-channel digest/size alignment for that installer, require current local release plus desktop workflow parity proofs, require the aggregate Blazor browser-lane proof set, and publish `.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json`.

## Cross-head hardening proof

`F0` is treated as materially closed for the UI head when these signoff rails remain executable:

- `scripts/ai/milestones/b13-accessibility-signoff-check.sh` for accessibility and browser-shell live-region proof.
- `scripts/ai/milestones/b7-browser-isolation-check.sh` for deployment/browser-constraint proof.
- `scripts/ai/milestones/b2-browse-virtualization-check.sh` for dense-data virtualization discipline.
- `scripts/ai/milestones/p5-ui-kit-shell-chrome-check.sh`
- `scripts/ai/milestones/p5-ui-kit-design-token-check.sh`
- `scripts/ai/milestones/p5-ui-kit-accessibility-state-check.sh`

## Release budgets

- Accessibility: workbench surfaces must keep explicit live/status semantics and the B13 signoff path must stay green.
- Localization: explain and workbench chrome must remain localization-safe; `Chummer.Presentation/Explain/RulesetExplainRenderer.cs` is allowed to fail fast on missing localization keys rather than silently falling back to stale copy, and `scripts/ai/milestones/b15-localization-release-gate.sh` is the release-required executable proof lane for shipping locale truth.
- Performance: dense browse and browser delivery must remain under the existing virtualization and browser-isolation guardrails instead of regressing into unbounded table/render paths.

## Ownership note

Installer-capable artifacts and updater integration are owned here.
Promoted release channels, installer/update-feed publication truth, and public `/downloads` state are owned downstream by `chummer6-hub-registry` and rendered by `chummer6-hub`.
Repo-local docker release proof for the portal/workbench shell is owned here, executed against the downstream public edge, and published as `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json`. The browser self-host workbench lane is separately published as `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`, and the hosted `chummer.run` browser route-entry lane is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`.

Hosted `chummer.run` workflow execution proof is separately published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json` and must remain distinct from both the self-host workbench receipt and the hosted route-entry receipt.

## Exit statement

The remaining UI debt is no longer missing shared-shell workbench capability or missing ruleset-specific shell adaptation proof. It is compatibility-cargo cleanup and future feature growth, both of which sit outside the current `E0`/`F0` closure bar.

### Source-Staged Blazor Proof Runbook

`docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md` is the operator guide for refreshing source-staged Blazor proof receipts. It is useful before hosted and Docker browser proof refreshes, but it is not release evidence by itself and must not be included in browser release-readiness aggregation.

### Source-Staged Release Boundary Guard

`scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh` verifies that source-staged Blazor receipts are not wired into browser release-readiness aggregation and that the docs preserve the boundary language. It is a source-policy guard only, not browser execution evidence.

### Portal Installer Handoff Source Contract

`docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md` defines the source-staged browser-to-portal installer/download/support handoff contract. It is useful for keeping route expectations aligned, but it is not runtime proof and must not replace local portal, hosted route-entry, or hosted execution receipts.

### Docker Self-Host Operator Source Contract

`docs/BLAZOR_DOCKER_SELF_HOST_OPERATOR_PROOF.md` defines the source-staged Docker self-host operator contract for the portal-backed Blazor workbench. It is source alignment only and must not replace `BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json` from `scripts/e2e-portal.sh`.

### Account and Support Handoff Source Contract

`docs/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_PROOF.md` defines the source-staged account/support/status handoff contract for the portal-backed Blazor workbench. It is route and documentation alignment only, not authentication or support runtime proof.

### Blazor Runtime Proof Refresh Plan

`docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md` defines the ordered runtime proof refresh path for promoting source-staged Blazor work into real Docker and hosted browser receipts. `scripts/ai/milestones/blazor-runtime-proof-refresh-plan-check.sh` verifies the plan and command sources exist, but does not execute runtime proof.

`scripts/print_blazor_public_edge_proof_status.py` reports that plan as `runtime_proof_refresh_plan_*` source-plan evidence only. `docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json` shows the expected compact receipt shape.

### Staged-to-Runtime Promotion Matrix

`docs/BLAZOR_STAGED_TO_RUNTIME_PROMOTION_MATRIX.md` maps each staged Blazor workflow family to the runtime receipts that must eventually prove it. `scripts/ai/milestones/blazor-staged-to-runtime-promotion-matrix-check.sh` verifies the matrix and hosted runner family IDs exist, but it does not execute runtime proof.

`scripts/print_blazor_public_edge_proof_status.py` reports that matrix as `staged_to_runtime_promotion_matrix_*` source-plan evidence only. `docs/examples/blazor-staged-to-runtime-promotion-matrix.receipt.example.json` shows the expected compact receipt shape.
