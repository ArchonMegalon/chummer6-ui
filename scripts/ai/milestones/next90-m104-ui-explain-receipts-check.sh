#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M104_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

PACKAGE_ID = "next90-m104-ui-explain-receipts"
MILESTONE_ID = 104
WAVE = "W7"
FRONTIER_ID = 3352869062
EXPECTED_LANDED_COMMIT = "63f57d62"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Blazor",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
]
PROOF_PATH_EXCEPTIONS = [
    ".codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json",
    "scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
]
EXPECTED_SURFACES = [
    "explain_receipts:desktop",
    "diagnostics_diff:desktop",
]
EXPECTED_RESOLVING_PROOF_COMMITS = [
    "b0f5a122",
    "0a2a321f",
    "2c29f1be",
    "1df92955",
    "7556a33b",
    "d9e5392d",
    "d4d34e1c",
    "cea19d0d",
    "f27fefb8",
    "b08d3b2c",
    "0a84aef2",
    "96125b0e",
    "c51f8657",
    "d3dfb527",
    "d18aa133",
    "0da2d157",
    "f494f32f",
    "7ddae55e",
    "9a4a2ae1",
    "cb784e7b",
    "7d5e8e61",
    "06819ea3",
    "208908b7",
    "21ddae58",
    "8c7d639f",
    "d2650d0b",
    "79b8b594",
    "ea689297",
    "5a8e0b2a",
    "bfd66025",
    "f9607bb8",
    "9d302a0e",
    "cb028208",
    "5c19e4e3",
    "c92d8dc4",
    "af590503",
    "f6049a9d",
    "283f8ee3",
    "853c807a",
    "2f69ed4e",
]
RECEIPT_PROOF_LINES = [
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustReceiptText.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/DialogHost.razor",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/SectionPane.razor",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/BlazorShellComponentTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
    'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "BlazorShellComponentTests|AccessibilitySignoffSmokeTests" --no-restore exits 0.',
]
REGISTRY_PROOF_LINES = [
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustReceiptText.cs surfaces import rule-environment receipts and diagnostics before-after diffs on Avalonia import, support, support-case, and report-issue flows.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs and DialogHost.razor surface the same grounded import receipt and before-after environment diff on the Blazor fallback dialog host.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/SectionPane.razor exposes Build Lab blocker receipts and before-after environment diffs in the desktop fallback build surface.",
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json records registry, queue, owned-surface, required-proof, and source-marker pass evidence for the desktop explain receipt slice.",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh fail-closes missing registry authority, queue closure, required proof files, owned-surface markers, import receipts, build blocker receipts, and support diagnostics diffs.",
    "/docker/chummercomplete/chummer6-ui-finish commit b0f5a122 tightens the successor queue proof so the completed M104 package fails closed if allowed_paths or owned_surfaces widen beyond the assigned slice.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0a2a321f tightens M104 explain receipt guard wiring so the compliance guard is part of the completed package proof chain.",
    "/docker/chummercomplete/chummer6-ui-finish commit 2c29f1be tightens M104 explain receipt commit proof so the completed package remains bound to the landed implementation commit.",
    "/docker/chummercomplete/chummer6-ui-finish commit 1df92955 tightens M104 explain receipt frontier guard so the completed package remains bound to successor frontier 3352869062.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7556a33b pins M104 explain receipt proof anchors.",
    "/docker/chummercomplete/chummer6-ui-finish commit d9e5392d wires the M104 explain receipt guard into standard scripts/ai/verify.sh so closed-package proof runs with normal UI verification.",
    "/docker/chummercomplete/chummer6-ui-finish commit d4d34e1c requires the standard-verify wiring commit as registry and queue proof for the completed M104 desktop explain receipt package.",
    "/docker/chummercomplete/chummer6-ui-finish commit cea19d0d tightens M104 explain receipt proof guard so registry, queue, source-marker, and standard-verify proof anchor closure cannot drift.",
    "/docker/chummercomplete/chummer6-ui-finish commit f27fefb8 tightens M104 proof commit resolution so stale commit anchors cannot keep the completed package green.",
    "/docker/chummercomplete/chummer6-ui-finish commit b08d3b2c tightens M104 blocked-helper proof exclusion and commit resolution proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0a84aef2 pins the M104 blocked-helper proof anchor so future shards verify the closed package instead of repeating it.",
    "/docker/chummercomplete/chummer6-ui-finish commit 96125b0e pins the M104 explain receipt proof guard so future shards verify the latest closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit c51f8657 pins the current M104 explain receipt proof guard so future shards verify the latest closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit d3dfb527 tightens M104 explain receipt proof anchor so future shards verify the current closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit d18aa133 pins M104 explain receipt proof anchor so future shards verify the latest closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0da2d157 pins M104 explain receipt latest proof anchor so future shards verify the closed package instead of repeating it.",
    "/docker/chummercomplete/chummer6-ui-finish commit f494f32f tightens M104 explain receipt proof anchor so future shards verify the current closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7ddae55e pins the current M104 explain receipt guard so future shards verify the latest closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9a4a2ae1 pins M104 proof closure to the 7ddae55e guard so future shards verify the current completed-package floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit cb784e7b tightens M104 explain receipt proof floor so future shards verify the current completed-package floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7d5e8e61 pins the current M104 explain receipt proof floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 06819ea3 pins the current M104 explain receipt proof floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 208908b7 pins M104 explain receipt current proof floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 21ddae58 tightens M104 proof commit citation checks so local proof anchors must also be cited by registry or queue proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 8c7d639f tightens M104 canonical queue closure so completed-package proof fails on worker-unsafe citations, queue mirror drift, or widened allowed paths and owned surfaces.",
    "/docker/chummercomplete/chummer6-ui-finish commit d2650d0b pins M104 explain receipt queue closure guard so future shards verify the current completed-package floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 79b8b594 pins M104 explain receipt current proof floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit ea689297 pins M104 explain receipt proof floor so future shards verify the current closed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 5a8e0b2a pins M104 explain receipt guard floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit bfd66025 pins M104 explain receipt current guard floor so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit f9607bb8 tightens M104 generated proof hygiene so receipt proof arrays reject worker-unsafe run-helper citations.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9d302a0e tightens M104 explain receipt proof-path scope so completed-package evidence cannot drift outside assigned UI roots or named proof exceptions.",
    "/docker/chummercomplete/chummer6-ui-finish commit cb028208 pins M104 explain receipt proof scope so future shards verify the latest completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 5c19e4e3 pins M104 explain receipt proof floor so future shards verify the current completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit c92d8dc4 tightens M104 explain receipt proof floor so future shards verify the current completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit af590503 tightens M104 canonical proof-path scope so extra registry or queue proof paths must stay inside the assigned UI roots or named proof exceptions.",
    "/docker/chummercomplete/chummer6-ui-finish commit f6049a9d tightens M104 queue and registry uniqueness proof so duplicate completed-package rows cannot make future shards repeat this slice.",
    "/docker/chummercomplete/chummer6-ui-finish commit 283f8ee3 pins M104 explain receipt uniqueness proof so future shards verify the current completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 853c807a tightens M104 encoded and escaped worker-context proof guards so future shards cannot close the package with disguised worker-unsafe citations.",
    "/docker/chummercomplete/chummer6-ui-finish commit 2f69ed4e tightens M104 explain receipt proof-line uniqueness so duplicate proof entries cannot keep the completed package green.",
    'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "BlazorShellComponentTests|AccessibilitySignoffSmokeTests" --no-restore exits 0.',
    "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh exits 0.",
]
QUEUE_PROOF_LINES = [
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustReceiptText.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/DialogHost.razor",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Blazor/Components/Shell/SectionPane.razor",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/BlazorShellComponentTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish commit b0f5a122 tightens exact allowed_paths and owned_surfaces closure for this completed package.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0a2a321f tightens M104 explain receipt guard wiring for the completed package proof chain.",
    "/docker/chummercomplete/chummer6-ui-finish commit 2c29f1be tightens M104 explain receipt commit proof for the completed package.",
    "/docker/chummercomplete/chummer6-ui-finish commit 1df92955 tightens M104 explain receipt frontier guard for the completed package.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7556a33b pins M104 explain receipt proof anchors.",
    "/docker/chummercomplete/chummer6-ui-finish commit d9e5392d wires the M104 explain receipt guard into standard scripts/ai/verify.sh for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit d4d34e1c requires the standard-verify wiring commit as registry and queue proof for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit cea19d0d tightens M104 explain receipt proof guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit f27fefb8 tightens M104 proof commit resolution for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit b08d3b2c tightens M104 blocked-helper proof exclusion and commit resolution proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0a84aef2 pins the M104 blocked-helper proof anchor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 96125b0e pins the latest M104 explain receipt proof guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit c51f8657 pins the current M104 explain receipt proof guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit d3dfb527 tightens M104 explain receipt proof anchor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit d18aa133 pins M104 explain receipt proof anchor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0da2d157 pins M104 explain receipt latest proof anchor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit f494f32f tightens M104 explain receipt proof anchor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7ddae55e pins the current M104 explain receipt guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9a4a2ae1 pins M104 proof closure to the 7ddae55e guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit cb784e7b tightens M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 7d5e8e61 pins the current M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 06819ea3 pins the current M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 208908b7 pins M104 explain receipt current proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 21ddae58 tightens M104 proof commit citation checks for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 8c7d639f tightens M104 canonical queue closure against worker-unsafe proof, queue mirror drift, and widened package scope.",
    "/docker/chummercomplete/chummer6-ui-finish commit d2650d0b pins M104 explain receipt queue closure guard for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 79b8b594 pins M104 explain receipt current proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit ea689297 pins M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 5a8e0b2a pins M104 explain receipt guard floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit bfd66025 pins M104 explain receipt current guard floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit f9607bb8 tightens M104 generated proof hygiene so receipt proof arrays reject worker-unsafe run-helper citations.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9d302a0e tightens M104 explain receipt proof-path scope for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit cb028208 pins M104 explain receipt proof scope for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit 5c19e4e3 pins M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit c92d8dc4 tightens M104 explain receipt proof floor for repeat prevention.",
    "/docker/chummercomplete/chummer6-ui-finish commit af590503 tightens M104 canonical proof-path scope so extra registry or queue proof paths must stay inside the assigned UI roots or named proof exceptions.",
    "/docker/chummercomplete/chummer6-ui-finish commit f6049a9d tightens M104 queue and registry uniqueness proof so duplicate completed-package rows cannot make future shards repeat this slice.",
    "/docker/chummercomplete/chummer6-ui-finish commit 283f8ee3 pins M104 explain receipt uniqueness proof so future shards verify the current completed-package guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 853c807a tightens M104 encoded and escaped worker-context proof guards so future shards cannot close the package with disguised worker-unsafe citations.",
    "/docker/chummercomplete/chummer6-ui-finish commit 2f69ed4e tightens M104 explain receipt proof-line uniqueness so duplicate proof entries cannot keep the completed package green.",
    "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
    'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "BlazorShellComponentTests|AccessibilitySignoffSmokeTests" --no-restore',
]
DISALLOWED_ACTIVE_RUN_PROOF_TOKENS = [
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
]

