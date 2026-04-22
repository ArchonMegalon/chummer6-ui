#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M103VeteranCertificationGuardTests
{
    private static readonly string[] ExpectedSurfaces =
    [
        "veteran_migration_certification",
        "screenshot_parity:desktop",
    ];

    private static readonly string[] ExpectedAllowedPaths =
    [
        "Chummer.Avalonia",
        "Chummer.Blazor",
        "Chummer.Tests",
        "scripts",
    ];

    private static readonly string[] ExpectedProofClosureCommits =
    [
        "fb6eb62e",
        "3bba8754",
        "6eafef39",
        "1e5557f9",
        "9e8d494b",
        "e796c016",
        "cb84c37b",
        "809a91d0",
        "0d2e357e",
        "b42416b8",
        "5d9f1c86",
        "df06b668",
        "8ea486f6",
        "11a0882e",
        "2bfc7338",
        "243062ac",
        "b8de3f95",
        "258fed08",
        "6c3c93e9",
        "22758a4c",
        "fd93bb8a",
        "e9f92d0d",
        "a4d93e27",
        "15e4d474",
        "075b292b",
        "d9bfcff5",
        "783ac00b",
        "827d3546",
        "89ab0ec0",
        "bb825994",
        "594aa3cd",
        "eb11fde4",
        "fa07f2bb",
        "680deb43",
        "10d65c5f",
        "ba914e9a",
        "de5837b9",
        "762aaedb",
        "8ce865c4",
        "55f2efca",
        "f649825d",
        "585ccc78",
        "9a6ebd38",
        "b0f424aa",
        "b8c0b19d",
        "136ff501",
        "b40a6556",
        "653adf49",
        "a5dff485",
    ];

    private static readonly string[] ExpectedProofItems =
    [
        "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json",
        "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION_REVIEW.generated.md",
        "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh",
        "/docker/chummercomplete/chummer6-ui-finish commit fb6eb62e",
        "/docker/chummercomplete/chummer6-ui-finish commit 3bba8754 tightens the M103 verifier receipt and standard-verify proof alignment.",
        "/docker/chummercomplete/chummer6-ui-finish commit 6eafef39 tightens the M103 verifier against design-owned queue source drift.",
        "/docker/chummercomplete/chummer6-ui-finish commit 1e5557f9 pins the M103 design queue source proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit 9e8d494b tightens the M103 successor registry guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit e796c016 pins M103 successor proof commits.",
        "/docker/chummercomplete/chummer6-ui-finish commit cb84c37b tightens the M103 veteran successor proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 809a91d0 pins the M103 veteran successor guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 0d2e357e pins the M103 veteran proof successor commit.",
        "/docker/chummercomplete/chummer6-ui-finish commit b42416b8 pins the M103 veteran proof hardening commit.",
        "/docker/chummercomplete/chummer6-ui-finish commit 5d9f1c86 pins M103 queue frontier proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit df06b668 pins M103 frontier proof anchor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 8ea486f6 binds M103 queue proof anchors.",
        "/docker/chummercomplete/chummer6-ui-finish commit 11a0882e pins M103 veteran proof anchor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 2bfc7338 pins M103 veteran queue proof anchors.",
        "/docker/chummercomplete/chummer6-ui-finish commit 243062ac pins M103 veteran queue proof commit.",
        "/docker/chummercomplete/chummer6-ui-finish commit b8de3f95 tightens the M103 active-run proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 258fed08 tightens M103 successor queue header proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit 6c3c93e9 pins M103 queue-header proof commit.",
        "/docker/chummercomplete/chummer6-ui-finish commit 22758a4c pins the latest M103 queue proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit fd93bb8a tightens M103 queue mirror proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit e9f92d0d pins M103 queue mirror proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit a4d93e27 pins M103 current queue mirror proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit 15e4d474 pins M103 current queue mirror proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit 075b292b tightens the M103 registry proof item guard so canonical successor evidence must match completed queue proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit d9bfcff5 pins M103 current veteran proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 783ac00b pins M103 veteran proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 827d3546 pins M103 veteran certification proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 89ab0ec0 pins M103 veteran certification proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit bb825994 pins M103 current veteran proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 594aa3cd pins M103 current veteran proof floor guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit eb11fde4 binds the M103 veteran proof guard to the current queue anchor.",
        "/docker/chummercomplete/chummer6-ui-finish commit fa07f2bb pins M103 queue anchor proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 680deb43 pins M103 veteran certification proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 10d65c5f pins M103 veteran certification proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit ba914e9a pins the current M103 veteran proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit de5837b9 tightens the M103 veteran proof token guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 762aaedb pins the M103 veteran proof token guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 8ce865c4 tightens M103 no-reopen proof posture.",
        "/docker/chummercomplete/chummer6-ui-finish commit 55f2efca tightens M103 active-run state-root proof exclusion.",
        "/docker/chummercomplete/chummer6-ui-finish commit f649825d pins M103 active-run proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit 585ccc78 pins M103 active-run proof floor guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit 9a6ebd38 pins M103 veteran certification proof floor.",
        "/docker/chummercomplete/chummer6-ui-finish commit b0f424aa tightens M103 scoped proof guard.",
        "/docker/chummercomplete/chummer6-ui-finish commit b8c0b19d tightens the M103 completed queue action guard so Fleet and design queue rows must carry verify_closed_package_only plus a package-specific do-not-reopen reason.",
        "/docker/chummercomplete/chummer6-ui-finish commit 136ff501 tightens the M103 veteran review pack proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit b40a6556 tightens the M103 desktop veteran parity proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit 653adf49 tightens the M103 veteran review packet proof.",
        "/docker/chummercomplete/chummer6-ui-finish commit a5dff485 tightens the M103 review packet proof binding.",
        "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M103VeteranCertificationGuardTests.cs",
        "bash scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh",
    ];

    [TestMethod]
    public void M103_veteran_certification_guard_pins_completed_queue_proof()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m103-ui-veteran-certification-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m103-ui-veteran-certification\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 2257965187");
        StringAssert.Contains(scriptText, "\"frontier_id\": FRONTIER_ID");
        StringAssert.Contains(scriptText, "EXPECTED_LANDED_COMMIT = \"a8e4f92c\"");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_COMMITS");
        StringAssert.Contains(scriptText, "\"fb6eb62e\": \"Tighten next90 M103 veteran proof guard\"");
        StringAssert.Contains(scriptText, "\"3bba8754\": \"Tighten M103 veteran verify proof\"");
        StringAssert.Contains(scriptText, "\"6eafef39\": \"Tighten M103 veteran queue source proof\"");
        StringAssert.Contains(scriptText, "\"1e5557f9\": \"Pin M103 design queue source proof\"");
        StringAssert.Contains(scriptText, "\"9e8d494b\": \"Tighten M103 successor registry guard\"");
        StringAssert.Contains(scriptText, "\"e796c016\": \"Pin M103 successor proof commits\"");
        StringAssert.Contains(scriptText, "\"cb84c37b\": \"Tighten M103 veteran successor proof guard\"");
        StringAssert.Contains(scriptText, "\"809a91d0\": \"Pin M103 veteran successor guard\"");
        StringAssert.Contains(scriptText, "\"0d2e357e\": \"Pin M103 veteran proof successor commit\"");
        StringAssert.Contains(scriptText, "\"b42416b8\": \"Pin M103 veteran proof hardening commit\"");
        StringAssert.Contains(scriptText, "\"5d9f1c86\": \"Pin M103 queue frontier proof\"");
        StringAssert.Contains(scriptText, "\"df06b668\": \"Pin M103 frontier proof anchor\"");
        StringAssert.Contains(scriptText, "\"8ea486f6\": \"Bind M103 queue proof anchors\"");
        StringAssert.Contains(scriptText, "\"11a0882e\": \"Pin M103 veteran proof anchor\"");
        StringAssert.Contains(scriptText, "\"2bfc7338\": \"Pin M103 veteran queue proof anchors\"");
        StringAssert.Contains(scriptText, "\"243062ac\": \"Pin M103 veteran queue proof commit\"");
        StringAssert.Contains(scriptText, "\"b8de3f95\": \"Tighten M103 active-run proof guard\"");
        StringAssert.Contains(scriptText, "\"258fed08\": \"Tighten M103 successor queue header proof\"");
        StringAssert.Contains(scriptText, "\"6c3c93e9\": \"Pin M103 queue header proof commit\"");
        StringAssert.Contains(scriptText, "\"22758a4c\": \"Pin latest M103 queue proof guard\"");
        StringAssert.Contains(scriptText, "\"fd93bb8a\": \"Tighten M103 queue mirror proof guard\"");
        StringAssert.Contains(scriptText, "\"e9f92d0d\": \"Pin M103 queue mirror proof guard\"");
        StringAssert.Contains(scriptText, "\"a4d93e27\": \"Pin M103 current queue mirror proof\"");
        StringAssert.Contains(scriptText, "\"15e4d474\": \"Pin M103 current queue mirror proof\"");
        StringAssert.Contains(scriptText, "\"075b292b\": \"Tighten M103 registry proof guard\"");
        StringAssert.Contains(scriptText, "\"d9bfcff5\": \"Pin M103 current veteran proof floor\"");
        StringAssert.Contains(scriptText, "\"783ac00b\": \"Pin M103 veteran proof floor\"");
        StringAssert.Contains(scriptText, "\"827d3546\": \"Pin M103 veteran certification proof floor\"");
        StringAssert.Contains(scriptText, "\"89ab0ec0\": \"Pin M103 veteran certification proof floor\"");
        StringAssert.Contains(scriptText, "\"bb825994\": \"Pin M103 current veteran proof floor\"");
        StringAssert.Contains(scriptText, "\"594aa3cd\": \"Pin M103 current veteran proof floor guard\"");
        StringAssert.Contains(scriptText, "\"eb11fde4\": \"Bind M103 veteran proof guard to queue anchor\"");
        StringAssert.Contains(scriptText, "\"fa07f2bb\": \"Pin M103 queue anchor proof floor\"");
        StringAssert.Contains(scriptText, "\"680deb43\": \"Pin M103 veteran certification proof floor\"");
        StringAssert.Contains(scriptText, "\"10d65c5f\": \"Pin M103 veteran certification proof floor\"");
        StringAssert.Contains(scriptText, "\"ba914e9a\": \"Pin current M103 veteran proof floor\"");
        StringAssert.Contains(scriptText, "\"de5837b9\": \"Tighten M103 veteran proof token guard\"");
        StringAssert.Contains(scriptText, "\"762aaedb\": \"Pin M103 veteran proof token guard\"");
        StringAssert.Contains(scriptText, "\"8ce865c4\": \"Tighten M103 veteran no-reopen proof\"");
        StringAssert.Contains(scriptText, "\"55f2efca\": \"Tighten M103 active-run proof guard\"");
        StringAssert.Contains(scriptText, "\"f649825d\": \"Pin M103 active-run proof floor\"");
        StringAssert.Contains(scriptText, "\"585ccc78\": \"Pin M103 active-run proof floor guard\"");
        StringAssert.Contains(scriptText, "\"9a6ebd38\": \"Pin M103 veteran certification proof floor\"");
        StringAssert.Contains(scriptText, "\"b0f424aa\": \"Tighten M103 scoped proof guard\"");
        StringAssert.Contains(scriptText, "\"b8c0b19d\": \"Tighten M103 closed queue action guard\"");
        StringAssert.Contains(scriptText, "\"136ff501\": \"test(next90-m103): tighten veteran review pack proof\"");
        StringAssert.Contains(scriptText, "\"b40a6556\": \"Tighten M103 desktop veteran parity proof\"");
        StringAssert.Contains(scriptText, "\"653adf49\": \"Tighten M103 veteran review packet proof\"");
        StringAssert.Contains(scriptText, "\"a5dff485\": \"Tighten M103 review packet proof binding\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_COMMIT_ITEMS");
        StringAssert.Contains(scriptText, "EXPECTED_ALLOWED_PATHS");
        foreach (string allowedPath in ExpectedAllowedPaths)
        {
            StringAssert.Contains(scriptText, $"\"{allowedPath}\"");
        }

        StringAssert.Contains(scriptText, "EXPECTED_SURFACES");
        foreach (string surface in ExpectedSurfaces)
        {
            StringAssert.Contains(scriptText, $"\"{surface}\"");
        }

        StringAssert.Contains(scriptText, "EXPECTED_NO_REOPEN_POSTURE");
        StringAssert.Contains(scriptText, "\"packageAlreadyComplete\": True");
        StringAssert.Contains(scriptText, "\"futureShardAction\": \"verify_completed_package_proof_floor\"");
        StringAssert.Contains(scriptText, "canonical_successor_registry_reopens_task_103_2");
        StringAssert.Contains(scriptText, "fleet_or_design_queue_row_drops_complete_status");
        StringAssert.Contains(scriptText, "promoted_desktop_head_binding_or_screenshot_evidence_regresses");
        StringAssert.Contains(scriptText, "\"noReopenPosture\": EXPECTED_NO_REOPEN_POSTURE");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_RECEIPT");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_SCRIPT");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_GUARD");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_COMMAND");
        StringAssert.Contains(scriptText, "EXPECTED_DESIGN_QUEUE_PATH");
        StringAssert.Contains(scriptText, "EXPECTED_REGISTRY_PATH");
        StringAssert.Contains(scriptText, "EXPECTED_QUEUE_HEADER");
        StringAssert.Contains(scriptText, "validate_queue_header");
        StringAssert.Contains(scriptText, "EXPECTED_VERIFY_BANNER");
        StringAssert.Contains(scriptText, "DISALLOWED_ACTIVE_RUN_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "operator/OODA");
        StringAssert.Contains(scriptText, "run-helper");
        StringAssert.Contains(scriptText, "base64.b64decode");
        StringAssert.Contains(scriptText, "gzip.decompress");
        StringAssert.Contains(scriptText, "zlib.decompress");
        StringAssert.Contains(scriptText, "block_contains_hex_encoded_ci");
        StringAssert.Contains(scriptText, "bytes.fromhex");
        StringAssert.Contains(scriptText, "block_contains_escaped_ci");
        StringAssert.Contains(scriptText, "urllib.parse.unquote");
        StringAssert.Contains(scriptText, "html.unescape");
        StringAssert.Contains(scriptText, "find_encoded_active_run_tokens");
        StringAssert.Contains(scriptText, "encoded_operator_helper_token_hits");
        StringAssert.Contains(scriptText, "\"required_proof_avoids_encoded_active_run_helpers\"");
        StringAssert.Contains(scriptText, "\"registry_evidence_avoids_encoded_active_run_helpers\"");
        StringAssert.Contains(scriptText, "\"queue_evidence_avoids_encoded_active_run_helpers\"");
        StringAssert.Contains(scriptText, "\"design_queue_evidence_avoids_encoded_active_run_helpers\"");
        StringAssert.Contains(scriptText, "standard_verify_encoded_blocked_active_run_helper_hits");
        StringAssert.Contains(scriptText, "\"verify_entrypoint_avoids_encoded_active_run_helpers\"");
        StringAssert.Contains(scriptText, "M103 standard verify check failed");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_REPO_PREFIX");
        StringAssert.Contains(scriptText, "MIN_SCREENSHOT_WIDTH = 1280");
        StringAssert.Contains(scriptText, "MIN_SCREENSHOT_HEIGHT = 800");
        StringAssert.Contains(scriptText, "MIN_SCREENSHOT_DISTINCT_SAMPLE_COLORS = 3");
        StringAssert.Contains(scriptText, "inspect_png_content");
        StringAssert.Contains(scriptText, "publishedScreenshotDistinctSampleColors");
        StringAssert.Contains(scriptText, "publishedScreenshotContentNonBlank");
        StringAssert.Contains(scriptText, "find_unscoped_proof_path_refs");
        StringAssert.Contains(scriptText, "proofScopeChecks");
        StringAssert.Contains(scriptText, "proofScopePathHits");
        StringAssert.Contains(scriptText, "tupleIdMatchesPromotedHeadAndPlatform");
        StringAssert.Contains(scriptText, "artifactIdPresent");
        StringAssert.Contains(scriptText, "publicInstallRouteIsDownloadRoute");
        StringAssert.Contains(scriptText, "expected a /downloads/install/ route");
        StringAssert.Contains(scriptText, "source_file_markers");
        StringAssert.Contains(scriptText, "sourceFileChecks");
        StringAssert.Contains(scriptText, "VeteranCertificationReviewSteps");
        StringAssert.Contains(scriptText, "Click FileMenuButton and capture MenuCommandsHost");
        StringAssert.Contains(scriptText, "Capture initial promoted Avalonia shell after WaitForReady.");
        StringAssert.Contains(scriptText, "Press Ctrl+G and capture the Global Settings dialog.");
        StringAssert.Contains(scriptText, "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.");
        StringAssert.Contains(scriptText, "Execute master_index and capture the Master Index dialog.");
        StringAssert.Contains(scriptText, "Execute character_roster and capture the Character Roster dialog.");
        StringAssert.Contains(scriptText, "Chummer5a ChummerMainForm File/Tools/Windows/Help top menu lineage.");
        StringAssert.Contains(scriptText, "Chummer5a ChummerMainForm toolStrip New/Open/OpenForPrinting/OpenForExport lineage.");
        StringAssert.Contains(scriptText, "Chummer5a EditGlobalSettings Global Options lineage.");
        StringAssert.Contains(scriptText, "Chummer5a File/Open and Hero Lab Importer import route lineage.");
        StringAssert.Contains(scriptText, "Chummer5a MasterIndex search utility lineage.");
        StringAssert.Contains(scriptText, "Chummer5a CharacterRoster watch-folder utility lineage.");
        StringAssert.Contains(scriptText, "capture_markers");
        StringAssert.Contains(scriptText, "captureMarkerChecks");
        StringAssert.Contains(scriptText, "screenshot capture proof is missing interaction markers");
        StringAssert.Contains(scriptText, "AssertDialogContainsAll");
        StringAssert.Contains(scriptText, "\"Open Character\"");
        StringAssert.Contains(scriptText, "18-import-dialog-light.png");
        StringAssert.Contains(scriptText, "harness.Click(\"FileMenuButton\")");
        StringAssert.Contains(scriptText, "harness.ClickMenuCommand(\"open_character\")");
        StringAssert.Contains(scriptText, "LEGACY_IMPORT_ROUTE_BASELINE_EVIDENCE");
        StringAssert.Contains(scriptText, "legacyImportRouteBaselineResults");
        StringAssert.Contains(scriptText, "import Chummer5a route baseline is missing file-open/import lineage markers");
        StringAssert.Contains(scriptText, "this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenFile);");
        StringAssert.Contains(scriptText, "this.mnuHeroLabImporter.Text = \"&Hero Lab Importer\";");
        StringAssert.Contains(scriptText, "event_handlers_path");
        StringAssert.Contains(scriptText, "eventHandlersSourceFile");
        StringAssert.Contains(scriptText, "designQueuePath");
        StringAssert.Contains(scriptText, "designQueueItem");
        StringAssert.Contains(scriptText, "designQueueTopLevel");
        StringAssert.Contains(scriptText, "source_design_queue_path");
        StringAssert.Contains(scriptText, "queueHeaderChecks");
        StringAssert.Contains(scriptText, "designQueueHeaderChecks");
        StringAssert.Contains(scriptText, "proof_item_checks");
        StringAssert.Contains(scriptText, "registryProofItemChecks");
        StringAssert.Contains(scriptText, "proof is missing required M103 closure evidence");
        StringAssert.Contains(scriptText, "queue_alignment_checks");
        StringAssert.Contains(scriptText, "queueMirrorAlignmentChecks");
        StringAssert.Contains(scriptText, "standardVerifyPath");
        StringAssert.Contains(scriptText, "operatorHelperProofChecks");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(scriptText, "/var/lib/codex-fleet");
        StringAssert.Contains(scriptText, "veteranCertificationMatrix");
        StringAssert.Contains(scriptText, "certificationMatrixRow");
        StringAssert.Contains(scriptText, "chummer5aBaselineFile");
        StringAssert.Contains(scriptText, "screenshotContentNonBlank");
        StringAssert.Contains(scriptText, "sourceFileProofCount");
        StringAssert.Contains(scriptText, "captureMarkerCount");
        StringAssert.Contains(scriptText, "semantic_payload.pop(\"generatedAt\", None)");
        StringAssert.Contains(scriptText, "previous_semantic_payload.pop(\"generatedAt\", None)");
        StringAssert.Contains(scriptText, "payload[\"generatedAt\"] = previous_payload[\"generatedAt\"]");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M103VeteranCertificationGuardTests.cs");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml");
        StringAssert.Contains(scriptText, "Queue item proof is missing required M103 closure evidence");
        StringAssert.Contains(scriptText, "Design queue item proof is missing required M103 closure evidence");
        StringAssert.Contains(scriptText, "Fleet successor queue row for M103 no longer matches the design-owned queue row");
        StringAssert.Contains(scriptText, "operator helper proof check failed");
        StringAssert.Contains(scriptText, "M103 scoped proof check failed");
        StringAssert.Contains(scriptText, "reviewMarkdownChecks");
        StringAssert.Contains(scriptText, "review markdown check failed");
        StringAssert.Contains(scriptText, "\"receipt_path_bound\"");
        StringAssert.Contains(scriptText, "\"screenshot_pack_bound\"");
        StringAssert.Contains(scriptText, "\"source_repo_bound\"");
        StringAssert.Contains(scriptText, "\"authority_repo_bound\"");
        StringAssert.Contains(scriptText, "\"surface_row_count_matches\"");
        StringAssert.Contains(scriptText, "\"surface_rows_present\"");
        StringAssert.Contains(scriptText, "\"screenshot_paths_present\"");
        StringAssert.Contains(scriptText, "M103 veteran certification guard is not wired into the standard AI verify path");
        StringAssert.Contains(scriptText, "M103 veteran certification compliance guard is missing required markers");
    }

    [TestMethod]
    public void M103_veteran_certification_receipt_keeps_surface_distinct_screenshot_proof()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString(), "M103 receipt must stay pass once the completed package is queued.");
        Assert.AreEqual("next90-m103-ui-veteran-certification", root.GetProperty("packageId").GetString());
        Assert.AreEqual(2257965187, root.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(103, root.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("avalonia", root.GetProperty("promotedPrimaryHead").GetString());
        Assert.AreEqual(0, root.GetProperty("reasons").GetArrayLength(), "Completed M103 proof must not carry unresolved reasons.");
        CollectionAssert.AreEquivalent(ExpectedAllowedPaths, ReadStringArray(root.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(ExpectedSurfaces, ReadStringArray(root.GetProperty("ownedSurfaces")));

        JsonElement evidence = root.GetProperty("evidence");
        JsonElement noReopenPosture = evidence.GetProperty("noReopenPosture");
        Assert.IsTrue(noReopenPosture.GetProperty("packageAlreadyComplete").GetBoolean(), "Completed M103 proof must tell future shards not to repeat the slice.");
        Assert.AreEqual(
            "verify_completed_package_proof_floor",
            noReopenPosture.GetProperty("futureShardAction").GetString(),
            "Future M103 shards should verify proof floor instead of re-running the parity package.");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "canonical_successor_registry_reopens_task_103_2",
                "fleet_or_design_queue_row_drops_complete_status",
                "promoted_desktop_head_binding_or_screenshot_evidence_regresses",
            },
            ReadStringArray(noReopenPosture.GetProperty("reopenOnlyIf")));

        JsonElement proofClosureCommits = evidence.GetProperty("gitProofClosureCommits");
        Assert.AreEqual(ExpectedProofClosureCommits.Length, proofClosureCommits.GetArrayLength(), "M103 proof must pin every follow-up proof-closure commit.");
        CollectionAssert.AreEquivalent(
            ExpectedProofClosureCommits,
            proofClosureCommits.EnumerateArray()
                .Select(commit => commit.GetProperty("commit").GetString() ?? string.Empty)
                .ToArray());
        foreach (JsonElement proofCommit in proofClosureCommits.EnumerateArray())
        {
            Assert.IsTrue(proofCommit.GetProperty("isAncestorOfHead").GetBoolean(), "M103 proof-closure commits must remain in package history.");
        }

        JsonElement queueItem = evidence.GetProperty("queueItem");
        Assert.AreEqual("complete", queueItem.GetProperty("status").GetString());
        Assert.AreEqual("verify_closed_package_only", queueItem.GetProperty("completion_action").GetString());
        StringAssert.Contains(queueItem.GetProperty("do_not_reopen_reason").GetString() ?? string.Empty, "future shards must verify this receipt");
        Assert.AreEqual(2257965187, queueItem.GetProperty("frontier_id").GetInt64());
        Assert.AreEqual("a8e4f92c", queueItem.GetProperty("landed_commit").GetString());
        CollectionAssert.AreEqual(ExpectedAllowedPaths, ReadStringArray(queueItem.GetProperty("allowed_paths")));
        CollectionAssert.AreEquivalent(ExpectedSurfaces, ReadStringArray(queueItem.GetProperty("owned_surfaces")));
        CollectionAssert.AreEquivalent(ExpectedProofItems, ReadStringArray(queueItem.GetProperty("proof")));

        JsonElement queueTopLevel = evidence.GetProperty("queueTopLevel");
        AssertQueueHeader(evidence.GetProperty("queueHeaderChecks"), "Fleet queue header");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            queueTopLevel.GetProperty("source_design_queue_path").GetString(),
            "Fleet staging must retain its design-owned queue source pointer.");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
            queueTopLevel.GetProperty("source_registry_path").GetString(),
            "Fleet staging must retain its canonical successor registry pointer.");
        Assert.AreEqual(
            "next90-staging-20260415-next-big-wins-widening",
            queueTopLevel.GetProperty("source_queue_fingerprint").GetString(),
            "Fleet staging must retain the completed M103 source queue fingerprint.");
        Assert.AreEqual(
            "/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            evidence.GetProperty("queuePath").GetString(),
            "M103 proof must keep checking the Fleet staging queue consumed by successor shards.");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            evidence.GetProperty("designQueuePath").GetString(),
            "M103 proof must keep checking the design-owned successor queue source.");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
            evidence.GetProperty("registryPath").GetString(),
            "M103 proof must stay anchored to the canonical successor-wave registry.");

        JsonElement designQueueTopLevel = evidence.GetProperty("designQueueTopLevel");
        AssertQueueHeader(evidence.GetProperty("designQueueHeaderChecks"), "Design queue header");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
            designQueueTopLevel.GetProperty("source_registry_path").GetString(),
            "Design queue staging must retain its canonical successor registry pointer.");
        Assert.AreEqual(
            "next90-staging-20260415-next-big-wins-widening",
            designQueueTopLevel.GetProperty("source_queue_fingerprint").GetString(),
            "Design queue staging must retain the completed M103 source queue fingerprint.");

        AssertQueueHeader(evidence.GetProperty("queueMirrorAlignmentChecks"), "M103 Fleet/design queue row alignment");

        JsonElement registryMilestone = evidence.GetProperty("registryMilestone103");
        foreach (JsonProperty property in registryMilestone.EnumerateObject())
        {
            Assert.IsTrue(
                property.Value.GetBoolean(),
                $"M103 registry authority check must remain true: {property.Name}");
        }
        AssertQueueHeader(evidence.GetProperty("registryProofItemChecks"), "M103 successor registry proof item");

        JsonElement designQueueItem = evidence.GetProperty("designQueueItem");
        Assert.AreEqual("complete", designQueueItem.GetProperty("status").GetString());
        Assert.AreEqual("verify_closed_package_only", designQueueItem.GetProperty("completion_action").GetString());
        StringAssert.Contains(designQueueItem.GetProperty("do_not_reopen_reason").GetString() ?? string.Empty, "future shards must verify this receipt");
        Assert.AreEqual(2257965187, designQueueItem.GetProperty("frontier_id").GetInt64());
        Assert.AreEqual("a8e4f92c", designQueueItem.GetProperty("landed_commit").GetString());
        CollectionAssert.AreEqual(ExpectedAllowedPaths, ReadStringArray(designQueueItem.GetProperty("allowed_paths")));
        CollectionAssert.AreEquivalent(ExpectedSurfaces, ReadStringArray(designQueueItem.GetProperty("owned_surfaces")));
        CollectionAssert.AreEquivalent(ExpectedProofItems, ReadStringArray(designQueueItem.GetProperty("proof")));

        JsonElement promotedBinding = evidence.GetProperty("promotedDesktopHeadBinding");
        Assert.AreEqual("published", promotedBinding.GetProperty("status").GetString());
        Assert.AreEqual("avalonia", promotedBinding.GetProperty("primaryHead").GetString());
        JsonElement primaryRouteTruth = promotedBinding.GetProperty("primaryRouteTruth");
        foreach (string platform in new[] { "linux", "macos", "windows" })
        {
            JsonElement route = primaryRouteTruth.GetProperty(platform);
            Assert.AreEqual("primary", route.GetProperty("routeRole").GetString(), $"{platform} M103 route must stay primary.");
            Assert.AreEqual("promoted", route.GetProperty("promotionState").GetString(), $"{platform} M103 route must stay promoted.");
            Assert.AreEqual("flagship_primary", route.GetProperty("parityPosture").GetString(), $"{platform} M103 route must stay flagship-primary.");
            Assert.IsTrue(route.GetProperty("tupleIdMatchesPromotedHeadAndPlatform").GetBoolean(), $"{platform} M103 route tuple must bind Avalonia to the platform.");
            Assert.IsTrue(route.GetProperty("artifactIdPresent").GetBoolean(), $"{platform} M103 route must keep an artifact id.");
            Assert.IsTrue(route.GetProperty("publicInstallRouteIsDownloadRoute").GetBoolean(), $"{platform} M103 route must keep a public install route.");
        }

        JsonElement standardVerifyPath = evidence.GetProperty("standardVerifyPath");
        Assert.AreEqual(
            Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
            standardVerifyPath.GetProperty("verifyScriptFile").GetString());
        Assert.AreEqual(0, standardVerifyPath.GetProperty("missingMarkers").GetArrayLength());
        CollectionAssert.AreEqual(
            new[]
            {
                "checking next-90 M103 Chummer5a veteran certification guard",
                "bash scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh",
            },
            ReadStringArray(standardVerifyPath.GetProperty("requiredMarkers")));

        JsonElement operatorHelperProofChecks = evidence.GetProperty("operatorHelperProofChecks");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("required_proof_avoids_active_run_helpers").GetBoolean(), "M103 proof constants must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("registry_evidence_avoids_active_run_helpers").GetBoolean(), "M103 registry evidence must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("queue_evidence_avoids_active_run_helpers").GetBoolean(), "M103 Fleet queue evidence must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("design_queue_evidence_avoids_active_run_helpers").GetBoolean(), "M103 design queue evidence must not cite active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("required_proof_avoids_encoded_active_run_helpers").GetBoolean(), "M103 proof constants must not cite encoded active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("registry_evidence_avoids_encoded_active_run_helpers").GetBoolean(), "M103 registry evidence must not cite encoded active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("queue_evidence_avoids_encoded_active_run_helpers").GetBoolean(), "M103 Fleet queue evidence must not cite encoded active-run helper artifacts.");
        Assert.IsTrue(operatorHelperProofChecks.GetProperty("design_queue_evidence_avoids_encoded_active_run_helpers").GetBoolean(), "M103 design queue evidence must not cite encoded active-run helper artifacts.");
        JsonElement operatorHelperTokenHits = evidence.GetProperty("operatorHelperTokenHits");
        Assert.AreEqual(0, operatorHelperTokenHits.GetProperty("required_proof").GetArrayLength(), "M103 required proof must not carry active-run helper token hits.");
        Assert.AreEqual(0, operatorHelperTokenHits.GetProperty("registry_evidence").GetArrayLength(), "M103 registry evidence must not carry active-run helper token hits.");
        Assert.AreEqual(0, operatorHelperTokenHits.GetProperty("queue_evidence").GetArrayLength(), "M103 Fleet queue evidence must not carry active-run helper token hits.");
        Assert.AreEqual(0, operatorHelperTokenHits.GetProperty("design_queue_evidence").GetArrayLength(), "M103 design queue evidence must not carry active-run helper token hits.");
        JsonElement encodedOperatorHelperTokenHits = evidence.GetProperty("encodedOperatorHelperTokenHits");
        Assert.AreEqual(0, encodedOperatorHelperTokenHits.GetProperty("required_proof").GetArrayLength(), "M103 required proof must not carry encoded active-run helper token hits.");
        Assert.AreEqual(0, encodedOperatorHelperTokenHits.GetProperty("registry_evidence").GetArrayLength(), "M103 registry evidence must not carry encoded active-run helper token hits.");
        Assert.AreEqual(0, encodedOperatorHelperTokenHits.GetProperty("queue_evidence").GetArrayLength(), "M103 Fleet queue evidence must not carry encoded active-run helper token hits.");
        Assert.AreEqual(0, encodedOperatorHelperTokenHits.GetProperty("design_queue_evidence").GetArrayLength(), "M103 design queue evidence must not carry encoded active-run helper token hits.");

        Assert.AreEqual(
            repoRoot,
            evidence.GetProperty("sourceRepoRoot").GetString(),
            "M103 source inspection must stay anchored to the local chummer6-ui package repo.");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer6-ui-finish",
            evidence.GetProperty("authorityProofRepoRoot").GetString(),
            "M103 closed-package queue authority should remain bound to the completed-package proof repo.");
        Assert.AreEqual(
            "/docker/chummercomplete/chummer6-ui-finish",
            evidence.GetProperty("expectedProofRepoPrefix").GetString(),
            "M103 completed-package proof must remain scoped to the completed-package authority repo.");
        JsonElement proofScopeChecks = evidence.GetProperty("proofScopeChecks");
        foreach (JsonProperty property in proofScopeChecks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"M103 proof scope check must remain true: {property.Name}");
        }
        JsonElement proofScopePathHits = evidence.GetProperty("proofScopePathHits");
        Assert.AreEqual(0, proofScopePathHits.GetProperty("required_proof").GetArrayLength(), "M103 required proof must not cite sibling repo paths.");
        Assert.AreEqual(0, proofScopePathHits.GetProperty("registry_evidence").GetArrayLength(), "M103 registry proof must not cite sibling repo paths.");
        Assert.AreEqual(0, proofScopePathHits.GetProperty("queue_evidence").GetArrayLength(), "M103 Fleet queue proof must not cite sibling repo paths.");
        Assert.AreEqual(0, proofScopePathHits.GetProperty("design_queue_evidence").GetArrayLength(), "M103 design queue proof must not cite sibling repo paths.");
        JsonElement standardVerifyChecks = evidence.GetProperty("standardVerifyChecks");
        Assert.IsTrue(standardVerifyChecks.GetProperty("verify_entrypoint_avoids_encoded_active_run_helpers").GetBoolean(), "M103 standard verify entrypoint must not cite encoded active-run helper artifacts.");
        Assert.AreEqual(0, evidence.GetProperty("standardVerifyEncodedBlockedActiveRunHelperHits").GetArrayLength(), "M103 standard verify entrypoint must not carry encoded helper hits.");

        string receiptText = File.ReadAllText(receiptPath);
        StringAssert.Contains(receiptText, "\"ACTIVE_RUN_HANDOFF.generated.md\"");
        StringAssert.Contains(receiptText, "\"/var/lib/codex-fleet\"");
        StringAssert.Contains(receiptText, "\"operatorHelperProofChecks\"");
        StringAssert.Contains(receiptText, "\"operatorHelperTokenHits\"");
        StringAssert.Contains(receiptText, "\"encodedOperatorHelperTokenHits\"");
        StringAssert.Contains(receiptText, "\"operator/OODA\"");
        StringAssert.Contains(receiptText, "\"proofScopeChecks\"");
        StringAssert.Contains(receiptText, "\"proofScopePathHits\"");
        StringAssert.Contains(receiptText, "\"sourceRepoRoot\"");
        StringAssert.Contains(receiptText, "\"authorityProofRepoRoot\"");
        StringAssert.Contains(receiptText, "\"standardVerifyChecks\"");
        StringAssert.Contains(receiptText, "\"standardVerifyEncodedBlockedActiveRunHelperHits\"");
        StringAssert.Contains(receiptText, "\"visualFamiliarityStatus\"");
        StringAssert.Contains(receiptText, "\"visualFamiliarityFailureCount\"");
        StringAssert.Contains(receiptText, "\"visualFamiliarityReviewChecks\"");
        StringAssert.Contains(receiptText, "\"visualFamiliarityReviewSummary\"");
        StringAssert.Contains(receiptText, "\"screenshotControlEvidence\"");
        StringAssert.Contains(receiptText, "\"screenshotControlEvidenceChecks\"");
        StringAssert.Contains(receiptText, "\"screenshotControlEvidenceSummary\"");
        StringAssert.Contains(receiptText, "\"visualDifferenceLedger\"");
        StringAssert.Contains(receiptText, "\"visualDifferenceLedgerChecks\"");
        StringAssert.Contains(receiptText, "\"visualDifferenceLedgerSummary\"");

        JsonElement surfaceResults = evidence.GetProperty("surfaceResults");
        JsonElement certificationMatrix = evidence.GetProperty("veteranCertificationMatrix");
        Assert.AreEqual(6, certificationMatrix.GetArrayLength(), "M103 certification matrix must carry one row per assigned desktop parity surface.");
        CollectionAssert.AreEquivalent(
            new[] { "menu", "toolstrip", "roster", "master_index", "settings", "import" },
            certificationMatrix.EnumerateArray()
                .Select(row => row.GetProperty("surface").GetString() ?? string.Empty)
                .ToArray(),
            "M103 certification matrix must cover exactly the assigned desktop parity surfaces.");

        HashSet<string> screenshotDigests = new(StringComparer.Ordinal);
        foreach (string surface in new[] { "menu", "toolstrip", "roster", "master_index", "settings", "import" })
        {
            JsonElement result = surfaceResults.GetProperty(surface);
            Assert.IsTrue(result.GetProperty("screenshotGeneratedByTest").GetBoolean(), $"{surface} screenshot must be generated by the Avalonia gate.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotExists").GetBoolean(), $"{surface} screenshot must be published.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotIsPng").GetBoolean(), $"{surface} screenshot must be a valid PNG.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotWidth").GetInt32() >= 1280, $"{surface} screenshot is too narrow.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotHeight").GetInt32() >= 800, $"{surface} screenshot is too short.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotContentSampled").GetBoolean(), $"{surface} screenshot content must be sampled.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotContentNonBlank").GetBoolean(), $"{surface} screenshot must not be blank.");
            Assert.IsTrue(result.GetProperty("publishedScreenshotDistinctSampleColors").GetInt32() >= 3, $"{surface} screenshot content sample is too flat.");
            Assert.AreEqual(0, result.GetProperty("missingMarkers").GetArrayLength(), $"{surface} source markers must remain present.");
            JsonElement workflowMap = result.GetProperty("workflowMap");
            Assert.IsFalse(string.IsNullOrWhiteSpace(workflowMap.GetProperty("legacyFamiliarity").GetString()), $"{surface} must explain the Chummer5a familiarity claim.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(workflowMap.GetProperty("promotedHeadTaskProof").GetString()), $"{surface} must explain the promoted-head task proof.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(workflowMap.GetProperty("parityQuestion").GetString()), $"{surface} must retain its veteran parity question.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(workflowMap.GetProperty("screenshotEvidenceRole").GetString()), $"{surface} must name the screenshot evidence role.");
            JsonElement captureMarkerChecks = result.GetProperty("captureMarkerChecks");
            Assert.IsTrue(captureMarkerChecks.GetProperty("requiredMarkers").GetArrayLength() > 0, $"{surface} must bind screenshot proof to concrete headless interactions.");
            Assert.AreEqual(0, captureMarkerChecks.GetProperty("missingMarkers").GetArrayLength(), $"{surface} screenshot capture markers must remain present.");
            if (surface is "settings" or "master_index" or "roster")
            {
                CollectionAssert.Contains(
                    ReadStringArray(captureMarkerChecks.GetProperty("requiredMarkers")),
                    "AssertDialogContainsAll(",
                    $"{surface} screenshot must assert visible dialog content before capture.");
            }

            if (surface == "import")
            {
                string[] requiredMarkers = ReadStringArray(captureMarkerChecks.GetProperty("requiredMarkers"));
                CollectionAssert.Contains(requiredMarkers, "harness.Click(\"FileMenuButton\")");
                CollectionAssert.Contains(requiredMarkers, "harness.ClickMenuCommand(\"open_character\")");
                CollectionAssert.Contains(requiredMarkers, "\"Open Character\"");
            }

            Assert.IsTrue(result.GetProperty("sourceFileChecks").GetArrayLength() > 0, $"{surface} must bind screenshot proof to at least one concrete source file.");
            foreach (JsonElement sourceFileCheck in result.GetProperty("sourceFileChecks").EnumerateArray())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(sourceFileCheck.GetProperty("sourceFile").GetString()), $"{surface} source-file proof must name the checked source file.");
                Assert.IsTrue(sourceFileCheck.GetProperty("markerCount").GetInt32() > 0, $"{surface} source-file proof must carry markers.");
                Assert.AreEqual(0, sourceFileCheck.GetProperty("missingMarkers").GetArrayLength(), $"{surface} source-file markers must remain present.");
            }
            JsonElement matrixRow = result.GetProperty("certificationMatrixRow");
            Assert.AreEqual(surface, matrixRow.GetProperty("surface").GetString());
            Assert.AreEqual("screenshot_parity:desktop", matrixRow.GetProperty("ownedSurface").GetString());
            Assert.AreEqual("avalonia", matrixRow.GetProperty("promotedHead").GetString());
            Assert.AreEqual(result.GetProperty("screenshot").GetString(), matrixRow.GetProperty("screenshot").GetString());
            Assert.AreEqual(result.GetProperty("publishedScreenshotSha256").GetString(), matrixRow.GetProperty("screenshotSha256").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(matrixRow.GetProperty("gesture").GetString()), $"{surface} certification matrix must name the promoted-head gesture.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(matrixRow.GetProperty("chummer5aBaseline").GetString()), $"{surface} certification matrix must state the Chummer5a baseline.");
            StringAssert.Contains(matrixRow.GetProperty("chummer5aBaselineFile").GetString() ?? string.Empty, "/docker/chummer5a/");
            Assert.IsGreaterThan(0, matrixRow.GetProperty("chummer5aMarkerCount").GetInt32(), $"{surface} certification matrix must count Chummer5a markers.");
            Assert.AreEqual(0, matrixRow.GetProperty("missingChummer5aMarkers").GetArrayLength(), $"{surface} certification matrix must have no missing Chummer5a markers.");
            Assert.IsGreaterThan(0, matrixRow.GetProperty("sourceFileProofCount").GetInt32(), $"{surface} certification matrix must count source-file proof.");
            Assert.IsGreaterThan(0, matrixRow.GetProperty("captureMarkerCount").GetInt32(), $"{surface} certification matrix must count capture proof.");
            Assert.IsTrue(matrixRow.GetProperty("screenshotContentNonBlank").GetBoolean(), $"{surface} certification matrix must prove nonblank screenshot content.");
            Assert.IsGreaterThanOrEqualTo(3, matrixRow.GetProperty("screenshotDistinctSampleColors").GetInt32(), $"{surface} certification matrix screenshot sample is too flat.");
            Assert.IsTrue(screenshotDigests.Add(result.GetProperty("publishedScreenshotSha256").GetString() ?? string.Empty), $"{surface} screenshot must be distinct.");
        }

        StringAssert.EndsWith(evidence.GetProperty("eventHandlersSourceFile").GetString(), "Chummer.Avalonia/MainWindow.EventHandlers.cs");
        StringAssert.StartsWith(evidence.GetProperty("sourceTestFile").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        StringAssert.StartsWith(evidence.GetProperty("complianceGuardFile").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        StringAssert.StartsWith(evidence.GetProperty("toolstripSourceFile").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        StringAssert.StartsWith(evidence.GetProperty("menuSourceFile").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        StringAssert.StartsWith(evidence.GetProperty("eventHandlersSourceFile").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        StringAssert.StartsWith(evidence.GetProperty("publishedScreenshotDir").GetString() ?? string.Empty, repoRoot, StringComparison.Ordinal);
        string reviewMarkdownPath = evidence.GetProperty("publishedScreenshotReviewMarkdownPath").GetString() ?? string.Empty;
        StringAssert.StartsWith(reviewMarkdownPath, repoRoot, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(reviewMarkdownPath), "M103 must materialize a local screenshot review markdown packet.");
        JsonElement reviewMarkdownChecks = evidence.GetProperty("reviewMarkdownChecks");
        foreach (JsonProperty property in reviewMarkdownChecks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"M103 review markdown check must remain true: {property.Name}");
        }
        Assert.AreEqual("pass", evidence.GetProperty("visualFamiliarityStatus").GetString(), "M103 must bind to a passing visual familiarity receipt.");
        Assert.AreEqual(0, evidence.GetProperty("visualFamiliarityFailureCount").GetInt32(), "M103 must bind to a zero-failure visual familiarity receipt.");
        JsonElement visualFamiliarityReviewChecks = evidence.GetProperty("visualFamiliarityReviewChecks");
        foreach (JsonProperty property in visualFamiliarityReviewChecks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"M103 visual familiarity review check must remain true: {property.Name}");
        }
        JsonElement visualFamiliarityReviewSummary = evidence.GetProperty("visualFamiliarityReviewSummary");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "flagshipGateReview",
                "headProofReview",
                "interactionProofReview",
                "sourceAnchorReview",
                "screenCaptureReview",
                "legacyFamiliarityReview",
            },
            ReadStringArray(visualFamiliarityReviewSummary.GetProperty("requiredReviewKeys")),
            "M103 must require the full visual familiarity review bucket set.");
        Assert.AreEqual(0, visualFamiliarityReviewSummary.GetProperty("missingReviewKeys").GetArrayLength(), "M103 must not tolerate missing visual familiarity review buckets.");
        Assert.AreEqual(0, visualFamiliarityReviewSummary.GetProperty("failingReviewKeys").GetArrayLength(), "M103 must not tolerate failing visual familiarity review buckets.");
        Assert.AreEqual(0, visualFamiliarityReviewSummary.GetProperty("reviewReasonCountMismatches").GetArrayLength(), "M103 must not tolerate visual familiarity review reasonCount drift.");
        JsonElement visualFamiliarityReviewReasonCounts = visualFamiliarityReviewSummary.GetProperty("reviewReasonCounts");
        foreach (string reviewKey in new[]
                 {
                     "flagshipGateReview",
                     "headProofReview",
                     "interactionProofReview",
                     "sourceAnchorReview",
                     "screenCaptureReview",
                     "legacyFamiliarityReview",
                 })
        {
            Assert.AreEqual(0, visualFamiliarityReviewReasonCounts.GetProperty(reviewKey).GetInt32(), $"M103 visual familiarity review must stay clean: {reviewKey}");
        }
        string screenshotControlEvidencePath = evidence.GetProperty("screenshotControlEvidencePath").GetString() ?? string.Empty;
        StringAssert.StartsWith(screenshotControlEvidencePath, repoRoot, StringComparison.Ordinal);
        JsonElement screenshotControlEvidence = evidence.GetProperty("screenshotControlEvidence");
        Assert.AreEqual(18, screenshotControlEvidence.GetArrayLength(), "M103 screenshot control evidence must cover every required screenshot frame/dialog.");
        JsonElement screenshotControlEvidenceChecks = evidence.GetProperty("screenshotControlEvidenceChecks");
        foreach (JsonProperty property in screenshotControlEvidenceChecks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"M103 screenshot control evidence check must remain true: {property.Name}");
        }
        CollectionAssert.AreEquivalent(
            ReadStringArray(evidence.GetProperty("visualFamiliarityRequiredScreenshots")),
            screenshotControlEvidence.EnumerateArray()
                .Select(entry => entry.GetProperty("screenshot").GetString() ?? string.Empty)
                .ToArray(),
            "M103 screenshot control evidence screenshot coverage must match the required screenshot pack exactly.");
        foreach (JsonElement entry in screenshotControlEvidence.EnumerateArray())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("theme").GetString()), "M103 screenshot control evidence entries must capture the current theme.");
            Assert.IsTrue(entry.GetProperty("visibleNamedControlIds").GetArrayLength() > 0, "M103 screenshot control evidence entries must capture visible named controls.");
            Assert.IsTrue(entry.GetProperty("visibleNamedControls").GetArrayLength() > 0, "M103 screenshot control evidence entries must capture visible named control details.");
            string? dialogTitle = entry.GetProperty("dialogTitle").GetString();
            if (!string.IsNullOrWhiteSpace(dialogTitle)
                && !string.Equals(dialogTitle, "(none)", StringComparison.Ordinal))
            {
                Assert.IsTrue(entry.GetProperty("dialogFieldIds").GetArrayLength() > 0, "M103 dialog screenshots must capture generated dialog field ids.");
                Assert.IsTrue(entry.GetProperty("dialogFieldControlIds").GetArrayLength() > 0, "M103 dialog screenshots must capture generated dialog control ids.");
                Assert.IsTrue(entry.GetProperty("dialogActionControlIds").GetArrayLength() > 0, "M103 dialog screenshots must capture generated dialog action control ids.");
            }
        }
        string visualDifferenceLedgerPath = evidence.GetProperty("visualDifferenceLedgerPath").GetString() ?? string.Empty;
        StringAssert.StartsWith(visualDifferenceLedgerPath, repoRoot, StringComparison.Ordinal);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "01-initial-shell-light.png",
                "02-menu-open-light.png",
                "03-settings-open-light.png",
                "04-loaded-runner-light.png",
                "05-dense-section-light.png",
                "06-dense-section-dark.png",
                "07-loaded-runner-tabs-light.png",
                "08-cyberware-dialog-light.png",
                "09-vehicles-section-light.png",
                "10-contacts-section-light.png",
                "11-diary-dialog-light.png",
                "12-magic-dialog-light.png",
                "13-matrix-dialog-light.png",
                "14-advancement-dialog-light.png",
                "15-creation-section-light.png",
                "16-master-index-dialog-light.png",
                "17-character-roster-dialog-light.png",
                "18-import-dialog-light.png",
            },
            ReadStringArray(evidence.GetProperty("visualFamiliarityRequiredScreenshots")),
            "M103 visual difference ledger must bind to every required visual-familiarity screenshot.");
        JsonElement visualDifferenceLedger = evidence.GetProperty("visualDifferenceLedger");
        Assert.AreEqual(18, visualDifferenceLedger.GetArrayLength(), "M103 visual difference ledger must cover every required screenshot frame/dialog.");
        JsonElement visualDifferenceLedgerChecks = evidence.GetProperty("visualDifferenceLedgerChecks");
        foreach (JsonProperty property in visualDifferenceLedgerChecks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"M103 visual difference ledger check must remain true: {property.Name}");
        }
        CollectionAssert.AreEquivalent(
            ReadStringArray(evidence.GetProperty("visualFamiliarityRequiredScreenshots")),
            visualDifferenceLedger.EnumerateArray()
                .Select(entry => entry.GetProperty("screenshot").GetString() ?? string.Empty)
                .ToArray(),
            "M103 visual difference ledger screenshot coverage must match the required screenshot pack exactly.");
        foreach (JsonElement entry in visualDifferenceLedger.EnumerateArray())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("surface").GetString()), "M103 visual difference ledger entries must name the surface.");
            Assert.IsTrue(
                new[] { "frame", "dialog" }.Contains(entry.GetProperty("surfaceKind").GetString()),
                "M103 visual difference ledger entries must declare frame/dialog kind.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.GetProperty("parityIntent").GetString()), "M103 visual difference ledger entries must retain parity intent.");
            JsonElement evidenceAnchors = entry.GetProperty("evidenceAnchors");
            Assert.IsTrue(evidenceAnchors.GetArrayLength() >= 2, "M103 visual difference ledger entries must carry at least two evidence anchors.");
            foreach (JsonElement evidenceAnchor in evidenceAnchors.EnumerateArray())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(evidenceAnchor.GetString()), "M103 visual difference ledger evidence anchors must be non-empty.");
            }
            JsonElement differences = entry.GetProperty("differences");
            Assert.IsTrue(differences.GetArrayLength() > 0, "M103 visual difference ledger entries must carry at least one difference.");
            foreach (JsonElement difference in differences.EnumerateArray())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(difference.GetProperty("uiElement").GetString()), "M103 visual difference rows must name the UI element.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(difference.GetProperty("legacyPosture").GetString()), "M103 visual difference rows must describe legacy posture.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(difference.GetProperty("currentPosture").GetString()), "M103 visual difference rows must describe current posture.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(difference.GetProperty("whyItDiffers").GetString()), "M103 visual difference rows must explain why the difference exists.");
            }
        }
        string reviewMarkdown = File.ReadAllText(reviewMarkdownPath);
        string screenshotDir = evidence.GetProperty("publishedScreenshotDir").GetString() ?? string.Empty;
        StringAssert.Contains(reviewMarkdown, "# Next90 M103 Veteran Certification Review");
        StringAssert.Contains(reviewMarkdown, $"Receipt: `{receiptPath}`");
        StringAssert.Contains(reviewMarkdown, $"Screenshot pack: `{screenshotDir}`");
        StringAssert.Contains(reviewMarkdown, $"Source repo: `{repoRoot}`");
        StringAssert.Contains(reviewMarkdown, "Authority proof repo: `/docker/chummercomplete/chummer6-ui-finish`");
        StringAssert.Contains(reviewMarkdown, $"Screenshot control evidence: `{screenshotControlEvidencePath}`");
        StringAssert.Contains(reviewMarkdown, "| Surface | Parity Question | Promoted-Head Proof | Legacy Familiarity | Screenshot | Sample Colors | SHA-256 |");
        StringAssert.Contains(reviewMarkdown, $"Difference ledger source: `{visualDifferenceLedgerPath}`");
        StringAssert.Contains(reviewMarkdown, "## Audited UI Differences");
        StringAssert.Contains(reviewMarkdown, "Observed named controls:");
        StringAssert.Contains(reviewMarkdown, "Evidence anchors:");
        StringAssert.Contains(reviewMarkdown, "Why it differs:");
        StringAssert.Contains(reviewMarkdown, "01-initial-shell-light.png");
        StringAssert.Contains(reviewMarkdown, "02-menu-open-light.png");
        StringAssert.Contains(reviewMarkdown, "03-settings-open-light.png");
        StringAssert.Contains(reviewMarkdown, "04-loaded-runner-light.png");
        StringAssert.Contains(reviewMarkdown, "05-dense-section-light.png");
        StringAssert.Contains(reviewMarkdown, "06-dense-section-dark.png");
        StringAssert.Contains(reviewMarkdown, "07-loaded-runner-tabs-light.png");
        StringAssert.Contains(reviewMarkdown, "08-cyberware-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "09-vehicles-section-light.png");
        StringAssert.Contains(reviewMarkdown, "10-contacts-section-light.png");
        StringAssert.Contains(reviewMarkdown, "11-diary-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "12-magic-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "13-matrix-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "14-advancement-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "15-creation-section-light.png");
        StringAssert.Contains(reviewMarkdown, "16-master-index-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "17-character-roster-dialog-light.png");
        StringAssert.Contains(reviewMarkdown, "18-import-dialog-light.png");

        JsonElement legacyBaselineResults = evidence.GetProperty("legacyBaselineResults");
        foreach (string surface in new[] { "menu", "toolstrip", "roster", "master_index", "settings", "import" })
        {
            Assert.AreEqual(0, legacyBaselineResults.GetProperty(surface).GetProperty("missingMarkers").GetArrayLength(), $"{surface} Chummer5a baseline markers must remain present.");
        }

        JsonElement legacyImportRouteBaselineResults = evidence.GetProperty("legacyImportRouteBaselineResults");
        StringAssert.EndsWith(
            legacyImportRouteBaselineResults.GetProperty("sourceFile").GetString(),
            "Chummer/Forms/ChummerMainForm.Designer.cs",
            "Import route baseline must stay anchored to the Chummer5a main form File/Open route.");
        Assert.AreEqual(
            0,
            legacyImportRouteBaselineResults.GetProperty("missingMarkers").GetArrayLength(),
            "Import route baseline must keep Chummer5a File/Open and importer lineage markers.");
        Assert.IsGreaterThanOrEqualTo(
            legacyImportRouteBaselineResults.GetProperty("markerCount").GetInt32(),
            5,
            "Import route baseline must carry enough markers to prove normal veteran file-open lineage.");
    }

    private static string[] ReadStringArray(JsonElement array)
        => array.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToArray();

    private static void AssertQueueHeader(JsonElement checks, string label)
    {
        foreach (JsonProperty property in checks.EnumerateObject())
        {
            Assert.IsTrue(property.Value.GetBoolean(), $"{label} check must remain true: {property.Name}");
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
