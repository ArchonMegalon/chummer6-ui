#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M119FirstSessionFlowGuardTests
{
    [TestMethod]
    public void M119_first_session_flow_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m119-ui-first-session-flow-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M119 first playable session desktop flow guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m119-ui-first-session-flow\"");
        StringAssert.Contains(scriptText, "QUEUE_TITLE = \"Add first playable session flow to desktop home and campaign entry points\"");
        StringAssert.Contains(scriptText, "TASK = \"Add first playable session flow to desktop home and campaign entry points.\"");
        StringAssert.Contains(scriptText, "REGISTRY_MILESTONE_TITLE = \"Guided onboarding and starter lane to first playable session\"");
        StringAssert.Contains(scriptText, "REGISTRY_TASK_TITLE = \"Add first playable session flow to desktop home and campaign entry points.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 3766544333");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"119.2\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"first_playable_session:desktop\"");
        StringAssert.Contains(scriptText, "\"campaign_entry:first_session\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M119FirstSessionFlowGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "CreateButton(\"Starter\", OpenStarterLaneReviewAsync)");
        StringAssert.Contains(scriptText, "OpenStarterLaneReviewAsync()");
        StringAssert.Contains(scriptText, "CreateButton(\"Starter\", OpenStarterLaneReviewAsync)");

        StringAssert.Contains(projectText, "Compliance\\Next90M119FirstSessionFlowGuardTests.cs");
    }

    [TestMethod]
    public void M119_first_session_flow_receipt_proves_desktop_surface_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m119_ui_first_session_flow", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m119-ui-first-session-flow", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(3766544333, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(119, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("119.2", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W14", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "first_playable_session:desktop", "campaign_entry:first_session" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m119_milestone").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_m119_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_has_m119_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_owner_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_status_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_evidence_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_wave_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_wave_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_repo_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_repo_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_proof_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_proof_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_design_block_parity").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_path_matches").GetBoolean());

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m119-ui-first-session-flow-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
                Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M119FirstSessionFlowGuardTests.cs"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));

        CollectionAssert.AreEquivalent(
            new[]
            {
                "bash scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh",
                "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M119FirstSessionFlowGuardTests\" --no-restore",
                "dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore",
            },
            ReadStringArray(evidence.GetProperty("proofCommands")));
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
