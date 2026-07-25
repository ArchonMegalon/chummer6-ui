#!/usr/bin/env python3
"""Assemble one provider-bound, nonpublishing flagship publication input.

This lane runs only after the metadata-only provider handoff, three independent
approvals, and the Hub retirement proof exist.  Every upstream GitHub artifact
archive is downloaded through the provider API and hashed before extraction.
The output remains nonpublishing; its receipt gives the later protected
publication transaction a causally valid, independently authenticated input.
"""

from __future__ import annotations

import argparse
import hmac
import os
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Mapping, Sequence

import assemble_global_flagship_release as flagship
import authenticate_global_flagship_release as provider
import publish_global_flagship_release as publication


PUBLICATION_INPUT_BOUND_AUTHORITY_ENTRIES = (
    2
    + len(flagship.REQUIRED_APPROVAL_ROLES)
    + len(publication.HUB_PROOF_ENTRIES)
)
PUBLICATION_INPUT_DUPLICATED_BUNDLE_ENTRIES = 2 * len(flagship.PLATFORMS)
PUBLICATION_INPUT_GENERATED_JSON_PATHS = (
    publication.DESTINATION_INTENT_PATH,
    publication.CHANNEL_PROMOTION_PATH,
    "public-bundle/RELEASE_CHANNEL.generated.json",
    "public-bundle/releases.json",
    "destination-plan.json",
    publication.ASSEMBLY_RECEIPT_NAME,
)
PUBLICATION_INPUT_GENERATED_JSON_ENTRIES = len(
    PUBLICATION_INPUT_GENERATED_JSON_PATHS
)
PUBLICATION_INPUT_ENTRY_OVERHEAD = (
    PUBLICATION_INPUT_BOUND_AUTHORITY_ENTRIES
    + PUBLICATION_INPUT_DUPLICATED_BUNDLE_ENTRIES
    + PUBLICATION_INPUT_GENERATED_JSON_ENTRIES
)
PUBLICATION_INPUT_RESERVED_AUTHORITY_BYTES = (
    publication.MAX_HUB_PROOF_ARTIFACT_BYTES
    + PUBLICATION_INPUT_GENERATED_JSON_ENTRIES * publication.MAX_JSON_BYTES
)
MAX_CANDIDATE_ENTRIES = (
    publication.MAX_PUBLICATION_INPUT_ENTRIES
    - PUBLICATION_INPUT_ENTRY_OVERHEAD
)
PRODUCER_PROPOSAL_PATH = "GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json"
PRODUCER_CANDIDATE_PATH = (
    "candidate/GLOBAL_FLAGSHIP_CANDIDATE.generated.json"
)
PUBLICATION_CANDIDATE_PATH = "GLOBAL_FLAGSHIP_CANDIDATE.generated.json"
REGISTRY_COMMIT = "e3e89b948b4323838bfb16572f080a7a500b5145"
REGISTRY_MATERIALIZER = "scripts/materialize_public_release_channel.py"
REGISTRY_VERIFIER = "scripts/verify_public_release_channel.py"


def fail(message: str) -> None:
    raise publication.ContractError(message)


@dataclass(frozen=True)
class PublicationInputProjection:
    entry_count: int
    expanded_bytes: int


def validate_publication_input_projection(
    *,
    candidate_entry_count: int,
    candidate_expanded_bytes: int,
    duplicated_public_bundle_bytes: int,
    known_authority_bytes: int,
) -> PublicationInputProjection:
    values = {
        "candidate entry count": candidate_entry_count,
        "candidate expanded bytes": candidate_expanded_bytes,
        "duplicated public-bundle bytes": duplicated_public_bundle_bytes,
        "known authority bytes": known_authority_bytes,
    }
    for label, value in values.items():
        if type(value) is not int or value < 0:
            fail(f"projected publication input {label} is invalid")
    projection = PublicationInputProjection(
        entry_count=(
            candidate_entry_count + PUBLICATION_INPUT_ENTRY_OVERHEAD
        ),
        expanded_bytes=(
            candidate_expanded_bytes
            + duplicated_public_bundle_bytes
            + known_authority_bytes
            + PUBLICATION_INPUT_RESERVED_AUTHORITY_BYTES
        ),
    )
    if projection.entry_count > publication.MAX_PUBLICATION_INPUT_ENTRIES:
        fail("projected publication input exceeds publisher entry boundary")
    if (
        projection.expanded_bytes
        > publication.MAX_PUBLICATION_INPUT_EXPANDED_BYTES
    ):
        fail(
            "projected publication input expands beyond publisher byte "
            "boundary"
        )
    return projection


def read_projected_candidate_reference(
    entries: Mapping[str, bytes],
    reference_root: PurePosixPath,
    value: object,
    *,
    label: str,
    maximum_bytes: int,
) -> tuple[PurePosixPath, bytes]:
    if type(maximum_bytes) is not int or maximum_bytes < 1:
        fail(f"{label} byte boundary is invalid")
    reference = value if isinstance(value, dict) else {}
    relative = flagship.safe_relative_path(
        reference.get("path"), f"{label}.path"
    )
    combined = (reference_root / PurePosixPath(relative)).as_posix()
    normalized = flagship.safe_relative_path(
        combined, f"{label} publication path"
    )
    data = entries.get(normalized)
    if data is None:
        fail(f"{label} is absent from the candidate payload")
    if not data or len(data) > maximum_bytes:
        fail(f"{label} has an invalid size")
    publication.require_equal(
        reference.get("sha256"),
        publication.sha256_bytes(data),
        f"{label}.sha256",
    )
    publication.require_equal(
        reference.get("sizeBytes"),
        len(data),
        f"{label}.sizeBytes",
    )
    return PurePosixPath(normalized), data


def projected_public_bundle_duplicate_bytes(
    *,
    entries: Mapping[str, bytes],
    candidate_relative: str,
    candidate: Mapping[str, Any],
) -> int:
    candidate_root = PurePosixPath(candidate_relative).parent
    raw_platforms = candidate.get("platforms")
    if not isinstance(raw_platforms, dict):
        fail("projected candidate manifest platforms are missing")
    total = 0
    for platform in flagship.PLATFORMS:
        raw_platform = raw_platforms.get(platform)
        if not isinstance(raw_platform, dict):
            fail(f"projected candidate {platform} platform is missing")
        _, artifact_bytes = read_projected_candidate_reference(
            entries,
            candidate_root,
            raw_platform.get("artifact"),
            label=f"projected {platform} candidate artifact",
            maximum_bytes=publication.MAX_PUBLIC_FILE_BYTES,
        )
        total += len(artifact_bytes)
        _, adapter_bytes = read_projected_candidate_reference(
            entries,
            candidate_root,
            raw_platform.get("nativeE2eReceipt"),
            label=f"projected {platform} native E2E adapter",
            maximum_bytes=flagship.MAX_JSON_BYTES,
        )
        adapter = publication.load_json_bytes(
            adapter_bytes, f"projected {platform} native E2E adapter"
        )
        checks = adapter.get("checks")
        clean = checks.get("cleanInstall") if isinstance(checks, dict) else {}
        if not isinstance(clean, dict):
            fail(
                f"projected {platform} native E2E clean-install check is "
                "missing"
            )
        rich_path, rich_bytes = read_projected_candidate_reference(
            entries,
            candidate_root,
            clean.get("evidence"),
            label=f"projected {platform} rich native lifecycle evidence",
            maximum_bytes=flagship.MAX_EVIDENCE_BYTES,
        )
        rich = publication.load_json_bytes(
            rich_bytes,
            f"projected {platform} rich native lifecycle evidence",
        )
        if platform == "macos":
            references = rich.get("references")
            if not isinstance(references, dict):
                fail("projected macOS aggregate references are missing")
            startup_root = candidate_root
            startup_reference = references.get("cleanStartupReceipt")
        else:
            core_workflow = rich.get("coreWorkflow")
            candidate_workflow = (
                core_workflow.get("candidate")
                if isinstance(core_workflow, dict)
                else {}
            )
            if not isinstance(candidate_workflow, dict):
                fail(
                    f"projected {platform} lifecycle candidate workflow is "
                    "missing"
                )
            startup_root = rich_path.parent
            startup_reference = candidate_workflow.get("startupReceipt")
        _, startup_bytes = read_projected_candidate_reference(
            entries,
            startup_root,
            startup_reference,
            label=f"projected {platform} candidate startup receipt",
            maximum_bytes=flagship.MAX_JSON_BYTES,
        )
        total += len(startup_bytes)
    return total


