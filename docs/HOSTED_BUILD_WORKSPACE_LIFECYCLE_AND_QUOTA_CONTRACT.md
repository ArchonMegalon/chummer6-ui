# Hosted Build workspace lifecycle and quota contract

## Status: NO-GO / BLOCKED before V002

V002 must not be authored, applied, or advertised as launch-ready until every
decision marked **UNRESOLVED** in this document has an accountable owner and the
required migration, compatibility, concurrency, restore, and privacy proofs
pass for the exact release artifacts.

V001 proves durable owner-scoped active-workspace storage. It does not define a
safe contract for quotas, tombstones, recreation, unknown mutation outcomes,
old offline writers, backup rollback, or deletion replay. Adding columns without
the V2 contract below would create deterministic data-resurrection and quota
over-admission paths.

This document is provider- and topology-neutral. It does not select numeric
limits, retention windows, legal policy, database provider, replication mode,
or production topology.

## Why V002 is blocked

- The public store and HTTP contracts fence mutations only by content revision;
  they carry no workspace generation, writer epoch, or operation ID.
- V001 physically deletes a row, and create resets its content revision to one.
  An old client can therefore recreate or overwrite a logically deleted ID.
- Desktop roaming currently treats `Missing` as permission to recreate the same
  ID. A tombstone hidden as an ordinary missing row is not a safe fix.
- A count or byte query followed by a separate insert/update over-admits quota
  under concurrency.
- The current provider retries reads only. A lost mutation acknowledgement is
  deliberately not retried because commit state is unknowable without an
  idempotency key.
- A backup restore can roll back tombstones, generations, usage counters, and
  idempotency receipts together. A database-local generation alone cannot fence
  pre-restore clients or prove that acknowledged deletions survived.
- The current owner key is derived from a fixed, public, unversioned hash input.
  A normalization/key change can split one owner into two quota buckets, while
  enumerable identifiers in a copied database can be tested offline.
- V1 list failure may appear as an empty list. UI must not turn an unavailable
  storage plane into an honest-sounding “no workspaces” or “quota available”
  claim.

Relevant current contracts include:

- `Chummer.Application/Workspaces/IWorkspaceStore.cs`
- `Chummer.Contracts/Workspaces/CharacterWorkspaceModels.cs`
- `Chummer.Api/Endpoints/WorkspaceEndpoints.cs`
- `Chummer.Desktop.Runtime/GrantBoundDesktopWorkspaceRoamingSync.cs`
- `Chummer.Presentation/Overview/WorkspaceRemoteCloseService.cs`
- `Chummer.Workspaces.Postgres/PostgresWorkspaceStore.cs`
- `Chummer.Workspaces.Postgres/Migrations/V001__chummer_build_workspace.sql`
- `docs/HOSTED_BUILD_POSTGRES_DURABILITY.md`

## Non-negotiable V2 invariants

1. The durable identity is one owner plus one workspace ID. It has one lineage
   high-watermark even while no live document exists.
2. Every mutation is fenced by `(writer epoch, generation, content revision)`.
   A missing component fails closed; it is never inferred from the latest row.
3. Generation is positive, advances on explicit recreation, never wraps, never
   resets on purge, and never resets during an in-place migration.
4. Content revision is also monotonic for the full owner/workspace lineage. A
   recreated document starts above the deleted generation's last revision, not
   at one. This preserves fencing for revision-only legacy requests.
5. A lineage state is exactly `live`, `deleted`, or compact `purged`. Only
   `live` has document bytes. Delete clears document content and its content
   hash in the same transaction that writes the tombstone.
6. Purge compacts private tombstone metadata but preserves the generation and
   revision high-watermarks. If policy requires physical lineage erasure, an
   independent durable fence must prevent reuse; that design is **UNRESOLVED**.
7. Blind create-by-ID succeeds only when no lineage has ever existed. Recreation
   is a separate V2 operation against the current tombstone version.
8. Generated IDs are never deliberately reused.
9. Workspace state, quota usage, lineage state, and the idempotency receipt
   commit or roll back together.
10. Quota admission is based on persisted authoritative counters under one
    owner-scoped lock, not a free-standing `COUNT` or `SUM`.
