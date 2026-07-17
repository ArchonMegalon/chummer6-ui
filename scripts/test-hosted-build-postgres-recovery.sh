#!/usr/bin/env bash
set -euo pipefail

export LC_ALL=C
umask 077

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
migration_path="$repo_root/Chummer.Workspaces.Postgres/Migrations/V001__chummer_build_workspace.sql"
postgres_image="${CHUMMER_BUILD_POSTGRES_RECOVERY_IMAGE:-postgres:17-alpine}"
run_stamp="$(date -u +%Y%m%d%H%M%S)"
run_id="${run_stamp}-$$"
container_name="chummer-build-postgres-recovery-$$"
source_database="chummer_build_source_${run_stamp}_$$"
corrupt_probe_database="chummer_build_corrupt_${run_stamp}_$$"
restore_database="chummer_build_restore_${run_stamp}_$$"
receipt_path="${CHUMMER_BUILD_POSTGRES_RECOVERY_RECEIPT:-$repo_root/artifacts/hosted-build-postgres-recovery-${run_id}.json}"
operation_timeout_seconds="${CHUMMER_BUILD_POSTGRES_RECOVERY_OPERATION_TIMEOUT_SECONDS:-120}"
overall_timeout_seconds="${CHUMMER_BUILD_POSTGRES_RECOVERY_OVERALL_TIMEOUT_SECONDS:-600}"
work_dir=""
container_started=false
timeout_command=""
watchdog_pid=""

current_stage="preflight"
started_at_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
completed_at_utc=""
postgres_image_id=""
postgres_version_number=""
migration_checksum=""
backup_sha256=""
backup_toc_sha256=""
backup_bytes=0
backup_completed_at_utc=""
backup_completed_epoch=0
backup_age_seconds=0
restore_duration_seconds=0
measured_recovery_seconds=0
row_snapshot_sha256=""
schema_snapshot_sha256=""
restored_row_count=0
truncated_archive_rejected=false
source_database_dropped=false
fresh_restore_target=false
ledger_verified=false
data_and_revisions_verified=false
owner_isolation_verified=false
document_checksums_verified=false
schema_verified=false

fail() {
  printf 'Hosted Build PostgreSQL recovery proof failed during %s.\n' "$current_stage" >&2
  exit 1
}

run_bounded_for() {
  local timeout_seconds="$1"
  shift
  "$timeout_command" \
    --signal=TERM \
    --kill-after=5s \
    "${timeout_seconds}s" \
    "$@"
}

run_bounded() {
  run_bounded_for "$operation_timeout_seconds" "$@"
}