def projected_publication_input_added_paths(
    *,
    final_relative: str,
    platforms: Mapping[str, Any],
) -> tuple[str, ...]:
    paths = [
        final_relative,
        "provider-handoff.json",
        *(
            f"approvals/{role}/approval.json"
            for role in flagship.REQUIRED_APPROVAL_ROLES
        ),
        "topology-retirement.json",
        "committed-boundary-receipt.json",
        "post-marker-convergence-receipt.json",
    ]
    for platform in flagship.PLATFORMS:
        row = platforms.get(platform)
        artifact = row.get("artifact") if isinstance(row, dict) else {}
        if not isinstance(artifact, dict):
            fail(f"projected {platform} proposal artifact is missing")
        file_name = flagship.safe_relative_path(
            artifact.get("fileName"),
            f"projected {platform} public artifact file name",
        )
        paths.append(f"public-bundle/files/{file_name}")
        paths.append(
            "public-bundle/startup-smoke/startup-smoke-avalonia-"
            f"{flagship.POLICIES[platform].rid}.receipt.json"
        )
    paths.extend(PUBLICATION_INPUT_GENERATED_JSON_PATHS)

    normalized_paths: list[str] = []
    for path in paths:
        normalized = flagship.safe_relative_path(
            path, "projected publication-input path"
        )
        if normalized != path:
            fail("projected publication-input path is not canonical")
        publication.require_publication_input_entry_name(
            normalized, "projected publication-input path"
        )
        normalized_paths.append(normalized)
    if (
        len(normalized_paths) != PUBLICATION_INPUT_ENTRY_OVERHEAD
        or len(set(normalized_paths)) != len(normalized_paths)
    ):
        fail("projected publication-input path inventory is invalid")
    return tuple(normalized_paths)


def write_entry(root: Path, relative: str, data: bytes, label: str) -> None:
    normalized = flagship.safe_relative_path(relative, f"{label} path")
    if normalized != relative:
        fail(f"{label} path is not canonical")
    publication.require_publication_input_entry_name(
        normalized, f"{label} path"
    )
    target = root / PurePosixPath(relative)
    if target.exists() or target.is_symlink():
        existing = publication.read_regular_file(
            target, label, publication.MAX_PUBLIC_FILE_BYTES
        )
        if not hmac.compare_digest(existing, data):
            fail(f"{label} collides with different candidate bytes")
        return
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
                    fail(f"{label} could not be written completely")
                view = view[written:]
            os.fchmod(descriptor, 0o600)
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    except FileExistsError:
        fail(f"{label} collided while it was materialized")
    except OSError as exc:
        fail(f"{label} cannot be materialized: {exc}")


def raw_artifact(
    client: provider.ProviderReader,
    *,
    api_path: str,
    artifact_id: int,
    expected_name: str,
    expected_digest: str,
    repository_id: int,
    source_sha: str,
    maximum_bytes: int,
    clock: Callable[[], datetime],
    label: str,
) -> tuple[dict[str, Any], bytes]:
    metadata_now = publication.read_clock(clock, f"{label} metadata read")
    response = client.get_json(api_path)
    provider.require_unpaginated(response.headers, f"{label} metadata")
    value = response.value if isinstance(response.value, dict) else {}
    workflow_run = value.get("workflow_run")
    if not isinstance(workflow_run, dict):
        fail(f"{label}.workflow_run is missing")
    run_id = provider.require_api_integer(
        workflow_run.get("id"), f"{label}.workflow_run.id"
    )
    metadata = provider.validate_artifact_metadata(
        value,
        expected_id=artifact_id,
        expected_name=expected_name,
        expected_run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=metadata_now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=label,
    )
    archive = provider.download_authenticated_artifact(
        client,
        metadata=metadata,
        maximum_bytes=maximum_bytes,
        label=label,
    )
    recheck_now = publication.read_clock(
        clock, f"{label} metadata recheck"
    )
    recheck = client.get_json(api_path)
    provider.require_unpaginated(
        recheck.headers, f"{label} metadata recheck"
    )
    rechecked = provider.validate_artifact_metadata(
        recheck.value,
        expected_id=artifact_id,
        expected_name=expected_name,
        expected_run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=recheck_now,
        maximum_bytes=maximum_bytes,
        expected_digest=expected_digest,
        label=f"{label} metadata recheck",
    )
    publication.require_equal(rechecked, metadata, f"{label} metadata recheck")
    return metadata, archive


def raw_hub_artifact(
    client: provider.ProviderReader,
    *,
    artifact_id: int,
    expected_name: str,
    expected_digest: str,
    repository_id: int,
    source_sha: str,
    clock: Callable[[], datetime],
) -> bytes:
    path = publication.hub_api_path(f"/actions/artifacts/{artifact_id}")
    metadata_now = publication.read_clock(
        clock, "assembly Hub artifact metadata read"
    )
    response = client.get_json(path)
    provider.require_unpaginated(
        response.headers, "assembly Hub artifact metadata"
    )
    artifact = response.value if isinstance(response.value, dict) else {}
    publication.require_equal(
        artifact.get("id"), artifact_id, "assembly Hub artifact.id"
    )
    publication.require_equal(
        artifact.get("name"),
        expected_name,
        "assembly Hub artifact.name",
    )
    publication.require_equal(
        artifact.get("digest"),
        expected_digest,
        "assembly Hub artifact.digest",
    )
    provider.require_api_string(
        expected_digest,
        "assembly Hub artifact expected digest",
        provider.ARTIFACT_DIGEST_RE,
    )
    size_bytes = provider.require_api_integer(
        artifact.get("size_in_bytes"),
        "assembly Hub artifact.size_in_bytes",
    )
    if size_bytes > publication.MAX_HUB_PROOF_ARTIFACT_BYTES:
        fail("assembly Hub artifact exceeds its byte boundary")
    publication.require_equal(
        artifact.get("expired"), False, "assembly Hub artifact.expired"
    )
    provider.require_not_expired(
        artifact.get("expires_at"),
        now=metadata_now,
        label="assembly Hub artifact.expires_at",
    )
    publication.require_equal(
        artifact.get("archive_download_url"),
        (
            f"{provider.API_ROOT}"
            f"{publication.hub_api_path(
                f'/actions/artifacts/{artifact_id}/zip'
            )}"
        ),
        "assembly Hub artifact.archive_download_url",
    )
    workflow_run = (
        artifact.get("workflow_run")
        if isinstance(artifact.get("workflow_run"), dict)
        else {}
    )
    for field in ("repository_id", "head_repository_id"):
        publication.require_equal(
            workflow_run.get(field),
            repository_id,
            f"assembly Hub artifact.workflow_run.{field}",
        )
    publication.require_equal(
        workflow_run.get("head_branch"),
        "main",
        "assembly Hub artifact workflow branch",
    )
    publication.require_equal(
        workflow_run.get("head_sha"),
        source_sha,
        "assembly Hub artifact workflow source",
    )
    archive = provider.download_authenticated_artifact(
        client,
        metadata={
            "id": artifact_id,
            "digest": expected_digest,
            "sizeBytes": size_bytes,
        },
        maximum_bytes=publication.MAX_HUB_PROOF_ARTIFACT_BYTES,
        label="assembly Hub topology artifact",
    )
    recheck_now = publication.read_clock(
        clock, "assembly Hub artifact metadata recheck"
    )
    recheck = client.get_json(path)
    provider.require_unpaginated(
        recheck.headers, "assembly Hub artifact metadata recheck"
    )
    publication.require_equal(
        recheck.value,
        response.value,
        "assembly Hub artifact metadata recheck",
    )
    rechecked = recheck.value if isinstance(recheck.value, dict) else {}
    provider.require_not_expired(
        rechecked.get("expires_at"),
        now=recheck_now,
        label="assembly Hub artifact metadata recheck.expires_at",
    )
    return archive


def handoff_binding_path(
    handoff: Mapping[str, Any], field: str
) -> str:
    binding = handoff.get(field)
    if not isinstance(binding, dict):
        fail(f"provider handoff {field} binding is missing")
    return flagship.safe_relative_path(
        binding.get("relativePath"),
        f"provider handoff {field}.relativePath",
    )


