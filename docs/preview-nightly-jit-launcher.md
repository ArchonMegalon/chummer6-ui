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
  the fixed exporter workflow and request/delete repository runners. Every
  REST call is forced to `github.com` with the official JSON accept header and
  API version `2026-03-10`; inherited enterprise-host settings are ignored.
- Docker must use a local Unix-socket context. The launcher pulls only
  `ghcr.io/actions/actions-runner:2.335.1` by the governed linux/amd64 manifest
  digest `sha256:f2387135856decdecbf780a2bfbc9debe9c2dffd742f150302444b3775474681`.
  It rejects the image unless `Config.User` is `runner` and `WorkingDir` is
  `/home/runner`. The export workflow also rejects the disposable runner
  unless its system `python3` reports exactly `Python 3.12.3` before the
  exporter is invoked.
- The absolute prepared stage root must contain the canonical manifest plus
  the Avalonia Windows x64 bootstrap installer and its download payload at
  the paths required by `preview_nightly_candidate_export.py`. Its
  KeyLocker signing receipt must cryptographically bind the installer to the
  authorized Authenticode certificate and SPKI pins.
- Supply a regular, owner-held, non-writable-by-group/others file containing
  the exact canonical immutable N−1 Windows release JSON. It must bind the
  `win-x64` installer, payload, manifest, generation, and a version distinct
  from the candidate. Symlinks, noncanonical JSON, schema drift, and changed
  bytes fail before dispatch.

## Invocation

```bash
scripts/run-preview-nightly-jit-launcher.sh \
  --prepared-stage-root /absolute/path/to/prepared-stage \
  --receipt-output /absolute/path/to/new-jit-launch-receipt.json \
  --n-minus-one-release-authority /absolute/path/to/n-minus-one-windows.json \
  --timeout-seconds 1800
```

The receipt path must not exist, and its parent must be owned by the effective
user with no group or world write permission. The resulting mode-0600 JSON is
written relative to a held no-follow parent-directory descriptor, then the
file and parent are fsynced. After the parent fsync, the held target and its
no-follow basename must retain their complete recorded identity; both the held
descriptor and a fresh no-follow reopen must contain the exact serialized
bytes and hash before the final parent identity check succeeds. It is
redacted: it
contains immutable candidate, workflow, runner-image, and artifact identities,
the N−1 byte digest and artifact identities, and the candidate signer
certificate/SPKI pins,
but never the encoded JIT configuration, credential/RSA secret bytes, or host
credentials. Runner identity and repository metadata are expected nonsecret
operational fields.

## Isolation and fail-closed behavior

The launcher snapshots the committed launcher, exporter, and native lifecycle
validator through held
no-follow descriptors, verifies their Git object IDs, and executes the
exporter and lifecycle validator from those captured bytes rather than
reopening a caller-replaceable path. It opens the exact candidate source files
without following links, holds their
descriptors, records their identities and hashes, copies to new exclusive
descriptors in a private directory, and checks the held and path identities
again before and after validation. The committed exporter contract validates
the private copy. Only that copy is bind-mounted read-only.

The random nonce creates one repository-unique runner label. Under the pinned
2026-03-10 API, workflow dispatch sends exactly the fixed ref and inputs plus
the top-level `return_run_details: true` request. The mandatory HTTP 200
response's positive run ID and canonical repository-bound URLs are parsed
first, but cancellation is armed
only after an exact GET verifies the run's actor, triggering actor, repository,
main ref/SHA, workflow, attempt, and URLs. A lost POST response is never guessed:
the launcher compares a pre-dispatch paginated baseline with the fixed
workflow's runs and accepts exactly one nonce-bound `run-name`/job-label match,
then exact-GET verifies it. Zero or multiple matches stop with a bounded manual
cleanup notice and no automated cancellation. Workflow correlation requires that exact
run *and* an export job whose labels are
exactly `self-hosted`, `linux`, `x64`, and that nonce-derived label; concurrent
dispatches with other labels cannot be selected.

The fixed exporter dispatch carries the exact N−1 JSON plus signer pins
derived from the validated signing receipt. A hosted preflight compares those
pins to the repository-authorized pins and independently validates the N−1
schema and canonical bytes. The producer handoff v3 binds their hashes. The
bot-owned hosted relay then dispatches only the fixed native-capture workflow
with both exact inputs; its correlation receipt v2 records the N−1 digest and
signer authority.

GitHub returns the encoded JIT configuration to the host `gh` process. The
host strictly decodes the pinned runner's exact three-file JIT map, rejects
duplicate JSON keys, noncanonical base64, extra filenames, and duplicate
labels, and creates a high-entropy private ownership marker. Only a synthetic
tar stream containing the marker and the exact three files crosses stdin of a
networkless, read-only-root holder container from the pinned image. The holder
is acquired by its returned 64-hex container ID and retains its anonymous
64-hex volume as an unforgeable lease. It stays stopped while the launcher
independently verifies the marker and every config file's hash, regular-file
identity, link count, uid/gid 1001, and mode 0600 through returned-ID,
networkless helpers. The marker and JIT bytes never appear in an argument,
environment variable, Docker inspect payload, label, receipt, or candidate
mount. The encoded JIT configuration and credential/RSA secret bytes never
appear in launcher-managed logs. The official runner may log nonsecret
`.runner` operational metadata such as its agent name, repository identity,
and server settings; that output is expected and is not credential material.

The job container runs as numeric uid/gid `1001:1001`, drops all capabilities,
enables `no-new-privileges`, and receives exactly two read-only mounts: the
three-file candidate subset and `/jit-seed`. Its root filesystem is a disposable
writable overlay. A static no-secret bootstrap exclusively copies and fsyncs
the exact three credential files into that overlay, revalidates their bytes and
mode, then execs `/home/runner/run.sh` with zero JIT arguments. No home
directory, Docker socket, repository sibling, or host credential is mounted.

The JIT runner may execute only one job. Completion is accepted only when the
correlated workflow succeeds, the exact export job reports the expected runner
name and label set, no second job reports that runner name, and one unexpired
run-bound candidate artifact exists. Timeouts, changed identities, extra jobs,
ambiguous runs, or cleanup mismatches fail closed. On every exit path the
launcher attempts identity-checked cleanup of the exact runner registration,
returned-ID runner/helpers, dispatched run, and private snapshot. The seed is
verified again before `docker container rm --volumes <holder-id>` removes the
holder lease and its anonymous volume; successful exact
`container ls --all --no-trunc -q` and `volume ls -q` inventories must confirm
absence. Docker list, inspect, and
context errors are failures, never evidence that an object is absent.
If cleanup itself fails while another error or interruption is already in
flight, the original failure remains primary. Cleanup failures are appended as
a bounded note containing only fixed operation names and exception types; raw
cleanup messages and secrets are never copied into the note.

`SIGTERM` and `SIGHUP` are converted to catchable governed termination so this
same cleanup path runs. Docker client timeouts terminate, then kill if needed,
and always communicate/reap the child while preserving the original exception.
`SIGKILL` cannot be caught; after a hard kill, treat any nonce-labeled holder,
runner registration, or workflow run as requiring manual operator inspection.

Do not reuse the receipt as publication authority by itself. Native Windows
capture/finalization, stage sealing, consumer dry-run, and transactional shelf
publication remain separate gates.
