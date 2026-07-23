# Preview-nightly evidence coordinator

`scripts/release/run_preview_nightly_pipeline.py` coordinates the existing
protected release operations without collapsing their authorities. It can run
stage preparation, the governed JIT candidate export, native Windows capture,
protected human finalization, original-artifact preservation, and stage seal.
It cannot upload release bytes, deploy, publish, or advance `CURRENT`.

All paths are absolute and integrity-bound into the resumable state:

```bash
python3 scripts/release/run_preview_nightly_pipeline.py \
  --state-file /secure/run/pipeline-state.json \
  --evidence-directory /secure/run/evidence \
  --prepared-stage-root /secure/nightly/.nightly-run-V.candidate \
  --stage-dir /secure/nightly/nightly-run-V \
  --release-version V \
  --published-at 2026-07-19T12:00:00Z \
  --stage-authority-input /secure/run/STAGE_AUTHORITY_INPUT.json \
  --provenance-output /secure/run/DURABLE_PROVENANCE.json \
  --review-request-output /secure/run/HUMAN_REVIEW_REQUEST.json \
  --handoff-output /secure/run/IMMUTABLE_PUBLICATION_HANDOFF.json \
  --finalized-archive /secure/run/finalized-original.zip \
  --run-prepare
```

`STAGE_AUTHORITY_INPUT.json` uses contract
`chummer6-ui.preview-nightly-stage-authority-input` version 1 and contains an
exact `environment` object with every source root/commit, retained-shelf
path/digest, and proof path/digest listed in `PREVIEW_NIGHTLY_STAGE.md`. Missing
or extra keys fail closed.

`--run-prepare` supports one signing backend:
`CHUMMER_WINDOWS_SIGNING_BACKEND=digicert_keylocker_linux_jsign`. Its non-secret
configuration is `CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS`,
`CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH`,
plus the governed signer leaf pins
`CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256` and
`CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256`. All digests are bare lowercase
SHA-256 values.

The caller must not supply toolchain paths or hashes. Before reading any
credential value, the coordinator verifies the root-owned `/usr/lib/dotnet`
tree and exact host digest, performs a locked restore, and publishes
`scripts/Chummer.KeyLockerSigner/Chummer.KeyLockerSigner.csproj` with the
absolute host from the signer project directory into a fresh private
directory. The project-local tracked `global.json` is byte- and
SHA-256-pinned to SDK 10.0.110 with roll-forward disabled and is checked before
restore, between restore and publish, and after publish. The publish is
framework-dependent and DLL-only; both locked restore and no-restore publish
bind the same exact `linux-x64` RID. The coordinator then seals the
complete output tree as current-UID-owned `0500` directories and single-link
`0400` files.

After verifying the provisioned tools, the coordinator owns the exact internal
contract: `CHUMMER_KEYLOCKER_DOTNET_ROOT`,
`CHUMMER_KEYLOCKER_DOTNET_BIN`, `CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256`,
`CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256`, `CHUMMER_KEYLOCKER_JAVA_HOME`,
`CHUMMER_KEYLOCKER_JAVA_BIN`, `CHUMMER_KEYLOCKER_JAVA_BIN_SHA256`,
`CHUMMER_KEYLOCKER_JAVA_TREE_SHA256`, `CHUMMER_KEYLOCKER_JSIGN_JAR`, and
`CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256`, plus the exact signer DLL path and
SHA-256 bindings for the DLL, complete publish tree, runtimeconfig, and deps
files and the project-local SDK pin. Those values are compiled or derived from the provisioned .NET tree,
Temurin 21.0.11+10, Jsign 7.5, and the private signer publish.
The Java-tree digest is recomputed using fixed `/usr/bin/tar`, sorted names,
epoch UTC mtime, numeric owner/group zero, and the parent/root-name-preserving
`java/temurin-21.0.11+10` input. Caller-selected path/hash pairs are rejected.
The signer verifies the public certificate and returned CMS leaf against both
explicit signer pins. The optional
`CHUMMER_WINDOWS_TIMESTAMP_URL` override is accepted only as the fixed
`http://timestamp.digicert.com` value. PFX,
SignTool, SMCTL, wildcard backend selection, and caller-selected Jsign modes are
not compatible fallbacks.

