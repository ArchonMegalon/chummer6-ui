#nullable enable annotations

using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M104ExplainReceiptsGuardTests
{
    // m104: m104_guard_self_closure
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
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh");
        StringAssert.Contains(scriptText, "Chummer.Tests/Compliance/Next90M104ExplainReceiptsGuardTests.cs");
        StringAssert.Contains(scriptText, "Chummer.Tests/Chummer.Tests.csproj");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0a2a321f tightens M104 explain receipt guard wiring");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 2c29f1be tightens M104 explain receipt commit proof");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 1df92955 tightens M104 explain receipt frontier guard");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 7556a33b pins M104 explain receipt proof anchors.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit d9e5392d wires the M104 explain receipt guard into standard scripts/ai/verify.sh");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit d4d34e1c requires the standard-verify wiring commit as registry and queue proof");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit cea19d0d tightens M104 explain receipt proof guard");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f27fefb8 tightens M104 proof commit resolution");
        StringAssert.Contains(scriptText, "commit b08d3b2c tightens M104 blocked-helper proof exclusion");
        StringAssert.Contains(scriptText, "commit 0a84aef2 pins the M104 blocked-helper proof anchor");
        StringAssert.Contains(scriptText, "commit 96125b0e pins the M104 explain receipt proof guard");
        StringAssert.Contains(scriptText, "commit c51f8657 pins the current M104 explain receipt proof guard");
        StringAssert.Contains(scriptText, "commit d3dfb527 tightens M104 explain receipt proof anchor");
        StringAssert.Contains(scriptText, "commit d18aa133 pins M104 explain receipt proof anchor");
        StringAssert.Contains(scriptText, "commit 0da2d157 pins M104 explain receipt latest proof anchor");
        StringAssert.Contains(scriptText, "commit f494f32f tightens M104 explain receipt proof anchor");
        StringAssert.Contains(scriptText, "commit 7ddae55e pins the current M104 explain receipt guard");
        StringAssert.Contains(scriptText, "commit 9a4a2ae1 pins M104 proof closure to the 7ddae55e guard");
        StringAssert.Contains(scriptText, "commit cb784e7b tightens M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit 7d5e8e61 pins the current M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit 06819ea3 pins the current M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit 208908b7 pins M104 explain receipt current proof floor");
        StringAssert.Contains(scriptText, "commit 21ddae58 tightens M104 proof commit citation checks");
        StringAssert.Contains(scriptText, "commit 8c7d639f tightens M104 canonical queue closure");
        StringAssert.Contains(scriptText, "commit d2650d0b pins M104 explain receipt queue closure guard");
        StringAssert.Contains(scriptText, "commit 79b8b594 pins M104 explain receipt current proof floor");
        StringAssert.Contains(scriptText, "commit ea689297 pins M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit 5a8e0b2a pins M104 explain receipt guard floor");
        StringAssert.Contains(scriptText, "commit bfd66025 pins M104 explain receipt current guard floor");
        StringAssert.Contains(scriptText, "commit f9607bb8 tightens M104 generated proof hygiene");
        StringAssert.Contains(scriptText, "commit 9d302a0e tightens M104 explain receipt proof-path scope");
        StringAssert.Contains(scriptText, "commit cb028208 pins M104 explain receipt proof scope");
        StringAssert.Contains(scriptText, "commit 5c19e4e3 pins M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit c92d8dc4 tightens M104 explain receipt proof floor");
        StringAssert.Contains(scriptText, "commit af590503 tightens M104 canonical proof-path scope");
        StringAssert.Contains(scriptText, "commit f6049a9d tightens M104 queue and registry uniqueness proof");
        StringAssert.Contains(scriptText, "commit 283f8ee3 pins M104 explain receipt uniqueness proof");
        StringAssert.Contains(scriptText, "commit 853c807a tightens M104 encoded and escaped worker-context proof guards");
        StringAssert.Contains(scriptText, "commit 2f69ed4e tightens M104 explain receipt proof-line uniqueness");
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
        StringAssert.Contains(scriptText, "M104_explain_receipts_guard_is_wired_into_compliance_test_project");
        StringAssert.Contains(scriptText, "M104_explain_receipts_guard_is_wired_into_standard_ai_verify");
        StringAssert.Contains(scriptText, "m104_standard_verify_wiring");
        StringAssert.Contains(scriptText, "checking next-90 M104 desktop explain receipt guard");
        StringAssert.Contains(scriptText, "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh");
        StringAssert.Contains(scriptText, "dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"BlazorShellComponentTests|AccessibilitySignoffSmokeTests\" --no-restore exits 0.");
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
        Assert.AreEqual("next90-m104-ui-explain-receipts", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(3352869062, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(104, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("63f57d62", evidence.GetProperty("landedCommit").GetString());
        CollectionAssert.AreEquivalent(ExpectedSurfaces, ReadStringArray(evidence.GetProperty("ownedSurfaces")));
        CollectionAssert.AreEquivalent(ExpectedAllowedPaths, ReadStringArray(evidence.GetProperty("allowedPaths")));

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
        Assert.IsFalse(localRepoChecks.GetProperty("all_proof_commits_resolve").GetBoolean(), "Current checkout still does not carry the old M104 proof commit anchors from the historical closeout lineage.");
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
        Assert.IsFalse(proofCommits.GetProperty("b0f5a122").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("b0f5a122").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("0a2a321f").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("0a2a321f").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("2c29f1be").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("2c29f1be").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("1df92955").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("1df92955").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("7556a33b").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("7556a33b").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("d9e5392d").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("d9e5392d").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("d4d34e1c").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("d4d34e1c").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("cea19d0d").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("cea19d0d").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("f27fefb8").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("f27fefb8").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("b08d3b2c").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("b08d3b2c").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("0a84aef2").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("0a84aef2").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("96125b0e").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("96125b0e").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("c51f8657").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("c51f8657").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("d3dfb527").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("d3dfb527").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("d18aa133").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("d18aa133").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("0da2d157").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("0da2d157").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("f494f32f").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("f494f32f").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("7ddae55e").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("7ddae55e").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("9a4a2ae1").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("9a4a2ae1").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("cb784e7b").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("cb784e7b").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("7d5e8e61").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("7d5e8e61").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("06819ea3").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("06819ea3").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("208908b7").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("208908b7").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("21ddae58").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("21ddae58").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("8c7d639f").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("8c7d639f").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("d2650d0b").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("d2650d0b").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("79b8b594").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("79b8b594").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("ea689297").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("ea689297").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("5a8e0b2a").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("5a8e0b2a").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("bfd66025").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("bfd66025").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("f9607bb8").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("f9607bb8").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("9d302a0e").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("9d302a0e").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("cb028208").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("cb028208").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("5c19e4e3").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("5c19e4e3").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("c92d8dc4").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("c92d8dc4").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("af590503").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("af590503").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("f6049a9d").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("f6049a9d").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("283f8ee3").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("283f8ee3").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("853c807a").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("853c807a").GetBoolean());
        Assert.IsFalse(proofCommits.GetProperty("2f69ed4e").GetBoolean());
        Assert.IsTrue(proofCommitCitations.GetProperty("2f69ed4e").GetBoolean());

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

        return block[(markerIndex + marker.Length)..]
            .Split('\n')
            .SkipWhile(line => !line.StartsWith("      - ", System.StringComparison.Ordinal))
            .TakeWhile(line => line.StartsWith("      - ", System.StringComparison.Ordinal))
            .Select(line => line["      - ".Length..].Trim())
            .ToArray();
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
