using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.CreationWizard.Presentation.Tests;

internal static class CharacterCreationMagicResonanceTestFixture
{
    internal static readonly CharacterWorkspaceId WorkspaceId = new("ws-wizard");
    internal static readonly CharacterCreationMagicResonanceOptionIdentity TraditionId =
        new(CharacterCreationMagicResonanceKinds.Tradition, "22fb2874-050a-4273-9ab8-7967c6cbd93f");
    internal static readonly CharacterCreationMagicResonanceOptionIdentity SpellOneId =
        new(CharacterCreationMagicResonanceKinds.Spell, "5a4587a1-361b-4d1a-a6f8-d9f951739d1d");
    internal static readonly CharacterCreationMagicResonanceOptionIdentity SpellTwoId =
        new(CharacterCreationMagicResonanceKinds.Spell, "82b4f523-a309-4ff4-9c94-a703cbb32dab");

    internal static CharacterCreationMagicResonanceSelections CompleteSelections { get; } = new(
        TraditionId,
        null,
        [],
        [SpellOneId, SpellTwoId],
        []);

    internal static CharacterCreationMagicResonanceState CreateState(
        string rawCharacterXmlDigest,
        long contentRevision = 12,
        string talentKind = CharacterCreationMagicResonanceKinds.Magician)
    {
        CharacterCreationMagicResonanceAuthority authority = CreateAuthority(talentKind);
        CharacterCreationPrerequisiteDraft prerequisite = CreatePrerequisite(authority, rawCharacterXmlDigest);
        CharacterCreationAttributesDraft attributes = CreateAttributes(prerequisite, rawCharacterXmlDigest);
        var binding = new CharacterCreationMagicResonanceBinding(
            WorkspaceId,
            contentRevision,
            contentRevision,
            rawCharacterXmlDigest,
            new string('9', 64),
            prerequisite.DraftRevision,
            prerequisite.DraftDigest,
            prerequisite.AuthorityDigest,
            attributes.DraftRevision,
            attributes.DraftDigest,
            authority.AuthorityDigest,
            authority.SourceInputsDigest,
            authority.CustomDataInputsDigest,
            authority.GmPolicyDigest,
            authority.RuntimeDigest);
        CharacterCreationMagicResonanceTalentOption talent = authority.Talents.Single();
        var state = new CharacterCreationMagicResonanceState(
            CharacterCreationMagicResonanceSchemas.SnapshotV1,
            binding,
            authority,
            prerequisite,
            attributes,
            talent,
            PendingDraft: null,
            Budget(CharacterCreationMagicResonanceKinds.Tradition, 1m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.Stream, 0m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.AdeptPower, 0m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.Spell, 2m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.ComplexForm, 0m, 0m),
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty);
        return WithSnapshotDigest(state);
    }

