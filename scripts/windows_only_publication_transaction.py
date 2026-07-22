#!/usr/bin/env python3
"""Prepare and atomically exchange Windows-only downloads shelf generations.

This helper has no network or upload capability.  It preserves every incumbent
ancillary file byte-for-byte, replaces only the approved desktop shelf, and
uses Linux renameat2(RENAME_EXCHANGE) so a live directory is never observed as
a partially copied generation.
"""

from __future__ import annotations

import argparse
import ctypes
import fcntl
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import sys
import tempfile
from pathlib import Path, PurePosixPath
from typing import Any


def _load_scope_module():
    path = Path(__file__).resolve().with_name("preview_nightly_publication_scope.py")
    spec = importlib.util.spec_from_file_location(
        "windows_only_publication_transaction_scope", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load Windows-only publication scope helper")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SCOPE = _load_scope_module()
CONTRACT_NAME = "chummer6-ui.windows-only-publication-generation"
CONTRACT_VERSION = 1
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
DESKTOP_ARTIFACT_RE = re.compile(
    r"^chummer-(?:avalonia|blazor-desktop|6)-.*(?:\.exe|\.zip|\.tar\.gz|\.deb|\.pkg|\.dmg|\.msix|\.zip\.json)$",
    re.IGNORECASE,
)
RENAME_EXCHANGE = 2
RUN_UPLOAD_ROOT_FILES = (
    SCOPE.COMPATIBILITY_MANIFEST_NAME,
    SCOPE.CANONICAL_MANIFEST_NAME,
)
RUN_CANDIDATE_KEYS = {
    "version",
    "canonicalManifestSha256",
    "inventorySha256",
    "fileCount",
    "totalBytes",
    "bundleIdentitySha256",
}
ACTIVATION_CONTRACT_NAME = "chummer6-ui.windows-only-publication-activation"
PREPARED_TRANSACTION_CONTRACT_NAME = (
    "chummer6-ui.windows-only-publication-transaction-prepared"
)
PREPARED_TRANSACTION_ROLLBACK_CONTRACT_NAME = (
    "chummer6-ui.windows-only-publication-prepared-rollback"
)
ACTIVATION_KEYS = {
    "contractName",
    "contractVersion",
    "fullShelfInventorySha256",
    "generationPath",
    "generationReceiptSha256",
    "incumbentInventorySha256",
    "preparedInventorySha256",
    "proposalSha256",
    "publicationScopeSha256",
    "runUploadPaths",
    "runUploadCandidate",
    "scopeDecisionSha256",
    "status",
    "target",
    "transactionId",
}
JOURNAL_CONTRACT_NAME = "chummer6-ui.windows-only-publication-transaction-journal"
JOURNAL_COMMIT_CONTRACT_NAME = (
    "chummer6-ui.windows-only-publication-transaction-commit"
)
JOURNAL_ROLLBACK_CONTRACT_NAME = (
    "chummer6-ui.windows-only-publication-transaction-rollback"
)
TRANSACTION_CONTRACT_VERSION = 1
ROLLBACK_POLICY = (
    "rollback_all_activated_targets_unless_an_exact_commit_record_binds_the_"
    "journal_and_publication_receipt"
)
GENERATION_KEYS = {
    "ancillaryInventorySha256",
    "contractName",
    "contractVersion",
    "fullShelfInventorySha256",
    "incumbentInventorySha256",
    "preparedInventorySha256",
    "proposalSha256",
    "publicationScopeSha256",
    "runUploadPaths",
    "runUploadCandidate",
    "scopeDecisionSha256",
    "status",
    "target",
}
PREPARED_TARGET_KEYS = {
    "activationReceiptPath",
    "generationPath",
    "generationReceipt",
    "incumbentInventorySha256",
    "index",
    "preparedInventorySha256",
    "target",
}
REGISTRY_PREPARE_FIELD = "registryPrepare"
TRANSACTION_COMMON_KEYS = (
    "fullShelfInventorySha256",
    "proposalSha256",
    "publicationScopeSha256",
    "runUploadPaths",
    "runUploadCandidate",
    "scopeDecisionSha256",
)
PREPARED_RECORD_SUFFIX = ".transaction.prepared.json"
JOURNAL_SUFFIX = ".transaction.json"
COMMIT_SUFFIX = ".transaction.committed.json"
ROLLBACK_SUFFIX = ".transaction.rolled-back.json"
DISCOVERY_LOCK_DIRECTORY = ".chummer-windows-publication.lock"
DISCOVERY_LOCK_FILE = "lease"


class TransactionError(RuntimeError):
    """Raised before or during a fail-closed generation transaction."""


def fail(message: str) -> None:
    raise TransactionError(message)


def canonical_bytes(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def canonical_sha256(value: object) -> str:
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def file_digest_metadata(path: Path) -> tuple[str, os.stat_result]:
    digest = hashlib.sha256()
    descriptor = -1
    total = 0
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY
            | getattr(os, "O_NOFOLLOW", 0)
            | getattr(os, "O_NONBLOCK", 0),
        )
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            fail(f"transaction input is not one non-hardlinked regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            while chunk := handle.read(1024 * 1024):
                digest.update(chunk)
                total += len(chunk)
            after = os.fstat(handle.fileno())
    except OSError as exc:
        fail(f"could not hash transaction input {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    stable_fields = lambda value: (
        value.st_dev,
        value.st_ino,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
        value.st_nlink,
    )
    if stable_fields(before) != stable_fields(after) or total != before.st_size:
        fail(f"transaction input changed while hashed: {path}")
    return digest.hexdigest(), before


def sha256_file(path: Path) -> str:
    return file_digest_metadata(path)[0]


def exact_directory(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.is_symlink() or not path.is_dir():
        fail(f"{label} must be an absolute existing non-symlink directory")
    return path.resolve(strict=True)


def fresh_directory_path(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail(f"{label} must be an absolute fresh path")
    parent = exact_directory(path.parent, f"{label} parent")
    return parent / path.name


def fresh_file_path(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail(f"{label} must be an absolute fresh path")
    parent = exact_directory(path.parent, f"{label} parent")
    return parent / path.name


def paths_overlap(first: Path, second: Path) -> bool:
    return first == second or first in second.parents or second in first.parents


def require_disjoint_paths(paths: tuple[tuple[str, Path], ...]) -> None:
    for index, (first_label, first) in enumerate(paths):
        for second_label, second in paths[index + 1 :]:
            if paths_overlap(first, second):
                fail(
                    f"{first_label} and {second_label} must not be equal or "
                    "ancestor/descendant paths"
                )


def require_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or SHA256_RE.fullmatch(value) is None:
        fail(f"{label} must be a lowercase SHA-256")
    return value


def read_json(path: Path, label: str) -> dict[str, Any]:
    return SCOPE.read_json(path, label)


def write_new_json(path: Path, payload: dict[str, Any]) -> None:
    _write_new_bytes_durable(
        path,
        rendered_json_bytes(payload),
        "transaction JSON receipt",
    )


def rendered_json_bytes(payload: dict[str, Any]) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _owned_regular_bytes(path: Path, label: str, *, max_bytes: int) -> bytes:
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        fail(f"{label} must be an absolute existing regular file")
    metadata = os.stat(path, follow_symlinks=False)
    if (
        not stat.S_ISREG(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_uid != os.geteuid()
        or metadata.st_size > max_bytes
    ):
        fail(f"{label} must be a bounded current-user regular file")
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        current = os.fstat(descriptor)
        if (current.st_dev, current.st_ino) != (metadata.st_dev, metadata.st_ino):
            fail(f"{label} changed while opened")
        data = b""
        while len(data) <= max_bytes:
            chunk = os.read(descriptor, min(1024 * 1024, max_bytes + 1 - len(data)))
            if not chunk:
                break
            data += chunk
        if len(data) > max_bytes:
            fail(f"{label} exceeds its enrolled size limit")
        after = os.fstat(descriptor)
        stable_fields = (
            "st_dev",
            "st_ino",
            "st_mode",
            "st_nlink",
            "st_uid",
            "st_gid",
            "st_size",
            "st_mtime_ns",
            "st_ctime_ns",
        )
        if any(getattr(after, field) != getattr(current, field) for field in stable_fields):
            fail(f"{label} changed while read")
        return data
    finally:
        os.close(descriptor)


def _write_new_bytes_durable(path: Path, data: bytes, label: str) -> None:
    path = fresh_file_path(path, label)
    descriptor = -1
    created = False
    try:
        descriptor = os.open(
            path,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        created = True
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            descriptor = -1
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        fsync_directory(path.parent)
    except BaseException:
        if descriptor >= 0:
            os.close(descriptor)
        if created:
            path.unlink(missing_ok=True)
            fsync_directory(path.parent)
        raise


def _replace_bytes_durable(path: Path, data: bytes, label: str) -> None:
    if not path.is_absolute():
        fail(f"{label} path must be absolute")
    parent = exact_directory(path.parent, f"{label} parent")
    path = parent / path.name
    if path.is_symlink() or (path.exists() and not path.is_file()):
        fail(f"{label} must be a regular file when it exists")
    previous = path.read_bytes() if path.exists() else None
    previous_mode = stat.S_IMODE(path.stat().st_mode) if path.exists() else 0o600

    def install(value: bytes, mode: int) -> None:
        descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=parent)
        temporary = Path(temporary_name)
        try:
            os.fchmod(descriptor, mode)
            with os.fdopen(descriptor, "wb", closefd=True) as handle:
                descriptor = -1
                handle.write(value)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, path)
        finally:
            if descriptor >= 0:
                os.close(descriptor)
            temporary.unlink(missing_ok=True)

    installed = False
    try:
        install(data, 0o600)
        installed = True
        fsync_directory(parent)
    except BaseException as exc:
        if installed:
            try:
                if previous is None:
                    path.unlink(missing_ok=True)
                else:
                    install(previous, previous_mode)
                fsync_directory(parent)
            except BaseException as rollback_exc:
                raise TransactionError(
                    f"{label} update failed and exact rollback also failed"
                ) from rollback_exc
        raise


def _unlink_owned_durable(path: Path, label: str) -> None:
    if not path.exists() and not path.is_symlink():
        return
    _owned_regular_bytes(path, label, max_bytes=1024 * 1024)
    path.unlink()
    fsync_directory(path.parent)


def inventory_tree(root: Path) -> list[dict[str, Any]]:
    root = exact_directory(root, "inventory root")
    rows: list[dict[str, Any]] = []
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        directories.sort()
        files.sort()
        for name in directories:
            path = current_path / name
            metadata = os.stat(path, follow_symlinks=False)
            if not stat.S_ISDIR(metadata.st_mode):
                fail(f"transaction tree contains a non-directory entry: {path}")
            rows.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": path.relative_to(root).as_posix(),
                    "type": "directory",
                }
            )
        for name in files:
            path = current_path / name
            digest, metadata = file_digest_metadata(path)
            rows.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": path.relative_to(root).as_posix(),
                    "sha256": digest,
                    "sizeBytes": metadata.st_size,
                    "type": "file",
                }
            )
    return sorted(rows, key=lambda row: (row["path"], row["type"]))


def _directory_identity(metadata: os.stat_result) -> tuple[int, int]:
    return metadata.st_dev, metadata.st_ino


def _fsync_regular_file(path: Path, label: str) -> None:
    descriptor = os.open(
        path,
        os.O_RDONLY
        | getattr(os, "O_NOFOLLOW", 0)
        | getattr(os, "O_NONBLOCK", 0),
    )
    try:
        before = os.fstat(descriptor)
        entry = os.stat(path, follow_symlinks=False)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_nlink != 1
            or (before.st_dev, before.st_ino) != (entry.st_dev, entry.st_ino)
        ):
            fail(f"{label} changed before its durable file sync")
        os.fsync(descriptor)
        after = os.fstat(descriptor)
        final_entry = os.stat(path, follow_symlinks=False)
        stable_fields = (
            "st_dev",
            "st_ino",
            "st_mode",
            "st_nlink",
            "st_uid",
            "st_gid",
            "st_size",
            "st_mtime_ns",
            "st_ctime_ns",
        )
        if any(getattr(after, field) != getattr(before, field) for field in stable_fields):
            fail(f"{label} changed while its bytes were durably synced")
        if (after.st_dev, after.st_ino) != (final_entry.st_dev, final_entry.st_ino):
            fail(f"{label} was replaced while its bytes were durably synced")
    finally:
        os.close(descriptor)


