#!/usr/bin/env python3
"""Compose the Windows-only unsigned preview shelf without publishing it.

The materializer replaces only the Avalonia ``win-x64`` installer, payload,
and payload metadata sidecar.
Every incumbent non-Windows managed artifact and every ancillary file is copied
byte-for-byte with its mode.  The resulting manifests say exactly what this
lane is: preview, Windows-only, unsigned, and not cross-run bit reproducible.
"""

from __future__ import annotations

import argparse
import ctypes
import errno
import hashlib
import json
import os
import re
import shutil
import stat
import tempfile
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any
from urllib.parse import urlsplit


CANONICAL_MANIFEST = "RELEASE_CHANNEL.generated.json"
COMPATIBILITY_MANIFEST = "releases.json"
INSTALLER_NAME = "chummer-avalonia-win-x64-installer.exe"
PAYLOAD_NAME = "chummer-avalonia-win-x64-payload.zip"
PAYLOAD_SIDECAR_NAME = f"{PAYLOAD_NAME}.json"
ARTIFACT_ID = "avalonia-win-x64-installer"
CHANNEL = "preview"
PLATFORM_SCOPE = "windows_only"
PREVIEW_POLICY = "preview_policy"
DOWNLOAD_ROOT = "https://chummer.run/downloads/files"
PROMOTED_DESKTOP_HEADS = ("avalonia",)
REGISTRY_PROJECTION_IDENTITY_KEYS = (
    "codeDeployCurrentShelfAuthority",
    "projectionProfile",
    "projectionStage",
    "registryCommit",
    "registry_commit",
)
OPTIONAL_AUTHORITY_POSTURE_FIELDS = (
    "codeDeploymentAuthority",
    "releaseUploadAuthority",
)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
TIMESTAMP_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)


class StageError(RuntimeError):
    """A fail-closed unsigned-preview stage contract error."""


def fail(message: str) -> None:
    raise StageError(message)


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
        payload = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda value: fail(
                f"{label} contains non-finite JSON number {value}"
            ),
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def canonical_json_bytes(payload: object) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            fail(f"expected a regular file: {path}")
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


