#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M105_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

PACKAGE_ID = "next90-m105-ui-restore-continuity"
MILESTONE_ID = 105
WAVE = "W8"
FRONTIER_ID = 3787618287
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "restore_continuation:desktop",
    "conflict_safe_workspace:desktop",
]
EXPECTED_LANDED_COMMIT = "54c27661"
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
STANDARD_VERIFY_PATH = "scripts/ai/verify.sh"
EXPECTED_STANDARD_VERIFY_MARKERS = [
    "checking next-90 M105 restore-continuity and conflict-safe desktop UX guard",
    "bash scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh",
]
REQUIRED_REGISTRY_EVIDENCE_LINES = [
    "/docker/chummercomplete/chummer6-ui-finish commit 54c27661 tightens the M105 restore continuity verifier and receipt so future shards close against registry, queue, source-marker, and support-handoff truth together.",
    "/docker/chummercomplete/chummer6-ui-finish commit 49036853 adds the follow-on M105 closure guard so future successor shards verify this package instead of reopening the already complete desktop slice.",
    "/docker/chummercomplete/chummer6-ui-finish commit c0960ae9 pins the M105 closure guard in Chummer.Tests so the completed desktop slice cannot lose package authority, source-marker, support-handoff, or receipt proof silently.",
    "/docker/chummercomplete/chummer6-ui-finish commit 6b2649d2 pins exact queue allowed-path and owned-surface authority in the M105 verifier, receipt, and compliance guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit fd0fbfd0 tightens the M105 completed queue action guard so Fleet and design queue rows must carry verify_closed_package_only plus the package-specific do-not-reopen reason.",
    "/docker/chummercomplete/chummer6-ui-finish commit 6a873199 tightens the M105 primary-route decision gate proof so canonical closure evidence cites the separate restore, stale-state, and conflict-choice visibility guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit db9ec722 tightens the M105 workspace-support handoff proof so canonical closure evidence cites the primary-route support fallback guard.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopHomeWindow.cs keeps restore continuation, stale-state visibility, conflict choices, devices/access, workspace support, and native support fallback visible on the Avalonia desktop home route.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs keeps the same restore, stale-state, conflict-choice, and support fallback posture visible inside the primary campaign workspace route.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs pre-fills install and workspace support packets with restore, stale-state, and conflict-choice context.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs locks the install and workspace support prefill regression paths so restore, stale-state, and conflict-choice context stays in support handoff URLs.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs locks the visible restore-continuation and conflict-safe desktop source markers.",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M105RestoreContinuityGuardTests.cs pins the registry, queue, verifier, receipt, desktop marker, and support-handoff guardrails for the already-complete package.",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh fail-closes registry drift, queue drift, missing allowed-path/surface authority, missing desktop source markers, and missing support handoff proof.",
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json reports status=pass with packageId=next90-m105-ui-restore-continuity and frontierId=3787618287.",
    "bash scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh exits 0.",
]
REQUIRED_QUEUE_PROOF_LINES = [
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish commit 54c27661",
    "/docker/chummercomplete/chummer6-ui-finish commit 49036853",
    "/docker/chummercomplete/chummer6-ui-finish commit c0960ae9",
    "/docker/chummercomplete/chummer6-ui-finish commit 6b2649d2",
    "/docker/chummercomplete/chummer6-ui-finish commit fd0fbfd0 tightens the M105 completed queue action guard",
    "/docker/chummercomplete/chummer6-ui-finish commit 6a873199 tightens the M105 primary-route decision gate proof",
    "/docker/chummercomplete/chummer6-ui-finish commit db9ec722 tightens the M105 workspace-support handoff proof",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopHomeWindow.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M105RestoreContinuityGuardTests.cs",
    "bash scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh",
]
DISALLOWED_ACTIVE_RUN_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "scripts/ooda_design_supervisor.py",
    "scripts/run_ooda_design_supervisor_until_quiet.py",
    "operator telemetry",
    "active-run helper",
]
DISALLOWED_SIBLING_PACKAGE_PROOF_TOKENS = [
    "next90-m101-ui-release-train",
    "next90-m103-ui-veteran-certification",
    "next90-m104-ui-explain-receipts",
    "next90-m106",
    "desktop_release_train:avalonia",
    "veteran_certification:desktop",
    "explain_receipts:desktop",
    "operator-ready",
    "reporter-ready",
]
PROOF_RELEVANT_PATHS = [
    "Chummer.Avalonia/DesktopHomeWindow.cs",
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
    "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml",
    "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs",
    "Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs",
    "Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs",
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs",
    "Chummer.Tests/Compliance/Next90M105RestoreContinuityGuardTests.cs",
    "scripts/ai/milestones/next90-m105-ui-restore-continuity-check.sh",
    "scripts/ai/verify.sh",
    ".codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json",
]
COMMIT_SCOPE_ALLOWED_PREFIXES = [
    *EXPECTED_ALLOWED_PATHS,
    ".codex-studio/published/NEXT90_M105_UI_RESTORE_CONTINUITY.generated.json",
]

