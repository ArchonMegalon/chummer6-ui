# Global flagship provider authentication

This lane authenticates the GitHub provenance of an already-finalized global
flagship approval set. It is deliberately nonpublishing. It cannot sign,
notarize, upload release bytes, deploy, activate a release, or authorize a
later process to do so.

The candidate and proposal normally come from the complete read-only artifact
produced by
[`GLOBAL_FLAGSHIP_CANDIDATE_PRODUCTION.md`](GLOBAL_FLAGSHIP_CANDIDATE_PRODUCTION.md).
Do not extract a subset and reconstruct either JSON file.

That candidate artifact already preserves the exact provider ZIPs and the
native Windows-export, Linux-lifecycle, and macOS-custody exit-gate receipts.
This later approval-authentication lane does not replace or regenerate those
provider-byte bindings.

## Trust result

A passing handoff sets:

- `provenanceAuthenticated: true`
- `provenanceScope` to approval runs, approval receipts, environment
  reviewers, the source reviewer policy, and current `main` governance
- `releaseArtifactBytesAuthenticated: false`
- `nonPublishing: true`
- `publicationAuthorized: false`

The first field is intentionally scoped by the next field. It is not a claim
that installer bytes, signatures, notarization, escrow, or a publication
target were re-opened through GitHub. Those checks remain mandatory in a
separate publication transaction.

## Separate authorities

Create a protected environment named
`global-flagship-provider-authentication`. Restrict it to `main`, require a
human reviewer, prevent self-review, disable administrator bypass, and give it
no signing, deployment, hosting, release, or publication credentials.

Add exactly one secret:
`CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN`. Use a fine-grained PAT or GitHub App
installation token restricted to `ArchonMegalon/chummer6-ui` with
**Administration: read**. Do not add this token to
`global-flagship-release-review`, any approval workflow, a repository
variable, or a repository-wide secret usable by that lane.

The normal job token has only `Actions: read` and `Contents: read`. The
verifier uses the separate Administration token only for
`GET /repos/ArchonMegalon/chummer6-ui/branches/main/protection`. Tests enforce
that separation, and the verifier wraps that authority in a client that
rejects every other API path and all artifact downloads.

GitHub does not expose an endpoint by which a token can prove its own
fine-grained repository selection. Configure the secret as single-repository
and read-only as described above; the handoff claims the authenticated branch
governance response, not an independently verified token-scope inventory.

## Metadata-only input

The transport artifact must be named exactly
`global-flagship-release-provider-authentication-input`. It contains one file,
`global-flagship-release-provider-authentication-input.zip`, produced with:

```bash
python3 scripts/release/authenticate_global_flagship_release.py pack \
  --proposal /path/to/proposal.json \
  --candidate /path/to/candidate.json \
  --final-receipt /path/to/final-receipt.json \
  --approval /path/to/quality/approval.json \
  --approval /path/to/release/approval.json \
  --approval /path/to/security/approval.json \
  --output global-flagship-release-provider-authentication-input.zip
```

`pack` reuses the assembler validators, refuses an existing output, and writes
a deterministic read-only ZIP. Its six entries are the proposal, candidate
manifest, final receipt, and three v2 approval receipts. Release binaries,
signing material, deployment credentials, and publication credentials are
not accepted by the bundle contract.

Upload that one ZIP from a nonpublishing job running at the candidate's exact
`main` source SHA with the pinned `actions/upload-artifact` action. Preserve
both outputs from that action:

- `artifact-id`
- `artifact-digest`

The input artifact is treated as untrusted transport, not authority. The
verifier still requires its exact provider ID, fixed name, source SHA,
nonexpired state, metadata digest, downloaded archive digest, and sole entry.
It then independently authenticates every approval artifact.

## Run procedure

Dispatch `global-flagship-provider-authentication.yml` from `main`. Supply the
input artifact ID, its exact `sha256:<lowercase-hex>` provider digest, and
confirm the nonpublishing operation.

The workflow requires a fresh dispatch (`runAttempt == 1`) by the same actor,
checks out the exact source, requires its executing `GITHUB_SHA` to equal the
candidate and current `main` source SHA, and runs under the protected provider
environment. A successful run uploads only:

`global-flagship-provider-authenticated-handoff-INPUT_ID-RUN_ID-1`

The handoff file is created once with mode `0444`; an existing path is never
replaced.

## Fail-closed checks

For each quality, release, and security approval, the verifier requires:

- one distinct actor and one distinct workflow run ID;
- exact repository, `main` ref, source SHA, workflow path, workflow ID,
  `workflow_dispatch` event, successful conclusion, and current
  `runAttempt == 1`;
- identical actor and triggering actor, both provider `User` identities;
- no pull-request binding and no referenced reusable workflow;
- exactly one run artifact with the deterministic approval name;
- exact artifact ID, digest, nonexpired metadata, source SHA, archive size,
  and downloaded ZIP bytes;
- exactly one `approval.json`, byte-for-byte equal to the receipt bound by the
  final receipt;
- an entire run-review-history log containing exactly one record, whose state
  is approved for the current environment ID and exact receipt reviewer,
  including the live provider user ID.

The shared trust root additionally requires:

- the exact proposal, candidate-manifest, v2 receipt, and final-receipt
  bindings accepted by the assembler contract;
- one source-controlled reviewer-policy blob at the exact source SHA, with one
  digest across all receipts and an exact match to live environment users;
- the current environment ID, disabled administrator bypass,
  prevent-self-review setting, distinct reviewer user IDs, and sole `main`
  deployment branch policy;
- current `main` still at the candidate SHA;
- strict status checks whose legacy context list exactly matches a nonempty,
  duplicate-free check list with every check bound to an explicit positive
  GitHub App ID; enforced admin protection; stale-review dismissal; last-push
  approval; at least one pull-request approval; no bypass allowance;
  conversation resolution; linear history; and disabled force push/deletion;
- a final reauthentication of every approval's current run and attempt,
  workflow definition, complete review-history log, artifact list and detail,
  downloaded archive bytes, and artifact detail recheck; followed by unchanged
  environment configuration and unchanged branch governance, with the source
  branch and its protection read last.

List endpoints reject an advertised next page, count mismatch, duplicates, or
more than one matching object. Contract JSON rejects duplicate keys and
unexpected fields. Provider responses tolerate additive fields but reject
missing, mistyped, or conflicting authority fields, including an absent
`can_admins_bypass` environment field.

GitHub's artifact-download REST endpoint necessarily returns a temporary
`302`. The client accepts exactly that one hop to GitHub's documented artifact
storage hosts, strips the Authorization header, verifies the archive digest,
and rejects HTTP redirects, unapproved hosts, credential-bearing URLs, or any
second redirect everywhere else.

## Remaining publication boundary

The handoff is evidence for a later decision, not that decision. A separate
protected publication transaction must revalidate the handoff and final
receipt, retrieve and hash every installer, authenticate Windows signing and
macOS signing/notarization, validate Linux package authority, inspect the live
publication target, and hold whatever narrowly scoped publication credentials
are required. None of those credentials belong in either approval lane.
