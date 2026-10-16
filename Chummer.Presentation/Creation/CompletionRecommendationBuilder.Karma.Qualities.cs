using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Authored usefulness weights, NOT prices, legality, or an optimal-build claim.
    // Source identities deliberately exclude story commitments, negative qualities,
    // implants, cost modifiers, and choices needing a player-selected follow-up.
    private sealed record KarmaQualityAdvice(Guid SourceId, int Weight, string Reason);
    private sealed record KarmaQualityBasket(string[] OptionIds, int Cost, int Score, string Key);

    private static KarmaQualityAdvice[] KarmaQualityAdviceFor(string archetypeId)
    {
        bool physical = archetypeId is CompletionAssistantRules.GeneralSurvivor or CompletionAssistantRules.StreetSamurai;
        bool technical = archetypeId is CompletionAssistantRules.Decker or CompletionAssistantRules.Rigger;
        return
        [
            new(new("0d0cb0fc-c79d-418e-9216-8a197369e52d"), 10, "completion.karma.reason.courage"), // Guts
            new(new("68cfe94a-fa7e-4129-a9b9-b5d73e3ced99"), physical ? 8 : 4, "completion.karma.reason.handedness"), // Ambidextrous
            new(new("cf3a62e1-8a41-413e-a679-30c8d1b5a0c1"), 8, "completion.karma.reason.judgment"), // Common Sense
            new(new("fd78fa11-5ab6-40db-bdb8-9fd722b08054"), 8, "completion.karma.reason.timing"), // Perfect Time
            new(new("c0f1c43c-7e50-4a84-ad0d-a0bc28f77836"), technical ? 8 : 4, "completion.karma.reason.reading"), // Speed Reading
            new(new("5c18a150-8e38-4f6d-b4f5-b1b674a3581e"), archetypeId == CompletionAssistantRules.Rigger ? 14 : 0,
                "completion.karma.reason.driving"), // Gearhead
            new(new("27c7ea84-bfe4-47e1-8382-38d9a5c8e680"), technical ? 12 : 0, "completion.karma.reason.repair"), // Juryrigger
            new(new("554ca59c-22e6-46ef-9b05-250ec8abd728"), archetypeId == CompletionAssistantRules.Decker ? 12 : 0,
                "completion.karma.reason.matrix"), // Overclocker
            // These familiar alternatives currently fail Core's effect/requirement
            // projection in the standard source resolver. Keep their authored
            // ranking, but never admit them merely because their price fits.
            new(new("291efdb6-a8b8-49ce-b2be-72f9d3f8a243"), 8, "completion.karma.reason.recovery"), // Quick Healer
            new(new("84305e09-f8d5-4a82-8257-0119b8c3f926"), physical ? 12 : 8, "completion.karma.reason.stealth"), // Catlike
            new(new("225f0d31-8c03-4283-9a22-558ab01a6c47"), physical ? 10 : 6, "completion.karma.reason.mobility"), // Natural Athlete
            new(new("00cc6499-db13-447e-8116-278d317a9e31"), physical ? 10 : 7, "completion.karma.reason.resilience"), // Toughness
            new(new("9d3be1d9-1309-45e7-8bd9-1f5a3ede3522"), technical ? 10 : 6, "completion.karma.reason.memory"), // Photographic Memory
            new(new("5b19dbcd-fb69-4a02-a25a-7ac5342ca576"), technical ? 12 : 6, "completion.karma.reason.analysis"), // Analytical Mind
            new(new("9f25cc0a-c47c-49dd-953c-8fe60628bae8"), archetypeId == CompletionAssistantRules.Face ? 16 : 0,
                "completion.karma.reason.first-impression")
        ];
    }

    private static IReadOnlyList<KarmaQualityBasket> QuoteKarmaQualityBaskets(
        CharacterCreationCompletionReviewInputs inputs, CharacterCreationCompletionPlan baseline,
        string archetypeId, CancellationToken cancellationToken)
    {
        var original = baseline.Selections.QualityOptionIds.ToArray();
        var baskets = new List<KarmaQualityBasket> { new(original, 0, 0, string.Empty) };
        var qualities = inputs.Lifestyles.Qualities;
        if (!qualities.Authority.IsAuthoritative || qualities.Authority.Blockers.Count != 0
            || baseline.Attributes!.Preview.CreationKarmaBudget.Used is < 0 or > int.MaxValue
            || baseline.Attributes.Preview.CreationKarmaBudget.Used != decimal.Truncate(baseline.Attributes.Preview.CreationKarmaBudget.Used))
            return baskets;
        int attributeCost = (int)baseline.Attributes.Preview.CreationKarmaBudget.Used;
        var initial = CharacterCreationQualitiesRules.EvaluateCompletion(
            new(qualities.Binding, qualities.Authority, original), attributeCost);
        if (initial.Blockers.Count != 0) return baskets;
        var retainedSources = initial.Selections.Select(row => row.SourceId)
            .Concat(initial.GrantedQualities.Select(row => row.SourceId)).ToHashSet();
        // At most six distinct sources => at most 63 additional basket quotes.
        // Even rejected combinations consume this fixed, small search space.
        var candidates = KarmaQualityAdviceFor(archetypeId).Where(advice => advice.Weight > 0
                && !retainedSources.Contains(advice.SourceId))
            .Select(advice => (Advice: advice, Options: qualities.Authority.Options.Where(option =>
                option.SourceId == advice.SourceId && option.Type == CharacterCreationQualityType.Positive
                && option.IsSelectable && option.EligibilityIsExact && !option.IsFreeOrGranted
                && !option.IsMetagenic && option.FollowUpChoiceId is null && option.FollowUpChoiceLabel is null
                && option.SourceAnchorIds.Count > 0 && !original.Contains(option.OptionId, StringComparer.Ordinal))
                .OrderBy(option => option.Rating).ThenBy(option => option.OptionId, StringComparer.Ordinal).ToArray()))
            .Where(row => row.Options.Length == 1) // ambiguous/rated choices stay manual
            .OrderByDescending(row => row.Advice.Weight).ThenBy(row => row.Advice.SourceId)
            .Take(6).ToArray();
        foreach (var candidate in candidates)
        {
            foreach (var basket in baskets.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string[] ids = [.. basket.OptionIds, candidate.Options[0].OptionId];
                var quote = CharacterCreationQualitiesRules.EvaluateCompletion(
                    new(qualities.Binding, qualities.Authority, ids), attributeCost);
                // Includes Core quality caps, policy costs, exclusions, and exact
                // eligibility. The final review still admits the entire graph.
                if (quote.Blockers.Count != 0) continue;
                int cost = initial.KarmaRemaining - quote.KarmaRemaining;
                if (cost < 0) continue;
                baskets.Add(new(ids, cost, basket.Score + candidate.Advice.Weight,
                    string.Join("|", ids.Order(StringComparer.Ordinal))));
            }
        }
        return baskets;
    }
}
