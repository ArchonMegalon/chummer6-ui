#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M145_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json}"

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

PACKAGE_ID = "next90-m145-ui-desktop-explain-drawer-and-follow-up"
TITLE = "Wire the desktop explain drawer, source-anchor launch, stale-state handling, and text-first follow-up on promoted workbench routes."
TASK = "Wire packet-backed desktop explain drawers, source-anchor affordances, stale snapshot handling, and text-first bounded follow-up across promoted workbench routes."
FRONTIER_ID = 1452045202
MILESTONE_ID = 145
WORK_TASK_ID = "145.2"
WAVE = "W28"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "explain_every_value_drawer:ui",
    "grounded_follow_up:desktop",
]
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M145 chummer6-ui desktop explain drawer and follow-up is complete; future shards must verify this receipt, registry row, "
    "queue row, design-queue row, and focused desktop tests instead of reopening the promoted workbench explain drawer package."
)
EXPECTED_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopExplainDrawerFollowUpWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/MainWindow.SelectionHandlers.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/MainWindow.FeedbackCoordinator.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Tests/Compliance/Next90M145DesktopExplainDrawerGuardTests.cs",
    "/docker/chummercomplete/chummer-presentation/scripts/ai/milestones/next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh",
    "/docker/chummercomplete/chummer-presentation/.codex-studio/published/NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json",
    "bash scripts/ai/milestones/next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M145DesktopExplainDrawerGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Standalone_section_context_reads_canonical_explanation_packet_fields_for_text_first_drawer_copy|FullyQualifiedName~Standalone_section_context_projects_packet_backed_explain_drawer_actions_for_desktop_launch_and_follow_up|FullyQualifiedName~Standalone_section_context_launches_source_anchor_from_packet_backed_explain_drawer" --no-restore -p:BuildProjectReferences=false'
REGISTRY_TASK_ANCHOR = """- id: '145.2'
      owner: chummer6-ui
      title: Wire the desktop explain drawer, source-anchor launch, stale-state handling, and text-first follow-up on promoted workbench routes."""
DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "operator telemetry",
    "active-run helper",
    "active-run helper command",
    "active-run helper commands",
    "supervisor status",
    "status helper",
    "prompt.txt",
]

SOURCE_MARKERS = {
    "Chummer.Avalonia/Controls/SectionHostControl.axaml.cs": [
        "ExplainDrawerOpenSourceAnchorActionId",
        "ExplainDrawerReviewBoundedFollowUpActionId",
        "_currentExplainDrawerContext = ReadExplainDrawerContext(TryParseRootObject(previewJson));",
        "ReadExplainDrawerContext(root);",
        'return "Open the bound local rulebook anchor from this desktop route.";',
        'return "Open the cited source anchor from this desktop route.";',
        'return $"Packet snapshot {packetSnapshot} no longer matches current snapshot {currentSnapshot}. Refresh before trusting this value.";',
        'renderedActions.Add(new SectionQuickActionDisplayItem(',
        "TryOpenExplainDrawerSourceAnchor()",
    ],
    "Chummer.Avalonia/DesktopExplainDrawerFollowUpWindow.cs": [
        'Title = "Explain Follow-up";',
        "Follow-up stays text-first, packet-backed, and scoped to the current desktop snapshot.",
        'CreateSection("Stale-state posture", FirstNonBlank(_context.StaleState, "No stale-state warning is attached to this packet."))',
        'CreateSection("Bounded follow-up", FirstNonBlank(_context.FollowUp, "No bounded follow-up is attached to this packet."))',
        'actions.Insert(0, CreateButton("Open Source Anchor", OpenSourceAnchorAsync));',
        '_statusText.Text = $"Opened source anchor for {_context.ExplainPacket}.";',
    ],
    "Chummer.Avalonia/MainWindow.SelectionHandlers.cs": [
        "TryHandleExplainDrawerQuickActionAsync",
        'ExplainDrawerContext? explainContext = _controls.SectionHost.GetCurrentExplainDrawerContext();',
        "DesktopExplainDrawerFollowUpWindow.ShowAsync(this, explainContext)",
        "MainWindowFeedbackCoordinator.ShowExplainFollowUpReviewed(_controls.ToolStrip);",
        "MainWindowFeedbackCoordinator.ShowExplainFollowUpUnavailable(_controls.ToolStrip);",
    ],
    "Chummer.Avalonia/MainWindow.FeedbackCoordinator.cs": [
        "ShowExplainFollowUpReviewed",
        "ShowExplainFollowUpUnavailable",
        "Explain follow-up reviewed.",
        "Explain follow-up is unavailable for the current selection.",
    ],
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        "Standalone_section_context_surfaces_text_first_explain_drawer_summary_when_packet_metadata_is_present",
        "Standalone_section_context_reads_canonical_explanation_packet_fields_for_text_first_drawer_copy",
        "Standalone_section_context_projects_packet_backed_explain_drawer_actions_for_desktop_launch_and_follow_up",
        "Standalone_section_context_launches_source_anchor_from_packet_backed_explain_drawer",
        "Main_window_review_bounded_follow_up_opens_text_first_desktop_follow_up_window",
    ],
    "Chummer.Tests/Compliance/Next90M145DesktopExplainDrawerGuardTests.cs": [
        "M145_desktop_explain_drawer_guard_is_wired_into_standard_verify",
        "NEXT90_M145_UI_DESKTOP_EXPLAIN_DRAWER_AND_FOLLOW_UP.generated.json",
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M145 desktop explain drawer and bounded follow-up guard",
        "bash scripts/ai/milestones/next90-m145-ui-desktop-explain-drawer-and-follow-up-check.sh",
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


def normalize_whitespace(value: str) -> str:
    return " ".join(value.split())


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


def reject_worker_unsafe_proof(block: str, label: str) -> list[str]:
    failures: list[str] = []
    lowered = block.lower()
    for token in DISALLOWED_PROOF_TOKENS:
        if token.lower() in lowered:
            failures.append(f"{label}:blocked_token:{token}")
    return failures


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_task_present": REGISTRY_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(REGISTRY_TASK_ANCHOR) == 1,
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
    "queue_status_complete": yaml_scalar(queue_block, "status") == "complete",
    "design_queue_status_complete": yaml_scalar(design_queue_block, "status") == "complete",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "design_queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(design_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "proof_items_exact": yaml_list_after(queue_block, "proof") == EXPECTED_PROOF_ITEMS,
    "design_proof_items_exact": yaml_list_after(design_queue_block, "proof") == EXPECTED_PROOF_ITEMS,
    "queue_design_block_parity": normalize_whitespace(queue_block) == normalize_whitespace(design_queue_block),
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

failed.extend(reject_worker_unsafe_proof(queue_block, "queue"))
failed.extend(reject_worker_unsafe_proof(design_queue_block, "design_queue"))

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m145_ui_desktop_explain_drawer_and_follow_up",
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
        "proofFiles": EXPECTED_PROOF_ITEMS,
    },
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if failed:
    raise SystemExit("next90-m145 desktop explain drawer proof failed: " + "; ".join(failed))
PY
