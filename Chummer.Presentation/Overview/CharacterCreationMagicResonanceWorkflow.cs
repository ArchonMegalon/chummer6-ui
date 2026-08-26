using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Renderer-neutral statement of the Presentation/Core boundary for the SR5 Standard Priority
/// Magic/Resonance step. Presentation may collect only typed identities and levels. Core remains
/// the sole owner of source interpretation, legality, budgets, preview, atomic auxiliary-state
/// persistence, idempotent replay, and the eventual character-effect composition.
/// </summary>
public static class CharacterCreationMagicResonancePresentationContract
{
    public const string Schema = "chummer.presentation.sr5_priority_magic_resonance.v1";
    public const string RulesetId = RulesetDefaults.Sr5;
    public const string BuildMethod = CharacterCreationBuildMethods.Priority;
    public const string AuthorityOwner = "chummer-core";
    public const string PersistenceMode = "core-atomic-auxiliary-state-only";
    public const bool AllowsCharacterDocumentMutation = false;
    public const bool AllowsPresentationPersistence = false;
    public const bool RequiresCorePreview = true;
    public const bool RequiresExplicitConfirmation = true;
    public const string ExactTalentOwnedByPrerequisite =
        "creation-magic-resonance-talent-owned-by-prerequisite";
    public const string PresentationProjectionInvalid =
        "creation-magic-resonance-presentation-projection-invalid";

    public static bool IsSupportedTalentKind(string? kind) => kind is
        CharacterCreationMagicResonanceKinds.Mundane
        or CharacterCreationMagicResonanceKinds.Adept
        or CharacterCreationMagicResonanceKinds.Magician
        or CharacterCreationMagicResonanceKinds.MysticAdept
        or CharacterCreationMagicResonanceKinds.AspectedMagician
        or CharacterCreationMagicResonanceKinds.Technomancer;
}

public static class CharacterCreationMagicResonancePresentationBudgetIds
{
    public const string Tradition = "magic-tradition";
    public const string Stream = "resonance-stream";
    public const string AdeptPowerPoints = "adept-power-points";
    public const string Spells = "spells";
    public const string ComplexForms = "complex-forms";

    public static string ForKind(string kind) => kind switch
    {
        CharacterCreationMagicResonanceKinds.Tradition => Tradition,
        CharacterCreationMagicResonanceKinds.Stream => Stream,
        CharacterCreationMagicResonanceKinds.AdeptPower => AdeptPowerPoints,
        CharacterCreationMagicResonanceKinds.Spell => Spells,
        CharacterCreationMagicResonanceKinds.ComplexForm => ComplexForms,
        _ => throw new InvalidOperationException("Core returned an unsupported Magic/Resonance budget kind.")
    };
}

public sealed record CharacterCreationMagicResonanceTalentProjection(
    CharacterCreationMagicResonanceTalentIdentity Identity,
    string Rank,
    string Name,
    string Kind,
    int Magic,
    int Resonance,
    int Depth,
    int SpellBudget,
    int ComplexFormBudget,
    decimal AdeptPowerPointBudget,
    bool RequiresTradition,
    bool RequiresStream,
    bool AllowsAdeptPowers,
    bool AllowsSpells,
    bool AllowsComplexForms,
    IReadOnlyList<string> RequiredMetatypeNames,
    IReadOnlyList<string> RequiredMetatypeCategories,
    IReadOnlyList<string> ForbiddenMetatypeNames,
    bool IsEnabled,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds,
    string SourceNodeDigest);

public sealed record CharacterCreationMagicResonanceOptionProjection(
    CharacterCreationMagicResonanceOptionIdentity Identity,
    string Name,
    string Category,
    decimal PointCost,
    int MaximumLevels,
    string SourceBook,
    string Page,
    string DrainExpression,
    bool IsEnabled,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds,
    string SourceNodeDigest);

public sealed record CharacterCreationMagicResonanceEditorState(
    string Schema,
    CharacterCreationMagicResonanceBinding Binding,
    CharacterCreationMagicResonanceTalentProjection Talent,
    IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> Traditions,
    IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> Streams,
    IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> AdeptPowers,
    IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> Spells,
    IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> ComplexForms,
    CharacterCreationMagicResonanceSelections Selections,
    IReadOnlyList<CharacterCreationMagicResonanceBudgetState> Budgets,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds,
    bool HasPendingDraft,
    bool CanEdit,
    string CoreSnapshotDigest);

