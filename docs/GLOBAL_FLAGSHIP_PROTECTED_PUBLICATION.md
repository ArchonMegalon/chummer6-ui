# Global flagship protected publication

This is the only workflow allowed to turn an approved global flagship
candidate into public bytes. It is intentionally separate from candidate
production, native signing, independent approval, and provider authentication.
A passing provider handoff is required but does not authorize publication.

## Protected authority

Create `global-flagship-protected-publication` as a protected environment:

- allow only `main`;
- require a human reviewer and prevent self-review;
- disable administrator bypass;
- add exactly two mutually independent environment secrets:
  `CHUMMER_FLAGSHIP_PUBLICATION_TOKEN` and
  `CHUMMER_FLAGSHIP_HUB_ACTIONS_READ_TOKEN`;
- scope the publication token only to the canonical Chummer downloads upload
  endpoint.

Scope the Hub token to **Actions: read** and **Contents: read** on
`ArchonMegalon/chummer6-hub` only. It must have no UI-repository,
Administration, deployment, or publication permission.

Do not expose the approval-environment reviewers, the
`CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN`, KeyLocker credentials, Apple Developer ID
material, notary credentials, repository-write credentials, or a Cloudflare
account token to this environment. The normal job token is read-only
(`Actions: read`, `Contents: read`). The publication token is passed only to
the canonical `scripts/publish-download-bundle-http.sh` child process through a
minimal environment.

## Complete publication input

Candidate production and publication-input assembly are distinct causal
steps. The candidate producer first uploads one pre-approval payload named
`global-flagship-candidate-payload-CANDIDATE_ID-PRODUCER_RUN_ID-1`.
That archive contains exactly the root
`GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json` plus the immutable
`candidate/` tree. It cannot pre-bind approvals, a final receipt, Hub proof,
public manifests, destination bytes, or a future publication bundle.
After the three independent approvals, provider-authenticated handoff, and Hub
retirement proof exist, dispatch
`global-flagship-publication-input-assembly.yml` from the same protected
`main` source commit.

Create `global-flagship-publication-input-assembly` as a second protected
environment. Allow only `main`, require a human reviewer with self-review
prevention, and disable administrator bypass. It receives only the independent
Hub Actions/Contents read token; it receives no publication, signing,
Administration, repository-write, or deployment authority.

The assembly lane downloads every upstream archive directly through its
provider API, hashes the downloaded ZIP, validates its exact artifact ID,
name, digest, source run, workflow, and attempt. It requires the producer
proposal and candidate bytes to equal the metadata-only provider bundle,
authenticates all three final-receipt-bound approval archives, and validates
the exact three-file Hub authority before constructing any public material.
It then rebases the immutable `candidate/` tree to the publication-input root,
runs the Registry v2 materializer and production verifier pinned at
`577d796bdd197fe6735c263c9122dad7e949d04a`, and emits one
artifact named
`global-flagship-publication-input-CANDIDATE_ID-ASSEMBLY_RUN_ID-1`.
Its extracted root contains:

```text
<the exact provider-handoff-bound candidate manifest path>
<the exact provider-handoff-bound proposal path>
<the exact provider-handoff-bound final-receipt path>
approvals/quality/<the final-receipt-bound quality receipt basename>
approvals/release/<the final-receipt-bound release receipt basename>
approvals/security/<the final-receipt-bound security receipt basename>
topology-retirement.json
committed-boundary-receipt.json
post-marker-convergence-receipt.json
channel-promotion-authority.json
destination-intent.json
destination-plan.json
public-bundle/RELEASE_CHANNEL.generated.json
public-bundle/releases.json
public-bundle/files/<the exact Windows, Linux, and macOS installers>
public-bundle/startup-smoke/<the three candidate-bound native startup receipts>
<every candidate-relative receipt and evidence file>
publication-input-assembly-receipt.json
```

Before it writes the candidate tree, assembly projects the completed archive,
not just the producer payload. The entry budget includes the eight bound
authority files, six duplicated public-bundle artifact/startup files, and six
generated JSON files. The expanded-byte budget includes the exact candidate
bytes, exact duplicated binaries and startup receipts, known handoff/final/
approval bytes, plus the maximum Hub and generated-authority envelopes. An
input that would exceed the protected publisher's entry or expanded-byte
limit fails before materialization. The publisher independently enforces the
same limits while extracting the authenticated assembly ZIP. Entry names are
bounded to 1024 UTF-8 bytes, and the expanded-byte limit sits below the outer
artifact limit by a worst-case standard ZIP framing reserve, so a level-zero
upload cannot become unpublishable solely because ZIP headers were added.

