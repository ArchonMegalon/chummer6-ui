#!/usr/bin/env python3
"""Safely coordinate the non-publishing preview-nightly evidence pipeline.

The command is resumable.  It prepares (when requested), launches the governed
candidate exporter, authenticates the relayed native capture, stops for an
accountable human review, dispatches protected finalization only from an exact
review input, preserves original artifact ZIPs, seals the stage, and emits a
non-publishing handoff.  It never uploads release bytes, deploys, publishes, or
advances a CURRENT pointer.
"""

from __future__ import annotations

import argparse
import fcntl
import hashlib
import importlib.util
import json
import os
import re
import selectors
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import time
import zipfile
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable
from urllib.parse import urlparse


def _load_publication_scope_module():
    module_name = "chummer6_ui_preview_publication_scope_pipeline_contract"
    existing = sys.modules.get(module_name)
    if existing is not None:
        if not isinstance(existing, type(sys)):
            raise RuntimeError("preloaded publication-scope contract is malformed")
        return existing
    path = Path(__file__).resolve().parents[1] / "preview_nightly_publication_scope.py"
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load preview publication-scope contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


PUBLICATION_SCOPE = _load_publication_scope_module()


def _load_desktop_native_lifecycle_module():
    module_name = "chummer6_ui_desktop_native_lifecycle_pipeline_contract"
    existing = sys.modules.get(module_name)
    if existing is not None:
        if not isinstance(existing, type(sys)):
            raise RuntimeError("preloaded desktop lifecycle contract is malformed")
        return existing
    path = Path(__file__).resolve().parents[1] / "desktop_native_lifecycle_evidence.py"
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load desktop lifecycle contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


DESKTOP_LIFECYCLE = _load_desktop_native_lifecycle_module()


REPOSITORY = "ArchonMegalon/chummer6-ui"
SOURCE_REF = "refs/heads/main"
CANDIDATE_WORKFLOW = ".github/workflows/preview-nightly-candidate-export.yml"
CAPTURE_WORKFLOW = ".github/workflows/windows-native-evidence-capture.yml"
FINALIZATION_WORKFLOW = ".github/workflows/windows-native-evidence-finalize.yml"
PROMOTED_WINDOWS_HEADS = ("avalonia",)
STATE_CONTRACT = "chummer6-ui.preview-nightly-pipeline-state"
PROVENANCE_CONTRACT = "chummer6-ui.preview-nightly-durable-provenance"
REVIEW_REQUEST_CONTRACT = "chummer6-ui.preview-nightly-human-review-request"
REVIEW_INPUT_CONTRACT = "chummer6-ui.preview-nightly-human-review-input"
HANDOFF_CONTRACT = "chummer6-ui.preview-nightly-immutable-publication-handoff"
JIT_CONTRACT = "chummer6-ui.preview-nightly-jit-launch"
CAPTURE_INVENTORY = "WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json"
CAPTURE_MANIFEST = "WINDOWS_NATIVE_CAPTURE.generated.json"
AUTHENTICODE_CAPTURE_FILE = (
    "authenticode/AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
)
FINALIZATION_RECEIPT = "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
CANDIDATE_INVENTORY = "PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json"
CANDIDATE_EXPORT = "PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json"
CAPTURE_DISPATCH_RECEIPT = "PREVIEW_NIGHTLY_CAPTURE_DISPATCH.generated.json"
STAGE_SEAL = "PREVIEW_NIGHTLY_STAGE_SEAL.generated.json"
NATIVE_EVIDENCE_RECEIPT = "NATIVE_WINDOWS_EVIDENCE.generated.json"
NATIVE_EVIDENCE_CONTRACT = "chummer6-ui.preview-nightly-native-windows-evidence"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
LOGIN_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$|^github-actions\[bot\]$")
MAX_ARCHIVE_BYTES = 512 * 1024 * 1024
MAX_EXPANDED_BYTES = 1024 * 1024 * 1024
MAX_MEMBERS = 512
MAX_STAGE_AUTHORITY_BYTES = 1024 * 1024
MAX_SEALED_RECEIPT_BYTES = 8 * 1024 * 1024
MAX_N_MINUS_ONE_AUTHORITY_BYTES = 64 * 1024
PORTABLE_VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
SOURCE_AUTHORITY_ENVIRONMENTS = (
    ("presentation", "CHUMMER_UI_ROOT", "CHUMMER_UI_EXPECTED_COMMIT"),
    ("core", "CHUMMER_CORE_ROOT", "CHUMMER_CORE_EXPECTED_COMMIT"),
    ("run", "CHUMMER_RUN_ROOT", "CHUMMER_RUN_EXPECTED_COMMIT"),
    ("ui-kit", "CHUMMER_UI_KIT_ROOT", "CHUMMER_UI_KIT_EXPECTED_COMMIT"),
    ("registry", "CHUMMER_HUB_REGISTRY_ROOT", "CHUMMER_HUB_REGISTRY_EXPECTED_COMMIT"),
    ("media-factory", "CHUMMER_MEDIA_FACTORY_ROOT", "CHUMMER_MEDIA_FACTORY_EXPECTED_COMMIT"),
    ("legacy", "CHUMMER_LEGACY_ROOT", "CHUMMER_LEGACY_EXPECTED_COMMIT"),
)
STAGE_AUTHORITY_PATHS = frozenset(
    {
        *(root for _, root, _ in SOURCE_AUTHORITY_ENVIRONMENTS),
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT",
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_PATH",
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_PATH",
        "CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH",
        "CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH",
        "CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH",
        "CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH",
        "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH",
        "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH",
        "CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH",
        "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH",
        "CHUMMER_UI_WORKFLOW_PARITY_PATH",
        "CHUMMER_SR4_WORKFLOW_PARITY_PATH",
        "CHUMMER_SR6_WORKFLOW_PARITY_PATH",
    }
)
STAGE_AUTHORITY_DIGESTS = frozenset(
    {
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_SHA256",
        "CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_SHA256",
        "CHUMMER_HUB_LOCAL_RELEASE_PROOF_SHA256",
        "CHUMMER_UI_LOCALIZATION_RELEASE_GATE_SHA256",
        "CHUMMER_UI_LOCAL_RELEASE_PROOF_SHA256",
        "CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_SHA256",
        "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_SHA256",
        "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_SHA256",
        "CHUMMER_UI_FLAGSHIP_RELEASE_GATE_SHA256",
        "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_SHA256",
        "CHUMMER_UI_WORKFLOW_PARITY_SHA256",
        "CHUMMER_SR4_WORKFLOW_PARITY_SHA256",
        "CHUMMER_SR6_WORKFLOW_PARITY_SHA256",
    }
)
STAGE_AUTHORITY_ENVIRONMENTS = frozenset(
    {
        *STAGE_AUTHORITY_PATHS,
        *STAGE_AUTHORITY_DIGESTS,
        *(commit for _, _, commit in SOURCE_AUTHORITY_ENVIRONMENTS),
    }
)
STAGE_CHILD_ENVIRONMENT_PASSTHROUGH = frozenset(
    {
        "ALL_PROXY",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "LANG",
        "LC_ALL",
        "NO_PROXY",
        "SSL_CERT_DIR",
        "SSL_CERT_FILE",
        "TZ",
        "all_proxy",
        "http_proxy",
        "https_proxy",
        "no_proxy",
    }
)
PREPARE_SIGNING_BACKEND = "digicert_keylocker_linux_jsign"
PREPARE_SIGNING_HOST = "https://clientauth.one.digicert.com"
PREPARE_SIGNING_JAVA_PARENT = Path(
    "/home/tibor/.local/share/ea-tools/chummer-signing/java"
)
PREPARE_SIGNING_JAVA_ROOT_NAME = "temurin-21.0.11+10"
PREPARE_SIGNING_JAVA_HOME = (
    PREPARE_SIGNING_JAVA_PARENT / PREPARE_SIGNING_JAVA_ROOT_NAME
)
PREPARE_SIGNING_JAVA_BIN = PREPARE_SIGNING_JAVA_HOME / "bin/java"
PREPARE_SIGNING_JSIGN_JAR = Path(
    "/home/tibor/.local/share/ea-tools/chummer-signing/jsign/7.5/jsign-7.5.jar"
)
PREPARE_SIGNING_APPROVED_JAVA_SHA256 = (
    "fd85538801d8ca61d3558c87a57a600e1868d8ac9e918d0860dd64281b548643"
)
PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256 = (
    "3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5"
)
PREPARE_SIGNING_APPROVED_JSIGN_SHA256 = (
    "602a51c3545a6dc4fb99bd2ea7152b26d1345916d0c93ddfbd5936cb735af91c"
)
PREPARE_SIGNING_DOTNET_PARENT = Path("/usr/lib")
PREPARE_SIGNING_DOTNET_ROOT_NAME = "dotnet"
PREPARE_SIGNING_DOTNET_ROOT = (
    PREPARE_SIGNING_DOTNET_PARENT / PREPARE_SIGNING_DOTNET_ROOT_NAME
)
PREPARE_SIGNING_DOTNET_BIN = PREPARE_SIGNING_DOTNET_ROOT / "dotnet"
PREPARE_SIGNING_APPROVED_DOTNET_SHA256 = (
    "a2e03e682b5ba32303077bc5ed95ca3dd6b57b6d55d09491b67444644e211940"
)
PREPARE_SIGNING_APPROVED_DOTNET_TREE_SHA256 = (
    "ba27f662b28bfe7b938b8c862c41e07739db8182a42481a6a0cc5b385ec5f2be"
)
PREPARE_SIGNING_PROJECT_RELATIVE = Path(
    "scripts/Chummer.KeyLockerSigner/Chummer.KeyLockerSigner.csproj"
)
PREPARE_SIGNING_SDK_PIN_NAME = "global.json"
PREPARE_SIGNING_RUNTIME_IDENTIFIER = "linux-x64"
PREPARE_SIGNING_SDK_PIN_BYTES = (
    b'{\n'
    b'  "sdk": {\n'
    b'    "version": "10.0.110",\n'
    b'    "rollForward": "disable",\n'
    b'    "allowPrerelease": false\n'
    b'  }\n'
    b'}\n'
)
PREPARE_SIGNING_APPROVED_SDK_PIN_SHA256 = (
    "878939d8aec1375674ef0508026fc15101ac15f31807d97651c6f38b99feb5dd"
)
PREPARE_SIGNING_OUTPUT_ROOT_NAME = "published"
PREPARE_SIGNING_DLL_NAME = "Chummer.KeyLockerSigner.dll"
PREPARE_SIGNING_RUNTIME_CONFIG_NAME = (
    "Chummer.KeyLockerSigner.runtimeconfig.json"
)
PREPARE_SIGNING_DEPS_NAME = "Chummer.KeyLockerSigner.deps.json"
PREPARE_SIGNING_TOOLCHAIN_ENVIRONMENTS = frozenset(
    {
        "CHUMMER_KEYLOCKER_DOTNET_BIN",
        "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256",
        "CHUMMER_KEYLOCKER_DOTNET_ROOT",
        "CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256",
        "CHUMMER_KEYLOCKER_JAVA_BIN",
        "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256",
        "CHUMMER_KEYLOCKER_JAVA_HOME",
        "CHUMMER_KEYLOCKER_JAVA_TREE_SHA256",
        "CHUMMER_KEYLOCKER_JSIGN_JAR",
        "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_DLL",
        "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256",
        "CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256",
    }
)
PREPARE_SIGNING_HANDOFF_ENVIRONMENT = "CHUMMER_WINDOWS_SIGNING_HANDOFF_FD"
PREPARE_SIGNING_HANDOFF_MAGIC = "chummer6-ui.windows-signing-handoff.v1"
PREPARE_SIGNING_SECRET_ENVIRONMENTS = (
    "SM_HOST",
    "SM_API_KEY",
    "SM_CLIENT_CERT_FILE",
    "SM_CLIENT_CERT_PASSWORD",
)
PREPARE_SIGNING_SECRET_LIMITS = {
    "SM_HOST": 2048,
    "SM_API_KEY": 4096,
    "SM_CLIENT_CERT_FILE": 4096,
    "SM_CLIENT_CERT_PASSWORD": 4096,
}
MAX_PREPARE_SIGNING_HANDOFF_BYTES = 16 * 1024
PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS = frozenset(
    {
        "CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH",
        "CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS",
        "CHUMMER_WINDOWS_SIGNING_BACKEND",
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256",
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256",
    }
)
PREPARE_SIGNING_OPTIONAL_PUBLIC_ENVIRONMENTS = frozenset(
    {
        "CHUMMER_WINDOWS_TIMESTAMP_URL",
    }
)
PREPARE_SIGNING_PUBLIC_ENVIRONMENTS = frozenset(
    {
        *PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS,
        *PREPARE_SIGNING_OPTIONAL_PUBLIC_ENVIRONMENTS,
        *PREPARE_SIGNING_TOOLCHAIN_ENVIRONMENTS,
    }
)
PREPARE_SIGNING_ACCEPTED_ENVIRONMENTS = frozenset(
    {
        *PREPARE_SIGNING_SECRET_ENVIRONMENTS,
        *PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS,
        *PREPARE_SIGNING_OPTIONAL_PUBLIC_ENVIRONMENTS,
    }
)
JIT_CHILD_ENVIRONMENT_PASSTHROUGH = frozenset(
    {
        "ALL_PROXY",
        "DOCKER_CONTEXT",
        "DOCKER_HOST",
        "GH_TOKEN",
        "GITHUB_TOKEN",
        "HOME",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "LANG",
        "LC_ALL",
        "LC_CTYPE",
        "NO_PROXY",
        "PATH",
        "SSL_CERT_DIR",
        "SSL_CERT_FILE",
        "TZ",
        "XDG_RUNTIME_DIR",
        "all_proxy",
        "http_proxy",
        "https_proxy",
        "no_proxy",
    }
)
FORBIDDEN_SHELL_ENVIRONMENTS = frozenset(
    {
        "BASHOPTS",
        "BASH_ENV",
        "CDPATH",
        "ENV",
        "GCONV_PATH",
        "GLOBIGNORE",
        "LOCPATH",
        "NLSPATH",
        "SHELLOPTS",
    }
)
FORBIDDEN_PREPARE_ENVIRONMENT_NAMES = frozenset(
    {
        "DOTNET_ADDITIONAL_DEPS",
        "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
        "DOTNET_ROOT",
        "DOTNET_STARTUP_HOOKS",
        "DOTNET_SHARED_STORE",
        "LD_LIBRARY_PATH",
        "LD_PRELOAD",
    }
)
FORBIDDEN_PREPARE_ENVIRONMENT_PREFIXES = (
    "BASH_FUNC_",
    "COMPlus_",
    "COREHOST_",
    "DOTNET_HOST_",
    "DOTNET_ROLL_FORWARD",
    "DOTNET_TRACE",
    "MSBuild",
    "NUGET_",
)
MAX_CHILD_OUTPUT_BYTES = 4 * 1024 * 1024
CHILD_TERMINATION_GRACE_SECONDS = 1.0
TRUSTED_BASH_PATH = Path("/bin/bash")
TRUSTED_TAR_PATH = Path("/usr/bin/tar")
TRUSTED_GIT_PATH = Path("/usr/bin/git")
MAX_TOOLCHAIN_TREE_ARCHIVE_BYTES = 1024 * 1024 * 1024
MAX_TOOLCHAIN_HASH_ERROR_BYTES = 64 * 1024
TOOLCHAIN_HASH_TIMEOUT_SECONDS = 120.0
SIGNER_BUILD_TIMEOUT_SECONDS = 600.0


class PipelineError(ValueError):
    pass


class ActionRequired(PipelineError):
    pass


class PrepareSigningMaterial:
    __slots__ = ("handoff", "public_environment", "runtime_root")

    def __init__(
        self,
        handoff: bytearray,
        public_environment: dict[str, str],
        runtime_root: Path | None = None,
    ) -> None:
        self.handoff = handoff
        self.public_environment = public_environment
        self.runtime_root = runtime_root

    def __repr__(self) -> str:
        return (
            "PrepareSigningMaterial("
            f"handoff_bytes={len(self.handoff)}, "
            f"public_keys={sorted(self.public_environment)})"
        )

    def clear(self) -> None:
        self.handoff[:] = b"\x00" * len(self.handoff)
        self.handoff.clear()
        if self.runtime_root is not None:
            _remove_private_signer_runtime(self.runtime_root)
            self.runtime_root = None


