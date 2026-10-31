# WIN-004 — Windows installer, update channel, recovery: native verification evidence

**Task:** WIN-004 (produce the existing Windows installer/update artifact locally and verify native installation/start/update/relaunch/recovery)
**Date:** 2026-10-31
**Worker:** friday-windows
**Branch:** `windows/WIN-004-installer-update-recovery` (from `2fec7c7ac3a7081d1b5f7a4a2a016045494f2ed0`, the task's assigned base)
**Source identity:** UI head `2fec7c7ac3a7081d1b5f7a4a2a016045494f2ed0` + working-tree changes listed below; Core content checkout at pinned commit `8706f38a7b5c29308fe007baa101038f06f61906`.

---

## Claim level (read this first)

> **This run exercises LOCAL, UNSIGNED DEVELOPER ARTIFACTS built from the worker's working tree.**
> It makes **no claim** about signed artifacts, published channels, or the NSIS bootstrap
> installer path (which is canonically produced on Linux/docker per
> `build-desktop-installer.sh` and the toolchain lock). Those remain the controller's
> release-infrastructure scope. A signing authority request is filed separately in this
> report; no signing key material exists on this machine and none was used.

## What was produced locally

| Artifact | Path (under `artifacts\win004-journey\dist\`) | Size |
|---|---|---|
| v1 installer (bundled payload) | `v1\chummer-avalonia-win-x64-installer.exe` | ~174 MB |
| v1 payload zip | `v1\chummer-avalonia-win-x64-payload.zip` | ~57 MB |
| v2 installer (bundled payload) | `v2\chummer-avalonia-win-x64-installer.exe` | ~174 MB |
| v2 payload zip | `v2\chummer-avalonia-win-x64-payload.zip` | ~57 MB |

Build recipe mirrors the canonical `build-desktop-installer.sh` bundled-mode path:
self-contained single-file app publish → reproducible payload zip → installer publish
with `InstallerPayloadZip` + metadata properties → `CHUMMER6PAYLOAD1` append → payload
gate verify. Both installs were produced with **distinct stamps** (v1 `run-20261031-194618`,
v2 baked `run-20261031-184914`) so the update is a genuine version transition.

## Journey result: **PASS** (9/9 legs)

Harness: `scripts/win004-installer-update-recovery-journey.ps1` (archived copy:
`C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-004\verification-harness\win004-installer-update-recovery-journey.ps1`).
Receipt: `artifacts\win004-journey\journey-receipt.json` (archived next to the harness copy).

| # | Leg | Result | What was actually verified |
|---|---|---|---|
| 1 | preflight | pass | SDK, core-content checkout validated (pinned-core contract), registry/pre-clean state |
| 2 | build-v1 | pass | Real local build of app + payload + installer |
| 3 | build-v2 | pass | Second, differently-stamped build for the update transition |
| 4 | manifests | pass | v2 channel manifest (canonical parser schema, file:// artifact) + sha-mismatch "bad" manifest |
| 5 | install | pass | Real `--unattended` install: `%LOCALAPPDATA%\Programs\Chummer6\avalonia-win-x64`, HKCU uninstall entry, Start-Menu/Desktop shortcuts |
| 6 | start | pass | Installed exe launches and stays alive ≥15 s with content root env + update channel wired |
| 7 | update-relaunch | pass | Live app (v1) → channel manifest v2 → download+verify → helper handoff → v1 exits for apply → staged v2 installer runs → **registry version flips** → updated head relaunches and stays alive |
| 8 | recovery | pass | Sha-mismatched manifest artifact: runtime refuses the download, journals the failure, app stays alive, install dir intact |
| 9 | uninstall | pass | `--unattended --uninstall`: HKCU entry gone, install dir removed, no dialog hang |

## Disclosed defects found by this verification (for the controller)

These are **base-branch product defects** surfaced only because WIN-004 drove the
full real-user update cycle. Fixes are outside WIN-004's allowed paths except where noted.

1. **`Chummer.Avalonia` never exits for the update handoff** (FIXED on this branch,
   allowed path): `MainWindow_OnOpened` returned after `updateHandoffStarted` with the
   main window still open, so the process never exited and the apply helper's 120 s
   wait-for-parent-exit always timed out before the staged installer could run.
   The Blazor head handles this contract correctly; the Avalonia head never did.
   Fix: close the main window on handoff (default `ShutdownMode` then exits the process).

2. **`Chummer.Desktop.Installer` splash use-after-dispose** (NOT fixed — project is
   **outside WIN-004's allowed paths**; controller authorization required): in the
   `--auto-update --launch-head` path the installer completes the full install
   (`install complete` in its own trace), then crashes ~250 ms later with
   `ObjectDisposedException` in `InstallSplashForm` (`Show` after dispose) — so the
   automatic post-update relaunch never happens. The harness verifies the relaunch
   natively itself (Start-Menu semantics, `relaunchMode=harness-fallback` in the
   receipt; `installerAutoRelaunchDefectDetected=true`). Recommend a controller-authorized
   follow-up lane for `Chummer.Desktop.Installer/`.

3. **Installed-app content resolution gap at base** (mitigated on this branch via
   WIN-001's design; coordination fact for the controller): at base `2fec7c7ac` the
   Avalonia head resolves its content root only from repo ancestors — an installed
   copy outside a repo tree cannot start (`Could not locate data file 'lifemodules.xml'`).
   Mitigation on this branch: ported WIN-001's `DesktopCoreContentRootResolver.cs`
   byte-identically from the handoff head into `Chummer.Avalonia/` (allowed path) and
   wired it in `ConfigureServices` exactly as the handoff head does, honoring
   `CHUMMER_CORE_CONTENT_ROOT` → validated pinned-core checkout
   (`D:\sources\chummer\chummer6-core` @ `8706f38a…`). The env inherits through the
   whole chain (app → apply helper → installer → relaunched head), verified natively.

## Source changes on this branch

```
M  Chummer.Avalonia/App.axaml.cs                          (defect 1 fix + resolver wiring)
A  Chummer.Avalonia/DesktopCoreContentRootResolver.cs     (byte-identical port from handoff head)
A  scripts/win004-installer-update-recovery-journey.ps1   (the verification harness)
A  docs/WIN-004-installer-update-recovery-evidence.md     (this file)
```

## Machine-level residue (disclosed)

The journey installs/uninstalls into the **real user-scope locations** (that is the
claim): `%LOCALAPPDATA%\Programs\Chummer6\avalonia-win-x64` (removed by the uninstall
leg), `HKCU\...\Uninstall\Chummer6.avalonia-win-x64` (removed), Start-Menu/Desktop
`Chummer6*.lnk` (removed by the uninstaller), and the desktop-update state root
`%LOCALAPPDATA%\Chummer6\desktop-update\` (left in place; journals + staged artifacts
are part of the evidence). No machine-wide or admin-scope changes were made.

## Verification provenance

- Journey receipts with per-leg details, timestamps, version stamps: `journey-receipt.json`
- Installer progress traces: `%TEMP%\chummer-desktop-installer-progress.log` snapshots
  captured in the receipt (`progressTraceTail` fields)
- The failing-update (recovery) leg's journaled runtime state is included in the
  receipt's `recovery.stateAfter` snapshot

## Handoff integration outcome (2026-10-31, post-PR-merge)

The authorized merge sequence landed on `feat/remote-windows-coordination-20261031`:

- #490 (WIN-002) → merge `4ff8cc5a2`
- #491 (WIN-003) → merge `fe07488de`
- #492 (WIN-004, this work) → merge `316e2593f`

Post-merge guards CI surfaced **6 new failures, 0 pre-existing** (guards green at `c857c39ef`):

1. **Product regression (fixed):** the WIN-002 branch content carried a divergent
   `Chummer.Avalonia/App.axaml` (`RequestedThemeVariant="Light"`, no BoxShadows, square
   corners, compact metrics) that overwrote the WIN-001 sealed visual contract. Restored
   via PR **#493** (`windows/fix-restore-win001-visual-contract`, head `8541ec1bba`) to
   the exact pre-merge blob `178bcf47e` (verified by hash). Attribution: the guards and
   fresh-checkout checks were already failing on the #490/#491 branch heads themselves,
   so the regression rode in on branch content, not merge mechanics.
2. **Package-plane topology failures (5):** pre-existing class — the fresh-checkout
   verifier fails identically at pre-merge `c857c39ef` (10:59Z run). These resolve only
   through the controller-owned sealed-preseal publication for handoff→main (#486):
   the split-preseal recipe whitelist (`ALLOWED_RECIPE_PATHS`, test-pinned) rejects 37
   of the handoff's 40 changed paths, so neither a preseal transaction nor a whitelist
   extension is a worker action.

Handoff→main publication (#486) remains **open/blocked for the controller**, unchanged.
