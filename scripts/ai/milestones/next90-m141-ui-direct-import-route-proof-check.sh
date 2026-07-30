#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
workspace_root="$(cd "$repo_root_physical/.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$workspace_root/chummer6-ui}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-$repo_root/.codex-design/product/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-$repo_root/.codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-$repo_root/.codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M141_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json}"
default_flagship_frontier_root="$workspace_root/fleet/.codex-studio/published/full-product-frontiers"
if [[ -d "/docker/fleet/.codex-studio/published/full-product-frontiers" ]]; then
  default_flagship_frontier_root="/docker/fleet/.codex-studio/published/full-product-frontiers"
fi
flagship_frontier_root="${CHUMMER_FLAGSHIP_FRONTIER_ROOT:-$default_flagship_frontier_root}"
flagship_frontier_id="${CHUMMER_FLAGSHIP_FRONTIER_ID:-1922169755}"
flagship_queue_path="${CHUMMER_FLAGSHIP_QUEUE_PATH:-$(dirname "$flagship_frontier_root")/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
preferred_flagship_frontier_path="$flagship_frontier_root/shard-1.generated.yaml"
whole_project_flagship_frontier_path="$(dirname "$flagship_frontier_root")/FULL_PRODUCT_FRONTIER.generated.yaml"
if [[ -n "${CHUMMER_FLAGSHIP_FRONTIER_PATH:-}" ]]; then
  default_flagship_frontier_path="$CHUMMER_FLAGSHIP_FRONTIER_PATH"
elif [[ -e "$preferred_flagship_frontier_path" || -L "$preferred_flagship_frontier_path" ]]; then
  default_flagship_frontier_path="$preferred_flagship_frontier_path"
else
  default_flagship_frontier_path="$whole_project_flagship_frontier_path"
fi
flagship_frontier_path="${CHUMMER_FLAGSHIP_FRONTIER_PATH:-$default_flagship_frontier_path}"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-}"
if [[ -z "${CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH:-}" && -z "$hub_registry_root" ]]; then
  hub_registry_root="$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)"
fi
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
if [[ -n "${CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH:-}" ]]; then
  release_channel_path_default="$CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH"
elif [[ -n "$canonical_release_channel_path" && ( -e "$canonical_release_channel_path" || -L "$canonical_release_channel_path" ) ]]; then
  release_channel_path_default="$canonical_release_channel_path"
elif [[ -e "$verified_release_channel_path" || -L "$verified_release_channel_path" ]]; then
  release_channel_path_default="$verified_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" "$release_channel_path" "$flagship_frontier_path" "$flagship_frontier_root" "$flagship_queue_path" "$flagship_frontier_id" <<'PY'
from __future__ import annotations

import json
import hashlib
import os
import re
import stat
import sys
import uuid
import zlib
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])
release_channel_path = Path(sys.argv[6])
flagship_frontier_path = Path(sys.argv[7])
flagship_frontier_root = Path(sys.argv[8])
flagship_queue_path = Path(sys.argv[9])
configured_flagship_frontier_id = int(sys.argv[10])

MAX_TEXT_BYTES = 16 * 1024 * 1024
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_PNG_BYTES = 64 * 1024 * 1024
RELEASE_MAX_AGE = timedelta(days=14)
SUPPORTING_RECEIPT_MAX_AGE = timedelta(days=14)
MAX_FUTURE_SKEW = timedelta(minutes=5)

PACKAGE_ID = "next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment"
TITLE = "Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes."
TASK = TITLE
FRONTIER_ID = 2354698282
MILESTONE_ID = 141
WORK_TASK_ID = "141.1"
WAVE = "W22P"
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M141 chummer6-ui translator, XML amendment, and Hero Lab direct route proof is complete; future shards must verify the closed-package receipt, focused guard test, runtime-backed screenshot gates, canonical registry row, and queue mirrors instead of reopening this slice."
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "capture_direct_screenshot_and_runtime_proof_for_translat:ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M141DirectImportRouteProofGuardTests" --no-restore'
EXPECTED_DESIGN_QUEUE_PATH = f"{repo_root}/.codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_SCREENSHOTS = [
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
]
EXPECTED_REVIEW_JOBS = [
    "translator_xml_custom_data",
    "hero_lab_import_oracle",
]
SCREENSHOT_REVIEW_JOB_ALIASES = {
    "translator_xml_custom_data": ["translator", "xml_editor"],
    "hero_lab_import_oracle": ["hero_lab_importer"],
}
EXPECTED_VETERAN_TASK_JOBS = [
    "translator_xml_custom_data",
]
EXPECTED_VETERAN_SCREENSHOT_REVIEW_JOBS: list[str] = []
EXPECTED_ROUTE_RECEIPTS = {
    "translator_xml_custom_data": {
        "routeIds": [
            "translator",
            "xml_editor",
            "source:translator_route",
            "source:xml_amendment_editor_route",
            "family:custom_data_xml_and_translator_bridge",
        ],
        "workflowFamilyId": "improvements-explain-result-parity",
        "screenshots": [
            "38-translator-dialog-light.png",
            "39-xml-editor-dialog-light.png",
        ],
    },
    "hero_lab_import_oracle": {
        "routeIds": [
            "hero_lab_importer",
            "source:hero_lab_importer_route",
            "family:legacy_and_adjacent_import_oracles",
        ],
        "workflowFamilyId": "create-open-import-save-save-as-print-export",
        "screenshots": [
            "40-hero-lab-importer-dialog-light.png",
        ],
    },
}
FLAGSHIP_FRONTIER_ID = 1922169755
if configured_flagship_frontier_id != FLAGSHIP_FRONTIER_ID:
    raise AssertionError(
        f"CHUMMER_FLAGSHIP_FRONTIER_ID must be {FLAGSHIP_FRONTIER_ID}, got {configured_flagship_frontier_id}"
    )
FLAGSHIP_FRONTIER_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]

