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
import tarfile
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
from datetime import UTC, datetime
from pathlib import Path, PurePosixPath
from typing import Any
from zipfile import BadZipFile, ZipFile


CONTRACT = "chummer6-ui.fresh-package-plane-lock"
RECEIPT_CONTRACT = "chummer6-ui.fresh-package-plane-verification"
CURRENT_FEED_RECEIPT_CONTRACT = (
    "chummer6-ui.current-owner-contract-feed-verification"
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
EXPECTED_SDK_VERSION = "10.0.103"
EXPECTED_SDK_ARCHIVE = {
    "fileName": "dotnet-sdk-10.0.103-linux-x64.tar.gz",
    "rid": "linux-x64",
    "sha512": "bab94f13c57b2ac821d4924fe66084be9b44c41761ff7ff64522c8f7aba345659d31258401dcec31cc3cf6ccae1d012623075aca1c9b9165bcfe5ba9abda1c0c",
    "source": "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/dotnet-sdk-10.0.103-linux-x64.tar.gz",
    "version": "10.0.103",
}
MAX_SDK_ARCHIVE_BYTES = 512 * 1024 * 1024
EXPECTED_OWNERS = {
    "chummer-core-engine": (
        "https://github.com/ArchonMegalon/chummer6-core.git",
        "0b0d20959630f7d92b377a522caf9a11cf4bdb9f",
    ),
    "chummer.run-services": (
        "https://github.com/ArchonMegalon/chummer6-hub.git",
        "35aa5a828f076d7c7c4a57dbab17d8715f9c3b68",
    ),
    "chummer-hub-registry": (
        "https://github.com/ArchonMegalon/chummer6-hub-registry.git",
        "4a312798a10cb7ae97c77731450e24fe6a74d963",
    ),
    "chummer-ui-kit": (
        "https://github.com/ArchonMegalon/chummer6-ui-kit.git",
        "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
    ),
}
EXPECTED_PACKAGES = {
    "Chummer.Engine.Contracts": ("chummer-core-engine", "Chummer.Contracts/Chummer.Contracts.csproj", "Chummer.Engine.Contracts.5.225.0.nupkg", "5.225.0"),
    "Chummer.Hub.Registry.Contracts": ("chummer-hub-registry", "Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj", "Chummer.Hub.Registry.Contracts.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Play.Contracts": ("chummer.run-services", "Chummer.Play.Contracts/Chummer.Play.Contracts.csproj", "Chummer.Play.Contracts.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Campaign.Contracts": ("chummer.run-services", "Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj", "Chummer.Campaign.Contracts.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Run.Contracts": ("chummer.run-services", "Chummer.Run.Contracts/Chummer.Run.Contracts.csproj", "Chummer.Run.Contracts.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Run.Registry": ("chummer-hub-registry", "Chummer.Run.Registry/Chummer.Run.Registry.csproj", "Chummer.Run.Registry.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Ui.Kit": ("chummer-ui-kit", "src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj", "Chummer.Ui.Kit.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Application": ("chummer-core-engine", "Chummer.Application/Chummer.Application.csproj", "Chummer.Application.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Rulesets.Hosting": ("chummer-core-engine", "Chummer.Rulesets.Hosting/Chummer.Rulesets.Hosting.csproj", "Chummer.Rulesets.Hosting.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Rulesets.Sr5": ("chummer-core-engine", "Chummer.Rulesets.Sr5/Chummer.Rulesets.Sr5.csproj", "Chummer.Rulesets.Sr5.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Rulesets.Sr6": ("chummer-core-engine", "Chummer.Rulesets.Sr6/Chummer.Rulesets.Sr6.csproj", "Chummer.Rulesets.Sr6.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Infrastructure": ("chummer-core-engine", "Chummer.Infrastructure/Chummer.Infrastructure.csproj", "Chummer.Infrastructure.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Rulesets.Sr4": ("chummer-core-engine", "Chummer.Rulesets.Sr4/Chummer.Rulesets.Sr4.csproj", "Chummer.Rulesets.Sr4.0.1.0-preview.nupkg", "0.1.0-preview"),
}
EXPECTED_HUB_CANONICAL_FEED = {
    "inventoryContract": "chummer-hub.external-package-inventory/v2",
    "inventoryFileName": "chummer-hub-packages.inventory.json",
    "inventorySha256": "e7396b51eb1ddcf59399869c4712c5b3caaff2d41bf2b7b45615c9a61a4bb70d",
    "lockContract": "chummer-hub.package-plane-lock/v3",
    "lockPath": "eng/package-plane.lock.json",
    "lockSha256": "51619c5cfabac19297eca9fe035fc4558784c01c0f61831dc4fadac8de31a4d8",
    "ownerDirectory": "chummer.run-services",
    "packageVersion": "0.1.0-preview",
    "producerPath": "scripts/ai/bootstrap-hub-package-feed.py",
    "producerSha256": "07ecc37d471fec7694a8892f0e9344ec88e35499d59c5588d0aa7742585c8105",
    "packages": [
        {
            "fileName": "Chummer.Engine.Contracts.5.225.0.nupkg",
            "packageId": "Chummer.Engine.Contracts",
            "sha256": "ee7a0578d7eaa421e0bdc445394e8d4304d48accb561c48853559e429375ad2d",
            "sizeBytes": 1389176,
            "version": "5.225.0",
        },
        {
            "fileName": "Chummer.Hub.Registry.Contracts.0.1.0-preview.nupkg",
            "packageId": "Chummer.Hub.Registry.Contracts",
            "sha256": "ece4182749d1db0cc224f7be169e7ae493b813e18f7e112bcecae77c08f1b5bc",
            "sizeBytes": 523758,
            "version": "0.1.0-preview",
        },
        {
            "fileName": "Chummer.Run.Registry.0.1.0-preview.nupkg",
            "packageId": "Chummer.Run.Registry",
            "sha256": "bccbc49cd44a21cac8cc7ed001724b410225701ba085470503efef68fdfe0233",
            "sizeBytes": 331894,
            "version": "0.1.0-preview",
        },
    ],
}
EXPECTED_CURRENT_OWNER_CONTRACT_FEED_SHA256 = (
    "0f1bcd60dab524c20e6cbb99f6c53d2c6e60c70b4edd0068cc2ccd690b34a475"
)
EXPECTED_CURRENT_OWNER_CONTRACT_PACKAGE_IDS = frozenset(
    {
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
    }
)
HUB_CANONICAL_PACKAGE_IDS = frozenset(
    row["packageId"] for row in EXPECTED_HUB_CANONICAL_FEED["packages"]
)
EXPECTED_EXTERNAL_PACKAGE_COUNT = 83
EXPECTED_EXTERNAL_AUTHORITY_SHA256 = "8b010e3ae9fc2d76f690f6c538900e0a29b023d6a6b738aaa30ad3029a0a974e"
EXPECTED_BUILD_PROJECTS = (
    "Chummer.Presentation/Chummer.Presentation.csproj",
    "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
    "Chummer.Workspaces.Postgres/Chummer.Workspaces.Postgres.csproj",
    "Chummer.Avalonia/Chummer.Avalonia.csproj",
    "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj",
)
EXPECTED_TEST_PROJECTS = (
    "Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj",
)
EXPECTED_TEST_COMPILE_ITEMS = {
    "DesktopUpdateArtifactTests.cs": None,
    "../Chummer.Tests/DesktopCrashRuntimeTests.cs": "DesktopCrashRuntimeTests.cs",
    "../Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs": "DesktopInstallLinkingRuntimeTests.cs",
    "../Chummer.Tests/DesktopPreferenceRuntimeTests.cs": "DesktopPreferenceRuntimeTests.cs",
    "../Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs": "DesktopStartupSmokeRuntimeTests.cs",
    "../Chummer.Tests/DesktopUpdateRuntimeTests.cs": "DesktopUpdateRuntimeTests.cs",
}
EXPECTED_CONSUMER_SOURCE_FILES = frozenset(
    {
        "Chummer.Avalonia/Chummer.Avalonia.csproj",
        "Chummer.Blazor/Chummer.Blazor.csproj",
        "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj",
        "Chummer.Desktop.Runtime/DesktopUpdateManifest.cs",
        "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
        "Chummer.Presentation/Chummer.Presentation.csproj",
        "Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj",
        "Chummer.Product.UnitTests/DesktopUpdateArtifactTests.cs",
        "Chummer.Tests/DesktopCrashRuntimeTests.cs",
        "Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs",
        "Chummer.Tests/DesktopPreferenceRuntimeTests.cs",
        "Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs",
        "Chummer.Tests/DesktopUpdateRuntimeTests.cs",
        "Chummer.Workspaces.Postgres/Chummer.Workspaces.Postgres.csproj",
        "Directory.Build.props",
        "Directory.Build.targets",
        "global.json",
        "scripts/ai/with-package-plane.sh",
    }
)
CHILD_ENVIRONMENT_PASSTHROUGH = frozenset(
    {
        "ALL_PROXY",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "LANG",
        "LC_ALL",
        "NO_PROXY",
        "SSL_CERT_DIR",
        "SSL_CERT_FILE",
        "TZ",
        "all_proxy",
        "http_proxy",
        "https_proxy",
        "no_proxy",
    }
)


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
    if set(lock) != {
        "approvedPackageSources",
        "canonicalOwnerFeed",
        "consumer",
        "contractName",
        "contractVersion",
        "currentOwnerContractFeed",
        "externalPackages",
        "owners",
        "packages",
        "sdkArchive",
        "sdkVersion",
    }:
        raise VerificationError("package-plane lock has missing or extra top-level fields")
    if lock.get("contractName") != CONTRACT or lock.get("contractVersion") != 6:
        raise VerificationError("package-plane lock contract is invalid")
    if lock.get("approvedPackageSources") != ["same-run-local-feed"]:
        raise VerificationError("package-plane lock permits an unapproved feed")
    if lock.get("canonicalOwnerFeed") != EXPECTED_HUB_CANONICAL_FEED:
        raise VerificationError("Hub canonical package authority differs from the fixed feed")
    current_owner_contract_feed = lock.get("currentOwnerContractFeed")
    if not isinstance(current_owner_contract_feed, dict):
        raise VerificationError("current owner-contract feed authority is missing")
    current_authority_sha256 = hashlib.sha256(
        json.dumps(
            current_owner_contract_feed,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    if current_authority_sha256 != EXPECTED_CURRENT_OWNER_CONTRACT_FEED_SHA256:
        raise VerificationError(
            "current owner-contract package authority differs from the fixed feed"
        )
    current_packages = current_owner_contract_feed.get("packages")
    if (
        not isinstance(current_packages, list)
        or {row.get("packageId") for row in current_packages if isinstance(row, dict)}
        != EXPECTED_CURRENT_OWNER_CONTRACT_PACKAGE_IDS
    ):
        raise VerificationError("current owner-contract package set is not exact")
    current_feed_rows = sorted(
        (
            {
                "fileName": row["fileName"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
            }
            for row in current_packages
        ),
        key=lambda row: row["fileName"],
    )
    current_feed_sha256 = hashlib.sha256(
        json.dumps(current_feed_rows, sort_keys=True, separators=(",", ":")).encode(
            "utf-8"
        )
    ).hexdigest()
    if current_feed_sha256 != current_owner_contract_feed.get(
        "packageFeedInventorySha256"
    ):
        raise VerificationError(
            "current owner-contract package inventory digest is inconsistent"
        )
    sdk = lock.get("sdkVersion")
    if sdk != EXPECTED_SDK_VERSION or lock.get("sdkArchive") != EXPECTED_SDK_ARCHIVE:
        raise VerificationError("package-plane SDK version differs from the fixed authority")
    external_packages = lock.get("externalPackages")
    owners = lock.get("owners")
    packages = lock.get("packages")
    if (
        not isinstance(external_packages, list)
        or len(external_packages) != EXPECTED_EXTERNAL_PACKAGE_COUNT
        or not isinstance(owners, list)
        or len(owners) != len(EXPECTED_OWNERS)
        or not isinstance(packages, list)
        or len(packages) != len(EXPECTED_PACKAGES)
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
    external_authority_sha256 = hashlib.sha256(
        json.dumps(external_packages, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    if (
        external_authority_sha256 != EXPECTED_EXTERNAL_AUTHORITY_SHA256
    ):
        raise VerificationError("external package authority differs from the fixed package/source set")
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
        if EXPECTED_OWNERS.get(directory) != (owner["repository"], owner["commit"]):
            raise VerificationError("owner repository/commit differs from the fixed authority")
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
        if EXPECTED_PACKAGES.get(str(package["packageId"])) != (
            package["ownerDirectory"],
            package["project"],
            file_name,
            package["version"],
        ):
            raise VerificationError("owner package authority differs from the fixed package set")
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
    if not HUB_CANONICAL_PACKAGE_IDS.issubset(package_ids):
        raise VerificationError("package-plane lock omits a Hub canonical package")
    locked_packages = {row["packageId"]: row for row in packages}
    for canonical in lock["canonicalOwnerFeed"]["packages"]:
        package = locked_packages[canonical["packageId"]]
        if (
            package["fileName"] != canonical["fileName"]
            or package["version"] != canonical["version"]
        ):
            raise VerificationError("Hub canonical package identity differs from package lock")
    if package_ids & external_ids or package_names & external_names:
        raise VerificationError("owner and external package authorities overlap")
    consumer = lock.get("consumer")
    if not isinstance(consumer, dict) or set(consumer) != {
        "buildProjects",
        "sourceFiles",
        "testProjects",
    }:
        raise VerificationError("consumer lock is invalid")
    build_projects = consumer["buildProjects"]
    if not isinstance(build_projects, list) or tuple(build_projects) != EXPECTED_BUILD_PROJECTS:
        raise VerificationError("consumer build project set differs from the fixed authority")
    for project in build_projects:
        require_relative(project, "consumer build project")
    test_projects = consumer["testProjects"]
    if not isinstance(test_projects, list) or tuple(test_projects) != EXPECTED_TEST_PROJECTS:
        raise VerificationError("consumer test project set differs from the fixed authority")
    for project in test_projects:
        require_relative(project, "consumer test project")
    source_files = consumer["sourceFiles"]
    if not isinstance(source_files, dict) or frozenset(source_files) != EXPECTED_CONSUMER_SOURCE_FILES:
        raise VerificationError("consumer source-file set differs from the fixed authority")
    for name, digest in source_files.items():
        require_relative(name, "consumer source file")
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest):
            raise VerificationError("consumer source-file digest is invalid")


def validate_test_compile_items(root: Path) -> None:
    project = root / EXPECTED_TEST_PROJECTS[0]
    try:
        document = ET.parse(project).getroot()
    except (OSError, ET.ParseError) as exc:
        raise VerificationError("product unit-test project XML is unavailable or invalid") from exc
    default_compile = [
        (group, child)
        for group in document.findall("PropertyGroup")
        for child in group.findall("EnableDefaultCompileItems")
    ]
    if (
        len(default_compile) != 1
        or default_compile[0][0].attrib
        or default_compile[0][1].attrib
        or list(default_compile[0][1])
        or (default_compile[0][1].text or "").strip().casefold() != "false"
    ):
        raise VerificationError("product unit-test project must disable default compile globs")
    actual: dict[str, str | None] = {}
    for group in document.findall("ItemGroup"):
        compile_items = group.findall("Compile")
        if compile_items and group.attrib:
            raise VerificationError("product unit-test Compile ItemGroup must be unconditional")
        for item in compile_items:
            if set(item.attrib) - {"Include", "Link"} or list(item) or (item.text or "").strip():
                raise VerificationError("product unit-test Compile item is not exact")
            include = item.attrib.get("Include")
            if not include or include in actual:
                raise VerificationError("product unit-test Compile item is missing or duplicated")
            actual[include] = item.attrib.get("Link")
    if actual != EXPECTED_TEST_COMPILE_ITEMS:
        raise VerificationError("product unit-test compile source set differs from fixed authority")


def isolated_child_environment(
    caches: Path, parent: dict[str, str] | None = None
) -> dict[str, str]:
    if caches.is_symlink() or (caches.exists() and not caches.is_dir()):
        raise VerificationError("isolated child cache root is not an exact directory")
    caches.mkdir(mode=0o700, parents=True, exist_ok=True)
    incoming = os.environ if parent is None else parent
    path_value = incoming.get("PATH") or os.defpath
    for command in ("bash", "dotnet", "git", "python3"):
        if shutil.which(command, path=path_value) is None:
            raise VerificationError(f"required child command is unavailable: {command}")
    environment = {
        key: value
        for key, value in incoming.items()
        if key in CHILD_ENVIRONMENT_PASSTHROUGH and value
    }
    bounded_directories = {
        "DOTNET_CLI_HOME": caches / "dotnet-home",
        "HOME": caches / "home",
        "NUGET_HTTP_CACHE_PATH": caches / "nuget-http",
        "NUGET_PACKAGES": caches / "owner-nuget",
        "NUGET_PLUGINS_CACHE_PATH": caches / "nuget-plugins",
        "TMP": caches / "tmp",
        "TEMP": caches / "tmp",
        "TMPDIR": caches / "tmp",
        "XDG_CACHE_HOME": caches / "xdg-cache",
        "XDG_CONFIG_HOME": caches / "xdg-config",
        "XDG_DATA_HOME": caches / "xdg-data",
    }
    for directory in set(bounded_directories.values()):
        directory.mkdir(mode=0o700)
    git_config = caches / "empty.gitconfig"
    if git_config.exists() or git_config.is_symlink():
        raise VerificationError("isolated child Git configuration already exists")
    git_config.write_text("", encoding="utf-8")
    environment.update(
        {
            **{key: str(value) for key, value in bounded_directories.items()},
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "GIT_CONFIG_GLOBAL": str(git_config),
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "MSBUILDDISABLENODEREUSE": "1",
            "NUGET_XMLDOC_MODE": "skip",
            "PATH": path_value,
        }
    )
    return environment


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


def package_inventory(
    feed: Path,
    expected_names: set[str],
    locked_sha256: dict[str, str] | None = None,
) -> list[dict[str, Any]]:
    actual_paths = sorted(feed.iterdir())
    for path in actual_paths:
        metadata = path.lstat()
        if path.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError("same-run feed contains a directory, link, or special entry")
    if {path.name for path in actual_paths} != expected_names:
        raise VerificationError("same-run feed contains missing or unexpected package bytes")
    rows = []
    for path in actual_paths:
        try:
            with ZipFile(path) as package:
                names = package.namelist()
                if not names or len(names) != len(set(names)) or not any(name.endswith(".nuspec") for name in names):
                    raise VerificationError(f"package ZIP inventory is invalid: {path.name}")
        except BadZipFile as exc:
            raise VerificationError(f"package is not a valid NuGet ZIP: {path.name}") from exc
        digest = sha256_file(path)
        if locked_sha256 is not None and path.name in locked_sha256:
            if digest != locked_sha256[path.name]:
                raise VerificationError(f"locked package changed: {path.name}")
        rows.append({"fileName": path.name, "sha256": digest, "sizeBytes": path.stat().st_size})
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
            if size > 96 * 1024 * 1024:
                raise VerificationError("external package exceeds the fixed 96 MiB limit")
            digest.update(chunk)
            output.write(chunk)
        output.flush()
        os.fsync(output.fileno())
    if size == 0 or digest.hexdigest() != package["sha256"]:
        target.unlink(missing_ok=True)
        raise VerificationError(f"external package digest differs: {package['fileName']}")


def acquire_sdk(archive: dict[str, str], toolchain_root: Path) -> tuple[Path, str]:
    if toolchain_root.exists() or toolchain_root.is_symlink():
        raise VerificationError("private SDK root must be new")
    archive_path = toolchain_root.parent / archive["fileName"]
    if archive_path.exists() or archive_path.is_symlink():
        raise VerificationError("private SDK archive target already exists")
    request = urllib.request.Request(
        archive["source"],
        headers={"User-Agent": "chummer6-ui-fresh-package-plane/3"},
    )
    digest = hashlib.sha512()
    size = 0
    try:
        with urllib.request.urlopen(request, timeout=60) as source, archive_path.open("xb") as output:
            while chunk := source.read(1024 * 1024):
                size += len(chunk)
                if size > MAX_SDK_ARCHIVE_BYTES:
                    raise VerificationError("private SDK archive exceeds the fixed size bound")
                digest.update(chunk)
                output.write(chunk)
            output.flush()
            os.fsync(output.fileno())
    except BaseException:
        archive_path.unlink(missing_ok=True)
        raise
    archive_sha512 = digest.hexdigest()
    if size == 0 or archive_sha512 != archive["sha512"]:
        archive_path.unlink(missing_ok=True)
        raise VerificationError("private SDK archive digest differs")
    toolchain_root.mkdir(mode=0o700)
    try:
        with tarfile.open(archive_path, mode="r:gz") as bundle:
            bundle.extractall(toolchain_root, filter="data")
    except (OSError, tarfile.TarError) as exc:
        raise VerificationError("private SDK archive is not a safe tarball") from exc
    dotnet = toolchain_root / "dotnet"
    if dotnet.is_symlink() or not dotnet.is_file() or not os.access(dotnet, os.X_OK):
        raise VerificationError("private SDK archive has no exact dotnet host")
    return toolchain_root, archive_sha512


def require_exact_sdk(
    cwd: Path, environment: dict[str, str], expected: str, label: str
) -> str:
    actual = run(
        ["dotnet", "--version"],
        cwd=cwd,
        environment=environment,
        capture=True,
    ).stdout.strip()
    if actual != expected:
        raise VerificationError(f"{label} SDK differs from lock: {actual}")
    return actual


def require_inventory_unchanged(
    before: list[dict[str, Any]], after: list[dict[str, Any]]
) -> None:
    if before != after:
        raise VerificationError("same-run package feed changed during restore/build")


def inventory_sha256(rows: list[dict[str, Any]]) -> str:
    encoded = json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def write_nuget_config(path: Path, feed: Path | None) -> None:
    source = (
        f'    <add key="same-run-local-feed" value="{feed.as_posix()}" />\n'
        if feed is not None
        else ""
    )
    path.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration>\n  <packageSources>\n    <clear />\n"
        + source
        + "  </packageSources>\n"
        "  <packageSourceMapping>\n"
        "    <packageSource key=\"same-run-local-feed\">\n"
        "      <package pattern=\"*\" />\n"
        "    </packageSource>\n"
        "  </packageSourceMapping>\n"
        "</configuration>\n",
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


def import_hub_canonical_feed(
    lock: dict[str, Any],
    hub_root: Path,
    sdk_root: Path,
    canonical_feed: Path,
    destination_feed: Path,
    environment: dict[str, str],
) -> dict[str, Any]:
    authority = lock["canonicalOwnerFeed"]
    producer = hub_root / require_relative(authority["producerPath"], "Hub feed producer")
    producer_lock = hub_root / require_relative(authority["lockPath"], "Hub feed lock")
    for path, expected_digest, label in (
        (producer, authority["producerSha256"], "producer"),
        (producer_lock, authority["lockSha256"], "lock"),
    ):
        try:
            metadata = path.lstat()
        except OSError as exc:
            raise VerificationError(f"Hub canonical feed {label} is unavailable") from exc
        if path.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError(f"Hub canonical feed {label} is not a regular file")
        if sha256_file(path) != expected_digest:
            raise VerificationError(f"Hub canonical feed {label} differs from authority")
    if canonical_feed.exists() or canonical_feed.is_symlink():
        raise VerificationError("Hub canonical feed destination must start absent")

    command = [
        sys.executable,
        str(producer),
        "--repo-root",
        str(hub_root),
        "--lock",
        str(producer_lock),
        "--feed",
        str(canonical_feed),
        "--dotnet",
        str(sdk_root / "dotnet"),
    ]
    run(command, cwd=hub_root, environment=environment)
    run([*command, "--validate-only"], cwd=hub_root, environment=environment)

    inventory_path = canonical_feed / authority["inventoryFileName"]
    try:
        inventory_metadata = inventory_path.lstat()
    except OSError as exc:
        raise VerificationError("Hub canonical feed inventory is unavailable") from exc
    if inventory_path.is_symlink() or not stat.S_ISREG(inventory_metadata.st_mode):
        raise VerificationError("Hub canonical feed inventory is not a regular file")
    if sha256_file(inventory_path) != authority["inventorySha256"]:
        raise VerificationError("Hub canonical feed inventory differs from authority")
    inventory = load_json(inventory_path)

    owners = {row["directory"]: row for row in lock["owners"]}
    packages = {row["packageId"]: row for row in lock["packages"]}
    expected_rows: list[dict[str, Any]] = []
    for canonical in authority["packages"]:
        package = packages[canonical["packageId"]]
        owner = owners[package["ownerDirectory"]]
        expected_rows.append(
            {
                "id": canonical["packageId"],
                "version": canonical["version"],
                "repository": owner["repository"],
                "commit": owner["commit"],
                "project": package["project"],
                "file_name": canonical["fileName"],
                "sha256": canonical["sha256"],
                "size_bytes": canonical["sizeBytes"],
            }
        )
    expected_inventory = {
        "contract": authority["inventoryContract"],
        "package_plane_lock_sha256": authority["lockSha256"],
        "package_version": authority["packageVersion"],
        "packages": expected_rows,
    }
    if inventory != expected_inventory:
        raise VerificationError("Hub canonical feed inventory payload differs from authority")

    expected_names = {
        authority["inventoryFileName"],
        *(row["fileName"] for row in authority["packages"]),
    }
    entries = list(canonical_feed.iterdir())
    if {entry.name for entry in entries} != expected_names:
        raise VerificationError("Hub canonical feed contains missing or unexpected entries")
    for entry in entries:
        metadata = entry.lstat()
        if entry.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError("Hub canonical feed contains a link or special entry")

    for package in authority["packages"]:
        source = canonical_feed / package["fileName"]
        target = destination_feed / package["fileName"]
        if target.exists() or target.is_symlink():
            raise VerificationError(f"canonical package target already exists: {target.name}")
        source_descriptor = os.open(
            source,
            os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
        )
        try:
            target_descriptor = os.open(
                target,
                os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
                0o600,
            )
        except BaseException:
            os.close(source_descriptor)
            raise
        try:
            with os.fdopen(source_descriptor, "rb") as input_stream, os.fdopen(
                target_descriptor, "wb"
            ) as output_stream:
                shutil.copyfileobj(input_stream, output_stream, length=1024 * 1024)
                output_stream.flush()
                os.fsync(output_stream.fileno())
        except BaseException:
            target.unlink(missing_ok=True)
            raise
        if (
            target.stat().st_size != package["sizeBytes"]
            or sha256_file(target) != package["sha256"]
        ):
            target.unlink(missing_ok=True)
            raise VerificationError(f"canonical package bytes differ: {target.name}")

    hub_owner = owners[authority["ownerDirectory"]]
    return {
        "inventoryContract": authority["inventoryContract"],
        "inventorySha256": authority["inventorySha256"],
        "lockContract": authority["lockContract"],
        "lockSha256": authority["lockSha256"],
        "ownerCommit": hub_owner["commit"],
        "packageCount": len(authority["packages"]),
        "packages": [
            {
                "fileName": row["fileName"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
            }
            for row in authority["packages"]
        ],
        "producerPath": authority["producerPath"],
        "producerSha256": authority["producerSha256"],
        "projectLockFilesEnforced": True,
        "status": "passed",
    }


def current_owner_contract_feed_binding_receipt(
    lock: dict[str, Any],
) -> dict[str, Any]:
    authority = lock["currentOwnerContractFeed"]
    return {
        "inventoryContract": authority["inventoryContract"],
        "inventorySha256": authority["inventorySha256"],
        "lockContract": authority["lockContract"],
        "lockSha256": authority["lockSha256"],
        "materializedFeedValidated": False,
        "packageCount": len(authority["packages"]),
        "packageFeedInventorySha256": authority["packageFeedInventorySha256"],
        "packages": [
            {
                "fileName": package["fileName"],
                "sha256": package["sha256"],
                "sizeBytes": package["sizeBytes"],
            }
            for package in authority["packages"]
        ],
        "packageVersion": authority["packageVersion"],
        "producerCommit": authority["producerCommit"],
        "producerPath": authority["producerPath"],
        "producerRepository": authority["producerRepository"],
        "producerSha256": authority["producerSha256"],
        "selectedForCanonicalFullFeed": False,
        "status": "bound_not_selected",
    }


def validate_materialized_current_owner_contract_feed(
    lock: dict[str, Any], feed: Path
) -> dict[str, Any]:
    authority = lock["currentOwnerContractFeed"]
    if not feed.is_absolute() or feed.is_symlink() or not feed.is_dir():
        raise VerificationError(
            "current owner-contract feed must be an absolute non-symlink directory"
        )
    if feed.resolve(strict=True) != feed:
        raise VerificationError(
            "current owner-contract feed must already be a physical canonical path"
        )

    inventory_path = feed / authority["inventoryFileName"]
    try:
        inventory_metadata = inventory_path.lstat()
    except OSError as exc:
        raise VerificationError(
            "current owner-contract feed inventory is unavailable"
        ) from exc
    if inventory_path.is_symlink() or not stat.S_ISREG(inventory_metadata.st_mode):
        raise VerificationError(
            "current owner-contract feed inventory is not a regular file"
        )
    if sha256_file(inventory_path) != authority["inventorySha256"]:
        raise VerificationError(
            "current owner-contract feed inventory differs from authority"
        )
    inventory = load_json(inventory_path)
    expected_inventory = {
        "contract": authority["inventoryContract"],
        "package_plane_lock_sha256": authority["lockSha256"],
        "package_version": authority["packageVersion"],
        "packages": [
            {
                "id": package["packageId"],
                "version": package["version"],
                "repository": package["repository"],
                "commit": package["commit"],
                "project": package["project"],
                "file_name": package["fileName"],
                "sha256": package["sha256"],
                "size_bytes": package["sizeBytes"],
            }
            for package in authority["packages"]
        ],
    }
    if inventory != expected_inventory:
        raise VerificationError(
            "current owner-contract feed inventory payload differs from authority"
        )

    expected_names = {
        authority["inventoryFileName"],
        *(package["fileName"] for package in authority["packages"]),
    }
    entries = list(feed.iterdir())
    if {entry.name for entry in entries} != expected_names:
        raise VerificationError(
            "current owner-contract feed contains missing or unexpected entries"
        )
    package_rows: list[dict[str, Any]] = []
    packages_by_name = {
        package["fileName"]: package for package in authority["packages"]
    }
    for entry in entries:
        metadata = entry.lstat()
        if entry.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError(
                "current owner-contract feed contains a link or special entry"
            )
        package = packages_by_name.get(entry.name)
        if package is None:
            continue
        actual_digest = sha256_file(entry)
        if metadata.st_size != package["sizeBytes"] or actual_digest != package["sha256"]:
            raise VerificationError(
                f"current owner-contract package differs from authority: {entry.name}"
            )
        package_rows.append(
            {
                "fileName": entry.name,
                "sha256": actual_digest,
                "sizeBytes": metadata.st_size,
            }
        )
    package_feed_sha256 = hashlib.sha256(
        json.dumps(
            sorted(package_rows, key=lambda row: row["fileName"]),
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    if package_feed_sha256 != authority["packageFeedInventorySha256"]:
        raise VerificationError(
            "current owner-contract package feed inventory differs from authority"
        )

    receipt = current_owner_contract_feed_binding_receipt(lock)
    receipt.update(
        {
            "materializedFeedValidated": True,
            "packageFeedInventorySha256": package_feed_sha256,
            "packages": sorted(package_rows, key=lambda row: row["fileName"]),
            "status": "passed",
        }
    )
    return receipt


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
    validate_test_compile_items(repo_root)
    source_rows = verify_source_files(repo_root, lock["consumer"]["sourceFiles"])

    with tempfile.TemporaryDirectory(prefix="chummer-ui-fresh-package-plane-") as temporary_name:
        temporary = Path(temporary_name)
        owners_root = temporary / "owners"
        feed = temporary / "feed"
        hub_canonical_feed = temporary / "hub-canonical-feed"
        caches = temporary / "caches"
        consumer_parent = temporary / "consumer-only"
        for path in (owners_root, feed, caches, consumer_parent):
            path.mkdir(mode=0o700)
        sdk_root, sdk_archive_sha512 = acquire_sdk(
            lock["sdkArchive"], temporary / "private-dotnet-sdk"
        )
        sdk_parent = os.environ.copy()
        sdk_parent["PATH"] = f"{sdk_root}{os.pathsep}{sdk_parent.get('PATH') or os.defpath}"
        environment = isolated_child_environment(caches, sdk_parent)
        environment["DOTNET_ROOT"] = str(sdk_root)
        require_exact_sdk(temporary, environment, lock["sdkVersion"], "private composition")
        for external_package in lock["externalPackages"]:
            acquire_external_package(external_package, feed)
        pack_config = temporary / "pack.NuGet.config"
        write_nuget_config(pack_config, feed)
        owner_roots = {
            owner["directory"]: acquire_owner(owner, owners_root, environment)
            for owner in lock["owners"]
        }
        owner_sdk_versions: dict[str, str] = {}
        for owner in lock["owners"]:
            owner_sdk_versions[owner["directory"]] = require_exact_sdk(
                owner_roots[owner["directory"]],
                environment,
                lock["sdkVersion"],
                f"{owner['directory']} owner",
            )
        current_owner_contract_feed_receipt = (
            current_owner_contract_feed_binding_receipt(lock)
        )
        canonical_feed_receipt = import_hub_canonical_feed(
            lock,
            owner_roots[lock["canonicalOwnerFeed"]["ownerDirectory"]],
            sdk_root,
            hub_canonical_feed,
            feed,
            environment,
        )
        for package in lock["packages"]:
            if package["packageId"] in HUB_CANONICAL_PACKAGE_IDS:
                continue
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
                    "-p:ChummerUseLocalCompatibilityTree=false",
                    "-p:ChummerLocalContractsProject=",
                    "-p:ChummerContractsPackageVersion=5.225.0",
                    "-p:ChummerEngineContractsPackageVersion=5.225.0",
                    "-p:ChummerCampaignContractsPackageVersion=0.1.0-preview",
                    "-p:ChummerHubRegistryContractsPackageVersion=0.1.0-preview",
                    "-p:ChummerRunContractsPackageVersion=0.1.0-preview",
                    "-p:ChummerRunRegistryPackageVersion=0.1.0-preview",
                    "-p:ChummerDesktopRuntimeIdentifiers=",
                    "-p:RuntimeIdentifiers=",
                    f"-p:RestoreSources={feed}",
                    "-p:RestoreAdditionalProjectSources=",
                    f"-p:RestoreConfigFile={pack_config}",
                    "-p:RestoreFallbackFolders=",
                    "-p:RestoreIgnoreFailedSources=false",
                    *(
                        ["-p:RestoreLockedMode=true"]
                        if package["ownerDirectory"]
                        == lock["canonicalOwnerFeed"]["ownerDirectory"]
                        else []
                    ),
                    "-warnaserror:NU1603,NU1608",
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
        locked_package_sha256 = {
            row["fileName"]: row["sha256"] for row in lock["externalPackages"]
        }
        locked_package_sha256.update(
            {
                row["fileName"]: row["sha256"]
                for row in lock["canonicalOwnerFeed"]["packages"]
            }
        )
        before = package_inventory(feed, expected_names, locked_package_sha256)
        consumer = consumer_parent / "ui"
        run(["git", "clone", "--quiet", "--no-local", str(repo_root), str(consumer)], cwd=consumer_parent, environment=environment)
        head = run(["git", "rev-parse", "HEAD"], cwd=repo_root, environment=environment, capture=True).stdout.strip()
        run(["git", "checkout", "--quiet", "--detach", head], cwd=consumer, environment=environment)
        if any((consumer_parent / name).exists() for name in owner_roots):
            raise VerificationError("consumer clone has an ambient sibling compatibility tree")
        validate_test_compile_items(consumer)
        verify_source_files(consumer, lock["consumer"]["sourceFiles"])
        consumer_config = temporary / "consumer.NuGet.config"
        write_nuget_config(consumer_config, feed)
        consumer_config_sha256 = sha256_file(consumer_config)
        feed_sha256 = inventory_sha256(before)
        consumer_cache_parent = caches / "consumer-package-invocations"
        if consumer_cache_parent.exists() or consumer_cache_parent.is_symlink():
            raise VerificationError("consumer package-cache parent was not fresh")
        consumer_cache_parent.mkdir(mode=0o700)
        environment.pop("NUGET_PACKAGES", None)
        environment.update(
            {
                "CHUMMER_ALLOW_STUB_PACKAGES": "0",
                "CHUMMER_PUBLISHED_FEED_ROOT": str(feed),
                "CHUMMER_PUBLISHED_FEED_SOURCES": str(feed),
                "CHUMMER_PUBLISHED_FEED_SHA256": feed_sha256,
                "CHUMMER_PUBLISHED_NUGET_CONFIG": str(consumer_config),
                "CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256": consumer_config_sha256,
                "CHUMMER_STRICT_PACKAGE_CACHE_PARENT": str(consumer_cache_parent),
                "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "0",
                "CHUMMER_VERIFY_MODE": "integration",
            }
        )
        build_executions: list[dict[str, str]] = []
        for build_project in lock["consumer"]["buildProjects"]:
            build_executions.append(
                {
                    "project": build_project,
                    "sdkVersion": require_exact_sdk(
                        consumer,
                        environment,
                        lock["sdkVersion"],
                        f"consumer build {build_project}",
                    ),
                }
            )
            run(
                [
                    "bash",
                    "scripts/ai/with-package-plane.sh",
                    "build",
                    build_project,
                    "-c",
                    "Release",
                    "-warnaserror:NU1603,NU1608",
                    "--disable-build-servers",
                    "--nologo",
                    "-v",
                    "minimal",
                ],
                cwd=consumer,
                environment=environment,
            )
        test_executions: list[dict[str, str]] = []
        for test_project in lock["consumer"]["testProjects"]:
            test_executions.append(
                {
                    "project": test_project,
                    "sdkVersion": require_exact_sdk(
                        consumer,
                        environment,
                        lock["sdkVersion"],
                        f"consumer test {test_project}",
                    ),
                }
            )
            run(
                [
                    "bash",
                    "scripts/ai/with-package-plane.sh",
                    "test",
                    test_project,
                    "-c",
                    "Release",
                    "-p:WarningsAsErrors=NU1603%3BNU1608",
                    "--minimum-expected-tests",
                    "1",
                    "--no-progress",
                ],
                cwd=consumer,
                environment=environment,
            )
        after = package_inventory(feed, expected_names, locked_package_sha256)
        require_inventory_unchanged(before, after)
        if run(["git", "status", "--porcelain"], cwd=consumer, environment=environment, capture=True).stdout:
            raise VerificationError("fresh consumer checkout became dirty")
        return {
            "buildProjects": lock["consumer"]["buildProjects"],
            "buildExecutions": build_executions,
            "canonicalOwnerFeed": canonical_feed_receipt,
            "consumerCommit": head,
            "contractName": RECEIPT_CONTRACT,
            "contractVersion": 6,
            "generatedAt": datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "localCompatibilityTree": False,
            "mode": "integration",
            "currentOwnerContractFeed": current_owner_contract_feed_receipt,
            "ownerSources": [
                {
                    "commit": owner["commit"],
                    "directory": owner["directory"],
                    "repository": owner["repository"],
                    "sdkVersion": owner_sdk_versions[owner["directory"]],
                }
                for owner in lock["owners"]
            ],
            "packageCacheWasFresh": True,
            "packageInventory": before,
            "packageFeedInventorySha256": feed_sha256,
            "nugetConfigSha256": consumer_config_sha256,
            "packageSources": ["same-run-local-feed"],
            "sdkArchiveSha512": sdk_archive_sha512,
            "sdkVersion": lock["sdkVersion"],
            "sourceInventory": source_rows,
            "status": "passed",
            "stubPackagesAllowed": False,
            "testProjects": lock["consumer"]["testProjects"],
            "testExecutions": test_executions,
        }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    repo_root = Path(__file__).resolve().parents[2]
    parser.add_argument("--repo-root", type=Path, default=repo_root)
    parser.add_argument("--lock", type=Path, default=repo_root / "config" / "package-plane.lock.json")
    parser.add_argument("--current-owner-contract-feed", type=Path)
    parser.add_argument("--receipt-output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if args.current_owner_contract_feed is not None:
            lock = load_json(args.lock)
            validate_lock(lock)
            receipt = {
                "contractName": CURRENT_FEED_RECEIPT_CONTRACT,
                "contractVersion": 1,
                "currentOwnerContractFeed": (
                    validate_materialized_current_owner_contract_feed(
                        lock, args.current_owner_contract_feed
                    )
                ),
                "generatedAt": datetime.now(UTC)
                .replace(microsecond=0)
                .isoformat()
                .replace("+00:00", "Z"),
                "status": "passed",
            }
        else:
            receipt = verify(args)
        exact_write_receipt(args.receipt_output, receipt)
    except (VerificationError, OSError, subprocess.SubprocessError) as exc:
        print(f"fresh-package-plane:error: {exc}", file=sys.stderr)
        return 2
    print(f"fresh-package-plane:receipt={args.receipt_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
