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
            lines.Add("Account link: required before this copy can be restored on another device.");
        }
        else if (string.Equals(updateStatus.InstallAccessClass, "open_public", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Account link: optional. You can claim this copy later for account recovery.");
        }
        else if (!string.IsNullOrWhiteSpace(updateStatus.InstallAccessClass))
        {
            lines.Add($"Account link: {FormatStatusLabel(updateStatus.InstallAccessClass)}.");
        }

        lines.Add("Devices & Access keeps this copy, downloads, updates, and recovery in one place.");

        if (!string.IsNullOrWhiteSpace(updateStatus.DesktopChannelRef))
        {
            lines.Add("Download channel: available.");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.InstallGuidanceRef))
        {
            lines.Add("Install help: available.");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.ParticipationReceiptRef))
        {
            lines.Add("Account activity: available.");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.RewardPublicationRef))
        {
            lines.Add("Rewards: available.");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.PublicInstallRoute))
        {
            lines.Add($"Recovery page: {updateStatus.PublicInstallRoute}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.DesktopSurfaceRationale))
        {
            lines.Add($"Release note: {CleanUserFacingFragment(updateStatus.DesktopSurfaceRationale)}");
        }

        return lines;
    }

    private static string FormatStatusLabel(string value)
        => CleanUserFacingFragment(value.Replace('_', ' ').Replace('-', ' '));

    private static string CleanUserFacingFragment(string value)
    {
        string cleaned = value.Trim().Replace('_', ' ').Replace('-', ' ');
        foreach ((string from, string to) in new[]
                 {
                     ("registry proof posture", "release status"),
                     ("proof posture", "status"),
                     ("install rail", "install path"),
                     ("release rail", "release path")
                 })
        {
            cleaned = ReplacePhrase(cleaned, from, to);
        }

        foreach ((string from, string to) in new[]
                 {
                     ("proof", "status"),
                     ("receipts", "records"),
                     ("receipt", "record"),
                     ("registry", "release records"),
                     ("posture", "status"),
                     ("entitlement", "account access"),
                     ("participation", "account activity"),
                     ("handoff", "return path"),
                     ("rail", "path"),
                     ("lane", "path"),
                     ("ref", "link")
                 })
        {
            cleaned = ReplaceWord(cleaned, from, to);
        }

        cleaned = ReplacePhrase(cleaned, "status status", "status");
        return cleaned.Trim();
    }

    private static string ReplacePhrase(string value, string from, string to)
        => System.Text.RegularExpressions.Regex.Replace(
            value,
            System.Text.RegularExpressions.Regex.Escape(from),
            to,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string ReplaceWord(string value, string from, string to)
        => System.Text.RegularExpressions.Regex.Replace(
            value,
            $@"(?<![A-Za-z0-9]){System.Text.RegularExpressions.Regex.Escape(from)}(?![A-Za-z0-9])",
            to,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
