#!/usr/bin/env python3
"""Fail-closed contracts for native-Windows capture and human finalization.

This module deliberately has no network, release, or publication code.  The
preflight command authenticates the canonical handoff/API claims, validates the
exact five-file exporter tree, and derives every candidate byte binding before
executable use.  The capture command repeats that validation, preserves the
exporter receipt/inventory, validates evidence already produced on a native
Windows runner, and inventories it.  The finalize command revalidates that
immutable capture, authenticates an allowlisted human who is not the automated
capture actor, and emits the
visual-proof JSON consumed by the preview-nightly stage contract.
"""

from __future__ import annotations

import argparse
import binascii
import hashlib
import json
import os
import re
import shutil
import stat
import struct
import sys
import zipfile
import zlib
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any


CAPTURE_CONTRACT = "chummer6-ui.preview-nightly-native-windows-capture"
CAPTURE_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-native-windows-capture-inventory"
FINALIZATION_CONTRACT = "chummer6-ui.preview-nightly-native-windows-finalization"
FINALIZED_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-native-windows-finalized-inventory"
VISUAL_PROOF_CONTRACT = "chummer6-ui.windows_installer_visual_proof"
NATIVE_HOST_CONTRACT = "chummer6-ui.native_windows_host_evidence"
CAPTURE_FILE = "WINDOWS_NATIVE_CAPTURE.generated.json"
CAPTURE_INVENTORY_FILE = "WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json"
FINALIZATION_FILE = "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
FINALIZED_INVENTORY_FILE = "WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json"
CAPTURE_WORKFLOW = ".github/workflows/windows-native-evidence-capture.yml"
FINALIZE_WORKFLOW = ".github/workflows/windows-native-evidence-finalize.yml"
PRODUCER_WORKFLOW = ".github/workflows/preview-nightly-candidate-export.yml"
PRODUCER_REF = "refs/heads/main"
CANDIDATE_HANDOFF_CONTRACT = "chummer6-ui.preview-nightly-candidate-handoff"
CANDIDATE_API_CONTRACT = "chummer6-ui.preview-nightly-candidate-authenticated-api"
CANDIDATE_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-candidate-content-inventory"
CANDIDATE_EXPORT_CONTRACT = "chummer6-ui.preview-nightly-candidate-export"
HELD_SNAPSHOT_CONTRACT = "chummer6-ui.preview-nightly-candidate-held-snapshot"
CANDIDATE_MANIFEST_CONTRACT = "Chummer.Hub.Registry.Contracts"
CANDIDATE_MANIFEST_FILE = "RELEASE_CHANNEL.generated.json"
CANDIDATE_INVENTORY_FILE = "PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json"
CANDIDATE_EXPORT_FILE = "PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json"
CANDIDATE_PROVENANCE_DIRECTORY = "candidate-provenance"
# The promoted preview surface has one primary Windows head. Compatibility
# fallback heads require their own independently declared candidate scope and
# are not inferred from ambient producer files.
HEADS = ("avalonia",)
REGISTRY_REQUIRED_DESKTOP_PLATFORMS = ("linux", "windows")
ACTIVE_PREVIEW_DESKTOP_PLATFORMS = ("linux", "windows")
ACTIVE_PREVIEW_DESKTOP_TUPLES = (
    ("avalonia", "linux", "linux-x64"),
    ("avalonia", "windows", "win-x64"),
)
ACTIVE_PREVIEW_DESKTOP_ARTIFACT_IDENTITIES = {
    ("avalonia", "linux", "linux-x64"): (
        "avalonia-linux-x64-installer",
        "chummer-avalonia-linux-x64-installer.deb",
    ),
    ("avalonia", "windows", "win-x64"): (
        "avalonia-win-x64-installer",
        "chummer-avalonia-win-x64-installer.exe",
    ),
}
RID = "win-x64"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
PREFIXED_SHA256_RE = re.compile(r"^sha256:([0-9a-f]{64})$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9.][A-Za-z0-9._/@+-]{0,255}$")
REVIEWER_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,38})$")
GITHUB_LOGIN_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?|github-actions\[bot\])$"
)
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
GITHUB_TIMESTAMP_RE = re.compile(r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
FULL_REF_RE = re.compile(r"^refs/(?:heads|tags)/[A-Za-z0-9.][A-Za-z0-9._/@+-]{0,238}$")
PASSING = {"pass", "passed", "ready"}
PROGRESS_MARKERS = (
    "Bootstrap temp root:",
    "Payload download target:",
    "Downloading application files",
    "Verifying payload size",
    "Verifying payload checksum",
    "Extracting application files",
    "Install complete",
)
PROGRESS_FAILURE_MARKERS = (
    "Payload download failed:",
    "Bundled curl download failed",
    "bundled curl download timed out",
    "bundled curl downloader did not start",
    "bundled curl completed without creating the payload file",
    "Chummer could not download the application files.",
)
MAX_CANDIDATE_ARCHIVE_BYTES = 2 * 1024 * 1024 * 1024
MAX_CANDIDATE_MEMBER_BYTES = 2 * 1024 * 1024 * 1024
MAX_CANDIDATE_EXPANDED_BYTES = 4 * 1024 * 1024 * 1024
MAX_CANDIDATE_JSON_BYTES = 8 * 1024 * 1024
MAX_CANDIDATE_COMPRESSION_RATIO = 8
MAX_CANDIDATE_COMPRESSION_SLACK = 1024 * 1024
JSON_CANDIDATE_PATHS = {
    CANDIDATE_MANIFEST_FILE,
    CANDIDATE_INVENTORY_FILE,
    CANDIDATE_EXPORT_FILE,
}


def candidate_installer_path(head: str) -> str:
    return f"files/chummer-{head}-{RID}-installer.exe"


def candidate_payload_path(head: str) -> str:
    return f"files/chummer-{head}-{RID}-payload.zip"


CANDIDATE_CONTENT_PATHS = (
    CANDIDATE_MANIFEST_FILE,
    *(
        path
        for head in HEADS
        for path in (candidate_installer_path(head), candidate_payload_path(head))
    ),
)
CANDIDATE_EXPORT_PATHS = (
    *CANDIDATE_CONTENT_PATHS,
    CANDIDATE_INVENTORY_FILE,
    CANDIDATE_EXPORT_FILE,
)


class ContractError(RuntimeError):
    pass


@dataclass(frozen=True)
class RegularFileSnapshot:
    relative_path: str
    sha256: str
    size_bytes: int
    data: bytes | None = None


def fail(message: str) -> None:
    raise ContractError(message)


def norm(value: object) -> str:
    return str(value or "").strip().lower()


def require_portable(value: str, label: str) -> str:
    value = str(value or "").strip()
    if not PORTABLE_RE.fullmatch(value):
        fail(f"{label} is missing or is not a portable identifier")
    return value


def require_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        fail(f"{label} must be an exact lowercase SHA-256")
    return value


def require_prefixed_sha256(value: object, label: str) -> str:
    match = PREFIXED_SHA256_RE.fullmatch(value) if isinstance(value, str) else None
    if match is None:
        fail(f"{label} must be an exact lowercase sha256:<hex> digest")
    return match.group(1)


def require_commit(value: str, label: str) -> str:
    value = str(value or "")
    if not COMMIT_RE.fullmatch(value):
        fail(f"{label} must be an exact 40-character commit SHA")
    return value


def require_full_ref(value: str, label: str) -> str:
    value = str(value or "")
    components = value.split("/")[2:]
    if (
        not FULL_REF_RE.fullmatch(value)
        or not components
        or "//" in value
        or ".." in value
        or "@{" in value
        or value.endswith(("/", ".", ".lock"))
        or any(component.startswith(".") for component in components)
        or any(component.lower().endswith(".lock") for component in components)
    ):
        fail(f"{label} must be an exact full refs/heads/... or refs/tags/... ref")
    return value


def require_positive_integer(value: object, label: str) -> str:
    if not isinstance(value, str) or not POSITIVE_INTEGER_RE.fullmatch(value):
        fail(f"{label} must be an exact positive integer string")
    if int(value) > 9_007_199_254_740_991:
        fail(f"{label} exceeds exact GitHub API integer authority")
    return value


def require_github_login(value: object, label: str) -> str:
    if not isinstance(value, str) or not GITHUB_LOGIN_RE.fullmatch(value):
        fail(f"{label} must be an exact GitHub login")
    return value


def require_exact_keys(payload: object, keys: set[str], label: str) -> dict[str, Any]:
    if not isinstance(payload, dict) or set(payload) != keys:
        fail(f"{label} has missing or extra fields")
    return payload


def parse_canonical_json(raw: object, keys: set[str], label: str) -> dict[str, Any]:
    if not isinstance(raw, str):
        fail(f"{label} must be an exact JSON string")
    try:
        payload = json.loads(raw)
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")
    payload = require_exact_keys(payload, keys, label)
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    if raw != canonical:
        fail(f"{label} must use exact canonical JSON serialization")
    return payload


def require_exact_string(payload: dict[str, Any], key: str, expected: str, label: str) -> None:
    if payload.get(key) != expected or not isinstance(payload.get(key), str):
        fail(f"{label} {key} must be exactly {expected!r}")


def require_positive_size(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        fail(f"{label} must be a positive byte count")
    return value


def parse_github_timestamp(value: object, label: str) -> tuple[str, datetime]:
    if not isinstance(value, str) or not GITHUB_TIMESTAMP_RE.fullmatch(value):
        fail(f"{label} must be an exact UTC timestamp")
    try:
        parsed = datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=UTC)
    except ValueError as exc:
        fail(f"{label} must be an exact UTC timestamp: {exc}")
    return value, parsed


def require_future_timestamp(value: object, label: str) -> tuple[str, datetime]:
    value, parsed = parse_github_timestamp(value, label)
    if parsed <= datetime.now(UTC):
        fail(f"{label} is not in the future")
    return value, parsed


def read_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid JSON object at {path}: {exc}")
    if not isinstance(payload, dict):
        fail(f"expected a JSON object at {path}")
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def regular_identity(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_nlink,
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_ctime_ns,
    )


def require_absolute_private_root(root_value: Path, label: str) -> Path:
    if not root_value.is_absolute() or root_value.is_symlink() or not root_value.is_dir():
        fail(f"{label} must be an absolute non-symlink directory")
    return root_value.resolve(strict=True)


def exact_relative_parts(relative: str, label: str) -> tuple[str, ...]:
    if not isinstance(relative, str) or not relative or "\\" in relative or "\x00" in relative:
        fail(f"{label} must be an exact portable relative path")
    parsed = PurePosixPath(relative)
    if parsed.is_absolute() or not parsed.parts or any(part in {"", ".", ".."} for part in parsed.parts):
        fail(f"{label} must be an exact portable relative path")
    if parsed.as_posix() != relative:
        fail(f"{label} must be an exact portable relative path")
    return parsed.parts


def open_regular_beneath(root_value: Path, relative: str, label: str) -> int:
    """Open one exact regular file without following a candidate-controlled link."""

    root = require_absolute_private_root(root_value, "candidate held root")
    parts = exact_relative_parts(relative, label)
    no_follow = getattr(os, "O_NOFOLLOW", 0)
    directory_flag = getattr(os, "O_DIRECTORY", 0)
    descriptor = -1
    directory_descriptors: list[int] = []
    try:
        if os.open in os.supports_dir_fd:
            current = os.open(root, os.O_RDONLY | directory_flag | no_follow)
            directory_descriptors.append(current)
            for part in parts[:-1]:
                current = os.open(
                    part,
                    os.O_RDONLY | directory_flag | no_follow,
                    dir_fd=current,
                )
                directory_descriptors.append(current)
            descriptor = os.open(parts[-1], os.O_RDONLY | no_follow, dir_fd=current)
        else:
            current_path = root
            for part in parts[:-1]:
                current_path = current_path / part
                metadata = os.stat(current_path, follow_symlinks=False)
                if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                    fail(f"{label} has a linked or non-directory parent")
            target = current_path / parts[-1]
            unresolved = os.stat(target, follow_symlinks=False)
            if stat.S_ISLNK(unresolved.st_mode):
                fail(f"{label} cannot be a symlink")
            descriptor = os.open(target, os.O_RDONLY | no_follow)
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
            fail(f"{label} must be one private regular file")
        return descriptor
    except ContractError:
        if descriptor >= 0:
            os.close(descriptor)
        raise
    except (OSError, ValueError) as exc:
        if descriptor >= 0:
            os.close(descriptor)
        fail(f"could not open exact {label}: {exc}")
    finally:
        for current in reversed(directory_descriptors):
            os.close(current)


def snapshot_regular_beneath(
    root: Path, relative: str, label: str, *, include_data: bool = False
) -> RegularFileSnapshot:
    descriptor = open_regular_beneath(root, relative, label)
    chunks: list[bytes] | None = [] if include_data else None
    digest = hashlib.sha256()
    try:
        before = os.fstat(descriptor)
        if include_data and before.st_size > MAX_CANDIDATE_JSON_BYTES:
            fail(f"{label} exceeds the fixed JSON byte bound")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
                if chunks is not None:
                    chunks.append(chunk)
            after = os.fstat(handle.fileno())
        if regular_identity(before) != regular_identity(after):
            fail(f"{label} changed while its exact bytes were read")
        return RegularFileSnapshot(
            relative_path=relative,
            sha256=digest.hexdigest(),
            size_bytes=after.st_size,
            data=b"".join(chunks) if chunks is not None else None,
        )
    except OSError as exc:
        fail(f"could not read exact {label}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def json_from_snapshot(snapshot: RegularFileSnapshot, label: str) -> dict[str, Any]:
    if snapshot.data is None:
        fail(f"{label} was not captured from its validated descriptor")
    try:
        payload = json.loads(snapshot.data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def safe_file(root: Path, relative: str, label: str) -> Path:
    if not relative or Path(relative).is_absolute():
        fail(f"{label} must be an evidence-root-relative path")
    root = root.resolve()
    unresolved = root / relative
    if unresolved.is_symlink():
        fail(f"{label} cannot be a symlink")
    candidate = unresolved.resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        fail(f"{label} escapes its evidence root")
    if not candidate.is_file():
        fail(f"{label} is missing or is not a regular file: {relative}")
    return candidate


def validate_png(path: Path, label: str) -> tuple[int, int]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        fail(f"{label} is not a PNG")
    offset = 8
    ihdr: tuple[int, int, int, int, int] | None = None
    compressed = bytearray()
    saw_iend = False
    while offset < len(data):
        if offset + 12 > len(data):
            fail(f"{label} has a truncated PNG chunk")
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        end = offset + 12 + length
        if length > 64 * 1024 * 1024 or end > len(data):
            fail(f"{label} has an invalid PNG chunk length")
        chunk_data = data[offset + 8 : offset + 8 + length]
        expected_crc = struct.unpack(">I", data[offset + 8 + length : end])[0]
        actual_crc = binascii.crc32(chunk_type + chunk_data) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            fail(f"{label} has a corrupt PNG chunk")
        if offset == 8 and chunk_type != b"IHDR":
            fail(f"{label} does not begin with IHDR")
        if chunk_type == b"IHDR":
            if ihdr is not None or length != 13:
                fail(f"{label} has an invalid IHDR")
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", chunk_data
            )
            if not (320 <= width <= 16384 and 200 <= height <= 16384):
                fail(f"{label} dimensions are outside 320x200..16384x16384")
            if compression != 0 or filtering != 0 or interlace != 0:
                fail(f"{label} uses unsupported PNG encoding")
            allowed_depths = {0: {1, 2, 4, 8, 16}, 2: {8, 16}, 3: {1, 2, 4, 8}, 4: {8, 16}, 6: {8, 16}}
            if bit_depth not in allowed_depths.get(color_type, set()):
                fail(f"{label} uses an invalid PNG color/depth combination")
            ihdr = (width, height, bit_depth, color_type, interlace)
        elif chunk_type == b"IDAT":
            if ihdr is None or saw_iend:
                fail(f"{label} has an out-of-order IDAT")
            compressed.extend(chunk_data)
            if len(compressed) > 64 * 1024 * 1024:
                fail(f"{label} compressed pixels exceed the evidence limit")
        elif chunk_type == b"IEND":
            if length != 0 or saw_iend:
                fail(f"{label} has an invalid IEND")
            saw_iend = True
            if end != len(data):
                fail(f"{label} has trailing bytes after IEND")
        offset = end
    if ihdr is None or not compressed or not saw_iend:
        fail(f"{label} is missing required PNG chunks")
    width, height, bit_depth, color_type, _ = ihdr
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    row_bytes = (width * channels * bit_depth + 7) // 8
    expected_pixel_bytes = height * (row_bytes + 1)
    try:
        decoder = zlib.decompressobj()
        pixels = decoder.decompress(bytes(compressed), expected_pixel_bytes + 1)
        if decoder.unconsumed_tail or len(pixels) > expected_pixel_bytes:
            fail(f"{label} expands beyond its declared PNG dimensions")
        if not decoder.eof:
            remaining = expected_pixel_bytes + 1 - len(pixels)
            pixels += decoder.flush(remaining)
    except zlib.error as exc:
        fail(f"{label} has invalid compressed pixels: {exc}")
    if not decoder.eof or decoder.unused_data or len(pixels) != expected_pixel_bytes:
        fail(f"{label} has an invalid decompressed pixel length")
    if any(pixels[row * (row_bytes + 1)] > 4 for row in range(height)):
        fail(f"{label} contains an invalid PNG row filter")
    return width, height


def manifest_installer_row(manifest: dict[str, Any], head: str) -> dict[str, Any]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("candidate manifest artifacts must be a list")
    matches = [
        row
        for row in rows
        if isinstance(row, dict)
        and norm(row.get("head") or row.get("headId")) == head
        and norm(row.get("platform")) == "windows"
        and norm(row.get("rid")) == RID
        and norm(row.get("kind")) == "installer"
    ]
    if len(matches) != 1:
        fail(f"candidate manifest must contain exactly one {head}/{RID} Windows installer")
    return matches[0]


def require_exact_desktop_scope(manifest: dict[str, Any]) -> None:
    coverage = manifest.get("desktopTupleCoverage")
    if not isinstance(coverage, dict):
        fail("candidate manifest desktopTupleCoverage must be an object")
    if coverage.get("requiredDesktopHeads") != list(HEADS):
        fail("candidate manifest requiredDesktopHeads differs from the promoted head set")
    platforms = coverage.get("requiredDesktopPlatforms")
    if platforms != list(REGISTRY_REQUIRED_DESKTOP_PLATFORMS):
        fail("candidate manifest requiredDesktopPlatforms differs from the promoted platform set")
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("candidate manifest artifacts must be a list")
    observed: list[tuple[str, str, str]] = []
    for row in rows:
        if not isinstance(row, dict):
            fail("candidate manifest contains a non-object artifact row")
        platform_aliases = [
            row[key]
            for key in ("platform", "platformId")
            if key in row and row[key] is not None
        ]
        if (
            not platform_aliases
            or any(not isinstance(value, str) or value != norm(value) for value in platform_aliases)
            or len(set(platform_aliases)) != 1
        ):
            fail("candidate manifest desktop artifact has no exact platform identity")
        platform = norm(row.get("platform"))
        if platform not in REGISTRY_REQUIRED_DESKTOP_PLATFORMS:
            fail("candidate manifest contains an artifact outside the active desktop platforms")
        aliases = [
            row[key]
            for key in ("head", "headId")
            if key in row and row[key] is not None
        ]
        if (
            not aliases
            or any(not isinstance(value, str) or value != norm(value) for value in aliases)
            or len(set(aliases)) != 1
        ):
            fail("candidate manifest desktop artifact has no exact head identity")
        key = (aliases[0], platform, norm(row.get("rid")))
        if aliases[0] not in HEADS:
            fail("candidate manifest contains an unpromoted desktop head")
        if platform not in ACTIVE_PREVIEW_DESKTOP_PLATFORMS:
            fail("candidate manifest contains an artifact outside the active desktop platforms")
        if (
            norm(row.get("kind")) != "installer"
            or key not in ACTIVE_PREVIEW_DESKTOP_TUPLES
        ):
            fail("candidate manifest active desktop scope differs from the promoted tuple set")
        expected_identity = ACTIVE_PREVIEW_DESKTOP_ARTIFACT_IDENTITIES[key]
        if (row.get("artifactId"), row.get("fileName")) != expected_identity:
            fail("candidate manifest active desktop artifact identity is not exact")
        observed.append(key)
    if len(observed) != len(ACTIVE_PREVIEW_DESKTOP_TUPLES) or set(observed) != set(
        ACTIVE_PREVIEW_DESKTOP_TUPLES
    ):
        fail("candidate manifest active desktop artifact set is not exact")


HANDOFF_KEYS = {
    "actor",
    "artifactId",
    "artifactName",
    "artifactSha256",
    "contentInventorySha256",
    "contractName",
    "contractVersion",
    "ref",
    "repository",
    "runAttempt",
    "runId",
    "sha",
    "workflow",
}
AUTHENTICATED_API_KEYS = {
    "actor",
    "artifactCreatedAt",
    "artifactExpiresAt",
    "artifactId",
    "artifactName",
    "artifactSha256",
    "conclusion",
    "contractName",
    "contractVersion",
    "event",
    "ref",
    "repository",
    "runAttempt",
    "runId",
    "sha",
    "status",
    "workflow",
}


def exact_candidate_tree(root_value: Path) -> Path:
    root = require_absolute_private_root(root_value, "candidate-root")
    files: list[str] = []
    directories: list[str] = []
    try:
        for current, dir_names, file_names in os.walk(root, topdown=True, followlinks=False):
            current_path = Path(current)
            dir_names.sort()
            file_names.sort()
            for name in dir_names:
                path = current_path / name
                mode = os.lstat(path).st_mode
                relative = path.relative_to(root).as_posix()
                if stat.S_ISLNK(mode):
                    fail(f"candidate export cannot contain symlinks: {relative}")
                if not stat.S_ISDIR(mode):
                    fail(f"candidate export contains a special directory entry: {relative}")
                directories.append(relative)
            for name in file_names:
                path = current_path / name
                mode = os.lstat(path).st_mode
                relative = path.relative_to(root).as_posix()
                if stat.S_ISLNK(mode):
                    fail(f"candidate export cannot contain symlinks: {relative}")
                if not stat.S_ISREG(mode):
                    fail(f"candidate export contains a special file: {relative}")
                files.append(relative)
    except OSError as exc:
        fail(f"candidate export tree could not be inspected: {exc}")
    if directories != ["files"] or files != sorted(CANDIDATE_EXPORT_PATHS):
        missing = sorted(set(CANDIDATE_EXPORT_PATHS) - set(files))
        extra = sorted(set(files) - set(CANDIDATE_EXPORT_PATHS))
        fail(
            "candidate export must be the exact five-file tree; "
            f"directories={directories}, missing={missing}, extra={extra}"
        )
    return root


def candidate_file_snapshots(root: Path) -> dict[str, RegularFileSnapshot]:
    exact_candidate_tree(root)
    snapshots = {
        relative: snapshot_regular_beneath(
            root,
            relative,
            f"candidate export member {relative}",
            include_data=relative in JSON_CANDIDATE_PATHS,
        )
        for relative in sorted(CANDIDATE_EXPORT_PATHS)
    }
    exact_candidate_tree(root)
    return snapshots


def snapshot_rows(snapshots: dict[str, RegularFileSnapshot]) -> list[dict[str, Any]]:
    return [
        {
            "path": relative,
            "sha256": snapshots[relative].sha256,
            "sizeBytes": snapshots[relative].size_bytes,
        }
        for relative in sorted(snapshots)
    ]


def revalidate_candidate_snapshot(candidate: dict[str, Any]) -> None:
    expected = snapshot_rows(candidate["snapshots"])
    actual = snapshot_rows(candidate_file_snapshots(candidate["root"]))
    if actual != expected:
        fail("private held candidate changed after its validated snapshot")


def validate_candidate_authority(args: argparse.Namespace) -> tuple[dict[str, Any], dict[str, Any]]:
    handoff = parse_canonical_json(
        args.candidate_handoff_json, HANDOFF_KEYS, "candidate handoff JSON"
    )
    api = parse_canonical_json(
        args.candidate_api_json, AUTHENTICATED_API_KEYS, "authenticated candidate API JSON"
    )
    if (
        handoff.get("contractName") != CANDIDATE_HANDOFF_CONTRACT
        or type(handoff.get("contractVersion")) is not int
        or handoff.get("contractVersion") != 1
    ):
        fail("candidate handoff contract is invalid")
    if (
        api.get("contractName") != CANDIDATE_API_CONTRACT
        or type(api.get("contractVersion")) is not int
        or api.get("contractVersion") != 1
    ):
        fail("authenticated candidate API contract is invalid")
    for payload, label in ((handoff, "candidate handoff"), (api, "authenticated candidate API")):
        repository = payload.get("repository")
        if not isinstance(repository, str) or not REPOSITORY_RE.fullmatch(repository):
            fail(f"{label} repository must be an exact owner/repository slug")
        require_exact_string(payload, "workflow", PRODUCER_WORKFLOW, label)
        require_exact_string(payload, "ref", PRODUCER_REF, label)
        require_positive_integer(payload.get("runId"), f"{label} runId")
        require_positive_integer(payload.get("runAttempt"), f"{label} runAttempt")
        require_positive_integer(payload.get("artifactId"), f"{label} artifactId")
        require_commit(payload.get("sha"), f"{label} sha")
        require_github_login(payload.get("actor"), f"{label} actor")
        require_sha256(payload.get("artifactSha256"), f"{label} artifactSha256")
        expected_name = f"preview-nightly-candidate-{payload['runId']}-{payload['runAttempt']}"
        require_exact_string(payload, "artifactName", expected_name, label)
    for key in (
        "repository",
        "workflow",
        "runId",
        "runAttempt",
        "ref",
        "sha",
        "actor",
        "artifactId",
        "artifactName",
        "artifactSha256",
    ):
        if api[key] != handoff[key] or type(api[key]) is not type(handoff[key]):
            fail(f"authenticated candidate API {key} differs from the canonical handoff")
    require_sha256(handoff.get("contentInventorySha256"), "candidate handoff contentInventorySha256")
    for key, expected in (
        ("event", "workflow_dispatch"),
        ("status", "completed"),
        ("conclusion", "success"),
    ):
        require_exact_string(api, key, expected, "authenticated candidate API")
    _, created_at = parse_github_timestamp(
        api.get("artifactCreatedAt"), "authenticated candidate API artifactCreatedAt"
    )
    _, expires_at = require_future_timestamp(
        api.get("artifactExpiresAt"), "authenticated candidate API artifactExpiresAt"
    )
    if created_at >= expires_at:
        fail("authenticated candidate API artifact timestamps are not ordered")
    if created_at > datetime.now(UTC) + timedelta(minutes=5):
        fail("authenticated candidate API artifactCreatedAt is more than five minutes in the future")
    return handoff, api


def validate_candidate_inventory(
    snapshots: dict[str, RegularFileSnapshot], handoff: dict[str, Any]
) -> tuple[dict[str, Any], dict[str, dict[str, Any]], str]:
    inventory_snapshot = snapshots[CANDIDATE_INVENTORY_FILE]
    if inventory_snapshot.sha256 != handoff["contentInventorySha256"]:
        fail("candidate content inventory bytes differ from the canonical handoff")
    inventory = json_from_snapshot(inventory_snapshot, "candidate content inventory")
    require_exact_keys(
        inventory,
        {"contractName", "contractVersion", "files", "manifest", "release"},
        "candidate content inventory",
    )
    if (
        inventory.get("contractName") != CANDIDATE_INVENTORY_CONTRACT
        or type(inventory.get("contractVersion")) is not int
        or inventory.get("contractVersion") != 1
    ):
        fail("candidate content inventory contract is invalid")
    release = require_exact_keys(
        inventory.get("release"), {"channel", "version"}, "candidate inventory release"
    )
    require_exact_string(release, "channel", "preview", "candidate inventory release")
    version = require_portable(release.get("version"), "candidate inventory version")
    manifest = require_exact_keys(
        inventory.get("manifest"), {"path", "sha256"}, "candidate inventory manifest"
    )
    require_exact_string(manifest, "path", CANDIDATE_MANIFEST_FILE, "candidate inventory manifest")
    manifest_sha = require_sha256(manifest.get("sha256"), "candidate inventory manifest sha256")
    rows = inventory.get("files")
    if not isinstance(rows, list) or len(rows) != len(CANDIDATE_CONTENT_PATHS):
        fail("candidate content inventory must contain the exact three content rows")
    expected_paths = sorted(CANDIDATE_CONTENT_PATHS)
    if [row.get("path") if isinstance(row, dict) else None for row in rows] != expected_paths:
        fail("candidate content inventory paths are not the exact canonical three-file order")
    by_path: dict[str, dict[str, Any]] = {}
    for row in rows:
        row = require_exact_keys(
            row, {"path", "sha256", "sizeBytes"}, "candidate content inventory row"
        )
        relative = row["path"]
        digest = require_sha256(row.get("sha256"), f"candidate inventory {relative} sha256")
        size = require_positive_size(row.get("sizeBytes"), f"candidate inventory {relative} sizeBytes")
        content = snapshots[relative]
        if content.sha256 != digest or content.size_bytes != size:
            fail(f"candidate inventory row does not match exact bytes: {relative}")
        by_path[relative] = row
    if by_path[CANDIDATE_MANIFEST_FILE]["sha256"] != manifest_sha:
        fail("candidate inventory manifest row differs from its manifest binding")
    return inventory, by_path, version


def exact_head_aliases(row: dict[str, Any], head: str) -> None:
    found = False
    for key in ("head", "headId"):
        if key not in row or row[key] is None:
            continue
        found = True
        if row[key] != head or not isinstance(row[key], str):
            fail(f"candidate manifest {head} {key} is not exact")
    if not found:
        fail(f"candidate manifest {head} lacks an exact head/headId")


def derive_candidate_head(
    root: Path,
    manifest: dict[str, Any],
    inventory_rows: dict[str, dict[str, Any]],
    head: str,
) -> tuple[dict[str, Any], dict[str, Any]]:
    row = manifest_installer_row(manifest, head)
    exact_head_aliases(row, head)
    for key, expected in (
        ("platform", "windows"),
        ("rid", RID),
        ("kind", "installer"),
        ("installerMode", "bootstrap"),
        ("payloadAcquisitionMode", "download"),
    ):
        require_exact_string(row, key, expected, f"candidate manifest {head}")
    installer_relative = candidate_installer_path(head)
    payload_relative = candidate_payload_path(head)
    installer_row = inventory_rows[installer_relative]
    payload_row = inventory_rows[payload_relative]
    require_exact_string(
        row, "fileName", Path(installer_relative).name, f"candidate manifest {head}"
    )
    require_exact_string(
        row, "payloadFileName", Path(payload_relative).name, f"candidate manifest {head}"
    )
    if require_sha256(row.get("sha256"), f"candidate manifest {head} sha256") != installer_row[
        "sha256"
    ]:
        fail(f"candidate manifest {head} installer digest differs from the inventory")
    if require_sha256(
        row.get("payloadSha256"), f"candidate manifest {head} payloadSha256"
    ) != payload_row["sha256"]:
        fail(f"candidate manifest {head} payload digest differs from the inventory")
    if require_positive_size(row.get("sizeBytes"), f"candidate manifest {head} sizeBytes") != installer_row[
        "sizeBytes"
    ]:
        fail(f"candidate manifest {head} installer size differs from the inventory")
    if require_positive_size(
        row.get("payloadSizeBytes"), f"candidate manifest {head} payloadSizeBytes"
    ) != payload_row["sizeBytes"]:
        fail(f"candidate manifest {head} payload size differs from the inventory")
    installer = {
        "relativePath": installer_relative,
        "fileName": Path(installer_relative).name,
        "sha256": installer_row["sha256"],
        "sizeBytes": installer_row["sizeBytes"],
    }
    payload = {
        "relativePath": payload_relative,
        "fileName": Path(payload_relative).name,
        "sha256": payload_row["sha256"],
        "sizeBytes": payload_row["sizeBytes"],
    }
    return installer, payload


def validate_candidate_export(args: argparse.Namespace) -> dict[str, Any]:
    root = exact_candidate_tree(args.candidate_root)
    handoff, api = validate_candidate_authority(args)
    snapshots = candidate_file_snapshots(root)
    inventory, inventory_rows, version = validate_candidate_inventory(snapshots, handoff)
    manifest = json_from_snapshot(snapshots[CANDIDATE_MANIFEST_FILE], "candidate manifest")
    require_exact_string(manifest, "contractName", CANDIDATE_MANIFEST_CONTRACT, "candidate manifest")
    if "contract_name" in manifest:
        require_exact_string(
            manifest, "contract_name", CANDIDATE_MANIFEST_CONTRACT, "candidate manifest"
        )
    if type(manifest.get("schemaVersion")) is not int or manifest.get("schemaVersion") != 1:
        fail("candidate manifest schemaVersion must be exactly 1")
    for key in ("version", "releaseVersion"):
        require_exact_string(manifest, key, version, "candidate manifest")
    for key in ("channelId", "channel"):
        require_exact_string(manifest, key, "preview", "candidate manifest")
    require_exact_desktop_scope(manifest)
    bindings = {
        head: derive_candidate_head(root, manifest, inventory_rows, head) for head in HEADS
    }
    binary_digests = [
        binding[index]["sha256"] for binding in bindings.values() for index in (0, 1)
    ]
    if len(set(binary_digests)) != len(binary_digests):
        fail("candidate installer/payload files must have distinct SHA-256 digests")
    receipt_snapshot = snapshots[CANDIDATE_EXPORT_FILE]
    receipt = json_from_snapshot(receipt_snapshot, "candidate export receipt")
    require_exact_keys(
        receipt,
        {
            "candidateManifest",
            "contentInventory",
            "contractName",
            "contractVersion",
            "heads",
            "release",
            "source",
            "status",
        },
        "candidate export receipt",
    )
    if (
        receipt.get("contractName") != CANDIDATE_EXPORT_CONTRACT
        or type(receipt.get("contractVersion")) is not int
        or receipt.get("contractVersion") != 1
    ):
        fail("candidate export receipt contract is invalid")
    require_exact_string(receipt, "status", "exported", "candidate export receipt")
    if receipt.get("release") != inventory["release"]:
        fail("candidate export receipt release differs from the content inventory")
    if receipt.get("candidateManifest") != inventory["manifest"]:
        fail("candidate export receipt manifest differs from the content inventory")
    expected_inventory_binding = {
        "path": CANDIDATE_INVENTORY_FILE,
        "sha256": handoff["contentInventorySha256"],
    }
    if receipt.get("contentInventory") != expected_inventory_binding:
        fail("candidate export receipt contentInventory differs from the canonical handoff")
    source = require_exact_keys(
        receipt.get("source"),
        {
            "actor",
            "artifactName",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "runnerLabel",
            "sha",
            "workflow",
        },
        "candidate export receipt source",
    )
    for key in (
        "repository",
        "workflow",
        "runId",
        "runAttempt",
        "ref",
        "sha",
        "actor",
        "artifactName",
    ):
        if source.get(key) != handoff[key] or not isinstance(source.get(key), str):
            fail(f"candidate export receipt source {key} differs from the canonical handoff")
    runner_label = source.get("runnerLabel")
    if not isinstance(runner_label, str) or not re.fullmatch(
        r"chummer-preview-nightly-export-[a-z0-9]{12,64}", runner_label
    ):
        fail("candidate export receipt runnerLabel is invalid")
    expected_heads = [
        {
            "headId": head,
            "rid": RID,
            "installer": bindings[head][0],
            "payload": bindings[head][1],
        }
        for head in HEADS
    ]
    if receipt.get("heads") != expected_heads:
        fail("candidate export receipt heads differ from derived manifest/inventory bindings")
    candidate = {
        "root": root,
        "snapshots": snapshots,
        "version": version,
        "channel": "preview",
        "manifestPath": CANDIDATE_MANIFEST_FILE,
        "manifestSha256": inventory["manifest"]["sha256"],
        "contentInventoryPath": CANDIDATE_INVENTORY_FILE,
        "contentInventorySha256": handoff["contentInventorySha256"],
        "exportReceiptPath": CANDIDATE_EXPORT_FILE,
        "exportReceiptSha256": receipt_snapshot.sha256,
        "handoff": handoff,
        "api": api,
        "bindings": bindings,
    }
    revalidate_candidate_snapshot(candidate)
    return candidate


def validate_new_held_root(root_value: Path) -> Path:
    if not root_value.is_absolute() or root_value.exists() or root_value.is_symlink():
        fail("candidate held root must be an absolute path that does not already exist")
    parent = root_value.parent
    if parent.is_symlink() or not parent.is_dir():
        fail("candidate held-root parent must be an existing non-symlink directory")
    return parent.resolve(strict=True) / root_value.name


def validate_archive_members(archive: zipfile.ZipFile) -> list[zipfile.ZipInfo]:
    members = archive.infolist()
    names = [member.filename for member in members]
    if len(members) != len(CANDIDATE_EXPORT_PATHS) or len(set(names)) != len(names):
        fail("candidate ZIP must contain exactly five unique members")
    if sorted(names) != sorted(CANDIDATE_EXPORT_PATHS):
        fail("candidate ZIP member names differ from the exact five-file export")
    expanded_total = 0
    for member in members:
        if getattr(member, "orig_filename", member.filename) != member.filename:
            fail(f"candidate ZIP member contains an embedded NUL: {member.filename}")
        exact_relative_parts(member.filename, "candidate ZIP member")
        if member.is_dir() or member.flag_bits & 0x1:
            fail(f"candidate ZIP member is a directory or encrypted: {member.filename}")
        if member.compress_type not in {zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED}:
            fail(f"candidate ZIP member uses unsupported compression: {member.filename}")
        unix_mode = (member.external_attr >> 16) & 0xFFFF
        file_type = stat.S_IFMT(unix_mode)
        if file_type not in {0, stat.S_IFREG} or unix_mode & (
            stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX
        ):
            fail(f"candidate ZIP member is a symlink or special file: {member.filename}")
        if member.file_size < 1 or member.file_size > MAX_CANDIDATE_MEMBER_BYTES:
            fail(f"candidate ZIP member size is outside the fixed bound: {member.filename}")
        if member.filename in JSON_CANDIDATE_PATHS and member.file_size > MAX_CANDIDATE_JSON_BYTES:
            fail(f"candidate ZIP JSON member exceeds the fixed byte bound: {member.filename}")
        if member.compress_size < 1 or member.file_size > (
            member.compress_size * MAX_CANDIDATE_COMPRESSION_RATIO
            + MAX_CANDIDATE_COMPRESSION_SLACK
        ):
            fail(f"candidate ZIP member has an unsafe compression ratio: {member.filename}")
        expanded_total += member.file_size
        if expanded_total > MAX_CANDIDATE_EXPANDED_BYTES:
            fail("candidate ZIP expands beyond the fixed total-byte bound")
    return members


def extract_exact_candidate_archive(archive: zipfile.ZipFile, held_root: Path) -> None:
    members = validate_archive_members(archive)
    held_root.mkdir(mode=0o700)
    (held_root / "files").mkdir(mode=0o700)
    try:
        for member in members:
            target = held_root.joinpath(*PurePosixPath(member.filename).parts)
            descriptor = -1
            copied = 0
            try:
                descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
                with (
                    archive.open(member, "r") as source,
                    os.fdopen(descriptor, "wb", closefd=True) as destination,
                ):
                    descriptor = -1
                    while True:
                        chunk = source.read(1024 * 1024)
                        if not chunk:
                            break
                        copied += len(chunk)
                        if copied > member.file_size or copied > MAX_CANDIDATE_MEMBER_BYTES:
                            fail(f"candidate ZIP member expanded past its declared size: {member.filename}")
                        destination.write(chunk)
                if copied != member.file_size:
                    fail(f"candidate ZIP member size differs after extraction: {member.filename}")
                target.chmod(0o600)
            finally:
                if descriptor >= 0:
                    os.close(descriptor)
        exact_candidate_tree(held_root)
    except Exception:
        shutil.rmtree(held_root, ignore_errors=True)
        raise


def materialize_candidate_archive(args: argparse.Namespace) -> dict[str, Any]:
    handoff, _ = validate_candidate_authority(args)
    held_root = validate_new_held_root(args.held_root)
    archive_path = args.candidate_zip
    if not archive_path.is_absolute() or archive_path.is_symlink() or not archive_path.is_file():
        fail("candidate ZIP must be an absolute non-symlink regular file")
    archive_parent = require_absolute_private_root(archive_path.parent, "candidate ZIP parent")
    descriptor = open_regular_beneath(
        archive_parent, archive_path.name, "candidate artifact ZIP"
    )
    try:
        with os.fdopen(descriptor, "rb", closefd=True) as archive_handle:
            descriptor = -1
            before = os.fstat(archive_handle.fileno())
            if before.st_size < 1 or before.st_size > MAX_CANDIDATE_ARCHIVE_BYTES:
                fail("candidate artifact ZIP size is outside the fixed bound")
            digest = hashlib.sha256()
            for chunk in iter(lambda: archive_handle.read(1024 * 1024), b""):
                digest.update(chunk)
            if digest.hexdigest() != handoff["artifactSha256"]:
                fail("candidate artifact ZIP bytes differ from the authenticated REST digest")
            archive_handle.seek(0)
            try:
                with zipfile.ZipFile(archive_handle, "r", allowZip64=True) as archive:
                    extract_exact_candidate_archive(archive, held_root)
            except (zipfile.BadZipFile, zipfile.LargeZipFile, RuntimeError, OSError) as exc:
                fail(f"candidate artifact ZIP is invalid: {exc}")
            after = os.fstat(archive_handle.fileno())
            if regular_identity(before) != regular_identity(after):
                fail("candidate artifact ZIP changed while it was authenticated and extracted")
        validation_args = argparse.Namespace(
            candidate_root=held_root,
            candidate_handoff_json=args.candidate_handoff_json,
            candidate_api_json=args.candidate_api_json,
        )
        candidate = validate_candidate_export(validation_args)
        revalidate_candidate_snapshot(candidate)
        return candidate
    except Exception:
        shutil.rmtree(held_root, ignore_errors=True)
        raise
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def validate_receipt(
    receipt: dict[str, Any], *, head: str, version: str, channel: str, installer: dict[str, Any], payload: dict[str, Any]
) -> None:
    if norm(receipt.get("status")) not in PASSING:
        fail(f"{head} startup receipt is not passing")
    expected = {
        "headId": head,
        "platform": "windows",
        "rid": RID,
        "channelId": channel,
        "releaseVersion": version,
        "artifactFileName": installer["fileName"],
        "bootstrapPayloadAcquisitionMode": "download",
        "bootstrapPayloadFileName": payload["fileName"],
    }
    for key, value in expected.items():
        if norm(receipt.get(key)) != norm(value):
            fail(f"{head} startup receipt {key} does not match the exact capture binding")
    if require_prefixed_sha256(
        receipt.get("artifactDigest"), f"{head} startup receipt artifactDigest"
    ) != installer["sha256"]:
        fail(f"{head} startup receipt artifactDigest does not match the exact capture binding")
    if require_sha256(
        receipt.get("bootstrapPayloadSha256"), f"{head} startup receipt bootstrapPayloadSha256"
    ) != payload["sha256"]:
        fail(f"{head} startup receipt bootstrapPayloadSha256 does not match the exact capture binding")
    try:
        payload_size = int(receipt.get("bootstrapPayloadSizeBytes"))
    except (TypeError, ValueError):
        fail(f"{head} startup receipt bootstrapPayloadSizeBytes is invalid")
    if payload_size != payload["sizeBytes"]:
        fail(f"{head} startup receipt bootstrapPayloadSizeBytes mismatch")
    if norm(receipt.get("readyCheckpoint")) != "pre_ui_event_loop":
        fail(f"{head} startup receipt did not reach pre_ui_event_loop")
    if norm(receipt.get("executionEnvironment")) != "native_windows":
        fail(f"{head} startup receipt is not native Windows evidence")
    native = receipt.get("nativeHostEvidence")
    if not isinstance(native, dict):
        fail(f"{head} startup receipt nativeHostEvidence is missing")
    if str(native.get("contractName") or "").strip() != NATIVE_HOST_CONTRACT:
        fail(f"{head} startup receipt native host contract is invalid")
    if norm(native.get("status")) != "verified" or native.get("isNativeWindows") is not True:
        fail(f"{head} startup receipt native host evidence is not verified")
    if norm(native.get("hostPlatform")) != "windows":
        fail(f"{head} startup receipt hostPlatform is not Windows")
    for key in ("hostKernel", "runner", "evidenceSource"):
        if not str(native.get(key) or "").strip():
            fail(f"{head} startup receipt nativeHostEvidence.{key} is missing")
    if "wine" in norm(native.get("runner")):
        fail(f"{head} startup receipt cannot classify Wine as native Windows")


def validate_progress(path: Path, head: str) -> None:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    for marker in PROGRESS_MARKERS:
        if marker not in text:
            fail(f"{head} progress log is missing marker: {marker}")
    for marker in PROGRESS_FAILURE_MARKERS:
        if marker.lower() in text.lower():
            fail(f"{head} progress log contains failure marker: {marker}")


def head_paths(head: str) -> dict[str, str]:
    return {
        "receipt": f"startup-smoke/startup-smoke-{head}-{RID}.receipt.json",
        "progressLog": f"startup-smoke/windows-installer-progress-{head}-{RID}.log",
        "progressScreenshot": f"screenshots/windows-installer-{head}-{RID}-progress.png",
        "completionScreenshot": f"screenshots/windows-installer-{head}-{RID}-completion.png",
    }


def validate_evidence_head(
    evidence_root: Path,
    *,
    head: str,
    version: str,
    channel: str,
    installer: dict[str, Any],
    payload: dict[str, Any],
) -> dict[str, Any]:
    paths = head_paths(head)
    receipt_path = safe_file(evidence_root, paths["receipt"], f"{head} startup receipt")
    progress_path = safe_file(evidence_root, paths["progressLog"], f"{head} progress log")
    progress_png = safe_file(evidence_root, paths["progressScreenshot"], f"{head} progress screenshot")
    completion_png = safe_file(evidence_root, paths["completionScreenshot"], f"{head} completion screenshot")
    validate_receipt(
        read_json(receipt_path), head=head, version=version, channel=channel, installer=installer, payload=payload
    )
    validate_progress(progress_path, head)
    progress_size = validate_png(progress_png, f"{head} progress screenshot")
    completion_size = validate_png(completion_png, f"{head} completion screenshot")
    screenshot_digests = (sha256_file(progress_png), sha256_file(completion_png))
    if screenshot_digests[0] == screenshot_digests[1]:
        fail(f"{head} progress and completion screenshots are digest-identical")
    return {
        "headId": head,
        "rid": RID,
        "installer": installer,
        "payload": payload,
        "receipt": {"path": paths["receipt"], "sha256": sha256_file(receipt_path)},
        "progressLog": {"path": paths["progressLog"], "sha256": sha256_file(progress_path)},
        "screenshots": [
            {
                "role": "progress",
                "path": paths["progressScreenshot"],
                "sha256": screenshot_digests[0],
                "width": progress_size[0],
                "height": progress_size[1],
            },
            {
                "role": "completion",
                "path": paths["completionScreenshot"],
                "sha256": screenshot_digests[1],
                "width": completion_size[0],
                "height": completion_size[1],
            },
        ],
    }


def exact_inventory(root: Path, *, exclude: set[str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            fail(f"capture evidence cannot contain symlinks: {path}")
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if relative in exclude:
            continue
        rows.append({"path": relative, "sha256": sha256_file(path), "sizeBytes": path.stat().st_size})
    return rows


def parse_allowlist(raw: str) -> list[str]:
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as exc:
        fail(f"reviewer allowlist must be a JSON array: {exc}")
    if not isinstance(parsed, list) or not parsed:
        fail("reviewer allowlist must be a non-empty JSON array")
    values: list[str] = []
    for value in parsed:
        reviewer = str(value or "").strip()
        if not REVIEWER_RE.fullmatch(reviewer):
            fail("reviewer allowlist contains an invalid GitHub login")
        if reviewer.lower() in {item.lower() for item in values}:
            fail("reviewer allowlist contains a duplicate GitHub login")
        values.append(reviewer)
    return values


def emit_candidate_bindings(candidate: dict[str, Any]) -> None:
    print(f"version={candidate['version']}")
    print(f"channel={candidate['channel']}")
    print(f"candidate_manifest={candidate['manifestPath']}")
    print(f"candidate_manifest_sha256={candidate['manifestSha256']}")
    print(f"candidate_content_inventory_sha256={candidate['contentInventorySha256']}")
    print(f"candidate_export_receipt_sha256={candidate['exportReceiptSha256']}")
    for head in HEADS:
        prefix = head.replace("-", "_")
        installer, payload = candidate["bindings"][head]
        print(f"{prefix}_installer={installer['relativePath']}")
        print(f"{prefix}_installer_sha256={installer['sha256']}")
        print(f"{prefix}_payload={payload['relativePath']}")
        print(f"{prefix}_payload_sha256={payload['sha256']}")


def preflight(args: argparse.Namespace) -> None:
    emit_candidate_bindings(validate_candidate_export(args))


def materialize(args: argparse.Namespace) -> None:
    candidate = materialize_candidate_archive(args)
    revalidate_candidate_snapshot(candidate)
    authority_path = args.authority_json
    authority_created = False
    authority_descriptor = -1
    try:
        if not authority_path.is_absolute() or authority_path.exists() or authority_path.is_symlink():
            fail("held snapshot authority must be an absolute path that does not already exist")
        authority_parent = authority_path.parent
        if authority_parent.is_symlink() or not authority_parent.is_dir():
            fail("held snapshot authority parent must be an existing non-symlink directory")
        authority_path = authority_parent.resolve(strict=True) / authority_path.name
        authority = {
            "artifactSha256": candidate["handoff"]["artifactSha256"],
            "contractName": HELD_SNAPSHOT_CONTRACT,
            "contractVersion": 1,
            "files": snapshot_rows(candidate["snapshots"]),
        }
        authority_descriptor = os.open(
            authority_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600
        )
        authority_created = True
        handle = os.fdopen(authority_descriptor, "w", encoding="utf-8", closefd=True)
        authority_descriptor = -1
        with handle:
            handle.write(json.dumps(authority, indent=2, sort_keys=True) + "\n")
    except Exception:
        if authority_descriptor >= 0:
            os.close(authority_descriptor)
        if authority_created:
            authority_path.unlink(missing_ok=True)
        shutil.rmtree(candidate["root"], ignore_errors=True)
        raise
    emit_candidate_bindings(candidate)


def copy_validated_held_member(
    candidate: dict[str, Any], source_name: str, target: Path, label: str
) -> RegularFileSnapshot:
    expected = candidate["snapshots"][source_name]
    source_descriptor = open_regular_beneath(candidate["root"], source_name, label)
    target_descriptor = -1
    digest = hashlib.sha256()
    copied = 0
    try:
        before = os.fstat(source_descriptor)
        target_descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with (
            os.fdopen(source_descriptor, "rb", closefd=True) as source_handle,
            os.fdopen(target_descriptor, "wb", closefd=True) as target_handle,
        ):
            source_descriptor = -1
            target_descriptor = -1
            while True:
                chunk = source_handle.read(1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
                copied += len(chunk)
                target_handle.write(chunk)
            after = os.fstat(source_handle.fileno())
        if regular_identity(before) != regular_identity(after):
            fail(f"{label} changed while its held descriptor was copied")
        if digest.hexdigest() != expected.sha256 or copied != expected.size_bytes:
            fail(f"{label} differs from the validated held snapshot")
        target.chmod(0o600)
        copied_snapshot = snapshot_regular_beneath(
            target.parent, target.name, f"copied {label}", include_data=True
        )
        if (
            copied_snapshot.sha256 != expected.sha256
            or copied_snapshot.size_bytes != expected.size_bytes
        ):
            fail(f"copied {label} bytes differ after their post-copy rehash")
        held_after = snapshot_regular_beneath(
            candidate["root"], source_name, label, include_data=True
        )
        if (
            held_after.sha256 != expected.sha256
            or held_after.size_bytes != expected.size_bytes
        ):
            fail(f"{label} changed before its provenance copy was bound")
        return copied_snapshot
    except Exception:
        target.unlink(missing_ok=True)
        raise
    finally:
        if source_descriptor >= 0:
            os.close(source_descriptor)
        if target_descriptor >= 0:
            os.close(target_descriptor)


def copy_candidate_provenance(candidate: dict[str, Any], evidence_root: Path) -> dict[str, Any]:
    revalidate_candidate_snapshot(candidate)
    provenance_root = evidence_root / CANDIDATE_PROVENANCE_DIRECTORY
    if provenance_root.exists() or provenance_root.is_symlink():
        fail("candidate provenance directory must not already exist")
    provenance_root.mkdir(mode=0o700)
    copied: dict[str, dict[str, Any]] = {}
    try:
        for key, source_name in (
            (
                "contentInventory",
                candidate["contentInventoryPath"],
            ),
            ("exportReceipt", candidate["exportReceiptPath"]),
        ):
            relative = f"{CANDIDATE_PROVENANCE_DIRECTORY}/{source_name}"
            target = evidence_root / relative
            copied_snapshot = copy_validated_held_member(
                candidate, source_name, target, f"candidate {key}"
            )
            copied[key] = {
                "path": relative,
                "sha256": copied_snapshot.sha256,
                "sizeBytes": copied_snapshot.size_bytes,
            }
        revalidate_candidate_snapshot(candidate)
    except Exception:
        shutil.rmtree(provenance_root, ignore_errors=True)
        raise
    return copied


def capture(args: argparse.Namespace) -> None:
    candidate_authority = validate_candidate_export(args)
    version = candidate_authority["version"]
    channel = candidate_authority["channel"]
    bindings = candidate_authority["bindings"]
    handoff = candidate_authority["handoff"]
    api = candidate_authority["api"]
    evidence_root = args.evidence_root.resolve()
    if not evidence_root.is_dir():
        fail("evidence-root must already exist")
    source = {
        "repository": require_portable(args.source_repository, "capture source repository"),
        "workflow": require_portable(args.source_workflow, "capture source workflow"),
        "runId": require_positive_integer(args.source_run_id, "capture source run ID"),
        "runAttempt": require_positive_integer(args.source_run_attempt, "capture source run attempt"),
        "ref": require_full_ref(args.source_ref, "capture source ref"),
        "sha": require_commit(args.source_sha, "capture source SHA"),
        "actor": require_github_login(args.source_actor, "capture source actor"),
        "artifactName": require_portable(args.output_artifact_name, "capture artifact name"),
    }
    if source["workflow"] != CAPTURE_WORKFLOW:
        fail(f"capture source workflow must be {CAPTURE_WORKFLOW}")
    if source["artifactName"] != f"windows-native-evidence-{source['runId']}-{source['runAttempt']}":
        fail("capture artifact name is not exactly bound to its run ID and attempt")
    if source["repository"] != handoff["repository"]:
        fail("capture and candidate producer repositories must match")
    if source["ref"] != PRODUCER_REF or source["sha"] != handoff["sha"]:
        fail("capture must execute from the exact producer main commit")
    if source["actor"] != "github-actions[bot]":
        fail("capture must be dispatched by the hosted producer relay")
    candidate = {
        "repository": handoff["repository"],
        "workflow": handoff["workflow"],
        "runId": handoff["runId"],
        "runAttempt": handoff["runAttempt"],
        "ref": handoff["ref"],
        "sha": handoff["sha"],
        "actor": handoff["actor"],
        "artifactId": handoff["artifactId"],
        "artifactName": handoff["artifactName"],
        "artifactSha256": handoff["artifactSha256"],
        "artifactCreatedAt": api["artifactCreatedAt"],
        "artifactExpiresAt": api["artifactExpiresAt"],
        "manifestPath": candidate_authority["manifestPath"],
        "manifestSha256": candidate_authority["manifestSha256"],
        "contentInventorySha256": candidate_authority["contentInventorySha256"],
        "exportReceiptSha256": candidate_authority["exportReceiptSha256"],
        "handoffSha256": hashlib.sha256(args.candidate_handoff_json.encode("utf-8")).hexdigest(),
        "authenticatedApiSha256": hashlib.sha256(
            args.candidate_api_json.encode("utf-8")
        ).hexdigest(),
    }
    heads = [
        validate_evidence_head(
            evidence_root,
            head=head,
            version=version,
            channel=channel,
            installer=bindings[head][0],
            payload=bindings[head][1],
        )
        for head in HEADS
    ]
    all_screenshot_digests = [shot["sha256"] for row in heads for shot in row["screenshots"]]
    if len(set(all_screenshot_digests)) != len(all_screenshot_digests):
        fail("all per-head progress/completion screenshots must be distinct captures")
    revalidate_candidate_snapshot(candidate_authority)
    provenance = copy_candidate_provenance(candidate_authority, evidence_root)
    candidate["contentInventory"] = provenance["contentInventory"]
    candidate["exportReceipt"] = provenance["exportReceipt"]
    capture_payload = {
        "contractName": CAPTURE_CONTRACT,
        "contractVersion": 1,
        "status": "captured",
        "captureMode": "interactive",
        "generatedAt": datetime.now(UTC).isoformat().replace("+00:00", "Z"),
        "version": version,
        "channelId": channel,
        "source": source,
        "candidate": candidate,
        "heads": heads,
    }
    write_json(evidence_root / CAPTURE_FILE, capture_payload)
    revalidate_candidate_snapshot(candidate_authority)
    rows = exact_inventory(evidence_root, exclude={CAPTURE_INVENTORY_FILE})
    inventory = {
        "contractName": CAPTURE_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "captureContract": CAPTURE_CONTRACT,
        "captureManifestSha256": sha256_file(evidence_root / CAPTURE_FILE),
        "files": rows,
    }
    write_json(evidence_root / CAPTURE_INVENTORY_FILE, inventory)
    print(f"capture_inventory_sha256={sha256_file(evidence_root / CAPTURE_INVENTORY_FILE)}")


def verify_inventory(capture_root: Path, expected_sha: str) -> dict[str, Any]:
    inventory_path = safe_file(capture_root, CAPTURE_INVENTORY_FILE, "capture inventory")
    if sha256_file(inventory_path) != require_sha256(expected_sha, "capture inventory SHA-256"):
        fail("capture inventory bytes do not match the independently supplied SHA-256")
    inventory = read_json(inventory_path)
    if inventory.get("contractName") != CAPTURE_INVENTORY_CONTRACT or inventory.get("contractVersion") != 1:
        fail("capture inventory contract is invalid")
    rows = inventory.get("files")
    if not isinstance(rows, list) or not rows:
        fail("capture inventory files must be a non-empty list")
    actual = exact_inventory(capture_root, exclude={CAPTURE_INVENTORY_FILE})
    if rows != actual:
        fail("capture artifact inventory does not exactly match its files")
    capture_path = safe_file(capture_root, CAPTURE_FILE, "capture manifest")
    if require_sha256(
        inventory.get("captureManifestSha256"), "capture inventory captureManifestSha256"
    ) != sha256_file(capture_path):
        fail("capture manifest digest does not match the capture inventory")
    return inventory


def require_confirmation(value: str, label: str) -> None:
    if norm(value) != "true":
        fail(f"explicit {label} confirmation is required")


def validate_capture_candidate_provenance(
    capture_root: Path,
    capture_payload: dict[str, Any],
    validated_heads: list[dict[str, Any]],
) -> None:
    candidate = require_exact_keys(
        capture_payload.get("candidate"),
        {
            "actor",
            "artifactCreatedAt",
            "artifactExpiresAt",
            "artifactId",
            "artifactName",
            "artifactSha256",
            "authenticatedApiSha256",
            "contentInventory",
            "contentInventorySha256",
            "exportReceipt",
            "exportReceiptSha256",
            "handoffSha256",
            "manifestPath",
            "manifestSha256",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        "capture candidate binding",
    )
    if not isinstance(candidate.get("repository"), str) or not REPOSITORY_RE.fullmatch(
        candidate["repository"]
    ):
        fail("capture candidate repository is invalid")
    require_exact_string(candidate, "workflow", PRODUCER_WORKFLOW, "capture candidate")
    require_exact_string(candidate, "ref", PRODUCER_REF, "capture candidate")
    require_commit(candidate.get("sha"), "capture candidate sha")
    require_github_login(candidate.get("actor"), "capture candidate actor")
    for key in ("runId", "runAttempt", "artifactId"):
        require_positive_integer(candidate.get(key), f"capture candidate {key}")
    require_exact_string(
        candidate,
        "artifactName",
        f"preview-nightly-candidate-{candidate['runId']}-{candidate['runAttempt']}",
        "capture candidate",
    )
    for key in (
        "artifactSha256",
        "authenticatedApiSha256",
        "contentInventorySha256",
        "exportReceiptSha256",
        "handoffSha256",
        "manifestSha256",
    ):
        require_sha256(candidate.get(key), f"capture candidate {key}")
    require_exact_string(
        candidate, "manifestPath", CANDIDATE_MANIFEST_FILE, "capture candidate"
    )
    _, created_at = parse_github_timestamp(
        candidate.get("artifactCreatedAt"), "capture candidate artifactCreatedAt"
    )
    _, expires_at = parse_github_timestamp(
        candidate.get("artifactExpiresAt"), "capture candidate artifactExpiresAt"
    )
    if created_at >= expires_at:
        fail("capture candidate artifact timestamps are not ordered")
    capture_source = require_exact_keys(
        capture_payload.get("source"),
        {
            "actor",
            "artifactName",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        "capture source binding",
    )
    if (
        candidate["repository"] != capture_source.get("repository")
        or candidate["ref"] != capture_source.get("ref")
        or candidate["sha"] != capture_source.get("sha")
    ):
        fail("capture candidate repository/ref/SHA differs from the capture source")

    documents: dict[str, dict[str, Any]] = {}
    for key, filename, expected_sha in (
        ("contentInventory", CANDIDATE_INVENTORY_FILE, candidate["contentInventorySha256"]),
        ("exportReceipt", CANDIDATE_EXPORT_FILE, candidate["exportReceiptSha256"]),
    ):
        binding = require_exact_keys(
            candidate.get(key), {"path", "sha256", "sizeBytes"}, f"capture candidate {key}"
        )
        expected_path = f"{CANDIDATE_PROVENANCE_DIRECTORY}/{filename}"
        require_exact_string(binding, "path", expected_path, f"capture candidate {key}")
        binding_sha = require_sha256(binding.get("sha256"), f"capture candidate {key} sha256")
        if binding_sha != expected_sha:
            fail(f"capture candidate {key} binding differs from its top-level digest")
        binding_size = require_positive_size(
            binding.get("sizeBytes"), f"capture candidate {key} sizeBytes"
        )
        snapshot = snapshot_regular_beneath(
            capture_root, expected_path, f"capture candidate {key}", include_data=True
        )
        if snapshot.sha256 != binding_sha or snapshot.size_bytes != binding_size:
            fail(f"capture candidate {key} path/hash/size differs from preserved provenance")
        documents[key] = json_from_snapshot(snapshot, f"capture candidate {key}")

    inventory = require_exact_keys(
        documents["contentInventory"],
        {"contractName", "contractVersion", "files", "manifest", "release"},
        "preserved candidate content inventory",
    )
    if (
        inventory.get("contractName") != CANDIDATE_INVENTORY_CONTRACT
        or type(inventory.get("contractVersion")) is not int
        or inventory.get("contractVersion") != 1
    ):
        fail("preserved candidate content inventory contract is invalid")
    expected_release = {
        "channel": capture_payload.get("channelId"),
        "version": capture_payload.get("version"),
    }
    if inventory.get("release") != expected_release:
        fail("preserved candidate inventory release differs from capture")
    expected_manifest = {
        "path": candidate["manifestPath"],
        "sha256": candidate["manifestSha256"],
    }
    if inventory.get("manifest") != expected_manifest:
        fail("preserved candidate inventory manifest differs from capture")
    inventory_rows = inventory.get("files")
    if not isinstance(inventory_rows, list) or len(inventory_rows) != len(CANDIDATE_CONTENT_PATHS):
        fail("preserved candidate inventory must contain exactly three content rows")
    if [row.get("path") if isinstance(row, dict) else None for row in inventory_rows] != sorted(
        CANDIDATE_CONTENT_PATHS
    ):
        fail("preserved candidate inventory paths are not canonical")
    inventory_by_path: dict[str, dict[str, Any]] = {}
    for row in inventory_rows:
        row = require_exact_keys(
            row, {"path", "sha256", "sizeBytes"}, "preserved candidate inventory row"
        )
        require_sha256(row.get("sha256"), f"preserved candidate {row['path']} sha256")
        require_positive_size(row.get("sizeBytes"), f"preserved candidate {row['path']} sizeBytes")
        inventory_by_path[row["path"]] = row
    if inventory_by_path[CANDIDATE_MANIFEST_FILE]["sha256"] != candidate["manifestSha256"]:
        fail("preserved candidate manifest row differs from the capture manifest binding")
    for captured_head in validated_heads:
        for kind in ("installer", "payload"):
            binding = captured_head[kind]
            expected_row = {
                "path": binding["relativePath"],
                "sha256": binding["sha256"],
                "sizeBytes": binding["sizeBytes"],
            }
            if inventory_by_path.get(binding["relativePath"]) != expected_row:
                fail(
                    f"preserved candidate inventory {captured_head['headId']} {kind} "
                    "differs from the captured byte binding"
                )

    receipt = require_exact_keys(
        documents["exportReceipt"],
        {
            "candidateManifest",
            "contentInventory",
            "contractName",
            "contractVersion",
            "heads",
            "release",
            "source",
            "status",
        },
        "preserved candidate export receipt",
    )
    if (
        receipt.get("contractName") != CANDIDATE_EXPORT_CONTRACT
        or type(receipt.get("contractVersion")) is not int
        or receipt.get("contractVersion") != 1
    ):
        fail("preserved candidate export receipt contract is invalid")
    require_exact_string(receipt, "status", "exported", "preserved candidate export receipt")
    if receipt.get("release") != expected_release or receipt.get("candidateManifest") != expected_manifest:
        fail("preserved candidate export receipt release/manifest differs from capture")
    if receipt.get("contentInventory") != {
        "path": CANDIDATE_INVENTORY_FILE,
        "sha256": candidate["contentInventorySha256"],
    }:
        fail("preserved candidate export receipt inventory binding differs from capture")
    source = require_exact_keys(
        receipt.get("source"),
        {
            "actor",
            "artifactName",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "runnerLabel",
            "sha",
            "workflow",
        },
        "preserved candidate export source",
    )
    for key in (
        "actor",
        "artifactName",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "workflow",
    ):
        if source.get(key) != candidate[key] or not isinstance(source.get(key), str):
            fail(f"preserved candidate export source {key} differs from capture")
    runner_label = source.get("runnerLabel")
    if not isinstance(runner_label, str) or not re.fullmatch(
        r"chummer-preview-nightly-export-[a-z0-9]{12,64}", runner_label
    ):
        fail("preserved candidate export source runnerLabel is invalid")
    expected_heads = [
        {
            "headId": row["headId"],
            "rid": RID,
            "installer": row["installer"],
            "payload": row["payload"],
        }
        for row in validated_heads
    ]
    if receipt.get("heads") != expected_heads:
        fail("preserved candidate export receipt heads differ from captured byte bindings")


def finalize(args: argparse.Namespace) -> None:
    capture_root = args.capture_root.resolve()
    output_root = args.output_root.resolve()
    if not capture_root.is_dir():
        fail("capture-root must exist")
    if output_root.exists():
        fail("output-root must not already exist")
    inventory_sha = require_sha256(args.capture_inventory_sha256, "capture inventory SHA-256")
    verify_inventory(capture_root, inventory_sha)
    capture_payload = read_json(safe_file(capture_root, CAPTURE_FILE, "capture manifest"))
    if capture_payload.get("contractName") != CAPTURE_CONTRACT or capture_payload.get("contractVersion") != 1:
        fail("capture manifest contract is invalid")
    if norm(capture_payload.get("status")) != "captured" or norm(capture_payload.get("captureMode")) != "interactive":
        fail("capture manifest is not an interactive machine capture")
    source = require_exact_keys(
        capture_payload.get("source"),
        {
            "actor",
            "artifactName",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        "capture manifest source binding",
    )
    expected_source = {
        "repository": args.expected_repository,
        "workflow": args.expected_workflow,
        "runId": args.expected_run_id,
        "runAttempt": args.expected_run_attempt,
        "ref": require_full_ref(args.expected_ref, "expected capture ref"),
        "sha": require_commit(args.expected_sha, "expected capture SHA"),
        "actor": args.expected_capture_actor,
        "artifactName": args.expected_artifact_name,
    }
    for key, value in expected_source.items():
        if str(source.get(key) or "").strip() != str(value or "").strip():
            fail(f"capture source {key} does not match authenticated workflow-run metadata")
    reviewer = str(args.reviewer_id or "").strip()
    if not REVIEWER_RE.fullmatch(reviewer):
        fail("authenticated reviewer is not a valid GitHub login")
    allowlist = parse_allowlist(args.reviewer_allowlist_json)
    if reviewer.lower() not in {value.lower() for value in allowlist}:
        fail("authenticated reviewer is not in the pinned reviewer allowlist")
    if reviewer.lower() == str(source.get("actor") or "").strip().lower():
        fail("capture actor cannot review or finalize their own capture")
    require_confirmation(args.human_review_confirmed, "human review")
    finalization_source = {
        "repository": require_portable(args.finalization_repository, "finalization repository"),
        "workflow": require_portable(args.finalization_workflow, "finalization workflow"),
        "runId": require_portable(args.finalization_run_id, "finalization run ID"),
        "runAttempt": require_portable(args.finalization_run_attempt, "finalization run attempt"),
        "ref": require_full_ref(args.finalization_ref, "finalization ref"),
        "sha": require_commit(args.finalization_sha, "finalization SHA"),
        "actor": require_portable(args.finalization_actor, "finalization actor"),
        "artifactName": require_portable(args.finalization_artifact_name, "finalization artifact name"),
    }
    if finalization_source["workflow"] != FINALIZE_WORKFLOW:
        fail(f"finalization workflow must be {FINALIZE_WORKFLOW}")
    if finalization_source["artifactName"] != (
        f"windows-native-evidence-finalized-{finalization_source['runId']}-{finalization_source['runAttempt']}"
    ):
        fail("finalization artifact name is not exactly bound to its run ID and attempt")
    if finalization_source["actor"].lower() != reviewer.lower():
        fail("finalization actor must be the authenticated reviewer")
    if finalization_source["repository"] != source["repository"]:
        fail("capture and finalization repositories must match")
    if finalization_source["sha"] != source["sha"]:
        fail("capture and finalization workflow SHAs must match")
    confirmations: dict[str, dict[str, str]] = {}
    for head in HEADS:
        prefix = head.replace("-", "_")
        confirmations[head] = {}
        for check in ("readability", "contrast", "clipping"):
            value = getattr(args, f"{prefix}_{check}")
            require_confirmation(value, f"{head} {check}")
            confirmations[head][check] = "passed"
    version = require_portable(capture_payload.get("version"), "capture version")
    channel = require_portable(capture_payload.get("channelId"), "capture channel")
    rows = capture_payload.get("heads")
    if not isinstance(rows, list) or [norm(row.get("headId")) for row in rows if isinstance(row, dict)] != list(HEADS):
        fail("capture manifest must contain the exact promoted Windows heads in canonical order")
    validated: list[dict[str, Any]] = []
    for row, head in zip(rows, HEADS, strict=True):
        installer = row.get("installer")
        payload = row.get("payload")
        if not isinstance(installer, dict) or not isinstance(payload, dict):
            fail(f"capture manifest {head} byte binding is invalid")
        installer["sha256"] = require_sha256(installer.get("sha256"), f"{head} installer SHA-256")
        payload["sha256"] = require_sha256(payload.get("sha256"), f"{head} payload SHA-256")
        validated_row = validate_evidence_head(
            capture_root,
            head=head,
            version=version,
            channel=channel,
            installer=installer,
            payload=payload,
        )
        if row != validated_row:
            fail(f"capture manifest {head} evidence metadata does not match the captured files")
        validated.append(validated_row)
    all_digests = [shot["sha256"] for row in validated for shot in row["screenshots"]]
    if len(set(all_digests)) != len(all_digests):
        fail("capture contains reused or digest-identical screenshots")
    validate_capture_candidate_provenance(capture_root, capture_payload, validated)
    shutil.copytree(capture_root, output_root, symlinks=False)
    verify_inventory(output_root, inventory_sha)
    generated_at = datetime.now(UTC).isoformat().replace("+00:00", "Z")
    proof_rows: list[dict[str, str]] = []
    for row in validated:
        head = row["headId"]
        screenshots = [
            {"role": shot["role"], "path": shot["path"], "sha256": shot["sha256"]}
            for shot in row["screenshots"]
        ]
        proof = {
            "contractName": VISUAL_PROOF_CONTRACT,
            "contractVersion": 1,
            "status": "passed",
            "generatedAt": generated_at,
            "version": version,
            "releaseVersion": version,
            "channel": channel,
            "channelId": channel,
            "platform": "windows",
            "head": head,
            "headId": head,
            "rid": RID,
            "artifactFileName": row["installer"]["fileName"],
            "artifactDigest": f"sha256:{row['installer']['sha256']}",
            "screenshots": screenshots,
            "checks": {"capture_mode": "interactive", "human_review_confirmed": True},
            "readabilityReview": {"status": "passed", "reviewer": reviewer},
            "contrastReview": {"status": "passed", "reviewer": reviewer},
            "clippingReview": {"status": "passed", "reviewer": reviewer},
            "review": {
                "authenticatedReviewer": reviewer,
                "captureActor": source["actor"],
                "allowlistSource": "repository variable plus protected environment",
                "explicitConfirmations": confirmations[head],
            },
            "finalizationBinding": finalization_source,
            "captureBinding": {
                "repository": source["repository"],
                "workflow": source["workflow"],
                "runId": source["runId"],
                "runAttempt": source["runAttempt"],
                "ref": source["ref"],
                "sha": source["sha"],
                "artifactName": source["artifactName"],
                "inventorySha256": inventory_sha,
            },
        }
        proof_name = f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-{RID}.generated.json"
        proof_path = output_root / proof_name
        write_json(proof_path, proof)
        proof_rows.append({"headId": head, "path": proof_name, "sha256": sha256_file(proof_path)})
    finalization = {
        "contractName": FINALIZATION_CONTRACT,
        "contractVersion": 1,
        "status": "passed",
        "generatedAt": generated_at,
        "captureInventorySha256": inventory_sha,
        "captureSource": source,
        "finalizationSource": finalization_source,
        "reviewer": reviewer,
        "reviewerWasCaptureActor": False,
        "humanReviewConfirmed": True,
        "proofs": proof_rows,
    }
    write_json(output_root / FINALIZATION_FILE, finalization)
    finalized_inventory = {
        "contractName": FINALIZED_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "captureInventorySha256": inventory_sha,
        "files": exact_inventory(output_root, exclude={FINALIZED_INVENTORY_FILE}),
    }
    write_json(output_root / FINALIZED_INVENTORY_FILE, finalized_inventory)
    print(f"finalized_evidence_root={output_root}")
    print(f"finalized_inventory_sha256={sha256_file(output_root / FINALIZED_INVENTORY_FILE)}")


def add_candidate_authority_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--candidate-handoff-json", required=True)
    parser.add_argument("--candidate-api-json", required=True)


def add_candidate_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--candidate-root", required=True, type=Path)
    add_candidate_authority_args(parser)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    preflight_parser = subparsers.add_parser(
        "preflight", help="validate exact candidate bytes before any installer executes"
    )
    add_candidate_args(preflight_parser)
    preflight_parser.set_defaults(handler=preflight)

    materialize_parser = subparsers.add_parser(
        "materialize",
        help="authenticate the original artifact ZIP and create one private held snapshot",
    )
    materialize_parser.add_argument("--candidate-zip", required=True, type=Path)
    materialize_parser.add_argument("--held-root", required=True, type=Path)
    materialize_parser.add_argument("--authority-json", required=True, type=Path)
    add_candidate_authority_args(materialize_parser)
    materialize_parser.set_defaults(handler=materialize)

    capture_parser = subparsers.add_parser("capture", help="validate and inventory native machine evidence")
    add_candidate_args(capture_parser)
    capture_parser.add_argument("--evidence-root", required=True, type=Path)
    for name in (
        "source-repository", "source-workflow", "source-run-id", "source-run-attempt", "source-ref",
        "source-sha", "source-actor", "output-artifact-name",
    ):
        capture_parser.add_argument(f"--{name}", required=True)
    capture_parser.set_defaults(handler=capture)

    finalize_parser = subparsers.add_parser(
        "finalize", help="apply allowlisted human review and materialize visual proofs"
    )
    finalize_parser.add_argument("--capture-root", required=True, type=Path)
    finalize_parser.add_argument("--output-root", required=True, type=Path)
    finalize_parser.add_argument("--capture-inventory-sha256", required=True)
    finalize_parser.add_argument("--human-review-confirmed", required=True)
    for name in (
        "expected-repository", "expected-workflow", "expected-run-id", "expected-run-attempt", "expected-ref",
        "expected-sha", "expected-capture-actor", "expected-artifact-name", "reviewer-id",
        "reviewer-allowlist-json",
    ):
        finalize_parser.add_argument(f"--{name}", required=True)
    for name in (
        "finalization-repository", "finalization-workflow", "finalization-run-id", "finalization-run-attempt",
        "finalization-ref", "finalization-sha", "finalization-actor", "finalization-artifact-name",
    ):
        finalize_parser.add_argument(f"--{name}", required=True)
    for head in HEADS:
        prefix = head.replace("-", "_")
        for check in ("readability", "contrast", "clipping"):
            finalize_parser.add_argument(f"--{head}-{check}", dest=f"{prefix}_{check}", required=True)
    finalize_parser.set_defaults(handler=finalize)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        args.handler(args)
    except ContractError as exc:
        print(f"windows-native-evidence:error: {exc}", file=sys.stderr)
        return 1
    print(f"windows-native-evidence:{args.command}:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
