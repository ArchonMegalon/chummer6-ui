using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.OriginBooks;

/// <summary>
/// Persistence seam for the user-owned live Origin Dossier timeline. Stores
/// must persist the complete sealed checkpoint atomically and must not merge
/// fields from different revisions.
/// </summary>
public interface IOriginDossierDraftTimelineStore
{
    Task<LifeModuleOriginDossierDraftCheckpoint?> LoadAsync(
        string ownerId,
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        LifeModuleOriginDossierDraftCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string ownerId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record OriginDossierLifeModuleEffectState(
    string EffectId,
    string Domain,
    string TargetId,
    string BeforeValue,
    string AfterValue,
    decimal BudgetDelta,
    IReadOnlyList<string> SourceAnchorIds,
    string ItemDigest);

public sealed record OriginDossierLifeModuleChoiceState(
    string ChoiceId,
    string Label,
    string Source,
    string PageReference,
    decimal KarmaCost,
    string KarmaRaw,
    IReadOnlyList<OriginDossierLifeModuleEffectState> Effects,
    IReadOnlyList<string> PendingFollowUpIds,
    IReadOnlyList<string> SourceAnchorIds,
    string ChoiceDigest,
    bool IsSelected);

public sealed record OriginDossierLifeModuleDecisionState(
    string OwnerId,
    string WorkspaceId,
    long WorkspaceRevision,
    string RunnerDisplayName,
    string StageId,
    int StageOrder,
    int TurnSequence,
    string Locale,
    string VisibleStoryMarkdown,
    string DecisionPrompt,
    IReadOnlyList<OriginNarrativeChapterProjection> Timeline,
    IReadOnlyList<OriginDossierLifeModuleChoiceState> Choices,
    string LtdProvenanceState,
    string LtdProviderDisplay,
    bool LtdAffectsMechanics,
    string? SelectedChoiceId,
    string? PendingPreviewDigest,
    string BoundTurnSeedDigest,
    string CheckpointDigest)
{
    public bool StoryEndsAtDecisionPoint { get; } = true;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(PendingPreviewDigest)
                              && Choices.Count(static choice => choice.IsSelected) == 1;
}

/// <summary>
/// Renderer-neutral projection of a Core-validated checkpoint. It does not
/// infer choices, effects, sources, or LTD provenance.
/// </summary>
public static class OriginDossierLifeModuleInteractionProjector
{
    public static OriginDossierLifeModuleDecisionState Project(
        LifeModuleOriginDossierDraftCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        OriginStoryArcSeed projection = checkpoint.Projection
            ?? throw new InvalidOperationException("The Origin Dossier checkpoint has no canonical projection.");
        LifeModuleNarrativeTurnSeed turn = projection.CurrentTurn
            ?? throw new InvalidOperationException("The Origin Dossier checkpoint has no current decision turn.");
        if (!string.Equals(checkpoint.OwnerId, turn.OwnerId, StringComparison.Ordinal)
            || !string.Equals(checkpoint.WorkspaceId, turn.WorkspaceId, StringComparison.Ordinal)
            || checkpoint.WorkspaceRevision != turn.WorkspaceRevision
            || string.IsNullOrWhiteSpace(checkpoint.CheckpointDigest)
            || string.IsNullOrWhiteSpace(turn.Locale)
            || !string.Equals(turn.Locale, turn.Locale.Trim(), StringComparison.Ordinal)
            || !turn.VisibleStoryMarkdown.EndsWith(turn.DecisionPrompt, StringComparison.Ordinal)
            || turn.LegalChoices.Count == 0
            || turn.LegalChoices.Select(static choice => choice.ChoiceId)
                .Distinct(StringComparer.Ordinal).Count() != turn.LegalChoices.Count
            || turn.LegalChoices.Any(static choice =>
                !choice.IsLegal
                || choice.Blockers.Count != 0
                || string.IsNullOrWhiteSpace(choice.Source)
                || choice.SourceAnchorIds.Count == 0
                || choice.MechanicsPreview is null
                || !choice.MechanicsPreview.KarmaIsExact
                || choice.MechanicsPreview.SourceAnchorIds.Count == 0))
        {
            throw new InvalidOperationException(
                "The Origin Dossier checkpoint is not a complete source-bound live decision.");
        }

        string? selectedChoiceId = checkpoint.PendingPreview?.SelectedChoice?.ChoiceId;
        if (selectedChoiceId is not null
            && turn.LegalChoices.Count(choice => string.Equals(
                choice.ChoiceId,
                selectedChoiceId,
                StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException(
                "The pending Origin Dossier preview does not identify exactly one current choice.");
        }

        OriginDossierLifeModuleChoiceState[] choices = turn.LegalChoices
            .Select(choice => new OriginDossierLifeModuleChoiceState(
                choice.ChoiceId,
                choice.Label,
                choice.Source,
                choice.PageReference,
                choice.MechanicsPreview.KarmaCost,
                choice.MechanicsPreview.KarmaRaw,
                choice.MechanicsPreview.Items.Select(static item =>
                    new OriginDossierLifeModuleEffectState(
                        item.EffectId,
                        item.Domain,
                        item.TargetId,
                        item.BeforeValue,
                        item.AfterValue,
                        item.BudgetDelta,
                        item.SourceAnchorIds.ToArray(),
                        item.ItemDigest)).ToArray(),
                choice.MechanicsPreview.PendingFollowUpIds.ToArray(),
                choice.SourceAnchorIds.ToArray(),
                choice.ChoiceDigest,
                string.Equals(choice.ChoiceId, selectedChoiceId, StringComparison.Ordinal)))
            .ToArray();

        OriginLtdNarrativeProvenance provenance = checkpoint.LtdProvenance
            ?? throw new InvalidOperationException("The Origin Dossier checkpoint has no LTD provenance state.");
        string providerDisplay = string.Equals(
            provenance.State,
            OriginLtdProvenanceStates.NotRequested,
            StringComparison.Ordinal)
            ? "Optional narrative extension off"
            : string.IsNullOrWhiteSpace(provenance.ProviderModelId)
                ? provenance.ProviderId
                : $"{provenance.ProviderId} · {provenance.ProviderModelId}";

        return new(
            checkpoint.OwnerId,
            checkpoint.WorkspaceId,
            checkpoint.WorkspaceRevision,
            turn.RunnerDisplayName,
            turn.StageId,
            turn.StageOrder,
            turn.TurnSequence,
            turn.Locale,
            turn.VisibleStoryMarkdown,
            turn.DecisionPrompt,
            projection.VisibleChapters.ToArray(),
            choices,
            provenance.State,
            providerDisplay,
            provenance.AffectsMechanics,
            selectedChoiceId,
            checkpoint.PendingPreview?.PreviewDigest,
            turn.SeedDigest,
            checkpoint.CheckpointDigest);
    }
}

public static class OriginDossierNarrativeRenderContractNames
{
    public const string RequestV1 = "chummer.origin-dossier.narrative-render-request/v1";
    public const string ResponseV1 = "chummer.origin-dossier.narrative-render-response/v1";
}

public sealed record OriginDossierNarrativeLocaleBinding(
    string FormattingLocale,
    string ResourceLanguage,
    bool UsesEnglishFallback)
{
    public bool CanRenderNarrativeLocale(string narrativeLocale)
        => string.Equals(
            OriginDossierNarrativeLocalePolicy.PrimaryLanguage(narrativeLocale),
            ResourceLanguage,
            StringComparison.Ordinal);
}

/// <summary>
/// Bounded Origin/Stories language matrix. German, English, and Spanish regional variants use
/// their matching language resources while retaining the normalized regional formatting locale.
/// Every other valid locale is explicitly bound to English resources.
/// </summary>
public static class OriginDossierNarrativeLocalePolicy
{
    private static readonly HashSet<string> SupportedResources = new(
        new[] { "de", "en", "es" },
        StringComparer.Ordinal);

    public static OriginDossierNarrativeLocaleBinding Resolve(string activeAppLocale)
    {
        string formattingLocale = Normalize(activeAppLocale);
        string requestedLanguage = PrimaryLanguage(formattingLocale);
        bool fallback = !SupportedResources.Contains(requestedLanguage);
        return new(
            formattingLocale,
            fallback ? "en" : requestedLanguage,
            fallback);
    }

    public static string PrimaryLanguage(string locale)
        => Normalize(locale).Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[0];

    private static string Normalize(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new InvalidOperationException("An active app locale is required for Origin rendering.");
        string[] segments = locale.Trim().Replace('_', '-').Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0
            || segments.Any(static segment => segment.Length is < 2 or > 8
                || segment.Any(static character => !char.IsLetterOrDigit(character))))
        {
            throw new InvalidOperationException("The active app locale is invalid.");
        }
        return string.Join(
            '-',
            segments.Select(static (segment, index) => index == 0
                ? segment.ToLowerInvariant()
                : segment.Length == 2
                    ? segment.ToUpperInvariant()
                    : segment));
    }
}

/// <summary>
/// Locale-bound rendering input projected only from a sealed Core checkpoint. Stable fact and
/// choice IDs remain the authority identifiers; locale changes copy, never identity or mechanics.
/// ProviderRequested is an explicit opt-in and does not activate a provider by itself.
/// </summary>
public sealed record OriginDossierNarrativeRenderRequest(
    string ContractName,
    string Locale,
    string NarrativeLocale,
    string ResourceLanguage,
    bool UsesEnglishFallback,
    string ArcSeedId,
    string BoundSeedDigest,
    string BoundDecisionGraphDigest,
    IReadOnlyList<string> StableCanonicalFactIds,
    IReadOnlyList<string> StableAcceptedDecisionIds,
    IReadOnlyList<string> StableChoiceIds,
    bool ProviderRequested,
    string RequestDigest);

/// <summary>
/// Untrusted optional-provider output. It contains prose only; every authority binding is checked
/// before a response can be projected to the phone.
/// </summary>
public sealed record OriginDossierNarrativeProviderCandidate(
    string Locale,
    string BoundRequestDigest,
    string BoundSeedDigest,
    string ProviderRouteReceiptDigest,
    string Markdown,
    IReadOnlyList<string> ReferencedCanonicalFactIds,
    IReadOnlyList<string> ReferencedAcceptedDecisionIds,
    IReadOnlyList<string> ReferencedChoiceIds);

public sealed record OriginDossierNarrativeRenderResponse(
    string ContractName,
    string Locale,
    string NarrativeLocale,
    string ResourceLanguage,
    bool UsesEnglishFallback,
    string BoundRequestDigest,
    string BoundSeedDigest,
    string ProviderRouteReceiptDigest,
    string Markdown,
    IReadOnlyList<string> ReferencedCanonicalFactIds,
    IReadOnlyList<string> ReferencedAcceptedDecisionIds,
    IReadOnlyList<string> ReferencedChoiceIds,
    string ResponseDigest)
{
    public bool AffectsMechanics { get; }
}

/// <summary>
/// Fail-closed language/provenance binding for deterministic local scenes and optional provider
/// proposals. It never invokes a provider and never derives IDs, locale, facts, or choices from
/// model output.
/// </summary>
public static class OriginDossierNarrativeRenderBinding
{
    public static OriginDossierNarrativeRenderRequest CreateRequest(
        LifeModuleOriginDossierDraftCheckpoint checkpoint,
        string activeAppLocale,
        bool providerRequested)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        LifeModuleNarrativeTurnSeed turn = checkpoint.Projection?.CurrentTurn
            ?? throw new InvalidOperationException("The Origin Dossier checkpoint has no current turn.");
        OriginDossierNarrativeLocaleBinding locale =
            OriginDossierNarrativeLocalePolicy.Resolve(activeAppLocale);
        if (!locale.CanRenderNarrativeLocale(turn.Locale)
            || string.IsNullOrWhiteSpace(checkpoint.Projection.ArcSeedId)
            || !DigestEquals(checkpoint.BoundSeedDigest, checkpoint.Projection.SeedDigest)
            || !DigestEquals(checkpoint.BoundDecisionGraphDigest, turn.DecisionGraphDigest)
            || !IsDigest(turn.SeedDigest))
        {
            throw new InvalidOperationException(
                "The Origin Dossier scene is not bound to the active app language and sealed authority.");
        }

        string[] factIds = ExactIds(checkpoint.Projection.AllowedCanonicalFactIds, "canonical fact");
        string[] acceptedDecisionIds = ExactIds(turn.AcceptedDecisionIds, "accepted decision");
        string[] choiceIds = ExactIds(checkpoint.Projection.AllowedChoiceIds, "choice");
        OriginDossierNarrativeRenderRequest request = new(
            OriginDossierNarrativeRenderContractNames.RequestV1,
            locale.FormattingLocale,
            turn.Locale,
            locale.ResourceLanguage,
            locale.UsesEnglishFallback,
            checkpoint.Projection.ArcSeedId,
            checkpoint.Projection.SeedDigest,
            turn.DecisionGraphDigest,
            factIds,
            acceptedDecisionIds,
            choiceIds,
            providerRequested,
            string.Empty);
        return request with { RequestDigest = ComputeRequestDigest(request) };
    }

    public static OriginDossierNarrativeRenderResponse BindProviderResponse(
        OriginDossierNarrativeRenderRequest request,
        OriginDossierNarrativeProviderCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);
        OriginDossierNarrativeLocaleBinding locale;
        try
        {
            locale = OriginDossierNarrativeLocalePolicy.Resolve(request.Locale);
            ExactIds(request.StableCanonicalFactIds, "canonical fact");
            ExactIds(request.StableAcceptedDecisionIds, "accepted decision");
            ExactIds(request.StableChoiceIds, "choice");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentNullException)
        {
            throw new InvalidOperationException("The narrative render request is invalid.");
        }
        if (!request.ProviderRequested
            || !string.Equals(request.ContractName, OriginDossierNarrativeRenderContractNames.RequestV1, StringComparison.Ordinal)
            || !string.Equals(locale.ResourceLanguage, request.ResourceLanguage, StringComparison.Ordinal)
            || locale.UsesEnglishFallback != request.UsesEnglishFallback
            || !locale.CanRenderNarrativeLocale(request.NarrativeLocale)
            || string.IsNullOrWhiteSpace(request.ArcSeedId)
            || !IsDigest(request.BoundSeedDigest)
            || !IsDigest(request.BoundDecisionGraphDigest)
            || !DigestEquals(request.RequestDigest, ComputeRequestDigest(request))
            || !LocaleEquals(request.Locale, candidate.Locale)
            || !DigestEquals(request.RequestDigest, candidate.BoundRequestDigest)
            || !DigestEquals(request.BoundSeedDigest, candidate.BoundSeedDigest)
            || !IsDigest(candidate.ProviderRouteReceiptDigest)
            || string.IsNullOrWhiteSpace(candidate.Markdown))
        {
            throw new InvalidOperationException(
                "The optional narrative response is not bound to the requested locale and seed.");
        }

        string[] factIds = ExactSubset(
            candidate.ReferencedCanonicalFactIds,
            request.StableCanonicalFactIds,
            "canonical fact");
        string[] acceptedDecisionIds = ExactSubset(
            candidate.ReferencedAcceptedDecisionIds,
            request.StableAcceptedDecisionIds,
            "accepted decision");
        string[] choiceIds = ExactSubset(
            candidate.ReferencedChoiceIds,
            request.StableChoiceIds,
            "choice");
        OriginDossierNarrativeRenderResponse response = new(
            OriginDossierNarrativeRenderContractNames.ResponseV1,
            request.Locale,
            request.NarrativeLocale,
            request.ResourceLanguage,
            request.UsesEnglishFallback,
            request.RequestDigest,
            request.BoundSeedDigest,
            candidate.ProviderRouteReceiptDigest,
            candidate.Markdown,
            factIds,
            acceptedDecisionIds,
            choiceIds,
            string.Empty);
        return response with { ResponseDigest = ComputeResponseDigest(response) };
    }

    private static string[] ExactSubset(
        IReadOnlyList<string> candidate,
        IReadOnlyList<string> allowed,
        string kind)
    {
        string[] ids = ExactIds(candidate, kind);
        HashSet<string> allowedIds = new(allowed, StringComparer.Ordinal);
        if (ids.Any(id => !allowedIds.Contains(id)))
        {
            throw new InvalidOperationException($"The narrative response referenced an unknown {kind} ID.");
        }
        return ids;
    }

    private static string[] ExactIds(IReadOnlyList<string> values, string kind)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Any(value => !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new InvalidOperationException($"The narrative request has invalid stable {kind} IDs.");
        }
        return values.ToArray();
    }

