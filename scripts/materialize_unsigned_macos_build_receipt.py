#!/usr/bin/env python3
"""Materialize a fail-closed receipt for one unsigned hosted macOS build."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Mapping


CONTRACT_NAME = "chummer6-ui.unsigned-macos-native-build.v2"
WORKFLOW_PATH = ".github/workflows/unsigned-macos-native-build.yml"
REPOSITORY = "ArchonMegalon/chummer6-ui"
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SHA512_PATTERN = re.compile(r"^[0-9a-f]{128}$")
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
RUN_ID_PATTERN = re.compile(r"^[1-9][0-9]*$")
REF_PATTERN = re.compile(r"^refs/(?:heads|tags)/[A-Za-z0-9][A-Za-z0-9._/-]{0,199}$")
LOGIN_PATTERN = re.compile(
    r"^(?:github-actions\[bot\]|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$"
)
IMAGE_VERSION_PATTERN = re.compile(r"^[0-9A-Za-z._-]{1,128}$")
VERSION_PATTERN = re.compile(r"^0\.0\.0-ci\.sha[0-9a-f]{12}$")
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_ARTIFACT_BYTES = 2 * 1024 * 1024 * 1024
OWNER_NAMES = frozenset(
    {
        "chummer-core-authority",
        "chummer-ui-kit",
        "chummer.run-services",
    }
)
RUNNER_POLICIES = {
    "osx-arm64": {
        "label": "macos-15",
        "runnerArch": "ARM64",
        "machine": "arm64",
        "artifact": "chummer-avalonia-osx-arm64-installer.dmg",
        "sdkSha512": "72ad818d165c1a07898b81f9f989d761dff2c7b7b5d21cc2a151621d2fc2081c7bbe066cb59cac654c19373603c7a129f7c7c7a11ce51bd1cdf48e05a4de78ca",
        "sdkSizeBytes": 230937527,
        "sdkSource": "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/dotnet-sdk-10.0.103-osx-arm64.tar.gz",
    },
    "osx-x64": {
        "label": "macos-15-intel",
        "runnerArch": "X64",
        "machine": "x86_64",
        "artifact": "chummer-avalonia-osx-x64-installer.dmg",
        "sdkSha512": "b8c9bd1660b2306c9dacf99bc7932cf68bdd543b850af79202909ec1d43a697a80c9548cd4cb43bd1a85f09239cea78f0996e2024ae3882bf52f19ee23cf031e",
        "sdkSizeBytes": 238610782,
        "sdkSource": "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/dotnet-sdk-10.0.103-osx-x64.tar.gz",
    },
}
RID_PACKAGE_IDENTITIES = {
    "osx-arm64": {
        "Microsoft.AspNetCore.App.Runtime.osx-arm64/10.0.3",
        "Microsoft.NETCore.App.Host.osx-arm64/10.0.3",
        "Microsoft.NETCore.App.Runtime.osx-arm64/10.0.3",
    },
    "osx-x64": {
        "Microsoft.AspNetCore.App.Runtime.osx-x64/10.0.3",
        "Microsoft.NETCore.App.Host.osx-x64/10.0.3",
        "Microsoft.NETCore.App.Runtime.osx-x64/10.0.3",
    },
}


class ReceiptError(ValueError):
    pass


def fail(message: str) -> None:
    raise ReceiptError(message)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def file_inventory(path: Path, label: str) -> dict[str, Any]:
    try:
        metadata = path.lstat()
    except OSError as error:
        fail(f"{label} is unavailable: {error}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode):
        fail(f"{label} must be a regular non-symlink file")
    if metadata.st_size < 1 or metadata.st_size > MAX_ARTIFACT_BYTES:
        fail(f"{label} has an invalid size")
    return {
        "fileName": path.name,
        "sha256": sha256_file(path),
        "sizeBytes": metadata.st_size,
    }


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate key {key}")
        result[key] = value
    return result


def read_json(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    inventory = file_inventory(path, label)
    if inventory["sizeBytes"] > MAX_JSON_BYTES:
        fail(f"{label} is too large")
    raw = path.read_bytes()
    try:
        payload = json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=unique_object,
            parse_constant=lambda value: fail(f"{label} contains {value}"),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"{label} is not strict UTF-8 JSON: {error}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload, raw


def git_value(root: Path, *arguments: str) -> str:
    completed = subprocess.run(
        ("git", "-C", str(root), *arguments),
        check=False,
        capture_output=True,
        text=True,
        timeout=30,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        fail(f"git authority is unavailable for {root.name}")
    return completed.stdout.strip()


def repository_identity(root: Path, expected_commit: str | None) -> dict[str, str]:
    try:
        metadata = root.lstat()
    except OSError as error:
        fail(f"repository root is unavailable: {error}")
    if not stat.S_ISDIR(metadata.st_mode):
        fail("repository root must be a real directory")
    commit = git_value(root, "rev-parse", "HEAD").lower()
    tree = git_value(root, "rev-parse", "HEAD^{tree}").lower()
    if COMMIT_PATTERN.fullmatch(commit) is None or COMMIT_PATTERN.fullmatch(tree) is None:
        fail("repository commit or tree is malformed")
    if expected_commit is not None and commit != expected_commit:
        fail(f"repository commit differs for {root.name}")
    status = subprocess.run(
        ("git", "-C", str(root), "status", "--porcelain=v1", "--untracked-files=no"),
        check=False,
        capture_output=True,
        text=True,
        timeout=30,
    )
    if status.returncode != 0 or status.stdout:
        fail(f"repository tracked source is not clean for {root.name}")
    origin = git_value(root, "remote", "get-url", "origin")
    return {
        "commit": commit,
        "originSha256": hashlib.sha256(origin.encode("utf-8")).hexdigest(),
        "tree": tree,
    }


def parse_owner(value: str) -> tuple[str, Path, str]:
    parts = value.split("=", 2)
    if len(parts) != 3:
        fail("owner binding must be NAME=PATH=COMMIT")
    name, raw_path, commit = parts
    if name not in OWNER_NAMES or COMMIT_PATTERN.fullmatch(commit) is None:
        fail("owner binding name or commit is invalid")
    path = Path(raw_path)
    if not path.is_absolute():
        fail("owner binding path must be absolute")
    return name, path, commit


def require_text(
    environment: Mapping[str, str], name: str, pattern: re.Pattern[str]
) -> str:
    value = environment.get(name, "")
    if pattern.fullmatch(value) is None:
        fail(f"{name} is invalid")
    return value


def runner_identity(
    environment: Mapping[str, str], rid: str, runner_label: str
) -> dict[str, str]:
    policy = RUNNER_POLICIES.get(rid)
    if policy is None or runner_label != policy["label"]:
        fail("RID and hosted runner label do not match the fixed policy")
    expected = {
        "RUNNER_ARCH": policy["runnerArch"],
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
        "ImageOS": "macos15",
    }
    for name, value in expected.items():
        if environment.get(name) != value:
            fail(f"{name} differs from the hosted runner policy")
    image_version = require_text(
        environment, "ImageVersion", IMAGE_VERSION_PATTERN
    )
    machine = environment.get("CHUMMER_MACOS_NATIVE_MACHINE", "")
    if machine != policy["machine"]:
        fail("native machine architecture differs from the RID")
    return {
        "architecture": policy["machine"],
        "environment": "github-hosted",
        "imageOS": "macos15",
        "imageVersion": image_version,
        "label": runner_label,
        "operatingSystem": "macOS",
    }


def github_identity(
    environment: Mapping[str, str], source_commit: str
) -> dict[str, str]:
    values = {
        "actor": require_text(environment, "GITHUB_ACTOR", LOGIN_PATTERN),
        "event": environment.get("GITHUB_EVENT_NAME", ""),
        "ref": require_text(environment, "GITHUB_REF", REF_PATTERN),
        "repository": environment.get("GITHUB_REPOSITORY", ""),
        "runAttempt": require_text(environment, "GITHUB_RUN_ATTEMPT", RUN_ID_PATTERN),
        "runId": require_text(environment, "GITHUB_RUN_ID", RUN_ID_PATTERN),
        "sha": require_text(environment, "GITHUB_SHA", COMMIT_PATTERN),
        "workflow": WORKFLOW_PATH,
    }
    if (
        values["event"] != "workflow_dispatch"
        or values["repository"] != REPOSITORY
        or values["sha"] != source_commit
    ):
        fail("GitHub context is not the exact manual internal-build authority")
    return values


def validate_signing_receipt(
    payload: dict[str, Any], *, rid: str, version: str, artifact: dict[str, Any]
) -> None:
    if (
        payload.get("contractName") != "chummer6-ui.desktop_artifact_signing"
        or payload.get("contractVersion") != 2
        or payload.get("platform") != "macos"
        or payload.get("app") != "avalonia"
        or payload.get("rid") != rid
        or payload.get("releaseChannel") != "preview"
        or payload.get("releaseVersion") != version
        or payload.get("signingStatus") != "skipped_preview"
        or payload.get("notarizationStatus") != "skipped_preview"
    ):
        fail("packaging signing receipt does not prove the unsigned preview posture")
    artifacts = payload.get("artifacts")
    if not isinstance(artifacts, list) or len(artifacts) != 1:
        fail("packaging signing receipt must bind exactly one artifact")
    row = artifacts[0]
    if (
        not isinstance(row, dict)
        or row.get("fileName") != artifact["fileName"]
        or row.get("sha256") != artifact["sha256"]
        or row.get("signingStatus") != "skipped_preview"
        or row.get("notarizationStatus") != "skipped_preview"
    ):
        fail("packaging signing receipt artifact binding differs")


def validate_startup_receipt(
    payload: dict[str, Any], *, rid: str, version: str, artifact: dict[str, Any]
) -> None:
    expected_arch = "arm64" if rid == "osx-arm64" else "x64"
    if (
        payload.get("status") != "pass"
        or payload.get("headId") != "avalonia"
        or payload.get("platform") != "macos"
        or payload.get("arch") != expected_arch
        or payload.get("rid") != rid
        or payload.get("releaseVersion") != version
        or payload.get("channelId") != "preview"
        or payload.get("artifactFileName") != artifact["fileName"]
        or payload.get("artifactSha256") != artifact["sha256"]
        or payload.get("artifactDigest") != f"sha256:{artifact['sha256']}"
        or not isinstance(payload.get("readyCheckpoint"), str)
        or not payload["readyCheckpoint"]
    ):
        fail("startup receipt does not prove the exact native packaged artifact")


def validate_sdk_receipt(payload: dict[str, Any], *, rid: str) -> None:
    policy = RUNNER_POLICIES[rid]
    archive = payload.get("archive")
    if (
        payload.get("contract") != "chummer6-ui.unsigned-macos-sdk/v1"
        or payload.get("status") != "pass"
        or payload.get("rid") != rid
        or payload.get("version") != "10.0.103"
        or not isinstance(archive, dict)
        or archive.get("sha512") != policy["sdkSha512"]
        or archive.get("sizeBytes") != policy["sdkSizeBytes"]
        or archive.get("source") != policy["sdkSource"]
    ):
        fail("digest-locked native SDK receipt is invalid")


def validate_package_resolution(
    payload: dict[str, Any],
    *,
    rid: str,
    source_commit: str,
    runner: dict[str, str],
) -> None:
    policy = RUNNER_POLICIES[rid]
    core = payload.get("coreAuthority")
    source = payload.get("uiSource")
    runtime = payload.get("runtime")
    packages = payload.get("packages")
    resolved_identities = payload.get("resolvedPackageIdentities")
    sdk_provided_identities = payload.get("sdkProvidedRidPackageIdentities")
    if (
        payload.get("contract")
        != "chummer6-ui.unsigned-macos-package-resolution/v1"
        or payload.get("status") != "pass"
        or payload.get("rid") != rid
        or payload.get("localCompatibilityTree") is not False
        or payload.get("noSiblingFallback") is not True
        or payload.get("nugetSourcePolicy") != "same-run-local-feed-only"
        or payload.get("packageCacheWasFresh") is not True
        or SHA256_PATTERN.fullmatch(str(payload.get("assetsSha256") or "")) is None
        or SHA256_PATTERN.fullmatch(str(payload.get("manifestSha256") or "")) is None
        or SHA256_PATTERN.fullmatch(str(payload.get("feedInventorySha256") or ""))
        is None
    ):
        fail("native macOS package resolution receipt is invalid")
    if not isinstance(core, dict) or (
        core.get("commit") != "c85ea198c19c149375913b44b304acd4d6353053"
        or core.get("runtimeSourceCommit")
        != "7599f9f5d46073b589612473472fccb445512fb1"
        or core.get("tree") != "ff95794055e514e58aa8ab41a92a1cfcaf712bb5"
        or core.get("publicHandoffReceiptSha256")
        != "b76bc1abff184366e04a63d449ded83ae0716b613e4016edd3eae628fd837637"
    ):
        fail("native macOS Core package authority differs")
    if not isinstance(source, dict) or (
        source.get("baseCommit")
        != "35e57b5b94334488c27a7a5bae27e0b125eeed85"
        or source.get("recipeCommit") != source_commit
        or not isinstance(source.get("recipeDelta"), list)
        or not source["recipeDelta"]
    ):
        fail("native macOS UI source/recipe authority differs")
    if not isinstance(runtime, dict) or (
        runtime.get("rid") != rid
        or runtime.get("machine") != policy["machine"]
        or runtime.get("dotnetSdkVersion") != "10.0.103"
        or runtime.get("framework") != "net10.0"
        or runtime.get("selfContained") is not True
        or runtime.get("imageOS") != runner["imageOS"]
        or runtime.get("imageVersion") != runner["imageVersion"]
        or runtime.get("executableArchitectures") != [policy["machine"]]
        or SHA256_PATTERN.fullmatch(str(runtime.get("executableSha256") or ""))
        is None
        or not all(
            isinstance(runtime.get(name), str) and runtime[name]
            for name in (
                "kernelRelease",
                "macOSBuildVersion",
                "macOSProductVersion",
            )
        )
    ):
        fail("native macOS architecture/runtime identity differs")
    if not isinstance(packages, list) or len(packages) != 44:
        fail("native macOS package resolution must bind exactly 44 packages")
    identities: set[tuple[str, str]] = set()
    chummer_ids: set[str] = set()
    for row in packages:
        if not isinstance(row, dict):
            fail("native macOS package resolution row is malformed")
        package_id = row.get("packageId")
        version = row.get("version")
        if (
            not isinstance(package_id, str)
            or not package_id
            or not isinstance(version, str)
            or not version
            or SHA256_PATTERN.fullmatch(str(row.get("sha256") or "")) is None
            or not isinstance(row.get("sizeBytes"), int)
            or row["sizeBytes"] < 1
            or row.get("sourceRole")
            not in {
                "core_locked_owner",
                "core_runtime_handoff",
                "linux_authority_source_pack",
                "locked_external",
            }
        ):
            fail("native macOS package resolution row is invalid")
        identity = (package_id.casefold(), version)
        if identity in identities:
            fail("native macOS package resolution repeats an identity")
        identities.add(identity)
        if package_id.startswith("Chummer."):
            chummer_ids.add(package_id)
    expected_chummer_ids = {
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
    }
    if chummer_ids != expected_chummer_ids:
        fail("native macOS Chummer package set differs")
    identity_strings = {f"{package_id}/{version}" for package_id, version in identities}
    expected_rid_identities = {value.casefold() for value in RID_PACKAGE_IDENTITIES[rid]}
    if (
        not isinstance(resolved_identities, list)
        or not expected_rid_identities <= identity_strings
        or resolved_identities != sorted(set(resolved_identities), key=str.casefold)
        or {str(value).casefold() for value in resolved_identities}
        != identity_strings - expected_rid_identities
        or not isinstance(sdk_provided_identities, list)
        or sdk_provided_identities
        != sorted(set(sdk_provided_identities), key=str.casefold)
        or not {str(value).casefold() for value in sdk_provided_identities}
        <= expected_rid_identities
    ):
        fail("native macOS resolved/SDK-provided package identities differ")


def write_json(path: Path, payload: dict[str, Any]) -> None:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail("output must be a new absolute path")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            os.fchmod(stream.fileno(), 0o600)
            stream.write(
                (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
            )
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def build_receipt(args: argparse.Namespace, environment: Mapping[str, str]) -> dict[str, Any]:
    policy = RUNNER_POLICIES.get(args.rid)
    if policy is None:
        fail("RID is unsupported")
    artifact = file_inventory(args.artifact, "DMG artifact")
    if artifact["fileName"] != policy["artifact"]:
        fail("DMG artifact name differs from the RID")
    source = repository_identity(args.source_repo, None)
    if not VERSION_PATTERN.fullmatch(args.release_version):
        fail("release version is not the deterministic source-SHA version")
    if args.release_version != f"0.0.0-ci.sha{source['commit'][:12]}":
        fail("release version does not match the exact source commit")
    github = github_identity(environment, source["commit"])
    runner = runner_identity(environment, args.rid, args.runner_label)

    owner_bindings = [parse_owner(value) for value in args.owner]
    if {name for name, _, _ in owner_bindings} != OWNER_NAMES or len(owner_bindings) != len(OWNER_NAMES):
        fail("exactly one binding for every owner repository is required")
    owners = {
        name: repository_identity(path, expected_commit)
        for name, path, expected_commit in sorted(owner_bindings)
    }

    signing, signing_raw = read_json(args.signing_receipt, "signing receipt")
    startup, startup_raw = read_json(args.startup_receipt, "startup receipt")
    package_resolution, package_resolution_raw = read_json(
        args.package_resolution, "native macOS package resolution"
    )
    sdk_receipt, sdk_receipt_raw = read_json(args.sdk_receipt, "native macOS SDK receipt")
    validate_signing_receipt(signing, rid=args.rid, version=args.release_version, artifact=artifact)
    validate_startup_receipt(startup, rid=args.rid, version=args.release_version, artifact=artifact)
    validate_package_resolution(
        package_resolution,
        rid=args.rid,
        source_commit=source["commit"],
        runner=runner,
    )
    validate_sdk_receipt(sdk_receipt, rid=args.rid)

    return {
        "artifact": artifact,
        "build": {
            "app": "avalonia",
            "configuration": "Release",
            "deterministicCompiler": True,
            "framework": "net10.0",
            "packagingDeterministic": False,
            "packagingDeterministicReason": (
                "hdiutil embeds filesystem metadata; the exact produced bytes are bound by SHA-256"
            ),
            "releaseChannel": "preview",
            "releaseVersion": args.release_version,
            "rid": args.rid,
            "selfContained": True,
        },
        "completedAtUtc": datetime.now(UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "contractName": CONTRACT_NAME,
        "contractVersion": 2,
        "distribution": {
            "actionsArtifactOnly": True,
            "deployed": False,
            "publishedRelease": False,
            "releaseEligible": False,
        },
        "github": github,
        "owners": owners,
        "packagePlane": {
            "coreAuthority": package_resolution["coreAuthority"],
            "feedInventorySha256": package_resolution["feedInventorySha256"],
            "localCompatibilityTree": False,
            "manifestSha256": package_resolution["manifestSha256"],
            "noSiblingFallback": True,
            "nugetSourcePolicy": package_resolution["nugetSourcePolicy"],
            "packageCount": len(package_resolution["packages"]),
            "packages": package_resolution["packages"],
            "resolutionReceiptSha256": hashlib.sha256(package_resolution_raw).hexdigest(),
            "status": "validated",
        },
        "runner": runner,
        "runtime": package_resolution["runtime"],
        "sdk": {
            "receiptSha256": hashlib.sha256(sdk_receipt_raw).hexdigest(),
            "version": sdk_receipt["version"],
            "archive": sdk_receipt["archive"],
        },
        "signing": {
            "developerIdSigning": "not_performed",
            "notarization": "not_requested",
            "receiptSha256": hashlib.sha256(signing_raw).hexdigest(),
            "status": "unsigned_internal_build",
        },
        "source": source,
        "startupSmoke": {
            "readyCheckpoint": startup["readyCheckpoint"],
            "receiptSha256": hashlib.sha256(startup_raw).hexdigest(),
            "status": "pass",
        },
        "status": "pass",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifact", type=Path, required=True)
    parser.add_argument("--signing-receipt", type=Path, required=True)
    parser.add_argument("--startup-receipt", type=Path, required=True)
    parser.add_argument("--package-resolution", type=Path, required=True)
    parser.add_argument("--sdk-receipt", type=Path, required=True)
    parser.add_argument("--source-repo", type=Path, required=True)
    parser.add_argument("--owner", action="append", default=[], required=True)
    parser.add_argument("--rid", choices=sorted(RUNNER_POLICIES), required=True)
    parser.add_argument("--release-version", required=True)
    parser.add_argument("--runner-label", required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        receipt = build_receipt(args, os.environ)
        write_json(args.output, receipt)
    except (OSError, ReceiptError, subprocess.SubprocessError) as error:
        print(f"unsigned-macos-build:error: {error}", file=sys.stderr)
        return 2
    print(f"unsigned-macos-build:receipt={args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
