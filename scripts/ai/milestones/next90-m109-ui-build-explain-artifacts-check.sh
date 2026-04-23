#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M109_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M109_UI_BUILD_EXPLAIN_ARTIFACTS.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

require_marker() {
  local file="$1"
  local marker="$2"
  if ! rg -n --fixed-strings "$marker" "$file" >/dev/null; then
    echo "[next90-m109] FAIL: missing marker in $file: $marker" >&2
    exit 109
  fi
}

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$repo_root" <<'PY'
from __future__ import annotations

import base64
import binascii
import gzip
import html
import quopri
import re
import subprocess
import sys
import urllib.parse
import zlib
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
repo_root = Path(sys.argv[4])

PACKAGE_ID = "next90-m109-ui-build-explain-artifacts"
WORK_TASK_ID = "109.2"
FRONTIER_ID = "4240255582"
MILESTONE_ID = "109"
WAVE = "W9"
REPO = "chummer6-ui"
LANDED_COMMIT = "da261bb7"
TITLE = "Launch build explain companions from compare, import, and blocker surfaces"
TASK = "Make desktop compare, import, and blocker surfaces open inspectable explain companions instead of plain receipts alone."
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M109 chummer6-ui build explain artifacts is complete; future shards must verify the companion launcher, "
    "desktop surface wiring, guard script, registry row, queue row, and focused test proof instead of reopening "
    "the compare/import/blocker companion package."
)
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_OWNED_SURFACES = [
    "build_explain:artifact_launch",
    "explain_receipts:desktop",
]
REQUIRED_QUEUE_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopExplainCompanionLauncher.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustPanelFactory.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/DesktopTrustReceiptComposer.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/DesktopTrustPanelFactoryTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M109BuildExplainArtifactsGuardTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish commit da261bb7 Launch desktop explain companions.",
    "/docker/chummercomplete/chummer6-ui-finish commit bfe39d47 Tighten M109 queue mirror proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 4fc3f056 Tighten M109 encoded worker-unsafe closure proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit b12f06d5 Tighten M109 encoded proof guard.",
    "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh",
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
    "OODA loop",
    "operator/OODA",
    "prompt.txt",
]
ENCODED_PROOF_TOKEN_PATTERN = re.compile(r"[A-Za-z0-9+/=_:%.-]{8,}")
HEX_PROOF_TOKEN_PATTERN = re.compile(r"(?<![0-9A-Fa-f])[0-9A-Fa-f]{16,}(?![0-9A-Fa-f])")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"[next90-m109] FAIL: {message}")


def read_text(path: Path) -> str:
    require(path.is_file(), f"missing authority file: {path}")
    return path.read_text(encoding="utf-8-sig")


def block_between(text: str, start: str, stop_prefix: str) -> str:
    start_index = text.find(start)
    if start_index < 0:
        return ""
    next_index = text.find(stop_prefix, start_index + len(start))
    return text[start_index:] if next_index < 0 else text[start_index:next_index]


def decode_raw_bytes(raw: bytes) -> list[str]:
    decoded: list[str] = []
    text = raw.decode("utf-8", errors="ignore")
    if text:
        decoded.append(text)
    for decompressor in (gzip.decompress, zlib.decompress):
        try:
            expanded = decompressor(raw)
        except (OSError, zlib.error, ValueError):
            continue
        expanded_text = expanded.decode("utf-8", errors="ignore")
        if expanded_text:
            decoded.append(expanded_text)
    return decoded


