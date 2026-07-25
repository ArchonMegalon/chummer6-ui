from __future__ import annotations

import base64
import copy
import hashlib
import importlib.util
import io
import json
import stat
import sys
import urllib.error
import zipfile
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any, Callable, Mapping

import pytest


ROOT = Path(__file__).resolve().parents[1]
RELEASE_SCRIPTS = ROOT / "scripts" / "release"
ASSEMBLER_SCRIPT = RELEASE_SCRIPTS / "assemble_global_flagship_release.py"
VERIFIER_SCRIPT = (
    RELEASE_SCRIPTS / "authenticate_global_flagship_release.py"
)
SOURCE_SHA = "1" * 40
NOW_TEXT = "2026-07-25T12:10:00Z"


def load_module(name: str, path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


if str(RELEASE_SCRIPTS) not in sys.path:
    sys.path.insert(0, str(RELEASE_SCRIPTS))
ASSEMBLER = load_module(
    "assemble_global_flagship_release", ASSEMBLER_SCRIPT
)
VERIFIER = load_module(
    "authenticate_global_flagship_release", VERIFIER_SCRIPT
)
NOW = ASSEMBLER.parse_time(NOW_TEXT, "test now")


def json_bytes(value: object) -> bytes:
    return (
        json.dumps(value, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def snapshot(data: bytes, name: str) -> object:
    return ASSEMBLER.Snapshot(
        path=Path(name),
        relative_path=name,
        sha256=hashlib.sha256(data).hexdigest(),
        size_bytes=len(data),
        data=data,
    )


def reference(name: str, seed: str) -> dict[str, object]:
    return {
        "path": name,
        "sha256": seed * 64,
        "sizeBytes": 100,
    }


def candidate_and_platforms() -> tuple[dict[str, Any], dict[str, Any]]:
    platform_specs = {
        "windows": (
            "win-x64",
            "avalonia-win-x64-installer",
            "chummer-avalonia-win-x64-installer.exe",
            "a",
        ),
        "linux": (
            "linux-x64",
            "avalonia-linux-x64-installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "b",
        ),
        "macos": (
            "osx-arm64",
            "avalonia-osx-arm64-installer",
            "chummer-avalonia-osx-arm64-installer.dmg",
            "c",
        ),
    }
    manifest_platforms: dict[str, Any] = {}
    projected_platforms: dict[str, Any] = {}
    for platform, (rid, artifact_id, file_name, seed) in platform_specs.items():
        artifact_path = f"artifacts/{file_name}"
        exit_reference = reference(f"{platform}-exit.json", seed)
        native_reference = reference(f"{platform}-native.json", seed)
        signing_reference = (
            None
            if platform == "linux"
            else reference(f"{platform}-signing.json", seed)
        )
        manifest_platforms[platform] = {
            "rid": rid,
            "artifact": {
                "artifactId": artifact_id,
                "fileName": file_name,
                "path": artifact_path,
                "sha256": seed * 64,
                "sizeBytes": 100,
            },
            "exitGateReceipt": exit_reference,
            "signingReceipt": signing_reference,
            "nativeE2eReceipt": native_reference,
        }

        def projection(
            item: Mapping[str, object] | None,
            **extra: object,
        ) -> dict[str, object] | None:
            if item is None:
                return None
            return {
                "relativePath": item["path"],
                "sha256": item["sha256"],
                "sizeBytes": item["sizeBytes"],
                **extra,
            }

        projected_platforms[platform] = {
            "rid": rid,
            "artifact": projection(
                manifest_platforms[platform]["artifact"],
                artifactId=artifact_id,
                fileName=file_name,
            ),
            "exitGateReceipt": projection(
                exit_reference,
                contractName=f"test.{platform}.exit",
                generatedAt="2026-07-25T11:35:00Z",
            ),
            "signingReceipt": projection(
                signing_reference,
                contractName=ASSEMBLER.SIGNING_CONTRACT,
                generatedAt="2026-07-25T11:36:00Z",
            ),
            "nativeE2eReceipt": projection(
                native_reference,
                contractName=f"test.{platform}.native",
                generatedAt="2026-07-25T11:37:00Z",
                runnerActor=f"{platform}-runner",
            ),
            "nativeE2eEvidence": {},
            "nativeLifecycleEvidence": None,
            "integrityPolicy": f"{platform}-test-policy",
        }
    candidate = {
        "contractName": ASSEMBLER.CANDIDATE_CONTRACT,
        "contractVersion": 1,
        "generatedAt": "2026-07-25T11:30:00Z",
        "expiresAt": "2026-07-26T11:30:00Z",
        "candidateId": "candidate-20260725",
        "generationId": "generation-20260725",
        "releaseVersion": "run-20260725-120000",
        "previousReleaseVersion": "run-20260724-120000",
        "channelId": "stable",
        "source": {
            "repository": ASSEMBLER.SOURCE_REPOSITORY,
            "ref": ASSEMBLER.RELEASE_APPROVAL_REF,
            "commit": SOURCE_SHA,
        },
        "producer": {
            "actor": "candidate-producer",
            "workflow": ".github/workflows/global-flagship-candidate.yml",
            "runId": 50,
            "runAttempt": 1,
        },
        "platforms": manifest_platforms,
    }
    return candidate, projected_platforms


def make_policy() -> tuple[dict[str, Any], bytes, object]:
    policy = {
        "contractName": ASSEMBLER.REVIEWER_POLICY_CONTRACT,
        "contractVersion": 1,
        "roles": {
            "quality": ["quality-reviewer"],
            "release": ["release-reviewer"],
            "security": ["security-reviewer"],
        },
    }
    data = json_bytes(policy)
    return policy, data, snapshot(data, "reviewer-policy.json")


def make_local_bundle() -> tuple[object, bytes, bytes, dict[str, bytes]]:
    candidate, platforms = candidate_and_platforms()
    candidate_data = json_bytes(candidate)
    candidate_snapshot = snapshot(candidate_data, "candidate.json")
    proposal = {
        "contractName": ASSEMBLER.PROPOSAL_CONTRACT,
        "contractVersion": 1,
        "generatedAt": "2026-07-25T12:00:00Z",
        "expiresAt": "2026-07-25T14:00:00Z",
        "status": "ready_for_independent_approval",
        "candidate": {
            "candidateId": candidate["candidateId"],
            "generationId": candidate["generationId"],
            "releaseVersion": candidate["releaseVersion"],
            "previousReleaseVersion": candidate["previousReleaseVersion"],
            "channelId": candidate["channelId"],
            "source": candidate["source"],
            "producer": candidate["producer"],
            "generatedAt": candidate["generatedAt"],
            "expiresAt": candidate["expiresAt"],
        },
        "candidateManifest": ASSEMBLER.binding(candidate_snapshot),
        "platforms": platforms,
        "requiredApprovals": list(ASSEMBLER.REQUIRED_APPROVAL_ROLES),
        "excludedApprovalActors": [
            "candidate-producer",
            "linux-runner",
            "macos-runner",
            "windows-runner",
        ],
        "externalRequirements": list(ASSEMBLER.EXTERNAL_REQUIREMENTS),
        "authorityLevel": ASSEMBLER.AUTHORITY_LEVEL,
        "provenanceAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": list(ASSEMBLER.ALLOWED_SIDE_EFFECTS),
    }
    proposal_data = json_bytes(proposal)
    proposal_snapshot = snapshot(proposal_data, "proposal.json")
    _policy, policy_data, policy_snapshot = make_policy()
    actors = {
        "quality": ("quality-reviewer", "release-reviewer", 201),
        "release": ("release-reviewer", "security-reviewer", 202),
        "security": ("security-reviewer", "quality-reviewer", 203),
    }
    approval_snapshots: dict[str, object] = {}
    approval_bytes: dict[str, bytes] = {}
    projections: list[dict[str, Any]] = []
    for offset, role in enumerate(ASSEMBLER.REQUIRED_APPROVAL_ROLES):
        actor, environment_approver, run_id = actors[role]
        payload = ASSEMBLER.approval_payload(
            proposal_snapshot,
            policy_snapshot,
            expected_proposal_sha256=proposal_snapshot.sha256,
            role=role,
            approval_confirmed=True,
            repository=ASSEMBLER.SOURCE_REPOSITORY,
            ref=ASSEMBLER.RELEASE_APPROVAL_REF,
            sha=SOURCE_SHA,
            workflow_ref=(
                f"{ASSEMBLER.SOURCE_REPOSITORY}/"
                f"{ASSEMBLER.APPROVAL_WORKFLOW}@"
                f"{ASSEMBLER.RELEASE_APPROVAL_REF}"
            ),
            workflow_sha=SOURCE_SHA,
            run_id=run_id,
            run_attempt=1,
            actor=actor,
            triggering_actor=actor,
            environment_approver=environment_approver,
            environment=ASSEMBLER.APPROVAL_ENVIRONMENT,
            now=ASSEMBLER.parse_time(
                f"2026-07-25T12:0{2 + offset}:00Z",
                "approval time",
            ),
        )
        data = json_bytes(payload)
        approval_bytes[role] = data
        approval_snapshot = snapshot(data, "approval.json")
        approval_snapshots[role] = approval_snapshot
        projections.append(
            ASSEMBLER.validate_approval(
                approval_snapshot,
                proposal_snapshot=proposal_snapshot,
                proposal=proposal,
                now=NOW,
            )
        )
    projections.sort(key=lambda item: item["role"])
    final = {
        "contractName": ASSEMBLER.FINAL_RECEIPT_CONTRACT,
        "contractVersion": 1,
        "generatedAt": "2026-07-25T12:05:00Z",
        "status": "passed",
        "candidate": proposal["candidate"],
        "candidateManifest": ASSEMBLER.binding(candidate_snapshot),
        "proposal": ASSEMBLER.binding(
            proposal_snapshot,
            contractName=ASSEMBLER.PROPOSAL_CONTRACT,
        ),
        "platforms": proposal["platforms"],
        "approvals": projections,
        "externalRequirements": proposal["externalRequirements"],
        "authorityLevel": proposal["authorityLevel"],
        "provenanceAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": proposal["allowedSideEffects"],
        "handoff": {
            "eligibleForSeparatePublicationReview": True,
            "requiredNextAuthority": (
                VERIFIER.ASSEMBLER_FINAL_REQUIRED_NEXT_AUTHORITY
            ),
        },
    }
    final_data = json_bytes(final)
    local_bundle = VERIFIER.LocalBundle(
        proposal=proposal_snapshot,
        candidate=candidate_snapshot,
        final_receipt=snapshot(final_data, "final-receipt.json"),
        approvals=approval_snapshots,
    )
    inner = VERIFIER.build_input_bundle(local_bundle, now=NOW)
    return local_bundle, inner, policy_data, approval_bytes


def zip_bytes(entries: Mapping[str, bytes]) -> bytes:
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_STORED) as archive:
        for name, data in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | 0o444) << 16
            archive.writestr(info, data)
    return output.getvalue()


def user(login: str, user_id: int) -> dict[str, object]:
    return {"login": login, "id": user_id, "type": "User"}


def artifact_metadata(
    *,
    artifact_id: int,
    name: str,
    run_id: int,
    archive: bytes,
) -> dict[str, object]:
    return {
        "id": artifact_id,
        "name": name,
        "size_in_bytes": len(archive),
        "archive_download_url": (
            f"{VERIFIER.API_ROOT}/repos/{ASSEMBLER.SOURCE_REPOSITORY}/"
            f"actions/artifacts/{artifact_id}/zip"
        ),
        "expired": False,
        "created_at": "2026-07-25T12:05:10Z",
        "expires_at": "2026-08-25T12:05:10Z",
        "updated_at": "2026-07-25T12:05:11Z",
        "digest": f"sha256:{hashlib.sha256(archive).hexdigest()}",
        "workflow_run": {
            "id": run_id,
            "repository_id": 42,
            "head_repository_id": 42,
            "head_branch": "main",
            "head_sha": SOURCE_SHA,
        },
    }


def content_response(path: str, data: bytes) -> dict[str, object]:
    return {
        "type": "file",
        "path": path,
        "encoding": "base64",
        "size": len(data),
        "content": base64.b64encode(data).decode("ascii"),
        "sha": VERIFIER.git_blob_sha1(data),
    }


class FakeProvider:
    def __init__(
        self,
        responses: Mapping[str, Any],
        archives: Mapping[int, bytes],
    ) -> None:
        self.responses = copy.deepcopy(dict(responses))
        self.archives = dict(archives)
        self.calls: list[str] = []

    def get_json(self, path: str) -> object:
        self.calls.append(path)
        if path not in self.responses:
            raise AssertionError(f"unexpected provider path: {path}")
        value = copy.deepcopy(self.responses[path])
        if isinstance(value, VERIFIER.JsonResponse):
            return value
        return VERIFIER.JsonResponse(value=value, headers={})

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        self.calls.append(f"archive:{artifact_id}")
        data = self.archives[artifact_id]
        assert len(data) <= max_bytes
        return data


@dataclass
class ProviderFixture:
    client: FakeProvider
    admin: FakeProvider
    input_id: int
    input_digest: str
    approval_artifact_ids: dict[str, int]


def make_provider_fixture() -> ProviderFixture:
    _local, inner, policy_data, approval_bytes = make_local_bundle()
    outer = zip_bytes({VERIFIER.INPUT_BUNDLE_FILE_NAME: inner})
    input_id = 900
    input_metadata = artifact_metadata(
        artifact_id=input_id,
        name=VERIFIER.INPUT_ARTIFACT_NAME,
        run_id=800,
        archive=outer,
    )
    responses: dict[str, Any] = {
        VERIFIER.repository_api_path(""): {
            "id": 42,
            "full_name": ASSEMBLER.SOURCE_REPOSITORY,
            "default_branch": "main",
            "archived": False,
            "disabled": False,
        },
        VERIFIER.repository_api_path(f"/actions/artifacts/{input_id}"): (
            input_metadata
        ),
        VERIFIER.repository_api_path("/branches/main"): {
            "name": "main",
            "protected": True,
            "commit": {"sha": SOURCE_SHA},
        },
        VERIFIER.repository_api_path(
            f"/contents/{ASSEMBLER.APPROVAL_WORKFLOW}"
            f"?ref={SOURCE_SHA}"
        ): content_response(
            ASSEMBLER.APPROVAL_WORKFLOW,
            b"name: Global flagship release approval\n",
        ),
        VERIFIER.repository_api_path(
            f"/contents/{VERIFIER.REVIEWER_POLICY_PATH}"
            f"?ref={SOURCE_SHA}"
        ): content_response(VERIFIER.REVIEWER_POLICY_PATH, policy_data),
        VERIFIER.repository_api_path(
            f"/environments/{ASSEMBLER.APPROVAL_ENVIRONMENT}"
        ): {
            "id": 99,
            "name": ASSEMBLER.APPROVAL_ENVIRONMENT,
            "can_admins_bypass": False,
            "deployment_branch_policy": {
                "protected_branches": False,
                "custom_branch_policies": True,
            },
            "protection_rules": [
                {"id": 1, "type": "branch_policy"},
                {
                    "id": 2,
                    "type": "required_reviewers",
                    "prevent_self_review": True,
                    "reviewers": [
                        {
                            "type": "User",
                            "reviewer": user("quality-reviewer", 11),
                        },
                        {
                            "type": "User",
                            "reviewer": user("release-reviewer", 12),
                        },
                        {
                            "type": "User",
                            "reviewer": user("security-reviewer", 13),
                        },
                    ],
                },
            ],
        },
        VERIFIER.repository_api_path(
            f"/environments/{ASSEMBLER.APPROVAL_ENVIRONMENT}/"
            "deployment-branch-policies?per_page=100&page=1"
        ): {
            "total_count": 1,
            "branch_policies": [{"id": 1, "name": "main"}],
        },
        VERIFIER.repository_api_path("/actions/workflows/77"): {
            "id": 77,
            "path": ASSEMBLER.APPROVAL_WORKFLOW,
            "state": "active",
        },
    }
    archives: dict[int, bytes] = {input_id: outer}
    approval_artifact_ids: dict[str, int] = {}
    role_data = {
        "quality": ("quality-reviewer", 11, "release-reviewer", 12, 201),
        "release": ("release-reviewer", 12, "security-reviewer", 13, 202),
        "security": ("security-reviewer", 13, "quality-reviewer", 11, 203),
    }
    for role, (
        actor,
        actor_id,
        approver,
        approver_id,
        run_id,
    ) in role_data.items():
        run = {
            "id": run_id,
            "run_attempt": 1,
            "event": "workflow_dispatch",
            "status": "completed",
            "conclusion": "success",
            "head_branch": "main",
            "head_sha": SOURCE_SHA,
            "path": f"{ASSEMBLER.APPROVAL_WORKFLOW}@main",
            "workflow_id": 77,
            "actor": user(actor, actor_id),
            "triggering_actor": user(actor, actor_id),
            "repository": {
                "id": 42,
                "full_name": ASSEMBLER.SOURCE_REPOSITORY,
            },
            "head_repository": {
                "id": 42,
                "full_name": ASSEMBLER.SOURCE_REPOSITORY,
            },
            "referenced_workflows": [],
            "pull_requests": [],
            "created_at": "2026-07-25T12:01:00Z",
            "run_started_at": "2026-07-25T12:01:30Z",
            "updated_at": "2026-07-25T12:06:00Z",
        }
        responses[
            VERIFIER.repository_api_path(f"/actions/runs/{run_id}")
        ] = copy.deepcopy(run)
        responses[
            VERIFIER.repository_api_path(
                f"/actions/runs/{run_id}/attempts/1"
                "?exclude_pull_requests=false"
            )
        ] = copy.deepcopy(run)
        responses[
            VERIFIER.repository_api_path(
                f"/actions/runs/{run_id}/approvals"
            )
        ] = [
            {
                "state": "approved",
                "comment": "reviewed",
                "environments": [
                    {
                        "id": 99,
                        "name": ASSEMBLER.APPROVAL_ENVIRONMENT,
                    }
                ],
                "user": user(approver, approver_id),
            }
        ]
        artifact_id = 700 + (run_id - 200)
        approval_artifact_ids[role] = artifact_id
        archive = zip_bytes({"approval.json": approval_bytes[role]})
        archives[artifact_id] = archive
        metadata = artifact_metadata(
            artifact_id=artifact_id,
            name=(
                f"global-flagship-release-approval-{role}-{run_id}-1"
            ),
            run_id=run_id,
            archive=archive,
        )
        responses[
            VERIFIER.repository_api_path(
                f"/actions/runs/{run_id}/artifacts?per_page=100&page=1"
            )
        ] = {"total_count": 1, "artifacts": [metadata]}
        responses[
            VERIFIER.repository_api_path(
                f"/actions/artifacts/{artifact_id}"
            )
        ] = metadata
    protection = {
        "required_status_checks": {
            "strict": True,
            "contexts": ["flagship-contracts"],
            "checks": [],
        },
        "enforce_admins": {"enabled": True},
        "required_pull_request_reviews": {
            "dismiss_stale_reviews": True,
            "require_code_owner_reviews": False,
            "required_approving_review_count": 2,
            "require_last_push_approval": True,
            "bypass_pull_request_allowances": {
                "users": [],
                "teams": [],
                "apps": [],
            },
        },
        "required_conversation_resolution": {"enabled": True},
        "required_linear_history": {"enabled": True},
        "allow_force_pushes": {"enabled": False},
        "allow_deletions": {"enabled": False},
    }
    admin_responses = {
        VERIFIER.repository_api_path("/branches/main/protection"): protection
    }
    return ProviderFixture(
        client=FakeProvider(responses, archives),
        admin=FakeProvider(admin_responses, {}),
        input_id=input_id,
        input_digest=str(input_metadata["digest"]),
        approval_artifact_ids=approval_artifact_ids,
    )


def authenticate(fixture: ProviderFixture) -> dict[str, Any]:
    return VERIFIER.authenticate_provider_handoff(
        fixture.client,
        fixture.admin,
        input_artifact_id=fixture.input_id,
        expected_input_artifact_digest=fixture.input_digest,
        expected_verifier_source_sha=SOURCE_SHA,
        now=NOW,
    )


def test_provider_authentication_e2e_is_true_but_nonpublishing() -> None:
    fixture = make_provider_fixture()
    handoff = authenticate(fixture)

    assert handoff["status"] == "passed"
    assert handoff["provenanceAuthenticated"] is True
    assert handoff["releaseArtifactBytesAuthenticated"] is False
    assert handoff["publicationAuthorized"] is False
    assert handoff["nonPublishing"] is True
    assert handoff["transportArtifact"]["trustedAsAuthority"] is False
    assert [item["role"] for item in handoff["approvals"]] == [
        "quality",
        "release",
        "security",
    ]
    assert len({item["run"]["id"] for item in handoff["approvals"]}) == 3
    assert len({item["actor"] for item in handoff["approvals"]}) == 3
    assert handoff["mainBranchGovernance"]["enforceAdmins"] is True
    assert handoff["mainBranchGovernance"][
        "requiredApprovingReviewCount"
    ] == 2
    assert set(fixture.admin.calls) == {
        VERIFIER.repository_api_path("/branches/main/protection")
    }
    assert not any(
        "/actions/" in path or "/contents/" in path
        for path in fixture.admin.calls
    )


def test_provider_authentication_binds_the_executing_verifier_source() -> None:
    fixture = make_provider_fixture()
    with pytest.raises(
        VERIFIER.ContractError,
        match="candidate and executing verifier source SHA",
    ):
        VERIFIER.authenticate_provider_handoff(
            fixture.client,
            fixture.admin,
            input_artifact_id=fixture.input_id,
            expected_input_artifact_digest=fixture.input_digest,
            expected_verifier_source_sha="2" * 40,
            now=NOW,
        )


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path("/actions/runs/201")
            ].__setitem__("run_attempt", 2),
            "run_attempt",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path("/actions/runs/201")
            ].__setitem__("event", "push"),
            "event",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path("/actions/runs/201")
            ].__setitem__("actor", user("different-reviewer", 88)),
            "actor.login",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path(
                    "/actions/runs/201/attempts/1"
                    "?exclude_pull_requests=false"
                )
            ].__setitem__("updated_at", "2026-07-25T12:06:01Z"),
            "attempt updatedAt",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path(
                    "/actions/runs/201/approvals"
                )
            ].clear(),
            "exactly one environment review",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path(
                    "/actions/runs/201/approvals"
                )
            ][0]["user"].__setitem__("id", 999),
            "approval history reviewer.id",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path(
                    f"/environments/{ASSEMBLER.APPROVAL_ENVIRONMENT}"
                )
            ].__setitem__("id", 100),
            "environment.id",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path(
                    f"/environments/{ASSEMBLER.APPROVAL_ENVIRONMENT}"
                )
            ].__setitem__("can_admins_bypass", True),
            "permits administrator bypass",
        ),
        (
            lambda fixture: fixture.client.responses[
                VERIFIER.repository_api_path("/branches/main")
            ]["commit"].__setitem__("sha", "2" * 40),
            "commit SHA",
        ),
        (
            lambda fixture: fixture.admin.responses[
                VERIFIER.repository_api_path("/branches/main/protection")
            ]["enforce_admins"].__setitem__("enabled", False),
            "does not enforce administrators",
        ),
        (
            lambda fixture: fixture.admin.responses[
                VERIFIER.repository_api_path("/branches/main/protection")
            ]["required_pull_request_reviews"][
                "bypass_pull_request_allowances"
            ]["users"].append(user("admin", 90)),
            "permits a branch-protection bypass",
        ),
        (
            lambda fixture: fixture.admin.responses[
                VERIFIER.repository_api_path("/branches/main/protection")
            ].__setitem__("required_pull_request_reviews", None),
            "pull-request reviews must be an object",
        ),
    ],
)
def test_provider_authentication_fails_closed_on_authority_drift(
    mutate: Callable[[ProviderFixture], None],
    message: str,
) -> None:
    fixture = make_provider_fixture()
    mutate(fixture)
    with pytest.raises(VERIFIER.ContractError, match=message):
        authenticate(fixture)


