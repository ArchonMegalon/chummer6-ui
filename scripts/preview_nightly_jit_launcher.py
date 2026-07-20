#!/usr/bin/env python3
"""Launch one governed disposable runner for the preview candidate export.

The host is the authority boundary.  It snapshots only the eight candidate
files, verifies them with the committed exporter contract, dispatches the
fixed workflow at the exact remote ``main`` commit, and gives one JIT runner
only the read-only snapshot and an ephemeral read-only JIT-config volume.
"""

from __future__ import annotations

import argparse
import base64
import dataclasses
import datetime as dt
import hashlib
import io
import json
import math
import os
import re
import secrets
import signal
import stat
import subprocess
import sys
import tarfile
import tempfile
import time
from pathlib import Path, PurePosixPath
from types import ModuleType
from typing import Any, Iterable


REPOSITORY = "ArchonMegalon/chummer6-ui"
ORIGIN_URL = "https://github.com/ArchonMegalon/chummer6-ui.git"
DEFAULT_BRANCH = "main"
SOURCE_REF = "refs/heads/main"
WORKFLOW_PATH = ".github/workflows/preview-nightly-candidate-export.yml"
WORKFLOW_FILE = "preview-nightly-candidate-export.yml"
EXPORT_JOB_NAME = "Export exact candidate bytes"
RUNNER_LABEL_PREFIX = "chummer-preview-nightly-export-"
RUNNER_NAME_PREFIX = "chummer-preview-nightly-jit-"
CONTAINER_PREFIX = "chummer-preview-nightly-jit-"
CONFIG_HOLDER_PREFIX = "chummer-preview-nightly-jit-config-holder-"
CONFIG_VERIFY_PREFIX = "chummer-preview-nightly-jit-config-verify-"
OWNER_LABEL = "run.chummer.preview-nightly-jit"
NONCE_LABEL = "run.chummer.preview-nightly-jit.nonce"
RUNNER_GROUP_ID = 1
RUNNER_UID = 1001
RUNNER_GID = 1001
IMAGE = (
    "ghcr.io/actions/actions-runner@sha256:"
    "f2387135856decdecbf780a2bfbc9debe9c2dffd742f150302444b3775474681"
)
IMAGE_DIGEST = "sha256:f2387135856decdecbf780a2bfbc9debe9c2dffd742f150302444b3775474681"
EXPECTED_IMAGE_USER = "runner"
EXPECTED_IMAGE_WORKDIR = "/home/runner"
RECEIPT_CONTRACT = "chummer6-ui.preview-nightly-jit-launch"
RECEIPT_VERSION = 1
CLEANUP_NOTE_PREFIX = "governed cleanup failures (redacted): "
MANUAL_CLEANUP_NOTE_PREFIX = "manual cleanup required (bounded): "
SAFE_TEMP_PARENT = Path("/tmp")
ALLOWED_JIT_CONFIG_FILES = (
    ".runner",
    ".credentials",
    ".credentials_rsaparams",
)
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
DOCKER_ID_RE = re.compile(r"^[0-9a-f]{64}$")
DOCKER_VOLUME_RE = re.compile(r"^[0-9a-f]{64}$")
NONCE_RE = re.compile(r"^[a-z0-9]{12,64}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
JIT_CONFIG_RE = re.compile(r"^[A-Za-z0-9+/]{98,199998}={0,2}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
EXPECTED_JOB_LABELS = frozenset(("self-hosted", "linux", "x64"))
JIT_CONFIG_LIMIT = 200_000
PROCESS_REAP_SECONDS = 5
SUPPLY_CHAIN_MODULE_NAME = "chummer6_ui_preview_supply_chain_contract"
EXPECTED_CONTENT_DIRECTORIES = (
    "files",
    "release-evidence",
    "release-evidence/sbom",
    "release-evidence/vulnerability",
)
SEED_WRITER_COMMAND = (
    "set -euo pipefail; umask 077; chmod 0700 /jit-seed; "
    "test -d /jit-seed; test -z \"$(find /jit-seed -mindepth 1 -maxdepth 1 -print -quit)\"; "
    "tar --extract --file=- --directory=/jit-seed --keep-old-files "
    "--no-same-owner --no-same-permissions; "
    "test \"$(find /jit-seed -mindepth 1 -maxdepth 1 -type f -printf '%f\\n' | LC_ALL=C sort)\" "
    "= $'.credentials\\n.credentials_rsaparams\\n.ownership-marker\\n.runner'; "
    "test \"$(find /jit-seed -mindepth 1 -maxdepth 1 ! -type f -print -quit)\" = ''; "
    "for name in .ownership-marker .runner .credentials .credentials_rsaparams; do "
    "chmod 0600 \"/jit-seed/$name\"; chown 1001:1001 \"/jit-seed/$name\"; "
    "test \"$(stat -c '%a:%u:%g:%h' \"/jit-seed/$name\")\" = 600:1001:1001:1; done; "
    "sync -f /jit-seed; chown 1001:1001 /jit-seed"
)
SEED_VERIFY_COMMAND = (
    "set -euo pipefail; cd /jit-seed; "
    "test \"$(find . -mindepth 1 -maxdepth 1 -type f -printf '%f\\n' | LC_ALL=C sort)\" "
    "= $'.credentials\\n.credentials_rsaparams\\n.ownership-marker\\n.runner'; "
    "test \"$(find . -mindepth 1 -maxdepth 1 ! -type f -print -quit)\" = ''; "
    "for name in .ownership-marker .runner .credentials .credentials_rsaparams; do "
    "test \"$(stat -c '%a:%u:%g:%h' \"$name\")\" = 600:1001:1001:1; done; "
    "sha256sum --check --strict --status -"
)
RUNNER_ENTRYPOINT_COMMAND = (
    "set -euo pipefail; umask 077; "
    "test \"$(find /jit-seed -mindepth 1 -maxdepth 1 -type f -printf '%f\\n' | LC_ALL=C sort)\" "
    "= $'.credentials\\n.credentials_rsaparams\\n.ownership-marker\\n.runner'; "
    "test \"$(find /jit-seed -mindepth 1 -maxdepth 1 ! -type f -print -quit)\" = ''; "
    "for name in .runner .credentials .credentials_rsaparams; do "
    "source=\"/jit-seed/$name\"; target=\"/home/runner/$name\"; "
    "test -f \"$source\"; test ! -L \"$source\"; "
    "test \"$(stat -c '%a:%u:%g:%h' \"$source\")\" = 600:1001:1001:1; "
    "test ! -e \"$target\"; "
    "(set -o noclobber; : > \"$target\"); chmod 0600 \"$target\"; "
    "cat \"$source\" > \"$target\"; sync -f \"$target\"; cmp -s \"$source\" \"$target\"; "
    "test \"$(stat -c '%a:%u:%g:%h' \"$target\")\" = 600:1001:1001:1; done; "
    "sync -f /home/runner; "
    "exec /home/runner/run.sh"
)


class LaunchError(RuntimeError):
    """A fail-closed launcher contract violation."""


class DispatchIndeterminate(LaunchError):
    """The dispatch request may have been accepted but its response was lost."""


class GovernedTermination(BaseException):
    """A catchable host termination that must unwind through governed cleanup."""

    def __init__(self, signal_number: int):
        super().__init__(f"received {signal.Signals(signal_number).name}")
        self.signal_number = signal_number


def fail(message: str) -> None:
    raise LaunchError(message)


def exact_string(value: object, label: str) -> str:
    if not isinstance(value, str) or "\x00" in value:
        fail(f"{label} must be an exact string")
    return value


def require_match(value: object, pattern: re.Pattern[str], label: str) -> str:
    text = exact_string(value, label)
    if pattern.fullmatch(text) is None:
        fail(f"{label} is not canonical")
    return text


def require_positive_integer(value: object, label: str) -> int:
    if isinstance(value, int) and not isinstance(value, bool) and value > 0:
        return value
    text = exact_string(value, label) if isinstance(value, str) else ""
    if POSITIVE_INTEGER_RE.fullmatch(text) is None:
        fail(f"{label} must be a positive integer")
    return int(text)


def exact_unique_labels(value: object, label: str) -> frozenset[str]:
    if not isinstance(value, list):
        fail(f"{label} must be an exact label list")
    labels: list[str] = []
    for row in value:
        if isinstance(row, dict):
            name = exact_string(row.get("name"), label)
        else:
            name = exact_string(row, label)
        if not name or any(ord(character) < 32 or ord(character) == 127 for character in name):
            fail(f"{label} contains a noncanonical label")
        labels.append(name)
    if len(labels) != len(set(labels)):
        fail(f"{label} contains duplicate labels")
    return frozenset(labels)


def add_cleanup_notes(
    primary: BaseException, errors: list[tuple[str, BaseException]]
) -> None:
    if errors:
        primary.add_note(cleanup_failure_note(errors))


def add_manual_cleanup_note(primary: BaseException, detail: str) -> None:
    safe_detail = re.sub(r"[^A-Za-z0-9 _=.-]", "_", detail)[:400]
    primary.add_note((MANUAL_CLEANUP_NOTE_PREFIX + safe_detail)[:512])


def command_environment(kind: str) -> dict[str, str]:
    if kind == "gh":
        environment = dict(os.environ)
        for key in tuple(environment):
            if (
                (key.startswith("GH_") and key != "GH_TOKEN")
                or key == "GITHUB_ENTERPRISE_TOKEN"
            ):
                environment.pop(key, None)
        return environment
    if kind not in {"docker", "local"}:
        fail("unknown child command environment")
    allowed = {
        "PATH", "HOME", "LANG", "LC_ALL", "LC_CTYPE", "XDG_RUNTIME_DIR",
        "DOCKER_HOST", "DOCKER_CONTEXT",
    }
    if kind == "local":
        allowed -= {"DOCKER_HOST", "DOCKER_CONTEXT"}
    return {key: value for key, value in os.environ.items() if key in allowed}


def run_checked(
    args: Iterable[str],
    *,
    input_text: str | None = None,
    timeout: float = 30,
    kind: str,
) -> str:
    command = [exact_string(arg, "command argument") for arg in args]
    try:
        result = subprocess.run(
            command,
            input=input_text,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            shell=False,
            check=False,
            timeout=timeout,
            env=command_environment(kind),
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        fail(f"{command[0]} command could not complete: {type(exc).__name__}")
    if result.returncode != 0:
        fail(f"{command[0]} command failed with exit code {result.returncode}")
    return result.stdout


def gh_json(
    endpoint: str,
    *,
    method: str = "GET",
    payload: dict[str, Any] | None = None,
    timeout: float = 30,
) -> Any:
    endpoint = exact_string(endpoint, "GitHub endpoint")
    repository_prefix = f"repos/{REPOSITORY}"
    if (
        endpoint != "user"
        and (
            not (
                endpoint == repository_prefix
                or endpoint.startswith(repository_prefix + "/")
            )
            or ".." in endpoint
            or re.fullmatch(r"[A-Za-z0-9_./?=&-]+", endpoint) is None
        )
    ):
        fail("GitHub endpoint is outside the fixed authority boundary")
    args = [
        "gh", "api", "--hostname", "github.com",
        "-H", "Accept: application/vnd.github+json",
        "-H", "X-GitHub-Api-Version: 2026-03-10",
    ]
    if method != "GET":
        if method not in {"POST", "DELETE"}:
            fail("unsupported GitHub API method")
        args.extend(("--method", method))
    args.append(endpoint)
    input_text = None
    if payload is not None:
        args.extend(("--input", "-"))
        input_text = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    raw = run_checked(args, input_text=input_text, timeout=timeout, kind="gh")
    if not raw.strip():
        return None
    try:
        return json.loads(raw)
    except json.JSONDecodeError as exc:
        fail(f"GitHub API returned invalid JSON: {exc.msg}")


def require_absolute_directory_no_links(path: Path, label: str) -> Path:
    if not path.is_absolute() or path != Path(os.path.normpath(str(path))):
        fail(f"{label} must be a canonical absolute path")
    current = Path(path.anchor)
    for part in path.parts[1:]:
        if part in {"", ".", ".."}:
            fail(f"{label} is not canonical")
        current /= part
        try:
            metadata = os.lstat(current)
        except OSError as exc:
            fail(f"{label} cannot be inspected: {exc}")
        if stat.S_ISLNK(metadata.st_mode):
            fail(f"{label} cannot traverse symlinks")
    if not stat.S_ISDIR(os.lstat(path).st_mode):
        fail(f"{label} must be a directory")
    if path == Path(path.anchor):
        fail(f"{label} cannot be a filesystem root")
    return path


def sha256_descriptor(descriptor: int) -> str:
    digest = hashlib.sha256()
    os.lseek(descriptor, 0, os.SEEK_SET)
    while True:
        chunk = os.read(descriptor, 1024 * 1024)
        if not chunk:
            break
        digest.update(chunk)
    os.lseek(descriptor, 0, os.SEEK_SET)
    return digest.hexdigest()


@dataclasses.dataclass(frozen=True)
class HeldSource:
    relative: str
    descriptor: int
    directory_descriptor: int
    basename: str
    identity: tuple[int, int, int, int, int, int]
    sha256: str


def stat_identity(metadata: os.stat_result) -> tuple[int, int, int, int, int, int]:
    return (
        metadata.st_dev, metadata.st_ino, metadata.st_mode, metadata.st_size,
        metadata.st_mtime_ns, metadata.st_ctime_ns,
    )


def open_held_sources(
    stage_root: Path, content_paths: tuple[str, ...]
) -> tuple[list[HeldSource], dict[str, int]]:
    nofollow = getattr(os, "O_NOFOLLOW", 0)
    root_fd = os.open(stage_root, os.O_RDONLY | os.O_DIRECTORY | nofollow)
    opened_directories = {".": root_fd}
    held: list[HeldSource] = []
    try:
        for relative in content_paths:
            if not isinstance(relative, str):
                fail("exporter content path is not canonical and portable")
            portable = PurePosixPath(relative)
            if (
                portable.is_absolute()
                or portable.as_posix() != relative
                or any(part in {"", ".", ".."} for part in portable.parts)
                or "\\" in relative
            ):
                fail("exporter content path is not canonical and portable")
        expected_parents = {
            parent
            for relative in content_paths
            for parent in (Path(relative).parent.as_posix(),)
            if parent != "."
        }
        if expected_parents != set(EXPECTED_CONTENT_DIRECTORIES):
            fail("exporter content directories differ from the exact candidate boundary")
        for relative_directory in sorted(
            EXPECTED_CONTENT_DIRECTORIES,
            key=lambda value: (len(Path(value).parts), value),
        ):
            portable = Path(relative_directory)
            parent = portable.parent.as_posix()
            parent_key = "." if parent == "." else parent
            parent_fd = opened_directories.get(parent_key)
            if parent_fd is None:
                fail("exporter content directory hierarchy is incomplete")
            opened_directories[relative_directory] = os.open(
                portable.name,
                os.O_RDONLY | os.O_DIRECTORY | nofollow,
                dir_fd=parent_fd,
            )
        for relative in content_paths:
            parts = Path(relative).parts
            if len(parts) == 1:
                directory_fd, basename = root_fd, parts[0]
            else:
                parent = Path(relative).parent.as_posix()
                directory_fd = opened_directories.get(parent)
                basename = parts[-1] if parts else ""
                if directory_fd is None:
                    fail("exporter content path escaped the exact candidate boundary")
            descriptor = os.open(basename, os.O_RDONLY | nofollow, dir_fd=directory_fd)
            metadata = os.fstat(descriptor)
            if not stat.S_ISREG(metadata.st_mode):
                os.close(descriptor)
                fail(f"prepared candidate entry is not a regular file: {relative}")
            held.append(
                HeldSource(
                    relative, descriptor, directory_fd, basename,
                    stat_identity(metadata), sha256_descriptor(descriptor),
                )
            )
        return held, opened_directories
    except Exception:
        for source in held:
            os.close(source.descriptor)
        for descriptor in reversed(opened_directories.values()):
            os.close(descriptor)
        raise


def validate_held_sources(held: list[HeldSource]) -> None:
    for source in held:
        current = os.fstat(source.descriptor)
        path_now = os.stat(
            source.basename, dir_fd=source.directory_descriptor, follow_symlinks=False
        )
        if stat_identity(current) != source.identity:
            fail(f"held candidate source changed: {source.relative}")
        if (path_now.st_dev, path_now.st_ino) != source.identity[:2]:
            fail(f"candidate source path identity changed: {source.relative}")
        if sha256_descriptor(source.descriptor) != source.sha256:
            fail(f"held candidate bytes changed: {source.relative}")


def validate_held_directories(
    stage_root: Path,
    descriptors: dict[str, int],
    identities: dict[str, tuple[int, int, int, int, int, int]],
) -> None:
    expected = {".", *EXPECTED_CONTENT_DIRECTORIES}
    if set(descriptors) != expected or set(identities) != expected:
        fail("candidate directory authority is incomplete")
    for relative in sorted(expected, key=lambda value: (len(Path(value).parts), value)):
        descriptor = descriptors[relative]
        held_now = os.fstat(descriptor)
        if relative == ".":
            path_now = os.stat(stage_root, follow_symlinks=False)
        else:
            portable = Path(relative)
            parent = portable.parent.as_posix()
            parent_key = "." if parent == "." else parent
            path_now = os.stat(
                portable.name,
                dir_fd=descriptors[parent_key],
                follow_symlinks=False,
            )
        if (
            stat_identity(held_now) != identities[relative]
            or stat_identity(path_now) != identities[relative]
            or not stat.S_ISDIR(held_now.st_mode)
        ):
            fail("prepared candidate directory identity changed")


def copy_held_source(source: HeldSource, target: Path) -> None:
    descriptor = os.open(
        target,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    digest = hashlib.sha256()
    try:
        os.lseek(source.descriptor, 0, os.SEEK_SET)
        while True:
            chunk = os.read(source.descriptor, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
            view = memoryview(chunk)
            while view:
                written = os.write(descriptor, view)
                view = view[written:]
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
        os.lseek(source.descriptor, 0, os.SEEK_SET)
    if digest.hexdigest() != source.sha256:
        fail(f"candidate source changed while copied: {source.relative}")


@dataclasses.dataclass(frozen=True)
class CandidateIdentity:
    root: Path
    version: str
    manifest_sha256: str
    content: tuple[dict[str, Any], ...]


def materialize_candidate_subset(
    stage_root: Path,
    subset_root: Path,
    exporter: ModuleType,
    source_commit: str,
) -> CandidateIdentity:
    stage_root = require_absolute_directory_no_links(stage_root, "prepared stage root")
    if not subset_root.is_absolute() or subset_root.exists() or subset_root.is_symlink():
        fail("private candidate subset must be a new absolute path")
    subset_root.mkdir(mode=0o700)
    for relative_directory in EXPECTED_CONTENT_DIRECTORIES:
        (subset_root / relative_directory).mkdir(mode=0o700)
    content_paths = tuple(exporter.CONTENT_PATHS)
    held, directory_descriptors = open_held_sources(stage_root, content_paths)
    directory_identities = {
        relative: stat_identity(os.fstat(descriptor))
        for relative, descriptor in directory_descriptors.items()
    }
    try:
        validate_held_directories(stage_root, directory_descriptors, directory_identities)
        validate_held_sources(held)
        for source in held:
            copy_held_source(source, subset_root / source.relative)
        validate_held_sources(held)
        validate_held_directories(stage_root, directory_descriptors, directory_identities)
        exporter.require_exact_tree(subset_root, content_paths, "private candidate subset")
        manifest_path = subset_root / exporter.MANIFEST_PATH
        manifest_sha = exporter.sha256_file(manifest_path)
        manifest = exporter.read_json(manifest_path, "prepared candidate manifest")
        version = require_match(manifest.get("version"), VERSION_RE, "candidate version")
        exporter.validate_candidate_root(
            subset_root, version, manifest_sha, source_commit
        )
        validate_held_sources(held)
        validate_held_directories(stage_root, directory_descriptors, directory_identities)
        content = tuple(exporter.content_rows(subset_root))
        for relative in content_paths:
            (subset_root / relative).chmod(0o444)
        for relative_directory in sorted(
            EXPECTED_CONTENT_DIRECTORIES,
            key=lambda value: (len(Path(value).parts), value),
            reverse=True,
        ):
            (subset_root / relative_directory).chmod(0o555)
        subset_root.chmod(0o555)
        return CandidateIdentity(subset_root, version, manifest_sha, content)
    finally:
        for source in held:
            os.close(source.descriptor)
        for descriptor in reversed(directory_descriptors.values()):
            os.close(descriptor)


@dataclasses.dataclass(frozen=True)
class PrivateTree:
    path: Path
    device: int
    inode: int
    owner: int


def create_private_tree() -> PrivateTree:
    require_absolute_directory_no_links(SAFE_TEMP_PARENT, "fixed private-tree parent")
    path = Path(
        tempfile.mkdtemp(prefix="chummer-preview-nightly-jit-", dir=SAFE_TEMP_PARENT)
    )
    path.chmod(0o700)
    metadata = os.lstat(path)
    return PrivateTree(path, metadata.st_dev, metadata.st_ino, metadata.st_uid)


def remove_private_tree(identity: PrivateTree) -> None:
    if not identity.path.exists() and not identity.path.is_symlink():
        return
    metadata = os.lstat(identity.path)
    if (
        stat.S_ISLNK(metadata.st_mode)
        or not stat.S_ISDIR(metadata.st_mode)
        or (metadata.st_dev, metadata.st_ino, metadata.st_uid)
        != (identity.device, identity.inode, identity.owner)
        or identity.owner != os.geteuid()
    ):
        fail("private-tree cleanup identity changed; refusing removal")
    for current, directories, files in os.walk(identity.path, topdown=False, followlinks=False):
        current_path = Path(current)
        current_metadata = os.lstat(current_path)
        if (
            not stat.S_ISDIR(current_metadata.st_mode)
            or stat.S_ISLNK(current_metadata.st_mode)
            or current_metadata.st_uid != identity.owner
        ):
            fail("private-tree cleanup found an unsafe current directory")
        current_path.chmod(0o700)
        for name in files:
            path = current_path / name
            item = os.lstat(path)
            if not stat.S_ISREG(item.st_mode) or item.st_uid != identity.owner:
                fail("private-tree cleanup found an unsafe file")
            path.unlink()
        for name in directories:
            path = current_path / name
            item = os.lstat(path)
            if not stat.S_ISDIR(item.st_mode) or stat.S_ISLNK(item.st_mode) or item.st_uid != identity.owner:
                fail("private-tree cleanup found an unsafe directory")
            path.chmod(0o700)
            path.rmdir()
    identity.path.rmdir()


@dataclasses.dataclass(frozen=True)
class LocalAuthority:
    commit: str
    exporter_source: bytes
    supply_chain_source: bytes = b""


def git_blob_sha1(content: bytes) -> str:
    header = f"blob {len(content)}\0".encode("ascii")
    return hashlib.sha1(header + content, usedforsecurity=False).hexdigest()


def committed_file_snapshot(repo_root: Path, commit: str, relative: str) -> bytes:
    commit = require_match(commit, COMMIT_RE, "trusted snapshot commit")
    path = repo_root / relative
    require_absolute_directory_no_links(path.parent, "trusted source parent")
    descriptor = os.open(
        path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    )
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode) or before.st_size < 1 or before.st_size > 2_000_000:
            fail(f"trusted source is not a bounded regular file: {relative}")
        chunks: list[bytes] = []
        remaining = before.st_size
        while remaining:
            chunk = os.read(descriptor, min(1024 * 1024, remaining))
            if not chunk:
                fail(f"trusted source ended early: {relative}")
            chunks.append(chunk)
            remaining -= len(chunk)
        if os.read(descriptor, 1):
            fail(f"trusted source grew while read: {relative}")
        after = os.fstat(descriptor)
        if stat_identity(before) != stat_identity(after):
            fail(f"trusted source changed while snapshotted: {relative}")
        source = b"".join(chunks)
    finally:
        os.close(descriptor)
    commit_object = run_checked(
        ("git", "-C", str(repo_root), "rev-parse", f"{commit}:{relative}"),
        kind="local",
    ).strip()
    if git_blob_sha1(source) != commit_object:
        fail(f"trusted source snapshot differs from commit: {relative}")
    return source


def require_local_head(repo_root: Path, expected_commit: str, boundary: str) -> None:
    expected_commit = require_match(
        expected_commit, COMMIT_RE, "expected local trusted commit"
    )
    current = require_match(
        run_checked(
            ("git", "-C", str(repo_root), "rev-parse", "HEAD"), kind="local"
        ).strip(),
        COMMIT_RE,
        "current local trusted commit",
    )
    if current != expected_commit:
        fail(f"local trusted commit changed {boundary}")


def verify_committed_local_authority(repo_root: Path) -> LocalAuthority:
    repo_root = require_absolute_directory_no_links(repo_root, "launcher repository")
    shown_root = Path(run_checked(
        ("git", "-C", str(repo_root), "rev-parse", "--show-toplevel"), kind="local"
    ).strip())
    if shown_root != repo_root:
        fail("launcher repository root differs from git authority")
    origin = run_checked(
        ("git", "-C", str(repo_root), "remote", "get-url", "origin"), kind="local"
    ).strip()
    if origin != ORIGIN_URL:
        fail("launcher origin differs from the fixed repository")
    commit = require_match(
        run_checked(("git", "-C", str(repo_root), "rev-parse", "HEAD"), kind="local").strip(),
        COMMIT_RE,
        "local trusted commit",
    )
    require_local_head(repo_root, commit, "before trusted snapshot construction")
    committed_file_snapshot(
        repo_root, commit, "scripts/preview_nightly_jit_launcher.py"
    )
    exporter_source = committed_file_snapshot(
        repo_root, commit, "scripts/preview_nightly_candidate_export.py"
    )
    supply_chain_source = committed_file_snapshot(
        repo_root, commit, "scripts/preview_supply_chain.py"
    )
    require_local_head(repo_root, commit, "after trusted snapshot construction")
    return LocalAuthority(commit, exporter_source, supply_chain_source)


def load_trusted_exporter(
    source: bytes, supply_chain_source: bytes | None = None
) -> ModuleType:
    if not isinstance(source, bytes) or not source:
        fail("trusted exporter snapshot is missing")
    previous_supply_chain = sys.modules.get(SUPPLY_CHAIN_MODULE_NAME)
    if supply_chain_source is not None:
        if not isinstance(supply_chain_source, bytes) or not supply_chain_source:
            fail("trusted supply-chain snapshot is missing")
        supply_chain = ModuleType(SUPPLY_CHAIN_MODULE_NAME)
        supply_chain.__file__ = "<committed-preview-supply-chain-snapshot>"
        try:
            code = compile(
                supply_chain_source,
                supply_chain.__file__,
                "exec",
                dont_inherit=True,
            )
            exec(code, supply_chain.__dict__)
        except Exception as exc:
            fail(
                "trusted supply-chain snapshot could not be loaded: "
                f"{type(exc).__name__}"
            )
        sys.modules[SUPPLY_CHAIN_MODULE_NAME] = supply_chain
    module = ModuleType("preview_nightly_candidate_export")
    module.__file__ = "<committed-preview-nightly-candidate-export-snapshot>"
    sys.modules[module.__name__] = module
    try:
        code = compile(source, module.__file__, "exec", dont_inherit=True)
        exec(code, module.__dict__)
    except Exception as exc:
        if supply_chain_source is not None:
            if previous_supply_chain is None:
                sys.modules.pop(SUPPLY_CHAIN_MODULE_NAME, None)
            else:
                sys.modules[SUPPLY_CHAIN_MODULE_NAME] = previous_supply_chain
        fail(f"trusted exporter snapshot could not be loaded: {type(exc).__name__}")
    return module


@dataclasses.dataclass(frozen=True)
class Authority:
    commit: str
    actor: str
    workflow_id: int


def validate_remote_authority(local_commit: str) -> Authority:
    repository = gh_json(f"repos/{REPOSITORY}")
    if not isinstance(repository, dict):
        fail("repository metadata is missing")
    if repository.get("full_name") != REPOSITORY or repository.get("default_branch") != DEFAULT_BRANCH:
        fail("repository/default-branch authority differs")
    ref = gh_json(f"repos/{REPOSITORY}/git/ref/heads/{DEFAULT_BRANCH}")
    remote_sha = ref.get("object", {}).get("sha") if isinstance(ref, dict) else None
    remote_sha = require_match(remote_sha, COMMIT_RE, "remote main commit")
    if remote_sha != local_commit:
        fail("remote main commit differs from the local trusted commit")
    workflow = gh_json(f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_FILE}")
    if not isinstance(workflow, dict) or workflow.get("path") != WORKFLOW_PATH or workflow.get("state") != "active":
        fail("fixed candidate exporter workflow is not active at its exact path")
    workflow_id = require_positive_integer(workflow.get("id"), "workflow ID")
    user = gh_json("user")
    actor = user.get("login") if isinstance(user, dict) else None
    actor = exact_string(actor, "authenticated GitHub actor")
    if re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?", actor) is None:
        fail("authenticated GitHub actor is not canonical")
    return Authority(remote_sha, actor, workflow_id)


def list_repository_runners() -> list[dict[str, Any]]:
    page = 1
    runners: list[dict[str, Any]] = []
    total: int | None = None
    while True:
        payload = gh_json(f"repos/{REPOSITORY}/actions/runners?per_page=100&page={page}")
        if not isinstance(payload, dict) or not isinstance(payload.get("runners"), list):
            fail("repository runner inventory is invalid")
        if total is None:
            raw_total = payload.get("total_count")
            if type(raw_total) is not int or raw_total < 0:
                fail("runner total must be an exact nonnegative JSON integer")
            total = raw_total
        if len(payload["runners"]) > 100:
            fail("repository runner page exceeds the exact requested size")
        runners.extend(row for row in payload["runners"] if isinstance(row, dict))
        if len(runners) >= total:
            break
        page += 1
        if page > 100:
            fail("repository runner inventory exceeded the bounded page limit")
    if len(runners) != total:
        fail("repository runner inventory count is ambiguous")
    runner_ids = [require_positive_integer(row.get("id"), "runner ID") for row in runners]
    if len(set(runner_ids)) != len(runner_ids):
        fail("repository runner inventory contains duplicate identities")
    for row in runners:
        exact_unique_labels(row.get("labels"), "repository runner labels")
    return runners


def generate_unique_nonce(runners: list[dict[str, Any]]) -> str:
    existing_names = {row.get("name") for row in runners}
    existing_labels = {
        label.get("name")
        for row in runners
        for label in row.get("labels", [])
        if isinstance(label, dict)
    }
    for _ in range(8):
        nonce = secrets.token_hex(12)
        label = RUNNER_LABEL_PREFIX + nonce
        name = RUNNER_NAME_PREFIX + nonce
        if NONCE_RE.fullmatch(nonce) and label not in existing_labels and name not in existing_names:
            return nonce
    fail("could not allocate a unique runner nonce")


def run_jobs(run_id: int) -> list[dict[str, Any]]:
    payload = gh_json(
        f"repos/{REPOSITORY}/actions/runs/{run_id}/jobs?filter=latest&per_page=100&page=1"
    )
    if not isinstance(payload, dict) or not isinstance(payload.get("jobs"), list):
        fail("workflow job inventory is invalid")
    jobs = [row for row in payload["jobs"] if isinstance(row, dict)]
    total = payload.get("total_count")
    if type(total) is not int or total < 0 or total > 100 or total != len(jobs):
        fail("workflow job inventory is incomplete or ambiguous")
    job_ids = [require_positive_integer(row.get("id"), "workflow job ID") for row in jobs]
    if len(set(job_ids)) != len(job_ids):
        fail("workflow job inventory contains duplicate identities")
    return jobs


def dispatch_workflow(candidate: CandidateIdentity, authority: Authority, nonce: str) -> Any:
    payload = {
        "ref": DEFAULT_BRANCH,
        "inputs": {
            "runner_nonce": nonce,
            "candidate_version": candidate.version,
            "candidate_manifest_sha256": candidate.manifest_sha256,
            "expected_source_sha": authority.commit,
            "export_confirmed": True,
        },
    }
    try:
        return gh_json(
            f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_FILE}/dispatches",
            method="POST",
            payload=payload,
        )
    except LaunchError as exc:
        raise DispatchIndeterminate(
            "workflow dispatch response was indeterminate"
        ) from exc


def workflow_run_inventory() -> list[dict[str, Any]]:
    page = 1
    total: int | None = None
    runs: list[dict[str, Any]] = []
    while True:
        payload = gh_json(
            f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_FILE}/runs"
            f"?event=workflow_dispatch&branch=main&per_page=100&page={page}"
        )
        rows = payload.get("workflow_runs") if isinstance(payload, dict) else None
        raw_total = payload.get("total_count") if isinstance(payload, dict) else None
        if (
            not isinstance(rows, list)
            or any(not isinstance(row, dict) for row in rows)
            or len(rows) > 100
            or type(raw_total) is not int
            or raw_total < 0
        ):
            fail("workflow run inventory is incomplete or ambiguous")
        if total is None:
            total = raw_total
        elif raw_total != total:
            fail("workflow run inventory changed during exact pagination")
        runs.extend(rows)
        if len(runs) >= total:
            break
        page += 1
        if page > 100:
            fail("workflow run inventory exceeded the bounded page limit")
    if len(runs) != total:
        fail("workflow run inventory count differs from exact pagination")
    identifiers = [
        require_positive_integer(row.get("id"), "workflow run ID") for row in runs
    ]
    if len(identifiers) != len(set(identifiers)):
        fail("workflow run inventory contains duplicate identities")
    return runs


def workflow_run_baseline() -> frozenset[int]:
    return frozenset(
        require_positive_integer(row.get("id"), "workflow run ID")
        for row in workflow_run_inventory()
    )


def reconciliation_row_matches(
    run: dict[str, Any], authority: Authority, nonce: str
) -> bool:
    actor = run.get("actor", {}).get("login")
    triggering_actor = run.get("triggering_actor", {}).get("login")
    return (
        run.get("display_title") == RUNNER_LABEL_PREFIX + nonce
        and actor == authority.actor
        and triggering_actor == authority.actor
        and exact_run_identity(run, authority)
    )


def reconcile_indeterminate_dispatch(
    baseline: frozenset[int], authority: Authority, nonce: str, deadline: float
) -> tuple[int, dict[str, Any]]:
    reconciliation_deadline = min(deadline, time.monotonic() + 60)
    while time.monotonic() < reconciliation_deadline:
        candidates: list[dict[str, Any]] = []
        for row in workflow_run_inventory():
            identifier = require_positive_integer(row.get("id"), "workflow run ID")
            if identifier not in baseline and reconciliation_row_matches(row, authority, nonce):
                candidates.append(row)
        if len(candidates) > 1:
            error = LaunchError("indeterminate dispatch reconciliation is ambiguous")
            add_manual_cleanup_note(
                error, "multiple nonce-bound post-baseline workflow runs require inspection"
            )
            raise error
        if len(candidates) == 1:
            run_id = require_positive_integer(
                candidates[0].get("id"), "reconciled workflow run ID"
            )
            run = gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}")
            if not isinstance(run, dict):
                fail("reconciled workflow run details are invalid")
            validate_known_run(run, run_id, authority)
            if run.get("display_title") != RUNNER_LABEL_PREFIX + nonce:
                fail("reconciled workflow run title differs from its nonce authority")
            correlated = wait_for_correlated_run(
                run_id, authority, RUNNER_LABEL_PREFIX + nonce, reconciliation_deadline
            )
            return run_id, correlated
        time.sleep(2)
    error = LaunchError("indeterminate dispatch could not be reconciled exactly")
    add_manual_cleanup_note(
        error, "no unique nonce-bound post-baseline workflow run was established"
    )
    raise error


def dispatch_run_id(response: object) -> int:
    if not isinstance(response, dict) or set(response) != {
        "workflow_run_id", "run_url", "html_url"
    }:
        fail("workflow dispatch did not return run details")
    value = response.get("workflow_run_id")
    if type(value) is not int or value < 1:
        fail("dispatched workflow run ID must be a positive JSON integer")
    expected_api_url = f"https://api.github.com/repos/{REPOSITORY}/actions/runs/{value}"
    expected_html_url = f"https://github.com/{REPOSITORY}/actions/runs/{value}"
    if response.get("run_url") != expected_api_url or response.get("html_url") != expected_html_url:
        fail("workflow dispatch returned mismatched run URLs")
    return value


def exact_run_identity(run: dict[str, Any], authority: Authority) -> bool:
    qualified_paths = {
        WORKFLOW_PATH,
        f"{WORKFLOW_PATH}@{DEFAULT_BRANCH}",
        f"{WORKFLOW_PATH}@{SOURCE_REF}",
        f"{WORKFLOW_PATH}@{authority.commit}",
    }
    return (
        run.get("event") == "workflow_dispatch"
        and run.get("head_branch") == DEFAULT_BRANCH
        and run.get("head_sha") == authority.commit
        and run.get("path") in qualified_paths
        and run.get("workflow_id") == authority.workflow_id
    )


def validate_dispatch_details(
    response: object, run_id: int, authority: Authority
) -> dict[str, Any]:
    if dispatch_run_id(response) != run_id:
        fail("persisted workflow run ID differs from dispatch details")
    expected_api_url = f"https://api.github.com/repos/{REPOSITORY}/actions/runs/{run_id}"
    expected_html_url = f"https://github.com/{REPOSITORY}/actions/runs/{run_id}"
    run = gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}")
    if not isinstance(run, dict):
        fail("dispatched workflow run identity is invalid")
    validate_known_run(run, run_id, authority)
    if (
        run.get("url") != expected_api_url
        or run.get("html_url") != expected_html_url
        or run.get("run_attempt") != 1
    ):
        fail("dispatched workflow run details differ from its exact authority")
    return run


