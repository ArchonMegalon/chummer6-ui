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
- add exactly one environment secret,
  `CHUMMER_FLAGSHIP_PUBLICATION_TOKEN`;
- scope that token only to the canonical Chummer downloads upload endpoint.

Do not expose the approval-environment reviewers, the
`CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN`, KeyLocker credentials, Apple Developer ID
material, notary credentials, repository-write credentials, or a Cloudflare
account token to this environment. The normal job token is read-only
(`Actions: read`, `Contents: read`). The publication token is passed only to
the canonical `scripts/publish-download-bundle-http.sh` child process through a
minimal environment.

## Complete publication input

Upload one artifact named
`global-flagship-publication-input-CANDIDATE_ID` from the exact candidate
producer run. Preserve its `artifact-id` and `artifact-digest`. Its extracted
root must contain:

```text
<the exact provider-handoff-bound candidate manifest path>
<the exact provider-handoff-bound proposal path>
<the exact provider-handoff-bound final-receipt path>
approvals/quality/<the final-receipt-bound quality receipt basename>
approvals/release/<the final-receipt-bound release receipt basename>
approvals/security/<the final-receipt-bound security receipt basename>
topology-retirement.json
destination-plan.json
public-bundle/RELEASE_CHANNEL.generated.json
public-bundle/releases.json
public-bundle/files/<the exact Windows, Linux, and macOS installers>
<every candidate-relative receipt and evidence file>
```

The protected transaction reuses the assembler and provider-authentication
validators. It re-opens every candidate-relative file and independently
validates:

- exact proposal, final-receipt, and provider-handoff bindings;
- all three installer byte counts and SHA-256 digests;
- Windows KeyLocker certificate/SPKI and RFC3161 timestamp evidence;
- macOS Developer ID signing, notarization, and stapling evidence;
- Linux package integrity evidence;
- clean-install, core-workflow, N-1 update, and common live-predecessor
  evidence for all three platforms;
- the exact candidate source commit, producer run, approval graph, and final
  source binding.

The GitHub artifact IDs are not treated as trusted user input. The workflow
downloads by ID, and the script independently reads the current provider
metadata twice. It requires the exact artifact names, provider SHA-256
digests, source SHA, producer run IDs, successful fresh-dispatch runs, workflow
paths, actors, and no pull-request or reusable-workflow binding.

## Topology-B retirement proof

`topology-retirement.json` uses
`chummer6-hub.topology-b-committed-retirement.v1`. It must be a fresh,
successful Hub `main` receipt with:

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
The destination plan must also bind the same receipt bytes at the fixed,
credential-free live authority
`https://chummer.run/downloads/TOPOLOGY_B_RETIREMENT.generated.json`. The
transaction reads and hashes that live proof both before and after publication;
a candidate-provided JSON file by itself is never retirement authority.

## Destination and operator binding

`destination-plan.json` binds the exact predecessor manifest seen by all three
native lifecycle runs, the new canonical and compatibility manifest bytes, and
the exact three public installer URLs, sizes, and hashes. The predecessor and
final manifest hashes must differ.

Dispatch
`global-flagship-protected-publication.yml` from the exact candidate `main`
commit with:

- the provider-handoff artifact ID, exact name, and `sha256:` digest;
- the complete publication-input artifact ID and `sha256:` digest;
- `PUBLISH:<proposal-sha256>` as the explicit operator confirmation.

The workflow rejects reruns (`runAttempt` must be `1`) and a different
triggering actor. Before invoking the publisher it reads the live manifest and
requires the exact common predecessor digest. After the canonical publisher
returns, it performs redirect-free public `GET` requests for both manifests
and every installer, then rechecks both manifests and the live topology proof.
Every byte count and SHA-256 must match.

Only after those five destination reads pass does the write-once `0444`
receipt set:

- `provenanceAuthenticated: true`;
- `releaseArtifactBytesAuthenticated: true`;
- `signingAndNotarizationAuthenticated: true`;
- `topologyRetirementAuthenticated: true`;
- `destinationBytesVerified: true`;
- `publicationAuthorized: true`.

Any drift leaves no authorized receipt. The receipt is the only uploaded
workflow artifact and contains no credentials.