def test_provider_authentication_rejects_paginated_or_duplicate_artifacts() -> None:
    fixture = make_provider_fixture()
    path = VERIFIER.repository_api_path(
        "/actions/runs/201/artifacts?per_page=100&page=1"
    )
    value = fixture.client.responses[path]
    fixture.client.responses[path] = VERIFIER.JsonResponse(
        value=value,
        headers={
            "Link": (
                '<https://api.github.com/example?page=2>; rel="next", '
                '<https://api.github.com/example?page=2>; rel="last"'
            )
        },
    )
    with pytest.raises(VERIFIER.ContractError, match="paginated"):
        authenticate(fixture)

    fixture = make_provider_fixture()
    listing = fixture.client.responses[path]
    listing["total_count"] = 2
    listing["artifacts"].append(copy.deepcopy(listing["artifacts"][0]))
    with pytest.raises(VERIFIER.ContractError, match="exactly one artifact"):
        authenticate(fixture)


def test_provider_authentication_rejects_expired_or_mutated_artifact() -> None:
    fixture = make_provider_fixture()
    artifact_id = fixture.approval_artifact_ids["quality"]
    detail_path = VERIFIER.repository_api_path(
        f"/actions/artifacts/{artifact_id}"
    )
    fixture.client.responses[detail_path]["expired"] = True
    with pytest.raises(VERIFIER.ContractError, match="expired"):
        authenticate(fixture)

    fixture = make_provider_fixture()
    artifact_id = fixture.approval_artifact_ids["quality"]
    fixture.client.archives[artifact_id] += b"mutation"
    with pytest.raises(
        VERIFIER.ContractError, match="size does not match provider metadata"
    ):
        authenticate(fixture)


