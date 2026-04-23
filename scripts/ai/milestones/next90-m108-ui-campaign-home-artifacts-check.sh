#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M108_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M108_UI_CAMPAIGN_HOME_ARTIFACTS.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

PACKAGE_ID = "next90-m108-ui-campaign-home-artifacts"
WORK_TASK_ID = "108.3"
MILESTONE_ID = "108"
WAVE = "W10"
REPO = "chummer6-ui"
FRONTIER_ID = "1521004750"
TITLE = "Surface primer and briefing artifacts in campaign home on the primary desktop route"
TASK = "Launch primer and mission-briefing artifacts from the promoted desktop campaign home without browser ritual."
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M108 chummer6-ui campaign-home artifacts is complete; future shards must verify the desktop campaign-home "
    "launch surfaces, startup-surface routing, guard script, canonical registry row, and queue rows instead of "
    "reopening the primer/mission-briefing desktop package."
)
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_OWNED_SURFACES = [
    "campaign_home:artifact_launch",
    "mission_briefing:desktop",
]
REQUIRED_QUEUE_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer-presentation/.codex-studio/published/NEXT90_M108_UI_CAMPAIGN_HOME_ARTIFACTS.generated.json",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopHomeWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/App.axaml.cs",
    "/docker/chummercomplete/chummer-presentation/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs",
    "/docker/chummercomplete/chummer-presentation/scripts/ai/milestones/next90-m108-ui-campaign-home-artifacts-check.sh",
    "bash scripts/ai/milestones/next90-m108-ui-campaign-home-artifacts-check.sh",
]
REQUIRED_REGISTRY_EVIDENCE = [
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopHomeWindow.cs adds direct campaign primer and mission briefing actions to the promoted desktop campaign-home route and keeps the next-session return lane visible beside campaign continuity state.",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs keeps primer and mission briefing launch actions visible inside the native desktop campaign workspace without requiring the browser artifact shelf.",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/DesktopCampaignArtifactWindow.cs provides desktop-native campaign primer and mission briefing surfaces with workspace return, devices/access, and support follow-through actions.",
    "/docker/chummercomplete/chummer-presentation/Chummer.Avalonia/App.axaml.cs and /docker/chummercomplete/chummer-presentation/Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs route campaign_primer and mission_briefing directly into the promoted Avalonia desktop startup surface catalog.",
    "/docker/chummercomplete/chummer-presentation/.codex-studio/published/NEXT90_M108_UI_CAMPAIGN_HOME_ARTIFACTS.generated.json records canonical registry/queue closure and repo-local desktop launch markers for this package.",
    "bash scripts/ai/milestones/next90-m108-ui-campaign-home-artifacts-check.sh exited 0 on 2026-04-23.",
]
DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "operator telemetry",
    "active-run helper",
    "active-run helper command",
    "active-run helper commands",
    "run-helper",
    "supervisor status",
    "status helper",
    "prompt.txt",
]

LANDING_COMMIT_PATTERN = re.compile(r"landed_commit:\s*([0-9a-f]+)")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"[next90-m108] FAIL: {message}")


def read_text(path: Path) -> str:
    require(path.is_file(), f"missing authority file: {path}")
    return path.read_text(encoding="utf-8-sig")


def extract_block(text: str, start_marker: str, next_marker: str) -> str:
    start = text.find(start_marker)
    require(start >= 0, f"missing block marker: {start_marker}")
    end = text.find(next_marker, start + len(start_marker))
    return text[start:] if end < 0 else text[start:end]


def require_contains(block: str, needle: str, label: str) -> bool:
    require(needle in block, f"{label} missing marker: {needle}")
    return True


def reject_worker_unsafe_proof(text: str, label: str) -> None:
    lowered = text.lower()
    for token in DISALLOWED_PROOF_TOKENS:
        require(token.lower() not in lowered, f"{label} must not cite worker-unsafe proof token: {token}")


def parse_landed_commit(block: str, label: str) -> str:
    match = LANDING_COMMIT_PATTERN.search(block)
    require(match is not None, f"{label} missing landed_commit")
    return match.group(1)


def git_commit_exists(commit: str) -> bool:
    result = subprocess.run(
        ["git", "cat-file", "-e", f"{commit}^{{commit}}"],
        cwd=repo_root,
        check=False,
        capture_output=True,
        text=True,
    )
    return result.returncode == 0


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)

registry_block = extract_block(registry_text, "      - id: 108.3", "      - id: 108.4")
queue_block = extract_block(queue_text, f"    package_id: {PACKAGE_ID}", "  - title:")
design_queue_block = extract_block(design_queue_text, f"    package_id: {PACKAGE_ID}", "  - title:")

