from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import time
import zipfile
from datetime import UTC, datetime, timedelta
from pathlib import Path
from types import ModuleType, SimpleNamespace

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "release" / "run_preview_nightly_pipeline.py"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("preview_pipeline", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pipeline = load_module()


def iso(value: datetime) -> str:
    return value.replace(microsecond=0).isoformat().replace("+00:00", "Z")


def valid_run(*, run_id: str = "123", workflow: str | None = None, sha: str = "a" * 40) -> dict:
    return {
        "actor": {"login": "github-actions[bot]"},
        "conclusion": "success",
        "event": "workflow_dispatch",
        "head_branch": "main",
        "head_sha": sha,
        "id": int(run_id),
        "path": workflow or pipeline.CAPTURE_WORKFLOW,
        "repository": {"full_name": pipeline.REPOSITORY},
        "run_attempt": 1,
        "status": "completed",
        "workflow_id": 77,
    }


def valid_artifact(*, name: str = "artifact", artifact_id: str = "55", content: bytes = b"zip") -> dict:
    current = datetime.now(UTC)
    return {
        "created_at": iso(current - timedelta(minutes=5)),
        "digest": f"sha256:{pipeline.sha256_bytes(content)}",
        "expired": False,
        "expires_at": iso(current + timedelta(days=10)),
        "id": int(artifact_id),
        "name": name,
        "size_in_bytes": len(content),
    }


@pytest.mark.parametrize(
    "mutation,expected",
    [
        ({"head_sha": "b" * 40}, "ref/SHA differs"),
        ({"path": ".github/workflows/other.yml"}, "path differs"),
        ({"event": "push"}, "not a workflow_dispatch"),
        ({"repository": {}}, "repository differs"),
        ({"workflow_id": None}, "workflow ID must be an exact positive-integer string"),
    ],
)
def test_wrong_run_ref_or_workflow_is_rejected(mutation: dict, expected: str) -> None:
    run = valid_run()
    run.update(mutation)
    with pytest.raises(pipeline.PipelineError, match=expected):
        pipeline.validate_run(
            run,
            run_id="123",
            workflow=pipeline.CAPTURE_WORKFLOW,
            sha="a" * 40,
            require_success=True,
        )


def test_authenticated_workflow_id_is_bound_to_exact_active_workflow_path() -> None:
    run = valid_run()
    paths: list[str] = []
    client = pipeline.GitHubClient.__new__(pipeline.GitHubClient)
    client._validated_workflows = set()

    def json_response(path: str, *_args: object, **_kwargs: object) -> dict:
        paths.append(path)
        if path.endswith("/actions/runs/123"):
            return run
        return {"id": 77, "path": pipeline.CAPTURE_WORKFLOW, "state": "active"}

    client.json = json_response
    assert client.run("123", pipeline.CAPTURE_WORKFLOW, "a" * 40, True) == run
    assert paths == [
        f"repos/{pipeline.REPOSITORY}/actions/runs/123",
        f"repos/{pipeline.REPOSITORY}/actions/workflows/77",
    ]

    with pytest.raises(pipeline.PipelineError, match="metadata path differs"):
        pipeline.validate_workflow_metadata(
            {"id": 77, "path": pipeline.FINALIZATION_WORKFLOW, "state": "active"},
            workflow_id="77",
            workflow=pipeline.CAPTURE_WORKFLOW,
        )


def test_expired_artifact_is_rejected() -> None:
    artifact = valid_artifact()
    artifact["expired"] = True
    with pytest.raises(pipeline.PipelineError, match="expired"):
        pipeline.validate_artifact(artifact, expected_name="artifact")


def test_tampered_download_cannot_become_preserved_provenance(tmp_path: Path) -> None:
    expected = valid_artifact(content=b"original")

    class TamperingClient:
        def download(self, _artifact_id: str, output: Path, _expected: dict) -> None:
            output.write_bytes(b"tampered")

    with pytest.raises(pipeline.PipelineError, match="digest changed"):
        pipeline.copy_original_artifact(TamperingClient(), expected, tmp_path / "artifact.zip")


def test_resume_state_tamper_is_rejected(tmp_path: Path) -> None:
    state_path = tmp_path / "state.json"
    payload = {
        "contractName": pipeline.STATE_CONTRACT,
        "contractVersion": 1,
        "createdAt": pipeline.now_iso(),
        "phase": "awaiting_capture",
        "repository": pipeline.REPOSITORY,
        "sourceRef": pipeline.SOURCE_REF,
        "sourceSha": "a" * 40,
    }
    pipeline.write_state(state_path, payload)
    tampered = json.loads(state_path.read_text(encoding="utf-8"))
    tampered["phase"] = "evidence_preserved"
    state_path.write_text(json.dumps(tampered), encoding="utf-8")
    with pytest.raises(pipeline.PipelineError, match="modified or forged"):
        pipeline.load_state(state_path)


def review_request() -> dict:
    return {
        "capture": {
            "actor": "github-actions[bot]",
            "artifactId": "55",
            "artifactName": "windows-native-evidence-123-1",
            "artifactSha256": "a" * 64,
            "inventorySha256": "b" * 64,
            "ref": pipeline.SOURCE_REF,
            "runAttempt": "1",
            "runId": "123",
            "sha": "c" * 40,
            "workflow": pipeline.CAPTURE_WORKFLOW,
            "workflowId": "77",
        },
        "contractName": pipeline.REVIEW_REQUEST_CONTRACT,
        "contractVersion": 1,
        "generatedAt": pipeline.now_iso(),
        "humanReviewConfirmed": False,
        "requiredChecks": ["readability", "contrast", "clipping"],
        "requiredHeads": list(pipeline.PROMOTED_WINDOWS_HEADS),
        "screenshots": [],
        "status": "action_required",
        "warning": "review",
    }


def write_review_input(path: Path, request: dict, request_sha: str, *, reviewer: str = "alice") -> None:
    path.write_text(
        json.dumps(
            {
                "capture": request["capture"],
                "contractName": pipeline.REVIEW_INPUT_CONTRACT,
                "contractVersion": 1,
                "heads": {
                    "avalonia": {"readability": True, "contrast": True, "clipping": True},
                },
                "humanReviewConfirmed": True,
                "reviewRequestSha256": request_sha,
                "reviewer": reviewer,
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )


def test_forged_human_confirmation_wrong_request_digest_is_rejected(tmp_path: Path) -> None:
    request = review_request()
    review_path = tmp_path / "review.json"
    write_review_input(review_path, request, "d" * 64)
    with pytest.raises(pipeline.PipelineError, match="different request"):
        pipeline.validate_review_input(
            review_path,
            request=request,
            request_sha="e" * 64,
            authenticated_login="alice",
        )


def test_forged_human_confirmation_wrong_actor_is_rejected(tmp_path: Path) -> None:
    request = review_request()
    review_path = tmp_path / "review.json"
    write_review_input(review_path, request, "e" * 64, reviewer="mallory")
    with pytest.raises(pipeline.PipelineError, match="authenticated dispatch actor"):
        pipeline.validate_review_input(
            review_path,
            request=request,
            request_sha="e" * 64,
            authenticated_login="alice",
        )


def test_human_review_and_finalization_dispatch_bind_only_promoted_head(
    tmp_path: Path,
) -> None:
    request = review_request()
    request_sha = "e" * 64
    review_path = tmp_path / "review.json"
    write_review_input(review_path, request, request_sha)
    review = pipeline.validate_review_input(
        review_path,
        request=request,
        request_sha=request_sha,
        authenticated_login="alice",
    )
    assert set(review["heads"]) == set(pipeline.PROMOTED_WINDOWS_HEADS)

    calls: list[dict[str, str]] = []

    class Client:
        def json(
            self,
            _path: str,
            method: str = "GET",
            fields: dict[str, str] | None = None,
        ) -> dict:
            assert method == "POST"
            calls.append(dict(fields or {}))
            return {
                "workflow_run_id": 999,
                "run_url": f"https://api.github.com/repos/{pipeline.REPOSITORY}/actions/runs/999",
                "html_url": f"https://github.com/{pipeline.REPOSITORY}/actions/runs/999",
            }

    assert pipeline.dispatch_finalization(Client(), review) == "999"
    assert "inputs[avalonia_review_json]" in calls[0]
    assert "inputs[blazor_review_json]" not in calls[0]

    widened = json.loads(review_path.read_text(encoding="utf-8"))
    widened["heads"]["blazor-desktop"] = {
        "readability": True,
        "contrast": True,
        "clipping": True,
    }
    review_path.write_text(json.dumps(widened), encoding="utf-8")
    with pytest.raises(pipeline.PipelineError, match="exact promoted heads"):
        pipeline.validate_review_input(
            review_path,
            request=request,
            request_sha=request_sha,
            authenticated_login="alice",
        )


def test_pipeline_stops_action_required_without_review_input(tmp_path: Path) -> None:
    request_path = tmp_path / "request.json"
    request_path.write_text(json.dumps(review_request()), encoding="utf-8")
    state = {
        "capture": {"reviewRequestSha256": pipeline.sha256_file(request_path)},
        "phase": "action_required_human_review",
    }
    args = SimpleNamespace(review_input=None, review_request_output=request_path)
    with pytest.raises(pipeline.ActionRequired, match="review exact capture artifact"):
        pipeline.request_finalization(args, object(), state)


def test_artifact_api_digest_and_original_archive_digest_must_match(tmp_path: Path) -> None:
    content = b"exact-original-zip"
    artifact = valid_artifact(content=content)

    class ExactClient:
        def download(self, _artifact_id: str, output: Path, _expected: dict) -> None:
            output.write_bytes(content)

    receipt = pipeline.copy_original_artifact(ExactClient(), artifact, tmp_path / "artifact.zip")
    assert receipt["archiveSha256"] == pipeline.sha256_bytes(content)
    assert receipt["onlineAvailabilityClaim"] == "unexpired_at_acquisition_only"


def candidate_state() -> dict:
    return {
        "actor": "release-operator",
        "artifactId": "55",
        "artifactName": "preview-nightly-candidate-123-1",
        "artifactSha256": "a" * 64,
        "contentInventorySha256": "b" * 64,
        "manifestSha256": "c" * 64,
        "runAttempt": "1",
        "runId": "123",
    }


def write_capture_dispatch(path: Path, candidate: dict, *, capture_run_id: str = "789") -> None:
    handoff = {
        "actor": candidate["actor"],
        "artifactId": candidate["artifactId"],
        "artifactName": candidate["artifactName"],
        "artifactSha256": candidate["artifactSha256"],
        "contentInventorySha256": candidate["contentInventorySha256"],
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 1,
        "ref": pipeline.SOURCE_REF,
        "repository": pipeline.REPOSITORY,
        "runAttempt": candidate["runAttempt"],
        "runId": candidate["runId"],
        "sha": "d" * 40,
        "workflow": pipeline.CANDIDATE_WORKFLOW,
    }
    payload = {
        "candidateHandoff": handoff,
        "candidateHandoffSha256": pipeline.sha256_bytes(pipeline.canonical_bytes(handoff)),
        "capture": {
            "htmlUrl": f"https://github.com/{pipeline.REPOSITORY}/actions/runs/{capture_run_id}",
            "ref": pipeline.SOURCE_REF,
            "repository": pipeline.REPOSITORY,
            "runId": capture_run_id,
            "runUrl": f"https://api.github.com/repos/{pipeline.REPOSITORY}/actions/runs/{capture_run_id}",
            "workflow": pipeline.CAPTURE_WORKFLOW,
        },
        "contractName": "chummer6-ui.preview-nightly-capture-dispatch",
        "contractVersion": 1,
        "status": "dispatched",
    }
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(pipeline.CAPTURE_DISPATCH_RECEIPT, json.dumps(payload, sort_keys=True))


def test_capture_dispatch_is_bound_to_exact_candidate_artifact_and_inventory(tmp_path: Path) -> None:
    candidate = candidate_state()
    archive = tmp_path / "dispatch.zip"
    write_capture_dispatch(archive, candidate)
    capture = pipeline.validate_capture_dispatch(archive, candidate=candidate, source_sha="d" * 40)
    assert capture["runId"] == "789"

    substituted = dict(candidate)
    substituted["artifactId"] = "56"
    with pytest.raises(pipeline.PipelineError, match="run/artifact/inventory correlation differs"):
        pipeline.validate_capture_dispatch(archive, candidate=substituted, source_sha="d" * 40)


def test_capture_polling_uses_dispatch_returned_run_id_only() -> None:
    calls: list[str] = []

    class Client:
        def run(self, run_id: str, workflow: str, sha: str, require_success: bool) -> dict:
            calls.append(run_id)
            return valid_run(run_id=run_id, workflow=workflow, sha=sha)

    result = pipeline.wait_for_capture(
        Client(), run_id="789", sha="a" * 40, deadline=10**12
    )
    assert str(result["id"]) == "789"
    assert calls == ["789"]


def test_seal_must_match_release_manifest_candidate_and_sources(tmp_path: Path) -> None:
    candidate = candidate_state()
    capture_source = {
        "repository": pipeline.REPOSITORY,
        "workflow": pipeline.CAPTURE_WORKFLOW,
        "runId": "789",
        "runAttempt": "1",
        "ref": pipeline.SOURCE_REF,
        "sha": "d" * 40,
        "actor": "capture-user",
        "artifactName": "windows-native-evidence-789-1",
    }
    finalization_source = {
        "repository": pipeline.REPOSITORY,
        "workflow": pipeline.FINALIZATION_WORKFLOW,
        "runId": "999",
        "runAttempt": "1",
        "ref": pipeline.SOURCE_REF,
        "sha": "d" * 40,
        "actor": "alice",
        "artifactName": "windows-native-evidence-finalized-999-1",
    }
    state = {
        "candidate": candidate,
        "capture": {
            "actor": "capture-user",
            "artifactName": "windows-native-evidence-789-1",
            "inventorySha256": "4" * 64,
            "runAttempt": "1",
            "runId": "789",
        },
        "finalization": {
            "archiveSha256": "5" * 64,
            "artifactName": "windows-native-evidence-finalized-999-1",
            "finalizationReceiptSha256": "6" * 64,
            "reviewer": "alice",
            "runAttempt": "1",
            "runId": "999",
        },
        "release": {"channel": "preview", "publishedAt": "2026-07-19T12:00:00Z", "version": "run-1"},
        "sourceAuthorities": [{"name": "presentation", "commit": "d" * 40}],
        "sourceSha": "d" * 40,
    }
    sealed_candidate = {
        **candidate,
        "repository": pipeline.REPOSITORY,
        "workflow": pipeline.CANDIDATE_WORKFLOW,
        "ref": pipeline.SOURCE_REF,
        "sha": "d" * 40,
    }
    native = {
        "archiveSha256": "5" * 64,
        "captureInventorySha256": "4" * 64,
        "captureSource": capture_source,
        "contractName": pipeline.NATIVE_EVIDENCE_CONTRACT,
        "contractVersion": 1,
        "finalizationSha256": "6" * 64,
        "finalizationSource": finalization_source,
        "status": "passed",
        "treeSha256": "7" * 64,
        "visualReviewers": {"avalonia": "alice"},
    }
    native_path = tmp_path / pipeline.NATIVE_EVIDENCE_RECEIPT
    native_path.write_text(json.dumps(native, sort_keys=True), encoding="utf-8")
    seal = {
        "contractName": "chummer6-ui.preview-nightly-stage",
        "contractVersion": 1,
        "status": "sealed",
        "release": state["release"],
        "sourceAuthorities": state["sourceAuthorities"],
        "proof": {
            "canonicalManifestSha256": candidate["manifestSha256"],
            "candidateProducerProvenance": {"candidate": sealed_candidate},
            "nativeWindowsEvidenceTreeSha256": "7" * 64,
        },
        "stage": {
            "files": [
                {
                    "path": pipeline.NATIVE_EVIDENCE_RECEIPT,
                    "sha256": pipeline.sha256_file(native_path),
                    "sizeBytes": native_path.stat().st_size,
                }
            ]
        },
        "uploadBoundary": {
            "uploadAuthorized": False,
            "postUploadHandoffEmitted": False,
            "producerMode": "stage_only",
        },
    }
    path = tmp_path / pipeline.STAGE_SEAL
    path.write_text(json.dumps(seal), encoding="utf-8")
    assert pipeline.validate_seal_against_state(path, state)["status"] == "sealed"
    seal["proof"]["canonicalManifestSha256"] = "f" * 64
    path.write_text(json.dumps(seal), encoding="utf-8")
    with pytest.raises(pipeline.PipelineError, match="manifest differs"):
        pipeline.validate_seal_against_state(path, state)


def test_seal_rejects_substituted_valid_finalization(tmp_path: Path) -> None:
    candidate = candidate_state()
    state = {
        "candidate": candidate,
        "capture": {
            "actor": "capture-user",
            "artifactName": "windows-native-evidence-789-1",
            "inventorySha256": "4" * 64,
            "runAttempt": "1",
            "runId": "789",
        },
        "finalization": {
            "archiveSha256": "5" * 64,
            "artifactName": "windows-native-evidence-finalized-999-1",
            "finalizationReceiptSha256": "6" * 64,
            "reviewer": "alice",
            "runAttempt": "1",
            "runId": "999",
        },
        "release": {"channel": "preview", "publishedAt": "2026-07-19T12:00:00Z", "version": "run-1"},
        "sourceAuthorities": [{"name": "presentation", "commit": "d" * 40}],
        "sourceSha": "d" * 40,
    }
    native = {
        "archiveSha256": "8" * 64,
        "captureInventorySha256": "4" * 64,
        "captureSource": {
            "repository": pipeline.REPOSITORY,
            "workflow": pipeline.CAPTURE_WORKFLOW,
            "runId": "789",
            "runAttempt": "1",
            "ref": pipeline.SOURCE_REF,
            "sha": "d" * 40,
            "actor": "capture-user",
            "artifactName": "windows-native-evidence-789-1",
        },
        "contractName": pipeline.NATIVE_EVIDENCE_CONTRACT,
        "contractVersion": 1,
        "finalizationSha256": "9" * 64,
        "finalizationSource": {
            "repository": pipeline.REPOSITORY,
            "workflow": pipeline.FINALIZATION_WORKFLOW,
            "runId": "1000",
            "runAttempt": "1",
            "ref": pipeline.SOURCE_REF,
            "sha": "d" * 40,
            "actor": "mallory",
            "artifactName": "windows-native-evidence-finalized-1000-1",
        },
        "status": "passed",
        "treeSha256": "7" * 64,
        "visualReviewers": {"avalonia": "mallory"},
    }
    native_path = tmp_path / pipeline.NATIVE_EVIDENCE_RECEIPT
    native_path.write_text(json.dumps(native, sort_keys=True), encoding="utf-8")
    sealed_candidate = {
        **candidate,
        "repository": pipeline.REPOSITORY,
        "workflow": pipeline.CANDIDATE_WORKFLOW,
        "ref": pipeline.SOURCE_REF,
        "sha": "d" * 40,
    }
    seal = {
        "contractName": "chummer6-ui.preview-nightly-stage",
        "contractVersion": 1,
        "status": "sealed",
        "release": state["release"],
        "sourceAuthorities": state["sourceAuthorities"],
        "proof": {
            "canonicalManifestSha256": candidate["manifestSha256"],
            "candidateProducerProvenance": {"candidate": sealed_candidate},
            "nativeWindowsEvidenceTreeSha256": "7" * 64,
        },
        "stage": {
            "files": [
                {
                    "path": pipeline.NATIVE_EVIDENCE_RECEIPT,
                    "sha256": pipeline.sha256_file(native_path),
                    "sizeBytes": native_path.stat().st_size,
                }
            ]
        },
        "uploadBoundary": {
            "uploadAuthorized": False,
            "postUploadHandoffEmitted": False,
            "producerMode": "stage_only",
        },
    }
    seal_path = tmp_path / pipeline.STAGE_SEAL
    seal_path.write_text(json.dumps(seal), encoding="utf-8")
    with pytest.raises(pipeline.PipelineError, match="finalization run differs"):
        pipeline.validate_seal_against_state(seal_path, state)


def stage_environment_fixture(tmp_path: Path) -> tuple[SimpleNamespace, Path, str]:
    environment = {key: str(tmp_path / key) for key in pipeline.STAGE_AUTHORITY_PATHS}
    environment.update({key: "e" * 64 for key in pipeline.STAGE_AUTHORITY_DIGESTS})
    for _, _, key in pipeline.SOURCE_AUTHORITY_ENVIRONMENTS:
        environment[key] = "d" * 40
    payload = {
        "contractName": "chummer6-ui.preview-nightly-stage-authority-input",
        "contractVersion": 1,
        "environment": environment,
    }
    authority_path = tmp_path / "authority.json"
    authority_path.write_text(json.dumps(payload), encoding="utf-8")
    args = SimpleNamespace(
        evidence_directory=tmp_path / "evidence",
        prepared_stage_root=tmp_path / "candidate",
        published_at="2026-07-19T12:00:00Z",
        release_version="run-1",
        stage_authority_input=authority_path,
        stage_dir=tmp_path / "stage",
    )
    return args, authority_path, pipeline.sha256_file(authority_path)


@pytest.fixture(autouse=True)
def approved_signing_tool_fixture_hashes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path_factory: pytest.TempPathFactory,
) -> None:
    actual_sha256_file = pipeline.sha256_file
    approved_java_sha256 = pipeline.PREPARE_SIGNING_APPROVED_JAVA_SHA256
    approved_jsign_sha256 = pipeline.PREPARE_SIGNING_APPROVED_JSIGN_SHA256
    tool_root = tmp_path_factory.mktemp("governed-signing-tools")
    java_parent = tool_root / "governed-java"
    java_root_name = "temurin-test"
    java_home = java_parent / java_root_name
    java = java_home / "bin" / "java"
    java.parent.mkdir(parents=True)
    java.write_text("#!/usr/bin/env sh\nexit 0\n", encoding="utf-8")
    java.chmod(0o700)
    jsign = tool_root / "governed-jsign" / "jsign.jar"
    jsign.parent.mkdir()
    jsign.write_bytes(b"fixed jsign fixture")

    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_JAVA_PARENT", java_parent)
    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_JAVA_ROOT_NAME", java_root_name)
    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_JAVA_HOME", java_home)
    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_JAVA_BIN", java)
    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_JSIGN_JAR", jsign)
    monkeypatch.setattr(
        pipeline,
        "PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256",
        pipeline._sha256_canonical_tree(java_parent, java_root_name),
    )

    def fixture_sha256_file(path: Path) -> str:
        candidate = Path(path)
        if (
            candidate.name == "java"
            and candidate.read_bytes() == b"#!/usr/bin/env sh\nexit 0\n"
        ):
            return approved_java_sha256
        if (
            candidate.name == "jsign.jar"
            and candidate.read_bytes() == b"fixed jsign fixture"
        ):
            return approved_jsign_sha256
        return actual_sha256_file(candidate)

    monkeypatch.setattr(pipeline, "sha256_file", fixture_sha256_file)


def production_toolchain_environment() -> dict[str, str]:
    java_home = (
        "/home/tibor/.local/share/ea-tools/chummer-signing/java/"
        "temurin-21.0.11+10"
    )
    return {
        "CHUMMER_KEYLOCKER_DOTNET_ROOT": "/usr/lib/dotnet",
        "CHUMMER_KEYLOCKER_DOTNET_BIN": "/usr/lib/dotnet/dotnet",
        "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256": (
            "a2e03e682b5ba32303077bc5ed95ca3dd6b57b6d55d09491b67444644e211940"
        ),
        "CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256": (
            "ba27f662b28bfe7b938b8c862c41e07739db8182a42481a6a0cc5b385ec5f2be"
        ),
        "CHUMMER_KEYLOCKER_JAVA_HOME": java_home,
        "CHUMMER_KEYLOCKER_JAVA_BIN": f"{java_home}/bin/java",
        "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256": (
            "fd85538801d8ca61d3558c87a57a600e1868d8ac9e918d0860dd64281b548643"
        ),
        "CHUMMER_KEYLOCKER_JAVA_TREE_SHA256": (
            "3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5"
        ),
        "CHUMMER_KEYLOCKER_JSIGN_JAR": (
            "/home/tibor/.local/share/ea-tools/chummer-signing/jsign/7.5/"
            "jsign-7.5.jar"
        ),
        "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256": (
            "602a51c3545a6dc4fb99bd2ea7152b26d1345916d0c93ddfbd5936cb735af91c"
        ),
        "CHUMMER_KEYLOCKER_SIGNER_DLL": (
            "/tmp/chummer-keylocker-signer-fixture/published/"
            "Chummer.KeyLockerSigner.dll"
        ),
        "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256": "c" * 64,
        "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256": "d" * 64,
        "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256": "e" * 64,
        "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256": "f" * 64,
        "CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256": (
            "878939d8aec1375674ef0508026fc15101ac15f31807d97651c6f38b99feb5dd"
        ),
    }


def require_provisioned_toolchain_environment() -> dict[str, str]:
    toolchain = production_toolchain_environment()
    required_files = (
        Path(toolchain["CHUMMER_KEYLOCKER_JAVA_BIN"]),
        Path(toolchain["CHUMMER_KEYLOCKER_JSIGN_JAR"]),
        Path(toolchain["CHUMMER_KEYLOCKER_DOTNET_BIN"]),
    )
    provisioned = tuple(path.is_file() for path in required_files)
    if not any(provisioned):
        pytest.skip(
            "exact flagship signing toolchain is not provisioned on this host"
        )
    assert all(provisioned), (
        "flagship signing toolchain is only partially provisioned: "
        + ", ".join(
            str(path)
            for path, present in zip(required_files, provisioned, strict=True)
            if not present
        )
    )
    return toolchain


def test_unprovisioned_signing_host_skips_physical_identity_checks(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(Path, "is_file", lambda _path: False)
    with pytest.raises(pytest.skip.Exception):
        require_provisioned_toolchain_environment()


def test_partially_provisioned_signing_host_fails_closed(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    toolchain = production_toolchain_environment()
    java_path = Path(toolchain["CHUMMER_KEYLOCKER_JAVA_BIN"])
    monkeypatch.setattr(Path, "is_file", lambda path: path == java_path)
    with pytest.raises(AssertionError, match="only partially provisioned"):
        require_provisioned_toolchain_environment()


def signing_environment_fixture(
    tmp_path: Path,
) -> tuple[dict[str, str], dict[str, str]]:
    public_certificate = tmp_path / "public.pem"
    public_certificate.write_text("public certificate fixture\n", encoding="utf-8")
    client_certificate = tmp_path / "client-auth.p12"
    client_certificate.write_bytes(b"private client authentication fixture")
    client_certificate.chmod(0o600)
    public = {
        "CHUMMER_WINDOWS_SIGNING_BACKEND": pipeline.PREPARE_SIGNING_BACKEND,
        "CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS": "keylocker-alias",
        "CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH": str(public_certificate),
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256": "a" * 64,
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256": "b" * 64,
        "CHUMMER_WINDOWS_TIMESTAMP_URL": "http://timestamp.digicert.com",
    }
    secret = {
        "SM_HOST": "https://clientauth.one.digicert.com",
        "SM_API_KEY": "synthetic-api-key",
        "SM_CLIENT_CERT_FILE": str(client_certificate),
        "SM_CLIENT_CERT_PASSWORD": "synthetic-client-password",
    }
    return public, secret


def test_provisioned_toolchain_matches_all_compiled_release_anchors() -> None:
    toolchain = require_provisioned_toolchain_environment()
    java_home = Path(toolchain["CHUMMER_KEYLOCKER_JAVA_HOME"])
    java_bin = Path(toolchain["CHUMMER_KEYLOCKER_JAVA_BIN"])
    jsign = Path(toolchain["CHUMMER_KEYLOCKER_JSIGN_JAR"])

    assert java_home.parent == Path(
        "/home/tibor/.local/share/ea-tools/chummer-signing/java"
    )
    assert java_home.name == "temurin-21.0.11+10"
    assert pipeline.sha256_file(java_bin) == toolchain[
        "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256"
    ]
    assert pipeline._sha256_canonical_tree(
        java_home.parent,
        java_home.name,
    ) == toolchain["CHUMMER_KEYLOCKER_JAVA_TREE_SHA256"]
    assert pipeline.sha256_file(jsign) == toolchain[
        "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256"
    ]
    dotnet_root = Path(toolchain["CHUMMER_KEYLOCKER_DOTNET_ROOT"])
    dotnet_bin = Path(toolchain["CHUMMER_KEYLOCKER_DOTNET_BIN"])
    assert dotnet_root == Path("/usr/lib/dotnet")
    assert pipeline.sha256_file(dotnet_bin) == toolchain[
        "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256"
    ]
    assert pipeline._sha256_canonical_tree(
        dotnet_root.parent,
        dotnet_root.name,
    ) == toolchain["CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256"]


def test_prepared_signer_output_is_sealed_and_bound_as_one_private_tree(
    tmp_path: Path,
) -> None:
    runtime_root = tmp_path / "chummer-keylocker-signer-fixture"
    output_root = runtime_root / pipeline.PREPARE_SIGNING_OUTPUT_ROOT_NAME
    output_root.mkdir(parents=True, mode=0o700)
    required = {
        pipeline.PREPARE_SIGNING_DLL_NAME: b"fixture signer dll",
        pipeline.PREPARE_SIGNING_RUNTIME_CONFIG_NAME: b'{"runtimeOptions":{}}',
        pipeline.PREPARE_SIGNING_DEPS_NAME: b'{"runtimeTarget":{}}',
        pipeline.PREPARE_SIGNING_SDK_PIN_NAME: (
            pipeline.PREPARE_SIGNING_SDK_PIN_BYTES
        ),
        "System.Security.Cryptography.Pkcs.dll": b"fixture dependency",
    }
    for name, content in required.items():
        (output_root / name).write_bytes(content)

    identity = pipeline._seal_private_signer_output(output_root)

    assert set(identity) == {
        "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_DLL",
        "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256",
    }
    assert identity["CHUMMER_KEYLOCKER_SIGNER_DLL"] == str(
        output_root / pipeline.PREPARE_SIGNING_DLL_NAME
    )
    assert (output_root.stat().st_mode & 0o777) == 0o500
    assert all(
        (path.stat().st_mode & 0o777) == 0o400
        for path in output_root.iterdir()
    )
    assert identity["CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256"] == (
        pipeline._sha256_canonical_tree(output_root.parent, output_root.name)
    )
    pipeline._remove_private_signer_runtime(runtime_root)
    assert not runtime_root.exists()


def test_signer_is_locked_published_before_intake_with_fixed_host_and_clean_env(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    repo_root = tmp_path / "presentation"
    project = repo_root / pipeline.PREPARE_SIGNING_PROJECT_RELATIVE
    project.parent.mkdir(parents=True)
    project.write_text("<Project Sdk=\"Microsoft.NET.Sdk\" />\n", encoding="utf-8")
    project.with_name("packages.lock.json").write_text("{}\n", encoding="utf-8")
    (project.parent / pipeline.PREPARE_SIGNING_SDK_PIN_NAME).write_bytes(
        pipeline.PREPARE_SIGNING_SDK_PIN_BYTES
    )
    calls: list[tuple[list[str], Path, dict[str, str]]] = []

    def fake_run_checked(
        command: list[str],
        *,
        cwd: Path,
        environment: dict[str, str],
        timeout_seconds: float,
        **_kwargs: object,
    ) -> None:
        assert timeout_seconds == pipeline.SIGNER_BUILD_TIMEOUT_SECONDS
        calls.append((command, cwd, environment))
        if command[1] == "publish":
            output = Path(command[command.index("--output") + 1])
            for name in (
                pipeline.PREPARE_SIGNING_DLL_NAME,
                pipeline.PREPARE_SIGNING_RUNTIME_CONFIG_NAME,
                pipeline.PREPARE_SIGNING_DEPS_NAME,
            ):
                (output / name).write_bytes(name.encode("utf-8"))
            (output / pipeline.PREPARE_SIGNING_SDK_PIN_NAME).write_bytes(
                pipeline.PREPARE_SIGNING_SDK_PIN_BYTES
            )

    monkeypatch.setattr(pipeline, "_require_root_owned_dotnet_tree", lambda: None)
    monkeypatch.setattr(pipeline, "run_checked", fake_run_checked)
    monkeypatch.setattr(
        pipeline,
        "_run_clean_git",
        lambda _repo_root, arguments: arguments[-1] + "\n",
    )

    runtime_root, identity = pipeline.prepare_linux_signer_runtime(repo_root)
    try:
        assert [call[0][1] for call in calls] == ["restore", "publish"]
        restore, publish = calls
        assert restore[0] == [
            "/usr/lib/dotnet/dotnet",
            "restore",
            str(project),
            "--locked-mode",
            "--runtime",
            "linux-x64",
        ]
        assert publish[0] == [
            "/usr/lib/dotnet/dotnet",
            "publish",
            str(project),
            "--configuration",
            "Release",
            "--runtime",
            "linux-x64",
            "--self-contained",
            "false",
            "-p:UseAppHost=false",
            "--no-restore",
            "--output",
            str(runtime_root / pipeline.PREPARE_SIGNING_OUTPUT_ROOT_NAME),
        ]
        assert restore[1] == publish[1] == project.parent
        assert "--no-restore" not in restore[0]
        assert publish[0].count("--no-restore") == 1
        assert (
            restore[0][restore[0].index("--runtime") + 1]
            == publish[0][publish[0].index("--runtime") + 1]
            == "linux-x64"
        )
        for _command, _cwd, environment in calls:
            assert set(environment) == {
                "DOTNET_CLI_HOME",
                "DOTNET_CLI_TELEMETRY_OPTOUT",
                "DOTNET_MULTILEVEL_LOOKUP",
                "DOTNET_NOLOGO",
                "DOTNET_ROOT",
                "HOME",
                "NUGET_PACKAGES",
            }
            assert environment["DOTNET_ROOT"] == "/usr/lib/dotnet"
            assert environment["DOTNET_MULTILEVEL_LOOKUP"] == "0"
        assert set(identity) == {
            "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_DLL",
            "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256",
        }
    finally:
        pipeline._remove_private_signer_runtime(runtime_root)

    monkeypatch.setattr(
        pipeline,
        "PREPARE_SIGNING_RUNTIME_IDENTIFIER",
        "win-x64",
    )
    with pytest.raises(pipeline.PipelineError, match="not fixed"):
        pipeline.prepare_linux_signer_runtime(repo_root)


def test_signer_sdk_pin_is_exact_tracked_input_and_fails_missing_or_wrong(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    repo_root = tmp_path / "presentation"
    project = repo_root / pipeline.PREPARE_SIGNING_PROJECT_RELATIVE
    project.parent.mkdir(parents=True)
    project.write_text("<Project />\n", encoding="utf-8")
    relative = (
        project.parent / pipeline.PREPARE_SIGNING_SDK_PIN_NAME
    ).relative_to(repo_root).as_posix()
    monkeypatch.setattr(
        pipeline,
        "_run_clean_git",
        lambda _repo_root, _arguments: relative + "\n",
    )
    sdk_pin = project.parent / pipeline.PREPARE_SIGNING_SDK_PIN_NAME

    with pytest.raises(pipeline.PipelineError, match="unavailable"):
        pipeline._require_signer_sdk_pin(repo_root, project)

    sdk_pin.write_text(
        '{"sdk":{"version":"10.0.110","rollForward":"latestPatch"}}\n',
        encoding="utf-8",
    )
    with pytest.raises(pipeline.PipelineError, match="differs"):
        pipeline._require_signer_sdk_pin(repo_root, project)

    sdk_pin.write_bytes(pipeline.PREPARE_SIGNING_SDK_PIN_BYTES)
    assert pipeline.sha256_file(sdk_pin) == (
        pipeline.PREPARE_SIGNING_APPROVED_SDK_PIN_SHA256
    )
    assert pipeline._require_signer_sdk_pin(repo_root, project) == sdk_pin

    monkeypatch.setattr(
        pipeline,
        "_run_clean_git",
        lambda _repo_root, _arguments: "",
    )
    with pytest.raises(pipeline.PipelineError, match="not tracked"):
        pipeline._require_signer_sdk_pin(repo_root, project)


def test_prepare_captures_fixed_signer_handoff_without_secret_child_environment(
    tmp_path: Path,
) -> None:
    args, _authority_path, authority_sha = stage_environment_fixture(tmp_path)
    public, secret = signing_environment_fixture(tmp_path)
    parent = {
        "PATH": os.environ["PATH"],
        **public,
        **secret,
        "CHUMMER_AMBIENT_OVERRIDE": "unrelated-value",
        "GH_TOKEN": "github-auth-value",
    }

    material = pipeline.capture_prepare_signing_material(parent, consume=True)
    child, sources, actual_authority_sha = pipeline.prepare_stage_environment(
        args,
        material.public_environment,
        parent,
    )

    fixed_toolchain = pipeline.prepare_signing_toolchain_environment()
    assert set(material.public_environment) == {*public, *fixed_toolchain}
    assert {key: child[key] for key in public} == public
    assert {key: child[key] for key in fixed_toolchain} == fixed_toolchain
    assert sources[0] == {"name": "presentation", "commit": "d" * 40}
    assert actual_authority_sha == authority_sha
    assert not set(secret).intersection(child)
    assert pipeline.PREPARE_SIGNING_HANDOFF_ENVIRONMENT not in child
    assert "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS" not in child
    assert not set(pipeline.PREPARE_SIGNING_ACCEPTED_ENVIRONMENTS).intersection(parent)

    fields = bytes(material.handoff).split(b"\x00")
    assert fields[0].decode() == pipeline.PREPARE_SIGNING_HANDOFF_MAGIC
    assert [field.decode() for field in fields[1:5]] == [
        secret[key] for key in pipeline.PREPARE_SIGNING_SECRET_ENVIRONMENTS
    ]
    assert fields[5] == b""
    material.clear()
    assert not material.handoff


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("SM_TLS_SKIP_VERIFY", "true"),
        ("SM_UNDOCUMENTED_SECRET", "provider-value"),
        ("SIGNING_SM_API_KEY", "internal-alias-secret"),
        ("SIGNING_HANDOFF_CAPTURED", "1"),
        ("CHUMMER_WINDOWS_SIGN_PFX_PATH", "/private/legacy.pfx"),
        ("CHUMMER_WINDOWS_KEYLOCKER_SIGNER_UNEXPECTED_SHA256", "a" * 64),
        ("CHUMMER_WINDOWS_SIGN_PFX_PASSWORD", "legacy-password"),
        ("CHUMMER_WINDOWS_SIGN_PFX_BASE64", "QUFB\nQkJC\nQ0ND"),
        ("CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS", "precomposed-secret"),
        ("BASH_ENV", "/tmp/injected"),
        ("ENV", "/tmp/injected"),
        ("BASH_FUNC_hostile%%", "() { return 97; }"),
        ("DOTNET_STARTUP_HOOKS", "/tmp/hostile.dll"),
        ("DOTNET_ADDITIONAL_DEPS", "/tmp/hostile.deps.json"),
        ("COREHOST_TRACE", "1"),
        ("COMPlus_EnableDiagnostics", "1"),
        ("NUGET_PLUGIN_PATHS", "/tmp/hostile-plugin"),
        ("MSBuildSDKsPath", "/tmp/hostile-sdks"),
        ("LD_PRELOAD", "/tmp/hostile.so"),
        ("LD_LIBRARY_PATH", "/tmp/hostile-libs"),
    ],
)
def test_prepare_rejects_wildcard_legacy_and_shell_initialization_environment(
    tmp_path: Path, name: str, value: str
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    parent = {"PATH": os.environ["PATH"], **public, **secret, name: value}
    with pytest.raises(pipeline.PipelineError):
        pipeline.capture_prepare_signing_material(parent)


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("SM_HOST", "http://clientauth.one.digicert.com"),
        ("SM_HOST", "https://*.one.digicert.com"),
        ("SM_HOST", "https://clientauth.other.digicert.com"),
        ("SM_API_KEY", "first\nsecond"),
        ("SM_CLIENT_CERT_PASSWORD", "first\rsecond"),
        ("SM_CLIENT_CERT_PASSWORD", "ambiguous|password"),
    ],
)
def test_prepare_rejects_insecure_multiline_and_ambiguous_secret_values(
    tmp_path: Path, name: str, value: str
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    secret[name] = value
    with pytest.raises(pipeline.PipelineError):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )


def test_prepare_requires_complete_fixed_backend_and_no_signing_fails_closed(
    tmp_path: Path,
) -> None:
    with pytest.raises(
        pipeline.PipelineError,
        match="complete fixed DIGICERTONE signing environment",
    ):
        pipeline.capture_prepare_signing_material({"PATH": os.environ["PATH"]})

    public, secret = signing_environment_fixture(tmp_path)
    public["CHUMMER_WINDOWS_SIGNING_BACKEND"] = "another-backend"
    with pytest.raises(pipeline.PipelineError, match="fixed Linux Jsign"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )

    public, secret = signing_environment_fixture(tmp_path)
    public.pop("CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256")
    with pytest.raises(
        pipeline.PipelineError,
        match="complete fixed DIGICERTONE signing environment",
    ):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )


@pytest.mark.parametrize("name", sorted(pipeline.PREPARE_SIGNING_TOOLCHAIN_ENVIRONMENTS))
def test_prepare_rejects_every_caller_supplied_toolchain_anchor(
    tmp_path: Path, name: str,
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    with pytest.raises(pipeline.PipelineError, match="unknown or forbidden"):
        pipeline.capture_prepare_signing_material(
            {
                "PATH": os.environ["PATH"],
                **public,
                **secret,
                name: "caller-self-pin",
            }
        )


def test_prepare_requires_approved_tools_and_lowercase_signer_pins(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    approved_java_sha256 = pipeline.PREPARE_SIGNING_APPROVED_JAVA_SHA256
    approved_tree_sha256 = pipeline.PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256
    public, secret = signing_environment_fixture(tmp_path)
    monkeypatch.setattr(pipeline, "PREPARE_SIGNING_APPROVED_JAVA_SHA256", "0" * 64)
    with pytest.raises(pipeline.PipelineError, match="Java digest differs"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )
    monkeypatch.setattr(
        pipeline,
        "PREPARE_SIGNING_APPROVED_JAVA_SHA256",
        approved_java_sha256,
    )

    monkeypatch.setattr(
        pipeline,
        "PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256",
        "0" * 64,
    )
    with pytest.raises(pipeline.PipelineError, match="Java tree digest differs"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )
    monkeypatch.setattr(
        pipeline,
        "PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256",
        approved_tree_sha256,
    )

    public["CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256"] = "A" * 64
    with pytest.raises(pipeline.PipelineError, match="pins must be lowercase"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )

    public, secret = signing_environment_fixture(tmp_path)
    pipeline.PREPARE_SIGNING_JSIGN_JAR.write_bytes(b"tampered jsign fixture")
    with pytest.raises(pipeline.PipelineError, match="Jsign digest differs"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )


def test_prepare_enforces_secret_file_and_field_bounds(tmp_path: Path) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    client_certificate = Path(secret["SM_CLIENT_CERT_FILE"])
    for invalid_mode in (0o644, 0o700, 0o000):
        client_certificate.chmod(invalid_mode)
        with pytest.raises(pipeline.PipelineError, match="private owned regular file"):
            pipeline.capture_prepare_signing_material(
                {"PATH": os.environ["PATH"], **public, **secret}
            )

    client_certificate.chmod(0o400)
    material = pipeline.capture_prepare_signing_material(
        {"PATH": os.environ["PATH"], **public, **secret}
    )
    material.clear()

    client_certificate.chmod(0o600)
    nested = tmp_path / "nested"
    nested.mkdir()
    secret["SM_CLIENT_CERT_FILE"] = str(nested / ".." / client_certificate.name)
    with pytest.raises(pipeline.PipelineError, match="private owned regular file"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )

    secret["SM_CLIENT_CERT_FILE"] = str(client_certificate)
    secret["SM_API_KEY"] = "x" * (
        pipeline.PREPARE_SIGNING_SECRET_LIMITS["SM_API_KEY"] + 1
    )
    with pytest.raises(pipeline.PipelineError, match="fixed bound"):
        pipeline.capture_prepare_signing_material(
            {"PATH": os.environ["PATH"], **public, **secret}
        )


def test_trusted_bash_is_absolute_root_owned_and_not_caller_selected(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    assert pipeline.require_trusted_bash() == "/bin/bash"

    caller_bash = tmp_path / "bash"
    caller_bash.write_text("#!/usr/bin/env sh\nexit 0\n", encoding="utf-8")
    caller_bash.chmod(0o777)
    monkeypatch.setattr(pipeline, "TRUSTED_BASH_PATH", caller_bash)
    with pytest.raises(pipeline.PipelineError, match="posture is invalid"):
        pipeline.require_trusted_bash()


def test_jit_environment_is_positive_and_rejects_all_signing_names() -> None:
    clean_parent = {
        "PATH": os.environ["PATH"],
        "HOME": "/tmp/operator-home",
        "GH_TOKEN": "jit-auth-token",
        "DOCKER_CONTEXT": "default",
        "CHUMMER_AMBIENT_OVERRIDE": "must-not-pass",
        "LD_PRELOAD": "/tmp/injected.so",
    }
    child = pipeline.jit_environment(clean_parent)
    assert child == {
        "PATH": clean_parent["PATH"],
        "HOME": clean_parent["HOME"],
        "GH_TOKEN": clean_parent["GH_TOKEN"],
        "DOCKER_CONTEXT": "default",
    }

    for name in (
        "SM_TLS_SKIP_VERIFY",
        "SM_UNDOCUMENTED_SECRET",
        "SM_API_KEY",
        "CHUMMER_WINDOWS_SIGN_PFX_PASSWORD",
        "CHUMMER_WINDOWS_KEYLOCKER_HOST",
        "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS",
        "BASH_ENV",
        "ENV",
    ):
        with pytest.raises(pipeline.PipelineError):
            pipeline.jit_environment({**clean_parent, name: "present"})


def test_unrelated_coordinator_child_has_no_signing_environment_or_descriptor(
    tmp_path: Path,
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    parent = {"PATH": os.environ["PATH"], **public, **secret}
    material = pipeline.capture_prepare_signing_material(parent, consume=True)
    result = tmp_path / "coordinator-child-probe.txt"
    probe = tmp_path / "coordinator_child_probe.py"
    probe.write_text(
        "import os\n"
        "from pathlib import Path\n"
        "environment = Path('/proc/self/environ').read_bytes()\n"
        "fd_targets = []\n"
        "for name in os.listdir('/proc/self/fd'):\n"
        "    try:\n"
        "        fd_targets.append(os.readlink(f'/proc/self/fd/{name}'))\n"
        "    except FileNotFoundError:\n"
        "        pass\n"
        "forbidden = (\n"
        "    b'SM_API_KEY=',\n"
        "    b'SM_CLIENT_CERT_PASSWORD=',\n"
        "    b'CHUMMER_WINDOWS_SIGNING_HANDOFF_FD=',\n"
        "    b'synthetic-api-key',\n"
        "    b'synthetic-client-password',\n"
        ")\n"
        "clean = not any(value in environment for value in forbidden)\n"
        "clean = clean and not any('chummer-windows-signing-handoff' in value for value in fd_targets)\n"
        f"Path({str(result)!r}).write_text('clean' if clean else 'leak')\n",
        encoding="utf-8",
    )
    pipeline.run_checked(
        [sys.executable, str(probe)],
        cwd=tmp_path,
        environment=pipeline.jit_environment(parent),
        timeout_seconds=5,
    )
    assert result.read_text(encoding="utf-8") == "clean"
    material.clear()


def test_secret_bearing_output_is_suppressed_even_when_fragmented(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    secret = b"QUFB\nQkJC\nQ0ND"
    handoff = bytearray(b"magic\x00" + secret + b"\x00")
    code = (
        "import os,sys;"
        "fd=int(os.environ['CHUMMER_WINDOWS_SIGNING_HANDOFF_FD']);"
        "data=b'';"
        "\nwhile True:\n"
        " chunk=os.read(fd,4096)\n"
        " if not chunk: break\n"
        " data+=chunk\n"
        "os.close(fd);"
        "os.write(1,data.replace(b'\\n',b''));"
        "os.write(2,b'QkJC\\n')"
    )
    pipeline.run_checked(
        [sys.executable, "-c", code],
        cwd=tmp_path,
        environment={"PATH": os.environ["PATH"]},
        timeout_seconds=5,
        secret_handoff=handoff,
    )
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == ""
    assert not handoff


def test_signing_handoff_is_sealed_against_child_writes(tmp_path: Path) -> None:
    handoff = bytearray(b"magic\x00synthetic-secret\x00")
    code = (
        "import errno,os;"
        "fd=int(os.environ['CHUMMER_WINDOWS_SIGNING_HANDOFF_FD']);"
        "\ntry:\n"
        " os.write(fd,b'overwrite')\n"
        "except OSError as exc:\n"
        " raise SystemExit(0 if exc.errno in {errno.EPERM,errno.EBADF} else 8)\n"
        "raise SystemExit(9)"
    )
    pipeline.run_checked(
        [sys.executable, "-c", code],
        cwd=tmp_path,
        environment={"PATH": os.environ["PATH"]},
        timeout_seconds=5,
        secret_handoff=handoff,
    )
    assert not handoff


@pytest.mark.parametrize("name", ["BASH_ENV", "ENV"])
def test_bounded_runner_rejects_shell_initialization_environment_and_clears_handoff(
    tmp_path: Path, name: str
) -> None:
    handoff = bytearray(b"magic\x00synthetic-secret\x00")
    with pytest.raises(
        pipeline.PipelineError,
        match="forbidden child-initialization environment",
    ):
        pipeline.run_checked(
            [sys.executable, "-c", "raise SystemExit(0)"],
            cwd=tmp_path,
            environment={"PATH": os.environ["PATH"], name: "/tmp/injected"},
            timeout_seconds=5,
            secret_handoff=handoff,
        )
    assert not handoff


def test_secret_bearing_failure_has_fixed_error_and_no_log_or_receipt_leak(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    marker = "synthetic-sensitive-marker"
    handoff = bytearray(f"magic\x00{marker}\x00".encode())
    code = (
        "import os;"
        "fd=int(os.environ['CHUMMER_WINDOWS_SIGNING_HANDOFF_FD']);"
        "data=os.read(fd,65536);os.close(fd);"
        "os.write(1,data);os.write(2,data);raise SystemExit(9)"
    )
    with pytest.raises(pipeline.PipelineError, match="bounded pipeline command failed") as caught:
        pipeline.run_checked(
            [sys.executable, "-c", code],
            cwd=tmp_path,
            environment={"PATH": os.environ["PATH"]},
            timeout_seconds=5,
            secret_handoff=handoff,
        )
    captured = capsys.readouterr()
    visible = captured.out + captured.err + str(caught.value)
    assert marker not in visible
    assert list(tmp_path.iterdir()) == []


def test_signing_handoff_never_appears_in_child_argv(tmp_path: Path) -> None:
    marker = "synthetic-sensitive-argv-marker"
    result = tmp_path / "argv-result.txt"
    handoff = bytearray(f"magic\x00{marker}\x00".encode())
    code = (
        "import os,pathlib;"
        "fd=int(os.environ['CHUMMER_WINDOWS_SIGNING_HANDOFF_FD']);"
        "data=os.read(fd,65536);os.close(fd);"
        "secret=data.split(b'\\x00')[1];"
        "cmd=pathlib.Path('/proc/self/cmdline').read_bytes();"
        f"pathlib.Path({str(result)!r}).write_text('clean' if secret not in cmd else 'leak')"
    )
    pipeline.run_checked(
        [sys.executable, "-c", code],
        cwd=tmp_path,
        environment={"PATH": os.environ["PATH"]},
        timeout_seconds=5,
        secret_handoff=handoff,
    )
    assert result.read_text() == "clean"


def test_bounded_runner_terminates_more_than_four_mibibytes_of_output(
    tmp_path: Path,
) -> None:
    code = (
        "import os;"
        f"os.write(1,b'x'*({pipeline.MAX_CHILD_OUTPUT_BYTES}+1))"
    )
    with pytest.raises(pipeline.PipelineError, match="output limit"):
        pipeline.run_checked(
            [sys.executable, "-c", code],
            cwd=tmp_path,
            environment={"PATH": os.environ["PATH"]},
            timeout_seconds=10,
        )


def test_bounded_runner_kills_the_process_group_on_timeout(tmp_path: Path) -> None:
    pid_path = tmp_path / "descendant.pid"
    code = (
        "import pathlib,subprocess,time;"
        "child=subprocess.Popen(['sleep','30']);"
        f"pathlib.Path({str(pid_path)!r}).write_text(str(child.pid));"
        "time.sleep(30)"
    )
    with pytest.raises(pipeline.PipelineError, match="timed out"):
        pipeline.run_checked(
            [sys.executable, "-c", code],
            cwd=tmp_path,
            environment={"PATH": os.environ["PATH"]},
            timeout_seconds=0.3,
        )
    descendant_pid = int(pid_path.read_text())
    deadline = time.monotonic() + 2
    while time.monotonic() < deadline:
        stat_path = Path(f"/proc/{descendant_pid}/stat")
        if not stat_path.exists() or stat_path.read_text().split()[2] == "Z":
            break
        time.sleep(0.02)
    else:
        pytest.fail("timeout left a live descendant in the bounded process group")


def test_bounded_runner_closes_all_parent_descriptors_on_failure(
    tmp_path: Path,
) -> None:
    before = set(os.listdir("/proc/self/fd"))
    with pytest.raises(pipeline.PipelineError, match="bounded pipeline command failed"):
        pipeline.run_checked(
            [sys.executable, "-c", "raise SystemExit(4)"],
            cwd=tmp_path,
            environment={"PATH": os.environ["PATH"]},
            timeout_seconds=5,
            secret_handoff=bytearray(b"magic\x00value\x00"),
        )
    after = set(os.listdir("/proc/self/fd"))
    assert after == before


@pytest.mark.parametrize(
    ("script", "arguments"),
    [
        (
            REPO_ROOT / "scripts" / "build-preview-nightly-stage.sh",
            ["prepare"],
        ),
        (
            REPO_ROOT / "scripts" / "build-desktop-installer.sh",
            ["/tmp/publish", "avalonia", "win-x64", "Chummer.Avalonia.exe"],
        ),
    ],
)
def test_shell_handoff_is_consumed_and_closed_before_first_external_child(
    tmp_path: Path, script: Path, arguments: list[str]
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    material = pipeline.capture_prepare_signing_material(
        {"PATH": os.environ["PATH"], **public, **secret}
    )
    signing_environment = dict(material.public_environment)
    signing_environment.update(production_toolchain_environment())
    material.clear()
    probe = tmp_path / "probe.txt"
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    fake_dirname = fake_bin / "dirname"
    fake_dirname.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        "status=clean\n"
        "if [[ -n \"${SM_API_KEY:-}${SM_CLIENT_CERT_PASSWORD:-}"
        "${SIGNING_SM_API_KEY:-}${SIGNING_SM_CLIENT_CERT_FILE:-}"
        "${SIGNING_SM_CLIENT_CERT_PASSWORD:-}"
        "${CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS:-}"
        "${CHUMMER_WINDOWS_SIGNING_HANDOFF_FD:-}\" ]]; then status=environment-leak; fi\n"
        "while IFS= read -r -d '' entry; do\n"
        "  case \"$entry\" in\n"
        "    SM_API_KEY=*|SM_CLIENT_CERT_FILE=*|SM_CLIENT_CERT_PASSWORD=*|"
        "SIGNING_SM_API_KEY=*|SIGNING_SM_CLIENT_CERT_FILE=*|"
        "SIGNING_SM_CLIENT_CERT_PASSWORD=*|"
        "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS=*|"
        "CHUMMER_WINDOWS_SIGNING_HANDOFF_FD=*|"
        "*synthetic-api-key*|*synthetic-client-password*|*client-auth.p12*)\n"
        "      status=proc-environment-leak\n"
        "      ;;\n"
        "  esac\n"
        "done < \"/proc/$$/environ\"\n"
        "if [[ -e \"/proc/$$/fd/${PROBE_FD}\" ]]; then status=fd-leak; fi\n"
        "if [[ \"$(ulimit -c)\" != \"0\" ]]; then status=core-dumps-enabled; fi\n"
        "printf '%s\\n' \"$status\" > \"$PROBE_OUTPUT\"\n"
        "exit 97\n",
        encoding="utf-8",
    )
    fake_dirname.chmod(0o700)
    payload = bytearray()
    for field in (
        pipeline.PREPARE_SIGNING_HANDOFF_MAGIC,
        *(secret[key] for key in pipeline.PREPARE_SIGNING_SECRET_ENVIRONMENTS),
    ):
        payload.extend(field.encode())
        payload.append(0)
    read_descriptor, write_descriptor = os.pipe()
    try:
        os.write(write_descriptor, payload)
        os.close(write_descriptor)
        write_descriptor = -1
        environment = {
            "PATH": f"{fake_bin}:/usr/bin:/bin",
            "PROBE_FD": str(read_descriptor),
            "PROBE_OUTPUT": str(probe),
            pipeline.PREPARE_SIGNING_HANDOFF_ENVIRONMENT: str(read_descriptor),
            **signing_environment,
        }
        completed = subprocess.run(
            [pipeline.require_trusted_bash(), str(script), *arguments],
            env=environment,
            pass_fds=(read_descriptor,),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            check=False,
            timeout=5,
        )
        assert completed.returncode != 0
    finally:
        os.close(read_descriptor)
        if write_descriptor >= 0:
            os.close(write_descriptor)
    assert probe.exists(), completed.stderr.decode("utf-8", errors="replace")
    assert probe.read_text(encoding="utf-8").strip() == "clean"


def test_direct_linux_signer_scrubs_hostile_environment_argv_and_handoff_fd(
    tmp_path: Path,
) -> None:
    toolchain = require_provisioned_toolchain_environment()
    project_root = tmp_path / "direct-signer-fixture"
    output_root = tmp_path / "chummer-keylocker-signer-fixture" / "published"
    project_root.mkdir()
    output_root.mkdir(parents=True)
    (project_root / "fixture.csproj").write_text(
        """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>Chummer.KeyLockerSigner</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
""".strip()
        + "\n",
        encoding="utf-8",
    )
    (project_root / "Program.cs").write_text(
        """
using System.Collections;
using System.Text.Json;

var environment = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>()
    .ToDictionary(
        row => row.Key?.ToString() ?? string.Empty,
        row => row.Value?.ToString() ?? string.Empty);
var descriptors = Directory.EnumerateFiles("/proc/self/fd")
    .ToDictionary(
        path => Path.GetFileName(path),
        path => new FileInfo(path).LinkTarget ?? string.Empty);
var report = new
{
    arguments = args,
    commandLine = File.ReadAllText("/proc/self/cmdline"),
    descriptors,
    environment,
};
File.WriteAllText(args[1], JsonSerializer.Serialize(report));
""".strip()
        + "\n",
        encoding="utf-8",
    )
    subprocess.run(
        [
            "/usr/lib/dotnet/dotnet",
            "publish",
            str(project_root / "fixture.csproj"),
            "--configuration",
            "Release",
            "--runtime",
            "linux-x64",
            "--self-contained",
            "false",
            "--output",
            str(output_root),
        ],
        cwd="/",
        env={
            "DOTNET_CLI_HOME": str(tmp_path / "dotnet-home"),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_ROOT": "/usr/lib/dotnet",
            "HOME": str(tmp_path),
        },
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        check=True,
        timeout=60,
    )
    (output_root / pipeline.PREPARE_SIGNING_SDK_PIN_NAME).write_bytes(
        pipeline.PREPARE_SIGNING_SDK_PIN_BYTES
    )
    for directory, _directory_names, file_names in os.walk(
        output_root,
        topdown=False,
    ):
        for name in file_names:
            (Path(directory) / name).chmod(0o400)
        Path(directory).chmod(0o500)

    signer_dll = output_root / "Chummer.KeyLockerSigner.dll"
    runtime_config = output_root / "Chummer.KeyLockerSigner.runtimeconfig.json"
    deps = output_root / "Chummer.KeyLockerSigner.deps.json"
    signer_tree_sha = pipeline._sha256_canonical_tree(
        output_root.parent,
        output_root.name,
    )
    installer_text = (
        REPO_ROOT / "scripts" / "build-desktop-installer.sh"
    ).read_text(encoding="utf-8")
    wrapper_marker = (
        "IFS= read -r -d '' wrapper_source <<'BASH' || :\n"
    )
    assert installer_text.count(wrapper_marker) == 1
    wrapper_source = installer_text.split(wrapper_marker, 1)[1].split(
        "\nBASH\n",
        1,
    )[0]
    assert "powershell" not in wrapper_source
    assert "/usr/bin/env" not in wrapper_source
    assert (
        'builtin exec "$dotnet_bin" "$signer_dll" "${signer_arguments[@]}"'
        in wrapper_source
    )
    for verifier_with_closed_handoff in (
        'builtin compgen -e 3<&-',
        'builtin compgen -A function 3<&-',
        '/usr/bin/readlink -f -- "$link_path" 3<&-',
        '/usr/bin/find "$dotnet_root" -xdev -type l -print0 3<&-',
        '/usr/bin/sha256sum -- "$1" 3<&-',
        '/usr/bin/sha256sum 3<&-',
    ):
        assert verifier_with_closed_handoff in wrapper_source

    public_certificate = tmp_path / "public.pem"
    public_certificate.write_text("public fixture\n", encoding="utf-8")
    report_path = tmp_path / "signer-process-report.json"
    secret_values = (
        "https://clientauth.one.digicert.com",
        "synthetic-direct-api-key",
        str(tmp_path / "client-auth.p12"),
        "synthetic-direct-client-password",
    )
    payload = b"\x00".join(
        value.encode("utf-8")
        for value in (
            pipeline.PREPARE_SIGNING_HANDOFF_MAGIC,
            *secret_values,
            "",
        )
    )
    read_descriptor, write_descriptor = os.pipe()
    os.set_inheritable(read_descriptor, True)
    handoff_inode = os.fstat(read_descriptor).st_ino

    try:
        os.write(write_descriptor, payload)
        os.close(write_descriptor)
        write_descriptor = -1
        completed = subprocess.run(
                [
                    pipeline.require_trusted_bash(),
                    "--noprofile",
                    "--norc",
                    "-c",
                    (
                        'handoff_fd="$1"; wrapper="$2"; shift 2; '
                        'exec /bin/bash --noprofile --norc -c "$wrapper" '
                        '"$@" 3<&"$handoff_fd"'
                    ),
                    "chummer-keylocker-fd-launcher",
                    str(read_descriptor),
                    wrapper_source,
                    "chummer-keylocker-direct-test",
                "",
                "avalonia",
                "win-x64",
                "preview",
                "fixture-version",
                "0",
                pipeline.PREPARE_SIGNING_BACKEND,
                "http://timestamp.digicert.com",
                "fixture-alias",
                str(public_certificate),
                "a" * 64,
                "b" * 64,
                toolchain["CHUMMER_KEYLOCKER_JAVA_HOME"],
                toolchain["CHUMMER_KEYLOCKER_JAVA_BIN"],
                toolchain["CHUMMER_KEYLOCKER_JAVA_BIN_SHA256"],
                toolchain["CHUMMER_KEYLOCKER_JAVA_TREE_SHA256"],
                toolchain["CHUMMER_KEYLOCKER_JSIGN_JAR"],
                toolchain["CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256"],
                toolchain["CHUMMER_KEYLOCKER_DOTNET_ROOT"],
                toolchain["CHUMMER_KEYLOCKER_DOTNET_BIN"],
                toolchain["CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256"],
                toolchain["CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256"],
                str(signer_dll),
                pipeline.sha256_file(signer_dll),
                signer_tree_sha,
                pipeline.sha256_file(runtime_config),
                pipeline.sha256_file(deps),
                toolchain["CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256"],
                str(report_path),
            ],
            cwd="/",
            env={
                "PATH": str(tmp_path / "attacker-path"),
                "HTTP_PROXY": "http://attacker.invalid",
                "DOTNET_ADDITIONAL_DEPS": str(tmp_path / "attacker.deps.json"),
                "NUGET_PACKAGES": str(tmp_path / "attacker-packages"),
                "MSBuildSDKsPath": str(tmp_path / "attacker-sdks"),
                "BASH_FUNC_hostile%%": "() { return 97; }",
            },
            pass_fds=(read_descriptor,),
                stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            check=False,
            timeout=30,
        )
    finally:
        os.close(read_descriptor)
        if write_descriptor >= 0:
            os.close(write_descriptor)
    assert completed.returncode == 0, completed.stderr.decode(
        "utf-8",
        errors="replace",
    )

    report = json.loads(report_path.read_text(encoding="utf-8"))
    environment = report["environment"]
    command_line = report["commandLine"]
    assert environment[
        "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS"
    ] == "|".join(secret_values[1:])
    assert environment["CHUMMER_WINDOWS_KEYLOCKER_HOST"] == secret_values[0]
    assert environment["DOTNET_ROOT"] == "/usr/lib/dotnet"
    assert environment["DOTNET_EnableDiagnostics"] == "0"
    for name in (
        "PATH",
        "HTTP_PROXY",
        "DOTNET_ADDITIONAL_DEPS",
        "NUGET_PACKAGES",
        "MSBuildSDKsPath",
        "BASH_FUNC_hostile%%",
        "BASH_ENV",
        "ENV",
        "LD_PRELOAD",
        "LD_LIBRARY_PATH",
    ):
        assert name not in environment
    for secret in secret_values:
        assert secret not in command_line
    assert str(signer_dll) in command_line
    assert "--artifact" in report["arguments"]
    assert str(report_path) in report["arguments"]
    assert all(
        target != f"pipe:[{handoff_inode}]"
        for target in report["descriptors"].values()
    )


@pytest.mark.parametrize(
    ("script", "arguments"),
    [
        (
            REPO_ROOT / "scripts" / "build-preview-nightly-stage.sh",
            ["prepare"],
        ),
        (
            REPO_ROOT / "scripts" / "build-desktop-installer.sh",
            ["/tmp/publish", "avalonia", "win-x64", "Chummer.Avalonia.exe"],
        ),
    ],
)
@pytest.mark.parametrize("name", sorted(pipeline.PREPARE_SIGNING_TOOLCHAIN_ENVIRONMENTS))
def test_shells_reject_every_mismatched_coordinator_owned_toolchain_field(
    tmp_path: Path,
    script: Path,
    arguments: list[str],
    name: str,
) -> None:
    public, secret = signing_environment_fixture(tmp_path)
    material = pipeline.capture_prepare_signing_material(
        {"PATH": os.environ["PATH"], **public, **secret}
    )
    payload = bytes(material.handoff)
    signing_environment = dict(material.public_environment)
    signing_environment.update(production_toolchain_environment())
    material.clear()
    signing_environment[name] = "caller-self-pin"

    probe = tmp_path / "unexpected-child.txt"
    fake_bin = tmp_path / "mismatch-bin"
    fake_bin.mkdir()
    fake_dirname = fake_bin / "dirname"
    fake_dirname.write_text(
        "#!/bin/bash\n"
        "printf '%s\\n' unexpected > \"$PROBE_OUTPUT\"\n"
        "exit 97\n",
        encoding="utf-8",
    )
    fake_dirname.chmod(0o700)
    read_descriptor, write_descriptor = os.pipe()
    try:
        os.write(write_descriptor, payload)
        os.close(write_descriptor)
        write_descriptor = -1
        completed = subprocess.run(
            [pipeline.require_trusted_bash(), str(script), *arguments],
            env={
                "PATH": f"{fake_bin}:/usr/bin:/bin",
                "PROBE_OUTPUT": str(probe),
                pipeline.PREPARE_SIGNING_HANDOFF_ENVIRONMENT: str(read_descriptor),
                **signing_environment,
            },
            pass_fds=(read_descriptor,),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
            check=False,
            timeout=5,
        )
        assert completed.returncode == 2
        assert "signing handoff is invalid" in completed.stderr.lower()
    finally:
        os.close(read_descriptor)
        if write_descriptor >= 0:
            os.close(write_descriptor)
    assert not probe.exists()


def test_stage_process_substitution_replaces_the_consumed_fd_with_only_fd_three(
    tmp_path: Path,
) -> None:
    _public, secret = signing_environment_fixture(tmp_path)
    result = tmp_path / "installer-fd-probe.json"
    receiver = tmp_path / "installer_fd_probe.py"
    receiver.write_text(
        "import json, os\n"
        "from pathlib import Path\n"
        "fd = int(os.environ['CHUMMER_WINDOWS_SIGNING_HANDOFF_FD'])\n"
        "environment = Path('/proc/self/environ').read_bytes()\n"
        "open_fds = []\n"
        "for name in os.listdir('/proc/self/fd'):\n"
        "    try:\n"
        "        descriptor = int(name)\n"
        "        os.fstat(descriptor)\n"
        "    except (FileNotFoundError, OSError, ValueError):\n"
        "        continue\n"
        "    if descriptor > 2:\n"
        "        open_fds.append(descriptor)\n"
        "payload = b''\n"
        "while True:\n"
        "    chunk = os.read(fd, 4096)\n"
        "    if not chunk:\n"
        "        break\n"
        "    payload += chunk\n"
        "os.close(fd)\n"
        "forbidden = (b'SM_API_KEY=', b'SM_CLIENT_CERT_PASSWORD=', b'synthetic-api-key')\n"
        "Path(os.environ['PROBE_OUTPUT']).write_text(json.dumps({\n"
        "    'environmentClean': not any(value in environment for value in forbidden),\n"
        "    'fields': len(payload.split(b'\\x00')) - 1,\n"
        "    'openFds': sorted(open_fds),\n"
        "}))\n",
        encoding="utf-8",
    )
    payload = bytearray()
    for field in (
        pipeline.PREPARE_SIGNING_HANDOFF_MAGIC,
        *(secret[key] for key in pipeline.PREPARE_SIGNING_SECRET_ENVIRONMENTS),
    ):
        payload.extend(field.encode())
        payload.append(0)
    read_descriptor, write_descriptor = os.pipe()
    try:
        os.write(write_descriptor, payload)
        os.close(write_descriptor)
        write_descriptor = -1
        forwarding_script = (
            "set -euo pipefail\n"
            "ulimit -c 0\n"
            "fd=\"$SOURCE_FD\"\n"
            "unset SOURCE_FD BASH_ENV ENV\n"
            "IFS= read -r -d '' -u \"$fd\" magic\n"
            "IFS= read -r -d '' -u \"$fd\" host\n"
            "IFS= read -r -d '' -u \"$fd\" api\n"
            "IFS= read -r -d '' -u \"$fd\" cert\n"
            "IFS= read -r -d '' -u \"$fd\" password\n"
            "exec {fd}<&-\n"
            "handoff_path=<(printf '%s\\0%s\\0%s\\0%s\\0%s\\0' "
            "\"$magic\" \"$host\" \"$api\" \"$cert\" \"$password\")\n"
            "handoff_writer_pid=\"$!\"\n"
            "handoff_source_fd=\"${handoff_path##*/}\"\n"
            "exec 3<\"$handoff_path\"\n"
            "exec {handoff_source_fd}<&-\n"
            "exec {unrelated_fd}</dev/null\n"
            "CHUMMER_WINDOWS_SIGNING_HANDOFF_FD=3 "
            f"{sys.executable!s} {receiver!s} "
            "3<&3 {unrelated_fd}<&-\n"
            "exec 3<&-\n"
            "exec {unrelated_fd}<&-\n"
            "wait \"$handoff_writer_pid\"\n"
        )
        completed = subprocess.run(
            [pipeline.require_trusted_bash(), "-c", forwarding_script],
            env={
                "PATH": os.environ["PATH"],
                "PROBE_OUTPUT": str(result),
                "SOURCE_FD": str(read_descriptor),
            },
            pass_fds=(read_descriptor,),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
            timeout=5,
        )
        assert completed.returncode == 0
    finally:
        os.close(read_descriptor)
        if write_descriptor >= 0:
            os.close(write_descriptor)
    assert json.loads(result.read_text(encoding="utf-8")) == {
        "environmentClean": True,
        "fields": 5,
        "openFds": [3],
    }


def test_stage_authority_input_requires_the_complete_exact_environment(tmp_path: Path) -> None:
    environment = {key: str(tmp_path / key) for key in pipeline.STAGE_AUTHORITY_PATHS}
    environment.update({key: "e" * 64 for key in pipeline.STAGE_AUTHORITY_DIGESTS})
    for _, _, key in pipeline.SOURCE_AUTHORITY_ENVIRONMENTS:
        environment[key] = "d" * 40
    payload = {
        "contractName": "chummer6-ui.preview-nightly-stage-authority-input",
        "contractVersion": 1,
        "environment": environment,
    }
    path = tmp_path / "authority.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    loaded, digest = pipeline.load_stage_authority(path)
    assert loaded == environment
    assert digest == pipeline.sha256_file(path)

    parent = {
        "PATH": os.environ["PATH"],
        "HTTP_PROXY": "http://network-proxy.invalid:8080",
        "DirectoryBuildPropsPath": "/tmp/injected.props",
        "CustomBeforeMicrosoftCommonTargets": "/tmp/injected.targets",
        "MSBuildSDKsPath": "/tmp/injected-sdks",
        "RestoreSources": "https://packages.invalid/v3/index.json",
        "CHUMMER_AMBIENT_OVERRIDE": "1",
        "GH_TOKEN": "secret",
        "GITHUB_TOKEN": "secret",
        "BASH_ENV": "/tmp/injected-bash-env",
        "LD_PRELOAD": "/tmp/injected.so",
    }
    args = SimpleNamespace(
        evidence_directory=tmp_path / "evidence",
        prepared_stage_root=tmp_path / "candidate",
        published_at="2026-07-19T12:00:00Z",
        release_version="run-1",
        stage_authority_input=path,
        stage_dir=tmp_path / "stage",
    )
    child, sources, authority_sha = pipeline.stage_environment(args, parent)
    assert child["HTTP_PROXY"] == parent["HTTP_PROXY"]
    assert Path(child["NUGET_PACKAGES"]).is_relative_to(args.evidence_directory)
    assert sources[0] == {"name": "presentation", "commit": "d" * 40}
    assert authority_sha == digest
    for name in (
        "DirectoryBuildPropsPath",
        "CustomBeforeMicrosoftCommonTargets",
        "MSBuildSDKsPath",
        "RestoreSources",
        "CHUMMER_AMBIENT_OVERRIDE",
        "GH_TOKEN",
        "GITHUB_TOKEN",
        "BASH_ENV",
        "LD_PRELOAD",
    ):
        assert name not in child

    payload["environment"].pop("CHUMMER_CORE_EXPECTED_COMMIT")
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(pipeline.PipelineError, match="environment set is not exact"):
        pipeline.load_stage_authority(path)


def test_stage_authority_input_rejects_symlink(tmp_path: Path) -> None:
    target = tmp_path / "authority-target.json"
    target.write_text("{}", encoding="utf-8")
    link = tmp_path / "authority.json"
    link.symlink_to(target)
    with pytest.raises(pipeline.PipelineError, match="regular non-symlink"):
        pipeline.load_stage_authority(link)


def test_durable_outputs_replace_machine_local_paths_with_portable_roles() -> None:
    state = {
        "candidate": {
            "archivePath": "/tmp/candidate-original.zip",
            "archiveSha256": "1" * 64,
            "version": "run-20260719",
            "workflowId": "11",
        },
        "capture": {
            "archivePath": "/docker/capture-original.zip",
            "reviewRequestPath": "/workspace/human-review.json",
            "workflowId": "12",
        },
        "captureDispatch": {
            "archivePath": "/home/operator/capture-dispatch.zip",
            "runUrl": f"https://api.github.com/repos/{pipeline.REPOSITORY}/actions/runs/123",
        },
        "finalization": {
            "archivePath": r"C:\Users\operator\finalized.zip",
            "reviewInputPath": r"C:\Users\operator\review.json",
            "workflowId": "13",
        },
        "handoff": {"contractName": pipeline.HANDOFF_CONTRACT, "path": "/tmp/handoff.json"},
        "phase": "sealed_non_publishing_handoff",
        "release": {"channel": "preview", "version": "run-20260719"},
        "sealedStage": {
            "manifestSha256": "2" * 64,
            "path": "/docker/stage/run-20260719",
            "sealPath": "/docker/stage/run-20260719/PREVIEW_NIGHTLY_STAGE_SEAL.generated.json",
        },
        "sourceAuthorities": [{"commit": "a" * 40, "name": "presentation"}],
        "sourceSha": "a" * 40,
        "stageAuthorityInputSha256": "3" * 64,
    }
    args = SimpleNamespace(provenance_output=Path("/workspace/DURABLE_PROVENANCE.generated.json"))
    provenance = pipeline.build_provenance_payload(args, state)
    handoff = pipeline.build_publication_handoff(args, state, "4" * 64)
    pipeline.require_portable_payload(provenance, "durable provenance")
    pipeline.require_portable_payload(handoff, "publication handoff")
    encoded = json.dumps({"provenance": provenance, "handoff": handoff})
    for forbidden in ("/tmp/", "/docker/", "/workspace/", "/home/operator/", "C:\\Users\\"):
        assert forbidden not in encoded


@pytest.mark.parametrize("path", ["/tmp/proof.json", "/docker/proof.json", r"C:\proof.json"])
def test_portable_payload_rejects_machine_local_paths(path: str) -> None:
    with pytest.raises(pipeline.PipelineError, match="machine-local path"):
        pipeline.require_portable_payload({"proof": path}, "receipt")
