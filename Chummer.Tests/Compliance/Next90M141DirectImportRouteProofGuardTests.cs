#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        StringAssert.Contains(guardScript, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests\" --no-restore'");
        StringAssert.Contains(guardScript, "EXPECTED_DESIGN_QUEUE_PATH = f\"{repo_root}/.codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml\"");
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
        StringAssert.Contains(guardScript, "\"release_channel_id_present\"");
        StringAssert.Contains(guardScript, "\"release_channel_version_present\"");
        StringAssert.Contains(guardScript, "\"frontier_artifact_path_under_root\"");
        StringAssert.Contains(guardScript, "\"frontier_artifact_uses_shard_generated_yaml\"");
        StringAssert.Contains(guardScript, "shard-1.generated.yaml");
        StringAssert.Contains(guardScript, "FLAGSHIP_FRONTIER_ID = 1922169755");
        StringAssert.Contains(guardScript, "\"flagshipFrontier\"");
        StringAssert.Contains(guardScript, "os.O_NOFOLLOW");
        StringAssert.Contains(guardScript, "revalidate_snapshots()");
        StringAssert.Contains(guardScript, "atomic_write_receipt(receipt)");
        StringAssert.Contains(guardScript, "\"schemaVersion\": 1");
        StringAssert.Contains(guardScript, "\"producerRunId\": str(uuid.uuid4())");
        Assert.IsFalse(
            guardScript.Contains("SKIP_FLAGSHIP_GATE_DEPENDENCY", StringComparison.Ordinal),
            "Direct import-route proof must not waive a failing flagship dependency.");
        Assert.IsFalse(
            guardScript.Contains("top_level_review_jobs", StringComparison.Ordinal),
            "Direct import-route proof must require direct route-local receipts instead of synthesizing them from top-level jobs.");
        Assert.IsFalse(
            guardScript.Contains(".glob(", StringComparison.Ordinal),
            "Direct import-route proof must not discover mutable frontier shards by content or glob order.");
        Assert.IsFalse(
            guardScript.Contains(" -nt ", StringComparison.Ordinal),
            "Direct import-route proof must not choose release authority by mutable file mtime.");
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

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.IsTrue(Guid.TryParse(root.GetProperty("producerRunId").GetString(), out _));
        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m141_ui_direct_import_route_proof", root.GetProperty("contract_name").GetString());
        string receiptChannelId = root.GetProperty("channelId").GetString() ?? string.Empty;
        Assert.IsFalse(string.IsNullOrWhiteSpace(receiptChannelId));
        Assert.AreEqual(receiptChannelId, root.GetProperty("channel").GetString());
        string receiptVersion = root.GetProperty("version").GetString() ?? string.Empty;
        Assert.IsFalse(string.IsNullOrWhiteSpace(receiptVersion));
        Assert.AreEqual(receiptVersion, root.GetProperty("releaseVersion").GetString());

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

        AssertAllChecksPass(evidence.GetProperty("flagshipFrontierChecks"));
        AssertAllChecksPass(evidence.GetProperty("flagshipQueueChecks"));

        AssertAllChecksPass(evidence.GetProperty("releaseChannelChecks"));
        foreach (JsonProperty supportingCheck in evidence.GetProperty("supportingReceiptChecks").EnumerateObject())
        {
            AssertAllChecksPass(supportingCheck.Value);
        }

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
        Assert.IsTrue(receiptChecks.GetProperty("release_channel_id_present").GetBoolean());
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

        string publishedRepoRoot = Directory.Exists(Path.Combine(Directory.GetParent(repoRoot)?.FullName ?? repoRoot, "chummer6-ui"))
            ? Path.Combine(Directory.GetParent(repoRoot)?.FullName ?? repoRoot, "chummer6-ui")
            : repoRoot;
        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Presentation", "CharacterOverviewPresenterTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Presentation", "DesktopDialogFactoryTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Presentation", "DualHeadAcceptanceTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Compliance", "Next90M141DirectImportRouteProofGuardTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "chummer5a-screenshot-review-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "veteran-task-time-evidence-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "next90-m141-ui-direct-import-route-proof-check.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "verify.sh"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));
        Assert.AreEqual(
            Path.Combine(publishedRepoRoot, ".codex-studio", "published", "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"),
            evidence.GetProperty("informationalOutputPath").GetString());

        JsonElement bindings = evidence.GetProperty("bindings");
        AssertBinding(bindings.GetProperty("registry"));
        AssertBinding(bindings.GetProperty("queue"));
        AssertBinding(bindings.GetProperty("designQueue"));
        AssertBinding(bindings.GetProperty("releaseChannel"));
        AssertBinding(bindings.GetProperty("flagshipFrontier"));
        AssertBinding(bindings.GetProperty("flagshipQueue"));
        Assert.AreEqual(4, bindings.GetProperty("supportingReceipts").EnumerateObject().Count());
        Assert.AreEqual(3, bindings.GetProperty("screenshots").EnumerateObject().Count());
        Assert.IsFalse(bindings.GetProperty("proofFiles").TryGetProperty(
            Path.Combine(publishedRepoRoot, ".codex-studio", "published", "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"),
            out _));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
                "dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests\" --no-restore",
            },
            ReadStringArray(evidence.GetProperty("proofCommands")));

        JsonElement supportingReceipts = evidence.GetProperty("supportingReceipts");
        string releaseChannelPath = supportingReceipts.GetProperty("releaseChannel").GetString() ?? string.Empty;
        string flagshipFrontierPath = supportingReceipts.GetProperty("flagshipFrontier").GetString() ?? string.Empty;
        string flagshipQueuePath = supportingReceipts.GetProperty("flagshipQueue").GetString() ?? string.Empty;
        StringAssert.Contains(releaseChannelPath, "RELEASE_CHANNEL.generated.json");
        Assert.IsTrue(
            flagshipFrontierPath.Contains("full-product-frontiers", StringComparison.Ordinal)
            || flagshipFrontierPath.EndsWith("FULL_PRODUCT_FRONTIER.generated.yaml", StringComparison.Ordinal),
            $"Unexpected flagship frontier receipt path: {flagshipFrontierPath}");
        Assert.IsTrue(File.Exists(releaseChannelPath), $"Release channel receipt is missing: {releaseChannelPath}");
        Assert.IsTrue(File.Exists(flagshipFrontierPath), $"Flagship frontier receipt is missing: {flagshipFrontierPath}");
        Assert.IsTrue(
            flagshipQueuePath.EndsWith("NEXT_90_DAY_QUEUE_STAGING.generated.yaml", StringComparison.Ordinal),
            $"Unexpected flagship queue receipt path: {flagshipQueuePath}");
        Assert.IsTrue(File.Exists(flagshipQueuePath), $"Flagship queue receipt is missing: {flagshipQueuePath}");

        using JsonDocument releaseChannel = JsonDocument.Parse(File.ReadAllText(releaseChannelPath));
        Assert.AreEqual(receiptChannelId, releaseChannel.RootElement.GetProperty("channelId").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(releaseChannel.RootElement.GetProperty("version").GetString()));

        string flagshipFrontierText = File.ReadAllText(flagshipFrontierPath);
        StringAssert.Contains(flagshipFrontierText, "contract_name: fleet.full_product_frontier");
        StringAssert.Contains(flagshipFrontierText, "bar: top_flagship_grade");
        StringAssert.Contains(flagshipFrontierText, "accept_lowered_standards: false");

        string flagshipQueueText = File.ReadAllText(flagshipQueuePath);
        StringAssert.Contains(flagshipQueueText, "status: live_parallel_successor");
        StringAssert.Contains(flagshipQueueText, "frontier_id: 2354698282");
        StringAssert.Contains(
            flagshipQueueText,
            "package_id: next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment");
        StringAssert.Contains(
            flagshipQueueText,
            "Capture direct screenshot and runtime proof for translator, XML amendment");
        Assert.IsFalse(
            flagshipFrontierText.Contains("TASK_LOCAL_TELEMETRY.generated.json", StringComparison.OrdinalIgnoreCase),
            "Flagship frontier proof must stay worker-safe and must not cite task-local telemetry helper output.");
        Assert.IsFalse(
            flagshipQueueText.Contains("TASK_LOCAL_TELEMETRY.generated.json", StringComparison.OrdinalIgnoreCase),
            "Flagship queue proof must stay worker-safe and must not cite task-local telemetry helper output.");
        Assert.IsFalse(
            flagshipFrontierText.Contains("ACTIVE_RUN_HANDOFF.generated.md", StringComparison.OrdinalIgnoreCase),
            "Flagship frontier proof must stay worker-safe and must not cite shard handoff helper output.");
        Assert.IsFalse(
            flagshipQueueText.Contains("ACTIVE_RUN_HANDOFF.generated.md", StringComparison.OrdinalIgnoreCase),
            "Flagship queue proof must stay worker-safe and must not cite shard handoff helper output.");
    }

    private static void AssertSourceMarkersPass(JsonElement sourceChecks)
    {
        foreach (JsonProperty markerCheck in sourceChecks.EnumerateObject())
        {
            Assert.IsTrue(markerCheck.Value.GetBoolean(), $"Expected source marker to pass: {markerCheck.Name}");
        }
    }

    private static void AssertAllChecksPass(JsonElement checks)
    {
        foreach (JsonProperty check in checks.EnumerateObject())
        {
            Assert.IsTrue(check.Value.GetBoolean(), $"Expected receipt check to pass: {check.Name}");
        }
    }

    private static void AssertBinding(JsonElement binding)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(binding.GetProperty("path").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(binding.GetProperty("resolvedPath").GetString()));
        Assert.AreEqual(64, binding.GetProperty("sha256").GetString()?.Length);
        Assert.IsTrue(binding.GetProperty("sizeBytes").GetInt64() > 0);
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
