using Avalonia.Controls;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class DesktopHorizonWorkbenchLauncher
{
    public static bool SupportsNativeWorkbench(string horizonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(horizonId);

        return horizonId switch
        {
            "karma_forge" => true,
            "alice" => true,
            "nexus_pan" => true,
            "jackpoint" => true,
            "runsite" => true,
            "run_control" => true,
            "runbook_press" => true,
            "table_pulse" => true,
            "black_ledger" => true,
            "community_hub" => true,
            "creator_os" => true,
            "anarchy" => true,
            "ghostwire" => true,
            "runner_passport" => true,
            "quicksilver" => true,
            "local_co_processor" => true,
            _ => false
        };
    }

    public static Task OpenKarmaForgeAsync(Window owner, string headId)
        => DesktopKarmaForgeWindow.ShowAsync(owner, headId);

    public static Task OpenAsync(Window owner, string headId, DesktopHorizonWorkbenchEntry entry)
        => OpenAsync(owner, headId, entry.Id, entry.PrimaryAction.RelativeHref);

    public static Task OpenAsync(Window owner, string headId, string horizonId, string? fallbackRelativeHref = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentException.ThrowIfNullOrWhiteSpace(horizonId);

        return horizonId switch
        {
            "karma_forge" => DesktopKarmaForgeWindow.ShowAsync(owner, headId),
            "alice" => DesktopAliceWindow.ShowAsync(owner, headId),
            "nexus_pan" => DesktopDevicesAccessWindow.ShowAsync(owner, headId),
            "jackpoint" => DesktopJackpointWindow.ShowAsync(owner, headId),
            "runsite" => DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(owner, headId),
            "run_control" => DesktopRunControlWindow.ShowAsync(owner, headId),
            "runbook_press" => DesktopCreatorPublicationWindow.ShowAsync(owner, headId),
            "table_pulse" => DesktopTablePulseWindow.ShowAsync(owner, headId),
            "black_ledger" => DesktopBlackLedgerWindow.ShowAsync(owner, headId),
            "community_hub" => DesktopCommunityHubWindow.ShowAsync(owner, headId),
            "creator_os" => DesktopCreatorPublicationWindow.ShowAsync(owner, headId),
            "anarchy" => DesktopAnarchyWindow.ShowAsync(owner, headId),
            "ghostwire" => DesktopGhostwireWindow.ShowAsync(owner, headId),
            "runner_passport" => DesktopRunnerPassportWindow.ShowAsync(owner, headId),
            "quicksilver" => DesktopQuicksilverWindow.ShowAsync(owner, headId),
            "local_co_processor" => DesktopLocalCoProcessorWindow.ShowAsync(owner, headId),
            _ when !string.IsNullOrWhiteSpace(fallbackRelativeHref) => Task.FromResult(DesktopInstallLinkingRuntime.TryOpenRelativePortal(fallbackRelativeHref)),
            _ => Task.CompletedTask
        };
    }
}
