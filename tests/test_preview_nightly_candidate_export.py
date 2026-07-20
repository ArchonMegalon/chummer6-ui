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
    rows.append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "head": "avalonia",
            "headId": "avalonia",
            "platform": "linux",
            "rid": "linux-x64",
            "kind": "installer",
            "fileName": "chummer-avalonia-linux-x64-installer.deb",
            "sha256": "f" * 64,
            "sizeBytes": 1,
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
            "desktopTupleCoverage": {
                "requiredDesktopHeads": list(candidate_export.HEADS),
                "requiredDesktopPlatforms": list(
                    candidate_export.REGISTRY_REQUIRED_DESKTOP_PLATFORMS
                ),
            },
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


def test_export_emits_exact_five_file_artifact_and_bound_receipt(tmp_path: Path) -> None:
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
            "unsupported-head",
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
        elif mutation == "unsupported-head":
            unsupported = dict(artifacts[0])
            unsupported["head"] = "blazor-desktop"
            unsupported["headId"] = "blazor-desktop"
            artifacts.append(unsupported)
        else:
            source = input_root / candidate_export.installer_path("avalonia")
            target = input_root / candidate_export.payload_path("avalonia")
            target.write_bytes(source.read_bytes())
            artifacts[0]["payloadSha256"] = sha256(target)
            artifacts[0]["payloadSizeBytes"] = target.stat().st_size

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_allows_only_absent_or_null_unused_head_aliases(tmp_path: Path) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        artifacts[0]["headId"] = None

    rewrite_manifest(input_root, args, mutate)
    inventory_sha = candidate_export.export_candidate(args)
    assert inventory_sha == sha256(args.output_root / candidate_export.CONTENT_INVENTORY_PATH)


@pytest.mark.parametrize(
    "mutation",
    (
        "missing-coverage",
        "widened-required-heads",
        "missing-required-platform",
        "blazor-windows-msix",
        "blazor-linux-retained",
        "avalonia-extra-rid",
        "avalonia-extra-linux-rid",
    ),
)
def test_export_rejects_any_desktop_scope_widening_or_coverage_drift(
    tmp_path: Path, mutation: str
) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        coverage = payload["desktopTupleCoverage"]
        artifacts = payload["artifacts"]
        assert isinstance(coverage, dict)
        assert isinstance(artifacts, list)
        if mutation == "missing-coverage":
            del payload["desktopTupleCoverage"]
        elif mutation == "widened-required-heads":
            coverage["requiredDesktopHeads"] = ["avalonia", "blazor-desktop"]
        elif mutation == "missing-required-platform":
            coverage["requiredDesktopPlatforms"] = ["windows"]
        else:
            extra = dict(artifacts[0])
            if mutation == "blazor-windows-msix":
                extra.update(
                    {
                        "artifactId": "blazor-desktop-win-x64-msix",
                        "head": "blazor-desktop",
                        "headId": "blazor-desktop",
                        "kind": "msix",
                        "fileName": "chummer-blazor-desktop-win-x64.msix",
                    }
                )
            elif mutation == "blazor-linux-retained":
                extra.update(
                    {
                        "artifactId": "blazor-desktop-linux-x64-installer",
                        "head": "blazor-desktop",
                        "headId": "blazor-desktop",
                        "platform": "linux",
                        "rid": "linux-x64",
                        "fileName": "chummer-blazor-desktop-linux-x64-installer.deb",
                    }
                )
            elif mutation == "avalonia-extra-rid":
                extra.update(
                    {
                        "artifactId": "avalonia-win-arm64-installer",
                        "rid": "win-arm64",
                        "fileName": "chummer-avalonia-win-arm64-installer.exe",
                    }
                )
            else:
                extra.update(
                    {
                        "artifactId": "avalonia-linux-arm64-installer",
                        "platform": "linux",
                        "rid": "linux-arm64",
                        "fileName": "chummer-avalonia-linux-arm64-installer.deb",
                    }
                )
            artifacts.append(extra)

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError, match="desktop|Desktop|Windows"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_rejects_avalonia_macos_artifact_outside_current_registry_target(
    tmp_path: Path,
) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        retained = dict(artifacts[-1])
        retained.update(
            {
                "artifactId": "avalonia-osx-arm64-installer",
                "platform": "macos",
                "rid": "osx-arm64",
                "fileName": "chummer-avalonia-osx-arm64-installer.pkg",
                "publicationState": "retained",
            }
        )
        artifacts.append(retained)

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError, match="outside the active desktop platforms"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


