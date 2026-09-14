# Compatibility Cargo

This repo’s primary ownership boundary is `workbench/browser/desktop UX`.

The following retained roots are compatibility cargo, not active boundary claims:

- `Chummer/`: legacy desktop application compatibility body kept for release continuity while workbench/browser/desktop seams stay canonical.
- `Chummer.Benchmarks/`: retained legacy benchmark harness root; migration-critical import/section/save performance ownership lives in `../chummer-core-engine/Chummer.Benchmarks`.
- `ChummerDataViewer/`: legacy inspection utility retained for compatibility and audit playback, not as a new product boundary.
- `TextblockConverter/`: legacy conversion helper kept for backward compatibility, not as a shared product surface.
- `Translator/`: legacy localization helper retained as compatibility tooling.

Boundary rule:

- New workbench/browser/desktop feature work must not expand these roots.
- New shared UI primitives belong in `Chummer.Ui.Kit`.
- Play/mobile heads remain outside this repo in `chummer6-mobile`.
- Migration-critical workspace benchmark budgets are owned and enforced in `chummer-core-engine`, not cloned as active release truth here.

Source portability:

- The repository does not carry host-absolute aliases for `chummer-core-engine`, `chummer-hub-registry`, `chummer-ui-kit`, or `chummer.run-services`.
- Package consumption remains the default. Explicit local compatibility uses `ChummerUseLocalCompatibilityTree`, `ChummerCompatibilityRoot` (default `../`), or the existing project-root overrides; nested host aliases are not required.
- Pull-request controls inspect tracked Git link blobs, including on systems with `core.symlinks=false`. Links must resolve through tracked source inside the repository; absolute targets, escaping hops, and unresolved or cyclic chains fail without reading external targets.
- Removing host aliases does not requalify a frozen release graph. Package resealing and Android source repinning and qualification remain a separate integration transaction.
