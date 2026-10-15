using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public enum CompletionChoiceDomain
{
    Attribute, ActiveSkill, KnowledgeSkill, SkillGroup
}

public enum CompletionChoiceOrigin
{
    Unknown, PlayerSet, UntouchedDefault, Imported, AcceptedAssistant
}

public sealed record CompletionChoiceIdentity(CompletionChoiceDomain Domain, string SourceId);
public sealed record CompletionChoiceProvenance(CompletionChoiceIdentity Choice, CompletionChoiceOrigin Origin);

/// <summary>Explicit permission to ADD to these choices, not permission to replace
/// them. Missing entries remain locked regardless of their value or provenance.
/// Group permissions do not silently unlock canonical member skills.</summary>
public sealed record CompletionAdditivePermissions(
    IReadOnlyList<string> AttributeIds,
    IReadOnlyList<CharacterCreationSkillCompletionTarget> SkillTargets);

public sealed record CompletionAssistantReserves(
    int NormalAttributePoints = 0,
    int SpecialAttributePoints = 0,
    int ActiveSkillPoints = 0,
    int SkillGroupPoints = 0,
    int KnowledgeSkillPoints = 0,
    int Karma = 0,
    decimal Nuyen = 0m);

/// <summary>The caller captures OwnerContext with the actual original input read.
/// The stamp binds this proposal; it is not a live lease. Free text is retained
/// as user intent, not interpreted by the deterministic starter implementation.
/// In additive Build, provenance is descriptive: even an untouched default needs
/// permission. BuildMissingOnly instead authorizes provably unselected targets;
/// supplied non-default provenance can lock choices, never unlock retained ones.</summary>
public sealed record CompletionAssistantIntent(
    OwnerContextStamp OwnerContext,
    string InputsSnapshotDigest,
    string ArchetypeId,
    CompletionAdditivePermissions Permissions,
    CompletionAssistantReserves Reserves)
{
    public string? Concept { get; init; }
    public string? SecondaryRole { get; init; }
    public string? PlayStyle { get; init; }
    public IReadOnlyList<CompletionChoiceProvenance> Provenance { get; init; } = [];
    public IReadOnlyList<CompletionChoiceIdentity> ExcludedChoices { get; init; } = [];
    public string? NativeLanguageSourceId { get; init; }
    /// <summary>Exact editable proposal for a missing identity choice, never an
    /// override of a saved tradition/stream. Null requests ranked source-bound
    /// advice in the default missing-only path. No purchased power points implied.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CharacterCreationMagicResonanceOptionIdentity? PreferredTradition { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CharacterCreationMagicResonanceOptionIdentity? PreferredStream { get; init; }
    public Guid? StartingLifestyleId { get; init; }
    /// <summary>Explicit additional-purchase ceiling for the default missing-only
    /// resource proposal. Null/zero authorizes no new purchases. Core independently
    /// enforces the actual funds and Nuyen reserve; this is not a price estimate.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MaximumAdditionalPurchaseSpend { get; init; }
    // Opt-ins are bound for future candidate packs. This starter adds neither.
    public bool PermitNegativeQualities { get; init; }
    public bool PermitImplants { get; init; }
}

public sealed record CompletionAttributeRecommendation(
    CharacterCreationAttributeCompletionTarget Target, string ReasonKey);
public sealed record CompletionSkillRecommendation(
    CharacterCreationSkillCompletionRatingTarget Target, string ReasonKey);

/// <summary>Authored advice, never rules/pricing authority. Exact source IDs must
/// resolve in the current Core inputs; there is no localized-name matching.</summary>
public sealed record CompletionRecommendationPack(
    string Id, string Version, string ArchetypeId,
    IReadOnlyList<CompletionAttributeRecommendation> Attributes,
    IReadOnlyList<CompletionSkillRecommendation> Skills);

/// <summary>Qualitative strength of authored baseline evidence, not an AI score,
/// probability, rule entitlement or proof that a role's loadout is complete.</summary>
public enum CompletionArchetypeConfidence { PlayerSelected, NoEvidence, Tentative, Supported }
public enum CompletionArchetypeEvidenceKind { Attribute, ActiveSkill, SkillGroup, Talent }
public sealed record CompletionArchetypeEvidence(
    string ReasonKey, CompletionArchetypeEvidenceKind Kind, string SourceId, int? ObservedValue);