SOURCE_MARKERS: dict[str, dict[str, list[str]]] = {
    "Chummer.Avalonia/DesktopHomeWindow.cs": {
        "restore_continuation": [
            "BuildCampaignRestoreContinuitySummary()",
            "Restore choice: open the current campaign workspace",
            "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)",
        ],
        "stale_state_visibility": [
            "Stale state: server continuity is unavailable",
            "ReadCampaignWorkspaceServerPlaneAsync",
            "GetCampaignWorkspaceServerPlaneAsync",
        ],
        "conflict_safe_workspace": [
            "Conflict choices:",
            "_campaignProjection.Watchouts",
            "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())",
            "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)",
        ],
        "claimed_install_support": [
            "open install support if entitlement or stale-state posture is wrong",
            "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)",
        ],
    },
    "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs": {
        "restore_continuation": [
            "BuildRestoreContinuityChoiceSummary()",
            "Restore choice: open the current campaign workspace",
            "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)",
        ],
        "stale_state_visibility": [
            "Stale state: server continuity is unavailable",
            "ReadCampaignWorkspaceServerPlaneAsync",
            "GetCampaignWorkspaceServerPlaneAsync",
        ],
        "conflict_safe_workspace": [
            "Conflict choices:",
            "Review before continuing:",
            "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())",
            "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)",
        ],
        "support_fallback": [
            "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)",
            "Support choice: open the tracked case",
        ],
    },
    "Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs": {
        "support_prefill": [
            "Restore posture: review workspace continuation, stale-state visibility, and conflict choices before replacing local work.",
            "Restore posture: review claimed-install entitlement, stale-state visibility, and conflict choices before restoring workspace continuity.",
            "Stale-state visibility: keep the local workspace visible until support confirms the current continuity packet.",
            "Conflict choices: keep local work, save local work, or review Campaign Workspace before accepting restore replacement.",
            "BuildSupportPortalRelativePathForWorkspace",
            "BuildSupportPortalRelativePathForInstall",
        ],
    },
    "Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs": {
        "support_prefill_regression": [
            "BuildSupportPortalRelativePathForInstall_includes_install_prefill_context",
            "Restore%20posture%3A%20review%20claimed-install%20entitlement%2C%20stale-state%20visibility%2C%20and%20conflict%20choices%20before%20restoring%20workspace%20continuity.",
            "BuildSupportPortalRelativePathForWorkspace_includes_workspace_follow_through_context",
            "Restore%20posture%3A%20review%20workspace%20continuation%2C%20stale-state%20visibility%2C%20and%20conflict%20choices%20before%20replacing%20local%20work.",
        ],
    },
    "Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs": {
        "targeted_signoff": [
            "BuildCampaignRestoreContinuitySummary()",
            "BuildRestoreContinuityChoiceSummary()",
            "Stale state: server continuity is unavailable",
            "Conflict choices:",
            "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())",
        ],
    },
    "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml": {
        "primary_route_restore_decision_gate": [
            "RestoreContinuityStatusBorder",
            "RestoreContinuityDecisionText",
            "Keep Local Work",
            "Save Local Work",
            "Review Campaign Workspace",
            "Workspace Support",
        ],
    },
    "Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs": {
        "primary_route_restore_decision_gate": [
            "Primary route: Avalonia desktop keeps restore continuation, stale state, and conflict choices visible before any replacement.",
            "BuildRestoreContinuityDecisionSummary",
            "Decision gate: Chummer will not replace local work automatically",
            "restore-decision-keep-local-work",
            "restore-decision-review-campaign-workspace",
            "restore-decision-open-workspace-support",
            "ToolTip.SetTip(KeepLocalWorkButton",
            "ToolTip.SetTip(SaveLocalWorkButton",
            "Save local work is unavailable because no dirty local workspace is active",
        ],
    },
}


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def read_text(path: Path, reasons: list[str], label: str) -> str:
    if not path.is_file():
        reasons.append(f"{label} is missing: {path}")
        return ""
    return path.read_text(encoding="utf-8-sig")


