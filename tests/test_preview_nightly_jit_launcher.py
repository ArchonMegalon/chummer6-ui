from __future__ import annotations

import argparse
import base64
import hashlib
import io
import importlib.util
import json
import os
import signal
import stat
import subprocess
import sys
import tarfile
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
VERSION = "preview-20260718.1"
SOURCE_SHA = "a" * 40
NONCE = "abcdefghijklmnopqrstuvwx"
RUN_ID = 12001


def load_module(name: str, relative: str):
    spec = importlib.util.spec_from_file_location(name, REPO_ROOT / relative)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


exporter = load_module(
    "preview_nightly_candidate_export_for_jit_tests",
    "scripts/preview_nightly_candidate_export.py",
)
launcher = load_module(
    "preview_nightly_jit_launcher_for_tests",
    "scripts/preview_nightly_jit_launcher.py",
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def make_stage(root: Path) -> Path:
    stage = root / "prepared-stage"
    files = stage / "files"
    files.mkdir(parents=True)
    rows = []
    for index, head in enumerate(exporter.HEADS, start=1):
        installer = stage / exporter.installer_path(head)
        payload = stage / exporter.payload_path(head)
        installer.write_bytes(b"MZ" + bytes([index]) * 1024)
        payload.write_bytes(b"PK" + bytes([index + 10]) * 2048)
        rows.append(
            {
                "head": head,
                "headId": head,
                "platform": "windows",
                "rid": "win-x64",
                "kind": "installer",
                "fileName": installer.name,
                "sha256": sha256(installer),
                "sizeBytes": installer.stat().st_size,
                "installerMode": "bootstrap",
                "payloadAcquisitionMode": "download",
                "payloadFileName": payload.name,
                "payloadSha256": sha256(payload),
                "payloadSizeBytes": payload.stat().st_size,
            }
        )
    write_json(
        stage / exporter.MANIFEST_PATH,
        {
            "contractName": exporter.MANIFEST_CONTRACT,
            "contract_name": exporter.MANIFEST_CONTRACT,
            "schemaVersion": 1,
            "version": VERSION,
            "releaseVersion": VERSION,
            "channelId": "preview",
            "channel": "preview",
            "artifacts": rows,
        },
    )
    return stage.resolve()


def authority() -> object:
    return launcher.Authority(SOURCE_SHA, "ArchonMegalon", 77)


def run_row(run_id: int = 101, *, path: str | None = None) -> dict[str, object]:
    return {
        "id": run_id,
        "event": "workflow_dispatch",
        "head_branch": "main",
        "head_sha": SOURCE_SHA,
        "path": path or launcher.WORKFLOW_PATH,
        "workflow_id": 77,
        "run_attempt": 1,
    }


def dispatch_response(run_id: int = RUN_ID) -> dict[str, object]:
    return {
        "workflow_run_id": run_id,
        "run_url": f"https://api.github.com/repos/{launcher.REPOSITORY}/actions/runs/{run_id}",
        "html_url": f"https://github.com/{launcher.REPOSITORY}/actions/runs/{run_id}",
    }


def verified_run_row(run_id: int = RUN_ID) -> dict[str, object]:
    row = run_row(run_id)
    row.update(
        {
            "url": dispatch_response(run_id)["run_url"],
            "html_url": dispatch_response(run_id)["html_url"],
            "actor": {"login": "ArchonMegalon"},
            "triggering_actor": {"login": "ArchonMegalon"},
            "repository": {"full_name": launcher.REPOSITORY},
            "display_title": launcher.RUNNER_LABEL_PREFIX + NONCE,
        }
    )
    return row


def encoded_jit_config() -> str:
    payload = {
        name: base64.b64encode((f"synthetic:{name}\n").encode()).decode()
        for name in launcher.ALLOWED_JIT_CONFIG_FILES
    }
    return base64.b64encode(
        json.dumps(payload, separators=(",", ":")).encode()
    ).decode()


def volume_identity(name: str = "a" * 64) -> object:
    labels = {"com.docker.volume.anonymous": ""}
    return launcher.VolumeIdentity(
        name,
        "local",
        f"/var/lib/docker/volumes/{name}/_data",
        "2026-07-18T00:00:00Z",
        "local",
        json.dumps(labels, sort_keys=True, separators=(",", ":")),
        "{}",
        NONCE,
    )


def container_identity(
    identifier: str = "b" * 64,
    name: str | None = None,
) -> object:
    return launcher.ContainerIdentity(
        identifier,
        name or launcher.CONFIG_HOLDER_PREFIX + NONCE,
        "2026-07-18T00:00:00Z",
        "sha256:" + "c" * 64,
        launcher.IMAGE,
        "0:0",
        "{}",
        "[]",
        "[]",
        "[]",
        "[]",
        NONCE,
    )


def config_lease(volume_name: str = "a" * 64) -> object:
    return launcher.ConfigLease(container_identity(), volume_identity(volume_name))


def test_materialize_exact_subset_and_permissions(tmp_path: Path) -> None:
    stage = make_stage(tmp_path)
    (stage / "unrelated-proof.json").write_text("ignored", encoding="utf-8")
    subset = (tmp_path / "private" / "candidate-input").resolve()
    subset.parent.mkdir()

    identity = launcher.materialize_candidate_subset(stage, subset, exporter)

    assert identity.version == VERSION
    assert identity.manifest_sha256 == sha256(stage / exporter.MANIFEST_PATH)
    assert [row["path"] for row in identity.content] == sorted(exporter.CONTENT_PATHS)
    assert exporter.exact_regular_files(subset, "test subset") == sorted(exporter.CONTENT_PATHS)
    assert stat.S_IMODE(subset.stat().st_mode) == 0o555
    assert stat.S_IMODE((subset / "files").stat().st_mode) == 0o555
    assert all(stat.S_IMODE((subset / path).stat().st_mode) == 0o444 for path in exporter.CONTENT_PATHS)


@pytest.mark.parametrize("target", ["root", "files", "manifest"])
def test_materialize_rejects_symlink_boundaries(tmp_path: Path, target: str) -> None:
    stage = make_stage(tmp_path)
    subset_parent = tmp_path / "private"
    subset_parent.mkdir()
    if target == "root":
        link = tmp_path / "stage-link"
        link.symlink_to(stage, target_is_directory=True)
        stage = link
    elif target == "files":
        real_files = tmp_path / "real-files"
        (stage / "files").rename(real_files)
        (stage / "files").symlink_to(real_files, target_is_directory=True)
    else:
        manifest = stage / exporter.MANIFEST_PATH
        real_manifest = tmp_path / "real-manifest.json"
        manifest.rename(real_manifest)
        manifest.symlink_to(real_manifest)
    with pytest.raises((launcher.LaunchError, OSError)):
        launcher.materialize_candidate_subset(stage, subset_parent / "candidate", exporter)


def test_materialize_detects_held_source_mutation(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    stage = make_stage(tmp_path)
    subset_parent = tmp_path / "private"
    subset_parent.mkdir()
    original = launcher.copy_held_source
    calls = 0

    def mutate_after_copy(source, target):
        nonlocal calls
        original(source, target)
        calls += 1
        if calls == 1:
            (stage / source.relative).write_bytes(b"changed")

    monkeypatch.setattr(launcher, "copy_held_source", mutate_after_copy)
    with pytest.raises(launcher.LaunchError, match="changed"):
        launcher.materialize_candidate_subset(stage, subset_parent / "candidate", exporter)


def test_private_tree_cleanup_handles_read_only_snapshot() -> None:
    identity = launcher.create_private_tree()
    subset = identity.path / "candidate-input"
    subset.mkdir()
    file = subset / "proof"
    file.write_text("proof", encoding="utf-8")
    file.chmod(0o444)
    subset.chmod(0o555)
    launcher.remove_private_tree(identity)
    assert not identity.path.exists()


def test_private_tree_cleanup_refuses_symlink() -> None:
    identity = launcher.create_private_tree()
    (identity.path / "escape").symlink_to("/tmp")
    with pytest.raises(launcher.LaunchError, match="unsafe"):
        launcher.remove_private_tree(identity)
    (identity.path / "escape").unlink()
    launcher.remove_private_tree(identity)


def test_trusted_exporter_executes_descriptor_snapshot_not_reopened_path(
    tmp_path: Path,
) -> None:
    path = tmp_path / "exporter.py"
    path.write_text("SNAPSHOT_VALUE = 'trusted'\n", encoding="utf-8")
    snapshot = path.read_bytes()
    path.write_text("SNAPSHOT_VALUE = 'replaced'\n", encoding="utf-8")
    module = launcher.load_trusted_exporter(snapshot)
    assert module.SNAPSHOT_VALUE == "trusted"


def test_child_environments_strip_credentials(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("GH_TOKEN", "secret")
    monkeypatch.setenv("GITHUB_TOKEN", "secret")
    monkeypatch.setenv("DOCKER_CONTEXT", "default")
    monkeypatch.setenv("GH_HOST", "enterprise.example")
    monkeypatch.setenv("GH_ENTERPRISE_TOKEN", "enterprise-secret")
    monkeypatch.setenv("GITHUB_ENTERPRISE_TOKEN", "enterprise-secret")
    assert launcher.command_environment("gh")["GH_TOKEN"] == "secret"
    assert "GH_TOKEN" not in launcher.command_environment("docker")
    assert "GITHUB_TOKEN" not in launcher.command_environment("docker")
    assert "GH_TOKEN" not in launcher.command_environment("local")
    assert "GH_HOST" not in launcher.command_environment("gh")
    assert "GH_ENTERPRISE_TOKEN" not in launcher.command_environment("gh")
    assert "GITHUB_ENTERPRISE_TOKEN" not in launcher.command_environment("gh")


def test_every_gh_api_call_pins_github_com_and_exact_api_version(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    captured: list[str] = []
    monkeypatch.setattr(
        launcher,
        "run_checked",
        lambda args, **_kwargs: captured.extend(args) or "{}",
    )
    launcher.gh_json(f"repos/{launcher.REPOSITORY}/actions/runs/{RUN_ID}")
    assert captured[:4] == ["gh", "api", "--hostname", "github.com"]
    assert "Accept: application/vnd.github+json" in captured
    assert "X-GitHub-Api-Version: 2026-03-10" in captured


@pytest.mark.parametrize(
    "endpoint",
    [
        "repos/evil/repo",
        "repos/ArchonMegalon/chummer6-uievil/actions/runs/1",
        "repos/ArchonMegalon/chummer6-ui/../evil",
        "https://example.test",
    ],
)
def test_gh_api_rejects_endpoint_injection(endpoint: str) -> None:
    with pytest.raises(launcher.LaunchError, match="authority boundary"):
        launcher.gh_json(endpoint)


def test_generate_nonce_retries_exact_runner_collision(monkeypatch: pytest.MonkeyPatch) -> None:
    values = iter(("a" * 24, "b" * 24))
    monkeypatch.setattr(launcher.secrets, "token_hex", lambda _: next(values))
    runners = [{"name": launcher.RUNNER_NAME_PREFIX + "a" * 24, "labels": []}]
    assert launcher.generate_unique_nonce(runners) == "b" * 24


@pytest.mark.parametrize(
    "qualified",
    [
        launcher.WORKFLOW_PATH,
        f"{launcher.WORKFLOW_PATH}@main",
        f"{launcher.WORKFLOW_PATH}@refs/heads/main",
        f"{launcher.WORKFLOW_PATH}@{SOURCE_SHA}",
    ],
)
def test_exact_run_identity_accepts_only_exact_qualified_paths(qualified: str) -> None:
    assert launcher.exact_run_identity(run_row(path=qualified), authority())
    assert not launcher.exact_run_identity(run_row(path=qualified + "-evil"), authority())


def test_wait_correlation_uses_only_exact_run_and_exact_unique_job_labels(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    label = launcher.RUNNER_LABEL_PREFIX + NONCE
    endpoints: list[str] = []
    monkeypatch.setattr(
        launcher,
        "gh_json",
        lambda endpoint, **_kwargs: endpoints.append(endpoint) or verified_run_row(),
    )
    monkeypatch.setattr(
        launcher,
        "run_jobs",
        lambda run_id: [{
            "id": run_id * 10,
            "name": launcher.EXPORT_JOB_NAME,
            "labels": ["self-hosted", "linux", "x64", label],
        }],
    )
    assert launcher.wait_for_correlated_run(
        RUN_ID, authority(), label, launcher.time.monotonic() + 10
    )["id"] == RUN_ID
    assert endpoints == [f"repos/{launcher.REPOSITORY}/actions/runs/{RUN_ID}"]


def test_wait_correlation_rejects_duplicate_job_labels(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    label = launcher.RUNNER_LABEL_PREFIX + NONCE
    monkeypatch.setattr(launcher, "gh_json", lambda *_args, **_kwargs: verified_run_row())
    monkeypatch.setattr(
        launcher,
        "run_jobs",
        lambda _run_id: [{
            "id": 1001,
            "name": launcher.EXPORT_JOB_NAME,
            "labels": ["self-hosted", "linux", "x64", label, label],
        }],
    )
    with pytest.raises(launcher.LaunchError, match="duplicate labels"):
        launcher.wait_for_correlated_run(
            RUN_ID, authority(), label, launcher.time.monotonic() + 10
        )


def test_wait_correlation_timeout_is_bounded(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(launcher.time, "monotonic", lambda: 100.0)
    with pytest.raises(launcher.LaunchError, match="timed out"):
        launcher.wait_for_correlated_run(
            RUN_ID,
            authority(),
            launcher.RUNNER_LABEL_PREFIX + NONCE,
            100.0,
        )


@pytest.mark.parametrize("total", [2, 101, True])
def test_run_jobs_rejects_nonexact_or_paginated_total_count(
    monkeypatch: pytest.MonkeyPatch, total: object
) -> None:
    monkeypatch.setattr(
        launcher,
        "gh_json",
        lambda endpoint, **_kwargs: {
            "total_count": total,
            "jobs": [{"id": 1}],
        },
    )
    with pytest.raises(launcher.LaunchError, match="incomplete or ambiguous"):
        launcher.run_jobs(RUN_ID)


def test_dispatch_uses_only_fixed_workflow_and_exact_inputs(monkeypatch: pytest.MonkeyPatch) -> None:
    captured = {}

    def fake(endpoint, **kwargs):
        captured.update(endpoint=endpoint, **kwargs)
        return dispatch_response()

    monkeypatch.setattr(launcher, "gh_json", fake)
    candidate = launcher.CandidateIdentity(Path("/candidate"), VERSION, "b" * 64, ())
    assert launcher.dispatch_workflow(candidate, authority(), NONCE) == dispatch_response()
    assert captured["endpoint"].endswith("/preview-nightly-candidate-export.yml/dispatches")
    assert captured["method"] == "POST"
    assert captured["payload"] == {
        "ref": "main",
        "return_run_details": True,
        "inputs": {
            "runner_nonce": NONCE,
            "candidate_version": VERSION,
            "candidate_manifest_sha256": "b" * 64,
            "expected_source_sha": SOURCE_SHA,
            "export_confirmed": True,
        },
    }


def test_dispatch_details_persist_exact_run_id_and_validate_urls_and_identity(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    response = dispatch_response()
    run_id = launcher.dispatch_run_id(response)
    assert run_id == RUN_ID
    monkeypatch.setattr(launcher, "gh_json", lambda *_args, **_kwargs: verified_run_row())
    assert launcher.validate_dispatch_details(response, run_id, authority()) == verified_run_row()


@pytest.mark.parametrize(
    "mutation",
    [
        lambda payload: payload.update(run_url="https://api.github.com/repos/evil/repo/actions/runs/12001"),
        lambda payload: payload.update(html_url="https://github.com/evil/repo/actions/runs/12001"),
        lambda payload: payload.update(extra="ambiguous"),
        lambda payload: payload.update(workflow_run_id=str(RUN_ID)),
    ],
)
def test_dispatch_details_reject_malformed_or_ambiguous_urls(
    mutation, monkeypatch: pytest.MonkeyPatch
) -> None:
    response = dispatch_response()
    mutation(response)
    monkeypatch.setattr(
        launcher,
        "gh_json",
        lambda *_args, **_kwargs: pytest.fail("invalid details must fail before run lookup"),
    )
    with pytest.raises(launcher.LaunchError):
        launcher.validate_dispatch_details(response, RUN_ID, authority())


def test_request_jit_config_keeps_bearer_out_of_payload(monkeypatch: pytest.MonkeyPatch) -> None:
    captured = {}
    encoded = encoded_jit_config()
    label = launcher.RUNNER_LABEL_PREFIX + NONCE

    def fake(endpoint, **kwargs):
        captured.update(endpoint=endpoint, **kwargs)
        return {
            "encoded_jit_config": encoded,
            "runner": {
                "id": 901,
                "name": launcher.RUNNER_NAME_PREFIX + NONCE,
                "labels": ["self-hosted", "linux", "x64", label],
            },
        }

    monkeypatch.setattr(launcher, "gh_json", fake)
    registration, seed = launcher.request_jit_config(NONCE)
    assert registration == launcher.RunnerRegistration(
        901,
        launcher.RUNNER_NAME_PREFIX + NONCE,
        frozenset(("self-hosted", "linux", "x64", label)),
    )
    assert encoded not in json.dumps(captured["payload"])
    assert captured["payload"]["runner_group_id"] == 1
    with tarfile.open(fileobj=io.BytesIO(seed.archive), mode="r:") as bundle:
        assert bundle.getnames() == [
            ".ownership-marker",
            ".runner",
            ".credentials",
            ".credentials_rsaparams",
        ]
        assert all(row.mode == 0o600 and row.uid == 1001 and row.gid == 1001 for row in bundle)


def test_request_jit_config_rejects_duplicate_labels_and_cleans_exact_registration(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    label = launcher.RUNNER_LABEL_PREFIX + NONCE
    monkeypatch.setattr(
        launcher,
        "gh_json",
        lambda *_args, **_kwargs: {
            "encoded_jit_config": encoded_jit_config(),
            "runner": {
                "id": 901,
                "name": launcher.RUNNER_NAME_PREFIX + NONCE,
                "labels": ["self-hosted", "linux", "x64", label, label],
            },
        },
    )
    monkeypatch.setattr(launcher, "recover_runner_registration", lambda *_args: None)
    with pytest.raises(launcher.LaunchError, match="duplicate labels"):
        launcher.request_jit_config(NONCE)


def test_materialize_secret_archive_is_stdin_only(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lease = config_lease()
    seed = launcher.canonicalize_jit_config(encoded_jit_config())
    calls: list[dict[str, object]] = []
    monkeypatch.setattr(launcher, "confirmed_config_lease", lambda *_args: (lease.holder, lease.volume))
    monkeypatch.setattr(launcher, "verify_seed_volume", lambda *_args: None)
    monkeypatch.setattr(
        launcher,
        "run_attached_container",
        lambda identity, **kwargs: calls.append({"identity": identity, **kwargs}),
    )
    launcher.materialize_config_lease(lease, seed, "sha256:" + "c" * 64)
    assert calls == [{
        "identity": lease.holder,
        "input_bytes": seed.archive,
        "timeout": 60,
        "label": "JIT seed materializer",
    }]
    assert b"synthetic" in seed.archive


def test_failed_child_command_never_reports_stdin_secret(monkeypatch: pytest.MonkeyPatch) -> None:
    class Result:
        returncode = 9
        stdout = "secret-output"
        stderr = "secret-error"

    monkeypatch.setattr(launcher.subprocess, "run", lambda *args, **kwargs: Result())
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.run_checked(
            ("docker", "version"), input_text="bearer-secret", kind="docker"
        )
    message = str(captured.value)
    assert "bearer-secret" not in message
    assert "secret-output" not in message
    assert "secret-error" not in message


def test_runner_command_has_exact_two_read_only_mounts_and_no_credential_argv(
    tmp_path: Path,
) -> None:
    root = (tmp_path / "candidate-input").resolve()
    root.mkdir()
    lease = config_lease()
    command = launcher.runner_docker_command(root, lease, NONCE)
    mounts = [command[index + 1] for index, item in enumerate(command) if item == "--mount"]
    assert len(mounts) == 2
    assert all("readonly" in mount for mount in mounts)
    assert str(root) in mounts[0]
    assert lease.volume.name in mounts[1]
    assert command[command.index("--user") + 1] == "1001:1001"
    assert "--env" not in command and "-e" not in command
    assert "/var/run/docker.sock" not in " ".join(command)
    assert "--jitconfig" not in " ".join(command)
    assert "encoded_jit_config" not in " ".join(command)
    assert "--cap-drop" in command and "ALL" in command
    assert launcher.RUNNER_ENTRYPOINT_COMMAND.endswith("exec /home/runner/run.sh")
    assert launcher.IMAGE in command


def test_validate_runner_container_rejects_extra_mount(tmp_path: Path) -> None:
    root = (tmp_path / "candidate-input").resolve()
    root.mkdir()
    lease = config_lease()
    image_id = "sha256:" + "c" * 64
    inspected = {
        "Image": image_id,
        "Config": {
            "Image": launcher.IMAGE,
            "User": "1001:1001",
            "Entrypoint": ["/bin/bash"],
            "Cmd": ["-c", launcher.RUNNER_ENTRYPOINT_COMMAND],
        },
        "HostConfig": {
            "Privileged": False,
            "AutoRemove": False,
            "ReadonlyRootfs": False,
            "NetworkMode": "bridge",
            "PidsLimit": 1024,
            "CapDrop": ["ALL"],
            "SecurityOpt": ["no-new-privileges:true"],
        },
        "Mounts": [
            {"Type": "bind", "Source": str(root), "Destination": "/candidate-input", "RW": False},
            {"Type": "volume", "Name": lease.volume.name, "Destination": "/jit-seed", "RW": False},
            {"Type": "bind", "Source": "/home", "Destination": "/host-home", "RW": False},
        ],
    }
    with pytest.raises(launcher.LaunchError, match="differs"):
        launcher.validate_runner_container(inspected, root, lease, NONCE, image_id)


def test_verify_docker_authority_asserts_digest_user_and_workdir(monkeypatch: pytest.MonkeyPatch) -> None:
    image = {
        "Id": "sha256:" + "c" * 64,
        "Architecture": "amd64",
        "Os": "linux",
        "RepoDigests": [launcher.IMAGE],
        "Config": {"User": "runner", "WorkingDir": "/home/runner"},
    }

    def fake(args, **_):
        command = list(args)
        if command[1:3] == ["context", "inspect"]:
            return json.dumps([{"Endpoints": {"docker": {"Host": "unix:///var/run/docker.sock"}}}])
        if command[1:3] == ["image", "inspect"]:
            return json.dumps([image])
        return ""

    monkeypatch.setattr(launcher, "run_checked", fake)
    assert launcher.verify_docker_authority() == image
    image["Config"]["User"] = "root"
    with pytest.raises(launcher.LaunchError, match="metadata differs"):
        launcher.verify_docker_authority()


def test_write_receipt_is_exclusive_mode_0600_and_contains_no_jit_config(tmp_path: Path) -> None:
    path = (tmp_path / "receipt.json").resolve()
    payload = {"status": "succeeded", "runnerImage": launcher.IMAGE}
    launcher.write_receipt(path, payload)
    assert json.loads(path.read_text()) == payload
    assert stat.S_IMODE(path.stat().st_mode) == 0o600
    assert "jit_config" not in path.read_text().lower()
    with pytest.raises(FileExistsError):
        launcher.write_receipt(path, payload)


def test_write_receipt_detects_parent_replacement_during_fsync(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    parent = tmp_path / "receipts"
    moved = tmp_path / "receipts-moved"
    parent.mkdir()
    original_fsync = launcher.os.fsync
    calls = 0

    def replace_parent(descriptor: int) -> None:
        nonlocal calls
        original_fsync(descriptor)
        calls += 1
        if calls == 1:
            parent.rename(moved)
            parent.mkdir()

    monkeypatch.setattr(launcher.os, "fsync", replace_parent)
    with pytest.raises(launcher.LaunchError, match="parent identity changed"):
        launcher.write_receipt((parent / "receipt.json").resolve(), {"status": "ok"})
    assert not (parent / "receipt.json").exists()
    assert (moved / "receipt.json").exists()


def test_private_tree_ignores_untrusted_tmpdir(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("TMPDIR", "/caller/controlled/path")
    identity = launcher.create_private_tree()
    try:
        assert identity.path.parent == launcher.SAFE_TEMP_PARENT
    finally:
        launcher.remove_private_tree(identity)


@pytest.mark.parametrize("source", ["/tmp/with,comma", "/tmp/with\nnewline"])
def test_mount_source_rejects_delimiter_or_control_before_docker(source: str) -> None:
    with pytest.raises(launcher.LaunchError, match="delimiter or control"):
        launcher.validate_mount_component(source, "mount source")


def test_nonzero_or_failed_docker_inventory_is_never_interpreted_as_absence(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    error = launcher.LaunchError("docker list failed")
    monkeypatch.setattr(
        launcher,
        "run_checked",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(error),
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.confirmed_volume(volume_identity())
    assert captured.value is error


def test_successful_empty_volume_list_is_confirmed_absence(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[tuple[str, ...]] = []

    def fake(args, **_kwargs):
        calls.append(tuple(args))
        return ""

    monkeypatch.setattr(launcher, "run_checked", fake)
    assert launcher.confirmed_volume(volume_identity()) is None
    assert calls == [("docker", "volume", "ls", "-q")]


def test_same_name_replacement_with_new_container_id_is_never_inspected(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    old = container_identity("b" * 64, launcher.CONTAINER_PREFIX + NONCE)
    replacement_id = "d" * 64
    calls: list[tuple[str, ...]] = []

    def fake(args, **_kwargs):
        command = tuple(args)
        calls.append(command)
        if command == (
            "docker", "container", "ls", "--all", "--no-trunc", "-q"
        ):
            return replacement_id + "\n"
        raise AssertionError("old exact ID must not be inspected")

    monkeypatch.setattr(launcher, "run_checked", fake)
    assert launcher.confirmed_container(old) is None
    assert calls == [
        ("docker", "container", "ls", "--all", "--no-trunc", "-q")
    ]


def test_container_acquisition_keyboard_interrupt_cleans_just_created_exact_id(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    identifier = "e" * 64
    name = launcher.CONFIG_HOLDER_PREFIX + NONCE
    primary = KeyboardInterrupt("operator")
    inventories = iter(([], [identifier]))
    inspected = {
        "Id": identifier,
        "Name": "/" + name,
        "Created": "2026-07-18T00:00:00Z",
        "Image": "sha256:" + "c" * 64,
        "Config": {
            "Image": launcher.IMAGE,
            "User": "0:0",
            "Labels": {
                launcher.OWNER_LABEL: "1",
                launcher.NONCE_LABEL: NONCE,
            },
            "Entrypoint": ["/bin/bash"],
            "Cmd": ["-c", "probe"],
        },
        "HostConfig": {"Mounts": []},
        "Mounts": [],
    }
    removed: list[tuple[object, dict[str, object]]] = []
    monkeypatch.setattr(launcher, "docker_container_ids", lambda: next(inventories))
    monkeypatch.setattr(launcher, "docker_inspect", lambda *_args: inspected)
    monkeypatch.setattr(launcher, "run_checked", lambda *_args, **_kwargs: identifier + "\n")
    monkeypatch.setattr(
        launcher,
        "remove_owned_container",
        lambda identity, **kwargs: removed.append((identity, kwargs)),
    )
    with pytest.raises(KeyboardInterrupt) as captured:
        launcher.create_owned_container(
            ["docker", "container", "create", "image"],
            name,
            NONCE,
            lambda _inspected: (_ for _ in ()).throw(primary),
            cleanup_volumes_on_failure=True,
        )
    assert captured.value is primary
    assert removed[0][0].identifier == identifier
    assert removed[0][1] == {"remove_volumes": True}


def test_seed_hash_mismatch_refuses_holder_and_volume_removal(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lease = config_lease()
    seed = launcher.canonicalize_jit_config(encoded_jit_config())
    monkeypatch.setattr(
        launcher,
        "confirmed_config_lease",
        lambda *_args: (lease.holder, lease.volume),
    )
    mismatch = launcher.LaunchError("seed hash mismatch")
    monkeypatch.setattr(
        launcher,
        "verify_seed_volume",
        lambda *_args: (_ for _ in ()).throw(mismatch),
    )
    monkeypatch.setattr(
        launcher,
        "remove_owned_container",
        lambda *_args, **_kwargs: pytest.fail("mismatched seed must not be removed"),
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.remove_config_lease(lease, seed, "sha256:" + "c" * 64)
    assert captured.value is mismatch


def test_config_lease_cleanup_removes_exact_holder_with_volumes(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lease = config_lease()
    seed = launcher.canonicalize_jit_config(encoded_jit_config())
    calls: list[tuple[object, dict[str, object]]] = []
    states = iter(((lease.holder, lease.volume), (lease.holder, lease.volume)))
    monkeypatch.setattr(launcher, "confirmed_config_lease", lambda *_args: next(states))
    monkeypatch.setattr(launcher, "verify_seed_volume", lambda *_args: None)
    monkeypatch.setattr(
        launcher,
        "remove_owned_container",
        lambda identity, **kwargs: calls.append((identity, kwargs)),
    )
    monkeypatch.setattr(launcher, "docker_container_ids", lambda: [])
    monkeypatch.setattr(launcher, "docker_volume_names", lambda: [])
    launcher.remove_config_lease(lease, seed, "sha256:" + "c" * 64)
    assert calls == [(
        lease.holder,
        {
            "remove_volumes": True,
            "expected_volumes": frozenset((lease.volume.name,)),
        },
    )]


def test_attached_timeout_terminates_kills_and_communicates_to_reap(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    events: list[str] = []

    class Process:
        returncode = None
        calls = 0

        def communicate(self, *args, **kwargs):
            self.calls += 1
            events.append(f"communicate_{self.calls}")
            if self.calls < 3:
                raise subprocess.TimeoutExpired("docker", 1)
            self.returncode = -9
            return b"", b""

        def terminate(self):
            events.append("terminate")

        def kill(self):
            events.append("kill")

    process = Process()
    monkeypatch.setattr(launcher.subprocess, "Popen", lambda *args, **kwargs: process)
    with pytest.raises(launcher.LaunchError, match="bounded wait"):
        launcher.run_attached_container(
            container_identity(), input_bytes=b"synthetic", timeout=1, label="probe"
        )
    assert events == ["communicate_1", "terminate", "communicate_2", "kill", "communicate_3"]


def test_attached_keyboard_interrupt_preserves_identical_primary_after_reap(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    primary = KeyboardInterrupt("operator")
    events: list[str] = []

    class Process:
        returncode = None
        calls = 0

        def communicate(self, *args, **kwargs):
            self.calls += 1
            events.append(f"communicate_{self.calls}")
            if self.calls == 1:
                raise primary
            self.returncode = -15
            return b"", b""

        def terminate(self):
            events.append("terminate")

        def kill(self):
            events.append("kill")

    monkeypatch.setattr(launcher.subprocess, "Popen", lambda *args, **kwargs: Process())
    with pytest.raises(KeyboardInterrupt) as captured:
        launcher.run_attached_container(
            container_identity(), input_bytes=None, timeout=1, label="probe"
        )
    assert captured.value is primary
    assert events == ["communicate_1", "terminate", "communicate_2"]


def test_parse_args_requires_absolute_paths_and_bounded_timeout(tmp_path: Path) -> None:
    with pytest.raises(SystemExit):
        launcher.parse_args(["--prepared-stage-root", "relative", "--receipt-output", str(tmp_path / "r")])
    with pytest.raises(SystemExit):
        launcher.parse_args(["--prepared-stage-root", str(tmp_path), "--receipt-output", str(tmp_path / "r"), "--timeout-seconds", "59"])
    args = launcher.parse_args(["--prepared-stage-root", str(tmp_path), "--receipt-output", str(tmp_path / "r")])
    assert args.timeout_seconds == 1800


@pytest.mark.parametrize("value", ["not-a-digest", "sha256:" + "A" * 64, " sha256:" + "a" * 64])
def test_artifact_digest_rejects_noncanonical_values(value: str) -> None:
    with pytest.raises(launcher.LaunchError):
        launcher.artifact_digest({"digest": value})


def test_workflow_run_inventory_paginates_with_exact_total_count(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[str] = []

    def fake(endpoint: str, **_kwargs):
        calls.append(endpoint)
        page = 1 if endpoint.endswith("page=1") else 2
        rows = [{"id": value} for value in (
            range(1, 101) if page == 1 else (101,)
        )]
        return {"total_count": 101, "workflow_runs": rows}

    monkeypatch.setattr(launcher, "gh_json", fake)
    assert len(launcher.workflow_run_inventory()) == 101
    assert calls[0].endswith("per_page=100&page=1")
    assert calls[1].endswith("per_page=100&page=2")


def test_indeterminate_dispatch_reconciliation_accepts_one_exact_postbaseline_run(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    row = verified_run_row()
    monkeypatch.setattr(launcher, "workflow_run_inventory", lambda: [row])
    monkeypatch.setattr(launcher, "gh_json", lambda *_args, **_kwargs: row)
    monkeypatch.setattr(launcher, "wait_for_correlated_run", lambda *_args: row)
    run_id, correlated = launcher.reconcile_indeterminate_dispatch(
        frozenset((RUN_ID - 1,)),
        authority(),
        NONCE,
        launcher.time.monotonic() + 10,
    )
    assert run_id == RUN_ID
    assert correlated is row


@pytest.mark.parametrize("count", [0, 2])
def test_indeterminate_dispatch_reconciliation_zero_or_ambiguous_is_manual_blocker(
    monkeypatch: pytest.MonkeyPatch, count: int
) -> None:
    rows = [verified_run_row(RUN_ID + offset) for offset in range(count)]
    monkeypatch.setattr(launcher, "workflow_run_inventory", lambda: rows)
    if count == 0:
        clock = iter((0.0, 61.0))
        monkeypatch.setattr(launcher.time, "monotonic", lambda: next(clock))
        deadline = 1000.0
    else:
        deadline = launcher.time.monotonic() + 10
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.reconcile_indeterminate_dispatch(
            frozenset(), authority(), NONCE, deadline
        )
    assert any(
        note.startswith(launcher.MANUAL_CLEANUP_NOTE_PREFIX)
        for note in captured.value.__notes__
    )


def arrange_orchestrate_correlation_failure(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    primary: BaseException,
    *,
    cleanup_failures: set[str] | None = None,
) -> tuple[argparse.Namespace, list[str]]:
    cleanup_failures = cleanup_failures or set()
    stage = tmp_path / "stage"
    receipts = tmp_path / "receipts"
    private_path = tmp_path / "private"
    stage.mkdir()
    receipts.mkdir()
    private_path.mkdir()
    args = argparse.Namespace(
        prepared_stage_root=stage.resolve(),
        receipt_output=(receipts / "receipt.json").resolve(),
        timeout_seconds=60,
    )
    events: list[str] = []
    private = launcher.PrivateTree(
        private_path.resolve(),
        private_path.stat().st_dev,
        private_path.stat().st_ino,
        private_path.stat().st_uid,
    )
    candidate = launcher.CandidateIdentity(
        (private_path / "candidate-input").resolve(), VERSION, "b" * 64, ()
    )

    monkeypatch.setattr(
        launcher,
        "verify_committed_local_authority",
        lambda _root: launcher.LocalAuthority(SOURCE_SHA, b"trusted exporter"),
    )
    monkeypatch.setattr(launcher, "validate_remote_authority", lambda _sha: authority())
    monkeypatch.setattr(
        launcher,
        "verify_docker_authority",
        lambda: {"Id": "sha256:" + "c" * 64},
    )
    monkeypatch.setattr(launcher, "load_trusted_exporter", lambda _source: object())
    monkeypatch.setattr(launcher, "create_private_tree", lambda: private)
    monkeypatch.setattr(
        launcher, "materialize_candidate_subset", lambda *_args: candidate
    )
    monkeypatch.setattr(launcher, "list_repository_runners", lambda: [])
    monkeypatch.setattr(launcher, "generate_unique_nonce", lambda _runners: NONCE)
    monkeypatch.setattr(launcher, "workflow_run_baseline", lambda: frozenset((100,)))
    monkeypatch.setattr(
        launcher, "dispatch_workflow", lambda *_args: dispatch_response()
    )
    monkeypatch.setattr(
        launcher, "validate_dispatch_details", lambda *_args: verified_run_row()
    )

    def correlation(*_args):
        events.append("correlate")
        raise primary

    monkeypatch.setattr(launcher, "wait_for_correlated_run", correlation)

    def cleanup(operation: str):
        events.append(operation)
        if operation in cleanup_failures:
            raise launcher.LaunchError(
                f"{operation} bearer-token=never-print-this"
            )

    monkeypatch.setattr(launcher, "recover_created_container", lambda *_args: None)
    monkeypatch.setattr(launcher, "recover_runner_registration", lambda *_args: None)
    monkeypatch.setattr(launcher, "recover_config_lease", lambda *_args: None)
    monkeypatch.setattr(
        launcher,
        "cancel_owned_run",
        lambda run_id, _authority: (
            events.append(f"cancel_run_id={run_id}"),
            cleanup("cancel_workflow"),
        ),
    )
    monkeypatch.setattr(
        launcher, "remove_private_tree", lambda _private: cleanup("remove_private_tree")
    )
    return args, events


@pytest.mark.parametrize(
    "message",
    [
        "timed out waiting for exact workflow/job correlation",
        "workflow run inventory is invalid",
        "multiple workflow runs claimed the unique runner label",
    ],
)
def test_post_dispatch_correlation_failures_cancel_returned_exact_run(
    message: str, monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    primary = launcher.LaunchError(message)
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, primary
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    assert events.index("correlate") < events.index(f"cancel_run_id={RUN_ID}")
    assert "cancel_workflow" in events


def test_post_dispatch_interruption_still_cancels_and_preserves_interruption(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    primary = KeyboardInterrupt("operator interruption")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, primary
    )
    with pytest.raises(KeyboardInterrupt) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    assert f"cancel_run_id={RUN_ID}" in events
    assert "cancel_workflow" in events


def test_reconciled_response_loss_arms_only_exact_get_validated_run(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    unused = launcher.LaunchError("normal correlation unused")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, unused
    )
    monkeypatch.setattr(
        launcher,
        "dispatch_workflow",
        lambda *_args: (_ for _ in ()).throw(
            launcher.DispatchIndeterminate("response lost")
        ),
    )
    monkeypatch.setattr(
        launcher,
        "reconcile_indeterminate_dispatch",
        lambda *_args: (RUN_ID, verified_run_row()),
    )
    primary = launcher.LaunchError("stop after exact reconciliation")
    monkeypatch.setattr(
        launcher,
        "request_jit_config",
        lambda *_args: (_ for _ in ()).throw(primary),
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    assert f"cancel_run_id={RUN_ID}" in events


def test_ambiguous_response_loss_never_arms_cancellation(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    unused = launcher.LaunchError("normal correlation unused")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, unused
    )
    monkeypatch.setattr(
        launcher,
        "dispatch_workflow",
        lambda *_args: (_ for _ in ()).throw(
            launcher.DispatchIndeterminate("response lost")
        ),
    )
    primary = launcher.LaunchError("ambiguous response-loss reconciliation")
    launcher.add_manual_cleanup_note(primary, "inspect nonce-bound runs")
    monkeypatch.setattr(
        launcher,
        "reconcile_indeterminate_dispatch",
        lambda *_args: (_ for _ in ()).throw(primary),
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    assert not any(event.startswith("cancel_run_id=") for event in events)


def test_cancellation_failure_does_not_mask_primary_and_redacts_cleanup_secret(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    primary = launcher.LaunchError("primary correlation failure")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch,
        tmp_path,
        primary,
        cleanup_failures={"cancel_workflow"},
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    notes = getattr(captured.value, "__notes__", [])
    assert len(notes) == 1
    assert "cancel_workflow=LaunchError" in notes[0]
    assert "never-print-this" not in notes[0]
    assert "remove_private_tree" in events


def test_cleanup_failures_are_bounded_aggregated_redacted_notes_on_primary(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    primary = launcher.LaunchError("primary remains primary")
    args, _events = arrange_orchestrate_correlation_failure(
        monkeypatch,
        tmp_path,
        primary,
        cleanup_failures={"cancel_workflow", "remove_private_tree"},
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    note = captured.value.__notes__[0]
    assert len(note) <= 512
    assert "cancel_workflow=LaunchError" in note
    assert "remove_private_tree=LaunchError" in note
    assert "bearer-token" not in note
    assert "never-print-this" not in note


def test_malformed_dispatch_urls_do_not_arm_arbitrary_run_cancellation(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    primary = launcher.LaunchError("correlation must not be reached")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, primary
    )
    malformed = dispatch_response()
    malformed["run_url"] = (
        f"https://api.github.com/repos/{launcher.REPOSITORY}/actions/runs/999999"
    )
    monkeypatch.setattr(
        launcher, "dispatch_workflow", lambda *_args: malformed
    )
    with pytest.raises(launcher.LaunchError, match="mismatched run URLs"):
        launcher.orchestrate(args)
    assert "correlate" not in events
    assert not any(event.startswith("cancel_run_id=") for event in events)
    assert "cancel_workflow" not in events


def test_immediate_run_identity_lookup_failure_never_arms_cancellation(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    unused_correlation_error = launcher.LaunchError("correlation not reached")
    args, events = arrange_orchestrate_correlation_failure(
        monkeypatch, tmp_path, unused_correlation_error
    )
    lookup_error = launcher.LaunchError("dispatched run identity lookup failed")
    monkeypatch.setattr(
        launcher,
        "validate_dispatch_details",
        lambda *_args: (_ for _ in ()).throw(lookup_error),
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is lookup_error
    assert "correlate" not in events
    assert f"cancel_run_id={RUN_ID}" not in events
    assert "cancel_workflow" not in events
    assert any(
        note.startswith(launcher.MANUAL_CLEANUP_NOTE_PREFIX)
        for note in captured.value.__notes__
    )


def test_cancel_owned_run_posts_directly_to_persisted_exact_run(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls = []
    monkeypatch.setattr(
        launcher,
        "gh_json",
        lambda endpoint, **kwargs: calls.append((endpoint, kwargs)),
    )
    launcher.cancel_owned_run(RUN_ID, authority())
    assert calls == [
        (
            f"repos/{launcher.REPOSITORY}/actions/runs/{RUN_ID}/cancel",
            {"method": "POST"},
        )
    ]


def test_main_reports_only_redacted_cleanup_note(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    primary = launcher.LaunchError("primary failure")
    primary.add_note(
        launcher.cleanup_failure_note(
            [("cancel_workflow", launcher.LaunchError("secret=do-not-print"))]
        )
    )
    monkeypatch.setattr(launcher, "orchestrate", lambda _args: (_ for _ in ()).throw(primary))
    result = launcher.main(
        [
            "--prepared-stage-root", str(tmp_path.resolve()),
            "--receipt-output", str((tmp_path / "receipt.json").resolve()),
        ]
    )
    captured = capsys.readouterr()
    assert result == 1
    assert "primary failure" in captured.err
    assert "cancel_workflow=LaunchError" in captured.err
    assert "do-not-print" not in captured.err


@pytest.mark.parametrize("signal_number", [signal.SIGTERM, signal.SIGHUP])
def test_main_catchable_termination_returns_signal_status_and_restores_handlers(
    signal_number: int,
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    previous_term = signal.getsignal(signal.SIGTERM)
    previous_hup = signal.getsignal(signal.SIGHUP)
    primary = launcher.GovernedTermination(signal_number)
    monkeypatch.setattr(
        launcher,
        "orchestrate",
        lambda _args: (_ for _ in ()).throw(primary),
    )
    result = launcher.main([
        "--prepared-stage-root", str(tmp_path.resolve()),
        "--receipt-output", str((tmp_path / "receipt.json").resolve()),
    ])
    assert result == 128 + signal_number
    assert signal.getsignal(signal.SIGTERM) == previous_term
    assert signal.getsignal(signal.SIGHUP) == previous_hup
    assert signal.Signals(signal_number).name in capsys.readouterr().err


def test_shell_entrypoint_and_docs_pin_governed_launcher() -> None:
    shell = (REPO_ROOT / "scripts" / "run-preview-nightly-jit-launcher.sh").read_text()
    docs = (REPO_ROOT / "docs" / "preview-nightly-jit-launcher.md").read_text()
    assert "preview_nightly_jit_launcher.py" in shell
    assert "GH_TOKEN" not in shell and "docker.sock" not in shell
    assert launcher.IMAGE_DIGEST in docs
    assert "mode 0600" in docs
