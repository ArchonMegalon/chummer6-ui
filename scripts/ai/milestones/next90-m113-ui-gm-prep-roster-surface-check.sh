#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M113_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json}"
local_release_proof_path="${CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH:-$repo_root/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json}"

mkdir -p "$(dirname "$receipt_path")"
CHUMMER_PORTAL_E2E_SKIP_EDGE_REBUILD=1 CHUMMER_PORTAL_PLAYWRIGHT=0 CHUMMER_PORTAL_LOCAL_PROOF_PATH="$local_release_proof_path" CHUMMER_NEXT90_M113_RECEIPT_PATH="$receipt_path" bash "$repo_root/scripts/e2e-portal.sh" >/dev/null

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$local_release_proof_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
local_release_proof_path = Path(sys.argv[5])
repo_root = Path(sys.argv[6])

PACKAGE_ID = "next90-m113-ui-gm-prep-roster-surface"
TITLE = "Add GM prep and roster movement surfaces to the desktop workspace"
TASK = "Add GM prep and roster movement surfaces to the primary desktop workspace route."
MILESTONE_TASK_ANCHOR = """- id: 113.3
        owner: chummer6-ui
        title: Add GM prep and roster movement surfaces to the desktop campaign workspace."""
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "gm_prep_packets:desktop",
    "roster_movement:desktop",
]
EXPECTED_WORK_TASK_ID = "113.3"
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M113GmPrepRosterSurfaceGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "FullyQualifiedName~AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"

SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "BuildReadinessBody()",
        "BuildRestoreBody()",
        "CreateReadinessActions()",
        "CreateRestoreActions()",
        "ShowGmPrepAsync",
        "ShowRosterMovementAsync",
        "Runboard:",
        "Next session:",
        "Keep Local Work",
        "Save Local Work",
        "OpenCampaignFollowThroughAsync",
        "OpenDevicesAccessWindowAsync",
        "OpenWorkspaceSupport",
    ],
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        "\"Open GM Prep Packets\"",
        "\"Open Roster Movement\"",
        "OpenGmPrepPacketsAsync",
        "OpenRosterMovementAsync",
        "OpenCreatorPublicationAsync()",
        "OpenCreatorModerationAsync()",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId)",
    ],
    "Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs": [
        "\"Open GM Prep Packets\"",
        "\"Open Roster Movement\"",
        "\"Open Creator Publication\"",
        "\"Review Moderation Flow\"",
        "OpenGmPrepPacketsAsync",
        "OpenRosterMovementAsync",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId, _portabilityActivity)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId, _portabilityActivity)",
    ],
    "Chummer.Avalonia/MainWindow.EventHandlers.cs": [
        "ToolStrip_OnGmPrepRequested",
        "ToolStrip_OnRosterMovementRequested",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, DesktopHeadId)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, DesktopHeadId)",
    ],
    "Chummer.Avalonia/MainWindow.axaml.cs": [
        "onGmPrepRequested: ToolStrip_OnGmPrepRequested,",
        "onRosterMovementRequested: ToolStrip_OnRosterMovementRequested,",
    ],
    "Chummer.Avalonia/MainWindow.ControlBinding.cs": [
        "AttachToolStripHandlers(toolStrip);",
        "AttachToolStripHandlers(classicToolStrip);",
        "surface.GmPrepRequested += onGmPrepRequested;",
        "surface.RosterMovementRequested += onRosterMovementRequested;",
    ],
    "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs": [
        "ShowGmPrep: showSampleControls,",
        "ShowRosterMovement: showSampleControls,",
    ],
    "Chummer.Avalonia/Controls/ToolStripControl.axaml.cs": [
        "public event EventHandler? GmPrepRequested;",
        "public event EventHandler? RosterMovementRequested;",
        "ApplyVisibility(GmPrepButton, state.ShowGmPrep);",
        "ApplyVisibility(RosterMovementButton, state.ShowRosterMovement);",
        "SetButtonLabel(GmPrepButton, \"Open GM Prep Packets\");",
        "SetButtonLabel(RosterMovementButton, \"Open Roster Movement\");",
    ],
    "Chummer.Avalonia/App.axaml.cs": [
        "DesktopStartupSurfaceCatalog.GmPrepPackets",
        "DesktopStartupSurfaceCatalog.RosterMovement",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(owner, \"avalonia\")",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(owner, \"avalonia\")",
    ],
    "Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs": [
        "public const string GmPrepPackets = \"gm_prep_packets\";",
        "public const string RosterMovement = \"roster_movement\";",
    ],
    "Chummer.Tests/Chummer.Tests.csproj": [
        "Compliance\\Next90M113GmPrepRosterSurfaceGuardTests.cs",
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "DesktopCampaignWorkspace_promotes_gm_prep_packets_and_roster_movement()",
        "DesktopStartupSurfaceCatalog.GmPrepPackets",
        "DesktopStartupSurfaceCatalog.RosterMovement",
        "RequireContains(homeSource, \"Open GM Prep Packets\")",
        "RequireContains(homeSource, \"Open Roster Movement\")",
        "RequireContains(organizerSource, \"Open GM Prep Packets\")",
        "RequireContains(organizerSource, \"Open Roster Movement\")",
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId)",
    ],
    "scripts/e2e-portal.sh": [
        "NEXT90_M113_RECEIPT_PATH",
        "\"desktop_workspace_routes\": [",
        "\"gm_prep_packets:desktop\"",
        "\"roster_movement:desktop\"",
        "\"next90-m113-ui-gm-prep-roster-surface\"",
        "\"Desktop campaign workspace keeps GM prep packets and roster movement as first-class successor surfaces.\"",
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
local_release_proof = json.loads(local_release_proof_path.read_text(encoding="utf-8"))

checks = {
    "registry_has_m113_ui_task": MILESTONE_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(MILESTONE_TASK_ANCHOR) == 1,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_title_matches": f"title: {TITLE}" in queue_block,
    "queue_task_matches": yaml_scalar_after(queue_block, "task") == TASK,
    "queue_work_task_id_matches": f"work_task_id: {EXPECTED_WORK_TASK_ID}" in queue_block,
    "design_queue_title_matches": f"title: {TITLE}" in design_queue_block,
    "design_queue_task_matches": yaml_scalar_after(design_queue_block, "task") == TASK,
    "design_queue_work_task_id_matches": f"work_task_id: {EXPECTED_WORK_TASK_ID}" in design_queue_block,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_queue_path_matches": str(design_queue_path) == EXPECTED_DESIGN_QUEUE_PATH,
    "local_release_proof_status_pass": str(local_release_proof.get("status") or "").strip().lower() in {"pass", "passed"},
    "local_release_proof_receipt_path_present": str(receipt_path) in json.dumps(local_release_proof),
    "local_release_proof_package_present": PACKAGE_ID in json.dumps(local_release_proof),
    "local_release_proof_surfaces_present": all(surface in json.dumps(local_release_proof) for surface in EXPECTED_SURFACES),
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
        str(local_release_proof_path),
        f"{repo_root}/scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh",
        f"{repo_root}/scripts/e2e-portal.sh",
        f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
        f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
        f"{repo_root}/Chummer.Avalonia/MainWindow.EventHandlers.cs",
        f"{repo_root}/Chummer.Avalonia/MainWindow.axaml.cs",
        f"{repo_root}/Chummer.Avalonia/MainWindow.ControlBinding.cs",
        f"{repo_root}/Chummer.Avalonia/MainWindow.ShellFrameProjector.cs",
        f"{repo_root}/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs",
        f"{repo_root}/Chummer.Avalonia/App.axaml.cs",
        f"{repo_root}/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs",
        f"{repo_root}/Chummer.Tests/Chummer.Tests.csproj",
        f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
        f"{repo_root}/Chummer.Tests/Compliance/Next90M113GmPrepRosterSurfaceGuardTests.cs",
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
    raise SystemExit("next90-m113 gm-prep/roster-surface proof failed: " + "; ".join(failed))
PY
