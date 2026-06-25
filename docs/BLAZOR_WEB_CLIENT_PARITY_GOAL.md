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
- a deliberate slate/amber/mint/blue browser-client palette with warm Chummer desktop chrome, strong focus states, and themed Character Library hierarchy surfaces so `/blazor/app` feels like a real client instead of a default preview page
- portal recovery pages for downloads, docs, help, status, and contact must preserve the same polished Chummer Online visual language with restrained ambient glow and deep ink/surface contrast, keep the portal root route rail labelled as `Chummer browser routes` with explicit hover/focus affordances and reduced-motion handling, and keep help/contact/status recovery exits as labelled recovery rails with pill-style keyboard focus, explicit hover/focus affordances, and reduced-motion guards

## Promoted Route Shape

The Chummer6 browser lane should present a clear route hierarchy:

- `/app` is the clean public Chummer Online route on the portal edge and should redirect into the hosted Blazor app while preserving command query strings
- `/blazor/` is the stable Blazor entry and should resolve into Chummer Online, not into a detached proof page or proof-named workbench route
- `/blazor/app` is the hosted Blazor implementation path for the same promoted browser client, while public CTAs should use `/app` or relative `app` links instead of exposing internal workbench language
- when the promoted browser shell is entered through `/app` or `/blazor/app`, its own workflow links should remain on the app route and use path-base-safe relative hrefs inside Blazor so users stay in Chummer Online under hosted `/blazor`, Docker self-host, and direct app hosting; `/blazor/workbench` remains an explicit proof-compatible route, not the default label shown to users
- portal-generated public CTAs should use `/app?command=character_roster`; Blazor-rendered internal links must use relative `app?command=character_roster`/`app` style hrefs so the same markup works under any path base
- `/blazor/home` is the explicit orientation/landing page for product copy and self-host evaluation; `/blazor/` should remain the stable public entry that moves users into Chummer Online rather than exposing proof-route language
- `/blazor/workbench` is the explicit proof-compatible route for the same promoted browser client
- `/blazor/preview` is allowed to remain a denser proof shelf, but it is not the primary user promise
- deep links under `/blazor/workbench` must be reload-safe and continuity-safe for startup commands, restored workspaces, section continuations, dialog/action continuations, and browser result continuations

This matters because the user workflow is part of the design. Chummer6 web parity is not just "the same controls exist"; it also requires the same practical entry path and continuation posture.

## Hosting Requirement

The public hosted path is `chummer.run` through `Chummer.Portal`.

The canonical self-host operator reference for this lane is `docs/BLAZOR_SELF_HOST_RUNBOOK.md`, with baseline environment defaults in `docs/examples/self-hosted-browser-workbench.env.example`.
The current hosted route-entry proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`.
The current hosted execution-proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`.

The portal must provide:

- a browser-head default landing that resolves `/blazor/` into Chummer Online, with `/blazor/home` kept as the explicit product/orientation page and `/blazor/workbench` retained for proof-compatible entry
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
- operator documentation that points self-host users at the portal-backed browser-client lane instead of treating raw Blazor hosting as the product shape

Current browser-backed proof now includes:

