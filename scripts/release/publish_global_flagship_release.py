#!/usr/bin/env python3
"""Execute the final protected global-flagship publication transaction.

This module deliberately re-opens every local byte accepted by the candidate
assembler.  A provider-authenticated approval handoff is necessary, but never
sufficient: the transaction independently hashes the three installers and all
signing, notarization, native-lifecycle, proposal, and final-receipt evidence.

The only production mutation is delegated to the repository's canonical HTTP
publisher.  The transaction emits ``publicationAuthorized: true`` only after
the exact public manifest and all three public installer bytes have been read
back without redirects and matched byte-for-byte.
"""

from __future__ import annotations

import argparse
import hashlib
import hmac
import json
import os
import re
import stat
import subprocess
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Mapping, Protocol, Sequence
from urllib.parse import quote, urlsplit

import assemble_global_flagship_release as assembler
import authenticate_global_flagship_release as provider_auth


CONTRACT = "chummer6-ui.global-flagship-publication-receipt.v1"
CONTRACT_VERSION = 1
JOURNAL_CONTRACT = "chummer6-ui.global-flagship-publication-journal.v1"
JOURNAL_CONTRACT_VERSION = 1
TOPOLOGY_CONTRACT = "chummer6-hub.topology-b-committed-retirement.v1"
DESTINATION_PLAN_CONTRACT = (
    "chummer6-ui.global-flagship-publication-destination-plan.v1"
)
PROVIDER_HANDOFF_WORKFLOW = (
    ".github/workflows/global-flagship-provider-authentication.yml"
)
PUBLICATION_WORKFLOW = (
    ".github/workflows/global-flagship-protected-publication.yml"
)
ASSEMBLY_WORKFLOW = (
    ".github/workflows/global-flagship-publication-input-assembly.yml"
)
PUBLICATION_ENVIRONMENT = "global-flagship-protected-publication"
ASSEMBLY_ENVIRONMENT = "global-flagship-publication-input-assembly"
ASSEMBLY_CONTRACT = (
    "chummer6-ui.global-flagship-publication-input-assembly.v1"
)
ASSEMBLY_CONTRACT_VERSION = 1
ASSEMBLY_RECEIPT_NAME = "publication-input-assembly-receipt.json"
ASSEMBLY_ARTIFACT_RE = re.compile(
    r"^global-flagship-publication-input-"
    r"([A-Za-z0-9][A-Za-z0-9._+-]{0,127})-([1-9][0-9]*)-1$"
)
CANDIDATE_PAYLOAD_ARTIFACT_RE = re.compile(
    r"^global-flagship-candidate-payload-"
    r"([A-Za-z0-9][A-Za-z0-9._+-]{0,127})-([1-9][0-9]*)-1$"
)
PROVIDER_HANDOFF_ARCHIVE_ENTRIES = {"handoff.json"}
CANONICAL_PUBLISHER = "scripts/publish-download-bundle-http.sh"
PUBLIC_BASE_URL = "https://chummer.run/downloads"
PUBLIC_MANIFEST_URL = (
    "https://chummer.run/downloads/RELEASE_CHANNEL.generated.json"
)
PUBLIC_RELEASES_URL = "https://chummer.run/downloads/releases.json"
PUBLIC_TOPOLOGY_PROOF_URL = (
    "https://chummer.run/downloads/TOPOLOGY_B_RETIREMENT.generated.json"
)
PUBLICATION_INPUT_PREFIX = "global-flagship-publication-input-"
HUB_REPOSITORY = "ArchonMegalon/chummer6-hub"
HUB_PROOF_WORKFLOW = (
    ".github/workflows/topology-b-committed-retirement-proof.yml"
)
HUB_PROOF_ARTIFACT_RE = re.compile(
    r"^topology-b-committed-retirement-proof-([1-9][0-9]*)-1$"
)
HUB_PROOF_ENTRIES = {
    "TOPOLOGY_B_RETIREMENT.generated.json",
    "committed-boundary-receipt.json",
    "post-marker-convergence-receipt.json",
}
HUB_TERMINAL_CONTRACT = "chummer.public-download-committed-retirement/v1"
HUB_TERMINAL_OPERATION = (
    "initial-release-shelf-public-download-cutover-retire"
)
HUB_TERMINAL_FIELDS = {
    "contractName",
    "status",
    "operation",
    "operationRoot",
    "projectName",
    "operationSourceHead",
    "controllerSourceHead",
    "retiredAuthorityPath",
    "retiredAuthoritySha256",
    "retirementEvidencePath",
    "retirementEvidenceSha256",
    "connectorGateSha256",
    "postMarkerConnectorGateSha256",
    "latestConnectorGateSha256",
    "priorConfigSha256",
    "restoredVersion",
    "incumbentBaselineSha256",
    "incumbentObservationSha256",
    "cleanupSha256",
    "completedAtUtc",
}
HUB_POST_MARKER_CONTRACT = (
    "chummer.public-download-retirement-connector-boundary/v1"
)
HUB_POST_MARKER_FIELDS = {
    "contractName",
    "status",
    "boundary",
    "operationRoot",
    "restoredVersion",
    "retiredAuthoritySha256",
    "markerConnectorGateSha256",
    "connectorConvergence",
    "connectorConvergenceSha256",
    "verifiedAtUtc",
}
MAX_HUB_PROOF_ARTIFACT_BYTES = 16 * 1024 * 1024
HANDOFF_ARTIFACT_RE = re.compile(
    r"^global-flagship-provider-authenticated-handoff-"
    r"([1-9][0-9]*)-([1-9][0-9]*)-1$"
)
MAX_TOPOLOGY_AGE = timedelta(hours=24)
MAX_JSON_BYTES = 4 * 1024 * 1024
MAX_PUBLIC_FILE_BYTES = 2 * 1024 * 1024 * 1024
MAX_PUBLICATION_INPUT_ARTIFACT_BYTES = 4 * 1024 * 1024 * 1024
HTTP_TIMEOUT_SECONDS = 60


class ContractError(RuntimeError):
    """Raised when the final publication boundary cannot be proven."""


def fail(message: str) -> None:
    raise ContractError(message)


def current_time() -> datetime:
    return datetime.now(UTC).replace(microsecond=0)


def read_clock(clock: Callable[[], datetime], label: str) -> datetime:
    value = clock()
    if value.tzinfo is None or value.utcoffset() is None:
        fail(f"{label} clock must return a timezone-aware UTC instant")
    return value.astimezone(UTC).replace(microsecond=0)


def require_equal(actual: object, expected: object, label: str) -> None:
    if type(actual) is not type(expected) or actual != expected:
        fail(f"{label} does not match the protected publication authority")


def exact_dict(
    value: object, keys: set[str], label: str
) -> dict[str, Any]:
    return assembler.exact_dict(value, keys, label)


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    value = provider_auth.json_load_any(data, label)
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    return value


def read_regular_file(path: Path, label: str, maximum: int) -> bytes:
    try:
        before = path.lstat()
    except OSError as exc:
        fail(f"{label} cannot be inspected: {exc}")
    if not stat.S_ISREG(before.st_mode) or path.is_symlink():
        fail(f"{label} must be a regular non-symlink file")
    if before.st_size < 1 or before.st_size > maximum:
        fail(f"{label} has an invalid size")
    try:
        data = path.read_bytes()
        after = path.lstat()
    except OSError as exc:
        fail(f"{label} cannot be read: {exc}")
    identity = lambda row: (  # noqa: E731 - compact immutable identity
        row.st_dev,
        row.st_ino,
        row.st_mode,
        row.st_size,
        row.st_mtime_ns,
    )
    if identity(before) != identity(after) or len(data) != before.st_size:
        fail(f"{label} changed while it was read")
    return data


def binding_bytes(
    data: bytes, relative_path: str, **extra: object
) -> dict[str, Any]:
    return {
        "relativePath": assembler.safe_relative_path(
            relative_path, "publication binding path"
        ),
        "sha256": sha256_bytes(data),
        "sizeBytes": len(data),
        **extra,
    }


def require_binding(
    value: object,
    *,
    data: bytes,
    relative_path: str,
    label: str,
    contract_name: str | None = None,
) -> None:
    binding = value if isinstance(value, dict) else {}
    require_equal(
        binding.get("relativePath"), relative_path, f"{label}.relativePath"
    )
    require_equal(binding.get("sha256"), sha256_bytes(data), f"{label}.sha256")
    require_equal(binding.get("sizeBytes"), len(data), f"{label}.sizeBytes")
    if contract_name is not None:
        require_equal(
            binding.get("contractName"),
            contract_name,
            f"{label}.contractName",
        )


def durable_write_once(path: Path, data: bytes, label: str) -> None:
    """Create one immutable, fsync-backed journal record.

    Journal durability cannot depend on a buffered Python write: the mutation
    marker is the last local action before the external publisher is invoked.
    """

    if not data:
        fail(f"{label} cannot be empty")
    parent = path.parent
    try:
        parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        parent_stat = parent.lstat()
    except OSError as exc:
        fail(f"{label} parent cannot be prepared: {exc}")
    if not stat.S_ISDIR(parent_stat.st_mode) or parent.is_symlink():
        fail(f"{label} parent must be a real directory")
    try:
        os.chmod(parent, 0o700)
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(path, flags, 0o400)
        try:
            view = memoryview(data)
            while view:
                written = os.write(descriptor, view)
                if written < 1:
                    fail(f"{label} could not be written completely")
                view = view[written:]
            os.fchmod(descriptor, 0o400)
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        directory_flags = os.O_RDONLY
        if hasattr(os, "O_DIRECTORY"):
            directory_flags |= os.O_DIRECTORY
        if hasattr(os, "O_NOFOLLOW"):
            directory_flags |= os.O_NOFOLLOW
        directory_descriptor = os.open(parent, directory_flags)
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    except FileExistsError:
        fail(f"{label} already exists")
    except OSError as exc:
        fail(f"{label} cannot be durably written: {exc}")


def journal_record(
    journal_id: str,
    phase: str,
    **fields: object,
) -> dict[str, Any]:
    return {
        "contractName": JOURNAL_CONTRACT,
        "contractVersion": JOURNAL_CONTRACT_VERSION,
        "journalId": journal_id,
        "phase": phase,
        **fields,
    }


def create_or_validate_journal_record(
    path: Path,
    expected: Mapping[str, Any],
    label: str,
) -> tuple[bytes, bool]:
    encoded = provider_auth.immutable_json_bytes(expected)
    if path.exists() or path.is_symlink():
        current = read_regular_file(path, label, MAX_JSON_BYTES)
        if not hmac.compare_digest(current, encoded):
            fail(f"{label} does not match the exact transaction")
        return current, False
    durable_write_once(path, encoded, label)
    return encoded, True


@dataclass(frozen=True)
class PublicationMaterial:
    root: Path
    handoff_path: Path
    candidate_path: Path
    proposal_path: Path
    final_path: Path
    topology_path: Path
    destination_plan_path: Path
    public_bundle: Path
    handoff_bytes: bytes
    candidate_bytes: bytes
    proposal_bytes: bytes
    final_bytes: bytes
    topology_bytes: bytes
    committed_boundary_bytes: bytes
    post_marker_convergence_bytes: bytes
    destination_plan_bytes: bytes
    proposal: Mapping[str, Any]
    candidate: Mapping[str, Any]
    platforms: Mapping[str, Any]
    destination_plan: Mapping[str, Any]
    artifact_identities: Mapping[str, tuple[str, int]]


def snapshot(path: Path, root: Path, label: str) -> assembler.Snapshot:
    try:
        relative = path.absolute().relative_to(root.absolute()).as_posix()
    except ValueError:
        fail(f"{label} path is outside the publication root")
    return assembler.snapshot_relative(
        root,
        relative,
        label,
        MAX_JSON_BYTES,
        read_data=True,
    )


def validate_provider_handoff(
    payload: Mapping[str, Any],
    *,
    handoff_bytes: bytes,
    proposal_snapshot: assembler.Snapshot,
    candidate_snapshot: assembler.Snapshot,
    final_snapshot: assembler.Snapshot,
    source_sha: str,
) -> None:
    required = {
        "contractName",
        "contractVersion",
        "generatedAt",
        "status",
        "repository",
        "source",
        "transportArtifact",
        "proposal",
        "candidateManifest",
        "finalReceipt",
        "reviewerPolicy",
        "approvalEnvironment",
        "approvals",
        "mainBranchGovernance",
        "authorityLevel",
        "provenanceScope",
        "provenanceAuthenticated",
        "releaseArtifactBytesAuthenticated",
        "nonPublishing",
        "publicationAuthorized",
        "allowedSideEffects",
        "handoff",
    }
    exact_dict(payload, required, "provider handoff")
    require_equal(
        payload["contractName"],
        provider_auth.HANDOFF_CONTRACT,
        "provider handoff contractName",
    )
    require_equal(
        payload["contractVersion"],
        provider_auth.HANDOFF_CONTRACT_VERSION,
        "provider handoff contractVersion",
    )
    require_equal(payload["status"], "passed", "provider handoff status")
    require_equal(
        payload["provenanceAuthenticated"],
        True,
        "provider handoff provenanceAuthenticated",
    )
    require_equal(
        payload["releaseArtifactBytesAuthenticated"],
        False,
        "provider handoff releaseArtifactBytesAuthenticated",
    )
    require_equal(payload["nonPublishing"], True, "provider handoff nonPublishing")
    require_equal(
        payload["publicationAuthorized"],
        False,
        "provider handoff publicationAuthorized",
    )
    require_equal(
        payload["allowedSideEffects"],
        list(provider_auth.HANDOFF_SIDE_EFFECTS),
        "provider handoff allowedSideEffects",
    )
    source = payload["source"] if isinstance(payload["source"], dict) else {}
    require_equal(
        source.get("repository"),
        assembler.SOURCE_REPOSITORY,
        "provider handoff source repository",
    )
    require_equal(source.get("ref"), "refs/heads/main", "provider handoff source ref")
    require_equal(source.get("sha"), source_sha, "provider handoff source sha")
    require_binding(
        payload["proposal"],
        data=proposal_snapshot.data or b"",
        relative_path=proposal_snapshot.relative_path,
        label="provider handoff proposal",
        contract_name=assembler.PROPOSAL_CONTRACT,
    )
    require_binding(
        payload["candidateManifest"],
        data=candidate_snapshot.data or b"",
        relative_path=candidate_snapshot.relative_path,
        label="provider handoff candidate manifest",
    )
    require_binding(
        payload["finalReceipt"],
        data=final_snapshot.data or b"",
        relative_path=final_snapshot.relative_path,
        label="provider handoff final receipt",
        contract_name=assembler.FINAL_RECEIPT_CONTRACT,
    )
    next_authority = (
        payload["handoff"] if isinstance(payload["handoff"], dict) else {}
    )
    require_equal(
        next_authority.get("eligibleForSeparatePublicationTransaction"),
        True,
        "provider handoff publication eligibility",
    )
    if not handoff_bytes:
        fail("provider handoff bytes are empty")


