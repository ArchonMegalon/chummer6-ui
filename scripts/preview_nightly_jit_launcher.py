#!/usr/bin/env python3
"""Launch one governed disposable runner for the preview candidate export.

The host is the authority boundary.  It snapshots only the five candidate
files, verifies them with the committed exporter contract, dispatches the
fixed workflow at the exact remote ``main`` commit, and gives one JIT runner
only the read-only snapshot and an ephemeral read-only JIT-config volume.
"""

from __future__ import annotations

import argparse
import dataclasses
import datetime as dt
import hashlib
import importlib.util
import json
import os
import re
import secrets
import stat
import subprocess
import sys
import tempfile
import time
from pathlib import Path
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
CONFIG_VOLUME_PREFIX = "chummer-preview-nightly-jit-config-"
CONFIG_INIT_PREFIX = "chummer-preview-nightly-jit-config-init-"
CONFIG_WRITE_PREFIX = "chummer-preview-nightly-jit-config-write-"
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
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
NONCE_RE = re.compile(r"^[a-z0-9]{12,64}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
JIT_CONFIG_RE = re.compile(r"^[A-Za-z0-9+/=_-]{100,200000}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
EXPECTED_JOB_LABELS = frozenset(("self-hosted", "linux", "x64"))


class LaunchError(RuntimeError):
    """A fail-closed launcher contract violation."""


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


def command_environment(kind: str) -> dict[str, str]:
    if kind == "gh":
        return dict(os.environ)
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
            not endpoint.startswith(repository_prefix)
            or ".." in endpoint
            or re.fullmatch(r"[A-Za-z0-9_./?=&-]+", endpoint) is None
        )
    ):
        fail("GitHub endpoint is outside the fixed authority boundary")
    args = [
        "gh", "api", "-H", "Accept: application/vnd.github+json",
        "-H", "X-GitHub-Api-Version: 2022-11-28",
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


def open_held_sources(stage_root: Path, content_paths: tuple[str, ...]) -> tuple[list[HeldSource], list[int]]:
    nofollow = getattr(os, "O_NOFOLLOW", 0)
    root_fd = os.open(stage_root, os.O_RDONLY | os.O_DIRECTORY | nofollow)
    opened_directories = [root_fd]
    held: list[HeldSource] = []
    try:
        files_fd = os.open("files", os.O_RDONLY | os.O_DIRECTORY | nofollow, dir_fd=root_fd)
        opened_directories.append(files_fd)
        for relative in content_paths:
            parts = Path(relative).parts
            if len(parts) == 1:
                directory_fd, basename = root_fd, parts[0]
            elif len(parts) == 2 and parts[0] == "files":
                directory_fd, basename = files_fd, parts[1]
            else:
                fail("exporter content path escaped the exact two-level boundary")
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
        for descriptor in reversed(opened_directories):
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
    descriptors: list[int],
    identities: tuple[tuple[int, int, int, int, int, int], ...],
) -> None:
    if len(descriptors) != 2 or len(identities) != 2:
        fail("candidate directory authority is incomplete")
    root_now = os.fstat(descriptors[0])
    files_now = os.fstat(descriptors[1])
    root_path_now = os.stat(stage_root, follow_symlinks=False)
    files_path_now = os.stat("files", dir_fd=descriptors[0], follow_symlinks=False)
    if (
        stat_identity(root_now) != identities[0]
        or stat_identity(files_now) != identities[1]
        or stat_identity(root_path_now) != identities[0]
        or stat_identity(files_path_now) != identities[1]
        or not stat.S_ISDIR(root_now.st_mode)
        or not stat.S_ISDIR(files_now.st_mode)
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
    stage_root: Path, subset_root: Path, exporter: ModuleType
) -> CandidateIdentity:
    stage_root = require_absolute_directory_no_links(stage_root, "prepared stage root")
    if not subset_root.is_absolute() or subset_root.exists() or subset_root.is_symlink():
        fail("private candidate subset must be a new absolute path")
    subset_root.mkdir(mode=0o700)
    (subset_root / "files").mkdir(mode=0o700)
    content_paths = tuple(exporter.CONTENT_PATHS)
    held, directory_descriptors = open_held_sources(stage_root, content_paths)
    directory_identities = tuple(
        stat_identity(os.fstat(descriptor)) for descriptor in directory_descriptors
    )
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
        exporter.validate_candidate_root(subset_root, version, manifest_sha)
        validate_held_sources(held)
        validate_held_directories(stage_root, directory_descriptors, directory_identities)
        content = tuple(exporter.content_rows(subset_root))
        for relative in content_paths:
            (subset_root / relative).chmod(0o444)
        (subset_root / "files").chmod(0o555)
        subset_root.chmod(0o555)
        return CandidateIdentity(subset_root, version, manifest_sha, content)
    finally:
        for source in held:
            os.close(source.descriptor)
        for descriptor in reversed(directory_descriptors):
            os.close(descriptor)