def rebase_candidate_payload(
    entries: Mapping[str, bytes],
    *,
    proposal_relative: str,
    final_relative: str,
) -> dict[str, bytes]:
    reserved = {
        "provider-handoff.json",
        publication.ASSEMBLY_RECEIPT_NAME,
        "topology-retirement.json",
        "committed-boundary-receipt.json",
        "post-marker-convergence-receipt.json",
        final_relative,
        publication.CHANNEL_PROMOTION_PATH,
        publication.DESTINATION_INTENT_PATH,
        "destination-plan.json",
        "RELEASE_CHANNEL.generated.json",
        "releases.json",
    }
    publication_entries: dict[str, bytes] = {}
    for relative, data in entries.items():
        if relative != proposal_relative and not relative.startswith(
            "candidate/"
        ):
            fail("candidate payload contains an unexpected root entry")
        rebased = (
            relative.removeprefix("candidate/")
            if relative.startswith("candidate/")
            else relative
        )
        if (
            rebased in reserved
            or rebased.startswith("approvals/")
            or rebased.startswith("public-bundle/")
        ):
            fail("candidate payload contains post-candidate authority bytes")
        if rebased in publication_entries:
            fail("candidate payload paths collide after canonical rebasing")
        publication_entries[rebased] = data
    return publication_entries


def read_candidate_reference(
    candidate_root: Path,
    value: object,
    *,
    label: str,
    maximum_bytes: int,
) -> tuple[str, bytes]:
    reference = value if isinstance(value, dict) else {}
    relative = flagship.safe_relative_path(
        reference.get("path"), f"{label}.path"
    )
    data = publication.read_regular_file(
        candidate_root / PurePosixPath(relative),
        label,
        maximum_bytes,
    )
    publication.require_equal(
        reference.get("sha256"),
        publication.sha256_bytes(data),
        f"{label}.sha256",
    )
    publication.require_equal(
        reference.get("sizeBytes"),
        len(data),
        f"{label}.sizeBytes",
    )
    return relative, data


def candidate_publication_sources(
    *,
    output_root: Path,
    candidate_relative: str,
    candidate: Mapping[str, Any],
    platforms: Mapping[str, Any],
) -> tuple[bytes, str, dict[str, bytes], Path]:
    candidate_path = output_root / PurePosixPath(candidate_relative)
    candidate_root = candidate_path.parent
    raw_platforms = candidate.get("platforms")
    if not isinstance(raw_platforms, dict):
        fail("candidate manifest platforms are missing")
    expected_predecessor_url, expected_predecessor_sha = (
        publication.common_live_predecessor(platforms)
    )
    publication.require_equal(
        expected_predecessor_url,
        publication.PUBLIC_MANIFEST_URL,
        "candidate live predecessor URL",
    )

    predecessors: list[bytes] = []
    predecessor_relative: str | None = None
    startup_receipts: dict[str, bytes] = {}
    macos_aggregate_path: Path | None = None
    for platform in flagship.PLATFORMS:
        raw_platform = raw_platforms.get(platform)
        if not isinstance(raw_platform, dict):
            fail(f"candidate {platform} platform is missing")
        _, adapter_bytes = read_candidate_reference(
            candidate_root,
            raw_platform.get("nativeE2eReceipt"),
            label=f"{platform} native E2E adapter",
            maximum_bytes=flagship.MAX_JSON_BYTES,
        )
        adapter = publication.load_json_bytes(
            adapter_bytes, f"{platform} native E2E adapter"
        )
        checks = adapter.get("checks")
        clean = checks.get("cleanInstall") if isinstance(checks, dict) else {}
        if not isinstance(clean, dict):
            fail(f"{platform} native E2E clean-install check is missing")
        rich_relative, rich_bytes = read_candidate_reference(
            candidate_root,
            clean.get("evidence"),
            label=f"{platform} rich native lifecycle evidence",
            maximum_bytes=flagship.MAX_EVIDENCE_BYTES,
        )
        if platform == "macos":
            macos_aggregate_path = (
                candidate_root / PurePosixPath(rich_relative)
            )
        rich = publication.load_json_bytes(
            rich_bytes, f"{platform} rich native lifecycle evidence"
        )
        rich_root = (
            candidate_root / PurePosixPath(rich_relative).parent
        )
        if platform == "macos":
            reference_root = candidate_root
            references = rich.get("references")
            if not isinstance(references, dict):
                fail("macOS aggregate references are missing")
            predecessor_reference = references.get("liveReleaseChannel")
            startup_reference = references.get("cleanStartupReceipt")
        else:
            reference_root = rich_root
            live_authority = rich.get("livePredecessorAuthority")
            core_workflow = rich.get("coreWorkflow")
            candidate_workflow = (
                core_workflow.get("candidate")
                if isinstance(core_workflow, dict)
                else {}
            )
            if (
                not isinstance(live_authority, dict)
                or not isinstance(candidate_workflow, dict)
            ):
                fail(f"{platform} lifecycle publication sources are missing")
            predecessor_reference = live_authority.get(
                "liveReleaseChannel"
            )
            startup_reference = candidate_workflow.get("startupReceipt")

        current_predecessor_relative, predecessor_bytes = (
            read_candidate_reference(
                reference_root,
                predecessor_reference,
                label=f"{platform} live predecessor manifest",
                maximum_bytes=flagship.MAX_EVIDENCE_BYTES,
            )
        )
        current_predecessor_path = (
            reference_root / PurePosixPath(current_predecessor_relative)
        )
        try:
            current_predecessor_root_relative = (
                current_predecessor_path.relative_to(output_root).as_posix()
            )
        except ValueError:
            fail(f"{platform} live predecessor escapes the publication input")
        if predecessor_relative is None:
            predecessor_relative = flagship.safe_relative_path(
                current_predecessor_root_relative,
                "candidate live predecessor publication path",
            )
        publication.require_equal(
            publication.sha256_bytes(predecessor_bytes),
            expected_predecessor_sha,
            f"{platform} live predecessor manifest SHA-256",
        )
        predecessors.append(predecessor_bytes)

        _, startup_bytes = read_candidate_reference(
            reference_root,
            startup_reference,
            label=f"{platform} candidate startup receipt",
            maximum_bytes=flagship.MAX_JSON_BYTES,
        )
        receipt_name = (
            "startup-smoke-avalonia-"
            f"{flagship.POLICIES[platform].rid}.receipt.json"
        )
        startup_receipts[receipt_name] = startup_bytes

    first_predecessor = predecessors[0]
    if any(
        not hmac.compare_digest(value, first_predecessor)
        for value in predecessors[1:]
    ):
        fail("platform lifecycle evidence binds different predecessor bytes")
    if macos_aggregate_path is None:
        fail("candidate omits the macOS flagship aggregate")
    if predecessor_relative is None:
        fail("candidate omits the live predecessor manifest")
    return (
        first_predecessor,
        predecessor_relative,
        startup_receipts,
        macos_aggregate_path,
    )


def registry_authority() -> tuple[Path, str]:
    configured = os.environ.get("CHUMMER_HUB_REGISTRY_ROOT", "")
    if not configured:
        fail("CHUMMER_HUB_REGISTRY_ROOT is required for publication assembly")
    try:
        root = Path(configured).resolve(strict=True)
    except OSError as exc:
        fail(f"Registry authority root cannot be resolved: {exc}")
    for relative in (REGISTRY_MATERIALIZER, REGISTRY_VERIFIER):
        publication.read_regular_file(
            root / PurePosixPath(relative),
            f"Registry authority {relative}",
            publication.MAX_PUBLIC_FILE_BYTES,
        )
    completed = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=False,
        capture_output=True,
        text=True,
        env={
            key: value
            for key in ("PATH", "HOME", "LANG", "LC_ALL")
            if (value := os.environ.get(key))
        },
    )
    commit = completed.stdout.strip()
    if completed.returncode != 0:
        fail("Registry authority checkout cannot be identified")
    flagship.require_string(
        commit, "Registry authority commit", flagship.COMMIT_RE
    )
    publication.require_equal(
        commit,
        REGISTRY_COMMIT,
        "Registry authority pinned commit",
    )
    clean = subprocess.run(
        [
            "git",
            "-C",
            str(root),
            "diff",
            "--quiet",
            "HEAD",
            "--",
            REGISTRY_MATERIALIZER,
            REGISTRY_VERIFIER,
        ],
        check=False,
        env={
            key: value
            for key in ("PATH", "HOME", "LANG", "LC_ALL")
            if (value := os.environ.get(key))
        },
    )
    if clean.returncode != 0:
        fail("Registry authority scripts differ from their pinned commit")
    return root, commit


