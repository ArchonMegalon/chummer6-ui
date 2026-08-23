from __future__ import annotations

import argparse
import importlib.util
import json
import subprocess
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize_unsigned_macos_build_receipt.py"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unsigned-macos-native-build.yml"
BUILD_SCRIPT = REPO_ROOT / "scripts" / "build-unsigned-macos-native.sh"
DOC = REPO_ROOT / "docs" / "UNSIGNED_MACOS_NATIVE_BUILD.md"
PACKAGE_PLANE_LOCK = REPO_ROOT / "config" / "package-plane.lock.json"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("unsigned_macos_receipt", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


receipt_tool = load_module()


def init_repository(root: Path, commit_name: str) -> str:
    root.mkdir(parents=True)
    subprocess.run(["git", "init", "-q", str(root)], check=True)
    subprocess.run(["git", "-C", str(root), "config", "user.email", "tests@example.invalid"], check=True)
    subprocess.run(["git", "-C", str(root), "config", "user.name", "Tests"], check=True)
    subprocess.run(["git", "-C", str(root), "remote", "add", "origin", f"https://example.invalid/{commit_name}.git"], check=True)
    (root / "source.txt").write_text(commit_name + "\n", encoding="utf-8")
    subprocess.run(["git", "-C", str(root), "add", "source.txt"], check=True)
    subprocess.run(["git", "-C", str(root), "commit", "-q", "-m", commit_name], check=True)
    return subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def candidate(tmp_path: Path, rid: str = "osx-arm64") -> tuple[argparse.Namespace, dict[str, str]]:
    source_root = tmp_path / "ui"
    source_commit = init_repository(source_root, "ui")
    version = f"0.0.0-ci.sha{source_commit[:12]}"
    artifact_name = receipt_tool.RUNNER_POLICIES[rid]["artifact"]
    artifact = tmp_path / artifact_name
    artifact.write_bytes(b"native-dmg-bytes")
    artifact_sha = receipt_tool.sha256_file(artifact)

    signing_receipt = tmp_path / "signing.json"
    signing_receipt.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.desktop_artifact_signing",
                "contractVersion": 2,
                "platform": "macos",
                "app": "avalonia",
                "rid": rid,
                "releaseChannel": "preview",
                "releaseVersion": version,
                "signingStatus": "skipped_preview",
                "notarizationStatus": "skipped_preview",
                "artifacts": [
                    {
                        "fileName": artifact_name,
                        "sha256": artifact_sha,
                        "signingStatus": "skipped_preview",
                        "notarizationStatus": "skipped_preview",
                    }
                ],
            }
        ),
        encoding="utf-8",
    )
    arch = "arm64" if rid == "osx-arm64" else "x64"
    startup_receipt = tmp_path / "startup.json"
    startup_receipt.write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "macos",
                "arch": arch,
                "rid": rid,
                "releaseVersion": version,
                "channelId": "preview",
                "artifactFileName": artifact_name,
                "artifactSha256": artifact_sha,
                "artifactDigest": f"sha256:{artifact_sha}",
                "readyCheckpoint": "pre_ui_event_loop",
            }
        ),
        encoding="utf-8",
    )
    package_inventory = tmp_path / "chummer-owner-contracts.inventory.json"
    package_ids = (
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
    )
    package_inventory.write_text(
        json.dumps(
            {
                "contract": "chummer-core.owner-contract-package-inventory/v1",
                "package_version": "0.0.0-packageplane.test",
                "packages": [
                    {
                        "id": package_id,
                        "file_name": f"{package_id}.nupkg",
                        "version": "0.0.0-packageplane.test",
                        "sha256": f"{index:064x}",
                        "size_bytes": index,
                    }
                    for index, package_id in enumerate(package_ids, start=1)
                ],
            }
        ),
        encoding="utf-8",
    )

    owners: list[str] = []
    for owner_name in sorted(receipt_tool.OWNER_NAMES):
        owner_path = tmp_path / owner_name
        owner_commit = init_repository(owner_path, owner_name)
        owners.append(f"{owner_name}={owner_path}={owner_commit}")

    policy = receipt_tool.RUNNER_POLICIES[rid]
    environment = {
        "CHUMMER_MACOS_NATIVE_MACHINE": policy["machine"],
        "GITHUB_ACTOR": "tests",
        "GITHUB_EVENT_NAME": "workflow_dispatch",
        "GITHUB_REF": "refs/heads/macos-build",
        "GITHUB_REPOSITORY": receipt_tool.REPOSITORY,
        "GITHUB_RUN_ATTEMPT": "1",
        "GITHUB_RUN_ID": "123",
        "GITHUB_SHA": source_commit,
        "ImageOS": "macos15",
        "ImageVersion": "20260820.1",
        "RUNNER_ARCH": policy["runnerArch"],
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
    }
    args = argparse.Namespace(
        artifact=artifact,
        signing_receipt=signing_receipt,
        startup_receipt=startup_receipt,
        package_inventory=package_inventory,
        source_repo=source_root,
        owner=owners,
        rid=rid,
        release_version=version,
        runner_label=policy["label"],
        output=tmp_path / "output.json",
    )
    return args, environment


