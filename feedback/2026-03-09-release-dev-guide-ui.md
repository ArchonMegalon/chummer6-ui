# Release Dev Guide Split: presentation

Source: 2026-03-09 Project Chummer release dev guide. This is the `chummer-presentation` slice only.

## Your authority

`chummer-presentation` must own:

- desktop/web/mobile rendering
- localization and explain rendering
- local-first UI state and offline cache behavior
- player play shell and GM ops surfaces
- browse/build/runtime-inspector screens
- artifact viewers and review flows

It must not own rules math or duplicated shared wire contracts.

## Immediate corrections

1. Delete duplicated shared contract source and consume package versions from core/run-services.
2. Stop carrying drifted explain DTOs; render the canonical engine envelope instead.
3. Keep all rules logic out of UI code, including fallback math and “temporary” client-side recomputation.
4. Treat queue completion as queue completion only; do not let UI status language imply product signoff.

## What to finish next

- Explain Everywhere UI using key/params from engine-authored contracts
- virtualized browse/catalog surfaces for large datasets
- Build Lab UI that reads projections only and performs no scoring in UI
- Runtime Inspector for runtime fingerprint, pack/profile, capability bindings, and diff previews
- player play shell for tablet/web/mobile
- GM Ops Board as tactical cards, not chat
- artifact viewer suite for portraits, dossiers, news, recap, and route-video review

## Session-shell requirements

- local event log first
- queued sync and resume behavior
- explicit stale/invalidation state
- runtime bundle health visibility
- no forced always-online interaction model

## Test and CI guidance

- keep rendering, viewmodel, virtualization, shell bootstrap, PWA/cache, and accessibility checks here
- remove service/business-logic tests that belong in run-services
- add browser/runtime compliance gates for COOP/COEP, MIME, deep-link refresh, and service-worker behavior
- fail loudly on missing localization keys

## Definition of done for this repo

Presentation is not done until:

- no rules math exists in UI code
- shared contracts are package-consumed, not source-copied
- explain/build/browse/play/GM/artifact surfaces are complete
- local-first session behavior converges correctly
- deployment and accessibility checks are green across supported heads
