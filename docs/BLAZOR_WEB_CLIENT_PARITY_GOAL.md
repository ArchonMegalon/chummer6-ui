# Blazor Web Client Design and Parity Goal

## Document Role

This is the primary design-spec and parity-contract document for the Chummer6 browser client.

Use it with:

- `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` for the documentation map
- `docs/BLAZOR_SELF_HOST_RUNBOOK.md` for operator and Docker posture
- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md` for the hosted `chummer.run` route-entry proof contract
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` for the hosted `chummer.run` execution-proof contract
- `docs/MIGRATION_BACKLOG.md` for implementation backlog items tied to this parity target
- `docs/WORKBENCH_RELEASE_SIGNOFF.md` for release-gate and signoff posture

## Objective

Make `Chummer.Blazor` a polished browser-hosted Chummer client that feels like another desktop client, not a preview page or a reduced companion surface.

The web client should let a user do the same normal work they expect from the Avalonia desktop client: create a runner, open/import existing files, inspect and edit the character sheet, use dense Chummer-style workbench navigation, run builder workflows, use dialogs and section actions, save/export/print where the browser platform permits, recover continuity, and move through support/update/download/account handoff without leaving the product shape.

This document is the Chummer6 web-client design spec for the promoted browser lane. It defines the intended product shape, route posture, workflow bar, and parity evidence required before `Chummer.Blazor` can be described as a polished first-class client.

The spec is intentionally broader than UI layout. It covers product entry, continuation posture, operator hosting, browser-native substitutes for desktop behaviors, and the proof standards required to claim parity with the Avalonia user workflow.

## Product Posture

`Chummer.Avalonia` can remain the native desktop reference head while the web client matures, but the target state for `Chummer.Blazor` is first-class web delivery.

The intended shipped posture is:

- `Chummer.Blazor` is the browser desktop client hosted on `chummer.run` behind `Chummer.Portal`.
- `Chummer.Blazor` is self-hostable through Docker with the same portal/API/download/session/coach/AI routing model used by the public edge.
- The web client shares the same user workflow vocabulary as Avalonia: startup, open/import, new character, ruleset choice, sheet editing, dense browse, dialogs, save/export/print, account/owner context, support, and recovery.
- The web client does not borrow Avalonia proof for public claims. Any claim that the web client is desktop-equivalent must be backed by browser-specific tests, screenshots, and route/runtime evidence.
- Browser limitations are named honestly. If a native-only capability cannot exist in web form, the web client must provide the closest useful browser workflow and document the difference in release evidence.

## Chummer6 Design Canon for Web

The browser client should preserve Chummer6 product character rather than collapsing into a generic web CRUD shell.

Required design traits:

- dense workbench-first navigation, with startup, open work, runner editing, dialogs, and result continuations all reachable without hunting through a marketing-style shell
- desktop-like continuity, where a user can reload, deep-link, resume, recover, and keep moving through the same workspace instead of restarting the task
- action-heavy editing surfaces, where the important job is done on the page through commands, tabs, row actions, dialogs, and committed follow-through, not by bouncing the user into disconnected mini flows
- explicit route honesty, where proof/demo/preview surfaces stay clearly distinct from the promoted product-shaped route
- browser-safe substitutes for native behaviors, especially around file open, save-as, export, print, downloads, and host integration
- release-proof honesty, where unsupported or partial routes are visible as such and are not dressed up as parity-complete

## Promoted Route Shape

The Chummer6 browser lane should present a clear route hierarchy:

- `/blazor/` is the stable public entry and should resolve into the browser workbench, not into a detached proof page
- `/blazor/workbench` is the promoted product-shaped browser client
- `/blazor/preview` is allowed to remain a denser proof shelf, but it is not the primary user promise
- deep links under `/blazor/workbench` must be reload-safe and continuity-safe for startup commands, restored workspaces, section continuations, dialog/action continuations, and browser result continuations

This matters because the user workflow is part of the design. Chummer6 web parity is not just "the same controls exist"; it also requires the same practical entry path and continuation posture.

## Hosting Requirement

The public hosted path is `chummer.run` through `Chummer.Portal`.

The canonical self-host operator reference for this lane is `docs/BLAZOR_SELF_HOST_RUNBOOK.md`, with baseline environment defaults in `docs/examples/self-hosted-browser-workbench.env.example`.
The current hosted route-entry proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`.
The current hosted execution-proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`.