def test_export_rejects_unknown_platform_artifact_outside_current_registry_target(
    tmp_path: Path,
) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        unknown = dict(artifacts[-1])
        unknown.update(
            {
                "artifactId": "avalonia-freebsd-x64-installer",
                "platform": "freebsd",
                "rid": "freebsd-x64",
                "fileName": "chummer-avalonia-freebsd-x64-installer.tar.zst",
            }
        )
        artifacts.append(unknown)

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError, match="outside the active desktop platforms"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


@pytest.mark.parametrize("mutation", ("platform-alias-conflict", "wrong-linux-identity"))
def test_export_rejects_inexact_active_desktop_artifact_identity(
    tmp_path: Path, mutation: str
) -> None:
    input_root, args = make_fixture(tmp_path)

    def mutate(payload: dict[str, object]) -> None:
        artifacts = payload["artifacts"]
        assert isinstance(artifacts, list)
        if mutation == "platform-alias-conflict":
            artifacts[0]["platformId"] = "macos"
        else:
            artifacts[-1]["artifactId"] = "avalonia-linux-x64-package"
            artifacts[-1]["fileName"] = "forged-linux-installer.deb"

    rewrite_manifest(input_root, args, mutate)
    with pytest.raises(candidate_export.ContractError, match="platform identity|artifact identity"):
        candidate_export.export_candidate(args)
    assert not args.output_root.exists()


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


def test_export_login_parser_accepts_only_the_exact_actions_bot_special_case(
    tmp_path: Path,
) -> None:
    _, args = make_fixture(tmp_path / "exact", actor="github-actions[bot]")
    candidate_export.export_candidate(args)
    receipt = json.loads(
        (args.output_root / candidate_export.EXPORT_RECEIPT_PATH).read_text(encoding="utf-8")
    )
    assert receipt["source"]["actor"] == "github-actions[bot]"
    for index, lookalike in enumerate(
        (
            "github-actions[Bot]",
            "github-actions[bot]x",
            "github_actions[bot]",
            "human[bot]",
        )
    ):
        _, invalid = make_fixture(tmp_path / f"invalid-{index}", actor=lookalike)
        with pytest.raises(candidate_export.ContractError, match="exact GitHub login"):
            candidate_export.export_candidate(invalid)


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


def producer_handoff_script() -> str:
    path = REPO_ROOT / ".github/workflows/preview-nightly-candidate-export.yml"
    workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
    step = next(
        row
        for row in workflow["jobs"]["export"]["steps"]
        if row.get("id") == "producer-handoff"
    )
    return step["run"]


def relay_script() -> str:
    path = REPO_ROOT / ".github/workflows/preview-nightly-candidate-export.yml"
    workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
    return workflow["jobs"]["relay-capture"]["steps"][0]["run"]


def valid_handoff_environment(root: Path) -> dict[str, str]:
    return {
        **os.environ,
        "ARTIFACT_ID": "777",
        "ARTIFACT_DIGEST": "d" * 64,
        "ARTIFACT_URL": "https://github.example/artifacts/777",
        "CONTENT_INVENTORY_SHA256": "e" * 64,
        "GITHUB_ACTOR": "capture-operator",
        "GITHUB_OUTPUT": str(root / "github-output"),
        "GITHUB_REF": "refs/heads/main",
        "GITHUB_REPOSITORY": "ArchonMegalon/chummer6-ui",
        "GITHUB_RUN_ATTEMPT": "1",
        "GITHUB_RUN_ID": "12000",
        "GITHUB_SHA": SOURCE_SHA,
        "GITHUB_STEP_SUMMARY": str(root / "summary"),
        "OUTPUT_ARTIFACT_NAME": "preview-nightly-candidate-12000-1",
    }


def valid_relay_environment(root: Path) -> dict[str, str]:
    handoff = {
        "actor": "capture-operator",
        "artifactId": "777",
        "artifactName": "preview-nightly-candidate-12000-1",
        "artifactSha256": "d" * 64,
        "contentInventorySha256": "e" * 64,
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 1,
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "runAttempt": "1",
        "runId": "12000",
        "sha": SOURCE_SHA,
        "workflow": candidate_export.PRODUCER_WORKFLOW,
    }
    return {
        **os.environ,
        "CANDIDATE_HANDOFF_JSON": json.dumps(
            handoff, sort_keys=True, separators=(",", ":")
        ),
        "CAPTURE_DISPATCH_RECEIPT": str(root / "PREVIEW_NIGHTLY_CAPTURE_DISPATCH.generated.json"),
        "GH_TOKEN": "fixture-token",
        "GITHUB_ACTOR": "capture-operator",
        "GITHUB_REF": "refs/heads/main",
        "GITHUB_REPOSITORY": "ArchonMegalon/chummer6-ui",
        "GITHUB_RUN_ATTEMPT": "1",
        "GITHUB_RUN_ID": "12000",
        "GITHUB_SHA": SOURCE_SHA,
        "FAKE_REQUEST_LOG": str(root / "request-log.jsonl"),
    }


