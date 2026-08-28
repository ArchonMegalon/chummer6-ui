#!/usr/bin/env python3
"""Build UI from a clean clone and a same-run cryptographically inventoried feed."""

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
RETAINED_WINDOWS_BUNDLE_CONTRACT = (
    "chummer6-ui.retained-windows-publish-closure"
)
CURRENT_FEED_RECEIPT_CONTRACT = (
    "chummer6-ui.current-owner-contract-feed-verification"
)
OWNER_PACKAGE_CACHE_CONTRACT = "chummer6-ui.owner-package-artifact-cache/v1"
UI_OWNER_FEED_INVENTORY_CONTRACT = "chummer6-ui.owner-package-inventory/v1"
UI_OWNER_FEED_RECEIPT_CONTRACT = "chummer6-ui.owner-package-production/v1"
UI_OWNER_PRODUCER_LOCK_CONTRACT = "chummer6-ui.owner-package-plane-lock/v1"
UI_OWNER_PRODUCER_LOCK_PATH = "config/ui-owner-package-plane.lock.json"
HUB_NO_SIBLINGS_RECEIPT_SHA256 = (
    "79e4113b54f627f264aab1179622d51970000734d121bfc3e73674e19af8ae67"
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
WINDOWS_RELEASE_CHANNEL = "preview"
WINDOWS_RELEASE_PLACEHOLDERS = frozenset(
    {"local", "local-rebuild", "run-local", "run-local-rebuild", "unpublished"}
)
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
        "3260ac73714d8b001a3599d6776196e394dc6c35",
    ),
    "chummer-ui-kit": (
        "https://github.com/ArchonMegalon/chummer6-ui-kit.git",
        "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
    ),
}
EXPECTED_PACKAGES = {
    "Chummer.Campaign.Contracts": ("chummer.run-services", "Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj", "Chummer.Campaign.Contracts.0.1.0-preview.nupkg", "0.1.0-preview"),
    "Chummer.Ui.Kit": ("chummer-ui-kit", "src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj", "Chummer.Ui.Kit.0.1.0-preview.nupkg", "0.1.0-preview"),
}
EXPECTED_UI_OWNER_SOURCES = {
    "Chummer.Campaign.Contracts": {
        "commit": "8cc22cb6fdf9bdf2af3c390125f7a88de90700b3",
        "ownerDirectory": "chummer.run-services",
        "project": "Chummer.Campaign.Contracts/Chummer.Campaign.Contracts.csproj",
        "projectSha256": "94c8d6582bc4b902673d5a09e6218adee82fdf7d5478a8b1e3434697b83957e0",
        "repository": "https://github.com/ArchonMegalon/chummer6-hub.git",
        "sourceTree": "970d7153b9e9509698ec059d191518d409214bb2",
    },
    "Chummer.Ui.Kit": {
        "commit": "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
        "ownerDirectory": "chummer-ui-kit",
        "project": "src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj",
        "projectSha256": "9386c6908b5495533729109c022e1cc00906a1285cd730bc35de3c7d5953c560",
        "repository": "https://github.com/ArchonMegalon/chummer6-ui-kit.git",
        "sourceTree": "1c9837c579a52c40fe49c70db9e4f7aff2af0143",
    },
}
EXPECTED_HUB_CANONICAL_FEED = {
    "inventoryContract": "chummer-hub.external-package-inventory/v4",
    "inventoryFileName": "chummer-hub-packages.inventory.json",
    "inventorySha256": "e02638a450141baf2ea7ab291fa86da5ff8c0aa49256e7ed82ff83f937fc3148",
    "lockContract": "chummer-hub.package-plane-lock/v5",
    "lockPath": "eng/package-plane.lock.json",
    "lockSha256": "f5797ad3d9b76754d818e102c5ac65ca9b09e5b296357fc1badf4459e5b66f29",
    "packageVersion": "0.1.0-packageplane.candidate.sh66c418a5004f",
    "producerCommit": "8cc22cb6fdf9bdf2af3c390125f7a88de90700b3",
    "producerDirectory": "chummer.run-services",
    "producerPath": "scripts/ai/bootstrap-hub-package-feed.py",
    "producerRepository": "https://github.com/ArchonMegalon/chummer6-hub.git",
    "producerSha256": "38e2dd040c9006134dc87eff70857e733e2c01bdcfd70992db358b1f985ced67",
    "receiptContract": "chummer-hub.no-siblings-package-plane/v2",
    "receiptFileName": "HUB_NO_SIBLINGS_PACKAGE_PLANE.generated.json",
    "receiptSha256": HUB_NO_SIBLINGS_RECEIPT_SHA256,
    "packages": [
        {
            "commit": "af9a7e19c3bf331e96411dfb8f9e7820a98cab29",
            "fileName": "Chummer.Hub.Registry.Contracts.0.1.0-packageplane.candidate.sh66c418a5004f.nupkg",
            "packageId": "Chummer.Hub.Registry.Contracts",
            "project": "Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj",
            "repository": "https://github.com/ArchonMegalon/chummer6-hub-registry.git",
            "sha256": "2916c9cbfd8da0bc4a13d6a26746ff30ada5e88a593a3e5039d632d58593935d",
            "sizeBytes": 524842,
            "version": "0.1.0-packageplane.candidate.sh66c418a5004f",
        },
        {
            "commit": "af9a7e19c3bf331e96411dfb8f9e7820a98cab29",
            "fileName": "Chummer.Run.Registry.0.1.0-packageplane.candidate.sh66c418a5004f.nupkg",
            "packageId": "Chummer.Run.Registry",
            "project": "Chummer.Run.Registry/Chummer.Run.Registry.csproj",
            "repository": "https://github.com/ArchonMegalon/chummer6-hub-registry.git",
            "sha256": "d8ddbcf1699d568adfa7ba6108bd0560f5d8a4b9b53714f4654f6c7dbd5e3be5",
            "sizeBytes": 345296,
            "version": "0.1.0-packageplane.candidate.sh66c418a5004f",
        },
        {
            "commit": "66c418a5004fae0cbc58ad9f2cf64e9a40954c3a",
            "fileName": "Chummer.Play.Contracts.0.1.0-packageplane.candidate.sh66c418a5004f.nupkg",
            "packageId": "Chummer.Play.Contracts",
            "project": "Chummer.Play.Contracts/Chummer.Play.Contracts.csproj",
            "repository": "https://github.com/ArchonMegalon/chummer6-hub.git",
            "sha256": "74040252d7f728ffd5ca882058e1dac9ec9568376cd1af95b5b50f6c01a49f01",
            "sizeBytes": 322544,
            "version": "0.1.0-packageplane.candidate.sh66c418a5004f",
        },
        {
            "commit": "66c418a5004fae0cbc58ad9f2cf64e9a40954c3a",
            "fileName": "Chummer.Run.Contracts.0.1.0-packageplane.candidate.sh66c418a5004f.nupkg",
            "packageId": "Chummer.Run.Contracts",
            "project": "Chummer.Run.Contracts/Chummer.Run.Contracts.csproj",
            "repository": "https://github.com/ArchonMegalon/chummer6-hub.git",
            "sha256": "86eeaaa5c39c4dc5c60f547904b2583ebfbee869cc2c4718a2d1b31a8fca06a1",
            "sizeBytes": 1838984,
            "version": "0.1.0-packageplane.candidate.sh66c418a5004f",
        },
    ],
}
CORE_RUNTIME_SOURCE_COMMIT = "febd698752e195dceef79fbc3f83dc971564fe00"
CORE_RUNTIME_RECIPE_COMMIT = "3260ac73714d8b001a3599d6776196e394dc6c35"
CORE_RUNTIME_PACKAGE_VERSION = "0.0.0-packageplane.candidate.shfebd698752e19"
EXPECTED_CORE_RUNTIME_FEED_METADATA = {
    "inventoryContract": "chummer-core.runtime-package-inventory/v1",
    "inventoryFileName": "chummer-core-runtime-packages.inventory.json",
    "inventorySha256": "7727e2a6cda4fbd911609c23bb4af90514deb891935f0676c121e2164a03823a",
    "lockContract": "chummer-core.runtime-package-plane-lock/v1",
    "lockFileName": "runtime-package-plane.lock.json",
    "lockSha256": "7d726ddea508af408d1eb50d36424385265a01a2895aa6a5e99e33a42056ae03",
    "packageRecipeCommit": CORE_RUNTIME_RECIPE_COMMIT,
    "packageVersion": CORE_RUNTIME_PACKAGE_VERSION,
    "receiptContract": "chummer-core.no-siblings-package-plane/v3",
    "receiptFileName": "no-siblings.v3.receipt.json",
    "receiptSha256": "579e864b24963aa23ddad989a81cb099494ea452f9c619a58d94291ceebdf801",
    "repository": "https://github.com/ArchonMegalon/chummer6-core.git",
    "runtimeSourceCommit": CORE_RUNTIME_SOURCE_COMMIT,
}
EXPECTED_CORE_RUNTIME_PACKAGES = {
    "Chummer.Engine.Contracts": (
        "Chummer.Contracts/Chummer.Contracts.csproj",
        "Chummer.Engine.Contracts.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "902bd9a36467ada5157eecb4f88828a052f8d232714c67903dacd6a60be667f1",
        1195080,
    ),
    "Chummer.Application": (
        "Chummer.Application/Chummer.Application.csproj",
        "Chummer.Application.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "fa259b8e7277569b625f7a1fdd3c2c222c2d5c289e669c4bffe2b313f8b4ec36",
        543120,
    ),
    "Chummer.Rulesets.Hosting": (
        "Chummer.Rulesets.Hosting/Chummer.Rulesets.Hosting.csproj",
        "Chummer.Rulesets.Hosting.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "55739e5666612b63f92b2bfe85ca5994cc5c1d57b842c501c6969960f7e99e07",
        14371,
    ),
    "Chummer.Rulesets.Sr5": (
        "Chummer.Rulesets.Sr5/Chummer.Rulesets.Sr5.csproj",
        "Chummer.Rulesets.Sr5.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "cc223bd95e53439981b6201f47fc3059c90dae9399c0703a5b4246a72479f240",
        31629,
    ),
    "Chummer.Rulesets.Sr6": (
        "Chummer.Rulesets.Sr6/Chummer.Rulesets.Sr6.csproj",
        "Chummer.Rulesets.Sr6.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "003a1fd6ba341e9062a17f895840533207b107e55ea0c71c2fae40bb43b0dcb4",
        41108,
    ),
    "Chummer.Infrastructure": (
        "Chummer.Infrastructure/Chummer.Infrastructure.csproj",
        "Chummer.Infrastructure.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "5fc002a6c0bc0336668a422b3c050d83ed96c196b856eec549fb2d01a9ee2349",
        274416,
    ),
    "Chummer.Rulesets.Sr4": (
        "Chummer.Rulesets.Sr4/Chummer.Rulesets.Sr4.csproj",
        "Chummer.Rulesets.Sr4.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "ce06baded2186c20c3005ff9e114a13b8196f8562d237576bdeb08c4edbcb490",
        34038,
    ),
    "Chummer.Engine.GmCharacterEdits": (
        "Chummer.GmCharacterEdits/Chummer.GmCharacterEdits.csproj",
        "Chummer.Engine.GmCharacterEdits.0.0.0-packageplane.candidate.shfebd698752e19.nupkg",
        "d4f62320708330d82026eba07954dd547757f8bde9644430c2b90f8cc08ee9b9",
        900712,
    ),
}
EXPECTED_CURRENT_OWNER_CONTRACT_FEED_SHA256 = (
    "4c8e2fef141cbd1faf696a1d304bd4216bdd83f9273a82153858fe82518a7d2e"
)
EXPECTED_CURRENT_OWNER_CONTRACT_PACKAGE_IDS = frozenset(
    {
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
    }
)
EXPECTED_WINDOWS_RUNTIME_PACKAGES = (
    {
        "fileName": "microsoft.netcore.app.runtime.win-x64.10.0.3.nupkg",
        "packageId": "Microsoft.NETCore.App.Runtime.win-x64",
        "sha256": "ab861ec8530982a04d4ed6e1675c1fcf1ca5603d0435159b71fbeaacb9c455ef",
        "sizeBytes": 40074136,
        "source": "https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.win-x64/10.0.3/microsoft.netcore.app.runtime.win-x64.10.0.3.nupkg",
        "version": "10.0.3",
    },
    {
        "fileName": "microsoft.aspnetcore.app.runtime.win-x64.10.0.3.nupkg",
        "packageId": "Microsoft.AspNetCore.App.Runtime.win-x64",
        "sha256": "4dd1ba27142e6cfdebfff4d2bfda9da2f9fd0198d26a25a0c31b3fcb6a57e840",
        "sizeBytes": 12795776,
        "source": "https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.app.runtime.win-x64/10.0.3/microsoft.aspnetcore.app.runtime.win-x64.10.0.3.nupkg",
        "version": "10.0.3",
    },
    {
        "fileName": "microsoft.netcore.app.host.win-x64.10.0.3.nupkg",
        "packageId": "Microsoft.NETCore.App.Host.win-x64",
        "sha256": "191a97bcf1dc318cc3027f3c0a96d5424e1052b0eb752bb2fff02996a33e5f90",
        "sizeBytes": 5781842,
        "source": "https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.host.win-x64/10.0.3/microsoft.netcore.app.host.win-x64.10.0.3.nupkg",
        "version": "10.0.3",
    },
)
EXPECTED_WINDOWS_RUNTIME_PACKAGE_SIZES = {
    row["fileName"]: row["sizeBytes"] for row in EXPECTED_WINDOWS_RUNTIME_PACKAGES
}
HUB_CANONICAL_PACKAGE_IDS = frozenset(
    row["packageId"] for row in EXPECTED_HUB_CANONICAL_FEED["packages"]
)
CANONICAL_ENGINE_CONTRACTS_VERSION = (
    CORE_RUNTIME_PACKAGE_VERSION
)
CANONICAL_HUB_CONTRACTS_VERSION = "0.1.0-packageplane.candidate.sh66c418a5004f"
FOCUSED_CAREER_ADVANCE_TEST_PROJECT = (
    "Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj"
)
FOCUSED_CAREER_ADVANCE_TEST_FILES = (
    "Chummer.Tests/Presentation/CareerActiveSkillAdvanceParityTests.cs|"
    "Chummer.Tests/Presentation/CareerSkillGroupAdvanceParityTests.cs|"
    "Chummer.Tests/Presentation/CareerSkillSpecializationParityTests.cs|"
    "Chummer.Tests/Presentation/CareerWeaponFireParityTests.cs"
)
FOCUSED_CAREER_ADVANCE_TEST_FILTER = (
    "FullyQualifiedName~CareerActiveSkillAdvanceParityTests|"
    "FullyQualifiedName~CareerSkillGroupAdvanceParityTests|"
    "FullyQualifiedName~CareerSkillSpecializationParityTests|"
    "FullyQualifiedName~CareerWeaponFireParityTests"
)
FOCUSED_CAREER_ADVANCE_MINIMUM_TESTS = 19
FOCUSED_OVERVIEW_TEST_PROJECT = "Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj"
FOCUSED_OVERVIEW_TEST_FILE = "Chummer.Tests/Presentation/WorkspaceOverviewLoaderTests.cs"
FOCUSED_OVERVIEW_TEST_FILTER = "FullyQualifiedName~WorkspaceOverviewLoaderTests"
FOCUSED_OVERVIEW_MINIMUM_TESTS = 19
PRODUCT_TEST_ASSEMBLY = (
    "Chummer.Product.UnitTests/bin/Release/net10.0/Chummer.Product.UnitTests.dll"
)
CREATION_INITIAL_AUTHORITY_BUDGET_SECONDS = 90
EXPECTED_EXTERNAL_PACKAGE_COUNT = 87
EXPECTED_EXTERNAL_AUTHORITY_SHA256 = "cd1054a9eeb9e36cbb5223c91d1e259746c848a41bc55c98fab1da5d355422a7"
WINDOWS_PUBLISH_PROJECT = "Chummer.Avalonia/Chummer.Avalonia.csproj"
WINDOWS_PUBLISH_FRAMEWORK = "net10.0"
WINDOWS_PUBLISH_RID = "win-x64"
REQUIRED_WINDOWS_PUBLISH_ASSETS = frozenset(
    {
        "Chummer.Avalonia.deps.json",
        "Chummer.Avalonia.dll",
        "Chummer.Avalonia.exe",
        "Chummer.Avalonia.runtimeconfig.json",
    }
)
CANONICAL_PACKAGE_PLANE_LOCK = Path("config/package-plane.lock.json")
TRUSTED_BASH = Path("/usr/bin/bash").resolve(strict=True)
TRUSTED_GIT = Path("/usr/bin/git").resolve(strict=True)
TRUSTED_PYTHON3 = Path("/usr/bin/python3").resolve(strict=True)
TRUSTED_SYSTEM_PATH = "/usr/bin:/bin"
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
    "../Chummer.Tests/Presentation/CareerActiveSkillAdvanceParityTests.cs": "Presentation/CareerActiveSkillAdvanceParityTests.cs",
    "../Chummer.Tests/Presentation/CareerSkillGroupAdvanceParityTests.cs": "Presentation/CareerSkillGroupAdvanceParityTests.cs",
    "../Chummer.Tests/Presentation/CareerSkillSpecializationParityTests.cs": "Presentation/CareerSkillSpecializationParityTests.cs",
    "../Chummer.Tests/Presentation/CareerWeaponFireParityTests.cs": "Presentation/CareerWeaponFireParityTests.cs",
    "../Chummer.Tests/Presentation/WorkspaceOverviewLoaderTests.cs": "Presentation/WorkspaceOverviewLoaderTests.cs",
}
EXPECTED_CONSUMER_SOURCE_FILES = frozenset(
    {
        "Chummer.Avalonia/Chummer.Avalonia.csproj",
        "Chummer.Blazor/Chummer.Blazor.csproj",
        "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj",
        "Chummer.Desktop.Runtime/DesktopUpdateManifest.cs",
        "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
        "Chummer.Presentation/AssemblyInfo.cs",
        "Chummer.Presentation/Chummer.Presentation.csproj",
        "Chummer.Presentation/Overview/CharacterOverviewPresenter.CreationBootstrap.cs",
        "Chummer.Presentation/Overview/CharacterOverviewPresenter.Dialogs.cs",
        "Chummer.Presentation/Overview/CharacterOverviewPresenter.cs",
        "Chummer.Presentation/Overview/DialogCoordinator.cs",
        "Chummer.Presentation/Overview/IDialogCoordinator.cs",
        "Chummer.Presentation/Overview/IWorkspaceOverviewLifecycleCoordinator.cs",
        "Chummer.Presentation/Overview/IWorkspaceOverviewStateFactory.cs",
        "Chummer.Presentation/Overview/WorkspaceOverviewLifecycleCoordinator.cs",
        "Chummer.Presentation/Overview/WorkspaceOverviewStateFactory.cs",
        "Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj",
        "Chummer.Product.UnitTests/DesktopUpdateArtifactTests.cs",
        "Chummer.Tests/DesktopCrashRuntimeTests.cs",
        "Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs",
        "Chummer.Tests/DesktopPreferenceRuntimeTests.cs",
        "Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs",
        "Chummer.Tests/DesktopUpdateRuntimeTests.cs",
        "Chummer.Tests/Presentation/CareerActiveSkillAdvanceParityTests.cs",
        "Chummer.Tests/Presentation/CareerSkillGroupAdvanceParityTests.cs",
        "Chummer.Tests/Presentation/CareerSkillSpecializationParityTests.cs",
        "Chummer.Tests/Presentation/CareerWeaponFireParityTests.cs",
        "Chummer.Tests/Presentation/WorkspaceOverviewLoaderTests.cs",
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


def require_windows_release_authority(
    release_version: str | None,
    release_channel: str | None,
) -> tuple[str, str]:
    if not isinstance(release_version, str) or not PORTABLE_RE.fullmatch(release_version):
        raise VerificationError("Windows release version must be one exact portable value")
    if release_version.lower() in WINDOWS_RELEASE_PLACEHOLDERS:
        raise VerificationError("Windows release version must not be a local placeholder")
    if release_channel != WINDOWS_RELEASE_CHANNEL:
        raise VerificationError(
            f"Windows release channel must be exactly {WINDOWS_RELEASE_CHANNEL}"
        )
    return release_version, release_channel


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


def validate_lock(
    lock: dict[str, Any], *, allow_unsealed_ui_owner: bool = False
) -> None:
    if set(lock) != {
        "approvedPackageSources",
        "canonicalOwnerFeed",
        "consumer",
        "contractName",
        "contractVersion",
        "coreRuntimeFeed",
        "currentOwnerContractFeed",
        "externalPackages",
        "owners",
        "packages",
        "sdkArchive",
        "sdkVersion",
    }:
        raise VerificationError("package-plane lock has missing or extra top-level fields")
    if lock.get("contractName") != CONTRACT or lock.get("contractVersion") != 10:
        raise VerificationError("package-plane lock contract is invalid")
    if lock.get("approvedPackageSources") != ["same-run-local-feed"]:
        raise VerificationError("package-plane lock permits an unapproved feed")
    canonical_owner_feed = lock.get("canonicalOwnerFeed")
    if not isinstance(canonical_owner_feed, dict):
        raise VerificationError("Hub canonical package authority is missing")
    canonical_producer_commit = canonical_owner_feed.get("producerCommit")
    if not isinstance(canonical_producer_commit, str) or not COMMIT_RE.fullmatch(
        canonical_producer_commit
    ):
        raise VerificationError("Hub canonical producer commit is not exact")
    if canonical_owner_feed != EXPECTED_HUB_CANONICAL_FEED:
        raise VerificationError("Hub canonical package authority differs from the fixed feed")
    core_runtime_feed = lock.get("coreRuntimeFeed")
    if not isinstance(core_runtime_feed, dict) or set(core_runtime_feed) != {
        *EXPECTED_CORE_RUNTIME_FEED_METADATA,
        "packages",
    }:
        raise VerificationError("Core runtime package authority fields are not exact")
    core_runtime_metadata = {
        key: value for key, value in core_runtime_feed.items() if key != "packages"
    }
    if core_runtime_metadata != EXPECTED_CORE_RUNTIME_FEED_METADATA:
        raise VerificationError("Core runtime package authority differs from the fixed feed")
    core_runtime_packages = core_runtime_feed.get("packages")
    if (
        not isinstance(core_runtime_packages, list)
        or tuple(
            row.get("packageId") for row in core_runtime_packages if isinstance(row, dict)
        )
        != tuple(EXPECTED_CORE_RUNTIME_PACKAGES)
    ):
        raise VerificationError("Core runtime package set or order is not exact")
    core_runtime_names: set[str] = set()
    for package in core_runtime_packages:
        if not isinstance(package, dict) or set(package) != {
            "commit",
            "fileName",
            "packageId",
            "project",
            "repository",
            "sha256",
            "sizeBytes",
            "version",
        }:
            raise VerificationError("Core runtime package row is invalid")
        package_id = str(package["packageId"])
        expected = EXPECTED_CORE_RUNTIME_PACKAGES.get(package_id)
        file_name = require_relative(package["fileName"], "Core runtime package file name")
        project = require_relative(package["project"], "Core runtime package project")
        if "/" in file_name or file_name in core_runtime_names:
            raise VerificationError("Core runtime package file name is invalid or duplicated")
        core_runtime_names.add(file_name)
        if expected != (
            project,
            file_name,
            package["sha256"],
            package["sizeBytes"],
        ):
            raise VerificationError("Core runtime package bytes differ from the fixed feed")
        if (
            package["commit"] != CORE_RUNTIME_SOURCE_COMMIT
            or package["repository"] != EXPECTED_CORE_RUNTIME_FEED_METADATA["repository"]
            or package["version"] != CORE_RUNTIME_PACKAGE_VERSION
        ):
            raise VerificationError("Core runtime package source authority is not exact")
    current_owner_contract_feed = lock.get("currentOwnerContractFeed")
    if not isinstance(current_owner_contract_feed, dict):
        raise VerificationError("current owner-contract feed authority is missing")
    if current_owner_contract_feed.get("selectedForCoreRuntimeCompatibility") is not True:
        raise VerificationError(
            "current owner-contract feed is not selected for exact Core compatibility"
        )
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
    external_by_name = {row["fileName"]: row for row in external_packages}
    for expected in EXPECTED_WINDOWS_RUNTIME_PACKAGES:
        locked = external_by_name.get(expected["fileName"])
        if locked != {key: value for key, value in expected.items() if key != "sizeBytes"}:
            raise VerificationError("Windows runtime package authority differs from the fixed closure")
    producer_directory = require_relative(
        canonical_owner_feed["producerDirectory"], "Hub canonical producer directory"
    )
    if "/" in producer_directory:
        raise VerificationError("Hub canonical producer directory must be single-level")
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
        if package["ownerDirectory"] not in owner_names | {producer_directory}:
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
    core_runtime_ids = {row["packageId"] for row in core_runtime_packages}
    if core_runtime_ids != set(EXPECTED_CORE_RUNTIME_PACKAGES):
        raise VerificationError("package-plane lock omits a required Core runtime package")
    canonical_ids = {
        row["packageId"] for row in canonical_owner_feed["packages"]
    }
    canonical_names = {
        row["fileName"] for row in canonical_owner_feed["packages"]
    }
    if canonical_ids != HUB_CANONICAL_PACKAGE_IDS:
        raise VerificationError("Hub canonical package set is not exact")
    if (
        package_ids & canonical_ids
        or package_ids & core_runtime_ids
        or canonical_ids & core_runtime_ids
        or package_names & canonical_names
        or package_names & core_runtime_names
        or canonical_names & core_runtime_names
    ):
        raise VerificationError("Core, Hub, and UI package authorities overlap")
    if (
        package_ids & external_ids
        or package_names & external_names
        or canonical_ids & external_ids
        or canonical_names & external_names
        or core_runtime_ids & external_ids
        or core_runtime_names & external_names
    ):
        raise VerificationError("internal and external package authorities overlap")
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
    caches: Path,
    parent: dict[str, str] | None = None,
    *,
    trusted_dotnet_root: Path,
) -> dict[str, str]:
    if caches.is_symlink() or (caches.exists() and not caches.is_dir()):
        raise VerificationError("isolated child cache root is not an exact directory")
    caches.mkdir(mode=0o700, parents=True, exist_ok=True)
    incoming = os.environ if parent is None else parent
    dotnet = trusted_dotnet_root / "dotnet"
    for command in (TRUSTED_BASH, TRUSTED_GIT, TRUSTED_PYTHON3, dotnet):
        try:
            metadata = command.lstat()
        except OSError as exc:
            raise VerificationError(f"trusted child executable is unavailable: {command}") from exc
        if command.is_symlink() or not stat.S_ISREG(metadata.st_mode) or not os.access(command, os.X_OK):
            raise VerificationError(f"trusted child executable is invalid: {command}")
    path_value = f"{trusted_dotnet_root}:{TRUSTED_SYSTEM_PATH}"
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
        row = secure_regular_file_inventory(
            path,
            label="same-run package",
            receipt_path=path.name,
            validate_nuget=True,
        )
        digest = row["sha256"]
        if locked_sha256 is not None and path.name in locked_sha256:
            if digest != locked_sha256[path.name]:
                raise VerificationError(f"locked package changed: {path.name}")
        rows.append(
            {
                "fileName": path.name,
                "sha256": digest,
                "sizeBytes": row["sizeBytes"],
            }
        )
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
    expected_size = EXPECTED_WINDOWS_RUNTIME_PACKAGE_SIZES.get(package["fileName"])
    if (
        size == 0
        or digest.hexdigest() != package["sha256"]
        or (expected_size is not None and size != expected_size)
    ):
        target.unlink(missing_ok=True)
        raise VerificationError(
            f"external package digest or fixed size differs: {package['fileName']}"
        )


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


def _stable_file_identity(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_nlink,
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_ctime_ns,
    )


def secure_regular_file_inventory(
    path: Path,
    *,
    label: str,
    receipt_path: str | None = None,
    validate_nuget: bool = False,
) -> dict[str, Any]:
    try:
        path_metadata = path.lstat()
        descriptor = os.open(
            path,
            os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
        )
    except OSError as exc:
        raise VerificationError(f"{label} is unavailable: {path}") from exc
    try:
        opened_metadata = os.fstat(descriptor)
        if (
            path.is_symlink()
            or not stat.S_ISREG(path_metadata.st_mode)
            or not stat.S_ISREG(opened_metadata.st_mode)
            or path_metadata.st_nlink != 1
            or opened_metadata.st_nlink != 1
            or _stable_file_identity(path_metadata)
            != _stable_file_identity(opened_metadata)
        ):
            raise VerificationError(
                f"{label} must be one stable regular non-linked file: {path}"
            )
        if validate_nuget:
            try:
                with os.fdopen(os.dup(descriptor), "rb") as package_stream:
                    with ZipFile(package_stream) as package:
                        names = package.namelist()
                        if (
                            not names
                            or len(names) != len(set(names))
                            or not any(name.endswith(".nuspec") for name in names)
                        ):
                            raise VerificationError(
                                f"package ZIP inventory is invalid: {path.name}"
                            )
            except BadZipFile as exc:
                raise VerificationError(
                    f"package is not a valid NuGet ZIP: {path.name}"
                ) from exc
        os.lseek(descriptor, 0, os.SEEK_SET)
        digest = hashlib.sha256()
        while chunk := os.read(descriptor, 1024 * 1024):
            digest.update(chunk)
        after_metadata = os.fstat(descriptor)
        final_path_metadata = path.lstat()
        if (
            _stable_file_identity(opened_metadata)
            != _stable_file_identity(after_metadata)
            or _stable_file_identity(opened_metadata)
            != _stable_file_identity(final_path_metadata)
        ):
            raise VerificationError(f"{label} changed while it was inventoried: {path}")
        return {
            "path": receipt_path if receipt_path is not None else str(path),
            "sha256": digest.hexdigest(),
            "sizeBytes": opened_metadata.st_size,
        }
    finally:
        os.close(descriptor)


def exact_file_inventory(path: Path) -> dict[str, Any]:
    return secure_regular_file_inventory(path, label="exact file")


def secure_regular_file_bytes(path: Path, *, label: str) -> bytes:
    before = secure_regular_file_inventory(path, label=label)
    metadata = path.lstat()
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        opened_metadata = os.fstat(descriptor)
        if _stable_file_identity(metadata) != _stable_file_identity(opened_metadata):
            raise VerificationError(f"{label} changed before exact-byte read")
        chunks: list[bytes] = []
        while chunk := os.read(descriptor, 1024 * 1024):
            chunks.append(chunk)
        if _stable_file_identity(opened_metadata) != _stable_file_identity(
            os.fstat(descriptor)
        ):
            raise VerificationError(f"{label} changed during exact-byte read")
    finally:
        os.close(descriptor)
    content = b"".join(chunks)
    after = secure_regular_file_inventory(path, label=label)
    if before != after or hashlib.sha256(content).hexdigest() != before["sha256"]:
        raise VerificationError(f"{label} exact bytes differ from its inventory")
    return content


def raise_tree_walk_error(error: OSError) -> None:
    raise VerificationError(f"publish asset tree traversal failed: {error}") from error


def require_owned_traversable_directory(path: Path, label: str) -> os.stat_result:
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise VerificationError(f"{label} is unavailable: {path}") from exc
    if (
        path.is_symlink()
        or not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or stat.S_IMODE(metadata.st_mode) & 0o500 != 0o500
    ):
        raise VerificationError(
            f"{label} must be an euid-owned readable/traversable directory: {path}"
        )
    return metadata


def directory_asset_inventory(root: Path) -> list[dict[str, Any]]:
    require_owned_traversable_directory(root, "asset inventory root")
    rows: list[dict[str, Any]] = []
    for directory, directory_names, file_names in os.walk(
        root,
        followlinks=False,
        onerror=raise_tree_walk_error,
    ):
        directory_path = Path(directory)
        require_owned_traversable_directory(
            directory_path,
            "publish asset directory",
        )
        directory_names.sort()
        file_names.sort()
        if directory_path != root and not directory_names and not file_names:
            raise VerificationError("publish assets contain an unbound empty directory")
        for name in directory_names:
            path = directory_path / name
            require_owned_traversable_directory(path, "publish asset directory")
        for name in file_names:
            path = directory_path / name
            relative = path.relative_to(root).as_posix()
            require_relative(relative, "publish asset path")
            rows.append(
                secure_regular_file_inventory(
                    path,
                    label="publish asset",
                    receipt_path=relative,
                )
            )
    return sorted(rows, key=lambda row: row["path"])


def validate_windows_asset_inventory(rows: list[dict[str, Any]]) -> None:
    invalid_characters = frozenset('<>:"\\|?*')
    reserved = {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        *(f"COM{number}" for number in range(1, 10)),
        *(f"LPT{number}" for number in range(1, 10)),
    }
    folded_paths: set[str] = set()
    for row in rows:
        relative = require_relative(row.get("path"), "Windows publish asset path")
        pure = PurePosixPath(relative)
        for component in pure.parts:
            if (
                component.endswith((" ", "."))
                or any(character in invalid_characters or ord(character) < 32 for character in component)
                or component.split(".", 1)[0].upper() in reserved
            ):
                raise VerificationError(
                    f"publish asset path is invalid on Windows: {relative}"
                )
        folded = relative.casefold()
        if folded in folded_paths:
            raise VerificationError("publish asset paths collide under Windows case-folding")
        folded_paths.add(folded)


def validate_retained_bundle_target(target: Path) -> tuple[Path, int]:
    if not target.is_absolute() or not target.name:
        raise VerificationError("retained bundle output must be an absolute directory path")
    parent = target.parent
    try:
        parent_metadata = parent.lstat()
        physical_parent = parent.resolve(strict=True)
    except OSError as exc:
        raise VerificationError("retained bundle parent must already exist") from exc
    if (
        parent.is_symlink()
        or not stat.S_ISDIR(parent_metadata.st_mode)
        or physical_parent != parent
        or parent_metadata.st_uid != os.geteuid()
        or stat.S_IMODE(parent_metadata.st_mode) & 0o022
    ):
        raise VerificationError(
            "retained bundle parent must be physical, euid-owned, and not group/world-writable"
        )
    try:
        target.lstat()
    except FileNotFoundError:
        pass
    except OSError as exc:
        raise VerificationError("retained bundle target could not be inspected") from exc
    else:
        raise VerificationError("retained bundle target must be absent")
    return parent, parent_metadata.st_dev


def require_same_filesystem(parent_device: int, staging: Path) -> os.stat_result:
    metadata = staging.lstat()
    if staging.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
        raise VerificationError("retained bundle staging is not a physical directory")
    if metadata.st_uid != os.geteuid() or stat.S_IMODE(metadata.st_mode) != 0o700:
        raise VerificationError("retained bundle staging must be euid-owned mode 0700")
    if metadata.st_dev != parent_device:
        raise VerificationError("retained bundle staging and target are cross-filesystem")
    return metadata


def fsync_directory(path: Path) -> None:
    flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_NOFOLLOW", 0)
    descriptor = os.open(path, flags)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def fsync_asset_tree(root: Path) -> None:
    directories: list[Path] = []
    require_owned_traversable_directory(root, "fsync asset root")
    for directory, directory_names, file_names in os.walk(
        root,
        followlinks=False,
        onerror=raise_tree_walk_error,
    ):
        directory_path = Path(directory)
        require_owned_traversable_directory(directory_path, "fsync asset directory")
        directories.append(directory_path)
        for name in sorted(directory_names):
            path = directory_path / name
            require_owned_traversable_directory(path, "fsync asset directory")
        for name in sorted(file_names):
            path = directory_path / name
            metadata = path.lstat()
            if (
                path.is_symlink()
                or not stat.S_ISREG(metadata.st_mode)
                or metadata.st_nlink != 1
            ):
                raise VerificationError("publish assets changed before retention")
            descriptor = os.open(
                path,
                os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
            )
            try:
                opened_metadata = os.fstat(descriptor)
                if _stable_file_identity(metadata) != _stable_file_identity(opened_metadata):
                    raise VerificationError("publish assets changed before retention")
                os.fsync(descriptor)
                if _stable_file_identity(opened_metadata) != _stable_file_identity(
                    os.fstat(descriptor)
                ):
                    raise VerificationError("publish assets changed during fsync")
            finally:
                os.close(descriptor)
    for directory in sorted(directories, key=lambda path: len(path.parts), reverse=True):
        fsync_directory(directory)


def remove_owned_staging_tree(root: Path, identity: tuple[int, int]) -> None:
    metadata = root.lstat()
    if (
        root.is_symlink()
        or not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or (metadata.st_dev, metadata.st_ino) != tuple(identity)
    ):
        raise VerificationError("owned staging identity changed before cleanup")

    def repair_owned_tree(path: Path) -> None:
        candidate = path.lstat()
        if path.is_symlink() or not stat.S_ISDIR(candidate.st_mode):
            raise VerificationError("owned staging directory changed during cleanup")
        if candidate.st_uid != os.geteuid():
            raise VerificationError("owned staging directory owner changed during cleanup")
        path.chmod(0o700)
        try:
            entries = list(os.scandir(path))
        except OSError as exc:
            raise VerificationError("owned staging could not be traversed for cleanup") from exc
        for entry in entries:
            child = path / entry.name
            child_metadata = child.lstat()
            if child_metadata.st_uid != os.geteuid():
                raise VerificationError("owned staging entry owner changed during cleanup")
            if child.is_symlink():
                continue
            if stat.S_ISDIR(child_metadata.st_mode):
                repair_owned_tree(child)

    repair_owned_tree(root)
    shutil.rmtree(root)
    if root.exists() or root.is_symlink():
        raise VerificationError("owned staging cleanup was incomplete")


def atomic_rename_noreplace(source: Path, target: Path) -> None:
    if os.name != "posix":
        raise VerificationError("atomic no-replace retention requires POSIX renameat2")
    libc = ctypes.CDLL(None, use_errno=True)
    renameat2 = getattr(libc, "renameat2", None)
    if renameat2 is None:
        raise VerificationError("atomic no-replace retention is unavailable")
    renameat2.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    renameat2.restype = ctypes.c_int
    at_fdcwd = -100
    rename_noreplace = 1
    result = renameat2(
        at_fdcwd,
        os.fsencode(source),
        at_fdcwd,
        os.fsencode(target),
        rename_noreplace,
    )
    if result == 0:
        return
    error_number = ctypes.get_errno()
    if error_number == errno.EEXIST:
        raise VerificationError("retained bundle target appeared before atomic rename")
    if error_number == errno.EXDEV:
        raise VerificationError("retained bundle atomic rename was cross-filesystem")
    raise VerificationError(
        f"retained bundle atomic no-replace rename failed: {os.strerror(error_number)}"
    )


def require_clean_consumer_head(
    consumer: Path,
    environment: dict[str, str],
    expected_commit: str,
) -> None:
    head = run(
        [str(TRUSTED_GIT), "rev-parse", "HEAD"],
        cwd=consumer,
        environment=environment,
        capture=True,
    ).stdout.strip()
    status = run(
        [str(TRUSTED_GIT), "status", "--porcelain"],
        cwd=consumer,
        environment=environment,
        capture=True,
    ).stdout
    if head != expected_commit or status:
        raise VerificationError("consumer commit or clean state changed during retention")


def capture_consumer_authority(
    repo_root: Path,
    supplied_lock: Path,
) -> tuple[str, Path, bytes, dict[str, Any]]:
    canonical_lock = repo_root / CANONICAL_PACKAGE_PLANE_LOCK
    try:
        supplied_resolved = supplied_lock.resolve(strict=True)
        canonical_resolved = canonical_lock.resolve(strict=True)
    except OSError as exc:
        raise VerificationError("canonical in-repo package-plane lock is unavailable") from exc
    if supplied_lock.is_symlink() or canonical_lock.is_symlink() or supplied_resolved != canonical_resolved:
        raise VerificationError("--lock must name the canonical in-repo package-plane lock")
    top_level = subprocess.run(
        [str(TRUSTED_GIT), "rev-parse", "--show-toplevel"],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    ).stdout.strip()
    if Path(top_level).resolve(strict=True) != repo_root:
        raise VerificationError("consumer repository root is not the exact Git top-level")
    head = subprocess.run(
        [str(TRUSTED_GIT), "rev-parse", "HEAD"],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    ).stdout.strip()
    status = subprocess.run(
        [str(TRUSTED_GIT), "status", "--porcelain"],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    ).stdout
    if not COMMIT_RE.fullmatch(head) or status:
        raise VerificationError("consumer checkout must have one exact clean commit")
    lock_bytes = secure_regular_file_bytes(
        canonical_lock,
        label="canonical consumer package-plane lock",
    )
    lock_inventory = secure_regular_file_inventory(
        canonical_lock,
        label="canonical consumer package-plane lock",
        receipt_path=CANONICAL_PACKAGE_PLANE_LOCK.as_posix(),
    )
    committed_lock_bytes = subprocess.run(
        [
            str(TRUSTED_GIT),
            "show",
            f"{head}:{CANONICAL_PACKAGE_PLANE_LOCK.as_posix()}",
        ],
        cwd=repo_root,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    ).stdout
    final_status = subprocess.run(
        [str(TRUSTED_GIT), "status", "--porcelain"],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    ).stdout
    if lock_bytes != committed_lock_bytes or final_status:
        raise VerificationError("canonical consumer lock is not the exact captured HEAD bytes")
    return head, canonical_lock, lock_bytes, lock_inventory


def clone_exact_consumer(
    repo_root: Path,
    consumer: Path,
    consumer_parent: Path,
    environment: dict[str, str],
    expected_commit: str,
    expected_lock_bytes: bytes,
) -> dict[str, Any]:
    run(
        [
            str(TRUSTED_GIT),
            "clone",
            "--quiet",
            "--no-local",
            "--no-checkout",
            str(repo_root),
            str(consumer),
        ],
        cwd=consumer_parent,
        environment=environment,
    )
    run(
        [str(TRUSTED_GIT), "checkout", "--quiet", "--detach", expected_commit],
        cwd=consumer,
        environment=environment,
    )
    require_clean_consumer_head(consumer, environment, expected_commit)
    cloned_lock = consumer / CANONICAL_PACKAGE_PLANE_LOCK
    cloned_lock_bytes = secure_regular_file_bytes(
        cloned_lock,
        label="cloned consumer package-plane lock",
    )
    if cloned_lock_bytes != expected_lock_bytes:
        raise VerificationError("cloned consumer lock bytes differ from captured authority")
    return secure_regular_file_inventory(
        cloned_lock,
        label="cloned consumer package-plane lock",
        receipt_path=CANONICAL_PACKAGE_PLANE_LOCK.as_posix(),
    )


def copy_regular_file_exact(source: Path, target: Path) -> None:
    source_before = secure_regular_file_inventory(source, label="retained source file")
    source_metadata = source.lstat()
    if target.exists() or target.is_symlink():
        raise VerificationError("retained copy target must be absent")
    source_descriptor = os.open(
        source,
        os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        opened_source_metadata = os.fstat(source_descriptor)
        if _stable_file_identity(source_metadata) != _stable_file_identity(
            opened_source_metadata
        ):
            raise VerificationError("retained source changed before copy")
        target_descriptor = os.open(
            target,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        try:
            while chunk := os.read(source_descriptor, 1024 * 1024):
                offset = 0
                while offset < len(chunk):
                    written = os.write(target_descriptor, chunk[offset:])
                    if written <= 0:
                        raise VerificationError("retained exact-byte copy was partial")
                    offset += written
            os.fchmod(target_descriptor, 0o600)
            os.fsync(target_descriptor)
        finally:
            os.close(target_descriptor)
        if _stable_file_identity(opened_source_metadata) != _stable_file_identity(
            os.fstat(source_descriptor)
        ):
            raise VerificationError("retained source changed during copy")
    finally:
        os.close(source_descriptor)
    source_after = secure_regular_file_inventory(source, label="retained source file")
    target_after = secure_regular_file_inventory(target, label="retained copied file")
    if source_before != source_after or (
        source_before["sha256"],
        source_before["sizeBytes"],
    ) != (
        target_after["sha256"],
        target_after["sizeBytes"],
    ):
        raise VerificationError("retained exact-byte copy inventory differs")
    target_metadata = target.lstat()
    if (
        (source_metadata.st_dev, source_metadata.st_ino)
        == (target_metadata.st_dev, target_metadata.st_ino)
        or target_metadata.st_nlink != 1
        or stat.S_IMODE(target_metadata.st_mode) != 0o600
    ):
        raise VerificationError("retained copy is not a distinct mode-0600 inode")


def copy_inventory_tree(
    source_root: Path,
    target_root: Path,
    expected_inventory: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    if target_root.exists() or target_root.is_symlink():
        raise VerificationError("retained inventory target must be absent")
    target_root.mkdir(mode=0o700)
    source_before = directory_asset_inventory(source_root)
    require_inventory_unchanged(expected_inventory, source_before)
    for row in source_before:
        relative = require_relative(row["path"], "retained inventory path")
        source = source_root / relative
        target = target_root / relative
        missing_parents: list[Path] = []
        current = target.parent
        while current != target_root and not current.exists():
            missing_parents.append(current)
            current = current.parent
        for directory in reversed(missing_parents):
            directory.mkdir(mode=0o700)
        copy_regular_file_exact(source, target)
    source_after = directory_asset_inventory(source_root)
    require_inventory_unchanged(source_before, source_after)
    copied = directory_asset_inventory(target_root)
    require_inventory_unchanged(source_after, copied)
    return copied


def package_rows_as_asset_rows(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "path": row["fileName"],
            "sha256": row["sha256"],
            "sizeBytes": row["sizeBytes"],
        }
        for row in rows
    ]


def publish_and_retain_windows_bundle(
    target: Path,
    *,
    consumer: Path,
    consumer_commit: str,
    consumer_config: Path,
    consumer_lock_inventory: dict[str, Any],
    environment: dict[str, str],
    expected_feed_inventory: list[dict[str, Any]],
    expected_names: set[str],
    feed: Path,
    locked_package_sha256: dict[str, str],
    release_version: str,
    release_channel: str,
) -> dict[str, Any]:
    release_version, release_channel = require_windows_release_authority(
        release_version,
        release_channel,
    )
    parent, parent_device = validate_retained_bundle_target(target)
    staging = Path(tempfile.mkdtemp(prefix=".chummer-win-retain-", dir=parent))
    publish_output = Path(
        tempfile.mkdtemp(prefix="chummer-win-publish-", dir=consumer.parent)
    )
    retained = False
    initial_staging_metadata = staging.lstat()
    staging_identity: tuple[int, int] = (
        initial_staging_metadata.st_dev,
        initial_staging_metadata.st_ino,
    )
    publish_output_metadata = publish_output.lstat()
    publish_output_identity = (
        publish_output_metadata.st_dev,
        publish_output_metadata.st_ino,
    )
    try:
        staging_metadata = require_same_filesystem(parent_device, staging)
        staging_identity = (staging_metadata.st_dev, staging_metadata.st_ino)
        require_clean_consumer_head(consumer, environment, consumer_commit)
        feed_before = package_inventory(feed, expected_names, locked_package_sha256)
        require_inventory_unchanged(expected_feed_inventory, feed_before)
        config_before = exact_file_inventory(consumer_config)
        assets_before = directory_asset_inventory(publish_output)
        if assets_before:
            raise VerificationError("Windows publish output staging was not empty")

        publish_arguments = [
            str(TRUSTED_BASH),
            "scripts/ai/with-package-plane.sh",
            "publish",
            WINDOWS_PUBLISH_PROJECT,
            "-c",
            "Release",
            "-f",
            WINDOWS_PUBLISH_FRAMEWORK,
            "-r",
            WINDOWS_PUBLISH_RID,
            "--self-contained",
            "true",
            f"-p:ChummerDesktopReleaseVersion={release_version}",
            f"-p:ChummerDesktopReleaseChannel={release_channel}",
            "--output",
            str(publish_output),
            "-warnaserror:NU1603,NU1608",
            "--disable-build-servers",
            "--nologo",
            "-v",
            "minimal",
        ]
        run(publish_arguments, cwd=consumer, environment=environment)

        feed_after = package_inventory(feed, expected_names, locked_package_sha256)
        require_inventory_unchanged(feed_before, feed_after)
        config_after = exact_file_inventory(consumer_config)
        if config_before != config_after:
            raise VerificationError("same-run NuGet config changed during Windows publish")
        assets_after = directory_asset_inventory(publish_output)
        validate_windows_asset_inventory(assets_after)
        asset_names = {row["path"] for row in assets_after}
        if not REQUIRED_WINDOWS_PUBLISH_ASSETS.issubset(asset_names):
            raise VerificationError("Windows publish closure is missing required desktop assets")
        require_clean_consumer_head(consumer, environment, consumer_commit)

        retained_assets_root = staging / "assets"
        retained_feed_root = staging / "feed"
        retained_config_root = staging / "config"
        retained_assets = copy_inventory_tree(
            publish_output,
            retained_assets_root,
            assets_after,
        )
        validate_windows_asset_inventory(retained_assets)
        copied_feed_assets = copy_inventory_tree(
            feed,
            retained_feed_root,
            package_rows_as_asset_rows(feed_after),
        )
        retained_feed = package_inventory(
            retained_feed_root,
            expected_names,
            locked_package_sha256,
        )
        require_inventory_unchanged(feed_after, retained_feed)
        require_inventory_unchanged(
            package_rows_as_asset_rows(retained_feed),
            copied_feed_assets,
        )
        retained_config_root.mkdir(mode=0o700)
        retained_config_path = retained_config_root / "NuGet.Config"
        write_nuget_config(retained_config_path, target / "feed")
        retained_config_path.chmod(0o600)
        retained_config_inventory = secure_regular_file_inventory(
            retained_config_path,
            label="retained NuGet config",
            receipt_path=str(target / "config" / "NuGet.Config"),
        )

        manifest_payload = {
            "assetInventory": {
                "afterPublish": assets_after,
                "afterPublishCount": len(assets_after),
                "afterPublishSha256": inventory_sha256(assets_after),
                "beforePublish": assets_before,
                "beforePublishCount": len(assets_before),
                "beforePublishSha256": inventory_sha256(assets_before),
                "retained": retained_assets,
                "retainedCount": len(retained_assets),
                "retainedSha256": inventory_sha256(retained_assets),
            },
            "atomicallyRetained": True,
            "authoritative": True,
            "buildNugetConfigEvidence": {
                "afterPublish": config_after,
                "beforePublish": config_before,
                "ephemeralBuildPath": True,
            },
            "consumerCommit": consumer_commit,
            "contractName": RETAINED_WINDOWS_BUNDLE_CONTRACT,
            "contractVersion": 2,
            "deterministicRepacking": False,
            "feedInventory": {
                "afterPublish": feed_after,
                "afterPublishCount": len(feed_after),
                "afterPublishSha256": inventory_sha256(feed_after),
                "beforePublish": feed_before,
                "beforePublishCount": len(feed_before),
                "beforePublishSha256": inventory_sha256(feed_before),
                "ephemeralBuildPath": str(feed),
                "retained": retained_feed,
                "retainedCount": len(retained_feed),
                "retainedPath": str(target / "feed"),
                "retainedSha256": inventory_sha256(retained_feed),
            },
            "generatedAt": datetime.now(UTC)
            .replace(microsecond=0)
            .isoformat()
            .replace("+00:00", "Z"),
            "packagePlaneLock": consumer_lock_inventory,
            "publish": {
                "arguments": publish_arguments,
                "executableAuthority": {
                    "bash": str(TRUSTED_BASH),
                    "git": str(TRUSTED_GIT),
                    "path": environment["PATH"],
                    "python3": str(TRUSTED_PYTHON3),
                },
                "framework": WINDOWS_PUBLISH_FRAMEWORK,
                "project": WINDOWS_PUBLISH_PROJECT,
                "projectSha256": source_digest(consumer / WINDOWS_PUBLISH_PROJECT),
                "runtimeIdentifier": WINDOWS_PUBLISH_RID,
                "releaseChannel": release_channel,
                "releaseVersion": release_version,
                "selfContained": True,
                "shell": False,
                "status": "passed",
            },
            "releaseEligibility": {
                "eligible": False,
                "reason": (
                    "native packaging, candidate sealing, upload, and deployment "
                    "gates are outside this verifier"
                ),
            },
            "release": {
                "channel": release_channel,
                "version": release_version,
            },
            "retainedNugetConfig": {
                **retained_config_inventory,
                "packageSource": str(target / "feed"),
                "usableAtRetainedTarget": True,
            },
            "sourceHeadChecks": {
                "afterPublish": consumer_commit,
                "beforePublish": consumer_commit,
                "clean": True,
            },
            "status": "passed",
            "targetPath": str(target),
        }
        manifest_path = staging / "manifest.json"
        exact_write_receipt(manifest_path, manifest_payload)
        manifest_inventory = secure_regular_file_inventory(
            manifest_path,
            label="retained authoritative manifest",
            receipt_path=str(target / "manifest.json"),
        )

        top_level = {path.name for path in staging.iterdir()}
        if top_level != {"assets", "config", "feed", "manifest.json"}:
            raise VerificationError("retained bundle contains an unexpected top-level entry")

        fsync_asset_tree(staging)
        require_same_filesystem(parent_device, staging)
        require_inventory_unchanged(
            retained_assets,
            directory_asset_inventory(retained_assets_root),
        )
        require_inventory_unchanged(
            retained_feed,
            package_inventory(retained_feed_root, expected_names, locked_package_sha256),
        )
        if retained_config_inventory != secure_regular_file_inventory(
            retained_config_path,
            label="retained NuGet config",
            receipt_path=str(target / "config" / "NuGet.Config"),
        ):
            raise VerificationError("retained NuGet config changed before atomic rename")
        if manifest_inventory != secure_regular_file_inventory(
            manifest_path,
            label="retained authoritative manifest",
            receipt_path=str(target / "manifest.json"),
        ):
            raise VerificationError("retained authoritative manifest changed before atomic rename")
        bundle_before_rename = directory_asset_inventory(staging)
        staging_metadata = staging.lstat()
        staging_identity = (staging_metadata.st_dev, staging_metadata.st_ino)
        atomic_rename_noreplace(staging, target)
        retained = True
        fsync_directory(parent)

        retained_metadata = target.lstat()
        if (
            target.is_symlink()
            or not stat.S_ISDIR(retained_metadata.st_mode)
            or (retained_metadata.st_dev, retained_metadata.st_ino) != staging_identity
        ):
            raise VerificationError("atomically retained bundle identity changed")
        final_assets = directory_asset_inventory(target / "assets")
        validate_windows_asset_inventory(final_assets)
        final_feed = package_inventory(
            target / "feed", expected_names, locked_package_sha256
        )
        final_config = secure_regular_file_inventory(
            target / "config" / "NuGet.Config",
            label="final retained NuGet config",
            receipt_path=str(target / "config" / "NuGet.Config"),
        )
        final_manifest = secure_regular_file_inventory(
            target / "manifest.json",
            label="final retained authoritative manifest",
            receipt_path=str(target / "manifest.json"),
        )
        require_inventory_unchanged(retained_assets, final_assets)
        require_inventory_unchanged(retained_feed, final_feed)
        if retained_config_inventory != final_config or manifest_inventory != final_manifest:
            raise VerificationError("retained config or manifest inventory changed after rename")
        if load_json(target / "manifest.json") != manifest_payload:
            raise VerificationError("retained authoritative manifest content changed")
        final_bundle_inventory = directory_asset_inventory(target)
        require_inventory_unchanged(bundle_before_rename, final_bundle_inventory)
        fsync_directory(parent)
        return {
            "_retainedBundleIdentity": staging_identity,
            "atomicallyRetained": True,
            "authority": False,
            "bundleInventoryCount": len(final_bundle_inventory),
            "bundleInventorySha256": inventory_sha256(final_bundle_inventory),
            "consumerCommit": consumer_commit,
            "contractName": "chummer6-ui.retained-windows-publish-closure-pointer",
            "contractVersion": 2,
            "manifest": final_manifest,
            "manifestIsAuthoritative": True,
            "release": {
                "channel": release_channel,
                "version": release_version,
            },
            "status": "passed",
            "targetPath": str(target),
        }
    except BaseException as original_error:
        if retained:
            try:
                rollback_retained_bundle(target, staging_identity)
                retained = False
            except BaseException as rollback_error:
                raise VerificationError(
                    f"retained bundle verification failed and rollback was unsafe: {rollback_error}"
                ) from original_error
        raise
    finally:
        for owned_staging, owned_identity in (
            (publish_output, publish_output_identity),
            (staging, staging_identity),
        ):
            try:
                owned_staging.lstat()
            except FileNotFoundError:
                continue
            remove_owned_staging_tree(owned_staging, owned_identity)


def write_nuget_config(path: Path, feed: Path | None) -> None:
    configuration = ET.Element("configuration")
    package_sources = ET.SubElement(configuration, "packageSources")
    ET.SubElement(package_sources, "clear")
    package_source_mapping = ET.SubElement(configuration, "packageSourceMapping")
    if feed is not None:
        ET.SubElement(
            package_sources,
            "add",
            {"key": "same-run-local-feed", "value": str(feed)},
        )
        source = ET.SubElement(
            package_source_mapping,
            "packageSource",
            {"key": "same-run-local-feed"},
        )
        ET.SubElement(source, "package", {"pattern": "*"})
    ET.indent(configuration, space="  ")
    ET.ElementTree(configuration).write(
        path,
        encoding="utf-8",
        xml_declaration=True,
        short_empty_elements=True,
    )
    with path.open("ab") as stream:
        stream.write(b"\n")
    require_exact_nuget_config_source(path, feed)


def require_exact_nuget_config_source(path: Path, feed: Path | None) -> None:
    try:
        configuration = ET.parse(path).getroot()
    except (OSError, ET.ParseError) as exc:
        raise VerificationError("same-run NuGet config is unavailable or invalid XML") from exc
    if configuration.tag != "configuration" or configuration.attrib:
        raise VerificationError("same-run NuGet config root is not exact")
    package_sources = configuration.findall("packageSources")
    mappings = configuration.findall("packageSourceMapping")
    if len(package_sources) != 1 or len(mappings) != 1:
        raise VerificationError("same-run NuGet config sections are not exact")
    source_children = list(package_sources[0])
    mapping_children = list(mappings[0])
    expected_source_count = 2 if feed is not None else 1
    expected_mapping_count = 1 if feed is not None else 0
    if (
        len(source_children) != expected_source_count
        or source_children[0].tag != "clear"
        or source_children[0].attrib
        or len(mapping_children) != expected_mapping_count
    ):
        raise VerificationError("same-run NuGet config source cardinality is not exact")
    if feed is None:
        return
    add = source_children[1]
    mapping = mapping_children[0]
    if (
        add.tag != "add"
        or add.attrib
        != {"key": "same-run-local-feed", "value": str(feed)}
        or mapping.tag != "packageSource"
        or mapping.attrib != {"key": "same-run-local-feed"}
        or len(mapping) != 1
        or mapping[0].tag != "package"
        or mapping[0].attrib != {"pattern": "*"}
    ):
        raise VerificationError("same-run NuGet config package source differs")


def acquire_owner(owner: dict[str, str], owners_root: Path, environment: dict[str, str]) -> Path:
    target = owners_root / owner["directory"]
    target.mkdir(mode=0o700)
    run([str(TRUSTED_GIT), "init", "--quiet"], cwd=target, environment=environment)
    run([str(TRUSTED_GIT), "remote", "add", "origin", owner["repository"]], cwd=target, environment=environment)
    run(
        [str(TRUSTED_GIT), "fetch", "--quiet", "--depth=1", "origin", owner["commit"]],
        cwd=target,
        environment=environment,
    )
    run([str(TRUSTED_GIT), "checkout", "--quiet", "--detach", "FETCH_HEAD"], cwd=target, environment=environment)
    actual = run([str(TRUSTED_GIT), "rev-parse", "HEAD"], cwd=target, environment=environment, capture=True).stdout.strip()
    if actual != owner["commit"]:
        raise VerificationError(f"owner checkout differs: {owner['directory']}")
    status = run([str(TRUSTED_GIT), "status", "--porcelain"], cwd=target, environment=environment, capture=True).stdout
    if status:
        raise VerificationError(f"owner checkout is dirty: {owner['directory']}")
    return target


def require_package_identity(
    path: Path,
    *,
    package_id: str,
    version: str,
    dependencies: dict[str, str],
) -> None:
    try:
        with ZipFile(path) as package:
            nuspec_names = [name for name in package.namelist() if name.endswith(".nuspec")]
            if len(nuspec_names) != 1:
                raise VerificationError(
                    f"UI-owner package must contain one nuspec: {path.name}"
                )
            document = ET.fromstring(package.read(nuspec_names[0]))
    except (BadZipFile, ET.ParseError, OSError) as exc:
        raise VerificationError(f"UI-owner package metadata is invalid: {path.name}") from exc
    metadata = next(
        (element for element in document.iter() if element.tag.rsplit("}", 1)[-1] == "metadata"),
        None,
    )
    if metadata is None:
        raise VerificationError(f"UI-owner package metadata is missing: {path.name}")

    def one_text(name: str) -> str | None:
        values = [
            (element.text or "").strip()
            for element in metadata
            if element.tag.rsplit("}", 1)[-1] == name
        ]
        return values[0] if len(values) == 1 else None

    if one_text("id") != package_id or one_text("version") != version:
        raise VerificationError(f"UI-owner package identity differs: {path.name}")
    actual_dependencies: dict[str, str] = {}
    for element in metadata.iter():
        if element.tag.rsplit("}", 1)[-1] != "dependency":
            continue
        dependency_id = element.attrib.get("id")
        dependency_version = element.attrib.get("version")
        if (
            not isinstance(dependency_id, str)
            or not dependency_id
            or not isinstance(dependency_version, str)
            or not dependency_version
            or dependency_id in actual_dependencies
        ):
            raise VerificationError(
                f"UI-owner package dependency metadata is invalid: {path.name}"
            )
        actual_dependencies[dependency_id] = dependency_version
    if actual_dependencies != dependencies:
        raise VerificationError(f"UI-owner package dependencies differ: {path.name}")


def ui_owner_dependency_versions(package_id: str) -> dict[str, str]:
    if package_id == "Chummer.Campaign.Contracts":
        return {"Chummer.Engine.Contracts": CORE_RUNTIME_PACKAGE_VERSION}
    if package_id == "Chummer.Ui.Kit":
        return {}
    raise VerificationError(f"unknown UI-owner package target: {package_id}")


def build_ui_owner_producer_lock(
    lock: dict[str, Any],
    *,
    recipe_commit: str,
    recipe_sha256: str,
) -> dict[str, Any]:
    if not COMMIT_RE.fullmatch(recipe_commit) or not SHA256_RE.fullmatch(
        recipe_sha256
    ):
        raise VerificationError("UI-owner producer recipe authority is not exact")
    return {
        "contract": UI_OWNER_PRODUCER_LOCK_CONTRACT,
        "dependencyAuthorityCacheKey": upstream_owner_package_cache_manifest(lock)[
            "cacheKey"
        ],
        "packageRecipeCommit": recipe_commit,
        "packageRecipePath": "scripts/ai/verify_fresh_checkout_package_plane.py",
        "packageRecipeSha256": recipe_sha256,
        "packages": [
            {
                "dependencies": ui_owner_dependency_versions(package["packageId"]),
                "fileName": package["fileName"],
                "packageId": package["packageId"],
                "version": package["version"],
            }
            for package in lock["packages"]
        ],
        "publicationAuthorized": False,
        "sdkArchiveSha512": lock["sdkArchive"]["sha512"],
        "sdkVersion": lock["sdkVersion"],
        "sources": [
            {
                "commit": source["commit"],
                "ownerDirectory": source["ownerDirectory"],
                "packageId": package_id,
                "project": source["project"],
                "projectSha256": source["projectSha256"],
                "repository": source["repository"],
                "sourceTree": source["sourceTree"],
            }
            for package_id, source in EXPECTED_UI_OWNER_SOURCES.items()
        ],
    }


def encoded_json(payload: dict[str, Any]) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def ui_owner_feed_authority(
    *,
    inventory: dict[str, Any],
    receipt: dict[str, Any],
    rows: list[dict[str, Any]],
) -> dict[str, Any]:
    inventory_sha256 = hashlib.sha256(encoded_json(inventory)).hexdigest()
    receipt_sha256 = hashlib.sha256(encoded_json(receipt)).hexdigest()
    return {
        "dependencyAuthorityCacheKey": inventory["dependencyAuthorityCacheKey"],
        "inventoryContract": UI_OWNER_FEED_INVENTORY_CONTRACT,
        "inventoryFileName": "ui-owner-packages.inventory.json",
        "inventorySha256": inventory_sha256,
        "packageRecipeCommit": inventory["packageRecipeCommit"],
        "packageRecipeSha256": inventory["packageRecipeSha256"],
        "packages": rows,
        "producerLockPath": UI_OWNER_PRODUCER_LOCK_PATH,
        "producerLockSha256": inventory["producerLockSha256"],
        "receiptContract": UI_OWNER_FEED_RECEIPT_CONTRACT,
        "receiptFileName": "ui-owner-packages.receipt.json",
        "receiptSha256": receipt_sha256,
        "sdkVersion": inventory["sdkVersion"],
    }


def produce_ui_owner_packages(
    lock: dict[str, Any],
    *,
    repo_root: Path,
    owner_roots: dict[str, Path],
    sdk_root: Path,
    feed: Path,
    pack_config: Path,
    environment: dict[str, str],
    recipe_commit: str,
    producer_lock_sha256: str,
) -> tuple[dict[str, Any], dict[str, Any], list[dict[str, Any]]]:
    if not COMMIT_RE.fullmatch(recipe_commit):
        raise VerificationError("UI-owner package recipe commit is not exact")
    if not SHA256_RE.fullmatch(producer_lock_sha256):
        raise VerificationError("UI-owner producer lock digest is not exact")
    recipe_path = repo_root / "scripts/ai/verify_fresh_checkout_package_plane.py"
    recipe_sha256 = source_digest(recipe_path)
    upstream_manifest = upstream_owner_package_cache_manifest(lock)
    dependency_cache_key = upstream_manifest["cacheKey"]
    feed_before = directory_asset_inventory(feed)
    source_before: dict[str, tuple[str, str, str, str]] = {}
    rows: list[dict[str, Any]] = []
    for package in lock["packages"]:
        package_id = str(package["packageId"])
        target = EXPECTED_PACKAGES.get(package_id)
        source = EXPECTED_UI_OWNER_SOURCES.get(package_id)
        if target is None or source is None:
            raise VerificationError("UI-owner package target differs from the fixed set")
        if target != (
            package["ownerDirectory"],
            package["project"],
            package["fileName"],
            package["version"],
        ):
            raise VerificationError("UI-owner package target authority differs")
        owner_root = owner_roots[source["ownerDirectory"]]
        project = owner_root / source["project"]
        head = run(
            [str(TRUSTED_GIT), "rev-parse", "HEAD"],
            cwd=owner_root,
            environment=environment,
            capture=True,
        ).stdout.strip()
        tree = run(
            [str(TRUSTED_GIT), "rev-parse", "HEAD^{tree}"],
            cwd=owner_root,
            environment=environment,
            capture=True,
        ).stdout.strip()
        status = run(
            [str(TRUSTED_GIT), "status", "--porcelain"],
            cwd=owner_root,
            environment=environment,
            capture=True,
        ).stdout
        project_sha256 = source_digest(project)
        if (
            head != source["commit"]
            or tree != source["sourceTree"]
            or status
            or project_sha256 != source["projectSha256"]
        ):
            raise VerificationError(f"UI-owner source authority differs: {package_id}")
        source_before[package_id] = (head, tree, status, project_sha256)
        output = feed / package["fileName"]
        if output.exists() or output.is_symlink():
            raise VerificationError(f"UI-owner package output already exists: {output.name}")
        run(
            [
                str(sdk_root / "dotnet"),
                "pack",
                str(project),
                "-c",
                "Release",
                "-o",
                str(feed),
                f"-p:PackageVersion={package['version']}",
                f"-p:ChummerWorkspaceRoot={owner_roots[source['ownerDirectory']].parent}",
                "-p:ChummerUseLocalCompatibilityTree=false",
                "-p:ChummerLocalContractsProject=",
                f"-p:ChummerContractsPackageVersion={CANONICAL_ENGINE_CONTRACTS_VERSION}",
                f"-p:ChummerEngineContractsPackageVersion={CANONICAL_ENGINE_CONTRACTS_VERSION}",
                f"-p:ChummerCoreRuntimePackageVersion={CORE_RUNTIME_PACKAGE_VERSION}",
                f"-p:RestoreSources={feed}",
                "-p:RestoreAdditionalProjectSources=",
                f"-p:RestoreConfigFile={pack_config}",
                "-p:RestoreFallbackFolders=",
                "-p:RestoreIgnoreFailedSources=false",
                "-p:RuntimeIdentifiers=",
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
        if not output.is_file() or output.is_symlink():
            raise VerificationError(f"UI-owner pack did not emit exact output: {output.name}")
        require_package_identity(
            output,
            package_id=package_id,
            version=package["version"],
            dependencies=ui_owner_dependency_versions(package_id),
        )
        inventory = secure_regular_file_inventory(
            output,
            label="UI-owner package",
            receipt_path=package["fileName"],
            validate_nuget=True,
        )
        rows.append(
            {
                "commit": source["commit"],
                "fileName": package["fileName"],
                "ownerDirectory": source["ownerDirectory"],
                "packageId": package_id,
                "project": source["project"],
                "projectSha256": source["projectSha256"],
                "repository": source["repository"],
                "sha256": inventory["sha256"],
                "sizeBytes": inventory["sizeBytes"],
                "sourceTree": source["sourceTree"],
                "version": package["version"],
            }
        )
        current = (
            run(
                [str(TRUSTED_GIT), "rev-parse", "HEAD"],
                cwd=owner_root,
                environment=environment,
                capture=True,
            ).stdout.strip(),
            run(
                [str(TRUSTED_GIT), "rev-parse", "HEAD^{tree}"],
                cwd=owner_root,
                environment=environment,
                capture=True,
            ).stdout.strip(),
            run(
                [str(TRUSTED_GIT), "status", "--porcelain"],
                cwd=owner_root,
                environment=environment,
                capture=True,
            ).stdout,
            source_digest(project),
        )
        if current != source_before[package_id]:
            raise VerificationError(f"UI-owner source mutated during pack: {package_id}")
    additions = {row["fileName"] for row in rows}
    feed_after = directory_asset_inventory(feed)
    if {row["path"] for row in feed_after} != {
        *(row["path"] for row in feed_before),
        *additions,
    }:
        raise VerificationError("UI-owner producer emitted missing or unexpected files")
    for row in feed_before:
        if next((candidate for candidate in feed_after if candidate["path"] == row["path"]), None) != row:
            raise VerificationError("UI-owner producer changed dependency package bytes")
    inventory = {
        "contract": UI_OWNER_FEED_INVENTORY_CONTRACT,
        "dependencyAuthorityCacheKey": dependency_cache_key,
        "packageRecipeCommit": recipe_commit,
        "packageRecipeSha256": recipe_sha256,
        "producerLockSha256": producer_lock_sha256,
        "packages": [
            {
                "commit": row["commit"],
                "file_name": row["fileName"],
                "id": row["packageId"],
                "project": row["project"],
                "repository": row["repository"],
                "sha256": row["sha256"],
                "size_bytes": row["sizeBytes"],
                "source_tree": row["sourceTree"],
                "version": row["version"],
            }
            for row in rows
        ],
        "sdkVersion": EXPECTED_SDK_VERSION,
    }
    inventory_sha256 = hashlib.sha256(
        (json.dumps(inventory, indent=2, sort_keys=True) + "\n").encode("utf-8")
    ).hexdigest()
    receipt = {
        "contract": UI_OWNER_FEED_RECEIPT_CONTRACT,
        "dependencyAuthorityCacheKey": dependency_cache_key,
        "inventorySha256": inventory_sha256,
        "packageCount": len(rows),
        "packageRecipeCommit": recipe_commit,
        "packageRecipeSha256": recipe_sha256,
        "producerLockSha256": producer_lock_sha256,
        "packages": [
            {
                "commit": row["commit"],
                "fileName": row["fileName"],
                "packageId": row["packageId"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
                "version": row["version"],
            }
            for row in rows
        ],
        "publicationAuthorized": False,
        "sdkVersion": EXPECTED_SDK_VERSION,
        "status": "passed",
    }
    return inventory, receipt, rows


def expected_hub_inventory(lock: dict[str, Any]) -> dict[str, Any]:
    authority = lock["canonicalOwnerFeed"]
    return {
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


def expected_current_owner_contract_inventory(lock: dict[str, Any]) -> dict[str, Any]:
    authority = lock["currentOwnerContractFeed"]
    return {
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


def upstream_owner_package_cache_manifest(lock: dict[str, Any]) -> dict[str, Any]:
    core = lock["coreRuntimeFeed"]
    hub = lock["canonicalOwnerFeed"]
    legacy = lock["currentOwnerContractFeed"]
    authorities = {
        "coreRuntime": {
            "inventorySha256": core["inventorySha256"],
            "lockSha256": core["lockSha256"],
            "packageRecipeCommit": core["packageRecipeCommit"],
            "receiptSha256": core["receiptSha256"],
            "runtimeSourceCommit": core["runtimeSourceCommit"],
        },
        "hubCanonical": {
            "inventorySha256": hub["inventorySha256"],
            "lockSha256": hub["lockSha256"],
            "packageSourceCommits": sorted(
                {package["commit"] for package in hub["packages"]}
            ),
            "producerCommit": hub["producerCommit"],
            "receiptSha256": hub["receiptSha256"],
        },
        "legacyOwnerContracts": {
            "inventorySha256": legacy["inventorySha256"],
            "lockSha256": legacy["lockSha256"],
            "packageSourceCommits": sorted(
                {package["commit"] for package in legacy["packages"]}
            ),
            "producerCommit": legacy["producerCommit"],
            "producerSha256": legacy["producerSha256"],
        },
    }
    cache_key = hashlib.sha256(
        json.dumps(
            authorities,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    authority_artifacts = [
        {
            "fileName": "core-inventory.json",
            "sha256": core["inventorySha256"],
        },
        {"fileName": "core-lock.json", "sha256": core["lockSha256"]},
        {"fileName": "core-receipt.json", "sha256": core["receiptSha256"]},
        {
            "fileName": "hub-inventory.json",
            "sha256": hub["inventorySha256"],
        },
        {"fileName": "hub-lock.json", "sha256": hub["lockSha256"]},
        {"fileName": "hub-producer.py", "sha256": hub["producerSha256"]},
        {"fileName": "hub-receipt.json", "sha256": hub["receiptSha256"]},
        {
            "fileName": "legacy-inventory.json",
            "sha256": legacy["inventorySha256"],
        },
        {"fileName": "legacy-lock.json", "sha256": legacy["lockSha256"]},
        {
            "fileName": "legacy-producer.py",
            "sha256": legacy["producerSha256"],
        },
    ]
    packages: list[dict[str, Any]] = []
    for plane, rows in (
        ("core-runtime", core["packages"]),
        ("hub-canonical", hub["packages"]),
        ("legacy-owner-contracts", legacy["packages"]),
    ):
        packages.extend(
            {
                "commit": package["commit"],
                "fileName": package["fileName"],
                "packageId": package["packageId"],
                "plane": plane,
                "repository": package["repository"],
                "sha256": package["sha256"],
                "sizeBytes": package["sizeBytes"],
                "version": package["version"],
            }
            for package in rows
        )
    return {
        "authorities": authorities,
        "authorityArtifacts": authority_artifacts,
        "cacheKey": cache_key,
        "contract": OWNER_PACKAGE_CACHE_CONTRACT,
        "packages": packages,
    }


def expected_ui_owner_inventory(lock: dict[str, Any]) -> dict[str, Any]:
    authority = lock["uiOwnerFeed"]
    return {
        "contract": authority["inventoryContract"],
        "dependencyAuthorityCacheKey": authority["dependencyAuthorityCacheKey"],
        "packageRecipeCommit": authority["packageRecipeCommit"],
        "packageRecipeSha256": authority["packageRecipeSha256"],
        "producerLockSha256": authority["producerLockSha256"],
        "packages": [
            {
                "commit": package["commit"],
                "file_name": package["fileName"],
                "id": package["packageId"],
                "project": package["project"],
                "repository": package["repository"],
                "sha256": package["sha256"],
                "size_bytes": package["sizeBytes"],
                "source_tree": package["sourceTree"],
                "version": package["version"],
            }
            for package in authority["packages"]
        ],
        "sdkVersion": authority["sdkVersion"],
    }


def expected_ui_owner_receipt(lock: dict[str, Any]) -> dict[str, Any]:
    authority = lock["uiOwnerFeed"]
    return {
        "contract": authority["receiptContract"],
        "dependencyAuthorityCacheKey": authority["dependencyAuthorityCacheKey"],
        "inventorySha256": authority["inventorySha256"],
        "packageCount": len(authority["packages"]),
        "packageRecipeCommit": authority["packageRecipeCommit"],
        "packageRecipeSha256": authority["packageRecipeSha256"],
        "producerLockSha256": authority["producerLockSha256"],
        "packages": [
            {
                "commit": package["commit"],
                "fileName": package["fileName"],
                "packageId": package["packageId"],
                "sha256": package["sha256"],
                "sizeBytes": package["sizeBytes"],
                "version": package["version"],
            }
            for package in authority["packages"]
        ],
        "publicationAuthorized": False,
        "sdkVersion": authority["sdkVersion"],
        "status": "passed",
    }


def owner_package_cache_manifest(lock: dict[str, Any]) -> dict[str, Any]:
    manifest = upstream_owner_package_cache_manifest(lock)
    authority = lock.get("uiOwnerFeed")
    if not isinstance(authority, dict):
        return manifest
    authorities = dict(manifest["authorities"])
    authorities["uiOwner"] = {
        "dependencyAuthorityCacheKey": authority["dependencyAuthorityCacheKey"],
        "inventorySha256": authority["inventorySha256"],
        "packageRecipeCommit": authority["packageRecipeCommit"],
        "packageRecipeSha256": authority["packageRecipeSha256"],
        "producerLockSha256": authority["producerLockSha256"],
        "packageSourceCommits": sorted(
            {package["commit"] for package in authority["packages"]}
        ),
        "receiptSha256": authority["receiptSha256"],
    }
    authority_artifacts = [
        *manifest["authorityArtifacts"],
        {
            "fileName": authority["inventoryFileName"],
            "sha256": authority["inventorySha256"],
        },
        {
            "fileName": authority["receiptFileName"],
            "sha256": authority["receiptSha256"],
        },
    ]
    packages = [
        *manifest["packages"],
        *(
            {
                "commit": package["commit"],
                "fileName": package["fileName"],
                "packageId": package["packageId"],
                "plane": "ui-owner",
                "repository": package["repository"],
                "sha256": package["sha256"],
                "sizeBytes": package["sizeBytes"],
                "version": package["version"],
            }
            for package in authority["packages"]
        ),
    ]
    cache_key = hashlib.sha256(
        json.dumps(authorities, sort_keys=True, separators=(",", ":")).encode(
            "utf-8"
        )
    ).hexdigest()
    return {
        **manifest,
        "authorities": authorities,
        "authorityArtifacts": authority_artifacts,
        "cacheKey": cache_key,
        "packages": packages,
    }


def owner_feed_binding_receipts(lock: dict[str, Any]) -> dict[str, Any]:
    hub = lock["canonicalOwnerFeed"]
    core = lock["coreRuntimeFeed"]
    receipts = {
        "canonicalOwnerFeed": {
            "inventoryContract": hub["inventoryContract"],
            "inventorySha256": hub["inventorySha256"],
            "lockContract": hub["lockContract"],
            "lockSha256": hub["lockSha256"],
            "producerCommit": hub["producerCommit"],
            "producerRepository": hub["producerRepository"],
            "packageCount": len(hub["packages"]),
            "packages": [
                {
                    "fileName": row["fileName"],
                    "sha256": row["sha256"],
                    "sizeBytes": row["sizeBytes"],
                }
                for row in hub["packages"]
            ],
            "producerPath": hub["producerPath"],
            "producerSha256": hub["producerSha256"],
            "projectLockFilesEnforced": True,
            "receiptContract": hub["receiptContract"],
            "receiptSha256": hub["receiptSha256"],
            "status": "passed",
        },
        "coreRuntimeFeed": {
            "inventoryContract": core["inventoryContract"],
            "inventorySha256": core["inventorySha256"],
            "lockContract": core["lockContract"],
            "lockSha256": core["lockSha256"],
            "packageCount": len(core["packages"]),
            "packageRecipeCommit": core["packageRecipeCommit"],
            "packages": [
                {
                    "fileName": row["fileName"],
                    "sha256": row["sha256"],
                    "sizeBytes": row["sizeBytes"],
                }
                for row in core["packages"]
            ],
            "receiptContract": core["receiptContract"],
            "receiptSha256": core["receiptSha256"],
            "runtimeSourceCommit": core["runtimeSourceCommit"],
            "selectedForCanonicalFullFeed": True,
            "status": "passed",
        },
    }
    ui_owner = lock.get("uiOwnerFeed")
    if isinstance(ui_owner, dict):
        receipts["uiOwnerFeed"] = {
            "dependencyAuthorityCacheKey": ui_owner[
                "dependencyAuthorityCacheKey"
            ],
            "inventoryContract": ui_owner["inventoryContract"],
            "inventorySha256": ui_owner["inventorySha256"],
            "packageCount": len(ui_owner["packages"]),
            "packageRecipeCommit": ui_owner["packageRecipeCommit"],
            "packageRecipeSha256": ui_owner["packageRecipeSha256"],
            "producerLockSha256": ui_owner["producerLockSha256"],
            "packages": [
                {
                    "fileName": row["fileName"],
                    "sha256": row["sha256"],
                    "sizeBytes": row["sizeBytes"],
                }
                for row in ui_owner["packages"]
            ],
            "receiptContract": ui_owner["receiptContract"],
            "receiptSha256": ui_owner["receiptSha256"],
            "sdkVersion": ui_owner["sdkVersion"],
            "status": "passed",
        }
    return receipts


def import_owner_package_artifact_cache(
    lock: dict[str, Any],
    cache: Path,
    destination_feed: Path,
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    if not cache.is_absolute() or cache.is_symlink() or not cache.is_dir():
        raise VerificationError(
            "owner package cache must be an absolute non-symlink directory"
        )
    if cache.resolve(strict=True) != cache:
        raise VerificationError("owner package cache path must already be physical")
    require_owned_traversable_directory(cache, "owner package cache")
    if {entry.name for entry in cache.iterdir()} != {
        "authority",
        "owner-package-cache.json",
        "packages",
    }:
        raise VerificationError("owner package cache contains missing or extra entries")
    authority_root = cache / "authority"
    package_root = cache / "packages"
    require_owned_traversable_directory(authority_root, "owner cache authority")
    require_owned_traversable_directory(package_root, "owner cache packages")

    expected_manifest = owner_package_cache_manifest(lock)
    manifest_path = cache / "owner-package-cache.json"
    manifest_inventory = secure_regular_file_inventory(
        manifest_path,
        label="owner package cache manifest",
        receipt_path="owner-package-cache.json",
    )
    if load_json(manifest_path) != expected_manifest:
        raise VerificationError("owner package cache manifest authority differs")

    authority_by_name = {
        row["fileName"]: row for row in expected_manifest["authorityArtifacts"]
    }
    authority_entries = list(authority_root.iterdir())
    if {entry.name for entry in authority_entries} != set(authority_by_name):
        raise VerificationError(
            "owner package cache authority contains missing or extra entries"
        )
    authority_inventory: list[dict[str, Any]] = []
    for entry in authority_entries:
        row = secure_regular_file_inventory(
            entry,
            label="owner package cache authority artifact",
            receipt_path=f"authority/{entry.name}",
        )
        if row["sha256"] != authority_by_name[entry.name]["sha256"]:
            raise VerificationError(
                f"owner package cache authority artifact differs: {entry.name}"
            )
        authority_inventory.append(row)

    if load_json(authority_root / "hub-inventory.json") != expected_hub_inventory(lock):
        raise VerificationError("cached Hub inventory payload differs from authority")
    if load_json(
        authority_root / "legacy-inventory.json"
    ) != expected_current_owner_contract_inventory(lock):
        raise VerificationError("cached legacy inventory payload differs from authority")
    hub_receipt = load_json(authority_root / "hub-receipt.json")
    hub = lock["canonicalOwnerFeed"]
    if (
        hub_receipt.get("contract") != hub["receiptContract"]
        or hub_receipt.get("status") != "pass"
        or hub_receipt.get("hub_commit") != hub["producerCommit"]
        or hub_receipt.get("package_plane_lock_sha256") != hub["lockSha256"]
        or hub_receipt.get("package_inventory_sha256") != hub["inventorySha256"]
        or hub_receipt.get("package_version") != hub["packageVersion"]
    ):
        raise VerificationError("cached Hub sealed receipt authority differs")
    ui_owner = lock.get("uiOwnerFeed")
    if isinstance(ui_owner, dict):
        if load_json(
            authority_root / ui_owner["inventoryFileName"]
        ) != expected_ui_owner_inventory(lock):
            raise VerificationError(
                "cached UI-owner inventory payload differs from authority"
            )
        if load_json(
            authority_root / ui_owner["receiptFileName"]
        ) != expected_ui_owner_receipt(lock):
            raise VerificationError(
                "cached UI-owner receipt payload differs from authority"
            )

    packages_by_name = {
        row["fileName"]: row for row in expected_manifest["packages"]
    }
    package_entries = list(package_root.iterdir())
    if {entry.name for entry in package_entries} != set(packages_by_name):
        raise VerificationError(
            "owner package cache packages contain missing or extra entries"
        )
    package_inventory_rows: list[dict[str, Any]] = []
    for entry in package_entries:
        expected = packages_by_name[entry.name]
        row = secure_regular_file_inventory(
            entry,
            label="owner package cache package",
            receipt_path=f"packages/{entry.name}",
            validate_nuget=True,
        )
        if (
            row["sha256"] != expected["sha256"]
            or row["sizeBytes"] != expected["sizeBytes"]
        ):
            raise VerificationError(
                f"owner package cache package differs: {entry.name}"
            )
        package_inventory_rows.append(row)

    before = directory_asset_inventory(cache)
    for package in expected_manifest["packages"]:
        source = package_root / package["fileName"]
        target = destination_feed / package["fileName"]
        if target.exists() or target.is_symlink():
            raise VerificationError(
                f"cached owner package target already exists: {target.name}"
            )
        copy_regular_file_exact(source, target)
        if (
            target.stat().st_size != package["sizeBytes"]
            or sha256_file(target) != package["sha256"]
        ):
            target.unlink(missing_ok=True)
            raise VerificationError(f"cached owner package differs: {target.name}")
    if before != directory_asset_inventory(cache):
        raise VerificationError("owner package cache changed during import")

    receipts = owner_feed_binding_receipts(lock)
    current_receipt = current_owner_contract_feed_binding_receipt(lock)
    current_receipt.update(
        {
            "compatibilityPurpose": "exact-core-runtime-transitive-dependencies",
            "materializedFeedValidated": True,
            "selectedForCanonicalFullFeed": True,
            "status": "passed",
        }
    )
    cache_receipt = {
        "authorityArtifacts": sorted(
            authority_inventory, key=lambda row: row["path"]
        ),
        "cacheKey": expected_manifest["cacheKey"],
        "coldProducerFallbackOnCacheMiss": True,
        "contract": OWNER_PACKAGE_CACHE_CONTRACT,
        "importedByCopy": True,
        "manifest": manifest_inventory,
        "packageCount": len(package_inventory_rows),
        "packages": sorted(package_inventory_rows, key=lambda row: row["path"]),
        "sourcePath": str(cache),
        "status": "passed",
        "used": True,
    }
    return receipts, current_receipt, cache_receipt


def import_hub_canonical_feed(
    lock: dict[str, Any],
    hub_root: Path,
    sdk_root: Path,
    core_feed: Path,
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
    if core_feed.exists() or core_feed.is_symlink():
        raise VerificationError("Core runtime feed destination must start absent")

    command = [
        str(TRUSTED_PYTHON3),
        str(producer),
        "--repo-root",
        str(hub_root),
        "--lock",
        str(producer_lock),
        "--feed",
        str(canonical_feed),
        "--core-feed",
        str(core_feed),
        "--download-core-runtime",
        "--dotnet",
        str(sdk_root / "dotnet"),
    ]
    run(command, cwd=hub_root, environment=environment)
    run(
        [
            str(TRUSTED_PYTHON3),
            str(producer),
            "--repo-root",
            str(hub_root),
            "--lock",
            str(producer_lock),
            "--feed",
            str(canonical_feed),
            "--validate-only",
        ],
        cwd=hub_root,
        environment=environment,
    )

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

    expected_rows = [
        {
            "id": canonical["packageId"],
            "version": canonical["version"],
            "repository": canonical["repository"],
            "commit": canonical["commit"],
            "project": canonical["project"],
            "file_name": canonical["fileName"],
            "sha256": canonical["sha256"],
            "size_bytes": canonical["sizeBytes"],
        }
        for canonical in authority["packages"]
    ]
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

    core_authority = lock["coreRuntimeFeed"]
    core_entries = list(core_feed.iterdir())
    expected_core_names = {row["fileName"] for row in core_authority["packages"]}
    if {entry.name for entry in core_entries} != expected_core_names:
        raise VerificationError("Core runtime feed contains missing or unexpected entries")
    for entry in core_entries:
        metadata = entry.lstat()
        if entry.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError("Core runtime feed contains a link or special entry")

    for source_feed, package in [
        *((canonical_feed, row) for row in authority["packages"]),
        *((core_feed, row) for row in core_authority["packages"]),
    ]:
        source = source_feed / package["fileName"]
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

    return {
        "canonicalOwnerFeed": {
            "inventoryContract": authority["inventoryContract"],
            "inventorySha256": authority["inventorySha256"],
            "lockContract": authority["lockContract"],
            "lockSha256": authority["lockSha256"],
            "producerCommit": authority["producerCommit"],
            "producerRepository": authority["producerRepository"],
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
        },
        "coreRuntimeFeed": {
            "inventoryContract": core_authority["inventoryContract"],
            "inventorySha256": core_authority["inventorySha256"],
            "lockContract": core_authority["lockContract"],
            "lockSha256": core_authority["lockSha256"],
            "packageCount": len(core_authority["packages"]),
            "packageRecipeCommit": core_authority["packageRecipeCommit"],
            "packages": [
                {
                    "fileName": row["fileName"],
                    "sha256": row["sha256"],
                    "sizeBytes": row["sizeBytes"],
                }
                for row in core_authority["packages"]
            ],
            "receiptContract": core_authority["receiptContract"],
            "receiptSha256": core_authority["receiptSha256"],
            "runtimeSourceCommit": core_authority["runtimeSourceCommit"],
            "selectedForCanonicalFullFeed": True,
            "status": "passed",
        },
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
        "selectedForCoreRuntimeCompatibility": authority[
            "selectedForCoreRuntimeCompatibility"
        ],
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


def import_current_owner_contract_feed(
    lock: dict[str, Any],
    core_root: Path,
    sdk_root: Path,
    materialized_feed: Path,
    workspace: Path,
    package_root: Path,
    destination_feed: Path,
    environment: dict[str, str],
) -> dict[str, Any]:
    authority = lock["currentOwnerContractFeed"]
    producer = core_root / require_relative(
        authority["producerPath"], "current owner-contract feed producer"
    )
    producer_lock = core_root / require_relative(
        authority["lockPath"], "current owner-contract feed lock"
    )
    for path, expected_digest, label in (
        (producer, authority["producerSha256"], "producer"),
        (producer_lock, authority["lockSha256"], "lock"),
    ):
        try:
            metadata = path.lstat()
        except OSError as exc:
            raise VerificationError(
                f"current owner-contract feed {label} is unavailable"
            ) from exc
        if path.is_symlink() or not stat.S_ISREG(metadata.st_mode):
            raise VerificationError(
                f"current owner-contract feed {label} is not a regular file"
            )
        if sha256_file(path) != expected_digest:
            raise VerificationError(
                f"current owner-contract feed {label} differs from authority"
            )
    for path, label in (
        (materialized_feed, "feed"),
        (workspace, "workspace"),
        (package_root, "package root"),
    ):
        if path.exists() or path.is_symlink():
            raise VerificationError(
                f"current owner-contract {label} destination must start absent"
            )

    command = [
        str(TRUSTED_PYTHON3),
        str(producer),
        "--repo-root",
        str(core_root),
        "--lock",
        str(producer_lock),
        "--feed",
        str(materialized_feed),
        "--workspace",
        str(workspace),
        "--package-root",
        str(package_root),
        "--dotnet",
        str(sdk_root / "dotnet"),
    ]
    run(command, cwd=core_root, environment=environment)
    run(
        [
            str(TRUSTED_PYTHON3),
            str(producer),
            "--repo-root",
            str(core_root),
            "--lock",
            str(producer_lock),
            "--feed",
            str(materialized_feed),
            "--validate-only",
        ],
        cwd=core_root,
        environment=environment,
    )
    receipt = validate_materialized_current_owner_contract_feed(
        lock, materialized_feed
    )
    for package in authority["packages"]:
        source = materialized_feed / package["fileName"]
        target = destination_feed / package["fileName"]
        if target.exists() or target.is_symlink():
            raise VerificationError(
                f"current owner-contract package target already exists: {target.name}"
            )
        copy_regular_file_exact(source, target)
        if (
            target.stat().st_size != package["sizeBytes"]
            or sha256_file(target) != package["sha256"]
        ):
            target.unlink(missing_ok=True)
            raise VerificationError(
                f"current owner-contract package bytes differ: {target.name}"
            )
    receipt.update(
        {
            "compatibilityPurpose": "exact-core-runtime-transitive-dependencies",
            "selectedForCanonicalFullFeed": True,
            "status": "passed",
        }
    )
    return receipt


def exact_write_receipt(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute():
        raise VerificationError("receipt output must be a new absolute path")
    path.parent.mkdir(parents=True, exist_ok=True)
    parent_metadata = path.parent.lstat()
    if (
        path.parent.is_symlink()
        or path.parent.resolve(strict=True) != path.parent
        or not stat.S_ISDIR(parent_metadata.st_mode)
        or parent_metadata.st_uid != os.geteuid()
        or stat.S_IMODE(parent_metadata.st_mode) & 0o022
    ):
        raise VerificationError("receipt parent is not a trusted physical directory")
    try:
        path.lstat()
    except FileNotFoundError:
        pass
    else:
        raise VerificationError("receipt output must be a new absolute path")
    encoded = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    descriptor, staging_name = tempfile.mkstemp(
        prefix=".chummer-receipt-",
        dir=path.parent,
    )
    staging = Path(staging_name)
    renamed = False
    output_identity: tuple[int, int] | None = None
    try:
        with os.fdopen(descriptor, "wb") as stream:
            os.fchmod(stream.fileno(), 0o600)
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
            metadata = os.fstat(stream.fileno())
            output_identity = (metadata.st_dev, metadata.st_ino)
        atomic_rename_noreplace(staging, path)
        renamed = True
        fsync_directory(path.parent)
    except BaseException:
        if renamed and output_identity is not None:
            metadata = path.lstat()
            if (
                path.is_symlink()
                or not stat.S_ISREG(metadata.st_mode)
                or metadata.st_nlink != 1
                or (metadata.st_dev, metadata.st_ino) != output_identity
            ):
                raise VerificationError("failed receipt output could not be safely rolled back")
            path.unlink()
            fsync_directory(path.parent)
        raise
    finally:
        try:
            metadata = staging.lstat()
        except FileNotFoundError:
            pass
        else:
            if staging.is_symlink() or not stat.S_ISREG(metadata.st_mode):
                raise VerificationError("owned receipt staging changed during cleanup")
            staging.unlink()


def rollback_retained_bundle(target: Path, identity: tuple[int, int]) -> None:
    metadata = target.lstat()
    if (
        target.is_symlink()
        or not stat.S_ISDIR(metadata.st_mode)
        or (metadata.st_dev, metadata.st_ino) != tuple(identity)
    ):
        raise VerificationError("retained bundle cannot be safely rolled back")
    parent = target.parent
    rollback = Path(tempfile.mkdtemp(prefix=".chummer-win-rollback-", dir=parent))
    rollback.rmdir()
    try:
        atomic_rename_noreplace(target, rollback)
        fsync_directory(parent)
        rollback_metadata = rollback.lstat()
        if (
            rollback.is_symlink()
            or not stat.S_ISDIR(rollback_metadata.st_mode)
            or (rollback_metadata.st_dev, rollback_metadata.st_ino) != tuple(identity)
        ):
            raise VerificationError("retained bundle rollback identity changed")
        shutil.rmtree(rollback)
        fsync_directory(parent)
    finally:
        if rollback.exists() and not rollback.is_symlink():
            shutil.rmtree(rollback)


def cleanup_pending_verification_temporary(args: argparse.Namespace) -> None:
    temporary = getattr(args, "_verification_temporary_path", None)
    identity = getattr(args, "_verification_temporary_identity", None)
    if temporary is None or identity is None:
        return
    try:
        temporary.lstat()
    except FileNotFoundError:
        pass
    else:
        remove_owned_staging_tree(temporary, tuple(identity))
    args._verification_temporary_path = None
    args._verification_temporary_identity = None


def rollback_pending_verification(args: argparse.Namespace) -> None:
    owner_cache_identity = getattr(args, "_produced_owner_cache_identity", None)
    owner_cache_target = getattr(args, "produce_owner_package_cache_output", None)
    if owner_cache_identity is not None and owner_cache_target is not None:
        rollback_retained_bundle(owner_cache_target, tuple(owner_cache_identity))
        args._produced_owner_cache_identity = None
    identity = getattr(args, "_retained_bundle_identity", None)
    target = getattr(args, "retain_windows_bundle_output", None)
    if identity is not None and target is not None:
        rollback_retained_bundle(target, tuple(identity))
        args._retained_bundle_identity = None
    cleanup_pending_verification_temporary(args)


def commit_verification_receipt(args: argparse.Namespace, receipt: dict[str, Any]) -> None:
    try:
        exact_write_receipt(args.receipt_output, receipt)
    except BaseException as original_error:
        try:
            rollback_pending_verification(args)
        except BaseException as rollback_error:
            raise VerificationError(
                f"verification receipt failed and retained-bundle rollback was unsafe: {rollback_error}"
            ) from original_error
        raise
    args._retained_bundle_identity = None
    args._produced_owner_cache_identity = None
    cleanup_pending_verification_temporary(args)


def produce_owner_package_cache(args: argparse.Namespace) -> dict[str, Any]:
    if args.owner_package_cache is None:
        raise VerificationError(
            "targeted owner-package production requires the exact upstream cache"
        )
    output = args.produce_owner_package_cache_output
    if output is None:
        raise VerificationError("targeted owner-package output is missing")
    parent, parent_device = validate_retained_bundle_target(output)
    repo_root = args.repo_root.resolve()
    head, canonical_lock_path, _, lock_inventory = capture_consumer_authority(
        repo_root, args.lock
    )
    lock = load_json(canonical_lock_path)
    validate_lock(lock, allow_unsealed_ui_owner=True)
    upstream_lock = dict(lock)
    upstream_lock.pop("uiOwnerFeed", None)
    staging = Path(tempfile.mkdtemp(prefix=".ui-owner-cache-", dir=parent))
    staging_metadata = staging.lstat()
    staging_identity = (staging_metadata.st_dev, staging_metadata.st_ino)
    temporary = Path(tempfile.mkdtemp(prefix="chummer-ui-owner-producer-"))
    temporary_metadata = temporary.lstat()
    args._verification_temporary_path = temporary
    args._verification_temporary_identity = (
        temporary_metadata.st_dev,
        temporary_metadata.st_ino,
    )
    retained = False
    try:
        require_same_filesystem(parent_device, staging)
        feed = temporary / "feed"
        owners_root = temporary / "owners"
        caches = temporary / "caches"
        for path in (feed, owners_root, caches):
            path.mkdir(mode=0o700)
        import_owner_package_artifact_cache(
            upstream_lock,
            args.owner_package_cache,
            feed,
        )
        sdk_root = args.sdk_root
        if sdk_root is None:
            sdk_root, sdk_archive_sha512 = acquire_sdk(
                lock["sdkArchive"], temporary / "private-dotnet-sdk"
            )
        else:
            if (
                not sdk_root.is_absolute()
                or sdk_root.is_symlink()
                or not sdk_root.is_dir()
                or sdk_root.resolve(strict=True) != sdk_root
            ):
                raise VerificationError("targeted producer SDK root is not exact")
            sdk_archive_sha512 = lock["sdkArchive"]["sha512"]
        environment = isolated_child_environment(
            caches,
            os.environ.copy(),
            trusted_dotnet_root=sdk_root,
        )
        environment["DOTNET_ROOT"] = str(sdk_root)
        require_exact_sdk(
            temporary,
            environment,
            lock["sdkVersion"],
            "targeted UI-owner package producer",
        )
        owner_rows = [
            {
                "commit": EXPECTED_UI_OWNER_SOURCES[package_id]["commit"],
                "directory": EXPECTED_UI_OWNER_SOURCES[package_id]["ownerDirectory"],
                "repository": EXPECTED_UI_OWNER_SOURCES[package_id]["repository"],
            }
            for package_id in EXPECTED_UI_OWNER_SOURCES
        ]
        if len({row["directory"] for row in owner_rows}) != len(owner_rows):
            raise VerificationError("UI-owner package sources are not distinct")
        owner_roots = {
            row["directory"]: acquire_owner(row, owners_root, environment)
            for row in owner_rows
        }
        pack_config = temporary / "producer.NuGet.config"
        write_nuget_config(pack_config, feed)
        recipe_sha256 = source_digest(
            repo_root / "scripts/ai/verify_fresh_checkout_package_plane.py"
        )
        producer_lock = build_ui_owner_producer_lock(
            lock,
            recipe_commit=head,
            recipe_sha256=recipe_sha256,
        )
        producer_lock_bytes = encoded_json(producer_lock)
        producer_lock_sha256 = hashlib.sha256(producer_lock_bytes).hexdigest()
        checked_in_producer_lock = repo_root / UI_OWNER_PRODUCER_LOCK_PATH
        if checked_in_producer_lock.exists() or checked_in_producer_lock.is_symlink():
            if (
                checked_in_producer_lock.is_symlink()
                or secure_regular_file_bytes(
                    checked_in_producer_lock,
                    label="UI-owner producer lock",
                )
                != producer_lock_bytes
            ):
                raise VerificationError("UI-owner producer lock differs from recipe authority")
        inventory, producer_receipt, package_rows = produce_ui_owner_packages(
            lock,
            repo_root=repo_root,
            owner_roots=owner_roots,
            sdk_root=sdk_root,
            feed=feed,
            pack_config=pack_config,
            environment=environment,
            recipe_commit=head,
            producer_lock_sha256=producer_lock_sha256,
        )
        authority = ui_owner_feed_authority(
            inventory=inventory,
            receipt=producer_receipt,
            rows=package_rows,
        )
        locked_authority = lock.get("uiOwnerFeed")
        if locked_authority is not None and locked_authority != authority:
            raise VerificationError("UI-owner produced authority differs from lock")
        proposed_lock = dict(lock)
        proposed_lock["uiOwnerFeed"] = authority
        proposed_lock["packages"] = package_rows
        authority_root = staging / "authority"
        package_root = staging / "packages"
        copy_inventory_tree(
            args.owner_package_cache / "authority",
            authority_root,
            directory_asset_inventory(args.owner_package_cache / "authority"),
        )
        copy_inventory_tree(
            args.owner_package_cache / "packages",
            package_root,
            directory_asset_inventory(args.owner_package_cache / "packages"),
        )
        for row in package_rows:
            copy_regular_file_exact(
                feed / row["fileName"],
                package_root / row["fileName"],
            )
        exact_write_receipt(
            authority_root / authority["inventoryFileName"], inventory
        )
        exact_write_receipt(
            authority_root / authority["receiptFileName"], producer_receipt
        )
        cache_manifest = owner_package_cache_manifest(proposed_lock)
        exact_write_receipt(staging / "owner-package-cache.json", cache_manifest)
        if {path.name for path in staging.iterdir()} != {
            "authority",
            "owner-package-cache.json",
            "packages",
        }:
            raise VerificationError("produced owner-package cache shape is not exact")
        if len(list(package_root.iterdir())) != 18:
            raise VerificationError("produced owner-package cache is not exact 18 packages")
        final_inventory = directory_asset_inventory(staging)
        fsync_asset_tree(staging)
        require_same_filesystem(parent_device, staging)
        atomic_rename_noreplace(staging, output)
        retained = True
        fsync_directory(parent)
        output_metadata = output.lstat()
        if (
            output.is_symlink()
            or not stat.S_ISDIR(output_metadata.st_mode)
            or (output_metadata.st_dev, output_metadata.st_ino) != staging_identity
            or directory_asset_inventory(output) != final_inventory
        ):
            raise VerificationError("produced owner-package cache changed after retention")
        args._produced_owner_cache_identity = staging_identity
        return {
            "authority": False,
            "cacheKey": cache_manifest["cacheKey"],
            "contractName": "chummer6-ui.owner-package-cache-production/v1",
            "dependencyPackageCount": 16,
            "packageCount": 18,
            "packagePlaneLock": lock_inventory,
            "proposedPackages": package_rows,
            "proposedProducerLock": producer_lock,
            "proposedProducerLockSha256": producer_lock_sha256,
            "proposedUiOwnerFeed": authority,
            "publicationAuthorized": False,
            "sdkArchiveSha512": sdk_archive_sha512,
            "sdkVersion": lock["sdkVersion"],
            "status": "passed",
            "targetPath": str(output),
        }
    except BaseException:
        if retained:
            rollback_retained_bundle(output, staging_identity)
        raise
    finally:
        try:
            staging.lstat()
        except FileNotFoundError:
            pass
        else:
            remove_owned_staging_tree(staging, staging_identity)


def verify(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = args.repo_root.resolve()
    (
        head,
        canonical_lock_path,
        captured_lock_bytes,
        captured_lock_inventory,
    ) = capture_consumer_authority(repo_root, args.lock)
    retained_windows_bundle_target = args.retain_windows_bundle_output
    if retained_windows_bundle_target is not None:
        validate_retained_bundle_target(retained_windows_bundle_target)
        try:
            retained_windows_bundle_target.relative_to(repo_root)
        except ValueError:
            pass
        else:
            raise VerificationError("retained bundle output must be outside the consumer checkout")
    lock = load_json(canonical_lock_path)
    validate_lock(lock)
    validate_test_compile_items(repo_root)
    source_rows = verify_source_files(repo_root, lock["consumer"]["sourceFiles"])

    with tempfile.TemporaryDirectory(prefix="chummer-ui-fresh-package-plane-") as temporary_name:
        temporary = Path(temporary_name)
        temporary_metadata = temporary.lstat()
        args._verification_temporary_path = temporary
        args._verification_temporary_identity = (
            temporary_metadata.st_dev,
            temporary_metadata.st_ino,
        )
        owners_root = temporary / "owners"
        feed = temporary / "feed"
        core_runtime_feed = temporary / "core-runtime-feed"
        current_owner_contract_feed = temporary / "current-owner-contract-feed"
        current_owner_contract_workspace = temporary / "current-owner-contract-sources"
        current_owner_contract_package_root = temporary / "current-owner-contract-packages"
        hub_canonical_feed = temporary / "hub-canonical-feed"
        caches = temporary / "caches"
        consumer_parent = temporary / "consumer-only"
        for path in (owners_root, feed, caches, consumer_parent):
            path.mkdir(mode=0o700)
        cached_feed_receipts: tuple[
            dict[str, Any], dict[str, Any], dict[str, Any]
        ] | None = None
        if args.owner_package_cache is not None:
            cached_feed_receipts = import_owner_package_artifact_cache(
                lock,
                args.owner_package_cache,
                feed,
            )
        sdk_root, sdk_archive_sha512 = acquire_sdk(
            lock["sdkArchive"], temporary / "private-dotnet-sdk"
        )
        sdk_parent = os.environ.copy()
        environment = isolated_child_environment(
            caches,
            sdk_parent,
            trusted_dotnet_root=sdk_root,
        )
        environment["DOTNET_ROOT"] = str(sdk_root)
        require_exact_sdk(temporary, environment, lock["sdkVersion"], "private composition")
        for external_package in lock["externalPackages"]:
            acquire_external_package(external_package, feed)
        pack_config = temporary / "pack.NuGet.config"
        write_nuget_config(pack_config, feed)
        canonical_authority = lock["canonicalOwnerFeed"]
        canonical_producer_owner = {
            "commit": canonical_authority["producerCommit"],
            "directory": canonical_authority["producerDirectory"],
            "repository": canonical_authority["producerRepository"],
        }
        owner_rows = [*lock["owners"], canonical_producer_owner]
        owner_roots = {
            owner["directory"]: acquire_owner(owner, owners_root, environment)
            for owner in owner_rows
        }
        owner_sdk_versions: dict[str, str] = {}
        for owner in owner_rows:
            owner_sdk_versions[owner["directory"]] = require_exact_sdk(
                owner_roots[owner["directory"]],
                environment,
                lock["sdkVersion"],
                f"{owner['directory']} owner",
            )
        if cached_feed_receipts is not None:
            (
                canonical_feed_receipts,
                current_owner_contract_feed_receipt,
                owner_package_cache_receipt,
            ) = cached_feed_receipts
        else:
            canonical_feed_receipts = import_hub_canonical_feed(
                lock,
                owner_roots[canonical_authority["producerDirectory"]],
                sdk_root,
                core_runtime_feed,
                hub_canonical_feed,
                feed,
                environment,
            )
            current_owner_contract_feed_receipt = import_current_owner_contract_feed(
                lock,
                owner_roots["chummer-core-engine"],
                sdk_root,
                current_owner_contract_feed,
                current_owner_contract_workspace,
                current_owner_contract_package_root,
                feed,
                environment,
            )
            owner_package_cache_receipt = {
                "coldProducerFallbackOnCacheMiss": True,
                "contract": OWNER_PACKAGE_CACHE_CONTRACT,
                "status": "not_supplied",
                "used": False,
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
                    "-p:ChummerUseLocalCompatibilityTree=false",
                    "-p:ChummerLocalContractsProject=",
                    f"-p:ChummerContractsPackageVersion={CANONICAL_ENGINE_CONTRACTS_VERSION}",
                    f"-p:ChummerEngineContractsPackageVersion={CANONICAL_ENGINE_CONTRACTS_VERSION}",
                    "-p:ChummerCampaignContractsPackageVersion=0.1.0-preview",
                    f"-p:ChummerCoreRuntimePackageVersion={CORE_RUNTIME_PACKAGE_VERSION}",
                    f"-p:ChummerHubRegistryContractsPackageVersion={CANONICAL_HUB_CONTRACTS_VERSION}",
                    f"-p:ChummerRunContractsPackageVersion={CANONICAL_HUB_CONTRACTS_VERSION}",
                    f"-p:ChummerRunRegistryPackageVersion={CANONICAL_HUB_CONTRACTS_VERSION}",
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
                        == canonical_authority["producerDirectory"]
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
            for row in [
                *lock["externalPackages"],
                *lock["currentOwnerContractFeed"]["packages"],
                *lock["coreRuntimeFeed"]["packages"],
                *canonical_authority["packages"],
                *lock["packages"],
            ]
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
        locked_package_sha256.update(
            {
                row["fileName"]: row["sha256"]
                for row in lock["currentOwnerContractFeed"]["packages"]
            }
        )
        locked_package_sha256.update(
            {
                row["fileName"]: row["sha256"]
                for row in lock["coreRuntimeFeed"]["packages"]
            }
        )
        before = package_inventory(feed, expected_names, locked_package_sha256)
        consumer = consumer_parent / "ui"
        cloned_lock_inventory = clone_exact_consumer(
            repo_root,
            consumer,
            consumer_parent,
            environment,
            head,
            captured_lock_bytes,
        )
        if cloned_lock_inventory != captured_lock_inventory:
            raise VerificationError("cloned consumer lock inventory differs from captured authority")
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
                    str(TRUSTED_BASH),
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
        test_executions: list[dict[str, Any]] = []
        for test_project in lock["consumer"]["testProjects"]:
            test_executions.append(
                {
                    "buildInParallel": False,
                    "disableBuildServers": True,
                    "maxCpuCount": 1,
                    "project": test_project,
                    "sdkVersion": require_exact_sdk(
                        consumer,
                        environment,
                        lock["sdkVersion"],
                        f"consumer test {test_project}",
                    ),
                    "useSharedCompilation": False,
                }
            )
            run(
                [
                    str(TRUSTED_BASH),
                    "scripts/ai/with-package-plane.sh",
                    "test",
                    test_project,
                    "-c",
                    "Release",
                    "-m:1",
                    "-p:BuildInParallel=false",
                    "-p:UseSharedCompilation=false",
                    "-p:WarningsAsErrors=NU1603%3BNU1608",
                    "--disable-build-servers",
                    "--minimum-expected-tests",
                    "1",
                    "--no-progress",
                ],
                cwd=consumer,
                environment=environment,
            )
        focused_test_assembly_path = consumer / PRODUCT_TEST_ASSEMBLY
        focused_test_assembly = secure_regular_file_inventory(
            focused_test_assembly_path,
            label="full-suite product test assembly",
            receipt_path=PRODUCT_TEST_ASSEMBLY,
        )

        def require_focused_test_inputs_unchanged(label: str) -> None:
            require_inventory_unchanged(
                before,
                package_inventory(feed, expected_names, locked_package_sha256),
            )
            require_exact_nuget_config_source(consumer_config, feed)
            require_clean_consumer_head(consumer, environment, head)
            current_assembly = secure_regular_file_inventory(
                focused_test_assembly_path,
                label=label,
                receipt_path=PRODUCT_TEST_ASSEMBLY,
            )
            if current_assembly != focused_test_assembly:
                raise VerificationError(
                    "focused tests did not reuse the exact full-suite assembly"
                )

        focused_career_advance_execution = {
            "filter": FOCUSED_CAREER_ADVANCE_TEST_FILTER,
            "minimumExpectedTests": FOCUSED_CAREER_ADVANCE_MINIMUM_TESTS,
            "project": FOCUSED_CAREER_ADVANCE_TEST_PROJECT,
            "reuseFullSuiteBuild": True,
            "runner": "direct-exact-assembly",
            "sdkVersion": require_exact_sdk(
                consumer,
                environment,
                lock["sdkVersion"],
                "focused career advancement parity tests",
            ),
            "sourceFiles": FOCUSED_CAREER_ADVANCE_TEST_FILES.split("|"),
            "testAssembly": focused_test_assembly,
        }
        require_focused_test_inputs_unchanged("focused career test assembly")
        run(
            [
                str(sdk_root / "dotnet"),
                str(focused_test_assembly_path),
                "--filter",
                FOCUSED_CAREER_ADVANCE_TEST_FILTER,
                "--minimum-expected-tests",
                str(FOCUSED_CAREER_ADVANCE_MINIMUM_TESTS),
                "--no-progress",
            ],
            cwd=focused_test_assembly_path.parent,
            environment=environment,
        )
        require_focused_test_inputs_unchanged("focused career test assembly")
        focused_overview_execution = {
            "filter": FOCUSED_OVERVIEW_TEST_FILTER,
            "minimumExpectedTests": FOCUSED_OVERVIEW_MINIMUM_TESTS,
            "project": FOCUSED_OVERVIEW_TEST_PROJECT,
            "reuseFullSuiteBuild": True,
            "runner": "direct-exact-assembly",
            "sdkVersion": require_exact_sdk(
                consumer,
                environment,
                lock["sdkVersion"],
                "focused overview and creation activation regression tests",
            ),
            "sourceFiles": [FOCUSED_OVERVIEW_TEST_FILE],
            "testAssembly": focused_test_assembly,
        }
        require_focused_test_inputs_unchanged("focused overview test assembly")
        run(
            [
                str(sdk_root / "dotnet"),
                str(focused_test_assembly_path),
                "--filter",
                FOCUSED_OVERVIEW_TEST_FILTER,
                "--minimum-expected-tests",
                str(FOCUSED_OVERVIEW_MINIMUM_TESTS),
                "--no-progress",
            ],
            cwd=focused_test_assembly_path.parent,
            environment=environment,
        )
        require_focused_test_inputs_unchanged("focused overview test assembly")
        after = package_inventory(feed, expected_names, locked_package_sha256)
        require_inventory_unchanged(before, after)
        require_clean_consumer_head(consumer, environment, head)
        receipt = {
            "buildProjects": lock["consumer"]["buildProjects"],
            "buildExecutions": build_executions,
            "canonicalOwnerFeed": canonical_feed_receipts["canonicalOwnerFeed"],
            "childExecutableAuthority": {
                "bash": str(TRUSTED_BASH),
                "git": str(TRUSTED_GIT),
                "path": environment["PATH"],
                "python3": str(TRUSTED_PYTHON3),
            },
            "consumerCommit": head,
            "consumerPackagePlaneLock": cloned_lock_inventory,
            "contractName": RECEIPT_CONTRACT,
            "contractVersion": 10,
            "generatedAt": datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "localCompatibilityTree": False,
            "mode": "integration",
            "currentOwnerContractFeed": current_owner_contract_feed_receipt,
            "coreRuntimeFeed": canonical_feed_receipts["coreRuntimeFeed"],
            "ownerPackageArtifactCache": owner_package_cache_receipt,
            "creationInitialAuthorityTimingContract": {
                "budgetSeconds": CREATION_INITIAL_AUTHORITY_BUDGET_SECONDS,
                "measurementClaimed": False,
                "requiresHostedWallClockMeasurement": True,
                "structuralRegression": (
                    "Initial_creation_activation_attempt_bypasses_workspace_and_domain_reload_path"
                ),
            },
            "focusedCareerAdvanceTestExecution": focused_career_advance_execution,
            "focusedOverviewTestExecution": focused_overview_execution,
            "ownerSources": [
                {
                    "commit": owner["commit"],
                    "directory": owner["directory"],
                    "repository": owner["repository"],
                    "sdkVersion": owner_sdk_versions[owner["directory"]],
                }
                for owner in owner_rows
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
        if retained_windows_bundle_target is not None:
            retained_windows_bundle_receipt = publish_and_retain_windows_bundle(
                retained_windows_bundle_target,
                consumer=consumer,
                consumer_commit=head,
                consumer_config=consumer_config,
                consumer_lock_inventory=cloned_lock_inventory,
                environment=environment,
                expected_feed_inventory=before,
                expected_names=expected_names,
                feed=feed,
                locked_package_sha256=locked_package_sha256,
                release_version=args.windows_release_version,
                release_channel=args.windows_release_channel,
            )
            args._retained_bundle_identity = retained_windows_bundle_receipt.pop(
                "_retainedBundleIdentity"
            )
            receipt["retainedWindowsBundle"] = retained_windows_bundle_receipt
        return receipt


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    repo_root = Path(__file__).resolve().parents[2]
    parser.add_argument("--repo-root", type=Path, default=repo_root)
    parser.add_argument("--lock", type=Path, default=repo_root / "config" / "package-plane.lock.json")
    parser.add_argument("--current-owner-contract-feed", type=Path)
    parser.add_argument("--owner-package-cache", type=Path)
    parser.add_argument("--produce-owner-package-cache-output", type=Path)
    parser.add_argument("--sdk-root", type=Path)
    parser.add_argument(
        "--retain-windows-bundle-output",
        "--retained-bundle-output",
        dest="retain_windows_bundle_output",
        type=Path,
    )
    parser.add_argument("--windows-release-version")
    parser.add_argument("--windows-release-channel")
    parser.add_argument("--receipt-output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if not args.receipt_output.is_absolute():
            raise VerificationError("receipt output must be an absolute path")
        try:
            args.receipt_output.lstat()
        except FileNotFoundError:
            pass
        except OSError as exc:
            raise VerificationError("receipt output could not be inspected") from exc
        else:
            raise VerificationError("receipt output must be absent")
        if (
            args.current_owner_contract_feed is not None
            and args.retain_windows_bundle_output is not None
        ):
            raise VerificationError(
                "retained Windows bundle output requires the full strict verification transaction"
            )
        if (
            args.current_owner_contract_feed is not None
            and args.owner_package_cache is not None
        ):
            raise VerificationError(
                "current owner-contract validation cannot import an owner package cache"
            )
        if getattr(args, "produce_owner_package_cache_output", None) is not None:
            if args.current_owner_contract_feed is not None:
                raise VerificationError(
                    "targeted owner-package production cannot validate a legacy feed"
                )
            if args.retain_windows_bundle_output is not None:
                raise VerificationError(
                    "targeted owner-package production cannot retain a Windows bundle"
                )
        release_authority_supplied = (
            args.windows_release_version is not None
            or args.windows_release_channel is not None
        )
        if args.retain_windows_bundle_output is None and release_authority_supplied:
            raise VerificationError(
                "Windows release authority requires a retained Windows bundle output"
            )
        if args.retain_windows_bundle_output is not None:
            (
                args.windows_release_version,
                args.windows_release_channel,
            ) = require_windows_release_authority(
                args.windows_release_version,
                args.windows_release_channel,
            )
            try:
                args.receipt_output.relative_to(args.retain_windows_bundle_output)
            except ValueError:
                pass
            else:
                raise VerificationError(
                    "receipt output must be outside the retained Windows bundle"
                )
        if getattr(args, "produce_owner_package_cache_output", None) is not None:
            receipt = produce_owner_package_cache(args)
        elif args.current_owner_contract_feed is not None:
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
        commit_verification_receipt(args, receipt)
    except BaseException as exc:
        try:
            rollback_pending_verification(args)
        except BaseException as rollback_error:
            print(
                "fresh-package-plane:error: verification failed and cleanup was unsafe: "
                f"{rollback_error}",
                file=sys.stderr,
            )
            return 2
        if isinstance(exc, (VerificationError, OSError, subprocess.SubprocessError)):
            print(f"fresh-package-plane:error: {exc}", file=sys.stderr)
            return 2
        raise
    print(f"fresh-package-plane:receipt={args.receipt_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
