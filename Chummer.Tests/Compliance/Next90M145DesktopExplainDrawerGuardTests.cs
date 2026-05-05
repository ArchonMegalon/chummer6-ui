#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M145DesktopExplainDrawerGuardTests
{
    [TestMethod]
    public void M145_desktop_explain_drawer_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string guardScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking next-90 M145 desktop explain drawer and bounded follow-up guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh");

        StringAssert.Contains(guardScript, "PACKAGE_ID = \"next90-m145-ui-desktop-explain-drawer-and-follow-up\"");
        StringAssert.Contains(guardScript, "TITLE = \"Wire the desktop explain drawer, source-anchor launch, stale-state handling, and text-first follow-up on promoted workbench routes.\"");
        StringAssert.Contains(guardScript, "TASK = \"Wire packet-backed desktop explain drawers, source-anchor affordances, stale snapshot handling, and text-first bounded follow-up across promoted workbench routes.\"");
        StringAssert.Contains(guardScript, "FRONTIER_ID = 1452045202");
        StringAssert.Contains(guardScript, "WORK_TASK_ID = \"145.2\"");
        StringAssert.Contains(guardScript, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(guardScript, "EXPECTED_DO_NOT_REOPEN_REASON");
        StringAssert.Contains(guardScript, "\"explain_every_value_drawer:ui\"");
        StringAssert.Contains(guardScript, "\"grounded_follow_up:desktop\"");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopExplainDrawerFollowUpWindow.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/MainWindow.SelectionHandlers.cs");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/MainWindow.FeedbackCoordinator.cs");
        StringAssert.Contains(guardScript, "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json");
        StringAssert.Contains(guardScript, "TASK_LOCAL_TELEMETRY.generated.json");
        StringAssert.Contains(guardScript, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(guardScript, "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M145DesktopExplainDrawerGuardTests\" --no-restore");
        StringAssert.Contains(guardScript, "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Standalone_section_context_reads_canonical_explanation_packet_fields_for_text_first_drawer_copy|FullyQualifiedName~Standalone_section_context_projects_packet_backed_explain_drawer_actions_for_desktop_launch_and_follow_up|FullyQualifiedName~Standalone_section_context_launches_source_anchor_from_packet_backed_explain_drawer\" --no-restore -p:BuildProjectReferences=false");

        StringAssert.Contains(projectText, "Compliance\\Next90M145DesktopExplainDrawerGuardTests.cs");
    }

    [TestMethod]
    public void M145_desktop_explain_drawer_receipt_proves_closed_package_state()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m145_ui_desktop_explain_drawer_and_follow_up", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m145-ui-desktop-explain-drawer-and-follow-up", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(1452045202, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(145, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("145.2", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W28", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "explain_every_value_drawer:ui", "grounded_follow_up:desktop" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_task_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_direct_proof_command_recorded").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("proof_items_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_proof_items_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_design_block_parity").GetBoolean());

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/Controls/SectionHostControl.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopExplainDrawerFollowUpWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.SelectionHandlers.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.FeedbackCoordinator.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Compliance/Next90M145DesktopExplainDrawerGuardTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/verify.sh"));
    }

    private static void AssertSourceMarkersPass(JsonElement sourceChecks)
    {
        foreach (JsonProperty markerCheck in sourceChecks.EnumerateObject())
        {
            Assert.IsTrue(markerCheck.Value.GetBoolean(), $"Expected source marker to pass: {markerCheck.Name}");
        }
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