def decode_token(token: str) -> list[str]:
    decoded: list[str] = []
    padded = token + ("=" * (-len(token) % 4))
    for value in {token, padded}:
        try:
            raw = base64.b64decode(value.encode("ascii"), validate=True)
        except (binascii.Error, ValueError):
            continue
        decoded.extend(decode_raw_bytes(raw))

    normalized = token.upper()
    for value in {normalized, normalized + ("=" * (-len(normalized) % 8))}:
        try:
            raw = base64.b32decode(value.encode("ascii"), casefold=True)
        except (binascii.Error, ValueError):
            continue
        decoded.extend(decode_raw_bytes(raw))

    for decoder in (base64.b85decode, base64.a85decode):
        try:
            raw = decoder(token.encode("ascii"))
        except (binascii.Error, ValueError):
            continue
        decoded.extend(decode_raw_bytes(raw))

    try:
        unquoted = urllib.parse.unquote(token)
    except Exception:
        unquoted = ""
    if unquoted and unquoted != token:
        decoded.append(unquoted)

    unescaped = html.unescape(token)
    if unescaped and unescaped != token:
        decoded.append(unescaped)

    try:
        qp = quopri.decodestring(token.encode("utf-8", errors="ignore")).decode("utf-8", errors="ignore")
    except Exception:
        qp = ""
    if qp and qp != token:
        decoded.append(qp)

    for match in HEX_PROOF_TOKEN_PATTERN.finditer(token):
        try:
            raw = bytes.fromhex(match.group(0))
        except ValueError:
            continue
        decoded.extend(decode_raw_bytes(raw))

    return decoded


def decode_proof_candidates(text: str) -> list[str]:
    candidates: list[str] = [text]
    for decoded in (
        html.unescape(text),
        urllib.parse.unquote(text),
        quopri.decodestring(text.encode("utf-8", errors="ignore")).decode("utf-8", errors="ignore"),
    ):
        if decoded and decoded not in candidates:
            candidates.append(decoded)

    for match in ENCODED_PROOF_TOKEN_PATTERN.finditer(text):
        token = match.group(0).strip()
        for decoded in decode_token(token):
            if decoded and decoded not in candidates:
                candidates.append(decoded)

    for match in HEX_PROOF_TOKEN_PATTERN.finditer(text):
        token = match.group(0)
        try:
            decoded = bytes.fromhex(token).decode("utf-8", errors="ignore")
        except ValueError:
            continue
        if decoded and decoded not in candidates:
            candidates.append(decoded)

    return candidates


def reject_worker_unsafe_proof(text: str, label: str) -> None:
    for candidate in decode_proof_candidates(text):
        lowered = candidate.lower()
        for token in DISALLOWED_PROOF_TOKENS:
            if token.lower() in lowered:
                raise SystemExit(
                    "[next90-m109] FAIL: M109 package proof cites blocked "
                    f"active-run/operator helper evidence in {label}: {token}"
                )


def require_single_queue_row(text: str, label: str) -> None:
    title_count = text.count(queue_anchor)
    package_count = text.count(f"    package_id: {PACKAGE_ID}\n")
    frontier_count = text.count(f"    frontier_id: {FRONTIER_ID}\n")
    require(title_count == 1, f"{label} queue has {title_count} rows titled for {PACKAGE_ID}")
    require(package_count == 1, f"{label} queue has {package_count} package_id rows for {PACKAGE_ID}")
    require(frontier_count == 1, f"{label} queue has {frontier_count} frontier rows for {PACKAGE_ID}")


def validate_queue_block(queue_block: str, label: str) -> None:
    require(queue_block, f"{label} successor queue is missing {PACKAGE_ID}")
    require(f"  - title: {TITLE}\n" in queue_block, f"{label} queue package title drifted")
    require(f"    package_id: {PACKAGE_ID}\n" in queue_block, f"{label} queue package id drifted")
    require(f"    frontier_id: {FRONTIER_ID}\n" in queue_block, f"{label} queue frontier id drifted")
    require(f"    task: {TASK}\n" in queue_block, f"{label} queue package task drifted")
    require(f"    milestone_id: {MILESTONE_ID}\n" in queue_block, f"{label} queue milestone id drifted")
    require(f"    wave: {WAVE}\n" in queue_block, f"{label} queue wave drifted")
    require(f"    repo: {REPO}\n" in queue_block, f"{label} queue repo drifted")
    require("    status: complete\n" in queue_block, f"{label} queue status is not complete")
    require(f"    landed_commit: {LANDED_COMMIT}\n" in queue_block, f"{label} queue landed commit drifted")
    require("    completion_action: verify_closed_package_only\n" in queue_block, f"{label} queue completion action drifted")
    require(EXPECTED_DO_NOT_REOPEN_REASON in queue_block, f"{label} queue do-not-reopen proof drifted")
    for proof_item in REQUIRED_QUEUE_PROOF_ITEMS:
        require(f"      - {proof_item}" in queue_block, f"{label} queue proof anchor missing: {proof_item}")
    for allowed_path in EXPECTED_ALLOWED_PATHS:
        require(f"      - {allowed_path}" in queue_block, f"{label} queue allowed path missing: {allowed_path}")
    for surface in EXPECTED_OWNED_SURFACES:
        require(f"      - {surface}" in queue_block, f"{label} queue owned surface missing: {surface}")