def validate_known_run(run: dict[str, Any], run_id: int, authority: Authority) -> None:
    actor = run.get("actor", {}).get("login")
    triggering_actor = run.get("triggering_actor", {}).get("login")
    repository = run.get("repository", {}).get("full_name")
    expected_api_url = f"https://api.github.com/repos/{REPOSITORY}/actions/runs/{run_id}"
    expected_html_url = f"https://github.com/{REPOSITORY}/actions/runs/{run_id}"
    if (
        run.get("id") != run_id
        or actor != authority.actor
        or triggering_actor != authority.actor
        or repository != REPOSITORY
        or run.get("url") != expected_api_url
        or run.get("html_url") != expected_html_url
        or run.get("run_attempt") != 1
        or not exact_run_identity(run, authority)
    ):
        fail("workflow run identity differs from its exact authority")


def wait_for_correlated_run(
    expected_run_id: int, authority: Authority, label: str, deadline: float
) -> dict[str, Any]:
    while time.monotonic() < deadline:
        run = gh_json(f"repos/{REPOSITORY}/actions/runs/{expected_run_id}")
        if not isinstance(run, dict):
            fail("dispatched workflow run is invalid during correlation")
        validate_known_run(run, expected_run_id, authority)
        export_jobs = [job for job in run_jobs(expected_run_id) if job.get("name") == EXPORT_JOB_NAME]
        if len(export_jobs) > 1:
            fail("workflow run has multiple export jobs")
        if export_jobs:
            labels = exact_unique_labels(export_jobs[0].get("labels"), "export job labels")
            if labels != EXPECTED_JOB_LABELS | {label}:
                fail("workflow export job labels differ from the exact unique runner label")
            return run
        time.sleep(2)
    fail("timed out waiting for exact workflow/job correlation")


