# Presentation implementation scope

## Mission

`chummer-presentation` owns desktop/browser workbench UX, large-screen operator flows, admin/moderation surfaces, and package consumption of the shared UI kit.

## Owns

* builder/workbench UX
* compare and inspect flows
* Explain Everywhere workbench UX
* publication/admin/moderation UX
* browser/desktop shell composition
* consumption of shared UI and engine contracts

## Must not own

* dedicated mobile/session play shell
* rules math
* offline play ledger persistence
* media job execution
* registry persistence internals
* provider secrets

## Current extraction focus

* align all local docs to the reality that `chummer-play` owns shipped `/session` and `/coach`
* consume `Chummer.Ui.Kit` as a package-only dependency
* keep workbench-only seams separate from play-only seams
* stop carrying stale or ambiguous play-host assumptions

## Milestone spine

* P0 ownership correction
* P1 package-only UI consumption
* P2 workbench shell
* P3 explain UX
* P4 Build Lab UX
* P5 publish/admin/moderation UX
* P6 platform parity
* P7 accessibility/performance
* P8 finished workbench

## Worker rule

If the job is about building, browsing, comparing, moderating, or explaining on a workbench/browser/desktop surface, it belongs here.
If the job is about live play shell behavior, it belongs in `chummer-play`.


## External integration note

`chummer-presentation` may render upstream projections, previews, docs/help links, and provider-assisted artifact references.

It must not own:

* vendor credentials
* direct provider SDK integrations
* direct third-party API orchestration
