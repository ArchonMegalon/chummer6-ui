#!/usr/bin/env python3
"""Fail-closed contracts for the stage-only Windows/Linux preview nightly lane.

This helper deliberately has no network or upload capability.  It validates exact
local Git authorities, hydrates an incumbent release shelf without dropping bytes,
stages separately captured native-Windows evidence, and seals a self-describing
bundle after the existing release verifiers have passed.
"""

from __future__ import annotations

import argparse
import binascii
import ctypes
import errno
import hashlib
import importlib.util
import json
import os
import re
import secrets
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import zipfile
import zlib
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


def _load_supply_chain_module():
    path = Path(__file__).resolve().with_name("preview_supply_chain.py")
    spec = importlib.util.spec_from_file_location("preview_supply_chain", path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load preview supply-chain contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules.setdefault(spec.name, module)
    spec.loader.exec_module(module)
    return module


SUPPLY_CHAIN = _load_supply_chain_module()


def _load_publication_scope_module():
    path = Path(__file__).resolve().with_name("preview_nightly_publication_scope.py")
    spec = importlib.util.spec_from_file_location("preview_nightly_publication_scope", path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load preview publication-scope contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules.setdefault(spec.name, module)
    spec.loader.exec_module(module)
    return module


PUBLICATION_SCOPE = _load_publication_scope_module()


CONTRACT_NAME = "chummer6-ui.preview-nightly-stage"
CONTRACT_VERSION = 1
INPUT_CONTRACT_NAME = "chummer6-ui.preview-nightly-stage-inputs"
NATIVE_EVIDENCE_CONTRACT_NAME = "chummer6-ui.preview-nightly-native-windows-evidence"
NATIVE_CAPTURE_CONTRACT_NAME = "chummer6-ui.preview-nightly-native-windows-capture"
NATIVE_CAPTURE_INVENTORY_CONTRACT_NAME = (
    "chummer6-ui.preview-nightly-native-windows-capture-inventory"
)
NATIVE_FINALIZATION_CONTRACT_NAME = "chummer6-ui.preview-nightly-native-windows-finalization"
NATIVE_FINALIZED_INVENTORY_CONTRACT_NAME = (
    "chummer6-ui.preview-nightly-native-windows-finalized-inventory"
)
NATIVE_CAPTURE_FILE_NAME = "WINDOWS_NATIVE_CAPTURE.generated.json"
NATIVE_CAPTURE_INVENTORY_FILE_NAME = "WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json"
NATIVE_FINALIZATION_FILE_NAME = "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
NATIVE_FINALIZED_INVENTORY_FILE_NAME = "WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json"
NATIVE_AUTHENTICODE_RELATIVE_PATH = (
    "authenticode/AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
)
NATIVE_CAPTURE_WORKFLOW = ".github/workflows/windows-native-evidence-capture.yml"
NATIVE_FINALIZATION_WORKFLOW = ".github/workflows/windows-native-evidence-finalize.yml"
CANDIDATE_EXPORT_WORKFLOW = ".github/workflows/preview-nightly-candidate-export.yml"
CANDIDATE_EXPORT_REF = "refs/heads/main"
CANDIDATE_CONTENT_INVENTORY_CONTRACT_NAME = (
    "chummer6-ui.preview-nightly-candidate-content-inventory"
)
CANDIDATE_EXPORT_CONTRACT_NAME = "chummer6-ui.preview-nightly-candidate-export"
CANDIDATE_PROVENANCE_DIRECTORY = "candidate-provenance"
CANDIDATE_CONTENT_INVENTORY_FILE_NAME = (
    "PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json"
)
CANDIDATE_EXPORT_FILE_NAME = "PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json"
CANDIDATE_CONTENT_INVENTORY_PATH = (
    f"{CANDIDATE_PROVENANCE_DIRECTORY}/{CANDIDATE_CONTENT_INVENTORY_FILE_NAME}"
)
CANDIDATE_EXPORT_PATH = (
    f"{CANDIDATE_PROVENANCE_DIRECTORY}/{CANDIDATE_EXPORT_FILE_NAME}"
)
CANDIDATE_MANIFEST_PATH = "RELEASE_CHANNEL.generated.json"
CANDIDATE_CONTENT_PATHS: tuple[str, ...] = (
    CANDIDATE_MANIFEST_PATH,
    "files/chummer-avalonia-win-x64-installer.exe",
    "files/chummer-avalonia-win-x64-payload.zip",
    *SUPPLY_CHAIN.SUPPLY_CHAIN_CONTENT_PATHS,
)
WINDOWS_ONLY_SCOPE_CONTENT_PATHS: tuple[str, ...] = (
    PUBLICATION_SCOPE.PROPOSAL_FILE_NAME,
    PUBLICATION_SCOPE.PUBLICATION_MANIFEST_RELATIVE_PATH,
    PUBLICATION_SCOPE.PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH,
    PUBLICATION_SCOPE.SIGNING_RECEIPT_RELATIVE_PATH,
)
PROMOTED_WINDOWS_HEADS: tuple[str, ...] = ("avalonia",)
# The disposable candidate build supplies exact fresh Windows and Linux evidence.
# The separately sealed incumbent snapshot determines which non-Windows tuples
# survive into the later publication disposition.
REGISTRY_REQUIRED_DESKTOP_PLATFORMS: tuple[str, ...] = ("linux", "windows")
ACTIVE_PREVIEW_DESKTOP_PLATFORMS: tuple[str, ...] = ("linux", "windows")
CANDIDATE_RUNNER_LABEL_RE = re.compile(
    r"^chummer-preview-nightly-export-[a-z0-9]{12,64}$"
)
GITHUB_API_TIMESTAMP_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)
GITHUB_ARTIFACT_CREATED_AT_MAX_FUTURE_SKEW = timedelta(minutes=5)
EVIDENCE_ARCHIVE_MAX_BYTES = 512 * 1024 * 1024
EVIDENCE_ARCHIVE_MAX_FILES = 256
EVIDENCE_ARCHIVE_MAX_MEMBER_BYTES = 256 * 1024 * 1024
EVIDENCE_ARCHIVE_MAX_EXPANDED_BYTES = 512 * 1024 * 1024
EVIDENCE_ARCHIVE_MAX_COMPRESSION_RATIO = 200
SEAL_FILE_NAME = "PREVIEW_NIGHTLY_STAGE_SEAL.generated.json"
INPUT_FILE_NAME = "PREVIEW_NIGHTLY_STAGE_INPUTS.generated.json"
CANDIDATE_FILE_NAME = "PREVIEW_NIGHTLY_STAGE_CANDIDATE.generated.json"
RUN_UPLOAD_CANDIDATE_FILE_NAME = "RELEASE_UPLOAD_CANDIDATE.generated.json"
AUTHORITATIVE_VALIDATION_FILE_NAME = "PREVIEW_NIGHTLY_AUTHORITATIVE_VALIDATION.generated.json"
WINDOWS_VISUAL_PROOF_CONTRACT_NAME = "chummer6-ui.windows_installer_visual_proof"
NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME = "chummer6-ui.native_windows_host_evidence"
RELEASE_MANIFEST_CONTRACT_NAME = "Chummer.Hub.Registry.Contracts"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
GITHUB_WORKFLOW_PATH_RE = re.compile(
    r"^\.github/workflows/[A-Za-z0-9][A-Za-z0-9._-]*\.ya?ml$"
)
GITHUB_FULL_REF_RE = re.compile(
    r"^refs/(?:heads|tags)/[A-Za-z0-9.][A-Za-z0-9._/@+-]{0,238}$"
)
PORTABLE_VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
GITHUB_LOGIN_RE = re.compile(
    r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$"
)
GITHUB_ACTIONS_BOT_LOGIN = "github-actions[bot]"
GITHUB_REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
HOSTED_BOOTSTRAP_SHA256 = "9ab907a19a0536979bf6dbce3d5f8e22f40ec264d91da7b71f810323b6cacf73"
HOSTED_UPLOAD_TOP_LEVEL_FILES: tuple[str, ...] = (
    "releases.json",
    "RELEASE_CHANNEL.generated.json",
    "release-evidence/public-promotion.json",
)
HOSTED_UPLOAD_RECURSIVE_DIRECTORIES: tuple[str, ...] = ("files", "startup-smoke")
UPSTREAM_PROOF_CONTRACTS: dict[str, str] = {
    "uiLocalizationReleaseGate": "chummer6-ui.localization_release_gate",
    "uiLocalReleaseProof": "chummer6-ui.local_release_proof",
    "blazorSelfHostWorkbenchProof": "chummer6-ui.blazor_self_host_workbench_proof",
    "blazorPublicEdgeWorkbenchProof": "chummer6-ui.blazor_public_edge_workbench_proof",
    "blazorBrowserLaneProofSet": "chummer6-ui.blazor_browser_lane_proof_set",
    "uiFlagshipReleaseGate": "chummer6-ui.flagship_ui_release_gate",
    "desktopWorkflowExecutionGate": "chummer6-ui.desktop_workflow_execution_gate",
    "uiWorkflowParity": "chummer6-ui.chummer5a_desktop_workflow_parity",
    "sr4WorkflowParity": "chummer6-ui.sr4_desktop_workflow_parity",
    "sr6WorkflowParity": "chummer6-ui.sr6_desktop_workflow_parity",
}

AUTHORITY_ENVIRONMENTS: tuple[tuple[str, str, str], ...] = (
    ("presentation", "CHUMMER_UI_ROOT", "CHUMMER_UI_EXPECTED_COMMIT"),
    ("core", "CHUMMER_CORE_ROOT", "CHUMMER_CORE_EXPECTED_COMMIT"),
    ("run", "CHUMMER_RUN_ROOT", "CHUMMER_RUN_EXPECTED_COMMIT"),
    ("ui-kit", "CHUMMER_UI_KIT_ROOT", "CHUMMER_UI_KIT_EXPECTED_COMMIT"),
    ("registry", "CHUMMER_HUB_REGISTRY_ROOT", "CHUMMER_HUB_REGISTRY_EXPECTED_COMMIT"),
    ("media-factory", "CHUMMER_MEDIA_FACTORY_ROOT", "CHUMMER_MEDIA_FACTORY_EXPECTED_COMMIT"),
    ("legacy", "CHUMMER_LEGACY_ROOT", "CHUMMER_LEGACY_EXPECTED_COMMIT"),
)

AUTHORITY_SENTINELS: dict[str, str] = {
    "presentation": "Chummer.Presentation/Chummer.Presentation.csproj",
    "core": "Chummer.Contracts/Chummer.Contracts.csproj",
    "run": "Chummer.Run.Contracts/Chummer.Run.Contracts.csproj",
    "ui-kit": "src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj",
    "registry": "Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj",
    "media-factory": "src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj",
    "legacy": "Chummer.sln",
}

AUTHORITATIVE_VALIDATOR_FILES: tuple[tuple[str, str, str], ...] = (
    (
        "registryMaterializer",
        "registry",
        "scripts/materialize_public_release_channel.py",
    ),
    (
        "registryReleaseVerifier",
        "registry",
        "scripts/verify_public_release_channel.py",
    ),
    (
        "windowsDesktopExitGate",
        "presentation",
        "scripts/materialize-windows-desktop-exit-gate.sh",
    ),
    (
        "windowsReleaseEvidence",
        "presentation",
        "scripts/verify-windows-release-evidence.py",
    ),
    (
        "releaseCandidateHandoff",
        "presentation",
        "scripts/materialize_release_candidate_handoff.py",
    ),
    (
        "previewSupplyChain",
        "presentation",
        "scripts/preview_supply_chain.py",
    ),
    (
        "windowsVisualProofHandoff",
        "presentation",
        "scripts/materialize_windows_visual_proof_handoff.py",
    ),
)

CURRENT_NIGHTLY_TUPLES: tuple[tuple[str, str, str], ...] = (
    ("avalonia", "windows", "win-x64"),
    ("avalonia", "linux", "linux-x64"),
)
CURRENT_NIGHTLY_ARTIFACT_IDENTITIES: dict[
    tuple[str, str, str], tuple[str, str]
] = {
    ("avalonia", "windows", "win-x64"): (
        "avalonia-win-x64-installer",
        "chummer-avalonia-win-x64-installer.exe",
    ),
    ("avalonia", "linux", "linux-x64"): (
        "avalonia-linux-x64-installer",
        "chummer-avalonia-linux-x64-installer.deb",
    ),
}

EXACT_PROOF_INPUTS: tuple[tuple[str, str, str, str], ...] = (
    ("hubLocalReleaseProof", "CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH", "CHUMMER_HUB_LOCAL_RELEASE_PROOF_SHA256", "HUB_LOCAL_RELEASE_PROOF.generated.json"),
    ("uiLocalizationReleaseGate", "CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH", "CHUMMER_UI_LOCALIZATION_RELEASE_GATE_SHA256", "UI_LOCALIZATION_RELEASE_GATE.generated.json"),
    ("uiLocalReleaseProof", "CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH", "CHUMMER_UI_LOCAL_RELEASE_PROOF_SHA256", "UI_LOCAL_RELEASE_PROOF.generated.json"),
    ("blazorSelfHostWorkbenchProof", "CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH", "CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_SHA256", "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"),
    ("blazorPublicEdgeWorkbenchProof", "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH", "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_SHA256", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
    ("blazorBrowserLaneProofSet", "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH", "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_SHA256", "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"),
    ("uiFlagshipReleaseGate", "CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH", "CHUMMER_UI_FLAGSHIP_RELEASE_GATE_SHA256", "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
    ("desktopWorkflowExecutionGate", "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH", "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_SHA256", "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"),
    ("uiWorkflowParity", "CHUMMER_UI_WORKFLOW_PARITY_PATH", "CHUMMER_UI_WORKFLOW_PARITY_SHA256", "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"),
    ("sr4WorkflowParity", "CHUMMER_SR4_WORKFLOW_PARITY_PATH", "CHUMMER_SR4_WORKFLOW_PARITY_SHA256", "SR4_DESKTOP_WORKFLOW_PARITY.generated.json"),
    ("sr6WorkflowParity", "CHUMMER_SR6_WORKFLOW_PARITY_PATH", "CHUMMER_SR6_WORKFLOW_PARITY_SHA256", "SR6_DESKTOP_WORKFLOW_PARITY.generated.json"),
)


class ContractError(RuntimeError):
    """Raised for an operator-correctable, fail-closed contract violation."""


def normalize(value: object) -> str:
    return str(value or "").strip()


def fail(message: str) -> None:
    raise ContractError(message)


def is_exact_github_full_ref(value: object) -> bool:
    if not isinstance(value, str) or value != value.strip():
        return False
    components = value.split("/")
    return bool(
        GITHUB_FULL_REF_RE.fullmatch(value)
        and "//" not in value
        and ".." not in value
        and "@{" not in value
        and not value.endswith(".")
        and all(
            component
            and not component.startswith(".")
            and not component.endswith(".lock")
            for component in components[2:]
        )
    )


def is_exact_github_actor_login(value: object) -> bool:
    return isinstance(value, str) and (
        value == GITHUB_ACTIONS_BOT_LOGIN or bool(GITHUB_LOGIN_RE.fullmatch(value))
    )


def github_workflow_run_path_matches(
    actual_path: object,
    bare_path: str,
    *,
    branch: object,
    ref: object,
    sha: object,
) -> bool:
    """Match the exact workflow-run path shapes returned by GitHub's REST API."""
    if not isinstance(actual_path, str) or not GITHUB_WORKFLOW_PATH_RE.fullmatch(bare_path):
        return False
    if not isinstance(branch, str) or not branch or branch != branch.strip():
        return False
    if not is_exact_github_full_ref(ref):
        return False
    if not isinstance(sha, str) or not COMMIT_RE.fullmatch(sha):
        return False
    branch_value = branch
    ref_value = ref
    sha_value = sha
    if ref_value not in {
        f"refs/heads/{branch_value}",
        f"refs/tags/{branch_value}",
    }:
        return False
    if actual_path == bare_path:
        return True
    prefix = f"{bare_path}@"
    if not actual_path.startswith(prefix):
        return False
    suffix = actual_path[len(prefix) :]
    return suffix in {branch_value, ref_value, sha_value}


def read_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid JSON object at {path}: {exc}")
    if not isinstance(payload, dict):
        fail(f"expected a JSON object at {path}")
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_json_object(payload: dict[str, Any]) -> str:
    canonical = json.dumps(
        payload,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def directory_identity(path: Path) -> dict[str, int]:
    if not path.is_absolute():
        fail("directory identity path must be absolute")
    try:
        info = path.lstat()
    except OSError as exc:
        fail(f"directory identity path is unavailable: {path}: {exc}")
    if path.is_symlink() or not stat.S_ISDIR(info.st_mode):
        fail(f"directory identity path must be a non-symlink directory: {path}")
    return {"device": info.st_dev, "inode": info.st_ino}


def _renameat2_no_replace(
    source_parent_fd: int,
    source_name: str,
    destination_parent_fd: int,
    destination_name: str,
) -> int:
    """Return errno from Linux renameat2(RENAME_NOREPLACE), or zero on success."""
    try:
        libc = ctypes.CDLL(None, use_errno=True)
        renameat2 = libc.renameat2
    except (OSError, AttributeError) as exc:
        fail(f"atomic no-replace directory installation is unavailable: {exc}")
    renameat2.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    renameat2.restype = ctypes.c_int
    result = renameat2(
        source_parent_fd,
        os.fsencode(source_name),
        destination_parent_fd,
        os.fsencode(destination_name),
        1,  # RENAME_NOREPLACE
    )
    return 0 if result == 0 else ctypes.get_errno()


def atomic_install_directory_no_replace(
    source: Path,
    destination: Path,
    *,
    expected_device: int,
    expected_inode: int,
) -> dict[str, int]:
    """Atomically install the exact owned directory without replacing a target."""
    expected_identity = {"device": expected_device, "inode": expected_inode}
    if directory_identity(source) != expected_identity:
        fail("atomic install source identity changed before installation")
    if not destination.is_absolute():
        fail("atomic install destination must be absolute")
    if not destination.parent.is_dir() or destination.parent.is_symlink():
        fail("atomic install destination parent must be a non-symlink directory")
    if source.parent == source or destination.parent == destination:
        fail("atomic install paths must name directory entries")
    open_flags = os.O_RDONLY | os.O_DIRECTORY
    if hasattr(os, "O_NOFOLLOW"):
        open_flags |= os.O_NOFOLLOW
    source_fd: int | None = None
    source_parent_fd: int | None = None
    destination_parent_fd: int | None = None
    try:
        source_fd = os.open(source, open_flags)
        source_parent_fd = os.open(source.parent, open_flags)
        destination_parent_fd = os.open(destination.parent, open_flags)
    except OSError as exc:
        for descriptor in (destination_parent_fd, source_parent_fd, source_fd):
            if descriptor is not None:
                os.close(descriptor)
        fail(f"could not pin atomic install directory identities: {exc}")
    assert source_fd is not None
    assert source_parent_fd is not None
    assert destination_parent_fd is not None
    try:
        source_info = os.fstat(source_fd)
        opened_identity = {"device": source_info.st_dev, "inode": source_info.st_ino}
        if opened_identity != expected_identity or directory_identity(source) != expected_identity:
            fail("atomic install source identity changed before installation")
        error_number = _renameat2_no_replace(
            source_parent_fd,
            source.name,
            destination_parent_fd,
            destination.name,
        )
        if error_number == 0:
            installed_identity = directory_identity(destination)
            if installed_identity != expected_identity:
                quarantine: Path | None = None
                for suffix in range(128):
                    candidate = destination.with_name(
                        f".{destination.name}.rejected.{os.getpid()}.{suffix}"
                    )
                    quarantine_error = _renameat2_no_replace(
                        destination_parent_fd,
                        destination.name,
                        destination_parent_fd,
                        candidate.name,
                    )
                    if quarantine_error == 0:
                        quarantine = candidate
                        break
                    if quarantine_error not in {errno.EEXIST, errno.ENOTEMPTY}:
                        break
                if quarantine is None:
                    fail(
                        "atomic install destination identity differs from the sealed source "
                        "and could not be quarantined"
                    )
                fail(
                    "atomic install destination identity differs from the sealed source; "
                    f"untrusted entry quarantined at {quarantine}"
                )
            return installed_identity
        if error_number in {errno.EEXIST, errno.ENOTEMPTY}:
            fail(f"atomic install destination already exists: {destination}")
        fail(
            "atomic no-replace directory installation failed: "
            f"{source} -> {destination}: {os.strerror(error_number)}"
        )
    finally:
        os.close(destination_parent_fd)
        os.close(source_parent_fd)
        os.close(source_fd)


def install_verified_sealed_directory_no_replace(
    source: Path,
    destination: Path,
    *,
    expected_device: int,
    expected_inode: int,
    expected_tree_sha256: str,
) -> dict[str, Any]:
    """Install once, then revalidate exact bytes at the irreversible boundary."""
    expected_tree = require_sha256(expected_tree_sha256, "installed stage tree sha256")
    verify_seal(source)
    before = digest_tree(
        source,
        expected_device=expected_device,
        expected_inode=expected_inode,
    )
    if before["treeSha256"] != expected_tree:
        fail("sealed install source tree differs from the caller-bound tree")
    installed = atomic_install_directory_no_replace(
        source,
        destination,
        expected_device=expected_device,
        expected_inode=expected_inode,
    )
    try:
        after = digest_tree(
            destination,
            expected_device=expected_device,
            expected_inode=expected_inode,
        )
        if after["treeSha256"] != expected_tree or after["fileCount"] != before["fileCount"]:
            fail("installed sealed stage tree differs at the no-replace boundary")
        verify_seal(destination)
    except Exception as validation_error:
        quarantine = destination.with_name(
            f".{destination.name}.rejected.{os.getpid()}.{secrets.token_hex(16)}"
        )
        try:
            consume_owned_directory(
                destination,
                quarantine,
                expected_device=expected_device,
                expected_inode=expected_inode,
            )
        except (ContractError, OSError) as cleanup_error:
            fail(
                "installed sealed stage failed boundary validation and exact-target cleanup failed: "
                f"validation={validation_error}; cleanup={cleanup_error}"
            )
        fail(f"installed sealed stage failed boundary validation and was removed: {validation_error}")
    return {**installed, "treeSha256": expected_tree, "fileCount": before["fileCount"]}


def consume_owned_directory(
    source: Path,
    quarantine: Path,
    *,
    expected_device: int,
    expected_inode: int,
) -> dict[str, Any]:
    """Move an exact owned directory into a private tombstone before deletion."""
    expected = {"device": expected_device, "inode": expected_inode}
    if directory_identity(source) != expected:
        fail("candidate identity changed before owned cleanup")
    if not quarantine.is_absolute() or quarantine.exists() or quarantine.is_symlink():
        fail("candidate cleanup quarantine must be an absent absolute path")
    quarantine.mkdir(mode=0o700)
    tombstone = quarantine / "owned-candidate"
    moved = False
    try:
        installed = atomic_install_directory_no_replace(
            source,
            tombstone,
            expected_device=expected_device,
            expected_inode=expected_inode,
        )
        moved = True
        if installed != expected:
            fail("candidate identity changed during owned cleanup quarantine")
        if not getattr(shutil.rmtree, "avoids_symlink_attacks", False):
            fail("platform does not provide symlink-safe recursive deletion")
        shutil.rmtree(tombstone)
        quarantine.rmdir()
    except (OSError, ContractError) as exc:
        if not moved:
            try:
                quarantine.rmdir()
            except OSError:
                pass
        if isinstance(exc, ContractError):
            raise
        fail(f"owned candidate cleanup failed: {exc}")
    return {"status": "consumed", **expected}


def require_sha256(value: str, label: str) -> str:
    normalized = normalize(value).lower()
    if not SHA256_RE.fullmatch(normalized):
        fail(f"{label} must be an exact lowercase SHA-256")
    return normalized


def require_exact_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        fail(f"{label} must be an exact lowercase SHA-256")
    return value


def require_exact_positive_integer(value: object, label: str) -> str:
    if not isinstance(value, str) or not POSITIVE_INTEGER_RE.fullmatch(value):
        fail(f"{label} must be an exact positive decimal string")
    return value


def parse_exact_github_api_timestamp(value: object, label: str) -> datetime:
    if not isinstance(value, str) or not GITHUB_API_TIMESTAMP_RE.fullmatch(value):
        fail(f"{label} must be an exact GitHub API UTC timestamp")
    try:
        parsed = datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=UTC)
    except ValueError as exc:
        fail(f"{label} is not a valid GitHub API UTC timestamp: {exc}")
    return parsed


def validate_github_artifact_time_window(
    created_at: object,
    expires_at: object,
    *,
    label: str,
) -> tuple[datetime, datetime]:
    created = parse_exact_github_api_timestamp(created_at, f"{label} creation")
    expires = parse_exact_github_api_timestamp(expires_at, f"{label} expiry")
    now = datetime.now(UTC)
    if created > now + GITHUB_ARTIFACT_CREATED_AT_MAX_FUTURE_SKEW:
        fail(f"{label} creation is more than five minutes in the future")
    if created >= expires or expires <= now:
        fail(f"{label} timestamps are expired or out of order")
    return created, expires


def require_local_regular_file(path_text: str, label: str) -> Path:
    if not path_text:
        fail(f"{label} is required")
    path = Path(path_text)
    if not path.is_absolute():
        fail(f"{label} must be an absolute local path")
    if path.is_symlink() or not path.is_file():
        fail(f"{label} must be a regular non-symlink file: {path}")
    mode = path.stat().st_mode
    if not stat.S_ISREG(mode):
        fail(f"{label} must be a regular file: {path}")
    return path.resolve(strict=True)


def require_exact_file(path_env: str, sha_env: str, label: str) -> Path:
    path = require_local_regular_file(normalize(os.environ.get(path_env)), path_env)
    expected = require_sha256(normalize(os.environ.get(sha_env)), sha_env)
    actual = sha256_file(path)
    if actual != expected:
        fail(f"{label} digest mismatch: expected {expected}, got {actual}")
    return path


def run_git(root: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(root), *args],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip() or f"status {completed.returncode}"
        fail(f"git {' '.join(args)} failed for {root}: {detail}")
    return completed.stdout.strip()


def committed_file_bytes(root: Path, commit: str, relative: str) -> bytes:
    """Read exact committed file bytes without trusting the mutable worktree."""
    if not COMMIT_RE.fullmatch(commit):
        fail(f"validator authority commit is malformed for {relative}")
    completed = subprocess.run(
        ["git", "-C", str(root), "show", f"{commit}:{relative}"],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        detail = completed.stderr.decode("utf-8", errors="replace").strip()
        fail(f"could not read committed validator bytes for {relative}: {detail}")
    return completed.stdout


def committed_file_sha256(root: Path, commit: str, relative: str) -> str:
    """Hash exact committed file bytes without trusting the mutable worktree."""
    return hashlib.sha256(committed_file_bytes(root, commit, relative)).hexdigest()


def require_committed_authority_file(
    root: Path,
    commit: str,
    relative: str,
    label: str,
) -> dict[str, str]:
    path = root / relative
    if path.is_symlink() or not path.is_file():
        fail(f"pinned {label} is missing: {relative}")
    expected = committed_file_sha256(root, commit, relative)
    actual = sha256_file(path)
    if actual != expected:
        fail(
            f"pinned {label} worktree bytes differ from git {commit}:{relative}: "
            f"{actual} != {expected}"
        )
    return {"authorityCommit": commit, "sha256": expected}


def revalidate_authoritative_validator_sources(
    presentation_root: Path,
    authorities: list[dict[str, str]],
) -> dict[str, dict[str, str]]:
    """Recheck clean authority state and every validator blob immediately at use."""
    current = validate_authorities(presentation_root)
    if current != authorities:
        fail("repository authorities changed at authoritative validator use")
    commits = {
        normalize(row.get("name")): normalize(row.get("commit"))
        for row in authorities
        if isinstance(row, dict)
    }
    roots = {
        name: Path(normalize(os.environ[root_env])).resolve(strict=True)
        for name, root_env, _ in AUTHORITY_ENVIRONMENTS
    }
    bindings: dict[str, dict[str, str]] = {}
    for source_name, authority_name, relative in AUTHORITATIVE_VALIDATOR_FILES:
        bindings[source_name] = require_committed_authority_file(
            roots[authority_name],
            commits.get(authority_name, ""),
            relative,
            source_name,
        )
    return bindings


def materialize_authoritative_validator_snapshot(
    snapshot_root: Path,
    authorities: list[dict[str, str]],
    validator_bindings: dict[str, dict[str, str]],
) -> dict[str, Path]:
    """Materialize immutable-by-contract validator inputs into a private snapshot."""
    if snapshot_root.is_symlink() or not snapshot_root.is_dir():
        fail("authoritative validator snapshot root must be a real directory")
    snapshot_root.chmod(0o700)
    if stat.S_IMODE(snapshot_root.stat().st_mode) != 0o700:
        fail("authoritative validator snapshot root must have mode 0700")

    commits = {
        normalize(row.get("name")): normalize(row.get("commit"))
        for row in authorities
        if isinstance(row, dict)
    }
    roots = {
        name: Path(normalize(os.environ[root_env])).resolve(strict=True)
        for name, root_env, _ in AUTHORITY_ENVIRONMENTS
    }
    expected_names = {row[0] for row in AUTHORITATIVE_VALIDATOR_FILES}
    if set(validator_bindings) != expected_names:
        fail("authoritative validator bindings are incomplete")

    materialized: dict[str, Path] = {}
    for source_name, authority_name, relative in AUTHORITATIVE_VALIDATOR_FILES:
        relative_path = PurePosixPath(relative)
        if relative_path.is_absolute() or not relative_path.parts or ".." in relative_path.parts:
            fail(f"authoritative validator path is unsafe: {relative}")
        commit = commits.get(authority_name, "")
        binding = validator_bindings.get(source_name)
        if (
            not isinstance(binding, dict)
            or set(binding) != {"authorityCommit", "sha256"}
            or binding.get("authorityCommit") != commit
        ):
            fail(f"authoritative validator binding is malformed for {source_name}")
        expected_sha256 = require_sha256(
            normalize(binding.get("sha256")), f"{source_name} committed validator sha256"
        )
        committed_bytes = committed_file_bytes(roots[authority_name], commit, relative)
        actual_sha256 = hashlib.sha256(committed_bytes).hexdigest()
        if actual_sha256 != expected_sha256:
            fail(
                f"committed validator bytes changed while snapshotting {source_name}: "
                f"{actual_sha256} != {expected_sha256}"
            )

        destination = snapshot_root / authority_name / Path(*relative_path.parts)
        current = snapshot_root
        for part in (authority_name, *relative_path.parts[:-1]):
            current /= part
            current.mkdir(mode=0o700, exist_ok=True)
            if current.is_symlink() or not current.is_dir():
                fail(f"authoritative validator snapshot directory is unsafe: {current}")
            current.chmod(0o700)
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_NOFOLLOW", 0)
        try:
            descriptor = os.open(destination, flags, 0o600)
        except OSError as exc:
            fail(f"could not create authoritative validator snapshot file {source_name}: {exc}")
        try:
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(committed_bytes)
                stream.flush()
                os.fsync(stream.fileno())
        except Exception:
            try:
                destination.unlink(missing_ok=True)
            except OSError:
                pass
            raise
        if destination.is_symlink() or not destination.is_file():
            fail(f"authoritative validator snapshot file is unsafe: {source_name}")
        if sha256_file(destination) != expected_sha256:
            fail(f"authoritative validator snapshot digest mismatch for {source_name}")
        materialized[source_name] = destination

    if set(materialized) != expected_names:
        fail("authoritative validator snapshot is incomplete")
    return materialized


def normalize_generated_snapshot_paths(
    path: Path,
    replacements: tuple[tuple[Path, Path], ...],
) -> None:
    """Replace ephemeral snapshot prefixes in generated text with stable authorities."""
    if not path.is_file():
        return
    payload = path.read_bytes()
    for ephemeral, stable in replacements:
        ephemeral_bytes = str(ephemeral).encode("utf-8")
        payload = payload.replace(ephemeral_bytes, str(stable).encode("utf-8"))
        if ephemeral_bytes in payload:
            fail(f"generated replay output retained an ephemeral validator path: {path}")
    path.write_bytes(payload)


def github_repository_slug(root: Path) -> str:
    remote = run_git(root, "remote", "get-url", "origin")
    match = re.fullmatch(
        r"(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)([^/\s]+)/([^/\s]+?)(?:\.git)?",
        remote,
        flags=re.IGNORECASE,
    )
    if not match:
        fail("presentation authority origin must be an exact GitHub repository URL")
    slug = f"{match.group(1)}/{match.group(2)}"
    if not GITHUB_REPOSITORY_RE.fullmatch(slug):
        fail("presentation authority GitHub repository slug is malformed")
    return slug


def native_evidence_authority(
    presentation_root: Path,
    authorities: list[dict[str, str]],
) -> dict[str, Any]:
    presentation_commit = next(
        (
            normalize(row.get("commit"))
            for row in authorities
            if isinstance(row, dict) and normalize(row.get("name")) == "presentation"
        ),
        "",
    )
    if not COMMIT_RE.fullmatch(presentation_commit):
        fail("presentation authority commit is unavailable for native evidence")
    workflows: dict[str, dict[str, str]] = {}
    for role, relative in (
        ("candidateExport", CANDIDATE_EXPORT_WORKFLOW),
        ("capture", NATIVE_CAPTURE_WORKFLOW),
        ("finalization", NATIVE_FINALIZATION_WORKFLOW),
    ):
        binding = require_committed_authority_file(
            presentation_root,
            presentation_commit,
            relative,
            f"native Windows {role} workflow",
        )
        workflows[role] = {
            "path": relative,
            "authorityCommit": binding["authorityCommit"],
            "sha256": binding["sha256"],
        }
    return {
        "repository": github_repository_slug(presentation_root),
        "presentationCommit": presentation_commit,
        "workflows": workflows,
    }


def validate_authorities(presentation_root: Path) -> list[dict[str, str]]:
    expected_presentation = presentation_root.resolve(strict=True)
    authorities: list[dict[str, str]] = []
    resolved_roots: set[Path] = set()
    for name, root_env, commit_env in AUTHORITY_ENVIRONMENTS:
        root_text = normalize(os.environ.get(root_env))
        if not root_text:
            fail(f"{root_env} is required; mutable repository defaults are forbidden")
        root_path = Path(root_text)
        if not root_path.is_absolute() or root_path.is_symlink() or not root_path.is_dir():
            fail(f"{root_env} must name an absolute, existing, non-symlink repository root")
        root = root_path.resolve(strict=True)
        if root != root_path:
            fail(f"{root_env} must already be a physical canonical path: {root_path} -> {root}")
        if root in resolved_roots:
            fail(f"repository authority roots must be distinct; duplicate root: {root}")
        resolved_roots.add(root)

        expected_commit = normalize(os.environ.get(commit_env)).lower()
        if not COMMIT_RE.fullmatch(expected_commit):
            fail(f"{commit_env} must be an exact lowercase 40-character commit")
        git_root = Path(run_git(root, "rev-parse", "--show-toplevel")).resolve(strict=True)
        if git_root != root:
            fail(f"{root_env} is not the repository top-level: {root} (git top-level {git_root})")
        actual_commit = run_git(root, "rev-parse", "HEAD").lower()
        if actual_commit != expected_commit:
            fail(f"{name} authority drift: expected {expected_commit}, got {actual_commit}")
        dirty = run_git(root, "status", "--porcelain=v1", "--untracked-files=all")
        if dirty:
            fail(f"{name} authority root is not clean")
        sentinel = root / AUTHORITY_SENTINELS[name]
        if sentinel.is_symlink() or not sentinel.is_file():
            fail(f"{name} authority repository identity sentinel is missing: {AUTHORITY_SENTINELS[name]}")
        tracked = run_git(root, "ls-files", "--error-unmatch", AUTHORITY_SENTINELS[name])
        if tracked != AUTHORITY_SENTINELS[name]:
            fail(f"{name} authority repository identity sentinel is not tracked")
        authorities.append({"name": name, "commit": actual_commit})

    presentation = next(item for item in authorities if item["name"] == "presentation")
    configured_presentation_root = Path(normalize(os.environ["CHUMMER_UI_ROOT"])).resolve(strict=True)
    if configured_presentation_root != expected_presentation:
        fail(
            "CHUMMER_UI_ROOT must be the repository containing this orchestrator: "
            f"{configured_presentation_root} != {expected_presentation}"
        )
    if not presentation["commit"]:
        fail("presentation authority was not recorded")
    workspace_root = expected_presentation.parent
    consumed_paths = {
        "core": workspace_root / "chummer-core-engine",
        "run": workspace_root / "chummer.run-services",
        "ui-kit": workspace_root / "chummer-ui-kit",
        "registry": workspace_root / "chummer-hub-registry",
        "media-factory": workspace_root / "fleet" / "repos" / "chummer-media-factory",
        "legacy": workspace_root.parent / "chummer5a",
    }
    try:
        expected_consumed_roots = {
            name: path.resolve(strict=True) for name, path in consumed_paths.items()
        }
    except OSError as exc:
        fail(f"compatibility-tree repository layout is incomplete: {exc}")
    configured_roots = {
        name: Path(normalize(os.environ[root_env])).resolve(strict=True)
        for name, root_env, _ in AUTHORITY_ENVIRONMENTS
    }
    for name, expected_root in expected_consumed_roots.items():
        if configured_roots[name] != expected_root:
            fail(
                f"{name} authority does not match the compatibility-tree path consumed by the build: "
                f"{configured_roots[name]} != {expected_root}"
            )
    return authorities


def parse_published_at(value: str) -> str:
    if not value.endswith("Z"):
        fail("CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT must be an explicit UTC RFC3339 timestamp ending in Z")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        fail(f"invalid CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT: {exc}")
    if parsed.tzinfo is None or parsed.utcoffset() != datetime.now(UTC).utcoffset():
        fail("CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT must be UTC")
    return parsed.replace(microsecond=0).isoformat().replace("+00:00", "Z")


def validate_paths_and_identity(candidate_dir: Path) -> tuple[str, str, Path]:
    version = normalize(os.environ.get("CHUMMER_PREVIEW_NIGHTLY_VERSION"))
    if not PORTABLE_VERSION_RE.fullmatch(version):
        fail("CHUMMER_PREVIEW_NIGHTLY_VERSION must be an explicit portable version token")
    published_at = parse_published_at(normalize(os.environ.get("CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT")))
    configured_candidate = normalize(os.environ.get("CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR"))
    configured_stage = normalize(os.environ.get("CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR"))
    if not configured_candidate or not configured_stage:
        fail("CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR and CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR are required")
    candidate_path = Path(configured_candidate)
    stage_path = Path(configured_stage)
    if not candidate_path.is_absolute() or not stage_path.is_absolute():
        fail("candidate and final stage paths must be absolute")
    if candidate_path.resolve(strict=False) != candidate_dir.resolve(strict=True):
        fail("candidate directory argument does not match CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR")
    if candidate_path.parent.resolve(strict=True) != stage_path.parent.resolve(strict=True):
        fail("candidate and final stage must share one parent for atomic sealing")
    if candidate_path == stage_path:
        fail("candidate and final stage paths must differ")
    if stage_path.exists():
        fail(f"final stage path already exists: {stage_path}")
    if candidate_path.name != f".nightly-run-{version}.candidate":
        fail("candidate basename must be .nightly-run-<version>.candidate")
    if stage_path.name != f"nightly-run-{version}":
        fail("final stage basename must be nightly-run-<version>")
    return version, published_at, stage_path


def safe_tree_entries(root: Path) -> list[Path]:
    entries: list[Path] = []
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if path.is_symlink():
            fail(f"symlinks are forbidden in staged evidence/shelf trees: {path}")
        mode = path.stat().st_mode
        if path.is_dir():
            continue
        if not stat.S_ISREG(mode):
            fail(f"special files are forbidden in staged evidence/shelf trees: {path}")
        entries.append(path)
    return entries


def inventory_tree(root: Path, *, exclusions: Iterable[str] = ()) -> list[dict[str, Any]]:
    excluded = set(exclusions)
    rows: list[dict[str, Any]] = []
    for path in safe_tree_entries(root):
        relative = path.relative_to(root).as_posix()
        if relative in excluded:
            continue
        rows.append({"path": relative, "sha256": sha256_file(path), "sizeBytes": path.stat().st_size})
    return rows


def inventory_sha256(rows: list[dict[str, Any]]) -> str:
    canonical = json.dumps(rows, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def evidence_relative_file(root: Path, relative: str, label: str) -> Path:
    portable = PurePosixPath(relative)
    if (
        not relative
        or portable.is_absolute()
        or relative != portable.as_posix()
        or any(part in {"", ".", ".."} for part in portable.parts)
        or "\\" in relative
    ):
        fail(f"{label} must be a safe evidence-root-relative path")
    containment = root.resolve(strict=True)
    path = require_local_regular_file(str(root / relative), label)
    try:
        path.relative_to(containment)
    except ValueError:
        fail(f"{label} escapes the native evidence root")
    return path


def validate_png_file(path: Path, label: str) -> tuple[int, int]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        fail(f"{label} is not a PNG")
    offset = 8
    ihdr: tuple[int, int, int, int] | None = None
    compressed = bytearray()
    saw_iend = False
    while offset < len(data):
        if offset + 12 > len(data):
            fail(f"{label} has a truncated PNG chunk")
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        end = offset + 12 + length
        if length > 64 * 1024 * 1024 or end > len(data):
            fail(f"{label} has an invalid PNG chunk length")
        chunk_data = data[offset + 8 : offset + 8 + length]
        expected_crc = struct.unpack(">I", data[offset + 8 + length : end])[0]
        if binascii.crc32(chunk_type + chunk_data) & 0xFFFFFFFF != expected_crc:
            fail(f"{label} has a corrupt PNG chunk")
        if offset == 8 and chunk_type != b"IHDR":
            fail(f"{label} does not begin with IHDR")
        if chunk_type == b"IHDR":
            if ihdr is not None or length != 13:
                fail(f"{label} has an invalid IHDR")
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", chunk_data
            )
            allowed_depths = {
                0: {1, 2, 4, 8, 16},
                2: {8, 16},
                3: {1, 2, 4, 8},
                4: {8, 16},
                6: {8, 16},
            }
            if not (320 <= width <= 16384 and 200 <= height <= 16384):
                fail(f"{label} dimensions are outside 320x200..16384x16384")
            if compression != 0 or filtering != 0 or interlace != 0:
                fail(f"{label} uses unsupported PNG encoding")
            if bit_depth not in allowed_depths.get(color_type, set()):
                fail(f"{label} uses an invalid PNG color/depth combination")
            ihdr = (width, height, bit_depth, color_type)
        elif chunk_type == b"IDAT":
            if ihdr is None or saw_iend:
                fail(f"{label} has an out-of-order IDAT")
            compressed.extend(chunk_data)
            if len(compressed) > 128 * 1024 * 1024:
                fail(f"{label} compressed pixels are too large")
        elif chunk_type == b"IEND":
            if length != 0 or saw_iend:
                fail(f"{label} has an invalid IEND")
            saw_iend = True
            if end != len(data):
                fail(f"{label} has trailing bytes after IEND")
        offset = end
    if ihdr is None or not compressed or not saw_iend:
        fail(f"{label} is missing required PNG chunks")
    width, height, bit_depth, color_type = ihdr
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    row_bytes = (width * channels * bit_depth + 7) // 8
    expected_size = height * (row_bytes + 1)
    if expected_size > 256 * 1024 * 1024:
        fail(f"{label} decompressed pixels are too large")
    decompressor = zlib.decompressobj()
    try:
        pixels = decompressor.decompress(bytes(compressed), expected_size + 1)
        if decompressor.unconsumed_tail or len(pixels) > expected_size:
            fail(f"{label} decompressed pixels exceed the declared dimensions")
        pixels += decompressor.flush()
    except zlib.error as exc:
        fail(f"{label} has invalid compressed pixels: {exc}")
    if len(pixels) != expected_size or not decompressor.eof or decompressor.unused_data:
        fail(f"{label} has an invalid decompressed pixel length")
    if any(pixels[row * (row_bytes + 1)] > 4 for row in range(height)):
        fail(f"{label} contains an invalid PNG row filter")
    return width, height


def validate_github_workflow_source(
    raw: object,
    *,
    label: str,
    authority: dict[str, Any],
    workflow: str,
    artifact_prefix: str,
) -> dict[str, str]:
    if not isinstance(raw, dict) or set(raw) != {
        "repository",
        "workflow",
        "runId",
        "runAttempt",
        "ref",
        "sha",
        "actor",
        "artifactName",
    }:
        fail(f"{label} source binding is malformed")
    raw_ref = raw.get("ref")
    raw_sha = raw.get("sha")
    raw_actor = raw.get("actor")
    source = {key: normalize(value) for key, value in raw.items()}
    if source["repository"] != normalize(authority.get("repository")):
        fail(f"{label} repository differs from the pinned GitHub authority")
    if source["workflow"] != workflow:
        fail(f"{label} workflow differs from the pinned workflow")
    if not POSITIVE_INTEGER_RE.fullmatch(source["runId"]) or not POSITIVE_INTEGER_RE.fullmatch(
        source["runAttempt"]
    ):
        fail(f"{label} run identity must be positive numeric GitHub metadata")
    if not is_exact_github_full_ref(raw_ref):
        fail(f"{label} ref must be an exact full refs/heads/... or refs/tags/... ref")
    if not isinstance(raw_sha, str) or not COMMIT_RE.fullmatch(raw_sha):
        fail(f"{label} SHA must be an exact lowercase 40-character commit")
    if source["sha"] != normalize(authority.get("presentationCommit")):
        fail(f"{label} SHA differs from the pinned Presentation authority")
    if not is_exact_github_actor_login(raw_actor):
        fail(f"{label} actor is not a GitHub login")
    expected_artifact = f"{artifact_prefix}-{source['runId']}-{source['runAttempt']}"
    if source["artifactName"] != expected_artifact:
        fail(f"{label} artifact name is not bound to its run identity")
    return source


def fetch_github_api_json(url: str) -> dict[str, Any]:
    if not url.startswith("https://api.github.com/"):
        fail("GitHub Actions provenance URL must use api.github.com HTTPS")
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "chummer6-preview-nightly-stage",
        },
        method="GET",
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            body = response.read(8 * 1024 * 1024 + 1)
    except (OSError, urllib.error.URLError) as exc:
        fail(f"GitHub Actions provenance query failed: {exc}")
    if len(body) > 8 * 1024 * 1024:
        fail("GitHub Actions provenance response is too large")
    try:
        payload = json.loads(body)
    except json.JSONDecodeError as exc:
        fail(f"GitHub Actions provenance response is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail("GitHub Actions provenance response is not an object")
    return payload


def verify_github_actions_provenance(
    source: dict[str, str],
    *,
    archive: Path | None = None,
) -> dict[str, Any]:
    repository = source["repository"]
    run_id = source["runId"]
    api_root = f"https://api.github.com/repos/{repository}/actions/runs/{run_id}"
    run = fetch_github_api_json(api_root)
    actor = run.get("actor")
    repository_row = run.get("repository")
    head_branch = run.get("head_branch")
    if not isinstance(head_branch, str) or not head_branch or head_branch != head_branch.strip():
        fail("GitHub Actions workflow-run ref differs from the evidence source")
    if source["ref"] not in {
        f"refs/heads/{head_branch}",
        f"refs/tags/{head_branch}",
    }:
        fail("GitHub Actions workflow-run ref differs from the evidence source")
    if (
        str(run.get("id")) != run_id
        or not github_workflow_run_path_matches(
            run.get("path"),
            source["workflow"],
            branch=head_branch,
            ref=source["ref"],
            sha=source["sha"],
        )
        or run.get("head_sha") != source["sha"]
        or str(run.get("run_attempt")) != source["runAttempt"]
        or run.get("event") != "workflow_dispatch"
        or normalize(run.get("status")) != "completed"
        or normalize(run.get("conclusion")) != "success"
        or not isinstance(actor, dict)
        or normalize(actor.get("login")) != source["actor"].lower()
        or not isinstance(repository_row, dict)
        or normalize(repository_row.get("full_name")) != repository.lower()
    ):
        fail("GitHub Actions workflow-run provenance differs from the evidence source")
    artifact_list = fetch_github_api_json(f"{api_root}/artifacts?per_page=100")
    raw_artifacts = artifact_list.get("artifacts")
    if not isinstance(raw_artifacts, list):
        fail("GitHub Actions artifact provenance response has no artifacts")
    total_count = artifact_list.get("total_count")
    if isinstance(total_count, bool) or not isinstance(total_count, int):
        fail("GitHub Actions artifact provenance has an invalid total_count")
    if total_count != len(raw_artifacts) or total_count > 100:
        fail("GitHub Actions artifact provenance count differs or requires pagination")
    matches = [
        row
        for row in raw_artifacts
        if isinstance(row, dict) and normalize(row.get("name")) == source["artifactName"]
    ]
    if len(matches) != 1:
        fail("GitHub Actions provenance did not return one exact named artifact")
    artifact = matches[0]
    workflow_run = artifact.get("workflow_run")
    validate_github_artifact_time_window(
        artifact.get("created_at"),
        artifact.get("expires_at"),
        label="GitHub Actions artifact",
    )
    artifact_sha = require_sha256(
        normalize(artifact.get("digest")).removeprefix("sha256:"),
        "GitHub Actions artifact digest",
    )
    if (
        artifact.get("expired") is not False
        or not isinstance(artifact.get("id"), int)
        or artifact["id"] <= 0
        or not isinstance(workflow_run, dict)
        or str(workflow_run.get("id")) != run_id
        or workflow_run.get("head_sha") != source["sha"]
    ):
        fail("GitHub Actions artifact provenance is expired or run-mismatched")
    if archive is not None:
        archive_path = require_local_regular_file(str(archive), "finalized evidence archive")
        if sha256_file(archive_path) != artifact_sha:
            fail("finalized evidence archive differs from the GitHub artifact digest")
    return {
        "repository": repository,
        "workflow": source["workflow"],
        "runId": run_id,
        "runAttempt": source["runAttempt"],
        "ref": source["ref"],
        "sha": source["sha"],
        "actor": source["actor"],
        "artifactId": artifact["id"],
        "artifactName": source["artifactName"],
        "artifactSha256": artifact_sha,
        "artifactCreatedAt": artifact["created_at"],
        "artifactExpiresAt": artifact["expires_at"],
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "expired": False,
    }


def _canonical_json_sha256(payload: dict[str, Any]) -> str:
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _read_json_snapshot(path: Path, label: str) -> tuple[dict[str, Any], str, int]:
    try:
        before = path.stat()
        raw = path.read_bytes()
        after = path.stat()
    except OSError as exc:
        fail(f"could not read exact {label} bytes: {exc}")
    if (
        before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or before.st_size != after.st_size
        or before.st_mtime_ns != after.st_mtime_ns
        or len(raw) != after.st_size
    ):
        fail(f"{label} changed while it was read")
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload, hashlib.sha256(raw).hexdigest(), len(raw)


def _candidate_local_content_rows(stage_dir: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    if (stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME).is_file():
        try:
            proposal = read_json(stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME)
            PUBLICATION_SCOPE.validate_proposal(proposal)
            registry_prepare = proposal.get("registryPrepare")
            registry_paths = (
                PUBLICATION_SCOPE.verify_registry_prepare_files(
                    registry_prepare,
                    stage_dir,
                    publication_dir=(
                        stage_dir / PUBLICATION_SCOPE.PUBLICATION_DIRECTORY
                    ),
                )
                if registry_prepare is not None
                else ()
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"staged Registry PREPARE evidence is invalid: {exc}")
        content_paths = tuple(
            dict.fromkeys(
                (*CANDIDATE_CONTENT_PATHS, *WINDOWS_ONLY_SCOPE_CONTENT_PATHS, *registry_paths)
            )
        )
    else:
        content_paths = CANDIDATE_CONTENT_PATHS
    for relative in sorted(content_paths):
        path = stage_dir / relative
        if path.is_symlink() or not path.is_file() or not stat.S_ISREG(path.stat().st_mode):
            fail(f"staged candidate producer content is missing: {relative}")
        rows.append(
            {
                "path": relative,
                "sha256": sha256_file(path),
                "sizeBytes": path.stat().st_size,
            }
        )
    return rows


def _validate_candidate_export_source(
    raw: object,
    *,
    authority: dict[str, Any],
) -> dict[str, str]:
    expected_keys = {
        "repository",
        "workflow",
        "runId",
        "runAttempt",
        "ref",
        "sha",
        "actor",
        "artifactName",
        "runnerLabel",
    }
    if not isinstance(raw, dict) or set(raw) != expected_keys:
        fail("candidate export receipt source is malformed")
    if raw.get("repository") != authority.get("repository"):
        fail("candidate exporter repository differs from the Presentation authority")
    if raw.get("workflow") != CANDIDATE_EXPORT_WORKFLOW:
        fail("candidate exporter workflow differs from the fixed exporter")
    run_id = require_exact_positive_integer(raw.get("runId"), "candidate exporter run ID")
    run_attempt = require_exact_positive_integer(
        raw.get("runAttempt"), "candidate exporter run attempt"
    )
    if raw.get("ref") != CANDIDATE_EXPORT_REF:
        fail("candidate exporter ref must be exactly refs/heads/main")
    source_sha = raw.get("sha")
    if (
        not isinstance(source_sha, str)
        or not COMMIT_RE.fullmatch(source_sha)
        or source_sha != authority.get("presentationCommit")
    ):
        fail("candidate exporter SHA differs from the Presentation authority")
    actor = raw.get("actor")
    if not is_exact_github_actor_login(actor):
        fail("candidate exporter actor is not an exact GitHub login")
    artifact_name = f"preview-nightly-candidate-{run_id}-{run_attempt}"
    if raw.get("artifactName") != artifact_name:
        fail("candidate exporter artifact name is not bound to its run identity")
    runner_label = raw.get("runnerLabel")
    if not isinstance(runner_label, str) or not CANDIDATE_RUNNER_LABEL_RE.fullmatch(
        runner_label
    ):
        fail("candidate exporter runner label is malformed")
    return {key: raw[key] for key in expected_keys}


def _validate_release_authoritative_supply_chain_verification(
    raw: object,
) -> dict[str, Any]:
    expected = {
        "mode": SUPPLY_CHAIN.LIVE_VERIFICATION_MODE,
        "releaseAuthoritative": True,
    }
    if not isinstance(raw, dict) or set(raw) != set(expected):
        fail("candidate export receipt supply-chain verification is malformed")
    if (
        raw.get("mode") != expected["mode"]
        or not isinstance(raw.get("mode"), str)
        or raw.get("releaseAuthoritative") is not True
    ):
        fail(
            "candidate export receipt must record release-authoritative pinned live scanner reexecution"
        )
    return raw


def _validate_candidate_export_heads(
    raw_heads: object,
    *,
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    local_rows: list[dict[str, Any]],
) -> None:
    if not isinstance(raw_heads, list) or len(raw_heads) != len(PROMOTED_WINDOWS_HEADS):
        fail("candidate export receipt must bind exactly the promoted Windows heads")
    rows_by_path = {row["path"]: row for row in local_rows}
    for head, raw_head in zip(PROMOTED_WINDOWS_HEADS, raw_heads, strict=True):
        if not isinstance(raw_head, dict) or set(raw_head) != {
            "headId",
            "rid",
            "installer",
            "payload",
        }:
            fail(f"candidate export receipt head binding is malformed for {head}")
        if raw_head.get("headId") != head or raw_head.get("rid") != "win-x64":
            fail(f"candidate export receipt tuple differs for {head}")
        artifact = tuples[(head, "windows", "win-x64")]
        expected_paths = {
            "installer": f"files/chummer-{head}-win-x64-installer.exe",
            "payload": f"files/chummer-{head}-win-x64-payload.zip",
        }
        for kind, relative in expected_paths.items():
            binding = raw_head.get(kind)
            local = rows_by_path[relative]
            if not isinstance(binding, dict) or binding != {
                "relativePath": relative,
                "fileName": PurePosixPath(relative).name,
                "sha256": local["sha256"],
                "sizeBytes": local["sizeBytes"],
            }:
                fail(f"candidate export receipt {head} {kind} differs from staged bytes")
        if (
            artifact_file_name(artifact) != PurePosixPath(expected_paths["installer"]).name
            or artifact_sha256(artifact) != rows_by_path[expected_paths["installer"]]["sha256"]
            or artifact.get("sizeBytes") != rows_by_path[expected_paths["installer"]]["sizeBytes"]
            or artifact.get("payloadFileName")
            != PurePosixPath(expected_paths["payload"]).name
            or artifact.get("payloadSha256") != rows_by_path[expected_paths["payload"]]["sha256"]
            or artifact.get("payloadSizeBytes") != rows_by_path[expected_paths["payload"]]["sizeBytes"]
        ):
            fail(f"candidate export receipt {head} differs from the canonical manifest")


def _verify_candidate_producer_github_actions_provenance(
    candidate: dict[str, Any],
) -> dict[str, Any]:
    validate_github_artifact_time_window(
        candidate.get("artifactCreatedAt"),
        candidate.get("artifactExpiresAt"),
        label="candidate producer artifact",
    )
    repository = candidate["repository"]
    run_id = candidate["runId"]
    api_root = f"https://api.github.com/repos/{repository}/actions/runs/{run_id}"
    run = fetch_github_api_json(api_root)
    if (
        isinstance(run.get("id"), bool)
        or not isinstance(run.get("id"), int)
        or run.get("id") != int(run_id)
        or not github_workflow_run_path_matches(
            run.get("path"),
            CANDIDATE_EXPORT_WORKFLOW,
            branch="main",
            ref=CANDIDATE_EXPORT_REF,
            sha=candidate["sha"],
        )
        or run.get("head_branch") != "main"
        or run.get("head_sha") != candidate["sha"]
        or isinstance(run.get("run_attempt"), bool)
        or not isinstance(run.get("run_attempt"), int)
        or run.get("run_attempt") != int(candidate["runAttempt"])
        or run.get("event") != "workflow_dispatch"
        or run.get("status") != "completed"
        or run.get("conclusion") != "success"
        or not isinstance(run.get("actor"), dict)
        or run["actor"].get("login") != candidate["actor"]
        or not isinstance(run.get("repository"), dict)
        or run["repository"].get("full_name") != repository
    ):
        fail("candidate producer GitHub Actions workflow-run provenance differs")

    artifact_list = fetch_github_api_json(f"{api_root}/artifacts?per_page=100")
    artifacts = artifact_list.get("artifacts")
    if not isinstance(artifacts, list):
        fail("candidate producer GitHub Actions response has no artifacts")
    total_count = artifact_list.get("total_count")
    if isinstance(total_count, bool) or not isinstance(total_count, int) or total_count < 0:
        fail("candidate producer GitHub Actions artifact total_count is invalid")
    if total_count != len(artifacts) or total_count > 100:
        fail(
            "candidate producer GitHub Actions artifact count differs or requires pagination"
        )
    matches = [
        row
        for row in artifacts
        if isinstance(row, dict)
        and str(row.get("id")) == candidate["artifactId"]
        and row.get("name") == candidate["artifactName"]
    ]
    if len(matches) != 1:
        fail("candidate producer GitHub Actions did not return one exact artifact")
    artifact = matches[0]
    workflow_run = artifact.get("workflow_run")
    expected_digest = f"sha256:{candidate['artifactSha256']}"
    if (
        isinstance(artifact.get("id"), bool)
        or not isinstance(artifact.get("id"), int)
        or artifact.get("id") != int(candidate["artifactId"])
        or artifact.get("expired") is not False
        or artifact.get("digest") != expected_digest
        or artifact.get("created_at") != candidate["artifactCreatedAt"]
        or artifact.get("expires_at") != candidate["artifactExpiresAt"]
        or not isinstance(workflow_run, dict)
        or isinstance(workflow_run.get("id"), bool)
        or not isinstance(workflow_run.get("id"), int)
        or workflow_run.get("id") != int(run_id)
        or workflow_run.get("head_sha") != candidate["sha"]
    ):
        fail("candidate producer GitHub Actions artifact provenance differs or is expired")
    return {
        "repository": repository,
        "workflow": CANDIDATE_EXPORT_WORKFLOW,
        "runId": run_id,
        "runAttempt": candidate["runAttempt"],
        "ref": CANDIDATE_EXPORT_REF,
        "sha": candidate["sha"],
        "actor": candidate["actor"],
        "artifactId": candidate["artifactId"],
        "artifactName": candidate["artifactName"],
        "artifactSha256": candidate["artifactSha256"],
        "artifactCreatedAt": candidate["artifactCreatedAt"],
        "artifactExpiresAt": candidate["artifactExpiresAt"],
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "expired": False,
    }


def _prefixed_candidate_supply_chain_bindings(
    bindings: dict[str, Any],
) -> dict[str, Any]:
    expected: dict[str, Any] = {"sboms": [], "scans": []}
    for category in ("sboms", "scans"):
        rows = bindings.get(category)
        if not isinstance(rows, list):
            fail(f"candidate supply-chain {category} binding is malformed")
        for row in rows:
            if not isinstance(row, dict) or set(row) != {"path", "sha256", "sizeBytes"}:
                fail(f"candidate supply-chain {category} row is malformed")
            expected[category].append(
                {
                    **row,
                    "path": f"{CANDIDATE_PROVENANCE_DIRECTORY}/{row['path']}",
                }
            )
    gate = bindings.get("gate")
    if not isinstance(gate, dict) or set(gate) != {"path", "sha256", "sizeBytes"}:
        fail("candidate aggregate supply-chain gate binding is malformed")
    expected["gate"] = {
        **gate,
        "path": f"{CANDIDATE_PROVENANCE_DIRECTORY}/{gate['path']}",
    }
    return expected


def _verify_copied_candidate_supply_chain(
    native_root: Path,
    capture_inventory: dict[str, dict[str, Any]],
    bindings: dict[str, Any],
) -> dict[str, Any]:
    copied = _prefixed_candidate_supply_chain_bindings(bindings)
    for category in ("sboms", "scans"):
        for row in copied[category]:
            path = require_capture_inventory_file(
                native_root,
                capture_inventory,
                row["path"],
                f"candidate supply-chain {category} {row['path']}",
            )
            if sha256_file(path) != row["sha256"] or path.stat().st_size != row["sizeBytes"]:
                fail(f"candidate supply-chain provenance differs: {row['path']}")
    gate = copied["gate"]
    gate_path = require_capture_inventory_file(
        native_root,
        capture_inventory,
        gate["path"],
        "candidate aggregate supply-chain gate",
    )
    if sha256_file(gate_path) != gate["sha256"] or gate_path.stat().st_size != gate["sizeBytes"]:
        fail("candidate aggregate supply-chain gate provenance differs")
    return copied


def validate_candidate_producer_provenance(
    stage_dir: Path,
    native_root: Path,
    capture_inventory: dict[str, dict[str, Any]],
    raw_candidate: object,
    authority: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    windows_only = isinstance(raw_candidate, dict) and "publicationScope" in raw_candidate
    registry_prepare = (
        windows_only
        and isinstance(raw_candidate, dict)
        and "registryPrepareSha256" in raw_candidate
    )
    candidate_keys = {
        "repository",
        "workflow",
        "runId",
        "runAttempt",
        "ref",
        "sha",
        "actor",
        "artifactId",
        "artifactName",
        "artifactSha256",
        "artifactCreatedAt",
        "artifactExpiresAt",
        "manifestPath",
        "manifestSha256",
        "contentInventorySha256",
        "exportReceiptSha256",
        "handoffSha256",
        "authenticatedApiSha256",
        "contentInventory",
        "exportReceipt",
        "supplyChain",
    }
    if windows_only:
        candidate_keys.update(
            {
                "fullShelfCompatibilityManifest",
                "fullShelfCompatibilityManifestPath",
                "fullShelfCompatibilityManifestSha256",
                "fullShelfManifest",
                "fullShelfManifestPath",
                "fullShelfManifestSha256",
                "publicationScope",
                "publicationScopePath",
                "publicationScopeSha256",
                "scopeDecisionSha256",
                "signingReceipt",
                "signingReceiptPath",
                "signingReceiptSha256",
            }
        )
    if registry_prepare:
        candidate_keys.update({"registryPrepareFiles", "registryPrepareSha256"})
    if not isinstance(raw_candidate, dict) or set(raw_candidate) != candidate_keys:
        fail("native capture candidate producer binding is malformed")
    candidate = raw_candidate
    common_source = {
        key: candidate.get(key)
        for key in (
            "repository",
            "workflow",
            "runId",
            "runAttempt",
            "ref",
            "sha",
            "actor",
            "artifactName",
        )
    }
    if candidate.get("repository") != authority.get("repository"):
        fail("candidate producer repository differs from the Presentation authority")
    if candidate.get("workflow") != CANDIDATE_EXPORT_WORKFLOW:
        fail("candidate producer workflow differs from the fixed exporter")
    require_exact_positive_integer(candidate.get("runId"), "candidate producer run ID")
    require_exact_positive_integer(candidate.get("runAttempt"), "candidate producer run attempt")
    require_exact_positive_integer(candidate.get("artifactId"), "candidate producer artifact ID")
    if candidate.get("ref") != CANDIDATE_EXPORT_REF:
        fail("candidate producer ref must be exactly refs/heads/main")
    if (
        not isinstance(candidate.get("sha"), str)
        or not COMMIT_RE.fullmatch(candidate["sha"])
        or candidate["sha"] != authority.get("presentationCommit")
    ):
        fail("candidate producer SHA differs from the Presentation authority")
    if not is_exact_github_actor_login(candidate.get("actor")):
        fail("candidate producer actor is not an exact GitHub login")
    if candidate.get("artifactName") != (
        f"preview-nightly-candidate-{candidate['runId']}-{candidate['runAttempt']}"
    ):
        fail("candidate producer artifact name is not bound to its run identity")
    for field in (
        "artifactSha256",
        "manifestSha256",
        "contentInventorySha256",
        "exportReceiptSha256",
        "handoffSha256",
        "authenticatedApiSha256",
    ):
        require_exact_sha256(candidate.get(field), f"candidate producer {field}")
    if windows_only:
        for field in (
            "fullShelfCompatibilityManifestSha256",
            "fullShelfManifestSha256",
            "publicationScopeSha256",
            "scopeDecisionSha256",
            "signingReceiptSha256",
        ):
            require_exact_sha256(candidate.get(field), f"candidate producer {field}")
    if registry_prepare:
        require_exact_sha256(
            candidate.get("registryPrepareSha256"),
            "candidate producer registryPrepareSha256",
        )
    validate_github_artifact_time_window(
        candidate.get("artifactCreatedAt"),
        candidate.get("artifactExpiresAt"),
        label="candidate producer artifact",
    )
    if candidate.get("manifestPath") != CANDIDATE_MANIFEST_PATH:
        fail("candidate producer manifest path differs from the fixed export contract")

    local_rows_before = _candidate_local_content_rows(stage_dir)
    manifest_row = next(
        row for row in local_rows_before if row["path"] == CANDIDATE_MANIFEST_PATH
    )
    if candidate.get("manifestSha256") != manifest_row["sha256"]:
        fail("candidate producer manifest digest differs from the staged candidate")

    inventory_path = require_capture_inventory_file(
        native_root,
        capture_inventory,
        CANDIDATE_CONTENT_INVENTORY_PATH,
        "candidate content inventory",
    )
    export_path = require_capture_inventory_file(
        native_root,
        capture_inventory,
        CANDIDATE_EXPORT_PATH,
        "candidate export receipt",
    )
    inventory_payload, inventory_sha, inventory_size = _read_json_snapshot(
        inventory_path, "candidate content inventory"
    )
    export_payload, export_sha, export_size = _read_json_snapshot(
        export_path, "candidate export receipt"
    )
    expected_nested = {
        "contentInventory": {
            "path": CANDIDATE_CONTENT_INVENTORY_PATH,
            "sha256": inventory_sha,
            "sizeBytes": inventory_size,
        },
        "exportReceipt": {
            "path": CANDIDATE_EXPORT_PATH,
            "sha256": export_sha,
            "sizeBytes": export_size,
        },
    }
    if windows_only:
        for field, relative, digest_field in (
            (
                "publicationScope",
                candidate.get("publicationScopePath"),
                "publicationScopeSha256",
            ),
            (
                "signingReceipt",
                candidate.get("signingReceiptPath"),
                "signingReceiptSha256",
            ),
            (
                "fullShelfManifest",
                candidate.get("fullShelfManifestPath"),
                "fullShelfManifestSha256",
            ),
            (
                "fullShelfCompatibilityManifest",
                candidate.get("fullShelfCompatibilityManifestPath"),
                "fullShelfCompatibilityManifestSha256",
            ),
        ):
            if not isinstance(relative, str) or relative not in WINDOWS_ONLY_SCOPE_CONTENT_PATHS:
                fail(f"candidate producer {field} path differs from the fixed scope contract")
            copied_path = f"{CANDIDATE_PROVENANCE_DIRECTORY}/{relative}"
            path = require_capture_inventory_file(
                native_root, capture_inventory, copied_path, f"candidate {field}"
            )
            expected_nested[field] = {
                "path": copied_path,
                "sha256": sha256_file(path),
                "sizeBytes": path.stat().st_size,
            }
            if expected_nested[field]["sha256"] != candidate.get(digest_field):
                fail(f"candidate producer {field} digest differs from copied bytes")
    registry_file_bindings: list[dict[str, Any]] = []
    if registry_prepare:
        provenance_root = native_root / CANDIDATE_PROVENANCE_DIRECTORY
        proposal_payload = read_json(
            provenance_root / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
        )
        proposal_registry = proposal_payload.get("registryPrepare")
        try:
            registry_sha = PUBLICATION_SCOPE.validate_registry_prepare_binding(
                proposal_registry
            )
            registry_paths = PUBLICATION_SCOPE.verify_registry_prepare_files(
                proposal_registry,
                provenance_root,
                publication_dir=(
                    provenance_root / PUBLICATION_SCOPE.PUBLICATION_DIRECTORY
                ),
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"candidate producer Registry PREPARE evidence is invalid: {exc}")
        if registry_sha != candidate["registryPrepareSha256"]:
            fail("candidate producer Registry PREPARE digest differs from its proposal")
        for relative in registry_paths:
            copied_path = f"{CANDIDATE_PROVENANCE_DIRECTORY}/{relative}"
            path = require_capture_inventory_file(
                native_root,
                capture_inventory,
                copied_path,
                f"candidate Registry PREPARE file {relative}",
            )
            registry_file_bindings.append(
                {
                    "path": copied_path,
                    "sha256": sha256_file(path),
                    "sizeBytes": path.stat().st_size,
                }
            )
        if candidate.get("registryPrepareFiles") != registry_file_bindings:
            fail("candidate producer Registry PREPARE file bindings differ")
    for field, expected in expected_nested.items():
        if candidate.get(field) != expected:
            fail(f"native capture candidate {field} binding differs from exact copied bytes")
    if (
        candidate.get("contentInventorySha256") != inventory_sha
        or candidate.get("exportReceiptSha256") != export_sha
    ):
        fail("native capture candidate flat provenance digests differ from copied bytes")

    manifest = read_json(stage_dir / CANDIDATE_MANIFEST_PATH)
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    expected_release = {"channel": channel, "version": version}
    expected_manifest = {
        "path": CANDIDATE_MANIFEST_PATH,
        "sha256": manifest_row["sha256"],
    }
    if (
        set(inventory_payload) != {
            "contractName",
            "contractVersion",
            "release",
            "manifest",
            "files",
        }
        or inventory_payload.get("contractName")
        != CANDIDATE_CONTENT_INVENTORY_CONTRACT_NAME
        or inventory_payload.get("contractVersion") != (2 if windows_only else CONTRACT_VERSION)
        or inventory_payload.get("release") != expected_release
        or inventory_payload.get("manifest") != expected_manifest
        or inventory_payload.get("files") != local_rows_before
        or any(
            not isinstance(row, dict) or set(row) != {"path", "sha256", "sizeBytes"}
            for row in inventory_payload.get("files", [])
        )
    ):
        fail("candidate content inventory contract or exact versioned staged bytes differ")

    expected_export_keys = {
        "contractName",
        "contractVersion",
        "status",
        "release",
        "source",
        "candidateManifest",
        "contentInventory",
        "heads",
        "supplyChain",
        "supplyChainVerification",
    }
    if windows_only:
        expected_export_keys.add("publicationScope")
    export_source = _validate_candidate_export_source(
        export_payload.get("source"), authority=authority
    )
    if (
        set(export_payload) != expected_export_keys
        or export_payload.get("contractName") != CANDIDATE_EXPORT_CONTRACT_NAME
        or export_payload.get("contractVersion") != (2 if windows_only else CONTRACT_VERSION)
        or export_payload.get("status") != "exported"
        or export_payload.get("release") != expected_release
        or export_payload.get("candidateManifest") != expected_manifest
        or export_payload.get("contentInventory")
        != {
            "path": CANDIDATE_CONTENT_INVENTORY_FILE_NAME,
            "sha256": inventory_sha,
        }
        or any(candidate[key] != export_source[key] for key in common_source)
    ):
        fail("candidate export receipt contract or capture binding differs")
    _validate_candidate_export_heads(
        export_payload.get("heads"), tuples=tuples, local_rows=local_rows_before
    )
    _validate_release_authoritative_supply_chain_verification(
        export_payload.get("supplyChainVerification")
    )
    try:
        SUPPLY_CHAIN.verify_gate(
            stage_root=stage_dir,
            version=version,
            source_commit=candidate["sha"],
        )
        local_supply_chain = SUPPLY_CHAIN.content_bindings(stage_dir)
    except SUPPLY_CHAIN.SupplyChainError as exc:
        fail(f"candidate producer supply-chain evidence is invalid: {exc}")
    if export_payload.get("supplyChain") != local_supply_chain:
        fail("candidate export receipt supply-chain binding differs from staged evidence")
    copied_supply_chain = _verify_copied_candidate_supply_chain(
        native_root, capture_inventory, local_supply_chain
    )
    if candidate.get("supplyChain") != copied_supply_chain:
        fail("native capture candidate supply-chain provenance binding differs")

    publication_scope = None
    if windows_only:
        staged_artifact = tuples[(PROMOTED_WINDOWS_HEADS[0], "windows", "win-x64")]
        provenance_root = native_root / CANDIDATE_PROVENANCE_DIRECTORY
        try:
            publication_scope = PUBLICATION_SCOPE.validate_export_inputs(
                provenance_root,
                expected_version=version,
                installer_sha256=artifact_sha256(staged_artifact),
                payload_sha256=require_sha256(
                    normalize(staged_artifact.get("payloadSha256")),
                    "Windows payload sha256",
                ),
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"candidate producer publication scope is invalid: {exc}")
        if export_payload.get("publicationScope") != publication_scope:
            fail("candidate export receipt publication scope differs")
        if registry_prepare and (
            publication_scope.get("registryPrepareSha256")
            != candidate.get("registryPrepareSha256")
        ):
            fail("candidate Registry PREPARE digest differs across provenance")
        staged_proposal = stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
        if (
            staged_proposal.is_symlink()
            or not staged_proposal.is_file()
            or sha256_file(staged_proposal) != candidate["publicationScopeSha256"]
        ):
            fail("staged publication scope proposal differs from candidate provenance")

    handoff = {
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 2 if windows_only else CONTRACT_VERSION,
        **common_source,
        "artifactId": candidate["artifactId"],
        "artifactSha256": candidate["artifactSha256"],
        "contentInventorySha256": candidate["contentInventorySha256"],
    }
    if windows_only:
        handoff.update(
            {
                "fullShelfManifestSha256": candidate["fullShelfManifestSha256"],
                "fullShelfCompatibilityManifestSha256": candidate[
                    "fullShelfCompatibilityManifestSha256"
                ],
                "publicationScopeSha256": candidate["publicationScopeSha256"],
                "scopeDecisionSha256": candidate["scopeDecisionSha256"],
                "signingReceiptSha256": candidate["signingReceiptSha256"],
            }
        )
        if registry_prepare:
            handoff["registryPrepareSha256"] = candidate[
                "registryPrepareSha256"
            ]
    authenticated_api = {
        "contractName": "chummer6-ui.preview-nightly-candidate-authenticated-api",
        "contractVersion": CONTRACT_VERSION,
        **common_source,
        "artifactId": candidate["artifactId"],
        "artifactSha256": candidate["artifactSha256"],
        "artifactCreatedAt": candidate["artifactCreatedAt"],
        "artifactExpiresAt": candidate["artifactExpiresAt"],
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
    }
    if _canonical_json_sha256(handoff) != candidate["handoffSha256"]:
        fail("candidate producer handoff digest differs from its exact reconstructed contract")
    if _canonical_json_sha256(authenticated_api) != candidate["authenticatedApiSha256"]:
        fail("candidate producer authenticated-API digest differs from its exact contract")

    api_provenance = _verify_candidate_producer_github_actions_provenance(candidate)
    if (
        sha256_file(inventory_path) != inventory_sha
        or inventory_path.stat().st_size != inventory_size
        or sha256_file(export_path) != export_sha
        or export_path.stat().st_size != export_size
        or _candidate_local_content_rows(stage_dir) != local_rows_before
    ):
        fail("candidate producer provenance or staged candidate changed during validation")
    result = {
        "candidate": candidate,
        "contentInventory": expected_nested["contentInventory"],
        "exportReceipt": expected_nested["exportReceipt"],
        "supplyChain": copied_supply_chain,
        "localCandidateFiles": local_rows_before,
        "githubActionsProvenance": api_provenance,
    }
    if windows_only:
        result["publicationScope"] = publication_scope
        result["scopeBindings"] = {
            field: expected_nested[field]
            for field in (
                "publicationScope",
                "signingReceipt",
                "fullShelfManifest",
                "fullShelfCompatibilityManifest",
            )
        }
        if registry_prepare:
            result["registryPrepareFiles"] = registry_file_bindings
            result["registryPrepareSha256"] = candidate[
                "registryPrepareSha256"
            ]
    return result


def extract_evidence_archive(archive: Path, destination: Path) -> None:
    archive = require_local_regular_file(str(archive), "finalized evidence archive")
    if archive.stat().st_size > EVIDENCE_ARCHIVE_MAX_BYTES:
        fail("finalized evidence archive exceeds 512 MiB")
    if destination.exists() or destination.is_symlink():
        fail(f"native Windows evidence destination already exists: {destination}")
    destination.mkdir(parents=True, mode=0o700)
    destination.chmod(0o700)
    if (
        destination.is_symlink()
        or not destination.is_dir()
        or stat.S_IMODE(destination.stat().st_mode) != 0o700
    ):
        fail("native Windows evidence extraction directory must be private mode 0700")
    total_size = 0
    file_count = 0
    seen: set[str] = set()
    try:
        with zipfile.ZipFile(archive) as bundle:
            if len(bundle.infolist()) > EVIDENCE_ARCHIVE_MAX_FILES:
                fail("finalized evidence archive has too many members")
            for info in bundle.infolist():
                is_directory = info.is_dir()
                raw_name = info.filename[:-1] if is_directory else info.filename
                if not raw_name or raw_name.endswith("/"):
                    fail(
                        f"finalized evidence archive has an unsafe member: {info.filename!r}"
                    )
                portable = PurePosixPath(raw_name)
                if (
                    portable.is_absolute()
                    or raw_name != portable.as_posix()
                    or len(raw_name.encode("utf-8")) > 512
                    or any(part in {"", ".", ".."} for part in portable.parts)
                    or "\\" in raw_name
                    or "\x00" in raw_name
                    or raw_name in seen
                ):
                    fail(f"finalized evidence archive has an unsafe member: {info.filename!r}")
                seen.add(raw_name)
                mode = (info.external_attr >> 16) & 0o170000
                expected_modes = {0, stat.S_IFDIR} if is_directory else {0, stat.S_IFREG}
                if mode not in expected_modes:
                    fail(f"finalized evidence archive has a special member: {raw_name}")
                if is_directory:
                    (destination / portable).mkdir(parents=True, exist_ok=True)
                    continue
                if info.flag_bits & 0x1:
                    fail(f"finalized evidence archive has an encrypted member: {raw_name}")
                if info.compress_type not in {zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED}:
                    fail(
                        f"finalized evidence archive uses unsupported compression: {raw_name}"
                    )
                if info.file_size < 0 or info.file_size > EVIDENCE_ARCHIVE_MAX_MEMBER_BYTES:
                    fail(f"finalized evidence archive member is too large: {raw_name}")
                if info.file_size > max(
                    1024 * 1024,
                    info.compress_size * EVIDENCE_ARCHIVE_MAX_COMPRESSION_RATIO,
                ):
                    fail(
                        f"finalized evidence archive member has an unsafe compression ratio: {raw_name}"
                    )
                file_count += 1
                if file_count > EVIDENCE_ARCHIVE_MAX_FILES:
                    fail("finalized evidence archive has too many files")
                total_size += info.file_size
                if total_size > EVIDENCE_ARCHIVE_MAX_EXPANDED_BYTES:
                    fail("finalized evidence archive expands beyond 512 MiB")
                target = destination / portable
                target.parent.mkdir(parents=True, exist_ok=True)
                with bundle.open(info) as source_handle, target.open("xb") as target_handle:
                    copied = 0
                    while True:
                        chunk = source_handle.read(1024 * 1024)
                        if not chunk:
                            break
                        copied += len(chunk)
                        if copied > info.file_size or total_size - info.file_size + copied > (
                            EVIDENCE_ARCHIVE_MAX_EXPANDED_BYTES
                        ):
                            fail(
                                f"finalized evidence archive member expanded beyond its declaration: {raw_name}"
                            )
                        target_handle.write(chunk)
                    if copied != info.file_size:
                        fail(
                            f"finalized evidence archive member size differs from its declaration: {raw_name}"
                        )
    except (
        OSError,
        EOFError,
        RuntimeError,
        NotImplementedError,
        zipfile.BadZipFile,
    ) as exc:
        fail(f"invalid finalized evidence archive: {exc}")


def _archive_descriptor_metadata(info: os.stat_result) -> dict[str, int]:
    return {
        "device": info.st_dev,
        "inode": info.st_ino,
        "mode": info.st_mode,
        "sizeBytes": info.st_size,
        "modifiedNs": info.st_mtime_ns,
        "changedNs": info.st_ctime_ns,
        "linkCount": info.st_nlink,
    }


def _copy_archive_descriptor_to_snapshot(
    source_fd: int,
    snapshot_fd: int,
    expected_size: int,
) -> str:
    digest = hashlib.sha256()
    copied = 0
    while True:
        chunk = os.read(source_fd, 1024 * 1024)
        if not chunk:
            break
        copied += len(chunk)
        if copied > expected_size or copied > EVIDENCE_ARCHIVE_MAX_BYTES:
            fail("finalized evidence archive changed size while snapshotting")
        digest.update(chunk)
        remaining = memoryview(chunk)
        while remaining:
            written = os.write(snapshot_fd, remaining)
            if written <= 0:
                fail("could not write the finalized evidence archive snapshot")
            remaining = remaining[written:]
    if copied != expected_size:
        fail("finalized evidence archive changed size while snapshotting")
    return digest.hexdigest()


def _verify_owned_archive_snapshot(
    snapshot: Path,
    *,
    expected_metadata: dict[str, int],
    expected_sha256: str,
) -> str:
    try:
        before = os.stat(snapshot, follow_symlinks=False)
    except OSError as exc:
        fail(f"finalized evidence archive snapshot is unavailable: {exc}")
    if (
        not stat.S_ISREG(before.st_mode)
        or stat.S_IMODE(before.st_mode) != 0o400
        or _archive_descriptor_metadata(before) != expected_metadata
    ):
        fail("finalized evidence archive snapshot identity or metadata changed")
    try:
        actual_sha256 = sha256_file(snapshot)
        after = os.stat(snapshot, follow_symlinks=False)
    except OSError as exc:
        fail(f"finalized evidence archive snapshot changed while hashing: {exc}")
    if _archive_descriptor_metadata(after) != expected_metadata:
        fail("finalized evidence archive snapshot changed while hashing")
    if actual_sha256 != expected_sha256:
        fail("finalized evidence archive snapshot digest changed")
    return actual_sha256


def _snapshot_finalized_evidence_archive(
    archive: Path,
    replay_root: Path,
) -> tuple[Path, str, dict[str, int]]:
    if not archive.is_absolute():
        fail("finalized evidence archive must be an absolute local path")
    if not hasattr(os, "O_NOFOLLOW"):
        fail("platform does not provide no-follow archive snapshot protection")
    source_flags = os.O_RDONLY | os.O_NOFOLLOW
    if hasattr(os, "O_NONBLOCK"):
        source_flags |= os.O_NONBLOCK
    snapshot_flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
    if hasattr(os, "O_CLOEXEC"):
        source_flags |= os.O_CLOEXEC
        snapshot_flags |= os.O_CLOEXEC
    snapshot = replay_root / "finalized-evidence.snapshot.zip"
    source_fd: int | None = None
    snapshot_fd: int | None = None
    snapshot_metadata: dict[str, int] | None = None
    copied_sha256 = ""
    try:
        source_fd = os.open(archive, source_flags)
        source_before = os.fstat(source_fd)
        source_metadata = _archive_descriptor_metadata(source_before)
        if not stat.S_ISREG(source_before.st_mode):
            fail("finalized evidence archive descriptor is not a regular file")
        if source_before.st_size > EVIDENCE_ARCHIVE_MAX_BYTES:
            fail("finalized evidence archive exceeds 512 MiB")
        snapshot_fd = os.open(snapshot, snapshot_flags, 0o400)
        copied_sha256 = _copy_archive_descriptor_to_snapshot(
            source_fd,
            snapshot_fd,
            source_before.st_size,
        )
        os.fsync(snapshot_fd)
        source_after = os.fstat(source_fd)
        if _archive_descriptor_metadata(source_after) != source_metadata:
            fail("finalized evidence archive descriptor changed while snapshotting")
        os.fchmod(snapshot_fd, 0o400)
        os.fsync(snapshot_fd)
        snapshot_info = os.fstat(snapshot_fd)
        snapshot_metadata = _archive_descriptor_metadata(snapshot_info)
        if (
            not stat.S_ISREG(snapshot_info.st_mode)
            or stat.S_IMODE(snapshot_info.st_mode) != 0o400
            or snapshot_info.st_size != source_before.st_size
            or snapshot_info.st_nlink != 1
        ):
            fail("finalized evidence archive snapshot is not an immutable private file")
    except (OSError, ValueError) as exc:
        fail(f"could not create finalized evidence archive snapshot: {exc}")
    finally:
        if snapshot_fd is not None:
            os.close(snapshot_fd)
        if source_fd is not None:
            os.close(source_fd)
    if snapshot_metadata is None:  # pragma: no cover - defensive completeness
        fail("finalized evidence archive snapshot has no recorded identity")
    snapshot_sha256 = _verify_owned_archive_snapshot(
        snapshot,
        expected_metadata=snapshot_metadata,
        expected_sha256=copied_sha256,
    )
    return snapshot, snapshot_sha256, snapshot_metadata


def run_upload_inventory_paths(stage_dir: Path) -> list[Path]:
    """Return the exact fail-closed upload inventory for this stage.

    Legacy stages retain the pinned hosted-bootstrap policy.  A Windows-only
    stage can expose only its composed ``publication/`` shelf; the root
    ``files/`` directory intentionally remains Windows+Linux build evidence.
    """
    windows_only = (stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME).is_file()
    inventory_root = (
        stage_dir / PUBLICATION_SCOPE.PUBLICATION_DIRECTORY
        if windows_only
        else stage_dir
    )
    top_level_files = (
        (
            PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME,
            PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME,
        )
        if windows_only
        else HOSTED_UPLOAD_TOP_LEVEL_FILES
    )
    recursive_directories = (
        ("files",) if windows_only else HOSTED_UPLOAD_RECURSIVE_DIRECTORIES
    )
    paths: list[Path] = []
    for relative in top_level_files:
        path = inventory_root / relative
        if path.is_file():
            paths.append(path)
    for directory_name in recursive_directories:
        directory = inventory_root / directory_name
        if directory.exists():
            if directory.is_symlink() or not directory.is_dir():
                fail(f"Run upload inventory directory is invalid: {directory}")
            paths.extend(safe_tree_entries(directory))
    required = set(top_level_files)
    actual = {path.relative_to(inventory_root).as_posix() for path in paths}
    missing = sorted(required - actual)
    if missing:
        fail(f"Run upload inventory is missing required files: {missing}")
    return sorted(paths, key=lambda path: path.relative_to(inventory_root).as_posix())


def build_run_upload_candidate(stage_dir: Path) -> dict[str, Any]:
    windows_only = (stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME).is_file()
    inventory_root = (
        stage_dir / PUBLICATION_SCOPE.PUBLICATION_DIRECTORY
        if windows_only
        else stage_dir
    )
    scope: dict[str, Any] | None = None
    if windows_only:
        try:
            scope = PUBLICATION_SCOPE.verify_scope(
                argparse.Namespace(
                    scope=stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME,
                    proposal=stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME,
                    publication_dir=inventory_root,
                    evidence_root=stage_dir,
                )
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"Run upload candidate publication shelf is invalid: {exc}")
    manifest_path = inventory_root / "RELEASE_CHANNEL.generated.json"
    manifest = read_json(manifest_path)
    version, _ = require_preview_manifest_identity(manifest, "canonical manifest")
    inventory_hasher = hashlib.sha256()
    file_count = 0
    total_bytes = 0
    for path in run_upload_inventory_paths(stage_dir):
        relative = path.relative_to(inventory_root).as_posix().encode("utf-8")
        size = path.stat().st_size
        digest = bytes.fromhex(sha256_file(path))
        inventory_hasher.update(len(relative).to_bytes(8, "big"))
        inventory_hasher.update(relative)
        inventory_hasher.update(size.to_bytes(8, "big"))
        inventory_hasher.update(digest)
        file_count += 1
        total_bytes += size
    payload: dict[str, Any] = {
        "version": version,
        "canonicalManifestSha256": sha256_file(manifest_path),
        "inventorySha256": inventory_hasher.hexdigest(),
        "fileCount": file_count,
        "totalBytes": total_bytes,
    }
    if scope is not None:
        payload.update(
            {
                "consumerBootstrapCompatible": False,
                "deployAuthorized": False,
                "fullShelfManifestSha256": scope["fullShelfManifestSha256"],
                "publicationDeltaSha256": PUBLICATION_SCOPE.canonical_sha256(
                    scope["publicationDeltaTuples"]
                ),
                "publicationScopeSha256": sha256_file(
                    stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME
                ),
                "uploadAuthorized": False,
                "uploadRoot": PUBLICATION_SCOPE.PUBLICATION_DIRECTORY,
            }
        )
    identity_bytes = json.dumps(
        payload, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    payload["bundleIdentitySha256"] = hashlib.sha256(identity_bytes).hexdigest()
    return payload


def copy_safe_tree(source: Path, destination: Path) -> list[dict[str, Any]]:
    if destination.exists():
        fail(f"destination already exists: {destination}")
    destination.mkdir(parents=True, mode=0o700)
    for source_path in safe_tree_entries(source):
        relative = source_path.relative_to(source)
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, target)
    return inventory_tree(destination)


def artifact_file_name(row: dict[str, Any]) -> str:
    raw = normalize(row.get("fileName"))
    if not raw:
        raw = Path(normalize(row.get("downloadUrl"))).name
    if not raw or Path(raw).name != raw or raw in {".", ".."}:
        fail(f"release artifact has an unsafe fileName: {raw!r}")
    return raw


def artifact_sha256(row: dict[str, Any]) -> str:
    return require_sha256(normalize(row.get("sha256")).removeprefix("sha256:"), "artifact sha256")


def verify_manifest_files(manifest: dict[str, Any], files_dir: Path) -> dict[tuple[str, str, str], dict[str, Any]]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list) or not rows:
        fail("canonical manifest has no artifacts")
    tuples: dict[tuple[str, str, str], dict[str, Any]] = {}
    file_names: set[str] = set()
    for raw_row in rows:
        if not isinstance(raw_row, dict):
            fail("canonical manifest contains a non-object artifact row")
        file_name = artifact_file_name(raw_row)
        if file_name in file_names:
            fail(f"canonical manifest repeats artifact fileName: {file_name}")
        file_names.add(file_name)
        path = files_dir / file_name
        if path.is_symlink() or not path.is_file():
            fail(f"canonical manifest artifact is missing from files shelf: {file_name}")
        expected_digest = artifact_sha256(raw_row)
        actual_digest = sha256_file(path)
        if actual_digest != expected_digest:
            fail(f"canonical manifest digest mismatch for {file_name}: {expected_digest} != {actual_digest}")
        expected_size = raw_row.get("sizeBytes")
        if expected_size is not None and int(expected_size) != path.stat().st_size:
            fail(f"canonical manifest size mismatch for {file_name}")
        payload_name = normalize(raw_row.get("payloadFileName"))
        payload_sha = normalize(raw_row.get("payloadSha256"))
        payload_size = raw_row.get("payloadSizeBytes")
        if payload_name or payload_sha or payload_size is not None:
            if not payload_name or Path(payload_name).name != payload_name:
                fail(f"canonical manifest has an unsafe or missing payloadFileName for {file_name}")
            payload_path = files_dir / payload_name
            if payload_path.is_symlink() or not payload_path.is_file():
                fail(f"canonical manifest payload is missing from files shelf: {payload_name}")
            expected_payload_sha = require_sha256(payload_sha, f"{payload_name} payloadSha256")
            try:
                expected_payload_size = int(payload_size)
            except (TypeError, ValueError):
                fail(f"canonical manifest has invalid payloadSizeBytes for {payload_name}")
            if sha256_file(payload_path) != expected_payload_sha:
                fail(f"canonical manifest payload digest mismatch for {payload_name}")
            if payload_path.stat().st_size != expected_payload_size:
                fail(f"canonical manifest payload size mismatch for {payload_name}")
        key = (
            normalize(raw_row.get("head")).lower(),
            normalize(raw_row.get("platform")).lower(),
            normalize(raw_row.get("rid")).lower(),
        )
        if all(key) and normalize(raw_row.get("kind")).lower() == "installer":
            if key in tuples:
                fail(f"canonical manifest repeats desktop installer tuple: {':'.join(key)}")
            tuples[key] = raw_row
    return tuples


def require_preview_manifest_identity(manifest: dict[str, Any], label: str) -> tuple[str, str]:
    version = normalize(manifest.get("version") or manifest.get("releaseVersion"))
    channel = normalize(manifest.get("channelId") or manifest.get("channel")).lower()
    if not version:
        fail(f"{label} has no release version")
    if channel != "preview":
        fail(f"{label} must identify the preview channel")
    if normalize(manifest.get("contractName") or manifest.get("contract_name")) != RELEASE_MANIFEST_CONTRACT_NAME:
        fail(f"{label} has the wrong Registry release contract")
    return version, channel


def compatibility_file_name(row: dict[str, Any]) -> str:
    raw = normalize(row.get("fileName"))
    if not raw:
        raw = Path(normalize(row.get("downloadUrl") or row.get("url"))).name
    if not raw or Path(raw).name != raw or raw in {".", ".."}:
        fail(f"compatibility manifest has an unsafe fileName: {raw!r}")
    return raw


def compatibility_sha256(row: dict[str, Any]) -> str:
    value = normalize(row.get("sha256") or row.get("artifactSha256") or row.get("digest"))
    return require_sha256(value.removeprefix("sha256:"), "compatibility artifact sha256")


def verify_compatibility_manifest(
    canonical: dict[str, Any],
    compatibility: dict[str, Any],
    files_dir: Path,
) -> None:
    canonical_version, canonical_channel = require_preview_manifest_identity(
        canonical, "canonical manifest"
    )
    compatibility_version, compatibility_channel = require_preview_manifest_identity(
        compatibility, "compatibility manifest"
    )
    if (compatibility_version, compatibility_channel) != (canonical_version, canonical_channel):
        fail("releases.json release identity differs from the canonical manifest")
    canonical_contract = normalize(canonical.get("contractName") or canonical.get("contract_name"))
    compatibility_contract = normalize(
        compatibility.get("contractName") or compatibility.get("contract_name")
    )
    if not canonical_contract or compatibility_contract != canonical_contract:
        fail("releases.json contract differs from the canonical manifest")
    canonical_rows = canonical.get("artifacts")
    download_rows = compatibility.get("downloads")
    if not isinstance(canonical_rows, list) or not canonical_rows:
        fail("canonical manifest has no artifacts")
    if not isinstance(download_rows, list) or not download_rows:
        fail("releases.json has no downloads")
    canonical_by_name = {
        artifact_file_name(row): row for row in canonical_rows if isinstance(row, dict)
    }
    downloads_by_name: dict[str, dict[str, Any]] = {}
    for raw_row in download_rows:
        if not isinstance(raw_row, dict):
            fail("releases.json contains a non-object download row")
        name = compatibility_file_name(raw_row)
        if name in downloads_by_name:
            fail(f"releases.json repeats download fileName: {name}")
        downloads_by_name[name] = raw_row
    if set(downloads_by_name) != set(canonical_by_name):
        missing = sorted(set(canonical_by_name) - set(downloads_by_name))
        extra = sorted(set(downloads_by_name) - set(canonical_by_name))
        fail(f"releases.json artifact set differs from canonical manifest: missing={missing} extra={extra}")
    for name, canonical_row in canonical_by_name.items():
        download = downloads_by_name[name]
        path = files_dir / name
        if path.is_symlink() or not path.is_file():
            fail(f"releases.json download bytes are missing: {name}")
        if compatibility_sha256(download) != artifact_sha256(canonical_row):
            fail(f"releases.json digest differs from canonical manifest for {name}")
        if int(download.get("sizeBytes") or 0) != int(canonical_row.get("sizeBytes") or 0):
            fail(f"releases.json size differs from canonical manifest for {name}")
        for field in ("artifactId", "head", "kind"):
            if normalize(download.get(field)) != normalize(canonical_row.get(field)):
                fail(f"releases.json {field} differs from canonical manifest for {name}")
        for field in (
            "installerMode",
            "payloadFileName",
            "payloadSha256",
            "payloadSizeBytes",
            "installAccessClass",
        ):
            if download.get(field) != canonical_row.get(field):
                fail(f"releases.json {field} differs from canonical manifest for {name}")
        canonical_platform = normalize(canonical_row.get("platform")).lower()
        download_platform = normalize(download.get("platform")).lower()
        platform_id = normalize(download.get("platformId")).lower()
        if download_platform:
            if download_platform != canonical_platform:
                fail(f"releases.json platform differs from canonical manifest for {name}")
        elif not platform_id.startswith(canonical_platform + "-"):
            fail(f"releases.json platformId differs from canonical manifest for {name}")
        canonical_rid = normalize(canonical_row.get("rid")).lower()
        download_rid = normalize(download.get("rid")).lower()
        download_arch = normalize(download.get("arch")).lower()
        if download_rid:
            if download_rid != canonical_rid:
                fail(f"releases.json rid differs from canonical manifest for {name}")
        elif not canonical_rid.endswith("-" + download_arch):
            fail(f"releases.json arch differs from canonical manifest for {name}")


def verify_files_shelf_scope(manifest: dict[str, Any], files_dir: Path) -> None:
    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        fail("canonical manifest has no artifacts")
    allowed = {
        artifact_file_name(row) for row in artifacts if isinstance(row, dict)
    }
    for row in artifacts:
        if not isinstance(row, dict):
            continue
        payload_name = normalize(row.get("payloadFileName"))
        if payload_name:
            if Path(payload_name).name != payload_name:
                fail(f"canonical manifest has unsafe payloadFileName: {payload_name!r}")
            allowed.add(payload_name)
    actual = {path.relative_to(files_dir).as_posix() for path in safe_tree_entries(files_dir)}
    unexplained = sorted(
        name for name in actual - allowed if not name.endswith((".json", ".sha256"))
    )
    if unexplained:
        fail(f"files shelf contains bytes not bound by the canonical manifest: {unexplained}")


def hydrate_retained_shelf(source_root: Path, candidate_dir: Path) -> dict[str, Any]:
    if not source_root.is_absolute() or source_root.is_symlink() or not source_root.is_dir():
        fail("CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT must be an absolute non-symlink directory")
    source_root = source_root.resolve(strict=True)
    canonical = require_exact_file(
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_PATH",
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_SHA256",
        "retained canonical manifest",
    )
    compatibility = require_exact_file(
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_PATH",
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_SHA256",
        "retained compatibility manifest",
    )
    for path in (canonical, compatibility):
        try:
            path.relative_to(source_root)
        except ValueError:
            fail(f"retained manifest must be inside retained shelf root: {path}")
    files_source = source_root / "files"
    if files_source.is_symlink() or not files_source.is_dir():
        fail("retained shelf must contain a regular files directory")
    manifest = read_json(canonical)
    compatibility_payload = read_json(compatibility)
    require_preview_manifest_identity(manifest, "retained canonical manifest")
    verify_manifest_files(manifest, files_source)
    verify_compatibility_manifest(manifest, compatibility_payload, files_source)
    verify_files_shelf_scope(manifest, files_source)

    retained_root = candidate_dir / "retained-source"
    retained_root.mkdir(mode=0o700)
    shutil.copy2(canonical, retained_root / "RELEASE_CHANNEL.generated.json")
    shutil.copy2(compatibility, retained_root / "releases.json")
    files_inventory = copy_safe_tree(files_source, retained_root / "files")
    for optional_name in ("startup-smoke", "signing"):
        source = source_root / optional_name
        if source.exists():
            if source.is_symlink() or not source.is_dir():
                fail(f"retained {optional_name} must be a non-symlink directory")
            copy_safe_tree(source, retained_root / optional_name)
        else:
            (retained_root / optional_name).mkdir(mode=0o700)
    for current_name in ("files", "startup-smoke", "signing"):
        (candidate_dir / current_name).mkdir(mode=0o700)
    return {
        "version": normalize(manifest.get("version")),
        "canonicalSha256": sha256_file(canonical),
        "compatibilitySha256": sha256_file(compatibility),
        "filesInventorySha256": inventory_sha256(files_inventory),
        "fileCount": len(files_inventory),
        "files": files_inventory,
    }


def validate_retained_files_inventory(retained: dict[str, Any]) -> list[dict[str, Any]]:
    """Validate the complete incumbent files-shelf inventory receipt."""
    raw_rows = retained.get("files")
    if not isinstance(raw_rows, list):
        fail("prepared inputs have no retained files inventory")
    rows: list[dict[str, Any]] = []
    seen: set[str] = set()
    for raw_row in raw_rows:
        if not isinstance(raw_row, dict) or set(raw_row) != {"path", "sha256", "sizeBytes"}:
            fail("prepared retained files inventory contains a malformed row")
        relative = normalize(raw_row.get("path"))
        portable = PurePosixPath(relative)
        if (
            not relative
            or portable.is_absolute()
            or relative != portable.as_posix()
            or any(part in {"", ".", ".."} for part in portable.parts)
            or "\\" in relative
        ):
            fail(f"prepared retained files inventory has an unsafe path: {relative!r}")
        if relative in seen:
            fail(f"prepared retained files inventory repeats a path: {relative}")
        seen.add(relative)
        digest = require_sha256(normalize(raw_row.get("sha256")), f"retained file {relative} sha256")
        size = raw_row.get("sizeBytes")
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            fail(f"prepared retained files inventory has an invalid size for {relative}")
        rows.append({"path": relative, "sha256": digest, "sizeBytes": size})
    if retained.get("fileCount") != len(rows):
        fail("prepared retained files inventory fileCount differs")
    expected_inventory = require_sha256(
        normalize(retained.get("filesInventorySha256")),
        "retainedShelf.filesInventorySha256",
    )
    if inventory_sha256(rows) != expected_inventory:
        fail("prepared retained files inventory digest differs")
    return rows


def verify_retained_files_inventory(
    stage_dir: Path,
    retained: dict[str, Any],
    retained_manifest: dict[str, Any],
) -> None:
    """Recheck every incumbent byte in the isolated rollback shelf."""
    rows = validate_retained_files_inventory(retained)
    if not isinstance(retained_manifest.get("artifacts"), list):
        fail("retained canonical manifest has no artifact rows")
    files_root = stage_dir / "retained-source" / "files"
    for row in rows:
        relative = row["path"]
        path = files_root / relative
        if path.is_symlink() or not path.is_file():
            fail(f"sealed stage dropped retained shelf file: {relative}")
        if sha256_file(path) != row["sha256"] or path.stat().st_size != row["sizeBytes"]:
            fail(f"sealed stage changed retained shelf file bytes: {relative}")


def prepare_inputs(presentation_root: Path, candidate_dir: Path) -> dict[str, Any]:
    if candidate_dir.is_symlink() or not candidate_dir.is_dir():
        fail("candidate directory must already exist as a non-symlink directory")
    if any(candidate_dir.iterdir()):
        fail("candidate directory must be empty")
    authorities = validate_authorities(presentation_root)
    version, published_at, stage_path = validate_paths_and_identity(candidate_dir)
    retained_root_text = normalize(os.environ.get("CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT"))
    if not retained_root_text:
        fail("CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT is required")
    retained_root = Path(retained_root_text)
    retained = hydrate_retained_shelf(retained_root, candidate_dir)
    inputs_dir = candidate_dir / "proof" / "inputs"
    inputs_dir.mkdir(parents=True, mode=0o700)
    input_rows: dict[str, dict[str, str]] = {}
    for input_name, path_env, sha_env, target_name in EXACT_PROOF_INPUTS:
        source = require_exact_file(path_env, sha_env, input_name)
        target = inputs_dir / target_name
        shutil.copy2(source, target)
        input_rows[input_name] = {
            "path": target.relative_to(candidate_dir).as_posix(),
            "sha256": sha256_file(target),
        }
    payload = {
        "contractName": INPUT_CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "status": "validated",
        "release": {"channel": "preview", "version": version, "publishedAt": published_at},
        "authorities": authorities,
        "nativeWindowsEvidenceAuthority": native_evidence_authority(
            presentation_root, authorities
        ),
        "retainedShelf": retained,
        "inputs": input_rows,
        "output": {
            "candidateBasename": candidate_dir.name,
            "sealedStageBasename": stage_path.name,
            "mode": "stage_only",
        },
    }
    write_json(candidate_dir / INPUT_FILE_NAME, payload)
    return payload


def compare_authorities_with_receipt(receipt: dict[str, Any], current: list[dict[str, str]]) -> None:
    recorded = receipt.get("authorities")
    if recorded != current:
        fail("repository authorities changed after candidate preparation")


def load_authenticated_native_reviewer(stage_dir: Path) -> str:
    payload = read_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json")
    raw_reviewers = payload.get("visualReviewers")
    if not isinstance(raw_reviewers, dict) or set(raw_reviewers) != set(
        PROMOTED_WINDOWS_HEADS
    ):
        fail("native Windows evidence has no per-head authenticated reviewer map")
    reviewers = {normalize(value) for value in raw_reviewers.values() if normalize(value)}
    if len(reviewers) != 1:
        fail("native Windows evidence heads do not share one authenticated reviewer")
    reviewer = next(iter(reviewers))
    if not GITHUB_LOGIN_RE.fullmatch(reviewer):
        fail("native Windows evidence reviewer is not a GitHub login")
    return reviewer


def require_exact_promoted_desktop_scope(manifest: dict[str, Any]) -> None:
    coverage = manifest.get("desktopTupleCoverage")
    if not isinstance(coverage, dict):
        fail("canonical manifest desktopTupleCoverage must be an object")
    if coverage.get("requiredDesktopHeads") != list(PROMOTED_WINDOWS_HEADS):
        fail("canonical manifest requiredDesktopHeads differs from the promoted head set")
    platforms = coverage.get("requiredDesktopPlatforms")
    if platforms != list(ACTIVE_PREVIEW_DESKTOP_PLATFORMS):
        fail("canonical build-evidence requiredDesktopPlatforms differs from the active build set")
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("canonical manifest artifacts must be a list")
    expected = set(CURRENT_NIGHTLY_TUPLES)
    observed: list[tuple[str, str, str]] = []
    for row in rows:
        if not isinstance(row, dict):
            fail("canonical manifest contains a non-object artifact row")
        platform_aliases = [
            row[key]
            for key in ("platform", "platformId")
            if key in row and row[key] is not None
        ]
        if (
            not platform_aliases
            or any(
                not isinstance(value, str) or value != value.strip().lower()
                for value in platform_aliases
            )
            or len(set(platform_aliases)) != 1
        ):
            fail("canonical manifest desktop artifact has no exact platform identity")
        platform = normalize(row.get("platform"))
        if platform not in REGISTRY_REQUIRED_DESKTOP_PLATFORMS:
            fail("canonical manifest contains an artifact outside the active desktop platforms")
        aliases = [
            row[key]
            for key in ("head", "headId")
            if key in row and row[key] is not None
        ]
        if (
            not aliases
            or any(
                not isinstance(value, str) or value != value.strip().lower()
                for value in aliases
            )
            or len(set(aliases)) != 1
        ):
            fail("canonical manifest desktop artifact has no exact head identity")
        key = (aliases[0], platform, normalize(row.get("rid")))
        if platform not in ACTIVE_PREVIEW_DESKTOP_PLATFORMS:
            fail("canonical manifest contains an artifact outside the active desktop platforms")
        if aliases[0] not in PROMOTED_WINDOWS_HEADS:
            fail("canonical manifest contains an unpromoted desktop head")
        if normalize(row.get("kind")) != "installer":
            fail(f"current nightly tuple is not an installer: {':'.join(key)}")
        if key not in expected:
            fail("canonical manifest active desktop artifact scope differs from the promoted tuple set")
        expected_identity = CURRENT_NIGHTLY_ARTIFACT_IDENTITIES[key]
        if (row.get("artifactId"), row.get("fileName")) != expected_identity:
            fail("canonical manifest active desktop artifact identity is not exact")
        observed.append(key)
    if len(observed) != len(expected) or set(observed) != expected:
        fail("canonical manifest promoted desktop artifact set is not exact")


def require_current_artifacts(stage_dir: Path) -> tuple[dict[str, Any], dict[tuple[str, str, str], dict[str, Any]]]:
    manifest_path = stage_dir / "RELEASE_CHANNEL.generated.json"
    if manifest_path.is_symlink() or not manifest_path.is_file():
        fail("stage is missing RELEASE_CHANNEL.generated.json")
    manifest = read_json(manifest_path)
    require_preview_manifest_identity(manifest, "canonical manifest")
    require_exact_promoted_desktop_scope(manifest)
    tuples = verify_manifest_files(manifest, stage_dir / "files")
    missing = [key for key in CURRENT_NIGHTLY_TUPLES if key not in tuples]
    if missing:
        fail("stage is missing current nightly tuples: " + ", ".join(":".join(key) for key in missing))
    expected_extensions = {"windows": "-installer.exe", "linux": "-installer.deb"}
    for key in CURRENT_NIGHTLY_TUPLES:
        head, platform, rid = key
        row = tuples[key]
        if normalize(row.get("kind")).lower() != "installer":
            fail(f"current nightly tuple is not an installer: {':'.join(key)}")
        if normalize(row.get("artifactId")) != f"{head}-{rid}-installer":
            fail(f"current nightly tuple has the wrong artifactId: {':'.join(key)}")
        if not artifact_file_name(row).endswith(expected_extensions[platform]):
            fail(f"current nightly tuple has the wrong installer filename: {':'.join(key)}")
    return manifest, tuples


def verify_supply_chain_gate(
    stage_dir: Path,
    manifest: dict[str, Any],
    authorities: list[dict[str, str]],
) -> dict[str, Any]:
    presentation_commit = next(
        (
            normalize(row.get("commit"))
            for row in authorities
            if isinstance(row, dict) and row.get("name") == "presentation"
        ),
        "",
    )
    if not COMMIT_RE.fullmatch(presentation_commit):
        fail("presentation authority commit is unavailable for supply-chain evidence")
    version, _ = require_preview_manifest_identity(manifest, "canonical manifest")
    try:
        SUPPLY_CHAIN.verify_gate(
            stage_root=stage_dir,
            version=version,
            source_commit=presentation_commit,
        )
        return SUPPLY_CHAIN.content_bindings(stage_dir)
    except SUPPLY_CHAIN.SupplyChainError as exc:
        fail(f"exact RID supply-chain gate failed: {exc}")


def receipt_digest(value: object) -> str:
    return normalize(value).lower().removeprefix("sha256:")


def verify_current_startup_receipts(
    stage_dir: Path,
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    *,
    require_native_windows: bool,
) -> None:
    manifest = read_json(stage_dir / "RELEASE_CHANNEL.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    startup_dir = stage_dir / "startup-smoke"
    for key in CURRENT_NIGHTLY_TUPLES:
        head, platform, rid = key
        receipt_path = startup_dir / f"startup-smoke-{head}-{rid}.receipt.json"
        receipt = read_json(require_local_regular_file(str(receipt_path.resolve(strict=False)), str(receipt_path)))
        if normalize(receipt.get("status")).lower() not in {"pass", "passed", "ready"}:
            fail(f"startup smoke is not passing for {':'.join(key)}")
        if normalize(receipt.get("headId")).lower() != head or normalize(receipt.get("rid")).lower() != rid:
            fail(f"startup smoke tuple mismatch for {receipt_path.name}")
        if normalize(receipt.get("platform")).lower() != platform:
            fail(f"startup smoke platform mismatch for {receipt_path.name}")
        if normalize(receipt.get("version") or receipt.get("releaseVersion")) != version:
            fail(f"startup smoke version mismatch for {receipt_path.name}")
        if normalize(receipt.get("channelId") or receipt.get("channel")).lower() != channel:
            fail(f"startup smoke channel mismatch for {receipt_path.name}")
        if normalize(receipt.get("artifactFileName") or receipt.get("fileName")) != artifact_file_name(
            tuples[key]
        ):
            fail(f"startup smoke artifact filename mismatch for {receipt_path.name}")
        if receipt_digest(receipt.get("artifactDigest")) != artifact_sha256(tuples[key]):
            fail(f"startup smoke artifact digest mismatch for {receipt_path.name}")
        if platform != "windows":
            continue
        artifact = tuples[key]
        if normalize(artifact.get("installerMode")).lower() != "bootstrap":
            fail(f"Windows current tuple must use a bootstrap installer: {':'.join(key)}")
        if normalize(artifact.get("payloadAcquisitionMode")).lower() != "download":
            fail(f"Windows current tuple must declare download payload acquisition: {':'.join(key)}")
        payload_name = normalize(artifact.get("payloadFileName"))
        if not payload_name or Path(payload_name).name != payload_name:
            fail(f"Windows current tuple has no safe payloadFileName: {':'.join(key)}")
        payload_path = stage_dir / "files" / payload_name
        if payload_path.is_symlink() or not payload_path.is_file():
            fail(f"Windows bootstrap payload bytes are missing: {payload_name}")
        payload_sha = require_sha256(
            normalize(artifact.get("payloadSha256")), f"{payload_name} payloadSha256"
        )
        try:
            payload_size = int(artifact.get("payloadSizeBytes"))
        except (TypeError, ValueError):
            fail(f"Windows current tuple has invalid payloadSizeBytes: {':'.join(key)}")
        if sha256_file(payload_path) != payload_sha or payload_path.stat().st_size != payload_size:
            fail(f"Windows bootstrap payload bytes differ from the manifest: {payload_name}")
        if normalize(receipt.get("bootstrapPayloadAcquisitionMode")).lower() != "download":
            fail(f"Windows startup smoke did not prove download acquisition: {receipt_path.name}")
        if normalize(receipt.get("bootstrapPayloadFileName")) != payload_name:
            fail(f"Windows startup smoke payload filename mismatch: {receipt_path.name}")
        if receipt_digest(receipt.get("bootstrapPayloadSha256")) != payload_sha:
            fail(f"Windows startup smoke payload digest mismatch: {receipt_path.name}")
        try:
            receipt_payload_size = int(receipt.get("bootstrapPayloadSizeBytes"))
        except (TypeError, ValueError):
            receipt_payload_size = -1
        if receipt_payload_size != payload_size:
            fail(f"Windows startup smoke payload size mismatch: {receipt_path.name}")
        if normalize(receipt.get("readyCheckpoint")).lower() != "pre_ui_event_loop":
            fail(f"Windows startup smoke did not reach pre_ui_event_loop: {receipt_path.name}")
        progress_log = startup_dir / f"windows-installer-progress-{head}-{rid}.log"
        if progress_log.is_symlink() or not progress_log.is_file():
            fail(f"Windows download smoke progress log is missing: {progress_log.name}")
        progress_text = progress_log.read_text(encoding="utf-8-sig", errors="replace")
        for marker in (
            "Bootstrap temp root:",
            "Payload download target:",
            "Downloading application files",
            "Verifying payload size",
            "Verifying payload checksum",
            "Extracting application files",
            "Install complete",
        ):
            if marker not in progress_text:
                fail(f"Windows download smoke progress log is missing {marker!r}: {progress_log.name}")
        if require_native_windows:
            if normalize(receipt.get("executionEnvironment")).lower() != "native_windows":
                fail(f"native Windows execution evidence is required for {receipt_path.name}")
            host = receipt.get("nativeHostEvidence")
            if not isinstance(host, dict):
                fail(f"native Windows host evidence is required for {receipt_path.name}")
            if normalize(host.get("contractName")) != NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME:
                fail(f"native Windows host evidence has the wrong contract: {receipt_path.name}")
            if normalize(host.get("status")).lower() != "verified" or host.get("isNativeWindows") is not True:
                fail(f"native Windows host evidence is not verified: {receipt_path.name}")
            if normalize(host.get("hostPlatform")).lower() != "windows":
                fail(f"native Windows host evidence has the wrong platform: {receipt_path.name}")
            for field in ("hostKernel", "runner", "evidenceSource"):
                if not normalize(host.get(field)):
                    fail(f"native Windows host evidence is missing {field}: {receipt_path.name}")
            if "wine" in normalize(host.get("runner")).lower():
                fail(f"native Windows host evidence cannot use Wine: {receipt_path.name}")


def verify_retained_shelf_preservation(
    stage_dir: Path,
    incoming_tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> None:
    retained_path = stage_dir / "retained-source" / "RELEASE_CHANNEL.generated.json"
    retained = read_json(retained_path)
    retained_rows = retained.get("artifacts")
    if not isinstance(retained_rows, list) or not retained_rows:
        fail("retained canonical manifest has no artifact rows")
    incoming_manifest = read_json(stage_dir / "RELEASE_CHANNEL.generated.json")
    incoming_rows = incoming_manifest.get("artifacts")
    if not isinstance(incoming_rows, list):
        fail("incoming canonical manifest has no artifact rows")
    incoming_by_id: dict[str, dict[str, Any]] = {}
    for row in incoming_rows:
        if not isinstance(row, dict):
            fail("incoming canonical manifest contains a non-object artifact row")
        artifact_id = normalize(row.get("artifactId"))
        if not artifact_id or artifact_id in incoming_by_id:
            fail("incoming canonical manifest has missing or duplicate artifactId")
        incoming_by_id[artifact_id] = row
    for raw_row in retained_rows:
        if not isinstance(raw_row, dict):
            fail("retained canonical manifest contains a non-object artifact row")
        key = (
            normalize(raw_row.get("head")).lower(),
            normalize(raw_row.get("platform")).lower(),
            normalize(raw_row.get("rid")).lower(),
        )
        if not all(key):
            fail("retained canonical manifest has an artifact without a desktop tuple")
        retained_id = normalize(raw_row.get("artifactId"))
        incoming = incoming_by_id.get(retained_id)
        if key in CURRENT_NIGHTLY_TUPLES:
            if key not in incoming_tuples or incoming is None:
                fail(f"sealed stage did not replace retained current tuple: {':'.join(key)}")
            if normalize(incoming.get("kind")).lower() != normalize(raw_row.get("kind")).lower():
                fail(f"sealed stage changed retained current tuple kind: {':'.join(key)}")
            continue
        if incoming is not None:
            fail(f"incoming manifest reintroduced retained non-current tuple: {':'.join(key)}")


def publication_scope_required(stage_dir: Path) -> bool:
    configured = normalize(os.environ.get("CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED")).lower()
    return configured in {"1", "true", "yes", "on"} or any(
        (stage_dir / name).is_file()
        for name in (
            PUBLICATION_SCOPE.PROPOSAL_FILE_NAME,
            PUBLICATION_SCOPE.FINAL_FILE_NAME,
        )
    )


def require_complete_windows_only_registry_shelf(stage_dir: Path) -> None:
    """Bind the Windows delta to the exact incumbent-derived public shelf."""
    proposal_path = stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
    proposal = read_json(proposal_path)
    try:
        PUBLICATION_SCOPE.validate_proposal(proposal)
    except PUBLICATION_SCOPE.ScopeError as exc:
        fail(f"Windows-only publication proposal is invalid: {exc}")

    delta = proposal.get("publicationDeltaTuples")
    retained = proposal.get("retainedTuples")
    post = proposal.get("postPublicationShelfTuples")
    if not all(isinstance(rows, list) for rows in (delta, retained, post)):
        fail("Windows-only publication proposal has malformed shelf tuple sets")
    if {normalize(row.get("platform")) for row in delta if isinstance(row, dict)} != {
        "windows"
    }:
        fail("Windows-only publication delta must contain only Windows tuples")
    snapshot = proposal.get("incumbentSnapshot")
    if not isinstance(snapshot, dict):
        fail("Windows-only publication proposal has no exact incumbent snapshot")
    incumbent_platforms_raw = snapshot.get("platforms")
    if (
        not isinstance(incumbent_platforms_raw, list)
        or not incumbent_platforms_raw
        or any(
            not isinstance(platform, str) or not platform
            for platform in incumbent_platforms_raw
        )
        or incumbent_platforms_raw != sorted(set(incumbent_platforms_raw))
    ):
        fail("Windows-only incumbent platform set is malformed")
    incumbent_platforms = set(incumbent_platforms_raw)
    retained_platforms = {
        normalize(row.get("platform")) for row in retained if isinstance(row, dict)
    }
    if retained_platforms != incumbent_platforms - {"windows"}:
        fail("Windows-only publication did not retain every incumbent non-Windows platform")
    post_platforms = {
        normalize(row.get("platform")) for row in post if isinstance(row, dict)
    }
    required = retained_platforms | {"windows"}
    if post_platforms != required:
        fail("Windows-only publication shelf differs from retained platforms plus Windows")
    expected_public_platforms = sorted(required)
    expected_incumbent_platforms = sorted(incumbent_platforms)

    for relative, rows_key, label in (
        (
            PUBLICATION_SCOPE.PUBLICATION_MANIFEST_RELATIVE_PATH,
            "artifacts",
            "full shelf canonical manifest",
        ),
        (
            PUBLICATION_SCOPE.PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH,
            "downloads",
            "full shelf compatibility manifest",
        ),
    ):
        payload = read_json(stage_dir / relative)
        coverage = payload.get("desktopTupleCoverage")
        if (
            not isinstance(coverage, dict)
            or coverage.get("requiredDesktopPlatforms")
            != expected_public_platforms
        ):
            fail(f"{label} coverage differs from the exact incumbent-derived shelf")
        rows = payload.get(rows_key)
        if not isinstance(rows, list) or {
            normalize(row.get("platformId") or row.get("platform"))
            for row in rows
            if isinstance(row, dict)
        } != required:
            fail(f"{label} does not expose retained platforms plus Windows")

    for relative, rows_key, label in (
        (
            "retained-source/RELEASE_CHANNEL.generated.json",
            "artifacts",
            "incumbent canonical channel",
        ),
        (
            "retained-source/releases.json",
            "downloads",
            "incumbent compatibility channel",
        ),
    ):
        incumbent = read_json(stage_dir / relative)
        retained_coverage = incumbent.get("desktopTupleCoverage")
        rows = incumbent.get(rows_key)
        if (
            not isinstance(retained_coverage, dict)
            or retained_coverage.get("requiredDesktopPlatforms")
            != expected_incumbent_platforms
            or not isinstance(rows, list)
            or {
                normalize(row.get("platformId") or row.get("platform"))
                for row in rows
                if isinstance(row, dict)
            }
            != incumbent_platforms
        ):
            fail(f"{label} differs from the exact sealed incumbent platform set")


def verify_pre_capture_publication_scope(
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    if not publication_scope_required(stage_dir):
        return {}
    artifact = tuples[(PROMOTED_WINDOWS_HEADS[0], "windows", "win-x64")]
    try:
        binding = PUBLICATION_SCOPE.validate_export_inputs(
            stage_dir,
            expected_version=normalize(manifest.get("version") or manifest.get("releaseVersion")),
            installer_sha256=artifact_sha256(artifact),
            payload_sha256=require_sha256(
                normalize(artifact.get("payloadSha256")), "Windows payload sha256"
            ),
        )
    except PUBLICATION_SCOPE.ScopeError as exc:
        fail(f"Windows-only publication scope is invalid: {exc}")
    require_complete_windows_only_registry_shelf(stage_dir)
    return binding


def mark_candidate(presentation_root: Path, stage_dir: Path) -> dict[str, Any]:
    inputs = read_json(stage_dir / INPUT_FILE_NAME)
    compare_authorities_with_receipt(inputs, validate_authorities(presentation_root))
    manifest, tuples = require_current_artifacts(stage_dir)
    supply_chain = verify_supply_chain_gate(stage_dir, manifest, inputs["authorities"])
    verify_current_startup_receipts(stage_dir, tuples, require_native_windows=False)
    publication_scope = verify_pre_capture_publication_scope(stage_dir, manifest, tuples)
    version = normalize(inputs.get("release", {}).get("version"))
    if normalize(manifest.get("version")) != version:
        fail("generated canonical manifest version does not match prepared inputs")
    if normalize(manifest.get("channelId") or manifest.get("channel")).lower() != "preview":
        fail("generated canonical manifest must be preview")
    compatibility_proof = stage_dir / "proof" / "windows-compatibility-startup"
    if compatibility_proof.exists():
        fail("compatibility startup proof destination already exists")
    compatibility_proof.mkdir(parents=True)
    for head in PROMOTED_WINDOWS_HEADS:
        name = f"startup-smoke-{head}-win-x64.receipt.json"
        shutil.copy2(stage_dir / "startup-smoke" / name, compatibility_proof / name)
    payload = {
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "status": "awaiting_native_windows_evidence",
        "uploadAuthorized": False,
        "release": inputs["release"],
        "authorities": inputs["authorities"],
        "manifestSha256": sha256_file(stage_dir / "RELEASE_CHANNEL.generated.json"),
        "supplyChain": supply_chain,
        "compatibilityWindowsDownloadSmoke": {
            "status": "preserved",
            "path": "proof/windows-compatibility-startup",
        },
        "nextRequiredAction": "Capture exact native-Windows startup and installer visual evidence, then run seal.",
    }
    if publication_scope:
        payload["publicationScope"] = publication_scope
        payload["publicationScopeRequired"] = True
    write_json(stage_dir / CANDIDATE_FILE_NAME, payload)
    return payload


def validate_candidate(presentation_root: Path, stage_dir: Path) -> dict[str, Any]:
    inputs = read_json(stage_dir / INPUT_FILE_NAME)
    candidate = read_json(stage_dir / CANDIDATE_FILE_NAME)
    current_authorities = validate_authorities(presentation_root)
    compare_authorities_with_receipt(inputs, current_authorities)
    if inputs.get("nativeWindowsEvidenceAuthority") != native_evidence_authority(
        presentation_root, current_authorities
    ):
        fail("native Windows evidence authority changed after candidate preparation")
    if candidate.get("authorities") != current_authorities:
        fail("candidate authority receipt disagrees with current repository authorities")
    version, published_at, _ = validate_paths_and_identity(stage_dir)
    recorded_release = inputs.get("release")
    if not isinstance(recorded_release, dict):
        fail("prepared inputs are missing release identity")
    if normalize(recorded_release.get("version")) != version:
        fail("candidate version environment differs from prepared inputs")
    if normalize(recorded_release.get("publishedAt")) != published_at:
        fail("candidate timestamp environment differs from prepared inputs")
    if candidate.get("status") != "awaiting_native_windows_evidence":
        fail("candidate is not awaiting native Windows evidence")
    manifest, tuples = require_current_artifacts(stage_dir)
    if normalize(manifest.get("version")) != version:
        fail("candidate manifest version differs from prepared inputs")
    supply_chain = verify_supply_chain_gate(stage_dir, manifest, current_authorities)
    if candidate.get("supplyChain") != supply_chain:
        fail("candidate supply-chain binding disagrees with exact current evidence")
    publication_scope = verify_pre_capture_publication_scope(stage_dir, manifest, tuples)
    if publication_scope:
        if candidate.get("publicationScopeRequired") is not True or candidate.get(
            "publicationScope"
        ) != publication_scope:
            fail("candidate publication scope differs from exact signed pre-capture bytes")
    elif candidate.get("publicationScopeRequired") is not None:
        fail("candidate unexpectedly claims a publication scope")
    return candidate


def validate_native_evidence_tree(source: Path, expected_sha256: str) -> list[dict[str, Any]]:
    if not source.is_absolute() or source.is_symlink() or not source.is_dir():
        fail("native Windows evidence root must be an absolute non-symlink directory")
    rows = inventory_tree(source)
    actual = inventory_sha256(rows)
    expected = require_sha256(expected_sha256, "CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_SHA256")
    if actual != expected:
        fail(f"native Windows evidence tree digest mismatch: expected {expected}, got {actual}")
    return rows


def validate_capture_inventory(
    native_root: Path,
) -> tuple[dict[str, Any], dict[str, dict[str, Any]], str]:
    path = evidence_relative_file(
        native_root,
        NATIVE_CAPTURE_INVENTORY_FILE_NAME,
        "native capture inventory",
    )
    payload = read_json(path)
    capture_path = evidence_relative_file(
        native_root, NATIVE_CAPTURE_FILE_NAME, "native capture"
    )
    raw_capture = read_json(capture_path)
    raw_candidate = raw_capture.get("candidate")
    expected_version = (
        2
        if isinstance(raw_candidate, dict) and "publicationScope" in raw_candidate
        else CONTRACT_VERSION
    )
    if (
        payload.get("contractName") != NATIVE_CAPTURE_INVENTORY_CONTRACT_NAME
        or payload.get("contractVersion") != expected_version
        or payload.get("captureContract") != NATIVE_CAPTURE_CONTRACT_NAME
    ):
        fail("native capture inventory has the wrong contract")
    rows = payload.get("files")
    if not isinstance(rows, list) or not rows:
        fail("native capture inventory has no files")
    normalized_rows: list[dict[str, Any]] = []
    by_path: dict[str, dict[str, Any]] = {}
    for raw_row in rows:
        if not isinstance(raw_row, dict) or set(raw_row) != {"path", "sha256", "sizeBytes"}:
            fail("native capture inventory contains a malformed row")
        relative = normalize(raw_row.get("path"))
        if relative in by_path:
            fail(f"native capture inventory repeats a path: {relative}")
        file_path = evidence_relative_file(native_root, relative, f"native capture file {relative}")
        digest = require_sha256(normalize(raw_row.get("sha256")), f"native capture file {relative}")
        size = raw_row.get("sizeBytes")
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            fail(f"native capture inventory has an invalid size for {relative}")
        if sha256_file(file_path) != digest or file_path.stat().st_size != size:
            fail(f"native capture inventory bytes differ for {relative}")
        row = {"path": relative, "sha256": digest, "sizeBytes": size}
        normalized_rows.append(row)
        by_path[relative] = row
    if normalized_rows != sorted(normalized_rows, key=lambda row: row["path"]):
        fail("native capture inventory is not in canonical path order")
    if payload.get("captureManifestSha256") != sha256_file(capture_path):
        fail("native capture inventory does not bind the capture manifest")
    return payload, by_path, sha256_file(path)


def require_capture_inventory_file(
    native_root: Path,
    inventory: dict[str, dict[str, Any]],
    relative: str,
    label: str,
) -> Path:
    row = inventory.get(relative)
    if row is None:
        fail(f"native capture inventory is missing {label}: {relative}")
    path = evidence_relative_file(native_root, relative, label)
    if sha256_file(path) != row["sha256"] or path.stat().st_size != row["sizeBytes"]:
        fail(f"native capture inventory binding changed for {label}")
    return path


def _validate_finalized_native_evidence_extraction(
    stage_dir: Path,
    native_root: Path,
    archive: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    inputs = read_json(stage_dir / INPUT_FILE_NAME)
    authority = verify_native_evidence_authority_receipt(inputs)
    _, inventory, inventory_sha = validate_capture_inventory(native_root)
    capture_path = require_capture_inventory_file(
        native_root, inventory, NATIVE_CAPTURE_FILE_NAME, "native capture manifest"
    )
    capture = read_json(capture_path)
    raw_candidate = capture.get("candidate")
    expected_capture_version = (
        2
        if isinstance(raw_candidate, dict) and "publicationScope" in raw_candidate
        else CONTRACT_VERSION
    )
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if (
        capture.get("contractName") != NATIVE_CAPTURE_CONTRACT_NAME
        or capture.get("contractVersion") != expected_capture_version
        or normalize(capture.get("status")).lower() != "captured"
        or normalize(capture.get("captureMode")).lower() != "interactive"
        or normalize(capture.get("version")) != version
        or normalize(capture.get("channelId")).lower() != channel
    ):
        fail("native Windows capture manifest has the wrong contract or release identity")
    capture_source = validate_github_workflow_source(
        capture.get("source"),
        label="native capture",
        authority=authority,
        workflow=NATIVE_CAPTURE_WORKFLOW,
        artifact_prefix="windows-native-evidence",
    )
    candidate_provenance = validate_candidate_producer_provenance(
        stage_dir,
        native_root,
        inventory,
        capture.get("candidate"),
        authority,
        tuples,
    )

    raw_heads = capture.get("heads")
    if not isinstance(raw_heads, list) or [
        normalize(row.get("headId")).lower() for row in raw_heads if isinstance(row, dict)
    ] != list(PROMOTED_WINDOWS_HEADS):
        fail("native capture must contain the promoted Windows heads in canonical order")
    head_rows: dict[str, dict[str, Any]] = {}
    expected_capture_paths = {
        NATIVE_CAPTURE_FILE_NAME,
        CANDIDATE_CONTENT_INVENTORY_PATH,
        CANDIDATE_EXPORT_PATH,
    }
    windows_only = "publicationScope" in candidate_provenance
    if windows_only:
        expected_capture_paths.update(
            binding["path"]
            for binding in candidate_provenance["scopeBindings"].values()
        )
        expected_capture_paths.update(
            binding["path"]
            for binding in candidate_provenance.get(
                "registryPrepareFiles", []
            )
        )
        expected_capture_paths.add(NATIVE_AUTHENTICODE_RELATIVE_PATH)
    candidate_supply_chain = candidate_provenance["supplyChain"]
    for binding in (
        *candidate_supply_chain["sboms"],
        *candidate_supply_chain["scans"],
        candidate_supply_chain["gate"],
    ):
        expected_capture_paths.add(binding["path"])
    screenshot_digests: set[str] = set()
    authenticode_binding: dict[str, Any] | None = None
    for head, raw_head in zip(PROMOTED_WINDOWS_HEADS, raw_heads, strict=True):
        expected_head_keys = {
            "headId",
            "rid",
            "installer",
            "payload",
            "receipt",
            "progressLog",
            "screenshots",
        }
        if windows_only:
            expected_head_keys.add("authenticodeVerification")
        if not isinstance(raw_head, dict) or set(raw_head) != expected_head_keys:
            fail(f"native capture head binding is malformed for {head}")
        if normalize(raw_head.get("rid")).lower() != "win-x64":
            fail(f"native capture RID differs for {head}")
        artifact = tuples[(head, "windows", "win-x64")]
        installer = raw_head.get("installer")
        payload = raw_head.get("payload")
        for label, binding in (("installer", installer), ("payload", payload)):
            if not isinstance(binding, dict) or set(binding) != {
                "relativePath",
                "fileName",
                "sha256",
                "sizeBytes",
            }:
                fail(f"native capture {head} {label} binding is malformed")
            relative = normalize(binding.get("relativePath"))
            file_name = normalize(binding.get("fileName"))
            if (
                not relative.startswith("files/")
                or PurePosixPath(relative).name != file_name
                or Path(file_name).name != file_name
            ):
                fail(f"native capture {head} {label} path is malformed")
            digest = require_sha256(normalize(binding.get("sha256")), f"{head} {label} sha256")
            size = binding.get("sizeBytes")
            if isinstance(size, bool) or not isinstance(size, int) or size < 1:
                fail(f"native capture {head} {label} size is invalid")
            staged_file = stage_dir / "files" / file_name
            if (
                staged_file.is_symlink()
                or not staged_file.is_file()
                or sha256_file(staged_file) != digest
                or staged_file.stat().st_size != size
            ):
                fail(f"native capture {head} {label} differs from staged bytes")
        if (
            normalize(installer.get("fileName")) != artifact_file_name(artifact)
            or normalize(installer.get("sha256")) != artifact_sha256(artifact)
            or installer.get("sizeBytes") != artifact.get("sizeBytes")
            or normalize(payload.get("fileName")) != normalize(artifact.get("payloadFileName"))
            or normalize(payload.get("sha256")) != normalize(artifact.get("payloadSha256"))
            or payload.get("sizeBytes") != artifact.get("payloadSizeBytes")
        ):
            fail(f"native capture {head} installer/payload differs from canonical manifest")
        if windows_only:
            raw_authenticode = raw_head.get("authenticodeVerification")
            if (
                not isinstance(raw_authenticode, dict)
                or capture.get("authenticodeVerification") != raw_authenticode
            ):
                fail("native capture Authenticode receipt binding differs across authorities")
            require_capture_inventory_file(
                native_root,
                inventory,
                NATIVE_AUTHENTICODE_RELATIVE_PATH,
                "independent Authenticode verification receipt",
            )
            scope_installer = {
                "artifactRole": "installer",
                "fileName": installer["fileName"],
                "sha256": installer["sha256"],
                "sizeBytes": installer["sizeBytes"],
            }
            try:
                authenticode_sha = PUBLICATION_SCOPE.validate_native_authenticode(
                    {
                        "authenticodeVerification": raw_authenticode,
                        "captureSource": capture_source,
                    },
                    native_root,
                    [scope_installer],
                    expected_relative_path=NATIVE_AUTHENTICODE_RELATIVE_PATH,
                )
            except PUBLICATION_SCOPE.ScopeError as exc:
                fail(f"native Authenticode verification is invalid: {exc}")
            if authenticode_sha != raw_authenticode.get("sha256"):
                fail("native Authenticode verification digest binding differs")
            authenticode_binding = {
                **raw_authenticode,
                "path": PUBLICATION_SCOPE.AUTHENTICODE_VERIFICATION_RELATIVE_PATH,
            }
        receipt = raw_head.get("receipt")
        progress = raw_head.get("progressLog")
        expected_receipt = f"startup-smoke/startup-smoke-{head}-win-x64.receipt.json"
        expected_progress = f"startup-smoke/windows-installer-progress-{head}-win-x64.log"
        for label, binding, expected_path in (
            ("startup receipt", receipt, expected_receipt),
            ("progress log", progress, expected_progress),
        ):
            if (
                not isinstance(binding, dict)
                or set(binding) != {"path", "sha256"}
                or binding.get("path") != expected_path
            ):
                fail(f"native capture {head} {label} binding is malformed")
            path = require_capture_inventory_file(
                native_root, inventory, expected_path, f"{head} {label}"
            )
            if binding.get("sha256") != sha256_file(path):
                fail(f"native capture {head} {label} digest differs")
            expected_capture_paths.add(expected_path)
        receipt_payload = read_json(native_root / expected_receipt)
        if (
            normalize(receipt_payload.get("status")).lower() not in {"pass", "passed", "ready"}
            or normalize(receipt_payload.get("headId")).lower() != head
            or normalize(receipt_payload.get("rid")).lower() != "win-x64"
            or normalize(receipt_payload.get("platform")).lower() != "windows"
            or normalize(receipt_payload.get("releaseVersion") or receipt_payload.get("version"))
            != version
            or normalize(receipt_payload.get("channelId") or receipt_payload.get("channel")).lower()
            != channel
            or normalize(receipt_payload.get("artifactFileName"))
            != normalize(installer.get("fileName"))
            or receipt_digest(receipt_payload.get("artifactDigest"))
            != normalize(installer.get("sha256"))
            or normalize(receipt_payload.get("bootstrapPayloadAcquisitionMode")).lower()
            != "download"
            or normalize(receipt_payload.get("bootstrapPayloadFileName"))
            != normalize(payload.get("fileName"))
            or receipt_digest(receipt_payload.get("bootstrapPayloadSha256"))
            != normalize(payload.get("sha256"))
            or receipt_payload.get("bootstrapPayloadSizeBytes") != payload.get("sizeBytes")
            or normalize(receipt_payload.get("readyCheckpoint")).lower()
            != "pre_ui_event_loop"
            or normalize(receipt_payload.get("executionEnvironment")).lower() != "native_windows"
        ):
            fail(f"native capture {head} startup receipt is not exact native Windows evidence")
        host = receipt_payload.get("nativeHostEvidence")
        if (
            not isinstance(host, dict)
            or host.get("contractName") != NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME
            or normalize(host.get("status")).lower() != "verified"
            or host.get("isNativeWindows") is not True
            or normalize(host.get("hostPlatform")).lower() != "windows"
            or any(not normalize(host.get(field)) for field in ("hostKernel", "runner", "evidenceSource"))
            or "wine" in normalize(host.get("runner")).lower()
        ):
            fail(f"native capture {head} host evidence is not exact native Windows")
        progress_text = (native_root / expected_progress).read_text(
            encoding="utf-8-sig", errors="replace"
        )
        for marker in (
            "Bootstrap temp root:",
            "Payload download target:",
            "Downloading application files",
            "Verifying payload size",
            "Verifying payload checksum",
            "Extracting application files",
            "Install complete",
        ):
            if marker not in progress_text:
                fail(f"native capture {head} progress log is missing {marker!r}")
        screenshots = raw_head.get("screenshots")
        if not isinstance(screenshots, list) or len(screenshots) != 2:
            fail(f"native capture {head} must bind two screenshots")
        for role, screenshot in zip(("progress", "completion"), screenshots, strict=True):
            expected_path = f"screenshots/windows-installer-{head}-win-x64-{role}.png"
            if (
                not isinstance(screenshot, dict)
                or set(screenshot) != {"role", "path", "sha256", "width", "height"}
                or normalize(screenshot.get("role")).lower() != role
                or screenshot.get("path") != expected_path
            ):
                fail(f"native capture {head} {role} screenshot binding is malformed")
            path = require_capture_inventory_file(
                native_root, inventory, expected_path, f"{head} {role} screenshot"
            )
            digest = require_sha256(
                normalize(screenshot.get("sha256")), f"{head} {role} screenshot sha256"
            )
            dimensions = validate_png_file(path, f"{head} {role} screenshot")
            if (
                digest != sha256_file(path)
                or screenshot.get("width") != dimensions[0]
                or screenshot.get("height") != dimensions[1]
                or digest in screenshot_digests
            ):
                fail(f"native capture {head} {role} screenshot bytes/dimensions are not distinct")
            screenshot_digests.add(digest)
            expected_capture_paths.add(expected_path)
        head_rows[head] = raw_head
    if set(inventory) != expected_capture_paths:
        fail("native capture inventory contains missing or unexpected files")

    finalization_path = evidence_relative_file(
        native_root, NATIVE_FINALIZATION_FILE_NAME, "native evidence finalization"
    )
    finalization = read_json(finalization_path)
    expected_finalization_keys = {
        "captureInventorySha256",
        "captureSource",
        "contractName",
        "contractVersion",
        "finalizationSource",
        "generatedAt",
        "humanReviewConfirmed",
        "proofs",
        "reviewer",
        "reviewerWasCaptureActor",
        "status",
    }
    if windows_only:
        expected_finalization_keys.update(
            {"authenticodeVerification", "scopeApproval"}
        )
    if (
        set(finalization) != expected_finalization_keys
        or finalization.get("contractName") != NATIVE_FINALIZATION_CONTRACT_NAME
        or finalization.get("contractVersion") != (2 if windows_only else CONTRACT_VERSION)
        or normalize(finalization.get("status")).lower() != "passed"
        or finalization.get("captureInventorySha256") != inventory_sha
        or finalization.get("captureSource") != capture.get("source")
        or finalization.get("reviewerWasCaptureActor") is not False
        or finalization.get("humanReviewConfirmed") is not True
    ):
        fail("native evidence finalization contract or capture binding differs")
    reviewer = normalize(finalization.get("reviewer"))
    if not GITHUB_LOGIN_RE.fullmatch(reviewer) or reviewer.casefold() == capture_source[
        "actor"
    ].casefold():
        fail("native evidence finalization reviewer is missing or self-reviewing")
    finalization_source = validate_github_workflow_source(
        finalization.get("finalizationSource"),
        label="native finalization",
        authority=authority,
        workflow=NATIVE_FINALIZATION_WORKFLOW,
        artifact_prefix="windows-native-evidence-finalized",
    )
    if (
        finalization_source["actor"].casefold() != reviewer.casefold()
        or finalization_source["repository"] != capture_source["repository"]
        or finalization_source["sha"] != capture_source["sha"]
    ):
        fail("native evidence finalization source is not the independent reviewer authority")
    proof_rows = finalization.get("proofs")
    expected_proof_names = {
        head: f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
        for head in PROMOTED_WINDOWS_HEADS
    }
    if not isinstance(proof_rows, list) or len(proof_rows) != len(
        PROMOTED_WINDOWS_HEADS
    ):
        fail("native evidence finalization must bind the promoted visual proofs")
    proof_digests: dict[str, str] = {}
    for head, proof_row in zip(PROMOTED_WINDOWS_HEADS, proof_rows, strict=True):
        if (
            not isinstance(proof_row, dict)
            or set(proof_row) != {"headId", "path", "sha256"}
            or normalize(proof_row.get("headId")).lower() != head
            or proof_row.get("path") != expected_proof_names[head]
        ):
            fail(f"native evidence finalization proof binding is malformed for {head}")
        proof_path = evidence_relative_file(native_root, expected_proof_names[head], f"{head} visual proof")
        proof_digest = require_sha256(
            normalize(proof_row.get("sha256")), f"{head} visual proof sha256"
        )
        if proof_digest != sha256_file(proof_path):
            fail(f"native evidence finalization visual proof digest differs for {head}")
        proof_digests[head] = proof_digest
    scope_approval: dict[str, Any] | None = None
    if windows_only:
        if authenticode_binding is None:
            fail("native Windows evidence lacks independent Authenticode verification")
        if finalization.get("authenticodeVerification") != capture.get(
            "authenticodeVerification"
        ):
            fail("native finalization Authenticode verification binding differs")
        binding = finalization.get("scopeApproval")
        if (
            not isinstance(binding, dict)
            or set(binding) != {
                "approver",
                "path",
                "scopeDecisionSha256",
                "sha256",
            }
            or binding.get("approver") != reviewer
            or binding.get("path") != "PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json"
            or binding.get("scopeDecisionSha256")
            != candidate_provenance["publicationScope"]["scopeDecisionSha256"]
        ):
            fail("native finalization scope approval binding is malformed")
        approval_path = evidence_relative_file(
            native_root, binding["path"], "publication scope approval"
        )
        if binding.get("sha256") != sha256_file(approval_path):
            fail("publication scope approval digest differs")
        scope_approval = read_json(approval_path)
        proposal_path = stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
        try:
            approver = PUBLICATION_SCOPE.validate_approval(
                scope_approval,
                read_json(proposal_path),
                sha256_file(proposal_path),
                authenticode_binding["sha256"],
                [capture_source["actor"], candidate_provenance["candidate"]["actor"]],
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"publication scope approval is invalid: {exc}")
        if approver.casefold() != reviewer.casefold():
            fail("publication scope approver differs from authenticated reviewer")
    expected_all_paths = expected_capture_paths | {
        NATIVE_CAPTURE_INVENTORY_FILE_NAME,
        NATIVE_FINALIZATION_FILE_NAME,
        NATIVE_FINALIZED_INVENTORY_FILE_NAME,
        *expected_proof_names.values(),
    }
    if windows_only:
        expected_all_paths.add("PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json")
    actual_all_paths = {
        path.relative_to(native_root).as_posix() for path in safe_tree_entries(native_root)
    }
    if actual_all_paths != expected_all_paths:
        fail("finalized native evidence tree contains missing or unexpected files")
    finalized_inventory_path = evidence_relative_file(
        native_root,
        NATIVE_FINALIZED_INVENTORY_FILE_NAME,
        "finalized native evidence inventory",
    )
    finalized_inventory = read_json(finalized_inventory_path)
    if (
        finalized_inventory.get("contractName")
        != NATIVE_FINALIZED_INVENTORY_CONTRACT_NAME
        or finalized_inventory.get("contractVersion") != CONTRACT_VERSION
        or finalized_inventory.get("captureInventorySha256") != inventory_sha
        or finalized_inventory.get("files")
        != inventory_tree(native_root, exclusions=(NATIVE_FINALIZED_INVENTORY_FILE_NAME,))
    ):
        fail("finalized native evidence inventory differs from the extracted artifact")
    capture_provenance = verify_github_actions_provenance(capture_source)
    finalization_provenance = verify_github_actions_provenance(
        finalization_source, archive=archive
    )
    result = {
        "capture": capture,
        "captureSource": capture_source,
        "captureInventorySha256": inventory_sha,
        "candidateProvenance": candidate_provenance,
        "heads": head_rows,
        "reviewer": reviewer,
        "finalization": finalization,
        "finalizationSource": finalization_source,
        "finalizationSha256": sha256_file(finalization_path),
        "finalizedInventorySha256": sha256_file(finalized_inventory_path),
        "proofSha256": proof_digests,
        "githubActionsProvenance": {
            "candidateProducer": candidate_provenance["githubActionsProvenance"],
            "capture": capture_provenance,
            "finalization": finalization_provenance,
        },
    }
    if windows_only:
        result["scopeApproval"] = {
            **finalization["scopeApproval"],
            "payload": scope_approval,
        }
        result["authenticodeVerification"] = authenticode_binding
    return result


def validate_finalized_native_evidence_package(
    stage_dir: Path,
    native_root: Path,
    archive: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    """Replay the original API-bound ZIP and compare its exact tree to the stage."""
    if not native_root.is_absolute() or native_root.is_symlink() or not native_root.is_dir():
        fail("staged native Windows evidence must be an absolute non-symlink directory")
    native_root = native_root.resolve(strict=True)
    staged_rows_before = inventory_tree(native_root)

    replay_root = Path(
        tempfile.mkdtemp(prefix="preview-nightly-evidence-replay-", dir=stage_dir.parent)
    )
    replay_identity = directory_identity(replay_root)
    cleanup_quarantine = replay_root.with_name(
        f".{replay_root.name}.cleanup.{secrets.token_hex(16)}"
    )
    result: dict[str, Any] | None = None
    validation_error: Exception | None = None
    extracted_root = replay_root / "evidence"
    extracted_rows_before: list[dict[str, Any]] | None = None
    archive_snapshot: Path | None = None
    archive_snapshot_sha256 = ""
    archive_snapshot_metadata: dict[str, int] | None = None
    try:
        replay_root.chmod(0o700)
        if (
            directory_identity(replay_root) != replay_identity
            or stat.S_IMODE(replay_root.stat().st_mode) != 0o700
        ):
            fail("finalized archive replay root is not the private owned directory")
        (
            archive_snapshot,
            archive_snapshot_sha256,
            archive_snapshot_metadata,
        ) = _snapshot_finalized_evidence_archive(archive, replay_root)
        extract_evidence_archive(archive_snapshot, extracted_root)
        extracted_rows_before = inventory_tree(extracted_root)
        result = _validate_finalized_native_evidence_extraction(
            stage_dir,
            extracted_root,
            archive_snapshot,
            manifest,
            tuples,
        )
        finalization_api = result.get("githubActionsProvenance", {}).get(
            "finalization", {}
        )
        if finalization_api.get("artifactSha256") != archive_snapshot_sha256:
            fail(
                "original finalized archive digest differs from authenticated GitHub provenance"
            )
        if extracted_rows_before != staged_rows_before:
            fail(
                "staged native Windows evidence differs from the original finalized archive"
            )
    except Exception as exc:
        validation_error = exc

    boundary_error: Exception | None = None
    try:
        if (
            directory_identity(replay_root) != replay_identity
            or stat.S_IMODE(replay_root.stat().st_mode) != 0o700
        ):
            fail("finalized archive replay root identity or privacy changed")
        if archive_snapshot is not None and archive_snapshot_metadata is not None:
            _verify_owned_archive_snapshot(
                archive_snapshot,
                expected_metadata=archive_snapshot_metadata,
                expected_sha256=archive_snapshot_sha256,
            )
        staged_rows_after = inventory_tree(native_root)
        extracted_rows_after = (
            inventory_tree(extracted_root) if extracted_root.is_dir() else None
        )
        if (
            staged_rows_after != staged_rows_before
            or (
                extracted_rows_before is not None
                and extracted_rows_after != extracted_rows_before
            )
            or (
                extracted_rows_after is not None
                and staged_rows_after != extracted_rows_after
            )
        ):
            fail(
                "finalized archive, replay extraction, or staged native evidence changed during validation"
            )
    except Exception as exc:
        boundary_error = exc
    if boundary_error is not None:
        if validation_error is None:
            validation_error = boundary_error
        else:
            validation_error = ContractError(
                "finalized archive replay and boundary recheck both failed: "
                f"validation={validation_error}; boundary={boundary_error}"
            )

    try:
        consume_owned_directory(
            replay_root,
            cleanup_quarantine,
            expected_device=replay_identity["device"],
            expected_inode=replay_identity["inode"],
        )
    except Exception as cleanup_error:
        if validation_error is not None:
            fail(
                "finalized archive replay failed and identity-safe cleanup also failed: "
                f"validation={validation_error}; cleanup={cleanup_error}"
            )
        raise
    if validation_error is not None:
        raise validation_error
    if result is None:  # pragma: no cover - defensive completeness
        fail("finalized archive replay produced no validation result")
    result["archiveSha256"] = archive_snapshot_sha256
    return result


def validate_windows_visual_proof(
    visual_proof: dict[str, Any],
    *,
    stage_dir: Path,
    path_base: Path,
    containment_root: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    expected_head: str,
    expected_reviewer: str,
    expected_capture_source: dict[str, str],
    expected_finalization_source: dict[str, str],
    expected_inventory_sha256: str,
    expected_head_row: dict[str, Any],
    expected_authenticode_binding: dict[str, Any] | None,
) -> tuple[str, dict[str, str]]:
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    expected_visual_keys = {
        "artifactDigest",
        "artifactFileName",
        "captureBinding",
        "channelId",
        "checks",
        "clippingReview",
        "contractName",
        "contractVersion",
        "contrastReview",
        "finalizationBinding",
        "head",
        "headId",
        "platform",
        "readabilityReview",
        "review",
        "rid",
        "screenshots",
        "status",
        "version",
    }
    if expected_authenticode_binding is not None:
        expected_visual_keys.update(
            {
                "authenticodeVerification",
                "channel",
                "generatedAt",
                "releaseVersion",
            }
        )
    if set(visual_proof) != expected_visual_keys:
        fail("native Windows installer visual proof has missing or extra fields")
    if normalize(visual_proof.get("contractName")) != WINDOWS_VISUAL_PROOF_CONTRACT_NAME:
        fail("native Windows installer visual proof has the wrong contract")
    if visual_proof.get("contractVersion") != CONTRACT_VERSION:
        fail("native Windows installer visual proof has the wrong contract version")
    if normalize(visual_proof.get("status")).lower() not in {"pass", "passed"}:
        fail("native Windows installer visual proof is not passing")
    if normalize(visual_proof.get("version")) != version:
        fail("native Windows installer visual proof version does not match the candidate")
    if normalize(visual_proof.get("channelId")).lower() != channel:
        fail("native Windows installer visual proof channel does not match the candidate")
    if expected_authenticode_binding is not None and (
        normalize(visual_proof.get("releaseVersion")) != version
        or normalize(visual_proof.get("channel")).lower() != channel
        or not isinstance(visual_proof.get("generatedAt"), str)
        or not visual_proof["generatedAt"].endswith("Z")
    ):
        fail("native Windows installer visual proof has inconsistent v1 release aliases")
    if normalize(visual_proof.get("platform")).lower() != "windows":
        fail("native Windows installer visual proof platform must be windows")
    visual_head = normalize(visual_proof.get("headId")).lower()
    visual_rid = normalize(visual_proof.get("rid")).lower()
    if (
        (visual_head, visual_rid) != (expected_head, "win-x64")
        or normalize(visual_proof.get("head")).lower() != expected_head
    ):
        fail(f"Windows installer visual proof must target {expected_head}:win-x64")
    key = (visual_head, "windows", visual_rid)
    if key not in tuples or normalize(tuples[key].get("kind")).lower() != "installer":
        fail("native Windows installer visual proof does not identify a current installer tuple")
    if receipt_digest(visual_proof.get("artifactDigest")) != artifact_sha256(tuples[key]):
        fail("native Windows installer visual proof is not bound to the staged installer bytes")
    if normalize(visual_proof.get("artifactFileName")) != artifact_file_name(tuples[key]):
        fail("native Windows installer visual proof filename differs from the staged installer")
    reviewers: set[str] = set()
    for review_name in ("readabilityReview", "contrastReview", "clippingReview"):
        review = visual_proof.get(review_name)
        if (
            not isinstance(review, dict)
            or set(review) != {"reviewer", "status"}
            or normalize(review.get("status")).lower() not in {
            "pass",
            "passed",
            "ready",
            }
        ):
            fail(f"native Windows installer visual proof {review_name} is not passing")
        reviewer = normalize(review.get("reviewer"))
        if not reviewer:
            fail(f"native Windows installer visual proof {review_name} has no accountable reviewer")
        reviewers.add(reviewer)
    if len(reviewers) != 1:
        fail("native Windows installer visual proof must use one accountable reviewer")
    reviewer = next(iter(reviewers))
    if reviewer.casefold() != expected_reviewer.casefold():
        fail(f"native Windows visual reviewer differs from authenticated finalization: {reviewer}")
    checks = visual_proof.get("checks")
    if not isinstance(checks, dict) or set(checks) != {
        "capture_mode",
        "human_review_confirmed",
    }:
        fail("native Windows installer visual proof is missing checks")
    if normalize(checks.get("capture_mode")).lower() != "interactive":
        fail("native Windows installer visual proof must use interactive capture")
    if checks.get("human_review_confirmed") is not True:
        fail("native Windows installer visual proof must confirm human review")
    review = visual_proof.get("review")
    if (
        not isinstance(review, dict)
        or set(review)
        != {
            "allowlistSource",
            "authenticatedReviewer",
            "captureActor",
            "explicitConfirmations",
        }
        or normalize(review.get("authenticatedReviewer")).casefold()
        != expected_reviewer.casefold()
        or normalize(review.get("captureActor")).casefold()
        != expected_capture_source["actor"].casefold()
        or review.get("allowlistSource") != "repository variable plus protected environment"
        or review.get("explicitConfirmations")
        != {"readability": "passed", "contrast": "passed", "clipping": "passed"}
    ):
        fail("native Windows installer visual proof review provenance differs")
    expected_capture_binding = {
        **(
            {
                key: value
                for key, value in expected_capture_source.items()
                if key != "actor"
            }
            if expected_authenticode_binding is not None
            else expected_capture_source
        ),
        "inventorySha256": expected_inventory_sha256,
    }
    if visual_proof.get("captureBinding") != expected_capture_binding:
        fail("native Windows installer visual proof capture provenance differs")
    if visual_proof.get("finalizationBinding") != expected_finalization_source:
        fail("native Windows installer visual proof finalization provenance differs")
    if expected_authenticode_binding is None:
        if "authenticodeVerification" in visual_proof:
            fail("native Windows installer visual proof unexpectedly claims Authenticode evidence")
    elif visual_proof.get("authenticodeVerification") != expected_authenticode_binding:
        fail("native Windows installer visual proof Authenticode binding differs")
    screenshots = visual_proof.get("screenshots")
    if not isinstance(screenshots, list) or len(screenshots) != 2:
        fail("native Windows installer visual proof must contain exactly two screenshots")
    normalized_paths: dict[str, str] = {}
    screenshot_digests: set[str] = set()
    containment = containment_root.resolve(strict=True)
    expected_screenshots = expected_head_row.get("screenshots")
    if not isinstance(expected_screenshots, list) or len(expected_screenshots) != 2:
        fail("native capture head has no exact screenshot bindings")
    expected_by_role = {
        normalize(row.get("role")).lower(): row
        for row in expected_screenshots
        if isinstance(row, dict)
    }
    for screenshot in screenshots:
        if not isinstance(screenshot, dict) or set(screenshot) != {
            "path",
            "role",
            "sha256",
        }:
            fail("native Windows visual proof contains a non-object screenshot row")
        role = normalize(screenshot.get("role")).lower()
        if role not in {"progress", "completion"} or role in normalized_paths:
            fail("native Windows visual proof must use unique progress and completion roles")
        raw_path = Path(normalize(screenshot.get("path")))
        if raw_path.is_absolute():
            fail("native Windows visual proof screenshot paths must be evidence-root-relative")
        screenshot_path = path_base / raw_path
        screenshot_path = require_local_regular_file(str(screenshot_path), f"native screenshot {role}")
        try:
            relative = screenshot_path.relative_to(containment)
        except ValueError:
            fail(f"native screenshot must be contained by the exact evidence tree: {screenshot_path}")
        expected = require_sha256(normalize(screenshot.get("sha256")), f"native screenshot {role} sha256")
        if sha256_file(screenshot_path) != expected:
            fail(f"native screenshot digest mismatch for {role}")
        if expected in screenshot_digests:
            fail("progress and completion screenshots must be distinct bytes")
        expected_capture = expected_by_role.get(role)
        if (
            not isinstance(expected_capture, dict)
            or relative.as_posix() != expected_capture.get("path")
            or expected != expected_capture.get("sha256")
            or validate_png_file(screenshot_path, f"native screenshot {role}")
            != (expected_capture.get("width"), expected_capture.get("height"))
        ):
            fail(f"native screenshot {role} differs from the authenticated capture")
        screenshot_digests.add(expected)
        normalized_paths[role] = screenshot_path.relative_to(stage_dir.resolve(strict=True)).as_posix()
    if set(normalized_paths) != {"progress", "completion"}:
        fail("native Windows visual proof must include progress and completion roles")
    return reviewer, normalized_paths


def stage_native_evidence(stage_dir: Path, archive: Path) -> dict[str, Any]:
    manifest, tuples = require_current_artifacts(stage_dir)
    target = stage_dir / "proof" / "windows-native"
    archive_target = stage_dir / "proof" / "windows-native-finalized.zip"
    archive = require_local_regular_file(str(archive), "finalized native Windows evidence archive")
    if target.exists() or archive_target.exists():
        fail("native Windows evidence destination already exists")
    replaced_paths = [
        stage_dir / "startup-smoke" / f"startup-smoke-{head}-win-x64.receipt.json"
        for head in PROMOTED_WINDOWS_HEADS
    ] + [
        stage_dir / "startup-smoke" / f"windows-installer-progress-{head}-win-x64.log"
        for head in PROMOTED_WINDOWS_HEADS
    ]
    backups = {path: path.read_bytes() for path in replaced_paths if path.is_file()}
    created_outputs = [
        stage_dir / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
        for head in PROMOTED_WINDOWS_HEADS
    ] + [
        stage_dir / NATIVE_FINALIZATION_FILE_NAME,
        stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json",
        stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json",
    ]
    for path in created_outputs:
        if path.exists() or path.is_symlink():
            fail(f"native Windows evidence output already exists: {path.name}")
    try:
        shutil.copy2(archive, archive_target)
        extract_evidence_archive(archive_target, target)
        rows = inventory_tree(target)
        package = validate_finalized_native_evidence_package(
            stage_dir,
            target,
            archive_target,
            manifest,
            tuples,
        )
        windows_only = "scopeApproval" in package
        if windows_only:
            source_finalization_path = target / NATIVE_FINALIZATION_FILE_NAME
            root_finalization_path = stage_dir / NATIVE_FINALIZATION_FILE_NAME
            shutil.copy2(source_finalization_path, root_finalization_path)
            if root_finalization_path.read_bytes() != source_finalization_path.read_bytes():
                fail("root native finalization is not byte-identical to producer v2")
        reviewers: dict[str, str] = {}
        visual_proof_digests: dict[str, str] = {}
        copied_receipts: dict[str, str] = {}
        copied_progress_logs: dict[str, str] = {}
        for head in PROMOTED_WINDOWS_HEADS:
            source_visual_path = (
                target / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
            )
            visual_proof = read_json(source_visual_path)
            reviewer, normalized_screenshot_paths = validate_windows_visual_proof(
                visual_proof,
                stage_dir=stage_dir,
                path_base=target,
                containment_root=target,
                manifest=manifest,
                tuples=tuples,
                expected_head=head,
                expected_reviewer=package["reviewer"],
                expected_capture_source=package["captureSource"],
                expected_finalization_source=package["finalizationSource"],
                expected_inventory_sha256=package["captureInventorySha256"],
                expected_head_row=package["heads"][head],
                expected_authenticode_binding=(
                    {
                        **package["authenticodeVerification"],
                        "path": NATIVE_AUTHENTICODE_RELATIVE_PATH,
                    }
                    if "authenticodeVerification" in package
                    else None
                ),
            )
            portable_visual_proof = json.loads(json.dumps(visual_proof))
            for screenshot in portable_visual_proof["screenshots"]:
                role = normalize(screenshot.get("role")).lower()
                screenshot["path"] = normalized_screenshot_paths[role]
            if "authenticodeVerification" in package:
                portable_visual_proof["authenticodeVerification"] = package[
                    "authenticodeVerification"
                ]
            portable_visual_path = (
                stage_dir / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
            )
            write_json(portable_visual_path, portable_visual_proof)
            reviewers[head] = reviewer
            visual_proof_digests[head] = sha256_file(portable_visual_path)
        for head in PROMOTED_WINDOWS_HEADS:
            receipt_name = f"startup-smoke-{head}-win-x64.receipt.json"
            source_receipt = target / "startup-smoke" / receipt_name
            shutil.copy2(source_receipt, stage_dir / "startup-smoke" / receipt_name)
            copied_receipts[head] = sha256_file(source_receipt)
            progress_log_name = f"windows-installer-progress-{head}-win-x64.log"
            progress_log = target / "startup-smoke" / progress_log_name
            shutil.copy2(progress_log, stage_dir / "startup-smoke" / progress_log_name)
            copied_progress_logs[head] = sha256_file(progress_log)
        shutil.copy2(
            stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json",
            stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json",
        )
        verify_current_startup_receipts(stage_dir, tuples, require_native_windows=True)
        release = {
            "channel": "preview",
            "version": normalize(manifest.get("version")),
        }
        payload = {
            "contractName": NATIVE_EVIDENCE_CONTRACT_NAME,
            "contractVersion": CONTRACT_VERSION,
            "status": "passed",
            "release": release,
            "treeSha256": inventory_sha256(rows),
            "fileCount": len(rows),
            "archivePath": archive_target.relative_to(stage_dir).as_posix(),
            "archiveSha256": package["archiveSha256"],
            "captureInventorySha256": package["captureInventorySha256"],
            "candidateProvenance": package["candidateProvenance"],
            "finalizedInventorySha256": package["finalizedInventorySha256"],
            "finalizationSha256": package["finalizationSha256"],
            "captureSource": package["captureSource"],
            "finalizationSource": package["finalizationSource"],
            "githubActionsProvenance": package["githubActionsProvenance"],
            "visualProofSha256": visual_proof_digests,
            "visualReviewers": reviewers,
            "startupReceiptSha256": copied_receipts,
            "progressLogSha256": copied_progress_logs,
        }
        if "scopeApproval" in package:
            root_finalization_path = stage_dir / NATIVE_FINALIZATION_FILE_NAME
            portable_visual_path = (
                stage_dir
                / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
            )
            payload["nativeFinalization"] = {
                "path": NATIVE_FINALIZATION_FILE_NAME,
                "sha256": sha256_file(root_finalization_path),
                "sizeBytes": root_finalization_path.stat().st_size,
            }
            payload["visualProof"] = {
                "path": portable_visual_path.name,
                "sha256": sha256_file(portable_visual_path),
                "sizeBytes": portable_visual_path.stat().st_size,
            }
            payload["scopeApproval"] = package["scopeApproval"]
        if "authenticodeVerification" in package:
            payload["authenticodeVerification"] = package[
                "authenticodeVerification"
            ]
        write_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json", payload)
        return payload
    except Exception:
        for path in created_outputs:
            if path.is_file() or path.is_symlink():
                path.unlink()
        for path, content in backups.items():
            path.write_bytes(content)
        if target.exists():
            shutil.rmtree(target)
        if archive_target.is_file() or archive_target.is_symlink():
            archive_target.unlink()
        raise


def require_passing_receipt(path: Path, label: str) -> dict[str, Any]:
    payload = read_json(path)
    if normalize(payload.get("status")).lower() not in {"pass", "passed", "ready", "complete"}:
        fail(f"{label} is not passing: {path}")
    return payload


def verify_promotion_evidence(
    path: Path,
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    manifest: dict[str, Any],
) -> dict[str, Any]:
    payload = read_json(path)
    if normalize(payload.get("contractName")) != "chummer.run.desktop_release_publication":
        fail("promotion evidence has the wrong contract")
    rows = payload.get("artifacts")
    if not isinstance(rows, list):
        fail("promotion evidence has no artifact rows")
    by_file = {
        normalize(row.get("fileName")): row
        for row in rows
        if isinstance(row, dict) and normalize(row.get("fileName"))
    }
    manifest_rows = manifest.get("artifacts")
    if not isinstance(manifest_rows, list):
        fail("canonical manifest has no artifact rows")
    expected_names = {
        artifact_file_name(row) for row in manifest_rows if isinstance(row, dict)
    }
    if set(by_file) != expected_names:
        fail("promotion evidence artifact set differs from the canonical manifest")
    for artifact in manifest_rows:
        if not isinstance(artifact, dict):
            fail("canonical manifest contains a non-object artifact row")
        file_name = artifact_file_name(artifact)
        evidence = by_file.get(file_name)
        if evidence is None:
            fail(f"promotion evidence is missing {file_name}")
        if normalize(evidence.get("promotionStatus")).lower() != "pass":
            fail(f"promotion evidence is not passing for {file_name}")
        if normalize(evidence.get("startupSmokeStatus")).lower() != "pass":
            fail(f"promotion evidence startup smoke is not passing for {file_name}")
        if receipt_digest(evidence.get("artifactSha256")) != artifact_sha256(artifact):
            fail(f"promotion evidence digest mismatch for {file_name}")
        if int(evidence.get("artifactSizeBytes") or 0) != int(artifact.get("sizeBytes") or 0):
            fail(f"promotion evidence size mismatch for {file_name}")
        if normalize(evidence.get("kind")).lower() != normalize(artifact.get("kind")).lower():
            fail(f"promotion evidence kind mismatch for {file_name}")
    return payload


def verify_native_evidence_authority_receipt(inputs: dict[str, Any]) -> dict[str, Any]:
    authority = inputs.get("nativeWindowsEvidenceAuthority")
    if not isinstance(authority, dict):
        fail("prepared inputs have no native Windows evidence authority")
    repository = normalize(authority.get("repository"))
    if not GITHUB_REPOSITORY_RE.fullmatch(repository):
        fail("prepared native Windows evidence repository is malformed")
    authorities = inputs.get("authorities")
    presentation_commit = next(
        (
            normalize(row.get("commit"))
            for row in authorities or []
            if isinstance(row, dict) and normalize(row.get("name")) == "presentation"
        ),
        "",
    )
    if authority.get("presentationCommit") != presentation_commit:
        fail("prepared native Windows evidence commit differs from presentation authority")
    workflows = authority.get("workflows")
    expected_paths = {
        "candidateExport": CANDIDATE_EXPORT_WORKFLOW,
        "capture": NATIVE_CAPTURE_WORKFLOW,
        "finalization": NATIVE_FINALIZATION_WORKFLOW,
    }
    if not isinstance(workflows, dict) or set(workflows) != set(expected_paths):
        fail("prepared native Windows evidence workflow authority set is incomplete")
    for role, expected_path in expected_paths.items():
        binding = workflows.get(role)
        if (
            not isinstance(binding, dict)
            or set(binding) != {"path", "authorityCommit", "sha256"}
            or binding.get("path") != expected_path
            or binding.get("authorityCommit") != presentation_commit
            or not SHA256_RE.fullmatch(normalize(binding.get("sha256")))
        ):
            fail(f"prepared native Windows {role} workflow authority is malformed")
    return authority


def verify_input_receipt(stage_dir: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    inputs = read_json(stage_dir / INPUT_FILE_NAME)
    candidate = read_json(stage_dir / CANDIDATE_FILE_NAME)
    if inputs.get("contractName") != INPUT_CONTRACT_NAME or inputs.get("contractVersion") != CONTRACT_VERSION:
        fail("prepared inputs have the wrong contract")
    if inputs.get("status") != "validated":
        fail("prepared inputs are not validated")
    release = inputs.get("release")
    if not isinstance(release, dict):
        fail("prepared inputs have no release identity")
    manifest = read_json(stage_dir / "RELEASE_CHANNEL.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if release != {
        "channel": channel,
        "version": version,
        "publishedAt": normalize(manifest.get("publishedAt")),
    }:
        fail("prepared release identity differs from the canonical manifest")
    if candidate.get("contractName") != CONTRACT_NAME or candidate.get("contractVersion") != CONTRACT_VERSION:
        fail("candidate has the wrong contract")
    if candidate.get("status") != "awaiting_native_windows_evidence":
        fail("candidate is not awaiting native Windows evidence")
    if candidate.get("uploadAuthorized") is not False:
        fail("candidate must explicitly deny upload authorization")
    if candidate.get("release") != release or candidate.get("authorities") != inputs.get("authorities"):
        fail("candidate identity differs from prepared inputs")
    manifest_sha = sha256_file(stage_dir / "RELEASE_CHANNEL.generated.json")
    if candidate.get("manifestSha256") != manifest_sha:
        fail("candidate manifest hash differs from current bytes")
    input_rows = inputs.get("inputs")
    if not isinstance(input_rows, dict) or set(input_rows) != {
        row[0] for row in EXACT_PROOF_INPUTS
    }:
        fail("prepared proof input set is incomplete")
    for input_name, _, _, target_name in EXACT_PROOF_INPUTS:
        row = input_rows.get(input_name)
        expected_path = f"proof/inputs/{target_name}"
        if not isinstance(row, dict) or row.get("path") != expected_path:
            fail(f"prepared proof input path differs for {input_name}")
        path = stage_dir / expected_path
        if row.get("sha256") != sha256_file(path):
            fail(f"prepared proof input digest differs for {input_name}")
    authorities = inputs.get("authorities")
    if not isinstance(authorities, list) or len(authorities) != len(AUTHORITY_ENVIRONMENTS):
        fail("prepared source authority set is incomplete")
    expected_authority_names = [row[0] for row in AUTHORITY_ENVIRONMENTS]
    if [normalize(row.get("name")) for row in authorities if isinstance(row, dict)] != expected_authority_names:
        fail("prepared source authority roles are incomplete or out of order")
    if any(
        not isinstance(row, dict)
        or set(row) != {"name", "commit"}
        or not COMMIT_RE.fullmatch(normalize(row.get("commit")))
        for row in authorities
    ):
        fail("prepared source authority receipt is malformed")
    verify_native_evidence_authority_receipt(inputs)
    return inputs, candidate


def validate_upstream_proof_envelopes(stage_dir: Path) -> None:
    target_by_name = {row[0]: row[3] for row in EXACT_PROOF_INPUTS}
    for input_name, expected_contract in UPSTREAM_PROOF_CONTRACTS.items():
        payload = read_json(stage_dir / "proof" / "inputs" / target_by_name[input_name])
        contract = normalize(payload.get("contractName") or payload.get("contract_name"))
        if contract != expected_contract:
            fail(f"{input_name} has the wrong authoritative contract")
        if normalize(payload.get("status")).lower() not in {"pass", "passed", "ready"}:
            fail(f"{input_name} is not passing")
        for blocker_field in ("reasons", "blockingFindings", "blocking_findings", "blockers"):
            blockers = payload.get(blocker_field)
            if isinstance(blockers, list) and blockers:
                fail(f"{input_name} still reports {blocker_field}")


def _require_manifest_review_gated(manifest: dict[str, Any], reason: str) -> None:
    public_trust_metrics = manifest.get("publicTrustMetrics")
    public_release_channel = (
        public_trust_metrics.get("releaseChannel")
        if isinstance(public_trust_metrics, dict)
        else None
    )
    registry_boundary = manifest.get("registryBoundaryCoverage")
    registry_release_channel = (
        registry_boundary.get("releaseChannel")
        if isinstance(registry_boundary, dict)
        else None
    )
    if (
        normalize(manifest.get("supportabilityState")).lower() != "review_required"
        or not isinstance(public_release_channel, dict)
        or normalize(public_release_channel.get("supportabilityState")).lower()
        != "review_required"
        or not isinstance(registry_release_channel, dict)
        or normalize(registry_release_channel.get("supportabilityState")).lower()
        != "review_required"
        or normalize(registry_release_channel.get("publicTrustPosture")).lower() != "blocked"
    ):
        fail(
            f"{reason} requires review_required canonical supportability and "
            "blocked registry public trust posture"
        )


def _load_registry_materializer(path: Path) -> tuple[Any, Path]:
    if path.is_symlink() or not path.is_file():
        fail("pinned Registry authority has no release materializer")
    spec = importlib.util.spec_from_file_location("preview_nightly_registry_materializer", path)
    if spec is None or spec.loader is None:
        fail("could not load the pinned Registry release materializer")
    module = importlib.util.module_from_spec(spec)
    previous_dont_write_bytecode = sys.dont_write_bytecode
    sys.dont_write_bytecode = True
    try:
        spec.loader.exec_module(module)
    except Exception as exc:
        fail(f"could not import the pinned Registry release materializer: {exc}")
    finally:
        sys.dont_write_bytecode = previous_dont_write_bytecode
    return module, path


def replay_authoritative_stage_validators(
    presentation_root: Path,
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    authorities: list[dict[str, str]],
) -> dict[str, Any]:
    """Replay exact committed validators from a private, disposable snapshot."""
    validator_bindings = revalidate_authoritative_validator_sources(
        presentation_root, authorities
    )
    with tempfile.TemporaryDirectory(
        prefix="preview-nightly-authority-snapshot-", dir=stage_dir.parent
    ) as snapshot_temp:
        snapshot_root = Path(snapshot_temp)
        validator_paths = materialize_authoritative_validator_snapshot(
            snapshot_root,
            authorities,
            validator_bindings,
        )
        if (
            revalidate_authoritative_validator_sources(presentation_root, authorities)
            != validator_bindings
        ):
            fail("authoritative validator source bindings changed while creating snapshot")
        payload = _replay_authoritative_stage_validators_from_snapshot(
            presentation_root,
            stage_dir,
            manifest,
            tuples,
            authorities,
            validator_bindings,
            validator_paths,
            snapshot_root,
        )
        if (
            revalidate_authoritative_validator_sources(presentation_root, authorities)
            != validator_bindings
        ):
            fail("authoritative validator source bindings changed after replay")
        return payload


def _replay_authoritative_stage_validators_from_snapshot(
    presentation_root: Path,
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
    authorities: list[dict[str, str]],
    validator_bindings: dict[str, dict[str, str]],
    validator_paths: dict[str, Path],
    snapshot_root: Path,
) -> dict[str, Any]:
    """Replay validators using only already-verified snapshot paths."""
    registry_root = Path(normalize(os.environ.get("CHUMMER_HUB_REGISTRY_ROOT"))).resolve(strict=True)
    registry_validator = validator_paths["registryMaterializer"]
    registry_module, registry_validator = _load_registry_materializer(registry_validator)
    snapshot_replacements = (
        (snapshot_root / "presentation", presentation_root),
        (snapshot_root / "registry", registry_root),
        (snapshot_root, presentation_root.parent),
    )
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed during Registry import")
    hub_path = stage_dir / "proof" / "inputs" / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    localization_path = (
        stage_dir / "proof" / "inputs" / "UI_LOCALIZATION_RELEASE_GATE.generated.json"
    )
    try:
        normalized_hub = registry_module.load_release_proof(hub_path)
        normalized_localization = registry_module.load_ui_localization_release_gate(localization_path)
    except (OSError, ValueError, TypeError) as exc:
        fail(f"Registry authoritative proof validation failed: {exc}")
    if not isinstance(normalized_hub, dict) or normalize(normalized_hub.get("status")).lower() not in {
        "pass",
        "passed",
        "ready",
    }:
        fail("Registry authoritative release proof validation did not pass")
    if not isinstance(normalized_localization, dict) or normalize(
        normalized_localization.get("status")
    ).lower() not in {"pass", "passed", "ready"}:
        fail("Registry authoritative localization validation did not pass")
    validate_upstream_proof_envelopes(stage_dir)
    if manifest.get("releaseProof") != normalized_hub:
        fail("canonical manifest releaseProof differs from the staged Registry-validated proof")
    if normalized_hub.get("uiLocalizationReleaseGate") != normalized_localization:
        fail("staged Hub proof localization gate differs from the staged Registry-validated gate")
    previous_dont_write_bytecode = sys.dont_write_bytecode
    sys.dont_write_bytecode = True
    try:
        expected_public_trust = registry_module.expected_public_trust_metrics(manifest)
        expected_registry_boundary = registry_module.expected_registry_boundary_coverage(manifest)
    except (KeyError, TypeError, ValueError, RuntimeError) as exc:
        fail(f"Registry authoritative public-trust projection failed: {exc}")
    finally:
        sys.dont_write_bytecode = previous_dont_write_bytecode
    if manifest.get("publicTrustMetrics") != expected_public_trust:
        fail("canonical publicTrustMetrics differ from the pinned Registry projection")
    if manifest.get("registryBoundaryCoverage") != expected_registry_boundary:
        fail("canonical registryBoundaryCoverage differs from the pinned Registry projection")
    freshness = expected_public_trust.get("proofFreshness")
    if not isinstance(freshness, dict) or normalize(freshness.get("status")).lower() not in {
        "fresh",
        "missing",
        "stale",
    }:
        fail("pinned Registry projection returned invalid proof freshness")
    if normalize(freshness.get("status")).lower() != "fresh":
        _require_manifest_review_gated(manifest, "non-fresh authoritative proof input")
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed during Registry replay")

    windows_validator = validator_paths["windowsDesktopExitGate"]
    if windows_validator.is_symlink() or not windows_validator.is_file():
        fail("pinned Presentation authority has no Windows desktop exit-gate materializer")
    reviewer_ids = [load_authenticated_native_reviewer(stage_dir)]
    proof_paths = {
        path_env: str(stage_dir / "proof" / "inputs" / target_name)
        for _, path_env, _, target_name in EXACT_PROOF_INPUTS
    }
    gate_paths: dict[str, Path] = {}
    with tempfile.TemporaryDirectory(prefix="preview-nightly-validator-", dir=stage_dir.parent) as temp:
        temp_root = Path(temp)
        for head in PROMOTED_WINDOWS_HEADS:
            gate_path = temp_root / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
            artifact = tuples[(head, "windows", "win-x64")]
            env = dict(os.environ)
            env.update(proof_paths)
            env.update(
                {
                    "CHUMMER_UI_REPO_ROOT_ALIAS": str(presentation_root),
                    "CHUMMER_HUB_REGISTRY_ROOT": str(registry_root),
                    "CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH": str(
                        stage_dir / "RELEASE_CHANNEL.generated.json"
                    ),
                    "CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT": str(stage_dir / "files"),
                    "CHUMMER_WINDOWS_INSTALLER_PATH": str(
                        stage_dir / "files" / artifact_file_name(artifact)
                    ),
                    "CHUMMER_WINDOWS_VISUAL_AUTHORIZED_REVIEWER_IDS": ",".join(reviewer_ids),
                    "CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH": str(
                        stage_dir
                        / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
                    ),
                    "CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH": str(gate_path),
                    "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY": head,
                    "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID": "win-x64",
                    "CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH": str(
                        stage_dir
                        / "startup-smoke"
                        / f"startup-smoke-{head}-win-x64.receipt.json"
                    ),
                    "CHUMMER_WINDOWS_STARTUP_SMOKE_PROGRESS_LOG_PATH": str(
                        stage_dir
                        / "startup-smoke"
                        / f"windows-installer-progress-{head}-win-x64.log"
                    ),
                }
            )
            if (
                revalidate_authoritative_validator_sources(presentation_root, authorities)
                != validator_bindings
            ):
                fail(f"authoritative validator source bindings changed before replaying {head}")
            completed = subprocess.run(
                ["bash", str(windows_validator)],
                cwd=presentation_root,
                env=env,
                check=False,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            if (
                revalidate_authoritative_validator_sources(presentation_root, authorities)
                != validator_bindings
            ):
                fail(f"authoritative validator source bindings changed while replaying {head}")
            if completed.returncode != 0:
                detail = completed.stderr.strip() or completed.stdout.strip()
                fail(f"authoritative Windows exit-gate replay failed for {head}: {detail}")
            normalize_generated_snapshot_paths(gate_path, snapshot_replacements)
            gate = read_json(gate_path)
            if normalize(gate.get("status")).lower() not in {"pass", "passed", "ready"}:
                fail(f"authoritative Windows exit-gate replay did not pass for {head}")
            gate_paths[head] = gate_path

        replay_root = temp_root / "replay"
        replay_root.mkdir()
        for head, source in gate_paths.items():
            shutil.copy2(
                source,
                replay_root / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json",
            )
        shutil.copy2(
            gate_paths["avalonia"], replay_root / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
        )
        verify_windows_exit_gates(replay_root, manifest, tuples)
        for head, source in gate_paths.items():
            shutil.copy2(
                source,
                stage_dir / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json",
            )
        shutil.copy2(
            gate_paths["avalonia"], stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
        )

    windows_release_validator = validator_paths["windowsReleaseEvidence"]
    handoff_materializer = validator_paths["releaseCandidateHandoff"]
    visual_handoff_materializer = validator_paths["windowsVisualProofHandoff"]
    for path, label in (
        (windows_release_validator, "Windows release-evidence verifier"),
        (handoff_materializer, "release-candidate handoff materializer"),
        (visual_handoff_materializer, "Windows visual-proof handoff materializer"),
    ):
        if path.is_symlink() or not path.is_file():
            fail(f"pinned Presentation authority has no {label}")
    release_evidence_command = [
        sys.executable,
        str(windows_release_validator),
        "--release-channel",
        str(stage_dir / "RELEASE_CHANNEL.generated.json"),
        "--downloads-manifest",
        str(stage_dir / "releases.json"),
        "--files-dir",
        str(stage_dir / "files"),
        "--signing-dir",
        str(stage_dir / "signing"),
        "--startup-smoke-dir",
        str(stage_dir / "startup-smoke"),
    ]
    for head in PROMOTED_WINDOWS_HEADS:
        release_evidence_command.extend(
            [
                "--windows-exit-gate",
                str(
                    stage_dir
                    / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
                ),
            ]
        )
    release_evidence_command.extend(
        [
            "--require-native-windows",
            "--output",
            str(stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json"),
        ]
    )
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed before Windows evidence replay")
    completed = subprocess.run(
        release_evidence_command,
        cwd=presentation_root,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed during Windows evidence replay")
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip()
        fail(f"authoritative Windows release-evidence replay failed: {detail}")
    normalize_generated_snapshot_paths(
        stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json", snapshot_replacements
    )
    gate_hashes_before_handoff = {
        path.name: sha256_file(path)
        for path in (
            stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
            *(
                stage_dir
                / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
                for head in PROMOTED_WINDOWS_HEADS
            ),
        )
    }
    handoff_env = dict(os.environ)
    handoff_env["CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH"] = str(
        snapshot_root / "presentation" / "scripts" / ".use-authoritatively-replayed-exit-gate"
    )
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed before handoff replay")
    completed = subprocess.run(
        [sys.executable, str(handoff_materializer), str(stage_dir)],
        cwd=presentation_root,
        env=handoff_env,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if (
        revalidate_authoritative_validator_sources(presentation_root, authorities)
        != validator_bindings
    ):
        fail("authoritative validator source bindings changed during handoff replay")
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip()
        fail(f"authoritative release-candidate handoff replay failed: {detail}")
    for generated_path in (
        stage_dir / "RELEASE_BUILD_HANDOFF.generated.json",
        stage_dir / "RELEASE_BUILD_HANDOFF.generated.md",
        stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
        stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md",
    ):
        normalize_generated_snapshot_paths(generated_path, snapshot_replacements)
    for path_name, digest in gate_hashes_before_handoff.items():
        if sha256_file(stage_dir / path_name) != digest:
            fail("release-candidate handoff replay changed an authoritative Windows exit gate")

    input_hashes = {
        input_name: sha256_file(stage_dir / "proof" / "inputs" / target_name)
        for input_name, _, _, target_name in EXACT_PROOF_INPUTS
    }
    payload = {
        "contractName": "chummer6-ui.preview-nightly-authoritative-validation",
        "contractVersion": CONTRACT_VERSION,
        "status": "passed",
        "release": {
            "channel": "preview",
            "version": normalize(manifest.get("version")),
            "publishedAt": normalize(manifest.get("publishedAt")),
        },
        "proofFreshness": freshness,
        "publicTrustMetricsSha256": sha256_json_object(expected_public_trust),
        "registryBoundaryCoverageSha256": sha256_json_object(expected_registry_boundary),
        "proofInputSha256": input_hashes,
        "windowsExitGateSha256": {
            head: sha256_file(
                stage_dir / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
            )
            for head in PROMOTED_WINDOWS_HEADS
        },
        "validatorSources": validator_bindings,
        "downstreamEvidenceSha256": {
            "windowsReleaseEvidence": sha256_file(
                stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json"
            ),
            "releaseBuildHandoff": sha256_file(
                stage_dir / "RELEASE_BUILD_HANDOFF.generated.json"
            ),
        },
    }
    write_json(stage_dir / AUTHORITATIVE_VALIDATION_FILE_NAME, payload)
    return payload


def verify_authoritative_validation_receipt(
    stage_dir: Path,
    manifest: dict[str, Any],
    authorities: list[dict[str, str]],
) -> dict[str, Any]:
    payload = read_json(stage_dir / AUTHORITATIVE_VALIDATION_FILE_NAME)
    if (
        payload.get("contractName") != "chummer6-ui.preview-nightly-authoritative-validation"
        or payload.get("contractVersion") != CONTRACT_VERSION
        or payload.get("status") != "passed"
    ):
        fail("authoritative validation receipt has the wrong contract or status")
    if payload.get("release") != {
        "channel": "preview",
        "version": normalize(manifest.get("version")),
        "publishedAt": normalize(manifest.get("publishedAt")),
    }:
        fail("authoritative validation receipt release identity differs")
    expected_inputs = {
        input_name: sha256_file(stage_dir / "proof" / "inputs" / target_name)
        for input_name, _, _, target_name in EXACT_PROOF_INPUTS
    }
    if payload.get("proofInputSha256") != expected_inputs:
        fail("authoritative validation receipt proof-input binding differs")
    expected_gates = {
        head: sha256_file(
            stage_dir / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
        )
        for head in PROMOTED_WINDOWS_HEADS
    }
    if payload.get("windowsExitGateSha256") != expected_gates:
        fail("authoritative validation receipt exit-gate binding differs")
    public_trust_metrics = manifest.get("publicTrustMetrics")
    registry_boundary = manifest.get("registryBoundaryCoverage")
    if not isinstance(public_trust_metrics, dict) or not isinstance(registry_boundary, dict):
        fail("canonical manifest has no Registry public-trust projection")
    freshness = public_trust_metrics.get("proofFreshness")
    if not isinstance(freshness, dict):
        fail("canonical manifest has no Registry proof freshness")
    if payload.get("proofFreshness") != freshness:
        fail("authoritative validation receipt freshness binding differs")
    if payload.get("publicTrustMetricsSha256") != sha256_json_object(public_trust_metrics):
        fail("authoritative validation receipt public-trust binding differs")
    if payload.get("registryBoundaryCoverageSha256") != sha256_json_object(registry_boundary):
        fail("authoritative validation receipt Registry-boundary binding differs")
    if normalize(freshness.get("status")).lower() != "fresh":
        _require_manifest_review_gated(manifest, "non-fresh authoritative proof input")
    expected_downstream = {
        "windowsReleaseEvidence": sha256_file(
            stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json"
        ),
        "releaseBuildHandoff": sha256_file(
            stage_dir / "RELEASE_BUILD_HANDOFF.generated.json"
        ),
    }
    if payload.get("downstreamEvidenceSha256") != expected_downstream:
        fail("authoritative validation receipt downstream-evidence binding differs")
    authority_commits = {
        normalize(row.get("name")): normalize(row.get("commit"))
        for row in authorities
        if isinstance(row, dict)
    }
    sources = payload.get("validatorSources")
    expected_source_names = {row[0] for row in AUTHORITATIVE_VALIDATOR_FILES}
    if not isinstance(sources, dict) or set(sources) != expected_source_names:
        fail("authoritative validation receipt has no validator sources")
    for source_name, authority_name, _ in AUTHORITATIVE_VALIDATOR_FILES:
        source = sources.get(source_name)
        if (
            not isinstance(source, dict)
            or source.get("authorityCommit") != authority_commits.get(authority_name)
            or not SHA256_RE.fullmatch(normalize(source.get("sha256")))
        ):
            fail(f"authoritative validation source binding differs for {source_name}")
    return payload


def verify_native_windows_evidence(
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    payload = read_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json")
    expected_wrapper_keys = {
        "archivePath",
        "archiveSha256",
        "candidateProvenance",
        "captureInventorySha256",
        "captureSource",
        "contractName",
        "contractVersion",
        "fileCount",
        "finalizationSha256",
        "finalizationSource",
        "finalizedInventorySha256",
        "githubActionsProvenance",
        "progressLogSha256",
        "release",
        "startupReceiptSha256",
        "status",
        "treeSha256",
        "visualProofSha256",
        "visualReviewers",
    }
    windows_only = "scopeApproval" in payload
    if windows_only:
        expected_wrapper_keys.update(
            {
                "authenticodeVerification",
                "nativeFinalization",
                "scopeApproval",
                "visualProof",
            }
        )
    if set(payload) != expected_wrapper_keys:
        fail("native Windows evidence wrapper has missing or extra fields")
    if payload.get("contractName") != NATIVE_EVIDENCE_CONTRACT_NAME or payload.get("contractVersion") != CONTRACT_VERSION:
        fail("native Windows evidence has the wrong contract")
    if payload.get("status") != "passed":
        fail("native Windows evidence is not passing")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if payload.get("release") != {"channel": channel, "version": version}:
        fail("native Windows evidence release identity differs from the manifest")
    native_root = stage_dir / "proof" / "windows-native"
    rows = inventory_tree(native_root)
    if payload.get("treeSha256") != inventory_sha256(rows) or payload.get("fileCount") != len(rows):
        fail("native Windows evidence tree receipt differs from staged bytes")
    archive = stage_dir / "proof" / "windows-native-finalized.zip"
    if payload.get("archivePath") != "proof/windows-native-finalized.zip":
        fail("native Windows evidence archive receipt differs from staged bytes")
    package = validate_finalized_native_evidence_package(
        stage_dir,
        native_root,
        archive,
        manifest,
        tuples,
    )
    if payload.get("archiveSha256") != package["archiveSha256"]:
        fail("native Windows evidence archive receipt differs from staged bytes")
    for field, expected in (
        ("captureInventorySha256", package["captureInventorySha256"]),
        ("candidateProvenance", package["candidateProvenance"]),
        ("finalizedInventorySha256", package["finalizedInventorySha256"]),
        ("finalizationSha256", package["finalizationSha256"]),
        ("captureSource", package["captureSource"]),
        ("finalizationSource", package["finalizationSource"]),
        ("githubActionsProvenance", package["githubActionsProvenance"]),
    ):
        if payload.get(field) != expected:
            fail(f"native Windows evidence {field} binding differs")
    if "scopeApproval" in package:
        if payload.get("scopeApproval") != package["scopeApproval"]:
            fail("native Windows evidence scope approval binding differs")
        root_finalization = stage_dir / NATIVE_FINALIZATION_FILE_NAME
        nested_finalization = native_root / NATIVE_FINALIZATION_FILE_NAME
        finalization_ref = payload.get("nativeFinalization")
        if (
            not isinstance(finalization_ref, dict)
            or set(finalization_ref) != {"path", "sha256", "sizeBytes"}
            or finalization_ref.get("path") != NATIVE_FINALIZATION_FILE_NAME
            or finalization_ref.get("sha256") != package["finalizationSha256"]
            or finalization_ref.get("sizeBytes") != root_finalization.stat().st_size
            or sha256_file(root_finalization) != package["finalizationSha256"]
            or root_finalization.read_bytes() != nested_finalization.read_bytes()
        ):
            fail("native Windows evidence root finalization reference differs")
    elif "scopeApproval" in payload:
        fail("native Windows evidence unexpectedly claims scope approval")
    if "authenticodeVerification" in package:
        if payload.get("authenticodeVerification") != package[
            "authenticodeVerification"
        ]:
            fail("native Windows evidence Authenticode binding differs")
    elif "authenticodeVerification" in payload:
        fail("native Windows evidence unexpectedly claims Authenticode verification")
    expected_visual_digests: dict[str, str] = {}
    expected_reviewers: dict[str, str] = {}
    for head in PROMOTED_WINDOWS_HEADS:
        portable_path = stage_dir / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
        proof = read_json(portable_path)
        reviewer, _ = validate_windows_visual_proof(
            proof,
            stage_dir=stage_dir,
            path_base=stage_dir,
            containment_root=native_root,
            manifest=manifest,
            tuples=tuples,
            expected_head=head,
            expected_reviewer=package["reviewer"],
            expected_capture_source=package["captureSource"],
            expected_finalization_source=package["finalizationSource"],
            expected_inventory_sha256=package["captureInventorySha256"],
            expected_head_row=package["heads"][head],
            expected_authenticode_binding=package.get("authenticodeVerification"),
        )
        expected_visual_digests[head] = sha256_file(portable_path)
        expected_reviewers[head] = reviewer
    if payload.get("visualProofSha256") != expected_visual_digests:
        fail("native Windows visual proof digest map differs")
    if windows_only:
        visual_path = (
            stage_dir
            / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
        )
        visual_ref = payload.get("visualProof")
        if (
            not isinstance(visual_ref, dict)
            or set(visual_ref) != {"path", "sha256", "sizeBytes"}
            or visual_ref.get("path") != visual_path.name
            or visual_ref.get("sha256") != expected_visual_digests["avalonia"]
            or visual_ref.get("sizeBytes") != visual_path.stat().st_size
        ):
            fail("native Windows evidence visual proof reference differs")
    if payload.get("visualReviewers") != expected_reviewers:
        fail("native Windows visual reviewer map differs")
    if sha256_file(stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json") != expected_visual_digests[
        "avalonia"
    ]:
        fail("canonical Windows visual proof alias differs from the Avalonia proof")
    expected_receipts: dict[str, str] = {}
    expected_logs: dict[str, str] = {}
    for head in PROMOTED_WINDOWS_HEADS:
        receipt = stage_dir / "startup-smoke" / f"startup-smoke-{head}-win-x64.receipt.json"
        source_receipt = native_root / "startup-smoke" / receipt.name
        if sha256_file(receipt) != sha256_file(source_receipt):
            fail(f"staged native startup receipt differs from copied evidence for {head}")
        expected_receipts[head] = sha256_file(receipt)
        progress = stage_dir / "startup-smoke" / f"windows-installer-progress-{head}-win-x64.log"
        source_progress = native_root / "startup-smoke" / progress.name
        if sha256_file(progress) != sha256_file(source_progress):
            fail(f"staged native progress log differs from copied evidence for {head}")
        expected_logs[head] = sha256_file(progress)
    if payload.get("startupReceiptSha256") != expected_receipts:
        fail("native Windows startup receipt digest map differs")
    if payload.get("progressLogSha256") != expected_logs:
        fail("native Windows progress-log digest map differs")
    return payload


def verify_windows_exit_gates(
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, str]:
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    digests: dict[str, str] = {}
    for head in PROMOTED_WINDOWS_HEADS:
        path = stage_dir / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
        gate = read_json(path)
        if normalize(gate.get("contract_name") or gate.get("contractName")) != "chummer6-ui.windows_desktop_exit_gate":
            fail(f"Windows desktop exit gate has the wrong contract for {head}")
        if normalize(gate.get("status")).lower() not in {"pass", "passed"}:
            fail(f"Windows desktop exit gate is not passing for {head}")
        if normalize(gate.get("channelId")).lower() != channel or normalize(gate.get("releaseVersion")) != version:
            fail(f"Windows desktop exit gate release identity differs for {head}")
        gate_head = gate.get("head")
        if not isinstance(gate_head, dict) or (
            normalize(gate_head.get("app_key")).lower(),
            normalize(gate_head.get("platform")).lower(),
            normalize(gate_head.get("rid")).lower(),
        ) != (head, "windows", "win-x64"):
            fail(f"Windows desktop exit gate tuple differs for {head}")
        if normalize(gate.get("blockingMode")).lower() != "none" or normalize(
            gate.get("blocking_mode")
        ).lower() != "none" or gate.get("reasons") != []:
            fail(f"Windows desktop exit gate remains blocked for {head}")
        checks = gate.get("checks")
        if not isinstance(checks, dict):
            fail(f"Windows desktop exit gate has no checks for {head}")
        artifact = tuples[(head, "windows", "win-x64")]
        digest = artifact_sha256(artifact)
        for field in ("installer_sha256", "startup_smoke_artifact_digest"):
            if receipt_digest(checks.get(field)) != digest:
                fail(
                    f"Windows desktop exit gate {field} differs for {head}: "
                    f"{receipt_digest(checks.get(field))} != {digest}"
                )
        visual_digest = receipt_digest(
            checks.get("windows_installer_visual_effective_artifact_digest")
            or checks.get("windows_installer_visual_proof_artifact_digest")
        )
        if visual_digest != digest:
            fail(
                f"Windows desktop exit gate visual proof digest differs for {head}: "
                f"{visual_digest} != {digest}"
            )
        if checks.get("windows_installer_visual_proof_skipped") is True:
            fail(f"Windows desktop exit gate skipped visual proof for {head}")
        digests[head] = sha256_file(path)
    if sha256_file(stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json") != digests["avalonia"]:
        fail("canonical Windows exit-gate alias differs from the Avalonia gate")
    return digests


def verify_windows_native_smoke_summary(
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    payload = read_json(stage_dir / "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if payload.get("status") != "pass" or payload.get("errors") != []:
        fail("native Windows bootstrap smoke summary is not passing")
    if normalize(payload.get("releaseVersion")) != version or normalize(payload.get("releaseChannel")).lower() != channel:
        fail("native Windows bootstrap smoke summary release identity differs")
    if payload.get("nativeWindowsRequired") is not True:
        fail("native Windows bootstrap smoke summary did not require native Windows")
    rows = payload.get("checkedArtifacts")
    if not isinstance(rows, list):
        fail("native Windows bootstrap smoke summary has no checkedArtifacts")
    by_key = {
        (normalize(row.get("head")).lower(), normalize(row.get("rid")).lower()): row
        for row in rows
        if isinstance(row, dict)
    }
    if set(by_key) != {(head, "win-x64") for head in PROMOTED_WINDOWS_HEADS}:
        fail("native Windows bootstrap smoke summary has the wrong artifact set")
    for head in PROMOTED_WINDOWS_HEADS:
        row = by_key[(head, "win-x64")]
        if (
            normalize(row.get("fileName")) != artifact_file_name(tuples[(head, "windows", "win-x64")])
            or normalize(row.get("installerMode")).lower() != "bootstrap"
            or normalize(row.get("payloadAcquisitionMode")).lower() != "download"
            or normalize(row.get("executionEnvironment")).lower() != "native_windows"
        ):
            fail(f"native Windows bootstrap smoke summary differs for {head}")
    return payload


def verify_windows_release_summary(
    stage_dir: Path,
    manifest: dict[str, Any],
    tuples: dict[tuple[str, str, str], dict[str, Any]],
) -> dict[str, Any]:
    payload = read_json(stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if payload.get("contractName") != "chummer.windows_release_evidence.v1":
        fail("Windows release evidence has the wrong contract")
    status = normalize(payload.get("status")).lower()
    if payload.get("errors") != [] or status not in {"pass", "proof_only"}:
        fail("Windows release evidence is neither passing nor an unsigned native preview")
    if status == "pass":
        if (
            payload.get("verdict") != "WINDOWS_FLAGSHIP_READY"
            or payload.get("launchReady") is not True
            or payload.get("supportabilityFloor") != "preview_supported"
            or payload.get("caveats") != []
        ):
            fail("passing Windows release evidence is internally inconsistent")
    else:
        if (
            payload.get("verdict") != "WINDOWS_PROOF_PREVIEW_READY"
            or payload.get("launchReady") is not False
            or payload.get("supportabilityFloor") != "review_required"
        ):
            fail("unsigned Windows preview evidence is internally inconsistent")
        _require_manifest_review_gated(manifest, "unsigned Windows preview")
    if normalize(payload.get("version")) != version or normalize(payload.get("channel")).lower() != channel:
        fail("Windows release evidence release identity differs")
    if payload.get("requireNativeWindows") is not True or payload.get("allowProofOnlyVisualHandoff") is not False:
        fail("Windows release evidence used a proof waiver")
    if normalize(payload.get("proofOnlyVisualHandoffPath")):
        fail("Windows release evidence unexpectedly names a proof-only handoff")
    rows = payload.get("checkedArtifacts")
    if not isinstance(rows, list):
        fail("Windows release evidence has no checkedArtifacts")
    by_key = {
        (normalize(row.get("head")).lower(), normalize(row.get("rid")).lower()): row
        for row in rows
        if isinstance(row, dict)
    }
    if set(by_key) != {(head, "win-x64") for head in PROMOTED_WINDOWS_HEADS}:
        fail("Windows release evidence has the wrong artifact set")
    for head in PROMOTED_WINDOWS_HEADS:
        artifact = tuples[(head, "windows", "win-x64")]
        row = by_key[(head, "win-x64")]
        if (
            normalize(row.get("artifactId")) != normalize(artifact.get("artifactId"))
            or normalize(row.get("fileName")) != artifact_file_name(artifact)
            or receipt_digest(row.get("sha256")) != artifact_sha256(artifact)
            or normalize(row.get("executionEnvironment")).lower() != "native_windows"
            or row.get("proofOnlyVisualHandoff") is not False
        ):
            fail(f"Windows release evidence artifact binding differs for {head}")
    if status == "proof_only":
        expected_caveats = {
            f"{tuples[(head, 'windows', 'win-x64')]['artifactId']}: unsigned preview artifact"
            for head in PROMOTED_WINDOWS_HEADS
        }
        caveats = payload.get("caveats")
        if not isinstance(caveats, list) or set(caveats) != expected_caveats:
            fail("unsigned Windows preview evidence has a non-signing caveat")
        if any(normalize(row.get("signingStatus")).lower() != "skipped_preview" for row in rows):
            fail("unsigned Windows preview evidence signing status is inconsistent")
    return payload


def verify_release_build_handoff(stage_dir: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    payload = read_json(stage_dir / "RELEASE_BUILD_HANDOFF.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    if normalize(payload.get("contract_name") or payload.get("contractName")) != "chummer.release_build_handoff":
        fail("release build handoff has the wrong contract")
    if normalize(payload.get("version")) != version or normalize(payload.get("channel")).lower() != channel:
        fail("release build handoff release identity differs")
    for field in ("handoff_only", "stable_release_unchanged", "requires_separate_publish_lane"):
        if payload.get(field) is not True:
            fail(f"release build handoff {field} must be true")
    if normalize(payload.get("handoff_scope")) != "staged_nightly":
        fail("release build handoff has the wrong scope")
    if payload.get("stage_proof_complete") is not True or payload.get("promotion_ready") is not True:
        fail("release build handoff is not stage-proof-complete")
    if payload.get("blockers") != [] or payload.get("missing_required_platforms") != [] or payload.get("missing_required_heads") != []:
        fail("release build handoff still reports blockers or missing coverage")
    manifest_rows = manifest.get("artifacts")
    rows = payload.get("artifacts")
    if not isinstance(manifest_rows, list) or not isinstance(rows, list):
        fail("release build handoff has no artifact inventory")
    expected = {
        (
            normalize(row.get("artifactId")),
            artifact_file_name(row),
            normalize(row.get("platform")).lower(),
            normalize(row.get("rid")).lower(),
            version,
        )
        for row in manifest_rows
        if isinstance(row, dict)
    }
    actual = {
        (
            normalize(row.get("artifact_id")),
            normalize(row.get("file_name")),
            normalize(row.get("platform")).lower(),
            normalize(row.get("rid")).lower(),
            normalize(row.get("version")),
        )
        for row in rows
        if isinstance(row, dict)
    }
    if actual != expected or payload.get("artifact_count") != len(expected):
        fail("release build handoff artifact inventory differs from the canonical manifest")
    return payload


def build_upload_semantic_proof(stage_dir: Path) -> dict[str, Any]:
    manifest = read_json(stage_dir / "RELEASE_CHANNEL.generated.json")
    native = read_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json")
    version, channel = require_preview_manifest_identity(manifest, "canonical manifest")
    gate_rows: list[dict[str, Any]] = []
    source_names = [
        "NATIVE_WINDOWS_EVIDENCE.generated.json",
        "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json",
        "WINDOWS_RELEASE_EVIDENCE.generated.json",
        "RELEASE_BUILD_HANDOFF.generated.json",
        SUPPLY_CHAIN.GATE_PATH,
        *(
            f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json"
            for head in PROMOTED_WINDOWS_HEADS
        ),
    ]
    if "nativeFinalization" in native:
        source_names.append(NATIVE_FINALIZATION_FILE_NAME)
    for head in PROMOTED_WINDOWS_HEADS:
        gate = read_json(stage_dir / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json")
        checks = gate.get("checks") if isinstance(gate.get("checks"), dict) else {}
        gate_rows.append(
            {
                "head": head,
                "rid": "win-x64",
                "status": gate.get("status"),
                "blockingMode": gate.get("blockingMode"),
                "installerSha256": receipt_digest(checks.get("installer_sha256")),
                "startupSmokeArtifactSha256": receipt_digest(
                    checks.get("startup_smoke_artifact_digest")
                ),
                "visualProofArtifactSha256": receipt_digest(
                    checks.get("windows_installer_visual_effective_artifact_digest")
                ),
            }
        )
    native_smoke = read_json(stage_dir / "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json")
    windows_release = read_json(stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json")
    handoff = read_json(stage_dir / "RELEASE_BUILD_HANDOFF.generated.json")
    return {
        "contractName": "chummer6-ui.preview-nightly-public-proof",
        "contractVersion": CONTRACT_VERSION,
        "status": "passed",
        "release": {"channel": channel, "version": version},
        "sourceReceiptSha256": {
            name: sha256_file(stage_dir / name) for name in source_names
        },
        "windowsExitGates": gate_rows,
        "windowsBootstrapNativeSmoke": {
            "status": native_smoke.get("status"),
            "releaseVersion": native_smoke.get("releaseVersion"),
            "releaseChannel": native_smoke.get("releaseChannel"),
            "nativeWindowsRequired": native_smoke.get("nativeWindowsRequired"),
            "checkedArtifacts": [
                {
                    field: row.get(field)
                    for field in (
                        "fileName",
                        "head",
                        "rid",
                        "installerMode",
                        "payloadAcquisitionMode",
                        "executionEnvironment",
                    )
                }
                for row in native_smoke.get("checkedArtifacts") or []
                if isinstance(row, dict)
            ],
        },
        "windowsReleaseEvidence": {
            field: windows_release.get(field)
            for field in (
                "contractName",
                "status",
                "verdict",
                "version",
                "channel",
                "launchReady",
                "supportabilityFloor",
                "requireNativeWindows",
                "allowProofOnlyVisualHandoff",
                "checkedArtifacts",
                "caveats",
                "errors",
            )
        },
        "releaseBuildHandoff": {
            field: handoff.get(field)
            for field in (
                "contract_name",
                "channel",
                "version",
                "artifact_count",
                "handoff_scope",
                "stage_proof_complete",
                "artifacts",
                "missing_required_platforms",
                "missing_required_heads",
                "blockers",
                "promotion_ready",
            )
        },
    }


def stage_upload_proof_receipts(stage_dir: Path) -> None:
    destination = stage_dir / "proof" / "nightly-stage"
    if destination.exists():
        fail("nightly-stage upload proof destination already exists")
    destination.mkdir(parents=True, mode=0o700)
    names = [
        "NATIVE_WINDOWS_EVIDENCE.generated.json",
        *(
            f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
            for head in PROMOTED_WINDOWS_HEADS
        ),
    ]
    native = read_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json")
    if "nativeFinalization" in native:
        names.append(NATIVE_FINALIZATION_FILE_NAME)
    for name in names:
        source = require_local_regular_file(str((stage_dir / name).resolve(strict=False)), name)
        shutil.copy2(source, destination / name)
    write_json(
        destination / "PREVIEW_NIGHTLY_PUBLIC_PROOF.generated.json",
        build_upload_semantic_proof(stage_dir),
    )


def verify_upload_proof_receipts(stage_dir: Path) -> dict[str, str]:
    destination = stage_dir / "proof" / "nightly-stage"
    rows: dict[str, str] = {}
    copied_names = {
        "NATIVE_WINDOWS_EVIDENCE.generated.json",
        *(
            f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
            for head in PROMOTED_WINDOWS_HEADS
        ),
    }
    native = read_json(stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json")
    if "nativeFinalization" in native:
        copied_names.add(NATIVE_FINALIZATION_FILE_NAME)
    for name in copied_names:
        path = destination / name
        source = stage_dir / name
        if source.is_symlink() or not source.is_file() or sha256_file(source) != sha256_file(path):
            fail(f"upload proof copy differs from semantic source: {name}")
        rows[name] = sha256_file(path)
    public_proof_path = destination / "PREVIEW_NIGHTLY_PUBLIC_PROOF.generated.json"
    if read_json(public_proof_path) != build_upload_semantic_proof(stage_dir):
        fail("portable nightly public proof differs from staged semantic receipts")
    rows[public_proof_path.name] = sha256_file(public_proof_path)
    expected_names = {
        "NATIVE_WINDOWS_EVIDENCE.generated.json",
        *(
            f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
            for head in PROMOTED_WINDOWS_HEADS
        ),
        "PREVIEW_NIGHTLY_PUBLIC_PROOF.generated.json",
    }
    if "nativeFinalization" in native:
        expected_names.add(NATIVE_FINALIZATION_FILE_NAME)
    actual_names = {
        path.relative_to(destination).as_posix() for path in safe_tree_entries(destination)
    }
    if set(rows) != expected_names or actual_names != expected_names:
        fail("upload proof receipt set is incomplete")
    return rows


def derive_stage_semantics(stage_dir: Path) -> dict[str, Any]:
    inputs, candidate = verify_input_receipt(stage_dir)
    manifest, tuples = require_current_artifacts(stage_dir)
    supply_chain = verify_supply_chain_gate(stage_dir, manifest, inputs["authorities"])
    authoritative_validation = verify_authoritative_validation_receipt(
        stage_dir, manifest, inputs["authorities"]
    )
    compatibility = read_json(stage_dir / "releases.json")
    verify_compatibility_manifest(manifest, compatibility, stage_dir / "files")
    verify_files_shelf_scope(manifest, stage_dir / "files")
    verify_retained_shelf_preservation(stage_dir, tuples)
    retained_manifest = read_json(stage_dir / "retained-source" / "RELEASE_CHANNEL.generated.json")
    retained_compatibility = read_json(stage_dir / "retained-source" / "releases.json")
    verify_compatibility_manifest(
        retained_manifest,
        retained_compatibility,
        stage_dir / "retained-source" / "files",
    )
    retained = inputs.get("retainedShelf")
    if not isinstance(retained, dict):
        fail("prepared inputs have no retained shelf identity")
    verify_retained_files_inventory(stage_dir, retained, retained_manifest)
    if retained.get("canonicalSha256") != sha256_file(
        stage_dir / "retained-source" / "RELEASE_CHANNEL.generated.json"
    ) or retained.get("compatibilitySha256") != sha256_file(
        stage_dir / "retained-source" / "releases.json"
    ):
        fail("retained shelf manifest hashes differ from prepared inputs")
    verify_current_startup_receipts(stage_dir, tuples, require_native_windows=True)
    native = verify_native_windows_evidence(stage_dir, manifest, tuples)
    gate_digests = verify_windows_exit_gates(stage_dir, manifest, tuples)
    native_smoke = verify_windows_native_smoke_summary(stage_dir, manifest, tuples)
    windows_release = verify_windows_release_summary(stage_dir, manifest, tuples)
    publication_scope: dict[str, Any] | None = None
    if candidate.get("publicationScopeRequired") is True:
        scope_path = stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME
        try:
            publication_scope = PUBLICATION_SCOPE.verify_scope(
                argparse.Namespace(
                    scope=scope_path,
                    proposal=stage_dir / PUBLICATION_SCOPE.PROPOSAL_FILE_NAME,
                    publication_dir=stage_dir / PUBLICATION_SCOPE.PUBLICATION_DIRECTORY,
                    evidence_root=stage_dir,
                )
            )
        except PUBLICATION_SCOPE.ScopeError as exc:
            fail(f"sealed Windows-only publication scope is invalid: {exc}")
        if candidate.get("publicationScope") != verify_pre_capture_publication_scope(
            stage_dir, manifest, tuples
        ):
            fail("sealed publication scope proposal differs from candidate")
        if publication_scope.get("nativeEvidenceSha256") != sha256_file(
            stage_dir / "NATIVE_WINDOWS_EVIDENCE.generated.json"
        ):
            fail("publication scope native evidence digest differs")
        visual_sha = sha256_file(
            stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
        )
        if publication_scope.get("visualApprovalSha256") != [visual_sha]:
            fail("publication scope visual approval digest differs")
    handoff = verify_release_build_handoff(stage_dir, manifest)
    promotion_path = stage_dir / "release-evidence" / "public-promotion.json"
    promotion = verify_promotion_evidence(promotion_path, tuples, manifest)
    upload_proofs = verify_upload_proof_receipts(stage_dir)
    run_candidate = read_json(stage_dir / RUN_UPLOAD_CANDIDATE_FILE_NAME)
    if run_candidate != build_run_upload_candidate(stage_dir):
        fail("Run upload candidate summary differs from the exact dry-run inventory")
    semantics = {
        "release": inputs["release"],
        "sourceAuthorities": inputs["authorities"],
        "retainedShelf": retained,
        "proof": {
            "canonicalManifestSha256": sha256_file(stage_dir / "RELEASE_CHANNEL.generated.json"),
            "compatibilityManifestSha256": sha256_file(stage_dir / "releases.json"),
            "candidateProducerProvenance": native["candidateProvenance"],
            "nativeWindowsEvidenceTreeSha256": native["treeSha256"],
            "windowsExitGateSha256": gate_digests,
            "windowsNativeSmokeSha256": sha256_file(stage_dir / "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json"),
            "windowsCrossEvidenceSha256": sha256_file(stage_dir / "WINDOWS_RELEASE_EVIDENCE.generated.json"),
            "releaseBuildHandoffSha256": sha256_file(stage_dir / "RELEASE_BUILD_HANDOFF.generated.json"),
            "promotionEvidenceSha256": sha256_file(promotion_path),
            "uploadProofReceiptSha256": upload_proofs,
            "authoritativeValidationSha256": sha256_file(
                stage_dir / AUTHORITATIVE_VALIDATION_FILE_NAME
            ),
            "supplyChain": supply_chain,
        },
        "uploadBoundary": {
            "producerMode": "stage_only",
            "uploadAuthorized": False,
            "credentialsRead": False,
            "requiredFirstConsumerMode": "dry_run",
            "candidateReceiptPath": RUN_UPLOAD_CANDIDATE_FILE_NAME,
            "candidateReceipt": run_candidate,
            "consumerBootstrapSha256": HOSTED_BOOTSTRAP_SHA256,
            "consumerInventoryTopLevelFiles": list(HOSTED_UPLOAD_TOP_LEVEL_FILES),
            "consumerInventoryRecursiveDirectories": list(
                HOSTED_UPLOAD_RECURSIVE_DIRECTORIES
            ),
            "postUploadHandoffContract": "chummer.release-upload-handoff/v1",
            "postUploadHandoffEmitted": False,
        },
        "checks": {
            "completeCurrentWindowsLinuxTupleSet": True,
            "completeRetainedShelfReplacementVerified": True,
            "compatibilityManifestBound": True,
            "windowsDownloadAcquisitionVerified": native_smoke.get("status") == "pass",
            "candidateProducerAuthenticated": native["candidateProvenance"][
                "githubActionsProvenance"
            ]["status"]
            == "completed",
            "nativeWindowsStartupRequired": True,
            "nativeWindowsVisualProofPerHeadRequired": True,
            "windowsReleaseEvidenceTruthfullyBound": windows_release.get("status") in {"pass", "proof_only"},
            "releaseBuildHandoffComplete": handoff.get("stage_proof_complete") is True,
            "authoritativeValidatorsReplayedAtSeal": authoritative_validation.get("status")
            == "passed",
            "manifestStageOnly": True,
            "exactRidSbomsAndFreshVulnerabilityGate": True,
        },
        "promotionEvidenceStatus": promotion.get("status", "pass"),
    }
    if publication_scope is not None:
        semantics["publicationScope"] = publication_scope
        semantics["proof"]["publicationScopeSha256"] = sha256_file(
            stage_dir / PUBLICATION_SCOPE.FINAL_FILE_NAME
        )
        semantics["checks"].update(
            {
                "authenticodeRequired": publication_scope.get("authenticodeRequired") is True,
                "approvalIndependent": publication_scope.get("approvalIndependent") is True,
                "registryFinalizeEligible": publication_scope.get(
                    "registryFinalizeEligible"
                ),
                "registryFinalizeAuthorityUnavailable": publication_scope.get(
                    "registryFinalizeEligible"
                )
                is not True,
                "uiPublicationEligibilityDenied": publication_scope.get(
                    "publicationEligible"
                )
                is False,
                "windowsOnlyPublicationDelta": all(
                    row.get("platform") == "windows"
                    for row in publication_scope.get("publicationDeltaTuples", [])
                ),
                "freshLinuxExcludedFromPublication": all(
                    row.get("platform") == "linux"
                    for row in publication_scope.get("nonPublishedEvidenceTuples", [])
                ),
                "completePostPublicationShelfVerified": True,
                "macosSoakNonBlocking": publication_scope.get("macosSoak", {}).get(
                    "required"
                )
                is False
                and publication_scope.get("macosSoak", {}).get("reason")
                in {
                    "retained_byte_identical",
                    "not_applicable_no_incumbent_tuple",
                },
            }
        )
        semantics["uploadBoundary"].update(
            {
                "consumerBootstrapCompatible": False,
                "consumerInventoryTopLevelFiles": [
                    PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME,
                    PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME,
                ],
                "consumerInventoryRecursiveDirectories": ["files"],
                "externalAuthorityConvergenceRequired": True,
                "requiredFirstConsumerMode": "converge_then_dry_run",
                "requiredUploadRoot": PUBLICATION_SCOPE.PUBLICATION_DIRECTORY,
            }
        )
    return semantics


def seal_stage(presentation_root: Path, stage_dir: Path) -> dict[str, Any]:
    inputs, candidate = verify_input_receipt(stage_dir)
    current_authorities = validate_authorities(presentation_root)
    compare_authorities_with_receipt(inputs, current_authorities)
    if inputs.get("nativeWindowsEvidenceAuthority") != native_evidence_authority(
        presentation_root, current_authorities
    ):
        fail("native Windows evidence authority changed before seal")
    if candidate.get("authorities") != current_authorities:
        fail("candidate authority receipt disagrees with current repository authorities")
    for output_name in (
        AUTHORITATIVE_VALIDATION_FILE_NAME,
        RUN_UPLOAD_CANDIDATE_FILE_NAME,
        SEAL_FILE_NAME,
    ):
        if (stage_dir / output_name).exists() or (stage_dir / output_name).is_symlink():
            fail(f"seal output already exists: {output_name}")
    manifest, tuples = require_current_artifacts(stage_dir)
    replay_authoritative_stage_validators(
        presentation_root,
        stage_dir,
        manifest,
        tuples,
        current_authorities,
    )
    stage_upload_proof_receipts(stage_dir)
    run_candidate = build_run_upload_candidate(stage_dir)
    write_json(stage_dir / RUN_UPLOAD_CANDIDATE_FILE_NAME, run_candidate)
    semantics = derive_stage_semantics(stage_dir)
    inventory = inventory_tree(stage_dir, exclusions=(SEAL_FILE_NAME,))
    payload = {
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "status": "sealed",
        **semantics,
        "stage": {
            "treeSha256": inventory_sha256(inventory),
            "fileCount": len(inventory),
            "files": inventory,
        },
    }
    write_json(stage_dir / SEAL_FILE_NAME, payload)
    return payload


def verify_seal(stage_dir: Path) -> dict[str, Any]:
    if not stage_dir.is_absolute() or stage_dir.is_symlink() or not stage_dir.is_dir():
        fail("sealed stage must be an absolute non-symlink directory")
    stage_dir = stage_dir.resolve(strict=True)
    seal = read_json(stage_dir / SEAL_FILE_NAME)
    if seal.get("contractName") != CONTRACT_NAME or seal.get("contractVersion") != CONTRACT_VERSION:
        fail("unsupported preview nightly stage seal contract")
    if seal.get("status") != "sealed":
        fail("preview nightly stage is not sealed")
    expected_semantics = derive_stage_semantics(stage_dir)
    for field, expected in expected_semantics.items():
        if seal.get(field) != expected:
            fail(f"preview nightly stage seal semantic field changed: {field}")
    stage = seal.get("stage")
    if not isinstance(stage, dict) or not isinstance(stage.get("files"), list):
        fail("preview nightly stage seal is missing its inventory")
    actual = inventory_tree(stage_dir, exclusions=(SEAL_FILE_NAME,))
    if stage.get("fileCount") != len(actual):
        fail("preview nightly stage seal fileCount changed")
    if actual != stage["files"]:
        fail("preview nightly stage bytes changed after sealing")
    if inventory_sha256(actual) != stage.get("treeSha256"):
        fail("preview nightly stage tree digest changed after sealing")
    return seal


def digest_tree(
    root: Path,
    *,
    expected_device: int | None = None,
    expected_inode: int | None = None,
) -> dict[str, Any]:
    if not root.is_absolute() or root.is_symlink() or not root.is_dir():
        fail("tree root must be an absolute non-symlink directory")
    if (expected_device is None) != (expected_inode is None):
        fail("owned tree digest requires both expected device and inode")
    expected_identity: dict[str, int] | None = None
    if expected_device is not None and expected_inode is not None:
        expected_identity = {"device": expected_device, "inode": expected_inode}
        if directory_identity(root) != expected_identity:
            fail("owned tree identity changed before digest")
    rows = inventory_tree(root)
    if expected_identity is not None and directory_identity(root) != expected_identity:
        fail("owned tree identity changed during digest")
    return {"treeSha256": inventory_sha256(rows), "fileCount": len(rows), "files": rows}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    prepare = subparsers.add_parser("prepare-inputs")
    prepare.add_argument("--presentation-root", type=Path, required=True)
    prepare.add_argument("--candidate-dir", type=Path, required=True)
    candidate = subparsers.add_parser("mark-candidate")
    candidate.add_argument("--presentation-root", type=Path, required=True)
    candidate.add_argument("--stage-dir", type=Path, required=True)
    validate = subparsers.add_parser("validate-candidate")
    validate.add_argument("--presentation-root", type=Path, required=True)
    validate.add_argument("--stage-dir", type=Path, required=True)
    native = subparsers.add_parser("stage-native-evidence")
    native.add_argument("--stage-dir", type=Path, required=True)
    native.add_argument("--evidence-archive", type=Path, required=True)
    seal = subparsers.add_parser("seal")
    seal.add_argument("--presentation-root", type=Path, required=True)
    seal.add_argument("--stage-dir", type=Path, required=True)
    verify = subparsers.add_parser("verify")
    verify.add_argument("--stage-dir", type=Path, required=True)
    digest = subparsers.add_parser("digest-tree")
    digest.add_argument("--root", type=Path, required=True)
    digest.add_argument("--expected-device", type=int)
    digest.add_argument("--expected-inode", type=int)
    identity = subparsers.add_parser("directory-identity")
    identity.add_argument("--root", type=Path, required=True)
    install = subparsers.add_parser("install-dir-no-replace")
    install.add_argument("--source", type=Path, required=True)
    install.add_argument("--destination", type=Path, required=True)
    install.add_argument("--expected-device", type=int, required=True)
    install.add_argument("--expected-inode", type=int, required=True)
    verified_install = subparsers.add_parser("install-verified-sealed-dir-no-replace")
    verified_install.add_argument("--source", type=Path, required=True)
    verified_install.add_argument("--destination", type=Path, required=True)
    verified_install.add_argument("--expected-device", type=int, required=True)
    verified_install.add_argument("--expected-inode", type=int, required=True)
    verified_install.add_argument("--expected-tree-sha256", required=True)
    consume = subparsers.add_parser("consume-owned-dir")
    consume.add_argument("--source", type=Path, required=True)
    consume.add_argument("--quarantine", type=Path, required=True)
    consume.add_argument("--expected-device", type=int, required=True)
    consume.add_argument("--expected-inode", type=int, required=True)
    return parser


def main() -> int:
    args = build_parser().parse_args()
    try:
        if args.command == "prepare-inputs":
            payload = prepare_inputs(args.presentation_root, args.candidate_dir)
        elif args.command == "mark-candidate":
            payload = mark_candidate(args.presentation_root, args.stage_dir)
        elif args.command == "validate-candidate":
            payload = validate_candidate(args.presentation_root, args.stage_dir)
        elif args.command == "stage-native-evidence":
            payload = stage_native_evidence(args.stage_dir, args.evidence_archive)
        elif args.command == "seal":
            payload = seal_stage(args.presentation_root, args.stage_dir)
        elif args.command == "verify":
            payload = verify_seal(args.stage_dir)
        elif args.command == "digest-tree":
            payload = digest_tree(
                args.root,
                expected_device=args.expected_device,
                expected_inode=args.expected_inode,
            )
        elif args.command == "directory-identity":
            payload = directory_identity(args.root)
        elif args.command == "install-dir-no-replace":
            payload = atomic_install_directory_no_replace(
                args.source,
                args.destination,
                expected_device=args.expected_device,
                expected_inode=args.expected_inode,
            )
        elif args.command == "install-verified-sealed-dir-no-replace":
            payload = install_verified_sealed_directory_no_replace(
                args.source,
                args.destination,
                expected_device=args.expected_device,
                expected_inode=args.expected_inode,
                expected_tree_sha256=args.expected_tree_sha256,
            )
        elif args.command == "consume-owned-dir":
            payload = consume_owned_directory(
                args.source,
                args.quarantine,
                expected_device=args.expected_device,
                expected_inode=args.expected_inode,
            )
        else:  # pragma: no cover
            raise AssertionError(args.command)
    except ContractError as exc:
        print(f"preview-nightly-stage contract failure: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(payload, separators=(",", ":"), sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