The portal must provide:

- a browser-head default landing that resolves `/blazor/` into the product-shaped workbench route, not only a proof-only preview label
- stable `/blazor/` routing with reload-safe deep links
- API access through `/api/*` with signed owner propagation where configured
- optional `/api/ai/*` forwarding for the AI control plane
- `/downloads/` for desktop downloads and install/update handoff
- optional `/session/` and `/coach/` forwarding for external play/coach hosts
- a Docker profile that can run the portal, API, Blazor head, hub head, and browser-hosted support surfaces for local or self-hosted use

## User Workflow Contract

The browser client should feel like a desktop Chummer session delivered through the web:

- launch into a workbench with practical first actions, not a feature teaser
- create a new runner or resume a prior workspace from the same top-level product route
- move from startup into rules/profile/build/gear/magic/support editing without changing mental model
- open dialogs from route-stable section actions and return to visibly updated sheet state after commit
- hit save/export/print/download affordances that are clearly browser-native in behavior but still sit in the same workflow slot as desktop
- survive refresh, reconnect, reload, and direct route-entry without breaking the active job
- hand off to downloads/account/support/session/coach surfaces through the same portal origin when those product lanes are enabled

## Browser/Desktop Parity Rules

Parity for the web lane is workflow parity, not screenshot resemblance.

The minimum rules are:

- the web client must preserve the same task order a Chummer user expects from the desktop head unless there is a browser constraint that forces a different route
- when the browser route differs, the difference must still keep the user inside the same product-shaped workflow and be documented as an intentional substitute
- route-entry proof alone is never enough for parity claims
- dialog entry proof alone is never enough for parity claims
- a restored-session route only counts when the resulting visible state proves the continued task actually resumed
- public hosted proof and Docker self-host proof are related but separate; one must not be used as a substitute for the other

## Desktop-Equivalent Workflow Bar

The web client is not considered desktop-equivalent until browser-specific evidence proves these workflows:

- first launch presents the same practical workbench entry points as Avalonia
- new-character and origin-dossier flows expose matching required choices and dialogs
- open/import/save/export/print workflows either work in browser form or fail with clear user-facing alternatives
- dense runner sheet navigation covers the same core sections and quick actions as Avalonia
- ruleset-specific SR4/SR5/SR6 posture remains explicit and test-backed
- account/owner context flows through API requests and survives reload/deep-link use
- workspace continuity and recovery are usable from the browser
- dialogs fit desktop and mobile browser viewports without clipped labels or hidden primary actions
- browser route, reconnect, and reload behavior works under `/blazor/`
- public hosted and Docker self-hosted lanes have separate proof so local success is not mistaken for `chummer.run` readiness

The workflow families that matter most for Chummer6 design completion are:

- startup and recent-work recovery
- runner creation and origin/rules selection
- dense runner-sheet editing across the core section families
- dialog-driven add/edit/commit loops
- browser result continuations for save, save as, export, print, and download
- cross-route continuity between workbench, support, downloads, account, and optional coach/session surfaces

## Verification Bar

Completion needs evidence, not intent.

Required proof should include:

- Blazor component/unit tests for shared workbench contracts and command/dialog coverage
- Playwright coverage for `/blazor/` through `Chummer.Portal`
- route/reload/reconnect checks under the portal path base
- browser screenshots for startup, loaded runner, dense section, dialog, mobile viewport, and key workflow states
- Docker compose smoke for self-hosted portal/API/Blazor use
- public-edge smoke against the `chummer.run` route when the hosted lane is being promoted
- a separate hosted execution-proof lane for `chummer.run` browser workflows, not only hosted route-entry posture
- parity ledger entries that distinguish Avalonia native behavior from browser behavior
- operator documentation that points self-host users at the portal-backed browser workbench lane instead of treating raw Blazor hosting as the product shape

Current browser-backed proof now includes:

