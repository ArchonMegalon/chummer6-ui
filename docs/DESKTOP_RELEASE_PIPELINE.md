# Desktop release pipeline

`chummer6-ui` owns desktop packaging, installer recipes, and updater integration.
It does not own promoted release-channel truth.

## What this repo owns

* building desktop publish directories
* producing Windows installer and portable `.exe` artifacts, plus portable platform bundles alongside macOS `.dmg` and Linux `.deb` desktop artifacts
* emitting a desktop release bundle (`files/` plus release metadata) that Fleet can orchestrate
* running startup smoke on each packaged desktop head before promotion evidence is considered complete
* materializing a repo-local Linux desktop exit gate that builds the Linux binary, packages the primary `.deb`, installs and purges that `.deb` inside an isolated dpkg root while booting the installed head in startup-smoke mode, and records unit-test proof
* emitting bounded release-regression packets when startup smoke fails or crashes
* keeping the desktop head honest about whether a target is still an archive, an installer, or a richer updater-ready package

## What this repo does not own

* final release-channel promotion
* canonical installer/update-feed state
* public `/downloads` truth
* public account-aware install policy

## Release flow

1. `chummer6-ui` builds Windows, macOS, and Linux artifacts from one release build.
2. `chummer6-ui` launches each packaged head in startup-smoke mode and captures receipts or a release-regression packet.
3. `chummer6-ui` materializes `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json` via `scripts/materialize-linux-desktop-exit-gate.sh` before Fleet may accept the desktop lane as release-complete.
4. Fleet orchestrates the release wave.
5. `chummer6-hub-registry` materializes `RELEASE_CHANNEL.generated.json` and the compatibility `releases.json`.
6. `chummer6-hub` serves public downloads by consuming the registry projection.

When a downloads deploy target is configured, the `Desktop Downloads Matrix` workflow publishes the live `chummer.run` shelf once per day during the 08:00 Europe/Vienna release window. Pushes do not publish downloads. Manual workflow runs are build/proof runs by default; they publish only when `force_publish_downloads` is explicitly enabled.

Local proof work should build only the surface and platform needed for the change. A desktop UI fix should use targeted unit/UI tests and, when packaging is needed, the affected public platform only. A full Windows+Linux release package is reserved for the scheduled release window or an explicit operator override.

Windows installer visual capture is a host-specific gate. After a Windows installer succeeds on a real Windows host, run:

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-windows-installer-visual-proof.ps1
```

The script launches the promoted installer, captures progress and completion screenshots, hashes the images and installer bytes, and writes `.codex-studio\published\WINDOWS_INSTALLER_VISUAL_PROOF.generated.json`. The desktop release matrix may treat this as the only external host proof when local payload, digest, and update-handoff gates pass; it must still fail for missing payloads, mismatched hashes, or stale local release artifacts.

For the public shelf, Windows `win-x64` and Linux `linux-x64` are the rolling-release scope. Mainline builds resolve to `preview` automatically for that scope, and `public_stable` remains an explicit promotion step. macOS remains buildable and publishable as a bounded artifact lane, but it does not get to hold back the public Windows/Linux shelf when the current public promotion policy is Windows/Linux-only.

Desktop heads may consume that canonical registry projection directly for self-update when `CHUMMER_DESKTOP_UPDATE_MANIFEST` points at `RELEASE_CHANNEL.generated.json` (or a compatible `/downloads/` base URL).

Chummer Online is a separate delivery lane from native desktop packaging. In that lane, `/app` is the clean public Chummer Online route, `/blazor/` is the stable hosted Blazor entry into Chummer Online, `/blazor/app` is the hosted app path, `/blazor/home` is the product/orientation page, `/blazor/workbench` remains the /blazor/workbench compatibility route for the same promoted Chummer Online client, and `/blazor/preview` remains a supporting proof surface rather than the primary user promise. For Docker self-hosting of `Chummer.Blazor` behind `Chummer.Portal`, use [BLAZOR_SELF_HOST_RUNBOOK.md](BLAZOR_SELF_HOST_RUNBOOK.md). For the broader Chummer Online design and parity bar, use [BLAZOR_WEB_CLIENT_PARITY_GOAL.md](BLAZOR_WEB_CLIENT_PARITY_GOAL.md).

The local shell wrappers in `scripts/generate-releases-manifest.sh` and `scripts/verify-releases-manifest.sh` are compatibility entrypoints. The canonical materializer now lives in `chummer6-hub-registry`.

For a Mac-hosted Codex/operator flow that builds, signs, notarizes, smoke-tests, and publishes a desktop bundle to `chummer.run`, use [MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md](MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md).