def validate_topology_retirement(
    payload: Mapping[str, Any],
    *,
    now: datetime,
    publisher_bytes: bytes,
    committed_boundary_bytes: bytes,
    post_marker_convergence_bytes: bytes,
) -> None:
    exact_dict(
        payload,
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "status",
            "source",
            "sidecarAuthorityRetired",
            "activeSidecarMarkerCount",
            "activeSidecarMarkers",
            "retiredAuthoritySha256",
            "committedBoundaryReceipt",
            "postMarkerConvergenceReceipt",
            "canonicalAuthority",
        },
        "topology retirement proof",
    )
    require_equal(
        payload["contractName"], TOPOLOGY_CONTRACT, "topology contractName"
    )
    require_equal(payload["contractVersion"], 1, "topology contractVersion")
    require_equal(payload["status"], "passed", "topology status")
    generated = assembler.parse_time(payload["generatedAt"], "topology generatedAt")
    if generated > now + timedelta(minutes=5) or now - generated > MAX_TOPOLOGY_AGE:
        fail("topology retirement proof is stale or from the future")
    source = exact_dict(
        payload["source"],
        {"repository", "ref", "commit"},
        "topology source",
    )
    require_equal(
        source["repository"],
        "ArchonMegalon/chummer6-hub",
        "topology source repository",
    )
    require_equal(source["ref"], "refs/heads/main", "topology source ref")
    assembler.require_string(
        source["commit"], "topology source commit", assembler.COMMIT_RE
    )
    terminal = exact_dict(
        load_json_bytes(
            committed_boundary_bytes,
            "committed topology retirement boundary",
        ),
        HUB_TERMINAL_FIELDS,
        "committed topology retirement boundary",
    )
    require_equal(
        terminal["contractName"],
        HUB_TERMINAL_CONTRACT,
        "committed topology retirement contractName",
    )
    require_equal(
        terminal["status"],
        "retired",
        "committed topology retirement status",
    )
    require_equal(
        terminal["operation"],
        HUB_TERMINAL_OPERATION,
        "committed topology retirement operation",
    )
    require_equal(
        terminal["controllerSourceHead"],
        source["commit"],
        "topology terminal controller source",
    )
    assembler.require_string(
        terminal["operationSourceHead"],
        "topology terminal operation source",
        assembler.COMMIT_RE,
    )
    for field in (
        "operationRoot",
        "projectName",
        "retiredAuthorityPath",
        "retirementEvidencePath",
    ):
        assembler.require_string(
            terminal[field], f"topology terminal {field}"
        )
    for field in (
        "retiredAuthoritySha256",
        "retirementEvidenceSha256",
        "connectorGateSha256",
        "postMarkerConnectorGateSha256",
        "latestConnectorGateSha256",
        "priorConfigSha256",
        "incumbentBaselineSha256",
        "incumbentObservationSha256",
        "cleanupSha256",
    ):
        assembler.require_sha256(
            terminal[field], f"topology terminal {field}"
        )
    restored_version = terminal["restoredVersion"]
    if (
        isinstance(restored_version, bool)
        or not isinstance(restored_version, int)
        or restored_version < 0
    ):
        fail("topology terminal restoredVersion must be a nonnegative integer")
    require_equal(
        terminal["incumbentObservationSha256"],
        terminal["incumbentBaselineSha256"],
        "topology terminal incumbent observation",
    )
    terminal_completed = assembler.parse_time(
        terminal["completedAtUtc"],
        "committed topology retirement completedAtUtc",
    )
    if terminal_completed > generated:
        fail("topology renewable envelope predates terminal completion")
    require_equal(
        payload["sidecarAuthorityRetired"],
        True,
        "topology sidecarAuthorityRetired",
    )
    require_equal(
        payload["activeSidecarMarkerCount"],
        0,
        "topology activeSidecarMarkerCount",
    )
    require_equal(
        payload["activeSidecarMarkers"], [], "topology activeSidecarMarkers"
    )
    assembler.require_sha256(
        payload["retiredAuthoritySha256"], "topology retiredAuthoritySha256"
    )
    require_equal(
        terminal["retiredAuthoritySha256"],
        payload["retiredAuthoritySha256"],
        "topology terminal retired authority",
    )
    post_marker = exact_dict(
        load_json_bytes(
            post_marker_convergence_bytes,
            "post-marker topology convergence boundary",
        ),
        HUB_POST_MARKER_FIELDS,
        "post-marker topology convergence boundary",
    )
    require_equal(
        post_marker["contractName"],
        HUB_POST_MARKER_CONTRACT,
        "post-marker topology contractName",
    )
    require_equal(
        post_marker["status"], "pass", "post-marker topology status"
    )
    if post_marker["boundary"] not in {
        "post-marker",
        "resume-post-marker",
    }:
        fail("post-marker topology boundary is invalid")
    require_equal(
        post_marker["operationRoot"],
        terminal["operationRoot"],
        "post-marker topology operationRoot",
    )
    require_equal(
        post_marker["restoredVersion"],
        restored_version,
        "post-marker topology restoredVersion",
    )
    require_equal(
        post_marker["retiredAuthoritySha256"],
        terminal["retiredAuthoritySha256"],
        "post-marker topology retired authority",
    )
    require_equal(
        post_marker["markerConnectorGateSha256"],
        terminal["connectorGateSha256"],
        "post-marker topology connector gate",
    )
    assembler.require_sha256(
        post_marker["connectorConvergenceSha256"],
        "post-marker topology connectorConvergenceSha256",
    )
    convergence = post_marker["connectorConvergence"]
    if not isinstance(convergence, dict):
        fail("post-marker topology connectorConvergence must be an object")
    require_equal(
        convergence.get("targetVersion"),
        restored_version,
        "post-marker topology convergence targetVersion",
    )
    require_equal(
        sha256_bytes(canonical_json_bytes(convergence)),
        post_marker["connectorConvergenceSha256"],
        "post-marker topology convergence digest",
    )
    post_marker_verified = assembler.parse_time(
        post_marker["verifiedAtUtc"],
        "post-marker topology verifiedAtUtc",
    )
    if post_marker_verified > terminal_completed:
        fail("post-marker topology boundary is later than terminal completion")
    post_marker_sha = sha256_bytes(canonical_json_bytes(post_marker))
    if post_marker["boundary"] == "post-marker":
        require_equal(
            post_marker_sha,
            terminal["postMarkerConnectorGateSha256"],
            "original post-marker topology digest",
        )
        require_equal(
            post_marker_sha,
            terminal["latestConnectorGateSha256"],
            "latest post-marker topology digest",
        )
    else:
        require_equal(
            post_marker_sha,
            terminal["latestConnectorGateSha256"],
            "resume post-marker topology digest",
        )
        if hmac.compare_digest(
            post_marker_sha,
            terminal["postMarkerConnectorGateSha256"],
        ):
            fail("resume post-marker topology did not advance the boundary")
    receipt_bytes = {
        "committedBoundaryReceipt": committed_boundary_bytes,
        "postMarkerConvergenceReceipt": post_marker_convergence_bytes,
    }
    for field in ("committedBoundaryReceipt", "postMarkerConvergenceReceipt"):
        receipt = exact_dict(
            payload[field], {"sha256", "sizeBytes"}, f"topology {field}"
        )
        expected_sha = assembler.require_sha256(
            receipt["sha256"], f"topology {field}.sha256"
        )
        expected_size = assembler.require_positive_integer(
            receipt["sizeBytes"], f"topology {field}.sizeBytes"
        )
        require_equal(
            sha256_bytes(receipt_bytes[field]),
            expected_sha,
            f"topology {field} bytes SHA-256",
        )
        require_equal(
            len(receipt_bytes[field]),
            expected_size,
            f"topology {field} bytes size",
        )
    canonical = exact_dict(
        payload["canonicalAuthority"],
        {"baseUrl", "manifestUrl", "publisherPath", "publisherSha256"},
        "topology canonicalAuthority",
    )
    require_equal(
        canonical["baseUrl"], PUBLIC_BASE_URL, "topology canonical baseUrl"
    )
    require_equal(
        canonical["manifestUrl"],
        PUBLIC_MANIFEST_URL,
        "topology canonical manifestUrl",
    )
    require_equal(
        canonical["publisherPath"],
        CANONICAL_PUBLISHER,
        "topology canonical publisherPath",
    )
    require_equal(
        canonical["publisherSha256"],
        sha256_bytes(publisher_bytes),
        "topology canonical publisherSha256",
    )


def common_live_predecessor(platforms: Mapping[str, Any]) -> tuple[str, str]:
    roots: set[tuple[str, str]] = set()
    for platform in assembler.PLATFORMS:
        row = platforms.get(platform)
        lifecycle = row.get("nativeLifecycleEvidence") if isinstance(row, dict) else {}
        authority = (
            lifecycle.get("livePredecessorAuthority")
            if isinstance(lifecycle, dict)
            else {}
        )
        if not isinstance(authority, dict):
            fail(f"{platform} is missing live predecessor authority")
        roots.add(
            (
                str(authority.get("url", "")),
                assembler.require_sha256(
                    authority.get("liveReleaseChannelSha256"),
                    f"{platform} live predecessor SHA-256",
                ),
            )
        )
    if len(roots) != 1:
        fail("the three platforms do not bind one exact live predecessor")
    return next(iter(roots))


def validate_public_manifest_contract(
    data: bytes,
    *,
    label: str,
    candidate: Mapping[str, Any],
    platforms: Mapping[str, Any],
    artifact_field: str,
    platform_field: str,
    url_field: str,
) -> dict[str, dict[str, Any]]:
    payload = load_json_bytes(data, label)
    release_version = str(candidate["releaseVersion"])
    channel_id = str(candidate["channelId"])
    require_equal(payload.get("version"), release_version, f"{label}.version")
    require_equal(
        payload.get("releaseVersion"),
        release_version,
        f"{label}.releaseVersion",
    )
    require_equal(payload.get("status"), "published", f"{label}.status")
    manifest_channel = payload.get("channelId", payload.get("channel"))
    require_equal(manifest_channel, channel_id, f"{label}.channel")
    artifacts = payload.get(artifact_field)
    if not isinstance(artifacts, list):
        fail(f"{label}.{artifact_field} must be an array")
    if len(artifacts) != len(assembler.PLATFORMS):
        fail(
            f"{label}.{artifact_field} must contain exactly the three "
            "candidate artifacts"
        )
    expected_names = {
        str(platforms[platform]["artifact"]["fileName"]): platform
        for platform in assembler.PLATFORMS
    }
    matches: dict[str, Mapping[str, Any]] = {}
    for index, raw in enumerate(artifacts):
        if not isinstance(raw, dict):
            fail(f"{label}.{artifact_field}[{index}] must be an object")
        file_name = raw.get("fileName")
        if file_name not in expected_names:
            fail(
                f"{label}.{artifact_field}[{index}] is not an exact "
                "candidate artifact"
            )
        if str(file_name) in matches:
            fail(f"{label} contains a duplicate candidate artifact row")
        matches[str(file_name)] = raw
    require_equal(
        set(matches), set(expected_names), f"{label} candidate artifact set"
    )
    normalized: dict[str, dict[str, Any]] = {}
    for file_name, platform in expected_names.items():
        row = matches[file_name]
        artifact = platforms[platform]["artifact"]
        require_equal(
            row.get(platform_field),
            platform,
            f"{label} {platform}.{platform_field}",
        )
        require_equal(
            row.get(url_field),
            f"{PUBLIC_BASE_URL}/files/{quote(file_name)}",
            f"{label} {platform}.{url_field}",
        )
        require_equal(
            row.get("sha256"), artifact["sha256"], f"{label} {platform}.sha256"
        )
        require_equal(
            row.get("sizeBytes"),
            artifact["sizeBytes"],
            f"{label} {platform}.sizeBytes",
        )
        require_equal(
            row.get("version"), release_version, f"{label} {platform}.version"
        )
        require_equal(
            row.get("releaseVersion"),
            release_version,
            f"{label} {platform}.releaseVersion",
        )
        require_equal(
            row.get("installAccessClass"),
            "open_public",
            f"{label} {platform}.installAccessClass",
        )
        require_equal(
            row.get("compatibilityState"),
            "compatible",
            f"{label} {platform}.compatibilityState",
        )
        normalized[platform] = {
            "platform": platform,
            "fileName": file_name,
            "url": row[url_field],
            "sha256": row["sha256"],
            "sizeBytes": row["sizeBytes"],
            "version": row["version"],
            "releaseVersion": row["releaseVersion"],
            "installAccessClass": row["installAccessClass"],
            "compatibilityState": row["compatibilityState"],
        }
    return normalized


def require_manifest_projection_equality(
    canonical: Mapping[str, Any],
    compatibility: Mapping[str, Any],
) -> None:
    require_equal(
        canonical,
        compatibility,
        "canonical and compatibility manifest artifact projections",
    )


def validate_canonical_bundle(
    *,
    public_bundle: Path,
    repository_root: Path,
) -> None:
    try:
        bundle_mode = public_bundle.lstat().st_mode
        files_mode = (public_bundle / "files").lstat().st_mode
    except OSError as exc:
        fail(f"public bundle directory cannot be inspected: {exc}")
    if (
        stat.S_ISLNK(bundle_mode)
        or not stat.S_ISDIR(bundle_mode)
        or stat.S_ISLNK(files_mode)
        or not stat.S_ISDIR(files_mode)
    ):
        fail("public bundle and files root must be non-symlink directories")
    allowed_entries = {
        "RELEASE_CHANNEL.generated.json",
        "releases.json",
        "files",
    }
    try:
        actual_entries = {entry.name for entry in public_bundle.iterdir()}
    except OSError as exc:
        fail(f"public bundle cannot be enumerated: {exc}")
    require_equal(
        actual_entries, allowed_entries, "canonical public bundle top-level files"
    )
    verifier = repository_root / "scripts" / "verify-releases-manifest.sh"
    environment = {
        key: value
        for key in ("PATH", "HOME", "LANG", "LC_ALL", "GITHUB_WORKSPACE")
        if (value := os.environ.get(key))
    }
    if registry_root := os.environ.get("CHUMMER_HUB_REGISTRY_ROOT"):
        environment["CHUMMER_HUB_REGISTRY_ROOT"] = registry_root
    environment["CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE"] = "1"
    completed = subprocess.run(
        [
            "bash",
            str(verifier),
            "--require-complete-desktop-coverage",
            str(public_bundle),
        ],
        cwd=repository_root,
        env=environment,
        check=False,
    )
    if completed.returncode != 0:
        fail("canonical Registry release-manifest verifier rejected the bundle")