reject_worker_unsafe_proof(registry_block, "registry block")
reject_worker_unsafe_proof(queue_block, "fleet queue block")
reject_worker_unsafe_proof(design_queue_block, "design queue block")

registry_checks = {
    "task_present": require_contains(registry_block, "      - id: 108.3", "registry"),
    "owner_matches": require_contains(registry_block, "owner: chummer6-ui", "registry"),
    "title_matches": require_contains(registry_block, f"title: {TITLE}.", "registry"),
    "status_complete": require_contains(registry_block, "status: complete", "registry"),
}

queue_checks = {
    "package_present": require_contains(queue_block, f"package_id: {PACKAGE_ID}", "fleet queue"),
    "title_matches": require_contains(queue_block, f"title: {TITLE}", "fleet queue"),
    "task_matches": require_contains(queue_block, f"task: {TASK}", "fleet queue"),
    "frontier_matches": require_contains(queue_block, f"frontier_id: {FRONTIER_ID}", "fleet queue"),
    "milestone_matches": require_contains(queue_block, f"milestone_id: {MILESTONE_ID}", "fleet queue"),
    "wave_matches": require_contains(queue_block, f"wave: {WAVE}", "fleet queue"),
    "repo_matches": require_contains(queue_block, f"repo: {REPO}", "fleet queue"),
    "status_complete": require_contains(queue_block, "status: complete", "fleet queue"),
    "completion_action_verify_closed_package_only": require_contains(queue_block, "completion_action: verify_closed_package_only", "fleet queue"),
    "do_not_reopen_reason_matches": require_contains(queue_block, f"do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}", "fleet queue"),
}

design_queue_checks = {
    "package_present": require_contains(design_queue_block, f"package_id: {PACKAGE_ID}", "design queue"),
    "title_matches": require_contains(design_queue_block, f"title: {TITLE}", "design queue"),
    "task_matches": require_contains(design_queue_block, f"task: {TASK}", "design queue"),
    "frontier_matches": require_contains(design_queue_block, f"frontier_id: {FRONTIER_ID}", "design queue"),
    "milestone_matches": require_contains(design_queue_block, f"milestone_id: {MILESTONE_ID}", "design queue"),
    "wave_matches": require_contains(design_queue_block, f"wave: {WAVE}", "design queue"),
    "repo_matches": require_contains(design_queue_block, f"repo: {REPO}", "design queue"),
    "status_complete": require_contains(design_queue_block, "status: complete", "design queue"),
    "completion_action_verify_closed_package_only": require_contains(design_queue_block, "completion_action: verify_closed_package_only", "design queue"),
    "do_not_reopen_reason_matches": require_contains(design_queue_block, f"do_not_reopen_reason: {EXPECTED_DO_NOT_REOPEN_REASON}", "design queue"),
}

for allowed_path in EXPECTED_ALLOWED_PATHS:
    queue_checks[f"allowed_path_{allowed_path}"] = require_contains(queue_block, f"      - {allowed_path}", "fleet queue")
    design_queue_checks[f"allowed_path_{allowed_path}"] = require_contains(design_queue_block, f"      - {allowed_path}", "design queue")

for owned_surface in EXPECTED_OWNED_SURFACES:
    queue_checks[f"owned_surface_{owned_surface}"] = require_contains(queue_block, f"      - {owned_surface}", "fleet queue")
    design_queue_checks[f"owned_surface_{owned_surface}"] = require_contains(design_queue_block, f"      - {owned_surface}", "design queue")

for proof_item in REQUIRED_QUEUE_PROOF_ITEMS:
    queue_checks[f"proof_{proof_item}"] = require_contains(queue_block, proof_item, "fleet queue")
    design_queue_checks[f"proof_{proof_item}"] = require_contains(design_queue_block, proof_item, "design queue")

for evidence_line in REQUIRED_REGISTRY_EVIDENCE:
    registry_checks[f"evidence_{len(registry_checks)}"] = require_contains(registry_block, evidence_line, "registry")

queue_landed_commit = parse_landed_commit(queue_block, "fleet queue")
design_queue_landed_commit = parse_landed_commit(design_queue_block, "design queue")
registry_landed_commit = parse_landed_commit(registry_block, "registry")

require(queue_landed_commit == design_queue_landed_commit == registry_landed_commit, "canonical landed_commit values must agree")
require(git_commit_exists(queue_landed_commit), f"landed commit {queue_landed_commit} must resolve in the repo")

desktop_home_path = repo_root / "Chummer.Avalonia" / "DesktopHomeWindow.cs"
campaign_workspace_path = repo_root / "Chummer.Avalonia" / "DesktopCampaignWorkspaceWindow.cs"
artifact_window_path = repo_root / "Chummer.Avalonia" / "DesktopCampaignArtifactWindow.cs"
app_path = repo_root / "Chummer.Avalonia" / "App.axaml.cs"
startup_surface_catalog_path = repo_root / "Chummer.Desktop.Runtime" / "DesktopStartupSurfaceCatalog.cs"

