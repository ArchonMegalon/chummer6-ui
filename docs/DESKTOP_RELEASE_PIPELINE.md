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

1. `chummer6-ui` builds Windows, macOS, and Linux artifacts from one release candidate.
2. `chummer6-ui` launches each packaged head in startup-smoke mode and captures receipts or a release-regression packet.
3. `chummer6-ui` materializes `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json` via `scripts/materialize-linux-desktop-exit-gate.sh` before Fleet may accept the desktop lane as release-complete.
4. Fleet orchestrates the release wave.
5. `chummer6-hub-registry` materializes `RELEASE_CHANNEL.generated.json` and the compatibility `releases.json`.
6. `chummer6-hub` serves public downloads by consuming the registry projection.

When a downloads deploy target is configured, the successful mainline `Desktop Downloads Matrix` build is responsible for replacing the live `chummer.run` shelf automatically. The public downloads shelf is latest-only: a green build must publish the newest bundle without a second manual release pass.

Desktop heads may consume that canonical registry projection directly for self-update when `CHUMMER_DESKTOP_UPDATE_MANIFEST` points at `RELEASE_CHANNEL.generated.json` (or a compatible `/downloads/` base URL).

The local shell wrappers in `scripts/generate-releases-manifest.sh` and `scripts/verify-releases-manifest.sh` are compatibility entrypoints. The canonical materializer now lives in `chummer6-hub-registry`.

For a Mac-hosted Codex/operator flow that builds, signs, notarizes, smoke-tests, and publishes a desktop bundle to `chummer.run`, use [MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md](MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md).
