from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
from pathlib import Path

import pytest
import yaml


REPO_ROOT = Path(__file__).resolve().parents[1]
VERSION = "preview-20260718.1"
SOURCE_SHA = "a" * 40


def load_export_module():
    path = REPO_ROOT / "scripts" / "preview_nightly_candidate_export.py"
    spec = importlib.util.spec_from_file_location("preview_nightly_candidate_export", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


candidate_export = load_export_module()


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def make_fixture(
    root: Path,
    *,
    run_id: str = "12000",
    run_attempt: str = "1",
    actor: str = "capture-operator",
) -> tuple[Path, argparse.Namespace]:
    input_root = root / "candidate-input"
    files = input_root / "files"
    files.mkdir(parents=True)
    rows: list[dict[str, object]] = []
    for index, head in enumerate(candidate_export.HEADS, start=1):
        installer_relative = candidate_export.installer_path(head)
        payload_relative = candidate_export.payload_path(head)
        installer = input_root / installer_relative
        payload = input_root / payload_relative
        installer.write_bytes(b"MZ" + bytes([index]) * 1024)
        payload.write_bytes(b"PK" + bytes([index + 10]) * 2048)
        rows.append(
            {
                "artifactId": f"{head}-win-x64-installer",
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
    manifest = input_root / candidate_export.MANIFEST_PATH
    write_json(
        manifest,
        {
            "contractName": "Chummer.Hub.Registry.Contracts",
            "contract_name": "Chummer.Hub.Registry.Contracts",
            "schemaVersion": 1,
            "version": VERSION,
            "releaseVersion": VERSION,
            "channelId": "preview",
            "channel": "preview",
            "artifacts": rows,
        },
    )
    args = argparse.Namespace(
        input_root=input_root.resolve(),
        output_root=(root / "candidate-output").resolve(),
        expected_version=VERSION,
        expected_manifest_sha256=sha256(manifest),
        source_repository="ArchonMegalon/chummer6-ui",
        source_workflow=candidate_export.PRODUCER_WORKFLOW,
        source_run_id=run_id,
        source_run_attempt=run_attempt,
        source_ref=candidate_export.PRODUCER_REF,
        source_sha=SOURCE_SHA,
        expected_source_sha=SOURCE_SHA,
        source_actor=actor,
        artifact_name=f"preview-nightly-candidate-{run_id}-{run_attempt}",
        runner_nonce="abcdefghijkl",
        require_read_only_input=False,
    )
    return input_root, args


def rewrite_manifest(input_root: Path, args: argparse.Namespace, mutation) -> dict[str, object]:
    path = input_root / candidate_export.MANIFEST_PATH
    payload = json.loads(path.read_text(encoding="utf-8"))
    mutation(payload)
    write_json(path, payload)
    args.expected_manifest_sha256 = sha256(path)
    return payload


def test_export_emits_exact_seven_file_artifact_and_bound_receipt(tmp_path: Path) -> None:
    _, args = make_fixture(tmp_path)
    inventory_sha = candidate_export.export_candidate(args)
    output = args.output_root

    assert candidate_export.exact_regular_files(output, "test output") == sorted(
        candidate_export.OUTPUT_PATHS
    )
    inventory_path = output / candidate_export.CONTENT_INVENTORY_PATH
    receipt_path = output / candidate_export.EXPORT_RECEIPT_PATH
    assert inventory_sha == sha256(inventory_path)
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert inventory == {
        "contractName": candidate_export.CONTENT_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "release": {"channel": "preview", "version": VERSION},
        "manifest": {
            "path": candidate_export.MANIFEST_PATH,
            "sha256": args.expected_manifest_sha256,
        },
        "files": candidate_export.content_rows(output),
    }
    assert [row["path"] for row in inventory["files"]] == sorted(candidate_export.CONTENT_PATHS)
    assert receipt["contractName"] == candidate_export.EXPORT_CONTRACT
    assert receipt["status"] == "exported"
    assert receipt["contentInventory"] == {
        "path": candidate_export.CONTENT_INVENTORY_PATH,
        "sha256": inventory_sha,
    }
    assert receipt["source"] == {
        "repository": "ArchonMegalon/chummer6-ui",
        "workflow": candidate_export.PRODUCER_WORKFLOW,
        "runId": "12000",
        "runAttempt": "1",
        "ref": candidate_export.PRODUCER_REF,
        "sha": SOURCE_SHA,
        "actor": "capture-operator",
        "artifactName": "preview-nightly-candidate-12000-1",
        "runnerLabel": "chummer-preview-nightly-export-abcdefghijkl",
    }
    assert [row["headId"] for row in receipt["heads"]] == list(candidate_export.HEADS)


def test_content_inventory_is_reproducible_across_export_runs(tmp_path: Path) -> None:
    _, first = make_fixture(tmp_path / "first", run_id="12000", actor="first-operator")
    _, second = make_fixture(
        tmp_path / "second", run_id="98765", run_attempt="3", actor="second-operator"
    )
    first.runner_nonce = "abcdefghijkl"
    second.runner_nonce = "mnopqrstuvwx"

    first_sha = candidate_export.export_candidate(first)
    second_sha = candidate_export.export_candidate(second)

    assert first_sha == second_sha
    assert (
        first.output_root / candidate_export.CONTENT_INVENTORY_PATH
    ).read_bytes() == (
        second.output_root / candidate_export.CONTENT_INVENTORY_PATH
    ).read_bytes()
    assert (
        first.output_root / candidate_export.EXPORT_RECEIPT_PATH
    ).read_bytes() != (
        second.output_root / candidate_export.EXPORT_RECEIPT_PATH
    ).read_bytes()


def test_cli_emits_the_single_github_output_binding(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _, args = make_fixture(tmp_path)
    result = candidate_export.main(
        [
            "--input-root",
            str(args.input_root),
            "--output-root",
            str(args.output_root),
            "--expected-version",
            args.expected_version,
            "--expected-manifest-sha256",
            args.expected_manifest_sha256,
            "--source-repository",
            args.source_repository,
            "--source-workflow",
            args.source_workflow,
            "--source-run-id",
            args.source_run_id,
            "--source-run-attempt",
            args.source_run_attempt,
            "--source-ref",
            args.source_ref,
            "--source-sha",
            args.source_sha,
            "--expected-source-sha",
            args.expected_source_sha,
            "--source-actor",
            args.source_actor,
            "--artifact-name",
            args.artifact_name,
            "--runner-nonce",
            args.runner_nonce,
        ]
    )
    assert result == 0
    output = capsys.readouterr().out.strip()
    assert output == (
        "content_inventory_sha256="
        + sha256(args.output_root / candidate_export.CONTENT_INVENTORY_PATH)
    )


@pytest.mark.parametrize("mutation", ["extra", "missing", "symlink", "installer", "manifest-sha"])
def test_export_rejects_non_exact_or_tampered_input_tree(tmp_path: Path, mutation: str) -> None:
    input_root, args = make_fixture(tmp_path)
    if mutation == "extra":
        (input_root / "unexpected.txt").write_text("unexpected\n", encoding="utf-8")
    elif mutation == "missing":
        (input_root / candidate_export.payload_path("avalonia")).unlink()
    elif mutation == "symlink":
        target = input_root / candidate_export.payload_path("avalonia")
        target.unlink()
        target.symlink_to(input_root / candidate_export.payload_path("blazor-desktop"))
    elif mutation == "installer":
        (input_root / candidate_export.installer_path("avalonia")).write_bytes(b"tampered")
    else:
        args.expected_manifest_sha256 = "0" * 64

    with pytest.raises(candidate_export.ContractError):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


@pytest.mark.parametrize(
    "mutation",
    [
        "contract",
        "contract-alias",
        "contract-alias-null",
        "schema",
        "channel",
        "channel-alias",
        "channel-missing",
        "version",
        "version-alias",
        "version-missing",
        "head-alias",
        "head-empty",
        "head-missing",
        "platform-case",
        "rid-padding",
        "kind-case",
        "mode",
        "mode-case",
        "payload-mode-case",
        "filename-case",
        "size",
        "duplicate-head",
        "digest-alias",
    ],
)
def test_export_rejects_manifest_or_head_contract_drift(tmp_path: Path, mutation: str) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        if mutation == "contract":
            payload["contractName"] = "caller-selected-contract"
        elif mutation == "contract-alias":
            payload["contract_name"] = "caller-selected-contract"
        elif mutation == "contract-alias-null":
            payload["contract_name"] = None
        elif mutation == "schema":
            payload["schemaVersion"] = True
        elif mutation == "channel":
            payload["channelId"] = "public_stable"
        elif mutation == "channel-alias":
            payload["channel"] = "Preview"
        elif mutation == "channel-missing":
            del payload["channel"]
        elif mutation == "version":
            payload["version"] = "different-version"
        elif mutation == "version-alias":
            payload["releaseVersion"] = f" {VERSION}"
        elif mutation == "version-missing":
            del payload["releaseVersion"]
        elif mutation == "head-alias":
            artifacts[0]["headId"] = "blazor-desktop"
        elif mutation == "head-empty":
            artifacts[0]["head"] = ""
        elif mutation == "head-missing":
            del artifacts[0]["head"]
            del artifacts[0]["headId"]
        elif mutation == "platform-case":
            artifacts[0]["platform"] = "Windows"
        elif mutation == "rid-padding":
            artifacts[0]["rid"] = "win-x64 "
        elif mutation == "kind-case":
            artifacts[0]["kind"] = "Installer"
        elif mutation == "mode":
            artifacts[0]["installerMode"] = "embedded"
        elif mutation == "mode-case":
            artifacts[0]["installerMode"] = "Bootstrap"
        elif mutation == "payload-mode-case":
            artifacts[0]["payloadAcquisitionMode"] = "Download"
        elif mutation == "filename-case":
            artifacts[0]["fileName"] = str(artifacts[0]["fileName"]).upper()
        elif mutation == "size":
            artifacts[0]["payloadSizeBytes"] += 1
        elif mutation == "duplicate-head":
            artifacts.append(dict(artifacts[0]))
        else:
            source = input_root / candidate_export.payload_path("avalonia")
            target = input_root / candidate_export.payload_path("blazor-desktop")
            target.write_bytes(source.read_bytes())
            artifacts[1]["payloadSha256"] = sha256(target)
            artifacts[1]["payloadSizeBytes"] = target.stat().st_size

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_allows_only_absent_or_null_unused_head_aliases(tmp_path: Path) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        del artifacts[0]["headId"]
        artifacts[1]["head"] = None

    rewrite_manifest(input_root, args, mutate)
    inventory_sha = candidate_export.export_candidate(args)
    assert inventory_sha == sha256(args.output_root / candidate_export.CONTENT_INVENTORY_PATH)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("source_workflow", ".github/workflows/other.yml"),
        ("source_ref", "refs/heads/feature"),
        ("source_sha", "A" * 40),
        ("expected_source_sha", "b" * 40),
        ("source_actor", "not a login"),
        ("artifact_name", "caller-selected-name"),
        ("runner_nonce", "self-hosted"),
    ],
)
def test_export_rejects_unbound_producer_source(tmp_path: Path, field: str, value: str) -> None:
    _, args = make_fixture(tmp_path)
    setattr(args, field, value)
    with pytest.raises(candidate_export.ContractError):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_can_require_a_read_only_candidate_mount(tmp_path: Path) -> None:
    _, args = make_fixture(tmp_path)
    args.require_read_only_input = True
    with pytest.raises(candidate_export.ContractError, match="mounted read-only"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_accepts_a_verified_read_only_candidate_mount(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, args = make_fixture(tmp_path)
    args.require_read_only_input = True

    class ReadOnlyMount:
        f_flag = candidate_export.os.ST_RDONLY

    monkeypatch.setattr(candidate_export.os, "statvfs", lambda _: ReadOnlyMount())
    inventory_sha = candidate_export.export_candidate(args)
    assert inventory_sha == sha256(args.output_root / candidate_export.CONTENT_INVENTORY_PATH)


def test_export_removes_partial_output_after_post_copy_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, args = make_fixture(tmp_path)
    original = candidate_export.write_json

    def fail_on_receipt(path: Path, payload: dict[str, object]) -> None:
        if path.name == candidate_export.EXPORT_RECEIPT_PATH:
            raise candidate_export.ContractError("simulated receipt failure")
        original(path, payload)

    monkeypatch.setattr(candidate_export, "write_json", fail_on_receipt)
    with pytest.raises(candidate_export.ContractError, match="simulated receipt failure"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


@pytest.mark.parametrize("held_mutation", ["manifest", "installer"])
def test_export_revalidates_held_authority_after_copy(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, held_mutation: str
) -> None:
    input_root, args = make_fixture(tmp_path)
    original_copy = candidate_export.copy_regular_no_follow
    mutated = False

    def mutate_before_copy(source: Path, target: Path) -> None:
        nonlocal mutated
        relative = source.relative_to(input_root).as_posix()
        wanted = (
            candidate_export.MANIFEST_PATH
            if held_mutation == "manifest"
            else candidate_export.installer_path("avalonia")
        )
        if relative == wanted and not mutated:
            mutated = True
            if held_mutation == "manifest":
                payload = json.loads(source.read_text(encoding="utf-8"))
                payload["releaseVersion"] = "stale-held-version"
                write_json(source, payload)
            else:
                source.write_bytes(source.read_bytes() + b"held-mutation")
        original_copy(source, target)

    monkeypatch.setattr(candidate_export, "copy_regular_no_follow", mutate_before_copy)
    with pytest.raises(candidate_export.ContractError):
        candidate_export.export_candidate(args)
    assert mutated
    assert not args.output_root.exists()


def preflight_script() -> str:
    path = REPO_ROOT / ".github/workflows/preview-nightly-candidate-export.yml"
    text = path.read_text(encoding="utf-8")
    workflow = yaml.load(text, Loader=yaml.BaseLoader)
    return workflow["jobs"]["preflight"]["steps"][0]["run"]


def valid_preflight_environment(output_path: Path) -> dict[str, str]:
    return {
        **os.environ,
        "EXPORT_CONFIRMED": "true",
        "RUNNER_NONCE": "abcdefghijkl",
        "CANDIDATE_VERSION": VERSION,
        "CANDIDATE_MANIFEST_SHA256": "c" * 64,
        "EXPECTED_SOURCE_SHA": SOURCE_SHA,
        "SOURCE_SHA": SOURCE_SHA,
        "SOURCE_REF": "refs/heads/main",
        "DEFAULT_BRANCH": "main",
        "GITHUB_OUTPUT": str(output_path),
    }


def test_preflight_emits_only_validated_fixed_prefix_outputs(tmp_path: Path) -> None:
    output = tmp_path / "github-output"
    result = subprocess.run(
        ["bash", "-c", preflight_script()],
        cwd=REPO_ROOT,
        env=valid_preflight_environment(output),
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr
    assert output.read_text(encoding="utf-8").splitlines() == [
        "runner_label=chummer-preview-nightly-export-abcdefghijkl",
        "runner_nonce=abcdefghijkl",
        f"candidate_version={VERSION}",
        f"candidate_manifest_sha256={'c' * 64}",
        f"expected_source_sha={SOURCE_SHA}",
        f"source_sha={SOURCE_SHA}",
        "source_ref=refs/heads/main",
    ]


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("EXPORT_CONFIRMED", "false"),
        ("RUNNER_NONCE", "self-hosted"),
        ("CANDIDATE_VERSION", f" {VERSION}"),
        ("CANDIDATE_MANIFEST_SHA256", "C" * 64),
        ("EXPECTED_SOURCE_SHA", "b" * 40),
        ("SOURCE_SHA", "A" * 40),
        ("SOURCE_REF", "refs/heads/feature"),
        ("DEFAULT_BRANCH", "master"),
    ],
)
def test_preflight_rejects_malformed_or_stale_authority_before_jit_queueing(
    tmp_path: Path, name: str, value: str
) -> None:
    output = tmp_path / "github-output"
    environment = valid_preflight_environment(output)
    environment[name] = value
    result = subprocess.run(
        ["bash", "-c", preflight_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode != 0
    assert not output.exists()


def test_workflow_is_a_pinned_read_only_disposable_artifact_lane() -> None:
    path = REPO_ROOT / ".github/workflows/preview-nightly-candidate-export.yml"
    text = path.read_text(encoding="utf-8")
    lower = text.lower()
    workflow = yaml.load(text, Loader=yaml.BaseLoader)

    assert set(workflow["on"]) == {"workflow_dispatch"}
    inputs = workflow["on"]["workflow_dispatch"]["inputs"]
    assert set(inputs) == {
        "runner_nonce",
        "candidate_version",
        "candidate_manifest_sha256",
        "expected_source_sha",
        "export_confirmed",
    }
    assert workflow["permissions"] == {}
    assert "concurrency" not in workflow
    assert "single-job jit runner" in lower
    assert "fresh disposable container" in lower
    assert "docker socket" in lower
    assert "destroy the runner and container" in lower
    preflight = workflow["jobs"]["preflight"]
    assert preflight["runs-on"] == "ubuntu-24.04"
    assert preflight["permissions"] == {}
    assert preflight["outputs"] == {
        "runner_label": "${{ steps.validate.outputs.runner_label }}",
        "runner_nonce": "${{ steps.validate.outputs.runner_nonce }}",
        "candidate_version": "${{ steps.validate.outputs.candidate_version }}",
        "candidate_manifest_sha256": "${{ steps.validate.outputs.candidate_manifest_sha256 }}",
        "expected_source_sha": "${{ steps.validate.outputs.expected_source_sha }}",
        "source_sha": "${{ steps.validate.outputs.source_sha }}",
        "source_ref": "${{ steps.validate.outputs.source_ref }}",
    }
    preflight_step = preflight["steps"][0]
    assert preflight_step["env"]["DEFAULT_BRANCH"] == (
        "${{ github.event.repository.default_branch }}"
    )
    assert preflight_step["env"]["SOURCE_REF"] == "${{ github.ref }}"
    assert preflight_step["env"]["SOURCE_SHA"] == "${{ github.sha }}"
    assert "chummer-preview-nightly-export-" in preflight_step["run"]
    job = workflow["jobs"]["export"]
    assert job["needs"] == "preflight"
    assert job["environment"] == "preview-nightly-candidate-export"
    assert job["permissions"] == {"contents": "read"}
    assert job["runs-on"] == [
        "self-hosted",
        "linux",
        "x64",
        "${{ needs.preflight.outputs.runner_label }}",
    ]
    assert job["concurrency"] == {
        "group": "preview-nightly-candidate-export-${{ needs.preflight.outputs.runner_nonce }}",
        "cancel-in-progress": "false",
    }
    assert "if" not in job
    assert "inputs." not in json.dumps(job)
    assert job["env"]["CANDIDATE_INPUT_ROOT"] == "/candidate-input"
    assert job["env"]["VALIDATED_SOURCE_SHA"] == "${{ needs.preflight.outputs.source_sha }}"
    assert job["env"]["VALIDATED_SOURCE_REF"] == "${{ needs.preflight.outputs.source_ref }}"
    steps = job["steps"]
    action_uses = [step["uses"] for step in steps if "uses" in step]
    assert action_uses == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    assert all(re.fullmatch(r"[^@]+@[0-9a-f]{40}", value) for value in action_uses)
    materialize = next(step for step in steps if step.get("id") == "materialize")
    assert "--require-read-only-input" in materialize["run"]
    assert '--expected-source-sha "$EXPECTED_SOURCE_SHA"' in materialize["run"]
    assert "--source-workflow .github/workflows/preview-nightly-candidate-export.yml" in materialize["run"]
    upload = next(step for step in steps if step.get("id") == "upload-candidate")
    assert upload["with"] == {
        "name": "${{ env.OUTPUT_ARTIFACT_NAME }}",
        "path": "${{ env.CANDIDATE_OUTPUT_ROOT }}",
        "if-no-files-found": "error",
        "retention-days": "14",
        "compression-level": "0",
        "overwrite": "false",
        "include-hidden-files": "false",
    }
    for forbidden in (
        "secrets.",
        "contents: write",
        "actions: write",
        "actions: read",
        "packages: write",
        "id-token: write",
        "gh release",
        "release create",
        "upload-session",
        "curl ",
        "wget ",
    ):
        assert forbidden not in lower