- a first-class `/workbench` route alias for the browser shell in addition to `/preview`
- portal-backed `/app` public route and `/blazor/` landing that resolve into Chummer Online, with `/blazor/app` as the hosted app path, `/blazor/home` as the product/orientation page, and `/blazor/workbench` retained as the explicit proof-compatible compatibility route for the same promoted browser client
- state-backed recent-work resume links on `/workbench`, plus explicit `/workbench?workspace={id}` restoration for shared-session continuity
- restored-session build-lab continuation on `/workbench` that reopens the active workspace directly on the create/build lane
- restored-session continuation lanes on `/workbench` that reopen the active workspace directly on profile, rules, gear, and advanced tabs
- restored-session action continuations on `/workbench` that reopen the active workspace directly into multiple live editing action dialogs across workflow families, including a promoted career-entry continuation for the calendar/support utility lane
- restored-session committed action continuations on `/workbench` that complete multiple resumed editing actions and leave visible state changes behind
- restored-session committed complex-form continuation on `/workbench` so the advanced technomancer lane now matches the same committed-route posture already used by the other promoted advanced actions
- restored-session result continuations on `/workbench` that reopen the active workspace directly into browser download/export/print flows
- startup deep links for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- hosted dense startup utility posture for `character_roster` and `master_index`, with browser-visible roster/reference layout markers beyond simple dialog-entry proof
- hosted origin/rules continuity posture for `new_character_origin` and restored `tab-rules`, with visible origin-wizard structure and rules-environment summary markers through the canonical proof-compatible route
- hosted build-lab continuity posture for restored `tab-create`, with visible planner, progression, and export/hand-off surfaces through the canonical proof-compatible route
- hosted weapon-selection posture for restored `tab-combat&control=combat_add_weapon`, with visible catalog/accessories/filter markers through the canonical proof-compatible route
- staged hosted combat-support posture for restored `tab-combat` utility controls `combat_add_armor`, `combat_reload`, and `combat_damage_track`, with the next hosted/self-host proof refresh set to assert armor selection, reload posture, damage-track review, and visible active-combat context without yet claiming full combat-state mutation parity
- hosted skill-selection posture for restored `tab-skills&control=skill_add`, with visible linked-attribute/filter markers through the canonical proof-compatible route
- staged hosted skill-maintenance posture for restored `tab-skills` utility controls `skill_specialize`, `skill_remove`, and `skill_group`, with the next hosted/self-host proof refresh set to assert specialization, removal/recovery, group-composition, and current-rating context without yet claiming full skill-state mutation parity
- staged hosted magic/resonance support posture for restored utility controls `adept_power_add`, `spirit_add`, `critter_power_add`, and `matrix_program_add`, with the next hosted/self-host proof refresh set to assert power, spirit, critter, and Matrix-program utility dialog posture without yet claiming full magic/resonance state mutation parity
- hosted vehicle-selection posture for restored `tab-gear&control=vehicle_add`, with visible catalog, filters, and dense selection details through the canonical proof-compatible route
- hosted vehicle-mod-selection posture for restored `tab-gear&control=vehicle_mod_add`, with visible slot/availability/source detail markers through the canonical proof-compatible route
- hosted quality-selection posture for restored `tab-qualities&control=quality_add`, with visible category/karma/source/filter markers through the canonical proof-compatible route
- hosted quality-delete posture for restored `tab-qualities&control=quality_delete`, with visible karma-impact and recovery markers through the canonical proof-compatible route
- hosted spell-selection posture for restored `tab-magician&control=spell_add`, with visible drain/source/category markers through the canonical proof-compatible route
- hosted magic-delete posture for restored `tab-magician&control=magic_delete`, with visible drain-impact and recovery markers through the canonical proof-compatible route
- hosted cyberware-selection posture for restored `tab-cyberware&control=cyberware_add`, with visible essence/cost/source/filter markers through the canonical proof-compatible route
- hosted cyberware-edit posture for restored `tab-cyberware&control=cyberware_edit`, with visible installed-ware and live recalculation markers through the canonical proof-compatible route
- hosted cyberware-delete posture for restored `tab-cyberware&control=cyberware_delete`, with visible removal impact and recovery markers through the canonical proof-compatible route
- hosted drug-selection posture for restored `tab-gear&control=drug_add`, with visible catalog and crash/speed/source detail markers through the canonical proof-compatible route
- staged hosted gear-maintenance posture for restored `tab-gear` utility controls `gear_add`, `gear_edit`, and `gear_delete`, with the next hosted/self-host proof refresh set to assert generic gear catalog, edit context, removal/recovery, and inventory-list posture without yet claiming full gear-state mutation parity
- staged hosted Runner Intelligence posture for restored `tab-stats` controls `runner_benchmark`, `runner_what_if`, and `runner_cohort_privacy`, using Chart LTD as the character-statistics layer for percentile benchmarks, Increase Initiative Force 6 spell/drug/gear what-if modeling, inventory synergy, drain/stun probability, and opt-in anonymized hosted cohorts, with reusable calculation in `Chummer.Presentation.RunnerIntelligence` for Blazor and Avalonia and without yet claiming statistical-engine execution, hosted cohort aggregation, Docker local benchmark persistence, or rules-engine calculation parity
- staged hosted source/gear utility posture for restored utility controls `show_source`, `gear_source`, `gear_mount`, and `toggle_free_paid`, with the next hosted/self-host proof refresh set to assert source-reference, gear source, attachment/mount, and cost posture without yet claiming full source/cost/attachment parity
- staged hosted magic cleanup utility posture for restored utility controls `magic_add`, `magic_bind`, `magic_source`, and `drug_delete`, with the next hosted/self-host proof refresh set to assert magic add, binding, source-reference, and drug-removal posture without yet claiming full magic/drug mutation parity
- staged hosted browser output handoff posture for restored output commands `save_character`, `save_character_as`, `export_character`, and `print_character`, with the Chummer Online and proof-compatible workbench cards exposing browser-native save, download, export download, and print-preview continuations without yet claiming full save/export/print/download parity
- staged hosted workbench portal handoff posture for `/downloads/`, `/status`, `/contact`, `/help`, and `/account/work`, with the Chummer Online and proof-compatible workbench exposing same-origin download, release-truth, support, help, and account/work cards without yet claiming portal, authentication, installer, help-runtime, or support-submission runtime parity
- staged portal installer/support handoff posture keeps portal root, downloads, docs, help, status, and contact visually aligned with Chummer Online through the polished slate/amber/mint/blue palette, restrained ambient glow, deep ink/surface contrast, labelled help/contact/status recovery rails, pill-style keyboard focus, hover/focus affordances, and reduced-motion guards without yet claiming portal runtime, installer availability, hosted execution, or Docker self-host execution proof
- staged hosted workbench polish posture for the Task dock and final slate/amber/mint/blue Chummer Online theme layer, including the refined final amber/mint/blue Chummer Online theme layer, keeping start, edit, output, and portal handoff shortcuts visible while preserving `/app` as the clean public browser-client route, `/blazor/app` as the hosted app path, `/blazor/workbench` as the proof-compatible route, and `/blazor/preview` as the preview tools/result-state route, with primary New runner and Open/import actions, keyboard-visible primary startup focus, mobile touch-friendly startup actions, portal-handoff header nav treatment, keyboard-visible portal nav focus, cohesive app-shell background, card, action, hover, focus, density-control checked-state, route-token app chrome treatment, mobile route-token wrapping, keyboard-visible route-token focus, high-contrast route-token affordances, route-aware status strip chrome, route-state status pill styling, desktop left-edge section accents, mobile top-edge section accents, reduced-motion-safe command-deck reveal, and reduced-motion styling without yet claiming screenshot, accessibility, or runtime workflow parity
- staged hosted workbench recovery posture for the Session recovery strip, making refresh, direct entry, and portal handoff continuation visible through recent, Build Lab, profile, status, restored workspace, restored gear, and restored output affordances without yet claiming reload/session persistence runtime parity
- staged hosted workbench hosting/privacy posture for hosted route, Docker self-host, and analytics privacy copy, including `CHUMMER_ANALYTICS_PROVIDER=none`, making Rybbit is optional and metadata-only visible on the Chummer Online and proof-compatible workbench routes while naming character, owner, workspace, XML, and dossier-content exclusions without yet claiming Docker runtime, hosted route availability, or analytics delivery
- staged hosted workbench command-palette posture for keyboard-style hints, reload-safe workbench links, and same-origin help, making common new, open, Build Lab, gear, save/download, print, support, and help commands discoverable without yet claiming actual keyboard-event handling, portal help runtime, or command execution runtime parity
- staged hosted workbench density posture for Compact desktop, Comfortable review, and Mobile safe display options, making dense desktop ergonomics and comfortable and mobile-safe postures visible without yet claiming persisted preferences, runtime layout mutation, or screenshot parity
- staged hosted workbench workflow-ledger posture for visible startup, editing, output, recovery, portal, and boundary rows, making browser-client capability and desktop handoff boundaries visible in the client and explicitly stating that some desktop-only actions still open Chummer desktop without yet claiming runtime capability or browser parity
- staged hosted workbench file-intake posture for browser-safe open/import, Hero Lab import, XML editor, native file-system handoff, and support paths, keeping file intake visible in the Chummer Online and proof-compatible workbench routes without yet claiming file picker, import execution, XML mutation, or native file-system handoff parity
- staged hosted workbench rules/data posture for ruleset choice, sourcebook review, XML/custom data, translation tools, and help, keeping rules and references visible in the Chummer Online and proof-compatible workbench routes while not yet claiming ruleset mutation, sourcebook runtime, XML mutation, localization runtime parity, or portal help runtime parity
- staged hosted workbench settings posture for global settings, character settings, ruleset choice, update status, support handoff, and help, keeping setup changes reachable from the Chummer Online and proof-compatible workbench routes while not yet claiming persisted preference mutation, runtime settings parity, or portal help runtime parity
- staged hosted workbench diagnostics posture for runtime inspector, About, health, status, preview tools, and help, keeping diagnostics reachable from the Chummer Online and proof-compatible workbench routes while not yet claiming runtime health, build validity, diagnostics execution parity, or portal help runtime parity
- staged hosted workbench connected-runtime posture for play, session, coach, assistant, and status links, keeping optional live lanes reachable from the Chummer Online and proof-compatible workbench routes while not yet claiming connected-runtime execution, signed owner forwarding, or downstream service health
- staged hosted workbench accessibility posture for keyboard order, dialog fit, readable density, reduced motion, and help links, keeping access affordances visible on the Chummer Online and proof-compatible workbench routes while not yet claiming screen-reader, keyboard-event, screenshot, or browser accessibility validation
- staged hosted workbench section-rail posture for profile, build, skills, gear, combat, magic, matrix, contacts, and career shortcuts, keeping sheet-scale navigation visible on the Chummer Online and proof-compatible workbench routes while not yet claiming section rendering or browser execution parity
- staged hosted workbench desktop install handoff posture for downloads, update channel, status, account, self-host notes, help, and support, keeping browser-to-desktop continuity visible on the Chummer Online and proof-compatible workbench routes while the native installer progress chrome uses the same slate/amber/mint visual family and native installer high-contrast system-color fallback, without yet claiming installer download, portal help runtime, or Docker runtime parity
- staged hosted workbench menu-bar posture for file, build, view, character, tools, and same-origin help entry points, keeping familiar menu affordances visible on the Chummer Online and proof-compatible workbench routes while not yet claiming keyboard accelerator, portal help runtime, or menu-command execution parity
- staged hosted workbench workspace-tabs posture for active runner, build lab, print/export, and recent import lanes, keeping runner task tabs visible on the Chummer Online and proof-compatible workbench routes while not yet claiming multi-document state or tab persistence parity
- staged hosted workbench status-bar posture for save, rules, validation, session, privacy, help, and support state cues, keeping current-state affordances visible on the Chummer Online and proof-compatible workbench routes while not yet claiming save execution, validation execution, analytics delivery, portal help runtime, or session runtime parity
- staged hosted workbench inspector-rail posture for summary, build checks, inventory, notes, and sources, keeping side-context affordances visible on the Chummer Online and proof-compatible workbench routes while not yet claiming live inspector state or split-pane persistence parity
- staged hosted workbench dialog-stack posture for active dialog, committed result, retry, back-to-sheet, help, and support continuations, keeping modal and result continuations visible on the Chummer Online and proof-compatible workbench routes while not yet claiming modal execution, portal help runtime, or committed-action runtime parity
- staged hosted workbench context-actions posture for add, edit, remove, duplicate, source lookup, help, and recover actions, keeping selection-style actions visible without hidden right-click dependency while not yet claiming right-click menu, portal help runtime, or selection-state runtime parity
- staged hosted workbench search/filter posture for roster, gear, skills, qualities, sources, help, and clear filter lanes, keeping dense-list filtering visible from the Chummer Online chrome while not yet claiming live search indexing, portal help runtime, or filter execution parity
- staged hosted workbench layout-presets posture for dense sheet, split review, output, mobile safe, focus pane, and help modes, keeping browser-safe layout choices visible on the Chummer Online and proof-compatible workbench routes while not yet claiming pane resizing, portal help runtime, or persisted layout parity
- staged hosted workbench activity-feed posture for save event, validation warning, output event, hosted status, help, and support escape entries, keeping recent activity and recovery cues visible on the Chummer Online and proof-compatible workbench routes while not yet claiming live event logging, portal help runtime, or toast delivery parity
- staged hosted workbench keyboard-shortcuts posture for command help, save/output, section jump, density toggle, help, and support escape affordances, keeping power-user keyboard help visible on the Chummer Online and proof-compatible workbench routes while not yet claiming key-event handling, portal help runtime, or accelerator execution parity
- staged hosted workbench resource-meters posture for karma, nuyen, essence, limits, wounds, and lifestyle context, keeping core character totals visible on the Chummer Online and proof-compatible workbench routes while not yet claiming live character-total calculation parity
- staged hosted workbench tree-tools posture for expand, collapse, sort, reorder, pin, help, and selection tools, keeping dense tree/list affordances visible on the Chummer Online and proof-compatible workbench routes while not yet claiming tree virtualization, portal help runtime, or list mutation parity
- staged hosted workbench save-session posture for save, Save As, autosave, dirty state, recovery, help, and export lifecycle actions, keeping desktop-like session state visible on the Chummer Online and proof-compatible workbench routes while not yet claiming persisted browser mutation, portal help runtime, or file-write parity
- staged hosted workbench output-handoff posture for PDF packet, print sheet, HTML summary, share link, audit queue, help, and download bundle actions, keeping desktop-like output generation visible on the Chummer Online and proof-compatible workbench routes while not yet claiming print, PDF, share, portal help runtime, or download execution parity
- staged hosted workbench validation-queue posture for rule issues, missing fields, cost checks, availability limits, build gate, help, and fix-next navigation, keeping desktop-like build readiness visible on the Chummer Online and proof-compatible workbench routes while not yet claiming rules-engine execution, portal help runtime, or validation-result parity
- staged hosted workbench history-undo posture for undo, redo, snapshot, compare, restore, help, and conflict review actions, keeping desktop-like recovery context visible on the Chummer Online and proof-compatible workbench routes while not yet claiming actual rollback, diff, portal help runtime, or conflict-resolution parity
- staged hosted workbench sync-presence posture for connection, offline, local cache, sync queue, presence, help, and handoff cues, keeping hosted and Docker self-host session state visible on the Chummer Online and proof-compatible workbench routes while not yet claiming network sync, offline-cache, portal help runtime, or multi-user presence parity
- staged hosted workbench data-packs posture for sourcebooks, errata, custom data, update packs, validation scope, help, and data-folder context, keeping desktop-like rules/data management visible on the Chummer Online and proof-compatible workbench routes while not yet claiming live sourcebook loading, custom data import, data-update, or portal help runtime parity
- staged hosted workbench character-library posture for open, recent, pin, clone, archive, import, and help actions, keeping desktop-like character library management visible on the Chummer Online and proof-compatible workbench routes while not yet claiming file-open, library persistence, clone, archive, import, or portal help runtime parity
- staged hosted workbench campaign-session posture for roster, GM review, session notes, rewards, table share, run handoff, and help actions, keeping live-table workflow visible on the Chummer Online and proof-compatible workbench routes while not yet claiming campaign persistence, GM approval, reward mutation, table-share, or portal help runtime parity
- staged hosted workbench observability-privacy posture for consent, Rybbit status, route events, error traces, privacy log, self-host telemetry toggle, and help actions, keeping Chummer Run analytics posture visible on the Chummer Online and proof-compatible workbench routes while not yet claiming Rybbit deployment, event delivery, consent persistence, telemetry runtime, or portal help runtime parity
- staged hosted workbench first-run posture for new runner, desktop import, sample runner, restore session, self-host setup, docs, and help actions, keeping web onboarding visible on the Chummer Online and proof-compatible workbench routes while not yet claiming setup persistence, migration, import, Docker installer execution, or portal help runtime parity
- staged hosted workbench PWA-install posture for install prompt, offline cache, update available, browser permissions, release channel, reset cache, and help actions, keeping desktop-like web app install/update posture visible on the Chummer Online and proof-compatible workbench routes while not yet claiming service-worker, install prompt, cache update, browser permission, or portal help runtime parity
- staged hosted workbench Docker-operator posture for container health, env check, volume mounts, backup, image update, support bundle, and help actions, keeping Docker self-host operations visible on the Chummer Online and proof-compatible workbench routes while not yet claiming live container inspection, env validation, backup, image update, log-bundle, or portal help runtime parity
- staged hosted workbench security-access posture for sign-in, workspace lock, roles, session expiry, key rotation, access audit, and help actions, keeping hosted access-control posture visible on the Chummer Online and proof-compatible workbench routes while not yet claiming authentication, RBAC, session expiry, key rotation, audit-log, or portal help runtime parity
- staged hosted workbench notifications-jobs posture for job queue, retry, dismiss, settings, history, support, and help actions, keeping async save/export/sync/import work visible on the Chummer Online and proof-compatible workbench routes while not yet claiming toast delivery, queue execution, retry, background-worker, or portal help runtime parity
- staged hosted workbench touch-mobile posture for touch mode, zoom, panel dock, compact actions, keyboard-safe layout, pointer help, and help actions, keeping phone, tablet, and trackpad ergonomics visible on the Chummer Online and proof-compatible workbench routes while not yet claiming touch gesture, viewport, virtual-keyboard, mobile browser, or portal help runtime parity
- staged hosted workbench navigation-deeplink posture for breadcrumbs, URL state, back/forward, copy route, tab restore, shared anchor, and help actions, keeping browser navigation and shareable workspace context visible on the Chummer Online and proof-compatible workbench routes while not yet claiming router-state, browser-history, route-copy, deep-link restore, or portal help runtime parity
- staged hosted workbench inline-editing posture for dirty fields, numeric steppers, commit, revert, formula preview, bulk apply, and help actions, keeping dense controlled editing visible on the Chummer Online and proof-compatible workbench routes while not yet claiming field mutation, edit persistence, formula evaluation, bulk mutation, or portal help runtime parity
- staged hosted workbench performance-virtualization posture for lazy sections, virtual lists, render budget, memory posture, degraded mode, profiler, and help actions, keeping large-sheet browser scaling posture visible on the Chummer Online and proof-compatible workbench routes while not yet claiming virtualized rendering, lazy loading, memory control, profiler, or portal help runtime parity
- staged hosted workbench print-layout posture for sheet template, paper size, theme, sections, preview, export profile, and help actions, keeping desktop-like sheet output profile control visible on the Chummer Online and proof-compatible workbench routes while not yet claiming print CSS, PDF rendering, paper layout, export-profile, or portal help runtime parity
- staged hosted workbench portrait-attachments posture for portrait, token art, notes, attachments, import media, cleanup, and help actions, keeping character media and reference attachments visible on the Chummer Online and proof-compatible workbench routes while not yet claiming file upload, storage persistence, thumbnail generation, attachment cleanup, or portal help runtime parity
- staged hosted workbench windowing-panes posture for split view, pop-out, pinned inspector, focus mode, second screen, restore layout, and help actions, keeping desktop-like pane management visible on the Chummer Online and proof-compatible workbench routes while not yet claiming multi-window, focus handling, second-screen routing, layout persistence, or portal help runtime parity
- staged hosted workbench GM-screen export posture for Cards, player view, initiative, conditions, notes, export bundle, and help, keeping table-ready browser handoff visible on the Chummer Online and proof-compatible workbench routes while not yet claiming GM-screen rendering, player-view routing, initiative sync, export-bundle parity, or portal help runtime parity
- staged hosted workbench calculation-provenance posture for derived breakdown, modifier stack, rule source, stale values, manual override, dependency path, and help actions, keeping derived-stat explainability visible on the Chummer Online and proof-compatible workbench routes while not yet claiming calculation-engine, dependency tracing, recalculation, override, or portal help runtime parity
- staged hosted workbench lifecycle-calendar posture for downtime, lifestyle upkeep, subscriptions, reminders, recurring costs, next session, and help actions, keeping between-session upkeep visible on the Chummer Online and proof-compatible workbench routes while not yet claiming scheduling, reminder delivery, recurring cost mutation, session calendar parity, or portal help runtime parity
- staged hosted workbench progression-ledger posture for karma spend, nuyen ledger, purchase queue, reputation, carryover, audit trail, and help actions, keeping advancement accounting visible on the Chummer Online and proof-compatible workbench routes while not yet claiming ledger mutation, purchase execution, reputation mutation, accounting parity, or portal help runtime parity
- staged hosted workbench import/reconcile posture for file selection, parse summary, rules mapping, custom data, conflict review, final acceptance, and help, keeping existing-runner migration visible on the Chummer Online and proof-compatible workbench routes while not yet claiming file upload, XML parsing, migration, conflict resolution, import persistence parity, or portal help runtime parity
- staged hosted workbench compare/merge posture for diff view, conflict choice, source trace, dry run, apply, rollback, and help actions, keeping merge review visible on the Chummer Online and proof-compatible workbench routes while not yet claiming diff execution, conflict resolution, source tracing, merge application, rollback parity, or portal help runtime parity
- staged hosted workbench restore/checkpoint posture for autosave, named checkpoint, backup, preview, rollback, retention, and help actions, keeping recovery visible on the Chummer Online and proof-compatible workbench routes while not yet claiming snapshot persistence, restore execution, backup generation, rollback mutation, retention policy parity, or portal help runtime parity
- staged hosted workbench offline/cache posture for cache status, queued edits, reconnect, local export, stale data, sync health, and help actions, keeping network-interruption continuity visible on the Chummer Online and proof-compatible workbench routes while not yet claiming service-worker caching, queued mutation persistence, reconnect execution, offline export generation, sync reconciliation parity, or portal help runtime parity
- staged hosted workbench session-locking posture for lock status, owner handoff, read-only fallback, stale recovery, conflict owner, takeover, and help actions, keeping edit ownership visible on the Chummer Online and proof-compatible workbench routes while not yet claiming lock acquisition, takeover mutation, cross-tab arbitration, stale lock cleanup, conflict persistence parity, or portal help runtime parity
- staged hosted workbench share/export privacy posture for redaction, scope, expiry, revocation, history, local-only export, and help actions, keeping private handoff visible on the Chummer Online and proof-compatible workbench routes while not yet claiming share-token issuance, redaction execution, revocation persistence, history storage, export-policy enforcement parity, or portal help runtime parity
- staged hosted workbench table-handoff posture for GM packet, initiative card, condition tracker, public handout, private notes, table export, and help actions, keeping session-table output visible on the Chummer Online and proof-compatible workbench routes while not yet claiming packet generation, live GM sharing, condition synchronization, handout publication, table export persistence parity, or portal help runtime parity
- staged hosted workbench rules-citation posture for source packet, citation scope, errata note, table summary, dispute trail, audit export, and help actions, keeping rule explanation visible on the Chummer Online and proof-compatible workbench routes while not yet claiming citation generation, source lookup, errata resolution, dispute persistence, audit export generation parity, or portal help runtime parity
- staged hosted workbench localization/terminology posture for language, units, dates, currency, table terms, source-title affordances, and help, keeping table-specific wording visible on the Chummer Online and proof-compatible workbench routes while not yet claiming translation coverage, formatter execution, localized source-title lookup, persisted locale settings, export localization parity, or portal help runtime parity
- staged hosted workbench help/recovery guidance posture for context help, shortcut hints, error explanations, recovery suggestions, same-origin portal help, docs links, and support handoff affordances, keeping guided browser recovery visible on the Chummer Online and proof-compatible workbench routes while not yet claiming contextual help resolution, keyboard shortcut execution, portal help runtime, support-ticket creation, docs search, or error remediation parity
- character roster custom hierarchy posture for custom roster directories and a hierarchy of the user's choosing, user-created virtual folders, nested groups, drag/drop move intent, explicit Move Runner/Directory actions, watched-file links that do not move disk files by default, safe directory deletion that moves runner/link items to Inbox first and reparents child directories, non-destructive metadata mutation that stages create/rename/delete/move/reorder changes before any filesystem mutation, editable Directory Name plus option-backed Source Directory and Target Directory fields with a custom-only source picker, system library buckets as filing drop targets but not draggable source directories, semantic bucket/folder/runner/watched-file row styling, styled visible hierarchy status counts with pending-move disclosure, polished amber/mint/blue hierarchy treatment, pending organization status, roster-first public home hero route pills, reduced-motion-safe reveal, high-contrast affordances, mobile-softened grid density, and hidden Source Item carriage for web workflow control, a Blazor drag/drop event bridge that fills `rosterTargetFolder`, carries `rosterSourceFolder` for directory drops, clears stale `rosterSourceFolder` for runner/link drops, carries `rosterSourceItem` for dragged runner/link rows, preserves full runner labels while stripping visual row suffixes, invokes the same virtual move/reorder actions, and blocks directory cycles when a parent is dropped into its own descendant, Blazor dialog markup that exposes roster hierarchy rows with virtual-folder data attributes including `data-roster-line-kind` and `data-roster-folder-scope`, selected-row state, drag-handle styling, row title affordances with row-level keyboard help, aria labels, aria-describedby linkage to dialog-scoped baseline keyboard hint notes and live source feedback hosts, labelled tree containers, vertical tree orientation, tree/treeitem roles, aria-level depth metadata, aria-selected state for selected/source rows, aria-expanded state for folder rows, aria-keyshortcuts for actionable rows, nullable optional ARIA emission for presentation rows, focusability, visible keyboard drag-source state, separate mouse and keyboard source state with mode-specific source badges and live source status instructions, mouse drag-end cleanup for stale mouse sources, atomic live source feedback, visible keyboard operation guidance, hierarchy-status keyboard shortcut summary, scoped Enter/Space/Escape keyboard handling that does not globally suppress focus navigation, and keyboard handling before full hosted drag execution is claimed, a shared `RosterHierarchyState` contract for folders, item links, move intent, safe filesystem-confirmation policy, shared `RosterHierarchyStateJson` normalization and validation for Avalonia/web metadata reuse, `RosterHierarchyJson` preference staging for non-destructive layout metadata, safe staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, and hidden settings carriage for later owner-scoped persistence
- source-level legacy UI-control coverage guard for all known `LegacyUiControlCatalog` IDs, mapping each control into hosted execution baseline evidence or a staged source-alignment family before any future browser proof refresh claims breadth
- aggregate source-staged proof set for the staged browser workflow families, deliberately separate from hosted execution proof and Docker self-host proof so staged breadth cannot be mistaken for runtime parity
- runtime refresh plan receipts are source-plan only and must stay out of release-readiness aggregation; they guide proof refresh order but do not replace hosted execution proof or Docker self-host proof
- hosted contact-connection posture for restored `tab-contacts&control=contact_connection`, with visible selected-contact summary context and compact edit controls through the canonical proof-compatible route
- hosted vehicle-edit posture for restored `tab-gear&control=vehicle_edit`, with visible vehicle details, live summary, and garage context through the canonical proof-compatible route
- hosted vehicle-delete posture for restored `tab-gear&control=vehicle_delete`, with visible impact and recovery context through the canonical proof-compatible route
- hosted contact-delete posture for restored `tab-contacts&control=contact_remove`, with visible roster impact and recovery context through the canonical proof-compatible route
- hosted contact-edit posture for restored `tab-contacts&control=contact_edit`, with visible selected-contact details and in-place edit context through the canonical proof-compatible route
- staged hosted career/support posture for restored `tab-calendar`, `create_entry`, `dialog_action=add`, `edit_entry`, `dialog_action=apply`, `delete_entry`, `dialog_action=delete`, `open_notes`, `dialog_action=save`, `move_up`, and `move_down`, with the restored-continuations and restored-actions cards exposing career/support utility lanes and the next hosted execution refresh set to assert section landing, compact add/edit list-detail editors, remove/recovery posture, runner notes editing, classic list reorder utilities, and visible add/edit/delete/notes-save commit results
- staged hosted identity/SIN/license posture for restored `tab-info` controls `identity_license_add`, `identity_license_edit`, and `identity_license_delete`, with the Chummer Online and proof-compatible workbench cards exposing add/edit/remove affordances and the next hosted execution refresh set to assert compact identity, source, rating, lifestyle-cover, and recovery context
- seeded runner continuity routes for Build Lab, Rules, Contacts, and Complex Forms
- seeded browser result-state routes for `save_character`, `save_character_as`, `print_character`, and `export_character` plus `dialog_action=download`, proving browser-visible save/download/print/export outcomes instead of only dialog entry posture
- separate self-host receipt proof for portal-backed `/blazor/app`, proof-compatible `/blazor/workbench`, and `/blazor/preview` routes under Docker, with the next refresh staged to include the same career/support section, add/edit/delete action, runner-notes action, move up/down list utilities, and committed-result lane as the hosted proof runner
- separate hosted route-entry proof for the `https://chummer.run/blazor/` public edge, including route shapes for restored result continuations, action continuations, committed action continuations, and staged career/support section/action routes
- a dedicated hosted execution-proof contract, runner scaffold, verifier, and published passing receipt so `chummer.run` browser workflow execution is promoted with its own evidence instead of being conflated with self-host proof, including hosted startup-command execution for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- optional browser analytics posture proof for hosted `chummer.run` Rybbit instrumentation and self-host default-off behavior, published as `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`, with `/blazor/app` classified as `chummer_app`, route/workflow metadata only, explicit no session replay and no autocapture posture, and no character, owner, workspace, document, XML, payload, hash, or dossier content capture, plus non-secret `/health` policy fields for self-host default, hosted edge posture, sensitive-data policy, session replay policy, and autocapture policy
- connected-runtime posture proof for optional session, coach, and assistant portal forwarding, published as `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`, proving the signed portal-owner boundary and visible configured/off browser posture without claiming full downstream connected-runtime workflow parity