write_receipt() {
  local status="$1"
  local exit_code="$2"
  local receipt_dir
  local receipt_tmp

  receipt_dir="$(dirname -- "$receipt_path")"
  mkdir -p -- "$receipt_dir"
  receipt_tmp="${receipt_path}.tmp.$$"

  printf '{\n  "schemaVersion": 1,\n  "gate": "hosted_build_postgres_disposable_recovery",\n  "scope": "local_disposable_only",\n  "status": "%s",\n  "exitCode": %s,\n  "lastStage": "%s",\n  "startedAtUtc": "%s",\n  "completedAtUtc": "%s",\n  "postgres": {\n    "requestedImage": "%s",\n    "immutableImageId": "%s",\n    "serverVersionNumber": "%s"\n  },\n  "migration": {\n    "version": 1,\n    "name": "V001__chummer_build_workspace.sql",\n    "checksumSha256": "%s",\n    "ledgerVerified": %s\n  },\n  "backup": {\n    "format": "postgresql_custom",\n    "sha256": "%s",\n    "archiveTocSha256": "%s",\n    "bytes": %s,\n    "completedAtUtc": "%s",\n    "encrypted": false,\n    "retained": false,\n    "truncatedArchiveRejected": %s\n  },\n  "lossSimulation": {\n    "sourceDatabaseDropped": %s,\n    "writesAfterBackup": 0,\n    "simulatedDataLossSeconds": 0\n  },\n  "restore": {\n    "freshTargetIdentifier": "%s",\n    "freshTargetVerified": %s,\n    "backupAgeSecondsAtRestore": %s,\n    "restoreDurationSeconds": %s,\n    "measuredRecoverySeconds": %s,\n    "rowCount": %s,\n    "rowSnapshotSha256": "%s",\n    "schemaSnapshotSha256": "%s",\n    "dataAndRevisionsVerified": %s,\n    "ownerIsolationVerified": %s,\n    "documentChecksumsVerified": %s,\n    "schemaVerified": %s\n  },\n  "claims": {\n    "productionPitrEstablished": false,\n    "productionRpoEstablished": false,\n    "productionRtoEstablished": false,\n    "managedFailoverEstablished": false,\n    "backupEncryptionEstablished": false,\n    "kmsPolicyEstablished": false,\n    "retentionPolicyEstablished": false\n  },\n  "productionLaunchGateSatisfied": false\n}\n' \
    "$status" \
    "$exit_code" \
    "$current_stage" \
    "$started_at_utc" \
    "$completed_at_utc" \
    "$postgres_image" \
    "$postgres_image_id" \
    "$postgres_version_number" \
    "$migration_checksum" \
    "$ledger_verified" \
    "$backup_sha256" \
    "$backup_toc_sha256" \
    "$backup_bytes" \
    "$backup_completed_at_utc" \
    "$truncated_archive_rejected" \
    "$source_database_dropped" \
    "$restore_database" \
    "$fresh_restore_target" \
    "$backup_age_seconds" \
    "$restore_duration_seconds" \
    "$measured_recovery_seconds" \
    "$restored_row_count" \
    "$row_snapshot_sha256" \
    "$schema_snapshot_sha256" \
    "$data_and_revisions_verified" \
    "$owner_isolation_verified" \
    "$document_checksums_verified" \
    "$schema_verified" > "$receipt_tmp"

  mv -f -- "$receipt_tmp" "$receipt_path"
}

cleanup() {
  local cleanup_failed=false

  if [[ -n "$watchdog_pid" ]]; then
    kill "$watchdog_pid" >/dev/null 2>&1 || true
    wait "$watchdog_pid" >/dev/null 2>&1 || true
    watchdog_pid=""
  fi

  if [[ "$container_started" == true ]]; then
    if ! run_bounded_for 15 \
        docker rm --force "$container_name" >/dev/null 2>&1; then
      cleanup_failed=true
    fi
    container_started=false
  fi

  if [[ -n "$work_dir" && -d "$work_dir" ]]; then
    if ! rm -rf -- "$work_dir"; then
      cleanup_failed=true
    fi
  fi

  [[ "$cleanup_failed" == false ]]
}

on_exit() {
  local exit_code=$?
  local status="failed"

  trap - EXIT INT TERM
  completed_at_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if [[ "$exit_code" -eq 0 ]]; then
    status="passed"
  fi

  if ! cleanup; then
    if [[ "$exit_code" -eq 0 ]]; then
      exit_code=1
      status="failed"
      current_stage="cleanup"
    fi
  fi

  if ! write_receipt "$status" "$exit_code"; then
    printf 'Could not write the Hosted Build PostgreSQL recovery receipt.\n' >&2
    exit_code=1
  elif [[ "$exit_code" -eq 0 ]]; then
    printf 'Hosted Build PostgreSQL disposable recovery proof passed.\n'
    printf 'Receipt: %s\n' "$receipt_path"
  else
    printf 'Failure receipt: %s\n' "$receipt_path" >&2
  fi

  exit "$exit_code"
}

trap on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

for required_command in docker sha256sum sed awk cmp dd wc mktemp date dirname mkdir mv rm sleep; do
  command -v "$required_command" >/dev/null 2>&1 || fail
done

