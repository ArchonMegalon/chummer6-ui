#nullable enable annotations

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M105RestoreContinuityGuardTests
{
    [TestMethod]
    public void M105_restore_continuity_guard_pins_closed_package_authority_and_live_repo_proof()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m105-ui-restore-continuity-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m105-ui-restore-continuity\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 3787618287");
        StringAssert.Contains(scriptText, "EXPECTED_ALLOWED_PATHS = [");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"restore_continuation:desktop\"");
        StringAssert.Contains(scriptText, "\"conflict_safe_workspace:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DESIGN_QUEUE_PATH = \"/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml\"");
        StringAssert.Contains(scriptText, "EXPECTED_LANDED_COMMIT = \"54c27661\"");
        StringAssert.Contains(scriptText, "Canonical registry and successor queue both mark the M105 UI package complete");
        StringAssert.Contains(scriptText, "REQUIRED_REGISTRY_EVIDENCE_LINES = [");
        StringAssert.Contains(scriptText, "REQUIRED_QUEUE_PROOF_LINES = [");
        StringAssert.Contains(scriptText, "commit fd0fbfd0 tightens the M105 completed queue action guard");
        StringAssert.Contains(scriptText, "commit db9ec722 tightens the M105 workspace-support handoff proof");
        StringAssert.Contains(scriptText, "DISALLOWED_ACTIVE_RUN_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "TASK_LOCAL_TELEMETRY.generated.json");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(scriptText, "operator helper proof check failed");
        StringAssert.Contains(scriptText, "DISALLOWED_SIBLING_PACKAGE_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "next90-m101-ui-release-train");
        StringAssert.Contains(scriptText, "next90-m103-ui-veteran-certification");
        StringAssert.Contains(scriptText, "next90-m104-ui-explain-receipts");
        StringAssert.Contains(scriptText, "next90-m106");
        StringAssert.Contains(scriptText, "sibling package proof check failed");
        StringAssert.Contains(scriptText, "STANDARD_VERIFY_PATH = \"scripts/ai/verify.sh\"");
        StringAssert.Contains(scriptText, "checking next-90 M105 restore-continuity and conflict-safe desktop UX guard");
        StringAssert.Contains(scriptText, "standard verify is missing M105 restore-continuity guard marker(s)");
        StringAssert.Contains(scriptText, "PROOF_RELEVANT_PATHS = [");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopHomeWindow.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/Controls/SummaryHeaderControl.axaml\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Compliance/Next90M105RestoreContinuityGuardTests.cs\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh\"");
        StringAssert.Contains(scriptText, "\".codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json\"");
        StringAssert.Contains(scriptText, "def latest_commit_for_path(relative_path: str) -> dict[str, Any]:");
        StringAssert.Contains(scriptText, "path_presence = [path_presence_for(path) for path in PROOF_RELEVANT_PATHS]");
        StringAssert.Contains(scriptText, "repo_proof_trail = [latest_commit_for_path(path) for path in PROOF_RELEVANT_PATHS]");
        StringAssert.Contains(scriptText, "\"head_commit_present\": bool(head_commit)");
        StringAssert.Contains(scriptText, "\"all_paths_present_in_worktree\": all(entry[\"present\"] for entry in path_presence)");
        StringAssert.Contains(scriptText, "\"all_paths_declared_under_allowed_scope\": all(entry[\"pathAllowed\"] for entry in path_presence)");
        StringAssert.Contains(scriptText, "\"all_historical_paths_ancestor_of_head\": all(entry[\"isAncestorOfHead\"] for entry in repo_proof_trail if entry[\"exists\"])");
        StringAssert.Contains(scriptText, "\"all_historical_paths_have_in_scope_anchor\": all(entry[\"scopeAllowed\"] for entry in repo_proof_trail if entry[\"exists\"])");
        StringAssert.Contains(scriptText, "\"history_optional_for_live_proof_files\": True");
        StringAssert.Contains(scriptText, "repo proof check failed");
        StringAssert.Contains(scriptText, "historicalBranchCommitChecks");
        StringAssert.Contains(scriptText, "retiredForCurrentRepoHistory");
        StringAssert.Contains(scriptText, "currentRepoProofMode");
        StringAssert.Contains(scriptText, "pathPresenceChecks");
        StringAssert.Contains(scriptText, "live_source_and_wiring");
        StringAssert.Contains(scriptText, "registry_review_reasons");
        StringAssert.Contains(scriptText, "queue_review_reasons");
        StringAssert.Contains(scriptText, "proof_hygiene_review_reasons");
        StringAssert.Contains(scriptText, "source_marker_review_reasons");
        StringAssert.Contains(scriptText, "verify_wiring_review_reasons");
        StringAssert.Contains(scriptText, "repo_proof_review_reasons");
        StringAssert.Contains(scriptText, "\"registryClosureReview\"");
        StringAssert.Contains(scriptText, "\"queueClosureReview\"");
        StringAssert.Contains(scriptText, "\"proofHygieneReview\"");
        StringAssert.Contains(scriptText, "\"sourceMarkerReview\"");
        StringAssert.Contains(scriptText, "\"verifyWiringReview\"");
        StringAssert.Contains(scriptText, "\"repoProofReview\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not registry_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not queue_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not proof_hygiene_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not source_marker_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not verify_wiring_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not repo_proof_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"failureCount\": len(reasons)");
    }

    [TestMethod]
    public void M105_restore_continuity_receipt_records_passed_live_repo_guard()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json");
        string receiptText = File.ReadAllText(receiptPath);
        using JsonDocument receipt = JsonDocument.Parse(receiptText);
        JsonElement root = receipt.RootElement;
        JsonElement evidence = root.GetProperty("evidence");
        JsonElement reviews = root.GetProperty("reviews");

        StringAssert.Contains(receiptText, "\"packageId\": \"next90-m105-ui-restore-continuity\"");
        StringAssert.Contains(receiptText, "\"frontierId\": 3787618287");
        StringAssert.Contains(receiptText, "\"status\": \"pass\"");
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.AreEqual("pass", reviews.GetProperty("registryClosureReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("queueClosureReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("proofHygieneReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("sourceMarkerReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("repoProofReview").GetProperty("status").GetString());
        StringAssert.Contains(receiptText, "\"canonicalRegistryComplete\": true");
        StringAssert.Contains(receiptText, "\"canonicalQueueComplete\": true");
        StringAssert.Contains(receiptText, "\"landedCommitPinned\": true");
        StringAssert.Contains(receiptText, "\"sourceMarkerProofRequired\": true");
        StringAssert.Contains(receiptText, "\"supportHandoffProofRequired\": true");
        StringAssert.Contains(receiptText, "\"sourceDesignQueuePathMatches\": true");
        StringAssert.Contains(receiptText, "\"packageBlocksMatch\": true");
        StringAssert.Contains(receiptText, "\"required_proof_avoids_active_run_helpers\": true");
        StringAssert.Contains(receiptText, "\"registry_evidence_avoids_active_run_helpers\": true");
        StringAssert.Contains(receiptText, "\"queue_evidence_avoids_active_run_helpers\": true");
        StringAssert.Contains(receiptText, "\"design_queue_evidence_avoids_active_run_helpers\": true");
        StringAssert.Contains(receiptText, "\"required_proof_avoids_sibling_packages\": true");
        StringAssert.Contains(receiptText, "\"registry_evidence_avoids_sibling_packages\": true");
        StringAssert.Contains(receiptText, "\"queue_evidence_avoids_sibling_packages\": true");
        StringAssert.Contains(receiptText, "\"design_queue_evidence_avoids_sibling_packages\": true");
        StringAssert.Contains(receiptText, "\"wired_into_standard_verify\": true");
        StringAssert.Contains(receiptText, "\"path\": \"scripts/ai/verify.sh\"");
        StringAssert.Contains(receiptText, "\"repoProofChecks\"");
        StringAssert.Contains(receiptText, "\"head_commit_present\": true");
        StringAssert.Contains(receiptText, "\"all_paths_present_in_worktree\": true");
        StringAssert.Contains(receiptText, "\"all_paths_declared_under_allowed_scope\": true");
        StringAssert.Contains(receiptText, "\"all_historical_paths_ancestor_of_head\": true");
        StringAssert.Contains(receiptText, "\"all_historical_paths_have_in_scope_anchor\": true");
        StringAssert.Contains(receiptText, "\"history_optional_for_live_proof_files\": true");
        StringAssert.Contains(receiptText, "\"repoProofTrail\"");
        StringAssert.Contains(receiptText, "\"pathPresenceChecks\"");
        StringAssert.Contains(receiptText, "\"headCommit\":");
        StringAssert.Contains(receiptText, "\"worktreeChecks\"");
        StringAssert.Contains(receiptText, "\"tracked_paths\"");
        StringAssert.Contains(receiptText, "\"historicalBranchCommitChecks\"");
        StringAssert.Contains(receiptText, "\"historicalProofBranch\": \"chummer6-ui-finish\"");
        StringAssert.Contains(receiptText, "\"commitPinsStillResolvableLocally\": false");
        StringAssert.Contains(receiptText, "\"retiredForCurrentRepoHistory\": true");
        StringAssert.Contains(receiptText, "\"currentRepoProofMode\": \"live_source_and_wiring\"");
        StringAssert.Contains(receiptText, "\"sourceMarkers\"");
        StringAssert.Contains(receiptText, "\"BuildCampaignRestoreContinuitySummary()\"");
        StringAssert.Contains(receiptText, "\"BuildRestoreContinuityChoiceSummary()\"");
        StringAssert.Contains(receiptText, "\"Stale state: server continuity is unavailable\"");
        StringAssert.Contains(receiptText, "\"Conflict choices:\"");
        StringAssert.Contains(receiptText, "\"restore-decision-keep-local-work\"");
        StringAssert.Contains(receiptText, "\"restore-decision-review-campaign-workspace\"");
        StringAssert.Contains(receiptText, "\"restore-decision-open-workspace-support\"");
        StringAssert.Contains(receiptText, "\"BuildSupportPortalRelativePathForWorkspace_includes_workspace_follow_through_context\"");
        StringAssert.Contains(receiptText, "\"Restore%20posture%3A%20review%20workspace%20continuation%2C%20stale-state%20visibility%2C%20and%20conflict%20choices%20before%20replacing%20local%20work.\"");
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