def test_source_policy_bytes_must_equal_all_receipt_bindings() -> None:
    fixture = make_provider_fixture()
    path = VERIFIER.repository_api_path(
        f"/contents/{VERIFIER.REVIEWER_POLICY_PATH}?ref={SOURCE_SHA}"
    )
    changed = json_bytes(
        {
            "contractName": ASSEMBLER.REVIEWER_POLICY_CONTRACT,
            "contractVersion": 1,
            "roles": {
                "quality": ["different-reviewer"],
                "release": ["release-reviewer"],
                "security": ["security-reviewer"],
            },
        }
    )
    fixture.client.responses[path] = content_response(
        VERIFIER.REVIEWER_POLICY_PATH, changed
    )
    with pytest.raises(
        ASSEMBLER.ContractError, match="not authorized|does not match"
    ):
        authenticate(fixture)


def test_input_bundle_final_receipt_binding_tamper_fails_closed() -> None:
    bundle, _inner, _policy, _approval_bytes = make_local_bundle()
    final = json.loads(bundle.final_receipt.data.decode("utf-8"))
    final["publicationAuthorized"] = True
    tampered = VERIFIER.LocalBundle(
        proposal=bundle.proposal,
        candidate=bundle.candidate,
        final_receipt=snapshot(
            json_bytes(final), "final-receipt.json"
        ),
        approvals=bundle.approvals,
    )
    with pytest.raises(VERIFIER.ContractError, match="final publicationAuthorized"):
        VERIFIER.validate_local_bundle(tampered, now=NOW)