11. Tombstone/lineage and idempotency-receipt growth are bounded by explicit
    policy. Repeated create/delete cannot bypass every resource boundary.
12. Overflow, counter drift, unknown schema state, missing writer-epoch proof,
    and generation exhaustion all fail closed.

## V2 application contract

The existing positional V1 records and `IWorkspaceStore` methods remain intact.
V2 uses additive types/capabilities so old constructors, deconstruction, numeric
enum values, and revision-only endpoints are not silently changed.

Conceptual V2 values are:

```text
WorkspaceVersion = writerEpoch + generation + contentRevision
WorkspaceMutation = operationId + canonicalRequestHash + expectedVersion
WorkspaceLifecycle = live | deleted | purged
WorkspaceUsage = liveCount + liveLogicalBytes + lineageCount + receiptCount
```

Required typed outcomes are:

| Outcome | Meaning | HTTP V2 |
| --- | --- | --- |
| `Success` | Mutation/read committed or an identical operation replayed | operation-specific 2xx |
| `Missing` | No visible identity exists | `404` |
| `Conflict` | Version mismatch, active duplicate, or operation-ID request mismatch | `409` |
| `Gone` | The authenticated owner's matching lineage is deleted/purged | `410` |
| `QuotaExceeded` | An identified quota dimension denied admission | `429` |
| `Corrupt` | Persisted or submitted canonical state is invalid | `422` |
| `Unavailable` | Storage, epoch, reconciliation, or contract proof is unavailable | `503` |

Missing `If-Match`, writer epoch, generation, or operation ID is an HTTP
precondition failure (`428`), not a storage outcome. `Retry-After` is emitted
only when the authoritative policy defines a real reset time.

## Legacy compatibility

- V1 list/get remain live-only. Tombstones are not added to ordinary lists and
  read as `404`, preserving the existing privacy boundary.
- V1 numeric strong ETags remain revision-only. V2 does not change their parser
  or silently emit a composite tag to old clients.
- V1 revision-only mutations may address a live recreated row only because the
  lineage content revision never repeats. They may not create through a known
  tombstone or purged lineage.
- Legacy create-by-ID conflicts with every existing lineage, including deleted
  and purged lineages. `Missing` must no longer authorize roaming recreation.
- V2 recreation requires the exact deleted lineage version and a new operation
  ID. It advances generation and content revision atomically.
- V1 clients that do not understand quota may fail closed with a generic
  unavailable error; they must never interpret quota denial as success.
- After restore or rollback, revision-only V1 writes remain disabled until an
  independently verified reconciliation establishes a non-reused revision
  floor. Read-only compatibility may remain available when it is honest.
- Mixed old-application/new-schema behavior is a release gate. Expand-only V002
  objects must not make the old application expose tombstones or bypass quota.

## Idempotency and unknown commit outcomes

Every V2 create, recreate, replace, checkpoint, and delete carries a
client-generated operation ID and the currently observed writer epoch.

- The request hash binds owner identity, operation kind, workspace ID, expected
  version, and canonical payload/hash. It never contains raw secret material in
  logs or evidence.
- First use reserves the operation ID inside the mutation transaction.
- Same operation ID plus the same request hash returns the stored original
  outcome and receipt without changing state or usage again.
- Same operation ID plus a different request hash returns `Conflict`.
- A repeated delete returns the original successful deletion receipt while that
  receipt is retained. A new operation against the tombstone returns `Gone`.
- Quota denial performs no mutation and does not consume the operation ID,
  allowing a later retry after authoritative usage changes.
- Transport code may retry an ambiguous mutation only with the identical
  operation ID and request bytes. Mutation retries without an operation ID are
  forbidden.
- Receipt retention and the maximum accepted offline-client age are
  **UNRESOLVED**. Expiry must not make an old operation ID unsafe to reuse; an
  expiry/high-watermark or old-client rejection design is required.

## Quota transaction and lock order

All lifecycle mutations use this order. No code path may acquire the same locks
in another order.

1. Validate bounded input, canonicalize it, compute the request hash, and compute
   the candidate logical-byte value outside the transaction.
