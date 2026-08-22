# Build Ghost private packet access lifecycle

Build Ghost packet-access grants are private, owner/workspace/revision-bound, single-use capabilities. They carry no provider authority and do not add a public `/explain` route. Deployment remains disabled unless the existing private-tool configuration is complete.

## Lifecycle

- Issue creates a random 256-bit key with a maximum caller-facing lifetime of 300 seconds. The raw key is returned once and is never written to disk. Its SHA-256 digest names a keyed-MAC-protected pending envelope.
- Consume atomically moves the pending record into a terminal claim before reading it. A valid unexpired claim is returned once; replay cannot recover it.
- Direct grant revocation uses the same atomic claim path. A concurrent consume and revoke therefore has exactly one terminal winner.
- Workspace close persists a scope-digest revocation marker through the closed revision, then claims matching pending grants. The marker is the fail-closed authority point: interrupted cleanup, restart, or a late issue cannot make a covered grant usable.
- Expiry is finalized during explicit cleanup and opportunistically during issue/revoke. An expired key, revoked key, replayed key, unknown key, and malformed key all resolve through the same absent binding and the same private endpoint `410` response.
- A process interrupted after an atomic claim recovers and finalizes that claim before accepting another operation.

The store uses a host-local exclusive operation lock in addition to atomic same-filesystem renames. This gives concurrent consume/revoke exactly one terminal winner for a singleton private store root shared by cooperating Presentation processes.

## Revocation scope

Revocation compares the exact owner ID, workspace ID, and `WorkspaceRevision <= ThroughRevision`. Marker filenames and content contain only domain-separated HMAC-SHA256 references for owner/workspace scope. A marker for one owner cannot revoke another owner's identically named workspace, and a marker for one workspace cannot affect another workspace.

Workspace IDs are terminal after close and must not be reused. Revocation markers are access-control state, not audit retention, and are not pruned implicitly.

## Audit receipts

Each successfully issued grant and each terminal consumed, expired, or revoked grant produces an atomic JSON receipt under `audit/` with schema `chummer.build_ghost.packet_access_audit.v2`.

Receipts contain only:

- the low-cardinality lifecycle event;
- timestamps and workspace revision;
- a one-way SHA-256 reference for the high-entropy grant key;
- domain-separated HMAC-SHA256 references for owner scope, workspace, packet, source, runtime fingerprint, locale, request kind, and audience;
- a keyed event ID and receipt MAC.

They never contain the raw grant key or raw scope/account identifiers. Low-entropy values cannot be checked with an offline dictionary without the private state-authentication key. Existing receipts must match the keyed identity and MAC or the store fails closed. Retention is bounded by `MaximumAuditRecords` (default `2048`); the oldest receipts are removed only after a new durable receipt exists. No code path implicitly removes revocation markers.

The 32-byte state-authentication key is derived in memory from the already-required service token and contract digest with a dedicated v2 domain. It is never serialized or logged. A keyed `state-authority.v2.json` marker binds the store to that key context. A wrong token/contract, a recomputed plain SHA-256 value, or pre-v2 unkeyed state fails closed before state reuse.

## Failure behavior

- Missing or invalid store configuration keeps private-tool access unavailable.
- A malformed or MAC-invalid pending envelope, revocation marker, authority marker, or audit receipt fails issue/consume/recovery closed.
- A grant is not returned from issue unless its issued receipt is durable.
- A terminal claim is not released or returned unless its terminal receipt is durable.
- Workspace close reports failure if configured grant revocation cannot be persisted; the workspace itself remains closed and therefore cannot mint a fresh grounded packet.
- Once workspace close commits, revocation completion is not canceled by client disconnect.

These guarantees are local packet-access guarantees. They do not activate Tough Tongue or another provider, mutate account entitlements, publish ingress, or authorize any remote model transport.
