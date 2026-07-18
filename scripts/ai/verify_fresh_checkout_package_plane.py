#!/usr/bin/env python3
"""Build UI from a clean clone and a same-run cryptographically inventoried feed."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.request
from datetime import UTC, datetime
from pathlib import Path, PurePosixPath
from typing import Any
from zipfile import BadZipFile, ZipFile


CONTRACT = "chummer6-ui.fresh-package-plane-lock"
RECEIPT_CONTRACT = "chummer6-ui.fresh-package-plane-verification"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")


class VerificationError(ValueError):
    pass


def reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise VerificationError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(
            path.read_text(encoding="utf-8-sig"),
            object_pairs_hook=reject_duplicate_pairs,
            parse_constant=lambda value: (_ for _ in ()).throw(
                VerificationError(f"non-finite JSON number: {value}")
            ),
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise VerificationError(f"could not read exact JSON {path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise VerificationError(f"expected JSON object in {path}")
    return payload


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_digest(path: Path) -> str:
    """Hash tracked text with Git-portable LF normalization."""
    content = path.read_bytes()
    if b"\0" in content:
        raise VerificationError(f"locked consumer source is unexpectedly binary: {path}")
    return hashlib.sha256(content.replace(b"\r\n", b"\n")).hexdigest()


def require_relative(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise VerificationError(f"{label} is missing")
    pure = PurePosixPath(value)
    if pure.is_absolute() or ".." in pure.parts or "" in pure.parts:
        raise VerificationError(f"{label} must be a portable relative path")
    return pure.as_posix()


def run(
    command: list[str], *, cwd: Path, environment: dict[str, str], capture: bool = False
) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(
        command,
        cwd=cwd,
        env=environment,
        text=True,
        stdout=subprocess.PIPE if capture else None,
        stderr=subprocess.PIPE if capture else None,
        check=False,
    )
    if completed.returncode != 0:
        detail = (completed.stderr or "").strip().splitlines()
        suffix = f": {detail[-1]}" if detail else ""
        raise VerificationError(f"command failed ({Path(command[0]).name}){suffix}")
    return completed


def validate_lock(lock: dict[str, Any]) -> None:
    if lock.get("contractName") != CONTRACT or lock.get("contractVersion") != 2:
        raise VerificationError("package-plane lock contract is invalid")
    if lock.get("approvedPackageSources") != ["same-run-local-feed"]:
        raise VerificationError("package-plane lock permits an unapproved feed")
    sdk = lock.get("sdkVersion")
    if not isinstance(sdk, str) or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", sdk):
        raise VerificationError("package-plane SDK version is invalid")
    external_packages = lock.get("externalPackages")
    owners = lock.get("owners")
    packages = lock.get("packages")
    if (
        not isinstance(external_packages, list)
        or len(external_packages) != 2
        or not isinstance(owners, list)
        or len(owners) != 4
        or not isinstance(packages, list)
        or len(packages) != 12
    ):
        raise VerificationError("package-plane lock owner/package cardinality differs")
    external_names: set[str] = set()
    external_ids: set[str] = set()
    for package in external_packages:
        if not isinstance(package, dict) or set(package) != {
            "fileName",
            "packageId",
            "sha256",
            "source",
            "version",
        }:
            raise VerificationError("external package lock row is invalid")
        file_name = require_relative(package["fileName"], "external package file name")
        package_id = str(package["packageId"])
        version = str(package["version"])
        if "/" in file_name or not file_name.endswith(".nupkg") or file_name in external_names:
            raise VerificationError("external package file name is invalid or duplicated")
        if not PORTABLE_RE.fullmatch(package_id) or package_id in external_ids:
            raise VerificationError("external package ID is invalid or duplicated")
        if not PORTABLE_RE.fullmatch(version):
            raise VerificationError("external package version is invalid")
        expected_source = (
            "https://api.nuget.org/v3-flatcontainer/"
            f"{package_id.lower()}/{version.lower()}/{file_name.lower()}"
        )
        if package["source"] != expected_source:
            raise VerificationError("external package source is not its immutable NuGet path")
        if not isinstance(package["sha256"], str) or not SHA256_RE.fullmatch(package["sha256"]):
            raise VerificationError("external package SHA-256 is invalid")
        external_names.add(file_name)
        external_ids.add(package_id)
    owner_names: set[str] = set()
    for owner in owners:
        if not isinstance(owner, dict) or set(owner) != {"commit", "directory", "repository"}:
            raise VerificationError("package owner lock row is invalid")
        directory = require_relative(owner["directory"], "owner directory")
        if "/" in directory or directory in owner_names:
            raise VerificationError("owner directory must be unique and single-level")
        owner_names.add(directory)
        if not COMMIT_RE.fullmatch(str(owner["commit"])):
            raise VerificationError("owner commit is not exact")
        if not str(owner["repository"]).startswith("https://github.com/ArchonMegalon/") or not str(
            owner["repository"]
        ).endswith(".git"):
            raise VerificationError("owner repository is outside the fixed GitHub authority")
    package_names: set[str] = set()
    package_ids: set[str] = set()
    for package in packages:
        if not isinstance(package, dict) or set(package) != {
            "fileName",
            "ownerDirectory",
            "packageId",
            "project",
            "version",
        }:
            raise VerificationError("package lock row is invalid")
        if package["ownerDirectory"] not in owner_names:
            raise VerificationError("package owner directory is not locked")
        require_relative(package["project"], "package project")
        file_name = require_relative(package["fileName"], "package file name")
        if "/" in file_name or not file_name.endswith(".nupkg") or file_name in package_names:
            raise VerificationError("package file name is invalid or duplicated")
        if not PORTABLE_RE.fullmatch(str(package["packageId"])) or package["packageId"] in package_ids:
            raise VerificationError("package ID is invalid or duplicated")
        if not PORTABLE_RE.fullmatch(str(package["version"])):
            raise VerificationError("package version is invalid")
        package_names.add(file_name)
        package_ids.add(package["packageId"])
    required_core_runtime = {
        "Chummer.Application",
        "Chummer.Infrastructure",
        "Chummer.Rulesets.Hosting",
        "Chummer.Rulesets.Sr4",
        "Chummer.Rulesets.Sr5",
        "Chummer.Rulesets.Sr6",
    }
    if not required_core_runtime.issubset(package_ids):
        raise VerificationError("package-plane lock omits a required Core runtime package")
    if package_ids & external_ids or package_names & external_names:
        raise VerificationError("owner and external package authorities overlap")
    consumer = lock.get("consumer")
    if not isinstance(consumer, dict) or set(consumer) != {"buildProject", "sourceFiles"}:
        raise VerificationError("consumer lock is invalid")
    require_relative(consumer["buildProject"], "consumer build project")
    source_files = consumer["sourceFiles"]
    if not isinstance(source_files, dict) or not source_files:
        raise VerificationError("consumer source-file digest lock is empty")
    for name, digest in source_files.items():
        require_relative(name, "consumer source file")
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest):
            raise VerificationError("consumer source-file digest is invalid")


def verify_source_files(root: Path, locked: dict[str, str]) -> list[dict[str, Any]]:
    rows = []
    for relative, expected in sorted(locked.items()):
        path = root / relative
        try:
            metadata = path.lstat()
        except OSError as exc:
            raise VerificationError(f"locked consumer source is unavailable: {relative}") from exc
        if not stat.S_ISREG(metadata.st_mode) or path.is_symlink():
            raise VerificationError(f"locked consumer source is not a regular file: {relative}")
        actual = source_digest(path)
        if actual != expected:
            raise VerificationError(f"consumer source differs from package-plane lock: {relative}")
        rows.append({"path": relative, "sha256": actual, "sizeBytes": metadata.st_size})
    return rows


def package_inventory(feed: Path, expected_names: set[str]) -> list[dict[str, Any]]:
    actual_paths = sorted(path for path in feed.iterdir() if path.is_file())
    if {path.name for path in actual_paths} != expected_names:
        raise VerificationError("same-run feed contains missing or unexpected package bytes")
    rows = []
    for path in actual_paths:
        if path.is_symlink() or not stat.S_ISREG(path.lstat().st_mode):
            raise VerificationError("same-run feed contains a linked/special package")
        try:
            with ZipFile(path) as package:
                names = package.namelist()
                if not names or len(names) != len(set(names)) or not any(name.endswith(".nuspec") for name in names):
                    raise VerificationError(f"package ZIP inventory is invalid: {path.name}")
        except BadZipFile as exc:
            raise VerificationError(f"package is not a valid NuGet ZIP: {path.name}") from exc
        rows.append({"fileName": path.name, "sha256": sha256_file(path), "sizeBytes": path.stat().st_size})
    return rows


def acquire_external_package(package: dict[str, str], feed: Path) -> None:
    target = feed / package["fileName"]
    if target.exists() or target.is_symlink():
        raise VerificationError(f"external package target already exists: {target.name}")
    request = urllib.request.Request(
        package["source"],
        headers={"User-Agent": "chummer6-ui-fresh-package-plane/2"},
    )
    digest = hashlib.sha256()
    size = 0
    with urllib.request.urlopen(request, timeout=30) as source, target.open("xb") as output:
        while chunk := source.read(1024 * 1024):
            size += len(chunk)
            if size > 64 * 1024 * 1024:
                raise VerificationError("external package exceeds the fixed 64 MiB limit")
            digest.update(chunk)
            output.write(chunk)
        output.flush()
        os.fsync(output.fileno())
    if size == 0 or digest.hexdigest() != package["sha256"]:
        target.unlink(missing_ok=True)
        raise VerificationError(f"external package digest differs: {package['fileName']}")


def require_inventory_unchanged(
    before: list[dict[str, Any]], after: list[dict[str, Any]]
) -> None:
    if before != after:
        raise VerificationError("same-run package feed changed during restore/build")


def write_nuget_config(path: Path, feed: Path | None) -> None:
    source = (
        f'    <add key="same-run" value="{feed.as_posix()}" />\n'
        if feed is not None
        else ""
    )
    path.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration>\n  <packageSources>\n    <clear />\n"
        + source
        + "  </packageSources>\n</configuration>\n",
        encoding="utf-8",
    )


def acquire_owner(owner: dict[str, str], owners_root: Path, environment: dict[str, str]) -> Path:
    target = owners_root / owner["directory"]
    target.mkdir(mode=0o700)
    run(["git", "init", "--quiet"], cwd=target, environment=environment)
    run(["git", "remote", "add", "origin", owner["repository"]], cwd=target, environment=environment)
    run(
        ["git", "fetch", "--quiet", "--depth=1", "origin", owner["commit"]],
        cwd=target,
        environment=environment,
    )
    run(["git", "checkout", "--quiet", "--detach", "FETCH_HEAD"], cwd=target, environment=environment)
    actual = run(["git", "rev-parse", "HEAD"], cwd=target, environment=environment, capture=True).stdout.strip()
    if actual != owner["commit"]:
        raise VerificationError(f"owner checkout differs: {owner['directory']}")
    status = run(["git", "status", "--porcelain"], cwd=target, environment=environment, capture=True).stdout
    if status:
        raise VerificationError(f"owner checkout is dirty: {owner['directory']}")
    return target


def exact_write_receipt(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        raise VerificationError("receipt output must be a new absolute path")
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o600)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(encoded)
        stream.flush()
        os.fsync(stream.fileno())


def verify(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = args.repo_root.resolve()
    lock = load_json(args.lock)
    validate_lock(lock)
    status = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        check=True,
    ).stdout
    if status:
        raise VerificationError("consumer checkout must be clean")
    source_rows = verify_source_files(repo_root, lock["consumer"]["sourceFiles"])
    sdk = subprocess.run(
        ["dotnet", "--version"], text=True, stdout=subprocess.PIPE, check=True
    ).stdout.strip()
    if sdk != lock["sdkVersion"]:
        raise VerificationError(f"dotnet SDK differs from lock: {sdk}")

    with tempfile.TemporaryDirectory(prefix="chummer-ui-fresh-package-plane-") as temporary_name:
        temporary = Path(temporary_name)
        owners_root = temporary / "owners"
        feed = temporary / "feed"
        caches = temporary / "caches"
        consumer_parent = temporary / "consumer-only"
        for path in (owners_root, feed, caches, consumer_parent):
            path.mkdir(mode=0o700)
        environment = os.environ.copy()
        environment.update(
            {
                "DOTNET_CLI_HOME": str(caches / "dotnet-home"),
                "NUGET_PACKAGES": str(caches / "owner-nuget"),
                "XDG_CACHE_HOME": str(caches / "xdg-cache"),
                "XDG_DATA_HOME": str(caches / "xdg-data"),
            }
        )
        for directory in environment["DOTNET_CLI_HOME"], environment["NUGET_PACKAGES"], environment["XDG_CACHE_HOME"], environment["XDG_DATA_HOME"]:
            Path(directory).mkdir(mode=0o700)
        for external_package in lock["externalPackages"]:
            acquire_external_package(external_package, feed)
        pack_config = temporary / "pack.NuGet.config"
        write_nuget_config(pack_config, feed)
        owner_roots = {
            owner["directory"]: acquire_owner(owner, owners_root, environment)
            for owner in lock["owners"]
        }
        for package in lock["packages"]:
            owner_root = owner_roots[package["ownerDirectory"]]
            project = owner_root / package["project"]
            if not project.is_file() or project.is_symlink():
                raise VerificationError(f"locked package project is unavailable: {package['project']}")
            run(
                [
                    "dotnet",
                    "pack",
                    str(project),
                    "-c",
                    "Release",
                    "-o",
                    str(feed),
                    f"-p:PackageVersion={package['version']}",
                    f"-p:ChummerWorkspaceRoot={owners_root}",
                    "-p:ChummerDesktopRuntimeIdentifiers=",
                    "-p:RuntimeIdentifiers=",
                    "--configfile",
                    str(pack_config),
                    "--disable-build-servers",
                    "--nologo",
                    "-v",
                    "minimal",
                ],
                cwd=owner_root,
                environment=environment,
            )
            expected = feed / package["fileName"]
            if not expected.is_file() or expected.is_symlink():
                raise VerificationError(f"pack did not emit exact locked package: {package['fileName']}")

        expected_names = {
            row["fileName"]
            for row in [*lock["externalPackages"], *lock["packages"]]
        }
        before = package_inventory(feed, expected_names)
        consumer = consumer_parent / "ui"
        run(["git", "clone", "--quiet", "--no-local", str(repo_root), str(consumer)], cwd=consumer_parent, environment=environment)
        head = run(["git", "rev-parse", "HEAD"], cwd=repo_root, environment=environment, capture=True).stdout.strip()
        run(["git", "checkout", "--quiet", "--detach", head], cwd=consumer, environment=environment)
        if any((consumer_parent / name).exists() for name in owner_roots):
            raise VerificationError("consumer clone has an ambient sibling compatibility tree")
        verify_source_files(consumer, lock["consumer"]["sourceFiles"])
        consumer_config = temporary / "consumer.NuGet.config"
        write_nuget_config(consumer_config, feed)
        consumer_cache = caches / "consumer-nuget"
        if consumer_cache.exists():
            raise VerificationError("consumer package cache was not fresh")
        environment.update(
            {
                "CHUMMER_ALLOW_STUB_PACKAGES": "0",
                "CHUMMER_PUBLISHED_FEED_SOURCES": str(feed),
                "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "0",
                "CHUMMER_VERIFY_MODE": "integration",
                "NUGET_PACKAGES": str(consumer_cache),
            }
        )
        run(
            [
                "bash",
                "scripts/ai/with-package-plane.sh",
                "build",
                lock["consumer"]["buildProject"],
                "-c",
                "Release",
                "--configfile",
                str(consumer_config),
                "--disable-build-servers",
                "--nologo",
                "-v",
                "minimal",
            ],
            cwd=consumer,
            environment=environment,
        )
        after = package_inventory(feed, expected_names)
        require_inventory_unchanged(before, after)
        if run(["git", "status", "--porcelain"], cwd=consumer, environment=environment, capture=True).stdout:
            raise VerificationError("fresh consumer checkout became dirty")
        return {
            "buildProject": lock["consumer"]["buildProject"],
            "consumerCommit": head,
            "contractName": RECEIPT_CONTRACT,
            "contractVersion": 1,
            "generatedAt": datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "localCompatibilityTree": False,
            "mode": "integration",
            "ownerSources": [
                {
                    "commit": owner["commit"],
                    "directory": owner["directory"],
                    "repository": owner["repository"],
                }
                for owner in lock["owners"]
            ],
            "packageCacheWasFresh": True,
            "packageInventory": before,
            "packageSources": ["same-run-local-feed"],
            "sourceInventory": source_rows,
            "status": "passed",
            "stubPackagesAllowed": False,
        }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    repo_root = Path(__file__).resolve().parents[2]
    parser.add_argument("--repo-root", type=Path, default=repo_root)
    parser.add_argument("--lock", type=Path, default=repo_root / "config" / "package-plane.lock.json")
    parser.add_argument("--receipt-output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        receipt = verify(args)
        exact_write_receipt(args.receipt_output, receipt)
    except (VerificationError, OSError, subprocess.SubprocessError) as exc:
        print(f"fresh-package-plane:error: {exc}", file=sys.stderr)
        return 2
    print(f"fresh-package-plane:receipt={args.receipt_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
