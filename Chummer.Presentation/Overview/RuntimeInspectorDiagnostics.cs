using System;
using System.Linq;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Overview;

public static class RuntimeInspectorDiagnostics
{
    private static readonly string[] HubSourceKinds =
    [
        RegistryEntrySourceKinds.PersistedManifest,
        RegistryEntrySourceKinds.OverlayCatalogBridge
    ];

    public static int CountProfileAttentionItems(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return projection.Warnings.Count(warning => !string.Equals(warning.Severity, RuntimeInspectorWarningSeverityLevels.Info, StringComparison.Ordinal))
            + projection.CompatibilityDiagnostics.Count(diagnostic => !string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.Ordinal))
            + projection.MigrationPreview.Count(item => item.RequiresRebind);
    }

    public static int CountProfileSessionSafeBindings(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return projection.ProviderBindings.Count(binding => binding.SessionSafe);
    }

    public static int CountRulePackWarnings(RuntimeInspectorProjection projection, RuntimeInspectorRulePackEntry rulePack)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(rulePack);

        return projection.Warnings.Count(warning => MatchesRulePack(rulePack, warning.SubjectId));
    }

    public static int CountRulePackMigrationItems(RuntimeInspectorProjection projection, RuntimeInspectorRulePackEntry rulePack)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(rulePack);

        return projection.MigrationPreview.Count(item => MatchesRulePack(rulePack, item.SubjectId));
    }

    public static int CountRulePackProviderBindings(RuntimeInspectorProjection projection, RuntimeInspectorRulePackEntry rulePack)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(rulePack);

        return projection.ProviderBindings.Count(binding =>
            string.Equals(binding.PackId, rulePack.RulePack.Id, StringComparison.Ordinal)
            || rulePack.CapabilityIds.Any(capabilityId => string.Equals(capabilityId, binding.CapabilityId, StringComparison.Ordinal)));
    }

    public static string BuildProfileDiagnosticsSummary(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return string.Join(
            Environment.NewLine,
            [
                $"Install target: {FormatInstallTarget(projection.Install)}",
                $"Profile source: {projection.ProfileSourceKind}",
                $"Session-safe bindings: {CountProfileSessionSafeBindings(projection)}/{projection.ProviderBindings.Count}",
                $"Attention items: {CountProfileAttentionItems(projection)}",
                $"Generated: {projection.GeneratedAtUtc.UtcDateTime:u}"
            ]);
    }

    public static string BuildRulePackDiagnosticsSummary(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (projection.ResolvedRulePacks.Count == 0)
        {
            return "(none)";
        }

        return string.Join(
            Environment.NewLine,
            projection.ResolvedRulePacks.Select(rulePack =>
                $"{rulePack.RulePack.Id}@{rulePack.RulePack.Version} | bindings={CountRulePackProviderBindings(projection, rulePack)} | warnings={CountRulePackWarnings(projection, rulePack)} | migration={CountRulePackMigrationItems(projection, rulePack)}"));
    }

    public static int CountHubOriginRulePacks(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return projection.ResolvedRulePacks.Count(rulePack => IsHubSourceKind(rulePack.SourceKind));
    }

    public static bool HasStaleOrInvalidatedState(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return projection.MigrationPreview.Any(item => item.RequiresRebind)
            || projection.CompatibilityDiagnostics.Any(diagnostic => !string.Equals(diagnostic.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.Ordinal))
            || projection.Warnings.Any(warning => !string.Equals(warning.Severity, RuntimeInspectorWarningSeverityLevels.Info, StringComparison.Ordinal));
    }

    public static string BuildHubClientDiagnosticsSummary(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        int hubOriginRulePackCount = CountHubOriginRulePacks(projection);
        bool hasStaleState = HasStaleOrInvalidatedState(projection);
        string reviewState = hasStaleState ? "review required" : "review current";
        string updateState = hasStaleState ? "refresh pending" : "no refresh required";

        return string.Join(
            Environment.NewLine,
            [
                $"Hub profile source: {projection.ProfileSourceKind}",
                $"Hub-origin RulePacks: {hubOriginRulePackCount}/{projection.ResolvedRulePacks.Count}",
                $"Install state: {projection.Install.State} ({FormatInstallTarget(projection.Install)})",
                $"Review state: {reviewState}",
                $"Update state: {updateState}",
                $"Stale/invalidation: {(hasStaleState ? "action required" : "none")}",
                $"Generated: {projection.GeneratedAtUtc.UtcDateTime:u}"
            ]);
    }

    public static string FormatInstallTarget(ArtifactInstallState install)
    {
        ArgumentNullException.ThrowIfNull(install);

        if (string.IsNullOrWhiteSpace(install.InstalledTargetKind))
        {
            return "(none)";
        }

        return string.IsNullOrWhiteSpace(install.InstalledTargetId)
            ? install.InstalledTargetKind
            : $"{install.InstalledTargetKind}:{install.InstalledTargetId}";
    }

    private static bool MatchesRulePack(RuntimeInspectorRulePackEntry rulePack, string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        return string.Equals(subjectId, rulePack.RulePack.Id, StringComparison.Ordinal)
            || rulePack.CapabilityIds.Any(capabilityId => string.Equals(capabilityId, subjectId, StringComparison.Ordinal));
    }

    private static bool IsHubSourceKind(string sourceKind)
    {
        if (string.IsNullOrWhiteSpace(sourceKind))
        {
            return false;
        }

        return HubSourceKinds.Any(kind => string.Equals(kind, sourceKind, StringComparison.Ordinal));
    }
}