def fsync_directory(path: Path) -> None:
    descriptor = os.open(
        path,
        os.O_RDONLY
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        before = os.fstat(descriptor)
        entry = os.stat(path, follow_symlinks=False)
        if (
            not stat.S_ISDIR(before.st_mode)
            or _directory_identity(before) != _directory_identity(entry)
        ):
            fail(f"directory changed before durable sync: {path}")
        os.fsync(descriptor)
        after = os.fstat(descriptor)
        final_entry = os.stat(path, follow_symlinks=False)
        if (
            _directory_identity(after) != _directory_identity(before)
            or _directory_identity(final_entry) != _directory_identity(after)
        ):
            fail(f"directory changed while durably synced: {path}")
    finally:
        os.close(descriptor)


def fsync_tree_bottom_up(root: Path) -> None:
    root = exact_directory(root, "durable prepared generation")
    directories: list[Path] = []
    files: list[Path] = []
    for current, child_directories, child_files in os.walk(
        root, topdown=True, followlinks=False
    ):
        current_path = Path(current)
        child_directories.sort()
        child_files.sort()
        directories.append(current_path)
        for name in child_directories:
            metadata = os.stat(current_path / name, follow_symlinks=False)
            if not stat.S_ISDIR(metadata.st_mode):
                fail("prepared generation changed during durable tree traversal")
        for name in child_files:
            path = current_path / name
            metadata = os.stat(path, follow_symlinks=False)
            if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
                fail("prepared generation contains an unsafe file during durable sync")
            files.append(path)
    for path in files:
        _fsync_regular_file(path, "prepared generation file")
    for path in reversed(directories):
        fsync_directory(path)


def _mkdir_child_durable(parent: Path, name: str, mode: int) -> Path:
    if not name or name in {".", ".."} or Path(name).name != name:
        fail("durable transaction directory has an invalid path component")
    parent = exact_directory(parent, "durable transaction directory parent")
    parent_fd = os.open(
        parent,
        os.O_RDONLY
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0),
    )
    child_fd = -1
    try:
        os.mkdir(name, mode=mode, dir_fd=parent_fd)
        child_fd = os.open(
            name,
            os.O_RDONLY
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_fd,
        )
        os.fchmod(child_fd, mode)
        child = os.fstat(child_fd)
        entry = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
        if (
            not stat.S_ISDIR(child.st_mode)
            or _directory_identity(child) != _directory_identity(entry)
        ):
            fail("durable transaction directory changed while created")
        os.fsync(child_fd)
        os.fsync(parent_fd)
    finally:
        if child_fd >= 0:
            os.close(child_fd)
        os.close(parent_fd)
    return exact_directory(parent / name, "durably created transaction directory")


def ensure_directory_durable(args: argparse.Namespace) -> dict[str, Any]:
    requested = Path(args.directory)
    if not requested.is_absolute():
        fail("durable transaction directory must be an absolute path")
    missing: list[str] = []
    cursor = requested
    while not cursor.exists() and not cursor.is_symlink():
        if cursor.parent == cursor:
            fail("durable transaction directory has no existing ancestor")
        missing.append(cursor.name)
        cursor = cursor.parent
    current = exact_directory(cursor, "durable transaction directory ancestor")
    for name in reversed(missing):
        current = _mkdir_child_durable(current, name, 0o700)
    current = exact_directory(requested, "durable transaction directory")
    metadata = os.stat(current, follow_symlinks=False)
    if metadata.st_uid != os.geteuid():
        fail("durable transaction directory is not current-user owned")
    fsync_directory(current)
    if current.parent != current:
        fsync_directory(current.parent)
    return {
        "contractName": "chummer6-ui.windows-only-publication-durable-directory",
        "contractVersion": 1,
        "path": str(current),
        "status": "durable",
    }


def copy_tree_exact(source: Path, destination: Path) -> list[dict[str, Any]]:
    source = exact_directory(source, "incumbent downloads shelf")
    destination = fresh_directory_path(destination, "prepared generation")
    require_disjoint_paths(
        (
            ("incumbent downloads shelf", source),
            ("prepared generation", destination),
        )
    )
    before = inventory_tree(source)
    destination.mkdir(mode=stat.S_IMODE(source.stat().st_mode))
    os.chmod(destination, stat.S_IMODE(source.stat().st_mode), follow_symlinks=False)
    try:
        for row in before:
            target = destination.joinpath(*PurePosixPath(row["path"]).parts)
            if row["type"] == "directory":
                target.mkdir(mode=row["mode"])
                os.chmod(target, row["mode"], follow_symlinks=False)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            SCOPE.copy_regular_exact(
                source.joinpath(*PurePosixPath(row["path"]).parts), target
            )
            os.chmod(target, row["mode"], follow_symlinks=False)
        if inventory_tree(source) != before:
            fail("incumbent downloads shelf changed while copied")
        copied = inventory_tree(destination)
        if copied != before:
            fail("prepared generation did not preserve the incumbent tree exactly")
        return before
    except Exception:
        shutil.rmtree(destination, ignore_errors=True)
        raise


def _file_rows(rows: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {row["path"]: row for row in rows if row["type"] == "file"}


def validate_run_upload_paths(paths: object) -> list[str]:
    if not isinstance(paths, list) or not (3 <= len(paths) <= 4096):
        fail("approved Run upload path set is invalid")
    if paths != sorted(paths) or len(paths) != len(set(paths)):
        fail("approved Run upload paths must be sorted and duplicate-free")
    if set(RUN_UPLOAD_ROOT_FILES) - set(paths):
        fail("approved Run upload paths omit a required manifest")
    portable_casefold: dict[str, str] = {}
    for path in paths:
        try:
            path = SCOPE.portable_relative_path(path, "approved Run upload path")
        except SCOPE.ScopeError as exc:
            fail(str(exc))
        pure = PurePosixPath(path)
        if (
            pure.is_absolute()
            or not pure.parts
            or (
                path not in RUN_UPLOAD_ROOT_FILES
                and (len(pure.parts) != 2 or pure.parts[0] != "files")
            )
        ):
            fail(f"approved Run upload path is outside the sealed boundary: {path}")
        collision_key = path.casefold()
        previous = portable_casefold.get(collision_key)
        if previous is not None:
            fail(
                "approved Run upload paths repeat or case-collide: "
                f"{previous!r} and {path!r}"
            )
        portable_casefold[collision_key] = path
    return paths


def run_upload_candidate(
    rows: list[dict[str, Any]], version: str, approved_paths: object
) -> dict[str, Any]:
    """Reproduce the authoritative Run upload summarizer over frozen rows."""
    file_rows = _file_rows(rows)
    paths = validate_run_upload_paths(approved_paths)
    missing = set(paths) - set(file_rows)
    if missing:
        fail(f"approved Run upload bytes are missing: {sorted(missing)}")
    selected = [file_rows[path] for path in paths]
    canonical = file_rows.get(SCOPE.CANONICAL_MANIFEST_NAME)
    if canonical is None:
        fail("prepared generation is missing the canonical upload manifest")
    if not isinstance(version, str) or not version:
        fail("publication scope release version is invalid")

    inventory_digest = hashlib.sha256()
    total_bytes = 0
    for row in selected:
        encoded_path = row["path"].encode("utf-8")
        inventory_digest.update(len(encoded_path).to_bytes(8, "big"))
        inventory_digest.update(encoded_path)
        inventory_digest.update(row["sizeBytes"].to_bytes(8, "big"))
        inventory_digest.update(bytes.fromhex(row["sha256"]))
        total_bytes += row["sizeBytes"]
    candidate = {
        "version": version,
        "canonicalManifestSha256": canonical["sha256"],
        "inventorySha256": inventory_digest.hexdigest(),
        "fileCount": len(selected),
        "totalBytes": total_bytes,
    }
    candidate["bundleIdentitySha256"] = canonical_sha256(candidate)
    return candidate


def validate_run_upload_candidate(candidate: object) -> dict[str, Any]:
    if not isinstance(candidate, dict) or set(candidate) != RUN_CANDIDATE_KEYS:
        fail("prepared generation Run upload candidate is invalid")
    if not isinstance(candidate.get("version"), str) or not candidate["version"]:
        fail("prepared generation Run upload version is invalid")
    for key in (
        "canonicalManifestSha256",
        "inventorySha256",
        "bundleIdentitySha256",
    ):
        require_sha256(candidate.get(key), f"prepared generation Run {key}")
    file_count = candidate.get("fileCount")
    total_bytes = candidate.get("totalBytes")
    if isinstance(file_count, bool) or not isinstance(file_count, int) or file_count < 1:
        fail("prepared generation Run fileCount is invalid")
    if isinstance(total_bytes, bool) or not isinstance(total_bytes, int) or total_bytes < 0:
        fail("prepared generation Run totalBytes is invalid")
    identity = {key: candidate[key] for key in RUN_CANDIDATE_KEYS - {"bundleIdentitySha256"}}
    if canonical_sha256(identity) != candidate["bundleIdentitySha256"]:
        fail("prepared generation Run bundle identity is invalid")
    return candidate


def validate_generation_payload(generation: object) -> dict[str, Any]:
    expected_keys = set(GENERATION_KEYS)
    if isinstance(generation, dict) and REGISTRY_PREPARE_FIELD in generation:
        expected_keys.add(REGISTRY_PREPARE_FIELD)
    if (
        not isinstance(generation, dict)
        or set(generation) != expected_keys
        or generation.get("contractName") != CONTRACT_NAME
        or generation.get("contractVersion") != CONTRACT_VERSION
        or generation.get("status") != "prepared"
    ):
        fail("prepared generation receipt contract is invalid")
    for key in (
        "ancillaryInventorySha256",
        "fullShelfInventorySha256",
        "incumbentInventorySha256",
        "preparedInventorySha256",
        "proposalSha256",
        "publicationScopeSha256",
        "scopeDecisionSha256",
    ):
        require_sha256(generation.get(key), f"prepared generation {key}")
    validate_run_upload_candidate(generation.get("runUploadCandidate"))
    validate_run_upload_paths(generation.get("runUploadPaths"))
    if REGISTRY_PREPARE_FIELD in generation:
        SCOPE.validate_registry_prepare_binding(generation[REGISTRY_PREPARE_FIELD])
    target = generation.get("target")
    if not isinstance(target, str) or not Path(target).is_absolute():
        fail("prepared generation target must be an absolute path")
    return generation


def _validate_transaction_id(value: object) -> str:
    transaction_id = value if isinstance(value, str) else ""
    if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]{7,127}", transaction_id) is None:
        fail("activation transaction ID is invalid")
    return transaction_id


def _load_exact_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                fail(f"{label} contains duplicate property {key!r}")
            result[key] = value
        return result

    try:
        payload = json.loads(data.decode("utf-8"), object_pairs_hook=reject_duplicates)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is not exact UTF-8 JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must contain a JSON object")
    return payload


def validate_activation_payload(
    payload: object,
    *,
    expected_transaction_id: str,
    verify_target: bool,
) -> dict[str, Any]:
    expected_keys = set(ACTIVATION_KEYS)
    if isinstance(payload, dict) and REGISTRY_PREPARE_FIELD in payload:
        expected_keys.add(REGISTRY_PREPARE_FIELD)
    if (
        not isinstance(payload, dict)
        or set(payload) != expected_keys
        or payload.get("contractName") != ACTIVATION_CONTRACT_NAME
        or payload.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or payload.get("status") != "activated"
    ):
        fail("activation receipt contract is invalid")
    if payload.get("transactionId") != expected_transaction_id:
        fail("activation receipt transaction ID is replayed or mismatched")
    _validate_transaction_id(payload.get("transactionId"))
    for key in (
        "fullShelfInventorySha256",
        "generationReceiptSha256",
        "incumbentInventorySha256",
        "preparedInventorySha256",
        "proposalSha256",
        "publicationScopeSha256",
        "scopeDecisionSha256",
    ):
        require_sha256(payload.get(key), f"activation receipt {key}")
    validate_run_upload_paths(payload.get("runUploadPaths"))
    validate_run_upload_candidate(payload.get("runUploadCandidate"))
    if REGISTRY_PREPARE_FIELD in payload:
        SCOPE.validate_registry_prepare_binding(payload[REGISTRY_PREPARE_FIELD])
    target_text = payload.get("target")
    generation_text = payload.get("generationPath")
    if not isinstance(target_text, str) or not Path(target_text).is_absolute():
        fail("activation receipt target must be an absolute path")
    if not isinstance(generation_text, str) or not Path(generation_text).is_absolute():
        fail("activation receipt generationPath must be an absolute path")
    if verify_target:
        target = exact_directory(Path(target_text), "activation receipt target")
        generation = exact_directory(
            Path(generation_text), "activation receipt predecessor generation"
        )
        if str(target) != target_text:
            fail("activation receipt target is not a canonical physical path")
        if str(generation) != generation_text:
            fail("activation receipt generationPath is not a canonical physical path")
        require_disjoint_paths(
            (("activation receipt target", target), ("activation generation", generation))
        )
        if canonical_sha256(inventory_tree(target)) != payload["preparedInventorySha256"]:
            fail("activation receipt target does not contain its prepared inventory")
        if canonical_sha256(inventory_tree(generation)) != payload["incumbentInventorySha256"]:
            fail("activation receipt generation does not contain its incumbent inventory")
    return payload


