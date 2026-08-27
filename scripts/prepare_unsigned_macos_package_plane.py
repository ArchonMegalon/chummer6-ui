#!/usr/bin/env python3
"""Materialize and verify the isolated package plane for unsigned macOS proof."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping


LOCK_CONTRACT = "chummer6-ui.unsigned-macos-package-plane-lock/v1"
PREPARE_CONTRACT = "chummer6-ui.unsigned-macos-package-plane-prepare/v1"
MANIFEST_CONTRACT = "chummer6-ui.unsigned-macos-package-manifest/v1"
RESOLUTION_CONTRACT = "chummer6-ui.unsigned-macos-package-resolution/v1"
OWNER_FEED_VALIDATION_CONTRACT = "chummer6-ui.core-owner-feed-validation/v1"
SOURCE_FEED_VALIDATION_CONTRACT = "chummer6-ui.linux-source-feed-validation/v1"
CORE_HANDOFF_CONTRACT = "chummer-core.runtime-package-public-handoff/v2"
CORE_RECEIPT_CONTRACT = "chummer-core.no-siblings-package-plane/v3"
CORE_RUNTIME_INVENTORY_CONTRACT = "chummer-core.runtime-package-inventory/v1"
OWNER_INVENTORY_CONTRACT = "chummer-core.owner-contract-package-inventory/v1"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
SHA512_RE = re.compile(r"^[0-9a-f]{128}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
VERSION_RE = re.compile(
    r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:\.(?:0|[1-9][0-9]*))?"
    r"(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$"
)
SAFE_NAME_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,199}$")
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_DOWNLOAD_BYTES = 512 * 1024 * 1024
MAX_SDK_EXPANDED_BYTES = 2 * 1024 * 1024 * 1024
ALLOWED_DOWNLOAD_HOSTS = frozenset(
    {
        "api.nuget.org",
        "builds.dotnet.microsoft.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    }
)


class PackagePlaneError(ValueError):
    pass


def fail(message: str) -> None:
    raise PackagePlaneError(message)


def reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate key {key!r}")
        result[key] = value
    return result


def strict_json_bytes(raw: bytes, label: str) -> dict[str, Any]:
    if not raw or len(raw) > MAX_JSON_BYTES:
        fail(f"{label} is empty or exceeds the fixed size bound")
    try:
        value = json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=reject_duplicate_pairs,
            parse_constant=lambda token: fail(f"{label} contains {token}"),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"{label} is not strict UTF-8 JSON: {error}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def strict_json_file(path: Path, label: str) -> dict[str, Any]:
    return strict_json_bytes(exact_file_bytes(path, label), label)


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def exact_file_bytes(path: Path, label: str, *, maximum: int = MAX_DOWNLOAD_BYTES) -> bytes:
    try:
        metadata = path.lstat()
    except OSError as error:
        fail(f"{label} is unavailable: {error}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode):
        fail(f"{label} must be a regular non-symlink file")
    if metadata.st_size < 1 or metadata.st_size > maximum:
        fail(f"{label} has an invalid size")
    with path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        raw = stream.read(maximum + 1)
        after = os.fstat(stream.fileno())
    if (
        len(raw) != before.st_size
        or before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or before.st_size != after.st_size
        or before.st_mtime_ns != after.st_mtime_ns
    ):
        fail(f"{label} changed during its stable read")
    return raw


def atomic_json(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail("JSON output must be one new absolute path")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            os.fchmod(stream.fileno(), 0o600)
            stream.write((json.dumps(payload, indent=2, sort_keys=True) + "\n").encode())
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def atomic_text(path: Path, value: str) -> None:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail("text output must be one new absolute path")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            os.fchmod(stream.fileno(), 0o600)
            stream.write(value)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def require_commit(value: object, label: str) -> str:
    token = str(value or "").lower()
    if COMMIT_RE.fullmatch(token) is None:
        fail(f"{label} is not one exact commit")
    return token


def require_sha256(value: object, label: str) -> str:
    token = str(value or "").lower()
    if SHA256_RE.fullmatch(token) is None:
        fail(f"{label} is not one exact SHA-256")
    return token


def require_version(value: object, label: str) -> str:
    token = str(value or "")
    if VERSION_RE.fullmatch(token) is None:
        fail(f"{label} is not one exact package version")
    return token


def require_new_directory(path: Path, label: str) -> None:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail(f"{label} must be one absent absolute path")
    if path.parent.resolve(strict=True) != path.parent:
        fail(f"{label} parent must be a physical canonical directory")


def git_value(root: Path, *arguments: str) -> str:
    completed = subprocess.run(
        ("git", "-C", str(root), *arguments),
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )
    if completed.returncode != 0:
        fail(f"git authority failed for {root.name}: {(completed.stderr or '').strip()}")
    return completed.stdout.strip()


def normalized_github_origin(value: str) -> str:
    return value.removesuffix(".git")


def repository_identity(
    root: Path,
    *,
    repository: str,
    commit: str,
    tree: str | None = None,
    require_no_untracked: bool = False,
) -> dict[str, str]:
    if not root.is_absolute() or root.is_symlink() or not root.is_dir():
        fail("repository authority must be one real absolute directory")
    observed_commit = require_commit(git_value(root, "rev-parse", "HEAD"), "repository commit")
    if observed_commit != commit:
        fail(f"repository commit differs for {root.name}")
    observed_tree = require_commit(git_value(root, "rev-parse", "HEAD^{tree}"), "repository tree")
    if tree is not None and observed_tree != tree:
        fail(f"repository tree differs for {root.name}")
    origin = git_value(root, "remote", "get-url", "origin")
    if normalized_github_origin(origin) != normalized_github_origin(repository):
        fail(f"repository origin differs for {root.name}")
    status_args = ["status", "--porcelain=v1"]
    status_args.append("--untracked-files=all" if require_no_untracked else "--untracked-files=no")
    if git_value(root, *status_args):
        fail(f"repository source is not clean for {root.name}")
    return {"commit": observed_commit, "tree": observed_tree}


def validate_ui_recipe(root: Path, lock: dict[str, Any]) -> dict[str, Any]:
    base = require_commit(lock.get("uiBaseCommit"), "UI base commit")
    head = require_commit(git_value(root, "rev-parse", "HEAD"), "UI recipe commit")
    if subprocess.run(
        ("git", "-C", str(root), "merge-base", "--is-ancestor", base, head),
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        timeout=30,
    ).returncode != 0:
        fail("UI proof recipe is not descended from the exact product source")
    allowed = lock.get("allowedRecipeDelta")
    if not isinstance(allowed, list) or not allowed or allowed != sorted(set(allowed)):
        fail("allowed UI recipe delta is not one canonical nonempty path list")
    observed = git_value(root, "diff", "--name-only", f"{base}..{head}").splitlines()
    if not observed or set(observed) - set(allowed):
        fail("UI recipe delta contains an unapproved product-source change")
    if git_value(root, "status", "--porcelain=v1", "--untracked-files=all"):
        fail("UI proof checkout is not fresh and clean")
    return {
        "allowedRecipeDelta": allowed,
        "baseCommit": base,
        "recipeCommit": head,
        "recipeDelta": sorted(observed),
        "tree": require_commit(git_value(root, "rev-parse", "HEAD^{tree}"), "UI tree"),
    }


def validate_lock(lock: dict[str, Any], rid: str | None = None) -> None:
    expected_keys = {
        "allowedRecipeDelta",
        "commonExternalPackages",
        "contract",
        "coreAuthority",
        "extraCommonExternalPackages",
        "globalExternalPackageLock",
        "locallyPackedPackages",
        "ownerPackageIds",
        "ridExternalPackages",
        "sdk",
        "uiBaseCommit",
    }
    if set(lock) != expected_keys or lock.get("contract") != LOCK_CONTRACT:
        fail("unsigned macOS package-plane lock schema is invalid")
    require_commit(lock.get("uiBaseCommit"), "UI base commit")
    core = lock.get("coreAuthority")
    if not isinstance(core, dict):
        fail("Core authority is missing")
    require_commit(core.get("commit"), "Core authority commit")
    require_commit(core.get("runtimeSourceCommit"), "Core runtime source commit")
    require_commit(core.get("tree"), "Core authority tree")
    sdk = lock.get("sdk")
    if not isinstance(sdk, dict) or sdk.get("version") != "10.0.103":
        fail("macOS SDK authority is invalid")
    if rid is not None:
        if rid not in {"linux-x64", "osx-arm64", "osx-x64"} or not isinstance(
            sdk.get(rid), dict
        ):
            fail("SDK RID is not supported by the package-plane lock")
        if rid.startswith("osx-") and not isinstance(
            lock.get("ridExternalPackages", {}).get(rid), list
        ):
            fail("RID external package authority is missing")


def load_lock(path: Path, rid: str | None = None) -> dict[str, Any]:
    lock = strict_json_file(path, "unsigned macOS package-plane lock")
    validate_lock(lock, rid)
    return lock


def safe_url(value: object, label: str) -> str:
    token = str(value or "")
    parsed = urllib.parse.urlsplit(token)
    if (
        parsed.scheme != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.hostname.casefold() not in ALLOWED_DOWNLOAD_HOSTS
        or parsed.fragment
    ):
        fail(f"{label} is not one credential-free allowlisted HTTPS URL")
    return token


class StrictRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request: Any, fp: Any, code: int, msg: str, headers: Any, newurl: str) -> Any:
        safe_url(newurl, "redirect target")
        return super().redirect_request(request, fp, code, msg, headers, newurl)


def download_exact(
    row: dict[str, Any],
    target: Path,
    *,
    algorithm: str = "sha256",
    require_size: bool = True,
) -> dict[str, Any]:
    if target.exists() or target.is_symlink() or not target.is_absolute():
        fail("download target must be one new absolute path")
    url = safe_url(row.get("source") or row.get("url") or row.get("archiveUrl"), "download source")
    expected_size = row.get("sizeBytes", row.get("archiveSizeBytes"))
    if expected_size is None and not require_size:
        pass
    elif (
        not isinstance(expected_size, int)
        or isinstance(expected_size, bool)
        or not 0 < expected_size <= MAX_DOWNLOAD_BYTES
    ):
        fail("download size authority is invalid")
    digest_key = "sha256" if algorithm == "sha256" else "archiveSha512"
    expected_digest = str(row.get(digest_key) or "").lower()
    pattern = SHA256_RE if algorithm == "sha256" else SHA512_RE
    if pattern.fullmatch(expected_digest) is None:
        fail("download digest authority is invalid")
    target.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    digest = hashlib.new(algorithm)
    observed_size = 0
    opener = urllib.request.build_opener(StrictRedirectHandler())
    try:
        request = urllib.request.Request(url, headers={"User-Agent": "chummer6-macos-proof/1"})
        with opener.open(request, timeout=120) as response, os.fdopen(descriptor, "wb") as stream:
            descriptor = -1
            safe_url(response.geturl(), "download final URL")
            content_length = response.headers.get("Content-Length")
            if content_length is not None:
                advertised_size = int(content_length)
                if advertised_size < 1 or advertised_size > MAX_DOWNLOAD_BYTES:
                    fail("download Content-Length exceeds the fixed size bound")
                if expected_size is not None and advertised_size != expected_size:
                    fail("download Content-Length differs from the locked size")
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                observed_size += len(chunk)
                if observed_size > MAX_DOWNLOAD_BYTES or (
                    expected_size is not None and observed_size > expected_size
                ):
                    fail("download exceeds the fixed or locked size")
                digest.update(chunk)
                stream.write(chunk)
            stream.flush()
            os.fsync(stream.fileno())
        if (
            observed_size < 1
            or (expected_size is not None and observed_size != expected_size)
            or digest.hexdigest() != expected_digest
        ):
            fail("download bytes differ from the locked size or digest")
    except BaseException:
        if descriptor >= 0:
            os.close(descriptor)
        target.unlink(missing_ok=True)
        raise
    return {
        "fileName": target.name,
        "sha256" if algorithm == "sha256" else "sha512": expected_digest,
        "sizeBytes": observed_size,
        "source": url,
    }


def safe_archive_name(raw: str, label: str) -> str:
    normalized = raw.rstrip("/")
    if normalized in {".", "./"}:
        return "."
    if normalized.startswith("./"):
        normalized = normalized[2:]
    pure = PurePosixPath(normalized)
    if (
        not raw
        or raw.startswith("/")
        or "\\" in raw
        or "\x00" in raw
        or any(part in {"", ".", ".."} for part in pure.parts)
        or pure.as_posix() != normalized
    ):
        fail(f"{label} contains an unsafe archive path")
    return pure.as_posix()


def extract_sdk_archive(archive: Path, destination: Path, expected_sha512: str, expected_size: int) -> None:
    require_new_directory(destination, "SDK destination")
    with archive.open("rb") as stream:
        metadata = os.fstat(stream.fileno())
        if metadata.st_size != expected_size or not stat.S_ISREG(metadata.st_mode):
            fail("SDK archive size or file type differs")
        digest = hashlib.sha512()
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
        if digest.hexdigest() != expected_sha512:
            fail("SDK archive SHA-512 differs")
        stream.seek(0)
        destination.mkdir(mode=0o700)
        try:
            with tarfile.open(fileobj=stream, mode="r:gz") as archive_stream:
                members = archive_stream.getmembers()
                names: set[str] = set()
                total_size = 0
                for member in members:
                    name = safe_archive_name(member.name, "SDK archive")
                    folded = name.casefold()
                    if folded in names:
                        fail("SDK archive contains a duplicate portable path")
                    names.add(folded)
                    if name == ".":
                        if not member.isdir():
                            fail("SDK archive root entry is not a directory")
                        continue
                    if not (member.isdir() or member.isfile()):
                        fail("SDK archive contains a link or special entry")
                    if member.isfile():
                        total_size += member.size
                        if total_size > MAX_SDK_EXPANDED_BYTES:
                            fail("SDK archive exceeds the expanded size bound")
                for member in members:
                    name = safe_archive_name(member.name, "SDK archive")
                    if name == ".":
                        continue
                    target = destination.joinpath(*PurePosixPath(name).parts)
                    if member.isdir():
                        target.mkdir(mode=0o755, parents=True, exist_ok=True)
                        continue
                    target.parent.mkdir(mode=0o755, parents=True, exist_ok=True)
                    source = archive_stream.extractfile(member)
                    if source is None:
                        fail("SDK archive regular file could not be opened")
                    mode = 0o755 if member.mode & 0o111 else 0o644
                    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode)
                    with source, os.fdopen(descriptor, "wb") as output:
                        shutil.copyfileobj(source, output, 1024 * 1024)
                        output.flush()
                        os.fsync(output.fileno())
        except BaseException:
            shutil.rmtree(destination, ignore_errors=True)
            raise


def acquire_sdk(args: argparse.Namespace) -> dict[str, Any]:
    lock = load_lock(args.lock, args.rid)
    sdk = lock["sdk"]
    row = sdk[args.rid]
    require_new_directory(args.download_root, "SDK download root")
    args.download_root.mkdir(mode=0o700)
    archive_name = PurePosixPath(safe_url(row["archiveUrl"], "SDK archive URL")).name
    if SAFE_NAME_RE.fullmatch(archive_name) is None:
        fail("SDK archive filename is not portable")
    archive = args.download_root / archive_name
    inventory = download_exact(row, archive, algorithm="sha512")
    extract_sdk_archive(archive, args.destination, row["archiveSha512"], row["archiveSizeBytes"])
    dotnet = args.destination / "dotnet"
    if dotnet.is_symlink() or not dotnet.is_file() or not os.access(dotnet, os.X_OK):
        fail("extracted SDK does not contain one real executable dotnet host")
    environment = os.environ.copy()
    environment["DOTNET_ROOT"] = str(args.destination)
    environment["PATH"] = f"{args.destination}{os.pathsep}{environment.get('PATH', '')}"
    completed = subprocess.run(
        (str(dotnet), "--version"),
        check=False,
        capture_output=True,
        text=True,
        env=environment,
        timeout=30,
    )
    if completed.returncode != 0 or completed.stdout.strip() != sdk["version"]:
        fail("extracted SDK runtime identity differs from the exact version")
    receipt = {
        "archive": inventory,
        "contract": "chummer6-ui.unsigned-macos-sdk/v1",
        "rid": args.rid,
        "status": "pass",
        "version": sdk["version"],
    }
    atomic_json(args.output, receipt)
    return receipt


def package_row(value: dict[str, Any], *, source_role: str) -> dict[str, Any]:
    package_id = str(value.get("packageId") or value.get("id") or "")
    version = require_version(value.get("version"), f"{package_id} version")
    file_name = str(value.get("fileName") or value.get("file_name") or "")
    if not package_id or SAFE_NAME_RE.fullmatch(file_name) is None:
        fail("package row identity or filename is invalid")
    size = value.get("sizeBytes", value.get("size_bytes"))
    if not isinstance(size, int) or isinstance(size, bool) or size < 1:
        fail(f"package size is invalid for {package_id}")
    return {
        "fileName": file_name,
        "packageId": package_id,
        "sha256": require_sha256(value.get("sha256"), f"{package_id} digest"),
        "sizeBytes": size,
        "sourceRole": source_role,
        "version": version,
    }


def global_external_rows(repo_root: Path, lock: dict[str, Any]) -> dict[tuple[str, str], dict[str, Any]]:
    authority = lock["globalExternalPackageLock"]
    relative = PurePosixPath(str(authority.get("path") or ""))
    if relative.is_absolute() or ".." in relative.parts:
        fail("global external package lock path is unsafe")
    path = repo_root.joinpath(*relative.parts)
    if sha256_file(path) != require_sha256(authority.get("sha256"), "global package lock digest"):
        fail("global external package lock bytes differ")
    payload = strict_json_file(path, "global external package lock")
    rows = payload.get("externalPackages")
    if not isinstance(rows, list):
        fail("global external package rows are missing")
    result: dict[tuple[str, str], dict[str, Any]] = {}
    for raw in rows:
        if not isinstance(raw, dict):
            fail("global external package row is malformed")
        key = (str(raw.get("packageId") or "").casefold(), str(raw.get("version") or ""))
        if key in result:
            fail("global external package identity is duplicated")
        package_id = str(raw.get("packageId") or "")
        file_name = str(raw.get("fileName") or "")
        if not package_id or SAFE_NAME_RE.fullmatch(file_name) is None:
            fail("global external package identity or filename is invalid")
        require_version(raw.get("version"), f"{package_id} version")
        require_sha256(raw.get("sha256"), f"{package_id} digest")
        safe_url(raw.get("source"), "global external package source")
        result[key] = raw
    return result


def selected_external_rows(repo_root: Path, lock: dict[str, Any], rid: str) -> list[dict[str, Any]]:
    available = global_external_rows(repo_root, lock)
    result: list[dict[str, Any]] = []
    identities: set[tuple[str, str]] = set()
    for selection in lock["commonExternalPackages"]:
        if not isinstance(selection, dict):
            fail("common external package selection is malformed")
        key = (str(selection.get("packageId") or "").casefold(), str(selection.get("version") or ""))
        raw = available.get(key)
        if raw is None:
            fail(f"selected external package is absent from global authority: {key[0]}")
        identities.add(key)
        result.append(raw)
    for raw in [*lock["extraCommonExternalPackages"], *lock["ridExternalPackages"][rid]]:
        if not isinstance(raw, dict):
            fail("dedicated external package row is malformed")
        package_row(raw, source_role="locked_external")
        safe_url(raw.get("source"), "dedicated external package source")
        key = (str(raw["packageId"]).casefold(), str(raw["version"]))
        if key in identities:
            fail("external package identity is duplicated across authorities")
        identities.add(key)
        result.append(raw)
    return sorted(result, key=lambda row: (str(row["packageId"]).casefold(), str(row["version"])))


def public_handoff(
    lock: dict[str, Any], download_root: Path
) -> tuple[dict[str, Any], bytes, bytes]:
    handoff = lock["coreAuthority"]["publicHandoff"]
    receipt_row = handoff["receipt"]
    bundle_row = handoff["bundle"]
    receipt_path = download_root / receipt_row["fileName"]
    bundle_path = download_root / bundle_row["fileName"]
    download_exact(receipt_row, receipt_path)
    download_exact(bundle_row, bundle_path)
    receipt_raw = exact_file_bytes(receipt_path, "Core public handoff receipt", maximum=MAX_JSON_BYTES)
    bundle_raw = exact_file_bytes(bundle_path, "Core public handoff bundle")
    receipt = strict_json_bytes(receipt_raw, "Core public handoff receipt")
    core = lock["coreAuthority"]
    if (
        receipt.get("contract") != CORE_HANDOFF_CONTRACT
        or receipt.get("repository") != "ArchonMegalon/chummer6-core"
        or receipt.get("commit") != core["commit"]
        or receipt.get("ref") != "refs/heads/main"
        or receipt.get("release_tag") != handoff["tag"]
        or receipt.get("receipt_asset_name") != receipt_row["fileName"]
    ):
        fail("Core public handoff authority differs")
    bundle = receipt.get("bundle")
    if not isinstance(bundle, dict) or (
        bundle.get("asset_name") != bundle_row["fileName"]
        or bundle.get("sha256") != bundle_row["sha256"]
        or bundle.get("size_bytes") != bundle_row["sizeBytes"]
    ):
        fail("Core public handoff bundle binding differs")
    source = receipt.get("source_actions_artifact")
    workflow = source.get("workflow_run") if isinstance(source, dict) else None
    if not isinstance(workflow, dict) or (
        workflow.get("event") != "push"
        or workflow.get("head_branch") != "main"
        or workflow.get("head_sha") != core["commit"]
        or workflow.get("head_tree") != core["tree"]
        or workflow.get("repository") != "ArchonMegalon/chummer6-core"
        or workflow.get("workflow_sha") != core["commit"]
    ):
        fail("Core public handoff workflow identity differs")
    return receipt, receipt_raw, bundle_raw


def bundle_members(receipt: dict[str, Any], bundle_raw: bytes) -> dict[str, bytes]:
    bundle = receipt["bundle"]
    rows = bundle.get("members")
    if not isinstance(rows, list) or len(rows) != bundle.get("member_count"):
        fail("Core public handoff member inventory is incomplete")
    expected: dict[str, dict[str, Any]] = {}
    for raw in rows:
        if not isinstance(raw, dict):
            fail("Core public handoff member row is malformed")
        name = safe_archive_name(str(raw.get("path") or ""), "Core public handoff")
        if name in expected:
            fail("Core public handoff repeats a member")
        require_sha256(raw.get("sha256"), f"Core member {name} digest")
        if not isinstance(raw.get("size_bytes"), int) or raw["size_bytes"] < 1:
            fail("Core public handoff member size is invalid")
        expected[name] = raw
    result: dict[str, bytes] = {}
    with zipfile.ZipFile(__import__("io").BytesIO(bundle_raw)) as archive:
        infos = archive.infolist()
        if len(infos) != len(expected):
            fail("Core public handoff ZIP member count differs")
        for info in infos:
            name = safe_archive_name(info.filename, "Core public handoff ZIP")
            file_type = (info.external_attr >> 16) & 0o170000
            if (
                info.is_dir()
                or file_type not in {0, stat.S_IFREG}
                or name not in expected
            ):
                fail("Core public handoff ZIP contains an unexpected entry")
            raw = archive.read(info)
            row = expected[name]
            if len(raw) != row["size_bytes"] or sha256_bytes(raw) != row["sha256"]:
                fail(f"Core public handoff member bytes differ: {name}")
            result[name] = raw
    if set(result) != set(expected):
        fail("Core public handoff ZIP member set differs")
    return result


def copy_new_bytes(target: Path, raw: bytes) -> None:
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())


def verify_owner_feed(
    owner_feed: Path,
    core_receipt: dict[str, Any],
    selected_ids: set[str],
    stage: Path,
) -> list[dict[str, Any]]:
    inventory_path = owner_feed / "chummer-owner-contracts.inventory.json"
    inventory_raw = exact_file_bytes(inventory_path, "Core owner-contract inventory", maximum=MAX_JSON_BYTES)
    if sha256_bytes(inventory_raw) != core_receipt.get("package_inventory_sha256"):
        fail("Core owner-contract inventory does not match the public no-siblings receipt")
    inventory = strict_json_bytes(inventory_raw, "Core owner-contract inventory")
    if inventory.get("contract") != OWNER_INVENTORY_CONTRACT:
        fail("Core owner-contract inventory contract differs")
    rows = inventory.get("packages")
    locked = core_receipt.get("locked_packages")
    if not isinstance(rows, list) or not isinstance(locked, list):
        fail("Core owner-contract package rows are missing")
    locked_by_id = {str(row.get("id")): row for row in locked if isinstance(row, dict)}
    selected: list[dict[str, Any]] = []
    for raw in rows:
        if not isinstance(raw, dict) or raw.get("id") not in selected_ids:
            continue
        row = package_row(raw, source_role="core_locked_owner")
        public_row = locked_by_id.get(row["packageId"])
        if not isinstance(public_row, dict) or (
            public_row.get("version") != row["version"]
            or public_row.get("sha256") != row["sha256"]
            or public_row.get("size_bytes") != row["sizeBytes"]
        ):
            fail(f"Core locked owner authority differs for {row['packageId']}")
        source = owner_feed / row["fileName"]
        raw_bytes = exact_file_bytes(source, f"owner package {row['packageId']}")
        if len(raw_bytes) != row["sizeBytes"] or sha256_bytes(raw_bytes) != row["sha256"]:
            fail(f"Core locked owner package bytes differ for {row['packageId']}")
        copy_new_bytes(stage / row["fileName"], raw_bytes)
        selected.append(row)
    if {row["packageId"] for row in selected} != selected_ids:
        fail("Core owner-contract feed does not contain the exact selected package set")
    return selected


def validate_owner_feed_authority(args: argparse.Namespace) -> dict[str, Any]:
    lock = load_lock(args.lock)
    core = lock["coreAuthority"]
    core_identity = repository_identity(
        args.core_authority,
        repository=core["repository"],
        commit=core["commit"],
        tree=core["tree"],
        require_no_untracked=True,
    )
    if git_value(args.core_authority, "rev-parse", "HEAD^") != core["runtimeSourceCommit"]:
        fail("Core package authority is not directly grounded in the runtime source")
    validator = args.core_authority / "scripts/ai/runtime-package-plane.py"
    completed = subprocess.run(
        (sys.executable, str(validator), "--repo-root", str(args.core_authority)),
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )
    if completed.returncode != 0:
        fail(f"Core runtime package authority rejected its checkout: {completed.stderr.strip()}")

    require_new_directory(args.download_root, "owner-feed validation download root")
    args.download_root.mkdir(mode=0o700)
    handoff, handoff_raw, bundle_raw = public_handoff(lock, args.download_root)
    members = bundle_members(handoff, bundle_raw)
    core_receipt = strict_json_bytes(
        members["no-siblings.v3.receipt.json"], "Core no-siblings receipt"
    )
    if (
        core_receipt.get("contract") != CORE_RECEIPT_CONTRACT
        or core_receipt.get("status") != "pass"
        or core_receipt.get("core_commit") != core["commit"]
        or core_receipt.get("runtime_source_commit") != core["runtimeSourceCommit"]
        or core_receipt.get("package_recipe_commit") != core["commit"]
        or core_receipt.get("no_sibling_directories") is not True
        or core_receipt.get("isolated_package_cache") is not True
    ):
        fail("Core no-siblings receipt authority differs for the owner feed")
    locked = core_receipt.get("locked_packages")
    if not isinstance(locked, list) or not locked:
        fail("Core locked owner package rows are missing")
    selected_ids = {
        str(row.get("id") or "") for row in locked if isinstance(row, dict)
    }
    if "" in selected_ids or len(selected_ids) != len(locked):
        fail("Core locked owner package identities are invalid")

    inventory_path = args.owner_feed / "chummer-owner-contracts.inventory.json"
    inventory_raw = exact_file_bytes(
        inventory_path, "Core owner-contract inventory", maximum=MAX_JSON_BYTES
    )
    inventory = strict_json_bytes(inventory_raw, "Core owner-contract inventory")
    inventory_rows = inventory.get("packages")
    if not isinstance(inventory_rows, list):
        fail("Core owner-contract inventory rows are missing")
    expected_names = {"chummer-owner-contracts.inventory.json"}
    for raw in inventory_rows:
        if not isinstance(raw, dict):
            fail("Core owner-contract inventory row is malformed")
        expected_names.add(package_row(raw, source_role="core_locked_owner")["fileName"])
    observed_names: set[str] = set()
    feed_inventory: list[dict[str, Any]] = []
    if not args.owner_feed.is_absolute() or args.owner_feed.is_symlink() or not args.owner_feed.is_dir():
        fail("Core owner-contract feed must be one real absolute directory")
    for path in sorted(args.owner_feed.iterdir(), key=lambda value: value.name):
        raw = exact_file_bytes(path, f"Core owner-feed member {path.name}")
        observed_names.add(path.name)
        feed_inventory.append(
            {
                "fileName": path.name,
                "sha256": sha256_bytes(raw),
                "sizeBytes": len(raw),
            }
        )
    if observed_names != expected_names:
        fail("Core owner-contract feed member set differs from its exact inventory")

    with tempfile.TemporaryDirectory(
        prefix="owner-feed-validation.", dir=args.download_root
    ) as stage_name:
        rows = verify_owner_feed(
            args.owner_feed,
            core_receipt,
            selected_ids,
            Path(stage_name),
        )
    receipt = {
        "contract": OWNER_FEED_VALIDATION_CONTRACT,
        "coreAuthority": {
            "commit": core["commit"],
            "publicHandoffReceiptSha256": sha256_bytes(handoff_raw),
            "runtimeSourceCommit": core["runtimeSourceCommit"],
            "tree": core_identity["tree"],
        },
        "feedInventory": feed_inventory,
        "feedInventorySha256": feed_inventory_sha256(feed_inventory),
        "ownerPackageInventorySha256": sha256_bytes(inventory_raw),
        "packages": sorted(rows, key=lambda row: (row["packageId"].casefold(), row["version"])),
        "status": "pass",
    }
    atomic_json(args.output, receipt)
    return receipt


def core_package_rows(
    lock: dict[str, Any],
    members: dict[str, bytes],
    core_receipt: dict[str, Any],
    stage: Path,
) -> list[dict[str, Any]]:
    runtime_inventory = strict_json_bytes(
        members["chummer-core-runtime-packages.inventory.json"],
        "Core runtime package inventory",
    )
    core = lock["coreAuthority"]
    if (
        runtime_inventory.get("contract") != CORE_RUNTIME_INVENTORY_CONTRACT
        or runtime_inventory.get("runtime_source_commit") != core["runtimeSourceCommit"]
        or runtime_inventory.get("package_recipe_commit") != core["commit"]
        or core_receipt.get("contract") != CORE_RECEIPT_CONTRACT
        or core_receipt.get("status") != "pass"
        or core_receipt.get("core_commit") != core["commit"]
        or core_receipt.get("runtime_source_commit") != core["runtimeSourceCommit"]
        or core_receipt.get("package_recipe_commit") != core["commit"]
        or core_receipt.get("no_sibling_directories") is not True
        or core_receipt.get("isolated_package_cache") is not True
    ):
        fail("Core runtime/no-siblings receipt authority differs")
    if sha256_bytes(members["chummer-core-runtime-packages.inventory.json"]) != core_receipt.get(
        "runtime_package_inventory_sha256"
    ):
        fail("Core runtime inventory digest differs from the no-siblings receipt")
    selected_ids = set(core["runtimePackageIds"])
    rows = runtime_inventory.get("packages")
    if not isinstance(rows, list):
        fail("Core runtime package rows are missing")
    selected: list[dict[str, Any]] = []
    for raw in rows:
        if not isinstance(raw, dict) or raw.get("id") not in selected_ids:
            continue
        row = package_row(raw, source_role="core_runtime_handoff")
        member_name = f"packages/{row['fileName']}"
        package_bytes = members.get(member_name)
        if package_bytes is None or len(package_bytes) != row["sizeBytes"] or sha256_bytes(package_bytes) != row["sha256"]:
            fail(f"Core runtime package bytes differ for {row['packageId']}")
        copy_new_bytes(stage / row["fileName"], package_bytes)
        selected.append(row)
    if {row["packageId"] for row in selected} != selected_ids:
        fail("Core runtime handoff does not contain the exact selected package set")
    return selected


def nuget_config(feed: Path) -> str:
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<configuration>\n"
        "  <packageSources>\n"
        "    <clear />\n"
        f'    <add key="same-run-local-feed" value="{feed.as_posix()}" />\n'
        "  </packageSources>\n"
        "  <packageSourceMapping>\n"
        '    <packageSource key="same-run-local-feed">\n'
        '      <package pattern="*" />\n'
        "    </packageSource>\n"
        "  </packageSourceMapping>\n"
        "</configuration>\n"
    )


def prepare_feed(args: argparse.Namespace) -> dict[str, Any]:
    lock = load_lock(args.lock, args.rid)
    ui = validate_ui_recipe(args.repo_root, lock)
    core = lock["coreAuthority"]
    core_identity = repository_identity(
        args.core_authority,
        repository=core["repository"],
        commit=core["commit"],
        tree=core["tree"],
        require_no_untracked=True,
    )
    if git_value(args.core_authority, "rev-parse", "HEAD^") != core["runtimeSourceCommit"]:
        fail("Core package authority is not directly grounded in the runtime source")
    validator = args.core_authority / "scripts/ai/runtime-package-plane.py"
    completed = subprocess.run(
        (sys.executable, str(validator), "--repo-root", str(args.core_authority)),
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )
    if completed.returncode != 0:
        fail(f"Core runtime package authority rejected its checkout: {completed.stderr.strip()}")
    require_new_directory(args.feed, "package feed")
    require_new_directory(args.download_root, "package download root")
    args.download_root.mkdir(mode=0o700)
    handoff, handoff_raw, bundle_raw = public_handoff(lock, args.download_root)
    members = bundle_members(handoff, bundle_raw)
    core_receipt = strict_json_bytes(
        members["no-siblings.v3.receipt.json"], "Core no-siblings receipt"
    )
    stage = Path(tempfile.mkdtemp(prefix="unsigned-macos-feed.", dir=args.feed.parent))
    os.chmod(stage, 0o700)
    try:
        rows = core_package_rows(lock, members, core_receipt, stage)
        rows.extend(
            verify_owner_feed(
                args.owner_feed,
                core_receipt,
                set(lock["ownerPackageIds"]),
                stage,
            )
        )
        if args.source_feed is not None:
            copy_source_feed(lock, args.source_feed, stage)
        for raw in selected_external_rows(args.repo_root, lock, args.rid):
            file_name = str(raw["fileName"])
            has_locked_size = "sizeBytes" in raw or "size_bytes" in raw
            inventory = download_exact(
                raw,
                stage / file_name,
                require_size=has_locked_size,
            )
            row = package_row(
                {**raw, "sizeBytes": inventory["sizeBytes"]},
                source_role="locked_external",
            )
            rows.append(row)
        os.rename(stage, args.feed)
    except BaseException:
        shutil.rmtree(stage, ignore_errors=True)
        raise
    atomic_text(args.pack_config, nuget_config(args.feed))
    receipt = {
        "contract": PREPARE_CONTRACT,
        "coreAuthority": {
            "commit": core["commit"],
            "publicHandoffReceiptSha256": sha256_bytes(handoff_raw),
            "runtimeSourceCommit": core["runtimeSourceCommit"],
            "tree": core_identity["tree"],
        },
        "localCompatibilityTree": False,
        "noSiblingFallback": True,
        "packages": sorted(rows, key=lambda row: (row["packageId"].casefold(), row["version"])),
        "pendingLocalPackages": lock["locallyPackedPackages"],
        "rid": args.rid,
        "status": "pass",
        "uiSource": ui,
    }
    atomic_json(args.output, receipt)
    return receipt


def load_core_canonicalizer(core_authority: Path) -> Any:
    path = core_authority / "scripts/ai/bootstrap-owner-contracts-feed.py"
    spec = importlib.util.spec_from_file_location("chummer_core_owner_canonicalizer", path)
    if spec is None or spec.loader is None:
        fail("Core package canonicalizer is unavailable")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def nuspec_identity(path: Path) -> tuple[str, str, str, str]:
    import xml.etree.ElementTree as ET

    try:
        with zipfile.ZipFile(path) as archive:
            names = [name for name in archive.namelist() if name.casefold().endswith(".nuspec")]
            if len(names) != 1:
                fail("locally packed package must contain exactly one nuspec")
            root = ET.fromstring(archive.read(names[0]))
    except (OSError, zipfile.BadZipFile, ET.ParseError) as error:
        fail(f"locally packed package is invalid: {error}")
    local = lambda tag: tag.rsplit("}", 1)[-1]
    values = {
        local(element.tag): (element.text or "").strip()
        for element in root.iter()
        if local(element.tag) in {"id", "version"}
    }
    repositories = [element for element in root.iter() if local(element.tag) == "repository"]
    if len(repositories) != 1:
        fail("locally packed package repository authority is missing")
    return (
        values.get("id", ""),
        values.get("version", ""),
        (repositories[0].get("url") or "").strip(),
        (repositories[0].get("commit") or "").strip(),
    )


def source_feed_rows(
    lock: dict[str, Any],
    source_feed: Path,
    *,
    canonicalizer: Any | None = None,
) -> list[dict[str, Any]]:
    if not source_feed.is_absolute() or source_feed.is_symlink() or not source_feed.is_dir():
        fail("Linux source-package feed must be one real absolute directory")
    expected_names = {str(raw["fileName"]) for raw in lock["locallyPackedPackages"]}
    observed_names: set[str] = set()
    for path in sorted(source_feed.iterdir(), key=lambda value: value.name):
        exact_file_bytes(path, f"Linux source-feed member {path.name}")
        observed_names.add(path.name)
    if observed_names != expected_names:
        fail("Linux source-package feed member set differs from the lock")

    rows: list[dict[str, Any]] = []
    for raw in lock["locallyPackedPackages"]:
        path = source_feed / raw["fileName"]
        if canonicalizer is not None:
            canonicalizer.canonicalize_nupkg(path)
        if nuspec_identity(path) != (
            raw["packageId"],
            raw["version"],
            raw["repository"],
            raw["commit"],
        ):
            fail(f"Linux source-package authority differs for {raw['packageId']}")
        row = package_row(raw, source_role="linux_authority_source_pack")
        package_bytes = exact_file_bytes(path, f"Linux source package {row['packageId']}")
        if len(package_bytes) != row["sizeBytes"] or sha256_bytes(package_bytes) != row["sha256"]:
            fail(f"Linux source-package bytes differ for {row['packageId']}")
        rows.append(row)
    return rows


def copy_source_feed(lock: dict[str, Any], source_feed: Path, stage: Path) -> None:
    for row in source_feed_rows(lock, source_feed):
        source = source_feed / row["fileName"]
        copy_new_bytes(stage / row["fileName"], exact_file_bytes(source, row["packageId"]))


def validate_source_feed_authority(args: argparse.Namespace) -> dict[str, Any]:
    lock = load_lock(args.lock)
    core = lock["coreAuthority"]
    core_identity = repository_identity(
        args.core_authority,
        repository=core["repository"],
        commit=core["commit"],
        tree=core["tree"],
        require_no_untracked=True,
    )
    owner_roots = {
        "chummer.run-services": args.hub_authority,
        "chummer-ui-kit": args.ui_kit_authority,
    }
    owners: dict[str, dict[str, str]] = {}
    for raw in lock["locallyPackedPackages"]:
        owner = str(raw["owner"])
        owners[owner] = repository_identity(
            owner_roots[owner],
            repository=raw["repository"],
            commit=raw["commit"],
            require_no_untracked=True,
        )
    rows = source_feed_rows(
        lock,
        args.source_feed,
        canonicalizer=load_core_canonicalizer(args.core_authority),
    )
    inventory = [
        {
            "fileName": row["fileName"],
            "sha256": row["sha256"],
            "sizeBytes": row["sizeBytes"],
        }
        for row in sorted(rows, key=lambda value: value["fileName"])
    ]
    receipt = {
        "contract": SOURCE_FEED_VALIDATION_CONTRACT,
        "coreAuthority": {
            "commit": core["commit"],
            "tree": core_identity["tree"],
        },
        "feedInventory": inventory,
        "feedInventorySha256": feed_inventory_sha256(inventory),
        "owners": owners,
        "packages": sorted(rows, key=lambda row: (row["packageId"].casefold(), row["version"])),
        "status": "pass",
    }
    atomic_json(args.output, receipt)
    return receipt


def feed_rows(feed: Path) -> list[dict[str, Any]]:
    if not feed.is_absolute() or feed.is_symlink() or not feed.is_dir():
        fail("package feed must be one real absolute directory")
    rows: list[dict[str, Any]] = []
    for path in sorted(feed.iterdir(), key=lambda value: value.name):
        metadata = path.lstat()
        if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or not path.name.casefold().endswith(".nupkg"):
            fail("package feed contains a link, special entry, or non-package file")
        rows.append({"fileName": path.name, "sha256": sha256_file(path), "sizeBytes": metadata.st_size})
    if not rows:
        fail("package feed is empty")
    return rows


def feed_inventory_sha256(rows: list[dict[str, Any]]) -> str:
    return sha256_bytes(json.dumps(rows, sort_keys=True, separators=(",", ":")).encode())


def seal_feed(args: argparse.Namespace) -> dict[str, Any]:
    lock = load_lock(args.lock, args.rid)
    prepare = strict_json_file(args.prepare_receipt, "package-plane prepare receipt")
    if (
        prepare.get("contract") != PREPARE_CONTRACT
        or prepare.get("status") != "pass"
        or prepare.get("rid") != args.rid
        or prepare.get("localCompatibilityTree") is not False
        or prepare.get("noSiblingFallback") is not True
    ):
        fail("package-plane prepare receipt is not passing for this RID")
    owner_roots = {
        "chummer.run-services": args.hub_authority,
        "chummer-ui-kit": args.ui_kit_authority,
    }
    for raw in lock["locallyPackedPackages"]:
        repository_identity(
            owner_roots[raw["owner"]],
            repository=raw["repository"],
            commit=raw["commit"],
        )
    local_rows: list[dict[str, Any]] = []
    for raw in lock["locallyPackedPackages"]:
        path = args.feed / raw["fileName"]
        if nuspec_identity(path) != (
            raw["packageId"],
            raw["version"],
            raw["repository"],
            raw["commit"],
        ):
            fail(f"locally packed package authority differs for {raw['packageId']}")
        row = package_row(raw, source_role="linux_authority_source_pack")
        if path.stat().st_size != row["sizeBytes"] or sha256_file(path) != row["sha256"]:
            fail(f"locally packed package bytes differ for {row['packageId']}")
        local_rows.append(row)
    rows = [*prepare["packages"], *local_rows]
    normalized = [package_row(row, source_role=str(row["sourceRole"])) for row in rows]
    names = [row["fileName"] for row in normalized]
    if len(names) != len(set(name.casefold() for name in names)):
        fail("sealed package manifest contains duplicate portable filenames")
    actual = feed_rows(args.feed)
    expected = sorted(
        ({"fileName": row["fileName"], "sha256": row["sha256"], "sizeBytes": row["sizeBytes"]} for row in normalized),
        key=lambda row: row["fileName"],
    )
    if actual != expected:
        fail("sealed package feed differs from the exact package manifest")
    config_raw = exact_file_bytes(args.pack_config, "package NuGet.Config", maximum=64 * 1024)
    if config_raw.decode("utf-8") != nuget_config(args.feed):
        fail("package NuGet.Config does not map only to the exact feed")
    manifest = {
        "contract": MANIFEST_CONTRACT,
        "coreAuthority": prepare["coreAuthority"],
        "feedInventory": actual,
        "feedInventorySha256": feed_inventory_sha256(actual),
        "localCompatibilityTree": False,
        "noSiblingFallback": True,
        "nugetSourcePolicy": "same-run-local-feed-only",
        "packages": sorted(normalized, key=lambda row: (row["packageId"].casefold(), row["version"])),
        "rid": args.rid,
        "status": "pass",
        "uiSource": prepare["uiSource"],
    }
    atomic_json(args.output, manifest)
    return manifest


def cache_package_rows(cache: Path) -> list[dict[str, Any]]:
    if not cache.is_absolute() or cache.is_symlink() or not cache.is_dir():
        fail("NuGet package cache must be one real absolute directory")
    rows: list[dict[str, Any]] = []
    for package_root in sorted(cache.iterdir(), key=lambda value: value.name):
        if package_root.is_symlink() or not package_root.is_dir():
            fail("NuGet package cache has an invalid package-id entry")
        for version_root in sorted(package_root.iterdir(), key=lambda value: value.name):
            if version_root.is_symlink() or not version_root.is_dir():
                fail("NuGet package cache has an invalid package-version entry")
            nupkg = version_root / f"{package_root.name}.{version_root.name}.nupkg"
            raw = exact_file_bytes(nupkg, f"cached package {package_root.name}")
            rows.append(
                {
                    "packageId": package_root.name,
                    "sha256": sha256_bytes(raw),
                    "sizeBytes": len(raw),
                    "version": version_root.name,
                }
            )
    return rows


def identity_list(values: set[tuple[str, str]]) -> str:
    return ",".join(f"{package_id}/{version}" for package_id, version in sorted(values)) or "<none>"


def canonical_package_identity_strings(
    expected: dict[tuple[str, str], dict[str, Any]],
    identities: set[tuple[str, str]],
) -> list[str]:
    return sorted(
        (f"{expected[identity]['packageId']}/{identity[1]}" for identity in identities),
        key=str.casefold,
    )


def command_value(*command: str) -> str:
    completed = subprocess.run(command, check=False, capture_output=True, text=True, timeout=30)
    if completed.returncode != 0 or not completed.stdout.strip():
        fail(f"runtime identity command failed: {command[0]}")
    return completed.stdout.strip()


def verify_resolution(args: argparse.Namespace, environment: Mapping[str, str]) -> dict[str, Any]:
    lock = load_lock(args.lock, args.rid)
    manifest_raw = exact_file_bytes(args.manifest, "sealed package manifest", maximum=MAX_JSON_BYTES)
    manifest = strict_json_bytes(manifest_raw, "sealed package manifest")
    if (
        manifest.get("contract") != MANIFEST_CONTRACT
        or manifest.get("status") != "pass"
        or manifest.get("rid") != args.rid
        or manifest.get("localCompatibilityTree") is not False
        or manifest.get("noSiblingFallback") is not True
        or manifest.get("nugetSourcePolicy") != "same-run-local-feed-only"
    ):
        fail("sealed package manifest is not passing for this RID")
    actual_feed = feed_rows(args.feed)
    if actual_feed != manifest.get("feedInventory") or feed_inventory_sha256(actual_feed) != manifest.get(
        "feedInventorySha256"
    ):
        fail("package feed changed after sealing")
    assets_raw = exact_file_bytes(args.assets, "publish project.assets.json", maximum=MAX_JSON_BYTES)
    assets = strict_json_bytes(assets_raw, "publish project.assets.json")
    package_folders = list((assets.get("packageFolders") or {}).keys())
    if len(package_folders) != 1 or Path(package_folders[0]).resolve() != args.package_cache:
        fail("publish restore used an ambient package cache")
    expected_rows = manifest.get("packages")
    if not isinstance(expected_rows, list):
        fail("sealed manifest package rows are missing")
    expected = {
        (str(row["packageId"]).casefold(), str(row["version"])): row
        for row in expected_rows
        if isinstance(row, dict)
    }
    cached_rows = cache_package_rows(args.package_cache)
    cached = {(row["packageId"].casefold(), row["version"]): row for row in cached_rows}
    libraries = assets.get("libraries")
    if not isinstance(libraries, dict):
        fail("publish assets library graph is missing")
    resolved: set[tuple[str, str]] = set()
    for identity, details in libraries.items():
        if not isinstance(details, dict) or details.get("type") != "package":
            continue
        package_id, separator, version = str(identity).rpartition("/")
        if not separator or not package_id or not version:
            fail("publish assets contains a malformed package identity")
        resolved.add((package_id.casefold(), version))
    rid_locked = {
        (str(row["packageId"]).casefold(), str(row["version"]))
        for row in lock["ridExternalPackages"][args.rid]
    }
    if resolved != set(expected) - rid_locked:
        fail(
            "publish assets graph differs from the sealed non-RID package set; "
            f"sealed-only={identity_list((set(expected) - rid_locked) - resolved)}; "
            f"assets-only={identity_list(resolved - (set(expected) - rid_locked))}"
        )
    if resolved - set(cached) or (set(cached) - resolved) - rid_locked:
        fail(
            "publish cache differs from the exact assets plus RID-pack policy; "
            f"assets-only={identity_list(resolved - set(cached))}; "
            f"unexpected-cache={identity_list((set(cached) - resolved) - rid_locked)}"
        )
    for identity, row in cached.items():
        authority = expected[identity]
        if row["sha256"] != authority["sha256"] or row["sizeBytes"] != authority["sizeBytes"]:
            fail(f"publish restore package bytes differ for {identity[0]}")
        metadata = args.package_cache / identity[0] / identity[1] / ".nupkg.metadata"
        metadata_payload = strict_json_file(metadata, f"NuGet source metadata for {identity[0]}")
        if Path(str(metadata_payload.get("source") or "")).resolve() != args.feed:
            fail(f"publish restore source differs for {identity[0]}")
    expected_chummer = {identity for identity in expected if identity[0].startswith("chummer.")}
    observed_chummer = {identity for identity in resolved if identity[0].startswith("chummer.")}
    if observed_chummer != expected_chummer:
        fail("publish graph is not backed by the exact Chummer package set")
    sdk = lock["sdk"]
    policy = sdk[args.rid]
    expected_environment = {
        "RUNNER_ARCH": policy["runnerArch"],
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
        "ImageOS": "macos15",
    }
    for name, expected_value in expected_environment.items():
        if environment.get(name) != expected_value:
            fail(f"native runtime environment differs: {name}")
    if command_value("uname", "-s") != "Darwin" or command_value("uname", "-m") != policy["machine"]:
        fail("native Darwin machine identity differs from the RID")
    if command_value("dotnet", "--version") != sdk["version"]:
        fail("native SDK version differs from the lock")
    executable_raw = exact_file_bytes(args.published_executable, "published native executable")
    architectures = command_value("lipo", "-archs", str(args.published_executable)).split()
    if architectures != [policy["machine"]]:
        fail("published executable architecture differs from the native RID")
    runtime = {
        "dotnetSdkVersion": sdk["version"],
        "executableArchitectures": architectures,
        "executableSha256": sha256_bytes(executable_raw),
        "framework": "net10.0",
        "imageOS": environment["ImageOS"],
        "imageVersion": str(environment.get("ImageVersion") or ""),
        "kernelRelease": command_value("uname", "-r"),
        "machine": policy["machine"],
        "macOSBuildVersion": command_value("sw_vers", "-buildVersion"),
        "macOSProductVersion": command_value("sw_vers", "-productVersion"),
        "rid": args.rid,
        "selfContained": True,
    }
    if not runtime["imageVersion"]:
        fail("GitHub-hosted image version is missing")
    receipt = {
        "assetsSha256": sha256_bytes(assets_raw),
        "contract": RESOLUTION_CONTRACT,
        "coreAuthority": manifest["coreAuthority"],
        "feedInventorySha256": manifest["feedInventorySha256"],
        "localCompatibilityTree": False,
        "manifestSha256": sha256_bytes(manifest_raw),
        "noSiblingFallback": True,
        "nugetSourcePolicy": manifest["nugetSourcePolicy"],
        "packageCacheWasFresh": True,
        "packages": manifest["packages"],
        "resolvedPackageIdentities": canonical_package_identity_strings(expected, resolved),
        "rid": args.rid,
        "runtime": runtime,
        "sdkProvidedRidPackageIdentities": canonical_package_identity_strings(
            expected, rid_locked - set(cached)
        ),
        "status": "pass",
        "uiSource": manifest["uiSource"],
    }
    atomic_json(args.output, receipt)
    return receipt


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    sdk = commands.add_parser("acquire-sdk")
    sdk.add_argument("--lock", type=Path, required=True)
    sdk.add_argument(
        "--rid", choices=("linux-x64", "osx-arm64", "osx-x64"), required=True
    )
    sdk.add_argument("--download-root", type=Path, required=True)
    sdk.add_argument("--destination", type=Path, required=True)
    sdk.add_argument("--output", type=Path, required=True)
    owner = commands.add_parser("validate-owner-feed")
    owner.add_argument("--lock", type=Path, required=True)
    owner.add_argument("--core-authority", type=Path, required=True)
    owner.add_argument("--owner-feed", type=Path, required=True)
    owner.add_argument("--download-root", type=Path, required=True)
    owner.add_argument("--output", type=Path, required=True)
    source = commands.add_parser("validate-source-feed")
    source.add_argument("--lock", type=Path, required=True)
    source.add_argument("--core-authority", type=Path, required=True)
    source.add_argument("--hub-authority", type=Path, required=True)
    source.add_argument("--ui-kit-authority", type=Path, required=True)
    source.add_argument("--source-feed", type=Path, required=True)
    source.add_argument("--output", type=Path, required=True)
    prepare = commands.add_parser("prepare-feed")
    prepare.add_argument("--repo-root", type=Path, required=True)
    prepare.add_argument("--lock", type=Path, required=True)
    prepare.add_argument("--core-authority", type=Path, required=True)
    prepare.add_argument("--owner-feed", type=Path, required=True)
    prepare.add_argument("--source-feed", type=Path)
    prepare.add_argument("--rid", choices=("osx-arm64", "osx-x64"), required=True)
    prepare.add_argument("--download-root", type=Path, required=True)
    prepare.add_argument("--feed", type=Path, required=True)
    prepare.add_argument("--pack-config", type=Path, required=True)
    prepare.add_argument("--output", type=Path, required=True)
    seal = commands.add_parser("seal-feed")
    seal.add_argument("--lock", type=Path, required=True)
    seal.add_argument("--core-authority", type=Path, required=True)
    seal.add_argument("--hub-authority", type=Path, required=True)
    seal.add_argument("--ui-kit-authority", type=Path, required=True)
    seal.add_argument("--rid", choices=("osx-arm64", "osx-x64"), required=True)
    seal.add_argument("--feed", type=Path, required=True)
    seal.add_argument("--pack-config", type=Path, required=True)
    seal.add_argument("--prepare-receipt", type=Path, required=True)
    seal.add_argument("--output", type=Path, required=True)
    resolution = commands.add_parser("verify-resolution")
    resolution.add_argument("--lock", type=Path, required=True)
    resolution.add_argument("--rid", choices=("osx-arm64", "osx-x64"), required=True)
    resolution.add_argument("--feed", type=Path, required=True)
    resolution.add_argument("--manifest", type=Path, required=True)
    resolution.add_argument("--assets", type=Path, required=True)
    resolution.add_argument("--package-cache", type=Path, required=True)
    resolution.add_argument("--published-executable", type=Path, required=True)
    resolution.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        for name, value in vars(args).items():
            if isinstance(value, Path):
                setattr(args, name, value.resolve(strict=False))
        if args.command == "acquire-sdk":
            acquire_sdk(args)
        elif args.command == "validate-owner-feed":
            validate_owner_feed_authority(args)
        elif args.command == "validate-source-feed":
            validate_source_feed_authority(args)
        elif args.command == "prepare-feed":
            prepare_feed(args)
        elif args.command == "seal-feed":
            seal_feed(args)
        else:
            verify_resolution(args, os.environ)
    except (OSError, PackagePlaneError, subprocess.SubprocessError, tarfile.TarError, urllib.error.URLError, zipfile.BadZipFile) as error:
        print(f"unsigned-macos-package-plane:error: {error}", file=sys.stderr)
        return 2
    print(f"unsigned-macos-package-plane:{args.command}:pass")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