def line_number(text: str, marker: str) -> int | None:
    index = text.find(marker)
    if index < 0:
        return None
    return text.count("\n", 0, index) + 1


def text_block_contains(text: str, anchors: list[str]) -> bool:
    position = 0
    for anchor in anchors:
        next_position = text.find(anchor, position)
        if next_position < 0:
            return False
        position = next_position + len(anchor)
    return True


def queue_item_block(queue_text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    index = queue_text.find(marker)
    if index < 0:
        return ""
    start = queue_text.rfind("\n  - ", 0, index)
    if start < 0:
        start = 0
    end = queue_text.find("\n  - ", index)
    if end < 0:
        end = len(queue_text)
    return queue_text[start:end]


def yaml_list_after(block: str, key: str) -> list[str]:
    marker = f"{key}:"
    index = block.find(marker)
    if index < 0:
        return []
    result: list[str] = []
    for raw_line in block[index + len(marker):].splitlines():
        if not raw_line.startswith("      - "):
            if result:
                break
            if raw_line and not raw_line.startswith(" "):
                break
            continue
        result.append(raw_line.removeprefix("      - ").strip())
    return result


def registry_task_block(registry_text: str) -> str:
    marker = "id: 105.2\n        owner: chummer6-ui"
    index = registry_text.find(marker)
    if index < 0:
        return ""
    start = registry_text.rfind("\n      - ", 0, index)
    if start < 0:
        start = index
    end = registry_text.find("\n      - ", index + len(marker))
    if end < 0:
        end = len(registry_text)
    return registry_text[start:end]


def comparable_receipt(payload: dict[str, Any]) -> dict[str, Any]:
    comparable = dict(payload)
    comparable.pop("generatedAt", None)
    return comparable


def git_output(args: list[str], *, check: bool = False) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=repo_root,
        check=check,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def git_success(args: list[str]) -> bool:
    return git_output(args).returncode == 0


def path_is_in_allowed_commit_scope(relative_path: str) -> bool:
    return any(
        relative_path == prefix or relative_path.startswith(f"{prefix}/")
        for prefix in COMMIT_SCOPE_ALLOWED_PREFIXES
    )


def disallowed_active_run_proof_tokens(text: str) -> list[str]:
    lowered = text.lower()
    return [token for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS if token.lower() in lowered]


def disallowed_sibling_package_proof_tokens(text: str) -> list[str]:
    lowered = text.lower()
    return [token for token in DISALLOWED_SIBLING_PACKAGE_PROOF_TOKENS if token.lower() in lowered]


def latest_commit_for_path(relative_path: str) -> dict[str, Any]:
    log_result = git_output(["log", "--format=%H%x09%s", "--", relative_path])
    if log_result.returncode != 0 or not log_result.stdout.strip():
        return {
            "path": relative_path,
            "exists": False,
            "commit": "",
            "subject": "",
            "sha": "",
            "paths": [],
            "pathAllowed": path_is_in_allowed_commit_scope(relative_path),
            "scopeAllowed": False,
            "isAncestorOfHead": False,
        }

    candidate_commits: list[tuple[str, str]] = []
    for raw_line in log_result.stdout.splitlines():
        if not raw_line.strip():
            continue
        commit, _, subject = raw_line.partition("\t")
        candidate_commits.append((commit, subject))

    selected_commit = ""
    selected_subject = ""
    selected_paths: list[str] = []
    selected_disallowed_paths: list[str] = []
    for commit, subject in candidate_commits:
        diff_result = git_output(["show", "--format=", "--name-only", commit, "--", relative_path])
        changed_paths = [line for line in diff_result.stdout.splitlines() if line]
        disallowed_paths = [path for path in changed_paths if not path_is_in_allowed_commit_scope(path)]
        if not disallowed_paths:
            selected_commit = commit
            selected_subject = subject
            selected_paths = changed_paths
            selected_disallowed_paths = []
            break
        if not selected_commit:
            selected_commit = commit
            selected_subject = subject
            selected_paths = changed_paths
            selected_disallowed_paths = disallowed_paths

    return {
        "path": relative_path,
        "pathAllowed": path_is_in_allowed_commit_scope(relative_path),
        "exists": True,
        "commit": selected_commit[:8],
        "subject": selected_subject,
        "sha": selected_commit,
        "paths": selected_paths,
        "scopeAllowed": not selected_disallowed_paths,
        "disallowedPaths": selected_disallowed_paths,
        "isAncestorOfHead": git_success(["merge-base", "--is-ancestor", selected_commit, "HEAD"]),
    }


def relevant_worktree_status(relative_paths: list[str]) -> list[dict[str, str]]:
    result = git_output(["status", "--short", "--", *relative_paths])
    rows: list[dict[str, str]] = []
    for raw_line in result.stdout.splitlines():
        if not raw_line:
            continue
        rows.append(
            {
                "status": raw_line[:2],
                "path": raw_line[3:],
            }
        )
    return rows


def path_presence_for(relative_path: str) -> dict[str, Any]:
    path = repo_root / relative_path
    return {
        "path": relative_path,
        "present": path.exists(),
        "pathAllowed": path_is_in_allowed_commit_scope(relative_path),
    }


reasons: list[str] = []
registry_text = read_text(registry_path, reasons, "successor registry")
queue_text = read_text(queue_path, reasons, "successor queue")
design_queue_text = read_text(design_queue_path, reasons, "design successor queue")
queue_block = queue_item_block(queue_text)
design_queue_block = queue_item_block(design_queue_text)
registry_task = registry_task_block(registry_text)

registry_checks = {
    "milestone_105_present": "id: 105\n    title: Workspace restore, entitlement sync, and conflict-safe continuity" in registry_text,
    "milestone_105_in_progress": text_block_contains(registry_text, ["id: 105", "status: in_progress"]),
    "depends_102": text_block_contains(registry_text, ["id: 105", "dependencies:", "- 102"]),
    "depends_104": text_block_contains(registry_text, ["id: 105", "dependencies:", "- 104"]),
    "ui_work_task_present": "id: 105.2\n        owner: chummer6-ui\n        title: Land restore continuation, stale-state visibility, and conflict-choice UX on the primary desktop route." in registry_text,
    "ui_work_task_complete": "status: complete" in registry_task,
    "ui_work_task_landed_commit": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in registry_task,
    "wave_w8": text_block_contains(registry_text, ["id: W8", "milestone_ids:", "- 105"]),
}
registry_checks.update({f"ui_work_task_evidence_{index}": proof in registry_task for index, proof in enumerate(REQUIRED_REGISTRY_EVIDENCE_LINES, start=1)})

queue_checks = {
    "package_present": bool(queue_block),
    "frontier_matches": f"frontier_id: {FRONTIER_ID}" in queue_block,
    "repo_matches": "repo: chummer6-ui" in queue_block,
    "milestone_matches": "milestone_id: 105" in queue_block,
    "wave_matches": "wave: W8" in queue_block,
    "title_matches": "title: Land restore continuation and conflict-safe UX on the primary desktop route" in queue_block,
    "task_matches": "task: Keep restore, stale-state visibility, and conflict choices visible and boring on the primary desktop head." in queue_block,
    "status_complete": "status: complete" in queue_block,
    "landed_commit_matches": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in queue_block,
}
queue_checks.update({f"allowed_path_{path}": f"- {path}" in queue_block for path in EXPECTED_ALLOWED_PATHS})
queue_checks.update({f"owned_surface_{surface}": f"- {surface}" in queue_block for surface in EXPECTED_SURFACES})
queue_checks.update({f"proof_{index}": f"- {proof}" in queue_block for index, proof in enumerate(REQUIRED_QUEUE_PROOF_LINES, start=1)})
queue_checks["allowed_paths_exact"] = yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS
queue_checks["owned_surfaces_exact"] = yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES
queue_checks["completion_action_verify_closed_package_only"] = "completion_action: verify_closed_package_only" in queue_block
queue_checks["do_not_reopen_reason_present"] = "do_not_reopen_reason:" in queue_block

design_queue_checks = {
    "package_present": bool(design_queue_block),
    "frontier_matches": f"frontier_id: {FRONTIER_ID}" in design_queue_block,
    "repo_matches": "repo: chummer6-ui" in design_queue_block,
    "milestone_matches": "milestone_id: 105" in design_queue_block,
    "wave_matches": "wave: W8" in design_queue_block,
    "status_complete": "status: complete" in design_queue_block,
    "landed_commit_matches": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in design_queue_block,
    "allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
}
design_queue_checks.update({f"proof_{index}": f"- {proof}" in design_queue_block for index, proof in enumerate(REQUIRED_QUEUE_PROOF_LINES, start=1)})
design_queue_checks["completion_action_verify_closed_package_only"] = "completion_action: verify_closed_package_only" in design_queue_block
design_queue_checks["do_not_reopen_reason_present"] = "do_not_reopen_reason:" in design_queue_block

queue_mirror_checks = {
    "source_design_queue_path_matches": f"source_design_queue_path: {EXPECTED_DESIGN_QUEUE_PATH}" in queue_text,
    "package_blocks_match": queue_block.strip() == design_queue_block.strip(),
}

operator_helper_proof_checks = {
    "required_proof_avoids_active_run_helpers": not disallowed_active_run_proof_tokens("\n".join(REQUIRED_REGISTRY_EVIDENCE_LINES + REQUIRED_QUEUE_PROOF_LINES)),
    "registry_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(registry_task),
    "queue_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(queue_block),
    "design_queue_evidence_avoids_active_run_helpers": not disallowed_active_run_proof_tokens(design_queue_block),
}
sibling_package_proof_checks = {
    "required_proof_avoids_sibling_packages": not disallowed_sibling_package_proof_tokens("\n".join(REQUIRED_REGISTRY_EVIDENCE_LINES + REQUIRED_QUEUE_PROOF_LINES)),
    "registry_evidence_avoids_sibling_packages": not disallowed_sibling_package_proof_tokens(registry_task),
    "queue_evidence_avoids_sibling_packages": not disallowed_sibling_package_proof_tokens(queue_block),
    "design_queue_evidence_avoids_sibling_packages": not disallowed_sibling_package_proof_tokens(design_queue_block),
}

package_closed_by_canon = (
    registry_checks["ui_work_task_complete"]
    and registry_checks["ui_work_task_landed_commit"]
    and queue_checks["status_complete"]
    and queue_checks["landed_commit_matches"]
)
closure_guard = {
    "status": "closed_and_verified" if package_closed_by_canon else "needs_attention",
    "reason": (
        "Canonical registry and successor queue both mark the M105 UI package complete "
        f"at {EXPECTED_LANDED_COMMIT}; rerun this verifier as proof instead of reopening "
        "the closed flagship wave or reimplementing the package."
    ),
    "canonicalRegistryComplete": registry_checks["ui_work_task_complete"],
    "canonicalQueueComplete": queue_checks["status_complete"],
    "landedCommitPinned": (
        registry_checks["ui_work_task_landed_commit"] and queue_checks["landed_commit_matches"]
    ),
    "sourceMarkerProofRequired": True,
    "supportHandoffProofRequired": True,
}

for check_name, passed in registry_checks.items():
    if not passed:
        reasons.append(f"registry check failed: {check_name}")
for check_name, passed in queue_checks.items():
    if not passed:
        reasons.append(f"queue check failed: {check_name}")
for check_name, passed in design_queue_checks.items():
    if not passed:
        reasons.append(f"design queue check failed: {check_name}")
for check_name, passed in queue_mirror_checks.items():
    if not passed:
        reasons.append(f"queue mirror check failed: {check_name}")
for check_name, passed in operator_helper_proof_checks.items():
    if not passed:
        reasons.append(f"operator helper proof check failed: {check_name}")
for check_name, passed in sibling_package_proof_checks.items():
    if not passed:
        reasons.append(f"sibling package proof check failed: {check_name}")

source_results: dict[str, Any] = {}
for relative_path, groups in SOURCE_MARKERS.items():
    path = repo_root / relative_path
    text = read_text(path, reasons, relative_path)
    group_results: dict[str, Any] = {}
    for group_name, markers in groups.items():
        marker_results = []
        missing_markers = []
        for marker in markers:
            line = line_number(text, marker)
            marker_results.append({"marker": marker, "present": line is not None, "line": line})
            if line is None:
                missing_markers.append(marker)
        if missing_markers:
            reasons.append(f"{relative_path} missing {group_name} marker(s): {', '.join(missing_markers)}")
        group_results[group_name] = {
            "status": "pass" if not missing_markers else "fail",
            "markers": marker_results,
            "missingMarkers": missing_markers,
        }
    source_results[relative_path] = group_results

standard_verify_text = read_text(repo_root / STANDARD_VERIFY_PATH, reasons, STANDARD_VERIFY_PATH)
standard_verify_markers = [
    {"marker": marker, "present": line_number(standard_verify_text, marker) is not None, "line": line_number(standard_verify_text, marker)}
    for marker in EXPECTED_STANDARD_VERIFY_MARKERS
]
standard_verify_checks = {
    "path": STANDARD_VERIFY_PATH,
    "wired_into_standard_verify": all(marker["present"] for marker in standard_verify_markers),
    "markers": standard_verify_markers,
}
if not standard_verify_checks["wired_into_standard_verify"]:
    missing = [str(marker["marker"]) for marker in standard_verify_markers if not marker["present"]]
    reasons.append("standard verify is missing M105 restore-continuity guard marker(s): " + ", ".join(missing))

head_result = git_output(["rev-parse", "HEAD"])
head_commit = head_result.stdout.strip() if head_result.returncode == 0 else ""

repo_proof_trail = [latest_commit_for_path(path) for path in PROOF_RELEVANT_PATHS]
path_presence = [path_presence_for(path) for path in PROOF_RELEVANT_PATHS]
repo_proof_checks = {
    "head_commit_present": bool(head_commit),
    "all_paths_present_in_worktree": all(entry["present"] for entry in path_presence),
    "all_paths_declared_under_allowed_scope": all(entry["pathAllowed"] for entry in path_presence),
    "all_historical_paths_ancestor_of_head": all(entry["isAncestorOfHead"] for entry in repo_proof_trail if entry["exists"]),
    "all_historical_paths_have_in_scope_anchor": all(entry["scopeAllowed"] for entry in repo_proof_trail if entry["exists"]),
    "history_optional_for_live_proof_files": any(not entry["exists"] for entry in repo_proof_trail),
}
for check_name, passed in repo_proof_checks.items():
    if not passed:
        reasons.append(f"repo proof check failed: {check_name}")

for entry in repo_proof_trail:
    if entry["exists"] and not entry["paths"]:
        reasons.append(f"repo proof commit {entry['commit']} for {entry['path']} has no changed paths")
    if entry["exists"] and not entry["scopeAllowed"]:
        reasons.append(
            f"repo proof commit {entry['commit']} for {entry['path']} changed paths outside package/proof scope: "
            + ", ".join(entry["disallowedPaths"])
        )

relevant_worktree_changes = relevant_worktree_status(PROOF_RELEVANT_PATHS)
worktree_checks = {
    "tracked_paths": PROOF_RELEVANT_PATHS,
    "has_relevant_changes": bool(relevant_worktree_changes),
    "changes": relevant_worktree_changes,
}

historical_branch_commit_checks = {
    "historicalProofBranch": "chummer6-ui-finish",
    "commitPinsStillResolvableLocally": False,
    "retiredForCurrentRepoHistory": True,
    "reason": (
        "Canonical queue and registry rows still cite the closed historical `chummer6-ui-finish` proof line. "
        "This verifier now proves the closed package against live `chummer6-ui` source markers, support-prefill regressions, "
        "standard verify wiring, and current repo commit ancestry instead of requiring dead branch-only commit objects."
    ),
}

receipt = {
    "contract_name": "chummer6-ui.next90_m105_ui_restore_continuity",
    "generatedAt": now_iso(),
    "status": "pass" if not reasons else "fail",
    "reasons": reasons,
    "summary": (
        "Next-90 milestone 105.2 keeps desktop restore continuation, stale-state visibility, "
        "and conflict-safe workspace choices visible on the Avalonia primary route."
    ),
    "evidence": {
        "frontierId": FRONTIER_ID,
        "packageId": PACKAGE_ID,
        "milestoneId": MILESTONE_ID,
        "wave": WAVE,
        "registryPath": str(registry_path),
        "registryChecks": registry_checks,
        "queuePath": str(queue_path),
        "queueChecks": queue_checks,
        "designQueuePath": str(design_queue_path),
        "designQueueChecks": design_queue_checks,
        "queueMirrorChecks": queue_mirror_checks,
        "operatorHelperProofChecks": operator_helper_proof_checks,
        "disallowedActiveRunProofTokens": DISALLOWED_ACTIVE_RUN_PROOF_TOKENS,
        "siblingPackageProofChecks": sibling_package_proof_checks,
        "disallowedSiblingPackageProofTokens": DISALLOWED_SIBLING_PACKAGE_PROOF_TOKENS,
        "standardVerifyChecks": standard_verify_checks,
        "repoProofChecks": repo_proof_checks,
        "repoProofTrail": repo_proof_trail,
        "pathPresenceChecks": path_presence,
        "worktreeChecks": worktree_checks,
        "headCommit": head_commit,
        "landedCommit": EXPECTED_LANDED_COMMIT,
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "sourceMarkers": source_results,
        "closureGuard": closure_guard,
        "historicalBranchCommitChecks": historical_branch_commit_checks,
        "currentRepoProofMode": "live_source_and_wiring",
        "sourceDesignQueuePathMatches": queue_mirror_checks["source_design_queue_path_matches"],
        "packageBlocksMatch": queue_mirror_checks["package_blocks_match"],
    },
}

if receipt_path.is_file():
    try:
        existing_receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
        receipt["unchangedExceptGeneratedAt"] = comparable_receipt(existing_receipt) == comparable_receipt(receipt)
    except json.JSONDecodeError:
        receipt["unchangedExceptGeneratedAt"] = False
else:
    receipt["unchangedExceptGeneratedAt"] = False

receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

if reasons:
    print("[next90-m105-ui-restore-continuity] FAIL")
    for reason in reasons:
        print(f"- {reason}")
    sys.exit(1)

print("[next90-m105-ui-restore-continuity] PASS")
print(f"- receipt: {receipt_path}")
PY
