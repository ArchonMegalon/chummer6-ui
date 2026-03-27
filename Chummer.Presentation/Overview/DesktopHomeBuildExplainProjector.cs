using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeBuildExplainProjection(string Summary);

public static class DesktopHomeBuildExplainProjector
{
    public static DesktopHomeBuildExplainProjection Create(
        IReadOnlyList<WorkspaceListItem> workspaces,
        CharacterBuildSection? build,
        CharacterRulesSection? rules)
    {
        if (workspaces.Count == 0)
        {
            return new DesktopHomeBuildExplainProjection(
                "No workspace is pinned yet. Start with one dossier or import so Build Lab can compare grounded variants before the first living-dossier handoff.\nRules explanations and support closure stay safer once this install is claimed and the first workspace gives the desktop shell a real continuity target.");
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        string displayName = string.IsNullOrWhiteSpace(leadWorkspace.Summary.Name)
            ? leadWorkspace.Id.Value
            : leadWorkspace.Summary.Name;

        if (build is null || rules is null)
        {
            return new DesktopHomeBuildExplainProjection(
                $"Next safe action: continue {displayName} on {leadWorkspace.RulesetId} and inspect explain traces before you export, publish, or reopen campaign work.\nBuild Lab keeps variant tradeoffs, progression rails, and overlap risks visible before the next campaign-facing handoff.\nRules explanations stay tied to the claimed install, current channel, and support path instead of drifting into detached notes.");
        }

        string buildLane = string.IsNullOrWhiteSpace(build.BuildMethod) ? leadWorkspace.Summary.BuildMethod : build.BuildMethod;
        string priorityLadder = BuildPriorityLadder(build);
        string gameplayMode = string.IsNullOrWhiteSpace(rules.GameplayOption) ? "default gameplay posture" : rules.GameplayOption;
        string bannedWare = BuildBannedWareSummary(rules.BannedWareGrades);

        return new DesktopHomeBuildExplainProjection(
            $"Next safe action: continue {displayName} and inspect the grounded {buildLane} lane before you export, publish, or reopen campaign work.\nBuild posture: {buildLane} with {priorityLadder}; contact points {build.ContactPointsUsed}/{build.ContactPoints}; special track {build.TotalSpecial}.\nRules posture: {rules.GameEdition} · {rules.Settings} · {gameplayMode}; limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen; banned ware {bannedWare}.");
    }

    private static string BuildPriorityLadder(CharacterBuildSection build)
    {
        string[] values =
        [
            NormalizePriority("Metatype", build.PriorityMetatype),
            NormalizePriority("Attributes", build.PriorityAttributes),
            NormalizePriority("Skills", build.PrioritySkills),
            NormalizePriority("Resources", build.PriorityResources),
            NormalizePriority("Talent", build.PriorityTalent)
        ];

        string[] present = values.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return present.Length == 0 ? "an explicit priority ladder" : string.Join(", ", present);
    }

    private static string NormalizePriority(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (trimmed is "-" or "None" or "none")
        {
            return string.Empty;
        }

        return $"{label} {trimmed}";
    }

    private static string BuildBannedWareSummary(IReadOnlyList<string> bannedWareGrades)
    {
        string[] entries = bannedWareGrades
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return entries.Length == 0 ? "none" : string.Join(", ", entries);
    }
}
