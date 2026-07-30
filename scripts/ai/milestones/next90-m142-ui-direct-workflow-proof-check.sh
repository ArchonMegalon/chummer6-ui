#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
cd "$repo_root"
workspace_root="$(cd "$repo_root/.." && pwd -P)"

default_fleet_queue_path="$workspace_root/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
if [[ ! -f "$default_fleet_queue_path" && -f "/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml" ]]; then
  default_fleet_queue_path="/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
fi

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-$workspace_root/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-$default_fleet_queue_path}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-$workspace_root/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M142_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json}"

# Release identity selection is ordered, deterministic, and never based on mtimes.
# An explicit M142 path is authoritative even when it is absent or invalid so a
# misspelled override cannot silently fall through to a different release.
explicit_release_channel_path="${CHUMMER_NEXT90_M142_RELEASE_CHANNEL_PATH:-}"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
run_services_release_channel_path="/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"
bundled_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$explicit_release_channel_path" ]]; then
  release_channel_path="$explicit_release_channel_path"
elif [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path="$canonical_release_channel_path"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path="$verified_release_channel_path"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path="$run_services_release_channel_path"
else
  release_channel_path="$bundled_release_channel_path"
fi

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" "$release_channel_path" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import stat
import sys
import tempfile
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])
release_channel_path = Path(sys.argv[6])
published_repo_root = (repo_root.parent / "chummer6-ui") if (repo_root.parent / "chummer6-ui").exists() else repo_root

PACKAGE_ID = "next90-m142-ui-close-direct-screenshot-and-runtime-proof-for-dense-builder-and-career-fl"
TITLE = "Close direct screenshot and runtime proof for dense builder and career flows, dice or initiative utilities, and contacts or lifestyles or notes workflows."
FRONTIER_ID = 9095697868
MILESTONE_ID = 142
WORK_TASK_ID = "142.1"
WAVE = "W22P"
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M142 chummer6-ui dense builder/career, dice/initiative, and contacts/lifestyles/notes direct proof is complete; "
    "future shards must verify the closed-package receipt, focused guard test, route-local gates, canonical registry row, "
    "and queue mirrors instead of reopening this slice."
)
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "close_direct_screenshot_and_runtime_proof_for_dense_buil:ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = (
    'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M142DirectWorkflowProofGuardTests" --no-restore'
)
EXPECTED_PROOF = [
    f"{published_repo_root}/Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs",
    f"{published_repo_root}/Chummer.Tests/Chummer.Tests.csproj",
    f"{published_repo_root}/scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh",
    f"{published_repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
    f"{published_repo_root}/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
    f"{published_repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    f"{published_repo_root}/scripts/ai/verify.sh",
    f"{published_repo_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
    f"{published_repo_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
    f"{published_repo_root}/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
    f"{published_repo_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
    f"{published_repo_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
    f"{published_repo_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json",
]
EXPECTED_REGISTRY_EVIDENCE = [
    (
        f"{published_repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, "
        f"{published_repo_root}/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh, and "
        f"{published_repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh keep dense builder/career, "
        "dice/initiative, and contacts/lifestyles/notes proof bound to direct screenshot-backed and runtime-backed route receipts instead of family prose."
    ),
    (
        f"{published_repo_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json, "
        f"{published_repo_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json, "
        f"{published_repo_root}/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json, "
        f"{published_repo_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json, "
        f"{published_repo_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json, and "
        f"{published_repo_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json keep the three milestone-142 parity families aligned to route-local proof."
    ),
    (
        f"{published_repo_root}/Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs, "
        f"{published_repo_root}/Chummer.Tests/Chummer.Tests.csproj, "
        f"{published_repo_root}/scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh, and "
        f"{published_repo_root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, audit evidence, or verify wiring drift from the completed package contract."
    ),
    (
        f"{published_repo_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json records the closed-package receipt for "
        f"`{PACKAGE_ID}`."
    ),
]
EXPECTED_SCREENSHOT_REVIEW_ROUTE_IDS = [
    "menu:dice_roller_or_workflow:initiative_screenshot",
    "dice_roller",
    "initiative_screenshot",
]
EXPECTED_SCREENSHOT_REVIEW_SCREENSHOTS = [
    "05-dense-section-light.png",
    "07-loaded-runner-tabs-light.png",
]
EXPECTED_WORKFLOW_SCREENSHOTS = [
    "05-dense-section-light.png",
    "07-loaded-runner-tabs-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "14-advancement-dialog-light.png",
]
# The M142 receipt is deliberately absent from these requirements. A receipt
# cannot establish the evidence needed to authorize its own pass verdict.
EXPECTED_FAMILY_REQUIREMENTS = {
    "family:dense_builder_and_career_workflows": {
        "required_suffixes": [
            "SECTION_HOST_RULESET_PARITY.generated.json",
            "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
            "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json",
            "UI_FLAGSHIP_RELEASE_GATE.generated.json",
            "UI_LOCAL_RELEASE_PROOF.generated.json",
            "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
        ],
    },
    "family:dice_initiative_and_table_utilities": {
        "required_suffixes": [
            "GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
            "SECTION_HOST_RULESET_PARITY.generated.json",
            "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
        ],
    },
    "family:identity_contacts_lifestyles_history": {
        "required_suffixes": [
            "SECTION_HOST_RULESET_PARITY.generated.json",
            "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
            "UI_FLAGSHIP_RELEASE_GATE.generated.json",
        ],
    },
}
DISALLOWED_FAMILY_TOKENS = [
    "/docker/chummercomplete/chummer-core-engine/docs/NEXT90_M142_DENSE_WORKBENCH_RECEIPTS.md",
]
SOURCE_MARKERS = {
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh": [
        '"dense_workbench_and_initiative"',
        '"menu:dice_roller_or_workflow:initiative_screenshot"',
        '"05-dense-section-light.png"',
        '"07-loaded-runner-tabs-light.png"',
    ],
    "scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh": [
        '"dense_builder_career"',
        '"initiative_utility"',
        '"contacts_lifestyles_notes"',
        '"10-contacts-section-light.png"',
        '"11-diary-dialog-light.png"',
        '"14-advancement-dialog-light.png"',
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        '"family:dense_builder_and_career_workflows"',
        '"SECTION_HOST_RULESET_PARITY.generated.json"',
        '"CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"',
        '"CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"',
        '"UI_LOCAL_RELEASE_PROOF.generated.json"',
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M142 direct workflow proof guard",
        "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh",
    ],
}
MAX_INPUT_BYTES = 16 * 1024 * 1024
MAX_RELEASE_BYTES = 2 * 1024 * 1024
MAX_RELEASE_AGE_CEILING_SECONDS = 86400
MAX_RELEASE_FUTURE_SKEW_CEILING_SECONDS = 300
OUTPUT_CONTRACT = "chummer6-ui.next90_m142_ui_direct_workflow_proof"
RELEASE_CONTRACT = "Chummer.Hub.Registry.Contracts"