@pytest.mark.parametrize(
    "location",
    [
        "http://productionresultssa.blob.core.windows.net/a",
        "https://evil.example/a",
        "https://token@example.blob.core.windows.net/a",
        "https://objects.githubusercontent.com/a#fragment",
        "https://productionresultssa.blob.core.windows.net:444/a",
        "https://productionresultssa.blob.core.windows.net:invalid/a",
    ],
)
def test_artifact_redirect_validation_fails_closed(location: str) -> None:
    with pytest.raises(VERIFIER.ContractError):
        VERIFIER.validate_artifact_redirect(location)


def test_artifact_redirect_accepts_only_documented_storage_hop() -> None:
    location = (
        "https://productionresultssa.blob.core.windows.net/"
        "actions-results/example?sig=redacted"
    )
    assert VERIFIER.validate_artifact_redirect(location) == location


def test_zip_reader_rejects_duplicates_links_and_traversal() -> None:
    duplicate_buffer = io.BytesIO()
    with zipfile.ZipFile(duplicate_buffer, "w") as archive:
        archive.writestr("approval.json", b"first")
        with pytest.warns(UserWarning, match="Duplicate name"):
            archive.writestr("approval.json", b"second")
    with pytest.raises(VERIFIER.ContractError, match="duplicate"):
        VERIFIER.read_exact_zip(
            duplicate_buffer.getvalue(),
            expected_names={"approval.json"},
            maximum_entries=2,
            maximum_total_bytes=1024,
            label="test archive",
        )

    link_buffer = io.BytesIO()
    link = zipfile.ZipInfo("approval.json")
    link.create_system = 3
    link.external_attr = (stat.S_IFLNK | 0o777) << 16
    with zipfile.ZipFile(link_buffer, "w") as archive:
        archive.writestr(link, b"target")
    with pytest.raises(VERIFIER.ContractError, match="non-regular"):
        VERIFIER.read_exact_zip(
            link_buffer.getvalue(),
            expected_names={"approval.json"},
            maximum_entries=1,
            maximum_total_bytes=1024,
            label="test archive",
        )

    traversal_buffer = io.BytesIO()
    with zipfile.ZipFile(traversal_buffer, "w") as archive:
        archive.writestr("../approval.json", b"receipt")
    with pytest.raises(VERIFIER.ContractError):
        VERIFIER.read_exact_zip(
            traversal_buffer.getvalue(),
            expected_names=None,
            maximum_entries=1,
            maximum_total_bytes=1024,
            label="test archive",
        )


