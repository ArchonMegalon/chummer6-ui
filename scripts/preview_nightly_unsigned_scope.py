#!/usr/bin/env python3
"""Validate and bind the exact unsigned Windows preview publication shelf.

This v3 proposal is intentionally non-authoritative.  It binds exact Registry
PREPARE v2 Windows-only shelf bytes and build provenance, while leaving publication, upload,
deployment, and Registry finalize authority false.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
from pathlib import Path, PurePosixPath
from typing import Any
from urllib.parse import urlsplit


CONTRACT_NAME = "chummer6-ui.preview-nightly-unsigned-publication-scope"
CONTRACT_VERSION = 3
PROJECTION_PROFILE = "v3_unsigned_windows_fresh_delta"
PROPOSAL_FILE_NAME = "PREVIEW_NIGHTLY_UNSIGNED_SCOPE.proposed.json"
CANONICAL_MANIFEST_NAME = "RELEASE_CHANNEL.generated.json"
COMPATIBILITY_MANIFEST_NAME = "releases.json"
INSTALLER_NAME = "chummer-avalonia-win-x64-installer.exe"
PAYLOAD_NAME = "chummer-avalonia-win-x64-payload.zip"
PAYLOAD_SIDECAR_NAME = f"{PAYLOAD_NAME}.json"
DOWNLOAD_ROOT = "https://chummer.run/downloads/files"
GOVERNED_DOWNLOAD_ROOT = "/downloads/files"
SIGNATURE = {
    "policy": "preview_policy",
    "required": False,
    "status": "unsigned",
}
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
PROVENANCE_KEYS = (
    "nativeToolchainLock",
    "packagePlaneLock",
    "packagePlaneReceipt",
    "retainedManifest",
)
ROOT_KEYS = {
    "compatibilityManifest",
    "contractName",
    "contractVersion",
    "crossRunBitReproducible",
    "deployAuthorized",
    "freshDelta",
    "fullShelfInventory",
    "fullShelfInventorySha256",
    "incumbentInventorySha256",
    "platformScope",
    "provenance",
    "projectionProfile",
    "publicationAuthorized",
    "publicationManifest",
    "release",
    "retainedFromIncumbent",
    "signature",
    "sourceSha",
    "status",
    "uploadAuthorized",
}


class ScopeError(RuntimeError):
    """A fail-closed unsigned publication-scope error."""


def fail(message: str) -> None:
    raise ScopeError(message)


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        raw = path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
            fail(f"{label} is not canonical UTF-8 JSON")
        value = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda item: fail(
                f"{label} contains non-finite JSON number {item}"
            ),
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def canonical_bytes(value: object) -> bytes:
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")


def canonical_sha256(value: object) -> str:
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def payload_sidecar_contract(
    version: str, payload_sha: str, payload_size: int
) -> dict[str, object]:
    return {
        "contractName": "chummer6-ui.windows_bootstrap_payload",
        "downloadUrl": f"{DOWNLOAD_ROOT}/{PAYLOAD_NAME}",
        "fileName": PAYLOAD_NAME,
        "installerFileName": INSTALLER_NAME,
        "payloadAcquisitionMode": "download",
        "releaseVersion": version,
        "sha256": payload_sha,
        "sizeBytes": payload_size,
    }


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
            fail(f"expected one regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        fail(f"could not hash {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def regular_metadata(path: Path, label: str) -> os.stat_result:
    try:
        metadata = path.lstat()
    except OSError as exc:
        fail(f"could not inspect {label}: {exc}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        fail(f"{label} must be one regular non-link file")
    return metadata


def portable_path(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    parsed = PurePosixPath(value)
    if (
        parsed.is_absolute()
        or parsed.as_posix() != value
        or any(part in {"", ".", ".."} for part in parsed.parts)
        or "\\" in value
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        fail(f"{label} is not a canonical portable path")
    return value


def portable_name(value: object, label: str) -> str:
    path = portable_path(value, label)
    if PurePosixPath(path).name != path:
        fail(f"{label} is not a portable file name")
    return path


def file_inventory(root: Path) -> list[dict[str, object]]:
    if root.is_symlink() or not root.is_dir():
        fail(f"inventory root must be one physical directory: {root}")
    rows: list[dict[str, object]] = []
    seen: set[str] = set()
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = portable_path(
                path.relative_to(root).as_posix(), "shelf inventory path"
            )
            collision = relative.casefold()
            if collision in seen:
                fail(f"shelf repeats or case-collides at {relative}")
            seen.add(collision)
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"shelf contains a symbolic link: {relative}")
            if stat.S_ISDIR(metadata.st_mode):
                continue
            if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
                fail(f"shelf contains a special or hard-linked entry: {relative}")
            rows.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": relative,
                    "sha256": sha256_file(path),
                    "sizeBytes": metadata.st_size,
                }
            )
    return sorted(rows, key=lambda row: str(row["path"]))


def directory_modes(root: Path) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for current, directories, _files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted(directories):
            path = current_path / name
            metadata = path.lstat()
            if path.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
                fail(f"shelf contains a linked or special directory: {path}")
            rows.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": path.relative_to(root).as_posix(),
                }
            )
    return sorted(rows, key=lambda row: str(row["path"]))


def validate_inventory(value: object, label: str) -> list[dict[str, object]]:
    if not isinstance(value, list) or not value:
        fail(f"{label} must be a non-empty inventory")
    rows: list[dict[str, object]] = []
    seen: set[str] = set()
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != {
            "mode",
            "path",
            "sha256",
            "sizeBytes",
        }:
            fail(f"{label} row fields differ")
        path = portable_path(raw["path"], f"{label} path")
        if path.casefold() in seen:
            fail(f"{label} repeats or case-collides at {path}")
        seen.add(path.casefold())
        if type(raw["mode"]) is not int or not 0 <= raw["mode"] <= 0o777:
            fail(f"{label} mode is invalid")
        if type(raw["sizeBytes"]) is not int or raw["sizeBytes"] < 0:
            fail(f"{label} size is invalid")
        if not isinstance(raw["sha256"], str) or SHA256_RE.fullmatch(raw["sha256"]) is None:
            fail(f"{label} SHA-256 is invalid")
        rows.append(dict(raw))
    if rows != sorted(rows, key=lambda row: str(row["path"])):
        fail(f"{label} is not sorted canonically")
    return rows


def validate_retained(value: object) -> list[dict[str, object]]:
    if not isinstance(value, list):
        fail("retainedFromIncumbent must be an array")
    result: list[dict[str, object]] = []
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != {
            "mode",
            "path",
            "retentionKind",
            "sha256",
            "sizeBytes",
        }:
            fail("retainedFromIncumbent row fields differ")
        inventory = validate_inventory(
            [{key: raw[key] for key in ("mode", "path", "sha256", "sizeBytes")}],
            "retainedFromIncumbent",
        )[0]
        kind = raw["retentionKind"]
        if kind not in {"ancillary", "managed_artifact"}:
            fail("retainedFromIncumbent retentionKind is invalid")
        result.append({**inventory, "retentionKind": kind})
    if result != sorted(result, key=lambda row: str(row["path"])):
        fail("retainedFromIncumbent is not sorted canonically")
    return result


def binding(path: Path, receipt_path: str | None = None) -> dict[str, object]:
    metadata = regular_metadata(path, "bound evidence")
    result: dict[str, object] = {
        "sha256": sha256_file(path),
        "sizeBytes": metadata.st_size,
    }
    if receipt_path is not None:
        result["path"] = receipt_path
    return result


def validate_binding(
    value: object, label: str, *, expected_path: str | None = None
) -> dict[str, object]:
    expected_keys = {"sha256", "sizeBytes"}
    if expected_path is not None:
        expected_keys.add("path")
    if not isinstance(value, dict) or set(value) != expected_keys:
        fail(f"{label} binding fields differ")
    if expected_path is not None and value.get("path") != expected_path:
        fail(f"{label} binding path differs")
    if not isinstance(value.get("sha256"), str) or SHA256_RE.fullmatch(value["sha256"]) is None:
        fail(f"{label} binding SHA-256 is invalid")
    if type(value.get("sizeBytes")) is not int or value["sizeBytes"] < 1:
        fail(f"{label} binding size is invalid")
    return dict(value)


def require_release(value: object, version: str) -> None:
    if value != {"channel": "preview", "version": version}:
        fail("release binding must be exact preview/version")


def manifest_rows(value: dict[str, Any], key: str, label: str) -> list[dict[str, Any]]:
    rows = value.get(key)
    if not isinstance(rows, list) or not rows:
        fail(f"{label} {key} must be non-empty")
    if any(not isinstance(row, dict) for row in rows):
        fail(f"{label} has a non-object row")
    return rows


def row_platform(row: dict[str, Any]) -> str:
    value = row.get("platformId") or row.get("platform")
    if not isinstance(value, str):
        fail("manifest row lacks a platform identity")
    lowered = value.strip().lower()
    if lowered.startswith("windows") or " windows " in f" {lowered} ":
        return "windows"
    if lowered.startswith("linux") or " linux " in f" {lowered} ":
        return "linux"
    if lowered.startswith(("macos", "osx")) or " macos " in f" {lowered} ":
        return "macos"
    return lowered


def row_name(row: dict[str, Any], label: str) -> str:
    return portable_name(row.get("fileName") or row.get("name"), label)


def validate_unsigned_pe(path: Path) -> None:
    metadata = regular_metadata(path, "fresh Windows installer")
    if metadata.st_size < 256:
        fail("fresh Windows installer is too small to be PE")
    with path.open("rb") as handle:
        dos = handle.read(64)
        if dos[:2] != b"MZ":
            fail("fresh Windows installer lacks MZ")
        offset = int.from_bytes(dos[60:64], "little")
        if offset < 64 or offset > metadata.st_size - 160:
            fail("fresh Windows installer PE offset is invalid")
        handle.seek(offset)
        if handle.read(4) != b"PE\x00\x00":
            fail("fresh Windows installer lacks PE signature")
        coff = handle.read(20)
        size = int.from_bytes(coff[16:18], "little")
        optional = handle.read(size)
    if len(optional) != size:
        fail("fresh Windows installer optional header is truncated")
    magic = int.from_bytes(optional[:2], "little") if len(optional) >= 2 else 0
    directory = 96 if magic == 0x10B else 112 if magic == 0x20B else -1
    security = directory + 32
    if directory < 0 or len(optional) < security + 8:
        fail("fresh Windows installer optional header is unsupported")
    if any(optional[security : security + 8]):
        fail("fresh Windows installer is not unsigned")


def validate_manifest_identity(
    manifest: dict[str, Any], version: str, *, rows_key: str
) -> list[dict[str, Any]]:
    for key in ("channel", "channelId"):
        if key in manifest and manifest[key] != "preview":
            fail(f"manifest {key} differs from preview")
    for key in ("version", "releaseVersion"):
        if manifest.get(key) != version:
            fail(f"manifest {key} differs from expected version")
    if manifest.get("platformScope") != "windows_only":
        fail("manifest platformScope differs")
    if manifest.get("crossRunBitReproducible") is not False:
        fail("manifest crossRunBitReproducible must be false")
    if manifest.get("previewPolicy") != "preview_policy":
        fail("manifest previewPolicy differs")
    if manifest.get("signature") != SIGNATURE:
        fail("manifest signature policy differs")
    for field in (
        "publicationAuthorized",
        "uploadAuthorized",
        "deployAuthorized",
    ):
        if manifest.get(field) is not False:
            fail(f"manifest {field} must be explicit false")
    return manifest_rows(manifest, rows_key, "publication manifest")


def validate_provenance_documents(
    args: argparse.Namespace, version: str, source_sha: str
) -> dict[str, dict[str, object]]:
    lock = read_json(args.package_plane_lock, "package-plane lock")
    if lock.get("contractName") != "chummer6-ui.fresh-package-plane-lock" or lock.get("contractVersion") != 8:
        fail("package-plane lock contract differs")
    receipt = read_json(args.package_plane_receipt, "package-plane receipt")
    if (
        receipt.get("contractName") != "chummer6-ui.fresh-package-plane-verification"
        or receipt.get("contractVersion") != 8
        or receipt.get("status") != "passed"
        or receipt.get("consumerCommit") != source_sha
        or receipt.get("mode") != "integration"
        or receipt.get("localCompatibilityTree") is not False
        or receipt.get("packageCacheWasFresh") is not True
        or receipt.get("stubPackagesAllowed") is not False
        or receipt.get("packageSources") != ["same-run-local-feed"]
    ):
        fail("package-plane receipt authority differs")
    pointer = receipt.get("retainedWindowsBundle")
    if (
        not isinstance(pointer, dict)
        or pointer.get("contractName")
        != "chummer6-ui.retained-windows-publish-closure-pointer"
        or pointer.get("contractVersion") != 2
        or pointer.get("status") != "passed"
        or pointer.get("consumerCommit") != source_sha
        or pointer.get("release") != {"channel": "preview", "version": version}
        or pointer.get("atomicallyRetained") is not True
        or pointer.get("authority") is not False
        or pointer.get("manifestIsAuthoritative") is not True
    ):
        fail("package-plane retained bundle pointer differs")
    retained = read_json(args.retained_manifest, "retained Windows manifest")
    if (
        retained.get("contractName")
        != "chummer6-ui.retained-windows-publish-closure"
        or retained.get("contractVersion") != 2
        or retained.get("status") != "passed"
        or retained.get("consumerCommit") != source_sha
        or retained.get("release") != {"channel": "preview", "version": version}
        or retained.get("atomicallyRetained") is not True
        or retained.get("authoritative") is not True
        or retained.get("deterministicRepacking") is not False
        or not isinstance(retained.get("releaseEligibility"), dict)
        or retained["releaseEligibility"].get("eligible") is not False
        or not isinstance(retained.get("publish"), dict)
        or retained["publish"].get("status") != "passed"
        or retained["publish"].get("releaseChannel") != "preview"
        or retained["publish"].get("releaseVersion") != version
    ):
        fail("retained Windows manifest authority differs")
    pointer_manifest = pointer.get("manifest")
    retained_binding = binding(args.retained_manifest)
    if (
        not isinstance(pointer_manifest, dict)
        or pointer_manifest.get("sha256") != retained_binding["sha256"]
        or pointer_manifest.get("sizeBytes") != retained_binding["sizeBytes"]
    ):
        fail("retained bundle pointer does not bind retained manifest bytes")
    for owner, label in (
        (receipt.get("consumerPackagePlaneLock"), "receipt"),
        (retained.get("packagePlaneLock"), "retained manifest"),
    ):
        lock_binding = binding(args.package_plane_lock)
        if (
            not isinstance(owner, dict)
            or owner.get("sha256") != lock_binding["sha256"]
            or owner.get("sizeBytes") != lock_binding["sizeBytes"]
        ):
            fail(f"{label} does not bind package-plane lock bytes")
    native = read_json(args.native_toolchain_lock, "native toolchain lock")
    if (
        native.get("contract_name")
        != "chummer6-ui.windows_native_bootstrap_toolchain_lock"
        or native.get("schema_version") != 1
        or native.get("platform") != {"architecture": "amd64", "os": "linux"}
        or not isinstance(native.get("container_image"), dict)
        or not isinstance(native.get("debian_snapshot"), dict)
        or not isinstance(native.get("packages"), list)
        or not native["packages"]
    ):
        fail("native toolchain lock contract differs")
    return {
        "nativeToolchainLock": binding(args.native_toolchain_lock),
        "packagePlaneLock": binding(args.package_plane_lock),
        "packagePlaneReceipt": binding(args.package_plane_receipt),
        "retainedManifest": retained_binding,
    }


def build_proposal(args: argparse.Namespace) -> dict[str, Any]:
    version = args.expected_version
    if VERSION_RE.fullmatch(version) is None or ".." in version:
        fail("expected version is not portable")
    source_sha = args.source_sha
    if COMMIT_RE.fullmatch(source_sha) is None:
        fail("sourceSha must be an exact lowercase commit")
    publication = args.publication_root.resolve(strict=True)
    incumbent = args.incumbent_root.resolve(strict=True)
    publication_inventory = file_inventory(publication)
    incumbent_inventory = file_inventory(incumbent)
    if directory_modes(publication) != directory_modes(incumbent):
        fail("publication directory set or modes differ from incumbent shelf")
    publication_by_path = {str(row["path"]): row for row in publication_inventory}
    incumbent_by_path = {str(row["path"]): row for row in incumbent_inventory}
    canonical = read_json(
        publication / CANONICAL_MANIFEST_NAME, "publication canonical manifest"
    )
    compatibility = read_json(
        publication / COMPATIBILITY_MANIFEST_NAME,
        "publication compatibility manifest",
    )
    canonical_profile = canonical.get("projectionProfile")
    compatibility_profile = compatibility.get("projectionProfile")
    if any(
        profile not in (None, PROJECTION_PROFILE)
        for profile in (canonical_profile, compatibility_profile)
    ):
        fail("publication manifest projection profile is unsupported")
    canonical_projected = canonical_profile == PROJECTION_PROFILE
    compatibility_projected = compatibility_profile == PROJECTION_PROFILE
    if canonical_projected != compatibility_projected:
        fail("publication manifest projection profiles disagree")
    expected_download_root = (
        GOVERNED_DOWNLOAD_ROOT if canonical_projected else DOWNLOAD_ROOT
    )
    artifacts = validate_manifest_identity(canonical, version, rows_key="artifacts")
    downloads = validate_manifest_identity(
        compatibility, version, rows_key="downloads"
    )
    windows = [
        row
        for row in artifacts
        if row_platform(row) == "windows"
        and (row.get("head") or row.get("headId")) == "avalonia"
        and row.get("rid") == "win-x64"
        and row_name(row, "Windows fileName") == INSTALLER_NAME
    ]
    if len(windows) != 1 or any(row_platform(row) == "windows" for row in artifacts if row not in windows):
        fail("publication must contain exactly one avalonia/windows/win-x64 installer")
    installer_row = windows[0]
    if installer_row.get("signature") != SIGNATURE:
        fail("Windows installer row signature policy differs")
    if installer_row.get("payloadFileName") != PAYLOAD_NAME:
        fail("Windows installer payload binding differs")
    installer_path = publication / "files" / INSTALLER_NAME
    payload_path = publication / "files" / PAYLOAD_NAME
    payload_sidecar_path = publication / "files" / PAYLOAD_SIDECAR_NAME
    validate_unsigned_pe(installer_path)
    installer_metadata = regular_metadata(installer_path, "fresh installer")
    payload_metadata = regular_metadata(payload_path, "fresh payload")
    payload_sidecar_metadata = regular_metadata(
        payload_sidecar_path, "fresh payload metadata sidecar"
    )
    installer_sha = sha256_file(installer_path)
    payload_sha = sha256_file(payload_path)
    payload_sidecar_sha = sha256_file(payload_sidecar_path)
    expected_url = f"{expected_download_root}/{INSTALLER_NAME}"
    expected_payload_url = f"{expected_download_root}/{PAYLOAD_NAME}"
    if (
        installer_row.get("sha256") != installer_sha
        or installer_row.get("sizeBytes") != installer_metadata.st_size
        or installer_row.get("payloadSha256") != payload_sha
        or installer_row.get("payloadSizeBytes") != payload_metadata.st_size
        or installer_row.get("downloadUrl") != expected_url
        or installer_row.get("payloadDownloadUrl") != expected_payload_url
        or installer_row.get("payloadAcquisitionMode") != "download"
        or installer_row.get("installerMode") != "bootstrap"
    ):
        fail("Windows installer/payload manifest binding differs from exact bytes")
    if read_json(
        payload_sidecar_path, "fresh payload metadata sidecar"
    ) != payload_sidecar_contract(version, payload_sha, payload_metadata.st_size):
        fail("Windows payload metadata sidecar differs from exact payload bytes")
    matching_downloads = [
        row for row in downloads if row_name(row, "Windows compatibility fileName") == INSTALLER_NAME
    ]
    if len(matching_downloads) != 1:
        fail("compatibility manifest lacks the exact Windows installer")
    windows_download = matching_downloads[0]
    compatibility_url = windows_download.get("url")
    compatibility_download_url = windows_download.get("downloadUrl")
    installer_url_is_exact = (
        compatibility_url == expected_url
        and compatibility_download_url in (None, expected_url)
    ) or (
        compatibility_url is None
        and compatibility_download_url == expected_url
    )
    if (
        row_platform(windows_download) != "windows"
        or windows_download.get("sha256") != installer_sha
        or windows_download.get("sizeBytes") != installer_metadata.st_size
        or windows_download.get("payloadFileName") != PAYLOAD_NAME
        or windows_download.get("payloadSha256") != payload_sha
        or windows_download.get("payloadSizeBytes") != payload_metadata.st_size
        or not installer_url_is_exact
        or windows_download.get("payloadDownloadUrl") != expected_payload_url
        or windows_download.get("signature") != SIGNATURE
    ):
        fail("Windows compatibility projection differs")

    incumbent_manifest = read_json(
        incumbent / CANONICAL_MANIFEST_NAME, "incumbent canonical manifest"
    )
    incumbent_artifacts = manifest_rows(
        incumbent_manifest, "artifacts", "incumbent manifest"
    )
    old_windows_paths: set[str] = set()
    managed_non_windows: set[str] = set()
    for row in incumbent_artifacts:
        names = [row_name(row, "incumbent artifact fileName")]
        if row.get("payloadFileName") is not None:
            names.append(portable_name(row["payloadFileName"], "incumbent payloadFileName"))
        target = old_windows_paths if row_platform(row) == "windows" else managed_non_windows
        target.update(f"files/{name}" for name in names)
    old_windows_paths.add(f"files/{PAYLOAD_SIDECAR_NAME}")
    for row in artifacts:
        name = row_name(row, "publication artifact fileName")
        path = f"files/{name}"
        inventory = publication_by_path.get(path)
        if inventory is None or inventory["sha256"] != row.get("sha256") or inventory["sizeBytes"] != row.get("sizeBytes"):
            fail(f"publication manifest does not bind exact artifact bytes: {name}")
        payload_name = row.get("payloadFileName")
        if payload_name is not None:
            payload_name = portable_name(payload_name, "publication payloadFileName")
            payload_inventory = publication_by_path.get(f"files/{payload_name}")
            if (
                payload_inventory is None
                or payload_inventory["sha256"] != row.get("payloadSha256")
                or payload_inventory["sizeBytes"] != row.get("payloadSizeBytes")
            ):
                fail(f"publication manifest does not bind exact payload bytes: {payload_name}")
    expected_paths = (
        set(incumbent_by_path)
        - old_windows_paths
        | {
            f"files/{INSTALLER_NAME}",
            f"files/{PAYLOAD_NAME}",
            f"files/{PAYLOAD_SIDECAR_NAME}",
        }
    )
    if set(publication_by_path) != expected_paths:
        fail("publication shelf has missing or unexplained paths")
    for path in managed_non_windows:
        if publication_by_path.get(path) != incumbent_by_path.get(path):
            fail(f"non-Windows managed artifact changed: {path}")
    retained: list[dict[str, object]] = []
    for path in sorted(expected_paths - {
        CANONICAL_MANIFEST_NAME,
        COMPATIBILITY_MANIFEST_NAME,
        f"files/{INSTALLER_NAME}",
        f"files/{PAYLOAD_NAME}",
        f"files/{PAYLOAD_SIDECAR_NAME}",
    }):
        final = publication_by_path[path]
        if final != incumbent_by_path.get(path):
            fail(f"incumbent ancillary byte or mode changed: {path}")
        retained.append(
            {
                **final,
                "retentionKind": (
                    "managed_artifact" if path in managed_non_windows else "ancillary"
                ),
            }
        )
    provenance = validate_provenance_documents(args, version, source_sha)
    proposal = {
        "compatibilityManifest": binding(
            publication / COMPATIBILITY_MANIFEST_NAME,
            COMPATIBILITY_MANIFEST_NAME,
        ),
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "crossRunBitReproducible": False,
        "deployAuthorized": False,
        "freshDelta": [
            {
                "artifactRole": "installer",
                "fileName": INSTALLER_NAME,
                "head": "avalonia",
                "mode": publication_by_path[f"files/{INSTALLER_NAME}"]["mode"],
                "path": f"files/{INSTALLER_NAME}",
                "platform": "windows",
                "rid": "win-x64",
                "sha256": installer_sha,
                "sizeBytes": installer_metadata.st_size,
            },
            {
                "artifactRole": "bootstrap_payload",
                "fileName": PAYLOAD_NAME,
                "head": "avalonia",
                "mode": publication_by_path[f"files/{PAYLOAD_NAME}"]["mode"],
                "path": f"files/{PAYLOAD_NAME}",
                "platform": "windows",
                "rid": "win-x64",
                "sha256": payload_sha,
                "sizeBytes": payload_metadata.st_size,
            },
            {
                "artifactRole": "bootstrap_payload_sidecar",
                "fileName": PAYLOAD_SIDECAR_NAME,
                "head": "avalonia",
                "mode": publication_by_path[f"files/{PAYLOAD_SIDECAR_NAME}"]["mode"],
                "path": f"files/{PAYLOAD_SIDECAR_NAME}",
                "platform": "windows",
                "rid": "win-x64",
                "sha256": payload_sidecar_sha,
                "sizeBytes": payload_sidecar_metadata.st_size,
            },
        ],
        "fullShelfInventory": publication_inventory,
        "fullShelfInventorySha256": canonical_sha256(publication_inventory),
        "incumbentInventorySha256": canonical_sha256(incumbent_inventory),
        "platformScope": "windows_only",
        "provenance": provenance,
        "projectionProfile": PROJECTION_PROFILE,
        "publicationAuthorized": False,
        "publicationManifest": binding(
            publication / CANONICAL_MANIFEST_NAME, CANONICAL_MANIFEST_NAME
        ),
        "release": {"channel": "preview", "version": version},
        "retainedFromIncumbent": retained,
        "signature": dict(SIGNATURE),
        "sourceSha": source_sha,
        "status": "prepared",
        "uploadAuthorized": False,
    }
    validate_proposal(proposal)
    return proposal


def validate_proposal(value: object) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != ROOT_KEYS:
        fail("scope proposal root fields differ")
    if (
        value.get("contractName") != CONTRACT_NAME
        or value.get("contractVersion") != CONTRACT_VERSION
        or value.get("status") != "prepared"
        or value.get("platformScope") != "windows_only"
        or value.get("crossRunBitReproducible") is not False
        or value.get("signature") != SIGNATURE
        or value.get("publicationAuthorized") is not False
        or value.get("uploadAuthorized") is not False
        or value.get("deployAuthorized") is not False
        or value.get("projectionProfile") != PROJECTION_PROFILE
    ):
        fail("scope proposal posture differs")
    source_sha = value.get("sourceSha")
    if not isinstance(source_sha, str) or COMMIT_RE.fullmatch(source_sha) is None:
        fail("scope proposal sourceSha differs")
    release = value.get("release")
    if not isinstance(release, dict) or set(release) != {"channel", "version"}:
        fail("scope proposal release fields differ")
    version = release.get("version")
    if (
        release.get("channel") != "preview"
        or not isinstance(version, str)
        or VERSION_RE.fullmatch(version) is None
        or ".." in version
    ):
        fail("scope proposal release differs")
    validate_binding(
        value.get("publicationManifest"),
        "publicationManifest",
        expected_path=CANONICAL_MANIFEST_NAME,
    )
    validate_binding(
        value.get("compatibilityManifest"),
        "compatibilityManifest",
        expected_path=COMPATIBILITY_MANIFEST_NAME,
    )
    provenance = value.get("provenance")
    if not isinstance(provenance, dict) or set(provenance) != set(PROVENANCE_KEYS):
        fail("scope proposal provenance fields differ")
    for key in PROVENANCE_KEYS:
        validate_binding(provenance[key], f"provenance.{key}")
    inventory = validate_inventory(value.get("fullShelfInventory"), "fullShelfInventory")
    if value.get("fullShelfInventorySha256") != canonical_sha256(inventory):
        fail("full shelf inventory digest differs")
    incumbent_sha = value.get("incumbentInventorySha256")
    if not isinstance(incumbent_sha, str) or SHA256_RE.fullmatch(incumbent_sha) is None:
        fail("incumbent inventory digest differs")
    retained = validate_retained(value.get("retainedFromIncumbent"))
    inventory_by_path = {row["path"]: row for row in inventory}
    for row in retained:
        exact = {key: row[key] for key in ("mode", "path", "sha256", "sizeBytes")}
        if inventory_by_path.get(row["path"]) != exact:
            fail("retained row differs from full shelf inventory")
    fresh = value.get("freshDelta")
    if not isinstance(fresh, list) or len(fresh) != 3:
        fail("freshDelta must contain exact installer/payload/metadata rows")
    expected_roles = (
        ("installer", INSTALLER_NAME),
        ("bootstrap_payload", PAYLOAD_NAME),
        ("bootstrap_payload_sidecar", PAYLOAD_SIDECAR_NAME),
    )
    for raw, (role, name) in zip(fresh, expected_roles, strict=True):
        if not isinstance(raw, dict) or set(raw) != {
            "artifactRole",
            "fileName",
            "head",
            "mode",
            "path",
            "platform",
            "rid",
            "sha256",
            "sizeBytes",
        }:
            fail("freshDelta row fields differ")
        if (
            raw.get("artifactRole") != role
            or raw.get("fileName") != name
            or raw.get("head") != "avalonia"
            or raw.get("platform") != "windows"
            or raw.get("rid") != "win-x64"
            or raw.get("path") != f"files/{name}"
        ):
            fail("freshDelta identity differs")
        exact = {key: raw[key] for key in ("mode", "path", "sha256", "sizeBytes")}
        if inventory_by_path.get(raw["path"]) != exact:
            fail("freshDelta differs from full shelf inventory")
    return dict(value)


def write_scope(path: Path, value: dict[str, Any]) -> None:
    if not path.is_absolute():
        fail("scope output must be absolute")
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() or path.is_symlink():
        fail("scope output must be absent")
    data = (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")
    descriptor = os.open(
        path,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written < 1:
                fail("scope output write made no progress")
            view = view[written:]
        os.fchmod(descriptor, 0o600)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    parent = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(parent)
    finally:
        os.close(parent)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("prepare", "verify"):
        command = commands.add_parser(name)
        command.add_argument("--publication-root", required=True, type=Path)
        command.add_argument("--incumbent-root", required=True, type=Path)
        command.add_argument("--expected-version", required=True)
        command.add_argument("--source-sha", required=True)
        command.add_argument("--package-plane-lock", required=True, type=Path)
        command.add_argument("--package-plane-receipt", required=True, type=Path)
        command.add_argument("--retained-manifest", required=True, type=Path)
        command.add_argument("--native-toolchain-lock", required=True, type=Path)
        if name == "prepare":
            command.add_argument("--output", required=True, type=Path)
        else:
            command.add_argument("--scope", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        expected = build_proposal(args)
        if args.command == "prepare":
            write_scope(args.output, expected)
            output = args.output
        else:
            observed = read_json(args.scope, "unsigned publication scope")
            validate_proposal(observed)
            if observed != expected:
                fail("unsigned publication scope replay differs")
            output = args.scope
    except (ScopeError, OSError, ValueError) as exc:
        print(f"unsigned-publication-scope:error: {exc}", file=os.sys.stderr)
        return 2
    print(f"scope={output}")
    print(f"scope_sha256={sha256_file(output)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
