#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_VETERAN_TASK_TIME_EVIDENCE_GATE_PATH:-$repo_root/.codex-studio/published/VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json}"
milestone_receipt_path="${CHUMMER_NEXT90_M141_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M141_UI_IMPORT_ROUTE_PROOF.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$milestone_receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
milestone_receipt_path = Path(sys.argv[5])
repo_root = Path(sys.argv[6])

PACKAGE_ID = "next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment"
TITLE = "Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."
TASK = "Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."
MILESTONE_TASK_ANCHOR = """- id: 141.1
        owner: chummer6-ui
        title: Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."""
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "capture_direct_screenshot_and_runtime_proof_for_translat:ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m141-ui-import-route-proof-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "Next90M141UiImportRouteProofGuardTests|DesktopDialogFactoryTests|CharacterOverviewPresenterTests|DualHeadAcceptanceTests|AvaloniaFlagshipUiGateTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_SCREENSHOTS = [
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
]
EXPECTED_RUNTIME_TOKENS = [
    "translator_xml_custom_data",
    "hero_lab_import_oracle",
    "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
    "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
    "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
    "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
    "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
]

SOURCE_MARKERS = {
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        "38-translator-dialog-light.png",
        "39-xml-editor-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png",
        "ImportRouteReviewSteps",
        "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
        'harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None)',
        'harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None)',
        'harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None)',
    ],
    "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs": [
        "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
        "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
        "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
    ],
    "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs": [
        "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        '"translator", "xml_editor", "hero_lab_importer"',
    ],
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs": [
        "BuildXmlEditorFields(",
        "xmlEditorXmlBridgePosture",
        "xmlEditorCustomDataAuthoringReceipt",
        "BuildHeroLabImporterFields(",
        "heroLabImportOracleLanePosture",
        "heroLabAdjacentSr6OracleReceipt",
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
        "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        '"translator_xml_custom_data": "pass"',
        '"hero_lab_import_oracle": "pass"',
    ],
}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def block_for_package(text: str, package_id: str) -> str:
    marker = f"package_id: {package_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing package row for {package_id}")
    title_start = text.rfind("\n- title:", 0, start)
    block_start = 0 if title_start == -1 else title_start + 1
    next_start = text.find("\n- title:", start + len(marker))
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    start = block.find(marker)
    if start == -1:
        raise AssertionError(f"missing {key}")
    items: list[str] = []
    for line in block[start + len(marker):].splitlines():
        stripped = line.lstrip()
        indent = len(line) - len(stripped)
        if stripped.startswith("- ") and indent >= 2:
            items.append(stripped.removeprefix("- ").strip())
            continue
        if items and indent <= 2 and stripped and not stripped.startswith("- "):
            break
        if not items and stripped and indent <= 0:
            break
    return items


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_has_m141_ui_task": MILESTONE_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(MILESTONE_TASK_ANCHOR) == 1,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_title_matches": f"title: {TITLE}" in queue_block,
    "queue_task_matches": f"task: {TASK}" in queue_block,
    "design_queue_title_matches": f"title: {TITLE}" in design_queue_block,
    "design_queue_task_matches": f"task: {TASK}" in design_queue_block,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_queue_path_matches": str(design_queue_path) == EXPECTED_DESIGN_QUEUE_PATH,
}

source_checks: dict[str, dict[str, bool]] = {}
for relative_path, markers in SOURCE_MARKERS.items():
    source_text = read_text(repo_root / relative_path)
    source_checks[relative_path] = {marker: marker in source_text for marker in markers}

failed = [name for name, ok in checks.items() if not ok]
for relative_path, marker_checks in source_checks.items():
    failed.extend(
        f"{relative_path}:{marker}"
        for marker, ok in marker_checks.items()
        if not ok
    )

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.veteran_task_time_evidence_gate",
    "packageId": PACKAGE_ID,
    "title": TITLE,
    "task": TASK,
    "status": "pass" if not failed else "fail",
    "sourceRegistryPath": str(registry_path),
    "sourceQueuePath": str(queue_path),
    "sourceDesignQueuePath": str(design_queue_path),
    "checks": checks,
    "sourceChecks": source_checks,
    "reviewSurfaceOrder": ["translator", "xml_amendment_editor", "hero_lab_importer"],
    "expectedScreenshots": EXPECTED_SCREENSHOTS,
    "runtimeProofTokens": EXPECTED_RUNTIME_TOKENS,
    "proofCommands": {
        "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
        "targetedTestCommand": EXPECTED_TARGETED_TEST_COMMAND,
    },
    "proofFiles": [
        str(receipt_path),
        str(milestone_receipt_path),
        f"{repo_root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
        f"{repo_root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
        f"{repo_root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
        f"{repo_root}/Chummer.Presentation/Overview/DesktopDialogFactory.cs",
        f"{repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
        f"{repo_root}/scripts/ai/milestones/next90-m141-ui-import-route-proof-check.sh",
    ],
    "failures": failed,
}

for output_path in (receipt_path, milestone_receipt_path):
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m141 ui import-route proof failed: " + "; ".join(failed))
PY
