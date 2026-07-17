using System.Data;
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
    IDisposable
{
    private const int MaximumReadAttempts = 2;
    private const int StorageSchemaVersion = 1;
    private const int MaximumWorkspaceIdLength = 256;
    private const int Sha256Length = 32;
    private const long InitialContentRevision = 1;
    private const long InitialSavedRevision = 0;
    private const string OwnerKeyDomain = "chummer-build-workspace-owner-v1\0";
    private const string UnavailableError = "Workspace storage is unavailable.";
    private const string CorruptError = "Workspace data is corrupt.";
    private const string MissingError = "Workspace not found.";
    private const string ConflictError = "Workspace content revision does not match the expected revision.";
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
        try
        {
            using NpgsqlConnection connection = OpenConnection();
            using NpgsqlCommand command = CreateCommand(connection, transaction: null, """
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

            return SuccessfulMutation(
                id,
                ReadUtc(reader, 2),
                reader.GetInt64(0),
                reader.GetInt64(1));
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return UnavailableMutation();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownerKey);
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
