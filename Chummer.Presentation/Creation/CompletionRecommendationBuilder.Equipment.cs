using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Authored source identities from gear.xml. Neither names nor package prices
    // are identities; the observed Core catalog must still admit every item.
    private static readonly Guid RegularAmmoSource = Guid.Parse("b2a0b340-c793-4322-8422-8b03d18a6fae");
    private static readonly Guid MicroTransceiverSource = Guid.Parse("d1fca845-ae01-4b39-a176-1506da699e0e");
    private static readonly Guid SubvocalMicSource = Guid.Parse("7bdc5866-88ae-45a6-a525-ec03f161f086");
    private static readonly Guid TagEraserSource = Guid.Parse("d0eb5971-03d6-4822-9fb5-c524bf16bf62");
    private static readonly Guid LockpickSetSource = Guid.Parse("0fdeb240-743c-4a9d-9cf1-fef0990575bb");
    private static readonly Guid FlashlightSource = Guid.Parse("c49a893a-d445-4aac-bec0-c8501cba4c2c");

    private static void AddMissingEquipmentAdvice(
        CharacterCreationGearCompletionInputs inputs, CharacterCreationLoadoutCatalogOption? recommendedWeapon,
        string archetypeId, IReadOnlyList<CharacterCreationPurchaseCompletionTarget> accepted,
        Func<IReadOnlyList<CharacterCreationPurchaseCompletionTarget>, bool> tryAdd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authority = inputs.State.Authority;
        if (!authority.IsAuthoritative) return;
        var stocks = new List<CharacterCreationPurchaseCompletionTarget[]>();

        // An accepted new or source-admitted retained conventional firearm can
        // justify loose stock. These categories are advice, not ammunition binding
        // or a claim that a clip is loaded. Tasers need a different source item.
        if (recommendedWeapon?.Category is "Assault Rifles" or "Submachine Guns"
            or "Heavy Pistols" or "Light Pistols" or "Holdouts")
            AddStock(RegularAmmoSource, [100, 50]);

        // A small communications kit, then a role preference: quiet entry for
        // the face/survivor, tag removal for the other supported role packs.
        AddStock(MicroTransceiverSource, [1]);
        AddStock(SubvocalMicSource, [1]);
        AddStock(archetypeId is CompletionAssistantRules.Face or CompletionAssistantRules.GeneralSurvivor
            ? LockpickSetSource : TagEraserSource, [1]);
        AddStock(FlashlightSource, [1]);

        // Core still evaluates every addition and the complete cumulative basket.
        // A funded kit needs one call, rather than replaying all earlier targets
        // for each utility. A rejected batch accepts nothing here: fall back in
        // the original order, including the smaller ammunition quantity.
        if (stocks.Count > 1 && tryAdd(stocks.Select(stock => stock[0]).ToArray())) return;
        foreach (var stock in stocks)
            foreach (var candidate in stock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tryAdd([candidate])) break;
            }

        void AddStock(Guid sourceId, int[] quantities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = authority.Options.Where(option => option.SourceId == sourceId
                && option.IsSelectable && option.PricingIsExact && option.AvailabilityIsExact
                && option.Blockers.Count == 0 && option.PackageQuantity > 0 && option.PackageCost > 0m)
                .Take(2).ToArray();
            if (options.Length != 1) return;
            var option = options[0];
            // Saved quantities are player choices, including a partial package.
            // Never top them up or add a duplicate of an already proposed source.
            if (inputs.State.PendingDraft?.Lines.Any(line => line.SourceId == sourceId
                    || line.OptionId == option.OptionId) == true
                || accepted.Any(target => target.Gear is { } gear && (gear.OptionId == option.OptionId
                    || authority.Options.Any(row => row.SourceId == sourceId && row.OptionId == gear.OptionId)))
                || stocks.Any(stock => stock[0].Gear?.OptionId == option.OptionId))
                return;
            // Quantities are stocking goals, never packages or a UI price.
            stocks.Add(quantities.Select(quantity => new CharacterCreationPurchaseCompletionTarget(
                Gear: new(option.OptionId, quantity))).ToArray());
        }
    }
}
