# UI implementation scope

## Mission

`chummer6-ui` owns the workbench, browser, and desktop user experience for Chummer6.
It is the repo for builders, inspectors, compare views, explain UX, moderation/admin surfaces, and installable desktop delivery.

## Owns

* workbench/browser/desktop UX
* builders, inspectors, and compare flows
* explain and audit-facing UX on the workbench side
* moderation and admin surfaces that stay outside the live play shell
* desktop packaging, installer delivery, and workbench-side release polish

## Must not own

* the dedicated play/mobile shell
* offline session-ledger authority
* engine/runtime mechanics truth
* hosted orchestration or provider-secret ownership
* source-copied shared UI primitives that belong in `Chummer.Ui.Kit`

## Package boundary

`chummer6-ui` consumes shared packages. It does not recreate them locally.

Primary consumption boundary:

* `Chummer.Engine.Contracts`
* `Chummer.Ui.Kit`

## Boundary truth

Feature completion inside this repo was not enough to close the split milestone.
`B2` is now treated as complete because the repo body matches the stated boundary closely enough for release:

* legacy/helper/tooling roots stop dominating the tree
* shared visual chrome migrates into `chummer6-ui-kit`
* play-shell ownership remains fully outside this repo
* installer/release work stays workbench-scoped instead of reabsorbing unrelated ownership

## Current reality

The product direction and the release bar are now aligned.
Retained legacy roots are compatibility cargo, not hidden ownership claims.

That means:

* feature maturity and boundary purity now align closely enough to close both `B2` and the workbench share of `E0`
* shared visual chrome is package-owned and regression-guarded
* workbench release evidence is explicit in `docs/WORKBENCH_RELEASE_SIGNOFF.md`
* any retained legacy roots must stay explicitly documented as compatibility cargo