def now_utc() -> datetime:
    return datetime.now(UTC)


def now_iso() -> str:
    return now_utc().replace(microsecond=0).isoformat().replace("+00:00", "Z")


def canonical_bytes(payload: dict[str, Any]) -> bytes:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("utf-8")


def sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    payload: dict[str, Any] = {}
    for key, value in pairs:
        if key in payload:
            raise PipelineError(f"duplicate JSON key: {key}")
        payload[key] = value
    return payload


def reject_nonfinite(value: str) -> None:
    raise PipelineError(f"non-finite JSON number: {value}")


def parse_json_bytes(content: bytes, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(
            content.decode("utf-8-sig"),
            object_pairs_hook=reject_duplicate_pairs,
            parse_constant=reject_nonfinite,
        )
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise PipelineError(f"{label} is not exact UTF-8 JSON: {exc}") from exc
    if not isinstance(payload, dict):
        raise PipelineError(f"{label} must be a JSON object")
    return payload


def load_json(path: Path, label: str) -> dict[str, Any]:
    try:
        return parse_json_bytes(path.read_bytes(), label)
    except OSError as exc:
        raise PipelineError(f"could not read {label}: {path}") from exc


def require_sha(value: Any, label: str) -> str:
    token = str(value or "").strip().lower()
    if token.startswith("sha256:"):
        token = token[7:]
    if not SHA256_RE.fullmatch(token):
        raise PipelineError(f"{label} must be an exact lowercase SHA-256")
    return token


def require_positive_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not POSITIVE_INTEGER_RE.fullmatch(value):
        raise PipelineError(f"{label} must be an exact positive-integer string")
    if int(value) > 9_007_199_254_740_991:
        raise PipelineError(f"{label} exceeds exact API integer authority")
    return value


def require_commit(value: Any, label: str) -> str:
    token = str(value or "").strip().lower()
    if not re.fullmatch(r"[0-9a-f]{40}", token):
        raise PipelineError(f"{label} must be a lowercase 40-character commit")
    return token


def require_login(value: Any, label: str) -> str:
    token = str(value or "").strip()
    if not LOGIN_RE.fullmatch(token):
        raise PipelineError(f"{label} is not an exact GitHub login")
    return token


def require_absolute(path: Path, label: str) -> Path:
    if not path.is_absolute():
        raise PipelineError(f"{label} must be absolute")
    return path


def require_regular(path: Path, label: str) -> Path:
    require_absolute(path, label)
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise PipelineError(f"{label} is unavailable: {path}") from exc
    if not stat.S_ISREG(metadata.st_mode) or path.is_symlink():
        raise PipelineError(f"{label} must be a regular non-symlink file")
    return path


def require_trusted_bash() -> str:
    try:
        metadata = TRUSTED_BASH_PATH.lstat()
    except OSError as exc:
        raise PipelineError("trusted Bash interpreter is unavailable") from exc
    if (
        TRUSTED_BASH_PATH.is_symlink()
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_uid != 0
        or stat.S_IMODE(metadata.st_mode) & 0o022
        or not os.access(TRUSTED_BASH_PATH, os.X_OK)
    ):
        raise PipelineError("trusted Bash interpreter posture is invalid")
    return str(TRUSTED_BASH_PATH)


def read_regular_bytes(path: Path, label: str, *, maximum_bytes: int) -> bytes:
    require_absolute(path, label)
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            raise PipelineError(f"{label} must be a regular non-symlink file")
        with os.fdopen(descriptor, "rb") as stream:
            descriptor = -1
            content = stream.read(maximum_bytes + 1)
    except PipelineError:
        raise
    except OSError as exc:
        raise PipelineError(f"{label} must be a regular non-symlink file") from exc
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if len(content) > maximum_bytes:
        raise PipelineError(f"{label} exceeds the fixed size bound")
    return content


def load_n_minus_one_authority(
    path: Path,
    live_release_channel_path: Path,
) -> tuple[str, str, dict[str, str]]:
    content = read_regular_bytes(
        path,
        "N-1 release authority",
        maximum_bytes=MAX_N_MINUS_ONE_AUTHORITY_BYTES,
    )
    if not content:
        raise PipelineError("N-1 release authority is empty")
    try:
        raw = content.decode("utf-8", errors="strict")
        live_content = read_regular_bytes(
            live_release_channel_path,
            "live release-channel authority",
            maximum_bytes=MAX_N_MINUS_ONE_AUTHORITY_BYTES,
        )
        if not live_content:
            raise PipelineError("live release-channel authority is empty")
        live_raw = live_content.decode("utf-8", errors="strict")
        binding = DESKTOP_LIFECYCLE.validate_live_predecessor_authority(
            raw,
            live_raw,
            "windows",
            "win-x64",
        )
    except (UnicodeDecodeError, DESKTOP_LIFECYCLE.ContractError) as exc:
        raise PipelineError(f"N-1 release authority is invalid: {exc}") from exc
    identity = {
        "artifactSha256": require_sha(
            binding.get("artifactSha256"), "N-1 artifact digest"
        ),
        "generationId": str(binding.get("generationId") or ""),
        "liveReleaseChannelSha256": require_sha(
            binding.get("liveReleaseChannelSha256"),
            "live release-channel digest",
        ),
        "manifestSha256": require_sha(
            binding.get("manifestSha256"), "N-1 manifest digest"
        ),
        "payloadSha256": require_sha(
            binding.get("payloadSha256"), "N-1 payload digest"
        ),
        "selectedTupleSha256": require_sha(
            binding.get("selectedTupleSha256"),
            "live-predecessor selected-tuple digest",
        ),
        "sha256": sha256_bytes(content),
        "version": str(binding.get("version") or ""),
    }
    if (
        not PORTABLE_VERSION_RE.fullmatch(identity["version"])
        or not identity["generationId"]
    ):
        raise PipelineError("N-1 release identity is not portable")
    return raw, live_raw, identity


def load_stage_authority(path: Path) -> tuple[dict[str, str], str]:
    content = read_regular_bytes(
        path,
        "stage authority input",
        maximum_bytes=MAX_STAGE_AUTHORITY_BYTES,
    )
    payload = parse_json_bytes(content, "stage authority input")
    if set(payload) != {"contractName", "contractVersion", "environment"}:
        raise PipelineError("stage authority input has missing or extra fields")
    if (
        payload.get("contractName") != "chummer6-ui.preview-nightly-stage-authority-input"
        or payload.get("contractVersion") != 1
    ):
        raise PipelineError("stage authority input contract is invalid")
    environment = payload.get("environment")
    if not isinstance(environment, dict) or set(environment) != STAGE_AUTHORITY_ENVIRONMENTS:
        raise PipelineError("stage authority environment set is not exact")
    normalized: dict[str, str] = {}
    for key, value in environment.items():
        if not isinstance(value, str) or not value or value != value.strip() or "\x00" in value:
            raise PipelineError(f"stage authority value is not an exact non-empty string: {key}")
        normalized[key] = value
    for _, _, commit_key in SOURCE_AUTHORITY_ENVIRONMENTS:
        require_commit(normalized[commit_key], commit_key)
    for digest_key in STAGE_AUTHORITY_DIGESTS:
        require_sha(normalized[digest_key], digest_key)
    for path_key in STAGE_AUTHORITY_PATHS:
        candidate = Path(normalized[path_key])
        if not candidate.is_absolute():
            raise PipelineError(f"stage authority path must be absolute: {path_key}")
    return normalized, sha256_bytes(content)


def stage_environment(
    args: argparse.Namespace, parent: dict[str, str] | None = None
) -> tuple[dict[str, str], list[dict[str, str]], str]:
    authority, authority_sha = load_stage_authority(args.stage_authority_input)
    incoming = os.environ if parent is None else parent
    path_value = incoming.get("PATH") or os.defpath
    require_trusted_bash()
    for command in ("dotnet", "git", "python3"):
        if shutil.which(command, path=path_value) is None:
            raise PipelineError(f"required stage command is unavailable: {command}")
    bounded_root = args.evidence_directory / ".stage-child-environment"
    if bounded_root.is_symlink() or (bounded_root.exists() and not bounded_root.is_dir()):
        raise PipelineError("bounded stage environment root is not an exact directory")
    bounded_root.mkdir(mode=0o700, parents=True, exist_ok=True)
    bounded_directories = {
        "DOTNET_CLI_HOME": bounded_root / "dotnet-home",
        "HOME": bounded_root / "home",
        "NUGET_HTTP_CACHE_PATH": bounded_root / "nuget-http",
        "NUGET_PACKAGES": bounded_root / "nuget-packages",
        "NUGET_PLUGINS_CACHE_PATH": bounded_root / "nuget-plugins",
        "TEMP": bounded_root / "tmp",
        "TMP": bounded_root / "tmp",
        "TMPDIR": bounded_root / "tmp",
        "XDG_CACHE_HOME": bounded_root / "xdg-cache",
        "XDG_CONFIG_HOME": bounded_root / "xdg-config",
        "XDG_DATA_HOME": bounded_root / "xdg-data",
    }
    for directory in set(bounded_directories.values()):
        if directory.is_symlink() or (directory.exists() and not directory.is_dir()):
            raise PipelineError("bounded stage environment contains a non-directory entry")
        directory.mkdir(mode=0o700, exist_ok=True)
    environment = {
        key: value
        for key, value in incoming.items()
        if key in STAGE_CHILD_ENVIRONMENT_PASSTHROUGH and value
    }
    environment.update(
        {
            **{key: str(value) for key, value in bounded_directories.items()},
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "MSBUILDDISABLENODEREUSE": "1",
            "NUGET_XMLDOC_MODE": "skip",
            "PATH": path_value,
        }
    )
    environment.update(authority)
    environment.update(
        {
            "CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR": str(args.prepared_stage_root),
            "CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR": str(args.stage_dir),
            "CHUMMER_PREVIEW_NIGHTLY_VERSION": args.release_version,
            "CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT": args.published_at,
        }
    )
    authorities = [
        {"name": name, "commit": authority[commit_key]}
        for name, _, commit_key in SOURCE_AUTHORITY_ENVIRONMENTS
    ]
    return environment, authorities, authority_sha


def prepare_stage_environment(
    args: argparse.Namespace,
    public_signing_environment: dict[str, str] | None = None,
    parent: dict[str, str] | None = None,
) -> tuple[dict[str, str], list[dict[str, str]], str]:
    environment, authorities, authority_sha = stage_environment(args, parent)
    signing = public_signing_environment or {}
    if (
        not PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS.issubset(signing)
        or not set(signing).issubset(PREPARE_SIGNING_PUBLIC_ENVIRONMENTS)
    ):
        raise PipelineError("prepare public signing environment set is not exact")
    environment.update(signing)
    return environment, authorities, authority_sha


def _contains_control(value: str) -> bool:
    return any(ord(character) < 32 or ord(character) == 127 for character in value)


def _is_signing_environment_name(key: str) -> bool:
    return (
        key.startswith("SM_")
        or key.startswith("SIGNING_HANDOFF_")
        or key.startswith("SIGNING_SM_")
        or key.startswith("CHUMMER_KEYLOCKER_")
        or key.startswith("CHUMMER_WINDOWS_SIGN")
        or key.startswith("CHUMMER_WINDOWS_KEYLOCKER")
        or key.startswith("CHUMMER_WINDOWS_JSIGN")
        or key == "CHUMMER_WINDOWS_TIMESTAMP_URL"
        or key == PREPARE_SIGNING_HANDOFF_ENVIRONMENT
    )


def _reject_unsafe_prepare_environment_names(
    incoming: dict[str, str] | os._Environ[str],
) -> None:
    for key in incoming:
        if (
            key in FORBIDDEN_SHELL_ENVIRONMENTS
            or key in FORBIDDEN_PREPARE_ENVIRONMENT_NAMES
            or any(
                key.startswith(prefix)
                for prefix in FORBIDDEN_PREPARE_ENVIRONMENT_PREFIXES
            )
        ):
            raise PipelineError(
                f"forbidden child-initialization environment is set: {key}"
            )
        if (
            _is_signing_environment_name(key)
            and key not in PREPARE_SIGNING_ACCEPTED_ENVIRONMENTS
        ):
            raise PipelineError(
                f"unknown or forbidden signing environment is set: {key}"
            )


def _require_bounded_signing_value(
    value: Any, key: str, maximum_bytes: int, *, forbid_pipe: bool = False
) -> str:
    if (
        not isinstance(value, str)
        or not value
        or value != value.strip()
        or "\x00" in value
        or _contains_control(value)
        or (forbid_pipe and "|" in value)
    ):
        raise PipelineError(f"prepare signing value is invalid: {key}")
    try:
        encoded = value.encode("utf-8")
    except UnicodeError as exc:
        raise PipelineError(f"prepare signing value is invalid: {key}") from exc
    if len(encoded) > maximum_bytes:
        raise PipelineError(f"prepare signing value exceeds its fixed bound: {key}")
    return value


def _require_https_endpoint(value: str, key: str) -> str:
    try:
        parsed = urlparse(value)
        hostname = parsed.hostname
        port = parsed.port
    except ValueError as exc:
        raise PipelineError(f"prepare signing endpoint is invalid: {key}") from exc
    if (
        parsed.scheme != "https"
        or not hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or parsed.path not in {"", "/"}
        or "*" in hostname
        or port is not None
        and not 1 <= port <= 65535
    ):
        raise PipelineError(f"prepare signing endpoint is invalid: {key}")
    return value


def _require_private_client_certificate(path_value: str) -> str:
    path = Path(path_value)
    if not path.is_absolute() or path.suffix.casefold() not in {".p12", ".pfx"}:
        raise PipelineError(
            "prepare signing client certificate must be an absolute .p12 or .pfx path"
        )
    try:
        normalized = path.resolve(strict=True)
        metadata = path.lstat()
    except OSError as exc:
        raise PipelineError("prepare signing client certificate is unavailable") from exc
    if (
        normalized != path
        or path.is_symlink()
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or metadata.st_nlink != 1
        or stat.S_IMODE(metadata.st_mode) not in {0o400, 0o600}
        or not 1 <= metadata.st_size <= 1024 * 1024
    ):
        raise PipelineError(
            "prepare signing client certificate must be a private owned regular file"
        )
    return path_value


def _require_public_signing_path(value: str, key: str) -> str:
    path = Path(value)
    if not path.is_absolute():
        raise PipelineError(f"prepare public signing path must be absolute: {key}")
    require_regular(path, f"prepare public signing path {key}")
    return value


def prepare_signing_toolchain_environment(
    signer_environment: dict[str, str] | None = None,
) -> dict[str, str]:
    environment = {
        "CHUMMER_KEYLOCKER_DOTNET_ROOT": str(PREPARE_SIGNING_DOTNET_ROOT),
        "CHUMMER_KEYLOCKER_DOTNET_BIN": str(PREPARE_SIGNING_DOTNET_BIN),
        "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256": (
            PREPARE_SIGNING_APPROVED_DOTNET_SHA256
        ),
        "CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256": (
            PREPARE_SIGNING_APPROVED_DOTNET_TREE_SHA256
        ),
        "CHUMMER_KEYLOCKER_JAVA_HOME": str(PREPARE_SIGNING_JAVA_HOME),
        "CHUMMER_KEYLOCKER_JAVA_BIN": str(PREPARE_SIGNING_JAVA_BIN),
        "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256": (
            PREPARE_SIGNING_APPROVED_JAVA_SHA256
        ),
        "CHUMMER_KEYLOCKER_JAVA_TREE_SHA256": (
            PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256
        ),
        "CHUMMER_KEYLOCKER_JSIGN_JAR": str(PREPARE_SIGNING_JSIGN_JAR),
        "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256": (
            PREPARE_SIGNING_APPROVED_JSIGN_SHA256
        ),
        "CHUMMER_KEYLOCKER_SIGNER_SDK_PIN_SHA256": (
            PREPARE_SIGNING_APPROVED_SDK_PIN_SHA256
        ),
    }
    if signer_environment is not None:
        if set(signer_environment) != {
            "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_DLL",
            "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256",
        }:
            raise PipelineError("prepared signer identity set is not exact")
        environment.update(signer_environment)
    return environment


def capture_prepare_signing_material(
    parent: dict[str, str] | None = None,
    *,
    consume: bool = False,
    signer_runtime_root: Path | None = None,
    signer_environment: dict[str, str] | None = None,
) -> PrepareSigningMaterial:
    incoming = os.environ if parent is None else parent
    _reject_unsafe_prepare_environment_names(incoming)
    if (signer_runtime_root is None) != (signer_environment is None):
        raise PipelineError("prepared signer runtime identity is incomplete")
    required_environment = frozenset(
        {
            *PREPARE_SIGNING_SECRET_ENVIRONMENTS,
            *PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS,
        }
    )
    missing = [
        key
        for key in required_environment
        if not incoming.get(key)
    ]
    if missing:
        raise PipelineError(
            "prepare requires the complete fixed DIGICERTONE signing environment"
        )

    caller_public_environment = frozenset(
        {
            *PREPARE_SIGNING_REQUIRED_PUBLIC_ENVIRONMENTS,
            *PREPARE_SIGNING_OPTIONAL_PUBLIC_ENVIRONMENTS,
        }
    )
    public_environment = {
        key: _require_bounded_signing_value(incoming[key], key, 4096)
        for key in caller_public_environment
        if incoming.get(key)
    }
    if public_environment["CHUMMER_WINDOWS_SIGNING_BACKEND"] != PREPARE_SIGNING_BACKEND:
        raise PipelineError("prepare signing backend is not the fixed Linux Jsign backend")
    _require_bounded_signing_value(
        public_environment["CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS"],
        "CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS",
        512,
    )
    for key in (
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256",
        "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256",
    ):
        pin = require_sha(public_environment[key], key)
        if pin != public_environment[key]:
            raise PipelineError("prepare signer certificate pins must be lowercase")
    timestamp_url = public_environment.get("CHUMMER_WINDOWS_TIMESTAMP_URL")
    if timestamp_url is not None and timestamp_url != "http://timestamp.digicert.com":
        raise PipelineError("prepare signing timestamp URL is not the fixed endpoint")

    secret_values: list[str] = []
    for key in PREPARE_SIGNING_SECRET_ENVIRONMENTS:
        value = _require_bounded_signing_value(
            incoming[key],
            key,
            PREPARE_SIGNING_SECRET_LIMITS[key],
            forbid_pipe=key != "SM_HOST",
        )
        if key == "SM_HOST":
            _require_https_endpoint(value, key)
            if value != PREPARE_SIGNING_HOST:
                raise PipelineError("prepare signing host is not the fixed endpoint")
        elif key == "SM_CLIENT_CERT_FILE":
            value = _require_private_client_certificate(value)
        secret_values.append(value)

    _require_public_signing_path(
        public_environment["CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH"],
        "CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH",
    )
    _require_public_signing_path(
        str(PREPARE_SIGNING_JAVA_BIN),
        "CHUMMER_KEYLOCKER_JAVA_BIN",
    )
    if not os.access(PREPARE_SIGNING_JAVA_BIN, os.X_OK):
        raise PipelineError("prepare signing Java path is not executable")
    _require_public_signing_path(
        str(PREPARE_SIGNING_JSIGN_JAR),
        "CHUMMER_KEYLOCKER_JSIGN_JAR",
    )
    if sha256_file(
        PREPARE_SIGNING_JAVA_BIN
    ) != PREPARE_SIGNING_APPROVED_JAVA_SHA256:
        raise PipelineError("prepare signing Java digest differs")
    if sha256_file(
        PREPARE_SIGNING_JSIGN_JAR
    ) != PREPARE_SIGNING_APPROVED_JSIGN_SHA256:
        raise PipelineError("prepare signing Jsign digest differs")
    if _sha256_governed_java_tree() != PREPARE_SIGNING_APPROVED_JAVA_TREE_SHA256:
        raise PipelineError("prepare signing Java tree digest differs")
    public_environment.update(
        prepare_signing_toolchain_environment(signer_environment)
    )

    fields = [PREPARE_SIGNING_HANDOFF_MAGIC, *secret_values]
    handoff = bytearray()
    for field in fields:
        handoff.extend(field.encode("utf-8"))
        handoff.append(0)
    if len(handoff) > MAX_PREPARE_SIGNING_HANDOFF_BYTES:
        raise PipelineError("prepare signing handoff exceeds its fixed total bound")

    if consume:
        for key in PREPARE_SIGNING_ACCEPTED_ENVIRONMENTS:
            incoming.pop(key, None)
    return PrepareSigningMaterial(
        handoff,
        public_environment,
        signer_runtime_root,
    )


def reject_ambient_signing_environment(
    parent: dict[str, str] | None = None,
) -> None:
    incoming = os.environ if parent is None else parent
    for key in incoming:
        if key in FORBIDDEN_SHELL_ENVIRONMENTS:
            raise PipelineError(f"forbidden child-initialization environment is set: {key}")
        if _is_signing_environment_name(key):
            raise PipelineError(f"signing environment is not allowed for this pipeline phase: {key}")


def jit_environment(
    parent: dict[str, str] | None = None,
) -> dict[str, str]:
    incoming = os.environ if parent is None else parent
    reject_ambient_signing_environment(incoming)
    path_value = incoming.get("PATH") or os.defpath
    environment = {
        key: value
        for key, value in incoming.items()
        if key in JIT_CHILD_ENVIRONMENT_PASSTHROUGH and value
    }
    environment["PATH"] = path_value
    return environment


def atomic_write(path: Path, payload: dict[str, Any], *, exclusive: bool = False) -> str:
    require_absolute(path, "JSON output")
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_symlink() or (exclusive and path.exists()):
        raise PipelineError(f"refusing existing or linked immutable output: {path}")
    encoded = canonical_bytes(payload) + b"\n"
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        if exclusive:
            try:
                os.link(temporary, path)
            except FileExistsError as exc:
                raise PipelineError(f"immutable output already exists: {path}") from exc
            temporary.unlink()
        else:
            os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()
    return sha256_bytes(encoded)


def state_digest(payload: dict[str, Any]) -> str:
    unsigned = {key: value for key, value in payload.items() if key != "stateSha256"}
    return sha256_bytes(canonical_bytes(unsigned))


def write_state(path: Path, payload: dict[str, Any]) -> None:
    payload = dict(payload)
    payload["updatedAt"] = now_iso()
    payload["stateSha256"] = state_digest(payload)
    atomic_write(path, payload)


def load_state(path: Path) -> dict[str, Any]:
    payload = load_json(require_regular(path, "pipeline state"), "pipeline state")
    if payload.get("contractName") != STATE_CONTRACT or payload.get("contractVersion") != 1:
        raise PipelineError("pipeline state contract is invalid")
    claimed = require_sha(payload.get("stateSha256"), "pipeline state digest")
    if claimed != state_digest(payload):
        raise PipelineError("pipeline resume state was modified or forged")
    if payload.get("repository") != REPOSITORY or payload.get("sourceRef") != SOURCE_REF:
        raise PipelineError("pipeline state repository/ref authority differs")
    return payload


def parse_utc(value: Any, label: str) -> datetime:
    if not isinstance(value, str) or not value.strip():
        raise PipelineError(f"{label} is missing")
    token = value.strip()
    try:
        parsed = datetime.fromisoformat(token[:-1] + "+00:00" if token.endswith("Z") else token)
    except ValueError as exc:
        raise PipelineError(f"{label} is not RFC3339") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise PipelineError(f"{label} lacks timezone authority")
    return parsed.astimezone(UTC)


def workflow_path_matches(value: Any, workflow: str, sha: str) -> bool:
    if not isinstance(value, str):
        return False
    return value in {
        workflow,
        f"{workflow}@main",
        f"{workflow}@{SOURCE_REF}",
        f"{workflow}@{sha}",
    }


def validate_run(
    run: dict[str, Any], *, run_id: str, workflow: str, sha: str, require_success: bool
) -> dict[str, Any]:
    if str(run.get("id")) != require_positive_string(run_id, "workflow run ID"):
        raise PipelineError("workflow run ID differs")
    if run.get("event") != "workflow_dispatch":
        raise PipelineError("workflow run is not a workflow_dispatch run")
    if run.get("head_branch") != "main" or require_commit(run.get("head_sha"), "run head SHA") != sha:
        raise PipelineError("workflow run ref/SHA differs")
    if not workflow_path_matches(run.get("path"), workflow, sha):
        raise PipelineError("workflow run path differs")
    repository = run.get("repository") if isinstance(run.get("repository"), dict) else {}
    if repository.get("full_name") != REPOSITORY:
        raise PipelineError("workflow run repository differs")
    require_positive_string(str(run.get("workflow_id") or ""), "workflow ID")
    if require_success and (run.get("status") != "completed" or run.get("conclusion") != "success"):
        raise PipelineError("workflow run has not completed successfully")
    require_login((run.get("actor") or {}).get("login"), "workflow actor")
    require_positive_string(str(run.get("run_attempt") or ""), "workflow run attempt")
    return run


def validate_workflow_metadata(
    metadata: dict[str, Any], *, workflow_id: str, workflow: str
) -> dict[str, Any]:
    expected_id = require_positive_string(workflow_id, "workflow ID")
    if str(metadata.get("id") or "") != expected_id:
        raise PipelineError("workflow metadata ID differs")
    if metadata.get("path") != workflow:
        raise PipelineError("workflow metadata path differs")
    if metadata.get("state") != "active":
        raise PipelineError("workflow metadata is not active")
    return metadata


def validate_artifact(
    artifact: dict[str, Any], *, expected_name: str, expected_id: str | None = None
) -> dict[str, Any]:
    artifact_id = require_positive_string(str(artifact.get("id") or ""), "artifact ID")
    if expected_id is not None and artifact_id != require_positive_string(expected_id, "expected artifact ID"):
        raise PipelineError("artifact ID differs")
    if artifact.get("name") != expected_name:
        raise PipelineError("artifact name differs")
    if artifact.get("expired") is not False:
        raise PipelineError("artifact is expired")
    require_sha(artifact.get("digest"), "artifact API digest")
    size = artifact.get("size_in_bytes")
    if type(size) is not int or not 1 <= size <= MAX_ARCHIVE_BYTES:
        raise PipelineError("artifact size is outside the fixed bound")
    created = parse_utc(artifact.get("created_at"), "artifact created_at")
    expires = parse_utc(artifact.get("expires_at"), "artifact expires_at")
    current = now_utc()
    if created >= expires or created > current.replace(microsecond=0) + timedelta(minutes=5):
        raise PipelineError("artifact timestamp ordering is invalid")
    if expires <= current:
        raise PipelineError("artifact is no longer available from Actions")
    return artifact


class GitHubClient:
    def __init__(self) -> None:
        if shutil.which("gh") is None:
            raise PipelineError("gh is required")
        self._validated_workflows: set[tuple[str, str]] = set()

    @staticmethod
    def _command(path: str, method: str = "GET", fields: dict[str, str] | None = None) -> list[str]:
        command = [
            "gh",
            "api",
            "--hostname",
            "github.com",
            "-H",
            "Accept: application/vnd.github+json",
            "-H",
            "X-GitHub-Api-Version: 2026-03-10",
            "--method",
            method,
            path,
        ]
        for key, value in sorted((fields or {}).items()):
            command.extend(["-f", f"{key}={value}"])
        return command

    def json(self, path: str, method: str = "GET", fields: dict[str, str] | None = None) -> dict[str, Any]:
        completed = subprocess.run(
            self._command(path, method, fields),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        if completed.returncode != 0:
            raise PipelineError(f"GitHub API {method} failed for a fixed release-control endpoint")
        return parse_json_bytes(completed.stdout, f"GitHub API {path}")

    def download(self, artifact_id: str, output: Path, expected: dict[str, Any]) -> None:
        require_absolute(output, "artifact archive output")
        if output.exists() or output.is_symlink():
            raise PipelineError(f"artifact archive output already exists: {output}")
        completed = subprocess.run(
            self._command(f"repos/{REPOSITORY}/actions/artifacts/{artifact_id}/zip"),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        if completed.returncode != 0:
            raise PipelineError("original Actions artifact download failed")
        content = completed.stdout
        if len(content) != expected["size_in_bytes"]:
            raise PipelineError("downloaded artifact size differs from authenticated API metadata")
        if sha256_bytes(content) != require_sha(expected.get("digest"), "artifact API digest"):
            raise PipelineError("downloaded artifact bytes differ from authenticated API digest")
        output.parent.mkdir(parents=True, exist_ok=True)
        descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o400)
        try:
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
        except BaseException:
            output.unlink(missing_ok=True)
            raise

    def artifact_for_run(self, run_id: str, expected_name: str, expected_id: str | None = None) -> dict[str, Any]:
        payload = self.json(f"repos/{REPOSITORY}/actions/runs/{run_id}/artifacts?per_page=100&page=1")
        rows = payload.get("artifacts")
        if type(payload.get("total_count")) is not int or not isinstance(rows, list):
            raise PipelineError("artifact inventory response is invalid")
        if payload["total_count"] != len(rows) or len(rows) > 100:
            raise PipelineError("artifact inventory is incomplete or requires pagination")
        matches = [row for row in rows if isinstance(row, dict) and row.get("name") == expected_name]
        if len(matches) != 1:
            raise PipelineError("expected exactly one named workflow artifact")
        return validate_artifact(matches[0], expected_name=expected_name, expected_id=expected_id)

    def run(self, run_id: str, workflow: str, sha: str, require_success: bool) -> dict[str, Any]:
        payload = self.json(f"repos/{REPOSITORY}/actions/runs/{run_id}")
        validated = validate_run(
            payload,
            run_id=run_id,
            workflow=workflow,
            sha=sha,
            require_success=require_success,
        )
        workflow_id = require_positive_string(str(validated.get("workflow_id") or ""), "workflow ID")
        authority = (workflow_id, workflow)
        if authority not in self._validated_workflows:
            metadata = self.json(f"repos/{REPOSITORY}/actions/workflows/{workflow_id}")
            validate_workflow_metadata(metadata, workflow_id=workflow_id, workflow=workflow)
            self._validated_workflows.add(authority)
        return validated


def safe_zip_members(path: Path) -> dict[str, bytes]:
    require_regular(path, "Actions artifact ZIP")
    if path.stat().st_size > MAX_ARCHIVE_BYTES:
        raise PipelineError("Actions artifact ZIP exceeds the fixed bound")
    members: dict[str, bytes] = {}
    expanded = 0
    try:
        with zipfile.ZipFile(path) as archive:
            infos = archive.infolist()
            if not 1 <= len(infos) <= MAX_MEMBERS:
                raise PipelineError("Actions artifact ZIP member count is outside the fixed bound")
            for info in infos:
                pure = PurePosixPath(info.filename)
                if info.filename.endswith("/"):
                    continue
                if pure.is_absolute() or ".." in pure.parts or "" in pure.parts:
                    raise PipelineError("Actions artifact ZIP contains an unsafe path")
                mode = info.external_attr >> 16
                if stat.S_ISLNK(mode):
                    raise PipelineError("Actions artifact ZIP contains a symbolic link")
                if info.flag_bits & 0x1:
                    raise PipelineError("Actions artifact ZIP contains encrypted content")
                expanded += info.file_size
                if expanded > MAX_EXPANDED_BYTES:
                    raise PipelineError("Actions artifact ZIP expanded size exceeds the fixed bound")
                name = pure.as_posix()
                if name in members:
                    raise PipelineError("Actions artifact ZIP contains duplicate paths")
                members[name] = archive.read(info)
    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        raise PipelineError(f"Actions artifact ZIP is invalid: {exc}") from exc
    return members


def find_member(members: dict[str, bytes], basename: str) -> tuple[str, bytes]:
    matches = [(name, content) for name, content in members.items() if PurePosixPath(name).name == basename]
    if len(matches) != 1:
        raise PipelineError(f"artifact must contain exactly one {basename}")
    return matches[0]


def validate_jit_receipt(
    path: Path,
    expected_sha: str,
    expected_n_minus_one: tuple[str, str, dict[str, str]],
) -> dict[str, Any]:
    receipt = load_json(require_regular(path, "JIT receipt"), "JIT receipt")
    if receipt.get("contractName") != JIT_CONTRACT or receipt.get("contractVersion") != 1 or receipt.get("status") != "succeeded":
        raise PipelineError("JIT receipt contract/status is invalid")
    exact = {
        "repository": REPOSITORY,
        "workflow": CANDIDATE_WORKFLOW,
        "ref": SOURCE_REF,
        "sourceSha": expected_sha,
    }
    for key, value in exact.items():
        if receipt.get(key) != value:
            raise PipelineError(f"JIT receipt {key} authority differs")
    run_id = require_positive_string(receipt.get("runId"), "candidate run ID")
    attempt = require_positive_string(receipt.get("runAttempt"), "candidate run attempt")
    artifact = receipt.get("artifact") if isinstance(receipt.get("artifact"), dict) else {}
    require_positive_string(artifact.get("id"), "candidate artifact ID")
    if artifact.get("name") != f"preview-nightly-candidate-{run_id}-{attempt}":
        raise PipelineError("candidate artifact name is not bound to run/attempt")
    require_sha(artifact.get("sha256"), "candidate artifact digest")
    candidate = receipt.get("candidate") if isinstance(receipt.get("candidate"), dict) else {}
    require_sha(candidate.get("manifestSha256"), "candidate manifest digest")
    if not PORTABLE_VERSION_RE.fullmatch(str(candidate.get("version") or "")):
        raise PipelineError("candidate version in JIT receipt is invalid")
    signer = (
        receipt.get("signerAuthority")
        if isinstance(receipt.get("signerAuthority"), dict)
        else {}
    )
    if set(signer) != {"certificateSha256", "spkiSha256"}:
        raise PipelineError("JIT receipt signer authority has missing or extra fields")
    certificate_sha256 = signer.get("certificateSha256")
    spki_sha256 = signer.get("spkiSha256")
    if (
        not isinstance(certificate_sha256, str)
        or SHA256_RE.fullmatch(certificate_sha256) is None
    ):
        raise PipelineError(
            "JIT signer certificate digest must be exact lowercase SHA-256"
        )
    if (
        not isinstance(spki_sha256, str)
        or SHA256_RE.fullmatch(spki_sha256) is None
    ):
        raise PipelineError("JIT signer SPKI digest must be exact lowercase SHA-256")
    expected_raw, expected_live_raw, expected_identity = expected_n_minus_one
    receipt_n_minus_one = (
        receipt.get("nMinusOneRelease")
        if isinstance(receipt.get("nMinusOneRelease"), dict)
        else {}
    )
    if receipt_n_minus_one != expected_identity:
        raise PipelineError("JIT receipt N-1 identity differs from exact input authority")
    if candidate.get("version") == expected_identity["version"]:
        raise PipelineError("candidate version must differ from N-1 authority version")
    try:
        validated = DESKTOP_LIFECYCLE.validate_windows_relay_authority(
            expected_raw,
            expected_live_raw,
            certificate_sha256,
            spki_sha256,
            expected_sha256=expected_identity["sha256"],
            expected_live_release_channel_sha256=expected_identity[
                "liveReleaseChannelSha256"
            ],
            expected_selected_tuple_sha256=expected_identity[
                "selectedTupleSha256"
            ],
        )
    except DESKTOP_LIFECYCLE.ContractError as exc:
        raise PipelineError(f"JIT relay authority is invalid: {exc}") from exc
    if any(
        validated.get(key) != value
        for key, value in expected_identity.items()
    ):
        raise PipelineError("JIT relay authority validation changed N-1 identity")
    dispatch_artifact = (
        receipt.get("captureDispatchArtifact")
        if isinstance(receipt.get("captureDispatchArtifact"), dict)
        else {}
    )
    require_positive_string(dispatch_artifact.get("id"), "capture dispatch artifact ID")
    if dispatch_artifact.get("name") != f"preview-nightly-capture-dispatch-{run_id}-{attempt}":
        raise PipelineError("capture dispatch artifact name is not bound to candidate run/attempt")
    require_sha(dispatch_artifact.get("sha256"), "capture dispatch artifact digest")
    return receipt


def wait_for_capture(
    client: GitHubClient, *, run_id: str, sha: str, deadline: float
) -> dict[str, Any]:
    while time.monotonic() < deadline:
        run = client.run(run_id, CAPTURE_WORKFLOW, sha, require_success=False)
        if run.get("status") == "completed":
            return validate_run(
                run,
                run_id=run_id,
                workflow=CAPTURE_WORKFLOW,
                sha=sha,
                require_success=True,
            )
        time.sleep(5)
    raise PipelineError(f"timed out waiting for exact relayed native capture {run_id}")


def validate_capture_dispatch(
    archive: Path, *, candidate: dict[str, Any], source_sha: str
) -> dict[str, Any]:
    members = safe_zip_members(archive)
    if set(members) != {CAPTURE_DISPATCH_RECEIPT}:
        raise PipelineError("capture dispatch artifact must contain exactly its correlation receipt")
    receipt = parse_json_bytes(members[CAPTURE_DISPATCH_RECEIPT], "capture dispatch receipt")
    if set(receipt) != {
        "candidateHandoff",
        "candidateHandoffSha256",
        "capture",
        "contractName",
        "contractVersion",
        "liveReleaseChannelSha256",
        "nMinusOneReleaseSha256",
        "selectedTupleSha256",
        "signerAuthority",
        "status",
    }:
        raise PipelineError("capture dispatch receipt has missing or extra fields")
    if (
        receipt.get("contractName") != "chummer6-ui.preview-nightly-capture-dispatch"
        or receipt.get("contractVersion") != 3
        or receipt.get("status") != "dispatched"
    ):
        raise PipelineError("capture dispatch receipt contract/status differs")
    expected_handoff = {
        "actor": candidate["actor"],
        "artifactId": candidate["artifactId"],
        "artifactName": candidate["artifactName"],
        "artifactSha256": candidate["artifactSha256"],
        "authenticodeSignerCertificateSha256": candidate[
            "authenticodeSignerCertificateSha256"
        ],
        "authenticodeSignerSpkiSha256": candidate[
            "authenticodeSignerSpkiSha256"
        ],
        "contentInventorySha256": candidate["contentInventorySha256"],
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 4,
        "fullShelfCompatibilityManifestSha256": candidate[
            "fullShelfCompatibilityManifestSha256"
        ],
        "fullShelfManifestSha256": candidate["fullShelfManifestSha256"],
        "liveReleaseChannelSha256": candidate[
            "liveReleaseChannelSha256"
        ],
        "nMinusOneReleaseSha256": candidate["nMinusOneReleaseSha256"],
        "publicationScopeSha256": candidate["publicationScopeSha256"],
        "ref": SOURCE_REF,
        "registryPrepareSha256": candidate["registryPrepareSha256"],
        "repository": REPOSITORY,
        "runAttempt": candidate["runAttempt"],
        "runId": candidate["runId"],
        "scopeDecisionSha256": candidate["scopeDecisionSha256"],
        "selectedTupleSha256": candidate["selectedTupleSha256"],
        "sha": source_sha,
        "signingReceiptSha256": candidate["signingReceiptSha256"],
        "workflow": CANDIDATE_WORKFLOW,
    }
    if receipt.get("candidateHandoff") != expected_handoff:
        raise PipelineError("capture dispatch candidate run/artifact/inventory correlation differs")
    if require_sha(receipt.get("candidateHandoffSha256"), "candidate handoff digest") != sha256_bytes(
        canonical_bytes(expected_handoff)
    ):
        raise PipelineError("capture dispatch candidate handoff digest differs")
    if require_sha(
        receipt.get("nMinusOneReleaseSha256"),
        "capture dispatch N-1 authority digest",
    ) != candidate["nMinusOneReleaseSha256"]:
        raise PipelineError("capture dispatch N-1 authority digest differs")
    if require_sha(
        receipt.get("liveReleaseChannelSha256"),
        "capture dispatch live release-channel digest",
    ) != candidate["liveReleaseChannelSha256"]:
        raise PipelineError("capture dispatch live release-channel digest differs")
    if require_sha(
        receipt.get("selectedTupleSha256"),
        "capture dispatch selected-tuple digest",
    ) != candidate["selectedTupleSha256"]:
        raise PipelineError("capture dispatch selected-tuple digest differs")
    expected_signer = {
        "certificateSha256": candidate["authenticodeSignerCertificateSha256"],
        "spkiSha256": candidate["authenticodeSignerSpkiSha256"],
    }
    if receipt.get("signerAuthority") != expected_signer:
        raise PipelineError("capture dispatch signer authority differs")
    capture = receipt.get("capture")
    if not isinstance(capture, dict) or set(capture) != {
        "htmlUrl",
        "ref",
        "repository",
        "runId",
        "runUrl",
        "workflow",
    }:
        raise PipelineError("capture dispatch run identity is invalid")
    run_id = require_positive_string(capture.get("runId"), "capture dispatch run ID")
    exact_capture = {
        "htmlUrl": f"https://github.com/{REPOSITORY}/actions/runs/{run_id}",
        "ref": SOURCE_REF,
        "repository": REPOSITORY,
        "runId": run_id,
        "runUrl": f"https://api.github.com/repos/{REPOSITORY}/actions/runs/{run_id}",
        "workflow": CAPTURE_WORKFLOW,
    }
    if capture != exact_capture:
        raise PipelineError("capture dispatch run URLs/ref/workflow differ")
    return capture


def wait_for_exact_run(
    client: GitHubClient, *, run_id: str, workflow: str, sha: str, deadline: float
) -> dict[str, Any]:
    while time.monotonic() < deadline:
        run = client.run(run_id, workflow, sha, require_success=False)
        if run.get("status") == "completed":
            return validate_run(run, run_id=run_id, workflow=workflow, sha=sha, require_success=True)
        time.sleep(5)
    raise PipelineError(f"timed out waiting for exact run {run_id}")


def copy_original_artifact(
    client: GitHubClient, artifact: dict[str, Any], output: Path
) -> dict[str, Any]:
    client.download(str(artifact["id"]), output, artifact)
    digest = sha256_file(output)
    if digest != require_sha(artifact.get("digest"), "artifact API digest"):
        raise PipelineError("preserved original artifact digest changed after write")
    return {
        "archivePath": str(output),
        "archiveSha256": digest,
        "artifactId": str(artifact["id"]),
        "artifactName": artifact["name"],
        "apiDigest": artifact["digest"],
        "createdAt": artifact["created_at"],
        "expiresAt": artifact["expires_at"],
        "onlineAvailabilityClaim": "unexpired_at_acquisition_only",
        "sizeBytes": artifact["size_in_bytes"],
    }


def require_capture_member_binding(
    members: dict[str, bytes],
    binding: Any,
    *,
    label: str,
    expected_keys: set[str],
) -> tuple[str, bytes]:
    if not isinstance(binding, dict) or set(binding) != expected_keys:
        raise PipelineError(f"{label} binding has missing or extra fields")
    path = binding.get("path")
    if not isinstance(path, str) or path not in members:
        raise PipelineError(f"{label} binding path is absent from the capture artifact")
    content = members[path]
    if (
        require_sha(binding.get("sha256"), f"{label} binding digest")
        != sha256_bytes(content)
        or type(binding.get("sizeBytes")) is not int
        or binding["sizeBytes"] != len(content)
        or binding["sizeBytes"] < 1
    ):
        raise PipelineError(f"{label} binding differs from the exact capture member")
    return path, content


def build_scope_approval_context(
    members: dict[str, bytes],
    capture: dict[str, Any],
    candidate_binding: dict[str, Any],
) -> dict[str, Any]:
    proposal_path, proposal_bytes = require_capture_member_binding(
        members,
        candidate_binding.get("publicationScope"),
        label="publication scope proposal",
        expected_keys={"path", "sha256", "sizeBytes"},
    )
    expected_proposal_path = (
        f"candidate-provenance/{PUBLICATION_SCOPE.PROPOSAL_FILE_NAME}"
    )
    if proposal_path != expected_proposal_path:
        raise PipelineError("publication scope proposal capture path differs")
    proposal = parse_json_bytes(proposal_bytes, "publication scope proposal")
    try:
        PUBLICATION_SCOPE.validate_proposal(proposal)
    except PUBLICATION_SCOPE.ScopeError as exc:
        raise PipelineError(f"publication scope proposal is invalid: {exc}") from exc
    if proposal.get("status") != "awaiting_native_evidence_and_independent_approval":
        raise PipelineError("publication scope proposal is not awaiting independent approval")

    authenticode = capture.get("authenticodeVerification")
    authenticode_path, _ = require_capture_member_binding(
        members,
        authenticode,
        label="independent Authenticode verification",
        expected_keys={
            "path",
            "sha256",
            "sizeBytes",
            "signerCertificateSha256",
            "signerSpkiSha256",
            "timestampUtc",
        },
    )
    if authenticode_path != AUTHENTICODE_CAPTURE_FILE:
        raise PipelineError("independent Authenticode verification capture path differs")
    require_sha(
        authenticode.get("signerCertificateSha256"),
        "Authenticode signer certificate digest",
    )
    require_sha(authenticode.get("signerSpkiSha256"), "Authenticode signer SPKI digest")
    if not isinstance(authenticode.get("timestampUtc"), str) or not authenticode[
        "timestampUtc"
    ]:
        raise PipelineError("independent Authenticode timestamp is absent")

    return {
        "authenticodeVerification": authenticode,
        "candidateProducerActor": require_login(
            candidate_binding.get("actor"), "candidate producer actor"
        ),
        "contractName": "chummer6-ui.preview-nightly-scope-approval-context",
        "contractVersion": 1,
        "proposal": proposal,
        "proposalPath": proposal_path,
        "proposalSha256": sha256_bytes(proposal_bytes),
    }


def build_review_request(
    *,
    capture_run: dict[str, Any],
    capture_artifact: dict[str, Any],
    archive: Path,
    source_sha: str,
    expected_candidate: dict[str, Any],
) -> dict[str, Any]:
    members = safe_zip_members(archive)
    inventory_path, inventory_bytes = find_member(members, CAPTURE_INVENTORY)
    capture_path, capture_bytes = find_member(members, CAPTURE_MANIFEST)
    capture = parse_json_bytes(capture_bytes, "native capture receipt")
    inventory = parse_json_bytes(inventory_bytes, "native capture inventory")
    inventory_sha = sha256_bytes(inventory_bytes)
    candidate_binding = capture.get("candidate")
    windows_only = (
        isinstance(candidate_binding, dict)
        and "publicationScope" in candidate_binding
    )
    if (
        capture.get("contractName")
        != "chummer6-ui.preview-nightly-native-windows-capture"
        or type(capture.get("contractVersion")) is not int
        or capture.get("contractVersion") != 2
        or not windows_only
    ):
        raise PipelineError("native capture receipt contract differs")
    if (
        inventory.get("contractName") != "chummer6-ui.preview-nightly-native-windows-capture-inventory"
        or type(inventory.get("contractVersion")) is not int
        or inventory.get("contractVersion") != 2
        or require_sha(inventory.get("captureManifestSha256"), "capture manifest inventory digest")
        != sha256_bytes(capture_bytes)
    ):
        raise PipelineError("native capture inventory contract/manifest binding differs")
    inventory_rows = inventory.get("files")
    actual_rows = [
        {"path": name, "sha256": sha256_bytes(content), "sizeBytes": len(content)}
        for name, content in sorted(members.items())
        if name != inventory_path
    ]
    if inventory_rows != actual_rows or capture_path not in {row["path"] for row in actual_rows}:
        raise PipelineError("native capture inventory differs from exact artifact members")
    source = capture.get("source") if isinstance(capture.get("source"), dict) else {}
    expected = {
        "repository": REPOSITORY,
        "workflow": CAPTURE_WORKFLOW,
        "runId": str(capture_run["id"]),
        "runAttempt": str(capture_run["run_attempt"]),
        "sha": source_sha,
        "artifactName": capture_artifact["name"],
    }
    for key, value in expected.items():
        if source.get(key) != value:
            raise PipelineError(f"native capture receipt {key} differs from Actions authority")
    expected_candidate_binding = {
        "repository": REPOSITORY,
        "workflow": CANDIDATE_WORKFLOW,
        "runId": expected_candidate["runId"],
        "runAttempt": expected_candidate["runAttempt"],
        "ref": SOURCE_REF,
        "sha": source_sha,
        "actor": expected_candidate["actor"],
        "artifactId": expected_candidate["artifactId"],
        "artifactName": expected_candidate["artifactName"],
        "artifactSha256": expected_candidate["artifactSha256"],
        "manifestSha256": expected_candidate["manifestSha256"],
        "contentInventorySha256": expected_candidate["contentInventorySha256"],
    }
    if any(candidate_binding.get(key) != value for key, value in expected_candidate_binding.items()):
        raise PipelineError("native capture candidate run/artifact/inventory binding differs")
    scope_approval_context = build_scope_approval_context(
        members, capture, candidate_binding
    )
    screenshot_rows = []
    for name, content in sorted(members.items()):
        if name.casefold().endswith(".png") and "/screenshots/" in f"/{name.casefold()}":
            screenshot_rows.append({"path": name, "sha256": sha256_bytes(content), "sizeBytes": len(content)})
    required_screenshot_count = 2 * len(PROMOTED_WINDOWS_HEADS)
    if (
        len(screenshot_rows) != required_screenshot_count
        or len({row["sha256"] for row in screenshot_rows}) != required_screenshot_count
    ):
        raise PipelineError(
            f"native capture must contain {required_screenshot_count} distinct screenshots"
        )
    return {
        "capture": {
            "actor": require_login((capture_run.get("actor") or {}).get("login"), "capture actor"),
            "artifactId": str(capture_artifact["id"]),
            "artifactName": capture_artifact["name"],
            "artifactSha256": require_sha(capture_artifact.get("digest"), "capture artifact digest"),
            "inventorySha256": inventory_sha,
            "ref": SOURCE_REF,
            "runAttempt": str(capture_run["run_attempt"]),
            "runId": str(capture_run["id"]),
            "sha": source_sha,
            "workflow": CAPTURE_WORKFLOW,
            "workflowId": require_positive_string(
                str(capture_run.get("workflow_id") or ""), "capture workflow ID"
            ),
        },
        "contractName": REVIEW_REQUEST_CONTRACT,
        "contractVersion": 2,
        "generatedAt": now_iso(),
        "humanReviewConfirmed": False,
        "requiredChecks": ["readability", "contrast", "clipping"],
        "requiredHeads": list(PROMOTED_WINDOWS_HEADS),
        "scopeApprovalContext": scope_approval_context,
        "screenshots": screenshot_rows,
        "status": "action_required",
        "warning": (
            "A protected, allowlisted human must inspect the exact named artifact "
            "and independently approve its bound publication scope. This request "
            "is not review or scope-approval evidence."
        ),
    }


def validate_review_input(
    path: Path, *, request: dict[str, Any], request_sha: str, authenticated_login: str
) -> dict[str, Any]:
    review = load_json(require_regular(path, "human review input"), "human review input")
    expected_keys = {
        "capture",
        "contractName",
        "contractVersion",
        "heads",
        "humanReviewConfirmed",
        "reviewRequestSha256",
        "reviewer",
        "scopeApproval",
    }
    if set(review) != expected_keys:
        raise PipelineError("human review input has missing or extra fields")
    if review.get("contractName") != REVIEW_INPUT_CONTRACT or review.get("contractVersion") != 2:
        raise PipelineError("human review input contract is invalid")
    if (
        request.get("contractName") != REVIEW_REQUEST_CONTRACT
        or request.get("contractVersion") != 2
    ):
        raise PipelineError("human review request contract is invalid")
    if review.get("capture") != request.get("capture"):
        raise PipelineError("human review input capture binding differs")
    if require_sha(review.get("reviewRequestSha256"), "review request digest") != request_sha:
        raise PipelineError("human review input is bound to a different request")
    reviewer = require_login(review.get("reviewer"), "human reviewer")
    if reviewer.casefold() != authenticated_login.casefold() or reviewer == "github-actions[bot]":
        raise PipelineError("human review input reviewer is not the authenticated dispatch actor")
    if reviewer.casefold() == str(request["capture"]["actor"]).casefold():
        raise PipelineError("human reviewer must differ from the automated capture actor")
    if review.get("humanReviewConfirmed") is not True:
        raise PipelineError("human review was not explicitly confirmed")
    heads = review.get("heads")
    expected_checks = {"readability": True, "contrast": True, "clipping": True}
    if not isinstance(heads, dict) or set(heads) != set(PROMOTED_WINDOWS_HEADS):
        raise PipelineError("human review input must bind the exact promoted heads")
    for head in PROMOTED_WINDOWS_HEADS:
        if heads.get(head) != expected_checks:
            raise PipelineError(f"human review confirmations are incomplete for {head}")
    scope_context = request.get("scopeApprovalContext")
    if not isinstance(scope_context, dict) or set(scope_context) != {
        "authenticodeVerification",
        "candidateProducerActor",
        "contractName",
        "contractVersion",
        "proposal",
        "proposalPath",
        "proposalSha256",
    }:
        raise PipelineError("human review request scope-approval context is invalid")
    if (
        scope_context.get("contractName")
        != "chummer6-ui.preview-nightly-scope-approval-context"
        or scope_context.get("contractVersion") != 1
        or scope_context.get("proposalPath")
        != f"candidate-provenance/{PUBLICATION_SCOPE.PROPOSAL_FILE_NAME}"
    ):
        raise PipelineError("human review request scope-approval context differs")
    authenticode = scope_context.get("authenticodeVerification")
    if not isinstance(authenticode, dict) or set(authenticode) != {
        "path",
        "sha256",
        "sizeBytes",
        "signerCertificateSha256",
        "signerSpkiSha256",
        "timestampUtc",
    }:
        raise PipelineError("human review request Authenticode binding is invalid")
    if authenticode.get("path") != AUTHENTICODE_CAPTURE_FILE:
        raise PipelineError("human review request Authenticode path differs")
    authenticode_sha = require_sha(
        authenticode.get("sha256"), "human review request Authenticode digest"
    )
    require_sha(
        authenticode.get("signerCertificateSha256"),
        "human review request signer certificate digest",
    )
    require_sha(
        authenticode.get("signerSpkiSha256"),
        "human review request signer SPKI digest",
    )
    if type(authenticode.get("sizeBytes")) is not int or authenticode["sizeBytes"] < 1:
        raise PipelineError("human review request Authenticode size is invalid")
    if not isinstance(authenticode.get("timestampUtc"), str) or not authenticode[
        "timestampUtc"
    ]:
        raise PipelineError("human review request Authenticode timestamp is absent")
    proposal = scope_context.get("proposal")
    if not isinstance(proposal, dict):
        raise PipelineError("human review request publication scope proposal is invalid")
    try:
        PUBLICATION_SCOPE.validate_proposal(proposal)
        approver = PUBLICATION_SCOPE.validate_approval(
            review.get("scopeApproval"),
            proposal,
            require_sha(
                scope_context.get("proposalSha256"),
                "human review request publication scope proposal digest",
            ),
            authenticode_sha,
            [
                request["capture"]["actor"],
                require_login(
                    scope_context.get("candidateProducerActor"),
                    "candidate producer actor",
                ),
            ],
        )
    except PUBLICATION_SCOPE.ScopeError as exc:
        raise PipelineError(f"human scope approval is invalid: {exc}") from exc
    if approver.casefold() != reviewer.casefold():
        raise PipelineError("human reviewer must own the exact scope approval")
    return review


def dispatch_finalization(client: GitHubClient, review: dict[str, Any]) -> str:
    capture = review["capture"]
    heads = review["heads"]
    response = client.json(
        f"repos/{REPOSITORY}/actions/workflows/windows-native-evidence-finalize.yml/dispatches",
        method="POST",
        fields={
            "ref": "main",
            "inputs[capture_run_id]": capture["runId"],
            "inputs[capture_run_attempt]": capture["runAttempt"],
            "inputs[capture_ref]": capture["ref"],
            "inputs[capture_sha]": capture["sha"],
            "inputs[capture_artifact_name]": capture["artifactName"],
            "inputs[capture_inventory_sha256]": capture["inventorySha256"],
            "inputs[human_review_confirmed]": "true",
            "inputs[avalonia_review_json]": json.dumps(
                heads["avalonia"], sort_keys=True, separators=(",", ":")
            ),
            "inputs[scope_approval_json]": json.dumps(
                review["scopeApproval"], sort_keys=True, separators=(",", ":")
            ),
        },
    )
    if set(response) != {"workflow_run_id", "run_url", "html_url"}:
        raise PipelineError("finalization dispatch did not return an exact run identity")
    run_id = require_positive_string(str(response.get("workflow_run_id") or ""), "finalization run ID")
    if response.get("run_url") != f"https://api.github.com/repos/{REPOSITORY}/actions/runs/{run_id}":
        raise PipelineError("finalization dispatch API URL differs")
    if response.get("html_url") != f"https://github.com/{REPOSITORY}/actions/runs/{run_id}":
        raise PipelineError("finalization dispatch HTML URL differs")
    return run_id


def portable_file_name(value: Any, label: str) -> str:
    token = str(value or "")
    name = PureWindowsPath(token).name if "\\" in token else PurePosixPath(token).name
    if not name or name in {".", ".."} or "/" in name or "\\" in name:
        raise PipelineError(f"{label} does not have a portable file name")
    return name


def portable_action_record(
    record: Any, *, role: str, local_keys: tuple[str, ...]
) -> dict[str, Any] | None:
    if record is None:
        return None
    if not isinstance(record, dict):
        raise PipelineError(f"{role} provenance record is invalid")
    result = {key: value for key, value in record.items() if key not in local_keys}
    result["artifactRole"] = role
    for key in local_keys:
        if key in record:
            result[f"{key}FileName"] = portable_file_name(record[key], f"{role} {key}")
    return result


def portable_sealed_stage(record: Any) -> dict[str, Any] | None:
    if record is None:
        return None
    if not isinstance(record, dict):
        raise PipelineError("sealed-stage provenance record is invalid")
    result = {
        key: value for key, value in record.items() if key not in {"path", "sealPath"}
    }
    result.update(
        {
            "artifactRole": "sealed-preview-stage",
            "sealFileName": portable_file_name(record.get("sealPath"), "sealed-stage receipt"),
            "stageName": portable_file_name(record.get("path"), "sealed-stage directory"),
        }
    )
    return result


def require_portable_payload(value: Any, label: str, location: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            require_portable_payload(child, label, f"{location}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            require_portable_payload(child, label, f"{location}[{index}]")
    elif isinstance(value, str):
        parsed = urlparse(value)
        if parsed.scheme and parsed.netloc:
            return
        if PurePosixPath(value).is_absolute() or PureWindowsPath(value).is_absolute():
            raise PipelineError(f"{label} contains a machine-local path at {location}")


def build_provenance_payload(args: argparse.Namespace, state: dict[str, Any]) -> dict[str, Any]:
    handoff = state.get("handoff")
    portable_handoff = None
    if handoff is not None:
        portable_handoff = portable_action_record(
            handoff,
            role="immutable-publication-handoff",
            local_keys=("path",),
        )
    return {
        "artifactAvailability": "Actions artifacts are ephemeral; original acquired ZIPs are preserved separately by digest.",
        "candidate": portable_action_record(
            state.get("candidate"), role="candidate", local_keys=("archivePath",)
        ),
        "capture": portable_action_record(
            state.get("capture"),
            role="native-capture",
            local_keys=("archivePath", "reviewRequestPath"),
        ),
        "captureDispatch": portable_action_record(
            state.get("captureDispatch"),
            role="capture-dispatch-correlation",
            local_keys=("archivePath",),
        ),
        "contractName": PROVENANCE_CONTRACT,
        "contractVersion": 1,
        "finalization": portable_action_record(
            state.get("finalization"),
            role="human-finalized-evidence",
            local_keys=("archivePath", "reviewInputPath"),
        ),
        "generatedAt": now_iso(),
        "handoff": portable_handoff,
        "nMinusOneRelease": state.get("nMinusOneRelease"),
        "phase": state.get("phase"),
        "publicationPerformed": False,
        "release": state.get("release"),
        "repository": REPOSITORY,
        "sealedStage": portable_sealed_stage(state.get("sealedStage")),
        "sourceAuthorities": state.get("sourceAuthorities"),
        "sourceSha": state.get("sourceSha"),
        "stageAuthorityInputSha256": state.get("stageAuthorityInputSha256"),
    }


def build_publication_handoff(
    args: argparse.Namespace, state: dict[str, Any], provenance_sha: str
) -> dict[str, Any]:
    payload = {
        "contractName": HANDOFF_CONTRACT,
        "contractVersion": 1,
        "currentPointerAdvanced": False,
        "deploymentPerformed": False,
        "generatedAt": now_iso(),
        "publicationPerformed": False,
        "releaseVersion": state["candidate"]["version"],
        "requiredFirstConsumerMode": "dry_run",
        "requiredNextAuthority": "separate_credentialed_release_operator",
        "sealedStage": portable_sealed_stage(state["sealedStage"]),
        "sourceSha": state["sourceSha"],
        "status": "sealed_for_dry_run_only",
        "durableProvenance": {
            "artifactRole": "durable-provenance",
            "fileName": portable_file_name(args.provenance_output, "durable provenance"),
            "sha256": provenance_sha,
        },
        "uploadAuthorized": False,
    }
    require_portable_payload(payload, "immutable publication handoff")
    return payload


def write_provenance(args: argparse.Namespace, state: dict[str, Any]) -> str:
    payload = build_provenance_payload(args, state)
    require_portable_payload(payload, "durable provenance")
    return atomic_write(args.provenance_output, payload)


def _terminate_process_group(process: subprocess.Popen[bytes]) -> None:
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        if process.poll() is None:
            try:
                process.wait(timeout=CHILD_TERMINATION_GRACE_SECONDS)
            except subprocess.TimeoutExpired:
                pass
        return
    if process.poll() is None:
        try:
            process.wait(timeout=CHILD_TERMINATION_GRACE_SECONDS)
        except subprocess.TimeoutExpired:
            pass
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        return
    try:
        process.wait(timeout=CHILD_TERMINATION_GRACE_SECONDS)
    except subprocess.TimeoutExpired:
        pass


def _sha256_canonical_tree(parent: Path, root_name: str) -> str:
    root = parent / root_name
    try:
        tar_metadata = TRUSTED_TAR_PATH.lstat()
        parent_metadata = parent.lstat()
        root_metadata = root.lstat()
        normalized_parent = parent.resolve(strict=True)
        normalized_root = root.resolve(strict=True)
    except OSError as exc:
        raise PipelineError("governed Java tree authority is unavailable") from exc
    if (
        TRUSTED_TAR_PATH.is_symlink()
        or not stat.S_ISREG(tar_metadata.st_mode)
        or tar_metadata.st_uid != 0
        or stat.S_IMODE(tar_metadata.st_mode) & 0o022
        or not os.access(TRUSTED_TAR_PATH, os.X_OK)
        or not parent.is_absolute()
        or normalized_parent != parent
        or normalized_root != root
        or parent.is_symlink()
        or root.is_symlink()
        or not stat.S_ISDIR(parent_metadata.st_mode)
        or not stat.S_ISDIR(root_metadata.st_mode)
        or not root_name
        or root_name in {".", ".."}
        or "/" in root_name
    ):
        raise PipelineError("governed Java tree authority posture is invalid")

    command = [
        str(TRUSTED_TAR_PATH),
        "--sort=name",
        "--mtime=UTC 1970-01-01",
        "--owner=0",
        "--group=0",
        "--numeric-owner",
        "-C",
        str(parent),
        "-cf",
        "-",
        root_name,
    ]
    process: subprocess.Popen[bytes] | None = None
    selector: selectors.BaseSelector | None = None
    streams: list[Any] = []
    digest = hashlib.sha256()
    archive_bytes = 0
    error_bytes = 0
    try:
        process = subprocess.Popen(
            command,
            cwd="/",
            env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
            shell=False,
            start_new_session=True,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        if process.stdout is None or process.stderr is None:
            _terminate_process_group(process)
            raise PipelineError("governed Java tree hash pipes are unavailable")
        streams = [process.stdout, process.stderr]
        selector = selectors.DefaultSelector()
        for stream in streams:
            os.set_blocking(stream.fileno(), False)
            selector.register(
                stream,
                selectors.EVENT_READ,
                data="archive" if stream is process.stdout else "error",
            )
        open_streams = len(streams)
        deadline = time.monotonic() + TOOLCHAIN_HASH_TIMEOUT_SECONDS
        while open_streams:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                _terminate_process_group(process)
                raise PipelineError("governed Java tree hash timed out")
            events = selector.select(min(remaining, 0.1))
            if not events:
                continue
            for key, _ in events:
                try:
                    chunk = os.read(key.fd, 1024 * 1024)
                except BlockingIOError:
                    continue
                if not chunk:
                    selector.unregister(key.fileobj)
                    open_streams -= 1
                    continue
                if key.data == "archive":
                    archive_bytes += len(chunk)
                    if archive_bytes > MAX_TOOLCHAIN_TREE_ARCHIVE_BYTES:
                        _terminate_process_group(process)
                        raise PipelineError(
                            "governed Java tree archive exceeds its fixed bound"
                        )
                    digest.update(chunk)
                else:
                    error_bytes += len(chunk)
                    if error_bytes > MAX_TOOLCHAIN_HASH_ERROR_BYTES:
                        _terminate_process_group(process)
                        raise PipelineError(
                            "governed Java tree hash error output exceeds its fixed bound"
                        )
        remaining = max(0.001, deadline - time.monotonic())
        try:
            returncode = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            _terminate_process_group(process)
            raise PipelineError("governed Java tree hash timed out") from None
    except PipelineError:
        raise
    except (OSError, subprocess.SubprocessError):
        if process is not None:
            _terminate_process_group(process)
        raise PipelineError("governed Java tree hash could not complete") from None
    finally:
        if selector is not None:
            try:
                selector.close()
            except OSError:
                pass
        for stream in streams:
            try:
                stream.close()
            except OSError:
                pass
        if process is not None and process.poll() is None:
            _terminate_process_group(process)
    if returncode != 0:
        raise PipelineError("governed Java tree hash failed")
    return digest.hexdigest()


def _sha256_governed_java_tree() -> str:
    return _sha256_canonical_tree(
        PREPARE_SIGNING_JAVA_PARENT,
        PREPARE_SIGNING_JAVA_ROOT_NAME,
    )


def _sha256_governed_dotnet_tree() -> str:
    return _sha256_canonical_tree(
        PREPARE_SIGNING_DOTNET_PARENT,
        PREPARE_SIGNING_DOTNET_ROOT_NAME,
    )


def _require_root_owned_dotnet_tree() -> None:
    try:
        root = PREPARE_SIGNING_DOTNET_ROOT.resolve(strict=True)
        binary = PREPARE_SIGNING_DOTNET_BIN.resolve(strict=True)
        root_metadata = PREPARE_SIGNING_DOTNET_ROOT.lstat()
    except OSError as exc:
        raise PipelineError("governed .NET runtime is unavailable") from exc
    if (
        root != PREPARE_SIGNING_DOTNET_ROOT
        or binary != PREPARE_SIGNING_DOTNET_BIN
        or PREPARE_SIGNING_DOTNET_ROOT.is_symlink()
        or PREPARE_SIGNING_DOTNET_BIN.is_symlink()
        or not stat.S_ISDIR(root_metadata.st_mode)
        or root_metadata.st_uid != 0
        or stat.S_IMODE(root_metadata.st_mode) & 0o022
    ):
        raise PipelineError("governed .NET runtime path posture is invalid")

    pending = [PREPARE_SIGNING_DOTNET_ROOT]
    while pending:
        directory = pending.pop()
        try:
            entries = list(os.scandir(directory))
        except OSError as exc:
            raise PipelineError("governed .NET runtime tree is unreadable") from exc
        for entry in entries:
            path = Path(entry.path)
            try:
                metadata = path.lstat()
            except OSError as exc:
                raise PipelineError(
                    "governed .NET runtime tree changed during validation"
                ) from exc
            if metadata.st_uid != 0:
                raise PipelineError(
                    "governed .NET runtime tree contains a non-root-owned entry"
                )
            if stat.S_ISLNK(metadata.st_mode):
                try:
                    target = path.resolve(strict=True)
                    target.relative_to(PREPARE_SIGNING_DOTNET_ROOT)
                    target_metadata = target.stat()
                except (OSError, ValueError) as exc:
                    raise PipelineError(
                        "governed .NET runtime symlink leaves its fixed tree"
                    ) from exc
                if (
                    target_metadata.st_uid != 0
                    or stat.S_IMODE(target_metadata.st_mode) & 0o022
                ):
                    raise PipelineError(
                        "governed .NET runtime symlink target is mutable"
                    )
                continue
            if stat.S_IMODE(metadata.st_mode) & 0o022:
                raise PipelineError(
                    "governed .NET runtime tree contains a mutable entry"
                )
            if stat.S_ISDIR(metadata.st_mode):
                pending.append(path)
            elif not stat.S_ISREG(metadata.st_mode):
                raise PipelineError(
                    "governed .NET runtime tree contains a special entry"
                )

    binary_metadata = PREPARE_SIGNING_DOTNET_BIN.lstat()
    if (
        not stat.S_ISREG(binary_metadata.st_mode)
        or binary_metadata.st_uid != 0
        or stat.S_IMODE(binary_metadata.st_mode) & 0o022
        or not os.access(PREPARE_SIGNING_DOTNET_BIN, os.X_OK)
    ):
        raise PipelineError("governed .NET host posture is invalid")
    if (
        sha256_file(PREPARE_SIGNING_DOTNET_BIN)
        != PREPARE_SIGNING_APPROVED_DOTNET_SHA256
    ):
        raise PipelineError("governed .NET host digest differs")
    if (
        _sha256_governed_dotnet_tree()
        != PREPARE_SIGNING_APPROVED_DOTNET_TREE_SHA256
    ):
        raise PipelineError("governed .NET runtime tree digest differs")


def _remove_private_signer_runtime(runtime_root: Path) -> None:
    try:
        normalized = runtime_root.resolve(strict=True)
        metadata = runtime_root.lstat()
    except OSError:
        return
    if (
        not runtime_root.is_absolute()
        or normalized != runtime_root
        or runtime_root.is_symlink()
        or not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or not runtime_root.name.startswith("chummer-keylocker-signer-")
    ):
        raise PipelineError("refusing to remove an unrecognized signer runtime")
    for directory, directory_names, file_names in os.walk(
        runtime_root,
        topdown=False,
        followlinks=False,
    ):
        for name in file_names:
            path = Path(directory) / name
            if path.is_symlink():
                path.unlink()
            else:
                path.chmod(0o600)
        for name in directory_names:
            path = Path(directory) / name
            if path.is_symlink():
                path.unlink()
            else:
                path.chmod(0o700)
        Path(directory).chmod(0o700)
    shutil.rmtree(runtime_root)


def _seal_private_signer_output(output_root: Path) -> dict[str, str]:
    if (
        not output_root.is_absolute()
        or output_root.name != PREPARE_SIGNING_OUTPUT_ROOT_NAME
        or output_root.is_symlink()
    ):
        raise PipelineError("prepared signer output path posture is invalid")
    found_file = False
    for directory, directory_names, file_names in os.walk(
        output_root,
        topdown=False,
        followlinks=False,
    ):
        current_directory = Path(directory)
        directory_metadata = current_directory.lstat()
        if (
            not stat.S_ISDIR(directory_metadata.st_mode)
            or directory_metadata.st_uid != os.geteuid()
            or current_directory.is_symlink()
        ):
            raise PipelineError("prepared signer output directory is unsafe")
        for name in directory_names:
            path = current_directory / name
            metadata = path.lstat()
            if path.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
                raise PipelineError(
                    "prepared signer output contains a linked directory"
                )
        for name in file_names:
            found_file = True
            path = current_directory / name
            metadata = path.lstat()
            if (
                path.is_symlink()
                or not stat.S_ISREG(metadata.st_mode)
                or metadata.st_uid != os.geteuid()
                or metadata.st_nlink != 1
            ):
                raise PipelineError(
                    "prepared signer output contains an unsafe file"
                )
            path.chmod(0o400)
        current_directory.chmod(0o500)
    if not found_file:
        raise PipelineError("prepared signer output is empty")

    signer_dll = output_root / PREPARE_SIGNING_DLL_NAME
    runtime_config = output_root / PREPARE_SIGNING_RUNTIME_CONFIG_NAME
    deps = output_root / PREPARE_SIGNING_DEPS_NAME
    sdk_pin = output_root / PREPARE_SIGNING_SDK_PIN_NAME
    for path in (signer_dll, runtime_config, deps, sdk_pin):
        try:
            metadata = path.lstat()
        except OSError as exc:
            raise PipelineError(
                "prepared signer required output is unavailable"
            ) from exc
        if (
            path.is_symlink()
            or not stat.S_ISREG(metadata.st_mode)
            or metadata.st_uid != os.geteuid()
            or metadata.st_nlink != 1
            or stat.S_IMODE(metadata.st_mode) != 0o400
        ):
            raise PipelineError("prepared signer required output is unsafe")
    if (
        sdk_pin.read_bytes() != PREPARE_SIGNING_SDK_PIN_BYTES
        or sha256_file(sdk_pin) != PREPARE_SIGNING_APPROVED_SDK_PIN_SHA256
    ):
        raise PipelineError("prepared signer output SDK pin differs")

    return {
        "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256": sha256_file(deps),
        "CHUMMER_KEYLOCKER_SIGNER_DLL": str(signer_dll),
        "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256": sha256_file(signer_dll),
        "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256": (
            _sha256_canonical_tree(
                output_root.parent,
                output_root.name,
            )
        ),
        "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256": (
            sha256_file(runtime_config)
        ),
    }


def _require_signer_sdk_pin(repo_root: Path, project: Path) -> Path:
    sdk_pin = project.parent / PREPARE_SIGNING_SDK_PIN_NAME
    try:
        normalized = sdk_pin.resolve(strict=True)
        metadata = sdk_pin.lstat()
        content = sdk_pin.read_bytes()
    except OSError as exc:
        raise PipelineError("Linux KeyLocker signer SDK pin is unavailable") from exc
    if (
        normalized != sdk_pin
        or sdk_pin.is_symlink()
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or stat.S_IMODE(metadata.st_mode) & 0o022
        or content != PREPARE_SIGNING_SDK_PIN_BYTES
        or hashlib.sha256(content).hexdigest()
        != PREPARE_SIGNING_APPROVED_SDK_PIN_SHA256
    ):
        raise PipelineError("Linux KeyLocker signer SDK pin differs")
    relative = sdk_pin.relative_to(repo_root).as_posix()
    tracked = _run_clean_git(
        repo_root,
        ["ls-files", "--error-unmatch", "--", relative],
    ).strip()
    if tracked != relative:
        raise PipelineError("Linux KeyLocker signer SDK pin is not tracked")
    return sdk_pin


def prepare_linux_signer_runtime(
    repo_root: Path,
) -> tuple[Path, dict[str, str]]:
    if PREPARE_SIGNING_RUNTIME_IDENTIFIER != "linux-x64":
        raise PipelineError(
            "Linux KeyLocker signer runtime identifier is not fixed"
        )
    _require_root_owned_dotnet_tree()
    project = repo_root / PREPARE_SIGNING_PROJECT_RELATIVE
    require_regular(project, "Linux KeyLocker signer project")
    lock_file = project.with_name("packages.lock.json")
    require_regular(lock_file, "Linux KeyLocker signer package lock")
    _require_signer_sdk_pin(repo_root, project)

    runtime_root = Path(
        tempfile.mkdtemp(prefix="chummer-keylocker-signer-")
    ).resolve(strict=True)
    runtime_root.chmod(0o700)
    output_root = runtime_root / PREPARE_SIGNING_OUTPUT_ROOT_NAME
    output_root.mkdir(mode=0o700)
    build_home = runtime_root / "dotnet-home"
    packages = runtime_root / "packages"
    build_home.mkdir(mode=0o700)
    packages.mkdir(mode=0o700)
    build_environment = {
        "DOTNET_CLI_HOME": str(build_home),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_NOLOGO": "1",
        "DOTNET_ROOT": str(PREPARE_SIGNING_DOTNET_ROOT),
        "HOME": str(build_home),
        "NUGET_PACKAGES": str(packages),
    }
    try:
        run_checked(
            [
                str(PREPARE_SIGNING_DOTNET_BIN),
                "restore",
                str(project),
                "--locked-mode",
                "--runtime",
                PREPARE_SIGNING_RUNTIME_IDENTIFIER,
            ],
            cwd=project.parent,
            environment=build_environment,
            timeout_seconds=SIGNER_BUILD_TIMEOUT_SECONDS,
        )
        _require_signer_sdk_pin(repo_root, project)
        run_checked(
            [
                str(PREPARE_SIGNING_DOTNET_BIN),
                "publish",
                str(project),
                "--configuration",
                "Release",
                "--runtime",
                PREPARE_SIGNING_RUNTIME_IDENTIFIER,
                "--self-contained",
                "false",
                "-p:UseAppHost=false",
                "--no-restore",
                "--output",
                str(output_root),
            ],
            cwd=project.parent,
            environment=build_environment,
            timeout_seconds=SIGNER_BUILD_TIMEOUT_SECONDS,
        )
        _require_signer_sdk_pin(repo_root, project)
        shutil.rmtree(build_home)
        shutil.rmtree(packages)
        signer_environment = _seal_private_signer_output(output_root)
        return runtime_root, signer_environment
    except BaseException:
        _remove_private_signer_runtime(runtime_root)
        raise


def _write_all(descriptor: int, content: bytes | bytearray) -> None:
    view = memoryview(content)
    written = 0
    while written < len(view):
        count = os.write(descriptor, view[written:])
        if count <= 0:
            raise OSError("short signing handoff write")
        written += count


def run_checked(
    command: list[str],
    *,
    cwd: Path,
    environment: dict[str, str],
    timeout_seconds: float,
    secret_handoff: bytearray | None = None,
    maximum_output_bytes: int = MAX_CHILD_OUTPUT_BYTES,
) -> None:
    read_descriptor = -1
    process: subprocess.Popen[bytes] | None = None
    selector: selectors.BaseSelector | None = None
    output_stream: Any = None
    pass_descriptors: tuple[int, ...] = ()
    try:
        if not command or timeout_seconds <= 0 or maximum_output_bytes <= 0:
            raise PipelineError("bounded pipeline command configuration is invalid")
        if PREPARE_SIGNING_HANDOFF_ENVIRONMENT in environment:
            raise PipelineError("signing handoff descriptor cannot be caller supplied")
        for key in FORBIDDEN_SHELL_ENVIRONMENTS:
            if key in environment:
                raise PipelineError(
                    f"forbidden child-initialization environment is set: {key}"
                )

        child_environment = dict(environment)
        if secret_handoff is not None:
            if (
                not secret_handoff
                or len(secret_handoff) > MAX_PREPARE_SIGNING_HANDOFF_BYTES
            ):
                raise PipelineError("prepare signing handoff size is invalid")
            memfd_create = getattr(os, "memfd_create", None)
            required_os_constants = ("MFD_ALLOW_SEALING", "MFD_CLOEXEC")
            required_seal_constants = (
                "F_ADD_SEALS",
                "F_SEAL_GROW",
                "F_SEAL_SEAL",
                "F_SEAL_SHRINK",
                "F_SEAL_WRITE",
            )
            if (
                memfd_create is None
                or any(not hasattr(os, name) for name in required_os_constants)
                or any(not hasattr(fcntl, name) for name in required_seal_constants)
            ):
                raise PipelineError("sealed in-memory signing handoff is unavailable")
            read_descriptor = memfd_create(
                "chummer-windows-signing-handoff",
                flags=os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING,
            )
            os.fchmod(read_descriptor, 0o400)
            _write_all(read_descriptor, secret_handoff)
            os.lseek(read_descriptor, 0, os.SEEK_SET)
            seals = (
                fcntl.F_SEAL_GROW
                | fcntl.F_SEAL_SEAL
                | fcntl.F_SEAL_SHRINK
                | fcntl.F_SEAL_WRITE
            )
            fcntl.fcntl(read_descriptor, fcntl.F_ADD_SEALS, seals)
            os.set_inheritable(read_descriptor, True)
            child_environment[PREPARE_SIGNING_HANDOFF_ENVIRONMENT] = str(
                read_descriptor
            )
            pass_descriptors = (read_descriptor,)

        process = subprocess.Popen(
            command,
            cwd=cwd,
            env=child_environment,
            shell=False,
            start_new_session=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            pass_fds=pass_descriptors,
        )
        if read_descriptor >= 0:
            os.close(read_descriptor)
            read_descriptor = -1

        if process.stdout is None:
            _terminate_process_group(process)
            raise PipelineError("bounded pipeline output pipe is unavailable")
        output_stream = process.stdout
        os.set_blocking(output_stream.fileno(), False)
        selector = selectors.DefaultSelector()
        selector.register(output_stream, selectors.EVENT_READ)
        output_bytes = 0
        output_closed = False
        deadline = time.monotonic() + timeout_seconds
        while not output_closed:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                _terminate_process_group(process)
                raise PipelineError(
                    f"bounded pipeline command timed out: {Path(command[0]).name}"
                )
            events = selector.select(min(remaining, 0.1))
            if not events:
                continue
            for key, _ in events:
                try:
                    chunk = os.read(key.fd, 64 * 1024)
                except BlockingIOError:
                    continue
                if not chunk:
                    selector.unregister(key.fileobj)
                    output_closed = True
                    break
                output_bytes += len(chunk)
                if output_bytes > maximum_output_bytes:
                    _terminate_process_group(process)
                    raise PipelineError(
                        f"bounded pipeline command exceeded its output limit: "
                        f"{Path(command[0]).name}"
                    )
        remaining = max(0.001, deadline - time.monotonic())
        try:
            returncode = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            _terminate_process_group(process)
            raise PipelineError(
                f"bounded pipeline command timed out: {Path(command[0]).name}"
            ) from None
    except PipelineError:
        raise
    except (OSError, subprocess.SubprocessError):
        if process is not None:
            _terminate_process_group(process)
        raise PipelineError(
            f"bounded pipeline command could not complete: {Path(command[0]).name}"
        ) from None
    finally:
        if secret_handoff is not None:
            secret_handoff[:] = b"\x00" * len(secret_handoff)
            secret_handoff.clear()
        if selector is not None:
            try:
                selector.close()
            except OSError:
                pass
        if output_stream is not None:
            try:
                output_stream.close()
            except OSError:
                pass
        if read_descriptor >= 0:
            try:
                os.close(read_descriptor)
            except OSError:
                pass
        if process is not None and process.poll() is None:
            _terminate_process_group(process)
    if returncode != 0:
        raise PipelineError(f"bounded pipeline command failed: {Path(command[0]).name}")


def _run_clean_git(repo_root: Path, arguments: list[str]) -> str:
    try:
        metadata = TRUSTED_GIT_PATH.lstat()
    except OSError as exc:
        raise PipelineError("trusted Git client is unavailable") from exc
    if (
        TRUSTED_GIT_PATH.is_symlink()
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_uid != 0
        or stat.S_IMODE(metadata.st_mode) & 0o022
        or not os.access(TRUSTED_GIT_PATH, os.X_OK)
    ):
        raise PipelineError("trusted Git client posture is invalid")
    try:
        result = subprocess.run(
            [str(TRUSTED_GIT_PATH), *arguments],
            cwd=repo_root,
            env={
                "GIT_CONFIG_NOSYSTEM": "1",
                "LANG": "C",
                "LC_ALL": "C",
                "PATH": "/usr/bin:/bin",
            },
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            check=True,
            timeout=60,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise PipelineError("trusted Git source check failed") from exc
    return result.stdout


def _require_exact_clean_source(repo_root: Path) -> str:
    source_sha = require_commit(
        _run_clean_git(repo_root, ["rev-parse", "HEAD"]).strip(),
        "Presentation source SHA",
    )
    remote_output = _run_clean_git(
        repo_root,
        ["ls-remote", "origin", "refs/heads/main"],
    ).split()
    if not remote_output:
        raise PipelineError("remote main SHA is unavailable")
    remote_sha = require_commit(remote_output[0], "remote main SHA")
    if source_sha != remote_sha:
        raise PipelineError("pipeline must run from the exact remote main commit")
    if _run_clean_git(repo_root, ["status", "--porcelain"]):
        raise PipelineError("pipeline requires a clean Presentation checkout")
    return source_sha


def initialize(
    args: argparse.Namespace,
    client: GitHubClient,
    signing_material: PrepareSigningMaterial | None,
) -> dict[str, Any]:
    if args.state_file.exists() or args.state_file.is_symlink():
        raise PipelineError("new pipeline state output already exists")
    if args.run_prepare != (signing_material is not None):
        raise PipelineError("prepare signing handoff posture differs from invocation")
    repo_root = Path(__file__).resolve().parents[2]
    source_sha = _require_exact_clean_source(repo_root)
    (
        n_minus_one_raw,
        live_release_channel_raw,
        n_minus_one_identity,
    ) = load_n_minus_one_authority(
        args.n_minus_one_release_authority,
        args.live_release_channel_authority,
    )
    if n_minus_one_identity["version"] == args.release_version:
        raise PipelineError("N-1 release version must differ from candidate release")

    if signing_material is None:
        prepare_environment, source_authorities, authority_input_sha = stage_environment(args)
    else:
        prepare_environment, source_authorities, authority_input_sha = prepare_stage_environment(
            args,
            signing_material.public_environment,
        )
    if prepare_environment["CHUMMER_UI_EXPECTED_COMMIT"] != source_sha:
        raise PipelineError("stage Presentation authority differs from coordinator source SHA")
    if Path(prepare_environment["CHUMMER_UI_ROOT"]).resolve(strict=True) != repo_root.resolve(strict=True):
        raise PipelineError("stage Presentation root differs from coordinator checkout")

    require_absolute(args.prepared_stage_root, "prepared stage root")
    if args.run_prepare:
        try:
            run_checked(
                [
                    require_trusted_bash(),
                    "--noprofile",
                    "--norc",
                    str(repo_root / "scripts" / "build-preview-nightly-stage.sh"),
                    "prepare",
                ],
                cwd=repo_root,
                environment=prepare_environment,
                timeout_seconds=args.timeout_seconds,
                secret_handoff=signing_material.handoff if signing_material is not None else None,
            )
        finally:
            if signing_material is not None:
                signing_material.clear()
    if not args.prepared_stage_root.is_dir() or args.prepared_stage_root.is_symlink():
        raise PipelineError("prepared stage root is unavailable after preparation")

    jit_receipt = args.evidence_directory / "PREVIEW_NIGHTLY_JIT_LAUNCH.generated.json"
    if jit_receipt.exists() or jit_receipt.is_symlink():
        raise PipelineError("JIT receipt output already exists")
    run_checked(
        [
            str(repo_root / "scripts" / "run-preview-nightly-jit-launcher.sh"),
            "--prepared-stage-root",
            str(args.prepared_stage_root),
            "--receipt-output",
            str(jit_receipt),
            "--n-minus-one-release-authority",
            str(args.n_minus_one_release_authority),
            "--live-release-channel-authority",
            str(args.live_release_channel_authority),
            "--timeout-seconds",
            str(args.timeout_seconds),
        ],
        cwd=repo_root,
        environment=jit_environment(),
        timeout_seconds=args.timeout_seconds,
    )
    receipt = validate_jit_receipt(
        jit_receipt,
        source_sha,
        (
            n_minus_one_raw,
            live_release_channel_raw,
            n_minus_one_identity,
        ),
    )
    candidate_run = client.run(receipt["runId"], CANDIDATE_WORKFLOW, source_sha, require_success=True)
    candidate_artifact = client.artifact_for_run(
        receipt["runId"], receipt["artifact"]["name"], receipt["artifact"]["id"]
    )
    if require_sha(candidate_artifact.get("digest"), "candidate API digest") != receipt["artifact"]["sha256"]:
        raise PipelineError("candidate API digest differs from JIT receipt")
    candidate_archive = args.evidence_directory / "candidate-original.zip"
    preserved = copy_original_artifact(client, candidate_artifact, candidate_archive)
    members = safe_zip_members(candidate_archive)
    _, inventory_bytes = find_member(members, CANDIDATE_INVENTORY)
    _, export_bytes = find_member(members, CANDIDATE_EXPORT)
    inventory = parse_json_bytes(inventory_bytes, "candidate content inventory")
    export_receipt = parse_json_bytes(export_bytes, "candidate export receipt")
    publication_scope = (
        export_receipt.get("publicationScope")
        if isinstance(export_receipt.get("publicationScope"), dict)
        else {}
    )
    def publication_sha(key: str, label: str) -> str:
        row = publication_scope.get(key)
        return require_sha(
            row.get("sha256") if isinstance(row, dict) else None,
            label,
        )

    publication_bindings = {
        "fullShelfCompatibilityManifestSha256": publication_sha(
            "fullShelfCompatibilityManifest",
            "candidate full-shelf compatibility manifest digest",
        ),
        "fullShelfManifestSha256": publication_sha(
            "fullShelfManifest",
            "candidate full-shelf manifest digest",
        ),
        "publicationScopeSha256": publication_sha(
            "proposal",
            "candidate publication-scope digest",
        ),
        "registryPrepareSha256": require_sha(
            publication_scope.get("registryPrepareSha256"),
            "candidate Registry PREPARE digest",
        ),
        "scopeDecisionSha256": require_sha(
            publication_scope.get("scopeDecisionSha256"),
            "candidate publication-scope decision digest",
        ),
        "signingReceiptSha256": publication_sha(
            "signingReceipt",
            "candidate signing receipt digest",
        ),
    }
    version = str((inventory.get("release") or {}).get("version") or "").strip()
    if (
        version != args.release_version
        or (inventory.get("release") or {}).get("channel") != "preview"
        or require_sha((inventory.get("manifest") or {}).get("sha256"), "candidate manifest digest")
        != receipt["candidate"]["manifestSha256"]
    ):
        raise PipelineError("candidate inventory release/manifest differs from JIT receipt")
    candidate_state = {
        **preserved,
        "actor": require_login((candidate_run.get("actor") or {}).get("login"), "candidate actor"),
        "authenticodeSignerCertificateSha256": receipt["signerAuthority"][
            "certificateSha256"
        ],
        "authenticodeSignerSpkiSha256": receipt["signerAuthority"]["spkiSha256"],
        "artifactId": receipt["artifact"]["id"],
        "artifactName": receipt["artifact"]["name"],
        "artifactSha256": receipt["artifact"]["sha256"],
        "contentInventorySha256": sha256_bytes(inventory_bytes),
        "manifestSha256": receipt["candidate"]["manifestSha256"],
        "liveReleaseChannelSha256": n_minus_one_identity[
            "liveReleaseChannelSha256"
        ],
        "nMinusOneReleaseSha256": n_minus_one_identity["sha256"],
        "selectedTupleSha256": n_minus_one_identity[
            "selectedTupleSha256"
        ],
        **publication_bindings,
        "runAttempt": receipt["runAttempt"],
        "runId": receipt["runId"],
        "version": version,
        "workflow": CANDIDATE_WORKFLOW,
        "workflowId": require_positive_string(
            str(candidate_run.get("workflow_id") or ""), "candidate workflow ID"
        ),
    }
    dispatch_artifact = client.artifact_for_run(
        receipt["runId"],
        receipt["captureDispatchArtifact"]["name"],
        receipt["captureDispatchArtifact"]["id"],
    )
    if (
        require_sha(dispatch_artifact.get("digest"), "capture dispatch artifact API digest")
        != receipt["captureDispatchArtifact"]["sha256"]
    ):
        raise PipelineError("capture dispatch artifact API digest differs from JIT receipt")
    dispatch_archive = args.evidence_directory / "capture-dispatch-original.zip"
    dispatch_preserved = copy_original_artifact(client, dispatch_artifact, dispatch_archive)
    capture_dispatch = validate_capture_dispatch(
        dispatch_archive, candidate=candidate_state, source_sha=source_sha
    )
    state = {
        "candidate": candidate_state,
        "captureDispatch": {
            **dispatch_preserved,
            **capture_dispatch,
        },
        "contractName": STATE_CONTRACT,
        "contractVersion": 1,
        "createdAt": now_iso(),
        "phase": "awaiting_capture",
        "paths": {
            "evidenceDirectory": str(args.evidence_directory),
            "finalizedArchive": str(args.finalized_archive),
            "handoffOutput": str(args.handoff_output),
            "liveReleaseChannelAuthority": str(
                args.live_release_channel_authority
            ),
            "nMinusOneReleaseAuthority": str(args.n_minus_one_release_authority),
            "preparedStageRoot": str(args.prepared_stage_root),
            "provenanceOutput": str(args.provenance_output),
            "reviewRequestOutput": str(args.review_request_output),
            "stageAuthorityInput": str(args.stage_authority_input),
            "stageDir": str(args.stage_dir),
        },
        "release": {
            "channel": "preview",
            "publishedAt": args.published_at,
            "version": args.release_version,
        },
        "repository": REPOSITORY,
        "nMinusOneRelease": n_minus_one_identity,
        "sourceAuthorities": source_authorities,
        "sourceRef": SOURCE_REF,
        "sourceSha": source_sha,
        "stageAuthorityInputSha256": authority_input_sha,
    }
    write_state(args.state_file, state)
    write_provenance(args, state)
    return state


def acquire_capture(args: argparse.Namespace, client: GitHubClient, state: dict[str, Any]) -> dict[str, Any]:
    if state.get("phase") != "awaiting_capture":
        return state
    run = wait_for_capture(
        client,
        run_id=state["captureDispatch"]["runId"],
        sha=state["sourceSha"],
        deadline=time.monotonic() + args.timeout_seconds,
    )
    run_id = str(run["id"])
    attempt = str(run["run_attempt"])
    name = f"windows-native-evidence-{run_id}-{attempt}"
    artifact = client.artifact_for_run(run_id, name)
    archive = args.evidence_directory / "capture-original.zip"
    preserved = copy_original_artifact(client, artifact, archive)
    request = build_review_request(
        capture_run=run,
        capture_artifact=artifact,
        archive=archive,
        source_sha=state["sourceSha"],
        expected_candidate=state["candidate"],
    )
    request_sha = atomic_write(args.review_request_output, request, exclusive=True)
    state["capture"] = {
        **preserved,
        "actor": require_login((run.get("actor") or {}).get("login"), "capture actor"),
        "inventorySha256": request["capture"]["inventorySha256"],
        "reviewRequestPath": str(args.review_request_output),
        "reviewRequestSha256": request_sha,
        "runAttempt": attempt,
        "runId": run_id,
        "workflow": CAPTURE_WORKFLOW,
        "workflowId": require_positive_string(
            str(run.get("workflow_id") or ""), "capture workflow ID"
        ),
    }
    state["phase"] = "action_required_human_review"
    write_state(args.state_file, state)
    write_provenance(args, state)
    return state


def request_finalization(args: argparse.Namespace, client: GitHubClient, state: dict[str, Any]) -> dict[str, Any]:
    if state.get("phase") != "action_required_human_review":
        return state
    if args.review_input is None:
        raise ActionRequired(
            f"review exact capture artifact, then resume with --review-input {args.review_request_output} companion input"
        )
    request = load_json(require_regular(args.review_request_output, "human review request"), "human review request")
    if sha256_file(args.review_request_output) != state["capture"]["reviewRequestSha256"]:
        raise PipelineError("human review request bytes changed after the action-required boundary")
    user = client.json("user")
    login = require_login(user.get("login"), "authenticated GitHub operator")
    review = validate_review_input(
        args.review_input,
        request=request,
        request_sha=state["capture"]["reviewRequestSha256"],
        authenticated_login=login,
    )
    run_id = dispatch_finalization(client, review)
    state["finalization"] = {
        "dispatchActor": login,
        "reviewInputPath": str(args.review_input),
        "reviewInputSha256": sha256_file(args.review_input),
        "reviewer": review["reviewer"],
        "runId": run_id,
        "workflow": FINALIZATION_WORKFLOW,
    }
    state["phase"] = "awaiting_finalization"
    write_state(args.state_file, state)
    write_provenance(args, state)
    return state


def acquire_finalization(args: argparse.Namespace, client: GitHubClient, state: dict[str, Any]) -> dict[str, Any]:
    if state.get("phase") != "awaiting_finalization":
        return state
    run_id = state["finalization"]["runId"]
    run = wait_for_exact_run(
        client,
        run_id=run_id,
        workflow=FINALIZATION_WORKFLOW,
        sha=state["sourceSha"],
        deadline=time.monotonic() + args.timeout_seconds,
    )
    attempt = str(run["run_attempt"])
    name = f"windows-native-evidence-finalized-{run_id}-{attempt}"
    artifact = client.artifact_for_run(run_id, name)
    preserved = copy_original_artifact(client, artifact, args.finalized_archive)
    members = safe_zip_members(args.finalized_archive)
    _, receipt_bytes = find_member(members, FINALIZATION_RECEIPT)
    receipt = parse_json_bytes(receipt_bytes, "native finalization receipt")
    reviewer = require_login(receipt.get("reviewer"), "finalization reviewer")
    run_actor = require_login((run.get("actor") or {}).get("login"), "finalization run actor")
    if reviewer.casefold() != run_actor.casefold() or reviewer.casefold() != state["finalization"]["reviewer"].casefold():
        raise PipelineError("finalization reviewer differs from authenticated workflow actor/input")
    if receipt.get("humanReviewConfirmed") is not True or receipt.get("reviewerWasCaptureActor") is not False:
        raise PipelineError("finalization receipt does not preserve independent human review")
    state["finalization"].update(
        {
            **preserved,
            "finalizationReceiptSha256": sha256_bytes(receipt_bytes),
            "runAttempt": attempt,
            "workflowId": require_positive_string(
                str(run.get("workflow_id") or ""), "finalization workflow ID"
            ),
        }
    )
    state["phase"] = "evidence_preserved"
    write_state(args.state_file, state)
    write_provenance(args, state)
    return state


def validate_sealed_native_evidence(
    seal_path: Path, seal: dict[str, Any], state: dict[str, Any]
) -> dict[str, Any]:
    stage = seal.get("stage") if isinstance(seal.get("stage"), dict) else {}
    files = stage.get("files") if isinstance(stage.get("files"), list) else []
    rows = [
        row
        for row in files
        if isinstance(row, dict) and row.get("path") == NATIVE_EVIDENCE_RECEIPT
    ]
    if len(rows) != 1 or set(rows[0]) != {"path", "sha256", "sizeBytes"}:
        raise PipelineError("sealed stage does not inventory exact native evidence receipt")
    content = read_regular_bytes(
        seal_path.parent / NATIVE_EVIDENCE_RECEIPT,
        "sealed native evidence receipt",
        maximum_bytes=MAX_SEALED_RECEIPT_BYTES,
    )
    if rows[0]["sha256"] != sha256_bytes(content) or rows[0]["sizeBytes"] != len(content):
        raise PipelineError("sealed native evidence receipt differs from stage inventory")
    native = parse_json_bytes(content, "sealed native evidence receipt")
    if (
        native.get("contractName") != NATIVE_EVIDENCE_CONTRACT
        or native.get("contractVersion") != 1
        or native.get("status") != "passed"
    ):
        raise PipelineError("sealed native evidence receipt contract/status differs")
    proof = seal.get("proof") if isinstance(seal.get("proof"), dict) else {}
    if require_sha(native.get("treeSha256"), "native evidence tree digest") != require_sha(
        proof.get("nativeWindowsEvidenceTreeSha256"), "sealed native evidence tree digest"
    ):
        raise PipelineError("sealed native evidence tree digest differs")

    capture = state.get("capture") if isinstance(state.get("capture"), dict) else {}
    expected_capture_source = {
        "repository": REPOSITORY,
        "workflow": CAPTURE_WORKFLOW,
        "runId": capture.get("runId"),
        "runAttempt": capture.get("runAttempt"),
        "ref": SOURCE_REF,
        "sha": state.get("sourceSha"),
        "actor": capture.get("actor"),
        "triggeringActor": capture.get("actor"),
        "rerunPolicy": "same-actor-only",
        "artifactName": capture.get("artifactName"),
    }
    if (
        native.get("captureSource") != expected_capture_source
        or require_sha(native.get("captureInventorySha256"), "sealed capture inventory digest")
        != require_sha(capture.get("inventorySha256"), "coordinator capture inventory digest")
    ):
        raise PipelineError("sealed native capture differs from coordinator state")

    finalization = (
        state.get("finalization") if isinstance(state.get("finalization"), dict) else {}
    )
    expected_finalization_source = {
        "repository": REPOSITORY,
        "workflow": FINALIZATION_WORKFLOW,
        "runId": finalization.get("runId"),
        "runAttempt": finalization.get("runAttempt"),
        "ref": SOURCE_REF,
        "sha": state.get("sourceSha"),
        "actor": finalization.get("reviewer"),
        "triggeringActor": finalization.get("reviewer"),
        "rerunPolicy": "same-actor-only",
        "artifactName": finalization.get("artifactName"),
    }
    if native.get("finalizationSource") != expected_finalization_source:
        raise PipelineError("sealed native finalization run differs from coordinator state")
    if require_sha(native.get("archiveSha256"), "sealed finalized archive digest") != require_sha(
        finalization.get("archiveSha256"), "coordinator finalized archive digest"
    ):
        raise PipelineError("sealed finalized archive differs from coordinator state")
    if require_sha(
        native.get("finalizationSha256"), "sealed finalization receipt digest"
    ) != require_sha(
        finalization.get("finalizationReceiptSha256"),
        "coordinator finalization receipt digest",
    ):
        raise PipelineError("sealed finalization receipt differs from coordinator state")
    reviewers = native.get("visualReviewers")
    if reviewers != {
        head: finalization.get("reviewer") for head in PROMOTED_WINDOWS_HEADS
    }:
        raise PipelineError("sealed visual reviewer differs from coordinator state")
    return native


def validate_seal_against_state(seal_path: Path, state: dict[str, Any]) -> dict[str, Any]:
    seal = load_json(require_regular(seal_path, "sealed-stage receipt"), "sealed-stage receipt")
    if (
        seal.get("contractName") != "chummer6-ui.preview-nightly-stage"
        or seal.get("contractVersion") != 1
        or seal.get("status") != "sealed"
    ):
        raise PipelineError("sealed-stage receipt contract/status differs")
    if seal.get("release") != state.get("release"):
        raise PipelineError("sealed-stage release identity differs from coordinator state")
    if seal.get("sourceAuthorities") != state.get("sourceAuthorities"):
        raise PipelineError("sealed-stage source authorities differ from coordinator state")
    proof = seal.get("proof") if isinstance(seal.get("proof"), dict) else {}
    if require_sha(proof.get("canonicalManifestSha256"), "sealed canonical manifest digest") != state[
        "candidate"
    ]["manifestSha256"]:
        raise PipelineError("sealed-stage manifest differs from coordinator candidate")
    producer = (
        proof.get("candidateProducerProvenance")
        if isinstance(proof.get("candidateProducerProvenance"), dict)
        else {}
    )
    sealed_candidate = producer.get("candidate") if isinstance(producer.get("candidate"), dict) else {}
    expected_candidate = {
        "repository": REPOSITORY,
        "workflow": CANDIDATE_WORKFLOW,
        "runId": state["candidate"]["runId"],
        "runAttempt": state["candidate"]["runAttempt"],
        "ref": SOURCE_REF,
        "sha": state["sourceSha"],
        "actor": state["candidate"]["actor"],
        "artifactId": state["candidate"]["artifactId"],
        "artifactName": state["candidate"]["artifactName"],
        "artifactSha256": state["candidate"]["artifactSha256"],
        "manifestSha256": state["candidate"]["manifestSha256"],
        "contentInventorySha256": state["candidate"]["contentInventorySha256"],
    }
    if any(sealed_candidate.get(key) != value for key, value in expected_candidate.items()):
        raise PipelineError("sealed-stage candidate run/artifact/inventory differs from coordinator state")
    validate_sealed_native_evidence(seal_path, seal, state)
    upload = seal.get("uploadBoundary") if isinstance(seal.get("uploadBoundary"), dict) else {}
    if (
        upload.get("uploadAuthorized") is not False
        or upload.get("postUploadHandoffEmitted") is not False
        or upload.get("producerMode") != "stage_only"
    ):
        raise PipelineError("sealed-stage receipt crossed the non-publication boundary")
    return seal


def seal_and_handoff(args: argparse.Namespace, state: dict[str, Any]) -> dict[str, Any]:
    if state.get("phase") != "evidence_preserved":
        return state
    repo_root = Path(__file__).resolve().parents[2]
    environment, source_authorities, authority_input_sha = stage_environment(args)
    if (
        source_authorities != state.get("sourceAuthorities")
        or authority_input_sha != state.get("stageAuthorityInputSha256")
    ):
        raise PipelineError("stage authority input changed before seal")
    environment["CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE"] = str(args.finalized_archive)
    run_checked(
        [
            require_trusted_bash(),
            str(repo_root / "scripts" / "build-preview-nightly-stage.sh"),
            "seal",
        ],
        cwd=repo_root,
        environment=environment,
        timeout_seconds=args.timeout_seconds,
    )
    seal_path = args.stage_dir / STAGE_SEAL
    seal = validate_seal_against_state(seal_path, state)
    state["sealedStage"] = {
        "candidateContentInventorySha256": state["candidate"]["contentInventorySha256"],
        "manifestSha256": state["candidate"]["manifestSha256"],
        "path": str(args.stage_dir),
        "release": seal["release"],
        "sealPath": str(seal_path),
        "sealSha256": sha256_file(seal_path),
        "sourceAuthorities": seal["sourceAuthorities"],
        "uploadAuthorized": False,
    }
    state["phase"] = "sealed_non_publishing_handoff"
    state["handoff"] = {
        "contractName": HANDOFF_CONTRACT,
        "path": str(args.handoff_output),
        "sha256": None,
    }
    provenance_sha = write_provenance(args, state)
    handoff = build_publication_handoff(args, state, provenance_sha)
    handoff_sha = atomic_write(args.handoff_output, handoff, exclusive=True)
    state["handoff"] = {
        "contractName": HANDOFF_CONTRACT,
        "path": str(args.handoff_output),
        "sha256": handoff_sha,
    }
    write_state(args.state_file, state)
    return state


def validate_invocation_paths(args: argparse.Namespace, state: dict[str, Any]) -> None:
    claimed = state.get("paths")
    expected = {
        "evidenceDirectory": str(args.evidence_directory),
        "finalizedArchive": str(args.finalized_archive),
        "handoffOutput": str(args.handoff_output),
        "liveReleaseChannelAuthority": str(
            args.live_release_channel_authority
        ),
        "nMinusOneReleaseAuthority": str(args.n_minus_one_release_authority),
        "preparedStageRoot": str(args.prepared_stage_root),
        "provenanceOutput": str(args.provenance_output),
        "reviewRequestOutput": str(args.review_request_output),
        "stageAuthorityInput": str(args.stage_authority_input),
        "stageDir": str(args.stage_dir),
    }
    if claimed != expected:
        raise PipelineError("resume paths differ from the integrity-bound pipeline state")
    expected_release = {
        "channel": "preview",
        "publishedAt": args.published_at,
        "version": args.release_version,
    }
    if state.get("release") != expected_release:
        raise PipelineError("resume release identity differs from the integrity-bound pipeline state")
    _raw, _live_raw, n_minus_one_identity = load_n_minus_one_authority(
        args.n_minus_one_release_authority,
        args.live_release_channel_authority,
    )
    if state.get("nMinusOneRelease") != n_minus_one_identity:
        raise PipelineError("resume N-1 release authority changed")
    _, source_authorities, authority_sha = stage_environment(args)
    if (
        state.get("sourceAuthorities") != source_authorities
        or state.get("stageAuthorityInputSha256") != authority_sha
    ):
        raise PipelineError("resume stage authority differs from the integrity-bound pipeline state")


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--state-file", required=True, type=Path)
    parser.add_argument("--evidence-directory", required=True, type=Path)
    parser.add_argument("--prepared-stage-root", required=True, type=Path)
    parser.add_argument("--stage-dir", required=True, type=Path)
    parser.add_argument("--provenance-output", required=True, type=Path)
    parser.add_argument("--review-request-output", required=True, type=Path)
    parser.add_argument("--handoff-output", required=True, type=Path)
    parser.add_argument("--finalized-archive", required=True, type=Path)
    parser.add_argument("--stage-authority-input", required=True, type=Path)
    parser.add_argument(
        "--n-minus-one-release-authority", required=True, type=Path
    )
    parser.add_argument(
        "--live-release-channel-authority", required=True, type=Path
    )
    parser.add_argument("--release-version", required=True)
    parser.add_argument("--published-at", required=True)
    parser.add_argument("--review-input", type=Path)
    parser.add_argument("--run-prepare", action="store_true")
    parser.add_argument("--timeout-seconds", type=int, default=3600)
    args = parser.parse_args(argv)
    for name in (
        "state_file",
        "evidence_directory",
        "prepared_stage_root",
        "stage_dir",
        "provenance_output",
        "review_request_output",
        "handoff_output",
        "finalized_archive",
        "stage_authority_input",
        "live_release_channel_authority",
        "n_minus_one_release_authority",
    ):
        require_absolute(getattr(args, name), name.replace("_", " "))
    if args.review_input is not None:
        require_absolute(args.review_input, "review input")
    if not PORTABLE_VERSION_RE.fullmatch(args.release_version):
        parser.error("--release-version must be a portable explicit version")
    published_at = parse_utc(args.published_at, "published-at")
    if args.published_at != published_at.replace(microsecond=0).isoformat().replace("+00:00", "Z"):
        parser.error("--published-at must be canonical whole-second UTC RFC3339")
    if not 60 <= args.timeout_seconds <= 7200:
        parser.error("--timeout-seconds must be between 60 and 7200")
    return args


def main(argv: list[str] | None = None) -> int:
    signing_material: PrepareSigningMaterial | None = None
    unowned_runtime_root: Path | None = None
    try:
        args = parse_args(argv)
        new_pipeline = not args.state_file.exists() and not args.state_file.is_symlink()
        if new_pipeline and args.run_prepare:
            _reject_unsafe_prepare_environment_names(os.environ)
            repo_root = Path(__file__).resolve().parents[2]
            _require_exact_clean_source(repo_root)
            (
                unowned_runtime_root,
                signer_environment,
            ) = prepare_linux_signer_runtime(repo_root)
            signing_material = capture_prepare_signing_material(
                consume=True,
                signer_runtime_root=unowned_runtime_root,
                signer_environment=signer_environment,
            )
            unowned_runtime_root = None
        else:
            reject_ambient_signing_environment()
        args.evidence_directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        if args.evidence_directory.is_symlink():
            raise PipelineError("evidence directory must not be a symlink")
        client = GitHubClient()
        state = (
            load_state(args.state_file)
            if args.state_file.exists()
            else initialize(args, client, signing_material)
        )
        validate_invocation_paths(args, state)
        state = acquire_capture(args, client, state)
        state = request_finalization(args, client, state)
        state = acquire_finalization(args, client, state)
        state = seal_and_handoff(args, state)
    except ActionRequired as exc:
        print(f"preview-nightly-pipeline:action-required: {exc}", file=sys.stderr)
        return 3
    except (PipelineError, OSError, subprocess.SubprocessError) as exc:
        print(f"preview-nightly-pipeline:error: {exc}", file=sys.stderr)
        return 2
    finally:
        if signing_material is not None:
            signing_material.clear()
        if unowned_runtime_root is not None:
            _remove_private_signer_runtime(unowned_runtime_root)
    print(f"preview-nightly-pipeline:phase={state['phase']}")
    print(f"preview-nightly-pipeline:state={args.state_file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
