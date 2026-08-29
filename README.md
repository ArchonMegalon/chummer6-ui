# chummer6-ui

Workbench, browser, and desktop UX for Chummer6.

Open it when you want to build a runner, understand the tradeoffs on the sheet, keep a table moving, or carry a character into the wider campaign layer without losing the rules underneath. The desktop is intentionally practical: dense where veteran users need speed, calmer where a new player needs a next step, and honest about what is live versus what is still a preview.

## What is here

This repository owns the desktop and browser workbench: the builder shell, inspectors, compare views, ALICE, Origin Dossier, download packaging, and the shared presentation seams that make Chummer6 feel like a real application instead of a collection of experiments.

The play/mobile shell, hosted campaign services, and release-channel authority live in adjacent Chummer repos. This repo can link to those surfaces, but it does not pretend to own their truth.

The product story should start with normal product areas. Workbench, ALICE, Origin Dossier, Ready for Tonight, Runner Passport, Knowledge Fabric, Table Pulse, and GM Cockpit are not a pile of speculative “Horizons”; they are the base shape a user should understand. Reserve Horizon language for the larger expansion bets that still need a clear future boundary.

## Ownership boundaries

The shipped play/mobile heads now live outside this repo in `chummer6-mobile`; the dedicated play/mobile shell is not owned here. after the `chummer-play` split, presentation ownership for session/coach flows is limited to shared UI-kit primitives consumed by `chummer-play` through `Chummer.Ui.Kit`. This repo's role in session/coach flows is limited to shared UI-kit primitives through `Chummer.Ui.Kit`, workbench-side coach sidecars and portal/proxy expectations explicit, and portal/proxy expectations for external `/session` and `/coach` hosts.

Hosted orchestration, rule pack publication, build kit registry, NPC vault, runtime locks, hub catalog, hub review, and protected publication surfaces stay behind their owner-backed service seams. Presentation code may consume those seams; it must not re-own hosted orchestration.

release-channel publication truth now lives downstream in `chummer6-hub-registry`; desktop heads can consume the canonical registry manifest for self-update when `CHUMMER_DESKTOP_UPDATE_MANIFEST` is configured.

Legacy head policy: `Chummer` and `Chummer.Web` are oracle/parity assets only. Net-new user-facing behavior belongs in the shared seam and active heads; legacy changes must be limited to regression-oracle maintenance, parity extraction, or compatibility verification.

Legacy hub policy: `ChummerHub` and `ChummerHub.Client` are archived compatibility assets only. They are not part of the active solution, public runtime, or future ChummerHub product path; all public-edge and hub work belongs behind `Chummer.Portal`.

## Start here

For a quick orientation, read `.codex-design/product/START_HERE.md`.

For the current release posture, read `docs/WORKBENCH_RELEASE_SIGNOFF.md`.

For the browser-hosted desktop-equivalent target, read `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`.

legacy compatibility cargo is explicitly isolated in `docs/COMPATIBILITY_CARGO.md` instead of being treated as active boundary truth.

For the larger campaign-layer showcase, read `docs/TABLE_PULSE_FLAGSHIP_SHOWCASE.md`.

## Today’s shape

The early-access desktop lane is strong enough to present as a focused workbench: Windows installer and portable outputs, macOS and Linux preview installers, startup-smoke coverage, and a clear split between active desktop work and legacy compatibility cargo.

Legacy `Chummer` and `Chummer.Web` code remains useful as parity and regression reference. New user-facing desktop behavior should land in the active workbench heads and shared presentation seams, not in legacy compatibility surfaces.

## Verification

Run repo-local restore/build/test flows through the package-plane helpers so shared contracts resolve through published feeds or an explicit compatibility tree, instead of ambient sibling-project auto-detection:

```bash
bash scripts/ai/restore.sh Chummer.Tests/Chummer.Tests.csproj -p:TargetFramework=net10.0
bash scripts/ai/build.sh Chummer.Blazor/Chummer.Blazor.csproj
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj -f net10.0 -p:TargetFramework=net10.0
bash scripts/ai/coverage.sh Chummer.Tests/Chummer.Tests.csproj
bash scripts/ai/test-matrix.sh Chummer.Tests/Chummer.Tests.csproj
bash scripts/ai/verify.sh
```

If you intentionally want the mounted sibling compatibility tree instead of the local package feed, pass `-p:ChummerUseLocalCompatibilityTree=true` explicitly.

The release-grade no-siblings lane is:

```bash
python3 scripts/ai/verify_fresh_checkout_package_plane.py \
  --receipt-output /absolute/new/path/fresh-package-plane.json
```

