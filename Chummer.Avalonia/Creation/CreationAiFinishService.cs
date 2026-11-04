using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Creation;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// Gated, staged AI completion. Domain writes remain Core-owned and owner-bound.
/// Stop at the first failed or uncertain result; retain a durable no-replay record.
/// This is not the future atomic completion API or complete AI release qualification.
/// </summary>
public sealed class CreationAiFinishService
{
    private readonly IWorkspaceStore _store;
    private readonly ICharacterSourceDataResolver _resolver;
    private readonly ICharacterFileQueries _queries;
    private readonly IOwnerContextLeaseAccessor _ownerContextAccessor;

    private readonly CreationAiFinishRunStore _runs;

    private const string KeyPrefix = "ai-finish:";

    public CreationAiFinishService(
        IWorkspaceStore store,
        ICharacterSourceDataResolver resolver,
        ICharacterFileQueries queries,
        IOwnerContextLeaseAccessor ownerContextAccessor,
        CreationAiFinishRunStore? runs = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _ownerContextAccessor = ownerContextAccessor ?? throw new ArgumentNullException(nameof(ownerContextAccessor));
        _runs = runs ?? CreationAiFinishRunStore.ForCurrentProfile();
    }

    /// <summary>
    /// One actionable recommendation for the review dialog: domain, label,
    /// rationale (localized reason key from the pack), and how confirm applies it.
    /// </summary>
    public sealed record AiFinishRecommendation(
        string Domain,
        string Label,
        string Rationale,
        string ApplyAction);

    /// <summary>
    /// A missing-only proposal for the current inputs: the Core-evaluated plan,
    /// the archetype suggestion, the review rows, and remaining manual tasks.
    /// </summary>
    public sealed record AiFinishProposal(
        string OperationId,
        CharacterCreationCompletionPlan Plan,
        CompletionArchetypeSuggestion Archetype,
        ImmutableArray<AiFinishRecommendation> Recommendations,
        ImmutableArray<string> ManualFollowUps,
        bool NeedsChanges)
    {
        public OwnerContextStamp ExpectedOwner { get; init; }
        public string WorkspaceId { get; init; } = string.Empty;
        public string PreparedPlanDigest { get; init; } = string.Empty;
    }

    /// <summary>
    /// Outcome of applying one domain of the plan through its creation service.
    /// </summary>
    public sealed record AiFinishDomainResult(
        string Domain,
        bool Success,
        string Outcome,
        ImmutableArray<string> Blockers);

    /// <summary>
    /// Raised when a stage fails unrecoverably (inputs load, suggestion, build).
    /// Outcome is a foundation outcome code; Blockers are rule blocker texts.
    /// </summary>
    public sealed class CreationAiFinishException : Exception
    {
        public CreationAiFinishException(string stage, string outcome, IReadOnlyList<string> blockers)
            : base(
                stage + " failed: " + outcome +
                (blockers.Count > 0 ? " (" + string.Join("; ", blockers) + ")" : string.Empty))
        {
            Stage = stage;
            Outcome = outcome;
            Blockers = blockers.ToImmutableArray();
        }

        public string Stage { get; }
        public string Outcome { get; }
        public ImmutableArray<string> Blockers { get; }
    }

    /// <summary>
    /// Loads live completion inputs, suggests an archetype from observation,
    /// builds the missing-only proposal, and flattens it into review rows.
    /// </summary>
    public AiFinishProposal BuildProposal(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace id required.", nameof(workspaceId));
        }

        var expectedOwner = _ownerContextAccessor.Capture();
        RequireOwner(expectedOwner);
        var inputs = LoadInputs(workspaceId, expectedOwner);
        if (_runs.Read(expectedOwner, workspaceId) is not null)
            throw new CreationAiFinishException("recovery", "saved-operation",
                ["An AI completion operation already exists for this character. Inspect its saved result; another plan will not be dispatched over a partial result."]);

        var suggestionResult = CompletionRecommendationBuilder.SuggestArchetype(inputs);
        if (suggestionResult.Value is null)
        {
            throw new CreationAiFinishException(
                "suggest-archetype", suggestionResult.Outcome, suggestionResult.Blockers);
        }

