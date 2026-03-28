using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeBuildExplainProjection(
    string Summary,
    string NextSafeAction,
    string ExplainFocus,
    string RuntimeHealthSummary,
    string ReturnTarget,
    string RulePosture,
    IReadOnlyList<string> CompatibilityReceipts,
    IReadOnlyList<string> Watchouts);

public static class DesktopHomeBuildExplainProjector
{
    public static DesktopHomeBuildExplainProjection Create(
        IReadOnlyList<WorkspaceListItem> workspaces,
        CharacterBuildSection? build,
        CharacterRulesSection? rules,
        ActiveRuntimeStatusProjection? activeRuntime = null,
        RuntimeInspectorProjection? runtimeInspector = null)
    {
        string runtimeHealthSummary = BuildRuntimeHealthSummary(activeRuntime, runtimeInspector);

        if (workspaces.Count == 0)
        {
            return new DesktopHomeBuildExplainProjection(
                "No workspace is pinned yet. Start with one dossier or import so Build Lab can compare grounded variants before the first living-dossier handoff.",
                "Create or import the first dossier before you trust this install to carry campaign continuity.",
                "Claim the install and seed one real workspace so grounded build receipts, rule answers, and support closure all share the same continuity target.",
                runtimeHealthSummary,
                "No workspace return target is pinned yet.",
                "Rule posture is still generic until the first workspace restores a grounded runtime fingerprint.",
                [
                    "Compatibility receipt: no grounded runtime fingerprint is attached yet, so campaign-safe build and explain proof still needs the first claimed workspace."
                ],
                [
                    "No grounded build lane is loaded yet for this desktop head.",
                    "Rules explanations stay generic until the first workspace is restored into local continuity."
                ]);
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        string displayName = string.IsNullOrWhiteSpace(leadWorkspace.Summary.Name)
            ? leadWorkspace.Id.Value
            : leadWorkspace.Summary.Name;
        string runtimeFingerprint = string.IsNullOrWhiteSpace(activeRuntime?.RuntimeFingerprint)
            ? leadWorkspace.RulesetId
            : activeRuntime!.RuntimeFingerprint;

        if (build is null || rules is null)
        {
            string[] fallbackWatchouts = new[]
            {
                "Build Lab is falling back to workspace summary until the build and rules sections can be read again.",
                "Support answers are safer after the dossier reloads the current build lane and rules posture."
            }
            .Concat(BuildRuntimeWatchouts(runtimeInspector))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

            return new DesktopHomeBuildExplainProjection(
                $"Continue {displayName} on {leadWorkspace.RulesetId} and inspect explain traces before you export, publish, or reopen campaign work.",
                ResolveRefreshAction(displayName, runtimeInspector)
                    ?? $"Reopen {displayName} and refresh the build and rules sections so the next action is grounded in live dossier state instead of cached workspace summary only.",
                "Build Lab keeps variant tradeoffs, progression rails, and overlap risks visible before the next campaign-facing handoff, while Rules explanations stay tied to the claimed install, current channel, and support path.",
                runtimeHealthSummary,
                $"Return target: {displayName} on runtime {runtimeFingerprint}.",
                $"Rule posture: runtime fingerprint {runtimeFingerprint} is pinned, but the live rules section still needs a refresh before you trust drift-sensitive decisions.",
                BuildCompatibilityReceipts(runtimeInspector, runtimeFingerprint),
                fallbackWatchouts);
        }

        string buildLane = string.IsNullOrWhiteSpace(build.BuildMethod) ? leadWorkspace.Summary.BuildMethod : build.BuildMethod;
        string priorityLadder = BuildPriorityLadder(build);
        string gameplayMode = string.IsNullOrWhiteSpace(rules.GameplayOption) ? "default gameplay posture" : rules.GameplayOption;
        string bannedWare = BuildBannedWareSummary(rules.BannedWareGrades);
        int remainingContactPoints = Math.Max(build.ContactPoints - build.ContactPointsUsed, 0);
        string nextSafeAction = ResolveRefreshAction(displayName, runtimeInspector) ?? (remainingContactPoints == 0
            ? $"Continue {displayName}, but review contact allocation before you export or hand the dossier back into campaign play."
            : $"Continue {displayName} and inspect the grounded {buildLane} lane before you export, publish, or reopen campaign work.");
        string explainFocus = $"Explain focus: {buildLane} with {priorityLadder}; {gameplayMode}; current limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen.";
        string returnTarget = $"Return target: {displayName} on runtime {runtimeFingerprint}.";
        string installState = string.IsNullOrWhiteSpace(activeRuntime?.InstallState)
            ? "workspace-only"
            : activeRuntime.InstallState;
        string rulePosture = $"Rule posture: {rules.GameEdition} · {rules.Settings} · {gameplayMode} · fingerprint {runtimeFingerprint} · install {installState}.";

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

        watchouts.AddRange(BuildRuntimeWatchouts(runtimeInspector));

        return new DesktopHomeBuildExplainProjection(
            $"Build posture: {buildLane} with {priorityLadder}; contact points {build.ContactPointsUsed}/{build.ContactPoints}; special track {build.TotalSpecial}.\nRules posture: {rules.GameEdition} · {rules.Settings} · {gameplayMode}; limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen; banned ware {bannedWare}.",
            nextSafeAction,
            explainFocus,
            runtimeHealthSummary,
            returnTarget,
            rulePosture,
            BuildCompatibilityReceipts(runtimeInspector, runtimeFingerprint),
            watchouts
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
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

    private static string BuildRuntimeHealthSummary(
        ActiveRuntimeStatusProjection? activeRuntime,
        RuntimeInspectorProjection? runtimeInspector)
    {
        if (activeRuntime is null)
        {
            return "Runtime health: no active runtime profile is loaded for this desktop lane yet.";
        }

        string installState = string.IsNullOrWhiteSpace(activeRuntime.InstallState)
            ? "available"
            : activeRuntime.InstallState;
        string warningSummary = activeRuntime.WarningCount == 0
            ? "no active runtime warnings"
            : $"{activeRuntime.WarningCount} runtime warning(s) require review";
        string compatibilitySummary = runtimeInspector is null
            ? "runtime inspector details are not loaded yet"
            : DescribeCompatibility(runtimeInspector.CompatibilityDiagnostics);

        return $"Runtime health: {activeRuntime.Title} · {installState} · fingerprint {activeRuntime.RuntimeFingerprint}; {warningSummary}; {compatibilitySummary}.";
    }

    private static string DescribeCompatibility(IReadOnlyList<RuntimeLockCompatibilityDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return "compatibility looks clear";
        }

        if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.RebindRequired, StringComparison.Ordinal)))
        {
            return "runtime drift requires a rebind before the next campaign-safe handoff";
        }

