from __future__ import annotations

import importlib.util
import json
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
        },
        "contractName": pipeline.REVIEW_REQUEST_CONTRACT,
        "contractVersion": 1,
        "generatedAt": pipeline.now_iso(),
        "humanReviewConfirmed": False,
        "requiredChecks": ["readability", "contrast", "clipping"],
        "requiredHeads": ["avalonia", "blazor-desktop"],
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
                    "blazor-desktop": {"readability": True, "contrast": True, "clipping": True},
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