if command -v timeout >/dev/null 2>&1; then
  timeout_command="timeout"
elif command -v gtimeout >/dev/null 2>&1; then
  timeout_command="gtimeout"
else
  fail
fi

if [[ ! "$operation_timeout_seconds" =~ ^[0-9]+$ \
      || "$operation_timeout_seconds" -lt 10 \
      || "$operation_timeout_seconds" -gt 600 \
      || ! "$overall_timeout_seconds" =~ ^[0-9]+$ \
      || "$overall_timeout_seconds" -lt 60 \
      || "$overall_timeout_seconds" -gt 3600 ]]; then
  fail
fi

if [[ ! "$postgres_image" =~ ^[A-Za-z0-9._/:@-]+$ ]]; then
  postgres_image="invalid"
  fail
fi
if [[ ! -f "$migration_path" || -L "$migration_path" ]]; then
  fail
fi
if [[ -L "$receipt_path" ]]; then
  fail
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-build-postgres-recovery.XXXXXX")"
backup_path="$work_dir/chummer-build.dump"
truncated_backup_path="$work_dir/chummer-build.truncated.dump"
archive_toc_path="$work_dir/chummer-build.archive.toc"
corrupt_restore_log="$work_dir/truncated-restore.stderr"
source_rows_path="$work_dir/source-rows.tsv"
restored_rows_path="$work_dir/restored-rows.tsv"
source_schema_path="$work_dir/source-schema.tsv"
restored_schema_path="$work_dir/restored-schema.tsv"

(
  sleep "$overall_timeout_seconds"
  kill -TERM "$$"
) &
watchdog_pid=$!

migration_checksum="$(sed 's/\r$//' "$migration_path" | sha256sum | awk '{print $1}')"
if [[ ! "$migration_checksum" =~ ^[0-9a-f]{64}$ ]]; then
  fail
fi

current_stage="start_disposable_postgres"
container_started=true
run_bounded docker run \
  --detach \
  --rm \
  --name "$container_name" \
  --network none \
  --env POSTGRES_HOST_AUTH_METHOD=trust \
  "$postgres_image" >/dev/null

for ((attempt = 1; attempt <= 30; attempt++)); do
  if run_bounded_for 5 docker exec "$container_name" \
      pg_isready --timeout=2 --username postgres \
      --dbname postgres >/dev/null 2>&1; then
    break
  fi
  if [[ "$attempt" == "30" ]]; then
    fail
  fi
  sleep 1
done

postgres_image_id="$(run_bounded docker inspect --format '{{.Image}}' "$container_name")"
postgres_version_number="$(run_bounded docker exec "$container_name" \
  psql -X --set=ON_ERROR_STOP=1 --tuples-only --no-align \
  --username postgres --dbname postgres --command 'SHOW server_version_num')"
if [[ ! "$postgres_image_id" =~ ^sha256:[0-9a-f]{64}$ \
      || ! "$postgres_version_number" =~ ^[0-9]+$ ]]; then
  fail
fi

psql_value() {
  local database="$1"
  local sql="$2"
  run_bounded docker exec "$container_name" \
    psql -X --set=ON_ERROR_STOP=1 --tuples-only --no-align \
    --username postgres --dbname "$database" --command "$sql"
}

write_row_snapshot() {
  local database="$1"
  local output_path="$2"
  run_bounded docker exec -i "$container_name" \
    psql -X --set=ON_ERROR_STOP=1 --tuples-only --no-align \
    --username postgres --dbname "$database" > "$output_path" <<'SQL'
SELECT concat_ws(
    E'\t',
    encode(owner_key, 'hex'),
    workspace_id,
    document_json::text,
    encode(document_sha256, 'hex'),
    content_revision::text,
    saved_revision::text,
    to_char(
        updated_at_utc AT TIME ZONE 'UTC',
        'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'))
FROM chummer_build.workspaces
ORDER BY encode(owner_key, 'hex'), workspace_id;
SQL
}

