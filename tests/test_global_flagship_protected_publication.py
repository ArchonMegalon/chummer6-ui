from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import stat
import sys
import zipfile
from dataclasses import replace
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import pytest


ROOT = Path(__file__).resolve().parents[1]
RELEASE_SCRIPTS = ROOT / "scripts" / "release"
sys.path.insert(0, str(RELEASE_SCRIPTS))
SCRIPT = RELEASE_SCRIPTS / "publish_global_flagship_release.py"
SPEC = importlib.util.spec_from_file_location(
    "publish_global_flagship_release", SCRIPT
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class FakeHubProvider:
    def __init__(
        self,
        responses: dict[str, object],
        archive: bytes,
    ) -> None:
        self.responses = responses
        self.archive = archive
        self.archive_reads = 0

    def get_json(self, path: str) -> Any:
        return MODULE.provider_auth.JsonResponse(
            value=self.responses[path],
            headers={},
        )

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        assert artifact_id == 701
        assert len(self.archive) <= max_bytes
        self.archive_reads += 1
        return self.archive


def artifact_zip(entries: dict[str, bytes]) -> bytes:
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_STORED) as archive:
        for name, data in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            # actions/upload-artifact v4 normalizes downloaded regular files
            # to 0644; permissions are not part of the provider byte contract.
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            archive.writestr(info, data)
    return output.getvalue()


class FakeReader:
    def __init__(
        self,
        before: dict[str, bytes],
        after: dict[str, bytes],
    ) -> None:
        self.resources = dict(before)
        self.after = dict(after)
        self.urls: list[str] = []

    def get(self, url: str, maximum_bytes: int) -> bytes:
        self.urls.append(url)
        data = self.resources[url]
        assert len(data) <= maximum_bytes
        return data


class FakePublisher:
    def __init__(self, reader: FakeReader) -> None:
        self.reader = reader
        self.calls: list[tuple[Path, str]] = []

    def publish(self, bundle: Path, manifest_url: str) -> None:
        self.calls.append((bundle, manifest_url))
        self.reader.resources = dict(self.reader.after)