The Registry v2 projection contains exactly the three open-public Avalonia
installers. Its macOS artifact carries
`chummer.registry.macos-flagship-evidence-binding` v1, derived from the exact
candidate-bound `chummer6-ui.macos-flagship-evidence` v3 aggregate. The
binding preserves the aggregate SHA-256 and size, candidate/global identity,
GitHub provenance, Developer ID certificate and SPKI pins, accepted
notarization submission, and signing/notary receipt references.

The candidate remains a nonpublishing `preview` identity throughout proposal,
approval, and provider authentication. Only after the assembly has
authenticated all three approval archives and the exact Hub three-file
authority does it create `channel-promotion-authority.json` and
`destination-intent.json`, then invoke Registry with `public_stable`.
Candidate bytes are never rewritten. The promotion authority binds the
protected assembly run, exact candidate, proposal, final receipt, approvals,
Hub evidence, startup receipts, destination intent, and canonical artifact
inventory. It permits only the Registry projection
(`registryProjectionAuthorized: true`) and explicitly withholds publication
mutation (`publicationMutationAuthorized: false`). Registry validates and
projects that digest-bound authority into the public material. The destination
plan binds both exact envelope files and the candidate-manifest SHA-256 with
the `preview` → `public_stable` promotion, so neither the producer nor a
pre-approval archive can pre-bind a public channel.

The immutable
`chummer6-ui.global-flagship-publication-input-assembly.v2` receipt binds the
candidate producer, candidate
payload archive, metadata-only provider input, provider handoff archive,
three distinct approval archives and actors, exact three-file Hub archive,
proposal, final receipt, channel-promotion authority, destination intent,
destination plan, both manifests, all three installers, and a complete file
inventory. The metadata-only provider input is recorded with
`trustedAsAuthority: false`; no synthetic authority receipt can substitute for
a provider-authenticated archive.

The protected publication transaction downloads and hashes both the handoff
and assembly ZIPs itself; it does not trust checkout paths or
`download-artifact` extraction behavior. It requires the handoff bytes inside
the assembly to equal the separately downloaded handoff archive, then
authenticates the assembly workflow and run independently. It re-opens every
candidate-relative file and validates:

- exact proposal, final-receipt, and provider-handoff bindings;
- all three installer byte counts and SHA-256 digests;
- Windows KeyLocker certificate/SPKI and RFC3161 timestamp evidence;
- macOS Developer ID signing, notarization, and stapling evidence;
- Linux package integrity evidence;
- clean-install, core-workflow, N-1 update, and common live-predecessor
  evidence for all three platforms;
- the exact candidate source commit, producer run, approval graph, and final
  source binding.

The GitHub artifact IDs are not treated as trusted user input. The scripts
read current provider metadata around each direct download and require exact
artifact names, provider SHA-256 digests, source SHA, producer or assembly run
IDs, successful fresh-dispatch runs, workflow paths, actors, and no
pull-request or reusable-workflow binding. Nested archive paths are preserved
and validated as canonical paths.

## Topology-B retirement proof

`topology-retirement.json` uses
`chummer6-hub.topology-b-committed-retirement.v1`. Its `generatedAt` is a
renewable provider envelope around immutable terminal retirement bytes. The
envelope must be no more than 24 hours old and must come from a successful Hub
`main` proof run. The terminal `source.commit` remains the original
`committed-boundary.controllerSourceHead`; terminal `completedAtUtc` cannot be
later than the renewable envelope. Every terminal and post-marker field is
type-checked without coercion, and both original and resumed post-marker
verification must precede immutable terminal completion. The proof also
requires:

- `sidecarAuthorityRetired: true`;
- `activeSidecarMarkerCount: 0` and an empty marker list;
- exact retired-authority, committed-boundary, and post-marker-convergence
  receipt digests;
- canonical authority fixed to `https://chummer.run/downloads`;
- the exact SHA-256 of this checkout's
  `scripts/publish-download-bundle-http.sh`.