write_schema_snapshot() {
  local database="$1"
  local output_path="$2"
  run_bounded docker exec -i "$container_name" \
    psql -X --set=ON_ERROR_STOP=1 --tuples-only --no-align \
    --username postgres --dbname "$database" > "$output_path" <<'SQL'
WITH schema_objects(line) AS (
    SELECT concat_ws(
        E'\t',
        'column',
        table_name,
        ordinal_position::text,
        column_name,
        udt_name,
        is_nullable,
        COALESCE(column_default, ''))
    FROM information_schema.columns
    WHERE table_schema = 'chummer_build'

    UNION ALL

    SELECT concat_ws(
        E'\t',
        'constraint',
        relation.relname,
        constraint_entry.conname,
        pg_get_constraintdef(constraint_entry.oid, true))
    FROM pg_constraint AS constraint_entry
    INNER JOIN pg_class AS relation
        ON relation.oid = constraint_entry.conrelid
    INNER JOIN pg_namespace AS namespace
        ON namespace.oid = relation.relnamespace
    WHERE namespace.nspname = 'chummer_build'
      AND relation.relname IN ('schema_migrations', 'workspaces')

    UNION ALL

    SELECT concat_ws(
        E'\t',
        'index',
        index_entry.indexname,
        index_entry.indexdef)
    FROM pg_indexes AS index_entry
    WHERE index_entry.schemaname = 'chummer_build'

    UNION ALL

    SELECT concat_ws(
        E'\t',
        'default_acl',
        COALESCE(namespace.nspname, 'global'),
        default_acl.defaclobjtype::text,
        array_to_string(default_acl.defaclacl, ','))
    FROM pg_default_acl AS default_acl
    LEFT JOIN pg_namespace AS namespace
        ON namespace.oid = default_acl.defaclnamespace
    WHERE default_acl.defaclrole = (
        SELECT schema_entry.nspowner
        FROM pg_namespace AS schema_entry
        WHERE schema_entry.nspname = 'chummer_build')
      AND (
          default_acl.defaclnamespace = 0
          OR namespace.nspname = 'chummer_build')
)
SELECT line
FROM schema_objects
ORDER BY line;
SQL
}