2. Begin the database transaction and reserve/verify the operation ID.
3. Create if absent, then lock the canonical owner-usage row.
4. Lock the workspace lineage row. Multi-workspace operations lock workspace IDs
   in ordinal order.
5. Verify writer epoch, expected generation, lifecycle state, revision, owner-key
   version, and the current usage invariant.
6. Perform overflow-safe admission. For example, compare a requested delta with
   remaining capacity rather than adding two attacker-controlled large values.
7. Mutate live state/lineage, usage counters, deletion event, and operation
   receipt in the same transaction.
8. Commit before returning the receipt.

Create/recreate increments live count and bytes. Replace applies
`newLogicalBytes - oldLogicalBytes`. Delete moves live usage to zero while
retaining lineage/tombstone usage as policy defines. Checkpoint does not change
logical bytes. No failure path may partially release or consume quota.

Purge workers first identify candidates without holding lineage locks. For each
candidate they then lock owner usage first, lock and revalidate lineage second,
and compact conditionally. Purge must respect legal hold without claiming a
legal-hold policy in this document.

## Logical-byte definition: decision required

The algorithm is versioned and its version/value are persisted with each live
workspace. All providers must produce the same value for the same canonical
document. V002 is blocked until one definition and compressed-input behavior are
selected.

| Candidate | Strength | Risk / unresolved consequence |
| --- | --- | --- |
| Canonical persisted-document UTF-8 bytes | Matches the application hash input and includes envelope metadata | Serializer/schema changes require an explicit byte-algorithm version and migration |
| Canonical payload UTF-8 bytes | Closest to user-authored logical content | Excludes envelope/format overhead and may price equivalent formats differently |
| Decoded upload/document bytes before wrapping | Easy to explain at ingress | Does not directly represent the persisted canonical record or later mutations |
| Database JSON text bytes | Queryable after write | Text rendering may differ by provider/version; admission occurs too late unless duplicated in application logic |
| Physical/TOAST/dump bytes | Reflects some infrastructure cost | Compression, page layout, provider, version, and backup format make it non-portable and unsuitable as the sole logical quota |

Compressed formats, if accepted, require bounded decompression and explicit
pre- and post-decompression limits. That product/security choice is
**UNRESOLVED**.

## Conceptual V002 schema

This is a logical sketch, not an approved SQL migration or provider selection.

```text
workspace_store_control
  writer_epoch, contract_version, logical_byte_algorithm_version

workspace_owners
  stable_owner_id, usage counters, applicable limit references, usage_revision

workspace_owner_aliases
  stable_owner_id, owner_key_version, owner_key
  unique(owner_key_version, owner_key)

workspace_lineages
  stable_owner_id, workspace_id, state, generation,
  content_revision, saved_revision, document, document_hash, logical_bytes,
  updated_at_utc, deleted_at_utc, purge_after_utc, legal_hold
  primary key(stable_owner_id, workspace_id)

workspace_operation_receipts
  stable_owner_id, operation_id, writer_epoch, operation_kind,
  canonical_request_hash, outcome, resulting_generation,
  resulting_revision, receipt, created_at_utc, expiry_metadata
  unique(stable_owner_id, operation_id)

workspace_deletion_events
  deletion_event_id, stable_owner_id, workspace_id, generation,
  revision, writer_epoch, deleted_at_utc, replay_checkpoint
```

The stable owner identity/HMAC/KMS approach is **UNRESOLVED**. At minimum, owner
keys are explicitly versioned and rotation cannot create a second usage row.
Raw owner identifiers are not persisted in workspace rows or receipts.

Constraints must enforce state/document/hash/byte/timestamp consistency,
positive generations/revisions, `savedRevision <= contentRevision`, nonnegative
usage, and admitted usage within the selected limits. Required access paths
include a live-only owner/update keyset index, a policy-qualified purge index,
and a receipt-expiry index. Direct runtime DML versus narrowly scoped mutation
procedures is **UNRESOLVED** and must be reflected in least-privilege validation.

## HTTP and ETag model

- V1 retains its strong numeric ETag and revision-only `If-Match` contract.
- V2 uses a distinct route or negotiated representation and a strong opaque ETag
  covering writer epoch, generation, and content revision.