def material(tmp_path: Path) -> tuple[Any, dict[str, bytes], dict[str, bytes]]:
    previous_manifest = b'{"releaseVersion":"baseline"}\n'
    final_manifest = b'{"releaseVersion":"flagship"}\n'
    releases = b'{"releases":[{"version":"flagship"}]}\n'
    topology = b'{"topology":true}\n'
    before = {
        MODULE.PUBLIC_MANIFEST_URL: previous_manifest,
        MODULE.PUBLIC_TOPOLOGY_PROOF_URL: topology,
    }
    after = {
        MODULE.PUBLIC_MANIFEST_URL: final_manifest,
        MODULE.PUBLIC_RELEASES_URL: releases,
        MODULE.PUBLIC_TOPOLOGY_PROOF_URL: topology,
    }
    root = tmp_path / "input"
    public_bundle = root / "public-bundle"
    (public_bundle / "files").mkdir(parents=True)
    platform_rows: dict[str, Any] = {}
    artifact_identities: dict[str, tuple[str, int]] = {}
    destination_artifacts: list[dict[str, Any]] = []
    for platform, file_name in (
        ("windows", "chummer-avalonia-win-x64-installer.exe"),
        ("linux", "chummer-avalonia-linux-x64-installer.deb"),
        ("macos", "chummer-avalonia-osx-arm64-installer.dmg"),
    ):
        data = f"{platform}-signed-installer".encode()
        candidate_path = root / "candidate-files" / file_name
        candidate_path.parent.mkdir(exist_ok=True)
        candidate_path.write_bytes(data)
        (public_bundle / "files" / file_name).write_bytes(data)
        relative = candidate_path.relative_to(root).as_posix()
        artifact = {
            "artifactId": f"{platform}-artifact",
            "fileName": file_name,
            "relativePath": relative,
            "sha256": digest(data),
            "sizeBytes": len(data),
        }
        platform_rows[platform] = {
            "artifact": artifact,
            "signingReceipt": (
                None
                if platform == "linux"
                else {
                    "sha256": "1" * 64,
                    "sizeBytes": 1,
                    "relativePath": f"{platform}-signing.json",
                }
            ),
            "integrityPolicy": f"{platform}-policy",
        }
        artifact_identities[platform] = (digest(data), len(data))
        url = f"{MODULE.PUBLIC_BASE_URL}/files/{file_name}"
        after[url] = data
        destination_artifacts.append(
            {
                "platform": platform,
                "fileName": file_name,
                "url": url,
                "sha256": digest(data),
                "sizeBytes": len(data),
            }
        )
    (public_bundle / "RELEASE_CHANNEL.generated.json").write_bytes(final_manifest)
    (public_bundle / "releases.json").write_bytes(releases)
    plan = {
        "contractName": MODULE.DESTINATION_PLAN_CONTRACT,
        "contractVersion": 1,
        "candidateId": "candidate-1",
        "releaseVersion": "flagship",
        "baseUrl": MODULE.PUBLIC_BASE_URL,
        "previousManifest": {
            "url": MODULE.PUBLIC_MANIFEST_URL,
            "sha256": digest(previous_manifest),
        },
        "topologyRetirementProof": {
            "url": MODULE.PUBLIC_TOPOLOGY_PROOF_URL,
            "sha256": digest(topology),
            "sizeBytes": len(topology),
        },
        "manifests": {
            "canonical": {
                "url": MODULE.PUBLIC_MANIFEST_URL,
                "sha256": digest(final_manifest),
                "sizeBytes": len(final_manifest),
            },
            "releases": {
                "url": MODULE.PUBLIC_RELEASES_URL,
                "sha256": digest(releases),
                "sizeBytes": len(releases),
            },
        },
        "artifacts": destination_artifacts,
    }
    result = MODULE.PublicationMaterial(
        root=root,
        handoff_path=tmp_path / "handoff.json",
        candidate_path=root / "candidate.json",
        proposal_path=root / "proposal.json",
        final_path=root / "final-receipt.json",
        topology_path=root / "topology-retirement.json",
        destination_plan_path=root / "destination-plan.json",
        public_bundle=public_bundle,
        handoff_bytes=b'{"handoff":true}\n',
        candidate_bytes=b'{"candidate":true}\n',
        proposal_bytes=b'{"proposal":true}\n',
        final_bytes=b'{"final":true}\n',
        topology_bytes=topology,
        committed_boundary_bytes=b"committed-boundary",
        post_marker_convergence_bytes=b"post-marker-convergence",
        destination_plan_bytes=json.dumps(plan).encode(),
        proposal={},
        candidate={
            "candidateId": "candidate-1",
            "releaseVersion": "flagship",
            "source": {"commit": "a" * 40},
        },
        platforms=platform_rows,
        destination_plan=plan,
        artifact_identities=artifact_identities,
    )
    return result, before, after


def execute(
    tmp_path: Path,
    *,
    before_override: dict[str, bytes] | None = None,
) -> tuple[Any, FakeReader, FakePublisher, Path]:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before_override or before, after)
    publisher = FakePublisher(reader)
    output = tmp_path / "publication-receipt.json"
    receipt = MODULE.execute_transaction(
        material=candidate,
        reader=reader,
        publisher=publisher,
        output=output,
        now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        workflow_run_id=123,
        workflow_actor="operator",
        authenticated_transports={"provider": "authenticated"},
        hub_provider_authority={"hub": "authenticated"},
        hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
    )
    return receipt, reader, publisher, output


def test_transaction_authorizes_only_after_exact_destination_readback(
    tmp_path: Path,
) -> None:
    receipt, reader, publisher, output = execute(tmp_path)
    assert receipt["publicationAuthorized"] is True
    assert receipt["releaseArtifactBytesAuthenticated"] is True
    assert receipt["signingAndNotarizationAuthenticated"] is True
    assert receipt["topologyRetirementAuthenticated"] is True
    assert receipt["destinationBytesVerified"] is True
    assert len(receipt["destinations"]) == 6
    assert len(publisher.calls) == 1
    assert reader.urls[0] == MODULE.PUBLIC_TOPOLOGY_PROOF_URL
    assert output.stat().st_mode & 0o777 == 0o444
    assert json.loads(output.read_text())["publicationAuthorized"] is True


