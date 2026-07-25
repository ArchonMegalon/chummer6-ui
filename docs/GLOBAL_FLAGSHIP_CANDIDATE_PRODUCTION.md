# Global flagship candidate production

`.github/workflows/global-flagship-candidate.yml` is the only repository
workflow that may turn the existing Windows, Linux, and macOS native artifacts
into a `chummer6-ui.global-flagship-candidate.v1` root. It is nonpublishing. It
cannot sign, notarize, create a release, deploy, mutate Registry state, advance
a channel, or upload public release bytes.

## Protected authority

Create the `global-flagship-candidate-production` environment:

- restrict deployment branches to `main` only;
- require an independent human reviewer and prevent self-review;
- disable administrator bypass;
- do not add deployment, hosting, Registry-write, release-write, signing, or
  notarization credentials;
- add only `CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PEM` and, when the key is
  encrypted, `CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PASSPHRASE`;
- set the environment variable
  `CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256` to the reviewed public recipient
  pin used by the macOS evidence lane.

The normal job token has only `Actions: read` and `Contents: read`. The private
key exists only as a mode `0600` runner-temp file while the encrypted macOS
candidate is opened. It is removed before upload. It, its passphrase, GitHub
tokens, signing keys, notary credentials, and deployment credentials are not
written to any receipt or artifact.

The dispatcher must be independent from every native evidence actor. The
existing assembler rejects a candidate producer who also supplied native
evidence.

## Exact provider inputs

Dispatch from the exact protected current `main` SHA with a candidate ID,
generation ID, channel ID, and all three fields—numeric provider artifact ID,
exact artifact name, and exact `sha256:<lowercase-hex>` provider digest—for
each of these seven roles:

| Role | Reserved workflow | Exact name |
| --- | --- | --- |
| Windows export | `preview-nightly-candidate-export.yml` | `preview-nightly-candidate-RUN_ID-1` |
| Windows capture | `windows-native-evidence-capture.yml` | `windows-native-evidence-RUN_ID-1` |
| Windows evidence | `windows-native-evidence-finalize.yml` | `windows-native-evidence-finalized-RUN_ID-1` |
| Linux export | `linux-native-candidate-export.yml` | `linux-native-candidate-RUN_ID-1` |
| Linux evidence | `linux-native-lifecycle-evidence.yml` | `linux-native-lifecycle-RUN_ID-1` |
| macOS encrypted custody | `macos-flagship-evidence.yml` | `macos-flagship-encrypted-escrow-RUN_ID-1` |
| macOS handoff | `macos-flagship-evidence.yml` | `macos-flagship-handoff-RUN_ID-1` |

The Linux lifecycle artifact must contain the passing
`UI_LINUX_DESKTOP_EXIT_GATE.generated.json` emitted by the repository's
canonical promoted-only Linux exit-gate materializer against the exact
candidate package. Its fixed candidate shelf, release manifest, unit-test
results, and native startup/mouse-first gate output remain in that same
provider artifact; transient NuGet/state/build-lock storage and the
materializer's local `latest` symlink do not. The macOS encrypted-custody
artifact must contain the passing
`UI_MACOS_AVALONIA_OSX_ARM64_DESKTOP_EXIT_GATE.generated.json` emitted from
the exact signed/notarized DMG, candidate release manifest, and real
post-update startup receipt before plaintext custody is sealed. These are
native provider outputs, not candidate-producer projections.

The two macOS artifacts must come from the same run. Every other role must use
a distinct run. Artifact IDs and names must all be distinct.

For every input, the producer authenticates through read-only GitHub APIs:

- the repository, current protected `main`, and exact source SHA;
- workflow run and exact attempt `1`, successful direct
  `workflow_dispatch`, source ref/SHA, original actor, triggering actor,
  repository, and head repository;
- the absence of pull-request binding and referenced reusable workflows;
- the active workflow ID/path and its source-controlled blob at the exact SHA;
- the live provider user/bot identity;
- the complete, unpaginated artifact list plus exact artifact detail, expiry,
  run binding, source binding, size, name, ID, and digest;
- the downloaded ZIP bytes from the numeric artifact ID through GitHub's one
  documented credential-stripped storage redirect.

The producer rejects reruns, replays, PR runs, reusable-workflow runs, deleted
or expired artifacts, pagination, duplicate IDs/names, cross-source inputs,
cross-platform run reuse, actor drift, provider-detail drift, archive digest
drift, unsafe ZIP paths, links, special files, encrypted members, duplicate or
case-colliding members, and compression or size abuse.

## Candidate materialization

Windows and Linux export/evidence trees are preserved under deterministic
provider-role directories. The macOS `receipts/` and `escrow/` layout stays at
the candidate root because its aggregate receipt binds those portable paths.
The macOS DMG is decrypted only after its escrow receipt, provider artifact,
recipient SPKI, ciphertext, and authenticated source run agree.

The producer:

1. keeps the exact downloaded provider ZIPs and their authenticated metadata;
2. validates the Windows and Linux rich lifecycle receipts and emits their
   existing assembler adapters with
   `desktop_native_lifecycle_evidence.py emit-flagship-adapter`;
3. revalidates the existing macOS adapter and aggregate evidence;
4. requires all platforms to identify the same candidate release, distinct
   N-1 release, source SHA, and live public predecessor-root bytes;
5. requires, but never invents, each platform's exact exit-gate receipt from
   its reserved authenticated provider artifact (Windows export, Linux
   lifecycle evidence, or macOS encrypted custody) and the required
   Windows/macOS signing receipts;
6. writes `GLOBAL_FLAGSHIP_CANDIDATE.generated.json`;
7. invokes
   `assemble_global_flagship_release.py propose` for that exact manifest;
8. re-reads every run, attempt, workflow, source blob, actor, artifact list,
   artifact detail, and protected current `main` one final time;
9. writes the late reauthentication receipt and changes every output file and
   directory to read-only.

If a provider input does not contain a required platform exit/signing
authority for those exact bytes, production stops. This lane does not turn a
native lifecycle receipt into a fabricated exit-gate receipt.

The one uploaded artifact is named
`global-flagship-candidate-PRODUCER_RUN_ID-1`. It contains the complete
read-only `candidate/` root and
`GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json`. The candidate root includes
the exact provider archives, extracted evidence, release artifacts, platform
receipts, candidate manifest, initial provider-input manifest, and final
provider reauthentication receipt.

## Later approval and provider handoff

Use the proposal from this artifact unchanged for the three protected
`global-flagship-release-approval.yml` roles. Finalize with the candidate root,
then create the metadata-only bundle described in
`GLOBAL_FLAGSHIP_PROVIDER_AUTHENTICATION.md`. Candidate production does not
replace either independent authority.

## Later publication-input boundary

Topology retirement happens after candidate production and is intentionally
absent here. The later publication-input assembler must receive a separately
provider-authenticated Hub artifact and deterministically copy these three
exact files side by side:

- `topology-retirement.json`
- `committed-boundary-receipt.json`
- `post-marker-convergence-receipt.json`

Each file must byte-match the corresponding path, SHA-256, and size in that
Hub provider-authentication receipt. Missing, extra, renamed, duplicated, or
conflicting files block publication-input assembly. No candidate, approval,
provider-authentication, or publication lane may synthesize placeholders for
them.
