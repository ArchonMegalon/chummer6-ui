# Compatibility Cargo

This repo’s primary ownership boundary is `workbench/browser/desktop UX`.

The following retained roots are compatibility cargo, not active boundary claims:

- `Chummer/`: legacy desktop application compatibility body kept for release continuity while workbench/browser/desktop seams stay canonical.
- `ChummerDataViewer/`: legacy inspection utility retained for compatibility and audit playback, not as a new product boundary.
- `TextblockConverter/`: legacy conversion helper kept for backward compatibility, not as a shared product surface.
- `Translator/`: legacy localization helper retained as compatibility tooling.

Boundary rule:

- New workbench/browser/desktop feature work must not expand these roots.
- New shared UI primitives belong in `Chummer.Ui.Kit`.
- Play/mobile heads remain outside this repo in `chummer6-mobile`.