Package-authority recipe changes use a protected split-preseal transaction:

```text
sealed main -> recipe P -> marker-only Q -> lock-only seal S
```

P and Q may be published only with GitHub's **rebase merge** method. Squash
merging destroys the reviewed recipe/marker topology, and a merge commit is not
linear publication authority; either form is rejected by the exact current-main
receipt. The preseal receipt explicitly grants no package-consumer, release, or
publication claim. After Q is on `main`, produce the cold owner feed against its
exact published recipe, then create S as Q's sole child changing exactly
`config/package-plane.lock.json` and
`config/ui-owner-package-plane.lock.json`. The marker remains checked in and is
atomically refreshed for the next authority cycle rather than accumulated.

If a published Q cannot be sealed because its hosted controls reject S, one
bounded recovery cycle may supersede that still-unsealed Q. The recovery
verifier requires the original Q to remain its exact marker-only commit, the
new P to retain both canonical locks and that marker byte-for-byte, and the new
Q to refresh only the marker. Repeated supersession is rejected. Lock bytes
from the rejected S are never reused: the cold producer must regenerate them
against the newly published recovery Q before its exact two-lock S is proposed.

The composer executes the pinned Hub v3 package producer from the exact Hub owner commit, validates its lock and inventory, and imports the exact canonical Engine and Registry package bytes. Hub contracts are then packed with their checked-in project locks explicitly enforced. The remaining owner packages are built from the commits and versions pinned by `config/package-plane.lock.json`; every restore sees only the finite same-run feed, and that feed is rehashed after all builds and tests. Receipt contract v5 records the canonical producer, lock, inventory, package digests, and enforced Hub project-lock posture.

To retain a new exact 18-package owner cache after a Core/Hub reseal, use the
cold producer with the newly sealed Core public runtime bundle and the exact Hub
no-siblings receipt. Both inputs are mandatory and are re-inventoried after the
build; an existing owner cache cannot be supplied in the same transaction:

```bash
python3 scripts/ai/verify_fresh_checkout_package_plane.py \
  --produce-owner-package-cache-output /absolute/new/path/owner-cache \
  --cold-core-runtime-bundle /absolute/path/core-runtime-public-bundle.zip \
  --cold-hub-package-plane-receipt /absolute/path/HUB_NO_SIBLINGS_PACKAGE_PLANE.generated.json \
  --transition-from-sealed-preseal \
  --proposed-package-plane-lock-output /absolute/new/path/package-plane.lock.json \
  --proposed-ui-owner-lock-output /absolute/new/path/ui-owner-package-plane.lock.json \
  --receipt-output /absolute/new/path/owner-cache-production.json
```

The cold lane validates canonical ZIP metadata and every Core package/authority
digest, rebuilds Hub and legacy owner packages from their locked commits, builds
the UI-owner packages, and reimports the complete cache through the normal
consumer validator before an atomic no-replace retention. It never copies or
updates a stale cache and does not authorize package publication. The explicit
transition flag is accepted only on the cold P/Q lane: it proves the previous
two committed lock bytes through the marker, derives the next Core/Hub upstream
authority in memory, and atomically retains the exact two proposed S lock files
under one trusted external parent. A failed output or final receipt rolls back
both lock files and the produced cache; no partial seal proposal is authoritative.
The producer performs one joint point-in-time verification of the receipt,
both proposed locks, and the complete cache while their validated descriptors
remain open. This is jointly verified generation, not a claim that files remain
immutable against the same operating-system user after the producer returns.
The lock-only S commit and the downstream Fresh consumer must rehash and
validate the exact lock bytes and self-bindings before granting any authority.
For a warm
UI-only rebuild against an already exact 16-package upstream cache, retain the
existing `--owner-package-cache /absolute/path` mode instead.

`scripts/ai/test-matrix.sh` is the host-aware entrypoint for the current test matrix:
- always runs the Linux `net10.0` suite
- always restores and builds the `net10.0-windows` target
- only executes the `net10.0-windows` test binary when `Microsoft.WindowsDesktop.App 10.x` is available on the host
- on macOS hosts, also builds the Avalonia and Blazor desktop heads

For final native-host certification use:

```bash
bash scripts/ai/test-native-host-matrix.sh Chummer.Tests/Chummer.Tests.csproj
```

That wrapper is intentionally stricter:
- on Windows, it requires real Windows desktop test execution
- on macOS, it runs the host-aware matrix and desktop-head builds

`scripts/ai/coverage.sh` collects Linux `net10.0` coverage with the `XPlat Code Coverage` collector and writes a Cobertura summary JSON under `.artifacts/coverage/summary.json`.