- V2 mutations require that composite `If-Match` plus `Idempotency-Key`.
- V2 create also requires the writer epoch obtained from an authenticated
  capability/bootstrap response. Recreate additionally matches the tombstone
  ETag.
- Successful mutation responses echo the exact resulting composite ETag and
  typed receipt. An ETag/body mismatch is corrupt, never silently accepted.
- Owner-private workspace, quota, tombstone, and restore-state responses use
  private/no-store cache policy and must not reveal whether another owner used
  the same workspace ID.
- Problem responses use stable machine codes for revision conflict, generation
  conflict, writer-epoch conflict, operation-ID reuse, quota dimension, gone,
  and reconciliation-required states.

## Restore, writer epoch, and deletion replay

Writer epoch is an externally fenced writable-target incarnation, not merely a
value restored from the same backup. Its authority and storage are
**UNRESOLVED**. A topology may not claim this contract until it proves that two
targets with the same epoch cannot accept writes. Epochs are never reused; a
restored database's copied epoch is never current merely because it is present.

The backup source is one consistent PostgreSQL snapshot in a reviewed native
format. The archive checksum is verified, truncated/corrupt negative cases are
rejected, and restore occurs only into a freshly created target, never in place
over a live database. PostgreSQL-major/tool compatibility, schema/data,
ACL/default-ACL posture, migration ledger/checksums, owner mapping, revisions,
and document hashes are part of the restore proof.

Before a restored target admits writes:

1. Fence/quiesce every old writer and restore into a fresh target that remains
   quarantined from application reads and writes.
2. Validate migration catalog, schema shape, owner mapping, hashes, usage
   counters, privileges, and semantic reads.
3. Obtain and persist a new externally authorized writer epoch.
4. Replay and verify deletion/tombstone events newer than the restore checkpoint
   from an independently durable current source outside the restored
   backup/PITR timeline, or remain quarantined.
5. Reconcile usage and idempotency receipts and prove no operation can be
   double-applied after the epoch change.
6. Enable V2 writes only after new clients refresh versions. Enable legacy writes
   only after the non-reused revision-floor proof passes.
7. Emit a secret-free receipt identifying backup checkpoint, new target, old/new
   epochs, deletion replay range, reconciliation result, and write-admission
   decision.

Epoch rotation fences stale mutations but does not itself remove content
resurrected by an older backup. Deletion replay, acknowledged-delete RPO,
legal/erasure treatment, and independent ledger retention are **UNRESOLVED**.
The selected tombstone/deletion-ledger availability must cover the longest
recoverable backup/PITR window plus an explicitly selected safety margin; no
duration is selected here.
Failover must likewise prove old-writer fencing and the selected acknowledged
write/delete RPO; provider marketing is not evidence.

## Honest UI and privacy boundary

- Normal list/get surfaces show live workspaces only.
- UI displays an empty state only after a typed successful list result, never
  after timeout, unavailable storage, restore reconciliation, or unknown state.
- UI does not infer quota from list length. It shows authoritative usage/limit
  data or an explicit unavailable/unknown state.
- UI must not promise undo, recovery, permanent deletion, cross-device sync, or
  acknowledged-delete survival until the selected retention and restore proofs
  support that claim.
- Whether an authenticated owner may see tombstone metadata/`410 Gone` is
  **UNRESOLVED**. Other owners always receive the non-enumerating missing view.
- Tombstones, operation receipts, evidence, metrics, and logs contain the minimum
  identifiers and no document content, raw owner ID, credential, or secret.
- A restore/reconciliation window is presented as read-only or unavailable, not
  as a completed save/delete or a clean empty account.

## Migration and rollout phases

1. **Decision gate:** resolve the blocking operator table below and freeze the V2
   wire/store contract. Status remains NO-GO.
2. **Contract/test expansion:** add additive V2 types, outcomes, capability
   negotiation, roaming fencing, and cross-provider conformance tests while V1
   remains authoritative.
3. **V002 expand:** add control, owner identity/versioning, lineage, usage,
   operation receipt, and deletion-event structures; backfill active V001 rows
   without changing their visible revisions. Validate exact counters/hashes.
4. **Mixed-version proof:** demonstrate old-app/new-schema and new-app/old-schema
   fail-safe behavior. Migration remains forward-only; no improvised down SQL.
