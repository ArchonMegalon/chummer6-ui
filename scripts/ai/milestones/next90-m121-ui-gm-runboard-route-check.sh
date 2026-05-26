#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M121_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json}"

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

PACKAGE_ID = "next90-m121-ui-add-the-desktop-gm-runboard-route-with-initiative-action"
TITLE = "Add the desktop GM Runboard route with initiative, action budgets, scene objectives, opposition refs, and ResolutionReport entry."
TASK = "Add the desktop GM Runboard route with initiative, action budgets, scene objectives, opposition refs, and ResolutionReport entry."
FRONTIER_ID = 7834909683
MILESTONE_ID = 121
WORK_TASK_ID = "121.3"
WAVE = "W15"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "add_the_desktop_gm_runboard:ui",
]
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M121 chummer6-ui desktop GM Runboard route is complete; future shards must verify the desktop runboard proof, focused guard tests, canonical registry row, and queue mirrors instead of reopening this slice."
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m121-ui-gm-runboard-route-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M121GmRunboardRouteGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "FullyQualifiedName~AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_EVIDENCE = [
    f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs now provides a dedicated GM Runboard desktop surface with initiative, action-budget, scene-objective, heat-posture, opposition-ref, and ResolutionReport-entry summaries plus focused follow-through actions.",
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs and {repo_root}/Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs now promote the GM Runboard route directly from the existing desktop campaign and organizer follow-through surfaces.",
    f"{repo_root}/Chummer.Avalonia/App.axaml.cs and {repo_root}/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs now keep `gm_runboard` available as an explicit desktop startup surface instead of hiding the route behind broader campaign workspace entrypoints.",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs, {repo_root}/Chummer.Tests/Compliance/Next90M121GmRunboardRouteGuardTests.cs, and {repo_root}/scripts/ai/milestones/next90-m121-ui-gm-runboard-route-check.sh fail closed when the GM Runboard route, startup surface, registry proof, or queue mirrors drift from the closed package contract.",
    f"{repo_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json records the closed-package receipt for `next90-m121-ui-add-the-desktop-gm-runboard-route-with-initiative-action`.",
]
EXPECTED_PROOF = [
    f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs",
    f"{repo_root}/Chummer.Avalonia/App.axaml.cs",
    f"{repo_root}/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M121GmRunboardRouteGuardTests.cs",
    f"{repo_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
    f"{repo_root}/scripts/ai/milestones/next90-m121-ui-gm-runboard-route-check.sh",
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
    EXPECTED_PRESENTATION_TEST_COMMAND,
]
SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "ShowGmRunboardAsync",
        '"desktop.campaign.section.runboard"',
        'Runboard:',
    ],
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        "\"Open GM Runboard\"",
        "OpenGmRunboardAsync()",
        "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId)",
    ],
    "Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs": [
        "\"Open GM Runboard\"",
        "OpenGmRunboardAsync()",
        "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId, _portabilityActivity)",
    ],
    "Chummer.Avalonia/App.axaml.cs": [
        "DesktopStartupSurfaceCatalog.GmRunboard",
        "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(owner, \"avalonia\")",
    ],
    "Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs": [
        "public const string GmRunboard = \"gm_runboard\";",
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "DesktopCampaignWorkspace_promotes_gm_runboard_route()",
        "RequireContains(source, \"desktop.campaign.section.runboard\")",
        "RequireContains(source, \"Runboard:\")",
        'RequireContains(homeSource, "Open GM Runboard")',
        'RequireContains(appSource, "DesktopStartupSurfaceCatalog.GmRunboard")',
    ],
    "Chummer.Tests/Chummer.Tests.csproj": [
        "Compliance\\Next90M121GmRunboardRouteGuardTests.cs",
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M121 desktop GM Runboard route guard",
        "bash scripts/ai/milestones/next90-m121-ui-gm-runboard-route-check.sh",
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
    quoted_marker = f"- id: '{task_id}'"
    start = text.find(marker)
    if start == -1:
        start = text.find(quoted_marker)
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
        if items and line.startswith(" ") and ":" not in line.strip():
            items[-1] = f"{items[-1]} {line.strip()}"
            continue
        if items:
            break
        if line and not line.startswith(" "):
            break
    return items


def yaml_scalar(block: str, key: str) -> str:
    marker = f"{key}:"
    lines = block.splitlines()
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith(marker) or stripped.startswith(f"- {marker}"):
            value = stripped.removeprefix(f"- {marker}" if stripped.startswith(f"- {marker}") else marker).strip()
            parts = [value]
            base_indent = len(line) - len(line.lstrip(" "))
            for continuation in lines[index + 1:]:
                continuation_indent = len(continuation) - len(continuation.lstrip(" "))
                continuation_stripped = continuation.strip()
                if not continuation_stripped:
                    break
                if continuation_stripped.startswith("- "):
                    break
                if ":" in continuation_stripped and continuation_indent == base_indent + 2:
                    break
                if continuation_indent <= base_indent:
                    break
                parts.append(continuation_stripped)
            return " ".join(part.strip("'") for part in parts).strip()
    raise AssertionError(f"missing {key}")


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_task_unique": registry_text.count(f"- id: {WORK_TASK_ID}") + registry_text.count(f"- id: '{WORK_TASK_ID}'") == 1,
    "registry_task_title_matches": yaml_scalar(registry_task_block, "title") == TASK,
    "registry_task_owner_matches": yaml_scalar(registry_task_block, "owner") == "chummer6-ui",
    "registry_task_status_is_queue_managed": "status:" not in registry_task_block,
    "registry_task_evidence_is_queue_managed": "evidence:" not in registry_task_block,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_work_task_matches": yaml_scalar(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_frontier_matches": int(yaml_scalar(queue_block, "frontier_id")) == FRONTIER_ID,
    "design_queue_frontier_matches": int(yaml_scalar(design_queue_block, "frontier_id")) == FRONTIER_ID,
    "queue_milestone_matches": int(yaml_scalar(queue_block, "milestone_id")) == MILESTONE_ID,
    "design_queue_milestone_matches": int(yaml_scalar(design_queue_block, "milestone_id")) == MILESTONE_ID,
    "queue_title_matches": yaml_scalar(queue_block, "title") == TITLE,
    "design_queue_title_matches": yaml_scalar(design_queue_block, "title") == TITLE,
    "queue_task_matches": yaml_scalar(queue_block, "task") == TASK,
    "design_queue_task_matches": yaml_scalar(design_queue_block, "task") == TASK,
    "queue_status_complete": yaml_scalar(queue_block, "status") == "complete",
    "design_queue_status_complete": yaml_scalar(design_queue_block, "status") == "complete",
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": normalize_whitespace(yaml_scalar(queue_block, "do_not_reopen_reason")) == normalize_whitespace(EXPECTED_DO_NOT_REOPEN_REASON),
    "design_queue_do_not_reopen_reason_matches": normalize_whitespace(yaml_scalar(design_queue_block, "do_not_reopen_reason")) == normalize_whitespace(EXPECTED_DO_NOT_REOPEN_REASON),
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

failures = [name for name, ok in checks.items() if not ok]
for relative_path, marker_checks in source_checks.items():
    failures.extend(
        f"{relative_path}:{marker}"
        for marker, ok in marker_checks.items()
        if not ok
    )

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.next90_m121_ui_gm_runboard_route",
    "status": "pass" if not failures else "fail",
    "evidence": {
        "packageId": PACKAGE_ID,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "queueChecks": checks,
        "sourceChecks": source_checks,
        "proofFiles": [
            str(repo_root / ".codex-studio" / "published" / "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json"),
            str(repo_root / "scripts" / "ai" / "milestones" / "next90-m121-ui-gm-runboard-route-check.sh"),
            str(repo_root / "Chummer.Avalonia" / "DesktopCampaignWorkspaceWindow.cs"),
            str(repo_root / "Chummer.Avalonia" / "DesktopHomeWindow.cs"),
            str(repo_root / "Chummer.Avalonia" / "DesktopOrganizerOperationsWindow.cs"),
            str(repo_root / "Chummer.Avalonia" / "App.axaml.cs"),
            str(repo_root / "Chummer.Desktop.Runtime" / "DesktopStartupSurfaceCatalog.cs"),
            str(repo_root / "Chummer.Tests" / "Presentation" / "AccessibilitySignoffSmokeTests.cs"),
            str(repo_root / "Chummer.Tests" / "Compliance" / "Next90M121GmRunboardRouteGuardTests.cs"),
        ],
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
    },
    "failures": failures,
}

with receipt_path.open("w", encoding="utf-8") as handle:
    json.dump(receipt, handle, indent=2)
    handle.write("\n")

if failures:
    raise SystemExit("\n".join(failures))
PY
