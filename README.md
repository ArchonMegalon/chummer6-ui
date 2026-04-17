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

Legacy head policy: `Chummer` and `Chummer.Web` are oracle/parity assets only.
Net-new user-facing behavior belongs in the shared seam and active heads; legacy changes must be limited to regression-oracle maintenance, parity extraction, or compatibility verification.
Legacy hub policy: `ChummerHub` and `ChummerHub.Client` are archived compatibility assets only.
They are not part of the active solution, public runtime, or future ChummerHub product path; all public-edge and hub work belongs behind `Chummer.Portal`.

## Current mission

The work here is purification:

- keep only workbench/browser/desktop ownership
- consume shared packages instead of rebuilding them locally
- finish accessibility and deployment signoff without pretending the split is already done
- keep workbench-side coach sidecars and portal/proxy expectations explicit

Current honesty clause:

- the workbench/browser/desktop lane is ready enough to ship the current early-access desktop scope
- the release lane now emits Windows installer and portable `.exe` outputs plus portable bundles, alongside macOS `.dmg` and Linux `.deb` preview installers, instead of loose files
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

Run repo-local restore/build/test flows through the package-plane helpers so shared contracts resolve through published feeds or an explicit compatibility tree, instead of ambient sibling-project auto-detection:

```bash
bash scripts/ai/restore.sh Chummer.Tests/Chummer.Tests.csproj -p:TargetFramework=net10.0
bash scripts/ai/build.sh Chummer.Blazor/Chummer.Blazor.csproj
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj -f net10.0 -p:TargetFramework=net10.0
bash scripts/ai/verify.sh
```

If you intentionally want the mounted sibling compatibility tree instead of the local package feed, pass `-p:ChummerUseLocalCompatibilityTree=true` explicitly.