SOURCE_MARKERS: dict[str, dict[str, list[str]]] = {
    "Chummer.Avalonia/DesktopTrustReceiptText.cs": {
        "import_rule_environment_receipt": [
            "BuildImportDiffBefore",
            "BuildImportRuleEnvironment",
            "BuildImportDiffAfter",
            "BuildImportSupportReuse",
            "Incoming {receipt.FormatId} payload before workspace merge.",
            "Diff signal:",
            "Support can cite payload",
        ],
        "diagnostics_environment_diff": [
            "Diagnostics environment diff:",
            "BuildDiagnosticsEnvironmentLine",
            "BuildDiagnosticsBeforeLine",
            "BuildDiagnosticsAfterLine",
            "BuildDiagnosticsExplainReceiptLine",
            "BuildDiagnosticsSupportReuseLine",
            "Before:",
            "After:",
            "Explain receipt:",
        ],
    },
    "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs": {
        "avalonia_import_receipt_surface": [
            "Import rule environment:",
            "Import environment before:",
            "Import environment after:",
            "Import explain receipt:",
            "Support reuse:",
        ],
    },
    "Chummer.Avalonia/DesktopSupportWindow.cs": {
        "avalonia_support_diagnostics": [
            "BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection)",
        ],
    },
    "Chummer.Avalonia/DesktopSupportCaseWindow.cs": {
        "avalonia_support_case_diagnostics": [
            "BuildTrackedCaseDiagnostics(_installState, _updateStatus, _supportProjection, _supportCase)",
        ],
    },
    "Chummer.Avalonia/Controls/SectionHostControl.axaml.cs": {
        "avalonia_build_blocker_receipts": [
            "Build blocker receipt:",
            "Rule environment:",
            "Before:",
            "After:",
            "BuildBuildBlockerBefore(buildLab)",
            "BuildBuildBlockerAfter(buildLab)",
            "BuildBuildBlockerSupport(buildLab)",
        ],
    },
    "Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs": {
        "blazor_import_rule_environment_receipt": [
            "BuildImportDiffBefore",
            "BuildImportRuleEnvironment",
            "BuildImportDiffAfter",
            "BuildImportSupportReuse",
            "Incoming {receipt.FormatId} payload before workspace merge.",
            "Diff signal:",
            "Support can cite payload",
        ],
    },
    "Chummer.Blazor/Components/Shell/DialogHost.razor": {
        "blazor_dialog_surface": [
            "data-dialog-explain-receipt",
            "DialogTrustReceiptText.BuildDialogBefore(dialog)",
            "DialogTrustReceiptText.BuildDialogExplainReceipt(dialog)",
        ],
    },
    "Chummer.Blazor/Components/Shell/SectionPane.razor": {
        "blazor_build_blocker_receipts": [
            "data-build-blocker-explain-receipt",
            "<h4>Build blocker receipt</h4>",
            "<div><dt>Rule environment</dt><dd>@BuildBuildBlockerEnvironment(buildLab)</dd></div>",
            "<div><dt>Before</dt><dd>@BuildBuildBlockerBefore(buildLab)</dd></div>",
            "<div><dt>After</dt><dd>@BuildBuildBlockerAfter(buildLab)</dd></div>",
        ],
    },
    "Chummer.Tests/Presentation/BlazorShellComponentTests.cs": {
        "blazor_import_receipt_test": [
            "ImportPanel_renders_ruleset_specific_copy_and_accepts_all_native_formats",
            "chummer.portable-dossier.v1; compatible-with-warnings; inspect-only; payload abcdef1234567890.",
            "Imported Runner Blue into sr4 with a bounded source toggle change.",
            "Review the before-after environment diff before campaign handoff.",
            "Support can cite payload abcdef1234567890 with compatible-with-warnings compatibility.",
            "Street Magic source toggle changed during import.",
        ],
        "blazor_build_receipt_test": [
            "Build blocker receipt",
            "Rule environment",
            "One quick-action binding still needs review.",
            "[data-build-blocker-explain-receipt]",
        ],
    },
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": {
        "targeted_signoff": [
            "BuildImportRuleEnvironment(activity.Receipt)",
            "Import environment before:",
            "Build blocker receipt:",
            "BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection)",
            "BuildBuildBlockerBefore(buildLab)",
        ],
    },
    "Chummer.Tests/Compliance/Next90M104ExplainReceiptsGuardTests.cs": {
        "m104_guard_self_closure": [
            "M104_explain_receipts_guard_fail_closes_missing_completed_queue_proof",
            "M104_explain_receipts_receipt_proves_desktop_trust_surfaces_are_closed_in_repo_local_state",
            "M104_explain_receipts_guard_is_wired_into_compliance_test_project",
            "M104_explain_receipts_guard_is_wired_into_standard_ai_verify",
            "Next90M104ExplainReceiptsGuardTests.cs",
            "commit 0a2a321f tightens M104 explain receipt guard wiring",
            "commit 2c29f1be tightens M104 explain receipt commit proof",
            "commit 1df92955 tightens M104 explain receipt frontier guard",
            "commit 7556a33b pins M104 explain receipt proof anchors",
            "commit d9e5392d wires the M104 explain receipt guard into standard scripts/ai/verify.sh",
            "commit d4d34e1c requires the standard-verify wiring commit as registry and queue proof",
            "commit cea19d0d tightens M104 explain receipt proof guard",
            "commit f27fefb8 tightens M104 proof commit resolution",
            "commit b08d3b2c tightens M104 blocked-helper proof exclusion",
            "commit 0a84aef2 pins the M104 blocked-helper proof anchor",
            "commit 96125b0e pins the M104 explain receipt proof guard",
            "commit c51f8657 pins the current M104 explain receipt proof guard",
            "commit d3dfb527 tightens M104 explain receipt proof anchor",
            "commit d18aa133 pins M104 explain receipt proof anchor",
            "commit 0da2d157 pins M104 explain receipt latest proof anchor",
            "commit f494f32f tightens M104 explain receipt proof anchor",
            "commit 7ddae55e pins the current M104 explain receipt guard",
            "commit 9a4a2ae1 pins M104 proof closure to the 7ddae55e guard",
            "commit cb784e7b tightens M104 explain receipt proof floor",
            "commit 7d5e8e61 pins the current M104 explain receipt proof floor",
            "commit 06819ea3 pins the current M104 explain receipt proof floor",
            "commit 208908b7 pins M104 explain receipt current proof floor",
            "commit 21ddae58 tightens M104 proof commit citation checks",
            "commit 8c7d639f tightens M104 canonical queue closure",
            "commit d2650d0b pins M104 explain receipt queue closure guard",
            "commit 79b8b594 pins M104 explain receipt current proof floor",
            "commit ea689297 pins M104 explain receipt proof floor",
            "commit 5a8e0b2a pins M104 explain receipt guard floor",
            "commit bfd66025 pins M104 explain receipt current guard floor",
            "commit f9607bb8 tightens M104 generated proof hygiene",
            "commit 9d302a0e tightens M104 explain receipt proof-path scope",
            "commit cb028208 pins M104 explain receipt proof scope",
            "commit 5c19e4e3 pins M104 explain receipt proof floor",
            "commit c92d8dc4 tightens M104 explain receipt proof floor",
            "commit af590503 tightens M104 canonical proof-path scope",
            "commit f6049a9d tightens M104 queue and registry uniqueness proof",
            "commit 283f8ee3 pins M104 explain receipt uniqueness proof",
            "commit 853c807a tightens M104 encoded and escaped worker-context proof guards",
            "commit 2f69ed4e tightens M104 explain receipt proof-line uniqueness",
            "canonical_block_proof_path_scope_checks",
            "canonicalProofPathScopeChecks",
            "all_canonical_block_paths_allowed",
            "canonical proof path scope check failed",
            "operatorHelperProofChecks",
            "required_proof_avoids_active_run_helpers",
            "registry_evidence_avoids_active_run_helpers",
            "queue_evidence_avoids_active_run_helpers",
            "design_queue_evidence_avoids_active_run_helpers",
            "VEFTS19MT0NBTF9URUxFTUVUUlkuZ2VuZXJhdGVkLmpzb24=",
            "QUNUSVZFX1JVTl9IQU5ET0ZGLmdlbmVyYXRlZC5tZA==",
            "5441534b5f4c4f43414c5f54454c454d455452592e67656e6572617465642e6a736f6e",
            "4143544956455f52554e5f48414e444f46462e67656e6572617465642e6d64",
            "TASK&#95;LOCAL&#95;TELEMETRY.generated.json",
            "ACTIVE&#95;RUN&#95;HANDOFF.generated.md",
        ],
    },
    "scripts/ai/verify.sh": {
        "m104_standard_verify_wiring": [
            "checking next-90 M104 desktop explain receipt guard",
            "bash scripts/ai/milestones/next90-m104-ui-explain-receipts-check.sh",
        ],
    },
    "Chummer.Tests/Chummer.Tests.csproj": {
        "m104_guard_project_wiring": [
            "Compliance\\Next90M104ExplainReceiptsGuardTests.cs",
        ],
    },
}


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def read_text(path: Path, reasons: list[str], label: str) -> str:
    if not path.is_file():
        reasons.append(f"{label} is missing: {path}")
        return ""
    return path.read_text(encoding="utf-8-sig", errors="replace")


