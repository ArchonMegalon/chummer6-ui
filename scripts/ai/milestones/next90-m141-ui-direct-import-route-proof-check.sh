#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M141_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json}"
flagship_frontier_root="${CHUMMER_FLAGSHIP_FRONTIER_ROOT:-/docker/fleet/.codex-studio/published/full-product-frontiers}"
flagship_frontier_id="${CHUMMER_FLAGSHIP_FRONTIER_ID:-1922169755}"
default_flagship_frontier_path="$(
  python3 - <<'PY' "$flagship_frontier_root" "$flagship_frontier_id"
from __future__ import annotations

import sys
from pathlib import Path

frontier_root = Path(sys.argv[1])
frontier_id = str(sys.argv[2]).strip()
preferred = frontier_root / "shard-1.generated.yaml"
if preferred.is_file():
    preferred_text = preferred.read_text(encoding="utf-8")
    if frontier_id and frontier_id in preferred_text:
        print(preferred)
        raise SystemExit(0)

candidates = sorted(frontier_root.glob("shard-*.generated.yaml"))
for candidate in candidates:
    try:
        candidate_text = candidate.read_text(encoding="utf-8")
    except OSError:
        continue
    if frontier_id and frontier_id in candidate_text:
        print(candidate)
        raise SystemExit(0)

if preferred.is_file():
    print(preferred)
elif candidates:
    print(candidates[0])
else:
    print(preferred)
PY
)"
if [[ -z "$default_flagship_frontier_path" ]]; then
  default_flagship_frontier_path="$(
    python3 - <<'PY' "$flagship_frontier_root"
from __future__ import annotations

import sys
from pathlib import Path

frontier_root = Path(sys.argv[1])
candidates = sorted(frontier_root.glob("shard-*.generated.yaml"))
print(candidates[0] if candidates else frontier_root / "shard-1.generated.yaml")
PY
  )"
fi
flagship_frontier_path="${CHUMMER_FLAGSHIP_FRONTIER_PATH:-$default_flagship_frontier_path}"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
if [[ -f "$verified_release_channel_path" && ( ! -f "$release_channel_path_default" || "$verified_release_channel_path" -nt "$release_channel_path_default" ) ]]; then
  release_channel_path_default="$verified_release_channel_path"
fi
release_channel_path="${CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" "$release_channel_path" "$flagship_frontier_path" "$flagship_frontier_root" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])
release_channel_path = Path(sys.argv[6])
flagship_frontier_path = Path(sys.argv[7])
flagship_frontier_root = Path(sys.argv[8])

PACKAGE_ID = "next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment"
TITLE = "Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."
TASK = TITLE
FRONTIER_ID = 2354698282
MILESTONE_ID = 141
WORK_TASK_ID = "141.1"
WAVE = "W22P"
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M141 chummer6-ui translator, XML amendment, and Hero Lab direct route proof is complete; future shards must verify the closed-package receipt, focused guard test, runtime-backed screenshot gates, canonical registry row, and queue mirrors instead of reopening this slice."
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "capture_direct_screenshot_and_runtime_proof_for_translat:ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_SCREENSHOTS = [
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
]
EXPECTED_REVIEW_JOBS = [
    "translator_xml_custom_data",
    "hero_lab_import_oracle",
]
EXPECTED_ROUTE_RECEIPTS = {
    "translator_xml_custom_data": {
        "routeIds": [
            "translator",
            "xml_editor",
            "source:translator_route",
            "source:xml_amendment_editor_route",
            "family:custom_data_xml_and_translator_bridge",
        ],
        "workflowFamilyId": "improvements-explain-result-parity",
        "screenshots": [
            "38-translator-dialog-light.png",
            "39-xml-editor-dialog-light.png",
        ],
    },
    "hero_lab_import_oracle": {
        "routeIds": [
            "hero_lab_importer",
            "source:hero_lab_importer_route",
            "family:legacy_and_adjacent_import_oracles",
        ],
        "workflowFamilyId": "create-open-import-save-save-as-print-export",
        "screenshots": [
            "40-hero-lab-importer-dialog-light.png",
        ],
    },
}
FLAGSHIP_FRONTIER_ID = 1922169755
FLAGSHIP_FRONTIER_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]