@dataclasses.dataclass(frozen=True)
class PrivateTree:
    path: Path
    device: int
    inode: int
    owner: int


def create_private_tree() -> PrivateTree:
    path = Path(tempfile.mkdtemp(prefix="chummer-preview-nightly-jit-"))
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


def verify_committed_local_authority(repo_root: Path) -> str:
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
    tracked = (
        "scripts/preview_nightly_candidate_export.py",
        "scripts/preview_nightly_jit_launcher.py",
    )
    for relative in tracked:
        path = repo_root / relative
        metadata = os.lstat(path)
        if not stat.S_ISREG(metadata.st_mode):
            fail(f"trusted launcher input is not a regular file: {relative}")
        disk_object = run_checked(
            ("git", "-C", str(repo_root), "hash-object", "--", relative), kind="local"
        ).strip()
        commit_object = run_checked(
            ("git", "-C", str(repo_root), "rev-parse", f"HEAD:{relative}"), kind="local"
        ).strip()
        if disk_object != commit_object:
            fail(f"trusted launcher input differs from commit: {relative}")
    return commit


def load_trusted_exporter(repo_root: Path) -> ModuleType:
    path = repo_root / "scripts" / "preview_nightly_candidate_export.py"
    spec = importlib.util.spec_from_file_location("preview_nightly_candidate_export", path)
    if spec is None or spec.loader is None:
        fail("trusted exporter contract could not be loaded")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
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
            total = require_positive_integer(payload.get("total_count", 0), "runner total") if payload.get("total_count") else 0
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


def workflow_runs() -> list[dict[str, Any]]:
    payload = gh_json(
        f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_FILE}/runs"
        "?event=workflow_dispatch&branch=main&per_page=100"
    )
    if not isinstance(payload, dict) or not isinstance(payload.get("workflow_runs"), list):
        fail("workflow run inventory is invalid")
    return [row for row in payload["workflow_runs"] if isinstance(row, dict)]


def run_jobs(run_id: int) -> list[dict[str, Any]]:
    payload = gh_json(
        f"repos/{REPOSITORY}/actions/runs/{run_id}/jobs?filter=latest&per_page=100"
    )
    if not isinstance(payload, dict) or not isinstance(payload.get("jobs"), list):
        fail("workflow job inventory is invalid")
    jobs = [row for row in payload["jobs"] if isinstance(row, dict)]
    total = payload.get("total_count")
    if type(total) is not int or total != len(jobs):
        fail("workflow job inventory is incomplete or ambiguous")
    job_ids = [require_positive_integer(row.get("id"), "workflow job ID") for row in jobs]
    if len(set(job_ids)) != len(job_ids):
        fail("workflow job inventory contains duplicate identities")
    return jobs


def run_has_exact_export_label(run: dict[str, Any], label: str) -> bool:
    run_id = require_positive_integer(run.get("id"), "workflow run ID")
    matches = []
    for job in run_jobs(run_id):
        labels = job.get("labels")
        if job.get("name") == EXPORT_JOB_NAME and isinstance(labels, list):
            if set(labels) == set(EXPECTED_JOB_LABELS) | {label}:
                matches.append(job)
    if len(matches) > 1:
        fail("workflow run has multiple exact export jobs")
    return len(matches) == 1


