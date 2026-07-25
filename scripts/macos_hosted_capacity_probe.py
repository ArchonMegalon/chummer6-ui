#!/usr/bin/env python3
"""Secretless capacity and native-toolchain probe for GitHub-hosted macOS.

This probe is intentionally separate from signing, notarization submission,
release assembly, and publication.  Its only destructive operation is a
bounded removal of inactive, version-named Xcode bundles on an ephemeral
GitHub-hosted runner when less than 20 GiB is available.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import platform
import re
import secrets
import shlex
import shutil
import stat
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Callable, Mapping, Sequence


CONTRACT_NAME = "chummer6-ui.macos-hosted-capacity-probe.v1"
CONTRACT_VERSION = 1
CANONICAL_REPOSITORY = "ArchonMegalon/chummer6-ui"
APPLICATIONS_ROOT = Path("/Applications")
GIB = 1024**3
MINIMUM_FREE_BYTES = 20 * GIB
MAX_XCODE_DELETE_COUNT = 8
MAX_XCODE_DELETE_BYTES = 160 * GIB
ALLOWED_RUNNER_IMAGES = {
    "macos-15": "macos15",
    "macos-26": "macos26",
}
XCODE_BUNDLE_RE = re.compile(
    r"Xcode_(?P<version>[0-9]+(?:\.[0-9]+){1,2})\.app"
)
ALLOWED_EVENTS = {"pull_request", "workflow_dispatch"}
REQUIRED_COMMANDS = (
    "codesign",
    "curl",
    "ditto",
    "dotnet",
    "git",
    "hdiutil",
    "jq",
    "lipo",
    "node",
    "openssl",
    "python3",
    "security",
    "spctl",
    "sudo",
    "xattr",
    "xcode-select",
    "xcrun",
)
PROHIBITED_AUTHORITY_ENVIRONMENT = (
    "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64",
    "CHUMMER_MACOS_DEVELOPER_ID_P12_PASSWORD",
    "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64",
    "CHUMMER_MACOS_NOTARY_KEY_ID",
    "CHUMMER_MACOS_NOTARY_ISSUER_ID",
    "CHUMMER_MACOS_DEVELOPER_ID_APPLICATION",
    "CHUMMER_MACOS_TEAM_ID",
    "CHUMMER_MACOS_CERT_SHA256",
    "CHUMMER_MACOS_CERT_SPKI_SHA256",
    "CHUMMER_MACOS_ESCROW_RECIPIENT_PUBLIC_KEY_PEM_BASE64",
    "CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256",
    "CHUMMER_REGISTRY_WRITE_TOKEN",
    "CHUMMER_RELEASE_UPLOAD_TOKEN",
    "CHUMMER_RELEASE_TICKET",
    "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
    "ACTIONS_ID_TOKEN_REQUEST_URL",
)


class ProbeFailure(RuntimeError):
    """Expected fail-closed result with a receipt-safe failure code."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code


