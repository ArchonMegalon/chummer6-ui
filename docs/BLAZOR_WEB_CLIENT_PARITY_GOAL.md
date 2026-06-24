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
- connected-runtime posture proof for optional session, coach, and AI portal forwarding, published as `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`, proving the signed portal-owner boundary and visible configured/off browser posture without claiming full downstream connected-runtime workflow parity

The remaining gap is breadth, not direction: the promoted hosted `/blazor/workbench` lane now has both separate route-entry proof and separate hosted execution proof, but more Avalonia-equivalent workflows still need browser-specific proof before the web client can claim full desktop equivalence.

## Current Truth

This is a target state, not a completed claim.

Current release language may still identify Avalonia as the native flagship desktop head and Blazor Desktop as a fallback desktop package. That does not conflict with this goal. The new goal is to raise the browser-hosted `Chummer.Blazor` client to a first-class web client with its own proof, hosted route, and Docker self-hosting story.

The immediate Chummer6 design direction is therefore:

- keep pushing `/blazor/workbench` as the product route
- keep `/blazor/preview` as proof/supporting evidence, not the primary product promise
- keep expanding browser workflow-family breadth beyond the now-passing hosted execution baseline
- only upgrade release language when hosted browser execution proof, self-host proof, and parity ledger evidence all support the claim
