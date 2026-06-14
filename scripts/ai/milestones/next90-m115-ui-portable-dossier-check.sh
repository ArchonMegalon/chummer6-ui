#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M115_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M115_UI_PORTABLE_DOSSIER.generated.json}"

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

PACKAGE_ID = "next90-m115-ui-portable-dossier"
TITLE = "Surface exchange, replay, and portability actions on desktop"
TASK = "Surface exchange, replay, and portability actions on desktop."
MILESTONE_ID = 115
WORK_TASK_ID = "115.4"
WAVE = "W12"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M115 chummer6-ui portable dossier and exchange actions are complete; future shards must verify this "
    "receipt, registry row, queue row, and design-queue row instead of reopening the desktop exchange and portability "
    "surface package."
)
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "exchange_replay_ui",
    "portable_dossier_export_ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m115-ui-portable-dossier-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M115PortableDossierGuardTests" --no-restore'
EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter "AccessibilitySignoffSmokeTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
MILESTONE_TASK_ANCHOR = """- id: 115.4
        owner: chummer6-ui
        title: Surface export, replay, recap, and federation actions on the primary desktop route."""

SOURCE_MARKERS = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": [
        "ResolveLeadCampaignId",
        "ReadPortableExchangePreviewAsync",
        "OpenPortableExchangeAsync()",
        "OpenReplayAfterActionAsync()",
        "/artifacts/replay-after-action",
    ],
    "Chummer.Avalonia/DesktopCampaignArtifactWindow.cs": [
        "_portableExchangePreview",
        "ResolveLeadCampaignId",
        "ReadPortableExchangePreviewAsync",
        '"Review Portable Exchange"',
        '"Open Replay After Action"',
        '"Open Portable Export"',
        "OpenPortableExchangeAsync()",
        "OpenReplayAfterActionAsync()",
        "OpenPortableExportAsync()",
        "OpenWorkspaceCommandFromDesktopSurfaceAsync",
        "/artifacts/replay-after-action",
        "#portable-exchange",
    ],
    "Chummer.Avalonia/DesktopCreatorPublicationWindow.cs": [
        "_portableExchangePreview",
        "ResolveLeadCampaignId",
        "ReadPortableExchangePreviewAsync",
        '"Review Portable Exchange"',
        '"Open Portable Export"',
        "OpenPortableExchangeAsync()",
        "OpenPortableExportAsync()",
        "OpenWorkspaceCommandFromDesktopSurfaceAsync",
        "#portable-exchange",
    ],
    "Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs": [
        "_portableExchangePreview",
        "ResolveLeadCampaignId",
        "ReadPortableExchangePreviewAsync",
        '"Review Portable Exchange"',
        "OpenPortableExchangeAsync()",
        "#portable-exchange",
    ],
    "Chummer.Avalonia/MainWindow.DesktopSurfaceNavigation.cs": [
        "OpenWorkspaceCommandFromDesktopSurfaceAsync",
        "_interactionCoordinator.SwitchWorkspaceAsync",
        "_interactionCoordinator.ExecuteCommandAsync",
    ],
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": [
        "ReadPortableExchangePreviewAsync",
        "Portable exchange:",
        "Exchange context:",
        "Exchange asset scope:",
        "GetPortableExchangePreviewAsync",
        "DesktopCreatorPublicationWindow.ShowAsync(",
    ],
}
EXPECTED_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopHomeWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCreatorPublicationWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/MainWindow.DesktopSurfaceNavigation.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Tests/Compliance/Next90M115PortableDossierGuardTests.cs",
    "/docker/chummercomplete/chummer-presentation/scripts/ai/milestones/next90-m115-ui-portable-dossier-check.sh",
    "/docker/chummercomplete/chummer-presentation/.codex-studio/published/NEXT90_M115_UI_PORTABLE_DOSSIER.generated.json",
]
DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "operator telemetry",
    "active-run helper",
    "supervisor status",
    "prompt.txt",
]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def normalize_whitespace(value: str) -> str:
    return " ".join(value.split())


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
            continuation_text = continuation.strip()
            if continuation_text.startswith("title:") or continuation_text.startswith("task:"):
                break
            values.append(continuation_text)
        return normalize_whitespace(" ".join(value for value in values if value).strip().strip("'\""))

    raise AssertionError(f"missing {key}")


def reject_worker_unsafe_proof(block: str, label: str) -> list[str]:
    failures: list[str] = []
    lowered = block.lower()
    for token in DISALLOWED_PROOF_TOKENS:
        if token.lower() in lowered:
            failures.append(f"{label}:blocked_token:{token}")
    return failures


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
        stripped = line.lstrip(" ")
        if stripped.startswith("- title:"):
            break
        if stripped.startswith("- "):
            items.append(stripped.removeprefix("- ").strip())
            continue
        if items:
            break
        if line and not line.startswith(" "):
            break
    return items


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)

checks = {
    "registry_has_m115_ui_task": MILESTONE_TASK_ANCHOR in registry_text,
    "registry_task_unique": registry_text.count(MILESTONE_TASK_ANCHOR) == 1,
    "queue_package_unique": queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "design_queue_package_unique": design_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "queue_title_matches": yaml_scalar_after(queue_block, "title") == TITLE,
    "queue_task_matches": yaml_scalar_after(queue_block, "task") == TASK,
    "queue_status_complete": yaml_scalar_after(queue_block, "status") == "complete",
    "queue_completion_action_matches": yaml_scalar_after(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_scalar_after(queue_block, "do_not_reopen_reason") == normalize_whitespace(EXPECTED_DO_NOT_REOPEN_REASON),
    "design_queue_title_matches": yaml_scalar_after(design_queue_block, "title") == TITLE,
    "design_queue_task_matches": yaml_scalar_after(design_queue_block, "task") == TASK,
    "design_queue_status_complete": yaml_scalar_after(design_queue_block, "status") == "complete",
    "design_queue_completion_action_matches": yaml_scalar_after(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_do_not_reopen_reason_matches": yaml_scalar_after(design_queue_block, "do_not_reopen_reason")
    == normalize_whitespace(EXPECTED_DO_NOT_REOPEN_REASON),
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "proof_items_exact": yaml_list_after(queue_block, "proof") == EXPECTED_PROOF_ITEMS,
    "design_proof_items_exact": yaml_list_after(design_queue_block, "proof") == EXPECTED_PROOF_ITEMS,
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
failed.extend(reject_worker_unsafe_proof(queue_block, "queue"))
failed.extend(reject_worker_unsafe_proof(design_queue_block, "design_queue"))

receipt = {
    "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m115_ui_portable_dossier",
    "evidence": {
        "packageId": PACKAGE_ID,
        "title": TITLE,
        "task": TASK,
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
    raise SystemExit("next90-m115 portable dossier proof failed: " + "; ".join(failed))
PY