def utc_now() -> str:
    return (
        dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def fail(code: str, message: str) -> None:
    raise ProbeFailure(code, message)


def run_checked(
    arguments: Sequence[str],
    *,
    label: str,
    accepted_returncodes: tuple[int, ...] = (0,),
) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(
        tuple(arguments),
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode not in accepted_returncodes:
        fail(
            "command-failed",
            f"{label} failed with exit code {completed.returncode}",
        )
    return completed


def safe_single_line(value: str, *, label: str) -> str:
    normalized = " ".join(value.split())
    if not normalized or len(normalized) > 512:
        fail("invalid-tool-output", f"{label} returned invalid output")
    if any(ord(character) < 32 for character in normalized):
        fail("invalid-tool-output", f"{label} returned control characters")
    return normalized


def require_command(name: str) -> str:
    resolved = shutil.which(name)
    if resolved is None or not Path(resolved).is_absolute():
        fail("missing-command", f"required command is unavailable: {name}")
    return resolved


def validate_hosted_context(
    environment: Mapping[str, str],
    *,
    system_name: str,
    machine_name: str,
) -> dict[str, str]:
    expected = {
        "CI": "true",
        "GITHUB_ACTIONS": "true",
        "RUNNER_ARCH": "ARM64",
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
        "GITHUB_REPOSITORY": CANONICAL_REPOSITORY,
    }
    for name, value in expected.items():
        if environment.get(name) != value:
            fail(
                "untrusted-runner-context",
                f"{name} does not identify the governed GitHub-hosted runner",
            )
    if system_name != "Darwin" or machine_name != "arm64":
        fail(
            "wrong-native-platform",
            "probe requires a native Darwin arm64 runner",
        )

    runner_label = environment.get(
        "CHUMMER_MACOS_HOSTED_PROBE_RUNNER_IMAGE", ""
    )
    if runner_label not in ALLOWED_RUNNER_IMAGES:
        fail("unsupported-runner-image", "runner image is not pinned")
    if environment.get("ImageOS") != ALLOWED_RUNNER_IMAGES[runner_label]:
        fail(
            "runner-image-mismatch",
            "ImageOS does not match the pinned runner label",
        )
    image_version = environment.get("ImageVersion", "")
    if (
        not image_version
        or len(image_version) > 128
        or re.fullmatch(r"[0-9A-Za-z._-]+", image_version) is None
    ):
        fail("invalid-image-version", "ImageVersion is absent or invalid")
    event_name = environment.get("GITHUB_EVENT_NAME", "")
    if event_name not in ALLOWED_EVENTS:
        fail("unsupported-event", "probe event is not pull_request or dispatch")

    runner_temp = Path(environment.get("RUNNER_TEMP", ""))
    workspace = Path(environment.get("GITHUB_WORKSPACE", ""))
    for label, path in (
        ("RUNNER_TEMP", runner_temp),
        ("GITHUB_WORKSPACE", workspace),
    ):
        if (
            not path.is_absolute()
            or not path.is_dir()
            or path.is_symlink()
        ):
            fail(
                "unsafe-runner-path",
                f"{label} is not an absolute real directory",
            )
    return {
        "eventName": event_name,
        "imageOS": environment["ImageOS"],
        "imageVersion": image_version,
        "runnerImage": runner_label,
        "runnerTemp": str(runner_temp),
        "workspace": str(workspace),
    }


def assert_secretless_environment(environment: Mapping[str, str]) -> None:
    present = [
        name
        for name in PROHIBITED_AUTHORITY_ENVIRONMENT
        if environment.get(name)
    ]
    if present:
        fail(
            "release-authority-present",
            "release, signing, notarization, escrow, or OIDC authority "
            "was supplied to the secretless probe",
        )


def parse_xcode_version(name: str) -> tuple[int, ...]:
    match = XCODE_BUNDLE_RE.fullmatch(name)
    if match is None:
        fail(
            "unsafe-xcode-path",
            "Xcode cleanup target does not have a strict versioned name",
        )
    return tuple(int(part) for part in match.group("version").split("."))


def validate_xcode_candidate(
    candidate: Path,
    applications_root: Path = APPLICATIONS_ROOT,
) -> tuple[Path, tuple[int, ...]]:
    if not candidate.is_absolute():
        fail("unsafe-xcode-path", "Xcode cleanup target is not absolute")
    if candidate.parent != applications_root:
        fail(
            "unsafe-xcode-path",
            "Xcode cleanup target is outside the direct Applications root",
        )
    version = parse_xcode_version(candidate.name)
    try:
        metadata = candidate.lstat()
    except FileNotFoundError:
        fail("unsafe-xcode-path", "Xcode cleanup target disappeared")
    if stat.S_ISLNK(metadata.st_mode):
        fail("unsafe-xcode-path", "Xcode cleanup target is a symlink")
    if not stat.S_ISDIR(metadata.st_mode):
        fail("unsafe-xcode-path", "Xcode cleanup target is not a directory")
    try:
        resolved_candidate = candidate.resolve(strict=True)
        resolved_root = applications_root.resolve(strict=True)
    except FileNotFoundError:
        fail("unsafe-xcode-path", "Xcode cleanup path cannot be resolved")
    if (
        resolved_candidate.parent != resolved_root
        or resolved_candidate.name != candidate.name
    ):
        fail(
            "unsafe-xcode-path",
            "Xcode cleanup target resolves outside the Applications root",
        )
    return resolved_candidate, version


def resolve_active_xcode(
    selected_developer: Path,
    applications_root: Path = APPLICATIONS_ROOT,
) -> dict[str, Path]:
    if (
        not selected_developer.is_absolute()
        or selected_developer.name != "Developer"
        or selected_developer.parent.name != "Contents"
        or not selected_developer.is_dir()
    ):
        fail(
            "unsafe-active-xcode",
            "xcode-select did not return an Xcode Developer directory",
        )
    logical_bundle = selected_developer.parent.parent
    if logical_bundle.parent != applications_root:
        fail(
            "unsafe-active-xcode",
            "selected Xcode bundle is outside the Applications root",
        )
    if (
        logical_bundle.name != "Xcode.app"
        and XCODE_BUNDLE_RE.fullmatch(logical_bundle.name) is None
    ):
        fail(
            "unsafe-active-xcode",
            "selected Xcode bundle name is not governed",
        )
    try:
        physical_developer = selected_developer.resolve(strict=True)
        resolved_root = applications_root.resolve(strict=True)
    except FileNotFoundError:
        fail("unsafe-active-xcode", "selected Xcode cannot be resolved")
    if (
        physical_developer.name != "Developer"
        or physical_developer.parent.name != "Contents"
    ):
        fail(
            "unsafe-active-xcode",
            "resolved selected Xcode has an unexpected structure",
        )
    physical_bundle = physical_developer.parent.parent
    if physical_bundle.parent != resolved_root:
        fail(
            "unsafe-active-xcode",
            "resolved selected Xcode is outside the Applications root",
        )
    if (
        physical_bundle.name != "Xcode.app"
        and XCODE_BUNDLE_RE.fullmatch(physical_bundle.name) is None
    ):
        fail(
            "unsafe-active-xcode",
            "resolved selected Xcode bundle name is not governed",
        )
    if physical_bundle.is_symlink() or not physical_bundle.is_dir():
        fail(
            "unsafe-active-xcode",
            "resolved selected Xcode bundle is not a real directory",
        )
    return {
        "logicalBundle": logical_bundle,
        "physicalBundle": physical_bundle,
        "selectedDeveloper": selected_developer,
        "physicalDeveloper": physical_developer,
    }


def directory_size_bytes(path: Path) -> int:
    completed = run_checked(
        ("/usr/bin/du", "-sk", str(path)),
        label="Xcode bundle size inspection",
    )
    fields = completed.stdout.split()
    if not fields or not fields[0].isdigit():
        fail("invalid-xcode-size", "Xcode bundle size was not numeric")
    return int(fields[0]) * 1024


def build_xcode_cleanup_plan(
    applications_root: Path,
    selected_developer: Path,
    *,
    size_provider: Callable[[Path], int] = directory_size_bytes,
) -> dict[str, object]:
    if (
        not applications_root.is_absolute()
        or not applications_root.is_dir()
        or applications_root.is_symlink()
    ):
        fail(
            "unsafe-applications-root",
            "Applications root is not an absolute real directory",
        )
    active = resolve_active_xcode(selected_developer, applications_root)
    active_physical = active["physicalBundle"]
    candidates: list[dict[str, object]] = []
    ignored_symlinks: list[dict[str, object]] = []
    for candidate in sorted(
        (
            path
            for path in applications_root.iterdir()
            if path.name.startswith("Xcode_")
            and path.name.endswith(".app")
        ),
        key=lambda path: path.name,
    ):
        version = parse_xcode_version(candidate.name)
        try:
            metadata = candidate.lstat()
        except FileNotFoundError:
            fail("unsafe-xcode-path", "Xcode cleanup target disappeared")
        if stat.S_ISLNK(metadata.st_mode):
            ignored_symlinks.append(
                {
                    "path": str(candidate),
                    "reason": "version-alias-symlink-not-deleteable",
                    "version": list(version),
                }
            )
            continue
        resolved, version = validate_xcode_candidate(
            candidate, applications_root
        )
        size_bytes = size_provider(candidate)
        if size_bytes <= 0:
            fail("invalid-xcode-size", "Xcode bundle size was not positive")
        candidates.append(
            {
                "active": resolved == active_physical,
                "path": str(candidate),
                "resolvedPath": str(resolved),
                "sizeBytes": size_bytes,
                "version": list(version),
            }
        )
    candidates.sort(key=lambda item: tuple(item["version"]))
    inactive = [candidate for candidate in candidates if not candidate["active"]]
    inactive_bytes = sum(int(candidate["sizeBytes"]) for candidate in inactive)
    if len(inactive) > MAX_XCODE_DELETE_COUNT:
        fail(
            "xcode-cleanup-bound-exceeded",
            "inactive Xcode bundle count exceeds the deletion bound",
        )
    if inactive_bytes > MAX_XCODE_DELETE_BYTES:
        fail(
            "xcode-cleanup-bound-exceeded",
            "inactive Xcode bytes exceed the deletion bound",
        )
    return {
        "active": active,
        "candidates": candidates,
        "ignoredSymlinks": ignored_symlinks,
        "inactive": inactive,
        "inactiveBytes": inactive_bytes,
    }


def available_bytes(path: Path) -> int:
    return shutil.disk_usage(path).free


def require_capacity(
    free_bytes: int,
    minimum_free_bytes: int = MINIMUM_FREE_BYTES,
) -> None:
    if free_bytes < minimum_free_bytes:
        fail(
            "insufficient-capacity",
            "runner has less than the required 20 GiB after bounded cleanup",
        )


def revalidate_active_xcode(
    expected_physical_bundle: Path,
) -> dict[str, Path]:
    selected = run_checked(
        ("xcode-select", "-p"),
        label="xcode-select revalidation",
    ).stdout.strip()
    active = resolve_active_xcode(Path(selected), APPLICATIONS_ROOT)
    if active["physicalBundle"] != expected_physical_bundle:
        fail(
            "active-xcode-drift",
            "active Xcode changed during capacity cleanup",
        )
    run_checked(
        ("xcrun", "--find", "notarytool"),
        label="notarytool revalidation",
    )
    run_checked(
        ("xcrun", "--find", "stapler"),
        label="stapler revalidation",
    )
    return active


def perform_bounded_cleanup(
    runner_temp: Path,
    receipt: dict[str, object],
) -> None:
    selected_developer = Path(
        run_checked(
            ("xcode-select", "-p"),
            label="xcode-select inspection",
        ).stdout.strip()
    )
    plan = build_xcode_cleanup_plan(
        APPLICATIONS_ROOT,
        selected_developer,
    )
    active = plan["active"]
    assert isinstance(active, dict)
    candidates = plan["candidates"]
    inactive = plan["inactive"]
    assert isinstance(candidates, list)
    assert isinstance(inactive, list)

    cleanup = receipt["cleanup"]
    assert isinstance(cleanup, dict)
    cleanup["activeDeveloper"] = str(active["selectedDeveloper"])
    cleanup["activeXcodeBundle"] = str(active["physicalBundle"])
    cleanup["candidates"] = candidates
    cleanup["ignoredSymlinks"] = plan["ignoredSymlinks"]
    cleanup["inactiveBytesWithinBound"] = plan["inactiveBytes"]

    initial_free = available_bytes(runner_temp)
    capacity = receipt["capacity"]
    assert isinstance(capacity, dict)
    capacity["initialFreeBytes"] = initial_free
    capacity["minimumFreeBytes"] = MINIMUM_FREE_BYTES
    current_free = initial_free
    expected_active = active["physicalBundle"]
    assert isinstance(expected_active, Path)

    if current_free < MINIMUM_FREE_BYTES:
        cleanup["attempted"] = True
        run_checked(
            ("sudo", "-n", "true"),
            label="passwordless sudo validation",
        )
        for candidate in inactive:
            if current_free >= MINIMUM_FREE_BYTES:
                break
            candidate_path = Path(str(candidate["path"]))
            resolved, _ = validate_xcode_candidate(
                candidate_path, APPLICATIONS_ROOT
            )
            if resolved == expected_active:
                fail(
                    "active-xcode-deletion-refused",
                    "active Xcode entered the inactive deletion plan",
                )
            run_checked(
                ("sudo", "-n", "/bin/rm", "-rf", "--", str(candidate_path)),
                label="bounded inactive Xcode removal",
            )
            if os.path.lexists(candidate_path):
                fail(
                    "xcode-cleanup-incomplete",
                    "inactive Xcode bundle remained after removal",
                )
            deleted = cleanup["deleted"]
            assert isinstance(deleted, list)
            deleted.append(
                {
                    "path": str(candidate_path),
                    "sizeBytes": candidate["sizeBytes"],
                }
            )
            revalidate_active_xcode(expected_active)
            current_free = available_bytes(runner_temp)

    revalidate_active_xcode(expected_active)
    final_free = available_bytes(runner_temp)
    capacity["finalFreeBytes"] = final_free
    require_capacity(final_free)


def parse_keychain_paths(raw: str) -> list[str]:
    try:
        values = shlex.split(raw)
    except ValueError:
        fail("invalid-keychain-state", "security returned invalid keychains")
    if not values or any(
        not value or "\x00" in value or "\n" in value or "\r" in value
        for value in values
    ):
        fail("invalid-keychain-state", "security returned no valid keychains")
    return values


def probe_dummy_keychain(runner_temp: Path) -> None:
    original_default = parse_keychain_paths(
        run_checked(
            ("security", "default-keychain", "-d", "user"),
            label="default keychain inspection",
        ).stdout
    )
    if len(original_default) != 1:
        fail(
            "invalid-keychain-state",
            "security returned multiple default keychains",
        )
    original_list = parse_keychain_paths(
        run_checked(
            ("security", "list-keychains", "-d", "user"),
            label="keychain list inspection",
        ).stdout
    )
    keychain_path = runner_temp / (
        f"chummer-hosted-probe-{uuid.uuid4().hex}.keychain-db"
    )
    if os.path.lexists(keychain_path):
        fail("unsafe-keychain-path", "dummy keychain path already exists")
    password = secrets.token_hex(32)
    created = False
    primary_error: BaseException | None = None
    try:
        run_checked(
            (
                "security",
                "create-keychain",
                "-p",
                password,
                str(keychain_path),
            ),
            label="dummy keychain creation",
        )
        created = True
        if keychain_path.is_symlink() or not keychain_path.is_file():
            fail(
                "unsafe-keychain-path",
                "security did not create a real dummy keychain file",
            )
        run_checked(
            (
                "security",
                "set-keychain-settings",
                "-lut",
                "300",
                str(keychain_path),
            ),
            label="dummy keychain settings",
        )
        run_checked(
            (
                "security",
                "unlock-keychain",
                "-p",
                password,
                str(keychain_path),
            ),
            label="dummy keychain unlock",
        )
        run_checked(
            (
                "security",
                "list-keychains",
                "-d",
                "user",
                "-s",
                str(keychain_path),
                *original_list,
            ),
            label="dummy keychain list activation",
        )
        run_checked(
            (
                "security",
                "default-keychain",
                "-d",
                "user",
                "-s",
                str(keychain_path),
            ),
            label="dummy default keychain activation",
        )
        observed_default = parse_keychain_paths(
            run_checked(
                ("security", "default-keychain", "-d", "user"),
                label="dummy default keychain verification",
            ).stdout
        )
        if len(observed_default) != 1:
            fail(
                "keychain-lifecycle-failed",
                "dummy keychain did not become the sole default",
            )
        if Path(observed_default[0]).resolve() != keychain_path.resolve():
            fail(
                "keychain-lifecycle-failed",
                "dummy keychain did not become the default",
            )
    except BaseException as exc:
        primary_error = exc
        raise
    finally:
        cleanup_errors: list[str] = []

        def cleanup_command(arguments: Sequence[str], label: str) -> None:
            completed = subprocess.run(
                tuple(arguments),
                check=False,
                capture_output=True,
                text=True,
            )
            if completed.returncode != 0:
                cleanup_errors.append(label)

        cleanup_command(
            (
                "security",
                "list-keychains",
                "-d",
                "user",
                "-s",
                *original_list,
            ),
            "restore keychain list",
        )
        cleanup_command(
            (
                "security",
                "default-keychain",
                "-d",
                "user",
                "-s",
                original_default[0],
            ),
            "restore default keychain",
        )
        if created or os.path.lexists(keychain_path):
            cleanup_command(
                ("security", "lock-keychain", str(keychain_path)),
                "lock dummy keychain",
            )
            cleanup_command(
                ("security", "delete-keychain", str(keychain_path)),
                "delete dummy keychain",
            )
        if os.path.lexists(keychain_path):
            cleanup_errors.append("dummy keychain remained")
        if cleanup_errors and primary_error is None:
            fail(
                "keychain-cleanup-failed",
                "dummy keychain lifecycle could not restore original state",
            )


def probe_tiny_dmg(runner_temp: Path) -> None:
    root = Path(
        tempfile.mkdtemp(
            prefix="chummer-hosted-dmg-probe-",
            dir=runner_temp,
        )
    )
    source = root / "source"
    mountpoint = root / "mount"
    dmg = root / "probe.dmg"
    source.mkdir(mode=0o700)
    mountpoint.mkdir(mode=0o700)
    marker = b"chummer-hosted-macos-probe-v1\n"
    (source / "probe.txt").write_bytes(marker)
    attached = False
    primary_error: BaseException | None = None
    try:
        run_checked(
            (
                "hdiutil",
                "create",
                "-quiet",
                "-ov",
                "-format",
                "UDZO",
                "-volname",
                "ChummerHostedProbe",
                "-srcfolder",
                str(source),
                str(dmg),
            ),
            label="tiny DMG creation",
        )
        if dmg.is_symlink() or not dmg.is_file():
            fail("dmg-probe-failed", "hdiutil did not create a real DMG")
        run_checked(
            (
                "hdiutil",
                "attach",
                "-quiet",
                "-nobrowse",
                "-readonly",
                "-mountpoint",
                str(mountpoint),
                str(dmg),
            ),
            label="tiny DMG mount",
        )
        attached = True
        if (mountpoint / "probe.txt").read_bytes() != marker:
            fail("dmg-probe-failed", "mounted DMG marker did not match")
    except BaseException as exc:
        primary_error = exc
        raise
    finally:
        detach_failed = False
        if attached:
            detached = subprocess.run(
                ("hdiutil", "detach", "-quiet", str(mountpoint)),
                check=False,
                capture_output=True,
                text=True,
            )
            detach_failed = detached.returncode != 0
        shutil.rmtree(root, ignore_errors=True)
        if (detach_failed or os.path.lexists(root)) and primary_error is None:
            fail(
                "dmg-cleanup-failed",
                "tiny DMG probe could not cleanly detach and remove state",
            )


def probe_toolchain(receipt: dict[str, object]) -> None:
    command_paths = {
        name: require_command(name) for name in REQUIRED_COMMANDS
    }
    notarytool = safe_single_line(
        run_checked(
            ("xcrun", "--find", "notarytool"),
            label="notarytool discovery",
        ).stdout,
        label="notarytool discovery",
    )
    stapler = safe_single_line(
        run_checked(
            ("xcrun", "--find", "stapler"),
            label="stapler discovery",
        ).stdout,
        label="stapler discovery",
    )
    notarytool_version = safe_single_line(
        run_checked(
            ("xcrun", "notarytool", "--version"),
            label="notarytool version",
        ).stdout,
        label="notarytool version",
    )
    stapler_help = run_checked(
        ("xcrun", "stapler", "help"),
        label="stapler CLI startup",
        accepted_returncodes=(0, 1, 64),
    )
    stapler_usage = f"{stapler_help.stdout}\n{stapler_help.stderr}".lower()
    if "stapler" not in stapler_usage and "usage" not in stapler_usage:
        fail("invalid-tool-output", "stapler help did not identify the tool")
    run_checked(
        (
            "codesign",
            "--verify",
            "--strict",
            command_paths["security"],
        ),
        label="codesign verification of the system security CLI",
    )
    spctl_status = safe_single_line(
        run_checked(
            ("spctl", "--status"),
            label="Gatekeeper assessment status",
        ).stdout,
        label="Gatekeeper assessment status",
    )
    if spctl_status != "assessments enabled":
        fail(
            "gatekeeper-disabled",
            "Gatekeeper assessments are not enabled",
        )
    dotnet_version = safe_single_line(
        run_checked(
            ("dotnet", "--version"),
            label=".NET SDK version",
        ).stdout,
        label=".NET SDK version",
    )
    if dotnet_version != "10.0.103":
        fail(
            "wrong-dotnet-version",
            "probe requires the governed .NET SDK 10.0.103",
        )
    node_version = safe_single_line(
        run_checked(
            ("node", "--version"),
            label="Node.js version",
        ).stdout,
        label="Node.js version",
    )
    if re.fullmatch(r"v22\.[0-9]+\.[0-9]+", node_version) is None:
        fail(
            "wrong-node-version",
            "probe requires a reviewed Node.js 22 runtime",
        )
    if sys.version_info < (3, 11):
        fail(
            "wrong-python-version",
            "probe requires Python 3.11 or newer",
        )

    toolchain = receipt["toolchain"]
    assert isinstance(toolchain, dict)
    toolchain.update(
        {
            "codesignSystemVerification": "passed",
            "commandPaths": command_paths,
            "dotnetVersion": dotnet_version,
            "gatekeeperStatus": spctl_status,
            "nodeVersion": node_version,
            "notarytoolPath": notarytool,
            "notarytoolVersion": notarytool_version,
            "pythonVersion": platform.python_version(),
            "staplerPath": stapler,
        }
    )


def base_receipt(environment: Mapping[str, str]) -> dict[str, object]:
    return {
        "checks": {
            "capacity": False,
            "dummyKeychainLifecycle": False,
            "hostedRunnerContext": False,
            "secretless": False,
            "tinyDmgLifecycle": False,
            "toolchain": False,
        },
        "cleanup": {
            "activeDeveloper": None,
            "activeXcodeBundle": None,
            "attempted": False,
            "bounded": {
                "applicationsRoot": str(APPLICATIONS_ROOT),
                "maximumDeleteBytes": MAX_XCODE_DELETE_BYTES,
                "maximumDeleteCount": MAX_XCODE_DELETE_COUNT,
                "selectionPolicy": (
                    "oldest-version-first-until-20-GiB-free"
                ),
                "strictBundlePattern": XCODE_BUNDLE_RE.pattern,
            },
            "candidates": [],
            "deleted": [],
            "ignoredSymlinks": [],
            "inactiveBytesWithinBound": None,
        },
        "capacity": {
            "finalFreeBytes": None,
            "initialFreeBytes": None,
            "minimumFreeBytes": MINIMUM_FREE_BYTES,
        },
        "completedAtUtc": None,
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "failure": None,
        "github": {
            "eventName": environment.get("GITHUB_EVENT_NAME", ""),
            "ref": environment.get("GITHUB_REF", ""),
            "repository": environment.get("GITHUB_REPOSITORY", ""),
            "runAttempt": environment.get("GITHUB_RUN_ATTEMPT", ""),
            "runId": environment.get("GITHUB_RUN_ID", ""),
            "sha": environment.get("GITHUB_SHA", ""),
            "workflow": environment.get("GITHUB_WORKFLOW", ""),
        },
        "nonPublishing": {
            "artifactBuilt": False,
            "notarizationSubmitted": False,
            "publicationAttempted": False,
            "releaseAuthorityAccepted": False,
            "signingAttempted": False,
        },
        "runner": {
            "architecture": platform.machine(),
            "environment": environment.get("RUNNER_ENVIRONMENT", ""),
            "imageOS": environment.get("ImageOS", ""),
            "imageVersion": environment.get("ImageVersion", ""),
            "label": environment.get(
                "CHUMMER_MACOS_HOSTED_PROBE_RUNNER_IMAGE", ""
            ),
            "operatingSystem": platform.system(),
        },
        "startedAtUtc": utc_now(),
        "status": "running",
        "toolchain": {},
    }


def validate_receipt_path(path: Path, runner_temp: Path) -> None:
    if not path.is_absolute() or path.parent != runner_temp:
        fail(
            "unsafe-receipt-path",
            "receipt must be a direct child of RUNNER_TEMP",
        )
    if os.path.lexists(path):
        fail("unsafe-receipt-path", "receipt path already exists")
    if runner_temp.is_symlink() or not runner_temp.is_dir():
        fail(
            "unsafe-receipt-path",
            "RUNNER_TEMP is not a real directory",
        )


def write_receipt(path: Path, receipt: dict[str, object]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    if os.path.lexists(temporary):
        fail("unsafe-receipt-path", "temporary receipt path already exists")
    payload = (
        json.dumps(
            receipt,
            indent=2,
            sort_keys=True,
            ensure_ascii=True,
        )
        + "\n"
    ).encode("utf-8")
    descriptor = os.open(
        temporary,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
        path.chmod(0o644)
    finally:
        if os.path.lexists(temporary):
            temporary.unlink()


def run_probe(receipt_path: Path, environment: Mapping[str, str]) -> int:
    runner_temp = Path(environment.get("RUNNER_TEMP", ""))
    validate_receipt_path(receipt_path, runner_temp)
    receipt = base_receipt(environment)
    write_receipt(receipt_path, receipt)
    exit_code = 1
    try:
        validate_hosted_context(
            environment,
            system_name=platform.system(),
            machine_name=platform.machine(),
        )
        receipt["checks"]["hostedRunnerContext"] = True

        assert_secretless_environment(environment)
        receipt["checks"]["secretless"] = True

        perform_bounded_cleanup(runner_temp, receipt)
        receipt["checks"]["capacity"] = True

        probe_toolchain(receipt)
        receipt["checks"]["toolchain"] = True

        probe_dummy_keychain(runner_temp)
        receipt["checks"]["dummyKeychainLifecycle"] = True

        probe_tiny_dmg(runner_temp)
        receipt["checks"]["tinyDmgLifecycle"] = True

        receipt["status"] = "passed"
        exit_code = 0
    except ProbeFailure as exc:
        receipt["failure"] = {
            "code": exc.code,
            "message": str(exc),
        }
        receipt["status"] = "failed"
        print(f"probe failed [{exc.code}]: {exc}", file=sys.stderr)
    except Exception:
        receipt["failure"] = {
            "code": "unexpected-error",
            "message": "probe failed with an unexpected internal error",
        }
        receipt["status"] = "failed"
        print(
            "probe failed [unexpected-error]; inspect the source and rerun",
            file=sys.stderr,
        )
    finally:
        receipt["completedAtUtc"] = utc_now()
        if os.path.lexists(receipt_path) and receipt_path.is_symlink():
            receipt["failure"] = {
                "code": "unsafe-receipt-path",
                "message": "probe receipt path was replaced by a symlink",
            }
            receipt["status"] = "failed"
            exit_code = 1
        write_receipt(receipt_path, receipt)
    if exit_code == 0:
        capacity = receipt["capacity"]
        assert isinstance(capacity, dict)
        final_gib = int(capacity["finalFreeBytes"]) / GIB
        print(
            f"secretless hosted-mac probe passed with {final_gib:.2f} GiB free"
        )
    return exit_code


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--receipt",
        type=Path,
        required=True,
        help="Direct RUNNER_TEMP child receiving the nonsecret JSON receipt",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_args(sys.argv[1:] if argv is None else argv)
    return run_probe(arguments.receipt, os.environ)


if __name__ == "__main__":
    raise SystemExit(main())
