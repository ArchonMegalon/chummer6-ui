#!/usr/bin/env python3
"""Compose the Windows-only unsigned preview shelf without publishing it.

The materializer replaces only the Avalonia ``win-x64`` installer, payload,
and payload metadata sidecar.
Every incumbent non-Windows managed artifact and every ancillary file is copied
byte-for-byte with its mode.  The resulting manifests say exactly what this
lane is: preview, Windows-only, unsigned, and not cross-run bit reproducible.
"""

from __future__ import annotations

import argparse
import ctypes
import errno
import hashlib
import json
import os
import re
import shutil
import stat
import tempfile
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any
from urllib.parse import urlsplit


CANONICAL_MANIFEST = "RELEASE_CHANNEL.generated.json"
COMPATIBILITY_MANIFEST = "releases.json"
INSTALLER_NAME = "chummer-avalonia-win-x64-installer.exe"
PAYLOAD_NAME = "chummer-avalonia-win-x64-payload.zip"
PAYLOAD_SIDECAR_NAME = f"{PAYLOAD_NAME}.json"
ARTIFACT_ID = "avalonia-win-x64-installer"
CHANNEL = "preview"
PLATFORM_SCOPE = "windows_only"
PREVIEW_POLICY = "preview_policy"
DOWNLOAD_ROOT = "https://chummer.run/downloads/files"
PROMOTED_DESKTOP_HEADS = ("avalonia",)
REGISTRY_PROJECTION_IDENTITY_KEYS = (
    "codeDeployCurrentShelfAuthority",
    "projectionProfile",
    "projectionStage",
    "registryCommit",
    "registry_commit",
)
OPTIONAL_AUTHORITY_POSTURE_FIELDS = (
    "codeDeploymentAuthority",
    "releaseUploadAuthority",
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
TIMESTAMP_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)


class StageError(RuntimeError):
    """A fail-closed unsigned-preview stage contract error."""


def fail(message: str) -> None:
    raise StageError(message)


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        raw = path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
            fail(f"{label} is not canonical UTF-8 JSON")
        payload = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda value: fail(
                f"{label} contains non-finite JSON number {value}"
            ),
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def canonical_json_bytes(payload: object) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            fail(f"expected a regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        fail(f"could not hash {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def require_regular(path: Path, label: str) -> os.stat_result:
    try:
        metadata = path.lstat()
    except OSError as exc:
        fail(f"could not inspect {label}: {exc}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        fail(f"{label} must be one regular non-link file")
    return metadata


def portable_name(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    portable = PurePosixPath(value)
    if (
        value in {"", ".", ".."}
        or portable.name != value
        or "/" in value
        or "\\" in value
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        fail(f"{label} is not a portable file name")
    return value


def require_version(value: str) -> str:
    if VERSION_RE.fullmatch(value) is None or ".." in value:
        fail("preview version is not portable")
    return value


def require_timestamp(value: str) -> str:
    if TIMESTAMP_RE.fullmatch(value) is None:
        fail("published timestamp must be UTC with whole-second precision")
    try:
        datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        fail("published timestamp is not a real UTC timestamp")
    return value


def file_inventory(root: Path) -> list[dict[str, object]]:
    if root.is_symlink() or not root.is_dir():
        fail(f"inventory root must be one physical directory: {root}")
    rows: list[dict[str, object]] = []
    casefolded: set[str] = set()
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            if relative.casefold() in casefolded:
                fail(f"shelf has a case-colliding path: {relative}")
            casefolded.add(relative.casefold())
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"shelf contains a symbolic link: {relative}")
            if name in {"", ".", ".."} or "\\" in name:
                fail(f"shelf contains a non-portable path: {relative}")
            if stat.S_ISDIR(metadata.st_mode):
                continue
            if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
                fail(f"shelf contains a special or hard-linked entry: {relative}")
            rows.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": relative,
                    "sha256": sha256_file(path),
                    "sizeBytes": metadata.st_size,
                }
            )
    return sorted(rows, key=lambda row: str(row["path"]))


def directory_mode_inventory(root: Path) -> list[dict[str, object]]:
    result: list[dict[str, object]] = []
    for current, directories, _files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted(directories):
            path = current_path / name
            metadata = path.lstat()
            if path.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
                fail(f"shelf contains a linked or special directory: {path}")
            result.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": path.relative_to(root).as_posix(),
                }
            )
    return sorted(result, key=lambda row: str(row["path"]))