def require_regular(path: Path, label: str) -> os.stat_result:
    try:
        metadata = path.lstat()
    except OSError as exc:
        fail(f"could not inspect {label}: {exc}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        fail(f"{label} must be one regular non-link file")
    return metadata


def portable_name(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    portable = PurePosixPath(value)
    if (
        value in {"", ".", ".."}
        or portable.name != value
        or "/" in value
        or "\\" in value
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        fail(f"{label} is not a portable file name")
    return value


def require_version(value: str) -> str:
    if VERSION_RE.fullmatch(value) is None or ".." in value:
        fail("preview version is not portable")
    return value


def require_timestamp(value: str) -> str:
    if TIMESTAMP_RE.fullmatch(value) is None:
        fail("published timestamp must be UTC with whole-second precision")
    try:
        datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        fail("published timestamp is not a real UTC timestamp")
    return value


def file_inventory(root: Path) -> list[dict[str, object]]:
    if root.is_symlink() or not root.is_dir():
        fail(f"inventory root must be one physical directory: {root}")
    rows: list[dict[str, object]] = []
    casefolded: set[str] = set()
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            if relative.casefold() in casefolded:
                fail(f"shelf has a case-colliding path: {relative}")
            casefolded.add(relative.casefold())
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"shelf contains a symbolic link: {relative}")
            if name in {"", ".", ".."} or "\\" in name:
                fail(f"shelf contains a non-portable path: {relative}")
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


def directory_mode_inventory(root: Path) -> list[dict[str, object]]:
    result: list[dict[str, object]] = []
    for current, directories, _files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted(directories):
            path = current_path / name
            metadata = path.lstat()
            if path.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
                fail(f"shelf contains a linked or special directory: {path}")
            result.append(
                {
                    "mode": stat.S_IMODE(metadata.st_mode),
                    "path": path.relative_to(root).as_posix(),
                }
            )
    return sorted(result, key=lambda row: str(row["path"]))


def copy_tree_exact(source: Path, destination: Path) -> None:
    before = file_inventory(source)
    directory_modes_before = directory_mode_inventory(source)
    if destination.exists() or destination.is_symlink():
        fail("stage output must be absent")
    shutil.copytree(source, destination, symlinks=False, copy_function=shutil.copy2)
    after = file_inventory(destination)
    directory_modes_after = directory_mode_inventory(destination)
    if before != after or directory_modes_before != directory_modes_after:
        fail("incumbent shelf changed while it was copied")


def directory_identity(metadata: os.stat_result) -> tuple[int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_uid,
        stat.S_IMODE(metadata.st_mode),
    )


def open_safe_parent(path: Path, label: str) -> tuple[int, os.stat_result]:
    path = path.absolute()
    if not path.is_absolute() or path != Path(os.path.normpath(str(path))):
        fail(f"{label} path must be canonical and absolute")
    flags = os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0)
    descriptor = -1
    try:
        descriptor = os.open("/", flags)
        for component in path.parts[1:]:
            if component in {"", ".", ".."}:
                fail(f"{label} has a non-canonical component")
            next_descriptor = os.open(component, flags, dir_fd=descriptor)
            os.close(descriptor)
            descriptor = next_descriptor
        metadata = os.fstat(descriptor)
    except OSError as exc:
        if descriptor >= 0:
            os.close(descriptor)
        fail(f"could not open physical {label}: {exc}")
    if (
        not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid != os.geteuid()
        or metadata.st_mode & (stat.S_IWGRP | stat.S_IWOTH)
    ):
        os.close(descriptor)
        fail(f"{label} must be one physical owner-controlled directory")
    return descriptor, metadata


def require_safe_parent(path: Path, label: str) -> os.stat_result:
    descriptor, metadata = open_safe_parent(path, label)
    os.close(descriptor)
    return metadata


def atomic_rename_noreplace(source: Path, target: Path) -> None:
    """Atomically publish one directory while refusing a racing destination."""

    source = source.absolute()
    target = target.absolute()
    if source.parent != target.parent:
        fail("exclusive directory publication requires one parent filesystem")
    parent_descriptor, parent_before = open_safe_parent(
        source.parent, "publication parent"
    )
    try:
        source_metadata = os.stat(
            source.name, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if not stat.S_ISDIR(source_metadata.st_mode):
            fail("exclusive directory publication source is not physical")
        try:
            os.stat(target.name, dir_fd=parent_descriptor, follow_symlinks=False)
        except FileNotFoundError:
            pass
        except OSError as exc:
            fail(f"could not inspect publication destination: {exc}")
        else:
            fail("publication destination already exists")
        libc = ctypes.CDLL(None, use_errno=True)
        renameat2 = getattr(libc, "renameat2", None)
        if renameat2 is None:
            fail("renameat2 no-replace support is unavailable")
        renameat2.argtypes = [
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_uint,
        ]
        renameat2.restype = ctypes.c_int
        result = renameat2(
            parent_descriptor,
            os.fsencode(source.name),
            parent_descriptor,
            os.fsencode(target.name),
            1,
        )
        if result != 0:
            error = ctypes.get_errno()
            if error in {errno.EEXIST, errno.ENOTEMPTY}:
                fail("publication destination appeared during exclusive commit")
            fail(f"exclusive directory publication failed: {os.strerror(error)}")
        target_metadata = os.stat(
            target.name, dir_fd=parent_descriptor, follow_symlinks=False
        )
        if directory_identity(target_metadata) != directory_identity(source_metadata):
            fail("exclusive directory publication target identity changed")
        os.fsync(parent_descriptor)
        reopened, parent_after = open_safe_parent(
            source.parent, "publication parent"
        )
        try:
            if directory_identity(parent_before) != directory_identity(parent_after):
                fail("publication parent identity changed during exclusive commit")
        finally:
            os.close(reopened)
    finally:
        os.close(parent_descriptor)


def write_new(path: Path, payload: object, mode: int) -> None:
    data = canonical_json_bytes(payload)
    descriptor = os.open(
        path,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        mode,
    )
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written < 1:
                fail(f"write made no progress: {path}")
            view = view[written:]
        os.fchmod(descriptor, mode)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def replace_json(path: Path, payload: object, mode: int) -> None:
    path.unlink()
    write_new(path, payload, mode)


def manifest_rows(manifest: dict[str, Any], label: str) -> list[dict[str, Any]]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail(f"{label} artifacts must be an array")
    result: list[dict[str, Any]] = []
    seen: set[tuple[str, str, str, str]] = set()
    for row in rows:
        if not isinstance(row, dict):
            fail(f"{label} has a non-object artifact")
        head = row.get("head") or row.get("headId")
        platform = row.get("platformId") or row.get("platform")
        rid = row.get("rid")
        artifact_id = row.get("artifactId") or row.get("id")
        if not all(isinstance(item, str) and item for item in (head, platform, rid, artifact_id)):
            fail(f"{label} has an ambiguous desktop tuple")
        identity = (head, platform, rid, artifact_id)
        if identity in seen:
            fail(f"{label} repeats desktop tuple {identity!r}")
        seen.add(identity)
        portable_name(row.get("fileName") or row.get("name"), f"{label} fileName")
        result.append(row)
    return result


def compatibility_rows(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    rows = manifest.get("downloads")
    if not isinstance(rows, list):
        fail("incumbent compatibility downloads must be an array")
    result: list[dict[str, Any]] = []
    seen: set[str] = set()
    for row in rows:
        if not isinstance(row, dict):
            fail("incumbent compatibility manifest has a non-object row")
        name = portable_name(row.get("fileName") or row.get("name"), "compatibility fileName")
        if name.casefold() in seen:
            fail(f"incumbent compatibility repeats or case-collides at {name}")
        seen.add(name.casefold())
        result.append(row)
    return result


def row_platform(row: dict[str, Any]) -> str:
    value = row.get("platformId") or row.get("platform")
    if not isinstance(value, str):
        fail("desktop row has no exact platform identity")
    return value.lower()


def require_promoted_windows_desktop_identity(
    rows: list[dict[str, Any]], label: str
) -> None:
    for row in rows:
        if row_platform(row) != "windows":
            continue
        head = row.get("head") or row.get("headId")
        if head not in PROMOTED_DESKTOP_HEADS or row.get("rid") != "win-x64":
            fail(
                f"{label} contains a Windows artifact outside the promoted "
                "avalonia/win-x64 tuple"
            )


def managed_names(rows: list[dict[str, Any]]) -> set[str]:
    result: set[str] = set()
    for row in rows:
        result.add(portable_name(row.get("fileName") or row.get("name"), "managed fileName"))
        payload = row.get("payloadFileName") or row.get("payloadName")
        if payload is not None:
            result.add(portable_name(payload, "managed payloadFileName"))
    return result


def verify_unsigned_pe(path: Path) -> None:
    metadata = require_regular(path, "Windows installer")
    if metadata.st_size < 256:
        fail("Windows installer is too small to be a PE image")
    with path.open("rb") as handle:
        dos = handle.read(64)
        if dos[:2] != b"MZ":
            fail("Windows installer lacks the PE DOS signature")
        pe_offset = int.from_bytes(dos[60:64], "little")
        if pe_offset < 64 or pe_offset > metadata.st_size - 160:
            fail("Windows installer has an invalid PE header offset")
        handle.seek(pe_offset)
        if handle.read(4) != b"PE\x00\x00":
            fail("Windows installer lacks the PE signature")
        coff = handle.read(20)
        optional_size = int.from_bytes(coff[16:18], "little")
        optional = handle.read(optional_size)
    if len(optional) != optional_size or optional_size < 136:
        fail("Windows installer has a truncated PE optional header")
    magic = int.from_bytes(optional[:2], "little")
    directory_offset = 96 if magic == 0x10B else 112 if magic == 0x20B else -1
    security_offset = directory_offset + 4 * 8
    if directory_offset < 0 or len(optional) < security_offset + 8:
        fail("Windows installer PE optional header is unsupported")
    certificate_file_offset = int.from_bytes(optional[security_offset : security_offset + 4], "little")
    certificate_size = int.from_bytes(optional[security_offset + 4 : security_offset + 8], "little")
    if certificate_file_offset != 0 or certificate_size != 0:
        fail("Windows preview installer has an Authenticode certificate table")


def installer_row(
    version: str,
    published_at: str,
    installer_sha: str,
    installer_size: int,
    payload_sha: str,
    payload_size: int,
    download_root: str,
) -> dict[str, Any]:
    base = download_root.rstrip("/")
    return {
        "arch": "x64",
        "artifactId": ARTIFACT_ID,
        "channel": CHANNEL,
        "channelId": CHANNEL,
        "compatibilityReason": None,
        "compatibilityState": "compatible",
        "crossRunBitReproducible": False,
        "downloadUrl": f"{base}/{INSTALLER_NAME}",
        "fileName": INSTALLER_NAME,
        "generatedAt": published_at,
        "generated_at": published_at,
        "head": "avalonia",
        "id": ARTIFACT_ID,
        "installAccessClass": "open_public",
        "installerMode": "bootstrap",
        "kind": "installer",
        "payloadAcquisitionMode": "download",
        "payloadDownloadUrl": f"{base}/{PAYLOAD_NAME}",
        "payloadFileName": PAYLOAD_NAME,
        "payloadSha256": payload_sha,
        "payloadSizeBytes": payload_size,
        "platform": "windows",
        "platformLabel": "Avalonia Desktop Windows X64 Installer",
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "releaseVersion": version,
        "rid": "win-x64",
        "sha256": installer_sha,
        "signature": {
            "policy": PREVIEW_POLICY,
            "required": False,
            "status": "unsigned",
        },
        "sizeBytes": installer_size,
        "version": version,
    }


def payload_sidecar(
    version: str,
    payload_sha: str,
    payload_size: int,
    download_root: str,
) -> dict[str, Any]:
    """Return the canonical metadata contract for the fresh bootstrap payload."""

    base = download_root.rstrip("/")
    return {
        "contractName": "chummer6-ui.windows_bootstrap_payload",
        "downloadUrl": f"{base}/{PAYLOAD_NAME}",
        "fileName": PAYLOAD_NAME,
        "installerFileName": INSTALLER_NAME,
        "payloadAcquisitionMode": "download",
        "releaseVersion": version,
        "sha256": payload_sha,
        "sizeBytes": payload_size,
    }


def compatibility_row(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "arch": row["arch"],
        "artifactId": row["artifactId"],
        "channel": CHANNEL,
        "channelId": CHANNEL,
        "compatibilityReason": row["compatibilityReason"],
        "compatibilityState": row["compatibilityState"],
        "crossRunBitReproducible": False,
        "fileName": row["fileName"],
        "flavor": row["kind"],
        "format": "exe",
        "head": row["head"],
        "id": row["artifactId"],
        "installAccessClass": row["installAccessClass"],
        "installerMode": row["installerMode"],
        "kind": row["kind"],
        "payloadAcquisitionMode": row["payloadAcquisitionMode"],
        "payloadDownloadUrl": row["payloadDownloadUrl"],
        "payloadFileName": row["payloadFileName"],
        "payloadSha256": row["payloadSha256"],
        "payloadSizeBytes": row["payloadSizeBytes"],
        "platform": row["platformLabel"],
        "platformId": "windows",
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "releaseVersion": row["releaseVersion"],
        "rid": row["rid"],
        "sha256": row["sha256"],
        "signature": row["signature"],
        "sizeBytes": row["sizeBytes"],
        "url": row["downloadUrl"],
        "version": row["version"],
    }


def apply_release_identity(
    manifest: dict[str, Any], version: str, published_at: str
) -> dict[str, Any]:
    result = dict(manifest)
    for key in REGISTRY_PROJECTION_IDENTITY_KEYS:
        result.pop(key, None)
    # Legacy compatibility projections materialized absent optional booleans as
    # null. Preserve every non-null value so Registry remains the fail-closed
    # authority validator instead of having UI coerce an unsafe posture.
    for key in OPTIONAL_AUTHORITY_POSTURE_FIELDS:
        if result.get(key) is None:
            result.pop(key, None)
    result.update(
        {
            "channel": CHANNEL,
            "channelId": CHANNEL,
            "crossRunBitReproducible": False,
            "generatedAt": published_at,
            "generated_at": published_at,
            "platformScope": PLATFORM_SCOPE,
            "previewPolicy": PREVIEW_POLICY,
            "publicationAuthorized": False,
            "publishedAt": published_at,
            "releaseVersion": version,
            "signature": {
                "policy": PREVIEW_POLICY,
                "required": False,
                "status": "unsigned",
            },
            "uploadAuthorized": False,
            "deployAuthorized": False,
            "version": version,
        }
    )
    return result


def reconcile_registry_counts(
    manifest: dict[str, Any], rows_key: str
) -> None:
    rows = manifest.get(rows_key)
    if not isinstance(rows, list):
        fail(f"{rows_key} must be an array before Registry count reconciliation")
    artifact_count = len(rows)
    compatible_count = sum(
        1
        for row in rows
        if isinstance(row, dict)
        and str(row.get("compatibilityState") or "").strip().lower() == "compatible"
    )
    registry = manifest.get("registryBoundaryCoverage")
    if registry is not None:
        if not isinstance(registry, dict):
            fail("registryBoundaryCoverage must be an object")
        persistence = registry.get("persistence")
        compatibility = registry.get("compatibility")
        if persistence is not None:
            if not isinstance(persistence, dict):
                fail("registryBoundaryCoverage.persistence must be an object")
            persistence["artifactCount"] = artifact_count
        if compatibility is not None:
            if not isinstance(compatibility, dict):
                fail("registryBoundaryCoverage.compatibility must be an object")
            compatibility["compatibleArtifactCount"] = compatible_count
            compatibility["unknownArtifactCount"] = artifact_count - compatible_count
    coverage = manifest.get("desktopTupleCoverage")
    if coverage is not None:
        if not isinstance(coverage, dict):
            fail("desktopTupleCoverage must be an object")
        installer_rows = [
            row
            for row in rows
            if isinstance(row, dict)
            and str(row.get("kind") or row.get("flavor") or "").lower()
            == "installer"
        ]
        for key in (
            "artifactCount",
            "desktopArtifactCount",
            "installerArtifactCount",
            "installerTupleCount",
            "promotedInstallerTupleCount",
        ):
            if key in coverage:
                coverage[key] = (
                    artifact_count
                    if key in {"artifactCount", "desktopArtifactCount"}
                    else len(installer_rows)
                )


def require_download_root(value: str) -> str:
    parsed = urlsplit(value)
    if (
        value != DOWNLOAD_ROOT
        or parsed.scheme != "https"
        or parsed.netloc != "chummer.run"
        or parsed.path != "/downloads/files"
        or parsed.query
        or parsed.fragment
        or parsed.username is not None
        or parsed.password is not None
    ):
        fail(f"download root must be exactly {DOWNLOAD_ROOT}")
    return value


def materialize(args: argparse.Namespace) -> dict[str, object]:
    version = require_version(args.expected_version)
    published_at = require_timestamp(args.published_at)
    if COMMIT_RE.fullmatch(args.source_sha) is None:
        fail("source SHA must be an exact lowercase commit")
    download_root = require_download_root(args.download_root)
    incumbent = args.incumbent_root.resolve(strict=True)
    build = args.build_root.resolve(strict=True)
    output = args.output.absolute()
    if output.exists() or output.is_symlink():
        fail("stage output must be absent")
    if output == incumbent or incumbent in output.parents or output in incumbent.parents:
        fail("stage output and incumbent shelf must be disjoint")
    incumbent_inventory = file_inventory(incumbent)
    canonical_path = incumbent / CANONICAL_MANIFEST
    compatibility_path = incumbent / COMPATIBILITY_MANIFEST
    canonical_mode = stat.S_IMODE(require_regular(canonical_path, "incumbent canonical manifest").st_mode)
    compatibility_mode = stat.S_IMODE(require_regular(compatibility_path, "incumbent compatibility manifest").st_mode)
    incumbent_manifest = read_json(canonical_path, "incumbent canonical manifest")
    incumbent_compatibility = read_json(compatibility_path, "incumbent compatibility manifest")
    canonical_rows = manifest_rows(incumbent_manifest, "incumbent canonical manifest")
    download_rows = compatibility_rows(incumbent_compatibility)
    incumbent_windows = [row for row in canonical_rows if row_platform(row) == "windows"]
    if any(
        (row.get("head") or row.get("headId"), row.get("rid"))
        != ("avalonia", "win-x64")
        for row in incumbent_windows
    ):
        fail("incumbent shelf has a Windows tuple outside avalonia/win-x64")
    if len(incumbent_windows) > 1:
        fail("incumbent shelf has more than one avalonia/win-x64 tuple")
    incumbent_managed = managed_names(canonical_rows)
    incumbent_file_rows = {
        str(row["path"]): row
        for row in incumbent_inventory
        if str(row["path"]).startswith("files/")
    }
    for name in incumbent_managed:
        if f"files/{name}" not in incumbent_file_rows:
            fail(f"incumbent manifest references missing bytes: {name}")

    installer = build / "files" / INSTALLER_NAME
    payload = build / "files" / PAYLOAD_NAME
    installer_metadata = require_regular(installer, "fresh Windows installer")
    payload_metadata = require_regular(payload, "fresh Windows payload")
    if payload_metadata.st_size < 1:
        fail("fresh Windows payload is empty")
    verify_unsigned_pe(installer)
    installer_sha = sha256_file(installer)
    payload_sha = sha256_file(payload)
    fresh_payload_sidecar = payload_sidecar(
        version,
        payload_sha,
        payload_metadata.st_size,
        download_root,
    )
    fresh = installer_row(
        version,
        published_at,
        installer_sha,
        installer_metadata.st_size,
        payload_sha,
        payload_metadata.st_size,
        download_root,
    )
    final_canonical_rows = [
        row for row in canonical_rows if row_platform(row) != "windows"
    ] + [fresh]
    windows_compat_names = managed_names(incumbent_windows)
    final_download_rows = [
        row
        for row in download_rows
        if portable_name(row.get("fileName") or row.get("name"), "compatibility fileName")
        not in windows_compat_names
        and str(row.get("platformId") or row.get("platform", "")).lower()
        not in {"windows", "avalonia desktop windows x64 installer"}
    ] + [compatibility_row(fresh)]
    require_promoted_windows_desktop_identity(
        final_canonical_rows, "proposed canonical manifest"
    )
    require_promoted_windows_desktop_identity(
        final_download_rows, "proposed compatibility manifest"
    )
    public_manifest = apply_release_identity(incumbent_manifest, version, published_at)
    public_manifest["artifacts"] = final_canonical_rows
    coverage = dict(public_manifest.get("desktopTupleCoverage") or {})
    coverage["requiredDesktopHeads"] = list(PROMOTED_DESKTOP_HEADS)
    coverage["requiredDesktopPlatforms"] = sorted(
        {row_platform(row) for row in final_canonical_rows}
    )
    public_manifest["desktopTupleCoverage"] = coverage
    public_compatibility = apply_release_identity(
        incumbent_compatibility, version, published_at
    )
    public_compatibility["downloads"] = final_download_rows
    compatibility_coverage = dict(
        public_compatibility.get("desktopTupleCoverage") or {}
    )
    compatibility_coverage["requiredDesktopHeads"] = list(
        PROMOTED_DESKTOP_HEADS
    )
    compatibility_coverage["requiredDesktopPlatforms"] = coverage[
        "requiredDesktopPlatforms"
    ]
    public_compatibility["desktopTupleCoverage"] = compatibility_coverage
    reconcile_registry_counts(public_manifest, "artifacts")
    reconcile_registry_counts(public_compatibility, "downloads")

    output.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(
        tempfile.mkdtemp(prefix=f".{output.name}.compose-", dir=output.parent)
    )
    try:
        staging.rmdir()
        copy_tree_exact(incumbent, staging)
        staging_files = staging / "files"
        for row in incumbent_windows:
            for name in managed_names([row]):
                target = staging_files / name
                if target.is_file() and not target.is_symlink():
                    target.unlink()
        for source, name in ((installer, INSTALLER_NAME), (payload, PAYLOAD_NAME)):
            target = staging_files / name
            if target.exists() or target.is_symlink():
                target.unlink()
            shutil.copy2(source, target, follow_symlinks=False)
            os.chmod(target, 0o644, follow_symlinks=False)
        sidecar_target = staging_files / PAYLOAD_SIDECAR_NAME
        if sidecar_target.exists() or sidecar_target.is_symlink():
            sidecar_target.unlink()
        write_new(sidecar_target, fresh_payload_sidecar, 0o644)
        payload_sidecar_sha = sha256_file(sidecar_target)
        replace_json(staging / CANONICAL_MANIFEST, public_manifest, canonical_mode)
        replace_json(
            staging / COMPATIBILITY_MANIFEST,
            public_compatibility,
            compatibility_mode,
        )
        final_inventory = file_inventory(staging)
        atomic_rename_noreplace(staging, output)
        staging = Path()
    finally:
        if staging != Path() and staging.exists():
            shutil.rmtree(staging)
    return {
        "channel": CHANNEL,
        "crossRunBitReproducible": False,
        "fullShelfInventorySha256": hashlib.sha256(
            canonical_json_bytes(final_inventory)
        ).hexdigest(),
        "installerSha256": installer_sha,
        "output": str(output),
        "payloadSha256": payload_sha,
        "payloadSidecarSha256": payload_sidecar_sha,
        "platformScope": PLATFORM_SCOPE,
        "previewPolicy": PREVIEW_POLICY,
        "signature": {
            "policy": PREVIEW_POLICY,
            "required": False,
            "status": "unsigned",
        },
        "sourceSha": args.source_sha,
        "version": version,
    }


def publish_directory(args: argparse.Namespace) -> dict[str, object]:
    source = args.source.absolute()
    output = args.output.absolute()
    if source.parent != output.parent:
        fail("candidate staging and output must share one parent")
    atomic_rename_noreplace(source, output)
    inventory = file_inventory(output)
    return {
        "fileCount": len(inventory),
        "inventorySha256": hashlib.sha256(canonical_json_bytes(inventory)).hexdigest(),
        "output": str(output),
        "status": "published_to_private_stage",
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    compose = subparsers.add_parser("materialize")
    compose.add_argument("--incumbent-root", required=True, type=Path)
    compose.add_argument("--build-root", required=True, type=Path)
    compose.add_argument("--expected-version", required=True)
    compose.add_argument("--published-at", required=True)
    compose.add_argument("--source-sha", required=True)
    compose.add_argument(
        "--download-root",
        default=DOWNLOAD_ROOT,
    )
    compose.add_argument("--output", required=True, type=Path)
    publish = subparsers.add_parser("publish-directory")
    publish.add_argument("--source", required=True, type=Path)
    publish.add_argument("--output", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        result = (
            materialize(args)
            if args.command == "materialize"
            else publish_directory(args)
        )
    except (StageError, OSError, ValueError) as exc:
        print(f"unsigned-windows-preview-stage:error: {exc}", file=os.sys.stderr)
        return 2
    for key in sorted(result):
        value = result[key]
        if isinstance(value, (dict, list)):
            value = json.dumps(value, sort_keys=True, separators=(",", ":"))
        print(f"{key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
