using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopInstallLinkingRuntimeTests
{
    private static readonly string[] LongFormSeparate = ["--install-claim-code", "claim-123"];
    private static readonly string[] SlashSeparate = ["/install-claim-code", "claim-123"];
    private static readonly string[] LongFormEquals = ["--install-claim-code=claim-123"];
    private static readonly string[] LongFormColon = ["--install-claim-code:claim-123"];
    private static readonly string[] SlashEquals = ["/install-claim-code=claim-123"];
    private static readonly string[] SlashColon = ["/install-claim-code:claim-123"];
    private static readonly string[] CallbackSwitchSeparate = ["--install-link-callback", "chummer://install-link?claimCode=claim-789"];
    private static readonly string[] CallbackSwitchEquals = ["--install-link-callback=https://chummer.run/downloads/install/callback?claim=claim-789"];
    private static readonly string[] CallbackDirectUri = ["chummer://install-link?claim_code=claim-789"];
    private static readonly string[] GrantCallbackSwitchSeparate = ["--install-link-callback", "chummer://install-link?code=grant-callback-789"];
    private static readonly string[] GrantCallbackSwitchEquals = ["--install-link-callback=https://chummer.run/downloads/install/callback?callbackCode=grant-callback-789"];
    private static readonly string[] GrantCallbackDirectUri = ["chummer://install-link?installLinkCode=grant-callback-789"];

    [TestMethod]
    public void BuildSupportPortalRelativePathForInstall_includes_install_prefill_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForInstall(CreateState());

        StringAssert.Contains(path, "/contact?", StringComparison.Ordinal);
        StringAssert.Contains(path, "kind=install_help", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "releaseChannel=preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "headId=avalonia", StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Restore posture: review claimed-install entitlement, stale-state visibility, and conflict choices before restoring workspace continuity."), StringComparison.Ordinal);
        StringAssert.Contains(path, "Restore%20posture%3A%20review%20claimed-install%20entitlement%2C%20stale-state%20visibility%2C%20and%20conflict%20choices%20before%20restoring%20workspace%20continuity.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildAccountPortalRelativePathForInstall_includes_browser_callback_hints()
    {
        string path = DesktopInstallLinkingRuntime.BuildAccountPortalRelativePathForInstall(CreateState() with
        {
            Status = "guest",
            GrantId = null,
            GrantToken = null,
            GrantIssuedAtUtc = null,
            GrantExpiresAtUtc = null
        });

        StringAssert.Contains(path, "/account/access/install-link?", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkMode=browser_callback", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkTransport=grant_callback", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkCallbackUri=chummer%3A%2F%2Finstall-link", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForUpdate(
            CreateState(),
            new DesktopUpdateClientStatus(
                HeadId: "avalonia",
                InstalledVersion: "6.0.1-preview",
                ChannelId: "preview",
                Platform: "linux",
                Arch: "x64",
                UpdatesEnabled: true,
                AutoApply: false,
                ManifestLocation: "http://127.0.0.1:8091/downloads/manifest.json",
                LastCheckedAtUtc: DateTimeOffset.Parse("2026-03-28T14:00:00+00:00"),
                LastManifestVersion: "6.0.2-preview",
                LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-03-28T13:55:00+00:00"),
                LastError: "Manifest signature mismatch.",
                Status: "attention_required",
                RecommendedAction: "Review the promoted preview and route support before retrying.",
                RolloutState: "local_docker_preview",
                SupportabilityState: "local_docker_proven",
                SupportabilitySummary: "Local proof passed for install, build, and support closure.",
                KnownIssueSummary: "Portable artifact is still preview-only on this channel.",
                FixAvailabilitySummary: "Only verify fixes after this install can see the promoted archive.",
                ProofStatus: "passed",
                ProofGeneratedAtUtc: DateTimeOffset.Parse("2026-03-28T13:56:00+00:00")));

        StringAssert.Contains(path, "title=Desktop%20update%20posture%20needs%20review%20for%20avalonia", StringComparison.Ordinal);
        StringAssert.Contains(path, "Manifest%20signature%20mismatch.", StringComparison.Ordinal);
        StringAssert.Contains(path, "applicationVersion=6.0.1-preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "Supportability%3A%20local_docker_proven", StringComparison.Ordinal);
        StringAssert.Contains(path, "Local%20release%20proof%3A%20passed", StringComparison.Ordinal);
        StringAssert.Contains(path, "Fix%20availability%3A%20Only%20verify%20fixes%20after%20this%20install%20can%20see%20the%20promoted%20archive.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForWorkspace_includes_workspace_follow_through_context()
    {
        WorkspaceListItem workspace = new(
            Id: new CharacterWorkspaceId("workspace-redmond"),
            Summary: new CharacterFileSummary(
                Name: "Redmond Edge",
                Alias: "Edge",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "SR6",
                AppVersion: "6.0.1-preview",
                Karma: 24,
                Nuyen: 18000,
                Created: true),
            LastUpdatedUtc: DateTimeOffset.Parse("2026-03-28T14:10:00+00:00"),
            RulesetId: "sr6.preview.v1",
            HasSavedWorkspace: true);

        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForWorkspace(CreateState(), workspace);

        StringAssert.Contains(path, "kind=bug_report", StringComparison.Ordinal);
        StringAssert.Contains(path, "Workspace%20follow-through%20needs%20help%20for%20Redmond%20Edge", StringComparison.Ordinal);
        StringAssert.Contains(path, "workspace-redmond", StringComparison.Ordinal);
        StringAssert.Contains(path, "sr6.preview.v1", StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Restore posture: review workspace continuation, stale-state visibility, and conflict choices before replacing local work."), StringComparison.Ordinal);
        StringAssert.Contains(path, "Restore%20posture%3A%20review%20workspace%20continuation%2C%20stale-state%20visibility%2C%20and%20conflict%20choices%20before%20replacing%20local%20work.", StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Stale-state visibility: keep the local workspace visible until support confirms the current continuity packet."), StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Conflict choices: keep local work, save local work, or review Campaign Workspace before accepting restore replacement."), StringComparison.Ordinal);
    }

    [TestMethod]
    public void DesktopStartupCompanionRuntime_CreateProjection_defaults_to_text_only_voice_prompt()
    {
        DesktopStartupCompanionProjection projection = DesktopStartupCompanionRuntime.CreateProjection(CreateState());

        Assert.AreEqual("You made it. If you said something, I couldn't hear you.", projection.Headline);
        Assert.AreEqual("Hard boundary: no cross-app observation", projection.BoundaryNote);
        Assert.AreEqual("Voice mode is off. Default posture is text-only until you opt in.", projection.VoiceStatus);
        Assert.AreEqual("Enable voice mode", projection.PrimaryActionLabel);
        Assert.AreEqual("Keep text only", projection.SecondaryActionLabel);
        Assert.IsFalse(projection.VoiceModeEnabled);
        Assert.IsFalse(projection.IsMacBootstrapGremlin);
    }

    [TestMethod]
    public void DesktopStartupCompanionRuntime_CreateProjection_marks_macos_bootstrap_route()
    {
        DesktopStartupCompanionProjection projection = DesktopStartupCompanionRuntime.CreateProjection(
            CreateState() with
            {
                Platform = "macos"
            },
            voiceModeEnabled: true);

        Assert.IsTrue(projection.IsMacBootstrapGremlin);
        StringAssert.Contains(projection.Body, "Mac bootstrap gremlin", StringComparison.Ordinal);
        StringAssert.Contains(projection.VoiceStatus, "Voice mode is on", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForBugReport_includes_structured_bug_fields_and_release_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForBugReport(
            CreateState(),
            CreateUpdateStatus(),
            title: "Armor mod save fails",
            expectedBehavior: "Saving the runner should preserve the armor mod selection.",
            actualBehavior: "The mod selection disappears after save and reopen.",
            reproSteps: "1. Open armor.\n2. Add modification.\n3. Save and reopen.",
            evidenceNote: "Screenshot available on request.");

        StringAssert.Contains(path, "kind=bug_report", StringComparison.Ordinal);
        StringAssert.Contains(path, "title=Armor%20mod%20save%20fails", StringComparison.Ordinal);
        StringAssert.Contains(path, "Expected%3A%20Saving%20the%20runner%20should%20preserve%20the%20armor%20mod%20selection.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Actual%3A%20The%20mod%20selection%20disappears%20after%20save%20and%20reopen.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Evidence%3A%20Screenshot%20available%20on%20request.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Release%20status%3A%20attention_required", StringComparison.Ordinal);
        StringAssert.Contains(path, "Known%20issues%3A%20Portable%20artifact%20is%20still%20preview-only%20on%20this%20channel.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForFeedback_includes_feedback_fields_and_install_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForFeedback(
            CreateState(),
            CreateUpdateStatus(),
            summary: "Campaign workspace should remember filters",
            detail: "The current filter resets every time I reopen the workspace.");

        StringAssert.Contains(path, "kind=feedback", StringComparison.Ordinal);
        StringAssert.Contains(path, "Desktop%20feedback%3A%20Campaign%20workspace%20should%20remember%20filters", StringComparison.Ordinal);
        StringAssert.Contains(path, "Feedback%3A%20Campaign%20workspace%20should%20remember%20filters", StringComparison.Ordinal);
        StringAssert.Contains(path, "Detail%3A%20The%20current%20filter%20resets%20every%20time%20I%20reopen%20the%20workspace.", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "applicationVersion=6.0.1-preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "Recommended%20action%3A%20Review%20the%20promoted%20preview%20and%20route%20support%20before%20retrying.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_installer_claim_code()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-linking-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };
            string pendingPath = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "windows",
                "x64",
                "pending-claim-code.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, "claim-123", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should remain available for installer handoff coverage.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIM123", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_installer_claim_code_from_legacy_path()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-linking-legacy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            string legacyStateRoot = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "win",
                "x64",
                "pending-claim-code.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyStateRoot)!);
            File.WriteAllText(legacyStateRoot, "claim-legacy", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should support legacy pending-claim paths.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIMLEGACY", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_installer_switch_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should remain available for installer handoff coverage.");

            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormSeparate, state]),
                "The long form with separate value should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashSeparate, state]),
                "The legacy slash form with separate value should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormEquals, state]),
                "The equals form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormColon, state]),
                "The colon form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashEquals, state]),
                "The legacy slash-equals form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashColon, state]),
                "The legacy slash-colon form should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_install_link_callback_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", "https://chummer.run/downloads/install/callback?installClaimCode=claim-789");

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should accept callback-style handoff coverage.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [Array.Empty<string>(), state]), "The callback environment variable should parse.");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackSwitchSeparate, state]), "The callback switch with separate value should parse.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackSwitchEquals, state]), "The callback switch equals form should parse.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackDirectUri, state]), "A direct callback URI argument should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_install_link_callback_file()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-link-callback-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };
            string pendingPath = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "windows",
                "x64",
                "pending-install-link-callback.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, "chummer://install-link?claimCode=claim-456", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should accept pending callback handoff coverage.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIM456", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupBrowserCallbackExtraction_reads_install_link_callback_code_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", "https://chummer.run/downloads/install/callback?code=grant-callback-789");

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupBrowserCallbackCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The browser callback extractor should remain available for install-link handoff coverage.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [Array.Empty<string>(), state]), "The callback environment variable should parse.");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackSwitchSeparate, state]), "The callback switch with separate value should parse.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackSwitchEquals, state]), "The callback switch equals form should parse.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackDirectUri, state]), "A direct callback URI argument should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
        }
    }

    [TestMethod]
    public void LoadOrCreateState_persists_private_key_outside_state_json_on_windows()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-state-store-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);

            DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            string platform = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsMacOS()
                    ? "macos"
                    : OperatingSystem.IsLinux()
                        ? "linux"
                        : "unknown";
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
            };

            string installRoot = Path.Combine(tempRoot, "Chummer6", "install-linking", "avalonia", platform, arch);
            string statePath = Path.Combine(installRoot, "state.json");
            string protectedKeyPath = Path.Combine(installRoot, "private-key.protected");
            Assert.IsTrue(File.Exists(statePath), "Install-linking state should be persisted after the first load.");

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath));
            string? persistedPrivateKey = document.RootElement.GetProperty("privateKey").GetString();
            if (OperatingSystem.IsWindows())
            {
                Assert.IsTrue(string.IsNullOrWhiteSpace(persistedPrivateKey), "Windows state.json should not persist the install-link private key in plaintext.");
                Assert.IsTrue(File.Exists(protectedKeyPath), "Windows installs should persist the private key in the DPAPI-backed sidecar.");
            }
            else
            {
                Assert.AreEqual(state.PrivateKey, persistedPrivateKey, "Non-Windows installs should continue to persist the key inline until an OS-backed store exists.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static DesktopInstallLinkingState CreateState()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-03-28T14:00:00+00:00");
        return new DesktopInstallLinkingState(
            InstallationId: "ins-avalonia-1",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 3,
            LastStartedAtUtc: now,
            ClaimedAtUtc: now,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: "CLAIM1",
            LastClaimMessage: "This copy is now linked to your Hub account.",
            LastClaimError: null,
            LastClaimAttemptUtc: now,
            GrantId: "grant-1",
            GrantToken: "token-1",
            GrantIssuedAtUtc: now,
            GrantExpiresAtUtc: now.AddDays(30),
            UserId: "user-1",
            SubjectId: "subject-1");
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus()
        => new(
            HeadId: "avalonia",
            InstalledVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "http://127.0.0.1:8091/downloads/manifest.json",
            LastCheckedAtUtc: DateTimeOffset.Parse("2026-03-28T14:00:00+00:00"),
            LastManifestVersion: "6.0.2-preview",
            LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-03-28T13:55:00+00:00"),
            LastError: "Manifest signature mismatch.",
            Status: "attention_required",
            RecommendedAction: "Review the promoted preview and route support before retrying.",
            RolloutState: "local_docker_preview",
            SupportabilityState: "local_docker_proven",
            SupportabilitySummary: "Local proof passed for install, build, and support closure.",
            KnownIssueSummary: "Portable artifact is still preview-only on this channel.",
            FixAvailabilitySummary: "Only verify fixes after this install can see the promoted archive.",
            ProofStatus: "passed",
            ProofGeneratedAtUtc: DateTimeOffset.Parse("2026-03-28T13:56:00+00:00"));
}
