using System.Data;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Npgsql;
using NpgsqlTypes;

namespace Chummer.Workspaces.Postgres;

public sealed class PostgresWorkspaceStore :
    IWorkspaceStore,
    IWorkspaceStoreReadinessProbe,
    IWorkspacePrivacyLifecycleStore,
    IDisposable
{
    private const int MaximumReadAttempts = 2;
    private const int StorageSchemaVersion = 1;
    private const int MaximumWorkspaceIdLength = 256;
    private const int Sha256Length = 32;
    private const long InitialContentRevision = 1;
    private const long InitialSavedRevision = 0;
    private const string OwnerKeyDomain = "chummer-build-workspace-owner-v1\0";
    private const string WorkspaceSubjectDomain = "chummer-build-workspace-delete-subject-v1\0";
    private const string OwnerSubjectDomain = "chummer-build-owner-delete-subject-v1\0";
    private const string DeletionReceiptDomain = "chummer-build-delete-receipt-v1\0";
    private const string UnavailableError = "Workspace storage is unavailable.";
    private const string CorruptError = "Workspace data is corrupt.";
    private const string MissingError = "Workspace not found.";
    private const string ConflictError = "Workspace content revision does not match the expected revision.";
    private const string DeletionFenceError = "Workspace deletion is still within its recovery-safety window.";
    private const string WorkspaceDeletionSubject = "workspace";
    private const string OwnerDeletionSubject = "owner";
    private static readonly TimeSpan DeletionReplayRetention = TimeSpan.FromDays(35);
    private static readonly TimeSpan DeletionAuditRetention = TimeSpan.FromDays(365);
    private static readonly JsonSerializerOptions DocumentJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly NpgsqlDataSource _dataSource;
    private readonly int _commandTimeoutSeconds;
    private readonly int _listPageSize;
    private readonly bool _requireLeastPrivilege;
    private bool _disposed;

    public PostgresWorkspaceStore(PostgresWorkspaceStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataSource = PostgresWorkspaceDataSourceFactory.Create(options);
        _commandTimeoutSeconds = PostgresWorkspaceDataSourceFactory.ToCommandTimeoutSeconds(
            options.CommandTimeout);
        _listPageSize = options.ListPageSize;
        _requireLeastPrivilege = options.RequireLeastPrivilege;
    }

    public WorkspaceStoreMutationResult CreateWorkspaceDocument(WorkspaceDocument document)
        => CreateWorkspaceDocumentCore(
            OwnerScope.LocalSingleUser,
            new CharacterWorkspaceId(Guid.NewGuid().ToString("N")),
            document);

    public WorkspaceStoreMutationResult CreateWorkspaceDocument(
        OwnerScope owner,
        WorkspaceDocument document)
        => IsInvalidScopedOwner(owner)
            ? InvalidOwnerMutation()
            : CreateWorkspaceDocumentCore(
                owner,
                new CharacterWorkspaceId(Guid.NewGuid().ToString("N")),
                document);

    public WorkspaceStoreMutationResult CreateWorkspaceDocument(
        CharacterWorkspaceId id,
        WorkspaceDocument document)
        => CreateWorkspaceDocumentCore(OwnerScope.LocalSingleUser, id, document);

    public WorkspaceStoreMutationResult CreateWorkspaceDocument(
        OwnerScope owner,
        CharacterWorkspaceId id,
        WorkspaceDocument document)
        => IsInvalidScopedOwner(owner)
            ? InvalidOwnerMutation()
            : CreateWorkspaceDocumentCore(owner, id, document);

    public IReadOnlyList<WorkspaceStoreEntry> List()
        => ListCore(OwnerScope.LocalSingleUser);

    public IReadOnlyList<WorkspaceStoreEntry> List(OwnerScope owner)
        => IsInvalidScopedOwner(owner) ? [] : ListCore(owner);

    public WorkspaceStoreReadResult Get(CharacterWorkspaceId id)
        => GetCore(OwnerScope.LocalSingleUser, id);

    public WorkspaceStoreReadResult Get(OwnerScope owner, CharacterWorkspaceId id)
        => IsInvalidScopedOwner(owner) ? InvalidOwnerRead() : GetCore(owner, id);

    public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document)
        => ReplaceWorkspaceDocumentCore(
            OwnerScope.LocalSingleUser,
            id,
            expectedContentRevision,
            document);

    public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document)
        => IsInvalidScopedOwner(owner)
            ? InvalidOwnerMutation()
            : ReplaceWorkspaceDocumentCore(owner, id, expectedContentRevision, document);

    public WorkspaceStoreMutationResult SaveCheckpoint(
        CharacterWorkspaceId id,
        long expectedContentRevision)
        => SaveCheckpointCore(
            OwnerScope.LocalSingleUser,
            id,
            expectedContentRevision);

    public WorkspaceStoreMutationResult SaveCheckpoint(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision)
        => IsInvalidScopedOwner(owner)
            ? InvalidOwnerMutation()
            : SaveCheckpointCore(owner, id, expectedContentRevision);

    public WorkspaceStoreMutationResult Delete(
        CharacterWorkspaceId id,
        long expectedContentRevision)
        => DeleteCore(OwnerScope.LocalSingleUser, id, expectedContentRevision);

    public WorkspaceStoreMutationResult Delete(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision)
        => IsInvalidScopedOwner(owner)
            ? InvalidOwnerMutation()
            : DeleteCore(owner, id, expectedContentRevision);

    public WorkspaceOwnerErasureResult EraseOwner(OwnerScope owner)
        => IsInvalidScopedOwner(owner)
            ? new WorkspaceOwnerErasureResult(
                false,
                null,
                null,
                0,
                null,
                "Owner scope is invalid.")
            : EraseOwnerCore(owner);

    public WorkspacePrivacyMaintenanceResult ApplyDeletionReplay(OwnerScope owner)
        => IsInvalidScopedOwner(owner)
            ? PrivacyMaintenanceFailure("Owner scope is invalid.")
            : ApplyDeletionReplayCore(owner);

    public WorkspacePrivacyMaintenanceResult ApplyAllDeletionReplay()
        => ApplyAllDeletionReplayCore();

    public WorkspacePrivacyMaintenanceResult PurgeExpiredDeletionAuditReceipts()
        => PurgeExpiredDeletionAuditReceiptsCore();

    public void Probe(OwnerScope owner)
    {
        if (IsInvalidScopedOwner(owner))
        {
            throw new ArgumentException(
                "A non-local owner scope is required for workspace readiness.",
                nameof(owner));
        }

        try
        {
            PostgresWorkspaceSchemaValidation schema = ExecuteReadWithRetry(() =>
            {
                using NpgsqlConnection connection = OpenConnection();
                return PostgresWorkspaceSchemaContract.Validate(
                    connection,
                    _commandTimeoutSeconds,
                    requireWritablePrimary: true);
            });
            if (!schema.Valid)
            {
                throw new WorkspaceStoreUnavailableException();
            }
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace readiness probe failed.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dataSource.Dispose();
    }

    private WorkspaceStoreMutationResult CreateWorkspaceDocumentCore(
        OwnerScope owner,
        CharacterWorkspaceId id,
        WorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsValidWorkspaceId(id))
        {
            return UnavailableMutation("Workspace id contains unsupported characters.");
        }

        if (!TrySerializeDocument(document, out string documentJson, out byte[] documentHash))
        {
            return CorruptMutation();
        }

        byte[] ownerKey = BuildOwnerKey(owner);
        byte[] workspaceKey = BuildWorkspaceSubjectKey(ownerKey, id);
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            AcquirePrivacyLocks(connection, transaction, ownerKey, workspaceKey);
            if (HasActiveDeletionFence(connection, transaction, ownerKey, workspaceKey))
            {
                return new WorkspaceStoreMutationResult(
                    WorkspaceOperationOutcome.Conflict,
                    Error: DeletionFenceError);
            }
            using NpgsqlCommand command = CreateCommand(connection, transaction, """
                INSERT INTO chummer_build.workspaces(
                    owner_key,
                    workspace_id,
                    document_json,
                    document_sha256,
                    content_revision,
                    saved_revision,
                    updated_at_utc)
                VALUES (
                    @owner_key,
                    @workspace_id,
                    @document_json,
                    @document_sha256,
                    1,
                    0,
                    clock_timestamp())
                ON CONFLICT (owner_key, workspace_id) DO NOTHING
                RETURNING content_revision, saved_revision, updated_at_utc
                """);
            AddOwnerAndId(command, ownerKey, id);
            command.Parameters.Add("document_json", NpgsqlDbType.Jsonb).Value = documentJson;
            command.Parameters.Add("document_sha256", NpgsqlDbType.Bytea).Value = documentHash;
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new WorkspaceStoreMutationResult(
                    WorkspaceOperationOutcome.Conflict,
                    Error: "Workspace already exists.");
            }

            WorkspaceStoreMutationResult result = SuccessfulMutation(
                id,
                ReadUtc(reader, 2),
                reader.GetInt64(0),
                reader.GetInt64(1));
            reader.Close();
            transaction.Commit();
            return result;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableMutation();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
            CryptographicOperations.ZeroMemory(workspaceKey);
            CryptographicOperations.ZeroMemory(documentHash);
        }
    }

    private IReadOnlyList<WorkspaceStoreEntry> ListCore(OwnerScope owner)
    {
        byte[] ownerKey = BuildOwnerKey(owner);
        try
        {
            return ExecuteReadWithRetry(() => ListSnapshot(ownerKey));
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return [];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
        }
    }

    private IReadOnlyList<WorkspaceStoreEntry> ListSnapshot(byte[] ownerKey)
    {
        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlTransaction transaction = connection.BeginTransaction(
            IsolationLevel.RepeatableRead);
        using (NpgsqlCommand readOnly = CreateCommand(
                   connection,
                   transaction,
                   "SET TRANSACTION READ ONLY"))
        {
            readOnly.ExecuteNonQuery();
        }

        var entries = new List<WorkspaceStoreEntry>();
        DateTimeOffset? cursorUpdatedAtUtc = null;
        string? cursorWorkspaceId = null;
        while (true)
        {
            string cursorPredicate = cursorUpdatedAtUtc.HasValue
                ? """
                  AND (
                      updated_at_utc < @cursor_updated_at_utc
                      OR (
                          updated_at_utc = @cursor_updated_at_utc
                          AND workspace_id > @cursor_workspace_id))
                  """
                : string.Empty;
            using NpgsqlCommand command = CreateCommand(connection, transaction, $"""
                SELECT
                    workspace_id,
                    content_revision,
                    saved_revision,
                    updated_at_utc
                FROM chummer_build.workspaces
                WHERE owner_key = @owner_key
                {cursorPredicate}
                ORDER BY updated_at_utc DESC, workspace_id
                LIMIT @page_size
                """);
            command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
            command.Parameters.Add("page_size", NpgsqlDbType.Integer).Value = _listPageSize;
            if (cursorUpdatedAtUtc.HasValue)
            {
                command.Parameters.Add(
                        "cursor_updated_at_utc",
                        NpgsqlDbType.TimestampTz)
                    .Value = cursorUpdatedAtUtc.Value;
                command.Parameters.Add(
                        "cursor_workspace_id",
                        NpgsqlDbType.Text)
                    .Value = cursorWorkspaceId!;
            }

            int observedRows = 0;
            using (NpgsqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    observedRows++;
                    cursorWorkspaceId = reader.GetString(0);
                    cursorUpdatedAtUtc = ReadUtc(reader, 3);
                    if (!TryReadStoredEntry(reader, out WorkspaceStoreEntry entry))
                    {
                        continue;
                    }

                    entries.Add(entry);
                }
            }

            if (observedRows < _listPageSize)
            {
                break;
            }
        }

        transaction.Commit();
        return entries;
    }

    private WorkspaceStoreReadResult GetCore(OwnerScope owner, CharacterWorkspaceId id)
    {
        if (!IsValidWorkspaceId(id))
        {
            return MissingRead();
        }

        byte[] ownerKey = BuildOwnerKey(owner);
        try
        {
            return ExecuteReadWithRetry(() =>
            {
                using NpgsqlConnection connection = OpenConnection();
                using NpgsqlCommand command = CreateCommand(connection, transaction: null, """
                    SELECT
                        document_json::text,
                        document_sha256,
                        content_revision,
                        saved_revision,
                        updated_at_utc
                    FROM chummer_build.workspaces
                    WHERE owner_key = @owner_key
                      AND workspace_id = @workspace_id
                    """);
                AddOwnerAndId(command, ownerKey, id);
                using NpgsqlDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return MissingRead();
                }

                return TryReadStoredDocument(
                    reader,
                    id,
                    startOrdinal: 0,
                    out WorkspaceStoredDocument stored)
                    ? new WorkspaceStoreReadResult(WorkspaceOperationOutcome.Success, stored)
                    : CorruptRead();
            });
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableRead();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
        }
    }

    private WorkspaceStoreMutationResult ReplaceWorkspaceDocumentCore(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsValidWorkspaceId(id))
        {
            return MissingMutation();
        }

        byte[] ownerKey = BuildOwnerKey(owner);
        byte[]? documentHash = null;
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            LockedWorkspaceRead current = ReadForUpdate(connection, transaction, ownerKey, id);
            if (current.Outcome != WorkspaceOperationOutcome.Success
                || current.Document is not WorkspaceStoredDocument stored)
            {
                return MutationFromLockedRead(current);
            }

            if (stored.ContentRevision != expectedContentRevision)
            {
                return ConflictMutation(stored);
            }

            if (stored.ContentRevision == long.MaxValue)
            {
                return UnavailableMutation("Workspace content revision is exhausted.");
            }

            if (!TrySerializeDocument(document, out string documentJson, out documentHash))
            {
                return CorruptMutation();
            }

            using NpgsqlCommand command = CreateCommand(connection, transaction, """
                UPDATE chummer_build.workspaces
                SET document_json = @document_json,
                    document_sha256 = @document_sha256,
                    content_revision = content_revision + 1,
                    updated_at_utc = clock_timestamp()
                WHERE owner_key = @owner_key
                  AND workspace_id = @workspace_id
                  AND content_revision = @expected_revision
                RETURNING content_revision, saved_revision, updated_at_utc
                """);
            AddOwnerAndId(command, ownerKey, id);
            command.Parameters.Add("document_json", NpgsqlDbType.Jsonb).Value = documentJson;
            command.Parameters.Add("document_sha256", NpgsqlDbType.Bytea).Value = documentHash;
            command.Parameters.Add("expected_revision", NpgsqlDbType.Bigint).Value = expectedContentRevision;
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return UnavailableMutation();
            }

            WorkspaceStoreMutationResult result = SuccessfulMutation(
                id,
                ReadUtc(reader, 2),
                reader.GetInt64(0),
                reader.GetInt64(1));
            reader.Close();
            transaction.Commit();
            return result;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableMutation();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
            if (documentHash is not null)
            {
                CryptographicOperations.ZeroMemory(documentHash);
            }
        }
    }

    private WorkspaceStoreMutationResult SaveCheckpointCore(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision)
    {
        if (!IsValidWorkspaceId(id))
        {
            return MissingMutation();
        }

        byte[] ownerKey = BuildOwnerKey(owner);
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            LockedWorkspaceRead current = ReadForUpdate(connection, transaction, ownerKey, id);
            if (current.Outcome != WorkspaceOperationOutcome.Success
                || current.Document is not WorkspaceStoredDocument stored)
            {
                return MutationFromLockedRead(current);
            }

            if (stored.ContentRevision != expectedContentRevision)
            {
                return ConflictMutation(stored);
            }

            if (stored.SavedRevision == stored.ContentRevision)
            {
                transaction.Commit();
                return new WorkspaceStoreMutationResult(
                    WorkspaceOperationOutcome.Success,
                    ToEntry(stored));
            }

            using NpgsqlCommand command = CreateCommand(connection, transaction, """
                UPDATE chummer_build.workspaces
                SET saved_revision = content_revision,
                    updated_at_utc = clock_timestamp()
                WHERE owner_key = @owner_key
                  AND workspace_id = @workspace_id
                  AND content_revision = @expected_revision
                RETURNING content_revision, saved_revision, updated_at_utc
                """);
            AddOwnerAndId(command, ownerKey, id);
            command.Parameters.Add("expected_revision", NpgsqlDbType.Bigint).Value = expectedContentRevision;
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return UnavailableMutation();
            }

            WorkspaceStoreMutationResult result = SuccessfulMutation(
                id,
                ReadUtc(reader, 2),
                reader.GetInt64(0),
                reader.GetInt64(1));
            reader.Close();
            transaction.Commit();
            return result;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableMutation();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
        }
    }

    private WorkspaceOwnerErasureResult EraseOwnerCore(OwnerScope owner)
    {
        byte[] ownerKey = BuildOwnerKey(owner);
        byte[] subjectKey = BuildOwnerSubjectKey(ownerKey);
        byte[]? receiptHash = null;
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            AcquirePrivacyLocks(connection, transaction, ownerKey);
            using NpgsqlCommand delete = CreateCommand(connection, transaction, """
                DELETE FROM chummer_build.workspaces
                WHERE owner_key = @owner_key
                """);
            delete.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
            int deletedCount = delete.ExecuteNonQuery();

            DateTimeOffset deletedAtUtc = DateTimeOffset.UtcNow;
            Guid operationId = Guid.NewGuid();
            receiptHash = BuildDeletionReceiptHash(
                operationId,
                ownerKey,
                OwnerDeletionSubject,
                subjectKey,
                contentRevision: null,
                deletedAtUtc);
            using NpgsqlCommand journal = CreateCommand(connection, transaction, """
                INSERT INTO chummer_build.workspace_deletion_journal(
                    operation_id,
                    owner_key,
                    subject_kind,
                    subject_key,
                    content_revision,
                    deleted_at_utc,
                    replay_expires_at_utc,
                    audit_expires_at_utc,
                    receipt_sha256)
                VALUES (
                    @operation_id,
                    @owner_key,
                    @subject_kind,
                    @subject_key,
                    @content_revision,
                    @deleted_at_utc,
                    @replay_expires_at_utc,
                    @audit_expires_at_utc,
                    @receipt_sha256)
                """);
            AddDeletionJournalParameters(
                journal,
                operationId,
                ownerKey,
                OwnerDeletionSubject,
                subjectKey,
                contentRevision: null,
                deletedAtUtc,
                receiptHash);
            journal.ExecuteNonQuery();
            transaction.Commit();

            return new WorkspaceOwnerErasureResult(
                true,
                operationId,
                deletedAtUtc,
                deletedCount,
                Convert.ToHexString(receiptHash).ToLowerInvariant());
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return new WorkspaceOwnerErasureResult(
                false,
                null,
                null,
                0,
                null,
                UnavailableError);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
            CryptographicOperations.ZeroMemory(subjectKey);
            if (receiptHash is not null)
            {
                CryptographicOperations.ZeroMemory(receiptHash);
            }
        }
    }

    private WorkspacePrivacyMaintenanceResult ApplyDeletionReplayCore(OwnerScope owner)
    {
        byte[] ownerKey = BuildOwnerKey(owner);
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            int replayed = ApplyDeletionReplayForOwnerKey(
                connection,
                transaction,
                ownerKey);
            transaction.Commit();
            return PrivacyMaintenanceSuccess(replayed);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return PrivacyMaintenanceFailure(UnavailableError);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
        }
    }

    private WorkspacePrivacyMaintenanceResult ApplyAllDeletionReplayCore()
    {
        var ownerKeys = new List<byte[]>();
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            ownerKeys.AddRange(ReadActiveDeletionOwnerKeys(connection, transaction));
            int replayed = 0;
            foreach (byte[] ownerKey in ownerKeys)
            {
                replayed = checked(replayed + ApplyDeletionReplayForOwnerKey(
                    connection,
                    transaction,
                    ownerKey));
            }

            transaction.Commit();
            return PrivacyMaintenanceSuccess(replayed);
        }
        catch (Exception exception) when (IsStorageFailure(exception) || exception is OverflowException)
        {
            return PrivacyMaintenanceFailure(UnavailableError);
        }
        finally
        {
            foreach (byte[] ownerKey in ownerKeys)
            {
                CryptographicOperations.ZeroMemory(ownerKey);
            }
        }
    }

    private int ApplyDeletionReplayForOwnerKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey)
    {
        AcquirePrivacyLocks(connection, transaction, ownerKey);
        if (HasActiveOwnerDeletionFence(connection, transaction, ownerKey))
        {
            using NpgsqlCommand deleteOwner = CreateCommand(connection, transaction, """
                DELETE FROM chummer_build.workspaces
                WHERE owner_key = @owner_key
                """);
            deleteOwner.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
            return deleteOwner.ExecuteNonQuery();
        }

        HashSet<string> activeSubjectKeys = ReadActiveWorkspaceDeletionKeys(
            connection,
            transaction,
            ownerKey);
        if (activeSubjectKeys.Count == 0)
        {
            return 0;
        }

        var workspaceIds = new List<string>();
        using (NpgsqlCommand read = CreateCommand(connection, transaction, """
            SELECT workspace_id
            FROM chummer_build.workspaces
            WHERE owner_key = @owner_key
            FOR UPDATE
            """))
        {
            read.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
            using NpgsqlDataReader reader = read.ExecuteReader();
            while (reader.Read())
            {
                workspaceIds.Add(reader.GetString(0));
            }
        }

        int replayed = 0;
        foreach (string workspaceId in workspaceIds)
        {
            var id = new CharacterWorkspaceId(workspaceId);
            byte[] subjectKey = BuildWorkspaceSubjectKey(ownerKey, id);
            try
            {
                if (!activeSubjectKeys.Contains(Convert.ToHexString(subjectKey)))
                {
                    continue;
                }

                using NpgsqlCommand delete = CreateCommand(connection, transaction, """
                    DELETE FROM chummer_build.workspaces
                    WHERE owner_key = @owner_key
                      AND workspace_id = @workspace_id
                    """);
                AddOwnerAndId(delete, ownerKey, id);
                replayed = checked(replayed + delete.ExecuteNonQuery());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(subjectKey);
            }
        }

        return replayed;
    }

    private WorkspacePrivacyMaintenanceResult PurgeExpiredDeletionAuditReceiptsCore()
    {
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlCommand command = CreateCommand(connection, transaction: null, """
                DELETE FROM chummer_build.workspace_deletion_journal
                WHERE audit_expires_at_utc <= clock_timestamp()
                """);
            return PrivacyMaintenanceSuccess(command.ExecuteNonQuery());
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return PrivacyMaintenanceFailure(UnavailableError);
        }
    }

    private WorkspaceStoreMutationResult DeleteCore(
        OwnerScope owner,
        CharacterWorkspaceId id,
        long expectedContentRevision)
    {
        if (!IsValidWorkspaceId(id))
        {
            return MissingMutation();
        }

        byte[] ownerKey = BuildOwnerKey(owner);
        byte[] workspaceKey = BuildWorkspaceSubjectKey(ownerKey, id);
        byte[]? receiptHash = null;
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            AcquirePrivacyLocks(connection, transaction, ownerKey, workspaceKey);
            LockedWorkspaceRead current = ReadForUpdate(connection, transaction, ownerKey, id);
            if (current.Outcome != WorkspaceOperationOutcome.Success
                || current.Document is not WorkspaceStoredDocument stored)
            {
                return MutationFromLockedRead(current);
            }

            if (stored.ContentRevision != expectedContentRevision)
            {
                return ConflictMutation(stored);
            }

            DateTimeOffset deletedAtUtc = DateTimeOffset.UtcNow;
            Guid operationId = Guid.NewGuid();
            receiptHash = BuildDeletionReceiptHash(
                operationId,
                ownerKey,
                WorkspaceDeletionSubject,
                workspaceKey,
                stored.ContentRevision,
                deletedAtUtc);
            using (NpgsqlCommand journal = CreateCommand(connection, transaction, """
                INSERT INTO chummer_build.workspace_deletion_journal(
                    operation_id,
                    owner_key,
                    subject_kind,
                    subject_key,
                    content_revision,
                    deleted_at_utc,
                    replay_expires_at_utc,
                    audit_expires_at_utc,
                    receipt_sha256)
                VALUES (
                    @operation_id,
                    @owner_key,
                    @subject_kind,
                    @subject_key,
                    @content_revision,
                    @deleted_at_utc,
                    @replay_expires_at_utc,
                    @audit_expires_at_utc,
                    @receipt_sha256)
                """))
            {
                AddDeletionJournalParameters(
                    journal,
                    operationId,
                    ownerKey,
                    WorkspaceDeletionSubject,
                    workspaceKey,
                    stored.ContentRevision,
                    deletedAtUtc,
                    receiptHash);
                journal.ExecuteNonQuery();
            }

            using NpgsqlCommand command = CreateCommand(connection, transaction, """
                DELETE FROM chummer_build.workspaces
                WHERE owner_key = @owner_key
                  AND workspace_id = @workspace_id
                  AND content_revision = @expected_revision
                """);
            AddOwnerAndId(command, ownerKey, id);
            command.Parameters.Add("expected_revision", NpgsqlDbType.Bigint).Value = expectedContentRevision;
            if (command.ExecuteNonQuery() != 1)
            {
                return UnavailableMutation();
            }

            transaction.Commit();
            return new WorkspaceStoreMutationResult(
                WorkspaceOperationOutcome.Success,
                ToEntry(stored));
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableMutation();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
            CryptographicOperations.ZeroMemory(workspaceKey);
            if (receiptHash is not null)
            {
                CryptographicOperations.ZeroMemory(receiptHash);
            }
        }
    }

    private LockedWorkspaceRead ReadForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey,
        CharacterWorkspaceId id)
    {
        using NpgsqlCommand command = CreateCommand(connection, transaction, """
            SELECT
                document_json::text,
                document_sha256,
                content_revision,
                saved_revision,
                updated_at_utc
            FROM chummer_build.workspaces
            WHERE owner_key = @owner_key
              AND workspace_id = @workspace_id
            FOR UPDATE
            """);
        AddOwnerAndId(command, ownerKey, id);
        using NpgsqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new LockedWorkspaceRead(WorkspaceOperationOutcome.Missing, null);
        }

        return TryReadStoredDocument(reader, id, startOrdinal: 0, out WorkspaceStoredDocument stored)
            ? new LockedWorkspaceRead(WorkspaceOperationOutcome.Success, stored)
            : new LockedWorkspaceRead(WorkspaceOperationOutcome.Corrupt, null);
    }

    private NpgsqlConnection OpenConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NpgsqlConnection connection = _dataSource.OpenConnection();
        if (!_requireLeastPrivilege)
        {
            return connection;
        }

        try
        {
            if (!PostgresWorkspaceRuntimeGrantHelper.ValidateCurrentRole(
                    connection,
                    _commandTimeoutSeconds))
            {
                throw new WorkspaceStoreUnavailableException();
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql)
    {
        NpgsqlCommand command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void AddOwnerAndId(
        NpgsqlCommand command,
        byte[] ownerKey,
        CharacterWorkspaceId id)
    {
        command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
        command.Parameters.Add("workspace_id", NpgsqlDbType.Text).Value = id.Value;
    }

    private bool HasActiveDeletionFence(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey,
        byte[] workspaceKey)
    {
        using NpgsqlCommand command = CreateCommand(connection, transaction, """
            SELECT EXISTS (
                SELECT 1
                FROM chummer_build.workspace_deletion_journal
                WHERE owner_key = @owner_key
                  AND replay_expires_at_utc > clock_timestamp()
                  AND (
                      subject_kind = 'owner'
                      OR (subject_kind = 'workspace' AND subject_key = @workspace_key)))
            """);
        command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
        command.Parameters.Add("workspace_key", NpgsqlDbType.Bytea).Value = workspaceKey;
        return Convert.ToBoolean(command.ExecuteScalar());
    }

    private void AcquirePrivacyLocks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey,
        byte[]? workspaceKey = null)
    {
        byte[] ownerSubjectKey = BuildOwnerSubjectKey(ownerKey);
        try
        {
            AcquirePrivacyLock(connection, transaction, ownerSubjectKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerSubjectKey);
        }

        if (workspaceKey is not null)
        {
            AcquirePrivacyLock(connection, transaction, workspaceKey);
        }
    }

    private void AcquirePrivacyLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] subjectKey)
    {
        long lockKey = BinaryPrimitives.ReadInt64BigEndian(subjectKey.AsSpan(0, sizeof(long)));
        using NpgsqlCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT pg_advisory_xact_lock(@lock_key)");
        command.Parameters.Add("lock_key", NpgsqlDbType.Bigint).Value = lockKey;
        command.ExecuteNonQuery();
    }

    private bool HasActiveOwnerDeletionFence(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey)
    {
        using NpgsqlCommand command = CreateCommand(connection, transaction, """
            SELECT EXISTS (
                SELECT 1
                FROM chummer_build.workspace_deletion_journal
                WHERE owner_key = @owner_key
                  AND subject_kind = 'owner'
                  AND replay_expires_at_utc > clock_timestamp())
            """);
        command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
        return Convert.ToBoolean(command.ExecuteScalar());
    }

    private HashSet<string> ReadActiveWorkspaceDeletionKeys(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        byte[] ownerKey)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        using NpgsqlCommand command = CreateCommand(connection, transaction, """
            SELECT subject_key
            FROM chummer_build.workspace_deletion_journal
            WHERE owner_key = @owner_key
              AND subject_kind = 'workspace'
              AND replay_expires_at_utc > clock_timestamp()
            """);
        command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
        using NpgsqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            byte[] key = reader.GetFieldValue<byte[]>(0);
            try
            {
                keys.Add(Convert.ToHexString(key));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        return keys;
    }

    private List<byte[]> ReadActiveDeletionOwnerKeys(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        var keys = new List<byte[]>();
        using NpgsqlCommand command = CreateCommand(connection, transaction, """
            SELECT DISTINCT owner_key
            FROM chummer_build.workspace_deletion_journal
            WHERE replay_expires_at_utc > clock_timestamp()
            ORDER BY owner_key
            """);
        using NpgsqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            keys.Add(reader.GetFieldValue<byte[]>(0));
        }

        return keys;
    }

    private static void AddDeletionJournalParameters(
        NpgsqlCommand command,
        Guid operationId,
        byte[] ownerKey,
        string subjectKind,
        byte[] subjectKey,
        long? contentRevision,
        DateTimeOffset deletedAtUtc,
        byte[] receiptHash)
    {
        command.Parameters.Add("operation_id", NpgsqlDbType.Uuid).Value = operationId;
        command.Parameters.Add("owner_key", NpgsqlDbType.Bytea).Value = ownerKey;
        command.Parameters.Add("subject_kind", NpgsqlDbType.Text).Value = subjectKind;
        command.Parameters.Add("subject_key", NpgsqlDbType.Bytea).Value = subjectKey;
        command.Parameters.Add("content_revision", NpgsqlDbType.Bigint).Value =
            contentRevision.HasValue ? contentRevision.Value : DBNull.Value;
        command.Parameters.Add("deleted_at_utc", NpgsqlDbType.TimestampTz).Value = deletedAtUtc;
        command.Parameters.Add("replay_expires_at_utc", NpgsqlDbType.TimestampTz).Value =
            deletedAtUtc.Add(DeletionReplayRetention);
        command.Parameters.Add("audit_expires_at_utc", NpgsqlDbType.TimestampTz).Value =
            deletedAtUtc.Add(DeletionAuditRetention);
        command.Parameters.Add("receipt_sha256", NpgsqlDbType.Bytea).Value = receiptHash;
    }

    private static byte[] BuildWorkspaceSubjectKey(
        byte[] ownerKey,
        CharacterWorkspaceId id)
    {
        byte[] input = Encoding.UTF8.GetBytes(WorkspaceSubjectDomain + id.Value);
        try
        {
            return HMACSHA256.HashData(ownerKey, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static byte[] BuildOwnerSubjectKey(byte[] ownerKey)
    {
        byte[] input = Encoding.UTF8.GetBytes(OwnerSubjectDomain);
        try
        {
            return HMACSHA256.HashData(ownerKey, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static byte[] BuildDeletionReceiptHash(
        Guid operationId,
        byte[] ownerKey,
        string subjectKind,
        byte[] subjectKey,
        long? contentRevision,
        DateTimeOffset deletedAtUtc)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] domain = Encoding.UTF8.GetBytes(DeletionReceiptDomain);
        byte[] operation = operationId.ToByteArray();
        byte[] kind = Encoding.UTF8.GetBytes(subjectKind);
        Span<byte> numeric = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(numeric[..8], contentRevision ?? 0);
        BinaryPrimitives.WriteInt64BigEndian(numeric[8..], deletedAtUtc.UtcTicks);
        try
        {
            hash.AppendData(domain);
            hash.AppendData(operation);
            hash.AppendData(ownerKey);
            hash.AppendData(kind);
            hash.AppendData(subjectKey);
            hash.AppendData(numeric);
            return hash.GetHashAndReset();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(domain);
            CryptographicOperations.ZeroMemory(operation);
            CryptographicOperations.ZeroMemory(kind);
            CryptographicOperations.ZeroMemory(numeric);
        }
    }

    private static bool TrySerializeDocument(
        WorkspaceDocument document,
        out string documentJson,
        out byte[] documentHash)
    {
        documentJson = string.Empty;
        documentHash = [];
        byte[]? documentBytes = null;
        try
        {
            WorkspaceDocumentState state = document.State;
            if (state is null
                || string.IsNullOrWhiteSpace(state.RulesetId)
                || state.SchemaVersion <= 0
                || string.IsNullOrWhiteSpace(state.PayloadKind)
                || string.IsNullOrWhiteSpace(state.Payload)
                || !Enum.IsDefined(document.Format))
            {
                return false;
            }

            var persisted = new PersistedWorkspaceDocument(
                StorageSchemaVersion,
                state.RulesetId,
                state.SchemaVersion,
                state.PayloadKind,
                state.Payload,
                document.Format.ToString());
            documentBytes = JsonSerializer.SerializeToUtf8Bytes(persisted, DocumentJsonOptions);
            documentJson = Encoding.UTF8.GetString(documentBytes);
            documentHash = SHA256.HashData(documentBytes);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        finally
        {
            if (documentBytes is not null)
            {
                CryptographicOperations.ZeroMemory(documentBytes);
            }
        }
    }

    private static bool TryReadStoredDocument(
        NpgsqlDataReader reader,
        CharacterWorkspaceId id,
        int startOrdinal,
        out WorkspaceStoredDocument stored)
    {
        stored = null!;
        byte[]? observedHash = null;
        byte[]? actualHash = null;
        byte[]? documentBytes = null;
        try
        {
            string json = reader.GetString(startOrdinal);
            observedHash = reader.GetFieldValue<byte[]>(startOrdinal + 1);
            long contentRevision = reader.GetInt64(startOrdinal + 2);
            long savedRevision = reader.GetInt64(startOrdinal + 3);
            if (observedHash.Length != Sha256Length
                || contentRevision < InitialContentRevision
                || savedRevision < InitialSavedRevision
                || savedRevision > contentRevision)
            {
                return false;
            }

            PersistedWorkspaceDocument? persisted = JsonSerializer.Deserialize<PersistedWorkspaceDocument>(
                json,
                DocumentJsonOptions);
            if (persisted is null
                || persisted.StorageSchemaVersion != StorageSchemaVersion
                || string.IsNullOrWhiteSpace(persisted.RulesetId)
                || persisted.WorkspaceSchemaVersion <= 0
                || string.IsNullOrWhiteSpace(persisted.PayloadKind)
                || string.IsNullOrWhiteSpace(persisted.Payload)
                || !Enum.TryParse(
                    persisted.Format,
                    ignoreCase: false,
                    out WorkspaceDocumentFormat format)
                || !Enum.IsDefined(format))
            {
                return false;
            }

            documentBytes = JsonSerializer.SerializeToUtf8Bytes(persisted, DocumentJsonOptions);
            actualHash = SHA256.HashData(documentBytes);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, observedHash))
            {
                return false;
            }

            var state = new WorkspaceDocumentState(
                persisted.RulesetId,
                persisted.WorkspaceSchemaVersion,
                persisted.PayloadKind,
                persisted.Payload);
            stored = new WorkspaceStoredDocument(
                id,
                new WorkspaceDocument(state, format),
                contentRevision,
                savedRevision,
                ReadUtc(reader, startOrdinal + 4));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        finally
        {
            if (observedHash is not null)
                CryptographicOperations.ZeroMemory(observedHash);
            if (actualHash is not null)
                CryptographicOperations.ZeroMemory(actualHash);
            if (documentBytes is not null)
                CryptographicOperations.ZeroMemory(documentBytes);
        }
    }

    private static bool TryReadStoredEntry(
        NpgsqlDataReader reader,
        out WorkspaceStoreEntry entry)
    {
        entry = default;
        try
        {
            var id = new CharacterWorkspaceId(reader.GetString(0));
            long contentRevision = reader.GetInt64(1);
            long savedRevision = reader.GetInt64(2);
            if (!IsValidWorkspaceId(id)
                || contentRevision < InitialContentRevision
                || savedRevision < InitialSavedRevision
                || savedRevision > contentRevision)
            {
                return false;
            }

            entry = new WorkspaceStoreEntry(
                id,
                ReadUtc(reader, 3),
                contentRevision,
                savedRevision);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static byte[] BuildOwnerKey(OwnerScope owner)
    {
        byte[] input = Encoding.UTF8.GetBytes(OwnerKeyDomain + owner.NormalizedValue);
        try
        {
            return SHA256.HashData(input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static bool IsValidWorkspaceId(CharacterWorkspaceId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)
            || id.Value.Length > MaximumWorkspaceIdLength)
        {
            return false;
        }

        return id.Value.All(static character =>
            char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private static bool IsInvalidScopedOwner(OwnerScope owner)
        => string.IsNullOrWhiteSpace(owner.NormalizedValue) || owner.IsLocalSingleUser;

    private static DateTimeOffset ReadUtc(NpgsqlDataReader reader, int ordinal)
    {
        DateTime value = reader.GetDateTime(ordinal);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static WorkspaceStoreEntry ToEntry(WorkspaceStoredDocument document)
        => new(
            document.Id,
            document.LastUpdatedUtc,
            document.ContentRevision,
            document.SavedRevision);

    private static WorkspaceStoreMutationResult SuccessfulMutation(
        CharacterWorkspaceId id,
        DateTimeOffset lastUpdatedUtc,
        long contentRevision,
        long savedRevision)
        => new(
            WorkspaceOperationOutcome.Success,
            new WorkspaceStoreEntry(id, lastUpdatedUtc, contentRevision, savedRevision));

    private static WorkspaceStoreMutationResult ConflictMutation(WorkspaceStoredDocument current)
        => new(
            WorkspaceOperationOutcome.Conflict,
            ToEntry(current),
            ConflictError);

    private static WorkspaceStoreMutationResult MutationFromLockedRead(LockedWorkspaceRead read)
        => read.Outcome switch
        {
            WorkspaceOperationOutcome.Missing => MissingMutation(),
            WorkspaceOperationOutcome.Corrupt => CorruptMutation(),
            _ => UnavailableMutation()
        };

    private static WorkspaceStoreReadResult MissingRead()
        => new(WorkspaceOperationOutcome.Missing, Error: MissingError);

    private static WorkspaceStoreReadResult CorruptRead()
        => new(WorkspaceOperationOutcome.Corrupt, Error: CorruptError);

    private static WorkspaceStoreReadResult UnavailableRead()
        => new(WorkspaceOperationOutcome.Unavailable, Error: UnavailableError);

    private static WorkspaceStoreReadResult InvalidOwnerRead()
        => new(WorkspaceOperationOutcome.Unavailable, Error: "Owner scope is invalid.");

    private static WorkspaceStoreMutationResult MissingMutation()
        => new(WorkspaceOperationOutcome.Missing, Error: MissingError);

    private static WorkspaceStoreMutationResult CorruptMutation()
        => new(WorkspaceOperationOutcome.Corrupt, Error: CorruptError);

    private static WorkspaceStoreMutationResult UnavailableMutation(string? error = null)
        => new(WorkspaceOperationOutcome.Unavailable, Error: error ?? UnavailableError);

    private static WorkspaceStoreMutationResult InvalidOwnerMutation()
        => UnavailableMutation("Owner scope is invalid.");

    private static WorkspacePrivacyMaintenanceResult PrivacyMaintenanceSuccess(int affectedCount)
        => new(true, affectedCount);

    private static WorkspacePrivacyMaintenanceResult PrivacyMaintenanceFailure(string error)
        => new(false, 0, error);

    private static bool IsStorageFailure(Exception exception)
        => exception is NpgsqlException
            or TimeoutException
            or InvalidOperationException
            or WorkspaceStoreUnavailableException;

    private static T ExecuteReadWithRetry<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception exception) when (
                attempt < MaximumReadAttempts
                && IsTransientReadFailure(exception))
            {
                // Retrying a complete read is safe. Mutations intentionally do
                // not use this helper because a lost commit acknowledgement is
                // ambiguous without a caller-provided idempotency key.
            }
        }
    }

    private static bool IsTransientReadFailure(Exception exception)
        => exception is TimeoutException
            || exception is NpgsqlException { IsTransient: true };

    private sealed record PersistedWorkspaceDocument(
        int StorageSchemaVersion,
        string RulesetId,
        int WorkspaceSchemaVersion,
        string PayloadKind,
        string Payload,
        string Format);

    private readonly record struct LockedWorkspaceRead(
        WorkspaceOperationOutcome Outcome,
        WorkspaceStoredDocument? Document);

    private sealed class WorkspaceStoreUnavailableException : Exception
    {
    }
}