The remaining gap is breadth, not direction: the promoted hosted browser client now has clean public `/app` route-entry posture, hosted `/blazor/app` path posture, and canonical `/blazor/workbench` hosted execution proof, but more Avalonia-equivalent workflows still need browser-specific proof before the web client can claim full desktop equivalence.

## Current Truth

This is a target state, not a completed claim.

Current release language may still identify Avalonia as the native flagship desktop head and Blazor Desktop as a fallback desktop package. That does not conflict with this goal. The new goal is to raise the browser-hosted `Chummer.Blazor` client to a first-class web client with its own proof, hosted route, and Docker self-hosting story.

## Target Product Contract

The browser client target is not a preview page and not a reduced companion portal. It is the web-delivered Chummer client head:

- `https://chummer.run/app` is the clean hosted public Chummer Online route, `https://chummer.run/blazor/app` is the hosted Blazor app path, `https://chummer.run/blazor/home` is the product/orientation page, and all are backed by the same promoted client family as `https://chummer.run/blazor/workbench`.
- Docker self-hosting must expose the same portal-backed `/app`, `/blazor/app`, `/blazor/home`, and `/blazor/workbench` workflow shape, with local operator configuration instead of hosted defaults.
- Avalonia remains the native desktop head, but browser parity means users can perform the same practical Chummer workflow in the web client unless a browser-specific boundary is explicitly called out.
- Rybbit may be enabled on hosted `chummer.run` for sanitized product telemetry; self-host Docker installs stay default-off and operator-controlled.
- Analytics must stay metadata-only: route family, command, tab, control, dialog action, and coarse workspace/fixture presence are allowed, while character data, owner identifiers, workspace identifiers, XML, files, payloads, hashes, and dossier content are excluded.
- Release language must distinguish source-staged affordance breadth from hosted execution proof, Docker self-host proof, and parity-ledger evidence.