def dispatch_workflow(candidate: CandidateIdentity, authority: Authority, nonce: str) -> Any:
    payload = {
        "ref": DEFAULT_BRANCH,
        "return_run_details": True,
        "inputs": {
            "runner_nonce": nonce,
            "candidate_version": candidate.version,
            "candidate_manifest_sha256": candidate.manifest_sha256,
            "expected_source_sha": authority.commit,
            "export_confirmed": True,
        },
    }
    return gh_json(
        f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_FILE}/dispatches",
        method="POST",
        payload=payload,
    )


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
    actor = run.get("actor", {}).get("login") if isinstance(run, dict) else None
    triggering_actor = (
        run.get("triggering_actor", {}).get("login") if isinstance(run, dict) else None
    )
    repository = run.get("repository", {}).get("full_name") if isinstance(run, dict) else None
    if (
        not isinstance(run, dict)
        or run.get("id") != run_id
        or run.get("url") != expected_api_url
        or run.get("html_url") != expected_html_url
        or actor != authority.actor
        or triggering_actor != authority.actor
        or repository != REPOSITORY
        or run.get("run_attempt") != 1
        or not exact_run_identity(run, authority)
    ):
        fail("dispatched workflow run identity differs from its exact authority")
    return run


def wait_for_correlated_run(
    expected_run_id: int, authority: Authority, label: str, deadline: float
) -> dict[str, Any]:
    while time.monotonic() < deadline:
        candidates = []
        for run in workflow_runs():
            run_id = require_positive_integer(run.get("id"), "workflow run ID")
            if not exact_run_identity(run, authority):
                continue
            if run_has_exact_export_label(run, label):
                candidates.append(run)
        if len(candidates) > 1:
            fail("multiple workflow runs claimed the unique runner label")
        if len(candidates) == 1:
            if candidates[0].get("id") != expected_run_id:
                fail("a different workflow run claimed the unique runner label")
            return candidates[0]
        time.sleep(2)
    fail("timed out waiting for exact workflow/job correlation")


def request_jit_config(nonce: str) -> tuple[str, str]:
    runner_name = RUNNER_NAME_PREFIX + nonce
    runner_label = RUNNER_LABEL_PREFIX + nonce
    payload = {
        "name": runner_name,
        "runner_group_id": RUNNER_GROUP_ID,
        "labels": ["self-hosted", "linux", "x64", runner_label],
        "work_folder": "_work",
    }
    response = gh_json(
        f"repos/{REPOSITORY}/actions/runners/generate-jitconfig",
        method="POST",
        payload=payload,
    )
    if not isinstance(response, dict):
        fail("JIT configuration response is invalid")
    encoded = require_match(response.get("encoded_jit_config"), JIT_CONFIG_RE, "encoded JIT configuration")
    runner = response.get("runner")
    if runner is not None:
        if not isinstance(runner, dict) or runner.get("name") != runner_name:
            fail("JIT response runner identity differs")
    return runner_name, encoded


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
    if (
        image.get("Architecture") != "amd64"
        or image.get("Os") != "linux"
        or config.get("User") != EXPECTED_IMAGE_USER
        or config.get("WorkingDir") != EXPECTED_IMAGE_WORKDIR
        or IMAGE not in repo_digests
    ):
        fail("pinned runner image metadata differs from the governed contract")
    return image


def docker_optional_inspect(kind: str, name: str) -> dict[str, Any] | None:
    if kind not in {"container", "volume"}:
        fail("unsupported Docker identity kind")
    result = subprocess.run(
        ["docker", kind, "inspect", name],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        shell=False,
        check=False,
        timeout=15,
        env=command_environment("docker"),
    )
    if result.returncode != 0:
        return None
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError:
        fail(f"Docker {kind} inspection is invalid")
    if not isinstance(payload, list) or len(payload) != 1 or not isinstance(payload[0], dict):
        fail(f"Docker {kind} inspection is ambiguous")
    return payload[0]


