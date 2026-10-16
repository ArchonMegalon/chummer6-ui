using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

/// <summary>Pure, providerless advice over one original Core observation. This
/// layer chooses goals, not prices, caps or legality. Core evaluates the complete
/// proposal, and every unsupported part stays visible for the caller.</summary>
public static partial class CompletionRecommendationBuilder
{
    private static readonly CompletionRecommendationPack Survivor = new(
        "sr5.general-survivor.foundation", "1", CompletionAssistantRules.GeneralSurvivor,
        [
            new(new("BOD", 3), "completion.reason.resilience"),
            new(new("REA", 3), "completion.reason.reaction"),
            new(new("INT", 3), "completion.reason.awareness"),
            new(new("WIL", 3), "completion.reason.resolve"),
            new(new("AGI", 3), "completion.reason.coordination"),
            new(new("CHA", 2), "completion.reason.social-baseline"),
            new(new("LOG", 2), "completion.reason.problem-solving"),
            new(new("STR", 2), "completion.reason.physical-baseline")
        ],
        [
            new(new(CharacterCreationSkillKinds.Active, "04e1eb3e-e82d-485b-a7fd-1e677df2a070", 3), "completion.reason.notice-danger"),
            new(new(CharacterCreationSkillKinds.Active, "9cff9aa7-d092-4f89-8b7b-3ab835818874", 2), "completion.reason.move-unnoticed"),
            new(new(CharacterCreationSkillKinds.Active, "b20acd11-f102-40f3-a641-e3c420fbdb91", 2), "completion.reason.social-baseline"),
            new(new(CharacterCreationSkillKinds.Active, "47cb1e8b-c285-4c54-9aaa-75305ad6dd4f", 1), "completion.reason.basic-first-aid"),
            new(new(CharacterCreationSkillKinds.Active, "a9fa961d-07e5-46da-8edc-403ae3e6cc75", 2), "completion.reason.mobility"),
            new(new(CharacterCreationSkillKinds.Active, "89ee1730-053a-400f-a13a-4fbadae015f0", 1), "completion.reason.basic-survival")
        ]);

    // Return detached advice: consumers cannot change the process-wide pack.
    public static CompletionRecommendationPack GeneralSurvivorPack => Detach(Survivor);

    // Authored SR5 advice keyed to canonical skills.xml identities. These are
    // foundation targets, NOT complete role kits or equipment capability claims.
    private sealed record RoleAdvice(string Id, string[] Attributes, string[] Skills);
    private static readonly RoleAdvice[] Roles =
    [
        new(CompletionAssistantRules.StreetSamurai, ["AGI", "REA", "BOD", "INT"],
            ["788b387b-ee41-4e6a-bf22-481a8cc4cf9f", "adf31a50-b228-4e09-a09c-46ab9f5e59a1",
             "48763fa5-4b89-48c7-80ff-d0a2761de4c0", "4fcd40cb-4b02-4b7e-afcb-f44d46cd5706"]),
        new(CompletionAssistantRules.Face, ["CHA", "INT", "WIL"],
            ["6d7f48d3-84a1-4fce-90d3-58d566f70fa6", "729c9cee-ef8f-492d-aa7f-17ec1bc3816e",
             "963a548d-c629-4a13-a3e3-31b085a42e20", "b20acd11-f102-40f3-a641-e3c420fbdb91"]),
        new(CompletionAssistantRules.Mage, ["WIL", "INT", "BOD"],
            ["c4367a39-4065-4b1d-aa62-e9dce377e452", "3db81bcc-264b-47e1-847c-06bdacd88973",
             "51e34c6c-b07f-45f4-8a5e-8f2b617ed32f", "dfba7c09-3d95-43fd-be75-39b3e8b22cd3"]),
        new(CompletionAssistantRules.Decker, ["LOG", "INT", "WIL"],
            ["c2bb65f5-4a6b-49bf-9925-ef6434cb6929", "7143f979-aa48-4cc8-a29c-e010400e6e11",
             "1c14bf0d-cc69-4126-9a95-1f2429c11aa5", "2c8e5f20-e52d-4844-89e9-51b92dba47df"]),
        new(CompletionAssistantRules.Rigger, ["REA", "LOG", "INT"],
            ["58452cff-44ea-41c6-a554-28a869149b27", "ae91a8a6-80e7-4f52-b9eb-21725a5528a4",
             "10d5c887-a1e5-4cca-8613-3a28f1aab810", "2c8e5f20-e52d-4844-89e9-51b92dba47df"])
    ];

    public static CompletionRecommendationPack? GetRecommendationPack(string archetypeId)
    {
        if (archetypeId == CompletionAssistantRules.GeneralSurvivor) return GeneralSurvivorPack;
        // A distinct suggestion style for the supported magic slice plus the
        // ordinary survivor foundation, not an authored complete adept role kit.
        if (archetypeId == CharacterCreationMagicResonanceKinds.MysticAdept)
            return GeneralSurvivorPack with { Id = "sr5.mystic-adept.foundation", ArchetypeId = archetypeId };
        var role = Roles.SingleOrDefault(row => row.Id == archetypeId);
        if (role is null) return null;
        var attributes = role.Attributes.Select((id, index) => new CompletionAttributeRecommendation(
            new(id, index == 0 ? 4 : 3), "completion.reason." + role.Id + ".foundation"));
        var skills = role.Skills.Select((id, index) => new CompletionSkillRecommendation(
            new(CharacterCreationSkillKinds.Active, id, index == 0 ? 3 : 2), "completion.reason." + role.Id + ".role-skill"));
        return new("sr5." + role.Id + ".foundation", "1", role.Id,
            attributes.Concat(Survivor.Attributes.Where(row => !role.Attributes.Contains(row.Target.AttributeId))).ToArray(),
            skills.Concat(Survivor.Skills.Where(row => !role.Skills.Contains(row.Target.SourceId))).ToArray());
    }

