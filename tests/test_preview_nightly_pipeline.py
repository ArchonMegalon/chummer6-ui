from __future__ import annotations

import importlib.util
import json
import os
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
