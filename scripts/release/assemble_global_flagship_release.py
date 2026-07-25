#!/usr/bin/env python3
"""Assemble a three-platform flagship candidate without publishing it.

The assembler is deliberately local and staged:

* ``propose`` snapshots one immutable candidate, its Windows/Linux/macOS
  artifacts, the existing platform exit-gate receipts, applicable signing and
  notarization receipts, and platform-native E2E evidence.
* ``approve`` emits one protected-workflow approval over the exact proposal
  bytes after enforcing the role-specific, disjoint reviewer policy.
* ``finalize`` validates three independent approvals over the exact proposal
  bytes and emits a final, still non-publishing, handoff receipt.

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

SCRIPTS_DIRECTORY = Path(__file__).resolve().parents[1]
if str(SCRIPTS_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIRECTORY))

import desktop_native_lifecycle_evidence as desktop_lifecycle  # noqa: E402
import macos_flagship_evidence as macos_flagship  # noqa: E402


CANDIDATE_CONTRACT = "chummer6-ui.global-flagship-candidate.v1"
PROPOSAL_CONTRACT = "chummer6-ui.global-flagship-release-proposal.v1"
FINAL_RECEIPT_CONTRACT = "chummer6-ui.global-flagship-release-final-receipt.v1"
APPROVAL_CONTRACT = "chummer6-ui.global-flagship-release-approval.v2"
APPROVAL_CONTRACT_VERSION = 2
REVIEWER_POLICY_CONTRACT = (
    "chummer6-ui.global-flagship-release-reviewer-policy.v1"
)
SIGNING_CONTRACT = "chummer6-ui.desktop_artifact_signing"
CONTRACT_VERSION = 1

PLATFORMS = ("windows", "linux", "macos")
REQUIRED_APPROVAL_ROLES = ("quality", "release", "security")
APPROVAL_WORKFLOW = ".github/workflows/global-flagship-release-approval.yml"
CANDIDATE_PRODUCER_WORKFLOW = (
    ".github/workflows/global-flagship-candidate.yml"
)
APPROVAL_ENVIRONMENT = "global-flagship-release-review"
SOURCE_REPOSITORY = "ArchonMegalon/chummer6-ui"
RELEASE_APPROVAL_REF = "refs/heads/main"
DESKTOP_APP_KEY = "avalonia"
PASSING = frozenset({"pass", "passed"})
ALLOWED_SIDE_EFFECTS = ("write_local_receipts",)
AUTHORITY_LEVEL = "local-structural-validation-only"
RERUN_POLICY = "same-actor-only"
APPROVAL_RERUN_POLICY = "fresh-dispatch-only"
PROVIDER_ACTOR_ROLES = (
    "windows-export",
    "windows-capture",
    "windows-evidence",
    "linux-export",
    "linux-evidence",
    "macos-escrow",
    "macos-handoff",
)

MAX_JSON_BYTES = 4 * 1024 * 1024
MAX_REVIEWER_POLICY_BYTES = 64 * 1024
MAX_REVIEWERS_PER_ROLE = 32
MAX_EVIDENCE_BYTES = 2 * 1024 * 1024 * 1024
DEFAULT_MAX_EVIDENCE_AGE_SECONDS = 24 * 60 * 60
MAX_EVIDENCE_AGE_SECONDS = 7 * 24 * 60 * 60
DEFAULT_PROPOSAL_TTL_SECONDS = 4 * 60 * 60
MAX_PROPOSAL_TTL_SECONDS = 24 * 60 * 60
MAX_CLOCK_SKEW_SECONDS = 5 * 60

SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
CANDIDATE_PAYLOAD_ARTIFACT_RE = re.compile(
    r"^global-flagship-candidate-payload-"
    r"([A-Za-z0-9][A-Za-z0-9._+-]{0,127})-"
    r"([1-9][0-9]*)-1$"
)
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
    native_e2e_version: int
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
        native_e2e_contract="chummer6-ui.flagship-native-e2e.windows.v2",
        native_e2e_version=2,
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
        native_e2e_contract="chummer6-ui.flagship-native-e2e.linux.v2",
        native_e2e_version=2,
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
        native_e2e_version=1,
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
            "A native Linux runner with noninteractive system-package authority "
            "must perform a clean install, core workflow, dpkg package "
            "verification, N-1 update, and normal purge execution."
        ),
    },
    {
        "platform": "macos",
        "requirement": (
            "A native macOS arm64 runner with a Developer ID identity and "
            "notary profile must sign, notarize, staple, clean-install, run the "
            "core workflow, and exercise the N-1 update. The exact resulting "
            "DMG must be recovered from the pinned encrypted escrow only after "
            "a protected downstream workflow authenticates the GitHub run and "
            "artifact digest through the provider API."
        ),
    },
    {
        "platform": "global",
        "requirement": (
            "Quality, release, and security approvals must come from three "
            "different authorized actors, all independent of the candidate "
            "producer and native evidence actors. The separate publisher "
            "must authenticate detailed main-branch protection through a "
            "read-only administration authority unavailable to the approval "
            "workflow."
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


def candidate_payload_artifact_name(
    candidate_id: object, producer_run_id: object
) -> str:
    canonical_candidate_id = require_string(
        candidate_id, "candidate payload candidateId", PORTABLE_RE
    )
    canonical_run_id = require_positive_integer(
        producer_run_id, "candidate payload producer runId"
    )
    result = (
        "global-flagship-candidate-payload-"
        f"{canonical_candidate_id}-{canonical_run_id}-1"
    )
    match = CANDIDATE_PAYLOAD_ARTIFACT_RE.fullmatch(result)
    if (
        match is None
        or match.group(1) != canonical_candidate_id
        or int(match.group(2)) != canonical_run_id
    ):
        fail("candidate payload artifact name is not canonical")
    return result


def validate_candidate_payload_artifact_name(
    value: object,
    *,
    candidate_id: object,
    producer_run_id: object,
) -> str:
    artifact_name = require_string(
        value, "candidate producer artifactName"
    )
    expected = candidate_payload_artifact_name(
        candidate_id, producer_run_id
    )
    if artifact_name != expected:
        fail(
            "candidate producer artifactName does not match the exact "
            "candidate payload contract"
        )
    return artifact_name


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
) -> tuple[str, dict[str, Any]]:
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
    matches = [
        item
        for item in artifacts
        if isinstance(item, dict)
        and item.get("fileName") == artifact["fileName"]
        and item.get("sha256") == artifact["sha256"]
    ]
    if len(matches) != 1:
        fail(f"{label} is not bound to the exact candidate artifact")
    match = matches[0]
    if str(match.get("signingStatus") or "").lower() not in PASSING:
        fail(f"{label} artifact entry does not prove successful signing")
    if policy.notarization_required and str(
        match.get("notarizationStatus") or ""
    ).lower() not in PASSING:
        fail(f"{label} artifact entry does not prove successful notarization")
    authority_projection: dict[str, Any] = {}
    if platform == "windows":
        require_equal(
            payload.get("signingBackend"),
            "digicert_keylocker_linux_jsign",
            f"{label}.signingBackend",
        )
        require_equal(
            payload.get("digestAlgorithm"), "sha256", f"{label}.digestAlgorithm"
        )
        signer = payload.get("signer")
        if not isinstance(signer, dict):
            fail(f"{label}.signer must be an object")
        certificate_sha256 = require_sha256(
            signer.get("certificateSha256"),
            f"{label}.signer.certificateSha256",
        )
        spki_sha256 = require_sha256(
            signer.get("spkiSha256"), f"{label}.signer.spkiSha256"
        )
        timestamp = payload.get("timestamp")
        if (
            not isinstance(timestamp, dict)
            or timestamp.get("protocol") != "rfc3161"
            or timestamp.get("digestAlgorithm") != "sha256"
            or timestamp.get("status") != "verified"
        ):
            fail(f"{label} does not prove a verified RFC3161 timestamp")

        signatures = payload.get("artifactSignatures")
        if not isinstance(signatures, list):
            fail(f"{label}.artifactSignatures must be a list")
        signature_matches = [
            row
            for row in signatures
            if isinstance(row, dict)
            and row.get("artifactFileName") == artifact["fileName"]
            and row.get("artifactSha256") == artifact["sha256"]
        ]
        if len(signature_matches) != 1:
            fail(f"{label} must contain one exact candidate artifactSignature")
        signature = signature_matches[0]
        signature_signer = signature.get("signer")
        signer_chain = signature.get("signerChain")
        signature_timestamp = signature.get("timestamp")
        timestamp_chain = (
            signature_timestamp.get("chain")
            if isinstance(signature_timestamp, dict)
            else None
        )
        verifier = signature.get("verifier")
        if (
            signature.get("digestAlgorithm") != "sha256"
            or signature.get("cryptographicVerification") != "passed"
            or signature_signer != signer
            or not isinstance(signature_signer, dict)
            or signature_signer.get("certificateSha256")
            != certificate_sha256
            or signature_signer.get("spkiSha256") != spki_sha256
            or not isinstance(signer_chain, dict)
            or signer_chain.get("trusted") is not True
            or not isinstance(signature_timestamp, dict)
            or signature_timestamp.get("status") != "verified"
            or signature_timestamp.get("format") != "rfc3161"
            or signature_timestamp.get("digestAlgorithm") != "sha256"
            or not isinstance(timestamp_chain, dict)
            or timestamp_chain.get("trusted") is not True
            or not isinstance(verifier, dict)
            or verifier.get("providerIndependent") is not True
            or verifier.get("jsignOutputTrusted") is not False
        ):
            fail(f"{label} artifactSignature cryptographic evidence is invalid")
        exact_artifact_row = exact_dict(
            match,
            {"fileName", "sha256", "kind", "signingStatus"},
            f"{label}.artifacts candidate row",
        )
        require_equal(
            exact_artifact_row["kind"],
            "installer",
            f"{label}.artifacts candidate row kind",
        )
        authority_projection = {
            "signingBackend": "digicert_keylocker_linux_jsign",
            "signerCertificateSha256": certificate_sha256,
            "signerSpkiSha256": spki_sha256,
            "timestampProtocol": "rfc3161",
            "artifactSignature": {
                "artifactFileName": artifact["fileName"],
                "artifactSha256": artifact["sha256"],
                "cryptographicVerification": "passed",
                "providerIndependent": True,
            },
        }
    generated_at = receipt_generated_at(
        payload, now=now, max_age_seconds=max_age_seconds, label=label
    )
    return generated_at, authority_projection


def validate_desktop_lifecycle_evidence(
    *,
    lifecycle_snapshot: Snapshot,
    platform: str,
    policy: PlatformPolicy,
    artifact: Mapping[str, Any],
    expected_identity: Mapping[str, str],
    source: Mapping[str, str],
    adapter_generated_at: str,
    adapter_runner: Mapping[str, Any],
    signing_snapshot: Snapshot | None,
) -> dict[str, Any]:
    """Revalidate the rich Windows/Linux receipt behind a generic adapter."""

    label = f"{platform} rich lifecycle receipt"
    try:
        validated = desktop_lifecycle.validate_receipt(
            lifecycle_snapshot.path, lifecycle_snapshot.path.parent
        )
    except desktop_lifecycle.ContractError as exc:
        fail(f"{label} failed independent validation: {exc}")
    require_equal(validated.get("platform"), platform, f"{label}.platform")
    require_equal(validated.get("rid"), policy.rid, f"{label}.rid")
    require_equal(
        validated.get("receiptSha256"),
        lifecycle_snapshot.sha256,
        f"{label}.sha256",
    )
    require_equal(
        validated.get("receiptSizeBytes"),
        lifecycle_snapshot.size_bytes,
        f"{label}.sizeBytes",
    )
    receipt = validated.get("receipt")
    if not isinstance(receipt, dict):
        fail(f"{label} validator did not return the parsed receipt")
    require_equal(
        receipt.get("generatedAt"), adapter_generated_at, f"{label}.generatedAt"
    )

    candidate = receipt.get("candidate")
    previous = receipt.get("nMinusOne")
    native_runner = receipt.get("nativeRunner")
    if (
        not isinstance(candidate, dict)
        or not isinstance(previous, dict)
        or not isinstance(native_runner, dict)
    ):
        fail(f"{label} is missing candidate, N-1, or native runner authority")
    for key, expected in (
        ("artifactFileName", artifact["fileName"]),
        ("sha256", artifact["sha256"]),
        ("sizeBytes", artifact["sizeBytes"]),
        ("version", expected_identity["releaseVersion"]),
        ("sourceCommit", expected_identity["sourceCommit"]),
    ):
        require_equal(candidate.get(key), expected, f"{label}.candidate.{key}")
    require_equal(
        previous.get("version"),
        expected_identity["previousReleaseVersion"],
        f"{label}.nMinusOne.version",
    )

    rich_source = native_runner.get("source")
    if not isinstance(rich_source, dict):
        fail(f"{label}.nativeRunner.source must be an object")
    for key, expected in (
        ("repository", source["repository"]),
        ("workflow", adapter_runner["workflow"]),
        ("ref", source["ref"]),
        ("actor", adapter_runner["actor"]),
        ("triggeringActor", adapter_runner["triggeringActor"]),
        ("rerunPolicy", adapter_runner["rerunPolicy"]),
        ("sha", source["commit"]),
    ):
        require_equal(rich_source.get(key), expected, f"{label}.source.{key}")
    for key in ("runId", "runAttempt"):
        require_equal(
            rich_source.get(key),
            str(adapter_runner[key]),
            f"{label}.source.{key}",
        )

    package_authority = receipt.get("packageAuthority")
    if not isinstance(package_authority, dict):
        fail(f"{label}.packageAuthority must be an object")
    manifest = previous.get("manifestSha256")
    manifest_sha256 = require_sha256(manifest, f"{label}.nMinusOne.manifestSha256")
    projection_base = {
        "contractName": desktop_lifecycle.RECEIPT_CONTRACT,
        "generatedAt": receipt["generatedAt"],
        "receiptSha256": lifecycle_snapshot.sha256,
        "receiptSizeBytes": lifecycle_snapshot.size_bytes,
        "candidate": {
            "releaseVersion": candidate["version"],
            "sourceCommit": candidate["sourceCommit"],
        },
        "artifact": {
            "fileName": candidate["artifactFileName"],
            "sha256": candidate["sha256"],
            "sizeBytes": candidate["sizeBytes"],
        },
        "source": {
            "repository": rich_source["repository"],
            "workflow": rich_source["workflow"],
            "ref": rich_source["ref"],
            "commit": rich_source["sha"],
            "runId": int(adapter_runner["runId"]),
            "runAttempt": int(adapter_runner["runAttempt"]),
            "actor": rich_source["actor"],
            "triggeringActor": rich_source["triggeringActor"],
            "rerunPolicy": rich_source["rerunPolicy"],
        },
        "nMinusOne": {
            "releaseVersion": previous["version"],
            "generationId": previous["generationId"],
            "manifestSha256": manifest_sha256,
        },
    }
    live_authority = exact_dict(
        receipt.get("livePredecessorAuthority"),
        {
            "liveReleaseChannel",
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
            "url",
        },
        f"{label}.livePredecessorAuthority",
    )
    live_authority_projection = {
        "liveReleaseChannelSha256": require_sha256(
            live_authority["liveReleaseChannelSha256"],
            f"{label}.livePredecessorAuthority.liveReleaseChannelSha256",
        ),
        "nMinusOneReleaseSha256": require_sha256(
            live_authority["nMinusOneReleaseSha256"],
            f"{label}.livePredecessorAuthority.nMinusOneReleaseSha256",
        ),
        "selectedTupleSha256": require_sha256(
            live_authority["selectedTupleSha256"],
            f"{label}.livePredecessorAuthority.selectedTupleSha256",
        ),
        "url": live_authority["url"],
    }
    require_equal(
        live_authority_projection["url"],
        desktop_lifecycle.LIVE_RELEASE_CHANNEL_URL,
        f"{label}.livePredecessorAuthority.url",
    )
    projection_base["livePredecessorAuthority"] = (
        live_authority_projection
    )
    if platform == "windows":
        certificate_sha256 = require_sha256(
            package_authority.get("expectedSignerCertificateSha256"),
            f"{label}.packageAuthority.expectedSignerCertificateSha256",
        )
        spki_sha256 = require_sha256(
            package_authority.get("expectedSignerSpkiSha256"),
            f"{label}.packageAuthority.expectedSignerSpkiSha256",
        )
        candidate_authority = package_authority.get("candidate")
        signing_reference = (
            candidate_authority.get("signingReceipt")
            if isinstance(candidate_authority, dict)
            else None
        )
        signing_reference = exact_dict(
            signing_reference,
            {"path", "role", "sha256", "sizeBytes"},
            f"{label}.packageAuthority.candidate.signingReceipt",
        )
        if signing_snapshot is None:
            fail(f"{label} has no candidate signing receipt to bind")
        require_equal(
            signing_reference["sha256"],
            signing_snapshot.sha256,
            f"{label} candidate v2 signing receipt SHA-256",
        )
        require_equal(
            signing_reference["sizeBytes"],
            signing_snapshot.size_bytes,
            f"{label} candidate v2 signing receipt size",
        )
        relative_signing = safe_relative_path(
            signing_reference["path"],
            f"{label}.packageAuthority.candidate.signingReceipt.path",
        )
        rich_signing_path = lifecycle_snapshot.path.parent.joinpath(
            *PurePosixPath(relative_signing).parts
        )
        require_equal(
            os.path.abspath(rich_signing_path),
            os.path.abspath(signing_snapshot.path),
            f"{label} candidate v2 signing receipt path",
        )
        return {
            **projection_base,
            "packageAuthorityMode": package_authority["mode"],
            "signerCertificateSha256": certificate_sha256,
            "signerSpkiSha256": spki_sha256,
            "candidateSigningReceiptSha256": signing_snapshot.sha256,
        }

    require_equal(
        package_authority.get("manifestSha256"),
        manifest_sha256,
        f"{label}.packageAuthority.manifestSha256",
    )
    candidate_package = package_authority.get("candidate")
    previous_package = package_authority.get("nMinusOne")
    if not isinstance(candidate_package, dict) or not isinstance(
        previous_package, dict
    ):
        fail(f"{label} is missing Debian package authority")
    return {
        **projection_base,
        "packageAuthorityMode": package_authority["mode"],
        "candidatePackage": {
            key: candidate_package[key]
            for key in ("packageName", "packageVersion", "architecture")
        },
        "nMinusOnePackage": {
            key: previous_package[key]
            for key in ("packageName", "packageVersion", "architecture")
        },
    }


def validate_rich_native_evidence(
    *,
    root: Path,
    evidence_snapshots: Mapping[str, Snapshot],
    platform: str,
    policy: PlatformPolicy,
    artifact: Mapping[str, Any],
    expected_identity: Mapping[str, str],
    source: Mapping[str, str],
    adapter_generated_at: str,
    adapter_runner: Mapping[str, Any],
    signing_snapshot: Snapshot | None,
    now: datetime,
    max_age_seconds: int,
) -> dict[str, Any] | None:
    """Platform hook for evidence richer than the portable adapter contract."""

    snapshots = list(evidence_snapshots.values())
    first = snapshots[0]
    for name, snapshot in evidence_snapshots.items():
        if (
            snapshot.relative_path,
            snapshot.sha256,
            snapshot.size_bytes,
        ) != (first.relative_path, first.sha256, first.size_bytes):
            fail(
                f"{platform} native E2E adapter {name} evidence does not equal "
                "the shared rich lifecycle receipt"
            )
    if platform == "macos":
        aggregate_snapshot = snapshot_relative(
            root,
            first.relative_path,
            "macOS rich aggregate evidence",
            max_bytes=MAX_JSON_BYTES,
        )
        aggregate = load_json_bytes(
            aggregate_snapshot.data, "macOS rich aggregate evidence"
        )
        aggregate_generated_at = validate_freshness(
            aggregate.get("generatedAtUtc"),
            now=now,
            max_age_seconds=max_age_seconds,
            label="macOS rich aggregate evidence.generatedAtUtc",
        )
        adapter_time = parse_time(
            adapter_generated_at, "macOS native E2E adapter generatedAt"
        )
        aggregate_time = parse_time(
            aggregate_generated_at,
            "macOS rich aggregate evidence.generatedAtUtc",
        )
        if (
            aggregate_time > adapter_time + timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
            or adapter_time - aggregate_time
            > timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
        ):
            fail(
                "macOS rich aggregate evidence was not captured with the "
                "native E2E adapter"
            )

        references = aggregate.get("references")
        if (
            not isinstance(references, dict)
            or set(references) != macos_flagship.AGGREGATE_REFERENCE_KEYS
        ):
            fail("macOS rich aggregate evidence references are incomplete")
        reference_files: dict[str, bytes] = {}
        for key in sorted(macos_flagship.AGGREGATE_REFERENCE_KEYS):
            reference = references[key]
            reference_snapshot = validate_reference(
                root,
                reference,
                f"macOS rich aggregate evidence references.{key}",
                max_bytes=16 * 1024 * 1024,
            )
            if reference_snapshot.data is None:
                fail(f"macOS rich aggregate evidence reference {key} is empty")
            reference_files[reference_snapshot.relative_path] = (
                reference_snapshot.data
            )

        expected_github = {
            "actor": adapter_runner["actor"],
            "ref": source["ref"],
            "repository": source["repository"],
            "rerunPolicy": adapter_runner["rerunPolicy"],
            "runAttempt": adapter_runner["runAttempt"],
            "runId": adapter_runner["runId"],
            "sha": source["commit"],
            "triggeringActor": adapter_runner["triggeringActor"],
            "workflow": adapter_runner["workflow"],
        }
        try:
            projection = macos_flagship.validate_aggregate_receipt(
                aggregate,
                reference_files,
                expected_candidate={
                    key: artifact[key]
                    for key in (
                        "artifactId",
                        "fileName",
                        "sha256",
                        "sizeBytes",
                    )
                },
                expected_global_identity=dict(expected_identity),
                expected_github=expected_github,
            )
        except macos_flagship.ContractError as exc:
            fail(f"macOS rich aggregate evidence is invalid: {exc}")

        if signing_snapshot is None:
            fail("macOS rich aggregate evidence has no signing receipt")
        aggregate_signing = projection["references"]["signingReceipt"]
        require_equal(
            aggregate_signing["path"],
            signing_snapshot.relative_path,
            "macOS aggregate signing receipt path",
        )
        require_equal(
            aggregate_signing["sha256"],
            signing_snapshot.sha256,
            "macOS aggregate signing receipt SHA-256",
        )
        require_equal(
            aggregate_signing["sizeBytes"],
            signing_snapshot.size_bytes,
            "macOS aggregate signing receipt size",
        )
        return {
            "contractName": macos_flagship.EVIDENCE_CONTRACT,
            "contractVersion": macos_flagship.EVIDENCE_CONTRACT_VERSION,
            "aggregateSha256": aggregate_snapshot.sha256,
            "aggregateSizeBytes": aggregate_snapshot.size_bytes,
            "generatedAt": aggregate_generated_at,
            **projection,
        }

    if platform not in {"windows", "linux"}:
        return None
    return validate_desktop_lifecycle_evidence(
        lifecycle_snapshot=first,
        platform=platform,
        policy=policy,
        artifact=artifact,
        expected_identity=expected_identity,
        source=source,
        adapter_generated_at=adapter_generated_at,
        adapter_runner=adapter_runner,
        signing_snapshot=signing_snapshot,
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
    signing_snapshot: Snapshot | None,
    now: datetime,
    max_age_seconds: int,
) -> tuple[str, str, dict[str, Any], dict[str, Any] | None]:
    label = f"{platform} native E2E receipt"
    receipt_keys = {
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
    }
    receipt_keys.add("livePredecessorAuthority")
    payload = exact_dict(
        payload,
        receipt_keys,
        label,
    )
    require_equal(payload["contractName"], policy.native_e2e_contract, f"{label}.contractName")
    require_equal(
        payload["contractVersion"],
        policy.native_e2e_version,
        f"{label}.contractVersion",
    )
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

    runner_keys = {
        "repository",
        "workflow",
        "ref",
        "runId",
        "runAttempt",
        "actor",
        "triggeringActor",
        "rerunPolicy",
        "os",
        "arch",
    }
    if platform == "macos":
        runner_keys.update(
            {
                "environment",
                "imageOS",
                "imageVersion",
                "label",
            }
        )
    runner = exact_dict(
        payload["runner"],
        runner_keys,
        f"{label}.runner",
    )
    require_equal(runner["repository"], source["repository"], f"{label}.runner.repository")
    require_equal(runner["ref"], source["ref"], f"{label}.runner.ref")
    require_string(runner["workflow"], f"{label}.runner.workflow", WORKFLOW_RE)
    require_positive_integer(runner["runId"], f"{label}.runner.runId")
    require_positive_integer(runner["runAttempt"], f"{label}.runner.runAttempt")
    actor = require_string(runner["actor"], f"{label}.runner.actor", GITHUB_LOGIN_RE)
    triggering_actor = require_string(
        runner["triggeringActor"],
        f"{label}.runner.triggeringActor",
        GITHUB_LOGIN_RE,
    )
    require_equal(
        triggering_actor,
        actor,
        f"{label}.runner same-actor rerun policy",
    )
    require_equal(
        runner["rerunPolicy"],
        RERUN_POLICY,
        f"{label}.runner.rerunPolicy",
    )
    runner_os = require_string(runner["os"], f"{label}.runner.os").lower()
    if not runner_os.startswith(policy.runner_os_prefix):
        fail(f"{label} was not captured on the required native operating system")
    require_equal(
        str(runner["arch"]).lower(), policy.runner_arch, f"{label}.runner.arch"
    )
    if platform == "macos":
        require_equal(
            runner["environment"],
            "github-hosted",
            f"{label}.runner.environment",
        )
        require_equal(
            runner["label"], "macos-15", f"{label}.runner.label"
        )
        require_equal(
            runner["imageOS"], "macos15", f"{label}.runner.imageOS"
        )
        image_version = require_string(
            runner["imageVersion"], f"{label}.runner.imageVersion"
        )
        if re.fullmatch(r"[0-9A-Za-z._-]{1,128}", image_version) is None:
            fail(f"{label}.runner.imageVersion is invalid")

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
    evidence_snapshots: dict[str, Snapshot] = {}
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
        evidence_snapshots[check_name] = evidence
        evidence_bindings[check_name] = binding(evidence)
    rich_evidence = validate_rich_native_evidence(
        root=root,
        evidence_snapshots=evidence_snapshots,
        platform=platform,
        policy=policy,
        artifact=artifact,
        expected_identity=expected_identity,
        source=source,
        adapter_generated_at=generated_at,
        adapter_runner=runner,
        signing_snapshot=signing_snapshot,
        now=now,
        max_age_seconds=max_age_seconds,
    )
    adapter_live_authority = exact_dict(
        payload["livePredecessorAuthority"],
        {
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
            "url",
        },
        f"{label}.livePredecessorAuthority",
    )
    if (
        not isinstance(rich_evidence, dict)
        or rich_evidence.get("livePredecessorAuthority")
        != adapter_live_authority
    ):
        fail(
            f"{label} live-predecessor authority differs from rich evidence"
        )
    return generated_at, actor, evidence_bindings, rich_evidence


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
) -> tuple[dict[str, Any], dict[str, Any], tuple[str, ...]]:
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
            "providerActors",
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
    require_equal(
        source_projection["ref"],
        RELEASE_APPROVAL_REF,
        "candidate.source.ref",
    )
    identity_with_commit = {**identity, "sourceCommit": source_projection["commit"]}

    producer = exact_dict(
        payload["producer"],
        {"actor", "artifactName", "workflow", "runId", "runAttempt"},
        "candidate.producer",
    )
    producer_run_id = require_positive_integer(
        producer["runId"], "candidate.producer.runId"
    )
    producer_projection = {
        "actor": require_string(
            producer["actor"], "candidate.producer.actor", GITHUB_LOGIN_RE
        ),
        "artifactName": validate_candidate_payload_artifact_name(
            producer["artifactName"],
            candidate_id=identity["candidateId"],
            producer_run_id=producer_run_id,
        ),
        "workflow": require_string(
            producer["workflow"], "candidate.producer.workflow", WORKFLOW_RE
        ),
        "runId": producer_run_id,
        "runAttempt": require_positive_integer(
            producer["runAttempt"], "candidate.producer.runAttempt"
        ),
    }
    require_equal(
        producer_projection["workflow"],
        CANDIDATE_PRODUCER_WORKFLOW,
        "candidate.producer.workflow",
    )
    require_equal(
        producer_projection["runAttempt"],
        1,
        "candidate.producer.runAttempt",
    )

    provider_actors = exact_dict(
        payload["providerActors"],
        set(PROVIDER_ACTOR_ROLES),
        "candidate.providerActors",
    )
    provider_actor_projection = {
        role: require_string(
            provider_actors[role],
            f"candidate.providerActors.{role}",
            GITHUB_LOGIN_RE,
        )
        for role in PROVIDER_ACTOR_ROLES
    }
    producer_identity = producer_projection["actor"].casefold()
    if producer_identity in {
        actor.casefold() for actor in provider_actor_projection.values()
    }:
        fail(
            "candidate producer actor must be independent of all "
            "authenticated provider run actors"
        )
    evidence_actor_by_identity: dict[str, str] = {}
    for actor in provider_actor_projection.values():
        evidence_actor_by_identity.setdefault(actor.casefold(), actor)
    evidence_actors = tuple(evidence_actor_by_identity.values())

    platforms = exact_dict(payload["platforms"], set(PLATFORMS), "candidate.platforms")
    platform_projections: dict[str, Any] = {}
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
        signing_snapshot: Snapshot | None = None
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
            signing_generated_at, signing_authority = validate_signing_receipt(
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
                **signing_authority,
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
        (
            native_generated_at,
            runner_actor,
            check_evidence,
            rich_evidence,
        ) = validate_native_e2e(
            native_payload,
            root=root,
            platform=platform,
            policy=policy,
            artifact=artifact,
            expected_identity=identity_with_commit,
            source=source_projection,
            signing_snapshot=signing_snapshot,
            now=now,
            max_age_seconds=max_age_seconds,
        )
        expected_provider_role = {
            "windows": "windows-capture",
            "linux": "linux-evidence",
            "macos": "macos-escrow",
        }[platform]
        require_equal(
            runner_actor,
            provider_actor_projection[expected_provider_role],
            (
                f"{platform} native runner actor and authenticated "
                f"{expected_provider_role} actor"
            ),
        )
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
            "nativeLifecycleEvidence": rich_evidence,
            "integrityPolicy": (
                "signed-authenticode-and-manifest-sha256"
                if platform == "windows"
                else "manifest-sha256-and-native-dpkg-verification"
                if platform == "linux"
                else "developer-id-signed-notarized-stapled-and-manifest-sha256"
            ),
        }

    live_root_identities: dict[str, tuple[str, str]] = {}
    for platform in PLATFORMS:
        rich_evidence = platform_projections[platform][
            "nativeLifecycleEvidence"
        ]
        if not isinstance(rich_evidence, dict):
            fail(f"{platform} native lifecycle evidence is unavailable")
        authority = exact_dict(
            rich_evidence.get("livePredecessorAuthority"),
            {
                "liveReleaseChannelSha256",
                "nMinusOneReleaseSha256",
                "selectedTupleSha256",
                "url",
            },
            f"{platform} native lifecycle live-predecessor authority",
        )
        live_root_identities[platform] = (
            authority["url"],
            authority["liveReleaseChannelSha256"],
        )
    if len(set(live_root_identities.values())) != 1:
        fail(
            "all platform native lifecycle evidence must bind one exact "
            "live predecessor release-channel root"
        )

    candidate_projection = {
        **identity,
        "channelId": channel_id,
        "source": source_projection,
        "producer": producer_projection,
        "providerActors": provider_actor_projection,
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
    if expires <= generated:
        fail("proposal.expiresAt must be later than proposal.generatedAt")
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
        "providerActors",
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
    producer = exact_dict(
        candidate["producer"],
        {"actor", "artifactName", "workflow", "runId", "runAttempt"},
        "proposal.candidate.producer",
    )
    candidate_source = exact_dict(
        candidate["source"],
        {"repository", "ref", "commit"},
        "proposal.candidate.source",
    )
    require_equal(
        require_string(
            candidate_source["repository"],
            "proposal.candidate.source.repository",
            REPOSITORY_RE,
        ),
        SOURCE_REPOSITORY,
        "proposal.candidate.source.repository",
    )
    require_equal(
        require_string(
            candidate_source["ref"],
            "proposal.candidate.source.ref",
            FULL_REF_RE,
        ),
        RELEASE_APPROVAL_REF,
        "proposal.candidate.source.ref",
    )
    require_string(
        candidate_source["commit"],
        "proposal.candidate.source.commit",
        COMMIT_RE,
    )
    producer_actor = require_string(
        producer["actor"],
        "proposal.candidate.producer.actor",
        GITHUB_LOGIN_RE,
    )
    producer_run_id = require_positive_integer(
        producer["runId"], "proposal.candidate.producer.runId"
    )
    require_equal(
        require_string(
            producer["workflow"],
            "proposal.candidate.producer.workflow",
            WORKFLOW_RE,
        ),
        CANDIDATE_PRODUCER_WORKFLOW,
        "proposal.candidate.producer.workflow",
    )
    require_equal(
        require_positive_integer(
            producer["runAttempt"],
            "proposal.candidate.producer.runAttempt",
        ),
        1,
        "proposal.candidate.producer.runAttempt",
    )
    validate_candidate_payload_artifact_name(
        producer["artifactName"],
        candidate_id=candidate["candidateId"],
        producer_run_id=producer_run_id,
    )
    provider_actors = exact_dict(
        candidate["providerActors"],
        set(PROVIDER_ACTOR_ROLES),
        "proposal.candidate.providerActors",
    )
    expected_excluded_by_identity: dict[str, str] = {
        producer_actor.casefold(): producer_actor
    }
    for role in PROVIDER_ACTOR_ROLES:
        provider_actor = require_string(
            provider_actors[role],
            f"proposal.candidate.providerActors.{role}",
            GITHUB_LOGIN_RE,
        )
        if provider_actor.casefold() == producer_actor.casefold():
            fail(
                "proposal candidate producer overlaps an authenticated "
                "provider actor"
            )
        expected_excluded_by_identity.setdefault(
            provider_actor.casefold(), provider_actor
        )
    expected_excluded = sorted(
        expected_excluded_by_identity.values(), key=str.casefold
    )
    require_equal(
        excluded,
        expected_excluded,
        "proposal.excludedApprovalActors",
    )
    return payload


def validate_reviewer_policy(
    snapshot: Snapshot,
    *,
    role: str,
    actor: str,
    environment_approver: str,
) -> dict[str, Any]:
    label = "global flagship reviewer policy"
    payload = exact_dict(
        load_json_bytes(snapshot.data, label),
        {"contractName", "contractVersion", "roles"},
        label,
    )
    require_equal(
        payload["contractName"],
        REVIEWER_POLICY_CONTRACT,
        f"{label}.contractName",
    )
    require_equal(payload["contractVersion"], 1, f"{label}.contractVersion")
    roles = exact_dict(
        payload["roles"], set(REQUIRED_APPROVAL_ROLES), f"{label}.roles"
    )

    normalized_owners: dict[str, str] = {}
    role_members: dict[str, list[str]] = {}
    for reviewer_role in REQUIRED_APPROVAL_ROLES:
        members = roles[reviewer_role]
        if (
            not isinstance(members, list)
            or not members
            or len(members) > MAX_REVIEWERS_PER_ROLE
        ):
            fail(
                f"{label}.roles.{reviewer_role} must contain between 1 and "
                f"{MAX_REVIEWERS_PER_ROLE} GitHub logins"
            )
        validated_members: list[str] = []
        for index, member in enumerate(members):
            login = require_string(
                member,
                f"{label}.roles.{reviewer_role}[{index}]",
                GITHUB_LOGIN_RE,
            )
            if login.casefold() == "github-actions[bot]":
                fail(f"{label} cannot authorize an automation actor")
            normalized = login.casefold()
            prior_role = normalized_owners.get(normalized)
            if prior_role is not None:
                fail(
                    f"{label} reviewer {login!r} appears in both "
                    f"{prior_role} and {reviewer_role}"
                )
            normalized_owners[normalized] = reviewer_role
            validated_members.append(login)
        role_members[reviewer_role] = validated_members

    actor_role = normalized_owners.get(actor.casefold())
    if actor_role != role:
        fail(f"approval actor is not authorized for the {role} reviewer role")
    if normalized_owners.get(environment_approver.casefold()) is None:
        fail("environment approver is not authorized by the reviewer policy")
    if environment_approver.casefold() == actor.casefold():
        fail("environment approver must be independent of the approval actor")
    return {
        "contractName": REVIEWER_POLICY_CONTRACT,
        "sha256": snapshot.sha256,
        "sizeBytes": snapshot.size_bytes,
        "role": role,
        "actorAuthorized": True,
        "rolesDisjoint": True,
        "authorizedRoleMembers": len(role_members[role]),
    }


def approval_payload(
    proposal_snapshot: Snapshot,
    reviewer_policy_snapshot: Snapshot,
    *,
    expected_proposal_sha256: str,
    role: str,
    approval_confirmed: bool,
    repository: str,
    ref: str,
    sha: str,
    workflow_ref: str,
    workflow_sha: str,
    run_id: int,
    run_attempt: int,
    actor: str,
    triggering_actor: str,
    environment_approver: str,
    environment: str,
    now: datetime,
) -> dict[str, Any]:
    proposal = validate_proposal(proposal_snapshot, now=now)
    require_equal(
        require_sha256(
            expected_proposal_sha256, "expected proposal SHA-256"
        ),
        proposal_snapshot.sha256,
        "expected proposal SHA-256",
    )
    if role not in REQUIRED_APPROVAL_ROLES:
        fail("approval role is not one of quality, release, or security")
    if approval_confirmed is not True:
        fail("explicit approval confirmation is required")

    repository = require_string(
        repository, "approval authority repository", REPOSITORY_RE
    )
    ref = require_string(ref, "approval authority ref", FULL_REF_RE)
    sha = require_string(sha, "approval authority SHA", COMMIT_RE)
    workflow_sha = require_string(
        workflow_sha, "approval workflow SHA", COMMIT_RE
    )
    actor = require_string(actor, "approval actor", GITHUB_LOGIN_RE)
    triggering_actor = require_string(
        triggering_actor, "approval triggering actor", GITHUB_LOGIN_RE
    )
    environment_approver = require_string(
        environment_approver,
        "approval environment approver",
        GITHUB_LOGIN_RE,
    )
    if actor.casefold() == "github-actions[bot]":
        fail("an automation actor cannot grant a flagship release approval")
    if environment_approver.casefold() == "github-actions[bot]":
        fail("an automation actor cannot approve the protected environment")
    require_equal(
        triggering_actor.casefold(),
        actor.casefold(),
        "approval same-actor rerun policy",
    )
    require_equal(
        repository, SOURCE_REPOSITORY, "approval authority repository"
    )
    require_equal(ref, RELEASE_APPROVAL_REF, "approval authority ref")
    require_equal(
        environment, APPROVAL_ENVIRONMENT, "approval authority environment"
    )
    expected_workflow_ref = (
        f"{SOURCE_REPOSITORY}/{APPROVAL_WORKFLOW}@{RELEASE_APPROVAL_REF}"
    )
    require_equal(
        workflow_ref, expected_workflow_ref, "approval workflow ref"
    )
    require_equal(workflow_sha, sha, "approval workflow SHA")
    run_id = require_positive_integer(run_id, "approval authority runId")
    run_attempt = require_positive_integer(
        run_attempt, "approval authority runAttempt"
    )
    require_equal(
        run_attempt, 1, "approval authority fresh-dispatch runAttempt"
    )

    candidate = proposal["candidate"]
    candidate_id = require_string(
        candidate.get("candidateId"), "proposal.candidate.candidateId", PORTABLE_RE
    )
    generation_id = require_string(
        candidate.get("generationId"),
        "proposal.candidate.generationId",
        PORTABLE_RE,
    )
    source = exact_dict(
        candidate.get("source"),
        {"repository", "ref", "commit"},
        "proposal.candidate.source",
    )
    require_equal(
        source["repository"], repository, "approval candidate repository"
    )
    require_equal(source["ref"], ref, "approval candidate ref")
    require_equal(source["commit"], sha, "approval candidate source commit")
    excluded_actors = {
        str(excluded_actor).casefold()
        for excluded_actor in proposal["excludedApprovalActors"]
    }
    if actor.casefold() in excluded_actors:
        fail("approval actor is not independent of production/evidence actors")
    if environment_approver.casefold() in excluded_actors:
        fail(
            "environment approver is not independent of production/evidence "
            "actors"
        )

    reviewer_policy = validate_reviewer_policy(
        reviewer_policy_snapshot,
        role=role,
        actor=actor,
        environment_approver=environment_approver,
    )
    expires = parse_time(proposal["expiresAt"], "proposal.expiresAt")
    if expires <= now:
        fail("proposal has expired")
    return {
        "contractName": APPROVAL_CONTRACT,
        "contractVersion": APPROVAL_CONTRACT_VERSION,
        "proposalSha256": proposal_snapshot.sha256,
        "proposalSizeBytes": proposal_snapshot.size_bytes,
        "candidateId": candidate_id,
        "generationId": generation_id,
        "role": role,
        "decision": "approve",
        "approvalConfirmed": True,
        "approvedAt": format_time(now),
        "expiresAt": format_time(expires),
        "actor": actor,
        "triggeringActor": triggering_actor,
        "rerunPolicy": APPROVAL_RERUN_POLICY,
        "environmentApproval": {
            "state": "approved",
            "reviewer": environment_approver,
        },
        "reviewerPolicy": reviewer_policy,
        "authority": {
            "repository": repository,
            "workflow": APPROVAL_WORKFLOW,
            "ref": ref,
            "sha": sha,
            "runId": run_id,
            "runAttempt": run_attempt,
            "environment": environment,
        },
    }


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
            "approvalConfirmed",
            "approvedAt",
            "expiresAt",
            "actor",
            "triggeringActor",
            "rerunPolicy",
            "environmentApproval",
            "reviewerPolicy",
            "authority",
        },
        label,
    )
    require_equal(payload["contractName"], APPROVAL_CONTRACT, f"{label}.contractName")
    require_equal(
        payload["contractVersion"],
        APPROVAL_CONTRACT_VERSION,
        f"{label}.contractVersion",
    )
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
    if payload["approvalConfirmed"] is not True:
        fail(f"{label} does not contain an explicit approval confirmation")
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
    if actor.casefold() == "github-actions[bot]":
        fail(f"{label} cannot be granted by an automation actor")
    triggering_actor = require_string(
        payload["triggeringActor"],
        f"{label}.triggeringActor",
        GITHUB_LOGIN_RE,
    )
    require_equal(
        triggering_actor.casefold(),
        actor.casefold(),
        f"{label} same-actor rerun policy",
    )
    require_equal(
        payload["rerunPolicy"],
        APPROVAL_RERUN_POLICY,
        f"{label}.rerunPolicy",
    )
    environment_approval = exact_dict(
        payload["environmentApproval"],
        {"state", "reviewer"},
        f"{label}.environmentApproval",
    )
    require_equal(
        environment_approval["state"],
        "approved",
        f"{label}.environmentApproval.state",
    )
    environment_approver = require_string(
        environment_approval["reviewer"],
        f"{label}.environmentApproval.reviewer",
        GITHUB_LOGIN_RE,
    )
    if environment_approver.casefold() == "github-actions[bot]":
        fail(f"{label} environment approval cannot come from automation")
    if environment_approver.casefold() == actor.casefold():
        fail(f"{label} environment approver must differ from approval actor")
    excluded_actors = {
        str(excluded_actor).casefold()
        for excluded_actor in proposal["excludedApprovalActors"]
    }
    if actor.casefold() in excluded_actors:
        fail(f"{label} actor is not independent of production/evidence actors")
    if environment_approver.casefold() in excluded_actors:
        fail(
            f"{label} environment approver is not independent of "
            "production/evidence actors"
        )

    reviewer_policy = exact_dict(
        payload["reviewerPolicy"],
        {
            "contractName",
            "sha256",
            "sizeBytes",
            "role",
            "actorAuthorized",
            "rolesDisjoint",
            "authorizedRoleMembers",
        },
        f"{label}.reviewerPolicy",
    )
    require_equal(
        reviewer_policy["contractName"],
        REVIEWER_POLICY_CONTRACT,
        f"{label}.reviewerPolicy.contractName",
    )
    require_sha256(
        reviewer_policy["sha256"], f"{label}.reviewerPolicy.sha256"
    )
    reviewer_policy_size = require_positive_integer(
        reviewer_policy["sizeBytes"], f"{label}.reviewerPolicy.sizeBytes"
    )
    if reviewer_policy_size > MAX_REVIEWER_POLICY_BYTES:
        fail(f"{label}.reviewerPolicy exceeds the policy byte limit")
    require_equal(
        reviewer_policy["role"], role, f"{label}.reviewerPolicy.role"
    )
    if (
        reviewer_policy["actorAuthorized"] is not True
        or reviewer_policy["rolesDisjoint"] is not True
    ):
        fail(f"{label}.reviewerPolicy does not prove a disjoint role authority")
    authorized_role_members = require_positive_integer(
        reviewer_policy["authorizedRoleMembers"],
        f"{label}.reviewerPolicy.authorizedRoleMembers",
    )
    if authorized_role_members > MAX_REVIEWERS_PER_ROLE:
        fail(f"{label}.reviewerPolicy authorizes too many role members")

    authority = exact_dict(
        payload["authority"],
        {
            "repository",
            "workflow",
            "ref",
            "sha",
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
        authority["ref"], RELEASE_APPROVAL_REF, f"{label}.authority.ref"
    )
    require_equal(
        authority["sha"], source["commit"], f"{label}.authority.sha"
    )
    require_string(authority["sha"], f"{label}.authority.sha", COMMIT_RE)
    require_equal(
        authority["workflow"], APPROVAL_WORKFLOW, f"{label}.authority.workflow"
    )
    require_equal(
        authority["environment"], APPROVAL_ENVIRONMENT, f"{label}.authority.environment"
    )
    require_positive_integer(authority["runId"], f"{label}.authority.runId")
    authority_run_attempt = require_positive_integer(
        authority["runAttempt"], f"{label}.authority.runAttempt"
    )
    require_equal(
        authority_run_attempt,
        1,
        f"{label}.authority fresh-dispatch runAttempt",
    )
    return {
        "role": role,
        "actor": actor,
        "triggeringActor": triggering_actor,
        "rerunPolicy": APPROVAL_RERUN_POLICY,
        "environmentApproval": {
            "state": "approved",
            "reviewer": environment_approver,
        },
        "approvedAt": format_time(approved_at),
        "expiresAt": format_time(expires),
        "reviewerPolicy": reviewer_policy,
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
    run_ids = [
        int(approval["authority"]["runId"]) for approval in approvals
    ]
    if len(set(run_ids)) != len(run_ids):
        fail("independent approvals require distinct workflow runs")
    reviewer_policies = {
        (
            approval["reviewerPolicy"]["sha256"],
            int(approval["reviewerPolicy"]["sizeBytes"]),
        )
        for approval in approvals
    }
    if len(reviewer_policies) != 1:
        fail("independent approvals require one exact reviewer policy")
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


def blocked_payload(
    contract_name: str,
    now: datetime,
    message: str,
    *,
    contract_version: int = CONTRACT_VERSION,
) -> dict[str, Any]:
    return {
        "contractName": contract_name,
        "contractVersion": contract_version,
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


def command_approve(args: argparse.Namespace) -> int:
    now = current_time()
    output = Path(args.output)
    try:
        proposal = snapshot_absolute(
            Path(args.proposal), "proposal", MAX_JSON_BYTES
        )
        reviewer_policy = snapshot_absolute(
            Path(args.reviewer_policy),
            "global flagship reviewer policy",
            MAX_REVIEWER_POLICY_BYTES,
        )
        if output_aliases_snapshot(output, (proposal, reviewer_policy)):
            print(
                "global flagship approval blocked: output aliases an authority input",
                file=sys.stderr,
            )
            return 2
        payload = approval_payload(
            proposal,
            reviewer_policy,
            expected_proposal_sha256=args.expected_proposal_sha256,
            role=args.role,
            approval_confirmed=args.approval_confirmed == "true",
            repository=args.repository,
            ref=args.ref,
            sha=args.sha,
            workflow_ref=args.workflow_ref,
            workflow_sha=args.workflow_sha,
            run_id=args.run_id,
            run_attempt=args.run_attempt,
            actor=args.actor,
            triggering_actor=args.triggering_actor,
            environment_approver=args.environment_approver,
            environment=args.environment,
            now=now,
        )
    except ContractError as exc:
        write_json_atomic(
            output,
            blocked_payload(
                APPROVAL_CONTRACT,
                now,
                str(exc),
                contract_version=APPROVAL_CONTRACT_VERSION,
            ),
        )
        print(f"global flagship approval blocked: {exc}", file=sys.stderr)
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

    approve = subparsers.add_parser(
        "approve",
        help=(
            "emit one non-publishing approval from the protected review "
            "workflow"
        ),
    )
    approve.add_argument("--proposal", required=True)
    approve.add_argument("--reviewer-policy", required=True)
    approve.add_argument("--expected-proposal-sha256", required=True)
    approve.add_argument(
        "--role", choices=REQUIRED_APPROVAL_ROLES, required=True
    )
    approve.add_argument(
        "--approval-confirmed", choices=("true", "false"), required=True
    )
    approve.add_argument("--repository", required=True)
    approve.add_argument("--ref", required=True)
    approve.add_argument("--sha", required=True)
    approve.add_argument("--workflow-ref", required=True)
    approve.add_argument("--workflow-sha", required=True)
    approve.add_argument(
        "--run-id",
        type=bounded_integer(1, 9_007_199_254_740_991),
        required=True,
    )
    approve.add_argument(
        "--run-attempt",
        type=bounded_integer(1, 9_007_199_254_740_991),
        required=True,
    )
    approve.add_argument("--actor", required=True)
    approve.add_argument("--triggering-actor", required=True)
    approve.add_argument("--environment-approver", required=True)
    approve.add_argument("--environment", required=True)
    approve.add_argument("--output", required=True)
    approve.set_defaults(handler=command_approve)

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
