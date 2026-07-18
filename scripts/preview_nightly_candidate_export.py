#!/usr/bin/env python3
"""Validate and materialize the exact preview-nightly Windows candidate subset.

This helper has no network or publication capability.  It accepts only a
read-only five-file candidate mount, verifies the canonical manifest and every
referenced byte, and emits a seven-file GitHub Actions artifact tree:

* the unchanged canonical manifest;
* the two Windows x64 bootstrap installers and their two payload ZIPs;
* a deterministic content inventory; and
* a workflow-run-bound export receipt.

The deterministic inventory is the reproducible candidate byte identity.  A
GitHub Actions artifact digest is a separate transport identity added by the
upload workflow after this helper completes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import sys
from pathlib import Path
from typing import Any


CONTENT_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-candidate-content-inventory"
EXPORT_CONTRACT = "chummer6-ui.preview-nightly-candidate-export"
CONTRACT_VERSION = 1
PRODUCER_WORKFLOW = ".github/workflows/preview-nightly-candidate-export.yml"
PRODUCER_REF = "refs/heads/main"
MANIFEST_CONTRACT = "Chummer.Hub.Registry.Contracts"
MANIFEST_PATH = "RELEASE_CHANNEL.generated.json"
CONTENT_INVENTORY_PATH = "PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json"
EXPORT_RECEIPT_PATH = "PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json"
HEADS = ("avalonia", "blazor-desktop")
RID = "win-x64"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
LOGIN_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$")
RUNNER_LABEL_PREFIX = "chummer-preview-nightly-export-"
RUNNER_NONCE_RE = re.compile(r"^[a-z0-9]{12,64}$")


def installer_path(head: str) -> str:
    return f"files/chummer-{head}-{RID}-installer.exe"


def payload_path(head: str) -> str:
    return f"files/chummer-{head}-{RID}-payload.zip"


CONTENT_PATHS = (
    MANIFEST_PATH,
    installer_path("avalonia"),
    payload_path("avalonia"),
    installer_path("blazor-desktop"),
    payload_path("blazor-desktop"),
)
OUTPUT_PATHS = (*CONTENT_PATHS, CONTENT_INVENTORY_PATH, EXPORT_RECEIPT_PATH)


class ContractError(RuntimeError):
    """Raised for a fail-closed candidate-export contract violation."""


def fail(message: str) -> None:
    raise ContractError(message)


def exact_text(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    return value


def selector_text(value: object) -> str:
    """Normalize only for locating a row that must later pass exact validation."""

    return value.strip().lower() if isinstance(value, str) else ""


def require_sha256(value: object, label: str) -> str:
    digest = exact_text(value, label)
    if not SHA256_RE.fullmatch(digest):
        fail(f"{label} must be an exact lowercase SHA-256")
    return digest


def require_commit(value: object, label: str) -> str:
    commit = exact_text(value, label)
    if not COMMIT_RE.fullmatch(commit):
        fail(f"{label} must be an exact lowercase 40-character commit SHA")
    return commit


def require_positive_integer(value: object, label: str) -> str:
    number = exact_text(value, label)
    if not POSITIVE_INTEGER_RE.fullmatch(number):
        fail(f"{label} must be a positive integer")
    return number


def require_version(value: object) -> str:
    version = exact_text(value, "expected version")
    if not VERSION_RE.fullmatch(version):
        fail("expected version is missing or is not portable")
    return version


def require_repository(value: object) -> str:
    repository = exact_text(value, "source repository")
    if not REPOSITORY_RE.fullmatch(repository):
        fail("source repository must be an exact owner/repository slug")
    return repository


def require_login(value: object) -> str:
    actor = exact_text(value, "source actor")
    if not LOGIN_RE.fullmatch(actor):
        fail("source actor must be an exact GitHub login")
    return actor


def require_runner_nonce(value: object) -> str:
    nonce = exact_text(value, "runner nonce")
    if not RUNNER_NONCE_RE.fullmatch(nonce):
        fail("runner nonce must be 12..64 lowercase ASCII letters or digits")
    return nonce


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    with path.open("x", encoding="utf-8") as handle:
        handle.write(json.dumps(payload, indent=2, sort_keys=True) + "\n")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        if not stat.S_ISREG(os.fstat(descriptor).st_mode):
            fail(f"candidate content is not a regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        fail(f"could not hash exact candidate content {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def regular_file_size(path: Path) -> int:
    try:
        metadata = os.stat(path, follow_symlinks=False)
    except OSError as exc:
        fail(f"could not stat exact candidate content {path}: {exc}")
    if not stat.S_ISREG(metadata.st_mode):
        fail(f"candidate content is not a regular file: {path}")
    return metadata.st_size


def copy_regular_no_follow(source: Path, target: Path) -> None:
    source_descriptor = -1
    target_descriptor = -1
    try:
        source_descriptor = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        if not stat.S_ISREG(os.fstat(source_descriptor).st_mode):
            fail(f"candidate input is not a regular file: {source}")
        target_descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with (
            os.fdopen(source_descriptor, "rb", closefd=True) as source_handle,
            os.fdopen(target_descriptor, "wb", closefd=True) as target_handle,
        ):
            source_descriptor = -1
            target_descriptor = -1
            shutil.copyfileobj(source_handle, target_handle, length=1024 * 1024)
    except OSError as exc:
        fail(f"could not copy exact candidate content {source}: {exc}")
    finally:
        if source_descriptor >= 0:
            os.close(source_descriptor)
        if target_descriptor >= 0:
            os.close(target_descriptor)


def validate_absolute_directory(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.is_symlink() or not path.is_dir():
        fail(f"{label} must be an absolute non-symlink directory")
    return path.resolve(strict=True)


def exact_regular_files(root: Path, label: str) -> list[str]:
    """Return the exact regular-file set while rejecting links and special files."""

    root = validate_absolute_directory(root, label)
    files: list[str] = []
    directories: set[str] = set()
    def walk_error(exc: OSError) -> None:
        fail(f"could not inspect exact {label} tree: {exc}")

    for current, dir_names, file_names in os.walk(
        root, topdown=True, onerror=walk_error, followlinks=False
    ):
        current_path = Path(current)
        for name in sorted(dir_names):
            path = current_path / name
            mode = os.lstat(path).st_mode
            relative = path.relative_to(root).as_posix()
            if stat.S_ISLNK(mode):
                fail(f"{label} cannot contain a directory symlink: {relative}")
            if not stat.S_ISDIR(mode):
                fail(f"{label} contains a non-directory tree entry: {relative}")
            directories.add(relative)
        for name in sorted(file_names):
            path = current_path / name
            mode = os.lstat(path).st_mode
            relative = path.relative_to(root).as_posix()
            if stat.S_ISLNK(mode):
                fail(f"{label} cannot contain a file symlink: {relative}")
            if not stat.S_ISREG(mode):
                fail(f"{label} contains a non-regular file: {relative}")
            files.append(relative)
    if directories != {"files"}:
        fail(f"{label} must contain only the files directory")
    return sorted(files)


def require_exact_tree(root: Path, expected: tuple[str, ...], label: str) -> None:
    actual = exact_regular_files(root, label)
    if actual != sorted(expected):
        missing = sorted(set(expected) - set(actual))
        extra = sorted(set(actual) - set(expected))
        fail(f"{label} file set differs; missing={missing}, extra={extra}")


def require_read_only_mount(root: Path) -> None:
    read_only_flag = getattr(os, "ST_RDONLY", 1)
    try:
        flags = os.statvfs(root).f_flag
    except OSError as exc:
        fail(f"could not inspect candidate input mount: {exc}")
    if flags & read_only_flag == 0:
        fail("candidate input root must be mounted read-only")


def require_exact_field(
    payload: dict[str, Any], key: str, expected: str, label: str
) -> None:
    if payload.get(key) != expected or not isinstance(payload.get(key), str):
        fail(f"{label} {key} must be exactly {expected!r}")


def require_exact_head_aliases(row: dict[str, Any], head: str) -> None:
    found = False
    for key in ("head", "headId"):
        if key not in row or row[key] is None:
            continue
        found = True
        if not isinstance(row[key], str) or row[key] != head:
            fail(f"candidate manifest {head} {key} must be exactly {head!r}")
    if not found:
        fail(f"candidate manifest {head} must have an exact head or headId")


def manifest_installer_row(manifest: dict[str, Any], head: str) -> dict[str, Any]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("candidate manifest artifacts must be a list")
    matches = [
        row
        for row in rows
        if isinstance(row, dict)
        and any(selector_text(row.get(key)) == head for key in ("head", "headId"))
        and selector_text(row.get("platform")) == "windows"
        and selector_text(row.get("rid")) == RID
        and selector_text(row.get("kind")) == "installer"
    ]
    if len(matches) != 1:
        fail(f"candidate manifest must contain exactly one {head}/{RID} Windows installer")
    return matches[0]


def positive_size(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        fail(f"{label} must be a positive byte count")
    return value


def validate_head(root: Path, manifest: dict[str, Any], head: str) -> dict[str, Any]:
    installer_relative = installer_path(head)
    payload_relative = payload_path(head)
    installer = root / installer_relative
    payload = root / payload_relative
    row = manifest_installer_row(manifest, head)
    require_exact_head_aliases(row, head)
    require_exact_field(row, "platform", "windows", f"candidate manifest {head}")
    require_exact_field(row, "rid", RID, f"candidate manifest {head}")
    require_exact_field(row, "kind", "installer", f"candidate manifest {head}")
    installer_sha = require_sha256(row.get("sha256"), f"{head} installer SHA-256")
    payload_sha = require_sha256(row.get("payloadSha256"), f"{head} payload SHA-256")
    installer_size = positive_size(row.get("sizeBytes"), f"{head} installer size")
    payload_size = positive_size(row.get("payloadSizeBytes"), f"{head} payload size")
    exact_names = {
        "fileName": installer.name,
        "payloadFileName": payload.name,
    }
    for key, value in exact_names.items():
        if row.get(key) != value or not isinstance(row.get(key), str):
            fail(f"candidate manifest {head} {key} differs from the exact export contract")
    exact_modes = {
        "installerMode": "bootstrap",
        "payloadAcquisitionMode": "download",
    }
    for key, value in exact_modes.items():
        if row.get(key) != value or not isinstance(row.get(key), str):
            fail(f"candidate manifest {head} {key} differs from the exact export contract")
    if sha256_file(installer) != installer_sha or regular_file_size(installer) != installer_size:
        fail(f"{head} installer bytes differ from the candidate manifest")
    if sha256_file(payload) != payload_sha or regular_file_size(payload) != payload_size:
        fail(f"{head} payload bytes differ from the candidate manifest")
    return {
        "headId": head,
        "rid": RID,
        "installer": {
            "relativePath": installer_relative,
            "fileName": installer.name,
            "sha256": installer_sha,
            "sizeBytes": installer_size,
        },
        "payload": {
            "relativePath": payload_relative,
            "fileName": payload.name,
            "sha256": payload_sha,
            "sizeBytes": payload_size,
        },
    }


def validate_candidate_root(
    root: Path, expected_version: str, expected_manifest_sha: str
) -> list[dict[str, Any]]:
    manifest_file = root / MANIFEST_PATH
    if sha256_file(manifest_file) != expected_manifest_sha:
        fail("candidate manifest bytes differ from the dispatched SHA-256")
    manifest = read_json(manifest_file, "candidate manifest")
    require_exact_field(manifest, "contractName", MANIFEST_CONTRACT, "candidate manifest")
    if "contract_name" in manifest:
        require_exact_field(
            manifest, "contract_name", MANIFEST_CONTRACT, "candidate manifest"
        )
    if type(manifest.get("schemaVersion")) is not int or manifest["schemaVersion"] != CONTRACT_VERSION:
        fail("candidate manifest schemaVersion must be exactly 1")
    for key in ("version", "releaseVersion"):
        require_exact_field(manifest, key, expected_version, "candidate manifest")
    for key in ("channelId", "channel"):
        require_exact_field(manifest, key, "preview", "candidate manifest")
    heads = [validate_head(root, manifest, head) for head in HEADS]
    all_binary_digests = [
        binding[kind]["sha256"]
        for binding in heads
        for kind in ("installer", "payload")
    ]
    if len(set(all_binary_digests)) != len(all_binary_digests):
        fail("the four candidate installer/payload files must have distinct SHA-256 digests")
    return heads


def content_rows(root: Path) -> list[dict[str, Any]]:
    return [
        {
            "path": relative,
            "sha256": sha256_file(root / relative),
            "sizeBytes": regular_file_size(root / relative),
        }
        for relative in sorted(CONTENT_PATHS)
    ]


def validate_source(args: argparse.Namespace) -> dict[str, str]:
    run_id = require_positive_integer(args.source_run_id, "source run ID")
    run_attempt = require_positive_integer(args.source_run_attempt, "source run attempt")
    workflow = exact_text(args.source_workflow, "source workflow")
    if workflow != PRODUCER_WORKFLOW:
        fail(f"source workflow must be exactly {PRODUCER_WORKFLOW}")
    ref = exact_text(args.source_ref, "source ref")
    if ref != PRODUCER_REF:
        fail(f"source ref must be exactly {PRODUCER_REF}")
    artifact_name = exact_text(args.artifact_name, "artifact name")
    expected_artifact = f"preview-nightly-candidate-{run_id}-{run_attempt}"
    if artifact_name != expected_artifact:
        fail("artifact name must be exactly bound to source run ID and attempt")
    runner_nonce = require_runner_nonce(args.runner_nonce)
    source_sha = require_commit(args.source_sha, "source SHA")
    expected_source_sha = require_commit(args.expected_source_sha, "expected source SHA")
    if source_sha != expected_source_sha:
        fail("source SHA differs from the explicitly authorized source SHA")
    return {
        "repository": require_repository(args.source_repository),
        "workflow": workflow,
        "runId": run_id,
        "runAttempt": run_attempt,
        "ref": ref,
        "sha": source_sha,
        "actor": require_login(args.source_actor),
        "artifactName": artifact_name,
        "runnerLabel": f"{RUNNER_LABEL_PREFIX}{runner_nonce}",
    }


def export_candidate(args: argparse.Namespace) -> str:
    input_root = validate_absolute_directory(args.input_root, "candidate input root")
    if args.require_read_only_input:
        require_read_only_mount(input_root)
    require_exact_tree(input_root, CONTENT_PATHS, "candidate input root")
    output_root = args.output_root
    if not output_root.is_absolute():
        fail("candidate output root must be absolute")
    if output_root.exists() or output_root.is_symlink():
        fail("candidate output root must not already exist")
    output_parent = output_root.parent
    if output_parent.is_symlink() or not output_parent.is_dir():
        fail("candidate output parent must be an existing non-symlink directory")
    output_parent = output_parent.resolve(strict=True)
    output_root = output_parent / output_root.name

    version = require_version(args.expected_version)
    expected_manifest_sha = require_sha256(
        args.expected_manifest_sha256, "expected candidate manifest SHA-256"
    )
    validate_candidate_root(input_root, version, expected_manifest_sha)
    source = validate_source(args)

    output_root.mkdir(mode=0o700)
    try:
        for relative in CONTENT_PATHS:
            target = output_root / relative
            target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
            copy_regular_no_follow(input_root / relative, target)
        require_exact_tree(output_root, CONTENT_PATHS, "candidate output content")
        output_content_rows = content_rows(output_root)
        if output_content_rows != content_rows(input_root):
            fail("candidate input changed while its exact bytes were copied")
        heads = validate_candidate_root(output_root, version, expected_manifest_sha)
        inventory = {
            "contractName": CONTENT_INVENTORY_CONTRACT,
            "contractVersion": CONTRACT_VERSION,
            "release": {"channel": "preview", "version": version},
            "manifest": {"path": MANIFEST_PATH, "sha256": expected_manifest_sha},
            "files": output_content_rows,
        }
        inventory_file = output_root / CONTENT_INVENTORY_PATH
        write_json(inventory_file, inventory)
        inventory_file.chmod(0o600)
        inventory_sha = sha256_file(inventory_file)
        receipt = {
            "contractName": EXPORT_CONTRACT,
            "contractVersion": CONTRACT_VERSION,
            "status": "exported",
            "release": {"channel": "preview", "version": version},
            "source": source,
            "candidateManifest": {"path": MANIFEST_PATH, "sha256": expected_manifest_sha},
            "contentInventory": {"path": CONTENT_INVENTORY_PATH, "sha256": inventory_sha},
            "heads": heads,
        }
        receipt_file = output_root / EXPORT_RECEIPT_PATH
        write_json(receipt_file, receipt)
        receipt_file.chmod(0o600)
        require_exact_tree(output_root, OUTPUT_PATHS, "candidate export root")
        return inventory_sha
    except Exception:
        shutil.rmtree(output_root, ignore_errors=True)
        raise


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--source-repository", required=True)
    parser.add_argument("--source-workflow", required=True)
    parser.add_argument("--source-run-id", required=True)
    parser.add_argument("--source-run-attempt", required=True)
    parser.add_argument("--source-ref", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--expected-source-sha", required=True)
    parser.add_argument("--source-actor", required=True)
    parser.add_argument("--artifact-name", required=True)
    parser.add_argument("--runner-nonce", required=True)
    parser.add_argument("--require-read-only-input", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        inventory_sha = export_candidate(args)
    except (ContractError, OSError) as exc:
        print(f"preview-nightly-candidate-export:error: {exc}", file=sys.stderr)
        return 1
    print(f"content_inventory_sha256={inventory_sha}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
