using System.Linq;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private string DesktopMarqueeCssClass => $"flagship-marquee marquee-{ResolveDesktopMarqueeTone()}";

    private string DesktopMarqueeEyebrow =>
        RulesetUiDirectiveCatalog.BuildDesktopMarqueeEyebrow(ResolveDesktopMarqueeRulesetId());

    private string DesktopMarqueeTitle =>
        RulesetUiDirectiveCatalog.BuildDesktopMarqueeTitle(ResolveDesktopMarqueeRulesetId());

    private string DesktopMarqueeSpotlight =>
        RulesetUiDirectiveCatalog.BuildHomeSpotlight(ResolveDesktopMarqueeRulesetId());

    private string DesktopMarqueeResumeLane =>
        RulesetUiDirectiveCatalog.Resolve(ResolveDesktopMarqueeRulesetId()).ResumeLaneSummary;

    private string DesktopMarqueePosture
    {
        get
        {
            RulesetUiDirective directive = RulesetUiDirectiveCatalog.Resolve(ResolveDesktopMarqueeRulesetId());
            return $"{directive.DisplayName} · {directive.PostureLabel} · {directive.FileExtension}";
        }
    }

    private string DesktopMarqueeRuntime =>
        ShellStatusTextFormatter.BuildActiveRuntimeSummary(
            _shellSurfaceState.ActiveRuntime,
            ResolveDesktopMarqueeRulesetId());

    private string DesktopMarqueeContinuity
    {
        get
        {
            IReadOnlyList<OpenWorkspaceState> workspaces = _shellSurfaceState.OpenWorkspaces;
            if (workspaces.Count == 0)
            {
                return "No grounded dossier is open yet; restore or import one before claiming flagship continuity.";
            }

            OpenWorkspaceState leadWorkspace = ResolveActiveWorkspace(workspaces) ?? workspaces[0];
            string alias = string.IsNullOrWhiteSpace(leadWorkspace.Alias)
                ? leadWorkspace.Name
                : $"{leadWorkspace.Name} / {leadWorkspace.Alias}";
            return $"{workspaces.Count} open dossiers · lead {alias}.";
        }
    }

    private string DesktopMarqueeSurfaceSummary =>
        $"{_shellSurfaceState.NavigationTabs.Count} tabs · {_shellSurfaceState.WorkspaceActions.Count} action rails · {(_shellSurfaceState.WorkflowDefinitions?.Count ?? 0)} workflows";

    private IReadOnlyList<string> DesktopMarqueeWatchouts =>
        RulesetUiDirectiveCatalog.Resolve(ResolveDesktopMarqueeRulesetId()).BuildExplainWatchouts.Take(2).ToArray();

    private string? ResolveDesktopMarqueeRulesetId()
        => RulesetDefaults.NormalizeOptional(_shellSurfaceState.ActiveRulesetId)
            ?? RulesetDefaults.NormalizeOptional(_shellSurfaceState.ActiveRuntime?.RulesetId)
            ?? RulesetDefaults.NormalizeOptional(State.Rules?.GameEdition)
            ?? RulesetDefaults.NormalizeOptional(_shellSurfaceState.OpenWorkspaces.FirstOrDefault()?.RulesetId);

    private string ResolveDesktopMarqueeTone()
        => RulesetUiDirectiveCatalog.Resolve(ResolveDesktopMarqueeRulesetId()).RulesetId switch
        {
            RulesetDefaults.Sr4 => "sr4",
            RulesetDefaults.Sr5 => "sr5",
            RulesetDefaults.Sr6 => "sr6",
            _ => "shared"
        };

    private OpenWorkspaceState? ResolveActiveWorkspace(IReadOnlyList<OpenWorkspaceState> workspaces)
    {
        if (_shellSurfaceState.ActiveWorkspaceId is null)
        {
            return null;
        }

        return workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id.Value, _shellSurfaceState.ActiveWorkspaceId.Value.Value, StringComparison.Ordinal));
    }
}
