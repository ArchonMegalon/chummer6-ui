#!/usr/bin/env python3
"""Authenticate flagship approval provenance without publishing release bytes.

The local assembler deliberately emits ``provenanceAuthenticated: false``.
This verifier is the next trust layer: it revalidates that exact local
proposal/final-receipt graph, then authenticates the three approval runs,
approval-history reviewers, source-controlled policy, artifact archives, and
current main-branch governance through read-only GitHub APIs.

The verifier never reads release artifacts, signing credentials, deployment
credentials, or publication credentials.  Its only outputs are a local,
write-once handoff receipt and, when invoked through the accompanying workflow,
an Actions artifact containing only that receipt.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import hmac
import io
import json
import os
import re
import stat
import sys
import urllib.error
import urllib.request
import zipfile
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Mapping, Protocol, Sequence
from urllib.parse import quote, urlsplit

import assemble_global_flagship_release as assembler


HANDOFF_CONTRACT = (
    "chummer6-ui.global-flagship-provider-authenticated-handoff.v1"
)
HANDOFF_CONTRACT_VERSION = 1
INPUT_ARTIFACT_NAME = (
    "global-flagship-release-provider-authentication-input"
)
INPUT_BUNDLE_FILE_NAME = f"{INPUT_ARTIFACT_NAME}.zip"
REVIEWER_POLICY_PATH = ".github/global-flagship-reviewer-policy.json"
SOURCE_BRANCH = "main"
SOURCE_REF = "refs/heads/main"
API_ROOT = "https://api.github.com"
API_VERSION = "2022-11-28"

MAX_API_JSON_BYTES = 8 * 1024 * 1024
MAX_INPUT_ARTIFACT_BYTES = 16 * 1024 * 1024
MAX_INPUT_BUNDLE_BYTES = 12 * 1024 * 1024
MAX_APPROVAL_ARTIFACT_BYTES = 2 * 1024 * 1024
MAX_WORKFLOW_BYTES = 1024 * 1024
HTTP_TIMEOUT_SECONDS = 30

ARTIFACT_DIGEST_RE = re.compile(r"^sha256:([0-9a-f]{64})$")
GIT_BLOB_SHA_RE = re.compile(r"^[0-9a-f]{40}$")
LINK_NEXT_RE = re.compile(r'(?:^|,)\s*<[^>]+>;\s*rel="next"(?:\s*;|,|$)')
APPROVAL_ARTIFACT_RE = re.compile(
    r"^global-flagship-release-approval-"
    r"(quality|release|security)-([1-9][0-9]*)-1$"
)

HANDOFF_AUTHORITY_LEVEL = (
    "github-provider-authenticated-approval-and-governance"
)
HANDOFF_PROVENANCE_SCOPE = (
    "approval-workflow-runs-receipts-reviewers-source-policy-"
    "and-current-main-governance"
)
HANDOFF_SIDE_EFFECTS = (
    "write_immutable_handoff_receipt",
    "upload_handoff_receipt_only",
)
FINAL_REQUIRED_NEXT_AUTHORITY = (
    "A separate protected publication transaction must revalidate this "
    "handoff, the final receipt, every bound release artifact byte, platform "
    "signing/notarization authority, and the live publication target before "
    "any upload or activation."
)
ASSEMBLER_FINAL_REQUIRED_NEXT_AUTHORITY = (
    "A protected workflow must authenticate every referenced GitHub run, "
    "artifact, signer identity, and approval actor via the provider API. A "
    "separate immutable publication transaction must then revalidate that "
    "authenticated handoff and all bound bytes before any upload or "
    "activation."
)


class ContractError(RuntimeError):
    """Raised when provider evidence cannot satisfy the exact contract."""


def fail(message: str) -> None:
    raise ContractError(message)


def require_mapping(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    return value


def require_list(value: object, label: str) -> list[Any]:
    if not isinstance(value, list):
        fail(f"{label} must be an array")
    return value


def require_key(mapping: Mapping[str, Any], key: str, label: str) -> Any:
    if key not in mapping:
        fail(f"{label}.{key} is missing")
    return mapping[key]


def require_api_string(
    value: object,
    label: str,
    pattern: re.Pattern[str] | None = None,
) -> str:
    return assembler.require_string(value, label, pattern)


def require_api_integer(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        fail(f"{label} must be an integer")
    return assembler.require_positive_integer(value, label)


def require_api_boolean(value: object, label: str) -> bool:
    if type(value) is not bool:
        fail(f"{label} must be a boolean")
    return value


def require_value(
    actual: object,
    expected: object,
    label: str,
) -> None:
    if type(actual) is not type(expected) or actual != expected:
        fail(f"{label} does not match the authenticated authority")


def json_load_any(data: bytes, label: str) -> Any:
    try:
        decoded = data.decode("utf-8")
    except UnicodeDecodeError:
        fail(f"{label} is not UTF-8 JSON")
    try:
        return json.loads(
            decoded,
            object_pairs_hook=assembler.duplicate_rejecting_object,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite JSON token {token}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git_blob_sha1(data: bytes) -> str:
    header = f"blob {len(data)}\0".encode("ascii")
    return hashlib.sha1(header + data).hexdigest()  # noqa: S324 - Git object ID


def snapshot_bytes(data: bytes, relative_path: str) -> assembler.Snapshot:
    portable = assembler.safe_relative_path(
        relative_path, "in-memory snapshot path"
    )
    return assembler.Snapshot(
        path=Path(portable),
        relative_path=portable,
        sha256=sha256_bytes(data),
        size_bytes=len(data),
        data=data,
    )


def parse_api_time(value: object, label: str) -> datetime:
    return assembler.parse_time(value, label)


def require_not_expired(
    value: object,
    *,
    now: datetime,
    label: str,
) -> str:
    parsed = parse_api_time(value, label)
    if parsed <= now:
        fail(f"{label} is expired")
    return assembler.format_time(parsed)


def header_value(headers: Mapping[str, str], name: str) -> str | None:
    lowered = name.casefold()
    for key, value in headers.items():
        if key.casefold() == lowered:
            return value
    return None


def require_unpaginated(headers: Mapping[str, str], label: str) -> None:
    link = header_value(headers, "Link")
    if link and LINK_NEXT_RE.search(link):
        fail(f"{label} is paginated; refusing an incomplete provider view")
    next_page = header_value(headers, "X-Next-Page")
    if next_page:
        fail(f"{label} advertises an unconsumed next page")


@dataclass(frozen=True)
class JsonResponse:
    value: Any
    headers: Mapping[str, str]


class ProviderReader(Protocol):
    def get_json(self, path: str) -> JsonResponse:
        """Fetch one JSON resource without following redirects."""

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        """Fetch one artifact ZIP through the provider's one-hop redirect."""


class RestrictedAdministrationReader:
    """Confine Administration authority to one exact branch-protection read."""

    def __init__(self, delegate: ProviderReader) -> None:
        self._delegate = delegate

    @staticmethod
    def _allowed_path() -> str:
        return (
            f"/repos/{assembler.SOURCE_REPOSITORY}/branches/"
            f"{SOURCE_BRANCH}/protection"
        )

    def get_json(self, path: str) -> JsonResponse:
        if path != self._allowed_path():
            fail(
                "Administration authority was requested outside the exact "
                "main branch-protection endpoint"
            )
        return self._delegate.get_json(path)

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        del artifact_id, max_bytes
        fail("Administration authority cannot download artifacts")


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(  # type: ignore[override]
        self,
        req: urllib.request.Request,
        fp: Any,
        code: int,
        msg: str,
        headers: Any,
        newurl: str,
    ) -> None:
        return None


def validate_artifact_redirect(location: str) -> str:
    parsed = urlsplit(location)
    try:
        port = parsed.port
    except ValueError:
        fail("artifact download redirect has a malformed port")
    if (
        parsed.scheme != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.fragment
        or port not in {None, 443}
    ):
        fail("artifact download redirect is not a credential-free HTTPS URL")
    hostname = parsed.hostname.casefold()
    if not (
        hostname.endswith(".blob.core.windows.net")
        or hostname == "objects.githubusercontent.com"
    ):
        fail("artifact download redirect targets an unapproved storage host")
    return location


