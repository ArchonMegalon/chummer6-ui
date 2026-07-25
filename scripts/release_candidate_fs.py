#!/usr/bin/env python3
"""Fail-closed filesystem staging and cutover for desktop release candidates."""

from __future__ import annotations

import ctypes
import errno
import fcntl
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import uuid
from pathlib import Path
from typing import NoReturn


CHUNK_SIZE = 1024 * 1024
CANONICAL_PLATFORM_FLOOR = ("linux", "windows", "macos")
DIGEST_RE = re.compile(r"^[0-9a-f]{64}$")
INSTALLER_SUFFIXES = (
    "-installer.deb",
    "-installer.exe",
    "-installer.msix",
    "-installer.dmg",
    "-installer.pkg",
)


def die(message: str, *, prefix: str = "") -> NoReturn:
    print(f"{prefix}{message}", file=sys.stderr)
    raise SystemExit(1)


def reject_lexical_symlinks(path: Path, *, message_prefix: str = "") -> None:
    path = path.absolute()
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            break
        except OSError as exc:
            die(f"Unable to inspect publication path component: {current} ({exc})", prefix=message_prefix)
        if stat.S_ISLNK(metadata.st_mode):
            die(f"Publication path cannot contain symlink components: {current}", prefix=message_prefix)


def command_reject_symlinks(arguments: list[str]) -> None:
    for raw in arguments:
        reject_lexical_symlinks(Path(raw))


def command_resolve_output(arguments: list[str]) -> None:
    raw_output, bundle_raw, deploy_raw, portal_raw = arguments

    def fail(message: str) -> NoReturn:
        die(message, prefix="Stage-only release candidate output is unsafe: ")

    if not raw_output.strip():
        fail("CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR is required")
    if "\n" in raw_output or "\r" in raw_output or "\x00" in raw_output:
        fail("output path contains control characters")

    output = Path(raw_output).absolute()
    bundle = Path(bundle_raw).absolute()
    deploy = Path(deploy_raw).absolute()
    portal = Path(portal_raw).absolute()
    if output == Path(output.anchor):
        fail("filesystem root cannot be used as the output directory")

    current = Path(output.anchor)
    for part in output.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            break
        except OSError as exc:
            fail(f"cannot inspect {current} ({exc})")
        if stat.S_ISLNK(metadata.st_mode):
            fail(f"path contains a symlink component: {current}")

    try:
        output.lstat()
    except FileNotFoundError:
        pass
    except OSError as exc:
        fail(f"cannot inspect requested output {output} ({exc})")
    else:
        fail(f"requested output already exists: {output}")

    parent = output.parent
    try:
        parent_metadata = parent.lstat()
    except OSError as exc:
        fail(f"output parent is unavailable: {parent} ({exc})")
    if stat.S_ISLNK(parent_metadata.st_mode) or not stat.S_ISDIR(parent_metadata.st_mode):
        fail(f"output parent is not a regular directory: {parent}")

    for label, protected_root in (
        ("input bundle", bundle),
        ("configured deploy directory", deploy),
        ("configured portal directory", portal),
    ):
        try:
            output.relative_to(protected_root)
        except ValueError:
            continue
        fail(f"output cannot be inside the {label}: {output}")

    print(output)


def command_rewrite_stage_paths(arguments: list[str]) -> None:
    candidate = Path(arguments[0]).absolute()
    final = Path(arguments[1]).absolute()
    source_token = os.fsencode(str(candidate))
    target_token = os.fsencode(str(final))
    text_suffixes = {".json", ".md", ".log", ".txt"}

    for current_root, directory_names, file_names in os.walk(candidate, topdown=True, followlinks=False):
        current = Path(current_root)
        for name in directory_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                die(f"Stage-only candidate contains an unsafe directory: {path}")
        for name in file_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                die(f"Stage-only candidate contains an unsafe file: {path}")
            if path.suffix.lower() not in text_suffixes:
                continue
            payload = path.read_bytes()
            if source_token not in payload:
                continue
            rewritten = payload.replace(source_token, target_token)
            temporary = path.with_name(f".{path.name}.stage-path-rewrite-{os.getpid()}")
            with temporary.open("xb") as stream:
                stream.write(rewritten)
                stream.flush()
                os.fsync(stream.fileno())
            os.chmod(temporary, stat.S_IMODE(metadata.st_mode))
            os.replace(temporary, path)