def validate_destination_plan(
    payload: Mapping[str, Any],
    *,
    candidate: Mapping[str, Any],
    platforms: Mapping[str, Any],
    public_bundle: Path,
    artifact_identities: Mapping[str, tuple[str, int]],
    topology_bytes: bytes,
) -> None:
    exact_dict(
        payload,
        {
            "contractName",
            "contractVersion",
            "candidateId",
            "releaseVersion",
            "baseUrl",
            "previousManifest",
            "topologyRetirementProof",
            "manifests",
            "artifacts",
        },
        "destination plan",
    )
    require_equal(
        payload["contractName"],
        DESTINATION_PLAN_CONTRACT,
        "destination plan contractName",
    )
    require_equal(payload["contractVersion"], 1, "destination plan contractVersion")
    require_equal(
        payload["candidateId"], candidate["candidateId"], "destination candidateId"
    )
    require_equal(
        payload["releaseVersion"],
        candidate["releaseVersion"],
        "destination releaseVersion",
    )
    require_equal(payload["baseUrl"], PUBLIC_BASE_URL, "destination baseUrl")
    predecessor_url, predecessor_sha = common_live_predecessor(platforms)
    previous = exact_dict(
        payload["previousManifest"],
        {"url", "sha256"},
        "destination previousManifest",
    )
    require_equal(previous["url"], predecessor_url, "destination predecessor URL")
    require_equal(
        previous["url"], PUBLIC_MANIFEST_URL, "destination canonical predecessor URL"
    )
    require_equal(
        previous["sha256"], predecessor_sha, "destination predecessor SHA-256"
    )
    public_topology = exact_dict(
        payload["topologyRetirementProof"],
        {"url", "sha256", "sizeBytes"},
        "destination topologyRetirementProof",
    )
    require_equal(
        public_topology["url"],
        PUBLIC_TOPOLOGY_PROOF_URL,
        "destination topology proof URL",
    )
    require_equal(
        public_topology["sha256"],
        sha256_bytes(topology_bytes),
        "destination topology proof SHA-256",
    )
    require_equal(
        public_topology["sizeBytes"],
        len(topology_bytes),
        "destination topology proof size",
    )
    manifests = exact_dict(
        payload["manifests"],
        {"canonical", "releases"},
        "destination manifests",
    )
    normalized_manifests: dict[str, Mapping[str, Any]] = {}
    for (
        key,
        file_name,
        expected_url,
        artifact_field,
        platform_field,
        url_field,
    ) in (
        (
            "canonical",
            "RELEASE_CHANNEL.generated.json",
            PUBLIC_MANIFEST_URL,
            "artifacts",
            "platform",
            "downloadUrl",
        ),
        (
            "releases",
            "releases.json",
            PUBLIC_RELEASES_URL,
            "downloads",
            "platformId",
            "url",
        ),
    ):
        data = read_regular_file(
            public_bundle / file_name,
            f"public bundle {file_name}",
            MAX_JSON_BYTES,
        )
        row = exact_dict(
            manifests[key],
            {"url", "sha256", "sizeBytes"},
            f"destination manifests.{key}",
        )
        require_equal(row["url"], expected_url, f"destination {key} URL")
        require_equal(
            row["sha256"], sha256_bytes(data), f"destination {key} SHA-256"
        )
        require_equal(row["sizeBytes"], len(data), f"destination {key} size")
        normalized_manifests[key] = validate_public_manifest_contract(
            data,
            label=f"public bundle {file_name}",
            candidate=candidate,
            platforms=platforms,
            artifact_field=artifact_field,
            platform_field=platform_field,
            url_field=url_field,
        )
    require_manifest_projection_equality(
        normalized_manifests["canonical"],
        normalized_manifests["releases"],
    )
    if manifests["canonical"]["sha256"] == previous["sha256"]:
        fail("destination final manifest equals its predecessor; refusing a rerun")

    rows = payload["artifacts"]
    if not isinstance(rows, list) or len(rows) != len(assembler.PLATFORMS):
        fail("destination plan must contain exactly three platform artifacts")
    by_platform: dict[str, Mapping[str, Any]] = {}
    for index, raw in enumerate(rows):
        row = exact_dict(
            raw,
            {"platform", "fileName", "url", "sha256", "sizeBytes"},
            f"destination artifacts[{index}]",
        )
        platform = str(row["platform"])
        if platform not in assembler.PLATFORMS or platform in by_platform:
            fail("destination artifact platforms must be distinct and complete")
        by_platform[platform] = row
    if set(by_platform) != set(assembler.PLATFORMS):
        fail("destination artifacts omit a required platform")
    for platform in assembler.PLATFORMS:
        artifact = platforms[platform]["artifact"]
        file_name = str(artifact["fileName"])
        row = by_platform[platform]
        require_equal(row["fileName"], file_name, f"{platform} destination fileName")
        require_equal(
            row["url"],
            f"{PUBLIC_BASE_URL}/files/{quote(file_name)}",
            f"{platform} destination URL",
        )
        require_equal(
            row["sha256"], artifact["sha256"], f"{platform} destination SHA-256"
        )
        require_equal(
            row["sizeBytes"], artifact["sizeBytes"], f"{platform} destination size"
        )
        public_snapshot = assembler.snapshot_absolute(
            public_bundle / "files" / file_name,
            f"{platform} public bundle artifact",
            MAX_PUBLIC_FILE_BYTES,
            read_data=False,
        )
        if (
            public_snapshot.sha256,
            public_snapshot.size_bytes,
        ) != artifact_identities[platform]:
            fail(f"{platform} public bundle bytes differ from candidate bytes")
    try:
        public_entries = list((public_bundle / "files").iterdir())
    except OSError as exc:
        fail(f"public bundle files cannot be enumerated: {exc}")
    if any(path.is_symlink() or not path.is_file() for path in public_entries):
        fail("public bundle files must be regular non-symlink files")
    public_file_names = {path.name for path in public_entries}
    require_equal(
        public_file_names,
        {
            str(platforms[platform]["artifact"]["fileName"])
            for platform in assembler.PLATFORMS
        },
        "public bundle exact artifact set",
    )


def load_material(
    *,
    publication_root: Path,
    handoff_path: Path,
    repository_root: Path,
    now: datetime,
) -> PublicationMaterial:
    root = publication_root.resolve(strict=True)
    handoff_path = handoff_path.resolve(strict=True)
    handoff_bytes = read_regular_file(
        handoff_path, "provider handoff", MAX_JSON_BYTES
    )
    handoff = load_json_bytes(handoff_bytes, "provider handoff")

    def handoff_bound_path(field: str) -> Path:
        value = handoff.get(field)
        if not isinstance(value, dict):
            fail(f"provider handoff {field} binding is missing")
        relative = assembler.safe_relative_path(
            value.get("relativePath"),
            f"provider handoff {field}.relativePath",
        )
        return root / PurePosixPath(relative)

    candidate_path = handoff_bound_path("candidateManifest")
    proposal_path = handoff_bound_path("proposal")
    final_path = handoff_bound_path("finalReceipt")
    topology_path = root / "topology-retirement.json"
    destination_path = root / "destination-plan.json"
    public_bundle = root / "public-bundle"
    publisher_path = repository_root / CANONICAL_PUBLISHER

    candidate_snapshot = snapshot(candidate_path, root, "candidate manifest")
    proposal_snapshot = snapshot(proposal_path, root, "proposal")
    final_snapshot = snapshot(final_path, root, "final receipt")
    final_payload = load_json_bytes(
        final_snapshot.data or b"", "final receipt"
    )
    final_approvals = final_payload.get("approvals")
    if not isinstance(final_approvals, list):
        fail("final receipt approvals must be an array")
    approvals: dict[str, assembler.Snapshot] = {}
    for index, raw in enumerate(final_approvals):
        if not isinstance(raw, dict):
            fail(f"final receipt approvals[{index}] must be an object")
        role = assembler.require_string(
            raw.get("role"), f"final receipt approvals[{index}].role"
        )
        if role not in assembler.REQUIRED_APPROVAL_ROLES or role in approvals:
            fail("final receipt approval roles must be distinct and complete")
        receipt = raw.get("receipt")
        if not isinstance(receipt, dict):
            fail(f"final receipt {role} approval binding is missing")
        relative = assembler.safe_relative_path(
            receipt.get("relativePath"),
            f"final receipt {role} approval relativePath",
        )
        storage_relative = (
            PurePosixPath("approvals") / role / PurePosixPath(relative)
        )
        stored = assembler.snapshot_relative(
            root,
            storage_relative.as_posix(),
            f"{role} approval",
            MAX_JSON_BYTES,
            read_data=True,
        )
        approvals[role] = assembler.Snapshot(
            path=stored.path,
            relative_path=relative,
            sha256=stored.sha256,
            size_bytes=stored.size_bytes,
            data=stored.data,
        )
    if set(approvals) != set(assembler.REQUIRED_APPROVAL_ROLES):
        fail("final receipt does not bind all three required approvals")
    local = provider_auth.validate_local_bundle(
        provider_auth.LocalBundle(
            proposal=proposal_snapshot,
            candidate=candidate_snapshot,
            final_receipt=final_snapshot,
            approvals=approvals,
        ),
        now=now,
    )
    candidate, platforms, _actors = assembler.validate_candidate(
        candidate_snapshot,
        now=now,
        max_age_seconds=assembler.MAX_EVIDENCE_AGE_SECONDS,
    )
    require_equal(candidate, local.proposal["candidate"], "candidate projection")
    require_equal(platforms, local.proposal["platforms"], "platform projections")

    source_sha = str(candidate["source"]["commit"])
    validate_provider_handoff(
        handoff,
        handoff_bytes=handoff_bytes,
        proposal_snapshot=proposal_snapshot,
        candidate_snapshot=candidate_snapshot,
        final_snapshot=final_snapshot,
        source_sha=source_sha,
    )
    publisher_bytes = read_regular_file(
        publisher_path, "canonical publisher", MAX_JSON_BYTES
    )
    topology_bytes = read_regular_file(
        topology_path, "topology retirement proof", MAX_JSON_BYTES
    )
    committed_boundary_bytes = read_regular_file(
        root / "committed-boundary-receipt.json",
        "committed boundary receipt",
        MAX_JSON_BYTES,
    )
    post_marker_convergence_bytes = read_regular_file(
        root / "post-marker-convergence-receipt.json",
        "post-marker convergence receipt",
        MAX_JSON_BYTES,
    )
    topology = load_json_bytes(topology_bytes, "topology retirement proof")
    validate_topology_retirement(
        topology,
        now=now,
        publisher_bytes=publisher_bytes,
        committed_boundary_bytes=committed_boundary_bytes,
        post_marker_convergence_bytes=post_marker_convergence_bytes,
    )

    artifact_identities: dict[str, tuple[str, int]] = {}
    for platform in assembler.PLATFORMS:
        artifact = local.proposal["platforms"][platform]["artifact"]
        relative = assembler.safe_relative_path(
            artifact["relativePath"], f"{platform} candidate artifact path"
        )
        artifact_snapshot = assembler.snapshot_relative(
            root,
            relative,
            f"{platform} candidate artifact",
            MAX_PUBLIC_FILE_BYTES,
            read_data=False,
        )
        require_equal(
            artifact_snapshot.sha256,
            artifact["sha256"],
            f"{platform} artifact SHA-256",
        )
        require_equal(
            artifact_snapshot.size_bytes,
            artifact["sizeBytes"],
            f"{platform} artifact size",
        )
        artifact_identities[platform] = (
            artifact_snapshot.sha256,
            artifact_snapshot.size_bytes,
        )

    destination_bytes = read_regular_file(
        destination_path, "destination plan", MAX_JSON_BYTES
    )
    destination = load_json_bytes(destination_bytes, "destination plan")
    validate_destination_plan(
        destination,
        candidate=candidate,
        platforms=platforms,
        public_bundle=public_bundle,
        artifact_identities=artifact_identities,
        topology_bytes=topology_bytes,
    )
    validate_canonical_bundle(
        public_bundle=public_bundle,
        repository_root=repository_root,
    )
    return PublicationMaterial(
        root=root,
        handoff_path=handoff_path,
        candidate_path=candidate_path,
        proposal_path=proposal_path,
        final_path=final_path,
        topology_path=topology_path,
        destination_plan_path=destination_path,
        public_bundle=public_bundle,
        handoff_bytes=handoff_bytes,
        candidate_bytes=candidate_snapshot.data or b"",
        proposal_bytes=proposal_snapshot.data or b"",
        final_bytes=final_snapshot.data or b"",
        topology_bytes=topology_bytes,
        committed_boundary_bytes=committed_boundary_bytes,
        post_marker_convergence_bytes=post_marker_convergence_bytes,
        destination_plan_bytes=destination_bytes,
        proposal=local.proposal,
        candidate=candidate,
        platforms=platforms,
        destination_plan=destination,
        artifact_identities=artifact_identities,
    )


def validate_transport_run(
    value: object,
    *,
    run_id: int,
    source_sha: str,
    workflow: str,
    actor: str | None,
    label: str,
) -> Mapping[str, Any]:
    run = value if isinstance(value, dict) else {}
    require_equal(run.get("id"), run_id, f"{label}.id")
    require_equal(run.get("run_attempt"), 1, f"{label}.run_attempt")
    require_equal(run.get("event"), "workflow_dispatch", f"{label}.event")
    require_equal(run.get("status"), "completed", f"{label}.status")
    require_equal(run.get("conclusion"), "success", f"{label}.conclusion")
    require_equal(run.get("head_branch"), "main", f"{label}.head_branch")
    require_equal(run.get("head_sha"), source_sha, f"{label}.head_sha")
    raw_path = assembler.require_string(run.get("path"), f"{label}.path")
    path, marker, suffix = raw_path.partition("@")
    require_equal(path, workflow, f"{label}.path")
    if marker and suffix not in {"main", "refs/heads/main"}:
        fail(f"{label}.path uses an unexpected workflow ref suffix")
    run_actor = provider_auth.validate_user(
        run.get("actor"),
        expected_login=(
            actor
            if actor is not None
            else assembler.require_string(
                (run.get("actor") or {}).get("login")
                if isinstance(run.get("actor"), dict)
                else None,
                f"{label}.actor.login",
                assembler.GITHUB_LOGIN_RE,
            )
        ),
        label=f"{label}.actor",
    )
    triggering = provider_auth.validate_user(
        run.get("triggering_actor"),
        expected_login=run_actor["login"],
        label=f"{label}.triggering_actor",
    )
    require_equal(
        triggering["id"], run_actor["id"], f"{label} triggering actor identity"
    )
    pull_requests = run.get("pull_requests")
    if pull_requests != []:
        fail(f"{label} must not bind a pull request")
    referenced = run.get("referenced_workflows")
    if referenced != []:
        fail(f"{label} must not invoke a reusable workflow")
    return {
        "id": run_id,
        "attempt": 1,
        "workflow": workflow,
        "actor": run_actor,
        "headSha": source_sha,
    }