class GitHubApi:
    """Minimal read-only GitHub client with controlled redirect handling."""

    def __init__(
        self,
        token: str,
        *,
        repository: str = assembler.SOURCE_REPOSITORY,
    ) -> None:
        if (
            not token
            or len(token) > 4096
            or "\n" in token
            or "\r" in token
            or "\x00" in token
        ):
            fail("GitHub API token is missing or malformed")
        self._repository = assembler.require_string(
            repository, "GitHub API repository", assembler.REPOSITORY_RE
        )
        self._token = token
        self._api_opener = urllib.request.build_opener(_NoRedirect())
        self._storage_opener = urllib.request.build_opener(_NoRedirect())

    def _api_request(self, path: str) -> urllib.request.Request:
        if not path.startswith("/") or "://" in path or "#" in path:
            fail("internal GitHub API path is invalid")
        return urllib.request.Request(
            f"{API_ROOT}{path}",
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self._token}",
                "User-Agent": "chummer6-global-flagship-provider-verifier/1",
                "X-GitHub-Api-Version": API_VERSION,
            },
            method="GET",
        )

    @staticmethod
    def _bounded_read(response: Any, maximum: int, label: str) -> bytes:
        content_length = response.headers.get("Content-Length")
        if content_length is not None:
            try:
                advertised = int(content_length)
            except ValueError:
                fail(f"{label} has a malformed Content-Length")
            if advertised < 0 or advertised > maximum:
                fail(f"{label} exceeds the {maximum}-byte boundary")
        data = response.read(maximum + 1)
        if len(data) > maximum:
            fail(f"{label} exceeds the {maximum}-byte boundary")
        return data

    def get_json(self, path: str) -> JsonResponse:
        request = self._api_request(path)
        try:
            with self._api_opener.open(
                request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                if response.status != 200 or response.geturl() != request.full_url:
                    fail("GitHub API returned an unexpected status or redirect")
                media_type = response.headers.get_content_type()
                if media_type not in {
                    "application/json",
                    "application/vnd.github+json",
                }:
                    fail("GitHub API returned an unexpected JSON media type")
                data = self._bounded_read(
                    response, MAX_API_JSON_BYTES, "GitHub API JSON response"
                )
                headers = dict(response.headers.items())
        except ContractError:
            raise
        except urllib.error.HTTPError as exc:
            fail(f"GitHub API read failed closed with HTTP {exc.code}")
        except (urllib.error.URLError, TimeoutError, OSError):
            fail("GitHub API read failed closed")
        return JsonResponse(
            value=json_load_any(data, "GitHub API response"),
            headers=headers,
        )

    def get_artifact_archive(self, artifact_id: int, max_bytes: int) -> bytes:
        artifact_id = assembler.require_positive_integer(
            artifact_id, "artifact download ID"
        )
        path = (
            f"/repos/{self._repository}/actions/artifacts/"
            f"{artifact_id}/zip"
        )
        request = self._api_request(path)
        location: str | None = None
        try:
            self._api_opener.open(request, timeout=HTTP_TIMEOUT_SECONDS)
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
            fail("artifact download endpoint omitted the redirect location")
        storage_url = validate_artifact_redirect(location)
        storage_request = urllib.request.Request(
            storage_url,
            headers={
                "Accept": "application/octet-stream",
                "User-Agent": "chummer6-global-flagship-provider-verifier/1",
            },
            method="GET",
        )
        try:
            with self._storage_opener.open(
                storage_request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                if response.status != 200 or response.geturl() != storage_url:
                    fail(
                        "artifact storage returned an unexpected status or "
                        "additional redirect"
                    )
                if response.headers.get("Location"):
                    fail("artifact storage attempted an additional redirect")
                return self._bounded_read(
                    response, max_bytes, "artifact archive"
                )
        except ContractError:
            raise
        except urllib.error.HTTPError as exc:
            fail(f"artifact storage read failed closed with HTTP {exc.code}")
        except (urllib.error.URLError, TimeoutError, OSError):
            fail("artifact storage read failed closed")


def zip_member_mode(info: zipfile.ZipInfo) -> int:
    return (info.external_attr >> 16) & 0xFFFF


def read_exact_zip(
    archive: bytes,
    *,
    expected_names: set[str] | None,
    maximum_entries: int,
    maximum_total_bytes: int,
    label: str,
) -> dict[str, bytes]:
    if not archive or len(archive) > maximum_total_bytes:
        fail(f"{label} has an invalid archive size")
    try:
        with zipfile.ZipFile(io.BytesIO(archive), "r") as handle:
            infos = handle.infolist()
            if not infos or len(infos) > maximum_entries:
                fail(f"{label} has an invalid entry count")
            names = [info.filename for info in infos]
            if len(set(names)) != len(names):
                fail(f"{label} contains duplicate entry names")
            if expected_names is not None and set(names) != expected_names:
                fail(f"{label} does not contain the exact expected entries")
            total = 0
            result: dict[str, bytes] = {}
            for info in infos:
                name = assembler.safe_relative_path(
                    info.filename, f"{label} entry name"
                )
                if name != info.filename or info.is_dir():
                    fail(f"{label} contains a non-canonical entry")
                mode = zip_member_mode(info)
                file_type = stat.S_IFMT(mode)
                if file_type not in {0, stat.S_IFREG}:
                    fail(f"{label} contains a non-regular entry")
                if info.flag_bits & 0x1:
                    fail(f"{label} contains an encrypted entry")
                if info.file_size < 1:
                    fail(f"{label} contains an empty entry")
                total += info.file_size
                if total > maximum_total_bytes:
                    fail(f"{label} expands beyond its byte boundary")
                with handle.open(info, "r") as entry:
                    data = entry.read(info.file_size + 1)
                    trailing = entry.read(1)
                if (
                    trailing
                    or len(data) != info.file_size
                    or len(data) > maximum_total_bytes
                ):
                    fail(f"{label} entry size changed during extraction")
                result[name] = data
            return result
    except ContractError:
        raise
    except (zipfile.BadZipFile, RuntimeError, OSError, EOFError):
        fail(f"{label} is not a valid bounded ZIP archive")


@dataclass(frozen=True)
class LocalBundle:
    proposal: assembler.Snapshot
    candidate: assembler.Snapshot
    final_receipt: assembler.Snapshot
    approvals: Mapping[str, assembler.Snapshot]


def one_prefixed_entry(
    entries: Mapping[str, bytes],
    prefix: str,
    label: str,
) -> tuple[str, bytes]:
    matches = [
        (name, data)
        for name, data in entries.items()
        if PurePosixPath(name).parts[:-1] == tuple(PurePosixPath(prefix).parts)
    ]
    if len(matches) != 1:
        fail(f"input bundle must contain exactly one {label}")
    name, data = matches[0]
    basename = PurePosixPath(name).name
    if assembler.FILE_NAME_RE.fullmatch(basename) is None:
        fail(f"input bundle {label} has a non-portable file name")
    return basename, data


def read_local_bundle(bundle_zip: bytes) -> LocalBundle:
    entries = read_exact_zip(
        bundle_zip,
        expected_names=None,
        maximum_entries=6,
        maximum_total_bytes=MAX_INPUT_BUNDLE_BYTES,
        label="provider input bundle",
    )
    final_name = "final-receipt.json"
    if final_name not in entries:
        fail("input bundle is missing final-receipt.json")
    proposal_name, proposal_data = one_prefixed_entry(
        entries, "proposal", "proposal"
    )
    candidate_name, candidate_data = one_prefixed_entry(
        entries, "candidate", "candidate manifest"
    )
    approvals: dict[str, assembler.Snapshot] = {}
    expected_names = {
        final_name,
        f"proposal/{proposal_name}",
        f"candidate/{candidate_name}",
    }
    for role in assembler.REQUIRED_APPROVAL_ROLES:
        basename, data = one_prefixed_entry(
            entries, f"approvals/{role}", f"{role} approval"
        )
        path = f"approvals/{role}/{basename}"
        expected_names.add(path)
        approvals[role] = snapshot_bytes(data, basename)
    if set(entries) != expected_names:
        fail("provider input bundle contains an unexpected entry")
    return LocalBundle(
        proposal=snapshot_bytes(proposal_data, proposal_name),
        candidate=snapshot_bytes(candidate_data, candidate_name),
        final_receipt=snapshot_bytes(entries[final_name], final_name),
        approvals=approvals,
    )


def require_reference_projection(
    manifest_reference: object,
    proposal_binding: object,
    label: str,
) -> None:
    manifest = assembler.exact_dict(
        manifest_reference, {"path", "sha256", "sizeBytes"}, label
    )
    assembler.safe_relative_path(manifest["path"], f"{label}.path")
    projection = require_mapping(proposal_binding, f"{label} projection")
    require_value(
        manifest["path"],
        projection.get("relativePath"),
        f"{label}.relativePath",
    )
    require_value(
        assembler.require_sha256(manifest["sha256"], f"{label}.sha256"),
        projection.get("sha256"),
        f"{label}.sha256",
    )
    require_value(
        assembler.require_positive_integer(
            manifest["sizeBytes"], f"{label}.sizeBytes"
        ),
        projection.get("sizeBytes"),
        f"{label}.sizeBytes",
    )


def validate_candidate_manifest_binding(
    snapshot: assembler.Snapshot,
    proposal: Mapping[str, Any],
) -> dict[str, Any]:
    """Revalidate candidate fields that do not require release artifact bytes."""

    manifest = assembler.exact_dict(
        assembler.load_json_bytes(snapshot.data, "candidate manifest"),
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "expiresAt",
            "candidateId",
            "generationId",
            "releaseVersion",
            "previousReleaseVersion",
            "channelId",
            "source",
            "producer",
            "providerActors",
            "platforms",
        },
        "candidate manifest",
    )
    require_value(
        manifest["contractName"],
        assembler.CANDIDATE_CONTRACT,
        "candidate manifest contractName",
    )
    require_value(
        manifest["contractVersion"],
        assembler.CONTRACT_VERSION,
        "candidate manifest contractVersion",
    )
    candidate = require_mapping(proposal["candidate"], "proposal.candidate")
    direct_fields = (
        "candidateId",
        "generationId",
        "releaseVersion",
        "previousReleaseVersion",
        "channelId",
        "source",
        "producer",
        "providerActors",
        "generatedAt",
        "expiresAt",
    )
    for key in direct_fields:
        require_value(
            manifest[key], candidate.get(key), f"candidate manifest {key}"
        )
    manifest_source = assembler.exact_dict(
        manifest["source"],
        {"repository", "ref", "commit"},
        "candidate manifest source",
    )
    require_value(
        manifest_source["repository"],
        assembler.SOURCE_REPOSITORY,
        "candidate manifest source repository",
    )
    require_value(
        manifest_source["ref"],
        assembler.RELEASE_APPROVAL_REF,
        "candidate manifest source ref",
    )
    assembler.require_string(
        manifest_source["commit"],
        "candidate manifest source commit",
        assembler.COMMIT_RE,
    )
    generated = assembler.parse_time(
        manifest["generatedAt"], "candidate manifest generatedAt"
    )
    expires = assembler.parse_time(
        manifest["expiresAt"], "candidate manifest expiresAt"
    )
    proposal_generated = assembler.parse_time(
        proposal["generatedAt"], "proposal generatedAt"
    )
    proposal_expires = assembler.parse_time(
        proposal["expiresAt"], "proposal expiresAt"
    )
    if (
        generated > proposal_generated
        or expires < proposal_expires
        or expires <= generated
        or expires
        > generated + timedelta(seconds=assembler.MAX_EVIDENCE_AGE_SECONDS)
    ):
        fail("candidate manifest validity window does not contain the proposal")
    producer = assembler.exact_dict(
        manifest["producer"],
        {"actor", "artifactName", "workflow", "runId", "runAttempt"},
        "candidate manifest producer",
    )
    producer_actor = assembler.require_string(
        producer["actor"],
        "candidate manifest producer.actor",
        assembler.GITHUB_LOGIN_RE,
    )
    require_value(
        assembler.require_string(
            producer["workflow"],
            "candidate manifest producer.workflow",
            assembler.WORKFLOW_RE,
        ),
        assembler.CANDIDATE_PRODUCER_WORKFLOW,
        "candidate manifest producer.workflow",
    )
    producer_run_id = assembler.require_positive_integer(
        producer["runId"], "candidate manifest producer.runId"
    )
    require_value(
        assembler.require_positive_integer(
            producer["runAttempt"],
            "candidate manifest producer.runAttempt",
        ),
        1,
        "candidate manifest producer.runAttempt",
    )
    assembler.validate_candidate_payload_artifact_name(
        producer["artifactName"],
        candidate_id=manifest["candidateId"],
        producer_run_id=producer_run_id,
    )
    provider_actors = assembler.exact_dict(
        manifest["providerActors"],
        set(assembler.PROVIDER_ACTOR_ROLES),
        "candidate manifest providerActors",
    )
    for role in assembler.PROVIDER_ACTOR_ROLES:
        provider_actor = assembler.require_string(
            provider_actors[role],
            f"candidate manifest providerActors.{role}",
            assembler.GITHUB_LOGIN_RE,
        )
        if provider_actor.casefold() == producer_actor.casefold():
            fail(
                "candidate manifest producer overlaps an authenticated "
                "provider actor"
            )

    manifest_platforms = assembler.exact_dict(
        manifest["platforms"],
        set(assembler.PLATFORMS),
        "candidate manifest platforms",
    )
    proposal_platforms = assembler.exact_dict(
        proposal["platforms"],
        set(assembler.PLATFORMS),
        "proposal platforms",
    )
    for platform in assembler.PLATFORMS:
        manifest_platform = assembler.exact_dict(
            manifest_platforms[platform],
            {
                "rid",
                "artifact",
                "exitGateReceipt",
                "signingReceipt",
                "nativeE2eReceipt",
            },
            f"candidate manifest platforms.{platform}",
        )
        projected_platform = require_mapping(
            proposal_platforms[platform], f"proposal platforms.{platform}"
        )
        require_value(
            manifest_platform["rid"],
            projected_platform.get("rid"),
            f"candidate manifest platforms.{platform}.rid",
        )
        artifact = assembler.exact_dict(
            manifest_platform["artifact"],
            {"artifactId", "fileName", "path", "sha256", "sizeBytes"},
            f"candidate manifest platforms.{platform}.artifact",
        )
        projected_artifact = require_mapping(
            projected_platform.get("artifact"),
            f"proposal platforms.{platform}.artifact",
        )
        artifact_pairs = {
            "artifactId": "artifactId",
            "fileName": "fileName",
            "path": "relativePath",
            "sha256": "sha256",
            "sizeBytes": "sizeBytes",
        }
        for manifest_key, projection_key in artifact_pairs.items():
            require_value(
                artifact[manifest_key],
                projected_artifact.get(projection_key),
                (
                    f"candidate manifest platforms.{platform}.artifact."
                    f"{manifest_key}"
                ),
            )
        assembler.safe_relative_path(
            artifact["path"],
            f"candidate manifest platforms.{platform}.artifact.path",
        )
        assembler.require_sha256(
            artifact["sha256"],
            f"candidate manifest platforms.{platform}.artifact.sha256",
        )
        assembler.require_positive_integer(
            artifact["sizeBytes"],
            f"candidate manifest platforms.{platform}.artifact.sizeBytes",
        )
        for manifest_key, projection_key in (
            ("exitGateReceipt", "exitGateReceipt"),
            ("nativeE2eReceipt", "nativeE2eReceipt"),
        ):
            require_reference_projection(
                manifest_platform[manifest_key],
                projected_platform.get(projection_key),
                f"candidate manifest platforms.{platform}.{manifest_key}",
            )
        signing = manifest_platform["signingReceipt"]
        projected_signing = projected_platform.get("signingReceipt")
        if signing is None or projected_signing is None:
            require_value(
                signing,
                projected_signing,
                f"candidate manifest platforms.{platform}.signingReceipt",
            )
        else:
            require_reference_projection(
                signing,
                projected_signing,
                f"candidate manifest platforms.{platform}.signingReceipt",
            )
    return manifest


def validate_approval_set(
    bundle: LocalBundle,
    *,
    proposal: Mapping[str, Any],
    now: datetime,
) -> list[dict[str, Any]]:
    projections: list[dict[str, Any]] = []
    for expected_role in assembler.REQUIRED_APPROVAL_ROLES:
        snapshot = bundle.approvals[expected_role]
        projection = assembler.validate_approval(
            snapshot,
            proposal_snapshot=bundle.proposal,
            proposal=proposal,
            now=now,
        )
        require_value(
            projection["role"],
            expected_role,
            f"{expected_role} approval role",
        )
        projections.append(projection)
    actors = [str(item["actor"]).casefold() for item in projections]
    if len(set(actors)) != len(actors):
        fail("the three approval actors are not distinct")
    run_ids = [int(item["authority"]["runId"]) for item in projections]
    if len(set(run_ids)) != len(run_ids):
        fail("the three approval workflow run IDs are not distinct")
    policies = {
        (
            str(item["reviewerPolicy"]["sha256"]),
            int(item["reviewerPolicy"]["sizeBytes"]),
        )
        for item in projections
    }
    if len(policies) != 1:
        fail("the three approvals do not bind one exact reviewer policy")
    projections.sort(key=lambda item: str(item["role"]))
    return projections


def validate_final_receipt(
    bundle: LocalBundle,
    *,
    proposal: Mapping[str, Any],
    approvals: Sequence[Mapping[str, Any]],
    now: datetime,
) -> dict[str, Any]:
    final = assembler.exact_dict(
        assembler.load_json_bytes(
            bundle.final_receipt.data, "global flagship final receipt"
        ),
        {
            "contractName",
            "contractVersion",
            "generatedAt",
            "status",
            "candidate",
            "candidateManifest",
            "proposal",
            "platforms",
            "approvals",
            "externalRequirements",
            "authorityLevel",
            "provenanceAuthenticated",
            "nonPublishing",
            "publicationAuthorized",
            "allowedSideEffects",
            "handoff",
        },
        "global flagship final receipt",
    )
    require_value(
        final["contractName"],
        assembler.FINAL_RECEIPT_CONTRACT,
        "final receipt contractName",
    )
    require_value(
        final["contractVersion"],
        assembler.CONTRACT_VERSION,
        "final receipt contractVersion",
    )
    require_value(final["status"], "passed", "final receipt status")
    generated = assembler.parse_time(
        final["generatedAt"], "final receipt generatedAt"
    )
    if generated > now + timedelta(seconds=assembler.MAX_CLOCK_SKEW_SECONDS):
        fail("final receipt generatedAt is too far in the future")
    if generated > assembler.parse_time(
        proposal["expiresAt"], "proposal expiresAt"
    ):
        fail("final receipt was generated after proposal expiry")
    latest_approval = max(
        assembler.parse_time(item["approvedAt"], "approval approvedAt")
        for item in approvals
    )
    if generated < latest_approval:
        fail("final receipt predates an approval")
    require_value(final["candidate"], proposal["candidate"], "final candidate")
    require_value(
        final["candidateManifest"],
        assembler.binding(bundle.candidate),
        "final candidate manifest binding",
    )
    require_value(
        final["proposal"],
        assembler.binding(
            bundle.proposal, contractName=assembler.PROPOSAL_CONTRACT
        ),
        "final proposal binding",
    )
    require_value(final["platforms"], proposal["platforms"], "final platforms")
    require_value(
        final["approvals"], list(approvals), "final approval projections"
    )
    for field in (
        "externalRequirements",
        "authorityLevel",
        "provenanceAuthenticated",
        "nonPublishing",
        "publicationAuthorized",
        "allowedSideEffects",
    ):
        require_value(final[field], proposal[field], f"final {field}")
    handoff = assembler.exact_dict(
        final["handoff"],
        {"eligibleForSeparatePublicationReview", "requiredNextAuthority"},
        "final receipt handoff",
    )
    if handoff["eligibleForSeparatePublicationReview"] is not True:
        fail("final receipt is not eligible for separate publication review")
    require_value(
        handoff["requiredNextAuthority"],
        ASSEMBLER_FINAL_REQUIRED_NEXT_AUTHORITY,
        "final receipt handoff requiredNextAuthority",
    )
    return final


@dataclass(frozen=True)
class LocalValidation:
    proposal: Mapping[str, Any]
    candidate_manifest: Mapping[str, Any]
    final_receipt: Mapping[str, Any]
    approvals: Sequence[Mapping[str, Any]]


def validate_local_bundle(
    bundle: LocalBundle,
    *,
    now: datetime,
) -> LocalValidation:
    proposal = assembler.validate_proposal(bundle.proposal, now=now)
    expected_candidate = require_mapping(
        proposal["candidateManifest"], "proposal candidateManifest"
    )
    require_value(
        assembler.binding(bundle.candidate),
        expected_candidate,
        "proposal candidate manifest binding",
    )
    candidate_manifest = validate_candidate_manifest_binding(
        bundle.candidate, proposal
    )
    approvals = validate_approval_set(
        bundle, proposal=proposal, now=now
    )
    final_receipt = validate_final_receipt(
        bundle,
        proposal=proposal,
        approvals=approvals,
        now=now,
    )
    return LocalValidation(
        proposal=proposal,
        candidate_manifest=candidate_manifest,
        final_receipt=final_receipt,
        approvals=approvals,
    )


def deterministic_zip_entry(name: str, data: bytes) -> tuple[zipfile.ZipInfo, bytes]:
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_STORED
    info.create_system = 3
    info.external_attr = (stat.S_IFREG | 0o444) << 16
    return info, data


def build_input_bundle(bundle: LocalBundle, *, now: datetime) -> bytes:
    validate_local_bundle(bundle, now=now)
    entries: list[tuple[str, bytes]] = [
        (
            f"proposal/{bundle.proposal.relative_path}",
            bundle.proposal.data or b"",
        ),
        (
            f"candidate/{bundle.candidate.relative_path}",
            bundle.candidate.data or b"",
        ),
        (
            "final-receipt.json",
            bundle.final_receipt.data or b"",
        ),
    ]
    for role in assembler.REQUIRED_APPROVAL_ROLES:
        approval = bundle.approvals[role]
        entries.append(
            (
                f"approvals/{role}/{approval.relative_path}",
                approval.data or b"",
            )
        )
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w") as archive:
        for name, data in sorted(entries):
            info, entry_data = deterministic_zip_entry(name, data)
            archive.writestr(info, entry_data)
    data = output.getvalue()
    if len(data) > MAX_INPUT_BUNDLE_BYTES:
        fail("provider input bundle exceeds its byte boundary")
    return data


def write_once(path: Path, data: bytes, *, mode: int = 0o444) -> None:
    target = path.absolute()
    target.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(
            target,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL,
            mode,
        )
    except FileExistsError:
        fail(f"refusing to replace existing output: {target}")
    except OSError:
        fail(f"output cannot be created safely: {target}")
    complete = False
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
            os.fchmod(handle.fileno(), mode)
        complete = True
    finally:
        if not complete:
            try:
                target.unlink()
            except FileNotFoundError:
                pass


def immutable_json_bytes(payload: Mapping[str, Any]) -> bytes:
    return (
        json.dumps(
            payload,
            indent=2,
            sort_keys=True,
            ensure_ascii=True,
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def repository_api_path(suffix: str) -> str:
    if suffix and not suffix.startswith("/"):
        fail("internal repository API suffix is invalid")
    return f"/repos/{assembler.SOURCE_REPOSITORY}{suffix}"


def validate_repository(
    response: JsonResponse,
) -> dict[str, Any]:
    require_unpaginated(response.headers, "repository response")
    repository = require_mapping(response.value, "repository response")
    repository_id = require_api_integer(
        require_key(repository, "id", "repository"),
        "repository.id",
    )
    require_value(
        require_key(repository, "full_name", "repository"),
        assembler.SOURCE_REPOSITORY,
        "repository.full_name",
    )
    require_value(
        require_key(repository, "default_branch", "repository"),
        SOURCE_BRANCH,
        "repository.default_branch",
    )
    if require_api_boolean(
        require_key(repository, "archived", "repository"),
        "repository.archived",
    ):
        fail("repository is archived")
    if require_api_boolean(
        require_key(repository, "disabled", "repository"),
        "repository.disabled",
    ):
        fail("repository is disabled")
    return {
        "id": repository_id,
        "fullName": assembler.SOURCE_REPOSITORY,
        "defaultBranch": SOURCE_BRANCH,
    }


def validate_user(
    value: object,
    *,
    expected_login: str,
    expected_id: int | None = None,
    label: str,
) -> dict[str, Any]:
    user = require_mapping(value, label)
    login = require_api_string(
        require_key(user, "login", label),
        f"{label}.login",
        assembler.GITHUB_LOGIN_RE,
    )
    if login.casefold() != expected_login.casefold():
        fail(f"{label}.login does not match the approval receipt")
    require_value(
        require_key(user, "type", label),
        "User",
        f"{label}.type",
    )
    user_id = require_api_integer(
        require_key(user, "id", label), f"{label}.id"
    )
    if expected_id is not None:
        require_value(user_id, expected_id, f"{label}.id")
    return {"id": user_id, "login": login, "type": "User"}


def normalize_run_workflow_path(value: object, label: str) -> str:
    raw = require_api_string(value, label)
    path, marker, suffix = raw.partition("@")
    require_value(path, assembler.APPROVAL_WORKFLOW, label)
    if marker and suffix not in {SOURCE_BRANCH, SOURCE_REF}:
        fail(f"{label} uses an unexpected workflow ref suffix")
    return path


def validate_run_shape(
    value: object,
    *,
    approval: Mapping[str, Any],
    repository_id: int,
    label: str,
) -> dict[str, Any]:
    run = require_mapping(value, label)
    authority = require_mapping(approval["authority"], "approval authority")
    run_id = int(authority["runId"])
    require_value(
        require_api_integer(require_key(run, "id", label), f"{label}.id"),
        run_id,
        f"{label}.id",
    )
    require_value(
        require_api_integer(
            require_key(run, "run_attempt", label),
            f"{label}.run_attempt",
        ),
        1,
        f"{label}.run_attempt",
    )
    require_value(
        require_key(run, "event", label),
        "workflow_dispatch",
        f"{label}.event",
    )
    require_value(
        require_key(run, "status", label),
        "completed",
        f"{label}.status",
    )
    require_value(
        require_key(run, "conclusion", label),
        "success",
        f"{label}.conclusion",
    )
    require_value(
        require_key(run, "head_branch", label),
        SOURCE_BRANCH,
        f"{label}.head_branch",
    )
    source_sha = str(authority["sha"])
    require_value(
        require_key(run, "head_sha", label),
        source_sha,
        f"{label}.head_sha",
    )
    workflow_path = normalize_run_workflow_path(
        require_key(run, "path", label), f"{label}.path"
    )
    workflow_id = require_api_integer(
        require_key(run, "workflow_id", label), f"{label}.workflow_id"
    )
    actor_login = str(approval["actor"])
    actor = validate_user(
        require_key(run, "actor", label),
        expected_login=actor_login,
        label=f"{label}.actor",
    )
    triggering = validate_user(
        require_key(run, "triggering_actor", label),
        expected_login=actor_login,
        label=f"{label}.triggering_actor",
    )
    require_value(
        triggering["id"], actor["id"], f"{label} triggering actor identity"
    )
    repository = require_mapping(
        require_key(run, "repository", label), f"{label}.repository"
    )
    require_value(
        require_api_integer(
            require_key(repository, "id", f"{label}.repository"),
            f"{label}.repository.id",
        ),
        repository_id,
        f"{label}.repository.id",
    )
    require_value(
        require_key(repository, "full_name", f"{label}.repository"),
        assembler.SOURCE_REPOSITORY,
        f"{label}.repository.full_name",
    )
    head_repository = require_mapping(
        require_key(run, "head_repository", label),
        f"{label}.head_repository",
    )
    require_value(
        require_api_integer(
            require_key(head_repository, "id", f"{label}.head_repository"),
            f"{label}.head_repository.id",
        ),
        repository_id,
        f"{label}.head_repository.id",
    )
    require_value(
        require_key(
            head_repository, "full_name", f"{label}.head_repository"
        ),
        assembler.SOURCE_REPOSITORY,
        f"{label}.head_repository.full_name",
    )
    referenced = require_list(
        require_key(run, "referenced_workflows", label),
        f"{label}.referenced_workflows",
    )
    if referenced:
        fail(f"{label} unexpectedly invokes a reusable workflow")
    pull_requests = require_list(
        require_key(run, "pull_requests", label),
        f"{label}.pull_requests",
    )
    if pull_requests:
        fail(f"{label} unexpectedly binds a pull request")
    created = parse_api_time(
        require_key(run, "created_at", label), f"{label}.created_at"
    )
    started = parse_api_time(
        require_key(run, "run_started_at", label),
        f"{label}.run_started_at",
    )
    updated = parse_api_time(
        require_key(run, "updated_at", label), f"{label}.updated_at"
    )
    approved = assembler.parse_time(
        approval["approvedAt"], "approval approvedAt"
    )
    if not (created <= started <= approved <= updated + timedelta(minutes=5)):
        fail(f"{label} timestamps do not contain the approval receipt time")
    return {
        "id": run_id,
        "attempt": 1,
        "workflowId": workflow_id,
        "workflowPath": workflow_path,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "actor": actor,
        "createdAt": assembler.format_time(created),
        "startedAt": assembler.format_time(started),
        "updatedAt": assembler.format_time(updated),
    }


def authenticate_workflow_run(
    client: ProviderReader,
    *,
    approval: Mapping[str, Any],
    repository_id: int,
) -> dict[str, Any]:
    run_id = int(approval["authority"]["runId"])
    current_response = client.get_json(
        repository_api_path(f"/actions/runs/{run_id}")
    )
    require_unpaginated(current_response.headers, "workflow run response")
    current = validate_run_shape(
        current_response.value,
        approval=approval,
        repository_id=repository_id,
        label=f"workflow run {run_id}",
    )
    attempt_response = client.get_json(
        repository_api_path(
            f"/actions/runs/{run_id}/attempts/1?exclude_pull_requests=false"
        )
    )
    require_unpaginated(
        attempt_response.headers, "workflow run attempt response"
    )
    attempt = validate_run_shape(
        attempt_response.value,
        approval=approval,
        repository_id=repository_id,
        label=f"workflow run {run_id} attempt 1",
    )
    for key in (
        "id",
        "attempt",
        "workflowId",
        "workflowPath",
        "event",
        "status",
        "conclusion",
        "actor",
        "createdAt",
        "startedAt",
        "updatedAt",
    ):
        require_value(
            attempt[key], current[key], f"workflow run attempt {key}"
        )
    return current


def authenticate_workflow_definition(
    client: ProviderReader,
    *,
    workflow_id: int,
) -> dict[str, Any]:
    response = client.get_json(
        repository_api_path(f"/actions/workflows/{workflow_id}")
    )
    require_unpaginated(response.headers, "workflow definition response")
    workflow = require_mapping(response.value, "workflow definition")
    require_value(
        require_api_integer(
            require_key(workflow, "id", "workflow definition"),
            "workflow definition.id",
        ),
        workflow_id,
        "workflow definition.id",
    )
    require_value(
        require_key(workflow, "path", "workflow definition"),
        assembler.APPROVAL_WORKFLOW,
        "workflow definition.path",
    )
    require_value(
        require_key(workflow, "state", "workflow definition"),
        "active",
        "workflow definition.state",
    )
    return {
        "id": workflow_id,
        "path": assembler.APPROVAL_WORKFLOW,
        "state": "active",
    }


def validate_artifact_metadata(
    value: object,
    *,
    expected_id: int | None,
    expected_name: str,
    expected_run_id: int | None,
    repository_id: int,
    source_sha: str,
    now: datetime,
    maximum_bytes: int,
    expected_digest: str | None = None,
    label: str,
) -> dict[str, Any]:
    artifact = require_mapping(value, label)
    artifact_id = require_api_integer(
        require_key(artifact, "id", label), f"{label}.id"
    )
    if expected_id is not None:
        require_value(artifact_id, expected_id, f"{label}.id")
    require_value(
        require_key(artifact, "name", label), expected_name, f"{label}.name"
    )
    size_bytes = require_api_integer(
        require_key(artifact, "size_in_bytes", label),
        f"{label}.size_in_bytes",
    )
    if size_bytes > maximum_bytes:
        fail(f"{label} exceeds the {maximum_bytes}-byte boundary")
    if require_api_boolean(
        require_key(artifact, "expired", label), f"{label}.expired"
    ):
        fail(f"{label} is expired")
    expires_at = require_not_expired(
        require_key(artifact, "expires_at", label),
        now=now,
        label=f"{label}.expires_at",
    )
    created_at = assembler.format_time(
        parse_api_time(
            require_key(artifact, "created_at", label),
            f"{label}.created_at",
        )
    )
    updated_at = assembler.format_time(
        parse_api_time(
            require_key(artifact, "updated_at", label),
            f"{label}.updated_at",
        )
    )
    digest = require_api_string(
        require_key(artifact, "digest", label),
        f"{label}.digest",
        ARTIFACT_DIGEST_RE,
    )
    if expected_digest is not None:
        require_value(digest, expected_digest, f"{label}.digest")
    expected_url = repository_api_path(
        f"/actions/artifacts/{artifact_id}/zip"
    )
    require_value(
        require_key(artifact, "archive_download_url", label),
        f"{API_ROOT}{expected_url}",
        f"{label}.archive_download_url",
    )
    workflow_run = require_mapping(
        require_key(artifact, "workflow_run", label),
        f"{label}.workflow_run",
    )
    workflow_run_id = require_api_integer(
        require_key(workflow_run, "id", f"{label}.workflow_run"),
        f"{label}.workflow_run.id",
    )
    if expected_run_id is not None:
        require_value(
            workflow_run_id,
            expected_run_id,
            f"{label}.workflow_run.id",
        )
    for key in ("repository_id", "head_repository_id"):
        require_value(
            require_api_integer(
                require_key(workflow_run, key, f"{label}.workflow_run"),
                f"{label}.workflow_run.{key}",
            ),
            repository_id,
            f"{label}.workflow_run.{key}",
        )
    require_value(
        require_key(workflow_run, "head_branch", f"{label}.workflow_run"),
        SOURCE_BRANCH,
        f"{label}.workflow_run.head_branch",
    )
    require_value(
        require_key(workflow_run, "head_sha", f"{label}.workflow_run"),
        source_sha,
        f"{label}.workflow_run.head_sha",
    )
    return {
        "id": artifact_id,
        "name": expected_name,
        "digest": digest,
        "sizeBytes": size_bytes,
        "createdAt": created_at,
        "updatedAt": updated_at,
        "expiresAt": expires_at,
        "workflowRunId": workflow_run_id,
    }


def download_authenticated_artifact(
    client: ProviderReader,
    *,
    metadata: Mapping[str, Any],
    maximum_bytes: int,
    label: str,
) -> bytes:
    archive = client.get_artifact_archive(
        int(metadata["id"]), maximum_bytes
    )
    if len(archive) != int(metadata["sizeBytes"]):
        fail(f"{label} archive size does not match provider metadata")
    digest_match = ARTIFACT_DIGEST_RE.fullmatch(str(metadata["digest"]))
    if digest_match is None:
        fail(f"{label} provider digest is malformed")
    if sha256_bytes(archive) != digest_match.group(1):
        fail(f"{label} archive bytes do not match the provider digest")
    return archive


def authenticate_input_artifact(
    client: ProviderReader,
    *,
    artifact_id: int,
    expected_digest: str,
    repository_id: int,
    now: datetime,
) -> tuple[dict[str, Any], bytes]:
    response = client.get_json(
        repository_api_path(f"/actions/artifacts/{artifact_id}")
    )
    require_unpaginated(response.headers, "input artifact response")
    artifact = require_mapping(response.value, "input artifact")
    workflow_run = require_mapping(
        require_key(artifact, "workflow_run", "input artifact"),
        "input artifact.workflow_run",
    )
    source_sha = require_api_string(
        require_key(workflow_run, "head_sha", "input artifact.workflow_run"),
        "input artifact.workflow_run.head_sha",
        assembler.COMMIT_RE,
    )
    metadata = validate_artifact_metadata(
        artifact,
        expected_id=artifact_id,
        expected_name=INPUT_ARTIFACT_NAME,
        expected_run_id=None,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_INPUT_ARTIFACT_BYTES,
        expected_digest=expected_digest,
        label="input artifact",
    )
    archive = download_authenticated_artifact(
        client,
        metadata=metadata,
        maximum_bytes=MAX_INPUT_ARTIFACT_BYTES,
        label="input artifact",
    )
    second = client.get_json(
        repository_api_path(f"/actions/artifacts/{artifact_id}")
    )
    require_unpaginated(second.headers, "input artifact recheck")
    rechecked = validate_artifact_metadata(
        second.value,
        expected_id=artifact_id,
        expected_name=INPUT_ARTIFACT_NAME,
        expected_run_id=int(metadata["workflowRunId"]),
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_INPUT_ARTIFACT_BYTES,
        expected_digest=expected_digest,
        label="input artifact recheck",
    )
    require_value(rechecked, metadata, "input artifact recheck")
    entries = read_exact_zip(
        archive,
        expected_names={INPUT_BUNDLE_FILE_NAME},
        maximum_entries=1,
        maximum_total_bytes=MAX_INPUT_ARTIFACT_BYTES,
        label="input artifact archive",
    )
    return metadata, entries[INPUT_BUNDLE_FILE_NAME]


def authenticate_approval_artifact(
    client: ProviderReader,
    *,
    approval: Mapping[str, Any],
    local_snapshot: assembler.Snapshot,
    repository_id: int,
    source_sha: str,
    now: datetime,
) -> tuple[dict[str, Any], assembler.Snapshot]:
    role = str(approval["role"])
    run_id = int(approval["authority"]["runId"])
    expected_name = (
        f"global-flagship-release-approval-{role}-{run_id}-1"
    )
    if APPROVAL_ARTIFACT_RE.fullmatch(expected_name) is None:
        fail(f"{role} approval artifact name is malformed")
    list_response = client.get_json(
        repository_api_path(
            f"/actions/runs/{run_id}/artifacts?per_page=100&page=1"
        )
    )
    require_unpaginated(
        list_response.headers, f"{role} approval artifact list"
    )
    listing = require_mapping(
        list_response.value, f"{role} approval artifact list"
    )
    total_count = require_api_integer(
        require_key(listing, "total_count", f"{role} artifact list"),
        f"{role} artifact list.total_count",
    )
    artifacts = require_list(
        require_key(listing, "artifacts", f"{role} artifact list"),
        f"{role} artifact list.artifacts",
    )
    if total_count != 1 or len(artifacts) != 1:
        fail(f"{role} approval run must contain exactly one artifact")
    metadata = validate_artifact_metadata(
        artifacts[0],
        expected_id=None,
        expected_name=expected_name,
        expected_run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_APPROVAL_ARTIFACT_BYTES,
        label=f"{role} approval artifact",
    )
    detail_response = client.get_json(
        repository_api_path(
            f"/actions/artifacts/{int(metadata['id'])}"
        )
    )
    require_unpaginated(
        detail_response.headers, f"{role} approval artifact detail"
    )
    detail = validate_artifact_metadata(
        detail_response.value,
        expected_id=int(metadata["id"]),
        expected_name=expected_name,
        expected_run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_APPROVAL_ARTIFACT_BYTES,
        expected_digest=str(metadata["digest"]),
        label=f"{role} approval artifact detail",
    )
    require_value(detail, metadata, f"{role} approval artifact detail")
    archive = download_authenticated_artifact(
        client,
        metadata=metadata,
        maximum_bytes=MAX_APPROVAL_ARTIFACT_BYTES,
        label=f"{role} approval artifact",
    )
    recheck_response = client.get_json(
        repository_api_path(
            f"/actions/artifacts/{int(metadata['id'])}"
        )
    )
    require_unpaginated(
        recheck_response.headers, f"{role} approval artifact recheck"
    )
    rechecked = validate_artifact_metadata(
        recheck_response.value,
        expected_id=int(metadata["id"]),
        expected_name=expected_name,
        expected_run_id=run_id,
        repository_id=repository_id,
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_APPROVAL_ARTIFACT_BYTES,
        expected_digest=str(metadata["digest"]),
        label=f"{role} approval artifact recheck",
    )
    require_value(rechecked, metadata, f"{role} approval artifact recheck")
    entries = read_exact_zip(
        archive,
        expected_names={"approval.json"},
        maximum_entries=1,
        maximum_total_bytes=MAX_APPROVAL_ARTIFACT_BYTES,
        label=f"{role} approval artifact archive",
    )
    provider_bytes = entries["approval.json"]
    if not hmac.compare_digest(
        provider_bytes, local_snapshot.data or b""
    ):
        fail(
            f"{role} provider approval bytes differ from the final receipt "
            "input"
        )
    return metadata, snapshot_bytes(
        provider_bytes, local_snapshot.relative_path
    )


def authenticate_source_file(
    client: ProviderReader,
    *,
    path: str,
    source_sha: str,
    maximum_bytes: int,
    label: str,
) -> tuple[dict[str, Any], bytes]:
    encoded_path = quote(path, safe="/")
    response = client.get_json(
        repository_api_path(
            f"/contents/{encoded_path}?ref={quote(source_sha, safe='')}"
        )
    )
    require_unpaginated(response.headers, f"{label} content response")
    content = require_mapping(response.value, f"{label} content")
    require_value(
        require_key(content, "type", f"{label} content"),
        "file",
        f"{label} content.type",
    )
    require_value(
        require_key(content, "path", f"{label} content"),
        path,
        f"{label} content.path",
    )
    require_value(
        require_key(content, "encoding", f"{label} content"),
        "base64",
        f"{label} content.encoding",
    )
    size_bytes = require_api_integer(
        require_key(content, "size", f"{label} content"),
        f"{label} content.size",
    )
    if size_bytes > maximum_bytes:
        fail(f"{label} exceeds the source byte boundary")
    encoded = require_api_string(
        require_key(content, "content", f"{label} content"),
        f"{label} content.content",
    )
    if "\r" in encoded:
        if "\r\n" not in encoded or encoded.replace("\r\n", "").find("\r") >= 0:
            fail(f"{label} content contains malformed base64 line endings")
        normalized_encoded = encoded.replace("\r\n", "")
    else:
        normalized_encoded = encoded.replace("\n", "")
    try:
        data = base64.b64decode(normalized_encoded, validate=True)
    except (ValueError, TypeError):
        fail(f"{label} content is not strict base64")
    if not data or len(data) != size_bytes:
        fail(f"{label} decoded size does not match source metadata")
    blob_sha = require_api_string(
        require_key(content, "sha", f"{label} content"),
        f"{label} content.sha",
        GIT_BLOB_SHA_RE,
    )
    if git_blob_sha1(data) != blob_sha:
        fail(f"{label} bytes do not match the Git blob identity")
    return (
        {
            "path": path,
            "gitBlobSha": blob_sha,
            "sha256": sha256_bytes(data),
            "sizeBytes": size_bytes,
        },
        data,
    )


def validate_policy_roles(
    policy_snapshot: assembler.Snapshot,
    approvals: Sequence[Mapping[str, Any]],
) -> dict[str, list[str]]:
    roles: dict[str, list[str]] = {}
    for approval in approvals:
        projection = assembler.validate_reviewer_policy(
            policy_snapshot,
            role=str(approval["role"]),
            actor=str(approval["actor"]),
            environment_approver=str(
                approval["environmentApproval"]["reviewer"]
            ),
        )
        require_value(
            projection,
            approval["reviewerPolicy"],
            f"{approval['role']} source reviewer policy projection",
        )
    policy = assembler.exact_dict(
        assembler.load_json_bytes(
            policy_snapshot.data, "source reviewer policy"
        ),
        {"contractName", "contractVersion", "roles"},
        "source reviewer policy",
    )
    policy_roles = assembler.exact_dict(
        policy["roles"],
        set(assembler.REQUIRED_APPROVAL_ROLES),
        "source reviewer policy roles",
    )
    for role in assembler.REQUIRED_APPROVAL_ROLES:
        members = require_list(
            policy_roles[role], f"source reviewer policy roles.{role}"
        )
        roles[role] = [str(member) for member in members]
    return roles


def policy_reviewer_union(roles: Mapping[str, Sequence[str]]) -> list[str]:
    reviewers = [
        reviewer
        for role in assembler.REQUIRED_APPROVAL_ROLES
        for reviewer in roles[role]
    ]
    if len(reviewers) < 3 or len(reviewers) > 6:
        fail("source reviewer policy union must contain three to six users")
    if len({reviewer.casefold() for reviewer in reviewers}) != len(reviewers):
        fail("source reviewer policy roles are not disjoint")
    return sorted(reviewers, key=str.casefold)


def authenticate_environment(
    client: ProviderReader,
    *,
    policy_reviewers: Sequence[str],
) -> dict[str, Any]:
    environment_path = repository_api_path(
        f"/environments/{quote(assembler.APPROVAL_ENVIRONMENT, safe='')}"
    )
    response = client.get_json(environment_path)
    require_unpaginated(response.headers, "approval environment response")
    environment = require_mapping(
        response.value, "approval environment"
    )
    environment_id = require_api_integer(
        require_key(environment, "id", "approval environment"),
        "approval environment.id",
    )
    require_value(
        require_key(environment, "name", "approval environment"),
        assembler.APPROVAL_ENVIRONMENT,
        "approval environment.name",
    )
    administrators_may_bypass = require_api_boolean(
        require_key(
            environment,
            "can_admins_bypass",
            "approval environment",
        ),
        "approval environment.can_admins_bypass",
    )
    if administrators_may_bypass:
        fail("approval environment permits administrator bypass")
    deployment_policy = require_mapping(
        require_key(
            environment, "deployment_branch_policy", "approval environment"
        ),
        "approval environment.deployment_branch_policy",
    )
    require_value(
        require_key(
            deployment_policy,
            "protected_branches",
            "approval environment deployment branch policy",
        ),
        False,
        "approval environment protected_branches",
    )
    require_value(
        require_key(
            deployment_policy,
            "custom_branch_policies",
            "approval environment deployment branch policy",
        ),
        True,
        "approval environment custom_branch_policies",
    )
    rules = require_list(
        require_key(environment, "protection_rules", "approval environment"),
        "approval environment.protection_rules",
    )
    reviewer_rules = [
        require_mapping(rule, "approval environment protection rule")
        for rule in rules
        if isinstance(rule, dict) and rule.get("type") == "required_reviewers"
    ]
    if len(reviewer_rules) != 1:
        fail("approval environment must have one required-reviewers rule")
    unexpected_types = {
        str(rule.get("type"))
        for rule in rules
        if not isinstance(rule, dict)
        or rule.get("type") not in {"required_reviewers", "branch_policy"}
    }
    if unexpected_types:
        fail("approval environment contains an unexpected protection rule")
    reviewer_rule = reviewer_rules[0]
    require_value(
        require_key(
            reviewer_rule,
            "prevent_self_review",
            "approval environment reviewer rule",
        ),
        True,
        "approval environment prevent_self_review",
    )
    reviewers = require_list(
        require_key(
            reviewer_rule,
            "reviewers",
            "approval environment reviewer rule",
        ),
        "approval environment reviewers",
    )
    live_logins: list[str] = []
    live_identities: list[dict[str, Any]] = []
    for index, entry_value in enumerate(reviewers):
        entry = require_mapping(
            entry_value, f"approval environment reviewer {index}"
        )
        require_value(
            require_key(
                entry,
                "type",
                f"approval environment reviewer {index}",
            ),
            "User",
            f"approval environment reviewer {index}.type",
        )
        reviewer = require_mapping(
            require_key(
                entry,
                "reviewer",
                f"approval environment reviewer {index}",
            ),
            f"approval environment reviewer {index}.reviewer",
        )
        login = require_api_string(
            require_key(
                reviewer,
                "login",
                f"approval environment reviewer {index}",
            ),
            f"approval environment reviewer {index}.login",
            assembler.GITHUB_LOGIN_RE,
        )
        require_value(
            require_key(
                reviewer,
                "type",
                f"approval environment reviewer {index}",
            ),
            "User",
            f"approval environment reviewer {index}.reviewer.type",
        )
        reviewer_id = require_api_integer(
            require_key(
                reviewer,
                "id",
                f"approval environment reviewer {index}",
            ),
            f"approval environment reviewer {index}.reviewer.id",
        )
        live_logins.append(login)
        live_identities.append(
            {"id": reviewer_id, "login": login, "type": "User"}
        )
    if len({identity["id"] for identity in live_identities}) != len(
        live_identities
    ):
        fail("approval environment reviewer identities are duplicated")
    if sorted(login.casefold() for login in live_logins) != sorted(
        login.casefold() for login in policy_reviewers
    ):
        fail(
            "approval environment reviewers do not equal the exact source "
            "reviewer-policy union"
        )
    policy_response = client.get_json(
        f"{environment_path}/deployment-branch-policies?per_page=100&page=1"
    )
    require_unpaginated(
        policy_response.headers,
        "approval environment branch policy response",
    )
    policies_value = require_mapping(
        policy_response.value, "approval environment branch policies"
    )
    total = require_api_integer(
        require_key(
            policies_value,
            "total_count",
            "approval environment branch policies",
        ),
        "approval environment branch policies.total_count",
    )
    policies = require_list(
        require_key(
            policies_value,
            "branch_policies",
            "approval environment branch policies",
        ),
        "approval environment branch policies.branch_policies",
    )
    if total != 1 or len(policies) != 1:
        fail("approval environment must have exactly one branch policy")
    policy = require_mapping(
        policies[0], "approval environment branch policy"
    )
    require_value(
        require_key(policy, "name", "approval environment branch policy"),
        SOURCE_BRANCH,
        "approval environment branch policy.name",
    )
    projection = {
        "id": environment_id,
        "name": assembler.APPROVAL_ENVIRONMENT,
        "administratorsMayBypass": False,
        "preventSelfReview": True,
        "reviewers": sorted(live_logins, key=str.casefold),
        "reviewerIdentities": sorted(
            live_identities, key=lambda item: str(item["login"]).casefold()
        ),
        "deploymentBranches": [SOURCE_BRANCH],
    }
    return {
        **projection,
        "configurationSha256": sha256_bytes(
            canonical_json_bytes(projection)
        ),
    }


def authenticate_approval_history(
    client: ProviderReader,
    *,
    approval: Mapping[str, Any],
    environment_id: int,
    expected_reviewer_id: int,
) -> dict[str, Any]:
    role = str(approval["role"])
    run_id = int(approval["authority"]["runId"])
    expected_reviewer = str(
        approval["environmentApproval"]["reviewer"]
    )
    response = client.get_json(
        repository_api_path(f"/actions/runs/{run_id}/approvals")
    )
    require_unpaginated(response.headers, f"{role} approval history")
    history = require_list(response.value, f"{role} approval history")
    if len(history) != 1:
        fail(f"{role} run must have exactly one environment review record")
    review = require_mapping(history[0], f"{role} approval history record")
    require_value(
        require_key(review, "state", f"{role} approval history record"),
        "approved",
        f"{role} approval history state",
    )
    reviewer = validate_user(
        require_key(review, "user", f"{role} approval history record"),
        expected_login=expected_reviewer,
        expected_id=expected_reviewer_id,
        label=f"{role} approval history reviewer",
    )
    environments = require_list(
        require_key(
            review, "environments", f"{role} approval history record"
        ),
        f"{role} approval history environments",
    )
    if len(environments) != 1:
        fail(f"{role} approval history must bind exactly one environment")
    environment = require_mapping(
        environments[0], f"{role} approval history environment"
    )
    require_value(
        require_api_integer(
            require_key(
                environment, "id", f"{role} approval history environment"
            ),
            f"{role} approval history environment.id",
        ),
        environment_id,
        f"{role} approval history environment.id",
    )
    require_value(
        require_key(
            environment, "name", f"{role} approval history environment"
        ),
        assembler.APPROVAL_ENVIRONMENT,
        f"{role} approval history environment.name",
    )
    return {
        "state": "approved",
        "reviewer": reviewer,
        "environmentId": environment_id,
        "environmentName": assembler.APPROVAL_ENVIRONMENT,
    }


def enabled_value(value: object, label: str) -> bool:
    mapping = require_mapping(value, label)
    enabled = require_api_boolean(
        require_key(mapping, "enabled", label), f"{label}.enabled"
    )
    return enabled


def bypass_entries_empty(value: object, label: str) -> None:
    if value is None:
        return
    mapping = require_mapping(value, label)
    for kind in ("users", "teams", "apps"):
        entries = mapping.get(kind, [])
        if not isinstance(entries, list):
            fail(f"{label}.{kind} must be an array")
        if entries:
            fail(f"{label}.{kind} permits a branch-protection bypass")


def authenticate_branch_governance(
    client: ProviderReader,
    admin_client: ProviderReader,
    *,
    source_sha: str,
) -> dict[str, Any]:
    branch_response = client.get_json(
        repository_api_path(f"/branches/{SOURCE_BRANCH}")
    )
    require_unpaginated(branch_response.headers, "main branch response")
    branch = require_mapping(branch_response.value, "main branch")
    require_value(
        require_key(branch, "name", "main branch"),
        SOURCE_BRANCH,
        "main branch.name",
    )
    require_value(
        require_key(branch, "protected", "main branch"),
        True,
        "main branch.protected",
    )
    commit = require_mapping(
        require_key(branch, "commit", "main branch"), "main branch.commit"
    )
    require_value(
        require_key(commit, "sha", "main branch.commit"),
        source_sha,
        "main branch commit SHA",
    )

    protection_response = admin_client.get_json(
        repository_api_path(
            f"/branches/{SOURCE_BRANCH}/protection"
        )
    )
    require_unpaginated(
        protection_response.headers, "main branch protection response"
    )
    protection = require_mapping(
        protection_response.value, "main branch protection"
    )
    status_checks = require_mapping(
        require_key(
            protection,
            "required_status_checks",
            "main branch protection",
        ),
        "main branch required status checks",
    )
    require_value(
        require_key(
            status_checks, "strict", "main branch required status checks"
        ),
        True,
        "main branch required status checks.strict",
    )
    contexts_value = require_list(
        require_key(
            status_checks,
            "contexts",
            "main branch required status checks",
        ),
        "main branch required status checks.contexts",
    )
    contexts = [
        require_api_string(
            value, f"main branch status context {index}"
        )
        for index, value in enumerate(contexts_value)
    ]
    checks_value = require_key(
        status_checks, "checks", "main branch required status checks"
    )
    checks = require_list(
        checks_value, "main branch required status checks.checks"
    )
    if not checks:
        fail(
            "main branch required status checks must contain a nonempty "
            "GitHub-App-bound checks representation"
        )
    check_projection: list[dict[str, Any]] = []
    for index, check_value in enumerate(checks):
        check = require_mapping(
            check_value, f"main branch required check {index}"
        )
        context = require_api_string(
            require_key(
                check, "context", f"main branch required check {index}"
            ),
            f"main branch required check {index}.context",
        )
        app_id = require_api_integer(
            require_key(
                check, "app_id", f"main branch required check {index}"
            ),
            f"main branch required check {index}.app_id",
        )
        check_projection.append({"context": context, "appId": app_id})
    check_contexts = [
        str(check["context"]) for check in check_projection
    ]
    if (
        not contexts
        or len(set(contexts)) != len(contexts)
        or len(set(check_contexts)) != len(check_contexts)
    ):
        fail(
            "main branch required status checks are empty or duplicated "
            "within a provider representation"
        )
    if sorted(contexts) != sorted(check_contexts):
        fail(
            "main branch legacy status contexts do not exactly correspond "
            "to the GitHub-App-bound required checks"
        )

    if not enabled_value(
        require_key(protection, "enforce_admins", "main branch protection"),
        "main branch enforce_admins",
    ):
        fail("main branch protection does not enforce administrators")
    reviews = require_mapping(
        require_key(
            protection,
            "required_pull_request_reviews",
            "main branch protection",
        ),
        "main branch pull-request reviews",
    )
    require_value(
        require_key(
            reviews,
            "dismiss_stale_reviews",
            "main branch pull-request reviews",
        ),
        True,
        "main branch dismiss_stale_reviews",
    )
    require_value(
        require_key(
            reviews,
            "require_last_push_approval",
            "main branch pull-request reviews",
        ),
        True,
        "main branch require_last_push_approval",
    )
    review_count = require_api_integer(
        require_key(
            reviews,
            "required_approving_review_count",
            "main branch pull-request reviews",
        ),
        "main branch required_approving_review_count",
    )
    bypass_entries_empty(
        reviews.get("bypass_pull_request_allowances"),
        "main branch pull-request bypass allowances",
    )
    if not enabled_value(
        require_key(
            protection,
            "required_conversation_resolution",
            "main branch protection",
        ),
        "main branch required conversation resolution",
    ):
        fail("main branch does not require conversation resolution")
    if not enabled_value(
        require_key(
            protection,
            "required_linear_history",
            "main branch protection",
        ),
        "main branch required linear history",
    ):
        fail("main branch does not require linear history")
    if enabled_value(
        require_key(
            protection, "allow_force_pushes", "main branch protection"
        ),
        "main branch allow_force_pushes",
    ):
        fail("main branch permits force pushes")
    if enabled_value(
        require_key(
            protection, "allow_deletions", "main branch protection"
        ),
        "main branch allow_deletions",
    ):
        fail("main branch permits deletion")
    projection = {
        "branch": SOURCE_BRANCH,
        "headSha": source_sha,
        "protected": True,
        "strictStatusChecks": True,
        "statusContexts": sorted(contexts),
        "statusChecks": sorted(
            check_projection,
            key=lambda item: (
                str(item["context"]),
                -1 if item["appId"] is None else int(item["appId"]),
            ),
        ),
        "enforceAdmins": True,
        "dismissStaleReviews": True,
        "requireLastPushApproval": True,
        "requiredApprovingReviewCount": review_count,
        "pullRequestBypassAllowances": {
            "users": [],
            "teams": [],
            "apps": [],
        },
        "requiredConversationResolution": True,
        "requiredLinearHistory": True,
        "allowForcePushes": False,
        "allowDeletions": False,
    }
    return {
        **projection,
        "configurationSha256": sha256_bytes(
            canonical_json_bytes(projection)
        ),
    }


def authenticate_provider_handoff(
    client: ProviderReader,
    admin_client: ProviderReader,
    *,
    input_artifact_id: int,
    expected_input_artifact_digest: str,
    expected_verifier_source_sha: str,
    now: datetime,
) -> dict[str, Any]:
    restricted_admin = RestrictedAdministrationReader(admin_client)
    digest = require_api_string(
        expected_input_artifact_digest,
        "expected input artifact digest",
        ARTIFACT_DIGEST_RE,
    )
    repository = validate_repository(
        client.get_json(repository_api_path(""))
    )
    transport, bundle_zip = authenticate_input_artifact(
        client,
        artifact_id=input_artifact_id,
        expected_digest=digest,
        repository_id=int(repository["id"]),
        now=now,
    )
    bundle = read_local_bundle(bundle_zip)
    local = validate_local_bundle(bundle, now=now)
    candidate = require_mapping(
        local.proposal["candidate"], "proposal candidate"
    )
    source = require_mapping(candidate["source"], "proposal candidate source")
    source_sha = require_api_string(
        source["commit"], "proposal candidate source commit", assembler.COMMIT_RE
    )
    verifier_source_sha = require_api_string(
        expected_verifier_source_sha,
        "expected verifier source SHA",
        assembler.COMMIT_RE,
    )
    require_value(
        source_sha,
        verifier_source_sha,
        "candidate and executing verifier source SHA",
    )
    require_value(
        source["repository"],
        assembler.SOURCE_REPOSITORY,
        "proposal candidate source repository",
    )
    require_value(
        source["ref"], SOURCE_REF, "proposal candidate source ref"
    )
    # The input artifact is transport, not authority, but binding its provider
    # run to the same source SHA catches stale or cross-source staging.
    input_detail = client.get_json(
        repository_api_path(
            f"/actions/artifacts/{int(transport['id'])}"
        )
    )
    input_value = require_mapping(
        input_detail.value, "input artifact source recheck"
    )
    require_unpaginated(
        input_detail.headers, "input artifact source recheck"
    )
    input_rechecked = validate_artifact_metadata(
        input_value,
        expected_id=int(transport["id"]),
        expected_name=INPUT_ARTIFACT_NAME,
        expected_run_id=int(transport["workflowRunId"]),
        repository_id=int(repository["id"]),
        source_sha=source_sha,
        now=now,
        maximum_bytes=MAX_INPUT_ARTIFACT_BYTES,
        expected_digest=str(transport["digest"]),
        label="input artifact source recheck",
    )
    require_value(
        input_rechecked, transport, "input artifact source recheck"
    )

    workflow_source, _workflow_bytes = authenticate_source_file(
        client,
        path=assembler.APPROVAL_WORKFLOW,
        source_sha=source_sha,
        maximum_bytes=MAX_WORKFLOW_BYTES,
        label="approval workflow",
    )
    policy_source, policy_bytes = authenticate_source_file(
        client,
        path=REVIEWER_POLICY_PATH,
        source_sha=source_sha,
        maximum_bytes=assembler.MAX_REVIEWER_POLICY_BYTES,
        label="reviewer policy",
    )
    policy_snapshot = snapshot_bytes(
        policy_bytes, PurePosixPath(REVIEWER_POLICY_PATH).name
    )
    roles = validate_policy_roles(policy_snapshot, local.approvals)
    policy_reviewers = policy_reviewer_union(roles)
    environment = authenticate_environment(
        client, policy_reviewers=policy_reviewers
    )
    branch_governance = authenticate_branch_governance(
        client, restricted_admin, source_sha=source_sha
    )

    workflow_definitions: dict[int, dict[str, Any]] = {}
    authenticated_approvals: list[dict[str, Any]] = []
    fetched_projections: list[dict[str, Any]] = []
    environment_reviewer_ids = {
        str(identity["login"]).casefold(): int(identity["id"])
        for identity in environment["reviewerIdentities"]
    }
    for approval in local.approvals:
        role = str(approval["role"])
        run = authenticate_workflow_run(
            client,
            approval=approval,
            repository_id=int(repository["id"]),
        )
        require_value(
            int(run["actor"]["id"]),
            environment_reviewer_ids[str(approval["actor"]).casefold()],
            f"{role} workflow actor provider identity",
        )
        workflow_id = int(run["workflowId"])
        if workflow_id not in workflow_definitions:
            workflow_definitions[workflow_id] = (
                authenticate_workflow_definition(
                    client, workflow_id=workflow_id
                )
            )
        history = authenticate_approval_history(
            client,
            approval=approval,
            environment_id=int(environment["id"]),
            expected_reviewer_id=environment_reviewer_ids[
                str(
                    approval["environmentApproval"]["reviewer"]
                ).casefold()
            ],
        )
        metadata, provider_snapshot = authenticate_approval_artifact(
            client,
            approval=approval,
            local_snapshot=bundle.approvals[role],
            repository_id=int(repository["id"]),
            source_sha=source_sha,
            now=now,
        )
        artifact_created = assembler.parse_time(
            metadata["createdAt"], f"{role} artifact createdAt"
        )
        approved_at = assembler.parse_time(
            approval["approvedAt"], f"{role} approval approvedAt"
        )
        run_updated = assembler.parse_time(
            run["updatedAt"], f"{role} run updatedAt"
        )
        if not (
            approved_at
            <= artifact_created
            <= run_updated + timedelta(seconds=assembler.MAX_CLOCK_SKEW_SECONDS)
        ):
            fail(
                f"{role} artifact creation time is outside the authenticated "
                "run"
            )
        provider_projection = assembler.validate_approval(
            provider_snapshot,
            proposal_snapshot=bundle.proposal,
            proposal=local.proposal,
            now=now,
        )
        require_value(
            provider_projection,
            approval,
            f"{role} provider approval projection",
        )
        fetched_projections.append(provider_projection)
        authenticated_approvals.append(
            {
                "role": role,
                "actor": str(approval["actor"]),
                "environmentApprover": str(
                    approval["environmentApproval"]["reviewer"]
                ),
                "run": run,
                "workflow": workflow_definitions[workflow_id],
                "reviewHistory": history,
                "artifact": {
                    **metadata,
                    "archiveEntry": "approval.json",
                    "receiptSha256": provider_snapshot.sha256,
                    "receiptSizeBytes": provider_snapshot.size_bytes,
                },
            }
        )
    fetched_projections.sort(key=lambda item: str(item["role"]))
    require_value(
        fetched_projections,
        list(local.approvals),
        "provider approval set",
    )
    authenticated_approvals.sort(key=lambda item: str(item["role"]))
    actors = [
        str(item["actor"]).casefold() for item in authenticated_approvals
    ]
    actor_ids = [
        int(item["run"]["actor"]["id"]) for item in authenticated_approvals
    ]
    run_ids = [int(item["run"]["id"]) for item in authenticated_approvals]
    if (
        len(set(actors)) != 3
        or len(set(actor_ids)) != 3
        or len(set(run_ids)) != 3
    ):
        fail(
            "provider approvals are not three distinct actor identities and "
            "run IDs"
        )

    # Recheck every authority that contributed to the handoff projection.
    # Approval runs, workflow definitions, review history, artifact listings,
    # metadata, and archive bytes are mutable provider views, so authenticate
    # the complete approval set again before the shared environment and source
    # branch checks. Keep branch governance last.
    final_policy, final_policy_bytes = authenticate_source_file(
        client,
        path=REVIEWER_POLICY_PATH,
        source_sha=source_sha,
        maximum_bytes=assembler.MAX_REVIEWER_POLICY_BYTES,
        label="reviewer policy final recheck",
    )
    require_value(
        final_policy, policy_source, "reviewer policy final recheck"
    )
    if not hmac.compare_digest(final_policy_bytes, policy_bytes):
        fail("reviewer policy bytes changed during authentication")

    final_authenticated_approvals: list[dict[str, Any]] = []
    for approval in local.approvals:
        role = str(approval["role"])
        final_run = authenticate_workflow_run(
            client,
            approval=approval,
            repository_id=int(repository["id"]),
        )
        require_value(
            int(final_run["actor"]["id"]),
            environment_reviewer_ids[str(approval["actor"]).casefold()],
            f"{role} final workflow actor provider identity",
        )
        final_workflow = authenticate_workflow_definition(
            client, workflow_id=int(final_run["workflowId"])
        )
        final_history = authenticate_approval_history(
            client,
            approval=approval,
            environment_id=int(environment["id"]),
            expected_reviewer_id=environment_reviewer_ids[
                str(
                    approval["environmentApproval"]["reviewer"]
                ).casefold()
            ],
        )
        final_metadata, final_provider_snapshot = (
            authenticate_approval_artifact(
                client,
                approval=approval,
                local_snapshot=bundle.approvals[role],
                repository_id=int(repository["id"]),
                source_sha=source_sha,
                now=now,
            )
        )
        final_artifact_created = assembler.parse_time(
            final_metadata["createdAt"], f"{role} final artifact createdAt"
        )
        final_approved_at = assembler.parse_time(
            approval["approvedAt"], f"{role} final approval approvedAt"
        )
        final_run_updated = assembler.parse_time(
            final_run["updatedAt"], f"{role} final run updatedAt"
        )
        if not (
            final_approved_at
            <= final_artifact_created
            <= final_run_updated
            + timedelta(seconds=assembler.MAX_CLOCK_SKEW_SECONDS)
        ):
            fail(
                f"{role} final artifact creation time is outside the "
                "authenticated run"
            )
        final_provider_projection = assembler.validate_approval(
            final_provider_snapshot,
            proposal_snapshot=bundle.proposal,
            proposal=local.proposal,
            now=now,
        )
        require_value(
            final_provider_projection,
            approval,
            f"{role} final provider approval projection",
        )
        final_authenticated_approvals.append(
            {
                "role": role,
                "actor": str(approval["actor"]),
                "environmentApprover": str(
                    approval["environmentApproval"]["reviewer"]
                ),
                "run": final_run,
                "workflow": final_workflow,
                "reviewHistory": final_history,
                "artifact": {
                    **final_metadata,
                    "archiveEntry": "approval.json",
                    "receiptSha256": final_provider_snapshot.sha256,
                    "receiptSizeBytes": final_provider_snapshot.size_bytes,
                },
            }
        )
    final_authenticated_approvals.sort(
        key=lambda item: str(item["role"])
    )
    require_value(
        final_authenticated_approvals,
        authenticated_approvals,
        "final authenticated approval set recheck",
    )

    final_environment = authenticate_environment(
        client, policy_reviewers=policy_reviewers
    )
    require_value(
        final_environment,
        environment,
        "final approval environment recheck",
    )
    final_branch = authenticate_branch_governance(
        client, restricted_admin, source_sha=source_sha
    )
    require_value(
        final_branch,
        branch_governance,
        "final main branch governance recheck",
    )

    return {
        "contractName": HANDOFF_CONTRACT,
        "contractVersion": HANDOFF_CONTRACT_VERSION,
        "generatedAt": assembler.format_time(now),
        "status": "passed",
        "repository": repository,
        "source": {
            "repository": assembler.SOURCE_REPOSITORY,
            "ref": SOURCE_REF,
            "sha": source_sha,
            "approvalWorkflow": workflow_source,
        },
        "transportArtifact": {
            **transport,
            "trustedAsAuthority": False,
            "purpose": "bounded-metadata-transport-only",
        },
        "proposal": assembler.binding(
            bundle.proposal, contractName=assembler.PROPOSAL_CONTRACT
        ),
        "candidateManifest": assembler.binding(bundle.candidate),
        "finalReceipt": assembler.binding(
            bundle.final_receipt,
            contractName=assembler.FINAL_RECEIPT_CONTRACT,
        ),
        "reviewerPolicy": {
            **policy_source,
            "contractName": assembler.REVIEWER_POLICY_CONTRACT,
            "roles": roles,
        },
        "approvalEnvironment": environment,
        "approvals": authenticated_approvals,
        "mainBranchGovernance": branch_governance,
        "authorityLevel": HANDOFF_AUTHORITY_LEVEL,
        "provenanceScope": HANDOFF_PROVENANCE_SCOPE,
        "provenanceAuthenticated": True,
        "releaseArtifactBytesAuthenticated": False,
        "nonPublishing": True,
        "publicationAuthorized": False,
        "allowedSideEffects": list(HANDOFF_SIDE_EFFECTS),
        "handoff": {
            "eligibleForSeparatePublicationTransaction": True,
            "requiredNextAuthority": FINAL_REQUIRED_NEXT_AUTHORITY,
        },
    }


def current_time() -> datetime:
    return datetime.now(UTC).replace(microsecond=0)


def command_pack(args: argparse.Namespace) -> int:
    now = current_time()
    try:
        proposal = assembler.snapshot_absolute(
            Path(args.proposal), "proposal", assembler.MAX_JSON_BYTES
        )
        candidate = assembler.snapshot_absolute(
            Path(args.candidate),
            "candidate manifest",
            assembler.MAX_JSON_BYTES,
        )
        final_receipt = assembler.snapshot_absolute(
            Path(args.final_receipt),
            "global flagship final receipt",
            assembler.MAX_JSON_BYTES,
        )
        approval_snapshots: dict[str, assembler.Snapshot] = {}
        for raw_path in args.approval:
            snapshot = assembler.snapshot_absolute(
                Path(raw_path),
                f"approval {raw_path}",
                assembler.MAX_JSON_BYTES,
            )
            payload = assembler.load_json_bytes(
                snapshot.data, f"approval {raw_path}"
            )
            role = assembler.require_string(
                payload.get("role"), f"approval {raw_path} role"
            )
            if role not in assembler.REQUIRED_APPROVAL_ROLES:
                fail(f"approval {raw_path} has an unexpected role")
            if role in approval_snapshots:
                fail(f"approval role {role} was supplied more than once")
            approval_snapshots[role] = snapshot
        if set(approval_snapshots) != set(assembler.REQUIRED_APPROVAL_ROLES):
            fail("pack requires exactly one approval for every role")
        bundle = LocalBundle(
            proposal=proposal,
            candidate=candidate,
            final_receipt=final_receipt,
            approvals=approval_snapshots,
        )
        bundle_bytes = build_input_bundle(bundle, now=now)
        write_once(Path(args.output), bundle_bytes)
    except (ContractError, assembler.ContractError) as exc:
        print(f"provider input bundle blocked: {exc}", file=sys.stderr)
        return 1
    print(
        f"{Path(args.output)} bundle-sha256:{sha256_bytes(bundle_bytes)} "
        "(not the Actions artifact-digest)",
        flush=True,
    )
    return 0


def command_verify(args: argparse.Namespace) -> int:
    now = current_time()
    output = Path(args.output)
    try:
        main_token = os.environ.get(args.github_token_env, "")
        admin_token = os.environ.get(args.admin_token_env, "")
        if not main_token or not admin_token:
            fail("both read-only provider tokens are required")
        if hmac.compare_digest(main_token, admin_token):
            fail(
                "the Actions/Contents token and Administration token must be "
                "separate authorities"
            )
        client = GitHubApi(main_token)
        admin_client = GitHubApi(admin_token)
        payload = authenticate_provider_handoff(
            client,
            admin_client,
            input_artifact_id=args.input_artifact_id,
            expected_input_artifact_digest=args.expected_input_artifact_digest,
            expected_verifier_source_sha=args.expected_verifier_source_sha,
            now=now,
        )
        write_once(output, immutable_json_bytes(payload))
    except (ContractError, assembler.ContractError) as exc:
        print(
            f"global flagship provider authentication blocked: {exc}",
            file=sys.stderr,
        )
        return 1
    print(output)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build or verify the strictly non-publishing global flagship "
            "provider-authentication handoff."
        )
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    pack = subparsers.add_parser(
        "pack",
        help=(
            "revalidate and package proposal/candidate/final metadata only; "
            "release bytes are never included"
        ),
    )
    pack.add_argument("--proposal", required=True)
    pack.add_argument("--candidate", required=True)
    pack.add_argument("--final-receipt", required=True)
    pack.add_argument(
        "--approval",
        action="append",
        required=True,
        help="approval receipt; pass exactly once per required role",
    )
    pack.add_argument("--output", required=True)
    pack.set_defaults(handler=command_pack)

    verify = subparsers.add_parser(
        "verify",
        help=(
            "authenticate the metadata bundle and all approval provenance "
            "through read-only GitHub APIs"
        ),
    )
    verify.add_argument(
        "--input-artifact-id",
        type=assembler.bounded_integer(1, 9_007_199_254_740_991),
        required=True,
    )
    verify.add_argument(
        "--expected-input-artifact-digest",
        required=True,
        help="exact sha256:<lowercase-hex> digest from upload-artifact",
    )
    verify.add_argument(
        "--expected-verifier-source-sha",
        required=True,
        help="exact workflow checkout SHA; must equal the candidate source",
    )
    verify.add_argument(
        "--github-token-env",
        default="GITHUB_TOKEN",
        help="environment variable containing Actions/Contents read authority",
    )
    verify.add_argument(
        "--admin-token-env",
        default="CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN",
        help=(
            "environment variable containing separate single-repository "
            "Administration(read) authority"
        ),
    )
    verify.add_argument("--output", required=True)
    verify.set_defaults(handler=command_verify)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return int(args.handler(args))


if __name__ == "__main__":
    raise SystemExit(main())