def copy_tree_exact(
    source: Path, destination: Path
) -> tuple[dict[str, int], dict[str, int], int]:
    source_root = source.lstat()
    if source.is_symlink() or not stat.S_ISDIR(source_root.st_mode):
        fail("incumbent shelf root must be one physical directory")
    before = file_inventory(source)
    directory_modes_before = directory_mode_inventory(source)
    root_mode_before = stat.S_IMODE(source_root.st_mode)
    if destination.exists() or destination.is_symlink():
        fail("stage output must be absent")
    shutil.copytree(source, destination, symlinks=False, copy_function=shutil.copy2)
    after = file_inventory(destination)
    directory_modes_after = directory_mode_inventory(destination)
    destination_root = destination.lstat()
    if (
        destination.is_symlink()
        or not stat.S_ISDIR(destination_root.st_mode)
        or before != after
        or directory_modes_before != directory_modes_after
        or stat.S_IMODE(destination_root.st_mode) != root_mode_before
    ):
        fail("incumbent shelf changed while it was copied")
    return (
        {str(row["path"]): int(row["mode"]) for row in before},
        {str(row["path"]): int(row["mode"]) for row in directory_modes_before},
        root_mode_before,
    )


def directory_identity(metadata: os.stat_result) -> tuple[int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_uid,
        stat.S_IMODE(metadata.st_mode),
    )


def open_safe_parent(path: Path, label: str) -> tuple[int, os.stat_result]:
    path = path.absolute()
    if not path.is_absolute() or path != Path(os.path.normpath(str(path))):
        fail(f"{label} path must be canonical and absolute")
    flags = os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0)
    descriptor = -1
    try:
        descriptor = os.open("/", flags)
        for component in path.parts[1:]:
            if component in {"", ".", ".."}:
                fail(f"{label} has a non-canonical component")
            next_descriptor = os.open(component, flags, dir_fd=descriptor)
            os.close(descriptor)
            descriptor = next_descriptor
        metadata = os.fstat(descriptor)
    except OSError as exc:
        if descriptor >= 0:
            os.close(descriptor)
        fail(f"could not open physical {label}: {exc}")
    if (
        not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or metadata.st_mode & (stat.S_IWGRP | stat.S_IWOTH)
    ):
        os.close(descriptor)
        fail(f"{label} must be one physical owner-controlled directory")
    return descriptor, metadata


def require_safe_parent(path: Path, label: str) -> os.stat_result:
    descriptor, metadata = open_safe_parent(path, label)
    os.close(descriptor)
    return metadata


def same_physical_entry(
    first: os.stat_result, second: os.stat_result
) -> bool:
    return (
        first.st_dev == second.st_dev
        and first.st_ino == second.st_ino
        and stat.S_IFMT(first.st_mode) == stat.S_IFMT(second.st_mode)
    )


