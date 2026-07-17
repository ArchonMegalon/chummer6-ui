# Hosted Build owner identity v2 migration

Authenticated Hosted Build storage owners use a domain-separated SHA-256
identifier derived from the provider-issued claim issuer and the exact immutable
subject value. Subject case, Unicode code-point sequence, and issuer are preserved
as identity inputs; leading or trailing whitespace, control characters, invalid
UTF-16, duplicate claims, and conflicting authenticated identities fail closed.
`NameIdentifier` and `sub` corroborate only when both their values and issuers are
ordinally identical.

Only a configured, cryptographically validated JWT authority may supply the tuple. Do not derive it
from email, display name, username, a request header, or a claim whose issuer is
`LOCAL AUTHORITY`. A deployment that composes multiple authentication schemes must
require every authenticated identity to supply and corroborate the same authoritative
issuer-qualified subject tuple before authenticated Build is enabled. An authenticated
identity without a supported stable subject is ambiguity and fails closed.

Hosted Build authentication is anonymous-only when all three of these settings are
absent:

- `CHUMMER_BUILD_AUTHENTICATION_AUTHORITY`: the exact HTTPS issuer and discovery
  authority;
- `CHUMMER_BUILD_AUTHENTICATION_AUDIENCE`: the exact JWT audience; and
- `CHUMMER_BUILD_AUTHENTICATION_SCHEME`: the exact named bearer scheme.

Setting only one or two values fails startup. With all three configured, the app
registers that exact JWT bearer handler, requires HTTPS metadata, signature,
expiration, lifetime, exact issuer, and exact audience validation, and does not save
the bearer token. Authentication runs before the owner boundary. The boundary calls
that exact scheme, accepts only the principal returned by it, rejects failed or
unsupported authorization attempts, rejects ambient authenticated principals, and
then independently checks every authenticated identity's scheme and every supported
stable claim's exact issuer. Do not place bearer tokens in URLs, logs, migration
files, or browser-persisted owner state. Authority reachability and a signed-token
positive/negative integration receipt remain deployment gates.

## Exact v2 identifier

The derivation uses these constants and no Unicode, case, URI, or whitespace
normalization:

- purpose: `Chummer.Blazor.HostedBuildAuthenticatedOwner.v2`
- prefix: `authenticated-v2-`
- text encoding: strict UTF-8 without a byte-order mark
- length encoding: four-byte big-endian, non-negative `Int32` byte length
- digest rendering: lowercase hexadecimal SHA-256

For `I = UTF8(exact issuer)` and `S = UTF8(exact subject)`, the hash input is:

```text
UTF8(purpose)
|| 0x00
|| BE32(I.Length) || I
|| BE32(S.Length) || S
```

The owner ID is:

```text
authenticated-v2- || lowercase_hex(SHA256(hash_input))
```

The explicit lengths make the issuer/subject boundary unambiguous. Migration code
must implement this framing exactly; concatenating strings, normalizing Unicode,
lowercasing, trimming, or hashing a JSON representation produces a different and
invalid owner.

### Golden vector

```text
purpose:        Chummer.Blazor.HostedBuildAuthenticatedOwner.v2
issuer:         https://identity.chummer.test
subject:        Alice@example.com
purpose bytes:  47
issuer bytes:   29 (0x0000001d)
subject bytes:  17 (0x00000011)
input bytes:    102
input hex:      4368756d6d65722e426c617a6f722e486f737465644275696c6441757468656e746963617465644f776e65722e7632000000001d68747470733a2f2f6964656e746974792e6368756d6d65722e7465737400000011416c696365406578616d706c652e636f6d
SHA-256:        777a4e91a40ee433fc820d1fe529caf4c39b2f702ab566dac7517dbe739ae406
owner:          authenticated-v2-777a4e91a40ee433fc820d1fe529caf4c39b2f702ab566dac7517dbe739ae406
```

This standard-shell reproduction must print the same digest:

```sh
purpose='Chummer.Blazor.HostedBuildAuthenticatedOwner.v2'
issuer='https://identity.chummer.test'
subject='Alice@example.com'
{
  printf '%s' "$purpose"
  # One separator byte followed by BE32(29).
  printf '\000\000\000\000\035'
  printf '%s' "$issuer"
  # BE32(17).
  printf '\000\000\000\021'
  printf '%s' "$subject"
} | sha256sum
```

