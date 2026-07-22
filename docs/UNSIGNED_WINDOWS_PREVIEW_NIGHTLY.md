# Unsigned Windows preview nightly lane

This additive lane prepares one `preview`/`windows_only` nightly candidate. The
fresh delta is exactly the Avalonia `win-x64` bootstrap installer and its bound
payload. The installer is deliberately unsigned under `preview_policy`.

The lane does not claim Authenticode, native Windows capture, visual approval,
human approval, a fresh Linux or macOS build, soak completion, stable-release
eligibility, upload authority, publication authority, or deploy authority.
Existing non-Windows shelf bytes are retained byte-for-byte and mode-for-mode.

## Contract graph

1. UI materializes a non-authoritative
   `PREVIEW_NIGHTLY_UNSIGNED_COMPOSITION.proposed.json` v3 request.
2. The disposable JIT runner exports only that request, the two proposed
   manifests, the unsigned Windows installer/payload, and four provenance
   documents as one ephemeral GitHub artifact.
3. Direct import validates the complete export receipt and inventory, validates
   the exact incumbent snapshot, and reconstructs the proposed full shelf. No
   retained platform bytes cross the JIT artifact boundary.
4. Registry PREPARE v2 validates the Windows-only composition and emits the two
   exact shelf manifests plus `PREVIEW_PUBLICATION_DELTA_CANDIDATE.json` v2.
5. UI emits `PREVIEW_NIGHTLY_UNSIGNED_SCOPE.proposed.json` v3 over the Registry
   PREPARE shelf.
6. Registry FINALIZE v2 and Hub candidate-import v3 may seal the review-required
   code-deploy graph. They do not publish, upload, route, or deploy it.

The legacy signed PREPARE v1 lane is unchanged. In particular, this lane never
relabels, synthesizes, replays, or freshly builds Linux policy evidence.

## Prepare the UI composition

Run from a clean checkout whose `HEAD` is the authorized protected-main commit.
All paths must be absolute; the candidate output must not exist.

```bash
export CHUMMER_UNSIGNED_WINDOWS_PREVIEW_CANDIDATE_DIR=/absolute/private/composition
export CHUMMER_UNSIGNED_WINDOWS_PREVIEW_INCUMBENT_ROOT=/absolute/incumbent/shelf
export CHUMMER_UNSIGNED_WINDOWS_PREVIEW_VERSION=run-YYYYMMDD-HHMMSS
export CHUMMER_UNSIGNED_WINDOWS_PREVIEW_PUBLISHED_AT=YYYY-MM-DDTHH:MM:SSZ
export CHUMMER_UNSIGNED_WINDOWS_PREVIEW_SOURCE_SHA="$(git rev-parse HEAD)"
scripts/build-unsigned-windows-preview-nightly-stage.sh prepare
scripts/build-unsigned-windows-preview-nightly-stage.sh verify
```

The build uses the pinned package plane and offline native bootstrap toolchain.
Signing inputs are cleared and the PE certificate table must be empty.

## Export through the disposable runner

The launcher verifies the committed protected-main authority, creates one
nonce-bound disposable runner, dispatches the unsigned export workflow, records
the single ephemeral artifact, and tears the runner down.

```bash
mkdir -m 700 /absolute/private/jit-receipts
scripts/run-preview-nightly-unsigned-jit-launcher.sh \
  --prepared-stage-root /absolute/private/composition \
  --receipt-output /absolute/private/jit-receipts/launch.json
```

The workflow has no signing, capture-relay, publication, deployment, or
cross-workflow dispatch permission. Its artifact is candidate transport only.
Transport identity binds path, SHA-256, and size only because GitHub artifact
extraction normalizes Unix permissions. Shelf file and directory modes are
restored and verified solely from the composition request inventories.

## Direct import and seal

After independently downloading and unpacking the one recorded ephemeral
artifact into an owner-controlled directory, run the offline coordinator from
clean UI, Registry, and Hub checkouts at their reviewed protected-main commits:

```bash
scripts/run-preview-nightly-unsigned-direct-import.py \
  --export-root /absolute/private/unpacked-export \
  --incumbent-root /absolute/incumbent/shelf \
  --registry-repo-root /absolute/chummer6-hub-registry \
  --registry-source-sha REGISTRY_MAIN_SHA \
  --hub-repo-root /absolute/chummer6-hub \
  --hub-source-sha HUB_MAIN_SHA \
  --expected-version run-YYYYMMDD-HHMMSS \
  --expected-manifest-sha256 PROPOSED_MANIFEST_SHA256 \
  --ui-source-sha UI_MAIN_SHA \
  --output-root /absolute/private/sealed-review-candidate
```

The coordinator performs no download itself. It requires each checkout to be
clean, at exactly `origin/main`, and at the explicitly supplied commit. It then
reconstructs the full shelf, runs the additive Registry PREPARE/FINALIZE v2
commands, generates UI scope v3, and invokes Hub candidate-import v3. The final
directory is committed with an atomic no-replace rename and remains
review-required/nonpublishing.

## Review gates

Before any later code-deploy review, independently retain and compare:

- the UI source commit and protected-main identity;
- the composition request SHA-256 and ephemeral export receipt/inventory;
- the incumbent snapshot digest used during full-shelf reconstruction;
- Registry PREPARE v2 candidate, Registry FINALIZE v2 authority/receipt, and UI
  scope v3 digests;
- the Hub v3 candidate-import authority and its bounded expiry.

A successful seal still has `publicationAuthorized=false`,
`uploadAuthorized=false`, and `deployAuthorized=false`. Actual public upload or
deployment requires a separate reviewed release ticket and is outside this
lane.

## Regression suite

```bash
python3 -m pytest -q \
  tests/test_fresh_package_plane_controls.py \
  tests/test_windows_native_bootstrap_toolchain.py \
  tests/test_preview_nightly_unsigned_stage.py \
  tests/test_preview_nightly_unsigned_scope.py \
  tests/test_preview_nightly_unsigned_candidate_export.py \
  tests/test_preview_nightly_unsigned_direct_import.py \
  tests/test_preview_nightly_unsigned_jit_launcher.py \
  tests/test_unsigned_windows_preview_nightly_workflow.py
```
