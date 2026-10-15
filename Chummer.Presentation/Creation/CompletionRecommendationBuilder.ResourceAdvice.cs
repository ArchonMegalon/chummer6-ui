using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Creation;

public static partial class CompletionRecommendationBuilder
{
    private const string DefaultNativeLanguage = "907a3a8f-2c40-4543-a5a9-891bcde41f6b";

    // Authored preferences, not ratings/point-price authority. An exact source
    // identity must exist in this observation; disabled entries stay Core's call.
    private static readonly CompletionSkillRecommendation[] ResourceKnowledgeAdvice =
    [
        Knowledge("0af06344-3637-4160-813b-a33dda2d16aa", "completion.reason.sprawl-life"),
        Knowledge("fe3ccfd5-9e14-4e0a-962d-f9601fc24950", "completion.reason.shadow-community"),
        Knowledge("8a82edaa-9ff0-447d-acb7-3a72a8bcfb9f", "completion.reason.security-procedures"),
        Knowledge("5c3ebb6f-d075-4486-8900-d7a19bae958f", "completion.reason.underworld"),
        Knowledge("d6304862-cfa6-4916-aee1-40789798b590", "completion.reason.matrix-literacy"),
        Knowledge("227bcaa5-0cc0-4f85-99e6-345a5d7e696d", "completion.reason.legal-awareness")
    ];

    private static readonly CompletionSkillRecommendation[] ResourceActiveAdvice =
    [
        Active("adf31a50-b228-4e09-a09c-46ab9f5e59a1", "completion.reason.personal-defense"),
        Active("1c14bf0d-cc69-4126-9a95-1f2429c11aa5", "completion.reason.matrix-literacy"),
        Active("4fcd40cb-4b02-4b7e-afcb-f44d46cd5706", "completion.reason.close-defense"),
        Active("ae91a8a6-80e7-4f52-b9eb-21725a5528a4", "completion.reason.ground-transport"),
        Active("729c9cee-ef8f-492d-aa7f-17ec1bc3816e", "completion.reason.negotiation"),
        Active("41e184e0-7273-403a-9300-fa29a1707bf0", "completion.reason.device-maintenance"),
        Active("b693f3bf-48dc-4570-9743-d94d14ee698b", "completion.reason.software-literacy")
    ];

