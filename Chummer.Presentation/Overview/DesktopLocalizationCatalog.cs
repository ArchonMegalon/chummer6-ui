using System.Globalization;

namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportedLanguage(
    string Code,
    string Label);

public static class DesktopLocalizationCatalog
{
    public const string DefaultLanguage = "en-us";
    private static readonly IReadOnlyDictionary<string, string> DefaultTrustSurfaceStrings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["desktop.shell.window_title"] = "Chummer Desktop",
        ["desktop.shell.banner"] = "Chummer desktop",
        ["desktop.shell.menu.file"] = "File",
        ["desktop.shell.menu.edit"] = "Edit",
        ["desktop.shell.menu.special"] = "Special",
        ["desktop.shell.menu.tools"] = "Tools",
        ["desktop.shell.menu.windows"] = "Windows",
        ["desktop.shell.menu.help"] = "Help",
        ["desktop.shell.tool.desktop_home"] = "Desktop Home",
        ["desktop.shell.tool.campaign_workspace"] = "Campaign Workspace",
        ["desktop.shell.tool.link_copy"] = "Link This Copy",
        ["desktop.shell.tool.open_support"] = "Open Support",
        ["desktop.shell.tool.import_character_file"] = "Import Character File",
        ["desktop.shell.tool.import_raw_xml"] = "Import Raw XML",
        ["desktop.shell.tool.save_workspace"] = "Save Workspace",
        ["desktop.shell.tool.close_active_workspace"] = "Close Active Workspace",
        ["desktop.shell.tool.status_idle"] = "State: idle",
        ["desktop.shell.state.value.ready"] = "ready",
        ["desktop.shell.state.value.busy"] = "busy",
        ["desktop.shell.state.value.saved"] = "saved",
        ["desktop.shell.state.value.unsaved"] = "unsaved",
        ["desktop.shell.state.value.loaded"] = "loaded",
        ["desktop.shell.state.value.online"] = "online",
        ["desktop.shell.state.value.error"] = "error",
        ["desktop.shell.value.none"] = "none",
        ["desktop.shell.value.na"] = "n/a",
        ["desktop.shell.state.snapshot"] = "State: {0}, workspace={1}, open={2}, saved={3}, last-command={4}",
        ["desktop.shell.state.error"] = "State: error - {0}",
        ["desktop.shell.workspace_strip.summary"] = "Workspace: {0} (open: {1}, {2})",
        ["desktop.shell.workspace_strip.empty"] = "Workspace: none",
        ["desktop.shell.summary.name"] = "Name",
        ["desktop.shell.summary.alias"] = "Alias",
        ["desktop.shell.summary.karma"] = "Karma",
        ["desktop.shell.summary.skills"] = "Skills",
        ["desktop.shell.summary.runtime"] = "Runtime",
        ["desktop.shell.summary.inspect_runtime"] = "Inspect Runtime",
        ["desktop.shell.summary.empty_value"] = "-",
        ["desktop.shell.status.character"] = "Character: {0}",
        ["desktop.shell.status.service"] = "Service: {0}",
        ["desktop.shell.status.time"] = "Time: {0}",
        ["desktop.shell.status.time_placeholder"] = "Time: -",
        ["desktop.shell.status.compliance_placeholder"] = "Compliance: shared presenter path",
        ["desktop.shell.feedback.import_raw_required"] = "State: provide debug XML content before importing.",
        ["desktop.shell.feedback.import_file_unavailable"] = "State: file picker unavailable on this platform.",
        ["desktop.shell.feedback.no_active_workspace"] = "State: no active workspace to close.",
        ["desktop.shell.feedback.desktop_home_reviewed"] = "State: desktop home reviewed.",
        ["desktop.shell.feedback.campaign_workspace_reviewed"] = "State: campaign workspace reviewed.",
        ["desktop.shell.feedback.install_linking_reviewed"] = "State: install linking reviewed.",
        ["desktop.shell.feedback.install_support_opened"] = "State: opened install-aware support.",
        ["desktop.shell.feedback.install_support_unavailable"] = "State: install-aware support is unavailable on this host.",
        ["desktop.shell.feedback.operation_failed_state"] = "State: error - {0} failed: {1}",
        ["desktop.shell.feedback.operation_failed_notice"] = "Notice: {0} failed.",
        ["desktop.home.title"] = "Desktop home cockpit",
        ["desktop.home.section.install_support"] = "Install and support",
        ["desktop.home.section.update_posture"] = "Update posture",
        ["desktop.home.section.campaign_return"] = "Campaign return and restore",
        ["desktop.home.section.support_closure"] = "Support closure and fix notices",
        ["desktop.home.section.build_explain"] = "Build and explain next",
        ["desktop.home.section.language_trust"] = "Language and trust surfaces",
        ["desktop.home.section.recent_workspaces"] = "Recent workspaces",
        ["desktop.campaign.title"] = "Campaign workspace",
        ["desktop.campaign.heading"] = "Campaign workspace",
        ["desktop.campaign.section.runboard"] = "Session readiness and runboard",
        ["desktop.campaign.section.restore"] = "Restore and device posture",
        ["desktop.campaign.section.support"] = "Support and watchouts",
        ["desktop.campaign.section.recent_workspaces"] = "Recent workspaces",
        ["desktop.campaign.button.refresh"] = "Refresh",
        ["desktop.campaign.intro.guest"] = "This campaign workspace is still using guest and local fallback posture. Link this copy before you trust restore, device-role, or support closure as install-aware truth.",
        ["desktop.campaign.intro.local_fallback"] = "The live campaign server plane is unavailable, so this workspace is showing the strongest safe local campaign digest and restore posture available on this desktop.",
        ["desktop.campaign.intro.watchouts"] = "This campaign workspace is grounded, but it has watchouts to clear before you resume live runboard work.",
        ["desktop.campaign.intro.ready"] = "This campaign workspace is grounded and ready to restore session posture, runboard state, and support closure from the flagship desktop.",
        ["desktop.campaign.status.local_fallback"] = "Campaign workspace status: bounded local fallback is active because the live campaign server plane is unavailable.",
        ["desktop.campaign.status.server_generated"] = "Campaign workspace status: live server plane generated {0} UTC.",
        ["desktop.campaign.status.refresh_failed"] = "Campaign workspace status: refresh failed, so the last good state is still shown with bounded fallback.",
        ["desktop.campaign.readiness.local_fallback"] = "Runboard detail is currently bounded to locally available campaign truth until the live server plane returns.",
        ["desktop.campaign.restore.latest_workspace"] = "Latest local workspace: {0} . {1} UTC",
        ["desktop.campaign.restore.no_workspace"] = "No local workspace is pinned yet, so restore posture stays bounded to campaign digest and claimed-device truth.",
        ["desktop.campaign.support.no_watchouts"] = "No current campaign or support watchouts are blocking continuation from this desktop surface.",
        ["desktop.install_link.title"] = "Link this copy",
        ["desktop.install_link.heading"] = "Link this desktop copy to your account",
        ["desktop.install_link.summary"] = "Chummer keeps the binary canonical. Linking happens through an install claim code and a Hub-issued installation grant instead of mutating the installer per user.",
        ["desktop.install_link.shipping_locales"] = "Shipping locales: {0}. Install, update, and support trust flows should stay aligned across this desktop wave.",
        ["desktop.install_link.claim_code_label"] = "Install claim code",
        ["desktop.install_link.button.copy_install_id"] = "Copy Install ID",
        ["desktop.install_link.button.open_downloads"] = "Open Downloads",
        ["desktop.install_link.button.open_support"] = "Open Support",
        ["desktop.install_link.button.open_work"] = "Open Work",
        ["desktop.install_link.button.open_account"] = "Open Account",
        ["desktop.install_link.button.link_copy"] = "Link This Copy",
        ["desktop.install_link.button.continue_guest"] = "Continue as Guest",
        ["desktop.home.language_summary"] = "Language: {0}\nShipping locales: {1}\nLanguage changes apply fully on restart during the current desktop wave.",
        ["desktop.home.next_safe_action"] = "Next safe action: {0}",
        ["desktop.home.watchout"] = "Watchout: {0}",
        ["desktop.home.intro.claim_failed_guest"] = "This flagship desktop head is still running as a guest because the last claim attempt failed. Link this copy from home before you rely on install-aware support, fix notices, or roaming continuity.",
        ["desktop.home.intro.guest_recommended_link"] = "This flagship desktop head is ready to continue as a guest, but the account-aware path is the recommended route if you want install-aware support, fix notices, and roaming continuity.",
        ["desktop.home.intro.update_available"] = "A promoted update is ready for this install. Review the update posture before you jump back into campaign work.",
        ["desktop.home.intro.release_posture_review"] = "This desktop head is linked, but the current release posture needs review before you trust update, support, and campaign continuity on this install.",
        ["desktop.home.intro.campaign_watchouts"] = "This desktop head is linked and current enough to continue, but the campaign return lane has watchouts to review before you reopen work.",
        ["desktop.home.intro.ready_recent_workspaces"] = "This desktop head is linked, current enough to continue, and ready to drop back into recent workspaces.",
        ["desktop.home.intro.ready_current_campaign_workspace"] = "This desktop head is linked, current enough to continue, and ready to drop back into the current campaign workspace.",
        ["desktop.home.install_summary.install_id"] = "Install ID: {0}",
        ["desktop.home.install_summary.head"] = "Head: {0}",
        ["desktop.home.install_summary.version"] = "Version: {0}",
        ["desktop.home.install_summary.channel"] = "Channel: {0}",
        ["desktop.home.install_summary.platform"] = "Platform: {0}/{1}",
        ["desktop.home.install_summary.linked_status"] = "Status: Linked to account. Grant expires {0} UTC.",
        ["desktop.home.install_summary.unlinked_status"] = "Status: Not linked yet. Support and closure stay stronger once the install is claimed.",
        ["desktop.home.install_summary.last_guest_defer"] = "Last guest defer: {0} UTC.",
        ["desktop.home.install_summary.last_claim_attempt"] = "Last claim attempt: {0} UTC.",
        ["desktop.home.install_summary.hub_message"] = "Hub message: {0}",
        ["desktop.home.install_summary.claim_error"] = "Claim error: {0}",
        ["desktop.home.update_summary"] = "Status: {0}\nInstalled: {1}\nManifest: {2}\nManifest published: {3} UTC\nChannel: {4}\nLast checked: {5} UTC\nAuto apply: {6}\nRelease posture: {7}\nRollout reason: {8}\nSupportability: {9}\nSupportability summary: {10}\nLocal release proof: {11}\nProof generated: {12} UTC\nKnown issues: {13}\nFix availability: {14}\nRecommended action: {15}\nLast error: {16}",
        ["desktop.home.value.unknown"] = "Unknown",
        ["desktop.home.value.never"] = "Never",
        ["desktop.home.value.none"] = "None",
        ["desktop.home.value.none_published"] = "None published",
        ["desktop.home.value.no_supportability_summary"] = "No supportability summary published yet.",
        ["desktop.home.value.no_fix_guidance"] = "No fix guidance published yet.",
        ["desktop.home.workspace_summary.empty"] = "No recent workspaces were restored yet. Import or create a runner to seed the campaign workspace lane.",
        ["desktop.home.workspace_summary.entry"] = "{0} . {1} . {2} UTC",
        ["desktop.home.button.continue"] = "Continue",
        ["desktop.home.button.open_devices_access"] = "Open Devices and Access",
        ["desktop.home.button.open_current_workspace"] = "Open Current Workspace",
        ["desktop.home.button.open_current_campaign_workspace"] = "Open Current Campaign Workspace",
        ["desktop.home.button.open_install_support"] = "Open Install Support",
        ["desktop.home.button.open_update_support"] = "Open Update Support",
        ["desktop.home.button.open_work_support"] = "Open Work Support",
        ["desktop.home.button.open_tracked_case"] = "Open Tracked Case",
        ["desktop.home.button.open_campaign_followthrough"] = "Open Campaign Follow-through",
        ["desktop.home.button.open_build_followthrough"] = "Open Build Follow-through",
        ["desktop.home.button.open_workspace_followthrough"] = "Open Workspace Follow-through",
        ["desktop.install_link.claim_code_watermark"] = "Paste the claim code from your Hub account",
        ["desktop.install_link.status.prompt_guest_claim"] = "If you downloaded while signed in, copy the pending claim code from your Hub account and paste it here.",
        ["desktop.install_link.status.clipboard_unavailable"] = "Clipboard access is unavailable in this host.",
        ["desktop.install_link.status.install_id_copied"] = "Copied the installation id to the clipboard.",
        ["desktop.install_link.status.opened_work_route"] = "Opened the account-aware work route so you can confirm restore, support, and update follow-through on this claimed install.",
        ["desktop.install_link.status.unable_open_work_route"] = "Unable to open the account-aware work route from this host.",
        ["desktop.install_link.status.opened_account"] = "Opened your Hub account so you can review or copy the install claim code.",
        ["desktop.install_link.status.unable_open_account"] = "Unable to open the Hub account page from this host.",
        ["desktop.install_link.status.opened_downloads"] = "Opened downloads so you can review the current release and installer posture before linking.",
        ["desktop.install_link.status.unable_open_downloads"] = "Unable to open downloads from this host.",
        ["desktop.install_link.status.opened_support"] = "Opened install-aware support so the closure path stays tied to this exact copy while you link it.",
        ["desktop.install_link.status.unable_open_support"] = "Unable to open support from this host.",
        ["desktop.install_link.status.claim_code_required"] = "Paste the claim code from your Hub account first.",
        ["desktop.install_link.status.linking"] = "Linking this installation with Hub...",
        ["desktop.install_link.summary.installation_id"] = "Installation ID: {0}",
        ["desktop.install_link.summary.head"] = "Head: {0}",
        ["desktop.install_link.summary.version"] = "Version: {0}",
        ["desktop.install_link.summary.channel"] = "Channel: {0}",
        ["desktop.install_link.summary.platform"] = "Platform: {0}/{1}",
        ["desktop.install_link.summary.status"] = "Status: {0}",
        ["desktop.install_link.summary.linked_status"] = "Linked. Grant expires {0} UTC.",
        ["desktop.install_link.summary.guest_status"] = "Not linked yet. You can keep using this copy as a guest.",
        ["desktop.install_link.summary.last_claim_attempt"] = "Last claim attempt: {0} UTC.",
        ["desktop.install_link.summary.hub_message"] = "Hub message: {0}",
        ["desktop.install_link.summary.claim_error"] = "Claim error: {0}",
        ["desktop.install_link.summary.next_safe_action_claimed"] = "Next safe action: open the account-aware work route and confirm restore, update, and support follow-through from this claimed install.",
        ["desktop.install_link.summary.next_safe_action_guest"] = "Next safe action: copy the installation id, redeem the Hub claim code, and keep install-aware support open until the grant lands."
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TrustSurfaceStrings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en-us"] = DefaultTrustSurfaceStrings,
            ["de-de"] = DefaultTrustSurfaceStrings,
            ["fr-fr"] = DefaultTrustSurfaceStrings,
            ["ja-jp"] = DefaultTrustSurfaceStrings,
            ["pt-br"] = DefaultTrustSurfaceStrings,
            ["zh-cn"] = DefaultTrustSurfaceStrings
        };

    public static IReadOnlyList<DesktopSupportedLanguage> ShippingLanguages { get; } =
    [
        new("en-us", "English (en-US)"),
        new("de-de", "Deutsch (de-DE)"),
        new("fr-fr", "Francais (fr-FR)"),
        new("ja-jp", "Japanese (ja-JP)"),
        new("pt-br", "Portugues (pt-BR)"),
        new("zh-cn", "Chinese Simplified (zh-CN)")
    ];

    public static string NormalizeOrDefault(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguage;
        }

        string normalized = languageCode.Trim().ToLowerInvariant();
        return ShippingLanguages.Any(language => string.Equals(language.Code, normalized, StringComparison.Ordinal))
            ? normalized
            : DefaultLanguage;
    }

    public static string GetDisplayLabel(string? languageCode)
    {
        string code = NormalizeOrDefault(languageCode);
        return ShippingLanguages
            .First(language => string.Equals(language.Code, code, StringComparison.Ordinal))
            .Label;
    }

    public static string BuildSupportedLanguageSummary()
        => string.Join(", ", ShippingLanguages.Select(language => language.Label));

    public static string GetCurrentLanguage()
        => NormalizeOrDefault(CultureInfo.CurrentUICulture.Name.Replace('_', '-').ToLowerInvariant());

    public static string GetRequiredString(string key, string? languageCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string normalizedLanguage = NormalizeOrDefault(languageCode);
        if (TrustSurfaceStrings.TryGetValue(normalizedLanguage, out IReadOnlyDictionary<string, string>? localized)
            && localized.TryGetValue(key, out string? value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (TrustSurfaceStrings.TryGetValue(DefaultLanguage, out IReadOnlyDictionary<string, string>? fallback)
            && fallback.TryGetValue(key, out value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new KeyNotFoundException($"desktop_localization_key_missing:{key}");
    }

    public static string GetRequiredFormattedString(string key, string? languageCode, params object[] values)
        => string.Format(GetRequiredString(key, languageCode), values);

    public static IReadOnlyList<string> RequiredTrustSurfaceKeys()
        => DefaultTrustSurfaceStrings.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
}