def authenticate_one_transport(
    client: provider_auth.ProviderReader,
    *,
    artifact_id: int,
    expected_digest: str,
    expected_name: str,
    expected_run_id: int,
    source_sha: str,
    repository_id: int,
    maximum_bytes: int,
    workflow: str,
    actor: str | None,
    now: datetime | None = None,
    clock: Callable[[], datetime] | None = None,
    label: str,
) -> Mapping[str, Any]:
    def observed_at(boundary: str) -> datetime:
        if clock is not None:
            return read_clock(clock, f"{label} {boundary}")
        if now is None:
            fail(f"{label} requires a provider clock")
        return now

    assembler.require_positive_integer(artifact_id, f"{label} ID")
    provider_auth.require_api_string(
        expected_digest,
        f"{label} expected digest",
        provider_auth.ARTIFACT_DIGEST_RE,
    )
    detail_path = provider_auth.repository_api_path(
        f"/actions/artifacts/{artifact_id}"
    )
    metadata_now = observed_at("metadata read")
    response = client.get_json(detail_path)
    provider_auth.require_unpaginated(response.headers, f"{label} metadata")
    metadata = provider_auth.validate_artifact_metadata(
        response.value,
        expected_id=artifact_id,
        expected_name=expected_name,
        expected_run_id=expected_run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=metadata_now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=label,
    )
    observed_at("workflow run read")
    run_response = client.get_json(
        provider_auth.repository_api_path(
            f"/actions/runs/{expected_run_id}"
        )
    )
    provider_auth.require_unpaginated(
        run_response.headers, f"{label} workflow run"
    )
    run = validate_transport_run(
        run_response.value,
        run_id=expected_run_id,
        source_sha=source_sha,
        workflow=workflow,
        actor=actor,
        label=f"{label} workflow run",
    )
    recheck_now = observed_at("metadata recheck")
    recheck = client.get_json(detail_path)
    provider_auth.require_unpaginated(
        recheck.headers, f"{label} metadata recheck"
    )
    final_metadata = provider_auth.validate_artifact_metadata(
        recheck.value,
        expected_id=artifact_id,
        expected_name=expected_name,
        expected_run_id=expected_run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=recheck_now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=f"{label} metadata recheck",
    )
    require_equal(final_metadata, metadata, f"{label} metadata recheck")
    return {"artifact": metadata, "run": run}


def authenticate_transport_archive(
    client: provider_auth.ProviderReader,
    *,
    artifact_id: int,
    expected_digest: str,
    expected_name: str,
    expected_run_id: int,
    source_sha: str,
    repository_id: int,
    maximum_bytes: int,
    workflow: str,
    actor: str | None,
    now: datetime | None = None,
    clock: Callable[[], datetime] | None = None,
    label: str,
) -> tuple[dict[str, Any], bytes]:
    authority = dict(
        authenticate_one_transport(
            client,
            artifact_id=artifact_id,
            expected_digest=expected_digest,
            expected_name=expected_name,
            expected_run_id=expected_run_id,
            source_sha=source_sha,
            repository_id=repository_id,
            maximum_bytes=maximum_bytes,
            workflow=workflow,
            actor=actor,
            now=now,
            clock=clock,
            label=label,
        )
    )
    archive = provider_auth.download_authenticated_artifact(
        client,
        metadata=authority["artifact"],
        maximum_bytes=maximum_bytes,
        label=label,
    )
    authority["archiveSha256"] = sha256_bytes(archive)
    authority["archiveSizeBytes"] = len(archive)
    return authority, archive


def materialize_archive_entries(
    entries: Mapping[str, bytes],
    root: Path,
    *,
    label: str,
) -> None:
    if root.exists() or root.is_symlink():
        fail(f"{label} extraction root already exists")
    try:
        root.mkdir(mode=0o700, parents=True)
    except OSError as exc:
        fail(f"{label} extraction root cannot be created: {exc}")
    for relative, data in sorted(entries.items()):
        normalized = assembler.safe_relative_path(
            relative, f"{label} entry path"
        )
        if normalized != relative:
            fail(f"{label} contains a non-canonical entry path")
        target = root / PurePosixPath(relative)
        try:
            target.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
            flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
            if hasattr(os, "O_NOFOLLOW"):
                flags |= os.O_NOFOLLOW
            descriptor = os.open(target, flags, 0o600)
            try:
                view = memoryview(data)
                while view:
                    written = os.write(descriptor, view)
                    if written < 1:
                        fail(f"{label} entry could not be written")
                    view = view[written:]
                os.fchmod(descriptor, 0o600)
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
        except FileExistsError:
            fail(f"{label} contains colliding entry paths")
        except OSError as exc:
            fail(f"{label} entry cannot be materialized: {exc}")


def publication_input_inventory(root: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    try:
        paths = sorted(root.rglob("*"))
    except OSError as exc:
        fail(f"publication input cannot be inventoried: {exc}")
    for path in paths:
        try:
            mode = path.lstat().st_mode
        except OSError as exc:
            fail(f"publication input entry cannot be inspected: {exc}")
        if stat.S_ISDIR(mode):
            if path.is_symlink():
                fail("publication input contains a symlink directory")
            continue
        if path.is_symlink() or not stat.S_ISREG(mode):
            fail("publication input contains a non-regular entry")
        relative = path.relative_to(root).as_posix()
        if relative == ASSEMBLY_RECEIPT_NAME:
            continue
        data = read_regular_file(
            path,
            f"publication input {relative}",
            MAX_PUBLIC_FILE_BYTES,
        )
        rows.append(binding_bytes(data, relative))
    if not rows:
        fail("publication input inventory is empty")
    return rows


def validate_transport_authority_receipt(
    value: object,
    label: str,
) -> dict[str, Any]:
    authority = exact_dict(
        value,
        {"artifact", "run", "archiveSha256", "archiveSizeBytes"},
        label,
    )
    artifact = exact_dict(
        authority["artifact"],
        {
            "id",
            "name",
            "digest",
            "sizeBytes",
            "createdAt",
            "updatedAt",
            "expiresAt",
            "workflowRunId",
        },
        f"{label}.artifact",
    )
    run = exact_dict(
        authority["run"],
        {"id", "attempt", "workflow", "actor", "headSha"},
        f"{label}.run",
    )
    archive_sha = assembler.require_sha256(
        authority["archiveSha256"], f"{label}.archiveSha256"
    )
    archive_size = assembler.require_positive_integer(
        authority["archiveSizeBytes"], f"{label}.archiveSizeBytes"
    )
    require_equal(
        artifact["digest"],
        f"sha256:{archive_sha}",
        f"{label} provider/archive digest",
    )
    require_equal(
        artifact["sizeBytes"],
        archive_size,
        f"{label} provider/archive size",
    )
    assembler.require_positive_integer(
        artifact["id"], f"{label}.artifact.id"
    )
    assembler.require_string(
        artifact["name"], f"{label}.artifact.name", assembler.FILE_NAME_RE
    )
    run_id = assembler.require_positive_integer(
        run["id"], f"{label}.run.id"
    )
    require_equal(
        artifact["workflowRunId"],
        run_id,
        f"{label} artifact/run ID",
    )
    require_equal(run["attempt"], 1, f"{label}.run.attempt")
    assembler.require_string(
        run["workflow"], f"{label}.run.workflow"
    )
    assembler.require_string(
        run["headSha"], f"{label}.run.headSha", assembler.COMMIT_RE
    )
    actor = (
        run["actor"] if isinstance(run["actor"], dict) else {}
    )
    actor_login = assembler.require_string(
        actor.get("login"),
        f"{label}.run.actor.login",
        assembler.GITHUB_LOGIN_RE,
    )
    provider_auth.validate_user(
        actor,
        expected_login=actor_login,
        label=f"{label}.run.actor",
    )
    created = assembler.parse_time(
        artifact["createdAt"], f"{label}.artifact.createdAt"
    )
    updated = assembler.parse_time(
        artifact["updatedAt"], f"{label}.artifact.updatedAt"
    )
    expires = assembler.parse_time(
        artifact["expiresAt"], f"{label}.artifact.expiresAt"
    )
    if not created <= updated < expires:
        fail(f"{label} artifact timestamps are not monotonic")
    return authority


def validate_assembly_receipt(
    payload: Mapping[str, Any],
    *,
    receipt_bytes: bytes,
    material: PublicationMaterial,
    assembly_authority: Mapping[str, Any],
    handoff_authority: Mapping[str, Any],
    now: datetime,
) -> Mapping[str, Any]:
    exact_dict(
        payload,
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "expiresAt",
            "status",
            "candidate",
            "assembly",
            "upstreamArtifacts",
            "providerHandoff",
            "candidateManifest",
            "proposal",
            "finalReceipt",
            "topologyRetirement",
            "committedBoundaryReceipt",
            "postMarkerConvergenceReceipt",
            "destinationPlan",
            "manifests",
            "platforms",
            "inventory",
            "nonPublishing",
            "publicationAuthorized",
            "releaseArtifactBytesAuthenticated",
        },
        "publication input assembly receipt",
    )
    require_equal(
        payload["contractName"],
        ASSEMBLY_CONTRACT,
        "assembly receipt contractName",
    )
    require_equal(
        payload["contractVersion"],
        ASSEMBLY_CONTRACT_VERSION,
        "assembly receipt contractVersion",
    )
    require_equal(payload["status"], "passed", "assembly receipt status")
    generated = assembler.parse_time(
        payload["generatedAt"], "assembly receipt generatedAt"
    )
    expires = assembler.parse_time(
        payload["expiresAt"], "assembly receipt expiresAt"
    )
    if (
        generated > now + timedelta(minutes=5)
        or expires <= now
        or expires - generated > timedelta(hours=24)
    ):
        fail("publication input assembly receipt is stale or from the future")
    require_equal(payload["nonPublishing"], True, "assembly nonPublishing")
    require_equal(
        payload["publicationAuthorized"],
        False,
        "assembly publicationAuthorized",
    )
    require_equal(
        payload["releaseArtifactBytesAuthenticated"],
        True,
        "assembly releaseArtifactBytesAuthenticated",
    )
    candidate = exact_dict(
        payload["candidate"],
        {"candidateId", "releaseVersion", "source", "producer"},
        "assembly candidate",
    )
    require_equal(
        candidate["candidateId"],
        material.candidate["candidateId"],
        "assembly candidateId",
    )
    require_equal(
        candidate["releaseVersion"],
        material.candidate["releaseVersion"],
        "assembly releaseVersion",
    )
    require_equal(
        candidate["source"], material.candidate["source"], "assembly source"
    )
    require_equal(
        candidate["producer"],
        material.candidate["producer"],
        "assembly producer",
    )
    assembly = exact_dict(
        payload["assembly"],
        {
            "repository",
            "ref",
            "sha",
            "workflow",
            "runId",
            "runAttempt",
            "actor",
            "environment",
        },
        "assembly authority",
    )
    exact_assembly_authority = validate_transport_authority_receipt(
        assembly_authority,
        "publication-input assembly transport authority",
    )
    require_equal(
        assembly["repository"], assembler.SOURCE_REPOSITORY, "assembly repository"
    )
    require_equal(assembly["ref"], "refs/heads/main", "assembly ref")
    require_equal(
        assembly["sha"], material.candidate["source"]["commit"], "assembly SHA"
    )
    require_equal(assembly["workflow"], ASSEMBLY_WORKFLOW, "assembly workflow")
    require_equal(
        assembly["runId"], assembly_authority["run"]["id"], "assembly run ID"
    )
    require_equal(assembly["runAttempt"], 1, "assembly run attempt")
    require_equal(
        str(assembly["actor"]).casefold(),
        str(assembly_authority["run"]["actor"]["login"]).casefold(),
        "assembly actor",
    )
    require_equal(
        assembly["environment"], ASSEMBLY_ENVIRONMENT, "assembly environment"
    )
    require_equal(
        exact_assembly_authority["run"]["workflow"],
        ASSEMBLY_WORKFLOW,
        "assembly transport workflow",
    )
    require_equal(
        exact_assembly_authority["run"]["headSha"],
        material.candidate["source"]["commit"],
        "assembly transport source",
    )
    assembly_name = ASSEMBLY_ARTIFACT_RE.fullmatch(
        str(exact_assembly_authority["artifact"]["name"])
    )
    if assembly_name is None:
        fail("publication-input assembly artifact name is malformed")
    require_equal(
        assembly_name.group(1),
        material.candidate["candidateId"],
        "assembly artifact candidate ID",
    )
    require_equal(
        int(assembly_name.group(2)),
        assembly["runId"],
        "assembly artifact run ID",
    )
    upstream = exact_dict(
        payload["upstreamArtifacts"],
        {
            "candidatePayload",
            "providerInput",
            "providerHandoff",
            "approvals",
            "hubTopology",
        },
        "assembly upstreamArtifacts",
    )
    candidate_authority = validate_transport_authority_receipt(
        upstream["candidatePayload"],
        "assembly candidate payload authority",
    )
    candidate_producer = material.candidate["producer"]
    candidate_run = candidate_authority["run"]
    candidate_artifact = candidate_authority["artifact"]
    require_equal(
        candidate_run["id"],
        candidate_producer["runId"],
        "assembly candidate producer run ID",
    )
    require_equal(
        candidate_run["workflow"],
        candidate_producer["workflow"],
        "assembly candidate producer workflow",
    )
    require_equal(
        str(candidate_run["actor"]["login"]).casefold(),
        str(candidate_producer["actor"]).casefold(),
        "assembly candidate producer actor",
    )
    require_equal(
        candidate_run["headSha"],
        material.candidate["source"]["commit"],
        "assembly candidate producer source",
    )
    candidate_name = CANDIDATE_PAYLOAD_ARTIFACT_RE.fullmatch(
        str(candidate_artifact["name"])
    )
    if candidate_name is None:
        fail("assembly candidate payload artifact name is malformed")
    require_equal(
        candidate_name.group(1),
        material.candidate["candidateId"],
        "assembly candidate payload artifact candidate ID",
    )
    require_equal(
        int(candidate_name.group(2)),
        candidate_run["id"],
        "assembly candidate payload artifact run ID",
    )
    if candidate_producer.get("artifactName") is not None:
        require_equal(
            candidate_producer.get("artifactName"),
            candidate_artifact["name"],
            "assembly candidate producer artifact name",
        )
    validate_transport_authority_receipt(
        upstream["providerHandoff"],
        "assembly provider handoff authority",
    )
    require_equal(
        upstream["providerHandoff"],
        handoff_authority,
        "assembly provider handoff authority",
    )
    provider_input = (
        upstream["providerInput"]
        if isinstance(upstream["providerInput"], dict)
        else {}
    )
    require_equal(
        provider_input.get("trustedAsAuthority"),
        False,
        "assembly provider-input authority boundary",
    )
    exact_dict(
        provider_input,
        {
            "artifact",
            "archiveSha256",
            "archiveSizeBytes",
            "trustedAsAuthority",
            "purpose",
        },
        "assembly provider input",
    )
    require_equal(
        provider_input.get("purpose"),
        "bounded-metadata-transport-only",
        "assembly provider-input purpose",
    )
    provider_input_artifact = exact_dict(
        provider_input.get("artifact"),
        {
            "id",
            "name",
            "digest",
            "sizeBytes",
            "createdAt",
            "updatedAt",
            "expiresAt",
            "workflowRunId",
        },
        "assembly provider input artifact",
    )
    provider_input_archive_sha = assembler.require_sha256(
        provider_input.get("archiveSha256"),
        "assembly provider input archiveSha256",
    )
    require_equal(
        provider_input_artifact["digest"],
        f"sha256:{provider_input_archive_sha}",
        "assembly provider input digest",
    )
    assembler.require_positive_integer(
        provider_input_artifact["id"],
        "assembly provider input artifact.id",
    )
    require_equal(
        provider_input_artifact["name"],
        provider_auth.INPUT_ARTIFACT_NAME,
        "assembly provider input artifact.name",
    )
    assembler.require_positive_integer(
        provider_input_artifact["workflowRunId"],
        "assembly provider input artifact.workflowRunId",
    )
    provider_input_archive_size = assembler.require_positive_integer(
        provider_input.get("archiveSizeBytes"),
        "assembly provider input archiveSizeBytes",
    )
    require_equal(
        provider_input_artifact["sizeBytes"],
        provider_input_archive_size,
        "assembly provider input size",
    )
    approval_rows = upstream["approvals"]
    if not isinstance(approval_rows, list) or len(approval_rows) != len(
        assembler.REQUIRED_APPROVAL_ROLES
    ):
        fail("assembly upstream approvals must contain exactly three rows")
    final_payload = load_json_bytes(
        material.final_bytes, "assembly-bound final receipt"
    )
    final_approvals = final_payload.get("approvals")
    if not isinstance(final_approvals, list):
        fail("assembly-bound final approvals are missing")
    final_by_role = {
        str(row.get("role")): row
        for row in final_approvals
        if isinstance(row, dict)
    }
    seen_approval_roles: set[str] = set()
    for index, raw in enumerate(approval_rows):
        row = exact_dict(
            raw,
            {"role", "authority", "receipt"},
            f"assembly upstream approvals[{index}]",
        )
        role = str(row["role"])
        if (
            role not in assembler.REQUIRED_APPROVAL_ROLES
            or role in seen_approval_roles
        ):
            fail("assembly upstream approval roles are invalid")
        seen_approval_roles.add(role)
        approval_authority = validate_transport_authority_receipt(
            row["authority"], f"assembly {role} approval authority"
        )
        approval_run = approval_authority["run"]
        approval_artifact = approval_authority["artifact"]
        expected_approval_name = (
            f"global-flagship-release-approval-{role}-"
            f"{approval_run['id']}-1"
        )
        require_equal(
            approval_artifact["name"],
            expected_approval_name,
            f"assembly {role} approval artifact name",
        )
        require_equal(
            approval_run["workflow"],
            assembler.APPROVAL_WORKFLOW,
            f"assembly {role} approval workflow",
        )
        require_equal(
            approval_run["headSha"],
            material.candidate["source"]["commit"],
            f"assembly {role} approval source",
        )
        final_row = final_by_role.get(role)
        final_binding = (
            final_row.get("receipt")
            if isinstance(final_row, dict)
            and isinstance(final_row.get("receipt"), dict)
            else {}
        )
        receipt_relative = assembler.safe_relative_path(
            final_binding.get("relativePath"),
            f"assembly final {role} approval path",
        )
        receipt_path = (
            material.root
            / "approvals"
            / role
            / PurePosixPath(receipt_relative)
        )
        approval_bytes = read_regular_file(
            receipt_path,
            f"assembly {role} approval receipt",
            MAX_JSON_BYTES,
        )
        require_binding(
            row["receipt"],
            data=approval_bytes,
            relative_path=f"approvals/{role}/{receipt_relative}",
            label=f"assembly {role} approval receipt",
            contract_name=assembler.APPROVAL_CONTRACT,
        )
        approval_payload = load_json_bytes(
            approval_bytes, f"assembly {role} approval receipt"
        )
        require_equal(
            str(approval_run["actor"]["login"]).casefold(),
            str(approval_payload.get("actor")).casefold(),
            f"assembly {role} approval actor",
        )
        approval_authority_row = approval_payload.get("authority")
        require_equal(
            approval_run["id"],
            (
                approval_authority_row.get("runId")
                if isinstance(approval_authority_row, dict)
                else None
            ),
            f"assembly {role} approval run",
        )
    require_equal(
        seen_approval_roles,
        set(assembler.REQUIRED_APPROVAL_ROLES),
        "assembly approval role set",
    )
    if not isinstance(upstream["hubTopology"], dict):
        fail("assembly Hub topology authority must be an object")
    require_binding(
        payload["providerHandoff"],
        data=material.handoff_bytes,
        relative_path="provider-handoff.json",
        label="assembly provider handoff",
        contract_name=provider_auth.HANDOFF_CONTRACT,
    )
    for field, data, relative, contract_name in (
        (
            "candidateManifest",
            material.candidate_bytes,
            material.candidate_path.relative_to(material.root).as_posix(),
            assembler.CANDIDATE_CONTRACT,
        ),
        (
            "proposal",
            material.proposal_bytes,
            material.proposal_path.relative_to(material.root).as_posix(),
            assembler.PROPOSAL_CONTRACT,
        ),
        (
            "finalReceipt",
            material.final_bytes,
            material.final_path.relative_to(material.root).as_posix(),
            assembler.FINAL_RECEIPT_CONTRACT,
        ),
        (
            "topologyRetirement",
            material.topology_bytes,
            "topology-retirement.json",
            TOPOLOGY_CONTRACT,
        ),
        (
            "committedBoundaryReceipt",
            material.committed_boundary_bytes,
            "committed-boundary-receipt.json",
            None,
        ),
        (
            "postMarkerConvergenceReceipt",
            material.post_marker_convergence_bytes,
            "post-marker-convergence-receipt.json",
            None,
        ),
        (
            "destinationPlan",
            material.destination_plan_bytes,
            "destination-plan.json",
            DESTINATION_PLAN_CONTRACT,
        ),
    ):
        require_binding(
            payload[field],
            data=data,
            relative_path=relative,
            label=f"assembly {field}",
            contract_name=contract_name,
        )
    manifests = exact_dict(
        payload["manifests"],
        {"canonical", "releases"},
        "assembly manifests",
    )
    for key, file_name in (
        ("canonical", "RELEASE_CHANNEL.generated.json"),
        ("releases", "releases.json"),
    ):
        data = read_regular_file(
            material.public_bundle / file_name,
            f"assembly public bundle {file_name}",
            MAX_JSON_BYTES,
        )
        require_binding(
            manifests[key],
            data=data,
            relative_path=f"public-bundle/{file_name}",
            label=f"assembly manifests.{key}",
        )
    platform_rows = exact_dict(
        payload["platforms"],
        set(assembler.PLATFORMS),
        "assembly platforms",
    )
    for platform in assembler.PLATFORMS:
        artifact = material.platforms[platform]["artifact"]
        data = read_regular_file(
            material.public_bundle / "files" / str(artifact["fileName"]),
            f"assembly {platform} public artifact",
            MAX_PUBLIC_FILE_BYTES,
        )
        require_binding(
            platform_rows[platform],
            data=data,
            relative_path=(
                f"public-bundle/files/{artifact['fileName']}"
            ),
            label=f"assembly platforms.{platform}",
        )
    require_equal(
        payload["inventory"],
        publication_input_inventory(material.root),
        "assembly exact publication input inventory",
    )
    if not receipt_bytes:
        fail("publication input assembly receipt bytes are empty")
    return payload


