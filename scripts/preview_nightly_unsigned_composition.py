#!/usr/bin/env python3
"""Prepare or replay the Windows-only unsigned preview composition request.

The request is an input to the additive Registry PREPARE v2 lane.  It binds a
proposed full shelf, the incumbent snapshot, the exact Windows delta, and the
four build-provenance documents, but grants no upload, publication, or deploy
authority.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys
from pathlib import Path
from types import ModuleType
from typing import Any


SCOPE_MODULE_NAME = "chummer6_ui_preview_nightly_unsigned_scope_contract"


def _load_scope() -> ModuleType:
    existing = sys.modules.get(SCOPE_MODULE_NAME)
    if existing is not None:
        if not isinstance(existing, ModuleType):
            raise RuntimeError("preloaded unsigned scope contract is malformed")
        return existing
    path = Path(__file__).resolve().with_name("preview_nightly_unsigned_scope.py")
    spec = importlib.util.spec_from_file_location(SCOPE_MODULE_NAME, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load unsigned scope contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SCOPE = _load_scope()
CONTRACT_NAME = "chummer6-ui.preview-nightly-unsigned-composition-request"
CONTRACT_VERSION = 3
PROJECTION_PROFILE = SCOPE.PROJECTION_PROFILE
PROPOSAL_FILE_NAME = "PREVIEW_NIGHTLY_UNSIGNED_COMPOSITION.proposed.json"
PROVENANCE_PATHS = {
    "nativeToolchainLock": (
        "provenance/config/windows-native-bootstrap-toolchain.lock.json"
    ),
    "packagePlaneLock": "provenance/config/package-plane.lock.json",
    "packagePlaneReceipt": (
        "provenance/UI_FRESH_PACKAGE_PLANE.generated.json"
    ),
    "retainedManifest": (
        "provenance/retained-windows-publish-closure/manifest.json"
    ),
}
ROOT_KEYS = {
    "contractName",
    "contractVersion",
    "crossRunBitReproducible",
    "deployAuthorized",
    "freshDelta",
    "incumbentSnapshot",
    "platformScope",
    "projectionProfile",
    "proposedCanonicalManifest",
    "proposedCompatibilityManifest",
    "proposedDirectoryModes",
    "proposedDirectoryModesSha256",
    "proposedShelfInventory",
    "proposedShelfInventorySha256",
    "provenance",
    "publicationAuthorized",
    "release",
    "retainedFromIncumbent",
    "signature",
    "sourceSha",
    "status",
    "uploadAuthorized",
}


class CompositionError(RuntimeError):
    """A fail-closed unsigned composition-request error."""


def fail(message: str) -> None:
    raise CompositionError(message)


def binding_with_path(path: Path, relative: str) -> dict[str, object]:
    return SCOPE.binding(path, relative)


def validate_directory_modes(value: object, label: str) -> list[dict[str, object]]:
    if not isinstance(value, list):
        fail(f"{label} must be an array")
    result: list[dict[str, object]] = []
    seen: set[str] = set()
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != {"mode", "path"}:
            fail(f"{label} row fields differ")
        path = SCOPE.portable_path(raw["path"], f"{label} path")
        if path.casefold() in seen:
            fail(f"{label} repeats or case-collides at {path}")
        seen.add(path.casefold())
        if type(raw["mode"]) is not int or not 0 <= raw["mode"] <= 0o777:
            fail(f"{label} mode is invalid")
        result.append({"mode": raw["mode"], "path": path})
    if result != sorted(result, key=lambda row: str(row["path"])):
        fail(f"{label} is not sorted canonically")
    return result


def incumbent_snapshot(root: Path) -> dict[str, object]:
    inventory = SCOPE.file_inventory(root)
    directories = SCOPE.directory_modes(root)
    snapshot: dict[str, object] = {
        "canonicalManifest": binding_with_path(
            root / SCOPE.CANONICAL_MANIFEST_NAME,
            SCOPE.CANONICAL_MANIFEST_NAME,
        ),
        "compatibilityManifest": binding_with_path(
            root / SCOPE.COMPATIBILITY_MANIFEST_NAME,
            SCOPE.COMPATIBILITY_MANIFEST_NAME,
        ),
        "directoryModes": directories,
        "directoryModesSha256": SCOPE.canonical_sha256(directories),
        "fullShelfInventory": inventory,
        "fullShelfInventorySha256": SCOPE.canonical_sha256(inventory),
    }
    snapshot["snapshotSha256"] = SCOPE.canonical_sha256(snapshot)
    return snapshot


def build_request(args: argparse.Namespace) -> dict[str, Any]:
    proposal = SCOPE.build_proposal(args)
    publication = args.publication_root.resolve(strict=True)
    incumbent = args.incumbent_root.resolve(strict=True)
    proposed_directories = SCOPE.directory_modes(publication)
    canonical = SCOPE.read_json(
        publication / SCOPE.CANONICAL_MANIFEST_NAME,
        "proposed canonical manifest",
    )
    rows = SCOPE.manifest_rows(
        canonical, "artifacts", "proposed canonical manifest"
    )
    windows = [
        row
        for row in rows
        if SCOPE.row_platform(row) == "windows"
        and (row.get("head") or row.get("headId")) == "avalonia"
        and row.get("rid") == "win-x64"
        and SCOPE.row_name(row, "proposed Windows fileName")
        == SCOPE.INSTALLER_NAME
    ]
    if len(windows) != 1:
        fail("proposed manifest lacks one exact Windows row")
    manifest_row_sha = SCOPE.canonical_sha256(windows[0])
    fresh = [
        {**row, "manifestRowSha256": manifest_row_sha}
        for row in proposal["freshDelta"]
    ]
    provenance_paths = {
        "nativeToolchainLock": args.native_toolchain_lock,
        "packagePlaneLock": args.package_plane_lock,
        "packagePlaneReceipt": args.package_plane_receipt,
        "retainedManifest": args.retained_manifest,
    }
    request = {
        "contractName": CONTRACT_NAME,
        "contractVersion": CONTRACT_VERSION,
        "crossRunBitReproducible": False,
        "deployAuthorized": False,
        "freshDelta": fresh,
        "incumbentSnapshot": incumbent_snapshot(incumbent),
        "platformScope": "windows_only",
        "projectionProfile": PROJECTION_PROFILE,
        "proposedCanonicalManifest": binding_with_path(
            publication / SCOPE.CANONICAL_MANIFEST_NAME,
            SCOPE.CANONICAL_MANIFEST_NAME,
        ),
        "proposedCompatibilityManifest": binding_with_path(
            publication / SCOPE.COMPATIBILITY_MANIFEST_NAME,
            SCOPE.COMPATIBILITY_MANIFEST_NAME,
        ),
        "proposedDirectoryModes": proposed_directories,
        "proposedDirectoryModesSha256": SCOPE.canonical_sha256(
            proposed_directories
        ),
        "proposedShelfInventory": proposal["fullShelfInventory"],
        "proposedShelfInventorySha256": proposal[
            "fullShelfInventorySha256"
        ],
        "provenance": {
            key: binding_with_path(provenance_paths[key], relative)
            for key, relative in PROVENANCE_PATHS.items()
        },
        "publicationAuthorized": False,
        "release": proposal["release"],
        "retainedFromIncumbent": proposal["retainedFromIncumbent"],
        "signature": dict(SCOPE.SIGNATURE),
        "sourceSha": proposal["sourceSha"],
        "status": "prepared",
        "uploadAuthorized": False,
    }
    validate_request(request)
    return request


def validate_snapshot(value: object) -> dict[str, Any]:
    keys = {
        "canonicalManifest",
        "compatibilityManifest",
        "directoryModes",
        "directoryModesSha256",
        "fullShelfInventory",
        "fullShelfInventorySha256",
        "snapshotSha256",
    }
    if not isinstance(value, dict) or set(value) != keys:
        fail("incumbent snapshot fields differ")
    SCOPE.validate_binding(
        value["canonicalManifest"],
        "incumbent canonical manifest",
        expected_path=SCOPE.CANONICAL_MANIFEST_NAME,
    )
    SCOPE.validate_binding(
        value["compatibilityManifest"],
        "incumbent compatibility manifest",
        expected_path=SCOPE.COMPATIBILITY_MANIFEST_NAME,
    )
    inventory = SCOPE.validate_inventory(
        value["fullShelfInventory"], "incumbent full shelf inventory"
    )
    directories = validate_directory_modes(
        value["directoryModes"], "incumbent directory modes"
    )
    if value["fullShelfInventorySha256"] != SCOPE.canonical_sha256(inventory):
        fail("incumbent full shelf inventory digest differs")
    if value["directoryModesSha256"] != SCOPE.canonical_sha256(directories):
        fail("incumbent directory-mode digest differs")
    without_digest = {key: value[key] for key in keys - {"snapshotSha256"}}
    if value["snapshotSha256"] != SCOPE.canonical_sha256(without_digest):
        fail("incumbent snapshot digest differs")
    return dict(value)


def validate_request(value: object) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != ROOT_KEYS:
        fail("composition request root fields differ")
    if (
        value.get("contractName") != CONTRACT_NAME
        or value.get("contractVersion") != CONTRACT_VERSION
        or value.get("status") != "prepared"
        or value.get("platformScope") != "windows_only"
        or value.get("crossRunBitReproducible") is not False
        or value.get("signature") != SCOPE.SIGNATURE
        or value.get("publicationAuthorized") is not False
        or value.get("uploadAuthorized") is not False
        or value.get("deployAuthorized") is not False
        or value.get("projectionProfile") != PROJECTION_PROFILE
    ):
        fail("composition request posture differs")
    source_sha = value.get("sourceSha")
    if not isinstance(source_sha, str) or SCOPE.COMMIT_RE.fullmatch(source_sha) is None:
        fail("composition request sourceSha differs")
    release = value.get("release")
    if not isinstance(release, dict) or set(release) != {"channel", "version"}:
        fail("composition request release fields differ")
    version = release.get("version")
    if (
        release.get("channel") != "preview"
        or not isinstance(version, str)
        or SCOPE.VERSION_RE.fullmatch(version) is None
        or ".." in version
    ):
        fail("composition request release differs")
    validate_snapshot(value.get("incumbentSnapshot"))
    SCOPE.validate_binding(
        value.get("proposedCanonicalManifest"),
        "proposed canonical manifest",
        expected_path=SCOPE.CANONICAL_MANIFEST_NAME,
    )
    SCOPE.validate_binding(
        value.get("proposedCompatibilityManifest"),
        "proposed compatibility manifest",
        expected_path=SCOPE.COMPATIBILITY_MANIFEST_NAME,
    )
    inventory = SCOPE.validate_inventory(
        value.get("proposedShelfInventory"), "proposed shelf inventory"
    )
    if value.get("proposedShelfInventorySha256") != SCOPE.canonical_sha256(
        inventory
    ):
        fail("proposed shelf inventory digest differs")
    directories = validate_directory_modes(
        value.get("proposedDirectoryModes"), "proposed directory modes"
    )
    if value.get("proposedDirectoryModesSha256") != SCOPE.canonical_sha256(
        directories
    ):
        fail("proposed directory-mode digest differs")
    provenance = value.get("provenance")
    if not isinstance(provenance, dict) or set(provenance) != set(
        PROVENANCE_PATHS
    ):
        fail("composition provenance fields differ")
    for key, relative in PROVENANCE_PATHS.items():
        SCOPE.validate_binding(
            provenance[key], f"provenance.{key}", expected_path=relative
        )
    retained = SCOPE.validate_retained(value.get("retainedFromIncumbent"))
    inventory_by_path = {row["path"]: row for row in inventory}
    for row in retained:
        exact = {key: row[key] for key in ("mode", "path", "sha256", "sizeBytes")}
        if inventory_by_path.get(row["path"]) != exact:
            fail("retained row differs from proposed shelf inventory")
    fresh = value.get("freshDelta")
    if not isinstance(fresh, list) or len(fresh) != 3:
        fail("freshDelta must contain exact installer/payload/metadata rows")
    expected = (
        ("installer", SCOPE.INSTALLER_NAME),
        ("bootstrap_payload", SCOPE.PAYLOAD_NAME),
        ("bootstrap_payload_sidecar", SCOPE.PAYLOAD_SIDECAR_NAME),
    )
    manifest_row_sha: str | None = None
    for row, (role, name) in zip(fresh, expected, strict=True):
        if not isinstance(row, dict) or set(row) != {
            "artifactRole",
            "fileName",
            "head",
            "manifestRowSha256",
            "mode",
            "path",
            "platform",
            "rid",
            "sha256",
            "sizeBytes",
        }:
            fail("freshDelta row fields differ")
        if (
            row.get("artifactRole") != role
            or row.get("fileName") != name
            or row.get("head") != "avalonia"
            or row.get("path") != f"files/{name}"
            or row.get("platform") != "windows"
            or row.get("rid") != "win-x64"
        ):
            fail("freshDelta identity differs")
        digest = row.get("manifestRowSha256")
        if not isinstance(digest, str) or SCOPE.SHA256_RE.fullmatch(digest) is None:
            fail("freshDelta manifest row digest differs")
        if manifest_row_sha is None:
            manifest_row_sha = digest
        elif manifest_row_sha != digest:
            fail("freshDelta rows do not share one manifest row digest")
        exact = {key: row[key] for key in ("mode", "path", "sha256", "sizeBytes")}
        if inventory_by_path.get(row["path"]) != exact:
            fail("freshDelta differs from proposed shelf inventory")
    return dict(value)


def write_request(path: Path, value: dict[str, Any]) -> None:
    SCOPE.write_scope(path, value)


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
            command.add_argument("--request", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        expected = build_request(args)
        if args.command == "prepare":
            write_request(args.output, expected)
            output = args.output
        else:
            observed = SCOPE.read_json(args.request, "unsigned composition request")
            validate_request(observed)
            if observed != expected:
                fail("unsigned composition request replay differs")
            output = args.request
    except (CompositionError, SCOPE.ScopeError, OSError, ValueError) as exc:
        print(f"unsigned-composition:error: {exc}", file=sys.stderr)
        return 2
    print(f"composition={output}")
    print(f"composition_sha256={SCOPE.sha256_file(output)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
