from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "materialize-downloads-publication-scope.py"


def load_module():
    spec = importlib.util.spec_from_file_location(
        "materialize_downloads_publication_scope", SCRIPT
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


MODULE = load_module()


def load_transaction_module():
    path = ROOT / "scripts" / "windows_only_publication_transaction.py"
    spec = importlib.util.spec_from_file_location(
        "downloads_scope_transaction_fixture", path
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


TRANSACTION = load_transaction_module()

VERSION = "nightly-1"
CANONICAL_MANIFEST_SHA256 = "1" * 64
INVENTORY_SHA256 = "2" * 64
FILE_COUNT = 7
TOTAL_BYTES = 4096
FULL_SHELF_INVENTORY_SHA256 = "3" * 64
RUN_ORIGIN = "https://chummer.run"
RUN_SESSION_ID = "0123456789abcdef0123456789abcdef"
RUN_UPLOAD_PATHS = [
    "RELEASE_CHANNEL.generated.json",
    "files/artifact-1.bin",
    "files/artifact-2.bin",
    "files/artifact-3.bin",
    "files/artifact-4.bin",
    "files/artifact-5.bin",
    "releases.json",
]


def finalized_scope(platform: str = "windows") -> dict[str, object]:
    return {
        "approvalIndependent": True,
        "authenticodeRequired": True,
        "contractName": "chummer6-ui.preview-nightly-windows-publication-scope",
        "contractVersion": 2,
        "deployAuthorized": False,
        "fullShelfInventorySha256": FULL_SHELF_INVENTORY_SHA256,
        "fullShelfManifestSha256": "a" * 64,
        "incumbentSnapshotSha256": "b" * 64,
        "nativeEvidenceSha256": "c" * 64,
        "publicationDeltaTuples": [{"platform": platform}],
        "publicationEligible": False,
        "registryFinalizeEligible": True,
        "scopeDecisionSha256": "d" * 64,
        "signingReceiptSha256": "e" * 64,
        "status": "validated",
        "uploadAuthorized": False,
    }


def completed_run_receipt(
    *,
    version: str = VERSION,
    canonical_manifest_sha256: str = CANONICAL_MANIFEST_SHA256,
    inventory_sha256: str = INVENTORY_SHA256,
    file_count: int = FILE_COUNT,
    total_bytes: int = TOTAL_BYTES,
    session_id: str = RUN_SESSION_ID,
) -> dict[str, Any]:
    candidate = {
        "version": version,
        "canonicalManifestSha256": canonical_manifest_sha256,
        "inventorySha256": inventory_sha256,
        "fileCount": file_count,
        "totalBytes": total_bytes,
    }
    identity_material = json.dumps(
        candidate, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    candidate["bundleIdentitySha256"] = hashlib.sha256(identity_material).hexdigest()
    timestamps = [
        "2026-07-21T12:00:00.000Z",
        "2026-07-21T12:01:00.000Z",
        "2026-07-21T12:02:00.000Z",
        "2026-07-21T12:03:00.000Z",
    ]
    states = ["created", "uploaded", "request_started", "completed"]
    return {
        "schemaVersion": "chummer.release-upload-handoff/v1",
        "apiOrigin": RUN_ORIGIN,
        "sessionId": session_id,
        "expiresAtUtc": "2026-07-22T00:00:00Z",
        "candidate": candidate,
        "completion": {
            "state": "completed",
            "requestStartedAtUtc": timestamps[2],
            "lastUpdatedAtUtc": timestamps[3],
            "lastHttpStatus": None,
            "lastProblemType": None,
            "traceId": None,
        },
        "stateHistory": [
            {"state": state, "atUtc": at_utc}
            for state, at_utc in zip(states, timestamps)
        ],
    }


def activation_binding() -> dict[str, Any]:
    return {
        "activationProofs": [
            {
                "fullShelfInventorySha256": FULL_SHELF_INVENTORY_SHA256,
                "generationPath": "/transactions/generation-0",
                "generationReceiptSha256": "4" * 64,
                "incumbentInventorySha256": "5" * 64,
                "index": 0,
                "path": "/receipts/0000.activation.json",
                "preparedInventorySha256": "6" * 64,
                "sha256": "7" * 64,
                "sizeBytes": 1024,
                "target": "/srv/downloads",
            }
        ],
        "contractName": "chummer6-ui.windows-only-publication-activation-binding",
        "contractVersion": 1,
        "fullShelfInventorySha256": FULL_SHELF_INVENTORY_SHA256,
        "journal": {
            "path": "/receipts/run.transaction.json",
            "sha256": "8" * 64,
            "sizeBytes": 2048,
        },
        "proposalSha256": "9" * 64,
        "publicationScopeSha256": "f" * 64,
        "rollbackPolicy": (
            "rollback_all_activated_targets_unless_an_exact_commit_record_binds_the_"
            "journal_and_publication_receipt"
        ),
        "runUploadPaths": RUN_UPLOAD_PATHS,
        "runUploadCandidate": completed_run_receipt()["candidate"],
        "scopeDecisionSha256": "d" * 64,
        "transactionId": "windows-nightly-test-0001",
    }


def create_activation_journal(
    root: Path,
    *,
    publication_receipt: Path,
    current_receipt: Path,
    publication_scope_sha256: str,
) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    publication_receipt.parent.mkdir(parents=True, exist_ok=True)
    current_receipt.parent.mkdir(parents=True, exist_ok=True)
    target = root / "activated-target"
    target.mkdir()
    (target / "activated.bin").write_bytes(b"activated shelf")
    prepared_sha256 = TRANSACTION.canonical_sha256(
        TRANSACTION.inventory_tree(target)
    )
    generation = root / "predecessor-generation"
    generation.mkdir()
    (generation / "incumbent.bin").write_bytes(b"incumbent shelf")
    incumbent_sha256 = TRANSACTION.canonical_sha256(
        TRANSACTION.inventory_tree(generation)
    )
    payload = {
        "contractName": TRANSACTION.ACTIVATION_CONTRACT_NAME,
        "contractVersion": 1,
        "fullShelfInventorySha256": FULL_SHELF_INVENTORY_SHA256,
        "generationPath": str(generation.resolve()),
        "generationReceiptSha256": "4" * 64,
        "incumbentInventorySha256": incumbent_sha256,
        "preparedInventorySha256": prepared_sha256,
        "proposalSha256": "9" * 64,
        "publicationScopeSha256": publication_scope_sha256,
        "runUploadPaths": RUN_UPLOAD_PATHS,
        "runUploadCandidate": completed_run_receipt()["candidate"],
        "scopeDecisionSha256": "d" * 64,
        "status": "activated",
        "target": str(target.resolve()),
        "transactionId": "windows-nightly-test-0001",
    }
    ephemeral = root / "activation.json"
    ephemeral.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    journal = publication_receipt.parent / f"{publication_receipt.name}.transaction.json"
    TRANSACTION.create_activation_journal(
        argparse.Namespace(
            transaction_id=payload["transactionId"],
            activation_receipt=[ephemeral],
            journal=journal,
            proof_dir=(
                publication_receipt.parent
                / f"{publication_receipt.name}.activation-proofs"
            ),
            publication_receipt=publication_receipt,
            current_receipt=current_receipt,
        )
    )
    return journal


def run_binding_kwargs(
    payload: dict[str, Any],
    *,
    expected_manifest_sha256: str = CANONICAL_MANIFEST_SHA256,
    expected_inventory_sha256: str = INVENTORY_SHA256,
    expected_file_count: int = FILE_COUNT,
    expected_total_bytes: int = TOTAL_BYTES,
) -> dict[str, Any]:
    receipt_bytes = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    receipt_sha256 = hashlib.sha256(receipt_bytes).hexdigest()
    return {
        "run_upload_receipt_bytes": receipt_bytes,
        "run_upload_receipt_path": "/evidence/release-upload-handoff.json",
        "expected_run_upload_receipt_sha256": receipt_sha256,
        "expected_run_api_origin": RUN_ORIGIN,
        "expected_run_session_id": RUN_SESSION_ID,
        "frozen_canonical_manifest_sha256": expected_manifest_sha256,
        "frozen_inventory_sha256": expected_inventory_sha256,
        "frozen_file_count": expected_file_count,
        "frozen_total_bytes": expected_total_bytes,
    }


def build_v2(**overrides: Any) -> dict[str, Any]:
    arguments: dict[str, Any] = {
        "deploy_dir": "/srv/downloads",
        "release_version": VERSION,
        "release_channel": "preview",
        "promoted_artifact_count": 4,
        "deploy_mode": True,
        "live_verify_target": "https://example.invalid/downloads/releases.json",
        "require_external_publish": True,
        "windows_publication_scope": finalized_scope(),
        "windows_publication_scope_sha256": "f" * 64,
        "windows_activation_binding": activation_binding(),
    }
    arguments.update(overrides)
    return MODULE.build_receipt(**arguments)


def test_v2_deploy_and_live_url_without_receipts_is_blocked() -> None:
    receipt = build_v2()

    assert receipt["schema"] == "chummer.downloads.publication_scope.v2"
    assert receipt["status"] == "blocked"
    assert receipt["scope"] == "local_downloads_shelf_only"
    assert receipt["externalArtifactPublishVerified"] is False
    binding = receipt["windowsOnlyPublicationScope"]
    assert binding["runUploadReceiptVerified"] is False
    assert binding["hubConvergenceVerified"] is False
    assert binding["liveVerifyTargetReferenceOnly"] is True


def test_v2_preexisting_matching_endpoint_is_never_external_evidence() -> None:
    receipt = build_v2(
        live_verify_target=f"https://chummer.run/downloads/{VERSION}/releases.json",
        require_external_publish=False,
    )

    assert receipt["status"] == "passed"
    assert receipt["scope"] == "local_downloads_shelf_only"
    assert receipt["externalArtifactPublishVerified"] is False


def test_v2_completed_exact_run_receipt_still_cannot_claim_hub_convergence() -> None:
    receipt = build_v2(**run_binding_kwargs(completed_run_receipt()))

    assert receipt["status"] == "blocked"
    assert receipt["externalArtifactPublishVerified"] is False
    binding = receipt["windowsOnlyPublicationScope"]
    assert binding["runUploadReceiptVerified"] is True
    assert binding["runUploadReceiptContract"] == "chummer.release-upload-handoff/v1"
    assert binding["runUploadReceiptPath"] == "/evidence/release-upload-handoff.json"
    assert len(binding["runUploadReceiptSha256"]) == 64
    assert binding["targetOrigin"] == RUN_ORIGIN
    assert binding["runSessionId"] == RUN_SESSION_ID
    assert binding["publicationScopeSha256"] == "f" * 64
    assert binding["runCandidateBinding"] == completed_run_receipt()["candidate"]
    assert binding["frozenGenerationBindingVerified"] is True
    assert binding["hubPostdeploySchemaEnrolled"] is False
    assert binding["hubPostdeployBindingVerified"] is False
    assert binding["hubConvergenceVerified"] is False
    assert binding["hubPostdeployAuthorityReason"] == "no_exact_schema_enrolled"


def test_v2_opaque_hub_receipt_digest_is_reference_only() -> None:
    hub_receipt_bytes = b'{"claimedStatus":"passed"}\n'
    hub_sha256 = hashlib.sha256(hub_receipt_bytes).hexdigest()
    receipt = build_v2(
        **run_binding_kwargs(completed_run_receipt()),
        hub_postdeploy_receipt_bytes=hub_receipt_bytes,
        hub_postdeploy_receipt_path="/evidence/hub-postdeploy.json",
        expected_hub_postdeploy_receipt_sha256=hub_sha256,
    )

    binding = receipt["windowsOnlyPublicationScope"]
    assert binding["hubPostdeployReceiptPath"] == "/evidence/hub-postdeploy.json"
    assert binding["hubPostdeployReceiptSha256"] == hub_sha256
    assert binding["hubPostdeployReferenceDigestVerified"] is True
    assert binding["hubPostdeploySchemaEnrolled"] is False
    assert binding["hubPostdeployBindingVerified"] is False
    assert binding["hubConvergenceVerified"] is False
    assert receipt["externalArtifactPublishVerified"] is False
    assert receipt["status"] == "blocked"


@pytest.mark.parametrize(
    "payload",
    [
        completed_run_receipt(inventory_sha256="9" * 64),
        completed_run_receipt(version="nightly-from-an-older-generation"),
    ],
    ids=["wrong-candidate", "replayed-candidate"],
)
def test_v2_rejects_wrong_or_replayed_run_candidate(payload: dict[str, Any]) -> None:
    with pytest.raises(ValueError, match="frozen generation binding"):
        build_v2(**run_binding_kwargs(payload))


def test_v2_rejects_run_receipt_with_extra_top_level_property() -> None:
    payload = completed_run_receipt()
    payload["liveUrl"] = "https://chummer.run/downloads/releases.json"

    with pytest.raises(ValueError, match="top-level property set"):
        build_v2(**run_binding_kwargs(payload))


def test_v2_rejects_nonterminal_run_receipt() -> None:
    payload = completed_run_receipt()
    payload["completion"]["state"] = "request_started"

    with pytest.raises(ValueError, match="not terminally completed"):
        build_v2(**run_binding_kwargs(payload))


def test_v2_rejects_run_receipt_hash_mismatch() -> None:
    arguments = run_binding_kwargs(completed_run_receipt())
    arguments["expected_run_upload_receipt_sha256"] = "0" * 64

    with pytest.raises(ValueError, match="does not match the expected binding"):
        build_v2(**arguments)


def test_v2_recomputes_run_candidate_bundle_identity() -> None:
    payload = completed_run_receipt()
    payload["candidate"]["bundleIdentitySha256"] = "0" * 64

    with pytest.raises(ValueError, match="does not bind the candidate summary"):
        build_v2(**run_binding_kwargs(payload))


def test_v2_rejects_unbound_run_session() -> None:
    payload = completed_run_receipt(
        session_id="abcdef0123456789abcdef0123456789"
    )

    with pytest.raises(ValueError, match="does not match the expected session"):
        build_v2(**run_binding_kwargs(payload))


def test_v2_rejects_unverified_hub_receipt_reference() -> None:
    with pytest.raises(ValueError, match="does not match the expected reference"):
        build_v2(
            **run_binding_kwargs(completed_run_receipt()),
            hub_postdeploy_receipt_bytes=b"opaque-reference",
            hub_postdeploy_receipt_path="/evidence/hub-postdeploy.json",
            expected_hub_postdeploy_receipt_sha256="0" * 64,
        )


def test_v2_receipt_preserves_producer_boundary() -> None:
    receipt = MODULE.build_receipt(
        deploy_dir="/srv/downloads",
        release_version=VERSION,
        release_channel="preview",
        promoted_artifact_count=4,
        deploy_mode=True,
        live_verify_target="https://example.invalid/downloads/releases.json",
        require_external_publish=True,
        windows_publication_scope=finalized_scope(),
        windows_publication_scope_sha256="f" * 64,
        windows_activation_binding=activation_binding(),
    )

    assert receipt["schema"] == "chummer.downloads.publication_scope.v2"
    assert receipt["externalArtifactPublishVerified"] is False
    assert receipt["status"] == "blocked"
    binding = receipt["windowsOnlyPublicationScope"]
    assert binding["publicationDeltaPlatforms"] == ["windows"]
    assert binding["producerUploadAuthorized"] is False
    assert binding["producerDeployAuthorized"] is False
    assert binding["registryFinalizeEligible"] is True
    assert binding["uiPublicationEligible"] is False
    assert binding["publicationScopeSha256"] == "f" * 64
    assert receipt["windowsOnlyActivation"] == activation_binding()
    assert receipt["transactionCommitRequired"] is True
    assert receipt["transactionCommitState"] == "awaiting_exact_commit_record"


def test_v2_receipt_requires_exact_nonreplayed_activation_binding() -> None:
    with pytest.raises(ValueError, match="activation binding contract"):
        build_v2(windows_activation_binding=None)

    replayed = activation_binding()
    duplicate = dict(replayed["activationProofs"][0])
    duplicate["index"] = 1
    duplicate["path"] = "/receipts/0001.activation.json"
    duplicate["sha256"] = "0" * 64
    replayed["activationProofs"] = [replayed["activationProofs"][0], duplicate]
    with pytest.raises(ValueError, match="target is replayed"):
        build_v2(windows_activation_binding=replayed)

    wrong_shelf = activation_binding()
    wrong_shelf["fullShelfInventorySha256"] = "0" * 64
    with pytest.raises(ValueError, match="different full shelf"):
        build_v2(windows_activation_binding=wrong_shelf)

    wrong_scope = activation_binding()
    wrong_scope["publicationScopeSha256"] = "0" * 64
    with pytest.raises(ValueError, match="another publication scope"):
        build_v2(windows_activation_binding=wrong_scope)

    wrong_decision = activation_binding()
    wrong_decision["scopeDecisionSha256"] = "0" * 64
    with pytest.raises(ValueError, match="another scope decision"):
        build_v2(windows_activation_binding=wrong_decision)


def test_v2_receipt_rejects_non_windows_delta() -> None:
    with pytest.raises(ValueError, match="another platform"):
        MODULE.build_receipt(
            deploy_dir="/srv/downloads",
            release_version="nightly-1",
            release_channel="preview",
            promoted_artifact_count=4,
            deploy_mode=False,
            live_verify_target="",
            require_external_publish=False,
            windows_publication_scope=finalized_scope("linux"),
            windows_publication_scope_sha256="f" * 64,
            windows_activation_binding=activation_binding(),
        )


def test_v2_cli_writes_status_atomically_and_refuses_overwrite(tmp_path: Path) -> None:
    scope_path = tmp_path / "scope.json"
    scope_path.write_text(
        json.dumps(finalized_scope(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    output = tmp_path / "status" / "PUBLICATION_SCOPE.generated.json"
    current = tmp_path / "status" / "PUBLICATION_SCOPE.current.json"
    journal = create_activation_journal(
        tmp_path / "activation",
        publication_receipt=output,
        current_receipt=current,
        publication_scope_sha256=hashlib.sha256(scope_path.read_bytes()).hexdigest(),
    )
    command = [
        sys.executable,
        str(SCRIPT),
        "--output",
        str(output),
        "--deploy-dir",
        str(tmp_path / "downloads"),
        "--release-version",
        VERSION,
        "--release-channel",
        "preview",
        "--promoted-artifact-count",
        "4",
        "--windows-publication-scope",
        str(scope_path),
        "--windows-activation-journal",
        str(journal),
    ]

    first = subprocess.run(command, text=True, capture_output=True, check=False)
    assert first.returncode == 0, first.stderr
    first_bytes = output.read_bytes()
    assert output.stat().st_mode & 0o777 == 0o600

    second = subprocess.run(command, text=True, capture_output=True, check=False)
    assert second.returncode != 0
    assert "refusing to overwrite" in second.stderr
    assert output.read_bytes() == first_bytes


def test_exclusive_receipt_fsync_failure_removes_success_receipt_durably(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    output = tmp_path / "receipts" / "run.committed.json"
    original_fsync = MODULE.os.fsync
    calls = 0

    def fail_directory_fsync(descriptor: int) -> None:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise OSError("injected receipt directory fsync failure")
        original_fsync(descriptor)

    monkeypatch.setattr(MODULE.os, "fsync", fail_directory_fsync)

    with pytest.raises(OSError, match="receipt directory fsync failure"):
        MODULE._write_json_exclusive_atomic(output, {"status": "passed"})

    assert calls >= 3
    assert not output.exists()


def test_current_receipt_fsync_failure_restores_previous_pointer_durably(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    current = tmp_path / "PUBLICATION_SCOPE.current.json"
    previous = b'{"generation":"previous"}\n'
    current.write_bytes(previous)
    current.chmod(0o600)
    original_fsync = MODULE.os.fsync
    calls = 0

    def fail_first_directory_fsync(descriptor: int) -> None:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise OSError("injected current pointer directory fsync failure")
        original_fsync(descriptor)

    monkeypatch.setattr(MODULE.os, "fsync", fail_first_directory_fsync)

    with pytest.raises(OSError, match="current pointer directory fsync failure"):
        MODULE._write_json_replace_atomic(current, {"generation": "new"})

    assert calls >= 4
    assert current.read_bytes() == previous
    assert current.stat().st_mode & 0o777 == 0o600


def test_v2_cli_hashes_and_binds_exact_run_receipt_bytes(tmp_path: Path) -> None:
    scope_path = tmp_path / "scope.json"
    scope_path.write_text(
        json.dumps(finalized_scope(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    run_path = tmp_path / "release-upload-handoff.json"
    run_bytes = (
        json.dumps(completed_run_receipt(), indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    run_path.write_bytes(run_bytes)
    run_path.chmod(0o600)
    run_sha256 = hashlib.sha256(run_bytes).hexdigest()
    output = tmp_path / "status.json"
    abort_output = tmp_path / "status.aborted.json"
    journal = create_activation_journal(
        tmp_path / "activation",
        publication_receipt=output,
        current_receipt=tmp_path / "status.current.json",
        publication_scope_sha256=hashlib.sha256(scope_path.read_bytes()).hexdigest(),
    )

    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--output",
            str(output),
            "--abort-output",
            str(abort_output),
            "--deploy-dir",
            str(tmp_path / "downloads"),
            "--release-version",
            VERSION,
            "--release-channel",
            "preview",
            "--promoted-artifact-count",
            "4",
            "--deploy-mode",
            "--live-verify-target",
            "https://chummer.run/downloads/releases.json",
            "--require-external-publish",
            "--windows-publication-scope",
            str(scope_path),
            "--windows-activation-journal",
            str(journal),
            "--run-upload-receipt",
            str(run_path),
            "--expected-run-upload-receipt-sha256",
            run_sha256,
            "--expected-run-api-origin",
            RUN_ORIGIN,
            "--expected-run-session-id",
            RUN_SESSION_ID,
            "--frozen-canonical-manifest-sha256",
            CANONICAL_MANIFEST_SHA256,
            "--frozen-inventory-sha256",
            INVENTORY_SHA256,
            "--frozen-file-count",
            str(FILE_COUNT),
            "--frozen-total-bytes",
            str(TOTAL_BYTES),
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert not output.exists()
    receipt = json.loads(abort_output.read_text(encoding="utf-8"))
    assert receipt["receiptDisposition"] == (
        "publication_aborted_shelf_rollback_required"
    )
    binding = receipt["windowsOnlyPublicationScope"]
    assert binding["runUploadReceiptVerified"] is True
    assert binding["runUploadReceiptPath"] == str(run_path.resolve())
    assert binding["runUploadReceiptSha256"] == run_sha256
    assert binding["targetOrigin"] == RUN_ORIGIN
    assert binding["runSessionId"] == RUN_SESSION_ID
    assert receipt["externalArtifactPublishVerified"] is False
    assert receipt["status"] == "blocked"


def test_v2_two_consecutive_runs_keep_unique_receipts_and_replace_current(
    tmp_path: Path,
) -> None:
    scope_path = tmp_path / "scope.json"
    scope_path.write_text(
        json.dumps(finalized_scope(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    current = tmp_path / "PUBLICATION_SCOPE.current.json"
    outputs = [tmp_path / f"receipts/run-{index}.json" for index in (1, 2)]
    for index, output in enumerate(outputs, start=1):
        journal = create_activation_journal(
            tmp_path / f"activation-{index}",
            publication_receipt=output,
            current_receipt=current,
            publication_scope_sha256=hashlib.sha256(scope_path.read_bytes()).hexdigest(),
        )
        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--output",
                str(output),
                "--deploy-dir",
                str(tmp_path / "downloads"),
                "--release-version",
                VERSION,
                "--release-channel",
                "preview",
                "--promoted-artifact-count",
                "2",
                "--windows-publication-scope",
                str(scope_path),
                "--windows-activation-journal",
                str(journal),
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        assert result.returncode == 0, result.stderr
        assert output.is_file()
        commit = output.parent / f"{output.name}.transaction.committed.json"
        TRANSACTION.commit_transaction(
            argparse.Namespace(journal=journal, commit=commit)
        )
        TRANSACTION.install_current_receipt(
            argparse.Namespace(journal=journal, commit=commit)
        )
    pointer = json.loads(current.read_text(encoding="utf-8"))
    assert pointer["status"] == "committed"
    assert pointer["publicationReceipt"]["path"] == str(outputs[-1])
    assert pointer["publicationReceipt"]["sha256"] == hashlib.sha256(
        outputs[-1].read_bytes()
    ).hexdigest()
    assert pointer["commitRecord"]["path"].endswith(
        "run-2.json.transaction.committed.json"
    )
    assert current.stat().st_mode & 0o777 == 0o600
    assert outputs[0].is_file() and outputs[1].is_file()


def test_v2_cli_refuses_current_pointer_before_transaction_commit(
    tmp_path: Path,
) -> None:
    scope_path = tmp_path / "scope.json"
    scope_path.write_text(
        json.dumps(finalized_scope(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    output = tmp_path / "receipts/run.json"
    current = tmp_path / "PUBLICATION_SCOPE.current.json"
    journal = create_activation_journal(
        tmp_path / "activation",
        publication_receipt=output,
        current_receipt=current,
        publication_scope_sha256=hashlib.sha256(scope_path.read_bytes()).hexdigest(),
    )
    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--output",
            str(output),
            "--current-output",
            str(current),
            "--deploy-dir",
            str(tmp_path / "downloads"),
            "--release-version",
            VERSION,
            "--release-channel",
            "preview",
            "--promoted-artifact-count",
            "2",
            "--windows-publication-scope",
            str(scope_path),
            "--windows-activation-journal",
            str(journal),
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode != 0
    assert "only be installed from an exact transaction commit record" in result.stderr
    assert not output.exists()
    assert not current.exists()


def test_v2_cli_rejects_run_receipt_without_current_user_0600_hygiene(
    tmp_path: Path,
) -> None:
    scope_path = tmp_path / "scope.json"
    scope_path.write_text(
        json.dumps(finalized_scope(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    run_path = tmp_path / "release-upload-handoff.json"
    run_bytes = (
        json.dumps(completed_run_receipt(), indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    run_path.write_bytes(run_bytes)
    run_path.chmod(0o644)
    output = tmp_path / "status.json"
    journal = create_activation_journal(
        tmp_path / "activation",
        publication_receipt=output,
        current_receipt=tmp_path / "status.current.json",
        publication_scope_sha256=hashlib.sha256(scope_path.read_bytes()).hexdigest(),
    )

    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--output",
            str(output),
            "--deploy-dir",
            str(tmp_path / "downloads"),
            "--release-version",
            VERSION,
            "--release-channel",
            "preview",
            "--promoted-artifact-count",
            "4",
            "--windows-publication-scope",
            str(scope_path),
            "--windows-activation-journal",
            str(journal),
            "--run-upload-receipt",
            str(run_path),
            "--expected-run-upload-receipt-sha256",
            hashlib.sha256(run_bytes).hexdigest(),
            "--expected-run-api-origin",
            RUN_ORIGIN,
            "--expected-run-session-id",
            RUN_SESSION_ID,
            "--frozen-canonical-manifest-sha256",
            CANONICAL_MANIFEST_SHA256,
            "--frozen-inventory-sha256",
            INVENTORY_SHA256,
            "--frozen-file-count",
            str(FILE_COUNT),
            "--frozen-total-bytes",
            str(TOTAL_BYTES),
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "owned by the current user with mode 0600" in result.stderr
    assert not output.exists()


def test_legacy_v1_deploy_and_live_url_behavior_is_preserved() -> None:
    receipt = MODULE.build_receipt(
        deploy_dir="/srv/downloads",
        release_version="stable-1",
        release_channel="public_stable",
        promoted_artifact_count=3,
        deploy_mode=True,
        live_verify_target="https://chummer.run/downloads/releases.json",
        require_external_publish=True,
    )

    assert receipt["schema"] == "chummer.downloads.publication_scope.v1"
    assert receipt["status"] == "passed"
    assert receipt["externalArtifactPublishVerified"] is True


def test_nightly_wrapper_defaults_public_edge_redeploy_off() -> None:
    wrapper = (ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(
        encoding="utf-8"
    )

    assert (
        'REDEPLOY_PUBLIC_EDGE="${CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH:-false}"'
        in wrapper
    )


def test_windows_only_publisher_uses_unique_commit_abort_and_recovery_receipts() -> None:
    publisher = (ROOT / "scripts" / "publish-download-bundle.sh").read_text(
        encoding="utf-8"
    )

    for marker in (
        "prepare-transaction",
        "recover-prepared",
        "recover-activation",
        ".committed.json",
        ".aborted.json",
        '--abort-output "$publication_abort_output"',
        "journal-activate",
        "journal-commit",
        "install-current",
        "transaction-status",
        "CHUMMER_WINDOWS_ONLY_INJECT_EXIT_BEFORE_COMMIT_MARKER",
        "CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_COMMIT_MARKER",
        "CHUMMER_WINDOWS_ONLY_INJECT_EXIT_AFTER_ACTIVATION_CHILD_COUNT",
        "CHUMMER_WINDOWS_ONLY_INJECT_EXIT_BEFORE_ACTIVATION_JOURNAL",
        "trap '' INT TERM HUP",
    ):
        assert marker in publisher


def test_registry_authority_gate_precedes_all_windows_publication_mutation() -> None:
    publisher = (ROOT / "scripts" / "publish-download-bundle.sh").read_text(
        encoding="utf-8"
    )
    gate = publisher.index("\nrequire_windows_only_registry_finalize_authority\n")

    for mutation in (
        "\nsnapshot_windows_only_publication_source\n",
        'sync_source_dir="$(mktemp -d)"',
        "\n  initialize_windows_only_publication_transaction\n",
        "\n  prepare_windows_only_publication_targets\n",
        "\n  activate_windows_only_publication_targets\n",
    ):
        assert gate < publisher.index(mutation)

    prepared = publisher.index(
        "\n  prepare_windows_only_publication_transaction_record\n"
    )
    replay = publisher.index("\n  replay_windows_only_registry_prepare\n")
    activation = publisher.index("\n  activate_windows_only_publication_targets\n")
    assert prepared < replay < activation


def test_registry_authority_gates_accept_only_the_merged_prepare_finalize_shape() -> None:
    for script_name in (
        "publish-download-bundle.sh",
        "publish-latest-nightly-to-downloads.sh",
    ):
        publisher = (ROOT / "scripts" / script_name).read_text(encoding="utf-8")

        assert 'prepare.get("finalizeAvailable") is not True' in publisher
        assert '"finalizeReceipt" not in prepare' in publisher
        assert 'prepare.get("finalizeReceipt") is not None' in publisher
        assert "isinstance(finalize_receipt, dict)" not in publisher


def test_nightly_wrapper_registry_gate_precedes_publish_dispatch() -> None:
    wrapper = (ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(
        encoding="utf-8"
    )
    gate = wrapper.index('\nrequire_registry_finalize_authority "$latest_stage"\n')

    assert gate < wrapper.index(
        '\nverify_public_nightly_installer_eligibility "$latest_stage"\n'
    )
    assert gate < wrapper.index(
        '\nbash "$SCRIPT_DIR/publish-download-bundle.sh" '
        '"$latest_stage/publication" "$DEPLOY_DIR"\n'
    )