REGISTRY_MARKERS = [
    "title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes",
    "source:translator_route",
    "source:xml_amendment_editor_route",
    "source:hero_lab_importer_route",
    "family:custom_data_xml_and_translator_bridge",
    "family:legacy_and_adjacent_import_oracles",
    "Direct screenshot-backed and runtime-backed receipts exist for `menu:translator`, `menu:xml_editor`, `menu:hero_lab_importer`,",
    "id: '141.1'",
    "owner: chummer6-ui",
    TITLE,
]

SOURCE_MARKERS = {
    "Chummer.Presentation/Overview/OverviewCommandDispatcher.cs": [
        'if (string.Equals(commandId, "translator", StringComparison.Ordinal))',
        '|| string.Equals(commandId, "translator", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "xml_editor", StringComparison.Ordinal)',
        '|| string.Equals(commandId, "hero_lab_importer", StringComparison.Ordinal)',
    ],
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs": [
        '"dialog.translator"',
        '"translatorLanePosture"',
        '"dialog.xml_editor"',
        '"xmlEditorLanePosture"',
        '"dialog.hero_lab_importer"',
        '"heroLabImportOracleLanePosture"',
        '"heroLabAdjacentSr6OracleReceipt"',
    ],
    "Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs": [
        'Command("translator", "command.translator", "tools", false)',
        'Command("xml_editor", "command.xml_editor", "tools", false)',
        'Command("hero_lab_importer", "command.hero_lab_importer", "tools", false)',
    ],
    "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs": [
        "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
        "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
        "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
    ],
    "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs": [
        "CreateCommandDialog_translator_prefers_catalog_languages_and_surfaces_lane_posture",
        "CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture",
        "CreateCommandDialog_hero_lab_importer_surfaces_import_oracle_and_adjacent_sr6_posture",
    ],
    "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs": [
        "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
        "Avalonia_and_Blazor_hero_lab_importer_dialog_preserves_matching_import_oracle_posture",
    ],
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
        "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
        'GetImportRouteReviewStep("translator").ScreenshotFileName',
        'GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName',
        'GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName',
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh": [
        '"translator": {',
        '"xml_editor": {',
        '"hero_lab_importer": {',
        '"38-translator-dialog-light.png"',
        '"39-xml-editor-dialog-light.png"',
        '"40-hero-lab-importer-dialog-light.png"',
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M141 direct import-route proof guard",
        "bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
    ],
}

DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "operator telemetry",
    "supervisor status",
]
EXPECTED_PROOF = [
    f"{repo_root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs",
    f"{repo_root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs",
    f"{repo_root}/Chummer.Tests/Chummer.Tests.csproj",
    f"{repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
    f"{repo_root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh",
    f"{repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    f"{repo_root}/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh",
    f"{repo_root}/scripts/ai/verify.sh",
    f"{repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json",
    EXPECTED_DIRECT_PROOF_COMMAND,
    EXPECTED_TARGETED_TEST_COMMAND,
]
EXPECTED_REGISTRY_EVIDENCE = [
    f"{repo_root}/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs, {repo_root}/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs, {repo_root}/Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs, and {repo_root}/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs keep the translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle flows bound to direct screenshot-backed and runtime-backed desktop route proof instead of broad family prose.",
    f"{repo_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, {repo_root}/scripts/ai/milestones/veteran-task-time-evidence-gate.sh, {repo_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh, and {repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json keep the direct screenshot pack, runtime-backed route receipts, and published closure proof aligned for translator, XML amendment, Hero Lab importer, and adjacent import-oracle coverage.",
    f"{repo_root}/Chummer.Tests/Compliance/Next90M141DirectImportRouteProofGuardTests.cs, {repo_root}/Chummer.Tests/Chummer.Tests.csproj, {repo_root}/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh, and {repo_root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, verify wiring, or worker-safe flagship frontier evidence drift from the completed package contract.",
    f"{repo_root}/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json records the closed-package receipt for `next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment`.",
]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


@dataclass(frozen=True)
class Snapshot:
    label: str
    path: Path
    resolved_path: Path
    payload: bytes
    device: int
    inode: int
    size: int
    mtime_ns: int
    sha256: str
    max_bytes: int

    def binding(self) -> dict[str, Any]:
        return {
            "path": str(self.path.absolute()),
            "resolvedPath": str(self.resolved_path),
            "sha256": self.sha256,
            "sizeBytes": self.size,
            "device": self.device,
            "inode": self.inode,
            "mtimeNs": self.mtime_ns,
        }


snapshots: list[Snapshot] = []


def _assert_no_untrusted_symlink_components(path: Path, label: str) -> None:
    absolute = path.absolute()
    allowed_alias = repo_root.absolute()
    cursor = Path(absolute.anchor)
    for component in absolute.parts[1:]:
        cursor = cursor / component
        try:
            mode = cursor.lstat().st_mode
        except OSError as exc:
            raise AssertionError(f"{label} cannot inspect path component: {cursor}: {exc}") from exc
        if stat.S_ISLNK(mode) and cursor != allowed_alias:
            raise AssertionError(f"{label} contains a symlink component: {cursor}")


def _strict_resolved(path: Path, label: str) -> Path:
    _assert_no_untrusted_symlink_components(path, label)
    try:
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise AssertionError(f"{label} cannot be resolved as an existing path: {path}: {exc}") from exc
    return resolved