def open_private_tree_root(root: Path, label: str) -> tuple[int, int]:
    root = root.absolute()
    if root != Path(os.path.normpath(str(root))) or root.name in {"", ".", ".."}:
        fail(f"{label} path must be canonical and absolute")
    parent_descriptor, _metadata = open_safe_parent(
        root.parent, f"{label} parent"
    )
    descriptor = -1
    try:
        inspected = os.stat(
            root.name, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if not stat.S_ISDIR(inspected.st_mode):
            fail(f"{label} must be one physical directory")
        descriptor = os.open(
            root.name,
            os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_descriptor,
        )
        opened = os.fstat(descriptor)
        if (
            not same_physical_entry(inspected, opened)
            or opened.st_uid != os.geteuid()
        ):
            fail(f"{label} identity or ownership changed")
        return parent_descriptor, descriptor
    except BaseException:
        if descriptor >= 0:
            os.close(descriptor)
        os.close(parent_descriptor)
        raise


def make_private_tree_owner_writable(root: Path) -> None:
    """Make only a held, owner-controlled compose tree mutable.

    Every traversal is relative to held directory descriptors and refuses
    links, special entries, ownership changes, or entry replacement.
    """

    parent_descriptor, root_descriptor = open_private_tree_root(
        root, "private compose tree"
    )

    def make_directory_writable(
        descriptor: int, relative: PurePosixPath
    ) -> None:
        metadata = os.fstat(descriptor)
        if (
            not stat.S_ISDIR(metadata.st_mode)
            or metadata.st_uid != os.geteuid()
        ):
            fail(f"private compose directory changed at {relative}")
        os.fchmod(
            descriptor,
            stat.S_IMODE(metadata.st_mode) | stat.S_IWUSR | stat.S_IXUSR,
        )
        with os.scandir(descriptor) as iterator:
            entries = sorted(iterator, key=lambda entry: entry.name)
        for entry in entries:
            inspected = entry.stat(follow_symlinks=False)
            child_relative = relative / entry.name
            if stat.S_ISLNK(inspected.st_mode):
                fail(
                    "private compose tree contains a symbolic link: "
                    f"{child_relative}"
                )
            if inspected.st_uid != os.geteuid():
                fail(f"private compose entry is not owner-controlled: {child_relative}")
            if stat.S_ISDIR(inspected.st_mode):
                child_descriptor = os.open(
                    entry.name,
                    os.O_RDONLY
                    | os.O_DIRECTORY
                    | getattr(os, "O_NOFOLLOW", 0),
                    dir_fd=descriptor,
                )
                try:
                    opened = os.fstat(child_descriptor)
                    if not same_physical_entry(inspected, opened):
                        fail(f"private compose directory changed at {child_relative}")
                    make_directory_writable(child_descriptor, child_relative)
                finally:
                    os.close(child_descriptor)
                continue
            if not stat.S_ISREG(inspected.st_mode) or inspected.st_nlink != 1:
                fail(f"private compose tree contains a special entry: {child_relative}")
            child_descriptor = os.open(
                entry.name,
                os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            try:
                opened = os.fstat(child_descriptor)
                if (
                    not same_physical_entry(inspected, opened)
                    or not stat.S_ISREG(opened.st_mode)
                    or opened.st_nlink != 1
                ):
                    fail(f"private compose file changed at {child_relative}")
                os.fchmod(
                    child_descriptor,
                    stat.S_IMODE(opened.st_mode) | stat.S_IWUSR,
                )
            finally:
                os.close(child_descriptor)

    try:
        make_directory_writable(root_descriptor, PurePosixPath("."))
    finally:
        os.close(root_descriptor)
        os.close(parent_descriptor)


def restore_private_tree_modes(
    root: Path,
    incumbent_file_modes: dict[str, int],
    incumbent_directory_modes: dict[str, int],
    incumbent_root_mode: int,
    final_file_modes: dict[str, int],
    removed_incumbent_files: set[str],
) -> None:
    """Restore retained modes and enforce modes for every generated file."""

    parent_descriptor, root_descriptor = open_private_tree_root(
        root, "private compose tree"
    )
    seen_files: set[str] = set()
    seen_directories: set[str] = set()

    def restore_directory(
        descriptor: int, relative: PurePosixPath
    ) -> None:
        with os.scandir(descriptor) as iterator:
            entries = sorted(iterator, key=lambda entry: entry.name)
        for entry in entries:
            inspected = entry.stat(follow_symlinks=False)
            child_relative = (
                PurePosixPath(entry.name)
                if relative == PurePosixPath(".")
                else relative / entry.name
            )
            child_name = child_relative.as_posix()
            if stat.S_ISLNK(inspected.st_mode):
                fail(
                    "private compose tree contains a symbolic link: "
                    f"{child_name}"
                )
            if inspected.st_uid != os.geteuid():
                fail(f"private compose entry is not owner-controlled: {child_name}")
            if stat.S_ISDIR(inspected.st_mode):
                if child_name not in incumbent_directory_modes:
                    fail(f"private compose tree has an unexpected directory: {child_name}")
                child_descriptor = os.open(
                    entry.name,
                    os.O_RDONLY
                    | os.O_DIRECTORY
                    | getattr(os, "O_NOFOLLOW", 0),
                    dir_fd=descriptor,
                )
                try:
                    opened = os.fstat(child_descriptor)
                    if not same_physical_entry(inspected, opened):
                        fail(f"private compose directory changed at {child_name}")
                    restore_directory(child_descriptor, child_relative)
                    os.fchmod(
                        child_descriptor, incumbent_directory_modes[child_name]
                    )
                    seen_directories.add(child_name)
                finally:
                    os.close(child_descriptor)
                continue
            if not stat.S_ISREG(inspected.st_mode) or inspected.st_nlink != 1:
                fail(f"private compose tree contains a special entry: {child_name}")
            expected_mode = final_file_modes.get(
                child_name, incumbent_file_modes.get(child_name)
            )
            if expected_mode is None:
                fail(f"private compose tree has an unexpected file: {child_name}")
            child_descriptor = os.open(
                entry.name,
                os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            try:
                opened = os.fstat(child_descriptor)
                if (
                    not same_physical_entry(inspected, opened)
                    or not stat.S_ISREG(opened.st_mode)
                    or opened.st_nlink != 1
                ):
                    fail(f"private compose file changed at {child_name}")
                os.fchmod(child_descriptor, expected_mode)
                seen_files.add(child_name)
            finally:
                os.close(child_descriptor)

    try:
        restore_directory(root_descriptor, PurePosixPath("."))
        if seen_directories != set(incumbent_directory_modes):
            missing = sorted(set(incumbent_directory_modes) - seen_directories)
            fail(f"private compose tree lost incumbent directories: {missing}")
        missing_files = (
            set(incumbent_file_modes) - seen_files - removed_incumbent_files
        )
        if missing_files:
            fail(
                "private compose tree lost retained incumbent files: "
                f"{sorted(missing_files)}"
            )
        missing_generated_files = set(final_file_modes) - seen_files
        if missing_generated_files:
            fail(
                "private compose tree lost generated files: "
                f"{sorted(missing_generated_files)}"
            )
        os.fchmod(root_descriptor, incumbent_root_mode)
    finally:
        os.close(root_descriptor)
        os.close(parent_descriptor)


def remove_private_tree(root: Path) -> None:
    """Remove one private tree through held dirfds without following links."""

    root = root.absolute()
    if root != Path(os.path.normpath(str(root))) or root.name in {"", ".", ".."}:
        fail("private cleanup path must be canonical and absolute")
    parent_descriptor, _metadata = open_safe_parent(
        root.parent, "private cleanup parent"
    )

    def remove_contents(descriptor: int) -> None:
        metadata = os.fstat(descriptor)
        os.fchmod(
            descriptor,
            stat.S_IMODE(metadata.st_mode) | stat.S_IRUSR | stat.S_IWUSR | stat.S_IXUSR,
        )
        with os.scandir(descriptor) as iterator:
            entries = sorted(iterator, key=lambda entry: entry.name)
        for entry in entries:
            inspected = entry.stat(follow_symlinks=False)
            if stat.S_ISDIR(inspected.st_mode) and not stat.S_ISLNK(
                inspected.st_mode
            ):
                child_descriptor = os.open(
                    entry.name,
                    os.O_RDONLY
                    | os.O_DIRECTORY
                    | getattr(os, "O_NOFOLLOW", 0),
                    dir_fd=descriptor,
                )
                try:
                    opened = os.fstat(child_descriptor)
                    if not same_physical_entry(inspected, opened):
                        fail(f"private cleanup entry changed: {entry.name}")
                    remove_contents(child_descriptor)
                finally:
                    os.close(child_descriptor)
                os.rmdir(entry.name, dir_fd=descriptor)
            else:
                os.unlink(entry.name, dir_fd=descriptor)

    try:
        try:
            inspected = os.stat(
                root.name, dir_fd=parent_descriptor, follow_symlinks=False
            )
        except FileNotFoundError:
            return
        if not stat.S_ISDIR(inspected.st_mode):
            os.unlink(root.name, dir_fd=parent_descriptor)
            return
        root_descriptor = os.open(
            root.name,
            os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_descriptor,
        )
        try:
            opened = os.fstat(root_descriptor)
            if not same_physical_entry(inspected, opened):
                fail("private cleanup root changed")
            remove_contents(root_descriptor)
        finally:
            os.close(root_descriptor)
        os.rmdir(root.name, dir_fd=parent_descriptor)
        os.fsync(parent_descriptor)
    finally:
        os.close(parent_descriptor)


def atomic_rename_noreplace(source: Path, target: Path) -> None:
    """Atomically publish one directory while refusing a racing destination."""

    source = source.absolute()
    target = target.absolute()
    if source.parent != target.parent:
        fail("exclusive directory publication requires one parent filesystem")
    parent_descriptor, parent_before = open_safe_parent(
        source.parent, "publication parent"
    )
    try:
        source_metadata = os.stat(
            source.name, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if not stat.S_ISDIR(source_metadata.st_mode):
            fail("exclusive directory publication source is not physical")
        try:
            os.stat(target.name, dir_fd=parent_descriptor, follow_symlinks=False)
        except FileNotFoundError:
            pass
        except OSError as exc:
            fail(f"could not inspect publication destination: {exc}")
        else:
            fail("publication destination already exists")
        libc = ctypes.CDLL(None, use_errno=True)
        renameat2 = getattr(libc, "renameat2", None)
        if renameat2 is None:
            fail("renameat2 no-replace support is unavailable")
        renameat2.argtypes = [
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_uint,
        ]
        renameat2.restype = ctypes.c_int
        result = renameat2(
            parent_descriptor,
            os.fsencode(source.name),
            parent_descriptor,
            os.fsencode(target.name),
            1,
        )
        if result != 0:
            error = ctypes.get_errno()
            if error in {errno.EEXIST, errno.ENOTEMPTY}:
                fail("publication destination appeared during exclusive commit")
            fail(f"exclusive directory publication failed: {os.strerror(error)}")
        target_metadata = os.stat(
            target.name, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if directory_identity(target_metadata) != directory_identity(source_metadata):
            fail("exclusive directory publication target identity changed")
        os.fsync(parent_descriptor)
        reopened, parent_after = open_safe_parent(
            source.parent, "publication parent"
        )
        try:
            if directory_identity(parent_before) != directory_identity(parent_after):
                fail("publication parent identity changed during exclusive commit")
        finally:
            os.close(reopened)
    finally:
        os.close(parent_descriptor)


def write_new(path: Path, payload: object, mode: int) -> None:
    data = canonical_json_bytes(payload)
    descriptor = os.open(
        path,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        mode,
    )
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written < 1:
                fail(f"write made no progress: {path}")
            view = view[written:]
        os.fchmod(descriptor, mode)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def replace_json(path: Path, payload: object, mode: int) -> None:
    path.unlink()
    write_new(path, payload, mode)


def manifest_rows(manifest: dict[str, Any], label: str) -> list[dict[str, Any]]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail(f"{label} artifacts must be an array")
    result: list[dict[str, Any]] = []
    seen: set[tuple[str, str, str, str]] = set()
    for row in rows:
        if not isinstance(row, dict):
            fail(f"{label} has a non-object artifact")
        head = row.get("head") or row.get("headId")
        platform = row.get("platformId") or row.get("platform")
        rid = row.get("rid")
        artifact_id = row.get("artifactId") or row.get("id")
        if not all(isinstance(item, str) and item for item in (head, platform, rid, artifact_id)):
            fail(f"{label} has an ambiguous desktop tuple")
        identity = (head, platform, rid, artifact_id)
        if identity in seen:
            fail(f"{label} repeats desktop tuple {identity!r}")
        seen.add(identity)
        portable_name(row.get("fileName") or row.get("name"), f"{label} fileName")
        result.append(row)
    return result


def compatibility_rows(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    rows = manifest.get("downloads")
    if not isinstance(rows, list):
        fail("incumbent compatibility downloads must be an array")
    result: list[dict[str, Any]] = []
    seen: set[str] = set()
    for row in rows:
        if not isinstance(row, dict):
            fail("incumbent compatibility manifest has a non-object row")
        name = portable_name(row.get("fileName") or row.get("name"), "compatibility fileName")
        if name.casefold() in seen:
            fail(f"incumbent compatibility repeats or case-collides at {name}")
        seen.add(name.casefold())
        result.append(row)
    return result


def row_platform(row: dict[str, Any]) -> str:
    value = row.get("platformId") or row.get("platform")
    if not isinstance(value, str):
        fail("desktop row has no exact platform identity")
    return value.lower()


def require_promoted_windows_desktop_identity(
    rows: list[dict[str, Any]], label: str
) -> None:
    for row in rows:
        if row_platform(row) != "windows":
            continue
        head = row.get("head") or row.get("headId")
        if head not in PROMOTED_DESKTOP_HEADS or row.get("rid") != "win-x64":
            fail(
                f"{label} contains a Windows artifact outside the promoted "
                "avalonia/win-x64 tuple"
            )


def managed_names(rows: list[dict[str, Any]]) -> set[str]:
    result: set[str] = set()
    for row in rows:
        result.add(portable_name(row.get("fileName") or row.get("name"), "managed fileName"))
        payload = row.get("payloadFileName") or row.get("payloadName")
        if payload is not None:
            result.add(portable_name(payload, "managed payloadFileName"))
    return result


def verify_unsigned_pe(path: Path) -> None:
    metadata = require_regular(path, "Windows installer")
    if metadata.st_size < 256:
        fail("Windows installer is too small to be a PE image")
    with path.open("rb") as handle:
        dos = handle.read(64)
        if dos[:2] != b"MZ":
            fail("Windows installer lacks the PE DOS signature")
        pe_offset = int.from_bytes(dos[60:64], "little")
        if pe_offset < 64 or pe_offset > metadata.st_size - 160:
            fail("Windows installer has an invalid PE header offset")
        handle.seek(pe_offset)
        if handle.read(4) != b"PE\x00\x00":
            fail("Windows installer lacks the PE signature")
        coff = handle.read(20)
        optional_size = int.from_bytes(coff[16:18], "little")
        optional = handle.read(optional_size)
    if len(optional) != optional_size or optional_size < 136:
        fail("Windows installer has a truncated PE optional header")
    magic = int.from_bytes(optional[:2], "little")
    directory_offset = 96 if magic == 0x10B else 112 if magic == 0x20B else -1
    security_offset = directory_offset + 4 * 8
    if directory_offset < 0 or len(optional) < security_offset + 8:
        fail("Windows installer PE optional header is unsupported")
    certificate_file_offset = int.from_bytes(optional[security_offset : security_offset + 4], "little")
    certificate_size = int.from_bytes(optional[security_offset + 4 : security_offset + 8], "little")
    if certificate_file_offset != 0 or certificate_size != 0:
        fail("Windows preview installer has an Authenticode certificate table")


def installer_row(
    version: str,
    published_at: str,
    installer_sha: str,
    installer_size: int,
    payload_sha: str,
    payload_size: int,
    download_root: str,
) -> dict[str, Any]:
    base = download_root.rstrip("/")
    return {
        "arch": "x64",
        "artifactId": ARTIFACT_ID,
        "channel": CHANNEL,
        "channelId": CHANNEL,
        "compatibilityReason": None,
        "compatibilityState": "compatible",
        "crossRunBitReproducible": False,
        "downloadUrl": f"{base}/{INSTALLER_NAME}",
        "fileName": INSTALLER_NAME,
        "generatedAt": published_at,
        "generated_at": published_at,
        "head": "avalonia",
        "id": ARTIFACT_ID,
        "installAccessClass": "open_public",
        "installerMode": "bootstrap",
        "kind": "installer",
        "payloadAcquisitionMode": "download",
        "payloadDownloadUrl": f"{base}/{PAYLOAD_NAME}",
        "payloadFileName": PAYLOAD_NAME,
        "payloadSha256": payload_sha,
        "payloadSizeBytes": payload_size,
        "platform": "windows",
        "platformLabel": "Avalonia Desktop Windows X64 Installer",
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "releaseVersion": version,
        "rid": "win-x64",
        "sha256": installer_sha,
        "signature": {
            "policy": PREVIEW_POLICY,
            "required": False,
            "status": "unsigned",
        },
        "sizeBytes": installer_size,
        "version": version,
    }


def payload_sidecar(
    version: str,
    payload_sha: str,
    payload_size: int,
    download_root: str,
) -> dict[str, Any]:
    """Return the canonical metadata contract for the fresh bootstrap payload."""

    base = download_root.rstrip("/")
    return {
        "contractName": "chummer6-ui.windows_bootstrap_payload",
        "downloadUrl": f"{base}/{PAYLOAD_NAME}",
        "fileName": PAYLOAD_NAME,
        "installerFileName": INSTALLER_NAME,
        "payloadAcquisitionMode": "download",
        "releaseVersion": version,
        "sha256": payload_sha,
        "sizeBytes": payload_size,
    }


def compatibility_row(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "arch": row["arch"],
        "artifactId": row["artifactId"],
        "channel": CHANNEL,
        "channelId": CHANNEL,
        "compatibilityReason": row["compatibilityReason"],
        "compatibilityState": row["compatibilityState"],
        "crossRunBitReproducible": False,
        "fileName": row["fileName"],
        "flavor": row["kind"],
        "format": "exe",
        "head": row["head"],
        "id": row["artifactId"],
        "installAccessClass": row["installAccessClass"],
        "installerMode": row["installerMode"],
        "kind": row["kind"],
        "payloadAcquisitionMode": row["payloadAcquisitionMode"],
        "payloadDownloadUrl": row["payloadDownloadUrl"],
        "payloadFileName": row["payloadFileName"],
        "payloadSha256": row["payloadSha256"],
        "payloadSizeBytes": row["payloadSizeBytes"],
        "platform": row["platformLabel"],
        "platformId": "windows",
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "releaseVersion": row["releaseVersion"],
        "rid": row["rid"],
        "sha256": row["sha256"],
        "signature": row["signature"],
        "sizeBytes": row["sizeBytes"],
        "url": row["downloadUrl"],
        "version": row["version"],
    }


def apply_release_identity(
    manifest: dict[str, Any], version: str, published_at: str
) -> dict[str, Any]:
    result = dict(manifest)
    for key in REGISTRY_PROJECTION_IDENTITY_KEYS:
        result.pop(key, None)
    # Legacy compatibility projections materialized absent optional booleans as
    # null. Preserve every non-null value so Registry remains the fail-closed
    # authority validator instead of having UI coerce an unsafe posture.
    for key in OPTIONAL_AUTHORITY_POSTURE_FIELDS:
        if result.get(key) is None:
            result.pop(key, None)
    result.update(
        {
            "channel": CHANNEL,
            "channelId": CHANNEL,
            "crossRunBitReproducible": False,
            "generatedAt": published_at,
            "generated_at": published_at,
            "platformScope": PLATFORM_SCOPE,
            "previewPolicy": PREVIEW_POLICY,
            "publicationAuthorized": False,
            "publishedAt": published_at,
            "releaseVersion": version,
            "signature": {
                "policy": PREVIEW_POLICY,
                "required": False,
                "status": "unsigned",
            },
            "uploadAuthorized": False,
            "deployAuthorized": False,
            "version": version,
        }
    )
    return result


def reconcile_registry_counts(
    manifest: dict[str, Any], rows_key: str
) -> None:
    rows = manifest.get(rows_key)
    if not isinstance(rows, list):
        fail(f"{rows_key} must be an array before Registry count reconciliation")
    artifact_count = len(rows)
    compatible_count = sum(
        1
        for row in rows
        if isinstance(row, dict)
        and str(row.get("compatibilityState") or "").strip().lower() == "compatible"
    )
    registry = manifest.get("registryBoundaryCoverage")
    if registry is not None:
        if not isinstance(registry, dict):
            fail("registryBoundaryCoverage must be an object")
        persistence = registry.get("persistence")
        compatibility = registry.get("compatibility")
        if persistence is not None:
            if not isinstance(persistence, dict):
                fail("registryBoundaryCoverage.persistence must be an object")
            persistence["artifactCount"] = artifact_count
        if compatibility is not None:
            if not isinstance(compatibility, dict):
                fail("registryBoundaryCoverage.compatibility must be an object")
            compatibility["compatibleArtifactCount"] = compatible_count
            compatibility["unknownArtifactCount"] = artifact_count - compatible_count
    coverage = manifest.get("desktopTupleCoverage")
    if coverage is not None:
        if not isinstance(coverage, dict):
            fail("desktopTupleCoverage must be an object")
        installer_rows = [
            row
            for row in rows
            if isinstance(row, dict)
            and str(row.get("kind") or row.get("flavor") or "").lower()
            == "installer"
        ]
        for key in (
            "artifactCount",
            "desktopArtifactCount",
            "installerArtifactCount",
            "installerTupleCount",
            "promotedInstallerTupleCount",
        ):
            if key in coverage:
                coverage[key] = (
                    artifact_count
                    if key in {"artifactCount", "desktopArtifactCount"}
                    else len(installer_rows)
                )


def require_download_root(value: str) -> str:
    parsed = urlsplit(value)
    if (
        value != DOWNLOAD_ROOT
        or parsed.scheme != "https"
        or parsed.netloc != "chummer.run"
        or parsed.path != "/downloads/files"
        or parsed.query
        or parsed.fragment
        or parsed.username is not None
        or parsed.password is not None
    ):
        fail(f"download root must be exactly {DOWNLOAD_ROOT}")
    return value


def materialize(args: argparse.Namespace) -> dict[str, object]:
    version = require_version(args.expected_version)
    published_at = require_timestamp(args.published_at)
    if COMMIT_RE.fullmatch(args.source_sha) is None:
        fail("source SHA must be an exact lowercase commit")
    download_root = require_download_root(args.download_root)
    incumbent = args.incumbent_root.resolve(strict=True)
    build = args.build_root.resolve(strict=True)
    output = args.output.absolute()
    if output.exists() or output.is_symlink():
        fail("stage output must be absent")
    if output == incumbent or incumbent in output.parents or output in incumbent.parents:
        fail("stage output and incumbent shelf must be disjoint")
    incumbent_inventory = file_inventory(incumbent)
    canonical_path = incumbent / CANONICAL_MANIFEST
    compatibility_path = incumbent / COMPATIBILITY_MANIFEST
    canonical_mode = stat.S_IMODE(require_regular(canonical_path, "incumbent canonical manifest").st_mode)
    compatibility_mode = stat.S_IMODE(require_regular(compatibility_path, "incumbent compatibility manifest").st_mode)
    incumbent_manifest = read_json(canonical_path, "incumbent canonical manifest")
    incumbent_compatibility = read_json(compatibility_path, "incumbent compatibility manifest")
    canonical_rows = manifest_rows(incumbent_manifest, "incumbent canonical manifest")
    download_rows = compatibility_rows(incumbent_compatibility)
    incumbent_windows = [row for row in canonical_rows if row_platform(row) == "windows"]
    if any(
        (row.get("head") or row.get("headId"), row.get("rid"))
        != ("avalonia", "win-x64")
        for row in incumbent_windows
    ):
        fail("incumbent shelf has a Windows tuple outside avalonia/win-x64")
    if len(incumbent_windows) > 1:
        fail("incumbent shelf has more than one avalonia/win-x64 tuple")
    incumbent_managed = managed_names(canonical_rows)
    incumbent_file_rows = {
        str(row["path"]): row
        for row in incumbent_inventory
        if str(row["path"]).startswith("files/")
    }
    for name in incumbent_managed:
        if f"files/{name}" not in incumbent_file_rows:
            fail(f"incumbent manifest references missing bytes: {name}")

    installer = build / "files" / INSTALLER_NAME
    payload = build / "files" / PAYLOAD_NAME
    installer_metadata = require_regular(installer, "fresh Windows installer")
    payload_metadata = require_regular(payload, "fresh Windows payload")
    if payload_metadata.st_size < 1:
        fail("fresh Windows payload is empty")
    verify_unsigned_pe(installer)
    installer_sha = sha256_file(installer)
    payload_sha = sha256_file(payload)
    fresh_payload_sidecar = payload_sidecar(
        version,
        payload_sha,
        payload_metadata.st_size,
        download_root,
    )
    fresh = installer_row(
        version,
        published_at,
        installer_sha,
        installer_metadata.st_size,
        payload_sha,
        payload_metadata.st_size,
        download_root,
    )
    final_canonical_rows = [
        row for row in canonical_rows if row_platform(row) != "windows"
    ] + [fresh]
    windows_compat_names = managed_names(incumbent_windows)
    final_download_rows = [
        row
        for row in download_rows
        if portable_name(row.get("fileName") or row.get("name"), "compatibility fileName")
        not in windows_compat_names
        and str(row.get("platformId") or row.get("platform", "")).lower()
        not in {"windows", "avalonia desktop windows x64 installer"}
    ] + [compatibility_row(fresh)]
    require_promoted_windows_desktop_identity(
        final_canonical_rows, "proposed canonical manifest"
    )
    require_promoted_windows_desktop_identity(
        final_download_rows, "proposed compatibility manifest"
    )
    public_manifest = apply_release_identity(incumbent_manifest, version, published_at)
    public_manifest["artifacts"] = final_canonical_rows
    coverage = dict(public_manifest.get("desktopTupleCoverage") or {})
    coverage["requiredDesktopHeads"] = list(PROMOTED_DESKTOP_HEADS)
    coverage["requiredDesktopPlatforms"] = sorted(
        {row_platform(row) for row in final_canonical_rows}
    )
    public_manifest["desktopTupleCoverage"] = coverage
    public_compatibility = apply_release_identity(
        incumbent_compatibility, version, published_at
    )
    public_compatibility["downloads"] = final_download_rows
    compatibility_coverage = dict(
        public_compatibility.get("desktopTupleCoverage") or {}
    )
    compatibility_coverage["requiredDesktopHeads"] = list(
        PROMOTED_DESKTOP_HEADS
    )
    compatibility_coverage["requiredDesktopPlatforms"] = coverage[
        "requiredDesktopPlatforms"
    ]
    public_compatibility["desktopTupleCoverage"] = compatibility_coverage
    reconcile_registry_counts(public_manifest, "artifacts")
    reconcile_registry_counts(public_compatibility, "downloads")

    output.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(
        tempfile.mkdtemp(prefix=f".{output.name}.compose-", dir=output.parent)
    )
    try:
        staging.rmdir()
        (
            incumbent_file_modes,
            incumbent_directory_modes,
            incumbent_root_mode,
        ) = copy_tree_exact(incumbent, staging)
        make_private_tree_owner_writable(staging)
        staging_files = staging / "files"
        removed_incumbent_files: set[str] = set()
        for row in incumbent_windows:
            for name in managed_names([row]):
                removed_incumbent_files.add(f"files/{name}")
                target = staging_files / name
                if target.is_file() and not target.is_symlink():
                    target.unlink()
        for source, name in ((installer, INSTALLER_NAME), (payload, PAYLOAD_NAME)):
            target = staging_files / name
            if target.exists() or target.is_symlink():
                target.unlink()
            shutil.copy2(source, target, follow_symlinks=False)
            os.chmod(target, 0o644, follow_symlinks=False)
        sidecar_target = staging_files / PAYLOAD_SIDECAR_NAME
        if sidecar_target.exists() or sidecar_target.is_symlink():
            sidecar_target.unlink()
        write_new(sidecar_target, fresh_payload_sidecar, 0o644)
        payload_sidecar_sha = sha256_file(sidecar_target)
        replace_json(staging / CANONICAL_MANIFEST, public_manifest, canonical_mode)
        replace_json(
            staging / COMPATIBILITY_MANIFEST,
            public_compatibility,
            compatibility_mode,
        )
        restore_private_tree_modes(
            staging,
            incumbent_file_modes,
            incumbent_directory_modes,
            incumbent_root_mode,
            {
                CANONICAL_MANIFEST: canonical_mode,
                COMPATIBILITY_MANIFEST: compatibility_mode,
                f"files/{INSTALLER_NAME}": 0o644,
                f"files/{PAYLOAD_NAME}": 0o644,
                f"files/{PAYLOAD_SIDECAR_NAME}": 0o644,
            },
            removed_incumbent_files,
        )
        final_inventory = file_inventory(staging)
        atomic_rename_noreplace(staging, output)
        staging = Path()
    finally:
        if staging != Path():
            remove_private_tree(staging)
    return {
        "channel": CHANNEL,
        "crossRunBitReproducible": False,
        "fullShelfInventorySha256": hashlib.sha256(
            canonical_json_bytes(final_inventory)
        ).hexdigest(),
        "installerSha256": installer_sha,
        "output": str(output),
        "payloadSha256": payload_sha,
        "payloadSidecarSha256": payload_sidecar_sha,
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "signature": {
            "policy": PREVIEW_POLICY,
            "required": False,
            "status": "unsigned",
        },
        "sourceSha": args.source_sha,
        "version": version,
    }


def publish_directory(args: argparse.Namespace) -> dict[str, object]:
    source = args.source.absolute()
    output = args.output.absolute()
    if source.parent != output.parent:
        fail("candidate staging and output must share one parent")
    atomic_rename_noreplace(source, output)
    inventory = file_inventory(output)
    return {
        "fileCount": len(inventory),
        "inventorySha256": hashlib.sha256(canonical_json_bytes(inventory)).hexdigest(),
        "output": str(output),
        "status": "published_to_private_stage",
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    compose = subparsers.add_parser("materialize")
    compose.add_argument("--incumbent-root", required=True, type=Path)
    compose.add_argument("--build-root", required=True, type=Path)
    compose.add_argument("--expected-version", required=True)
    compose.add_argument("--published-at", required=True)
    compose.add_argument("--source-sha", required=True)
    compose.add_argument(
        "--download-root",
        default=DOWNLOAD_ROOT,
    )
    compose.add_argument("--output", required=True, type=Path)
    publish = subparsers.add_parser("publish-directory")
    publish.add_argument("--source", required=True, type=Path)
    publish.add_argument("--output", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        result = (
            materialize(args)
            if args.command == "materialize"
            else publish_directory(args)
        )
    except (StageError, OSError, ValueError) as exc:
        print(f"unsigned-windows-preview-stage:error: {exc}", file=os.sys.stderr)
        return 2
    for key in sorted(result):
        value = result[key]
        if isinstance(value, (dict, list)):
            value = json.dumps(value, sort_keys=True, separators=(",", ":"))
        print(f"{key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