@dataclasses.dataclass(frozen=True)
class RunnerRegistration:
    identifier: int
    name: str
    labels: frozenset[str]


@dataclasses.dataclass(frozen=True)
class JitSeedMaterial:
    archive: bytes
    hashes: tuple[tuple[str, str], ...]

    def verification_input(self) -> bytes:
        return "".join(
            f"{digest}  {name}\n" for name, digest in self.hashes
        ).encode("ascii")


def parse_jit_config_file(content: bytes, name: str) -> dict[str, Any]:
    if not content or b"\x00" in content:
        fail(f"JIT configuration file is not exact UTF-8 JSON: {name}")
    try:
        text = content.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        fail(f"JIT configuration file is not exact UTF-8 JSON: {name}")
    if "\ufeff" in text:
        fail(f"JIT configuration file contains a BOM: {name}")

    def exact_pairs(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, item in pairs:
            if key in result:
                fail(f"JIT configuration file contains duplicate keys: {name}")
            result[key] = item
        return result

    def reject_nonfinite(_value: str) -> None:
        fail(f"JIT configuration file contains a non-finite number: {name}")

    try:
        parsed = json.loads(
            text,
            object_pairs_hook=exact_pairs,
            parse_constant=reject_nonfinite,
        )
    except json.JSONDecodeError:
        fail(f"JIT configuration file is not exact UTF-8 JSON: {name}")
    if type(parsed) is not dict or not parsed:
        fail(f"JIT configuration file must contain a nonempty object: {name}")

    pending: list[object] = [parsed]
    while pending:
        item = pending.pop()
        if isinstance(item, float) and not math.isfinite(item):
            fail(f"JIT configuration file contains a non-finite number: {name}")
        if isinstance(item, str):
            if "\x00" in item or "\ufeff" in item:
                fail(f"JIT configuration file contains a forbidden character: {name}")
        elif isinstance(item, dict):
            pending.extend(item.keys())
            pending.extend(item.values())
        elif isinstance(item, list):
            pending.extend(item)
    return parsed


def canonicalize_jit_config(value: object) -> JitSeedMaterial:
    encoded = require_match(value, JIT_CONFIG_RE, "encoded JIT configuration")
    try:
        wire = encoded.encode("ascii")
        decoded = base64.b64decode(wire, validate=True)
    except (UnicodeEncodeError, ValueError) as exc:
        fail(f"encoded JIT configuration is invalid: {type(exc).__name__}")
    if base64.b64encode(decoded) != wire:
        fail("encoded JIT configuration is not canonical base64")

    def exact_pairs(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, item in pairs:
            if key in result:
                fail("JIT configuration JSON contains duplicate keys")
            result[key] = item
        return result

    try:
        payload = json.loads(decoded.decode("utf-8"), object_pairs_hook=exact_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError):
        fail("JIT configuration does not contain exact UTF-8 JSON")
    if type(payload) is not dict or set(payload) != set(ALLOWED_JIT_CONFIG_FILES):
        fail("JIT configuration contains an unexpected file set")
    content_by_name: dict[str, bytes] = {}
    for name in ALLOWED_JIT_CONFIG_FILES:
        item = payload[name]
        if type(item) is not str or not item or len(item) > JIT_CONFIG_LIMIT:
            fail("JIT configuration contains an invalid file payload")
        try:
            inner_wire = item.encode("ascii")
            inner = base64.b64decode(inner_wire, validate=True)
        except (UnicodeEncodeError, ValueError):
            fail("JIT configuration contains invalid file base64")
        if not inner or base64.b64encode(inner) != inner_wire:
            fail("JIT configuration contains noncanonical file base64")
        parse_jit_config_file(inner, name)
        content_by_name[name] = inner
    canonical_json = json.dumps(
        payload, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")
    if len(base64.b64encode(canonical_json)) > JIT_CONFIG_LIMIT:
        fail("canonical JIT configuration exceeds the fixed bound")
    marker = secrets.token_bytes(32)
    materialized = {".ownership-marker": marker, **content_by_name}
    archive = io.BytesIO()
    with tarfile.open(fileobj=archive, mode="w", format=tarfile.USTAR_FORMAT) as bundle:
        for name in (".ownership-marker",) + ALLOWED_JIT_CONFIG_FILES:
            content = materialized[name]
            entry = tarfile.TarInfo(name)
            entry.size = len(content)
            entry.mode = 0o600
            entry.uid = RUNNER_UID
            entry.gid = RUNNER_GID
            entry.uname = ""
            entry.gname = ""
            entry.mtime = 0
            bundle.addfile(entry, io.BytesIO(content))
    hashes = tuple(
        (name, hashlib.sha256(materialized[name]).hexdigest())
        for name in (".ownership-marker",) + ALLOWED_JIT_CONFIG_FILES
    )
    return JitSeedMaterial(archive.getvalue(), hashes)


def request_jit_config(nonce: str) -> tuple[RunnerRegistration, JitSeedMaterial]:
    runner_name = RUNNER_NAME_PREFIX + nonce
    runner_label = RUNNER_LABEL_PREFIX + nonce
    payload = {
        "name": runner_name,
        "runner_group_id": RUNNER_GROUP_ID,
        "labels": ["self-hosted", "linux", "x64", runner_label],
        "work_folder": "_work",
    }
    try:
        response = gh_json(
            f"repos/{REPOSITORY}/actions/runners/generate-jitconfig",
            method="POST",
            payload=payload,
        )
    except BaseException as primary:
        errors: list[tuple[str, BaseException]] = []
        try:
            recovered = recover_runner_registration(runner_name, runner_label)
            if recovered is not None:
                cleanup_runner_registration(recovered)
        except BaseException as exc:
            errors.append(("recover_jit_registration", exc))
        add_cleanup_notes(primary, errors)
        raise
    registration: RunnerRegistration | None = None
    try:
        if not isinstance(response, dict):
            fail("JIT configuration response is invalid")
        runner = response.get("runner")
        if not isinstance(runner, dict):
            fail("JIT response did not return an exact runner identity")
        identifier = require_positive_integer(runner.get("id"), "JIT runner ID")
        name = exact_string(runner.get("name"), "JIT runner name")
        labels = exact_unique_labels(runner.get("labels"), "JIT runner labels")
        expected_labels = EXPECTED_JOB_LABELS | {runner_label}
        if name != runner_name or labels != expected_labels:
            fail("JIT response runner identity differs")
        registration = RunnerRegistration(identifier, name, labels)
        seed_material = canonicalize_jit_config(response.get("encoded_jit_config"))
        return registration, seed_material
    except BaseException as primary:
        errors: list[tuple[str, BaseException]] = []
        try:
            if registration is None:
                registration = recover_runner_registration(runner_name, runner_label)
            if registration is not None:
                cleanup_runner_registration(registration)
        except BaseException as exc:
            errors.append(("cleanup_invalid_jit_registration", exc))
        add_cleanup_notes(primary, errors)
        raise


def verify_docker_authority() -> dict[str, Any]:
    contexts = json.loads(run_checked(("docker", "context", "inspect"), kind="docker"))
    if not isinstance(contexts, list) or len(contexts) != 1:
        fail("Docker context is ambiguous")
    host = contexts[0].get("Endpoints", {}).get("docker", {}).get("Host")
    if not isinstance(host, str) or not host.startswith("unix://"):
        fail("launcher requires an exact local Unix-socket Docker context")
    run_checked(("docker", "pull", "--platform", "linux/amd64", IMAGE), timeout=600, kind="docker")
    images = json.loads(run_checked(("docker", "image", "inspect", IMAGE), kind="docker"))
    if not isinstance(images, list) or len(images) != 1:
        fail("pinned runner image inspection is ambiguous")
    image = images[0]
    config = image.get("Config", {})
    repo_digests = image.get("RepoDigests", [])
    image_id = exact_string(image.get("Id"), "pinned Docker image ID")
    if (
        re.fullmatch(r"sha256:[0-9a-f]{64}", image_id) is None
        or
        image.get("Architecture") != "amd64"
        or image.get("Os") != "linux"
        or config.get("User") != EXPECTED_IMAGE_USER
        or config.get("WorkingDir") != EXPECTED_IMAGE_WORKDIR
        or IMAGE not in repo_digests
    ):
        fail("pinned runner image metadata differs from the governed contract")
    return image


def docker_volume_names() -> list[str]:
    raw = run_checked(("docker", "volume", "ls", "-q"), kind="docker")
    names = raw.splitlines()
    for name in names:
        if (
            not name
            or re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,254}", name) is None
            or any(ord(character) < 32 or ord(character) == 127 for character in name)
        ):
            fail("Docker volume inventory contains a noncanonical identity")
    if len(names) != len(set(names)):
        fail("Docker volume inventory contains duplicate identities")
    return names


def docker_container_ids() -> list[str]:
    raw = run_checked(
        (
            "docker", "container", "ls", "--all", "--no-trunc", "-q",
        ), kind="docker"
    )
    identifiers = [
        require_match(row, DOCKER_ID_RE, "Docker container ID")
        for row in raw.splitlines()
    ]
    if len(identifiers) != len(set(identifiers)):
        fail("Docker container inventory contains duplicate identities")
    return identifiers


def docker_inspect(kind: str, identifier: str) -> dict[str, Any]:
    if kind not in {"container", "volume"}:
        fail("unsupported Docker identity kind")
    raw = run_checked(("docker", kind, "inspect", identifier), kind="docker")
    try:
        payload = json.loads(raw)
    except json.JSONDecodeError:
        fail(f"Docker {kind} inspection is invalid")
    if not isinstance(payload, list) or len(payload) != 1 or not isinstance(payload[0], dict):
        fail(f"Docker {kind} inspection is ambiguous")
    return payload[0]


def exact_string_mapping(value: object, label: str) -> dict[str, str]:
    if value is None:
        return {}
    if not isinstance(value, dict):
        fail(f"{label} is invalid")
    result: dict[str, str] = {}
    for key, item in value.items():
        exact_key = exact_string(key, label)
        exact_value = exact_string(item, label)
        result[exact_key] = exact_value
    return result


@dataclasses.dataclass(frozen=True)
class VolumeIdentity:
    name: str
    driver: str
    mountpoint: str
    created_at: str
    scope: str
    labels_json: str
    options_json: str
    nonce: str


def bind_volume_identity(inspected: dict[str, Any], name: str, nonce: str) -> VolumeIdentity:
    name = require_match(name, DOCKER_VOLUME_RE, "Docker volume identity")
    labels = exact_string_mapping(inspected.get("Labels"), "Docker volume labels")
    options = exact_string_mapping(inspected.get("Options"), "Docker volume options")
    mountpoint = exact_string(inspected.get("Mountpoint"), "Docker volume mountpoint")
    if (
        inspected.get("Name") != name
        or labels != {"com.docker.volume.anonymous": ""}
        or not mountpoint.startswith("/")
        or mountpoint != os.path.normpath(mountpoint)
        or any(ord(character) < 32 or ord(character) == 127 for character in mountpoint)
    ):
        fail("Docker volume identity differs from the governed acquisition")
    return VolumeIdentity(
        name=name,
        driver=exact_string(inspected.get("Driver"), "Docker volume driver"),
        mountpoint=mountpoint,
        created_at=exact_string(inspected.get("CreatedAt"), "Docker volume creation time"),
        scope=exact_string(inspected.get("Scope"), "Docker volume scope"),
        labels_json=json.dumps(labels, sort_keys=True, separators=(",", ":")),
        options_json=json.dumps(options, sort_keys=True, separators=(",", ":")),
        nonce=nonce,
    )


def confirmed_volume(identity: VolumeIdentity) -> VolumeIdentity | None:
    names = docker_volume_names()
    if identity.name not in names:
        return None
    current = bind_volume_identity(
        docker_inspect("volume", identity.name), identity.name, identity.nonce
    )
    if current != identity:
        fail("Docker volume stable identity changed")
    return current


def validate_mount_component(value: str, label: str) -> str:
    value = exact_string(value, label)
    if not value or "," in value or any(
        ord(character) < 32 or ord(character) == 127 for character in value
    ):
        fail(f"{label} contains a Docker mount delimiter or control character")
    return value


def bind_mount(source: Path, destination: str) -> str:
    source = require_absolute_directory_no_links(source, "Docker bind mount source")
    source_text = validate_mount_component(str(source), "Docker bind mount source")
    destination = validate_mount_component(destination, "Docker bind mount destination")
    if not destination.startswith("/") or destination != os.path.normpath(destination):
        fail("Docker bind mount destination is not canonical")
    return (
        f"type=bind,src={source_text},dst={destination},readonly,"
        "bind-propagation=rprivate"
    )


def volume_mount(volume: VolumeIdentity, destination: str, *, readonly: bool) -> str:
    source = require_match(
        validate_mount_component(volume.name, "Docker volume mount source"),
        DOCKER_VOLUME_RE,
        "Docker volume mount source",
    )
    destination = validate_mount_component(destination, "Docker volume mount destination")
    if not destination.startswith("/") or destination != os.path.normpath(destination):
        fail("Docker volume mount destination is not canonical")
    suffix = ",readonly" if readonly else ""
    return f"type=volume,src={source},dst={destination}{suffix},volume-nocopy"


def anonymous_volume_mount(destination: str) -> str:
    destination = validate_mount_component(
        destination, "Docker anonymous volume mount destination"
    )
    if not destination.startswith("/") or destination != os.path.normpath(destination):
        fail("Docker anonymous volume mount destination is not canonical")
    return f"type=volume,dst={destination},volume-nocopy"


@dataclasses.dataclass(frozen=True)
class ContainerIdentity:
    identifier: str
    name: str
    created: str
    image_id: str
    config_image: str
    user: str
    labels_json: str
    entrypoint_json: str
    command_json: str
    mounts_json: str
    host_mounts_json: str
    nonce: str


def canonical_mount_inventory(
    value: object, destination_key: str, label: str
) -> str:
    if not isinstance(value, list):
        fail(f"{label} must be an exact list")
    destinations: set[str] = set()
    canonical_rows: list[tuple[str, str]] = []
    for row in value:
        if not isinstance(row, dict) or any(type(key) is not str for key in row):
            fail(f"{label} contains an invalid mount")
        destination = validate_mount_component(
            row.get(destination_key), f"{label} destination"
        )
        if (
            not destination.startswith("/")
            or destination != os.path.normpath(destination)
        ):
            fail(f"{label} destination is not canonical")
        if destination in destinations:
            fail(f"{label} contains duplicate destinations")
        destinations.add(destination)
        try:
            canonical = json.dumps(
                row,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
                allow_nan=False,
            )
        except (TypeError, ValueError):
            fail(f"{label} contains a noncanonical mount")
        canonical_rows.append((destination, canonical))
    canonical_rows.sort()
    return "[" + ",".join(canonical for _destination, canonical in canonical_rows) + "]"


def bind_container_identity(
    inspected: dict[str, Any], identifier: str, name: str, nonce: str
) -> ContainerIdentity:
    identifier = require_match(identifier, DOCKER_ID_RE, "Docker container identity")
    config = inspected.get("Config")
    host = inspected.get("HostConfig")
    mounts = inspected.get("Mounts")
    if not isinstance(config, dict) or not isinstance(host, dict) or not isinstance(mounts, list):
        fail("Docker container inspection is incomplete")
    labels = exact_string_mapping(config.get("Labels"), "Docker container labels")
    if (
        inspected.get("Id") != identifier
        or inspected.get("Name") != "/" + name
        or labels.get(OWNER_LABEL) != "1"
        or labels.get(NONCE_LABEL) != nonce
    ):
        fail("Docker container identity differs from the governed acquisition")
    return ContainerIdentity(
        identifier=identifier,
        name=name,
        created=exact_string(inspected.get("Created"), "Docker container creation time"),
        image_id=exact_string(inspected.get("Image"), "Docker container image ID"),
        config_image=exact_string(config.get("Image"), "Docker container image reference"),
        user=exact_string(config.get("User"), "Docker container user"),
        labels_json=json.dumps(labels, sort_keys=True, separators=(",", ":")),
        entrypoint_json=json.dumps(config.get("Entrypoint"), sort_keys=True, separators=(",", ":")),
        command_json=json.dumps(config.get("Cmd"), sort_keys=True, separators=(",", ":")),
        mounts_json=canonical_mount_inventory(
            mounts, "Destination", "Docker container mounts"
        ),
        host_mounts_json=canonical_mount_inventory(
            host.get("Mounts"), "Target", "Docker host-config mounts"
        ),
        nonce=nonce,
    )


def confirmed_container(identity: ContainerIdentity) -> tuple[ContainerIdentity, dict[str, Any]] | None:
    identifiers = docker_container_ids()
    if identity.identifier not in identifiers:
        return None
    inspected = docker_inspect("container", identity.identifier)
    current = bind_container_identity(
        inspected, identity.identifier, identity.name, identity.nonce
    )
    if current != identity:
        fail("Docker container stable identity changed")
    return current, inspected


def recover_created_container(
    before_ids: set[str], name: str, nonce: str
) -> ContainerIdentity | None:
    candidates: list[ContainerIdentity] = []
    for identifier in docker_container_ids():
        if identifier in before_ids:
            continue
        try:
            inspected = docker_inspect("container", identifier)
            if inspected.get("Name") != "/" + name:
                continue
            candidate = bind_container_identity(inspected, identifier, name, nonce)
        except LaunchError:
            continue
        candidates.append(candidate)
    if len(candidates) > 1:
        fail("Docker container acquisition recovery is ambiguous")
    return candidates[0] if candidates else None


def create_owned_container(
    command: list[str], name: str, nonce: str, validator: Any,
    *, cleanup_volumes_on_failure: bool = False,
) -> ContainerIdentity:
    before_ids = set(docker_container_ids())
    for identifier in before_ids:
        if docker_inspect("container", identifier).get("Name") == "/" + name:
            fail("Docker container name is already allocated")
    acquired: ContainerIdentity | None = None
    try:
        created = run_checked(command, kind="docker").strip()
        identifier = require_match(created, DOCKER_ID_RE, "created Docker container ID")
        if identifier in before_ids:
            fail("Docker returned a pre-existing container identity")
        if identifier not in docker_container_ids():
            fail("new Docker container is absent from the authoritative inventory")
        inspected = docker_inspect("container", identifier)
        acquired = bind_container_identity(inspected, identifier, name, nonce)
        validator(inspected)
        if confirmed_container(acquired) is None:
            fail("new Docker container disappeared during identity binding")
        return acquired
    except BaseException as primary:
        errors: list[tuple[str, BaseException]] = []
        try:
            if acquired is None:
                acquired = recover_created_container(before_ids, name, nonce)
            if acquired is not None:
                remove_owned_container(
                    acquired, remove_volumes=cleanup_volumes_on_failure
                )
        except BaseException as exc:
            errors.append(("recover_container_acquisition", exc))
        add_cleanup_notes(primary, errors)
        raise


def remove_owned_container(
    identity: ContainerIdentity,
    *,
    remove_volumes: bool = False,
    expected_volumes: frozenset[str] = frozenset(),
) -> None:
    recorded_volumes: frozenset[str] = frozenset()
    if remove_volumes:
        try:
            recorded_mounts = json.loads(identity.mounts_json)
        except json.JSONDecodeError:
            fail("recorded container mount identity is invalid")
        if not isinstance(recorded_mounts, list):
            fail("recorded container mount identity is incomplete")
        recorded_volumes = frozenset(
            require_match(row.get("Name"), DOCKER_VOLUME_RE, "recorded volume identity")
            for row in recorded_mounts
            if isinstance(row, dict) and row.get("Type") == "volume"
        )
        if len(recorded_volumes) != 1:
            fail("recorded container did not bind one anonymous volume")
        if expected_volumes and recorded_volumes != expected_volumes:
            fail("recorded container volume identity differs")
    confirmed = confirmed_container(identity)
    if confirmed is None:
        if remove_volumes and recorded_volumes & set(docker_volume_names()):
            fail("container is absent while its recorded volume remains")
        return
    inspected = confirmed[1]
    state = inspected.get("State")
    if not isinstance(state, dict) or type(state.get("Running")) is not bool:
        fail("Docker container state is ambiguous during cleanup")
    if state["Running"]:
        output = run_checked(
            ("docker", "container", "stop", "--timeout", "10", identity.identifier),
            timeout=30,
            kind="docker",
        ).strip()
        if output not in {identity.identifier, identity.name}:
            fail("Docker stopped an unexpected container")
        confirmed = confirmed_container(identity)
        if confirmed is None:
            if remove_volumes and recorded_volumes & set(docker_volume_names()):
                fail("container disappeared while its recorded volume remained")
            return
        state = confirmed[1].get("State")
        if not isinstance(state, dict) or state.get("Running") is not False:
            fail("Docker container stop could not be confirmed")
    if remove_volumes:
        inspected_mounts = confirmed[1].get("Mounts") or []
        attached_volumes = frozenset(
            require_match(row.get("Name"), DOCKER_VOLUME_RE, "attached volume identity")
            for row in inspected_mounts
            if isinstance(row, dict) and row.get("Type") == "volume"
        )
        if len(attached_volumes) != 1:
            fail("container cleanup did not bind one exact anonymous volume")
        if attached_volumes != recorded_volumes:
            fail("container cleanup mount differs from its recorded volume")
        if expected_volumes and attached_volumes != expected_volumes:
            fail("container cleanup volume identity differs")
    else:
        attached_volumes = frozenset()
    command = ["docker", "container", "rm"]
    if remove_volumes:
        command.append("--volumes")
    command.append(identity.identifier)
    output = run_checked(command, kind="docker").strip()
    if output not in {identity.identifier, identity.name}:
        fail("Docker removed an unexpected container")
    if confirmed_container(identity) is not None:
        fail("Docker container removal could not be confirmed")
    remaining_volumes = set(docker_volume_names())
    if remove_volumes and attached_volumes & remaining_volumes:
        fail("anonymous Docker volume removal could not be confirmed")


def terminate_and_reap(process: subprocess.Popen[bytes]) -> list[tuple[str, BaseException]]:
    errors: list[tuple[str, BaseException]] = []
    try:
        process.terminate()
    except BaseException as exc:
        errors.append(("terminate_docker_client", exc))
    try:
        process.communicate(timeout=PROCESS_REAP_SECONDS)
        return errors
    except subprocess.TimeoutExpired:
        pass
    except BaseException as exc:
        errors.append(("wait_docker_client", exc))
    try:
        process.kill()
    except BaseException as exc:
        errors.append(("kill_docker_client", exc))
    try:
        process.communicate(timeout=PROCESS_REAP_SECONDS)
    except BaseException as exc:
        errors.append(("reap_docker_client", exc))
    return errors


def run_attached_container(
    identity: ContainerIdentity,
    *,
    input_bytes: bytes | None,
    timeout: float,
    label: str,
) -> None:
    if timeout <= 0:
        fail(f"{label} deadline expired")
    command = ["docker", "container", "start", "--attach"]
    if input_bytes is not None:
        command.append("--interactive")
    command.append(identity.identifier)
    process = subprocess.Popen(
        command,
        stdin=subprocess.PIPE if input_bytes is not None else subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        shell=False,
        env=command_environment("docker"),
    )
    try:
        process.communicate(input=input_bytes, timeout=timeout)
    except subprocess.TimeoutExpired:
        primary = LaunchError(f"{label} exceeded the bounded wait")
        add_cleanup_notes(primary, terminate_and_reap(process))
        raise primary
    except BaseException as primary:
        add_cleanup_notes(primary, terminate_and_reap(process))
        raise
    if process.returncode != 0:
        fail(f"{label} failed with exit code {process.returncode}")


def validate_helper_container(
    inspected: dict[str, Any], image_id: str, expected_user: str,
    volume: VolumeIdentity, readonly: bool,
) -> None:
    config = inspected.get("Config") or {}
    host = inspected.get("HostConfig") or {}
    mounts = inspected.get("Mounts") or []
    if (
        inspected.get("Image") != image_id
        or config.get("Image") != IMAGE
        or config.get("User") != expected_user
        or config.get("Entrypoint") != ["/bin/bash"]
        or config.get("Cmd") != ["-c", SEED_VERIFY_COMMAND]
        or config.get("OpenStdin") is not True
        or host.get("Privileged") is not False
        or host.get("AutoRemove") is not False
        or host.get("NetworkMode") != "none"
        or host.get("ReadonlyRootfs") is not True
        or "ALL" not in (host.get("CapDrop") or [])
        or (host.get("CapAdd") or []) != []
        or "no-new-privileges:true" not in (host.get("SecurityOpt") or [])
        or len(mounts) != 1
    ):
        fail("Docker helper isolation differs from the governed contract")
    mount = mounts[0] if mounts and isinstance(mounts[0], dict) else {}
    if (
        mount.get("Type") != "volume"
        or mount.get("Name") != volume.name
        or mount.get("Destination") != "/jit-seed"
        or mount.get("RW") is not (not readonly)
    ):
        fail("Docker helper mount differs from the exact seed boundary")


def run_and_remove_helper(
    identity: ContainerIdentity, *, input_bytes: bytes | None, label: str
) -> None:
    primary: BaseException | None = None
    try:
        run_attached_container(
            identity, input_bytes=input_bytes, timeout=60, label=label
        )
    except BaseException as exc:
        primary = exc
        raise
    finally:
        try:
            remove_owned_container(identity)
        except BaseException as exc:
            if primary is not None:
                add_cleanup_notes(primary, [("remove_helper_container", exc)])
            else:
                raise


@dataclasses.dataclass(frozen=True)
class ConfigLease:
    holder: ContainerIdentity
    volume: VolumeIdentity


def validate_config_holder(
    inspected: dict[str, Any], image_id: str, nonce: str
) -> VolumeIdentity:
    config = inspected.get("Config") or {}
    host = inspected.get("HostConfig") or {}
    mounts = inspected.get("Mounts") or []
    if (
        inspected.get("Image") != image_id
        or config.get("Image") != IMAGE
        or config.get("User") != "0:0"
        or config.get("Entrypoint") != ["/bin/bash"]
        or config.get("Cmd") != ["-c", SEED_WRITER_COMMAND]
        or config.get("OpenStdin") is not True
        or host.get("Privileged") is not False
        or host.get("AutoRemove") is not False
        or host.get("NetworkMode") != "none"
        or host.get("ReadonlyRootfs") is not True
        or "ALL" not in (host.get("CapDrop") or [])
        or host.get("CapAdd") != ["CAP_CHOWN"]
        or "no-new-privileges:true" not in (host.get("SecurityOpt") or [])
        or len(mounts) != 1
    ):
        fail("JIT config holder isolation differs from the governed contract")
    mount = mounts[0] if isinstance(mounts[0], dict) else {}
    volume_name = require_match(
        mount.get("Name"), DOCKER_VOLUME_RE, "anonymous JIT volume identity"
    )
    if (
        mount.get("Type") != "volume"
        or mount.get("Destination") != "/jit-seed"
        or mount.get("RW") is not True
    ):
        fail("JIT config holder mount differs from the anonymous lease")
    if volume_name not in docker_volume_names():
        fail("anonymous JIT volume is absent from the authoritative inventory")
    return bind_volume_identity(
        docker_inspect("volume", volume_name), volume_name, nonce
    )


def confirmed_config_lease(
    lease: ConfigLease, image_id: str
) -> tuple[ContainerIdentity, VolumeIdentity] | None:
    confirmed = confirmed_container(lease.holder)
    if confirmed is None:
        if lease.volume.name in docker_volume_names():
            fail("config holder disappeared while its anonymous volume remained")
        return None
    volume = validate_config_holder(confirmed[1], image_id, lease.holder.nonce)
    if volume != lease.volume or confirmed_volume(lease.volume) is None:
        fail("anonymous JIT volume lease identity changed")
    return confirmed[0], volume


def create_config_lease(nonce: str, image_id: str) -> ConfigLease:
    name = CONFIG_HOLDER_PREFIX + nonce
    anonymous_mount = anonymous_volume_mount("/jit-seed")
    captured: dict[str, VolumeIdentity] = {}

    def validate(inspected: dict[str, Any]) -> None:
        captured["volume"] = validate_config_holder(inspected, image_id, nonce)

    holder = create_owned_container(
        [
            "docker", "container", "create", "--platform", "linux/amd64",
            "--name", name, "--network", "none", "--read-only", "--interactive",
            "--user", "0:0", "--cap-drop", "ALL", "--cap-add", "CHOWN",
            "--security-opt", "no-new-privileges:true",
            "--label", f"{OWNER_LABEL}=1", "--label", f"{NONCE_LABEL}={nonce}",
            "--mount", anonymous_mount, "--entrypoint", "/bin/bash", IMAGE, "-c",
            SEED_WRITER_COMMAND,
        ],
        name,
        nonce,
        validate,
        cleanup_volumes_on_failure=True,
    )
    try:
        volume = captured.get("volume")
        if volume is None:
            fail("config holder did not bind an anonymous volume")
        lease = ConfigLease(holder, volume)
        if confirmed_config_lease(lease, image_id) is None:
            fail("new JIT config lease disappeared during identity binding")
        return lease
    except BaseException as primary:
        errors: list[tuple[str, BaseException]] = []
        try:
            remove_owned_container(holder, remove_volumes=True)
        except BaseException as exc:
            errors.append(("remove_unbound_config_holder", exc))
        add_cleanup_notes(primary, errors)
        raise


def recover_config_lease(nonce: str, image_id: str) -> ConfigLease | None:
    holder = recover_created_container(set(), CONFIG_HOLDER_PREFIX + nonce, nonce)
    if holder is None:
        return None
    volume = validate_config_holder(
        docker_inspect("container", holder.identifier), image_id, nonce
    )
    lease = ConfigLease(holder, volume)
    if confirmed_config_lease(lease, image_id) is None:
        fail("recovered JIT config lease disappeared")
    return lease


def materialize_config_lease(
    lease: ConfigLease, seed: JitSeedMaterial, image_id: str
) -> None:
    if confirmed_config_lease(lease, image_id) is None:
        fail("JIT config lease is absent before materialization")
    run_attached_container(
        lease.holder,
        input_bytes=seed.archive,
        timeout=60,
        label="JIT seed materializer",
    )
    if confirmed_config_lease(lease, image_id) is None:
        fail("JIT config lease disappeared after materialization")
    verify_seed_volume(lease, seed, image_id)


def verify_seed_volume(
    lease: ConfigLease, seed: JitSeedMaterial, image_id: str
) -> None:
    if confirmed_config_lease(lease, image_id) is None:
        fail("JIT seed lease is absent before content verification")
    volume = lease.volume
    nonce = lease.holder.nonce
    mount = volume_mount(volume, "/jit-seed", readonly=True)
    name = CONFIG_VERIFY_PREFIX + nonce
    verifier = create_owned_container(
        [
            "docker", "container", "create", "--platform", "linux/amd64",
            "--name", name, "--network", "none", "--read-only", "--interactive",
            "--user", "runner", "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges:true",
            "--label", f"{OWNER_LABEL}=1", "--label", f"{NONCE_LABEL}={nonce}",
            "--mount", mount, "--entrypoint", "/bin/bash", IMAGE, "-c",
            SEED_VERIFY_COMMAND,
        ],
        name,
        nonce,
        lambda inspected: validate_helper_container(
            inspected, image_id, "runner", volume, True
        ),
    )
    run_and_remove_helper(
        verifier,
        input_bytes=seed.verification_input(),
        label="JIT seed identity verifier",
    )
    if confirmed_config_lease(lease, image_id) is None:
        fail("JIT seed lease disappeared during content verification")


def remove_config_lease(
    lease: ConfigLease, seed: JitSeedMaterial, image_id: str
) -> None:
    if confirmed_config_lease(lease, image_id) is None:
        return
    verify_seed_volume(lease, seed, image_id)
    if confirmed_config_lease(lease, image_id) is None:
        fail("JIT config lease disappeared after cleanup verification")
    remove_owned_container(
        lease.holder,
        remove_volumes=True,
        expected_volumes=frozenset((lease.volume.name,)),
    )
    if lease.holder.identifier in docker_container_ids():
        fail("JIT config holder removal could not be confirmed")
    if lease.volume.name in docker_volume_names():
        fail("leased anonymous JIT volume removal could not be confirmed")


def runner_docker_command(
    candidate_root: Path, lease: ConfigLease, nonce: str
) -> list[str]:
    candidate_mount = bind_mount(candidate_root, "/candidate-input")
    seed_mount = volume_mount(lease.volume, "/jit-seed", readonly=True)
    return [
        "docker", "container", "create", "--platform", "linux/amd64",
        "--name", CONTAINER_PREFIX + nonce,
        "--label", f"{OWNER_LABEL}=1", "--label", f"{NONCE_LABEL}={nonce}",
        "--cap-drop", "ALL", "--security-opt", "no-new-privileges:true",
        "--user", "1001:1001",
        "--pids-limit", "1024", "--stop-timeout", "30",
        "--mount", candidate_mount, "--mount", seed_mount,
        "--entrypoint", "/bin/bash", IMAGE, "-c", RUNNER_ENTRYPOINT_COMMAND,
    ]


def validate_runner_container(
    inspected: dict[str, Any], candidate_root: Path, lease: ConfigLease,
    nonce: str, image_id: str,
) -> None:
    config = inspected.get("Config") or {}
    host = inspected.get("HostConfig") or {}
    mounts = inspected.get("Mounts") or []
    if (
        inspected.get("Image") != image_id
        or config.get("Image") != IMAGE
        or config.get("User") != "1001:1001"
        or config.get("Entrypoint") != ["/bin/bash"]
        or config.get("Cmd") != ["-c", RUNNER_ENTRYPOINT_COMMAND]
        or host.get("Privileged") is not False
        or host.get("AutoRemove") is not False
        or host.get("ReadonlyRootfs") is not False
        or host.get("NetworkMode") not in {"default", "bridge"}
        or host.get("PidsLimit") != 1024
        or "ALL" not in (host.get("CapDrop") or [])
        or (host.get("CapAdd") or []) != []
        or "no-new-privileges:true" not in (host.get("SecurityOpt") or [])
        or len(mounts) != 2
    ):
        fail("JIT runner container identity/isolation differs")
    actual = {
        (
            row.get("Type"),
            row.get("Source") if row.get("Type") == "bind" else row.get("Name"),
            row.get("Destination"),
            row.get("RW"),
        )
        for row in mounts if isinstance(row, dict)
    }
    expected = {
        ("bind", str(candidate_root), "/candidate-input", False),
        ("volume", lease.volume.name, "/jit-seed", False),
    }
    if actual != expected:
        fail("JIT runner mounts differ from the exact two-mount boundary")


def create_runner_container(
    candidate: CandidateIdentity, lease: ConfigLease, seed: JitSeedMaterial,
    nonce: str, image_id: str,
) -> ContainerIdentity:
    verify_seed_volume(lease, seed, image_id)
    name = CONTAINER_PREFIX + nonce
    command = runner_docker_command(candidate.root, lease, nonce)
    return create_owned_container(
        command,
        name,
        nonce,
        lambda inspected: validate_runner_container(
            inspected, candidate.root, lease, nonce, image_id
        ),
    )


def execute_runner(identity: ContainerIdentity, deadline: float) -> None:
    remaining = deadline - time.monotonic()
    run_attached_container(
        identity, input_bytes=None, timeout=remaining, label="JIT runner container"
    )


def wait_for_workflow_success(run_id: int, authority: Authority, runner_name: str, label: str, deadline: float) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    while time.monotonic() < deadline:
        run = gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}")
        if not isinstance(run, dict):
            fail("correlated workflow run identity changed")
        validate_known_run(run, run_id, authority)
        if run.get("status") != "completed":
            time.sleep(2)
            continue
        if run.get("conclusion") != "success":
            fail("candidate exporter workflow did not succeed")
        jobs = run_jobs(run_id)
        export_jobs = [job for job in jobs if job.get("name") == EXPORT_JOB_NAME]
        if len(export_jobs) != 1:
            fail("completed workflow has an ambiguous export job")
        job = export_jobs[0]
        if (
            exact_unique_labels(job.get("labels"), "completed export job labels")
            != EXPECTED_JOB_LABELS | {label}
            or job.get("runner_name") != runner_name
            or job.get("conclusion") != "success"
        ):
            fail("completed export job differs from the exact JIT runner identity")
        if any(other is not job and other.get("runner_name") == runner_name for other in jobs):
            fail("JIT runner executed more than the exact export job")
        artifacts = gh_json(
            f"repos/{REPOSITORY}/actions/runs/{run_id}/artifacts?per_page=100&page=1"
        )
        rows = artifacts.get("artifacts") if isinstance(artifacts, dict) else None
        artifact_total = artifacts.get("total_count") if isinstance(artifacts, dict) else None
        if type(artifact_total) is not int or artifact_total != 2 or not isinstance(rows, list) or len(rows) != 2:
            fail("completed workflow must have exactly the candidate and capture-dispatch artifacts")
        attempt = require_positive_integer(run.get("run_attempt"), "workflow run attempt")
        expected_name = f"preview-nightly-candidate-{run_id}-{attempt}"
        matches = [row for row in rows or [] if isinstance(row, dict) and row.get("name") == expected_name]
        if len(matches) != 1 or matches[0].get("expired") is not False:
            fail("completed workflow artifact identity is ambiguous or expired")
        dispatch_name = f"preview-nightly-capture-dispatch-{run_id}-{attempt}"
        dispatch_matches = [
            row for row in rows or [] if isinstance(row, dict) and row.get("name") == dispatch_name
        ]
        if len(dispatch_matches) != 1 or dispatch_matches[0].get("expired") is not False:
            fail("completed workflow capture-dispatch artifact identity is ambiguous or expired")
        return run, matches[0], dispatch_matches[0]
    fail("timed out waiting for the correlated workflow to complete")


def recover_runner_registration(
    runner_name: str, unique_label: str
) -> RunnerRegistration | None:
    matches: list[RunnerRegistration] = []
    for runner in list_repository_runners():
        labels = exact_unique_labels(runner.get("labels"), "repository runner labels")
        name_matches = runner.get("name") == runner_name
        label_matches = unique_label in labels
        if name_matches or label_matches:
            if not name_matches or not label_matches or labels != EXPECTED_JOB_LABELS | {unique_label}:
                fail("recovered runner registration identity differs")
            matches.append(
                RunnerRegistration(
                    require_positive_integer(runner.get("id"), "runner ID"),
                    runner_name,
                    labels,
                )
            )
    if len(matches) > 1:
        fail("recovered runner registration identity is ambiguous")
    return matches[0] if matches else None


def cleanup_runner_registration(registration: RunnerRegistration) -> None:
    runners = list_repository_runners()
    unique_labels = registration.labels - EXPECTED_JOB_LABELS
    if len(unique_labels) != 1:
        fail("recorded runner registration lacks one exact unique label")
    unique_label = next(iter(unique_labels))
    by_id = [
        row for row in runners
        if require_positive_integer(row.get("id"), "runner ID") == registration.identifier
    ]
    colliding = []
    for runner in runners:
        labels = exact_unique_labels(runner.get("labels"), "repository runner labels")
        if runner.get("name") == registration.name or unique_label in labels:
            colliding.append((runner, labels))
    if not by_id:
        if colliding:
            fail("runner registration identity disappeared but its name/labels were reused")
        return
    if len(by_id) != 1:
        fail("runner cleanup ID is ambiguous")
    runner = by_id[0]
    labels = exact_unique_labels(runner.get("labels"), "repository runner labels")
    if runner.get("name") != registration.name or labels != registration.labels:
        fail("runner cleanup identity differs from the recorded registration")
    if len(colliding) != 1 or colliding[0][0] is not runner:
        fail("runner cleanup name/label identity is ambiguous")
    gh_json(
        f"repos/{REPOSITORY}/actions/runners/{registration.identifier}",
        method="DELETE",
    )
    remaining = list_repository_runners()
    for row in remaining:
        labels = exact_unique_labels(row.get("labels"), "repository runner labels")
        if (
            require_positive_integer(row.get("id"), "runner ID") == registration.identifier
            or row.get("name") == registration.name
            or unique_label in labels
        ):
            fail("runner registration deletion could not be confirmed")


def cancel_owned_run(run_id: int, authority: Authority) -> None:
    require_positive_integer(run_id, "cleanup workflow run ID")
    if not isinstance(authority, Authority):
        fail("workflow cleanup authority is invalid")
    gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}/cancel", method="POST")