def _proof_projection(
    payload: dict[str, Any], path: Path, data: bytes, index: int
) -> dict[str, Any]:
    return {
        "fullShelfInventorySha256": payload["fullShelfInventorySha256"],
        "generationPath": payload["generationPath"],
        "generationReceiptSha256": payload["generationReceiptSha256"],
        "incumbentInventorySha256": payload["incumbentInventorySha256"],
        "index": index,
        "path": str(path),
        "preparedInventorySha256": payload["preparedInventorySha256"],
        "sha256": hashlib.sha256(data).hexdigest(),
        "sizeBytes": len(data),
        "target": payload["target"],
    }


def _journal_output_path(path: Path, label: str) -> Path:
    if not path.is_absolute():
        fail(f"{label} must be an absolute path")
    parent = exact_directory(path.parent, f"{label} parent")
    return parent / path.name


def create_prepared_transaction(args: argparse.Namespace) -> dict[str, Any]:
    transaction_id = _validate_transaction_id(args.transaction_id)
    receipt_paths = list(args.generation_receipt or [])
    targets = list(args.target or [])
    generations = list(args.prepared or [])
    activation_receipts = list(args.activation_receipt or [])
    if not (
        1 <= len(receipt_paths) <= 64
        and len(receipt_paths)
        == len(targets)
        == len(generations)
        == len(activation_receipts)
    ):
        fail("prepared transaction requires matching 1..64 target generations")
    output_path = fresh_file_path(args.output, "prepared transaction record")
    activation_journal = _journal_output_path(
        args.activation_journal, "future activation journal"
    )
    if activation_journal == output_path:
        fail("prepared transaction record and activation journal must differ")

    rows: list[dict[str, Any]] = []
    common: dict[str, Any] | None = None
    seen_targets: set[str] = set()
    disjoint: list[tuple[str, Path]] = [
        ("prepared transaction record", output_path),
        ("future activation journal", activation_journal),
    ]
    for index, (receipt_arg, target_arg, generation_arg, activation_arg) in enumerate(
        zip(
            receipt_paths,
            targets,
            generations,
            activation_receipts,
            strict=True,
        )
    ):
        receipt_path = SCOPE.exact_file(
            receipt_arg, f"prepared generation receipt {index}"
        )
        receipt_bytes = _owned_regular_bytes(
            receipt_path, f"prepared generation receipt {index}", max_bytes=256 * 1024
        )
        generation = validate_generation_payload(
            _load_exact_json_bytes(receipt_bytes, f"prepared generation receipt {index}")
        )
        target = exact_directory(target_arg, f"prepared transaction target {index}")
        prepared = exact_directory(
            generation_arg, f"prepared transaction generation {index}"
        )
        activation_receipt = fresh_file_path(
            activation_arg, f"future activation receipt {index}"
        )
        if str(target) != generation["target"]:
            fail("prepared transaction target differs from its generation receipt")
        if target == prepared or paths_overlap(target, prepared):
            fail("prepared transaction target and generation overlap")
        if receipt_path.parent != prepared.parent or activation_receipt.parent != prepared.parent:
            fail("prepared transaction receipts must stay inside their generation root")
        if str(target) in seen_targets:
            fail("prepared transaction repeats an activation target")
        seen_targets.add(str(target))
        if canonical_sha256(inventory_tree(target)) != generation[
            "incumbentInventorySha256"
        ]:
            fail("prepared transaction target changed before durable enrollment")
        fsync_tree_bottom_up(target)
        fsync_directory(target.parent)
        if target.parent.parent != target.parent:
            fsync_directory(target.parent.parent)
        if canonical_sha256(inventory_tree(target)) != generation[
            "incumbentInventorySha256"
        ]:
            fail("prepared transaction target changed while durably enrolled")
        if canonical_sha256(inventory_tree(prepared)) != generation[
            "preparedInventorySha256"
        ]:
            fail("prepared transaction generation changed before durable enrollment")
        fsync_tree_bottom_up(prepared)
        fsync_directory(prepared.parent)
        if prepared.parent.parent != prepared.parent:
            fsync_directory(prepared.parent.parent)
        _fsync_regular_file(
            receipt_path, f"prepared generation receipt {index}"
        )
        fsync_directory(receipt_path.parent)
        if receipt_path.parent.parent != receipt_path.parent:
            fsync_directory(receipt_path.parent.parent)
        durable_receipt_bytes = _owned_regular_bytes(
            receipt_path,
            f"durably enrolled generation receipt {index}",
            max_bytes=256 * 1024,
        )
        if durable_receipt_bytes != receipt_bytes:
            fail("prepared generation receipt changed while durably enrolled")
        if canonical_sha256(inventory_tree(prepared)) != generation[
            "preparedInventorySha256"
        ]:
            fail("prepared transaction generation changed while durably enrolled")
        current_common = {key: generation[key] for key in TRANSACTION_COMMON_KEYS}
        if REGISTRY_PREPARE_FIELD in generation:
            current_common[REGISTRY_PREPARE_FIELD] = generation[
                REGISTRY_PREPARE_FIELD
            ]
        if common is None:
            common = current_common
        elif current_common != common:
            fail("prepared transaction generations disagree on transaction-wide bindings")
        rows.append(
            {
                "activationReceiptPath": str(activation_receipt),
                "generationPath": str(prepared),
                "generationReceipt": {
                    "path": str(receipt_path),
                    "sha256": hashlib.sha256(receipt_bytes).hexdigest(),
                    "sizeBytes": len(receipt_bytes),
                },
                "incumbentInventorySha256": generation[
                    "incumbentInventorySha256"
                ],
                "index": index,
                "preparedInventorySha256": generation["preparedInventorySha256"],
                "target": str(target),
            }
        )
        disjoint.extend(
            (
                (f"prepared transaction target {index}", target),
                (f"prepared transaction generation {index}", prepared),
                (f"prepared generation receipt {index}", receipt_path),
                (f"future activation receipt {index}", activation_receipt),
            )
        )
    assert common is not None
    require_disjoint_paths(tuple(disjoint))
    if activation_journal.parent != output_path.parent:
        fail("prepared transaction record and activation journal must share a directory")
    fsync_directory(output_path.parent)
    if output_path.parent.parent != output_path.parent:
        fsync_directory(output_path.parent.parent)
    payload = {
        "activationJournalPath": str(activation_journal),
        "contractName": PREPARED_TRANSACTION_CONTRACT_NAME,
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "fullShelfInventorySha256": common["fullShelfInventorySha256"],
        "proposalSha256": common["proposalSha256"],
        "publicationScopeSha256": common["publicationScopeSha256"],
        "rollbackPolicy": ROLLBACK_POLICY,
        "runUploadCandidate": common["runUploadCandidate"],
        "runUploadPaths": common["runUploadPaths"],
        "scopeDecisionSha256": common["scopeDecisionSha256"],
        "status": "prepared",
        "targetCount": len(rows),
        "targets": rows,
        "transactionId": transaction_id,
    }
    if REGISTRY_PREPARE_FIELD in common:
        payload[REGISTRY_PREPARE_FIELD] = common[REGISTRY_PREPARE_FIELD]
    _write_new_bytes_durable(
        output_path, rendered_json_bytes(payload), "prepared transaction record"
    )
    return payload


def load_prepared_transaction(
    path: Path,
) -> tuple[dict[str, Any], bytes]:
    path = SCOPE.exact_file(path, "prepared transaction record")
    data = _owned_regular_bytes(
        path, "prepared transaction record", max_bytes=1024 * 1024
    )
    payload = _load_exact_json_bytes(data, "prepared transaction record")
    expected_keys = {
        "activationJournalPath",
        "contractName",
        "contractVersion",
        "fullShelfInventorySha256",
        "proposalSha256",
        "publicationScopeSha256",
        "rollbackPolicy",
        "runUploadCandidate",
        "runUploadPaths",
        "scopeDecisionSha256",
        "status",
        "targetCount",
        "targets",
        "transactionId",
    }
    if isinstance(payload, dict) and REGISTRY_PREPARE_FIELD in payload:
        expected_keys.add(REGISTRY_PREPARE_FIELD)
    if (
        set(payload) != expected_keys
        or payload.get("contractName") != PREPARED_TRANSACTION_CONTRACT_NAME
        or payload.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or payload.get("status") != "prepared"
        or payload.get("rollbackPolicy") != ROLLBACK_POLICY
    ):
        fail("prepared transaction record contract is invalid")
    _validate_transaction_id(payload.get("transactionId"))
    for key in (
        "fullShelfInventorySha256",
        "proposalSha256",
        "publicationScopeSha256",
        "scopeDecisionSha256",
    ):
        require_sha256(payload.get(key), f"prepared transaction {key}")
    validate_run_upload_paths(payload.get("runUploadPaths"))
    validate_run_upload_candidate(payload.get("runUploadCandidate"))
    if REGISTRY_PREPARE_FIELD in payload:
        SCOPE.validate_registry_prepare_binding(payload[REGISTRY_PREPARE_FIELD])
    activation_journal = _journal_output_path(
        Path(str(payload.get("activationJournalPath", ""))),
        "future activation journal",
    )
    if activation_journal.parent != path.parent or activation_journal == path:
        fail("prepared transaction future journal path is invalid")
    rows = payload.get("targets")
    if (
        not isinstance(rows, list)
        or not (1 <= len(rows) <= 64)
        or payload.get("targetCount") != len(rows)
    ):
        fail("prepared transaction target count is invalid")
    seen_targets: set[str] = set()
    common_keys = list(TRANSACTION_COMMON_KEYS)
    if REGISTRY_PREPARE_FIELD in payload:
        common_keys.append(REGISTRY_PREPARE_FIELD)
    for index, row in enumerate(rows):
        if not isinstance(row, dict) or set(row) != PREPARED_TARGET_KEYS:
            fail("prepared transaction target row is invalid")
        if row.get("index") != index:
            fail("prepared transaction target ordering is invalid")
        target = exact_directory(Path(str(row.get("target", ""))), "prepared target")
        prepared = exact_directory(
            Path(str(row.get("generationPath", ""))), "prepared generation"
        )
        if str(target) != row["target"] or str(prepared) != row["generationPath"]:
            fail("prepared transaction paths are not canonical physical paths")
        if row["target"] in seen_targets:
            fail("prepared transaction repeats a target")
        seen_targets.add(row["target"])
        require_sha256(
            row.get("incumbentInventorySha256"), "prepared target incumbent inventory"
        )
        require_sha256(
            row.get("preparedInventorySha256"), "prepared target generation inventory"
        )
        reference = row.get("generationReceipt")
        if not isinstance(reference, dict) or set(reference) != {
            "path",
            "sha256",
            "sizeBytes",
        }:
            fail("prepared transaction generation receipt reference is invalid")
        receipt_path = SCOPE.exact_file(
            Path(str(reference.get("path", ""))), "prepared generation receipt"
        )
        receipt_data = _owned_regular_bytes(
            receipt_path, "prepared generation receipt", max_bytes=256 * 1024
        )
        if (
            len(receipt_data) != reference.get("sizeBytes")
            or hashlib.sha256(receipt_data).hexdigest() != reference.get("sha256")
        ):
            fail("prepared transaction generation receipt bytes changed")
        generation = validate_generation_payload(
            _load_exact_json_bytes(receipt_data, "prepared generation receipt")
        )
        if generation["target"] != row["target"] or any(
            generation[key] != row[key]
            for key in ("incumbentInventorySha256", "preparedInventorySha256")
        ):
            fail("prepared transaction target differs from its generation receipt")
        for key in common_keys:
            if generation[key] != payload[key]:
                fail(f"prepared transaction generation differs on {key}")
        activation_receipt = Path(str(row.get("activationReceiptPath", "")))
        if (
            not activation_receipt.is_absolute()
            or activation_receipt.parent != prepared.parent
            or receipt_path.parent != prepared.parent
        ):
            fail("prepared transaction receipt path escapes its generation root")
    return payload, data


