#!/usr/bin/env python3
"""Assemble a three-platform flagship candidate without publishing it.

The assembler is deliberately local and two-phase:

* ``propose`` snapshots one immutable candidate, its Windows/Linux/macOS
  artifacts, the existing platform exit-gate receipts, applicable signing and
  notarization receipts, and platform-native E2E evidence.
* ``finalize`` authenticates three independent approvals over the exact
  proposal bytes and emits a final, still non-publishing, handoff receipt.

There is no upload, activation, deployment, release, or network code here.
Publication remains a separate transaction with its own authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import tempfile
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping, Sequence


CANDIDATE_CONTRACT = "chummer6-ui.global-flagship-candidate.v1"
PROPOSAL_CONTRACT = "chummer6-ui.global-flagship-release-proposal.v1"
FINAL_RECEIPT_CONTRACT = "chummer6-ui.global-flagship-release-final-receipt.v1"
APPROVAL_CONTRACT = "chummer6-ui.global-flagship-release-approval.v1"
SIGNING_CONTRACT = "chummer6-ui.desktop_artifact_signing"
CONTRACT_VERSION = 1

PLATFORMS = ("windows", "linux", "macos")
REQUIRED_APPROVAL_ROLES = ("quality", "release", "security")
APPROVAL_WORKFLOW = ".github/workflows/global-flagship-release-approval.yml"
APPROVAL_ENVIRONMENT = "global-flagship-release-review"
SOURCE_REPOSITORY = "ArchonMegalon/chummer6-ui"
DESKTOP_APP_KEY = "avalonia"
PASSING = frozenset({"pass", "passed"})
ALLOWED_SIDE_EFFECTS = ("write_local_receipts",)
AUTHORITY_LEVEL = "local-structural-validation-only"

MAX_JSON_BYTES = 4 * 1024 * 1024
MAX_EVIDENCE_BYTES = 2 * 1024 * 1024 * 1024
DEFAULT_MAX_EVIDENCE_AGE_SECONDS = 24 * 60 * 60
MAX_EVIDENCE_AGE_SECONDS = 7 * 24 * 60 * 60
DEFAULT_PROPOSAL_TTL_SECONDS = 4 * 60 * 60
MAX_PROPOSAL_TTL_SECONDS = 24 * 60 * 60
MAX_CLOCK_SKEW_SECONDS = 5 * 60

SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
ARTIFACT_ID_RE = re.compile(r"^[a-z0-9][a-z0-9._-]{0,127}$")
FILE_NAME_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,255}$")
GITHUB_LOGIN_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?"
    r"|github-actions\[bot\])$"
)
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
FULL_REF_RE = re.compile(
    r"^refs/(?:heads|tags)/[A-Za-z0-9][A-Za-z0-9._/@+-]{0,238}$"
)
WORKFLOW_RE = re.compile(
    r"^\.github/workflows/[A-Za-z0-9][A-Za-z0-9._-]{0,127}\.ya?ml$"
)
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
ZULU_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)


@dataclass(frozen=True)
class PlatformPolicy:
    rid: str
    artifact_id: str
    file_name: str
    exit_gate_contract: str
    native_e2e_contract: str
    signing_required: bool
    notarization_required: bool
    runner_os_prefix: str
    runner_arch: str


POLICIES: Mapping[str, PlatformPolicy] = {
    "windows": PlatformPolicy(
        rid="win-x64",
        artifact_id="avalonia-win-x64-installer",
        file_name="chummer-avalonia-win-x64-installer.exe",
        exit_gate_contract="chummer6-ui.windows_desktop_exit_gate",
        native_e2e_contract="chummer6-ui.flagship-native-e2e.windows.v1",
        signing_required=True,
        notarization_required=False,
        runner_os_prefix="windows",
        runner_arch="x64",
    ),
    "linux": PlatformPolicy(
        rid="linux-x64",
        artifact_id="avalonia-linux-x64-installer",
        file_name="chummer-avalonia-linux-x64-installer.deb",
        exit_gate_contract="chummer6-ui.linux_desktop_exit_gate",
        native_e2e_contract="chummer6-ui.flagship-native-e2e.linux.v1",
        signing_required=False,
        notarization_required=False,
        runner_os_prefix="linux",
        runner_arch="x64",
    ),
    "macos": PlatformPolicy(
        rid="osx-arm64",
        artifact_id="avalonia-osx-arm64-installer",
        file_name="chummer-avalonia-osx-arm64-installer.dmg",
        exit_gate_contract="chummer6-ui.macos_desktop_exit_gate",
        native_e2e_contract="chummer6-ui.flagship-native-e2e.macos.v1",
        signing_required=True,
        notarization_required=True,
        runner_os_prefix="macos",
        runner_arch="arm64",
    ),
}

EXTERNAL_REQUIREMENTS = (
    {
        "platform": "windows",
        "requirement": (
            "A native Windows runner plus DigiCert KeyLocker credentials and "
            "public signer pins must produce the existing signing and exit-gate "
            "contracts for the exact installer bytes."
        ),
    },
    {
        "platform": "linux",
        "requirement": (
            "A native Linux runner must perform rootless clean install, core "
            "workflow, dpkg package verification, and N-1 update execution."
        ),
    },
    {
        "platform": "macos",
        "requirement": (
            "A native macOS arm64 runner with a Developer ID identity and "
            "notary profile must sign, notarize, staple, clean-install, run the "
            "core workflow, and exercise the N-1 update."
        ),
    },
    {
        "platform": "global",
        "requirement": (
            "Quality, release, and security approvals must come from three "
            "different authorized actors, all independent of the candidate "
            "producer and native evidence actors."
        ),
    },
)


class ContractError(RuntimeError):
    """Raised when an authority or evidence binding fails closed."""


@dataclass(frozen=True)
class Snapshot:
    path: Path
    relative_path: str
    sha256: str
    size_bytes: int
    data: bytes | None


def fail(message: str) -> None:
    raise ContractError(message)


def exact_dict(
    value: object, keys: Iterable[str], label: str
) -> dict[str, Any]:
    expected = set(keys)
    if not isinstance(value, dict) or set(value) != expected:
        missing = sorted(expected - set(value) if isinstance(value, dict) else expected)
        extra = sorted(set(value) - expected if isinstance(value, dict) else set())
        fail(f"{label} has missing or extra fields (missing={missing}, extra={extra})")
    return value


def require_string(
    value: object, label: str, pattern: re.Pattern[str] | None = None
) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be a non-empty string")
    if pattern is not None and pattern.fullmatch(value) is None:
        fail(f"{label} has an invalid format")
    return value


def require_positive_integer(value: object, label: str) -> int:
    if isinstance(value, bool):
        fail(f"{label} must be a positive integer")
    if isinstance(value, int):
        result = value
    elif isinstance(value, str) and POSITIVE_INTEGER_RE.fullmatch(value):
        result = int(value)
    else:
        fail(f"{label} must be a positive integer")
    if result < 1 or result > 9_007_199_254_740_991:
        fail(f"{label} is outside the exact positive-integer range")
    return result


def require_sha256(value: object, label: str) -> str:
    return require_string(value, label, SHA256_RE)


def parse_time(value: object, label: str) -> datetime:
    raw = require_string(value, label, ZULU_RE)
    try:
        return datetime.strptime(raw, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=UTC)
    except ValueError:
        fail(f"{label} is not a real UTC timestamp")


def format_time(value: datetime) -> str:
    return value.astimezone(UTC).replace(microsecond=0).strftime(
        "%Y-%m-%dT%H:%M:%SZ"
    )


def validate_freshness(
    generated_at: object,
    *,
    now: datetime,
    max_age_seconds: int,
    label: str,
) -> str:
    generated = parse_time(generated_at, label)
    if generated > now + timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        fail(f"{label} is too far in the future")
    age = (now - generated).total_seconds()
    if age > max_age_seconds:
        fail(
            f"{label} is stale ({int(age)}s old; maximum is "
            f"{max_age_seconds}s)"
        )
    return format_time(generated)


def duplicate_rejecting_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate key {key!r}")
        result[key] = value
    return result


def load_json_bytes(data: bytes | None, label: str) -> dict[str, Any]:
    if data is None:
        fail(f"{label} was not materialized as bounded JSON")
    try:
        decoded = data.decode("utf-8")
    except UnicodeDecodeError:
        fail(f"{label} is not UTF-8 JSON")
    try:
        value = json.loads(
            decoded,
            object_pairs_hook=duplicate_rejecting_object,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite JSON token {token}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(value, dict):
        fail(f"{label} must contain a JSON object")
    return value


def snapshot_absolute(
    path: Path,
    label: str,
    max_bytes: int,
    *,
    read_data: bool = True,
) -> Snapshot:
    path = path.absolute()
    try:
        before = path.lstat()
    except OSError as exc:
        fail(f"{label} cannot be inspected: {path}: {exc}")
    if stat.S_ISLNK(before.st_mode) or not stat.S_ISREG(before.st_mode):
        fail(f"{label} must be a regular non-symlink file: {path}")
    if before.st_size < 1 or before.st_size > max_bytes:
        fail(f"{label} has an invalid byte size: {before.st_size}")
    data: bytes | None = None
    hasher = hashlib.sha256()
    size_bytes = 0
    try:
        with path.open("rb") as handle:
            opened = os.fstat(handle.fileno())
            if (
                opened.st_dev != before.st_dev
                or opened.st_ino != before.st_ino
                or not stat.S_ISREG(opened.st_mode)
            ):
                fail(f"{label} changed before it could be read")
            if read_data:
                data = handle.read(max_bytes + 1)
                size_bytes = len(data)
                hasher.update(data)
            else:
                for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                    size_bytes += len(chunk)
                    if size_bytes > max_bytes:
                        fail(f"{label} exceeds the {max_bytes}-byte limit")
                    hasher.update(chunk)
        after = path.lstat()
    except ContractError:
        raise
    except OSError as exc:
        fail(f"{label} cannot be read safely: {path}: {exc}")
    if size_bytes > max_bytes:
        fail(f"{label} exceeds the {max_bytes}-byte limit")
    identity_before = (
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
    )
    identity_after = (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
    )
    if identity_before != identity_after:
        fail(f"{label} changed while it was being read")
    if size_bytes != before.st_size:
        fail(f"{label} changed while it was being read")
    return Snapshot(
        path=path,
        relative_path=path.name,
        sha256=hasher.hexdigest(),
        size_bytes=size_bytes,
        data=data,
    )


def safe_relative_path(value: object, label: str) -> str:
    raw = require_string(value, label)
    if "\\" in raw or "\x00" in raw:
        fail(f"{label} must use a portable POSIX relative path")
    path = PurePosixPath(raw)
    if (
        path.is_absolute()
        or not path.parts
        or any(part in {"", ".", ".."} for part in path.parts)
        or path.as_posix() != raw
    ):
        fail(f"{label} must be a traversal-free relative path")
    return path.as_posix()


def snapshot_relative(
    root: Path,
    relative_path: object,
    label: str,
    max_bytes: int,
    *,
    read_data: bool = True,
) -> Snapshot:
    portable = safe_relative_path(relative_path, f"{label}.path")
    parts = PurePosixPath(portable).parts
    current = root.absolute()
    for part in parts:
        current = current / part
        try:
            mode = current.lstat().st_mode
        except OSError as exc:
            fail(f"{label} cannot be inspected: {portable}: {exc}")
        if stat.S_ISLNK(mode):
            fail(f"{label} path contains a symlink: {portable}")
    snapshot = snapshot_absolute(
        current, label, max_bytes, read_data=read_data
    )
    try:
        snapshot.path.relative_to(root.absolute())
    except ValueError:
        fail(f"{label} resolves outside the candidate root")
    return Snapshot(
        path=snapshot.path,
        relative_path=portable,
        sha256=snapshot.sha256,
        size_bytes=snapshot.size_bytes,
        data=snapshot.data,
    )


def validate_reference(
    root: Path,
    value: object,
    label: str,
    *,
    max_bytes: int,
    read_data: bool = True,
) -> Snapshot:
    reference = exact_dict(value, {"path", "sha256", "sizeBytes"}, label)
    expected_sha = require_sha256(reference["sha256"], f"{label}.sha256")
    expected_size = require_positive_integer(
        reference["sizeBytes"], f"{label}.sizeBytes"
    )
    snapshot = snapshot_relative(
        root,
        reference["path"],
        label,
        max_bytes=max_bytes,
        read_data=read_data,
    )
    if snapshot.sha256 != expected_sha:
        fail(f"{label} SHA-256 does not match the referenced bytes")
    if snapshot.size_bytes != expected_size:
        fail(f"{label} size does not match the referenced bytes")
    return snapshot


def binding(snapshot: Snapshot, **extra: object) -> dict[str, Any]:
    result: dict[str, Any] = {
        "relativePath": snapshot.relative_path,
        "sha256": snapshot.sha256,
        "sizeBytes": snapshot.size_bytes,
    }
    result.update(extra)
    return result


def receipt_contract(payload: Mapping[str, Any]) -> str:
    first = payload.get("contractName")
    second = payload.get("contract_name")
    if first is not None and second is not None and first != second:
        fail("receipt contractName and contract_name aliases disagree")
    return require_string(first or second, "receipt contract name")


def receipt_status(payload: Mapping[str, Any], label: str) -> str:
    value = require_string(payload.get("status"), f"{label}.status").lower()
    if value not in PASSING:
        fail(f"{label} did not pass")
    return value


def receipt_generated_at(
    payload: Mapping[str, Any],
    *,
    now: datetime,
    max_age_seconds: int,
    label: str,
) -> str:
    first = payload.get("generatedAt")
    second = payload.get("generated_at")
    if first is not None and second is not None and first != second:
        fail(f"{label} generatedAt aliases disagree")
    return validate_freshness(
        first or second,
        now=now,
        max_age_seconds=max_age_seconds,
        label=f"{label}.generatedAt",
    )


def require_equal(actual: object, expected: object, label: str) -> None:
    if type(actual) is not type(expected) or actual != expected:
        fail(f"{label} does not match the immutable candidate identity")


def validate_candidate_identity(value: object) -> dict[str, str]:
    candidate = exact_dict(
        value,
        {
            "candidateId",
            "generationId",
            "releaseVersion",
            "previousReleaseVersion",
            "sourceCommit",
        },
        "nativeE2E.candidate",
    )
    return {
        "candidateId": require_string(
            candidate["candidateId"], "candidateId", PORTABLE_RE
        ),
        "generationId": require_string(
            candidate["generationId"], "generationId", PORTABLE_RE
        ),
        "releaseVersion": require_string(
            candidate["releaseVersion"], "releaseVersion", PORTABLE_RE
        ),
        "previousReleaseVersion": require_string(
            candidate["previousReleaseVersion"],
            "previousReleaseVersion",
            PORTABLE_RE,
        ),
        "sourceCommit": require_string(
            candidate["sourceCommit"], "sourceCommit", COMMIT_RE
        ),
    }


def validate_exit_gate(
    payload: dict[str, Any],
    *,
    platform: str,
    policy: PlatformPolicy,
    artifact: Mapping[str, Any],
    release_version: str,
    channel_id: str,
    now: datetime,
    max_age_seconds: int,
) -> str:
    label = f"{platform} exit-gate receipt"
    if receipt_contract(payload) != policy.exit_gate_contract:
        fail(f"{label} uses the wrong platform contract")
    receipt_status(payload, label)
    generated_at = receipt_generated_at(
        payload, now=now, max_age_seconds=max_age_seconds, label=label
    )
    require_equal(payload.get("releaseVersion"), release_version, f"{label}.releaseVersion")
    require_equal(payload.get("channelId"), channel_id, f"{label}.channelId")
    head = payload.get("head")
    if not isinstance(head, dict):
        fail(f"{label}.head must be an object")
    require_equal(head.get("platform"), platform, f"{label}.head.platform")
    require_equal(head.get("rid"), policy.rid, f"{label}.head.rid")
    require_equal(head.get("app_key"), DESKTOP_APP_KEY, f"{label}.head.app_key")

    expected_sha = artifact["sha256"]
    expected_size = artifact["sizeBytes"]
    if platform == "windows":
        checks = payload.get("checks")
        proof_artifact = (
            checks.get("release_channel_windows_artifact")
            if isinstance(checks, dict)
            else None
        )
        sha_key, size_key = "sha256", "sizeBytes"
    elif platform == "linux":
        checks = payload.get("checks")
        proof_artifact = (
            checks.get("release_channel_linux_artifact")
            if isinstance(checks, dict)
            else None
        )
        sha_key, size_key = "sha256", "sizeBytes"
    else:
        proof_artifact = payload.get("artifact")
        sha_key, size_key = "installer_sha256", "installer_size_bytes"
    if not isinstance(proof_artifact, dict):
        fail(f"{label} does not expose its platform artifact binding")
    require_equal(proof_artifact.get(sha_key), expected_sha, f"{label} artifact SHA-256")
    require_equal(
        proof_artifact.get(size_key), expected_size, f"{label} artifact size"
    )
    return generated_at


def validate_signing_receipt(
    payload: dict[str, Any],
    *,
    platform: str,
    policy: PlatformPolicy,
    artifact: Mapping[str, Any],
    release_version: str,
    now: datetime,
    max_age_seconds: int,
) -> str:
    label = f"{platform} signing receipt"
    if receipt_contract(payload) != SIGNING_CONTRACT:
        fail(f"{label} does not use the existing desktop signing contract")
    require_equal(payload.get("contractVersion"), 2, f"{label}.contractVersion")
    require_equal(payload.get("platform"), platform, f"{label}.platform")
    require_equal(payload.get("rid"), policy.rid, f"{label}.rid")
    require_equal(payload.get("app"), DESKTOP_APP_KEY, f"{label}.app")
    require_equal(
        payload.get("releaseVersion"), release_version, f"{label}.releaseVersion"
    )
    if str(payload.get("signingStatus") or "").lower() not in PASSING:
        fail(f"{label} does not prove successful platform signing")
    if policy.notarization_required and str(
        payload.get("notarizationStatus") or ""
    ).lower() not in PASSING:
        fail(f"{label} does not prove successful notarization and stapling")
    artifacts = payload.get("artifacts")
    if not isinstance(artifacts, list):
        fail(f"{label}.artifacts must be a list")
    match = next(
        (
            item
            for item in artifacts
            if isinstance(item, dict)
            and item.get("fileName") == artifact["fileName"]
            and item.get("sha256") == artifact["sha256"]
        ),
        None,
    )
    if match is None:
        fail(f"{label} is not bound to the exact candidate artifact")
    if str(match.get("signingStatus") or "").lower() not in PASSING:
        fail(f"{label} artifact entry does not prove successful signing")
    if policy.notarization_required and str(
        match.get("notarizationStatus") or ""
    ).lower() not in PASSING:
        fail(f"{label} artifact entry does not prove successful notarization")
    return receipt_generated_at(
        payload, now=now, max_age_seconds=max_age_seconds, label=label
    )


def validate_native_e2e(
    payload: dict[str, Any],
    *,
    root: Path,
    platform: str,
    policy: PlatformPolicy,
    artifact: Mapping[str, Any],
    expected_identity: Mapping[str, str],
    source: Mapping[str, str],
    now: datetime,
    max_age_seconds: int,
) -> tuple[str, str, dict[str, Any]]:
    label = f"{platform} native E2E receipt"
    payload = exact_dict(
        payload,
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "status",
            "candidate",
            "platform",
            "rid",
            "artifact",
            "runner",
            "checks",
        },
        label,
    )
    require_equal(payload["contractName"], policy.native_e2e_contract, f"{label}.contractName")
    require_equal(payload["contractVersion"], 1, f"{label}.contractVersion")
    receipt_status(payload, label)
    generated_at = validate_freshness(
        payload["generatedAt"],
        now=now,
        max_age_seconds=max_age_seconds,
        label=f"{label}.generatedAt",
    )
    actual_identity = validate_candidate_identity(payload["candidate"])
    require_equal(actual_identity, dict(expected_identity), f"{label}.candidate")
    require_equal(payload["platform"], platform, f"{label}.platform")
    require_equal(payload["rid"], policy.rid, f"{label}.rid")

    actual_artifact = exact_dict(
        payload["artifact"],
        {"artifactId", "fileName", "sha256", "sizeBytes"},
        f"{label}.artifact",
    )
    for key in ("artifactId", "fileName", "sha256", "sizeBytes"):
        require_equal(
            actual_artifact.get(key), artifact[key], f"{label}.artifact.{key}"
        )

    runner = exact_dict(
        payload["runner"],
        {
            "repository",
            "workflow",
            "ref",
            "runId",
            "runAttempt",
            "actor",
            "os",
            "arch",
        },
        f"{label}.runner",
    )
    require_equal(runner["repository"], source["repository"], f"{label}.runner.repository")
    require_equal(runner["ref"], source["ref"], f"{label}.runner.ref")
    require_string(runner["workflow"], f"{label}.runner.workflow", WORKFLOW_RE)
    require_positive_integer(runner["runId"], f"{label}.runner.runId")
    require_positive_integer(runner["runAttempt"], f"{label}.runner.runAttempt")
    actor = require_string(runner["actor"], f"{label}.runner.actor", GITHUB_LOGIN_RE)
    runner_os = require_string(runner["os"], f"{label}.runner.os").lower()
    if not runner_os.startswith(policy.runner_os_prefix):
        fail(f"{label} was not captured on the required native operating system")
    require_equal(
        str(runner["arch"]).lower(), policy.runner_arch, f"{label}.runner.arch"
    )

    checks = exact_dict(
        payload["checks"],
        {"cleanInstall", "coreWorkflow", "nMinusOneUpdate"},
        f"{label}.checks",
    )
    clean = exact_dict(
        checks["cleanInstall"],
        {"status", "mode", "evidence"},
        f"{label}.checks.cleanInstall",
    )
    if str(clean["status"]).lower() not in PASSING or clean["mode"] != "clean":
        fail(f"{label} does not prove a clean native install")
    core = exact_dict(
        checks["coreWorkflow"],
        {"status", "scenario", "evidence"},
        f"{label}.checks.coreWorkflow",
    )
    if str(core["status"]).lower() not in PASSING:
        fail(f"{label} does not prove the native core workflow")
    require_string(core["scenario"], f"{label}.checks.coreWorkflow.scenario")
    update = exact_dict(
        checks["nMinusOneUpdate"],
        {
            "status",
            "fromReleaseVersion",
            "toReleaseVersion",
            "evidence",
        },
        f"{label}.checks.nMinusOneUpdate",
    )
    if str(update["status"]).lower() not in PASSING:
        fail(f"{label} does not prove the N-1 update")
    require_equal(
        update["fromReleaseVersion"],
        expected_identity["previousReleaseVersion"],
        f"{label} N-1 source version",
    )
    require_equal(
        update["toReleaseVersion"],
        expected_identity["releaseVersion"],
        f"{label} N-1 target version",
    )

    evidence_bindings: dict[str, Any] = {}
    for check_name, check in (
        ("cleanInstall", clean),
        ("coreWorkflow", core),
        ("nMinusOneUpdate", update),
    ):
        evidence = validate_reference(
            root,
            check["evidence"],
            f"{label}.{check_name}.evidence",
            max_bytes=MAX_EVIDENCE_BYTES,
            read_data=False,
        )
        evidence_bindings[check_name] = binding(evidence)
    return generated_at, actor, evidence_bindings


def validate_artifact(
    root: Path,
    value: object,
    platform: str,
    policy: PlatformPolicy,
) -> tuple[dict[str, Any], Snapshot]:
    label = f"candidate.platforms.{platform}.artifact"
    artifact = exact_dict(
        value,
        {"artifactId", "fileName", "path", "sha256", "sizeBytes"},
        label,
    )
    artifact_id = require_string(
        artifact["artifactId"], f"{label}.artifactId", ARTIFACT_ID_RE
    )
    file_name = require_string(
        artifact["fileName"], f"{label}.fileName", FILE_NAME_RE
    )
    require_equal(artifact_id, policy.artifact_id, f"{label}.artifactId")
    require_equal(file_name, policy.file_name, f"{label}.fileName")
    path = safe_relative_path(artifact["path"], f"{label}.path")
    if PurePosixPath(path).name != file_name:
        fail(f"{label}.fileName does not match the artifact path")
    expected_sha = require_sha256(artifact["sha256"], f"{label}.sha256")
    expected_size = require_positive_integer(artifact["sizeBytes"], f"{label}.sizeBytes")
    snapshot = snapshot_relative(
        root,
        path,
        label,
        max_bytes=MAX_EVIDENCE_BYTES,
        read_data=False,
    )
    if snapshot.sha256 != expected_sha or snapshot.size_bytes != expected_size:
        fail(f"{label} digest or size does not match the immutable bytes")
    projection = {
        "artifactId": artifact_id,
        "fileName": file_name,
        "sha256": expected_sha,
        "sizeBytes": expected_size,
    }
    return projection, snapshot


def validate_candidate(
    snapshot: Snapshot,
    *,
    now: datetime,
    max_age_seconds: int,
) -> tuple[dict[str, Any], dict[str, Any], set[str]]:
    payload = exact_dict(
        load_json_bytes(snapshot.data, "candidate manifest"),
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "expiresAt",
            "candidateId",
            "generationId",
            "releaseVersion",
            "previousReleaseVersion",
            "channelId",
            "source",
            "producer",
            "platforms",
        },
        "candidate manifest",
    )
    require_equal(payload["contractName"], CANDIDATE_CONTRACT, "candidate contractName")
    require_equal(payload["contractVersion"], CONTRACT_VERSION, "candidate contractVersion")
    generated_at = validate_freshness(
        payload["generatedAt"],
        now=now,
        max_age_seconds=max_age_seconds,
        label="candidate.generatedAt",
    )
    expires = parse_time(payload["expiresAt"], "candidate.expiresAt")
    generated = parse_time(generated_at, "candidate.generatedAt")
    if expires <= now:
        fail("candidate authority has expired")
    if expires > generated + timedelta(seconds=MAX_EVIDENCE_AGE_SECONDS):
        fail("candidate authority validity window is wider than seven days")

    identity = {
        "candidateId": require_string(
            payload["candidateId"], "candidate.candidateId", PORTABLE_RE
        ),
        "generationId": require_string(
            payload["generationId"], "candidate.generationId", PORTABLE_RE
        ),
        "releaseVersion": require_string(
            payload["releaseVersion"], "candidate.releaseVersion", PORTABLE_RE
        ),
        "previousReleaseVersion": require_string(
            payload["previousReleaseVersion"],
            "candidate.previousReleaseVersion",
            PORTABLE_RE,
        ),
    }
    if identity["releaseVersion"] == identity["previousReleaseVersion"]:
        fail("candidate releaseVersion must differ from previousReleaseVersion")
    channel_id = require_string(payload["channelId"], "candidate.channelId", PORTABLE_RE)

    source = exact_dict(
        payload["source"], {"repository", "ref", "commit"}, "candidate.source"
    )
    source_projection = {
        "repository": require_string(
            source["repository"], "candidate.source.repository", REPOSITORY_RE
        ),
        "ref": require_string(source["ref"], "candidate.source.ref", FULL_REF_RE),
        "commit": require_string(
            source["commit"], "candidate.source.commit", COMMIT_RE
        ),
    }
    require_equal(
        source_projection["repository"],
        SOURCE_REPOSITORY,
        "candidate.source.repository",
    )
    identity_with_commit = {**identity, "sourceCommit": source_projection["commit"]}

    producer = exact_dict(
        payload["producer"],
        {"actor", "workflow", "runId", "runAttempt"},
        "candidate.producer",
    )
    producer_projection = {
        "actor": require_string(
            producer["actor"], "candidate.producer.actor", GITHUB_LOGIN_RE
        ),
        "workflow": require_string(
            producer["workflow"], "candidate.producer.workflow", WORKFLOW_RE
        ),
        "runId": require_positive_integer(
            producer["runId"], "candidate.producer.runId"
        ),
        "runAttempt": require_positive_integer(
            producer["runAttempt"], "candidate.producer.runAttempt"
        ),
    }

    platforms = exact_dict(payload["platforms"], set(PLATFORMS), "candidate.platforms")
    platform_projections: dict[str, Any] = {}
    evidence_actors: set[str] = set()
    root = snapshot.path.parent
    for platform in PLATFORMS:
        policy = POLICIES[platform]
        platform_value = exact_dict(
            platforms[platform],
            {
                "rid",
                "artifact",
                "exitGateReceipt",
                "signingReceipt",
                "nativeE2eReceipt",
            },
            f"candidate.platforms.{platform}",
        )
        require_equal(
            platform_value["rid"], policy.rid, f"candidate.platforms.{platform}.rid"
        )
        artifact, artifact_snapshot = validate_artifact(
            root, platform_value["artifact"], platform, policy
        )

        exit_snapshot = validate_reference(
            root,
            platform_value["exitGateReceipt"],
            f"{platform} exit-gate receipt",
            max_bytes=MAX_JSON_BYTES,
        )
        exit_payload = load_json_bytes(exit_snapshot.data, f"{platform} exit-gate receipt")
        exit_generated_at = validate_exit_gate(
            exit_payload,
            platform=platform,
            policy=policy,
            artifact=artifact,
            release_version=identity["releaseVersion"],
            channel_id=channel_id,
            now=now,
            max_age_seconds=max_age_seconds,
        )

        signing_binding: dict[str, Any] | None
        signing_value = platform_value["signingReceipt"]
        if policy.signing_required:
            signing_snapshot = validate_reference(
                root,
                signing_value,
                f"{platform} signing receipt",
                max_bytes=MAX_JSON_BYTES,
            )
            signing_payload = load_json_bytes(
                signing_snapshot.data, f"{platform} signing receipt"
            )
            signing_generated_at = validate_signing_receipt(
                signing_payload,
                platform=platform,
                policy=policy,
                artifact=artifact,
                release_version=identity["releaseVersion"],
                now=now,
                max_age_seconds=max_age_seconds,
            )
            signing_binding = binding(
                signing_snapshot,
                contractName=SIGNING_CONTRACT,
                generatedAt=signing_generated_at,
            )
        else:
            if signing_value is not None:
                fail(
                    "linux signingReceipt must be null; direct .deb integrity is "
                    "bound by the manifest digest and native dpkg evidence"
                )
            signing_binding = None

        native_snapshot = validate_reference(
            root,
            platform_value["nativeE2eReceipt"],
            f"{platform} native E2E receipt",
            max_bytes=MAX_JSON_BYTES,
        )
        native_payload = load_json_bytes(
            native_snapshot.data, f"{platform} native E2E receipt"
        )
        native_generated_at, runner_actor, check_evidence = validate_native_e2e(
            native_payload,
            root=root,
            platform=platform,
            policy=policy,
            artifact=artifact,
            expected_identity=identity_with_commit,
            source=source_projection,
            now=now,
            max_age_seconds=max_age_seconds,
        )
        evidence_actors.add(runner_actor)
        platform_projections[platform] = {
            "rid": policy.rid,
            "artifact": binding(
                artifact_snapshot,
                artifactId=artifact["artifactId"],
                fileName=artifact["fileName"],
            ),
            "exitGateReceipt": binding(
                exit_snapshot,
                contractName=policy.exit_gate_contract,
                generatedAt=exit_generated_at,
            ),
            "signingReceipt": signing_binding,
            "nativeE2eReceipt": binding(
                native_snapshot,
                contractName=policy.native_e2e_contract,
                generatedAt=native_generated_at,
                runnerActor=runner_actor,
            ),
            "nativeE2eEvidence": check_evidence,
            "integrityPolicy": (
                "signed-authenticode-and-manifest-sha256"
                if platform == "windows"
                else "manifest-sha256-and-native-dpkg-verification"
                if platform == "linux"
                else "developer-id-signed-notarized-stapled-and-manifest-sha256"
            ),
        }

    candidate_projection = {
        **identity,
        "channelId": channel_id,
        "source": source_projection,
        "producer": producer_projection,
        "generatedAt": generated_at,
        "expiresAt": format_time(expires),
    }
    return candidate_projection, platform_projections, evidence_actors


def proposal_payload(
    candidate_snapshot: Snapshot,
    *,
    now: datetime,
    max_age_seconds: int,
    ttl_seconds: int,
) -> dict[str, Any]:
    candidate, platforms, evidence_actors = validate_candidate(
        candidate_snapshot, now=now, max_age_seconds=max_age_seconds
    )
    expires_at = now + timedelta(seconds=ttl_seconds)
    candidate_expiry = parse_time(candidate["expiresAt"], "candidate.expiresAt")
    expires_at = min(expires_at, candidate_expiry)
    if expires_at <= now:
        fail("proposal would not have a positive validity window")
    excluded_by_identity: dict[str, str] = {}
    for actor in (candidate["producer"]["actor"], *evidence_actors):
        excluded_by_identity.setdefault(actor.casefold(), actor)
    return {
        "contractName": PROPOSAL_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generatedAt": format_time(now),
        "expiresAt": format_time(expires_at),
        "status": "ready_for_independent_approval",
        "candidate": candidate,
        "candidateManifest": binding(candidate_snapshot),
        "platforms": platforms,
        "requiredApprovals": list(REQUIRED_APPROVAL_ROLES),
        "excludedApprovalActors": sorted(
            excluded_by_identity.values(), key=str.casefold
        ),
        "externalRequirements": list(EXTERNAL_REQUIREMENTS),
        "authorityLevel": AUTHORITY_LEVEL,
        "provenanceAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": list(ALLOWED_SIDE_EFFECTS),
    }


def validate_proposal(
    snapshot: Snapshot, *, now: datetime
) -> dict[str, Any]:
    payload = exact_dict(
        load_json_bytes(snapshot.data, "proposal"),
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "expiresAt",
            "status",
            "candidate",
            "candidateManifest",
            "platforms",
            "requiredApprovals",
            "excludedApprovalActors",
            "externalRequirements",
            "authorityLevel",
            "provenanceAuthenticated",
            "nonPublishing",
            "publicationAuthorized",
            "allowedSideEffects",
        },
        "proposal",
    )
    require_equal(payload["contractName"], PROPOSAL_CONTRACT, "proposal.contractName")
    require_equal(payload["contractVersion"], CONTRACT_VERSION, "proposal.contractVersion")
    require_equal(
        payload["status"], "ready_for_independent_approval", "proposal.status"
    )
    generated = parse_time(payload["generatedAt"], "proposal.generatedAt")
    if generated > now + timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        fail("proposal.generatedAt is too far in the future")
    if now - generated > timedelta(seconds=MAX_PROPOSAL_TTL_SECONDS):
        fail("proposal is older than the maximum proposal lifetime")
    expires = parse_time(payload["expiresAt"], "proposal.expiresAt")
    if expires <= now:
        fail("proposal has expired")
    if expires > generated + timedelta(seconds=MAX_PROPOSAL_TTL_SECONDS):
        fail("proposal validity window is wider than 24 hours")
    require_equal(
        payload["requiredApprovals"],
        list(REQUIRED_APPROVAL_ROLES),
        "proposal.requiredApprovals",
    )
    if payload["nonPublishing"] is not True:
        fail("proposal must remain non-publishing")
    if payload["publicationAuthorized"] is not False:
        fail("proposal must not authorize publication")
    require_equal(
        payload["allowedSideEffects"],
        list(ALLOWED_SIDE_EFFECTS),
        "proposal.allowedSideEffects",
    )
    require_equal(
        payload["externalRequirements"],
        list(EXTERNAL_REQUIREMENTS),
        "proposal.externalRequirements",
    )
    require_equal(
        payload["authorityLevel"], AUTHORITY_LEVEL, "proposal.authorityLevel"
    )
    if payload["provenanceAuthenticated"] is not False:
        fail("local proposal must not claim authenticated external provenance")
    candidate = payload["candidate"]
    if not isinstance(candidate, dict):
        fail("proposal.candidate must be an object")
    for key in (
        "candidateId",
        "generationId",
        "releaseVersion",
        "previousReleaseVersion",
        "source",
        "producer",
    ):
        if key not in candidate:
            fail(f"proposal.candidate.{key} is missing")
    if not isinstance(payload["platforms"], dict) or tuple(
        key for key in PLATFORMS if key in payload["platforms"]
    ) != PLATFORMS or set(payload["platforms"]) != set(PLATFORMS):
        fail("proposal.platforms must contain exactly Windows, Linux, and macOS")
    excluded = payload["excludedApprovalActors"]
    if (
        not isinstance(excluded, list)
        or not excluded
        or len({str(actor).casefold() for actor in excluded}) != len(excluded)
        or any(
            not isinstance(actor, str) or GITHUB_LOGIN_RE.fullmatch(actor) is None
            for actor in excluded
        )
    ):
        fail("proposal.excludedApprovalActors is invalid")
    return payload


def validate_approval(
    snapshot: Snapshot,
    *,
    proposal_snapshot: Snapshot,
    proposal: Mapping[str, Any],
    now: datetime,
) -> dict[str, Any]:
    label = f"approval {snapshot.path}"
    payload = exact_dict(
        load_json_bytes(snapshot.data, label),
        {
            "contractName",
            "contractVersion",
            "proposalSha256",
            "proposalSizeBytes",
            "candidateId",
            "generationId",
            "role",
            "decision",
            "approvedAt",
            "expiresAt",
            "actor",
            "authority",
        },
        label,
    )
    require_equal(payload["contractName"], APPROVAL_CONTRACT, f"{label}.contractName")
    require_equal(payload["contractVersion"], CONTRACT_VERSION, f"{label}.contractVersion")
    require_equal(
        payload["proposalSha256"], proposal_snapshot.sha256, f"{label}.proposalSha256"
    )
    require_equal(
        payload["proposalSizeBytes"],
        proposal_snapshot.size_bytes,
        f"{label}.proposalSizeBytes",
    )
    candidate = proposal["candidate"]
    require_equal(payload["candidateId"], candidate["candidateId"], f"{label}.candidateId")
    require_equal(
        payload["generationId"], candidate["generationId"], f"{label}.generationId"
    )
    role = require_string(payload["role"], f"{label}.role")
    if role not in REQUIRED_APPROVAL_ROLES:
        fail(f"{label}.role is not one of the required independent roles")
    require_equal(payload["decision"], "approve", f"{label}.decision")
    approved_at = parse_time(payload["approvedAt"], f"{label}.approvedAt")
    if approved_at > now + timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        fail(f"{label}.approvedAt is too far in the future")
    proposal_generated = parse_time(proposal["generatedAt"], "proposal.generatedAt")
    if approved_at < proposal_generated:
        fail(f"{label} predates the proposal")
    expires = parse_time(payload["expiresAt"], f"{label}.expiresAt")
    if expires <= now:
        fail(f"{label} has expired")
    if expires <= approved_at:
        fail(f"{label}.expiresAt must be later than approvedAt")
    if expires > parse_time(proposal["expiresAt"], "proposal.expiresAt"):
        fail(f"{label} outlives the proposal")
    actor = require_string(payload["actor"], f"{label}.actor", GITHUB_LOGIN_RE)
    if actor.casefold() in {
        str(excluded_actor).casefold()
        for excluded_actor in proposal["excludedApprovalActors"]
    }:
        fail(f"{label} actor is not independent of production/evidence actors")

    authority = exact_dict(
        payload["authority"],
        {
            "repository",
            "workflow",
            "ref",
            "runId",
            "runAttempt",
            "environment",
        },
        f"{label}.authority",
    )
    source = candidate["source"]
    require_equal(
        authority["repository"], source["repository"], f"{label}.authority.repository"
    )
    require_equal(authority["ref"], source["ref"], f"{label}.authority.ref")
    require_equal(
        authority["workflow"], APPROVAL_WORKFLOW, f"{label}.authority.workflow"
    )
    require_equal(
        authority["environment"], APPROVAL_ENVIRONMENT, f"{label}.authority.environment"
    )
    require_positive_integer(authority["runId"], f"{label}.authority.runId")
    require_positive_integer(
        authority["runAttempt"], f"{label}.authority.runAttempt"
    )
    return {
        "role": role,
        "actor": actor,
        "approvedAt": format_time(approved_at),
        "expiresAt": format_time(expires),
        "authority": authority,
        "receipt": binding(snapshot),
    }


def final_receipt_payload(
    proposal_snapshot: Snapshot,
    candidate_snapshot: Snapshot,
    approval_snapshots: Sequence[Snapshot],
    *,
    now: datetime,
    max_age_seconds: int,
) -> dict[str, Any]:
    proposal = validate_proposal(proposal_snapshot, now=now)
    expected_candidate_binding = proposal["candidateManifest"]
    if not isinstance(expected_candidate_binding, dict):
        fail("proposal.candidateManifest must be an object")
    require_equal(
        candidate_snapshot.relative_path,
        expected_candidate_binding.get("relativePath"),
        "finalization candidate manifest path",
    )
    require_equal(
        candidate_snapshot.sha256,
        expected_candidate_binding.get("sha256"),
        "finalization candidate manifest SHA-256",
    )
    require_equal(
        candidate_snapshot.size_bytes,
        expected_candidate_binding.get("sizeBytes"),
        "finalization candidate manifest size",
    )
    candidate, platforms, evidence_actors = validate_candidate(
        candidate_snapshot, now=now, max_age_seconds=max_age_seconds
    )
    require_equal(candidate, proposal["candidate"], "finalization candidate")
    require_equal(platforms, proposal["platforms"], "finalization platform bindings")
    expected_excluded_by_identity: dict[str, str] = {}
    for actor in (candidate["producer"]["actor"], *evidence_actors):
        expected_excluded_by_identity.setdefault(actor.casefold(), actor)
    expected_excluded_actors = sorted(
        expected_excluded_by_identity.values(), key=str.casefold
    )
    require_equal(
        expected_excluded_actors,
        proposal["excludedApprovalActors"],
        "finalization excluded approval actors",
    )
    approvals = [
        validate_approval(
            snapshot,
            proposal_snapshot=proposal_snapshot,
            proposal=proposal,
            now=now,
        )
        for snapshot in approval_snapshots
    ]
    roles = [approval["role"] for approval in approvals]
    if sorted(roles) != list(REQUIRED_APPROVAL_ROLES):
        fail(
            "finalization requires exactly one quality, release, and security "
            "approval"
        )
    actors = [approval["actor"] for approval in approvals]
    if len({actor.casefold() for actor in actors}) != len(actors):
        fail("quality, release, and security approvals require distinct actors")
    authorities = [
        (
            int(approval["authority"]["runId"]),
            int(approval["authority"]["runAttempt"]),
        )
        for approval in approvals
    ]
    if len(set(authorities)) != len(authorities):
        fail("independent approvals require distinct workflow runs")
    approvals.sort(key=lambda item: item["role"])
    return {
        "contractName": FINAL_RECEIPT_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generatedAt": format_time(now),
        "status": "passed",
        "candidate": proposal["candidate"],
        "candidateManifest": binding(candidate_snapshot),
        "proposal": binding(proposal_snapshot, contractName=PROPOSAL_CONTRACT),
        "platforms": proposal["platforms"],
        "approvals": approvals,
        "externalRequirements": proposal["externalRequirements"],
        "authorityLevel": AUTHORITY_LEVEL,
        "provenanceAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": list(ALLOWED_SIDE_EFFECTS),
        "handoff": {
            "eligibleForSeparatePublicationReview": True,
            "requiredNextAuthority": (
                "A protected workflow must authenticate every referenced "
                "GitHub run, artifact, signer identity, and approval actor via "
                "the provider API. A separate immutable publication "
                "transaction must then revalidate that authenticated handoff "
                "and all bound bytes before any upload or activation."
            ),
        },
    }


def blocked_payload(contract_name: str, now: datetime, message: str) -> dict[str, Any]:
    return {
        "contractName": contract_name,
        "contractVersion": CONTRACT_VERSION,
        "generatedAt": format_time(now),
        "status": "blocked",
        "blockers": [message],
        "externalRequirements": list(EXTERNAL_REQUIREMENTS),
        "authorityLevel": AUTHORITY_LEVEL,
        "provenanceAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": list(ALLOWED_SIDE_EFFECTS),
    }


def write_json_atomic(path: Path, payload: Mapping[str, Any]) -> None:
    target = path.absolute()
    target.parent.mkdir(parents=True, exist_ok=True)
    data = (
        json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=True) + "\n"
    ).encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{target.name}.", suffix=".tmp", dir=target.parent
    )
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(temporary_name, 0o644)
        os.replace(temporary_name, target)
    finally:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass


def current_time() -> datetime:
    """Return the production clock used for all freshness decisions."""

    return datetime.now(UTC).replace(microsecond=0)


def output_aliases_snapshot(output: Path, snapshots: Sequence[Snapshot]) -> bool:
    """Reject outputs that would replace an input, including hard-link aliases."""

    target = output.absolute()
    for snapshot in snapshots:
        if target == snapshot.path:
            return True
        try:
            if target.exists() and os.path.samefile(target, snapshot.path):
                return True
        except OSError as exc:
            fail(f"output path cannot be inspected safely: {target}: {exc}")
    return False


def command_propose(args: argparse.Namespace) -> int:
    now = current_time()
    output = Path(args.output)
    try:
        candidate = snapshot_absolute(
            Path(args.candidate), "candidate manifest", MAX_JSON_BYTES
        )
        if output_aliases_snapshot(output, (candidate,)):
            print(
                "global flagship proposal blocked: output aliases the candidate input",
                file=sys.stderr,
            )
            return 2
        candidate = Snapshot(
            path=candidate.path,
            relative_path=candidate.path.name,
            sha256=candidate.sha256,
            size_bytes=candidate.size_bytes,
            data=candidate.data,
        )
        payload = proposal_payload(
            candidate,
            now=now,
            max_age_seconds=DEFAULT_MAX_EVIDENCE_AGE_SECONDS,
            ttl_seconds=args.proposal_ttl_seconds,
        )
    except ContractError as exc:
        write_json_atomic(output, blocked_payload(PROPOSAL_CONTRACT, now, str(exc)))
        print(f"global flagship proposal blocked: {exc}", file=sys.stderr)
        return 1
    write_json_atomic(output, payload)
    print(output)
    return 0


def command_finalize(args: argparse.Namespace) -> int:
    now = current_time()
    output = Path(args.output)
    try:
        proposal = snapshot_absolute(Path(args.proposal), "proposal", MAX_JSON_BYTES)
        candidate = snapshot_absolute(
            Path(args.candidate), "candidate manifest", MAX_JSON_BYTES
        )
        approvals = [
            snapshot_absolute(Path(path), f"approval {path}", MAX_JSON_BYTES)
            for path in args.approval
        ]
        if output_aliases_snapshot(
            output, (proposal, candidate, *approvals)
        ):
            print(
                "global flagship finalization blocked: output aliases an authority input",
                file=sys.stderr,
            )
            return 2
        payload = final_receipt_payload(
            proposal,
            candidate,
            approvals,
            now=now,
            max_age_seconds=DEFAULT_MAX_EVIDENCE_AGE_SECONDS,
        )
    except ContractError as exc:
        write_json_atomic(
            output, blocked_payload(FINAL_RECEIPT_CONTRACT, now, str(exc))
        )
        print(f"global flagship finalization blocked: {exc}", file=sys.stderr)
        return 1
    write_json_atomic(output, payload)
    print(output)
    return 0


def bounded_integer(minimum: int, maximum: int):
    def parse(value: str) -> int:
        try:
            result = int(value)
        except ValueError:
            raise argparse.ArgumentTypeError("must be an integer")
        if result < minimum or result > maximum:
            raise argparse.ArgumentTypeError(
                f"must be between {minimum} and {maximum}"
            )
        return result

    return parse


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Assemble one immutable Windows/Linux/macOS flagship candidate "
            "into auditable local receipts. This command cannot publish."
        )
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    propose = subparsers.add_parser(
        "propose",
        help="validate and snapshot a candidate into a non-publishing proposal",
    )
    propose.add_argument("--candidate", required=True)
    propose.add_argument("--output", required=True)
    propose.add_argument(
        "--proposal-ttl-seconds",
        type=bounded_integer(1, MAX_PROPOSAL_TTL_SECONDS),
        default=DEFAULT_PROPOSAL_TTL_SECONDS,
    )
    propose.set_defaults(handler=command_propose)

    finalize = subparsers.add_parser(
        "finalize",
        help=(
            "bind three independent approvals to a proposal and emit a final "
            "non-publishing receipt"
        ),
    )
    finalize.add_argument("--proposal", required=True)
    finalize.add_argument(
        "--candidate",
        required=True,
        help="the same immutable candidate manifest used to create the proposal",
    )
    finalize.add_argument(
        "--approval",
        action="append",
        required=True,
        help="approval receipt; pass exactly once per required role",
    )
    finalize.add_argument("--output", required=True)
    finalize.set_defaults(handler=command_finalize)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return int(args.handler(args))


if __name__ == "__main__":
    raise SystemExit(main())
