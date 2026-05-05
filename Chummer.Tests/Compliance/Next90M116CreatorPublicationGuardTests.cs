#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M116CreatorPublicationGuardTests
{
    [TestMethod]
    public void M116_creator_publication_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m116-ui-creator-publication-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M116 creator publication and moderation desktop actions guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m116-ui-creator-publication-check.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m116-ui-creator-publication\"");
        StringAssert.Contains(scriptText, "TITLE = \"Surface creator publication and moderation actions on desktop\"");
        StringAssert.Contains(scriptText, "TASK = \"Surface creator publication and moderation actions on desktop.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 1446581929");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"116.3\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"creator_publication:desktop\"");
        StringAssert.Contains(scriptText, "\"moderation_flow:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m116-ui-creator-publication-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M116CreatorPublicationGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "\"Open Creator Publication\"");
        StringAssert.Contains(scriptText, "\"Review Moderation Flow\"");
        StringAssert.Contains(scriptText, "DesktopCreatorPublicationWindow.ShowAsync(");
        StringAssert.Contains(scriptText, "DesktopCreatorPublicationWindow.ShowModerationAsync(");
        StringAssert.Contains(scriptText, "Desktop creator publication surface requires an IChummerClient instance.");

        StringAssert.Contains(projectText, "Compliance\\Next90M116CreatorPublicationGuardTests.cs");
    }

    [TestMethod]
    public void M116_creator_publication_receipt_proves_desktop_surface_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m116_ui_creator_publication", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m116-ui-creator-publication", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(1446581929, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(116, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("116.3", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W13", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "creator_publication:desktop", "moderation_flow:desktop" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m116_ui_task").GetBoolean());
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
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCreatorPublicationWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m116-ui-creator-publication-check.sh"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M116CreatorPublicationGuardTests.cs"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));
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