def test_network_failure_does_not_leak_token() -> None:
    secret = "github_pat_never-print-this"
    client = VERIFIER.GitHubApi(secret)

    class FailingOpener:
        def open(self, *_args: object, **_kwargs: object) -> object:
            raise urllib.error.URLError(f"transport contained {secret}")

    client._api_opener = FailingOpener()
    with pytest.raises(VERIFIER.ContractError) as caught:
        client.get_json("/repos/ArchonMegalon/chummer6-ui")
    assert secret not in str(caught.value)


def test_administration_reader_rejects_every_other_capability() -> None:
    fixture = make_provider_fixture()
    restricted = VERIFIER.RestrictedAdministrationReader(fixture.admin)
    expected = VERIFIER.repository_api_path("/branches/main/protection")

    assert restricted.get_json(expected).value["enforce_admins"] == {
        "enabled": True
    }
    with pytest.raises(VERIFIER.ContractError, match="outside the exact"):
        restricted.get_json(VERIFIER.repository_api_path("/branches/main"))
    with pytest.raises(VERIFIER.ContractError, match="cannot download"):
        restricted.get_artifact_archive(701, 1024)
    assert fixture.admin.calls == [expected]


def test_write_once_refuses_overwrite_and_seals_output(tmp_path: Path) -> None:
    output = tmp_path / "handoff.json"
    VERIFIER.write_once(output, b"first\n")
    assert stat.S_IMODE(output.stat().st_mode) == 0o444
    with pytest.raises(VERIFIER.ContractError, match="refusing to replace"):
        VERIFIER.write_once(output, b"second\n")
    assert output.read_bytes() == b"first\n"