public sealed record CompletionArchetypeCandidate(
    string ArchetypeId, CompletionArchetypeConfidence Confidence,
    IReadOnlyList<CompletionArchetypeEvidence> Evidence, bool HasRecommendationPack);
public sealed record CompletionArchetypeSuggestion(
    string InputsSnapshotDigest, string ArchetypeId, bool WasPrefilled,
    CompletionArchetypeConfidence Confidence, IReadOnlyList<CompletionArchetypeEvidence> Evidence,
    IReadOnlyList<CompletionArchetypeCandidate> Alternatives, IReadOnlyList<string> Limitations,
    bool HasRecommendationPack, string SuggestionDigest)
{
    public string RecommendationMode => "deterministic-baseline";
    public bool IsEditable => true;
}

public enum CompletionSuggestionState
{
    Locked, Excluded, SourceUnavailable, TargetMet, TargetUnmet, NotEvaluated
}

public sealed record CompletionSuggestion(
    CompletionChoiceIdentity Choice,
    int TargetRating,
    string ReasonKey,
    CompletionChoiceOrigin Origin,
    CompletionSuggestionState State,
    int? EvaluatedRating);

public enum CompletionCoverageState
{
    CoreValidated, NotApplicable, OptionalManual, NeedsChoice, Unsupported, NotEvaluated
}

public enum CompletionAllocationPolicy { ExplicitAdditive, MissingOnly }

/// <summary>CoreValidated means only the named evaluated category, not a complete
/// practical loadout or character. OptionalManual is not a completion blocker.</summary>
public sealed record CompletionCategoryCoverage(
    string Category, CompletionCoverageState State, string ExplanationKey,
    IReadOnlyList<string> CoreBlockers);

/// <summary>Pure inspectable recommendation. No save/finalize capability, provider
/// response, ownership admission or proof of a complete character is returned.
/// Career requires a separate current Core finalization review and explicit apply.</summary>
public sealed record CompletionAssistantProposal(
    string Schema,
    CompletionAssistantIntent Intent,
    string PackId,
    string PackVersion,
    string PackDigest,
    CharacterCreationCompletionPlanRequest Request,
    CharacterCreationFoundationResult<CharacterCreationCompletionPlan> Evaluation,
    IReadOnlyList<CompletionSuggestion> Suggestions,
    IReadOnlyList<CompletionCategoryCoverage> Coverage,
    IReadOnlyList<string> RemainingTasks,
    string ProposalDigest)
{
    public string RecommendationMode => FillsRemainingResources ? "deterministic-resource-plan" : "deterministic-starter";
    public bool CanReview => Evaluation.Value is not null;
    public bool RequiresFinalizationReview => true;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public CompletionAllocationPolicy AllocationPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CompletionArchetypeSuggestion? ArchetypeSuggestion { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool FillsRemainingResources { get; init; }
    /// <summary>Core-priced weapon/armor summary of the final evaluated basket,
    /// including retained choices. Names, quantities and costs come from Core.
    /// This does not attest ammunition, modifications or rated gear.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CharacterCreationLoadoutCompletionProjection? Loadout { get; init; }
}

public static class CompletionAssistantRules
{
    public const string ProposalSchema = "chummer.presentation.completion-assistant.v1";
    public const string GeneralSurvivor = "general-survivor";
    public const string StreetSamurai = "street-samurai";
    public const string Face = "face";
    public const string Mage = "mage";
    public const string Decker = "decker";
    public const string Rigger = "rigger";
    public const string IntentInvalid = "completion-assistant-intent-invalid";
    public const string SnapshotConflict = "completion-assistant-snapshot-conflict";
    public const string ArchetypeUnsupported = "completion-assistant-archetype-unsupported";
    public const string ArchetypeBaselineUnavailable = "completion-assistant-archetype-baseline-unavailable";
    public const string PackInvalid = "completion-assistant-pack-invalid";
    public const string AttributeChoiceIntentRequired = "completion.remaining.attribute-choice-intent";

    public static string ComputeProposalDigest(CompletionAssistantProposal proposal)
        => CharacterCreationFinalizationDigest.Compute(proposal with { ProposalDigest = string.Empty });

    public static string ComputeArchetypeSuggestionDigest(CompletionArchetypeSuggestion suggestion)
        => CharacterCreationFinalizationDigest.Compute(new
        {
            Schema = "chummer.presentation.completion-archetype.v1",
            Suggestion = suggestion with { SuggestionDigest = string.Empty }
        });
}