@dataclass(frozen=True)
class AuthenticatedPublicationInputs:
    material: PublicationMaterial
    transports: Mapping[str, Any]
    assembly_receipt_bytes: bytes
    assembly_receipt: Mapping[str, Any]
    handoff_archive_sha256: str
    handoff_archive_size: int
    assembly_archive_sha256: str
    assembly_archive_size: int


def prepare_publication_inputs(
    client: provider_auth.ProviderReader,
    *,
    repository_root: Path,
    publication_root: Path,
    source_sha: str,
    provider_handoff_artifact_id: int,
    provider_handoff_artifact_digest: str,
    provider_handoff_artifact_name: str,
    publication_input_artifact_id: int,
    publication_input_artifact_digest: str,
    publication_input_artifact_name: str,
    clock: Callable[[], datetime],
) -> AuthenticatedPublicationInputs:
    read_clock(clock, "UI repository provider read")
    repository = provider_auth.validate_repository(
        client.get_json(provider_auth.repository_api_path(""))
    )
    repository_id = int(repository["id"])
    handoff_match = HANDOFF_ARTIFACT_RE.fullmatch(
        provider_handoff_artifact_name
    )
    if handoff_match is None:
        fail("provider handoff artifact name is malformed")
    handoff_run_id = int(handoff_match.group(2))
    handoff_authority, handoff_archive = authenticate_transport_archive(
        client,
        artifact_id=provider_handoff_artifact_id,
        expected_digest=provider_handoff_artifact_digest,
        expected_name=provider_handoff_artifact_name,
        expected_run_id=handoff_run_id,
        source_sha=source_sha,
        repository_id=repository_id,
        maximum_bytes=provider_auth.MAX_APPROVAL_ARTIFACT_BYTES,
        workflow=PROVIDER_HANDOFF_WORKFLOW,
        actor=None,
        clock=clock,
        label="provider handoff artifact",
    )
    handoff_entries = provider_auth.read_exact_zip(
        handoff_archive,
        expected_names=PROVIDER_HANDOFF_ARCHIVE_ENTRIES,
        maximum_entries=1,
        maximum_total_bytes=provider_auth.MAX_APPROVAL_ARTIFACT_BYTES,
        label="provider handoff artifact archive",
    )
    handoff_bytes = handoff_entries["handoff.json"]

    assembly_match = ASSEMBLY_ARTIFACT_RE.fullmatch(
        publication_input_artifact_name
    )
    if assembly_match is None:
        fail("publication-input assembly artifact name is malformed")
    assembly_run_id = int(assembly_match.group(2))
    assembly_authority, assembly_archive = authenticate_transport_archive(
        client,
        artifact_id=publication_input_artifact_id,
        expected_digest=publication_input_artifact_digest,
        expected_name=publication_input_artifact_name,
        expected_run_id=assembly_run_id,
        source_sha=source_sha,
        repository_id=repository_id,
        maximum_bytes=MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        workflow=ASSEMBLY_WORKFLOW,
        actor=None,
        clock=clock,
        label="publication-input assembly artifact",
    )
    assembly_entries = provider_auth.read_exact_zip(
        assembly_archive,
        expected_names=None,
        maximum_entries=4097,
        maximum_total_bytes=MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        label="publication-input assembly archive",
    )
    required_entries = {ASSEMBLY_RECEIPT_NAME, "provider-handoff.json"}
    if not required_entries.issubset(assembly_entries):
        fail("publication-input assembly archive omits its authority receipts")
    if not hmac.compare_digest(
        assembly_entries["provider-handoff.json"], handoff_bytes
    ):
        fail(
            "publication-input assembly handoff differs from the directly "
            "authenticated provider archive"
        )
    materialize_archive_entries(
        assembly_entries,
        publication_root,
        label="publication-input assembly archive",
    )
    material = load_material(
        publication_root=publication_root,
        handoff_path=publication_root / "provider-handoff.json",
        repository_root=repository_root,
        now=read_clock(clock, "publication input material validation"),
    )
    require_equal(
        assembly_match.group(1),
        material.candidate["candidateId"],
        "publication-input assembly artifact candidate ID",
    )
    handoff = load_json_bytes(handoff_bytes, "provider handoff")
    transport = handoff.get("transportArtifact")
    transport_id = (
        transport.get("id") if isinstance(transport, dict) else None
    )
    require_equal(
        int(handoff_match.group(1)),
        transport_id,
        "provider handoff artifact input-ID name binding",
    )
    receipt_bytes = assembly_entries[ASSEMBLY_RECEIPT_NAME]
    receipt = load_json_bytes(
        receipt_bytes, "publication input assembly receipt"
    )
    validate_assembly_receipt(
        receipt,
        receipt_bytes=receipt_bytes,
        material=material,
        assembly_authority=assembly_authority,
        handoff_authority=handoff_authority,
        now=read_clock(clock, "publication input assembly receipt validation"),
    )
    transports = {
        "repository": repository,
        "providerHandoff": handoff_authority,
        "publicationInputAssembly": assembly_authority,
        "metadataOnlyProviderBoundary": receipt["upstreamArtifacts"][
            "providerInput"
        ],
    }
    return AuthenticatedPublicationInputs(
        material=material,
        transports=transports,
        assembly_receipt_bytes=receipt_bytes,
        assembly_receipt=receipt,
        handoff_archive_sha256=sha256_bytes(handoff_archive),
        handoff_archive_size=len(handoff_archive),
        assembly_archive_sha256=sha256_bytes(assembly_archive),
        assembly_archive_size=len(assembly_archive),
    )