def install_fake_urllib(root: Path) -> Path:
    package = root / "fake-modules" / "urllib"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "error.py").write_text(
        "class HTTPError(Exception):\n"
        "    def __init__(self, code):\n"
        "        super().__init__(code)\n"
        "        self.code = code\n",
        encoding="utf-8",
    )
    (package / "request.py").write_text(
        "import json\n"
        "import os\n"
        "\n"
        "class Request:\n"
        "    def __init__(self, url, data, headers, method):\n"
        "        self.full_url = url\n"
        "        self.data = data\n"
        "        self.headers = headers\n"
        "        self.method = method\n"
        "\n"
        "class Response:\n"
        "    status = 200\n"
        "\n"
        "    def __enter__(self):\n"
        "        return self\n"
        "\n"
        "    def __exit__(self, *_args):\n"
        "        return False\n"
        "\n"
        "    def read(self, limit):\n"
        "        return os.environ['FAKE_RESPONSE_JSON'].encode('utf-8')[:limit]\n"
        "\n"
        "def urlopen(request, timeout):\n"
        "    record = {\n"
        "        'data': request.data.decode('utf-8'),\n"
        "        'method': request.method,\n"
        "        'timeout': timeout,\n"
        "        'url': request.full_url,\n"
        "    }\n"
        "    with open(os.environ['FAKE_REQUEST_LOG'], 'a', encoding='utf-8') as log:\n"
        "        log.write(json.dumps(record, sort_keys=True) + '\\n')\n"
        "    return Response()\n",
        encoding="utf-8",
    )
    return package.parent


