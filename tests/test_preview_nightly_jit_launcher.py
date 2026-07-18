from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import stat
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
VERSION = "preview-20260718.1"
SOURCE_SHA = "a" * 40
NONCE = "abcdefghijklmnopqrstuvwx"


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


def test_child_environments_strip_credentials(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("GH_TOKEN", "secret")
    monkeypatch.setenv("GITHUB_TOKEN", "secret")
    monkeypatch.setenv("DOCKER_CONTEXT", "default")
    assert launcher.command_environment("gh")["GH_TOKEN"] == "secret"
    assert "GH_TOKEN" not in launcher.command_environment("docker")
    assert "GITHUB_TOKEN" not in launcher.command_environment("docker")
    assert "GH_TOKEN" not in launcher.command_environment("local")


@pytest.mark.parametrize(
    "endpoint",
    ["repos/evil/repo", "repos/ArchonMegalon/chummer6-ui/../evil", "https://example.test"],
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


def test_run_label_correlation_distinguishes_concurrent_dispatches(monkeypatch: pytest.MonkeyPatch) -> None:
    expected = launcher.RUNNER_LABEL_PREFIX + NONCE

    def jobs(run_id: int):
        suffix = NONCE if run_id == 102 else "z" * 24
        return [{"id": run_id * 10, "name": launcher.EXPORT_JOB_NAME, "labels": ["self-hosted", "linux", "x64", launcher.RUNNER_LABEL_PREFIX + suffix]}]

    monkeypatch.setattr(launcher, "run_jobs", jobs)
    assert launcher.run_has_exact_export_label(run_row(102), expected)
    assert not launcher.run_has_exact_export_label(run_row(103), expected)


def test_run_label_correlation_rejects_duplicate_export_jobs(monkeypatch: pytest.MonkeyPatch) -> None:
    label = launcher.RUNNER_LABEL_PREFIX + NONCE
    job = {"id": 1001, "name": launcher.EXPORT_JOB_NAME, "labels": ["self-hosted", "linux", "x64", label]}
    monkeypatch.setattr(launcher, "run_jobs", lambda _: [job, dict(job)])
    with pytest.raises(launcher.LaunchError, match="multiple"):
        launcher.run_has_exact_export_label(run_row(), label)


def test_dispatch_uses_only_fixed_workflow_and_exact_inputs(monkeypatch: pytest.MonkeyPatch) -> None:
    captured = {}
    monkeypatch.setattr(launcher, "gh_json", lambda endpoint, **kwargs: captured.update(endpoint=endpoint, **kwargs))
    candidate = launcher.CandidateIdentity(Path("/candidate"), VERSION, "b" * 64, ())
    launcher.dispatch_workflow(candidate, authority(), NONCE)
    assert captured["endpoint"].endswith("/preview-nightly-candidate-export.yml/dispatches")
    assert captured["method"] == "POST"
    assert captured["payload"] == {
        "ref": "main",
        "inputs": {
            "runner_nonce": NONCE,
            "candidate_version": VERSION,
            "candidate_manifest_sha256": "b" * 64,
            "expected_source_sha": SOURCE_SHA,
            "export_confirmed": True,
        },
    }


def test_request_jit_config_keeps_bearer_out_of_payload(monkeypatch: pytest.MonkeyPatch) -> None:
    captured = {}

    def fake(endpoint, **kwargs):
        captured.update(endpoint=endpoint, **kwargs)
        return {"encoded_jit_config": "A" * 120, "runner": {"name": launcher.RUNNER_NAME_PREFIX + NONCE}}

    monkeypatch.setattr(launcher, "gh_json", fake)
    name, encoded = launcher.request_jit_config(NONCE)
    assert name == launcher.RUNNER_NAME_PREFIX + NONCE
    assert encoded == "A" * 120
    assert "A" * 120 not in json.dumps(captured["payload"])
    assert captured["payload"]["runner_group_id"] == 1


def test_config_volume_sends_secret_only_on_default_runner_stdin(monkeypatch: pytest.MonkeyPatch) -> None:
    calls = []
    volume = launcher.CONFIG_VOLUME_PREFIX + NONCE
    monkeypatch.setattr(launcher, "docker_optional_inspect", lambda *_: None)

    def fake(args, **kwargs):
        calls.append((list(args), kwargs))
        if list(args)[:3] == ["docker", "volume", "create"]:
            return volume + "\n"
        return ""

    monkeypatch.setattr(launcher, "run_checked", fake)
    assert launcher.create_config_volume(NONCE, "S" * 120) == volume
    secret_calls = [call for call in calls if call[1].get("input_text")]
    assert len(secret_calls) == 1
    command, kwargs = secret_calls[0]
    assert kwargs["input_text"] == "S" * 120 + "\n"
    assert "S" * 120 not in " ".join(command)
    assert "--user" not in command
    assert "--interactive" in command
    assert "--network" in command and "none" in command


def test_config_volume_failure_removes_only_exact_labeled_volume(monkeypatch: pytest.MonkeyPatch) -> None:
    volume = launcher.CONFIG_VOLUME_PREFIX + NONCE
    removed = []
    inspections = 0

    def inspect(kind: str, name: str):
        nonlocal inspections
        if kind == "volume" and name == volume:
            inspections += 1
            if inspections == 1:
                return None
            return {
                "Name": volume,
                "Labels": {launcher.OWNER_LABEL: "1", launcher.NONCE_LABEL: NONCE},
            }
        return None

    def fake(args, **kwargs):
        command = list(args)
        if command[:3] == ["docker", "volume", "create"]:
            return volume + "\n"
        if command[:3] == ["docker", "volume", "rm"]:
            removed.append(command[-1])
            return volume + "\n"
        if kwargs.get("input_text"):
            raise launcher.LaunchError("redacted writer failure")
        return ""

    monkeypatch.setattr(launcher, "docker_optional_inspect", inspect)
    monkeypatch.setattr(launcher, "run_checked", fake)
    with pytest.raises(launcher.LaunchError, match="redacted writer failure"):
        launcher.create_config_volume(NONCE, "S" * 120)
    assert removed == [volume]


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


def test_runner_command_has_exact_two_read_only_mounts_and_no_credential_env() -> None:
    root = Path("/private/candidate-input")
    volume = launcher.CONFIG_VOLUME_PREFIX + NONCE
    command = launcher.runner_docker_command(root, volume, NONCE)
    mounts = [command[index + 1] for index, item in enumerate(command) if item == "--mount"]
    assert len(mounts) == 2
    assert all("readonly" in mount for mount in mounts)
    assert str(root) in mounts[0]
    assert volume in mounts[1]
    assert "--user" not in command
    assert "--env" not in command and "-e" not in command
    assert "/var/run/docker.sock" not in " ".join(command)
    assert "--cap-drop" in command and "ALL" in command
    assert launcher.IMAGE in command


def test_validate_running_container_rejects_extra_mount() -> None:
    root = Path("/private/candidate-input")
    volume = launcher.CONFIG_VOLUME_PREFIX + NONCE
    inspected = {
        "Name": "/" + launcher.CONTAINER_PREFIX + NONCE,
        "Config": {"Image": launcher.IMAGE, "User": "runner", "Labels": {launcher.OWNER_LABEL: "1", launcher.NONCE_LABEL: NONCE}},
        "HostConfig": {"Privileged": False, "CapDrop": ["ALL"], "SecurityOpt": ["no-new-privileges:true"]},
        "Mounts": [
            {"Type": "bind", "Source": str(root), "Destination": "/candidate-input", "RW": False},
            {"Type": "volume", "Name": volume, "Destination": "/jit-config", "RW": False},
            {"Type": "bind", "Source": "/home", "Destination": "/host-home", "RW": False},
        ],
    }
    with pytest.raises(launcher.LaunchError, match="differs"):
        launcher.validate_running_container(inspected, root, volume, NONCE)


def test_verify_docker_authority_asserts_digest_user_and_workdir(monkeypatch: pytest.MonkeyPatch) -> None:
    image = {
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


def test_shell_entrypoint_and_docs_pin_governed_launcher() -> None:
    shell = (REPO_ROOT / "scripts" / "run-preview-nightly-jit-launcher.sh").read_text()
    docs = (REPO_ROOT / "docs" / "preview-nightly-jit-launcher.md").read_text()
    assert "preview_nightly_jit_launcher.py" in shell
    assert "GH_TOKEN" not in shell and "docker.sock" not in shell
    assert launcher.IMAGE_DIGEST in docs
    assert "mode 0600" in docs
