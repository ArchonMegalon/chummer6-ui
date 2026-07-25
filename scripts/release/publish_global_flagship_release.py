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
from typing import Any, Mapping, Protocol, Sequence
from urllib.parse import quote, urlsplit

import assemble_global_flagship_release as assembler
import authenticate_global_flagship_release as provider_auth


CONTRACT = "chummer6-ui.global-flagship-publication-receipt.v1"
CONTRACT_VERSION = 1
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
    for field in ("committedBoundaryReceipt", "postMarkerConvergenceReceipt"):
        receipt = exact_dict(
            payload[field], {"sha256", "sizeBytes"}, f"topology {field}"
        )
        assembler.require_sha256(receipt["sha256"], f"topology {field}.sha256")
        assembler.require_positive_integer(
            receipt["sizeBytes"], f"topology {field}.sizeBytes"
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
    topology = load_json_bytes(topology_bytes, "topology retirement proof")
    validate_topology_retirement(
        topology, now=now, publisher_bytes=publisher_bytes
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
) -> Mapping[str, Any]:
    if output.exists() or output.is_symlink():
        fail("publication receipt output already exists; refusing rerun")
    if not authenticated_transports:
        fail("provider artifact transports were not independently authenticated")
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
    before = destination_identity(reader, previous["url"], MAX_JSON_BYTES)
    require_equal(
        before.sha256,
        previous["sha256"],
        "live predecessor manifest SHA-256",
    )
    final_manifest_sha = material.destination_plan["manifests"]["canonical"][
        "sha256"
    ]
    if hmac.compare_digest(before.sha256, final_manifest_sha):
        fail("live destination already contains the candidate; refusing rerun")

    publisher.publish(material.public_bundle, previous["url"])

    # Re-open all candidate artifacts after the external call.  A local
    # mutation during publication invalidates the result even if the target
    # happened to receive the pre-mutation bytes.
    for platform in assembler.PLATFORMS:
        artifact = material.platforms[platform]["artifact"]
        relative = assembler.safe_relative_path(
            artifact["relativePath"], f"{platform} post-publish artifact path"
        )
        current_snapshot = assembler.snapshot_relative(
            material.root,
            relative,
            f"{platform} post-publish artifact",
            MAX_PUBLIC_FILE_BYTES,
            read_data=False,
        )
        if (
            current_snapshot.sha256,
            current_snapshot.size_bytes,
        ) != material.artifact_identities[platform]:
            fail(f"{platform} candidate artifact changed during publication")

    verified = verify_destinations(reader, material)
    receipt = {
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
        },
        "providerTransports": authenticated_transports,
        "publisher": {
            "path": CANONICAL_PUBLISHER,
            "topology": "committed-canonical-authority-only",
        },
        "destinations": verified,
        "provenanceAuthenticated": True,
        "releaseArtifactBytesAuthenticated": True,
        "signingAndNotarizationAuthenticated": True,
        "topologyRetirementAuthenticated": True,
        "destinationBytesVerified": True,
        "publicationAuthorized": True,
        "immutable": True,
    }
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
        token = os.environ.get(args.publication_token_env, "")
        if token and hmac.compare_digest(token, github_token):
            fail("publication and read-only provider authorities must be separate")
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
    parser.add_argument("--output", required=True)
    parser.set_defaults(handler=command_execute)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    return int(args.handler(args))


if __name__ == "__main__":
    raise SystemExit(main())
