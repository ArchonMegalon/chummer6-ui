#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public void ReadPlan_defaults_to_a_complete_sr5_priority_journey()
    {
        string[] variables =
        [
            DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable,
            DesktopMouseFirstJourneyRuntime.MetatypeCategoryEnvironmentVariable,
            DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable,
            DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable,
            DesktopMouseFirstJourneyRuntime.PriorityTalentEnvironmentVariable,
            DesktopMouseFirstJourneyRuntime.PriorityTalentChoiceEnvironmentVariable
        ];
        Dictionary<string, string?> prior = variables.ToDictionary(
            variable => variable,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);

        try
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

            Assert.AreEqual("Priority", plan.BuildMethod);
            Assert.AreEqual("Standard", plan.MetatypeCategory);
            Assert.AreEqual("E", plan.PriorityHeritage);
            Assert.AreEqual("Human", plan.Metatype);
            Assert.AreEqual("B", plan.PriorityTalent);
            Assert.AreEqual("Mystic Adept", plan.PriorityTalentChoice);
        }
        finally
        {
            foreach ((string variable, string? value) in prior)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
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
        string? priorUserJourneyTrace = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTraceOutputEnvironmentVariable);
        string? priorTesterShard = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTesterShardIdEnvironmentVariable);
        string? priorFixShard = Environment.GetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyFixShardIdEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ArtifactDigestEnvironmentVariable, new string('c', 64));
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.HostClassEnvironmentVariable, "linux-x64-journey");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseVersionEnvironmentVariable, "local-journey");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.RidEnvironmentVariable, "linux-x64");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseChannelEnvironmentVariable, "docker");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.TracePathEnvironmentVariable, "/tmp/mouse-trace.json");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTraceOutputEnvironmentVariable, "/tmp/staged-user-journey.json");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTesterShardIdEnvironmentVariable, "tester-linux");
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyFixShardIdEnvironmentVariable, "fixer-linux");

            DesktopMouseFirstJourneyContext context = DesktopMouseFirstJourneyRuntime.BuildContext("avalonia", DateTimeOffset.UtcNow);

            Assert.AreEqual("sha256:" + new string('c', 64), context.ArtifactDigest);
            Assert.AreEqual("environment", context.ArtifactDigestSource);
            Assert.AreEqual("linux-x64-journey", context.HostClass);
            Assert.AreEqual("local-journey", context.Version);
            Assert.AreEqual("local-journey", context.ReleaseVersion);
            Assert.AreEqual("linux-x64", context.Rid);
            Assert.AreEqual("docker", context.ChannelId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(context.ProcessPath));
            Assert.IsFalse(context.ProcessPath.Contains('/'));
            Assert.IsFalse(context.ProcessPath.Contains('\\'));
            Assert.AreEqual("/tmp/mouse-trace.json", context.TracePath);
            Assert.AreEqual("/tmp/staged-user-journey.json", context.UserJourneyTraceOutputPath);
            Assert.AreEqual("tester-linux", context.UserJourneyTesterShardId);
            Assert.AreEqual("fixer-linux", context.UserJourneyFixShardId);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ArtifactDigestEnvironmentVariable, priorDigest);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.HostClassEnvironmentVariable, priorHostClass);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseVersionEnvironmentVariable, priorReleaseVersion);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.RidEnvironmentVariable, priorRid);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.ReleaseChannelEnvironmentVariable, priorChannel);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.TracePathEnvironmentVariable, priorTrace);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTraceOutputEnvironmentVariable, priorUserJourneyTrace);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyTesterShardIdEnvironmentVariable, priorTesterShard);
            Environment.SetEnvironmentVariable(DesktopMouseFirstJourneyRuntime.UserJourneyFixShardIdEnvironmentVariable, priorFixShard);
        }
    }

    [TestMethod]
    public void WriteSuccessReceipt_emits_atomic_bound_user_journey_trace_only_for_complete_unique_evidence()
    {
        string root = CreateTempDirectory();
        try
        {
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root);
            DesktopMouseFirstJourneyEvidence evidence = BuildUserJourneyEvidence(context.ScreenshotDirectory!);
            File.WriteAllText(context.UserJourneyTraceOutputPath!, "stale trace must be invalidated");

            DesktopMouseFirstJourneyRuntime.PrepareUserJourneyTraceOutput(context);
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(context, evidence);

            Assert.IsTrue(File.Exists(context.ReceiptPath));
            Assert.IsTrue(File.Exists(context.UserJourneyTraceOutputPath));
            using JsonDocument trace = JsonDocument.Parse(File.ReadAllText(context.UserJourneyTraceOutputPath!));
            JsonElement rootElement = trace.RootElement;
            Assert.AreEqual("chummer6-ui.user_journey_tester_trace", rootElement.GetProperty("contract_name").GetString());
            Assert.AreEqual("pass", rootElement.GetProperty("status").GetString());
            Assert.AreEqual("1.2.3-candidate", rootElement.GetProperty("release_version").GetString());
            Assert.AreEqual("candidate", rootElement.GetProperty("release_channel").GetString());
            Assert.AreEqual("sha256:" + new string('a', 64), rootElement.GetProperty("artifact_digest").GetString());
            Assert.AreEqual("tester-linux", rootElement.GetProperty("tester_shard_id").GetString());
            Assert.AreEqual("fixer-linux", rootElement.GetProperty("fix_shard_id").GetString());
            Assert.IsTrue(rootElement.GetProperty("linux_binary_under_test").GetBoolean());
            Assert.IsFalse(rootElement.GetProperty("used_internal_apis").GetBoolean());
            Assert.AreEqual(5, rootElement.GetProperty("workflows").GetArrayLength());
            Assert.AreEqual(0, rootElement.GetProperty("open_blocking_findings").GetArrayLength());

            string expectedReceiptDigest = "sha256:" + Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(context.ReceiptPath!))).ToLowerInvariant();
            Assert.AreEqual(expectedReceiptDigest, rootElement.GetProperty("source_mouse_receipt_sha256").GetString());
            Assert.AreEqual(Path.GetFileName(context.ReceiptPath), rootElement.GetProperty("source_mouse_receipt_name").GetString());
            Assert.IsTrue(DateTimeOffset.TryParse(rootElement.GetProperty("generated_at_utc").GetString(), out DateTimeOffset generatedAt));
            Assert.IsTrue(generatedAt >= context.StartedAtUtc);

            HashSet<string> hashes = new(StringComparer.Ordinal);
            foreach (JsonElement workflow in rootElement.GetProperty("workflows").EnumerateArray())
            {
                Assert.AreEqual("pass", workflow.GetProperty("status").GetString());
                Assert.AreEqual(2, workflow.GetProperty("screenshots").GetArrayLength());
                Assert.AreEqual(2, workflow.GetProperty("screenshot_sha256").EnumerateObject().Count());
                Assert.IsTrue(workflow.GetProperty("interaction_notes").GetArrayLength() > 0);
                foreach (JsonProperty binding in workflow.GetProperty("screenshot_sha256").EnumerateObject())
                {
                    Assert.IsTrue(hashes.Add(binding.Value.GetString() ?? string.Empty));
                }
            }

            Assert.AreEqual(10, hashes.Count);
            Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void WriteSuccessReceipt_rejects_partial_or_duplicate_screenshot_evidence_without_emitting_trace()
    {
        string root = CreateTempDirectory();
        try
        {
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root);
            DesktopMouseFirstJourneyEvidence complete = BuildUserJourneyEvidence(context.ScreenshotDirectory!);
            DesktopUserJourneyWorkflowEvidence[] workflows = complete.UserJourneyWorkflows!.ToArray();
            File.Copy(
                workflows[0].ScreenshotPaths[0],
                workflows[0].ScreenshotPaths[1],
                overwrite: true);
            DesktopMouseFirstJourneyEvidence duplicateFrameEvidence = complete with { UserJourneyWorkflows = workflows };

            DesktopMouseFirstJourneyRuntime.PrepareUserJourneyTraceOutput(context);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(context, duplicateFrameEvidence));
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            Assert.IsFalse(File.Exists(context.ReceiptPath));

            DesktopMouseFirstJourneyEvidence partialEvidence = BuildUserJourneyEvidence(context.ScreenshotDirectory!) with
            {
                UserJourneyWorkflows = BuildUserJourneyEvidence(context.ScreenshotDirectory!).UserJourneyWorkflows!.Take(4).ToArray()
            };
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(context, partialEvidence));
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            Assert.IsFalse(File.Exists(context.ReceiptPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void WriteSuccessReceipt_names_failed_user_journey_assertions()
    {
        string root = CreateTempDirectory();
        try
        {
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root);
            DesktopMouseFirstJourneyEvidence complete = BuildUserJourneyEvidence(context.ScreenshotDirectory!);
            DesktopUserJourneyWorkflowEvidence[] workflows = complete.UserJourneyWorkflows!.ToArray();
            Dictionary<string, bool> assertions = new(workflows[1].Assertions, StringComparer.Ordinal)
            {
                ["starter_attributes_match_seeded_workspace"] = false
            };
            workflows[1] = workflows[1] with { Assertions = assertions };

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(
                    context,
                    complete with { UserJourneyWorkflows = workflows }));

            Assert.AreEqual(
                "Workflow 'file_new_character_visible_workspace' assertions invalid: "
                + "failed_or_missing=[starter_attributes_match_seeded_workspace]; unexpected=[]; "
                + "expected_count=4; actual_count=4.",
                error.Message);
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            Assert.IsFalse(File.Exists(context.ReceiptPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UserJourneyTraceProducer_rejects_missing_bindings_and_equal_shards()
    {
        string root = CreateTempDirectory();
        try
        {
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root);
            DesktopMouseFirstJourneyEvidence evidence = BuildUserJourneyEvidence(context.ScreenshotDirectory!);

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(
                    context with { ArtifactDigest = null },
                    evidence));
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            Assert.IsFalse(File.Exists(context.ReceiptPath));

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(
                    context with { UserJourneyFixShardId = context.UserJourneyTesterShardId },
                    evidence));
            Assert.IsFalse(File.Exists(context.UserJourneyTraceOutputPath));
            Assert.IsFalse(File.Exists(context.ReceiptPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PrepareUserJourneyTraceOutput_never_mutates_canonical_published_trace_path()
    {
        string root = CreateTempDirectory();
        try
        {
            string canonicalDirectory = Path.Combine(root, ".codex-studio", "published");
            Directory.CreateDirectory(canonicalDirectory);
            string canonicalPath = Path.Combine(canonicalDirectory, "USER_JOURNEY_TESTER_TRACE.generated.json");
            File.WriteAllText(canonicalPath, "canonical-may-trace");
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root) with
            {
                UserJourneyTraceOutputPath = canonicalPath
            };

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.PrepareUserJourneyTraceOutput(context));
            Assert.AreEqual("canonical-may-trace", File.ReadAllText(canonicalPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PrepareUserJourneyTraceOutput_rejects_symlink_alias_to_canonical_trace()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root = CreateTempDirectory();
        string aliasDirectory = Path.Combine(root, "staged-output");
        try
        {
            string canonicalDirectory = Path.Combine(root, ".codex-studio", "published");
            Directory.CreateDirectory(canonicalDirectory);
            string canonicalPath = Path.Combine(canonicalDirectory, "USER_JOURNEY_TESTER_TRACE.generated.json");
            File.WriteAllText(canonicalPath, "canonical-may-trace");
            Directory.CreateSymbolicLink(aliasDirectory, canonicalDirectory);
            DesktopMouseFirstJourneyContext context = BuildUserJourneyContext(root) with
            {
                UserJourneyTraceOutputPath = Path.Combine(aliasDirectory, "USER_JOURNEY_TESTER_TRACE.generated.json")
            };

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DesktopMouseFirstJourneyRuntime.PrepareUserJourneyTraceOutput(context));
            Assert.AreEqual("canonical-may-trace", File.ReadAllText(canonicalPath));
        }
        finally
        {
            if (Directory.Exists(aliasDirectory))
            {
                Directory.Delete(aliasDirectory);
            }

            Directory.Delete(root, recursive: true);
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
            Assert.AreEqual("chummer", receipt.RootElement.GetProperty("processPath").GetString());
            Assert.AreEqual("file_name_only", receipt.RootElement.GetProperty("processPathDisclosure").GetString());
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
            Assert.AreEqual("chummer", receipt.RootElement.GetProperty("processPath").GetString());
            Assert.AreEqual("file_name_only", receipt.RootElement.GetProperty("processPathDisclosure").GetString());
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

    private static DesktopMouseFirstJourneyContext BuildUserJourneyContext(string root)
    {
        string screenshotDirectory = Path.Combine(root, "screens");
        Directory.CreateDirectory(screenshotDirectory);
        return new DesktopMouseFirstJourneyContext(
            HeadId: "avalonia",
            Version: "1.2.3-candidate",
            ReleaseVersion: "1.2.3-candidate",
            ChannelId: "candidate",
            Platform: "linux",
            Arch: "x64",
            Rid: "linux-x64",
            HostClass: "linux-x64-journey",
            ProcessPath: "/tmp/chummer",
            ArtifactDigest: "sha256:" + new string('a', 64),
            ArtifactDigestSource: "environment",
            Framework: ".NET 10",
            OperatingSystem: "Linux",
            StartedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
            ReceiptPath: Path.Combine(root, "mouse-receipt.json"),
            FailurePacketPath: Path.Combine(root, "mouse-failure.json"),
            ScreenshotDirectory: screenshotDirectory,
            TracePath: Path.Combine(root, "observed-input.json"),
            UserJourneyTraceOutputPath: Path.Combine(root, "staged-user-journey.json"),
            UserJourneyTesterShardId: "tester-linux",
            UserJourneyFixShardId: "fixer-linux");
    }

    private static DesktopMouseFirstJourneyEvidence BuildUserJourneyEvidence(string screenshotDirectory)
    {
        (string Id, string[] Assertions)[] workflowDefinitions =
        [
            ("master_index_search_focus_stability", ["focus_preserved_after_typing", "search_text_accumulates_keyboard_input"]),
            ("file_new_character_visible_workspace", ["new_character_action_opened_visible_workspace", "visible_workspace_nonblank", "starter_attributes_match_seeded_workspace", "section_preview_omits_review_copy"]),
            ("minimal_character_build_save_reload", ["character_created_saved_reloaded", "reload_preserved_character_identity"]),
            ("major_navigation_sanity", ["primary_navigation_clicks_change_visible_content", "no_unhandled_errors"]),
            ("validation_or_export_smoke", ["validation_or_export_action_completed", "result_visible_or_file_created"])
        ];
        List<DesktopUserJourneyWorkflowEvidence> workflows = [];
        int screenshotIndex = 1;
        foreach ((string workflowId, string[] assertions) in workflowDefinitions)
        {
            string beforePath = Path.Combine(screenshotDirectory, $"{workflowId}-before.png");
            string afterPath = Path.Combine(screenshotDirectory, $"{workflowId}-after.png");
            WriteFakePng(beforePath, screenshotIndex++);
            WriteFakePng(afterPath, screenshotIndex++);
            workflows.Add(new DesktopUserJourneyWorkflowEvidence(
                Id: workflowId,
                ScreenshotPaths: [beforePath, afterPath],
                Assertions: assertions.ToDictionary(assertion => assertion, _ => true, StringComparer.Ordinal),
                InteractionNotes: [$"routed workflow {workflowId}"]));
        }

        return new DesktopMouseFirstJourneyEvidence(
            Steps: ["exercise five routed workflows"],
            ScreenshotPaths: workflows.SelectMany(workflow => workflow.ScreenshotPaths).ToArray(),
            PointerActionCount: 20,
            TextEntryActionCount: 4,
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
            AuthenticationPortalOpened: false,
            AuthenticationPortalUri: null,
            ActiveDialogId: null,
            VerificationNotes: ["complete"],
            UserJourneyWorkflows: workflows);
    }

    private static void WriteFakePng(string path, int uniqueIndex)
    {
        byte[] bytes = new byte[64];
        byte[] signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        signature.CopyTo(bytes, 0);
        bytes[11] = 13;
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        bytes[19] = (byte)uniqueIndex;
        bytes[23] = (byte)(uniqueIndex + 1);
        bytes[24] = 8;
        bytes[25] = 6;
        bytes[63] = (byte)(uniqueIndex * 7);
        File.WriteAllBytes(path, bytes);
    }

    private static string CreateTempDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mouse-user-journey-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
