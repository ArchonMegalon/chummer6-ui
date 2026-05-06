#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M118OrganizerOperationsGuardTests
{
    [TestMethod]
    public void M118_organizer_operations_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m118-ui-organizer-ops-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M118 organizer desktop operations guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m118-ui-organizer-ops-check.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m118-ui-organizer-ops\"");
        StringAssert.Contains(scriptText, "TITLE = \"Surface organizer operations on desktop without confusing GM, player, creator, and operator roles.\"");
        StringAssert.Contains(scriptText, "TASK = \"Surface organizer operations on desktop without confusing GM, player, creator, and operator roles.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 2639996822");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"118.2\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"organizer_ops:desktop\"");
        StringAssert.Contains(scriptText, "\"organizer_roles_ui\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON = \"M118 chummer6-ui organizer desktop operations are complete; future shards must verify the\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m118-ui-organizer-ops-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M118OrganizerOperationsGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "\"Open Organizer Operations\"");
        StringAssert.Contains(scriptText, "\"Review Organizer Roles\"");
        StringAssert.Contains(scriptText, "\"Organizer lane:\"");
        StringAssert.Contains(scriptText, "\"Operator packet lane:\"");
        StringAssert.Contains(scriptText, "Desktop organizer operations surface requires an IChummerClient instance.");

        StringAssert.Contains(projectText, "Compliance\\Next90M118OrganizerOperationsGuardTests.cs");
    }

    [TestMethod]
    public void M118_organizer_operations_receipt_proves_desktop_surface_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M118_UI_ORGANIZER_OPS.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m118_ui_organizer_ops", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m118-ui-organizer-ops", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(2639996822, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(118, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("118.2", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W13", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "organizer_ops:desktop", "organizer_roles_ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m118_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_owner_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_evidence_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_frontier_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_frontier_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_task_matches").GetBoolean());
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
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/App.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M118_UI_ORGANIZER_OPS.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m118-ui-organizer-ops-check.sh"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M118OrganizerOperationsGuardTests.cs"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));

        JsonElement closedPackage = evidence.GetProperty("closedPackage");
        Assert.AreEqual("verify_closed_package_only", closedPackage.GetProperty("completionAction").GetString());
        StringAssert.Contains(closedPackage.GetProperty("doNotReopenReason").GetString(), "M118 chummer6-ui organizer desktop operations are complete");
        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M118OrganizerOperationsGuardTests.cs"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M118_UI_ORGANIZER_OPS.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m118-ui-organizer-ops-check.sh"),
                "bash scripts/ai/milestones/next90-m118-ui-organizer-ops-check.sh",
                "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M118OrganizerOperationsGuardTests\" --no-restore",
                "dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore",
            },
            ReadStringArray(closedPackage.GetProperty("proof")));
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
