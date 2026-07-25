#!/usr/bin/env python3
"""Secretless native build/install/startup proof for hosted macOS ARM64.

The ``run`` command consumes an already-created stage-only bundle, installs
its unsigned app into an isolated Applications-equivalent directory, executes
the app's startup-smoke path, uninstalls it, and emits a non-publishing
receipt.  The ``verify`` command consumes that receipt in the later protected
evidence job before any Apple or escrow secret is referenced.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import platform
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any, Mapping, Sequence

import macos_hosted_capacity_probe as capacity_probe


CONTRACT_NAME = "chummer6-ui.macos-hosted-native-lifecycle-proof.v1"
CONSUMPTION_CONTRACT_NAME = (
    "chummer6-ui.macos-hosted-native-proof-consumption.v1"
)
CONTRACT_VERSION = 1
AUTHORITY_CONTRACT = "chummer6-ui.macos-flagship-authority-validation"
CAPACITY_CONTRACT = "chummer6-ui.macos-hosted-capacity-probe.v1"
STAGE_CONTRACT = "chummer.run.mac_release_stage_only"
WORKFLOW_PATH = ".github/workflows/macos-flagship-evidence.yml"
REPOSITORY = "ArchonMegalon/chummer6-ui"
RUNNER_LABEL = "macos-15"
RUNNER_IMAGE_OS = "macos15"
RUNNER_ARCH = "arm64"
RUNNER_ENVIRONMENT = "github-hosted"
RUNNER_OPERATING_SYSTEM = "Darwin"
RELEASE_REF = "refs/heads/main"
RID = "osx-arm64"
APP_KEY = "avalonia"
LAUNCH_TARGET = "Chummer.Avalonia"
ARTIFACT_FILE_NAME = "chummer-avalonia-osx-arm64-installer.dmg"
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
VERSION_PATTERN = re.compile(r"^run-[0-9]{8}-[0-9]{6}$")
INTEGER_PATTERN = re.compile(r"^[1-9][0-9]*$")
IMAGE_VERSION_PATTERN = re.compile(r"^[0-9A-Za-z._-]{1,128}$")
LOGIN_PATTERN = re.compile(
    r"^(?:github-actions\[bot\]|"
    r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$"
)
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_ARTIFACT_BYTES = 512 * 1024 * 1024


class ProofFailure(RuntimeError):
    """Expected fail-closed proof rejection."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code


def fail(code: str, message: str) -> None:
    raise ProofFailure(code, message)


