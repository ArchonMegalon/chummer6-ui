namespace Chummer.Desktop.Runtime;

public static class DesktopStartupSurfaceCatalog
{
    public const string EnvironmentVariableName = "CHUMMER_DESKTOP_STARTUP_SURFACE";
    public const string CampaignWorkspace = "campaign_workspace";
    public const string Update = "update";
    public const string Support = "support";
    public const string SupportCase = "support_case";
    public const string DevicesAccess = "devices_access";
    public const string CampaignPrimer = "campaign_primer";
    public const string MissionBriefing = "mission_briefing";
    public const string ReportIssue = "report_issue";
    public const string CrashRecovery = "crash_recovery";
    public const string Settings = "settings";

    public static bool Matches(string? startupSurface, string expectedSurface)
        => !string.IsNullOrWhiteSpace(expectedSurface)
           && string.Equals(startupSurface, expectedSurface, StringComparison.OrdinalIgnoreCase);
}
