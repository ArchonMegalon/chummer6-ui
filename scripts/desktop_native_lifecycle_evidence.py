#!/usr/bin/env python3
"""Fail-closed contracts for native desktop install/update/uninstall evidence.

This module does not install, download, or publish anything.  Native runner
scripts perform those actions and then use this verifier to prove that the
resulting evidence is bound to exact candidate and N-1 artifact bytes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import zipfile
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable
from urllib.parse import unquote, urlsplit


N_MINUS_ONE_CONTRACT = "chummer6-ui.desktop-native-lifecycle-n-minus-one"
LIVE_PREDECESSOR_SELECTION_CONTRACT = (
    "chummer6-ui.desktop-native-live-predecessor-selection"
)
CANDIDATE_CONTRACT = "chummer6-ui.desktop-native-lifecycle-candidate"
RECEIPT_CONTRACT = "chummer6-ui.desktop-native-lifecycle-evidence"
CONTRACT_VERSION = 1
RERUN_POLICY = "same-actor-only"
FLAGSHIP_ADAPTER_CONTRACTS = {
    "windows": "chummer6-ui.flagship-native-e2e.windows.v1",
    "linux": "chummer6-ui.flagship-native-e2e.linux.v1",
}
FLAGSHIP_ARTIFACT_IDS = {
    "windows": "avalonia-win-x64-installer",
    "linux": "avalonia-linux-x64-installer",
}
LIVE_PREDECESSOR_PLATFORMS = {
    "windows": "win-x64",
    "linux": "linux-x64",
    "macos": "osx-arm64",
}
LIVE_PREDECESSOR_ARTIFACT_IDS = {
    "windows": "avalonia-win-x64-installer",
    "linux": "avalonia-linux-x64-installer",
    "macos": "avalonia-osx-arm64-installer",
}
FLAGSHIP_ARTIFACT_NAMES = {
    "windows": "chummer-avalonia-win-x64-installer.exe",
    "linux": "chummer-avalonia-linux-x64-installer.deb",
}
LINUX_CANDIDATE_PRODUCER_WORKFLOW = (
    ".github/workflows/linux-native-candidate-export.yml"
)
MAX_ARTIFACT_BYTES = 2 * 1024 * 1024 * 1024
MAX_EVIDENCE_BYTES = 512 * 1024 * 1024
MAX_WINDOWS_RELAY_AUTHORITY_BYTES = 64 * 1024
MAX_LIVE_RELEASE_CHANNEL_BYTES = 64 * 1024
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9.][A-Za-z0-9._+@/-]{0,255}$")
FLAGSHIP_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
GITHUB_LOGIN_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?|github-actions\[bot\])$"
)
PLATFORMS = {"windows": "win-x64", "linux": "linux-x64"}
FULL_REF_RE = re.compile(
    r"^refs/(?:heads|tags)/[A-Za-z0-9][A-Za-z0-9._/@+-]{0,238}$"
)
WORKFLOW_RE = re.compile(
    r"^\.github/workflows/[A-Za-z0-9][A-Za-z0-9._-]{0,127}\.ya?ml$"
)
ZULU_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)
PHASES = (
    "artifact_authentication",
    "clean_install_n_minus_one",
    "core_workflow_n_minus_one",
    "update_to_candidate",
    "core_workflow_candidate",
    "normal_uninstall",
)


class ContractError(RuntimeError):
    """The supplied authority or evidence cannot support a release claim."""


def fail(message: str) -> None:
    raise ContractError(message)


def canonical_json(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def duplicate_rejecting_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate key {key!r}")
        result[key] = value
    return result


def parse_canonical_json(raw: str, label: str) -> dict[str, Any]:
    try:
        value = json.loads(
            raw,
            object_pairs_hook=duplicate_rejecting_object,
            parse_constant=lambda constant: fail(
                f"{label} contains non-finite JSON number {constant!r}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    if canonical_json(value) != raw:
        fail(f"{label} must use exact canonical JSON serialization")
    return value


def exact_keys(value: Any, expected: set[str], label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    actual = set(value)
    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        fail(f"{label} has missing keys {missing} or extra keys {extra}")
    return value


def require_text(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or not PORTABLE_RE.fullmatch(value):
        fail(f"{label} must be a portable non-empty string")
    return value


def require_sha256(value: Any, label: str) -> str:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        fail(f"{label} must be an exact lowercase SHA-256")
    return value


def require_positive_integer(value: Any, label: str, *, maximum: int | None = None) -> int:
    if type(value) is not int or value < 1:
        fail(f"{label} must be a positive JSON integer")
    if maximum is not None and value > maximum:
        fail(f"{label} exceeds its fixed maximum")
    return value


def require_positive_integer_text(value: Any, label: str) -> str:
    if not isinstance(value, str) or not POSITIVE_INTEGER_RE.fullmatch(value):
        fail(f"{label} must be an exact positive integer string")
    if int(value) > (2**53 - 1):
        fail(f"{label} exceeds exact API integer authority")
    return value


def parse_timestamp(value: Any, label: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        fail(f"{label} must be an RFC3339 UTC timestamp")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        fail(f"{label} is not a valid timestamp: {exc}")
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        fail(f"{label} must be UTC")
    return parsed


def require_platform(platform: str, rid: str) -> None:
    if platform not in PLATFORMS or PLATFORMS[platform] != rid:
        fail(f"unsupported or mismatched native platform tuple: {platform}/{rid}")


def require_live_predecessor_platform(platform: str, rid: str) -> None:
    if (
        platform not in LIVE_PREDECESSOR_PLATFORMS
        or LIVE_PREDECESSOR_PLATFORMS[platform] != rid
    ):
        fail(
            "unsupported or mismatched live-predecessor platform tuple: "
            f"{platform}/{rid}"
        )


def safe_relative(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or "\\" in value:
        fail(f"{label} must be a normalized relative POSIX path")
    relative = PurePosixPath(value)
    if (
        relative.is_absolute()
        or relative.as_posix() != value
        or any(part in {"", ".", ".."} for part in relative.parts)
    ):
        fail(f"{label} must be a normalized relative POSIX path")
    return value


def _stable_regular(
    path: Path, label: str, maximum: int, *, collect: bool
) -> tuple[str, int, bytes | None]:
    absolute = Path(os.path.abspath(path))
    current = Path(absolute.anchor)
    reparse = int(getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
    for component in absolute.parts[1:]:
        current /= component
        try:
            state = os.stat(current, follow_symlinks=False)
        except OSError as exc:
            fail(f"unable to inspect {label}: {exc}")
        if stat.S_ISLNK(state.st_mode) or (
            reparse and int(getattr(state, "st_file_attributes", 0)) & reparse
        ):
            fail(f"{label} must not traverse a symlink or reparse point")
    flags = os.O_RDONLY | int(getattr(os, "O_CLOEXEC", 0)) | int(
        getattr(os, "O_NOFOLLOW", 0)
    )
    try:
        descriptor = os.open(absolute, flags)
    except OSError as exc:
        fail(f"{label} must be a readable regular file: {exc}")
    digest = hashlib.sha256()
    size = 0
    chunks: list[bytes] | None = [] if collect else None
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            fail(f"{label} must be a singly linked regular file")
        if before.st_size < 1 or before.st_size > maximum:
            fail(f"{label} size is outside its fixed bound")
        while True:
            chunk = os.read(descriptor, min(1024 * 1024, maximum - size + 1))
            if not chunk:
                break
            digest.update(chunk)
            size += len(chunk)
            if chunks is not None:
                chunks.append(chunk)
            if size > maximum:
                fail(f"{label} grew beyond its fixed bound")
        after = os.fstat(descriptor)
        if (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
        ) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        ):
            fail(f"{label} changed while it was read")
    finally:
        os.close(descriptor)
    return digest.hexdigest(), size, b"".join(chunks) if chunks is not None else None


def stable_regular_file(path: Path, label: str, maximum: int) -> tuple[str, int]:
    digest, size, _ = _stable_regular(path, label, maximum, collect=False)
    return digest, size


def stable_regular_bytes(path: Path, label: str, maximum: int) -> tuple[str, int, bytes]:
    digest, size, data = _stable_regular(path, label, maximum, collect=True)
    assert data is not None
    return digest, size, data


def validate_immutable_url(
    raw: Any,
    *,
    label: str,
    generation_id: str,
    expected_file_name: str | None = None,
) -> str:
    if not isinstance(raw, str):
        fail(f"{label} must be a URL string")
    parsed = urlsplit(raw)
    if (
        parsed.scheme != "https"
        or parsed.hostname != "chummer.run"
        or parsed.username is not None
        or parsed.password is not None
        or parsed.port not in {None, 443}
        or parsed.query
        or parsed.fragment
    ):
        fail(f"{label} must be a credential-free immutable HTTPS URL on chummer.run")
    decoded_parts = [unquote(part) for part in parsed.path.split("/") if part]
    if any(part in {"", ".", ".."} or "/" in part or "\\" in part for part in decoded_parts):
        fail(f"{label} contains an unsafe encoded path component")
    if generation_id not in decoded_parts:
        fail(f"{label} is not bound to generation {generation_id}")
    if expected_file_name is not None and (
        not decoded_parts or decoded_parts[-1] != expected_file_name
    ):
        fail(f"{label} does not end in the declared artifact file name")
    return raw


def validate_n_minus_one(raw: str, platform: str, rid: str) -> dict[str, Any]:
    require_live_predecessor_platform(platform, rid)
    binding_keys = {
        "artifactFileName",
        "artifactSha256",
        "artifactSizeBytes",
        "artifactUrl",
        "contractName",
        "contractVersion",
        "generationId",
        "manifestSha256",
        "manifestUrl",
        "platform",
        "releasedAt",
        "rid",
        "version",
    }
    if platform == "windows":
        binding_keys.update(
            {"payloadFileName", "payloadSha256", "payloadSizeBytes", "payloadUrl"}
        )
    value = exact_keys(
        parse_canonical_json(raw, "N-1 binding"),
        binding_keys,
        "N-1 binding",
    )
    if (
        value["contractName"] != N_MINUS_ONE_CONTRACT
        or type(value["contractVersion"]) is not int
        or value["contractVersion"] != CONTRACT_VERSION
    ):
        fail("N-1 binding contract is invalid")
    if value["platform"] != platform or value["rid"] != rid:
        fail("N-1 binding platform tuple differs from the native lane")
    generation = require_text(value["generationId"], "N-1 generationId")
    file_name = require_text(value["artifactFileName"], "N-1 artifactFileName")
    if "/" in file_name:
        fail("N-1 artifactFileName must be a basename")
    require_text(value["version"], "N-1 version")
    require_sha256(value["artifactSha256"], "N-1 artifactSha256")
    require_sha256(value["manifestSha256"], "N-1 manifestSha256")
    require_positive_integer(
        value["artifactSizeBytes"], "N-1 artifactSizeBytes", maximum=MAX_ARTIFACT_BYTES
    )
    released_at = parse_timestamp(value["releasedAt"], "N-1 releasedAt")
    if released_at > datetime.now(UTC) + timedelta(minutes=5):
        fail("N-1 releasedAt is in the future")
    validate_immutable_url(
        value["artifactUrl"],
        label="N-1 artifactUrl",
        generation_id=generation,
        expected_file_name=file_name,
    )
    validate_immutable_url(
        value["manifestUrl"],
        label="N-1 manifestUrl",
        generation_id=generation,
    )
    if platform == "windows":
        payload_name = require_text(value["payloadFileName"], "N-1 payloadFileName")
        if "/" in payload_name:
            fail("N-1 payloadFileName must be a basename")
        require_sha256(value["payloadSha256"], "N-1 payloadSha256")
        require_positive_integer(
            value["payloadSizeBytes"],
            "N-1 payloadSizeBytes",
            maximum=MAX_ARTIFACT_BYTES,
        )
        validate_immutable_url(
            value["payloadUrl"],
            label="N-1 payloadUrl",
            generation_id=generation,
            expected_file_name=payload_name,
        )
    return value


def validate_windows_relay_authority(
    raw: str,
    live_release_channel_raw: str,
    certificate_sha256: str,
    spki_sha256: str,
    *,
    expected_sha256: str | None = None,
    expected_live_release_channel_sha256: str | None = None,
    expected_selected_tuple_sha256: str | None = None,
) -> dict[str, Any]:
    predecessor = validate_live_predecessor_authority(
        raw,
        live_release_channel_raw,
        "windows",
        "win-x64",
        expected_n_minus_one_sha256=expected_sha256,
        expected_live_release_channel_sha256=(
            expected_live_release_channel_sha256
        ),
        expected_selected_tuple_sha256=expected_selected_tuple_sha256,
    )
    certificate = require_sha256(
        certificate_sha256,
        "Windows relay Authenticode signer certificate SHA-256",
    )
    spki = require_sha256(
        spki_sha256,
        "Windows relay Authenticode signer SPKI SHA-256",
    )
    return {
        "artifactSha256": predecessor["artifactSha256"],
        "certificateSha256": certificate,
        "generationId": predecessor["generationId"],
        "liveReleaseChannelSha256": predecessor["liveReleaseChannelSha256"],
        "manifestSha256": predecessor["manifestSha256"],
        "payloadSha256": predecessor["payloadSha256"],
        "selectedTupleSha256": predecessor["selectedTupleSha256"],
        "sha256": predecessor["nMinusOneReleaseSha256"],
        "spkiSha256": spki,
        "version": predecessor["version"],
    }


def manifest_download_path(raw: Any, generation_id: str, label: str) -> str:
    if not isinstance(raw, str) or not raw:
        fail(f"{label} must be a non-empty URL path")
    parsed = urlsplit(raw)
    if parsed.scheme:
        if (
            parsed.scheme != "https"
            or parsed.hostname != "chummer.run"
            or parsed.username is not None
            or parsed.password is not None
            or parsed.port not in {None, 443}
        ):
            fail(f"{label} absolute URL authority is invalid")
    elif parsed.netloc or not parsed.path.startswith("/"):
        fail(f"{label} must be a root-relative path or chummer.run HTTPS URL")
    if parsed.query or parsed.fragment:
        fail(f"{label} must not contain a query or fragment")
    decoded_parts = [unquote(part) for part in parsed.path.split("/") if part]
    if (
        generation_id not in decoded_parts
        or any(part in {"", ".", ".."} or "/" in part or "\\" in part for part in decoded_parts)
    ):
        fail(f"{label} is not a safe immutable generation path")
    return parsed.path


def _bounded_utf8(raw: Any, label: str, maximum: int) -> bytes:
    if not isinstance(raw, str):
        fail(f"{label} must be an exact JSON string")
    try:
        encoded = raw.encode("utf-8")
    except UnicodeEncodeError as exc:
        fail(f"{label} is not UTF-8 encodable: {exc}")
    if not encoded or len(encoded) > maximum:
        fail(f"{label} is outside its fixed byte bound")
    return encoded


def _parse_release_channel(raw: str, label: str) -> dict[str, Any]:
    try:
        value = json.loads(
            raw,
            object_pairs_hook=duplicate_rejecting_object,
            parse_constant=lambda constant: fail(
                f"{label} contains non-finite JSON number {constant!r}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    return value


def _release_channel_artifact(
    manifest: dict[str, Any],
    binding: dict[str, Any],
    platform: str,
    rid: str,
    label: str,
) -> dict[str, Any]:
    if (
        manifest.get("contractName") != "Chummer.Hub.Registry.Contracts"
        or type(manifest.get("schemaVersion")) is not int
        or manifest.get("schemaVersion") != 1
        or manifest.get("status") != "published"
        or manifest.get("generationId") != binding["generationId"]
    ):
        fail(f"{label} contract, status, or generation is invalid")

    versions = [
        manifest[key]
        for key in ("releaseVersion", "version")
        if key in manifest
    ]
    if not versions or any(value != binding["version"] for value in versions):
        fail(f"{label} release version differs from its binding")
    published_values = [
        manifest[key]
        for key in ("publishedAt", "generatedAt")
        if key in manifest
    ]
    if (
        not published_values
        or any(value != binding["releasedAt"] for value in published_values)
    ):
        fail(f"{label} publication time differs from its binding")
    parse_timestamp(published_values[0], f"{label} publication time")

    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        fail(f"{label} artifacts must be an array")
    expected_artifact_id = LIVE_PREDECESSOR_ARTIFACT_IDS[platform]
    matches: list[dict[str, Any]] = []
    for row in artifacts:
        if not isinstance(row, dict):
            continue
        row_ids = [
            row[key]
            for key in ("artifactId", "id")
            if key in row
        ]
        if (
            row.get("platform") == platform
            and row.get("rid") == rid
            and expected_artifact_id in row_ids
        ):
            matches.append(row)
    if len(matches) != 1:
        fail(f"{label} does not select one exact flagship artifact")
    artifact = matches[0]
    row_ids = [
        artifact[key]
        for key in ("artifactId", "id")
        if key in artifact
    ]
    if not row_ids or any(value != expected_artifact_id for value in row_ids):
        fail(f"{label} flagship artifact identifier aliases conflict")
    if platform == "windows":
        native = artifact.get("nativeHostEvidence")
        if (
            artifact.get("executionEnvironment") != "native_windows"
            or artifact.get("verificationScope") != "native_windows_startup"
            or not isinstance(native, dict)
            or native.get("contractName")
            != "chummer6-ui.native_windows_host_evidence"
            or native.get("status") != "verified"
            or native.get("isNativeWindows") is not True
            or native.get("hostPlatform") != "windows"
        ):
            fail(
                f"{label} Windows artifact lacks verified native-host "
                "flagship evidence"
            )

    expected_artifact = {
        "fileName": binding["artifactFileName"],
        "sha256": binding["artifactSha256"],
        "sizeBytes": binding["artifactSizeBytes"],
    }
    for key, expected in expected_artifact.items():
        if type(artifact.get(key)) is not type(expected) or artifact.get(key) != expected:
            fail(f"{label} artifact {key} differs from its binding")
    artifact_versions = [
        artifact[key]
        for key in ("releaseVersion", "version")
        if key in artifact
    ]
    if not artifact_versions or any(
        value != binding["version"] for value in artifact_versions
    ):
        fail(f"{label} artifact version differs from its binding")
    artifact_path = manifest_download_path(
        artifact.get("downloadUrl"),
        binding["generationId"],
        f"{label} artifact URL",
    )
    if artifact_path != urlsplit(binding["artifactUrl"]).path:
        fail(f"{label} artifact URL differs from its binding")

    result = {
        "artifact": artifact,
        "artifactDownloadPath": artifact_path,
        "artifactId": expected_artifact_id,
    }
    if platform == "windows":
        expected_payload = {
            "payloadFileName": binding["payloadFileName"],
            "payloadSha256": binding["payloadSha256"],
            "payloadSizeBytes": binding["payloadSizeBytes"],
        }
        for key, expected in expected_payload.items():
            if type(artifact.get(key)) is not type(expected) or artifact.get(key) != expected:
                fail(f"{label} {key} differs from its binding")
        result["payloadDownloadPath"] = manifest_download_path(
            artifact.get("payloadDownloadUrl"),
            binding["generationId"],
            f"{label} payload URL",
        )
    return result


def validate_live_predecessor_authority(
    raw_binding: str,
    live_release_channel_raw: str,
    platform: str,
    rid: str,
    *,
    expected_n_minus_one_sha256: str | None = None,
    expected_live_release_channel_sha256: str | None = None,
    expected_selected_tuple_sha256: str | None = None,
) -> dict[str, Any]:
    """Bind N-1 to the exact artifact selected by the current public root."""

    require_live_predecessor_platform(platform, rid)
    binding_bytes = _bounded_utf8(
        raw_binding,
        "N-1 release authority",
        MAX_WINDOWS_RELAY_AUTHORITY_BYTES,
    )
    live_bytes = _bounded_utf8(
        live_release_channel_raw,
        "live release-channel authority",
        MAX_LIVE_RELEASE_CHANNEL_BYTES,
    )
    binding = validate_n_minus_one(raw_binding, platform, rid)
    manifest = _parse_release_channel(
        live_release_channel_raw,
        "live release-channel authority",
    )
    selected = _release_channel_artifact(
        manifest,
        binding,
        platform,
        rid,
        "live release-channel authority",
    )

    n_minus_one_sha256 = hashlib.sha256(binding_bytes).hexdigest()
    live_sha256 = hashlib.sha256(live_bytes).hexdigest()
    if expected_n_minus_one_sha256 is not None:
        expected = require_sha256(
            expected_n_minus_one_sha256,
            "expected N-1 release authority SHA-256",
        )
        if n_minus_one_sha256 != expected:
            fail("N-1 release authority bytes differ from the expected SHA-256")
    if expected_live_release_channel_sha256 is not None:
        expected = require_sha256(
            expected_live_release_channel_sha256,
            "expected live release-channel authority SHA-256",
        )
        if live_sha256 != expected:
            fail(
                "live release-channel authority bytes differ from the expected "
                "SHA-256"
            )

    selected_tuple: dict[str, Any] = {
        "artifact": {
            "artifactId": selected["artifactId"],
            "downloadPath": selected["artifactDownloadPath"],
            "fileName": binding["artifactFileName"],
            "sha256": binding["artifactSha256"],
            "sizeBytes": binding["artifactSizeBytes"],
            "url": binding["artifactUrl"],
        },
        "contractName": LIVE_PREDECESSOR_SELECTION_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generationId": binding["generationId"],
        "liveReleaseChannelSha256": live_sha256,
        "manifest": {
            "sha256": binding["manifestSha256"],
            "url": binding["manifestUrl"],
        },
        "nMinusOneReleaseSha256": n_minus_one_sha256,
        "platform": platform,
        "releasedAt": binding["releasedAt"],
        "rid": rid,
        "version": binding["version"],
    }
    if platform == "windows":
        selected_tuple["payload"] = {
            "downloadPath": selected["payloadDownloadPath"],
            "fileName": binding["payloadFileName"],
            "sha256": binding["payloadSha256"],
            "sizeBytes": binding["payloadSizeBytes"],
            "url": binding["payloadUrl"],
        }
    selected_tuple_sha256 = hashlib.sha256(
        canonical_json(selected_tuple).encode("utf-8")
    ).hexdigest()
    if expected_selected_tuple_sha256 is not None:
        expected = require_sha256(
            expected_selected_tuple_sha256,
            "expected live-predecessor selected-tuple SHA-256",
        )
        if selected_tuple_sha256 != expected:
            fail("live-predecessor selected tuple differs from the expected SHA-256")

    result = {
        "artifactSha256": binding["artifactSha256"],
        "generationId": binding["generationId"],
        "liveReleaseChannelSha256": live_sha256,
        "manifestSha256": binding["manifestSha256"],
        "nMinusOneReleaseSha256": n_minus_one_sha256,
        "selectedTupleSha256": selected_tuple_sha256,
        "version": binding["version"],
    }
    if platform == "windows":
        result["payloadSha256"] = binding["payloadSha256"]
    return result


def validate_downloaded_n_minus_one_manifest(
    path: Path,
    raw_binding: str,
    platform: str,
    rid: str,
) -> dict[str, Any]:
    binding = validate_n_minus_one(raw_binding, platform, rid)
    digest, _, data = stable_regular_bytes(
        path, "downloaded N-1 manifest", 8 * 1024 * 1024
    )
    if digest != binding["manifestSha256"]:
        fail("downloaded N-1 manifest SHA-256 differs from its binding")
    try:
        raw_manifest = data.decode("utf-8-sig")
    except UnicodeError as exc:
        fail(f"downloaded N-1 manifest is invalid JSON: {exc}")
    manifest = _parse_release_channel(raw_manifest, "downloaded N-1 manifest")
    _release_channel_artifact(
        manifest,
        binding,
        platform,
        rid,
        "downloaded N-1 manifest",
    )
    return {
        "artifactSha256": binding["artifactSha256"],
        "generationId": binding["generationId"],
        "manifestSha256": digest,
        "version": binding["version"],
    }


def receipt_n_minus_one_binding(
    previous: dict[str, Any], platform: str, rid: str
) -> dict[str, Any]:
    binding: dict[str, Any] = {
        "artifactFileName": previous["artifactFileName"],
        "artifactSha256": previous["sha256"],
        "artifactSizeBytes": previous["sizeBytes"],
        "artifactUrl": previous["artifactUrl"],
        "contractName": N_MINUS_ONE_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generationId": previous["generationId"],
        "manifestSha256": previous["manifestSha256"],
        "manifestUrl": previous["manifestUrl"],
        "platform": platform,
        "releasedAt": previous["releasedAt"],
        "rid": rid,
        "version": previous["version"],
    }
    if platform == "windows":
        payload = previous["payload"]
        binding.update(
            {
                "payloadFileName": payload["fileName"],
                "payloadSha256": payload["sha256"],
                "payloadSizeBytes": payload["sizeBytes"],
                "payloadUrl": payload["url"],
            }
        )
    return binding


def validate_candidate(
    raw: str,
    platform: str,
    rid: str,
    candidate_root: Path | None = None,
) -> dict[str, Any]:
    require_platform(platform, rid)
    value = exact_keys(
        parse_canonical_json(raw, "candidate binding"),
        {
            "artifactFileName",
            "artifactMemberPath",
            "artifactSha256",
            "artifactSizeBytes",
            "contractName",
            "contractVersion",
            "platform",
            "producedAt",
            "producer",
            "rid",
            "version",
        },
        "candidate binding",
    )
    if (
        value["contractName"] != CANDIDATE_CONTRACT
        or type(value["contractVersion"]) is not int
        or value["contractVersion"] != CONTRACT_VERSION
    ):
        fail("candidate binding contract is invalid")
    if value["platform"] != platform or value["rid"] != rid:
        fail("candidate binding platform tuple differs from the native lane")
    file_name = require_text(value["artifactFileName"], "candidate artifactFileName")
    if "/" in file_name:
        fail("candidate artifactFileName must be a basename")
    member = safe_relative(value["artifactMemberPath"], "candidate artifactMemberPath")
    if PurePosixPath(member).name != file_name:
        fail("candidate artifact member basename differs from artifactFileName")
    require_text(value["version"], "candidate version")
    require_sha256(value["artifactSha256"], "candidate artifactSha256")
    require_positive_integer(
        value["artifactSizeBytes"], "candidate artifactSizeBytes", maximum=MAX_ARTIFACT_BYTES
    )
    produced_at = parse_timestamp(value["producedAt"], "candidate producedAt")
    if produced_at > datetime.now(UTC) + timedelta(minutes=5):
        fail("candidate producedAt is in the future")
    producer = exact_keys(
        value["producer"],
        {
            "actor",
            "artifactId",
            "artifactName",
            "artifactZipSha256",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        "candidate producer",
    )
    if not isinstance(producer["repository"], str) or not REPOSITORY_RE.fullmatch(
        producer["repository"]
    ):
        fail("candidate producer repository is invalid")
    if producer["repository"] != "ArchonMegalon/chummer6-ui":
        fail("candidate producer repository is not the governed UI repository")
    if not isinstance(producer["actor"], str) or not GITHUB_LOGIN_RE.fullmatch(
        producer["actor"]
    ):
        fail("candidate producer actor is invalid")
    if (
        not isinstance(producer["workflow"], str)
        or not WORKFLOW_RE.fullmatch(producer["workflow"])
    ):
        fail("candidate producer workflow is invalid")
    if (
        platform == "linux"
        and producer["workflow"] != LINUX_CANDIDATE_PRODUCER_WORKFLOW
    ):
        fail("Linux candidate producer workflow is not the governed export lane")
    if producer["ref"] != "refs/heads/main":
        fail("candidate producer ref must be the governed main branch")
    if not isinstance(producer["sha"], str) or not COMMIT_RE.fullmatch(producer["sha"]):
        fail("candidate producer sha is invalid")
    for key in ("runId", "runAttempt", "artifactId"):
        require_positive_integer_text(producer[key], f"candidate producer {key}")
    require_text(producer["artifactName"], "candidate producer artifactName")
    require_sha256(producer["artifactZipSha256"], "candidate producer artifactZipSha256")
    if candidate_root is not None:
        root = Path(os.path.abspath(candidate_root))
        candidate_path = root.joinpath(*PurePosixPath(member).parts)
        try:
            candidate_path.relative_to(root)
        except ValueError:
            fail("candidate artifact member escapes candidate root")
        digest, size = stable_regular_file(
            candidate_path, "candidate artifact member", MAX_ARTIFACT_BYTES
        )
        if digest != value["artifactSha256"] or size != value["artifactSizeBytes"]:
            fail("candidate artifact member bytes differ from their binding")
        value = dict(value)
        value["resolvedPath"] = str(candidate_path)
    return value


def materialize_candidate(
    archive_path: Path,
    raw: str,
    platform: str,
    rid: str,
    output_root: Path,
) -> dict[str, Any]:
    value = validate_candidate(raw, platform, rid)
    archive_sha256, archive_size = stable_regular_file(
        archive_path, "candidate artifact ZIP", MAX_ARTIFACT_BYTES
    )
    if archive_sha256 != value["producer"]["artifactZipSha256"]:
        fail("candidate artifact ZIP bytes differ from authenticated API authority")
    output_root = Path(os.path.abspath(output_root))
    if output_root.exists():
        if output_root.is_symlink() or any(output_root.iterdir()):
            fail("candidate output root must be an empty regular directory")
    else:
        output_root.mkdir(parents=True, mode=0o700)
    member_name = value["artifactMemberPath"]
    descriptor = os.open(
        Path(os.path.abspath(archive_path)),
        os.O_RDONLY
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_NOFOLLOW", 0)),
    )
    try:
        before = os.fstat(descriptor)
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            live_digest = hashlib.sha256()
            live_size = 0
            while True:
                chunk = handle.read(1024 * 1024)
                if not chunk:
                    break
                live_digest.update(chunk)
                live_size += len(chunk)
                if live_size > MAX_ARTIFACT_BYTES:
                    fail("candidate artifact ZIP exceeded its fixed read bound")
            if live_size != archive_size or live_digest.hexdigest() != archive_sha256:
                fail("candidate artifact ZIP changed before materialization")
            handle.seek(0)
            with zipfile.ZipFile(handle) as archive:
                infos = archive.infolist()
                if not infos or len(infos) > 50_000:
                    fail("candidate artifact ZIP member count is outside its fixed bound")
                names: set[str] = set()
                selected: zipfile.ZipInfo | None = None
                for info in infos:
                    raw_name = info.filename[:-1] if info.is_dir() else info.filename
                    normalized = safe_relative(raw_name, "candidate ZIP member path")
                    if normalized in names:
                        fail("candidate artifact ZIP contains duplicate member names")
                    names.add(normalized)
                    unix_mode = (info.external_attr >> 16) & 0xFFFF
                    if unix_mode and stat.S_ISLNK(unix_mode):
                        fail("candidate artifact ZIP contains a symbolic-link member")
                    if normalized == member_name:
                        selected = info
                if selected is None or selected.is_dir():
                    fail("candidate artifact ZIP does not contain the declared package member")
                if (
                    selected.file_size != value["artifactSizeBytes"]
                    or selected.file_size < 1
                    or selected.file_size > MAX_ARTIFACT_BYTES
                    or selected.compress_size < 1
                    or selected.file_size > selected.compress_size * 100 + 1024 * 1024
                ):
                    fail("candidate package ZIP metadata differs or exceeds extraction bounds")
                target = output_root.joinpath(*PurePosixPath(member_name).parts)
                target.parent.mkdir(parents=True, mode=0o700)
                flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | int(
                    getattr(os, "O_CLOEXEC", 0)
                )
                target_descriptor = os.open(target, flags, 0o600)
                digest = hashlib.sha256()
                size = 0
                try:
                    with (
                        archive.open(selected, "r") as source,
                        os.fdopen(target_descriptor, "wb", closefd=True) as destination,
                    ):
                        target_descriptor = -1
                        while True:
                            chunk = source.read(1024 * 1024)
                            if not chunk:
                                break
                            size += len(chunk)
                            if size > value["artifactSizeBytes"]:
                                fail("candidate package exceeded its declared extraction size")
                            digest.update(chunk)
                            destination.write(chunk)
                        destination.flush()
                        os.fsync(destination.fileno())
                finally:
                    if target_descriptor >= 0:
                        os.close(target_descriptor)
                if (
                    size != value["artifactSizeBytes"]
                    or digest.hexdigest() != value["artifactSha256"]
                ):
                    target.unlink(missing_ok=True)
                    fail("materialized candidate package bytes differ from their binding")
            after = os.fstat(handle.fileno())
        if (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
        ) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        ) or after.st_size != archive_size:
            fail("candidate artifact ZIP changed during materialization")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return validate_candidate(raw, platform, rid, output_root)


def file_binding(root: Path, raw: Any, label: str) -> dict[str, Any]:
    row = exact_keys(raw, {"path", "role", "sha256", "sizeBytes"}, label)
    relative = safe_relative(row["path"], f"{label} path")
    role = require_text(row["role"], f"{label} role")
    digest = require_sha256(row["sha256"], f"{label} sha256")
    size = require_positive_integer(
        row["sizeBytes"], f"{label} sizeBytes", maximum=MAX_EVIDENCE_BYTES
    )
    path = root.joinpath(*PurePosixPath(relative).parts)
    actual_digest, actual_size = stable_regular_file(path, label, MAX_EVIDENCE_BYTES)
    if actual_digest != digest or actual_size != size:
        fail(f"{label} bytes differ from their receipt binding")
    return {"path": relative, "role": role, "sha256": digest, "sizeBytes": size}


def require_pass_receipt_file(
    root: Path,
    binding: Any,
    label: str,
    *,
    expected_version: str,
    expected_artifact_sha256: str,
    platform: str,
    rid: str,
    receipt_kind: str,
) -> dict[str, Any]:
    row = exact_keys(binding, {"path", "role", "sha256", "sizeBytes"}, label)
    relative = safe_relative(row["path"], f"{label} path")
    require_text(row["role"], f"{label} role")
    expected_file_digest = require_sha256(row["sha256"], f"{label} sha256")
    expected_size = require_positive_integer(
        row["sizeBytes"], f"{label} sizeBytes", maximum=MAX_EVIDENCE_BYTES
    )
    path = root.joinpath(*PurePosixPath(relative).parts)
    actual_digest, actual_size, data = stable_regular_bytes(
        path, label, MAX_EVIDENCE_BYTES
    )
    if actual_digest != expected_file_digest or actual_size != expected_size:
        fail(f"{label} bytes differ from their receipt binding")
    try:
        payload = json.loads(
            data.decode("utf-8-sig"),
            object_pairs_hook=duplicate_rejecting_object,
        )
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"{label} must contain UTF-8 JSON: {exc}")
    if not isinstance(payload, dict) or payload.get("status") != "pass":
        fail(f"{label} does not contain a passing receipt")
    expected_host_class = f"github-actions-{platform}-x64"
    expected_artifact_digest = f"sha256:{expected_artifact_sha256}"
    if (
        payload.get("headId") != "avalonia"
        or payload.get("version") != expected_version
        or payload.get("releaseVersion") != expected_version
        or payload.get("platform") != platform
        or payload.get("arch") != "x64"
        or payload.get("rid") != rid
        or payload.get("hostClass") != expected_host_class
        or payload.get("artifactDigest") != expected_artifact_digest
        or payload.get("artifactDigestSource") != "environment"
    ):
        fail(f"{label} does not bind the exact native release artifact")
    if receipt_kind == "startup":
        if payload.get("readyCheckpoint") != "pre_ui_event_loop":
            fail(f"{label} did not reach the required startup checkpoint")
    elif receipt_kind == "mouse-first":
        if (
            payload.get("journeyMode") != "mouse_first_live_binary"
            or type(payload.get("pointerActionCount")) is not int
            or payload["pointerActionCount"] < 1
            or not isinstance(payload.get("steps"), list)
            or not payload["steps"]
            or payload.get("error") is not None
        ):
            fail(f"{label} does not prove a live pointer-driven core workflow")
    else:
        fail(f"{label} uses an unsupported receipt kind")
    return {
        "path": relative,
        "role": row["role"],
        "sha256": expected_file_digest,
        "sizeBytes": expected_size,
    }


def validate_keylocker_signing_receipt(
    payload: Any,
    *,
    candidate: dict[str, Any],
    rid: str,
    certificate_pin: str,
    spki_pin: str,
) -> None:
    if not isinstance(payload, dict):
        fail("candidate v2 signing receipt must be an object")
    expected_scalars: dict[str, Any] = {
        "contractName": "chummer6-ui.desktop_artifact_signing",
        "contractVersion": 2,
        "platform": "windows",
        "rid": rid,
        "releaseVersion": candidate["version"],
        "signingStatus": "pass",
        "signingBackend": "digicert_keylocker_linux_jsign",
        "digestAlgorithm": "sha256",
    }
    for key, expected in expected_scalars.items():
        if type(payload.get(key)) is not type(expected) or payload.get(key) != expected:
            fail(f"candidate v2 signing receipt {key} is invalid")

    signer = payload.get("signer")
    if (
        not isinstance(signer, dict)
        or signer.get("certificateSha256") != certificate_pin
        or signer.get("spkiSha256") != spki_pin
    ):
        fail("candidate v2 signing receipt signer pins differ from lifecycle authority")
    require_sha256(signer.get("certificateSha256"), "v2 signing signer certificateSha256")
    require_sha256(signer.get("spkiSha256"), "v2 signing signer spkiSha256")

    timestamp = payload.get("timestamp")
    if (
        not isinstance(timestamp, dict)
        or timestamp.get("protocol") != "rfc3161"
        or timestamp.get("digestAlgorithm") != "sha256"
        or timestamp.get("status") != "verified"
    ):
        fail("candidate v2 signing receipt timestamp authority is invalid")

    signatures = payload.get("artifactSignatures")
    if not isinstance(signatures, list):
        fail("candidate v2 signing receipt artifactSignatures must be an array")
    matching_signatures = [
        row
        for row in signatures
        if isinstance(row, dict)
        and row.get("artifactFileName") == candidate["artifactFileName"]
        and row.get("artifactSha256") == candidate["sha256"]
    ]
    if len(matching_signatures) != 1:
        fail("candidate v2 signing receipt does not bind one exact installer signature")
    signature = matching_signatures[0]
    signature_signer = signature.get("signer")
    signer_chain = signature.get("signerChain")
    signature_timestamp = signature.get("timestamp")
    timestamp_chain = (
        signature_timestamp.get("chain")
        if isinstance(signature_timestamp, dict)
        else None
    )
    verifier = signature.get("verifier")
    if (
        signature.get("digestAlgorithm") != "sha256"
        or signature.get("cryptographicVerification") != "passed"
        or not isinstance(signature_signer, dict)
        or signature_signer.get("certificateSha256") != certificate_pin
        or signature_signer.get("spkiSha256") != spki_pin
        or signature_signer != signer
        or not isinstance(signer_chain, dict)
        or signer_chain.get("trusted") is not True
        or not isinstance(signature_timestamp, dict)
        or signature_timestamp.get("status") != "verified"
        or signature_timestamp.get("format") != "rfc3161"
        or signature_timestamp.get("digestAlgorithm") != "sha256"
        or not isinstance(timestamp_chain, dict)
        or timestamp_chain.get("trusted") is not True
        or not isinstance(verifier, dict)
        or verifier.get("providerIndependent") is not True
        or verifier.get("jsignOutputTrusted") is not False
    ):
        fail("candidate v2 signing receipt signature evidence is invalid")

    artifacts = payload.get("artifacts")
    if not isinstance(artifacts, list):
        fail("candidate v2 signing receipt artifacts must be an array")
    matching_artifacts = [
        row
        for row in artifacts
        if isinstance(row, dict)
        and row.get("fileName") == candidate["artifactFileName"]
        and row.get("sha256") == candidate["sha256"]
        and row.get("kind") == "installer"
        and row.get("signingStatus") == "pass"
    ]
    if len(matching_artifacts) != 1:
        fail("candidate v2 signing receipt artifact row is invalid")


def validate_receipt(path: Path, evidence_root: Path) -> dict[str, Any]:
    digest, receipt_size, receipt_bytes = stable_regular_bytes(
        path, "lifecycle receipt", MAX_EVIDENCE_BYTES
    )
    try:
        receipt = json.loads(
            receipt_bytes.decode("utf-8-sig"),
            object_pairs_hook=duplicate_rejecting_object,
        )
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"lifecycle receipt is invalid JSON: {exc}")
    receipt = exact_keys(
        receipt,
        {
            "candidate",
            "contractName",
            "contractVersion",
            "coreWorkflow",
            "evidenceFiles",
            "generatedAt",
            "nMinusOne",
            "nativeRunner",
            "packageAuthority",
            "phases",
            "platform",
            "rid",
            "statePreservation",
            "status",
            "uninstall",
        },
        "lifecycle receipt",
    )
    if (
        receipt["contractName"] != RECEIPT_CONTRACT
        or type(receipt["contractVersion"]) is not int
        or receipt["contractVersion"] != CONTRACT_VERSION
        or receipt["status"] != "passed"
    ):
        fail("lifecycle receipt contract or status is invalid")
    platform = receipt["platform"]
    rid = receipt["rid"]
    require_platform(platform, rid)
    if (
        not isinstance(receipt["generatedAt"], str)
        or not ZULU_RE.fullmatch(receipt["generatedAt"])
    ):
        fail("receipt generatedAt must use whole-second RFC3339 UTC form")
    generated_at = parse_timestamp(receipt["generatedAt"], "receipt generatedAt")
    if generated_at > datetime.now(UTC) + timedelta(minutes=5):
        fail("receipt generatedAt is in the future")
    if datetime.now(UTC) - generated_at > timedelta(hours=24):
        fail("lifecycle receipt is stale")

    runner = exact_keys(
        receipt["nativeRunner"],
        {"architecture", "environment", "kernel", "runnerName", "runnerOs", "source"},
        "nativeRunner",
    )
    expected_os = "Windows" if platform == "windows" else "Linux"
    if runner["environment"] != "native" or runner["runnerOs"] != expected_os:
        fail("lifecycle evidence was not captured on the matching native runner")
    if str(runner["architecture"]).lower() not in {"x64", "amd64", "x86_64"}:
        fail("native runner architecture does not match the x64 RID")
    for key in ("kernel", "runnerName"):
        require_text(runner[key], f"nativeRunner {key}")
    source = exact_keys(
        runner["source"],
        {
            "actor",
            "ref",
            "repository",
            "rerunPolicy",
            "runAttempt",
            "runId",
            "sha",
            "triggeringActor",
            "workflow",
        },
        "nativeRunner source",
    )
    if not isinstance(source["repository"], str) or not REPOSITORY_RE.fullmatch(
        source["repository"]
    ):
        fail("nativeRunner source repository is invalid")
    if source["repository"] != "ArchonMegalon/chummer6-ui":
        fail("nativeRunner source repository is not the governed UI repository")
    if not isinstance(source["actor"], str) or not GITHUB_LOGIN_RE.fullmatch(source["actor"]):
        fail("nativeRunner source actor is invalid")
    if source["actor"] != "github-actions[bot]":
        fail("nativeRunner source actor is not the governed producer relay")
    if (
        not isinstance(source["triggeringActor"], str)
        or not GITHUB_LOGIN_RE.fullmatch(source["triggeringActor"])
        or source["triggeringActor"] != source["actor"]
        or source["rerunPolicy"] != RERUN_POLICY
    ):
        fail("nativeRunner source violates the same-actor-only rerun policy")
    if not isinstance(source["sha"], str) or not COMMIT_RE.fullmatch(source["sha"]):
        fail("nativeRunner source sha is invalid")
    for key in ("runId", "runAttempt"):
        require_positive_integer_text(source[key], f"nativeRunner source {key}")
    if (
        not isinstance(source["workflow"], str)
        or not WORKFLOW_RE.fullmatch(source["workflow"])
    ):
        fail("nativeRunner source workflow is invalid")
    if not isinstance(source["ref"], str) or not FULL_REF_RE.fullmatch(source["ref"]):
        fail("nativeRunner source ref is invalid")
    if source["ref"] != "refs/heads/main":
        fail("nativeRunner source ref must be the governed main branch")
    expected_workflow = (
        ".github/workflows/windows-native-evidence-capture.yml"
        if platform == "windows"
        else ".github/workflows/linux-native-lifecycle-evidence.yml"
    )
    if source["workflow"] != expected_workflow:
        fail("nativeRunner source workflow is not the governed platform lane")

    artifact_keys = {
        "artifactFileName",
        "sha256",
        "sizeBytes",
        "sourceCommit",
        "version",
    }
    candidate_keys = set(artifact_keys)
    previous_keys = (artifact_keys - {"sourceCommit"}) | {
        "artifactUrl",
        "generationId",
        "manifestSha256",
        "manifestUrl",
        "releasedAt",
    }
    if platform == "windows":
        candidate_keys.add("payload")
        previous_keys.add("payload")
    candidate = exact_keys(receipt["candidate"], candidate_keys, "candidate receipt binding")
    previous = exact_keys(
        receipt["nMinusOne"],
        previous_keys,
        "N-1 receipt binding",
    )
    for label, value in (("candidate", candidate), ("N-1", previous)):
        require_text(value["artifactFileName"], f"{label} artifactFileName")
        require_text(value["version"], f"{label} version")
        require_sha256(value["sha256"], f"{label} sha256")
        require_positive_integer(
            value["sizeBytes"], f"{label} sizeBytes", maximum=MAX_ARTIFACT_BYTES
        )
    if (
        not isinstance(candidate["sourceCommit"], str)
        or not COMMIT_RE.fullmatch(candidate["sourceCommit"])
    ):
        fail("candidate sourceCommit must be an exact lowercase Git commit")
    if candidate["sourceCommit"] != source["sha"]:
        fail("candidate sourceCommit differs from the authenticated native source")
    if candidate["version"] == previous["version"] or candidate["sha256"] == previous["sha256"]:
        fail("candidate and N-1 artifacts must be distinct")
    generation = require_text(previous["generationId"], "N-1 generationId")
    parse_timestamp(previous["releasedAt"], "N-1 releasedAt")
    require_sha256(previous["manifestSha256"], "N-1 manifestSha256")
    validate_immutable_url(
        previous["artifactUrl"],
        label="receipt N-1 artifactUrl",
        generation_id=generation,
        expected_file_name=previous["artifactFileName"],
    )
    validate_immutable_url(
        previous["manifestUrl"],
        label="receipt N-1 manifestUrl",
        generation_id=generation,
    )
    if platform == "windows":
        candidate_payload = exact_keys(
            candidate["payload"],
            {"fileName", "sha256", "sizeBytes"},
            "candidate payload receipt binding",
        )
        previous_payload = exact_keys(
            previous["payload"],
            {"fileName", "sha256", "sizeBytes", "url"},
            "N-1 payload receipt binding",
        )
        for label, payload in (
            ("candidate", candidate_payload),
            ("N-1", previous_payload),
        ):
            file_name = require_text(payload["fileName"], f"{label} payload fileName")
            if "/" in file_name:
                fail(f"{label} payload fileName must be a basename")
            require_sha256(payload["sha256"], f"{label} payload sha256")
            require_positive_integer(
                payload["sizeBytes"],
                f"{label} payload sizeBytes",
                maximum=MAX_ARTIFACT_BYTES,
            )
        validate_immutable_url(
            previous_payload["url"],
            label="receipt N-1 payload URL",
            generation_id=generation,
            expected_file_name=previous_payload["fileName"],
        )

    phases = receipt["phases"]
    if not isinstance(phases, list) or len(phases) != len(PHASES):
        fail("lifecycle receipt must contain the exact six phases")
    last_completed: datetime | None = None
    for expected, raw_phase in zip(PHASES, phases, strict=True):
        phase = exact_keys(
            raw_phase, {"completedAt", "details", "name", "startedAt", "status"}, f"{expected} phase"
        )
        if phase["name"] != expected or phase["status"] != "passed":
            fail(f"{expected} phase did not pass in canonical order")
        started = parse_timestamp(phase["startedAt"], f"{expected} startedAt")
        completed = parse_timestamp(phase["completedAt"], f"{expected} completedAt")
        if started > completed or (last_completed is not None and started < last_completed):
            fail(f"{expected} phase timestamps are not monotonic")
        last_completed = completed
        if not isinstance(phase["details"], dict):
            fail(f"{expected} phase details must be an object")
    required_detail_truths = {
        "artifact_authentication": {
            "candidateDigestVerified",
            "nMinusOneDigestVerified",
            "nativePackageAuthorityVerified",
        },
        "clean_install_n_minus_one": {"installed", "launcherPresent"},
        "core_workflow_n_minus_one": {"mouseFirstJourneyPassed", "startupSmokePassed"},
        "update_to_candidate": {
            "candidateBytesInstalled",
            "installedVersionChanged",
            "statePreserved",
        },
        "core_workflow_candidate": {"mouseFirstJourneyPassed", "startupSmokePassed"},
        "normal_uninstall": {"launcherAbsent", "packageAbsent", "uninstallerInvoked"},
    }
    for phase in phases:
        for key in required_detail_truths[phase["name"]]:
            if phase["details"].get(key) is not True:
                fail(f"{phase['name']} did not prove {key}")

    state = exact_keys(
        receipt["statePreservation"],
        {
            "preservedAfterUninstall",
            "preservedAfterUpdate",
            "sentinelSha256AfterUninstall",
            "sentinelSha256AfterUpdate",
            "sentinelSha256BeforeUpdate",
        },
        "statePreservation",
    )
    sentinel_digests = [
        require_sha256(state[key], f"statePreservation {key}")
        for key in (
            "sentinelSha256BeforeUpdate",
            "sentinelSha256AfterUpdate",
            "sentinelSha256AfterUninstall",
        )
    ]
    if (
        state["preservedAfterUpdate"] is not True
        or state["preservedAfterUninstall"] is not True
        or len(set(sentinel_digests)) != 1
    ):
        fail("user state was not preserved through update and uninstall")

    core = exact_keys(receipt["coreWorkflow"], {"candidate", "nMinusOne"}, "coreWorkflow")
    core_file_bindings: list[dict[str, Any]] = []
    for release_key in ("nMinusOne", "candidate"):
        artifact = previous if release_key == "nMinusOne" else candidate
        run = exact_keys(
            core[release_key],
            {"mouseFirstReceipt", "startupReceipt"},
            f"coreWorkflow {release_key}",
        )
        core_file_bindings.append(
            require_pass_receipt_file(
                evidence_root,
                run["startupReceipt"],
                f"{release_key} startup receipt",
                expected_version=artifact["version"],
                expected_artifact_sha256=artifact["sha256"],
                platform=platform,
                rid=rid,
                receipt_kind="startup",
            )
        )
        core_file_bindings.append(
            require_pass_receipt_file(
                evidence_root,
                run["mouseFirstReceipt"],
                f"{release_key} mouse-first receipt",
                expected_version=artifact["version"],
                expected_artifact_sha256=artifact["sha256"],
                platform=platform,
                rid=rid,
                receipt_kind="mouse-first",
            )
        )

    package_authority = receipt["packageAuthority"]
    authority_file_bindings: list[dict[str, Any]] = []
    manifest_row: dict[str, Any]
    if platform == "windows":
        authority = exact_keys(
            package_authority,
            {
                "candidate",
                "expectedSignerCertificateSha256",
                "expectedSignerSpkiSha256",
                "manifestReceipt",
                "mode",
                "nMinusOne",
            },
            "Windows packageAuthority",
        )
        if authority["mode"] != "authenticode":
            fail("Windows packageAuthority mode must be authenticode")
        certificate_pin = require_sha256(
            authority["expectedSignerCertificateSha256"],
            "Windows expected signer certificate SHA-256",
        )
        spki_pin = require_sha256(
            authority["expectedSignerSpkiSha256"],
            "Windows expected signer SPKI SHA-256",
        )
        candidate_authority = exact_keys(
            authority["candidate"],
            {"authenticodeReceipt", "signingReceipt"},
            "candidate packageAuthority",
        )
        previous_authority = exact_keys(
            authority["nMinusOne"],
            {"authenticodeReceipt"},
            "N-1 packageAuthority",
        )
        manifest_row = file_binding(
            evidence_root,
            authority["manifestReceipt"],
            "N-1 release manifest",
        )
        if manifest_row["sha256"] != previous["manifestSha256"]:
            fail("N-1 release manifest evidence differs from lifecycle authority")
        authority_file_bindings.append(manifest_row)
        for label, binding, artifact in (
            (
                "candidate Authenticode receipt",
                candidate_authority["authenticodeReceipt"],
                candidate,
            ),
            (
                "N-1 Authenticode receipt",
                previous_authority["authenticodeReceipt"],
                previous,
            ),
        ):
            row = file_binding(evidence_root, binding, label)
            authority_file_bindings.append(row)
            _, _, raw = stable_regular_bytes(
                evidence_root.joinpath(*PurePosixPath(row["path"]).parts),
                label,
                MAX_EVIDENCE_BYTES,
            )
            try:
                auth = json.loads(
                    raw.decode("utf-8-sig"),
                    object_pairs_hook=duplicate_rejecting_object,
                )
            except (UnicodeError, json.JSONDecodeError) as exc:
                fail(f"{label} is invalid JSON: {exc}")
            if not isinstance(auth, dict) or (
                auth.get("contractName") != "chummer6-ui.windows-authenticode-verification"
                or type(auth.get("contractVersion")) is not int
                or auth.get("contractVersion") != 1
                or auth.get("status") != "verified"
            ):
                fail(f"{label} contract or status is invalid")
            auth_artifact = auth.get("artifact")
            policy = auth.get("policy")
            signer = auth.get("signer")
            if (
                not isinstance(auth_artifact, dict)
                or auth_artifact.get("fileName") != artifact["artifactFileName"]
                or auth_artifact.get("sha256") != artifact["sha256"]
                or auth_artifact.get("sizeBytes") != artifact["sizeBytes"]
                or not isinstance(policy, dict)
                or policy.get("signerCertificateSha256") != certificate_pin
                or policy.get("signerSpkiSha256") != spki_pin
                or not isinstance(signer, dict)
                or signer.get("certificateSha256") != certificate_pin
                or signer.get("spkiSha256") != spki_pin
                or auth.get("source") != source
            ):
                fail(f"{label} artifact or signer pins differ from lifecycle authority")
        signing_row = file_binding(
            evidence_root,
            candidate_authority["signingReceipt"],
            "candidate v2 signing receipt",
        )
        authority_file_bindings.append(signing_row)
        _, _, signing_raw = stable_regular_bytes(
            evidence_root.joinpath(*PurePosixPath(signing_row["path"]).parts),
            "candidate v2 signing receipt",
            MAX_EVIDENCE_BYTES,
        )
        try:
            signing_payload = json.loads(
                signing_raw.decode("utf-8-sig"),
                object_pairs_hook=duplicate_rejecting_object,
            )
        except (UnicodeError, json.JSONDecodeError) as exc:
            fail(f"candidate v2 signing receipt is invalid JSON: {exc}")
        validate_keylocker_signing_receipt(
            signing_payload,
            candidate=candidate,
            rid=rid,
            certificate_pin=certificate_pin,
            spki_pin=spki_pin,
        )
    else:
        authority = exact_keys(
            package_authority,
            {
                "candidate",
                "manifestReceipt",
                "manifestSha256",
                "mode",
                "nMinusOne",
            },
            "Linux packageAuthority",
        )
        if authority["mode"] != "debian-package-metadata-and-immutable-manifest":
            fail("Linux packageAuthority mode is invalid")
        if require_sha256(
            authority["manifestSha256"], "Linux packageAuthority manifestSha256"
        ) != previous["manifestSha256"]:
            fail("Linux package authority manifest differs from N-1 binding")
        manifest_row = file_binding(
            evidence_root,
            authority["manifestReceipt"],
            "N-1 release manifest",
        )
        if manifest_row["sha256"] != previous["manifestSha256"]:
            fail("N-1 release manifest evidence differs from lifecycle authority")
        authority_file_bindings.append(manifest_row)
        linux_package_keys = {"architecture", "packageName", "packageVersion"}
        candidate_package = exact_keys(
            authority["candidate"], linux_package_keys, "candidate Debian authority"
        )
        previous_package = exact_keys(
            authority["nMinusOne"], linux_package_keys, "N-1 Debian authority"
        )
        for label, package in (
            ("candidate", candidate_package),
            ("N-1", previous_package),
        ):
            require_text(package["packageName"], f"{label} Debian packageName")
            require_text(package["packageVersion"], f"{label} Debian packageVersion")
            if package["architecture"] != "amd64":
                fail(f"{label} Debian architecture does not match linux-x64")
        if (
            candidate_package["packageName"] != previous_package["packageName"]
            or candidate_package["packageVersion"] == previous_package["packageVersion"]
        ):
            fail("candidate and N-1 Debian package identities do not prove an update")

    validate_downloaded_n_minus_one_manifest(
        evidence_root.joinpath(*PurePosixPath(manifest_row["path"]).parts),
        canonical_json(receipt_n_minus_one_binding(previous, platform, rid)),
        platform,
        rid,
    )

    uninstall = exact_keys(
        receipt["uninstall"],
        {"installRootRemoved", "launchersRemoved", "mode", "statusAfter"},
        "uninstall",
    )
    require_text(uninstall["mode"], "uninstall mode")
    if (
        uninstall["statusAfter"] != "not-installed"
        or uninstall["installRootRemoved"] is not True
        or uninstall["launchersRemoved"] is not True
    ):
        fail("normal uninstall did not remove the installed application")

    rows = receipt["evidenceFiles"]
    if not isinstance(rows, list) or len(rows) < 4:
        fail("lifecycle receipt evidenceFiles must contain at least four files")
    verified = [file_binding(evidence_root, row, f"evidenceFiles[{index}]") for index, row in enumerate(rows)]
    for binding in [*core_file_bindings, *authority_file_bindings]:
        if binding not in verified:
            fail("authoritative file binding is absent from evidenceFiles")
    paths = [row["path"] for row in verified]
    roles = [row["role"] for row in verified]
    if paths != sorted(paths) or len(paths) != len(set(paths)):
        fail("evidenceFiles paths must be unique and sorted")
    required_roles = {
        "candidate-core-mouse-first",
        "candidate-core-startup",
        "n-minus-one-core-mouse-first",
        "n-minus-one-core-startup",
        "n-minus-one-release-manifest",
    }
    if not required_roles.issubset(set(roles)):
        fail("evidenceFiles is missing required core-workflow receipts")
    if platform == "windows":
        required_roles.update(
            {
                "candidate-authenticode",
                "candidate-v2-signing-receipt",
                "n-minus-one-authenticode",
            }
        )
        if not required_roles.issubset(set(roles)):
            fail("Windows evidenceFiles is missing package-authority receipts")
    return {
        "platform": platform,
        "receipt": receipt,
        "receiptSha256": digest,
        "receiptSizeBytes": receipt_size,
        "rid": rid,
    }


def require_flagship_id(value: Any, label: str) -> str:
    if not isinstance(value, str) or not FLAGSHIP_ID_RE.fullmatch(value):
        fail(f"{label} must match the global flagship portable identifier contract")
    return value


def write_new_json(path: Path, payload: dict[str, Any], label: str) -> tuple[str, int]:
    absolute = Path(os.path.abspath(path))
    parent = absolute.parent
    current = Path(parent.anchor)
    reparse = int(getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
    for component in parent.parts[1:]:
        current /= component
        try:
            state = os.stat(current, follow_symlinks=False)
        except OSError as exc:
            fail(f"unable to inspect {label} parent: {exc}")
        if (
            not stat.S_ISDIR(state.st_mode)
            or stat.S_ISLNK(state.st_mode)
            or (
                reparse
                and int(getattr(state, "st_file_attributes", 0)) & reparse
            )
        ):
            fail(f"{label} parent must contain only real directories")
    data = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    flags = (
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_NOFOLLOW", 0))
    )
    try:
        descriptor = os.open(absolute, flags, 0o600)
    except OSError as exc:
        fail(f"{label} must be a new regular file: {exc}")
    try:
        offset = 0
        while offset < len(data):
            written = os.write(descriptor, data[offset:])
            if written < 1:
                fail(f"{label} write made no forward progress")
            offset += written
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    return stable_regular_file(absolute, label, MAX_EVIDENCE_BYTES)


def emit_flagship_adapter(
    *,
    receipt_path: Path,
    evidence_root: Path,
    candidate_root: Path,
    evidence_path: str,
    output_path: Path,
    candidate_id: str,
    generation_id: str,
    artifact_id: str,
    source_commit: str,
) -> dict[str, Any]:
    validated = validate_receipt(receipt_path, evidence_root)
    receipt = validated["receipt"]
    platform = validated["platform"]
    expected_artifact_id = FLAGSHIP_ARTIFACT_IDS[platform]
    expected_artifact_name = FLAGSHIP_ARTIFACT_NAMES[platform]
    if artifact_id != expected_artifact_id:
        fail(f"artifactId must be {expected_artifact_id} for {platform}")
    require_flagship_id(candidate_id, "candidateId")
    require_flagship_id(generation_id, "generationId")
    require_flagship_id(artifact_id, "artifactId")
    if not isinstance(source_commit, str) or not COMMIT_RE.fullmatch(source_commit):
        fail("sourceCommit must be an exact lowercase Git commit")
    candidate = receipt["candidate"]
    if source_commit != candidate["sourceCommit"]:
        fail("adapter sourceCommit differs from the validated lifecycle candidate")
    if candidate["artifactFileName"] != expected_artifact_name:
        fail("validated lifecycle artifact file name is not the flagship artifact")
    require_flagship_id(candidate["version"], "candidate releaseVersion")
    require_flagship_id(receipt["nMinusOne"]["version"], "N-1 releaseVersion")

    relative_evidence = safe_relative(evidence_path, "adapter lifecycle evidence path")
    expected_receipt_path = Path(
        os.path.abspath(
            candidate_root.joinpath(*PurePosixPath(relative_evidence).parts)
        )
    )
    if expected_receipt_path != Path(os.path.abspath(receipt_path)):
        fail("adapter evidence path does not resolve to the validated lifecycle receipt")
    evidence = {
        "path": relative_evidence,
        "sha256": validated["receiptSha256"],
        "sizeBytes": validated["receiptSizeBytes"],
    }
    runner = receipt["nativeRunner"]
    source = runner["source"]
    adapter = {
        "artifact": {
            "artifactId": artifact_id,
            "fileName": candidate["artifactFileName"],
            "sha256": candidate["sha256"],
            "sizeBytes": candidate["sizeBytes"],
        },
        "candidate": {
            "candidateId": candidate_id,
            "generationId": generation_id,
            "previousReleaseVersion": receipt["nMinusOne"]["version"],
            "releaseVersion": candidate["version"],
            "sourceCommit": source_commit,
        },
        "checks": {
            "cleanInstall": {
                "evidence": evidence,
                "mode": "clean",
                "status": "passed",
            },
            "coreWorkflow": {
                "evidence": evidence,
                "scenario": "desktop-startup-and-mouse-first",
                "status": "passed",
            },
            "nMinusOneUpdate": {
                "evidence": evidence,
                "fromReleaseVersion": receipt["nMinusOne"]["version"],
                "status": "passed",
                "toReleaseVersion": candidate["version"],
            },
        },
        "contractName": FLAGSHIP_ADAPTER_CONTRACTS[platform],
        "contractVersion": 1,
        "generatedAt": receipt["generatedAt"],
        "platform": platform,
        "rid": validated["rid"],
        "runner": {
            "actor": source["actor"],
            "arch": "x64",
            "os": runner["runnerOs"],
            "ref": source["ref"],
            "repository": source["repository"],
            "rerunPolicy": source["rerunPolicy"],
            "runAttempt": source["runAttempt"],
            "runId": source["runId"],
            "triggeringActor": source["triggeringActor"],
            "workflow": source["workflow"],
        },
        "status": "passed",
    }
    adapter_digest, adapter_size = write_new_json(
        output_path, adapter, "flagship native E2E adapter"
    )
    return {
        "adapter": adapter,
        "adapterSha256": adapter_digest,
        "adapterSizeBytes": adapter_size,
    }


def emit_binding(value: dict[str, Any], *, candidate: bool) -> None:
    mapping = {
        "artifact_file_name": value["artifactFileName"],
        "artifact_sha256": value["artifactSha256"],
        "artifact_size_bytes": value["artifactSizeBytes"],
        "version": value["version"],
    }
    if candidate:
        producer = value["producer"]
        mapping.update(
            {
                "artifact_member_path": value["artifactMemberPath"],
                "producer_artifact_id": producer["artifactId"],
                "producer_artifact_name": producer["artifactName"],
                "producer_artifact_zip_sha256": producer["artifactZipSha256"],
                "producer_run_attempt": producer["runAttempt"],
                "producer_run_id": producer["runId"],
            }
        )
        if "resolvedPath" in value:
            mapping["resolved_path"] = value["resolvedPath"]
    else:
        mapping.update(
            {
                "artifact_url": value["artifactUrl"],
                "generation_id": value["generationId"],
                "manifest_sha256": value["manifestSha256"],
                "manifest_url": value["manifestUrl"],
                "released_at": value["releasedAt"],
            }
        )
        if value["platform"] == "windows":
            mapping.update(
                {
                    "payload_file_name": value["payloadFileName"],
                    "payload_sha256": value["payloadSha256"],
                    "payload_size_bytes": value["payloadSizeBytes"],
                    "payload_url": value["payloadUrl"],
                }
            )
    for key in sorted(mapping):
        print(f"{key}={mapping[key]}")


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(description=__doc__)
    commands = root.add_subparsers(dest="command", required=True)
    previous = commands.add_parser("validate-n-minus-one")
    previous.add_argument("--binding-json", required=True)
    previous.add_argument("--platform", required=True)
    previous.add_argument("--rid", required=True)
    live_predecessor = commands.add_parser(
        "validate-live-predecessor-authority"
    )
    live_predecessor.add_argument("--binding-json", required=True)
    live_predecessor.add_argument("--live-release-channel-json", required=True)
    live_predecessor.add_argument("--platform", required=True)
    live_predecessor.add_argument("--rid", required=True)
    live_predecessor.add_argument("--expected-n-minus-one-sha256")
    live_predecessor.add_argument("--expected-live-release-channel-sha256")
    live_predecessor.add_argument("--expected-selected-tuple-sha256")
    relay = commands.add_parser("validate-windows-relay-authority")
    relay.add_argument("--binding-json", required=True)
    relay.add_argument("--live-release-channel-json", required=True)
    relay.add_argument("--signer-certificate-sha256", required=True)
    relay.add_argument("--signer-spki-sha256", required=True)
    relay.add_argument("--expected-sha256")
    relay.add_argument("--expected-live-release-channel-sha256")
    relay.add_argument("--expected-selected-tuple-sha256")
    previous_manifest = commands.add_parser("validate-n-minus-one-manifest")
    previous_manifest.add_argument("--manifest", required=True, type=Path)
    previous_manifest.add_argument("--binding-json", required=True)
    previous_manifest.add_argument("--platform", required=True)
    previous_manifest.add_argument("--rid", required=True)
    candidate = commands.add_parser("validate-candidate")
    candidate.add_argument("--binding-json", required=True)
    candidate.add_argument("--platform", required=True)
    candidate.add_argument("--rid", required=True)
    candidate.add_argument("--candidate-root", type=Path)
    materialize = commands.add_parser("materialize-candidate")
    materialize.add_argument("--candidate-zip", required=True, type=Path)
    materialize.add_argument("--binding-json", required=True)
    materialize.add_argument("--platform", required=True)
    materialize.add_argument("--rid", required=True)
    materialize.add_argument("--output-root", required=True, type=Path)
    verify = commands.add_parser("verify-receipt")
    verify.add_argument("--receipt", required=True, type=Path)
    verify.add_argument("--evidence-root", required=True, type=Path)
    adapter = commands.add_parser("emit-flagship-adapter")
    adapter.add_argument("--receipt", required=True, type=Path)
    adapter.add_argument("--evidence-root", required=True, type=Path)
    adapter.add_argument("--candidate-root", required=True, type=Path)
    adapter.add_argument("--evidence-path", required=True)
    adapter.add_argument("--output", required=True, type=Path)
    adapter.add_argument("--candidate-id", required=True)
    adapter.add_argument("--generation-id", required=True)
    adapter.add_argument("--artifact-id", required=True)
    adapter.add_argument("--source-commit", required=True)
    return root


def main(argv: Iterable[str] | None = None) -> int:
    args = parser().parse_args(list(argv) if argv is not None else None)
    try:
        if args.command == "validate-n-minus-one":
            emit_binding(
                validate_n_minus_one(args.binding_json, args.platform, args.rid),
                candidate=False,
            )
        elif args.command == "validate-live-predecessor-authority":
            result = validate_live_predecessor_authority(
                args.binding_json,
                args.live_release_channel_json,
                args.platform,
                args.rid,
                expected_n_minus_one_sha256=args.expected_n_minus_one_sha256,
                expected_live_release_channel_sha256=(
                    args.expected_live_release_channel_sha256
                ),
                expected_selected_tuple_sha256=(
                    args.expected_selected_tuple_sha256
                ),
            )
            for key in sorted(result):
                snake = re.sub(r"(?<!^)(?=[A-Z])", "_", key).lower()
                print(f"{snake}={result[key]}")
        elif args.command == "validate-windows-relay-authority":
            result = validate_windows_relay_authority(
                args.binding_json,
                args.live_release_channel_json,
                args.signer_certificate_sha256,
                args.signer_spki_sha256,
                expected_sha256=args.expected_sha256,
                expected_live_release_channel_sha256=(
                    args.expected_live_release_channel_sha256
                ),
                expected_selected_tuple_sha256=(
                    args.expected_selected_tuple_sha256
                ),
            )
            for key in sorted(result):
                snake = re.sub(r"(?<!^)(?=[A-Z])", "_", key).lower()
                print(f"{snake}={result[key]}")
        elif args.command == "validate-n-minus-one-manifest":
            result = validate_downloaded_n_minus_one_manifest(
                args.manifest,
                args.binding_json,
                args.platform,
                args.rid,
            )
            print(f"manifest_sha256={result['manifestSha256']}")
            print(f"generation_id={result['generationId']}")
            print(f"version={result['version']}")
        elif args.command == "validate-candidate":
            emit_binding(
                validate_candidate(
                    args.binding_json, args.platform, args.rid, args.candidate_root
                ),
                candidate=True,
            )
        elif args.command == "materialize-candidate":
            emit_binding(
                materialize_candidate(
                    args.candidate_zip,
                    args.binding_json,
                    args.platform,
                    args.rid,
                    args.output_root,
                ),
                candidate=True,
            )
        elif args.command == "verify-receipt":
            result = validate_receipt(args.receipt, args.evidence_root)
            print(f"receipt_sha256={result['receiptSha256']}")
            print(f"platform={result['platform']}")
            print(f"rid={result['rid']}")
        else:
            result = emit_flagship_adapter(
                receipt_path=args.receipt,
                evidence_root=args.evidence_root,
                candidate_root=args.candidate_root,
                evidence_path=args.evidence_path,
                output_path=args.output,
                candidate_id=args.candidate_id,
                generation_id=args.generation_id,
                artifact_id=args.artifact_id,
                source_commit=args.source_commit,
            )
            print(f"adapter_sha256={result['adapterSha256']}")
            print(f"adapter_size_bytes={result['adapterSizeBytes']}")
    except ContractError as exc:
        print(f"desktop-native-lifecycle-evidence:error: {exc}", file=sys.stderr)
        return 1
    print(f"desktop-native-lifecycle-evidence:{args.command}:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
