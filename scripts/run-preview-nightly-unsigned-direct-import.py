#!/usr/bin/env python3
"""Seal one exported unsigned Windows nightly behind review-required authority.

This offline coordinator validates and reconstructs the exported UI composition,
runs Registry PREPARE v2, materializes UI scope v3, runs Registry FINALIZE v2,
and asks Hub to emit candidate-import authority v3.  It has no network, upload,
publication, routing, or deployment behavior.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from pathlib import Path
from types import ModuleType
from typing import Any


def load_exporter() -> ModuleType:
    name = "chummer6_ui_preview_nightly_unsigned_direct_import_exporter"
    existing = sys.modules.get(name)
    if existing is not None:
        if not isinstance(existing, ModuleType):
            raise RuntimeError("preloaded direct-import exporter is malformed")
        return existing
    path = Path(__file__).resolve().with_name(
        "preview_nightly_unsigned_candidate_export.py"
    )
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load direct-import exporter")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


EXPORT = load_exporter()


SCOPE = EXPORT.PUBLICATION_SCOPE
COMPOSITION = EXPORT.COMPOSITION
STAGE = None
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
REGISTRY_ORIGIN = "https://github.com/ArchonMegalon/chummer6-hub-registry.git"
HUB_ORIGIN = "https://github.com/ArchonMegalon/chummer6-hub.git"
UI_ORIGIN = "https://github.com/ArchonMegalon/chummer6-ui.git"
REGISTRY_SCRIPT = "scripts/materialize_unsigned_preview_publication_delta.py"
HUB_SCRIPT = "scripts/release/materialize_candidate_import_authority.py"
SCOPE_NAME = SCOPE.PROPOSAL_FILE_NAME
COMPOSITION_NAME = COMPOSITION.PROPOSAL_FILE_NAME
CANONICAL_NAME = SCOPE.CANONICAL_MANIFEST_NAME
COMPATIBILITY_NAME = SCOPE.COMPATIBILITY_MANIFEST_NAME
REGISTRY_CANDIDATE_NAME = "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"
REGISTRY_AUTHORITY_NAME = "PREVIEW_PUBLICATION_DELTA_AUTHORITY.json"
REGISTRY_FINALIZE_NAME = "PREVIEW_PUBLICATION_DELTA_FINALIZE.json"
CANDIDATE_INVENTORY_NAME = "RELEASE_UPLOAD_CANDIDATE_INVENTORY.generated.json"
CANDIDATE_SUMMARY_NAME = "RELEASE_UPLOAD_CANDIDATE_SUMMARY.generated.json"
HUB_AUTHORITY_NAME = "RELEASE_UPLOAD_CANDIDATE_AUTHORITY.generated.json"
COORDINATOR_RECEIPT_NAME = "UNSIGNED_WINDOWS_PREVIEW_DIRECT_IMPORT.generated.json"


class ImportError(RuntimeError):
    """A fail-closed direct-import coordinator error."""


def fail(message: str) -> None:
    raise ImportError(message)


def stage_module():
    global STAGE
    if STAGE is None:
        path = Path(__file__).resolve().with_name(
            "preview_nightly_unsigned_stage.py"
        )
        spec = importlib.util.spec_from_file_location(
            "chummer6_ui_preview_nightly_unsigned_direct_import_stage", path
        )
        if spec is None or spec.loader is None:
            fail("could not load exact stage transaction helper")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        STAGE = module
    return STAGE


def child_environment() -> dict[str, str]:
    exact = {"PATH", "HOME", "LANG", "XDG_RUNTIME_DIR"}
    return {
        key: value
        for key, value in os.environ.items()
        if key in exact or key.startswith("LC_")
    }


def run_checked(arguments: list[str], *, label: str, cwd: Path | None = None) -> str:
    try:
        completed = subprocess.run(
            arguments,
            cwd=cwd,
            check=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=1800,
            env=child_environment(),
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        fail(f"{label} could not run: {type(exc).__name__}")
    if completed.returncode != 0:
        detail = completed.stderr.strip().splitlines()
        suffix = f": {detail[-1]}" if detail else ""
        fail(f"{label} failed with exit {completed.returncode}{suffix}")
    return completed.stdout.strip()


def git_value(root: Path, *arguments: str) -> str:
    return run_checked(
        ["git", "-C", str(root), *arguments], label="repository authority check"
    ).strip()


def verify_repository(
    root: Path, expected_sha: str, expected_origin: str, label: str
) -> Path:
    if COMMIT_RE.fullmatch(expected_sha) is None:
        fail(f"{label} expected source SHA is invalid")
    root = root.resolve(strict=True)
    metadata = root.lstat()
    if root.is_symlink() or not stat.S_ISDIR(metadata.st_mode):
        fail(f"{label} repository root must be one physical directory")
    if Path(git_value(root, "rev-parse", "--show-toplevel")) != root:
        fail(f"{label} repository root differs from git authority")
    if git_value(root, "remote", "get-url", "origin") != expected_origin:
        fail(f"{label} repository origin differs")
    head = git_value(root, "rev-parse", "HEAD")
    protected = git_value(root, "rev-parse", "refs/remotes/origin/main^{commit}")
    if head != expected_sha or protected != expected_sha:
        fail(f"{label} checkout is not the exact protected-main commit")
    if git_value(root, "status", "--porcelain", "--untracked-files=normal"):
        fail(f"{label} checkout is not clean")
    return root


def require_plain_file(path: Path, label: str) -> Path:
    metadata = path.lstat()
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        fail(f"{label} must be one regular non-link file")
    return path


def copy_exact(source: Path, target: Path, mode: int = 0o444) -> None:
    require_plain_file(source, "custody source")
    target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    if target.exists() or target.is_symlink():
        fail(f"custody destination already exists: {target.name}")
    shutil.copyfile(source, target, follow_symlinks=False)
    os.chmod(target, mode, follow_symlinks=False)
    if EXPORT.sha256_file(source) != EXPORT.sha256_file(target):
        fail(f"custody copy changed bytes: {target.name}")


def json_new(path: Path, value: object, mode: int = 0o600) -> None:
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
                fail("direct-import receipt write made no progress")
            view = view[written:]
        os.fchmod(descriptor, mode)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def byte_reference(path: Path, relative: str) -> dict[str, object]:
    metadata = require_plain_file(path, relative).stat()
    return {
        "path": relative,
        "sha256": EXPORT.sha256_file(path),
        "sizeBytes": metadata.st_size,
    }


def validate_private_tree(root: Path) -> None:
    seen: set[str] = set()
    for current, directories, files in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            if relative.casefold() in seen:
                fail(f"direct-import output repeats or case-collides at {relative}")
            seen.add(relative.casefold())
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"direct-import output contains a symbolic link: {relative}")
            if stat.S_ISDIR(metadata.st_mode):
                continue
            if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
                fail(f"direct-import output contains a special or linked file: {relative}")


def inventory_digest(rows: list[dict[str, Any]]) -> str:
    digest = hashlib.sha256()
    for row in rows:
        encoded = row["path"].encode("utf-8")
        digest.update(len(encoded).to_bytes(8, "big"))
        digest.update(encoded)
        digest.update(row["sizeBytes"].to_bytes(8, "big"))
        digest.update(bytes.fromhex(row["sha256"]))
    return digest.hexdigest()


def materialize_upload_identity(bundle: Path, stage: Path, version: str) -> None:
    rows = [
        {key: row[key] for key in ("path", "sha256", "sizeBytes")}
        for row in SCOPE.file_inventory(bundle)
    ]
    inventory = {
        "contractName": "chummer.release-upload.candidate-inventory/v1",
        "contractVersion": 1,
        "files": rows,
    }
    json_new(stage / CANDIDATE_INVENTORY_NAME, inventory)
    summary: dict[str, Any] = {
        "canonicalManifestSha256": EXPORT.sha256_file(bundle / CANONICAL_NAME),
        "fileCount": len(rows),
        "inventorySha256": inventory_digest(rows),
        "totalBytes": sum(row["sizeBytes"] for row in rows),
        "version": version,
    }
    identity = json.dumps(summary, sort_keys=True, separators=(",", ":")).encode(
        "utf-8"
    )
    summary["bundleIdentitySha256"] = hashlib.sha256(identity).hexdigest()
    json_new(stage / CANDIDATE_SUMMARY_NAME, summary)


def run_pipeline(args: argparse.Namespace) -> dict[str, Any]:
    version = args.expected_version
    source_sha = args.ui_source_sha
    manifest_sha = args.expected_manifest_sha256
    if (
        SCOPE.VERSION_RE.fullmatch(version) is None
        or ".." in version
        or COMMIT_RE.fullmatch(source_sha) is None
        or SHA256_RE.fullmatch(manifest_sha) is None
    ):
        fail("direct-import release identity is invalid")
    ui_root = verify_repository(
        Path(__file__).resolve().parents[1], source_sha, UI_ORIGIN, "UI"
    )
    registry_root = verify_repository(
        args.registry_repo_root,
        args.registry_source_sha,
        REGISTRY_ORIGIN,
        "Registry",
    )
    hub_root = verify_repository(
        args.hub_repo_root, args.hub_source_sha, HUB_ORIGIN, "Hub"
    )
    registry_script = require_plain_file(
        registry_root / REGISTRY_SCRIPT, "Registry unsigned materializer"
    )
    hub_script = require_plain_file(hub_root / HUB_SCRIPT, "Hub authority materializer")
    export_root = args.export_root.resolve(strict=True)
    incumbent = args.incumbent_root.resolve(strict=True)
    output = args.output_root.absolute()
    if output.exists() or output.is_symlink():
        fail("direct-import output must be absent")
    forbidden_roots = (ui_root, registry_root, hub_root, export_root, incumbent)
    if any(output == root or output in root.parents or root in output.parents for root in forbidden_roots):
        fail("direct-import output must be disjoint from all input/authority roots")
    helper = stage_module()
    helper.require_safe_parent(output.parent, "direct-import output parent")
    proposal, _transport_inventory, transport_receipt = EXPORT.validate_export_root(
        export_root, version, manifest_sha, source_sha
    )
    staging = Path(
        tempfile.mkdtemp(prefix=f".{output.name}.direct-import-", dir=output.parent)
    )
    try:
        staging.chmod(0o700)
        bundle = staging / "bundle"
        EXPORT.reconstruct_publication(
            export_root, incumbent, bundle, version, manifest_sha, source_sha
        )
        copy_exact(export_root / COMPOSITION_NAME, staging / COMPOSITION_NAME)
        for relative in COMPOSITION.PROVENANCE_PATHS.values():
            copy_exact(export_root / relative, staging / relative)
        transport = staging / "transport"
        transport.mkdir(mode=0o700)
        for relative in (EXPORT.CONTENT_INVENTORY_PATH, EXPORT.EXPORT_RECEIPT_PATH):
            copy_exact(export_root / relative, transport / relative)

        composition_sha = EXPORT.sha256_file(staging / COMPOSITION_NAME)
        provenance = {
            name: staging / relative
            for name, relative in COMPOSITION.PROVENANCE_PATHS.items()
        }
        transaction_root = staging / "registry-transactions"
        transaction_root.mkdir(mode=0o700)
        prepare_root = transaction_root / "prepare"
        finalize_root = transaction_root / "finalize"
        prepare = [
            sys.executable,
            str(registry_script),
            "prepare",
            "--composition-request",
            str(staging / COMPOSITION_NAME),
            "--expected-composition-request-sha256",
            composition_sha,
            "--publication-root",
            str(bundle),
            "--incumbent-root",
            str(incumbent),
            "--package-plane-lock",
            str(provenance["packagePlaneLock"]),
            "--package-plane-receipt",
            str(provenance["packagePlaneReceipt"]),
            "--retained-manifest",
            str(provenance["retainedManifest"]),
            "--native-toolchain-lock",
            str(provenance["nativeToolchainLock"]),
            "--output-manifest",
            str(prepare_root / CANONICAL_NAME),
            "--output-compatibility-manifest",
            str(prepare_root / COMPATIBILITY_NAME),
            "--output-candidate-receipt",
            str(prepare_root / REGISTRY_CANDIDATE_NAME),
        ]
        run_checked(prepare, label="Registry PREPARE v2", cwd=registry_root)
        for name in (CANONICAL_NAME, COMPATIBILITY_NAME):
            if EXPORT.sha256_file(prepare_root / name) != EXPORT.sha256_file(bundle / name):
                fail("Registry PREPARE manifest differs from reconstructed shelf")
        for name in (
            CANONICAL_NAME,
            COMPATIBILITY_NAME,
            REGISTRY_CANDIDATE_NAME,
        ):
            copy_exact(prepare_root / name, staging / name)

        scope_args = argparse.Namespace(
            publication_root=bundle,
            incumbent_root=incumbent,
            expected_version=version,
            source_sha=source_sha,
            package_plane_lock=provenance["packagePlaneLock"],
            package_plane_receipt=provenance["packagePlaneReceipt"],
            retained_manifest=provenance["retainedManifest"],
            native_toolchain_lock=provenance["nativeToolchainLock"],
        )
        scope = SCOPE.build_proposal(scope_args)
        SCOPE.write_scope(staging / SCOPE_NAME, scope)
        scope_sha = EXPORT.sha256_file(staging / SCOPE_NAME)

        finalize = [
            sys.executable,
            str(registry_script),
            "finalize",
            "--composition-request",
            str(staging / COMPOSITION_NAME),
            "--expected-composition-request-sha256",
            composition_sha,
            "--candidate-manifest",
            str(prepare_root / CANONICAL_NAME),
            "--candidate-compatibility-manifest",
            str(prepare_root / COMPATIBILITY_NAME),
            "--candidate-receipt",
            str(prepare_root / REGISTRY_CANDIDATE_NAME),
            "--unsigned-scope",
            str(staging / SCOPE_NAME),
            "--expected-unsigned-scope-sha256",
            scope_sha,
            "--publication-root",
            str(bundle),
            "--incumbent-root",
            str(incumbent),
            "--package-plane-lock",
            str(provenance["packagePlaneLock"]),
            "--package-plane-receipt",
            str(provenance["packagePlaneReceipt"]),
            "--retained-manifest",
            str(provenance["retainedManifest"]),
            "--native-toolchain-lock",
            str(provenance["nativeToolchainLock"]),
            "--output-authority",
            str(finalize_root / REGISTRY_AUTHORITY_NAME),
            "--output-finalize-receipt",
            str(finalize_root / REGISTRY_FINALIZE_NAME),
        ]
        run_checked(finalize, label="Registry FINALIZE v2", cwd=registry_root)
        for name in (REGISTRY_AUTHORITY_NAME, REGISTRY_FINALIZE_NAME):
            copy_exact(finalize_root / name, staging / name)
        shutil.rmtree(transaction_root)

        materialize_upload_identity(bundle, staging, version)
        hub = [
            sys.executable,
            str(hub_script),
            "--bundle-root",
            str(bundle),
            "--canonical-manifest",
            str(bundle / CANONICAL_NAME),
            "--candidate-summary",
            str(staging / CANDIDATE_SUMMARY_NAME),
            "--candidate-inventory",
            str(staging / CANDIDATE_INVENTORY_NAME),
            "--publication-stage-root",
            str(staging),
            "--publication-scope",
            str(staging / SCOPE_NAME),
            "--registry-candidate-receipt",
            str(staging / REGISTRY_CANDIDATE_NAME),
            "--registry-finalize-authority",
            str(staging / REGISTRY_AUTHORITY_NAME),
            "--registry-finalize-receipt",
            str(staging / REGISTRY_FINALIZE_NAME),
            "--output",
            str(staging / HUB_AUTHORITY_NAME),
        ]
        run_checked(hub, label="Hub candidate-import v3", cwd=hub_root)
        receipt = {
            "compositionRequest": byte_reference(
                staging / COMPOSITION_NAME, COMPOSITION_NAME
            ),
            "contractName": "chummer6-ui.preview-nightly-unsigned-direct-import",
            "contractVersion": 1,
            "crossRunBitReproducible": False,
            "deployAuthorized": False,
            "hubCandidateImportAuthority": byte_reference(
                staging / HUB_AUTHORITY_NAME, HUB_AUTHORITY_NAME
            ),
            "platformScope": "windows_only",
            "publicationAuthorized": False,
            "registryFinalizeAuthority": byte_reference(
                staging / REGISTRY_AUTHORITY_NAME, REGISTRY_AUTHORITY_NAME
            ),
            "registryCandidateReceipt": byte_reference(
                staging / REGISTRY_CANDIDATE_NAME, REGISTRY_CANDIDATE_NAME
            ),
            "registryFinalizeReceipt": byte_reference(
                staging / REGISTRY_FINALIZE_NAME, REGISTRY_FINALIZE_NAME
            ),
            "release": {"channel": "preview", "version": version},
            "signature": dict(SCOPE.SIGNATURE),
            "sourceCommits": {
                "hub": args.hub_source_sha,
                "registry": args.registry_source_sha,
                "ui": source_sha,
            },
            "status": "sealed_review_required",
            "uiScope": byte_reference(staging / SCOPE_NAME, SCOPE_NAME),
            "transport": {
                "exportReceipt": byte_reference(
                    transport / EXPORT.EXPORT_RECEIPT_PATH,
                    f"transport/{EXPORT.EXPORT_RECEIPT_PATH}",
                ),
                "inventory": byte_reference(
                    transport / EXPORT.CONTENT_INVENTORY_PATH,
                    f"transport/{EXPORT.CONTENT_INVENTORY_PATH}",
                ),
                "sourceRunId": transport_receipt["source"]["runId"],
            },
            "uploadAuthorized": False,
        }
        json_new(staging / COORDINATOR_RECEIPT_NAME, receipt)
        validate_private_tree(staging)
        helper.atomic_rename_noreplace(staging, output)
        staging = Path()
        return receipt
    finally:
        if staging != Path() and staging.exists():
            shutil.rmtree(staging, ignore_errors=True)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-root", required=True, type=Path)
    parser.add_argument("--incumbent-root", required=True, type=Path)
    parser.add_argument("--registry-repo-root", required=True, type=Path)
    parser.add_argument("--registry-source-sha", required=True)
    parser.add_argument("--hub-repo-root", required=True, type=Path)
    parser.add_argument("--hub-source-sha", required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--ui-source-sha", required=True)
    parser.add_argument("--output-root", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    try:
        args = parse_args(argv)
        receipt = run_pipeline(args)
    except (ImportError, EXPORT.ExportError, SCOPE.ScopeError, OSError, ValueError) as exc:
        print(f"unsigned-direct-import:error: {exc}", file=sys.stderr)
        return 2
    print(f"output={args.output_root}")
    print(f"status={receipt['status']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
