# Governed preview-nightly JIT launcher

`scripts/run-preview-nightly-jit-launcher.sh` is the host-side authority for
the single-use Linux runner that exports the Windows preview candidate. It is
not a publisher and it cannot alter the live downloads shelf.

## Preconditions

- Run it from a committed checkout whose `HEAD` is the exact remote
  `ArchonMegalon/chummer6-ui` default-branch (`main`) commit. The committed
  launcher and candidate-export contract must be byte-identical to that
  commit.
- `gh` must already be authenticated for the repository and able to dispatch
  the fixed exporter workflow and request/delete repository runners.
- Docker must use a local Unix-socket context. The launcher pulls only
  `ghcr.io/actions/actions-runner:2.335.1` by the governed linux/amd64 manifest
  digest `sha256:f2387135856decdecbf780a2bfbc9debe9c2dffd742f150302444b3775474681`.
  It rejects the image unless `Config.User` is `runner` and `WorkingDir` is
  `/home/runner`.
- The absolute prepared stage root must contain the canonical manifest plus
  the two Windows x64 bootstrap installers and their two download payloads at
  the paths required by `preview_nightly_candidate_export.py`.

## Invocation

```bash
scripts/run-preview-nightly-jit-launcher.sh \
  --prepared-stage-root /absolute/path/to/prepared-stage \
  --receipt-output /absolute/path/to/new-jit-launch-receipt.json \
  --timeout-seconds 1800
```

The receipt path must not exist. The resulting mode-0600 JSON is redacted: it
contains immutable candidate, workflow, runner-image, and artifact identities,
but never the encoded JIT configuration or host credentials.

## Isolation and fail-closed behavior

The launcher opens the five source files without following links, holds their
descriptors, records their identities and hashes, copies to new exclusive
descriptors in a private directory, and checks the held and path identities
again before and after validation. The committed exporter contract validates
the private copy. Only that copy is bind-mounted read-only.

The random nonce creates one repository-unique runner label. Workflow
correlation requires the exact new run *and* an export job whose labels are
exactly `self-hosted`, `linux`, `x64`, and that nonce-derived label; concurrent
dispatches with other labels cannot be selected.

GitHub returns the encoded JIT configuration to the host `gh` process. It is
sent only on stdin to a networkless helper from the same pinned image and
stored as uid/gid 1001, mode 0600, in a uniquely labeled Docker volume. It is
never placed in a command argument, environment variable, log, host receipt,
or candidate mount. The job container uses the image-default non-root user,
drops all capabilities, enables `no-new-privileges`, and receives exactly two
read-only mounts: the five-file candidate subset and the config volume. No
home directory, Docker socket, repository sibling, or host credential is
mounted.

The JIT runner may execute only one job. Completion is accepted only when the
correlated workflow succeeds, the exact export job reports the expected runner
name and label set, no second job reports that runner name, and one unexpired
run-bound candidate artifact exists. Timeouts, changed identities, extra jobs,
ambiguous runs, or cleanup mismatches fail closed. On every exit path the
launcher attempts identity-checked cleanup of the exact runner registration,
container, config volume, dispatched run, and private snapshot.

Do not reuse the receipt as publication authority by itself. Native Windows
capture/finalization, stage sealing, consumer dry-run, and transactional shelf
publication remain separate gates.
