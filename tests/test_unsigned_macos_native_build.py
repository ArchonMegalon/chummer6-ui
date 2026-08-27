from __future__ import annotations

import argparse
import importlib.util
import io
import json
import subprocess
import tarfile
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize_unsigned_macos_build_receipt.py"
PACKAGE_PLANE_SCRIPT = REPO_ROOT / "scripts" / "prepare_unsigned_macos_package_plane.py"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unsigned-macos-native-build.yml"
BUILD_SCRIPT = REPO_ROOT / "scripts" / "build-unsigned-macos-native.sh"
DOC = REPO_ROOT / "docs" / "UNSIGNED_MACOS_NATIVE_BUILD.md"
PACKAGE_PLANE_LOCK = REPO_ROOT / "config" / "package-plane.lock.json"
MACOS_PACKAGE_PLANE_LOCK = REPO_ROOT / "config" / "unsigned-macos-package-plane.lock.json"
PRESENTATION_PROJECT = REPO_ROOT / "Chummer.Presentation" / "Chummer.Presentation.csproj"


def load_module(name: str, path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


receipt_tool = load_module("unsigned_macos_receipt", SCRIPT)
package_plane_tool = load_module("unsigned_macos_package_plane", PACKAGE_PLANE_SCRIPT)


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
    runtime_machine = "arm64" if rid == "osx-arm64" else "x86_64"
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
    chummer_package_ids = (
        "Chummer.Application",
        "Chummer.Campaign.Contracts",
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Infrastructure",
        "Chummer.Play.Contracts",
        "Chummer.Rulesets.Hosting",
        "Chummer.Rulesets.Sr4",
        "Chummer.Rulesets.Sr5",
        "Chummer.Rulesets.Sr6",
        "Chummer.Run.Contracts",
        "Chummer.Ui.Kit",
    )
    package_rows = [
        {
            "packageId": package_id,
            "version": "0.0.0-packageplane.test",
            "sha256": f"{index:064x}",
            "sizeBytes": index,
            "sourceRole": (
                "linux_authority_source_pack"
                if package_id in {"Chummer.Campaign.Contracts", "Chummer.Ui.Kit"}
                else "core_runtime_handoff"
            ),
        }
        for index, package_id in enumerate(chummer_package_ids, start=1)
    ]
    package_rows.extend(
        {
            "packageId": f"External.Package.{index:02d}",
            "version": "1.0.0",
            "sha256": f"{index + 12:064x}",
            "sizeBytes": index + 12,
            "sourceRole": "locked_external",
        }
        for index in range(1, 33)
    )
    rid_external_ids = (
        f"Microsoft.AspNetCore.App.Runtime.{rid}",
        f"Microsoft.NETCore.App.Host.{rid}",
        f"Microsoft.NETCore.App.Runtime.{rid}",
    )
    for row, package_id in zip(package_rows[-3:], rid_external_ids, strict=True):
        row["packageId"] = package_id
        row["version"] = "10.0.3"
    rid_identity_strings = {f"{package_id}/10.0.3" for package_id in rid_external_ids}
    resolved_identity_strings = sorted(
        {
            f"{row['packageId']}/{row['version']}"
            for row in package_rows
            if f"{row['packageId']}/{row['version']}" not in rid_identity_strings
        },
        key=str.casefold,
    )
    package_resolution = tmp_path / "package-resolution.json"
    package_resolution.write_text(
        json.dumps(
            {
                "assetsSha256": "1" * 64,
                "contract": "chummer6-ui.unsigned-macos-package-resolution/v1",
                "coreAuthority": {
                    "commit": "c85ea198c19c149375913b44b304acd4d6353053",
                    "publicHandoffReceiptSha256": "b76bc1abff184366e04a63d449ded83ae0716b613e4016edd3eae628fd837637",
                    "runtimeSourceCommit": "7599f9f5d46073b589612473472fccb445512fb1",
                    "tree": "ff95794055e514e58aa8ab41a92a1cfcaf712bb5",
                },
                "feedInventorySha256": "2" * 64,
                "localCompatibilityTree": False,
                "manifestSha256": "3" * 64,
                "noSiblingFallback": True,
                "nugetSourcePolicy": "same-run-local-feed-only",
                "packageCacheWasFresh": True,
                "packages": package_rows,
                "resolvedPackageIdentities": resolved_identity_strings,
                "rid": rid,
                "runtime": {
                    "dotnetSdkVersion": "10.0.103",
                    "executableArchitectures": [runtime_machine],
                    "executableSha256": "4" * 64,
                    "framework": "net10.0",
                    "imageOS": "macos15",
                    "imageVersion": "20260820.1",
                    "kernelRelease": "25.0.0",
                    "machine": runtime_machine,
                    "macOSBuildVersion": "25A123",
                    "macOSProductVersion": "16.0",
                    "rid": rid,
                    "selfContained": True,
                },
                "sdkProvidedRidPackageIdentities": sorted(
                    rid_identity_strings, key=str.casefold
                ),
                "status": "pass",
                "uiSource": {
                    "baseCommit": "35e57b5b94334488c27a7a5bae27e0b125eeed85",
                    "recipeCommit": source_commit,
                    "recipeDelta": ["scripts/build-unsigned-macos-native.sh"],
                },
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
    sdk_receipt = tmp_path / "sdk.json"
    sdk_receipt.write_text(
        json.dumps(
            {
                "archive": {
                    "sha512": policy["sdkSha512"],
                    "sizeBytes": policy["sdkSizeBytes"],
                    "source": policy["sdkSource"],
                },
                "contract": "chummer6-ui.unsigned-macos-sdk/v1",
                "rid": rid,
                "status": "pass",
                "version": "10.0.103",
            }
        ),
        encoding="utf-8",
    )
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
        package_resolution=package_resolution,
        sdk_receipt=sdk_receipt,
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
    assert receipt["packagePlane"]["packageCount"] == 44
    assert receipt["packagePlane"]["localCompatibilityTree"] is False
    assert receipt["packagePlane"]["noSiblingFallback"] is True
    assert (
        receipt["sdk"]["archive"]["sha512"]
        == receipt_tool.RUNNER_POLICIES[rid]["sdkSha512"]
    )
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


def test_receipt_rejects_incomplete_or_ambient_package_resolution(tmp_path: Path) -> None:
    args, environment = candidate(tmp_path)
    resolution = json.loads(args.package_resolution.read_text(encoding="utf-8"))
    resolution["noSiblingFallback"] = False
    args.package_resolution.write_text(json.dumps(resolution), encoding="utf-8")
    with pytest.raises(receipt_tool.ReceiptError, match="package resolution receipt"):
        receipt_tool.build_receipt(args, environment)

    args, environment = candidate(tmp_path / "second")
    resolution = json.loads(args.package_resolution.read_text(encoding="utf-8"))
    resolution["packages"].pop()
    args.package_resolution.write_text(json.dumps(resolution), encoding="utf-8")
    with pytest.raises(receipt_tool.ReceiptError, match="exactly 44 packages"):
        receipt_tool.build_receipt(args, environment)

    args, environment = candidate(tmp_path / "third")
    resolution = json.loads(args.package_resolution.read_text(encoding="utf-8"))
    resolution["sdkProvidedRidPackageIdentities"].append("Unexpected.Package/1.0.0")
    args.package_resolution.write_text(json.dumps(resolution), encoding="utf-8")
    with pytest.raises(receipt_tool.ReceiptError, match="resolved/SDK-provided"):
        receipt_tool.build_receipt(args, environment)


def test_sdk_extractor_rejects_links_before_writing_destination(tmp_path: Path) -> None:
    archive = tmp_path / "sdk.tar.gz"
    with tarfile.open(archive, "w:gz") as stream:
        member = tarfile.TarInfo("dotnet")
        member.type = tarfile.SYMTYPE
        member.linkname = "/tmp/not-authority"
        stream.addfile(member)
    raw = archive.read_bytes()
    destination = tmp_path / "sdk"
    with pytest.raises(package_plane_tool.PackagePlaneError, match="link or special"):
        package_plane_tool.extract_sdk_archive(
            archive,
            destination,
            package_plane_tool.hashlib.sha512(raw).hexdigest(),
            len(raw),
        )
    assert not destination.exists()


def test_sdk_extractor_accepts_one_benign_dot_prefix(tmp_path: Path) -> None:
    archive = tmp_path / "sdk.tar.gz"
    with tarfile.open(archive, "w:gz") as stream:
        root = tarfile.TarInfo("./")
        root.type = tarfile.DIRTYPE
        stream.addfile(root)
        raw_file = b"native-host"
        member = tarfile.TarInfo("./dotnet")
        member.mode = 0o755
        member.size = len(raw_file)
        stream.addfile(member, io.BytesIO(raw_file))
    raw = archive.read_bytes()
    destination = tmp_path / "sdk"
    package_plane_tool.extract_sdk_archive(
        archive,
        destination,
        package_plane_tool.hashlib.sha512(raw).hexdigest(),
        len(raw),
    )
    assert (destination / "dotnet").read_bytes() == b"native-host"
    assert (destination / "dotnet").stat().st_mode & 0o111


def test_core_bundle_extractor_rejects_uninventoried_member() -> None:
    payload = io.BytesIO()
    with package_plane_tool.zipfile.ZipFile(payload, "w") as archive:
        archive.writestr("unexpected.txt", b"unexpected")
    receipt = {"bundle": {"member_count": 0, "members": []}}
    with pytest.raises(package_plane_tool.PackagePlaneError, match="member count"):
        package_plane_tool.bundle_members(receipt, payload.getvalue())


def test_package_identity_receipt_uses_canonical_string_order() -> None:
    expected = {
        ("avalonia", "11.3.7"): {"packageId": "Avalonia"},
        ("avalonia.angle.windows.natives", "2.1"): {
            "packageId": "Avalonia.Angle.Windows.Natives"
        },
    }
    assert package_plane_tool.canonical_package_identity_strings(
        expected, set(expected)
    ) == [
        "Avalonia.Angle.Windows.Natives/2.1",
        "Avalonia/11.3.7",
    ]


@pytest.mark.parametrize("rid", ["osx-arm64", "osx-x64"])
def test_external_selection_accepts_digest_only_global_lock_but_keeps_rid_sizes(
    rid: str,
) -> None:
    lock = package_plane_tool.load_lock(MACOS_PACKAGE_PLANE_LOCK, rid)
    rows = package_plane_tool.selected_external_rows(REPO_ROOT, lock, rid)
    assert len(rows) == 32
    global_rows = [row for row in rows if "sizeBytes" not in row]
    dedicated_rows = [row for row in rows if "sizeBytes" in row]
    assert len(global_rows) == 28
    assert len(dedicated_rows) == 4
    assert all(package_plane_tool.SHA256_RE.fullmatch(row["sha256"]) for row in rows)
    assert all(row["sizeBytes"] > 0 for row in dedicated_rows)


def test_workflow_is_dual_arch_secretless_and_nonpublishing() -> None:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    documentation = DOC.read_text(encoding="utf-8")

    assert "workflow_dispatch" in workflow
    assert "pull_request:" not in workflow
    assert "push:" not in workflow
    assert "macos-15\n" in workflow
    assert "macos-15-intel" in workflow
    assert "ubuntu-24.04" in workflow
    assert "osx-arm64" in workflow
    assert "osx-x64" in workflow
    assert "RUNNER_ENVIRONMENT" in workflow
    assert "github-hosted" in workflow
    assert "permissions:\n  contents: read" in workflow
    assert "secrets." not in workflow
    assert "environment:" not in workflow
    assert "release-action" not in workflow
    assert "https://chummer.run" not in workflow
    assert "publish-download" not in workflow
    assert "PublishSingleFile=true" in build_script
    assert "CHUMMER_MAC_SIGNING_REQUIRED=0" in build_script
    assert "CHUMMER_MAC_NOTARIZATION_REQUIRED=0" in build_script
    assert "startup-smoke" in build_script
    assert "unsigned and unnotarized" in documentation
    assert "setup-dotnet" not in workflow
    assert "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1" not in workflow
    assert "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1" not in build_script
    assert "prepare_unsigned_macos_package_plane.py acquire-sdk" in workflow
    assert "validate-owner-feed" in workflow
    assert "validate-source-feed" in workflow
    assert "actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093" in workflow
    assert "bootstrap-owner-contracts-feed.py" in workflow
    assert "bootstrap-owner-contracts-feed.py" not in build_script
    assert "core-owner-feed-packet/feed" in build_script
    assert "core-owner-feed-packet/source-feed" in build_script
    assert "dotnet pack" in workflow
    assert "dotnet pack" not in build_script
    assert "--source-feed" in build_script
    assert "--package-resolution" in build_script
    assert "stage-inactive" in workflow


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
        "https://github.com/ArchonMegalon/chummer6-core.git": "7599f9f5d46073b589612473472fccb445512fb1",
        "https://github.com/ArchonMegalon/chummer6-hub.git": "9af3cec2620e87a3086e6ac503a5730763c3ce4c",
        "https://github.com/ArchonMegalon/chummer6-hub-registry.git": "af9a7e19c3bf331e96411dfb8f9e7820a98cab29",
        "https://github.com/ArchonMegalon/chummer6-ui-kit.git": "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
    }
    mac_lock = json.loads(MACOS_PACKAGE_PLANE_LOCK.read_text(encoding="utf-8"))
    expected_workflow_commits = {
        mac_lock["coreAuthority"]["commit"],
        *(row["commit"] for row in mac_lock["locallyPackedPackages"]),
    }
    assert mac_lock["coreAuthority"]["runtimeSourceCommit"] == expected[
        "https://github.com/ArchonMegalon/chummer6-core.git"
    ]
    assert expected_workflow_commits == {
        "c85ea198c19c149375913b44b304acd4d6353053",
        "9af3cec2620e87a3086e6ac503a5730763c3ce4c",
        "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61",
    }
    for commit in expected_workflow_commits:
        assert commit in workflow
        assert commit in build_script
    assert mac_lock["uiBaseCommit"] in workflow
    assert mac_lock["coreAuthority"]["runtimeSourceCommit"] in workflow


def test_product_may_retain_local_compatibility_but_proof_lane_forbids_it() -> None:
    project = PRESENTATION_PROJECT.read_text(encoding="utf-8")

    assert (
        "../../chummer-core-engine/Chummer.Application/Chummer.Application.csproj"
        in project
    )
    workflow = WORKFLOW.read_text(encoding="utf-8")
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "test ! -e ../chummer-core-engine" in workflow
    assert "Fresh consumer checkout has a forbidden sibling fallback" in build_script
    assert "export CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=0" in build_script
    assert (
        "Condition=\"Exists('$(MSBuildProjectDirectory)/../../chummer-core-engine/"
        "Chummer.Application/Chummer.Application.csproj')\""
        in project
    )


def test_scripts_parse_and_compile() -> None:
    build_script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "export HOME=" not in build_script
    assert "declare -A" not in build_script
    assert "${environment_name,,}" not in build_script
    assert "tr '[:upper:]' '[:lower:]'" in build_script
    package_plane = (
        REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"
    ).read_text(encoding="utf-8")
    assert "failure diagnostics require the secretless local compatibility tree" in package_plane
    assert 'tail -n 400 "$build_log"' in package_plane
    subprocess.run(["bash", "-n", str(BUILD_SCRIPT)], check=True)
    subprocess.run(["python3", "-m", "py_compile", str(SCRIPT)], check=True)
    subprocess.run(["python3", "-m", "py_compile", str(PACKAGE_PLANE_SCRIPT)], check=True)
