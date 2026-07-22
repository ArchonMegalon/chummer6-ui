#!/usr/bin/env python3
"""Compose and verify a fail-closed Windows-only preview publication shelf.

The build lane intentionally produces Windows and Linux evidence.  This module
keeps that evidence distinct from publication authority: only an Authenticode-
signed Windows installer and its bound bootstrap payload may form the delta.
Every other public row and byte is copied from an authenticated incumbent
snapshot.  The resulting publication directory is a complete shelf, not a
partial upload overlay.

This helper never signs, approves, uploads, deploys, or publishes anything.
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
import unicodedata
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable
from urllib.parse import urlsplit


CONTRACT_NAME = "chummer6-ui.preview-nightly-windows-publication-scope"
APPROVAL_CONTRACT_NAME = "chummer6-ui.preview-nightly-windows-publication-approval"
CONTRACT_VERSION = 2
PROPOSAL_FILE_NAME = "PREVIEW_NIGHTLY_PUBLICATION_SCOPE.proposed.json"
FINAL_FILE_NAME = "PREVIEW_NIGHTLY_PUBLICATION_SCOPE.generated.json"
PUBLICATION_DIRECTORY = "publication"
CANONICAL_MANIFEST_NAME = "RELEASE_CHANNEL.generated.json"
COMPATIBILITY_MANIFEST_NAME = "releases.json"
SIGNING_RECEIPT_RELATIVE_PATH = "signing/signing-avalonia-win-x64.receipt.json"
AUTHENTICODE_VERIFICATION_CONTRACT_NAME = (
    "chummer6-ui.windows-authenticode-verification"
)
AUTHENTICODE_VERIFICATION_RELATIVE_PATH = (
    "proof/windows-native/authenticode/"
    "AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
)
AUTHENTICODE_VERIFIER_RELATIVE_PATH = "scripts/verify-windows-authenticode.ps1"
NATIVE_EVIDENCE_RELATIVE_PATH = "NATIVE_WINDOWS_EVIDENCE.generated.json"
NATIVE_EVIDENCE_CONTRACT_NAME = "chummer6-ui.preview-nightly-native-windows-evidence"
NATIVE_EVIDENCE_CONTRACT_VERSION = 1
NATIVE_FINALIZATION_RELATIVE_PATH = (
    "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
)
NATIVE_FINALIZATION_SOURCE_RELATIVE_PATH = (
    "proof/windows-native/WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
)
NATIVE_FINALIZATION_CONTRACT_NAME = (
    "chummer6-ui.preview-nightly-native-windows-finalization"
)
NATIVE_FINALIZATION_CONTRACT_VERSION = 2
NATIVE_CAPTURE_RELATIVE_PATH = (
    "proof/windows-native/WINDOWS_NATIVE_CAPTURE.generated.json"
)
NATIVE_CAPTURE_CONTRACT_NAME = "chummer6-ui.preview-nightly-native-windows-capture"
NATIVE_CAPTURE_CONTRACT_VERSION = 2
WINDOWS_VISUAL_PROOF_CONTRACT_NAME = "chummer6-ui.windows_installer_visual_proof"
WINDOWS_VISUAL_PROOF_CONTRACT_VERSION = 1
WINDOWS_VISUAL_PROOF_RELATIVE_PATH = (
    "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
)
NATIVE_CAPTURE_WORKFLOW = ".github/workflows/windows-native-evidence-capture.yml"
NATIVE_FINALIZATION_WORKFLOW = (
    ".github/workflows/windows-native-evidence-finalize.yml"
)
PUBLICATION_MANIFEST_RELATIVE_PATH = f"{PUBLICATION_DIRECTORY}/{CANONICAL_MANIFEST_NAME}"
PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH = (
    f"{PUBLICATION_DIRECTORY}/{COMPATIBILITY_MANIFEST_NAME}"
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
ACTOR_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?|github-actions\[bot\])$"
)
WINDOWS_RESERVED_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}
WINDOWS_INVALID_CHARACTERS = frozenset('<>:"\\|?*')
EXPECTED_BUILD_KEYS = {
    ("avalonia", "windows", "win-x64", "installer"),
    ("avalonia", "windows", "win-x64", "payload"),
    ("avalonia", "linux", "linux-x64", "installer"),
}
EXPECTED_DELTA_KEYS = {
    ("avalonia", "windows", "win-x64", "installer"),
    ("avalonia", "windows", "win-x64", "payload"),
}
TUPLE_KEYS = {
    "artifactRole",
    "consumerCommit",
    "fileName",
    "head",
    "manifestRowSha256",
    "path",
    "platform",
    "rid",
    "sha256",
    "sizeBytes",
    "sourceReceipt",
}
SOURCE_RECEIPT_KEYS = {"contractName", "contractVersion", "path", "sha256"}
PROPOSAL_KEYS = {
    "approvalIndependent",
    "authenticodeRequired",
    "authenticodeVerificationSha256",
    "buildEvidenceTuples",
    "contractName",
    "contractVersion",
    "deployAuthorized",
    "fullShelfCompatibilityManifestSha256",
    "fullShelfInventory",
    "fullShelfInventorySha256",
    "fullShelfManifestSha256",
    "incumbentSnapshot",
    "incumbentSnapshotSha256",
    "macosSoak",
    "nativeEvidenceComposite",
    "nativeEvidenceSha256",
    "nonPublishedEvidenceTuples",
    "postPublicationShelfTuples",
    "publicationDeltaTuples",
    "publicationEligible",
    "registryPrepare",
    "registryFinalizeEligible",
    "release",
    "retainedTuples",
    "scopeDecision",
    "scopeDecisionSha256",
    "signingReceipt",
    "signingReceiptSha256",
    "status",
    "uploadAuthorized",
    "visualApprovalSha256",
}
FINAL_KEYS = PROPOSAL_KEYS | {"approval"}
REGISTRY_PREPARE_CONTRACT_NAME = "chummer6-ui.registry-preview-prepare-binding"
REGISTRY_PREPARE_CONTRACT_VERSION = 1
REGISTRY_AUTHORITY_COMMIT = "01c08982348432cab71ae461e231ce9a42084911"
REGISTRY_PREPARE_OUTPUT_NAMES = (
    CANONICAL_MANIFEST_NAME,
    COMPATIBILITY_MANIFEST_NAME,
    "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json",
)
REGISTRY_PROJECTION_INPUTS = {
    "materializer": {
        "path": "scripts/materialize_preview_publication_delta.py",
        "sha256": "74c88878f2219d35bcae258a86a976162982cc4200779ee0312ef1d09202bb70",
        "sizeBytes": 202660,
    },
    "releaseChannelMaterializer": {
        "path": "scripts/materialize_public_release_channel.py",
        "sha256": "333cb21427e495314aab5f870af1d7130c588f444d023e9b89ce69f3e9d76027",
        "sizeBytes": 241522,
    },
    "schema": {
        "path": "contracts/preview-publication-delta-v1.schema.json",
        "sha256": "27af4db39bc9435864d6e038c36c225302c1d4e0d3792e59d554f36d529e8f79",
        "sizeBytes": 27754,
    },
    "verifier": {
        "path": "scripts/verify_public_release_channel.py",
        "sha256": "3488aa9688c066247a54df513a8e314963bc4b14f3c495aa30423205945c29f4",
        "sizeBytes": 383489,
    },
}
REGISTRY_INCUMBENT_LINEAGE_PATH = (
    "release-evidence/registry-incumbent-lineage.json"
)
REGISTRY_WINDOWS_LINEAGE_PATH = "release-evidence/windows-build.json"
REGISTRY_LINUX_LINEAGE_PATH = "release-evidence/linux-build.json"


class ScopeError(RuntimeError):
    """Raised when publication scope is incomplete, ambiguous, or changed."""


def fail(message: str) -> None:
    raise ScopeError(message)


def _pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains a duplicate key: {key}")
        result[key] = value
    return result


def read_regular_file(path: Path, label: str) -> tuple[bytes, os.stat_result]:
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY
            | getattr(os, "O_NOFOLLOW", 0)
            | getattr(os, "O_NONBLOCK", 0),
        )
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            fail(f"{label} must be one non-hardlinked regular file")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            raw = handle.read()
            after = os.fstat(handle.fileno())
    except OSError as exc:
        fail(f"could not read {label}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    identity = lambda value: (
        value.st_dev,
        value.st_ino,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
        value.st_nlink,
    )
    if identity(before) != identity(after) or len(raw) != before.st_size:
        fail(f"{label} changed while its exact bytes were held")
    return raw, before


def read_regular_bytes(path: Path, label: str) -> bytes:
    raw, _metadata = read_regular_file(path, label)
    return raw


def parse_json_bytes(raw: bytes, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=_pairs,
            parse_constant=lambda value: fail(f"{label} contains non-finite {value}"),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is not exact UTF-8 JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def read_json_bound(path: Path, label: str) -> tuple[dict[str, Any], str]:
    raw = read_regular_bytes(path, label)
    return parse_json_bytes(raw, label), hashlib.sha256(raw).hexdigest()


def read_json(path: Path, label: str) -> dict[str, Any]:
    payload, _ = read_json_bound(path, label)
    return payload


def canonical_bytes(value: object) -> bytes:
    return json.dumps(
        value, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def canonical_sha256(value: object) -> str:
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def write_new_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    try:
        descriptor = os.open(
            path,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
            handle.write(json.dumps(payload, indent=2, sort_keys=True) + "\n")
            handle.flush()
            os.fsync(handle.fileno())
    except OSError as exc:
        fail(f"could not create {path}: {exc}")


def file_digest_size(path: Path) -> tuple[str, int]:
    descriptor = -1
    digest = hashlib.sha256()
    total = 0
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY
            | getattr(os, "O_NOFOLLOW", 0)
            | getattr(os, "O_NONBLOCK", 0),
        )
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            fail(f"publication input is not one non-hardlinked regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            while chunk := handle.read(1024 * 1024):
                digest.update(chunk)
                total += len(chunk)
            after = os.fstat(handle.fileno())
    except OSError as exc:
        fail(f"could not hash publication input {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    stable_fields = lambda value: (
        value.st_dev,
        value.st_ino,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
        value.st_nlink,
    )
    if stable_fields(before) != stable_fields(after) or total != before.st_size:
        fail(f"publication input changed while hashed: {path}")
    return digest.hexdigest(), total


def sha256_file(path: Path) -> str:
    return file_digest_size(path)[0]


def exact_directory(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.is_symlink() or not path.is_dir():
        fail(f"{label} must be an absolute existing non-symlink directory")
    return path.resolve(strict=True)


def exact_file(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        fail(f"{label} must be an absolute existing non-symlink regular file")
    return path.resolve(strict=True)


def fresh_absolute_path(path: Path, label: str) -> Path:
    if not path.is_absolute() or path.exists() or path.is_symlink():
        fail(f"{label} must be an absolute fresh path")
    parent = exact_directory(path.parent, f"{label} parent")
    return parent / path.name


def paths_overlap(first: Path, second: Path) -> bool:
    return first == second or first in second.parents or second in first.parents


def require_disjoint_paths(paths: Iterable[tuple[str, Path]]) -> None:
    resolved = list(paths)
    for index, (first_label, first) in enumerate(resolved):
        for second_label, second in resolved[index + 1 :]:
            if paths_overlap(first, second):
                fail(
                    f"{first_label} and {second_label} must not be equal or "
                    "ancestor/descendant paths"
                )


def require_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or SHA256_RE.fullmatch(value) is None:
        fail(f"{label} must be an exact lowercase SHA-256")
    return value


def require_commit(value: object, label: str) -> str:
    if not isinstance(value, str) or COMMIT_RE.fullmatch(value) is None:
        fail(f"{label} must be an exact lowercase commit SHA")
    return value


def require_actor(value: object, label: str) -> str:
    if not isinstance(value, str) or ACTOR_RE.fullmatch(value) is None:
        fail(f"{label} must be an exact GitHub login")
    return value


def require_positive_size(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        fail(f"{label} must be a positive integer")
    return value


def portable_name(value: object, label: str) -> str:
    if not isinstance(value, str) or not value or Path(value).name != value:
        fail(f"{label} must be an exact portable basename")
    return value


def _exact_text(value: object, label: str) -> str:
    if (
        not isinstance(value, str)
        or not value
        or value != value.strip()
    ):
        fail(f"{label} must be exact nonempty text")
    return value


def _digest_alias(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact SHA-256 string")
    digest = value.removeprefix("sha256:")
    return require_sha256(digest, label)


def _resolve_alias(
    payload: dict[str, Any],
    aliases: tuple[str, ...],
    label: str,
    normalizer: Any,
    *,
    required: bool,
) -> Any:
    present = [(key, payload[key]) for key in aliases if key in payload]
    if not present:
        if required:
            fail(f"{label} is missing every supported alias: {', '.join(aliases)}")
        return None
    normalized = [
        (key, normalizer(value, f"{label}.{key}")) for key, value in present
    ]
    expected = normalized[0][1]
    if any(value != expected or type(value) is not type(expected) for _key, value in normalized[1:]):
        fail(f"{label} aliases conflict: {', '.join(key for key, _value in present)}")
    return expected


def _optional_alias_normalizer(normalizer: Any) -> Any:
    def normalize(value: object, label: str) -> Any:
        if value is None:
            return None
        return normalizer(value, label)

    return normalize


def _require_expected_aliases(
    payload: dict[str, Any],
    aliases: tuple[str, ...],
    expected: Any,
    label: str,
    normalizer: Any,
    *,
    required: bool,
) -> None:
    if not any(alias in payload for alias in aliases) and not required:
        return
    actual = _resolve_alias(
        payload, aliases, label, normalizer, required=required
    )
    if actual != expected or type(actual) is not type(expected):
        fail(f"{label} differs from the canonical Registry identity")


def manifest_identity(manifest: dict[str, Any], label: str) -> tuple[str, str]:
    contract = _resolve_alias(
        manifest,
        ("contractName", "contract_name"),
        f"{label} Registry contract",
        _exact_text,
        required=True,
    )
    if contract != "Chummer.Hub.Registry.Contracts":
        fail(f"{label} has the wrong Registry contract")
    channel = _resolve_alias(
        manifest,
        ("channelId", "channel"),
        f"{label} channel",
        _exact_text,
        required=True,
    )
    version = _resolve_alias(
        manifest,
        ("version", "releaseVersion"),
        f"{label} version",
        _exact_text,
        required=True,
    )
    _resolve_alias(
        manifest,
        ("generatedAt", "generated_at"),
        f"{label} generated timestamp",
        _exact_text,
        required=False,
    )
    if channel != "preview":
        fail(f"{label} must identify an exact preview release")
    return version, channel


def manifest_rows(manifest: dict[str, Any], label: str) -> list[dict[str, Any]]:
    raw = manifest.get("artifacts")
    if not isinstance(raw, list) or not raw:
        fail(f"{label} must contain artifact rows")
    rows: list[dict[str, Any]] = []
    for row in raw:
        if not isinstance(row, dict):
            fail(f"{label} contains a non-object artifact row")
        rows.append(row)
    return rows


def source_receipt(path: str, manifest: dict[str, Any], digest: str) -> dict[str, Any]:
    version = manifest.get("schemaVersion")
    if isinstance(version, bool) or not isinstance(version, int) or version < 1:
        fail("release manifest schemaVersion must be a positive integer")
    return {
        "contractName": manifest.get("contractName") or manifest.get("contract_name"),
        "contractVersion": version,
        "path": path,
        "sha256": digest,
    }


def artifact_tuple_key(row: dict[str, Any], role: str) -> tuple[str, str, str, str]:
    values = (
        row.get("head") or row.get("headId"),
        row.get("platform") or row.get("platformId"),
        row.get("rid"),
        role,
    )
    if any(not isinstance(value, str) or not value for value in values):
        fail("release artifact has no exact head/platform/rid identity")
    normalized = tuple(value.strip().lower() for value in values)
    if tuple(values) != normalized:
        fail("release artifact tuple identities must already be lowercase and trimmed")
    return normalized  # type: ignore[return-value]


def build_bindings(
    manifest: dict[str, Any],
    *,
    receipt: dict[str, Any],
    files_dir: Path,
    consumer_commit: str,
    label: str,
) -> tuple[list[dict[str, Any]], dict[tuple[str, str, str, str], dict[str, Any]]]:
    require_commit(consumer_commit, f"{label} consumer commit")
    if set(receipt) != SOURCE_RECEIPT_KEYS:
        fail(f"{label} source receipt has missing or extra fields")
    require_sha256(receipt.get("sha256"), f"{label} source receipt sha256")
    rows = manifest_rows(manifest, label)
    bindings: list[dict[str, Any]] = []
    by_key: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    names: set[str] = set()
    digests: set[str] = set()
    for row in rows:
        if str(row.get("kind") or "").strip().lower() != "installer":
            fail(f"{label} contains a non-installer public row")
        row_sha = canonical_sha256(row)
        descriptors = [
            (
                "installer",
                portable_name(row.get("fileName"), f"{label} fileName"),
                require_sha256(row.get("sha256"), f"{label} artifact sha256"),
                require_positive_size(row.get("sizeBytes"), f"{label} artifact sizeBytes"),
            )
        ]
        payload_name = row.get("payloadFileName")
        if payload_name is not None:
            descriptors.append(
                (
                    "payload",
                    portable_name(payload_name, f"{label} payloadFileName"),
                    require_sha256(row.get("payloadSha256"), f"{label} payloadSha256"),
                    require_positive_size(
                        row.get("payloadSizeBytes"), f"{label} payloadSizeBytes"
                    ),
                )
            )
        for role, name, digest, size in descriptors:
            path = files_dir / name
            if path.is_symlink() or not path.is_file():
                fail(f"{label} bytes are missing: {name}")
            actual_digest, actual_size = file_digest_size(path)
            if actual_digest != digest or actual_size != size:
                fail(f"{label} bytes differ from the manifest: {name}")
            key = artifact_tuple_key(row, role)
            if key in by_key:
                fail(f"{label} repeats tuple {'/'.join(key)}")
            if name in names:
                fail(f"{label} repeats artifact path {name}")
            if digest in digests:
                fail(f"{label} repeats artifact digest {digest}")
            names.add(name)
            digests.add(digest)
            binding = {
                "artifactRole": role,
                "consumerCommit": consumer_commit,
                "fileName": name,
                "head": key[0],
                "manifestRowSha256": row_sha,
                "path": f"files/{name}",
                "platform": key[1],
                "rid": key[2],
                "sha256": digest,
                "sizeBytes": size,
                "sourceReceipt": receipt,
            }
            bindings.append(binding)
            by_key[key] = binding
    bindings.sort(key=tuple_sort_key)
    return bindings, by_key


def tuple_sort_key(row: dict[str, Any]) -> tuple[str, str, str, str, str]:
    return (
        str(row.get("head")),
        str(row.get("platform")),
        str(row.get("rid")),
        str(row.get("artifactRole")),
        str(row.get("fileName")),
    )


def binding_key(row: dict[str, Any], label: str) -> tuple[str, str, str, str]:
    if not isinstance(row, dict) or set(row) != TUPLE_KEYS:
        fail(f"{label} tuple has missing or extra fields")
    source = row.get("sourceReceipt")
    if not isinstance(source, dict) or set(source) != SOURCE_RECEIPT_KEYS:
        fail(f"{label} tuple sourceReceipt has missing or extra fields")
    require_sha256(source.get("sha256"), f"{label} sourceReceipt sha256")
    require_sha256(row.get("sha256"), f"{label} sha256")
    require_sha256(row.get("manifestRowSha256"), f"{label} manifestRowSha256")
    require_positive_size(row.get("sizeBytes"), f"{label} sizeBytes")
    require_commit(row.get("consumerCommit"), f"{label} consumerCommit")
    portable_name(row.get("fileName"), f"{label} fileName")
    path = row.get("path")
    if (
        not isinstance(path, str)
        or PurePosixPath(path).is_absolute()
        or path != PurePosixPath(path).as_posix()
        or any(part in {"", ".", ".."} for part in PurePosixPath(path).parts)
        or "\\" in path
    ):
        fail(f"{label} path is not an exact portable relative path")
    key = tuple(
        row.get(name) for name in ("head", "platform", "rid", "artifactRole")
    )
    if any(not isinstance(value, str) or not value for value in key):
        fail(f"{label} tuple identity is incomplete")
    if tuple(value.strip().lower() for value in key) != key:
        fail(f"{label} tuple identity is not exact")
    return key  # type: ignore[return-value]


def validate_tuple_set(rows: object, label: str) -> list[dict[str, Any]]:
    if not isinstance(rows, list):
        fail(f"{label} must be a list")
    result: list[dict[str, Any]] = []
    keys: set[tuple[str, str, str, str]] = set()
    names: set[str] = set()
    digests: set[str] = set()
    for raw in rows:
        if not isinstance(raw, dict):
            fail(f"{label} contains a non-object tuple")
        key = binding_key(raw, label)
        name = raw["fileName"]
        digest = raw["sha256"]
        if key in keys or name in names or digest in digests:
            fail(f"{label} contains a duplicate tuple, path, or digest")
        keys.add(key)
        names.add(name)
        digests.add(digest)
        result.append(raw)
    if result != sorted(result, key=tuple_sort_key):
        fail(f"{label} must use deterministic tuple ordering")
    return result


def validate_signing_receipt_payload(
    receipt: dict[str, Any],
    windows: list[dict[str, Any]],
    *,
    expected_version: str,
) -> dict[str, Any]:
    if (
        receipt.get("contractName") != "chummer6-ui.desktop_artifact_signing"
        or type(receipt.get("contractVersion")) is not int
        or receipt.get("contractVersion") != 2
    ):
        fail("Windows signing receipt must use the v2 Authenticode contract")
    exact = {
        "platform": "windows",
        "app": "avalonia",
        "rid": "win-x64",
        "releaseChannel": "preview",
        "releaseVersion": expected_version,
        "signingStatus": "pass",
    }
    for key, value in exact.items():
        if receipt.get(key) != value or type(receipt.get(key)) is not type(value):
            fail(f"Windows signing receipt {key} is not exact")
    candidate_rows = receipt.get("candidateBindings")
    if not isinstance(candidate_rows, list) or len(candidate_rows) != 2:
        fail("Windows signing receipt must bind installer and payload candidate bytes")
    expected = {
        (row["artifactRole"], row["fileName"], row["sha256"], row["sizeBytes"])
        for row in windows
    }
    actual: set[tuple[str, str, str, int]] = set()
    for row in candidate_rows:
        if not isinstance(row, dict) or set(row) != {
            "artifactRole",
            "authenticodeStatus",
            "fileName",
            "sha256",
            "sizeBytes",
        }:
            fail("Windows signing receipt candidate binding is malformed")
        role = row.get("artifactRole")
        expected_authenticode = "pass" if role == "installer" else "not_applicable_payload"
        if row.get("authenticodeStatus") != expected_authenticode:
            fail("Windows signing receipt Authenticode status is not exact")
        actual.add(
            (
                str(role),
                portable_name(row.get("fileName"), "signing candidate fileName"),
                require_sha256(row.get("sha256"), "signing candidate sha256"),
                require_positive_size(row.get("sizeBytes"), "signing candidate sizeBytes"),
            )
        )
    if actual != expected:
        fail("Windows signing receipt candidate bytes differ from the Windows delta")
    artifacts = receipt.get("artifacts")
    if not isinstance(artifacts, list):
        fail("Windows signing receipt artifacts must be a list")
    installer = next(row for row in windows if row["artifactRole"] == "installer")
    matches = [
        row
        for row in artifacts
        if isinstance(row, dict)
        and row.get("fileName") == installer["fileName"]
        and row.get("sha256") == installer["sha256"]
        and row.get("signingStatus") == "pass"
    ]
    if len(matches) != 1:
        fail("Windows installer lacks one exact passing Authenticode artifact row")
    return receipt


def validate_signing_receipt(
    receipt_path: Path,
    windows: list[dict[str, Any]],
    *,
    expected_version: str,
) -> dict[str, Any]:
    receipt, _ = read_json_bound(receipt_path, "Windows signing receipt")
    return validate_signing_receipt_payload(
        receipt, windows, expected_version=expected_version
    )


def copy_regular_exact(source: Path, destination: Path) -> None:
    if destination.exists() or destination.is_symlink():
        fail(f"publication destination already exists: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    source_fd = -1
    target_fd = -1
    try:
        source_fd = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        source_metadata = os.fstat(source_fd)
    except OSError as exc:
        if source_fd >= 0:
            os.close(source_fd)
        fail(f"could not open publication input {source}: {exc}")
    if not stat.S_ISREG(source_metadata.st_mode) or source_metadata.st_nlink != 1:
        os.close(source_fd)
        fail(f"publication input is not one non-hardlinked regular file: {source}")
    source_size = source_metadata.st_size
    source_mode = stat.S_IMODE(source_metadata.st_mode)
    if source_mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
        os.close(source_fd)
        fail(f"publication input has unsafe permission bits: {source}")
    source_digest = hashlib.sha256()
    try:
        target_fd = os.open(
            destination,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        with (
            os.fdopen(source_fd, "rb", closefd=True) as input_handle,
            os.fdopen(target_fd, "wb", closefd=True) as output_handle,
        ):
            source_fd = -1
            target_fd = -1
            while chunk := input_handle.read(1024 * 1024):
                source_digest.update(chunk)
                output_handle.write(chunk)
            source_after = os.fstat(input_handle.fileno())
            os.fchmod(output_handle.fileno(), source_mode)
            output_handle.flush()
            os.fsync(output_handle.fileno())
    except OSError as exc:
        destination.unlink(missing_ok=True)
        fail(f"could not copy publication byte {source}: {exc}")
    finally:
        if source_fd >= 0:
            os.close(source_fd)
        if target_fd >= 0:
            os.close(target_fd)
    stable_fields = lambda value: (
        value.st_dev,
        value.st_ino,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
        value.st_nlink,
    )
    if stable_fields(source_metadata) != stable_fields(source_after):
        destination.unlink(missing_ok=True)
        fail(f"publication input changed while copied: {source.name}")
    if (
        file_digest_size(destination)
        != (source_digest.hexdigest(), source_size)
    ):
        destination.unlink(missing_ok=True)
        fail(f"publication byte changed while copied: {source.name}")


def portable_relative_path(relative: object, label: str) -> str:
    if not isinstance(relative, str):
        fail(f"{label} must be a string")
    token = PurePosixPath(relative)
    if (
        not relative
        or token.is_absolute()
        or relative != token.as_posix()
        or any(part in {"", ".", ".."} for part in token.parts)
        or "\\" in relative
    ):
        fail(f"{label} is not an exact portable relative path: {relative!r}")
    for part in token.parts:
        if unicodedata.normalize("NFC", part) != part:
            fail(f"{label} is not NFC-normalized: {relative!r}")
        if (
            part.endswith((" ", "."))
            or any(ord(character) < 32 for character in part)
            or any(character in WINDOWS_INVALID_CHARACTERS for character in part)
            or part.split(".", 1)[0].upper() in WINDOWS_RESERVED_NAMES
        ):
            fail(f"{label} is Windows-invalid: {relative!r}")
    return relative


def file_inventory(root: Path) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    portable_entries: dict[str, str] = {}
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        directories.sort()
        files.sort()
        for name in directories:
            path = current_path / name
            metadata = os.stat(path, follow_symlinks=False)
            relative = portable_relative_path(
                path.relative_to(root).as_posix(), "publication shelf directory"
            )
            collision_key = relative.casefold()
            if collision_key in portable_entries:
                fail(
                    "publication shelf contains a Windows case-colliding path: "
                    f"{portable_entries[collision_key]!r} and {relative!r}"
                )
            portable_entries[collision_key] = relative
            if not stat.S_ISDIR(metadata.st_mode):
                fail("publication shelf contains a directory symlink or special entry")
            if stat.S_IMODE(metadata.st_mode) & (
                stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX
            ):
                fail("publication shelf directory contains unsafe permission bits")
        for name in files:
            path = current_path / name
            relative = portable_relative_path(
                path.relative_to(root).as_posix(), "publication shelf file"
            )
            collision_key = relative.casefold()
            if collision_key in portable_entries:
                fail(
                    "publication shelf contains a Windows case-colliding path: "
                    f"{portable_entries[collision_key]!r} and {relative!r}"
                )
            portable_entries[collision_key] = relative
            raw, metadata = read_regular_file(path, "publication shelf file")
            mode = stat.S_IMODE(metadata.st_mode)
            if mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
                fail("publication shelf contains unsafe permission bits")
            result.append(
                {
                    "mode": mode,
                    "path": relative,
                    "sha256": hashlib.sha256(raw).hexdigest(),
                    "sizeBytes": metadata.st_size,
                }
            )
    return sorted(result, key=lambda row: row["path"])


def copy_tree_exact(source: Path, destination: Path) -> list[dict[str, Any]]:
    source = exact_directory(source, "incumbent full shelf")
    destination = fresh_absolute_path(
        destination, "incumbent full-shelf snapshot"
    )
    require_disjoint_paths(
        (
            ("incumbent full shelf", source),
            ("incumbent full-shelf snapshot", destination),
        )
    )
    before = file_inventory(source)
    root_mode = stat.S_IMODE(source.stat().st_mode)
    if root_mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
        fail("incumbent full shelf root contains unsafe permission bits")
    destination.mkdir(parents=True, mode=root_mode)
    os.chmod(destination, root_mode, follow_symlinks=False)
    try:
        for current, directories, files in os.walk(source, topdown=True, followlinks=False):
            current_path = Path(current)
            target_root = destination / current_path.relative_to(source)
            for name in sorted(directories):
                source_directory = current_path / name
                target_directory = target_root / name
                mode = stat.S_IMODE(
                    os.stat(source_directory, follow_symlinks=False).st_mode
                )
                if mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
                    fail("incumbent full shelf directory contains unsafe permission bits")
                target_directory.mkdir(mode=mode)
                os.chmod(target_directory, mode, follow_symlinks=False)
            for name in sorted(files):
                copy_regular_exact(current_path / name, target_root / name)
        if file_inventory(source) != before:
            fail("incumbent full shelf changed while snapshotted")
        after = file_inventory(destination)
        if after != before:
            fail("incumbent full-shelf snapshot changed bytes or permission modes")
        return after
    except Exception:
        shutil.rmtree(destination, ignore_errors=True)
        raise


def validate_inventory(value: object, label: str) -> list[dict[str, Any]]:
    if not isinstance(value, list) or not value:
        fail(f"{label} must be a non-empty exact file inventory")
    rows: list[dict[str, Any]] = []
    seen: set[str] = set()
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != {
            "mode",
            "path",
            "sha256",
            "sizeBytes",
        }:
            fail(f"{label} row has missing or extra fields")
        path = portable_relative_path(raw.get("path"), f"{label} path")
        collision_key = path.casefold()
        if collision_key in seen:
            fail(f"{label} repeats or case-collides at {path!r}")
        seen.add(collision_key)
        mode = raw.get("mode")
        if (
            isinstance(mode, bool)
            or not isinstance(mode, int)
            or mode < 0
            or mode > 0o777
        ):
            fail(f"{label} mode is invalid for {path!r}")
        size = raw.get("sizeBytes")
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            fail(f"{label} size is invalid for {path!r}")
        rows.append(
            {
                "mode": mode,
                "path": path,
                "sha256": require_sha256(raw.get("sha256"), f"{label} sha256"),
                "sizeBytes": size,
            }
        )
    if rows != sorted(rows, key=lambda row: row["path"]):
        fail(f"{label} rows are not sorted canonically")
    return rows


def _manifest_row_by_sha(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for row in manifest_rows(manifest, "release manifest"):
        digest = canonical_sha256(row)
        if digest in result:
            fail("release manifest repeats an exact artifact row")
        result[digest] = row
    return result


def _downloads_by_name(manifest: dict[str, Any], label: str) -> dict[str, dict[str, Any]]:
    rows = manifest.get("downloads")
    if not isinstance(rows, list) or not rows:
        fail(f"{label} has no downloads")
    result: dict[str, dict[str, Any]] = {}
    for row in rows:
        if not isinstance(row, dict):
            fail(f"{label} contains a non-object download")
        name = _resolve_alias(
            row,
            ("fileName", "name"),
            f"{label} file name",
            portable_name,
            required=True,
        )
        if name in result:
            fail(f"{label} repeats {name}")
        result[name] = row
    return result


def _registry_format(file_name: str) -> str:
    lowered = file_name.lower()
    for suffix in (".tar.gz", ".tar.zst", ".tar.xz"):
        if lowered.endswith(suffix):
            return suffix[1:]
    suffix = PurePosixPath(lowered).suffix.removeprefix(".")
    if not suffix:
        fail(f"Registry artifact file has no explicit format extension: {file_name}")
    return suffix


def _registry_arch(rid: str) -> str:
    if "-" not in rid:
        fail(f"Registry RID cannot derive an architecture: {rid}")
    return rid.rsplit("-", 1)[1]


def _registry_platform_semantics(
    canonical: dict[str, Any], label: str
) -> tuple[str, str]:
    platform_id = _resolve_alias(
        canonical,
        ("platformId",),
        f"{label} canonical platformId",
        _exact_text,
        required=False,
    )
    platform_value = _resolve_alias(
        canonical,
        ("platform",),
        f"{label} canonical platform",
        _exact_text,
        required=False,
    )
    platform_label = _resolve_alias(
        canonical,
        ("platformLabel",),
        f"{label} canonical platformLabel",
        _exact_text,
        required=False,
    )
    machine_from_platform = (
        platform_value
        if isinstance(platform_value, str)
        and re.fullmatch(r"[a-z0-9][a-z0-9_-]*", platform_value)
        else None
    )
    machine = platform_id or machine_from_platform
    if not isinstance(machine, str):
        fail(
            f"{label} canonical platform identity is ambiguous; migrate to exact "
            "platformId plus platformLabel fields"
        )
    if platform_id is not None and machine_from_platform is not None and platform_id != machine_from_platform:
        fail(f"{label} canonical platform and platformId conflict")
    if platform_label is None:
        fail(
            f"{label} canonical row lacks platformLabel; migrate older display-platform "
            "rows before Windows-only publication"
        )
    if platform_value is not None and platform_value not in {machine, platform_label}:
        fail(f"{label} canonical platform is neither its machine ID nor display label")
    return machine, platform_label


def _row_url_basename(url: str, label: str) -> str:
    name = PurePosixPath(urlsplit(url).path).name
    return portable_name(name, f"{label} URL basename")


def _compatibility_projection(
    canonical: dict[str, Any],
    download: dict[str, Any],
    *,
    label: str,
    version: str,
    channel: str,
) -> dict[str, Any]:
    artifact_id = _resolve_alias(
        canonical,
        ("artifactId", "id"),
        f"{label} canonical artifact ID",
        _exact_text,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("artifactId", "id"),
        artifact_id,
        f"{label} compatibility artifact ID",
        _exact_text,
        required=True,
    )
    file_name = _resolve_alias(
        canonical,
        ("fileName", "name"),
        f"{label} canonical file name",
        portable_name,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("fileName", "name"),
        file_name,
        f"{label} compatibility file name",
        portable_name,
        required=True,
    )
    download_url = _resolve_alias(
        canonical,
        ("downloadUrl", "url"),
        f"{label} canonical download URL",
        _exact_text,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("downloadUrl", "url"),
        download_url,
        f"{label} compatibility download URL",
        _exact_text,
        required=True,
    )
    if _row_url_basename(download_url, label) != file_name:
        fail(f"{label} canonical download URL basename differs from fileName")
    for alias in ("downloadUrl", "url"):
        if alias in download and _row_url_basename(download[alias], label) != file_name:
            fail(f"{label} compatibility {alias} basename differs from fileName")

    head = _resolve_alias(
        canonical,
        ("head", "headId"),
        f"{label} canonical head",
        _exact_text,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("head", "headId"),
        head,
        f"{label} compatibility head",
        _exact_text,
        required=True,
    )
    platform_id, platform_label = _registry_platform_semantics(canonical, label)
    if "platformId" not in download:
        fail(
            f"{label} compatibility row lacks machine platformId; migrate older "
            "display-platform rows before Windows-only publication"
        )
    _require_expected_aliases(
        download,
        ("platformId",),
        platform_id,
        f"{label} compatibility platformId",
        _exact_text,
        required=True,
    )
    compatibility_platform = _resolve_alias(
        download,
        ("platform",),
        f"{label} compatibility platform",
        _exact_text,
        required=True,
    )
    if compatibility_platform not in {platform_id, platform_label}:
        fail(
            f"{label} compatibility display platform is ambiguous; migrate it to "
            "the exact canonical platformLabel with platformId"
        )
    _require_expected_aliases(
        download,
        ("platformLabel",),
        platform_label,
        f"{label} compatibility platformLabel",
        _exact_text,
        required=False,
    )

    rid = _resolve_alias(
        canonical,
        ("rid",),
        f"{label} canonical RID",
        _exact_text,
        required=True,
    )
    arch = _registry_arch(rid)
    _require_expected_aliases(
        canonical,
        ("arch",),
        arch,
        f"{label} canonical architecture",
        _exact_text,
        required=False,
    )
    _require_expected_aliases(
        download,
        ("rid",),
        rid,
        f"{label} compatibility RID",
        _exact_text,
        required=False,
    )
    _require_expected_aliases(
        download,
        ("arch",),
        arch,
        f"{label} compatibility architecture",
        _exact_text,
        required=True,
    )

    kind = _resolve_alias(
        canonical,
        ("kind", "flavor"),
        f"{label} canonical kind",
        _exact_text,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("kind", "flavor"),
        kind,
        f"{label} compatibility kind/flavor",
        _exact_text,
        required=True,
    )
    file_format = _registry_format(file_name)
    _require_expected_aliases(
        canonical,
        ("format",),
        file_format,
        f"{label} canonical format",
        _exact_text,
        required=False,
    )
    _require_expected_aliases(
        download,
        ("format",),
        file_format,
        f"{label} compatibility format",
        _exact_text,
        required=True,
    )
    source_channel = _resolve_alias(
        canonical,
        ("channelId", "channel"),
        f"{label} canonical channel",
        _exact_text,
        required=True,
    )
    if source_channel != channel:
        fail(f"{label} canonical channel differs from the publication channel")
    _require_expected_aliases(
        download,
        ("channelId", "channel"),
        source_channel,
        f"{label} compatibility channel",
        _exact_text,
        required=True,
    )
    source_version = _resolve_alias(
        canonical,
        ("version", "releaseVersion"),
        f"{label} canonical version",
        _exact_text,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("version", "releaseVersion"),
        source_version,
        f"{label} compatibility version",
        _exact_text,
        required=True,
    )

    digest = _resolve_alias(
        canonical,
        ("sha256", "artifactSha256", "digest"),
        f"{label} canonical digest",
        _digest_alias,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("sha256", "artifactSha256", "digest"),
        digest,
        f"{label} compatibility digest",
        _digest_alias,
        required=True,
    )
    size = _resolve_alias(
        canonical,
        ("sizeBytes", "artifactSizeBytes", "size"),
        f"{label} canonical size",
        require_positive_size,
        required=True,
    )
    _require_expected_aliases(
        download,
        ("sizeBytes", "artifactSizeBytes", "size"),
        size,
        f"{label} compatibility size",
        require_positive_size,
        required=True,
    )

    optional_name = _optional_alias_normalizer(portable_name)
    optional_text = _optional_alias_normalizer(_exact_text)
    optional_digest = _optional_alias_normalizer(_digest_alias)
    optional_size = _optional_alias_normalizer(require_positive_size)
    payload_name = _resolve_alias(
        canonical,
        ("payloadFileName", "payloadName"),
        f"{label} canonical payload file name",
        optional_name,
        required=False,
    )
    payload_url = _resolve_alias(
        canonical,
        ("payloadDownloadUrl", "payloadUrl"),
        f"{label} canonical payload URL",
        optional_text,
        required=False,
    )
    payload_digest = _resolve_alias(
        canonical,
        ("payloadSha256", "payloadArtifactSha256", "payloadDigest"),
        f"{label} canonical payload digest",
        optional_digest,
        required=False,
    )
    payload_size = _resolve_alias(
        canonical,
        ("payloadSizeBytes", "payloadSize"),
        f"{label} canonical payload size",
        optional_size,
        required=False,
    )
    payload_values = (payload_name, payload_url, payload_digest, payload_size)
    if any(value is None for value in payload_values) and any(
        value is not None for value in payload_values
    ):
        fail(f"{label} canonical payload metadata is incomplete")
    if payload_name is not None and _row_url_basename(payload_url, label) != payload_name:
        fail(f"{label} canonical payload URL basename differs from payloadFileName")
    for aliases, expected, field_label, normalizer in (
        (("payloadFileName", "payloadName"), payload_name, "payload file name", optional_name),
        (("payloadDownloadUrl", "payloadUrl"), payload_url, "payload URL", optional_text),
        (("payloadSha256", "payloadArtifactSha256", "payloadDigest"), payload_digest, "payload digest", optional_digest),
        (("payloadSizeBytes", "payloadSize"), payload_size, "payload size", optional_size),
    ):
        _require_expected_aliases(
            download,
            aliases,
            expected,
            f"{label} compatibility {field_label}",
            normalizer,
            required=payload_name is not None,
        )
    if payload_name is not None:
        for alias in ("payloadDownloadUrl", "payloadUrl"):
            if alias in download and _row_url_basename(download[alias], label) != payload_name:
                fail(f"{label} compatibility {alias} basename differs from payloadFileName")

    for field in (
        "installerMode",
        "payloadAcquisitionMode",
        "installAccessClass",
        "compatibilityState",
        "compatibilityReason",
    ):
        if field in download and download[field] != canonical.get(field):
            fail(f"{label} compatibility {field} differs from the canonical artifact")

    projection = {
        "id": artifact_id,
        "platform": platform_label,
        "url": download_url,
        "sha256": digest,
        "sizeBytes": size,
        "format": file_format,
        "flavor": kind,
        "kind": kind,
        "head": head,
        "platformId": platform_id,
        "arch": arch,
        "rid": rid,
        "fileName": file_name,
        "channelId": channel,
        "channel": channel,
        "version": version,
        "releaseVersion": version,
        "compatibilityState": canonical.get("compatibilityState"),
        "compatibilityReason": canonical.get("compatibilityReason"),
        "installerMode": canonical.get("installerMode"),
        "payloadFileName": payload_name,
        "payloadDownloadUrl": payload_url,
        "payloadSha256": payload_digest,
        "payloadSizeBytes": payload_size,
        "installAccessClass": canonical.get("installAccessClass"),
        "artifactId": artifact_id,
    }
    if "payloadAcquisitionMode" in canonical:
        projection["payloadAcquisitionMode"] = canonical[
            "payloadAcquisitionMode"
        ]
    return projection


def _require_compatibility_bijection(
    canonical: dict[str, Any],
    download: dict[str, Any],
    label: str,
    *,
    version: str,
    channel: str,
) -> dict[str, Any]:
    return _compatibility_projection(
        canonical,
        download,
        label=label,
        version=version,
        channel=channel,
    )


def _copy_release_identity(target: dict[str, Any], source: dict[str, Any]) -> None:
    version, channel = manifest_identity(source, "release identity source")
    target["version"] = version
    target["releaseVersion"] = version
    target["channel"] = channel
    target["channelId"] = channel
    generated = _resolve_alias(
        source,
        ("generatedAt", "generated_at"),
        "release identity generated timestamp",
        _exact_text,
        required=False,
    )
    if generated is not None:
        target["generatedAt"] = generated
        target["generated_at"] = generated
    if "publishedAt" in source:
        target["publishedAt"] = _exact_text(
            source["publishedAt"], "release identity publishedAt"
        )


COMPATIBILITY_METADATA_KEYS = (
    "source",
    "status",
    "message",
    "rolloutState",
    "rolloutReason",
    "supportabilityState",
    "supportabilitySummary",
    "knownIssueSummary",
    "fixAvailabilitySummary",
    "releaseProof",
    "installAwareArtifactRegistry",
    "desktopSurfaceRefs",
    "artifactIdentityRegistry",
    "artifactPublicationBindings",
    "publicTrustMetrics",
    "registryBoundaryCoverage",
)


def _compatibility_manifest_projection(
    incumbent: dict[str, Any],
    identity_source: dict[str, Any],
    downloads: list[dict[str, Any]],
    required_platforms: list[str],
) -> dict[str, Any]:
    version, channel = manifest_identity(
        identity_source, "build compatibility manifest"
    )
    generated = _resolve_alias(
        identity_source,
        ("generatedAt", "generated_at"),
        "build compatibility generated timestamp",
        _exact_text,
        required=True,
    )
    published = _exact_text(
        identity_source.get("publishedAt", generated),
        "build compatibility publishedAt",
    )
    projection: dict[str, Any] = {
        "generated_at": generated,
        "generatedAt": generated,
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "version": version,
        "releaseVersion": version,
        "publicVersion": version,
        "channelId": channel,
        "channel": channel,
        "publishedAt": published,
    }
    for key in COMPATIBILITY_METADATA_KEYS:
        if key in incumbent:
            projection[key] = incumbent[key]
    incumbent_coverage = incumbent.get("desktopTupleCoverage")
    if incumbent_coverage is not None and not isinstance(incumbent_coverage, dict):
        fail("incumbent compatibility desktopTupleCoverage must be an object")
    coverage = dict(incumbent_coverage or {})
    coverage["requiredDesktopPlatforms"] = required_platforms
    projection["downloads"] = downloads
    projection["desktopTupleCoverage"] = coverage
    return projection


def registry_canonical_document_bytes(value: object) -> bytes:
    return canonical_bytes(value) + b"\n"


def registry_document_sha256(value: object) -> str:
    return hashlib.sha256(registry_canonical_document_bytes(value)).hexdigest()


def write_new_registry_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    data = registry_canonical_document_bytes(payload)
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_NOFOLLOW", 0),
            0o644,
        )
        os.fchmod(descriptor, 0o644)
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            descriptor = -1
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
    except OSError as exc:
        fail(f"could not create Registry composition input {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def _registry_inventory(root: Path) -> list[dict[str, Any]]:
    return [
        {
            "mode": f"{row['mode']:04o}",
            "path": row["path"],
            "sha256": row["sha256"],
            "sizeBytes": row["sizeBytes"],
        }
        for row in file_inventory(root)
    ]


def _registry_source_receipt(
    manifest: dict[str, Any],
    rows: list[dict[str, Any]],
    *,
    ui_commit: str,
    desktop_commit: str | None,
    release_version: str,
) -> dict[str, Any]:
    contract = _resolve_alias(
        manifest,
        ("contractName", "contract_name"),
        "Registry lineage source contract",
        _exact_text,
        required=True,
    )
    schema_version = manifest.get("schemaVersion")
    if (
        isinstance(schema_version, bool)
        or not isinstance(schema_version, int)
        or schema_version < 1
    ):
        fail("Registry lineage source schemaVersion must be a positive integer")
    receipt: dict[str, Any] = {
        "artifacts": rows,
        "consumerCommit": ui_commit,
        "uiCommit": ui_commit,
        "contractName": contract,
        "contract_name": contract,
        "contractVersion": schema_version,
        "schemaVersion": schema_version,
        "releaseVersion": release_version,
        "version": release_version,
        "status": "passed",
    }
    if desktop_commit is not None:
        receipt["desktopCommit"] = desktop_commit
        receipt["producerCommit"] = desktop_commit
    return receipt


def _registry_tuple(
    binding: dict[str, Any],
    *,
    source_receipt_path: str,
    source_receipt_sha256: str,
    source_contract: str,
    source_contract_version: int,
) -> dict[str, Any]:
    row = {key: binding[key] for key in TUPLE_KEYS}
    row["path"] = f"files/{binding['fileName']}"
    row["sourceReceipt"] = {
        "contractName": source_contract,
        "contractVersion": source_contract_version,
        "path": source_receipt_path,
        "sha256": source_receipt_sha256,
    }
    return row


def _registry_tuple_sort_key(row: dict[str, Any]) -> tuple[str, ...]:
    return (
        row["platform"],
        row["rid"],
        row["head"],
        row["artifactRole"],
        row["path"],
    )


def _verify_registry_authority(registry_root: Path) -> None:
    registry_root = exact_directory(registry_root, "pinned Registry source root")
    try:
        commit = subprocess.run(
            ["git", "-C", str(registry_root), "rev-parse", "HEAD"],
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()
    except (OSError, subprocess.CalledProcessError) as exc:
        fail(f"could not resolve pinned Registry source commit: {exc}")
    if commit != REGISTRY_AUTHORITY_COMMIT:
        fail(
            "Registry PREPARE source commit differs from the frozen authority: "
            f"expected {REGISTRY_AUTHORITY_COMMIT}, got {commit}"
        )
    for reference in REGISTRY_PROJECTION_INPUTS.values():
        path = exact_file(
            registry_root / reference["path"],
            f"pinned Registry source {reference['path']}",
        )
        digest, size = file_digest_size(path)
        if digest != reference["sha256"] or size != reference["sizeBytes"]:
            fail(f"pinned Registry source bytes changed: {reference['path']}")


def _registry_binding_path(path: Path, evidence_root: Path, label: str) -> str:
    try:
        relative = path.relative_to(evidence_root).as_posix()
    except ValueError:
        fail(f"{label} must be contained by the sealed candidate evidence root")
    return portable_relative_path(relative, label)


def prepare_registry_projection(
    *,
    args: argparse.Namespace,
    proposal_output: Path,
    incumbent_snapshot_dir: Path,
    build_manifest: dict[str, Any],
    incumbent_manifest: dict[str, Any],
    build_rows_by_sha: dict[str, dict[str, Any]],
    incumbent_rows_by_sha: dict[str, dict[str, Any]],
    build_files: Path,
    delta: list[dict[str, Any]],
    non_published: list[dict[str, Any]],
    incumbent: list[dict[str, Any]],
    version: str,
    consumer_commit: str,
) -> tuple[dict[str, Any], dict[str, Any], Path, Path]:
    registry_root_arg = getattr(args, "registry_root", None)
    prepare_root_arg = getattr(args, "registry_prepare_root", None)
    desktop_commit_arg = getattr(args, "desktop_commit", None)
    if registry_root_arg is None or prepare_root_arg is None or desktop_commit_arg is None:
        fail(
            "Registry PREPARE requires explicit --registry-root, "
            "--registry-prepare-root, and --desktop-commit"
        )
    registry_root = exact_directory(
        Path(registry_root_arg), "pinned Registry source root"
    )
    desktop_commit = require_commit(desktop_commit_arg, "desktop producer commit")
    _verify_registry_authority(registry_root)
    prepare_root = fresh_absolute_path(
        Path(prepare_root_arg), "Registry PREPARE transaction root"
    )
    evidence_root = proposal_output.parent
    _registry_binding_path(prepare_root, evidence_root, "Registry PREPARE root")
    prepare_root.mkdir(mode=0o700)
    inputs_root = prepare_root / "inputs"
    inputs_root.mkdir(mode=0o700)
    registry_incumbent = inputs_root / "incumbent"
    registry_delta = inputs_root / "delta"
    registry_evidence = inputs_root / "evidence"
    copy_tree_exact(incumbent_snapshot_dir, registry_incumbent)
    registry_delta.mkdir(mode=0o700)
    registry_evidence.mkdir(mode=0o700)

    incumbent_source_rows = [
        incumbent_rows_by_sha[row["manifestRowSha256"]]
        for row in incumbent
        if row["artifactRole"] == "installer"
    ]
    incumbent_version, _incumbent_channel = manifest_identity(
        incumbent_manifest, "incumbent Registry lineage manifest"
    )
    incumbent_lineage = _registry_source_receipt(
        incumbent_manifest,
        incumbent_source_rows,
        ui_commit=consumer_commit,
        desktop_commit=None,
        release_version=incumbent_version,
    )
    incumbent_lineage_path = registry_incumbent / REGISTRY_INCUMBENT_LINEAGE_PATH
    write_new_registry_json(incumbent_lineage_path, incumbent_lineage)
    incumbent_lineage_sha = sha256_file(incumbent_lineage_path)

    fresh_installer_bindings = [
        row for row in [*delta, *non_published] if row["artifactRole"] == "installer"
    ]
    fresh_source_rows = {
        row["platform"]: build_rows_by_sha[row["manifestRowSha256"]]
        for row in fresh_installer_bindings
    }
    source_contract = _resolve_alias(
        build_manifest,
        ("contractName", "contract_name"),
        "build Registry lineage contract",
        _exact_text,
        required=True,
    )
    source_contract_version = build_manifest.get("schemaVersion")
    if (
        isinstance(source_contract_version, bool)
        or not isinstance(source_contract_version, int)
        or source_contract_version < 1
    ):
        fail("build Registry lineage schemaVersion must be a positive integer")
    lineage_bindings: dict[str, tuple[str, str]] = {}
    for platform, root, receipt_relative in (
        ("windows", registry_delta, REGISTRY_WINDOWS_LINEAGE_PATH),
        ("linux", registry_evidence, REGISTRY_LINUX_LINEAGE_PATH),
    ):
        receipt = _registry_source_receipt(
            build_manifest,
            [fresh_source_rows[platform]],
            ui_commit=consumer_commit,
            desktop_commit=desktop_commit,
            release_version=version,
        )
        receipt_path = root / receipt_relative
        write_new_registry_json(receipt_path, receipt)
        lineage_bindings[platform] = (receipt_relative, sha256_file(receipt_path))
    for binding in delta:
        target = registry_delta / "files" / binding["fileName"]
        copy_regular_exact(build_files / binding["fileName"], target)
        os.chmod(target, 0o644, follow_symlinks=False)
    for binding in non_published:
        target = registry_evidence / "files" / binding["fileName"]
        copy_regular_exact(build_files / binding["fileName"], target)
        os.chmod(target, 0o644, follow_symlinks=False)

    incumbent_tuples = sorted(
        [
            _registry_tuple(
                row,
                source_receipt_path=REGISTRY_INCUMBENT_LINEAGE_PATH,
                source_receipt_sha256=incumbent_lineage_sha,
                source_contract=str(incumbent_lineage["contractName"]),
                source_contract_version=int(incumbent_lineage["contractVersion"]),
            )
            for row in incumbent
        ],
        key=_registry_tuple_sort_key,
    )
    delta_tuples = sorted(
        [
            _registry_tuple(
                row,
                source_receipt_path=lineage_bindings["windows"][0],
                source_receipt_sha256=lineage_bindings["windows"][1],
                source_contract=source_contract,
                source_contract_version=source_contract_version,
            )
            for row in delta
        ],
        key=_registry_tuple_sort_key,
    )
    evidence_tuples = sorted(
        [
            _registry_tuple(
                row,
                source_receipt_path=lineage_bindings["linux"][0],
                source_receipt_sha256=lineage_bindings["linux"][1],
                source_contract=source_contract,
                source_contract_version=source_contract_version,
            )
            for row in non_published
        ],
        key=_registry_tuple_sort_key,
    )
    incumbent_inventory = _registry_inventory(registry_incumbent)
    canonical_path = registry_incumbent / CANONICAL_MANIFEST_NAME
    compatibility_path = registry_incumbent / COMPATIBILITY_MANIFEST_NAME
    incumbent_snapshot = {
        "canonicalManifest": {
            "path": CANONICAL_MANIFEST_NAME,
            "sha256": sha256_file(canonical_path),
            "sizeBytes": canonical_path.stat().st_size,
        },
        "compatibilityManifest": {
            "path": COMPATIBILITY_MANIFEST_NAME,
            "sha256": sha256_file(compatibility_path),
            "sizeBytes": compatibility_path.stat().st_size,
        },
        "desktopTupleSetSha256": registry_document_sha256(incumbent_tuples),
        "desktopTuples": incumbent_tuples,
        "fullInventory": incumbent_inventory,
        "fullInventorySha256": registry_document_sha256(incumbent_inventory),
        "managedPaths": [row["path"] for row in incumbent_inventory],
        "platforms": sorted({row["platform"] for row in incumbent_tuples}),
    }
    incumbent_snapshot["snapshotSha256"] = canonical_sha256(
        {
            "canonicalManifestSha256": incumbent_snapshot["canonicalManifest"]["sha256"],
            "compatibilityManifestSha256": incumbent_snapshot["compatibilityManifest"]["sha256"],
            "desktopTupleSetSha256": incumbent_snapshot["desktopTupleSetSha256"],
            "desktopTuples": incumbent_tuples,
            "inventory": incumbent_inventory,
            "inventorySha256": incumbent_snapshot["fullInventorySha256"],
            "managedPaths": incumbent_snapshot["managedPaths"],
            "platforms": incumbent_snapshot["platforms"],
        }
    )
    composition = {
        "channel": "preview",
        "contractName": "chummer.registry.preview-publication-delta-composition",
        "contractVersion": 1,
        "incumbentSnapshot": incumbent_snapshot,
        "nonPublishedEvidenceTupleSetSha256": registry_document_sha256(evidence_tuples),
        "nonPublishedEvidenceTuples": evidence_tuples,
        "policy": {
            "allowIncumbentRemoval": False,
            "deltaPlatforms": ["windows"],
            "evidencePlatforms": ["linux"],
            "producerDeployAuthority": False,
            "producerReleaseUploadAuthority": False,
            "retainAllIncumbent": True,
            "scope": "windows_only",
        },
        "producerCommits": {
            "desktop": desktop_commit,
            "registry": REGISTRY_AUTHORITY_COMMIT,
            "ui": consumer_commit,
        },
        "publicationDeltaTupleSetSha256": registry_document_sha256(delta_tuples),
        "publicationDeltaTuples": delta_tuples,
        "releaseVersion": version,
    }
    composition_path = prepare_root / "composition.json"
    write_new_registry_json(composition_path, composition)
    composition_sha = sha256_file(composition_path)
    output_root = prepare_root / "output"
    output_root.mkdir(mode=0o700)
    command = [
        sys.executable,
        str(registry_root / REGISTRY_PROJECTION_INPUTS["materializer"]["path"]),
        "prepare",
        "--composition-input",
        str(composition_path),
        "--expected-composition-input-sha256",
        composition_sha,
        "--incumbent-root",
        str(registry_incumbent),
        "--delta-root",
        str(registry_delta),
        "--evidence-root",
        str(registry_evidence),
        "--output-manifest",
        str(output_root / CANONICAL_MANIFEST_NAME),
        "--output-compatibility-manifest",
        str(output_root / COMPATIBILITY_MANIFEST_NAME),
        "--output-candidate-receipt",
        str(output_root / "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"),
    ]
    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        fail(
            "pinned Registry PREPARE rejected the sealed composition: "
            f"{completed.stderr.strip()}"
        )
    verified = subprocess.run(
        [
            sys.executable,
            str(registry_root / REGISTRY_PROJECTION_INPUTS["verifier"]["path"]),
            str(output_root),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    if verified.returncode != 0:
        fail(
            "pinned Registry whole-directory verification failed: "
            f"{verified.stderr.strip()}"
        )
    output_inventory = _registry_inventory(output_root)
    if (
        [row["path"] for row in output_inventory]
        != sorted(REGISTRY_PREPARE_OUTPUT_NAMES)
        or any(row["mode"] != "0644" for row in output_inventory)
    ):
        fail("Registry PREPARE output must be exactly three mode-0644 files")
    candidate_path = output_root / "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"
    candidate, _candidate_sha = read_json_bound(
        candidate_path, "Registry PREPARE candidate receipt"
    )
    if (
        candidate.get("publicationStatus") != "review_required"
        or candidate.get("publicationEligible") is not False
        or candidate.get("releaseUploadAuthority") is not False
        or candidate.get("deployAuthority") is not False
        or candidate.get("routeAuthority") is not False
        or candidate.get("registryProjectionInputs") != REGISTRY_PROJECTION_INPUTS
    ):
        fail("Registry PREPARE candidate receipt overclaims or changed source bindings")
    relative_prepare_root = _registry_binding_path(
        prepare_root, evidence_root, "Registry PREPARE root"
    )
    projection_inputs = {
        key: dict(value) for key, value in REGISTRY_PROJECTION_INPUTS.items()
    }
    binding = {
        "candidateReceiptSha256": sha256_file(candidate_path),
        "composition": {
            "mode": "0644",
            "path": f"{relative_prepare_root}/composition.json",
            "sha256": composition_sha,
            "sizeBytes": composition_path.stat().st_size,
        },
        "contractName": REGISTRY_PREPARE_CONTRACT_NAME,
        "contractVersion": REGISTRY_PREPARE_CONTRACT_VERSION,
        "deployAuthority": False,
        "finalizeAvailable": True,
        "finalizeReceipt": None,
        "inputRoots": {
            name: {
                "fileCount": len(_registry_inventory(path)),
                "inventorySha256": registry_document_sha256(_registry_inventory(path)),
                "path": f"{relative_prepare_root}/inputs/{name}",
            }
            for name, path in (
                ("delta", registry_delta),
                ("evidence", registry_evidence),
                ("incumbent", registry_incumbent),
            )
        },
        "outputInventory": output_inventory,
        "outputInventorySha256": registry_document_sha256(output_inventory),
        "projectionInputs": projection_inputs,
        "publicationEligible": False,
        "registryCommit": REGISTRY_AUTHORITY_COMMIT,
        "releaseUploadAuthority": False,
        "routeAuthority": False,
        "status": "review_required",
        "wholeDirectoryVerified": True,
    }
    return binding, candidate, output_root, incumbent_lineage_path


def prepare_scope(args: argparse.Namespace) -> dict[str, Any]:
    build_manifest_path = exact_file(args.build_manifest, "build evidence manifest")
    build_releases_path = exact_file(args.build_releases, "build evidence compatibility manifest")
    incumbent_manifest_path = exact_file(args.incumbent_manifest, "incumbent manifest")
    incumbent_releases_path = exact_file(args.incumbent_releases, "incumbent compatibility manifest")
    signing_receipt_path = exact_file(args.signing_receipt, "Windows signing receipt")
    build_files = exact_directory(args.build_files_dir, "build evidence files directory")
    incumbent_files = exact_directory(args.incumbent_files_dir, "incumbent files directory")
    incumbent_shelf_arg = getattr(args, "incumbent_shelf_dir", None)
    if incumbent_shelf_arg is None:
        fail("v2 publication scope requires an explicit incumbent full shelf")
    incumbent_snapshot_arg = getattr(args, "incumbent_snapshot_dir", None)
    if incumbent_snapshot_arg is None:
        fail("v2 publication scope requires an explicit sealed incumbent snapshot path")
    incumbent_shelf = exact_directory(
        Path(incumbent_shelf_arg), "incumbent full shelf"
    )
    incumbent_snapshot_dir = fresh_absolute_path(
        Path(incumbent_snapshot_arg), "incumbent full-shelf snapshot output"
    )
    consumer_commit = require_commit(args.consumer_commit, "consumer commit")
    build_manifest, build_manifest_sha = read_json_bound(
        build_manifest_path, "build evidence manifest"
    )
    build_releases, _build_releases_sha = read_json_bound(
        build_releases_path, "build evidence compatibility manifest"
    )
    incumbent_manifest, incumbent_manifest_sha = read_json_bound(
        incumbent_manifest_path, "incumbent manifest"
    )
    incumbent_releases, incumbent_releases_sha = read_json_bound(
        incumbent_releases_path, "incumbent compatibility manifest"
    )
    signing_receipt, signing_receipt_sha = read_json_bound(
        signing_receipt_path, "Windows signing receipt"
    )
    version, channel = manifest_identity(build_manifest, "build evidence manifest")
    manifest_identity(incumbent_manifest, "incumbent manifest")
    if manifest_identity(build_releases, "build compatibility manifest") != (version, channel):
        fail("build compatibility release identity differs from the canonical manifest")
    if manifest_identity(
        incumbent_releases, "incumbent compatibility manifest"
    ) != manifest_identity(incumbent_manifest, "incumbent manifest"):
        fail("incumbent compatibility release identity differs from the canonical manifest")
    build_receipt = source_receipt(
        args.build_manifest_receipt_path,
        build_manifest,
        build_manifest_sha,
    )
    incumbent_receipt = source_receipt(
        args.incumbent_manifest_receipt_path,
        incumbent_manifest,
        incumbent_manifest_sha,
    )
    build, build_by_key = build_bindings(
        build_manifest,
        receipt=build_receipt,
        files_dir=build_files,
        consumer_commit=consumer_commit,
        label="build evidence manifest",
    )
    if set(build_by_key) != EXPECTED_BUILD_KEYS:
        fail("build evidence must contain exactly Windows installer/payload and Linux installer")
    incumbent, incumbent_by_key = build_bindings(
        incumbent_manifest,
        receipt=incumbent_receipt,
        files_dir=incumbent_files,
        consumer_commit=consumer_commit,
        label="incumbent manifest",
    )
    incumbent_windows_keys = {
        key for key in incumbent_by_key if key[1] == "windows"
    }
    if incumbent_windows_keys not in (set(), EXPECTED_DELTA_KEYS):
        fail(
            "incumbent shelf must contain either no Windows tuple or the exact "
            "installer/payload pair being replaced"
        )
    if any(
        key[1] == "windows" and key not in EXPECTED_DELTA_KEYS
        for key in incumbent_by_key
    ):
        fail("incumbent shelf contains an unsupported additional Windows tuple")
    delta = sorted(
        [row for key, row in build_by_key.items() if key in EXPECTED_DELTA_KEYS],
        key=tuple_sort_key,
    )
    if {binding_key(row, "publicationDeltaTuples") for row in delta} != EXPECTED_DELTA_KEYS:
        fail("publication delta must be the complete signed Windows installer/payload pair")
    validate_signing_receipt_payload(signing_receipt, delta, expected_version=version)
    retained = sorted(
        [row for key, row in incumbent_by_key.items() if key not in EXPECTED_DELTA_KEYS],
        key=tuple_sort_key,
    )
    non_published = sorted(
        [
            {
                **row,
                "path": f"release-evidence/non-published/files/{row['fileName']}",
            }
            for key, row in build_by_key.items()
            if key not in EXPECTED_DELTA_KEYS
        ],
        key=tuple_sort_key,
    )
    if {binding_key(row, "nonPublishedEvidenceTuples") for row in non_published} != {
        ("avalonia", "linux", "linux-x64", "installer")
    }:
        fail("the fresh Linux build must be the exact non-published evidence tuple")
    post = sorted([*retained, *delta], key=tuple_sort_key)
    post_keys = {binding_key(row, "postPublicationShelfTuples") for row in post}
    expected_post_keys = (set(incumbent_by_key) - EXPECTED_DELTA_KEYS) | EXPECTED_DELTA_KEYS
    if post_keys != expected_post_keys or len(post) != len(post_keys):
        fail("post-publication shelf is not the exact retained/Windows-delta union")
    retained_non_windows = {
        key: row for key, row in incumbent_by_key.items() if key[1] != "windows"
    }
    post_non_windows = {key: row for key, row in ((binding_key(row, "post"), row) for row in post) if key[1] != "windows"}
    if post_non_windows != retained_non_windows:
        fail("post-publication shelf changed an incumbent non-Windows tuple")
    if {row["path"] for row in non_published} & {row["path"] for row in post}:
        fail("non-published Linux evidence overlaps the publication shelf")

    output_root = fresh_absolute_path(
        args.publication_dir, "publication output directory"
    )
    proposal_output = fresh_absolute_path(args.output, "publication scope proposal output")
    require_disjoint_paths(
        (
            ("incumbent full shelf", incumbent_shelf),
            ("publication output directory", output_root),
            ("sealed incumbent snapshot", incumbent_snapshot_dir),
            ("publication scope proposal output", proposal_output),
        )
    )
    output_root.mkdir(mode=0o700)
    non_published_outputs: list[Path] = []
    try:
        snapshot_inventory = copy_tree_exact(
            incumbent_shelf, incumbent_snapshot_dir
        )
        snapshot_manifest_path = incumbent_snapshot_dir / CANONICAL_MANIFEST_NAME
        snapshot_releases_path = incumbent_snapshot_dir / COMPATIBILITY_MANIFEST_NAME
        snapshot_files = incumbent_snapshot_dir / "files"
        if (
            sha256_file(snapshot_manifest_path) != incumbent_manifest_sha
            or sha256_file(snapshot_releases_path) != incumbent_releases_sha
            or snapshot_files.is_symlink()
            or not snapshot_files.is_dir()
        ):
            fail("incumbent full-shelf snapshot differs from the retained authority")
        build_rows_by_sha = _manifest_row_by_sha(build_manifest)
        incumbent_rows_by_sha = _manifest_row_by_sha(incumbent_manifest)
        installer_bindings = [row for row in post if row["artifactRole"] == "installer"]
        source_public_rows: list[dict[str, Any]] = []
        for binding in installer_bindings:
            source_rows = (
                build_rows_by_sha if binding["platform"] == "windows" else incumbent_rows_by_sha
            )
            row = source_rows.get(binding["manifestRowSha256"])
            if row is None:
                fail("post-publication tuple cannot resolve its exact manifest row")
            source_public_rows.append(row)
        source_public_rows.sort(
            key=lambda row: (
                str(row.get("head") or row.get("headId")),
                str(row.get("platform") or row.get("platformId")),
                str(row.get("rid")),
                str(row.get("artifactId")),
            )
        )
        public_rows = source_public_rows
        public_manifest = dict(incumbent_manifest)
        _copy_release_identity(public_manifest, build_manifest)
        public_manifest["artifacts"] = public_rows
        post_platforms = sorted({row["platform"] for row in post})
        canonical_coverage = dict(
            public_manifest.get("desktopTupleCoverage") or {}
        )
        canonical_coverage["requiredDesktopPlatforms"] = post_platforms
        public_manifest["desktopTupleCoverage"] = canonical_coverage
        build_downloads = _downloads_by_name(build_releases, "build compatibility manifest")
        incumbent_downloads = _downloads_by_name(
            incumbent_releases, "incumbent compatibility manifest"
        )
        public_downloads: list[dict[str, Any]] = []
        for row in source_public_rows:
            name = portable_name(row.get("fileName"), "public manifest fileName")
            source_downloads = build_downloads if row.get("platform") == "windows" else incumbent_downloads
            download = source_downloads.get(name)
            if download is None:
                fail(f"post-publication compatibility row is missing: {name}")
            public_downloads.append(
                _require_compatibility_bijection(
                    row,
                    download,
                    f"post-publication {name}",
                    version=version,
                    channel=channel,
                )
            )
        public_releases = _compatibility_manifest_projection(
            incumbent_releases,
            build_releases,
            public_downloads,
            post_platforms,
        )
        registry_prepare: dict[str, Any] | None = None
        registry_candidate: dict[str, Any] | None = None
        registry_output_root: Path | None = None
        registry_incumbent_lineage: Path | None = None
        registry_args = (
            getattr(args, "registry_root", None),
            getattr(args, "registry_prepare_root", None),
            getattr(args, "desktop_commit", None),
        )
        if any(value is not None for value in registry_args):
            (
                registry_prepare,
                registry_candidate,
                registry_output_root,
                registry_incumbent_lineage,
            ) = prepare_registry_projection(
                args=args,
                proposal_output=proposal_output,
                incumbent_snapshot_dir=incumbent_snapshot_dir,
                build_manifest=build_manifest,
                incumbent_manifest=incumbent_manifest,
                build_rows_by_sha=build_rows_by_sha,
                incumbent_rows_by_sha=incumbent_rows_by_sha,
                build_files=build_files,
                delta=delta,
                non_published=non_published,
                incumbent=incumbent,
                version=version,
                consumer_commit=consumer_commit,
            )
            copy_regular_exact(
                registry_output_root / CANONICAL_MANIFEST_NAME,
                output_root / CANONICAL_MANIFEST_NAME,
            )
            copy_regular_exact(
                registry_output_root / COMPATIBILITY_MANIFEST_NAME,
                output_root / COMPATIBILITY_MANIFEST_NAME,
            )
            public_manifest = read_json(
                output_root / CANONICAL_MANIFEST_NAME,
                "Registry-authoritative publication manifest",
            )
            public_releases = read_json(
                output_root / COMPATIBILITY_MANIFEST_NAME,
                "Registry-authoritative compatibility manifest",
            )
        else:
            write_new_json(output_root / CANONICAL_MANIFEST_NAME, public_manifest)
            write_new_json(output_root / COMPATIBILITY_MANIFEST_NAME, public_releases)
        snapshot_by_path = {row["path"]: row for row in snapshot_inventory}
        if registry_prepare is None:
            for name in (CANONICAL_MANIFEST_NAME, COMPATIBILITY_MANIFEST_NAME):
                snapshot_row = snapshot_by_path.get(name)
                if snapshot_row is None:
                    fail(f"incumbent full-shelf snapshot is missing {name}")
                os.chmod(
                    output_root / name,
                    snapshot_row["mode"],
                    follow_symlinks=False,
                )
        public_files = output_root / "files"
        public_files.mkdir(mode=0o700)
        for binding in post:
            source_root = build_files if binding["platform"] == "windows" else snapshot_files
            public_file = public_files / binding["fileName"]
            copy_regular_exact(source_root / binding["fileName"], public_file)
            if registry_prepare is not None and binding["platform"] == "windows":
                os.chmod(public_file, 0o644, follow_symlinks=False)
        if registry_incumbent_lineage is not None:
            copy_regular_exact(
                registry_incumbent_lineage,
                output_root / REGISTRY_INCUMBENT_LINEAGE_PATH,
            )
        for binding in non_published:
            target = output_root.parent / binding["path"]
            copy_regular_exact(build_files / binding["fileName"], target)
            non_published_outputs.append(target)
        publication_inventory = file_inventory(output_root)
        expected_file_names = {row["fileName"] for row in post}
        actual_file_names = {
            row["path"].removeprefix("files/")
            for row in publication_inventory
            if row["path"].startswith("files/")
        }
        if actual_file_names != expected_file_names:
            fail("complete publication shelf contains missing or unexplained artifact bytes")
        full_manifest_sha = sha256_file(output_root / CANONICAL_MANIFEST_NAME)
        incumbent_managed_paths = sorted(
            {
                CANONICAL_MANIFEST_NAME,
                COMPATIBILITY_MANIFEST_NAME,
                *(f"files/{row['fileName']}" for row in incumbent),
            }
        )
        missing_managed = set(incumbent_managed_paths) - set(snapshot_by_path)
        if missing_managed:
            fail(
                "incumbent full-shelf snapshot is missing managed paths: "
                f"{sorted(missing_managed)}"
            )
        ancillary_inventory = [
            row for row in snapshot_inventory if row["path"] not in incumbent_managed_paths
        ]
        full_shelf_inventory = sorted(
            [*ancillary_inventory, *publication_inventory],
            key=lambda row: row["path"],
        )
        validate_inventory(full_shelf_inventory, "full shelf inventory")
        if registry_candidate is not None:
            registry_full_inventory = [
                {
                    "mode": int(row["mode"], 8),
                    "path": row["path"],
                    "sha256": row["sha256"],
                    "sizeBytes": row["sizeBytes"],
                }
                for row in registry_candidate["fullShelfInventory"]
            ]
            if registry_full_inventory != full_shelf_inventory:
                fail(
                    "Registry PREPARE candidate inventory differs from the exact "
                    "composed publication shelf"
                )
        incumbent_snapshot = {
            "canonicalManifestSha256": incumbent_manifest_sha,
            "compatibilityManifestSha256": incumbent_releases_sha,
            "desktopTupleSetSha256": canonical_sha256(incumbent),
            "desktopTuples": incumbent,
            "inventory": snapshot_inventory,
            "inventorySha256": canonical_sha256(snapshot_inventory),
            "managedPaths": incumbent_managed_paths,
            "platforms": sorted({row["platform"] for row in incumbent}),
        }
        incumbent_snapshot_sha = canonical_sha256(incumbent_snapshot)
        decision = {
            "channel": channel,
            "fullShelfCompatibilityManifestSha256": sha256_file(
                output_root / COMPATIBILITY_MANIFEST_NAME
            ),
            "fullShelfInventorySha256": canonical_sha256(full_shelf_inventory),
            "fullShelfManifestSha256": full_manifest_sha,
            "incumbentSnapshotSha256": incumbent_snapshot_sha,
            "publicationDeltaSha256": canonical_sha256(delta),
            "releaseVersion": version,
            "scope": "windows_only",
        }
        macos_incumbent = sorted(
            [row for row in incumbent if row["platform"] == "macos"], key=tuple_sort_key
        )
        macos_post = sorted(
            [row for row in post if row["platform"] == "macos"], key=tuple_sort_key
        )
        if macos_incumbent != macos_post:
            fail("macOS soak exemption is invalid because incumbent macOS tuples drifted")
        macos_soak = (
            {
                "byteIdentical": True,
                "incumbentTupleSetSha256": canonical_sha256(macos_incumbent),
                "postPublicationTupleSetSha256": canonical_sha256(macos_post),
                "reason": "retained_byte_identical",
                "required": False,
            }
            if macos_incumbent
            else {
                "byteIdentical": False,
                "incumbentTupleSetSha256": canonical_sha256([]),
                "postPublicationTupleSetSha256": canonical_sha256([]),
                "reason": "not_applicable_no_incumbent_tuple",
                "required": False,
            }
        )
        payload = {
            "approvalIndependent": False,
            "authenticodeRequired": True,
            "authenticodeVerificationSha256": None,
            "buildEvidenceTuples": build,
            "contractName": CONTRACT_NAME,
            "contractVersion": CONTRACT_VERSION,
            "fullShelfCompatibilityManifestSha256": sha256_file(
                output_root / COMPATIBILITY_MANIFEST_NAME
            ),
            "fullShelfInventory": full_shelf_inventory,
            "fullShelfInventorySha256": canonical_sha256(full_shelf_inventory),
            "fullShelfManifestSha256": full_manifest_sha,
            "incumbentSnapshot": incumbent_snapshot,
            "incumbentSnapshotSha256": incumbent_snapshot_sha,
            "macosSoak": macos_soak,
            "nativeEvidenceComposite": None,
            "nativeEvidenceSha256": None,
            "nonPublishedEvidenceTuples": non_published,
            "postPublicationShelfTuples": post,
            "publicationDeltaTuples": delta,
            "publicationEligible": False,
            "registryPrepare": registry_prepare,
            "registryFinalizeEligible": False,
            "release": {"channel": channel, "version": version},
            "retainedTuples": retained,
            "scopeDecision": decision,
            "scopeDecisionSha256": canonical_sha256(decision),
            "signingReceipt": {
                "path": SIGNING_RECEIPT_RELATIVE_PATH,
                "sha256": signing_receipt_sha,
            },
            "signingReceiptSha256": signing_receipt_sha,
            "status": "awaiting_native_evidence_and_independent_approval",
            "uploadAuthorized": False,
            "deployAuthorized": False,
            "visualApprovalSha256": None,
        }
        write_new_json(proposal_output, payload)
        return payload
    except Exception:
        shutil.rmtree(output_root, ignore_errors=True)
        shutil.rmtree(incumbent_snapshot_dir, ignore_errors=True)
        for path in non_published_outputs:
            path.unlink(missing_ok=True)
        proposal_output.unlink(missing_ok=True)
        raise


def exact_timestamp(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.endswith("Z"):
        fail(f"{label} must be an exact UTC timestamp")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        fail(f"{label} is invalid: {exc}")
    if parsed.tzinfo is None or parsed.astimezone(UTC) != parsed:
        fail(f"{label} must be UTC")
    return value


def sha_values(value: object) -> set[str]:
    values: set[str] = set()
    if isinstance(value, dict):
        for child in value.values():
            values.update(sha_values(child))
    elif isinstance(value, list):
        for child in value:
            values.update(sha_values(child))
    elif isinstance(value, str):
        candidate = value.removeprefix("sha256:")
        if SHA256_RE.fullmatch(candidate):
            values.add(candidate)
    return values


def validate_registry_prepare_binding(
    binding: object,
    *,
    full_manifest_sha256: str | None = None,
    compatibility_manifest_sha256: str | None = None,
) -> str:
    if not isinstance(binding, dict) or set(binding) != {
        "candidateReceiptSha256",
        "composition",
        "contractName",
        "contractVersion",
        "deployAuthority",
        "finalizeAvailable",
        "finalizeReceipt",
        "inputRoots",
        "outputInventory",
        "outputInventorySha256",
        "projectionInputs",
        "publicationEligible",
        "registryCommit",
        "releaseUploadAuthority",
        "routeAuthority",
        "status",
        "wholeDirectoryVerified",
    }:
        fail("Registry PREPARE binding has missing or extra fields")
    if (
        binding.get("contractName") != REGISTRY_PREPARE_CONTRACT_NAME
        or binding.get("contractVersion") != REGISTRY_PREPARE_CONTRACT_VERSION
        or type(binding.get("contractVersion")) is not int
        or binding.get("registryCommit") != REGISTRY_AUTHORITY_COMMIT
        or binding.get("projectionInputs") != REGISTRY_PROJECTION_INPUTS
        or binding.get("status") != "review_required"
        or binding.get("wholeDirectoryVerified") is not True
        or binding.get("finalizeAvailable") is not True
        or binding.get("finalizeReceipt") is not None
        or binding.get("publicationEligible") is not False
        or binding.get("releaseUploadAuthority") is not False
        or binding.get("deployAuthority") is not False
        or binding.get("routeAuthority") is not False
    ):
        fail("Registry PREPARE binding identity or fail-closed authority changed")
    composition = binding.get("composition")
    if (
        not isinstance(composition, dict)
        or set(composition) != {"mode", "path", "sha256", "sizeBytes"}
        or composition.get("mode") != "0644"
        or not str(composition.get("path", "")).endswith("/composition.json")
        or isinstance(composition.get("sizeBytes"), bool)
        or not isinstance(composition.get("sizeBytes"), int)
        or composition["sizeBytes"] < 1
    ):
        fail("Registry PREPARE composition binding is malformed")
    require_sha256(composition.get("sha256"), "Registry composition sha256")
    composition_relative = portable_relative_path(
        composition.get("path"), "Registry composition path"
    )
    prepare_root = PurePosixPath(composition_relative).parent.as_posix()
    if (
        PurePosixPath(composition_relative).name != "composition.json"
        or prepare_root != "registry-prepare"
    ):
        fail("Registry PREPARE composition must use registry-prepare/composition.json")
    roots = binding.get("inputRoots")
    if not isinstance(roots, dict) or set(roots) != {
        "delta",
        "evidence",
        "incumbent",
    }:
        fail("Registry PREPARE input-root binding is malformed")
    for name, root in roots.items():
        if (
            not isinstance(root, dict)
            or set(root) != {"fileCount", "inventorySha256", "path"}
            or isinstance(root.get("fileCount"), bool)
            or not isinstance(root.get("fileCount"), int)
            or root["fileCount"] < 2
        ):
            fail(f"Registry PREPARE {name} input-root binding is malformed")
        require_sha256(
            root.get("inventorySha256"), f"Registry PREPARE {name} inventory sha256"
        )
        root_path = portable_relative_path(
            root.get("path"), f"Registry PREPARE {name} path"
        )
        if root_path != f"{prepare_root}/inputs/{name}":
            fail(
                f"Registry PREPARE {name} input root is not anchored to the "
                "sealed composition"
            )
    rows = binding.get("outputInventory")
    if not isinstance(rows, list) or len(rows) != 3:
        fail("Registry PREPARE output inventory must contain exactly three files")
    if [row.get("path") for row in rows if isinstance(row, dict)] != sorted(
        REGISTRY_PREPARE_OUTPUT_NAMES
    ):
        fail("Registry PREPARE output inventory names changed")
    for row in rows:
        if (
            not isinstance(row, dict)
            or set(row) != {"mode", "path", "sha256", "sizeBytes"}
            or row.get("mode") != "0644"
            or isinstance(row.get("sizeBytes"), bool)
            or not isinstance(row.get("sizeBytes"), int)
            or row["sizeBytes"] < 1
        ):
            fail("Registry PREPARE output inventory row is malformed")
        require_sha256(row.get("sha256"), "Registry PREPARE output sha256")
    if binding.get("outputInventorySha256") != registry_document_sha256(rows):
        fail("Registry PREPARE output inventory digest differs")
    by_name = {row["path"]: row for row in rows}
    candidate_sha = require_sha256(
        binding.get("candidateReceiptSha256"),
        "Registry PREPARE candidate receipt sha256",
    )
    if (
        by_name["PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"]["sha256"]
        != candidate_sha
    ):
        fail("Registry PREPARE candidate receipt digest differs from output inventory")
    if (
        full_manifest_sha256 is not None
        and by_name[CANONICAL_MANIFEST_NAME]["sha256"]
        != full_manifest_sha256
    ):
        fail("Registry PREPARE canonical bytes differ from the publication scope")
    if (
        compatibility_manifest_sha256 is not None
        and by_name[COMPATIBILITY_MANIFEST_NAME]["sha256"]
        != compatibility_manifest_sha256
    ):
        fail("Registry PREPARE compatibility bytes differ from the publication scope")
    return canonical_sha256(binding)


def verify_registry_prepare_files(
    binding: object,
    evidence_root: Path,
    *,
    publication_dir: Path | None = None,
) -> tuple[str, ...]:
    """Verify and enumerate every byte in one sealed Registry PREPARE transaction."""

    binding_sha = validate_registry_prepare_binding(binding)
    if not isinstance(binding, dict):  # narrowed by the validator above
        fail("Registry PREPARE binding is not an object")
    root = exact_directory(evidence_root, "sealed Registry PREPARE evidence root")
    composition = binding["composition"]
    composition_relative = composition["path"]
    prepare_relative = PurePosixPath(composition_relative).parent.as_posix()

    def contained_file(relative: str, label: str) -> Path:
        relative = portable_relative_path(relative, f"{label} path")
        path = exact_file(root.joinpath(*PurePosixPath(relative).parts), label)
        try:
            path.resolve(strict=True).relative_to(root)
        except ValueError:
            fail(f"{label} escapes the sealed Registry PREPARE evidence root")
        return path

    def contained_directory(relative: str, label: str) -> Path:
        relative = portable_relative_path(relative, f"{label} path")
        path = exact_directory(root.joinpath(*PurePosixPath(relative).parts), label)
        try:
            path.resolve(strict=True).relative_to(root)
        except ValueError:
            fail(f"{label} escapes the sealed Registry PREPARE evidence root")
        return path

    paths: list[str] = []
    composition_path = contained_file(
        composition_relative, "sealed Registry PREPARE composition"
    )
    composition_digest, composition_size = file_digest_size(composition_path)
    if (
        composition_digest != composition["sha256"]
        or composition_size != composition["sizeBytes"]
        or stat.S_IMODE(composition_path.stat().st_mode) != 0o644
    ):
        fail("sealed Registry PREPARE composition bytes or mode changed")
    paths.append(composition_relative)

    for name in ("delta", "evidence", "incumbent"):
        root_binding = binding["inputRoots"][name]
        input_root = contained_directory(
            root_binding["path"], f"sealed Registry PREPARE {name} input root"
        )
        inventory = _registry_inventory(input_root)
        if (
            len(inventory) != root_binding["fileCount"]
            or registry_document_sha256(inventory)
            != root_binding["inventorySha256"]
        ):
            fail(f"sealed Registry PREPARE {name} input inventory changed")
        paths.extend(
            f"{root_binding['path']}/{row['path']}" for row in inventory
        )

    output_relative = f"{prepare_relative}/output"
    output_root = contained_directory(
        output_relative, "sealed Registry PREPARE output root"
    )
    output_inventory = _registry_inventory(output_root)
    if output_inventory != binding["outputInventory"]:
        fail("sealed Registry PREPARE output bytes, modes, or file set changed")
    if registry_document_sha256(output_inventory) != binding["outputInventorySha256"]:
        fail("sealed Registry PREPARE output inventory digest changed")
    paths.extend(f"{output_relative}/{row['path']}" for row in output_inventory)

    candidate_path = output_root / "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"
    candidate, candidate_sha = read_json_bound(
        candidate_path, "sealed Registry PREPARE candidate receipt"
    )
    if candidate_sha != binding["candidateReceiptSha256"] or any(
        (
            candidate.get("publicationStatus") != "review_required",
            candidate.get("publicationEligible") is not False,
            candidate.get("releaseUploadAuthority") is not False,
            candidate.get("deployAuthority") is not False,
            candidate.get("routeAuthority") is not False,
            candidate.get("registryProjectionInputs") != REGISTRY_PROJECTION_INPUTS,
        )
    ):
        fail("sealed Registry PREPARE candidate receipt changed or overclaims authority")

    if publication_dir is not None:
        shelf = exact_directory(publication_dir, "Registry-projected publication shelf")
        for name in (CANONICAL_MANIFEST_NAME, COMPATIBILITY_MANIFEST_NAME):
            if read_regular_bytes(
                output_root / name, f"sealed Registry PREPARE {name}"
            ) != read_regular_bytes(shelf / name, f"publication shelf {name}"):
                fail(f"Registry PREPARE {name} differs byte-for-byte from the shelf")

    if len(paths) != len(set(paths)):
        fail("sealed Registry PREPARE file set contains overlapping paths")
    if canonical_sha256(binding) != binding_sha:
        fail("Registry PREPARE binding changed while its files were verified")
    return tuple(sorted(paths))


def replay_registry_prepare(
    binding: object,
    evidence_root: Path,
    registry_root: Path,
) -> dict[str, Any]:
    """Re-run the pinned PREPARE and byte/mode-compare its complete output."""

    if not isinstance(binding, dict):
        fail("Registry PREPARE replay requires an exact binding")
    root = exact_directory(evidence_root, "sealed Registry PREPARE replay evidence root")
    registry = exact_directory(registry_root, "pinned Registry replay source root")
    _verify_registry_authority(registry)
    sealed_paths = verify_registry_prepare_files(
        binding,
        root,
        publication_dir=root / PUBLICATION_DIRECTORY,
    )
    composition_relative = binding["composition"]["path"]
    prepare_relative = PurePosixPath(composition_relative).parent.as_posix()
    composition_path = exact_file(
        root.joinpath(*PurePosixPath(composition_relative).parts),
        "Registry PREPARE replay composition",
    )
    input_paths = {
        name: exact_directory(
            root.joinpath(*PurePosixPath(binding["inputRoots"][name]["path"]).parts),
            f"Registry PREPARE replay {name} input",
        )
        for name in ("delta", "evidence", "incumbent")
    }
    sealed_output = exact_directory(
        root.joinpath(*PurePosixPath(f"{prepare_relative}/output").parts),
        "sealed Registry PREPARE replay output",
    )

    with tempfile.TemporaryDirectory(prefix="chummer-registry-prepare-replay-") as temporary:
        replay_output = Path(temporary) / "output"
        replay_output.mkdir(mode=0o700)
        command = [
            sys.executable,
            str(registry / REGISTRY_PROJECTION_INPUTS["materializer"]["path"]),
            "prepare",
            "--composition-input",
            str(composition_path),
            "--expected-composition-input-sha256",
            binding["composition"]["sha256"],
            "--incumbent-root",
            str(input_paths["incumbent"]),
            "--delta-root",
            str(input_paths["delta"]),
            "--evidence-root",
            str(input_paths["evidence"]),
            "--output-manifest",
            str(replay_output / CANONICAL_MANIFEST_NAME),
            "--output-compatibility-manifest",
            str(replay_output / COMPATIBILITY_MANIFEST_NAME),
            "--output-candidate-receipt",
            str(replay_output / "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"),
        ]
        completed = subprocess.run(command, capture_output=True, text=True, check=False)
        if completed.returncode != 0:
            fail(
                "pinned Registry PREPARE replay rejected the sealed inputs: "
                f"{completed.stderr.strip()}"
            )
        verified = subprocess.run(
            [
                sys.executable,
                str(registry / REGISTRY_PROJECTION_INPUTS["verifier"]["path"]),
                str(replay_output),
            ],
            capture_output=True,
            text=True,
            check=False,
        )
        if verified.returncode != 0:
            fail(
                "pinned Registry PREPARE replay whole-directory verification failed: "
                f"{verified.stderr.strip()}"
            )
        replay_inventory = _registry_inventory(replay_output)
        if replay_inventory != binding["outputInventory"]:
            fail("Registry PREPARE replay output bytes, modes, or file set changed")
        for row in replay_inventory:
            if read_regular_bytes(
                replay_output / row["path"],
                f"Registry PREPARE replay {row['path']}",
            ) != read_regular_bytes(
                sealed_output / row["path"],
                f"sealed Registry PREPARE {row['path']}",
            ):
                fail(f"Registry PREPARE replay raw bytes changed: {row['path']}")

    if verify_registry_prepare_files(
        binding,
        root,
        publication_dir=root / PUBLICATION_DIRECTORY,
    ) != sealed_paths:
        fail("Registry PREPARE inputs changed while the pinned replay executed")
    return {
        "contractName": "chummer6-ui.registry-preview-prepare-replay",
        "contractVersion": 1,
        "outputInventorySha256": binding["outputInventorySha256"],
        "registryCommit": binding["registryCommit"],
        "registryPrepareSha256": canonical_sha256(binding),
        "status": "reproduced",
        "wholeDirectoryVerified": True,
    }


def replay_registry_prepare_command(args: argparse.Namespace) -> dict[str, Any]:
    scope_path = exact_file(args.scope, "Registry PREPARE replay publication scope")
    payload, _scope_sha = read_json_bound(
        scope_path, "Registry PREPARE replay publication scope"
    )
    validate_proposal(payload)
    if payload.get("status") != "validated":
        fail("Registry PREPARE replay requires the finalized validated UI scope")
    return replay_registry_prepare(
        payload.get("registryPrepare"),
        args.evidence_root,
        args.registry_root,
    )


def validate_approval(
    approval: dict[str, Any],
    proposal: dict[str, Any],
    proposal_sha256: str,
    authenticode_verification_sha256: str,
    disallowed_actors: Iterable[str],
) -> str:
    expected_keys = {
        "approvedAt",
        "approver",
        "authenticodeVerificationSha256",
        "contractName",
        "contractVersion",
        "fullShelfCompatibilityManifestSha256",
        "fullShelfInventorySha256",
        "fullShelfManifestSha256",
        "incumbentSnapshotSha256",
        "publicationDeltaSha256",
        "publicationScopeProposalSha256",
        "registryPrepareSha256",
        "scopeDecisionSha256",
        "signingReceiptSha256",
        "status",
    }
    if set(approval) != expected_keys:
        fail("scope approval has missing or extra fields")
    if (
        approval.get("contractName") != APPROVAL_CONTRACT_NAME
        or type(approval.get("contractVersion")) is not int
        or approval.get("contractVersion") != CONTRACT_VERSION
        or approval.get("status") != "approved"
    ):
        fail("scope approval does not use the exact approved v2 contract")
    approver = require_actor(approval.get("approver"), "scope approver")
    exact_timestamp(approval.get("approvedAt"), "scope approval approvedAt")
    blocked = {require_actor(actor, "disallowed approval actor").lower() for actor in disallowed_actors}
    if approver.lower() in blocked:
        fail("scope approval is not independent of candidate production/capture")
    expected = {
        "authenticodeVerificationSha256": require_sha256(
            authenticode_verification_sha256,
            "independent Authenticode verification sha256",
        ),
        "scopeDecisionSha256": proposal["scopeDecisionSha256"],
        "incumbentSnapshotSha256": proposal["incumbentSnapshotSha256"],
        "fullShelfCompatibilityManifestSha256": proposal[
            "fullShelfCompatibilityManifestSha256"
        ],
        "fullShelfInventorySha256": proposal["fullShelfInventorySha256"],
        "fullShelfManifestSha256": proposal["fullShelfManifestSha256"],
        "signingReceiptSha256": proposal["signingReceiptSha256"],
        "publicationDeltaSha256": canonical_sha256(proposal["publicationDeltaTuples"]),
        "publicationScopeProposalSha256": require_sha256(
            proposal_sha256, "publication scope proposal sha256"
        ),
        "registryPrepareSha256": (
            validate_registry_prepare_binding(
                proposal["registryPrepare"],
                full_manifest_sha256=proposal["fullShelfManifestSha256"],
                compatibility_manifest_sha256=proposal[
                    "fullShelfCompatibilityManifestSha256"
                ],
            )
            if proposal.get("registryPrepare") is not None
            else None
        ),
    }
    for key, value in expected.items():
        if approval.get(key) != value:
            fail(f"scope approval {key} differs from the exact decision")
    return approver


def validate_proposal(payload: dict[str, Any]) -> None:
    if (
        payload.get("contractName") != CONTRACT_NAME
        or type(payload.get("contractVersion")) is not int
        or payload.get("contractVersion") != CONTRACT_VERSION
    ):
        fail("old or unsupported publication scope contract")
    status = payload.get("status")
    expected_keys = (
        PROPOSAL_KEYS
        if status == "awaiting_native_evidence_and_independent_approval"
        else FINAL_KEYS
        if status == "validated"
        else set()
    )
    if not expected_keys or set(payload) != expected_keys:
        fail("publication scope has an unsupported status or missing/extra fields")
    for field in (
        "buildEvidenceTuples",
        "publicationDeltaTuples",
        "retainedTuples",
        "postPublicationShelfTuples",
        "nonPublishedEvidenceTuples",
    ):
        validate_tuple_set(payload.get(field), field)
    if payload.get("authenticodeRequired") is not True:
        fail("Windows-only publication scope must require Authenticode")
    if payload.get("uploadAuthorized") is not False or payload.get("deployAuthorized") is not False:
        fail("stage publication scope must not grant upload or deploy authority")
    if payload.get("scopeDecisionSha256") != canonical_sha256(payload.get("scopeDecision")):
        fail("scope decision digest differs")
    if payload.get("incumbentSnapshotSha256") != canonical_sha256(payload.get("incumbentSnapshot")):
        fail("incumbent snapshot digest differs")
    full_shelf_inventory = validate_inventory(
        payload.get("fullShelfInventory"), "full shelf inventory"
    )
    if payload.get("fullShelfInventorySha256") != canonical_sha256(
        full_shelf_inventory
    ):
        fail("full shelf inventory digest differs")
    for field in (
        "fullShelfCompatibilityManifestSha256",
        "fullShelfInventorySha256",
        "fullShelfManifestSha256",
        "incumbentSnapshotSha256",
        "scopeDecisionSha256",
        "signingReceiptSha256",
    ):
        require_sha256(payload.get(field), field)
    release = payload.get("release")
    if (
        not isinstance(release, dict)
        or set(release) != {"channel", "version"}
        or release.get("channel") != "preview"
        or not isinstance(release.get("version"), str)
        or not release["version"]
    ):
        fail("publication scope release identity is malformed")
    signing = payload.get("signingReceipt")
    if signing != {
        "path": SIGNING_RECEIPT_RELATIVE_PATH,
        "sha256": payload.get("signingReceiptSha256"),
    }:
        fail("publication scope signing receipt binding is malformed")
    registry_prepare = payload.get("registryPrepare")
    if registry_prepare is not None:
        validate_registry_prepare_binding(
            registry_prepare,
            full_manifest_sha256=payload["fullShelfManifestSha256"],
            compatibility_manifest_sha256=payload[
                "fullShelfCompatibilityManifestSha256"
            ],
        )

    build = payload["buildEvidenceTuples"]
    delta = payload["publicationDeltaTuples"]
    retained = payload["retainedTuples"]
    post = payload["postPublicationShelfTuples"]
    non_published = payload["nonPublishedEvidenceTuples"]
    build_by_key = {binding_key(row, "buildEvidenceTuples"): row for row in build}
    delta_by_key = {
        binding_key(row, "publicationDeltaTuples"): row for row in delta
    }
    if set(build_by_key) != EXPECTED_BUILD_KEYS:
        fail("build evidence is not the exact Windows/Linux tuple set")
    if set(delta_by_key) != EXPECTED_DELTA_KEYS:
        fail("publication delta is not the complete Windows pair")
    if any(row["platform"] == "windows" for row in retained):
        fail("retained tuples must not contain an additional Windows artifact")
    for label, rows in (
        ("buildEvidenceTuples", build),
        ("publicationDeltaTuples", delta),
        ("retainedTuples", retained),
        ("postPublicationShelfTuples", post),
    ):
        if any(row["path"] != f"files/{row['fileName']}" for row in rows):
            fail(f"{label} contains a path that differs from its exact artifact byte")
    if any(delta_by_key[key] != build_by_key[key] for key in EXPECTED_DELTA_KEYS):
        fail("publication delta differs from the exact Windows build evidence")
    if len(non_published) != 1:
        fail("non-published evidence must contain exactly the fresh Linux installer")
    linux_key = ("avalonia", "linux", "linux-x64", "installer")
    linux_evidence = non_published[0]
    if binding_key(linux_evidence, "nonPublishedEvidenceTuples") != linux_key:
        fail("non-published evidence is not the exact fresh Linux tuple")
    expected_linux = {
        **build_by_key[linux_key],
        "path": (
            "release-evidence/non-published/files/"
            f"{build_by_key[linux_key]['fileName']}"
        ),
    }
    if linux_evidence != expected_linux:
        fail("non-published Linux evidence differs from the build-evidence bytes")
    expected_post = sorted([*retained, *delta], key=tuple_sort_key)
    if post != expected_post:
        fail("post-publication shelf is not retained union Windows delta")
    retained_non_windows = sorted(
        [row for row in retained if row["platform"] != "windows"],
        key=tuple_sort_key,
    )
    post_non_windows = sorted(
        [row for row in post if row["platform"] != "windows"],
        key=tuple_sort_key,
    )
    if post_non_windows != retained_non_windows:
        fail("post-publication shelf changed incumbent non-Windows tuples")
    incumbent_snapshot = payload.get("incumbentSnapshot")
    build_receipts = {
        canonical_sha256(row["sourceReceipt"]): row["sourceReceipt"] for row in build
    }
    if len(build_receipts) != 1:
        fail("build publication tuples do not share one exact manifest authority")
    snapshot_inventory = (
        incumbent_snapshot.get("inventory")
        if isinstance(incumbent_snapshot, dict)
        else None
    )
    snapshot_rows = validate_inventory(
        snapshot_inventory, "incumbent snapshot inventory"
    )
    snapshot_by_path = {row["path"]: row for row in snapshot_rows}
    snapshot_desktop_tuples = validate_tuple_set(
        incumbent_snapshot.get("desktopTuples")
        if isinstance(incumbent_snapshot, dict)
        else None,
        "incumbent snapshot desktop tuples",
    )
    snapshot_desktop_by_key = {
        binding_key(row, "incumbent snapshot desktop tuples"): row
        for row in snapshot_desktop_tuples
    }
    incumbent_windows_keys = {
        key for key in snapshot_desktop_by_key if key[1] == "windows"
    }
    if incumbent_windows_keys not in (set(), EXPECTED_DELTA_KEYS):
        fail("incumbent snapshot contains a partial or unsupported Windows tuple set")
    expected_retained = sorted(
        [
            row
            for key, row in snapshot_desktop_by_key.items()
            if key not in EXPECTED_DELTA_KEYS
        ],
        key=tuple_sort_key,
    )
    if retained != expected_retained:
        fail("retained tuples differ from every incumbent non-Windows tuple")
    incumbent_receipts = {
        canonical_sha256(row["sourceReceipt"]): row["sourceReceipt"]
        for row in snapshot_desktop_tuples
    }
    if len(incumbent_receipts) != 1:
        fail("incumbent snapshot tuples do not share one exact manifest authority")
    managed_paths = (
        incumbent_snapshot.get("managedPaths")
        if isinstance(incumbent_snapshot, dict)
        else None
    )
    expected_managed_paths = sorted(
        {
            CANONICAL_MANIFEST_NAME,
            COMPATIBILITY_MANIFEST_NAME,
            *(f"files/{row['fileName']}" for row in snapshot_desktop_tuples),
        }
    )
    if (
        not isinstance(incumbent_snapshot, dict)
        or set(incumbent_snapshot)
        != {
            "canonicalManifestSha256",
            "compatibilityManifestSha256",
            "desktopTupleSetSha256",
            "desktopTuples",
            "inventory",
            "inventorySha256",
            "managedPaths",
            "platforms",
        }
        or incumbent_snapshot.get("inventorySha256")
        != canonical_sha256(snapshot_rows)
        or incumbent_snapshot.get("desktopTupleSetSha256")
        != canonical_sha256(snapshot_desktop_tuples)
        or incumbent_snapshot.get("platforms")
        != sorted({row["platform"] for row in snapshot_desktop_tuples})
        or managed_paths != expected_managed_paths
        or any(path not in snapshot_by_path for path in expected_managed_paths)
        or next(iter(incumbent_receipts.values())).get("sha256")
        != incumbent_snapshot.get("canonicalManifestSha256")
    ):
        fail("incumbent snapshot does not exactly bind every retained byte")
    for row in retained:
        snapshot_row = snapshot_by_path.get(f"files/{row['fileName']}")
        if snapshot_row is None or any(
            snapshot_row[key] != row[key] for key in ("sha256", "sizeBytes")
        ):
            fail("incumbent snapshot changed a retained desktop byte")
    full_by_path = {row["path"]: row for row in full_shelf_inventory}
    ancillary_paths = set(snapshot_by_path) - set(expected_managed_paths)
    expected_full_paths = ancillary_paths | {
        CANONICAL_MANIFEST_NAME,
        COMPATIBILITY_MANIFEST_NAME,
        *(f"files/{row['fileName']}" for row in post),
    }
    if registry_prepare is not None:
        expected_full_paths.add(REGISTRY_INCUMBENT_LINEAGE_PATH)
    if set(full_by_path) != expected_full_paths:
        fail("full shelf inventory is not retained ancillary union final managed shelf")
    for path in ancillary_paths:
        if full_by_path[path] != snapshot_by_path[path]:
            fail(f"full shelf inventory changed retained ancillary bytes or mode: {path}")
    for row in retained:
        path = f"files/{row['fileName']}"
        if full_by_path.get(path) != snapshot_by_path.get(path):
            fail(f"full shelf inventory changed retained desktop bytes or mode: {path}")
    for row in delta:
        final_row = full_by_path.get(f"files/{row['fileName']}")
        if final_row is None or any(
            final_row[key] != row[key] for key in ("sha256", "sizeBytes")
        ):
            fail("full shelf inventory differs from the Windows publication delta")
    if (
        full_by_path[CANONICAL_MANIFEST_NAME]["sha256"]
        != payload.get("fullShelfManifestSha256")
        or full_by_path[COMPATIBILITY_MANIFEST_NAME]["sha256"]
        != payload.get("fullShelfCompatibilityManifestSha256")
    ):
        fail("full shelf inventory manifest bytes differ")
    require_sha256(
        incumbent_snapshot.get("canonicalManifestSha256"),
        "incumbent canonical manifest sha256",
    )
    require_sha256(
        incumbent_snapshot.get("compatibilityManifestSha256"),
        "incumbent compatibility manifest sha256",
    )
    decision = payload.get("scopeDecision")
    expected_decision = {
        "channel": "preview",
        "fullShelfCompatibilityManifestSha256": payload[
            "fullShelfCompatibilityManifestSha256"
        ],
        "fullShelfInventorySha256": payload["fullShelfInventorySha256"],
        "fullShelfManifestSha256": payload["fullShelfManifestSha256"],
        "incumbentSnapshotSha256": payload["incumbentSnapshotSha256"],
        "publicationDeltaSha256": canonical_sha256(delta),
        "releaseVersion": release["version"],
        "scope": "windows_only",
    }
    if decision != expected_decision:
        fail("scope decision does not bind the exact shelf and Windows delta")

    if status == "awaiting_native_evidence_and_independent_approval":
        if (
            payload.get("approvalIndependent") is not False
            or payload.get("publicationEligible") is not False
            or payload.get("registryFinalizeEligible") is not False
            or payload.get("authenticodeVerificationSha256") is not None
            or payload.get("nativeEvidenceComposite") is not None
            or payload.get("nativeEvidenceSha256") is not None
            or payload.get("visualApprovalSha256") is not None
        ):
            fail("pre-capture publication scope claims unearned evidence or approval")
    else:
        if (
            payload.get("approvalIndependent") is not True
            or payload.get("registryFinalizeEligible") is not True
            or payload.get("publicationEligible") is not False
        ):
            fail(
                "final UI scope must be independently approved and eligible only "
                "for Registry FINALIZE while publication remains fail-closed"
            )
        require_sha256(payload.get("nativeEvidenceSha256"), "native evidence sha256")
        require_sha256(
            payload.get("authenticodeVerificationSha256"),
            "independent Authenticode verification sha256",
        )
        visuals = payload.get("visualApprovalSha256")
        if not isinstance(visuals, list) or not visuals:
            fail("final publication scope lacks visual approval digests")
        for digest in visuals:
            require_sha256(digest, "visual approval sha256")
        composite = validate_native_evidence_composite_binding(
            payload.get("nativeEvidenceComposite")
        )
        if (
            composite["wrapper"]["sha256"] != payload["nativeEvidenceSha256"]
            or composite["visualProof"]["sha256"] not in visuals
            or composite["authenticodeVerification"]["sha256"]
            != payload["authenticodeVerificationSha256"]
        ):
            fail("native evidence composite differs from the final flat evidence bindings")
        approval = payload.get("approval")
        if (
            not isinstance(approval, dict)
            or set(approval) != {"approver", "path", "sha256"}
        ):
            fail("final publication scope approval binding is malformed")
        require_actor(approval.get("approver"), "final scope approver")
        require_sha256(approval.get("sha256"), "final scope approval sha256")
        approval_path = approval.get("path")
        if (
            not isinstance(approval_path, str)
            or PurePosixPath(approval_path).is_absolute()
            or any(
                part in {"", ".", ".."}
                for part in PurePosixPath(approval_path).parts
            )
            or approval_path != PurePosixPath(approval_path).as_posix()
        ):
            fail("final scope approval path is not portable")
    mac = payload.get("macosSoak")
    mac_incumbent = sorted(
        [row for row in retained if row["platform"] == "macos"], key=tuple_sort_key
    )
    mac_post = sorted([row for row in post if row["platform"] == "macos"], key=tuple_sort_key)
    if mac_incumbent:
        expected_mac = {
            "byteIdentical": True,
            "incumbentTupleSetSha256": canonical_sha256(mac_incumbent),
            "postPublicationTupleSetSha256": canonical_sha256(mac_post),
            "reason": "retained_byte_identical",
            "required": False,
        }
        if mac != expected_mac or mac_incumbent != mac_post:
            fail("macOS soak exemption is invalid after retained tuple drift")
    else:
        expected_mac = {
            "byteIdentical": False,
            "incumbentTupleSetSha256": canonical_sha256([]),
            "postPublicationTupleSetSha256": canonical_sha256([]),
            "reason": "not_applicable_no_incumbent_tuple",
            "required": False,
        }
        if mac != expected_mac or mac_post:
            fail("macOS soak state is invalid when there is no incumbent macOS tuple")


def validate_export_inputs(
    root: Path,
    *,
    expected_version: str,
    installer_sha256: str,
    payload_sha256: str,
) -> dict[str, Any]:
    """Validate the pre-capture scope, signed bytes, and full-shelf manifest.

    The disposable exporter intentionally receives only the complete manifest,
    not every retained binary.  The proposal's incumbent/full-shelf inventory
    remains a stage-side seal obligation and is replayed before upload.
    """
    proposal_path = root / PROPOSAL_FILE_NAME
    signing_path = root / SIGNING_RECEIPT_RELATIVE_PATH
    full_manifest_path = root / PUBLICATION_MANIFEST_RELATIVE_PATH
    full_compatibility_path = root / PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH
    for path, label in (
        (proposal_path, "publication scope proposal"),
        (signing_path, "Windows signing receipt"),
        (full_manifest_path, "full shelf manifest"),
        (full_compatibility_path, "full shelf compatibility manifest"),
    ):
        if path.is_symlink() or not path.is_file():
            fail(f"candidate export is missing {label}")
    proposal, proposal_sha = read_json_bound(proposal_path, "publication scope proposal")
    signing_receipt, signing_sha = read_json_bound(
        signing_path, "Windows signing receipt"
    )
    full_manifest, full_manifest_sha = read_json_bound(
        full_manifest_path, "full shelf manifest"
    )
    _full_compatibility, full_compatibility_sha = read_json_bound(
        full_compatibility_path, "full shelf compatibility manifest"
    )
    validate_proposal(proposal)
    if proposal.get("status") != "awaiting_native_evidence_and_independent_approval":
        fail("candidate export requires an unapproved pre-capture scope proposal")
    if proposal.get("publicationEligible") is not False:
        fail("pre-capture scope must not claim publication eligibility")
    if proposal.get("release") != {"channel": "preview", "version": expected_version}:
        fail("publication scope release differs from the candidate")
    if signing_sha != proposal.get("signingReceiptSha256"):
        fail("publication scope signing receipt bytes changed")
    if full_manifest_sha != proposal.get("fullShelfManifestSha256"):
        fail("publication scope full shelf manifest bytes changed")
    if full_compatibility_sha != proposal.get(
        "fullShelfCompatibilityManifestSha256"
    ):
        fail("publication scope full shelf compatibility manifest bytes changed")
    windows = proposal["publicationDeltaTuples"]
    actual = {
        row["artifactRole"]: row["sha256"] for row in windows
    }
    if actual != {"installer": installer_sha256, "payload": payload_sha256}:
        fail("publication delta differs from the exact candidate Windows bytes")
    validate_signing_receipt_payload(
        signing_receipt, windows, expected_version=expected_version
    )
    full_version, full_channel = manifest_identity(full_manifest, "full shelf manifest")
    if (full_version, full_channel) != (expected_version, "preview"):
        fail("full shelf manifest release identity differs from the candidate")
    full_rows = manifest_rows(full_manifest, "full shelf manifest")
    full_windows = [
        row
        for row in full_rows
        if row.get("head") == "avalonia"
        and row.get("platform") == "windows"
        and row.get("rid") == "win-x64"
    ]
    if len(full_windows) != 1:
        fail("full shelf manifest lacks the exact Windows delta row")
    full_windows_row = full_windows[0]
    if (
        full_windows_row.get("sha256") != installer_sha256
        or full_windows_row.get("payloadSha256") != payload_sha256
    ):
        fail("full shelf manifest Windows bytes differ from the signed candidate")
    result: dict[str, Any] = {
        "proposal": {
            "path": PROPOSAL_FILE_NAME,
            "sha256": proposal_sha,
        },
        "signingReceipt": {
            "path": SIGNING_RECEIPT_RELATIVE_PATH,
            "sha256": signing_sha,
        },
        "fullShelfManifest": {
            "path": PUBLICATION_MANIFEST_RELATIVE_PATH,
            "sha256": full_manifest_sha,
        },
        "fullShelfCompatibilityManifest": {
            "path": PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH,
            "sha256": full_compatibility_sha,
        },
        "scopeDecisionSha256": proposal["scopeDecisionSha256"],
        "incumbentSnapshotSha256": proposal["incumbentSnapshotSha256"],
        "publicationDeltaSha256": canonical_sha256(windows),
    }
    registry_prepare = proposal.get("registryPrepare")
    if registry_prepare is not None:
        verify_registry_prepare_files(
            registry_prepare,
            root,
            publication_dir=root / PUBLICATION_DIRECTORY,
        )
        result["registryPrepare"] = registry_prepare
        result["registryPrepareSha256"] = canonical_sha256(registry_prepare)
    return result


def finalize_scope(args: argparse.Namespace) -> dict[str, Any]:
    proposal_path = exact_file(args.proposal, "publication scope proposal")
    approval_path = exact_file(args.approval, "independent scope approval")
    native_path = exact_file(args.native_evidence, "native Windows evidence")
    visual_paths = [exact_file(path, "Windows visual approval") for path in args.visual_approval]
    proposal, proposal_sha = read_json_bound(proposal_path, "publication scope proposal")
    validate_proposal(proposal)
    if proposal.get("status") != "awaiting_native_evidence_and_independent_approval":
        fail("publication scope proposal is not awaiting final evidence")
    if proposal.get("macosSoak", {}).get("required") is not False:
        fail("Windows-only publication must keep macOS soak nonblocking")
    native, native_composite = _validate_native_wrapper_and_documents(
        native_path, visual_paths, proposal
    )
    native_sha = native_composite["wrapper"]["sha256"]
    installer_rows = [
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "installer"
    ]
    authenticode_sha = validate_native_authenticode(
        native, native_path.parent, installer_rows
    )
    approval, approval_sha = read_json_bound(
        approval_path, "independent scope approval"
    )
    approver = validate_approval(
        approval,
        proposal,
        proposal_sha,
        authenticode_sha,
        args.disallowed_actor,
    )
    delta_digests = {row["sha256"] for row in proposal["publicationDeltaTuples"]}
    if not delta_digests.issubset(sha_values(native)):
        fail("native Windows evidence does not bind every Windows delta digest")
    installer_digests = {row["sha256"] for row in installer_rows}
    visual_digests: list[str] = []
    visual_reviewers: set[str] = set()
    for path in visual_paths:
        visual, visual_sha = read_json_bound(path, "Windows visual approval")
        if visual.get("status") not in {"passed", "pass"}:
            fail("Windows visual approval is not passing")
        if not installer_digests.issubset(sha_values(visual)):
            fail("Windows visual approval does not bind the signed installer digest")
        review = visual.get("review")
        reviewer = review.get("authenticatedReviewer") if isinstance(review, dict) else None
        visual_reviewers.add(require_actor(reviewer, "visual reviewer"))
        visual_digests.append(visual_sha)
    if len(visual_reviewers) != 1:
        fail("Windows visual approvals do not share one authenticated reviewer")
    payload = dict(proposal)
    payload.update(
        {
            "approval": {
                "approver": approver,
                "path": args.approval_receipt_path,
                "sha256": approval_sha,
            },
            "approvalIndependent": True,
            "authenticodeVerificationSha256": authenticode_sha,
            "nativeEvidenceComposite": native_composite,
            "nativeEvidenceSha256": native_sha,
            "publicationEligible": False,
            "registryFinalizeEligible": True,
            "status": "validated",
            "uploadAuthorized": False,
            "deployAuthorized": False,
            "visualApprovalSha256": sorted(visual_digests),
        }
    )
    write_new_json(args.output, payload)
    return payload


def evidence_file(root: Path, relative: str, label: str) -> Path:
    token = PurePosixPath(relative)
    if (
        token.is_absolute()
        or relative != token.as_posix()
        or any(part in {"", ".", ".."} for part in token.parts)
        or "\\" in relative
    ):
        fail(f"{label} path is not an exact portable relative path")
    path = root.joinpath(*token.parts)
    if path.is_symlink() or not path.is_file():
        fail(f"{label} is missing or not a regular file")
    resolved = path.resolve(strict=True)
    try:
        resolved.relative_to(root)
    except ValueError:
        fail(f"{label} escapes the sealed evidence root")
    return resolved


def _native_contract_reference(
    value: object,
    *,
    label: str,
    contract_name: str,
    contract_version: int,
    path: str,
) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != {
        "contractName",
        "contractVersion",
        "path",
        "sha256",
        "sizeBytes",
    }:
        fail(f"{label} must be an exact contract-aware file reference")
    if (
        value.get("contractName") != contract_name
        or type(value.get("contractVersion")) is not int
        or value.get("contractVersion") != contract_version
        or value.get("path") != path
    ):
        fail(f"{label} contract identity or path differs")
    require_sha256(value.get("sha256"), f"{label} sha256")
    require_positive_size(value.get("sizeBytes"), f"{label} sizeBytes")
    return value


def validate_native_evidence_composite_binding(value: object) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != {
        "authenticodeVerification",
        "nativeFinalization",
        "visualProof",
        "wrapper",
    }:
        fail("native evidence composite must contain exactly four contract references")
    _native_contract_reference(
        value["wrapper"],
        label="native evidence wrapper reference",
        contract_name=NATIVE_EVIDENCE_CONTRACT_NAME,
        contract_version=NATIVE_EVIDENCE_CONTRACT_VERSION,
        path=NATIVE_EVIDENCE_RELATIVE_PATH,
    )
    _native_contract_reference(
        value["nativeFinalization"],
        label="native finalization reference",
        contract_name=NATIVE_FINALIZATION_CONTRACT_NAME,
        contract_version=NATIVE_FINALIZATION_CONTRACT_VERSION,
        path=NATIVE_FINALIZATION_RELATIVE_PATH,
    )
    _native_contract_reference(
        value["visualProof"],
        label="Windows visual proof reference",
        contract_name=WINDOWS_VISUAL_PROOF_CONTRACT_NAME,
        contract_version=WINDOWS_VISUAL_PROOF_CONTRACT_VERSION,
        path=WINDOWS_VISUAL_PROOF_RELATIVE_PATH,
    )
    _native_contract_reference(
        value["authenticodeVerification"],
        label="Authenticode verification reference",
        contract_name=AUTHENTICODE_VERIFICATION_CONTRACT_NAME,
        contract_version=1,
        path=AUTHENTICODE_VERIFICATION_RELATIVE_PATH,
    )
    return value


def _native_file_reference(
    path: Path,
    *,
    root: Path,
    contract_name: str,
    contract_version: int,
) -> dict[str, Any]:
    return {
        "contractName": contract_name,
        "contractVersion": contract_version,
        "path": path.relative_to(root).as_posix(),
        "sha256": sha256_file(path),
        "sizeBytes": path.stat().st_size,
    }


def _native_workflow_source(
    value: object,
    *,
    label: str,
    workflow: str,
    artifact_prefix: str,
) -> dict[str, str]:
    expected_keys = {
        "actor",
        "artifactName",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "workflow",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        fail(f"{label} workflow source has missing or extra fields")
    if any(not isinstance(value.get(key), str) or not value[key] for key in expected_keys):
        fail(f"{label} workflow source values must be non-empty strings")
    if (
        value["workflow"] != workflow
        or re.fullmatch(r"[1-9][0-9]*", value["runId"]) is None
        or re.fullmatch(r"[1-9][0-9]*", value["runAttempt"]) is None
        or not value["ref"].startswith(("refs/heads/", "refs/tags/"))
        or any(character.isspace() for character in value["ref"])
        or COMMIT_RE.fullmatch(value["sha"]) is None
        or "/" not in value["repository"]
    ):
        fail(f"{label} workflow/run/commit identity is invalid")
    require_actor(value["actor"], f"{label} actor")
    expected_artifact = f"{artifact_prefix}-{value['runId']}-{value['runAttempt']}"
    if value["artifactName"] != expected_artifact:
        fail(f"{label} artifact name differs from its run identity")
    return value


def _validate_native_wrapper_and_documents(
    native_path: Path,
    visual_paths: list[Path],
    proposal: dict[str, Any],
) -> tuple[dict[str, Any], dict[str, Any]]:
    root = exact_directory(native_path.parent, "native evidence composite root")
    if native_path != root / NATIVE_EVIDENCE_RELATIVE_PATH:
        fail("native evidence wrapper must use the exact stage-root path")
    native, native_sha = read_json_bound(native_path, "native Windows evidence wrapper")
    expected_wrapper_keys = {
        "archivePath",
        "archiveSha256",
        "authenticodeVerification",
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
        "nativeFinalization",
        "progressLogSha256",
        "release",
        "scopeApproval",
        "startupReceiptSha256",
        "status",
        "treeSha256",
        "visualProof",
        "visualProofSha256",
        "visualReviewers",
    }
    if set(native) != expected_wrapper_keys:
        fail("native Windows evidence wrapper has missing or extra fields")
    if (
        native.get("contractName") != NATIVE_EVIDENCE_CONTRACT_NAME
        or type(native.get("contractVersion")) is not int
        or native.get("contractVersion") != NATIVE_EVIDENCE_CONTRACT_VERSION
        or native.get("status") != "passed"
        or native.get("release")
        != {"channel": "preview", "version": proposal["release"]["version"]}
    ):
        fail("native Windows evidence wrapper contract or release identity differs")
    for key in (
        "archiveSha256",
        "captureInventorySha256",
        "finalizationSha256",
        "finalizedInventorySha256",
        "treeSha256",
    ):
        require_sha256(native.get(key), f"native evidence wrapper {key}")
    if isinstance(native.get("fileCount"), bool) or not isinstance(
        native.get("fileCount"), int
    ) or native["fileCount"] < 1:
        fail("native evidence wrapper fileCount is invalid")

    finalization_ref = native.get("nativeFinalization")
    visual_ref = native.get("visualProof")
    if (
        not isinstance(finalization_ref, dict)
        or set(finalization_ref) != {"path", "sha256", "sizeBytes"}
        or finalization_ref.get("path") != NATIVE_FINALIZATION_RELATIVE_PATH
        or not isinstance(visual_ref, dict)
        or set(visual_ref) != {"path", "sha256", "sizeBytes"}
        or visual_ref.get("path") != WINDOWS_VISUAL_PROOF_RELATIVE_PATH
    ):
        fail("native evidence wrapper finalization or visual reference is malformed")
    finalization_path = evidence_file(
        root, NATIVE_FINALIZATION_RELATIVE_PATH, "root native finalization v2"
    )
    nested_finalization_path = evidence_file(
        root,
        NATIVE_FINALIZATION_SOURCE_RELATIVE_PATH,
        "producer native finalization v2",
    )
    finalization, finalization_sha = read_json_bound(
        finalization_path, "root native finalization v2"
    )
    if finalization_path.read_bytes() != nested_finalization_path.read_bytes():
        fail("root native finalization v2 is not byte-identical to the producer document")
    if (
        finalization_ref.get("sha256") != finalization_sha
        or finalization_ref.get("sizeBytes") != finalization_path.stat().st_size
        or native.get("finalizationSha256") != finalization_sha
    ):
        fail("native evidence wrapper finalization reference differs from exact bytes")

    expected_finalization_keys = {
        "authenticodeVerification",
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
        "scopeApproval",
        "status",
    }
    if set(finalization) != expected_finalization_keys or (
        finalization.get("contractName") != NATIVE_FINALIZATION_CONTRACT_NAME
        or type(finalization.get("contractVersion")) is not int
        or finalization.get("contractVersion") != NATIVE_FINALIZATION_CONTRACT_VERSION
        or finalization.get("status") != "passed"
        or finalization.get("humanReviewConfirmed") is not True
        or finalization.get("reviewerWasCaptureActor") is not False
        or finalization.get("captureInventorySha256")
        != native["captureInventorySha256"]
    ):
        fail("native finalization v2 contract or capture binding differs")
    exact_timestamp(finalization.get("generatedAt"), "native finalization generatedAt")
    capture_source = _native_workflow_source(
        finalization.get("captureSource"),
        label="native capture",
        workflow=NATIVE_CAPTURE_WORKFLOW,
        artifact_prefix="windows-native-evidence",
    )
    finalization_source = _native_workflow_source(
        finalization.get("finalizationSource"),
        label="native finalization",
        workflow=NATIVE_FINALIZATION_WORKFLOW,
        artifact_prefix="windows-native-evidence-finalized",
    )
    reviewer = require_actor(finalization.get("reviewer"), "native finalization reviewer")
    if (
        reviewer.casefold() == capture_source["actor"].casefold()
        or reviewer.casefold() != finalization_source["actor"].casefold()
        or capture_source["repository"] != finalization_source["repository"]
        or capture_source["sha"] != finalization_source["sha"]
        or native.get("captureSource") != capture_source
        or native.get("finalizationSource") != finalization_source
    ):
        fail("native finalization actors or workflow authorities differ")

    raw_auth = finalization.get("authenticodeVerification")
    wrapper_auth = native.get("authenticodeVerification")
    auth_keys = {
        "path",
        "sha256",
        "signerCertificateSha256",
        "signerSpkiSha256",
        "sizeBytes",
        "timestampUtc",
    }
    if (
        not isinstance(raw_auth, dict)
        or not isinstance(wrapper_auth, dict)
        or set(raw_auth) != auth_keys
        or set(wrapper_auth) != auth_keys
        or raw_auth.get("path")
        != "authenticode/AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
        or wrapper_auth.get("path") != AUTHENTICODE_VERIFICATION_RELATIVE_PATH
        or any(
            raw_auth.get(key) != wrapper_auth.get(key)
            for key in auth_keys - {"path"}
        )
    ):
        fail("native finalization and wrapper Authenticode references differ")

    capture_path = evidence_file(root, NATIVE_CAPTURE_RELATIVE_PATH, "native capture v2")
    capture, _capture_sha = read_json_bound(capture_path, "native capture v2")
    expected_capture_keys = {
        "authenticodeVerification",
        "candidate",
        "captureMode",
        "channelId",
        "contractName",
        "contractVersion",
        "generatedAt",
        "heads",
        "source",
        "status",
        "version",
    }
    if set(capture) != expected_capture_keys or (
        capture.get("contractName") != NATIVE_CAPTURE_CONTRACT_NAME
        or type(capture.get("contractVersion")) is not int
        or capture.get("contractVersion") != NATIVE_CAPTURE_CONTRACT_VERSION
        or capture.get("status") != "captured"
        or capture.get("captureMode") != "interactive"
        or capture.get("version") != proposal["release"]["version"]
        or capture.get("channelId") != "preview"
        or capture.get("source") != capture_source
        or capture.get("authenticodeVerification") != raw_auth
    ):
        fail("native capture v2 contract, release, or authority differs")
    exact_timestamp(capture.get("generatedAt"), "native capture generatedAt")
    candidate = capture.get("candidate")
    wrapper_candidate = native.get("candidateProvenance")
    if not isinstance(candidate, dict) or not isinstance(wrapper_candidate, dict):
        fail("native capture or wrapper lacks candidate provenance")
    producer_actor = require_actor(candidate.get("actor"), "native candidate producer")
    wrapper_candidate_binding = wrapper_candidate.get("candidate")
    if (
        not isinstance(wrapper_candidate_binding, dict)
        or wrapper_candidate_binding.get("actor") != producer_actor
        or producer_actor.casefold() == reviewer.casefold()
    ):
        fail("native candidate producer and reviewer are not independently bound")

    heads = capture.get("heads")
    if not isinstance(heads, list) or len(heads) != 1:
        fail("native capture v2 must bind exactly one promoted Windows head")
    head = heads[0]
    expected_head_keys = {
        "authenticodeVerification",
        "headId",
        "installer",
        "payload",
        "progressLog",
        "receipt",
        "rid",
        "screenshots",
    }
    if (
        not isinstance(head, dict)
        or set(head) != expected_head_keys
        or head.get("headId") != "avalonia"
        or head.get("rid") != "win-x64"
        or head.get("authenticodeVerification") != raw_auth
    ):
        fail("native capture v2 Windows tuple identity differs")
    rows_by_role = {
        row["artifactRole"]: row
        for row in proposal["publicationDeltaTuples"]
        if row.get("head") == "avalonia"
        and row.get("platform") == "windows"
        and row.get("rid") == "win-x64"
    }
    if set(rows_by_role) != {"installer", "payload"}:
        fail("publication scope lacks the exact Windows installer/payload tuple")
    for role in ("installer", "payload"):
        binding = head.get(role)
        row = rows_by_role[role]
        if (
            not isinstance(binding, dict)
            or set(binding) != {"fileName", "relativePath", "sha256", "sizeBytes"}
            or binding.get("relativePath") != f"files/{row['fileName']}"
            or binding.get("fileName") != row["fileName"]
            or binding.get("sha256") != row["sha256"]
            or binding.get("sizeBytes") != row["sizeBytes"]
        ):
            fail(f"native capture v2 {role} artifact binding differs")
    screenshots = head.get("screenshots")
    if not isinstance(screenshots, list) or len(screenshots) != 2:
        fail("native capture v2 must bind exactly two screenshots")
    screenshot_bindings: dict[str, dict[str, Any]] = {}
    for role, screenshot in zip(("progress", "completion"), screenshots, strict=True):
        expected_path = f"screenshots/windows-installer-avalonia-win-x64-{role}.png"
        if (
            not isinstance(screenshot, dict)
            or set(screenshot) != {"height", "path", "role", "sha256", "width"}
            or screenshot.get("role") != role
            or screenshot.get("path") != expected_path
            or isinstance(screenshot.get("width"), bool)
            or not isinstance(screenshot.get("width"), int)
            or screenshot["width"] < 1
            or isinstance(screenshot.get("height"), bool)
            or not isinstance(screenshot.get("height"), int)
            or screenshot["height"] < 1
        ):
            fail(f"native capture v2 {role} screenshot binding differs")
        screenshot_path = evidence_file(
            root, f"proof/windows-native/{expected_path}", f"native {role} screenshot"
        )
        if screenshot.get("sha256") != sha256_file(screenshot_path):
            fail(f"native capture v2 {role} screenshot bytes differ")
        screenshot_bindings[role] = screenshot
    if len({row["sha256"] for row in screenshot_bindings.values()}) != 2:
        fail("native capture v2 screenshots are not distinct")

    proof_rows = finalization.get("proofs")
    if (
        not isinstance(proof_rows, list)
        or len(proof_rows) != 1
        or not isinstance(proof_rows[0], dict)
        or set(proof_rows[0]) != {"headId", "path", "sha256"}
        or proof_rows[0].get("headId") != "avalonia"
        or proof_rows[0].get("path") != WINDOWS_VISUAL_PROOF_RELATIVE_PATH
    ):
        fail("native finalization v2 visual proof reference is malformed")
    producer_visual_path = evidence_file(
        root,
        f"proof/windows-native/{WINDOWS_VISUAL_PROOF_RELATIVE_PATH}",
        "producer Windows visual proof",
    )
    if proof_rows[0].get("sha256") != sha256_file(producer_visual_path):
        fail("native finalization v2 visual proof digest differs")

    raw_scope = finalization.get("scopeApproval")
    wrapper_scope = native.get("scopeApproval")
    if (
        not isinstance(raw_scope, dict)
        or set(raw_scope) != {"approver", "path", "scopeDecisionSha256", "sha256"}
        or not isinstance(wrapper_scope, dict)
        or set(wrapper_scope)
        != {"approver", "path", "payload", "scopeDecisionSha256", "sha256"}
        or any(wrapper_scope.get(key) != raw_scope.get(key) for key in raw_scope)
        or raw_scope.get("approver") != reviewer
        or raw_scope.get("path")
        != "PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json"
        or raw_scope.get("scopeDecisionSha256") != proposal["scopeDecisionSha256"]
    ):
        fail("native finalization v2 scope approval reference differs")

    if len(visual_paths) != 1:
        fail("native evidence composite requires exactly one Windows visual proof")
    visual_path = visual_paths[0]
    if visual_path != root / WINDOWS_VISUAL_PROOF_RELATIVE_PATH:
        fail("Windows visual proof must use the exact stage-root path")
    visual, visual_sha = read_json_bound(visual_path, "Windows visual proof v1")
    expected_visual_keys = {
        "artifactDigest",
        "artifactFileName",
        "authenticodeVerification",
        "captureBinding",
        "channel",
        "channelId",
        "checks",
        "clippingReview",
        "contractName",
        "contractVersion",
        "contrastReview",
        "finalizationBinding",
        "generatedAt",
        "head",
        "headId",
        "platform",
        "readabilityReview",
        "releaseVersion",
        "review",
        "rid",
        "screenshots",
        "status",
        "version",
    }
    installer = rows_by_role["installer"]
    if set(visual) != expected_visual_keys or (
        visual.get("contractName") != WINDOWS_VISUAL_PROOF_CONTRACT_NAME
        or type(visual.get("contractVersion")) is not int
        or visual.get("contractVersion") != WINDOWS_VISUAL_PROOF_CONTRACT_VERSION
        or visual.get("status") != "passed"
        or visual.get("version") != proposal["release"]["version"]
        or visual.get("releaseVersion") != proposal["release"]["version"]
        or visual.get("channel") != "preview"
        or visual.get("channelId") != "preview"
        or visual.get("platform") != "windows"
        or visual.get("head") != "avalonia"
        or visual.get("headId") != "avalonia"
        or visual.get("rid") != "win-x64"
        or visual.get("artifactFileName") != installer["fileName"]
        or visual.get("artifactDigest") != f"sha256:{installer['sha256']}"
        or visual.get("authenticodeVerification") != wrapper_auth
        or visual.get("finalizationBinding") != finalization_source
        or visual.get("captureBinding")
        != {
            **{
                key: value
                for key, value in capture_source.items()
                if key != "actor"
            },
            "inventorySha256": native["captureInventorySha256"],
        }
    ):
        fail("Windows visual proof v1 contract, tuple, release, or artifact differs")
    exact_timestamp(visual.get("generatedAt"), "Windows visual proof generatedAt")
    if visual.get("checks") != {
        "capture_mode": "interactive",
        "human_review_confirmed": True,
    }:
        fail("Windows visual proof v1 checks differ")
    for key in ("readabilityReview", "contrastReview", "clippingReview"):
        if visual.get(key) != {"status": "passed", "reviewer": reviewer}:
            fail(f"Windows visual proof v1 {key} differs")
    if visual.get("review") != {
        "allowlistSource": "repository variable plus protected environment",
        "authenticatedReviewer": reviewer,
        "captureActor": capture_source["actor"],
        "explicitConfirmations": {
            "clipping": "passed",
            "contrast": "passed",
            "readability": "passed",
        },
    }:
        fail("Windows visual proof v1 review actors or confirmations differ")
    visual_screenshots = visual.get("screenshots")
    if not isinstance(visual_screenshots, list) or len(visual_screenshots) != 2:
        fail("Windows visual proof v1 must bind exactly two screenshots")
    for role, screenshot in zip(
        ("progress", "completion"), visual_screenshots, strict=True
    ):
        capture_screenshot = screenshot_bindings[role]
        expected_path = f"proof/windows-native/{capture_screenshot['path']}"
        if (
            not isinstance(screenshot, dict)
            or set(screenshot) != {"path", "role", "sha256"}
            or screenshot
            != {
                "path": expected_path,
                "role": role,
                "sha256": capture_screenshot["sha256"],
            }
        ):
            fail(f"Windows visual proof v1 {role} screenshot differs")
    if (
        visual_ref.get("sha256") != visual_sha
        or visual_ref.get("sizeBytes") != visual_path.stat().st_size
        or native.get("visualProofSha256") != {"avalonia": visual_sha}
        or native.get("visualReviewers") != {"avalonia": reviewer}
    ):
        fail("native evidence wrapper visual proof reference differs from exact bytes")

    composite = {
        "wrapper": _native_file_reference(
            native_path,
            root=root,
            contract_name=NATIVE_EVIDENCE_CONTRACT_NAME,
            contract_version=NATIVE_EVIDENCE_CONTRACT_VERSION,
        ),
        "nativeFinalization": _native_file_reference(
            finalization_path,
            root=root,
            contract_name=NATIVE_FINALIZATION_CONTRACT_NAME,
            contract_version=NATIVE_FINALIZATION_CONTRACT_VERSION,
        ),
        "visualProof": _native_file_reference(
            visual_path,
            root=root,
            contract_name=WINDOWS_VISUAL_PROOF_CONTRACT_NAME,
            contract_version=WINDOWS_VISUAL_PROOF_CONTRACT_VERSION,
        ),
        "authenticodeVerification": {
            "contractName": AUTHENTICODE_VERIFICATION_CONTRACT_NAME,
            "contractVersion": 1,
            "path": AUTHENTICODE_VERIFICATION_RELATIVE_PATH,
            "sha256": wrapper_auth["sha256"],
            "sizeBytes": wrapper_auth["sizeBytes"],
        },
    }
    validate_native_evidence_composite_binding(composite)
    if composite["wrapper"]["sha256"] != native_sha:
        fail("native evidence composite wrapper digest differs")
    return native, composite


def _authenticode_timestamp(value: object, label: str) -> datetime:
    text = exact_timestamp(value, label)
    return datetime.fromisoformat(text[:-1] + "+00:00")


def _validate_authenticode_chain(
    value: object,
    *,
    label: str,
    timestamp: datetime,
) -> None:
    if not isinstance(value, dict) or set(value) != {
        "revocationFlag",
        "revocationMode",
        "status",
        "trusted",
        "verificationFlags",
        "verificationTimeUtc",
    }:
        fail(f"{label} has missing or extra fields")
    expected = {
        "revocationFlag": "entire_chain",
        "revocationMode": "online",
        "status": [],
        "trusted": True,
        "verificationFlags": "no_flag",
    }
    for key, expected_value in expected.items():
        if value.get(key) != expected_value or type(value.get(key)) is not type(
            expected_value
        ):
            fail(f"{label} is not the exact trusted whole-chain result")
    if _authenticode_timestamp(
        value.get("verificationTimeUtc"), f"{label} verificationTimeUtc"
    ) != timestamp:
        fail(f"{label} was not checked at the RFC3161 timestamp")


def validate_native_authenticode(
    native: dict[str, Any],
    evidence_root: Path,
    installer_rows: list[dict[str, Any]],
    *,
    expected_relative_path: str = AUTHENTICODE_VERIFICATION_RELATIVE_PATH,
) -> str:
    if len(installer_rows) != 1:
        fail("publication scope lacks the exact Windows installer for Authenticode")
    binding = native.get("authenticodeVerification")
    if not isinstance(binding, dict) or set(binding) != {
        "path",
        "sha256",
        "signerCertificateSha256",
        "signerSpkiSha256",
        "sizeBytes",
        "timestampUtc",
    }:
        fail("native Windows evidence lacks the exact Authenticode verification binding")
    if binding.get("path") != expected_relative_path:
        fail("native Authenticode verification uses an unexpected evidence path")
    receipt_path = evidence_file(
        evidence_root,
        binding["path"],
        "independent Authenticode verification receipt",
    )
    receipt_raw, receipt_metadata = read_regular_file(
        receipt_path, "independent Authenticode verification receipt"
    )
    receipt = parse_json_bytes(
        receipt_raw, "independent Authenticode verification receipt"
    )
    receipt_sha = hashlib.sha256(receipt_raw).hexdigest()
    if (
        receipt_sha != require_sha256(binding.get("sha256"), "Authenticode receipt sha256")
        or receipt_metadata.st_size
        != require_positive_size(binding.get("sizeBytes"), "Authenticode receipt sizeBytes")
    ):
        fail("independent Authenticode verification receipt bytes changed")
    if not isinstance(receipt, dict) or set(receipt) != {
        "artifact",
        "contractName",
        "contractVersion",
        "generatedAt",
        "policy",
        "signature",
        "signer",
        "source",
        "status",
        "timestamp",
        "verifier",
    }:
        fail("independent Authenticode verification receipt has missing or extra fields")
    if (
        receipt.get("contractName") != AUTHENTICODE_VERIFICATION_CONTRACT_NAME
        or type(receipt.get("contractVersion")) is not int
        or receipt.get("contractVersion") != 1
        or receipt.get("status") != "verified"
    ):
        fail("independent Authenticode verification receipt is not verified")
    generated_at = _authenticode_timestamp(
        receipt.get("generatedAt"), "Authenticode verification generatedAt"
    )
    if generated_at > datetime.now(UTC) + timedelta(minutes=5):
        fail("independent Authenticode verification receipt is from the future")

    installer = installer_rows[0]
    artifact = receipt.get("artifact")
    expected_artifact = {
        "fileName": installer["fileName"],
        "sha256": installer["sha256"],
        "sizeBytes": installer["sizeBytes"],
    }
    if (
        not isinstance(artifact, dict)
        or set(artifact) != set(expected_artifact)
        or artifact != expected_artifact
        or any(
            type(artifact.get(key)) is not type(value)
            for key, value in expected_artifact.items()
        )
    ):
        fail("independent Authenticode receipt binds different installer bytes")

    capture_source = native.get("captureSource")
    source = receipt.get("source")
    expected_source_keys = {
        "actor",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "workflow",
    }
    if (
        not isinstance(capture_source, dict)
        or not isinstance(source, dict)
        or set(source) != expected_source_keys
        or any(source.get(key) != capture_source.get(key) for key in expected_source_keys)
    ):
        fail("independent Authenticode receipt capture authority differs")

    policy = receipt.get("policy")
    if not isinstance(policy, dict) or set(policy) != {
        "signerCertificateSha256",
        "signerSpkiSha256",
    }:
        fail("Authenticode signer policy binding is malformed")
    signer_certificate_sha = require_sha256(
        policy.get("signerCertificateSha256"), "pinned signer certificate sha256"
    )
    signer_spki_sha = require_sha256(
        policy.get("signerSpkiSha256"), "pinned signer SPKI sha256"
    )
    if (
        binding.get("signerCertificateSha256") != signer_certificate_sha
        or binding.get("signerSpkiSha256") != signer_spki_sha
    ):
        fail("native Authenticode signer identity differs from the verifier policy")

    signature = receipt.get("signature")
    if signature != {
        "codeSigningEkuOid": "1.3.6.1.5.5.7.3.3",
        "cryptographicVerification": "passed",
        "status": "valid",
        "type": "authenticode",
    }:
        fail("native Authenticode signature result is not exact and valid")
    signer = receipt.get("signer")
    if not isinstance(signer, dict) or set(signer) != {
        "certificateSha256",
        "chain",
        "issuer",
        "notAfterUtc",
        "notBeforeUtc",
        "serialNumber",
        "spkiSha256",
        "subject",
    }:
        fail("native Authenticode signer identity is malformed")
    if (
        signer.get("certificateSha256") != signer_certificate_sha
        or signer.get("spkiSha256") != signer_spki_sha
    ):
        fail("validated Authenticode signer differs from the pinned policy")
    for field in ("issuer", "serialNumber", "subject"):
        if not isinstance(signer.get(field), str) or not signer[field] or signer[field] != signer[field].strip():
            fail(f"native Authenticode signer {field} is invalid")
    signer_not_before = _authenticode_timestamp(
        signer.get("notBeforeUtc"), "signer certificate notBeforeUtc"
    )
    signer_not_after = _authenticode_timestamp(
        signer.get("notAfterUtc"), "signer certificate notAfterUtc"
    )

    timestamp = receipt.get("timestamp")
    if not isinstance(timestamp, dict) or set(timestamp) != {
        "attributeOid",
        "certificateSha256",
        "chain",
        "format",
        "generatedAtUtc",
        "issuer",
        "messageImprintAlgorithmOid",
        "messageImprintSha256",
        "notAfterUtc",
        "notBeforeUtc",
        "serialNumber",
        "status",
        "subject",
        "timestampingEkuOid",
    }:
        fail("native RFC3161 timestamp result is malformed")
    expected_timestamp = {
        "attributeOid": "1.2.840.113549.1.9.16.2.14",
        "format": "rfc3161",
        "messageImprintAlgorithmOid": "2.16.840.1.101.3.4.2.1",
        "status": "verified",
        "timestampingEkuOid": "1.3.6.1.5.5.7.3.8",
    }
    if any(timestamp.get(key) != value for key, value in expected_timestamp.items()):
        fail("native RFC3161 timestamp is not exact and verified")
    require_sha256(timestamp.get("certificateSha256"), "timestamp certificate sha256")
    require_sha256(timestamp.get("messageImprintSha256"), "timestamp message imprint sha256")
    for field in ("issuer", "serialNumber", "subject"):
        if not isinstance(timestamp.get(field), str) or not timestamp[field] or timestamp[field] != timestamp[field].strip():
            fail(f"native RFC3161 timestamp {field} is invalid")
    timestamp_at = _authenticode_timestamp(
        timestamp.get("generatedAtUtc"), "RFC3161 timestamp generatedAtUtc"
    )
    tsa_not_before = _authenticode_timestamp(
        timestamp.get("notBeforeUtc"), "timestamp certificate notBeforeUtc"
    )
    tsa_not_after = _authenticode_timestamp(
        timestamp.get("notAfterUtc"), "timestamp certificate notAfterUtc"
    )
    if (
        not signer_not_before <= timestamp_at <= signer_not_after
        or not tsa_not_before <= timestamp_at <= tsa_not_after
        or timestamp_at > generated_at
        or binding.get("timestampUtc") != timestamp.get("generatedAtUtc")
    ):
        fail("native RFC3161 timestamp chronology or certificate validity is invalid")
    _validate_authenticode_chain(
        signer.get("chain"), label="Authenticode signer chain", timestamp=timestamp_at
    )
    _validate_authenticode_chain(
        timestamp.get("chain"), label="RFC3161 timestamp chain", timestamp=timestamp_at
    )

    verifier = receipt.get("verifier")
    verifier_script = Path(__file__).resolve().with_name(
        Path(AUTHENTICODE_VERIFIER_RELATIVE_PATH).name
    )
    if not isinstance(verifier, dict) or set(verifier) != {
        "implementation",
        "implementationSha256",
        "platform",
        "powershellVersion",
    } or any(
        verifier.get(key) != value
        for key, value in {
            "implementation": AUTHENTICODE_VERIFIER_RELATIVE_PATH,
            "implementationSha256": sha256_file(verifier_script),
            "platform": "windows",
        }.items()
    ) or not isinstance(verifier.get("powershellVersion"), str) or not verifier[
        "powershellVersion"
    ]:
        fail("native Authenticode verifier implementation identity differs")
    return receipt_sha


def verify_final_evidence(
    payload: dict[str, Any],
    proposal: dict[str, Any],
    proposal_sha256: str,
    evidence_root: Path,
) -> None:
    root = exact_directory(evidence_root, "sealed publication evidence root")
    approval_binding = payload["approval"]
    approval_path = evidence_file(
        root, approval_binding["path"], "independent publication scope approval"
    )
    approval, approval_sha = read_json_bound(
        approval_path, "independent publication scope approval"
    )
    if approval_sha != approval_binding["sha256"]:
        fail("independent publication scope approval bytes changed")

    installer_rows = [
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "installer"
    ]
    visual_paths = [
        evidence_file(
            root,
            f"WINDOWS_INSTALLER_VISUAL_PROOF-{row['head']}-{row['rid']}.generated.json",
            "Windows visual approval",
        )
        for row in installer_rows
    ]

    native_path = evidence_file(
        root,
        NATIVE_EVIDENCE_RELATIVE_PATH,
        "native Windows evidence",
    )
    native, native_sha = read_json_bound(native_path, "native Windows evidence")
    if native_sha != payload["nativeEvidenceSha256"]:
        fail("native Windows evidence bytes changed")
    validated_native, native_composite = _validate_native_wrapper_and_documents(
        native_path, visual_paths, proposal
    )
    if validated_native != native or native_composite != payload[
        "nativeEvidenceComposite"
    ]:
        fail("native evidence composite changed after scope finalization")
    candidate = native.get("candidateProvenance", {}).get("candidate", {})
    capture = native.get("captureSource", {})
    if not isinstance(candidate, dict) or not isinstance(capture, dict):
        fail("native Windows evidence lacks producer/capture authorities")
    registry_prepare = proposal.get("registryPrepare")
    if registry_prepare is not None:
        registry_sha = validate_registry_prepare_binding(
            registry_prepare,
            full_manifest_sha256=proposal["fullShelfManifestSha256"],
            compatibility_manifest_sha256=proposal[
                "fullShelfCompatibilityManifestSha256"
            ],
        )
        verify_registry_prepare_files(
            registry_prepare,
            root,
            publication_dir=root / PUBLICATION_DIRECTORY,
        )
        candidate_provenance = native.get("candidateProvenance")
        if (
            not isinstance(candidate_provenance, dict)
            or candidate.get("registryPrepareSha256") != registry_sha
            or candidate_provenance.get("registryPrepareSha256") != registry_sha
            or candidate_provenance.get("publicationScope", {}).get(
                "registryPrepareSha256"
            )
            != registry_sha
        ):
            fail(
                "native Windows evidence does not bind the exact Registry "
                "PREPARE transaction"
            )
    authenticode_sha = validate_native_authenticode(native, root, installer_rows)
    if authenticode_sha != payload["authenticodeVerificationSha256"]:
        fail("independent Authenticode verification bytes changed")
    disallowed = [
        require_actor(candidate.get("actor"), "native evidence candidate actor"),
        require_actor(capture.get("actor"), "native evidence capture actor"),
    ]
    approver = validate_approval(
        approval,
        proposal,
        proposal_sha256,
        authenticode_sha,
        disallowed,
    )
    if approver != approval_binding["approver"]:
        fail("final scope approver differs from the sealed approval bytes")
    native_scope = native.get("scopeApproval")
    if not isinstance(native_scope, dict) or any(
        native_scope.get(key) != value
        for key, value in {
            "approver": approver,
            "scopeDecisionSha256": proposal["scopeDecisionSha256"],
            "sha256": approval_binding["sha256"],
        }.items()
    ):
        fail("native Windows evidence does not bind the exact scope approval")
    delta_digests = {row["sha256"] for row in proposal["publicationDeltaTuples"]}
    if not delta_digests.issubset(sha_values(native)):
        fail("sealed native Windows evidence does not bind every Windows delta digest")

    bound_visuals = [
        read_json_bound(path, "Windows visual approval") for path in visual_paths
    ]
    if sorted(digest for _visual, digest in bound_visuals) != payload["visualApprovalSha256"]:
        fail("Windows visual approval bytes changed")
    for (visual, _visual_sha), installer in zip(
        bound_visuals, installer_rows, strict=True
    ):
        if visual.get("status") != "passed" or installer["sha256"] not in sha_values(
            visual
        ):
            fail("Windows visual approval does not bind the signed installer")

    signing_path = evidence_file(
        root, SIGNING_RECEIPT_RELATIVE_PATH, "Windows signing receipt"
    )
    signing_receipt, signing_sha = read_json_bound(
        signing_path, "Windows signing receipt"
    )
    if signing_sha != payload["signingReceiptSha256"]:
        fail("Windows signing receipt bytes changed")
    validate_signing_receipt_payload(
        signing_receipt,
        proposal["publicationDeltaTuples"],
        expected_version=proposal["release"]["version"],
    )
    retained_snapshot_root = exact_directory(
        root / "retained-full-source", "sealed incumbent full-shelf snapshot"
    )
    actual_snapshot_inventory = file_inventory(retained_snapshot_root)
    expected_snapshot_inventory = validate_inventory(
        proposal["incumbentSnapshot"]["inventory"],
        "incumbent snapshot inventory",
    )
    if actual_snapshot_inventory != expected_snapshot_inventory:
        fail("sealed incumbent full-shelf snapshot bytes or modes changed")


def verify_scope(args: argparse.Namespace) -> dict[str, Any]:
    scope_path = exact_file(args.scope, "publication scope receipt")
    payload, _scope_sha = read_json_bound(scope_path, "publication scope receipt")
    validate_proposal(payload)
    if payload.get("status") != "validated" or payload.get("approvalIndependent") is not True:
        fail("publication scope has no independent exact-decision approval")
    if (
        payload.get("registryFinalizeEligible") is not True
        or payload.get("publicationEligible") is not False
    ):
        fail(
            "approved publication scope must be Registry-finalize eligible while "
            "remaining publication-ineligible"
        )
    if payload.get("uploadAuthorized") is not False or payload.get("deployAuthorized") is not False:
        fail("publication scope improperly grants upload/deploy authority")
    proposal_arg = getattr(args, "proposal", None)
    proposal: dict[str, Any] | None = None
    proposal_path: Path | None = None
    if proposal_arg is not None:
        proposal_path = exact_file(proposal_arg, "publication scope proposal")
        proposal, _proposal_sha = read_json_bound(
            proposal_path, "publication scope proposal"
        )
        validate_proposal(proposal)
        mutable_fields = {
            "approvalIndependent",
            "authenticodeVerificationSha256",
            "nativeEvidenceComposite",
            "nativeEvidenceSha256",
            "publicationEligible",
            "registryFinalizeEligible",
            "status",
            "visualApprovalSha256",
        }
        for field, value in proposal.items():
            if field not in mutable_fields and payload.get(field) != value:
                fail(f"final publication scope changed proposed field: {field}")
    evidence_root = getattr(args, "evidence_root", None)
    if evidence_root is None or proposal is None or proposal_path is None:
        fail("final scope verification requires the exact sealed evidence root and proposal")
    verify_final_evidence(
        payload,
        proposal,
        _proposal_sha,
        evidence_root,
    )
    publication_dir = exact_directory(args.publication_dir, "publication shelf")
    manifest = publication_dir / CANONICAL_MANIFEST_NAME
    compatibility_manifest = publication_dir / COMPATIBILITY_MANIFEST_NAME
    public_manifest, manifest_sha = read_json_bound(
        manifest, "full publication shelf manifest"
    )
    _compatibility_payload, compatibility_sha = read_json_bound(
        compatibility_manifest, "full publication shelf compatibility manifest"
    )
    if manifest_sha != payload.get("fullShelfManifestSha256"):
        fail("full publication shelf manifest changed")
    if compatibility_sha != payload.get(
        "fullShelfCompatibilityManifestSha256"
    ):
        fail("full publication shelf compatibility manifest changed")
    public_version, public_channel = manifest_identity(
        public_manifest, "full publication shelf manifest"
    )
    if (public_version, public_channel) != (
        payload["release"]["version"],
        payload["release"]["channel"],
    ):
        fail("full publication shelf release identity changed")
    installer_bindings = [
        row
        for row in payload["postPublicationShelfTuples"]
        if row["artifactRole"] == "installer"
    ]
    post_by_key = {
        binding_key(row, "postPublicationShelfTuples"): row
        for row in payload["postPublicationShelfTuples"]
    }
    if payload.get("registryPrepare") is not None:
        public_rows_by_key: dict[tuple[str, str, str, str], dict[str, Any]] = {}
        for row in manifest_rows(public_manifest, "Registry publication manifest"):
            key = artifact_tuple_key(row, "installer")
            if key in public_rows_by_key:
                fail("Registry publication manifest repeats an installer tuple")
            public_rows_by_key[key] = row
        expected_installer_keys = {
            binding_key(row, "postPublicationShelfTuples")
            for row in installer_bindings
        }
        if set(public_rows_by_key) != expected_installer_keys:
            fail("Registry publication manifest tuple set differs from the shelf")
        for installer in installer_bindings:
            key = binding_key(installer, "postPublicationShelfTuples")
            row = public_rows_by_key[key]
            if (
                _resolve_alias(
                    row,
                    ("fileName", "name"),
                    "Registry publication installer file name",
                    portable_name,
                    required=True,
                )
                != installer["fileName"]
                or _resolve_alias(
                    row,
                    ("sha256", "artifactSha256", "digest"),
                    "Registry publication installer digest",
                    _digest_alias,
                    required=True,
                )
                != installer["sha256"]
                or _resolve_alias(
                    row,
                    ("sizeBytes", "artifactSizeBytes", "size"),
                    "Registry publication installer size",
                    require_positive_size,
                    required=True,
                )
                != installer["sizeBytes"]
            ):
                fail("Registry publication manifest installer binding changed")
            payload_binding = post_by_key.get((key[0], key[1], key[2], "payload"))
            if payload_binding is not None and (
                _resolve_alias(
                    row,
                    ("payloadFileName", "payloadName"),
                    "Registry publication payload file name",
                    portable_name,
                    required=True,
                )
                != payload_binding["fileName"]
                or _resolve_alias(
                    row,
                    ("payloadSha256", "payloadArtifactSha256", "payloadDigest"),
                    "Registry publication payload digest",
                    _digest_alias,
                    required=True,
                )
                != payload_binding["sha256"]
                or _resolve_alias(
                    row,
                    ("payloadSizeBytes", "payloadSize"),
                    "Registry publication payload size",
                    require_positive_size,
                    required=True,
                )
                != payload_binding["sizeBytes"]
            ):
                fail("Registry publication manifest payload binding changed")
    else:
        public_rows_by_sha = _manifest_row_by_sha(public_manifest)
        if set(public_rows_by_sha) != {
            row["manifestRowSha256"] for row in installer_bindings
        }:
            fail(
                "full publication manifest rows differ from the exact "
                "post-publication shelf"
            )
        for installer in installer_bindings:
            row = public_rows_by_sha[installer["manifestRowSha256"]]
            key = binding_key(installer, "postPublicationShelfTuples")
            if (
                row.get("fileName") != installer["fileName"]
                or row.get("sha256") != installer["sha256"]
                or row.get("sizeBytes") != installer["sizeBytes"]
            ):
                fail("full publication manifest installer binding changed")
            payload_binding = post_by_key.get((key[0], key[1], key[2], "payload"))
            if payload_binding is not None and (
                row.get("payloadFileName") != payload_binding["fileName"]
                or row.get("payloadSha256") != payload_binding["sha256"]
                or row.get("payloadSizeBytes") != payload_binding["sizeBytes"]
            ):
                fail("full publication manifest payload binding changed")
    publication_inventory = file_inventory(publication_dir)
    snapshot_inventory = validate_inventory(
        payload["incumbentSnapshot"]["inventory"],
        "incumbent snapshot inventory",
    )
    managed_paths = set(payload["incumbentSnapshot"]["managedPaths"])
    ancillary_inventory = [
        row for row in snapshot_inventory if row["path"] not in managed_paths
    ]
    logical_full_inventory = sorted(
        [*ancillary_inventory, *publication_inventory],
        key=lambda row: row["path"],
    )
    if logical_full_inventory != payload["fullShelfInventory"]:
        fail("full publication shelf inventory bytes or modes changed")
    if canonical_sha256(logical_full_inventory) != payload.get(
        "fullShelfInventorySha256"
    ):
        fail("full publication shelf inventory digest changed")
    public_files = publication_dir / "files"
    expected = {
        (row["fileName"], row["sha256"], row["sizeBytes"])
        for row in payload["postPublicationShelfTuples"]
    }
    actual = {
        (path.name, *file_digest_size(path))
        for path in public_files.iterdir()
        if path.is_file() and not path.is_symlink()
    }
    if actual != expected:
        fail("publication files are not the complete retained/Windows-delta shelf")
    non_published_bytes = {
        (row["fileName"], row["sha256"], row["sizeBytes"])
        for row in payload["nonPublishedEvidenceTuples"]
    }
    if non_published_bytes & actual:
        fail("fresh Linux evidence leaked into the publication shelf")
    return payload


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    prepare = commands.add_parser("prepare")
    prepare.add_argument("--build-manifest", type=Path, required=True)
    prepare.add_argument("--build-releases", type=Path, required=True)
    prepare.add_argument("--build-files-dir", type=Path, required=True)
    prepare.add_argument("--incumbent-manifest", type=Path, required=True)
    prepare.add_argument("--incumbent-releases", type=Path, required=True)
    prepare.add_argument("--incumbent-files-dir", type=Path, required=True)
    prepare.add_argument("--incumbent-shelf-dir", type=Path, required=True)
    prepare.add_argument("--incumbent-snapshot-dir", type=Path, required=True)
    prepare.add_argument("--signing-receipt", type=Path, required=True)
    prepare.add_argument("--consumer-commit", required=True)
    prepare.add_argument("--desktop-commit")
    prepare.add_argument("--registry-root", type=Path)
    prepare.add_argument("--registry-prepare-root", type=Path)
    prepare.add_argument(
        "--build-manifest-receipt-path", default=CANONICAL_MANIFEST_NAME
    )
    prepare.add_argument(
        "--incumbent-manifest-receipt-path",
        default=f"retained-source/{CANONICAL_MANIFEST_NAME}",
    )
    prepare.add_argument("--publication-dir", type=Path, required=True)
    prepare.add_argument("--output", type=Path, required=True)
    prepare.set_defaults(handler=prepare_scope)

    finalize = commands.add_parser("finalize")
    finalize.add_argument("--proposal", type=Path, required=True)
    finalize.add_argument("--approval", type=Path, required=True)
    finalize.add_argument("--approval-receipt-path", required=True)
    finalize.add_argument("--native-evidence", type=Path, required=True)
    finalize.add_argument("--visual-approval", action="append", type=Path, required=True)
    finalize.add_argument("--disallowed-actor", action="append", default=[])
    finalize.add_argument("--output", type=Path, required=True)
    finalize.set_defaults(handler=finalize_scope)

    verify = commands.add_parser("verify")
    verify.add_argument("--scope", type=Path, required=True)
    verify.add_argument("--publication-dir", type=Path, required=True)
    verify.add_argument("--proposal", type=Path)
    verify.add_argument("--evidence-root", type=Path)
    verify.set_defaults(handler=verify_scope)

    replay = commands.add_parser("replay-registry-prepare")
    replay.add_argument("--scope", type=Path, required=True)
    replay.add_argument("--evidence-root", type=Path, required=True)
    replay.add_argument("--registry-root", type=Path, required=True)
    replay.set_defaults(handler=replay_registry_prepare_command)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        payload = args.handler(args)
    except (OSError, ScopeError) as exc:
        print(f"preview-nightly-publication-scope:error: {exc}", file=sys.stderr)
        return 1
    print(
        "preview-nightly-publication-scope:"
        f"{args.command}:ok scope_sha256={canonical_sha256(payload)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
