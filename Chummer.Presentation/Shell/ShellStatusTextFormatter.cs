using Chummer.Contracts.Content;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;

namespace Chummer.Presentation.Shell;

public static class ShellStatusTextFormatter
{
    public static string BuildActiveRuntimeSummary(
        Chummer.Contracts.Content.ActiveRuntimeStatusProjection? activeRuntime,
        string? rulesetId = null)
    {
        RulesetUiDirective directive = RulesetUiDirectiveCatalog.Resolve(rulesetId ?? activeRuntime?.RulesetId);

        if (activeRuntime is null)
        {
            return $"{directive.DisplayName} · {directive.PostureLabel} · no active runtime profile";
        }

        string installState = string.IsNullOrWhiteSpace(activeRuntime.InstallState)
            ? ArtifactInstallStates.Available
            : activeRuntime.InstallState;
        string warningSuffix = activeRuntime.WarningCount > 0
            ? $", warnings={activeRuntime.WarningCount}"
            : string.Empty;

        return $"{directive.DisplayName} · {directive.PostureLabel} · {activeRuntime.Title} [{TrimFingerprint(activeRuntime.RuntimeFingerprint)}] ({installState}{warningSuffix})";
    }

    public static string BuildComplianceState(ShellSurfaceState shellSurface, DesktopPreferenceState preferences)
    {
        ArgumentNullException.ThrowIfNull(shellSurface);
        ArgumentNullException.ThrowIfNull(preferences);

        int workflowDefinitionCount = shellSurface.WorkflowDefinitions?.Count ?? 0;
        int workflowSurfaceCount = shellSurface.WorkflowSurfaces?.Count ?? 0;
        RulesetUiDirective directive = RulesetUiDirectiveCatalog.Resolve(shellSurface.ActiveRulesetId ?? shellSurface.ActiveRuntime?.RulesetId);
        string rulesetSummary = $"{directive.DisplayName} {directive.FileExtension}";
        return $"Ruleset: {rulesetSummary} | Workflows: {workflowDefinitionCount} defs / {workflowSurfaceCount} surfaces | Prefs: {preferences.UiScalePercent}%/{preferences.Theme}/{preferences.Language}";
    }

    private static string TrimFingerprint(string runtimeFingerprint)
    {
        if (string.IsNullOrWhiteSpace(runtimeFingerprint))
        {
            return "unresolved";
        }

        return runtimeFingerprint.Length <= 18
            ? runtimeFingerprint
            : runtimeFingerprint[..18];
    }
}