def create_config_volume(nonce: str, encoded_config: str) -> str:
    volume = CONFIG_VOLUME_PREFIX + nonce
    if docker_optional_inspect("volume", volume) is not None:
        fail("JIT config volume name is already allocated")
    init_name = CONFIG_INIT_PREFIX + nonce
    write_name = CONFIG_WRITE_PREFIX + nonce
    common = (
        "docker", "run", "--rm", "--platform", "linux/amd64",
        "--network", "none", "--read-only", "--security-opt", "no-new-privileges:true",
        "--label", f"{OWNER_LABEL}=1", "--label", f"{NONCE_LABEL}={nonce}",
    )
    try:
        created = run_checked(
            (
                "docker", "volume", "create",
                "--label", f"{OWNER_LABEL}=1",
                "--label", f"{NONCE_LABEL}={nonce}",
                volume,
            ),
            kind="docker",
        ).strip()
        if created != volume:
            fail("Docker created an unexpected JIT config volume")
        run_checked(
            common
            + (
                "--name", init_name, "--user", "0:0", "--cap-drop", "ALL", "--cap-add", "CHOWN",
                "--mount", f"type=volume,src={volume},dst=/jit-config,volume-nocopy",
                "--entrypoint", "/bin/bash", IMAGE, "-c",
                "set -euo pipefail; chmod 0700 /jit-config; chown 1001:1001 /jit-config",
            ),
            kind="docker",
        )
        output = run_checked(
            common
            + (
                "--name", write_name, "--interactive", "--cap-drop", "ALL",
                "--mount", f"type=volume,src={volume},dst=/jit-config,volume-nocopy",
                "--entrypoint", "/bin/bash", IMAGE, "-c",
                "set -euo pipefail; umask 077; test ! -e /jit-config/encoded; "
                "IFS= read -r encoded; test -n \"$encoded\"; "
                "printf '%s' \"$encoded\" > /jit-config/encoded; unset encoded; "
                "test \"$(stat -c '%a:%u:%g' /jit-config/encoded)\" = 600:1001:1001",
            ),
            input_text=encoded_config + "\n",
            kind="docker",
        )
        if output:
            fail("JIT config writer produced unexpected output")
        return volume
    except Exception as original:
        cleanup_errors: list[Exception] = []
        for helper in (init_name, write_name):
            try:
                inspected = docker_optional_inspect("container", helper)
                if inspected is not None:
                    labels = inspected.get("Config", {}).get("Labels") or {}
                    if labels.get(OWNER_LABEL) != "1" or labels.get(NONCE_LABEL) != nonce:
                        fail("JIT config helper cleanup identity differs")
                    run_checked(
                        ("docker", "stop", "--time", "5", helper),
                        timeout=20,
                        kind="docker",
                    )
            except Exception as exc:
                cleanup_errors.append(exc)
        try:
            remove_config_volume(volume, nonce)
        except Exception as exc:
            cleanup_errors.append(exc)
        if cleanup_errors:
            raise LaunchError(
                "JIT config preparation and cleanup failed: "
                + "; ".join(str(exc) for exc in cleanup_errors)
            ) from original
        raise


def remove_config_volume(volume: str, nonce: str) -> None:
    inspected = docker_optional_inspect("volume", volume)
    if inspected is None:
        return
    labels = inspected.get("Labels") or {}
    if inspected.get("Name") != volume or labels.get(OWNER_LABEL) != "1" or labels.get(NONCE_LABEL) != nonce:
        fail("JIT config volume cleanup identity differs")
    removed = run_checked(("docker", "volume", "rm", volume), kind="docker").strip()
    if removed != volume:
        fail("Docker removed an unexpected volume")


def runner_docker_command(candidate_root: Path, volume: str, nonce: str) -> list[str]:
    container = CONTAINER_PREFIX + nonce
    return [
        "docker", "run", "--rm", "--platform", "linux/amd64", "--name", container,
        "--label", f"{OWNER_LABEL}=1", "--label", f"{NONCE_LABEL}={nonce}",
        "--cap-drop", "ALL", "--security-opt", "no-new-privileges:true",
        "--pids-limit", "1024", "--stop-timeout", "30",
        "--mount", f"type=bind,src={candidate_root},dst=/candidate-input,readonly,bind-propagation=rprivate",
        "--mount", f"type=volume,src={volume},dst=/jit-config,readonly,volume-nocopy",
        "--entrypoint", "/bin/bash", IMAGE, "-c",
        "set -euo pipefail; test \"$(stat -c '%a:%u:%g' /jit-config/encoded)\" = 600:1001:1001; "
        "jit_config=$(< /jit-config/encoded); test -n \"$jit_config\"; "
        "exec /home/runner/run.sh --jitconfig \"$jit_config\"",
    ]


