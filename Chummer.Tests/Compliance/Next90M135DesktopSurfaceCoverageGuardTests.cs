#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M135DesktopSurfaceCoverageGuardTests
{
    [TestMethod]
    public void M135_desktop_surface_coverage_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m135-ui-desktop-surface-coverage-check.sh"));

        StringAssert.Contains(verifyScript, "checking next-90 M135 desktop surface coverage closure guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m135-ui-desktop-surface-coverage-check.sh");
        StringAssert.Contains(verifyScript, "checking veteran task-time evidence gate");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m135-ui-close-desktop-workbench-build-lab-gm-runboard-publicatio\"");
        StringAssert.Contains(scriptText, "TITLE = \"Close desktop workbench, Build Lab, GM Runboard, publication, restore, support, and veteran-familiarity surface coverage.\"");
        StringAssert.Contains(scriptText, "TASK = \"Close desktop workbench, Build Lab, GM Runboard, publication, restore, support, and veteran-familiarity surface coverage.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 8351771106");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"135.6\"");
        StringAssert.Contains(scriptText, "\"close_desktop_workbench_build_lab:ui\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M114_UI_RULE_STUDIO.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M118_UI_ORGANIZER_OPS.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json");
        StringAssert.Contains(scriptText, "DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics");
        StringAssert.Contains(scriptText, "CreateSection(\"Amend-package lifecycle\"");
        StringAssert.Contains(scriptText, "CreateButton(\"Open GM Runboard\", OpenGmRunboardAsync)");
        StringAssert.Contains(projectText, "Compliance\\Next90M135DesktopSurfaceCoverageGuardTests.cs");
    }

    [TestMethod]
    public void M135_desktop_surface_coverage_receipt_proves_closure_bundle()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m135_ui_desktop_surface_coverage", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m135-ui-close-desktop-workbench-build-lab-gm-runboard-publicatio", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(8351771106, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(135, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("135.6", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W22", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "close_desktop_workbench_build_lab:ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m135_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_owner_matches").GetBoolean());
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
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCreatorPublicationWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopSupportWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/App.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/verify.sh"));

        JsonElement subReceiptChecks = evidence.GetProperty("subReceiptChecks");
        AssertSubReceiptPass(subReceiptChecks.GetProperty("restoreContinuity"));
        AssertSubReceiptPass(subReceiptChecks.GetProperty("ruleStudio"));
        AssertSubReceiptPass(subReceiptChecks.GetProperty("creatorPublication"));
        AssertSubReceiptPass(subReceiptChecks.GetProperty("organizerOperations"));
        AssertSubReceiptPass(subReceiptChecks.GetProperty("gmRunboard"));
        AssertSubReceiptPass(subReceiptChecks.GetProperty("desktopExplainFollowUp"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRuleEnvironmentStudioWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopSupportWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M135DesktopSurfaceCoverageGuardTests.cs"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M114_UI_RULE_STUDIO.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M118_UI_ORGANIZER_OPS.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "veteran-task-time-evidence-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m135-ui-desktop-surface-coverage-check.sh"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));

        JsonElement closedPackage = evidence.GetProperty("closedPackage");
        Assert.AreEqual("verify_closed_package_only", closedPackage.GetProperty("completionAction").GetString());
        StringAssert.Contains(closedPackage.GetProperty("doNotReopenReason").GetString(), "M135 chummer6-ui desktop surface coverage is complete");
        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRuleEnvironmentStudioWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopSupportWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M135DesktopSurfaceCoverageGuardTests.cs"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M114_UI_RULE_STUDIO.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M118_UI_ORGANIZER_OPS.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "veteran-task-time-evidence-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m135-ui-desktop-surface-coverage-check.sh"),
                "bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
                "bash scripts/ai/milestones/next90-m135-ui-desktop-surface-coverage-check.sh",
                "dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M135DesktopSurfaceCoverageGuardTests\" --no-restore",
                "dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore",
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

    private static void AssertSubReceiptPass(JsonElement subReceipt)
    {
        Assert.IsTrue(subReceipt.GetProperty("exists").GetBoolean());
        Assert.IsTrue(subReceipt.GetProperty("statusPass").GetBoolean());
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