    private static string ComputeRequestDigest(OriginDossierNarrativeRenderRequest request)
        => ComputeDigest(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("contractName", request.ContractName);
            writer.WriteString("locale", request.Locale);
            writer.WriteString("narrativeLocale", request.NarrativeLocale);
            writer.WriteString("resourceLanguage", request.ResourceLanguage);
            writer.WriteBoolean("usesEnglishFallback", request.UsesEnglishFallback);
            writer.WriteString("arcSeedId", request.ArcSeedId);
            writer.WriteString("boundSeedDigest", request.BoundSeedDigest);
            writer.WriteString("boundDecisionGraphDigest", request.BoundDecisionGraphDigest);
            WriteIds(writer, "stableCanonicalFactIds", request.StableCanonicalFactIds);
            WriteIds(writer, "stableAcceptedDecisionIds", request.StableAcceptedDecisionIds);
            WriteIds(writer, "stableChoiceIds", request.StableChoiceIds);
            writer.WriteBoolean("providerRequested", request.ProviderRequested);
            writer.WriteEndObject();
        });

    private static string ComputeResponseDigest(OriginDossierNarrativeRenderResponse response)
        => ComputeDigest(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("contractName", response.ContractName);
            writer.WriteString("locale", response.Locale);
            writer.WriteString("narrativeLocale", response.NarrativeLocale);
            writer.WriteString("resourceLanguage", response.ResourceLanguage);
            writer.WriteBoolean("usesEnglishFallback", response.UsesEnglishFallback);
            writer.WriteString("boundRequestDigest", response.BoundRequestDigest);
            writer.WriteString("boundSeedDigest", response.BoundSeedDigest);
            writer.WriteString("providerRouteReceiptDigest", response.ProviderRouteReceiptDigest);
            writer.WriteString("markdown", response.Markdown);
            WriteIds(writer, "referencedCanonicalFactIds", response.ReferencedCanonicalFactIds);
            WriteIds(writer, "referencedAcceptedDecisionIds", response.ReferencedAcceptedDecisionIds);
            WriteIds(writer, "referencedChoiceIds", response.ReferencedChoiceIds);
            writer.WriteEndObject();
        });

    private static void WriteIds(Utf8JsonWriter writer, string name, IReadOnlyList<string> ids)
    {
        writer.WriteStartArray(name);
        foreach (string id in ids)
            writer.WriteStringValue(id);
        writer.WriteEndArray();
    }

    private static string ComputeDigest(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    private static bool LocaleEquals(string left, string right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool DigestEquals(string left, string right)
        => IsDigest(left)
           && IsDigest(right)
           && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsDigest(string value)
        => value.Length == 64 && value.All(static character => Uri.IsHexDigit(character));
}