def _is_relative_to(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def require_bound_path(path: Path, root: Path, label: str) -> Path:
    try:
        resolved_root = root.resolve(strict=True)
    except OSError as exc:
        raise AssertionError(f"{label} root cannot be resolved: {root}: {exc}") from exc
    if not resolved_root.is_dir():
        raise AssertionError(f"{label} root is not a directory: {root}")
    resolved_path = _strict_resolved(path, label)
    if not _is_relative_to(resolved_path, resolved_root):
        raise AssertionError(f"{label} escapes its declared root: {path} -> {resolved_path}")
    relative = resolved_path.relative_to(resolved_root)
    cursor = resolved_root
    for component in relative.parts:
        cursor = cursor / component
        try:
            if stat.S_ISLNK(cursor.lstat().st_mode):
                raise AssertionError(f"{label} contains a symlink component: {cursor}")
        except OSError as exc:
            raise AssertionError(f"{label} cannot inspect path component: {cursor}: {exc}") from exc
    return resolved_path


def safe_snapshot(
    path: Path,
    label: str,
    *,
    max_bytes: int,
    required_root: Path | None = None,
) -> Snapshot:
    if required_root is not None:
        expected_resolved = require_bound_path(path, required_root, label)
    else:
        expected_resolved = _strict_resolved(path, label)

    flags = os.O_RDONLY
    if hasattr(os, "O_CLOEXEC"):
        flags |= os.O_CLOEXEC
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except OSError as exc:
        raise AssertionError(f"{label} cannot be opened safely: {path}: {exc}") from exc
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode):
            raise AssertionError(f"{label} is not a regular file: {path}")
        if before.st_size < 0 or before.st_size > max_bytes:
            raise AssertionError(
                f"{label} exceeds the {max_bytes}-byte policy bound: {path} ({before.st_size} bytes)"
            )
        chunks: list[bytes] = []
        remaining = before.st_size
        while remaining:
            chunk = os.read(descriptor, min(1024 * 1024, remaining))
            if not chunk:
                raise AssertionError(f"{label} changed or truncated during read: {path}")
            chunks.append(chunk)
            remaining -= len(chunk)
        if os.read(descriptor, 1):
            raise AssertionError(f"{label} grew during read: {path}")
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)

    identity_before = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
    identity_after = (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
    if identity_before != identity_after:
        raise AssertionError(f"{label} changed during read: {path}")
    payload = b"".join(chunks)
    if len(payload) != before.st_size:
        raise AssertionError(f"{label} byte count changed during read: {path}")
    if path.resolve(strict=True) != expected_resolved:
        raise AssertionError(f"{label} path binding changed during read: {path}")

    snapshot = Snapshot(
        label=label,
        path=path,
        resolved_path=expected_resolved,
        payload=payload,
        device=before.st_dev,
        inode=before.st_ino,
        size=before.st_size,
        mtime_ns=before.st_mtime_ns,
        sha256=hashlib.sha256(payload).hexdigest(),
        max_bytes=max_bytes,
    )
    snapshots.append(snapshot)
    return snapshot


def decode_text(snapshot: Snapshot) -> str:
    try:
        return snapshot.payload.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise AssertionError(f"{snapshot.label} is not valid UTF-8: {snapshot.path}") from exc


def load_json_snapshot(snapshot: Snapshot) -> dict[str, Any]:
    try:
        payload = json.loads(decode_text(snapshot))
    except json.JSONDecodeError as exc:
        raise AssertionError(f"{snapshot.label} is not valid JSON: {snapshot.path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise AssertionError(f"{snapshot.label} JSON root is not an object: {snapshot.path}")
    return payload


def parse_timestamp(value: Any, label: str) -> datetime:
    raw = str(value or "").strip()
    if not raw or not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z", raw):
        raise AssertionError(f"{label} must be a strict UTC RFC3339 timestamp")
    try:
        parsed = datetime.fromisoformat(raw.removesuffix("Z") + "+00:00")
    except ValueError as exc:
        raise AssertionError(f"{label} is not a valid timestamp: {raw}") from exc
    return parsed


def is_fresh(value: Any, label: str, max_age: timedelta, reference: datetime) -> bool:
    try:
        parsed = parse_timestamp(value, label)
    except AssertionError:
        return False
    return reference - max_age <= parsed <= reference + MAX_FUTURE_SKEW


def exact_alias_value(
    payload: dict[str, Any],
    keys: tuple[str, ...],
    label: str,
    *,
    require_all: bool,
) -> str:
    values: list[str] = []
    for key in keys:
        if key not in payload:
            if require_all:
                raise AssertionError(f"{label} is missing required alias {key}")
            continue
        value = str(payload.get(key) or "").strip()
        if not value:
            raise AssertionError(f"{label} alias {key} is blank")
        values.append(value)
    if not values:
        raise AssertionError(f"{label} has no recognized aliases")
    if len(set(values)) != 1:
        raise AssertionError(f"{label} aliases disagree: {values}")
    return values[0]


def supporting_contract(payload: dict[str, Any], expected: str, label: str) -> bool:
    try:
        return exact_alias_value(
            payload,
            ("contract_name", "contractName"),
            f"{label} contract",
            require_all=False,
        ) == expected
    except AssertionError:
        return False


def supporting_release_alignment(
    payload: dict[str, Any],
    channel_id: str,
    release_version: str,
    label: str,
) -> bool:
    try:
        actual_channel = exact_alias_value(
            payload,
            ("channelId", "channel"),
            f"{label} channel",
            require_all=True,
        )
        actual_version = exact_alias_value(
            payload,
            ("version", "releaseVersion"),
            f"{label} version",
            require_all=True,
        )
    except AssertionError:
        return False
    return actual_channel == channel_id and actual_version == release_version


def valid_png(payload: bytes) -> bool:
    if len(payload) < 45 or payload[:8] != b"\x89PNG\r\n\x1a\n":
        return False
    cursor = 8
    chunk_index = 0
    seen_ihdr = False
    seen_idat = False
    seen_iend = False
    while cursor < len(payload):
        if len(payload) - cursor < 12:
            return False
        length = int.from_bytes(payload[cursor : cursor + 4], "big")
        chunk_type = payload[cursor + 4 : cursor + 8]
        cursor += 8
        if length > len(payload) - cursor - 4:
            return False
        data = payload[cursor : cursor + length]
        cursor += length
        expected_crc = int.from_bytes(payload[cursor : cursor + 4], "big")
        cursor += 4
        if zlib.crc32(chunk_type + data) & 0xFFFFFFFF != expected_crc:
            return False
        if chunk_index == 0:
            if chunk_type != b"IHDR" or length != 13:
                return False
            width = int.from_bytes(data[0:4], "big")
            height = int.from_bytes(data[4:8], "big")
            if width <= 0 or height <= 0:
                return False
            seen_ihdr = True
        elif chunk_type == b"IHDR":
            return False
        if chunk_type == b"IDAT":
            seen_idat = True
        if chunk_type == b"IEND":
            if length != 0 or cursor != len(payload):
                return False
            seen_iend = True
            break
        chunk_index += 1
    return seen_ihdr and seen_idat and seen_iend


def revalidate_snapshots() -> None:
    original = list(snapshots)
    snapshots.clear()
    try:
        for expected in original:
            current = safe_snapshot(
                expected.path,
                f"{expected.label} final revalidation",
                max_bytes=expected.max_bytes,
            )
            if (
                current.resolved_path != expected.resolved_path
                or current.device != expected.device
                or current.inode != expected.inode
                or current.size != expected.size
                or current.mtime_ns != expected.mtime_ns
                or current.sha256 != expected.sha256
            ):
                raise AssertionError(f"{expected.label} changed before receipt publication")
    finally:
        snapshots.clear()
        snapshots.extend(original)


def atomic_write_receipt(payload: dict[str, Any]) -> None:
    output_root = receipt_path.parent
    require_bound_path(output_root, repo_root, "receipt output directory")
    if receipt_path.exists() or receipt_path.is_symlink():
        if receipt_path.is_symlink() or not receipt_path.is_file():
            raise AssertionError(f"receipt output must be a regular file or absent: {receipt_path}")
    revalidate_snapshots()
    serialized = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    directory_flags = os.O_RDONLY
    if hasattr(os, "O_DIRECTORY"):
        directory_flags |= os.O_DIRECTORY
    if hasattr(os, "O_CLOEXEC"):
        directory_flags |= os.O_CLOEXEC
    if hasattr(os, "O_NOFOLLOW"):
        directory_flags |= os.O_NOFOLLOW
    directory_fd = os.open(output_root, directory_flags)
    temporary_name = f".{receipt_path.name}.{uuid.uuid4()}.tmp"
    temporary_fd: int | None = None
    try:
        create_flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        if hasattr(os, "O_CLOEXEC"):
            create_flags |= os.O_CLOEXEC
        if hasattr(os, "O_NOFOLLOW"):
            create_flags |= os.O_NOFOLLOW
        temporary_fd = os.open(temporary_name, create_flags, 0o600, dir_fd=directory_fd)
        view = memoryview(serialized)
        while view:
            written = os.write(temporary_fd, view)
            if written <= 0:
                raise OSError("short write while publishing receipt")
            view = view[written:]
        os.fsync(temporary_fd)
        os.close(temporary_fd)
        temporary_fd = None
        os.replace(
            temporary_name,
            receipt_path.name,
            src_dir_fd=directory_fd,
            dst_dir_fd=directory_fd,
        )
        os.fsync(directory_fd)
    finally:
        if temporary_fd is not None:
            os.close(temporary_fd)
        try:
            os.unlink(temporary_name, dir_fd=directory_fd)
        except FileNotFoundError:
            pass
        os.close(directory_fd)


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


def block_for_work_task(text: str, task_id: str) -> str:
    marker = f"- id: '{task_id}'"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing work task row for {task_id}")
    block_start = text.rfind("\n", 0, start)
    block_start = 0 if block_start == -1 else block_start + 1
    next_start = text.find("\n    - id:", start + len(marker))
    return text[block_start:] if next_start == -1 else text[block_start:next_start]


def block_for_milestone(text: str, milestone_id: int) -> str:
    marker = f"  - id: {milestone_id}"
    start = text.find(marker)
    if start == -1:
        raise AssertionError(f"missing milestone row for {milestone_id}")
    next_start = text.find("\n  - id:", start + len(marker))
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
        if line.startswith("        - "):
            items.append(line.removeprefix("        - ").strip())
            continue
        if items:
            if line.startswith("        ") and not line.strip().endswith(":"):
                items[-1] = f"{items[-1]} {line.strip()}"
                continue
            break
        if line.strip():
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


def read_string_list(value: Any) -> list[str]:
    if not isinstance(value, list):
        return []
    return [str(item).strip() for item in value if str(item).strip()]


def normalize_space(value: str) -> str:
    return " ".join(value.split())


CANONICAL_REPO_ROOT_ALIASES = [
    repo_root,
    repo_root.parent / "chummer6-ui",
]


def normalize_repo_root_aliases(value: str) -> str:
    normalized = str(value)
    for alias in CANONICAL_REPO_ROOT_ALIASES:
        normalized = normalized.replace(str(alias), str(repo_root))
    return normalize_space(normalized)


def normalized_string_list(values: list[str]) -> list[str]:
    return [normalize_repo_root_aliases(value) for value in values]


def normalize_proof_entry(value: str) -> str:
    return normalize_repo_root_aliases(value)


def normalized_proof_list(values: list[str]) -> list[str]:
    return [normalize_proof_entry(value) for value in values]


def normalize_repo_relative_proof_entry(value: str) -> str:
    normalized = normalize_proof_entry(value)
    for alias in CANONICAL_REPO_ROOT_ALIASES:
        alias_prefix = f"{alias}/"
        if normalized.startswith(alias_prefix):
            return normalize_space(normalized.removeprefix(alias_prefix))
    repo_prefix = f"{repo_root}/"
    if normalized.startswith(repo_prefix):
        return normalize_space(normalized.removeprefix(repo_prefix))
    return normalized


def proof_lists_match(actual: list[str], expected: list[str]) -> bool:
    return [
        normalize_repo_relative_proof_entry(value)
        for value in actual
    ] == [
        normalize_repo_relative_proof_entry(value)
        for value in expected
    ]


def design_queue_path_matches_expected(path: Path) -> bool:
    expected_candidates = {
        normalize_space(EXPECTED_DESIGN_QUEUE_PATH),
        normalize_space(str(repo_root / ".codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml")),
        normalize_space(str(repo_root.parent / "chummer-design" / "products" / "chummer" / "NEXT_90_DAY_QUEUE_STAGING.generated.yaml")),
    }
    normalized = normalize_space(str(path))
    try:
        resolved = normalize_space(str(path.resolve()))
    except OSError:
        resolved = normalized
    return normalized in expected_candidates or resolved in expected_candidates


reference_time = datetime.now(timezone.utc)

registry_snapshot = safe_snapshot(
    registry_path,
    "next-90 registry",
    max_bytes=MAX_TEXT_BYTES,
    required_root=repo_root,
)
queue_snapshot = safe_snapshot(
    queue_path,
    "next-90 queue",
    max_bytes=MAX_TEXT_BYTES,
    required_root=repo_root,
)
design_queue_snapshot = safe_snapshot(
    design_queue_path,
    "next-90 design queue",
    max_bytes=MAX_TEXT_BYTES,
    required_root=repo_root,
)
registry_text = decode_text(registry_snapshot)
queue_text = decode_text(queue_snapshot)
design_queue_text = decode_text(design_queue_snapshot)
queue_block = block_for_package(queue_text, PACKAGE_ID)
design_queue_block = block_for_package(design_queue_text, PACKAGE_ID)
registry_task_block = block_for_work_task(registry_text, WORK_TASK_ID)
registry_milestone_block = block_for_milestone(registry_text, MILESTONE_ID)

frontier_root_resolved = flagship_frontier_root.resolve(strict=True)
frontier_path_resolved = flagship_frontier_path.resolve(strict=True)
whole_project_frontier = flagship_frontier_root.parent / "FULL_PRODUCT_FRONTIER.generated.yaml"
whole_project_resolved = whole_project_frontier.resolve(strict=False)
frontier_is_direct_shard = (
    frontier_path_resolved.parent == frontier_root_resolved
    and re.fullmatch(r"shard-[1-9]\d*\.generated\.yaml", flagship_frontier_path.name) is not None
)
frontier_is_exact_whole_project = frontier_path_resolved == whole_project_resolved
if not frontier_is_direct_shard and not frontier_is_exact_whole_project:
    raise AssertionError(
        "flagship frontier must be a direct shard under the declared root or its exact whole-project sibling"
    )
frontier_snapshot = safe_snapshot(
    flagship_frontier_path,
    "flagship frontier",
    max_bytes=MAX_TEXT_BYTES,
    required_root=flagship_frontier_root if frontier_is_direct_shard else flagship_frontier_root.parent,
)
flagship_frontier_text = decode_text(frontier_snapshot)
expected_flagship_queue_path = flagship_frontier_root.parent / "NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
flagship_queue_resolved = flagship_queue_path.resolve(strict=True)
if flagship_queue_resolved != expected_flagship_queue_path.resolve(strict=True):
    raise AssertionError(
        "flagship queue must be the exact NEXT_90_DAY_QUEUE_STAGING.generated.yaml sibling of the declared frontier root"
    )
flagship_queue_snapshot = safe_snapshot(
    flagship_queue_path,
    "flagship successor queue",
    max_bytes=MAX_TEXT_BYTES,
    required_root=flagship_frontier_root.parent,
)
flagship_queue_text = decode_text(flagship_queue_snapshot)
flagship_queue_block = block_for_package(flagship_queue_text, PACKAGE_ID)

release_snapshot = safe_snapshot(
    release_channel_path,
    "release channel",
    max_bytes=MAX_JSON_BYTES,
)
release_channel = load_json_snapshot(release_snapshot)
release_contract = ""
release_channel_channel_id = ""
release_channel_version = ""
try:
    release_contract = exact_alias_value(
        release_channel,
        ("contract_name", "contractName"),
        "release channel contract",
        require_all=True,
    )
    release_channel_channel_id = exact_alias_value(
        release_channel,
        ("channelId", "channel"),
        "release channel id",
        require_all=True,
    )
    release_channel_version = exact_alias_value(
        release_channel,
        ("version", "releaseVersion"),
        "release channel version",
        require_all=True,
    )
except AssertionError:
    pass

visual_gate_path = repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
screenshot_review_gate_path = repo_root / ".codex-studio" / "published" / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
veteran_task_gate_path = repo_root / ".codex-studio" / "published" / "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
ui_flagship_gate_path = repo_root / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"

supporting_specs = {
    "visualFamiliarityGate": (
        visual_gate_path,
        "chummer6-ui.desktop_visual_familiarity_exit_gate",
        True,
    ),
    "screenshotReviewGate": (
        screenshot_review_gate_path,
        "chummer6-ui.chummer5a_screenshot_review_gate",
        True,
    ),
    "veteranTaskTimeGate": (
        veteran_task_gate_path,
        "chummer6-ui.veteran_task_time_evidence_gate",
        False,
    ),
    "uiFlagshipReleaseGate": (
        ui_flagship_gate_path,
        "chummer6-ui.flagship_ui_release_gate",
        True,
    ),
}
supporting_snapshots: dict[str, Snapshot] = {}
supporting_payloads: dict[str, dict[str, Any]] = {}
for label, (path, _contract, _release_scoped) in supporting_specs.items():
    snapshot = safe_snapshot(
        path,
        label,
        max_bytes=MAX_JSON_BYTES,
        required_root=repo_root,
    )
    supporting_snapshots[label] = snapshot
    supporting_payloads[label] = load_json_snapshot(snapshot)

visual_gate = supporting_payloads["visualFamiliarityGate"]
screenshot_review_gate = supporting_payloads["screenshotReviewGate"]
veteran_task_gate = supporting_payloads["veteranTaskTimeGate"]
ui_flagship_gate = supporting_payloads["uiFlagshipReleaseGate"]

visual_evidence = visual_gate.get("evidence")
if not isinstance(visual_evidence, dict):
    visual_evidence = {}
screenshot_review_evidence = screenshot_review_gate.get("evidence")
if not isinstance(screenshot_review_evidence, dict):
    screenshot_review_evidence = {}
veteran_task_evidence = veteran_task_gate.get("evidence")
if not isinstance(veteran_task_evidence, dict):
    veteran_task_evidence = {}
ui_flagship_gate_direct_import_route_proof = ui_flagship_gate.get("directImportRouteProof")
if not isinstance(ui_flagship_gate_direct_import_route_proof, dict):
    ui_flagship_gate_direct_import_route_proof = {}

reviewed_jobs = set(read_string_list(screenshot_review_evidence.get("reviewedJobs")))
covered_jobs = set(read_string_list(veteran_task_evidence.get("coveredJobs")))
screenshot_review_jobs = set(read_string_list(veteran_task_evidence.get("screenshotReviewJobs")))
required_screenshots = set(read_string_list(visual_evidence.get("required_screenshots")))
missing_screenshots = set(read_string_list(visual_evidence.get("missing_screenshots")))
screenshot_dir_raw = str(visual_evidence.get("screenshot_dir") or "").strip()
screenshot_dir = Path(screenshot_dir_raw) if screenshot_dir_raw else Path("/__missing_m141_screenshot_directory__")
expected_screenshot_dir = repo_root / ".codex-studio" / "published" / "ui-flagship-release-gate-screenshots"
screenshot_dir_exact = False
try:
    screenshot_dir_exact = (
        require_bound_path(screenshot_dir, repo_root, "screenshot directory")
        == expected_screenshot_dir.resolve(strict=True)
    )
except AssertionError:
    screenshot_dir_exact = False
route_local_receipts = screenshot_review_evidence.get("routeLocalReceipts")
if not isinstance(route_local_receipts, dict):
    route_local_receipts = {}

ui_flagship_gate_review_jobs = read_string_list(ui_flagship_gate_direct_import_route_proof.get("reviewJobs"))
ui_flagship_gate_screenshots = read_string_list(ui_flagship_gate_direct_import_route_proof.get("screenshots"))
ui_flagship_gate_presenter_tests = read_string_list(
    ui_flagship_gate_direct_import_route_proof.get("characterOverviewPresenterTests")
)

queue_checks = {
    "registry_markers_present": all(marker in registry_text for marker in REGISTRY_MARKERS),
    "registry_milestone_present": f"  - id: {MILESTONE_ID}" in registry_text,
    "registry_milestone_title_matches": "title: Direct parity proof for translator, XML amendment, Hero Lab, and adjacent import routes" in registry_milestone_block,
    "registry_task_unique": registry_text.count(f"- id: '{WORK_TASK_ID}'") == 1,
    "registry_task_owner_matches": "owner: chummer6-ui" in registry_task_block,
    "registry_task_title_matches": f"title: {TITLE}" in registry_task_block,
    "registry_task_status_complete": f"status: {EXPECTED_STATUS}" in registry_task_block,
    "registry_task_completion_action_matches": f"completion_action: {EXPECTED_COMPLETION_ACTION}" in registry_task_block,
    "registry_task_do_not_reopen_reason_matches": yaml_wrapped_scalar(registry_task_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "registry_task_evidence_exact": normalized_string_list(yaml_list_after(registry_task_block, "evidence")) == normalized_string_list(EXPECTED_REGISTRY_EVIDENCE),
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
    "queue_status_complete": yaml_scalar(queue_block, "status") == EXPECTED_STATUS,
    "design_queue_status_complete": yaml_scalar(design_queue_block, "status") == EXPECTED_STATUS,
    "queue_wave_matches": yaml_scalar(queue_block, "wave") == WAVE,
    "design_queue_wave_matches": yaml_scalar(design_queue_block, "wave") == WAVE,
    "queue_repo_matches": yaml_scalar(queue_block, "repo") == "chummer6-ui",
    "design_queue_repo_matches": yaml_scalar(design_queue_block, "repo") == "chummer6-ui",
    "queue_completion_action_matches": yaml_scalar(queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "design_queue_completion_action_matches": yaml_scalar(design_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "design_queue_do_not_reopen_reason_matches": yaml_wrapped_scalar(design_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "queue_proof_exact": proof_lists_match(yaml_list_after(queue_block, "proof"), EXPECTED_PROOF),
    "design_queue_proof_exact": proof_lists_match(yaml_list_after(design_queue_block, "proof"), EXPECTED_PROOF),
    "allowed_paths_exact": yaml_list_after(queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "design_allowed_paths_exact": yaml_list_after(design_queue_block, "allowed_paths") == EXPECTED_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "design_owned_surfaces_exact": yaml_list_after(design_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "queue_design_block_parity": queue_block == design_queue_block,
    "design_queue_path_matches": design_queue_path_matches_expected(design_queue_path),
    "queue_worker_safe": all(token.lower() not in queue_block.lower() for token in DISALLOWED_PROOF_TOKENS),
    "design_queue_worker_safe": all(token.lower() not in design_queue_block.lower() for token in DISALLOWED_PROOF_TOKENS),
}
normalized_flagship_frontier_text = normalize_space(flagship_frontier_text)
flagship_frontier_path_is_whole_project = flagship_frontier_path.name == "FULL_PRODUCT_FRONTIER.generated.yaml"

flagship_frontier_checks = {
    "frontier_artifact_present": bool(flagship_frontier_text),
    "frontier_artifact_path_under_root": frontier_is_direct_shard or frontier_is_exact_whole_project,
    "frontier_artifact_uses_shard_generated_yaml": (
        frontier_is_direct_shard
    ) or flagship_frontier_path_is_whole_project,
    "contract_exact": "contract_name: fleet.full_product_frontier" in flagship_frontier_text,
    "schema_version_exact": "schema_version: 1" in flagship_frontier_text,
    "mode_exact": "mode: flagship_product" in flagship_frontier_text,
    "quality_bar_exact": "bar: top_flagship_grade" in flagship_frontier_text,
    "whole_project_frontier_required": "whole_project_frontier: true" in flagship_frontier_text,
    "lowered_standards_rejected": "accept_lowered_standards: false" in flagship_frontier_text,
    "worker_safe": all(token.lower() not in flagship_frontier_text.lower() for token in DISALLOWED_PROOF_TOKENS),
}
deterministic_flagship_frontier_id = 950_000_000 + int(
    hashlib.sha1(PACKAGE_ID.encode("utf-8")).hexdigest()[:8],
    16,
)
flagship_queue_checks = {
    "queue_artifact_present": bool(flagship_queue_text),
    "queue_artifact_path_exact": flagship_queue_resolved == expected_flagship_queue_path.resolve(strict=True),
    "mode_exact": yaml_scalar(flagship_queue_text, "mode") == "append",
    "status_exact": yaml_scalar(flagship_queue_text, "status") == "live_parallel_successor",
    "source_registry_present": "NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml" in flagship_queue_text,
    "activation_rule_worker_safe": (
        "Use these items immediately on shards" in normalized_string_list([flagship_queue_text])[0]
        and "do not let them replace active shards" in normalized_string_list([flagship_queue_text])[0]
    ),
    "package_unique": flagship_queue_text.count(f"package_id: {PACKAGE_ID}") == 1,
    "title_matches": yaml_wrapped_scalar(flagship_queue_block, "title") == TITLE,
    "task_matches": yaml_wrapped_scalar(flagship_queue_block, "task") == TASK,
    "package_frontier_matches": yaml_scalar(flagship_queue_block, "frontier_id") == str(FRONTIER_ID),
    "work_task_matches": yaml_scalar(flagship_queue_block, "work_task_id") == WORK_TASK_ID,
    "milestone_matches": yaml_scalar(flagship_queue_block, "milestone_id") == str(MILESTONE_ID),
    "status_complete": yaml_scalar(flagship_queue_block, "status") == EXPECTED_STATUS,
    "wave_matches": yaml_scalar(flagship_queue_block, "wave") == WAVE,
    "repo_matches": yaml_scalar(flagship_queue_block, "repo") == "chummer6-ui",
    "completion_action_matches": yaml_scalar(flagship_queue_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "do_not_reopen_reason_matches": yaml_wrapped_scalar(flagship_queue_block, "do_not_reopen_reason") == EXPECTED_DO_NOT_REOPEN_REASON,
    "proof_exact": proof_lists_match(yaml_list_after(flagship_queue_block, "proof"), EXPECTED_PROOF),
    "allowed_paths_exact": yaml_list_after(flagship_queue_block, "allowed_paths") == FLAGSHIP_FRONTIER_ALLOWED_PATHS,
    "owned_surfaces_exact": yaml_list_after(flagship_queue_block, "owned_surfaces") == EXPECTED_SURFACES,
    "worker_safe": all(token.lower() not in flagship_queue_block.lower() for token in DISALLOWED_PROOF_TOKENS),
    "deterministic_frontier_id_matches": deterministic_flagship_frontier_id == FLAGSHIP_FRONTIER_ID,
}

source_checks: dict[str, dict[str, bool]] = {}
source_bindings: dict[str, dict[str, Any]] = {}
for relative_path, markers in SOURCE_MARKERS.items():
    source_snapshot = safe_snapshot(
        repo_root / relative_path,
        f"source {relative_path}",
        max_bytes=MAX_TEXT_BYTES,
        required_root=repo_root,
    )
    source_text = decode_text(source_snapshot)
    source_checks[relative_path] = {marker: marker in source_text for marker in markers}
    source_bindings[relative_path] = source_snapshot.binding()

proof_file_bindings: dict[str, dict[str, Any]] = {}
informational_output_path = str(receipt_path)
for proof_path_text in EXPECTED_PROOF[:-2]:
    proof_path = Path(proof_path_text)
    if proof_path.absolute() == receipt_path.absolute():
        continue
    proof_snapshot = safe_snapshot(
        proof_path,
        f"proof file {proof_path.name}",
        max_bytes=MAX_TEXT_BYTES,
        required_root=repo_root,
    )
    proof_file_bindings[str(proof_path)] = proof_snapshot.binding()

release_checks = {
    "schema_version_exact": type(release_channel.get("schemaVersion")) is int
    and release_channel.get("schemaVersion") == 1,
    "contract_exact": release_contract == "Chummer.Hub.Registry.Contracts",
    "status_exact": release_channel.get("status") == "published",
    "channel_aliases_present_and_agree": bool(release_channel_channel_id),
    "version_aliases_present_and_agree": bool(release_channel_version),
    "generated_at_fresh": is_fresh(
        release_channel.get("generatedAt"),
        "release channel generatedAt",
        RELEASE_MAX_AGE,
        reference_time,
    ),
}

supporting_receipt_checks: dict[str, dict[str, bool]] = {}
for label, (_path, contract, release_scoped) in supporting_specs.items():
    payload = supporting_payloads[label]
    supporting_receipt_checks[label] = {
        "contract_exact": supporting_contract(payload, contract, label),
        "status_exact": payload.get("status") == "pass",
        "generated_at_fresh": is_fresh(
            payload.get("generatedAt"),
            f"{label} generatedAt",
            SUPPORTING_RECEIPT_MAX_AGE,
            reference_time,
        ),
        "release_aligned": (
            supporting_release_alignment(
                payload,
                release_channel_channel_id,
                release_channel_version,
                label,
            )
            if release_scoped
            else True
        ),
    }

receipt_checks: dict[str, Any] = {
    "release_channel_id_present": bool(release_channel_channel_id),
    "release_channel_version_present": bool(release_channel_version),
    "visual_familiarity_gate_pass": visual_gate.get("status") == "pass",
    "visual_required_screenshots_present": all(name in required_screenshots for name in EXPECTED_SCREENSHOTS),
    "visual_missing_screenshots_clear": all(name not in missing_screenshots for name in EXPECTED_SCREENSHOTS),
    "visual_screenshot_dir_exists": screenshot_dir_exact,
    "screenshot_review_gate_pass": screenshot_review_gate.get("status") == "pass",
    "screenshot_review_jobs_present": all(
        all(alias in reviewed_jobs for alias in SCREENSHOT_REVIEW_JOB_ALIASES.get(job, [job]))
        for job in EXPECTED_REVIEW_JOBS
    ),
    "veteran_task_gate_pass": veteran_task_gate.get("status") == "pass",
    "veteran_task_jobs_present": all(job in covered_jobs for job in EXPECTED_VETERAN_TASK_JOBS),
    "veteran_task_screenshot_jobs_present": all(
        job in screenshot_review_jobs for job in EXPECTED_VETERAN_SCREENSHOT_REVIEW_JOBS
    ),
    "ui_flagship_gate_pass": ui_flagship_gate.get("status") == "pass",
    "ui_flagship_gate_tokens_present": (
        ui_flagship_gate_review_jobs == EXPECTED_REVIEW_JOBS
        and ui_flagship_gate_screenshots == EXPECTED_SCREENSHOTS
        and ui_flagship_gate_presenter_tests == [
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
        ]
    ),
}

screenshot_files: dict[str, bool] = {}
screenshot_bindings: dict[str, dict[str, Any]] = {}
for name in EXPECTED_SCREENSHOTS:
    screenshot_ok = False
    if screenshot_dir_exact:
        try:
            screenshot_snapshot = safe_snapshot(
                screenshot_dir / name,
                f"screenshot {name}",
                max_bytes=MAX_PNG_BYTES,
                required_root=expected_screenshot_dir,
            )
            screenshot_ok = valid_png(screenshot_snapshot.payload)
            screenshot_bindings[name] = screenshot_snapshot.binding()
        except AssertionError:
            screenshot_ok = False
    screenshot_files[name] = screenshot_ok

route_receipt_checks: dict[str, Any] = {}
for route_key, expected in EXPECTED_ROUTE_RECEIPTS.items():
    route_receipt = route_local_receipts.get(route_key)
    if not isinstance(route_receipt, dict):
        route_receipt = {}
    route_receipt_checks[route_key] = {
        "exists": bool(route_receipt),
        "status_pass": route_receipt.get("status") == "pass",
        "route_ids_exact": read_string_list(route_receipt.get("routeIds")) == expected["routeIds"],
        "workflow_family_matches": str(route_receipt.get("workflowFamilyId") or "").strip() == expected["workflowFamilyId"],
        "screenshots_exact": read_string_list(route_receipt.get("screenshots")) == expected["screenshots"],
    }

failed: list[str] = []
failed.extend(name for name, ok in queue_checks.items() if not ok)
failed.extend(
    f"flagship_frontier:{name}"
    for name, ok in flagship_frontier_checks.items()
    if not ok
)
failed.extend(
    f"flagship_queue:{name}"
    for name, ok in flagship_queue_checks.items()
    if not ok
)
failed.extend(f"release_channel:{name}" for name, ok in release_checks.items() if not ok)
for label, checks in supporting_receipt_checks.items():
    failed.extend(f"{label}:{name}" for name, ok in checks.items() if not ok)
for relative_path, marker_checks in source_checks.items():
    failed.extend(
        f"{relative_path}:{marker}"
        for marker, ok in marker_checks.items()
        if not ok
    )
failed.extend(name for name, ok in receipt_checks.items() if not ok)
failed.extend(name for name, ok in screenshot_files.items() if not ok)
for route_key, checks in route_receipt_checks.items():
    failed.extend(
        f"{route_key}:{name}"
        for name, ok in checks.items()
        if not ok
    )

receipt = {
    "schemaVersion": 1,
    "producerRunId": str(uuid.uuid4()),
    "generatedAt": now_iso(),
    "status": "pass" if not failed else "fail",
    "unresolved": failed,
    "contract_name": "chummer6-ui.next90_m141_ui_direct_import_route_proof",
    "channelId": release_channel_channel_id,
    "channel": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "version": release_channel_version,
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
        "queueChecks": queue_checks,
        "flagshipFrontierId": FLAGSHIP_FRONTIER_ID,
        "flagshipFrontierChecks": flagship_frontier_checks,
        "flagshipQueueChecks": flagship_queue_checks,
        "releaseChannelChecks": release_checks,
        "supportingReceiptChecks": supporting_receipt_checks,
        "sourceChecks": source_checks,
        "supportingReceipts": {
            "visualFamiliarityGate": str(visual_gate_path),
            "screenshotReviewGate": str(screenshot_review_gate_path),
            "veteranTaskTimeGate": str(veteran_task_gate_path),
            "uiFlagshipReleaseGate": str(ui_flagship_gate_path),
            "releaseChannel": str(release_channel_path),
            "flagshipFrontier": str(flagship_frontier_path),
            "flagshipQueue": str(flagship_queue_path),
        },
        "receiptChecks": receipt_checks,
        "routeReceiptChecks": route_receipt_checks,
        "expectedScreenshots": EXPECTED_SCREENSHOTS,
        "screenshotFiles": screenshot_files,
        "proofFiles": [path for path in EXPECTED_PROOF[:-2] if Path(path).absolute() != receipt_path.absolute()],
        "informationalOutputPath": informational_output_path,
        "proofCommands": EXPECTED_PROOF[-2:],
        "bindings": {
            "registry": registry_snapshot.binding(),
            "queue": queue_snapshot.binding(),
            "designQueue": design_queue_snapshot.binding(),
            "releaseChannel": release_snapshot.binding(),
            "flagshipFrontier": frontier_snapshot.binding(),
            "flagshipQueue": flagship_queue_snapshot.binding(),
            "supportingReceipts": {
                label: snapshot.binding()
                for label, snapshot in supporting_snapshots.items()
            },
            "sources": source_bindings,
            "proofFiles": proof_file_bindings,
            "screenshots": screenshot_bindings,
        },
    },
}

atomic_write_receipt(receipt)

if failed:
    raise SystemExit("\n".join(failed))
PY
