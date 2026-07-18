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
        }
    )
    return row


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


def test_wait_correlation_rejects_two_runs_claiming_exact_label(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        launcher, "workflow_runs", lambda: [run_row(RUN_ID), run_row(RUN_ID + 1)]
    )
    monkeypatch.setattr(
        launcher, "run_has_exact_export_label", lambda _run, _label: True
    )
    with pytest.raises(launcher.LaunchError, match="multiple workflow runs"):
        launcher.wait_for_correlated_run(
            RUN_ID,
            authority(),
            launcher.RUNNER_LABEL_PREFIX + NONCE,
            launcher.time.monotonic() + 10,
        )


def test_wait_correlation_propagates_malformed_inventory(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    malformed = launcher.LaunchError("workflow run inventory is invalid")
    monkeypatch.setattr(
        launcher, "workflow_runs", lambda: (_ for _ in ()).throw(malformed)
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.wait_for_correlated_run(
            RUN_ID,
            authority(),
            launcher.RUNNER_LABEL_PREFIX + NONCE,
            launcher.time.monotonic() + 10,
        )
    assert captured.value is malformed


def test_wait_correlation_timeout_is_bounded(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(launcher.time, "monotonic", lambda: 100.0)
    with pytest.raises(launcher.LaunchError, match="timed out"):
        launcher.wait_for_correlated_run(
            RUN_ID,
            authority(),
            launcher.RUNNER_LABEL_PREFIX + NONCE,
            100.0,
        )


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

    monkeypatch.setattr(launcher, "verify_committed_local_authority", lambda _root: SOURCE_SHA)
    monkeypatch.setattr(launcher, "validate_remote_authority", lambda _sha: authority())
    monkeypatch.setattr(launcher, "verify_docker_authority", lambda: {})
    monkeypatch.setattr(launcher, "load_trusted_exporter", lambda _root: object())
    monkeypatch.setattr(launcher, "create_private_tree", lambda: private)
    monkeypatch.setattr(
        launcher, "materialize_candidate_subset", lambda *_args: candidate
    )
    monkeypatch.setattr(launcher, "list_repository_runners", lambda: [])
    monkeypatch.setattr(launcher, "generate_unique_nonce", lambda _runners: NONCE)
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

    monkeypatch.setattr(
        launcher, "stop_owned_container", lambda _nonce: cleanup("stop_container")
    )
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
        cleanup_failures={"stop_container", "cancel_workflow", "remove_private_tree"},
    )
    with pytest.raises(launcher.LaunchError) as captured:
        launcher.orchestrate(args)
    assert captured.value is primary
    note = captured.value.__notes__[0]
    assert len(note) <= 512
    assert "stop_container=LaunchError" in note
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


def test_immediate_run_identity_lookup_failure_cancels_persisted_dispatch_id(
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
    assert f"cancel_run_id={RUN_ID}" in events
    assert "cancel_workflow" in events


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


def test_shell_entrypoint_and_docs_pin_governed_launcher() -> None:
    shell = (REPO_ROOT / "scripts" / "run-preview-nightly-jit-launcher.sh").read_text()
    docs = (REPO_ROOT / "docs" / "preview-nightly-jit-launcher.md").read_text()
    assert "preview_nightly_jit_launcher.py" in shell
    assert "GH_TOKEN" not in shell and "docker.sock" not in shell
    assert launcher.IMAGE_DIGEST in docs
    assert "mode 0600" in docs
