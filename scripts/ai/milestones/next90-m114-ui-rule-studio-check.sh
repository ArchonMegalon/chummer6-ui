#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M114_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json}"

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

PACKAGE_ID = "next90-m114-ui-rule-studio"
TITLE = "Add rule-environment studio entry points to desktop workflows"
TASK = "Surface amend-package lifecycle, before-after diffs, and explain receipts in mechanical desktop workflows."
FRONTIER_ID = 3253425842
MILESTONE_ID = 114
WORK_TASK_ID = "114.2"
WAVE = "W12"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "rule_environment_studio:desktop",
    "explain_receipts:desktop",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m114-ui-rule-studio-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M114RuleEnvironmentStudioGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
MILESTONE_TASK_ANCHOR = """- id: 114.2
        owner: chummer6-ui
        title: Add rule-environment studio and explain-receipt entry points to desktop mechanical workflows."""

SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        'CreateButton("Explain", OpenRuleEnvironmentStudioAsync)',
        "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)",
        "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId)",
        "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId)",
    ],
    "Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs": [
        "_portabilityActivity",
        "DesktopHomeWindow.ShowAsync(owner, _installState.HeadId)",
        "DesktopCampaignWorkspaceWindow.ShowAsync(owner, _installState.HeadId)",
    ],
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": [
        "public static Task ShowGmRunboardAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)",
        "public static Task ShowGmPrepAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)",
        "public static Task ShowRosterMovementAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)",
    ],
    "Chummer.Avalonia/DesktopCampaignArtifactWindow.cs": [
        "_portabilityActivity",
        'CreateButton("Open Rule Environment Studio", OpenRuleEnvironmentStudioAsync));',
        "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)",
        "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)",
    ],
    "Chummer.Avalonia/MainWindow.EventHandlers.cs": [
        'DesktopHomeWindow.ShowAsync(this, "avalonia")',
        'DesktopCampaignWorkspaceWindow.ShowAsync(this, "avalonia")',
        "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, DesktopHeadId)",
        "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, DesktopHeadId)",
        "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, DesktopHeadId, _adapter.State.LatestPortabilityActivity)",
    ],
    "Chummer.Avalonia/Controls/ToolStripControl.axaml": [
        'x:Name="RuleEnvironmentStudioButton"',
        'Content="Rule Studio"',
        "RuleEnvironmentStudioButton_OnClick",
    ],
    "Chummer.Avalonia/Controls/ToolStripControl.axaml.cs": [
        "public event EventHandler? RuleEnvironmentStudioRequested;",
        'SetButtonLabel(RuleEnvironmentStudioButton, "Open Rule Environment Studio");',
        "RuleEnvironmentStudioRequested?.Invoke(this, EventArgs.Empty);",
    ],
    "Chummer.Avalonia/MainWindow.ControlBinding.cs": [
        "EventHandler onRuleEnvironmentStudioRequested,",
        "AttachToolStripHandlers(toolStrip);",
        "AttachToolStripHandlers(classicToolStrip);",
        "surface.RuleEnvironmentStudioRequested += onRuleEnvironmentStudioRequested;",
    ],
    "Chummer.Avalonia/App.axaml.cs": [
        "DesktopStartupSurfaceCatalog.RuleEnvironmentStudio",
        'DesktopRuleEnvironmentStudioWindow.ShowAsync(owner, "avalonia")',
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "DesktopHomeWindow.ShowAsync(this, \\\"avalonia\\\")",
        "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)",
        "Import explain receipt:",
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

checks = {
    "registry_has_m114_ui_task": MILESTONE_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(MILESTONE_TASK_ANCHOR) == 1,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_package_id_matches": yaml_scalar(queue_block, "package_id") == PACKAGE_ID,
    "design_queue_package_id_matches": yaml_scalar(design_queue_block, "package_id") == PACKAGE_ID,
    "queue_work_task_matches": yaml_scalar(queue_block, "work_task_id") == WORK_TASK_ID,
    "design_queue_work_task_matches": yaml_scalar(design_queue_block, "work_task_id") == WORK_TASK_ID,
    "queue_milestone_matches": yaml_scalar(queue_block, "milestone_id") == str(MILESTONE_ID),
    "design_queue_milestone_matches": yaml_scalar(design_queue_block, "milestone_id") == str(MILESTONE_ID),
    "queue_title_matches": f"title: {TITLE}" in queue_block,
    "queue_task_matches": yaml_scalar_after(queue_block, "task") == TASK,
    "queue_status_complete": "status: complete" in queue_block,
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "design_queue_title_matches": f"title: {TITLE}" in design_queue_block,
    "design_queue_task_matches": yaml_scalar_after(design_queue_block, "task") == TASK,
    "design_queue_status_complete": "status: complete" in design_queue_block,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "queue_design_block_parity": normalize_whitespace(queue_block) == normalize_whitespace(design_queue_block),
    "design_queue_path_matches": str(design_queue_path) == EXPECTED_DESIGN_QUEUE_PATH,
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == "verify_closed_package_only",
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == "verify_closed_package_only",
    "queue_has_do_not_reopen_reason": "do_not_reopen_reason:" in queue_block,
    "design_queue_has_do_not_reopen_reason": "do_not_reopen_reason:" in design_queue_block,
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
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m114_ui_rule_studio",
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
        "proofCommands": {
            "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
            "targetedTestCommand": EXPECTED_TARGETED_TEST_COMMAND,
            "presentationTestCommand": EXPECTED_PRESENTATION_TEST_COMMAND,
        },
        "proofFiles": [
            str(receipt_path),
            f"{repo_root}/scripts/ai/milestones/next90-m114-ui-rule-studio-check.sh",
            f"{repo_root}/Chummer.Avalonia/DesktopHomeWindow.cs",
            f"{repo_root}/Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs",
            f"{repo_root}/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
            f"{repo_root}/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
            f"{repo_root}/Chummer.Avalonia/MainWindow.EventHandlers.cs",
            f"{repo_root}/Chummer.Avalonia/Controls/ToolStripControl.axaml",
            f"{repo_root}/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs",
            f"{repo_root}/Chummer.Avalonia/MainWindow.ControlBinding.cs",
            f"{repo_root}/Chummer.Avalonia/App.axaml.cs",
            f"{repo_root}/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
            f"{repo_root}/Chummer.Tests/Compliance/Next90M114RuleEnvironmentStudioGuardTests.cs",
        ],
    },
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
    raise SystemExit("next90-m114 rule-studio proof failed: " + "; ".join(failed))
PY
