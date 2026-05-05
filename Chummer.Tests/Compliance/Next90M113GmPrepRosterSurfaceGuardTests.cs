#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M113GmPrepRosterSurfaceGuardTests
{
    [TestMethod]
    public void M113_gm_prep_roster_surface_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m113-ui-gm-prep-roster-surface-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M113 GM prep and roster movement desktop surface guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m113-ui-gm-prep-roster-surface\"");
        StringAssert.Contains(scriptText, "TITLE = \"Add GM prep and roster movement surfaces to the desktop workspace\"");
        StringAssert.Contains(scriptText, "TASK = \"Add GM prep and roster movement surfaces to the primary desktop workspace route.\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"gm_prep_packets:desktop\"");
        StringAssert.Contains(scriptText, "\"roster_movement:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M113GmPrepRosterSurfaceGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspaceSurface.GmPrepPackets\"");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspaceSurface.RosterMovement\"");
        StringAssert.Contains(scriptText, "\"GM prep packets:\"");
        StringAssert.Contains(scriptText, "\"Roster movement follow-through:\"");
        StringAssert.Contains(scriptText, "\"\\\"Open Creator Publication\\\"\"");
        StringAssert.Contains(scriptText, "\"\\\"Review Moderation Flow\\\"\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopHomeWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"\\\"Open GM Prep Packets\\\"\"");
        StringAssert.Contains(scriptText, "\"\\\"Review Roster Movement\\\"\"");
        StringAssert.Contains(scriptText, "\"OpenGmPrepPacketsAsync\"");
        StringAssert.Contains(scriptText, "\"OpenRosterMovementAsync\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/MainWindow.EventHandlers.cs\": [");
        StringAssert.Contains(scriptText, "\"ToolStrip_OnGmPrepRequested\"");
        StringAssert.Contains(scriptText, "\"ToolStrip_OnRosterMovementRequested\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/Controls/ToolStripControl.axaml.cs\": [");
        StringAssert.Contains(scriptText, "\"public event EventHandler? GmPrepRequested;\"");
        StringAssert.Contains(scriptText, "\"public event EventHandler? RosterMovementRequested;\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/App.axaml.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopStartupSurfaceCatalog.GmPrepPackets\"");
        StringAssert.Contains(scriptText, "\"DesktopStartupSurfaceCatalog.RosterMovement\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs\": [");
        StringAssert.Contains(scriptText, "\"public const string GmPrepPackets = \\\"gm_prep_packets\\\";\"");
        StringAssert.Contains(scriptText, "\"public const string RosterMovement = \\\"roster_movement\\\";\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspace_promotes_gm_prep_packets_and_roster_movement()\"");
        StringAssert.Contains(scriptText, "\"registry_has_m113_ui_task\"");
        StringAssert.Contains(scriptText, "\"queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"design_queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"allowed_paths_exact\"");
        StringAssert.Contains(scriptText, "\"owned_surfaces_exact\"");
        StringAssert.Contains(scriptText, "\"proofCommands\"");
        StringAssert.Contains(scriptText, "\"proofFiles\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not failed else \"fail\"");
    }

    [TestMethod]
    public void M113_gm_prep_roster_surface_receipt_proves_desktop_workspace_route()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("next90-m113-ui-gm-prep-roster-surface", root.GetProperty("packageId").GetString());
        Assert.AreEqual("Add GM prep and roster movement surfaces to the desktop workspace", root.GetProperty("title").GetString());
        Assert.AreEqual("Add GM prep and roster movement surfaces to the primary desktop workspace route.", root.GetProperty("task").GetString());

        JsonElement checks = root.GetProperty("checks");
        Assert.IsTrue(checks.GetProperty("registry_has_m113_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_path_matches").GetBoolean());

        JsonElement sourceChecks = root.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.EventHandlers.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/App.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m113-ui-gm-prep-roster-surface-check.sh"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.EventHandlers.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ToolStripControl.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M113GmPrepRosterSurfaceGuardTests.cs"),
            },
            ReadStringArray(root.GetProperty("proofFiles")));

        JsonElement proofCommands = root.GetProperty("proofCommands");
        Assert.AreEqual(
            "bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh",
            proofCommands.GetProperty("directProofCommand").GetString());
        Assert.AreEqual(
            "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M113GmPrepRosterSurfaceGuardTests\" --no-restore",
            proofCommands.GetProperty("targetedTestCommand").GetString());
        Assert.AreEqual(
            "dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore",
            proofCommands.GetProperty("presentationTestCommand").GetString());
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
