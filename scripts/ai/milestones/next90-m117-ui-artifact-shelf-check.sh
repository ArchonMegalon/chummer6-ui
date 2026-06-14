#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M117_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M117_UI_ARTIFACT_SHELF.generated.json}"

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

PACKAGE_ID = "next90-m117-ui-artifact-shelf"
QUEUE_TITLE = "Add artifact shelf entry points to desktop surfaces"
REGISTRY_TITLE = "Close desktop artifact shelf and public proof shelf entry points across home, campaign, build, and publication surfaces."
TASK = "Expose artifact shelves from desktop home, campaign, build, and publication surfaces without hiding source truth."
FRONTIER_ID = 3393065971
MILESTONE_ID = 117
WORK_TASK_ID = "117.3"
WAVE = "W13"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "artifact_shelf:desktop",
    "public_proof_shelf:desktop",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M117ArtifactShelfGuardTests|FullyQualifiedName~Next90M116CreatorPublicationGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M117 chummer6-ui desktop artifact shelf entry points are complete; future shards must verify the"
EXPECTED_PROOF = [
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopCreatorPublicationWindow.cs",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M117ArtifactShelfGuardTests.cs",
    f"{repo_root}/.codex-studio/published/NEXT90_M117_UI_ARTIFACT_SHELF.generated.json",
    f"{repo_root}/scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh",
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
    EXPECTED_PRESENTATION_TEST_COMMAND,
]

SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        'desktop.home.button.open_my_artifacts',
        'desktop.home.button.open_campaign_artifacts',
        'desktop.home.button.open_published_artifacts',
        "OpenArtifactShelfView",
    ],
    "Chummer.Avalonia/DesktopCampaignArtifactWindow.cs": [
        '"Open My Artifact Shelf"',
        '"Open Campaign Artifact Shelf"',
        '"Open Creator Artifact Shelf"',
        '"Open Public Proof Shelf"',
        'OpenArtifactShelfView("personal")',
        'OpenArtifactShelfView("campaign")',
        'OpenArtifactShelfView("creator")',
        'OpenArtifactShelfView("public")',
    ],
    "Chummer.Avalonia/DesktopCreatorPublicationWindow.cs": [
        '"Open Creator Artifact Shelf"',
        '"Open My Artifact Shelf"',
        '"Open Campaign Artifact Shelf"',
        '"Open Public Proof Shelf"',
        'OpenArtifactShelfView("creator")',
        'OpenArtifactShelfView("personal")',
        'OpenArtifactShelfView("campaign")',
        'OpenArtifactShelfView("public")',
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        'RequireContains(source, "desktop.home.button.open_my_artifacts");',
        'RequireContains(source, "desktop.home.button.open_campaign_artifacts");',
        'RequireContains(source, "desktop.home.button.open_published_artifacts");',
        'RequireContains(source, "OpenArtifactShelfView");',
        'RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal($\\"/artifacts?view={Uri.EscapeDataString(view)}\\")");',
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
    if block_start == -1:
        block_start = text.rfind("\n  - title:", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n- title:", start)
    if next_start == -1:
        next_start = text.find("\n  - title:", start)
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def block_for_work_task(text: str, task_id: str) -> str:
    marker = f"- id: {task_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing work task row for {task_id}")
    block_start = text.rfind("\n", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n      - id:", start + len(marker))
    if next_start == -1:
        next_start = text.find("\n  - id:", start + len(marker))
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    start = block.find(marker)
    if start == -1:
        raise AssertionError(f"missing {key}")
    items: list[str] = []
    for line in block[start + len(marker):].splitlines():
        stripped = line.lstrip(" ")
        if stripped.startswith("- title:"):
            break
        if stripped.startswith("- "):
            items.append(stripped.removeprefix("- ").strip())
            continue
        if items and line.startswith("    ") and not line.strip().endswith(":"):
            items[-1] = f"{items[-1]} {line.strip()}"
            continue
        if items:
            break
        if line and not line.startswith(" "):
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
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)

checks = {
    "registry_has_m117_ui_task": f"- id: {WORK_TASK_ID}" in registry_text,
    "registry_task_unique": registry_text.count(f"- id: {WORK_TASK_ID}") == 1,
    "registry_task_title_matches": "title: Add artifact shelf entry points to desktop home, campaign, build, and publication surfaces." in registry_task_block,
    "registry_task_owner_matches": "owner: chummer6-ui" in registry_task_block,
    "registry_task_status_is_queue_managed": "status:" not in registry_task_block,
    "registry_task_evidence_is_queue_managed": "evidence:" not in registry_task_block,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_package_id_matches": yaml_scalar_after(queue_block, "package_id") == PACKAGE_ID,
    "design_queue_package_id_matches": yaml_scalar_after(design_queue_block, "package_id") == PACKAGE_ID,
    "queue_work_task_matches": yaml_scalar_after(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar_after(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_milestone_matches": yaml_scalar_after(queue_block, "milestone_id") == str(MILESTONE_ID),
    "design_queue_milestone_matches": yaml_scalar_after(design_queue_block, "milestone_id") == str(MILESTONE_ID),
    "queue_title_matches": yaml_scalar_after(queue_block, "title") == QUEUE_TITLE,
    "queue_task_matches": yaml_scalar_after(queue_block, "task") == TASK,
    "queue_status_complete": yaml_scalar_after(queue_block, "status") == "complete",
    "queue_wave_matches": yaml_scalar_after(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar_after(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar_after(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar_after(design_queue_block, "repo") == "chummer6-ui",
    "design_queue_title_matches": yaml_scalar_after(design_queue_block, "title") == QUEUE_TITLE,
    "design_queue_task_matches": yaml_scalar_after(design_queue_block, "task") == TASK,
    "design_queue_status_complete": yaml_scalar_after(design_queue_block, "status") == "complete",
    "queue_completion_action_matches": yaml_scalar_after(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar_after(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": EXPECTED_DO_NOT_REOPEN_REASON in yaml_scalar_after(queue_block, "do_not_reopen_reason"),
    "design_queue_do_not_reopen_reason_matches": EXPECTED_DO_NOT_REOPEN_REASON in yaml_scalar_after(design_queue_block, "do_not_reopen_reason"),
    "queue_proof_exact": yaml_list_after(queue_block, "proof") == EXPECTED_PROOF,
    "design_queue_proof_exact": yaml_list_after(design_queue_block, "proof") == EXPECTED_PROOF,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "queue_design_block_parity": normalize_whitespace(queue_block) == normalize_whitespace(design_queue_block),
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

receipt = {
    "generatedAt": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m117_ui_artifact_shelf",
    "evidence": {
        "packageId": PACKAGE_ID,
        "title": QUEUE_TITLE,
        "task": TASK,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "queueChecks": checks,
        "sourceChecks": source_checks,
        "proofCommands": {
            "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
            "targetedTestCommand": EXPECTED_TARGETED_TEST_COMMAND,
            "presentationTestCommand": EXPECTED_PRESENTATION_TEST_COMMAND,
        },
        "closedPackage": {
            "completionAction": EXPECTED_COMPLETION_ACTION,
            "doNotReopenReason": EXPECTED_DO_NOT_REOPEN_REASON,
            "proof": EXPECTED_PROOF,
        },
        "proofFiles": [
            f"{repo_root}/.codex-studio/published/NEXT90_M117_UI_ARTIFACT_SHELF.generated.json",
            f"{repo_root}/scripts/ai/milestones/next90-m117-ui-artifact-shelf-check.sh",
            f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
            f"{repo_root}/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
            f"{repo_root}/Chummer.Avalonia/DesktopCreatorPublicationWindow.cs",
            f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
            f"{repo_root}/Chummer.Tests/Compliance/Next90M117ArtifactShelfGuardTests.cs",
        ],
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m117 artifact-shelf proof failed: " + "; ".join(failed))
PY