def test_fresh_dispatch_adopts_an_exact_live_candidate_without_republishing(
    tmp_path: Path,
) -> None:
    candidate, _before, after = material(tmp_path)
    reader = FakeReader(after, after)
    publisher = FakePublisher(reader)
    receipt = MODULE.execute_transaction(
        material=candidate,
        reader=reader,
        publisher=publisher,
        output=tmp_path / "receipt.json",
        journal=tmp_path / "fresh-dispatch-journal",
        now=datetime(2026, 7, 25, tzinfo=UTC),
        workflow_run_id=1,
        workflow_actor="operator",
        authenticated_transports={"provider": "authenticated"},
        hub_provider_authority={"hub": "authenticated"},
        hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
    )
    assert publisher.calls == []
    assert (
        receipt["publicationWorkflow"]["transactionMode"]
        == "exact-live-candidate-adoption"
    )
    assert receipt["destinationBytesVerified"] is True
    assert receipt["transactionJournal"]["recoveredMutationMarker"] is False


def test_crash_after_publish_recovers_exact_live_candidate_without_republish(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    first_publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    journal = tmp_path / "journal"

    def crash() -> None:
        raise RuntimeError("simulated process crash")

    with pytest.raises(RuntimeError, match="simulated process crash"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=first_publisher,
            output=output,
            journal=journal,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
            after_publisher_return=crash,
        )
    assert len(first_publisher.calls) == 1
    assert not output.exists()
    assert (journal / "prepared.json").stat().st_mode & 0o777 == 0o400
    assert (journal / "mutation-started.json").exists()

    recovery_publisher = FakePublisher(reader)
    receipt = MODULE.execute_transaction(
        material=candidate,
        reader=reader,
        publisher=recovery_publisher,
        output=output,
        journal=journal,
        now=datetime(2026, 7, 25, 0, 1, tzinfo=UTC),
        workflow_run_id=2,
        workflow_actor="operator",
        authenticated_transports={"provider": "authenticated"},
        hub_provider_authority={"hub": "authenticated"},
        hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
    )
    assert recovery_publisher.calls == []
    assert receipt["publicationAuthorized"] is True
    assert (
        receipt["publicationWorkflow"]["transactionMode"]
        == "exact-live-candidate-adoption"
    )
    assert receipt["transactionJournal"]["recoveredMutationMarker"] is True


def test_recovery_rejects_partial_live_candidate_without_republishing(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    partial = dict(after)
    partial[MODULE.PUBLIC_RELEASES_URL] = b'{"partial":true}\n'
    reader = FakeReader(before, partial)
    first_publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    journal = tmp_path / "journal"

    with pytest.raises(RuntimeError, match="simulated process crash"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=first_publisher,
            output=output,
            journal=journal,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
            after_publisher_return=lambda: (_ for _ in ()).throw(
                RuntimeError("simulated process crash")
            ),
        )

    recovery_publisher = FakePublisher(reader)
    with pytest.raises(MODULE.ContractError, match="size|SHA-256"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=recovery_publisher,
            output=output,
            journal=journal,
            now=datetime(2026, 7, 25, 0, 1, tzinfo=UTC),
            workflow_run_id=2,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert recovery_publisher.calls == []
    assert not output.exists()


def test_recovery_rejects_predecessor_after_a_started_mutation(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    first_publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    journal = tmp_path / "journal"

    with pytest.raises(RuntimeError, match="simulated process crash"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=first_publisher,
            output=output,
            journal=journal,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
            after_publisher_return=lambda: (_ for _ in ()).throw(
                RuntimeError("simulated process crash")
            ),
        )
    reader.resources = dict(before)
    recovery_publisher = FakePublisher(reader)
    with pytest.raises(MODULE.ContractError, match="refusing to republish"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=recovery_publisher,
            output=output,
            journal=journal,
            now=datetime(2026, 7, 25, 0, 1, tzinfo=UTC),
            workflow_run_id=2,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert recovery_publisher.calls == []
    assert not output.exists()


def test_destination_drift_never_emits_authorized_receipt(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    after[MODULE.PUBLIC_RELEASES_URL] = b"drift"
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    with pytest.raises(MODULE.ContractError, match="size"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=output,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert not output.exists()


def test_unpublished_topology_retirement_proof_blocks_before_publisher(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    before[MODULE.PUBLIC_TOPOLOGY_PROOF_URL] = b"forged-or-stale"
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    with pytest.raises(MODULE.ContractError, match="topology retirement proof"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=tmp_path / "receipt.json",
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert publisher.calls == []


@pytest.mark.parametrize(
    "authenticated_transports",
    [None, {}],
)
def test_transaction_requires_provider_authenticated_transports(
    tmp_path: Path,
    authenticated_transports: dict[str, Any] | None,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    with pytest.raises(MODULE.ContractError, match="independently authenticated"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=tmp_path / "receipt.json",
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports=authenticated_transports,
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert publisher.calls == []


def test_existing_receipt_blocks_before_any_network_or_publish(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    output.write_text("existing")
    with pytest.raises(MODULE.ContractError, match="already exists"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=output,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
        )
    assert reader.urls == []
    assert publisher.calls == []


def topology_payload(
    publisher_sha256: str,
    committed: bytes,
    converged: bytes,
) -> dict[str, Any]:
    return {
        "contractName": MODULE.TOPOLOGY_CONTRACT,
        "contractVersion": 1,
        "generatedAt": "2026-07-25T11:00:00Z",
        "status": "passed",
        "source": {
            "repository": "ArchonMegalon/chummer6-hub",
            "ref": "refs/heads/main",
            "commit": "a" * 40,
        },
        "sidecarAuthorityRetired": True,
        "activeSidecarMarkerCount": 0,
        "activeSidecarMarkers": [],
        "retiredAuthoritySha256": "b" * 64,
        "committedBoundaryReceipt": {
            "sha256": digest(committed),
            "sizeBytes": len(committed),
        },
        "postMarkerConvergenceReceipt": {
            "sha256": digest(converged),
            "sizeBytes": len(converged),
        },
        "canonicalAuthority": {
            "baseUrl": MODULE.PUBLIC_BASE_URL,
            "manifestUrl": MODULE.PUBLIC_MANIFEST_URL,
            "publisherPath": MODULE.CANONICAL_PUBLISHER,
            "publisherSha256": publisher_sha256,
        },
    }


def test_topology_proof_requires_committed_retirement_and_zero_markers() -> None:
    publisher = b"canonical publisher"
    committed = b"committed"
    converged = b"converged"
    payload = topology_payload(digest(publisher), committed, converged)
    MODULE.validate_topology_retirement(
        payload,
        now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        publisher_bytes=publisher,
        committed_boundary_bytes=committed,
        post_marker_convergence_bytes=converged,
    )
    payload["activeSidecarMarkerCount"] = 1
    payload["activeSidecarMarkers"] = ["preview-sidecar"]
    with pytest.raises(MODULE.ContractError, match="activeSidecarMarkerCount"):
        MODULE.validate_topology_retirement(
            payload,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            publisher_bytes=publisher,
            committed_boundary_bytes=committed,
            post_marker_convergence_bytes=converged,
        )


def test_operator_confirmation_binds_exact_proposal_and_fresh_actor() -> None:
    proposal_sha = "e" * 64
    MODULE.require_execution_context(
        confirmation=f"PUBLISH:{proposal_sha}",
        proposal_sha256=proposal_sha,
        repository="ArchonMegalon/chummer6-ui",
        ref="refs/heads/main",
        run_attempt="1",
        actor="operator",
        triggering_actor="operator",
        environment_name=MODULE.PUBLICATION_ENVIRONMENT,
        source_sha="a" * 40,
        candidate_source_sha="a" * 40,
    )
    with pytest.raises(MODULE.ContractError, match="exact proposal"):
        MODULE.require_execution_context(
            confirmation=f"PUBLISH:{'f' * 64}",
            proposal_sha256=proposal_sha,
            repository="ArchonMegalon/chummer6-ui",
            ref="refs/heads/main",
            run_attempt="1",
            actor="operator",
            triggering_actor="operator",
            environment_name=MODULE.PUBLICATION_ENVIRONMENT,
            source_sha="a" * 40,
            candidate_source_sha="a" * 40,
        )


def test_public_manifest_must_expose_all_three_exact_open_public_artifacts(
    tmp_path: Path,
) -> None:
    candidate, _before, _after = material(tmp_path)
    candidate_identity = {
        "releaseVersion": "flagship",
        "channelId": "stable",
    }
    downloads = []
    for platform in MODULE.assembler.PLATFORMS:
        artifact = candidate.platforms[platform]["artifact"]
        downloads.append(
            {
                "platformId": platform,
                "fileName": artifact["fileName"],
                "url": (
                    f"{MODULE.PUBLIC_BASE_URL}/files/"
                    f"{artifact['fileName']}"
                ),
                "sha256": artifact["sha256"],
                "sizeBytes": artifact["sizeBytes"],
                "version": "flagship",
                "releaseVersion": "flagship",
                "installAccessClass": "open_public",
                "compatibilityState": "compatible",
            }
        )
    payload = {
        "version": "flagship",
        "releaseVersion": "flagship",
        "status": "published",
        "channelId": "stable",
        "downloads": downloads,
    }
    MODULE.validate_public_manifest_contract(
        json.dumps(payload).encode(),
        label="manifest",
        candidate=candidate_identity,
        platforms=candidate.platforms,
    )
    downloads[2]["installAccessClass"] = "account_required"
    with pytest.raises(MODULE.ContractError, match="installAccessClass"):
        MODULE.validate_public_manifest_contract(
            json.dumps(payload).encode(),
            label="manifest",
            candidate=candidate_identity,
            platforms=candidate.platforms,
        )


def hub_provider_fixture(
    tmp_path: Path,
) -> tuple[Any, FakeHubProvider, str]:
    base_material, _before, _after = material(tmp_path)
    hub_sha = "0123456789abcdef0123456789abcdef01234567"
    committed = b'{"committed":true}\n'
    converged = b'{"converged":true}\n'
    topology = {
        "generatedAt": "2026-07-25T11:00:00Z",
        "source": {
            "repository": MODULE.HUB_REPOSITORY,
            "ref": "refs/heads/main",
            "commit": hub_sha,
        },
    }
    topology_bytes = json.dumps(topology, sort_keys=True).encode() + b"\n"
    bound_material = replace(
        base_material,
        topology_bytes=topology_bytes,
        committed_boundary_bytes=committed,
        post_marker_convergence_bytes=converged,
    )
    entries = {
        "TOPOLOGY_B_RETIREMENT.generated.json": topology_bytes,
        "committed-boundary-receipt.json": committed,
        "post-marker-convergence-receipt.json": converged,
    }
    archive = artifact_zip(entries)
    archive_digest = f"sha256:{digest(archive)}"
    repository_id = 777
    run_id = 601
    workflow_id = 501
    user = {"id": 42, "login": "hub-operator", "type": "User"}
    repository = {"id": repository_id, "full_name": MODULE.HUB_REPOSITORY}
    run = {
        "id": run_id,
        "run_attempt": 1,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "head_branch": "main",
        "head_sha": hub_sha,
        "path": MODULE.HUB_PROOF_WORKFLOW,
        "workflow_id": workflow_id,
        "actor": user,
        "triggering_actor": user,
        "repository": repository,
        "head_repository": repository,
        "pull_requests": [],
        "referenced_workflows": [],
        "created_at": "2026-07-25T10:55:00Z",
        "run_started_at": "2026-07-25T10:56:00Z",
        "updated_at": "2026-07-25T11:06:00Z",
    }
    artifact = {
        "id": 701,
        "name": f"topology-b-committed-retirement-proof-{run_id}-1",
        "size_in_bytes": len(archive),
        "archive_download_url": (
            f"{MODULE.provider_auth.API_ROOT}"
            f"{MODULE.hub_api_path('/actions/artifacts/701/zip')}"
        ),
        "expired": False,
        "created_at": "2026-07-25T11:05:00Z",
        "expires_at": "2026-08-25T11:05:00Z",
        "updated_at": "2026-07-25T11:05:10Z",
        "digest": archive_digest,
        "workflow_run": {
            "id": run_id,
            "repository_id": repository_id,
            "head_repository_id": repository_id,
            "head_branch": "main",
            "head_sha": hub_sha,
        },
    }
    responses = {
        MODULE.hub_api_path(""): {
            "id": repository_id,
            "full_name": MODULE.HUB_REPOSITORY,
            "default_branch": "main",
            "archived": False,
            "disabled": False,
        },
        MODULE.hub_api_path("/actions/artifacts/701"): artifact,
        MODULE.hub_api_path(f"/actions/runs/{run_id}"): run,
        MODULE.hub_api_path(
            f"/actions/runs/{run_id}/attempts/1?exclude_pull_requests=false"
        ): dict(run),
        MODULE.hub_api_path(f"/actions/workflows/{workflow_id}"): {
            "id": workflow_id,
            "path": MODULE.HUB_PROOF_WORKFLOW,
            "state": "active",
        },
        MODULE.hub_api_path("/branches/main"): {
            "name": "main",
            "protected": True,
            "commit": {"sha": hub_sha},
        },
    }
    return bound_material, FakeHubProvider(responses, archive), archive_digest


def test_hub_topology_provider_authenticates_exact_run_artifact_and_bytes(
    tmp_path: Path,
) -> None:
    candidate, provider, archive_digest = hub_provider_fixture(tmp_path)
    authority = MODULE.authenticate_hub_topology_provider(
        provider,
        material=candidate,
        artifact_id=701,
        artifact_name="topology-b-committed-retirement-proof-601-1",
        expected_digest=archive_digest,
        now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
    )
    assert authority["source"]["protected"] is True
    assert authority["run"]["attempt"] == 1
    assert authority["workflow"]["path"] == MODULE.HUB_PROOF_WORKFLOW
    assert authority["artifact"]["archiveSha256"] == archive_digest.removeprefix(
        "sha256:"
    )
    assert provider.archive_reads == 1


def test_hub_topology_provider_rejects_main_or_archive_drift(
    tmp_path: Path,
) -> None:
    candidate, provider, archive_digest = hub_provider_fixture(tmp_path)
    provider.responses[MODULE.hub_api_path("/branches/main")]["commit"]["sha"] = (
        "89abcdef0123456789abcdef0123456789abcdef"
    )
    with pytest.raises(MODULE.ContractError, match="Hub main branch commit"):
        MODULE.authenticate_hub_topology_provider(
            provider,
            material=candidate,
            artifact_id=701,
            artifact_name="topology-b-committed-retirement-proof-601-1",
            expected_digest=archive_digest,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        )

    candidate, provider, archive_digest = hub_provider_fixture(tmp_path / "two")
    provider.archive = artifact_zip(
        {
            "TOPOLOGY_B_RETIREMENT.generated.json": b'{"forged":true}\n',
            "committed-boundary-receipt.json": candidate.committed_boundary_bytes,
            "post-marker-convergence-receipt.json": (
                candidate.post_marker_convergence_bytes
            ),
        }
    )
    with pytest.raises(
        MODULE.provider_auth.ContractError, match="size|provider digest"
    ):
        MODULE.authenticate_hub_topology_provider(
            provider,
            material=candidate,
            artifact_id=701,
            artifact_name="topology-b-committed-retirement-proof-601-1",
            expected_digest=archive_digest,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        )


def test_late_hub_provider_drift_prevents_authorized_receipt(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    with pytest.raises(MODULE.ContractError, match="late Hub"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=output,
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            hub_provider_authority={"generation": 1},
            hub_provider_reauthenticate=lambda: {"generation": 2},
        )
    assert len(publisher.calls) == 1
    assert not output.exists()