    internal static CharacterCreationMagicResonancePreview CreatePreview(
        CharacterCreationMagicResonanceState state,
        CharacterCreationMagicResonanceSelections selections)
    {
        var blockers = new List<string>();
        decimal traditionUsed = selections.Tradition == TraditionId ? 1m : 0m;
        if (traditionUsed == 0m)
            blockers.Add(CharacterCreationMagicResonanceBlockers.TraditionRequired);
        if (selections.Stream is not null
            || selections.AdeptPowers.Count != 0
            || selections.ComplexForms.Count != 0)
            blockers.Add(CharacterCreationMagicResonanceBlockers.OptionInvalid);
        if (selections.Spells.Distinct().Count() != selections.Spells.Count
            || selections.Spells.Any(identity => identity != SpellOneId && identity != SpellTwoId))
            blockers.Add(CharacterCreationMagicResonanceBlockers.OptionInvalid);
        if (selections.Spells.Count < 2)
            blockers.Add(CharacterCreationMagicResonanceBlockers.SpellBudgetIncomplete);
        if (selections.Spells.Count > 2)
            blockers.Add(CharacterCreationMagicResonanceBlockers.SpellBudgetExceeded);

        string[] normalizedBlockers = blockers.Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        string[] anchors = state.SelectedTalent!.SourceAnchorIds
            .Concat(selections.Tradition is null
                ? []
                : state.Authority.Traditions.Single().SourceAnchorIds)
            .Concat(selections.Spells.SelectMany(identity => state.Authority.Spells
                .Where(option => option.Identity == identity)
                .SelectMany(option => option.SourceAnchorIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static anchor => anchor, StringComparer.Ordinal)
            .ToArray();
        var preview = new CharacterCreationMagicResonancePreview(
            CharacterCreationMagicResonanceSchemas.PreviewV1,
            state.Binding,
            state.SelectedTalent,
            selections,
            Budget(CharacterCreationMagicResonanceKinds.Tradition, 1m, traditionUsed),
            Budget(CharacterCreationMagicResonanceKinds.Stream, 0m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.AdeptPower, 0m, 0m),
            Budget(CharacterCreationMagicResonanceKinds.Spell, 2m, selections.Spells.Count),
            Budget(CharacterCreationMagicResonanceKinds.ComplexForm, 0m, 0m),
            anchors,
            normalizedBlockers,
            RequiresExplicitConfirmation: true,
            CanConfirm: normalizedBlockers.Length == 0,
            PreviewDigest: string.Empty);
        return preview with
        {
            PreviewDigest = CharacterCreationMagicResonanceDigest.Compute(
                preview with { PreviewDigest = string.Empty })
        };
    }

    internal static (CharacterCreationMagicResonanceState State, CharacterCreationMagicResonanceReceipt Receipt)
        CreateConfirmed(
            CharacterCreationMagicResonanceState before,
            CharacterCreationMagicResonancePreview preview,
            string idempotencyKey)
    {
        long nextRevision = before.Binding.ContentRevision + 1;
        string keyDigest = CharacterCreationMagicResonanceDigest.ComputeUtf8(idempotencyKey);
        string commandDigest = Digest('c');
        var draft = new CharacterCreationMagicResonanceDraft(
            CharacterCreationMagicResonanceSchemas.DraftV1,
            before.Binding.WorkspaceId,
            DraftRevision: 1,
            BaseContentRevision: before.Binding.ContentRevision,
            BaseRawCharacterXmlDigest: before.Binding.RawCharacterXmlDigest,
            before.Binding.PrerequisiteDraftRevision,
            before.Binding.PrerequisiteDraftDigest,
            before.Binding.PrerequisiteAuthorityDigest,
            before.Binding.AttributesDraftRevision,
            before.Binding.AttributesDraftDigest,
            before.Binding.AuthorityDigest,
            before.Binding.SourceInputsDigest,
            before.Binding.CustomDataInputsDigest,
            before.Binding.GmPolicyDigest,
            before.Binding.RuntimeDigest,
            before.SelectedTalent!.Identity,
            before.SelectedTalent.Kind,
            before.SelectedTalent.Magic,
            before.SelectedTalent.Resonance,
            before.SelectedTalent.Depth,
            preview.Selections,
            preview.TraditionBudget,
            preview.StreamBudget,
            preview.AdeptPowerPointBudget,
            preview.SpellBudget,
            preview.ComplexFormBudget,
            preview.SourceAnchorIds,
            CharacterEffectsApplied: false,
            LastIdempotencyKeyDigest: keyDigest,
            LastPreviewDigest: preview.PreviewDigest,
            LastCommandDigest: commandDigest,
            DraftDigest: string.Empty);
        draft = draft with
        {
            DraftDigest = CharacterCreationMagicResonanceDraftIntegrity.ComputeDigest(draft)
        };
        var receipt = new CharacterCreationMagicResonanceReceipt(
            CharacterCreationMagicResonanceSchemas.ReceiptV1,
            before.Binding.WorkspaceId,
            before.Binding.ContentRevision,
            nextRevision,
            nextRevision,
            draft.DraftRevision,
            draft.DraftDigest,
            preview.PreviewDigest,
            keyDigest,
            commandDigest,
            CharacterCreationMagicResonanceDigest.ReceiptLedgerRootDigest,
            before.Binding.AuthorityDigest,
            before.Binding.SourceInputsDigest,
            before.Binding.CustomDataInputsDigest,
            before.Binding.GmPolicyDigest,
            before.Binding.RuntimeDigest,
            before.SelectedTalent.Kind,
            preview.AdeptPowerPointBudget.Remaining,
            decimal.ToInt32(preview.SpellBudget.Remaining),
            decimal.ToInt32(preview.ComplexFormBudget.Remaining),
            CharacterDocumentChanged: false,
            ReceiptDigest: string.Empty);
        receipt = receipt with
        {
            ReceiptDigest = CharacterCreationMagicResonanceDigest.ComputeReceipt(receipt)
        };
        CharacterCreationMagicResonanceBinding binding = before.Binding with
        {
            ContentRevision = nextRevision,
            SavedRevision = nextRevision,
            AuxiliaryStateDigest = new string('8', 64)
        };
        var persisted = before with
        {
            Binding = binding,
            PendingDraft = draft,
            TraditionBudget = preview.TraditionBudget,
            StreamBudget = preview.StreamBudget,
            AdeptPowerPointBudget = preview.AdeptPowerPointBudget,
            SpellBudget = preview.SpellBudget,
            ComplexFormBudget = preview.ComplexFormBudget,
            SnapshotDigest = string.Empty
        };
        return (WithSnapshotDigest(persisted), receipt);
    }

    internal static string Digest(char value) => "sha256:" + new string(value, 64);

    private static CharacterCreationMagicResonanceAuthority CreateAuthority(string talentKind)
    {
        var talent = new CharacterCreationMagicResonanceTalentOption(
            new CharacterCreationMagicResonanceTalentIdentity(
                "0487cf47-7ad1-4f4d-a38f-09a094e0e246",
                "talent-0",
                talentKind == CharacterCreationMagicResonanceKinds.ArtificialIntelligence
                    ? "A.I."
                    : "Magician"),
            Rank: "A",
            Name: talentKind == CharacterCreationMagicResonanceKinds.ArtificialIntelligence
                ? "A.I."
                : "Magician",
            Kind: talentKind,
            Magic: talentKind == CharacterCreationMagicResonanceKinds.ArtificialIntelligence ? 0 : 6,
            Resonance: 0,
            Depth: talentKind == CharacterCreationMagicResonanceKinds.ArtificialIntelligence ? 6 : 0,
            SpellBudget: talentKind == CharacterCreationMagicResonanceKinds.ArtificialIntelligence ? 0 : 2,
            ComplexFormBudget: 0,
            AdeptPowerPointBudget: 0m,
            RequiresTradition: talentKind != CharacterCreationMagicResonanceKinds.ArtificialIntelligence,
            RequiresStream: false,
            AllowsAdeptPowers: false,
            AllowsSpells: talentKind != CharacterCreationMagicResonanceKinds.ArtificialIntelligence,
            AllowsComplexForms: false,
            RequiredMetatypeNames: [],
            RequiredMetatypeCategories: [],
            ForbiddenMetatypeNames: [],
            SourceNodeDigest: Digest('1'),
            SourceAnchorIds: ["priorities.xml#priority:magic-a:talent:0"],
            Blockers: [],
            IsEnabled: true);
        CharacterCreationMagicResonanceCatalogOption tradition = Option(
            TraditionId,
            "Hermetic",
            "magic-tradition",
            1m,
            "traditions.xml#tradition:hermetic",
            '2');
        CharacterCreationMagicResonanceCatalogOption spellOne = Option(
            SpellOneId,
            "Armor",
            "health",
            1m,
            "spells.xml#spell:armor",
            '3');
        CharacterCreationMagicResonanceCatalogOption spellTwo = Option(
            SpellTwoId,
            "Heal",
            "health",
            1m,
            "spells.xml#spell:heal",
            '4');
        var authority = new CharacterCreationMagicResonanceAuthority(
            CharacterCreationMagicResonanceSchemas.AuthorityV1,
            "settings-profile",
            Digest('5'),
            Digest('6'),
            Digest('7'),
            Digest('a'),
            Digest('b'),
            [talent],
            [new CharacterCreationMagicResonanceMetatypeCapability(
                "1916c7ef-9b05-4bf9-81c8-e1eef64b6736",
                "Human",
                "Metahuman",
                ["metatypes.xml#metatype:human"],
                Digest('d'))],
            [tradition],
            [],
            [],
            [spellOne, spellTwo],
            [],
            ["priorities.xml", "spells.xml", "traditions.xml"],
            [],
            IsAuthoritative: true,
            AuthorityDigest: string.Empty);
        return authority with
        {
            AuthorityDigest = CharacterCreationMagicResonanceDigest.Compute(
                authority with { AuthorityDigest = string.Empty })
        };
    }

    private static CharacterCreationMagicResonanceCatalogOption Option(
        CharacterCreationMagicResonanceOptionIdentity identity,
        string name,
        string category,
        decimal cost,
        string anchor,
        char digest) => new(
        CharacterCreationMagicResonanceSchemas.CatalogOptionV1,
        identity,
        name,
        category,
        cost,
        MaximumLevels: 1,
        SourceBook: "SR5",
        Page: "172",
        SourceNodeDigest: Digest(digest),
        SourceAnchorIds: [anchor],
        Blockers: [],
        IsEnabled: true);

    private static CharacterCreationPrerequisiteDraft CreatePrerequisite(
        CharacterCreationMagicResonanceAuthority authority,
        string rawDigest)
    {
        var draft = new CharacterCreationPrerequisiteDraft(
            CharacterCreationPrerequisiteSchemas.DraftV1,
            WorkspaceId,
            DraftRevision: 2,
            BaseContentRevision: 10,
            BaseRawCharacterXmlDigest: rawDigest,
            AuthorityDigest: authority.PrerequisiteAuthorityDigest,
            BuildMethod: CharacterCreationBuildMethods.Priority,
            SettingsProfileId: authority.SettingsProfileId,
            PriorityTable: "Standard",
            PriorityArray: ["A", "B", "C", "D", "E"],
            SumToTenTarget: null,
            Assignments:
            [
                new CharacterCreationPriorityAssignment(
                    Order: 0,
                    CategoryId: CharacterCreationPriorityCategoryIds.Talent,
                    Rank: authority.Talents[0].Rank,
                    SourceId: authority.Talents[0].Identity.PrioritySourceId,
                    SourceNodeDigest: Digest('0'),
                    SumToTenValue: 4,
                    BaseNormalAttributePoints: null,
                    SourceAnchorIds: ["priorities.xml#priority:magic-a"])
            ],
            CreationKarmaTotal: 25,
            CreationKarmaUsed: 0,
            SourceAnchorIds: ["priorities.xml"],
            DraftDigest: Digest('e'))
        {
            TalentSelection = new CharacterCreationPriorityTalentSelection(
                authority.Talents[0].Identity.TalentSelectionId,
                authority.Talents[0].Identity.PrioritySourceId,
                authority.Talents[0].Name,
                authority.Talents[0].Identity.TalentValue,
                SpecialAttributePoints: 0,
                Magic: authority.Talents[0].Magic,
                Resonance: authority.Talents[0].Resonance,
                Depth: authority.Talents[0].Depth,
                GrantedQualities: [],
                authority.Talents[0].SourceNodeDigest,
                authority.Talents[0].SourceAnchorIds)
        };
        return draft;
    }

    private static CharacterCreationAttributesDraft CreateAttributes(
        CharacterCreationPrerequisiteDraft prerequisite,
        string rawDigest) => new(
        CharacterCreationAttributesSchemas.DraftV1,
        WorkspaceId,
        DraftRevision: 3,
        BaseContentRevision: 11,
        BaseRawCharacterXmlDigest: rawDigest,
        prerequisite.DraftRevision,
        prerequisite.DraftDigest,
        prerequisite.AuthorityDigest,
        MetatypeSourceId: "1916c7ef-9b05-4bf9-81c8-e1eef64b6736",
        MetatypeSourceNodeDigest: Digest('d'),
        HalvesNormalAttributePoints: false,
        NormalPointTotal: 24,
        NormalPointUsed: 24,
        SpecialPointTotal: 5,
        SpecialPointUsed: 5,
        CreationKarmaTotal: 25,
        CreationKarmaUsed: 0,
        Allocations: [],
        Attributes: [],
        SourceAnchorIds: ["metatypes.xml#metatype:human"],
        CharacterEffectsApplied: false,
        DraftDigest: Digest('f'));

    private static CharacterCreationMagicResonanceBudgetState Budget(
        string kind,
        decimal total,
        decimal used) => new(
        kind,
        total,
        Math.Min(used, total),
        Math.Max(0m, total - used),
        used <= total ? [] : [CharacterCreationMagicResonanceBlockers.OptionInvalid]);

    private static CharacterCreationMagicResonanceState WithSnapshotDigest(
        CharacterCreationMagicResonanceState state) => state with
    {
        SnapshotDigest = CharacterCreationMagicResonanceDigest.Compute(
            state with { SnapshotDigest = string.Empty })
    };
}

internal sealed class StubMagicResonanceService : ICharacterCreationMagicResonanceService
{
    private CharacterCreationMagicResonanceState _state;
    private CharacterCreationMagicResonanceReceipt? _receipt;
    private string? _idempotencyKey;

    internal StubMagicResonanceService(CharacterCreationMagicResonanceState state)
    {
        _state = state;
    }

    internal int LoadCalls { get; private set; }
    internal int PreviewCalls { get; private set; }
    internal int ConfirmCalls { get; private set; }

    public CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> Load(
        CharacterCreationMagicResonanceLoadRequest request)
    {
        LoadCalls++;
        return request.WorkspaceId == _state.Binding.WorkspaceId
            ? new(CharacterCreationFoundationOutcomes.Success, _state, _state.Blockers)
            : new(CharacterCreationFoundationOutcomes.Missing, null,
                [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);
    }

    public CharacterCreationFoundationResult<CharacterCreationMagicResonancePreview> Preview(
        CharacterCreationMagicResonancePreviewRequest request)
    {
        PreviewCalls++;
        if (request.Binding != _state.Binding)
            return new(CharacterCreationFoundationOutcomes.Conflict, null,
                [CharacterCreationMagicResonanceBlockers.StaleWorkspaceRevision]);
        CharacterCreationMagicResonancePreview preview =
            CharacterCreationMagicResonanceTestFixture.CreatePreview(_state, request.Selections);
        return new(
            preview.CanConfirm
                ? CharacterCreationFoundationOutcomes.Success
                : CharacterCreationFoundationOutcomes.Blocked,
            preview,
            preview.Blockers);
    }

    public CharacterCreationFoundationResult<CharacterCreationMagicResonanceReceipt> Confirm(
        CharacterCreationMagicResonanceConfirmRequest request)
    {
        ConfirmCalls++;
        if (_receipt is not null && string.Equals(_idempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
            return new(CharacterCreationFoundationOutcomes.Success, _receipt, []);
        if (!request.ExplicitlyConfirmed)
            return new(CharacterCreationFoundationOutcomes.Invalid, null,
                [CharacterCreationMagicResonanceBlockers.ExplicitConfirmationRequired]);
        if (request.Binding != _state.Binding)
            return new(CharacterCreationFoundationOutcomes.Conflict, null,
                [CharacterCreationMagicResonanceBlockers.StaleWorkspaceRevision]);
        CharacterCreationMagicResonancePreview preview =
            CharacterCreationMagicResonanceTestFixture.CreatePreview(_state, request.Selections);
        if (!preview.CanConfirm
            || !CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                preview.PreviewDigest,
                request.PreviewDigest))
            return new(CharacterCreationFoundationOutcomes.Blocked, null, preview.Blockers);
        (_state, _receipt) = CharacterCreationMagicResonanceTestFixture.CreateConfirmed(
            _state,
            preview,
            request.IdempotencyKey);
        _idempotencyKey = request.IdempotencyKey;
        return new(CharacterCreationFoundationOutcomes.Success, _receipt, []);
    }
}