def test_producer_handoff_step_emits_one_exact_canonical_json_output(tmp_path: Path) -> None:
    environment = valid_handoff_environment(tmp_path)
    result = subprocess.run(
        ["bash", "-c", producer_handoff_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr
    line = (tmp_path / "github-output").read_text(encoding="utf-8").strip()
    prefix = "candidate_handoff_json="
    assert line.startswith(prefix)
    raw = line.removeprefix(prefix)
    payload = json.loads(raw)
    assert raw == json.dumps(payload, sort_keys=True, separators=(",", ":"))
    assert payload == {
        "actor": "capture-operator",
        "artifactId": "777",
        "artifactName": "preview-nightly-candidate-12000-1",
        "artifactSha256": "d" * 64,
        "contentInventorySha256": "e" * 64,
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 1,
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "runAttempt": "1",
        "runId": "12000",
        "sha": SOURCE_SHA,
        "workflow": candidate_export.PRODUCER_WORKFLOW,
    }


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("ARTIFACT_ID", "0777"),
        ("ARTIFACT_DIGEST", f"sha256:{'d' * 64}"),
        ("ARTIFACT_DIGEST", f"SHA256:{'d' * 64}"),
        ("ARTIFACT_DIGEST", "D" * 64),
        ("CONTENT_INVENTORY_SHA256", "E" * 64),
    ],
)
def test_producer_handoff_step_rejects_non_exact_artifact_identity(
    tmp_path: Path, name: str, value: str
) -> None:
    environment = valid_handoff_environment(tmp_path)
    environment[name] = value
    result = subprocess.run(
        ["bash", "-c", producer_handoff_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode != 0
    assert not (tmp_path / "github-output").exists()


def test_relay_rejects_invalid_handoff_before_any_dispatch_request() -> None:
    environment = {
        **os.environ,
        "CANDIDATE_HANDOFF_JSON": "{}",
        "GH_TOKEN": "not-used",
        "GITHUB_ACTOR": "capture-operator",
        "GITHUB_REF": "refs/heads/main",
        "GITHUB_REPOSITORY": "ArchonMegalon/chummer6-ui",
        "GITHUB_RUN_ATTEMPT": "1",
        "GITHUB_RUN_ID": "12000",
        "GITHUB_SHA": SOURCE_SHA,
    }
    result = subprocess.run(
        ["bash", "-c", relay_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode != 0
    assert "missing or extra fields" in result.stderr


def test_relay_accepts_one_http_200_dispatch_with_exact_run_details(
    tmp_path: Path,
) -> None:
    environment = valid_relay_environment(tmp_path)
    environment["PYTHONPATH"] = str(install_fake_urllib(tmp_path))
    run_id = 13001
    environment["FAKE_RESPONSE_JSON"] = json.dumps(
        {
            "workflow_run_id": run_id,
            "run_url": (
                "https://api.github.com/repos/ArchonMegalon/chummer6-ui/"
                f"actions/runs/{run_id}"
            ),
            "html_url": (
                "https://github.com/ArchonMegalon/chummer6-ui/"
                f"actions/runs/{run_id}"
            ),
        },
        separators=(",", ":"),
    )
    result = subprocess.run(
        ["bash", "-c", relay_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr
    calls = [
        json.loads(row)
        for row in Path(environment["FAKE_REQUEST_LOG"])
        .read_text(encoding="utf-8")
        .splitlines()
    ]
    assert len(calls) == 1
    assert calls[0]["method"] == "POST"
    assert calls[0]["timeout"] == 30
    assert calls[0]["url"].endswith(
        "/actions/workflows/windows-native-evidence-capture.yml/dispatches"
    )
    assert json.loads(calls[0]["data"]) == {
        "ref": "main",
        "inputs": {
            "candidate_handoff_json": environment["CANDIDATE_HANDOFF_JSON"]
        },
    }
    assert "return_" + "run_details" not in calls[0]["data"]
    receipt = json.loads(
        Path(environment["CAPTURE_DISPATCH_RECEIPT"]).read_text(encoding="utf-8")
    )
    assert receipt["candidateHandoff"] == json.loads(environment["CANDIDATE_HANDOFF_JSON"])
    assert receipt["capture"]["runId"] == str(run_id)
    assert receipt["status"] == "dispatched"


@pytest.mark.parametrize(
    "response",
    [
        {
            "workflow_run_id": 0,
            "run_url": "https://api.github.com/repos/ArchonMegalon/chummer6-ui/actions/runs/0",
            "html_url": "https://github.com/ArchonMegalon/chummer6-ui/actions/runs/0",
        },
        {
            "workflow_run_id": 13001,
            "run_url": "https://api.github.com/repos/evil/repo/actions/runs/13001",
            "html_url": "https://github.com/ArchonMegalon/chummer6-ui/actions/runs/13001",
        },
        {
            "workflow_run_id": 13001,
            "run_url": "https://api.github.com/repos/ArchonMegalon/chummer6-ui/actions/runs/13001",
            "html_url": "https://github.com/evil/repo/actions/runs/13001",
        },
        {
            "workflow_run_id": 13001,
            "run_url": "https://api.github.com/repos/ArchonMegalon/chummer6-ui/actions/runs/13001",
            "html_url": "https://github.com/ArchonMegalon/chummer6-ui/actions/runs/13001",
            "extra": "ambiguous",
        },
    ],
    ids=("nonpositive-id", "foreign-api-url", "foreign-html-url", "extra-field"),
)
def test_relay_rejects_inexact_http_200_run_details(
    tmp_path: Path, response: dict[str, object]
) -> None:
    environment = valid_relay_environment(tmp_path)
    environment["PYTHONPATH"] = str(install_fake_urllib(tmp_path))
    environment["FAKE_RESPONSE_JSON"] = json.dumps(response, separators=(",", ":"))
    result = subprocess.run(
        ["bash", "-c", relay_script()],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode != 0
    assert len(
        Path(environment["FAKE_REQUEST_LOG"])
        .read_text(encoding="utf-8")
        .splitlines()
    ) == 1


def test_workflow_is_a_pinned_read_only_disposable_artifact_lane() -> None:
    path = REPO_ROOT / ".github/workflows/preview-nightly-candidate-export.yml"
    text = path.read_text(encoding="utf-8")
    lower = text.lower()
    workflow = yaml.load(text, Loader=yaml.BaseLoader)

    assert set(workflow["on"]) == {"workflow_dispatch"}
    assert workflow["run-name"] == (
        "chummer-preview-nightly-export-${{ inputs.runner_nonce }}"
    )
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
    assert "CANDIDATE_OUTPUT_ROOT" not in job["env"]
    assert job["env"]["VALIDATED_SOURCE_SHA"] == "${{ needs.preflight.outputs.source_sha }}"
    assert job["env"]["VALIDATED_SOURCE_REF"] == "${{ needs.preflight.outputs.source_ref }}"
    assert job["outputs"]["candidate_handoff_json"] == (
        "${{ steps.producer-handoff.outputs.candidate_handoff_json }}"
    )
    steps = job["steps"]
    python_gate = next(
        step for step in steps
        if step.get("name") == "Verify the pinned runner Python runtime"
    )
    materialize = next(step for step in steps if step.get("id") == "materialize")
    assert steps.index(python_gate) < steps.index(materialize)
    assert python_gate["run"] == (
        'set -euo pipefail\n'
        'test "$(python3 --version)" = "Python 3.12.3"\n'
    )
    assert "actions/setup-python" not in lower
    assert materialize["env"]["CANDIDATE_OUTPUT_ROOT"] == (
        "${{ runner.temp }}/preview-nightly-candidate-export"
    )
    action_uses = [step["uses"] for step in steps if "uses" in step]
    assert action_uses == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    assert all(re.fullmatch(r"[^@]+@[0-9a-f]{40}", value) for value in action_uses)
    assert "--require-read-only-input" in materialize["run"]
    assert '--expected-source-sha "$EXPECTED_SOURCE_SHA"' in materialize["run"]
    assert "--source-workflow .github/workflows/preview-nightly-candidate-export.yml" in materialize["run"]
    upload = next(step for step in steps if step.get("id") == "upload-candidate")
    assert upload["with"] == {
        "name": "${{ env.OUTPUT_ARTIFACT_NAME }}",
        "path": "${{ runner.temp }}/preview-nightly-candidate-export",
        "if-no-files-found": "error",
        "retention-days": "14",
        "compression-level": "0",
        "overwrite": "false",
        "include-hidden-files": "false",
    }
    handoff = next(step for step in steps if step.get("id") == "producer-handoff")
    assert "candidate_handoff_json=" in handoff["run"]
    assert 're.fullmatch(r"[0-9a-f]{64}", artifact_digest_value)' in handoff["run"]
    assert 'json.dumps(handoff, sort_keys=True, separators=(",", ":"))' in handoff["run"]
    relay = workflow["jobs"]["relay-capture"]
    assert relay["needs"] == "export"
    assert relay["runs-on"] == "ubuntu-24.04"
    assert relay["permissions"] == {"actions": "write"}
    assert len(relay["steps"]) == 2
    relay_step = relay["steps"][0]
    assert relay_step["env"]["CANDIDATE_HANDOFF_JSON"] == (
        "${{ needs.export.outputs.candidate_handoff_json }}"
    )
    assert relay_step["env"]["GH_TOKEN"] == "${{ github.token }}"
    assert relay_step["env"]["CAPTURE_DISPATCH_RECEIPT"] == (
        "${{ runner.temp }}/PREVIEW_NIGHTLY_CAPTURE_DISPATCH.generated.json"
    )
    assert "/actions/workflows/windows-native-evidence-capture.yml/dispatches" in relay_step["run"]
    assert '{"ref": "main", "inputs": {"candidate_handoff_json": canonical}}' in relay_step["run"]
    assert '"X-GitHub-Api-Version": "2026-03-10"' in relay_step["run"]
    assert "response.status != 200" in relay_step["run"]
    assert "response.status != 204" not in relay_step["run"]
    assert "workflow_run_id" in relay_step["run"]
    assert "return_" + "run_details" not in relay_step["run"]
    dispatch_upload = relay["steps"][1]
    assert dispatch_upload["uses"] == (
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02"
    )
    assert dispatch_upload["with"] == {
        "name": "preview-nightly-capture-dispatch-${{ github.run_id }}-${{ github.run_attempt }}",
        "path": "${{ runner.temp }}/PREVIEW_NIGHTLY_CAPTURE_DISPATCH.generated.json",
        "if-no-files-found": "error",
        "retention-days": "14",
        "compression-level": "0",
        "overwrite": "false",
        "include-hidden-files": "false",
    }
    export_lower = json.dumps(job).lower()
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
        assert forbidden not in export_lower
    for forbidden in (
        "secrets.",
        "contents: write",
        "packages: write",
        "id-token: write",
        "gh release",
        "release create",
    ):
        assert forbidden not in lower


def test_pull_request_ci_pins_actionlint_for_workflow_semantic_validation() -> None:
    path = REPO_ROOT / ".github/workflows/pull-request-ci.yml"
    workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
    steps = workflow["jobs"]["release-controls"]["steps"]
    validation = next(
        step
        for step in steps
        if step.get("name") == "Validate GitHub Actions workflow semantics"
    )

    assert validation["env"] == {
        "ACTIONLINT_ARCHIVE_SHA256": (
            "8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8"
        ),
        "ACTIONLINT_VERSION": "1.7.12",
    }
    script = validation["run"]
    assert "rhysd/actionlint/releases/download/v${ACTIONLINT_VERSION}" in script
    assert "sha256sum --check --strict" in script
    assert 'test "$("$actionlint_root/actionlint" -version | head -n 1)"' in script
    assert '"$actionlint_root/actionlint" -color' in script