def validate_running_container(inspected: dict[str, Any], candidate_root: Path, volume: str, nonce: str) -> None:
    config = inspected.get("Config", {})
    host = inspected.get("HostConfig", {})
    labels = config.get("Labels") or {}
    mounts = inspected.get("Mounts") or []
    if (
        inspected.get("Name") != "/" + CONTAINER_PREFIX + nonce
        or config.get("Image") != IMAGE
        or config.get("User") != EXPECTED_IMAGE_USER
        or labels.get(OWNER_LABEL) != "1"
        or labels.get(NONCE_LABEL) != nonce
        or host.get("Privileged") is not False
        or host.get("AutoRemove") is not True
        or host.get("NetworkMode") not in {"default", "bridge"}
        or host.get("PidsLimit") != 1024
        or "ALL" not in (host.get("CapDrop") or [])
        or "no-new-privileges:true" not in (host.get("SecurityOpt") or [])
        or len(mounts) != 2
    ):
        fail("running JIT container identity/isolation differs")
    expected = {
        ("bind", str(candidate_root), "/candidate-input", False),
        ("volume", volume, "/jit-config", False),
    }
    actual = {
        (row.get("Type"), row.get("Source") if row.get("Type") == "bind" else row.get("Name"), row.get("Destination"), row.get("RW"))
        for row in mounts if isinstance(row, dict)
    }
    if actual != expected:
        fail("running JIT container mounts differ from the exact two-mount boundary")


def stop_owned_container(nonce: str) -> None:
    name = CONTAINER_PREFIX + nonce
    inspected = docker_optional_inspect("container", name)
    if inspected is None:
        return
    labels = inspected.get("Config", {}).get("Labels") or {}
    if labels.get(OWNER_LABEL) != "1" or labels.get(NONCE_LABEL) != nonce or inspected.get("Name") != "/" + name:
        fail("container cleanup identity differs")
    run_checked(("docker", "stop", "--time", "10", name), timeout=30, kind="docker")


def execute_runner(candidate: CandidateIdentity, volume: str, nonce: str, deadline: float) -> None:
    name = CONTAINER_PREFIX + nonce
    if docker_optional_inspect("container", name) is not None:
        fail("JIT container name is already allocated")
    command = runner_docker_command(candidate.root, volume, nonce)
    process = subprocess.Popen(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        shell=False,
        env=command_environment("docker"),
    )
    try:
        inspected = None
        for _ in range(50):
            inspected = docker_optional_inspect("container", name)
            if inspected is not None:
                break
            if process.poll() is not None:
                fail("JIT container exited before its identity could be verified")
            time.sleep(0.1)
        if inspected is None:
            fail("JIT container did not become inspectable")
        validate_running_container(inspected, candidate.root, volume, nonce)
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            fail("JIT runner deadline expired")
        try:
            code = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            stop_owned_container(nonce)
            process.wait(timeout=30)
            fail("JIT runner exceeded the bounded wait")
        if code != 0:
            fail(f"JIT runner container failed with exit code {code}")
    finally:
        if process.poll() is None:
            stop_owned_container(nonce)
            process.wait(timeout=30)


def wait_for_workflow_success(run_id: int, authority: Authority, runner_name: str, label: str, deadline: float) -> tuple[dict[str, Any], dict[str, Any]]:
    while time.monotonic() < deadline:
        run = gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}")
        if not isinstance(run, dict) or require_positive_integer(run.get("id"), "workflow run ID") != run_id or not exact_run_identity(run, authority):
            fail("correlated workflow run identity changed")
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
            set(job.get("labels") or []) != set(EXPECTED_JOB_LABELS) | {label}
            or job.get("runner_name") != runner_name
            or job.get("conclusion") != "success"
        ):
            fail("completed export job differs from the exact JIT runner identity")
        if any(other is not job and other.get("runner_name") == runner_name for other in jobs):
            fail("JIT runner executed more than the exact export job")
        artifacts = gh_json(f"repos/{REPOSITORY}/actions/runs/{run_id}/artifacts?per_page=100")
        rows = artifacts.get("artifacts") if isinstance(artifacts, dict) else None
        artifact_total = artifacts.get("total_count") if isinstance(artifacts, dict) else None
        if type(artifact_total) is not int or artifact_total != 1 or not isinstance(rows, list) or len(rows) != 1:
            fail("completed workflow must have exactly one artifact")
        attempt = require_positive_integer(run.get("run_attempt"), "workflow run attempt")
        expected_name = f"preview-nightly-candidate-{run_id}-{attempt}"
        matches = [row for row in rows or [] if isinstance(row, dict) and row.get("name") == expected_name]
        if len(matches) != 1 or matches[0].get("expired") is not False:
            fail("completed workflow artifact identity is ambiguous or expired")
        return run, matches[0]
    fail("timed out waiting for the correlated workflow to complete")


