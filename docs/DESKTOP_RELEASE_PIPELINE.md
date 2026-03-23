# Desktop release pipeline

`chummer6-ui` owns desktop packaging, installer recipes, and updater integration.
It does not own promoted release-channel truth.

## What this repo owns

* building desktop publish directories
* producing installer-capable desktop artifacts
* emitting a desktop release bundle (`files/` plus release metadata) that Fleet can orchestrate
* keeping the desktop head honest about whether a target is still an archive, an installer, or a richer updater-ready package

## What this repo does not own

* final release-channel promotion
* canonical installer/update-feed state
* public `/downloads` truth
* public account-aware install policy

## Release flow

1. `chummer6-ui` builds artifacts and assembles a desktop bundle.
2. Fleet orchestrates the release wave.
3. `chummer6-hub-registry` materializes `RELEASE_CHANNEL.generated.json` and the compatibility `releases.json`.
4. `chummer6-hub` serves public downloads by consuming the registry projection.

The local shell wrappers in `scripts/generate-releases-manifest.sh` and `scripts/verify-releases-manifest.sh` are compatibility entrypoints. The canonical materializer now lives in `chummer6-hub-registry`.