    /// <summary>Advisory prefill from the ORIGINAL source-admitted observation,
    /// never from this assistant's hypothetical output. Retained skill ratings
    /// may include grants; this does not call them paid or manually allocated.
    /// The caller must still acquire fresh Core authority before mutation.</summary>
    public static CharacterCreationFoundationResult<CompletionArchetypeSuggestion> SuggestArchetype(
        CharacterCreationCompletionReviewInputs inputs, string? selectedArchetypeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasInputShape(inputs) || !Text(selectedArchetypeId, 80)
            || inputs.Skills.Authority.Blockers is null || inputs.Magic.Authority?.Blockers is null
            || inputs.Skills.Allocations is null || inputs.Skills.GroupAllocations is null
            || inputs.Skills.Allocations.Any(row => row is null || row.Rating is < 0)
            || inputs.Skills.GroupAllocations.Any(row => row is null || row.Rating < 0)
            || inputs.Skills.Allocations.Select(row => (row.Kind, row.SourceSkillId)).Distinct().Count() != inputs.Skills.Allocations.Count
            || inputs.Skills.GroupAllocations.Select(row => row.GroupId).Distinct(StringComparer.Ordinal).Count() != inputs.Skills.GroupAllocations.Count
            || inputs.Skills.Authority.SkillGroups.Any(row => row.MemberSkillSourceIds is null))
            return new(CharacterCreationFoundationOutcomes.Invalid, null, [CompletionAssistantRules.IntentInvalid]);
        inputs = Detach(inputs);
        if (!CharacterCreationSkillsDigest.EqualsFixedTime(inputs.SnapshotDigest,
            CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs)))
            return new(CharacterCreationFoundationOutcomes.Conflict, null, [CompletionAssistantRules.SnapshotConflict]);
        if (!inputs.Skills.Authority.IsAuthoritative || inputs.Skills.Authority.Blockers.Count != 0
            || !inputs.Magic.Authority.IsAuthoritative || inputs.Magic.Authority.Blockers.Count != 0
            || !inputs.Magic.SelectedTalent.IsEnabled || inputs.Magic.SelectedTalent.Blockers.Count != 0)
            return new(CharacterCreationFoundationOutcomes.Blocked, null, [CompletionAssistantRules.ArchetypeBaselineUnavailable]);
        var ranked = new List<(string Id, int Score, List<CompletionArchetypeEvidence> Evidence)>();
        var talent = inputs.Magic.SelectedTalent;
        string? talentRole = talent.IsEnabled && talent.Blockers.Count == 0 ? talent.Kind switch
        {
            CharacterCreationMagicResonanceKinds.Magician or CharacterCreationMagicResonanceKinds.AspectedMagician => CompletionAssistantRules.Mage,
            CharacterCreationMagicResonanceKinds.Adept => CharacterCreationMagicResonanceKinds.Adept,
            CharacterCreationMagicResonanceKinds.MysticAdept => CharacterCreationMagicResonanceKinds.MysticAdept,
            CharacterCreationMagicResonanceKinds.Technomancer => CharacterCreationMagicResonanceKinds.Technomancer,
            CharacterCreationMagicResonanceKinds.ArtificialIntelligence => CharacterCreationMagicResonanceKinds.ArtificialIntelligence,
            _ => null
        } : null;
        foreach (var role in Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = new List<CompletionArchetypeEvidence>();
            int score = 0;
            foreach (string id in role.Skills)
            {
                if (inputs.Skills.Authority.ActiveSkills.Count(row => row.SourceSkillId == id) != 1) continue;
                var allocations = inputs.Skills.Allocations.Where(row => row.Kind == CharacterCreationSkillKinds.Active
                    && row.SourceSkillId == id && !row.IsNativeLanguage).ToArray();
                if (allocations.Length != 1 || allocations[0].Rating is not > 0) continue;
                int rating = allocations[0].Rating!.Value;
                evidence.Add(new("completion.archetype.retained-skill", CompletionArchetypeEvidenceKind.ActiveSkill, id, rating));
                score += Math.Min(rating, 4); // authored preference, never rules math
            }
            foreach (var allocation in inputs.Skills.GroupAllocations.Where(row => row.Rating > 0))
            {
                var groups = inputs.Skills.Authority.SkillGroups.Where(row => row.GroupId == allocation.GroupId).ToArray();
                if (groups.Length != 1 || !groups[0].MemberSkillSourceIds.Intersect(role.Skills, StringComparer.Ordinal).Any()) continue;
                evidence.Add(new("completion.archetype.retained-group", CompletionArchetypeEvidenceKind.SkillGroup,
                    allocation.GroupId, allocation.Rating));
                score += Math.Min(allocation.Rating, 4); // one group, not several independent choices
            }
            if (role.Id == talentRole)
            {
                evidence.Add(TalentEvidence());
                score += 6;
            }
            if (score == 0) continue; // racial minima or attributes alone do not invent a role
            foreach (var attribute in inputs.Skills.Attributes.Attributes.Where(row => row.IsEnabled
                && role.Attributes.Contains(row.AttributeId) && (row.PriorityPointsSpent > 0 || row.KarmaLevels > 0)))
            {
                evidence.Add(new("completion.archetype.invested-attribute", CompletionArchetypeEvidenceKind.Attribute,
                    attribute.AttributeId, attribute.UnaugmentedCurrent));
                score++;
            }
            ranked.Add((role.Id, score, evidence));
        }
        // These distinct talents have no authored role kit. A bounded fallback
        // foundation does not turn a Mystic Adept into a Mage or another role.
        if (talentRole is not null && !Roles.Any(row => row.Id == talentRole))
            ranked.Add((talentRole, 6, [TalentEvidence()]));
        var ordered = ranked.OrderByDescending(row => row.Score).ThenBy(row => row.Id, StringComparer.Ordinal).ToArray();
        bool prefilled = string.IsNullOrWhiteSpace(selectedArchetypeId);
        // Preserve a known distinct talent identity even when ordinary role
        // skills score higher. A technomancer's Hacking is not proof of a decker.
        string automatic = talentRole is not null && !Roles.Any(row => row.Id == talentRole) ? talentRole
            : ordered.FirstOrDefault().Id ?? CompletionAssistantRules.GeneralSurvivor;
        string selected = prefilled ? automatic : selectedArchetypeId!;
        var matching = ordered.Where(row => row.Id == selected).ToArray();
        var selectedEvidence = matching.Length == 1 ? matching[0].Evidence.ToArray() : [];
        CompletionArchetypeConfidence confidence = !prefilled ? CompletionArchetypeConfidence.PlayerSelected
            : ordered.Length == 0 ? CompletionArchetypeConfidence.NoEvidence
            : Confidence(Array.FindIndex(ordered, row => row.Id == selected));
        var alternatives = ordered.Select((row, index) => new CompletionArchetypeCandidate(row.Id, Confidence(index),
                row.Evidence.ToArray(), HasPack(row.Id))).Where(row => row.ArchetypeId != selected).ToArray();
        var limitations = new List<string> { "completion.archetype.advisory-not-capability", "completion.remaining.practical-loadout" };
        if (!HasPack(selected)) limitations.Add("completion.archetype.foundation-unavailable");
        if (selected == CompletionAssistantRules.StreetSamurai) limitations.Add("completion.archetype.combat-does-not-imply-implants");
        if (prefilled && confidence is CompletionArchetypeConfidence.NoEvidence or CompletionArchetypeConfidence.Tentative)
            limitations.Add("completion.archetype.confirm-role");
        if (talent.Kind != CharacterCreationMagicResonanceKinds.Mundane) limitations.Add("completion.archetype.awakened-choices-not-inferred");
        var suggestion = new CompletionArchetypeSuggestion(inputs.SnapshotDigest, selected, prefilled, confidence,
            selectedEvidence, alternatives, limitations.ToArray(), HasPack(selected), string.Empty);
        suggestion = suggestion with { SuggestionDigest = CompletionAssistantRules.ComputeArchetypeSuggestionDigest(suggestion) };
        cancellationToken.ThrowIfCancellationRequested();
        return new(CharacterCreationFoundationOutcomes.Success, suggestion, []);

        CompletionArchetypeEvidence TalentEvidence() => new("completion.archetype.selected-talent",
            CompletionArchetypeEvidenceKind.Talent, talent.Identity.TalentSelectionId, null);
        CompletionArchetypeConfidence Confidence(int index) => index == 0
            && ordered[index].Evidence.Count(row => row.Kind != CompletionArchetypeEvidenceKind.Attribute) >= 2
            && (ordered.Length == 1 || ordered[0].Score - ordered[1].Score >= 2)
                ? CompletionArchetypeConfidence.Supported : CompletionArchetypeConfidence.Tentative;
        static bool HasPack(string id) => id is CompletionAssistantRules.GeneralSurvivor
            or CharacterCreationMagicResonanceKinds.MysticAdept || Roles.Any(row => row.Id == id);
    }

    public static CharacterCreationFoundationResult<CompletionAssistantProposal> Build(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        CompletionArchetypeSuggestion? suggestion = null;
        if (string.IsNullOrWhiteSpace(intent.ArchetypeId))
        {
            var inferred = SuggestArchetype(inputs, cancellationToken: cancellationToken);
            if (inferred.Value is null) return new(inferred.Outcome, null, inferred.Blockers);
            suggestion = inferred.Value;
            intent = intent with { ArchetypeId = suggestion.ArchetypeId };
        }
        var pack = GetRecommendationPack(intent.ArchetypeId);
        if (pack is null) return Reject(CharacterCreationFoundationOutcomes.Blocked, CompletionAssistantRules.ArchetypeUnsupported);
        var result = Build(inputs, intent, pack, cancellationToken);
        if (result.Value is null || suggestion is null) return result;
        var proposal = result.Value with { ArchetypeSuggestion = suggestion };
        return result with { Value = proposal with { ProposalDigest = CompletionAssistantRules.ComputeProposalDigest(proposal) } };
    }

    /// <summary>Explicit authorization to recommend only unselected choices in
    /// the original Core observation. Additive permissions never unlock retained
    /// choices here. Core-recorded attribute choices stay locked, including zero;
    /// legacy drafts without edit intent stay fully locked. This is not full character completion.</summary>
    public static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildMissingOnly(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CancellationToken cancellationToken = default)
        => BuildMissingOnly(inputs, intent, cancellationToken, progress: null);

    public static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildMissingOnly(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CancellationToken cancellationToken, Action<CompletionAnalysisProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        var analysis = new CompletionAnalysisReporter(progress, CompletionAnalysisProgress.MissingOnlyStageCount, cancellationToken);
        analysis.Start(CompletionAnalysisStage.PreparingChoices);
        CompletionArchetypeSuggestion? suggestion = null;
        if (string.IsNullOrWhiteSpace(intent.ArchetypeId))
        {
            var inferred = SuggestArchetype(inputs, cancellationToken: cancellationToken);
            if (inferred.Value is null) return new(inferred.Outcome, null, inferred.Blockers);
            suggestion = inferred.Value;
            intent = intent with { ArchetypeId = suggestion.ArchetypeId };
        }
        var pack = GetRecommendationPack(intent.ArchetypeId);
        if (pack is null) return Reject(CharacterCreationFoundationOutcomes.Blocked, CompletionAssistantRules.ArchetypeUnsupported);
        var result = BuildMissingOnlyCore(inputs, intent, pack, fillRemainingResources: true, cancellationToken, analysis);
        if (result.Value is null) return result;
        if (suggestion is not null)
        {
            var proposal = result.Value with { ArchetypeSuggestion = suggestion };
            result = result with { Value = proposal with { ProposalDigest = CompletionAssistantRules.ComputeProposalDigest(proposal) } };
        }
        analysis.Complete(); // includes final coverage, inferred identity and proposal digest
        return result;
    }

    public static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildMissingOnly(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, CancellationToken cancellationToken = default)
        => BuildMissingOnly(inputs, intent, pack, cancellationToken, progress: null);

    public static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildMissingOnly(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, CancellationToken cancellationToken,
        Action<CompletionAnalysisProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(pack);
        var analysis = new CompletionAnalysisReporter(progress, 4, cancellationToken);
        analysis.Start(CompletionAnalysisStage.PreparingChoices);
        var result = BuildMissingOnlyCore(inputs, intent, pack, fillRemainingResources: false, cancellationToken, analysis);
        if (result.Value is not null) analysis.Complete();
        return result;
    }

    private static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildMissingOnlyCore(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, bool fillRemainingResources, CancellationToken cancellationToken,
        CompletionAnalysisReporter analysis)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(pack);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidIntent(intent) || !HasMissingOnlyInputShape(inputs)
            || fillRemainingResources && !HasResourceInputShape(inputs))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.IntentInvalid);
        if (!ValidPack(pack))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.PackInvalid);
        inputs = Detach(inputs);
        intent = DetachIntent(intent);
        pack = Detach(pack);
        if (!CharacterCreationSkillsDigest.EqualsFixedTime(intent.InputsSnapshotDigest, inputs.SnapshotDigest)
            || !CharacterCreationSkillsDigest.EqualsFixedTime(inputs.SnapshotDigest,
                CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs)))
            return Reject(CharacterCreationFoundationOutcomes.Conflict, CompletionAssistantRules.SnapshotConflict);
        if (!inputs.Skills.Attributes.CanEdit || !inputs.Skills.Authority.IsAuthoritative
            || inputs.Skills.Authority.Blockers.Count != 0)
            return Reject(CharacterCreationFoundationOutcomes.Blocked, CompletionAssistantRules.ArchetypeBaselineUnavailable);

        var locked = intent.ExcludedChoices.ToHashSet();
        // An absent provenance entry is not an explicit Unknown observation.
        // Even UntouchedDefault cannot override a retained or granted identity.
        locked.UnionWith(intent.Provenance.Where(row => row.Origin != CompletionChoiceOrigin.UntouchedDefault)
            .Select(row => row.Choice));
        locked.UnionWith(inputs.Skills.Allocations.Select(row => SkillIdentity(row.Kind, row.SourceSkillId)));
        locked.UnionWith(inputs.Skills.GroupAllocations.Select(row =>
            new CompletionChoiceIdentity(CompletionChoiceDomain.SkillGroup, row.GroupId)));
        if (inputs.Skills.PrerequisiteDraft.TalentSelection?.GrantPlan is { } grants)
        {
            locked.UnionWith(grants.ActiveSkills.Select(row =>
                new CompletionChoiceIdentity(CompletionChoiceDomain.ActiveSkill, row.SourceId)));
            // A selected zero-rated grant group has no GroupAllocations row.
            // Its exact source members still identify choices that must be kept.
            locked.UnionWith(grants.SkillGroups.SelectMany(row => row.MemberSkillSourceIds)
                .Select(id => new CompletionChoiceIdentity(CompletionChoiceDomain.ActiveSkill, id)));
        }
        foreach (var group in inputs.Skills.Authority.SkillGroups.Where(row =>
            locked.Contains(new(CompletionChoiceDomain.SkillGroup, row.GroupId))))
            locked.UnionWith(group.MemberSkillSourceIds.Select(id =>
                new CompletionChoiceIdentity(CompletionChoiceDomain.ActiveSkill, id)));

        // Core binds edit intent to the admitted draft. A known empty list means
        // untouched defaults; a chosen zero still locks its identity. Legacy null
        // cannot be inferred from dense allocation rows or their current values.
        var attributeDraft = inputs.Skills.Attributes.PendingDraft;
        bool knownAttributeIntent = attributeDraft is null || attributeDraft.PlayerChosenAttributeIds is not null;
        if (attributeDraft?.PlayerChosenAttributeIds is { } chosenAttributes)
            locked.UnionWith(chosenAttributes.Select(AttributeIdentity));
        if (fillRemainingResources)
        {
            analysis.Complete();
            analysis.Start(CompletionAnalysisStage.Skills);
            pack = ExpandResourceAdvice(inputs, intent, pack, locked, cancellationToken);
        }
        var attributes = knownAttributeIntent ? pack.Attributes.Where(row =>
                !locked.Contains(AttributeIdentity(row.Target.AttributeId))
                && inputs.Skills.Attributes.Attributes.Any(value =>
                    value.AttributeId == row.Target.AttributeId && value.IsEnabled))
            .Select(row => row.Target.AttributeId).ToArray() : [];
        var skills = new List<CharacterCreationSkillCompletionTarget>();
        foreach (var row in pack.Skills)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (locked.Contains(SkillIdentity(row.Target)) || !KnownSkill(inputs.Skills.Authority, row.Target)) continue;
            if (row.Target.Kind == CharacterCreationSkillCompletionKinds.Group)
            {
                var group = inputs.Skills.Authority.SkillGroups.Single(value => value.GroupId == row.Target.SourceId);
                if (group.MemberSkillSourceIds.Any(id => locked.Contains(new(CompletionChoiceDomain.ActiveSkill, id)))) continue;
                foreach (string id in group.MemberSkillSourceIds)
                    AddPermission(new(CharacterCreationSkillKinds.Active, id));
            }
            AddPermission(new(row.Target.Kind, row.Target.SourceId));
        }
        string? nativeLanguage = intent.NativeLanguageSourceId;
        if (fillRemainingResources && nativeLanguage is null
            && !inputs.Skills.Allocations.Any(row => row.IsNativeLanguage)
            && !locked.Contains(new(CompletionChoiceDomain.KnowledgeSkill, DefaultNativeLanguage))
            && inputs.Skills.Authority.KnowledgeSkills.Any(row => row.SourceSkillId == DefaultNativeLanguage
                && row.CanBeNativeLanguage))
            nativeLanguage = DefaultNativeLanguage;
        var restricted = intent with
        {
            Permissions = new(attributes, fillRemainingResources ? skills.ToArray()
                : skills.OrderBy(row => row.Kind, StringComparer.Ordinal)
                    .ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray()),
            NativeLanguageSourceId = nativeLanguage is { } language
                && locked.Contains(new(CompletionChoiceDomain.KnowledgeSkill, language)) ? null : nativeLanguage
        };
        // These are already private, digest-admitted snapshots. The restricted
        // request is authored above; do not serialize the complete catalogs a
        // second time on this private continuation path.
        analysis.Complete();
        var result = BuildFromSnapshot(inputs, restricted, pack, fillRemainingResources, cancellationToken, analysis);
        if (result.Value is not { } proposal) return result;
        bool needsAttributeIntent = !knownAttributeIntent && (pack.Attributes.Any(row =>
                !locked.Contains(AttributeIdentity(row.Target.AttributeId))
                && inputs.Skills.Attributes.Attributes.Any(value => value.AttributeId == row.Target.AttributeId
                    && value.UnaugmentedCurrent < row.Target.TargetUnaugmentedRating))
            || fillRemainingResources && proposal.Evaluation.Value?.Attributes?.Preview is { } attributePreview
                && (attributePreview.NormalPointBudget.Remaining > intent.Reserves.NormalAttributePoints
                    || attributePreview.SpecialPointBudget.Remaining > intent.Reserves.SpecialAttributePoints));
        proposal = proposal with
        {
            Intent = intent,
            AllocationPolicy = CompletionAllocationPolicy.MissingOnly,
            Coverage = needsAttributeIntent ? [.. proposal.Coverage,
                new("attribute-choice-intent", CompletionCoverageState.Unsupported,
                    CompletionAssistantRules.AttributeChoiceIntentRequired, [])] : proposal.Coverage,
            RemainingTasks = needsAttributeIntent ? proposal.RemainingTasks
                .Append(CompletionAssistantRules.AttributeChoiceIntentRequired)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : proposal.RemainingTasks
        };
        return result with { Value = proposal with { ProposalDigest = CompletionAssistantRules.ComputeProposalDigest(proposal) } };

        void AddPermission(CharacterCreationSkillCompletionTarget target)
        {
            if (!skills.Contains(target)) skills.Add(target);
        }
    }

    public static CharacterCreationFoundationResult<CompletionAssistantProposal> Build(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, CancellationToken cancellationToken = default)
        => BuildCore(inputs, intent, pack, fillRemainingResources: false, cancellationToken);

    private static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildCore(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, bool fillRemainingResources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(pack);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidIntent(intent) || !HasInputShape(inputs))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.IntentInvalid);
        if (!ValidPack(pack))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.PackInvalid);
        if (!string.Equals(pack.ArchetypeId, intent.ArchetypeId, StringComparison.Ordinal))
            return Reject(CharacterCreationFoundationOutcomes.Blocked, CompletionAssistantRules.ArchetypeUnsupported);
        inputs = Detach(inputs);
        intent = DetachIntent(intent);
        pack = Detach(pack);
        if (!CharacterCreationSkillsDigest.EqualsFixedTime(intent.InputsSnapshotDigest, inputs.SnapshotDigest)
            || !CharacterCreationSkillsDigest.EqualsFixedTime(inputs.SnapshotDigest,
                CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs)))
            return Reject(CharacterCreationFoundationOutcomes.Conflict, CompletionAssistantRules.SnapshotConflict);

        return BuildFromSnapshot(inputs, intent, pack, fillRemainingResources, cancellationToken);
    }

    // Only the two public-boundary paths above can supply these detached,
    // digest-admitted inputs. No cached state or client-provided evaluation is
    // accepted, and derived permissions still pass the ordinary shape checks.
    private static CharacterCreationFoundationResult<CompletionAssistantProposal> BuildFromSnapshot(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, bool fillRemainingResources, CancellationToken cancellationToken,
        CompletionAnalysisReporter? analysis = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidIntent(intent) || !HasInputShape(inputs))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.IntentInvalid);
        if (!ValidPack(pack))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.PackInvalid);
        if (!string.Equals(pack.ArchetypeId, intent.ArchetypeId, StringComparison.Ordinal))
            return Reject(CharacterCreationFoundationOutcomes.Blocked, CompletionAssistantRules.ArchetypeUnsupported);
        if (!ValidMagicPreferences(inputs, intent, fillRemainingResources))
            return Reject(CharacterCreationFoundationOutcomes.Invalid, CompletionAssistantRules.IntentInvalid);
        var excluded = intent.ExcludedChoices.ToHashSet();
        var attributePermissions = intent.Permissions.AttributeIds
            .Where(id => !excluded.Contains(AttributeIdentity(id))
                && inputs.Skills.Attributes.Attributes.Any(candidate => candidate.AttributeId == id)).ToArray();
        var skillPermissions = intent.Permissions.SkillTargets
            .Where(target => !excluded.Contains(SkillIdentity(target))
                && KnownSkill(inputs.Skills.Authority, target.Kind, target.SourceId)).ToArray();
        var permittedAttributes = attributePermissions.ToHashSet(StringComparer.Ordinal);
        var permittedSkills = skillPermissions.Select(SkillIdentity).ToHashSet();
        var attributeTargets = pack.Attributes.Where(row => permittedAttributes.Contains(row.Target.AttributeId)
                && !excluded.Contains(AttributeIdentity(row.Target.AttributeId))
                && inputs.Skills.Attributes.Attributes.Any(candidate => candidate.AttributeId == row.Target.AttributeId))
            .Select(row => row.Target).ToArray();
        var skillTargets = pack.Skills.Where(row => permittedSkills.Contains(SkillIdentity(row.Target))
                && !excluded.Contains(SkillIdentity(row.Target)) && KnownSkill(inputs.Skills.Authority, row.Target))
            .Select(row => row.Target).ToArray();
        var reserves = intent.Reserves;
        var attributes = new CharacterCreationAttributeCompletionRequest(inputs.Skills.Attributes.Binding,
            inputs.Skills.Attributes.SnapshotDigest, attributePermissions,
            reserves.NormalAttributePoints, reserves.SpecialAttributePoints)
        {
            // Built-in resource advice is a preference order, not a mandatory
            // starter floor that could block a nearly-finished manual character.
            Targets = fillRemainingResources ? [] : attributeTargets, FillRemainingPoints = fillRemainingResources
        };
        var skills = new CharacterCreationSkillCompletionRequest(inputs.Skills.Binding,
            inputs.Skills.Attributes.SnapshotDigest, inputs.Skills.SnapshotDigest, skillPermissions,
            reserves.ActiveSkillPoints, reserves.SkillGroupPoints, reserves.KnowledgeSkillPoints)
        {
            Targets = fillRemainingResources ? [] : skillTargets,
            FillRemainingPoints = fillRemainingResources,
            NativeLanguageSourceId = intent.NativeLanguageSourceId is { } language
                && excluded.Contains(new(CompletionChoiceDomain.KnowledgeSkill, language)) ? null : intent.NativeLanguageSourceId
        };
        // No negative quality, purchased power point, implant or starting-cash
        // roll is inferred from the remaining funds.
        analysis?.Start(CompletionAnalysisStage.Magic);
        var magic = new CharacterCreationMagicCompletionRequest(inputs.Magic.SnapshotDigest,
            inputs.Lifestyles.Qualities.SnapshotDigest, inputs.Magic.Selections, [], [], [], [], [],
            KarmaToReserve: reserves.Karma);
        if (fillRemainingResources)
            magic = AddMissingMagicAdvice(inputs, intent, magic);
        var request = new CharacterCreationCompletionPlanRequest(inputs.SnapshotDigest, attributes, skills, magic,
            inputs.Lifestyles.Qualities.SelectedOptionIds, inputs.Contacts.Contacts,
            inputs.Lifestyles.Gear.Resources.PendingDraft?.KarmaInvestment ?? 0, [],
            intent.StartingLifestyleId, reserves.Karma, reserves.Nuyen,
            MaximumAdditionalPurchaseSpend: fillRemainingResources ? intent.MaximumAdditionalPurchaseSpend ?? 0m : 0m,
            PermitNegativeQualityAdditions: intent.PermitNegativeQualities);
        analysis?.Complete();
        var completed = fillRemainingResources
            ? CompleteResources(inputs, request, intent.ArchetypeId, cancellationToken, analysis) : new ResourceCompletion(request);
        if (fillRemainingResources) analysis?.Complete();
        request = completed.Request;
        KarmaCompletion karma = new(completed, []);
        if (fillRemainingResources)
        {
            analysis?.Start(CompletionAnalysisStage.Karma);
            karma = CompleteKarmaAdvice(inputs, intent, completed, cancellationToken);
            completed = karma.Completed;
            request = completed.Request;
            analysis?.Complete();
        }
        // A genuine whole Core plan can be reused only when optional resource
        // advice left its complete request unchanged. Changed purchases still
        // require the ordinary whole-plan evaluation, never a patched result.
        analysis?.Start(CompletionAnalysisStage.FinalBudgets);
        var evaluated = (ReferenceEquals(request, completed.Request) ? completed.Evaluation : null)
            ?? CharacterCreationCompletionPlanner.Plan(inputs, request, cancellationToken);
        analysis?.Complete();
        analysis?.Start(CompletionAnalysisStage.PreparingReview);
        var loadout = BuildLoadoutSummary(inputs, evaluated.Value, cancellationToken);
        var suggestions = BuildSuggestions(inputs, intent, pack, evaluated.Value, fillRemainingResources).ToArray();
        var coverage = BuildCoverage(inputs, intent, evaluated.Value)
            .Concat(BuildLoadoutCoverage(inputs.Loadout, loadout)).Concat(karma.Coverage).ToArray();
        var remaining = coverage.Where(row => row.State is CompletionCoverageState.NeedsChoice
                or CompletionCoverageState.Unsupported or CompletionCoverageState.NotEvaluated)
            .Select(row => row.ExplanationKey)
            .Concat(suggestions.Where(row => row.State is CompletionSuggestionState.Locked
                    or CompletionSuggestionState.SourceUnavailable or CompletionSuggestionState.TargetUnmet
                    or CompletionSuggestionState.NotEvaluated)
                .Select(row => "completion.remaining.starter-targets"))
            .Concat(["completion.remaining.finalization-review"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var proposal = new CompletionAssistantProposal(CompletionAssistantRules.ProposalSchema, intent,
            pack.Id, pack.Version, CharacterCreationFinalizationDigest.Compute(pack), request, evaluated,
            suggestions, coverage, remaining, string.Empty)
        {
            FillsRemainingResources = fillRemainingResources,
            Loadout = loadout
        };
        proposal = proposal with { ProposalDigest = CompletionAssistantRules.ComputeProposalDigest(proposal) };
        cancellationToken.ThrowIfCancellationRequested();
        return new(evaluated.Outcome, DetachProposal(proposal), evaluated.Blockers.ToArray());
    }

    private static IEnumerable<CompletionSuggestion> BuildSuggestions(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack pack, CharacterCreationCompletionPlan? plan, bool actualRatings = false)
    {
        var provenance = intent.Provenance.ToDictionary(row => row.Choice, row => row.Origin);
        var excluded = intent.ExcludedChoices.ToHashSet();
        var attributePermissions = intent.Permissions.AttributeIds.ToHashSet(StringComparer.Ordinal);
        var skillPermissions = intent.Permissions.SkillTargets.Select(SkillIdentity).ToHashSet();
        foreach (var row in pack.Attributes)
        {
            var identity = AttributeIdentity(row.Target.AttributeId);
            int? actual = plan?.Attributes?.Preview.Attributes.SingleOrDefault(value => value.AttributeId == row.Target.AttributeId)?.UnaugmentedCurrent;
            yield return Suggest(identity, row.Target.TargetUnaugmentedRating, row.ReasonKey,
                inputs.Skills.Attributes.Attributes.Any(value => value.AttributeId == row.Target.AttributeId),
                attributePermissions.Contains(row.Target.AttributeId), actual);
        }
        foreach (var row in pack.Skills)
        {
            var identity = SkillIdentity(row.Target);
            // Sparse rows, group-backed ratings, source grants and native language
            // semantics belong to Core. Another target's blocker is not this goal's status.
            var evaluation = plan?.Skills is { } skills
                ? CharacterCreationSkillCompletionPlanner.EvaluateTarget(skills.Preview,
                    inputs.Skills.Authority, inputs.Skills.MovementCapability, row.Target) : null;
            yield return Suggest(identity, row.Target.TargetRating, row.ReasonKey,
                KnownSkill(inputs.Skills.Authority, row.Target), skillPermissions.Contains(identity),
                evaluation?.EffectiveRating, evaluation?.IsSatisfied);
        }
        CompletionSuggestion Suggest(CompletionChoiceIdentity choice, int desired, string reason,
            bool known, bool permitted, int? actual, bool? targetSatisfied = null)
        {
            // The balanced mode proposes the evaluated result, not the old
            // starter floor. Never display an allocation Core did not produce.
            // Zero can also mean an unavailable source-known skill. It is not
            // evidence that Core allocated anything or fulfilled a positive goal.
            if (actualRatings && actual is { } allocated && allocated > 0)
            {
                desired = allocated;
                targetSatisfied = true;
            }
            bool satisfied = targetSatisfied ?? actual >= desired;
            var state = excluded.Contains(choice) ? CompletionSuggestionState.Excluded
                : !known ? CompletionSuggestionState.SourceUnavailable
                : !permitted ? satisfied ? CompletionSuggestionState.TargetMet
                    : actual is null && targetSatisfied is null ? CompletionSuggestionState.NotEvaluated : CompletionSuggestionState.Locked
                : plan is null ? CompletionSuggestionState.NotEvaluated
                : satisfied ? CompletionSuggestionState.TargetMet
                : actual is null && targetSatisfied is null ? CompletionSuggestionState.NotEvaluated
                : CompletionSuggestionState.TargetUnmet;
            return new(choice, desired, reason, provenance.GetValueOrDefault(choice, CompletionChoiceOrigin.Unknown), state, actual);
        }
    }

    private static IEnumerable<CompletionCategoryCoverage> BuildCoverage(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent, CharacterCreationCompletionPlan? plan)
    {
        yield return Evaluated("attributes", plan?.Attributes?.Blockers);
        yield return Evaluated("skills", plan?.Skills?.Blockers);
        yield return plan?.Magic is { IsMagicComplete: true }
            ? new("magic", inputs.Magic.SelectedTalent.Kind == CharacterCreationMagicResonanceKinds.Mundane
                ? CompletionCoverageState.NotApplicable : CompletionCoverageState.CoreValidated,
                plan.Magic.HasChanges ? "completion.coverage.proposed-magic"
                    : "completion.coverage.retained-magic", [])
            : Evaluated("magic", plan?.Magic?.Blockers);
        if (inputs.Magic.SelectedTalent.Kind == CharacterCreationMagicResonanceKinds.MysticAdept)
            // Core's complete free magic budget is not an authored adept build.
            // Keep the unsupported power advice visible without inventing a blocker.
            yield return new("adept-powers", CompletionCoverageState.Unsupported, "completion.remaining.magic-choices", []);
        yield return Evaluated("qualities", plan?.Review.UpstreamBudget?.Qualities.Blockers);
        yield return Evaluated("resources", plan?.Review.Lifestyles?.Gear.Resources.Blockers);
        yield return Evaluated("gear", plan?.Review.Lifestyles?.Gear.Blockers);
        yield return new("loadout", CompletionCoverageState.Unsupported, "completion.remaining.practical-loadout", []);
        yield return Evaluated("contacts", plan?.Review.Contacts?.Blockers);
        yield return Evaluated("lifestyle", plan?.Review.Lifestyles?.Blockers);
        yield return new("identity", CompletionCoverageState.NotEvaluated, "completion.remaining.identity-review", []);
        yield return new("specializations", CompletionCoverageState.OptionalManual, "completion.coverage.specializations-retained", []);
        yield return new("implants", intent.PermitImplants ? CompletionCoverageState.Unsupported
            : CompletionCoverageState.OptionalManual, intent.PermitImplants
            ? "completion.remaining.implant-recommendations" : "completion.coverage.no-implant-additions", []);
        if (!string.IsNullOrWhiteSpace(intent.Concept) || !string.IsNullOrWhiteSpace(intent.SecondaryRole)
            || !string.IsNullOrWhiteSpace(intent.PlayStyle))
            yield return new("concept", CompletionCoverageState.Unsupported, "completion.remaining.free-text-not-interpreted", []);
        bool lifeModules = inputs.Skills.PrerequisiteDraft.BuildMethod == CharacterCreationBuildMethods.LifeModules;
        yield return new("life-modules", lifeModules ? CompletionCoverageState.Unsupported : CompletionCoverageState.NotApplicable,
            lifeModules ? "completion.remaining.life-module-story-gates" : "completion.coverage.not-life-modules", []);

        static CompletionCategoryCoverage Evaluated(string category, IReadOnlyList<string>? blockers)
            => new(category, blockers is null ? CompletionCoverageState.NotEvaluated
                    : blockers.Count == 0 ? CompletionCoverageState.CoreValidated : CompletionCoverageState.NeedsChoice,
                blockers is null ? "completion.remaining." + category + "-evaluation"
                    : blockers.Count == 0 ? "completion.coverage." + category + "-validated"
                    : "completion.remaining." + category + "-choices", blockers?.ToArray() ?? []);
    }

    private static bool HasInputShape(CharacterCreationCompletionReviewInputs inputs)
        => inputs is { Skills: { Attributes: { Attributes: not null }, Authority: { ActiveSkills: not null,
                KnowledgeSkills: not null, SkillGroups: not null }, PrerequisiteDraft: not null },
            Magic: { Selections: not null, SelectedTalent: { Identity: not null, Blockers: not null } },
            Lifestyles: { Qualities: { SelectedOptionIds: not null }, Gear: { Resources: not null } },
            Contacts: { Contacts: not null } }
            && inputs.Skills.Attributes.Attributes.All(row => row is not null)
            && inputs.Skills.Authority.ActiveSkills.All(row => row is not null)
            && inputs.Skills.Authority.KnowledgeSkills.All(row => row is not null)
            && inputs.Skills.Authority.SkillGroups.All(row => row is not null)
            && HasLoadoutInputShape(inputs.Loadout);

    private static bool HasMissingOnlyInputShape(CharacterCreationCompletionReviewInputs inputs)
        => HasInputShape(inputs) && inputs.Contacts.BootstrapBinding is not null
            && (inputs.Skills.Attributes.PendingDraft?.PlayerChosenAttributeIds is not { } chosenAttributes
                || chosenAttributes.Count <= 13
                && chosenAttributes.All(id => Text(id, 64, false)
                    && inputs.Skills.Attributes.Attributes.Any(row => row.AttributeId == id))
                && chosenAttributes.Distinct(StringComparer.Ordinal).Count() == chosenAttributes.Count)
            && inputs.Skills.Authority.Blockers is not null
            && inputs.Skills.Allocations is not null && inputs.Skills.Allocations.All(row => row is not null
                && ValidKind(row.Kind) && Text(row.SourceSkillId, 256, false))
            && inputs.Skills.GroupAllocations is not null && inputs.Skills.GroupAllocations.All(row => row is not null)
            && inputs.Skills.Authority.SkillGroups.All(row => row.MemberSkillSourceIds is { Count: > 0 })
            && inputs.Skills.Authority.SkillGroups.Select(row => row.GroupId).Distinct(StringComparer.Ordinal).Count()
                == inputs.Skills.Authority.SkillGroups.Count
            && (inputs.Skills.PrerequisiteDraft.TalentSelection?.GrantPlan is not { } grants
                || grants.ActiveSkills is not null && grants.SkillGroups is not null
                && grants.ActiveSkills.All(row => row is not null && Text(row.SourceId, 256, false))
                && grants.SkillGroups.All(row => row is not null && row.MemberSkillSourceIds is { Count: > 0 }));

    private static bool ValidIntent(CompletionAssistantIntent intent)
        => intent.OwnerContext.IsValid && !string.IsNullOrWhiteSpace(intent.InputsSnapshotDigest)
            && Text(intent.ArchetypeId, 80, false) && Text(intent.Concept, 2000) && Text(intent.SecondaryRole, 200)
            && Text(intent.PlayStyle, 200) && Text(intent.NativeLanguageSourceId, 256)
            && ValidMagicPreferenceShape(intent.PreferredTradition, CharacterCreationMagicResonanceKinds.Tradition)
            && ValidMagicPreferenceShape(intent.PreferredStream, CharacterCreationMagicResonanceKinds.Stream)
            && intent.StartingLifestyleId != Guid.Empty
            && (intent.MaximumAdditionalPurchaseSpend is null or >= 0m)
            && intent.Reserves is { NormalAttributePoints: >= 0, SpecialAttributePoints: >= 0,
                ActiveSkillPoints: >= 0, SkillGroupPoints: >= 0, KnowledgeSkillPoints: >= 0, Karma: >= 0, Nuyen: >= 0 }
            && intent.Permissions is { AttributeIds.Count: <= 13, SkillTargets.Count: <= 128 }
            && intent.Permissions.AttributeIds.All(id => Text(id, 64, false))
            && intent.Permissions.AttributeIds.Distinct(StringComparer.Ordinal).Count() == intent.Permissions.AttributeIds.Count
            && intent.Permissions.SkillTargets.All(target => target is not null && ValidKind(target.Kind) && Text(target.SourceId, 256, false))
            && intent.Permissions.SkillTargets.Distinct().Count() == intent.Permissions.SkillTargets.Count
            && intent.Provenance is { Count: <= 256 } && intent.Provenance.All(row => row is not null
                && ValidChoice(row.Choice) && Enum.IsDefined(row.Origin))
            && intent.Provenance.Select(row => row.Choice).Distinct().Count() == intent.Provenance.Count
            && intent.ExcludedChoices is { Count: <= 256 } && intent.ExcludedChoices.All(ValidChoice)
            && intent.ExcludedChoices.Distinct().Count() == intent.ExcludedChoices.Count;

    private static bool ValidPack(CompletionRecommendationPack pack)
        => Text(pack.Id, 128, false) && Text(pack.Version, 80, false) && Text(pack.ArchetypeId, 80, false)
            && pack.Attributes is { Count: <= 13 } && pack.Attributes.All(row => row is { Target: not null }
                && Text(row.Target.AttributeId, 64, false) && row.Target.TargetUnaugmentedRating >= 0 && Text(row.ReasonKey, 160, false))
            && pack.Attributes.Select(row => row.Target.AttributeId).Distinct(StringComparer.Ordinal).Count() == pack.Attributes.Count
            && pack.Skills is { Count: <= 128 } && pack.Skills.All(row => row is { Target: not null }
                && ValidKind(row.Target.Kind) && Text(row.Target.SourceId, 256, false) && row.Target.TargetRating >= 0
                && Text(row.ReasonKey, 160, false))
            && pack.Skills.Select(row => SkillIdentity(row.Target)).Distinct().Count() == pack.Skills.Count;

    private static bool KnownSkill(CharacterCreationSkillsAuthority authority, CharacterCreationSkillCompletionRatingTarget target)
        => KnownSkill(authority, target.Kind, target.SourceId);
    private static bool KnownSkill(CharacterCreationSkillsAuthority authority, string kind, string sourceId)
        => kind switch
        {
            CharacterCreationSkillCompletionKinds.Group => authority.SkillGroups.Any(row => row.GroupId == sourceId),
            CharacterCreationSkillKinds.Active => authority.ActiveSkills.Any(row => row.SourceSkillId == sourceId),
            CharacterCreationSkillKinds.Knowledge => authority.KnowledgeSkills.Any(row => row.SourceSkillId == sourceId),
            _ => false
        };
    private static bool ValidKind(string kind) => kind is CharacterCreationSkillKinds.Active
        or CharacterCreationSkillKinds.Knowledge or CharacterCreationSkillCompletionKinds.Group;
    private static bool ValidChoice(CompletionChoiceIdentity? choice)
        => choice is not null && Enum.IsDefined(choice.Domain) && Text(choice.SourceId, 256, false);
    private static bool Text(string? value, int maximum, bool optional = true)
        => value is null ? optional : value.Length <= maximum && (optional || !string.IsNullOrWhiteSpace(value));
    private static CompletionChoiceIdentity AttributeIdentity(string id) => new(CompletionChoiceDomain.Attribute, id);
    private static CompletionChoiceIdentity SkillIdentity(CharacterCreationSkillCompletionTarget target)
        => SkillIdentity(target.Kind, target.SourceId);
    private static CompletionChoiceIdentity SkillIdentity(CharacterCreationSkillCompletionRatingTarget target)
        => SkillIdentity(target.Kind, target.SourceId);
    private static CompletionChoiceIdentity SkillIdentity(string kind, string id) => new(kind switch
    {
        CharacterCreationSkillKinds.Active => CompletionChoiceDomain.ActiveSkill,
        CharacterCreationSkillKinds.Knowledge => CompletionChoiceDomain.KnowledgeSkill,
        _ => CompletionChoiceDomain.SkillGroup
    }, id);
    // OwnerScope deliberately cannot recreate trusted-local authority from JSON.
    // Detach mutable advice while preserving only the caller's exact in-process
    // stamp; a value-only local name must never be promoted by this copy.
    private static CompletionAssistantIntent DetachIntent(CompletionAssistantIntent intent)
        => Detach(intent) with { OwnerContext = intent.OwnerContext };
    private static CompletionAssistantProposal DetachProposal(CompletionAssistantProposal proposal)
    {
        var detached = Detach(proposal);
        return detached with { Intent = detached.Intent with { OwnerContext = proposal.Intent.OwnerContext } };
    }
    private static T Detach<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
    private static CharacterCreationFoundationResult<CompletionAssistantProposal> Reject(string outcome, string blocker)
        => new(outcome, null, [blocker]);
}