        var suggestion = suggestionResult.Value;
        var pack = CompletionRecommendationBuilder.GetRecommendationPack(suggestion.ArchetypeId)
            ?? throw new CreationAiFinishException(
                "resolve-pack", CharacterCreationFoundationOutcomes.Blocked, [suggestion.ArchetypeId]);

        var intent = new CompletionAssistantIntent(
            expectedOwner,
            inputs.SnapshotDigest,
            suggestion.ArchetypeId,
            Permissions(pack),
            new())
        {
            NativeLanguageSourceId = NativeLanguageSourceId(inputs),
        };

        var proposalResult = CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent);
        if (proposalResult.Value is null)
        {
            throw new CreationAiFinishException(
                "build-missing-only", proposalResult.Outcome, proposalResult.Blockers);
        }

        var proposal = proposalResult.Value;
        if (!proposal.CanReview || proposal.Evaluation.Value is not { } plan)
        {
            throw new CreationAiFinishException(
                "evaluate-plan", proposal.Evaluation.Outcome, proposal.Evaluation.Blockers);
        }

        var recommendations = new List<AiFinishRecommendation>();
        var manual = new List<string>();
        var needsChanges = false;

        foreach (var coverage in proposal.Coverage)
        {
            if (coverage.State == CompletionCoverageState.NeedsChoice)
            {
                needsChanges = true;
                recommendations.Add(new AiFinishRecommendation(
                    coverage.Category,
                    Labels(coverage.Category, plan),
                    coverage.ExplanationKey,
                    ApplyAction(coverage.Category)));
            }
            else if (coverage.State == CompletionCoverageState.OptionalManual
                && coverage.Category is "contacts" or "lifestyles")
            {
                manual.Add(coverage.Category + ": " + coverage.ExplanationKey);
            }
        }

        foreach (var task in proposal.RemainingTasks)
        {
            manual.Add(task);
        }

        RequireOwner(expectedOwner);
        return new AiFinishProposal(
            DeriveOperationId(workspaceId, inputs.SnapshotDigest),
            plan,
            suggestion,
            recommendations.ToImmutableArray(),
            manual.ToImmutableArray(),
            needsChanges)
        {
            ExpectedOwner = expectedOwner,
            WorkspaceId = workspaceId,
            PreparedPlanDigest = PlanDigest(plan)
        };
    }

    public bool IsCurrentOwner(OwnerContextStamp expectedOwner) =>
        CreationAiFinishRunStore.IsLinkedOwner(expectedOwner)
        && _ownerContextAccessor.Capture() == expectedOwner;

    private void RequireOwner(OwnerContextStamp expectedOwner)
    {
        if (!IsCurrentOwner(expectedOwner))
            throw new InvalidOperationException("Link your account and reopen the character before using AI completion.");
    }

    private static string PlanDigest(CharacterCreationCompletionPlan plan) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));

    /// <summary>Fresh authorization, read-only reload, and honest recorded outcomes.
    /// A recorded success is not a new Core receipt; an unobserved write stays uncertain.
    /// No pending record is cleared or converted into permission to replay.</summary>
    public IReadOnlyList<AiFinishDomainResult>? InspectSavedResult(string workspaceId, OwnerContextStamp expectedOwner)
    {
        RequireOwner(expectedOwner);
        _ = LoadInputs(workspaceId, expectedOwner);
        var entries = _runs.Read(expectedOwner, workspaceId);
        RequireOwner(expectedOwner);
        if (entries is null) return null;
        var results = entries.Where(e => e.Kind == "observed" && e.Result is not null).Select(e => e.Result!).ToList();
        string? outstanding = entries.LastOrDefault(e => e.Kind == "dispatching")?.Domain;
        if (outstanding is not null && !results.Any(r => r.Domain == outstanding))
            results.Add(new(outstanding, false, CharacterCreationFoundationOutcomes.Invalid,
                ["The process stopped before the saved outcome was observed. The current character was loaded read-only; no retry was sent."]));
        if (entries[^1].Kind != "completed")
            results.Add(new("recovery", false, CharacterCreationFoundationOutcomes.Blocked,
                ["Partial operation retained. Inspect the current character manually; automatic continuation is unavailable in this gated version."]));
        return results;
    }

    /// <summary>
    /// Qualified durable staged resumption entry: with fresh authorization, only
    /// the single uncertain domain of the SAVED frozen plan is re-dispatched,
    /// under the SAME operation identity, inside the SAME exclusive fence.
    /// Exact receipt reconciliation happens in the run store. Everything else —
    /// including the retained read-only inspection contract — is unchanged.
    /// </summary>
    public CreationAiFinishRunStore.ResumptionReceipt ResumeSavedResult(string workspaceId)
    {
        var expectedOwner = _ownerContextAccessor.Capture();
        RequireOwner(expectedOwner);
        var entries = _runs.Read(expectedOwner, workspaceId)
            ?? throw new InvalidOperationException(
                "No saved AI completion operation exists for this character; use the normal review flow instead.");
        if (entries[0].OperationId is not { Length: > 0 } savedOperationId)
            throw new InvalidOperationException("The saved record has no operation identity; resumption is unavailable.");
        string frozenPlanJson = entries.FirstOrDefault(e => e.Kind == "claimed")?.PlanJson
            ?? throw new InvalidOperationException("The saved record has no frozen plan; resumption is unavailable.");

        // The frozen plan is the qualification: deserialize it and rebuild the
        // exact same per-domain steps as Apply, then let the run store reconcile
        // and continue only the uncertain domain.
        var plan = JsonSerializer.Deserialize<CharacterCreationCompletionPlan>(frozenPlanJson)
            ?? throw new InvalidOperationException("The frozen plan is unreadable; resumption is unavailable.");
        var proposal = new AiFinishProposal(
            savedOperationId, plan,
            Archetype: null,
            Recommendations: ImmutableArray<AiFinishRecommendation>.Empty,
            ManualFollowUps: ImmutableArray<string>.Empty,
            NeedsChanges: false)
        {
            ExpectedOwner = expectedOwner,
            WorkspaceId = workspaceId,
            PreparedPlanDigest = PlanDigest(plan),
        };
        return _runs.ReconcileAndResume(expectedOwner, workspaceId, savedOperationId, frozenPlanJson,
        [
            ("attributes", () => ApplyAttributes(expectedOwner, workspaceId, savedOperationId, plan.Selections.AttributeAllocations)),
            ("skills", () => ApplySkills(expectedOwner, workspaceId, savedOperationId, plan.Selections.SkillAllocations, plan.Selections.GroupAllocations)),
            ("qualities", () => ApplyQualities(expectedOwner, workspaceId, savedOperationId, plan.Selections.QualityOptionIds)),
            ("resources", () => ApplyResources(expectedOwner, workspaceId, savedOperationId, proposal)),
            ("gear", () => ApplyGear(expectedOwner, workspaceId, savedOperationId, plan.Selections.Gear))
        ]);
    }

    public IReadOnlyList<AiFinishDomainResult> Apply(string workspaceId, AiFinishProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        RequireOwner(proposal.ExpectedOwner);
        if (workspaceId != proposal.WorkspaceId
            || proposal.OperationId != DeriveOperationId(workspaceId, proposal.Plan.Request.InputsSnapshotDigest)
            || PlanDigest(proposal.Plan) != proposal.PreparedPlanDigest)
            return [new("review", false, CharacterCreationFoundationOutcomes.Conflict, ["The reviewed character or plan changed. No changes were applied."])];
        // Check the durable fence BEFORE the old inputs digest: partial commits
        // change that digest and must not mint a replacement operation.
        if (InspectSavedResult(workspaceId, proposal.ExpectedOwner) is { } saved) return saved;
        var current = LoadInputs(workspaceId, proposal.ExpectedOwner);
        if (current.SnapshotDigest != proposal.Plan.Request.InputsSnapshotDigest)
            return [new("snapshot", false, CharacterCreationFoundationOutcomes.Conflict, ["The character changed after review. No changes were applied."])];

        var selections = proposal.Plan.Selections;
        var operationId = proposal.OperationId;
        var owner = proposal.ExpectedOwner;
        return _runs.Execute(owner, workspaceId, operationId, JsonSerializer.Serialize(proposal.Plan),
        [
            ("attributes", () => ApplyAttributes(owner, workspaceId, operationId, selections.AttributeAllocations)),
            ("skills", () => ApplySkills(owner, workspaceId, operationId, selections.SkillAllocations, selections.GroupAllocations)),
            ("qualities", () => ApplyQualities(owner, workspaceId, operationId, selections.QualityOptionIds)),
            ("resources", () => ApplyResources(owner, workspaceId, operationId, proposal)),
            ("gear", () => ApplyGear(owner, workspaceId, operationId, selections.Gear))
        ]);
    }

    private CharacterCreationCompletionReviewInputs LoadInputs(string workspaceId, OwnerContextStamp expectedOwner)
    {
        var reader = new OwnerBoundCharacterCreationCompletionReader(
            _store, _ownerContextAccessor, _resolver, _queries);
        var loaded = reader.LoadCompletionInputs(
            expectedOwner,
            new CharacterCreationSkillsLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (loaded.Value is null)
        {
            throw new CreationAiFinishException("load-completion-inputs", loaded.Outcome, loaded.Blockers);
        }

        return loaded.Value;
    }

    private static string NativeLanguageSourceId(CharacterCreationCompletionReviewInputs inputs)
    {
        var native = inputs.Skills.Authority.KnowledgeSkills.FirstOrDefault(row => row.CanBeNativeLanguage);
        return native?.SourceSkillId ?? string.Empty;
    }

    private static CompletionAdditivePermissions Permissions(CompletionRecommendationPack pack) =>
        new(
            pack.Attributes.Select(row => row.Target.AttributeId).ToArray(),
            pack.Skills.Select(row => new CharacterCreationSkillCompletionTarget(row.Target.Kind, row.Target.SourceId)).ToArray());

    private static string ApplyAction(string category) => category switch
    {
        "attributes" => "Set recommended ratings for untouched attributes only",
        "skills" => "Add recommended skill ranks for untouched skills only",
        "qualities" => "Keep your qualities; add only plan-required ones",
        "resources" => "Select the reviewed karma-investment option",
        "gear" => "Apply the reviewed gear basket",
        _ => "Review and apply",
    };

    private static string Labels(string category, CharacterCreationCompletionPlan plan)
    {
        switch (category)
        {
            case "attributes" when plan.Attributes is { } attributes:
                return string.Join(
                    ", ",
                    attributes.Allocations.Select(row => row.AttributeId + " " + (row.PriorityPoints + row.KarmaLevels).ToString(CultureInfo.InvariantCulture)));
            case "skills" when plan.Skills is { } skills:
                return string.Join(
                    ", ",
                    skills.Allocations.Take(6).Select(row => row.SourceSkillId + " " + (row.Rating?.ToString(CultureInfo.InvariantCulture) ?? "native")))
                    + (skills.Allocations.Count > 6 ? " …" : string.Empty);
            default:
                return category;
        }
    }

    private AiFinishDomainResult ApplyAttributes(
        OwnerContextStamp expectedOwner,
        string workspaceId,
        string operationId,
        IReadOnlyList<CharacterCreationAttributeAllocation> allocations)
    {

        var service = new OwnerBoundCharacterCreationAttributesService(_store, _ownerContextAccessor, _resolver);
        var state = service.Load(expectedOwner, new CharacterCreationAttributesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("attributes", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(expectedOwner, new(state.Value.Binding, allocations));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("attributes", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        // The durable run fence prevents a second dispatch; a digest alone is not recovery.
        var confirm = service.Confirm(expectedOwner, new(
            preview.Value.Binding, allocations, preview.Value.PreviewDigest, true));
        return new("attributes", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplySkills(
        OwnerContextStamp expectedOwner,
        string workspaceId,
        string operationId,
        IReadOnlyList<CharacterCreationSkillAllocation> allocations,
        IReadOnlyList<CharacterCreationSkillGroupAllocation> groups)
    {
        var service = new OwnerBoundCharacterCreationSkillsService(_store, _ownerContextAccessor, _resolver);
        var state = service.Load(expectedOwner, new CharacterCreationSkillsLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("skills", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(expectedOwner, new(state.Value.Binding, allocations, groups));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("skills", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(expectedOwner, new(
            preview.Value.Binding, allocations, groups, preview.Value.PreviewDigest,
            OperationKey(operationId, "skills"), true));
        return new("skills", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyQualities(
        OwnerContextStamp expectedOwner,
        string workspaceId,
        string operationId,
        IReadOnlyList<string> qualityOptionIds)
    {
        var service = new OwnerBoundCharacterCreationQualitiesService(_store, _ownerContextAccessor, _queries, _resolver);
        var state = service.Load(expectedOwner, new CharacterCreationQualitiesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("qualities", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(expectedOwner, new(state.Value.Binding, qualityOptionIds));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("qualities", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        // Stable operation identities supplement the durable fence; they do not authorize retries.
        var confirm = service.Confirm(expectedOwner, new(
            preview.Value.Binding, qualityOptionIds, preview.Value.PreviewDigest,
            OperationKey(operationId, "qualities"), OperationGuid(operationId, "qualities"), true));
        return new("qualities", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyResources(
        OwnerContextStamp expectedOwner,
        string workspaceId,
        string operationId,
        AiFinishProposal proposal)
    {
        var service = new OwnerBoundCharacterCreationResourcesService(_store, _ownerContextAccessor, _resolver);
        var state = service.Load(expectedOwner, new CharacterCreationResourcesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("resources", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        // The plan binds the resources slice in its review balances; pick the
        // enabled option whose karma investment matches the reviewed total. No
        // match means the plan cannot be translated unambiguously — report, don't guess.
        var target = proposal.Plan.Selections.TotalKarmaInvestment;
        var option = state.Value.Options.FirstOrDefault(row => row.IsEnabled && row.KarmaInvestment == target);
        if (option is null)
        {
            return new("resources", false, CharacterCreationFoundationOutcomes.Blocked,
            [
                "ai-finish: no enabled resources option matches the reviewed karma investment",
            ]);
        }

        var preview = service.Preview(expectedOwner, new(state.Value.Binding, option.OptionId));
        if (preview.Value is null)
        {
            return new("resources", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(expectedOwner, new(
            preview.Value.Binding, option.OptionId, preview.Value.PreviewDigest,
            OperationKey(operationId, "resources"), true));
        return new("resources", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyGear(
        OwnerContextStamp expectedOwner,
        string workspaceId,
        string operationId,
        IReadOnlyList<CharacterCreationGearSelection> gear)
    {
        var service = new OwnerBoundCharacterCreationGearService(_store, _ownerContextAccessor, _resolver);
        var state = service.Load(expectedOwner, new CharacterCreationGearLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("gear", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(expectedOwner, new(state.Value.Binding, gear));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("gear", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(expectedOwner, new(
            preview.Value.Binding, gear, preview.Value.PreviewDigest,
            OperationKey(operationId, "gear"), true));
        return new("gear", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    // Stable command identities are retained for inspection, never blind replay.
    private static string OperationKey(string operationId, string domain) =>
        KeyPrefix + domain + ":" + operationId;

    /// <summary>
    /// Stable per-domain transaction Guid: same derivation contract as OperationKey.
    /// Built from the first 16 bytes of SHA-256 over the operation id + domain, which
    /// is collision-resistant for this use and deterministic across processes.
    /// </summary>
    private static Guid OperationGuid(string operationId, string domain)
    {
#if NET10_0_OR_GREATER
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(operationId + "|" + domain));
#else
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(operationId + "|" + domain));
#endif
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Deterministic operation identity: SHA-256 over workspaceId + inputs snapshot
    /// digest, hex-encoded. Same inputs + same workspace always derive the same
    /// identity; any inputs change mints a new one.
    /// </summary>
    private static string DeriveOperationId(string workspaceId, string inputsSnapshotDigest)
    {
#if NET10_0_OR_GREATER
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId + "|" + inputsSnapshotDigest));
#else
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(workspaceId + "|" + inputsSnapshotDigest));
#endif
        var sb = new System.Text.StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
