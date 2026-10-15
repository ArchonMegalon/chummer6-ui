using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    // Authored preferences only. The current Core catalog must still admit the
    // exact source, configuration, price and complete cumulative basket.
    private static readonly Guid[] ResourceLifestyleSources =
    [
        Guid.Parse("451eef87-d18e-4bee-a972-1ee165b08522"), // Low
        Guid.Parse("9367556e-f82f-4f9d-840e-24c8f824ca68")  // Squatter
    ];

    private sealed record ResourceCompletion(CharacterCreationCompletionPlanRequest Request,
        CharacterCreationFoundationResult<CharacterCreationCompletionPlan>? Evaluation = null);

    private sealed record ResourceContactAdvice(string Key, string Role, int Connection, int Loyalty,
        params string[] Aliases);

    private static ResourceCompletion CompleteResources(
        CharacterCreationCompletionReviewInputs inputs,
        CharacterCreationCompletionPlanRequest request,
        string archetypeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Do not repair, replace or silently discard a caller's existing choice.
        if (inputs.Contacts.Contacts.Any(original => !request.Contacts.Contains(original))) return new(request);
        var attributes = CharacterCreationAttributeCompletionPlanner.Plan(
            inputs.Skills.Attributes, request.Attributes, cancellationToken);
        if (attributes.Value is null || attributes.Outcome is
            CharacterCreationFoundationOutcomes.Invalid or CharacterCreationFoundationOutcomes.Conflict)
            return new(request);

        var contacts = request.Contacts.ToList();
        // These are bounded, authored relationship goals, not rule caps or costs.
        // Favor a useful primary relationship before broadening the network.
        var fixer = new ResourceContactAdvice("fixer", "Fixer", 4, 3);
        var medic = new ResourceContactAdvice("street-medic", "Street Medic", 2, 4,
            "Street Doc", "Street Doctor", "Medic", "Doctor");
        ResourceContactAdvice[] advice = archetypeId switch
        {
            CompletionAssistantRules.Face =>
                [new("corporate-liaison", "Corporate Liaison", 5, 2), fixer, medic,
                    new("bartender", "Bartender", 2, 4)],
            CompletionAssistantRules.StreetSamurai =>
                [new("arms-dealer", "Arms Dealer", 4, 2, "Weapons Dealer"), medic, fixer,
                    new("gang-lookout", "Gang Lookout", 2, 3)],
            CompletionAssistantRules.Mage =>
                [new("talismonger", "Talismonger", 4, 2), fixer, medic,
                    new("occult-researcher", "Occult Researcher", 2, 4)],
            CompletionAssistantRules.Decker =>
                [new("matrix-broker", "Matrix Broker", 4, 2), fixer, medic,
                    new("electronics-technician", "Electronics Technician", 2, 4)],
            CompletionAssistantRules.Rigger =>
                [new("mechanic", "Mechanic", 2, 4, "Drone Mechanic"), fixer, medic,
                    new("smuggler", "Smuggler", 4, 2)],
            _ => [fixer, medic, new("local-informant", "Local Informant", 3, 2),
                new("smuggler", "Smuggler", 4, 2)]
        };
        foreach (var goal in advice)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (contacts.Any(contact => goal.Aliases.Prepend(goal.Role).Any(alias =>
                    string.Equals(contact.Identity.Role?.Trim(), alias, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(contact.Identity.Name?.Trim(), alias, StringComparison.OrdinalIgnoreCase))))
                continue;
            Guid id = ResourceCandidateId(inputs.SnapshotDigest, "contact", goal.Key);
            if (contacts.Any(contact => contact.ContactId == id)) continue;
            var candidate = new CharacterCreationKarmaContactSelection(id,
                new CharacterCreationContactIdentity(goal.Role, goal.Role, "", "", "", "", "", "", "Professional", "", "", "", ""),
                Connection: 1, Loyalty: 1);
            if (!IsFreeContactProposal([.. contacts, candidate])) continue;
            int addedIndex = contacts.Count;
            contacts.Add(candidate);

            // Only this invocation's newly added row can improve. Retained rows,
            // including earlier assistant suggestions accepted by the user, stay exact.
            foreach (bool connectionFirst in goal.Connection >= goal.Loyalty ? new[] { true, false } : new[] { false, true })
            {
                int target = connectionFirst ? goal.Connection : goal.Loyalty;
                for (int rating = 2; rating <= target; rating++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var improved = connectionFirst ? candidate with { Connection = rating } : candidate with { Loyalty = rating };
                    var proposed = contacts.ToArray();
                    proposed[addedIndex] = improved;
                    if (!IsFreeContactProposal(proposed)) break;
                    contacts[addedIndex] = candidate = improved;
                }
            }
        }

        bool IsFreeContactProposal(IReadOnlyList<CharacterCreationKarmaContactSelection> proposed)
        {
            var projected = CharacterCreationContactsService.PreviewCompletion(inputs.Contacts,
                attributes.Value.Allocations, request.QualityOptionIds, proposed, cancellationToken);
            // Core prices the proposed CHA/quality graph, including High Places.
            // Zero added Karma leaves the whole request's other domains/reserves intact.
            return projected is { Outcome: CharacterCreationContactOutcomes.Available,
                    Value: { Blockers.Count: 0, AdditionalKarmaCost: 0,
                        ContactBudget: { IsExact: true, Overspend: 0 },
                        HighPlacesBudget: { IsExact: true, Overspend: 0 } } };
        }
        request = request with { Contacts = contacts.ToArray() };
        if (request.MaximumAdditionalPurchaseSpend <= 0m) return new(request);

        // Reuse Core's complete contact/investment/Karma reserve calculation to
        // obtain the actual proposed magic choices before pricing any purchase.
        var upstream = CharacterCreationCompletionPlanner.Plan(inputs, request, cancellationToken);
        var unchanged = new ResourceCompletion(request, upstream);
        if (upstream.Value is not { Magic: { } magic, Purchases: { } originalPurchases }
            || upstream.Outcome is CharacterCreationFoundationOutcomes.Invalid or CharacterCreationFoundationOutcomes.Conflict
            || originalPurchases.Targets.Any(target => target.Outcome is not
                (CharacterCreationPurchaseCompletionTargetOutcomes.Added
                 or CharacterCreationPurchaseCompletionTargetOutcomes.AlreadyPresent)))
            return unchanged;

        var accepted = request.Purchases.ToList();
        Guid? startingLifestyleId = request.StartingLifestyleId;
        var purchaseRequest = new CharacterCreationPurchaseCompletionRequest(
            inputs.Lifestyles.SnapshotDigest, inputs.Magic.SnapshotDigest,
            attributes.Value.Allocations, magic.Selections, request.QualityOptionIds,
            request.TotalKarmaInvestment, request.Purchases, startingLifestyleId,
            request.NuyenToReserve, request.MaximumAdditionalPurchaseSpend, request.MaximumPurchaseEvaluations)
        {
            LoadoutSnapshotDigest = inputs.Loadout?.SnapshotDigest
        };
        // Retain only a result produced here for this exact private input and
        // full request. Failed optional candidates never replace this value.
        CharacterCreationPurchaseCompletionRequest? acceptedRequest = null;
        CharacterCreationFoundationResult<CharacterCreationPurchaseCompletionPlan>? acceptedResult = null;

        // Reserve the useful equipment choices before optional home/utility gear.
        // Every probe still prices the entire accepted basket in Core.
        var recommendedWeapon = AddMissingLoadoutAdvice(inputs.Loadout, archetypeId, accepted,
            candidate => TryAddPurchase(candidate, startingLifestyleId), cancellationToken);
        AddMissingEquipmentAdvice(inputs.Lifestyles.Gear, recommendedWeapon, archetypeId, accepted,
            candidates => TryAddPurchases(candidates, startingLifestyleId), cancellationToken);

        if (inputs.Lifestyles.Lifestyles.Count == 0 && startingLifestyleId is null
            && accepted.All(target => target.Lifestyle is null) && inputs.Lifestyles.Authority.IsAuthoritative)
        {
            foreach (Guid sourceId in ResourceLifestyleSources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var options = inputs.Lifestyles.Authority.LifestyleOptions.Where(option =>
                    option.SourceId == sourceId && option.IsSelectable && option.EligibilityIsExact
                    && option.Blockers.Count == 0).Take(2).ToArray();
                if (options.Length != 1) continue;
                var option = options[0];
                Guid id = ResourceCandidateId(inputs.SnapshotDigest, "lifestyle", sourceId.ToString("D"));
                var home = new CharacterCreationLifestyleConfiguration(id, option.OptionId, option.Name,
                    CharacterCreationLifestyleStyleIds.Standard, option.DefaultIncrementId, 1, 100m, 0,
                    false, false, 0, 0, 0, 0, "", "", "", []);
                if (TryAddPurchase(new(Lifestyle: home), id)) break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // No accepted addition means the complete, genuine upstream plan is
        // already the final answer for this exact request. Never reclassify a
        // partial purchase result as whole-character readiness.
        if (acceptedResult is null) return unchanged;
        var finalRequest = purchaseRequest with { Targets = accepted.ToArray(), StartingLifestyleId = startingLifestyleId };
        // The last accepted candidate already evaluated this cumulative basket.
        // Compare the FULL request, including cash choice, caps and reserves;
        // reuse is local to this call and cannot cross source/owner observations.
        var final = CharacterCreationFinalizationDigest.Compute(acceptedRequest)
            == CharacterCreationFinalizationDigest.Compute(finalRequest)
            ? acceptedResult
            : CharacterCreationPurchaseCompletionPlanner.Plan(inputs.Lifestyles, inputs.Magic,
                inputs.Loadout, finalRequest, cancellationToken);
        return AcceptedPurchases(final)
            ? new(request with { Purchases = accepted.ToArray(), StartingLifestyleId = startingLifestyleId })
            : unchanged;

        bool TryAddPurchase(CharacterCreationPurchaseCompletionTarget candidate, Guid? startingId)
            => TryAddPurchases([candidate], startingId);

        bool TryAddPurchases(IReadOnlyList<CharacterCreationPurchaseCompletionTarget> candidates, Guid? startingId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.Count == 0
                || accepted.Count + candidates.Count > CharacterCreationPurchaseCompletionPlanner.MaximumTargets) return false;
            var candidateRequest = purchaseRequest with { Targets = [.. accepted, .. candidates], StartingLifestyleId = startingId };
            var result = CharacterCreationPurchaseCompletionPlanner.Plan(inputs.Lifestyles, inputs.Magic,
                inputs.Loadout, candidateRequest, cancellationToken);
            if (!AcceptedPurchases(result)
                || result.Value!.Targets.Count != candidateRequest.Targets.Count
                || result.Value.Targets.Skip(accepted.Count).Any(target =>
                    target.Outcome != CharacterCreationPurchaseCompletionTargetOutcomes.Added))
                return false;
            accepted.AddRange(candidates);
            startingLifestyleId = startingId;
            acceptedRequest = candidateRequest;
            acceptedResult = result;
            return true;
        }
    }

    // A missing starting lifestyle is not a failed gear purchase. Keep accepted
    // additions while leaving that separate requirement visible to whole review.
    private static bool AcceptedPurchases(CharacterCreationFoundationResult<CharacterCreationPurchaseCompletionPlan> result)
        => result.Outcome is CharacterCreationFoundationOutcomes.Success or CharacterCreationFoundationOutcomes.Blocked
            && result.Value is { } plan
            && plan.Blockers.All(blocker => blocker == CharacterCreationLifestylesCompletionSchemas.StartingLifestyleRequired)
            && plan.Targets.All(target => target.Outcome is CharacterCreationPurchaseCompletionTargetOutcomes.Added
                or CharacterCreationPurchaseCompletionTargetOutcomes.AlreadyPresent);

    private static Guid ResourceCandidateId(string snapshotDigest, string kind, string key)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(
            "chummer.completion.resources.v1\0" + snapshotDigest + "\0" + kind + "\0" + key)).AsSpan(0, 16));

    private static bool HasResourceInputShape(CharacterCreationCompletionReviewInputs inputs)
        => inputs.Lifestyles is { Lifestyles: not null,
                Authority: { LifestyleOptions: not null }, Gear.State.Authority.Options: not null }
            && inputs.Lifestyles.Lifestyles.All(row => row is not null)
            && inputs.Lifestyles.Authority.LifestyleOptions.All(row => row is not null && row.Blockers is not null)
            && inputs.Lifestyles.Gear.State.Authority.Options.All(row => row is not null && row.Blockers is not null)
            && (inputs.Lifestyles.Gear.State.PendingDraft is not { } gear
                || gear.Lines is not null && gear.Lines.All(row => row is not null))
            && inputs.Contacts.Contacts.All(row => row is not null && row.Identity is not null);
}
