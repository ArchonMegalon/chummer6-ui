# Hosted Build PostgreSQL durability runbook

## Status: NO-GO

Hosted Build is **NO-GO for production launch, horizontal scaling, or durability
claims** until every acceptance gate in this document has a current, local,
secret-free receipt for the exact application and migration image digests being
released.

Provisioning a database, completing a migration, or observing a healthy process
is not durability proof. The launch gate requires a verified backup, a restore
into a fresh target, migration and least-privilege evidence, and the applicable
failover proof.

## Storage boundary

- Hosted Build owns only the dedicated PostgreSQL schema `chummer_build`.
- No Build table, sequence, function, migration ledger, or database object may
  be created in `public` or in another product's schema.
- The schema owner/migrator, application runtime, maintenance, and backup
  identities are separate. The application identity must not own the database
  or schema, inherit the migration identity, create schema objects, change the
  migration ledger, truncate tables, or bypass row-security controls where
  they are used.
- Cross-schema writes and foreign keys require a separately reviewed contract.
  They are forbidden by default.
- Application startup must never run migrations. A dedicated, finite
  release-job migration completes and validates the schema before any new
  application instance becomes ready.
- `provider=file` is a single-instance development or recovery posture only.
  It is not supported for multiple writers, horizontal scaling, automatic
  failover, or hosted HA. A deployment using `provider=file` must run exactly
  one Build application instance and must not claim PostgreSQL durability.

## Connection, secret, and TLS contract

- Database passwords, client keys, and complete connection strings are supplied
  through read-only secret files. They must not be placed in Compose
  `environment`, command-line arguments, image layers, source control, logs, or
  evidence receipts.
- Secret files are mounted only into the job or service that needs them, owned
  by the runtime identity, unreadable by group/other, and absent from backup
  payloads. Migration credentials are never mounted into the application.
- The application rejects reparse/symlink final files, group/other-accessible
  secret files, and group/other-writable immediate secret directories. It opens
  the final file once and binds type, mode, size, and bounded strict-UTF-8 reads
  to that handle. Portable .NET does not expose descriptor-relative
  `O_NOFOLLOW` or Unix UID ownership validation, so the deployment must also
  guarantee a trusted, non-writable mount ancestry and correct file owner; those
  properties remain an infrastructure acceptance check rather than an
  application claim.
- PostgreSQL connections require `SSL Mode=VerifyFull`, CA validation, and
  hostname verification. `VerifyCA` is not sufficient. Cleartext connections,
  disabled certificate validation, and trust-all server certificate modes are
  release blockers.
- The runtime role receives only the enumerated DML privileges needed by Build.
  The release job must verify privileges across every table, sequence, function,
  schema, role membership, database privilege, `PUBLIC` grant, and default
  privilege for future migrations.
- Connection attempts, commands, advisory-lock waits, and release jobs have
  explicit deadlines. Logs may include a public endpoint label and SQLSTATE,
  never a password, token, certificate, connection string, or user data.
- Production sets both `CHUMMER_BUILD_POSTGRES_MAX_POOL_SIZE` and
  `CHUMMER_BUILD_POSTGRES_AGGREGATE_CONNECTION_BUDGET` explicitly. The service
  fails closed unless `CHUMMER_BUILD_EXPECTED_REPLICA_COUNT` multiplied by the
  per-instance pool fits within that aggregate runtime allocation. The
  aggregate value is the application allocation after operator-reserved
  migration, backup, monitoring, and emergency headroom; it is not the server's
  raw `max_connections` value.
- The provider forces pooling on, a zero minimum pool, and the validated maximum
  pool even if the secret connection string requests broader settings. Npgsql
  read-write session targeting is forced for multi-host connection strings;
  single-host endpoints still fail readiness when PostgreSQL reports recovery
  or transaction-read-only posture. Neither check is a substitute for
  provider-side fencing or split-brain prevention.

## Release procedure

