# chummer6-ui

Workbench, browser, and desktop UX for Chummer6.

## What this repo is

`chummer6-ui` owns the big-screen side of Chummer:

- builders, inspectors, and compare views
- moderation and admin UX
- browser and desktop workbench flows
- shared presentation seams that stay on the workbench side

## What this repo is not

This repo does not own:

- the dedicated play/mobile shell
- hosted orchestration
- render-only media execution
- copied shared contracts

The shipped play/mobile heads now live outside this repo in `chummer6-mobile`, and shared UI-kit primitives belong in `Chummer.Ui.Kit`.

## Current mission

The work here is purification:

- keep only workbench/browser/desktop ownership
- consume shared packages instead of rebuilding them locally
- finish accessibility and deployment signoff without pretending the split is already done
- keep workbench-side coach sidecars and portal/proxy expectations explicit

Current honesty clause:

- the workbench/browser/desktop lane is ready enough to ship the current early-access desktop scope
- the release lane now emits Windows `.exe`, macOS `.dmg`, and Linux `.deb` preview installers and desktop bundles, not just loose files
- release-channel publication truth now lives downstream in `chummer6-hub-registry`; this repo emits the desktop bundle and installer recipe, not the promoted channel head
- desktop heads can consume the canonical registry manifest for self-update when `CHUMMER_DESKTOP_UPDATE_MANIFEST` is configured
- every packaged desktop head now has a startup-smoke gate and emits a bounded release-regression packet before promotion if the smoke start fails
- explicit release evidence lives in `docs/WORKBENCH_RELEASE_SIGNOFF.md`
- legacy compatibility cargo is explicitly isolated in `docs/COMPATIBILITY_CARGO.md` instead of being treated as active boundary truth
- after the `chummer-play` split, presentation ownership for session/coach flows is limited to shared UI-kit primitives consumed by `chummer-play` through `Chummer.Ui.Kit`, workbench-side coach sidecars, and portal/proxy expectations for external `/session` and `/coach` hosts

## Go deeper

Legacy root `chummer-presentation.design*.md` files remain only as compatibility aliases. Use `.codex-design/*` as the live canon.

- `.codex-design/repo/IMPLEMENTATION_SCOPE.md`
- `.codex-design/review/REVIEW_CONTEXT.md`
- `docs/DESKTOP_RELEASE_PIPELINE.md`
- `docs/WORKBENCH_RELEASE_SIGNOFF.md`
- `docs/COMPATIBILITY_CARGO.md`

## Verification

Run:

```bash
bash scripts/ai/verify.sh
```