def recover_prepared_transaction(args: argparse.Namespace) -> dict[str, Any]:
    if args.activation_journal.exists() or args.activation_journal.is_symlink():
        fail("prepared recovery cannot run after an activation journal exists")
    if args.commit.exists() or args.commit.is_symlink():
        fail("prepared recovery cannot roll back a committed transaction")
    prepared, prepared_bytes = load_prepared_transaction(args.prepared_record)
    if str(args.activation_journal) != prepared["activationJournalPath"]:
        fail("prepared recovery activation journal path differs")
    for row in reversed(prepared["targets"]):
        recover_activation(
            argparse.Namespace(
                target=Path(row["target"]),
                prepared=Path(row["generationPath"]),
                incumbent_inventory=row["incumbentInventorySha256"],
                prepared_inventory=row["preparedInventorySha256"],
                activation_receipt=Path(row["activationReceiptPath"]),
            )
        )
    for row in prepared["targets"]:
        if canonical_sha256(
            inventory_tree(exact_directory(Path(row["target"]), "recovered target"))
        ) != row["incumbentInventorySha256"]:
            fail("prepared transaction recovery did not restore an incumbent target")
        if canonical_sha256(
            inventory_tree(
                exact_directory(Path(row["generationPath"]), "recovered generation")
            )
        ) != row["preparedInventorySha256"]:
            fail("prepared transaction recovery changed a prepared generation")
    rollback_path = fresh_file_path(args.rollback, "prepared transaction rollback record")
    rollback = {
        "contractName": PREPARED_TRANSACTION_ROLLBACK_CONTRACT_NAME,
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "preparedTransaction": {
            "path": str(SCOPE.exact_file(args.prepared_record, "prepared transaction record")),
            "sha256": hashlib.sha256(prepared_bytes).hexdigest(),
            "sizeBytes": len(prepared_bytes),
        },
        "status": "rolled_back",
        "transactionId": prepared["transactionId"],
    }
    _write_new_bytes_durable(
        rollback_path,
        rendered_json_bytes(rollback),
        "prepared transaction rollback record",
    )
    return rollback


def validate_prepared_rollback_record(
    rollback_path: Path,
    prepared_path: Path,
    prepared: dict[str, Any],
    prepared_bytes: bytes,
) -> dict[str, Any]:
    rollback_bytes = _owned_regular_bytes(
        rollback_path, "prepared transaction rollback record", max_bytes=256 * 1024
    )
    rollback = _load_exact_json_bytes(
        rollback_bytes, "prepared transaction rollback record"
    )
    expected_reference = {
        "path": str(SCOPE.exact_file(prepared_path, "prepared transaction record")),
        "sha256": hashlib.sha256(prepared_bytes).hexdigest(),
        "sizeBytes": len(prepared_bytes),
    }
    if (
        set(rollback)
        != {
            "contractName",
            "contractVersion",
            "preparedTransaction",
            "status",
            "transactionId",
        }
        or rollback.get("contractName")
        != PREPARED_TRANSACTION_ROLLBACK_CONTRACT_NAME
        or rollback.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or rollback.get("preparedTransaction") != expected_reference
        or rollback.get("status") != "rolled_back"
        or rollback.get("transactionId") != prepared["transactionId"]
    ):
        fail("prepared transaction rollback record contract is invalid")
    for row in prepared["targets"]:
        target = exact_directory(
            Path(row["target"]), "prepared rollback incumbent target"
        )
        if canonical_sha256(inventory_tree(target)) != row[
            "incumbentInventorySha256"
        ]:
            fail("prepared rollback record does not match incumbent target state")
    return rollback


def _discovered_paths(prepared_path: Path) -> dict[str, Path]:
    if not prepared_path.name.endswith(PREPARED_RECORD_SUFFIX):
        fail("discovered prepared transaction has an invalid file name")
    prefix = prepared_path.name[: -len(PREPARED_RECORD_SUFFIX)]
    if not prefix or Path(prefix).name != prefix:
        fail("discovered prepared transaction has an invalid prefix")
    return {
        "prepared": prepared_path,
        "journal": prepared_path.parent / f"{prefix}{JOURNAL_SUFFIX}",
        "commit": prepared_path.parent / f"{prefix}{COMMIT_SUFFIX}",
        "rollback": prepared_path.parent / f"{prefix}{ROLLBACK_SUFFIX}",
    }


def _regular_marker(path: Path, label: str) -> bool:
    if not path.exists() and not path.is_symlink():
        return False
    _owned_regular_bytes(path, label, max_bytes=1024 * 1024)
    return True


def _prepared_header(path: Path) -> tuple[dict[str, Any], bytes]:
    data = _owned_regular_bytes(
        path, "discovered prepared transaction", max_bytes=1024 * 1024
    )
    payload = _load_exact_json_bytes(data, "discovered prepared transaction")
    rows = payload.get("targets")
    if (
        payload.get("contractName") != PREPARED_TRANSACTION_CONTRACT_NAME
        or payload.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or payload.get("status") != "prepared"
        or not isinstance(rows, list)
        or not (1 <= len(rows) <= 64)
        or payload.get("targetCount") != len(rows)
    ):
        fail("discovered prepared transaction contract is invalid")
    _validate_transaction_id(payload.get("transactionId"))
    for index, row in enumerate(rows):
        if (
            not isinstance(row, dict)
            or set(row) != PREPARED_TARGET_KEYS
            or row.get("index") != index
        ):
            fail("discovered prepared transaction target row is invalid")
        for key in ("target", "generationPath"):
            value = Path(str(row.get(key, "")))
            if not value.is_absolute():
                fail(f"discovered prepared transaction {key} is not absolute")
    return payload, data


def _validate_terminal_header(
    path: Path,
    *,
    prepared_transaction_id: str,
    expected_contracts: set[str],
    label: str,
) -> None:
    data = _owned_regular_bytes(path, label, max_bytes=1024 * 1024)
    payload = _load_exact_json_bytes(data, label)
    if (
        payload.get("contractName") not in expected_contracts
        or payload.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or payload.get("status") not in {"committed", "rolled_back"}
        or payload.get("transactionId") != prepared_transaction_id
    ):
        fail(f"{label} contract is invalid")


def _prepared_inventory_state(prepared: dict[str, Any]) -> str:
    states: list[str] = []
    for row in prepared["targets"]:
        target = exact_directory(Path(row["target"]), "discovered prepared target")
        generation = exact_directory(
            Path(row["generationPath"]), "discovered prepared generation"
        )
        pair = (
            canonical_sha256(inventory_tree(target)),
            canonical_sha256(inventory_tree(generation)),
        )
        if pair == (
            row["incumbentInventorySha256"],
            row["preparedInventorySha256"],
        ):
            states.append("prepared")
        elif pair == (
            row["preparedInventorySha256"],
            row["incumbentInventorySha256"],
        ):
            states.append("activated")
        else:
            fail(
                "discovered prepared transaction is ambiguous: target/generation "
                "state is unrecognized; preserve it for manual reconciliation"
            )
    if all(state == "prepared" for state in states):
        return "prepared"
    if all(state == "activated" for state in states):
        return "activated"
    return "partially_activated"