validate_schema_and_ledger() {
  local database="$1"
  local column_contract
  local object_contract
  local default_acl_contract
  local ledger_contract

  object_contract="$(psql_value "$database" "
    SELECT concat_ws(
        '|',
        CASE WHEN to_regclass('chummer_build.schema_migrations') IS NOT NULL
            THEN '1' ELSE '0' END,
        CASE WHEN to_regclass('chummer_build.workspaces') IS NOT NULL
            THEN '1' ELSE '0' END,
        CASE WHEN to_regclass('chummer_build.ix_chummer_build_workspaces_owner_updated') IS NOT NULL
            THEN '1' ELSE '0' END,
        (SELECT string_agg(table_name, ',' ORDER BY table_name)
         FROM information_schema.tables
         WHERE table_schema = 'chummer_build'
           AND table_type = 'BASE TABLE'),
        (SELECT count(*)
         FROM information_schema.columns
         WHERE table_schema = 'chummer_build'
           AND table_name = 'schema_migrations'),
        (SELECT count(*)
         FROM information_schema.columns
         WHERE table_schema = 'chummer_build'
           AND table_name = 'workspaces'),
        (SELECT count(*)
         FROM pg_constraint AS constraint_entry
         INNER JOIN pg_class AS relation
             ON relation.oid = constraint_entry.conrelid
         INNER JOIN pg_namespace AS namespace
             ON namespace.oid = relation.relnamespace
         WHERE namespace.nspname = 'chummer_build'
           AND relation.relname IN ('schema_migrations', 'workspaces')),
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_class AS relation
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'chummer_build'
              AND relation.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
              AND (
                  relation.relkind <> 'r'
                  OR relation.relname NOT IN ('schema_migrations', 'workspaces')))
            THEN '1' ELSE '0' END,
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_proc AS procedure
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = procedure.pronamespace
            WHERE namespace.nspname = 'chummer_build')
            THEN '1' ELSE '0' END,
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_trigger AS trigger
            INNER JOIN pg_class AS relation
                ON relation.oid = trigger.tgrelid
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'chummer_build'
              AND NOT trigger.tgisinternal)
            THEN '1' ELSE '0' END,
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_class AS relation
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'chummer_build'
              AND relation.relkind IN ('r', 'p')
              AND (relation.relrowsecurity OR relation.relforcerowsecurity))
            THEN '1' ELSE '0' END,
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_namespace AS namespace
            CROSS JOIN LATERAL aclexplode(
                COALESCE(namespace.nspacl, acldefault('n', namespace.nspowner))) AS acl
            WHERE namespace.nspname = 'chummer_build'
              AND acl.grantee = 0)
            THEN '1' ELSE '0' END,
        CASE WHEN NOT EXISTS (
            SELECT 1
            FROM pg_class AS relation
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            CROSS JOIN LATERAL aclexplode(
                COALESCE(relation.relacl, acldefault('r', relation.relowner))) AS acl
            WHERE namespace.nspname = 'chummer_build'
              AND relation.relkind IN ('r', 'p')
              AND acl.grantee = 0)
            THEN '1' ELSE '0' END); ")"
  [[ "$object_contract" == "1|1|1|schema_migrations,workspaces|4|7|12|1|1|1|1|1|1" ]] || return 1

  column_contract="$(psql_value "$database" "
    SELECT string_agg(
        concat_ws(
            ':',
            table_name,
            ordinal_position::text,
            column_name,
            udt_name,
            is_nullable,
            CASE WHEN column_default IS NULL THEN '0' ELSE '1' END),
        ',' ORDER BY table_name, ordinal_position)
    FROM information_schema.columns
    WHERE table_schema = 'chummer_build'
      AND table_name IN ('schema_migrations', 'workspaces');")"
  [[ "$column_contract" == "schema_migrations:1:version:int4:NO:0,schema_migrations:2:name:text:NO:0,schema_migrations:3:checksum_sha256:text:NO:0,schema_migrations:4:applied_at_utc:timestamptz:NO:1,workspaces:1:owner_key:bytea:NO:0,workspaces:2:workspace_id:text:NO:0,workspaces:3:document_json:jsonb:NO:0,workspaces:4:document_sha256:bytea:NO:0,workspaces:5:content_revision:int8:NO:0,workspaces:6:saved_revision:int8:NO:0,workspaces:7:updated_at_utc:timestamptz:NO:1" ]] || return 1

  default_acl_contract="$(psql_value "$database" "
    WITH schema_owner AS (
        SELECT oid AS schema_oid, nspowner AS owner_oid
        FROM pg_namespace
        WHERE nspname = 'chummer_build'
    ), default_object_types(object_type) AS (
        VALUES ('r'::\"char\"), ('S'::\"char\"), ('f'::\"char\")
    ), effective_global_defaults AS (
        SELECT
            object_type,
            COALESCE(
                default_acl.defaclacl,
                acldefault(object_type, schema_owner.owner_oid)) AS acl
        FROM schema_owner
        CROSS JOIN default_object_types
        LEFT JOIN pg_default_acl AS default_acl
          ON default_acl.defaclnamespace = 0
         AND default_acl.defaclrole = schema_owner.owner_oid
         AND default_acl.defaclobjtype = object_type
    )
    SELECT CASE WHEN NOT EXISTS (
        SELECT 1
        FROM effective_global_defaults
        CROSS JOIN LATERAL aclexplode(effective_global_defaults.acl) AS acl
        WHERE acl.grantee = 0

        UNION ALL

        SELECT 1
        FROM pg_default_acl AS default_acl
        INNER JOIN schema_owner
          ON schema_owner.schema_oid = default_acl.defaclnamespace
         AND schema_owner.owner_oid = default_acl.defaclrole
        CROSS JOIN LATERAL aclexplode(default_acl.defaclacl) AS acl
        WHERE acl.grantee = 0)
      THEN '1' ELSE '0' END;")"
  [[ "$default_acl_contract" == "1" ]] || return 1

  ledger_contract="$(psql_value "$database" "
    SELECT concat_ws('|', version::text, name, checksum_sha256)
    FROM chummer_build.schema_migrations
    ORDER BY version;")"
  [[ "$ledger_contract" == "1|V001__chummer_build_workspace.sql|${migration_checksum}" ]]
}

