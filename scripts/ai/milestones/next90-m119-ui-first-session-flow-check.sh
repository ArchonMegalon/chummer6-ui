#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M119_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json}"

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

PACKAGE_ID = "next90-m119-ui-first-session-flow"
QUEUE_TITLE = "Add first playable session flow to desktop home and campaign entry points"
TASK = "Add first playable session flow to desktop home and campaign entry points."
REGISTRY_MILESTONE_TITLE = "Guided onboarding and starter lane to first playable session"
REGISTRY_TASK_TITLE = "Add first playable session flow to desktop home and campaign entry points."
FRONTIER_ID = 3766544333
MILESTONE_ID = 119
WORK_TASK_ID = "119.2"
WAVE = "W14"
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M119 chummer6-ui desktop first playable session entry points are complete; future shards must verify the home and campaign starter-lane actions, the closed-package guard and receipt, standard verifier wiring, and the canonical registry plus queue rows instead of reopening this slice."
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "first_playable_session:desktop",
    "campaign_entry:first_session",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M119FirstSessionFlowGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_PROOF = [
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M119FirstSessionFlowGuardTests.cs",
    f"{repo_root}/Chummer.Tests/Chummer.Tests.csproj",
    f"{repo_root}/scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh",
    f"{repo_root}/scripts/ai/verify.sh",
    f"{repo_root}/.codex-studio/published/NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json",
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
    EXPECTED_PRESENTATION_TEST_COMMAND,
]
EXPECTED_REGISTRY_EVIDENCE = [
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs and {repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs keep starter-lane review and first-playable-session launch entry points distinct across the promoted desktop home and campaign follow-through surfaces.",
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs and {repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs route signed-in starter-lane launches straight to the mission briefing when a lead workspace exists and fall back to the campaign primer when the first session is still pre-workspace.",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs, {repo_root}/Chummer.Tests/Compliance/Next90M119FirstSessionFlowGuardTests.cs, {repo_root}/Chummer.Tests/Chummer.Tests.csproj, {repo_root}/scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh, and {repo_root}/scripts/ai/verify.sh fail closed when starter-lane desktop actions, receipt proof, or canonical queue and registry closure drift from the completed package contract.",
    f"{repo_root}/.codex-studio/published/NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json records the closed-package receipt for `next90-m119-ui-first-session-flow`.",
]

SOURCE_MARKERS = {
    "scripts/ai/verify.sh": [
        "checking next-90 M119 first playable session desktop flow guard",
        "bash scripts/ai/milestones/next90-m119-ui-first-session-flow-check.sh",
    ],
    "Chummer.Tests/Chummer.Tests.csproj": [
        "Compliance\\Next90M119FirstSessionFlowGuardTests.cs",
    ],
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        '"Start First Playable Session"',
        '"Review Starter Lane"',
        'CreateButton("Review Starter Lane", OpenStarterLaneReviewAsync)',
        'CreateButton("Start First Playable Session", OpenFirstPlayableSessionAsync',
        'private Task OpenFirstPlayableSessionAsync()',
        'private Task OpenStarterLaneReviewAsync()',
        'return OpenMissionBriefingArtifact();',
        'return OpenCampaignPrimerArtifact();',
        'BuildFirstPlayableSessionSummary()',
        'FindCampaignHighlight("First session:")',
    ],
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        '"Start First Playable Session"',
        '"Review Starter Lane"',
        'CreateButton("Review Starter Lane", OpenStarterLaneReviewAsync)',
        'CreateButton("Start First Playable Session", OpenFirstPlayableSessionAsync',
        'private Task OpenFirstPlayableSessionAsync()',
        'private Task OpenStarterLaneReviewAsync()',
        'return OpenMissionBriefingArtifact();',
        'return OpenCampaignPrimerArtifact();',
        'BuildFirstPlayableSessionSummary()',
        'FindCampaignHighlight("First session:")',
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        'RequireContains(source, "\\"Review Starter Lane\\"");',
        'RequireContains(source, "OpenFirstPlayableSessionAsync()");',
        'RequireContains(source, "OpenStarterLaneReviewAsync()");',
        'RequireContains(source, "CreateButton(\\"Review Starter Lane\\", OpenStarterLaneReviewAsync)");',
    ],
}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


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
        if line.startswith("          - "):
            items.append(line.removeprefix("          - ").strip())
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
            return stripped.removeprefix(marker).strip()
    raise AssertionError(f"missing {key}")


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
registry_milestone_block = block_for_milestone(registry_text, MILESTONE_ID)