def registry_seed_artifacts(
    platforms: Mapping[str, Any],
    *,
    release_version: str,
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for platform in flagship.PLATFORMS:
        policy = flagship.POLICIES[platform]
        artifact = platforms[platform]["artifact"]
        rows.append(
            {
                "arch": policy.runner_arch,
                "artifactId": artifact["artifactId"],
                "channel": publication.CANDIDATE_CHANNEL_ID,
                "channelId": publication.CANDIDATE_CHANNEL_ID,
                "compatibilityReason": None,
                "compatibilityState": "compatible",
                "downloadUrl": (
                    f"{publication.PUBLIC_BASE_URL}/files/"
                    f"{publication.quote(str(artifact['fileName']))}"
                ),
                "fileName": artifact["fileName"],
                "head": "avalonia",
                "id": artifact["artifactId"],
                "installAccessClass": "open_public",
                "kind": "installer",
                "platform": platform,
                "platformLabel": (
                    f"Avalonia Desktop {platform} "
                    f"{policy.runner_arch.upper()} Installer"
                ),
                "releaseVersion": release_version,
                "rid": policy.rid,
                "sha256": artifact["sha256"],
                "sizeBytes": artifact["sizeBytes"],
                "url": (
                    f"{publication.PUBLIC_BASE_URL}/files/"
                    f"{publication.quote(str(artifact['fileName']))}"
                ),
                "version": release_version,
            }
        )
    return rows


def construct_publication_material(
    *,
    output_root: Path,
    candidate_relative: str,
    proposal_relative: str,
    final_relative: str,
    candidate: Mapping[str, Any],
    proposal: Mapping[str, Any],
    final_receipt: Mapping[str, Any],
    platforms: Mapping[str, Any],
    approval_receipts: Mapping[str, bytes],
    hub_entries: Mapping[str, bytes],
    assembly_authority: Mapping[str, Any],
    generated_at: datetime,
) -> None:
    topology_bytes = hub_entries[
        "TOPOLOGY_B_RETIREMENT.generated.json"
    ]
    publication.require_equal(
        candidate.get("channelId"),
        publication.CANDIDATE_CHANNEL_ID,
        "authenticated candidate channel",
    )
    candidate_bytes = publication.read_regular_file(
        output_root / PurePosixPath(candidate_relative),
        "authenticated candidate manifest",
        flagship.MAX_JSON_BYTES,
    )
    proposal_bytes = publication.read_regular_file(
        output_root / PurePosixPath(proposal_relative),
        "authenticated release proposal",
        flagship.MAX_JSON_BYTES,
    )
    final_bytes = publication.read_regular_file(
        output_root / PurePosixPath(final_relative),
        "authenticated final approval receipt",
        flagship.MAX_JSON_BYTES,
    )
    (
        predecessor_bytes,
        predecessor_relative,
        startup_receipts,
        macos_aggregate_path,
    ) = candidate_publication_sources(
        output_root=output_root,
        candidate_relative=candidate_relative,
        candidate=candidate,
        platforms=platforms,
    )
    predecessor = publication.load_json_bytes(
        predecessor_bytes, "candidate-bound live predecessor manifest"
    )
    macos_aggregate_bytes = publication.read_regular_file(
        macos_aggregate_path,
        "candidate-bound macOS flagship aggregate",
        flagship.MAX_EVIDENCE_BYTES,
    )
    proposal_platforms = proposal.get("platforms")
    proposal_macos = (
        proposal_platforms.get("macos")
        if isinstance(proposal_platforms, dict)
        else None
    )
    proposal_macos_lifecycle = (
        proposal_macos.get("nativeLifecycleEvidence")
        if isinstance(proposal_macos, dict)
        else None
    )
    if not isinstance(proposal_macos_lifecycle, dict):
        fail("producer proposal omits macOS lifecycle evidence")
    publication.require_equal(
        proposal_macos_lifecycle.get("contractName"),
        "chummer6-ui.macos-flagship-evidence",
        "producer proposal macOS lifecycle contract",
    )
    publication.require_equal(
        proposal_macos_lifecycle.get("contractVersion"),
        3,
        "producer proposal macOS lifecycle contractVersion",
    )
    publication.require_equal(
        proposal_macos_lifecycle.get("aggregateSha256"),
        publication.sha256_bytes(macos_aggregate_bytes),
        "producer proposal macOS aggregate SHA-256",
    )
    publication.require_equal(
        proposal_macos_lifecycle.get("aggregateSizeBytes"),
        len(macos_aggregate_bytes),
        "producer proposal macOS aggregate size",
    )
    release_proof = predecessor.get("releaseProof")
    readiness = (
        release_proof.get("flagshipReadiness")
        if isinstance(release_proof, dict)
        else None
    )
    if not isinstance(readiness, dict):
        fail(
            "candidate-bound predecessor omits the Registry flagship "
            "readiness receipt"
        )

    public_bundle = output_root / "public-bundle"
    for platform in flagship.PLATFORMS:
        raw_platform = candidate.get("platforms", {}).get(platform, {})
        if not isinstance(raw_platform, dict):
            fail(f"candidate {platform} platform is missing")
        _, artifact_bytes = read_candidate_reference(
            (output_root / PurePosixPath(candidate_relative)).parent,
            raw_platform.get("artifact"),
            label=f"{platform} candidate artifact",
            maximum_bytes=publication.MAX_PUBLIC_FILE_BYTES,
        )
        artifact = platforms[platform]["artifact"]
        write_entry(
            output_root,
            f"public-bundle/files/{artifact['fileName']}",
            artifact_bytes,
            f"assembly {platform} public artifact",
        )
    for name, data in sorted(startup_receipts.items()):
        write_entry(
            output_root,
            f"public-bundle/startup-smoke/{name}",
            data,
            f"assembly {name}",
        )

    artifact_inventory = publication.promotion_artifact_inventory(platforms)
    artifact_inventory_sha = (
        publication.promotion_artifact_inventory_sha256(platforms)
    )
    intent = {
        "contractName": publication.DESTINATION_INTENT_CONTRACT,
        "contractVersion": 1,
        "candidateId": candidate["candidateId"],
        "releaseVersion": candidate["releaseVersion"],
        "sourceChannel": publication.CANDIDATE_CHANNEL_ID,
        "targetChannel": publication.PUBLICATION_CHANNEL_ID,
        "baseUrl": publication.PUBLIC_BASE_URL,
        "previousManifest": publication.promotion_reference(
            predecessor_bytes, predecessor_relative
        ),
        "topologyRetirementProof": publication.promotion_reference(
            topology_bytes, "topology-retirement.json"
        ),
        "artifactInventory": artifact_inventory,
        "artifactInventorySha256": artifact_inventory_sha,
    }
    intent_bytes = provider.immutable_json_bytes(intent)
    write_entry(
        output_root,
        publication.DESTINATION_INTENT_PATH,
        intent_bytes,
        "assembly destination intent",
    )
    approval_rows = {}
    for role in flagship.REQUIRED_APPROVAL_ROLES:
        relative = f"approvals/{role}/approval.json"
        approval_rows[role] = publication.promotion_reference(
            approval_receipts[role], relative
        )
    startup_rows = []
    for platform in sorted(flagship.PLATFORMS):
        policy = flagship.POLICIES[platform]
        name = (
            "startup-smoke-avalonia-"
            f"{policy.rid}.receipt.json"
        )
        artifact = platforms[platform]["artifact"]
        startup_rows.append(
            {
                **publication.promotion_reference(
                    startup_receipts[name],
                    f"public-bundle/startup-smoke/{name}",
                ),
                "platform": platform,
                "rid": policy.rid,
                "artifactId": artifact["artifactId"],
                "fileName": artifact["fileName"],
            }
        )
    promotion = {
        "contractName": publication.CHANNEL_PROMOTION_CONTRACT,
        "contractVersion": 1,
        "generatedAt": flagship.format_time(generated_at),
        "releaseProfile": "global_flagship",
        "sourceChannel": publication.CANDIDATE_CHANNEL_ID,
        "targetChannel": publication.PUBLICATION_CHANNEL_ID,
        "candidateId": candidate["candidateId"],
        "releaseVersion": candidate["releaseVersion"],
        "assembly": dict(assembly_authority),
        "artifactInventorySha256": artifact_inventory_sha,
        "destinationIntent": publication.promotion_reference(
            intent_bytes, publication.DESTINATION_INTENT_PATH
        ),
        "candidateManifest": publication.promotion_reference(
            candidate_bytes, candidate_relative
        ),
        "proposal": publication.promotion_reference(
            proposal_bytes, proposal_relative
        ),
        "finalApprovalReceipt": publication.promotion_reference(
            final_bytes, final_relative
        ),
        "approvals": approval_rows,
        "hubEvidence": {
            "topologyRetirement": publication.promotion_reference(
                topology_bytes, "topology-retirement.json"
            ),
            "committedBoundaryReceipt": publication.promotion_reference(
                hub_entries["committed-boundary-receipt.json"],
                "committed-boundary-receipt.json",
            ),
            "postMarkerConvergenceReceipt": (
                publication.promotion_reference(
                    hub_entries["post-marker-convergence-receipt.json"],
                    "post-marker-convergence-receipt.json",
                )
            ),
        },
        "startupReceipts": startup_rows,
        "registryProjectionAuthorized": True,
        "publicationMutationAuthorized": False,
    }
    promotion_bytes = provider.immutable_json_bytes(promotion)
    write_entry(
        output_root,
        publication.CHANNEL_PROMOTION_PATH,
        promotion_bytes,
        "assembly channel promotion authority",
    )

    registry_root, registry_commit = registry_authority()
    seed = dict(predecessor)
    seed["channel"] = publication.CANDIDATE_CHANNEL_ID
    seed["channelId"] = publication.CANDIDATE_CHANNEL_ID
    seed["version"] = candidate["releaseVersion"]
    seed["releaseVersion"] = candidate["releaseVersion"]
    seed["artifacts"] = registry_seed_artifacts(
        platforms,
        release_version=str(candidate["releaseVersion"]),
    )
    seed.pop("downloads", None)
    seed["status"] = "published"
    published_at = flagship.require_string(
        final_receipt.get("generatedAt"),
        "final receipt generatedAt",
        flagship.ZULU_RE,
    )
    with tempfile.TemporaryDirectory(
        prefix=".global-flagship-registry-"
    ) as temporary:
        temporary_root = Path(temporary)
        seed_path = temporary_root / "candidate-bound-seed.json"
        readiness_path = temporary_root / "flagship-readiness.json"
        seed_path.write_bytes(provider.immutable_json_bytes(seed))
        readiness_path.write_bytes(
            provider.immutable_json_bytes(readiness)
        )
        completed = subprocess.run(
            [
                sys.executable,
                str(registry_root / REGISTRY_MATERIALIZER),
                "--manifest",
                str(seed_path),
                "--downloads-dir",
                str(public_bundle / "files"),
                "--startup-smoke-dir",
                str(public_bundle / "startup-smoke"),
                "--output",
                str(public_bundle / "RELEASE_CHANNEL.generated.json"),
                "--compat-output",
                str(public_bundle / "releases.json"),
                "--flagship-readiness",
                str(readiness_path),
                "--channel-promotion-authority",
                str(output_root / publication.CHANNEL_PROMOTION_PATH),
                "--macos-flagship-evidence",
                str(macos_aggregate_path),
                "--product",
                "chummer6",
                "--channel",
                publication.PUBLICATION_CHANNEL_ID,
                "--version",
                str(candidate["releaseVersion"]),
                "--contract-name",
                "Chummer.Hub.Registry.Contracts",
                "--published-at",
                published_at,
                "--artifact-source",
                "ui_global_flagship_candidate",
                "--registry-commit",
                registry_commit,
                "--downloads-prefix",
                f"{publication.PUBLIC_BASE_URL}/files",
                "--required-desktop-heads",
                "avalonia",
                "--required-desktop-platforms",
                ",".join(flagship.PLATFORMS),
            ],
            check=False,
            capture_output=True,
            text=True,
            env={
                key: value
                for key in ("PATH", "HOME", "LANG", "LC_ALL")
                if (value := os.environ.get(key))
            },
        )
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout).strip()
        fail(
            "Registry authority rejected three-platform flagship "
            f"materialization: {detail[-1000:]}"
        )

    canonical_bytes = publication.read_regular_file(
        public_bundle / "RELEASE_CHANNEL.generated.json",
        "assembled canonical release manifest",
        publication.MAX_JSON_BYTES,
    )
    releases_bytes = publication.read_regular_file(
        public_bundle / "releases.json",
        "assembled compatibility release manifest",
        publication.MAX_JSON_BYTES,
    )
    destination = {
        "contractName": publication.DESTINATION_PLAN_CONTRACT,
        "contractVersion": 2,
        "candidateId": candidate["candidateId"],
        "releaseVersion": candidate["releaseVersion"],
        "channelPromotion": {
            "candidateChannelId": publication.CANDIDATE_CHANNEL_ID,
            "publicationChannelId": publication.PUBLICATION_CHANNEL_ID,
            "candidateManifestSha256": publication.sha256_bytes(
                candidate_bytes
            ),
            "authority": publication.binding_bytes(
                promotion_bytes,
                publication.CHANNEL_PROMOTION_PATH,
                contractName=publication.CHANNEL_PROMOTION_CONTRACT,
            ),
            "destinationIntent": publication.binding_bytes(
                intent_bytes,
                publication.DESTINATION_INTENT_PATH,
                contractName=publication.DESTINATION_INTENT_CONTRACT,
            ),
        },
        "baseUrl": publication.PUBLIC_BASE_URL,
        "previousManifest": {
            "url": publication.PUBLIC_MANIFEST_URL,
            "sha256": publication.sha256_bytes(predecessor_bytes),
        },
        "topologyRetirementProof": {
            "url": publication.PUBLIC_TOPOLOGY_PROOF_URL,
            "sha256": publication.sha256_bytes(topology_bytes),
            "sizeBytes": len(topology_bytes),
        },
        "manifests": {
            "canonical": {
                "url": publication.PUBLIC_MANIFEST_URL,
                "sha256": publication.sha256_bytes(canonical_bytes),
                "sizeBytes": len(canonical_bytes),
            },
            "releases": {
                "url": publication.PUBLIC_RELEASES_URL,
                "sha256": publication.sha256_bytes(releases_bytes),
                "sizeBytes": len(releases_bytes),
            },
        },
        "artifacts": [
            {
                "platform": platform,
                "fileName": platforms[platform]["artifact"]["fileName"],
                "url": (
                    f"{publication.PUBLIC_BASE_URL}/files/"
                    f"{publication.quote(str(
                        platforms[platform]['artifact']['fileName']
                    ))}"
                ),
                "sha256": platforms[platform]["artifact"]["sha256"],
                "sizeBytes": platforms[platform]["artifact"]["sizeBytes"],
            }
            for platform in flagship.PLATFORMS
        ],
    }
    write_entry(
        output_root,
        "destination-plan.json",
        provider.immutable_json_bytes(destination),
        "assembly destination plan",
    )


