namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportedLanguage(
    string Code,
    string Label);

public static class DesktopLocalizationCatalog
{
    public const string DefaultLanguage = "en-us";
    private static readonly IReadOnlyDictionary<string, string> DefaultTrustSurfaceStrings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["desktop.home.title"] = "Desktop home cockpit",
        ["desktop.home.section.install_support"] = "Install and support",
        ["desktop.home.section.update_posture"] = "Update posture",
        ["desktop.home.section.campaign_return"] = "Campaign return and restore",
        ["desktop.home.section.support_closure"] = "Support closure and fix notices",
        ["desktop.home.section.build_explain"] = "Build and explain next",
        ["desktop.home.section.language_trust"] = "Language and trust surfaces",
        ["desktop.home.section.recent_workspaces"] = "Recent workspaces",
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
