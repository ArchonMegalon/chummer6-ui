using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Authored utility preferences, not permissions or rule authority. All IDs
    // must resolve uniquely in the current enabled Core source before use.
    private static readonly string[] TraditionAdvice =
    [
        "19320625-bc1a-492f-8904-da6a847e5700", // Hermetic
        "8d185e0e-5f49-4992-babd-d1ac9c848f68"  // Shamanic
    ];
    private static readonly string[] StreamAdvice = ["7a3ecfbe-616e-425d-b204-329de37ffdbb"];
    private static readonly string[] SpellAdvice =
    [
        "c09e8bb5-4bed-44f9-a41c-bed6a4deb871", // Heal
        "47423962-6b73-4cc3-ad4e-e8d037cf9507", // Stunbolt
        "1d9430e9-3ae9-4c0a-ba60-ee92c245ee08", // Improved Invisibility
        "8be2c62c-b2c9-435e-8721-dfad01f5161a", // Levitate
        "1e1e488d-d727-43f8-900d-5c171c320d1d", // Physical Barrier
        "37b3d6ac-624a-42d4-bd6e-a12142dc5725", // Increase Reflexes
        "9bcd193f-b149-4c04-bb0e-8b2d66dae241", // Detect Life, Extended
        "b1d545b6-2498-4682-8058-a3f7783c1c84", // Mana Barrier
        "e81dda32-1c97-482b-a6a5-ddd12eded03a", // Analyze Device
        "036b3b19-caa7-4da1-b2de-f887ac3a6d20", // Magic Fingers
        "794f21eb-dc8d-45e8-b9b0-b85b810437f3", // Armor
        "c307d243-669b-47d0-9642-aee7f9207e63", // Silence
        "a6bd05ed-b72f-4886-bb8b-6aba947e8cf5", // Combat Sense
        "d82c56f8-9231-48a9-9dea-dea6c513caad"  // Clairvoyance
    ];
    private static readonly string[] ComplexFormAdvice =
    [
        "373638b9-4334-4645-99f5-c3673e4f809b", // Cleaner
        "6b4ed8d5-75c8-4415-9578-15afa4ac8494", // Editor
        "dbb1d719-c829-4c45-9a53-9ff538865c14", // Static Veil
        "42cc98b0-2b3f-42a0-bbe7-fb7d2633d11a", // Resonance Veil
        "cfebf27d-707e-4ea2-a376-394738a11b3c", // Resonance Channel
        "7cd49196-0912-43c5-b1a1-c9da96432340", // Stitches
        "704abd70-c0e6-4f06-b186-53a7cb856584", // Resonance Spike
        "f64ccd1d-634f-4cf3-a020-ab03247abf25"  // Static Bomb
    ];

    /// <summary>Source-bound identity choices for the editable proposal, not a
    /// finalization admission. Core evaluates the chosen identity with the actual
    /// proposed attributes. Existing identities are never replacement candidates.</summary>
    public static IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> GetMagicIdentityChoices(
        CharacterCreationCompletionReviewInputs inputs, string kind)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!SupportsMagicAdvice(inputs)) return [];
        var magic = inputs.Magic;
        IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> options = kind switch
        {
            CharacterCreationMagicResonanceKinds.Tradition when SupportsSpellAdvice(magic.SelectedTalent.Kind)
                && magic.SelectedTalent.RequiresTradition
                && magic.Selections.Tradition is null => magic.Authority.Traditions,
            CharacterCreationMagicResonanceKinds.Stream when magic.SelectedTalent.Kind
                == CharacterCreationMagicResonanceKinds.Technomancer && magic.SelectedTalent.RequiresStream
                && magic.Selections.Stream is null => magic.Authority.Streams,
            _ => []
        };
        string[] order = kind == CharacterCreationMagicResonanceKinds.Tradition ? TraditionAdvice : StreamAdvice;
        return AdmittedMagicOptions(options, kind).OrderBy(row =>
                Array.IndexOf(order, row.Identity.SourceId) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.Identity.SourceId, StringComparer.Ordinal)
            .Select(row => row with { SourceAnchorIds = row.SourceAnchorIds.ToArray(), Blockers = row.Blockers.ToArray() })
            .ToArray();
    }

    private static CharacterCreationMagicCompletionRequest AddMissingMagicAdvice(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CharacterCreationMagicCompletionRequest request)
    {
        if (!SupportsMagicAdvice(inputs)) return request;
        bool spellcaster = SupportsSpellAdvice(inputs.Magic.SelectedTalent.Kind);
        return request with
        {
            RankedTraditions = IdentityAdvice(CharacterCreationMagicResonanceKinds.Tradition, intent.PreferredTradition),
            RankedStreams = IdentityAdvice(CharacterCreationMagicResonanceKinds.Stream, intent.PreferredStream),
            RankedSpells = spellcaster && inputs.Magic.SelectedTalent.AllowsSpells
                ? RankAdmitted(inputs.Magic.Authority.Spells, CharacterCreationMagicResonanceKinds.Spell, SpellAdvice) : [],
            RankedComplexForms = inputs.Magic.SelectedTalent.Kind == CharacterCreationMagicResonanceKinds.Technomancer
                && inputs.Magic.SelectedTalent.AllowsComplexForms
                ? RankAdmitted(inputs.Magic.Authority.ComplexForms, CharacterCreationMagicResonanceKinds.ComplexForm, ComplexFormAdvice) : []
            // ManualSelections stays the exact original. No PP purchase or
            // adept power allocation is inferred by this tradition/spell/stream/form slice.
        };

        CharacterCreationMagicResonanceOptionIdentity[] IdentityAdvice(string kind,
            CharacterCreationMagicResonanceOptionIdentity? preferred)
        {
            var choices = GetMagicIdentityChoices(inputs, kind);
            return preferred is not null ? choices.Where(row => row.Identity == preferred).Select(row => row.Identity).ToArray()
                : choices.Take(CharacterCreationMagicCompletionPlanner.MaximumCandidatesPerKind).Select(row => row.Identity).ToArray();
        }
    }

    private static CharacterCreationMagicResonanceOptionIdentity[] RankAdmitted(
        IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> options, string kind, IReadOnlyList<string> rankedIds)
    {
        var admitted = AdmittedMagicOptions(options, kind).ToDictionary(row => row.Identity.SourceId, StringComparer.Ordinal);
        return rankedIds.Where(admitted.ContainsKey).Select(id => admitted[id].Identity).ToArray();
    }

    private static IEnumerable<CharacterCreationMagicResonanceCatalogOption> AdmittedMagicOptions(
        IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> options, string kind)
        => options.Where(row => row?.Identity?.Kind == kind).GroupBy(row => row.Identity)
            .Where(group => group.Count() == 1).Select(group => group.Single())
            .Where(row => row.IsEnabled && row.Blockers is { Count: 0 } && row.SourceAnchorIds is not null
                && Text(row.Identity.SourceId, 256, false) && !string.IsNullOrWhiteSpace(row.Name));

    private static bool SupportsMagicAdvice(CharacterCreationCompletionReviewInputs inputs)
        => inputs.Magic is { Authority: { IsAuthoritative: true, Blockers.Count: 0,
                Traditions: not null, Streams: not null, Spells: not null, ComplexForms: not null },
            SelectedTalent: { IsEnabled: true, Blockers.Count: 0 }, Selections: not null }
            && (SupportsSpellAdvice(inputs.Magic.SelectedTalent.Kind)
                || inputs.Magic.SelectedTalent.Kind == CharacterCreationMagicResonanceKinds.Technomancer);

    private static bool SupportsSpellAdvice(string talentKind)
        => talentKind is CharacterCreationMagicResonanceKinds.Magician or CharacterCreationMagicResonanceKinds.MysticAdept;

    private static bool ValidMagicPreferences(CharacterCreationCompletionReviewInputs inputs,
        CompletionAssistantIntent intent, bool fillRemainingResources)
    {
        return (intent.PreferredTradition is null && intent.PreferredStream is null)
            || fillRemainingResources && Matches(intent.PreferredTradition, inputs.Magic.Selections.Tradition,
                    CharacterCreationMagicResonanceKinds.Tradition)
                && Matches(intent.PreferredStream, inputs.Magic.Selections.Stream, CharacterCreationMagicResonanceKinds.Stream);

        bool Matches(CharacterCreationMagicResonanceOptionIdentity? preferred,
            CharacterCreationMagicResonanceOptionIdentity? original, string kind)
            => preferred is null || original is not null && preferred == original
                || original is null && GetMagicIdentityChoices(inputs, kind).Any(row => row.Identity == preferred);
    }

    private static bool ValidMagicPreferenceShape(CharacterCreationMagicResonanceOptionIdentity? choice, string kind)
        => choice is null || choice.Kind == kind && Text(choice.SourceId, 256, false);
}