Do not migrate with tooling that fails this vector byte-for-byte.

## Fail-closed migration procedure

This replaces the legacy trim-and-lowercase raw-subject owner ID. There is no
automatic dual-read or legacy fallback because the legacy namespace can merge
different users by case, whitespace, or issuer. Anonymous protected-cookie owners
are unchanged.

Before enabling authenticated Build for an existing deployment:

1. Stop authenticated Build writes and take a restorable, access-controlled backup.
2. Export a provider-verified mapping containing the stored legacy owner, exact
   issuer, exact subject, and the evidence used to associate that tuple with the
   account. Preserve the source strings byte-for-byte.
3. Validate every tuple against the runtime claim constraints and compute its v2
   owner with tooling that passes the golden vector.
4. Produce a dry-run manifest containing each legacy owner, v2 owner, record count,
   stable record identifiers, and a content digest. Do not mutate storage yet.
5. Require a bijection for automatic migration: each legacy owner must map to
   exactly one distinct v2 owner, and each v2 owner must map back to exactly one
   legacy owner. Missing and non-bijective mappings fail the migration.
6. Quarantine every ambiguous bucket. If one legacy owner maps to multiple exact
   `(issuer, subject)` tuples, do not copy, duplicate, merge, or assign the bucket
   wholesale to any target. Attribute each record manually using provider and
   application audit evidence. Records that cannot be attributed remain
   quarantined and inaccessible.
7. Likewise quarantine multiple legacy buckets that converge on one v2 owner until
   an operator verifies each record and resolves duplicates. Never merge them
   automatically.
8. Apply only the approved one-to-one or manually attributed changes in a
   transaction or resumable idempotent batch. The public application must remain
   unable to read legacy owners during the operation.
9. Verify the dry-run manifest against post-migration record IDs, counts, and
   content digests. Test positive access for the intended account and denial across
   case variants, Unicode-sequence variants, issuer variants, unrelated accounts,
   and the legacy owner ID.
10. Keep ambiguous records quarantined and retain the rollback backup until an
    independent review signs off. Do not enable authenticated Build while any
    unreviewed mapping could widen access.

An access failure is preferable to copying one user's records into another user's
owner scope. No migration convenience path may weaken that rule.

### Deterministic read-only planner

Use `scripts/plan-hosted-build-owner-v2-migration.py`; do not replace it with an
ad-hoc spreadsheet or string-concatenation script. It reads evidence exports and
writes one deterministic JSON plan to standard output. It never writes application
storage. The plan contains separate `approvedManifest` and `quarantinedManifest`
sections, stable record ordering and record-set digests, normalized source digests,
the golden vector, and an overall plan digest. Digests use lowercase SHA-256 over
strict UTF-8 canonical JSON (object keys sorted, no insignificant whitespace); the
`planDigest` is computed over the complete plan object before adding the
`planDigest` field itself.

The mapping input is strict UTF-8 JSON:

```json
{
  "schemaVersion": 1,
  "expectedMappingCount": 1,
  "mappings": [
    {
      "legacyOwner": "legacy-owner-id",
      "issuer": "https://identity.example.test",
      "subject": "provider-immutable-subject",
      "evidenceRef": "access-controlled-provider-export:record-42"
    }
  ]
}
```

The inventory is a read-only export of exact record IDs and byte-content digests:

```json
{
  "schemaVersion": 1,
  "expectedRecordCount": 1,
  "records": [
    {
      "recordId": "workspace-123",
      "legacyOwner": "legacy-owner-id",
      "contentDigest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    }
  ]
}
```

Run it without granting storage credentials:

```sh
python3 scripts/plan-hosted-build-owner-v2-migration.py \
  --mapping /secure/export/provider-mapping.json \
  --inventory /secure/export/record-inventory.json \
  > /secure/review/owner-v2-plan.json
```