class InputFailure(Exception):
    pass


@dataclass(frozen=True)
class InputSnapshot:
    label: str
    requested_path: str
    resolved_path: str
    data: bytes
    sha256: str
    size_bytes: int
    device: int
    inode: int
    mtime_ns: int

    def binding(self) -> dict[str, Any]:
        return {
            "requestedPath": self.requested_path,
            "resolvedPath": self.resolved_path,
            "sha256": self.sha256,
            "sizeBytes": self.size_bytes,
            "device": self.device,
            "inode": self.inode,
            "mtimeNs": self.mtime_ns,
        }


def lexical_absolute(path: Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def snapshot_regular_file(label: str, path: Path, max_bytes: int = MAX_INPUT_BYTES) -> InputSnapshot:
    requested = lexical_absolute(path)
    try:
        before_lstat = os.lstat(requested)
    except OSError as exc:
        raise InputFailure(f"{label} is unavailable: {requested}: {exc.strerror or exc}") from exc
    if stat.S_ISLNK(before_lstat.st_mode):
        raise InputFailure(f"{label} must not be a symbolic link: {requested}")
    if not stat.S_ISREG(before_lstat.st_mode):
        raise InputFailure(f"{label} must be a regular file: {requested}")
    if not hasattr(os, "O_NOFOLLOW"):
        raise InputFailure(f"{label} cannot be read safely because O_NOFOLLOW is unavailable")

    resolved_before = os.path.realpath(requested)
    flags = os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW
    try:
        descriptor = os.open(requested, flags)
    except OSError as exc:
        raise InputFailure(f"{label} could not be opened safely: {requested}: {exc.strerror or exc}") from exc
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode):
            raise InputFailure(f"{label} descriptor is not a regular file: {requested}")
        initial_path_identity = (
            before_lstat.st_dev,
            before_lstat.st_ino,
            before_lstat.st_size,
            before_lstat.st_mtime_ns,
        )
        initial_descriptor_identity = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
        if initial_path_identity != initial_descriptor_identity:
            raise InputFailure(f"{label} path was rebound before it could be read: {requested}")
        if before.st_size < 0 or before.st_size > max_bytes:
            raise InputFailure(f"{label} exceeds the {max_bytes}-byte input bound: {requested}")
        chunks: list[bytes] = []
        total = 0
        while True:
            chunk = os.read(descriptor, min(1024 * 1024, max_bytes + 1 - total))
            if not chunk:
                break
            chunks.append(chunk)
            total += len(chunk)
            if total > max_bytes:
                raise InputFailure(f"{label} exceeds the {max_bytes}-byte input bound while reading: {requested}")
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)

    identity_before = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
    identity_after = (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
    if identity_before != identity_after:
        raise InputFailure(f"{label} changed while it was being read: {requested}")
    data = b"".join(chunks)
    if len(data) != before.st_size:
        raise InputFailure(f"{label} size changed while it was being read: {requested}")

    try:
        after_lstat = os.lstat(requested)
    except OSError as exc:
        raise InputFailure(f"{label} disappeared after it was read: {requested}: {exc.strerror or exc}") from exc
    if stat.S_ISLNK(after_lstat.st_mode) or not stat.S_ISREG(after_lstat.st_mode):
        raise InputFailure(f"{label} path no longer names a regular non-symlink file: {requested}")
    path_identity = (after_lstat.st_dev, after_lstat.st_ino, after_lstat.st_size, after_lstat.st_mtime_ns)
    if path_identity != identity_after:
        raise InputFailure(f"{label} path was rebound while it was being read: {requested}")
    resolved_after = os.path.realpath(requested)
    if resolved_before != resolved_after:
        raise InputFailure(f"{label} resolved path changed while it was being read: {requested}")

    return InputSnapshot(
        label=label,
        requested_path=str(requested),
        resolved_path=resolved_before,
        data=data,
        sha256=hashlib.sha256(data).hexdigest(),
        size_bytes=len(data),
        device=before.st_dev,
        inode=before.st_ino,
        mtime_ns=before.st_mtime_ns,
    )


def snapshot_matches(first: InputSnapshot, second: InputSnapshot) -> bool:
    return (
        first.requested_path == second.requested_path
        and first.resolved_path == second.resolved_path
        and first.sha256 == second.sha256
        and first.size_bytes == second.size_bytes
        and first.device == second.device
        and first.inode == second.inode
        and first.mtime_ns == second.mtime_ns
    )


def decode_text(snapshot: InputSnapshot) -> str:
    try:
        return snapshot.data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise InputFailure(f"{snapshot.label} is not valid UTF-8: {snapshot.requested_path}") from exc


def parse_json(snapshot: InputSnapshot) -> dict[str, Any]:
    try:
        value = json.loads(decode_text(snapshot))
    except (InputFailure, json.JSONDecodeError) as exc:
        raise InputFailure(f"{snapshot.label} is not a valid JSON object: {snapshot.requested_path}: {exc}") from exc
    if not isinstance(value, dict):
        raise InputFailure(f"{snapshot.label} must contain a JSON object: {snapshot.requested_path}")
    return value


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_timestamp(value: Any) -> datetime | None:
    if not isinstance(value, str) or not value.strip():
        return None
    candidate = value.strip()
    if candidate.endswith("Z"):
        candidate = candidate[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(candidate)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def normalize_inline_whitespace(value: str) -> str:
    return " ".join(str(value or "").split())


def extract_block(text: str, anchor: str, next_anchors: list[str]) -> str:
    start = text.find(anchor)
    if start < 0:
        return ""
    end_candidates = [text.find(candidate, start + len(anchor)) for candidate in next_anchors]
    end_candidates = [index for index in end_candidates if index >= 0]
    end = min(end_candidates) if end_candidates else len(text)
    return text[start:end]


def extract_queue_item(text: str, package_id: str) -> str:
    anchor = f"package_id: {package_id}"
    anchor_index = text.find(anchor)
    if anchor_index < 0:
        return ""
    item_start = text.rfind("\n- title:", 0, anchor_index)
    if item_start < 0 and text.startswith("- title:"):
        item_start = 0
    elif item_start >= 0:
        item_start += 1
    else:
        return ""
    item_end = text.find("\n- title:", anchor_index + len(anchor))
    return text[item_start : item_end if item_end >= 0 else len(text)]


def exact_contract(payload: dict[str, Any], expected: str) -> bool:
    values = [payload.get(key) for key in ("contract_name", "contractName") if payload.get(key) not in (None, "")]
    return bool(values) and all(isinstance(value, str) and value == expected for value in values)


def exact_alias(payload: dict[str, Any], primary: str, alias: str, require_both: bool) -> tuple[str, bool]:
    primary_value = payload.get(primary)
    alias_value = payload.get(alias)
    primary_text = primary_value if isinstance(primary_value, str) else ""
    alias_text = alias_value if isinstance(alias_value, str) else ""
    values = [value for value in (primary_text, alias_text) if value]
    valid = bool(values) and all(value == value.strip() for value in values) and len(set(values)) == 1
    if require_both:
        valid = valid and bool(primary_text) and bool(alias_text)
    return (values[0] if valid else ""), valid


def exact_string_list(value: Any) -> list[str] | None:
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
        return None
    return value


def atomic_write_bytes(path: Path, data: bytes) -> None:
    parent = lexical_absolute(path.parent)
    parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=parent)
    temporary_path = Path(temporary_name)
    try:
        os.fchmod(descriptor, 0o644)
        view = memoryview(data)
        written = 0
        while written < len(view):
            written += os.write(descriptor, view[written:])
        os.fsync(descriptor)
        os.close(descriptor)
        descriptor = -1
        os.replace(temporary_path, lexical_absolute(path))
        directory_descriptor = os.open(parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        try:
            temporary_path.unlink()
        except FileNotFoundError:
            pass


def atomic_write_json(path: Path, payload: dict[str, Any]) -> None:
    atomic_write_bytes(path, (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8"))


generated_at = now_iso()
producer_run_id = str(uuid.uuid4())
unresolved: list[str] = []
snapshots: dict[str, InputSnapshot] = {}


def add_failure(message: str) -> None:
    if message not in unresolved:
        unresolved.append(message)


input_paths: dict[str, tuple[Path, int]] = {
    "registry": (registry_path, MAX_INPUT_BYTES),
    "queue": (queue_path, MAX_INPUT_BYTES),
    "designQueue": (design_queue_path, MAX_INPUT_BYTES),
    "releaseChannel": (release_channel_path, MAX_RELEASE_BYTES),
    "uiParityAudit": (repo_root / ".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json", MAX_INPUT_BYTES),
    "screenshotReview": (repo_root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", MAX_INPUT_BYTES),
    "workflowExecution": (repo_root / ".codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json", MAX_INPUT_BYTES),
    "generatedDialog": (repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json", MAX_INPUT_BYTES),
    "sectionHost": (repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json", MAX_INPUT_BYTES),
    "runboardRoute": (repo_root / ".codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json", MAX_INPUT_BYTES),
    "guardTestSource": (repo_root / "Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs", MAX_INPUT_BYTES),
    "testProjectSource": (repo_root / "Chummer.Tests/Chummer.Tests.csproj", MAX_INPUT_BYTES),
    "producerSource": (repo_root / "scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh", MAX_INPUT_BYTES),
}
for relative_source in SOURCE_MARKERS:
    input_paths[f"source:{relative_source}"] = (repo_root / relative_source, MAX_INPUT_BYTES)

for label, (path, max_bytes) in input_paths.items():
    try:
        snapshots[label] = snapshot_regular_file(label, path, max_bytes)
    except InputFailure as exc:
        add_failure(str(exc))


def text_input(label: str) -> str:
    snapshot = snapshots.get(label)
    if snapshot is None:
        return ""
    try:
        return decode_text(snapshot)
    except InputFailure as exc:
        add_failure(str(exc))
        return ""


def json_input(label: str) -> dict[str, Any]:
    snapshot = snapshots.get(label)
    if snapshot is None:
        return {}
    try:
        return parse_json(snapshot)
    except InputFailure as exc:
        add_failure(str(exc))
        return {}


registry_text = text_input("registry")
queue_text = text_input("queue")
design_queue_text = text_input("designQueue")
release_channel = json_input("releaseChannel")
audit_receipt = json_input("uiParityAudit")
screenshot_review_receipt = json_input("screenshotReview")
workflow_execution_receipt = json_input("workflowExecution")
generated_dialog_receipt = json_input("generatedDialog")
section_host_receipt = json_input("sectionHost")
runboard_route_receipt = json_input("runboardRoute")

registry_block = extract_block(registry_text, "- id: '142.1'", ["- id: '142.2'"])
queue_block = extract_queue_item(queue_text, PACKAGE_ID)
design_queue_block = extract_queue_item(design_queue_text, PACKAGE_ID)
registry_block_normalized = normalize_inline_whitespace(registry_block)
queue_block_normalized = normalize_inline_whitespace(queue_block)
design_queue_block_normalized = normalize_inline_whitespace(design_queue_block)

queue_checks: dict[str, bool] = {
    "registry_block_present": bool(registry_block),
    "queue_block_present": bool(queue_block),
    "design_queue_block_present": bool(design_queue_block),
    "registry_title_matches": TITLE in registry_block,
    "queue_title_matches": TITLE in queue_block_normalized,
    "design_queue_title_matches": TITLE in design_queue_block_normalized,
    "registry_status_complete": f"status: {EXPECTED_STATUS}" in registry_block_normalized,
    "queue_status_complete": f"status: {EXPECTED_STATUS}" in queue_block_normalized,
    "design_queue_status_complete": f"status: {EXPECTED_STATUS}" in design_queue_block_normalized,
    "registry_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in registry_block_normalized,
    "queue_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in queue_block_normalized,
    "design_queue_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in design_queue_block_normalized,
    "registry_do_not_reopen_reason_matches": normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in registry_block_normalized,
    "queue_do_not_reopen_reason_matches": normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in queue_block_normalized,
    "design_queue_do_not_reopen_reason_matches": normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in design_queue_block_normalized,
    "queue_frontier_matches": f"frontier_id: {FRONTIER_ID}" in queue_block,
    "design_queue_frontier_matches": f"frontier_id: {FRONTIER_ID}" in design_queue_block,
    "registry_evidence_exact": all(normalize_inline_whitespace(entry) in registry_block_normalized for entry in EXPECTED_REGISTRY_EVIDENCE),
    "queue_proof_exact": all(normalize_inline_whitespace(entry) in queue_block_normalized for entry in EXPECTED_PROOF + [EXPECTED_DIRECT_PROOF_COMMAND, EXPECTED_TARGETED_TEST_COMMAND]),
    "design_queue_proof_exact": all(normalize_inline_whitespace(entry) in design_queue_block_normalized for entry in EXPECTED_PROOF + [EXPECTED_DIRECT_PROOF_COMMAND, EXPECTED_TARGETED_TEST_COMMAND]),
}
for key, passed in queue_checks.items():
    if not passed:
        add_failure(f"Queue/registry proof check failed: {key}")

try:
    release_max_age_seconds = int(os.environ.get("CHUMMER_NEXT90_M142_RELEASE_MAX_AGE_SECONDS", "86400"))
    release_max_future_skew_seconds = int(os.environ.get("CHUMMER_NEXT90_M142_RELEASE_MAX_FUTURE_SKEW_SECONDS", "300"))
    if (
        release_max_age_seconds < 1
        or release_max_age_seconds > MAX_RELEASE_AGE_CEILING_SECONDS
        or release_max_future_skew_seconds < 0
        or release_max_future_skew_seconds > MAX_RELEASE_FUTURE_SKEW_CEILING_SECONDS
    ):
        raise ValueError
except ValueError:
    release_max_age_seconds = 0
    release_max_future_skew_seconds = -1
    add_failure(
        "Release freshness bounds must be integers within the enforced one-day age and five-minute future-skew ceilings"
    )

release_channel_id, release_channel_aliases_agree = exact_alias(release_channel, "channelId", "channel", require_both=True)
release_version, release_version_aliases_agree = exact_alias(release_channel, "releaseVersion", "version", require_both=True)
release_generated_at = parse_timestamp(release_channel.get("generatedAt"))
release_generated_at_alias = parse_timestamp(release_channel.get("generated_at")) if release_channel.get("generated_at") not in (None, "") else release_generated_at
release_age_seconds: float | None = None
if release_generated_at is not None:
    release_age_seconds = (datetime.now(timezone.utc) - release_generated_at).total_seconds()
release_checks: dict[str, bool] = {
    "schema_version_exact": release_channel.get("schemaVersion") == 1 and not isinstance(release_channel.get("schemaVersion"), bool),
    "contract_exact": exact_contract(release_channel, RELEASE_CONTRACT),
    "status_published": release_channel.get("status") == "published",
    "channel_aliases_agree": release_channel_aliases_agree,
    "version_aliases_agree": release_version_aliases_agree,
    "generated_at_valid": release_generated_at is not None,
    "generated_at_alias_agrees": release_generated_at is not None and release_generated_at_alias == release_generated_at,
    "generated_at_fresh": (
        release_age_seconds is not None
        and release_max_future_skew_seconds >= 0
        and -release_max_future_skew_seconds <= release_age_seconds <= release_max_age_seconds
    ),
}
for key, passed in release_checks.items():
    if not passed:
        add_failure(f"Release channel proof check failed: {key}")

rows_value = audit_receipt.get("rows")
rows = rows_value if isinstance(rows_value, list) else []
family_checks: dict[str, dict[str, bool]] = {}
for family_id, requirements in EXPECTED_FAMILY_REQUIREMENTS.items():
    matching_rows = [row for row in rows if isinstance(row, dict) and row.get("id") == family_id]
    row = matching_rows[0] if len(matching_rows) == 1 else {}
    evidence_value = row.get("evidence")
    evidence = exact_string_list(evidence_value) or []
    evidence_names = [Path(entry).name for entry in evidence if entry.strip()]
    checks = {
        "row_present_exactly_once": len(matching_rows) == 1,
        "visual_parity_yes": row.get("visual_parity") == "yes",
        "behavioral_parity_yes": row.get("behavioral_parity") == "yes",
        "evidence_is_string_list": exact_string_list(evidence_value) is not None,
        "required_direct_evidence_present": all(evidence_names.count(suffix) == 1 for suffix in requirements["required_suffixes"]),
        "disallowed_external_receipts_clear": not any(
            token in entry for token in DISALLOWED_FAMILY_TOKENS for entry in evidence
        ),
    }
    family_checks[family_id] = checks
    for key, passed in checks.items():
        if not passed:
            add_failure(f"Family proof check failed for {family_id}: {key}")

def supporting_generated_at_valid(payload: dict[str, Any], primary: str = "generatedAt") -> bool:
    return parse_timestamp(payload.get(primary)) is not None


audit_checks = {
    "audit_identity_exact": audit_receipt.get("probe_kind") == "ui_parity_audit",
    "audit_status_pass": audit_receipt.get("status") == "pass",
    "audit_generated_at_valid": supporting_generated_at_valid(audit_receipt, "generated_at"),
    "audit_visual_no_count_zero": type(audit_receipt.get("visualNoCount")) is int and audit_receipt.get("visualNoCount") == 0,
    "audit_behavioral_no_count_zero": type(audit_receipt.get("behavioralNoCount")) is int and audit_receipt.get("behavioralNoCount") == 0,
    "audit_release_blocking_no_count_zero": type(audit_receipt.get("releaseBlockingNoCount")) is int and audit_receipt.get("releaseBlockingNoCount") == 0,
    "audit_findings_empty": audit_receipt.get("findings") == [],
    "audit_coverage_gap_keys_empty": audit_receipt.get("coverageGapKeys") == [],
}

screenshot_channel, screenshot_channel_valid = exact_alias(screenshot_review_receipt, "channelId", "channel", require_both=False)
screenshot_version, screenshot_version_valid = exact_alias(screenshot_review_receipt, "releaseVersion", "version", require_both=False)
workflow_channel, workflow_channel_valid = exact_alias(workflow_execution_receipt, "channelId", "channel", require_both=False)
workflow_version, workflow_version_valid = exact_alias(workflow_execution_receipt, "releaseVersion", "version", require_both=False)

route_local_receipts = screenshot_review_receipt.get("routeLocalReceipts")
route_local_receipt = route_local_receipts.get("dense_workbench_and_initiative") if isinstance(route_local_receipts, dict) else {}
if not isinstance(route_local_receipt, dict):
    route_local_receipt = {}
workflow_evidence = workflow_execution_receipt.get("evidence")
workflow_evidence = workflow_evidence if isinstance(workflow_evidence, dict) else {}
direct_runtime_checks = workflow_evidence.get("direct_workflow_runtime_marker_checks")
direct_runtime_checks = direct_runtime_checks if isinstance(direct_runtime_checks, dict) else {}
generated_evidence = generated_dialog_receipt.get("evidence")
generated_evidence = generated_evidence if isinstance(generated_evidence, dict) else {}
section_evidence = section_host_receipt.get("evidence")
section_evidence = section_evidence if isinstance(section_evidence, dict) else {}
runboard_evidence = runboard_route_receipt.get("evidence")
runboard_evidence = runboard_evidence if isinstance(runboard_evidence, dict) else {}
closed_package = runboard_evidence.get("closedPackage")
closed_package = closed_package if isinstance(closed_package, dict) else {}

def nested_status_is_pass(mapping: dict[str, Any], key: str) -> bool:
    value = mapping.get(key)
    return isinstance(value, dict) and value.get("status") == "pass"


receipt_checks: dict[str, bool] = {
    **audit_checks,
    "screenshot_contract_exact": exact_contract(screenshot_review_receipt, "chummer6-ui.chummer5a_screenshot_review_gate"),
    "screenshot_status_pass": screenshot_review_receipt.get("status") == "pass",
    "screenshot_generated_at_valid": supporting_generated_at_valid(screenshot_review_receipt),
    "screenshot_channel_matches_release": screenshot_channel_valid and bool(release_channel_id) and screenshot_channel == release_channel_id,
    "screenshot_version_matches_release": screenshot_version_valid and bool(release_version) and screenshot_version == release_version,
    "workflow_contract_exact": exact_contract(workflow_execution_receipt, "chummer6-ui.desktop_workflow_execution_gate"),
    "workflow_status_pass": workflow_execution_receipt.get("status") == "pass",
    "workflow_generated_at_valid": supporting_generated_at_valid(workflow_execution_receipt),
    "workflow_channel_matches_release": workflow_channel_valid and bool(release_channel_id) and workflow_channel == release_channel_id,
    "workflow_version_matches_release": workflow_version_valid and bool(release_version) and workflow_version == release_version,
    "generated_dialog_contract_exact": exact_contract(generated_dialog_receipt, "chummer6-ui.generated_dialog_element_parity"),
    "generated_dialog_status_pass": generated_dialog_receipt.get("status") == "pass",
    "generated_dialog_generated_at_valid": supporting_generated_at_valid(generated_dialog_receipt),
    "section_host_contract_exact": exact_contract(section_host_receipt, "chummer6-ui.section_host_ruleset_parity"),
    "section_host_status_pass": section_host_receipt.get("status") == "pass",
    "section_host_generated_at_valid": supporting_generated_at_valid(section_host_receipt),
    "runboard_contract_exact": exact_contract(runboard_route_receipt, "chummer6-ui.next90_m121_ui_gm_runboard_route"),
    "runboard_status_pass": runboard_route_receipt.get("status") == "pass",
    "runboard_generated_at_valid": supporting_generated_at_valid(runboard_route_receipt),
    "route_local_dense_initiative_pass": route_local_receipt.get("status") == "pass",
    "route_local_dense_initiative_route_ids_match": exact_string_list(route_local_receipt.get("routeIds")) is not None and sorted(route_local_receipt.get("routeIds")) == sorted(EXPECTED_SCREENSHOT_REVIEW_ROUTE_IDS),
    "route_local_dense_initiative_screenshots_match": exact_string_list(route_local_receipt.get("screenshots")) is not None and sorted(route_local_receipt.get("screenshots")) == sorted(EXPECTED_SCREENSHOT_REVIEW_SCREENSHOTS),
    "workflow_dense_builder_career_pass": nested_status_is_pass(direct_runtime_checks, "dense_builder_career"),
    "workflow_initiative_utility_pass": nested_status_is_pass(direct_runtime_checks, "initiative_utility"),
    "workflow_contacts_lifestyles_notes_pass": nested_status_is_pass(direct_runtime_checks, "contacts_lifestyles_notes"),
    "workflow_required_screenshots_present": (
        exact_string_list(workflow_evidence.get("direct_workflow_required_screenshot_files")) is not None
        and all(screenshot in workflow_evidence.get("direct_workflow_required_screenshot_files") for screenshot in EXPECTED_WORKFLOW_SCREENSHOTS)
    ),
    "workflow_missing_screenshots_clear": workflow_evidence.get("direct_workflow_missing_screenshot_files") == [],
    "generated_dialog_dice_command_present": "dice_roller" in (exact_string_list(generated_evidence.get("commandIdsFound")) or []),
    "generated_dialog_dice_dialog_present": "dialog.dice_roller" in (exact_string_list(generated_evidence.get("rebuildableDialogIdsFound")) or []),
    "section_host_dice_command_present": "dice_roller" in (exact_string_list(section_evidence.get("commandIdsFound")) or []),
    "runboard_closed_package_present": bool(closed_package),
    "runboard_completion_action_exact": closed_package.get("completionAction") == "verify_closed_package_only",
}
for key, passed in receipt_checks.items():
    if not passed:
        add_failure(f"Route-local receipt check failed: {key}")

source_checks: dict[str, dict[str, bool]] = {}
for relative_path, markers in SOURCE_MARKERS.items():
    text = text_input(f"source:{relative_path}")
    marker_checks = {marker: marker in text for marker in markers}
    source_checks[relative_path] = marker_checks
    for marker, passed in marker_checks.items():
        if not passed:
            add_failure(f"Source marker missing in {relative_path}: {marker}")

# A deterministic fixture-only rendezvous lets the isolated behavioral suite
# mutate an already-snapshotted input and prove publication revalidation fails.
# Its files are derived from the caller-selected receipt path, so the hook does
# not introduce a second caller-controlled write target.
test_rendezvous = os.environ.get("CHUMMER_NEXT90_M142_TEST_REVALIDATION_RENDEZVOUS", "").strip()
if test_rendezvous not in {"", "1"}:
    add_failure("The M142 test revalidation rendezvous must be unset or exactly 1")
elif test_rendezvous == "1":
    test_signal = Path(f"{lexical_absolute(receipt_path)}.before-revalidation")
    test_continue = Path(f"{lexical_absolute(receipt_path)}.continue")
    atomic_write_bytes(test_signal, b"ready\n")
    deadline = time.monotonic() + 10.0
    while not os.path.exists(test_continue) and time.monotonic() < deadline:
        time.sleep(0.01)
    if not os.path.exists(test_continue):
        add_failure("Timed out waiting for the M142 test revalidation rendezvous")

final_revalidation: dict[str, bool] = {}
for label, original in snapshots.items():
    path, max_bytes = input_paths[label]
    try:
        current = snapshot_regular_file(label, path, max_bytes)
        unchanged = snapshot_matches(original, current)
    except InputFailure as exc:
        add_failure(f"Final input revalidation failed: {exc}")
        unchanged = False
    final_revalidation[label] = unchanged
    if not unchanged:
        add_failure(f"Authoritative input changed before publication: {label}")

release_snapshot = snapshots.get("releaseChannel")
release_evidence = {
    "selectedPath": str(lexical_absolute(release_channel_path)),
    "resolvedPath": release_snapshot.resolved_path if release_snapshot else None,
    "sha256": release_snapshot.sha256 if release_snapshot else None,
    "sizeBytes": release_snapshot.size_bytes if release_snapshot else None,
    "contract": RELEASE_CONTRACT,
    "generatedAt": release_channel.get("generatedAt"),
    "ageSeconds": round(release_age_seconds, 3) if release_age_seconds is not None else None,
    "maxAgeSeconds": release_max_age_seconds,
    "maxFutureSkewSeconds": release_max_future_skew_seconds,
    "channelId": release_channel_id or None,
    "releaseVersion": release_version or None,
    "checks": release_checks,
}
payload: dict[str, Any] = {
    "schemaVersion": 1,
    "contract_name": OUTPUT_CONTRACT,
    "contractName": OUTPUT_CONTRACT,
    "producerRunId": producer_run_id,
    "generatedAt": generated_at,
    "status": "pass" if not unresolved else "fail",
    "channelId": release_channel_id or None,
    "channel": release_channel_id or None,
    "releaseVersion": release_version or None,
    "version": release_version or None,
    "summary": (
        "Milestone 142 direct workflow proof is closed on route-local evidence."
        if not unresolved
        else "Milestone 142 direct workflow proof is incomplete."
    ),
    "unresolved": unresolved,
    "releaseEvidence": release_evidence,
    "evidence": {
        "packageId": PACKAGE_ID,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "queueChecks": queue_checks,
        "familyChecks": family_checks,
        "receiptChecks": receipt_checks,
        "sourceChecks": source_checks,
        "releaseChecks": release_checks,
        "inputBindings": {label: snapshot.binding() for label, snapshot in sorted(snapshots.items())},
        "finalInputRevalidation": final_revalidation,
        "proofFiles": EXPECTED_PROOF,
        "proofCommands": [
            EXPECTED_DIRECT_PROOF_COMMAND,
            EXPECTED_TARGETED_TEST_COMMAND,
        ],
    },
}

atomic_write_json(receipt_path, payload)
if unresolved:
    raise SystemExit(43)
PY
