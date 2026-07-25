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
from types import SimpleNamespace
from typing import Any

import pytest


ROOT = Path(__file__).resolve().parents[1]
RELEASE_SCRIPTS = ROOT / "scripts" / "release"
sys.path.insert(0, str(RELEASE_SCRIPTS))
SCRIPT = RELEASE_SCRIPTS / "publish_global_flagship_release.py"
ASSEMBLY_SCRIPT = (
    RELEASE_SCRIPTS / "assemble_global_flagship_publication_input.py"
)
SPEC = importlib.util.spec_from_file_location(
    "publish_global_flagship_release", SCRIPT
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
ASSEMBLY_SPEC = importlib.util.spec_from_file_location(
    "assemble_global_flagship_publication_input", ASSEMBLY_SCRIPT
)
assert ASSEMBLY_SPEC is not None and ASSEMBLY_SPEC.loader is not None
ASSEMBLY_MODULE = importlib.util.module_from_spec(ASSEMBLY_SPEC)
sys.modules[ASSEMBLY_SPEC.name] = ASSEMBLY_MODULE
ASSEMBLY_SPEC.loader.exec_module(ASSEMBLY_MODULE)


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


class FakeTransportProvider:
    def __init__(
        self,
        *,
        artifact_id: int,
        metadata: dict[str, Any],
        run: dict[str, Any],
        archive: bytes,
    ) -> None:
        self.artifact_id = artifact_id
        self.metadata = metadata
        self.run = run
        self.archive = archive

    def get_json(self, path: str) -> Any:
        if path.endswith(f"/actions/artifacts/{self.artifact_id}"):
            value = self.metadata
        elif path.endswith(f"/actions/runs/{self.run['id']}"):
            value = self.run
        else:
            raise AssertionError(f"unexpected provider path: {path}")
        return MODULE.provider_auth.JsonResponse(value=value, headers={})

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        assert artifact_id == self.artifact_id
        assert len(self.archive) <= max_bytes
        return self.archive


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
        handoff_path=root / "provider-handoff.json",
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


def test_clock_advance_revalidation_blocks_before_publication_mutation(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    instants = iter(
        (
            datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            datetime(2026, 7, 25, 13, 1, tzinfo=UTC),
        )
    )

    def clock() -> datetime:
        return next(instants)

    def revalidate_fresh_material(observed_now: datetime) -> Any:
        if observed_now >= datetime(2026, 7, 25, 13, 0, tzinfo=UTC):
            raise MODULE.ContractError(
                "candidate/proposal/topology/artifact freshness expired"
            )
        return candidate

    with pytest.raises(MODULE.ContractError, match="freshness expired"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=tmp_path / "receipt.json",
            clock=clock,
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
            transport_reauthenticate=lambda: (
                clock(),
                {"provider": "authenticated"},
            )[1],
            hub_provider_authority={"hub": "authenticated"},
            hub_provider_reauthenticate=lambda: {"hub": "authenticated"},
            freshness_revalidate=revalidate_fresh_material,
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


def post_marker_payload(
    *,
    boundary: str = "post-marker",
    verified_at: str = "2026-07-25T10:58:00Z",
) -> dict[str, Any]:
    convergence = {"targetVersion": 1}
    return {
        "contractName": MODULE.HUB_POST_MARKER_CONTRACT,
        "status": "pass",
        "boundary": boundary,
        "operationRoot": "/operation",
        "restoredVersion": 1,
        "retiredAuthoritySha256": "b" * 64,
        "markerConnectorGateSha256": "d" * 64,
        "connectorConvergence": convergence,
        "connectorConvergenceSha256": digest(
            MODULE.canonical_json_bytes(convergence)
        ),
        "verifiedAtUtc": verified_at,
    }


def terminal_retirement_payload(
    source_sha: str,
    *,
    completed_at: str = "2026-07-25T10:59:00Z",
    post_marker: dict[str, Any] | None = None,
) -> dict[str, Any]:
    marker = post_marker or post_marker_payload()
    marker_sha = digest(MODULE.canonical_json_bytes(marker))
    return {
        "contractName": MODULE.HUB_TERMINAL_CONTRACT,
        "status": "retired",
        "operation": MODULE.HUB_TERMINAL_OPERATION,
        "operationRoot": "/operation",
        "projectName": "chummer",
        "operationSourceHead": "abcdef0123456789abcdef0123456789abcdef01",
        "controllerSourceHead": source_sha,
        "retiredAuthorityPath": "retired-authority.json",
        "retiredAuthoritySha256": "b" * 64,
        "retirementEvidencePath": "retirement-evidence.json",
        "retirementEvidenceSha256": "c" * 64,
        "connectorGateSha256": "d" * 64,
        "postMarkerConnectorGateSha256": marker_sha,
        "latestConnectorGateSha256": marker_sha,
        "priorConfigSha256": "f" * 64,
        "restoredVersion": 1,
        "incumbentBaselineSha256": "1" * 64,
        "incumbentObservationSha256": "1" * 64,
        "cleanupSha256": "2" * 64,
        "completedAtUtc": completed_at,
    }


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
    post_marker = post_marker_payload()
    committed = (
        json.dumps(
            terminal_retirement_payload("a" * 40, post_marker=post_marker),
            sort_keys=True,
        ).encode()
        + b"\n"
    )
    converged = json.dumps(post_marker, sort_keys=True).encode() + b"\n"
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

    late_post_marker = post_marker_payload(
        boundary="resume-post-marker",
        verified_at="2026-07-25T11:01:00Z",
    )
    late_bytes = json.dumps(late_post_marker, sort_keys=True).encode() + b"\n"
    late_payload = topology_payload(digest(publisher), committed, late_bytes)
    with pytest.raises(MODULE.ContractError, match="later than terminal"):
        MODULE.validate_topology_retirement(
            late_payload,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            publisher_bytes=publisher,
            committed_boundary_bytes=committed,
            post_marker_convergence_bytes=late_bytes,
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
    compatibility_projection = MODULE.validate_public_manifest_contract(
        json.dumps(payload).encode(),
        label="manifest",
        candidate=candidate_identity,
        platforms=candidate.platforms,
        artifact_field="downloads",
        platform_field="platformId",
        url_field="url",
    )
    canonical_payload = {
        **payload,
        "artifacts": [
            {
                **{
                    key: value
                    for key, value in row.items()
                    if key not in {"platformId", "url"}
                },
                "platform": row["platformId"],
                "downloadUrl": row["url"],
            }
            for row in downloads
        ],
    }
    canonical_payload.pop("downloads")
    canonical_projection = MODULE.validate_public_manifest_contract(
        json.dumps(canonical_payload).encode(),
        label="canonical manifest",
        candidate=candidate_identity,
        platforms=candidate.platforms,
        artifact_field="artifacts",
        platform_field="platform",
        url_field="downloadUrl",
    )
    MODULE.require_manifest_projection_equality(
        canonical_projection, compatibility_projection
    )
    downloads[2]["installAccessClass"] = "account_required"
    with pytest.raises(MODULE.ContractError, match="installAccessClass"):
        MODULE.validate_public_manifest_contract(
            json.dumps(payload).encode(),
            label="manifest",
            candidate=candidate_identity,
            platforms=candidate.platforms,
            artifact_field="downloads",
            platform_field="platformId",
            url_field="url",
        )
    downloads[2]["installAccessClass"] = "open_public"
    downloads.append(dict(downloads[0], fileName="unexpected.zip"))
    with pytest.raises(MODULE.ContractError, match="exactly the three"):
        MODULE.validate_public_manifest_contract(
            json.dumps(payload).encode(),
            label="manifest",
            candidate=candidate_identity,
            platforms=candidate.platforms,
            artifact_field="downloads",
            platform_field="platformId",
            url_field="url",
        )


def test_canonical_and_compatibility_manifest_projection_divergence_fails() -> None:
    canonical = {
        "windows": {
            "platform": "windows",
            "fileName": "installer.exe",
            "url": f"{MODULE.PUBLIC_BASE_URL}/files/installer.exe",
            "sha256": "a" * 64,
            "sizeBytes": 42,
        }
    }
    compatibility = json.loads(json.dumps(canonical))
    compatibility["windows"]["url"] = (
        f"{MODULE.PUBLIC_BASE_URL}/files/different.exe"
    )
    with pytest.raises(
        MODULE.ContractError,
        match="canonical and compatibility manifest artifact projections",
    ):
        MODULE.require_manifest_projection_equality(
            canonical, compatibility
        )


def test_real_load_material_returns_both_hub_receipt_byte_fields(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    root = tmp_path / "publication-input"
    (root / "public-bundle" / "files").mkdir(parents=True)
    candidate = {
        "candidateId": "candidate-1",
        "releaseVersion": "run-1",
        "source": {"commit": "a" * 40},
    }
    platforms: dict[str, Any] = {}
    for platform, suffix in (
        ("windows", "installer.exe"),
        ("linux", "installer.deb"),
        ("macos", "installer.dmg"),
    ):
        data = f"{platform}-artifact".encode()
        relative = f"artifacts/{platform}-{suffix}"
        path = root / relative
        path.parent.mkdir(exist_ok=True)
        path.write_bytes(data)
        (root / "public-bundle" / "files" / path.name).write_bytes(data)
        platforms[platform] = {
            "artifact": {
                "fileName": path.name,
                "relativePath": relative,
                "sha256": digest(data),
                "sizeBytes": len(data),
            }
        }
    candidate_bytes = json.dumps(candidate).encode()
    proposal_bytes = b'{"proposal":true}\n'
    approval_rows = []
    for role in MODULE.assembler.REQUIRED_APPROVAL_ROLES:
        relative = "approval.json"
        approval_path = root / "approvals" / role / relative
        approval_path.parent.mkdir(parents=True)
        approval_path.write_bytes(f'{{"role":"{role}"}}\n'.encode())
        approval_rows.append(
            {"role": role, "receipt": {"relativePath": relative}}
        )
    final_bytes = json.dumps({"approvals": approval_rows}).encode()
    files = {
        "candidate.json": candidate_bytes,
        "proposal.json": proposal_bytes,
        "final-receipt.json": final_bytes,
        "topology-retirement.json": b'{"topology":true}\n',
        "committed-boundary-receipt.json": b'{"committed":true}\n',
        "post-marker-convergence-receipt.json": b'{"converged":true}\n',
        "destination-plan.json": b'{"destination":true}\n',
        "provider-handoff.json": json.dumps(
            {
                "candidateManifest": {"relativePath": "candidate.json"},
                "proposal": {"relativePath": "proposal.json"},
                "finalReceipt": {"relativePath": "final-receipt.json"},
            }
        ).encode(),
    }
    for relative, data in files.items():
        (root / relative).write_bytes(data)
    (root / "public-bundle" / "RELEASE_CHANNEL.generated.json").write_bytes(
        b'{"canonical":true}\n'
    )
    (root / "public-bundle" / "releases.json").write_bytes(
        b'{"compatibility":true}\n'
    )

    monkeypatch.setattr(
        MODULE.provider_auth,
        "validate_local_bundle",
        lambda bundle, now: SimpleNamespace(
            proposal={"candidate": candidate, "platforms": platforms}
        ),
    )
    monkeypatch.setattr(
        MODULE.assembler,
        "validate_candidate",
        lambda snapshot, now, max_age_seconds: (candidate, platforms, set()),
    )
    monkeypatch.setattr(MODULE, "validate_provider_handoff", lambda *a, **k: None)
    monkeypatch.setattr(
        MODULE, "validate_topology_retirement", lambda *a, **k: None
    )
    destination_calls: list[dict[str, Any]] = []

    def validate_destination_plan(
        payload: dict[str, Any],
        *,
        candidate: dict[str, Any],
        platforms: dict[str, Any],
        public_bundle: Path,
        artifact_identities: dict[str, tuple[str, int]],
        topology_bytes: bytes,
    ) -> None:
        destination_calls.append(
            {
                "payload": payload,
                "candidate": candidate,
                "platforms": platforms,
                "public_bundle": public_bundle,
                "artifact_identities": artifact_identities,
                "topology_bytes": topology_bytes,
            }
        )

    monkeypatch.setattr(
        MODULE, "validate_destination_plan", validate_destination_plan
    )
    monkeypatch.setattr(
        MODULE, "validate_canonical_bundle", lambda **kwargs: None
    )

    loaded = MODULE.load_material(
        publication_root=root,
        handoff_path=root / "provider-handoff.json",
        repository_root=ROOT,
        now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
    )

    assert loaded.committed_boundary_bytes == files[
        "committed-boundary-receipt.json"
    ]
    assert loaded.post_marker_convergence_bytes == files[
        "post-marker-convergence-receipt.json"
    ]
    assert len(destination_calls) == 1
    assert set(loaded.artifact_identities) == set(
        MODULE.assembler.PLATFORMS
    )


def test_assembly_receipt_binds_complete_inventory_and_upstream_graph(
    tmp_path: Path,
) -> None:
    base, _before, _after = material(tmp_path)
    source_sha = "1234567890abcdef1234567890abcdef12345678"
    producer = {
        "actor": "candidate-producer",
        "workflow": ".github/workflows/global-flagship-candidate.yml",
        "runId": 50,
        "runAttempt": 1,
        "artifactName": (
            "global-flagship-candidate-payload-candidate-1-50-1"
        ),
    }
    candidate = {
        **base.candidate,
        "source": {
            "repository": MODULE.assembler.SOURCE_REPOSITORY,
            "ref": "refs/heads/main",
            "commit": source_sha,
        },
        "producer": producer,
    }
    candidate_bytes = json.dumps(candidate).encode()
    proposal_bytes = b'{"proposal":true}\n'
    handoff_bytes = b'{"handoff":true}\n'
    approval_rows: list[dict[str, Any]] = []
    approval_receipts: dict[str, bytes] = {}
    for index, role in enumerate(
        MODULE.assembler.REQUIRED_APPROVAL_ROLES, start=1
    ):
        run_id = 100 + index
        approval_bytes = json.dumps(
            {
                "contractName": MODULE.assembler.APPROVAL_CONTRACT,
                "actor": f"{role}-reviewer",
                "authority": {"runId": run_id},
            }
        ).encode()
        approval_receipts[role] = approval_bytes
        approval_path = base.root / "approvals" / role / "approval.json"
        approval_path.parent.mkdir(parents=True)
        approval_path.write_bytes(approval_bytes)
        approval_rows.append(
            {
                "role": role,
                "receipt": {"relativePath": "approval.json"},
            }
        )
    final_bytes = json.dumps({"approvals": approval_rows}).encode()
    file_bytes = {
        base.handoff_path: handoff_bytes,
        base.candidate_path: candidate_bytes,
        base.proposal_path: proposal_bytes,
        base.final_path: final_bytes,
        base.topology_path: base.topology_bytes,
        base.root
        / "committed-boundary-receipt.json": base.committed_boundary_bytes,
        base.root
        / "post-marker-convergence-receipt.json": (
            base.post_marker_convergence_bytes
        ),
        base.destination_plan_path: base.destination_plan_bytes,
    }
    for path, data in file_bytes.items():
        path.write_bytes(data)
    candidate_material = replace(
        base,
        handoff_bytes=handoff_bytes,
        candidate_bytes=candidate_bytes,
        proposal_bytes=proposal_bytes,
        final_bytes=final_bytes,
        candidate=candidate,
    )

    def authority(
        *,
        name: str,
        run_id: int,
        workflow: str,
        actor: str,
        seed: str,
    ) -> dict[str, Any]:
        sha = seed * 64
        return {
            "artifact": {
                "id": run_id + 1000,
                "name": name,
                "digest": f"sha256:{sha}",
                "sizeBytes": 10,
                "createdAt": "2026-07-25T11:00:00Z",
                "updatedAt": "2026-07-25T11:01:00Z",
                "expiresAt": "2026-08-25T11:00:00Z",
                "workflowRunId": run_id,
            },
            "run": {
                "id": run_id,
                "attempt": 1,
                "workflow": workflow,
                "actor": {"id": run_id, "login": actor, "type": "User"},
                "headSha": source_sha,
            },
            "archiveSha256": sha,
            "archiveSizeBytes": 10,
        }

    assembly_authority = authority(
        name="global-flagship-publication-input-candidate-1-70-1",
        run_id=70,
        workflow=MODULE.ASSEMBLY_WORKFLOW,
        actor="assembly-operator",
        seed="a",
    )
    handoff_authority = authority(
        name="global-flagship-provider-authenticated-handoff-900-60-1",
        run_id=60,
        workflow=MODULE.PROVIDER_HANDOFF_WORKFLOW,
        actor="handoff-operator",
        seed="b",
    )
    candidate_authority = authority(
        name=producer["artifactName"],
        run_id=50,
        workflow=producer["workflow"],
        actor=producer["actor"],
        seed="c",
    )
    upstream_approvals = []
    for index, role in enumerate(
        MODULE.assembler.REQUIRED_APPROVAL_ROLES, start=1
    ):
        run_id = 100 + index
        upstream_approvals.append(
            {
                "role": role,
                "authority": authority(
                    name=(
                        "global-flagship-release-approval-"
                        f"{role}-{run_id}-1"
                    ),
                    run_id=run_id,
                    workflow=MODULE.assembler.APPROVAL_WORKFLOW,
                    actor=f"{role}-reviewer",
                    seed=str(index),
                ),
                "receipt": MODULE.binding_bytes(
                    approval_receipts[role],
                    f"approvals/{role}/approval.json",
                    contractName=MODULE.assembler.APPROVAL_CONTRACT,
                ),
            }
        )
    manifests = {}
    for key, file_name in (
        ("canonical", "RELEASE_CHANNEL.generated.json"),
        ("releases", "releases.json"),
    ):
        data = (base.public_bundle / file_name).read_bytes()
        manifests[key] = MODULE.binding_bytes(
            data, f"public-bundle/{file_name}"
        )
    platform_bindings = {}
    for platform in MODULE.assembler.PLATFORMS:
        artifact = base.platforms[platform]["artifact"]
        relative = f"public-bundle/files/{artifact['fileName']}"
        platform_bindings[platform] = MODULE.binding_bytes(
            (base.root / relative).read_bytes(), relative
        )
    receipt = {
        "contractName": MODULE.ASSEMBLY_CONTRACT,
        "contractVersion": MODULE.ASSEMBLY_CONTRACT_VERSION,
        "generatedAt": "2026-07-25T12:00:00Z",
        "expiresAt": "2026-07-25T13:00:00Z",
        "status": "passed",
        "candidate": {
            "candidateId": candidate["candidateId"],
            "releaseVersion": candidate["releaseVersion"],
            "source": candidate["source"],
            "producer": producer,
        },
        "assembly": {
            "repository": MODULE.assembler.SOURCE_REPOSITORY,
            "ref": "refs/heads/main",
            "sha": source_sha,
            "workflow": MODULE.ASSEMBLY_WORKFLOW,
            "runId": 70,
            "runAttempt": 1,
            "actor": "assembly-operator",
            "environment": MODULE.ASSEMBLY_ENVIRONMENT,
        },
        "upstreamArtifacts": {
            "candidatePayload": candidate_authority,
            "providerInput": {
                "artifact": {
                    "id": 900,
                    "name": MODULE.provider_auth.INPUT_ARTIFACT_NAME,
                    "digest": f"sha256:{'d' * 64}",
                    "sizeBytes": 10,
                    "createdAt": "2026-07-25T11:00:00Z",
                    "updatedAt": "2026-07-25T11:01:00Z",
                    "expiresAt": "2026-08-25T11:00:00Z",
                    "workflowRunId": 40,
                },
                "archiveSha256": "d" * 64,
                "archiveSizeBytes": 10,
                "trustedAsAuthority": False,
                "purpose": "bounded-metadata-transport-only",
            },
            "providerHandoff": handoff_authority,
            "approvals": upstream_approvals,
            "hubTopology": {"provider": "hub"},
        },
        "providerHandoff": MODULE.binding_bytes(
            handoff_bytes,
            "provider-handoff.json",
            contractName=MODULE.provider_auth.HANDOFF_CONTRACT,
        ),
        "candidateManifest": MODULE.binding_bytes(
            candidate_bytes,
            base.candidate_path.relative_to(base.root).as_posix(),
            contractName=MODULE.assembler.CANDIDATE_CONTRACT,
        ),
        "proposal": MODULE.binding_bytes(
            proposal_bytes,
            base.proposal_path.relative_to(base.root).as_posix(),
            contractName=MODULE.assembler.PROPOSAL_CONTRACT,
        ),
        "finalReceipt": MODULE.binding_bytes(
            final_bytes,
            base.final_path.relative_to(base.root).as_posix(),
            contractName=MODULE.assembler.FINAL_RECEIPT_CONTRACT,
        ),
        "topologyRetirement": MODULE.binding_bytes(
            base.topology_bytes,
            "topology-retirement.json",
            contractName=MODULE.TOPOLOGY_CONTRACT,
        ),
        "committedBoundaryReceipt": MODULE.binding_bytes(
            base.committed_boundary_bytes,
            "committed-boundary-receipt.json",
        ),
        "postMarkerConvergenceReceipt": MODULE.binding_bytes(
            base.post_marker_convergence_bytes,
            "post-marker-convergence-receipt.json",
        ),
        "destinationPlan": MODULE.binding_bytes(
            base.destination_plan_bytes,
            "destination-plan.json",
            contractName=MODULE.DESTINATION_PLAN_CONTRACT,
        ),
        "manifests": manifests,
        "platforms": platform_bindings,
        "inventory": MODULE.publication_input_inventory(base.root),
        "nonPublishing": True,
        "publicationAuthorized": False,
        "releaseArtifactBytesAuthenticated": True,
    }

    MODULE.validate_assembly_receipt(
        receipt,
        receipt_bytes=b"assembly receipt",
        material=candidate_material,
        assembly_authority=assembly_authority,
        handoff_authority=handoff_authority,
        now=datetime(2026, 7, 25, 12, 1, tzinfo=UTC),
    )
    first_platform = MODULE.assembler.PLATFORMS[0]
    first_file = base.platforms[first_platform]["artifact"]["fileName"]
    (base.public_bundle / "files" / first_file).write_bytes(b"corrupted")
    with pytest.raises(MODULE.ContractError, match="sha256"):
        MODULE.validate_assembly_receipt(
            receipt,
            receipt_bytes=b"assembly receipt",
            material=candidate_material,
            assembly_authority=assembly_authority,
            handoff_authority=handoff_authority,
            now=datetime(2026, 7, 25, 12, 1, tzinfo=UTC),
        )


def test_direct_provider_archive_rejects_corruption_and_clock_expiry() -> None:
    artifact_id = 81
    run_id = 71
    repository_id = 61
    source_sha = "1234567890abcdef1234567890abcdef12345678"
    name = f"global-flagship-provider-authenticated-handoff-51-{run_id}-1"
    expected_archive = artifact_zip({"handoff.json": b'{"passed":true}\n'})
    corrupted_archive = bytearray(expected_archive)
    corrupted_archive[-1] ^= 1
    digest_value = f"sha256:{digest(expected_archive)}"
    metadata = {
        "id": artifact_id,
        "name": name,
        "size_in_bytes": len(expected_archive),
        "archive_download_url": (
            f"{MODULE.provider_auth.API_ROOT}"
            f"{MODULE.provider_auth.repository_api_path(
                f'/actions/artifacts/{artifact_id}/zip'
            )}"
        ),
        "expired": False,
        "created_at": "2026-07-25T11:00:00Z",
        "expires_at": "2026-08-25T11:00:00Z",
        "updated_at": "2026-07-25T11:01:00Z",
        "digest": digest_value,
        "workflow_run": {
            "id": run_id,
            "repository_id": repository_id,
            "head_repository_id": repository_id,
            "head_branch": "main",
            "head_sha": source_sha,
        },
    }
    user = {"id": 7, "login": "release-operator", "type": "User"}
    run = {
        "id": run_id,
        "run_attempt": 1,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "head_branch": "main",
        "head_sha": source_sha,
        "path": MODULE.PROVIDER_HANDOFF_WORKFLOW,
        "actor": user,
        "triggering_actor": user,
        "pull_requests": [],
        "referenced_workflows": [],
    }
    client = FakeTransportProvider(
        artifact_id=artifact_id,
        metadata=metadata,
        run=run,
        archive=bytes(corrupted_archive),
    )

    with pytest.raises(
        MODULE.provider_auth.ContractError,
        match="archive bytes do not match the provider digest",
    ):
        MODULE.authenticate_transport_archive(
            client,
            artifact_id=artifact_id,
            expected_digest=digest_value,
            expected_name=name,
            expected_run_id=run_id,
            source_sha=source_sha,
            repository_id=repository_id,
            maximum_bytes=1024 * 1024,
            workflow=MODULE.PROVIDER_HANDOFF_WORKFLOW,
            actor=None,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            label="direct handoff",
        )

    expiring_metadata = dict(metadata)
    expiring_metadata["expires_at"] = "2026-07-25T12:30:00Z"
    expiring_client = FakeTransportProvider(
        artifact_id=artifact_id,
        metadata=expiring_metadata,
        run=run,
        archive=expected_archive,
    )
    provider_instants = iter(
        (
            datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            datetime(2026, 7, 25, 12, 15, tzinfo=UTC),
            datetime(2026, 7, 25, 13, 0, tzinfo=UTC),
        )
    )
    with pytest.raises(
        MODULE.provider_auth.ContractError,
        match="metadata recheck.expires_at is expired",
    ):
        MODULE.authenticate_transport_archive(
            expiring_client,
            artifact_id=artifact_id,
            expected_digest=digest_value,
            expected_name=name,
            expected_run_id=run_id,
            source_sha=source_sha,
            repository_id=repository_id,
            maximum_bytes=1024 * 1024,
            workflow=MODULE.PROVIDER_HANDOFF_WORKFLOW,
            actor=None,
            clock=lambda: next(provider_instants),
            label="direct handoff",
        )


def hub_provider_fixture(
    tmp_path: Path,
) -> tuple[Any, FakeHubProvider, str]:
    base_material, _before, _after = material(tmp_path)
    hub_sha = "0123456789abcdef0123456789abcdef01234567"
    provider_sha = "89abcdef0123456789abcdef0123456789abcdef"
    committed = (
        json.dumps(
            terminal_retirement_payload(hub_sha),
            sort_keys=True,
        ).encode()
        + b"\n"
    )
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
        "head_sha": provider_sha,
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
            "head_sha": provider_sha,
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
            "commit": {"sha": provider_sha},
        },
        MODULE.hub_api_path(
            f"/compare/{hub_sha}...{provider_sha}"
        ): {
            "status": "ahead",
            "ahead_by": 3,
            "behind_by": 0,
            "base_commit": {"sha": hub_sha},
            "merge_base_commit": {"sha": hub_sha},
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
    assert authority["source"]["sourceSha"] != authority["source"][
        "providerSourceSha"
    ]
    assert authority["source"]["ancestry"]["status"] == "ahead"
    assert authority["run"]["attempt"] == 1
    assert authority["workflow"]["path"] == MODULE.HUB_PROOF_WORKFLOW
    assert authority["artifact"]["archiveSha256"] == archive_digest.removeprefix(
        "sha256:"
    )
    assert provider.archive_reads == 1


def test_assembly_direct_hub_download_uses_hub_repository_authority(
    tmp_path: Path,
) -> None:
    _candidate, provider, archive_digest = hub_provider_fixture(tmp_path)
    artifact = provider.responses[
        MODULE.hub_api_path("/actions/artifacts/701")
    ]
    archive = ASSEMBLY_MODULE.raw_hub_artifact(
        provider,
        artifact_id=701,
        expected_name="topology-b-committed-retirement-proof-601-1",
        expected_digest=archive_digest,
        repository_id=777,
        source_sha=artifact["workflow_run"]["head_sha"],
        clock=lambda: datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
    )
    assert archive == provider.archive
    assert provider.archive_reads == 1


def test_hub_topology_provider_rejects_main_or_archive_drift(
    tmp_path: Path,
) -> None:
    candidate, provider, archive_digest = hub_provider_fixture(tmp_path)
    provider.responses[MODULE.hub_api_path("/branches/main")]["commit"]["sha"] = (
        "fedcba9876543210fedcba9876543210fedcba98"
    )
    with pytest.raises(
        MODULE.ContractError, match="Hub main branch provider commit"
    ):
        MODULE.authenticate_hub_topology_provider(
            provider,
            material=candidate,
            artifact_id=701,
            artifact_name="topology-b-committed-retirement-proof-601-1",
            expected_digest=archive_digest,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        )

    candidate, provider, archive_digest = hub_provider_fixture(
        tmp_path / "ancestry"
    )
    compare_path = next(
        path for path in provider.responses if "/compare/" in path
    )
    provider.responses[compare_path]["merge_base_commit"]["sha"] = "f" * 40
    with pytest.raises(MODULE.ContractError, match="ancestry merge base"):
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


def test_hub_provider_drift_blocks_before_publication_mutation(
    tmp_path: Path,
) -> None:
    candidate, before, after = material(tmp_path)
    reader = FakeReader(before, after)
    publisher = FakePublisher(reader)
    output = tmp_path / "receipt.json"
    with pytest.raises(
        MODULE.ContractError,
        match="immediately before publication mutation Hub provider",
    ):
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
    assert publisher.calls == []
    assert not output.exists()