def test_pack_cli_revalidates_metadata_only_bundle(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    bundle, _inner, _policy, _approval_bytes = make_local_bundle()
    proposal = tmp_path / bundle.proposal.relative_path
    candidate = tmp_path / bundle.candidate.relative_path
    final_receipt = tmp_path / bundle.final_receipt.relative_path
    proposal.write_bytes(bundle.proposal.data)
    candidate.write_bytes(bundle.candidate.data)
    final_receipt.write_bytes(bundle.final_receipt.data)
    approval_paths: list[Path] = []
    for role in ASSEMBLER.REQUIRED_APPROVAL_ROLES:
        path = tmp_path / role / "approval.json"
        path.parent.mkdir()
        path.write_bytes(bundle.approvals[role].data)
        approval_paths.append(path)
    output = tmp_path / VERIFIER.INPUT_BUNDLE_FILE_NAME
    monkeypatch.setattr(VERIFIER, "current_time", lambda: NOW)
    argv = [
        "pack",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--final-receipt",
        str(final_receipt),
        "--output",
        str(output),
    ]
    for path in approval_paths:
        argv.extend(["--approval", str(path)])

    assert VERIFIER.main(argv) == 0
    assert stat.S_IMODE(output.stat().st_mode) == 0o444
    rebuilt = VERIFIER.read_local_bundle(output.read_bytes())
    validated = VERIFIER.validate_local_bundle(rebuilt, now=NOW)
    assert validated.final_receipt["publicationAuthorized"] is False
    assert VERIFIER.main(argv) == 1