The credential intake is exactly `SM_HOST`, `SM_API_KEY`,
`SM_CLIENT_CERT_FILE`, and `SM_CLIENT_CERT_PASSWORD`. `SM_HOST` must be exactly
`https://clientauth.one.digicert.com`. The client-auth `.p12` or `.pfx` must be
an absolute normalized, caller-owned, current-UID, single-link, non-symlink
private file no larger than 1 MiB, with mode exactly `0400` or `0600`. The
pipeline does not copy, materialize, persist, or delete that caller-owned file;
the signer revalidates it immediately before launching Java. Every field and
the complete handoff have fixed byte limits; NULs, control
characters, multiline values, pipe-delimiter ambiguity, alternate or wildcard
hosts, `SM_TLS_SKIP_VERIFY`, unknown `SM_*`, legacy PFX variables, shell
startup hooks and exported functions, .NET/MSBuild/NuGet host hooks, native
loader hooks, and caller-selected toolchain pins fail before a child starts.

The coordinator validates and launches the root-owned, non-writable
`/bin/bash` directly, removes those intake variables from its environment,
and sends the fixed-order NUL-delimited credential record to `prepare` over a
sealed anonymous in-memory descriptor. The stage shell disables core dumps
with a builtin, consumes and closes that first descriptor before its first
external command, and keeps the values as non-exported shell state. For the one
Windows installer child, it uses the same trusted Bash and a fresh,
command-scoped process-substitution pipe on descriptor 3, closes the
process-substitution source descriptor, and closes the unrelated package-lock
descriptor in that child. The installer
disables core dumps, consumes and closes descriptor 3 before its first external
command. For the Linux selector, PowerShell is not launched. The installer
starts `/bin/bash --noprofile --norc` with the public sealed identities in
argv and a second anonymous credential descriptor. That Bash clears inherited
environment variables, imported functions, traps, options, and unrelated
descriptors; rechecks the .NET, Java, Jsign, complete signer output,
runtimeconfig, deps, and DLL identities; only then reads and closes the
credential descriptor. With no intervening process, it exports a strict fixed
allowlist, disables .NET diagnostics and telemetry, and `exec`s exactly
`/usr/lib/dotnet/dotnet <sealed-signer.dll>` with repeated public
`--artifact <absolute-path>` arguments. The secret host and storepass exist
only in that final signer environment, never in its argv. The signer in turn
uses only the sealed publish dependencies and anchored `/usr/lib/dotnet`
framework, and independently revalidates the client-auth file and Java/Jsign
toolchain before credentialed work. Unrelated restore, build, Docker, scan,
smoke, JIT, and seal children do not receive the credential names or values.
No credential is placed in coordinator argv, state, provenance, or handoff
receipts. Legacy PFX PowerShell signing remains available only outside this
flagship Linux selector and never receives its descriptor or secret fields.

Secret-bearing child output is not replayed or weakly text-redacted. The
coordinator drains and suppresses it with a 4 MiB cap, a fixed timeout, and
process-group termination; failures expose only fixed command-level messages.
JIT and seal start from explicit positive environment allowlists. Resume and
non-prepare phases reject ambient signing variables rather than silently
carrying them forward.

This boundary protects argv, child environments, normal logs, and durable
coordinator artifacts. It is not isolation from a hostile process already
running as the same operating-system user: such a process may be able to use
ptrace, process-memory inspection, or replace the caller-owned P12 after
validation under the host's kernel policy. Run the coordinator under a
dedicated release identity and do not colocate untrusted same-UID workloads.

The first invocation authenticates the exact source/ref, source commits,
candidate run, artifact ID/API digest, candidate inventory, relay-returned
capture run ID, and capture artifact. The relay run ID is preserved in a second
artifact bound to the exact candidate run, artifact ID, and inventory digest;
the coordinator polls only that run ID, so another same-SHA capture cannot be
substituted. It preserves the original candidate, dispatch, and capture ZIP
bytes and exits with code `3` at `action_required_human_review`.

The reviewer must inspect the two digest-bound Avalonia screenshots from the exact
request and create a separate input with contract
`chummer6-ui.preview-nightly-human-review-input` version 1. It must bind the
request SHA-256, exact capture object, authenticated reviewer, the promoted
Avalonia head, and
explicit `readability`, `contrast`, and `clipping` confirmations. Resume with
the same arguments plus:

```bash
  --review-input /secure/run/HUMAN_REVIEW_INPUT.json
```

The coordinator then dispatches the protected `windows-visual-review`
finalization workflow. The workflow remains responsible for its environment
approval and reviewer allowlist. On success the coordinator downloads the
original finalized Actions ZIP by exact artifact ID, checks the REST digest,
records durable provenance (workflow/run/source/artifact identities, reviewer,
candidate inventory, and archive digests), parses the seal, proves its release,
manifest, candidate inventory, and seven source authorities match coordinator
state, then emits an
exclusive handoff marked `sealed_for_dry_run_only` and
`uploadAuthorized=false`.

Actions expiry timestamps are recorded only as acquisition-time facts. The
provenance never claims long-term online availability; the exact original ZIPs
are the durable local evidence.