def cleanup_failure_note(errors: list[tuple[str, BaseException]]) -> str:
    entries: list[str] = []
    for operation, error in errors[:8]:
        safe_operation = re.sub(r"[^a-z0-9_-]", "_", operation.lower())[:48]
        safe_type = re.sub(r"[^A-Za-z0-9_]", "_", type(error).__name__)[:48]
        entries.append(f"{safe_operation}={safe_type}")
    if len(errors) > 8:
        entries.append(f"additional={len(errors) - 8}")
    return (CLEANUP_NOTE_PREFIX + ", ".join(entries))[:512]


def artifact_digest(artifact: dict[str, Any]) -> str:
    value = exact_string(artifact.get("digest"), "artifact digest")
    if value.startswith("sha256:"):
        value = value[7:]
    return require_match(value, SHA256_RE, "artifact digest")


def receipt_parent_identity(metadata: os.stat_result) -> tuple[int, int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_uid,
        metadata.st_gid,
    )


def receipt_target_identity(
    metadata: os.stat_result,
) -> tuple[int, int, int, int, int, int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_uid,
        metadata.st_gid,
        metadata.st_nlink,
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_ctime_ns,
    )


def verify_receipt_descriptor_bytes(
    descriptor: int, expected: bytes, expected_sha256: str, label: str
) -> None:
    os.lseek(descriptor, 0, os.SEEK_SET)
    observed = bytearray()
    while len(observed) <= len(expected):
        chunk = os.read(
            descriptor,
            min(1024 * 1024, len(expected) + 1 - len(observed)),
        )
        if not chunk:
            break
        observed.extend(chunk)
    os.lseek(descriptor, 0, os.SEEK_SET)
    observed_bytes = bytes(observed)
    if (
        observed_bytes != expected
        or hashlib.sha256(observed_bytes).hexdigest() != expected_sha256
    ):
        fail(f"{label} differs from the exact serialized receipt")


