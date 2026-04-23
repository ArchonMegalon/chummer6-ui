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
        StringAssert.Contains(scriptText, "frontier_id: {FRONTIER_ID}");
        StringAssert.Contains(scriptText, "EXPECTED_QUEUE_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "queue_allowed_path_items");
        StringAssert.Contains(scriptText, "queue_owned_surface_items");
        StringAssert.Contains(scriptText, "design_queue_allowed_path_items");
        StringAssert.Contains(scriptText, "design_queue_owned_surface_items");
        StringAssert.Contains(scriptText, "queueProofItems");
        StringAssert.Contains(scriptText, "designQueueProofItems");
        StringAssert.Contains(scriptText, "queueProofItemsMatchDesignQueue");
        StringAssert.Contains(scriptText, "Fleet and design-owned M101 queue proof items must match exactly.");
        StringAssert.Contains(scriptText, "Fleet M101 queue proof item(s) are missing from the design-owned queue row");
        StringAssert.Contains(scriptText, "Design-owned M101 queue proof item(s) are missing from the Fleet queue row");
        StringAssert.Contains(scriptText, "\"allowed_paths_exact\"");
        StringAssert.Contains(scriptText, "\"owned_surfaces_exact\"");
        StringAssert.Contains(scriptText, "EXPECTED_DESIGN_QUEUE_PATH");
        StringAssert.Contains(scriptText, "EXPECTED_SOURCE_REGISTRY_PATH");
        StringAssert.Contains(scriptText, "EXPECTED_PROGRAM_WAVE");
        StringAssert.Contains(scriptText, "EXPECTED_QUEUE_STATUS");
        StringAssert.Contains(scriptText, "EXPECTED_SOURCE_QUEUE_FINGERPRINT");
        StringAssert.Contains(scriptText, "EXPECTED_PACKAGE_TITLE = \"Keep native-host release proof independent for the primary desktop head\"");
        StringAssert.Contains(scriptText, "title: {EXPECTED_PACKAGE_TITLE}");
        StringAssert.Contains(scriptText, "\"title_matches\"");
        StringAssert.Contains(scriptText, "\"packageTitle\": EXPECTED_PACKAGE_TITLE");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON");
        StringAssert.Contains(scriptText, "completion_action: {EXPECTED_COMPLETION_ACTION}");
        StringAssert.Contains(scriptText, "do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}");
        StringAssert.Contains(scriptText, "\"completion_action_verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "\"do_not_reopen_reason_matches\"");
        StringAssert.Contains(scriptText, "\"completionAction\": EXPECTED_COMPLETION_ACTION");
        StringAssert.Contains(scriptText, "\"doNotReopenClosedPackage\": True");
        StringAssert.Contains(scriptText, "next90-staging-20260415-next-big-wins-widening");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m101-ui-release-train-check.sh");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M101ReleaseTrainGuardTests.cs");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 5844ad03 pins successor frontier 2450443084 into the completed M101 proof guard.");
        StringAssert.Contains(scriptText, "LANDED_COMMIT,");
        StringAssert.Contains(scriptText, "\"da549ef8\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 2e87dce3 tightens M101 verifier against design-owned queue source drift.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit c61a8fb5 pins M101 design queue closure tokens into the verifier, receipt, and compliance guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 79760cc1 refreshes the M101 release train receipt after queue closure proof tightening.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit a3bf058e tightens M101 proof commit resolution so stale proof anchors cannot keep the closed package green.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0954e2a1 pins M101 proof resolution guard into verifier, receipt, and compliance proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit e519ca4b pins the latest M101 proof anchor into the verifier, receipt, and compliance guard.");
        StringAssert.Contains(scriptText, "\"e519ca4b\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f3e0e90b tightens the M101 blocked-helper proof guard so closed-package evidence cannot cite active-run telemetry or operator helper commands.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit a8944fa5 pins the M101 blocked-helper proof anchor into the verifier, receipt, and compliance guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b481d3ef refreshes the M101 release train receipt after blocked-helper anchor proof tightening.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 52b118ff pins the latest M101 release train proof anchors.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 24eb3732 tightens the M101 queue source-fingerprint proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 48970414 pins M101 queue fingerprint proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 2ef1a22d pins M101 latest queue proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 8bc1fb02 pins M101 latest queue proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 9629b207 pins M101 current queue proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 6c032e2c pins M101 current queue proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 5c069924 pins M101 current proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 8115735b pins M101 current proof floor guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0605657d pins M101 811 proof floor guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 53b701e2 pins M101 060 proof floor guard.");
        StringAssert.Contains(scriptText, "\"007182bc\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit a0303d5f pins M101 latest release train proof floor.");
        StringAssert.Contains(scriptText, "\"a0303d5f\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0fa3ce01 pins the current M101 release train proof floor.");
        StringAssert.Contains(scriptText, "\"0fa3ce01\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b0c0b732 pins M101 current release train proof floor.");
        StringAssert.Contains(scriptText, "\"b0c0b732\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 3f99eb0a tightens the M101 blocked-helper proof scan.");
        StringAssert.Contains(scriptText, "\"3f99eb0a\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b21ca671 pins M101 blocked-helper scan proof floor.");
        StringAssert.Contains(scriptText, "\"b21ca671\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0849d8c2 tightens M101 proof commit scope so closure evidence cannot cite unrelated repo changes.");
        StringAssert.Contains(scriptText, "\"0849d8c2\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit e64db32c pins M101 release train standard verify guard.");
        StringAssert.Contains(scriptText, "\"e64db32c\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit bb268a79 refreshes the M101 release train proof receipt after canonical successor queue verification.");
        StringAssert.Contains(scriptText, "\"bb268a79\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 7945695d pins the refreshed M101 release train proof receipt into the verifier and compliance guard.");
        StringAssert.Contains(scriptText, "\"7945695d\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 9e3d931a pins M101 package identity, allowed scope, owned surfaces, landed commit, and Avalonia independence at the receipt top level.");
        StringAssert.Contains(scriptText, "\"9e3d931a\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 492e8f83 records the M101 top-level package-proof floor in the verifier, compliance guard, and generated receipt.");
        StringAssert.Contains(scriptText, "\"492e8f83\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 31cb7cf7 tightens the M101 release train proof floor.");
        StringAssert.Contains(scriptText, "\"31cb7cf7\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 5a282824 pins the M101 release train verifier, compliance guard, and generated receipt to proof floor 31cb7cf7.");
        StringAssert.Contains(scriptText, "\"5a282824\"");
        StringAssert.Contains(scriptText, "\"8e8d97a4\"");
        StringAssert.Contains(scriptText, "\"bd340416\"");
        StringAssert.Contains(scriptText, "\"faba38da\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 237e039d pins the M101 active-run proof guard floor so future shards verify the latest completed-package guard instead of repeating it.");
        StringAssert.Contains(scriptText, "\"237e039d\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 49a5466c pins M101 latest release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 90c0a763 pins the M101 verifier, generated receipt, and compliance guard to proof floor 49a5466c.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 60092e8d pins the M101 release train verifier, generated receipt, and compliance guard to the canonical 90c0a763 proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 5403219b stabilizes the M101 release train receipt timestamp so repeated proof checks do not reopen the completed package.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 871c7f7b pins the M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 8b0e1801 pins the current M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit eae55383 pins the current M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 287c7538 pins the M101 proof floor to the latest completed-package guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit fa67f014 tightens the M101 queue-row uniqueness guard so future shards reject duplicate completed-package rows instead of repeating the closed slice.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit c63379a3 pins M101 queue uniqueness proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 44ac83db pins the M101 queue uniqueness proof floor into the verifier, compliance guard, and generated receipt.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0c239ada tightens the M101 run-control proof guard so future shards reject worker-unsafe closure citations.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 52086c9d tightens the M101 active-run field proof guard so copied task-local status fields cannot close the completed package.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 82df294e pins the M101 active-run field proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit bb90dca8 tightens M101 verify entrypoint hygiene.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 20487c22 pins M101 verify entrypoint proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit bc01c725 pins the M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 8ac6d072 pins the latest M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 1c7b5819 tightens M101 queue proof commit guard so completed queue proof commit citations must resolve locally inside package scope.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit aa394d32 pins the M101 queue proof commit guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 8db934d3 tightens M101 Avalonia startup-smoke receipt independence proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit cb1fe210 pins M101 receipt independence proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 75b38965 tightens M101 blocked-helper proof source traceability.");
        StringAssert.Contains(scriptText, "\"56d9733a\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit db4fc1e1 tightens M101 worker-context proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 4a4079f5 pins the latest M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 9b97ab1a tightens M101 primary route-truth proof so Avalonia primary evidence cannot smuggle fallback tokens into proof-bearing fields.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f563293f pins M101 primary route proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit c9f49b5b tightens M101 closed-package proof so future shards verify the completed package instead of reopening the Avalonia primary-route slice.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f11cff77 tightens M101 authority proof path scope so canonical proof citations cannot drift outside the Avalonia release-train package.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 22380dee tightens M101 authority proof item scope so canonical registry and queue proof/evidence items cannot drift outside the Avalonia release-train package.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 93f7dcea pins the M101 authority proof item guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit de600a43 pins the M101 authority proof guard floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 6dd1064f tightens the M101 primary-route desktop executable proof guard.");
        StringAssert.Contains(scriptText, "\"6dd1064f\"");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b99e13fd pins M101 Avalonia receipt identity proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 28533e61 pins M101 Avalonia receipt floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 82334376 pins M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 466e0fc0 tightens M101 queue scope proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b8dcab2d pins M101 queue scope proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 757783c4 pins M101 b8 queue scope proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0b8414d7 pins M101 current release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit b958e116 tightens M101 standard verify mutation coverage so Avalonia primary route-truth rows cannot cite Blazor fallback proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 46a9f070 tightens M101 standard verify artifact-identity mutation coverage so Avalonia primary route-truth artifact IDs cannot cite Blazor fallback proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit ccc77950 pins M101 artifact mutation proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 4f103b72 pins M101 current release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit deff0535 pins the current M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 342bff22 pins M101 active-run proof guard floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0e894712 pins M101 active-run guard proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit e923acd0 pins M101 current proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 0758c4a1 pins M101 current proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 235f6db6 pins M101 release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit eef780a5 tightens M101 required desktop platform and head proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 84959efa tightens M101 startup receipt fallback proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit c896be32 tightens M101 Avalonia route-truth artifact matching proof.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 9846ce73 pins M101 Avalonia artifact proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit a3917b15 pins M101 current release train proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 1c8aa33c tightens M101 closed queue proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f7fcf1a9 tightens M101 queue title proof.");
        StringAssert.Contains(scriptText, "\"f7fcf1a9\"");
        StringAssert.Contains(scriptText, "ui_work_task_queue_title_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit f3779b5d pins M101 queue title proof floor.");
        StringAssert.Contains(scriptText, "\"f3779b5d\"");
        StringAssert.Contains(scriptText, "ui_work_task_queue_title_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 4779b4c9 tightens M101 encoded worker-context proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit a0decd1a tightens M101 hex-encoded helper proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 9a0a00b6 pins M101 hex helper proof floor.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit c7b4a56f tightens M101 escaped helper proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 58bc9f1b pins M101 escaped helper proof guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit fb8ad231 resolves the M101 release train proof citation to the current generated receipt and verifier guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 46eb74e2 tightens the M101 release train proof citation.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 35433ce3 tightens M101 proof floor authority guard.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit d7c9b1ec tightens M101 startup-smoke identity, install-posture, and fallback-receipt proof for the Avalonia primary release train.");
        StringAssert.Contains(scriptText, "/docker/chummercomplete/chummer6-ui-finish commit 79fb7eb9 tightens M101 percent-decoded fallback-proof smuggling guards for the Avalonia primary release train.");
        StringAssert.Contains(scriptText, "\"362686fb\"");
        StringAssert.Contains(scriptText, "ui_work_task_encoded_helper_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_hex_encoded_helper_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_hex_helper_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_escaped_helper_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_escaped_helper_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "\"355b497e\"");
        StringAssert.Contains(scriptText, "\"b99e13fd\"");
        StringAssert.Contains(scriptText, "\"82334376\"");
        StringAssert.Contains(scriptText, "\"466e0fc0\"");
        StringAssert.Contains(scriptText, "\"0b8414d7\"");
        StringAssert.Contains(scriptText, "\"4f103b72\"");
        StringAssert.Contains(scriptText, "\"deff0535\"");
        StringAssert.Contains(scriptText, "\"2e8f29b7\"");
        StringAssert.Contains(scriptText, "\"342bff22\"");
        StringAssert.Contains(scriptText, "\"a3917b15\"");
        StringAssert.Contains(scriptText, "EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT = \"362686fb\"");
        StringAssert.Contains(scriptText, "\"currentPackageProofFloorCommit\": EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT");
        StringAssert.Contains(scriptText, "previous_semantic_payload.pop(\"generatedAt\", None)");
        StringAssert.Contains(scriptText, "payload[\"generatedAt\"] = previous_payload[\"generatedAt\"]");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_receipt_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_canonical_release_train_receipt_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_receipt_timestamp_stability_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_proof_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_proof_floor_receipt_pin_v3_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_proof_floor_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_completed_package_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_row_uniqueness_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_uniqueness_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_uniqueness_receipt_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_operator_ooda_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_active_run_field_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_active_run_field_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_verify_entrypoint_hygiene_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_verify_entrypoint_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_release_train_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_proof_commit_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_proof_commit_guard_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_receipt_independence_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_receipt_independence_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_source_traceability_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_worker_context_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_proof_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_primary_route_truth_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_primary_route_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_closed_package_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_authority_path_scope_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_authority_proof_item_scope_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_authority_proof_item_guard_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_authority_proof_guard_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_primary_route_desktop_executable_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_primary_route_verify_mutation_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_primary_route_artifact_identity_verify_mutation_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_artifact_mutation_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_proof_floor_v3_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_release_train_floor_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_frontier_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_receipt_refresh_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_proof_commit_resolution_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_anchor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_receipt_refresh_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_release_train_anchor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_fingerprint_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_queue_fingerprint_anchor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_queue_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_queue_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_queue_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_queue_proof_floor_anchor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_proof_floor_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_proof_floor_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_local_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_scan_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_blocked_helper_scan_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_proof_commit_scope_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_standard_verify_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_refreshed_receipt_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_refreshed_receipt_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_receipt_top_level_package_proof_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_top_level_package_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_release_train_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_required_desktop_platform_head_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_avalonia_artifact_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_current_release_train_proof_floor_v5_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_closed_queue_proof_guard_pin_present");
        StringAssert.Contains(scriptText, "ui_work_task_latest_release_train_proof_floor_pin_present");
        StringAssert.Contains(scriptText, "commit 5844ad03 pins successor frontier 2450443084 into the M101 verifier");
        StringAssert.Contains(scriptText, "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true");
        StringAssert.Contains(scriptText, "EXPECTED_RESOLVING_PROOF_COMMITS");
        StringAssert.Contains(scriptText, "EXPECTED_QUEUE_PROOF_COMMIT_TOKENS");
        StringAssert.Contains(scriptText, "re.search(r\"\\bcommit\\s+([0-9a-f]{7,40})\\b\", token)");
        StringAssert.Contains(scriptText, "queue_proof_commit_tokens");
        StringAssert.Contains(scriptText, "authority_row_proof_commit_tokens");
        StringAssert.Contains(scriptText, "queue_proof_commit_tokens_resolve");
        StringAssert.Contains(scriptText, "authority_row_proof_commit_tokens_resolve");
        StringAssert.Contains(scriptText, "Queue proof cites commit {commit} without adding it to the resolving proof floor.");
        StringAssert.Contains(scriptText, "Queue proof cites commit {commit} but it did not resolve inside M101 package/proof scope.");
        StringAssert.Contains(scriptText, "Canonical M101 authority proof cites commit {commit} without adding it to the resolving proof floor.");
        StringAssert.Contains(scriptText, "Canonical M101 authority proof cites commit {commit} but it did not resolve inside M101 package/proof scope.");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_COMMIT_PATH_PREFIXES");
        StringAssert.Contains(scriptText, "EXPECTED_PROOF_PATH_PREFIXES");
        StringAssert.Contains(scriptText, "extract_ui_repo_path_tokens");
        StringAssert.Contains(scriptText, "proof_commit_paths");
        StringAssert.Contains(scriptText, "proof_commit_scope");
        StringAssert.Contains(scriptText, "proof_commit_scope_allowed_prefixes");
        StringAssert.Contains(scriptText, "authority_row_proof_path_tokens");
        StringAssert.Contains(scriptText, "authority_row_proof_path_scope");
        StringAssert.Contains(scriptText, "proof_path_scope_allowed_prefixes");
        StringAssert.Contains(scriptText, "ALLOWED_AUTHORITY_PROOF_ITEM_PREFIXES");
        StringAssert.Contains(scriptText, "ALLOWED_AUTHORITY_PROOF_ITEMS");
        StringAssert.Contains(scriptText, "extract_yaml_list_items_after_key");
        StringAssert.Contains(scriptText, "authority_proof_item_in_scope");
        StringAssert.Contains(scriptText, "authority_row_proof_items");
        StringAssert.Contains(scriptText, "authority_row_proof_item_scope");
        StringAssert.Contains(scriptText, "authority_proof_item_scope_allowed_prefixes");
        StringAssert.Contains(scriptText, "authority_proof_item_scope_allowed_items");
        StringAssert.Contains(scriptText, "Canonical M101 authority proof item is outside M101 package/proof scope");
        StringAssert.Contains(scriptText, "Package proof commit {commit} changed paths outside M101 package/proof scope");
        StringAssert.Contains(scriptText, "Canonical M101 authority proof cites path outside M101 package/proof scope");
        StringAssert.Contains(scriptText, ".codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json");
        StringAssert.Contains(scriptText, "Package proof commit {commit} does not resolve in local chummer6-ui history.");
        StringAssert.Contains(scriptText, "queue_checks[f\"proof_{token}\"]");
        StringAssert.Contains(scriptText, "design_queue_checks[f\"proof_{token}\"]");
        StringAssert.Contains(scriptText, "DISALLOWED_ACTIVE_RUN_PROOF_TOKENS");
        StringAssert.Contains(scriptText, "TASK_LOCAL_TELEMETRY.generated.json");
        StringAssert.Contains(scriptText, "TASK_LOCAL_TELEMETRY.generated");
        StringAssert.Contains(scriptText, "TASK_LOCAL_TELEMETRY");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF.generated.md");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF.generated");
        StringAssert.Contains(scriptText, "ACTIVE_RUN_HANDOFF");
        StringAssert.Contains(scriptText, "active_run_handoff");
        StringAssert.Contains(scriptText, "/var/lib/codex-fleet/chummer_design_supervisor");
        StringAssert.Contains(scriptText, "Prompt path:");
        StringAssert.Contains(scriptText, "Recent stderr tail");
        StringAssert.Contains(scriptText, "Active Run");
        StringAssert.Contains(scriptText, "scripts/ooda_design_supervisor.py");
        StringAssert.Contains(scriptText, "scripts/run_ooda_design_supervisor_until_quiet.py");
        StringAssert.Contains(scriptText, "run_ooda_design_supervisor");
        StringAssert.Contains(scriptText, "supervisor status");
        StringAssert.Contains(scriptText, "status helper");
        StringAssert.Contains(scriptText, "block_contains_ci");
        StringAssert.Contains(scriptText, "needle.casefold() in block.casefold()");
        StringAssert.Contains(scriptText, "block_contains_encoded_ci");
        StringAssert.Contains(scriptText, "base64.b64decode");
        StringAssert.Contains(scriptText, "gzip.decompress");
        StringAssert.Contains(scriptText, "zlib.decompress");
        StringAssert.Contains(scriptText, "encodedBlockedActiveRunProofHits");
        StringAssert.Contains(scriptText, "M101 package proof cites encoded blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "block_contains_hex_encoded_ci");
        StringAssert.Contains(scriptText, "bytes.fromhex");
        StringAssert.Contains(scriptText, "hexEncodedBlockedActiveRunProofHits");
        StringAssert.Contains(scriptText, "M101 package proof cites hex-encoded blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "block_contains_escaped_ci");
        StringAssert.Contains(scriptText, "urllib.parse.unquote");
        StringAssert.Contains(scriptText, "html.unescape");
        StringAssert.Contains(scriptText, "decode_json_unicode_escapes");
        StringAssert.Contains(scriptText, "for _ in range(4):");
        StringAssert.Contains(scriptText, "next_decoded = {");
        StringAssert.Contains(scriptText, "seen.update(next_decoded)");
        StringAssert.Contains(scriptText, "re.sub(r\"\\\\u([0-9a-fA-F]{4})\"");
        StringAssert.Contains(scriptText, "escapedBlockedActiveRunProofHits");
        StringAssert.Contains(scriptText, "M101 package proof cites escaped blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "scalar_leaf_values");
        StringAssert.Contains(scriptText, "fallback_token_hits");
        StringAssert.Contains(scriptText, "scalar_contains_fallback_token");
        StringAssert.Contains(scriptText, "block_contains_encoded_ci(text, token)");
        StringAssert.Contains(scriptText, "block_contains_hex_encoded_ci(text, token)");
        StringAssert.Contains(scriptText, "block_contains_escaped_ci(text, token)");
        StringAssert.Contains(scriptText, "path_folded = path.casefold()");
        StringAssert.Contains(scriptText, "token in text_folded");
        StringAssert.Contains(scriptText, "or token in path_folded");
        StringAssert.Contains(scriptText, "PRIMARY_ROUTE_TRUTH_FALLBACK_DISTINCT_FIELDS");
        StringAssert.Contains(scriptText, "\"rollbackReason\"");
        StringAssert.Contains(scriptText, "fallback_distinct_field_hits");
        StringAssert.Contains(scriptText, "primary_route_truth_all_scalar_fields_avoid_fallback_head");
        StringAssert.Contains(scriptText, "primary_route_truth_proof_fields_distinct_from_fallback_row");
        StringAssert.Contains(scriptText, "fallback_row_promoted_for_rollback");
        StringAssert.Contains(scriptText, "allowed_primary_route_fallback_reference_paths = {\"$.rollbackReason\"}");
        StringAssert.Contains(scriptText, "primary_route_truth_rollback_reason_names_primary_tuple");
        StringAssert.Contains(scriptText, "primary_route_truth_rollback_reason_names_fallback_tuple");
        StringAssert.Contains(scriptText, "primary_route_truth_rollback_state_matches_fallback_promotion_truth");
        StringAssert.Contains(scriptText, "primary_route_truth_rollback_reason_code_matches_fallback_promotion_truth");
        StringAssert.Contains(scriptText, "primary_route_truth_rollback_reason_matches_fallback_promotion_truth");
        StringAssert.Contains(scriptText, "\"a promoted fallback route \"");
        StringAssert.Contains(scriptText, "\"fallback_missing_artifact_or_startup_smoke_proof\"");
        StringAssert.Contains(scriptText, "receipt_all_scalar_fields_avoid_fallback_head");
        StringAssert.Contains(scriptText, "avalonia primary route-truth scalar field(s) cite fallback proof");
        StringAssert.Contains(scriptText, "avalonia primary route-truth proof field(s) reuse fallback row value");
        StringAssert.Contains(scriptText, "startup-smoke receipt scalar field(s) cite fallback proof");
        StringAssert.Contains(scriptText, "operator/OODA");
        StringAssert.Contains(scriptText, "operator telemetry or active-run helper commands");
        StringAssert.Contains(scriptText, "operator telemetry helper");
        StringAssert.Contains(scriptText, "operator/OODA loop");
        StringAssert.Contains(scriptText, "OODA loop");
        StringAssert.Contains(scriptText, "Do not query supervisor");
        StringAssert.Contains(scriptText, "active-run helper commands");
        StringAssert.Contains(scriptText, "active run helper");
        StringAssert.Contains(scriptText, "run-state helper");
        StringAssert.Contains(scriptText, "worker-safe resume context");
        StringAssert.Contains(scriptText, "worker-state helper");
        StringAssert.Contains(scriptText, "status_query_supported");
        StringAssert.Contains(scriptText, "polling_disabled");
        StringAssert.Contains(scriptText, "supervisor telemetry");
        StringAssert.Contains(scriptText, "supervisor eta");
        StringAssert.Contains(scriptText, "successor-wave telemetry");
        StringAssert.Contains(scriptText, "successor telemetry");
        StringAssert.Contains(scriptText, "remaining milestones");
        StringAssert.Contains(scriptText, "remaining queue items");
        StringAssert.Contains(scriptText, "critical path");
        StringAssert.Contains(scriptText, "scope_label");
        StringAssert.Contains(scriptText, "frontier_briefs");
        StringAssert.Contains(scriptText, "Open milestone ids");
        StringAssert.Contains(scriptText, "Successor frontier ids");
        StringAssert.Contains(scriptText, "Successor frontier detail");
        StringAssert.Contains(scriptText, "eta:");
        StringAssert.Contains(scriptText, "blockedActiveRunProofScanMode");
        StringAssert.Contains(scriptText, "\"case_insensitive\"");
        StringAssert.Contains(scriptText, "blockedActiveRunProofSources");
        StringAssert.Contains(scriptText, "\"label\": \"registry\"");
        StringAssert.Contains(scriptText, "\"label\": \"queue\"");
        StringAssert.Contains(scriptText, "\"label\": \"design_queue\"");
        StringAssert.Contains(scriptText, "M101 blocked-helper proof source is missing");
        StringAssert.Contains(scriptText, "M101 package proof cites blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "\"packageId\": PACKAGE_ID");
        StringAssert.Contains(scriptText, "\"milestoneId\": MILESTONE_ID");
        StringAssert.Contains(scriptText, "\"landedCommit\": LANDED_COMMIT");
        StringAssert.Contains(scriptText, "\"ownedSurfaces\": EXPECTED_SURFACES");
        StringAssert.Contains(scriptText, "\"allowedPaths\": EXPECTED_ALLOWED_PATHS");
        StringAssert.Contains(scriptText, "\"primaryProofIndependentFromFallback\": evidence[\"primaryProofIndependentFromFallback\"]");
        StringAssert.Contains(scriptText, "source_design_queue_path_matches");
        StringAssert.Contains(scriptText, "source_registry_path_matches");
        StringAssert.Contains(scriptText, "source_queue_fingerprint_matches");
        StringAssert.Contains(scriptText, "count_top_level_item_blocks");
        StringAssert.Contains(scriptText, "queuePackageRowCount");
        StringAssert.Contains(scriptText, "designQueuePackageRowCount");
        StringAssert.Contains(scriptText, "package_row_count_exactly_one");
        StringAssert.Contains(scriptText, "status_live_parallel_successor");
        StringAssert.Contains(scriptText, "program_wave_matches");
        StringAssert.Contains(scriptText, "designQueueTopLevel");
        StringAssert.Contains(scriptText, "Design-owned package queue check failed");
        StringAssert.Contains(scriptText, "verifyScriptPath");
        StringAssert.Contains(scriptText, "standardVerifyChecks");
        StringAssert.Contains(scriptText, "m101_guard_wired");
        StringAssert.Contains(scriptText, "verify_entrypoint_avoids_active_run_helpers");
        StringAssert.Contains(scriptText, "verify_entrypoint_avoids_encoded_active_run_helpers");
        StringAssert.Contains(scriptText, "standardVerifyBlockedActiveRunHelperHits");
        StringAssert.Contains(scriptText, "standardVerifyEncodedBlockedActiveRunHelperHits");
        StringAssert.Contains(scriptText, "standardVerifyHexEncodedBlockedActiveRunHelperHits");
        StringAssert.Contains(scriptText, "Standard verify script cites blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "Standard verify script cites encoded blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "Standard verify script cites hex-encoded blocked active-run/operator helper evidence");
        StringAssert.Contains(scriptText, "Standard verify wiring check failed");
        StringAssert.Contains(scriptText, "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh");
        StringAssert.Contains(scriptText, "EXPECTED_REQUIRED_PLATFORM_HEAD_RID_TUPLES");
        StringAssert.Contains(scriptText, "EXPECTED_REQUIRED_DESKTOP_PLATFORMS");
        StringAssert.Contains(scriptText, "EXPECTED_REQUIRED_DESKTOP_HEADS");
        StringAssert.Contains(scriptText, "requiredDesktopHeads must be exactly avalonia for M101 primary-route proof");
        StringAssert.Contains(scriptText, "requiredDesktopPlatforms must be exactly linux, macos, and windows for M101 primary-route proof");
        StringAssert.Contains(scriptText, "requiredDesktopPlatformHeadRidTuples must be exactly the Avalonia primary-route tuples");
        StringAssert.Contains(scriptText, "promotedPlatformHeads does not list avalonia first as primary desktop route");
        StringAssert.Contains(scriptText, "promotedPlatformHeads must list avalonia exactly once");
        StringAssert.Contains(scriptText, "\"primaryPromotedHead\"");
        StringAssert.Contains(scriptText, "avalonia desktopRouteTruth rid does not match required tuple");
        StringAssert.Contains(scriptText, "avalonia desktopRouteTruth arch does not match required tuple");
        StringAssert.Contains(scriptText, "avalonia desktopRouteTruth tupleId does not match required primary tuple");
        StringAssert.Contains(scriptText, "avalonia desktopRouteTruth publicInstallRoute does not match promoted avalonia installer route");
        StringAssert.Contains(scriptText, "\"expectedRouteTupleId\"");
        StringAssert.Contains(scriptText, "\"expectedPublicInstallRoute\"");
        StringAssert.Contains(scriptText, "\"routeRid\"");
        StringAssert.Contains(scriptText, "\"routeArch\"");
        StringAssert.Contains(scriptText, "FALLBACK_PROOF_TEXT_TOKENS");
        StringAssert.Contains(scriptText, "PRIMARY_ROUTE_TRUTH_PROOF_FIELDS");
        StringAssert.Contains(scriptText, "primaryRouteTruthRequiredFieldChecks");
        StringAssert.Contains(scriptText, "avalonia primary route-truth required field is blank:");
        StringAssert.Contains(scriptText, "avalonia primary route-truth independence check failed:");
        StringAssert.Contains(scriptText, "primaryRouteTruthIndependenceChecks");
        StringAssert.Contains(scriptText, "routeRoleReason");
        StringAssert.Contains(scriptText, "promotionReason");
        StringAssert.Contains(scriptText, "publicInstallRoute");
        StringAssert.Contains(scriptText, "startup-smoke receipt independence check failed:");
        StringAssert.Contains(scriptText, "receipt_artifact_id_matches_primary_artifact_when_present");
        StringAssert.Contains(scriptText, "receipt_primary_artifact_locator_present");
        StringAssert.Contains(scriptText, "receipt_primary_artifact_locator_names_primary_head");
        StringAssert.Contains(scriptText, "digest == expected_digest");
        StringAssert.Contains(scriptText, "return None, None");
        StringAssert.Contains(scriptText, "startupSmokeReceiptIndependenceChecks");
        StringAssert.Contains(scriptText, "routeTruthRowCardinality");
        StringAssert.Contains(scriptText, "primaryRouteTruthRowCounts");
        StringAssert.Contains(scriptText, "fallbackRouteTruthRowCounts");
        StringAssert.Contains(scriptText, "unexpectedPrimaryOrFallbackRouteTruthRows");
        StringAssert.Contains(scriptText, "ALLOWED_DESKTOP_ROUTE_TRUTH_KEYS");
        StringAssert.Contains(scriptText, "\"routeRoleReasonCode\"");
        StringAssert.Contains(scriptText, "\"promotionReasonCode\"");
        StringAssert.Contains(scriptText, "\"rollbackReasonCode\"");
        StringAssert.Contains(scriptText, "\"revokeReasonCode\"");
        StringAssert.Contains(scriptText, "unexpectedPrimaryOrFallbackRouteTruthKeys");
        StringAssert.Contains(scriptText, "nonObjectDesktopRouteTruthRows");
        StringAssert.Contains(scriptText, "desktopRouteTruth must contain exactly one avalonia primary row");
        StringAssert.Contains(scriptText, "desktopRouteTruth must contain exactly one blazor-desktop fallback row");
        StringAssert.Contains(scriptText, "desktopRouteTruth contains unexpected primary/fallback route rows outside the required M101 platforms");
        StringAssert.Contains(scriptText, "desktopRouteTruth contains non-object row(s) that cannot prove head/platform/route-role independence");
        StringAssert.Contains(scriptText, "desktopRouteTruth primary/fallback row has unexpected proof key(s):");
        StringAssert.Contains(scriptText, "avalonia desktopRouteTruth routeRoleReasonCode is not primary_flagship_head");
        StringAssert.Contains(scriptText, "blazor-desktop fallback row is incorrectly marked with primary_flagship_head reason code");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_m101_primary_route_fallback_proof_leak()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "m101_primary_route_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "m101_queue_proof_mirror_mutation_queue");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted Fleet queue proof drift from the design-owned queue row.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the queue proof mirror drift marker.");
        StringAssert.Contains(verifyScriptText, "Fleet and design-owned M101 queue proof items must match exactly.");
        StringAssert.Contains(verifyScriptText, "m101_queue_html_entity_helper_mutation_queue");
        StringAssert.Contains(verifyScriptText, "m101_queue_nested_escape_helper_mutation_queue");
        StringAssert.Contains(verifyScriptText, "urllib.parse.quote(urllib.parse.quote(blocked, safe=\"\"), safe=\"\")");
        StringAssert.Contains(verifyScriptText, "chr(95)");
        StringAssert.Contains(verifyScriptText, "\"&#x{ord(character):x};\"");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted HTML-entity helper proof in Fleet queue.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted nested-escaped helper proof in Fleet queue.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the escaped helper proof marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the nested escaped helper proof marker.");
        StringAssert.Contains(verifyScriptText, "M101 package proof cites escaped blocked active-run/operator helper evidence: queue:");
        StringAssert.Contains(verifyScriptText, "primary route incorrectly leans on blazor-desktop fallback proof");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_extra_scalar_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "blazor-desktop-fallback-proof-smuggled-through-noncanonical-scalar");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_encoded_scalar_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "base64.b64encode(b\"blazor-desktop fallback proof\")");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with encoded fallback proof in a scalar field.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_compressed_encoded_scalar_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "gzip.compress(b\"blazor-desktop fallback proof\")");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with compressed encoded fallback proof in a scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route compressed encoded fallback marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_zlib_encoded_scalar_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "zlib.compress(b\"blazor-desktop fallback proof\")");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with zlib-compressed encoded fallback proof in a scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route zlib encoded fallback marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_extra_scalar_key_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "blazorFallbackProofReceipt");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_unexpected_key_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "supportingReceiptPath");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with an ungoverned proof key.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route unexpected proof key marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_non_object_row_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "blazor-desktop fallback proof smuggled as a non-object route-truth row");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted a non-object desktopRouteTruth fallback-proof row.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the non-object route-truth marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_fallback_reuse_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row that reused a fallback proof-bearing value.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route fallback row reuse marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_fallback_rollback_reuse_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_unpromoted_fallback_rollback_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route rollback rationale that reused a fallback proof-bearing value.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route rollback state that leaned on an unpromoted fallback row.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route fallback rollback reuse marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the unpromoted fallback rollback marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_artifact_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "blazor-desktop-fallback-artifact-smuggled-into-avalonia-primary-proof");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row that cited Blazor fallback proof.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with fallback proof in a noncanonical scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route row with fallback proof in a noncanonical scalar key.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route artifactId that cited Blazor fallback proof.");
        StringAssert.Contains(verifyScriptText, "m101_primary_artifact_match_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "chummer-avalonia-linux-x64-wrong-primary-artifact");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route artifactId that did not match the promoted installer artifact.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route artifact match marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_public_install_route_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "/downloads/install/avalonia-linux-x64-detached-proof");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route public install route that did not match the promoted installer artifact.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route public install route marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_reason_code_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "routeRoleReasonCode");
        StringAssert.Contains(verifyScriptText, "fallback_recovery_head");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route reason code that identified fallback recovery proof.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route reason-code marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_route_rid_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia primary-route rid that did not match the required platform tuple.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route rid tuple marker.");
        StringAssert.Contains(verifyScriptText, "m101_primary_head_order_mutation_release_channel");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted Blazor as the first promoted platform head for a required desktop platform.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-head ordering marker.");
        StringAssert.Contains(verifyScriptText, "promotedPlatformHeads does not list avalonia first as primary desktop route");
        StringAssert.Contains(verifyScriptText, "m101_startup_receipt_mutation_root");
        StringAssert.Contains(verifyScriptText, "m101_startup_receipt_extra_scalar_mutation_root");
        StringAssert.Contains(verifyScriptText, "m101_startup_receipt_encoded_scalar_mutation_root");
        StringAssert.Contains(verifyScriptText, "m101_startup_receipt_zlib_encoded_scalar_mutation_root");
        StringAssert.Contains(verifyScriptText, "m101_startup_receipt_extra_scalar_key_mutation_root");
        StringAssert.Contains(verifyScriptText, "blazor-desktop-fallback-receipt-smuggled-into-avalonia-startup-smoke");
        StringAssert.Contains(verifyScriptText, "blazor-desktop-fallback-proof-smuggled-through-receipt-scalar");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia startup-smoke receipt with encoded fallback proof in a scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia startup-smoke receipt with zlib-compressed encoded fallback proof in a scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia startup-smoke receipt that cited Blazor fallback proof.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia startup-smoke receipt with fallback proof in a noncanonical scalar field.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 guard accepted an Avalonia startup-smoke receipt with fallback proof in a noncanonical scalar key.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route independence marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route all-scalar fallback marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route encoded fallback marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route scalar-key fallback marker.");
        StringAssert.Contains(verifyScriptText, "desktopRouteTruth primary/fallback row has unexpected proof key(s): avalonia:linux:avalonia:linux:linux-x64: supportingReceiptPath");
        StringAssert.Contains(verifyScriptText, "desktopRouteTruth contains non-object row\\(s\\) that cannot prove head/platform/route-role independence");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the primary-route artifact identity marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the startup-smoke receipt independence marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the startup-smoke all-scalar fallback marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the startup-smoke encoded fallback marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the startup-smoke zlib encoded fallback marker.");
        StringAssert.Contains(verifyScriptText, "next-90 M101 mutation did not emit the startup-smoke scalar-key fallback marker.");
        StringAssert.Contains(verifyScriptText, "primary_route_truth_routeRoleReason_avoids_fallback_head");
        StringAssert.Contains(verifyScriptText, "primary_route_truth_artifactId_avoids_fallback_head");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth proof field(s) reuse fallback row value: updateEligibilityReason");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth proof field(s) reuse fallback row value: rollbackReason");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth independence check failed: primary_route_truth_rollback_reason_names_primary_tuple");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth independence check failed: primary_route_truth_rollback_state_matches_fallback_promotion_truth");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth independence check failed: primary_route_truth_rollback_reason_code_matches_fallback_promotion_truth");
        StringAssert.Contains(verifyScriptText, "avalonia primary route-truth scalar field(s) cite fallback proof");
        StringAssert.Contains(verifyScriptText, "avalonia desktopRouteTruth artifactId does not match promoted avalonia installer artifact");
        StringAssert.Contains(verifyScriptText, "avalonia desktopRouteTruth publicInstallRoute does not match promoted avalonia installer route");
        StringAssert.Contains(verifyScriptText, "avalonia desktopRouteTruth routeRoleReasonCode is not primary_flagship_head");
        StringAssert.Contains(verifyScriptText, "avalonia desktopRouteTruth rid does not match required tuple");
        StringAssert.Contains(verifyScriptText, "startup-smoke receipt independence check failed: receipt_artifact_path_avoids_fallback_head");
        StringAssert.Contains(verifyScriptText, "startup-smoke receipt independence check failed: receipt_all_scalar_fields_avoid_fallback_head");
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
        StringAssert.Contains(root.GetProperty("doNotReopenReason").GetString(), "future shards must verify this receipt");
        Assert.IsTrue(root.GetProperty("primaryProofIndependentFromFallback").GetBoolean());
        Assert.AreEqual(0, root.GetProperty("reasons").GetArrayLength());
        CollectionAssert.AreEquivalent(
            new[] { "desktop_release_train:avalonia", "flagship_route_truth:desktop" },
            root.GetProperty("ownedSurfaces")
                .EnumerateArray()
                .Select(surface => surface.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            root.GetProperty("allowedPaths")
                .EnumerateArray()
                .Select(path => path.GetString())
                .ToArray());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(2450443084, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual("verify_closed_package_only", evidence.GetProperty("completionAction").GetString());
        Assert.IsTrue(evidence.GetProperty("doNotReopenClosedPackage").GetBoolean());
        StringAssert.Contains(evidence.GetProperty("doNotReopenReason").GetString(), "instead of reopening the Avalonia primary-route package");
        Assert.IsTrue(evidence.GetProperty("primaryProofIndependentFromFallback").GetBoolean());
        JsonElement gitChecks = evidence.GetProperty("gitChecks");
        JsonElement proofCommits = gitChecks.GetProperty("resolving_proof_commits");
        JsonElement proofCommitScope = gitChecks.GetProperty("proof_commit_scope");
        Assert.IsTrue(proofCommits.GetProperty("c9c0d84f").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c9c0d84f").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("da549ef8").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("da549ef8").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("5844ad03").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("5844ad03").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("2e87dce3").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("2e87dce3").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("c61a8fb5").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c61a8fb5").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("79760cc1").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("79760cc1").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a3bf058e").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a3bf058e").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0954e2a1").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0954e2a1").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("e519ca4b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("e519ca4b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("7e0c8d07").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("7e0c8d07").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("54766b3a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("54766b3a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("f3e0e90b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("f3e0e90b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a8944fa5").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a8944fa5").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b481d3ef").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b481d3ef").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("52b118ff").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("52b118ff").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("24eb3732").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("24eb3732").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("48970414").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("48970414").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("2ef1a22d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("2ef1a22d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8bc1fb02").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8bc1fb02").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("9629b207").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("9629b207").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("6c032e2c").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("6c032e2c").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("5c069924").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("5c069924").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8115735b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8115735b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0605657d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0605657d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("53b701e2").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("53b701e2").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("007182bc").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("007182bc").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a0303d5f").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a0303d5f").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0fa3ce01").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0fa3ce01").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b0c0b732").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b0c0b732").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("3f99eb0a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("3f99eb0a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b21ca671").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b21ca671").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0849d8c2").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0849d8c2").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("e64db32c").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("e64db32c").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("bb268a79").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("bb268a79").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("7945695d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("7945695d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("9e3d931a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("9e3d931a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("492e8f83").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("492e8f83").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("31cb7cf7").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("31cb7cf7").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("5a282824").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("5a282824").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8e8d97a4").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8e8d97a4").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("bd340416").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("bd340416").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("faba38da").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("faba38da").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("237e039d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("237e039d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("49a5466c").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("49a5466c").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("90c0a763").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("90c0a763").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("60092e8d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("60092e8d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("5403219b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("5403219b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("871c7f7b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("871c7f7b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8b0e1801").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8b0e1801").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("eae55383").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("eae55383").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("287c7538").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("287c7538").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("fa67f014").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("fa67f014").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("c63379a3").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c63379a3").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("44ac83db").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("44ac83db").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0c239ada").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0c239ada").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("52086c9d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("52086c9d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("82df294e").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("82df294e").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("bb90dca8").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("bb90dca8").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("20487c22").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("20487c22").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("bc01c725").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("bc01c725").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8ac6d072").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8ac6d072").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("1c7b5819").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("1c7b5819").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("aa394d32").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("aa394d32").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("8db934d3").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("8db934d3").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("cb1fe210").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("cb1fe210").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("75b38965").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("75b38965").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("56d9733a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("56d9733a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("db4fc1e1").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("db4fc1e1").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("4a4079f5").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("4a4079f5").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("9b97ab1a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("9b97ab1a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("f563293f").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("f563293f").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("c9f49b5b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c9f49b5b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("f11cff77").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("f11cff77").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("22380dee").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("22380dee").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("93f7dcea").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("93f7dcea").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("de600a43").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("de600a43").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("6dd1064f").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("6dd1064f").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("355b497e").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("355b497e").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b99e13fd").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b99e13fd").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("28533e61").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("28533e61").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("82334376").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("82334376").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("466e0fc0").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("466e0fc0").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b8dcab2d").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b8dcab2d").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("757783c4").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("757783c4").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0b8414d7").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0b8414d7").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("b958e116").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("b958e116").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("46a9f070").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("46a9f070").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("ccc77950").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("ccc77950").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("4f103b72").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("4f103b72").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("deff0535").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("deff0535").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("2e8f29b7").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("2e8f29b7").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("342bff22").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("342bff22").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0e894712").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0e894712").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("e923acd0").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("e923acd0").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("0758c4a1").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("0758c4a1").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("235f6db6").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("235f6db6").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("eef780a5").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("eef780a5").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("84959efa").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("84959efa").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("c896be32").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c896be32").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("9846ce73").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("9846ce73").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a3917b15").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a3917b15").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("1c8aa33c").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("1c8aa33c").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("4779b4c9").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("4779b4c9").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a0decd1a").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a0decd1a").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("9a0a00b6").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("9a0a00b6").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("c7b4a56f").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("c7b4a56f").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("58bc9f1b").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("58bc9f1b").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("217d67fe").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("217d67fe").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("a7ff93b4").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("a7ff93b4").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("fb8ad231").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("fb8ad231").GetBoolean());
        Assert.IsTrue(proofCommits.GetProperty("362686fb").GetBoolean());
        Assert.IsTrue(proofCommitScope.GetProperty("362686fb").GetBoolean());
        JsonElement queueProofCommitTokens = gitChecks.GetProperty("queue_proof_commit_tokens");
        JsonElement queueProofCommitTokensResolve = gitChecks.GetProperty("queue_proof_commit_tokens_resolve");
        JsonElement authorityProofCommitTokens = gitChecks.GetProperty("authority_row_proof_commit_tokens");
        JsonElement authorityProofCommitTokensResolve = gitChecks.GetProperty("authority_row_proof_commit_tokens_resolve");
        JsonElement authorityProofPathTokens = gitChecks.GetProperty("authority_row_proof_path_tokens");
        JsonElement authorityProofPathScope = gitChecks.GetProperty("authority_row_proof_path_scope");
        JsonElement authorityProofItems = gitChecks.GetProperty("authority_row_proof_items");
        JsonElement authorityProofItemScope = gitChecks.GetProperty("authority_row_proof_item_scope");
        JsonElement proofItemScopePrefixes = gitChecks.GetProperty("authority_proof_item_scope_allowed_prefixes");
        JsonElement proofItemScopeItems = gitChecks.GetProperty("authority_proof_item_scope_allowed_items");
        JsonElement proofPathScopePrefixes = gitChecks.GetProperty("proof_path_scope_allowed_prefixes");
        Assert.IsTrue(proofPathScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/"));
        Assert.IsTrue(proofPathScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/"));
        Assert.IsTrue(proofPathScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/"));
        Assert.IsTrue(proofPathScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/scripts/"));
        Assert.IsTrue(proofPathScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json"));
        Assert.IsTrue(proofItemScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/"));
        Assert.IsTrue(proofItemScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/scripts/"));
        Assert.IsTrue(proofItemScopePrefixes.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish commit "));
        Assert.IsTrue(proofItemScopeItems.EnumerateArray().Any(item => item.GetString() == "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh"));
        Assert.IsTrue(proofItemScopeItems.EnumerateArray().Any(item => item.GetString() == "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh exits 0."));
        Assert.IsTrue(authorityProofItems.EnumerateArray().Any(item => item.GetString() == "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true"));
        Assert.IsTrue(authorityProofItems.EnumerateArray().Any(item => item.GetString() == "source assertion check for the M101 guard tokens and generated primaryProofIndependentFromFallback=true exits 0."));
        Assert.IsTrue(authorityProofPathTokens.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m101-ui-release-train-check.sh"));
        Assert.IsTrue(authorityProofPathTokens.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs"));
        Assert.IsTrue(authorityProofPathTokens.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M101ReleaseTrainGuardTests.cs"));
        Assert.IsTrue(authorityProofPathTokens.EnumerateArray().Any(path => path.GetString() == "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json"));
        foreach (JsonProperty pathScope in authorityProofPathScope.EnumerateObject())
        {
            Assert.IsTrue(pathScope.Value.GetBoolean(), pathScope.Name);
        }
        foreach (JsonProperty itemScope in authorityProofItemScope.EnumerateObject())
        {
            Assert.IsTrue(itemScope.Value.GetBoolean(), itemScope.Name);
        }
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "e519ca4b"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("e519ca4b").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "1c7b5819"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("1c7b5819").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "aa394d32"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("aa394d32").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "8db934d3"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("8db934d3").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "cb1fe210"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("cb1fe210").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "75b38965"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("75b38965").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "db4fc1e1"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("db4fc1e1").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "4a4079f5"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("4a4079f5").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "9b97ab1a"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("9b97ab1a").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "f563293f"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("f563293f").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "c9f49b5b"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("c9f49b5b").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "f11cff77"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("f11cff77").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "22380dee"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("22380dee").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "93f7dcea"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("93f7dcea").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "de600a43"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("de600a43").GetBoolean());
        Assert.IsTrue(queueProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "6dd1064f"));
        Assert.IsTrue(queueProofCommitTokensResolve.GetProperty("6dd1064f").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "aa394d32"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("aa394d32").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "8db934d3"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("8db934d3").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "cb1fe210"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("cb1fe210").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "75b38965"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("75b38965").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "db4fc1e1"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("db4fc1e1").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "4a4079f5"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("4a4079f5").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "9b97ab1a"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("9b97ab1a").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "f563293f"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("f563293f").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "c9f49b5b"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("c9f49b5b").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "f11cff77"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("f11cff77").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "22380dee"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("22380dee").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "93f7dcea"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("93f7dcea").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "de600a43"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("de600a43").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "6dd1064f"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("6dd1064f").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "b99e13fd"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("b99e13fd").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "28533e61"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("28533e61").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "82334376"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("82334376").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "466e0fc0"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("466e0fc0").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "b8dcab2d"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("b8dcab2d").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "757783c4"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("757783c4").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "0b8414d7"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("0b8414d7").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "b958e116"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("b958e116").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "46a9f070"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("46a9f070").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "ccc77950"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("ccc77950").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "4f103b72"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("4f103b72").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "9846ce73"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("9846ce73").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "a3917b15"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("a3917b15").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "1c8aa33c"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("1c8aa33c").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "4779b4c9"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("4779b4c9").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "a0decd1a"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("a0decd1a").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "9a0a00b6"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("9a0a00b6").GetBoolean());
        Assert.IsTrue(authorityProofCommitTokens.EnumerateArray().Any(commit => commit.GetString() == "c7b4a56f"));
        Assert.IsTrue(authorityProofCommitTokensResolve.GetProperty("c7b4a56f").GetBoolean());
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            evidence.GetProperty("designQueuePath").GetString());
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
            evidence.GetProperty("queueTopLevel").GetProperty("source_design_queue_path").GetString());
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
            evidence.GetProperty("queueTopLevel").GetProperty("source_registry_path").GetString());
        Assert.AreEqual("next_90_day_product_advance", evidence.GetProperty("queueTopLevel").GetProperty("program_wave").GetString());
        Assert.AreEqual("live_parallel_successor", evidence.GetProperty("queueTopLevel").GetProperty("status").GetString());
        Assert.AreEqual(
            "next90-staging-20260415-next-big-wins-widening",
            evidence.GetProperty("queueTopLevel").GetProperty("source_queue_fingerprint").GetString());
        Assert.AreEqual(
            "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
            evidence.GetProperty("designQueueTopLevel").GetProperty("source_registry_path").GetString());
        Assert.AreEqual("next_90_day_product_advance", evidence.GetProperty("designQueueTopLevel").GetProperty("program_wave").GetString());
        Assert.AreEqual("live_parallel_successor", evidence.GetProperty("designQueueTopLevel").GetProperty("status").GetString());
        Assert.AreEqual(
            "next90-staging-20260415-next-big-wins-widening",
            evidence.GetProperty("designQueueTopLevel").GetProperty("source_queue_fingerprint").GetString());
        JsonElement routeTruthRowCardinality = evidence.GetProperty("routeTruthRowCardinality");
        Assert.AreEqual("avalonia", routeTruthRowCardinality.GetProperty("primaryHead").GetString());
        Assert.AreEqual("blazor-desktop", routeTruthRowCardinality.GetProperty("fallbackHead").GetString());
        CollectionAssert.AreEquivalent(
            RequiredPlatforms,
            routeTruthRowCardinality.GetProperty("expectedPlatforms")
                .EnumerateArray()
                .Select(platform => platform.GetString())
                .ToArray());
        foreach (string platform in RequiredPlatforms)
        {
            Assert.AreEqual(1, routeTruthRowCardinality.GetProperty("primaryRouteTruthRowCounts").GetProperty(platform).GetInt32());
            Assert.AreEqual(1, routeTruthRowCardinality.GetProperty("fallbackRouteTruthRowCounts").GetProperty(platform).GetInt32());
        }
        Assert.AreEqual(0, routeTruthRowCardinality.GetProperty("unexpectedPrimaryOrFallbackRouteTruthRows").GetArrayLength());
        Assert.AreEqual(
            Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
            evidence.GetProperty("verifyScriptPath").GetString());
        Assert.IsTrue(evidence.GetProperty("standardVerifyChecks").GetProperty("m101_guard_wired").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("standardVerifyChecks").GetProperty("verify_entrypoint_avoids_active_run_helpers").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("standardVerifyChecks").GetProperty("verify_entrypoint_avoids_encoded_active_run_helpers").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("standardVerifyChecks").GetProperty("verify_entrypoint_avoids_hex_encoded_active_run_helpers").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("standardVerifyChecks").GetProperty("verify_entrypoint_avoids_escaped_active_run_helpers").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("standardVerifyBlockedActiveRunHelperHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("standardVerifyEncodedBlockedActiveRunHelperHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("standardVerifyHexEncodedBlockedActiveRunHelperHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("standardVerifyEscapedBlockedActiveRunHelperHits").GetArrayLength());
        JsonElement designQueueChecks = evidence.GetProperty("designQueueChecks");
        Assert.AreEqual(1, evidence.GetProperty("queuePackageRowCount").GetInt32());
        Assert.AreEqual(1, evidence.GetProperty("designQueuePackageRowCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("frontier_matches").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("package_row_count_exactly_one").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("source_registry_path_matches").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("source_queue_fingerprint_matches").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("program_wave_matches").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("status_live_parallel_successor").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("queueChecks").GetProperty("owned_surfaces_exact").GetBoolean());
        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            evidence.GetProperty("queueAllowedPathItems")
                .EnumerateArray()
                .Select(path => path.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "desktop_release_train:avalonia", "flagship_route_truth:desktop" },
            evidence.GetProperty("queueOwnedSurfaceItems")
                .EnumerateArray()
                .Select(surface => surface.GetString())
                .ToArray());
        Assert.IsTrue(designQueueChecks.GetProperty("frontier_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("package_row_count_exactly_one").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("source_registry_path_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("source_queue_fingerprint_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("program_wave_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("status_live_parallel_successor").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("package_complete").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("package_landed_commit_matches").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("owned_surfaces_exact").GetBoolean());
        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            evidence.GetProperty("designQueueAllowedPathItems")
                .EnumerateArray()
                .Select(path => path.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "desktop_release_train:avalonia", "flagship_route_truth:desktop" },
            evidence.GetProperty("designQueueOwnedSurfaceItems")
                .EnumerateArray()
                .Select(surface => surface.GetString())
                .ToArray());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 79760cc1 refreshes the M101 release train receipt after queue closure proof tightening.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit a3bf058e tightens M101 proof commit resolution so stale proof anchors cannot keep the closed package green.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 0954e2a1 pins M101 proof resolution guard into verifier, receipt, and compliance proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit e519ca4b pins the latest M101 proof anchor into the verifier, receipt, and compliance guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit f3e0e90b tightens the M101 blocked-helper proof guard so closed-package evidence cannot cite active-run telemetry or operator helper commands.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit a8944fa5 pins the M101 blocked-helper proof anchor into the verifier, receipt, and compliance guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit b481d3ef refreshes the M101 release train receipt after blocked-helper anchor proof tightening.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 52b118ff pins the latest M101 release train proof anchors.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 24eb3732 tightens the M101 queue source-fingerprint proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 48970414 pins M101 queue fingerprint proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 2ef1a22d pins M101 latest queue proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 8bc1fb02 pins M101 latest queue proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 9629b207 pins M101 current queue proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 6c032e2c pins M101 current queue proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 5c069924 pins M101 current proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 8115735b pins M101 current proof floor guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 0605657d pins M101 811 proof floor guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 53b701e2 pins M101 060 proof floor guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit a0303d5f pins M101 latest release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 0fa3ce01 pins the current M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit b0c0b732 pins M101 current release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 3f99eb0a tightens the M101 blocked-helper proof scan.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit b21ca671 pins M101 blocked-helper scan proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit bb268a79 refreshes the M101 release train proof receipt after canonical successor queue verification.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 7945695d pins the refreshed M101 release train proof receipt into the verifier and compliance guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 9e3d931a pins M101 package identity, allowed scope, owned surfaces, landed commit, and Avalonia independence at the receipt top level.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 492e8f83 records the M101 top-level package-proof floor in the verifier, compliance guard, and generated receipt.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 31cb7cf7 tightens the M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 5a282824 pins the M101 release train verifier, compliance guard, and generated receipt to proof floor 31cb7cf7.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 237e039d pins the M101 active-run proof guard floor so future shards verify the latest completed-package guard instead of repeating it.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 49a5466c pins M101 latest release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 90c0a763 pins the M101 verifier, generated receipt, and compliance guard to proof floor 49a5466c.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 60092e8d pins the M101 release train verifier, generated receipt, and compliance guard to the canonical 90c0a763 proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 5403219b stabilizes the M101 release train receipt timestamp so repeated proof checks do not reopen the completed package.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 871c7f7b pins the M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 8b0e1801 pins the current M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit eae55383 pins the current M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 287c7538 pins the M101 proof floor to the latest completed-package guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit fa67f014 tightens the M101 queue-row uniqueness guard so future shards reject duplicate completed-package rows instead of repeating the closed slice.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit c63379a3 pins M101 queue uniqueness proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 44ac83db pins the M101 queue uniqueness proof floor into the verifier, compliance guard, and generated receipt.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 0c239ada tightens the M101 run-control proof guard so future shards reject worker-unsafe closure citations.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 52086c9d tightens the M101 active-run field proof guard so copied task-local status fields cannot close the completed package.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 82df294e pins the M101 active-run field proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit bb90dca8 tightens M101 verify entrypoint hygiene.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 20487c22 pins M101 verify entrypoint proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit bc01c725 pins the M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 8ac6d072 pins the latest M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 1c7b5819 tightens M101 queue proof commit guard so completed queue proof commit citations must resolve locally inside package scope.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit aa394d32 pins the M101 queue proof commit guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 8db934d3 tightens M101 Avalonia startup-smoke receipt independence proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit cb1fe210 pins M101 receipt independence proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 75b38965 tightens M101 blocked-helper proof source traceability.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit db4fc1e1 tightens M101 worker-context proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 4a4079f5 pins the latest M101 release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 9b97ab1a tightens M101 primary route-truth proof so Avalonia primary evidence cannot smuggle fallback tokens into proof-bearing fields.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit f563293f pins M101 primary route proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit c9f49b5b tightens M101 closed-package proof so future shards verify the completed package instead of reopening the Avalonia primary-route slice.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit f11cff77 tightens M101 authority proof path scope so canonical proof citations cannot drift outside the Avalonia release-train package.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 22380dee tightens M101 authority proof item scope so canonical registry and queue proof/evidence items cannot drift outside the Avalonia release-train package.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 93f7dcea pins the M101 authority proof item guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit de600a43 pins the M101 authority proof guard floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 6dd1064f tightens the M101 primary-route desktop executable proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 0b8414d7 pins M101 current release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit b958e116 tightens M101 standard verify mutation coverage so Avalonia primary route-truth rows cannot cite Blazor fallback proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 46a9f070 tightens M101 standard verify artifact-identity mutation coverage so Avalonia primary route-truth artifact IDs cannot cite Blazor fallback proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit ccc77950 pins M101 artifact mutation proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 4f103b72 pins M101 current release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 84959efa tightens M101 startup receipt fallback proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit c896be32 tightens M101 Avalonia route-truth artifact matching proof.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 9846ce73 pins M101 Avalonia artifact proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit a3917b15 pins M101 current release train proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 1c8aa33c tightens M101 closed queue proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 4779b4c9 tightens M101 encoded worker-context proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit a0decd1a tightens M101 hex-encoded helper proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 9a0a00b6 pins M101 hex helper proof floor.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit c7b4a56f tightens M101 escaped helper proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_/docker/chummercomplete/chummer6-ui-finish commit 58bc9f1b pins M101 escaped helper proof guard.").GetBoolean());
        Assert.IsTrue(designQueueChecks.GetProperty("proof_source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("blockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("encodedBlockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("hexEncodedBlockedActiveRunProofHits").GetArrayLength());
        Assert.AreEqual(0, evidence.GetProperty("escapedBlockedActiveRunProofHits").GetArrayLength());
        JsonElement disallowedProofTokens = evidence.GetProperty("disallowedActiveRunProofTokens");
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "TASK_LOCAL_TELEMETRY.generated.json"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "TASK_LOCAL_TELEMETRY.generated"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "TASK_LOCAL_TELEMETRY"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "ACTIVE_RUN_HANDOFF.generated.md"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "ACTIVE_RUN_HANDOFF.generated"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "ACTIVE_RUN_HANDOFF"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "active_run_handoff"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "/var/lib/codex-fleet/chummer_design_supervisor"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Prompt path:"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Recent stderr tail"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Active Run"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "scripts/ooda_design_supervisor.py"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "scripts/run_ooda_design_supervisor_until_quiet.py"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "run_ooda_design_supervisor"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "supervisor status"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "status helper"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "operator telemetry or active-run helper commands"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "operator telemetry helper"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "operator/OODA"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "operator/OODA loop"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "OODA loop"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Do not query supervisor"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "active-run helper commands"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "active run helper"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "run-state helper"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "worker-safe resume context"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "worker-state helper"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "status_query_supported"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "polling_disabled"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "supervisor telemetry"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "supervisor eta"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Open milestone ids"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Successor frontier ids"));
        Assert.IsTrue(disallowedProofTokens.EnumerateArray().Any(token => token.GetString() == "Successor frontier detail"));
        Assert.AreEqual("case_insensitive", evidence.GetProperty("blockedActiveRunProofScanMode").GetString());
        JsonElement blockedProofSources = evidence.GetProperty("blockedActiveRunProofSources");
        Assert.AreEqual(3, blockedProofSources.GetArrayLength());
        Assert.IsTrue(blockedProofSources.EnumerateArray().Any(source =>
            source.GetProperty("label").GetString() == "registry"
            && source.GetProperty("path").GetString() == "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml"
            && source.GetProperty("marker").GetString() == "id: 101.3"
            && source.GetProperty("present").GetBoolean()));
        Assert.IsTrue(blockedProofSources.EnumerateArray().Any(source =>
            source.GetProperty("label").GetString() == "queue"
            && source.GetProperty("path").GetString() == "/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
            && source.GetProperty("marker").GetString() == "package_id: next90-m101-ui-release-train"
            && source.GetProperty("present").GetBoolean()));
        Assert.IsTrue(blockedProofSources.EnumerateArray().Any(source =>
            source.GetProperty("label").GetString() == "design_queue"
            && source.GetProperty("path").GetString() == "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
            && source.GetProperty("marker").GetString() == "package_id: next90-m101-ui-release-train"
            && source.GetProperty("present").GetBoolean()));
        CollectionAssert.AreEquivalent(
            RequiredPlatforms,
            evidence.GetProperty("expectedRequiredDesktopPlatforms")
                .EnumerateArray()
                .Select(platform => platform.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "avalonia" },
            evidence.GetProperty("expectedRequiredDesktopHeads")
                .EnumerateArray()
                .Select(head => head.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            RequiredPlatforms,
            evidence.GetProperty("requiredDesktopPlatforms")
                .EnumerateArray()
                .Select(platform => platform.GetString())
                .ToArray());
        CollectionAssert.AreEquivalent(
            RequiredPlatformHeadRidTuples,
            evidence.GetProperty("requiredDesktopPlatformHeadRidTuples")
                .EnumerateArray()
                .Select(tuple => tuple.GetString())
                .ToArray());

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
            StringAssert.StartsWith(result.GetProperty("routeTupleId").GetString() ?? string.Empty, $"avalonia:{platform}:");
            StringAssert.StartsWith(result.GetProperty("publicInstallRoute").GetString() ?? string.Empty, "/downloads/install/avalonia-");
            StringAssert.StartsWith(result.GetProperty("startupSmokeReceiptPath").GetString() ?? string.Empty, "/docker/");
            JsonElement independenceChecks = result.GetProperty("startupSmokeReceiptIndependenceChecks");
            Assert.IsTrue(independenceChecks.GetProperty("receipt_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_artifact_id_matches_primary_artifact_when_present").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_primary_artifact_locator_present").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_primary_artifact_locator_names_primary_head").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_process_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_artifact_path_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_file_name_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(independenceChecks.GetProperty("receipt_all_scalar_fields_avoid_fallback_head").GetBoolean(), platform);
            Assert.AreEqual(0, result.GetProperty("startupSmokeReceiptFallbackTokenHits").GetArrayLength(), platform);
            JsonElement primaryRouteTruthIndependenceChecks = result.GetProperty("primaryRouteTruthIndependenceChecks");
            JsonElement primaryRouteTruthRequiredFieldChecks = result.GetProperty("primaryRouteTruthRequiredFieldChecks");
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_artifactId_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_installPostureReason_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_promotionReason_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_publicInstallRoute_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_routeRoleReason_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_tupleId_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthRequiredFieldChecks.GetProperty("primary_route_truth_updateEligibilityReason_present").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_artifactId_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_installPostureReason_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_promotionReason_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_publicInstallRoute_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_routeRoleReason_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_tupleId_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_updateEligibilityReason_avoids_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_all_scalar_fields_avoid_fallback_head").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_proof_fields_distinct_from_fallback_row").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_rollback_state_matches_fallback_promotion_truth").GetBoolean(), platform);
            Assert.IsTrue(primaryRouteTruthIndependenceChecks.GetProperty("primary_route_truth_rollback_reason_matches_fallback_promotion_truth").GetBoolean(), platform);
            Assert.AreEqual(0, result.GetProperty("primaryRouteTruthFallbackTokenHits").GetArrayLength(), platform);
            Assert.AreEqual(0, result.GetProperty("primaryRouteTruthFallbackDistinctFieldHits").GetArrayLength(), platform);
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
