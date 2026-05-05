#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M114RuleEnvironmentStudioGuardTests
{
    [TestMethod]
    public void M114_rule_environment_studio_guard_pins_queue_identity_and_desktop_markers()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m114-ui-rule-studio-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m114-ui-rule-studio\"");
        StringAssert.Contains(scriptText, "TITLE = \"Add rule-environment studio entry points to desktop workflows\"");
        StringAssert.Contains(scriptText, "TASK = \"Surface amend-package lifecycle, before-after diffs, and explain receipts in mechanical desktop workflows.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 3253425842");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"114.2\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"rule_environment_studio:desktop\"");
        StringAssert.Contains(scriptText, "\"explain_receipts:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m114-ui-rule-studio-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M114RuleEnvironmentStudioGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "\"queue_design_block_parity\": queue_block == design_queue_block");
        StringAssert.Contains(scriptText, "\"queue_work_task_matches\": yaml_scalar(queue_block, \"work_task_id\") == WORK_TASK_ID");
        StringAssert.Contains(scriptText, "\"queue_repo_matches\": yaml_scalar(queue_block, \"repo\") == \"chummer6-ui\"");
        StringAssert.Contains(scriptText, "\"design_queue_repo_matches\": yaml_scalar(design_queue_block, \"repo\") == \"chummer6-ui\"");
        StringAssert.Contains(scriptText, "DesktopHomeWindow.ShowAsync(this, \"avalonia\", _adapter.State.LatestPortabilityActivity)");
        StringAssert.Contains(scriptText, "DesktopHomeWindow.ShowAsync(owner, _installState.HeadId, _portabilityActivity)");
        StringAssert.Contains(scriptText, "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)");
        StringAssert.Contains(scriptText, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)");
        StringAssert.Contains(scriptText, "Import explain receipt:");
    }

    [TestMethod]
    public void M114_rule_environment_studio_receipt_proves_desktop_workflow_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M114_UI_RULE_STUDIO.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m114_ui_rule_studio", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m114-ui-rule-studio", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(3253425842, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(114, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("114.2", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W12", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "rule_environment_studio:desktop", "explain_receipts:desktop" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m114_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_status_in_progress").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_status_in_progress").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_wave_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_wave_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_repo_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_repo_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_design_block_parity").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_path_matches").GetBoolean());

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignArtifactWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.EventHandlers.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));
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
