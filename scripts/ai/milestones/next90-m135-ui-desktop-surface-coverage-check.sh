#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M135_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json}"

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
repo_root = Path(sys.argv[5]).resolve()

PACKAGE_ID = "next90-m135-ui-close-desktop-workbench-build-lab-gm-runboard-publicatio"
TITLE = "Close desktop workbench, Build Lab, GM Runboard, publication, restore, support, and veteran-familiarity surface coverage."
TASK = "Close desktop workbench, Build Lab, GM Runboard, publication, restore, support, and veteran-familiarity surface coverage."
FRONTIER_ID = 8351771106
MILESTONE_ID = 135
WORK_TASK_ID = "135.6"
WAVE = "W22"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "close_desktop_workbench_build_lab:ui",
]
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M135 chummer6-ui desktop surface coverage is complete; future shards must verify the closure receipt, "
    "veteran-familiarity gate, focused desktop guards, canonical registry row, and queue mirrors instead of reopening "
    "the workbench/publication/restore/support surface bundle."
)
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m135-ui-desktop-surface-coverage-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M135DesktopSurfaceCoverageGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "FullyQualifiedName~AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_VETERAN_GATE_COMMAND = "bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh"
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_PROOF_FILES = [
    f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopCreatorPublicationWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs",
    f"{repo_root}/Chummer.Avalonia/DesktopSupportWindow.cs",
    f"{repo_root}/Chummer.Avalonia/App.axaml.cs",
    f"{repo_root}/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs",
    f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M135DesktopSurfaceCoverageGuardTests.cs",
    f"{repo_root}/.codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M118_UI_ORGANIZER_OPS.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json",
    f"{repo_root}/.codex-studio/published/NEXT90_M135_UI_DESKTOP_SURFACE_COVERAGE.generated.json",
    f"{repo_root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
    f"{repo_root}/scripts/ai/milestones/next90-m135-ui-desktop-surface-coverage-check.sh",
]
EXPECTED_PROOF = EXPECTED_PROOF_FILES + [
    EXPECTED_VETERAN_GATE_COMMAND,
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
    EXPECTED_PRESENTATION_TEST_COMMAND,
]
SUB_RECEIPTS = {
    "restoreContinuity": repo_root / ".codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json",
    "ruleStudio": repo_root / ".codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json",
    "creatorPublication": repo_root / ".codex-studio/published/NEXT90_M116_UI_CREATOR_PUBLICATION.generated.json",
    "organizerOperations": repo_root / ".codex-studio/published/NEXT90_M118_UI_ORGANIZER_OPS.generated.json",
    "gmRunboard": repo_root / ".codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
    "desktopExplainFollowUp": repo_root / ".codex-studio/published/NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json",
}
SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        '"Open GM Runboard"',
        'CreateButton(S("desktop.home.button.open_my_artifacts"), OpenCreatorPublicationAsync)',
        'CreateButton(S("desktop.home.button.open_published_artifacts"), OpenCreatorModerationAsync)',
        "DesktopCreatorPublicationWindow.ShowAsync(",
        "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId);",
        "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId);",
        "DesktopSupportWindow.ShowAsync(this, _installState.HeadId);",
        "OpenInstallSupport",
    ],
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "ShowGmRunboardAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)",
        "S(\"desktop.campaign.section.runboard\")",
        "Runboard:",
        "BuildRestoreBody()",
        "DesktopSupportWindow.ShowAsync(this, _installState.HeadId);",
    ],
    "Chummer.Avalonia/DesktopCreatorPublicationWindow.cs": [
        "Creator publication",
        "review discovery posture, trust ranking, lineage, and moderation flow",
        'CreateButton("Review Moderation Flow", OpenModerationSurfaceAsync)',
        'CreateButton("Open Rule Environment Studio", OpenRuleEnvironmentStudioAsync, isPrimary: true)',
        "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())",
    ],
    "Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs": [
        "Desktop organizer operations surface requires an IChummerClient instance.",
        'CreateButton("Open Organizer Operations", OpenOrganizerOperationsSurfaceAsync',
        'CreateButton("Open Creator Publication", OpenCreatorPublicationAsync)',
        'CreateButton("Review Moderation Flow", OpenCreatorModerationAsync)',
        'CreateButton("Open Rule Environment Studio", OpenRuleEnvironmentStudioAsync)',
        'CreateButton("Open GM Runboard", OpenGmRunboardAsync)',
        "Publication boundary:",
        "Support escalation:",
        "Proof shelf:",
    ],
    "Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs": [
        'CreateSection("Amend-package lifecycle", BuildLifecycleBody())',
        'CreateSection("Before-after diffs", BuildDiffBody())',
        'CreateSection("Explain receipts", BuildReceiptBody())',
        "GetBuildPathSuggestionsAsync",
        "GetBuildPathPreviewAsync",
        "Rule-environment studio",
        'CreateButton("Support", () => OpenSupportAsync(owner))',
    ],
    "Chummer.Avalonia/DesktopSupportWindow.cs": [
        "Desktop support requires an IChummerClient instance.",
        "BuildCaseBody()",
        "BuildReleaseBody()",
        "BuildDiagnosticsBody()",
        "BuildFollowThroughBody()",
        "DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection)",
    ],
    "Chummer.Avalonia/App.axaml.cs": [
        "DesktopStartupSurfaceCatalog.GmRunboard",
        "DesktopStartupSurfaceCatalog.OrganizerOperations",
        "DesktopStartupSurfaceCatalog.RuleEnvironmentStudio",
        'DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(owner, "avalonia")',
        'DesktopOrganizerOperationsWindow.ShowAsync(owner, "avalonia")',
        'DesktopRuleEnvironmentStudioWindow.ShowAsync(owner, "avalonia")',
        'string.Equals(startupSurface, "support", StringComparison.OrdinalIgnoreCase)',
        'string.Equals(startupSurface, "support_case", StringComparison.OrdinalIgnoreCase)',
        'DesktopSupportWindow.ShowAsync(owner, "avalonia")',
    ],
    "Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs": [
        'public const string Support = "support";',
        'public const string SupportCase = "support_case";',
        'public const string GmRunboard = "gm_runboard";',
        'public const string OrganizerOperations = "organizer_operations";',
        'public const string RuleEnvironmentStudio = "rule_environment_studio";',
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "DesktopCampaignWorkspace_is_a_real_top_level_surface();",
        "DesktopCampaignWorkspace_promotes_gm_runboard_route();",
        "DesktopOrganizerOperationsSurface_is_a_real_top_level_surface();",
        "DesktopRuleEnvironmentStudioSurface_is_a_real_top_level_surface();",
        "DesktopCampaignWorkspace_keeps_restore_conflict_choices_visible();",
        "DesktopSupportSurface_is_a_real_top_level_surface();",
        "DesktopHome_exposes_claim_aware_install_and_update_actions();",
        'RequireContains(source, "DesktopCreatorPublicationWindow.ShowAsync(");',
    ],
    "scripts/ai/verify.sh": [
        "checking veteran task-time evidence gate",
        "bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
        "checking next-90 M135 desktop surface coverage closure guard",
        "bash scripts/ai/milestones/next90-m135-ui-desktop-surface-coverage-check.sh",
    ],
}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def read_json(path: Path) -> dict[str, object]:
    return json.loads(read_text(path))


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


