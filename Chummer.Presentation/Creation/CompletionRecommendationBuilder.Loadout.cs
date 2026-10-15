using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Authored preferences over canonical source categories/names, never ratings,
    // prices or admission rules. Core's enabled catalog supplies every identity.
    // Bounded alternatives leave the existing purchase evaluation limits intact.
    private const int MaximumLoadoutCandidatesPerKind = 8;

    private static bool HasLoadoutInputShape(CharacterCreationLoadoutCompletionInputs? inputs)
        => inputs is null || inputs is { Authority: { Options: not null, Blockers: not null },
                OriginalSelections: { Weapons: not null, Armor: not null } }
            && inputs.Authority.Options.All(row => row is not null && row.Blockers is not null)
            && inputs.OriginalSelections.Weapons.All(row => row is not null)
            && inputs.OriginalSelections.Armor.All(row => row is not null);

    private static CharacterCreationLoadoutCatalogOption? AddMissingLoadoutAdvice(
        CharacterCreationLoadoutCompletionInputs? inputs, string archetypeId,
        IReadOnlyList<CharacterCreationPurchaseCompletionTarget> accepted,
        Func<CharacterCreationPurchaseCompletionTarget, bool> tryAdd,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (inputs is null || !CharacterCreationLoadoutRules.IsValidAuthority(inputs.Authority)
            || inputs.OriginalSelections is not { Weapons: not null, Armor: not null }) return null;

        // One existing choice locks its whole category; a second weapon or armor
        // is a deliberate player choice, not a missing default to be filled.
        if (inputs.OriginalSelections.Armor.Count == 0 && accepted.All(row => row.Armor is null))
            foreach (var option in LoadoutCandidates(inputs.Authority, CharacterCreationLoadoutKinds.Armor, archetypeId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tryAdd(new() { Armor = new(option.OptionId) })) break;
            }
        if (inputs.OriginalSelections.Weapons.Count == 0 && accepted.All(row => row.Weapon is null))
            foreach (var option in LoadoutCandidates(inputs.Authority, CharacterCreationLoadoutKinds.Weapon, archetypeId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tryAdd(new() { Weapon = new(option.OptionId) })) return option;
            }

        // A retained weapon still locks its category, but may justify loose
        // ammunition. Resolve its exact canonical identity without proposing a
        // replacement or changing any saved quantity. Core admits the saved
        // draft/receipt history and prices every resulting equipment basket.
        foreach (var saved in inputs.OriginalSelections.Weapons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = inputs.Authority.Options.Where(option => option.OptionId == saved.OptionId
                && option.Kind == CharacterCreationLoadoutKinds.Weapon && option.IsSelectable
                && option.PricingIsExact && option.AvailabilityIsExact && option.Blockers.Count == 0)
                .Take(2).ToArray();
            if (options.Length == 1 && options[0].Category is "Assault Rifles" or "Submachine Guns"
                or "Heavy Pistols" or "Light Pistols" or "Holdouts")
                return options[0];
        }
        return null;
    }

    private static IEnumerable<CharacterCreationLoadoutCatalogOption> LoadoutCandidates(
        CharacterCreationLoadoutAuthority authority, string kind, string archetypeId)
    {
        string[] preferences = kind == CharacterCreationLoadoutKinds.Armor
            ? archetypeId == CompletionAssistantRules.Face
                ? ["Actioneer Business Clothes", "Lined Coat", "Armor Vest", "Armor Jacket", "Armor Clothing"]
                : ["Armor Jacket", "Armor Vest", "Lined Coat", "Armor Clothing"]
            : archetypeId switch
            {
                CompletionAssistantRules.StreetSamurai => ["Assault Rifles", "Submachine Guns", "Heavy Pistols", "Light Pistols"],
                CompletionAssistantRules.Face => ["Light Pistols", "Holdouts", "Tasers", "Heavy Pistols"],
                CompletionAssistantRules.Mage => ["Tasers", "Light Pistols", "Heavy Pistols"],
                _ => ["Light Pistols", "Tasers", "Heavy Pistols"]
            };
        return authority.Options.Where(option => option.Kind == kind && option.IsSelectable
                && option.PricingIsExact && option.AvailabilityIsExact && option.Blockers.Count == 0)
            .Select(option => (Option: option, Rank: Array.IndexOf(preferences,
                kind == CharacterCreationLoadoutKinds.Armor ? option.Name : option.Category)))
            .Where(row => row.Rank >= 0)
            .GroupBy(row => row.Rank).OrderBy(group => group.Key)
            .SelectMany(group => group.OrderBy(row => row.Option.UnitCost)
                .ThenBy(row => row.Option.OptionId, StringComparer.Ordinal).Take(2))
            .Take(MaximumLoadoutCandidatesPerKind).Select(row => row.Option);
    }

    private static CharacterCreationLoadoutCompletionProjection? BuildLoadoutSummary(
        CharacterCreationCompletionReviewInputs inputs, CharacterCreationCompletionPlan? plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (inputs.Loadout is null || plan?.Purchases?.Loadout is not { } selections) return null;
        // Project the genuine final selection, never an earlier candidate or a
        // generic Gear substitute. This read-only Core call supplies display rows.
        var result = CharacterCreationLoadoutCompletionService.Preview(inputs.Loadout, selections);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Value;
    }

    private static IEnumerable<CompletionCategoryCoverage> BuildLoadoutCoverage(
        CharacterCreationLoadoutCompletionInputs? inputs, CharacterCreationLoadoutCompletionProjection? projection)
    {
        // Leave legacy snapshots and their coverage unchanged. Callers opt into
        // the real reader's LoadCompletionInputsWithLoadout observation.
        if (inputs is null) yield break;
        foreach (string kind in new[] { CharacterCreationLoadoutKinds.Weapon, CharacterCreationLoadoutKinds.Armor })
        {
            var blockers = projection?.Blockers ?? inputs.Authority.Blockers;
            bool present = projection is not null && blockers.Count == 0
                && projection.Lines.Any(line => line.Kind == kind);
            yield return new(kind, present ? CompletionCoverageState.CoreValidated : CompletionCoverageState.NeedsChoice,
                present ? "completion.coverage." + kind + "-validated" : "completion.remaining." + kind + "-choices",
                blockers.ToArray());
        }
        yield return new("loadout-extras", CompletionCoverageState.Unsupported,
            "completion.remaining.ammo-binding-loading-modifications", []);
        yield return new("advanced-gear", CompletionCoverageState.Unsupported,
            "completion.remaining.commlinks-rated-gear-child-graphs", []);
    }
}