def command_persist_handoff(arguments: list[str]) -> None:
    candidate = Path(arguments[0]).absolute()
    bundle = Path(arguments[1]).absolute()
    source_token = os.fsencode(str(candidate))
    target_token = os.fsencode(str(bundle))
    for file_name in (
        "RELEASE_BUILD_HANDOFF.generated.json",
        "RELEASE_BUILD_HANDOFF.generated.md",
        "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
        "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md",
    ):
        source = candidate / file_name
        destination = bundle / file_name
        if not source.exists():
            continue
        source_metadata = source.lstat()
        if stat.S_ISLNK(source_metadata.st_mode) or not stat.S_ISREG(source_metadata.st_mode):
            die(f"Refusing to persist unsafe Windows visual proof handoff source: {source}")
        if destination.is_symlink() or (destination.exists() and not destination.is_file()):
            die(
                "Refusing to persist Windows visual proof handoff through an unsafe "
                f"bundle path: {destination}"
            )
        payload = source.read_bytes().replace(source_token, target_token)
        temporary = destination.with_name(f".{destination.name}.handoff-{os.getpid()}")
        with temporary.open("xb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, stat.S_IMODE(source_metadata.st_mode))
        os.replace(temporary, destination)


def hash_regular_file(path: Path, *, fail_callback) -> str:
    metadata = path.lstat()
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
        fail_callback(f"path is not a regular file: {path}")
    hasher = hashlib.sha256()
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (
            metadata.st_dev,
            metadata.st_ino,
        ):
            fail_callback(f"path changed before read: {path}")
        while True:
            chunk = os.read(descriptor, CHUNK_SIZE)
            if not chunk:
                break
            hasher.update(chunk)
        after = os.fstat(descriptor)
        if (opened.st_size, opened.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            fail_callback(f"path changed during read: {path}")
    finally:
        os.close(descriptor)
    return hasher.hexdigest()


def regular_tree_inventory(root: Path, *, fail_callback) -> dict[str, str]:
    if root.is_symlink() or not root.is_dir():
        fail_callback(f"required regular tree is missing or symlinked: {root}")
    inventory: dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current = Path(current_root)
        for name in directory_names:
            metadata = (current / name).lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                fail_callback(f"tree contains a non-directory or symlink: {current / name}")
        for name in file_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                fail_callback(f"tree contains a non-regular file or symlink: {path}")
            hasher = hashlib.sha256()
            descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            try:
                opened = os.fstat(descriptor)
                if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (
                    metadata.st_dev,
                    metadata.st_ino,
                ):
                    fail_callback(f"file changed before inventory: {path}")
                while True:
                    chunk = os.read(descriptor, CHUNK_SIZE)
                    if not chunk:
                        break
                    hasher.update(chunk)
                after = os.fstat(descriptor)
                if (opened.st_size, opened.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                    fail_callback(f"file changed during inventory: {path}")
            finally:
                os.close(descriptor)
            inventory[path.relative_to(root).as_posix()] = hasher.hexdigest()
    return inventory


def command_publish_stage_only(arguments: list[str]) -> None:
    candidate = Path(arguments[0]).absolute()
    output = Path(arguments[1]).absolute()

    def fail(message: str) -> NoReturn:
        die(message, prefix="Stage-only release candidate publication failed: ")

    try:
        output.lstat()
    except FileNotFoundError:
        pass
    except OSError as exc:
        fail(f"cannot inspect requested output {output} ({exc})")
    else:
        fail(f"requested output already exists: {output}")

    candidate_metadata = candidate.lstat()
    parent_metadata = output.parent.lstat()
    if stat.S_ISLNK(candidate_metadata.st_mode) or not stat.S_ISDIR(candidate_metadata.st_mode):
        fail(f"candidate is not a regular directory: {candidate}")
    if stat.S_ISLNK(parent_metadata.st_mode) or not stat.S_ISDIR(parent_metadata.st_mode):
        fail(f"output parent is not a regular directory: {output.parent}")
    if candidate_metadata.st_dev != parent_metadata.st_dev:
        fail("candidate and output parent are not on the same filesystem")

    expected = regular_tree_inventory(candidate, fail_callback=fail)
    libc = ctypes.CDLL(None, use_errno=True)
    renameat2 = getattr(libc, "renameat2", None)
    if renameat2 is None:
        fail("the platform does not provide atomic no-replace renameat2")
    renameat2.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    renameat2.restype = ctypes.c_int
    old_parent_fd = os.open(
        candidate.parent,
        os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
    )
    new_parent_fd = os.open(
        output.parent,
        os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        result = renameat2(
            old_parent_fd,
            os.fsencode(candidate.name),
            new_parent_fd,
            os.fsencode(output.name),
            1,
        )
        if result != 0:
            error = ctypes.get_errno()
            if error == errno.EEXIST:
                fail(f"requested output appeared before atomic publication: {output}")
            fail(f"atomic no-replace rename failed: {os.strerror(error)}")
        os.fsync(old_parent_fd)
        os.fsync(new_parent_fd)
    finally:
        os.close(old_parent_fd)
        os.close(new_parent_fd)

    published_metadata = output.lstat()
    if (published_metadata.st_dev, published_metadata.st_ino) != (
        candidate_metadata.st_dev,
        candidate_metadata.st_ino,
    ):
        fail("published output identity differs from the validated candidate")
    if regular_tree_inventory(output, fail_callback=fail) != expected:
        fail("published output bytes differ from the validated candidate")


def command_compare_validator(arguments: list[str]) -> None:
    governed_validator, configured_validator, governed_support, configured_support = map(Path, arguments)

    def simple_hash(path: Path) -> str:
        hasher = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(CHUNK_SIZE), b""):
                hasher.update(chunk)
        return hasher.hexdigest()

    if (
        simple_hash(governed_validator) != simple_hash(configured_validator)
        or simple_hash(governed_support) != simple_hash(configured_support)
    ):
        die("Configured build provenance validator does not match the governed portable validator bytes.")


def mac_file_name(value: object) -> bool:
    name = Path(str(value or "").strip()).name.lower()
    return name.startswith("chummer-") and ("-osx-" in name or "-macos-" in name)


def command_classify_provenance(arguments: list[str]) -> None:
    manifest_path = Path(arguments[0])
    files_root = Path(arguments[1])
    mac_files = []
    if files_root.is_dir():
        mac_files = sorted(
            path.name for path in files_root.iterdir() if path.is_file() and mac_file_name(path.name)
        )

    if not manifest_path.is_file() or manifest_path.is_symlink():
        if mac_files:
            print(
                f"Mac artifacts require a regular canonical source manifest before publication: {manifest_path}",
                file=sys.stderr,
            )
            raise SystemExit(2)
        raise SystemExit(1)
    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        print(
            f"Canonical source manifest is unreadable for build provenance: {manifest_path} ({exc})",
            file=sys.stderr,
        )
        raise SystemExit(2)
    rows = payload.get("artifacts") if isinstance(payload, dict) else None
    if not isinstance(rows, list):
        print(
            f"Canonical source manifest artifacts must be a list: {manifest_path}",
            file=sys.stderr,
        )
        raise SystemExit(2)
    mac_rows = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        platform = str(row.get("platform") or "").strip().lower()
        platform = {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(platform, platform)
        file_name = row.get("fileName") or row.get("downloadUrl") or row.get("url")
        if platform == "macos" or mac_file_name(file_name):
            mac_rows.append(row)
    if mac_rows:
        raise SystemExit(0)
    if mac_files:
        print(
            "Mac artifact bytes are present but the canonical source manifest has no Mac rows; "
            "refusing unproven publication.",
            file=sys.stderr,
        )
        raise SystemExit(2)
    raise SystemExit(1)


def command_copy_tree(arguments: list[str]) -> None:
    source_root = Path(arguments[0])
    target_root = Path(arguments[1])
    lexical_root = Path(arguments[2])

    def fail(message: str) -> NoReturn:
        die(message)

    for candidate in (lexical_root, source_root):
        if candidate.is_symlink():
            fail(f"Build provenance path cannot be a symlink: {candidate}")
    if not source_root.is_dir():
        fail(f"Build provenance source directory is missing: {source_root}")

    entries: list[tuple[Path, Path, os.stat_result]] = []
    for current_root, directory_names, file_names in os.walk(
        source_root,
        topdown=True,
        followlinks=False,
    ):
        current = Path(current_root)
        for name in sorted(directory_names):
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                fail(f"Build provenance contains a non-directory or symlinked path: {path}")
        for name in sorted(file_names):
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                fail(f"Build provenance contains a non-regular or symlinked file: {path}")
            entries.append((path, path.relative_to(source_root), metadata))

    if target_root.exists() or target_root.is_symlink():
        fail(f"Build provenance staging target already exists: {target_root}")
    target_root.mkdir(parents=True, mode=0o700)
    for source, relative, before in entries:
        destination = target_root / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        source_fd = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        digest = hashlib.sha256()
        try:
            opened = os.fstat(source_fd)
            if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (
                before.st_dev,
                before.st_ino,
            ):
                fail(f"Build provenance source changed before copy: {source}")
            with os.fdopen(source_fd, "rb", closefd=False) as source_stream, destination.open(
                "xb"
            ) as destination_stream:
                while True:
                    chunk = source_stream.read(CHUNK_SIZE)
                    if not chunk:
                        break
                    digest.update(chunk)
                    destination_stream.write(chunk)
                destination_stream.flush()
                os.fsync(destination_stream.fileno())
            after = os.fstat(source_fd)
            if (
                after.st_dev,
                after.st_ino,
                after.st_size,
                after.st_mtime_ns,
            ) != (
                opened.st_dev,
                opened.st_ino,
                opened.st_size,
                opened.st_mtime_ns,
            ):
                fail(f"Build provenance source changed during copy: {source}")
        finally:
            os.close(source_fd)
        if destination.stat().st_size != before.st_size:
            fail(f"Build provenance staging size mismatch: {relative}")
        staged_digest = hashlib.sha256()
        with destination.open("rb") as staged_stream:
            for chunk in iter(lambda: staged_stream.read(CHUNK_SIZE), b""):
                staged_digest.update(chunk)
        if staged_digest.hexdigest() != digest.hexdigest():
            fail(f"Build provenance staging digest mismatch: {relative}")
        os.chmod(destination, stat.S_IMODE(before.st_mode))


def command_stage_validator(arguments: list[str]) -> None:
    source_validator = Path(arguments[0])
    source_support = source_validator.with_name("build_provenance_support.py")
    target_dir = Path(arguments[1])
    target_dir.mkdir(parents=True, mode=0o700)

    def validator_hash(path: Path) -> str:
        def fail(message: str) -> NoReturn:
            raise ValueError(message)

        return hash_regular_file(path, fail_callback=fail)

    for source in (source_validator, source_support):
        expected_digest = validator_hash(source)
        destination = target_dir / source.name
        if destination.exists() or destination.is_symlink():
            raise ValueError(f"private validator staging path already exists: {destination}")
        source_descriptor = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        try:
            with destination.open("xb") as target_stream:
                while True:
                    chunk = os.read(source_descriptor, CHUNK_SIZE)
                    if not chunk:
                        break
                    target_stream.write(chunk)
                target_stream.flush()
                os.fsync(target_stream.fileno())
        finally:
            os.close(source_descriptor)
        os.chmod(destination, 0o400)
        if validator_hash(source) != expected_digest or validator_hash(destination) != expected_digest:
            raise ValueError(f"governed validator bytes changed while staging: {source}")
    print(target_dir / source_validator.name)


def command_compare_trees(arguments: list[str]) -> None:
    def fail(message: str) -> NoReturn:
        raise ValueError(message)

    try:
        left = regular_tree_inventory(Path(arguments[0]), fail_callback=fail)
        right = regular_tree_inventory(Path(arguments[1]), fail_callback=fail)
    except (OSError, ValueError) as exc:
        die(f"Build provenance tree comparison failed: {exc}")
    if left != right:
        die("Build provenance tree bytes differ from the validated source.")


def command_verify_mac_identity(arguments: list[str]) -> None:
    canonical_path = Path(arguments[0])
    compatibility_path = Path(arguments[1])
    files_root = Path(arguments[2])

    def fail(message: str) -> NoReturn:
        die(message, prefix="Build provenance candidate manifest disagreement: ")

    def load(path: Path) -> dict:
        try:
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as exc:
            fail(f"{path} is unavailable or malformed ({exc})")
        if not isinstance(payload, dict):
            fail(f"{path} must contain an object")
        return payload

    def file_name(row: dict) -> str:
        direct = str(row.get("fileName") or "").strip()
        if direct:
            return Path(direct).name
        return Path(str(row.get("downloadUrl") or row.get("url") or "").strip()).name

    def is_mac(row: dict) -> bool:
        platform = str(row.get("platform") or "").strip().lower()
        platform = {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(
            platform,
            platform,
        )
        name = file_name(row).lower()
        return platform == "macos" or "-osx-" in name or "-macos-" in name

    def identity(row: dict) -> tuple[str, str, str, int]:
        artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
        name = file_name(row)
        digest = str(row.get("sha256") or "").strip().lower().removeprefix("sha256:")
        raw_size = row.get("sizeBytes")
        if (
            not artifact_id
            or not name
            or len(digest) != 64
            or not isinstance(raw_size, int)
            or isinstance(raw_size, bool)
        ):
            fail(f"incomplete Mac identity row: {artifact_id or name or '<unknown>'}")
        return artifact_id, name, digest, raw_size

    canonical = load(canonical_path)
    compatibility = load(compatibility_path)
    canonical_rows = canonical.get("artifacts")
    compatibility_rows = compatibility.get("downloads")
    if not isinstance(canonical_rows, list) or not isinstance(compatibility_rows, list):
        fail("canonical artifacts and compatibility downloads must both be lists")
    canonical_identities = sorted(
        identity(row) for row in canonical_rows if isinstance(row, dict) and is_mac(row)
    )
    compatibility_identities = sorted(
        identity(row) for row in compatibility_rows if isinstance(row, dict) and is_mac(row)
    )
    if canonical_identities != compatibility_identities:
        fail(
            f"canonical Mac identities {canonical_identities!r} do not match compatibility "
            f"Mac identities {compatibility_identities!r}"
        )
    for _, name, expected_digest, expected_size in canonical_identities:
        artifact = files_root / name
        if artifact.is_symlink() or not artifact.is_file():
            fail(f"candidate Mac artifact is missing or symlinked: {artifact}")
        hasher = hashlib.sha256()
        with artifact.open("rb") as stream:
            for chunk in iter(lambda: stream.read(CHUNK_SIZE), b""):
                hasher.update(chunk)
        if artifact.stat().st_size != expected_size or hasher.hexdigest() != expected_digest:
            fail(f"candidate Mac artifact bytes do not match both manifests: {name}")


def command_verify_shelf(arguments: list[str]) -> None:
    candidate_root = Path(arguments[0]).absolute()
    requested_channel = str(arguments[1] or "").strip().lower()
    target_roots = [Path(value).absolute() for value in arguments[2:]]
    canonical_path = candidate_root / "RELEASE_CHANNEL.generated.json"
    compatibility_path = candidate_root / "releases.json"
    files_root = candidate_root / "files"

    def fail(message: str) -> NoReturn:
        die(message, prefix="Release candidate shelf invariant failed: ")

    def load_required(path: Path) -> dict:
        if path.is_symlink() or not path.is_file():
            fail(f"required candidate manifest is missing or symlinked: {path}")
        try:
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as exc:
            fail(f"candidate manifest is unreadable: {path} ({exc})")
        if not isinstance(payload, dict):
            fail(f"candidate manifest must contain a JSON object: {path}")
        return payload

    def load_optional(path: Path) -> dict:
        if path.is_symlink() or not path.is_file():
            return {}
        try:
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError):
            return {}
        return payload if isinstance(payload, dict) else {}

    def normalized_platform(value: object) -> str:
        platform = str(value or "").strip().lower()
        return {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(
            platform,
            platform,
        )

    def compatibility_platform(row: dict) -> str:
        for value in (row.get("platformId"), row.get("platform")):
            token = str(value or "").strip().lower()
            normalized = normalized_platform(token)
            if normalized in CANONICAL_PLATFORM_FLOOR:
                return normalized
            for platform in CANONICAL_PLATFORM_FLOOR:
                if token.startswith(f"{platform}-"):
                    return platform
        return ""

    def normalized_digest(value: object) -> str:
        return str(value or "").strip().lower().removeprefix("sha256:")

    def manifest_version(payload: dict) -> str:
        return str(
            payload.get("version")
            or payload.get("releaseVersion")
            or payload.get("release_version")
            or ""
        ).strip()

    def manifest_channel(payload: dict) -> str:
        return str(
            payload.get("channel")
            or payload.get("releaseChannel")
            or payload.get("release_channel")
            or ""
        ).strip().lower()

    def file_name(row: dict) -> str:
        direct = str(row.get("fileName") or "").strip()
        raw = direct or str(row.get("downloadUrl") or row.get("url") or "").strip()
        return Path(raw).name if raw else ""

    def is_installer(row: dict) -> bool:
        kind = str(row.get("kind") or "").strip().lower()
        return kind == "installer" or file_name(row).lower().endswith(INSTALLER_SUFFIXES)

    def artifact_id(row: dict) -> str:
        return str(row.get("artifactId") or row.get("id") or "").strip()

    def identity(row: dict, *, label: str) -> tuple[str, str, str, int]:
        row_id = artifact_id(row)
        declared_name = str(row.get("fileName") or "").strip()
        if declared_name and (
            declared_name in {".", ".."}
            or "/" in declared_name
            or "\\" in declared_name
            or Path(declared_name).name != declared_name
        ):
            fail(f"{label} installer {row_id or '<unknown>'} fileName must be a base name")
        name = file_name(row)
        digest = normalized_digest(row.get("sha256"))
        size = row.get("sizeBytes")
        if not row_id or not name:
            fail(f"{label} installer identity is missing artifact id or file name")
        if not DIGEST_RE.fullmatch(digest):
            fail(f"{label} installer {row_id} has an invalid sha256")
        if not isinstance(size, int) or isinstance(size, bool) or size < 0:
            fail(f"{label} installer {row_id} has an invalid sizeBytes")
        return row_id, name, digest, size

    def installer_rows(payload: dict, key: str, *, label: str) -> list[dict]:
        rows = payload.get(key)
        if not isinstance(rows, list):
            fail(f"{label} {key} must be a list")
        return [row for row in rows if isinstance(row, dict) and is_installer(row)]

    def indexed_identities(
        rows: list[dict],
        *,
        label: str,
    ) -> dict[str, tuple[dict, tuple[str, str, str, int]]]:
        indexed: dict[str, tuple[dict, tuple[str, str, str, int]]] = {}
        for row in rows:
            item = identity(row, label=label)
            row_id = item[0]
            if row_id in indexed:
                fail(f"{label} contains duplicate installer artifact id: {row_id}")
            indexed[row_id] = (row, item)
        if not indexed:
            fail(f"{label} contains no installer bindings")
        return indexed

    def installer_tuples(rows: list[dict], *, label: str) -> set[tuple[str, str, str]]:
        tuples: set[tuple[str, str, str]] = set()
        for row in rows:
            head = str(row.get("head") or "").strip().lower()
            platform = normalized_platform(row.get("platform"))
            rid = str(row.get("rid") or "").strip().lower()
            if not head or not platform or not rid:
                fail(
                    f"{label} installer {artifact_id(row) or file_name(row) or '<unknown>'} "
                    "is missing head/platform/rid"
                )
            tuples.add((head, platform, rid))
        return tuples

    canonical = load_required(canonical_path)
    compatibility = load_required(compatibility_path)
    canonical_version = manifest_version(canonical)
    compatibility_version = manifest_version(compatibility)
    canonical_channel = manifest_channel(canonical)
    compatibility_channel = manifest_channel(compatibility)
    if not canonical_version or canonical_version != compatibility_version:
        fail(
            "canonical and compatibility manifests must declare the same non-empty version "
            f"(canonical={canonical_version or 'missing'} "
            f"compatibility={compatibility_version or 'missing'})"
        )
    if not canonical_channel or canonical_channel != compatibility_channel:
        fail(
            "canonical and compatibility manifests must declare the same non-empty channel "
            f"(canonical={canonical_channel or 'missing'} "
            f"compatibility={compatibility_channel or 'missing'})"
        )
    if requested_channel and canonical_channel != requested_channel:
        fail(
            f"staged manifest channel {canonical_channel} does not match requested publication "
            f"channel {requested_channel}"
        )

    canonical_rows = installer_rows(canonical, "artifacts", label="canonical manifest")
    compatibility_rows = installer_rows(
        compatibility,
        "downloads",
        label="compatibility manifest",
    )
    canonical_identities = indexed_identities(canonical_rows, label="canonical manifest")
    compatibility_identities = indexed_identities(
        compatibility_rows,
        label="compatibility manifest",
    )
    if set(canonical_identities) != set(compatibility_identities):
        fail(
            "canonical and compatibility installer artifact ids differ "
            f"(canonical={sorted(canonical_identities)} "
            f"compatibility={sorted(compatibility_identities)})"
        )

    for row_id, (canonical_row, canonical_identity) in canonical_identities.items():
        compatibility_row, compatibility_identity = compatibility_identities[row_id]
        if canonical_identity != compatibility_identity:
            fail(f"canonical and compatibility installer identity differs for {row_id}")
        canonical_head = str(canonical_row.get("head") or "").strip().lower()
        compatibility_head = str(compatibility_row.get("head") or "").strip().lower()
        if canonical_head and compatibility_head and canonical_head != compatibility_head:
            fail(f"canonical and compatibility head differs for {row_id}")
        canonical_platform = normalized_platform(canonical_row.get("platform"))
        compatibility_platform_id = compatibility_platform(compatibility_row)
        if (
            canonical_platform
            and compatibility_platform_id
            and canonical_platform != compatibility_platform_id
        ):
            fail(f"canonical and compatibility platform differs for {row_id}")
        canonical_rid = str(canonical_row.get("rid") or "").strip().lower()
        compatibility_rid = str(compatibility_row.get("rid") or "").strip().lower()
        if canonical_rid and compatibility_rid and canonical_rid != compatibility_rid:
            fail(f"canonical and compatibility rid differs for {row_id}")
        _, name, expected_digest, expected_size = canonical_identity
        artifact = files_root / name
        if artifact.is_symlink() or not artifact.is_file():
            fail(f"candidate installer bytes are missing or symlinked: {artifact}")
        if artifact.stat().st_size != expected_size:
            fail(f"candidate installer size does not match both manifests: {name}")
        hasher = hashlib.sha256()
        with artifact.open("rb") as stream:
            for chunk in iter(lambda: stream.read(CHUNK_SIZE), b""):
                hasher.update(chunk)
        if hasher.hexdigest() != expected_digest:
            fail(f"candidate installer digest does not match both manifests: {name}")

    candidate_tuples = installer_tuples(canonical_rows, label="candidate")
    coverage = canonical.get("desktopTupleCoverage")
    if requested_channel == "public_stable":
        if not isinstance(coverage, dict):
            fail("public_stable candidate is missing desktopTupleCoverage")
        required_platforms = [
            normalized_platform(value)
            for value in coverage.get("requiredDesktopPlatforms", [])
        ]
        if tuple(required_platforms) != CANONICAL_PLATFORM_FLOOR:
            fail(
                "public_stable candidate requiredDesktopPlatforms must equal the canonical "
                f"platform floor {list(CANONICAL_PLATFORM_FLOOR)}"
            )
        if coverage.get("complete") is not True:
            fail("public_stable candidate desktopTupleCoverage.complete must be true")
        missing_lists = (
            "missingRequiredPlatforms",
            "missingRequiredHeads",
            "missingRequiredPlatformHeadPairs",
            "missingRequiredPlatformHeadRidTuples",
        )
        if any(coverage.get(key) not in (None, []) for key in missing_lists):
            fail("public_stable candidate desktopTupleCoverage still declares missing required tuples")
        missing_floor = [
            platform
            for platform in CANONICAL_PLATFORM_FLOOR
            if not any(
                head == "avalonia" and row_platform == platform
                for head, row_platform, _ in candidate_tuples
            )
        ]
        if missing_floor:
            fail(f"public_stable candidate is missing primary installer bindings for: {missing_floor}")

    def protected_incumbent_tuples(target_root: Path) -> set[tuple[str, str, str]]:
        payload = load_optional(target_root / "RELEASE_CHANNEL.generated.json")
        coverage_payload = payload.get("desktopTupleCoverage") if isinstance(payload, dict) else None
        rows = payload.get("artifacts") if isinstance(payload, dict) else None
        if not isinstance(coverage_payload, dict) or coverage_payload.get("complete") is not True:
            return set()
        if not isinstance(rows, list):
            return set()
        required_platforms = {
            normalized_platform(value)
            for value in coverage_payload.get("requiredDesktopPlatforms", [])
            if normalized_platform(value)
        }
        incumbent_rows = [
            row for row in rows if isinstance(row, dict) and is_installer(row)
        ]
        incumbent_tuples: set[tuple[str, str, str]] = set()
        for row in incumbent_rows:
            head = str(row.get("head") or "").strip().lower()
            platform = normalized_platform(row.get("platform"))
            rid = str(row.get("rid") or "").strip().lower()
            digest = normalized_digest(row.get("sha256"))
            size = row.get("sizeBytes")
            if (
                not artifact_id(row)
                or not head
                or not platform
                or not rid
                or not DIGEST_RE.fullmatch(digest)
                or not isinstance(size, int)
                or isinstance(size, bool)
                or size < 0
            ):
                return set()
            incumbent_tuples.add((head, platform, rid))
        incumbent_platforms = {platform for _, platform, _ in incumbent_tuples}
        if not required_platforms or not required_platforms.issubset(incumbent_platforms):
            return set()
        for key in (
            "missingRequiredPlatforms",
            "missingRequiredHeads",
            "missingRequiredPlatformHeadPairs",
            "missingRequiredPlatformHeadRidTuples",
        ):
            if coverage_payload.get(key) not in (None, []):
                return set()
        return incumbent_tuples

    for target_root in target_roots:
        incumbent_tuples = protected_incumbent_tuples(target_root)
        removed = sorted(incumbent_tuples - candidate_tuples)
        if removed:
            rendered = [f"{head}:{platform}:{rid}" for head, platform, rid in removed]
            fail(
                f"candidate would erase installer tuples from complete shelf {target_root}: "
                f"{rendered}. Publish one coherent cross-platform candidate instead."
            )


def command_preflight_managed(arguments: list[str]) -> None:
    target = Path(arguments[0]).absolute()
    managed_files = (
        "releases.json",
        "RELEASE_CHANNEL.generated.json",
        "aur-packages.json",
        "PUBLICATION_SCOPE.generated.json",
        "RELEASE_BUILD_HANDOFF.generated.json",
        "RELEASE_BUILD_HANDOFF.generated.md",
        "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
        "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md",
        "QUARANTINED_INSTALLER_PROMOTION.generated.json",
    )
    managed_trees = ("files", "startup-smoke", "release-evidence")

    def fail(message: str) -> NoReturn:
        die(message, prefix="Managed release target preflight failed: ")

    try:
        target_metadata = target.lstat()
    except FileNotFoundError:
        return
    except OSError as exc:
        fail(f"cannot inspect target root {target} ({exc})")
    if stat.S_ISLNK(target_metadata.st_mode) or not stat.S_ISDIR(target_metadata.st_mode):
        fail(f"target root is not a regular directory: {target}")

    for name in managed_files:
        path = target / name
        try:
            metadata = path.lstat()
        except FileNotFoundError:
            continue
        except OSError as exc:
            fail(f"cannot inspect managed file {path} ({exc})")
        if stat.S_ISLNK(metadata.st_mode):
            fail(f"managed file is symlinked: {path}")
        if not stat.S_ISREG(metadata.st_mode):
            fail(f"managed file is not regular: {path}")

    for name in managed_trees:
        root = target / name
        try:
            metadata = root.lstat()
        except FileNotFoundError:
            continue
        except OSError as exc:
            fail(f"cannot inspect managed tree {root} ({exc})")
        if stat.S_ISLNK(metadata.st_mode):
            fail(f"managed tree is symlinked: {root}")
        if not stat.S_ISDIR(metadata.st_mode):
            fail(f"managed tree is not a directory: {root}")
        for current_root, directory_names, file_names in os.walk(
            root,
            topdown=True,
            followlinks=False,
        ):
            current = Path(current_root)
            for child_name in directory_names:
                child = current / child_name
                child_metadata = child.lstat()
                if stat.S_ISLNK(child_metadata.st_mode) or not stat.S_ISDIR(
                    child_metadata.st_mode
                ):
                    fail(f"managed tree contains an unsafe directory: {child}")
            for child_name in file_names:
                child = current / child_name
                child_metadata = child.lstat()
                if stat.S_ISLNK(child_metadata.st_mode) or not stat.S_ISREG(
                    child_metadata.st_mode
                ):
                    fail(f"managed tree contains an unsafe file: {child}")


def command_transaction(arguments: list[str]) -> None:
    if len(arguments) < 3:
        die("transaction requires a candidate, validator selector, and at least one target")
    candidate = Path(arguments[0]).absolute()
    validator_argument = arguments[1]
    validator_enabled = validator_argument != "-"
    validator_path = Path(validator_argument).absolute() if validator_enabled else None
    raw_targets = [Path(value).absolute() for value in arguments[2:]]
    targets: list[Path] = []
    seen: set[str] = set()
    for target in raw_targets:
        key = os.path.normcase(str(target))
        if key not in seen:
            targets.append(target)
            seen.add(key)
    targets.sort(key=lambda path: os.path.normcase(str(path)))

    def fail(message: str) -> NoReturn:
        print(f"Transactional release candidate cutover failed: {message}", file=sys.stderr)
        raise RuntimeError(message)

    def inventory(root: Path) -> dict[str, str]:
        return regular_tree_inventory(root, fail_callback=fail)

    def managed_path_snapshot(root: Path, relative: Path) -> tuple[str, object]:
        path = root / relative
        if path.is_symlink():
            fail(f"managed path is symlinked: {path}")
        if not path.exists():
            return "missing", None
        if path.is_dir():
            return "directory", inventory(path)
        if path.is_file():
            hasher = hashlib.sha256()
            descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            try:
                metadata = os.fstat(descriptor)
                if not stat.S_ISREG(metadata.st_mode):
                    fail(f"managed path is not a regular file: {path}")
                while True:
                    chunk = os.read(descriptor, CHUNK_SIZE)
                    if not chunk:
                        break
                    hasher.update(chunk)
            finally:
                os.close(descriptor)
            return "file", hasher.hexdigest()
        fail(f"managed path is not regular: {path}")

    exact_managed_paths = (
        Path("startup-smoke"),
        Path("aur-packages.json"),
        Path("PUBLICATION_SCOPE.generated.json"),
        Path("RELEASE_BUILD_HANDOFF.generated.json"),
        Path("RELEASE_BUILD_HANDOFF.generated.md"),
        Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"),
        Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"),
        Path("QUARANTINED_INSTALLER_PROMOTION.generated.json"),
        Path("release-evidence"),
    )

    def release_commitment(root: Path, *, proof_required: bool) -> str:
        snapshot: dict[str, object] = {
            "releases.json": managed_path_snapshot(root, Path("releases.json")),
            "RELEASE_CHANNEL.generated.json": managed_path_snapshot(
                root,
                Path("RELEASE_CHANNEL.generated.json"),
            ),
            "files": managed_path_snapshot(root, Path("files")),
            "managed": {
                relative.as_posix(): managed_path_snapshot(root, relative)
                for relative in exact_managed_paths
            },
        }
        for required_name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
            if snapshot[required_name][0] != "file":  # type: ignore[index]
                fail(f"required release manifest is missing: {root / required_name}")
        if snapshot["files"][0] != "directory":  # type: ignore[index]
            fail(f"required release files tree is missing: {root / 'files'}")
        proof_path = root / "proof" / "build-provenance" / "v1"
        proof_snapshot = managed_path_snapshot(
            root,
            Path("proof") / "build-provenance" / "v1",
        )
        if proof_required and proof_snapshot[0] != "directory":
            fail(f"required governed build provenance is missing: {proof_path}")
        if not proof_required and proof_snapshot[0] != "missing":
            fail(f"non-Mac candidate retained stale build provenance: {proof_path}")
        snapshot["proof"] = proof_snapshot
        encoded = json.dumps(
            snapshot,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
        return hashlib.sha256(encoded).hexdigest()

    def copy_regular_tree(source: Path, destination: Path) -> None:
        expected = inventory(source)
        shutil.copytree(source, destination, copy_function=shutil.copy2)
        if inventory(destination) != expected:
            fail(f"copied tree differs from candidate: {source}")

    def replace_path_from_candidate(
        stage: Path,
        relative: Path,
        *,
        required: bool = False,
    ) -> None:
        source = candidate / relative
        destination = stage / relative
        if destination.is_symlink():
            fail(f"managed target path is symlinked: {destination}")
        if destination.is_dir():
            shutil.rmtree(destination)
        elif destination.exists():
            destination.unlink()
        if not source.exists():
            if required:
                fail(f"candidate is missing required path: {source}")
            return
        if source.is_symlink():
            fail(f"candidate managed path is symlinked: {source}")
        destination.parent.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            copy_regular_tree(source, destination)
        elif source.is_file():
            shutil.copy2(source, destination, follow_symlinks=False)
        else:
            fail(f"candidate managed path is not regular: {source}")

    def apply_candidate(stage: Path) -> None:
        replace_path_from_candidate(stage, Path("releases.json"), required=True)
        replace_path_from_candidate(
            stage,
            Path("RELEASE_CHANNEL.generated.json"),
            required=True,
        )
        replace_path_from_candidate(stage, Path("files"), required=True)
        replace_path_from_candidate(stage, Path("startup-smoke"))
        replace_path_from_candidate(stage, Path("aur-packages.json"))
        replace_path_from_candidate(stage, Path("PUBLICATION_SCOPE.generated.json"))
        replace_path_from_candidate(stage, Path("RELEASE_BUILD_HANDOFF.generated.json"))
        replace_path_from_candidate(stage, Path("RELEASE_BUILD_HANDOFF.generated.md"))
        replace_path_from_candidate(
            stage,
            Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"),
        )
        replace_path_from_candidate(
            stage,
            Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"),
        )
        replace_path_from_candidate(
            stage,
            Path("QUARANTINED_INSTALLER_PROMOTION.generated.json"),
        )
        replace_path_from_candidate(stage, Path("release-evidence"))

        candidate_v1 = candidate / "proof" / "build-provenance" / "v1"
        target_v1 = stage / "proof" / "build-provenance" / "v1"
        for path in (stage / "proof", stage / "proof" / "build-provenance", target_v1):
            if path.is_symlink():
                fail(f"managed target proof path is symlinked: {path}")
        if target_v1.is_dir():
            shutil.rmtree(target_v1)
        elif target_v1.exists():
            target_v1.unlink()
        if candidate_v1.is_dir() and not candidate_v1.is_symlink():
            target_v1.parent.mkdir(parents=True, exist_ok=True)
            copy_regular_tree(candidate_v1, target_v1)
        elif validator_enabled:
            fail(f"candidate is missing governed proof namespace: {candidate_v1}")

    def path_exists(path: Path) -> bool:
        try:
            path.lstat()
        except FileNotFoundError:
            return False
        return True

    def remove_path(path: Path) -> None:
        try:
            metadata = path.lstat()
        except FileNotFoundError:
            return
        if stat.S_ISDIR(metadata.st_mode) and not stat.S_ISLNK(metadata.st_mode):
            shutil.rmtree(path)
        else:
            path.unlink()

    def fsync_tree(root: Path) -> None:
        for current_root, directory_names, file_names in os.walk(
            root,
            topdown=True,
            followlinks=False,
        ):
            current = Path(current_root)
            for name in directory_names:
                metadata = (current / name).lstat()
                if stat.S_ISLNK(metadata.st_mode):
                    continue
                if not stat.S_ISDIR(metadata.st_mode):
                    fail(f"staged shelf contains an unsafe directory entry: {current / name}")
            for name in file_names:
                path = current / name
                metadata = path.lstat()
                if stat.S_ISLNK(metadata.st_mode):
                    continue
                if not stat.S_ISREG(metadata.st_mode):
                    fail(f"staged shelf contains a non-regular file: {path}")
                descriptor = os.open(
                    path,
                    os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                )
                try:
                    os.fsync(descriptor)
                finally:
                    os.close(descriptor)
        directories = [
            Path(current_root)
            for current_root, _, _ in os.walk(
                root,
                topdown=False,
                followlinks=False,
            )
        ]
        for directory in directories:
            metadata = directory.lstat()
            if stat.S_ISLNK(metadata.st_mode):
                continue
            descriptor = os.open(
                directory,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
            )
            try:
                os.fsync(descriptor)
            finally:
                os.close(descriptor)

    def assert_parent_identity(context: dict[str, object]) -> None:
        parent = context["parent"]
        descriptor = context["fd"]
        opened = context["opened"]
        assert isinstance(parent, Path)
        assert isinstance(descriptor, int)
        assert isinstance(opened, os.stat_result)
        try:
            current = parent.lstat()
        except OSError as exc:
            fail(f"publication target parent disappeared: {parent} ({exc})")
        if stat.S_ISLNK(current.st_mode) or not stat.S_ISDIR(current.st_mode):
            fail(f"publication target parent is no longer a regular directory: {parent}")
        if (current.st_dev, current.st_ino) != (opened.st_dev, opened.st_ino):
            fail(f"publication target parent identity changed: {parent}")
        descriptor_metadata = os.fstat(descriptor)
        if (descriptor_metadata.st_dev, descriptor_metadata.st_ino) != (
            opened.st_dev,
            opened.st_ino,
        ):
            fail(f"held publication parent descriptor changed identity: {parent}")

    def sibling_replace(context: dict[str, object], source_name: str, target_name: str) -> None:
        assert_parent_identity(context)
        descriptor = context["fd"]
        assert isinstance(descriptor, int)
        os.replace(
            source_name,
            target_name,
            src_dir_fd=descriptor,
            dst_dir_fd=descriptor,
        )
        os.fsync(descriptor)
        assert_parent_identity(context)

    def journal_name(context: dict[str, object], transaction_id: str) -> str:
        target = context["target"]
        assert isinstance(target, Path)
        return f".{target.name}.release-transaction-{transaction_id}.json"

    def atomic_write_journal(
        context: dict[str, object],
        transaction_id: str,
        payload: dict[str, object],
    ) -> None:
        descriptor = context["fd"]
        assert isinstance(descriptor, int)
        name = journal_name(context, transaction_id)
        try:
            existing = os.stat(name, dir_fd=descriptor, follow_symlinks=False)
        except FileNotFoundError:
            existing = None
        if existing is not None and not stat.S_ISREG(existing.st_mode):
            fail(f"transaction journal path is unsafe: {context['parent'] / name}")
        temporary_name = f".{name}.write-{os.getpid()}-{uuid.uuid4().hex}"
        temporary_descriptor = os.open(
            temporary_name,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_NOFOLLOW", 0),
            0o600,
            dir_fd=descriptor,
        )
        try:
            encoded = (
                json.dumps(payload, indent=2, sort_keys=True) + "\n"
            ).encode("utf-8")
            view = memoryview(encoded)
            while view:
                written = os.write(temporary_descriptor, view)
                view = view[written:]
            os.fsync(temporary_descriptor)
        finally:
            os.close(temporary_descriptor)
        try:
            os.replace(
                temporary_name,
                name,
                src_dir_fd=descriptor,
                dst_dir_fd=descriptor,
            )
            os.fsync(descriptor)
        except BaseException:
            try:
                os.unlink(temporary_name, dir_fd=descriptor)
            except FileNotFoundError:
                pass
            raise

    def read_journal(context: dict[str, object], name: str) -> dict[str, object]:
        descriptor = context["fd"]
        assert isinstance(descriptor, int)
        journal_descriptor = os.open(
            name,
            os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=descriptor,
        )
        try:
            metadata = os.fstat(journal_descriptor)
            if not stat.S_ISREG(metadata.st_mode):
                fail(f"transaction journal is not a regular file: {context['parent'] / name}")
            chunks: list[bytes] = []
            while True:
                chunk = os.read(journal_descriptor, CHUNK_SIZE)
                if not chunk:
                    break
                chunks.append(chunk)
        finally:
            os.close(journal_descriptor)
        try:
            payload = json.loads(b"".join(chunks))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            fail(f"transaction journal is malformed: {context['parent'] / name} ({exc})")
        if not isinstance(payload, dict):
            fail(f"transaction journal must contain an object: {context['parent'] / name}")
        return payload

    def remove_journals(transaction_id: str) -> None:
        for context in target_contexts:
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            name = journal_name(context, transaction_id)
            try:
                metadata = os.stat(name, dir_fd=descriptor, follow_symlinks=False)
            except FileNotFoundError:
                continue
            if not stat.S_ISREG(metadata.st_mode):
                fail(f"transaction journal path is unsafe: {context['parent'] / name}")
            os.unlink(name, dir_fd=descriptor)
            os.fsync(descriptor)

    def transaction_paths(
        context: dict[str, object],
        transaction_id: str,
    ) -> tuple[Path, Path, Path]:
        anchor = context["anchor"]
        target = context["target"]
        assert isinstance(anchor, Path)
        assert isinstance(target, Path)
        return (
            anchor / target.name,
            anchor / f".{target.name}.release-stage-{transaction_id}",
            anchor / f".{target.name}.release-backup-{transaction_id}",
        )

    def validate_journal_payloads(
        transaction_id: str,
        payloads: list[dict[str, object]],
    ) -> tuple[dict[str, object], str]:
        expected_targets = [str(target) for target in targets]
        if not re.fullmatch(r"[0-9a-f]{32}", transaction_id):
            fail(f"transaction journal has an invalid id: {transaction_id}")
        if not payloads:
            fail(f"transaction journal set is empty: {transaction_id}")
        invariant_keys = (
            "schema",
            "transactionId",
            "targets",
            "incumbentTargets",
            "candidateCommitment",
            "validatorEnabled",
        )
        reference = payloads[0]
        for payload in payloads:
            if any(payload.get(key) != reference.get(key) for key in invariant_keys):
                fail(f"transaction journal copies disagree: {transaction_id}")
        if reference.get("schema") != "chummer.release_candidate.transaction.v1":
            fail(f"transaction journal schema is unsupported: {transaction_id}")
        if reference.get("transactionId") != transaction_id:
            fail(f"transaction journal id does not match its file name: {transaction_id}")
        if reference.get("targets") != expected_targets:
            fail(
                "unfinished release transaction target set differs from this invocation; "
                "rerun with the exact original targets"
            )
        incumbents = reference.get("incumbentTargets")
        if not isinstance(incumbents, list) or any(
            not isinstance(value, str) or value not in expected_targets
            for value in incumbents
        ):
            fail(f"transaction journal incumbent set is invalid: {transaction_id}")
        commitment = reference.get("candidateCommitment")
        if not isinstance(commitment, str) or not DIGEST_RE.fullmatch(commitment):
            fail(f"transaction journal candidate commitment is invalid: {transaction_id}")
        if not isinstance(reference.get("validatorEnabled"), bool):
            fail(f"transaction journal validator mode is invalid: {transaction_id}")
        states = {payload.get("state") for payload in payloads}
        if not states.issubset({"prepared", "committed"}):
            fail(f"transaction journal state is invalid: {transaction_id}")
        recovery_state = "prepared" if "prepared" in states else "committed"
        return reference, recovery_state

    def rollback_prepared(
        transaction_id: str,
        record: dict[str, object],
    ) -> None:
        incumbent_targets = set(record["incumbentTargets"])
        candidate_commitment = str(record["candidateCommitment"])
        proof_required = bool(record["validatorEnabled"])
        for context in reversed(target_contexts):
            target = context["target"]
            assert isinstance(target, Path)
            target_path, stage_path, backup_path = transaction_paths(
                context,
                transaction_id,
            )
            had_incumbent = str(target) in incumbent_targets
            if had_incumbent:
                if path_exists(backup_path):
                    if backup_path.is_symlink() or not backup_path.is_dir():
                        fail(f"transaction backup is unsafe: {backup_path}")
                    failed_path = backup_path.with_name(
                        f".{target.name}.release-recovery-discard-{transaction_id}"
                    )
                    if path_exists(failed_path):
                        fail(f"transaction recovery discard path already exists: {failed_path}")
                    if path_exists(target_path):
                        if target_path.is_symlink() or not target_path.is_dir():
                            fail(f"activated transaction target is unsafe: {target_path}")
                        sibling_replace(
                            context,
                            target.name,
                            failed_path.name,
                        )
                    sibling_replace(
                        context,
                        backup_path.name,
                        target.name,
                    )
                    remove_path(failed_path)
                    descriptor = context["fd"]
                    assert isinstance(descriptor, int)
                    os.fsync(descriptor)
                elif not path_exists(target_path):
                    fail(f"incumbent and backup are both missing during recovery: {target}")
            elif path_exists(target_path):
                if target_path.is_symlink() or not target_path.is_dir():
                    fail(f"new transaction target is unsafe during recovery: {target}")
                if (
                    release_commitment(
                        target_path,
                        proof_required=proof_required,
                    )
                    != candidate_commitment
                ):
                    fail(
                        "refusing to erase a non-candidate path while rolling back "
                        f"new target: {target}"
                    )
                remove_path(target_path)
                descriptor = context["fd"]
                assert isinstance(descriptor, int)
                os.fsync(descriptor)
            remove_path(stage_path)
            if path_exists(backup_path):
                fail(f"transaction backup remained after rollback: {backup_path}")
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            os.fsync(descriptor)
            assert_parent_identity(context)
        remove_journals(transaction_id)

    def roll_forward_committed(
        transaction_id: str,
        record: dict[str, object],
    ) -> None:
        candidate_commitment = str(record["candidateCommitment"])
        proof_required = bool(record["validatorEnabled"])
        for context in target_contexts:
            target = context["target"]
            assert isinstance(target, Path)
            target_path, stage_path, backup_path = transaction_paths(
                context,
                transaction_id,
            )
            if not path_exists(target_path):
                if not path_exists(stage_path):
                    fail(f"committed target and stage are both missing: {target}")
                if (
                    release_commitment(
                        stage_path,
                        proof_required=proof_required,
                    )
                    != candidate_commitment
                ):
                    fail(f"committed recovery stage differs from candidate: {target}")
                sibling_replace(context, stage_path.name, target.name)
            if target_path.is_symlink() or not target_path.is_dir():
                fail(f"committed transaction target is unsafe: {target}")
            if (
                release_commitment(
                    target_path,
                    proof_required=proof_required,
                )
                != candidate_commitment
            ):
                fail(f"committed transaction target differs from candidate: {target}")
            remove_path(stage_path)
            remove_path(backup_path)
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            os.fsync(descriptor)
            assert_parent_identity(context)
        remove_journals(transaction_id)

    def discover_unfinished_transactions() -> dict[str, list[dict[str, object]]]:
        discovered: dict[str, list[dict[str, object]]] = {}
        target_names_by_parent: dict[int, set[str]] = {}
        for context in target_contexts:
            descriptor = context["fd"]
            target = context["target"]
            assert isinstance(descriptor, int)
            assert isinstance(target, Path)
            target_names_by_parent.setdefault(descriptor, set()).add(target.name)
        visited_descriptors: set[int] = set()
        for context in target_contexts:
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            if descriptor in visited_descriptors:
                continue
            visited_descriptors.add(descriptor)
            target_names = target_names_by_parent[descriptor]
            for name in os.listdir(descriptor):
                transaction_id = ""
                for target_name in target_names:
                    prefix = f".{target_name}.release-transaction-"
                    if name.startswith(prefix) and name.endswith(".json"):
                        transaction_id = name[len(prefix) : -len(".json")]
                        break
                if not transaction_id:
                    continue
                discovered.setdefault(transaction_id, []).append(
                    read_journal(context, name)
                )
        return discovered

    def recover_unfinished_transactions(
        discovered: dict[str, list[dict[str, object]]],
    ) -> bool:
        if len(discovered) > 1:
            fail(
                "multiple unfinished release transactions were found for the requested "
                f"targets: {sorted(discovered)}"
            )
        if not discovered:
            return False
        transaction_id, payloads = next(iter(discovered.items()))
        record, recovery_state = validate_journal_payloads(
            transaction_id,
            payloads,
        )
        if recovery_state == "prepared":
            rollback_prepared(transaction_id, record)
            print(
                f"recovered_release_candidate_transaction={transaction_id}:rolled_back",
                file=sys.stderr,
            )
        else:
            roll_forward_committed(transaction_id, record)
            print(
                f"recovered_release_candidate_transaction={transaction_id}:committed",
                file=sys.stderr,
            )
        return True

    if not targets:
        fail("no publication targets were provided")
    for index, target in enumerate(targets):
        if target == Path(target.anchor):
            fail("filesystem root cannot be used as a publication target")
        for other in targets[index + 1 :]:
            if target in other.parents or other in target.parents:
                fail(f"publication targets cannot contain one another: {target}, {other}")
    for target in targets:
        reject_lexical_symlinks(target)

    def validate_current_candidate() -> tuple[
        dict[str, str],
        dict[str, str] | None,
        dict[Path, tuple[str, object]],
        str,
    ]:
        reject_lexical_symlinks(candidate)
        if validator_path is not None:
            reject_lexical_symlinks(validator_path)
            if validator_path.is_symlink() or not validator_path.is_file():
                fail(f"private governed validator is unavailable: {validator_path}")
        files = inventory(candidate / "files")
        proof = (
            inventory(candidate / "proof" / "build-provenance" / "v1")
            if validator_enabled
            else None
        )
        managed_snapshots = {
            relative: managed_path_snapshot(candidate, relative)
            for relative in exact_managed_paths
        }
        commitment = release_commitment(
            candidate,
            proof_required=validator_enabled,
        )
        if validator_path is not None:
            candidate_validation = subprocess.run(
                [sys.executable, "-I", str(validator_path), str(candidate)],
                capture_output=True,
                text=True,
                check=False,
            )
            if candidate_validation.returncode != 0:
                fail(
                    "private governed validator rejected the sealed candidate: "
                    f"{candidate_validation.stderr.strip() or candidate_validation.stdout.strip()}"
                )
        return files, proof, managed_snapshots, commitment

    if os.environ.get(
        "CHUMMER_RELEASE_TRANSACTION_TEST_PAUSE_BEFORE_PARENT_OPEN",
        "",
    ).strip().lower() in {"1", "true", "yes", "on"}:
        os.kill(os.getpid(), signal.SIGSTOP)

    def open_directory_chain(path: Path, *, create: bool) -> int:
        if not path.is_absolute() or path == Path(path.anchor):
            fail(f"publication target parent must be a non-root absolute path: {path}")
        descriptor = os.open(
            path.anchor,
            os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
        )
        try:
            for part in path.parts[1:]:
                if part in {"", ".", ".."}:
                    fail(f"publication target parent has an unsafe component: {path}")
                try:
                    child_descriptor = os.open(
                        part,
                        os.O_RDONLY
                        | os.O_DIRECTORY
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=descriptor,
                    )
                except FileNotFoundError:
                    if not create:
                        raise
                    os.mkdir(part, mode=0o755, dir_fd=descriptor)
                    os.fsync(descriptor)
                    child_descriptor = os.open(
                        part,
                        os.O_RDONLY
                        | os.O_DIRECTORY
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=descriptor,
                    )
                except OSError as exc:
                    if exc.errno == errno.ELOOP:
                        fail(
                            "publication target parent contains a symlink component: "
                            f"{path}"
                        )
                    raise
                os.close(descriptor)
                descriptor = child_descriptor
            return descriptor
        except BaseException:
            os.close(descriptor)
            raise

    parent_handles: dict[str, dict[str, object]] = {}
    target_contexts: list[dict[str, object]] = []
    target_parents = sorted(
        {target.parent for target in targets},
        key=lambda path: os.path.normcase(str(path)),
    )

    def ensure_parent_handle(parent: Path, *, create: bool) -> bool:
        key = os.path.normcase(str(parent))
        if key in parent_handles:
            return True
        try:
            descriptor = open_directory_chain(parent, create=create)
        except FileNotFoundError:
            if not create:
                return False
            raise
        except OSError as exc:
            fail(f"publication target parent is unavailable: {parent} ({exc})")
        try:
            opened = os.fstat(descriptor)
            if not stat.S_ISDIR(opened.st_mode):
                fail(f"publication target parent is not a directory: {parent}")
            try:
                fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                fail(f"another publisher holds the target parent lock: {parent}")
            anchor = Path(f"/proc/self/fd/{descriptor}")
            anchor_metadata = anchor.stat()
            if (anchor_metadata.st_dev, anchor_metadata.st_ino) != (
                opened.st_dev,
                opened.st_ino,
            ):
                fail(f"descriptor-anchored target parent is unavailable: {parent}")
            context = {
                "parent": parent,
                "fd": descriptor,
                "opened": opened,
                "anchor": anchor,
            }
            assert_parent_identity(context)
            parent_handles[key] = context
        except BaseException:
            os.close(descriptor)
            raise
        return True

    def refresh_target_contexts() -> None:
        target_contexts[:] = []
        for target in targets:
            parent_context = parent_handles.get(
                os.path.normcase(str(target.parent))
            )
            if parent_context is not None:
                target_contexts.append({**parent_context, "target": target})

    transaction_id = uuid.uuid4().hex
    fault_after_commits = int(
        os.environ.get("CHUMMER_RELEASE_TRANSACTION_FAULT_AFTER_COMMITS", "0") or "0"
    )
    hard_exit_after_renames = int(
        os.environ.get("CHUMMER_RELEASE_TRANSACTION_HARD_EXIT_AFTER_RENAMES", "0") or "0"
    )
    hard_exit_phase = (
        os.environ.get("CHUMMER_RELEASE_TRANSACTION_HARD_EXIT_PHASE", "")
        .strip()
        .lower()
    )
    journal_durable = False
    commit_marker_durable = False
    record: dict[str, object] | None = None

    def abort_transaction(signum: int, _frame: object) -> None:
        raise RuntimeError(f"received signal {signum} during release cutover")

    for signal_number in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signal_number, abort_transaction)

    try:
        for parent in target_parents:
            ensure_parent_handle(parent, create=False)
        refresh_target_contexts()
        unfinished_transactions = discover_unfinished_transactions()
        recovered_transaction = False
        if unfinished_transactions:
            if len(unfinished_transactions) > 1:
                fail(
                    "multiple unfinished release transactions were found for the "
                    f"requested targets: {sorted(unfinished_transactions)}"
                )
            discovered_transaction_id, discovered_payloads = next(
                iter(unfinished_transactions.items())
            )
            validate_journal_payloads(
                discovered_transaction_id,
                discovered_payloads,
            )
            for parent in target_parents:
                ensure_parent_handle(parent, create=True)
            refresh_target_contexts()
            recovered_transaction = recover_unfinished_transactions(
                unfinished_transactions
            )
        if (
            recovered_transaction
            and os.environ.get(
                "CHUMMER_RELEASE_TRANSACTION_STOP_AFTER_RECOVERY",
                "",
            ).strip().lower()
            in {"1", "true", "yes", "on"}
        ):
            print("release_candidate_transaction_recovery_only=pass")
            return

        (
            candidate_files,
            candidate_proof,
            candidate_managed_snapshots,
            candidate_commitment,
        ) = validate_current_candidate()

        for parent in target_parents:
            ensure_parent_handle(parent, create=True)
        refresh_target_contexts()
        if not unfinished_transactions:
            late_unfinished_transactions = discover_unfinished_transactions()
            if late_unfinished_transactions:
                recovered_transaction = recover_unfinished_transactions(
                    late_unfinished_transactions
                )
                if (
                    recovered_transaction
                    and os.environ.get(
                        "CHUMMER_RELEASE_TRANSACTION_STOP_AFTER_RECOVERY",
                        "",
                    ).strip().lower()
                    in {"1", "true", "yes", "on"}
                ):
                    print("release_candidate_transaction_recovery_only=pass")
                    return

        incumbent_targets: list[str] = []
        for context in target_contexts:
            target = context["target"]
            assert isinstance(target, Path)
            target_path, stage, backup = transaction_paths(context, transaction_id)
            if path_exists(stage) or path_exists(backup):
                fail(f"transaction path collision beside target: {target}")
            if path_exists(target_path):
                if target_path.is_symlink() or not target_path.is_dir():
                    fail(f"publication target is not a regular directory: {target}")
                incumbent_targets.append(str(target))
                shutil.copytree(
                    target_path,
                    stage,
                    symlinks=True,
                    copy_function=shutil.copy2,
                )
            else:
                descriptor = context["fd"]
                assert isinstance(descriptor, int)
                os.mkdir(stage.name, mode=0o755, dir_fd=descriptor)
            apply_candidate(stage)
            fsync_tree(stage)
            if inventory(stage / "files") != candidate_files:
                fail(f"staged target file bytes differ: {target}")
            staged_proof = stage / "proof" / "build-provenance" / "v1"
            if candidate_proof is not None:
                if inventory(staged_proof) != candidate_proof:
                    fail(f"staged target proof bytes differ: {target}")
            elif staged_proof.exists() or staged_proof.is_symlink():
                fail(f"staged target retained stale build provenance: {target}")
            for name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
                if (stage / name).read_bytes() != (candidate / name).read_bytes():
                    fail(f"staged target manifest bytes differ: {target / name}")
            for relative, expected in candidate_managed_snapshots.items():
                if managed_path_snapshot(stage, relative) != expected:
                    fail(f"staged target managed path differs: {target / relative}")
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            if validator_path is not None:
                stage_validation = subprocess.run(
                    [sys.executable, "-I", str(validator_path), str(stage)],
                    capture_output=True,
                    text=True,
                    check=False,
                    pass_fds=(descriptor,),
                )
                if stage_validation.returncode != 0:
                    fail(
                        f"private governed validator rejected staged target {target}: "
                        f"{stage_validation.stderr.strip() or stage_validation.stdout.strip()}"
                    )
            if (
                release_commitment(
                    stage,
                    proof_required=validator_enabled,
                )
                != candidate_commitment
            ):
                fail(f"staged target commitment differs from candidate: {target}")
            os.fsync(descriptor)
            assert_parent_identity(context)

        record = {
            "schema": "chummer.release_candidate.transaction.v1",
            "transactionId": transaction_id,
            "state": "prepared",
            "targets": [str(target) for target in targets],
            "incumbentTargets": incumbent_targets,
            "candidateCommitment": candidate_commitment,
            "validatorEnabled": validator_enabled,
        }
        for context in target_contexts:
            atomic_write_journal(context, transaction_id, record)
            journal_durable = True

        activated_count = 0
        for context in target_contexts:
            target = context["target"]
            assert isinstance(target, Path)
            target_path, stage, backup = transaction_paths(context, transaction_id)
            if str(target) in incumbent_targets:
                sibling_replace(context, target.name, backup.name)
                if hard_exit_phase == "after-backup":
                    os._exit(92)
            try:
                sibling_replace(context, stage.name, target.name)
            except Exception:
                if path_exists(backup) and not path_exists(target_path):
                    sibling_replace(context, backup.name, target.name)
                raise
            activated_count += 1
            if hard_exit_after_renames > 0 and activated_count >= hard_exit_after_renames:
                os._exit(93)
            if fault_after_commits > 0 and activated_count >= fault_after_commits:
                fail(f"fault injection after {activated_count} committed target(s)")

        for context in target_contexts:
            target = context["target"]
            assert isinstance(target, Path)
            target_path, _, _ = transaction_paths(context, transaction_id)
            if inventory(target_path / "files") != candidate_files:
                fail(f"committed target file bytes differ: {target}")
            committed_proof = target_path / "proof" / "build-provenance" / "v1"
            if candidate_proof is not None:
                if inventory(committed_proof) != candidate_proof:
                    fail(f"committed target proof bytes differ: {target}")
            elif committed_proof.exists() or committed_proof.is_symlink():
                fail(f"committed target retained stale build provenance: {target}")
            for name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
                if (target_path / name).read_bytes() != (candidate / name).read_bytes():
                    fail(f"committed target manifest bytes differ: {target / name}")
            for relative, expected in candidate_managed_snapshots.items():
                if managed_path_snapshot(target_path, relative) != expected:
                    fail(f"committed target managed path differs: {target / relative}")
            if (
                release_commitment(
                    target_path,
                    proof_required=validator_enabled,
                )
                != candidate_commitment
            ):
                fail(f"committed target commitment differs from candidate: {target}")
            assert_parent_identity(context)

        record["state"] = "committed"
        for context in target_contexts:
            atomic_write_journal(context, transaction_id, record)
        commit_marker_durable = True
        if hard_exit_phase == "after-commit-marker":
            os._exit(94)
        roll_forward_committed(transaction_id, record)
    except BaseException as exc:
        recovery_outcome = "failed safely"
        try:
            if record is not None and commit_marker_durable:
                roll_forward_committed(transaction_id, record)
                recovery_outcome = "completed the durable commit"
            elif record is not None and journal_durable:
                rollback_prepared(transaction_id, record)
                recovery_outcome = "rolled back safely"
            else:
                for context in target_contexts:
                    _, stage, backup = transaction_paths(context, transaction_id)
                    remove_path(stage)
                    if path_exists(backup):
                        fail(f"unexpected unjournaled transaction backup remains: {backup}")
                    descriptor = context["fd"]
                    assert isinstance(descriptor, int)
                    os.fsync(descriptor)
        except BaseException as recovery_exc:
            print(
                "Transactional release candidate recovery failed closed: "
                f"{recovery_exc}",
                file=sys.stderr,
            )
        print(
            f"Transactional release candidate cutover {recovery_outcome}: {exc}",
            file=sys.stderr,
        )
        raise SystemExit(1)
    else:
        print(f"transactional_release_candidate_targets={len(targets)}")
    finally:
        for context in parent_handles.values():
            descriptor = context["fd"]
            assert isinstance(descriptor, int)
            os.close(descriptor)


COMMANDS = {
    "reject-symlinks": command_reject_symlinks,
    "resolve-output": command_resolve_output,
    "rewrite-stage-paths": command_rewrite_stage_paths,
    "persist-handoff": command_persist_handoff,
    "publish-stage-only": command_publish_stage_only,
    "compare-validator": command_compare_validator,
    "classify-provenance": command_classify_provenance,
    "copy-tree": command_copy_tree,
    "stage-validator": command_stage_validator,
    "compare-trees": command_compare_trees,
    "verify-mac-identity": command_verify_mac_identity,
    "verify-shelf": command_verify_shelf,
    "preflight-managed": command_preflight_managed,
    "transaction": command_transaction,
}


def main() -> None:
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        die(
            "usage: release_candidate_fs.py "
            f"<{'|'.join(sorted(COMMANDS))}> [arguments ...]"
        )
    COMMANDS[sys.argv[1]](sys.argv[2:])


if __name__ == "__main__":
    main()