def reauthenticate_publication_inputs(
    client: provider_auth.ProviderReader,
    *,
    prepared: AuthenticatedPublicationInputs,
    source_sha: str,
    provider_handoff_artifact_id: int,
    provider_handoff_artifact_digest: str,
    provider_handoff_artifact_name: str,
    publication_input_artifact_id: int,
    publication_input_artifact_digest: str,
    publication_input_artifact_name: str,
    clock: Callable[[], datetime],
) -> Mapping[str, Any]:
    read_clock(clock, "UI repository provider reauthentication")
    repository = provider_auth.validate_repository(
        client.get_json(provider_auth.repository_api_path(""))
    )
    repository_id = int(repository["id"])
    handoff_match = HANDOFF_ARTIFACT_RE.fullmatch(
        provider_handoff_artifact_name
    )
    assembly_match = ASSEMBLY_ARTIFACT_RE.fullmatch(
        publication_input_artifact_name
    )
    if handoff_match is None or assembly_match is None:
        fail("publication transport artifact name changed")
    handoff_authority, handoff_archive = authenticate_transport_archive(
        client,
        artifact_id=provider_handoff_artifact_id,
        expected_digest=provider_handoff_artifact_digest,
        expected_name=provider_handoff_artifact_name,
        expected_run_id=int(handoff_match.group(2)),
        source_sha=source_sha,
        repository_id=repository_id,
        maximum_bytes=provider_auth.MAX_APPROVAL_ARTIFACT_BYTES,
        workflow=PROVIDER_HANDOFF_WORKFLOW,
        actor=None,
        clock=clock,
        label="provider handoff artifact reauthentication",
    )
    assembly_authority, assembly_archive = authenticate_transport_archive(
        client,
        artifact_id=publication_input_artifact_id,
        expected_digest=publication_input_artifact_digest,
        expected_name=publication_input_artifact_name,
        expected_run_id=int(assembly_match.group(2)),
        source_sha=source_sha,
        repository_id=repository_id,
        maximum_bytes=MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        workflow=ASSEMBLY_WORKFLOW,
        actor=None,
        clock=clock,
        label="publication-input assembly artifact reauthentication",
    )
    for actual, expected, label in (
        (
            sha256_bytes(handoff_archive),
            prepared.handoff_archive_sha256,
            "provider handoff archive reauthentication SHA-256",
        ),
        (
            len(handoff_archive),
            prepared.handoff_archive_size,
            "provider handoff archive reauthentication size",
        ),
        (
            sha256_bytes(assembly_archive),
            prepared.assembly_archive_sha256,
            "assembly archive reauthentication SHA-256",
        ),
        (
            len(assembly_archive),
            prepared.assembly_archive_size,
            "assembly archive reauthentication size",
        ),
    ):
        require_equal(actual, expected, label)
    validate_assembly_receipt(
        prepared.assembly_receipt,
        receipt_bytes=prepared.assembly_receipt_bytes,
        material=prepared.material,
        assembly_authority=assembly_authority,
        handoff_authority=handoff_authority,
        now=read_clock(clock, "assembly receipt reauthentication"),
    )
    final = {
        "repository": repository,
        "providerHandoff": handoff_authority,
        "publicationInputAssembly": assembly_authority,
        "metadataOnlyProviderBoundary": prepared.assembly_receipt[
            "upstreamArtifacts"
        ]["providerInput"],
    }
    require_equal(
        final, prepared.transports, "publication input transport reauthentication"
    )
    return final


def hub_api_path(suffix: str) -> str:
    if suffix and not suffix.startswith("/"):
        fail("internal Hub provider API suffix is invalid")
    return f"/repos/{HUB_REPOSITORY}{suffix}"


def validate_hub_run(
    value: object,
    *,
    repository_id: int,
    run_id: int,
    source_sha: str,
    label: str,
) -> Mapping[str, Any]:
    run = value if isinstance(value, dict) else {}
    require_equal(run.get("id"), run_id, f"{label}.id")
    require_equal(run.get("run_attempt"), 1, f"{label}.run_attempt")
    require_equal(run.get("event"), "workflow_dispatch", f"{label}.event")
    require_equal(run.get("status"), "completed", f"{label}.status")
    require_equal(run.get("conclusion"), "success", f"{label}.conclusion")
    require_equal(run.get("head_branch"), "main", f"{label}.head_branch")
    require_equal(run.get("head_sha"), source_sha, f"{label}.head_sha")
    raw_path = assembler.require_string(run.get("path"), f"{label}.path")
    path, marker, suffix = raw_path.partition("@")
    require_equal(path, HUB_PROOF_WORKFLOW, f"{label}.path")
    if marker and suffix not in {"main", "refs/heads/main"}:
        fail(f"{label}.path uses an unexpected workflow ref")
    workflow_id = provider_auth.require_api_integer(
        run.get("workflow_id"), f"{label}.workflow_id"
    )
    actor_value = run.get("actor")
    actor_login = assembler.require_string(
        actor_value.get("login") if isinstance(actor_value, dict) else None,
        f"{label}.actor.login",
        assembler.GITHUB_LOGIN_RE,
    )
    actor = provider_auth.validate_user(
        actor_value, expected_login=actor_login, label=f"{label}.actor"
    )
    triggering = provider_auth.validate_user(
        run.get("triggering_actor"),
        expected_login=actor_login,
        label=f"{label}.triggering_actor",
    )
    require_equal(
        actor["id"], triggering["id"], f"{label} triggering actor identity"
    )
    for field in ("repository", "head_repository"):
        repository = run.get(field)
        if not isinstance(repository, dict):
            fail(f"{label}.{field} must be an object")
        require_equal(
            repository.get("id"), repository_id, f"{label}.{field}.id"
        )
        require_equal(
            repository.get("full_name"),
            HUB_REPOSITORY,
            f"{label}.{field}.full_name",
        )
    require_equal(run.get("pull_requests"), [], f"{label}.pull_requests")
    require_equal(
        run.get("referenced_workflows"), [], f"{label}.referenced_workflows"
    )
    created = provider_auth.parse_api_time(
        run.get("created_at"), f"{label}.created_at"
    )
    started = provider_auth.parse_api_time(
        run.get("run_started_at"), f"{label}.run_started_at"
    )
    updated = provider_auth.parse_api_time(
        run.get("updated_at"), f"{label}.updated_at"
    )
    if not created <= started <= updated:
        fail(f"{label} timestamps are not monotonic")
    return {
        "id": run_id,
        "attempt": 1,
        "workflowId": workflow_id,
        "workflowPath": HUB_PROOF_WORKFLOW,
        "actor": actor,
        "createdAt": assembler.format_time(created),
        "startedAt": assembler.format_time(started),
        "updatedAt": assembler.format_time(updated),
    }


def authenticate_hub_topology_provider(
    client: provider_auth.ProviderReader,
    *,
    material: PublicationMaterial,
    artifact_id: int,
    artifact_name: str,
    expected_digest: str,
    now: datetime | None = None,
    clock: Callable[[], datetime] | None = None,
) -> Mapping[str, Any]:
    def observed_at(boundary: str) -> datetime:
        if clock is not None:
            return read_clock(clock, f"Hub topology {boundary}")
        if now is None:
            fail("Hub topology provider requires a live clock")
        return now

    name_match = HUB_PROOF_ARTIFACT_RE.fullmatch(artifact_name)
    if name_match is None:
        fail("Hub topology provider artifact name is malformed")
    run_id = int(name_match.group(1))
    topology = load_json_bytes(
        material.topology_bytes, "topology retirement proof"
    )
    topology_source = topology.get("source")
    if not isinstance(topology_source, dict):
        fail("topology retirement proof source is missing")
    terminal_source_sha = assembler.require_string(
        topology_source.get("commit"),
        "topology Hub source commit",
        assembler.COMMIT_RE,
    )
    if (
        len(set(terminal_source_sha)) < 4
        or terminal_source_sha == "0" * 40
    ):
        fail("topology Hub source commit is synthetic")
    terminal = exact_dict(
        load_json_bytes(
            material.committed_boundary_bytes,
            "committed topology retirement boundary",
        ),
        HUB_TERMINAL_FIELDS,
        "committed topology retirement boundary",
    )
    require_equal(
        terminal.get("controllerSourceHead"),
        terminal_source_sha,
        "topology terminal controller source",
    )

    observed_at("repository read")
    repository_response = client.get_json(hub_api_path(""))
    provider_auth.require_unpaginated(
        repository_response.headers, "Hub repository response"
    )
    repository = (
        repository_response.value
        if isinstance(repository_response.value, dict)
        else {}
    )
    repository_id = provider_auth.require_api_integer(
        repository.get("id"), "Hub repository.id"
    )
    require_equal(
        repository.get("full_name"),
        HUB_REPOSITORY,
        "Hub repository.full_name",
    )
    require_equal(
        repository.get("default_branch"), "main", "Hub repository.default_branch"
    )
    require_equal(repository.get("archived"), False, "Hub repository.archived")
    require_equal(repository.get("disabled"), False, "Hub repository.disabled")

    digest_value = provider_auth.require_api_string(
        expected_digest,
        "Hub topology artifact expected digest",
        provider_auth.ARTIFACT_DIGEST_RE,
    )
    artifact_path = hub_api_path(f"/actions/artifacts/{artifact_id}")
    artifact_now = observed_at("artifact metadata read")
    artifact_response = client.get_json(artifact_path)
    provider_auth.require_unpaginated(
        artifact_response.headers, "Hub topology artifact response"
    )
    artifact = (
        artifact_response.value
        if isinstance(artifact_response.value, dict)
        else {}
    )
    require_equal(artifact.get("id"), artifact_id, "Hub topology artifact.id")
    require_equal(
        artifact.get("name"), artifact_name, "Hub topology artifact.name"
    )
    size_bytes = provider_auth.require_api_integer(
        artifact.get("size_in_bytes"), "Hub topology artifact.size_in_bytes"
    )
    if size_bytes > MAX_HUB_PROOF_ARTIFACT_BYTES:
        fail("Hub topology artifact exceeds its byte boundary")
    require_equal(
        artifact.get("expired"), False, "Hub topology artifact.expired"
    )
    provider_auth.require_not_expired(
        artifact.get("expires_at"),
        now=artifact_now,
        label="Hub topology artifact.expires_at",
    )
    require_equal(
        artifact.get("digest"), digest_value, "Hub topology artifact.digest"
    )
    require_equal(
        artifact.get("archive_download_url"),
        f"{provider_auth.API_ROOT}{hub_api_path(f'/actions/artifacts/{artifact_id}/zip')}",
        "Hub topology artifact.archive_download_url",
    )
    workflow_run = artifact.get("workflow_run")
    if not isinstance(workflow_run, dict):
        fail("Hub topology artifact.workflow_run must be an object")
    require_equal(
        workflow_run.get("id"), run_id, "Hub topology artifact workflow run ID"
    )
    for field in ("repository_id", "head_repository_id"):
        require_equal(
            workflow_run.get(field),
            repository_id,
            f"Hub topology artifact.workflow_run.{field}",
        )
    require_equal(
        workflow_run.get("head_branch"),
        "main",
        "Hub topology artifact workflow branch",
    )
    provider_source_sha = assembler.require_string(
        workflow_run.get("head_sha"),
        "Hub topology artifact provider source",
        assembler.COMMIT_RE,
    )
    if (
        len(set(provider_source_sha)) < 4
        or provider_source_sha == "0" * 40
    ):
        fail("Hub topology provider source commit is synthetic")
    created_at = provider_auth.parse_api_time(
        artifact.get("created_at"), "Hub topology artifact.created_at"
    )
    updated_at = provider_auth.parse_api_time(
        artifact.get("updated_at"), "Hub topology artifact.updated_at"
    )
    topology_generated = assembler.parse_time(
        topology.get("generatedAt"), "topology generatedAt"
    )
    if not (
        topology_generated - timedelta(minutes=5)
        <= created_at
        <= updated_at
        <= artifact_now + timedelta(minutes=5)
    ):
        fail("Hub topology artifact timestamps do not contain the proof")

    observed_at("workflow run read")
    run_response = client.get_json(hub_api_path(f"/actions/runs/{run_id}"))
    provider_auth.require_unpaginated(
        run_response.headers, "Hub topology workflow run"
    )
    run = validate_hub_run(
        run_response.value,
        repository_id=repository_id,
        run_id=run_id,
        source_sha=provider_source_sha,
        label="Hub topology workflow run",
    )
    observed_at("workflow attempt read")
    attempt_response = client.get_json(
        hub_api_path(
            f"/actions/runs/{run_id}/attempts/1?exclude_pull_requests=false"
        )
    )
    provider_auth.require_unpaginated(
        attempt_response.headers, "Hub topology workflow attempt"
    )
    attempt = validate_hub_run(
        attempt_response.value,
        repository_id=repository_id,
        run_id=run_id,
        source_sha=provider_source_sha,
        label="Hub topology workflow attempt",
    )
    require_equal(attempt, run, "Hub topology workflow attempt")

    observed_at("workflow definition read")
    workflow_response = client.get_json(
        hub_api_path(f"/actions/workflows/{run['workflowId']}")
    )
    provider_auth.require_unpaginated(
        workflow_response.headers, "Hub topology workflow definition"
    )
    workflow = (
        workflow_response.value
        if isinstance(workflow_response.value, dict)
        else {}
    )
    require_equal(
        workflow.get("id"),
        run["workflowId"],
        "Hub topology workflow definition.id",
    )
    require_equal(
        workflow.get("path"),
        HUB_PROOF_WORKFLOW,
        "Hub topology workflow definition.path",
    )
    require_equal(
        workflow.get("state"),
        "active",
        "Hub topology workflow definition.state",
    )

    observed_at("main branch read")
    branch_response = client.get_json(hub_api_path("/branches/main"))
    provider_auth.require_unpaginated(
        branch_response.headers, "Hub main branch"
    )
    branch = (
        branch_response.value
        if isinstance(branch_response.value, dict)
        else {}
    )
    require_equal(branch.get("name"), "main", "Hub main branch.name")
    require_equal(branch.get("protected"), True, "Hub main branch.protected")
    commit = branch.get("commit")
    if not isinstance(commit, dict):
        fail("Hub main branch commit is missing")
    require_equal(
        commit.get("sha"),
        provider_source_sha,
        "Hub main branch provider commit",
    )

    observed_at("terminal ancestry read")
    compare_response = client.get_json(
        hub_api_path(
            f"/compare/{terminal_source_sha}...{provider_source_sha}"
        )
    )
    provider_auth.require_unpaginated(
        compare_response.headers, "Hub terminal source ancestry"
    )
    comparison = (
        compare_response.value
        if isinstance(compare_response.value, dict)
        else {}
    )
    base_commit = comparison.get("base_commit")
    merge_base = comparison.get("merge_base_commit")
    if not isinstance(base_commit, dict) or not isinstance(merge_base, dict):
        fail("Hub terminal source ancestry commits are missing")
    require_equal(
        base_commit.get("sha"),
        terminal_source_sha,
        "Hub terminal ancestry base commit",
    )
    require_equal(
        merge_base.get("sha"),
        terminal_source_sha,
        "Hub terminal ancestry merge base",
    )
    status = comparison.get("status")
    ahead_by = comparison.get("ahead_by")
    behind_by = comparison.get("behind_by")
    if (
        status not in {"ahead", "identical"}
        or isinstance(ahead_by, bool)
        or not isinstance(ahead_by, int)
        or ahead_by < 0
        or isinstance(behind_by, bool)
        or not isinstance(behind_by, int)
        or behind_by != 0
        or (
            provider_source_sha == terminal_source_sha
            and (status != "identical" or ahead_by != 0)
        )
        or (
            provider_source_sha != terminal_source_sha
            and (status != "ahead" or ahead_by < 1)
        )
    ):
        fail(
            "Hub terminal source is not an authenticated ancestor of "
            "protected main"
        )

    observed_at("artifact archive read")
    archive = provider_auth.download_authenticated_artifact(
        client,
        metadata={
            "id": artifact_id,
            "digest": digest_value,
            "sizeBytes": size_bytes,
        },
        maximum_bytes=MAX_HUB_PROOF_ARTIFACT_BYTES,
        label="Hub topology artifact",
    )
    entries = provider_auth.read_exact_zip(
        archive,
        expected_names=HUB_PROOF_ENTRIES,
        maximum_entries=3,
        maximum_total_bytes=MAX_HUB_PROOF_ARTIFACT_BYTES,
        label="Hub topology artifact archive",
    )
    expected_entries = {
        "TOPOLOGY_B_RETIREMENT.generated.json": material.topology_bytes,
        "committed-boundary-receipt.json": material.committed_boundary_bytes,
        "post-marker-convergence-receipt.json": (
            material.post_marker_convergence_bytes
        ),
    }
    for name, expected_bytes in expected_entries.items():
        if not hmac.compare_digest(entries[name], expected_bytes):
            fail(f"Hub topology provider bytes differ for {name}")

    recheck_now = observed_at("artifact metadata final recheck")
    artifact_recheck = client.get_json(artifact_path)
    provider_auth.require_unpaginated(
        artifact_recheck.headers, "Hub topology artifact final recheck"
    )
    require_equal(
        artifact_recheck.value,
        artifact_response.value,
        "Hub topology artifact final recheck",
    )
    rechecked_artifact = (
        artifact_recheck.value
        if isinstance(artifact_recheck.value, dict)
        else {}
    )
    provider_auth.require_not_expired(
        rechecked_artifact.get("expires_at"),
        now=recheck_now,
        label="Hub topology artifact final expires_at",
    )
    return {
        "repository": {
            "id": repository_id,
            "fullName": HUB_REPOSITORY,
            "defaultBranch": "main",
        },
        "source": {
            "ref": "refs/heads/main",
            "sourceSha": terminal_source_sha,
            "providerSourceSha": provider_source_sha,
            "protected": True,
            "ancestry": {
                "status": status,
                "aheadBy": ahead_by,
                "behindBy": 0,
            },
        },
        "run": run,
        "workflow": {
            "id": run["workflowId"],
            "path": HUB_PROOF_WORKFLOW,
            "state": "active",
        },
        "artifact": {
            "id": artifact_id,
            "name": artifact_name,
            "digest": digest_value,
            "sizeBytes": size_bytes,
            "archiveSha256": sha256_bytes(archive),
            "createdAt": assembler.format_time(created_at),
            "updatedAt": assembler.format_time(updated_at),
        },
        "entries": {
            name: {
                "sha256": sha256_bytes(data),
                "sizeBytes": len(data),
            }
            for name, data in sorted(entries.items())
        },
    }