Missing retirement, stale proof, an active marker, or publisher drift blocks
before the publication token is used. This workflow does not revive the
Topology-B preview sidecar and cannot select a second live topology.
The proof is not trusted from the publication input alone. Using the separate
Hub read token, the transaction authenticates the current protected Hub
`main`, the successful fresh-dispatch attempt-1 proof workflow, and the exact
provider artifact name, ID, digest, and three-entry ZIP. It requires byte-for-
byte equality for `topology-retirement.json`,
`committed-boundary-receipt.json`, and
`post-marker-convergence-receipt.json`, then repeats the complete Hub provider
authentication after public readback and before authorization. The proof
workflow may run from a newer protected Hub `main` commit only when the
provider API's compare authority proves that the immutable terminal source is
its merge-base ancestor. The workflow run, artifact metadata, and current
protected branch must all bind that newer provider source SHA; no equality
between terminal source and current main is assumed.
The destination plan must also bind the same receipt bytes at the fixed,
credential-free live authority
`https://chummer.run/downloads/TOPOLOGY_B_RETIREMENT.generated.json`. The
transaction reads and hashes that live proof both before and after publication;
a candidate-provided JSON file by itself is never retirement authority.

## Destination and operator binding

`destination-plan.json` uses the
`chummer6-ui.global-flagship-publication-destination-plan.v2` contract and
binds `channel-promotion-authority.json`,
`destination-intent.json`, the exact predecessor manifest seen by all three
native lifecycle runs, the new canonical and compatibility manifest bytes,
and the exact three public installer URLs, sizes, and hashes. The predecessor
and final manifest hashes must differ. The canonical manifest's `artifacts`
projection and compatibility manifest's `downloads` projection are normalized
separately and must be exactly equal for all three platforms.
Both manifests must expose `public_stable`, while the bound candidate manifest
must remain `preview`.

Dispatch
`global-flagship-protected-publication.yml` from the exact candidate `main`
commit with:

- the provider-handoff artifact ID, exact name, and `sha256:` digest;
- the assembled publication-input artifact ID, exact
  `global-flagship-publication-input-CANDIDATE_ID-ASSEMBLY_RUN_ID-1` name, and
  `sha256:` digest;
- the Hub committed-retirement artifact ID, exact name, and `sha256:` digest;
- `PUBLISH:<proposal-sha256>` as the explicit operator confirmation.

The workflow rejects reruns (`runAttempt` must be `1`) and a different
triggering actor. Before invoking the publisher it reads the live manifest and
requires the exact common predecessor digest. After the canonical publisher
returns, it performs redirect-free public `GET` requests for both manifests
and every installer, then rechecks both manifests and the live topology proof.
Every byte count and SHA-256 must match.

UTC is recomputed for each provider read and immediately before mutation and
authorization. At both late boundaries the transaction reloads the entire
material graph, so proposal, candidate, topology, assembly, and artifact
freshness cannot be carried forward from an earlier clock observation. The
publication credential is removed from the parent environment before the
publisher is constructed, and the publisher removes it from its shell
environment immediately after copying it into its private variable.

Immediately before the publisher can mutate public state, the transaction
fsyncs an immutable `0400` prepared record and mutation-started marker in its
private local journal. If the publisher succeeds but the process stops before
the receipt is written, a fresh protected dispatch classifies the canonical
manifest before choosing any action. An exact predecessor permits one new
transaction only when its journal has no prior mutation marker. An exact
candidate triggers readback-only adoption: all six destinations, both manifest
rechecks, local candidate bytes, and the Hub provider proof must pass again,
and the publisher is not called. Any other manifest, incomplete candidate,
drifted byte, or prior marker with a predecessor fails closed without
republishing. This also permits recovery after a hosted runner and its local
journal are gone, because adoption derives authority again from the immutable
inputs, protected operator dispatch, live exact bytes, and both provider APIs.

Only after those six destination reads and final rechecks pass does the
write-once `0444`
receipt set:

- `provenanceAuthenticated: true`;
- `releaseArtifactBytesAuthenticated: true`;
- `signingAndNotarizationAuthenticated: true`;
- `topologyRetirementAuthenticated: true`;
- `destinationBytesVerified: true`;
- `publicationAuthorized: true`.

Any drift leaves no authorized receipt. The receipt is the only uploaded
workflow artifact and contains no credentials.
