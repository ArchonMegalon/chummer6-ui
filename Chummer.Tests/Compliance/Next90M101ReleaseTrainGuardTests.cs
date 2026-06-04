#nullable enable annotations

using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M101ReleaseTrainGuardTests
{
    private static readonly string[] RequiredPlatforms = { "linux", "windows", "macos" };
    private static readonly string[] RequiredPlatformHeadRidTuples =
    {
        "avalonia:linux-x64:linux",
        "avalonia:osx-arm64:macos",
        "avalonia:win-x64:windows",
    };

    [TestMethod]
    public void M101_release_train_guard_fail_closes_missing_completed_queue_proof()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m101-ui-release-train-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "FRONTIER_ID = 2450443084");
        StringAssert.Contains(scriptText, "EXPECTED_QUEUE_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "queueProofItemsMatchDesignQueue");
        StringAssert.Contains(scriptText, "Fleet and design-owned M101 queue proof items must match exactly.");
        StringAssert.Contains(scriptText, "EXPECTED_PACKAGE_TITLE = \"Keep native-host release proof independent for the primary desktop head\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON");
        StringAssert.Contains(scriptText, "source_queue_fingerprint_matches_design_queue");
        StringAssert.Contains(scriptText, "source_queue_fingerprint_matches_fleet_queue");
        StringAssert.Contains(scriptText, "authority_proof_item_in_scope");
        StringAssert.Contains(scriptText, "\"authority_repo_available\"");
        StringAssert.Contains(scriptText, "\"authority_repo_has_dedicated_history\"");
        StringAssert.Contains(scriptText, "\"primaryProofIndependentFromFallback\"");
        StringAssert.Contains(scriptText, "routeRoleReasonCode");
        StringAssert.Contains(scriptText, "publicInstallRoute");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_m101_primary_route_fallback_proof_leak()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string milestoneScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m101-ui-release-train-check.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);
        string milestoneScriptText = File.ReadAllText(milestoneScriptPath);

        StringAssert.Contains(verifyScriptText, "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh");
        StringAssert.Contains(milestoneScriptText, "Fleet and design-owned M101 queue proof items must match exactly.");
        StringAssert.Contains(milestoneScriptText, "source_queue_fingerprint_matches_fleet_queue");
        StringAssert.Contains(milestoneScriptText, "primaryProofIndependentFromFallback");
        StringAssert.Contains(milestoneScriptText, "routeRoleReasonCode");
        StringAssert.Contains(milestoneScriptText, "publicInstallRoute");
        StringAssert.Contains(milestoneScriptText, "promotedPlatformHeads");
        StringAssert.Contains(milestoneScriptText, "desktopRouteTruth contains unexpected primary/fallback route rows outside the required M101 platforms");
        StringAssert.Contains(milestoneScriptText, "avalonia desktopRouteTruth artifactId does not match promoted avalonia installer artifact");
        StringAssert.Contains(milestoneScriptText, "\"receipt_artifact_path_avoids_fallback_head\"");
    }

    [TestMethod]
    public void M101_release_train_receipt_keeps_avalonia_independent_on_all_platforms()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M101_UI_RELEASE_TRAIN.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("next90-m101-ui-release-train", root.GetProperty("packageId").GetString());
        Assert.AreEqual(2450443084, root.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(101, root.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("c9c0d84f", root.GetProperty("landedCommit").GetString());
        Assert.AreEqual("362686fb", root.GetProperty("currentPackageProofFloorCommit").GetString());
        Assert.AreEqual("verify_closed_package_only", root.GetProperty("completionAction").GetString());
        Assert.IsTrue(root.GetProperty("doNotReopenClosedPackage").GetBoolean());
        Assert.IsTrue(root.GetProperty("primaryProofIndependentFromFallback").GetBoolean());
        Assert.AreEqual(0, root.GetProperty("reasons").GetArrayLength());
        CollectionAssert.AreEquivalent(
            new[] { "desktop_release_train:avalonia", "flagship_route_truth:desktop" },
            root.GetProperty("ownedSurfaces").EnumerateArray().Select(surface => surface.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            root.GetProperty("allowedPaths").EnumerateArray().Select(path => path.GetString()).ToArray());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(2450443084, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual("verify_closed_package_only", evidence.GetProperty("completionAction").GetString());
        Assert.IsTrue(evidence.GetProperty("doNotReopenClosedPackage").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("primaryProofIndependentFromFallback").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("blockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("encodedBlockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("hexEncodedBlockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("escapedBlockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual("case_insensitive", evidence.GetProperty("blockedActiveRunProofScanMode").GetString());

        JsonElement gitChecks = evidence.GetProperty("gitChecks");
        Assert.IsTrue(gitChecks.GetProperty("authority_repo_available").GetBoolean());
        Assert.IsTrue(gitChecks.GetProperty("resolving_proof_commits").TryGetProperty("c9c0d84f", out _));
        Assert.IsTrue(gitChecks.GetProperty("resolving_proof_commits").TryGetProperty("362686fb", out _));
        Assert.IsTrue(gitChecks.GetProperty("resolving_proof_commits").TryGetProperty("2e87dce3", out _));
        Assert.AreEqual(0, gitChecks.GetProperty("queue_proof_commit_tokens").GetArrayLength());
        Assert.AreEqual(0, gitChecks.GetProperty("queue_proof_commit_tokens_resolve").EnumerateObject().Count());
        Assert.IsTrue(gitChecks.GetProperty("authority_row_proof_path_tokens").EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m101-ui-release-train-check.sh"));
        Assert.IsTrue(gitChecks.GetProperty("authority_row_proof_path_tokens").EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M101ReleaseTrainGuardTests.cs"));
        Assert.IsTrue(gitChecks.GetProperty("authority_row_proof_items").EnumerateArray().Any(item => item.GetString() == "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true"));

        CollectionAssert.AreEquivalent(
            RequiredPlatforms,
            evidence.GetProperty("expectedRequiredDesktopPlatforms").EnumerateArray().Select(platform => platform.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "avalonia" },
            evidence.GetProperty("expectedRequiredDesktopHeads").EnumerateArray().Select(head => head.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            RequiredPlatforms,
            evidence.GetProperty("requiredDesktopPlatforms").EnumerateArray().Select(platform => platform.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            RequiredPlatformHeadRidTuples,
            evidence.GetProperty("requiredDesktopPlatformHeadRidTuples").EnumerateArray().Select(tuple => tuple.GetString()).ToArray());

        JsonElement platformResults = evidence.GetProperty("platformResults");
        foreach (string platform in RequiredPlatforms)
        {
            JsonElement result = platformResults.GetProperty(platform);
            Assert.AreEqual("pass", result.GetProperty("status").GetString(), platform);
            Assert.AreEqual("avalonia", result.GetProperty("proofHead").GetString(), platform);
            Assert.AreEqual("avalonia", result.GetProperty("primaryPromotedHead").GetString(), platform);
            Assert.AreEqual("fallback", result.GetProperty("fallbackRouteRole").GetString(), platform);
            Assert.AreEqual(result.GetProperty("expectedRouteTupleId").GetString(), result.GetProperty("routeTupleId").GetString(), platform);
            Assert.AreEqual(result.GetProperty("expectedPublicInstallRoute").GetString(), result.GetProperty("publicInstallRoute").GetString(), platform);
            Assert.AreEqual(0, result.GetProperty("startupSmokeReceiptFallbackTokenHits").GetArrayLength(), platform);
            Assert.AreEqual(0, result.GetProperty("primaryRouteTruthFallbackTokenHits").GetArrayLength(), platform);
            Assert.AreEqual(0, result.GetProperty("primaryRouteTruthFallbackDistinctFieldHits").GetArrayLength(), platform);

            JsonElement startupChecks = result.GetProperty("startupSmokeReceiptIndependenceChecks");
            Assert.IsTrue(startupChecks.GetProperty("receipt_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_artifact_id_matches_primary_artifact_when_present").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_primary_artifact_locator_present").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_primary_artifact_locator_names_primary_head").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_process_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_artifact_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_file_name_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(startupChecks.GetProperty("receipt_all_scalar_fields_avoid_fallback_head").GetBoolean(), platform);

            JsonElement requiredFieldChecks = result.GetProperty("primaryRouteTruthRequiredFieldChecks");
            Assert.IsTrue(requiredFieldChecks.GetProperty("primary_route_truth_artifactId_present").GetBoolean(), platform);
            Assert.IsTrue(requiredFieldChecks.GetProperty("primary_route_truth_publicInstallRoute_present").GetBoolean(), platform);
            Assert.IsTrue(requiredFieldChecks.GetProperty("primary_route_truth_routeRoleReason_present").GetBoolean(), platform);
            Assert.IsTrue(requiredFieldChecks.GetProperty("primary_route_truth_tupleId_present").GetBoolean(), platform);

            JsonElement routeChecks = result.GetProperty("primaryRouteTruthIndependenceChecks");
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_artifactId_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_publicInstallRoute_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_routeRoleReason_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_tupleId_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_all_scalar_fields_avoid_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_proof_fields_distinct_from_fallback_row").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_rollback_state_matches_fallback_promotion_truth").GetBoolean(), platform);
            Assert.IsTrue(routeChecks.GetProperty("primary_route_truth_rollback_reason_matches_fallback_promotion_truth").GetBoolean(), platform);
        }
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate Chummer.sln from the current test directory.");
        return string.Empty;
    }
}