validate_rows() {
  local database="$1"
  local owner_contract
  local row_a_contract
  local row_b_contract

  owner_contract="$(psql_value "$database" "
    SELECT concat_ws(
        '|',
        count(*)::text,
        count(DISTINCT encode(owner_key, 'hex'))::text,
        count(DISTINCT workspace_id)::text,
        count(DISTINCT encode(document_sha256, 'hex'))::text,
        count(*) FILTER (WHERE octet_length(owner_key) <> 32)::text)
    FROM chummer_build.workspaces;")"
  [[ "$owner_contract" == "2|2|1|2|0" ]] || return 1

  row_a_contract="$(psql_value "$database" "
    SELECT concat_ws(
        '|',
        encode(document_sha256, 'hex'),
        content_revision::text,
        saved_revision::text,
        document_json ->> 'storageSchemaVersion',
        document_json ->> 'rulesetId',
        document_json ->> 'workspaceSchemaVersion',
        document_json ->> 'payloadKind',
        document_json ->> 'payload',
        document_json ->> 'format')
    FROM chummer_build.workspaces
    WHERE owner_key = decode('${owner_a_hash}', 'hex')
      AND workspace_id = 'shared-recovery-proof';")"
  [[ "$row_a_contract" == "${document_a_sha256}|3|2|1|sr5|1|native-xml|recovery-alpha|NativeXml" ]] || return 1

  row_b_contract="$(psql_value "$database" "
    SELECT concat_ws(
        '|',
        encode(document_sha256, 'hex'),
        content_revision::text,
        saved_revision::text,
        document_json ->> 'storageSchemaVersion',
        document_json ->> 'rulesetId',
        document_json ->> 'workspaceSchemaVersion',
        document_json ->> 'payloadKind',
        document_json ->> 'payload',
        document_json ->> 'format')
    FROM chummer_build.workspaces
    WHERE owner_key = decode('${owner_b_hash}', 'hex')
      AND workspace_id = 'shared-recovery-proof';")"
  [[ "$row_b_contract" == "${document_b_sha256}|1|0|1|sr5|1|native-xml|recovery-beta|NativeXml" ]]
}

current_stage="migrate_source"
run_bounded docker exec "$container_name" \
  createdb --username postgres "$source_database"

run_bounded docker exec -i "$container_name" \
  psql -X --set=ON_ERROR_STOP=1 --username postgres \
  --dbname "$source_database" >/dev/null <<'SQL'
CREATE SCHEMA IF NOT EXISTS chummer_build;
REVOKE ALL ON SCHEMA chummer_build FROM PUBLIC;
CREATE TABLE IF NOT EXISTS chummer_build.schema_migrations (
    version integer PRIMARY KEY CHECK (version > 0),
    name text NOT NULL UNIQUE CHECK (char_length(name) BETWEEN 1 AND 256),
    checksum_sha256 text NOT NULL CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$'),
    applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
);
REVOKE ALL ON chummer_build.schema_migrations FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA chummer_build
    REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA chummer_build
    REVOKE ALL ON SEQUENCES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES
    REVOKE ALL ON FUNCTIONS FROM PUBLIC;
SQL

run_bounded docker exec -i "$container_name" \
  psql -X --set=ON_ERROR_STOP=1 --username postgres \
  --dbname "$source_database" < "$migration_path" >/dev/null