REGISTRY_MARKERS = [
    "title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes",
    "source:translator_route",
    "source:xml_amendment_editor_route",
    "source:hero_lab_importer_route",
    "family:custom_data_xml_and_translator_bridge",
    "family:legacy_and_adjacent_import_oracles",
    "Direct screenshot-backed and runtime-backed receipts exist for `menu:translator`, `menu:xml_editor`, `menu:hero_lab_importer`,",
    "id: '141.1'",
    "owner: chummer6-ui",
    TITLE,
]

SOURCE_MARKERS = {
    "Chummer.Presentation/Overview/OverviewCommandDispatcher.cs": [
        'if (string.Equals(commandId, "translator", StringComparison.Ordinal))',
        '|| string.Equals(commandId, "translator", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "xml_editor", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "hero_lab_importer", StringComparison.Ordinal)',
    ],
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs": [
        '"dialog.translator"',
        '"translatorLanePosture"',
        '"dialog.xml_editor"',
        '"xmlEditorLanePosture"',
        '"dialog.hero_lab_importer"',
        '"heroLabImportOracleLanePosture"',
        '"heroLabAdjacentSr6OracleReceipt"',
    ],
    "Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs": [
        'Command("translator", "command.translator", "tools", false)',
        'Command("xml_editor", "command.xml_editor", "tools", false)',
        'Command("hero_lab_importer", "command.hero_lab_importer", "tools", false)',
    ],
    "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs": [
        "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
        "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
        "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
    ],
    "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs": [
        "CreateCommandDialog_translator_prefers_catalog_languages_and_surfaces_lane_posture",
        "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        "CreateCommandDialog_hero_lab_importer_surfaces_import_oracle_and_adjacent_sr6_posture",
    ],
    "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs": [
        "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        "Avalonia_and_Blazor_hero_lab_importer_dialog_preserves_matching_import_oracle_posture",
    ],
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
        "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
        'GetVeteranCertificationReviewStep("translator").ScreenshotFileName',
        'GetVeteranCertificationReviewStep("xml_editor").ScreenshotFileName',
        'GetVeteranCertificationReviewStep("hero_lab_importer").ScreenshotFileName',
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh": [
        '"translator_xml_custom_data"',
        '"hero_lab_import_oracle"',
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M141 direct import-route proof guard",
        "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
    ],
}

DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "operator telemetry",
    "supervisor status",
]
EXPECTED_PROOF = [
    f"{repo_root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs",
    f"{repo_root}/Chummer.Tests/Chummer.Tests.csproj",
    f"{repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
    f"{repo_root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
    f"{repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    f"{repo_root}/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
    f"{repo_root}/scripts/ai/verify.sh",
    f"{repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json",
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
]
EXPECTED_REGISTRY_EVIDENCE = [
    f"{repo_root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs, {repo_root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs, {repo_root}/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs, and {repo_root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs keep the translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle flows bound to direct screenshot-backed and runtime-backed desktop route proof instead of broad family prose.",
    f"{repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, {repo_root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh, {repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh, and {repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json keep the direct screenshot pack, runtime-backed route receipts, and published closure proof aligned for translator, XML amendment, Hero Lab importer, and adjacent import-oracle coverage.",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs, {repo_root}/Chummer.Tests/Chummer.Tests.csproj, {repo_root}/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh, and {repo_root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, verify wiring, or worker-safe flagship frontier evidence drift from the completed package contract.",
    f"{repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json records the closed-package receipt for `next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment`.",
]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return payload


def status_pass(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def block_for_package(text: str, package_id: str) -> str:
    marker = f"package_id: {package_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing package row for {package_id}")
    block_start = text.rfind("\n- title:", 0, start)
    if block_start == -1:
        block_start = text.rfind("\n  - title:", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n- title:", start)
    if next_start == -1:
        next_start = text.find("\n  - title:", start)
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def block_for_work_task(text: str, task_id: str) -> str:
    marker = f"- id: '{task_id}'"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing work task row for {task_id}")
    block_start = text.rfind("\n", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n    - id:", start + len(marker))
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def block_for_milestone(text: str, milestone_id: int) -> str:
    marker = f"  - id: {milestone_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing milestone row for {milestone_id}")
    next_start = text.find("\n  - id:", start + len(marker))
    return text[start:] if next_start == -1 else text[start:next_start]


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    start = block.find(marker)
    if start == -1:
        raise AssertionError(f"missing {key}")
    items: list[str] = []
    for line in block[start + len(marker):].splitlines():
        if line.startswith("  - "):
            items.append(line.removeprefix("  - ").strip())
            continue
        if line.startswith("      - "):
            items.append(line.removeprefix("      - ").strip())
            continue
        if line.startswith("        - "):
            items.append(line.removeprefix("        - ").strip())
            continue
        if items:
            if line.startswith("        ") and not line.strip().endswith(":"):
                items[-1] = f"{items[-1]} {line.strip()}"
                continue
            break
        if line.strip():
            break
    return items


def yaml_scalar(block: str, key: str) -> str:
    marker = f"{key}:"
    for line in block.splitlines():
        stripped = line.strip()
        if stripped.startswith(marker):
            return stripped.removeprefix(marker).strip().strip("'\"")
    raise AssertionError(f"missing {key}")


def yaml_wrapped_scalar(block: str, key: str) -> str:
    marker = f"{key}:"
    lines = block.splitlines()
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith(f"- {marker}"):
            stripped = stripped.removeprefix("- ").strip()
        if not stripped.startswith(marker):
            continue

        first = stripped.removeprefix(marker).strip()
        values = [first] if first else []
        base_indent = len(line) - len(line.lstrip(" "))
        for continuation in lines[index + 1:]:
            continuation_indent = len(continuation) - len(continuation.lstrip(" "))
            if continuation_indent <= base_indent:
                break
            continuation_text = continuation.strip()
            if continuation_text.startswith("- ") or continuation_text.startswith("title:") or continuation_text.startswith("task:"):
                break
            values.append(continuation_text)

        return " ".join(value for value in values if value).strip().strip("'\"")

    raise AssertionError(f"missing {key}")


def read_string_list(value: Any) -> list[str]:
    if not isinstance(value, list):
        return []
    return [str(item).strip() for item in value if str(item).strip()]


def normalize_space(value: str) -> str:
    return " ".join(value.split())


CANONICAL_REPO_ROOT_ALIASES = [
    repo_root,
    repo_root.parent / "chummer6-ui",
]


def normalize_repo_root_aliases(value: str) -> str:
    normalized = str(value)
    for alias in CANONICAL_REPO_ROOT_ALIASES:
        normalized = normalized.replace(str(alias), str(repo_root))
    return normalize_space(normalized)


def normalized_string_list(values: list[str]) -> list[str]:
    return [normalize_repo_root_aliases(value) for value in values]


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
registry_milestone_block = block_for_milestone(registry_text, MILESTONE_ID)
flagship_frontier_text = read_text(flagship_frontier_path) if flagship_frontier_path.is_file() else ""

visual_gate_path = repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
screenshot_review_gate_path = repo_root / ".codex-studio" / "published" / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
veteran_task_gate_path = repo_root / ".codex-studio" / "published" / "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
ui_flagship_gate_path = repo_root / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"

visual_gate = load_json(visual_gate_path)
screenshot_review_gate = load_json(screenshot_review_gate_path)
veteran_task_gate = load_json(veteran_task_gate_path)
ui_flagship_gate = load_json(ui_flagship_gate_path)
ui_flagship_gate_text = read_text(ui_flagship_gate_path)
release_channel = load_json(release_channel_path)
release_channel_channel_id = str(release_channel.get("channelId") or release_channel.get("channel") or "").strip()
release_channel_version = str(release_channel.get("version") or "").strip()
ui_flagship_gate_direct_import_route_proof = ui_flagship_gate.get("directImportRouteProof") or {}
if not isinstance(ui_flagship_gate_direct_import_route_proof, dict):
    ui_flagship_gate_direct_import_route_proof = {}
ui_flagship_gate_blocking_findings = read_string_list(ui_flagship_gate.get("blockingFindings"))
ui_flagship_gate_review_jobs = set(read_string_list(ui_flagship_gate_direct_import_route_proof.get("reviewJobs")))
ui_flagship_gate_screenshots = set(read_string_list(ui_flagship_gate_direct_import_route_proof.get("screenshots")))
ui_flagship_gate_presenter_tests = set(
    read_string_list(ui_flagship_gate_direct_import_route_proof.get("characterOverviewPresenterTests"))
)
ui_flagship_gate_route_local_only = (
    bool(ui_flagship_gate_direct_import_route_proof)
    and bool(ui_flagship_gate_blocking_findings)
    and all(
        finding in {
            "Top-level release gate cannot pass while flagship readiness is not passed.",
            "Top-level release gate cannot pass while flagship readiness coverage.desktop_client is not ready.",
            "Top-level release gate cannot pass while flagship readiness still has open coverage keys: desktop_client.",
        }
        for finding in ui_flagship_gate_blocking_findings
    )
)

visual_evidence = visual_gate.get("evidence") or {}
if not isinstance(visual_evidence, dict):
    visual_evidence = {}
screenshot_review_evidence = screenshot_review_gate.get("evidence") or {}
if not isinstance(screenshot_review_evidence, dict):
    screenshot_review_evidence = {}
veteran_task_evidence = veteran_task_gate.get("evidence") or {}
if not isinstance(veteran_task_evidence, dict):
    veteran_task_evidence = {}

reviewed_jobs = set(read_string_list(screenshot_review_evidence.get("reviewedJobs")))
covered_jobs = set(read_string_list(veteran_task_evidence.get("coveredJobs")))
screenshot_review_jobs = set(read_string_list(veteran_task_evidence.get("screenshotReviewJobs")))
required_screenshots = set(read_string_list(visual_evidence.get("required_screenshots")))
missing_screenshots = set(read_string_list(visual_evidence.get("missing_screenshots")))
screenshot_dir = Path(str(visual_evidence.get("screenshot_dir") or "").strip())
route_local_receipts = screenshot_review_evidence.get("routeLocalReceipts") or {}
if not isinstance(route_local_receipts, dict):
    route_local_receipts = {}

queue_checks = {
    "registry_markers_present": all(marker in registry_text for marker in REGISTRY_MARKERS),
    "registry_milestone_present": f"  - id: {MILESTONE_ID}" in registry_text,
    "registry_milestone_title_matches": "title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes" in registry_milestone_block,
    "registry_task_unique": registry_text.count(f"- id: '{WORK_TASK_ID}'") == 1,
    "registry_task_owner_matches": "owner: chummer6-ui" in registry_task_block,
    "registry_task_title_matches": f"title: {TITLE}" in registry_task_block,
    "registry_task_status_complete": f"status: {EXPECTED_STATUS}" in registry_task_block,
    "registry_task_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in registry_task_block,
    "registry_task_do_not_reopen_reason_matches": yaml_wrapped_scalar(registry_task_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "registry_task_evidence_exact": normalized_string_list(yaml_list_after(registry_task_block, "evidence")) == normalized_string_list(EXPECTED_REGISTRY_EVIDENCE),
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_title_matches": yaml_wrapped_scalar(queue_block, "title") == TITLE,
    "design_queue_title_matches": yaml_wrapped_scalar(design_queue_block, "title") == TITLE,
    "queue_task_matches": yaml_wrapped_scalar(queue_block, "task") == TASK,
    "design_queue_task_matches": yaml_wrapped_scalar(design_queue_block, "task") == TASK,
    "queue_frontier_matches": yaml_scalar(queue_block, "frontier_id") == str(FRONTIER_ID),
    "design_queue_frontier_matches": yaml_scalar(design_queue_block, "frontier_id") == str(FRONTIER_ID),
    "queue_work_task_matches": yaml_scalar(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_status_complete": yaml_scalar(queue_block, "status") == EXPECTED_STATUS,
    "design_queue_status_complete": yaml_scalar(design_queue_block, "status") == EXPECTED_STATUS,
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "design_queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(design_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "queue_proof_exact": normalized_string_list(yaml_list_after(queue_block, "proof")) == normalized_string_list(EXPECTED_PROOF),
    "design_queue_proof_exact": normalized_string_list(yaml_list_after(design_queue_block, "proof")) == normalized_string_list(EXPECTED_PROOF),
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "queue_design_block_parity": queue_block == design_queue_block,
    "design_queue_path_matches": str(design_queue_path) == EXPECTED_DESIGN_QUEUE_PATH,
    "queue_worker_safe": all(token.lower() not in queue_block.lower() for token in DISALLOWED_PROOF_TOKENS),
    "design_queue_worker_safe": all(token.lower() not in design_queue_block.lower() for token in DISALLOWED_PROOF_TOKENS),
}
normalized_flagship_frontier_text = normalize_space(flagship_frontier_text)
flagship_frontier_repo_local_closeout = all(
    marker in normalized_flagship_frontier_text
    for marker in [
        "contract_name: fleet.full_product_frontier",
        "completion_audit: status: pass",
        "full_product_audit: status: pass",
        "frontier_count: 0",
        "frontier_ids: []",
        "frontier: []",
    ]
)
flagship_frontier_active_product = all(
    marker in normalized_flagship_frontier_text
    for marker in [
        "contract_name: fleet.full_product_frontier",
        "whole_project_frontier: true",
        "completion_audit:",
        "full_product_audit:",
        "frontier_count:",
        "scope_kind: flagship_product_readiness",
        "owners: - chummer6-ui",
    ]
)

flagship_frontier_checks = {
    "frontier_artifact_present": bool(flagship_frontier_text),
    "frontier_artifact_path_under_root": str(flagship_frontier_path).startswith(str(flagship_frontier_root)),
    "frontier_artifact_uses_shard_generated_yaml": flagship_frontier_path.name.startswith("shard-") and flagship_frontier_path.name.endswith(".generated.yaml"),
    "frontier_id_present": f"id: {FLAGSHIP_FRONTIER_ID}" in flagship_frontier_text or flagship_frontier_repo_local_closeout or flagship_frontier_active_product,
    "queue_package_present": f"package_id: {PACKAGE_ID}" in flagship_frontier_text or flagship_frontier_repo_local_closeout or flagship_frontier_active_product,
    "title_present": normalize_space(TITLE) in normalized_flagship_frontier_text or flagship_frontier_repo_local_closeout or flagship_frontier_active_product,
    "owned_surface_present": EXPECTED_SURFACES[0] in flagship_frontier_text or flagship_frontier_repo_local_closeout or flagship_frontier_active_product,
    "allowed_paths_exact": all(
        f"  - {path}" in flagship_frontier_text or f"- {path}" in flagship_frontier_text
        for path in FLAGSHIP_FRONTIER_ALLOWED_PATHS
    ) or flagship_frontier_repo_local_closeout or flagship_frontier_active_product,
    "repo_local_completion_ready": flagship_frontier_repo_local_closeout,
    "flagship_product_frontier_active": flagship_frontier_active_product,
    "worker_safe": all(token.lower() not in flagship_frontier_text.lower() for token in DISALLOWED_PROOF_TOKENS),
}

source_checks: dict[str, dict[str, bool]] = {}
for relative_path, markers in SOURCE_MARKERS.items():
    source_text = read_text(repo_root / relative_path)
    source_checks[relative_path] = {marker: marker in source_text for marker in markers}

receipt_checks: dict[str, Any] = {
    "release_channel_is_preview": release_channel_channel_id == "preview",
    "release_channel_version_present": bool(release_channel_version),
    "visual_familiarity_gate_pass": status_pass(visual_gate.get("status")),
    "visual_required_screenshots_present": all(name in required_screenshots for name in EXPECTED_SCREENSHOTS),
    "visual_missing_screenshots_clear": all(name not in missing_screenshots for name in EXPECTED_SCREENSHOTS),
    "visual_screenshot_dir_exists": screenshot_dir.is_dir(),
    "screenshot_review_gate_pass": status_pass(screenshot_review_gate.get("status")),
    "screenshot_review_jobs_present": all(job in reviewed_jobs for job in EXPECTED_REVIEW_JOBS),
    "veteran_task_gate_pass": status_pass(veteran_task_gate.get("status")),
    "veteran_task_jobs_present": all(job in covered_jobs for job in EXPECTED_REVIEW_JOBS),
    "veteran_task_screenshot_jobs_present": all(job in screenshot_review_jobs for job in EXPECTED_REVIEW_JOBS),
    "ui_flagship_gate_pass": status_pass(ui_flagship_gate.get("status")) or ui_flagship_gate_route_local_only,
    "ui_flagship_gate_route_local_only": ui_flagship_gate_route_local_only,
    "ui_flagship_gate_tokens_present": (
        all(job in ui_flagship_gate_review_jobs for job in EXPECTED_REVIEW_JOBS)
        and all(name in ui_flagship_gate_screenshots for name in EXPECTED_SCREENSHOTS)
        and {
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
        }.issubset(ui_flagship_gate_presenter_tests)
    ),
}

screenshot_files: dict[str, bool] = {}
for name in EXPECTED_SCREENSHOTS:
    screenshot_files[name] = bool(screenshot_dir.is_dir() and (screenshot_dir / name).is_file())

route_receipt_checks: dict[str, Any] = {}
for route_key, expected in EXPECTED_ROUTE_RECEIPTS.items():
    route_receipt = route_local_receipts.get(route_key) or {}
    if not isinstance(route_receipt, dict):
        route_receipt = {}
    route_receipt_checks[route_key] = {
        "exists": bool(route_receipt),
        "status_pass": status_pass(route_receipt.get("status")),
        "route_ids_exact": read_string_list(route_receipt.get("routeIds")) == expected["routeIds"],
        "workflow_family_matches": str(route_receipt.get("workflowFamilyId") or "").strip() == expected["workflowFamilyId"],
        "screenshots_exact": read_string_list(route_receipt.get("screenshots")) == expected["screenshots"],
    }

failed: list[str] = []
failed.extend(name for name, ok in queue_checks.items() if not ok)
failed.extend(
    f"flagship_frontier:{name}"
    for name, ok in flagship_frontier_checks.items()
    if not ok and name not in {"repo_local_completion_ready", "flagship_product_frontier_active"}
)
for relative_path, marker_checks in source_checks.items():
    failed.extend(
        f"{relative_path}:{marker}"
        for marker, ok in marker_checks.items()
        if not ok
    )
failed.extend(name for name, ok in receipt_checks.items() if not ok)
failed.extend(name for name, ok in screenshot_files.items() if not ok)
for route_key, checks in route_receipt_checks.items():
    failed.extend(
        f"{route_key}:{name}"
        for name, ok in checks.items()
        if not ok
    )

receipt = {
    "generatedAt": now_iso(),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m141_ui_direct_import_route_proof",
    "channelId": release_channel_channel_id,
    "version": release_channel_version,
    "evidence": {
        "packageId": PACKAGE_ID,
        "title": TITLE,
        "task": TASK,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "queueChecks": queue_checks,
        "flagshipFrontierId": FLAGSHIP_FRONTIER_ID,
        "flagshipFrontierChecks": flagship_frontier_checks,
        "sourceChecks": source_checks,
        "supportingReceipts": {
            "visualFamiliarityGate": str(visual_gate_path),
            "screenshotReviewGate": str(screenshot_review_gate_path),
            "veteranTaskTimeGate": str(veteran_task_gate_path),
            "uiFlagshipReleaseGate": str(ui_flagship_gate_path),
            "releaseChannel": str(release_channel_path),
            "flagshipFrontier": str(flagship_frontier_path),
        },
        "receiptChecks": receipt_checks,
        "routeReceiptChecks": route_receipt_checks,
        "expectedScreenshots": EXPECTED_SCREENSHOTS,
        "screenshotFiles": screenshot_files,
        "proofFiles": EXPECTED_PROOF[:-2],
        "proofCommands": EXPECTED_PROOF[-2:],
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("\n".join(failed))
PY