Exit `0` means the complete inventory is bijective and approved. Exit `1` means a
plan was emitted but missing, one-to-many, or many-to-one mappings were quarantined;
do not migrate any quarantined bucket or enable authenticated Build. Exit `2` means
the evidence itself is invalid and no plan was emitted. Review the approved and
quarantined manifests even after exit `0`, verify the plan digest independently,
and keep the plan access-controlled because issuer/subject tuples are account data.
The expected counts must come from the independently captured export receipt; a
count mismatch is invalid evidence rather than an empty or partially approved plan.

## HMAC key provisioning

Owner invalidation-channel HMAC keys are separate from the identity digest. Each
current and previous key must be an independently generated 32-byte CSPRNG secret,
Base64 encoded for configuration. Length alone is not evidence of entropy: repeated
bytes, counters, passphrases, hashes of operator text, test vectors, and all-zero
values are prohibited even if the runtime accepts their length.

Generate keys in a controlled environment or secret manager. One suitable Linux
example is:

```sh
umask 077
openssl rand -base64 32 > /run/secrets/chummer-owner-hmac-current
```

Provision the value through the deployment secret provider as
`CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64`; do not put it in source control,
container layers, command-line arguments, shell history, logs, migration manifests,
or owner data. Generate the previous and current rotation keys independently and
verify that they differ. Set
`CHUMMER_BUILD_OWNER_CHANNEL_PREVIOUS_HMAC_KEY_BASE64` only for the bounded overlap
period required for existing tabs, then remove it after those tabs drain.
`CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL` is restricted to Development and Test
and must never be used as a production fallback.

## Linux descriptor and filesystem policy

Production Data Protection uses
`CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD`, not a mutable repository path.
The low-level `FromInheritedUnixDirectoryDescriptor` factory borrows its source: it
sets `FD_CLOEXEC`, duplicates the descriptor, and owns and closes only its duplicate.
Its direct caller remains responsible for closing the borrowed source. In contrast,
`ConfigureFromConfiguration` is the service bootstrap ownership-transfer boundary:
immediately after parsing the configured descriptor it becomes the sole owner,
keeps that handle alive while creating the pinned duplicate, and closes the source
exactly once on success and on every later validation/material-construction failure.
The caller must relinquish any competing `SafeFileHandle` before invocation. Closing
only a supervisor or parent process's separate descriptor copy is insufficient. Fail
startup if this ownership handoff cannot be proved, and do not close or reuse the
descriptor number concurrently while startup is in progress.

The source descriptor must survive the one intentional process-exec handoff, so a
launcher must not mark it close-on-exec before that handoff. The borrowed source
must then be closed before the service can launch any later child process. The
boundary creates its retained duplicate with `F_DUPFD_CLOEXEC` and rejects a
duplicate that lacks `FD_CLOEXEC`; deployment smoke checks should confirm in
`/proc/<pid>/fd` and `/proc/<pid>/fdinfo` that the source is gone and the retained
duplicate is close-on-exec. Never intentionally inherit the repository descriptor
into a child process.

Provision secret material before starting the service with this minimum policy:

- run Chummer as a dedicated, non-login service account rather than `root`;
- place the writable Data Protection repository and readable PKCS#12 certificate
  outside the application content root on a persistent, non-shared secret volume;
- make the repository directory service-owned with mode `0700`, and audit generated
  key files as service-owned mode `0600`;
- make the PKCS#12 file and any materialized HMAC secret service-readable only,
  mode `0400` or `0600` as operationally required;
- keep every writable parent directory free of group/world write permission, reject
  symbolic-link path components, and use a controlled service group only when a
  documented rotation process requires it; and
- verify expected UID, GID, file type, mode, descriptor target, and mount before
  launch and after secret rotation.

The application pins file identity, rejects mutable production repository paths,
requires both the repository and certificate to be owned by its effective UID,
requires repository mode `0700`, and accepts certificate mode only `0400` or `0600`.
The deployment remains responsible for controlled parent directories, expected GID,
mount identity, secret rotation, and generated key-file audits. Any application or
deployment permission mismatch must stop launch rather than broaden access.

## Completion evidence

Keep the provider mapping, deterministic planner output, collision/quarantine report, golden
vector receipt, before/after counts and digests, negative-access results, descriptor
inspection, and owner/mode inspection as one access-controlled migration receipt.
The receipt must contain identifiers and digests, not HMAC keys, certificate
passwords, private-key bytes, or unredacted tokens.