- a first-class `/workbench` route alias for the browser shell in addition to `/preview`
- portal-backed `/blazor/` landing that resolves into `/blazor/workbench`
- state-backed recent-work resume links on `/workbench`, plus explicit `/workbench?workspace={id}` restoration for shared-session continuity
- restored-session build-lab continuation on `/workbench` that reopens the active workspace directly on the create/build lane
- restored-session continuation lanes on `/workbench` that reopen the active workspace directly on profile, rules, gear, and advanced tabs
- restored-session action continuations on `/workbench` that reopen the active workspace directly into multiple live editing action dialogs across workflow families, including a promoted career-entry continuation for the calendar/support utility lane
- restored-session committed action continuations on `/workbench` that complete multiple resumed editing actions and leave visible state changes behind
- restored-session committed complex-form continuation on `/workbench` so the advanced technomancer lane now matches the same committed-route posture already used by the other promoted advanced actions
- restored-session result continuations on `/workbench` that reopen the active workspace directly into browser download/export/print flows
- startup deep links for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- hosted dense startup utility posture for `character_roster` and `master_index`, with browser-visible roster/reference layout markers beyond simple dialog-entry proof
- hosted origin/rules continuity posture for `new_character_origin` and restored `tab-rules`, with visible origin-wizard structure and rules-environment summary markers on the promoted workbench route
- hosted build-lab continuity posture for restored `tab-create`, with visible planner, progression, and export/hand-off surfaces on the promoted workbench route
- hosted weapon-selection posture for restored `tab-combat&control=combat_add_weapon`, with visible catalog/accessories/filter markers on the promoted workbench route
- staged hosted combat-support posture for restored `tab-combat` utility controls `combat_add_armor`, `combat_reload`, and `combat_damage_track`, with the next hosted/self-host proof refresh set to assert armor selection, reload posture, damage-track review, and visible active-combat context without yet claiming full combat-state mutation parity
- hosted skill-selection posture for restored `tab-skills&control=skill_add`, with visible linked-attribute/filter markers on the promoted workbench route
- staged hosted skill-maintenance posture for restored `tab-skills` utility controls `skill_specialize`, `skill_remove`, and `skill_group`, with the next hosted/self-host proof refresh set to assert specialization, removal/recovery, group-composition, and current-rating context without yet claiming full skill-state mutation parity
- staged hosted magic/resonance support posture for restored utility controls `adept_power_add`, `spirit_add`, `critter_power_add`, and `matrix_program_add`, with the next hosted/self-host proof refresh set to assert power, spirit, critter, and Matrix-program utility dialog posture without yet claiming full magic/resonance state mutation parity
- hosted vehicle-selection posture for restored `tab-gear&control=vehicle_add`, with visible catalog, filters, and dense selection details on the promoted workbench route
- hosted vehicle-mod-selection posture for restored `tab-gear&control=vehicle_mod_add`, with visible slot/availability/source detail markers on the promoted workbench route
- hosted quality-selection posture for restored `tab-qualities&control=quality_add`, with visible category/karma/source/filter markers on the promoted workbench route
- hosted quality-delete posture for restored `tab-qualities&control=quality_delete`, with visible karma-impact and recovery markers on the promoted workbench route
- hosted spell-selection posture for restored `tab-magician&control=spell_add`, with visible drain/source/category markers on the promoted workbench route
- hosted magic-delete posture for restored `tab-magician&control=magic_delete`, with visible drain-impact and recovery markers on the promoted workbench route
- hosted cyberware-selection posture for restored `tab-cyberware&control=cyberware_add`, with visible essence/cost/source/filter markers on the promoted workbench route
- hosted cyberware-edit posture for restored `tab-cyberware&control=cyberware_edit`, with visible installed-ware and live recalculation markers on the promoted workbench route
- hosted cyberware-delete posture for restored `tab-cyberware&control=cyberware_delete`, with visible removal impact and recovery markers on the promoted workbench route
- hosted drug-selection posture for restored `tab-gear&control=drug_add`, with visible catalog and crash/speed/source detail markers on the promoted workbench route
- staged hosted gear-maintenance posture for restored `tab-gear` utility controls `gear_add`, `gear_edit`, and `gear_delete`, with the next hosted/self-host proof refresh set to assert generic gear catalog, edit context, removal/recovery, and inventory-list posture without yet claiming full gear-state mutation parity
- staged hosted source/gear utility posture for restored utility controls `show_source`, `gear_source`, `gear_mount`, and `toggle_free_paid`, with the next hosted/self-host proof refresh set to assert source-reference, gear source, attachment/mount, and cost posture without yet claiming full source/cost/attachment parity
- staged hosted magic cleanup utility posture for restored utility controls `magic_add`, `magic_bind`, `magic_source`, and `drug_delete`, with the next hosted/self-host proof refresh set to assert magic add, binding, source-reference, and drug-removal posture without yet claiming full magic/drug mutation parity
- staged hosted browser output handoff posture for restored output commands `save_character`, `save_character_as`, `export_character`, and `print_character`, with the promoted workbench cards exposing browser-native save, download, export download, and print-preview continuations without yet claiming full save/export/print/download parity
- staged hosted workbench portal handoff posture for `/downloads/`, `/status`, `/contact`, and `/account/work`, with the promoted workbench exposing same-origin download, release-truth, support, and account/work cards without yet claiming portal, authentication, installer, or support-submission runtime parity
- staged hosted workbench polish posture for the Task dock, keeping start, edit, output, and portal handoff shortcuts visible on the promoted browser route without yet claiming screenshot, accessibility, or runtime workflow parity
- staged hosted workbench recovery posture for the Session recovery strip, making refresh, direct entry, and portal handoff continuation visible through recent, Build Lab, profile, status, restored workspace, restored gear, and restored output affordances without yet claiming reload/session persistence runtime parity
- staged hosted workbench hosting/privacy posture for hosted route, Docker self-host, and analytics privacy copy, making Rybbit is optional and metadata-only visible on the promoted route while naming character, owner, workspace, XML, and dossier-content exclusions without yet claiming Docker runtime, hosted route availability, or analytics delivery
- staged hosted workbench command-palette posture for keyboard-style hints and reload-safe workbench links, making common new, open, Build Lab, gear, save/download, print, and support commands discoverable without yet claiming actual keyboard-event handling or command execution runtime parity
- staged hosted workbench density posture for Compact desktop, Comfortable review, and Mobile safe display options, making dense desktop ergonomics and comfortable and mobile-safe postures visible without yet claiming persisted preferences, runtime layout mutation, or screenshot parity
- staged hosted workbench workflow-ledger posture for visible startup, editing, output, recovery, portal, and boundary rows, making browser-client capability and desktop handoff boundaries visible in the client and explicitly stating that some desktop-only actions still open Chummer desktop without yet claiming runtime capability or browser parity
- staged hosted workbench file-intake posture for browser-safe open/import, Hero Lab import, XML editor, native file-system handoff, and support paths, keeping file intake visible in the promoted route without yet claiming file picker, import execution, XML mutation, or native file-system handoff parity
- staged hosted workbench rules/data posture for ruleset choice, sourcebook review, XML/custom data, and translation tools, keeping rules and references visible in the promoted route while not yet claiming ruleset mutation, sourcebook runtime, XML mutation, or localization runtime parity
- staged hosted workbench settings posture for global settings, character settings, ruleset choice, update status, and support handoff, keeping setup changes reachable from the promoted route while not yet claiming persisted preference mutation or runtime settings parity
- staged hosted workbench diagnostics posture for runtime inspector, About, health, status, and preview tools, keeping diagnostics reachable from the promoted route while not yet claiming runtime health, build validity, or diagnostics execution parity
- staged hosted workbench connected-runtime posture for play, session, coach, assistant, and status links, keeping optional live lanes reachable from the promoted route while not yet claiming connected-runtime execution, signed owner forwarding, or downstream service health
- staged hosted workbench accessibility posture for keyboard order, dialog fit, readable density, reduced motion, and help links, keeping access affordances visible on the promoted route while not yet claiming screen-reader, keyboard-event, screenshot, or browser accessibility validation
- staged hosted workbench section-rail posture for profile, build, skills, gear, combat, magic, matrix, contacts, and career shortcuts, keeping sheet-scale navigation visible on the promoted route while not yet claiming section rendering or browser execution parity
- staged hosted workbench desktop install handoff posture for downloads, update channel, status, account, self-host notes, and support, keeping browser-to-desktop continuity visible on the promoted route while not yet claiming installer download or Docker runtime parity
- staged hosted workbench menu-bar posture for file, build, view, character, tools, and help entry points, keeping familiar menu affordances visible on the promoted route while not yet claiming keyboard accelerator or menu-command execution parity
- staged hosted workbench workspace-tabs posture for active runner, build lab, print/export, and recent import lanes, keeping runner task tabs visible on the promoted route while not yet claiming multi-document state or tab persistence parity
- staged hosted workbench status-bar posture for save, rules, validation, session, privacy, and support state cues, keeping current-state affordances visible on the promoted route while not yet claiming save execution, validation execution, analytics delivery, or session runtime parity
- staged hosted workbench inspector-rail posture for summary, build checks, inventory, notes, and sources, keeping side-context affordances visible on the promoted route while not yet claiming live inspector state or split-pane persistence parity
- staged hosted workbench dialog-stack posture for active dialog, committed result, retry, back-to-sheet, and support continuations, keeping modal and result continuations visible on the promoted route while not yet claiming modal execution or committed-action runtime parity
- staged hosted workbench context-actions posture for add, edit, remove, duplicate, source lookup, and recover actions, keeping selection-style actions visible without hidden right-click dependency while not yet claiming right-click menu or selection-state runtime parity
- staged hosted workbench search/filter posture for roster, gear, skills, qualities, sources, and clear filter lanes, keeping dense-list filtering visible from the promoted workbench chrome while not yet claiming live search indexing or filter execution parity
- staged hosted workbench layout-presets posture for dense sheet, split review, output, mobile safe, and focus pane modes, keeping browser-safe layout choices visible on the promoted route while not yet claiming pane resizing or persisted layout parity
- staged hosted workbench activity-feed posture for save event, validation warning, output event, hosted status, and support escape entries, keeping recent activity and recovery cues visible on the promoted route while not yet claiming live event logging or toast delivery parity
- staged hosted workbench keyboard-shortcuts posture for command help, save/output, section jump, density toggle, and support escape affordances, keeping power-user keyboard help visible on the promoted route while not yet claiming key-event handling or accelerator execution parity
- staged hosted workbench resource-meters posture for karma, nuyen, essence, limits, wounds, and lifestyle context, keeping core character totals visible on the promoted route while not yet claiming live character-total calculation parity
- staged hosted workbench tree-tools posture for expand, collapse, sort, reorder, pin, and selection tools, keeping dense tree/list affordances visible on the promoted route while not yet claiming tree virtualization or list mutation parity
- staged hosted workbench save-session posture for save, Save As, autosave, dirty state, recovery, and export lifecycle actions, keeping desktop-like session state visible on the promoted route while not yet claiming persisted browser mutation or file-write parity
- staged hosted workbench output-handoff posture for PDF packet, print sheet, HTML summary, share link, audit queue, and download bundle actions, keeping desktop-like output generation visible on the promoted route while not yet claiming print, PDF, share, or download execution parity
- staged hosted workbench validation-queue posture for rule issues, missing fields, cost checks, availability limits, build gate, and fix-next navigation, keeping desktop-like build readiness visible on the promoted route while not yet claiming rules-engine execution or validation-result parity
- staged hosted workbench history-undo posture for undo, redo, snapshot, compare, restore, and conflict review actions, keeping desktop-like recovery context visible on the promoted route while not yet claiming actual rollback, diff, or conflict-resolution parity
- staged hosted workbench sync-presence posture for connection, offline, local cache, sync queue, presence, and handoff cues, keeping hosted and Docker self-host session state visible on the promoted route while not yet claiming network sync, offline-cache, or multi-user presence parity
- staged hosted workbench data-packs posture for sourcebooks, errata, custom data, update packs, validation scope, and data-folder context, keeping desktop-like rules/data management visible on the promoted route while not yet claiming live sourcebook loading, custom data import, or data-update parity
- staged hosted workbench character-library posture for open, recent, pin, clone, archive, and import actions, keeping desktop-like character library management visible on the promoted route while not yet claiming file-open, library persistence, clone, archive, or import parity
- staged hosted workbench campaign-session posture for roster, GM review, session notes, rewards, table share, and run handoff actions, keeping live-table workflow visible on the promoted route while not yet claiming campaign persistence, GM approval, reward mutation, or table-share parity
- staged hosted workbench observability-privacy posture for consent, Rybbit status, route events, error traces, privacy log, and self-host telemetry toggle actions, keeping Chummer Run analytics posture visible on the promoted route while not yet claiming Rybbit deployment, event delivery, consent persistence, or telemetry runtime parity
- staged hosted workbench first-run posture for new runner, desktop import, sample runner, restore session, self-host setup, and docs actions, keeping web onboarding visible on the promoted route while not yet claiming setup persistence, migration, import, or Docker installer execution parity
- staged hosted workbench PWA-install posture for install prompt, offline cache, update available, browser permissions, release channel, and reset cache actions, keeping desktop-like web app install/update posture visible on the promoted route while not yet claiming service-worker, install prompt, cache update, or browser permission parity
- staged hosted workbench Docker-operator posture for container health, env check, volume mounts, backup, image update, and support bundle actions, keeping Docker self-host operations visible on the promoted route while not yet claiming live container inspection, env validation, backup, image update, or log-bundle parity
- staged hosted workbench security-access posture for sign-in, workspace lock, roles, session expiry, key rotation, and access audit actions, keeping hosted access-control posture visible on the promoted route while not yet claiming authentication, RBAC, session expiry, key rotation, or audit-log parity
- staged hosted workbench notifications-jobs posture for job queue, retry, dismiss, settings, history, and support actions, keeping async save/export/sync/import work visible on the promoted route while not yet claiming toast delivery, queue execution, retry, or background-worker parity
- staged hosted workbench touch-mobile posture for touch mode, zoom, panel dock, compact actions, keyboard-safe layout, and pointer help actions, keeping phone, tablet, and trackpad ergonomics visible on the promoted route while not yet claiming touch gesture, viewport, virtual-keyboard, or mobile browser parity
- staged hosted workbench navigation-deeplink posture for breadcrumbs, URL state, back/forward, copy route, tab restore, and shared anchor actions, keeping browser navigation and shareable workspace context visible on the promoted route while not yet claiming router-state, browser-history, route-copy, or deep-link restore parity
- staged hosted workbench inline-editing posture for dirty fields, numeric steppers, commit, revert, formula preview, and bulk apply actions, keeping dense controlled editing visible on the promoted route while not yet claiming field mutation, edit persistence, formula evaluation, or bulk mutation parity
- staged hosted workbench performance-virtualization posture for lazy sections, virtual lists, render budget, memory posture, degraded mode, and profiler actions, keeping large-sheet browser scaling posture visible on the promoted route while not yet claiming virtualized rendering, lazy loading, memory control, or profiler parity
- staged hosted workbench print-layout posture for sheet template, paper size, theme, sections, preview, and export profile actions, keeping desktop-like sheet output profile control visible on the promoted route while not yet claiming print CSS, PDF rendering, paper layout, or export-profile parity
- staged hosted workbench portrait-attachments posture for portrait, token art, notes, attachments, import media, and cleanup actions, keeping character media and reference attachments visible on the promoted route while not yet claiming file upload, storage persistence, thumbnail generation, or attachment cleanup parity
- staged hosted workbench windowing-panes posture for split view, pop-out, pinned inspector, focus mode, second screen, and restore layout actions, keeping desktop-like pane management visible on the promoted route while not yet claiming multi-window, focus handling, second-screen routing, or layout persistence parity
- staged hosted workbench calculation-provenance posture for derived breakdown, modifier stack, rule source, stale values, manual override, and dependency path actions, keeping derived-stat explainability visible on the promoted route while not yet claiming calculation-engine, dependency tracing, recalculation, or override parity
- staged hosted workbench lifecycle-calendar posture for downtime, lifestyle upkeep, subscriptions, reminders, recurring costs, and next session actions, keeping between-session upkeep visible on the promoted route while not yet claiming scheduling, reminder delivery, recurring cost mutation, or session calendar parity
- staged hosted workbench progression-ledger posture for karma spend, nuyen ledger, purchase queue, reputation, carryover, and audit trail actions, keeping advancement accounting visible on the promoted route while not yet claiming ledger mutation, purchase execution, reputation mutation, or accounting parity
- staged hosted workbench import/reconcile posture for file selection, parse summary, rules mapping, custom data, conflict review, and final acceptance, keeping existing-runner migration visible on the promoted route while not yet claiming file upload, XML parsing, migration, conflict resolution, or import persistence parity
- staged hosted workbench compare/merge posture for diff view, conflict choice, source trace, dry run, apply, and rollback actions, keeping merge review visible on the promoted route while not yet claiming diff execution, conflict resolution, source tracing, merge application, or rollback parity
- staged hosted workbench restore/checkpoint posture for autosave, named checkpoint, backup, preview, rollback, and retention actions, keeping recovery visible on the promoted route while not yet claiming snapshot persistence, restore execution, backup generation, rollback mutation, or retention policy parity
- staged hosted workbench offline/cache posture for cache status, queued edits, reconnect, local export, stale data, and sync health actions, keeping network-interruption continuity visible on the promoted route while not yet claiming service-worker caching, queued mutation persistence, reconnect execution, offline export generation, or sync reconciliation parity
- staged hosted workbench session-locking posture for lock status, owner handoff, read-only fallback, stale recovery, conflict owner, and takeover actions, keeping edit ownership visible on the promoted route while not yet claiming lock acquisition, takeover mutation, cross-tab arbitration, stale lock cleanup, or conflict persistence parity
- staged hosted workbench share/export privacy posture for redaction, scope, expiry, revocation, history, and local-only export actions, keeping private handoff visible on the promoted route while not yet claiming share-token issuance, redaction execution, revocation persistence, history storage, or export-policy enforcement parity
- staged hosted workbench table-handoff posture for GM packet, initiative card, condition tracker, public handout, private notes, and table export actions, keeping session-table output visible on the promoted route while not yet claiming packet generation, live GM sharing, condition synchronization, handout publication, or table export persistence parity
- staged hosted workbench rules-citation posture for source packet, citation scope, errata note, table summary, dispute trail, and audit export actions, keeping rule explanation visible on the promoted route while not yet claiming citation generation, source lookup, errata resolution, dispute persistence, or audit export generation parity
- staged hosted workbench localization/terminology posture for language, units, dates, currency, table terms, and source-title affordances, keeping table-specific wording visible on the promoted route while not yet claiming translation coverage, formatter execution, localized source-title lookup, persisted locale settings, or export localization parity
- staged hosted workbench help/recovery guidance posture for context help, shortcut hints, error explanations, recovery suggestions, docs links, and support handoff affordances, keeping guided browser recovery visible on the promoted route while not yet claiming contextual help resolution, keyboard shortcut execution, support-ticket creation, docs search, or error remediation parity
- character roster custom hierarchy posture for user-created virtual folders, nested groups, drag/drop move intent, explicit Move Runner/Folder actions, watched-file links that do not move disk files by default, safe folder deletion that moves runner/link items to Inbox first and reparents child folders, non-destructive metadata mutation that stages create/rename/delete/move/reorder changes before any filesystem mutation, editable Folder Name plus option-backed Source Folder and Target Folder fields with a custom-only source picker, styled visible hierarchy status counts with pending-move disclosure, and hidden Source Item carriage for web workflow control, a Blazor drag/drop event bridge that fills `rosterTargetFolder`, carries `rosterSourceFolder` for folder drops, carries `rosterSourceItem` for dragged runner/link rows, preserves full runner labels while stripping visual row suffixes, invokes the same virtual move/reorder actions, and blocks folder cycles when a parent is dropped into its own descendant, Blazor dialog markup that exposes roster hierarchy rows with virtual-folder data attributes, selected-row state, drag-handle styling, row title affordances with row-level keyboard help, aria labels, aria-describedby linkage to live source feedback, labelled tree containers, vertical tree orientation, tree/treeitem roles, aria-level depth metadata, aria-selected state for selected/source rows, aria-expanded state for folder rows, aria-keyshortcuts for actionable rows, nullable optional ARIA emission for presentation rows, focusability, visible keyboard drag-source state, separate mouse and keyboard source state with mode-specific source badges and live source status instructions, mouse drag-end cleanup for stale mouse sources, atomic live source feedback, visible keyboard operation guidance, hierarchy-status keyboard shortcut summary, default browser key suppression for actionable roster rows, and Enter/Space/Escape keyboard handling before full hosted drag execution is claimed, a shared `RosterHierarchyState` contract for folders, item links, move intent, safe filesystem-confirmation policy, `RosterHierarchyJson` preference staging for non-destructive layout metadata, safe staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, and hidden settings carriage for later owner-scoped persistence
- source-level legacy UI-control coverage guard for all known `LegacyUiControlCatalog` IDs, mapping each control into hosted execution baseline evidence or a staged source-alignment family before any future browser proof refresh claims breadth
- aggregate source-staged proof set for the staged browser workflow families, deliberately separate from hosted execution proof and Docker self-host proof so staged breadth cannot be mistaken for runtime parity
- hosted contact-connection posture for restored `tab-contacts&control=contact_connection`, with visible selected-contact summary context and compact edit controls on the promoted workbench route
- hosted vehicle-edit posture for restored `tab-gear&control=vehicle_edit`, with visible vehicle details, live summary, and garage context on the promoted workbench route
- hosted vehicle-delete posture for restored `tab-gear&control=vehicle_delete`, with visible impact and recovery context on the promoted workbench route
- hosted contact-delete posture for restored `tab-contacts&control=contact_remove`, with visible roster impact and recovery context on the promoted workbench route
- hosted contact-edit posture for restored `tab-contacts&control=contact_edit`, with visible selected-contact details and in-place edit context on the promoted workbench route
- staged hosted career/support posture for restored `tab-calendar`, `create_entry`, `dialog_action=add`, `edit_entry`, `dialog_action=apply`, `delete_entry`, `dialog_action=delete`, `open_notes`, `dialog_action=save`, `move_up`, and `move_down`, with the restored-continuations and restored-actions cards exposing career/support utility lanes and the next hosted execution refresh set to assert section landing, compact add/edit list-detail editors, remove/recovery posture, runner notes editing, classic list reorder utilities, and visible add/edit/delete/notes-save commit results
- staged hosted identity/SIN/license posture for restored `tab-info` controls `identity_license_add`, `identity_license_edit`, and `identity_license_delete`, with the promoted workbench cards exposing add/edit/remove affordances and the next hosted execution refresh set to assert compact identity, source, rating, lifestyle-cover, and recovery context
- seeded runner continuity routes for Build Lab, Rules, Contacts, and Complex Forms
- seeded browser result-state routes for `save_character`, `save_character_as`, `print_character`, and `export_character` plus `dialog_action=download`, proving browser-visible save/download/print/export outcomes instead of only dialog entry posture
- separate self-host receipt proof for portal-backed `/blazor/workbench` and `/blazor/preview` routes under Docker, with the next refresh staged to include the same career/support section, add/edit/delete action, runner-notes action, move up/down list utilities, and committed-result lane as the hosted proof runner
- separate hosted route-entry proof for the `https://chummer.run/blazor/` public edge, including route shapes for restored result continuations, action continuations, committed action continuations, and staged career/support section/action routes
- a dedicated hosted execution-proof contract, runner scaffold, verifier, and published passing receipt so `chummer.run` browser workflow execution is promoted with its own evidence instead of being conflated with self-host proof, including hosted startup-command execution for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- optional browser analytics posture proof for hosted `chummer.run` Rybbit instrumentation and self-host default-off behavior, published as `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`, with route/workflow metadata only and no character, owner, workspace, document, XML, payload, hash, or dossier content capture
- connected-runtime posture proof for optional session, coach, and assistant portal forwarding, published as `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`, proving the signed portal-owner boundary and visible configured/off browser posture without claiming full downstream connected-runtime workflow parity

