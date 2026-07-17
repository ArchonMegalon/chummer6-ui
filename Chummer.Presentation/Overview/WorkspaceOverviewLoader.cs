using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewLoader : IWorkspaceOverviewLoader, IAuthoritativeWorkspaceOverviewLoader
{
    private static readonly object CapabilityIssuer = new();
    private static readonly CanonicalDocumentAuthority CanonicalAuthority = new();
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly IChummerClient? _authoritativeClient;

    public WorkspaceOverviewLoader()
    {
    }

    private WorkspaceOverviewLoader(IChummerClient authoritativeClient)
    {
        _authoritativeClient = authoritativeClient
            ?? throw new ArgumentNullException(nameof(authoritativeClient));
    }

    internal static WorkspaceOverviewLoader CreateCompositionBound(IChummerClient authoritativeClient)
        => new(authoritativeClient);

    bool IAuthoritativeWorkspaceOverviewLoader.IsCompositionBound
        => _authoritativeClient is not null;

    public Task<WorkspaceOverviewLoadResult> LoadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => LoadCoreAsync(client, workspaceId, ct);

    Task<WorkspaceOverviewLoadResult> IAuthoritativeWorkspaceOverviewLoader.LoadAuthoritativeAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (_authoritativeClient is null)
        {
            throw new InvalidOperationException(
                "Recovery authority is available only from the composition-bound workspace loader.");
        }

        return LoadAuthoritativeCoreAsync(workspaceId, ct);
    }

    Task<WorkspaceRecoveryAuthoritySnapshot> IAuthoritativeWorkspaceOverviewLoader.LoadRecoverySnapshotAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => LoadRecoverySnapshotCoreAsync(workspaceId, ct);

    private async Task<WorkspaceOverviewLoadResult> LoadAuthoritativeCoreAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        IChummerClient authoritativeClient = _authoritativeClient
            ?? throw new InvalidOperationException(
                "Recovery authority is available only from the composition-bound workspace loader.");
        WorkspaceOverviewLoadResult loaded = await LoadCoreAsync(
                authoritativeClient,
                workspaceId,
                ct)
            .ConfigureAwait(false);
        WorkspaceDocument document = loaded.Document
            ?? throw new InvalidOperationException(
                $"Dossier '{workspaceId.Value}' did not return a canonical document for recovery validation.");

        return loaded with
        {
            CanonicalValidation = ValidateCanonicalDocument(
                workspaceId,
                loaded.ContentRevision,
                document)
        };
    }

    private async Task<WorkspaceRecoveryAuthoritySnapshot> LoadRecoverySnapshotCoreAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        IChummerClient authoritativeClient = _authoritativeClient
            ?? throw new InvalidOperationException(
                "Recovery authority is available only from the composition-bound workspace loader.");
        WorkspaceDocumentSnapshot first = await ReadWorkspaceSnapshotAsync(
                authoritativeClient,
                workspaceId,
                ct)
            .ConfigureAwait(false);
        WorkspaceDocumentSnapshot verified = await ReadWorkspaceSnapshotAsync(
                authoritativeClient,
                workspaceId,
                ct)
            .ConfigureAwait(false);
        if (!SnapshotsMatch(first, verified))
        {
            throw new InvalidOperationException(
                $"Dossier '{workspaceId.Value}' changed while its recovery snapshot was being verified.");
        }

        CanonicalValidationCapability validation = ValidateCanonicalDocument(
            workspaceId,
            verified.ContentRevision,
            verified.Document);
        return new WorkspaceRecoveryAuthoritySnapshot(
            verified.Document,
            verified.ContentRevision,
            validation);
    }

    private static async Task<WorkspaceDocumentSnapshot> ReadWorkspaceSnapshotAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CommandResult<WorkspaceDocumentSnapshot> read = await client
            .GetWorkspaceAsync(workspaceId, ct)
            .ConfigureAwait(false);
        WorkspaceDocumentSnapshot snapshot = read.Success && read.Value is not null
            ? read.Value
            : throw new InvalidOperationException(read.Error ?? "Dossier could not be read for recovery validation.");
        if (!string.Equals(snapshot.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Dossier read returned '{snapshot.Id.Value}' while '{workspaceId.Value}' was requested.");
        }

        return snapshot;
    }

    private static bool SnapshotsMatch(
        WorkspaceDocumentSnapshot left,
        WorkspaceDocumentSnapshot right)
        => string.Equals(left.Id.Value, right.Id.Value, StringComparison.Ordinal)
            && left.ContentRevision == right.ContentRevision
            && left.SavedRevision == right.SavedRevision
            && DocumentsMatch(left.Document, right.Document);

    private static bool DocumentsMatch(WorkspaceDocument left, WorkspaceDocument right)
        => left.Format == right.Format
            && string.Equals(left.RulesetId, right.RulesetId, StringComparison.Ordinal)
            && left.SchemaVersion == right.SchemaVersion
            && string.Equals(left.PayloadKind, right.PayloadKind, StringComparison.Ordinal)
            && string.Equals(left.Content, right.Content, StringComparison.Ordinal);

    private static async Task<WorkspaceOverviewLoadResult> LoadCoreAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        WorkspaceDocumentSnapshot workspace = await ReadWorkspaceSnapshotAsync(
                client,
                workspaceId,
                ct)
            .ConfigureAwait(false);

        Task<CharacterProfileSection> profileTask = client.GetProfileAsync(workspaceId, ct);
        Task<CharacterProgressSection> progressTask = client.GetProgressAsync(workspaceId, ct);
        Task<CharacterSkillsSection> skillsTask = client.GetSkillsAsync(workspaceId, ct);
        Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(workspaceId, ct);
        Task<CharacterBuildSection> buildTask = client.GetBuildAsync(workspaceId, ct);
        Task<CharacterMovementSection> movementTask = client.GetMovementAsync(workspaceId, ct);
        Task<CharacterAwakeningSection> awakeningTask = client.GetAwakeningAsync(workspaceId, ct);
        Task<CharacterValidationResult> validationTask = client.ValidateAsync(workspaceId, ct);

        await Task.WhenAll(
            profileTask,
            progressTask,
            skillsTask,
            rulesTask,
            buildTask,
            movementTask,
            awakeningTask,
            validationTask);

        WorkspaceDocumentSnapshot verifiedWorkspace = await ReadWorkspaceSnapshotAsync(
                client,
                workspaceId,
                ct)
            .ConfigureAwait(false);

        if (!SnapshotsMatch(workspace, verifiedWorkspace))
        {
            throw new InvalidOperationException(
                $"Dossier '{workspaceId.Value}' changed while it was loading or returned inconsistent canonical bytes. Reload to use one consistent revision.");
        }

        if (!validationTask.Result.IsValid)
        {
            throw new InvalidOperationException(
                $"Dossier '{workspaceId.Value}' was not accepted by its canonical ruleset loader.");
        }

        return new WorkspaceOverviewLoadResult(
            Profile: profileTask.Result,
            Progress: progressTask.Result,
            Skills: skillsTask.Result,
            Rules: rulesTask.Result,
            Build: buildTask.Result,
            Movement: movementTask.Result,
            Awakening: awakeningTask.Result,
            ContentRevision: verifiedWorkspace.ContentRevision,
            SavedRevision: verifiedWorkspace.SavedRevision,
            Document: verifiedWorkspace.Document);
    }

    private CanonicalValidationCapability ValidateCanonicalDocument(
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        WorkspaceDocument document)
    {
        CanonicalAuthority.Validate(workspaceId, document);
        return new CanonicalValidationCapability(
            CapabilityIssuer,
            workspaceId,
            contentRevision,
            document);
    }

    private sealed class CanonicalDocumentAuthority
    {
        private readonly RulesetWorkspaceCodecResolver _resolver;

        public CanonicalDocumentAuthority()
        {
            var fileService = new CharacterFileService();
            var fileQueries = new XmlCharacterFileQueries(fileService);
            var sectionQueries = new XmlCharacterSectionQueries(new CharacterSectionService());
            var metadataCommands = new XmlCharacterMetadataCommands(fileService);
            _resolver = new RulesetWorkspaceCodecResolver(
            [
                new Sr4WorkspaceCodec(fileQueries, sectionQueries, metadataCommands),
                new Sr5WorkspaceCodec(fileQueries, sectionQueries, metadataCommands),
                new Sr6WorkspaceCodec(fileQueries, sectionQueries, metadataCommands)
            ]);
        }

        public void Validate(CharacterWorkspaceId workspaceId, WorkspaceDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            try
            {
                if (document.State is null || !Enum.IsDefined(document.Format))
                    throw new InvalidOperationException("Workspace document envelope is not canonical.");

                WorkspacePayloadEnvelope envelope = document.PayloadEnvelope;
                IRulesetWorkspaceCodec codec = _resolver.Resolve(envelope.RulesetId);
                if (!string.Equals(envelope.RulesetId, codec.RulesetId, StringComparison.Ordinal)
                    || envelope.SchemaVersion != codec.SchemaVersion
                    || !string.Equals(envelope.PayloadKind, codec.PayloadKind, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Workspace codec contract does not match its canonical ruleset.");
                }

                _ = codec.ParseSummary(envelope);
                CharacterValidationResult validation = codec.Validate(envelope);
                if (!validation.IsValid)
                    throw new InvalidOperationException("Workspace payload failed canonical ruleset validation.");
                _ = codec.BuildDownload(workspaceId, envelope, document.Format);
            }
            catch (Exception ex) when (ex is ArgumentException
                or FormatException
                or InvalidDataException
                or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Dossier '{workspaceId.Value}' was not accepted by the loader-owned canonical codec authority.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Opaque evidence that this loader observed one exact document/revision
    /// pass the canonical validation route. The private constructor keeps
    /// tests and other callers from manufacturing validation authority.
    /// </summary>
    internal sealed class CanonicalValidationCapability
    {
        private readonly CharacterWorkspaceId _workspaceId;
        private readonly long _contentRevision;
        private readonly byte[] _payloadDigest;
        private readonly WorkspaceDocumentFormat _format;
        private readonly string _rulesetId;
        private readonly int _schemaVersion;
        private readonly string _payloadKind;

        internal CanonicalValidationCapability(
            object issuer,
            CharacterWorkspaceId workspaceId,
            long contentRevision,
            WorkspaceDocument document)
        {
            if (!ReferenceEquals(issuer, CapabilityIssuer))
                throw new InvalidOperationException("Canonical validation authority is loader-owned.");
            ArgumentNullException.ThrowIfNull(document);
            byte[] bytes = StrictUtf8.GetBytes(document.Content);
            try
            {
                _workspaceId = workspaceId;
                _contentRevision = contentRevision;
                _payloadDigest = SHA256.HashData(bytes);
                _format = document.Format;
                _rulesetId = document.RulesetId;
                _schemaVersion = document.SchemaVersion;
                _payloadKind = document.PayloadKind;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        internal bool Matches(
            CharacterWorkspaceId workspaceId,
            long contentRevision,
            WorkspaceDocument document,
            ReadOnlySpan<byte> digest)
            => string.Equals(_workspaceId.Value, workspaceId.Value, StringComparison.Ordinal)
                && _contentRevision == contentRevision
                && CryptographicOperations.FixedTimeEquals(_payloadDigest, digest)
                && _format == document.Format
                && string.Equals(_rulesetId, document.RulesetId, StringComparison.Ordinal)
                && _schemaVersion == document.SchemaVersion
                && string.Equals(_payloadKind, document.PayloadKind, StringComparison.Ordinal);
    }
}