def line_number(text: str, marker: str) -> int | None:
    index = text.find(marker)
    if index < 0:
        return None
    return text.count("\n", 0, index) + 1


def block_after_marker(text: str, marker: str, next_marker: str) -> str:
    index = text.find(marker)
    if index < 0:
        return ""
    start = text.rfind("\n  - ", 0, index)
    if start < 0:
        start = index
    end = text.find(next_marker, index + len(marker))
    if end < 0:
        end = len(text)
    return text[start:end]


def queue_item_block(queue_text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    index = queue_text.find(marker)
    if index < 0:
        return ""
    start = queue_text.rfind("\n  - ", 0, index)
    if start < 0:
        start = 0
    end = queue_text.find("\n  - ", index)
    if end < 0:
        end = len(queue_text)
    return queue_text[start:end]


def package_occurrence_count(text: str) -> int:
    return text.count(f"package_id: {PACKAGE_ID}")


def queue_checks_for(block: str) -> dict[str, bool]:
    checks = {
        "package_present": bool(block),
        "repo_matches": "repo: chummer6-ui" in block,
        "frontier_matches": f"frontier_id: {FRONTIER_ID}" in block,
        "milestone_matches": "milestone_id: 104" in block,
        "wave_matches": "wave: W7" in block,
        "title_matches": "title: Surface explain receipts and environment diffs where users need trust" in block,
        "task_matches": "task: Expose grounded explain receipts and before-after environment diffs on import, build blockers, and support diagnostics." in block,
        "status_complete": "status: complete" in block,
        "landed_commit_matches": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in block,
    }
    checks.update({f"allowed_path_{path}": f"- {path}" in block for path in EXPECTED_ALLOWED_PATHS})
    checks.update({f"owned_surface_{surface}": f"- {surface}" in block for surface in EXPECTED_SURFACES})
    checks.update({f"proof_{index}": f"- {proof}" in block for index, proof in enumerate(QUEUE_PROOF_LINES, start=1)})
    checks["allowed_paths_exact"] = yaml_list_after(block, "allowed_paths") == EXPECTED_ALLOWED_PATHS
    checks["owned_surfaces_exact"] = yaml_list_after(block, "owned_surfaces") == EXPECTED_SURFACES
    return checks


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    index = block.find(marker)
    if index < 0:
        return []
    result: list[str] = []
    for raw_line in block[index + len(marker):].splitlines():
        if not raw_line.startswith("      - "):
            if result:
                break
            if raw_line and not raw_line.startswith(" "):
                break
            continue
        result.append(raw_line.removeprefix("      - ").strip())
    return result


def registry_task_block(registry_text: str) -> str:
    marker = "id: 104.3\n        owner: chummer6-ui"
    index = registry_text.find(marker)
    if index < 0:
        return ""
    start = registry_text.rfind("\n      - ", 0, index)
    if start < 0:
        start = index
    end = registry_text.find("\n      - ", index + len(marker))
    if end < 0:
        end = len(registry_text)
    return registry_text[start:end]


def comparable_receipt(payload: dict[str, Any]) -> dict[str, Any]:
    comparable = dict(payload)
    comparable.pop("generatedAt", None)
    return comparable


def git_object_exists(repo: Path, revision: str) -> bool:
    try:
        subprocess.run(
            ["git", "cat-file", "-e", f"{revision}^{{commit}}"],
            cwd=repo,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
    except (OSError, subprocess.CalledProcessError):
        return False
    return True


def disallowed_active_run_proof_tokens(text: str) -> list[str]:
    lowered = text.lower()
    return [token for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS if token.lower() in lowered]


def duplicate_items(items: list[str]) -> list[str]:
    seen: set[str] = set()
    duplicates: list[str] = []
    for item in items:
        if item in seen and item not in duplicates:
            duplicates.append(item)
        seen.add(item)
    return duplicates


def repo_local_proof_path(line: str) -> str | None:
    prefix = f"{repo_root}/"
    line = line.strip()
    if line.startswith("- "):
        line = line[2:].strip()
    if not line.startswith(prefix):
        return None
    return line.removeprefix(prefix).split(maxsplit=1)[0].rstrip(".,")


def proof_path_scope_checks(proof_lines: list[str]) -> dict[str, Any]:
    scoped_paths: dict[str, bool] = {}
    for proof_line in proof_lines:
        relative_path = repo_local_proof_path(proof_line)
        if relative_path is None:
            continue
        scoped_paths[relative_path] = (
            any(relative_path == allowed or relative_path.startswith(f"{allowed}/") for allowed in EXPECTED_ALLOWED_PATHS)
            or relative_path in PROOF_PATH_EXCEPTIONS
        )
    return {
        "scoped_paths": scoped_paths,
        "all_scoped_paths_allowed": all(scoped_paths.values()),
        "proof_path_exceptions": PROOF_PATH_EXCEPTIONS,
    }


def canonical_block_proof_path_scope_checks(blocks: dict[str, str]) -> dict[str, Any]:
    block_results: dict[str, dict[str, bool]] = {}
    for block_name, block in blocks.items():
        block_results[block_name] = proof_path_scope_checks(block.splitlines())["scoped_paths"]

    return {
        "blocks": block_results,
        "all_canonical_block_paths_allowed": all(
            allowed
            for block_result in block_results.values()
            for allowed in block_result.values()
        ),
    }


reasons: list[str] = []
registry_text = read_text(registry_path, reasons, "successor registry")
queue_text = read_text(queue_path, reasons, "successor queue")
design_queue_text = read_text(design_queue_path, reasons, "design successor queue")
registry_task = registry_task_block(registry_text)
queue_block = queue_item_block(queue_text)
design_queue_block = queue_item_block(design_queue_text)
milestone_block = block_after_marker(registry_text, "id: 104\n", "\n  - id: 105")

registry_checks = {
    "milestone_104_present": "id: 104\n    title: Engine proof pack, explain budgets, and import-oracle discipline" in registry_text,
    "milestone_104_in_progress": "status: in_progress" in milestone_block,
    "depends_103": "- 103" in milestone_block,
    "ui_work_task_present": "id: 104.3\n        owner: chummer6-ui\n        title: Surface explain receipts and environment diffs on import, build blockers, and support diagnostics." in registry_text,
    "ui_work_task_unique": registry_text.count("id: 104.3\n        owner: chummer6-ui") == 1,
    "ui_work_task_complete": "status: complete" in registry_task,
    "ui_work_task_landed_commit": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in registry_task,
    "wave_w7": "id: W7" in registry_text and "- 104" in block_after_marker(registry_text, "id: W7", "\n  - id: W8"),
}
registry_checks.update({f"ui_work_task_evidence_{index}": proof in registry_task for index, proof in enumerate(REGISTRY_PROOF_LINES, start=1)})

queue_checks = queue_checks_for(queue_block)
design_queue_checks = queue_checks_for(design_queue_block)
queue_mirror_checks = {
    "fleet_queue_points_to_design_queue": f"source_design_queue_path: {design_queue_path}" in queue_text,
    "package_blocks_match": queue_block.strip() == design_queue_block.strip(),
    "fleet_queue_package_unique": package_occurrence_count(queue_text) == 1,
    "design_queue_package_unique": package_occurrence_count(design_queue_text) == 1,
}
operator_helper_proof_checks = {
    "required_proof_avoids_active_run_helpers": not disallowed_active_run_proof_tokens("\n".join(RECEIPT_PROOF_LINES + REGISTRY_PROOF_LINES + QUEUE_PROOF_LINES)),
    "registry_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(registry_task),
    "queue_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(queue_block),
    "design_queue_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(design_queue_block),
}
proof_uniqueness_checks = {
    "required_proof_lines_unique": not duplicate_items(RECEIPT_PROOF_LINES),
    "registry_proof_lines_unique": not duplicate_items(REGISTRY_PROOF_LINES),
    "queue_proof_lines_unique": not duplicate_items(QUEUE_PROOF_LINES),
    "required_proof_duplicate_lines": duplicate_items(RECEIPT_PROOF_LINES),
    "registry_proof_duplicate_lines": duplicate_items(REGISTRY_PROOF_LINES),
    "queue_proof_duplicate_lines": duplicate_items(QUEUE_PROOF_LINES),
}
local_repo_checks = {
    "landed_commit_resolves": git_object_exists(repo_root, EXPECTED_LANDED_COMMIT),
    "landed_commit_cited_canonically": False,
    "resolving_proof_commits": {},
    "proof_commits_have_canonical_citations": {},
}
proof_scope_checks = proof_path_scope_checks(RECEIPT_PROOF_LINES + REGISTRY_PROOF_LINES + QUEUE_PROOF_LINES)
canonical_proof_scope_checks = canonical_block_proof_path_scope_checks(
    {
        "registry_task": registry_task,
        "fleet_queue": queue_block,
        "design_queue": design_queue_block,
    }
)
canonical_proof_text = "\n".join([registry_task, queue_block, design_queue_block])
local_repo_checks["landed_commit_cited_canonically"] = f"landed_commit: {EXPECTED_LANDED_COMMIT}" in canonical_proof_text
if not local_repo_checks["landed_commit_cited_canonically"]:
    reasons.append("local repo check failed: landed_commit_cited_canonically")
for commit in EXPECTED_RESOLVING_PROOF_COMMITS:
    commit_resolves = git_object_exists(repo_root, commit)
    local_repo_checks["resolving_proof_commits"][commit] = commit_resolves
    commit_cited = f"commit {commit}" in canonical_proof_text
    local_repo_checks["proof_commits_have_canonical_citations"][commit] = commit_cited
    if not commit_cited:
        reasons.append(f"local repo check failed: proof commit {commit} is not cited by registry, Fleet queue, or design queue proof")
local_repo_checks["all_proof_commits_resolve"] = all(local_repo_checks["resolving_proof_commits"].values())
local_repo_checks["all_proof_commits_have_canonical_citations"] = all(local_repo_checks["proof_commits_have_canonical_citations"].values())

for check_name, passed in registry_checks.items():
    if not passed:
        reasons.append(f"registry check failed: {check_name}")
for check_name, passed in queue_checks.items():
    if not passed:
        reasons.append(f"queue check failed: {check_name}")
for check_name, passed in design_queue_checks.items():
    if not passed:
        reasons.append(f"design queue check failed: {check_name}")
for check_name, passed in queue_mirror_checks.items():
    if not passed:
        reasons.append(f"queue mirror check failed: {check_name}")
for check_name, passed in operator_helper_proof_checks.items():
    if not passed:
        reasons.append(f"operator helper proof check failed: {check_name}")
for check_name in [
    "required_proof_lines_unique",
    "registry_proof_lines_unique",
    "queue_proof_lines_unique",
]:
    if not proof_uniqueness_checks[check_name]:
        reasons.append(f"proof uniqueness check failed: {check_name}")
for check_name in [
    "landed_commit_cited_canonically",
    "all_proof_commits_have_canonical_citations",
]:
    if not local_repo_checks[check_name]:
        reasons.append(f"local repo check failed: {check_name}")
for relative_path, passed in proof_scope_checks["scoped_paths"].items():
    if not passed:
        reasons.append(f"proof path scope check failed: {relative_path} is outside M104 allowed paths and proof exceptions")
if not proof_scope_checks["all_scoped_paths_allowed"]:
    reasons.append("proof path scope check failed: all_scoped_paths_allowed")
for block_name, scoped_paths in canonical_proof_scope_checks["blocks"].items():
    for relative_path, passed in scoped_paths.items():
        if not passed:
            reasons.append(f"canonical proof path scope check failed: {block_name}:{relative_path} is outside M104 allowed paths and proof exceptions")
if not canonical_proof_scope_checks["all_canonical_block_paths_allowed"]:
    reasons.append("canonical proof path scope check failed: all_canonical_block_paths_allowed")

source_results: dict[str, Any] = {}
for relative_path, groups in SOURCE_MARKERS.items():
    text = read_text(repo_root / relative_path, reasons, relative_path)
    file_results: dict[str, Any] = {}
    for group_name, markers in groups.items():
        marker_results = [
            {
                "marker": marker,
                "present": marker in text,
                "line": line_number(text, marker),
            }
            for marker in markers
        ]
        missing = [result["marker"] for result in marker_results if not result["present"]]
        if missing:
            reasons.append(f"{relative_path}:{group_name} missing markers: {', '.join(missing)}")
        file_results[group_name] = {
            "status": "pass" if not missing else "fail",
            "missingMarkers": missing,
            "markers": marker_results,
        }
    source_results[relative_path] = file_results

registry_review_reasons = [check_name for check_name, passed in registry_checks.items() if not passed]
queue_review_reasons = (
    [f"fleet:{check_name}" for check_name, passed in queue_checks.items() if not passed]
    + [f"design:{check_name}" for check_name, passed in design_queue_checks.items() if not passed]
    + [f"mirror:{check_name}" for check_name, passed in queue_mirror_checks.items() if not passed]
)
proof_hygiene_review_reasons = (
    [f"operator_helper:{check_name}" for check_name, passed in operator_helper_proof_checks.items() if not passed]
    + [
        f"proof_uniqueness:{check_name}"
        for check_name in ["required_proof_lines_unique", "registry_proof_lines_unique", "queue_proof_lines_unique"]
        if not proof_uniqueness_checks[check_name]
    ]
    + [
        f"proof_scope:{relative_path}"
        for relative_path, passed in proof_scope_checks["scoped_paths"].items()
        if not passed
    ]
    + (
        ["proof_scope:all_scoped_paths_allowed"]
        if not proof_scope_checks["all_scoped_paths_allowed"]
        else []
    )
    + [
        f"canonical_scope:{block_name}:{relative_path}"
        for block_name, scoped_paths in canonical_proof_scope_checks["blocks"].items()
        for relative_path, passed in scoped_paths.items()
        if not passed
    ]
    + (
        ["canonical_scope:all_canonical_block_paths_allowed"]
        if not canonical_proof_scope_checks["all_canonical_block_paths_allowed"]
        else []
    )
)
local_repo_review_reasons = (
    (
        ["landed_commit_cited_canonically"]
        if not local_repo_checks["landed_commit_cited_canonically"]
        else []
    )
    + (
        ["all_proof_commits_have_canonical_citations"]
        if not local_repo_checks["all_proof_commits_have_canonical_citations"]
        else []
    )
    + [
        f"proof_commit_citation:{commit}"
        for commit, cited in local_repo_checks["proof_commits_have_canonical_citations"].items()
        if not cited
    ]
)
source_marker_review_reasons = [
    f"{relative_path}:{group_name}"
    for relative_path, file_results in source_results.items()
    for group_name, group_result in file_results.items()
    if str(group_result.get("status") or "").strip().lower() != "pass"
]
reviews = {
    "registryClosureReview": {
        "status": "pass" if not registry_review_reasons else "fail",
        "reasons": registry_review_reasons,
    },
    "queueClosureReview": {
        "status": "pass" if not queue_review_reasons else "fail",
        "reasons": queue_review_reasons,
    },
    "proofHygieneReview": {
        "status": "pass" if not proof_hygiene_review_reasons else "fail",
        "reasons": proof_hygiene_review_reasons,
    },
    "localRepoCitationReview": {
        "status": "pass" if not local_repo_review_reasons else "fail",
        "reasons": local_repo_review_reasons,
    },
    "sourceMarkerReview": {
        "status": "pass" if not source_marker_review_reasons else "fail",
        "reasons": source_marker_review_reasons,
    },
}

payload: dict[str, Any] = {
    "contract_name": "chummer6-ui.next90_m104_ui_explain_receipts",
    "generatedAt": now_iso(),
    "status": "pass" if not reasons else "fail",
    "evidence": {
        "packageId": PACKAGE_ID,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "wave": WAVE,
        "landedCommit": EXPECTED_LANDED_COMMIT,
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "registryPath": str(registry_path),
        "queuePath": str(queue_path),
        "designQueuePath": str(design_queue_path),
        "receiptPath": str(receipt_path),
        "requiredProof": RECEIPT_PROOF_LINES,
        "registryProof": REGISTRY_PROOF_LINES,
        "queueProof": QUEUE_PROOF_LINES,
        "registryChecks": registry_checks,
        "queueChecks": queue_checks,
        "designQueueChecks": design_queue_checks,
        "queueMirrorChecks": queue_mirror_checks,
        "operatorHelperProofChecks": operator_helper_proof_checks,
        "proofUniquenessChecks": proof_uniqueness_checks,
        "disallowedActiveRunProofTokens": DISALLOWED_ACTIVE_RUN_PROOF_TOKENS,
        "localRepoChecks": local_repo_checks,
        "proofPathScopeChecks": proof_scope_checks,
        "canonicalProofPathScopeChecks": canonical_proof_scope_checks,
        "sourceResults": source_results,
        "failureCount": len(reasons),
    },
    "reviews": reviews,
    "unresolved": reasons,
}

previous_payload: dict[str, Any] | None = None
if receipt_path.is_file():
    try:
        previous_payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        previous_payload = None

if previous_payload is None or comparable_receipt(previous_payload) != comparable_receipt(payload):
    receipt_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if reasons:
    for reason in reasons:
        print(f"[M104] FAIL: {reason}", file=sys.stderr)
    sys.exit(1)

print(f"[M104] PASS: {PACKAGE_ID} registry, queue, and desktop explain receipt proof are closed.")
PY