The immediate Chummer6 design direction is therefore:

- keep pushing `/app` as the clean user-facing product route while preserving `/blazor/app` as the hosted app path and `/blazor/workbench` for explicit workbench/proof compatibility
- keep `/blazor/preview` as proof/supporting evidence, not the primary product promise
- keep expanding browser workflow-family breadth beyond the now-passing hosted execution baseline
- only upgrade release language when hosted browser execution proof, self-host proof, and parity ledger evidence all support the claim

## Extended Goal Scope

The browser-client goal now explicitly includes the remaining proof and parity work needed before Chummer Online can be treated as a polished web-delivered desktop client:

- regenerate and republish affected `.codex-studio/published/*.generated.json` receipts after source-contract route, marker, Rybbit, portal, and proof-language changes land
- execute the relevant source materializers, proof verifiers, browser probes, and Docker self-host lanes before promoting any release language from source-staged posture to runtime proof
- keep `/app` as the clean public Chummer Online entry, keep `/blazor/app` as the hosted Blazor implementation path, and keep `/blazor/workbench` as the explicit proof-compatible execution lane until receipts and verifiers are deliberately migrated
- refresh hosted public-edge route-entry proof, hosted execution proof, Docker self-host proof, and aggregate browser-lane proof as separate receipts; none may substitute for the others
- expand Avalonia-equivalent workflow breadth beyond visible staged affordances, especially mutation, persistence, import/export, rules validation, help/support runtime, installer handoff, and connected-runtime workflows
- keep Runner Intelligence and character-statistics calculations reusable from Avalonia and Blazor through shared `Chummer.Presentation` seams, including percentile benchmarks, Increase Initiative Force 6 what-if modelling, inventory synergy, drain/stun risk, and privacy-safe cohort posture
- keep character roster hierarchy work non-destructive by default, with custom folders, nested hierarchy, drag/drop intent, keyboard operation, system library buckets as drop targets only, staged metadata, cycle prevention, and shared Avalonia/web serialization
- keep hosted Rybbit metadata-only and self-host default-off, with no session replay, no autocapture, and no character, owner, workspace, XML, file, payload, hash, or dossier-content capture
- keep the slate/amber/mint/blue Chummer Online theme consistent across Blazor, portal recovery pages, downloads/install handoff, docs explorer, and native installer progress surfaces
- keep source-staged receipts, source-plan receipts, source-calculation receipts, hosted runtime receipts, Docker self-host receipts, and release-readiness aggregation visibly separated so staged breadth cannot be mistaken for browser parity

### Release Evidence Boundary

The parity goal remains intentionally broad, but release language is narrower than the target state. The Blazor web client cannot be described as polished desktop-equivalent parity until `docs/WORKBENCH_RELEASE_SIGNOFF.md#extended-goal-release-blockers` and `docs/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.md#extended-goal-refresh-gates` are both satisfied by refreshed receipts.

Source-staged, source-plan, and source-calculation receipts are planning evidence only. They do not prove hosted route-entry, hosted execution, Docker self-host execution, aggregate browser-lane readiness, analytics runtime posture, connected-runtime behavior, installer/download behavior, durable roster hierarchy persistence, or Runner Intelligence runtime/statistical correctness.

The release claim must be backed by separate hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts. `/app` remains the clean public Chummer Online route, `/blazor/app` remains the hosted app path, and `/blazor/workbench` remains the proof-compatible execution lane until the proof verifiers are deliberately migrated together.
