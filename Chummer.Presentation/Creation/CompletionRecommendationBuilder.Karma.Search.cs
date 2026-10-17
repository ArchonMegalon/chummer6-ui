using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // This replaces, rather than wraps, the old greedy attribute-karma pass.
    // Local quotes are bounded independently of the expensive whole-graph work.
    private const int MaximumKarmaAttributeQuotes = 96;
    private const int MaximumKarmaCombinationReviews = 8;
    private const int MaximumKarmaSkillCombinationReviews = 6;
    private const int MaximumKarmaFinalPlans = 2;
    private sealed record KarmaCompletion(ResourceCompletion Completed, IReadOnlyList<CompletionCategoryCoverage> Coverage);
    private sealed record KarmaAttributeBasket(CharacterCreationAttributeAllocation[] Allocations,
        CharacterCreationAttributeCompletionTarget[] Targets, int Cost, int Score, string Key);
    private sealed record KarmaCombination(KarmaAttributeBasket Attribute, KarmaQualityBasket Quality, KarmaSkillBasket Skill)
    {
        public int Score => Attribute.Score + Quality.Score + Skill.Score;
        public long Cost => (long)Attribute.Cost + Quality.Cost + Skill.Cost;
    }

    private static KarmaCompletion CompleteKarmaAdvice(CharacterCreationCompletionReviewInputs inputs,
        CompletionAssistantIntent intent, ResourceCompletion completed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = completed.Request;
        KarmaSkillSearch? skillSearch = null;
        var baselineResult = completed.Evaluation ?? CharacterCreationCompletionPlanner.Plan(inputs, request, cancellationToken);
        completed = new(request, baselineResult);
        // Unallocated special points are a separate unfinished pool, not a
        // failed Karma purchase. Retain that exact Core blocker and outcome;
        // never waive a rules failure or turn a partial plan into success.
        if (baselineResult.Value is not { Attributes: not null, Skills: not null, Magic: not null, Purchases: not null } baseline
            || !HasOnlyUnspentSpecialPoints(baselineResult)
            || baseline.Review.Balances.RemainingCreationKarma is not int remaining)
            return Finish(completed, "completion.karma.baseline-unavailable");
        int reserve = Math.Max(request.KarmaToReserve, request.Magic.KarmaToReserve);
        if (remaining <= reserve) return Finish(completed,
            reserve > 0 ? "completion.karma.explicit-reserve" : "completion.karma.wallet-spent");

        var advice = AddMissingAttributeKarmaAdvice(inputs, request, intent.ArchetypeId);
        var attributes = QuoteKarmaAttributeBaskets(inputs, baseline, advice, intent.ArchetypeId, remaining - reserve, cancellationToken);
        var qualities = QuoteKarmaQualityBaskets(inputs, baseline, intent.ArchetypeId, cancellationToken);
        skillSearch = QuoteKarmaSkillBaskets(inputs, baseline, request, intent.ArchetypeId, remaining - reserve, cancellationToken);
        // These deltas come from Core quotes, and only rank/prune the bounded
        // search. Contact discounts/dependencies can change the final wallet;
        // only a successful whole review and plan can accept a candidate.
        var ranked = RankKarmaCombinations(attributes, qualities, skillSearch.Baskets, remaining - reserve, cancellationToken);
        // Keep review/plan room for existing advice when a whole-graph modifier
        // or dependency rejects the independently priced skill candidates.
        var combinations = ranked.ToList();
        if (skillSearch.Baskets.Count > 1)
        {
            combinations = ranked.Take(MaximumKarmaSkillCombinationReviews).ToList();
            foreach (var fallback in RankKarmaCombinations(attributes, qualities, [skillSearch.Baskets[0]], remaining - reserve, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!combinations.Contains(fallback)) combinations.Add(fallback);
                if (combinations.Count >= MaximumKarmaCombinationReviews) break;
            }
        }
        bool splitFinalPlans = combinations.Any(row => row.Skill.Score > 0) && combinations.Any(row => row.Skill.Score == 0);
        int finalPlans = 0;
        int skillFinalPlans = 0;
        int otherFinalPlans = 0;
        foreach (var candidate in combinations.Take(MaximumKarmaCombinationReviews))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool addsSkills = candidate.Skill.Score > 0;
            if (splitFinalPlans && (addsSkills ? skillFinalPlans : otherFinalPlans) > 0) continue;
            var selections = baseline.Selections with
            {
                AttributeAllocations = candidate.Attribute.Allocations, QualityOptionIds = candidate.Quality.OptionIds,
                SkillKarmaPurchases = candidate.Skill.Purchases
            };
            var reviewed = CharacterCreationCompletionReviewService.Review(inputs,
                new(request.InputsSnapshotDigest, selections), cancellationToken);
            if (reviewed.Outcome != CharacterCreationFoundationOutcomes.Success
                || reviewed.Value is not { Blockers.Count: 0 } review
                || review.Balances.RemainingCreationKarma is not int wallet || wallet < reserve
                || request.NuyenToReserve > 0 && (review.Balances.RemainingNuyen is not decimal nuyen || nuyen < request.NuyenToReserve))
            {
                if (addsSkills)
                    skillSearch = skillSearch with { Blockers = skillSearch.Blockers.Concat(reviewed.Blockers)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                continue;
            }
            bool addsAttributes = candidate.Attribute.Targets.Length > 0;
            var proposed = request with
            {
                QualityOptionIds = candidate.Quality.OptionIds,
                SkillKarmaPurchases = candidate.Skill.Purchases,
                AllocateRemainingAttributeKarma = addsAttributes,
                Attributes = request.Attributes with
                {
                    KarmaTargets = addsAttributes ? candidate.Attribute.Targets : null,
                    MaximumAdditionalKarmaSpend = addsAttributes ? candidate.Attribute.Cost : 0
                }
            };
            // Recreate through Core using the original request/bindings. Never
            // assemble a fake plan from local previews or patch a failed result.
            var evaluated = CharacterCreationCompletionPlanner.Plan(inputs, proposed, cancellationToken);
            finalPlans++;
            if (addsSkills) skillFinalPlans++; else otherFinalPlans++;
            if (HasOnlyUnspentSpecialPoints(evaluated)
                && evaluated.Outcome == baselineResult.Outcome
                && evaluated.Blockers.SequenceEqual(baselineResult.Blockers)
                && evaluated.Value is { } plan
                && plan.Review.Balances.RemainingCreationKarma is int finalWallet && finalWallet >= reserve
                && plan.Review.ReviewDigest == review.ReviewDigest
                && CharacterCreationFinalizationDigest.Compute(plan.Selections) == CharacterCreationFinalizationDigest.Compute(selections))
                return Finish(new(proposed, evaluated), "completion.karma.bounded-authored-search");
            if (finalPlans >= MaximumKarmaFinalPlans) break;
        }
        return Finish(completed, "completion.karma.no-accepted-bounded-candidate");

        KarmaCompletion Finish(ResourceCompletion result, string explanation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coverage = new List<CompletionCategoryCoverage>
            {
                new("karma-advice", CompletionCoverageState.OptionalManual, explanation, []),
                new("karma-manual-domains", CompletionCoverageState.OptionalManual,
                    inputs.Skills.KarmaPurchasePolicy is null ? "completion.karma.skills-specializations-manual"
                        : "completion.karma.groups-exotics-modifiers-ungrounded-specializations-manual", [])
            };
            coverage.AddRange(BuildKarmaSkillCoverage(inputs, result, skillSearch, cancellationToken));
            if (result.Evaluation?.Value?.Review.Balances.RemainingCreationKarma is int left && left > 0)
                coverage.Add(new("karma-remaining", CompletionCoverageState.OptionalManual,
                    left <= Math.Max(request.KarmaToReserve, request.Magic.KarmaToReserve)
                        ? "completion.karma.explicit-reserve" : "completion.karma.remaining-bounded-or-manual", []));
            var selected = result.Request.QualityOptionIds.Except(request.QualityOptionIds, StringComparer.Ordinal).ToHashSet();
            foreach (var row in KarmaQualityAdviceFor(intent.ArchetypeId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (inputs.Lifestyles.Qualities.Authority.Options.Any(option => option.SourceId == row.SourceId && selected.Contains(option.OptionId)))
                    coverage.Add(new("karma-quality-" + row.SourceId, CompletionCoverageState.CoreValidated, row.Reason, []));
            }
            return new(result, coverage);
        }
    }

    private static IReadOnlyList<KarmaCombination> RankKarmaCombinations(
        IReadOnlyList<KarmaAttributeBasket> attributes, IReadOnlyList<KarmaQualityBasket> qualities,
        IReadOnlyList<KarmaSkillBasket> skills, int spendable, CancellationToken cancellationToken)
    {
        // Stream the independently capped baskets (at most 97 * 64 * 49).
        // Keep only eight candidates, never materialize/sort a Cartesian product.
        // Costs only prune/settle usefulness ties; this is not an optimality claim.
        var best = new List<KarmaCombination>(MaximumKarmaCombinationReviews + 1);
        foreach (var attribute in attributes)
        foreach (var quality in qualities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((long)attribute.Cost + quality.Cost > spendable) continue;
            foreach (var skill in skills)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = new KarmaCombination(attribute, quality, skill);
                if (candidate.Score <= 0 || candidate.Cost > spendable) continue;
                int at = best.FindIndex(other => CompareKarmaCombinations(candidate, other) < 0);
                if (at < 0) at = best.Count;
                if (at >= MaximumKarmaCombinationReviews) continue;
                best.Insert(at, candidate);
                if (best.Count > MaximumKarmaCombinationReviews) best.RemoveAt(MaximumKarmaCombinationReviews);
            }
        }
        return best;
    }

    private static int CompareKarmaCombinations(KarmaCombination left, KarmaCombination right)
    {
        int order = right.Score.CompareTo(left.Score);
        if (order == 0) order = right.Cost.CompareTo(left.Cost);
        if (order == 0) order = StringComparer.Ordinal.Compare(left.Attribute.Key, right.Attribute.Key);
        if (order == 0) order = StringComparer.Ordinal.Compare(left.Quality.Key, right.Quality.Key);
        return order == 0 ? StringComparer.Ordinal.Compare(left.Skill.Key, right.Skill.Key) : order;
    }

    private static bool HasOnlyUnspentSpecialPoints(CharacterCreationFoundationResult<CharacterCreationCompletionPlan> result)
        => result.Value is { Review.Blockers.Count: 0 } plan
           && (result.Outcome == CharacterCreationFoundationOutcomes.Success
                   && result.Blockers.Count == 0 && plan.Blockers.Count == 0
               || result.Outcome == CharacterCreationFoundationOutcomes.Blocked
                   && result.Blockers.Count > 0
                   && result.Blockers.All(code => code == CharacterCreationAttributeCompletionBlockers.SpecialPointsUnallocated)
                   && plan.Blockers.SequenceEqual(result.Blockers));

    private static IReadOnlyList<KarmaAttributeBasket> QuoteKarmaAttributeBaskets(
        CharacterCreationCompletionReviewInputs inputs, CharacterCreationCompletionPlan baseline,
        CharacterCreationCompletionPlanRequest advice, string archetypeId, int spendable, CancellationToken cancellationToken)
    {
        var original = baseline.Selections.AttributeAllocations.ToArray();
        var baskets = new List<KarmaAttributeBasket> { new(original, [], 0, 0, string.Empty) };
        if (!advice.AllocateRemainingAttributeKarma || advice.Attributes.KarmaTargets is not { Count: > 0 } goals)
            return baskets;
        var state = inputs.Skills.Attributes;
        // Mirror the automatic-permission admission before doing any local work;
        // the final Core planner enforces it independently. A chosen zero is a lock.
        if (advice.Attributes.PermittedAttributeOrder.Any(id => state.PendingDraft is { } draft
            ? draft.PlayerChosenAttributeIds is not { } chosen || chosen.Contains(id, StringComparer.Ordinal)
            : !state.Attributes.Any(row => row.AttributeId == id && row.PriorityPointsSpent == 0 && row.KarmaLevels == 0)))
            return baskets;
        var session = CharacterCreationAttributesService.CreateDraftSession(state);
        if (session is null) return baskets;
        var primary = GetRecommendationPack(archetypeId)!.Attributes.Take(3)
            .Select(row => row.Target.AttributeId).ToHashSet(StringComparer.Ordinal);
        decimal initialCost = baseline.Attributes!.Preview.CreationKarmaBudget.Used;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int quotes = 0;
        for (int index = 0; index < baskets.Count && quotes < MaximumKarmaAttributeQuotes; index++)
        {
            var basket = baskets[index];
            for (int rank = 0; rank < goals.Count && quotes < MaximumKarmaAttributeQuotes; rank++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var goal = goals[rank];
                int at = Array.FindIndex(basket.Allocations, row => row.AttributeId == goal.AttributeId);
                if (at < 0 || basket.Allocations[at].KarmaLevels == int.MaxValue) continue;
                int priorRating = basket.Targets.SingleOrDefault(row => row.AttributeId == goal.AttributeId)?.TargetUnaugmentedRating
                    ?? baseline.Attributes.Preview.Attributes.Single(row => row.AttributeId == goal.AttributeId).UnaugmentedCurrent;
                if (priorRating >= goal.TargetUnaugmentedRating) continue;
                var allocations = basket.Allocations.ToArray();
                allocations[at] = allocations[at] with { KarmaLevels = allocations[at].KarmaLevels + 1 };
                string key = string.Join("|", allocations.Select(row => row.AttributeId + ":" + row.KarmaLevels.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                if (!seen.Add(key)) continue;
                quotes++;
                var evaluated = session.Preview(allocations);
                if (evaluated.Outcome != CharacterCreationFoundationOutcomes.Success
                    || evaluated.Value is not { CanConfirm: true, Blockers.Count: 0 } preview) continue;
                decimal cost = preview.CreationKarmaBudget.Used - initialCost;
                if (cost < 0 || cost > spendable || cost != decimal.Truncate(cost)) continue;
                int rating = preview.Attributes.Single(row => row.AttributeId == goal.AttributeId).UnaugmentedCurrent;
                if (rating <= priorRating) continue;
                var targets = goals.Where(row => row.AttributeId == goal.AttributeId
                        || basket.Targets.Any(target => target.AttributeId == row.AttributeId))
                    .Select(row => new CharacterCreationAttributeCompletionTarget(row.AttributeId,
                        row.AttributeId == goal.AttributeId ? rating
                            : basket.Targets.Single(target => target.AttributeId == row.AttributeId).TargetUnaugmentedRating)).ToArray();
                // One useful level in a primary role foundation ranks above a
                // secondary level; quality weights are authored on the same scale.
                baskets.Add(new(allocations, targets, (int)cost, basket.Score + (primary.Contains(goal.AttributeId) ? 12 : 8), key));
            }
        }
        return baskets;
    }
}
