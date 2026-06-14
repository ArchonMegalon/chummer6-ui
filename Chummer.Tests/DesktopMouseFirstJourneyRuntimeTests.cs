#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopMouseFirstJourneyRuntimeTests
{
    [TestMethod]
    public void ShouldRun_returns_true_when_mouse_first_switch_is_present()
    {
        Assert.IsTrue(DesktopMouseFirstJourneyRuntime.ShouldRun(["app", DesktopMouseFirstJourneyRuntime.MouseFirstJourneySwitch]));
        Assert.IsFalse(DesktopMouseFirstJourneyRuntime.ShouldRun(["app"]));
    }

    [TestMethod]
    public void BuildContext_reads_explicit_environment_overrides()
    {
        string? priorDigest = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ArtifactDigestEnvironmentVariable);
        string? priorHostClass = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.HostClassEnvironmentVariable);
        string? priorReleaseVersion = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseVersionEnvironmentVariable);
        string? priorRid = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.RidEnvironmentVariable);
        string? priorChannel = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseChannelEnvironmentVariable);
        string? priorTrace = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.TracePathEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ArtifactDigestEnvironmentVariable, new string('c', 64));
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.HostClassEnvironmentVariable, "linux-x64-journey");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseVersionEnvironmentVariable, "local-journey");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.RidEnvironmentVariable, "linux-x64");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseChannelEnvironmentVariable, "docker");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.TracePathEnvironmentVariable, "/tmp/mouse-trace.json");

            DesktopMouseFirstJourneyContext context = DesktopMouseFirstJourneyRuntime.BuildContext("avalonia", DateTimeOffset.UtcNow);

            Assert.AreEqual("sha256:" + new string('c', 64), context.ArtifactDigest);
            Assert.AreEqual("environment", context.ArtifactDigestSource);
            Assert.AreEqual("linux-x64-journey", context.HostClass);
            Assert.AreEqual("local-journey", context.Version);
            Assert.AreEqual("local-journey", context.ReleaseVersion);
            Assert.AreEqual("linux-x64", context.Rid);
            Assert.AreEqual("docker", context.ChannelId);
            Assert.AreEqual("/tmp/mouse-trace.json", context.TracePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ArtifactDigestEnvironmentVariable, priorDigest);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.HostClassEnvironmentVariable, priorHostClass);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseVersionEnvironmentVariable, priorReleaseVersion);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.RidEnvironmentVariable, priorRid);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseChannelEnvironmentVariable, priorChannel);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.TracePathEnvironmentVariable, priorTrace);
        }
    }

    [TestMethod]
    public void WriteSuccessReceipt_writes_pass_payload()
    {
        string receiptPath = Path.Combine(Path.GetTempPath(), $"mouse-first-journey-{Guid.NewGuid():N}.json");
        DesktopMouseFirstJourneyContext context = new(
            HeadId: "avalonia",
            Version: "local",
            ReleaseVersion: "local",
            ChannelId: "docker",
            Platform: "linux",
            Arch: "x64",
            Rid: "linux-x64",
            HostClass: "linux-x64-host",
            ProcessPath: "/tmp/chummer",
            ArtifactDigest: "sha256:" + new string('d', 64),
            ArtifactDigestSource: "environment",
            Framework: ".NET 10",
            OperatingSystem: "Linux",
            StartedAtUtc: DateTimeOffset.UtcNow,
            ReceiptPath: receiptPath,
            FailurePacketPath: null,
            ScreenshotDirectory: "/tmp/screens",
            TracePath: "/tmp/mouse-trace.json");

        try
        {
            DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(
                context,
                new DesktopMouseFirstJourneyEvidence(
                    Steps: ["click file menu", "click save"],
                    ScreenshotPaths: [],
                    PointerActionCount: 4,
                    TextEntryActionCount: 1,
                    DirectTextMutationCount: 0,
                    UsedForcedComboDropdownOpen: false,
                    UsedComboSelectionFallback: false,
                    ObservedInputEvents:
                    [
                        new DesktopMouseFirstJourneyObservedInputEvent("tapped", "MenuItem", "FileMenuButton", null, null, DateTimeOffset.UtcNow)
                    ],
                    ScenarioId: "sr5-priority",
                    WorkspaceId: "ws-1",
                    CharacterName: "Mouse Journey Runner",
                    CharacterAlias: "MouseRoute",
                    RulesetId: "sr5",
                    BuildMethod: "Priority",
                    MetatypeCategory: "Standard",
                    PriorityHeritage: "E",
                    Metatype: "Human",
                    PriorityTalent: "B",
                    PriorityTalentChoice: "Mystic Adept",
                    HasSavedWorkspace: true,
                    AuthenticationPortalOpened: true,
                    AuthenticationPortalUri: "https://chummer.run/account/access/install-link?login=1",
                    ActiveDialogId: null,
                    VerificationNotes: ["saved"]));

            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
            Assert.AreEqual("pass", receipt.RootElement.GetProperty("status").GetString());
            Assert.AreEqual("mouse_first_live_binary", receipt.RootElement.GetProperty("journeyMode").GetString());
            Assert.AreEqual("sr5-priority", receipt.RootElement.GetProperty("scenarioId").GetString());
            Assert.AreEqual("ws-1", receipt.RootElement.GetProperty("workspaceId").GetString());
            Assert.IsTrue(receipt.RootElement.GetProperty("hasSavedWorkspace").GetBoolean());
            Assert.AreEqual("Mouse Journey Runner", receipt.RootElement.GetProperty("characterName").GetString());
            Assert.AreEqual("Priority", receipt.RootElement.GetProperty("buildMethod").GetString());
            Assert.AreEqual("Standard", receipt.RootElement.GetProperty("metatypeCategory").GetString());
            Assert.AreEqual("E", receipt.RootElement.GetProperty("priorityHeritage").GetString());
            Assert.AreEqual("Human", receipt.RootElement.GetProperty("metatype").GetString());
            Assert.AreEqual("B", receipt.RootElement.GetProperty("priorityTalent").GetString());
            Assert.AreEqual("Mystic Adept", receipt.RootElement.GetProperty("priorityTalentChoice").GetString());
            Assert.AreEqual("/tmp/screens", receipt.RootElement.GetProperty("screenshotDirectory").GetString());
            Assert.AreEqual("/tmp/mouse-trace.json", receipt.RootElement.GetProperty("tracePath").GetString());
            Assert.AreEqual(4, receipt.RootElement.GetProperty("pointerActionCount").GetInt32());
            Assert.AreEqual(1, receipt.RootElement.GetProperty("textEntryActionCount").GetInt32());
            Assert.AreEqual(0, receipt.RootElement.GetProperty("directTextMutationCount").GetInt32());
            Assert.IsTrue(receipt.RootElement.GetProperty("authenticationPortalOpened").GetBoolean());
            Assert.AreEqual("https://chummer.run/account/access/install-link?login=1", receipt.RootElement.GetProperty("authenticationPortalUri").GetString());
            Assert.IsFalse(receipt.RootElement.GetProperty("usedForcedComboDropdownOpen").GetBoolean());
            Assert.IsFalse(receipt.RootElement.GetProperty("usedComboSelectionFallback").GetBoolean());
            Assert.AreEqual(1, receipt.RootElement.GetProperty("observedInputEvents").GetArrayLength());
        }
        finally
        {
            if (File.Exists(receiptPath))
            {
                File.Delete(receiptPath);
            }
        }
    }

    [TestMethod]
    public void WriteFailureArtifacts_writes_fail_receipt_and_packet()
    {
        string receiptPath = Path.Combine(Path.GetTempPath(), $"mouse-first-journey-fail-{Guid.NewGuid():N}.json");
        string packetPath = Path.Combine(Path.GetTempPath(), $"mouse-first-journey-packet-{Guid.NewGuid():N}.json");
        string[] screenshotPaths = ["/tmp/screens/step-01.png", "/tmp/screens/step-02.png"];
        DesktopMouseFirstJourneyContext context = new(
            HeadId: "avalonia",
            Version: "local",
            ReleaseVersion: "local",
            ChannelId: "docker",
            Platform: "linux",
            Arch: "x64",
            Rid: "linux-x64",
            HostClass: "linux-x64-host",
            ProcessPath: "/tmp/chummer",
            ArtifactDigest: "sha256:" + new string('e', 64),
            ArtifactDigestSource: "environment",
            Framework: ".NET 10",
            OperatingSystem: "Linux",
            StartedAtUtc: DateTimeOffset.UtcNow,
            ReceiptPath: receiptPath,
            FailurePacketPath: packetPath,
            ScreenshotDirectory: null,
            TracePath: "/tmp/mouse-trace.json");

        try
        {
            DesktopMouseFirstJourneyRuntime.WriteFailureArtifacts(
                context,
                new InvalidOperationException("boom"),
                ["click file menu"],
                screenshotPaths: screenshotPaths,
                pointerActionCount: 3,
                textEntryActionCount: 1,
                directTextMutationCount: 1,
                usedForcedComboDropdownOpen: true,
                usedComboSelectionFallback: true,
                observedInputEvents:
                [
                    new DesktopMouseFirstJourneyObservedInputEvent("tapped", "ComboBox", "dialog.field.newCharacterRulesetId", null, "dialog.new_character", DateTimeOffset.UtcNow)
                ],
                priorityTalent: "B",
                priorityTalentChoice: "Mystic Adept",
                activeDialogId: "dialog.new_character",
                workspaceId: "ws-fail",
                authenticationPortalOpened: true,
                authenticationPortalUri: "https://chummer.run/login?next=%2Faccount%2Faccess");

            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
            Assert.AreEqual("fail", receipt.RootElement.GetProperty("status").GetString());
            Assert.AreEqual("dialog.new_character", receipt.RootElement.GetProperty("activeDialogId").GetString());
            Assert.AreEqual("ws-fail", receipt.RootElement.GetProperty("workspaceId").GetString());
            Assert.AreEqual("B", receipt.RootElement.GetProperty("priorityTalent").GetString());
            Assert.AreEqual("Mystic Adept", receipt.RootElement.GetProperty("priorityTalentChoice").GetString());
            Assert.AreEqual(2, receipt.RootElement.GetProperty("screenshotPaths").GetArrayLength());
            Assert.AreEqual(screenshotPaths[0], receipt.RootElement.GetProperty("screenshotPaths")[0].GetString());
            Assert.AreEqual(3, receipt.RootElement.GetProperty("pointerActionCount").GetInt32());
            Assert.AreEqual(1, receipt.RootElement.GetProperty("textEntryActionCount").GetInt32());
            Assert.AreEqual(1, receipt.RootElement.GetProperty("directTextMutationCount").GetInt32());
            Assert.IsTrue(receipt.RootElement.GetProperty("usedForcedComboDropdownOpen").GetBoolean());
            Assert.IsTrue(receipt.RootElement.GetProperty("usedComboSelectionFallback").GetBoolean());
            Assert.AreEqual(1, receipt.RootElement.GetProperty("observedInputEvents").GetArrayLength());

            using JsonDocument packet = JsonDocument.Parse(File.ReadAllText(packetPath));
            Assert.AreEqual("desktop_mouse_first_journey_failure", packet.RootElement.GetProperty("signalClass").GetString());
            Assert.AreEqual("ws-fail", packet.RootElement.GetProperty("workspaceId").GetString());
            Assert.AreEqual("B", packet.RootElement.GetProperty("priorityTalent").GetString());
            Assert.AreEqual("Mystic Adept", packet.RootElement.GetProperty("priorityTalentChoice").GetString());
            Assert.AreEqual(3, packet.RootElement.GetProperty("pointerActionCount").GetInt32());
            Assert.AreEqual(1, packet.RootElement.GetProperty("textEntryActionCount").GetInt32());
            Assert.AreEqual(1, packet.RootElement.GetProperty("directTextMutationCount").GetInt32());
            Assert.IsTrue(packet.RootElement.GetProperty("usedForcedComboDropdownOpen").GetBoolean());
            Assert.IsTrue(packet.RootElement.GetProperty("usedComboSelectionFallback").GetBoolean());
            Assert.AreEqual(1, packet.RootElement.GetProperty("observedInputEvents").GetArrayLength());
            Assert.IsTrue(packet.RootElement.GetProperty("authenticationPortalOpened").GetBoolean());
            Assert.AreEqual("https://chummer.run/login?next=%2Faccount%2Faccess", packet.RootElement.GetProperty("authenticationPortalUri").GetString());
        }
        finally
        {
            if (File.Exists(receiptPath))
            {
                File.Delete(receiptPath);
            }

            if (File.Exists(packetPath))
            {
                File.Delete(packetPath);
            }
        }
    }
}