def close_receipt_descriptors(
    descriptors: tuple[tuple[str, int | None], ...],
) -> None:
    primary = sys.exc_info()[1]
    errors: list[tuple[str, BaseException]] = []
    for operation, descriptor in descriptors:
        if descriptor is None:
            continue
        try:
            os.close(descriptor)
        except BaseException as exc:
            errors.append((operation, exc))
    if errors:
        note = cleanup_failure_note(errors)
        if primary is not None:
            primary.add_note(note)
        else:
            raise LaunchError(note)


def verify_published_receipt(
    parent_descriptor: int,
    descriptor: int,
    basename: str,
    target_identity: tuple[int, int, int, int, int, int, int, int, int],
    data: bytes,
    data_sha256: str,
    nofollow: int,
) -> None:
    if (
        receipt_target_identity(os.fstat(descriptor)) != target_identity
        or receipt_target_identity(
            os.stat(basename, dir_fd=parent_descriptor, follow_symlinks=False)
        )
        != target_identity
    ):
        fail("receipt target identity changed after parent commit")
    verify_receipt_descriptor_bytes(
        descriptor, data, data_sha256, "held receipt target"
    )
    if receipt_target_identity(os.fstat(descriptor)) != target_identity:
        fail("held receipt target identity changed while reread")

    reopened: int | None = None
    try:
        try:
            reopened = os.open(
                basename,
                os.O_RDONLY | nofollow,
                dir_fd=parent_descriptor,
            )
        except OSError:
            fail("receipt target could not be reopened without following links")
        if receipt_target_identity(os.fstat(reopened)) != target_identity:
            fail("reopened receipt target identity differs")
        verify_receipt_descriptor_bytes(
            reopened, data, data_sha256, "reopened receipt target"
        )
        if receipt_target_identity(os.fstat(reopened)) != target_identity:
            fail("reopened receipt target identity changed while reread")
    finally:
        close_receipt_descriptors((("close_reopened_receipt", reopened),))

    if (
        receipt_target_identity(os.fstat(descriptor)) != target_identity
        or receipt_target_identity(
            os.stat(basename, dir_fd=parent_descriptor, follow_symlinks=False)
        )
        != target_identity
    ):
        fail("receipt target identity changed during final verification")


