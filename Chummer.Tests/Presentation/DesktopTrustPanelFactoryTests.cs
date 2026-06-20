using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Contracts.AI;
using Chummer.Contracts.Rulesets;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopTrustPanelFactoryTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;

    [TestMethod]
    public void CreateDialogPanel_surfaces_import_explanation_and_companion_launch_context()
    {
        EnsureHeadlessPlatform();

        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = factory.CreateCommandDialog(
            "open_character",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        string trustReceiptText = DesktopTrustReceiptText.BuildDialogReceipt(dialog);
        Control? panel = DesktopTrustPanelFactory.CreateDialogPanel(dialog, trustReceiptText);

        Assert.IsNotNull(panel);
        Assert.AreEqual(
            "Import explanation and environment details",
            FindDescendant<TextBlock>(panel, text => text.Text?.Contains("Import explanation and environment details", StringComparison.Ordinal) is true).Text);

        Button companionButton = FindDescendant<Button>(panel, "OpenDesktopDialogExplainCompanionButton");
        Assert.AreEqual("Open inspectable explain companion", AutomationProperties.GetName(companionButton));

        string launchUri = AssertString(companionButton.Tag);
        AiCoachLaunchContext context = AiCoachLaunchQuery.Parse(new Uri("https://chummer.test" + launchUri).Query);
        Assert.AreEqual(AiRouteTypes.Build, context.RouteType);
        Assert.AreEqual(RulesetDefaults.Sr5, context.RulesetId);
        StringAssert.Contains(context.Message ?? string.Empty, "Open Character explanation");
        StringAssert.Contains(context.Message ?? string.Empty, "Import rule-environment record");
    }

    [TestMethod]
    public void CreateDiagnosticsPanel_surfaces_support_blocker_companion()
    {
        EnsureHeadlessPlatform();

        Control? panel = DesktopTrustPanelFactory.CreateDiagnosticsPanel(
            CreateInstallState(status: "claimed"),
            CreateUpdateStatus(status: "attention_required", lastError: "Manifest signature mismatch."),
            rawLines: ["Fallback diagnostics receipt"]);

        Assert.IsNotNull(panel);
        Assert.AreEqual(
            "Support diagnostics and environment details",
            FindDescendant<TextBlock>(panel, text => text.Text?.Contains("Support diagnostics and environment details", StringComparison.Ordinal) is true).Text);

        Button companionButton = FindDescendant<Button>(panel, "OpenDesktopBlockerExplainCompanionButton");
        string launchUri = AssertString(companionButton.Tag);
        AiCoachLaunchContext context = AiCoachLaunchQuery.Parse(new Uri("https://chummer.test" + launchUri).Query);
        Assert.AreEqual(AiRouteTypes.Build, context.RouteType);
        Assert.AreEqual("avalonia", context.RuntimeFingerprint);
        StringAssert.Contains(context.Message ?? string.Empty, "Support blocker explanation");
        StringAssert.Contains(context.Message ?? string.Empty, "Diagnostics record correlation key: support/install-1/avalonia/stable");
        StringAssert.Contains(context.Message ?? string.Empty, "Manifest signature mismatch.");
    }

    [TestMethod]
    public void CreateCrashDiagnosticsPanel_surfaces_crash_blocker_companion()
    {
        EnsureHeadlessPlatform();

        Control? panel = DesktopTrustPanelFactory.CreateCrashDiagnosticsPanel(
            new DesktopCrashReport(
                CrashId: "crash-42",
                HeadId: "avalonia",
                CapturedAtUtc: DateTimeOffset.Parse("2026-05-11T06:00:00Z"),
                IsTerminating: true,
                ApplicationVersion: "1.4.0",
                RuntimeVersion: ".NET 10.0.7",
                OperatingSystem: "Linux",
                ProcessArchitecture: "x64",
                ProcessName: "Chummer.Avalonia",
                BaseDirectoryLabel: "/opt/chummer",
                CurrentDirectoryLabel: "/opt/chummer",
                ExceptionType: "System.InvalidOperationException",
                ExceptionMessage: "Boom",
                ExceptionDetail: "stack"),
            rawLines: ["Fallback crash receipt"]);

        Assert.IsNotNull(panel);
        Assert.AreEqual(
            "Crash diagnostics and environment details",
            FindDescendant<TextBlock>(panel, text => text.Text?.Contains("Crash diagnostics and environment details", StringComparison.Ordinal) is true).Text);

        Button companionButton = FindDescendant<Button>(panel, "OpenDesktopCrashBlockerExplainCompanionButton");
        string launchUri = AssertString(companionButton.Tag);
        AiCoachLaunchContext context = AiCoachLaunchQuery.Parse(new Uri("https://chummer.test" + launchUri).Query);
        Assert.AreEqual(AiRouteTypes.Build, context.RouteType);
        Assert.AreEqual("avalonia", context.RuntimeFingerprint);
        StringAssert.Contains(context.Message ?? string.Empty, "Crash blocker explanation");
        StringAssert.Contains(context.Message ?? string.Empty, "Crash diagnostics record");
    }

    [TestMethod]
    public void BuildLaunchUri_preserves_surface_metadata_for_workspace_follow_up()
    {
        DesktopExplainCompanionRequest request = new(
            Title: "Build lab explain companion",
            SurfaceId: "build_lab:desktop",
            SurfaceLabel: "Desktop build lab explain companion",
            Sections:
            [
                new DesktopTrustReceiptSection(
                    "Grounded explain receipt",
                    ["Support handoff receipt: support/build-lab/123", "Correlation key: build-lab/123"])
            ],
            SurfaceFamilyId: "build_lab",
            WorkspaceId: "ws-77",
            RulesetId: RulesetDefaults.Sr6,
            RuntimeFingerprint: "sha256:runtime");

        string launchUri = DesktopExplainCompanionLauncher.BuildLaunchUri(request);
        AiCoachLaunchContext context = AiCoachLaunchQuery.Parse(new Uri("https://chummer.test" + launchUri).Query);

        Assert.AreEqual(AiRouteTypes.Build, context.RouteType);
        Assert.AreEqual("sha256:runtime", context.RuntimeFingerprint);
        Assert.AreEqual("ws-77", context.WorkspaceId);
        Assert.AreEqual(RulesetDefaults.Sr6, context.RulesetId);
        StringAssert.Contains(context.Message ?? string.Empty, "build_lab:desktop");
        StringAssert.Contains(context.Message ?? string.Empty, "Grounded explain receipt");
    }

    [TestMethod]
    public void CreateLaunchButton_hides_companion_when_ai_features_are_disabled()
    {
        EnsureHeadlessPlatform();
        DesktopPreferenceState previousPreferences = DesktopPreferenceStateRuntime.Current;
        try
        {
            DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default with { DisableAiFeatures = true });
            DesktopExplainCompanionRequest request = new(
                Title: "Build lab explain companion",
                SurfaceId: "build_lab:desktop",
                SurfaceLabel: "Desktop build lab explain companion",
                Sections:
                [
                    new DesktopTrustReceiptSection(
                        "Grounded explain receipt",
                        ["Support handoff receipt: support/build-lab/123", "Correlation key: build-lab/123"])
                ],
                SurfaceFamilyId: "build_lab",
                WorkspaceId: "ws-77",
                RulesetId: RulesetDefaults.Sr6,
                RuntimeFingerprint: "sha256:runtime");

            Button companionButton = DesktopExplainCompanionLauncher.CreateLaunchButton(
                new Border(),
                request,
                "OpenSuppressedExplainCompanionButton");

            Assert.IsFalse(companionButton.IsVisible);
            Assert.IsFalse(companionButton.IsEnabled);
            Assert.IsNull(companionButton.Tag);
            Assert.AreEqual(string.Empty, companionButton.Content?.ToString());
        }
        finally
        {
            DesktopPreferenceStateRuntime.SetCurrent(previousPreferences);
        }
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return;
            }

            try
            {
                AppBuilder.Configure<global::Avalonia.Application>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
            {
                // Several Avalonia presentation tests share one process; another test may have already initialized the platform.
            }

            _headlessInitialized = true;
        }
    }

    private static T FindDescendant<T>(Control root, string name)
        where T : Control
        => root.GetVisualDescendants()
            .OfType<T>()
            .Single(control => string.Equals(control.Name, name, StringComparison.Ordinal));

    private static T FindDescendant<T>(Control root, Func<T, bool> predicate)
        where T : Control
        => root.GetVisualDescendants()
            .OfType<T>()
            .Single(predicate);

    private static string AssertString(object? value)
    {
        Assert.IsInstanceOfType<string>(value);
        return (string)value;
    }

    private static DesktopInstallLinkingState CreateInstallState(string status)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-05-11T06:00:00Z");
        return new DesktopInstallLinkingState(
            InstallationId: "install-1",
            HeadId: "avalonia",
            ApplicationVersion: "1.4.0",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            Status: status,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 3,
            LastStartedAtUtc: now,
            ClaimedAtUtc: string.Equals(status, "claimed", StringComparison.Ordinal) ? now : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: string.Equals(status, "claimed", StringComparison.Ordinal) ? "grant-token" : null);
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus(string status, string? lastError)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-05-11T06:00:00Z");
        return new DesktopUpdateClientStatus(
            HeadId: "avalonia",
            InstalledVersion: "1.4.0",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "/tmp/releases.json",
            LastCheckedAtUtc: now,
            LastManifestVersion: "1.4.1",
            LastManifestPublishedAtUtc: now,
            LastError: lastError,
            Status: status,
            RecommendedAction: "Open support before promoting the next installer.",
            RolloutState: "gated",
            RolloutReason: "support-follow-through",
            SupportabilityState: "needs_attention",
            ProofStatus: "pending");
    }
}
