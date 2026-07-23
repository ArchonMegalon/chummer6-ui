#!/usr/bin/env python3
"""Export the exact read-only unsigned Windows preview composition subset."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import sys
import tempfile
from pathlib import Path, PurePosixPath
from types import ModuleType
from typing import Any


COMPOSITION_MODULE_NAME = (
    "chummer6_ui_preview_nightly_unsigned_composition_contract"
)


def _load_composition() -> ModuleType:
    existing = sys.modules.get(COMPOSITION_MODULE_NAME)
    if existing is not None:
        if not isinstance(existing, ModuleType):
            raise RuntimeError("preloaded unsigned composition contract is malformed")
        return existing
    path = Path(__file__).resolve().with_name(
        "preview_nightly_unsigned_composition.py"
    )
    spec = importlib.util.spec_from_file_location(COMPOSITION_MODULE_NAME, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load unsigned composition contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


COMPOSITION = _load_composition()
PUBLICATION_SCOPE = COMPOSITION.SCOPE
MANIFEST_PATH = f"publication/{PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME}"
COMPATIBILITY_PATH = f"publication/{PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME}"
INSTALLER_PATH = f"publication/files/{PUBLICATION_SCOPE.INSTALLER_NAME}"
PAYLOAD_PATH = f"publication/files/{PUBLICATION_SCOPE.PAYLOAD_NAME}"
PAYLOAD_SIDECAR_PATH = (
    f"publication/files/{PUBLICATION_SCOPE.PAYLOAD_SIDECAR_NAME}"
)
COMPOSITION_PATH = COMPOSITION.PROPOSAL_FILE_NAME
PACKAGE_LOCK_PATH = "provenance/config/package-plane.lock.json"
PACKAGE_RECEIPT_PATH = "provenance/UI_FRESH_PACKAGE_PLANE.generated.json"
RETAINED_MANIFEST_PATH = (
    "provenance/retained-windows-publish-closure/manifest.json"
)
NATIVE_LOCK_PATH = (
    "provenance/config/windows-native-bootstrap-toolchain.lock.json"
)
CONTENT_PATHS = (
    COMPOSITION_PATH,
    MANIFEST_PATH,
    COMPATIBILITY_PATH,
    INSTALLER_PATH,
    PAYLOAD_PATH,
    PAYLOAD_SIDECAR_PATH,
    PACKAGE_LOCK_PATH,
    PACKAGE_RECEIPT_PATH,
    RETAINED_MANIFEST_PATH,
    NATIVE_LOCK_PATH,
)
CONTENT_INVENTORY_PATH = (
    "PREVIEW_NIGHTLY_UNSIGNED_CANDIDATE_CONTENT_INVENTORY.generated.json"
)
EXPORT_RECEIPT_PATH = "PREVIEW_NIGHTLY_UNSIGNED_CANDIDATE_EXPORT.generated.json"
OUTPUT_PATHS = (*CONTENT_PATHS, CONTENT_INVENTORY_PATH, EXPORT_RECEIPT_PATH)
CONTENT_INVENTORY_CONTRACT = (
    "chummer6-ui.preview-nightly-unsigned-candidate-content-inventory"
)
EXPORT_CONTRACT = "chummer6-ui.preview-nightly-unsigned-candidate-export"
CONTRACT_VERSION = 1
PRODUCER_WORKFLOW = (
    ".github/workflows/unsigned-windows-preview-nightly-candidate-export.yml"
)
PRODUCER_REF = "refs/heads/main"
SOURCE_REPOSITORY = "ArchonMegalon/chummer6-ui"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
LOGIN_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?|github-actions\[bot\])$"
)
NONCE_RE = re.compile(r"^[a-z0-9]{12,64}$")


class ExportError(RuntimeError):
    """A fail-closed candidate export error."""


def fail(message: str) -> None:
    raise ExportError(message)


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
            parse_constant=lambda item: fail(
                f"{label} contains non-finite JSON number {item}"
            ),
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
            fail(f"candidate entry is not one regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        fail(f"could not hash candidate entry {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def file_size(path: Path) -> int:
    try:
        metadata = path.lstat()
    except OSError as exc:
        fail(f"could not inspect candidate entry {path}: {exc}")
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        fail(f"candidate entry is not one regular file: {path}")
    return metadata.st_size


def portable_path(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    parsed = PurePosixPath(value)
    if (
        parsed.is_absolute()
        or parsed.as_posix() != value
        or any(part in {"", ".", ".."} for part in parsed.parts)
        or "\\" in value
    ):
        fail(f"{label} is not a canonical portable path")
    return value


def expected_directories(content_paths: tuple[str, ...]) -> set[str]:
    directories: set[str] = set()
    for relative in content_paths:
        parent = PurePosixPath(relative).parent
        while parent != PurePosixPath("."):
            directories.add(parent.as_posix())
            parent = parent.parent
    return directories


CONTENT_DIRECTORIES = tuple(
    sorted(expected_directories(CONTENT_PATHS), key=lambda item: (len(PurePosixPath(item).parts), item))
)


def require_exact_tree(root: Path, expected: tuple[str, ...], label: str) -> None:
    if root.is_symlink() or not root.is_dir():
        fail(f"{label} must be one physical directory")
    expected_files = set(expected)
    expected_dirs = expected_directories(expected)
    actual_files: set[str] = set()
    actual_dirs: set[str] = set()
    casefolded: set[str] = set()
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = portable_path(path.relative_to(root).as_posix(), label)
            if relative.casefold() in casefolded:
                fail(f"{label} repeats or case-collides at {relative}")
            casefolded.add(relative.casefold())
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"{label} contains a symbolic link: {relative}")
            if stat.S_ISDIR(metadata.st_mode):
                actual_dirs.add(relative)
            elif stat.S_ISREG(metadata.st_mode) and metadata.st_nlink == 1:
                actual_files.add(relative)
            else:
                fail(f"{label} contains a special or hard-linked entry: {relative}")
    if actual_files != expected_files or actual_dirs != expected_dirs:
        fail(f"{label} differs from the exact candidate boundary")


def content_rows(root: Path, paths: tuple[str, ...]) -> list[dict[str, object]]:
    rows = []
    for relative in paths:
        path = root / relative
        size = file_size(path)
        rows.append(
            {
                "path": relative,
                "sha256": sha256_file(path),
                "sizeBytes": size,
            }
        )
    return sorted(rows, key=lambda row: str(row["path"]))


def windows_only_content_paths(_root: Path) -> tuple[str, ...]:
    return CONTENT_PATHS


def validate_binding(
    path: Path, value: object, label: str, *, expected_path: str | None = None
) -> None:
    if not isinstance(value, dict):
        fail(f"{label} binding must be an object")
    expected_keys = {"sha256", "sizeBytes"}
    if expected_path is not None:
        expected_keys.add("path")
    if set(value) != expected_keys:
        fail(f"{label} binding fields differ")
    if expected_path is not None and value.get("path") != expected_path:
        fail(f"{label} binding path differs")
    if value.get("sha256") != sha256_file(path) or value.get("sizeBytes") != file_size(path):
        fail(f"{label} binding differs from exact bytes")


def validate_unsigned_pe(path: Path) -> None:
    size = file_size(path)
    if size < 256:
        fail("Windows installer is too small to be PE")
    data = path.read_bytes()
    if data[:2] != b"MZ":
        fail("Windows installer lacks MZ")
    offset = int.from_bytes(data[60:64], "little")
    if offset < 64 or data[offset : offset + 4] != b"PE\x00\x00":
        fail("Windows installer lacks PE signature")
    optional_size = int.from_bytes(data[offset + 20 : offset + 22], "little")
    optional = data[offset + 24 : offset + 24 + optional_size]
    magic = int.from_bytes(optional[:2], "little") if len(optional) >= 2 else 0
    directory = 96 if magic == 0x10B else 112 if magic == 0x20B else -1
    security = directory + 32
    if directory < 0 or len(optional) < security + 8:
        fail("Windows installer optional header is unsupported")
    if any(optional[security : security + 8]):
        fail("Windows installer is not unsigned")


def validate_candidate_root(
    root: Path,
    expected_version: str,
    expected_manifest_sha256: str,
    source_commit: str,
    *,
    exact_paths: tuple[str, ...] = CONTENT_PATHS,
) -> dict[str, Any]:
    require_exact_tree(root, exact_paths, "unsigned candidate subset")
    if VERSION_RE.fullmatch(expected_version) is None or ".." in expected_version:
        fail("expected version is not portable")
    if SHA256_RE.fullmatch(expected_manifest_sha256) is None:
        fail("expected manifest SHA-256 is invalid")
    if COMMIT_RE.fullmatch(source_commit) is None:
        fail("expected source commit is invalid")
    manifest_path = root / MANIFEST_PATH
    if sha256_file(manifest_path) != expected_manifest_sha256:
        fail("candidate manifest SHA-256 differs")
    manifest = read_json(manifest_path, "candidate manifest")
    if (
        manifest.get("version") != expected_version
        or manifest.get("releaseVersion") != expected_version
        or manifest.get("platformScope") != "windows_only"
        or manifest.get("crossRunBitReproducible") is not False
        or manifest.get("signature") != PUBLICATION_SCOPE.SIGNATURE
    ):
        fail("candidate manifest unsigned preview posture differs")
    composition_path = root / COMPOSITION_PATH
    proposal = COMPOSITION.validate_request(
        read_json(composition_path, "unsigned composition request")
    )
    if proposal.get("sourceSha") != source_commit:
        fail("composition source SHA differs from workflow authority")
    if proposal.get("release") != {"channel": "preview", "version": expected_version}:
        fail("composition release differs from workflow authority")
    validate_binding(
        manifest_path,
        proposal["proposedCanonicalManifest"],
        "proposed canonical manifest",
        expected_path=PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME,
    )
    validate_binding(
        root / COMPATIBILITY_PATH,
        proposal["proposedCompatibilityManifest"],
        "proposed compatibility manifest",
        expected_path=PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME,
    )
    evidence = {
        "packagePlaneLock": PACKAGE_LOCK_PATH,
        "packagePlaneReceipt": PACKAGE_RECEIPT_PATH,
        "retainedManifest": RETAINED_MANIFEST_PATH,
        "nativeToolchainLock": NATIVE_LOCK_PATH,
    }
    for key, relative in evidence.items():
        validate_binding(
            root / relative,
            proposal["provenance"][key],
            f"provenance.{key}",
            expected_path=relative,
        )
    fresh_by_role = {row["artifactRole"]: row for row in proposal["freshDelta"]}
    for role, relative in (
        ("installer", INSTALLER_PATH),
        ("bootstrap_payload", PAYLOAD_PATH),
        ("bootstrap_payload_sidecar", PAYLOAD_SIDECAR_PATH),
    ):
        row = fresh_by_role.get(role)
        path = root / relative
        if (
            not isinstance(row, dict)
            or row.get("sha256") != sha256_file(path)
            or row.get("sizeBytes") != file_size(path)
        ):
            fail(f"fresh {role} binding differs from exported bytes")
    validate_unsigned_pe(root / INSTALLER_PATH)
    artifacts = manifest.get("artifacts")
    windows = [
        row
        for row in artifacts or []
        if isinstance(row, dict)
        and row.get("head") == "avalonia"
        and row.get("rid") == "win-x64"
        and (row.get("platformId") or row.get("platform")) == "windows"
    ]
    if len(windows) != 1:
        fail("candidate manifest lacks exact Windows tuple")
    row = windows[0]
    manifest_row_sha256 = PUBLICATION_SCOPE.canonical_sha256(row)
    if (
        row.get("fileName") != PUBLICATION_SCOPE.INSTALLER_NAME
        or row.get("sha256") != sha256_file(root / INSTALLER_PATH)
        or row.get("sizeBytes") != file_size(root / INSTALLER_PATH)
        or row.get("payloadFileName") != PUBLICATION_SCOPE.PAYLOAD_NAME
        or row.get("payloadSha256") != sha256_file(root / PAYLOAD_PATH)
        or row.get("payloadSizeBytes") != file_size(root / PAYLOAD_PATH)
        or row.get("signature") != PUBLICATION_SCOPE.SIGNATURE
        or any(
            fresh.get("manifestRowSha256") != manifest_row_sha256
            for fresh in proposal["freshDelta"]
        )
    ):
        fail("candidate manifest Windows tuple differs from exact bytes")
    sidecar = read_json(root / PAYLOAD_SIDECAR_PATH, "payload metadata sidecar")
    if sidecar != PUBLICATION_SCOPE.payload_sidecar_contract(
        expected_version,
        sha256_file(root / PAYLOAD_PATH),
        file_size(root / PAYLOAD_PATH),
    ):
        fail("candidate payload metadata sidecar differs from exact payload bytes")
    return proposal


def validate_export_root(
    root: Path,
    expected_version: str,
    expected_manifest_sha256: str,
    source_commit: str,
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    """Replay the complete ephemeral export boundary before direct import."""

    proposal = validate_candidate_root(
        root,
        expected_version,
        expected_manifest_sha256,
        source_commit,
        exact_paths=OUTPUT_PATHS,
    )
    inventory = read_json(
        root / CONTENT_INVENTORY_PATH, "candidate content inventory"
    )
    if set(inventory) != {
        "contractName",
        "contractVersion",
        "crossRunBitReproducible",
        "files",
        "platformScope",
        "release",
        "signature",
        "sourceSha",
    } or (
        inventory.get("contractName") != CONTENT_INVENTORY_CONTRACT
        or inventory.get("contractVersion") != CONTRACT_VERSION
        or inventory.get("crossRunBitReproducible") is not False
        or inventory.get("platformScope") != "windows_only"
        or inventory.get("release")
        != {"channel": "preview", "version": expected_version}
        or inventory.get("signature") != PUBLICATION_SCOPE.SIGNATURE
        or inventory.get("sourceSha") != source_commit
    ):
        fail("candidate content inventory posture differs")
    exact_content = content_rows(root, CONTENT_PATHS)
    if inventory.get("files") != exact_content:
        fail("candidate content inventory differs from exact exported bytes")
    receipt = read_json(root / EXPORT_RECEIPT_PATH, "candidate export receipt")
    if set(receipt) != {
        "compositionRequest",
        "contractName",
        "contractVersion",
        "crossRunBitReproducible",
        "deployAuthorized",
        "exportedContent",
        "githubArtifactTransport",
        "inventory",
        "platformScope",
        "publicationAuthorized",
        "release",
        "runnerNonce",
        "signature",
        "source",
        "status",
        "uiUploadAuthorized",
        "uploadAuthorized",
    } or (
        receipt.get("contractName") != EXPORT_CONTRACT
        or receipt.get("contractVersion") != CONTRACT_VERSION
        or receipt.get("status") != "exported"
        or receipt.get("crossRunBitReproducible") is not False
        or receipt.get("githubArtifactTransport") != "ephemeral_candidate_only"
        or receipt.get("platformScope") != "windows_only"
        or receipt.get("release")
        != {"channel": "preview", "version": expected_version}
        or receipt.get("signature") != PUBLICATION_SCOPE.SIGNATURE
        or receipt.get("publicationAuthorized") is not False
        or receipt.get("uploadAuthorized") is not False
        or receipt.get("uiUploadAuthorized") is not False
        or receipt.get("deployAuthorized") is not False
        or receipt.get("exportedContent") != exact_content
    ):
        fail("candidate export receipt posture differs")
    validate_binding(
        root / CONTENT_INVENTORY_PATH,
        receipt.get("inventory"),
        "candidate content inventory receipt",
        expected_path=CONTENT_INVENTORY_PATH,
    )
    validate_binding(
        root / COMPOSITION_PATH,
        receipt.get("compositionRequest"),
        "composition request receipt",
        expected_path=COMPOSITION_PATH,
    )
    nonce = receipt.get("runnerNonce")
    if not isinstance(nonce, str) or NONCE_RE.fullmatch(nonce) is None:
        fail("candidate export runner nonce differs")
    source = receipt.get("source")
    if not isinstance(source, dict) or set(source) != {
        "actor",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "workflow",
    }:
        fail("candidate export source fields differ")
    for value, pattern, label in (
        (source.get("actor"), LOGIN_RE, "candidate export source actor"),
        (source.get("repository"), REPOSITORY_RE, "candidate export repository"),
        (source.get("runAttempt"), POSITIVE_INTEGER_RE, "candidate export run attempt"),
        (source.get("runId"), POSITIVE_INTEGER_RE, "candidate export run ID"),
    ):
        if not isinstance(value, str) or pattern.fullmatch(value) is None:
            fail(f"{label} differs")
    if (
        source.get("ref") != PRODUCER_REF
        or source.get("repository") != SOURCE_REPOSITORY
        or source.get("workflow") != PRODUCER_WORKFLOW
        or source.get("sha") != source_commit
    ):
        fail("candidate export source authority differs")
    return proposal, inventory, receipt


def reconstruct_publication(
    export_root: Path,
    incumbent_root: Path,
    output: Path,
    expected_version: str,
    expected_manifest_sha256: str,
    source_commit: str,
) -> dict[str, Any]:
    """Reconstruct the full proposed shelf from an exact export and incumbent."""

    proposal, _inventory, _receipt = validate_export_root(
        export_root,
        expected_version,
        expected_manifest_sha256,
        source_commit,
    )
    incumbent = incumbent_root.resolve(strict=True)
    if COMPOSITION.incumbent_snapshot(incumbent) != proposal["incumbentSnapshot"]:
        fail("direct import incumbent snapshot differs from composition request")
    output = output.absolute()
    if output.exists() or output.is_symlink():
        fail("reconstructed publication output must be absent")
    stage_module_path = Path(__file__).resolve().with_name(
        "preview_nightly_unsigned_stage.py"
    )
    spec = importlib.util.spec_from_file_location(
        "chummer6_ui_preview_nightly_unsigned_reconstruction_stage",
        stage_module_path,
    )
    if spec is None or spec.loader is None:
        fail("could not load exact shelf reconstruction helper")
    stage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(stage)
    stage.require_safe_parent(output.parent, "reconstructed publication parent")
    staging = Path(
        tempfile.mkdtemp(prefix=f".{output.name}.reconstruct-", dir=output.parent)
    )
    try:
        staging.rmdir()
        stage.copy_tree_exact(incumbent, staging)
        incumbent_manifest = PUBLICATION_SCOPE.read_json(
            incumbent / PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME,
            "direct import incumbent canonical manifest",
        )
        old_windows = [
            row
            for row in PUBLICATION_SCOPE.manifest_rows(
                incumbent_manifest,
                "artifacts",
                "direct import incumbent canonical manifest",
            )
            if PUBLICATION_SCOPE.row_platform(row) == "windows"
        ]
        remove_names: set[str] = set()
        for row in old_windows:
            remove_names.add(
                PUBLICATION_SCOPE.row_name(row, "incumbent Windows fileName")
            )
            payload = row.get("payloadFileName")
            if payload is not None:
                remove_names.add(
                    PUBLICATION_SCOPE.portable_name(
                        payload, "incumbent Windows payloadFileName"
                    )
                )
        incumbent_sidecar = (
            staging / "files" / PUBLICATION_SCOPE.PAYLOAD_SIDECAR_NAME
        )
        if incumbent_sidecar.exists() or incumbent_sidecar.is_symlink():
            remove_names.add(PUBLICATION_SCOPE.PAYLOAD_SIDECAR_NAME)
        for name in sorted(remove_names):
            target = staging / "files" / name
            metadata = PUBLICATION_SCOPE.regular_metadata(
                target, "incumbent Windows managed artifact"
            )
            if metadata.st_nlink != 1:
                fail("incumbent Windows managed artifact is hard-linked")
            target.unlink()
        proposed_by_path = {
            row["path"]: row for row in proposal["proposedShelfInventory"]
        }
        replacements = (
            (MANIFEST_PATH, PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME),
            (COMPATIBILITY_PATH, PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME),
            (INSTALLER_PATH, f"files/{PUBLICATION_SCOPE.INSTALLER_NAME}"),
            (PAYLOAD_PATH, f"files/{PUBLICATION_SCOPE.PAYLOAD_NAME}"),
            (
                PAYLOAD_SIDECAR_PATH,
                f"files/{PUBLICATION_SCOPE.PAYLOAD_SIDECAR_NAME}",
            ),
        )
        for source_relative, target_relative in replacements:
            target = staging / target_relative
            if target.exists() or target.is_symlink():
                PUBLICATION_SCOPE.regular_metadata(
                    target, "replaced publication entry"
                )
                target.unlink()
            source = export_root / source_relative
            shutil.copyfile(source, target, follow_symlinks=False)
            os.chmod(
                target,
                proposed_by_path[target_relative]["mode"],
                follow_symlinks=False,
            )
        if (
            PUBLICATION_SCOPE.file_inventory(staging)
            != proposal["proposedShelfInventory"]
            or PUBLICATION_SCOPE.directory_modes(staging)
            != proposal["proposedDirectoryModes"]
        ):
            fail("reconstructed publication differs from proposed shelf")
        stage.atomic_rename_noreplace(staging, output)
        staging = Path()
    finally:
        if staging != Path() and staging.exists():
            shutil.rmtree(staging, ignore_errors=True)
    return proposal


def require_text(value: str, pattern: re.Pattern[str], label: str) -> str:
    if pattern.fullmatch(value) is None:
        fail(f"{label} is invalid")
    return value


def write_json_new(path: Path, value: object, mode: int = 0o600) -> None:
    data = (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")
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
                fail("candidate export write made no progress")
            view = view[written:]
        os.fchmod(descriptor, mode)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def export_candidate(args: argparse.Namespace) -> dict[str, Any]:
    version = require_text(args.expected_version, VERSION_RE, "expected version")
    manifest_sha = require_text(
        args.expected_manifest_sha256, SHA256_RE, "expected manifest SHA-256"
    )
    source_sha = require_text(args.source_sha, COMMIT_RE, "source SHA")
    repository = require_text(args.source_repository, REPOSITORY_RE, "source repository")
    actor = require_text(args.source_actor, LOGIN_RE, "source actor")
    nonce = require_text(args.runner_nonce, NONCE_RE, "runner nonce")
    if repository != SOURCE_REPOSITORY:
        fail("source repository authority differs")
    if args.source_workflow != PRODUCER_WORKFLOW or args.source_ref != PRODUCER_REF:
        fail("workflow/ref authority differs")
    for value, label in (
        (args.source_run_id, "source run ID"),
        (args.source_run_attempt, "source run attempt"),
    ):
        require_text(value, POSITIVE_INTEGER_RE, label)
    candidate = args.candidate_root.resolve(strict=True)
    proposal = validate_candidate_root(candidate, version, manifest_sha, source_sha)
    output = args.output_root.absolute()
    if output.exists() or output.is_symlink():
        fail("candidate export output must be absent")
    output.mkdir(parents=True, mode=0o700)
    try:
        for directory in CONTENT_DIRECTORIES:
            (output / directory).mkdir(mode=0o700)
        before = content_rows(candidate, CONTENT_PATHS)
        for relative in CONTENT_PATHS:
            source = candidate / relative
            target = output / relative
            shutil.copyfile(source, target, follow_symlinks=False)
            os.chmod(target, 0o444, follow_symlinks=False)
        after = content_rows(output, CONTENT_PATHS)
        if [
            {key: row[key] for key in ("path", "sha256", "sizeBytes")}
            for row in before
        ] != [
            {key: row[key] for key in ("path", "sha256", "sizeBytes")}
            for row in after
        ]:
            fail("candidate bytes changed while exported")
        inventory = {
            "contractName": CONTENT_INVENTORY_CONTRACT,
            "contractVersion": CONTRACT_VERSION,
            "crossRunBitReproducible": False,
            "files": after,
            "platformScope": "windows_only",
            "release": {"channel": "preview", "version": version},
            "signature": dict(PUBLICATION_SCOPE.SIGNATURE),
            "sourceSha": source_sha,
        }
        write_json_new(output / CONTENT_INVENTORY_PATH, inventory, 0o444)
        inventory_binding = {
            "path": CONTENT_INVENTORY_PATH,
            "sha256": sha256_file(output / CONTENT_INVENTORY_PATH),
            "sizeBytes": file_size(output / CONTENT_INVENTORY_PATH),
        }
        receipt = {
            "contractName": EXPORT_CONTRACT,
            "contractVersion": CONTRACT_VERSION,
            "crossRunBitReproducible": False,
            "deployAuthorized": False,
            "exportedContent": after,
            "inventory": inventory_binding,
            "githubArtifactTransport": "ephemeral_candidate_only",
            "platformScope": "windows_only",
            "publicationAuthorized": False,
            "release": {"channel": "preview", "version": version},
            "runnerNonce": nonce,
            "signature": dict(PUBLICATION_SCOPE.SIGNATURE),
            "source": {
                "actor": actor,
                "ref": args.source_ref,
                "repository": repository,
                "runAttempt": args.source_run_attempt,
                "runId": args.source_run_id,
                "sha": source_sha,
                "workflow": args.source_workflow,
            },
            "status": "exported",
            "uploadAuthorized": False,
            "uiUploadAuthorized": False,
            "compositionRequest": {
                "path": COMPOSITION_PATH,
                "sha256": sha256_file(output / COMPOSITION_PATH),
                "sizeBytes": file_size(output / COMPOSITION_PATH),
            },
        }
        write_json_new(output / EXPORT_RECEIPT_PATH, receipt, 0o444)
        require_exact_tree(output, OUTPUT_PATHS, "unsigned candidate export")
        return receipt
    except BaseException:
        shutil.rmtree(output, ignore_errors=True)
        raise


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument(
        "--source-repository", required=True, choices=(SOURCE_REPOSITORY,)
    )
    parser.add_argument("--source-workflow", required=True)
    parser.add_argument("--source-run-id", required=True)
    parser.add_argument("--source-run-attempt", required=True)
    parser.add_argument("--source-ref", required=True)
    parser.add_argument("--source-actor", required=True)
    parser.add_argument("--runner-nonce", required=True)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        receipt = export_candidate(args)
    except (ExportError, OSError, ValueError) as exc:
        print(f"unsigned-candidate-export:error: {exc}", file=sys.stderr)
        return 2
    print(f"output={args.output_root}")
    print(f"composition_sha256={receipt['compositionRequest']['sha256']}")
    print(f"inventory_sha256={receipt['inventory']['sha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
