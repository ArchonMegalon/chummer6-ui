#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M117ArtifactShelfGuardTests
{
    [TestMethod]
    public void M117_artifact_shelf_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m117-ui-artifact-shelf-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M117 desktop artifact shelf entrypoint guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m117-ui-artifact-shelf\"");
        StringAssert.Contains(scriptText, "QUEUE_TITLE = \"Add artifact shelf entry points to desktop surfaces\"");
        StringAssert.Contains(scriptText, "REGISTRY_TITLE = \"Close desktop artifact shelf and public proof shelf entry points across home, campaign, build, and publication surfaces.\"");
        StringAssert.Contains(scriptText, "TASK = \"Expose artifact shelves from desktop home, campaign, build, and publication surfaces without hiding source truth.\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 3393065971");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"117.3\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"artifact_shelf:desktop\"");
        StringAssert.Contains(scriptText, "\"public_proof_shelf:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON = \"M117 chummer6-ui desktop artifact shelf entry points are complete; future shards must verify the\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M117ArtifactShelfGuardTests|FullyQualifiedName~Next90M116CreatorPublicationGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "\"Open Public Files\"");
        StringAssert.Contains(scriptText, "\"Open Creator Files\"");
        StringAssert.Contains(scriptText, "OpenArtifactShelfView(\"public\")");
        StringAssert.Contains(scriptText, "OpenArtifactShelfView(\"creator\")");
    }

    [TestMethod]
    public void M117_artifact_shelf_receipt_proves_desktop_surface_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M117_UI_ARTIFACT_SHELF.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m117_ui_artifact_shelf", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m117-ui-artifact-shelf", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(3393065971, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(117, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("117.3", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W13", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "artifact_shelf:desktop", "public_proof_shelf:desktop" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_has_m117_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_owner_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_status_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_evidence_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_work_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_milestone_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
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
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignArtifactWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCreatorPublicationWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M117_UI_ARTIFACT_SHELF.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m117-ui-artifact-shelf-check.sh"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignArtifactWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M117ArtifactShelfGuardTests.cs"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));

        JsonElement closedPackage = evidence.GetProperty("closedPackage");
        Assert.AreEqual("verify_closed_package_only", closedPackage.GetProperty("completionAction").GetString());
        StringAssert.Contains(closedPackage.GetProperty("doNotReopenReason").GetString(), "M117 chummer6-ui desktop artifact shelf entry points are complete");
        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignArtifactWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M117ArtifactShelfGuardTests.cs"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M117_UI_ARTIFACT_SHELF.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m117-ui-artifact-shelf-check.sh"),
                "bash scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh",
                "dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M117ArtifactShelfGuardTests|FullyQualifiedName~Next90M116CreatorPublicationGuardTests\" --no-restore",
                "dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore",
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