def utc_now() -> str:
    return (
        dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def canonical_bytes(payload: Any) -> bytes:
    return json.dumps(
        payload,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def require_real_file(
    path: Path,
    label: str,
    *,
    maximum: int = MAX_JSON_BYTES,
) -> None:
    try:
        metadata = path.lstat()
    except FileNotFoundError:
        fail("missing-input", f"{label} is missing")
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
        fail("unsafe-input", f"{label} must be a regular non-symlink file")
    if metadata.st_size < 1 or metadata.st_size > maximum:
        fail("invalid-input-size", f"{label} has an invalid size")


def read_json(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    require_real_file(path, label)
    raw = path.read_bytes()
    try:
        payload = json.loads(
            raw.decode("utf-8"),
            parse_constant=lambda value: fail(
                "invalid-json", f"{label} contains {value}"
            ),
            object_pairs_hook=_unique_object,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail("invalid-json", f"{label} is not strict UTF-8 JSON: {error}")
    if not isinstance(payload, dict):
        fail("invalid-json", f"{label} must be a JSON object")
    return payload, raw


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail("duplicate-json-key", f"JSON contains duplicate key {key}")
        result[key] = value
    return result


def write_json(path: Path, payload: dict[str, Any]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    descriptor = os.open(
        temporary,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(
                json.dumps(
                    payload,
                    indent=2,
                    sort_keys=True,
                    ensure_ascii=True,
                    allow_nan=False,
                ).encode("utf-8")
                + b"\n"
            )
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
        path.chmod(0o644)
    finally:
        if os.path.lexists(temporary):
            temporary.unlink()


def validate_output_path(path: Path, runner_temp: Path) -> None:
    if (
        not path.is_absolute()
        or path.parent != runner_temp
        or os.path.lexists(path)
    ):
        fail(
            "unsafe-output",
            "proof output must be a new direct child of RUNNER_TEMP",
        )


def require_string(
    payload: Mapping[str, Any],
    key: str,
    label: str,
    *,
    pattern: re.Pattern[str],
) -> str:
    value = payload.get(key)
    if not isinstance(value, str) or pattern.fullmatch(value) is None:
        fail("invalid-receipt", f"{label} {key} is invalid")
    return value


def require_exact_keys(
    payload: Any,
    expected: set[str],
    label: str,
) -> None:
    if not isinstance(payload, dict) or set(payload) != expected:
        fail("invalid-receipt", f"{label} does not have the exact schema")


def require_sha256(value: Any, label: str) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        fail("invalid-receipt", f"{label} is not an exact SHA-256")
    return value


def require_timestamp(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.endswith("Z"):
        fail("invalid-receipt", f"{label} is not a UTC timestamp")
    try:
        parsed = dt.datetime.fromisoformat(value.removesuffix("Z") + "+00:00")
    except ValueError:
        fail("invalid-receipt", f"{label} is not a UTC timestamp")
    if parsed.tzinfo != dt.timezone.utc:
        fail("invalid-receipt", f"{label} is not a UTC timestamp")
    return value


def current_github(environment: Mapping[str, str]) -> dict[str, str]:
    github = {
        "actor": environment.get("GITHUB_ACTOR", ""),
        "ref": environment.get("GITHUB_REF", ""),
        "repository": environment.get("GITHUB_REPOSITORY", ""),
        "rerunPolicy": "same-actor-only",
        "runAttempt": environment.get("GITHUB_RUN_ATTEMPT", ""),
        "runId": environment.get("GITHUB_RUN_ID", ""),
        "sha": environment.get("GITHUB_SHA", ""),
        "triggeringActor": environment.get("GITHUB_TRIGGERING_ACTOR", ""),
        "workflow": WORKFLOW_PATH,
    }
    if (
        environment.get("GITHUB_EVENT_NAME") != "workflow_dispatch"
        or github["repository"] != REPOSITORY
        or github["ref"] != RELEASE_REF
        or COMMIT_PATTERN.fullmatch(github["sha"]) is None
        or LOGIN_PATTERN.fullmatch(github["actor"]) is None
        or LOGIN_PATTERN.fullmatch(github["triggeringActor"]) is None
        or github["actor"] != github["triggeringActor"]
        or INTEGER_PATTERN.fullmatch(github["runId"]) is None
        or INTEGER_PATTERN.fullmatch(github["runAttempt"]) is None
    ):
        fail(
            "untrusted-github-context",
            "GitHub runtime is not the governed same-actor main dispatch",
        )
    return github


def current_runner(
    environment: Mapping[str, str],
    *,
    system_name: str | None = None,
    machine_name: str | None = None,
) -> dict[str, str]:
    system_name = platform.system() if system_name is None else system_name
    machine_name = platform.machine() if machine_name is None else machine_name
    observed = capacity_probe.validate_hosted_context(
        environment,
        system_name=system_name,
        machine_name=machine_name,
    )
    if observed["runnerImage"] != RUNNER_LABEL:
        fail("wrong-runner-label", "native proof requires pinned macos-15")
    return {
        "architecture": machine_name,
        "environment": environment.get("RUNNER_ENVIRONMENT", ""),
        "imageOS": observed["imageOS"],
        "imageVersion": observed["imageVersion"],
        "label": observed["runnerImage"],
        "operatingSystem": system_name,
    }


def expected_runner_policy() -> dict[str, str]:
    return {
        "architecture": RUNNER_ARCH,
        "environment": RUNNER_ENVIRONMENT,
        "imageOS": RUNNER_IMAGE_OS,
        "label": RUNNER_LABEL,
        "operatingSystem": "macOS",
    }


def validate_capacity_receipt(
    payload: dict[str, Any],
    *,
    expected_runner: dict[str, str],
    expected_github: dict[str, str],
) -> None:
    runner = payload.get("runner")
    github = payload.get("github")
    checks = payload.get("checks")
    capacity = payload.get("capacity")
    nonpublishing = payload.get("nonPublishing")
    if (
        payload.get("contractName") != CAPACITY_CONTRACT
        or payload.get("contractVersion") != 1
        or payload.get("status") != "passed"
        or runner != expected_runner
        or not isinstance(github, dict)
        or github.get("repository") != expected_github["repository"]
        or github.get("ref") != expected_github["ref"]
        or github.get("sha") != expected_github["sha"]
        or str(github.get("runId") or "") != expected_github["runId"]
        or str(github.get("runAttempt") or "")
        != expected_github["runAttempt"]
        or not isinstance(checks, dict)
        or set(checks)
        != {
            "capacity",
            "dummyKeychainLifecycle",
            "hostedRunnerContext",
            "secretless",
            "tinyDmgLifecycle",
            "toolchain",
        }
        or any(value is not True for value in checks.values())
        or not isinstance(capacity, dict)
        or capacity.get("minimumFreeBytes")
        != capacity_probe.MINIMUM_FREE_BYTES
        or isinstance(capacity.get("finalFreeBytes"), bool)
        or not isinstance(capacity.get("finalFreeBytes"), int)
        or capacity["finalFreeBytes"] < capacity_probe.MINIMUM_FREE_BYTES
        or nonpublishing
        != {
            "artifactBuilt": False,
            "notarizationSubmitted": False,
            "publicationAttempted": False,
            "releaseAuthorityAccepted": False,
            "signingAttempted": False,
        }
    ):
        fail(
            "invalid-capacity-receipt",
            "capacity receipt does not bind the exact passing hosted runner",
        )


def validate_authority_receipt(
    payload: dict[str, Any],
    *,
    expected_github: dict[str, str],
) -> tuple[str, str]:
    if (
        payload.get("contractName") != AUTHORITY_CONTRACT
        or payload.get("contractVersion") != 2
        or payload.get("status") != "pass"
        or payload.get("github") != expected_github
        or payload.get("runnerPolicy") != expected_runner_policy()
    ):
        fail(
            "invalid-authority-receipt",
            "authority receipt does not permit the fixed hosted runner",
        )
    release_version = require_string(
        payload,
        "releaseVersion",
        "authority receipt",
        pattern=VERSION_PATTERN,
    )
    if payload.get("rid") != RID:
        fail("invalid-authority-receipt", "authority receipt RID is invalid")
    return release_version, RID


def validate_stage(
    stage_root: Path,
    *,
    release_version: str,
) -> tuple[Path, Path, Path, dict[str, Any]]:
    if (
        not stage_root.is_absolute()
        or not stage_root.is_dir()
        or stage_root.is_symlink()
    ):
        fail("unsafe-stage", "stage root must be an absolute real directory")
    source = stage_root / "files" / ARTIFACT_FILE_NAME
    manifest_path = stage_root / "RELEASE_CHANNEL.generated.json"
    receipt_path = stage_root / "release-evidence" / "mac-stage-only.json"
    require_real_file(source, "unsigned stage DMG", maximum=MAX_ARTIFACT_BYTES)
    manifest, _ = read_json(manifest_path, "stage manifest")
    stage_receipt, _ = read_json(receipt_path, "stage-only receipt")
    if (
        stage_receipt.get("contractName") != STAGE_CONTRACT
        or stage_receipt.get("status") != "pass"
        or stage_receipt.get("mode") != "stage_only"
        or stage_receipt.get("releaseVersion") != release_version
        or stage_receipt.get("rid") != RID
        or stage_receipt.get("appHeads") != [APP_KEY]
        or stage_receipt.get("outputPathDisclosure")
        != "directory_name_only"
        or SHA256_PATTERN.fullmatch(
            str(stage_receipt.get("sourceReceiptSha256") or "")
        )
        is None
        or any(
            stage_receipt.get(key) is not False
            for key in (
                "countsAsPublicationEvidence",
                "publicActivationAttempted",
                "publicationAttempted",
                "uploadAttempted",
            )
        )
    ):
        fail(
            "invalid-stage-receipt",
            "stage-only receipt is not a passing non-publishing build",
        )
    source_sha = sha256_file(source)
    rows = [
        row
        for row in (manifest.get("artifacts") or [])
        if isinstance(row, dict)
        and row.get("head") == APP_KEY
        and row.get("platform") == "macos"
        and row.get("rid") == RID
        and row.get("fileName") == ARTIFACT_FILE_NAME
    ]
    if (
        len(rows) != 1
        or str(rows[0].get("sha256") or "").removeprefix("sha256:")
        != source_sha
        or rows[0].get("sizeBytes") != source.stat().st_size
    ):
        fail(
            "invalid-stage-manifest",
            "stage manifest does not bind the exact unsigned DMG",
        )
    return source, manifest_path, receipt_path, stage_receipt


def run_checked(
    arguments: Sequence[str],
    *,
    label: str,
    environment: Mapping[str, str] | None = None,
    timeout: int = 300,
) -> subprocess.CompletedProcess[str]:
    try:
        completed = subprocess.run(
            tuple(arguments),
            check=False,
            capture_output=True,
            text=True,
            env=None if environment is None else dict(environment),
            timeout=timeout,
        )
    except subprocess.TimeoutExpired:
        fail("command-timeout", f"{label} timed out")
    if completed.returncode != 0:
        fail(
            "command-failed",
            f"{label} failed with exit code {completed.returncode}",
        )
    return completed


def find_single_app(root: Path) -> Path:
    matches = sorted(
        path
        for path in root.iterdir()
        if path.name.endswith(".app")
        and path.is_dir()
        and not path.is_symlink()
    )
    if len(matches) != 1:
        fail(
            "invalid-dmg-layout",
            "unsigned DMG must contain exactly one top-level app bundle",
        )
    return matches[0]


def validate_startup_receipt(
    payload: dict[str, Any],
    *,
    artifact_sha: str,
    release_version: str,
    installed_executable: Path,
) -> None:
    process_path = str(payload.get("processPath") or "")
    if (
        str(payload.get("status") or "").lower() not in {"pass", "passed"}
        or payload.get("headId") != APP_KEY
        or payload.get("platform") != "macos"
        or payload.get("rid") != RID
        or payload.get("releaseVersion") != release_version
        or payload.get("readyCheckpoint") != "pre_ui_event_loop"
        or str(payload.get("artifactDigest") or "").removeprefix("sha256:")
        != artifact_sha
        or payload.get("hostClass")
        != "github-hosted-macos-arm64-secretless-capacity"
        or Path(process_path).name != installed_executable.name
    ):
        fail(
            "invalid-startup-receipt",
            "startup receipt does not bind the installed unsigned app",
        )


def lifecycle_nonpublishing() -> dict[str, bool]:
    return {
        "artifactRetained": False,
        "countsAsPublicationEvidence": False,
        "notarizationSubmitted": False,
        "protectedReleaseAuthorityAccepted": False,
        "publicActivationAttempted": False,
        "publicationAttempted": False,
        "releaseUploadAttempted": False,
        "signingAttempted": False,
        "unsignedArtifactBuilt": True,
    }


def validate_owned_plaintext_roots(
    runner_temp: Path,
    *,
    stage_root: Path,
    build_root: Path,
) -> None:
    expected: dict[Path, str] = {
        stage_root: "macos-hosted-native-stage",
        build_root: "macos-hosted-native-build",
    }
    for path, expected_name in expected.items():
        if (
            not path.is_absolute()
            or path.parent != runner_temp
            or path.name != expected_name
            or path.is_symlink()
            or not path.is_dir()
        ):
            fail(
                "unsafe-cleanup-root",
                "secretless plaintext cleanup root is not the owned path",
            )
    return None


def remove_owned_plaintext_roots(
    runner_temp: Path,
    *,
    stage_root: Path,
    build_root: Path,
) -> None:
    validate_owned_plaintext_roots(
        runner_temp,
        stage_root=stage_root,
        build_root=build_root,
    )
    expected = (stage_root, build_root)
    for path in expected:
        shutil.rmtree(path)
        if os.path.lexists(path):
            fail(
                "plaintext-cleanup-failed",
                "secretless build or stage bytes remained after cleanup",
            )


def execute_lifecycle(
    *,
    source_dmg: Path,
    release_version: str,
    runner_temp: Path,
    environment: Mapping[str, str],
) -> tuple[dict[str, Any], dict[str, bool]]:
    work_root = Path(
        tempfile.mkdtemp(prefix="macos-hosted-native.", dir=runner_temp)
    )
    work_root.chmod(0o700)
    mount_root = work_root / "mount"
    install_root = work_root / "Applications"
    state_root = work_root / "state"
    startup_receipt_path = work_root / "startup.receipt.json"
    failure_path = work_root / "startup.failure.json"
    mounted = False
    installed_app: Path | None = None
    checks = {
        "arm64Executable": False,
        "dmgMountedReadOnly": False,
        "isolatedInstall": False,
        "isolatedUninstall": False,
        "plaintextCleanup": False,
        "startupSmoke": False,
    }
    try:
        mount_root.mkdir(mode=0o700)
        install_root.mkdir(mode=0o700)
        state_root.mkdir(mode=0o700)
        run_checked(
            (
                "hdiutil",
                "attach",
                "-nobrowse",
                "-readonly",
                "-mountpoint",
                str(mount_root),
                str(source_dmg),
            ),
            label="unsigned DMG read-only mount",
        )
        mounted = True
        checks["dmgMountedReadOnly"] = True
        app_on_dmg = find_single_app(mount_root)
        installed_app = install_root / app_on_dmg.name
        run_checked(
            ("ditto", str(app_on_dmg), str(installed_app)),
            label="isolated unsigned app install",
        )
        if installed_app.is_symlink() or not installed_app.is_dir():
            fail(
                "invalid-installed-app",
                "isolated install did not produce a real app directory",
            )
        checks["isolatedInstall"] = True
        run_checked(
            ("hdiutil", "detach", str(mount_root)),
            label="unsigned DMG detach",
        )
        mounted = False
        executable = installed_app / "Contents" / "MacOS" / LAUNCH_TARGET
        require_real_file(
            executable,
            "installed app executable",
            maximum=MAX_ARTIFACT_BYTES,
        )
        architectures = run_checked(
            ("lipo", "-archs", str(executable)),
            label="installed app architecture inspection",
        ).stdout.split()
        if RUNNER_ARCH not in architectures:
            fail(
                "wrong-app-architecture",
                "installed app executable does not contain arm64",
            )
        checks["arm64Executable"] = True
        artifact_sha = sha256_file(source_dmg)
        startup_environment = dict(environment)
        startup_environment.update(
            {
                "CHUMMER_DESKTOP_RELEASE_CHANNEL": "preview",
                "CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST": (
                    f"sha256:{artifact_sha}"
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET": str(
                    failure_path
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS": (
                    "github-hosted-macos-arm64-secretless-capacity"
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT": (
                    "pre_ui_event_loop"
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT": str(
                    startup_receipt_path
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION": (
                    release_version
                ),
                "CHUMMER_DESKTOP_STARTUP_SMOKE_RID": RID,
                "CHUMMER_DESKTOP_STATE_ROOT": str(state_root),
                "HOME": str(work_root / "home"),
            }
        )
        (work_root / "home").mkdir(mode=0o700)
        run_checked(
            (str(executable), "--startup-smoke"),
            label="installed unsigned app startup smoke",
            environment=startup_environment,
            timeout=180,
        )
        startup, startup_raw = read_json(
            startup_receipt_path, "startup-smoke receipt"
        )
        validate_startup_receipt(
            startup,
            artifact_sha=artifact_sha,
            release_version=release_version,
            installed_executable=executable,
        )
        checks["startupSmoke"] = True
        shutil.rmtree(installed_app)
        if os.path.lexists(installed_app):
            fail(
                "uninstall-failed",
                "isolated unsigned app remained after uninstall",
            )
        checks["isolatedUninstall"] = True
        return (
            {
                "artifactDigest": f"sha256:{artifact_sha}",
                "hostClass": startup["hostClass"],
                "readyCheckpoint": startup["readyCheckpoint"],
                "receiptSha256": sha256_bytes(startup_raw),
                "releaseVersion": release_version,
                "rid": RID,
                "status": "pass",
            },
            checks,
        )
    finally:
        if mounted:
            subprocess.run(
                ("hdiutil", "detach", "-force", str(mount_root)),
                check=False,
                capture_output=True,
                text=True,
            )
        if installed_app is not None and os.path.lexists(installed_app):
            shutil.rmtree(installed_app, ignore_errors=True)
        shutil.rmtree(work_root)
        if os.path.lexists(work_root):
            fail(
                "lifecycle-cleanup-failed",
                "isolated native lifecycle root remained after cleanup",
            )


def command_run(args: argparse.Namespace) -> int:
    environment = os.environ
    runner_temp = Path(environment.get("RUNNER_TEMP", ""))
    validate_output_path(args.output, runner_temp)
    runner = current_runner(environment)
    github = current_github(environment)
    capacity_probe.assert_secretless_environment(environment)
    capacity, capacity_raw = read_json(
        args.capacity_receipt, "capacity receipt"
    )
    validate_capacity_receipt(
        capacity,
        expected_runner=runner,
        expected_github=github,
    )
    authority, authority_raw = read_json(
        args.authority_receipt, "authority receipt"
    )
    release_version, _ = validate_authority_receipt(
        authority,
        expected_github=github,
    )
    validate_owned_plaintext_roots(
        runner_temp,
        stage_root=args.stage_root,
        build_root=args.build_root,
    )
    source, manifest_path, stage_receipt_path, _ = validate_stage(
        args.stage_root,
        release_version=release_version,
    )
    build_projection = {
        "artifact": {
            "fileName": source.name,
            "sha256": sha256_file(source),
            "sizeBytes": source.stat().st_size,
        },
        "stageManifestSha256": sha256_file(manifest_path),
        "stageOnlyReceiptSha256": sha256_file(stage_receipt_path),
    }
    startup, checks = execute_lifecycle(
        source_dmg=source,
        release_version=release_version,
        runner_temp=runner_temp,
        environment=environment,
    )
    remove_owned_plaintext_roots(
        runner_temp,
        stage_root=args.stage_root,
        build_root=args.build_root,
    )
    checks["plaintextCleanup"] = True
    receipt = {
        "authority": {
            "receiptSha256": sha256_bytes(authority_raw),
            "releaseVersion": release_version,
            "rid": RID,
        },
        "build": build_projection,
        "capacityReceiptSha256": sha256_bytes(capacity_raw),
        "checks": checks,
        "completedAtUtc": utc_now(),
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "github": github,
        "nonPublishing": lifecycle_nonpublishing(),
        "runner": runner,
        "startup": startup,
        "status": "pass",
    }
    write_json(args.output, receipt)
    return 0


def normalize_artifact_digest(value: str) -> str:
    normalized = value.lower().removeprefix("sha256:")
    if SHA256_PATTERN.fullmatch(normalized) is None:
        fail("invalid-artifact-digest", "Actions artifact digest is invalid")
    return normalized


def validate_lifecycle_receipt(
    payload: dict[str, Any],
    *,
    capacity_raw: bytes,
    authority_raw: bytes,
    expected_runner: dict[str, str],
    expected_github: dict[str, str],
) -> None:
    require_exact_keys(
        payload,
        {
            "authority",
            "build",
            "capacityReceiptSha256",
            "checks",
            "completedAtUtc",
            "contractName",
            "contractVersion",
            "github",
            "nonPublishing",
            "runner",
            "startup",
            "status",
        },
        "native lifecycle receipt",
    )
    checks = payload.get("checks")
    build = payload.get("build")
    startup = payload.get("startup")
    authority = payload.get("authority")
    artifact = build.get("artifact") if isinstance(build, dict) else None
    require_exact_keys(
        authority,
        {"receiptSha256", "releaseVersion", "rid"},
        "native lifecycle authority",
    )
    require_exact_keys(
        build,
        {"artifact", "stageManifestSha256", "stageOnlyReceiptSha256"},
        "native lifecycle build",
    )
    require_exact_keys(
        artifact,
        {"fileName", "sha256", "sizeBytes"},
        "native lifecycle artifact",
    )
    require_exact_keys(
        startup,
        {
            "artifactDigest",
            "hostClass",
            "readyCheckpoint",
            "receiptSha256",
            "releaseVersion",
            "rid",
            "status",
        },
        "native lifecycle startup",
    )
    require_timestamp(
        payload.get("completedAtUtc"),
        "native lifecycle completedAtUtc",
    )
    if (
        payload.get("contractName") != CONTRACT_NAME
        or payload.get("contractVersion") != CONTRACT_VERSION
        or payload.get("status") != "pass"
        or payload.get("github") != expected_github
        or payload.get("runner") != expected_runner
        or payload.get("capacityReceiptSha256")
        != sha256_bytes(capacity_raw)
        or authority.get("receiptSha256")
        != sha256_bytes(authority_raw)
        or VERSION_PATTERN.fullmatch(
            str(authority.get("releaseVersion") or "")
        )
        is None
        or authority.get("rid") != RID
        or not isinstance(checks, dict)
        or set(checks)
        != {
            "arm64Executable",
            "dmgMountedReadOnly",
            "isolatedInstall",
            "isolatedUninstall",
            "plaintextCleanup",
            "startupSmoke",
        }
        or any(value is not True for value in checks.values())
        or not isinstance(artifact, dict)
        or artifact.get("fileName") != ARTIFACT_FILE_NAME
        or require_sha256(
            artifact.get("sha256"), "native lifecycle artifact.sha256"
        )
        != artifact.get("sha256")
        or isinstance(artifact.get("sizeBytes"), bool)
        or not isinstance(artifact.get("sizeBytes"), int)
        or artifact["sizeBytes"] < 1
        or artifact["sizeBytes"] > MAX_ARTIFACT_BYTES
        or require_sha256(
            build.get("stageManifestSha256"),
            "native lifecycle stageManifestSha256",
        )
        != build.get("stageManifestSha256")
        or require_sha256(
            build.get("stageOnlyReceiptSha256"),
            "native lifecycle stageOnlyReceiptSha256",
        )
        != build.get("stageOnlyReceiptSha256")
        or not isinstance(startup, dict)
        or startup.get("status") != "pass"
        or startup.get("rid") != RID
        or startup.get("releaseVersion")
        != authority.get("releaseVersion")
        or startup.get("artifactDigest")
        != f"sha256:{artifact.get('sha256')}"
        or require_sha256(
            startup.get("receiptSha256"),
            "native lifecycle startup.receiptSha256",
        )
        != startup.get("receiptSha256")
        or payload.get("nonPublishing") != lifecycle_nonpublishing()
    ):
        fail(
            "invalid-lifecycle-receipt",
            "native lifecycle receipt is not an exact secretless proof",
        )


def command_verify(args: argparse.Namespace) -> int:
    environment = os.environ
    runner_temp = Path(environment.get("RUNNER_TEMP", ""))
    validate_output_path(args.output, runner_temp)
    evidence_runner = current_runner(environment)
    github = current_github(environment)
    capacity_probe.assert_secretless_environment(environment)

    source_capacity, source_capacity_raw = read_json(
        args.source_capacity_receipt, "source capacity receipt"
    )
    source_authority, source_authority_raw = read_json(
        args.source_authority_receipt, "source authority receipt"
    )
    source_lifecycle, source_lifecycle_raw = read_json(
        args.source_lifecycle_receipt, "source lifecycle receipt"
    )
    evidence_capacity, evidence_capacity_raw = read_json(
        args.evidence_capacity_receipt, "evidence capacity receipt"
    )
    source_runner = source_lifecycle.get("runner")
    if not isinstance(source_runner, dict):
        fail("invalid-lifecycle-receipt", "source runner is absent")
    validate_capacity_receipt(
        source_capacity,
        expected_runner=source_runner,
        expected_github=github,
    )
    validate_authority_receipt(
        source_authority,
        expected_github=github,
    )
    validate_lifecycle_receipt(
        source_lifecycle,
        capacity_raw=source_capacity_raw,
        authority_raw=source_authority_raw,
        expected_runner=source_runner,
        expected_github=github,
    )
    validate_capacity_receipt(
        evidence_capacity,
        expected_runner=evidence_runner,
        expected_github=github,
    )
    if (
        not INTEGER_PATTERN.fullmatch(args.artifact_id)
        or args.artifact_name
        != (
            "macos-hosted-native-capacity-"
            f"{github['runId']}-{github['runAttempt']}"
        )
    ):
        fail(
            "invalid-artifact-identity",
            "source proof artifact is not bound to this run and attempt",
        )
    artifact_digest = normalize_artifact_digest(args.artifact_digest)
    receipt = {
        "actionsArtifact": {
            "digest": artifact_digest,
            "id": args.artifact_id,
            "name": args.artifact_name,
        },
        "completedAtUtc": utc_now(),
        "contractName": CONSUMPTION_CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "evidenceCapacityReceiptSha256": sha256_bytes(
            evidence_capacity_raw
        ),
        "evidenceRunner": evidence_runner,
        "github": github,
        "nonPublishing": {
            "countsAsPublicationEvidence": False,
            "protectedSecretsReferenced": False,
            "publicActivationAttempted": False,
            "publicationAttempted": False,
            "releaseUploadAttempted": False,
        },
        "sourceProof": {
            "artifact": source_lifecycle["build"]["artifact"],
            "authorityReceiptSha256": sha256_bytes(source_authority_raw),
            "capacityReceiptSha256": sha256_bytes(source_capacity_raw),
            "lifecycleReceiptSha256": sha256_bytes(source_lifecycle_raw),
            "releaseVersion": source_lifecycle["authority"][
                "releaseVersion"
            ],
            "rid": RID,
            "runner": source_runner,
        },
        "status": "pass",
    }
    write_json(args.output, receipt)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    run = subparsers.add_parser("run")
    run.add_argument("--capacity-receipt", type=Path, required=True)
    run.add_argument("--authority-receipt", type=Path, required=True)
    run.add_argument("--stage-root", type=Path, required=True)
    run.add_argument("--build-root", type=Path, required=True)
    run.add_argument("--output", type=Path, required=True)
    run.set_defaults(handler=command_run)

    verify = subparsers.add_parser("verify")
    verify.add_argument(
        "--source-capacity-receipt", type=Path, required=True
    )
    verify.add_argument(
        "--source-authority-receipt", type=Path, required=True
    )
    verify.add_argument(
        "--source-lifecycle-receipt", type=Path, required=True
    )
    verify.add_argument(
        "--evidence-capacity-receipt", type=Path, required=True
    )
    verify.add_argument("--artifact-id", required=True)
    verify.add_argument("--artifact-digest", required=True)
    verify.add_argument("--artifact-name", required=True)
    verify.add_argument("--output", type=Path, required=True)
    verify.set_defaults(handler=command_verify)
    return parser


def main() -> int:
    try:
        args = build_parser().parse_args()
        return int(args.handler(args))
    except (ProofFailure, capacity_probe.ProbeFailure) as error:
        code = getattr(error, "code", "capacity-rejected")
        print(f"hosted native proof rejected [{code}]: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