desktop_home = read_text(desktop_home_path)
campaign_workspace = read_text(campaign_workspace_path)
artifact_window = read_text(artifact_window_path)
app_source = read_text(app_path)
startup_surface_catalog = read_text(startup_surface_catalog_path)

for token in ("TASK_LOCAL_TELEMETRY.generated.json", "ACTIVE_RUN_HANDOFF.generated.md"):
    require(token not in desktop_home, f"desktop home must not cite worker-local helper evidence: {token}")
    require(token not in campaign_workspace, f"campaign workspace must not cite worker-local helper evidence: {token}")
    require(token not in artifact_window, f"artifact window must not cite worker-local helper evidence: {token}")

local_repo_checks = {
    "desktop_home_campaign_artifact_summary": require_contains(desktop_home, "CampaignArtifactLaunchSummary", "desktop home"),
    "desktop_home_primer_button": require_contains(desktop_home, "desktop.home.button.open_campaign_primer", "desktop home"),
    "desktop_home_briefing_button": require_contains(desktop_home, "desktop.home.button.open_mission_briefing", "desktop home"),
    "desktop_home_primer_launch": require_contains(desktop_home, "DesktopCampaignArtifactWindow.ShowPrimerAsync(", "desktop home"),
    "desktop_home_briefing_launch": require_contains(desktop_home, "DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(", "desktop home"),
    "campaign_workspace_summary": require_contains(campaign_workspace, "CampaignArtifactLaunchSummary", "campaign workspace"),
    "campaign_workspace_primer_launch": require_contains(campaign_workspace, "DesktopCampaignArtifactWindow.ShowPrimerAsync(", "campaign workspace"),
    "campaign_workspace_briefing_launch": require_contains(campaign_workspace, "DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(", "campaign workspace"),
    "artifact_window_primer_surface": require_contains(artifact_window, 'public static Task ShowPrimerAsync(Window owner, string headId)', "artifact window"),
    "artifact_window_briefing_surface": require_contains(artifact_window, 'public static Task ShowMissionBriefingAsync(Window owner, string headId)', "artifact window"),
    "artifact_window_workspace_return": require_contains(artifact_window, "Open Campaign Workspace", "artifact window"),
    "startup_surface_primer": require_contains(startup_surface_catalog, 'public const string CampaignPrimer = "campaign_primer";', "startup surface catalog"),
    "startup_surface_briefing": require_contains(startup_surface_catalog, 'public const string MissionBriefing = "mission_briefing";', "startup surface catalog"),
    "app_startup_primer_route": require_contains(app_source, "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.CampaignPrimer)", "app startup"),
    "app_startup_briefing_route": require_contains(app_source, "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.MissionBriefing)", "app startup"),
}

receipt = {
    "contract_name": "chummer6-ui.next90_m108_ui_campaign_home_artifacts",
    "status": "pass",
    "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "summary": "M108 desktop campaign-home artifact launch is canonically closed and locally guarded against browser-ritual regressions.",
    "evidence": {
        "packageId": PACKAGE_ID,
        "workTaskId": WORK_TASK_ID,
        "milestoneId": MILESTONE_ID,
        "wave": WAVE,
        "repo": REPO,
        "frontierId": FRONTIER_ID,
        "landedCommit": queue_landed_commit,
        "title": TITLE,
        "task": TASK,
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_OWNED_SURFACES,
        "directProofCommand": "bash scripts/ai/milestones/next90-m108-ui-campaign-home-artifacts-check.sh",
        "queueChecks": queue_checks,
        "designQueueChecks": design_queue_checks,
        "registryChecks": registry_checks,
        "localRepoChecks": local_repo_checks,
        "closureGuard": {
            "status": "closed_and_verified",
            "canonicalRegistryComplete": True,
            "canonicalQueueComplete": True,
            "designQueueComplete": True,
            "doNotReopenReasonPinned": True,
            "reason": (
                f"Canonical registry and both successor queue mirrors mark the M108 desktop campaign-home artifact "
                f"package complete at {queue_landed_commit}; rerun this verifier instead of reopening the closed slice."
            ),
        },
    },
    "unresolved": [],
}

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

print(
    "[next90-m108] PASS: "
    f"{PACKAGE_ID} ({WORK_TASK_ID}, milestone {MILESTONE_ID}, wave {WAVE}, repo {REPO}) "
    "keeps campaign-home artifact launch wired on the promoted desktop route and closed in canonical registry/queue proof."
)
PY
