using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Rulesets;
using Chummer.Campaign.Contracts;

namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeBuildExplainProjection(
    string RulesetId,
    string RulesetSpotlight,
    string Summary,
    string NextSafeAction,
    string ExplainFocus,
    string RuntimeHealthSummary,
    string ReturnTarget,
    string RulePosture,
    IReadOnlyList<string> CompatibilityReceipts,
    IReadOnlyList<string> BuildPathComparisons,
    IReadOnlyList<string> Watchouts);

public static class DesktopHomeBuildExplainProjector
{
    public static DesktopHomeBuildExplainProjection Create(
        IReadOnlyList<WorkspaceListItem> workspaces,
        CharacterBuildSection? build,
        CharacterRulesSection? rules,
        AccountCampaignSummary? campaignSummary = null,
        ActiveRuntimeStatusProjection? activeRuntime = null,
        RuntimeInspectorProjection? runtimeInspector = null,
        IReadOnlyList<DesktopBuildPathCandidate>? buildPathCandidates = null)
    {
        string runtimeHealthSummary = BuildRuntimeHealthSummary(activeRuntime, runtimeInspector);
        DesktopBuildPathCandidate? leadBuildPath = buildPathCandidates?.FirstOrDefault();
        string? effectiveRulesetId = ResolveRulesetId(
            workspaces.FirstOrDefault()?.RulesetId,
            activeRuntime?.RulesetId,
            rules?.GameEdition,
            leadBuildPath?.Suggestion.Targets.FirstOrDefault());
        string resolvedRulesetId = effectiveRulesetId ?? string.Empty;
        string rulesetSpotlight = RulesetUiDirectiveCatalog.BuildHomeSpotlight(effectiveRulesetId);
        IReadOnlyList<string> campaignReceipts = BuildCampaignReceipts(campaignSummary);
        IReadOnlyList<string> campaignWatchouts = BuildCampaignWatchouts(campaignSummary);
        string? campaignNextSafeAction = BuildCampaignNextSafeAction(campaignSummary);
        string? campaignExplainFocus = BuildCampaignExplainFocus(campaignSummary);

        if (workspaces.Count == 0)
        {
            List<string> compatibilityReceipts =
            [
                "Compatibility details: no current runtime fingerprint is attached yet, so campaign-safe build explanations still need the first claimed dossier."
            ];
            compatibilityReceipts.AddRange(BuildBuildPathReceipts(leadBuildPath));
            compatibilityReceipts.AddRange(campaignReceipts);

            return Humanize(new DesktopHomeBuildExplainProjection(
                RulesetId: resolvedRulesetId,
                RulesetSpotlight: rulesetSpotlight,
                "No dossier is pinned yet. Start with one dossier or import so Build Lab can compare grounded variants before the first living-dossier handoff.",
                leadBuildPath is null
                    ? campaignNextSafeAction
                        ?? "Create or import the first dossier before you trust this install to carry campaign continuity."
                    : campaignNextSafeAction
                        ?? $"Create or import the first dossier, then review the recommended {leadBuildPath.Suggestion.Title} build path before you trust this install to carry campaign continuity.",
                leadBuildPath is null
                    ? campaignExplainFocus
                        ?? "Claim the install and add one real dossier so build guidance, rules answers, and support all stay tied to the same character."
                    : campaignExplainFocus
                        ?? $"Claim the install, add one real dossier, and review the suggested {leadBuildPath.Suggestion.Title} path before you reopen campaign work.",
                runtimeHealthSummary,
                "No dossier return target is pinned yet.",
                RulesetUiDirectiveCatalog.BuildUngroundedRulePosture(effectiveRulesetId),
                compatibilityReceipts,
                BuildBuildPathComparisons(buildPathCandidates),
                new[]
                {
                    "No build plan is loaded yet for this copy.",
                    "Rules explanations become character-specific after you restore or create the first dossier."
                }
                .Concat(RulesetUiDirectiveCatalog.BuildBuildExplainWatchouts(effectiveRulesetId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()));
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        string displayName = string.IsNullOrWhiteSpace(leadWorkspace.Summary.Name)
            ? leadWorkspace.Id.Value
            : leadWorkspace.Summary.Name;
        string runtimeFingerprint = string.IsNullOrWhiteSpace(activeRuntime?.RuntimeFingerprint)
            ? leadWorkspace.RulesetId
            : activeRuntime!.RuntimeFingerprint;

        IReadOnlyList<string> buildPathReceipts = BuildBuildPathReceipts(leadBuildPath);

        if (build is null || rules is null)
        {
            string[] fallbackWatchouts = new[]
            {
                "Build Lab is falling back to dossier summary until the build and rules sections can be read again.",
                "Support answers are safer after the dossier reloads the current build and rules state."
            }
            .Concat(BuildRuntimeWatchouts(runtimeInspector))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

            return Humanize(new DesktopHomeBuildExplainProjection(
                RulesetId: resolvedRulesetId,
                RulesetSpotlight: rulesetSpotlight,
                $"Continue {displayName} on {leadWorkspace.RulesetId} and inspect explain traces before you export, publish, or reopen campaign work.",
                ResolveRefreshAction(displayName, runtimeInspector)
                    ?? campaignNextSafeAction
                    ?? $"Reopen {displayName} and refresh the build and rules sections so the next action is grounded in live dossier state instead of cached dossier summary only.",
                campaignExplainFocus
                    ?? "Build Lab keeps variant tradeoffs, progression rails, and overlap risks visible before the next campaign-facing handoff, while Rules explanations stay tied to the claimed install, current channel, and support path.",
                runtimeHealthSummary,
                $"Return target: {displayName} on runtime {runtimeFingerprint}.",
                RulesetUiDirectiveCatalog.BuildPinnedRuntimeRulePosture(effectiveRulesetId, runtimeFingerprint),
                BuildCompatibilityReceipts(runtimeInspector, runtimeFingerprint)
                    .Concat(buildPathReceipts)
                    .Concat(campaignReceipts)
                    .ToArray(),
                BuildBuildPathComparisons(buildPathCandidates),
                fallbackWatchouts
                    .Concat(RulesetUiDirectiveCatalog.BuildBuildExplainWatchouts(effectiveRulesetId))
                    .Concat(campaignWatchouts)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()));
        }

        string buildMethodLabel = string.IsNullOrWhiteSpace(build.BuildMethod) ? leadWorkspace.Summary.BuildMethod : build.BuildMethod;
        string priorityLadder = BuildPriorityLadder(build);
        string gameplayMode = string.IsNullOrWhiteSpace(rules.GameplayOption) ? "default gameplay state" : rules.GameplayOption;
        string bannedWare = BuildBannedWareSummary(rules.BannedWareGrades);
        int remainingContactPoints = Math.Max(build.ContactPoints - build.ContactPointsUsed, 0);
        string nextSafeAction = ResolveRefreshAction(displayName, runtimeInspector) ?? (remainingContactPoints == 0
            ? $"Continue {displayName}, but review contact allocation before you export or hand the dossier back into campaign play."
            : $"Continue {displayName} and inspect the current {buildMethodLabel} build before you export, publish, or reopen campaign work.");
        if (!string.IsNullOrWhiteSpace(campaignNextSafeAction) && string.IsNullOrWhiteSpace(ResolveRefreshAction(displayName, runtimeInspector)))
        {
            nextSafeAction = campaignNextSafeAction!;
        }
        string buildPathFocus = leadBuildPath is null
            ? string.Empty
            : $" Build path focus: {leadBuildPath.Suggestion.Title} keeps the next grounded handoff explicit.";
        string explainFocus = !string.IsNullOrWhiteSpace(campaignExplainFocus)
            ? $"Explain focus: {buildMethodLabel} build with {priorityLadder}; {gameplayMode}; current limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen.{buildPathFocus} {campaignExplainFocus}".Trim()
            : $"Explain focus: {buildMethodLabel} build with {priorityLadder}; {gameplayMode}; current limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen.{buildPathFocus}";
        string returnTarget = $"Return target: {displayName} on runtime {runtimeFingerprint}.";
        string installState = string.IsNullOrWhiteSpace(activeRuntime?.InstallState)
            ? "workspace-only"
            : activeRuntime.InstallState;
        string rulePosture = RulesetUiDirectiveCatalog.BuildGroundedRulePosture(
            effectiveRulesetId,
            rules.GameEdition,
            rules.Settings,
            gameplayMode,
            runtimeFingerprint,
            installState);

        List<string> watchouts =
        [
            remainingContactPoints == 0
                ? "Contact points are fully allocated, so any new social or team-facing change now forces a tradeoff."
                : $"Contact allocation leaves {remainingContactPoints} point(s) available before the next handoff.",
            string.Equals(bannedWare, "none", StringComparison.Ordinal)
                ? "No banned ware grades are currently blocking the next safe build/export decision."
                : $"Current rules state bans {bannedWare}, so gear and upgrade choices need an explicit compatibility review."
        ];

        if (rules.MaxKarma > 0)
        {
            watchouts.Add($"Campaign rules cap this build at {rules.MaxKarma} Karma before the next progression checkpoint changes.");
        }

        if (leadBuildPath?.Preview?.RequiresConfirmation == true)
        {
            watchouts.Add("The recommended build path still requires explicit confirmation before it can be applied.");
        }

        watchouts.AddRange(BuildRuntimeWatchouts(runtimeInspector));
        watchouts.AddRange(RulesetUiDirectiveCatalog.BuildBuildExplainWatchouts(effectiveRulesetId));
        watchouts.AddRange(campaignWatchouts);

        return Humanize(new DesktopHomeBuildExplainProjection(
            RulesetId: resolvedRulesetId,
            RulesetSpotlight: rulesetSpotlight,
            $"Build state: {buildMethodLabel} build with {priorityLadder}; contact points {build.ContactPointsUsed}/{build.ContactPoints}; special track {build.TotalSpecial}.\nRules state: {rules.GameEdition} · {rules.Settings} · {gameplayMode}; limits {rules.MaxKarma} Karma / {rules.MaxNuyen} nuyen; banned ware {bannedWare}.",
            nextSafeAction,
            explainFocus,
            runtimeHealthSummary,
            returnTarget,
            rulePosture,
            BuildCompatibilityReceipts(runtimeInspector, runtimeFingerprint)
                .Concat(buildPathReceipts)
                .Concat(campaignReceipts)
                .ToArray(),
            BuildBuildPathComparisons(buildPathCandidates),
            watchouts
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()));
    }

    private static string? ResolveRulesetId(params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            string? normalized = RulesetDefaults.NormalizeOptional(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildCampaignReceipts(AccountCampaignSummary? campaignSummary)
    {
        if (campaignSummary is null)
        {
            return [];
        }

        BuildLabHandoffProjection? leadHandoff = campaignSummary.BuildLabHandoffs
            .OrderByDescending(static handoff => handoff.UpdatedAtUtc)
            .FirstOrDefault();
        RulesNavigatorAnswerProjection? leadRulesAnswer = campaignSummary.RulesNavigator.FirstOrDefault();
        LegacyMigrationReceiptProjection? leadMigration = campaignSummary.MigrationReceipts
            .OrderByDescending(static receipt => receipt.ImportedAtUtc)
            .FirstOrDefault();
        CreatorPublicationProjection? leadPublication = campaignSummary.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();

        List<string> receipts = [];

        if (leadHandoff is not null)
        {
            receipts.Add($"Build Lab handoff: {leadHandoff.Title} — {leadHandoff.Summary}");
            if (leadHandoff.TradeoffLines.Count > 0)
            {
                receipts.Add($"Build Lab tradeoff: {leadHandoff.TradeoffLines[0]}");
            }

            if (leadHandoff.ProgressionOutcomes.Count > 0)
            {
                receipts.Add($"Build Lab progression: {leadHandoff.ProgressionOutcomes[0]}");
            }

            if (!string.IsNullOrWhiteSpace(leadHandoff.RuntimeCompatibilitySummary))
            {
                receipts.Add($"Build Lab runtime: {leadHandoff.RuntimeCompatibilitySummary}");
            }

            if (!string.IsNullOrWhiteSpace(leadHandoff.CampaignReturnSummary))
            {
                receipts.Add($"Build Lab return: {leadHandoff.CampaignReturnSummary}");
            }

            if (!string.IsNullOrWhiteSpace(leadHandoff.SupportClosureSummary))
            {
                receipts.Add($"Build Lab support: {leadHandoff.SupportClosureSummary}");
            }

            if (!string.IsNullOrWhiteSpace(leadHandoff.PlannerCoverageSummary))
            {
                receipts.Add($"Build Lab coverage: {leadHandoff.PlannerCoverageSummary}");
            }

            if (leadHandoff.PlannerCoverageLines is { Count: > 0 })
            {
                receipts.Add($"Build Lab coverage detail: {leadHandoff.PlannerCoverageLines[0]}");
            }
        }

        if (leadRulesAnswer is not null)
        {
            receipts.Add($"Rules navigator: {leadRulesAnswer.Question} — {leadRulesAnswer.ShortAnswer}");
            if (!string.IsNullOrWhiteSpace(leadRulesAnswer.AfterSummary))
            {
                receipts.Add($"Rules after: {leadRulesAnswer.AfterSummary}");
            }

            RulesetEnvironmentDiffProjection? leadRulesDiff = leadRulesAnswer.Diffs?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(leadRulesDiff?.AfterSummary))
            {
                receipts.Add($"Rules diff: {leadRulesDiff.Label} — {leadRulesDiff.AfterSummary}");
            }
        }

        if (leadMigration is not null)
        {
            receipts.Add($"Migration summary: {leadMigration.Summary}");
        }

        if (leadPublication is not null)
        {
            receipts.Add($"Publication summary: {leadPublication.Title} — {leadPublication.ProvenanceSummary}");
        }

        return receipts
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildCampaignWatchouts(AccountCampaignSummary? campaignSummary)
    {
        if (campaignSummary is null)
        {
            return [];
        }

        BuildLabHandoffProjection? leadHandoff = campaignSummary.BuildLabHandoffs
            .OrderByDescending(static handoff => handoff.UpdatedAtUtc)
            .FirstOrDefault();
        LegacyMigrationReceiptProjection? leadMigration = campaignSummary.MigrationReceipts
            .OrderByDescending(static receipt => receipt.ImportedAtUtc)
            .FirstOrDefault();
        CreatorPublicationProjection? leadPublication = campaignSummary.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();

        List<string> watchouts = [];
        if (leadHandoff?.Watchouts is not null)
        {
            watchouts.AddRange(leadHandoff.Watchouts);
        }

        LegacyMigrationFieldProjection? migrationAttention = leadMigration?.Fields.FirstOrDefault(static field => NeedsAttentionStatus(field.Status));
        if (migrationAttention is not null)
        {
            watchouts.Add($"Migration watchout: {migrationAttention.Label} is {migrationAttention.Status} — {migrationAttention.Summary}");
        }

        if (leadPublication is not null && NeedsAttentionStatus(leadPublication.PublicationStatus))
        {
            watchouts.Add($"Publication watchout: {leadPublication.Title} is {leadPublication.PublicationStatus} — {leadPublication.DiscoverySummary}");
        }

        return watchouts
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? BuildCampaignNextSafeAction(AccountCampaignSummary? campaignSummary)
    {
        if (campaignSummary is null)
        {
            return null;
        }

        BuildLabHandoffProjection? leadHandoff = campaignSummary.BuildLabHandoffs
            .OrderByDescending(static handoff => handoff.UpdatedAtUtc)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(leadHandoff?.NextSafeAction))
        {
            return leadHandoff.NextSafeAction;
        }

        RulesNavigatorAnswerProjection? leadRulesAnswer = campaignSummary.RulesNavigator.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(leadRulesAnswer?.AfterSummary))
        {
            return $"Review the current rules answer before the next handoff: {leadRulesAnswer.AfterSummary}";
        }

        return null;
    }

    private static string? BuildCampaignExplainFocus(AccountCampaignSummary? campaignSummary)
    {
        if (campaignSummary is null)
        {
            return null;
        }

        BuildLabHandoffProjection? leadHandoff = campaignSummary.BuildLabHandoffs
            .OrderByDescending(static handoff => handoff.UpdatedAtUtc)
            .FirstOrDefault();
        RulesNavigatorAnswerProjection? leadRulesAnswer = campaignSummary.RulesNavigator.FirstOrDefault();

        if (leadHandoff is null && leadRulesAnswer is null)
        {
            return null;
        }

        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(leadHandoff?.Summary))
        {
            parts.Add($"Campaign handoff: {leadHandoff.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(leadRulesAnswer?.BeforeSummary))
        {
            parts.Add($"Rules question: {leadRulesAnswer.BeforeSummary}");
        }

        return string.Join(" ", parts);
    }

    private static bool NeedsAttentionStatus(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && !status.Equals("healthy", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("info", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("ok", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("ready", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("published", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("safe", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("mapped", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("approved", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("active", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildBuildPathReceipts(
        DesktopBuildPathCandidate? buildPathCandidate)
    {
        if (buildPathCandidate is null)
        {
            return [];
        }

        DesktopBuildPathSuggestion buildPathSuggestion = buildPathCandidate.Suggestion;
        DesktopBuildPathPreview? buildPathPreview = buildPathCandidate.Preview;
        List<string> receipts =
        [
            buildPathPreview is null
                ? $"Build path option: {buildPathSuggestion.Title} is available for {string.Join(", ", buildPathSuggestion.Targets)} once a dossier is ready."
                : $"Build path option: {buildPathSuggestion.Title} is {buildPathPreview.State} for this dossier on runtime {buildPathPreview.RuntimeFingerprint ?? "pending"}."
        ];

        string? firstChange = buildPathPreview?.ChangeSummaries.FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary));
        if (!string.IsNullOrWhiteSpace(firstChange))
        {
            receipts.Add($"Build path change: {firstChange}");
        }

        string? firstDiagnostic = buildPathPreview?.DiagnosticMessages.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        if (!string.IsNullOrWhiteSpace(firstDiagnostic))
        {
            receipts.Add($"Build path diagnostic: {firstDiagnostic}");
        }

        if (!string.IsNullOrWhiteSpace(buildPathPreview?.RuntimeCompatibilitySummary))
        {
            receipts.Add($"Build path runtime: {buildPathPreview.RuntimeCompatibilitySummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildPathPreview?.CampaignReturnSummary))
        {
            receipts.Add($"Build path return: {buildPathPreview.CampaignReturnSummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildPathPreview?.SupportClosureSummary))
        {
            receipts.Add($"Build path support: {buildPathPreview.SupportClosureSummary}");
        }

        return receipts;
    }

    private static IReadOnlyList<string> BuildBuildPathComparisons(IReadOnlyList<DesktopBuildPathCandidate>? buildPathCandidates)
    {
        if (buildPathCandidates is null || buildPathCandidates.Count == 0)
        {
            return [];
        }

        return buildPathCandidates
            .Take(3)
            .Select(static candidate =>
            {
                string targetSummary = string.Join(", ", candidate.Suggestion.Targets);
                if (candidate.Preview is null)
                {
                    return $"Build path compare: {candidate.Suggestion.Title} is available for {targetSummary}, but the first grounded dossier still needs to land before the handoff can be compared.";
                }

                string headline = candidate.Preview.ChangeSummaries.FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary))
                    ?? candidate.Preview.DiagnosticMessages.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? $"State {candidate.Preview.State} is ready to compare.";
                string nextStep = candidate.Preview.RequiresConfirmation
                    ? "Requires explicit confirmation before it is applied."
                    : "Ready to move into the current story path.";
                string runtime = string.IsNullOrWhiteSpace(candidate.Preview.RuntimeFingerprint)
                    ? "runtime pending"
                    : $"runtime {candidate.Preview.RuntimeFingerprint}";
                return $"Build path compare: {candidate.Suggestion.Title} on {runtime}. {headline} {nextStep}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static DesktopHomeBuildExplainProjection Humanize(DesktopHomeBuildExplainProjection projection)
        => projection with
        {
            RulesetSpotlight = UndetectableHumanizerCopyAdapter.Humanize(projection.RulesetSpotlight),
            Summary = UndetectableHumanizerCopyAdapter.Humanize(projection.Summary),
            NextSafeAction = UndetectableHumanizerCopyAdapter.Humanize(projection.NextSafeAction),
            ExplainFocus = UndetectableHumanizerCopyAdapter.Humanize(projection.ExplainFocus),
            RuntimeHealthSummary = UndetectableHumanizerCopyAdapter.Humanize(projection.RuntimeHealthSummary),
            ReturnTarget = UndetectableHumanizerCopyAdapter.Humanize(projection.ReturnTarget),
            RulePosture = UndetectableHumanizerCopyAdapter.Humanize(projection.RulePosture),
            CompatibilityReceipts = UndetectableHumanizerCopyAdapter.HumanizeLines(projection.CompatibilityReceipts),
            BuildPathComparisons = UndetectableHumanizerCopyAdapter.HumanizeLines(projection.BuildPathComparisons),
            Watchouts = UndetectableHumanizerCopyAdapter.HumanizeLines(projection.Watchouts)
        };

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
            return "Runtime health: no active runtime profile is loaded for this dossier yet.";
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
                yield return "A required rule pack is missing, so rules answers and dossier handoffs need review first.";
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
                $"Compatibility note: runtime details are still loading for fingerprint {runtimeFingerprint}, so drift-sensitive decisions should stay in review."
            ];
        }

        List<string> receipts = [];
        if (runtimeInspector.CompatibilityDiagnostics.Count == 0)
        {
            receipts.Add($"Compatibility note: fingerprint {runtimeFingerprint} is aligned with the current runner and no runtime drift is active.");
        }

        foreach (RuntimeLockCompatibilityDiagnostic diagnostic in runtimeInspector.CompatibilityDiagnostics)
        {
            if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.RebindRequired, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility note: runtime drift requires a profile refresh before the next campaign return, export, or publication handoff.");
            }
            else if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.MissingPack, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility note: at least one required rule pack is missing, so build guidance and rules answers are incomplete.");
            }
            else if (string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.EngineApiMismatch, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility note: an engine mismatch blocks a safe handoff until the runtime and rules content line up again.");
            }
            else if (!string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.Ordinal))
            {
                receipts.Add($"Compatibility note: {diagnostic.Message}");
            }
        }

        foreach (RuntimeInspectorWarning warning in runtimeInspector.Warnings)
        {
            if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.Migration, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility details: migration guidance is active, so the next campaign-facing step should stay explicitly review-required.");
            }
            else if (string.Equals(warning.Kind, RuntimeInspectorWarningKinds.ProviderBinding, StringComparison.Ordinal))
            {
                receipts.Add("Compatibility details: service links changed recently, so explanations should be rechecked before you trust them in support or publication.");
            }
        }

        return receipts
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