def git_object_exists(repo_root: Path, revision: str) -> bool:
    result = subprocess.run(
        ["git", "-C", str(repo_root), "cat-file", "-e", f"{revision}^{{commit}}"],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    return result.returncode == 0


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)

milestone_block = block_between(registry_text, "  - id: 109\n", "\n  - id: 110\n")
require(milestone_block, "canonical registry is missing milestone 109")
require("title: Build Lab explain companion lane" in milestone_block, "registry milestone 109 title drifted")
require("status: in_progress" in milestone_block, "registry milestone 109 is not in progress")
require("      - chummer6-ui\n" in milestone_block, "registry milestone 109 no longer assigns chummer6-ui")
require("      - 104\n" in milestone_block and "      - 107\n" in milestone_block, "registry milestone 109 dependency set drifted")
require(f"      - id: {WORK_TASK_ID}\n" in milestone_block, "registry milestone 109 is missing UI work task 109.2")
registry_task_block = block_between(milestone_block, "      - id: 109.2\n", "\n      - id: 109.3\n")
require("        owner: chummer6-ui\n" in registry_task_block, "registry work task 109.2 owner drifted")
require("        title: Launch explain companions from compare, import, and blocker surfaces on the promoted desktop head." in registry_task_block, "registry work task 109.2 title drifted")
require("        status: complete\n" in registry_task_block, "registry work task 109.2 is not complete")
require(f"        landed_commit: {LANDED_COMMIT}\n" in registry_task_block, "registry work task 109.2 landed commit drifted")
reject_worker_unsafe_proof(milestone_block, "registry milestone 109")

queue_anchor = f"  - title: {TITLE}\n"
require_single_queue_row(queue_text, "fleet mirror")
require_single_queue_row(design_queue_text, "design source")
fleet_queue_block = block_between(queue_text, queue_anchor, "\n  - title: ")
design_queue_block = block_between(design_queue_text, queue_anchor, "\n  - title: ")
validate_queue_block(fleet_queue_block, "fleet mirror")
validate_queue_block(design_queue_block, "design source")
reject_worker_unsafe_proof(fleet_queue_block, "Fleet queue row")
reject_worker_unsafe_proof(design_queue_block, "design queue row")
require(fleet_queue_block == design_queue_block, "Fleet and design-owned M109 successor queue rows drifted apart")

local_files = {
    "scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh": PACKAGE_ID,
    "Chummer.Tests/Compliance/Next90M109BuildExplainArtifactsGuardTests.cs": FRONTIER_ID,
    "Chummer.Tests/Chummer.Tests.csproj": "Compliance\\Next90M109BuildExplainArtifactsGuardTests.cs",
    "scripts/ai/verify.sh": "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh",
}
for relative_path, marker in local_files.items():
    local_text = read_text(repo_root / relative_path)
    require(marker in local_text, f"repo-local proof marker missing from {relative_path}: {marker}")

require(not git_object_exists(repo_root, LANDED_COMMIT), "historical M109 landed commit unexpectedly resolves in current repo checkout")
PY

require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "PACKAGE_ID = \"next90-m109-ui-build-explain-artifacts\""
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "FRONTIER_ID = \"4240255582\""
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "REQUIRED_QUEUE_PROOF_ITEMS"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopExplainCompanionLauncher.cs"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "TASK_LOCAL_TELEMETRY.generated.json"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "ACTIVE_RUN_HANDOFF.generated.md"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "active-run helper commands"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "Fleet and design-owned M109 successor queue rows drifted apart"
require_marker scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh "historical M109 landed commit unexpectedly resolves in current repo checkout"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

