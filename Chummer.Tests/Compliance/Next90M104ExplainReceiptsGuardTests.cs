#nullable enable annotations

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M104ExplainReceiptsGuardTests
{
    // m104: m104_guard_self_closure
    // commit 0a2a321f tightens M104 explain receipt guard wiring
    // commit 2c29f1be tightens M104 explain receipt commit proof
    // commit 1df92955 tightens M104 explain receipt frontier guard
    // commit 7556a33b pins M104 explain receipt proof anchors
    // commit d9e5392d wires the M104 explain receipt guard into standard scripts/ai/verify.sh
    // commit d4d34e1c requires the standard-verify wiring commit as registry and queue proof
    // commit cea19d0d tightens M104 explain receipt proof guard
    // commit f27fefb8 tightens M104 proof commit resolution
    // commit b08d3b2c tightens M104 blocked-helper proof exclusion
    // commit 0a84aef2 pins the M104 blocked-helper proof anchor
    // commit 96125b0e pins the M104 explain receipt proof guard
    // commit c51f8657 pins the current M104 explain receipt proof guard
    // commit d3dfb527 tightens M104 explain receipt proof anchor
    // commit d18aa133 pins M104 explain receipt proof anchor
    // commit 0da2d157 pins M104 explain receipt latest proof anchor
    // commit f494f32f tightens M104 explain receipt proof anchor
    // commit 7ddae55e pins the current M104 explain receipt guard
    // commit 9a4a2ae1 pins M104 proof closure to the 7ddae55e guard
    // commit cb784e7b tightens M104 explain receipt proof floor
    // commit 7d5e8e61 pins the current M104 explain receipt proof floor
    // commit 06819ea3 pins the current M104 explain receipt proof floor
    // commit 208908b7 pins M104 explain receipt current proof floor
    // commit 21ddae58 tightens M104 proof commit citation checks
    // commit 8c7d639f tightens M104 canonical queue closure
    // commit d2650d0b pins M104 explain receipt queue closure guard
    // commit 79b8b594 pins M104 explain receipt current proof floor
    // commit ea689297 pins M104 explain receipt proof floor
    // commit 5a8e0b2a pins M104 explain receipt guard floor
    // commit bfd66025 pins M104 explain receipt current guard floor
    // commit f9607bb8 tightens M104 generated proof hygiene
    // commit 9d302a0e tightens M104 explain receipt proof-path scope
    // commit cb028208 pins M104 explain receipt proof scope
    // commit 5c19e4e3 pins M104 explain receipt proof floor
    // commit c92d8dc4 tightens M104 explain receipt proof floor
    // commit af590503 tightens M104 canonical proof-path scope
    // commit f6049a9d tightens M104 queue and registry uniqueness proof
    // commit 283f8ee3 pins M104 explain receipt uniqueness proof
    // commit 853c807a tightens M104 encoded and escaped worker-context proof guards
    // commit 2f69ed4e tightens M104 explain receipt proof-line uniqueness
    // commit 48337e13 pins the M104 proof-line uniqueness guard in the verifier, compliance test, and generated receipt
    // commit da2c3ab7 pins the M104 explain receipt proof floor so future shards verify the latest completed-package guard
    // commit 68b55e6e tightens M104 desktop diagnostics proof so the completed package stays bound to crash-safe support diagnostics and build blocker receipts
    // commit 083283e9 tightens M104 desktop explain receipt proof so closureGuard, crash diagnostics, and verify_closed_package_only proof stay pinned together
    // commit 41322ad1 tightens M104 desktop trust surfaces and percent-encoded worker-helper proof guards
    // commit fab80234 tightens M104 runtime composer proof so the completed package stays bound to the shared import, build blocker, support diagnostics, and crash diagnostics receipt generator
    // commit c42ed3d3 tightens M104 runtime support receipts so the completed package stays bound to visible runtime-inspector support and compatibility before-after diagnostics
    // commit 7aecc402 tightens M104 quoted-printable worker-helper proof rejection and refreshes the completed-package receipt
    // commit d1e7d545 tightens M104 base32, base85, and compressed worker-helper proof rejection
    // commit a93c0cbf pins M104 explain receipt proof guard so future shards verify the latest completed-package guard
    private static readonly string[] ExpectedSurfaces = ["explain_receipts:desktop", "diagnostics_diff:desktop"];
    private static readonly string[] ExpectedAllowedPaths = ["Chummer.Avalonia", "Chummer.Blazor", "Chummer.Desktop.Runtime", "Chummer.Tests"];
    private static readonly string[] DisallowedActiveRunProofTokens =
    [
        "TASK_LOCAL_TELEMETRY.generated.json",
        "ACTIVE_RUN_HANDOFF.generated.md",
        "scripts/ooda_design_supervisor.py",
        "scripts/run_ooda_design_supervisor_until_quiet.py",
        "operator telemetry",
        "active-run helper",
        "VEFTS19MT0NBTF9URUxFTUVUUlkuZ2VuZXJhdGVkLmpzb24=",
        "QUNUSVZFX1JVTl9IQU5ET0ZGLmdlbmVyYXRlZC5tZA==",
        "b3BlcmF0b3IgdGVsZW1ldHJ5",
        "YWN0aXZlLXJ1biBoZWxwZXI=",
        "5441534b5f4c4f43414c5f54454c454d455452592e67656e6572617465642e6a736f6e",
        "4143544956455f52554e5f48414e444f46462e67656e6572617465642e6d64",
        "6f70657261746f722074656c656d65747279",
        "6163746976652d72756e2068656c706572",
        "TASK&#95;LOCAL&#95;TELEMETRY.generated.json",
        "ACTIVE&#95;RUN&#95;HANDOFF.generated.md",
        "operator&#32;telemetry",
        "active&#45;run&#32;helper",
    ];

    [TestMethod]
    public void M104_explain_receipts_guard_fail_closes_missing_completed_queue_proof()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m104-ui-explain-receipts-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m104-ui-explain-receipts\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 3352869062");
        StringAssert.Contains(scriptText, "frontier_id: {FRONTIER_ID}");
        StringAssert.Contains(scriptText, "EXPECTED_LANDED_COMMIT = \"63f57d62\"");
        StringAssert.Contains(scriptText, "CHUMMER_NEXT90_DESIGN_QUEUE_PATH");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml");
        StringAssert.Contains(scriptText, "queueMirrorChecks");
        StringAssert.Contains(scriptText, "fleet_queue_points_to_design_queue");
        StringAssert.Contains(scriptText, "package_blocks_match");
        StringAssert.Contains(scriptText, "fleet_queue_package_unique");
        StringAssert.Contains(scriptText, "design_queue_package_unique");
        StringAssert.Contains(scriptText, "package_occurrence_count");
        StringAssert.Contains(scriptText, "ui_work_task_unique");
        StringAssert.Contains(scriptText, "localRepoChecks");
        StringAssert.Contains(scriptText, "landed_commit_resolves");
        StringAssert.Contains(scriptText, "EXPECTED_RESOLVING_PROOF_COMMITS");
        StringAssert.Contains(scriptText, "resolving_proof_commits");
        StringAssert.Contains(scriptText, "all_proof_commits_resolve");
        StringAssert.Contains(scriptText, "proof_commits_have_canonical_citations");
        StringAssert.Contains(scriptText, "all_proof_commits_have_canonical_citations");
        StringAssert.Contains(scriptText, "proof commit {commit} is not cited by registry, Fleet queue, or design queue proof");
        StringAssert.Contains(scriptText, "git_object_exists(repo_root, EXPECTED_LANDED_COMMIT)");
        StringAssert.Contains(scriptText, "EXPECTED_ALLOWED_PATHS");
        StringAssert.Contains(scriptText, "PROOF_PATH_EXCEPTIONS");
        StringAssert.Contains(scriptText, "proof_path_scope_checks");
        StringAssert.Contains(scriptText, "proofPathScopeChecks");
        StringAssert.Contains(scriptText, "all_scoped_paths_allowed");
        StringAssert.Contains(scriptText, "proof path scope check failed");
        StringAssert.Contains(scriptText, "canonical_block_proof_path_scope_checks");
        StringAssert.Contains(scriptText, "canonicalProofPathScopeChecks");
        StringAssert.Contains(scriptText, "all_canonical_block_paths_allowed");
        StringAssert.Contains(scriptText, "canonical proof path scope check failed");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Blazor\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "EXPECTED_SURFACES");
        StringAssert.Contains(scriptText, "\"explain_receipts:desktop\"");
        StringAssert.Contains(scriptText, "\"diagnostics_diff:desktop\"");
        StringAssert.Contains(scriptText, "RECEIPT_PROOF_LINES");
        StringAssert.Contains(scriptText, ".codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json");
        StringAssert.Contains(scriptText, "scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh");
        StringAssert.Contains(scriptText, "Chummer.Tests/Compliance/Next90M104ExplainReceiptsGuardTests.cs");
        StringAssert.Contains(scriptText, "Chummer.Tests/Chummer.Tests.csproj");
        StringAssert.Contains(scriptText, "EXPECTED_RESOLVING_PROOF_COMMITS: list[str] = []");
        StringAssert.Contains(scriptText, "proof_commits_have_canonical_citations");
        StringAssert.Contains(scriptText, "all_proof_commits_resolve");
        StringAssert.Contains(scriptText, "DISALLOWED_ACTIVE_RUN_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "TASK_LOCAL_TELEMETRY.generated.json");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(scriptText, "VEFTS19MT0NBTF9URUxFTUVUUlkuZ2VuZXJhdGVkLmpzb24=");
        StringAssert.Contains(scriptText, "QUNUSVZFX1JVTl9IQU5ET0ZGLmdlbmVyYXRlZC5tZA==");
        StringAssert.Contains(scriptText, "5441534b5f4c4f43414c5f54454c454d455452592e67656e6572617465642e6a736f6e");
        StringAssert.Contains(scriptText, "4143544956455f52554e5f48414e444f46462e67656e6572617465642e6d64");
        StringAssert.Contains(scriptText, "TASK&#95;LOCAL&#95;TELEMETRY.generated.json");
        StringAssert.Contains(scriptText, "ACTIVE&#95;RUN&#95;HANDOFF.generated.md");
        StringAssert.Contains(scriptText, "operatorHelperProofChecks");
        StringAssert.Contains(scriptText, "required_proof_avoids_active_run_helpers");
        StringAssert.Contains(scriptText, "registry_evidence_avoids_active_run_helpers");
        StringAssert.Contains(scriptText, "queue_evidence_avoids_active_run_helpers");
        StringAssert.Contains(scriptText, "design_queue_evidence_avoids_active_run_helpers");
        StringAssert.Contains(scriptText, "operator helper proof check failed");
        StringAssert.Contains(scriptText, "proofUniquenessChecks");
        StringAssert.Contains(scriptText, "required_proof_lines_unique");
        StringAssert.Contains(scriptText, "registry_proof_lines_unique");
        StringAssert.Contains(scriptText, "queue_proof_lines_unique");
        StringAssert.Contains(scriptText, "proof uniqueness check failed");
        StringAssert.Contains(scriptText, "registry_review_reasons");
        StringAssert.Contains(scriptText, "queue_review_reasons");
        StringAssert.Contains(scriptText, "proof_hygiene_review_reasons");
        StringAssert.Contains(scriptText, "local_repo_review_reasons");
        StringAssert.Contains(scriptText, "source_marker_review_reasons");
        StringAssert.Contains(scriptText, "\"registryClosureReview\"");
        StringAssert.Contains(scriptText, "\"queueClosureReview\"");
        StringAssert.Contains(scriptText, "\"proofHygieneReview\"");
        StringAssert.Contains(scriptText, "\"localRepoCitationReview\"");
        StringAssert.Contains(scriptText, "\"sourceMarkerReview\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not registry_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not queue_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not proof_hygiene_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not local_repo_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not source_marker_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"failureCount\": len(reasons)");
        StringAssert.Contains(scriptText, "M104_explain_receipts_guard_is_wired_into_compliance_test_project");
        StringAssert.Contains(scriptText, "M104_explain_receipts_guard_is_wired_into_standard_ai_verify");
        StringAssert.Contains(scriptText, "m104_standard_verify_wiring");
        StringAssert.Contains(scriptText, "checking next-90 M104 desktop explain receipt guard");
        StringAssert.Contains(scriptText, "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh");
        StringAssert.Contains(scriptText, "Chummer.Tests/Presentation/BlazorShellComponentTests.cs");
        StringAssert.Contains(scriptText, "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs");
    }

    [TestMethod]
    public void M104_explain_receipts_receipt_proves_desktop_trust_surfaces_are_closed_in_repo_local_state()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString(), "M104 receipt must pass when repo-local surfaces and canonical closure proof stay intact.");
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength(), "M104 receipt must not surface unresolved drift when the live package repo matches canonical closure proof.");

        JsonElement evidence = root.GetProperty("evidence");
        JsonElement reviews = root.GetProperty("reviews");
        Assert.AreEqual("next90-m104-ui-explain-receipts", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(3352869062, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(104, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("63f57d62", evidence.GetProperty("landedCommit").GetString());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        CollectionAssert.AreEquivalent(ExpectedSurfaces, ReadStringArray(evidence.GetProperty("ownedSurfaces")));
        CollectionAssert.AreEquivalent(ExpectedAllowedPaths, ReadStringArray(evidence.GetProperty("allowedPaths")));
        Assert.AreEqual("pass", reviews.GetProperty("registryClosureReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("queueClosureReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("proofHygieneReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("localRepoCitationReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("sourceMarkerReview").GetProperty("status").GetString());

        JsonElement queueChecks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(queueChecks.GetProperty("status_complete").GetBoolean(), "Queue row must remain closed.");
        Assert.IsTrue(queueChecks.GetProperty("frontier_matches").GetBoolean(), "Queue row must stay bound to the assigned successor frontier.");
        Assert.IsTrue(queueChecks.GetProperty("landed_commit_matches").GetBoolean(), "Queue landed commit must stay bound.");
        Assert.IsTrue(queueChecks.GetProperty("owned_surface_explain_receipts:desktop").GetBoolean(), "Queue owned surface must keep explain receipts.");
        Assert.IsTrue(queueChecks.GetProperty("owned_surface_diagnostics_diff:desktop").GetBoolean(), "Queue owned surface must keep diagnostics diffs.");
        Assert.IsTrue(queueChecks.GetProperty("allowed_paths_exact").GetBoolean(), "Queue allowed paths must stay exact for the M104 UI slice.");
        Assert.IsTrue(queueChecks.GetProperty("owned_surfaces_exact").GetBoolean(), "Queue owned surfaces must stay exact for the M104 UI slice.");

        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            evidence.GetProperty("designQueuePath").GetString(),
            "M104 proof must bind Fleet staging back to the design-side queue staging file.");
        JsonElement designQueueChecks = evidence.GetProperty("designQueueChecks");
        Assert.IsTrue(designQueueChecks.GetProperty("status_complete").GetBoolean(), "Design queue row must remain closed.");
        Assert.IsTrue(designQueueChecks.GetProperty("frontier_matches").GetBoolean(), "Design queue row must stay bound to the assigned successor frontier.");
        Assert.IsTrue(designQueueChecks.GetProperty("allowed_paths_exact").GetBoolean(), "Design queue allowed paths must stay exact for the M104 UI slice.");
        Assert.IsTrue(designQueueChecks.GetProperty("owned_surfaces_exact").GetBoolean(), "Design queue owned surfaces must stay exact for the M104 UI slice.");
        JsonElement queueMirrorChecks = evidence.GetProperty("queueMirrorChecks");
        Assert.IsTrue(queueMirrorChecks.GetProperty("fleet_queue_points_to_design_queue").GetBoolean(), "Fleet queue staging must keep its design queue source path.");
        Assert.IsTrue(queueMirrorChecks.GetProperty("package_blocks_match").GetBoolean(), "Fleet and design queue rows must not drift for the completed M104 package.");
        Assert.IsTrue(queueMirrorChecks.GetProperty("fleet_queue_package_unique").GetBoolean(), "Fleet queue staging must not carry duplicate M104 package rows.");
        Assert.IsTrue(queueMirrorChecks.GetProperty("design_queue_package_unique").GetBoolean(), "Design queue staging must not carry duplicate M104 package rows.");

        JsonElement registryChecks = evidence.GetProperty("registryChecks");
        Assert.IsTrue(registryChecks.GetProperty("ui_work_task_complete").GetBoolean(), "Registry task 104.3 must remain complete.");
        Assert.IsTrue(registryChecks.GetProperty("ui_work_task_unique").GetBoolean(), "Registry must not carry duplicate M104 UI work-task rows.");
        Assert.IsTrue(registryChecks.GetProperty("ui_work_task_landed_commit").GetBoolean(), "Registry task 104.3 must stay commit-bound.");
        JsonElement localRepoChecks = evidence.GetProperty("localRepoChecks");
        Assert.IsFalse(localRepoChecks.GetProperty("landed_commit_resolves").GetBoolean(), "Current checkout still does not carry the historical landed commit from the old closeout lineage.");
        Assert.IsTrue(localRepoChecks.GetProperty("landed_commit_cited_canonically").GetBoolean(), "Current checkout must still prove that the historical landed commit remains canonically cited by registry and queue closure.");
        Assert.IsTrue(localRepoChecks.GetProperty("all_proof_commits_resolve").GetBoolean(), "Current receipt now resolves the active repo-local M104 proof lineage directly instead of carrying the historical unresolved proof-commit list.");
        Assert.IsTrue(localRepoChecks.GetProperty("all_proof_commits_have_canonical_citations").GetBoolean(), "Recorded M104 proof commit anchors must be cited by registry or queue proof.");
        JsonElement proofPathScopeChecks = evidence.GetProperty("proofPathScopeChecks");
        Assert.IsTrue(proofPathScopeChecks.GetProperty("all_scoped_paths_allowed").GetBoolean(), "M104 proof paths must stay inside assigned UI roots or named proof exceptions.");
        JsonElement scopedPaths = proofPathScopeChecks.GetProperty("scoped_paths");
        foreach (JsonProperty scopedPath in scopedPaths.EnumerateObject())
        {
            Assert.IsTrue(scopedPath.Value.GetBoolean(), $"M104 proof path is outside assigned scope: {scopedPath.Name}");
        }
        JsonElement canonicalProofPathScopeChecks = evidence.GetProperty("canonicalProofPathScopeChecks");
        Assert.IsTrue(
            canonicalProofPathScopeChecks.GetProperty("all_canonical_block_paths_allowed").GetBoolean(),
            "M104 canonical registry, Fleet queue, and design queue proof paths must stay inside assigned UI roots or named proof exceptions.");
        foreach (JsonProperty proofBlock in canonicalProofPathScopeChecks.GetProperty("blocks").EnumerateObject())
        {
            foreach (JsonProperty scopedPath in proofBlock.Value.EnumerateObject())
            {
                Assert.IsTrue(scopedPath.Value.GetBoolean(), $"M104 canonical proof path is outside assigned scope: {proofBlock.Name}:{scopedPath.Name}");
            }
        }

        JsonElement proofCommits = localRepoChecks.GetProperty("resolving_proof_commits");
        JsonElement proofCommitCitations = localRepoChecks.GetProperty("proof_commits_have_canonical_citations");
        Assert.AreEqual(0, proofCommits.EnumerateObject().Count(), "Current M104 receipt does not carry a historical unresolved proof-commit list anymore.");
        Assert.AreEqual(0, proofCommitCitations.EnumerateObject().Count(), "Current M104 receipt does not need per-commit citation rows when the proof-commit list is empty.");

        JsonElement operatorHelperProofChecks = evidence.GetProperty("operatorHelperProofChecks");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("required_proof_avoids_active_run_helpers").GetBoolean(), "M104 proof constants must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("registry_evidence_avoids_active_run_helpers").GetBoolean(), "M104 registry evidence must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("queue_evidence_avoids_active_run_helpers").GetBoolean(), "M104 Fleet queue evidence must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("design_queue_evidence_avoids_active_run_helpers").GetBoolean(), "M104 design queue evidence must not cite active-run helper artifacts.");
        string receiptText = File.ReadAllText(receiptPath);
        StringAssert.Contains(receiptText, "\"TASK_LOCAL_TELEMETRY.generated.json\"");
        StringAssert.Contains(receiptText, "\"ACTIVE_RUN_HANDOFF.generated.md\"");

        JsonElement proofUniquenessChecks = evidence.GetProperty("proofUniquenessChecks");
        Assert.IsTrue(proofUniquenessChecks.GetProperty("required_proof_lines_unique").GetBoolean(), "M104 required proof constants must not contain duplicate entries.");
        Assert.IsTrue(proofUniquenessChecks.GetProperty("registry_proof_lines_unique").GetBoolean(), "M104 registry proof constants must not contain duplicate entries.");
        Assert.IsTrue(proofUniquenessChecks.GetProperty("queue_proof_lines_unique").GetBoolean(), "M104 queue proof constants must not contain duplicate entries.");
        Assert.AreEqual(0, proofUniquenessChecks.GetProperty("required_proof_duplicate_lines").GetArrayLength());
        Assert.AreEqual(0, proofUniquenessChecks.GetProperty("registry_proof_duplicate_lines").GetArrayLength());
        Assert.AreEqual(0, proofUniquenessChecks.GetProperty("queue_proof_duplicate_lines").GetArrayLength());

        JsonElement sourceResults = evidence.GetProperty("sourceResults");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/DesktopTrustReceiptText.cs", "import_rule_environment_receipt");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/DesktopTrustReceiptText.cs", "diagnostics_environment_diff");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs", "avalonia_import_receipt_surface");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/DesktopSupportWindow.cs", "avalonia_support_diagnostics");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/DesktopSupportCaseWindow.cs", "avalonia_support_case_diagnostics");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Avalonia/Controls/SectionHostControl.axaml.cs", "avalonia_build_blocker_receipts");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs", "blazor_import_rule_environment_receipt");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Blazor/Components/Shell/DialogHost.razor", "blazor_dialog_surface");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Blazor/Components/Shell/SectionPane.razor", "blazor_build_blocker_receipts");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Tests/Compliance/Next90M104ExplainReceiptsGuardTests.cs", "m104_guard_self_closure");
        AssertSourceMarkerGroupPassed(sourceResults, "Chummer.Tests/Chummer.Tests.csproj", "m104_guard_project_wiring");
        AssertSourceMarkerGroupPassed(sourceResults, "scripts/ai/verify.sh", "m104_standard_verify_wiring");
    }

    [TestMethod]
    public void M104_explain_receipts_guard_is_wired_into_compliance_test_project()
    {
        string repoRoot = FindRepoRoot();
        string projectPath = Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj");
        string projectText = File.ReadAllText(projectPath);

        StringAssert.Contains(projectText, "Compliance\\Next90M104ExplainReceiptsGuardTests.cs");
    }

    [TestMethod]
    public void M104_explain_receipts_guard_is_wired_into_standard_ai_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(verifyText, "checking next-90 M104 desktop explain receipt guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh");
    }

    [TestMethod]
    public void M104_explain_receipts_canonical_queue_closure_stays_worker_safe_and_scope_exact()
    {
        string registryText = File.ReadAllText("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml");
        string fleetQueueText = File.ReadAllText("/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml");
        string designQueueText = File.ReadAllText("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml");

        string registryTask = ExtractBlock(registryText, "id: 104.3\n        owner: chummer6-ui", "\n      - id: 104.4");
        string fleetQueueBlock = ExtractQueueBlock(fleetQueueText);
        string designQueueBlock = ExtractQueueBlock(designQueueText);

        foreach (string block in new[] { fleetQueueBlock, designQueueBlock })
        {
            AssertExactScope(block);
            StringAssert.Contains(block, "status: complete");
            StringAssert.Contains(block, "frontier_id: 3352869062");
            StringAssert.Contains(block, "landed_commit: 63f57d62");
            AssertNoActiveRunHelperProof(block);
        }

        StringAssert.Contains(registryTask, "status: complete");
        StringAssert.Contains(registryTask, "landed_commit: 63f57d62");
        StringAssert.Contains(registryTask, "successor frontier 3352869062");
        AssertNoActiveRunHelperProof(registryTask);

        Assert.AreEqual(
            fleetQueueBlock.Trim(),
            designQueueBlock.Trim(),
            "Fleet and design queue closure proof for M104 UI explain receipts must stay byte-equivalent at the package block level.");
    }

    [TestMethod]
    public void M104_explain_receipts_generated_proof_arrays_stay_worker_safe()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement evidence = receipt.RootElement.GetProperty("evidence");

        AssertJsonArrayNoActiveRunHelperProof(evidence.GetProperty("requiredProof"), "requiredProof");
        AssertJsonArrayNoActiveRunHelperProof(evidence.GetProperty("registryProof"), "registryProof");
        AssertJsonArrayNoActiveRunHelperProof(evidence.GetProperty("queueProof"), "queueProof");

        JsonElement operatorHelperProofChecks = evidence.GetProperty("operatorHelperProofChecks");
        foreach (JsonProperty check in operatorHelperProofChecks.EnumerateObject())
        {
            Assert.IsTrue(check.Value.GetBoolean(), $"Generated M104 proof must keep worker-safe evidence check green: {check.Name}");
        }
    }

    private static string[] ReadStringArray(JsonElement array)
        => array.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToArray();

    private static void AssertSourceMarkerGroupPassed(JsonElement sourceResults, string sourcePath, string groupName)
    {
        JsonElement result = sourceResults.GetProperty(sourcePath).GetProperty(groupName);
        Assert.AreEqual("pass", result.GetProperty("status").GetString(), $"{sourcePath} {groupName} must stay pass.");
        Assert.AreEqual(0, result.GetProperty("missingMarkers").GetArrayLength(), $"{sourcePath} {groupName} must not lose source markers.");
    }

    private static void AssertExactScope(string block)
    {
        CollectionAssert.AreEqual(ExpectedAllowedPaths, ReadYamlList(block, "allowed_paths"));
        CollectionAssert.AreEqual(ExpectedSurfaces, ReadYamlList(block, "owned_surfaces"));
    }

    private static void AssertNoActiveRunHelperProof(string block)
    {
        foreach (string token in DisallowedActiveRunProofTokens)
        {
            Assert.IsFalse(
                block.Contains(token, System.StringComparison.OrdinalIgnoreCase),
                $"M104 completed-package proof must not cite active-run helper evidence: {token}");
        }
    }

    private static void AssertJsonArrayNoActiveRunHelperProof(JsonElement array, string label)
    {
        foreach (JsonElement element in array.EnumerateArray())
        {
            string value = element.GetString() ?? string.Empty;
            foreach (string token in DisallowedActiveRunProofTokens)
            {
                Assert.IsFalse(
                    value.Contains(token, System.StringComparison.OrdinalIgnoreCase),
                    $"M104 generated {label} must not cite active-run helper evidence: {token}");
            }
        }
    }

    private static string ExtractQueueBlock(string text)
    {
        return ExtractBlock(text, "package_id: next90-m104-ui-explain-receipts", "\n  - title:");
    }

    private static string ExtractBlock(string text, string marker, string nextMarker)
    {
        int markerIndex = text.IndexOf(marker, System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, markerIndex, $"Expected marker was not found: {marker}");
        int start = text.LastIndexOf("\n  - ", markerIndex, System.StringComparison.Ordinal);
        if (start < 0)
        {
            start = text.LastIndexOf("\n      - ", markerIndex, System.StringComparison.Ordinal);
        }

        if (start < 0)
        {
            start = markerIndex;
        }

        int end = text.IndexOf(nextMarker, markerIndex + marker.Length, System.StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string[] ReadYamlList(string block, string key)
    {
        string marker = key + ":";
        int markerIndex = block.IndexOf(marker, System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, markerIndex, $"Expected YAML list was not found: {key}");

        List<string> items = new();
        foreach (string line in block[(markerIndex + marker.Length)..].Split('\n'))
        {
            if (line.StartsWith("      - ", System.StringComparison.Ordinal))
            {
                items.Add(line["      - ".Length..].Trim());
                continue;
            }

            if (line.StartsWith("  - ", System.StringComparison.Ordinal))
            {
                items.Add(line["  - ".Length..].Trim());
                continue;
            }

            if (items.Count > 0)
            {
                break;
            }
        }

        return items.ToArray();
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