def assemble(args: argparse.Namespace) -> Mapping[str, Any]:
    source_sha = flagship.require_string(
        args.source_sha, "assembly source SHA", flagship.COMMIT_RE
    )
    publication.require_equal(
        args.repository, flagship.SOURCE_REPOSITORY, "assembly repository"
    )
    publication.require_equal(
        args.ref, "refs/heads/main", "assembly source ref"
    )
    publication.require_equal(args.run_attempt, "1", "assembly run attempt")
    if args.actor.casefold() != args.triggering_actor.casefold():
        fail("a different actor cannot rerun publication-input assembly")
    publication.require_equal(
        args.environment,
        publication.ASSEMBLY_ENVIRONMENT,
        "assembly protected environment",
    )
    candidate_id_input = flagship.require_string(
        args.candidate_id, "assembly candidate ID", flagship.PORTABLE_RE
    )
    output_root = Path(args.output_root)
    if output_root.exists() or output_root.is_symlink():
        fail("publication-input assembly output already exists")

    ui_token = os.environ.get(args.github_token_env, "")
    hub_token = os.environ.get(args.hub_token_env, "")
    if not ui_token or not hub_token or hmac.compare_digest(ui_token, hub_token):
        fail("independent UI and Hub read-only authorities are required")
    ui_client = provider.GitHubApi(ui_token)
    hub_client = provider.GitHubApi(
        hub_token, repository=publication.HUB_REPOSITORY
    )
    publication.read_clock(
        publication.current_time, "assembly UI repository read"
    )
    ui_repository = provider.validate_repository(
        ui_client.get_json(provider.repository_api_path(""))
    )
    ui_repository_id = int(ui_repository["id"])

    handoff_match = publication.HANDOFF_ARTIFACT_RE.fullmatch(
        args.provider_handoff_artifact_name
    )
    if handoff_match is None:
        fail("provider handoff artifact name is malformed")
    handoff_run_id = int(handoff_match.group(2))
    handoff_authority, handoff_archive = (
        publication.authenticate_transport_archive(
            ui_client,
            artifact_id=args.provider_handoff_artifact_id,
            expected_digest=args.provider_handoff_artifact_digest,
            expected_name=args.provider_handoff_artifact_name,
            expected_run_id=handoff_run_id,
            source_sha=source_sha,
            repository_id=ui_repository_id,
            maximum_bytes=provider.MAX_APPROVAL_ARTIFACT_BYTES,
            workflow=publication.PROVIDER_HANDOFF_WORKFLOW,
            actor=None,
            clock=publication.current_time,
            label="assembly provider handoff artifact",
        )
    )
    handoff_entries = provider.read_exact_zip(
        handoff_archive,
        expected_names=publication.PROVIDER_HANDOFF_ARCHIVE_ENTRIES,
        maximum_entries=1,
        maximum_total_bytes=provider.MAX_APPROVAL_ARTIFACT_BYTES,
        label="assembly provider handoff archive",
    )
    handoff_bytes = handoff_entries["handoff.json"]
    handoff = publication.load_json_bytes(
        handoff_bytes, "assembly provider handoff"
    )
    publication.require_equal(
        handoff.get("provenanceAuthenticated"),
        True,
        "assembly handoff provenanceAuthenticated",
    )
    publication.require_equal(
        handoff.get("releaseArtifactBytesAuthenticated"),
        False,
        "assembly metadata-only handoff boundary",
    )
    publication.require_equal(
        handoff.get("publicationAuthorized"),
        False,
        "assembly handoff publicationAuthorized",
    )

    transport = handoff.get("transportArtifact")
    if not isinstance(transport, dict):
        fail("provider handoff transportArtifact is missing")
    provider_input_id = provider.require_api_integer(
        transport.get("id"), "assembly provider input artifact ID"
    )
    publication.require_equal(
        int(handoff_match.group(1)),
        provider_input_id,
        "assembly provider handoff input-ID name binding",
    )
    provider_input_digest = provider.require_api_string(
        transport.get("digest"),
        "assembly provider input artifact digest",
        provider.ARTIFACT_DIGEST_RE,
    )
    provider_input_metadata, provider_input_archive = raw_artifact(
        ui_client,
        api_path=provider.repository_api_path(
            f"/actions/artifacts/{provider_input_id}"
        ),
        artifact_id=provider_input_id,
        expected_name=provider.INPUT_ARTIFACT_NAME,
        expected_digest=provider_input_digest,
        repository_id=ui_repository_id,
        source_sha=source_sha,
        maximum_bytes=provider.MAX_INPUT_ARTIFACT_BYTES,
        clock=publication.current_time,
        label="assembly metadata-only provider input",
    )
    provider_input_projection = {
        key: transport[key]
        for key in (
            "id",
            "name",
            "digest",
            "sizeBytes",
            "createdAt",
            "updatedAt",
            "expiresAt",
            "workflowRunId",
        )
    }
    publication.require_equal(
        provider_input_metadata,
        provider_input_projection,
        "assembly provider input metadata",
    )
    provider_input_outer = provider.read_exact_zip(
        provider_input_archive,
        expected_names={provider.INPUT_BUNDLE_FILE_NAME},
        maximum_entries=1,
        maximum_total_bytes=provider.MAX_INPUT_ARTIFACT_BYTES,
        label="assembly provider input archive",
    )
    provider_bundle_bytes = provider_input_outer[
        provider.INPUT_BUNDLE_FILE_NAME
    ]
    provider_bundle = provider.read_local_bundle(provider_bundle_bytes)
    provider.validate_local_bundle(
        provider_bundle, now=publication.current_time()
    )

    candidate_path_relative = handoff_binding_path(
        handoff, "candidateManifest"
    )
    candidate_name_match = (
        publication.CANDIDATE_PAYLOAD_ARTIFACT_RE.fullmatch(
            args.candidate_payload_artifact_name
        )
    )
    if candidate_name_match is None:
        fail("candidate payload artifact name is malformed")
    publication.require_equal(
        candidate_name_match.group(1),
        candidate_id_input,
        "candidate payload artifact candidate ID",
    )
    candidate_run_id = int(candidate_name_match.group(2))
    _, candidate_archive = raw_artifact(
        ui_client,
        api_path=provider.repository_api_path(
            f"/actions/artifacts/{args.candidate_payload_artifact_id}"
        ),
        artifact_id=args.candidate_payload_artifact_id,
        expected_name=args.candidate_payload_artifact_name,
        expected_digest=args.candidate_payload_artifact_digest,
        repository_id=ui_repository_id,
        source_sha=source_sha,
        maximum_bytes=publication.MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        clock=publication.current_time,
        label="assembly candidate payload artifact",
    )
    candidate_entries = provider.read_exact_zip(
        candidate_archive,
        expected_names=None,
        maximum_entries=MAX_CANDIDATE_ENTRIES,
        maximum_total_bytes=publication.MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
        label="assembly candidate payload archive",
    )
    publication.require_equal(
        candidate_path_relative,
        PUBLICATION_CANDIDATE_PATH,
        "provider handoff candidate manifest path",
    )
    if PRODUCER_CANDIDATE_PATH not in candidate_entries:
        fail("candidate payload archive omits the handoff-bound candidate")
    if not hmac.compare_digest(
        candidate_entries[PRODUCER_CANDIDATE_PATH],
        provider_bundle.candidate.data,
    ):
        fail("candidate payload bytes differ from the provider metadata bundle")
    candidate = publication.load_json_bytes(
        provider_bundle.candidate.data, "assembly candidate manifest"
    )
    publication.require_equal(
        candidate.get("candidateId"),
        candidate_id_input,
        "assembly candidate ID",
    )
    producer = candidate.get("producer")
    if not isinstance(producer, dict):
        fail("candidate producer identity is missing")
    publication.require_equal(
        producer.get("artifactName"),
        args.candidate_payload_artifact_name,
        "candidate producer artifact name",
    )
    publication.require_equal(
        candidate_run_id, producer.get("runId"), "candidate producer run ID"
    )
    publication.require_equal(
        producer.get("runAttempt"), 1, "candidate producer run attempt"
    )
    candidate_authority, candidate_archive_recheck = (
        publication.authenticate_transport_archive(
            ui_client,
            artifact_id=args.candidate_payload_artifact_id,
            expected_digest=args.candidate_payload_artifact_digest,
            expected_name=args.candidate_payload_artifact_name,
            expected_run_id=candidate_run_id,
            source_sha=source_sha,
            repository_id=ui_repository_id,
            maximum_bytes=publication.MAX_PUBLICATION_INPUT_ARTIFACT_BYTES,
            workflow=str(producer.get("workflow")),
            actor=str(producer.get("actor")),
            clock=publication.current_time,
            label="assembly candidate payload artifact",
        )
    )
    if not hmac.compare_digest(candidate_archive_recheck, candidate_archive):
        fail("candidate payload archive changed during provider authentication")

    proposal_relative = handoff_binding_path(handoff, "proposal")
    final_relative = handoff_binding_path(handoff, "finalReceipt")
    publication.require_equal(
        proposal_relative,
        PRODUCER_PROPOSAL_PATH,
        "candidate producer proposal path",
    )
    if proposal_relative not in candidate_entries:
        fail("candidate payload archive omits the producer proposal")
    if not hmac.compare_digest(
        candidate_entries[proposal_relative],
        provider_bundle.proposal.data,
    ):
        fail("candidate producer proposal differs from the provider bundle")
    proposal_payload = publication.load_json_bytes(
        provider_bundle.proposal.data, "assembly producer proposal"
    )
    proposal_platforms = proposal_payload.get("platforms")
    if not isinstance(proposal_platforms, dict):
        fail("assembly producer proposal platforms are missing")
    publication_entries = rebase_candidate_payload(
        candidate_entries,
        proposal_relative=proposal_relative,
        final_relative=final_relative,
    )
    added_paths = projected_publication_input_added_paths(
        final_relative=final_relative,
        platforms=proposal_platforms,
    )
    if set(publication_entries).intersection(added_paths):
        fail("projected publication-input paths collide with candidate bytes")
    for relative in (*publication_entries, *added_paths):
        publication.require_publication_input_entry_name(
            relative, "assembly projected publication-input entry name"
        )
    duplicated_public_bundle_bytes = (
        projected_public_bundle_duplicate_bytes(
            entries=publication_entries,
            candidate_relative=candidate_path_relative,
            candidate=candidate,
        )
    )
    known_authority_bytes = (
        len(provider_bundle.final_receipt.data or b"")
        + len(handoff_bytes)
        + sum(
            len(provider_bundle.approvals[role].data or b"")
            for role in flagship.REQUIRED_APPROVAL_ROLES
        )
    )
    validate_publication_input_projection(
        candidate_entry_count=len(publication_entries),
        candidate_expanded_bytes=sum(
            len(data) for data in publication_entries.values()
        ),
        duplicated_public_bundle_bytes=duplicated_public_bundle_bytes,
        known_authority_bytes=known_authority_bytes,
    )
    publication.materialize_archive_entries(
        publication_entries,
        output_root,
        label="assembly candidate payload",
    )
    write_entry(
        output_root,
        final_relative,
        provider_bundle.final_receipt.data,
        "assembly final receipt",
    )
    write_entry(
        output_root,
        "provider-handoff.json",
        handoff_bytes,
        "assembly provider handoff",
    )

    final_payload = publication.load_json_bytes(
        provider_bundle.final_receipt.data, "assembly final receipt"
    )
    final_approvals = final_payload.get("approvals")
    if not isinstance(final_approvals, list):
        fail("assembly final receipt approvals must be an array")
    handoff_approvals = handoff.get("approvals")
    if not isinstance(handoff_approvals, list):
        fail("assembly handoff approvals must be an array")
    handoff_by_role = {
        str(row.get("role")): row
        for row in handoff_approvals
        if isinstance(row, dict)
    }
    approval_authorities: list[dict[str, Any]] = []
    approval_receipt_bytes: dict[str, bytes] = {}
    for row in final_approvals:
        if not isinstance(row, dict):
            fail("assembly final approval row must be an object")
        role = str(row.get("role"))
        if role not in flagship.REQUIRED_APPROVAL_ROLES:
            fail("assembly final receipt contains an unknown approval role")
        authenticated = handoff_by_role.get(role)
        if not isinstance(authenticated, dict):
            fail(f"assembly handoff omits the {role} approval")
        artifact = authenticated.get("artifact")
        run = authenticated.get("run")
        if not isinstance(artifact, dict) or not isinstance(run, dict):
            fail(f"assembly handoff {role} provider authority is incomplete")
        approval_authority, approval_archive = (
            publication.authenticate_transport_archive(
                ui_client,
                artifact_id=int(artifact["id"]),
                expected_digest=str(artifact["digest"]),
                expected_name=str(artifact["name"]),
                expected_run_id=int(run["id"]),
                source_sha=source_sha,
                repository_id=ui_repository_id,
                maximum_bytes=provider.MAX_APPROVAL_ARTIFACT_BYTES,
                workflow=flagship.APPROVAL_WORKFLOW,
                actor=str(authenticated["actor"]),
                clock=publication.current_time,
                label=f"assembly {role} approval artifact",
            )
        )
        approval_entries = provider.read_exact_zip(
            approval_archive,
            expected_names={"approval.json"},
            maximum_entries=1,
            maximum_total_bytes=provider.MAX_APPROVAL_ARTIFACT_BYTES,
            label=f"assembly {role} approval archive",
        )
        approval_bytes = approval_entries["approval.json"]
        local_approval = provider_bundle.approvals[role]
        if not hmac.compare_digest(approval_bytes, local_approval.data):
            fail(f"assembly {role} approval provider bytes differ")
        receipt_binding = row.get("receipt")
        if not isinstance(receipt_binding, dict):
            fail(f"assembly final {role} receipt binding is missing")
        receipt_relative = flagship.safe_relative_path(
            receipt_binding.get("relativePath"),
            f"assembly final {role} receipt path",
        )
        publication.require_equal(
            receipt_relative,
            "approval.json",
            f"assembly final {role} canonical approval path",
        )
        write_entry(
            output_root,
            f"approvals/{role}/{receipt_relative}",
            approval_bytes,
            f"assembly {role} approval",
        )
        approval_receipt_bytes[role] = approval_bytes
        approval_authorities.append(
            {
                "role": role,
                "authority": approval_authority,
                "receipt": publication.binding_bytes(
                    approval_bytes,
                    f"approvals/{role}/{receipt_relative}",
                    contractName=flagship.APPROVAL_CONTRACT,
                ),
            }
        )
    if {row["role"] for row in approval_authorities} != set(
        flagship.REQUIRED_APPROVAL_ROLES
    ):
        fail("assembly requires three distinct approval authorities")
    approval_authorities.sort(key=lambda row: str(row["role"]))

    publication.read_clock(
        publication.current_time, "assembly Hub repository read"
    )
    hub_repository_response = hub_client.get_json(
        publication.hub_api_path("")
    )
    provider.require_unpaginated(
        hub_repository_response.headers, "assembly Hub repository"
    )
    hub_repository = (
        hub_repository_response.value
        if isinstance(hub_repository_response.value, dict)
        else {}
    )
    hub_repository_id = provider.require_api_integer(
        hub_repository.get("id"), "assembly Hub repository.id"
    )
    publication.read_clock(
        publication.current_time, "assembly Hub artifact identity read"
    )
    hub_artifact_response = hub_client.get_json(
        publication.hub_api_path(
            f"/actions/artifacts/{args.hub_topology_artifact_id}"
        )
    )
    provider.require_unpaginated(
        hub_artifact_response.headers, "assembly Hub artifact identity"
    )
    hub_artifact = (
        hub_artifact_response.value
        if isinstance(hub_artifact_response.value, dict)
        else {}
    )
    hub_workflow_run = hub_artifact.get("workflow_run")
    if not isinstance(hub_workflow_run, dict):
        fail("assembly Hub artifact workflow run is missing")
    hub_source_sha = flagship.require_string(
        hub_workflow_run.get("head_sha"),
        "assembly Hub source SHA",
        flagship.COMMIT_RE,
    )
    hub_archive = raw_hub_artifact(
        hub_client,
        artifact_id=args.hub_topology_artifact_id,
        expected_name=args.hub_topology_artifact_name,
        expected_digest=args.hub_topology_artifact_digest,
        repository_id=hub_repository_id,
        source_sha=hub_source_sha,
        clock=publication.current_time,
    )
    hub_entries = provider.read_exact_zip(
        hub_archive,
        expected_names=publication.HUB_PROOF_ENTRIES,
        maximum_entries=3,
        maximum_total_bytes=publication.MAX_HUB_PROOF_ARTIFACT_BYTES,
        label="assembly Hub topology archive",
    )
    hub_authority = publication.authenticate_hub_topology_provider(
        hub_client,
        topology_entries=hub_entries,
        artifact_id=args.hub_topology_artifact_id,
        artifact_name=args.hub_topology_artifact_name,
        expected_digest=args.hub_topology_artifact_digest,
        clock=publication.current_time,
    )
    repository_root = Path(__file__).resolve().parents[2]
    publisher_bytes = publication.read_regular_file(
        repository_root / publication.CANONICAL_PUBLISHER,
        "canonical publisher",
        publication.MAX_JSON_BYTES,
    )
    publication.validate_topology_retirement(
        publication.load_json_bytes(
            hub_entries["TOPOLOGY_B_RETIREMENT.generated.json"],
            "assembly topology retirement proof",
        ),
        now=publication.current_time(),
        publisher_bytes=publisher_bytes,
        committed_boundary_bytes=hub_entries[
            "committed-boundary-receipt.json"
        ],
        post_marker_convergence_bytes=hub_entries[
            "post-marker-convergence-receipt.json"
        ],
    )
    write_entry(
        output_root,
        "topology-retirement.json",
        hub_entries["TOPOLOGY_B_RETIREMENT.generated.json"],
        "assembly topology retirement proof",
    )
    write_entry(
        output_root,
        "committed-boundary-receipt.json",
        hub_entries["committed-boundary-receipt.json"],
        "assembly committed boundary receipt",
    )
    write_entry(
        output_root,
        "post-marker-convergence-receipt.json",
        hub_entries["post-marker-convergence-receipt.json"],
        "assembly post-marker convergence receipt",
    )

    promotion_now = publication.current_time()
    construct_publication_material(
        output_root=output_root,
        candidate_relative=candidate_path_relative,
        proposal_relative=proposal_relative,
        final_relative=final_relative,
        candidate=candidate,
        proposal=proposal_payload,
        final_receipt=final_payload,
        platforms=proposal_platforms,
        approval_receipts=approval_receipt_bytes,
        hub_entries=hub_entries,
        assembly_authority={
            "repository": flagship.SOURCE_REPOSITORY,
            "workflow": publication.ASSEMBLY_WORKFLOW,
            "ref": "refs/heads/main",
            "sha": source_sha,
            "runId": args.run_id,
            "runAttempt": 1,
            "actor": args.actor,
            "triggeringActor": args.triggering_actor,
            "environment": publication.ASSEMBLY_ENVIRONMENT,
        },
        generated_at=promotion_now,
    )
    receipt_now = publication.current_time()
    material = publication.load_material(
        publication_root=output_root,
        handoff_path=output_root / "provider-handoff.json",
        repository_root=repository_root,
        now=receipt_now,
    )
    expires_at = min(
        flagship.parse_time(
            material.candidate["expiresAt"], "assembly candidate expiresAt"
        ),
        receipt_now + timedelta(hours=23),
    )
    if expires_at <= receipt_now:
        fail("candidate expired before publication-input assembly")
    manifests: dict[str, Any] = {}
    for key, file_name in (
        ("canonical", "RELEASE_CHANNEL.generated.json"),
        ("releases", "releases.json"),
    ):
        data = publication.read_regular_file(
            material.public_bundle / file_name,
            f"assembly {file_name}",
            publication.MAX_JSON_BYTES,
        )
        manifests[key] = publication.binding_bytes(
            data, f"public-bundle/{file_name}"
        )
    platform_bindings: dict[str, Any] = {}
    for platform in flagship.PLATFORMS:
        artifact = material.platforms[platform]["artifact"]
        relative = f"public-bundle/files/{artifact['fileName']}"
        data = publication.read_regular_file(
            output_root / PurePosixPath(relative),
            f"assembly {platform} binary",
            publication.MAX_PUBLIC_FILE_BYTES,
        )
        platform_bindings[platform] = publication.binding_bytes(data, relative)

    receipt = {
        "contractName": publication.ASSEMBLY_CONTRACT,
        "contractVersion": publication.ASSEMBLY_CONTRACT_VERSION,
        "generatedAt": flagship.format_time(receipt_now),
        "expiresAt": flagship.format_time(expires_at),
        "status": "passed",
        "candidate": {
            "candidateId": material.candidate["candidateId"],
            "releaseVersion": material.candidate["releaseVersion"],
            "source": material.candidate["source"],
            "producer": material.candidate["producer"],
        },
        "assembly": {
            "repository": flagship.SOURCE_REPOSITORY,
            "ref": "refs/heads/main",
            "sha": source_sha,
            "workflow": publication.ASSEMBLY_WORKFLOW,
            "runId": args.run_id,
            "runAttempt": 1,
            "actor": args.actor,
            "environment": publication.ASSEMBLY_ENVIRONMENT,
        },
        "upstreamArtifacts": {
            "candidatePayload": candidate_authority,
            "providerInput": {
                "artifact": provider_input_metadata,
                "archiveSha256": publication.sha256_bytes(
                    provider_input_archive
                ),
                "archiveSizeBytes": len(provider_input_archive),
                "trustedAsAuthority": False,
                "purpose": "bounded-metadata-transport-only",
            },
            "providerHandoff": handoff_authority,
            "approvals": approval_authorities,
            "hubTopology": hub_authority,
        },
        "providerHandoff": publication.binding_bytes(
            handoff_bytes,
            "provider-handoff.json",
            contractName=provider.HANDOFF_CONTRACT,
        ),
        "candidateManifest": publication.binding_bytes(
            material.candidate_bytes,
            material.candidate_path.relative_to(output_root).as_posix(),
            contractName=flagship.CANDIDATE_CONTRACT,
        ),
        "proposal": publication.binding_bytes(
            material.proposal_bytes,
            material.proposal_path.relative_to(output_root).as_posix(),
            contractName=flagship.PROPOSAL_CONTRACT,
        ),
        "finalReceipt": publication.binding_bytes(
            material.final_bytes,
            material.final_path.relative_to(output_root).as_posix(),
            contractName=flagship.FINAL_RECEIPT_CONTRACT,
        ),
        "topologyRetirement": publication.binding_bytes(
            material.topology_bytes,
            "topology-retirement.json",
            contractName=publication.TOPOLOGY_CONTRACT,
        ),
        "committedBoundaryReceipt": publication.binding_bytes(
            material.committed_boundary_bytes,
            "committed-boundary-receipt.json",
        ),
        "postMarkerConvergenceReceipt": publication.binding_bytes(
            material.post_marker_convergence_bytes,
            "post-marker-convergence-receipt.json",
        ),
        "channelPromotionAuthority": publication.binding_bytes(
            material.channel_promotion_bytes,
            publication.CHANNEL_PROMOTION_PATH,
            contractName=publication.CHANNEL_PROMOTION_CONTRACT,
        ),
        "destinationIntent": publication.binding_bytes(
            material.destination_intent_bytes,
            publication.DESTINATION_INTENT_PATH,
            contractName=publication.DESTINATION_INTENT_CONTRACT,
        ),
        "destinationPlan": publication.binding_bytes(
            material.destination_plan_bytes,
            "destination-plan.json",
            contractName=publication.DESTINATION_PLAN_CONTRACT,
        ),
        "manifests": manifests,
        "platforms": platform_bindings,
        "inventory": publication.publication_input_inventory(output_root),
        "nonPublishing": True,
        "publicationAuthorized": False,
        "releaseArtifactBytesAuthenticated": True,
    }
    receipt_path = output_root / publication.ASSEMBLY_RECEIPT_NAME
    receipt_bytes = provider.immutable_json_bytes(receipt)
    if len(receipt_bytes) > publication.MAX_JSON_BYTES:
        fail("publication input assembly receipt exceeds its byte boundary")
    provider.write_once(receipt_path, receipt_bytes)
    return receipt