checks = {
    "registry_has_m119_milestone": f"  - id: {MILESTONE_ID}" in registry_text,
    "registry_m119_title_matches": f"title: {REGISTRY_MILESTONE_TITLE}" in registry_milestone_block,
    "registry_has_m119_ui_task": f"- id: {WORK_TASK_ID}" in registry_text,
    "registry_task_unique": registry_text.count(f"- id: {WORK_TASK_ID}") == 1,
    "registry_task_title_matches": f"title: {REGISTRY_TASK_TITLE}" in registry_task_block,
    "registry_task_owner_matches": "owner: chummer6-ui" in registry_task_block,
    "registry_task_status_complete": f"status: {EXPECTED_STATUS}" in registry_task_block,
    "registry_task_evidence_exact": yaml_list_after(registry_task_block, "evidence") == EXPECTED_REGISTRY_EVIDENCE,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_package_id_matches": yaml_scalar(queue_block, "package_id") == PACKAGE_ID,
    "design_queue_package_id_matches": yaml_scalar(design_queue_block, "package_id") == PACKAGE_ID,
    "queue_work_task_matches": yaml_scalar(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_milestone_matches": yaml_scalar(queue_block, "milestone_id") == str(MILESTONE_ID),
    "design_queue_milestone_matches": yaml_scalar(design_queue_block, "milestone_id") == str(MILESTONE_ID),
    "queue_title_matches": f"title: {QUEUE_TITLE}" in queue_block,
    "design_queue_title_matches": f"title: {QUEUE_TITLE}" in design_queue_block,
    "queue_task_matches": f"task: {TASK}" in queue_block,
    "design_queue_task_matches": f"task: {TASK}" in design_queue_block,
    "queue_status_complete": f"status: {EXPECTED_STATUS}" in queue_block,
    "design_queue_status_complete": f"status: {EXPECTED_STATUS}" in design_queue_block,
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_scalar(queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "design_queue_do_not_reopen_reason_matches": yaml_scalar(design_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "queue_proof_exact": yaml_list_after(queue_block, "proof") == EXPECTED_PROOF,
    "design_queue_proof_exact": yaml_list_after(design_queue_block, "proof") == EXPECTED_PROOF,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "queue_design_block_parity": queue_block == design_queue_block,
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

status = "pass" if not failed else "fail"

receipt = {
    "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    "status": status,
    "contract_name": "chummer6-ui.next90_m119_ui_first_session_flow",
    "summary": "Desktop first-session flow keeps review and start entry points distinct across home and campaign surfaces.",
    "evidence": {
        "packageId": PACKAGE_ID,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "proofFiles": [
            str(repo_root / ".codex-studio" / "published" / "NEXT90_M119_UI_FIRST_SESSION_FLOW.generated.json"),
            str(repo_root / "scripts" / "ai" / "milestones" / "next90-m119-ui-first-session-flow-check.sh"),
            str(repo_root / "scripts" / "ai" / "verify.sh"),
            str(repo_root / "Chummer.Tests" / "Chummer.Tests.csproj"),
            str(repo_root / "Chummer.Avalonia" / "DesktopHomeWindow.cs"),
            str(repo_root / "Chummer.Avalonia" / "DesktopCampaignWorkspaceWindow.cs"),
            str(repo_root / "Chummer.Tests" / "Presentation" / "AccessibilitySignoffSmokeTests.cs"),
            str(repo_root / "Chummer.Tests" / "Compliance" / "Next90M119FirstSessionFlowGuardTests.cs"),
        ],
        "proofCommands": [
            EXPECTED_DIRECT_PROOF_COMMAND,
            EXPECTED_TARGETED_TEST_COMMAND,
            EXPECTED_PRESENTATION_TEST_COMMAND,
        ],
        "queueChecks": checks,
        "sourceChecks": source_checks,
        "failures": failed,
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m119-ui-first-session-flow-check: " + "; ".join(failed))
PY