1. Freeze schema-changing work and bind the release to immutable application
   and migration image digests.
2. Validate the selected database target, PostgreSQL major, TLS peer, dedicated
   `chummer_build` schema, role separation, disk headroom, and current migration
   catalog checksums.
3. Produce and verify a fresh pre-migration backup. Record its SHA-256, format,
   PostgreSQL major, source identity, migration version, and completion time in
   a secret-free receipt.
4. Run the one-shot migration job with the schema-owner identity. It must hold a
   bounded advisory lock, apply forward-only transactional migrations, and
   fail on unknown versions, missing versions, or checksum drift.
5. Run schema-object and least-privilege validation with independent checks.
   A successful migration process exit alone is insufficient.
6. Start the new application image with the runtime identity. Liveness remains
   process-only; side-effect-free readiness must fail closed on database
   unavailability, wrong schema version or physical object shape, invalid
   runtime privileges, or a non-writable primary. Health polling must not
   create durable proof rows or continuous database write churn.
7. Run a representative create/read/update transaction, idempotent replay, and
   application-level semantic check. Delete only disposable proof records.
8. Admit traffic only after every required receipt is complete. Continue backup,
   replica-lag, capacity, error-rate, and restore-age monitoring.

## Acceptance gates

### Migration gate

Run against a disposable local PostgreSQL instance of the production major:

- migrate an empty database to the current version and validate all expected
  tables, sequences, indexes, constraints, triggers, functions, and checksums;
- rerun idempotently and run two migrators concurrently to prove serialization;
- prove checksum tampering, an unknown version, a missing object, and an expired
  lock deadline fail closed;
- prove the runtime identity cannot perform DDL or prohibited destructive work;
- prove old-application/new-schema and new-application/old-schema behavior for
  the declared compatibility window.

### Backup gate

- Back up a seeded `chummer_build` schema with a supported PostgreSQL-native
  format and a canonical manifest.
- Encrypt the backup outside the database credential boundary and verify both
  archive and manifest digests.
- Prove scheduling, monitoring, failure alerting, retention selection, and an
  independent copy outside the primary failure domain.
- Prove a corrupt, truncated, incomplete, or wrong-key backup is rejected.

### Restore gate

- Restore the backup into a newly created empty database/volume, never over the
  source.
- Run migration-catalog, object, privilege, row-count, and semantic-digest
  validation; then start the exact application image and execute representative
  Build reads, writes, and idempotent retries.
- Record measured backup age, restore duration, achieved RPO/RTO, restored
  migration version, and fresh target identifier.

### Disposable local recovery drill

Run the bounded provider-neutral recovery smoke proof with:

```bash
scripts/test-hosted-build-postgres-recovery.sh
```

Set `CHUMMER_BUILD_POSTGRES_RECOVERY_IMAGE` to test another immutable-compatible
PostgreSQL image and `CHUMMER_BUILD_POSTGRES_RECOVERY_RECEIPT` to select the
receipt destination. The default receipt is written below `artifacts/` and the
script records the container's immutable image ID as well as the requested tag.
The lane requires GNU `timeout` (named `timeout` or `gtimeout`) and defaults to
a 120-second per-operation limit plus a 600-second whole-drill watchdog. The
bounded overrides are
`CHUMMER_BUILD_POSTGRES_RECOVERY_OPERATION_TIMEOUT_SECONDS` and
`CHUMMER_BUILD_POSTGRES_RECOVERY_OVERALL_TIMEOUT_SECONDS`.

The script creates a network-isolated disposable PostgreSQL container without a
database password, materializes the checked-in V001 schema and migration ledger,
and seeds two synthetic owners with the same workspace ID. Their documents use
the application's full persisted-document SHA-256 contract and different
content/saved revisions. It then:

1. validates the source schema, ledger checksum, owner partition, document
   checksums, and revisions;