run_bounded docker exec -i "$container_name" \
  psql -X --set=ON_ERROR_STOP=1 --username postgres \
  --dbname "$source_database" \
  --set=migration_checksum="$migration_checksum" >/dev/null <<'SQL'
INSERT INTO chummer_build.schema_migrations(
    version,
    name,
    checksum_sha256)
VALUES (
    1,
    'V001__chummer_build_workspace.sql',
    :'migration_checksum');
SQL

current_stage="seed_source"
owner_a_hash="$({ printf 'chummer-build-workspace-owner-v1\0'; printf '%s' 'recovery-owner-alpha'; } | sha256sum | awk '{print $1}')"
owner_b_hash="$({ printf 'chummer-build-workspace-owner-v1\0'; printf '%s' 'recovery-owner-beta'; } | sha256sum | awk '{print $1}')"
document_a='{"storageSchemaVersion":1,"rulesetId":"sr5","workspaceSchemaVersion":1,"payloadKind":"native-xml","payload":"recovery-alpha","format":"NativeXml"}'
document_b='{"storageSchemaVersion":1,"rulesetId":"sr5","workspaceSchemaVersion":1,"payloadKind":"native-xml","payload":"recovery-beta","format":"NativeXml"}'
document_a_sha256="$(printf '%s' "$document_a" | sha256sum | awk '{print $1}')"
document_b_sha256="$(printf '%s' "$document_b" | sha256sum | awk '{print $1}')"

run_bounded docker exec -i "$container_name" \
  psql -X --set=ON_ERROR_STOP=1 --username postgres \
  --dbname "$source_database" \
  --set=owner_a="$owner_a_hash" \
  --set=owner_b="$owner_b_hash" \
  --set=document_a="$document_a" \
  --set=document_b="$document_b" \
  --set=document_a_sha256="$document_a_sha256" \
  --set=document_b_sha256="$document_b_sha256" >/dev/null <<'SQL'
INSERT INTO chummer_build.workspaces(
    owner_key,
    workspace_id,
    document_json,
    document_sha256,
    content_revision,
    saved_revision,
    updated_at_utc)
VALUES
    (
        decode(:'owner_a', 'hex'),
        'shared-recovery-proof',
        :'document_a'::jsonb,
        decode(:'document_a_sha256', 'hex'),
        3,
        2,
        TIMESTAMPTZ '2026-01-01T00:00:00Z'),
    (
        decode(:'owner_b', 'hex'),
        'shared-recovery-proof',
        :'document_b'::jsonb,
        decode(:'document_b_sha256', 'hex'),
        1,
        0,
        TIMESTAMPTZ '2026-01-01T00:00:01Z');
SQL

current_stage="validate_source"
validate_schema_and_ledger "$source_database" || fail
validate_rows "$source_database" || fail
write_row_snapshot "$source_database" "$source_rows_path"
write_schema_snapshot "$source_database" "$source_schema_path"
row_snapshot_sha256="$(sha256sum "$source_rows_path" | awk '{print $1}')"
schema_snapshot_sha256="$(sha256sum "$source_schema_path" | awk '{print $1}')"

current_stage="backup_source"
run_bounded docker exec "$container_name" \
  pg_dump --username postgres --dbname "$source_database" \
  --format=custom --no-owner > "$backup_path"

backup_bytes="$(wc -c < "$backup_path" | awk '{print $1}')"
if [[ ! "$backup_bytes" =~ ^[0-9]+$ || "$backup_bytes" -lt 1024 ]]; then
  fail
fi
backup_sha256="$(sha256sum "$backup_path" | awk '{print $1}')"
run_bounded docker exec -i "$container_name" \
  pg_restore --list > "$archive_toc_path" < "$backup_path"