def normalize_whitespace(value: str) -> str:
    return " ".join(value.split())


def block_for_work_task(text: str, task_id: str) -> str:
    marker = f"- id: '{task_id}'"
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
        if line.startswith("  - "):
            items.append(line.removeprefix("  - ").strip())
            continue
        if line.startswith("      - "):
            items.append(line.removeprefix("      - ").strip())
            continue
        if line.startswith("          - "):
            items.append(line.removeprefix("          - ").strip())
            continue
        if items and line.startswith("    ") and ":" not in line.strip():
            items[-1] = f"{items[-1]} {line.strip()}"
            continue
        if line.startswith("    ") and not line.startswith("      "):
            break
        if line.startswith("        ") and not line.startswith("          "):
            break
        if items:
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
            if continuation_text.startswith("- "):
                break
            if ":" in continuation_text and continuation_indent == base_indent + 2:
                break

            values.append(continuation_text)

        return " ".join(value for value in values if value).strip().strip("'\"")

    raise AssertionError(f"missing {key}")


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_has_m135_ui_task": f"- id: '{WORK_TASK_ID}'" in registry_text,
    "registry_task_unique": registry_text.count(f"- id: '{WORK_TASK_ID}'") == 1,
    "registry_task_title_matches": yaml_wrapped_scalar(registry_task_block, "title") == TITLE,
    "registry_task_owner_matches": yaml_scalar(registry_task_block, "owner") == "chummer6-ui",
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_package_id_matches": yaml_scalar(queue_block, "package_id") == PACKAGE_ID,
    "design_queue_package_id_matches": yaml_scalar(design_queue_block, "package_id") == PACKAGE_ID,
    "queue_work_task_matches": yaml_scalar(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_frontier_matches": yaml_scalar(queue_block, "frontier_id") == str(FRONTIER_ID),
    "design_queue_frontier_matches": yaml_scalar(design_queue_block, "frontier_id") == str(FRONTIER_ID),
    "queue_milestone_matches": yaml_scalar(queue_block, "milestone_id") == str(MILESTONE_ID),
    "design_queue_milestone_matches": yaml_scalar(design_queue_block, "milestone_id") == str(MILESTONE_ID),
    "queue_title_matches": yaml_wrapped_scalar(queue_block, "title") == TITLE,
    "design_queue_title_matches": yaml_wrapped_scalar(design_queue_block, "title") == TITLE,
    "queue_task_matches": yaml_wrapped_scalar(queue_block, "task") == TASK,
    "design_queue_task_matches": yaml_wrapped_scalar(design_queue_block, "task") == TASK,
    "queue_status_complete": yaml_scalar(queue_block, "status") == "complete",
    "design_queue_status_complete": yaml_scalar(design_queue_block, "status") == "complete",
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "design_queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(design_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
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

sub_receipt_checks: dict[str, dict[str, object]] = {}
for label, path in SUB_RECEIPTS.items():
    if not path.exists():
        sub_receipt_checks[label] = {
            "path": str(path),
            "exists": False,
            "statusPass": False,
        }
        continue

    payload = read_json(path)
    sub_receipt_checks[label] = {
        "path": str(path),
        "exists": True,
        "statusPass": payload.get("status") == "pass",
        "contractName": payload.get("contract_name"),
    }

failed = [name for name, ok in checks.items() if not ok]
for relative_path, marker_checks in source_checks.items():
    failed.extend(
        f"{relative_path}:{marker}"
        for marker, ok in marker_checks.items()
        if not ok
    )
for label, receipt_check in sub_receipt_checks.items():
    if not receipt_check["exists"]:
        failed.append(f"{label}:missing")
    elif not receipt_check["statusPass"]:
        failed.append(f"{label}:status")

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m135_ui_desktop_surface_coverage",
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
        "queueChecks": checks,
        "sourceChecks": source_checks,
        "subReceiptChecks": sub_receipt_checks,
        "proofCommands": {
            "veteranGateCommand": EXPECTED_VETERAN_GATE_COMMAND,
            "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
            "targetedTestCommand": EXPECTED_TARGETED_TEST_COMMAND,
            "presentationTestCommand": EXPECTED_PRESENTATION_TEST_COMMAND,
        },
        "proofFiles": EXPECTED_PROOF_FILES,
        "closedPackage": {
            "completionAction": EXPECTED_COMPLETION_ACTION,
            "doNotReopenReason": EXPECTED_DO_NOT_REOPEN_REASON,
            "proof": EXPECTED_PROOF,
        },
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m135 desktop surface coverage proof failed: " + "; ".join(failed))
PY
