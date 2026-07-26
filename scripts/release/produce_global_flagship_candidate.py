#!/usr/bin/env python3
"""Produce one provider-authenticated, nonpublishing flagship candidate.

The command accepts exact GitHub Actions artifact identities for the Windows,
Linux, and macOS native lanes.  It authenticates their provider metadata,
downloads each archive by numeric artifact ID, materializes a bounded
candidate tree, opens the macOS encrypted custody artifact, invokes the
existing flagship assembler's ``propose`` operation, and then performs a late
provider reauthentication before making the output read-only.

It has no publishing, signing, notarization, release, deployment, or channel
mutation operation.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Mapping, Protocol, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
if str(REPO_ROOT / "scripts") not in sys.path:
    sys.path.insert(0, str(REPO_ROOT / "scripts"))

import assemble_global_flagship_release as assembler  # noqa: E402
import desktop_native_lifecycle_evidence as desktop_lifecycle  # noqa: E402
import linux_deb_signing  # noqa: E402


SOURCE_REPOSITORY = "ArchonMegalon/chummer6-ui"
SOURCE_BRANCH = "main"
SOURCE_REF = "refs/heads/main"
PRODUCER_WORKFLOW = ".github/workflows/global-flagship-candidate.yml"
PROVIDER_CONTRACT = "chummer6-ui.global-flagship-candidate-provider-inputs.v1"
REAUTH_CONTRACT = "chummer6-ui.global-flagship-candidate-provider-reauth.v1"
API_ROOT = "https://api.github.com"
API_VERSION = "2022-11-28"
USER_AGENT = "chummer6-global-flagship-candidate-producer/1"
HTTP_TIMEOUT_SECONDS = 60
MAX_API_JSON_BYTES = 16 * 1024 * 1024
MAX_ARCHIVE_BYTES = 4 * 1024 * 1024 * 1024
MAX_EXPANDED_BYTES = 6 * 1024 * 1024 * 1024
MAX_ENTRY_BYTES = 2 * 1024 * 1024 * 1024
MAX_ARCHIVE_ENTRIES = 20_000
MAX_COMPRESSION_RATIO = 200
MAX_JSON_BYTES = 32 * 1024 * 1024
MAX_EXACT_INTEGER = 9_007_199_254_740_991
ARTIFACT_DIGEST_RE = re.compile(r"^sha256:([0-9a-f]{64})$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
LOGIN_RE = re.compile(
    r"^(?:github-actions\[bot\]|"
    r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$"
)
PORTABLE_SEGMENT_RE = re.compile(r"^[A-Za-z0-9._+@-]{1,255}$")


class ContractError(RuntimeError):
    """An input cannot support a global flagship candidate claim."""


def fail(message: str) -> None:
    raise ContractError(message)


@dataclass(frozen=True)
class ApiResponse:
    value: Any
    headers: Mapping[str, str]


class ProviderReader(Protocol):
    def get_json(self, path: str) -> ApiResponse: ...

    def download_artifact(
        self, artifact_id: int, output: Path, maximum_bytes: int
    ) -> tuple[str, int]: ...


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(
        self,
        req: urllib.request.Request,
        fp: Any,
        code: int,
        msg: str,
        headers: Any,
        newurl: str,
    ) -> None:
        return None


class GitHubApi:
    """Minimal GET-only GitHub client with a single safe artifact redirect."""

    def __init__(self, token: str) -> None:
        if (
            not token
            or len(token) > 4096
            or any(character in token for character in ("\r", "\n", "\x00"))
        ):
            fail("GitHub token is missing or malformed")
        self._token = token
        self._api = urllib.request.build_opener(_NoRedirect())
        self._storage = urllib.request.build_opener(_NoRedirect())

    def _request(self, path: str) -> urllib.request.Request:
        if not path.startswith("/") or "://" in path or "#" in path:
            fail("internal GitHub API path is invalid")
        return urllib.request.Request(
            API_ROOT + path,
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self._token}",
                "User-Agent": USER_AGENT,
                "X-GitHub-Api-Version": API_VERSION,
            },
            method="GET",
        )

    @staticmethod
    def _bounded_read(response: Any, maximum: int, label: str) -> bytes:
        advertised = response.headers.get("Content-Length")
        if advertised is not None:
            try:
                size = int(advertised)
            except ValueError:
                fail(f"{label} has a malformed Content-Length")
            if size < 0 or size > maximum:
                fail(f"{label} exceeds its byte boundary")
        data = response.read(maximum + 1)
        if len(data) > maximum:
            fail(f"{label} exceeds its byte boundary")
        return data

    def get_json(self, path: str) -> ApiResponse:
        request = self._request(path)
        try:
            with self._api.open(
                request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                if response.status != 200 or response.geturl() != request.full_url:
                    fail("GitHub API returned an unexpected status or redirect")
                if response.headers.get_content_type() not in {
                    "application/json",
                    "application/vnd.github+json",
                }:
                    fail("GitHub API returned an unexpected JSON media type")
                raw = self._bounded_read(
                    response, MAX_API_JSON_BYTES, "GitHub API response"
                )
                headers = dict(response.headers.items())
        except ContractError:
            raise
        except urllib.error.HTTPError as exc:
            fail(f"GitHub API read failed closed with HTTP {exc.code}")
        except (urllib.error.URLError, TimeoutError, OSError):
            fail("GitHub API read failed closed")
        return ApiResponse(
            parse_json_bytes(raw, "GitHub API response"), headers
        )

    def download_artifact(
        self, artifact_id: int, output: Path, maximum_bytes: int
    ) -> tuple[str, int]:
        positive_integer(artifact_id, "artifact download ID")
        request = self._request(
            f"/repos/{SOURCE_REPOSITORY}/actions/artifacts/{artifact_id}/zip"
        )
        try:
            self._api.open(request, timeout=HTTP_TIMEOUT_SECONDS)
            fail("artifact endpoint did not return its documented redirect")
        except urllib.error.HTTPError as exc:
            if exc.code != 302:
                fail(
                    "artifact download endpoint failed closed with "
                    f"HTTP {exc.code}"
                )
            location = exc.headers.get("Location")
        except ContractError:
            raise
        except (urllib.error.URLError, TimeoutError, OSError):
            fail("artifact download endpoint failed closed")
        if not location:
            fail("artifact download endpoint omitted its redirect")
        storage_url = validate_artifact_redirect(location)
        storage_request = urllib.request.Request(
            storage_url,
            headers={
                "Accept": "application/octet-stream",
                "User-Agent": USER_AGENT,
            },
            method="GET",
        )
        descriptor = -1
        digest = hashlib.sha256()
        size = 0
        try:
            descriptor = os.open(
                output,
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | int(getattr(os, "O_NOFOLLOW", 0)),
                0o600,
            )
            with self._storage.open(
                storage_request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                if response.status != 200 or response.geturl() != storage_url:
                    fail(
                        "artifact storage returned an unexpected status or "
                        "additional redirect"
                    )
                if response.headers.get("Location"):
                    fail("artifact storage attempted a second redirect")
                advertised = response.headers.get("Content-Length")
                if advertised is not None:
                    try:
                        advertised_size = int(advertised)
                    except ValueError:
                        fail("artifact storage has a malformed Content-Length")
                    if advertised_size < 1 or advertised_size > maximum_bytes:
                        fail("artifact archive exceeds its byte boundary")
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    size += len(chunk)
                    if size > maximum_bytes:
                        fail("artifact archive exceeds its byte boundary")
                    digest.update(chunk)
                    write_all(descriptor, chunk)
            if size < 1:
                fail("artifact archive is empty")
            os.fsync(descriptor)
        except ContractError:
            try:
                output.unlink()
            except FileNotFoundError:
                pass
            raise
        except urllib.error.HTTPError as exc:
            try:
                output.unlink()
            except FileNotFoundError:
                pass
            fail(f"artifact storage read failed closed with HTTP {exc.code}")
        except (urllib.error.URLError, TimeoutError, OSError):
            try:
                output.unlink()
            except FileNotFoundError:
                pass
            fail("artifact storage read failed closed")
        finally:
            if descriptor >= 0:
                os.close(descriptor)
        return digest.hexdigest(), size


@dataclass(frozen=True)
class ArtifactSpec:
    role: str
    artifact_id: int
    name: str
    digest: str
    workflow_path: str
    name_prefix: str
    platform: str


@dataclass(frozen=True)
class AuthenticatedArtifact:
    spec: ArtifactSpec
    metadata: Mapping[str, Any]
    run: Mapping[str, Any]
    workflow: Mapping[str, Any]
    actor: Mapping[str, Any]
    workflow_blob: Mapping[str, Any]
    archive_path: Path

    def projection(
        self, candidate_root: Path, archive_path: Path | None = None
    ) -> dict[str, Any]:
        return {
            "role": self.spec.role,
            "platform": self.spec.platform,
            "artifact": dict(self.metadata),
            "run": dict(self.run),
            "workflow": dict(self.workflow),
            "actor": dict(self.actor),
            "workflowBlob": dict(self.workflow_blob),
            "archive": file_reference(
                candidate_root,
                self.archive_path if archive_path is None else archive_path,
            ),
        }


ROLE_POLICIES: Mapping[str, tuple[str, str, str]] = {
    "windows-export": (
        ".github/workflows/preview-nightly-candidate-export.yml",
        "preview-nightly-candidate",
        "windows",
    ),
    "windows-capture": (
        ".github/workflows/windows-native-evidence-capture.yml",
        "windows-native-evidence",
        "windows",
    ),
    "windows-evidence": (
        ".github/workflows/windows-native-evidence-finalize.yml",
        "windows-native-evidence-finalized",
        "windows",
    ),
    "linux-export": (
        ".github/workflows/linux-native-candidate-export.yml",
        "linux-native-candidate",
        "linux",
    ),
    "linux-evidence": (
        ".github/workflows/linux-native-lifecycle-evidence.yml",
        "linux-native-lifecycle",
        "linux",
    ),
    "macos-escrow": (
        ".github/workflows/macos-flagship-evidence.yml",
        "macos-flagship-encrypted-escrow",
        "macos",
    ),
    "macos-handoff": (
        ".github/workflows/macos-flagship-evidence.yml",
        "macos-flagship-handoff",
        "macos",
    ),
}


def write_all(descriptor: int, data: bytes) -> None:
    offset = 0
    while offset < len(data):
        written = os.write(descriptor, data[offset:])
        if written < 1:
            fail("file write made no forward progress")
        offset += written


def validate_artifact_redirect(value: str) -> str:
    parsed = urllib.parse.urlsplit(value)
    try:
        port = parsed.port
    except ValueError:
        fail("artifact redirect has a malformed port")
    if (
        parsed.scheme != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.fragment
        or port not in {None, 443}
    ):
        fail("artifact redirect is not a credential-free HTTPS URL")
    host = parsed.hostname.casefold()
    if not (
        host == "objects.githubusercontent.com"
        or host.endswith(".blob.core.windows.net")
    ):
        fail("artifact redirect targets an unapproved storage host")
    return value


def duplicate_rejecting_object(
    pairs: list[tuple[str, Any]],
) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            fail(f"JSON contains duplicate key {key!r}")
        value[key] = item
    return value


def parse_json_bytes(raw: bytes, label: str) -> Any:
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        fail(f"{label} is not UTF-8")
    try:
        return json.loads(
            text,
            object_pairs_hook=duplicate_rejecting_object,
            parse_constant=lambda value: fail(
                f"{label} contains non-finite number {value!r}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")


def load_json(path: Path, label: str) -> dict[str, Any]:
    before = path.stat(follow_symlinks=False)
    if (
        not stat.S_ISREG(before.st_mode)
        or before.st_size < 2
        or before.st_size > MAX_JSON_BYTES
    ):
        fail(f"{label} is not a bounded regular file")
    with path.open("rb") as stream:
        raw = stream.read(MAX_JSON_BYTES + 1)
    after = path.stat(follow_symlinks=False)
    if (
        len(raw) > MAX_JSON_BYTES
        or (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
            before.st_ctime_ns,
        )
        != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
            after.st_ctime_ns,
        )
    ):
        fail(f"{label} changed while it was read")
    value = parse_json_bytes(raw, label)
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def mapping(value: Any, label: str) -> Mapping[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    return value


def sequence(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        fail(f"{label} must be an array")
    return value


def string(
    value: Any, label: str, pattern: re.Pattern[str] | None = None
) -> str:
    if (
        not isinstance(value, str)
        or not value
        or len(value) > 4096
        or any(ord(character) < 32 for character in value)
        or (pattern is not None and pattern.fullmatch(value) is None)
    ):
        fail(f"{label} is invalid")
    return value


def positive_integer(value: Any, label: str) -> int:
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < 1
        or value > MAX_EXACT_INTEGER
    ):
        fail(f"{label} must be a bounded positive integer")
    return value


def parse_time(value: Any, label: str) -> datetime:
    raw = string(value, label)
    if not raw.endswith("Z"):
        fail(f"{label} must be UTC")
    try:
        parsed = datetime.fromisoformat(raw[:-1] + "+00:00")
    except ValueError:
        fail(f"{label} is not RFC3339")
    if parsed.tzinfo is None:
        fail(f"{label} has no timezone")
    return parsed.astimezone(UTC)


def format_time(value: datetime) -> str:
    return value.astimezone(UTC).replace(microsecond=0).isoformat().replace(
        "+00:00", "Z"
    )


def require_unpaginated(response: ApiResponse, label: str) -> None:
    if any(key.casefold() == "link" for key in response.headers):
        fail(f"{label} unexpectedly advertises another page")


def repository_path(suffix: str) -> str:
    if not suffix.startswith("/"):
        fail("repository API suffix is invalid")
    return f"/repos/{SOURCE_REPOSITORY}{suffix}"


def validate_user(
    value: Any, *, expected_login: str | None, label: str
) -> dict[str, Any]:
    user = mapping(value, label)
    login = string(user.get("login"), f"{label}.login", LOGIN_RE)
    if expected_login is not None and login != expected_login:
        fail(f"{label}.login differs")
    user_type = string(user.get("type"), f"{label}.type")
    if user_type not in {"User", "Bot"}:
        fail(f"{label}.type is not an authenticated user or bot")
    return {
        "id": positive_integer(user.get("id"), f"{label}.id"),
        "login": login,
        "nodeId": string(user.get("node_id"), f"{label}.node_id"),
        "type": user_type,
    }


def normalize_workflow_path(value: Any, expected: str, label: str) -> str:
    raw = string(value, label)
    path, marker, suffix = raw.partition("@")
    if path != expected:
        fail(f"{label} is not the reserved workflow")
    if marker and suffix not in {SOURCE_BRANCH, SOURCE_REF}:
        fail(f"{label} has an unexpected ref suffix")
    return path


def validate_run(
    value: Any,
    *,
    expected_id: int,
    expected_workflow: str,
    expected_source_sha: str,
    repository_id: int,
    now: datetime,
    label: str,
) -> dict[str, Any]:
    run = mapping(value, label)
    if positive_integer(run.get("id"), f"{label}.id") != expected_id:
        fail(f"{label}.id differs")
    if positive_integer(run.get("run_attempt"), f"{label}.run_attempt") != 1:
        fail(f"{label} is a rerun or replay")
    for key, expected in (
        ("event", "workflow_dispatch"),
        ("status", "completed"),
        ("conclusion", "success"),
        ("head_branch", SOURCE_BRANCH),
        ("head_sha", expected_source_sha),
    ):
        if run.get(key) != expected:
            fail(f"{label}.{key} differs")
    workflow_path = normalize_workflow_path(
        run.get("path"), expected_workflow, f"{label}.path"
    )
    workflow_id = positive_integer(
        run.get("workflow_id"), f"{label}.workflow_id"
    )
    actor = validate_user(run.get("actor"), expected_login=None, label=f"{label}.actor")
    triggering = validate_user(
        run.get("triggering_actor"),
        expected_login=actor["login"],
        label=f"{label}.triggering_actor",
    )
    if triggering != actor:
        fail(f"{label} actor and triggering actor identities differ")
    repository = mapping(run.get("repository"), f"{label}.repository")
    head_repository = mapping(
        run.get("head_repository"), f"{label}.head_repository"
    )
    for repository_label, repository_value in (
        ("repository", repository),
        ("head_repository", head_repository),
    ):
        if (
            positive_integer(
                repository_value.get("id"),
                f"{label}.{repository_label}.id",
            )
            != repository_id
            or repository_value.get("full_name") != SOURCE_REPOSITORY
        ):
            fail(f"{label}.{repository_label} differs")
    if sequence(
        run.get("referenced_workflows"), f"{label}.referenced_workflows"
    ):
        fail(f"{label} invokes a reusable workflow")
    if sequence(run.get("pull_requests"), f"{label}.pull_requests"):
        fail(f"{label} binds a pull request")
    created = parse_time(run.get("created_at"), f"{label}.created_at")
    started = parse_time(run.get("run_started_at"), f"{label}.run_started_at")
    updated = parse_time(run.get("updated_at"), f"{label}.updated_at")
    if not (
        created <= started <= updated
        and updated <= now + timedelta(minutes=5)
    ):
        fail(f"{label} timestamps are inconsistent")
    return {
        "id": expected_id,
        "attempt": 1,
        "workflowId": workflow_id,
        "workflowPath": workflow_path,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "sourceSha": expected_source_sha,
        "actor": actor,
        "createdAt": format_time(created),
        "startedAt": format_time(started),
        "updatedAt": format_time(updated),
    }


def read_run(
    client: ProviderReader,
    *,
    run_id: int,
    spec: ArtifactSpec,
    source_sha: str,
    repository_id: int,
    now: datetime,
) -> dict[str, Any]:
    current_response = client.get_json(
        repository_path(f"/actions/runs/{run_id}")
    )
    require_unpaginated(current_response, f"{spec.role} run")
    current = validate_run(
        current_response.value,
        expected_id=run_id,
        expected_workflow=spec.workflow_path,
        expected_source_sha=source_sha,
        repository_id=repository_id,
        now=now,
        label=f"{spec.role} run",
    )
    attempt_response = client.get_json(
        repository_path(
            f"/actions/runs/{run_id}/attempts/1?exclude_pull_requests=false"
        )
    )
    require_unpaginated(attempt_response, f"{spec.role} run attempt")
    attempt = validate_run(
        attempt_response.value,
        expected_id=run_id,
        expected_workflow=spec.workflow_path,
        expected_source_sha=source_sha,
        repository_id=repository_id,
        now=now,
        label=f"{spec.role} run attempt",
    )
    if attempt != current:
        fail(f"{spec.role} current run differs from exact attempt 1")
    return current


def validate_artifact(
    value: Any,
    *,
    spec: ArtifactSpec,
    repository_id: int,
    source_sha: str,
    now: datetime,
    expected_run_id: int | None,
    label: str,
) -> dict[str, Any]:
    artifact = mapping(value, label)
    artifact_id = positive_integer(artifact.get("id"), f"{label}.id")
    if artifact_id != spec.artifact_id:
        fail(f"{label}.id differs")
    if artifact.get("name") != spec.name:
        fail(f"{label}.name differs")
    digest = string(artifact.get("digest"), f"{label}.digest")
    if digest != spec.digest or ARTIFACT_DIGEST_RE.fullmatch(digest) is None:
        fail(f"{label}.digest differs")
    if artifact.get("expired") is not False:
        fail(f"{label} is expired")
    size_bytes = positive_integer(
        artifact.get("size_in_bytes"), f"{label}.size_in_bytes"
    )
    if size_bytes > MAX_ARCHIVE_BYTES:
        fail(f"{label} is too large")
    created = parse_time(artifact.get("created_at"), f"{label}.created_at")
    updated = parse_time(artifact.get("updated_at"), f"{label}.updated_at")
    expires = parse_time(artifact.get("expires_at"), f"{label}.expires_at")
    if not (
        created <= updated <= now + timedelta(minutes=5)
        and expires > now
    ):
        fail(f"{label} timestamps are inconsistent or expired")
    expected_download_url = (
        f"{API_ROOT}/repos/{SOURCE_REPOSITORY}/actions/artifacts/"
        f"{spec.artifact_id}/zip"
    )
    if artifact.get("archive_download_url") != expected_download_url:
        fail(f"{label}.archive_download_url differs")
    workflow_run = mapping(
        artifact.get("workflow_run"), f"{label}.workflow_run"
    )
    run_id = positive_integer(
        workflow_run.get("id"), f"{label}.workflow_run.id"
    )
    if expected_run_id is not None and run_id != expected_run_id:
        fail(f"{label}.workflow_run.id differs")
    for key in ("repository_id", "head_repository_id"):
        if (
            positive_integer(
                workflow_run.get(key), f"{label}.workflow_run.{key}"
            )
            != repository_id
        ):
            fail(f"{label}.workflow_run.{key} differs")
    if (
        workflow_run.get("head_branch") != SOURCE_BRANCH
        or workflow_run.get("head_sha") != source_sha
    ):
        fail(f"{label}.workflow_run source differs")
    return {
        "id": artifact_id,
        "name": spec.name,
        "digest": digest,
        "sizeBytes": size_bytes,
        "createdAt": format_time(created),
        "updatedAt": format_time(updated),
        "expiresAt": format_time(expires),
        "workflowRunId": run_id,
    }


def read_workflow(
    client: ProviderReader, workflow_id: int, spec: ArtifactSpec
) -> dict[str, Any]:
    response = client.get_json(
        repository_path(f"/actions/workflows/{workflow_id}")
    )
    require_unpaginated(response, f"{spec.role} workflow")
    workflow = mapping(response.value, f"{spec.role} workflow")
    if (
        positive_integer(workflow.get("id"), f"{spec.role} workflow.id")
        != workflow_id
        or workflow.get("path") != spec.workflow_path
        or workflow.get("state") != "active"
    ):
        fail(f"{spec.role} workflow definition differs")
    return {"id": workflow_id, "path": spec.workflow_path, "state": "active"}


def read_workflow_blob(
    client: ProviderReader, spec: ArtifactSpec, source_sha: str
) -> dict[str, Any]:
    encoded_path = urllib.parse.quote(spec.workflow_path, safe="/")
    response = client.get_json(
        repository_path(f"/contents/{encoded_path}?ref={source_sha}")
    )
    require_unpaginated(response, f"{spec.role} workflow blob")
    blob = mapping(response.value, f"{spec.role} workflow blob")
    if (
        blob.get("type") != "file"
        or blob.get("path") != spec.workflow_path
        or blob.get("encoding") != "base64"
    ):
        fail(f"{spec.role} workflow blob identity differs")
    blob_sha = string(
        blob.get("sha"), f"{spec.role} workflow blob.sha", COMMIT_RE
    )
    size = positive_integer(
        blob.get("size"), f"{spec.role} workflow blob.size"
    )
    content_value = blob.get("content")
    if (
        not isinstance(content_value, str)
        or not content_value
        or len(content_value) > MAX_API_JSON_BYTES
        or any(
            character.isspace() and character not in {"\r", "\n"}
            for character in content_value
        )
    ):
        fail(f"{spec.role} workflow blob.content is invalid")
    content = "".join(content_value.splitlines())
    try:
        decoded = base64.b64decode(content, validate=True)
    except ValueError:
        fail(f"{spec.role} workflow blob content is not canonical base64")
    if len(decoded) != size or not decoded:
        fail(f"{spec.role} workflow blob content size differs")
    return {"path": spec.workflow_path, "gitBlobSha": blob_sha, "sizeBytes": size}


def read_live_user(
    client: ProviderReader, expected: Mapping[str, Any], role: str
) -> dict[str, Any]:
    login = str(expected["login"])
    response = client.get_json(
        "/users/" + urllib.parse.quote(login, safe="")
    )
    require_unpaginated(response, f"{role} actor")
    live = validate_user(
        response.value, expected_login=login, label=f"{role} actor"
    )
    if live != expected:
        fail(f"{role} live actor differs from workflow run metadata")
    return live


def read_artifact_list(
    client: ProviderReader,
    *,
    spec: ArtifactSpec,
    run_id: int,
    repository_id: int,
    source_sha: str,
    now: datetime,
) -> dict[str, Any]:
    response = client.get_json(
        repository_path(
            f"/actions/runs/{run_id}/artifacts?per_page=100&page=1"
        )
    )
    require_unpaginated(response, f"{spec.role} artifact list")
    listing = mapping(response.value, f"{spec.role} artifact list")
    total = positive_integer(
        listing.get("total_count"), f"{spec.role} artifact list.total_count"
    )
    artifacts = sequence(
        listing.get("artifacts"), f"{spec.role} artifact list.artifacts"
    )
    if total != len(artifacts) or total > 100:
        fail(f"{spec.role} artifact list count differs or is paginated")
    ids = [
        positive_integer(
            mapping(item, f"{spec.role} artifact row").get("id"),
            f"{spec.role} artifact row.id",
        )
        for item in artifacts
    ]
    names = [
        string(
            mapping(item, f"{spec.role} artifact row").get("name"),
            f"{spec.role} artifact row.name",
        )
        for item in artifacts
    ]
    if len(set(ids)) != len(ids) or len(set(names)) != len(names):
        fail(f"{spec.role} artifact list contains duplicates")
    matches = [
        item
        for item in artifacts
        if mapping(item, f"{spec.role} artifact row").get("id")
        == spec.artifact_id
        and mapping(item, f"{spec.role} artifact row").get("name")
        == spec.name
    ]
    if len(matches) != 1:
        fail(f"{spec.role} artifact is not unique in its source run")
    return validate_artifact(
        matches[0],
        spec=spec,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        expected_run_id=run_id,
        label=f"{spec.role} artifact list row",
    )


def authenticate_source(
    client: ProviderReader, source_sha: str
) -> dict[str, Any]:
    repository_response = client.get_json(repository_path(""))
    require_unpaginated(repository_response, "repository")
    repository = mapping(repository_response.value, "repository")
    if (
        repository.get("full_name") != SOURCE_REPOSITORY
        or repository.get("default_branch") != SOURCE_BRANCH
    ):
        fail("repository identity or default branch differs")
    repository_id = positive_integer(repository.get("id"), "repository.id")
    branch_response = client.get_json(
        repository_path(f"/branches/{SOURCE_BRANCH}")
    )
    require_unpaginated(branch_response, "main branch")
    branch = mapping(branch_response.value, "main branch")
    commit = mapping(branch.get("commit"), "main branch.commit")
    if (
        branch.get("name") != SOURCE_BRANCH
        or branch.get("protected") is not True
        or commit.get("sha") != source_sha
    ):
        fail("current main is unprotected or differs from the exact source")
    return {
        "repositoryId": repository_id,
        "repository": SOURCE_REPOSITORY,
        "branch": SOURCE_BRANCH,
        "ref": SOURCE_REF,
        "commit": source_sha,
        "protected": True,
    }


def authenticate_artifact(
    client: ProviderReader,
    *,
    spec: ArtifactSpec,
    repository_id: int,
    source_sha: str,
    now: datetime,
    archive_path: Path,
) -> AuthenticatedArtifact:
    detail_response = client.get_json(
        repository_path(f"/actions/artifacts/{spec.artifact_id}")
    )
    require_unpaginated(detail_response, f"{spec.role} artifact detail")
    metadata = validate_artifact(
        detail_response.value,
        spec=spec,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        expected_run_id=None,
        label=f"{spec.role} artifact detail",
    )
    run_id = int(metadata["workflowRunId"])
    expected_name = f"{spec.name_prefix}-{run_id}-1"
    if spec.name != expected_name:
        fail(f"{spec.role} artifact name is not bound to run and attempt 1")
    run = read_run(
        client,
        run_id=run_id,
        spec=spec,
        source_sha=source_sha,
        repository_id=repository_id,
        now=now,
    )
    workflow = read_workflow(client, int(run["workflowId"]), spec)
    workflow_blob = read_workflow_blob(client, spec, source_sha)
    actor = read_live_user(client, mapping(run["actor"], f"{spec.role} actor"), spec.role)
    listed = read_artifact_list(
        client,
        spec=spec,
        run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
    )
    if listed != metadata:
        fail(f"{spec.role} artifact list and detail differ")
    downloaded_digest, downloaded_size = client.download_artifact(
        spec.artifact_id, archive_path, MAX_ARCHIVE_BYTES
    )
    digest_match = ARTIFACT_DIGEST_RE.fullmatch(spec.digest)
    assert digest_match is not None
    if (
        downloaded_digest != digest_match.group(1)
        or downloaded_size != metadata["sizeBytes"]
    ):
        fail(f"{spec.role} downloaded archive differs from provider metadata")
    recheck_response = client.get_json(
        repository_path(f"/actions/artifacts/{spec.artifact_id}")
    )
    require_unpaginated(recheck_response, f"{spec.role} artifact recheck")
    rechecked = validate_artifact(
        recheck_response.value,
        spec=spec,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        expected_run_id=run_id,
        label=f"{spec.role} artifact recheck",
    )
    if rechecked != metadata:
        fail(f"{spec.role} artifact changed during download")
    return AuthenticatedArtifact(
        spec=spec,
        metadata=metadata,
        run=run,
        workflow=workflow,
        actor=actor,
        workflow_blob=workflow_blob,
        archive_path=archive_path,
    )


def reauthenticate_artifact(
    client: ProviderReader,
    *,
    authenticated: AuthenticatedArtifact,
    repository_id: int,
    source_sha: str,
    now: datetime,
) -> dict[str, Any]:
    spec = authenticated.spec
    response = client.get_json(
        repository_path(f"/actions/artifacts/{spec.artifact_id}")
    )
    require_unpaginated(response, f"{spec.role} late artifact detail")
    metadata = validate_artifact(
        response.value,
        spec=spec,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        expected_run_id=int(authenticated.run["id"]),
        label=f"{spec.role} late artifact detail",
    )
    run = read_run(
        client,
        run_id=int(authenticated.run["id"]),
        spec=spec,
        source_sha=source_sha,
        repository_id=repository_id,
        now=now,
    )
    workflow = read_workflow(client, int(run["workflowId"]), spec)
    workflow_blob = read_workflow_blob(client, spec, source_sha)
    actor = read_live_user(client, mapping(run["actor"], f"{spec.role} actor"), spec.role)
    listed = read_artifact_list(
        client,
        spec=spec,
        run_id=int(run["id"]),
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
    )
    observed = {
        "metadata": metadata,
        "run": run,
        "workflow": workflow,
        "actor": actor,
        "workflowBlob": workflow_blob,
    }
    expected = {
        "metadata": dict(authenticated.metadata),
        "run": dict(authenticated.run),
        "workflow": dict(authenticated.workflow),
        "actor": dict(authenticated.actor),
        "workflowBlob": dict(authenticated.workflow_blob),
    }
    if observed != expected or listed != metadata:
        fail(f"{spec.role} provider authority drifted before handoff")
    return observed


def safe_member_path(name: str, label: str) -> PurePosixPath:
    if (
        not name
        or len(name) > 4096
        or "\\" in name
        or "\x00" in name
        or name.startswith("/")
    ):
        fail(f"{label} is not a portable relative path")
    value = PurePosixPath(name.rstrip("/"))
    if not value.parts or any(
        part in {"", ".", ".."}
        or PORTABLE_SEGMENT_RE.fullmatch(part) is None
        for part in value.parts
    ):
        fail(f"{label} is not a portable relative path")
    if value.as_posix() != name.rstrip("/"):
        fail(f"{label} is not canonical")
    return value


def extract_artifact_archive(
    archive: Path, destination: Path, label: str
) -> list[Path]:
    if archive.is_symlink() or not archive.is_file():
        fail(f"{label} archive is not a regular file")
    extracted: list[Path] = []
    try:
        with zipfile.ZipFile(archive, "r") as handle:
            infos = handle.infolist()
            if not infos or len(infos) > MAX_ARCHIVE_ENTRIES:
                fail(f"{label} archive has an invalid entry count")
            exact_names: set[str] = set()
            folded_names: set[str] = set()
            total = 0
            for info in infos:
                member = safe_member_path(info.filename, f"{label} member")
                normalized = member.as_posix()
                folded = normalized.casefold()
                if normalized in exact_names or folded in folded_names:
                    fail(f"{label} archive has duplicate or case-colliding entries")
                exact_names.add(normalized)
                folded_names.add(folded)
                mode = (info.external_attr >> 16) & 0xFFFF
                file_type = stat.S_IFMT(mode)
                is_directory = info.is_dir()
                if is_directory:
                    if file_type not in {0, stat.S_IFDIR} or info.file_size != 0:
                        fail(f"{label} archive has an invalid directory entry")
                    continue
                if file_type not in {0, stat.S_IFREG}:
                    fail(f"{label} archive has a linked or special entry")
                if info.flag_bits & 0x1:
                    fail(f"{label} archive has an encrypted ZIP member")
                if info.file_size < 1 or info.file_size > MAX_ENTRY_BYTES:
                    fail(f"{label} archive member has an invalid size")
                if (
                    info.compress_size < 1
                    or info.file_size
                    > info.compress_size * MAX_COMPRESSION_RATIO
                ):
                    fail(f"{label} archive member exceeds compression limits")
                total += info.file_size
                if total > MAX_EXPANDED_BYTES:
                    fail(f"{label} archive expands beyond its fixed boundary")
                target = destination.joinpath(*member.parts)
                target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
                if target.exists() or target.is_symlink():
                    fail(f"{label} archive collides with an existing path")
                descriptor = os.open(
                    target,
                    os.O_WRONLY
                    | os.O_CREAT
                    | os.O_EXCL
                    | int(getattr(os, "O_NOFOLLOW", 0)),
                    0o600,
                )
                try:
                    read_size = 0
                    with handle.open(info, "r") as source:
                        while True:
                            chunk = source.read(1024 * 1024)
                            if not chunk:
                                break
                            read_size += len(chunk)
                            if read_size > info.file_size:
                                fail(f"{label} archive member expanded unexpectedly")
                            write_all(descriptor, chunk)
                    if read_size != info.file_size:
                        fail(f"{label} archive member size differs")
                    os.fsync(descriptor)
                finally:
                    os.close(descriptor)
                extracted.append(target)
    except ContractError:
        raise
    except (zipfile.BadZipFile, RuntimeError, OSError, EOFError):
        fail(f"{label} archive is not a valid bounded ZIP")
    return extracted


def sha256_file(path: Path) -> tuple[str, int]:
    before = path.stat(follow_symlinks=False)
    if not stat.S_ISREG(before.st_mode) or before.st_size < 1:
        fail(f"{path} is not a nonempty regular file")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    after = path.stat(follow_symlinks=False)
    if (
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
        before.st_ctime_ns,
    ) != (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
        after.st_ctime_ns,
    ):
        fail(f"{path} changed while it was hashed")
    return digest.hexdigest(), after.st_size


def file_reference(root: Path, path: Path) -> dict[str, Any]:
    try:
        relative = path.relative_to(root).as_posix()
    except ValueError:
        fail("candidate reference escapes the candidate root")
    digest, size = sha256_file(path)
    return {"path": relative, "sha256": digest, "sizeBytes": size}


def artifact_reference(
    root: Path, path: Path, artifact_id: str
) -> dict[str, Any]:
    reference = file_reference(root, path)
    return {
        "artifactId": artifact_id,
        "fileName": path.name,
        **reference,
    }


def atomic_json(path: Path, payload: Mapping[str, Any]) -> None:
    data = (
        json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False) + "\n"
    ).encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor = os.open(
        path,
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | int(getattr(os, "O_NOFOLLOW", 0)),
        0o600,
    )
    try:
        write_all(descriptor, data)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def copy_exact(source: Path, destination: Path) -> None:
    expected_digest, expected_size = sha256_file(source)
    destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    source_descriptor = os.open(
        source, os.O_RDONLY | int(getattr(os, "O_NOFOLLOW", 0))
    )
    destination_descriptor = os.open(
        destination,
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | int(getattr(os, "O_NOFOLLOW", 0)),
        0o600,
    )
    try:
        copied = 0
        while True:
            chunk = os.read(source_descriptor, 1024 * 1024)
            if not chunk:
                break
            copied += len(chunk)
            write_all(destination_descriptor, chunk)
        os.fsync(destination_descriptor)
    finally:
        os.close(source_descriptor)
        os.close(destination_descriptor)
    actual_digest, actual_size = sha256_file(destination)
    if (
        copied != expected_size
        or actual_size != expected_size
        or actual_digest != expected_digest
    ):
        fail("copied candidate artifact differs from its source")


def contract_name(payload: Mapping[str, Any]) -> str | None:
    value = payload.get("contractName", payload.get("contract_name"))
    return value if isinstance(value, str) else None


def json_contracts(root: Path) -> list[tuple[Path, dict[str, Any]]]:
    result: list[tuple[Path, dict[str, Any]]] = []
    for path in sorted(root.rglob("*.json")):
        if path.is_symlink() or not path.is_file():
            fail("candidate input JSON is linked or not regular")
        payload = load_json(path, f"candidate input {path.relative_to(root)}")
        result.append((path, payload))
    return result


def unique_contract(
    contracts: Sequence[tuple[Path, dict[str, Any]]],
    expected_contract: str,
    *,
    predicate: Any,
    label: str,
) -> tuple[Path, dict[str, Any]]:
    matches = [
        (path, payload)
        for path, payload in contracts
        if contract_name(payload) == expected_contract and predicate(payload)
    ]
    if not matches:
        fail(f"candidate inputs contain no exact {label}")
    digests = {sha256_file(path)[0] for path, _ in matches}
    if len(digests) != 1:
        fail(f"candidate inputs contain conflicting {label} receipts")
    return sorted(matches, key=lambda item: item[0].as_posix())[0]


def locate_exact_artifact(
    root: Path, *, file_name: str, sha256: str, size_bytes: int
) -> Path:
    matches: list[Path] = []
    for path in sorted(root.rglob(file_name)):
        if "provider-archives" in path.parts:
            continue
        if path.is_symlink() or not path.is_file():
            fail(f"{file_name} candidate input is linked or not regular")
        digest, size = sha256_file(path)
        if digest == sha256 and size == size_bytes:
            matches.append(path)
    if not matches:
        fail(f"candidate inputs do not contain exact {file_name} bytes")
    return matches[0]


def reference_from_binding(
    root: Path, base: Path, binding: Mapping[str, Any], label: str
) -> tuple[Path, dict[str, Any]]:
    relative = assembler.safe_relative_path(binding.get("path"), f"{label}.path")
    path = base.joinpath(*PurePosixPath(relative).parts)
    reference = file_reference(root, path)
    if (
        reference["sha256"] != binding.get("sha256")
        or reference["sizeBytes"] != binding.get("sizeBytes")
    ):
        fail(f"{label} bytes differ from their binding")
    return path, reference


def validate_input_relationships(
    artifacts: Sequence[AuthenticatedArtifact],
) -> None:
    if len(artifacts) != len(ROLE_POLICIES):
        fail("all seven platform artifact roles are mandatory")
    ids = [item.spec.artifact_id for item in artifacts]
    names = [item.spec.name for item in artifacts]
    roles = [item.spec.role for item in artifacts]
    if (
        len(set(ids)) != len(ids)
        or len(set(names)) != len(names)
        or set(roles) != set(ROLE_POLICIES)
    ):
        fail("provider inputs contain duplicate or missing artifact identities")
    runs = {item.spec.role: int(item.run["id"]) for item in artifacts}
    if runs["macos-escrow"] != runs["macos-handoff"]:
        fail("macOS escrow and handoff artifacts are cross-run inputs")
    non_macos = [
        runs[role]
        for role in ROLE_POLICIES
        if not role.startswith("macos-")
    ]
    if len(set(non_macos)) != len(non_macos):
        fail("Windows or Linux inputs reuse a workflow run")
    if runs["macos-escrow"] in set(non_macos):
        fail("macOS input run is reused across platforms")


def provider_actor_map(
    artifacts: Sequence[AuthenticatedArtifact],
) -> dict[str, str]:
    by_role = {item.spec.role: item for item in artifacts}
    if set(by_role) != set(ROLE_POLICIES) or len(by_role) != len(artifacts):
        fail("all seven authenticated provider roles are required for actors")
    return {
        role: string(
            mapping(by_role[role].actor, f"{role} actor").get("login"),
            f"{role} actor login",
            LOGIN_RE,
        )
        for role in ROLE_POLICIES
    }


def validate_producer_actor_separation(
    producer_actor: str,
    artifacts: Sequence[AuthenticatedArtifact],
) -> dict[str, str]:
    canonical_producer_actor = string(
        producer_actor, "producer actor", LOGIN_RE
    )
    actors = provider_actor_map(artifacts)
    if canonical_producer_actor.casefold() in {
        actor.casefold() for actor in actors.values()
    }:
        fail(
            "producer dispatcher must be independent of all seven "
            "authenticated provider run actors"
        )
    return actors


def provider_source_projection(
    item: AuthenticatedArtifact, *, artifact_name: bool
) -> dict[str, Any]:
    result = {
        "repository": SOURCE_REPOSITORY,
        "workflow": item.spec.workflow_path,
        "runId": str(item.run["id"]),
        "runAttempt": "1",
        "ref": SOURCE_REF,
        "sha": item.run["sourceSha"],
        "actor": item.actor["login"],
        "triggeringActor": item.actor["login"],
        "rerunPolicy": "same-actor-only",
    }
    if artifact_name:
        result["artifactName"] = item.spec.name
    return result


def require_source_binding(
    value: Any,
    item: AuthenticatedArtifact,
    *,
    label: str,
    artifact_name: bool,
    same_actor: bool = True,
) -> None:
    source = mapping(value, label)
    expected = provider_source_projection(item, artifact_name=artifact_name)
    if not same_actor:
        expected.pop("triggeringActor")
        expected.pop("rerunPolicy")
    for key, expected_value in expected.items():
        if str(source.get(key)) != str(expected_value):
            fail(f"{label}.{key} differs from provider authority")


def validate_windows_provider_chain(
    *,
    extraction_roots: Mapping[str, Path],
    authenticated: Mapping[str, AuthenticatedArtifact],
) -> None:
    capture_contract = (
        "chummer6-ui.preview-nightly-native-windows-capture"
    )
    capture_inventory_contract = (
        "chummer6-ui.preview-nightly-native-windows-capture-inventory"
    )
    finalization_contract = (
        "chummer6-ui.preview-nightly-native-windows-finalization"
    )
    capture_contracts = json_contracts(
        extraction_roots["windows-capture"]
    )
    finalized_contracts = json_contracts(
        extraction_roots["windows-evidence"]
    )
    capture_path, capture = unique_contract(
        capture_contracts,
        capture_contract,
        predicate=lambda _: True,
        label="Windows capture manifest",
    )
    finalized_capture_path, finalized_capture = unique_contract(
        finalized_contracts,
        capture_contract,
        predicate=lambda _: True,
        label="finalized Windows capture manifest",
    )
    if (
        sha256_file(capture_path) != sha256_file(finalized_capture_path)
        or capture != finalized_capture
    ):
        fail("finalized Windows evidence does not preserve the exact capture")
    inventory_path, inventory = unique_contract(
        capture_contracts,
        capture_inventory_contract,
        predicate=lambda _: True,
        label="Windows capture inventory",
    )
    finalized_inventory_path, finalized_inventory = unique_contract(
        finalized_contracts,
        capture_inventory_contract,
        predicate=lambda _: True,
        label="finalized Windows capture inventory",
    )
    if (
        sha256_file(inventory_path) != sha256_file(finalized_inventory_path)
        or inventory != finalized_inventory
    ):
        fail(
            "finalized Windows evidence does not preserve the exact capture "
            "inventory"
        )
    _, finalization = unique_contract(
        finalized_contracts,
        finalization_contract,
        predicate=lambda _: True,
        label="Windows finalization receipt",
    )
    capture_item = authenticated["windows-capture"]
    evidence_item = authenticated["windows-evidence"]
    export_item = authenticated["windows-export"]
    require_source_binding(
        capture.get("source"),
        capture_item,
        label="Windows capture source",
        artifact_name=True,
    )
    require_source_binding(
        finalization.get("captureSource"),
        capture_item,
        label="Windows finalization capture source",
        artifact_name=True,
    )
    require_source_binding(
        finalization.get("finalizationSource"),
        evidence_item,
        label="Windows finalization source",
        artifact_name=True,
    )
    inventory_digest, _ = sha256_file(finalized_inventory_path)
    if finalization.get("captureInventorySha256") != inventory_digest:
        fail("Windows finalization does not bind the exact capture inventory")
    candidate = mapping(capture.get("candidate"), "Windows capture candidate")
    expected_export = {
        "repository": SOURCE_REPOSITORY,
        "workflow": export_item.spec.workflow_path,
        "runId": str(export_item.run["id"]),
        "runAttempt": "1",
        "ref": SOURCE_REF,
        "sha": export_item.run["sourceSha"],
        "actor": export_item.actor["login"],
        "artifactId": str(export_item.spec.artifact_id),
        "artifactName": export_item.spec.name,
        "artifactSha256": export_item.spec.digest.removeprefix("sha256:"),
    }
    for key, expected_value in expected_export.items():
        if str(candidate.get(key)) != str(expected_value):
            fail(
                "Windows capture candidate producer differs from the exact "
                f"export provider authority at {key}"
            )


def validate_linux_provider_chain(
    *,
    extraction_roots: Mapping[str, Path],
    authenticated: Mapping[str, AuthenticatedArtifact],
    lifecycle_receipt: Mapping[str, Any],
) -> None:
    export_contracts = json_contracts(extraction_roots["linux-export"])
    export_path, export_receipt = unique_contract(
        export_contracts,
        linux_deb_signing.EXPORT_CONTRACT,
        predicate=lambda payload: (
            payload.get("contractVersion") == 3
            and payload.get("status") == "signed"
            and payload.get("nonPublishing") is True
        ),
        label="signed Linux candidate export receipt",
    )
    export_item = authenticated["linux-export"]
    evidence_item = authenticated["linux-evidence"]
    require_source_binding(
        export_receipt.get("source"),
        export_item,
        label="Linux export source",
        artifact_name=False,
        same_actor=False,
    )
    native_runner = mapping(
        lifecycle_receipt.get("nativeRunner"), "Linux native runner"
    )
    require_source_binding(
        native_runner.get("source"),
        evidence_item,
        label="Linux lifecycle source",
        artifact_name=False,
    )
    exported_artifact = mapping(
        export_receipt.get("artifact"), "Linux exported artifact"
    )
    candidate = mapping(
        lifecycle_receipt.get("candidate"), "Linux lifecycle candidate"
    )
    for export_key, lifecycle_key in (
        ("fileName", "artifactFileName"),
        ("sha256", "sha256"),
        ("sizeBytes", "sizeBytes"),
    ):
        if exported_artifact.get(export_key) != candidate.get(lifecycle_key):
            fail("Linux export and lifecycle candidate bytes differ")
    if export_receipt.get("releaseVersion") != candidate.get("version"):
        fail("Linux export and lifecycle release versions differ")
    package_authority = mapping(
        lifecycle_receipt.get("packageAuthority"),
        "Linux lifecycle package authority",
    )
    lifecycle_candidate = mapping(
        package_authority.get("candidate"),
        "Linux lifecycle candidate package authority",
    )
    lifecycle_export = mapping(
        lifecycle_candidate.get("signedExportReceipt"),
        "Linux lifecycle signed export receipt binding",
    )
    export_digest, export_size = sha256_file(export_path)
    if (
        lifecycle_export.get("sha256") != export_digest
        or lifecycle_export.get("sizeBytes") != export_size
    ):
        fail(
            "authenticated Linux export receipt differs from the exact "
            "receipt independently verified by the lifecycle lane"
        )
    expected_material = {
        "signingReceipt": "signingReceipt",
        "verificationPolicy": "verificationPolicy",
        "publicKeyring": "publicKeyring",
    }
    export_root = extraction_roots["linux-export"]
    for export_key, lifecycle_key in expected_material.items():
        export_binding = mapping(
            export_receipt.get(export_key),
            f"Linux signed export {export_key}",
        )
        member = safe_member_path(
            string(
                export_binding.get("memberPath"),
                f"Linux signed export {export_key}.memberPath",
            ),
            f"Linux signed export {export_key}.memberPath",
        )
        material_path = export_root.joinpath(*member.parts)
        material_digest, material_size = sha256_file(material_path)
        lifecycle_binding = mapping(
            lifecycle_candidate.get(lifecycle_key),
            f"Linux lifecycle {lifecycle_key} binding",
        )
        if (
            export_binding.get("sha256") != material_digest
            or export_binding.get("sizeBytes") != material_size
            or lifecycle_binding.get("sha256") != material_digest
            or lifecycle_binding.get("sizeBytes") != material_size
        ):
            fail(
                f"Linux {export_key} differs across authenticated export "
                "and independently verified lifecycle authority"
            )
    lifecycle_live = mapping(
        lifecycle_receipt.get("livePredecessorAuthority"),
        "Linux lifecycle live predecessor authority",
    )
    export_live = mapping(
        export_receipt.get("livePredecessorAuthority"),
        "Linux signed export live predecessor authority",
    )
    for key in (
        "liveReleaseChannelSha256",
        "nMinusOneReleaseSha256",
        "selectedTupleSha256",
    ):
        if export_live.get(key) != lifecycle_live.get(key):
            fail(
                "Linux signed export and lifecycle live-predecessor "
                f"authority differ at {key}"
            )
    export_package = mapping(
        export_receipt.get("package"), "Linux signed export package"
    )
    for export_key, lifecycle_key in (
        ("name", "packageName"),
        ("version", "packageVersion"),
        ("architecture", "architecture"),
    ):
        if export_package.get(export_key) != lifecycle_candidate.get(
            lifecycle_key
        ):
            fail(
                "Linux signed export and lifecycle package identities differ"
            )


def validate_macos_provider_chain(
    *,
    candidate_root: Path,
    extraction_roots: Mapping[str, Path],
    authenticated: Mapping[str, AuthenticatedArtifact],
    macos_adapter_path: Path,
    macos_adapter: Mapping[str, Any],
) -> None:
    handoff_contracts = json_contracts(
        extraction_roots["macos-handoff"]
    )
    _, handoff = unique_contract(
        handoff_contracts,
        "chummer6-ui.macos-flagship-evidence-handoff",
        predicate=lambda payload: payload.get("contractVersion") == 3,
        label="macOS coordinator handoff",
    )
    escrow_item = authenticated["macos-escrow"]
    handoff_item = authenticated["macos-handoff"]
    expected = {
        "artifactId": str(escrow_item.spec.artifact_id),
        "artifactName": escrow_item.spec.name,
        "artifactDigest": escrow_item.spec.digest.removeprefix("sha256:"),
        "artifactUrl": (
            f"https://github.com/{SOURCE_REPOSITORY}/actions/runs/"
            f"{escrow_item.run['id']}/artifacts/{escrow_item.spec.artifact_id}"
        ),
        "artifactContents": "receipts_and_encrypted_candidate_escrow",
        "repository": SOURCE_REPOSITORY,
        "workflow": escrow_item.spec.workflow_path,
        "runId": str(escrow_item.run["id"]),
        "runAttempt": "1",
        "ref": SOURCE_REF,
        "sha": escrow_item.run["sourceSha"],
        "actor": escrow_item.actor["login"],
        "triggeringActor": escrow_item.actor["login"],
        "rerunPolicy": "same-actor-only",
        "environment": "macos-flagship-evidence",
        "rid": "osx-arm64",
        "candidatePlaintextDistributed": False,
        "candidateBytesRetained": True,
        "provenanceAuthenticated": False,
    }
    for key, expected_value in expected.items():
        if handoff.get(key) != expected_value:
            fail(f"macOS handoff {key} differs from provider authority")
    if int(handoff_item.run["id"]) != int(escrow_item.run["id"]):
        fail("macOS handoff artifact does not share the escrow source run")
    adapter_digest, _ = sha256_file(macos_adapter_path)
    if handoff.get("nativeE2EReceiptSha256") != adapter_digest:
        fail("macOS handoff does not bind the exact native adapter")
    candidate = mapping(macos_adapter.get("artifact"), "macOS candidate artifact")
    if handoff.get("candidateArtifactSha256") != candidate.get("sha256"):
        fail("macOS handoff candidate digest differs from native evidence")
    evidence_reference = mapping(
        mapping(
            mapping(macos_adapter.get("checks"), "macOS checks").get(
                "cleanInstall"
            ),
            "macOS clean-install check",
        ).get("evidence"),
        "macOS aggregate reference",
    )
    evidence_path, _ = reference_from_binding(
        candidate_root,
        candidate_root,
        evidence_reference,
        "macOS aggregate evidence",
    )
    evidence_digest, _ = sha256_file(evidence_path)
    if handoff.get("evidenceSha256") != evidence_digest:
        fail("macOS handoff does not bind the exact aggregate evidence")
    evidence = load_json(evidence_path, "macOS aggregate evidence")
    receipt_path = candidate_root / "escrow" / (
        "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json"
    )
    ciphertext_path = candidate_root / "escrow" / (
        "chummer-avalonia-osx-arm64-installer.dmg.aes256gcm"
    )
    try:
        escrow_projection = assembler.macos_flagship.validate_escrow_receipt(
            receipt_path,
            ciphertext_path,
            evidence=evidence,
            repository=SOURCE_REPOSITORY,
            ref=SOURCE_REF,
            sha=str(escrow_item.run["sourceSha"]),
            actor=str(escrow_item.actor["login"]),
            triggering_actor=str(escrow_item.actor["login"]),
            run_id=str(escrow_item.run["id"]),
            run_attempt="1",
        )
    except assembler.macos_flagship.ContractError as exc:
        fail(f"macOS escrow receipt is invalid: {exc}")
    if handoff.get("candidateEscrow") != escrow_projection:
        fail("macOS handoff escrow projection differs from exact custody bytes")
    receipt_contracts = json_contracts(candidate_root / "receipts")
    inventory_path, _ = unique_contract(
        receipt_contracts,
        "chummer6-ui.macos-flagship-evidence-inventory",
        predicate=lambda payload: payload.get("contractVersion") == 1,
        label="macOS evidence inventory",
    )
    inventory_digest, _ = sha256_file(inventory_path)
    if handoff.get("inventorySha256") != inventory_digest:
        fail("macOS handoff does not bind the exact evidence inventory")
    if (
        handoff.get("releaseVersion")
        != mapping(macos_adapter.get("candidate"), "macOS identity").get(
            "releaseVersion"
        )
        or handoff.get("livePredecessorAuthority")
        != macos_adapter.get("livePredecessorAuthority")
    ):
        fail("macOS handoff release or predecessor identity differs")


def assemble_candidate(
    *,
    candidate_root: Path,
    candidate_id: str,
    generation_id: str,
    channel_id: str,
    source_sha: str,
    producer_actor: str,
    producer_run_id: int,
    authenticated: Sequence[AuthenticatedArtifact],
    macos_private_key: Path,
    macos_recipient_spki_sha256: str,
) -> Path:
    for label, value in (
        ("candidate ID", candidate_id),
        ("generation ID", generation_id),
        ("channel ID", channel_id),
    ):
        string(value, label, PORTABLE_ID_RE)
    if channel_id != "preview":
        fail("global flagship candidate production is restricted to preview")
    string(source_sha, "source SHA", COMMIT_RE)
    string(producer_actor, "producer actor", LOGIN_RE)
    positive_integer(producer_run_id, "producer run ID")
    provider_actors = validate_producer_actor_separation(
        producer_actor, authenticated
    )

    archive_root = candidate_root / "provider-archives"
    archive_root.mkdir(parents=True, mode=0o700)
    extraction_roots: dict[str, Path] = {}
    archived_paths: dict[str, Path] = {}
    for item in authenticated:
        archived = archive_root / f"{item.spec.role}.zip"
        copy_exact(item.archive_path, archived)
        archived_paths[item.spec.role] = archived
        if item.spec.role == "macos-escrow":
            destination = candidate_root
        else:
            destination = candidate_root / "provider-inputs" / item.spec.role
            destination.mkdir(parents=True, exist_ok=False, mode=0o700)
        extract_artifact_archive(
            archived, destination, f"{item.spec.role} artifact"
        )
        extraction_roots[item.spec.role] = destination

    provider_manifest_path = (
        candidate_root / "GLOBAL_FLAGSHIP_PROVIDER_INPUTS.generated.json"
    )
    atomic_json(
        provider_manifest_path,
        {
            "contractName": PROVIDER_CONTRACT,
            "contractVersion": 1,
            "nonPublishing": True,
            "provenanceAuthenticated": True,
            "releaseArtifactBytesAuthenticated": True,
            "source": {
                "repository": SOURCE_REPOSITORY,
                "ref": SOURCE_REF,
                "commit": source_sha,
            },
            "artifacts": [
                item.projection(
                    candidate_root, archived_paths[item.spec.role]
                )
                for item in sorted(
                    authenticated, key=lambda value: value.spec.role
                )
            ],
        },
    )

    escrow_receipt = candidate_root / "escrow" / (
        "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json"
    )
    escrow_ciphertext = candidate_root / "escrow" / (
        "chummer-avalonia-osx-arm64-installer.dmg.aes256gcm"
    )
    macos_artifact = (
        candidate_root
        / "artifacts"
        / "macos"
        / "chummer-avalonia-osx-arm64-installer.dmg"
    )
    macos_artifact.parent.mkdir(parents=True, mode=0o700)
    subprocess.run(
        [
            "node",
            str(REPO_ROOT / "scripts/macos_flagship_candidate_escrow.mjs"),
            "open",
            "--receipt",
            str(escrow_receipt),
            "--ciphertext",
            str(escrow_ciphertext),
            "--private-key",
            str(macos_private_key),
            "--expected-recipient-spki-sha256",
            macos_recipient_spki_sha256,
            "--output",
            str(macos_artifact),
        ],
        cwd=REPO_ROOT,
        check=True,
    )

    windows_contracts = json_contracts(
        extraction_roots["windows-evidence"]
    )
    linux_contracts = json_contracts(extraction_roots["linux-evidence"])
    macos_receipt_contracts = json_contracts(candidate_root / "receipts")
    authenticated_by_role = {
        item.spec.role: item for item in authenticated
    }
    validate_windows_provider_chain(
        extraction_roots=extraction_roots,
        authenticated=authenticated_by_role,
    )

    lifecycle: dict[str, tuple[Path, dict[str, Any]]] = {}
    for platform, contracts, rid in (
        ("windows", windows_contracts, "win-x64"),
        ("linux", linux_contracts, "linux-x64"),
    ):
        lifecycle[platform] = unique_contract(
            contracts,
            desktop_lifecycle.RECEIPT_CONTRACT,
            predicate=lambda payload, expected_platform=platform, expected_rid=rid: (
                payload.get("platform") == expected_platform
                and payload.get("rid") == expected_rid
            ),
            label=f"{platform} rich native lifecycle",
        )

    windows_receipt = lifecycle["windows"][1]
    linux_receipt = lifecycle["linux"][1]
    release_version = string(
        windows_receipt.get("candidate", {}).get("version"),
        "Windows candidate release version",
        PORTABLE_ID_RE,
    )
    previous_version = string(
        windows_receipt.get("nMinusOne", {}).get("version"),
        "Windows previous release version",
        PORTABLE_ID_RE,
    )
    if release_version == previous_version:
        fail("candidate and N-1 release versions must be distinct")
    for platform, receipt in (
        ("linux", linux_receipt),
    ):
        if (
            receipt.get("candidate", {}).get("version") != release_version
            or receipt.get("nMinusOne", {}).get("version") != previous_version
            or receipt.get("candidate", {}).get("sourceCommit") != source_sha
        ):
            fail(f"{platform} baseline/candidate identity differs")
    if windows_receipt.get("candidate", {}).get("sourceCommit") != source_sha:
        fail("Windows candidate source differs")
    validate_linux_provider_chain(
        extraction_roots=extraction_roots,
        authenticated=authenticated_by_role,
        lifecycle_receipt=linux_receipt,
    )

    adapters: dict[str, tuple[Path, dict[str, Any]]] = {}
    for platform, rid in (
        ("windows", "win-x64"),
        ("linux", "linux-x64"),
    ):
        receipt_path, _ = lifecycle[platform]
        adapter_path = (
            candidate_root
            / "receipts"
            / platform
            / f"FLAGSHIP_NATIVE_E2E.{platform}.generated.json"
        )
        adapter_path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        desktop_lifecycle.emit_flagship_adapter(
            receipt_path=receipt_path,
            evidence_root=receipt_path.parent,
            candidate_root=candidate_root,
            evidence_path=receipt_path.relative_to(candidate_root).as_posix(),
            output_path=adapter_path,
            candidate_id=candidate_id,
            generation_id=generation_id,
            artifact_id=f"avalonia-{rid}-installer",
            source_commit=source_sha,
        )
        adapters[platform] = (
            adapter_path,
            load_json(adapter_path, f"{platform} native adapter"),
        )

    macos_adapter_path, macos_adapter = unique_contract(
        macos_receipt_contracts,
        "chummer6-ui.flagship-native-e2e.macos.v1",
        predicate=lambda payload: (
            payload.get("platform") == "macos"
            and payload.get("rid") == "osx-arm64"
        ),
        label="macOS native adapter",
    )
    macos_identity = mapping(macos_adapter.get("candidate"), "macOS identity")
    if macos_identity != {
        "candidateId": candidate_id,
        "generationId": generation_id,
        "previousReleaseVersion": previous_version,
        "releaseVersion": release_version,
        "sourceCommit": source_sha,
    }:
        fail("macOS global candidate identity differs")
    adapters["macos"] = (macos_adapter_path, macos_adapter)
    validate_macos_provider_chain(
        candidate_root=candidate_root,
        extraction_roots=extraction_roots,
        authenticated=authenticated_by_role,
        macos_adapter_path=macos_adapter_path,
        macos_adapter=macos_adapter,
    )

    live_roots = {
        platform: mapping(
            adapter.get("livePredecessorAuthority"),
            f"{platform} live predecessor authority",
        ).get("liveReleaseChannelSha256")
        for platform, (_, adapter) in adapters.items()
    }
    if (
        len(set(live_roots.values())) != 1
        or next(iter(live_roots.values()), None) is None
    ):
        fail("platform inputs do not share one exact live predecessor root")

    artifacts: dict[str, Path] = {"macos": macos_artifact}
    for platform, (_, adapter) in adapters.items():
        identity = mapping(adapter.get("artifact"), f"{platform} artifact")
        file_name = string(
            identity.get("fileName"), f"{platform} artifact file name"
        )
        digest = string(
            identity.get("sha256"), f"{platform} artifact SHA-256", SHA256_RE
        )
        size = positive_integer(
            identity.get("sizeBytes"), f"{platform} artifact size"
        )
        if platform == "macos":
            actual_digest, actual_size = sha256_file(macos_artifact)
            if actual_digest != digest or actual_size != size:
                fail("decrypted macOS candidate differs from native evidence")
            continue
        source_artifact = locate_exact_artifact(
            candidate_root,
            file_name=file_name,
            sha256=digest,
            size_bytes=size,
        )
        destination = candidate_root / "artifacts" / platform / file_name
        copy_exact(source_artifact, destination)
        artifacts[platform] = destination

    signing_paths: dict[str, Path | None] = {}
    linux_package = mapping(
        linux_receipt.get("packageAuthority"),
        "Linux package authority",
    )
    linux_candidate_package = mapping(
        linux_package.get("candidate"), "Linux candidate package authority"
    )
    linux_signing_binding = mapping(
        linux_candidate_package.get("signingReceipt"),
        "Linux signing receipt binding",
    )
    linux_signing_path, _ = reference_from_binding(
        candidate_root,
        lifecycle["linux"][0].parent,
        linux_signing_binding,
        "Linux signing receipt",
    )
    signing_paths["linux"] = linux_signing_path
    windows_package = mapping(
        windows_receipt.get("packageAuthority"),
        "Windows package authority",
    )
    windows_candidate_package = mapping(
        windows_package.get("candidate"), "Windows candidate package authority"
    )
    windows_signing_binding = mapping(
        windows_candidate_package.get("signingReceipt"),
        "Windows signing receipt binding",
    )
    windows_signing_path, _ = reference_from_binding(
        candidate_root,
        lifecycle["windows"][0].parent,
        windows_signing_binding,
        "Windows signing receipt",
    )
    signing_paths["windows"] = windows_signing_path

    macos_aggregate_reference = mapping(
        mapping(
            macos_adapter.get("checks"), "macOS native checks"
        ).get("cleanInstall"),
        "macOS clean install",
    ).get("evidence")
    macos_aggregate_path, _ = reference_from_binding(
        candidate_root,
        candidate_root,
        mapping(macos_aggregate_reference, "macOS aggregate reference"),
        "macOS aggregate evidence",
    )
    macos_aggregate = load_json(
        macos_aggregate_path, "macOS aggregate evidence"
    )
    macos_signing_binding = mapping(
        mapping(macos_aggregate.get("references"), "macOS references").get(
            "signingReceipt"
        ),
        "macOS signing receipt binding",
    )
    macos_signing_path, _ = reference_from_binding(
        candidate_root,
        candidate_root,
        macos_signing_binding,
        "macOS signing receipt",
    )
    signing_paths["macos"] = macos_signing_path

    exit_contracts = {
        "windows": (
            json_contracts(extraction_roots["windows-export"]),
            "chummer6-ui.windows_desktop_exit_gate",
        ),
        "linux": (
            linux_contracts,
            "chummer6-ui.linux_desktop_exit_gate",
        ),
        "macos": (
            macos_receipt_contracts,
            "chummer6-ui.macos_desktop_exit_gate",
        ),
    }
    exit_paths: dict[str, Path] = {}
    for platform, (contracts, expected_contract) in exit_contracts.items():
        rid = {"windows": "win-x64", "linux": "linux-x64", "macos": "osx-arm64"}[
            platform
        ]
        artifact_digest, artifact_size = sha256_file(artifacts[platform])

        def matches_exit(
            payload: Mapping[str, Any],
            *,
            expected_platform: str = platform,
            expected_rid: str = rid,
            expected_digest: str = artifact_digest,
            expected_size: int = artifact_size,
        ) -> bool:
            if (
                payload.get("releaseVersion") != release_version
                or payload.get("channelId") != channel_id
            ):
                return False
            head = payload.get("head")
            if not isinstance(head, dict) or (
                head.get("platform") != expected_platform
                or head.get("rid") != expected_rid
                or head.get("app_key") != "avalonia"
            ):
                return False
            if expected_platform == "macos":
                artifact = payload.get("artifact")
                return isinstance(artifact, dict) and (
                    artifact.get("installer_sha256") == expected_digest
                    and artifact.get("installer_size_bytes") == expected_size
                )
            checks = payload.get("checks")
            key = f"release_channel_{expected_platform}_artifact"
            artifact = checks.get(key) if isinstance(checks, dict) else None
            return isinstance(artifact, dict) and (
                artifact.get("sha256") == expected_digest
                and artifact.get("sizeBytes") == expected_size
            )

        exit_path, _ = unique_contract(
            contracts,
            expected_contract,
            predicate=matches_exit,
            label=f"{platform} exit gate from its reserved provider role",
        )
        exit_paths[platform] = exit_path

    generated_at = datetime.now(UTC).replace(microsecond=0)
    expires_at = generated_at + timedelta(hours=24)
    platforms: dict[str, Any] = {}
    for platform, policy in assembler.POLICIES.items():
        platforms[platform] = {
            "rid": policy.rid,
            "artifact": artifact_reference(
                candidate_root, artifacts[platform], policy.artifact_id
            ),
            "exitGateReceipt": file_reference(
                candidate_root, exit_paths[platform]
            ),
            "signingReceipt": (
                None
                if signing_paths[platform] is None
                else file_reference(
                    candidate_root, signing_paths[platform]  # type: ignore[arg-type]
                )
            ),
            "nativeE2eReceipt": file_reference(
                candidate_root, adapters[platform][0]
            ),
        }

    manifest_path = (
        candidate_root / "GLOBAL_FLAGSHIP_CANDIDATE.generated.json"
    )
    atomic_json(
        manifest_path,
        {
            "contractName": assembler.CANDIDATE_CONTRACT,
            "contractVersion": 1,
            "generatedAt": format_time(generated_at),
            "expiresAt": format_time(expires_at),
            "candidateId": candidate_id,
            "generationId": generation_id,
            "releaseVersion": release_version,
            "previousReleaseVersion": previous_version,
            "channelId": channel_id,
            "source": {
                "repository": SOURCE_REPOSITORY,
                "ref": SOURCE_REF,
                "commit": source_sha,
            },
            "producer": {
                "actor": producer_actor,
                "artifactName": assembler.candidate_payload_artifact_name(
                    candidate_id, producer_run_id
                ),
                "workflow": PRODUCER_WORKFLOW,
                "runId": producer_run_id,
                "runAttempt": 1,
            },
            "providerActors": provider_actors,
            "platforms": platforms,
        },
    )
    return manifest_path


def make_read_only(root: Path) -> None:
    for path in sorted(root.rglob("*"), reverse=True):
        if path.is_symlink():
            fail("candidate output contains a symlink")
        if path.is_file():
            path.chmod(0o444)
        elif path.is_dir():
            path.chmod(0o555)
        else:
            fail("candidate output contains a special file")
    root.chmod(0o555)


def parse_spec(args: argparse.Namespace, role: str) -> ArtifactSpec:
    key = role.replace("-", "_")
    artifact_id = positive_integer(
        int(getattr(args, f"{key}_artifact_id")),
        f"{role} artifact ID",
    )
    name = string(getattr(args, f"{key}_artifact_name"), f"{role} name")
    digest = string(
        getattr(args, f"{key}_artifact_digest"), f"{role} digest"
    )
    if ARTIFACT_DIGEST_RE.fullmatch(digest) is None:
        fail(f"{role} digest must be sha256:<lowercase-hex>")
    workflow, prefix, platform = ROLE_POLICIES[role]
    return ArtifactSpec(
        role=role,
        artifact_id=artifact_id,
        name=name,
        digest=digest,
        workflow_path=workflow,
        name_prefix=prefix,
        platform=platform,
    )


def command_produce(args: argparse.Namespace) -> int:
    if args.assembly_confirmed != "true":
        fail("assembly confirmation is mandatory")
    if args.repository != SOURCE_REPOSITORY or args.ref != SOURCE_REF:
        fail("producer must execute in the reserved repository on main")
    if args.run_attempt != 1 or args.actor != args.triggering_actor:
        fail("producer rejects reruns and triggering-actor drift")
    string(args.source_sha, "producer source SHA", COMMIT_RE)
    string(args.actor, "producer actor", LOGIN_RE)
    positive_integer(args.run_id, "producer run ID")
    if (
        not args.macos_private_key.is_file()
        or args.macos_private_key.is_symlink()
    ):
        fail("macOS escrow private key must be a regular private file")
    string(
        args.macos_recipient_spki_sha256,
        "macOS recipient SPKI SHA-256",
        SHA256_RE,
    )

    output_root = args.output.resolve()
    if output_root.exists() or output_root.is_symlink():
        fail("output root must not already exist")
    output_root.mkdir(parents=True, mode=0o700)
    candidate_root = output_root / "candidate"
    candidate_root.mkdir(mode=0o700)
    proposal_path = output_root / "GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json"
    try:
        client = GitHubApi(os.environ.get("GITHUB_TOKEN", ""))
        source = authenticate_source(client, args.source_sha)
        specs = [parse_spec(args, role) for role in ROLE_POLICIES]
        incoming_root = output_root / ".incoming"
        incoming_root.mkdir(mode=0o700)
        authenticated = [
            authenticate_artifact(
                client,
                spec=spec,
                repository_id=int(source["repositoryId"]),
                source_sha=args.source_sha,
                now=datetime.now(UTC),
                archive_path=incoming_root / f"{spec.role}.zip",
            )
            for spec in specs
        ]
        validate_input_relationships(authenticated)
        validate_producer_actor_separation(args.actor, authenticated)
        manifest_path = assemble_candidate(
            candidate_root=candidate_root,
            candidate_id=args.candidate_id,
            generation_id=args.generation_id,
            channel_id=args.channel_id,
            source_sha=args.source_sha,
            producer_actor=args.actor,
            producer_run_id=args.run_id,
            authenticated=authenticated,
            macos_private_key=args.macos_private_key.resolve(),
            macos_recipient_spki_sha256=args.macos_recipient_spki_sha256,
        )
        subprocess.run(
            [
                sys.executable,
                str(REPO_ROOT / "scripts/release/assemble_global_flagship_release.py"),
                "propose",
                "--candidate",
                str(manifest_path),
                "--output",
                str(proposal_path),
            ],
            cwd=REPO_ROOT,
            check=True,
        )

        late = [
            {
                "role": item.spec.role,
                **reauthenticate_artifact(
                    client,
                    authenticated=item,
                    repository_id=int(source["repositoryId"]),
                    source_sha=args.source_sha,
                    now=datetime.now(UTC),
                ),
            }
            for item in authenticated
        ]
        late_source = authenticate_source(client, args.source_sha)
        if late_source != source:
            fail("protected current main drifted before candidate handoff")
        atomic_json(
            candidate_root
            / "GLOBAL_FLAGSHIP_PROVIDER_REAUTHENTICATION.generated.json",
            {
                "contractName": REAUTH_CONTRACT,
                "contractVersion": 1,
                "authenticatedAt": format_time(datetime.now(UTC)),
                "nonPublishing": True,
                "source": late_source,
                "artifacts": late,
            },
        )
        shutil.rmtree(incoming_root)
        make_read_only(output_root)
    except Exception:
        # Preserve the fail-closed work directory for runner diagnostics.  The
        # workflow uploads only on success, so partial output cannot escape.
        raise
    print(f"candidate={manifest_path}")
    print(f"proposal={proposal_path}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Authenticate seven exact native provider artifacts, assemble one "
            "immutable three-platform candidate, and propose it without "
            "publishing."
        )
    )
    parser.add_argument("--candidate-id", required=True)
    parser.add_argument("--generation-id", required=True)
    parser.add_argument("--channel-id", required=True)
    for role in ROLE_POLICIES:
        option = role.replace("-", "_")
        parser.add_argument(f"--{role}-artifact-id", dest=f"{option}_artifact_id", required=True)
        parser.add_argument(
            f"--{role}-artifact-name",
            dest=f"{option}_artifact_name",
            required=True,
        )
        parser.add_argument(
            f"--{role}-artifact-digest",
            dest=f"{option}_artifact_digest",
            required=True,
        )
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--run-id", required=True, type=int)
    parser.add_argument("--run-attempt", required=True, type=int)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--triggering-actor", required=True)
    parser.add_argument("--macos-private-key", required=True, type=Path)
    parser.add_argument(
        "--macos-recipient-spki-sha256", required=True
    )
    parser.add_argument(
        "--assembly-confirmed", choices=("true", "false"), required=True
    )
    parser.add_argument("--output", required=True, type=Path)
    parser.set_defaults(handler=command_produce)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.handler(args))
    except (ContractError, assembler.ContractError) as exc:
        print(f"global flagship candidate production blocked: {exc}", file=sys.stderr)
        return 2
    except subprocess.CalledProcessError as exc:
        print(
            "global flagship candidate production blocked: validated helper "
            f"failed with exit {exc.returncode}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
