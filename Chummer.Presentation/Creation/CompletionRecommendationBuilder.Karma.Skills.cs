using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Future Core package boundary: no policy means no skill purchase proposal.
    // These are advice/search limits, never prices, maxima or rule entitlements.
    private const int MaximumKarmaSkillQuotes = 48;
    private const int MaximumKarmaActiveGoals = 8;
    private const int MaximumKarmaKnowledgeGoals = 4;
    private sealed record KarmaSkillGoal(CharacterCreationSkillCatalogEntry Source, int Rating,
        int Weight, string Reason, CharacterCreationSkillSpecializationOption? Specialization, string SpecializationReason);
    private sealed record KarmaSkillBasket(CharacterCreationSkillKarmaPurchase[]? Purchases, int Cost, int Score, string Key);
    private sealed record KarmaSkillSearch(IReadOnlyList<KarmaSkillBasket> Baskets,
        IReadOnlyList<KarmaSkillGoal> Goals, IReadOnlyList<string> Blockers);

    private static KarmaSkillSearch QuoteKarmaSkillBaskets(CharacterCreationCompletionReviewInputs inputs,
        CharacterCreationCompletionPlan baseline, CharacterCreationCompletionPlanRequest request,
        string archetypeId, int spendable, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var original = baseline.Selections.SkillKarmaPurchases?.ToArray();
        var baskets = new List<KarmaSkillBasket> { new(original, 0, 0, string.Empty) };
        if (inputs.Skills.KarmaPurchasePolicy is null)
            return new(baskets, [], [CharacterCreationSkillKarmaPurchaseBlockers.PolicyUnavailable]);
        if (baseline.Review.Balances.SkillsKarmaCost is not int initialCost)
            return new(baskets, [], [CharacterCreationCompletionReviewSchemas.KarmaBudgetUnavailable]);
        var goals = KarmaSkillGoals(inputs, baseline, request, archetypeId, cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var blockers = new HashSet<string>(StringComparer.Ordinal);
        int quotes = 0;
        for (int index = 0; index < baskets.Count && quotes < MaximumKarmaSkillQuotes; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var basket = baskets[index];
            foreach (var goal in goals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = goal.Source;
                var prior = basket.Purchases?.SingleOrDefault(row => row.Kind == source.Kind && row.SourceSkillId == source.SourceSkillId);
                var point = baseline.Skills!.Preview.Skills.SingleOrDefault(row => row.Kind == source.Kind && row.SourceSkillId == source.SourceSkillId);
                int rating = prior?.Rating ?? point?.Rating ?? 0;
                // Separate rating and specialization alternatives; neither is a
                // point allocation or permission to edit an original skill row.
                foreach (bool specialize in new[] { false, true })
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (quotes >= MaximumKarmaSkillQuotes) break;
                    if (specialize ? rating == 0 || goal.Specialization is null
                            || prior?.SpecializationOptionId is not null || point?.SpecializationOptionId is not null
                        : rating >= goal.Rating) continue;
                    var purchase = new CharacterCreationSkillKarmaPurchase(source.SourceSkillId, source.Kind,
                        source.SourceNodeDigest, specialize ? rating : rating + 1,
                        specialize ? goal.Specialization!.OptionId : prior?.SpecializationOptionId);
                    var purchases = (basket.Purchases ?? []).Where(row => row.Kind != source.Kind || row.SourceSkillId != source.SourceSkillId)
                        .Append(purchase).OrderBy(row => row.Kind, StringComparer.Ordinal)
                        .ThenBy(row => row.SourceSkillId, StringComparer.Ordinal).ToArray();
                    string key = CharacterCreationFinalizationDigest.Compute(purchases);
                    if (!seen.Add(key)) continue;
                    quotes++;
                    var quoted = CharacterCreationSkillsService.PreviewCompletionPurchases(inputs.Skills,
                        baseline.Selections.AttributeAllocations, baseline.Selections.SkillAllocations,
                        baseline.Selections.GroupAllocations, purchases, cancellationToken);
                    if (quoted.Outcome != CharacterCreationFoundationOutcomes.Success
                        || quoted.Value is not { Blockers.Count: 0 } preview || quoted.Blockers.Count != 0)
                    {
                        blockers.UnionWith(quoted.Blockers);
                        continue;
                    }
                    int cost = preview.Skills.Sum(row => row.KarmaCost) - initialCost;
                    if (cost < 0 || cost > spendable) continue;
                    baskets.Add(new(purchases, cost, basket.Score + (specialize ? 8 : goal.Weight), key));
                }
            }
        }
        return new(baskets, goals, blockers.Order(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<KarmaSkillGoal> KarmaSkillGoals(CharacterCreationCompletionReviewInputs inputs,
        CharacterCreationCompletionPlan baseline, CharacterCreationCompletionPlanRequest request,
        string archetypeId, CancellationToken cancellationToken)
    {
        var pack = GetRecommendationPack(archetypeId);
        if (pack is null) return [];
        var permitted = request.Skills.PermittedTargetOrder.Select(SkillIdentity).ToHashSet();
        var retained = inputs.Skills.Allocations.Select(row => SkillIdentity(row.Kind, row.SourceSkillId)).ToHashSet();
        var primary = pack.Skills.Take(3).Select(row => SkillIdentity(row.Target)).ToHashSet();
        var goals = new List<KarmaSkillGoal>();
        // The same source-bound practical knowledge advice as the point pass,
        // with an authored rating-two goal, never a randomly chosen lore focus.
        var advice = pack.Skills.Concat(ResourceActiveAdvice).Concat(ResourceKnowledgeAdvice.Select(row =>
                row with { Target = row.Target with { TargetRating = 2 } }))
            .DistinctBy(row => SkillIdentity(row.Target));
        foreach (var row in advice)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = SkillIdentity(row.Target);
            if (!permitted.Contains(identity) || retained.Contains(identity)) continue;
            bool active = row.Target.Kind == CharacterCreationSkillKinds.Active;
            if (!active && row.Target.Kind != CharacterCreationSkillKinds.Knowledge) continue;
            if (goals.Count(goal => goal.Source.Kind == row.Target.Kind)
                >= (active ? MaximumKarmaActiveGoals : MaximumKarmaKnowledgeGoals)) continue;
            var sources = (active ? inputs.Skills.Authority.ActiveSkills : inputs.Skills.Authority.KnowledgeSkills)
                .Where(source => source.SourceSkillId == row.Target.SourceId).Take(2).ToArray();
            if (sources.Length != 1 || sources[0].SourceAnchorIds.Count == 0) continue;
            var source = sources[0];
            var point = baseline.Skills!.Preview.Skills.SingleOrDefault(value => value.Kind == source.Kind && value.SourceSkillId == source.SourceSkillId);
            if (point?.IsNativeLanguage == true) continue;
            var specialization = GroundedKarmaSpecialization(inputs, baseline, source, archetypeId, cancellationToken);
            if ((point?.Rating ?? 0) >= row.Target.TargetRating
                && (specialization.Option is null || point?.SpecializationOptionId is not null)) continue;
            goals.Add(new(source, row.Target.TargetRating, active ? primary.Contains(identity) ? 12 : 8 : 4,
                row.ReasonKey, specialization.Option, specialization.Reason));
        }
        return goals;
    }

    private static (CharacterCreationSkillSpecializationOption? Option, string Reason) GroundedKarmaSpecialization(
        CharacterCreationCompletionReviewInputs inputs, CharacterCreationCompletionPlan baseline,
        CharacterCreationSkillCatalogEntry source, string archetypeId, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string reason = "completion.karma.reason.equipment-specialization";
        // Canonical source vocabulary, not translated names or first-catalog-row
        // selection. Remote operation is an authored rigger role preference.
        if (archetypeId == CompletionAssistantRules.Rigger
            && source.SourceSkillId == "ae91a8a6-80e7-4f52-b9eb-21725a5528a4")
        {
            names.Add("Remote Operation");
            reason = "completion.karma.reason.remote-operation";
        }
        if (inputs.Loadout is { } loadout && baseline.Selections.Loadout is { } selected)
            foreach (var weapon in selected.Weapons)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var options = loadout.Authority.Options.Where(option => option.OptionId == weapon.OptionId
                    && option.Kind == CharacterCreationLoadoutKinds.Weapon).Take(2).ToArray();
                if (options.Length != 1) continue;
                string category = options[0].Category;
                if (source.SourceSkillId == "788b387b-ee41-4e6a-bf22-481a8cc4cf9f"
                    && category is "Assault Rifles" or "Submachine Guns") names.Add(category);
                if (source.SourceSkillId == "adf31a50-b228-4e09-a09c-46ab9f5e59a1"
                    && category is "Tasers" or "Holdouts") names.Add(category);
            }
        if (names.Count != 1) return (null, reason); // ambiguous context stays manual
        var matches = source.Specializations.Where(option => names.Contains(option.Name)
            && !string.IsNullOrWhiteSpace(option.SourceAnchorId)).Take(2).ToArray();
        return (matches.Length == 1 ? matches[0] : null, reason);
    }

    private static IEnumerable<CompletionCategoryCoverage> BuildKarmaSkillCoverage(
        CharacterCreationCompletionReviewInputs inputs, ResourceCompletion result,
        KarmaSkillSearch? search, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return inputs.Skills.KarmaPurchasePolicy is null
            ? new("karma-skills", CompletionCoverageState.Unsupported, "completion.karma.skill-policy-unavailable",
                [CharacterCreationSkillKarmaPurchaseBlockers.PolicyUnavailable])
            : new("karma-skills", search is null ? CompletionCoverageState.NotEvaluated : CompletionCoverageState.OptionalManual,
                search is null ? "completion.karma.skills-not-evaluated" : "completion.karma.skill-search-bounded", search?.Blockers ?? []);
        if (search is null || result.Evaluation?.Value is not { } plan) yield break;
        foreach (var purchase in plan.Selections.SkillKarmaPurchases ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var goal = search.Goals.SingleOrDefault(row => row.Source.Kind == purchase.Kind && row.Source.SourceSkillId == purchase.SourceSkillId);
            if (goal is null) continue;
            string identity = purchase.Kind + "-" + purchase.SourceSkillId;
            yield return new("karma-skill-" + identity, CompletionCoverageState.CoreValidated, goal.Reason, []);
            // A retained purchase can carry a player's specialization. Only the
            // exact authored option has the equipment/role grounding claimed here.
            if (goal.Specialization is { } specialization && purchase.SpecializationOptionId == specialization.OptionId)
                yield return new("karma-specialization-" + identity, CompletionCoverageState.CoreValidated, goal.SpecializationReason, []);
        }
    }
}
