#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M141DirectImportRouteProofGuardTests
{
    [TestMethod]
    public void M141_direct_import_route_proof_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string guardScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m141-ui-direct-import-route-proof-check.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking next-90 M141 direct import-route proof guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh");

        StringAssert.Contains(guardScript, "PACKAGE_ID = \"next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment\"");
        StringAssert.Contains(guardScript, "FRONTIER_ID = 2354698282");
        StringAssert.Contains(guardScript, "WORK_TASK_ID = \"141.1\"");
        StringAssert.Contains(guardScript, "EXPECTED_STATUS = \"complete\"");
        StringAssert.Contains(guardScript, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(guardScript, "EXPECTED_DO_NOT_REOPEN_REASON = \"M141 chummer6-ui translator, XML amendment, and Hero Lab direct route proof is complete;");
        StringAssert.Contains(guardScript, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh\"");
        StringAssert.Contains(guardScript, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests\" --no-restore'");
        StringAssert.Contains(guardScript, "EXPECTED_DESIGN_QUEUE_PATH = \"/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml\"");
        StringAssert.Contains(guardScript, "\"38-translator-dialog-light.png\"");
        StringAssert.Contains(guardScript, "\"39-xml-editor-dialog-light.png\"");
        StringAssert.Contains(guardScript, "\"40-hero-lab-importer-dialog-light.png\"");
        StringAssert.Contains(guardScript, "\"translator_xml_custom_data\"");
        StringAssert.Contains(guardScript, "\"hero_lab_import_oracle\"");
        StringAssert.Contains(guardScript, "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json");
        StringAssert.Contains(guardScript, "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json");
        StringAssert.Contains(guardScript, "UI_FLAGSHIP_RELEASE_GATE.generated.json");
        StringAssert.Contains(guardScript, "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json");
        StringAssert.Contains(guardScript, "RELEASE_CHANNEL.generated.json");
        StringAssert.Contains(guardScript, "CHUMMER_FLAGSHIP_FRONTIER_PATH");
        StringAssert.Contains(guardScript, "CHUMMER_FLAGSHIP_FRONTIER_ROOT");
        StringAssert.Contains(guardScript, "CHUMMER_FLAGSHIP_FRONTIER_ID");
        StringAssert.Contains(guardScript, "default_flagship_frontier_path");
        StringAssert.Contains(guardScript, "\"release_channel_is_preview\"");
        StringAssert.Contains(guardScript, "\"release_channel_version_present\"");
        StringAssert.Contains(guardScript, "\"frontier_artifact_path_under_root\"");
        StringAssert.Contains(guardScript, "\"frontier_artifact_uses_shard_generated_yaml\"");
        StringAssert.Contains(guardScript, "shard-1.generated.yaml");
        StringAssert.Contains(guardScript, "FLAGSHIP_FRONTIER_ID = 1922169755");
        StringAssert.Contains(guardScript, "\"flagshipFrontier\"");
        Assert.IsFalse(
            guardScript.Contains("full-product-frontiers/shard-2.generated.yaml", StringComparison.Ordinal),
            "Direct import-route proof must resolve the active flagship frontier artifact instead of pinning shard-2.");

        StringAssert.Contains(projectText, "Compliance\\Next90M141DirectImportRouteProofGuardTests.cs");
    }

    [TestMethod]
    public void M141_direct_import_route_proof_receipt_proves_current_route_coverage()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m141_ui_direct_import_route_proof", root.GetProperty("contract_name").GetString());
        Assert.AreEqual("preview", root.GetProperty("channelId").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(2354698282, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(141, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("141.1", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W22P", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());
        Assert.AreEqual(1922169755, evidence.GetProperty("flagshipFrontierId").GetInt64());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "capture_direct_screenshot_and_runtime_proof_for_translat:ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement queueChecks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(queueChecks.GetProperty("registry_markers_present").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_milestone_present").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_milestone_title_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_owner_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_title_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_status_complete").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_completion_action_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("registry_task_evidence_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_task_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_frontier_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_frontier_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_work_task_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_work_task_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_status_complete").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_status_complete").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_wave_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_wave_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_repo_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_repo_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_completion_action_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_do_not_reopen_reason_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_proof_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_proof_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_design_block_parity").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_path_matches").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("queue_worker_safe").GetBoolean());
        Assert.IsTrue(queueChecks.GetProperty("design_queue_worker_safe").GetBoolean());

        JsonElement flagshipFrontierChecks = evidence.GetProperty("flagshipFrontierChecks");
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("frontier_artifact_present").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("frontier_artifact_path_under_root").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("frontier_artifact_uses_shard_generated_yaml").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("frontier_id_present").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("queue_package_present").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("title_present").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("owned_surface_present").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(flagshipFrontierChecks.GetProperty("worker_safe").GetBoolean());

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Presentation/Overview/OverviewCommandDispatcher.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Presentation/Overview/DesktopDialogFactory.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/milestones/b14-flagship-ui-release-gate.sh"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/milestones/chummer5a-screenshot-review-gate.sh"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/verify.sh"));

        JsonElement receiptChecks = evidence.GetProperty("receiptChecks");
        Assert.IsTrue(receiptChecks.GetProperty("release_channel_is_preview").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("release_channel_version_present").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("visual_familiarity_gate_pass").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("visual_required_screenshots_present").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("visual_missing_screenshots_clear").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("visual_screenshot_dir_exists").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("screenshot_review_gate_pass").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("screenshot_review_jobs_present").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("veteran_task_gate_pass").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("veteran_task_jobs_present").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("veteran_task_screenshot_jobs_present").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("ui_flagship_gate_pass").GetBoolean());
        Assert.IsTrue(receiptChecks.GetProperty("ui_flagship_gate_tokens_present").GetBoolean());

        JsonElement routeReceiptChecks = evidence.GetProperty("routeReceiptChecks");
        AssertRouteReceiptPass(routeReceiptChecks.GetProperty("translator_xml_custom_data"));
        AssertRouteReceiptPass(routeReceiptChecks.GetProperty("hero_lab_import_oracle"));

        JsonElement screenshotFiles = evidence.GetProperty("screenshotFiles");
        Assert.IsTrue(screenshotFiles.GetProperty("38-translator-dialog-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("39-xml-editor-dialog-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("40-hero-lab-importer-dialog-light.png").GetBoolean());

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "CharacterOverviewPresenterTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "DesktopDialogFactoryTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "DualHeadAcceptanceTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M141DirectImportRouteProofGuardTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "chummer5a-screenshot-review-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "veteran-task-time-evidence-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m141-ui-direct-import-route-proof-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
                "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests\" --no-restore",
            },
            ReadStringArray(evidence.GetProperty("proofCommands")));

        JsonElement supportingReceipts = evidence.GetProperty("supportingReceipts");
        string releaseChannelPath = supportingReceipts.GetProperty("releaseChannel").GetString() ?? string.Empty;
        string flagshipFrontierPath = supportingReceipts.GetProperty("flagshipFrontier").GetString() ?? string.Empty;
        StringAssert.Contains(releaseChannelPath, "RELEASE_CHANNEL.generated.json");
        StringAssert.Contains(flagshipFrontierPath, "full-product-frontiers");
        Assert.IsTrue(File.Exists(releaseChannelPath), $"Release channel receipt is missing: {releaseChannelPath}");
        Assert.IsTrue(File.Exists(flagshipFrontierPath), $"Flagship frontier receipt is missing: {flagshipFrontierPath}");

        using JsonDocument releaseChannel = JsonDocument.Parse(File.ReadAllText(releaseChannelPath));
        Assert.AreEqual("preview", releaseChannel.RootElement.GetProperty("channelId").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(releaseChannel.RootElement.GetProperty("version").GetString()));

        string flagshipFrontierText = File.ReadAllText(flagshipFrontierPath);
        StringAssert.Contains(flagshipFrontierText, "frontier_ids:");
        StringAssert.Contains(flagshipFrontierText, "- 1922169755");
        StringAssert.Contains(flagshipFrontierText, "package_id: next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment");
        StringAssert.Contains(flagshipFrontierText, "Capture direct screenshot and runtime proof for translator, XML amendment");
        Assert.IsFalse(
            flagshipFrontierText.Contains("TASK_LOCAL_TELEMETRY.generated.json", StringComparison.OrdinalIgnoreCase),
            "Flagship frontier proof must stay worker-safe and must not cite task-local telemetry helper output.");
        Assert.IsFalse(
            flagshipFrontierText.Contains("ACTIVE_RUN_HANDOFF.generated.md", StringComparison.OrdinalIgnoreCase),
            "Flagship frontier proof must stay worker-safe and must not cite shard handoff helper output.");
    }

    private static void AssertSourceMarkersPass(JsonElement sourceChecks)
    {
        foreach (JsonProperty markerCheck in sourceChecks.EnumerateObject())
        {
            Assert.IsTrue(markerCheck.Value.GetBoolean(), $"Expected source marker to pass: {markerCheck.Name}");
        }
    }

    private static void AssertRouteReceiptPass(JsonElement routeReceiptChecks)
    {
        Assert.IsTrue(routeReceiptChecks.GetProperty("exists").GetBoolean());
        Assert.IsTrue(routeReceiptChecks.GetProperty("status_pass").GetBoolean());
        Assert.IsTrue(routeReceiptChecks.GetProperty("route_ids_exact").GetBoolean());
        Assert.IsTrue(routeReceiptChecks.GetProperty("workflow_family_matches").GetBoolean());
        Assert.IsTrue(routeReceiptChecks.GetProperty("screenshots_exact").GetBoolean());
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