PACKAGE_ID = "next90-m109-ui-build-explain-artifacts"
FRONTIER_ID = 4240255582
MILESTONE_ID = 109
WORK_TASK_ID = "109.2"
WAVE = "W9"
REPO = "chummer6-ui"
LANDED_COMMIT = "da261bb7"
TITLE = "Launch build explain companions from compare, import, and blocker surfaces"
TASK = "Make desktop compare, import, and blocker surfaces open inspectable explain companions instead of plain receipts alone."
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_OWNED_SURFACES = [
    "build_explain:artifact_launch",
    "explain_receipts:desktop",
]
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M109 chummer6-ui build explain artifacts is complete; future shards must verify the companion launcher, "
    "desktop surface wiring, guard script, registry row, queue row, and focused test proof instead of reopening "
    "the compare/import/blocker companion package."
)
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh"
EXPECTED_TEST_COMMAND = (
    'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter '
    '"FullyQualifiedName~Next90M109BuildExplainArtifactsGuardTests" --no-restore'
)
EXPECTED_QUEUE_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopExplainCompanionLauncher.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/DesktopTrustPanelFactory.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/DesktopTrustReceiptComposer.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/DesktopTrustPanelFactoryTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M109BuildExplainArtifactsGuardTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish commit da261bb7 Launch desktop explain companions.",
    "/docker/chummercomplete/chummer6-ui-finish commit bfe39d47 Tighten M109 queue mirror proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 4fc3f056 Tighten M109 encoded worker-unsafe closure proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit b12f06d5 Tighten M109 encoded proof guard.",
    "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh",
]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def block_between(text: str, start: str, stop_prefix: str) -> str:
    start_index = text.find(start)
    if start_index < 0:
        return ""
    next_index = text.find(stop_prefix, start_index + len(start))
    return text[start_index:] if next_index < 0 else text[start_index:next_index]


def comparable_receipt(payload: dict[str, object]) -> dict[str, object]:
    comparable = dict(payload)
    comparable.pop("generatedAt", None)
    return comparable


