from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
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


def test_transaction_refuses_rerun_before_calling_publisher(
    tmp_path: Path,
) -> None:
    candidate, _before, after = material(tmp_path)
    reader = FakeReader(after, after)
    publisher = FakePublisher(reader)
    with pytest.raises(MODULE.ContractError, match="predecessor"):
        MODULE.execute_transaction(
            material=candidate,
            reader=reader,
            publisher=publisher,
            output=tmp_path / "receipt.json",
            now=datetime(2026, 7, 25, tzinfo=UTC),
            workflow_run_id=1,
            workflow_actor="operator",
            authenticated_transports={"provider": "authenticated"},
        )
    assert publisher.calls == []


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
        )
    assert reader.urls == []
    assert publisher.calls == []


def topology_payload(publisher_sha256: str) -> dict[str, Any]:
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
        "committedBoundaryReceipt": {"sha256": "c" * 64, "sizeBytes": 1},
        "postMarkerConvergenceReceipt": {
            "sha256": "d" * 64,
            "sizeBytes": 1,
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
    payload = topology_payload(digest(publisher))
    MODULE.validate_topology_retirement(
        payload,
        now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
        publisher_bytes=publisher,
    )
    payload["activeSidecarMarkerCount"] = 1
    payload["activeSidecarMarkers"] = ["preview-sidecar"]
    with pytest.raises(MODULE.ContractError, match="activeSidecarMarkerCount"):
        MODULE.validate_topology_retirement(
            payload,
            now=datetime(2026, 7, 25, 12, 0, tzinfo=UTC),
            publisher_bytes=publisher,
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