    private static CompletionRecommendationPack ExpandResourceAdvice(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CompletionRecommendationPack foundation, HashSet<CompletionChoiceIdentity> locked,
        CancellationToken ct)
    {
        // Read Core's evaluated group eligibility and actual pool. Do not infer
        // available group points or movement eligibility from ranks/metatype.
        var baseline = PreviewGroups([]);
        var groups = new List<CompletionSkillRecommendation>();
        var members = new HashSet<string>(StringComparer.Ordinal);
        string roleAnchor = foundation.ArchetypeId switch
        {
            CompletionAssistantRules.StreetSamurai => "788b387b-ee41-4e6a-bf22-481a8cc4cf9f", // Firearms
            CompletionAssistantRules.Face => "729c9cee-ef8f-492d-aa7f-17ec1bc3816e", // Influence
            CompletionAssistantRules.Decker => "c2bb65f5-4a6b-49bf-9925-ef6434cb6929", // Cracking
            CompletionAssistantRules.Rigger => "5e5f2f7f-f63b-4f65-a65d-91b3d4523c6f", // Engineering
            CompletionAssistantRules.Mage => "c4367a39-4065-4b1d-aa62-e9dce377e452", // Sorcery
            _ => "a9fa961d-07e5-46da-8edc-403ae3e6cc75" // Athletics
        };
        foreach (string anchor in new[] { roleAnchor,
            "b20acd11-f102-40f3-a641-e3c420fbdb91", "89ee1730-053a-400f-a13a-4fbadae015f0" })
        {
            ct.ThrowIfCancellationRequested();
            if (baseline is null || baseline.SkillGroupPointBudget.Remaining <= intent.Reserves.SkillGroupPoints) break;
            var matches = inputs.Skills.Authority.SkillGroups.Where(row =>
                row.MemberSkillSourceIds.Contains(anchor, StringComparer.Ordinal)).ToArray();
            if (matches.Length != 1) continue;
            var group = matches[0];
            if (groups.Any(row => row.Target.SourceId == group.GroupId)
                || locked.Contains(new(CompletionChoiceDomain.SkillGroup, group.GroupId))
                || group.MemberSkillSourceIds.Any(id => locked.Contains(new(CompletionChoiceDomain.ActiveSkill, id)))) continue;
            var candidate = PreviewGroups(groups.Select(row => row.Target)
                .Append(new(CharacterCreationSkillCompletionKinds.Group, group.GroupId, 1)).ToArray());
            if (candidate is null
                || candidate.SkillGroups.SingleOrDefault(row => row.GroupId == group.GroupId)
                    is not { IsEnabled: true, Blockers.Count: 0 }
                || candidate.SkillGroupPointBudget.Remaining < intent.Reserves.SkillGroupPoints) continue;
            // This probe uses the original attributes only to admit a group.
            // Its old INT/LOG budget cannot veto unrelated future reserves;
            // the full Core planner checks every pool using proposed attributes.
            baseline = candidate;
            groups.Add(new(new(CharacterCreationSkillCompletionKinds.Group, group.GroupId, 1),
                "completion.reason.broad-skill-foundation"));
            members.UnionWith(group.MemberSkillSourceIds);
        }

        // A group floor and a separate higher member floor would deliberately
        // break that new group. The group is the advice for all of its members.
        var skills = groups.Concat(foundation.Skills.Concat(ResourceActiveAdvice)
            .Where(row => row.Target.Kind != CharacterCreationSkillKinds.Active || !members.Contains(row.Target.SourceId)))
            .Concat(ResourceKnowledgeAdvice)
            .DistinctBy(row => SkillIdentity(row.Target)).ToArray();
        var attributes = foundation.Attributes.Concat(new[]
        {
            new CompletionAttributeRecommendation(new("EDG", 3), "completion.reason.edge-reserve")
        }).ToList();
        // Already selected awakened/resonant access is Core authority, not a new
        // talent choice. Zero is a floor only; the fill pass uses the same kernel.
        foreach (string id in new[] { "MAG", "RES" })
            if (inputs.Skills.Attributes.Attributes.Any(row => row.AttributeId == id && row.IsEnabled))
                attributes.Add(new(new(id, 0), "completion.reason.selected-talent-capacity"));
        return foundation with
        {
            Id = foundation.Id + ".resources", Version = "2",
            Attributes = attributes.ToArray(), Skills = skills
        };

        CharacterCreationAttributeSkillsPreview? PreviewGroups(
            IReadOnlyList<CharacterCreationSkillCompletionRatingTarget> targets)
        {
            // Use the public package boundary. The planner invokes the genuine
            // Core preview kernel and retains all original skills/groups; this
            // request permits only these cumulative group goals and their members.
            var permissions = targets.SelectMany(target => inputs.Skills.Authority.SkillGroups
                    .Single(group => group.GroupId == target.SourceId).MemberSkillSourceIds
                    .Select(id => new CharacterCreationSkillCompletionTarget(CharacterCreationSkillKinds.Active, id))
                    .Prepend(new(CharacterCreationSkillCompletionKinds.Group, target.SourceId)))
                .Distinct().ToArray();
            var request = new CharacterCreationSkillCompletionRequest(inputs.Skills.Binding,
                inputs.Skills.Attributes.SnapshotDigest, inputs.Skills.SnapshotDigest, permissions,
                GroupPointsToReserve: intent.Reserves.SkillGroupPoints) { Targets = targets };
            return CharacterCreationSkillCompletionPlanner.Plan(inputs.Skills,
                inputs.Skills.Attributes.PendingDraft?.Allocations ?? [], request, ct).Value?.Preview;
        }
    }

    private static CompletionSkillRecommendation Knowledge(string id, string reason)
        => new(new(CharacterCreationSkillKinds.Knowledge, id, 1), reason);

    private static CompletionSkillRecommendation Active(string id, string reason)
        => new(new(CharacterCreationSkillKinds.Active, id, 1), reason);
}
