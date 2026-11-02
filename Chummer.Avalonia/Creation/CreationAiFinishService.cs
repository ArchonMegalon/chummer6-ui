using System.Collections.Immutable;
using System.Globalization;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Creation;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// Build Ghost AI-finish orchestrator for the creation wizard.
///
/// Builds a deterministic missing-only proposal (CompletionRecommendationBuilder)
/// from the live completion inputs, presents it for review, and applies the
/// user-confirmed plan through the creation services' own Loadâ†’Previewâ†’Confirm
/// flows with snapshot guards (inputs digest re-verified before apply; each
/// domain previews immediately before its confirm).
///
/// Contract notes honored:
/// - Core's plan is "not an Apply capability": this orchestrator translates the
///   reviewed plan into per-domain preview/confirm requests on the live session.
///   The Finalize step remains the separate finalization review + explicit confirm.
/// - Missing-only: the plan retains original player choices (magic is never
///   re-selected â€” the builder requires magic to be already chosen and never
///   proposes changes to it).
/// - Permissions are exactly the built-in pack's additive permissions.
/// - Domains that cannot be applied unambiguously (contacts, lifestyles) are
///   reported as manual follow-ups instead of being applied.
/// </summary>
public sealed class CreationAiFinishService
{
    private readonly IWorkspaceStore _store;
    private readonly ICharacterSourceDataResolver _resolver;
    private readonly ICharacterFileQueries _queries;
    private readonly IOwnerContextLeaseAccessor _ownerContextAccessor;

    private const string KeyPrefix = "ai-finish:";

