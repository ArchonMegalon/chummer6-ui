namespace Chummer.Desktop.Runtime;

public static class DesktopSurfacePostureText
{
    public static IReadOnlyList<string> BuildLines(DesktopUpdateClientStatus updateStatus)
    {
        ArgumentNullException.ThrowIfNull(updateStatus);

        if (string.IsNullOrWhiteSpace(updateStatus.DesktopChannelRef)
            && string.IsNullOrWhiteSpace(updateStatus.InstallGuidanceRef)
            && string.IsNullOrWhiteSpace(updateStatus.ParticipationReceiptRef)
            && string.IsNullOrWhiteSpace(updateStatus.RewardPublicationRef)
            && string.IsNullOrWhiteSpace(updateStatus.PublicInstallRoute)
            && string.IsNullOrWhiteSpace(updateStatus.InstallAccessClass)
            && string.IsNullOrWhiteSpace(updateStatus.DesktopSurfaceRationale))
        {
            return Array.Empty<string>();
        }

        List<string> lines = [];
        if (string.Equals(updateStatus.InstallAccessClass, "account_required", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Entitlement posture: account-linked install guidance is required for this desktop channel.");
        }
        else if (string.Equals(updateStatus.InstallAccessClass, "open_public", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Entitlement posture: this desktop channel remains guest-readable until you choose to claim it.");
        }
        else if (!string.IsNullOrWhiteSpace(updateStatus.InstallAccessClass))
        {
            lines.Add($"Entitlement posture: {updateStatus.InstallAccessClass}.");
        }

        lines.Add("Desktop follow-through: Devices & Access keeps claim, entitlement, participation, and recovery posture visible before any browser handoff.");

        if (!string.IsNullOrWhiteSpace(updateStatus.DesktopChannelRef))
        {
            lines.Add($"Desktop channel ref: {updateStatus.DesktopChannelRef}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.InstallGuidanceRef))
        {
            lines.Add($"Install guidance ref: {updateStatus.InstallGuidanceRef}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.ParticipationReceiptRef))
        {
            lines.Add($"Participation receipt: {updateStatus.ParticipationReceiptRef}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.RewardPublicationRef))
        {
            lines.Add($"Reward publication ref: {updateStatus.RewardPublicationRef}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.PublicInstallRoute))
        {
            lines.Add($"Recovery route: {updateStatus.PublicInstallRoute}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.DesktopSurfaceRationale))
        {
            lines.Add($"Registry rationale: {updateStatus.DesktopSurfaceRationale}");
        }

        return lines;
    }
}