        if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.MissingPack, StringComparison.Ordinal)))
        {
            return "one or more required rule packs are missing";
        }

        if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.EngineApiMismatch, StringComparison.Ordinal)))
        {
            return "engine API mismatch blocks a safe handoff";
        }

        return "compatibility diagnostics need review";
    }

    private static string? ResolveRefreshAction(string displayName, RuntimeInspectorProjection? runtimeInspector)
    {
        if (runtimeInspector is null)
        {
            return null;
        }

        if (runtimeInspector.CompatibilityDiagnostics.Any(static diagnostic => string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.RebindRequired, StringComparison.Ordinal)))
        {
            return $"Inspect runtime drift for {displayName} and rebind the active profile before you export, publish, or rejoin campaign continuity.";
        }

        if (runtimeInspector.CompatibilityDiagnostics.Any(static diagnostic => string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.MissingPack, StringComparison.Ordinal)))
        {
            return $"Resolve missing rule-pack content for {displayName} before you trust build comparisons, rules answers, or campaign return targets.";
        }

        return null;
    }

    private static IEnumerable<string> BuildRuntimeWatchouts(RuntimeInspectorProjection? runtimeInspector)
    {
        if (runtimeInspector is null)
        {
            yield break;
        }

        foreach (RuntimeLockCompatibilityDiagnostic diagnostic in runtimeInspector.CompatibilityDiagnostics)
        {
            if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.RebindRequired, StringComparison.Ordinal))
            {
                yield return "Runtime drift was detected, so the current profile needs a rebind before the next safe export or campaign return.";
            }
            else if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.MissingPack, StringComparison.Ordinal))
            {
                yield return "A required rule pack is missing, so grounded rules answers and dossier handoffs are not trustworthy yet.";
            }
            else if (!string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.Ordinal))
            {
                yield return $"Runtime compatibility needs review: {diagnostic.Message}.";
            }
        }

        foreach (RuntimeInspectorWarning warning in runtimeInspector.Warnings)
        {
            if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.Migration, StringComparison.Ordinal))
            {
                yield return "Migration guidance is active for the current runtime, so treat the next handoff as review-required.";
            }
            else if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.ProviderBinding, StringComparison.Ordinal))
            {
                yield return "Provider bindings changed recently, so explain answers should be reviewed before you trust them in support or publication.";
            }
        }
    }

    private static IReadOnlyList<string> BuildCompatibilityReceipts(RuntimeInspectorProjection? runtimeInspector, string runtimeFingerprint)
    {
        if (runtimeInspector is null)
        {
            return
            [
                $"Compatibility receipt: runtime inspector details are still loading for fingerprint {runtimeFingerprint}, so drift-sensitive decisions should stay review-only."
            ];
        }

        List<string> receipts = [];
        if (runtimeInspector.CompatibilityDiagnostics.Count == 0)
        {
            receipts.Add($"Compatibility receipt: fingerprint {runtimeFingerprint} is aligned with the current workspace and no runtime drift is active.");
        }

        foreach (RuntimeLockCompatibilityDiagnostic diagnostic in runtimeInspector.CompatibilityDiagnostics)
        {
            if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.RebindRequired, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility receipt: runtime drift requires a profile rebind before the next campaign return, export, or publication handoff.");
            }
            else if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.MissingPack, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility receipt: at least one required rule pack is missing, so grounded build and explain answers are incomplete.");
            }
            else if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.EngineApiMismatch, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility receipt: engine API mismatch blocks a safe handoff until the runtime and rules content converge again.");
            }
            else if (!string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.Ordinal))
            {
                receipts.Add($"Compatibility receipt: {diagnostic.Message}");
            }
        }

        foreach (RuntimeInspectorWarning warning in runtimeInspector.Warnings)
        {
            if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.Migration, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility receipt: migration guidance is active, so the next campaign-facing handoff should stay explicitly review-required.");
            }
            else if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.ProviderBinding, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility receipt: provider bindings changed recently, so explain answers should be rechecked before you trust them in support or publication.");
            }
        }

        return receipts
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