@pytest.mark.parametrize("rid", ["osx-arm64", "osx-x64"])
def test_receipt_binds_native_runner_source_owners_and_unsigned_boundary(
    tmp_path: Path, rid: str
) -> None:
    args, environment = candidate(tmp_path, rid)

    receipt = receipt_tool.build_receipt(args, environment)

    assert receipt["contractName"] == receipt_tool.CONTRACT_NAME
    assert receipt["runner"]["label"] == receipt_tool.RUNNER_POLICIES[rid]["label"]
    assert receipt["source"]["commit"] == environment["GITHUB_SHA"]
    assert set(receipt["owners"]) == receipt_tool.OWNER_NAMES
    assert receipt["signing"]["status"] == "unsigned_internal_build"
    assert receipt["signing"]["notarization"] == "not_requested"
    assert receipt["distribution"] == {
        "actionsArtifactOnly": True,
        "deployed": False,
        "publishedRelease": False,
        "releaseEligible": False,
    }
    assert receipt["build"]["packagingDeterministic"] is False
    assert receipt["packagePlane"]["packageCount"] == 4
    assert receipt["artifact"]["sha256"] == receipt_tool.sha256_file(args.artifact)


def test_receipt_rejects_cross_architecture_or_false_signing_claim(tmp_path: Path) -> None:
    args, environment = candidate(tmp_path)
    environment["RUNNER_ARCH"] = "X64"
    with pytest.raises(receipt_tool.ReceiptError, match="RUNNER_ARCH"):
        receipt_tool.build_receipt(args, environment)

    args, environment = candidate(tmp_path / "second")
    signing = json.loads(args.signing_receipt.read_text(encoding="utf-8"))
    signing["signingStatus"] = "pass"
    args.signing_receipt.write_text(json.dumps(signing), encoding="utf-8")
    with pytest.raises(receipt_tool.ReceiptError, match="unsigned preview posture"):
        receipt_tool.build_receipt(args, environment)


def test_receipt_rejects_version_not_derived_from_source_sha(tmp_path: Path) -> None:
    args, environment = candidate(tmp_path)
    args.release_version = "0.0.0-ci.sha000000000000"
    with pytest.raises(receipt_tool.ReceiptError, match="exact source commit"):
        receipt_tool.build_receipt(args, environment)


def test_workflow_is_dual_arch_secretless_and_nonpublishing() -> None:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    documentation = DOC.read_text(encoding="utf-8")

    assert "workflow_dispatch" in workflow
    assert "pull_request:" not in workflow
    assert "push:" not in workflow
    assert "macos-15\n" in workflow
    assert "macos-15-intel" in workflow
    assert "osx-arm64" in workflow
    assert "osx-x64" in workflow
    assert "RUNNER_ENVIRONMENT" in workflow
    assert "github-hosted" in workflow
    assert "permissions:\n  contents: read" in workflow
    assert "secrets." not in workflow
    assert "environment:" not in workflow
    assert 'CHUMMER_PACKAGE_PLANE_FAILURE_DIAGNOSTICS: "1"' in workflow
    assert "release-action" not in workflow
    assert "https://chummer.run" not in workflow
    assert "publish-download" not in workflow
    assert "PublishSingleFile=true" in build_script
    assert "CHUMMER_MAC_SIGNING_REQUIRED=0" in build_script
    assert "CHUMMER_MAC_NOTARIZATION_REQUIRED=0" in build_script
    assert "startup-smoke" in build_script
    assert "unsigned and unnotarized" in documentation


def test_workflow_and_builder_use_exact_package_plane_owner_commits() -> None:
    lock = json.loads(PACKAGE_PLANE_LOCK.read_text(encoding="utf-8"))
    workflow = WORKFLOW.read_text(encoding="utf-8")
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    canonical = lock["canonicalOwnerFeed"]
    expected = {
        row["repository"]: row["commit"] for row in canonical["packages"]
    }
    expected[canonical["producerRepository"]] = canonical["producerCommit"]
    expected.update(
        {row["repository"]: row["commit"] for row in lock["owners"]}
    )
    assert expected == {
        "https://github.com/ArchonMegalon/chummer6-core.git": "b375ad0b0e24659e192e0d10911544450d85e68c",
        "https://github.com/ArchonMegalon/chummer6-hub.git": "8e9b2e3e744de5ee6b200e6526815787497beaaa",
        "https://github.com/ArchonMegalon/chummer6-hub-registry.git": "af9a7e19c3bf331e96411dfb8f9e7820a98cab29",
        "https://github.com/ArchonMegalon/chummer6-ui-kit.git": "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
    }
    for commit in expected.values():
        assert commit in workflow
        assert commit in build_script


def test_scripts_parse_and_compile() -> None:
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "export HOME=" not in build_script
    assert "declare -A" not in build_script
    assert "declare -a OWNER_NAMES" in build_script
    package_plane = (
        REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"
    ).read_text(encoding="utf-8")
    assert "failure diagnostics require the secretless local compatibility tree" in package_plane
    assert 'tail -n 400 "$build_log"' in package_plane
    subprocess.run(["bash", "-n", str(BUILD_SCRIPT)], check=True)
    subprocess.run(["python3", "-m", "py_compile", str(SCRIPT)], check=True)
