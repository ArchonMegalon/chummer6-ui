using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    private static CharacterCreationCompletionPlanRequest AddMissingAttributeKarmaAdvice(
        CharacterCreationCompletionReviewInputs inputs,
        CharacterCreationCompletionPlanRequest request,
        string archetypeId)
    {
        var pack = GetRecommendationPack(archetypeId);
        if (pack is null) return request;
        // These are authored role goals, not rule maxima. The first three
        // foundations may benefit from a strong rating; the remaining ones
        // retain a well-rounded baseline. Core alone admits affordable levels.
        // Special attributes are deliberately outside this advice: no inferred
        // magical entitlement, Power Point purchase or changed talent choice.
        var goals = pack.Attributes.Select((row, index) => new CharacterCreationAttributeCompletionTarget(
                row.Target.AttributeId, Math.Max(row.Target.TargetUnaugmentedRating, index < 3 ? 5 : 4)))
            .Where(target => request.Attributes.PermittedAttributeOrder.Contains(target.AttributeId, StringComparer.Ordinal)
                && inputs.Skills.Attributes.Attributes.Any(attribute => attribute.AttributeId == target.AttributeId
                    && attribute.IsEnabled && attribute.Category == CharacterCreationAttributeCategories.Normal))
            .ToArray();
        if (goals.Length == 0) return request;
        return request with
        {
            AllocateRemainingAttributeKarma = true,
            Attributes = request.Attributes with
            {
                KarmaTargets = goals,
                // Finish-missing authorizes the unreserved wallet, not a second
                // allowance based on stale character Karma. Core computes that
                // wallet after all retained and proposed expenses and reserves.
                MaximumAdditionalKarmaSpend = int.MaxValue
            }
        };
    }
}