The remaining gap is breadth, not direction: the promoted hosted `/blazor/workbench` lane now has both separate route-entry proof and separate hosted execution proof, but more Avalonia-equivalent workflows still need browser-specific proof before the web client can claim full desktop equivalence.

## Current Truth

This is a target state, not a completed claim.

Current release language may still identify Avalonia as the native flagship desktop head and Blazor Desktop as a fallback desktop package. That does not conflict with this goal. The new goal is to raise the browser-hosted `Chummer.Blazor` client to a first-class web client with its own proof, hosted route, and Docker self-hosting story.

## Target Product Contract

The browser client target is not a preview page and not a reduced companion portal. It is the web-delivered Chummer client head:

- `https://chummer.run/blazor/workbench` is the hosted public product route.
- Docker self-hosting must expose the same portal-backed `/blazor/workbench` workflow shape, with local operator configuration instead of hosted defaults.
- Avalonia remains the native desktop head, but browser parity means users can perform the same practical Chummer workflow in the web client unless a browser-specific boundary is explicitly called out.
- Rybbit may be enabled on hosted `chummer.run` for sanitized product telemetry; self-host Docker installs stay default-off and operator-controlled.
- Analytics must stay metadata-only: route family, command, tab, control, dialog action, and coarse workspace/fixture presence are allowed, while character data, owner identifiers, workspace identifiers, XML, files, payloads, hashes, and dossier content are excluded.
- Release language must distinguish source-staged affordance breadth from hosted execution proof, Docker self-host proof, and parity-ledger evidence.

The immediate Chummer6 design direction is therefore:

- keep pushing `/blazor/workbench` as the product route
- keep `/blazor/preview` as proof/supporting evidence, not the primary product promise
- keep expanding browser workflow-family breadth beyond the now-passing hosted execution baseline
- only upgrade release language when hosted browser execution proof, self-host proof, and parity ledger evidence all support the claim
