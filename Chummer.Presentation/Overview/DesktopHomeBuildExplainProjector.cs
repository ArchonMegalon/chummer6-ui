using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeBuildExplainProjection(
    string Summary,
    string NextSafeAction,
    string ExplainFocus,
    string ReturnTarget,
    string RulePosture,
    IReadOnlyList<string> Watchouts);

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
                "No workspace is pinned yet. Start with one dossier or import so Build Lab can compare grounded variants before the first living-dossier handoff.",
                "Create or import the first dossier before you trust this install to carry campaign continuity.",
                "Claim the install and seed one real workspace so grounded build receipts, rule answers, and support closure all share the same continuity target.",
                "No workspace return target is pinned yet.",
                "Rule posture is still generic until the first workspace restores a grounded runtime fingerprint.",
                [
                    "No grounded build lane is loaded yet for this desktop head.",
                    "Rules explanations stay generic until the first workspace is restored into local continuity."
                ]);
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        string displayName = string.IsNullOrWhiteSpace(leadWorkspace.Summary.Name)
            ? leadWorkspace.Id.Value
            : leadWorkspace.Summary.Name;

        if (build is null || rules is null)
        {
            return new DesktopHomeBuildExplainProjection(
                $"Continue {displayName} on {leadWorkspace.RulesetId} and inspect explain traces before you export, publish, or reopen campaign work.",
                $"Reopen {displayName} and refresh the build and rules sections so the next action is grounded in live dossier state instead of cached workspace summary only.",
                "Build Lab keeps variant tradeoffs, progression rails, and overlap risks visible before the next campaign-facing handoff, while Rules explanations stay tied to the claimed install, current channel, and support path.",
                $"Return target: {displayName} on runtime {leadWorkspace.RulesetId}.",
                $"Rule posture: runtime fingerprint {leadWorkspace.RulesetId} is pinned, but the live rules section still needs a refresh before you trust drift-sensitive decisions.",
                [
                    "Build Lab is falling back to workspace summary until the build and rules sections can be read again.",
                    "Support answers are safer after the dossier reloads the current build lane and rules posture."
                ]);
        }

        string buildLane = string.IsNullOrWhiteSpace(build.BuildMethod) ? leadWorkspace.Summary.BuildMethod : build.BuildMethod;
        string priorityLadder = BuildPriorityLadder(build);
        string gameplayMode = string.IsNullOrWhiteSpace(rules.GameplayOption) ? "default gameplay posture" : rules.GameplayOption;
        string bannedWare = BuildBannedWareSummary(rules.BannedWareGrades);
        int remainingContactPoints = Math.Max(build.ContactPoints - build.ContactPointsUsed, 0);
        string nextSafeAction = remainingContactPoints == 0
            ? $"Continue {displayName}, but review contact allocation before you export or hand the dossier back into campaign play."
            : $"Continue {displayName} and inspect the grounded {buildLane} lane before you export, publish, or reopen campaign work.";
        string explainFocus = $"Explain focus: {buildLane} with {priorityLadder}; {gameplayMode}; current limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen.";
        string returnTarget = $"Return target: {displayName} on runtime {leadWorkspace.RulesetId}.";
        string rulePosture = $"Rule posture: {rules.GameEdition} · {rules.Settings} · {gameplayMode} · fingerprint {leadWorkspace.RulesetId}.";

        List<string> watchouts =
        [
            remainingContactPoints == 0
                ? "Contact points are fully allocated, so any new social or team-facing change now forces a tradeoff."
                : $"Contact allocation leaves {remainingContactPoints} point(s) available before the next handoff.",
            string.Equals(bannedWare, "none", StringComparison.Ordinal)
                ? "No banned ware grades are currently blocking the next safe build/export decision."
                : $"Current rules posture bans {bannedWare}, so gear and upgrade choices need an explicit compatibility check."
        ];

        if (rules.MaxKarma > 0)
        {
            watchouts.Add($"Campaign rules cap this lane at {rules.MaxKarma} Karma before the next progression checkpoint changes.");
        }

        return new DesktopHomeBuildExplainProjection(
            $"Build posture: {buildLane} with {priorityLadder}; contact points {build.ContactPointsUsed}/{build.ContactPoints}; special track {build.TotalSpecial}.\nRules posture: {rules.GameEdition} · {rules.Settings} · {gameplayMode}; limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen; banned ware {bannedWare}.",
            nextSafeAction,
            explainFocus,
            returnTarget,
            rulePosture,
            watchouts);
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