5. **Shadow/dual-write proof:** maintain and compare lineage/usage/receipt state
   without enforcing user quota or changing delete visibility.
6. **V2 admission:** enable generation/epoch/idempotency-aware clients in a
   bounded cohort after concurrency, unknown-commit, and UI proofs pass.
7. **Quota enforcement:** enable only after the selected limits and logical-byte
   algorithm are configured and counter-drift monitoring is proven.
8. **Tombstone deletion:** change physical delete only after deletion replay,
   purge, legal/privacy, backup, and restore gates pass.
9. **Legacy contraction:** retire unsafe create/recreate and write paths only with
   measured client compatibility and an explicit rollback/read-only plan.

## Required proof suite

### Contract and lifecycle

- Initial create, explicit recreate, delete, repeated delete, purge, and gone
  visibility across in-memory, file, and hosted providers.
- Stale offline and roaming snapshots cannot recreate deleted/purged IDs.
- Old generation/revision/epoch mutations always fail after recreate or restore.
- Generation/revision exhaustion fails without wrap or reset.
- Live list/get exclude tombstones and retain deterministic keyset ordering.

### Quota and transactions

- Two creates competing for the final count slot admit exactly one.
- Concurrent replacements competing for remaining bytes cannot over-admit.
- Delete releases live usage atomically while retaining configured lineage use.
- Fault injection after every statement preserves workspace, usage, deletion
  event, and operation receipt consistency.
- Counter drift, integer overflow, compressed expansion, oversized input,
  Unicode/escaped JSON, compressible/incompressible documents, and byte-algorithm
  version transitions fail or account exactly as specified.
- Purge/recreate/legal-hold races and lock-order stress show no deadlocks.

### Idempotency, HTTP, and clients

- Unknown `COMMIT` followed by identical operation ID returns the original result.
- Reused operation ID with altered bytes, version, owner, ID, or operation fails.
- V1 numeric ETags and status mappings remain compatible; V2 composite ETags,
  `428`, `409`, `410`, `429`, and `503` map to typed client outcomes.
- Roaming, close, save, and retry callers propagate epoch/generation and never
  turn `Missing`, `Gone`, quota, or reconciliation into blind recreation.

### Migration, identity, restore, and security

- Empty V002 bootstrap, V001-to-V002 backfill, repeat migration, concurrent
  migrators, interruption/resume, checksum drift, and exact object validation.
- Backfill recomputes usage exactly and detects injected lineage/counter drift.
- Owner-key v1/v2 dual-read/rotation never creates duplicate quota buckets;
  offline enumeration and key compromise are included in the threat review.
- Restoring an older snapshot cannot silently re-enable deleted content or replay
  an acknowledged mutation under the new epoch.
- Failover proves the declared write/delete RPO and old-writer fencing.
- Runtime least privilege, direct-DML/procedure posture, secret-free diagnostics,
  and evidence privacy are independently verified.

## Unresolved operator decision table

No row has an implicit default.

| Decision ID | Decision | Explicit choices required | Blocking evidence/owner |
| --- | --- | --- | --- |
| `quota_policy` | Quota policy | Dimensions, numeric limits, tier mapping, limit-change behavior, and whether tombstones/receipts consume user-visible quota | Product, billing, operations; concurrency and UI receipts |
| `logical_bytes` | Logical bytes | Versioned candidate above, canonicalization changes, compressed-input treatment, explicit accept/reject flag, and conditional pre/post-decompression limits | Product/security/engineering; cross-provider byte vectors |
| `recreation_and_undo` | Recreation and undo | Whether IDs may be recreated, who may request it, and visible recovery promise | Product; lifecycle/roaming tests |
| `offline_compatibility` | Offline compatibility | Maximum client age, operation-ID lifetime, and write-disable behavior for older clients | Product/support; client telemetry and replay tests |
| `tombstone_privacy_policy` | Tombstone/privacy policy | Tombstone, purge, legal-hold, erasure, an explicit physical-lineage-erasure flag, independent-lineage reuse fencing, and owner-visible `410` behavior | Legal/privacy/product; approved retention matrix |
| `stable_owner_identity` | Stable owner identity | Stable ID or versioned key aliases, HMAC/KMS posture, and rotation/recovery | Security/identity; rotation and enumeration review |
| `writer_epoch` | Writer epoch | External authority, allocation, rotation, fencing, and outage behavior | Operations/security; split-writer negative proof |
| `delete_replay_and_rpo` | Delete replay and RPO | Independent deletion-event durability, replay checkpoint, acknowledged-delete loss tolerance, ledger retention, and retention safety margin | Legal/product/operations; restore/failover drill |
| `provider_and_topology` | Provider and topology | Provider, PostgreSQL major, single/standby/other topology, regions, and failover mechanism | Operations; provider-specific acceptance receipts |
| `enforcement_boundary` | Enforcement boundary | Direct DML versus mutation procedures and corresponding runtime grants | Security/engineering; least-privilege proof |
| `migration_posture` | Migration posture | Stop-the-world versus phased backfill, mixed-version window, rollback/read-only plan | Release/operations; exact-image rehearsal |
| `capacity_and_retention` | Capacity and retention | Lineage/receipt caps, cleanup cadence, backup/WAL retention, RPO/RTO, budget | Product/legal/operations; capacity and recovery receipts |