/// <summary>
/// Presentation draft. It intentionally contains no cost, budget, source, legality, grant,
/// attribute, or character-effect claim; those values can only be returned by Core Preview.
/// </summary>
public sealed record CharacterCreationMagicResonanceDesktopDraft(
    CharacterCreationMagicResonanceBinding ExpectedBinding,
    CharacterCreationMagicResonanceSelections Selections,
    string ExpectedCoreSnapshotDigest);

public sealed record CharacterCreationMagicResonanceReview(
    CharacterCreationMagicResonanceDesktopDraft Draft,
    CharacterCreationMagicResonancePreview Preview);

public sealed record CharacterCreationMagicResonanceConfirmation(
    CharacterCreationMagicResonanceReceipt Receipt,
    CharacterCreationMagicResonanceEditorState PersistedState,
    bool IsIdempotentReplay,
    bool IsCurrentDraft);

/// <summary>
/// Typed SR5 Standard Priority Magic/Resonance interaction boundary. This class never accepts a
/// write-capable workspace and never mutates character XML. Preview and confirmation are always
/// delegated to <see cref="ICharacterCreationMagicResonanceService"/>.
/// </summary>
public static class CharacterCreationMagicResonanceWorkflow
{
    private const int MaximumCatalogOptionsPerKind = 65_536;

    public static CharacterCreationMagicResonanceEditorState Project(
        CharacterCreationMagicResonanceState state)
    {
        if (!TryProject(state, out CharacterCreationMagicResonanceEditorState? projected))
            throw new InvalidOperationException(
                CharacterCreationMagicResonancePresentationContract.PresentationProjectionInvalid);
        return projected!;
    }

    public static bool TryProject(
        CharacterCreationMagicResonanceState? state,
        out CharacterCreationMagicResonanceEditorState? projected)
    {
        projected = null;
        if (!HasValidCoreShape(state))
            return false;

        CharacterCreationMagicResonanceTalentOption talent = state!.SelectedTalent!;
        CharacterCreationMagicResonanceSelections selections = state.PendingDraft?.Selections
            ?? new CharacterCreationMagicResonanceSelections(null, null, [], [], []);
        CharacterCreationMagicResonanceBudgetState[] budgets =
        [
            CopyBudget(state.TraditionBudget),
            CopyBudget(state.StreamBudget),
            CopyBudget(state.AdeptPowerPointBudget),
            CopyBudget(state.SpellBudget),
            CopyBudget(state.ComplexFormBudget)
        ];
        string[] blockers = state.Blockers
            .Concat(talent.Blockers)
            .Concat(budgets.SelectMany(static budget => budget.Blockers))
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        string[] sourceAnchors = talent.SourceAnchorIds
            .Concat(state.PendingDraft?.SourceAnchorIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static anchor => anchor, StringComparer.Ordinal)
            .ToArray();

        projected = new CharacterCreationMagicResonanceEditorState(
            CharacterCreationMagicResonancePresentationContract.Schema,
            state.Binding,
            new CharacterCreationMagicResonanceTalentProjection(
                talent.Identity,
                talent.Rank,
                talent.Name,
                talent.Kind,
                talent.Magic,
                talent.Resonance,
                talent.Depth,
                talent.SpellBudget,
                talent.ComplexFormBudget,
                talent.AdeptPowerPointBudget,
                talent.RequiresTradition,
                talent.RequiresStream,
                talent.AllowsAdeptPowers,
                talent.AllowsSpells,
                talent.AllowsComplexForms,
                talent.RequiredMetatypeNames.ToArray(),
                talent.RequiredMetatypeCategories.ToArray(),
                talent.ForbiddenMetatypeNames.ToArray(),
                talent.IsEnabled,
                talent.Blockers.ToArray(),
                talent.SourceAnchorIds.ToArray(),
                talent.SourceNodeDigest),
            ProjectOptions(state.Authority.Traditions),
            ProjectOptions(state.Authority.Streams),
            ProjectOptions(state.Authority.AdeptPowers),
            ProjectOptions(state.Authority.Spells),
            ProjectOptions(state.Authority.ComplexForms),
            NormalizeSelections(selections),
            budgets,
            blockers,
            sourceAnchors,
            state.PendingDraft is not null,
            state.CanEdit && blockers.Length == 0,
            state.SnapshotDigest);
        return true;
    }

