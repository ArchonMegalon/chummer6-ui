#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M122_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M122_UI_DESKTOP_CAMPAIGN_CLOSEOUT.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

PACKAGE_ID = "next90-m122-ui-surface-campaign-adoption-runner-goal-pins-resolutionrep"
TITLE = "Surface campaign adoption, runner goal pins, ResolutionReport closeout, and BLACK LEDGER consequence proof on desktop."
TASK = TITLE
WORK_TASK_ID = "122.3"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "surface_campaign_adoption_runner_goal:ui",
]

SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        "BuildCampaignAdoptionSummary()",
        "BuildCampaignAdoptionConfidenceSummary()",
        "BuildRunnerGoalPinSummary()",
        "BuildResolutionReportCloseoutSummary()",
        "BLACK LEDGER consequence proof:",
        "Campaign adoption proof:",
        "Review campaign consequences before continuing this restore route.",
    ],
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "BuildCampaignAdoptionSummary()",
        "BuildCampaignAdoptionConfidenceSummary()",
        "BuildRunnerGoalPinSummary()",
        "BuildResolutionReportCloseoutSummary()",
        "BLACK LEDGER consequence proof:",
        "Campaign adoption proof:",
        "Review campaign consequences before continuing this restore route.",
    ],
    "Chummer.Presentation/Overview/DesktopHomeCampaignServerPlane.cs": [
        "DesktopHomeCampaignAdoptionDto",
        "DesktopHomeRunnerGoalPinDto",
        "DesktopHomeResolutionReportCloseoutDto",
        "DesktopHomeBlackLedgerConsequenceDto",
        "Campaign adoption:",
        "Adoption confidence:",
        "Goal pins:",
        "ResolutionReport closeout:",
        "BLACK LEDGER consequence:",
        "BLACK LEDGER proof:",
    ],
    "Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs": [
        "Campaign adoption:",
        "Adoption confidence:",
        "Adoption proof:",
        "Goal pins:",
        "ResolutionReport closeout:",
        "BLACK LEDGER consequence:",
        "BLACK LEDGER proof:",
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "BuildCampaignAdoptionSummary()",
        "BuildRunnerGoalPinSummary()",
        "BuildResolutionReportCloseoutSummary()",
        "BLACK LEDGER consequence proof:",
        "Campaign adoption proof:",
    ],
}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def normalize_whitespace(value: str) -> str:
    return " ".join(value.split())


def block_for_package(text: str, package_id: str) -> str:
    marker = f"package_id: {package_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing package row for {package_id}")
    block_start = text.rfind("\n- title:", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n- title:", start)
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


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    start = block.find(marker)
    if start == -1:
        raise AssertionError(f"missing {key}")
    items: list[str] = []
    for line in block[start + len(marker):].splitlines():
        stripped = line.lstrip()
        indent = len(line) - len(stripped)
        if stripped.startswith("- title:"):
            break
        if stripped.startswith("- ") and indent >= 2:
            items.append(stripped.removeprefix("- ").strip())
            continue
        if items and indent <= 2 and stripped and not stripped.startswith("- "):
            break
    return items


def yaml_scalar_after(block: str, key: str) -> str:
    marker = f"{key}:"
    lines = block.splitlines()
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith(f"- {marker}"):
            stripped = stripped.removeprefix("- ").strip()
        elif not stripped.startswith(marker):
            continue
        first = stripped.removeprefix(marker).strip()
        values = [first] if first else []
        base_indent = len(line) - len(line.lstrip(" "))
        for continuation in lines[index + 1:]:
            continuation_indent = len(continuation) - len(continuation.lstrip(" "))
            if continuation_indent <= base_indent:
                break
            continuation_text = continuation.lstrip(" ")
            if continuation_text.startswith("- title:"):
                break
            if continuation_text.startswith("- "):
                if values:
                    break
                continue
            if ":" in continuation_text:
                head = continuation_text.split(":", 1)[0]
                if head.replace("_", "").replace("-", "").isalnum():
                    break
            values.append(continuation.strip())
        return normalize_whitespace(" ".join(value for value in values if value))
    raise AssertionError(f"missing {key}")


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_has_task": TITLE in registry_task_block,
    "queue_title_matches": yaml_scalar_after(queue_block, "title") == TITLE,
    "queue_task_matches": yaml_scalar_after(queue_block, "task") == TASK,
    "design_queue_title_matches": yaml_scalar_after(design_queue_block, "title") == TITLE,
    "design_queue_task_matches": yaml_scalar_after(design_queue_block, "task") == TASK,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
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

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "packageId": PACKAGE_ID,
    "title": TITLE,
    "task": TASK,
    "status": "pass" if not failed else "fail",
    "checks": checks,
    "sourceChecks": source_checks,
    "proofFiles": [
        str(receipt_path),
        f"{repo_root}/scripts/ai/milestones/next90-m122-ui-desktop-campaign-closeout-check.sh",
        f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
        f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
        f"{repo_root}/Chummer.Presentation/Overview/DesktopHomeCampaignServerPlane.cs",
        f"{repo_root}/Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs",
        f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    ],
    "failures": failed,
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m122 desktop campaign closeout proof failed: " + "; ".join(failed))
PY
