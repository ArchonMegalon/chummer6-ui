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

- feature maturity is ahead of boundary purity
- the release lane now ships installer-capable desktop bundles, not just loose files
- legacy compatibility cargo is explicitly isolated in `docs/COMPATIBILITY_CARGO.md` instead of being treated as active boundary truth

## Go deeper

- `.codex-design/repo/IMPLEMENTATION_SCOPE.md`
- `.codex-design/review/REVIEW_CONTEXT.md`
- `docs/COMPATIBILITY_CARGO.md`

## Verification

Run:

```bash
bash scripts/ai/verify.sh
```
