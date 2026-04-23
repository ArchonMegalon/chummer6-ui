#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M109BuildExplainArtifactsGuardTests
{
    [TestMethod]
    public void M109_build_explain_artifact_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string guardScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m109-ui-build-explain-artifacts-check.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking next-90 M109 build explain artifact companion closure guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh");

        StringAssert.Contains(guardScript, "PACKAGE_ID = \"next90-m109-ui-build-explain-artifacts\"");
        StringAssert.Contains(guardScript, "WORK_TASK_ID = \"109.2\"");
        StringAssert.Contains(guardScript, "FRONTIER_ID = \"4240255582\"");
        StringAssert.Contains(guardScript, "LANDED_COMMIT = \"da261bb7\"");
        StringAssert.Contains(guardScript, "EXPECTED_DO_NOT_REOPEN_REASON");
        StringAssert.Contains(guardScript, "REQUIRED_QUEUE_PROOF_ITEMS");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopExplainCompanionLauncher.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustPanelFactory.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/DesktopTrustReceiptComposer.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/DesktopTrustPanelFactoryTests.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M109BuildExplainArtifactsGuardTests.cs");
        StringAssert.Contains(guardScript, "TASK_LOCAL_TELEMETRY.generated.json");
        StringAssert.Contains(guardScript, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(guardScript, "active-run helper commands");
        StringAssert.Contains(guardScript, "base64.b32decode");
        StringAssert.Contains(guardScript, "base64.b85decode");
        StringAssert.Contains(guardScript, "base64.a85decode");
        StringAssert.Contains(guardScript, "gzip.decompress");
        StringAssert.Contains(guardScript, "zlib.decompress");
        StringAssert.Contains(guardScript, "fleet mirror");
        StringAssert.Contains(guardScript, "design source");
        StringAssert.Contains(guardScript, "Fleet and design-owned M109 successor queue rows drifted apart");
        StringAssert.Contains(guardScript, "historical M109 landed commit unexpectedly resolves in current repo checkout");
        StringAssert.Contains(guardScript, "NEXT90_M109_UI_BUILD_EXPLAIN_ARTIFACTS.generated.json");
        StringAssert.Contains(guardScript, "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M109BuildExplainArtifactsGuardTests\" --no-restore");

        StringAssert.Contains(projectText, "Compliance\\Next90M109BuildExplainArtifactsGuardTests.cs");
    }

    [TestMethod]
    public void M109_build_explain_artifact_receipt_proves_closed_package_state()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M109_UI_BUILD_EXPLAIN_ARTIFACTS.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m109_ui_build_explain_artifacts", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m109-ui-build-explain-artifacts", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(4240255582, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(109, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("109.2", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W9", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());
        Assert.AreEqual("da261bb7", evidence.GetProperty("landedCommit").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "build_explain:artifact_launch", "explain_receipts:desktop" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement queueChecks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(queueChecks.GetProperty("status_complete").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("completion_action_verify_closed_package_only").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_proof_items_exact").GetBoolean());

        JsonElement designQueueChecks = evidence.GetProperty("designQueueChecks");
        Assert.IsTrue(designQueueChecks.GetProperty("status_complete").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("completion_action_verify_closed_package_only").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("queue_proof_items_exact").GetBoolean());

        JsonElement registryChecks = evidence.GetProperty("registryChecks");
        Assert.IsTrue(registryChecks.GetProperty("milestone_present").GetBoolean());
        Assert.IsTrue(registryChecks.GetProperty("task_present").GetBoolean());
        Assert.IsTrue(registryChecks.GetProperty("status_complete").GetBoolean());
        Assert.IsTrue(registryChecks.GetProperty("landed_commit_matches").GetBoolean());
        Assert.IsTrue(registryChecks.GetProperty("direct_proof_command_recorded").GetBoolean());

        JsonElement queueMirrorChecks = evidence.GetProperty("queueMirrorChecks");
        Assert.IsTrue(queueMirrorChecks.GetProperty("fleet_queue_points_to_design_queue").GetBoolean());
        Assert.IsTrue(queueMirrorChecks.GetProperty("package_blocks_match").GetBoolean());
        Assert.IsTrue(queueMirrorChecks.GetProperty("fleet_queue_package_unique").GetBoolean());
        Assert.IsTrue(queueMirrorChecks.GetProperty("design_queue_package_unique").GetBoolean());

        JsonElement localRepoChecks = evidence.GetProperty("localRepoChecks");
        Assert.IsTrue(localRepoChecks.GetProperty("guard_script_present").GetBoolean());
        Assert.IsTrue(localRepoChecks.GetProperty("guard_test_present").GetBoolean());
        Assert.IsTrue(localRepoChecks.GetProperty("verify_wiring_present").GetBoolean());
        Assert.IsTrue(localRepoChecks.GetProperty("compliance_wiring_present").GetBoolean());
        Assert.IsFalse(localRepoChecks.GetProperty("historical_landed_commit_resolves").GetBoolean());
        Assert.IsTrue(localRepoChecks.GetProperty("historical_finish_repo_proof_pinned").GetBoolean());

        JsonElement closureGuard = evidence.GetProperty("closureGuard");
        Assert.AreEqual("closed_and_verified", closureGuard.GetProperty("status").GetString());
        Assert.IsTrue(closureGuard.GetProperty("canonicalRegistryComplete").GetBoolean());
        Assert.IsTrue(closureGuard.GetProperty("canonicalQueueComplete").GetBoolean());
        Assert.IsTrue(closureGuard.GetProperty("completionActionPinned").GetBoolean());
        Assert.IsTrue(closureGuard.GetProperty("doNotReopenReasonPinned").GetBoolean());
        StringAssert.Contains(
            closureGuard.GetProperty("reason").GetString(),
            "rerun this verifier as proof instead of reopening the closed desktop explain-companion package");
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        List<string> values = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            string? value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }
}
