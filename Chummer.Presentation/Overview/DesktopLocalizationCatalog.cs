using System.Globalization;

namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportedLanguage(
    string Code,
    string Label);

public static class DesktopLocalizationCatalog
{
    public const string DefaultLanguage = "en-us";
    private const string EnglishFallbackMarker = " [en-US fallback]";
    private static string? _currentLanguageOverride;
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
        ["desktop.shell.tool.update_status"] = "Update Status",
        ["desktop.shell.tool.link_copy"] = "Link This Copy",
        ["desktop.shell.tool.open_support"] = "Open Support",
        ["desktop.shell.tool.report_issue"] = "Report Issue",
        ["desktop.shell.tool.settings"] = "Settings",
        ["desktop.shell.tool.load_demo_runner"] = "Load Demo Runner",
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
        ["desktop.shell.feedback.demo_runner_unavailable"] = "State: bundled demo runner is unavailable in this build.",
        ["desktop.shell.feedback.demo_runner_loading"] = "State: loading bundled demo runner from {0}.",
        ["desktop.shell.feedback.no_active_workspace"] = "State: no active workspace to close.",
        ["desktop.shell.feedback.desktop_home_reviewed"] = "State: desktop home reviewed.",
        ["desktop.shell.feedback.campaign_workspace_reviewed"] = "State: campaign workspace reviewed.",
        ["desktop.shell.feedback.update_reviewed"] = "State: update status reviewed.",
        ["desktop.shell.feedback.install_linking_reviewed"] = "State: install linking reviewed.",
        ["desktop.shell.feedback.support_reviewed"] = "State: support reviewed.",
        ["desktop.shell.feedback.report_issue_reviewed"] = "State: report issue reviewed.",
        ["desktop.shell.feedback.settings_reviewed"] = "State: settings reviewed.",
        ["desktop.shell.feedback.install_support_unavailable"] = "State: support follow-through is unavailable on this host.",
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
        ["desktop.home.button.open_my_artifacts"] = "Open My Stuff",
        ["desktop.home.button.open_campaign_artifacts"] = "Open Campaign Stuff",
        ["desktop.home.button.open_published_artifacts"] = "Open Published Stuff",
        ["desktop.home.button.open_build_followthrough"] = "Open Build Follow-through",
        ["desktop.home.button.open_workspace_followthrough"] = "Open Workspace Follow-through",
        ["desktop.home.button.open_update_status"] = "Open Update Status",
        ["desktop.home.button.open_support_center"] = "Open Support Center",
        ["desktop.home.button.open_report_issue"] = "Report Issue",
        ["desktop.home.button.open_settings"] = "Open Settings",
        ["desktop.dialog.action.close"] = "Close",
        ["desktop.dialog.action.save"] = "Save",
        ["desktop.dialog.action.cancel"] = "Cancel",
        ["desktop.dialog.global_settings.title"] = "Global Settings",
        ["desktop.dialog.global_settings.message"] = "Phase-1 desktop language changes apply on restart. Shipping locales: {0}",
        ["desktop.dialog.global_settings.field.ui_scale"] = "UI Scale (%)",
        ["desktop.dialog.global_settings.field.theme"] = "Theme",
        ["desktop.dialog.global_settings.field.language"] = "Language",
        ["desktop.dialog.global_settings.field.compact_mode"] = "Compact Mode",
        ["desktop.dialog.global_settings.notice.updated"] = "Global settings updated.",
        ["desktop.dialog.global_settings.notice.updated_restart"] = "Global settings updated. Restart the desktop head to fully apply {0} across shell chrome, update, and support surfaces.",
        ["desktop.dialog.translator.title"] = "Translator",
        ["desktop.dialog.translator.message"] = "Shipping desktop locale set. Install, update, support, explain, and artifact trust flows should all reach parity for: {0}",
        ["desktop.dialog.translator.field.search"] = "Language Search",
        ["desktop.dialog.translator.field.search_placeholder"] = "filter languages",
        ["desktop.dialog.character_settings.notice.updated"] = "Character settings updated.",
        ["desktop.shell.notice.download_unavailable"] = "Notice: download requested but file save is unavailable on this platform.",
        ["desktop.shell.notice.download_cancelled"] = "Notice: download canceled.",
        ["desktop.shell.notice.download_completed"] = "Notice: downloaded {0}.",
        ["desktop.shell.notice.export_unavailable"] = "Notice: export requested but file save is unavailable on this platform.",
        ["desktop.shell.notice.export_cancelled"] = "Notice: export canceled.",
        ["desktop.shell.notice.export_completed"] = "Notice: exported {0}.",
        ["desktop.shell.notice.print_unavailable"] = "Notice: print preview requested but file save is unavailable on this platform.",
        ["desktop.shell.notice.print_cancelled"] = "Notice: print preview canceled.",
        ["desktop.shell.notice.print_completed"] = "Notice: saved print preview {0}.",
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
        ["desktop.install_link.summary.next_safe_action_guest"] = "Next safe action: copy the installation id, redeem the Hub claim code, and keep install-aware support open until the grant lands.",
        ["desktop.devices.title"] = "Devices and Access",
        ["desktop.devices.heading"] = "Devices, access, and claim follow-through",
        ["desktop.devices.section.current"] = "This desktop copy",
        ["desktop.devices.section.claimed"] = "Claimed devices and restore posture",
        ["desktop.devices.section.claims"] = "Claim codes and recent handoffs",
        ["desktop.devices.section.follow_through"] = "Access follow-through",
        ["desktop.devices.button.copy_claim_code"] = "Copy Claim Code",
        ["desktop.devices.intro.guest"] = "This desktop copy is still guest-only, so devices and access stay quiet until a claim code or signed-in restore packet attaches this install.",
        ["desktop.devices.intro.pending"] = "A pending claim code is already waiting on the account side, so this surface can close the install-linking loop without guessing.",
        ["desktop.devices.intro.claimed_single"] = "This desktop copy is linked, and the flagship desktop can now show install-aware access and restore posture directly.",
        ["desktop.devices.intro.claimed_multi"] = "This desktop copy is one of several claimed installs, and this surface keeps the role, channel, and restore posture visible per device.",
        ["desktop.devices.status.current"] = "Devices and access status: current account-aware projection loaded.",
        ["desktop.devices.status.refresh_failed"] = "Devices and access status: refresh failed, so the last good projection is still shown.",
        ["desktop.devices.status.claim_code_copied"] = "Copied the latest pending claim code to the clipboard.",
        ["desktop.devices.status.no_claim_code"] = "No pending claim code is visible for this account right now.",
        ["desktop.devices.context.current_local"] = "Local install state is loaded directly from this desktop copy.",
        ["desktop.devices.context.current_account_match"] = "Account record: {0} on {1}/{2}, refreshed {3} UTC.",
        ["desktop.devices.context.current_grant"] = "Active grant: {0} until {1} UTC.",
        ["desktop.devices.context.current_unlinked"] = "No signed-in account record for this installation is visible yet.",
        ["desktop.devices.context.claimed_none"] = "No claimed installs are attached to the signed-in restore packet yet.",
        ["desktop.devices.context.claimed_device"] = "Device: {0} · {1} on {2}/{3} ({4})",
        ["desktop.devices.context.claimed_restore"] = "Restore: {0}",
        ["desktop.devices.context.claimed_fallback"] = "Linked copy: {0} · {1} {2} on {3}/{4}.",
        ["desktop.devices.context.claims_none"] = "No pending claim codes or recent download handoffs are visible right now.",
        ["desktop.devices.context.claims_pending"] = "Pending claim code {0} for {1} {2} expires {3} UTC.",
        ["desktop.devices.context.claims_pending_install"] = "Pending claim code {0} is already bound to install {1} and expires {2} UTC.",
        ["desktop.devices.context.claims_local_last"] = "Last local claim code: {0}",
        ["desktop.devices.context.claims_receipt"] = "Recent handoff: {0} · {1} {2} on {3}/{4}, issued {5} UTC.",
        ["desktop.devices.context.access_guest"] = "Next safe action: link this copy before you expect linked devices, access grants, or tracked fix follow-through to stay attached.",
        ["desktop.devices.context.access_claimed"] = "Next safe action: keep devices, support, update, and work follow-through grounded to this claimed install instead of bouncing out to a generic account shelf.",
        ["desktop.devices.context.access_no_grants"] = "No active installation grants are visible for this signed-in account yet.",
        ["desktop.devices.context.access_grant"] = "Grant {0} for {1} stays {2} until {3} UTC.",
        ["desktop.update.title"] = "Update status",
        ["desktop.update.heading"] = "Update status and release posture",
        ["desktop.update.section.current"] = "Current release posture",
        ["desktop.update.section.follow_through"] = "Update follow-through",
        ["desktop.update.section.install"] = "Install context",
        ["desktop.update.button.check_now"] = "Check for Updates",
        ["desktop.update.button.refresh"] = "Refresh",
        ["desktop.update.intro.disabled"] = "This desktop head cannot promise self-update yet because update truth is not configured locally.",
        ["desktop.update.intro.available"] = "A promoted update is available for this install. Review the release posture before you move back into campaign work.",
        ["desktop.update.intro.attention"] = "This release posture needs review before you trust the current install.",
        ["desktop.update.intro.never_checked"] = "Local update truth has not been seeded yet for this install.",
        ["desktop.update.intro.current"] = "This install is current enough to continue, with registry-backed release posture available locally.",
        ["desktop.update.checking"] = "Checking for updates against the current registry-backed manifest.",
        ["desktop.update.checked"] = "Update check finished. Result: {0}.",
        ["desktop.update.apply_scheduled"] = "A compatible update was staged. Chummer is closing so the update helper can apply it.",
        ["desktop.update.pending_update"] = "Pending update: {0} on channel {1}.",
        ["desktop.update.no_pending_update"] = "No staged update is waiting locally.",
        ["desktop.update.last_checked"] = "Last checked: {0} UTC.",
        ["desktop.update.last_launch_attempt"] = "Last update launch attempt: {0} UTC.",
        ["desktop.update.rollback_window"] = "Rollback window: {0} UTC -> {1} UTC.",
        ["desktop.update.manifest_location"] = "Manifest location: {0}",
        ["desktop.update.updates_enabled"] = "Updates enabled: {0}",
        ["desktop.support.title"] = "Support",
        ["desktop.support.heading"] = "Support and closure",
        ["desktop.support.section.case"] = "Tracked case and closure",
        ["desktop.support.section.release"] = "Release and install context",
        ["desktop.support.section.follow_through"] = "Support follow-through",
        ["desktop.support.button.refresh"] = "Refresh",
        ["desktop.support.intro.guest"] = "This desktop copy is still guest-only, so support stays bounded to generic help until the install is linked.",
        ["desktop.support.intro.quiet"] = "No tracked case is attached right now, but this support surface is ready to expose install-aware closure when a real case exists.",
        ["desktop.support.intro.action_needed"] = "A tracked case needs follow-through before closure is honest on this install.",
        ["desktop.support.intro.tracked"] = "A tracked support case is attached to this install and can be followed through from the flagship desktop.",
        ["desktop.support.status.current"] = "Support status: current support projection loaded.",
        ["desktop.support.status.refresh_failed"] = "Support status: refresh failed, so the last good projection is still shown.",
        ["desktop.support.context.release_status"] = "Release status: {0}",
        ["desktop.support.context.recommended_action"] = "Recommended action: {0}",
        ["desktop.support.context.known_issues"] = "Known issues: {0}",
        ["desktop.support.context.fix_availability"] = "Fix availability: {0}",
        ["desktop.support.context.last_error"] = "Last update error: {0}",
        ["desktop.support.follow_through.claimed"] = "Support lane is grounded to a claimed install and can route directly into update, case, and closure follow-through.",
        ["desktop.support.follow_through.guest"] = "Support lane is currently bounded to guest posture until this copy is linked.",
        ["desktop.support.follow_through.attention"] = "Reporter follow-through is still needed before this case can count as honest closure.",
        ["desktop.support_case.title"] = "Tracked support case",
        ["desktop.support_case.heading"] = "Tracked support case and closure status",
        ["desktop.support_case.section.summary"] = "Case summary and trust posture",
        ["desktop.support_case.section.timeline"] = "Timeline and evidence",
        ["desktop.support_case.section.follow_through"] = "Desktop follow-through",
        ["desktop.support_case.button.refresh"] = "Refresh",
        ["desktop.support_case.button.open_attachment"] = "Open Attachment",
        ["desktop.support_case.intro.preview"] = "This is a bounded tracked-case preview used to live-verify the native support closure surface when no signed-in support case is reachable from this desktop.",
        ["desktop.support_case.intro.fallback"] = "The full tracked-case detail is unavailable right now, so this desktop is showing the strongest safe local support digest instead of dropping you out of the flagship client.",
        ["desktop.support_case.intro.action_needed"] = "This tracked case still needs install-aware follow-through before closure is honest for this desktop copy.",
        ["desktop.support_case.intro.current"] = "This tracked case is grounded to the flagship desktop and can explain why it is still open, what changed, and what is safe to do next.",
        ["desktop.support_case.status.current"] = "Tracked case status: current detail and desktop-safe follow-through are loaded.",
        ["desktop.support_case.status.preview"] = "Tracked case status: synthetic preview loaded for flagship desktop smoke verification.",
        ["desktop.support_case.status.case_unavailable"] = "Tracked case status: the detailed support record is unavailable, so the bounded local digest is still shown.",
        ["desktop.support_case.status.refresh_failed"] = "Tracked case status: refresh failed, so the last good desktop projection is still shown.",
        ["desktop.support_case.context.case_id"] = "Case ID: {0}",
        ["desktop.support_case.context.kind"] = "Kind: {0}",
        ["desktop.support_case.context.stage"] = "Stage: {0} ({1})",
        ["desktop.support_case.context.updated"] = "Updated: {0}",
        ["desktop.support_case.context.source"] = "Source: {0}",
        ["desktop.support_case.context.install_readiness"] = "Linked install state: {0}",
        ["desktop.support_case.context.fixed_release"] = "Fixed release: {0}",
        ["desktop.support_case.context.affected_install"] = "Affected install: {0}",
        ["desktop.support_case.context.release_progress"] = "Release progress: {0}",
        ["desktop.support_case.context.verification"] = "Verification: {0}",
        ["desktop.support_case.context.follow_up"] = "Follow-up: {0}",
        ["desktop.support_case.context.detail"] = "Detail: {0}",
        ["desktop.support_case.context.timeline_entry"] = "{0} UTC | {1} | {2}",
        ["desktop.support_case.context.timeline_actor"] = "Actor: {0}",
        ["desktop.support_case.context.timeline_none"] = "No timeline events are attached to this tracked case yet.",
        ["desktop.support_case.context.timeline_fallback"] = "Timeline detail is currently bounded to the local tracked-case digest until the live support record is reachable again.",
        ["desktop.support_case.context.attachment"] = "Attachment: {0} | {1} | uploaded {2} UTC",
        ["desktop.support_case.follow_through.current"] = "Desktop follow-through is current enough to continue without leaving the flagship client.",
        ["desktop.support_case.follow_through.attention"] = "Follow-through is still needed on this same tracked case before closure is honest.",
        ["desktop.support_case.follow_through.link_install"] = "Next safe action: open Devices and access and relink or reclaim the affected install before you trust fix closure here.",
        ["desktop.support_case.follow_through.update_install"] = "Next safe action: open Update status and bring this linked install onto the reporter-ready release before you verify the fix.",
        ["desktop.support_case.follow_through.verify"] = "This linked install is already carrying the reporter-ready fix posture. Use the signed-in support lane to record final confirmation without losing case continuity.",
        ["desktop.crash.title"] = "Crash recovery",
        ["desktop.crash.heading"] = "Crash recovery and local evidence",
        ["desktop.crash.section.summary"] = "Crash summary and evidence",
        ["desktop.crash.section.recovery"] = "Recovery and follow-through",
        ["desktop.crash.intro.current"] = "Chummer saved a local crash report from the previous run. This surface keeps the full diagnostics bundle local while retrying a small redacted crash envelope to Hub when transport is available.",
        ["desktop.crash.intro.preview"] = "This is a synthetic crash-recovery preview used to verify the flagship desktop surface. A real pending crash would appear here on the next launch after failure.",
        ["desktop.crash.context.head"] = "Head: {0}",
        ["desktop.crash.context.version"] = "Version: {0}",
        ["desktop.crash.context.captured"] = "Captured: {0} UTC",
        ["desktop.crash.context.os"] = "OS: {0}",
        ["desktop.crash.context.arch"] = "Architecture: {0}",
        ["desktop.crash.recovery.private_local"] = "Diagnostics stay local by default. The automatic send path is bounded to a redacted crash envelope and does not replace install-aware support follow-through.",
        ["desktop.crash.recovery.retry"] = "If the last automatic send failed, retry from here, keep the report local only, or route into support and bug reporting without losing the local evidence bundle.",
        ["desktop.crash.recovery.preview"] = "Preview mode skips hosted send and exists only to verify that the native crash-recovery surface can open cleanly on this desktop head.",
        ["desktop.crash.recovery.incident"] = "Hosted incident: {0}",
        ["desktop.crash.recovery.last_error"] = "Last automatic-send error: {0}",
        ["desktop.crash.button.open_folder"] = "Open Folder",
        ["desktop.crash.button.open_bundle"] = "Open Bundle",
        ["desktop.crash.button.copy_summary"] = "Copy Summary",
        ["desktop.crash.button.retry_send"] = "Retry Send",
        ["desktop.crash.button.keep_local_only"] = "Keep Local Only",
        ["desktop.crash.status.current"] = "Crash status: local evidence is available and the recovery lane is ready.",
        ["desktop.crash.status.preview"] = "Crash status: preview surface loaded for live verification without a real pending crash.",
        ["desktop.crash.status.already_submitted"] = "Crash status: the redacted crash envelope already reached Hub as {0}.",
        ["desktop.crash.status.previous_send_failed"] = "Crash status: previous automatic send failed: {0}",
        ["desktop.crash.status.sending"] = "Crash status: sending a redacted crash envelope to Hub.",
        ["desktop.crash.status.folder_opened"] = "Crash status: opened the local crash-report folder.",
        ["desktop.crash.status.bundle_opened"] = "Crash status: opened the local diagnostics bundle.",
        ["desktop.crash.status.summary_copied"] = "Crash status: copied the crash summary to the clipboard.",
        ["desktop.crash.status.clipboard_unavailable"] = "Crash status: clipboard access is unavailable on this host.",
        ["desktop.crash.status.unable_open_path"] = "Crash status: unable to open {0}.",
        ["desktop.crash.status.kept_local_only"] = "Crash status: kept the report local only and stopped future automatic reminders for this crash.",
        ["desktop.report.title"] = "Report issue",
        ["desktop.report.heading"] = "Report a bug or share feedback",
        ["desktop.report.intro"] = "This native desktop surface keeps bug and feedback drafting in-app while preserving install-aware context for support closure.",
        ["desktop.report.private_split"] = "Private support intake is the default route here. Public-safe issue projection can happen later without exposing local diagnostics by default.",
        ["desktop.report.section.context"] = "Auto-filled desktop context",
        ["desktop.report.section.bug"] = "Structured bug report",
        ["desktop.report.section.feedback"] = "Lightweight feedback",
        ["desktop.report.context.manifest"] = "Manifest: {0}",
        ["desktop.report.context.supportability"] = "Supportability: {0}",
        ["desktop.report.bug.intro"] = "Use this lane for reproducible behavior. Expected versus actual detail and repro steps help support and triage turn a report into honest closure.",
        ["desktop.report.bug.title_label"] = "Title",
        ["desktop.report.bug.title_watermark"] = "Short summary of the bug",
        ["desktop.report.bug.expected_label"] = "Expected behavior",
        ["desktop.report.bug.expected_watermark"] = "What should have happened?",
        ["desktop.report.bug.actual_label"] = "Actual behavior",
        ["desktop.report.bug.actual_watermark"] = "What happened instead?",
        ["desktop.report.bug.repro_label"] = "Repro steps",
        ["desktop.report.bug.repro_watermark"] = "How can support reproduce this?",
        ["desktop.report.bug.evidence_label"] = "Evidence or screenshot note",
        ["desktop.report.bug.evidence_watermark"] = "Optional: screenshot path, attachment note, or crash id",
        ["desktop.report.feedback.intro"] = "Use this lane for ideas, confusion, or low-friction product signals without the burden of a full bug report.",
        ["desktop.report.feedback.summary_label"] = "Feedback summary",
        ["desktop.report.feedback.summary_watermark"] = "What feels off, useful, or missing?",
        ["desktop.report.feedback.detail_label"] = "More detail",
        ["desktop.report.feedback.detail_watermark"] = "Optional detail for triage, design review, or follow-through",
        ["desktop.report.button.open_bug"] = "Open Private Bug Draft",
        ["desktop.report.button.copy_bug"] = "Copy Bug Draft",
        ["desktop.report.button.open_feedback"] = "Open Private Feedback Draft",
        ["desktop.report.button.copy_feedback"] = "Copy Feedback Draft",
        ["desktop.report.status.ready"] = "Report status: draft locally, then route private intake or copy the draft if the host cannot open the portal.",
        ["desktop.report.status.bug_opened"] = "Report status: opened the private support draft for this bug report.",
        ["desktop.report.status.feedback_opened"] = "Report status: opened the private support draft for this feedback item.",
        ["desktop.report.status.bug_copied"] = "Report status: copied the bug-report draft to the clipboard.",
        ["desktop.report.status.feedback_copied"] = "Report status: copied the feedback draft to the clipboard.",
        ["desktop.report.status.bug_copied_fallback"] = "Report status: support portal unavailable on this host, so the bug-report draft was copied instead.",
        ["desktop.report.status.feedback_copied_fallback"] = "Report status: support portal unavailable on this host, so the feedback draft was copied instead.",
        ["desktop.report.status.clipboard_unavailable"] = "Report status: clipboard access is unavailable on this host.",
        ["desktop.report.status.portal_unavailable"] = "Report status: unable to open the private support route or copy the local draft on this host."
    };

    private static IReadOnlyDictionary<string, string> BuildLocaleTrustSurfaceStrings(string languageCode)
    {
        var localized = new Dictionary<string, string>(DefaultTrustSurfaceStrings.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in DefaultTrustSurfaceStrings)
        {
            localized[pair.Key] = pair.Value;
        }

        if (string.Equals(languageCode, "de-de", StringComparison.Ordinal))
        {
            localized["desktop.shell.window_title"] = "Chummer Desktop";
            localized["desktop.shell.banner"] = "Chummer Desktop";
            localized["desktop.shell.menu.file"] = "Datei";
            localized["desktop.shell.menu.edit"] = "Bearbeiten";
            localized["desktop.shell.menu.special"] = "Besonderes";
            localized["desktop.shell.menu.tools"] = "Werkzeuge";
            localized["desktop.shell.menu.windows"] = "Fenster";
            localized["desktop.shell.menu.help"] = "Hilfe";
            localized["desktop.shell.tool.desktop_home"] = "Desktop Start";
            localized["desktop.shell.tool.campaign_workspace"] = "Kampagnen-Arbeitsbereich";
            localized["desktop.shell.tool.update_status"] = "Update-Status";
            localized["desktop.shell.tool.link_copy"] = "Kopie verknüpfen";
            localized["desktop.shell.tool.open_support"] = "Support öffnen";
            localized["desktop.shell.tool.report_issue"] = "Fehler melden";
            localized["desktop.shell.tool.settings"] = "Einstellungen";
            localized["desktop.shell.tool.import_character_file"] = "Charakterdatei importieren";
            localized["desktop.shell.tool.import_raw_xml"] = "Raw-XML importieren";
            localized["desktop.shell.tool.save_workspace"] = "Arbeitsbereich speichern";
            localized["desktop.shell.tool.close_active_workspace"] = "Aktiven Arbeitsbereich schließen";
            localized["desktop.shell.tool.status_idle"] = "Status: inaktiv";
            localized["desktop.shell.state.value.ready"] = "bereit";
            localized["desktop.support.title"] = "Support";
            localized["desktop.home.title"] = "Desktop-Start-Cockpit";
            localized["desktop.home.section.install_support"] = "Installation und Support";
            localized["desktop.home.section.update_posture"] = "Update-Posture";
        }

        if (string.Equals(languageCode, "fr-fr", StringComparison.Ordinal))
        {
            localized["desktop.shell.window_title"] = "Chummer Bureau";
            localized["desktop.shell.banner"] = "Chummer Desktop";
            localized["desktop.shell.menu.file"] = "Fichier";
            localized["desktop.shell.menu.edit"] = "Éditer";
            localized["desktop.shell.menu.tools"] = "Outils";
            localized["desktop.shell.menu.help"] = "Aide";
            localized["desktop.shell.tool.desktop_home"] = "Accueil";
            localized["desktop.shell.tool.campaign_workspace"] = "Espace de campagne";
            localized["desktop.shell.tool.update_status"] = "État de la mise à jour";
            localized["desktop.shell.tool.link_copy"] = "Lier cette copie";
            localized["desktop.shell.tool.open_support"] = "Ouvrir le support";
            localized["desktop.shell.tool.report_issue"] = "Signaler un problème";
            localized["desktop.shell.tool.settings"] = "Paramètres";
            localized["desktop.support.title"] = "Support";
            localized["desktop.home.title"] = "Cockpit principal";
            localized["desktop.home.section.install_support"] = "Installation et support";
            localized["desktop.home.section.update_posture"] = "Posture de mise à jour";
        }

        if (string.Equals(languageCode, "ja-jp", StringComparison.Ordinal))
        {
            localized["desktop.shell.window_title"] = "Chummer デスクトップ";
            localized["desktop.shell.banner"] = "Chummer Desktop";
            localized["desktop.shell.menu.file"] = "ファイル";
            localized["desktop.shell.menu.edit"] = "編集";
            localized["desktop.shell.menu.tools"] = "ツール";
            localized["desktop.shell.menu.help"] = "ヘルプ";
            localized["desktop.shell.tool.desktop_home"] = "デスクトップホーム";
            localized["desktop.shell.tool.campaign_workspace"] = "キャンペーンワークスペース";
            localized["desktop.shell.tool.update_status"] = "更新ステータス";
            localized["desktop.shell.tool.link_copy"] = "このコピーをリンク";
            localized["desktop.shell.tool.open_support"] = "サポートを開く";
            localized["desktop.shell.tool.report_issue"] = "問題を報告";
            localized["desktop.shell.tool.settings"] = "設定";
            localized["desktop.support.title"] = "サポート";
            localized["desktop.home.title"] = "デスクトップホーム";
            localized["desktop.home.section.install_support"] = "インストールとサポート";
            localized["desktop.home.section.update_posture"] = "更新の姿勢";
        }

        if (string.Equals(languageCode, "pt-br", StringComparison.Ordinal))
        {
            localized["desktop.shell.window_title"] = "Chummer Desktop";
            localized["desktop.shell.banner"] = "Chummer Desktop";
            localized["desktop.shell.menu.file"] = "Arquivo";
            localized["desktop.shell.menu.edit"] = "Editar";
            localized["desktop.shell.menu.tools"] = "Ferramentas";
            localized["desktop.shell.menu.help"] = "Ajuda";
            localized["desktop.shell.tool.desktop_home"] = "Início";
            localized["desktop.shell.tool.campaign_workspace"] = "Espaço de campanha";
            localized["desktop.shell.tool.update_status"] = "Status da atualização";
            localized["desktop.shell.tool.link_copy"] = "Vincular esta cópia";
            localized["desktop.shell.tool.open_support"] = "Abrir suporte";
            localized["desktop.shell.tool.report_issue"] = "Reportar problema";
            localized["desktop.shell.tool.settings"] = "Configurações";
            localized["desktop.support.title"] = "Suporte";
            localized["desktop.home.title"] = "Painel inicial";
            localized["desktop.home.section.install_support"] = "Instalação e suporte";
            localized["desktop.home.section.update_posture"] = "Postura de atualização";
        }

        if (string.Equals(languageCode, "zh-cn", StringComparison.Ordinal))
        {
            localized["desktop.shell.window_title"] = "Chummer 桌面";
            localized["desktop.shell.banner"] = "Chummer 桌面";
            localized["desktop.shell.menu.file"] = "文件";
            localized["desktop.shell.menu.edit"] = "编辑";
            localized["desktop.shell.menu.tools"] = "工具";
            localized["desktop.shell.menu.help"] = "帮助";
            localized["desktop.shell.tool.desktop_home"] = "桌面首页";
            localized["desktop.shell.tool.campaign_workspace"] = "战役工作区";
            localized["desktop.shell.tool.update_status"] = "更新状态";
            localized["desktop.shell.tool.link_copy"] = "绑定此副本";
            localized["desktop.shell.tool.open_support"] = "打开支持";
            localized["desktop.shell.tool.report_issue"] = "报告问题";
            localized["desktop.shell.tool.settings"] = "设置";
            localized["desktop.support.title"] = "支持";
            localized["desktop.home.title"] = "桌面控制台";
            localized["desktop.home.section.install_support"] = "安装与支持";
            localized["desktop.home.section.update_posture"] = "更新状态";
        }

        return localized;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TrustSurfaceStrings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en-us"] = DefaultTrustSurfaceStrings,
            ["de-de"] = BuildLocaleTrustSurfaceStrings("de-de"),
            ["fr-fr"] = BuildLocaleTrustSurfaceStrings("fr-fr"),
            ["ja-jp"] = BuildLocaleTrustSurfaceStrings("ja-jp"),
            ["pt-br"] = BuildLocaleTrustSurfaceStrings("pt-br"),
            ["zh-cn"] = BuildLocaleTrustSurfaceStrings("zh-cn")
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

    public static void SetCurrentLanguageOverride(string? languageCode)
    {
        _currentLanguageOverride = string.IsNullOrWhiteSpace(languageCode)
            ? null
            : NormalizeOrDefault(languageCode);
    }

    public static string GetCurrentLanguage()
    {
        if (!string.IsNullOrWhiteSpace(_currentLanguageOverride))
        {
            return _currentLanguageOverride;
        }

        return NormalizeOrDefault(CultureInfo.CurrentUICulture.Name.Replace('_', '-').ToLowerInvariant());
    }

    public static string GetRequiredString(string key, string? languageCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string normalizedLanguage = NormalizeOrDefault(languageCode);
        if (TryGetLocalizedValue(normalizedLanguage, key, out string? value))
        {
            return value;
        }

        if (TryGetLocalizedValue(DefaultLanguage, key, out value))
        {
            return normalizedLanguage == DefaultLanguage ? value : string.Concat(value, EnglishFallbackMarker, "[", normalizedLanguage, "]");
        }

        throw new KeyNotFoundException($"desktop_localization_key_missing:{key}");
    }

    private static bool TryGetLocalizedValue(string languageCode, string key, out string? value)
    {
        if (TrustSurfaceStrings.TryGetValue(languageCode, out IReadOnlyDictionary<string, string>? strings)
            && strings.TryGetValue(key, out value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = null;
        return false;
    }

    public static string GetRequiredFormattedString(string key, string? languageCode, params object[] values)
        => string.Format(GetRequiredString(key, languageCode), values);

    public static IReadOnlyList<string> RequiredTrustSurfaceKeys()
        => DefaultTrustSurfaceStrings.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
}