    public CreationAiFinishService(
        IWorkspaceStore store,
        ICharacterSourceDataResolver resolver,
        ICharacterFileQueries queries,
        IOwnerContextLeaseAccessor ownerContextAccessor)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _ownerContextAccessor = ownerContextAccessor ?? throw new ArgumentNullException(nameof(ownerContextAccessor));
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
        CharacterCreationCompletionPlan Plan,
        CompletionArchetypeSuggestion Archetype,
        ImmutableArray<AiFinishRecommendation> Recommendations,
        ImmutableArray<string> ManualFollowUps,
        bool NeedsChanges);

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

        var inputs = LoadInputs(workspaceId);
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
            _ownerContextAccessor.Capture(),
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

        return new AiFinishProposal(
            plan,
            suggestion,
            recommendations.ToImmutableArray(),
            manual.ToImmutableArray(),
            needsChanges);
    }

    /// <summary>
    /// Applies the reviewed plan to the live session through each domain's own
    /// Loadâ†’Previewâ†’Confirm flow, in dependency order (attributes â†’ skills â†’
    /// qualities â†’ resources â†’ gear). Re-verifies the inputs digest first so any
    /// concurrent edit aborts before the first mutation. Each domain reports
    /// independently: a domain failure does not roll back earlier domains (the
    /// wizard surfaces per-domain results and the user can review and retry).
    /// </summary>
    public IReadOnlyList<AiFinishDomainResult> Apply(string workspaceId, AiFinishProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        // Snapshot guard: the live inputs must still digest to what was reviewed.
        var current = LoadInputs(workspaceId);
        if (!string.Equals(current.SnapshotDigest, proposal.Plan.Request.InputsSnapshotDigest, StringComparison.Ordinal))
        {
            return
            [
                new AiFinishDomainResult(
                    "snapshot",
                    false,
                    CharacterCreationFoundationOutcomes.Conflict,
                    ["ai-finish: inputs changed since the proposal was reviewed"]),
            ];
        }

        var results = new List<AiFinishDomainResult>();
        var selections = proposal.Plan.Selections;

        results.Add(ApplyAttributes(workspaceId, selections.AttributeAllocations));
        if (!results[^1].Success)
        {
            // Attribute state anchors downstream previews (prerequisites, karma);
            // do not cascade confusing failures into skills/qualities.
            return results;
        }

        results.Add(ApplySkills(workspaceId, selections.SkillAllocations, selections.GroupAllocations));
        results.Add(ApplyQualities(workspaceId, selections.QualityOptionIds));
        results.Add(ApplyResources(workspaceId, proposal));
        results.Add(ApplyGear(workspaceId, selections.Gear));

        return results;
    }

    private CharacterCreationCompletionReviewInputs LoadInputs(string workspaceId)
    {
        var reader = new OwnerBoundCharacterCreationCompletionReader(
            _store, _ownerContextAccessor, _resolver, _queries);
        var loaded = reader.LoadCompletionInputs(
            _ownerContextAccessor.Capture(),
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
                    + (skills.Allocations.Count > 6 ? " â€¦" : string.Empty);
            default:
                return category;
        }
    }

    private AiFinishDomainResult ApplyAttributes(
        string workspaceId,
        IReadOnlyList<CharacterCreationAttributeAllocation> allocations)
    {
        var service = new CharacterCreationAttributesService(_store, _resolver);
        var state = service.Load(new CharacterCreationAttributesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("attributes", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(new(state.Value.Binding, allocations));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("attributes", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(new(
            preview.Value.Binding, allocations, preview.Value.PreviewDigest, true));
        return new("attributes", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplySkills(
        string workspaceId,
        IReadOnlyList<CharacterCreationSkillAllocation> allocations,
        IReadOnlyList<CharacterCreationSkillGroupAllocation> groups)
    {
        var service = new CharacterCreationSkillsService(_store, _resolver);
        var state = service.Load(new CharacterCreationSkillsLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("skills", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(new(state.Value.Binding, allocations, groups));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("skills", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(new(
            preview.Value.Binding, allocations, groups, preview.Value.PreviewDigest,
            NewIdempotencyKey("skills"), true));
        return new("skills", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyQualities(string workspaceId, IReadOnlyList<string> qualityOptionIds)
    {
        var prerequisites = new CharacterCreationPrerequisiteService(_store, _queries, _resolver);
        var attributes = new CharacterCreationAttributesService(_store, _resolver);
        var service = new CharacterCreationQualitiesService(_store, _resolver, prerequisites, attributes);
        var state = service.Load(new CharacterCreationQualitiesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("qualities", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(new(state.Value.Binding, qualityOptionIds));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("qualities", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(new(
            preview.Value.Binding, qualityOptionIds, preview.Value.PreviewDigest,
            NewIdempotencyKey("qualities"), Guid.NewGuid(), true));
        return new("qualities", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyResources(string workspaceId, AiFinishProposal proposal)
    {
        var service = new CharacterCreationResourcesService(_store, _resolver);
        var state = service.Load(new CharacterCreationResourcesLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("resources", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        // The plan binds the resources slice in its review balances; pick the
        // enabled option whose karma investment matches the reviewed total. No
        // match means the plan cannot be translated unambiguously â€” report, don't guess.
        var target = proposal.Plan.Selections.TotalKarmaInvestment;
        var option = state.Value.Options.FirstOrDefault(row => row.IsEnabled && row.KarmaInvestment == target);
        if (option is null)
        {
            return new("resources", false, CharacterCreationFoundationOutcomes.Blocked,
            [
                "ai-finish: no enabled resources option matches the reviewed karma investment",
            ]);
        }

        var preview = service.Preview(new(state.Value.Binding, option.OptionId));
        if (preview.Value is null)
        {
            return new("resources", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(new(
            preview.Value.Binding, option.OptionId, preview.Value.PreviewDigest,
            NewIdempotencyKey("resources"), true));
        return new("resources", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private AiFinishDomainResult ApplyGear(
        string workspaceId,
        IReadOnlyList<CharacterCreationGearSelection> gear)
    {
        var service = new CharacterCreationGearService(_store, _resolver);
        var state = service.Load(new CharacterCreationGearLoadRequest(new CharacterWorkspaceId(workspaceId)));
        if (state.Value is null)
        {
            return new("gear", false, state.Outcome, state.Blockers.ToImmutableArray());
        }

        var preview = service.Preview(new(state.Value.Binding, gear));
        if (preview.Value is null || !preview.Value.CanConfirm)
        {
            return new("gear", false, preview.Outcome, preview.Blockers.ToImmutableArray());
        }

        var confirm = service.Confirm(new(
            preview.Value.Binding, gear, preview.Value.PreviewDigest,
            NewIdempotencyKey("gear"), true));
        return new("gear", confirm.Value is not null, confirm.Outcome, confirm.Blockers.ToImmutableArray());
    }

    private static string NewIdempotencyKey(string domain) =>
        KeyPrefix + domain + ":" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
