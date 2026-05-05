#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M112_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M112_UI_CAMPAIGN_MEMORY.generated.json}"

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

PACKAGE_ID = "next90-m112-ui-campaign-memory"
TITLE = "Surface campaign memory and consequences on desktop"
TASK = "Make campaign consequences, stale state, and next-session return actions visible on the promoted desktop route."
MILESTONE_TASK_ANCHOR = """- id: 112.3
        owner: chummer6-ui
        title: Surface campaign memory, consequences, and return-loop actions on the primary desktop route."""
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "campaign_workspace:memory",
    "campaign_return_loop:desktop",
]
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M112 chummer6-ui campaign memory desktop surfacing is complete; future shards must verify the desktop route proof, focused guard tests, canonical registry row, and queue mirrors instead of reopening this slice."
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M112CampaignMemoryGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "FullyQualifiedName~AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"

SOURCE_MARKERS = {
    "scripts/ai/verify.sh": [
        "checking next-90 M112 campaign memory and return-loop desktop guard",
        "bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh",
    ],
    "Chummer.Tests/Chummer.Tests.csproj": [
        "Compliance\\Next90M112CampaignMemoryGuardTests.cs",
    ],
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        "BuildCampaignConsequenceVisibilitySummary()",
        "BuildCampaignMemoryVisibilitySummary()",
        "BuildCampaignConsequenceSummary()",
        "BuildCampaignConsequenceEvidenceSummary()",
        "BuildCampaignNextSessionReturnSummary()",
        "BuildCampaignReturnActionSummary()",
        "BuildCampaignNextSessionReturnActionSummary()",
        "BuildCampaignStaleStateVisibilitySummary()",
        "CreateCampaignMemoryActions()",
        "ResolveCampaignMemoryEvidence()",
        "\"Review Campaign Memory\"",
        "OpenWorkspaceSupport",
        "OpenCurrentWorkspace",
        "OpenDevicesAccessWindowAsync",
        "Review campaign consequences",
        "Review next-session return",
        "Campaign consequence proof:",
        "Campaign memory stale-state check:",
        "Next-session return actions:",
        "Stale state: server continuity is unavailable",
    ],
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "BuildCampaignConsequenceVisibilitySummary()",
        "BuildCampaignMemoryVisibilitySummary()",
        "BuildCampaignConsequenceSummary()",
        "BuildCampaignConsequenceEvidenceSummary()",
        "BuildCampaignNextSessionReturnSummary()",
        "BuildCampaignNextSessionReturnActionSummary()",
        "BuildRestoreStaleStateVisibilitySummary()",
        "BuildRestoreConflictChoiceSummary()",
        "CreateReadinessActions()",
        "CreateRestoreActions()",
        "ResolveCampaignMemorySummary()",
        "ResolveCampaignMemoryReturnSummary()",
        "ResolveCampaignMemoryEvidence()",
        "\"Open Rule Environment Studio\"",
        "OpenWorkspaceSupport",
        "OpenDevicesAccessWindowAsync",
        "Review campaign consequences",
        "Review next-session return",
        "Campaign consequence proof:",
        "Campaign memory stale-state check:",
        "Next-session return actions:",
        "Stale state: server continuity is unavailable",
        "Conflict choices:",
    ],
    "Chummer.Avalonia/App.axaml.cs": [
        "DesktopStartupSurfaceCatalog.EnvironmentVariableName",
        "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.CampaignWorkspace)",
        "DesktopCampaignWorkspaceWindow.ShowAsync(owner, \"avalonia\")",
        "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.GmPrepPackets)",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(owner, \"avalonia\")",
        "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.RosterMovement)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(owner, \"avalonia\")",
    ],
    "Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs": [
        "public const string CampaignWorkspace = \"campaign_workspace\";",
        "public const string GmPrepPackets = \"gm_prep_packets\";",
        "public const string RosterMovement = \"roster_movement\";",
        "public static bool Matches(string? startupSurface, string expectedSurface)",
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "DesktopHome_promotes_campaign_memory_and_return_actions()",
        "DesktopCampaignWorkspace_keeps_restore_conflict_choices_visible()",
        "BuildCampaignConsequenceEvidenceSummary()",
        "ResolveCampaignMemoryEvidence()",
        "Campaign consequence proof:",
        "Review campaign consequences",
        "BuildCampaignNextSessionReturnActionSummary()",
        "BuildCampaignStaleStateVisibilitySummary()",
        "\"Review Campaign Memory\"",
        "OpenWorkspaceSupport",
        "OpenCurrentWorkspace",
        "OpenDevicesAccessWindowAsync",
        "Campaign memory stale-state check:",
        "Conflict choices:",
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


def block_for_work_task(text: str, work_task_id: str) -> str:
    marker = f"      - id: {work_task_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing work task row for {work_task_id}")
    next_start = text.find("\n      - id:", start + len(marker))
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
        if line.startswith("    ") and not line.startswith("      "):
            break
        if items:
            break
    return items


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
registry_block = block_for_work_task(registry_text, "112.3")
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_has_m112_ui_task": MILESTONE_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(MILESTONE_TASK_ANCHOR) == 1,
    "registry_status_complete": f"status: {EXPECTED_STATUS}" in registry_block,
    "registry_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in registry_block,
    "registry_do_not_reopen_reason_matches": f"do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}" in registry_block,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_status_complete": f"status: {EXPECTED_STATUS}" in queue_block,
    "queue_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in queue_block,
    "queue_do_not_reopen_reason_matches": f"do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}" in queue_block,
    "queue_title_matches": f"title: {TITLE}" in queue_block,
    "queue_task_matches": f"task: {TASK}" in queue_block,
    "design_queue_status_complete": f"status: {EXPECTED_STATUS}" in design_queue_block,
    "design_queue_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in design_queue_block,
    "design_queue_do_not_reopen_reason_matches": f"do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}" in design_queue_block,
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

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "packageId": PACKAGE_ID,
    "title": TITLE,
    "task": TASK,
    "status": "pass" if not failed else "fail",
    "sourceRegistryPath": str(registry_path),
    "sourceQueuePath": str(queue_path),
    "sourceDesignQueuePath": str(design_queue_path),
    "checks": checks,
    "sourceChecks": source_checks,
    "proofCommands": {
        "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
        "targetedTestCommand": EXPECTED_TARGETED_TEST_COMMAND,
        "presentationTestCommand": EXPECTED_PRESENTATION_TEST_COMMAND,
    },
    "proofFiles": [
        str(receipt_path),
        f"{repo_root}/scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh",
        f"{repo_root}/scripts/ai/verify.sh",
        f"{repo_root}/Chummer.Tests/Chummer.Tests.csproj",
        f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
        f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
        f"{repo_root}/Chummer.Avalonia/App.axaml.cs",
        f"{repo_root}/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs",
        f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
        f"{repo_root}/Chummer.Tests/Compliance/Next90M112CampaignMemoryGuardTests.cs",
    ],
    "failures": failed,
}

if receipt_path.exists():
    try:
        existing_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        existing_receipt = None
    if isinstance(existing_receipt, dict):
        comparable_receipt = dict(receipt)
        comparable_existing_receipt = dict(existing_receipt)
        comparable_receipt.pop("generatedAt", None)
        comparable_existing_receipt.pop("generatedAt", None)
        if comparable_receipt == comparable_existing_receipt and isinstance(existing_receipt.get("generatedAt"), str):
            receipt["generatedAt"] = existing_receipt["generatedAt"]

receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m112 campaign-memory proof failed: " + "; ".join(failed))
PY