2. writes a PostgreSQL custom-format backup of the disposable dedicated
   database, including the schema's ACL/default-ACL posture, and hashes both the
   archive and its table-of-contents manifest;
3. proves a deliberately truncated copy is rejected;
4. drops the source database to simulate destructive loss;
5. restores into a database whose nonexistence was checked immediately before
   creation; and
6. byte-compares canonical row and schema/ACL snapshots, rechecks the ledger,
   checksums, revisions, and same-ID owner isolation, then always removes the
   container and temporary backup material.

The atomic JSON receipt contains synthetic metadata and digests only. It never
contains a password, connection string, raw owner ID, or document payload. A
passing receipt establishes only that this checkout can perform a native dump,
detect one truncated-archive failure mode, survive complete loss of its
disposable source database, and restore the tested records and schema into a
fresh local target. The receipt deliberately keeps
`productionLaunchGateSatisfied=false`.

This drill does **not** establish production point-in-time recovery, WAL
archiving, encrypted backup handling, an independent failure-domain copy,
retention/deletion behavior, managed-provider failover or fencing, production
RPO/RTO, KMS ownership/rotation/revocation, alert delivery, or application-image
compatibility after restore. Those remain separate operator decisions and
provider-specific acceptance gates below. The container's trust authentication
is acceptable only because it has no published port and uses Docker's
network-none isolation; it must never be copied into a deployed topology.

### HA and failover gate

Required before more than one application instance or any HA claim:

- use a provider-neutral primary/standby test with replication and fencing;
- commit at controlled points, terminate the primary, promote the standby, and
  verify every acknowledged write permitted by the chosen RPO;
- prove stale-primary writes are fenced, retries are idempotent, replica lag is
  bounded, and readiness remains closed until promotion and schema checks pass;
- measure recovery time rather than inferring it from provider marketing.

### Rollback gate

- Prefer an expand/contract migration compatible with the previous application
  image. Prove that compatibility before release.
- Do not run an improvised down migration. If the previous image is not schema
  compatible, stop writes and restore the verified pre-migration backup into a
  fresh target, validate it, point the previous image at that target, and admit
  traffic only after its functional checks pass.
- Preserve failed target metadata and logs for diagnosis without copying
  secrets or personal data into receipts.

## Evidence receipt

Each gate emits canonical JSON containing the gate version, UTC timestamps,
application and migration image digests, PostgreSQL major, migration catalog
checksums, backup SHA-256, fresh restore target identifier, negative-case
results, measured RPO/RTO, and final status. Receipts stay local and
provider-neutral. Secret scanning of the receipt and logs is itself mandatory.

## Unresolved operator decisions

These decisions require explicit operator values; defaults must not be inferred:

1. **Region:** primary region, standby/failover region, data-residency boundary,
   and maximum acceptable application-to-database latency.
2. **Provider:** managed or self-hosted PostgreSQL provider, production major,
   topology, support/SLA, portability, and exit/export procedure.
3. **Budget:** monthly steady-state ceiling, backup/replica/egress allowance,
   alert threshold, and authorized overage path during recovery.
4. **RPO:** maximum tolerable committed-data loss, expressed as a number and
   time unit, for routine failure and regional disaster.
5. **RTO:** maximum time to restore service, expressed as a number and time
   unit, for database failover and full backup restore.
6. **Retention:** backup frequency, daily/weekly/monthly retention counts, WAL
   or point-in-time recovery window, deletion hold, and restore-drill cadence.
7. **KMS:** encryption/KMS provider, key region, key owner, access approvers,
   rotation interval, revocation process, escrow/recovery policy, and behavior
   when the KMS is unavailable.

## Launch decision

The decision remains **NO-GO** while any operator decision is unresolved, any
gate lacks a current passing receipt, the restore target is not demonstrably
fresh, credentials appear in environment/logs/receipts, TLS verification is
weakened, `provider=file` is proposed for multiple instances, or rollback has
not been rehearsed for the exact release pair.