    public static CharacterCreationMagicResonanceDesktopDraft CreateDraft(
        CharacterCreationMagicResonanceEditorState state,
        CharacterCreationMagicResonanceOptionIdentity? tradition,
        CharacterCreationMagicResonanceOptionIdentity? stream,
        IReadOnlyList<CharacterCreationAdeptPowerAllocation>? adeptPowers,
        IReadOnlyList<CharacterCreationMagicResonanceOptionIdentity>? spells,
        IReadOnlyList<CharacterCreationMagicResonanceOptionIdentity>? complexForms)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.Equals(
                state.Schema,
                CharacterCreationMagicResonancePresentationContract.Schema,
                StringComparison.Ordinal)
            || !state.CanEdit)
        {
            throw new InvalidOperationException("Magic/Resonance editing is not backed by current Core authority.");
        }

        var selections = NormalizeSelections(new CharacterCreationMagicResonanceSelections(
            tradition,
            stream,
            adeptPowers ?? [],
            spells ?? [],
            complexForms ?? []));
        ValidateSelectionIdentities(state, selections);
        return new CharacterCreationMagicResonanceDesktopDraft(
            state.Binding,
            selections,
            state.CoreSnapshotDigest);
    }

    public static CharacterCreationMagicResonanceReview Review(
        ICharacterCreationMagicResonanceService service,
        CharacterCreationMagicResonanceEditorState currentState,
        CharacterCreationMagicResonanceDesktopDraft draft)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(draft);
        if (!BindingsEqual(currentState.Binding, draft.ExpectedBinding)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                currentState.CoreSnapshotDigest,
                draft.ExpectedCoreSnapshotDigest))
        {
            throw new InvalidOperationException("Magic/Resonance authority changed before review.");
        }
        ValidateSelectionIdentities(currentState, draft.Selections);

        CharacterCreationFoundationResult<CharacterCreationMagicResonancePreview> result = service.Preview(
            new CharacterCreationMagicResonancePreviewRequest(
                draft.ExpectedBinding,
                draft.Selections));
        if (result.Value is not { } preview
            || !string.Equals(
                result.Outcome,
                preview.CanConfirm
                    ? CharacterCreationFoundationOutcomes.Success
                    : CharacterCreationFoundationOutcomes.Blocked,
                StringComparison.Ordinal)
            || !IsValidPreview(currentState, draft, preview, result.Blockers))
            throw new InvalidOperationException("Core did not return a valid Magic/Resonance review.");
        return new CharacterCreationMagicResonanceReview(draft, preview);
    }

    public static CharacterCreationMagicResonanceConfirmation Confirm(
        ICharacterCreationMagicResonanceService service,
        CharacterCreationMagicResonanceReview review,
        string idempotencyKey,
        bool explicitlyConfirmed)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(review);
        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> beforeResult =
            service.Load(new CharacterCreationMagicResonanceLoadRequest(
                review.Draft.ExpectedBinding.WorkspaceId));
        if (!string.Equals(
                beforeResult.Outcome,
                CharacterCreationFoundationOutcomes.Success,
                StringComparison.Ordinal)
            || beforeResult.Value is not { } beforeCoreState
            || !TryProject(beforeCoreState, out CharacterCreationMagicResonanceEditorState? before)
            || before!.Binding.ContentRevision < review.Draft.ExpectedBinding.ContentRevision)
        {
            throw new InvalidOperationException(
                "Current Core Magic/Resonance state is unavailable before confirmation.");
        }
        bool replayCandidate = before.Binding.ContentRevision
                               > review.Draft.ExpectedBinding.ContentRevision;
        if (!replayCandidate
            && (!BindingsEqual(before.Binding, review.Draft.ExpectedBinding)
                || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                    before.CoreSnapshotDigest,
                    review.Draft.ExpectedCoreSnapshotDigest)))
        {
            throw new InvalidOperationException("Magic/Resonance authority changed before confirmation.");
        }

        CharacterCreationFoundationResult<CharacterCreationMagicResonanceReceipt> result = service.Confirm(
            new CharacterCreationMagicResonanceConfirmRequest(
                review.Draft.ExpectedBinding,
                review.Draft.Selections,
                review.Preview.PreviewDigest,
                idempotencyKey,
                explicitlyConfirmed));
        if (!string.Equals(result.Outcome, CharacterCreationFoundationOutcomes.Success, StringComparison.Ordinal)
            || result.Value is not { } receipt
            || result.Blockers.Count != 0)
        {
            throw new InvalidOperationException(
                result.Blockers.FirstOrDefault()
                ?? "Core rejected the Magic/Resonance confirmation.");
        }

        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> persistedResult =
            service.Load(new CharacterCreationMagicResonanceLoadRequest(
                review.Draft.ExpectedBinding.WorkspaceId));
        if (!string.Equals(
                persistedResult.Outcome,
                CharacterCreationFoundationOutcomes.Success,
                StringComparison.Ordinal)
            || persistedResult.Value is not { } persistedCoreState
            || !TryProject(persistedCoreState, out CharacterCreationMagicResonanceEditorState? persisted)
            || !persisted!.CanEdit)
        {
            throw new InvalidOperationException(
                "Core confirmation succeeded but the persisted Magic/Resonance projection is invalid.");
        }

        ValidateReceipt(review, receipt, persisted!, replayCandidate);
        bool isCurrentDraft = persistedCoreState.PendingDraft is { } pending
            && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                pending.DraftDigest,
                receipt.DraftDigest)
            && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                CharacterCreationMagicResonanceDigest.Compute(pending.Selections),
                CharacterCreationMagicResonanceDigest.Compute(review.Preview.Selections));
        if (!replayCandidate && !isCurrentDraft)
            throw new InvalidOperationException(
                "The newly committed Core receipt is not the current persisted Magic/Resonance draft.");
        return new CharacterCreationMagicResonanceConfirmation(
            receipt,
            persisted,
            replayCandidate,
            isCurrentDraft);
    }

    private static bool HasValidCoreShape(CharacterCreationMagicResonanceState? state)
    {
        if (state is null
            || !string.Equals(state.Schema, CharacterCreationMagicResonanceSchemas.SnapshotV1, StringComparison.Ordinal)
            || state.Binding is null
            || state.Authority is null
            || state.PrerequisiteDraft is null
            || state.AttributesDraft is null
            || state.SelectedTalent is null
            || state.Blockers is null
            || state.TraditionBudget is null
            || state.StreamBudget is null
            || state.AdeptPowerPointBudget is null
            || state.SpellBudget is null
            || state.ComplexFormBudget is null
            || !CharacterCreationMagicResonanceDraftIntegrity.IsValidAuthority(state.Authority)
            || !CharacterCreationMagicResonancePresentationContract.IsSupportedTalentKind(
                state.SelectedTalent.Kind)
            || state.Authority.Talents.Count(candidate =>
                candidate.Identity == state.SelectedTalent.Identity
                && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                    CharacterCreationMagicResonanceDigest.Compute(candidate),
                    CharacterCreationMagicResonanceDigest.Compute(state.SelectedTalent))) != 1
            || string.IsNullOrWhiteSpace(state.Binding.WorkspaceId.Value)
            || state.Binding.ContentRevision <= 0
            || state.Binding.SavedRevision < 0
            || state.Binding.SavedRevision > state.Binding.ContentRevision
            || !BindingDigestsAreCanonical(state.Binding)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.AuthorityDigest,
                state.Authority.AuthorityDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.SourceInputsDigest,
                state.Authority.SourceInputsDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.CustomDataInputsDigest,
                state.Authority.CustomDataInputsDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.GmPolicyDigest,
                state.Authority.GmPolicyDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.RuntimeDigest,
                state.Authority.RuntimeDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.PrerequisiteAuthorityDigest,
                state.Authority.PrerequisiteAuthorityDigest)
            || state.Binding.PrerequisiteDraftRevision != state.PrerequisiteDraft.DraftRevision
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.PrerequisiteDraftDigest,
                state.PrerequisiteDraft.DraftDigest)
            || state.Binding.AttributesDraftRevision != state.AttributesDraft.DraftRevision
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.Binding.AttributesDraftDigest,
                state.AttributesDraft.DraftDigest)
            || !string.Equals(
                state.PrerequisiteDraft.BuildMethod,
                CharacterCreationMagicResonancePresentationContract.BuildMethod,
                StringComparison.Ordinal)
            || !string.Equals(
                state.PrerequisiteDraft.SettingsProfileId,
                state.Authority.SettingsProfileId,
                StringComparison.Ordinal)
            || !PrerequisiteSelectsExactTalent(
                state.PrerequisiteDraft,
                state.SelectedTalent)
            || !BudgetsAreValid(state)
            || !CharacterCreationMagicResonanceDigest.IsCanonical(state.SnapshotDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                state.SnapshotDigest,
                CharacterCreationMagicResonanceDigest.Compute(state with { SnapshotDigest = string.Empty })))
        {
            return false;
        }

        bool expectedCanEdit = state.Blockers.Count == 0
                               && state.SelectedTalent.IsEnabled
                               && state.SelectedTalent.Blockers.Count == 0;
        if (state.CanEdit != expectedCanEdit)
            return false;
        if (state.PendingDraft is not null
            && !CharacterCreationMagicResonanceDraftIntegrity.IsStructurallyValidPending(
                state.PendingDraft,
                state.Binding.WorkspaceId,
                state.Binding.ContentRevision,
                state.Binding.RawCharacterXmlDigest,
                state.PrerequisiteDraft,
                state.AttributesDraft,
                state.Authority))
        {
            return false;
        }
        return state.Authority.Traditions.Count <= MaximumCatalogOptionsPerKind
               && state.Authority.Streams.Count <= MaximumCatalogOptionsPerKind
               && state.Authority.AdeptPowers.Count <= MaximumCatalogOptionsPerKind
               && state.Authority.Spells.Count <= MaximumCatalogOptionsPerKind
               && state.Authority.ComplexForms.Count <= MaximumCatalogOptionsPerKind;
    }

    private static bool PrerequisiteSelectsExactTalent(
        CharacterCreationPrerequisiteDraft prerequisite,
        CharacterCreationMagicResonanceTalentOption talent)
    {
        CharacterCreationPriorityTalentSelection? selected = prerequisite.TalentSelection;
        CharacterCreationPriorityAssignment[] assignments = prerequisite.Assignments
            .Where(static assignment => string.Equals(
                assignment.CategoryId,
                CharacterCreationPriorityCategoryIds.Talent,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return selected is not null
               && assignments.Length == 1
               && string.Equals(assignments[0].Rank, talent.Rank, StringComparison.Ordinal)
               && string.Equals(
                   assignments[0].SourceId,
                   talent.Identity.PrioritySourceId,
                   StringComparison.Ordinal)
               && string.Equals(
                   selected.SelectionId,
                   talent.Identity.TalentSelectionId,
                   StringComparison.Ordinal)
               && string.Equals(
                   selected.PrioritySourceId,
                   talent.Identity.PrioritySourceId,
                   StringComparison.Ordinal)
               && string.Equals(selected.Value, talent.Identity.TalentValue, StringComparison.Ordinal)
               && selected.Magic.GetValueOrDefault() == talent.Magic
               && selected.Resonance.GetValueOrDefault() == talent.Resonance
               && selected.Depth.GetValueOrDefault() == talent.Depth
               && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                   selected.PriorityChildNodeDigest,
                   talent.SourceNodeDigest)
               && selected.SourceAnchorIds.SequenceEqual(
                   talent.SourceAnchorIds,
                   StringComparer.Ordinal);
    }

    private static bool BindingDigestsAreCanonical(CharacterCreationMagicResonanceBinding binding)
        => CharacterCreationMagicResonanceDigest.IsCanonical(binding.RawCharacterXmlDigest)
           && IsLowerRawSha256(binding.AuxiliaryStateDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.PrerequisiteDraftDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.PrerequisiteAuthorityDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.AttributesDraftDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.AuthorityDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.SourceInputsDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.CustomDataInputsDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.GmPolicyDigest)
           && CharacterCreationMagicResonanceDigest.IsCanonical(binding.RuntimeDigest);

    private static bool IsLowerRawSha256(string? value)
        => value is { Length: 64 }
           && value.All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool BudgetsAreValid(CharacterCreationMagicResonanceState state)
    {
        CharacterCreationMagicResonanceBudgetState[] budgets =
        [
            state.TraditionBudget,
            state.StreamBudget,
            state.AdeptPowerPointBudget,
            state.SpellBudget,
            state.ComplexFormBudget
        ];
        string[] expectedKinds =
        [
            CharacterCreationMagicResonanceKinds.Tradition,
            CharacterCreationMagicResonanceKinds.Stream,
            CharacterCreationMagicResonanceKinds.AdeptPower,
            CharacterCreationMagicResonanceKinds.Spell,
            CharacterCreationMagicResonanceKinds.ComplexForm
        ];
        return state.TraditionBudget.Total == (state.SelectedTalent!.RequiresTradition ? 1m : 0m)
               && state.StreamBudget.Total == (state.SelectedTalent.RequiresStream ? 1m : 0m)
               && state.AdeptPowerPointBudget.Total == state.SelectedTalent.AdeptPowerPointBudget
               && state.SpellBudget.Total == state.SelectedTalent.SpellBudget
               && state.ComplexFormBudget.Total == state.SelectedTalent.ComplexFormBudget
               && budgets.Select(static budget => budget.Kind)
                   .SequenceEqual(expectedKinds, StringComparer.Ordinal)
               && budgets.All(static budget =>
                   budget.Total >= 0m
                   && budget.Used >= 0m
                   && budget.Used <= budget.Total
                   && budget.Remaining == budget.Total - budget.Used
                   && budget.Blockers is not null);
    }

    private static IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> ProjectOptions(
        IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> options) => options
        .Select(static option => new CharacterCreationMagicResonanceOptionProjection(
            option.Identity,
            option.Name,
            option.Category,
            option.PointCost,
            option.MaximumLevels,
            option.SourceBook,
            option.Page,
            option.DrainExpression,
            option.IsEnabled,
            option.Blockers.ToArray(),
            option.SourceAnchorIds.ToArray(),
            option.SourceNodeDigest))
        .ToArray();

    private static CharacterCreationMagicResonanceBudgetState CopyBudget(
        CharacterCreationMagicResonanceBudgetState budget) => budget with
    {
        Blockers = budget.Blockers.ToArray()
    };

    private static void ValidateSelectionIdentities(
        CharacterCreationMagicResonanceEditorState state,
        CharacterCreationMagicResonanceSelections selections)
    {
        if (selections.Tradition is not null && !state.Talent.RequiresTradition
            || selections.Stream is not null && !state.Talent.RequiresStream
            || selections.AdeptPowers.Count != 0 && !state.Talent.AllowsAdeptPowers
            || selections.Spells.Count != 0 && !state.Talent.AllowsSpells
            || selections.ComplexForms.Count != 0 && !state.Talent.AllowsComplexForms)
        {
            throw new InvalidOperationException(
                "A Magic/Resonance selection is not allowed by Core's exact Talent projection.");
        }
        ValidateSingle(selections.Tradition, state.Traditions, CharacterCreationMagicResonanceKinds.Tradition);
        ValidateSingle(selections.Stream, state.Streams, CharacterCreationMagicResonanceKinds.Stream);
        ValidateMany(
            selections.AdeptPowers.Select(static item => item.Identity),
            state.AdeptPowers,
            CharacterCreationMagicResonanceKinds.AdeptPower);
        ValidateMany(selections.Spells, state.Spells, CharacterCreationMagicResonanceKinds.Spell);
        ValidateMany(selections.ComplexForms, state.ComplexForms, CharacterCreationMagicResonanceKinds.ComplexForm);
        if (selections.AdeptPowers.Any(allocation => allocation.Levels < 1
            || state.AdeptPowers.Single(option => option.Identity == allocation.Identity).MaximumLevels
               < allocation.Levels))
        {
            throw new InvalidOperationException("An adept-power level is outside Core's projected bounds.");
        }
    }

    private static void ValidateSingle(
        CharacterCreationMagicResonanceOptionIdentity? identity,
        IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> catalog,
        string expectedKind)
    {
        if (identity is null)
            return;
        ValidateMany([identity], catalog, expectedKind);
    }

    private static void ValidateMany(
        IEnumerable<CharacterCreationMagicResonanceOptionIdentity> identities,
        IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> catalog,
        string expectedKind)
    {
        CharacterCreationMagicResonanceOptionIdentity[] values = identities.ToArray();
        if (values.Distinct().Count() != values.Length
            || values.Any(identity => !string.Equals(identity.Kind, expectedKind, StringComparison.Ordinal)
                || catalog.Count(option => option.Identity == identity
                    && option.IsEnabled
                    && option.Blockers.Count == 0) != 1))
        {
            throw new InvalidOperationException("A Magic/Resonance selection identity is invalid or duplicated.");
        }
    }

    private static bool IsValidPreview(
        CharacterCreationMagicResonanceEditorState state,
        CharacterCreationMagicResonanceDesktopDraft draft,
        CharacterCreationMagicResonancePreview preview,
        IReadOnlyList<string> resultBlockers)
        => string.Equals(preview.Schema, CharacterCreationMagicResonanceSchemas.PreviewV1, StringComparison.Ordinal)
           && BindingsEqual(preview.Binding, draft.ExpectedBinding)
           && preview.Talent.Identity == state.Talent.Identity
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               preview.Talent.SourceNodeDigest,
               state.Talent.SourceNodeDigest)
           && string.Equals(preview.Talent.Kind, state.Talent.Kind, StringComparison.Ordinal)
           && preview.Talent.Magic == state.Talent.Magic
           && preview.Talent.Resonance == state.Talent.Resonance
           && preview.Talent.Depth == state.Talent.Depth
           && preview.Talent.SpellBudget == state.Talent.SpellBudget
           && preview.Talent.ComplexFormBudget == state.Talent.ComplexFormBudget
           && preview.Talent.AdeptPowerPointBudget == state.Talent.AdeptPowerPointBudget
           && preview.Talent.RequiresTradition == state.Talent.RequiresTradition
           && preview.Talent.RequiresStream == state.Talent.RequiresStream
           && preview.Talent.AllowsAdeptPowers == state.Talent.AllowsAdeptPowers
           && preview.Talent.AllowsSpells == state.Talent.AllowsSpells
           && preview.Talent.AllowsComplexForms == state.Talent.AllowsComplexForms
           && preview.Talent.RequiredMetatypeNames.SequenceEqual(
               state.Talent.RequiredMetatypeNames,
               StringComparer.Ordinal)
           && preview.Talent.RequiredMetatypeCategories.SequenceEqual(
               state.Talent.RequiredMetatypeCategories,
               StringComparer.Ordinal)
           && preview.Talent.ForbiddenMetatypeNames.SequenceEqual(
               state.Talent.ForbiddenMetatypeNames,
               StringComparer.Ordinal)
           && preview.Talent.IsEnabled == state.Talent.IsEnabled
           && preview.Talent.Blockers.SequenceEqual(state.Talent.Blockers, StringComparer.Ordinal)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               CharacterCreationMagicResonanceDigest.Compute(preview.Selections),
               CharacterCreationMagicResonanceDigest.Compute(draft.Selections))
           && resultBlockers.Where(static item => !string.IsNullOrWhiteSpace(item))
               .ToHashSet(StringComparer.Ordinal)
               .SetEquals(preview.Blockers.Where(static item => !string.IsNullOrWhiteSpace(item)))
           && preview.RequiresExplicitConfirmation
           && preview.CanConfirm == (preview.Blockers.Count == 0)
           && PreviewShapeIsValid(state, preview)
           && CharacterCreationMagicResonanceDigest.IsCanonical(preview.PreviewDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               preview.PreviewDigest,
               CharacterCreationMagicResonanceDigest.Compute(preview with { PreviewDigest = string.Empty }));

    private static bool PreviewShapeIsValid(
        CharacterCreationMagicResonanceEditorState state,
        CharacterCreationMagicResonancePreview preview)
    {
        CharacterCreationMagicResonanceBudgetState[] budgets =
        [
            preview.TraditionBudget,
            preview.StreamBudget,
            preview.AdeptPowerPointBudget,
            preview.SpellBudget,
            preview.ComplexFormBudget
        ];
        string[] expectedKinds =
        [
            CharacterCreationMagicResonanceKinds.Tradition,
            CharacterCreationMagicResonanceKinds.Stream,
            CharacterCreationMagicResonanceKinds.AdeptPower,
            CharacterCreationMagicResonanceKinds.Spell,
            CharacterCreationMagicResonanceKinds.ComplexForm
        ];
        if (!budgets.Select(static budget => budget.Kind)
                .SequenceEqual(expectedKinds, StringComparer.Ordinal)
            || preview.TraditionBudget.Total != (state.Talent.RequiresTradition ? 1m : 0m)
            || preview.StreamBudget.Total != (state.Talent.RequiresStream ? 1m : 0m)
            || preview.AdeptPowerPointBudget.Total != state.Talent.AdeptPowerPointBudget
            || preview.SpellBudget.Total != state.Talent.SpellBudget
            || preview.ComplexFormBudget.Total != state.Talent.ComplexFormBudget
            || budgets.Any(static budget =>
                budget.Total < 0m
                || budget.Used < 0m
                || budget.Used > budget.Total
                || budget.Remaining != budget.Total - budget.Used
                || budget.Blockers is null)
            || preview.CanConfirm && budgets.Any(static budget => budget.Remaining != 0m))
        {
            return false;
        }

        HashSet<string> allowedAnchors = state.Talent.SourceAnchorIds
            .Concat(state.Traditions.SelectMany(static option => option.SourceAnchorIds))
            .Concat(state.Streams.SelectMany(static option => option.SourceAnchorIds))
            .Concat(state.AdeptPowers.SelectMany(static option => option.SourceAnchorIds))
            .Concat(state.Spells.SelectMany(static option => option.SourceAnchorIds))
            .Concat(state.ComplexForms.SelectMany(static option => option.SourceAnchorIds))
            .ToHashSet(StringComparer.Ordinal);
        return preview.SourceAnchorIds.Count > 0
               && preview.SourceAnchorIds.All(anchor => allowedAnchors.Contains(anchor))
               && preview.SourceAnchorIds.SequenceEqual(
                   preview.SourceAnchorIds.Distinct(StringComparer.Ordinal)
                       .OrderBy(static anchor => anchor, StringComparer.Ordinal),
                   StringComparer.Ordinal)
               && state.Talent.SourceAnchorIds.All(anchor =>
                   preview.SourceAnchorIds.Contains(anchor, StringComparer.Ordinal));
    }

    private static void ValidateReceipt(
        CharacterCreationMagicResonanceReview review,
        CharacterCreationMagicResonanceReceipt receipt,
        CharacterCreationMagicResonanceEditorState persisted,
        bool replayCandidate)
    {
        CharacterCreationMagicResonanceBinding expected = review.Draft.ExpectedBinding;
        if (!CharacterCreationMagicResonanceDigest.IsValidReceipt(
                receipt,
                expected.WorkspaceId,
                persisted.Binding.ContentRevision)
            || receipt.PreviousContentRevision != expected.ContentRevision
            || receipt.SavedRevision != receipt.ContentRevision
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                receipt.PreviewDigest,
                review.Preview.PreviewDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(receipt.AuthorityDigest, expected.AuthorityDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(receipt.SourceInputsDigest, expected.SourceInputsDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(receipt.CustomDataInputsDigest, expected.CustomDataInputsDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(receipt.GmPolicyDigest, expected.GmPolicyDigest)
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(receipt.RuntimeDigest, expected.RuntimeDigest)
            || !string.Equals(receipt.TalentKind, review.Preview.Talent.Kind, StringComparison.Ordinal)
            || receipt.AdeptPowerPointsRemaining != review.Preview.AdeptPowerPointBudget.Remaining
            || receipt.SpellsRemaining != review.Preview.SpellBudget.Remaining
            || receipt.ComplexFormsRemaining != review.Preview.ComplexFormBudget.Remaining
            || receipt.CharacterDocumentChanged
            || persisted.Binding.ContentRevision < receipt.ContentRevision
            || persisted.Binding.SavedRevision < receipt.SavedRevision
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                persisted.Binding.RawCharacterXmlDigest,
                expected.RawCharacterXmlDigest)
            || !replayCandidate
            && (persisted.Binding.ContentRevision != receipt.ContentRevision
                || persisted.Binding.SavedRevision != receipt.SavedRevision
                || CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                    persisted.Binding.AuxiliaryStateDigest,
                    expected.AuxiliaryStateDigest)))
        {
            throw new InvalidOperationException("Core returned an invalid Magic/Resonance receipt or persisted projection.");
        }
    }

    private static bool BindingsEqual(
        CharacterCreationMagicResonanceBinding left,
        CharacterCreationMagicResonanceBinding right)
        => left.WorkspaceId == right.WorkspaceId
           && left.ContentRevision == right.ContentRevision
           && left.SavedRevision == right.SavedRevision
           && left.PrerequisiteDraftRevision == right.PrerequisiteDraftRevision
           && left.AttributesDraftRevision == right.AttributesDraftRevision
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.RawCharacterXmlDigest, right.RawCharacterXmlDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.AuxiliaryStateDigest, right.AuxiliaryStateDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.PrerequisiteDraftDigest, right.PrerequisiteDraftDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.PrerequisiteAuthorityDigest, right.PrerequisiteAuthorityDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.AttributesDraftDigest, right.AttributesDraftDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.AuthorityDigest, right.AuthorityDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.SourceInputsDigest, right.SourceInputsDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.CustomDataInputsDigest, right.CustomDataInputsDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.GmPolicyDigest, right.GmPolicyDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(left.RuntimeDigest, right.RuntimeDigest);

    private static CharacterCreationMagicResonanceSelections NormalizeSelections(
        CharacterCreationMagicResonanceSelections selections) => new(
        selections.Tradition,
        selections.Stream,
        (selections.AdeptPowers ?? [])
            .OrderBy(static item => item.Identity.Kind, StringComparer.Ordinal)
            .ThenBy(static item => item.Identity.SourceId, StringComparer.Ordinal)
            .ThenBy(static item => item.Levels)
            .ToArray(),
        (selections.Spells ?? [])
            .OrderBy(static item => item.Kind, StringComparer.Ordinal)
            .ThenBy(static item => item.SourceId, StringComparer.Ordinal)
            .ToArray(),
        (selections.ComplexForms ?? [])
            .OrderBy(static item => item.Kind, StringComparer.Ordinal)
            .ThenBy(static item => item.SourceId, StringComparer.Ordinal)
            .ToArray());
}