def command(args: argparse.Namespace) -> int:
    try:
        assemble(args)
    except (
        publication.ContractError,
        provider.ContractError,
        flagship.ContractError,
        OSError,
        ValueError,
    ) as exc:
        print(f"publication-input assembly blocked: {exc}", file=sys.stderr)
        return 1
    print(Path(args.output_root) / publication.ASSEMBLY_RECEIPT_NAME)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Assemble one protected nonpublishing flagship input."
    )
    parser.add_argument("--candidate-id", required=True)
    parser.add_argument(
        "--candidate-payload-artifact-id",
        type=flagship.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--candidate-payload-artifact-name", required=True)
    parser.add_argument("--candidate-payload-artifact-digest", required=True)
    parser.add_argument(
        "--provider-handoff-artifact-id",
        type=flagship.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--provider-handoff-artifact-name", required=True)
    parser.add_argument("--provider-handoff-artifact-digest", required=True)
    parser.add_argument(
        "--hub-topology-artifact-id",
        type=flagship.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--hub-topology-artifact-name", required=True)
    parser.add_argument("--hub-topology-artifact-digest", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument(
        "--run-id",
        type=flagship.bounded_integer(1, 2**63 - 1),
        required=True,
    )
    parser.add_argument("--run-attempt", required=True)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--triggering-actor", required=True)
    parser.add_argument("--environment", required=True)
    parser.add_argument("--github-token-env", default="GITHUB_TOKEN")
    parser.add_argument(
        "--hub-token-env",
        default="CHUMMER_FLAGSHIP_HUB_ACTIONS_READ_TOKEN",
    )
    parser.add_argument("--output-root", required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    return command(build_parser().parse_args(argv))


if __name__ == "__main__":
    raise SystemExit(main())
