from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from argparse import Namespace
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOL = REPO_ROOT / "scripts" / "macos_hosted_native_lifecycle_probe.py"
WORKFLOW = (
    REPO_ROOT / ".github" / "workflows" / "macos-flagship-evidence.yml"
)


def load_tool_module():
    scripts = str(REPO_ROOT / "scripts")
    if scripts not in sys.path:
        sys.path.insert(0, scripts)
    spec = importlib.util.spec_from_file_location(
        "macos_hosted_native_lifecycle_probe", TOOL
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def hosted_environment(tmp_path: Path) -> dict[str, str]:
    runner_temp = tmp_path / "runner-temp"
    workspace = tmp_path / "workspace"
    runner_temp.mkdir()
    workspace.mkdir()
    return {
        "CI": "true",
        "GITHUB_ACTIONS": "true",
        "GITHUB_ACTOR": "release-operator",
        "GITHUB_EVENT_NAME": "workflow_dispatch",
        "GITHUB_REF": "refs/heads/main",
        "GITHUB_REPOSITORY": "ArchonMegalon/chummer6-ui",
        "GITHUB_RUN_ATTEMPT": "2",
        "GITHUB_RUN_ID": "100",
        "GITHUB_SHA": "1" * 40,
        "GITHUB_TRIGGERING_ACTOR": "release-operator",
        "GITHUB_WORKSPACE": str(workspace),
        "ImageOS": "macos15",
        "ImageVersion": "20260720.1",
        "RUNNER_ARCH": "ARM64",
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
        "RUNNER_TEMP": str(runner_temp),
        "CHUMMER_MACOS_HOSTED_PROBE_RUNNER_IMAGE": "macos-15",
    }


def github_payload() -> dict[str, str]:
    return {
        "actor": "release-operator",
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "rerunPolicy": "same-actor-only",
        "runAttempt": "2",
        "runId": "100",
        "sha": "1" * 40,
        "triggeringActor": "release-operator",
        "workflow": ".github/workflows/macos-flagship-evidence.yml",
    }


def runner_payload() -> dict[str, str]:
    return {
        "architecture": "arm64",
        "environment": "github-hosted",
        "imageOS": "macos15",
        "imageVersion": "20260720.1",
        "label": "macos-15",
        "operatingSystem": "Darwin",
    }


def capacity_payload() -> dict:
    return {
        "capacity": {
            "finalFreeBytes": 24 * 1024**3,
            "minimumFreeBytes": 20 * 1024**3,
        },
        "checks": {
            "capacity": True,
            "dummyKeychainLifecycle": True,
            "hostedRunnerContext": True,
            "secretless": True,
            "tinyDmgLifecycle": True,
            "toolchain": True,
        },
        "contractName": "chummer6-ui.macos-hosted-capacity-probe.v1",
        "contractVersion": 1,
        "github": {
            "ref": "refs/heads/main",
            "repository": "ArchonMegalon/chummer6-ui",
            "runAttempt": "2",
            "runId": "100",
            "sha": "1" * 40,
        },
        "nonPublishing": {
            "artifactBuilt": False,
            "notarizationSubmitted": False,
            "publicationAttempted": False,
            "releaseAuthorityAccepted": False,
            "signingAttempted": False,
        },
        "runner": runner_payload(),
        "status": "passed",
    }


def authority_payload() -> dict:
    return {
        "contractName": (
            "chummer6-ui.macos-flagship-authority-validation"
        ),
        "contractVersion": 2,
        "github": github_payload(),
        "releaseVersion": "run-20260725-120000",
        "rid": "osx-arm64",
        "runnerPolicy": {
            "architecture": "arm64",
            "environment": "github-hosted",
            "imageOS": "macos15",
            "label": "macos-15",
            "operatingSystem": "macOS",
        },
        "status": "pass",
    }


def write_json(path: Path, payload: dict) -> bytes:
    raw = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode()
    path.write_bytes(raw)
    return raw


def lifecycle_payload(tool, capacity_raw: bytes, authority_raw: bytes) -> dict:
    artifact_sha = "a" * 64
    return {
        "authority": {
            "receiptSha256": tool.sha256_bytes(authority_raw),
            "releaseVersion": "run-20260725-120000",
            "rid": "osx-arm64",
        },
        "build": {
            "artifact": {
                "fileName": (
                    "chummer-avalonia-osx-arm64-installer.dmg"
                ),
                "sha256": artifact_sha,
                "sizeBytes": 1024,
            },
            "stageManifestSha256": "b" * 64,
            "stageOnlyReceiptSha256": "c" * 64,
        },
        "capacityReceiptSha256": tool.sha256_bytes(capacity_raw),
        "checks": {
            "arm64Executable": True,
            "dmgMountedReadOnly": True,
            "isolatedInstall": True,
            "isolatedUninstall": True,
            "plaintextCleanup": True,
            "startupSmoke": True,
        },
        "completedAtUtc": "2026-07-25T12:00:00Z",
        "contractName": (
            "chummer6-ui.macos-hosted-native-lifecycle-proof.v1"
        ),
        "contractVersion": 1,
        "github": github_payload(),
        "nonPublishing": tool.lifecycle_nonpublishing(),
        "runner": runner_payload(),
        "startup": {
            "artifactDigest": f"sha256:{artifact_sha}",
            "hostClass": (
                "github-hosted-macos-arm64-secretless-capacity"
            ),
            "readyCheckpoint": "pre_ui_event_loop",
            "receiptSha256": "d" * 64,
            "releaseVersion": "run-20260725-120000",
            "rid": "osx-arm64",
            "status": "pass",
        },
        "status": "pass",
    }


def test_runner_identity_binds_image_os_version_and_arch(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)

    runner = tool.current_runner(
        environment,
        system_name="Darwin",
        machine_name="arm64",
    )

    assert runner == runner_payload()
    environment["ImageVersion"] = ""
    with pytest.raises(tool.capacity_probe.ProbeFailure):
        tool.current_runner(
            environment,
            system_name="Darwin",
            machine_name="arm64",
        )


def test_stage_validation_binds_exact_unsigned_dmg(tmp_path: Path) -> None:
    tool = load_tool_module()
    stage = tmp_path / "stage"
    (stage / "files").mkdir(parents=True)
    (stage / "release-evidence").mkdir()
    dmg = stage / "files" / tool.ARTIFACT_FILE_NAME
    dmg.write_bytes(b"unsigned-native-dmg")
    write_json(
        stage / "RELEASE_CHANNEL.generated.json",
        {
            "artifacts": [
                {
                    "fileName": tool.ARTIFACT_FILE_NAME,
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "sha256": tool.sha256_file(dmg),
                    "sizeBytes": dmg.stat().st_size,
                }
            ]
        },
    )
    write_json(
        stage / "release-evidence" / "mac-stage-only.json",
        {
            "appHeads": ["avalonia"],
            "contractName": "chummer.run.mac_release_stage_only",
            "countsAsPublicationEvidence": False,
            "mode": "stage_only",
            "outputPathDisclosure": "directory_name_only",
            "publicActivationAttempted": False,
            "publicationAttempted": False,
            "releaseVersion": "run-20260725-120000",
            "rid": "osx-arm64",
            "sourceReceiptSha256": "f" * 64,
            "status": "pass",
            "uploadAttempted": False,
        },
    )

    source, _, _, _ = tool.validate_stage(
        stage,
        release_version="run-20260725-120000",
    )

    assert source == dmg


def test_plaintext_cleanup_is_limited_to_owned_runner_roots(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    runner_temp = tmp_path / "runner-temp"
    stage = runner_temp / "macos-hosted-native-stage"
    build = runner_temp / "macos-hosted-native-build"
    stage.mkdir(parents=True)
    build.mkdir()
    (stage / "candidate.dmg").write_bytes(b"candidate")
    (build / "app-bytes").write_bytes(b"app")

    tool.remove_owned_plaintext_roots(
        runner_temp,
        stage_root=stage,
        build_root=build,
    )

    assert not stage.exists()
    assert not build.exists()
    outside = tmp_path / "outside"
    outside.mkdir()
    with pytest.raises(tool.ProofFailure, match="owned path"):
        tool.remove_owned_plaintext_roots(
            runner_temp,
            stage_root=outside,
            build_root=runner_temp / "macos-hosted-native-build",
        )


def test_verify_consumes_exact_receipts_before_secrets(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)
    for key, value in environment.items():
        monkeypatch.setenv(key, value)
    monkeypatch.setattr(tool.platform, "system", lambda: "Darwin")
    monkeypatch.setattr(tool.platform, "machine", lambda: "arm64")

    capacity_path = tmp_path / "capacity.json"
    authority_path = tmp_path / "authority.json"
    lifecycle_path = tmp_path / "lifecycle.json"
    evidence_capacity_path = tmp_path / "evidence-capacity.json"
    capacity_raw = write_json(capacity_path, capacity_payload())
    authority_raw = write_json(authority_path, authority_payload())
    lifecycle = lifecycle_payload(tool, capacity_raw, authority_raw)
    write_json(lifecycle_path, lifecycle)
    write_json(evidence_capacity_path, capacity_payload())
    output = Path(environment["RUNNER_TEMP"]) / "consumption.json"

    result = tool.command_verify(
        Namespace(
            source_capacity_receipt=capacity_path,
            source_authority_receipt=authority_path,
            source_lifecycle_receipt=lifecycle_path,
            evidence_capacity_receipt=evidence_capacity_path,
            artifact_id="300",
            artifact_digest="e" * 64,
            artifact_name="macos-hosted-native-capacity-100-2",
            output=output,
        )
    )

    assert result == 0
    receipt = json.loads(output.read_text())
    assert receipt["evidenceRunner"] == runner_payload()
    assert receipt["sourceProof"]["runner"] == runner_payload()
    assert receipt["sourceProof"]["artifact"] == {
        "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
        "sha256": "a" * 64,
        "sizeBytes": 1024,
    }
    assert receipt["sourceProof"]["releaseVersion"] == (
        "run-20260725-120000"
    )
    assert receipt["actionsArtifact"] == {
        "digest": "e" * 64,
        "id": "300",
        "name": "macos-hosted-native-capacity-100-2",
    }
    assert receipt["nonPublishing"]["protectedSecretsReferenced"] is False


def test_probe_never_signs_notarizes_or_publishes() -> None:
    text = TOOL.read_text(encoding="utf-8")

    assert '"hdiutil",' in text
    assert '"ditto",' in text
    assert '"lipo", "-archs"' in text
    assert '"--startup-smoke"' in text
    assert '"security", "import"' not in text
    assert '"codesign", "--sign"' not in text
    assert '"notarytool", "submit"' not in text
    assert "publish-download-bundle" not in text
    assert "shell=True" not in text


def test_workflow_places_secretless_hosted_proof_before_secrets() -> None:
    text = WORKFLOW.read_text(encoding="utf-8")

    assert "runs-on: macos-15" in text
    assert "self-hosted" not in text
    assert "native_capacity:" in text
    native_job = text.split("  native_capacity:", 1)[1].split(
        "\n  evidence:", 1
    )[0]
    assert "environment:" not in native_job
    assert "${{ secrets." not in native_job
    assert "--stage-only" in native_job
    assert "macos_hosted_native_lifecycle_probe.py run" in native_job
    assert "MACOS_HOSTED_NATIVE_LIFECYCLE.generated.json" in native_job
    evidence_job = text.split("\n  evidence:", 1)[1]
    assert evidence_job.index(
        "macos_hosted_native_lifecycle_probe.py verify"
    ) < evidence_job.index("${{ secrets.")
    assert "actions/download-artifact@" in evidence_job
    assert "artifact-ids: ${{ needs.native_capacity.outputs.artifact_id }}" in text
    assert "environment: macos-flagship-evidence" in evidence_job


def test_python_contract_compiles() -> None:
    result = subprocess.run(
        (sys.executable, "-m", "py_compile", str(TOOL)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0, result.stderr