## Machine-verifiable operator decision gate

The editable decision packet is
`.codex-design/product/HOSTED_BUILD_V002_OPERATOR_DECISIONS.json`. Its twelve
stable decision IDs map one-to-one to the table above. The packet starts with
every resolution explicitly `unresolved`; empty fields are not defaults or
approvals.

Selected answers use per-facet typed envelopes rather than free text. The gate
distinguishes identifiers and identifier lists/maps, booleans, 64-bit
representation-bounded positive integers, dimensioned byte values, positive
durations with explicit units, per-dimension quota
limits, boolean accounting maps, PostgreSQL major versions, and currency plus
minor-unit budgets. Only explicitly conditional compression-limit and
lineage-reuse-fence facets may be `not_applicable`, always with rationale.
Cross-policy validation requires operation IDs to remain valid for at least the
maximum supported client age; deletion-ledger and tombstone retention to cover
the longer of backup or WAL/PITR retention plus the explicitly selected safety
margin; a false compressed-
input acceptance flag to mark both decompression limits inapplicable; and a true
flag to select both limits. A true physical-lineage-erasure flag also requires an
explicit independent-lineage reuse fence. Descriptive policy identifiers cannot
activate or bypass these boolean safety branches.

Operator approval is not a self-asserted actor string. Each required role must
provide a digest-bound Ed25519 attestation whose key is active and authorized
for both that role and that exact actor ID in
`.codex-design/product/HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json`. The
checked-in registry is deliberately `unconfigured` and contains no keys; real
operator public keys must be provisioned through the reviewed trust-root process
before any approved packet can pass. An active registry additionally requires a
detached Ed25519 root authorization verified against the public key supplied by
the independently protected
`CHUMMER_HOSTED_BUILD_V002_APPROVAL_ROOT_PUBLIC_KEY_BASE64` runtime setting. The
raw registry file must also match the independently protected
`CHUMMER_HOSTED_BUILD_V002_APPROVAL_REGISTRY_SHA256` value, so restoring an older
but once-valid root-signed registry cannot resurrect revoked keys. The repository
cannot nominate either trust anchor. No private key belongs in this repository.
Evidence is likewise constrained to versioned Hosted Build V002
contracts under `.codex-studio/published/hosted-build-v002/`, source- and
decision-digest binding, and an exact candidate release identity where the proof
is image-specific.

### Operator wire formats and signing workflow

The packet itself is the resolution scaffold. Every selected answer has exactly
`disposition`, `value`, and `rationale`; `disposition` is `selected` or an
explicitly permitted `not_applicable`. A selected value uses exactly one of
these envelopes:

- `identifier`: `kind`, snake-case `value`;
- `identifier_list`: `kind`, non-empty unique snake-case `value` array;
- `identifier_map`: `kind`, non-empty snake-case key/value map;
- `boolean` or `boolean_map`: `kind` plus a boolean or non-empty boolean map;
- `positive_integer`: `kind`, integer `value` from 1 through `2^63-1`;
- `bytes`: the same bounded integer plus `unit: bytes`;
- `duration`: the same bounded integer plus `unit` in `seconds`, `minutes`,
  `hours`, or `days`;