def _acquire_discovery_lock(lock_dir: Path) -> int:
    try:
        lock_dir.mkdir(mode=0o700)
        fsync_directory(lock_dir.parent)
    except FileExistsError:
        pass
    lock_dir = exact_directory(lock_dir, "Windows-only publication lock directory")
    metadata = os.stat(lock_dir, follow_symlinks=False)
    if metadata.st_uid != os.geteuid():
        fail("Windows-only publication lock directory is not current-user owned")
    lease = lock_dir / DISCOVERY_LOCK_FILE
    descriptor = os.open(
        lease,
        os.O_RDWR
        | os.O_CREAT
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    try:
        os.fchmod(descriptor, 0o600)
        lease_metadata = os.fstat(descriptor)
        if (
            not stat.S_ISREG(lease_metadata.st_mode)
            or lease_metadata.st_nlink != 1
            or lease_metadata.st_uid != os.geteuid()
        ):
            fail("Windows-only publication lock lease is not an owned regular file")
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            fail(
                "an active Windows-only publication process still holds the durable "
                f"lock lease: {lease}"
            )
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def recover_discovered_transactions(args: argparse.Namespace) -> dict[str, Any]:
    receipt_dir = exact_directory(
        args.receipt_dir, "Windows-only transaction receipt directory"
    )
    discovered: list[dict[str, Any]] = []
    for entry in sorted(os.scandir(receipt_dir), key=lambda item: item.name):
        if not entry.name.endswith(PREPARED_RECORD_SUFFIX):
            continue
        prepared_path = receipt_dir / entry.name
        paths = _discovered_paths(prepared_path)
        header, header_bytes = _prepared_header(prepared_path)
        commit_exists = _regular_marker(
            paths["commit"], "discovered transaction commit marker"
        )
        rollback_exists = _regular_marker(
            paths["rollback"], "discovered transaction rollback marker"
        )
        if commit_exists and rollback_exists:
            fail(
                "discovered transaction has both commit and rollback markers; "
                "preserve it for manual reconciliation"
            )
        generation_exists = [
            Path(row["generationPath"]).is_dir()
            and not Path(row["generationPath"]).is_symlink()
            for row in header["targets"]
        ]
        if (commit_exists or rollback_exists) and not any(generation_exists):
            marker = paths["commit"] if commit_exists else paths["rollback"]
            _validate_terminal_header(
                marker,
                prepared_transaction_id=header["transactionId"],
                expected_contracts=(
                    {JOURNAL_COMMIT_CONTRACT_NAME}
                    if commit_exists
                    else {
                        JOURNAL_ROLLBACK_CONTRACT_NAME,
                        PREPARED_TRANSACTION_ROLLBACK_CONTRACT_NAME,
                    }
                ),
                label="historical transaction terminal marker",
            )
            continue
        if not all(generation_exists):
            fail(
                "discovered transaction has a partial generation set; preserve it "
                "for manual reconciliation"
            )
        prepared, prepared_bytes = load_prepared_transaction(prepared_path)
        if prepared_bytes != header_bytes:
            fail("discovered prepared transaction changed during recovery discovery")
        discovered.append(
            {
                "paths": paths,
                "prepared": prepared,
                "preparedBytes": prepared_bytes,
            }
        )
    if not discovered:
        return {"reconciled": [], "status": "clean"}

    target_owners: dict[str, str] = {}
    lock_owners: dict[Path, str] = {}
    lock_paths: set[Path] = set()
    for item in discovered:
        transaction_id = item["prepared"]["transactionId"]
        transaction_lock_paths: set[Path] = set()
        for row in item["prepared"]["targets"]:
            target = Path(row["target"])
            previous = target_owners.setdefault(str(target), transaction_id)
            if previous != transaction_id:
                fail(
                    "discovered prepared transactions overlap one target; preserve "
                    "them for manual reconciliation"
                )
            lock_path = target.parent / DISCOVERY_LOCK_DIRECTORY
            transaction_lock_paths.add(lock_path)
            previous_lock = lock_owners.setdefault(lock_path, transaction_id)
            if previous_lock != transaction_id:
                fail(
                    "discovered prepared transactions share one durable lock; preserve "
                    "them for manual reconciliation"
                )
        lock_paths.update(transaction_lock_paths)

    descriptors: list[int] = []
    try:
        for lock_path in sorted(lock_paths, key=str):
            descriptors.append(_acquire_discovery_lock(lock_path))

        plans: list[tuple[dict[str, Any], str]] = []
        allowed_journal_states = {
            "activated",
            "committed",
            "partially_rolled_back",
            "rolled_back",
            "rolled_back_pending_marker",
        }
        for item in discovered:
            paths = item["paths"]
            commit_exists = _regular_marker(
                paths["commit"], "discovered transaction commit marker"
            )
            rollback_exists = _regular_marker(
                paths["rollback"], "discovered transaction rollback marker"
            )
            if commit_exists and rollback_exists:
                fail(
                    "discovered transaction became both committed and rolled back; "
                    "preserve it for manual reconciliation"
                )
            journal_exists = _regular_marker(
                paths["journal"], "discovered activation journal"
            )
            if journal_exists:
                state = transaction_status(
                    argparse.Namespace(
                        journal=paths["journal"],
                        commit=paths["commit"],
                        rollback=paths["rollback"],
                    )
                )["status"]
                if state not in allowed_journal_states:
                    fail(f"discovered transaction has unsupported state: {state}")
            else:
                if commit_exists:
                    fail(
                        "discovered transaction has a commit without its activation "
                        "journal; preserve it for manual reconciliation"
                    )
                if rollback_exists:
                    validate_prepared_rollback_record(
                        paths["rollback"],
                        paths["prepared"],
                        item["prepared"],
                        item["preparedBytes"],
                    )
                    state = "rolled_back"
                else:
                    state = _prepared_inventory_state(item["prepared"])
            plans.append((item, state))

        reconciled: list[dict[str, str]] = []
        for item, state in plans:
            paths = item["paths"]
            if _regular_marker(paths["journal"], "discovered activation journal"):
                if state == "committed":
                    install_current_receipt(
                        argparse.Namespace(
                            journal=paths["journal"], commit=paths["commit"]
                        )
                    )
                elif state != "rolled_back":
                    discard_uncommitted_receipt(
                        argparse.Namespace(
                            journal=paths["journal"], commit=paths["commit"]
                        )
                    )
                    resume_transaction_rollback(
                        argparse.Namespace(
                            journal=paths["journal"], commit=paths["commit"]
                        )
                    )
                    mark_transaction_rolled_back(
                        argparse.Namespace(
                            journal=paths["journal"],
                            commit=paths["commit"],
                            rollback=paths["rollback"],
                        )
                    )
                    state = "rolled_back"
            elif state != "rolled_back":
                recover_prepared_transaction(
                    argparse.Namespace(
                        prepared_record=paths["prepared"],
                        activation_journal=paths["journal"],
                        commit=paths["commit"],
                        rollback=paths["rollback"],
                    )
                )
                state = "rolled_back"
            reconciled.append(
                {
                    "status": state,
                    "transactionId": item["prepared"]["transactionId"],
                }
            )
        return {"reconciled": reconciled, "status": "reconciled"}
    finally:
        for descriptor in reversed(descriptors):
            try:
                fcntl.flock(descriptor, fcntl.LOCK_UN)
            finally:
                os.close(descriptor)


def create_activation_journal(args: argparse.Namespace) -> dict[str, Any]:
    transaction_id = _validate_transaction_id(args.transaction_id)
    receipt_paths = list(args.activation_receipt or [])
    if not (1 <= len(receipt_paths) <= 64):
        fail("transaction journal requires 1..64 activation receipts")
    journal_path = fresh_file_path(args.journal, "activation transaction journal")
    proof_dir = fresh_directory_path(args.proof_dir, "durable activation proof directory")
    publication_receipt = _journal_output_path(
        args.publication_receipt, "transaction publication receipt"
    )
    current_receipt = _journal_output_path(
        args.current_receipt, "transaction current receipt"
    )
    if publication_receipt == current_receipt:
        fail("transaction publication and current receipt paths must differ")

    loaded: list[tuple[Path, bytes, dict[str, Any]]] = []
    targets: set[str] = set()
    sources: set[Path] = set()
    for raw_path in receipt_paths:
        source = SCOPE.exact_file(raw_path, "ephemeral activation receipt")
        if source in sources:
            fail("transaction journal activation receipt is duplicated")
        sources.add(source)
        data = _owned_regular_bytes(
            source, "ephemeral activation receipt", max_bytes=256 * 1024
        )
        payload = validate_activation_payload(
            _load_exact_json_bytes(data, "ephemeral activation receipt"),
            expected_transaction_id=transaction_id,
            verify_target=True,
        )
        if payload["target"] in targets:
            fail("transaction journal activation target is duplicated")
        targets.add(payload["target"])
        loaded.append((source, data, payload))

    first = loaded[0][2]
    common_keys = list(TRANSACTION_COMMON_KEYS)
    if REGISTRY_PREPARE_FIELD in first:
        common_keys.append(REGISTRY_PREPARE_FIELD)
    for _source, _data, payload in loaded[1:]:
        if (REGISTRY_PREPARE_FIELD in payload) != (
            REGISTRY_PREPARE_FIELD in first
        ):
            fail("activation receipts disagree on Registry PREPARE presence")
        for key in common_keys:
            if payload[key] != first[key]:
                fail(f"activation receipts disagree on transaction-wide {key}")

    prepared_record_arg = getattr(args, "prepared_record", None)
    if prepared_record_arg is not None:
        prepared, _prepared_bytes = load_prepared_transaction(prepared_record_arg)
        if (
            prepared["transactionId"] != transaction_id
            or prepared["activationJournalPath"] != str(journal_path)
            or prepared["targetCount"] != len(loaded)
        ):
            fail("activation receipts differ from the durable prepared transaction")
        for index, ((_source, _data, activation), enrolled) in enumerate(
            zip(loaded, prepared["targets"], strict=True)
        ):
            expected = {
                "activationReceiptPath": str(loaded[index][0]),
                "generationPath": activation["generationPath"],
                "generationReceiptSha256": activation["generationReceiptSha256"],
                "incumbentInventorySha256": activation["incumbentInventorySha256"],
                "preparedInventorySha256": activation["preparedInventorySha256"],
                "target": activation["target"],
            }
            actual = {
                "activationReceiptPath": enrolled["activationReceiptPath"],
                "generationPath": enrolled["generationPath"],
                "generationReceiptSha256": enrolled["generationReceipt"]["sha256"],
                "incumbentInventorySha256": enrolled["incumbentInventorySha256"],
                "preparedInventorySha256": enrolled["preparedInventorySha256"],
                "target": enrolled["target"],
            }
            if expected != actual or enrolled["index"] != index:
                fail("activation receipt is not enrolled by the prepared transaction")
        for key in common_keys:
            if prepared[key] != first[key]:
                fail(f"prepared transaction differs from activation receipts on {key}")

    disjoint: list[tuple[str, Path]] = [
        ("activation transaction journal", journal_path),
        ("durable activation proof directory", proof_dir),
        ("transaction publication receipt", publication_receipt),
        ("transaction current receipt", current_receipt),
    ]
    for index, (_source, _data, payload) in enumerate(loaded):
        disjoint.extend(
            (
                (f"activation target {index}", Path(payload["target"])),
                (
                    f"activation predecessor generation {index}",
                    Path(payload["generationPath"]),
                ),
            )
        )
    require_disjoint_paths(tuple(disjoint))

    proof_dir.mkdir(mode=0o700)
    os.chmod(proof_dir, 0o700, follow_symlinks=False)
    fsync_directory(proof_dir.parent)
    try:
        projections: list[dict[str, Any]] = []
        for index, (_source, data, payload) in enumerate(loaded):
            destination = proof_dir / f"{index:04d}.activation.json"
            _write_new_bytes_durable(
                destination, data, "durable activation proof"
            )
            projections.append(_proof_projection(payload, destination, data, index))
        journal = {
            "activationProofs": projections,
            "contractName": JOURNAL_CONTRACT_NAME,
            "contractVersion": TRANSACTION_CONTRACT_VERSION,
            "currentReceiptPath": str(current_receipt),
            "fullShelfInventorySha256": first["fullShelfInventorySha256"],
            "proofDirectory": str(proof_dir),
            "proposalSha256": first["proposalSha256"],
            "publicationReceiptPath": str(publication_receipt),
            "publicationScopeSha256": first["publicationScopeSha256"],
            "rollbackPolicy": ROLLBACK_POLICY,
            "runUploadPaths": first["runUploadPaths"],
            "runUploadCandidate": first["runUploadCandidate"],
            "scopeDecisionSha256": first["scopeDecisionSha256"],
            "status": "activated",
            "targetCount": len(projections),
            "transactionId": transaction_id,
        }
        if REGISTRY_PREPARE_FIELD in first:
            journal[REGISTRY_PREPARE_FIELD] = first[REGISTRY_PREPARE_FIELD]
        _write_new_bytes_durable(
            journal_path,
            rendered_json_bytes(journal),
            "activation transaction journal",
        )
        return journal
    except BaseException:
        if journal_path.exists() or journal_path.is_symlink():
            _unlink_owned_durable(journal_path, "partial activation transaction journal")
        shutil.rmtree(proof_dir, ignore_errors=True)
        fsync_directory(proof_dir.parent)
        raise


def activation_binding_from_journal(
    journal_path: Path, *, verify_targets: bool = True
) -> dict[str, Any]:
    journal_path = SCOPE.exact_file(journal_path, "activation transaction journal")
    journal_bytes = _owned_regular_bytes(
        journal_path, "activation transaction journal", max_bytes=1024 * 1024
    )
    journal = _load_exact_json_bytes(journal_bytes, "activation transaction journal")
    expected_keys = {
        "activationProofs",
        "contractName",
        "contractVersion",
        "currentReceiptPath",
        "fullShelfInventorySha256",
        "proofDirectory",
        "proposalSha256",
        "publicationReceiptPath",
        "publicationScopeSha256",
        "rollbackPolicy",
        "runUploadPaths",
        "runUploadCandidate",
        "scopeDecisionSha256",
        "status",
        "targetCount",
        "transactionId",
    }
    if isinstance(journal, dict) and REGISTRY_PREPARE_FIELD in journal:
        expected_keys.add(REGISTRY_PREPARE_FIELD)
    if (
        set(journal) != expected_keys
        or journal.get("contractName") != JOURNAL_CONTRACT_NAME
        or journal.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or journal.get("status") != "activated"
        or journal.get("rollbackPolicy") != ROLLBACK_POLICY
    ):
        fail("activation transaction journal contract is invalid")
    transaction_id = _validate_transaction_id(journal.get("transactionId"))
    for key in (
        "fullShelfInventorySha256",
        "proposalSha256",
        "publicationScopeSha256",
        "scopeDecisionSha256",
    ):
        require_sha256(journal.get(key), f"activation transaction journal {key}")
    validate_run_upload_paths(journal.get("runUploadPaths"))
    validate_run_upload_candidate(journal.get("runUploadCandidate"))
    if REGISTRY_PREPARE_FIELD in journal:
        SCOPE.validate_registry_prepare_binding(journal[REGISTRY_PREPARE_FIELD])
    proofs = journal.get("activationProofs")
    if (
        not isinstance(proofs, list)
        or not (1 <= len(proofs) <= 64)
        or journal.get("targetCount") != len(proofs)
    ):
        fail("activation transaction journal proof count is invalid")
    proof_dir = exact_directory(
        Path(str(journal.get("proofDirectory", ""))),
        "durable activation proof directory",
    )
    if proof_dir.parent != journal_path.parent:
        fail("durable activation proofs must share the journal directory")
    targets: set[str] = set()
    validated: list[dict[str, Any]] = []
    expected_proof_keys = {
        "fullShelfInventorySha256",
        "generationPath",
        "generationReceiptSha256",
        "incumbentInventorySha256",
        "index",
        "path",
        "preparedInventorySha256",
        "sha256",
        "sizeBytes",
        "target",
    }
    for index, projection in enumerate(proofs):
        if not isinstance(projection, dict) or set(projection) != expected_proof_keys:
            fail("activation transaction journal proof row is invalid")
        if projection.get("index") != index:
            fail("activation transaction journal proof order is invalid")
        proof_path = Path(str(projection.get("path", "")))
        if proof_path.parent != proof_dir or proof_path.name != f"{index:04d}.activation.json":
            fail("activation transaction journal proof path is invalid")
        proof_bytes = _owned_regular_bytes(
            proof_path, "durable activation proof", max_bytes=256 * 1024
        )
        if (
            len(proof_bytes) != projection.get("sizeBytes")
            or hashlib.sha256(proof_bytes).hexdigest() != projection.get("sha256")
        ):
            fail("durable activation proof bytes changed")
        payload = validate_activation_payload(
            _load_exact_json_bytes(proof_bytes, "durable activation proof"),
            expected_transaction_id=transaction_id,
            verify_target=verify_targets,
        )
        if _proof_projection(payload, proof_path, proof_bytes, index) != projection:
            fail("activation transaction journal proof projection changed")
        if (REGISTRY_PREPARE_FIELD in payload) != (
            REGISTRY_PREPARE_FIELD in journal
        ):
            fail("activation proof differs from journal Registry PREPARE presence")
        proof_common_keys = list(TRANSACTION_COMMON_KEYS)
        if REGISTRY_PREPARE_FIELD in journal:
            proof_common_keys.append(REGISTRY_PREPARE_FIELD)
        for key in proof_common_keys:
            if payload[key] != journal[key]:
                fail(f"activation proof differs from journal {key}")
        if payload["target"] in targets:
            fail("activation transaction journal target is replayed")
        targets.add(payload["target"])
        validated.append(dict(projection))
    for key in ("publicationReceiptPath", "currentReceiptPath"):
        output = _journal_output_path(Path(str(journal.get(key, ""))), key)
        if key == "publicationReceiptPath" and output.parent != journal_path.parent:
            fail(f"activation transaction journal {key} must share its directory")
    if journal["publicationReceiptPath"] == journal["currentReceiptPath"]:
        fail("activation transaction journal receipt paths overlap")
    binding = {
        "activationProofs": validated,
        "contractName": "chummer6-ui.windows-only-publication-activation-binding",
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "fullShelfInventorySha256": journal["fullShelfInventorySha256"],
        "journal": {
            "path": str(journal_path),
            "sha256": hashlib.sha256(journal_bytes).hexdigest(),
            "sizeBytes": len(journal_bytes),
        },
        "rollbackPolicy": ROLLBACK_POLICY,
        "proposalSha256": journal["proposalSha256"],
        "publicationScopeSha256": journal["publicationScopeSha256"],
        "runUploadPaths": journal["runUploadPaths"],
        "runUploadCandidate": journal["runUploadCandidate"],
        "scopeDecisionSha256": journal["scopeDecisionSha256"],
        "transactionId": transaction_id,
    }
    if REGISTRY_PREPARE_FIELD in journal:
        binding[REGISTRY_PREPARE_FIELD] = journal[REGISTRY_PREPARE_FIELD]
    return binding


def activation_inventory_state(binding: dict[str, Any]) -> str:
    states: list[str] = []
    for proof in binding["activationProofs"]:
        target = exact_directory(Path(proof["target"]), "journal activation target")
        generation = exact_directory(
            Path(proof["generationPath"]), "journal predecessor generation"
        )
        target_sha = canonical_sha256(inventory_tree(target))
        generation_sha = canonical_sha256(inventory_tree(generation))
        pair = (target_sha, generation_sha)
        if pair == (
            proof["preparedInventorySha256"],
            proof["incumbentInventorySha256"],
        ):
            states.append("activated")
        elif pair == (
            proof["incumbentInventorySha256"],
            proof["preparedInventorySha256"],
        ):
            states.append("rolled_back")
        else:
            fail("transaction journal found an unrecognized target/generation state")
    if all(state == "activated" for state in states):
        return "activated"
    if all(state == "rolled_back" for state in states):
        return "rolled_back_pending_marker"
    return "partially_rolled_back"


def _require_committed_targets(binding: dict[str, Any]) -> None:
    for proof in binding["activationProofs"]:
        target = exact_directory(Path(proof["target"]), "committed activation target")
        if canonical_sha256(inventory_tree(target)) != proof["preparedInventorySha256"]:
            fail("committed activation target does not contain its prepared inventory")


def _commit_record(
    commit_path: Path, journal_path: Path, *, verify_targets: bool
) -> tuple[dict[str, Any], dict[str, Any], bytes]:
    binding = activation_binding_from_journal(journal_path, verify_targets=False)
    if verify_targets:
        _require_committed_targets(binding)
    journal = read_json(journal_path, "activation transaction journal")
    receipt_path = Path(journal["publicationReceiptPath"])
    receipt_bytes = _owned_regular_bytes(
        receipt_path, "committed publication receipt", max_bytes=1024 * 1024
    )
    receipt = _load_exact_json_bytes(receipt_bytes, "committed publication receipt")
    commit_path = SCOPE.exact_file(commit_path, "transaction commit record")
    commit_bytes = _owned_regular_bytes(
        commit_path, "transaction commit record", max_bytes=1024 * 1024
    )
    commit = _load_exact_json_bytes(commit_bytes, "transaction commit record")
    expected_keys = {
        "activation",
        "contractName",
        "contractVersion",
        "publicationReceipt",
        "rollbackPolicy",
        "status",
        "transactionId",
    }
    expected_receipt_ref = {
        "path": str(receipt_path),
        "sha256": hashlib.sha256(receipt_bytes).hexdigest(),
        "sizeBytes": len(receipt_bytes),
    }
    if (
        set(commit) != expected_keys
        or commit.get("contractName") != JOURNAL_COMMIT_CONTRACT_NAME
        or commit.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or commit.get("status") != "committed"
        or commit.get("transactionId") != binding["transactionId"]
        or commit.get("activation") != binding
        or commit.get("publicationReceipt") != expected_receipt_ref
        or commit.get("rollbackPolicy")
        != "committed_targets_must_never_be_rolled_back;repair_current_from_bound_receipt"
    ):
        fail("transaction commit record contract is invalid")
    if (
        receipt.get("windowsOnlyActivation") != binding
        or receipt.get("status") != "passed"
        or receipt.get("transactionCommitRequired") is not True
        or receipt.get("transactionCommitState") != "awaiting_exact_commit_record"
    ):
        fail("committed publication receipt does not bind this activation journal")
    return commit, journal, receipt_bytes


def commit_transaction(args: argparse.Namespace) -> dict[str, Any]:
    journal_path = SCOPE.exact_file(args.journal, "activation transaction journal")
    commit_path = fresh_file_path(args.commit, "transaction commit record")
    binding = activation_binding_from_journal(journal_path, verify_targets=True)
    journal = read_json(journal_path, "activation transaction journal")
    receipt_path = SCOPE.exact_file(
        Path(journal["publicationReceiptPath"]), "publication receipt awaiting commit"
    )
    receipt_bytes = _owned_regular_bytes(
        receipt_path, "publication receipt awaiting commit", max_bytes=1024 * 1024
    )
    receipt = _load_exact_json_bytes(receipt_bytes, "publication receipt awaiting commit")
    if (
        receipt.get("status") != "passed"
        or receipt.get("windowsOnlyActivation") != binding
        or receipt.get("transactionCommitRequired") is not True
        or receipt.get("transactionCommitState") != "awaiting_exact_commit_record"
    ):
        fail("publication receipt does not bind the exact activated transaction")
    commit = {
        "activation": binding,
        "contractName": JOURNAL_COMMIT_CONTRACT_NAME,
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "publicationReceipt": {
            "path": str(receipt_path),
            "sha256": hashlib.sha256(receipt_bytes).hexdigest(),
            "sizeBytes": len(receipt_bytes),
        },
        "rollbackPolicy": (
            "committed_targets_must_never_be_rolled_back;"
            "repair_current_from_bound_receipt"
        ),
        "status": "committed",
        "transactionId": binding["transactionId"],
    }
    _write_new_bytes_durable(
        commit_path, rendered_json_bytes(commit), "transaction commit record"
    )
    return commit


def install_current_receipt(args: argparse.Namespace) -> dict[str, Any]:
    commit, journal, receipt_bytes = _commit_record(
        args.commit, args.journal, verify_targets=True
    )
    commit_path = SCOPE.exact_file(args.commit, "transaction commit record")
    commit_bytes = _owned_regular_bytes(
        commit_path, "transaction commit record", max_bytes=1024 * 1024
    )
    current = Path(journal["currentReceiptPath"])
    pointer = {
        "contractName": "chummer6-ui.windows-only-publication-current-pointer",
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "commitRecord": {
            "path": str(commit_path),
            "sha256": hashlib.sha256(commit_bytes).hexdigest(),
            "sizeBytes": len(commit_bytes),
        },
        "publicationReceipt": commit["publicationReceipt"],
        "status": "committed",
        "transactionId": commit["transactionId"],
    }
    if pointer["publicationReceipt"]["sha256"] != hashlib.sha256(receipt_bytes).hexdigest():
        fail("current pointer publication receipt binding changed")
    _replace_bytes_durable(
        current, rendered_json_bytes(pointer), "current publication receipt pointer"
    )
    return pointer


def transaction_status(args: argparse.Namespace) -> dict[str, Any]:
    journal_path = SCOPE.exact_file(args.journal, "activation transaction journal")
    commit_path = args.commit
    if commit_path.exists() or commit_path.is_symlink():
        commit, _journal, _receipt = _commit_record(
            commit_path, journal_path, verify_targets=True
        )
        return {"status": "committed", "transactionId": commit["transactionId"]}
    rollback_path = getattr(args, "rollback", None)
    if rollback_path is not None and (
        rollback_path.exists() or rollback_path.is_symlink()
    ):
        rollback = validate_rollback_record(rollback_path, journal_path)
        return {"status": "rolled_back", "transactionId": rollback["transactionId"]}
    binding = activation_binding_from_journal(journal_path, verify_targets=False)
    return {
        "status": activation_inventory_state(binding),
        "transactionId": binding["transactionId"],
    }


def discard_uncommitted_receipt(args: argparse.Namespace) -> dict[str, Any]:
    if args.commit.exists() or args.commit.is_symlink():
        fail("cannot discard a publication receipt after a commit record exists")
    binding = activation_binding_from_journal(args.journal, verify_targets=False)
    activation_inventory_state(binding)
    journal = read_json(args.journal, "activation transaction journal")
    receipt_path = Path(journal["publicationReceiptPath"])
    if receipt_path.exists() or receipt_path.is_symlink():
        receipt_bytes = _owned_regular_bytes(
            receipt_path, "uncommitted publication receipt", max_bytes=1024 * 1024
        )
        receipt = _load_exact_json_bytes(receipt_bytes, "uncommitted publication receipt")
        if receipt.get("windowsOnlyActivation") != binding:
            fail("uncommitted publication receipt does not bind this transaction")
        _unlink_owned_durable(receipt_path, "uncommitted publication receipt")
    return {"status": "discarded", "transactionId": binding["transactionId"]}


def resume_transaction_rollback(args: argparse.Namespace) -> dict[str, Any]:
    if args.commit.exists() or args.commit.is_symlink():
        fail("cannot roll back a transaction after a commit record exists")
    binding = activation_binding_from_journal(args.journal, verify_targets=False)
    activation_inventory_state(binding)
    for proof in reversed(binding["activationProofs"]):
        target = exact_directory(Path(proof["target"]), "rollback activation target")
        generation = exact_directory(
            Path(proof["generationPath"]), "rollback predecessor generation"
        )
        target_sha = canonical_sha256(inventory_tree(target))
        generation_sha = canonical_sha256(inventory_tree(generation))
        if (target_sha, generation_sha) == (
            proof["incumbentInventorySha256"],
            proof["preparedInventorySha256"],
        ):
            continue
        if (target_sha, generation_sha) != (
            proof["preparedInventorySha256"],
            proof["incumbentInventorySha256"],
        ):
            fail("rollback resumption found an unrecognized target/generation state")
        exchange(
            argparse.Namespace(
                left=target,
                right=generation,
                expected_left_inventory=proof["preparedInventorySha256"],
                expected_right_inventory=proof["incumbentInventorySha256"],
            )
        )
    if activation_inventory_state(binding) != "rolled_back_pending_marker":
        fail("rollback resumption did not restore every incumbent shelf")
    return {"status": "rolled_back", "transactionId": binding["transactionId"]}


def mark_transaction_rolled_back(args: argparse.Namespace) -> dict[str, Any]:
    if args.commit.exists() or args.commit.is_symlink():
        fail("cannot mark a committed transaction as rolled back")
    binding = activation_binding_from_journal(args.journal, verify_targets=False)
    journal = read_json(args.journal, "activation transaction journal")
    if Path(journal["publicationReceiptPath"]).exists():
        fail("cannot finalize rollback while an uncommitted publication receipt exists")
    if activation_inventory_state(binding) != "rolled_back_pending_marker":
        fail("cannot mark a transaction before every target is rolled back")
    if args.rollback.exists() or args.rollback.is_symlink():
        return validate_rollback_record(args.rollback, args.journal)
    rollback_path = fresh_file_path(args.rollback, "transaction rollback record")
    payload = {
        "activation": binding,
        "contractName": JOURNAL_ROLLBACK_CONTRACT_NAME,
        "contractVersion": TRANSACTION_CONTRACT_VERSION,
        "rollbackPolicy": ROLLBACK_POLICY,
        "status": "rolled_back",
        "transactionId": binding["transactionId"],
    }
    _write_new_bytes_durable(
        rollback_path, rendered_json_bytes(payload), "transaction rollback record"
    )
    return payload


def validate_rollback_record(
    rollback_path: Path, journal_path: Path
) -> dict[str, Any]:
    binding = activation_binding_from_journal(journal_path, verify_targets=False)
    rollback_path = SCOPE.exact_file(rollback_path, "transaction rollback record")
    rollback_bytes = _owned_regular_bytes(
        rollback_path, "transaction rollback record", max_bytes=1024 * 1024
    )
    rollback = _load_exact_json_bytes(
        rollback_bytes, "transaction rollback record"
    )
    if (
        set(rollback)
        != {
            "activation",
            "contractName",
            "contractVersion",
            "rollbackPolicy",
            "status",
            "transactionId",
        }
        or rollback.get("activation") != binding
        or rollback.get("contractName") != JOURNAL_ROLLBACK_CONTRACT_NAME
        or rollback.get("contractVersion") != TRANSACTION_CONTRACT_VERSION
        or rollback.get("rollbackPolicy") != ROLLBACK_POLICY
        or rollback.get("status") != "rolled_back"
        or rollback.get("transactionId") != binding["transactionId"]
    ):
        fail("transaction rollback record contract is invalid")
    for proof in binding["activationProofs"]:
        target = exact_directory(Path(proof["target"]), "rolled-back activation target")
        if canonical_sha256(inventory_tree(target)) != proof["incumbentInventorySha256"]:
            fail("transaction rollback record does not match rolled-back target state")
    return rollback


def validate_incumbent(
    incumbent: Path, scope_payload: dict[str, Any]
) -> tuple[list[dict[str, Any]], set[str]]:
    incumbent = exact_directory(incumbent, "incumbent downloads shelf")
    snapshot = scope_payload["incumbentSnapshot"]
    canonical = incumbent / SCOPE.CANONICAL_MANIFEST_NAME
    compatibility = incumbent / SCOPE.COMPATIBILITY_MANIFEST_NAME
    if sha256_file(canonical) != snapshot["canonicalManifestSha256"]:
        fail("live incumbent canonical manifest differs from the approved snapshot")
    if sha256_file(compatibility) != snapshot["compatibilityManifestSha256"]:
        fail("live incumbent compatibility manifest differs from the approved snapshot")
    expected_inventory = SCOPE.validate_inventory(
        snapshot.get("inventory"), "approved incumbent full-shelf inventory"
    )
    if SCOPE.file_inventory(incumbent) != expected_inventory:
        fail("live incumbent full shelf differs from the approved bytes or modes")
    expected_names = {
        Path(path).name
        for path in snapshot["managedPaths"]
        if path.startswith("files/") and DESKTOP_ARTIFACT_RE.fullmatch(Path(path).name)
    }
    actual_desktop = {
        path.name
        for path in (incumbent / "files").iterdir()
        if path.is_file()
        and not path.is_symlink()
        and DESKTOP_ARTIFACT_RE.fullmatch(path.name)
    }
    if actual_desktop != expected_names:
        fail("live incumbent has missing or unexplained desktop artifacts")
    return inventory_tree(incumbent), expected_names


def verify_prepared_generation(
    prepared: Path,
    source_publication: Path,
    scope_payload: dict[str, Any],
    ancillary_rows: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    prepared = exact_directory(prepared, "prepared downloads generation")
    source_publication = exact_directory(source_publication, "publication source shelf")
    for name, expected_sha in (
        (SCOPE.CANONICAL_MANIFEST_NAME, scope_payload["fullShelfManifestSha256"]),
        (
            SCOPE.COMPATIBILITY_MANIFEST_NAME,
            scope_payload["fullShelfCompatibilityManifestSha256"],
        ),
    ):
        path = prepared / name
        if path.is_symlink() or not path.is_file() or sha256_file(path) != expected_sha:
            fail(f"prepared downloads generation changed {name}")
        if sha256_file(source_publication / name) != expected_sha:
            fail(f"publication source changed {name}")
    expected_artifacts = {
        row["fileName"]: (row["sha256"], row["sizeBytes"])
        for row in scope_payload["postPublicationShelfTuples"]
    }
    actual_desktop: set[str] = set()
    for path in (prepared / "files").iterdir():
        if path.is_symlink():
            fail("prepared downloads files contain a symlink")
        if path.is_file() and DESKTOP_ARTIFACT_RE.fullmatch(path.name):
            actual_desktop.add(path.name)
    if actual_desktop != set(expected_artifacts):
        fail("prepared downloads generation has missing or unexplained desktop artifacts")
    for name, (digest, size) in expected_artifacts.items():
        path = prepared / "files" / name
        if (
            not path.is_file()
            or path.is_symlink()
            or path.stat().st_size != size
            or sha256_file(path) != digest
        ):
            fail(f"prepared downloads generation changed artifact bytes: {name}")
    current_rows = inventory_tree(prepared)
    current_files = _file_rows(current_rows)
    for row in ancillary_rows:
        if current_files.get(row["path"]) != row:
            fail(f"prepared generation changed incumbent ancillary bytes: {row['path']}")
    approved_full_inventory = SCOPE.validate_inventory(
        scope_payload.get("fullShelfInventory"), "approved full shelf inventory"
    )
    if SCOPE.file_inventory(prepared) != approved_full_inventory:
        fail("prepared generation differs from the approved full shelf bytes or modes")
    return current_rows


def prepare_generation(args: argparse.Namespace) -> dict[str, Any]:
    scope_path = SCOPE.exact_file(args.scope, "final publication scope")
    proposal_path = SCOPE.exact_file(args.proposal, "publication scope proposal")
    evidence_root = exact_directory(args.evidence_root, "sealed publication evidence root")
    source_publication = exact_directory(args.publication_dir, "publication source shelf")
    incumbent = exact_directory(args.incumbent, "incumbent downloads shelf")
    prepared = fresh_directory_path(args.output_dir, "prepared generation")
    receipt_path = fresh_file_path(args.receipt, "generation receipt")
    for path, label in (
        (scope_path, "final publication scope"),
        (proposal_path, "publication scope proposal"),
        (source_publication, "publication source shelf"),
    ):
        try:
            relative = path.relative_to(evidence_root)
        except ValueError:
            fail(f"{label} must be contained by the sealed evidence root")
        if not relative.parts:
            fail(f"{label} must be strictly below the sealed evidence root")
    require_disjoint_paths(
        (
            ("sealed evidence root", evidence_root),
            ("incumbent downloads shelf", incumbent),
            ("prepared generation", prepared),
            ("generation receipt", receipt_path),
        )
    )
    scope_payload = SCOPE.verify_scope(
        argparse.Namespace(
            scope=scope_path,
            proposal=proposal_path,
            publication_dir=source_publication,
            evidence_root=evidence_root,
        )
    )
    incumbent_rows, incumbent_artifact_names = validate_incumbent(
        incumbent, scope_payload
    )
    managed_paths = {
        SCOPE.CANONICAL_MANIFEST_NAME,
        SCOPE.COMPATIBILITY_MANIFEST_NAME,
        *(f"files/{name}" for name in incumbent_artifact_names),
    }
    ancillary_rows = [
        row
        for row in incumbent_rows
        if row["type"] == "file" and row["path"] not in managed_paths
    ]
    copied_rows = copy_tree_exact(incumbent, prepared)
    try:
        for relative in managed_paths:
            if relative in {
                SCOPE.CANONICAL_MANIFEST_NAME,
                SCOPE.COMPATIBILITY_MANIFEST_NAME,
            }:
                continue
            prepared.joinpath(*PurePosixPath(relative).parts).unlink()
        for name in (
            SCOPE.CANONICAL_MANIFEST_NAME,
            SCOPE.COMPATIBILITY_MANIFEST_NAME,
        ):
            target = prepared / name
            target.unlink()
            SCOPE.copy_regular_exact(source_publication / name, target)
        for source in sorted((source_publication / "files").iterdir()):
            if source.is_symlink() or not source.is_file():
                fail("publication source files contain a non-regular entry")
            SCOPE.copy_regular_exact(source, prepared / "files" / source.name)
        prepared_rows = verify_prepared_generation(
            prepared, source_publication, scope_payload, ancillary_rows
        )
        fsync_tree_bottom_up(prepared)
        fsync_directory(prepared.parent)
        if prepared.parent.parent != prepared.parent:
            fsync_directory(prepared.parent.parent)
        if inventory_tree(prepared) != prepared_rows:
            fail("prepared generation changed while its final tree was durably synced")
        run_upload_paths = sorted(
            {
                *RUN_UPLOAD_ROOT_FILES,
                *(
                    f"files/{row['fileName']}"
                    for row in scope_payload["postPublicationShelfTuples"]
                ),
            }
        )
        receipt = {
            "ancillaryInventorySha256": canonical_sha256(ancillary_rows),
            "contractName": CONTRACT_NAME,
            "contractVersion": CONTRACT_VERSION,
            "fullShelfInventorySha256": scope_payload["fullShelfInventorySha256"],
            "incumbentInventorySha256": canonical_sha256(copied_rows),
            "preparedInventorySha256": canonical_sha256(prepared_rows),
            "proposalSha256": sha256_file(proposal_path),
            "publicationScopeSha256": sha256_file(scope_path),
            "runUploadCandidate": run_upload_candidate(
                inventory_tree(source_publication),
                scope_payload["release"]["version"],
                run_upload_paths,
            ),
            "runUploadPaths": run_upload_paths,
            "scopeDecisionSha256": scope_payload["scopeDecisionSha256"],
            "status": "prepared",
            "target": str(incumbent),
        }
        if scope_payload.get(REGISTRY_PREPARE_FIELD) is not None:
            receipt[REGISTRY_PREPARE_FIELD] = scope_payload[
                REGISTRY_PREPARE_FIELD
            ]
        write_new_json(receipt_path, receipt)
        return receipt
    except Exception:
        shutil.rmtree(prepared, ignore_errors=True)
        receipt_path.unlink(missing_ok=True)
        raise


def _open_exchange_endpoint(path: Path, label: str) -> dict[str, Any]:
    parent_fd = -1
    child_fd = -1
    try:
        parent_fd = os.open(
            path.parent,
            os.O_RDONLY
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_NOFOLLOW", 0),
        )
        parent_metadata = os.fstat(parent_fd)
        child_fd = os.open(
            path.name,
            os.O_RDONLY
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_fd,
        )
        child_metadata = os.fstat(child_fd)
        entry_metadata = os.stat(path.name, dir_fd=parent_fd, follow_symlinks=False)
    except OSError as exc:
        if child_fd >= 0:
            os.close(child_fd)
        if parent_fd >= 0:
            os.close(parent_fd)
        fail(f"could not hold {label} by directory descriptor: {exc}")
    if (
        not stat.S_ISDIR(parent_metadata.st_mode)
        or not stat.S_ISDIR(child_metadata.st_mode)
        or _directory_identity(entry_metadata) != _directory_identity(child_metadata)
    ):
        os.close(child_fd)
        os.close(parent_fd)
        fail(f"{label} changed while its directory descriptors were acquired")
    return {
        "childFd": child_fd,
        "childIdentity": _directory_identity(child_metadata),
        "label": label,
        "name": path.name,
        "parentFd": parent_fd,
        "parentIdentity": _directory_identity(parent_metadata),
        "path": path,
    }


def _close_exchange_endpoint(endpoint: dict[str, Any]) -> None:
    os.close(endpoint["childFd"])
    os.close(endpoint["parentFd"])


def _validate_exchange_endpoint(
    endpoint: dict[str, Any], expected_entry_identity: tuple[int, int]
) -> None:
    parent = os.fstat(endpoint["parentFd"])
    held_child = os.fstat(endpoint["childFd"])
    entry = os.stat(
        endpoint["name"],
        dir_fd=endpoint["parentFd"],
        follow_symlinks=False,
    )
    if (
        _directory_identity(parent) != endpoint["parentIdentity"]
        or _directory_identity(held_child) != endpoint["childIdentity"]
        or not stat.S_ISDIR(entry.st_mode)
        or _directory_identity(entry) != expected_entry_identity
    ):
        fail(f"{endpoint['label']} changed or was aliased during atomic exchange")


def _rename_exchange_raw(
    left: dict[str, Any],
    right: dict[str, Any],
    *,
    expected_left_identity: tuple[int, int],
    expected_right_identity: tuple[int, int],
) -> None:
    _validate_exchange_endpoint(left, expected_left_identity)
    _validate_exchange_endpoint(right, expected_right_identity)
    if left["parentIdentity"][0] != right["parentIdentity"][0]:
        fail("atomic generation exchange requires one filesystem")
    libc = ctypes.CDLL(None, use_errno=True)
    function = getattr(libc, "renameat2", None)
    if function is None:
        fail("atomic renameat2(RENAME_EXCHANGE) is unavailable")
    function.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    function.restype = ctypes.c_int
    result = function(
        left["parentFd"],
        os.fsencode(left["name"]),
        right["parentFd"],
        os.fsencode(right["name"]),
        RENAME_EXCHANGE,
    )
    if result != 0:
        error = ctypes.get_errno()
        fail(f"atomic generation exchange failed: {os.strerror(error)}")


def _fsync_exchange_endpoint_parents(
    left: dict[str, Any], right: dict[str, Any]
) -> None:
    os.fsync(left["parentFd"])
    if right["parentIdentity"] != left["parentIdentity"]:
        os.fsync(right["parentFd"])


def _fsync_exchange_parents(left: Path, right: Path) -> None:
    fsync_directory(left.parent)
    if right.parent != left.parent:
        fsync_directory(right.parent)


def exchange(args: argparse.Namespace) -> dict[str, Any]:
    left = exact_directory(args.left, "exchange left generation")
    right = exact_directory(args.right, "exchange right generation")
    require_disjoint_paths(
        (
            ("exchange left generation", left),
            ("exchange right generation", right),
        )
    )
    expected_left = require_sha256(args.expected_left_inventory, "expected left inventory")
    expected_right = require_sha256(args.expected_right_inventory, "expected right inventory")
    left_endpoint = _open_exchange_endpoint(left, "exchange left generation")
    try:
        right_endpoint = _open_exchange_endpoint(right, "exchange right generation")
    except BaseException:
        _close_exchange_endpoint(left_endpoint)
        raise
    left_identity = left_endpoint["childIdentity"]
    right_identity = right_endpoint["childIdentity"]
    if left_identity == right_identity:
        _close_exchange_endpoint(right_endpoint)
        _close_exchange_endpoint(left_endpoint)
        fail("atomic generation exchange endpoints alias one directory")
    exchanged = False
    try:
        _validate_exchange_endpoint(left_endpoint, left_identity)
        _validate_exchange_endpoint(right_endpoint, right_identity)
        before_left = inventory_tree(left)
        before_right = inventory_tree(right)
        _validate_exchange_endpoint(left_endpoint, left_identity)
        _validate_exchange_endpoint(right_endpoint, right_identity)
        if canonical_sha256(before_left) != expected_left:
            fail("exchange left generation changed before activation")
        if canonical_sha256(before_right) != expected_right:
            fail("exchange right generation changed before activation")
        _rename_exchange_raw(
            left_endpoint,
            right_endpoint,
            expected_left_identity=left_identity,
            expected_right_identity=right_identity,
        )
        exchanged = True
        _fsync_exchange_endpoint_parents(left_endpoint, right_endpoint)
        _validate_exchange_endpoint(left_endpoint, right_identity)
        _validate_exchange_endpoint(right_endpoint, left_identity)
        _fsync_exchange_parents(left, right)
        if canonical_sha256(inventory_tree(left)) != expected_right:
            fail("exchange left generation does not contain the complete activated shelf")
        if canonical_sha256(inventory_tree(right)) != expected_left:
            fail("exchange right generation does not contain the exact prior shelf")
        _validate_exchange_endpoint(left_endpoint, right_identity)
        _validate_exchange_endpoint(right_endpoint, left_identity)
    except BaseException as exc:
        if exchanged:
            try:
                _rename_exchange_raw(
                    left_endpoint,
                    right_endpoint,
                    expected_left_identity=right_identity,
                    expected_right_identity=left_identity,
                )
                _fsync_exchange_endpoint_parents(left_endpoint, right_endpoint)
                _validate_exchange_endpoint(left_endpoint, left_identity)
                _validate_exchange_endpoint(right_endpoint, right_identity)
                _fsync_exchange_parents(left, right)
                if canonical_sha256(inventory_tree(left)) != expected_left:
                    fail("exchange rollback did not restore the exact left generation")
                if canonical_sha256(inventory_tree(right)) != expected_right:
                    fail("exchange rollback did not restore the exact right generation")
            except BaseException as rollback_exc:
                raise TransactionError(
                    f"atomic exchange failed and exact rollback also failed: {rollback_exc}"
                ) from exc
        raise
    finally:
        _close_exchange_endpoint(right_endpoint)
        _close_exchange_endpoint(left_endpoint)
    return {
        "contractName": "chummer6-ui.windows-only-publication-exchange",
        "contractVersion": 1,
        "leftInventorySha256": expected_right,
        "rightInventorySha256": expected_left,
        "status": "exchanged",
    }


def activate(args: argparse.Namespace) -> dict[str, Any]:
    generation_receipt_path = SCOPE.exact_file(
        args.generation_receipt, "prepared generation receipt"
    )
    generation_receipt_bytes = _owned_regular_bytes(
        generation_receipt_path,
        "prepared generation receipt",
        max_bytes=256 * 1024,
    )
    generation = validate_generation_payload(
        _load_exact_json_bytes(
            generation_receipt_bytes, "prepared generation receipt"
        )
    )
    run_upload_paths = validate_run_upload_paths(generation.get("runUploadPaths"))
    left = exact_directory(args.target, "activation target")
    right = exact_directory(args.prepared, "prepared activation generation")
    if generation.get("target") != str(left):
        fail("prepared generation target differs from activation target")
    prepared_manifest_bytes = _owned_regular_bytes(
        right / SCOPE.CANONICAL_MANIFEST_NAME,
        "prepared generation canonical manifest",
        max_bytes=16 * 1024 * 1024,
    )
    prepared_manifest = _load_exact_json_bytes(
        prepared_manifest_bytes, "prepared generation canonical manifest"
    )
    prepared_version, _ = SCOPE.manifest_identity(
        prepared_manifest, "prepared generation canonical manifest"
    )
    if run_upload_candidate(
        inventory_tree(right), prepared_version, run_upload_paths
    ) != generation["runUploadCandidate"]:
        fail("prepared generation Run upload candidate changed before activation")
    transaction_id = _validate_transaction_id(args.transaction_id)
    receipt_path = fresh_file_path(args.receipt, "activation receipt")
    require_disjoint_paths(
        (
            ("activation target", left),
            ("prepared activation generation", right),
            ("prepared generation receipt", generation_receipt_path),
            ("activation receipt", receipt_path),
        )
    )
    exchange_args = argparse.Namespace(
        left=left,
        right=right,
        expected_left_inventory=generation["incumbentInventorySha256"],
        expected_right_inventory=generation["preparedInventorySha256"],
    )
    result = exchange(exchange_args)
    try:
        payload = {
            "contractName": ACTIVATION_CONTRACT_NAME,
            "contractVersion": TRANSACTION_CONTRACT_VERSION,
            "fullShelfInventorySha256": generation["fullShelfInventorySha256"],
            "generationPath": str(right),
            "generationReceiptSha256": hashlib.sha256(
                generation_receipt_bytes
            ).hexdigest(),
            "incumbentInventorySha256": generation["incumbentInventorySha256"],
            "preparedInventorySha256": generation["preparedInventorySha256"],
            "proposalSha256": generation["proposalSha256"],
            "publicationScopeSha256": generation["publicationScopeSha256"],
            "runUploadPaths": run_upload_paths,
            "runUploadCandidate": generation["runUploadCandidate"],
            "scopeDecisionSha256": generation["scopeDecisionSha256"],
            "status": "activated",
            "target": str(left),
            "transactionId": transaction_id,
        }
        if REGISTRY_PREPARE_FIELD in generation:
            payload[REGISTRY_PREPARE_FIELD] = generation[REGISTRY_PREPARE_FIELD]
        write_new_json(receipt_path, payload)
        return payload
    except BaseException:
        exchange(
            argparse.Namespace(
                left=left,
                right=right,
                expected_left_inventory=generation["preparedInventorySha256"],
                expected_right_inventory=generation["incumbentInventorySha256"],
            )
        )
        raise


def recover_activation(args: argparse.Namespace) -> dict[str, Any]:
    target = exact_directory(args.target, "activation recovery target")
    prepared = exact_directory(args.prepared, "activation recovery generation")
    require_disjoint_paths(
        (
            ("activation recovery target", target),
            ("activation recovery generation", prepared),
        )
    )
    incumbent_sha = require_sha256(
        args.incumbent_inventory, "activation recovery incumbent inventory"
    )
    prepared_sha = require_sha256(
        args.prepared_inventory, "activation recovery prepared inventory"
    )
    target_sha = canonical_sha256(inventory_tree(target))
    generation_sha = canonical_sha256(inventory_tree(prepared))
    if (target_sha, generation_sha) == (incumbent_sha, prepared_sha):
        status = "unchanged"
    elif (target_sha, generation_sha) == (prepared_sha, incumbent_sha):
        exchange(
            argparse.Namespace(
                left=target,
                right=prepared,
                expected_left_inventory=prepared_sha,
                expected_right_inventory=incumbent_sha,
            )
        )
        status = "rolled_back"
    else:
        fail("activation recovery found an unrecognized target/generation state")

    receipt_path = Path(args.activation_receipt)
    if (
        not receipt_path.is_absolute()
        or receipt_path.parent.resolve(strict=True) != prepared.parent
    ):
        fail("activation recovery receipt must be inside the transaction root")
    if receipt_path.exists() or receipt_path.is_symlink():
        metadata = os.stat(receipt_path, follow_symlinks=False)
        if (
            not stat.S_ISREG(metadata.st_mode)
            or metadata.st_nlink != 1
            or metadata.st_uid != os.geteuid()
        ):
            fail("activation recovery receipt is not an owned regular transaction file")
        receipt_path.unlink()
        fsync_directory(receipt_path.parent)
    if canonical_sha256(inventory_tree(target)) != incumbent_sha:
        fail("activation recovery did not restore the exact incumbent target")
    if canonical_sha256(inventory_tree(prepared)) != prepared_sha:
        fail("activation recovery did not restore the exact prepared generation")
    return {
        "contractName": "chummer6-ui.windows-only-publication-activation-recovery",
        "contractVersion": 1,
        "status": status,
        "targetInventorySha256": incumbent_sha,
        "generationInventorySha256": prepared_sha,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    durable_directory = commands.add_parser("ensure-directory")
    durable_directory.add_argument("--directory", type=Path, required=True)
    durable_directory.set_defaults(handler=ensure_directory_durable)
    prepare = commands.add_parser("prepare")
    prepare.add_argument("--scope", type=Path, required=True)
    prepare.add_argument("--proposal", type=Path, required=True)
    prepare.add_argument("--evidence-root", type=Path, required=True)
    prepare.add_argument("--publication-dir", type=Path, required=True)
    prepare.add_argument("--incumbent", type=Path, required=True)
    prepare.add_argument("--output-dir", type=Path, required=True)
    prepare.add_argument("--receipt", type=Path, required=True)
    prepare.set_defaults(handler=prepare_generation)
    swap = commands.add_parser("exchange")
    swap.add_argument("--left", type=Path, required=True)
    swap.add_argument("--right", type=Path, required=True)
    swap.add_argument("--expected-left-inventory", required=True)
    swap.add_argument("--expected-right-inventory", required=True)
    swap.set_defaults(handler=exchange)
    activation = commands.add_parser("activate")
    activation.add_argument("--target", type=Path, required=True)
    activation.add_argument("--prepared", type=Path, required=True)
    activation.add_argument("--generation-receipt", type=Path, required=True)
    activation.add_argument("--transaction-id", required=True)
    activation.add_argument("--receipt", type=Path, required=True)
    activation.set_defaults(handler=activate)
    recovery = commands.add_parser("recover-activation")
    recovery.add_argument("--target", type=Path, required=True)
    recovery.add_argument("--prepared", type=Path, required=True)
    recovery.add_argument("--incumbent-inventory", required=True)
    recovery.add_argument("--prepared-inventory", required=True)
    recovery.add_argument("--activation-receipt", type=Path, required=True)
    recovery.set_defaults(handler=recover_activation)
    prepared_transaction = commands.add_parser("prepare-transaction")
    prepared_transaction.add_argument("--transaction-id", required=True)
    prepared_transaction.add_argument(
        "--generation-receipt", type=Path, action="append", required=True
    )
    prepared_transaction.add_argument(
        "--target", type=Path, action="append", required=True
    )
    prepared_transaction.add_argument(
        "--prepared", type=Path, action="append", required=True
    )
    prepared_transaction.add_argument(
        "--activation-receipt", type=Path, action="append", required=True
    )
    prepared_transaction.add_argument("--activation-journal", type=Path, required=True)
    prepared_transaction.add_argument("--output", type=Path, required=True)
    prepared_transaction.set_defaults(handler=create_prepared_transaction)
    prepared_recovery = commands.add_parser("recover-prepared")
    prepared_recovery.add_argument("--prepared-record", type=Path, required=True)
    prepared_recovery.add_argument("--activation-journal", type=Path, required=True)
    prepared_recovery.add_argument("--commit", type=Path, required=True)
    prepared_recovery.add_argument("--rollback", type=Path, required=True)
    prepared_recovery.set_defaults(handler=recover_prepared_transaction)
    discovered_recovery = commands.add_parser("recover-discovered")
    discovered_recovery.add_argument("--receipt-dir", type=Path, required=True)
    discovered_recovery.set_defaults(handler=recover_discovered_transactions)
    journal = commands.add_parser("journal-activate")
    journal.add_argument("--transaction-id", required=True)
    journal.add_argument(
        "--activation-receipt", type=Path, action="append", required=True
    )
    journal.add_argument("--journal", type=Path, required=True)
    journal.add_argument("--proof-dir", type=Path, required=True)
    journal.add_argument("--publication-receipt", type=Path, required=True)
    journal.add_argument("--current-receipt", type=Path, required=True)
    journal.add_argument("--prepared-record", type=Path)
    journal.set_defaults(handler=create_activation_journal)
    commit = commands.add_parser("journal-commit")
    commit.add_argument("--journal", type=Path, required=True)
    commit.add_argument("--commit", type=Path, required=True)
    commit.set_defaults(handler=commit_transaction)
    current = commands.add_parser("install-current")
    current.add_argument("--journal", type=Path, required=True)
    current.add_argument("--commit", type=Path, required=True)
    current.set_defaults(handler=install_current_receipt)
    status = commands.add_parser("transaction-status")
    status.add_argument("--journal", type=Path, required=True)
    status.add_argument("--commit", type=Path, required=True)
    status.add_argument("--rollback", type=Path)
    status.set_defaults(handler=transaction_status)
    discard = commands.add_parser("discard-uncommitted")
    discard.add_argument("--journal", type=Path, required=True)
    discard.add_argument("--commit", type=Path, required=True)
    discard.set_defaults(handler=discard_uncommitted_receipt)
    resume = commands.add_parser("resume-rollback")
    resume.add_argument("--journal", type=Path, required=True)
    resume.add_argument("--commit", type=Path, required=True)
    resume.set_defaults(handler=resume_transaction_rollback)
    rollback = commands.add_parser("mark-rolled-back")
    rollback.add_argument("--journal", type=Path, required=True)
    rollback.add_argument("--commit", type=Path, required=True)
    rollback.add_argument("--rollback", type=Path, required=True)
    rollback.set_defaults(handler=mark_transaction_rolled_back)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        payload = args.handler(args)
    except (OSError, SCOPE.ScopeError, TransactionError) as exc:
        print(f"windows-only-publication-transaction:error: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(payload, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
