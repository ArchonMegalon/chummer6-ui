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
import hashlib
import json
import os
import re
import shutil
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
FINALIZATION_RECEIPT = "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
CANDIDATE_INVENTORY = "PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json"
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


class PipelineError(ValueError):
    pass


class ActionRequired(PipelineError):
    pass


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
    for command in ("bash", "dotnet", "git", "python3"):
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


def validate_jit_receipt(path: Path, expected_sha: str) -> dict[str, Any]:
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
        "status",
    }:
        raise PipelineError("capture dispatch receipt has missing or extra fields")
    if (
        receipt.get("contractName") != "chummer6-ui.preview-nightly-capture-dispatch"
        or receipt.get("contractVersion") != 1
        or receipt.get("status") != "dispatched"
    ):
        raise PipelineError("capture dispatch receipt contract/status differs")
    expected_handoff = {
        "actor": candidate["actor"],
        "artifactId": candidate["artifactId"],
        "artifactName": candidate["artifactName"],
        "artifactSha256": candidate["artifactSha256"],
        "contentInventorySha256": candidate["contentInventorySha256"],
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 1,
        "ref": SOURCE_REF,
        "repository": REPOSITORY,
        "runAttempt": candidate["runAttempt"],
        "runId": candidate["runId"],
        "sha": source_sha,
        "workflow": CANDIDATE_WORKFLOW,
    }
    if receipt.get("candidateHandoff") != expected_handoff:
        raise PipelineError("capture dispatch candidate run/artifact/inventory correlation differs")
    if require_sha(receipt.get("candidateHandoffSha256"), "candidate handoff digest") != sha256_bytes(
        canonical_bytes(expected_handoff)
    ):
        raise PipelineError("capture dispatch candidate handoff digest differs")
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
    if capture.get("contractName") != "chummer6-ui.preview-nightly-native-windows-capture":
        raise PipelineError("native capture receipt contract differs")
    if (
        inventory.get("contractName") != "chummer6-ui.preview-nightly-native-windows-capture-inventory"
        or inventory.get("contractVersion") != 1
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
    candidate_binding = capture.get("candidate") if isinstance(capture.get("candidate"), dict) else {}
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
        "contractVersion": 1,
        "generatedAt": now_iso(),
        "humanReviewConfirmed": False,
        "requiredChecks": ["readability", "contrast", "clipping"],
        "requiredHeads": list(PROMOTED_WINDOWS_HEADS),
        "screenshots": screenshot_rows,
        "status": "action_required",
        "warning": "A protected, allowlisted human must inspect the exact named artifact. This request is not review evidence.",
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
    }
    if set(review) != expected_keys:
        raise PipelineError("human review input has missing or extra fields")
    if review.get("contractName") != REVIEW_INPUT_CONTRACT or review.get("contractVersion") != 1:
        raise PipelineError("human review input contract is invalid")
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


def run_checked(command: list[str], *, cwd: Path, environment: dict[str, str] | None = None) -> None:
    completed = subprocess.run(command, cwd=cwd, env=environment, check=False)
    if completed.returncode != 0:
        raise PipelineError(f"bounded pipeline command failed: {Path(command[0]).name}")


def initialize(args: argparse.Namespace, client: GitHubClient) -> dict[str, Any]:
    if args.state_file.exists() or args.state_file.is_symlink():
        raise PipelineError("new pipeline state output already exists")
    repo_root = Path(__file__).resolve().parents[2]
    source_sha = require_commit(
        subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=repo_root, text=True, stdout=subprocess.PIPE, check=True
        ).stdout.strip(),
        "Presentation source SHA",
    )
    remote_sha = require_commit(
        subprocess.run(
            ["git", "ls-remote", "origin", "refs/heads/main"],
            cwd=repo_root,
            text=True,
            stdout=subprocess.PIPE,
            check=True,
        ).stdout.split()[0],
        "remote main SHA",
    )
    if source_sha != remote_sha:
        raise PipelineError("pipeline must run from the exact remote main commit")
    if subprocess.run(["git", "status", "--porcelain"], cwd=repo_root, text=True, stdout=subprocess.PIPE, check=True).stdout:
        raise PipelineError("pipeline requires a clean Presentation checkout")

    prepare_environment, source_authorities, authority_input_sha = stage_environment(args)
    if prepare_environment["CHUMMER_UI_EXPECTED_COMMIT"] != source_sha:
        raise PipelineError("stage Presentation authority differs from coordinator source SHA")
    if Path(prepare_environment["CHUMMER_UI_ROOT"]).resolve(strict=True) != repo_root.resolve(strict=True):
        raise PipelineError("stage Presentation root differs from coordinator checkout")

    require_absolute(args.prepared_stage_root, "prepared stage root")
    if args.run_prepare:
        run_checked(
            ["bash", str(repo_root / "scripts" / "build-preview-nightly-stage.sh"), "prepare"],
            cwd=repo_root,
            environment=prepare_environment,
        )
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
            "--timeout-seconds",
            str(args.timeout_seconds),
        ],
        cwd=repo_root,
    )
    receipt = validate_jit_receipt(jit_receipt, source_sha)
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
    inventory = parse_json_bytes(inventory_bytes, "candidate content inventory")
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
        "artifactId": receipt["artifact"]["id"],
        "artifactName": receipt["artifact"]["name"],
        "artifactSha256": receipt["artifact"]["sha256"],
        "contentInventorySha256": sha256_bytes(inventory_bytes),
        "manifestSha256": receipt["candidate"]["manifestSha256"],
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
        ["bash", str(repo_root / "scripts" / "build-preview-nightly-stage.sh"), "seal"],
        cwd=repo_root,
        environment=environment,
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
    try:
        args = parse_args(argv)
        args.evidence_directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        if args.evidence_directory.is_symlink():
            raise PipelineError("evidence directory must not be a symlink")
        client = GitHubClient()
        state = load_state(args.state_file) if args.state_file.exists() else initialize(args, client)
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
    print(f"preview-nightly-pipeline:phase={state['phase']}")
    print(f"preview-nightly-pipeline:state={args.state_file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