- `quota_limit_map`: `kind` plus a dimension map whose values are exactly
  `{mode: limited, value: <bounded-positive-integer>}` or
  `{mode: unlimited, value: null}`;
- `money`: `kind`, `amountMinor` from 0 through `2^63-1`, and a three-letter
  uppercase `currency`.

The packet's `requiredAnswerFacets` and adjacent `requiredAnswerSchema` are the
self-contained authoritative per-facet type map; the verifier tests them for
exact one-to-one agreement with its executable schema. Required owner roles and
evidence kinds are ordered contracts, not sets.

An active approval-key registry has exactly `contractName`, `contractVersion`,
`status`, `keys`, and `rootAuthorization`. Each key has exactly `keyId`,
`algorithm: ed25519`, `publicKeyBase64`, ordered unique `roles`, ordered unique
`actorIds`, and `status` (`active` or `revoked`). Duplicate key IDs are forbidden
even across revoked entries, and duplicate Ed25519 public-key bytes are forbidden
across all key IDs. `rootAuthorization` has exactly `authority:
external_ed25519_root`, the deterministic root-key fingerprint `rootKeyId`, a
strict UTC `signedAtUtc`, `registryContentSha256`, and `signatureBase64`. Its
signature covers canonical UTF-8 JSON of the authorization object without
`signatureBase64`; the content digest covers canonical JSON of registry name,
version, status, and keys. Canonical JSON normalizes strings to NFC, sorts object
keys, and uses compact separators without a trailing newline.
The root key ID is `root-` followed by the first 32 lowercase hexadecimal
characters of SHA-256 over the 32 raw public-key bytes. An active registry is
trusted only when its raw-file `sha256:` digest equals the separately protected
registry-digest setting as well as its root signature; root signature validity
alone is insufficient currentness evidence.

For an approved decision, the canonical digest material has exactly
`sourceContractSha256`, `id`, `title`, `requiredOwnerRoles`,
`requiredAnswerFacets`, `requiredAnswerSchema`, `requiredEvidenceKinds`,
`requiredEvidenceProofArtifacts`, `decisionStatus`, `accountableOwner`, `answers`,
and `resolutionRationale`.
`decisionContentSha256` is the SHA-256 of that canonical JSON and evidence
receipts bind it. After ordered evidence references are attached,
`decisionSha256` hashes the same exact object plus `evidenceRefs`; every role
approval and attestation binds this second digest. Approval records themselves
are excluded so signatures are not circular.

Each evidence reference has exactly `kind`, allowlisted `repo`, path under
`.codex-studio/published/hosted-build-v002/`, raw-file `sha256`, exact versioned
`contractName`, and nullable `releaseIdentity`. The contract name is derived
without discretion as
`chummer.hosted_build_v002.evidence.<requiredEvidenceKind>.v1`. The referenced
receipt must declare contract version 1, the same evidence kind, `status: pass`,
`reviewRequired: false`, an empty blocker array, matching source and decision
digests, a non-future generation time, and the same release identity. Exact-image
rehearsal evidence must match the packet's candidate release identity; other
evidence must use null.

An evidence receipt cannot be a pass/status wrapper. It also has exactly a
`producer` object (`name`, `version`, `runId`, `invocationSha256`) and an ordered
`proofArtifacts` array. Every proof artifact has exactly `artifactType`,
allowlisted `repo`, a path below
`.codex-studio/published/hosted-build-v002/artifacts/`, raw-file `sha256`, and a
positive verified `byteCount`. The packet repeats the required mapping beside
every row as `requiredEvidenceProofArtifacts`, and the verifier requires exact
agreement. Paths and raw digests must be distinct, so one file cannot satisfy
multiple required artifact types. Required artifact types are:

| Evidence kind | Ordered proof artifact types |
| --- | --- |
| `concurrency_receipt` | `concurrency_test_report`, `database_invariant_report` |
| `ui_receipt` | `ui_journey_report`, `ui_capture_index` |
| `cross_provider_byte_vectors` | `byte_vector_set`, `provider_comparison_report` |
| `lifecycle_tests` | `lifecycle_test_report` |
| `roaming_tests` | `roaming_test_report` |
| `client_telemetry` | `client_age_telemetry_report` |
| `replay_tests` | `operation_replay_test_report` |
| `approved_retention_matrix` | `signed_retention_matrix` |
| `rotation_review` | `identity_rotation_review` |
| `enumeration_review` | `identity_enumeration_review` |
| `split_writer_negative_proof` | `split_writer_test_report`, `epoch_authority_receipt` |
| `restore_drill` | `restore_drill_report`, `deletion_replay_reconciliation_report` |
| `failover_drill` | `failover_drill_report`, `old_writer_fencing_report` |
| `provider_acceptance_receipt` | `provider_acceptance_report` |
| `least_privilege_proof` | `runtime_grant_report`, `direct_mutation_negative_test` |
| `exact_image_rehearsal` | `exact_image_rehearsal_report`, `release_image_digest_report` |
| `capacity_receipt` | `capacity_test_report` |
| `recovery_receipt` | `recovery_drill_report`, `rpo_rto_measurement_report` |

Each approval record has exactly `role`, registered `actorId`, `approvedAtUtc`,
`decisionSha256`, `keyId`, and `attestationRef`. The attestation JSON has contract
`chummer.hosted_build_v002.operator_approval.v1`, version 1, authority
`ed25519_role_registry`, approved/pass fields, the same role/actor/time/source and
decision digests, key ID, and `signatureBase64`. Its signature covers canonical
JSON with only `signatureBase64` removed. The attestation reference binds its raw
file digest and must live under
`.codex-studio/published/hosted-build-v002/approvals/`.
Multi-role decisions require distinct registered actor IDs and distinct Ed25519
keys for every approval; one signer or one private key cannot satisfy multiple
accountable roles in the same decision.

Trust time is bounded as well as ordered. Registry root authorization, evidence,
and approvals must not be future-dated and must be no more than 30 days old at
receipt generation. A generated decision-gate receipt is accepted by the CLI and
flagship consumer only for 24 hours; regeneration does not alter decisions or
approvals, but prevents a stale derived status from lingering. The CLI permits at
most five minutes of forward clock skew.

Operator order of operations is: select and justify every typed facet; attach
content-digest-bound evidence in required order; calculate the evidence-bound
decision digest; obtain every ordered role attestation; attach raw-file-digest
references; provision and externally root-authorize the actor-bound public-key
registry; pin that exact raw registry digest outside the repository; refresh the
source, registry, and packet digests; then run the canonical
verifier. Its validation errors are the doctor output. `--packet` and
`--workspace-root` are diagnostic-only overrides and can never produce a pass;
`--generated-at-utc` rejects values more than five minutes in the future or more
than 24 hours in the past.

Validate and materialize the local decision-gate receipt with:

```sh
python3 scripts/verify_hosted_build_v002_operator_decisions.py \
  --summary-output .codex-studio/published/HOSTED_BUILD_V002_OPERATOR_DECISION_GATE.generated.json
```

The verifier returns `1` for a structurally valid packet that still has
unresolved decisions, `2` for malformed, stale, unauthenticated, or
evidence-unbound input, and `0` only when every required policy input,
authorized role signature, and required evidence artifact is explicit and
digest-bound. It rejects source-contract drift, missing or extra decisions,
implicit or untyped selections, unverified evidence paths, symlink traversal,
noncanonical input provenance, and status/summary claims that disagree with the
decision records.

A passing decision gate freezes operator choices only. It does not authorize
V002 authoring or application, quota enforcement, tombstone deletion, a
production rollout, or any public recovery/retention claim. Those actions stay
blocked on the separate migration, mixed-version, concurrency, restore,
privacy, and exact-release proof suite in this contract.

## Launch decision

The lifecycle/quota slice remains **NO-GO / BLOCKED** while any operator row is
unresolved, V002 lacks mixed-version and rollback proof, quota can be checked
outside the canonical transaction, a lineage high-watermark can disappear,
mutation retries lack idempotency, restored writers lack an external epoch and
deletion replay, UI can misstate unavailable data, or the exact release lacks
current migration/concurrency/restore/privacy receipts.