def write_receipt(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute() or path != Path(os.path.normpath(str(path))):
        fail("receipt output must be a canonical absolute path")
    parent = require_absolute_directory_no_links(path.parent, "receipt output parent")
    basename = path.name
    if (
        basename in {"", ".", ".."}
        or "/" in basename
        or any(ord(character) < 32 or ord(character) == 127 for character in basename)
    ):
        fail("receipt filename is not canonical")
    data = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode()
    data_sha256 = hashlib.sha256(data).hexdigest()
    nofollow = getattr(os, "O_NOFOLLOW", 0)
    parent_descriptor = os.open(
        parent, os.O_RDONLY | os.O_DIRECTORY | nofollow
    )
    descriptor: int | None = None
    try:
        held_parent = os.fstat(parent_descriptor)
        path_parent = os.stat(parent, follow_symlinks=False)
        held_parent_identity = receipt_parent_identity(held_parent)
        if (
            not stat.S_ISDIR(held_parent.st_mode)
            or held_parent.st_uid != os.geteuid()
            or held_parent.st_mode & (stat.S_IWGRP | stat.S_IWOTH)
        ):
            fail("receipt output parent ownership or mode is unsafe")
        if held_parent_identity != receipt_parent_identity(path_parent):
            fail("receipt output parent changed before commit")
        descriptor = os.open(
            basename,
            os.O_RDWR | os.O_CREAT | os.O_EXCL | nofollow,
            0o600,
            dir_fd=parent_descriptor,
        )
        os.fchmod(descriptor, 0o600)
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written < 1:
                fail("receipt write made no progress")
            view = view[written:]
        os.fsync(descriptor)
        target_metadata = os.fstat(descriptor)
        if (
            not stat.S_ISREG(target_metadata.st_mode)
            or stat.S_IMODE(target_metadata.st_mode) != 0o600
            or target_metadata.st_uid != os.geteuid()
            or target_metadata.st_nlink != 1
            or target_metadata.st_size != len(data)
        ):
            fail("receipt target metadata differs from the governed contract")
        target_identity = receipt_target_identity(target_metadata)
        target_at_parent = os.stat(
            basename, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if receipt_target_identity(target_at_parent) != target_identity:
            fail("receipt target identity changed before commit")
        os.fsync(parent_descriptor)
        verify_published_receipt(
            parent_descriptor,
            descriptor,
            basename,
            target_identity,
            data,
            data_sha256,
            nofollow,
        )
        held_parent_now = os.fstat(parent_descriptor)
        path_parent_now = os.stat(parent, follow_symlinks=False)
        if (
            held_parent_identity != receipt_parent_identity(held_parent_now)
            or held_parent_identity != receipt_parent_identity(path_parent_now)
        ):
            fail("receipt output parent identity changed during commit")
    finally:
        close_receipt_descriptors(
            (
                ("close_receipt_target", descriptor),
                ("close_receipt_parent", parent_descriptor),
            )
        )


def orchestrate(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = Path(__file__).resolve().parents[1]
    stage_root = require_absolute_directory_no_links(
        args.prepared_stage_root, "prepared stage root"
    )
    receipt_parent = require_absolute_directory_no_links(
        args.receipt_output.parent, "receipt output parent"
    )
    if receipt_parent == stage_root or stage_root in receipt_parent.parents:
        fail("receipt output cannot modify the prepared stage tree")
    if receipt_parent == repo_root or repo_root in receipt_parent.parents:
        fail("receipt output cannot modify the trusted launcher checkout")
    local = verify_committed_local_authority(repo_root)
    require_local_head(repo_root, local.commit, "before remote authority validation")
    authority = validate_remote_authority(local.commit)
    require_local_head(repo_root, local.commit, "after remote authority validation")
    image = verify_docker_authority()
    image_id = exact_string(image.get("Id"), "pinned Docker image ID")
    exporter = load_trusted_exporter(
        local.exporter_source, local.supply_chain_source
    )
    private = create_private_tree()
    lease: ConfigLease | None = None
    seed: JitSeedMaterial | None = None
    registration: RunnerRegistration | None = None
    runner_container: ContainerIdentity | None = None
    nonce: str | None = None
    run_id: int | None = None
    cancellation_armed = False
    completed = False
    try:
        candidate = materialize_candidate_subset(
            stage_root,
            private.path / "candidate-input",
            exporter,
            authority.commit,
        )
        runners = list_repository_runners()
        nonce = generate_unique_nonce(runners)
        label = RUNNER_LABEL_PREFIX + nonce
        deadline = time.monotonic() + args.timeout_seconds
        baseline = workflow_run_baseline()
        require_local_head(repo_root, local.commit, "before workflow dispatch")
        try:
            dispatch_response = dispatch_workflow(candidate, authority, nonce)
        except DispatchIndeterminate:
            run_id, correlated = reconcile_indeterminate_dispatch(
                baseline, authority, nonce, deadline
            )
            cancellation_armed = True
        else:
            try:
                run_id = dispatch_run_id(dispatch_response)
                validate_dispatch_details(dispatch_response, run_id, authority)
            except BaseException as primary:
                add_manual_cleanup_note(
                    primary,
                    "dispatch response or exact GET could not establish cancellation authority",
                )
                raise
            cancellation_armed = True
            correlated = wait_for_correlated_run(run_id, authority, label, deadline)
        if require_positive_integer(
            correlated.get("id"), "correlated workflow run ID"
        ) != run_id:
            fail("correlated workflow run differs from the dispatched run")
        registration, seed = request_jit_config(nonce)
        bind_mount(candidate.root, "/candidate-input")
        anonymous_volume_mount("/jit-seed")
        lease = create_config_lease(nonce, image_id)
        volume_mount(lease.volume, "/jit-seed", readonly=True)
        materialize_config_lease(lease, seed, image_id)
        seed = dataclasses.replace(seed, archive=b"")
        runner_container = create_runner_container(
            candidate, lease, seed, nonce, image_id
        )
        execute_runner(runner_container, deadline)
        run, artifact, dispatch_artifact = wait_for_workflow_success(
            run_id, authority, registration.name, label, deadline
        )
        completed = True
        receipt = {
            "contractName": RECEIPT_CONTRACT,
            "contractVersion": RECEIPT_VERSION,
            "status": "succeeded",
            "repository": REPOSITORY,
            "workflow": WORKFLOW_PATH,
            "ref": SOURCE_REF,
            "sourceSha": authority.commit,
            "actor": authority.actor,
            "runId": str(run_id),
            "runAttempt": str(require_positive_integer(run.get("run_attempt"), "run attempt")),
            "runnerName": registration.name,
            "runnerLabel": label,
            "runnerImage": IMAGE,
            "candidate": {
                "version": candidate.version,
                "manifestSha256": candidate.manifest_sha256,
                "files": list(candidate.content),
            },
            "artifact": {
                "id": str(require_positive_integer(artifact.get("id"), "artifact ID")),
                "name": artifact.get("name"),
                "sha256": artifact_digest(artifact),
                "sizeBytes": require_positive_integer(artifact.get("size_in_bytes"), "artifact size"),
            },
            "captureDispatchArtifact": {
                "id": str(require_positive_integer(dispatch_artifact.get("id"), "capture dispatch artifact ID")),
                "name": dispatch_artifact.get("name"),
                "sha256": artifact_digest(dispatch_artifact),
                "sizeBytes": require_positive_integer(
                    dispatch_artifact.get("size_in_bytes"), "capture dispatch artifact size"
                ),
            },
            "completedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        }
        write_receipt(args.receipt_output, receipt)
        return receipt
    finally:
        primary_error = sys.exc_info()[1]
        errors: list[tuple[str, BaseException]] = []
        if (
            primary_error is not None
            and run_id is not None
            and not cancellation_armed
            and not completed
            and not any(
                isinstance(note, str)
                and note.startswith(MANUAL_CLEANUP_NOTE_PREFIX)
                for note in getattr(primary_error, "__notes__", ())
            )
        ):
            add_manual_cleanup_note(
                primary_error,
                "returned workflow run ID was not exact-GET validated for cancellation",
            )
        if runner_container is None and nonce is not None:
            try:
                runner_container = recover_created_container(
                    set(), CONTAINER_PREFIX + nonce, nonce
                )
            except BaseException as exc:
                errors.append(("recover_runner_container", exc))
        if runner_container is not None:
            try:
                remove_owned_container(runner_container)
            except BaseException as exc:
                errors.append(("remove_runner_container", exc))
        if registration is None and nonce is not None:
            try:
                registration = recover_runner_registration(
                    RUNNER_NAME_PREFIX + nonce, RUNNER_LABEL_PREFIX + nonce
                )
            except BaseException as exc:
                errors.append(("recover_runner_registration", exc))
        if registration is not None:
            try:
                cleanup_runner_registration(registration)
            except BaseException as exc:
                errors.append(("delete_runner", exc))
        if run_id is not None and cancellation_armed and not completed:
            try:
                cancel_owned_run(run_id, authority)
            except BaseException as exc:
                errors.append(("cancel_workflow", exc))
        if lease is None and nonce is not None:
            try:
                lease = recover_config_lease(nonce, image_id)
            except BaseException as exc:
                errors.append(("recover_config_lease", exc))
        if lease is not None:
            try:
                if seed is None:
                    fail("JIT seed authority is unavailable for volume cleanup")
                remove_config_lease(lease, seed, image_id)
            except BaseException as exc:
                errors.append(("remove_config_lease", exc))
        try:
            remove_private_tree(private)
        except BaseException as exc:
            errors.append(("remove_private_tree", exc))
        if errors:
            note = cleanup_failure_note(errors)
            if primary_error is not None:
                primary_error.add_note(note)
            else:
                raise LaunchError(note)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepared-stage-root", required=True, type=Path)
    parser.add_argument("--receipt-output", required=True, type=Path)
    parser.add_argument("--timeout-seconds", type=int, default=1800)
    args = parser.parse_args(argv)
    if not 60 <= args.timeout_seconds <= 3600:
        parser.error("--timeout-seconds must be between 60 and 3600")
    if not args.prepared_stage_root.is_absolute():
        parser.error("--prepared-stage-root must be absolute")
    if not args.receipt_output.is_absolute():
        parser.error("--receipt-output must be absolute")
    return args


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    previous_handlers: dict[int, Any] = {}

    def governed_signal_handler(signal_number: int, _frame: object) -> None:
        raise GovernedTermination(signal_number)

    for signal_number in (signal.SIGTERM, signal.SIGHUP):
        previous_handlers[signal_number] = signal.getsignal(signal_number)
        signal.signal(signal_number, governed_signal_handler)
    try:
        try:
            receipt = orchestrate(args)
        except GovernedTermination as exc:
            print(f"preview-nightly-jit-launch:terminated: {exc}", file=sys.stderr)
            for note in getattr(exc, "__notes__", ()):
                if isinstance(note, str) and note.startswith(
                    (CLEANUP_NOTE_PREFIX, MANUAL_CLEANUP_NOTE_PREFIX)
                ):
                    print(f"preview-nightly-jit-launch:cleanup: {note}", file=sys.stderr)
            return 128 + exc.signal_number
        except (LaunchError, OSError, ValueError, json.JSONDecodeError) as exc:
            print(f"preview-nightly-jit-launch:error: {exc}", file=sys.stderr)
            for note in getattr(exc, "__notes__", ()):
                if isinstance(note, str) and note.startswith(
                    (CLEANUP_NOTE_PREFIX, MANUAL_CLEANUP_NOTE_PREFIX)
                ):
                    print(f"preview-nightly-jit-launch:cleanup: {note}", file=sys.stderr)
            return 1
    finally:
        for signal_number, previous in previous_handlers.items():
            signal.signal(signal_number, previous)
    print(f"workflow_run_id={receipt['runId']}")
    print(f"receipt={args.receipt_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
