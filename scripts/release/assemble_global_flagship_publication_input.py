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
import sys
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Mapping, Sequence

import assemble_global_flagship_release as flagship
import authenticate_global_flagship_release as provider
import publish_global_flagship_release as publication


MAX_CANDIDATE_ENTRIES = 4096


def fail(message: str) -> None:
    raise publication.ContractError(message)


def write_entry(root: Path, relative: str, data: bytes, label: str) -> None:
    normalized = flagship.safe_relative_path(relative, f"{label} path")
    if normalized != relative:
        fail(f"{label} path is not canonical")
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
    if candidate_path_relative not in candidate_entries:
        fail("candidate payload archive omits the handoff-bound candidate")
    if not hmac.compare_digest(
        candidate_entries[candidate_path_relative],
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
    if producer.get("artifactName") is not None:
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
    reserved = {
        "provider-handoff.json",
        publication.ASSEMBLY_RECEIPT_NAME,
        "topology-retirement.json",
        "committed-boundary-receipt.json",
        "post-marker-convergence-receipt.json",
        proposal_relative,
        final_relative,
    }
    for relative in candidate_entries:
        if relative in reserved or relative.startswith("approvals/"):
            fail("candidate payload contains post-candidate authority bytes")
    publication.materialize_archive_entries(
        candidate_entries,
        output_root,
        label="assembly candidate payload",
    )
    write_entry(
        output_root,
        proposal_relative,
        provider_bundle.proposal.data,
        "assembly proposal",
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
        write_entry(
            output_root,
            f"approvals/{role}/{receipt_relative}",
            approval_bytes,
            f"assembly {role} approval",
        )
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

    repository_root = Path(__file__).resolve().parents[2]
    material = publication.load_material(
        publication_root=output_root,
        handoff_path=output_root / "provider-handoff.json",
        repository_root=repository_root,
        now=publication.current_time(),
    )
    hub_authority = publication.authenticate_hub_topology_provider(
        hub_client,
        material=material,
        artifact_id=args.hub_topology_artifact_id,
        artifact_name=args.hub_topology_artifact_name,
        expected_digest=args.hub_topology_artifact_digest,
        clock=publication.current_time,
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
    provider.write_once(receipt_path, provider.immutable_json_bytes(receipt))
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