def cleanup_runner_registration(runner_name: str, label: str) -> None:
    matches = []
    for runner in list_repository_runners():
        labels = {row.get("name") for row in runner.get("labels", []) if isinstance(row, dict)}
        if runner.get("name") == runner_name or label in labels:
            matches.append((runner, labels))
    if not matches:
        return
    if len(matches) != 1:
        fail("runner cleanup identity is ambiguous")
    runner, labels = matches[0]
    if runner.get("name") != runner_name or label not in labels:
        fail("runner cleanup identity differs")
    runner_id = require_positive_integer(runner.get("id"), "runner ID")
    gh_json(f"repos/{REPOSITORY}/actions/runners/{runner_id}", method="DELETE")


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


def write_receipt(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute() or path != Path(os.path.normpath(str(path))):
        fail("receipt output must be a canonical absolute path")
    parent = require_absolute_directory_no_links(path.parent, "receipt output parent")
    target = parent / path.name
    descriptor = os.open(
        target,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    try:
        data = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode()
        view = memoryview(data)
        while view:
            view = view[os.write(descriptor, view):]
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


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
    local_commit = verify_committed_local_authority(repo_root)
    authority = validate_remote_authority(local_commit)
    verify_docker_authority()
    exporter = load_trusted_exporter(repo_root)
    private = create_private_tree()
    volume: str | None = None
    runner_name: str | None = None
    nonce: str | None = None
    run_id: int | None = None
    completed = False
    try:
        candidate = materialize_candidate_subset(
            stage_root, private.path / "candidate-input", exporter
        )
        runners = list_repository_runners()
        nonce = generate_unique_nonce(runners)
        label = RUNNER_LABEL_PREFIX + nonce
        deadline = time.monotonic() + args.timeout_seconds
        dispatch_response = dispatch_workflow(candidate, authority, nonce)
        run_id = dispatch_run_id(dispatch_response)
        validate_dispatch_details(dispatch_response, run_id, authority)
        correlated = wait_for_correlated_run(run_id, authority, label, deadline)
        if require_positive_integer(
            correlated.get("id"), "correlated workflow run ID"
        ) != run_id:
            fail("correlated workflow run differs from the dispatched run")
        runner_name, encoded_config = request_jit_config(nonce)
        volume = create_config_volume(nonce, encoded_config)
        encoded_config = ""  # do not retain the bearer configuration beyond volume creation
        execute_runner(candidate, volume, nonce, deadline)
        run, artifact = wait_for_workflow_success(
            run_id, authority, runner_name, label, deadline
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
            "runnerName": runner_name,
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
            "completedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        }
        write_receipt(args.receipt_output, receipt)
        return receipt
    finally:
        primary_error = sys.exc_info()[1]
        errors: list[tuple[str, BaseException]] = []
        if nonce is not None:
            try:
                stop_owned_container(nonce)
            except BaseException as exc:
                errors.append(("stop_container", exc))
        if runner_name is not None and nonce is not None:
            try:
                cleanup_runner_registration(runner_name, RUNNER_LABEL_PREFIX + nonce)
            except BaseException as exc:
                errors.append(("delete_runner", exc))
        if run_id is not None and not completed:
            try:
                cancel_owned_run(run_id, authority)
            except BaseException as exc:
                errors.append(("cancel_workflow", exc))
        if volume is not None and nonce is not None:
            try:
                remove_config_volume(volume, nonce)
            except BaseException as exc:
                errors.append(("remove_config_volume", exc))
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
    try:
        receipt = orchestrate(args)
    except (LaunchError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"preview-nightly-jit-launch:error: {exc}", file=sys.stderr)
        for note in getattr(exc, "__notes__", ()):
            if isinstance(note, str) and note.startswith(CLEANUP_NOTE_PREFIX):
                print(f"preview-nightly-jit-launch:cleanup: {note}", file=sys.stderr)
        return 1
    print(f"workflow_run_id={receipt['runId']}")
    print(f"receipt={args.receipt_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
