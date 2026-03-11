using Chummer.Contracts.Content;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public static class ShellStatusTextFormatter
{
    public static string BuildActiveRuntimeSummary(Chummer.Contracts.Content.ActiveRuntimeStatusProjection? activeRuntime)
    {
        if (activeRuntime is null)
        {
            return "No active runtime profile";
        }

        string installState = string.IsNullOrWhiteSpace(activeRuntime.InstallState)
            ? ArtifactInstallStates.Available
            : activeRuntime.InstallState;
        string warningSuffix = activeRuntime.WarningCount > 0
            ? $", warnings={activeRuntime.WarningCount}"
            : string.Empty;

        return $"{activeRuntime.Title} [{TrimFingerprint(activeRuntime.RuntimeFingerprint)}] ({installState}{warningSuffix})";
    }

    public static string BuildComplianceState(ShellSurfaceState shellSurface, DesktopPreferenceState preferences)
    {
        ArgumentNullException.ThrowIfNull(shellSurface);
        ArgumentNullException.ThrowIfNull(preferences);

        string rulesetId = string.IsNullOrWhiteSpace(shellSurface.ActiveRulesetId)
            ? "unresolved"
            : shellSurface.ActiveRulesetId;
        int workflowDefinitionCount = shellSurface.WorkflowDefinitions?.Count ?? 0;
        int workflowSurfaceCount = shellSurface.WorkflowSurfaces?.Count ?? 0;
        string runtimeState = shellSurface.ActiveRuntime is null
            ? "Runtime: none"
            : $"Runtime: {BuildActiveRuntimeSummary(shellSurface.ActiveRuntime)}";

        return $"{runtimeState} | Ruleset: {rulesetId} | Workflows: {workflowDefinitionCount} defs / {workflowSurfaceCount} surfaces | Prefs: {preferences.UiScalePercent}%/{preferences.Theme}/{preferences.Language}";
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
