from __future__ import annotations

import importlib.util
import json
import stat
import sys
import zipfile
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = (
    ROOT / "scripts" / "release" / "produce_global_flagship_candidate.py"
)
SPEC = importlib.util.spec_from_file_location(
    "produce_global_flagship_candidate", SCRIPT
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def timestamp(offset: timedelta = timedelta()) -> str:
    return (
        (datetime.now(UTC) + offset)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def artifact(role: str, artifact_id: int, run_id: int) -> object:
    workflow, prefix, platform = MODULE.ROLE_POLICIES[role]
    spec = MODULE.ArtifactSpec(
        role=role,
        artifact_id=artifact_id,
        name=f"{prefix}-{run_id}-1",
        digest=f"sha256:{artifact_id:064x}",
        workflow_path=workflow,
        name_prefix=prefix,
        platform=platform,
    )
    return MODULE.AuthenticatedArtifact(
        spec=spec,
        metadata={"id": artifact_id},
        run={"id": run_id, "sourceSha": "a" * 40},
        workflow={"path": workflow},
        actor={"login": "operator"},
        workflow_blob={"path": workflow},
        archive_path=Path(f"/tmp/{role}.zip"),
    )


def run_payload(*, attempt: int = 1) -> dict[str, object]:
    user = {
        "id": 123,
        "login": "release-operator",
        "node_id": "MDQ6VXNlcjEyMw==",
        "type": "User",
    }
    repository = {
        "id": 456,
        "full_name": MODULE.SOURCE_REPOSITORY,
    }
    return {
        "id": 789,
        "run_attempt": attempt,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "head_branch": "main",
        "head_sha": "a" * 40,
        "path": ".github/workflows/linux-native-lifecycle-evidence.yml",
        "workflow_id": 987,
        "actor": user,
        "triggering_actor": dict(user),
        "repository": repository,
        "head_repository": dict(repository),
        "referenced_workflows": [],
        "pull_requests": [],
        "created_at": timestamp(timedelta(minutes=-3)),
        "run_started_at": timestamp(timedelta(minutes=-2)),
        "updated_at": timestamp(timedelta(minutes=-1)),
    }


def test_all_seven_exact_platform_roles_are_mandatory_and_cross_bound() -> None:
    values = [
        artifact("windows-export", 1, 101),
        artifact("windows-capture", 2, 102),
        artifact("windows-evidence", 3, 103),
        artifact("linux-export", 4, 104),
        artifact("linux-evidence", 5, 105),
        artifact("macos-escrow", 6, 106),
        artifact("macos-handoff", 7, 106),
    ]
    MODULE.validate_input_relationships(values)

    duplicate = list(values)
    duplicate[-1] = artifact("macos-handoff", 7, 107)
    with pytest.raises(MODULE.ContractError, match="cross-run"):
        MODULE.validate_input_relationships(duplicate)

    cross_platform = list(values)
    cross_platform[4] = artifact("linux-evidence", 5, 101)
    with pytest.raises(MODULE.ContractError, match="reuse"):
        MODULE.validate_input_relationships(cross_platform)


@pytest.mark.parametrize(
    ("mutation", "message"),
    (
        (lambda value: value.__setitem__("run_attempt", 2), "rerun"),
        (
            lambda value: value["referenced_workflows"].append(
                {"path": "reusable.yml"}
            ),
            "reusable",
        ),
        (
            lambda value: value["pull_requests"].append({"number": 1}),
            "pull request",
        ),
        (
            lambda value: value.__setitem__("head_sha", "b" * 40),
            "head_sha",
        ),
    ),
)
def test_workflow_run_rejects_replay_reuse_pr_and_source_drift(
    mutation: object, message: str
) -> None:
    payload = run_payload()
    mutation(payload)
    with pytest.raises(MODULE.ContractError, match=message):
        MODULE.validate_run(
            payload,
            expected_id=789,
            expected_workflow=(
                ".github/workflows/linux-native-lifecycle-evidence.yml"
            ),
            expected_source_sha="a" * 40,
            repository_id=456,
            now=datetime.now(UTC),
            label="test run",
        )


def test_workflow_run_accepts_only_fresh_direct_dispatch_attempt_one() -> None:
    projection = MODULE.validate_run(
        run_payload(),
        expected_id=789,
        expected_workflow=(
            ".github/workflows/linux-native-lifecycle-evidence.yml"
        ),
        expected_source_sha="a" * 40,
        repository_id=456,
        now=datetime.now(UTC),
        label="test run",
    )
    assert projection["attempt"] == 1
    assert projection["actor"]["login"] == "release-operator"


def test_zip_extraction_is_deterministic_and_rejects_traversal(
    tmp_path: Path,
) -> None:
    archive = tmp_path / "valid.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as handle:
        handle.writestr("receipts/evidence.json", b'{"status":"passed"}\n')
    destination = tmp_path / "valid"
    destination.mkdir()
    paths = MODULE.extract_artifact_archive(
        archive, destination, "valid test"
    )
    assert [path.relative_to(destination).as_posix() for path in paths] == [
        "receipts/evidence.json"
    ]

    unsafe = tmp_path / "unsafe.zip"
    with zipfile.ZipFile(unsafe, "w") as handle:
        handle.writestr("../escape.json", b"{}\n")
    with pytest.raises(MODULE.ContractError, match="portable"):
        MODULE.extract_artifact_archive(
            unsafe, tmp_path / "unsafe", "unsafe test"
        )


def test_zip_extraction_rejects_links_and_case_collisions(
    tmp_path: Path,
) -> None:
    linked = tmp_path / "linked.zip"
    info = zipfile.ZipInfo("receipts/link")
    info.create_system = 3
    info.external_attr = (stat.S_IFLNK | 0o777) << 16
    with zipfile.ZipFile(linked, "w") as handle:
        handle.writestr(info, b"target")
    with pytest.raises(MODULE.ContractError, match="linked or special"):
        MODULE.extract_artifact_archive(
            linked, tmp_path / "linked", "linked test"
        )

    collision = tmp_path / "collision.zip"
    with zipfile.ZipFile(collision, "w") as handle:
        handle.writestr("Evidence.json", b"{}\n")
        handle.writestr("evidence.json", b"{}\n")
    with pytest.raises(MODULE.ContractError, match="case-colliding"):
        MODULE.extract_artifact_archive(
            collision, tmp_path / "collision", "collision test"
        )


def test_duplicate_json_keys_and_unsafe_artifact_redirects_fail_closed() -> None:
    with pytest.raises(MODULE.ContractError, match="duplicate key"):
        MODULE.parse_json_bytes(b'{"id":1,"id":2}', "duplicate JSON")
    with pytest.raises(MODULE.ContractError, match="unapproved"):
        MODULE.validate_artifact_redirect(
            "https://attacker.example/candidate.zip"
        )
    with pytest.raises(MODULE.ContractError, match="credential-free"):
        MODULE.validate_artifact_redirect(
            "https://token@objects.githubusercontent.com/candidate.zip"
        )


def test_candidate_producer_contains_no_publication_or_signing_operation() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    forbidden = (
        "create_git_release",
        "upload_release_asset",
        "create_deployment",
        "update_release",
        "publish-download-bundle",
        "gh release",
        "notarytool submit",
        "signtool",
        "jsign",
    )
    for value in forbidden:
        assert value not in source
    assert '"propose"' in source
    assert "reauthenticate_artifact" in source
    assert "make_read_only(output_root)" in source


def test_exit_gates_are_loaded_only_from_their_authenticated_provider_roles() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert 'json_contracts(extraction_roots["windows-export"])' in source
    assert 'linux_contracts,' in source
    assert 'macos_receipt_contracts,' in source
    assert 'all_contracts' not in source
    assert (
        "global flagship candidate production is restricted to preview"
        in source
    )


def test_provider_receipts_never_contain_private_key_fields() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    provider_payloads = (
        source[source.index('"contractName": PROVIDER_CONTRACT') :],
        source[source.index('"contractName": REAUTH_CONTRACT') :],
    )
    for payload_source in provider_payloads:
        assert '"privateKey"' not in payload_source
        assert '"passphrase"' not in payload_source


def write_payload(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def test_windows_provider_chain_binds_export_capture_and_finalization(
    tmp_path: Path,
) -> None:
    capture_root = tmp_path / "capture"
    finalized_root = tmp_path / "finalized"
    authenticated = {
        role: value
        for role, value in (
            ("windows-export", artifact("windows-export", 1, 101)),
            ("windows-capture", artifact("windows-capture", 2, 102)),
            ("windows-evidence", artifact("windows-evidence", 3, 103)),
        )
    }
    capture_source = MODULE.provider_source_projection(
        authenticated["windows-capture"], artifact_name=True
    )
    export = authenticated["windows-export"]
    capture = {
        "contractName": (
            "chummer6-ui.preview-nightly-native-windows-capture"
        ),
        "candidate": {
            "repository": MODULE.SOURCE_REPOSITORY,
            "workflow": export.spec.workflow_path,
            "runId": str(export.run["id"]),
            "runAttempt": "1",
            "ref": MODULE.SOURCE_REF,
            "sha": export.run["sourceSha"],
            "actor": export.actor["login"],
            "artifactId": str(export.spec.artifact_id),
            "artifactName": export.spec.name,
            "artifactSha256": export.spec.digest.removeprefix("sha256:"),
        },
        "source": capture_source,
    }
    inventory = {
        "contractName": (
            "chummer6-ui.preview-nightly-native-windows-capture-inventory"
        ),
        "files": [{"path": "capture.json"}],
    }
    write_payload(capture_root / "capture.json", capture)
    write_payload(capture_root / "inventory.json", inventory)
    write_payload(finalized_root / "capture.json", capture)
    write_payload(finalized_root / "inventory.json", inventory)
    inventory_digest = MODULE.sha256_file(
        finalized_root / "inventory.json"
    )[0]
    finalization = {
        "contractName": (
            "chummer6-ui.preview-nightly-native-windows-finalization"
        ),
        "captureSource": capture_source,
        "finalizationSource": MODULE.provider_source_projection(
            authenticated["windows-evidence"], artifact_name=True
        ),
        "captureInventorySha256": inventory_digest,
    }
    write_payload(finalized_root / "finalization.json", finalization)
    MODULE.validate_windows_provider_chain(
        extraction_roots={
            "windows-capture": capture_root,
            "windows-evidence": finalized_root,
        },
        authenticated=authenticated,
    )

    finalization["captureInventorySha256"] = "0" * 64
    (finalized_root / "finalization.json").unlink()
    write_payload(finalized_root / "finalization.json", finalization)
    with pytest.raises(MODULE.ContractError, match="capture inventory"):
        MODULE.validate_windows_provider_chain(
            extraction_roots={
                "windows-capture": capture_root,
                "windows-evidence": finalized_root,
            },
            authenticated=authenticated,
        )


def test_linux_provider_chain_binds_export_bytes_and_lifecycle_run(
    tmp_path: Path,
) -> None:
    export_root = tmp_path / "linux-export"
    export = artifact("linux-export", 10, 201)
    evidence = artifact("linux-evidence", 11, 202)
    candidate = {
        "artifactFileName": "chummer-avalonia-linux-x64-installer.deb",
        "sha256": "b" * 64,
        "sizeBytes": 1234,
        "version": "run-20260725-120000",
    }
    export_receipt = {
        "contractName": "chummer6-ui.linux-native-candidate-export",
        "contractVersion": 2,
        "artifact": {
            "fileName": candidate["artifactFileName"],
            "memberPath": f"files/{candidate['artifactFileName']}",
            "sha256": candidate["sha256"],
            "sizeBytes": candidate["sizeBytes"],
        },
        "releaseVersion": candidate["version"],
        "source": {
            key: value
            for key, value in MODULE.provider_source_projection(
                export, artifact_name=False
            ).items()
            if key not in {"triggeringActor", "rerunPolicy"}
        },
    }
    write_payload(export_root / "export.json", export_receipt)
    lifecycle = {
        "candidate": candidate,
        "nativeRunner": {
            "source": MODULE.provider_source_projection(
                evidence, artifact_name=False
            )
        },
    }
    MODULE.validate_linux_provider_chain(
        extraction_roots={"linux-export": export_root},
        authenticated={"linux-export": export, "linux-evidence": evidence},
        lifecycle_receipt=lifecycle,
    )

    lifecycle["candidate"]["sha256"] = "c" * 64
    with pytest.raises(MODULE.ContractError, match="candidate bytes"):
        MODULE.validate_linux_provider_chain(
            extraction_roots={"linux-export": export_root},
            authenticated={"linux-export": export, "linux-evidence": evidence},
            lifecycle_receipt=lifecycle,
        )