def git_object_exists(repo_root: Path, revision: str) -> bool:
    result = subprocess.run(
        ["git", "-C", str(repo_root), "cat-file", "-e", f"{revision}^{{commit}}"],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    return result.returncode == 0


def build_queue_checks(block: str) -> dict[str, bool]:
    return {
        "package_present": bool(block),
        "title_matches": f"  - title: {TITLE}\n" in block,
        "task_matches": f"    task: {TASK}\n" in block,
        "frontier_matches": f"    frontier_id: {FRONTIER_ID}\n" in block,
        "milestone_matches": f"    milestone_id: {MILESTONE_ID}\n" in block,
        "wave_matches": f"    wave: {WAVE}\n" in block,
        "repo_matches": f"    repo: {REPO}\n" in block,
        "status_complete": "    status: complete\n" in block,
        "landed_commit_matches": f"    landed_commit: {LANDED_COMMIT}\n" in block,
        "completion_action_verify_closed_package_only": "    completion_action: verify_closed_package_only\n" in block,
        "do_not_reopen_reason_matches": EXPECTED_DO_NOT_REOPEN_REASON in block,
        "allowed_paths_exact": all(f"      - {path}" in block for path in EXPECTED_ALLOWED_PATHS),
        "owned_surfaces_exact": all(f"      - {surface}" in block for surface in EXPECTED_OWNED_SURFACES),
        "queue_proof_items_exact": all(f"      - {item}" in block for item in EXPECTED_QUEUE_PROOF_ITEMS),
    }


registry_text = read_text(registry_path)
queue_text = read_text(queue_path)
design_queue_text = read_text(design_queue_path)

milestone_block = block_between(registry_text, "  - id: 109\n", "\n  - id: 110\n")
registry_task_block = block_between(milestone_block, "      - id: 109.2\n", "\n      - id: 109.3\n")
queue_anchor = f"  - title: {TITLE}\n"
fleet_queue_block = block_between(queue_text, queue_anchor, "\n  - title: ")
design_queue_block = block_between(design_queue_text, queue_anchor, "\n  - title: ")

evidence = {
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "milestoneId": MILESTONE_ID,
    "workTaskId": WORK_TASK_ID,
    "wave": WAVE,
    "repo": REPO,
    "landedCommit": LANDED_COMMIT,
    "title": TITLE,
    "task": TASK,
    "allowedPaths": EXPECTED_ALLOWED_PATHS,
    "ownedSurfaces": EXPECTED_OWNED_SURFACES,
    "directProofCommand": EXPECTED_DIRECT_PROOF_COMMAND,
    "targetedTestCommand": EXPECTED_TEST_COMMAND,
    "registryChecks": {
        "milestone_present": bool(milestone_block),
        "task_present": bool(registry_task_block),
        "status_complete": "        status: complete\n" in registry_task_block,
        "landed_commit_matches": f"        landed_commit: {LANDED_COMMIT}\n" in registry_task_block,
        "owner_matches": "        owner: chummer6-ui\n" in registry_task_block,
        "title_matches": "        title: Launch explain companions from compare, import, and blocker surfaces on the promoted desktop head.\n" in registry_task_block,
        "direct_proof_command_recorded": "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh exited 0" in registry_task_block,
    },
    "queueChecks": build_queue_checks(fleet_queue_block),
    "designQueueChecks": build_queue_checks(design_queue_block),
    "queueMirrorChecks": {
        "fleet_queue_points_to_design_queue": 'source_design_queue_path: /docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml' in queue_text,
        "package_blocks_match": fleet_queue_block == design_queue_block,
        "fleet_queue_package_unique": queue_text.count(f"    package_id: {PACKAGE_ID}\n") == 1,
        "design_queue_package_unique": design_queue_text.count(f"    package_id: {PACKAGE_ID}\n") == 1,
    },
    "localRepoChecks": {
        "guard_script_present": (repo_root / "scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh").is_file(),
        "guard_test_present": (repo_root / "Chummer.Tests/Compliance/Next90M109BuildExplainArtifactsGuardTests.cs").is_file(),
        "verify_wiring_present": "bash scripts/ai/milestones/next90-m109-ui-build-explain-artifacts-check.sh" in read_text(repo_root / "scripts/ai/verify.sh"),
        "compliance_wiring_present": "Compliance\\Next90M109BuildExplainArtifactsGuardTests.cs" in read_text(repo_root / "Chummer.Tests/Chummer.Tests.csproj"),
        "historical_landed_commit_resolves": git_object_exists(repo_root, LANDED_COMMIT),
        "historical_finish_repo_proof_pinned": all(f"      - {item}" in fleet_queue_block for item in EXPECTED_QUEUE_PROOF_ITEMS[:-1]),
    },
    "closureGuard": {
        "status": "closed_and_verified",
        "canonicalRegistryComplete": "        status: complete\n" in registry_task_block,
        "canonicalQueueComplete": "    status: complete\n" in fleet_queue_block and "    status: complete\n" in design_queue_block,
        "completionActionPinned": "    completion_action: verify_closed_package_only\n" in fleet_queue_block and "    completion_action: verify_closed_package_only\n" in design_queue_block,
        "doNotReopenReasonPinned": EXPECTED_DO_NOT_REOPEN_REASON in fleet_queue_block and EXPECTED_DO_NOT_REOPEN_REASON in design_queue_block,
        "reason": "Canonical registry and successor queue both mark the M109 UI package complete at da261bb7; rerun this verifier as proof instead of reopening the closed desktop explain-companion package.",
    },
}

receipt = {
    "contract_name": "chummer6-ui.next90_m109_ui_build_explain_artifacts",
    "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "status": "pass",
    "summary": "M109 desktop explain companions remain canonically closed and locally guarded against reopen work in the active chummer6-ui repo.",
    "unresolved": [],
    "evidence": evidence,
}

if receipt_path.is_file():
    try:
        existing_receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception:
        existing_receipt = None
    if (
        isinstance(existing_receipt, dict)
        and comparable_receipt(existing_receipt) == comparable_receipt(receipt)
        and isinstance(existing_receipt.get("generatedAt"), str)
    ):
        receipt["generatedAt"] = existing_receipt["generatedAt"]

receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
print(f"[next90-m109] PASS: wrote receipt {receipt_path}")
PY

dotnet test Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M109BuildExplainArtifactsGuardTests" --no-restore