backup_toc_sha256="$(sha256sum "$archive_toc_path" | awk '{print $1}')"
if ! awk '
    /^;/ { next }
    NF == 0 { next }
    / (TABLE|TABLE DATA|SEQUENCE|FUNCTION|INDEX|CONSTRAINT|TRIGGER) / {
      if ($0 !~ / chummer_build /) {
        unexpected = 1
      }
    }
    END { exit unexpected }
  ' "$archive_toc_path"; then
  fail
fi
backup_completed_at_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
backup_completed_epoch="$(date -u +%s)"

current_stage="reject_truncated_backup"
dd if="$backup_path" of="$truncated_backup_path" \
  bs=1 count="$((backup_bytes / 2))" status=none
run_bounded docker exec "$container_name" \
  createdb --username postgres "$corrupt_probe_database"
set +e
run_bounded docker exec -i "$container_name" \
  pg_restore --exit-on-error --username postgres \
  --dbname "$corrupt_probe_database" < "$truncated_backup_path" \
  >/dev/null 2> "$corrupt_restore_log"
corrupt_restore_status=$?
set -e
run_bounded docker exec "$container_name" \
  dropdb --force --username postgres "$corrupt_probe_database"
if [[ "$corrupt_restore_status" -eq 0 ]]; then
  fail
fi
truncated_archive_rejected=true

current_stage="simulate_source_loss"
recovery_started_epoch="$(date -u +%s)"
run_bounded docker exec "$container_name" \
  dropdb --force --username postgres "$source_database"
source_exists="$(psql_value postgres "
  SELECT count(*)
  FROM pg_database
  WHERE datname = '${source_database}';")"
if [[ "$source_exists" != "0" ]]; then
  fail
fi
source_database_dropped=true

current_stage="create_fresh_restore_target"
restore_exists="$(psql_value postgres "
  SELECT count(*)
  FROM pg_database
  WHERE datname = '${restore_database}';")"
if [[ "$restore_exists" != "0" ]]; then
  fail
fi
run_bounded docker exec "$container_name" \
  createdb --username postgres "$restore_database"
fresh_restore_target=true

current_stage="restore_backup"
restore_started_epoch="$(date -u +%s)"
backup_age_seconds="$((restore_started_epoch - backup_completed_epoch))"
run_bounded docker exec -i "$container_name" \
  pg_restore --exit-on-error --username postgres \
  --dbname "$restore_database" < "$backup_path" >/dev/null
restore_completed_epoch="$(date -u +%s)"
restore_duration_seconds="$((restore_completed_epoch - restore_started_epoch))"

current_stage="validate_restored_contract"
validate_schema_and_ledger "$restore_database" || fail
ledger_verified=true
validate_rows "$restore_database" || fail
restored_row_count="$(psql_value "$restore_database" \
  'SELECT count(*) FROM chummer_build.workspaces;')"
if [[ "$restored_row_count" != "2" ]]; then
  fail
fi

write_row_snapshot "$restore_database" "$restored_rows_path"
write_schema_snapshot "$restore_database" "$restored_schema_path"
cmp -s "$source_rows_path" "$restored_rows_path" || fail
data_and_revisions_verified=true
owner_isolation_verified=true
document_checksums_verified=true
cmp -s "$source_schema_path" "$restored_schema_path" || fail
schema_verified=true

restored_row_snapshot_sha256="$(sha256sum "$restored_rows_path" | awk '{print $1}')"
restored_schema_snapshot_sha256="$(sha256sum "$restored_schema_path" | awk '{print $1}')"
restored_backup_sha256="$(sha256sum "$backup_path" | awk '{print $1}')"
if [[ "$restored_row_snapshot_sha256" != "$row_snapshot_sha256" \
      || "$restored_schema_snapshot_sha256" != "$schema_snapshot_sha256" \
      || "$restored_backup_sha256" != "$backup_sha256" ]]; then
  fail
fi

validation_completed_epoch="$(date -u +%s)"
measured_recovery_seconds="$((validation_completed_epoch - recovery_started_epoch))"
current_stage="complete"
