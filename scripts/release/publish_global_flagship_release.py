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
PUBLICATION_ENVIRONMENT = "global-flagship-protected-publication"
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
) -> None:
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
    downloads = payload.get("downloads")
    if not isinstance(downloads, list):
        fail(f"{label}.downloads must be an array")
    expected_names = {
        str(platforms[platform]["artifact"]["fileName"]): platform
        for platform in assembler.PLATFORMS
    }
    matches: dict[str, Mapping[str, Any]] = {}
    for index, raw in enumerate(downloads):
        if not isinstance(raw, dict):
            fail(f"{label}.downloads[{index}] must be an object")
        file_name = raw.get("fileName")
        if file_name not in expected_names:
            continue
        if str(file_name) in matches:
            fail(f"{label} contains a duplicate candidate artifact row")
        matches[str(file_name)] = raw
    require_equal(
        set(matches), set(expected_names), f"{label} candidate artifact set"
    )
    for file_name, platform in expected_names.items():
        row = matches[file_name]
        artifact = platforms[platform]["artifact"]
        require_equal(
            row.get("platformId"), platform, f"{label} {platform}.platformId"
        )
        require_equal(
            row.get("url"),
            f"{PUBLIC_BASE_URL}/files/{quote(file_name)}",
            f"{label} {platform}.url",
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
    for key, file_name, expected_url in (
        ("canonical", "RELEASE_CHANNEL.generated.json", PUBLIC_MANIFEST_URL),
        ("releases", "releases.json", PUBLIC_RELEASES_URL),
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
        validate_public_manifest_contract(
            data,
            label=f"public bundle {file_name}",
            candidate=candidate,
            platforms=platforms,
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
        committed_boundary_bytes=committed_boundary_bytes,
        post_marker_convergence_bytes=post_marker_convergence_bytes,
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
    now: datetime,
    label: str,
) -> Mapping[str, Any]:
    assembler.require_positive_integer(artifact_id, f"{label} ID")
    provider_auth.require_api_string(
        expected_digest,
        f"{label} expected digest",
        provider_auth.ARTIFACT_DIGEST_RE,
    )
    detail_path = provider_auth.repository_api_path(
        f"/actions/artifacts/{artifact_id}"
    )
    response = client.get_json(detail_path)
    provider_auth.require_unpaginated(response.headers, f"{label} metadata")
    metadata = provider_auth.validate_artifact_metadata(
        response.value,
        expected_id=artifact_id,
        expected_name=expected_name,
        expected_run_id=expected_run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=label,
    )
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
        now=now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=f"{label} metadata recheck",
    )
    require_equal(final_metadata, metadata, f"{label} metadata recheck")
    return {"artifact": metadata, "run": run}


def authenticate_transports(
    client: provider_auth.ProviderReader,
    *,
    material: PublicationMaterial,
    provider_handoff_artifact_id: int,
    provider_handoff_artifact_digest: str,
    provider_handoff_artifact_name: str,
    publication_input_artifact_id: int,
    publication_input_artifact_digest: str,
    now: datetime,
) -> Mapping[str, Any]:
    repository = provider_auth.validate_repository(
        client.get_json(provider_auth.repository_api_path(""))
    )
    source_sha = str(material.candidate["source"]["commit"])
    handoff_match = HANDOFF_ARTIFACT_RE.fullmatch(
        provider_handoff_artifact_name
    )
    if handoff_match is None:
        fail("provider handoff artifact name is malformed")
    transport = (
        load_json_bytes(material.handoff_bytes, "provider handoff").get(
            "transportArtifact"
        )
    )
    transport_id = (
        transport.get("id") if isinstance(transport, dict) else None
    )
    require_equal(
        int(handoff_match.group(1)),
        transport_id,
        "provider handoff artifact input-ID name binding",
    )
    handoff_run_id = int(handoff_match.group(2))
    handoff = authenticate_one_transport(
        client,
        artifact_id=provider_handoff_artifact_id,
        expected_digest=provider_handoff_artifact_digest,
        expected_name=provider_handoff_artifact_name,
        expected_run_id=handoff_run_id,
        source_sha=source_sha,
        repository_id=int(repository["id"]),
        maximum_bytes=provider_auth.MAX_APPROVAL_ARTIFACT_BYTES,
        workflow=PROVIDER_HANDOFF_WORKFLOW,
        actor=None,
        now=now,
        label="provider handoff artifact",
    )
    producer = material.candidate["producer"]
    publication_name = (
        f"{PUBLICATION_INPUT_PREFIX}{material.candidate['candidateId']}"
    )
    publication = authenticate_one_transport(
        client,
        artifact_id=publication_input_artifact_id,
        expected_digest=publication_input_artifact_digest,
        expected_name=publication_name,
        expected_run_id=int(producer["runId"]),
        source_sha=source_sha,
        repository_id=int(repository["id"]),
        maximum_bytes=MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        workflow=str(producer["workflow"]),
        actor=str(producer["actor"]),
        now=now,
        label="publication input artifact",
    )
    return {
        "repository": repository,
        "providerHandoff": handoff,
        "publicationInput": publication,
    }


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
    now: datetime,
) -> Mapping[str, Any]:
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
    hub_source_sha = assembler.require_string(
        topology_source.get("commit"),
        "topology Hub source commit",
        assembler.COMMIT_RE,
    )
    if len(set(hub_source_sha)) < 4 or hub_source_sha == "0" * 40:
        fail("topology Hub source commit is synthetic")

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
        now=now,
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
    require_equal(
        workflow_run.get("head_sha"),
        hub_source_sha,
        "Hub topology artifact workflow source",
    )
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
        <= now + timedelta(minutes=5)
    ):
        fail("Hub topology artifact timestamps do not contain the proof")

    run_response = client.get_json(hub_api_path(f"/actions/runs/{run_id}"))
    provider_auth.require_unpaginated(
        run_response.headers, "Hub topology workflow run"
    )
    run = validate_hub_run(
        run_response.value,
        repository_id=repository_id,
        run_id=run_id,
        source_sha=hub_source_sha,
        label="Hub topology workflow run",
    )
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
        source_sha=hub_source_sha,
        label="Hub topology workflow attempt",
    )
    require_equal(attempt, run, "Hub topology workflow attempt")

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
    require_equal(commit.get("sha"), hub_source_sha, "Hub main branch commit")

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

    artifact_recheck = client.get_json(artifact_path)
    provider_auth.require_unpaginated(
        artifact_recheck.headers, "Hub topology artifact final recheck"
    )
    require_equal(
        artifact_recheck.value,
        artifact_response.value,
        "Hub topology artifact final recheck",
    )
    return {
        "repository": {
            "id": repository_id,
            "fullName": HUB_REPOSITORY,
            "defaultBranch": "main",
        },
        "source": {
            "ref": "refs/heads/main",
            "sha": hub_source_sha,
            "protected": True,
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
                "CHUMMER_RELEASE_UPLOAD_TOKEN": self._token,
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
        completed = subprocess.run(
            ["bash", str(publisher), str(bundle)],
            cwd=self._repository_root,
            env=environment,
            check=False,
        )
        self._token = ""
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
            "handoff.json",
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
            "handoff.json",
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
    now: datetime,
    workflow_run_id: int,
    workflow_actor: str,
    authenticated_transports: Mapping[str, Any] | None = None,
    hub_provider_authority: Mapping[str, Any] | None = None,
    hub_provider_reauthenticate: Callable[[], Mapping[str, Any]] | None = None,
    journal: Path | None = None,
    after_publisher_return: Callable[[], None] | None = None,
) -> Mapping[str, Any]:
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
    final_hub_provider_authority = hub_provider_reauthenticate()
    require_equal(
        final_hub_provider_authority,
        hub_provider_authority,
        "late Hub topology provider reauthentication",
    )
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
    receipt = build_publication_receipt(
        material=material,
        now=now,
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
    now = datetime.now(UTC).replace(microsecond=0)
    repository_root = Path(__file__).resolve().parents[2]
    try:
        material = load_material(
            publication_root=Path(args.publication_root),
            handoff_path=Path(args.provider_handoff),
            repository_root=repository_root,
            now=now,
        )
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
        github_token = os.environ.get(args.github_token_env, "")
        if not github_token:
            fail("read-only GitHub transport authority is missing")
        transports = authenticate_transports(
            provider_auth.GitHubApi(github_token),
            material=material,
            provider_handoff_artifact_id=args.provider_handoff_artifact_id,
            provider_handoff_artifact_digest=(
                args.provider_handoff_artifact_digest
            ),
            provider_handoff_artifact_name=args.provider_handoff_artifact_name,
            publication_input_artifact_id=args.publication_input_artifact_id,
            publication_input_artifact_digest=(
                args.publication_input_artifact_digest
            ),
            now=now,
        )
        hub_token = os.environ.get(args.hub_token_env, "")
        if not hub_token:
            fail("separate read-only Hub provider authority is missing")
        token = os.environ.get(args.publication_token_env, "")
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
            return authenticate_hub_topology_provider(
                hub_client,
                material=material,
                artifact_id=args.hub_topology_artifact_id,
                artifact_name=args.hub_topology_artifact_name,
                expected_digest=args.hub_topology_artifact_digest,
                now=now,
            )

        hub_authority = read_hub_authority()
        publisher = CanonicalHttpPublisher(
            repository_root=repository_root,
            publication_token=token,
        )
        execute_transaction(
            material=material,
            reader=PublicHttpsReader(),
            publisher=publisher,
            output=Path(args.output),
            now=now,
            workflow_run_id=args.run_id,
            workflow_actor=args.actor,
            authenticated_transports=transports,
            hub_provider_authority=hub_authority,
            hub_provider_reauthenticate=read_hub_authority,
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
    parser.add_argument("--provider-handoff", required=True)
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