class DestinationReader(Protocol):
    def get(self, url: str, maximum_bytes: int) -> bytes | "FetchedResource":
        """Read one exact public HTTPS resource without redirects."""


class Publisher(Protocol):
    def publish(self, bundle: Path, manifest_url: str) -> None:
        """Publish one already-validated immutable public bundle."""


@dataclass(frozen=True)
class FetchedResource:
    sha256: str
    size_bytes: int


def destination_identity(
    reader: DestinationReader,
    url: str,
    maximum_bytes: int,
) -> FetchedResource:
    fetched = reader.get(url, maximum_bytes)
    if isinstance(fetched, bytes):
        if len(fetched) > maximum_bytes:
            fail("public destination exceeds its bounded size")
        return FetchedResource(sha256_bytes(fetched), len(fetched))
    if not isinstance(fetched, FetchedResource):
        fail("destination reader returned an invalid result")
    return fetched


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(  # type: ignore[override]
        self,
        req: urllib.request.Request,
        fp: Any,
        code: int,
        msg: str,
        headers: Any,
        newurl: str,
    ) -> None:
        del req, fp, code, msg, headers, newurl
        return None


class PublicHttpsReader:
    def __init__(self) -> None:
        self._opener = urllib.request.build_opener(_NoRedirect())

    def get(self, url: str, maximum_bytes: int) -> FetchedResource:
        parsed = urlsplit(url)
        if (
            parsed.scheme != "https"
            or parsed.hostname != "chummer.run"
            or parsed.username is not None
            or parsed.password is not None
            or parsed.fragment
        ):
            fail("public destination URL is outside the exact chummer.run authority")
        request = urllib.request.Request(
            url,
            headers={"Accept": "application/json, application/octet-stream"},
            method="GET",
        )
        try:
            with self._opener.open(
                request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                if response.status != 200 or response.geturl() != url:
                    fail("public destination returned an unexpected status or URL")
                hasher = hashlib.sha256()
                size_bytes = 0
                while chunk := response.read(1024 * 1024):
                    size_bytes += len(chunk)
                    if size_bytes > maximum_bytes:
                        fail("public destination exceeds its bounded size")
                    hasher.update(chunk)
        except urllib.error.HTTPError as exc:
            fail(f"public destination GET failed closed with HTTP {exc.code}")
        except urllib.error.URLError:
            fail("public destination GET failed closed")
        return FetchedResource(hasher.hexdigest(), size_bytes)


class CanonicalHttpPublisher:
    def __init__(
        self,
        *,
        repository_root: Path,
        publication_token: str,
    ) -> None:
        if not publication_token:
            fail("least-privilege publication token is missing")
        self._repository_root = repository_root
        self._token = publication_token

    def publish(self, bundle: Path, manifest_url: str) -> None:
        publisher = self._repository_root / CANONICAL_PUBLISHER
        token = self._token
        self._token = ""
        allowed = (
            "PATH",
            "HOME",
            "LANG",
            "LC_ALL",
            "TMPDIR",
            "GITHUB_WORKSPACE",
            "CHUMMER_HUB_REGISTRY_ROOT",
        )
        environment = {
            key: value for key in allowed if (value := os.environ.get(key))
        }
        environment.update(
            {
                "CHUMMER_RELEASE_UPLOAD_TOKEN": token,
                "CHUMMER_RELEASE_UPLOAD_NON_INTERACTIVE": "1",
                "CHUMMER_RELEASE_UPLOAD_ALLOW_DIRECT_FALLBACK": "0",
                "CHUMMER_RELEASE_UPLOAD_DRY_RUN": "0",
                "CHUMMER_RELEASE_UPLOAD_VERIFY_MANIFEST": "1",
                "CHUMMER_RELEASE_UPLOAD_VERIFY_ROUTES": "1",
                "CHUMMER_RELEASE_UPLOAD_VERIFY_WINDOWS_PAYLOADS": "1",
                "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": manifest_url,
                "CHUMMER_PUBLIC_BASE_URL": "https://chummer.run",
            }
        )
        try:
            completed = subprocess.run(
                ["bash", str(publisher), str(bundle)],
                cwd=self._repository_root,
                env=environment,
                check=False,
            )
        finally:
            token = ""
            environment.pop("CHUMMER_RELEASE_UPLOAD_TOKEN", None)
        if completed.returncode != 0:
            fail(
                "canonical HTTP publisher failed; publication remains "
                "unauthorized"
            )


def expected_destination_rows(
    material: PublicationMaterial,
) -> list[tuple[str, str, int]]:
    plan = material.destination_plan
    manifests = plan["manifests"]
    rows = [
        (
            plan["topologyRetirementProof"]["url"],
            plan["topologyRetirementProof"]["sha256"],
            int(plan["topologyRetirementProof"]["sizeBytes"]),
        ),
        (
            manifests["canonical"]["url"],
            manifests["canonical"]["sha256"],
            int(manifests["canonical"]["sizeBytes"]),
        ),
        (
            manifests["releases"]["url"],
            manifests["releases"]["sha256"],
            int(manifests["releases"]["sizeBytes"]),
        ),
    ]
    for artifact in plan["artifacts"]:
        rows.append(
            (
                artifact["url"],
                artifact["sha256"],
                int(artifact["sizeBytes"]),
            )
        )
    return rows


def verify_destinations(
    reader: DestinationReader,
    material: PublicationMaterial,
) -> list[dict[str, Any]]:
    verified: list[dict[str, Any]] = []
    for url, expected_sha, expected_size in expected_destination_rows(material):
        fetched = destination_identity(
            reader, url, max(expected_size, MAX_JSON_BYTES)
        )
        require_equal(
            fetched.size_bytes, expected_size, f"live destination {url} size"
        )
        require_equal(
            fetched.sha256, expected_sha, f"live destination {url} SHA-256"
        )
        verified.append(
            {"url": url, "sha256": expected_sha, "sizeBytes": expected_size}
        )
    # Close the consistency window around artifact reads.  Both public
    # manifests must still be the exact final bytes after all installers were
    # streamed and hashed.
    manifests = material.destination_plan["manifests"]
    for key in ("canonical", "releases"):
        row = manifests[key]
        rechecked = destination_identity(
            reader, row["url"], max(int(row["sizeBytes"]), MAX_JSON_BYTES)
        )
        require_equal(
            rechecked.sha256,
            row["sha256"],
            f"live destination {row['url']} final recheck SHA-256",
        )
        require_equal(
            rechecked.size_bytes,
            row["sizeBytes"],
            f"live destination {row['url']} final recheck size",
        )
    return verified


def require_execution_context(
    *,
    confirmation: str,
    proposal_sha256: str,
    repository: str,
    ref: str,
    run_attempt: str,
    actor: str,
    triggering_actor: str,
    environment_name: str,
    source_sha: str,
    candidate_source_sha: str,
) -> None:
    require_equal(repository, assembler.SOURCE_REPOSITORY, "workflow repository")
    require_equal(ref, "refs/heads/main", "workflow ref")
    require_equal(run_attempt, "1", "workflow fresh-dispatch attempt")
    if not actor or actor.casefold() != triggering_actor.casefold():
        fail("a different actor cannot rerun the publication transaction")
    require_equal(
        environment_name,
        PUBLICATION_ENVIRONMENT,
        "workflow protected environment",
    )
    assembler.require_string(
        source_sha, "workflow source SHA", assembler.COMMIT_RE
    )
    require_equal(
        source_sha,
        candidate_source_sha,
        "workflow and candidate source SHA",
    )
    expected = f"PUBLISH:{proposal_sha256}"
    if not hmac.compare_digest(confirmation, expected):
        fail(
            "explicit operator confirmation must bind the exact proposal "
            "SHA-256"
        )
    forbidden = (
        "CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN",
        "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64",
        "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64",
        "CHUMMER_KEYLOCKER_API_KEY",
        "CHUMMER_KEYLOCKER_CLIENT_CERTIFICATE_BASE64",
    )
    present = [name for name in forbidden if os.environ.get(name)]
    if present:
        fail("publication job exposes forbidden approval/admin/signing authority")


def publication_journal_prepared_record(
    *,
    material: PublicationMaterial,
    authenticated_transports: Mapping[str, Any],
    hub_provider_authority: Mapping[str, Any],
) -> tuple[str, dict[str, Any]]:
    identity = {
        "candidateId": material.candidate["candidateId"],
        "releaseVersion": material.candidate["releaseVersion"],
        "sourceCommit": material.candidate["source"]["commit"],
        "candidateManifest": binding_bytes(
            material.candidate_bytes,
            material.candidate_path.relative_to(material.root).as_posix(),
            contractName=assembler.CANDIDATE_CONTRACT,
        ),
        "proposal": binding_bytes(
            material.proposal_bytes,
            material.proposal_path.relative_to(material.root).as_posix(),
            contractName=assembler.PROPOSAL_CONTRACT,
        ),
        "finalReceipt": binding_bytes(
            material.final_bytes,
            material.final_path.relative_to(material.root).as_posix(),
            contractName=assembler.FINAL_RECEIPT_CONTRACT,
        ),
        "providerHandoff": binding_bytes(
            material.handoff_bytes,
            material.handoff_path.relative_to(material.root).as_posix(),
            contractName=provider_auth.HANDOFF_CONTRACT,
        ),
        "topologyRetirement": binding_bytes(
            material.topology_bytes,
            "topology-retirement.json",
            contractName=TOPOLOGY_CONTRACT,
        ),
        "destinationPlan": binding_bytes(
            material.destination_plan_bytes,
            "destination-plan.json",
            contractName=DESTINATION_PLAN_CONTRACT,
        ),
        "providerTransportsSha256": sha256_bytes(
            canonical_json_bytes(authenticated_transports)
        ),
        "hubTopologyProviderSha256": sha256_bytes(
            canonical_json_bytes(hub_provider_authority)
        ),
        "previousManifest": material.destination_plan["previousManifest"],
        "destinations": [
            {"url": url, "sha256": digest, "sizeBytes": size}
            for url, digest, size in expected_destination_rows(material)
        ],
    }
    journal_id = sha256_bytes(canonical_json_bytes(identity))
    return journal_id, journal_record(
        journal_id,
        "prepared",
        transaction=identity,
    )


def require_local_candidate_unchanged(material: PublicationMaterial) -> None:
    for platform in assembler.PLATFORMS:
        artifact = material.platforms[platform]["artifact"]
        relative = assembler.safe_relative_path(
            artifact["relativePath"], f"{platform} post-publish artifact path"
        )
        current_snapshot = assembler.snapshot_relative(
            material.root,
            relative,
            f"{platform} transaction artifact",
            MAX_PUBLIC_FILE_BYTES,
            read_data=False,
        )
        if (
            current_snapshot.sha256,
            current_snapshot.size_bytes,
        ) != material.artifact_identities[platform]:
            fail(f"{platform} candidate artifact changed during publication")


def classify_live_publication(
    reader: DestinationReader,
    material: PublicationMaterial,
) -> str:
    topology_authority = material.destination_plan["topologyRetirementProof"]
    public_topology = destination_identity(
        reader,
        topology_authority["url"],
        MAX_JSON_BYTES,
    )
    require_equal(
        public_topology.sha256,
        topology_authority["sha256"],
        "live topology retirement proof SHA-256",
    )
    require_equal(
        public_topology.size_bytes,
        topology_authority["sizeBytes"],
        "live topology retirement proof size",
    )
    previous = material.destination_plan["previousManifest"]
    current = destination_identity(reader, previous["url"], MAX_JSON_BYTES)
    previous_sha = str(previous["sha256"])
    final_sha = str(
        material.destination_plan["manifests"]["canonical"]["sha256"]
    )
    if hmac.compare_digest(current.sha256, previous_sha):
        return "predecessor"
    if hmac.compare_digest(current.sha256, final_sha):
        return "candidate"
    fail(
        "live canonical manifest is neither the exact predecessor nor the "
        "exact candidate; publication state is mixed or drifted"
    )


def build_publication_receipt(
    *,
    material: PublicationMaterial,
    now: datetime,
    workflow_run_id: int,
    workflow_actor: str,
    authenticated_transports: Mapping[str, Any],
    hub_provider_authority: Mapping[str, Any],
    verified: Sequence[Mapping[str, Any]],
    transaction_mode: str,
    journal_id: str,
    prepared_journal_bytes: bytes,
    recovered_mutation_marker: bool,
) -> dict[str, Any]:
    return {
        "contractName": CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generatedAt": assembler.format_time(now),
        "status": "passed",
        "candidate": material.candidate,
        "candidateManifest": binding_bytes(
            material.candidate_bytes,
            material.candidate_path.relative_to(material.root).as_posix(),
            contractName=assembler.CANDIDATE_CONTRACT,
        ),
        "proposal": binding_bytes(
            material.proposal_bytes,
            material.proposal_path.relative_to(material.root).as_posix(),
            contractName=assembler.PROPOSAL_CONTRACT,
        ),
        "finalReceipt": binding_bytes(
            material.final_bytes,
            material.final_path.relative_to(material.root).as_posix(),
            contractName=assembler.FINAL_RECEIPT_CONTRACT,
        ),
        "providerHandoff": binding_bytes(
            material.handoff_bytes,
            material.handoff_path.relative_to(material.root).as_posix(),
            contractName=provider_auth.HANDOFF_CONTRACT,
        ),
        "topologyRetirement": binding_bytes(
            material.topology_bytes,
            "topology-retirement.json",
            contractName=TOPOLOGY_CONTRACT,
        ),
        "destinationPlan": binding_bytes(
            material.destination_plan_bytes,
            "destination-plan.json",
            contractName=DESTINATION_PLAN_CONTRACT,
        ),
        "platforms": {
            platform: {
                "artifact": material.platforms[platform]["artifact"],
                "signingReceipt": material.platforms[platform]["signingReceipt"],
                "integrityPolicy": material.platforms[platform]["integrityPolicy"],
            }
            for platform in assembler.PLATFORMS
        },
        "publicationWorkflow": {
            "repository": assembler.SOURCE_REPOSITORY,
            "ref": "refs/heads/main",
            "sha": material.candidate["source"]["commit"],
            "workflow": PUBLICATION_WORKFLOW,
            "environment": PUBLICATION_ENVIRONMENT,
            "runId": workflow_run_id,
            "runAttempt": 1,
            "actor": workflow_actor,
            "transactionMode": transaction_mode,
        },
        "transactionJournal": {
            "contractName": JOURNAL_CONTRACT,
            "contractVersion": JOURNAL_CONTRACT_VERSION,
            "journalId": journal_id,
            "preparedSha256": sha256_bytes(prepared_journal_bytes),
            "preparedSizeBytes": len(prepared_journal_bytes),
            "recoveredMutationMarker": recovered_mutation_marker,
        },
        "providerTransports": authenticated_transports,
        "hubTopologyProvider": hub_provider_authority,
        "publisher": {
            "path": CANONICAL_PUBLISHER,
            "topology": "committed-canonical-authority-only",
        },
        "destinations": list(verified),
        "provenanceAuthenticated": True,
        "releaseArtifactBytesAuthenticated": True,
        "signingAndNotarizationAuthenticated": True,
        "topologyRetirementAuthenticated": True,
        "destinationBytesVerified": True,
        "publicationAuthorized": True,
        "immutable": True,
    }


def execute_transaction(
    *,
    material: PublicationMaterial,
    reader: DestinationReader,
    publisher: Publisher,
    output: Path,
    now: datetime | None = None,
    clock: Callable[[], datetime] | None = None,
    workflow_run_id: int,
    workflow_actor: str,
    authenticated_transports: Mapping[str, Any] | None = None,
    transport_reauthenticate: Callable[[], Mapping[str, Any]] | None = None,
    hub_provider_authority: Mapping[str, Any] | None = None,
    hub_provider_reauthenticate: Callable[[], Mapping[str, Any]] | None = None,
    freshness_revalidate: (
        Callable[[datetime], PublicationMaterial] | None
    ) = None,
    journal: Path | None = None,
    after_publisher_return: Callable[[], None] | None = None,
) -> Mapping[str, Any]:
    def observed_at(label: str) -> datetime:
        if clock is not None:
            return read_clock(clock, label)
        if now is None:
            fail("publication transaction requires a live clock")
        return now

    def reauthenticate_before_effect(label: str) -> datetime:
        if transport_reauthenticate is not None:
            refreshed_transports = transport_reauthenticate()
            require_equal(
                refreshed_transports,
                authenticated_transports,
                f"{label} UI provider reauthentication",
            )
        refreshed_hub = hub_provider_reauthenticate()
        require_equal(
            refreshed_hub,
            hub_provider_authority,
            f"{label} Hub provider reauthentication",
        )
        boundary_now = observed_at(label)
        if freshness_revalidate is not None:
            refreshed = freshness_revalidate(boundary_now)
            require_equal(
                refreshed,
                material,
                f"{label} material freshness revalidation",
            )
        return boundary_now

    if output.exists() or output.is_symlink():
        fail("publication receipt output already exists; refusing rerun")
    if not authenticated_transports:
        fail("provider artifact transports were not independently authenticated")
    if not hub_provider_authority or hub_provider_reauthenticate is None:
        fail("Hub topology provider authority was not authenticated")
    journal_root = (
        journal
        if journal is not None
        else output.parent / f".{output.stem}.journal"
    )
    journal_id, prepared_record = publication_journal_prepared_record(
        material=material,
        authenticated_transports=authenticated_transports,
        hub_provider_authority=hub_provider_authority,
    )
    prepared_journal_bytes, _prepared_created = (
        create_or_validate_journal_record(
            journal_root / "prepared.json",
            prepared_record,
            "publication transaction prepared journal",
        )
    )
    live_state = classify_live_publication(reader, material)
    mutation_record_path = journal_root / "mutation-started.json"
    if live_state == "predecessor":
        reauthenticate_before_effect("immediately before publication mutation")
        mutation_record = journal_record(
            journal_id,
            "mutation-started",
            preparedSha256=sha256_bytes(prepared_journal_bytes),
            publisherPath=CANONICAL_PUBLISHER,
        )
        _mutation_bytes, mutation_created = create_or_validate_journal_record(
            mutation_record_path,
            mutation_record,
            "publication transaction mutation journal",
        )
        if not mutation_created:
            fail(
                "a prior publication mutation did not reach an exact live "
                "candidate; refusing to republish a possibly partial state"
            )
        publisher.publish(
            material.public_bundle,
            material.destination_plan["previousManifest"]["url"],
        )
        if after_publisher_return is not None:
            after_publisher_return()
        transaction_mode = "canonical-publish"
    else:
        reauthenticate_before_effect("before exact-live adoption verification")
        adoption_record = journal_record(
            journal_id,
            "exact-live-candidate-adoption-started",
            preparedSha256=sha256_bytes(prepared_journal_bytes),
        )
        create_or_validate_journal_record(
            journal_root / "adoption-started.json",
            adoption_record,
            "publication transaction adoption journal",
        )
        transaction_mode = "exact-live-candidate-adoption"

    require_local_candidate_unchanged(material)
    verified = verify_destinations(reader, material)
    final_hub_provider_authority = hub_provider_authority
    verified_record = journal_record(
        journal_id,
        "destinations-verified",
        preparedSha256=sha256_bytes(prepared_journal_bytes),
        destinations=list(verified),
        hubTopologyProviderSha256=sha256_bytes(
            canonical_json_bytes(final_hub_provider_authority)
        ),
    )
    create_or_validate_journal_record(
        journal_root / "destinations-verified.json",
        verified_record,
        "publication transaction verified journal",
    )
    receipt_now = reauthenticate_before_effect(
        "immediately before publication authorization receipt"
    )
    receipt = build_publication_receipt(
        material=material,
        now=receipt_now,
        workflow_run_id=workflow_run_id,
        workflow_actor=workflow_actor,
        authenticated_transports=authenticated_transports,
        hub_provider_authority=final_hub_provider_authority,
        verified=verified,
        transaction_mode=transaction_mode,
        journal_id=journal_id,
        prepared_journal_bytes=prepared_journal_bytes,
        recovered_mutation_marker=(
            transaction_mode == "exact-live-candidate-adoption"
            and mutation_record_path.exists()
        ),
    )
    provider_auth.write_once(output, provider_auth.immutable_json_bytes(receipt))
    return receipt


def command_execute(args: argparse.Namespace) -> int:
    repository_root = Path(__file__).resolve().parents[2]
    clock = current_time
    try:
        token = os.environ.pop(args.publication_token_env, "")
        github_token = os.environ.get(args.github_token_env, "")
        if not github_token:
            fail("read-only GitHub transport authority is missing")
        github_client = provider_auth.GitHubApi(github_token)
        prepared = prepare_publication_inputs(
            github_client,
            repository_root=repository_root,
            publication_root=Path(args.publication_root),
            source_sha=args.source_sha,
            provider_handoff_artifact_id=args.provider_handoff_artifact_id,
            provider_handoff_artifact_digest=(
                args.provider_handoff_artifact_digest
            ),
            provider_handoff_artifact_name=args.provider_handoff_artifact_name,
            publication_input_artifact_id=args.publication_input_artifact_id,
            publication_input_artifact_digest=(
                args.publication_input_artifact_digest
            ),
            publication_input_artifact_name=(
                args.publication_input_artifact_name
            ),
            clock=clock,
        )
        material = prepared.material
        proposal_sha = sha256_bytes(material.proposal_bytes)
        require_execution_context(
            confirmation=args.confirmation,
            proposal_sha256=proposal_sha,
            repository=args.repository,
            ref=args.ref,
            run_attempt=args.run_attempt,
            actor=args.actor,
            triggering_actor=args.triggering_actor,
            environment_name=args.environment,
            source_sha=args.source_sha,
            candidate_source_sha=str(material.candidate["source"]["commit"]),
        )
        hub_token = os.environ.get(args.hub_token_env, "")
        if not hub_token:
            fail("separate read-only Hub provider authority is missing")
        authorities = [github_token, hub_token, token]
        if (
            any(not authority for authority in authorities)
            or len({sha256_bytes(authority.encode()) for authority in authorities})
            != len(authorities)
        ):
            fail(
                "publication, UI provider, and Hub provider authorities must "
                "be separate"
            )
        hub_client = provider_auth.GitHubApi(
            hub_token, repository=HUB_REPOSITORY
        )

        def read_hub_authority() -> Mapping[str, Any]:
            authority = authenticate_hub_topology_provider(
                hub_client,
                material=material,
                artifact_id=args.hub_topology_artifact_id,
                artifact_name=args.hub_topology_artifact_name,
                expected_digest=args.hub_topology_artifact_digest,
                clock=clock,
            )
            require_equal(
                authority,
                prepared.assembly_receipt["upstreamArtifacts"][
                    "hubTopology"
                ],
                "assembly receipt exact Hub topology archive",
            )
            return authority

        hub_authority = read_hub_authority()
        def revalidate_material(observed_now: datetime) -> PublicationMaterial:
            return load_material(
                publication_root=material.root,
                handoff_path=material.handoff_path,
                repository_root=repository_root,
                now=observed_now,
            )

        def read_ui_authority() -> Mapping[str, Any]:
            return reauthenticate_publication_inputs(
                github_client,
                prepared=prepared,
                source_sha=args.source_sha,
                provider_handoff_artifact_id=(
                    args.provider_handoff_artifact_id
                ),
                provider_handoff_artifact_digest=(
                    args.provider_handoff_artifact_digest
                ),
                provider_handoff_artifact_name=(
                    args.provider_handoff_artifact_name
                ),
                publication_input_artifact_id=(
                    args.publication_input_artifact_id
                ),
                publication_input_artifact_digest=(
                    args.publication_input_artifact_digest
                ),
                publication_input_artifact_name=(
                    args.publication_input_artifact_name
                ),
                clock=clock,
            )

        publisher = CanonicalHttpPublisher(
            repository_root=repository_root,
            publication_token=token,
        )
        execute_transaction(
            material=material,
            reader=PublicHttpsReader(),
            publisher=publisher,
            output=Path(args.output),
            clock=clock,
            workflow_run_id=args.run_id,
            workflow_actor=args.actor,
            authenticated_transports=prepared.transports,
            transport_reauthenticate=read_ui_authority,
            hub_provider_authority=hub_authority,
            hub_provider_reauthenticate=read_hub_authority,
            freshness_revalidate=revalidate_material,
            journal=Path(args.journal),
        )
    except (
        ContractError,
        assembler.ContractError,
        provider_auth.ContractError,
        OSError,
        ValueError,
    ) as exc:
        print(f"global flagship publication blocked: {exc}", file=sys.stderr)
        return 1
    print(args.output)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Execute the protected global flagship publication transaction."
    )
    parser.add_argument("--publication-root", required=True)
    parser.add_argument(
        "--provider-handoff-artifact-id",
        type=assembler.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--provider-handoff-artifact-digest", required=True)
    parser.add_argument("--provider-handoff-artifact-name", required=True)
    parser.add_argument(
        "--publication-input-artifact-id",
        type=assembler.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--publication-input-artifact-name", required=True)
    parser.add_argument("--publication-input-artifact-digest", required=True)
    parser.add_argument(
        "--hub-topology-artifact-id",
        type=assembler.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--hub-topology-artifact-name", required=True)
    parser.add_argument("--hub-topology-artifact-digest", required=True)
    parser.add_argument("--confirmation", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--run-id", type=assembler.bounded_integer(1, 2**63 - 1), required=True)
    parser.add_argument("--run-attempt", required=True)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--triggering-actor", required=True)
    parser.add_argument("--environment", required=True)
    parser.add_argument(
        "--publication-token-env",
        default="CHUMMER_FLAGSHIP_PUBLICATION_TOKEN",
    )
    parser.add_argument("--github-token-env", default="GITHUB_TOKEN")
    parser.add_argument(
        "--hub-token-env",
        default="CHUMMER_FLAGSHIP_HUB_ACTIONS_READ_TOKEN",
    )
    parser.add_argument("--journal", required=True)
    parser.add_argument("--output", required=True)
    parser.set_defaults(handler=command_execute)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    return int(args.handler(args))


if __name__ == "__main__":
    raise SystemExit(main())
