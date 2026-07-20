#!/usr/bin/env python3
"""Exact SBOM and vulnerability evidence for the Windows/Linux preview.

The SBOM is a deterministic CycloneDX 1.6 projection of one exact RID target
from ``project.assets.json`` plus the final installer/payload bytes.  The
vulnerability receipt is deliberately *not* described as reproducible: it
binds a fresh live OSV response, its query time and response digest, and the
checksum-pinned scanner which produced it.

This module has no publication or credential handling capability.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import math
import os
import re
import stat
import subprocess
import sys
import tempfile
import uuid
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable
from urllib.parse import quote


SBOM_CONTRACT = "chummer6-ui.preview-rid-cyclonedx"
RID_GRAPH_CONTRACT = "chummer6-ui.preview-rid-project-assets-graph"
RID_GRAPH_SOURCE_AUTHORITY_CONTRACT = (
    "chummer6-ui.preview-rid-source-graph-authority"
)
SCAN_CONTRACT = "chummer6-ui.preview-rid-vulnerability-scan"
GATE_CONTRACT = "chummer6-ui.preview-supply-chain-gate"
CONTRACT_VERSION = 1
CYCLONEDX_SCHEMA = "https://cyclonedx.org/schema/bom-1.6.schema.json"
CYCLONEDX_SPEC_VERSION = "1.6"
GENERATOR_NAME = "chummer6-ui.preview_supply_chain"
GENERATOR_VERSION = "1"
OSV_SCANNER_NAME = "google/osv-scanner"
OSV_SCANNER_VERSION = "2.3.8"
OSV_SCANNER_COMMIT = "408fcd6f8707999a29e7ba45e15809764cf24f67"
OSV_SCANNER_SHA256 = "bc98e15319ed0d515e3f9235287ba53cdc5535d576d24fd573978ecfe9ab92dc"
OSV_DATA_SOURCE = "https://api.osv.dev"
OSV_QUERY_MODE = "live_api"
OSV_RESPONSE_NORMALIZATION = "exact_sbom_source_path_portabilized/v1"
ADVISORY_FRESHNESS = timedelta(hours=24)
MAX_FUTURE_SKEW = timedelta(minutes=5)
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
SHA512_RE = re.compile(r"^[0-9a-f]{128}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
RID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)+$")
ACTIVE_TUPLES: tuple[tuple[str, str, str], ...] = (
    ("avalonia", "linux", "linux-x64"),
    ("avalonia", "windows", "win-x64"),
)
SBOM_PATHS: dict[str, str] = {
    "linux-x64": "release-evidence/sbom/chummer-avalonia-linux-x64.cdx.json",
    "win-x64": "release-evidence/sbom/chummer-avalonia-win-x64.cdx.json",
}
SCAN_PATHS: dict[str, str] = {
    "linux-x64": "release-evidence/vulnerability/OSV_SCAN-avalonia-linux-x64.generated.json",
    "win-x64": "release-evidence/vulnerability/OSV_SCAN-avalonia-win-x64.generated.json",
}
RID_GRAPH_SOURCE_AUTHORITY_PATHS: dict[str, str] = {
    "linux-x64": (
        "release-authority/preview/avalonia-linux-x64.project-assets-lock.json"
    ),
    "win-x64": (
        "release-authority/preview/avalonia-win-x64.project-assets-lock.json"
    ),
}
RID_GRAPH_SOURCE_AUTHORITY_SHA256: dict[str, str] = {
    "linux-x64": "7e00549d3d90dec2110a6105cd09a6b9ebebf676a40e791f203bf558b89bf035",
    "win-x64": "2d21ff7859410b630a9b2c6f36328fbfd6713bbcbd10425be862fb86cb3c1c2b",
}
GATE_PATH = "release-evidence/PREVIEW_SUPPLY_CHAIN_GATE.generated.json"
LIVE_VERIFICATION_MODE = "pinned_live_scanner_reexecution"
STRUCTURAL_VERIFICATION_MODE = "structural_non_release_authoritative"
SUPPLY_CHAIN_CONTENT_PATHS: tuple[str, ...] = (
    *(SBOM_PATHS[rid] for _, _, rid in ACTIVE_TUPLES),
    *(SCAN_PATHS[rid] for _, _, rid in ACTIVE_TUPLES),
    GATE_PATH,
)
LEGACY_ALERT_ASSERTIONS: tuple[dict[str, str], ...] = (
    {
        "package": "System.Text.Json",
        "blockedVersion": "7.0.3",
        "disposition": "must_be_absent_from_active_graph_not_ignored",
    },
    {
        "packagePattern": "*IdentityModel*",
        "blockedVersionRange": "6.x",
        "disposition": "must_be_absent_from_active_graph_not_ignored",
    },
)


class SupplyChainError(RuntimeError):
    """Raised when exact release supply-chain evidence cannot be trusted."""


def fail(message: str) -> None:
    raise SupplyChainError(message)


def _exact_string(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be an exact non-empty string")
    return value


def _exact_object(
    value: object, expected_keys: Iterable[str], label: str
) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an exact JSON object")
    expected = set(expected_keys)
    actual = set(value)
    if actual != expected:
        fail(
            f"{label} schema differs: "
            f"missing={sorted(expected - actual)}, extra={sorted(actual - expected)}"
        )
    return value


def _exact_integer(
    value: object, label: str, *, minimum: int | None = None
) -> int:
    if type(value) is not int:
        fail(f"{label} must be an exact JSON integer")
    if minimum is not None and value < minimum:
        fail(f"{label} must be at least {minimum}")
    return value


def _require_exact_json(actual: object, expected: object, label: str) -> None:
    """Compare JSON values without Python's bool/int equality aliasing."""

    if type(actual) is not type(expected):
        fail(
            f"{label} JSON type differs: "
            f"expected={type(expected).__name__}, actual={type(actual).__name__}"
        )
    if isinstance(expected, dict):
        assert isinstance(actual, dict)
        if set(actual) != set(expected):
            fail(f"{label} JSON object schema differs")
        for key in expected:
            _require_exact_json(actual[key], expected[key], f"{label}.{key}")
        return
    if isinstance(expected, list):
        assert isinstance(actual, list)
        if len(actual) != len(expected):
            fail(f"{label} JSON list length differs")
        for index, (actual_item, expected_item) in enumerate(zip(actual, expected)):
            _require_exact_json(
                actual_item, expected_item, f"{label}[{index}]"
            )
        return
    if actual != expected:
        fail(f"{label} JSON value differs")


def require_sha256(value: object, label: str) -> str:
    digest = _exact_string(value, label)
    if SHA256_RE.fullmatch(digest) is None:
        fail(f"{label} must be an exact lowercase SHA-256")
    return digest


def require_commit(value: object, label: str = "source commit") -> str:
    commit = _exact_string(value, label)
    if COMMIT_RE.fullmatch(commit) is None:
        fail(f"{label} must be an exact lowercase 40-character commit SHA")
    return commit


def require_version(value: object) -> str:
    version = _exact_string(value, "release version")
    if VERSION_RE.fullmatch(version) is None:
        fail("release version is not portable")
    return version


def require_rid(value: object) -> str:
    rid = _exact_string(value, "RID")
    if RID_RE.fullmatch(rid) is None or rid not in SBOM_PATHS:
        fail("RID is outside the exact active preview set")
    return rid


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        raw = path.read_bytes()
        payload = json.loads(raw.decode("utf-8-sig"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def canonical_json_bytes(payload: object) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def compact_json_sha256(payload: object) -> str:
    raw = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def compact_json_text(payload: object) -> str:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))


def _generator_source_sha256() -> str:
    """Return the exact validator/generator source identity.

    The JIT launcher executes a commit-verified in-memory source snapshot.  It
    injects that snapshot's digest before execution because its synthetic
    ``__file__`` has no filesystem bytes to hash.  Normal invocations hash the
    exact module file that is executing.
    """

    trusted = globals().get("_TRUSTED_SOURCE_SHA256")
    if trusted is not None:
        return require_sha256(trusted, "trusted supply-chain source digest")
    return sha256_file(Path(__file__).resolve())


def write_new_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "wb", closefd=True) as handle:
        handle.write(canonical_json_bytes(payload))


def sha256_file(path: Path) -> str:
    if path.is_symlink():
        fail(f"symlinks are forbidden in supply-chain evidence: {path}")
    try:
        descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    except OSError as exc:
        fail(f"could not open supply-chain input {path}: {exc}")
    digest = hashlib.sha256()
    try:
        info = os.fstat(descriptor)
        if not stat.S_ISREG(info.st_mode):
            fail(f"supply-chain input is not a regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            while chunk := handle.read(1024 * 1024):
                digest.update(chunk)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def _regular_file_size(path: Path) -> int:
    if path.is_symlink() or not path.is_file():
        fail(f"required supply-chain input is not a regular file: {path}")
    size = path.stat().st_size
    if size < 1:
        fail(f"required supply-chain input is empty: {path}")
    return size


def parse_utc(value: object, label: str) -> datetime:
    raw = _exact_string(value, label)
    if not raw.endswith("Z"):
        fail(f"{label} must be an explicit UTC timestamp ending in Z")
    try:
        parsed = datetime.fromisoformat(raw[:-1] + "+00:00")
    except ValueError as exc:
        fail(f"{label} is invalid: {exc}")
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        fail(f"{label} must be UTC")
    return parsed


def utc_text(value: datetime) -> str:
    return value.astimezone(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def purl_for_nuget(name: str, version: str) -> str:
    return f"pkg:nuget/{quote(name, safe='._~-')}@{quote(version, safe='._~-')}"


def _split_library_key(key: str) -> tuple[str, str]:
    if "/" not in key:
        fail(f"project assets target has an invalid library identity: {key}")
    name, version = key.rsplit("/", 1)
    if not name or not version:
        fail(f"project assets target has an incomplete library identity: {key}")
    return name, version


def _sha512_hex(value: object, label: str) -> str | None:
    if value is None:
        return None
    raw = _exact_string(value, label)
    try:
        decoded = base64.b64decode(raw, validate=True)
    except ValueError as exc:
        fail(f"{label} is not canonical base64: {exc}")
    if len(decoded) != 64:
        fail(f"{label} is not a SHA-512 digest")
    if base64.b64encode(decoded).decode("ascii") != raw:
        fail(f"{label} is not canonical base64")
    return decoded.hex()


def _target_graph(assets: dict[str, Any], rid: str) -> tuple[str, dict[str, Any]]:
    targets = assets.get("targets")
    if not isinstance(targets, dict):
        fail("project assets targets must be an object")
    matches = [
        (key, value)
        for key, value in targets.items()
        if isinstance(key, str)
        and key.rsplit("/", 1)[-1] == rid
        and isinstance(value, dict)
    ]
    if len(matches) != 1:
        fail(f"project assets must contain exactly one target graph for {rid}")
    target_name, target = matches[0]
    if not target_name.startswith("net10.0/"):
        fail(f"project assets {rid} target is not net10.0")
    if not target:
        fail(f"project assets {rid} target graph is empty")
    return target_name, target


def _legacy_alerts_present(packages: Iterable[dict[str, str]]) -> list[str]:
    present: list[str] = []
    for package in packages:
        name = package["name"]
        version = package["version"]
        lowered = name.lower()
        if lowered == "system.text.json" and version == "7.0.3":
            present.append(f"{name}@{version}")
        if "identitymodel" in lowered and version.split(".", 1)[0] == "6":
            present.append(f"{name}@{version}")
    return sorted(set(present), key=str.lower)


def _artifact_component(relative: str, absolute: Path, rid: str) -> dict[str, Any]:
    portable = PurePosixPath(relative)
    if (
        portable.is_absolute()
        or portable.as_posix() != relative
        or any(part in {"", ".", ".."} for part in portable.parts)
        or "\\" in relative
    ):
        fail(f"artifact path is not portable: {relative}")
    digest = sha256_file(absolute)
    size = _regular_file_size(absolute)
    return {
        "bom-ref": f"artifact:{rid}:{digest}",
        "hashes": [{"alg": "SHA-256", "content": digest}],
        "name": portable.name,
        "properties": [
            {"name": "chummer:relative-path", "value": relative},
            {"name": "chummer:rid", "value": rid},
            {"name": "chummer:size-bytes", "value": str(size)},
        ],
        "type": "file",
    }


def _expected_artifact_paths(rid: str) -> tuple[str, ...]:
    if rid == "win-x64":
        return (
            "files/chummer-avalonia-win-x64-installer.exe",
            "files/chummer-avalonia-win-x64-payload.zip",
        )
    if rid == "linux-x64":
        return ("files/chummer-avalonia-linux-x64-installer.deb",)
    fail("RID is outside the exact active preview set")


def _normalized_rid_graph(assets_path: Path, rid: str) -> dict[str, Any]:
    assets = read_json(assets_path, "project assets")
    target_name, target = _target_graph(assets, rid)
    libraries = assets.get("libraries")
    if not isinstance(libraries, dict):
        fail("project assets libraries must be an object")
    if any(not isinstance(key, str) or not key for key in target):
        fail("project assets target library keys must be exact non-empty strings")

    rows: list[dict[str, Any]] = []
    names: dict[str, str] = {}
    for key in sorted(target, key=lambda value: (value.lower(), value)):
        target_row = target[key]
        library_row = libraries.get(key)
        if not isinstance(target_row, dict) or not isinstance(library_row, dict):
            fail(f"project assets library metadata is incomplete for {key}")
        name, package_version = _split_library_key(key)
        lowered = name.lower()
        if lowered in names:
            fail(f"project assets target contains multiple versions of {name}")
        names[lowered] = key
        library_type = library_row.get("type")
        if library_type not in {"package", "project"}:
            fail(f"project assets contains unsupported library type for {key}: {library_type}")
        sha512 = _sha512_hex(library_row.get("sha512"), f"{key} sha512")
        if library_type == "package" and sha512 is None:
            fail(f"project assets package authority has no SHA-512 for {key}")
        if library_type == "project" and sha512 is not None:
            fail(f"project assets project authority unexpectedly has a package digest for {key}")
        raw_dependencies = target_row.get("dependencies", {})
        if not isinstance(raw_dependencies, dict):
            fail(f"project assets dependencies must be an object for {key}")
        if any(not isinstance(name, str) or not name for name in raw_dependencies):
            fail(f"project assets dependency names must be exact strings for {key}")
        dependencies: list[dict[str, str]] = []
        for dependency_name in sorted(
            raw_dependencies, key=lambda value: (value.lower(), value)
        ):
            dependencies.append(
                {
                    "name": _exact_string(
                        dependency_name, f"{key} dependency name"
                    ),
                    "requestedVersion": _exact_string(
                        raw_dependencies[dependency_name],
                        f"{key} dependency {dependency_name} requested version",
                    ),
                }
            )
        rows.append(
            {
                "dependencies": dependencies,
                "key": key,
                "name": name,
                "sha512": sha512,
                "type": library_type,
                "version": package_version,
            }
        )

    for row in rows:
        for dependency in row["dependencies"]:
            if dependency["name"].lower() not in names:
                fail(
                    "project assets dependency is unresolved for "
                    f"{row['key']}: {dependency['name']}"
                )
    graph = {
        "contractName": RID_GRAPH_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "libraries": rows,
        "projectAssetsSha256": sha256_file(assets_path),
        "rid": rid,
        "targetName": target_name,
    }
    _validate_normalized_rid_graph(graph, rid)
    return graph


def _validate_normalized_rid_graph(
    graph: object, rid: str
) -> dict[str, Any]:
    graph = _exact_object(
        graph,
        {
            "contractName",
            "contractVersion",
            "libraries",
            "projectAssetsSha256",
            "rid",
            "targetName",
        },
        "normalized RID graph authority",
    )
    if graph.get("contractName") != RID_GRAPH_CONTRACT:
        fail("normalized RID graph contract name differs")
    if _exact_integer(
        graph.get("contractVersion"), "normalized RID graph contractVersion"
    ) != CONTRACT_VERSION:
        fail("normalized RID graph contract version differs")
    if graph.get("rid") != rid or graph.get("targetName") != f"net10.0/{rid}":
        fail("normalized RID graph target identity differs")
    require_sha256(
        graph.get("projectAssetsSha256"),
        "normalized RID graph project-assets digest",
    )
    libraries = graph.get("libraries")
    if not isinstance(libraries, list) or not libraries:
        fail("normalized RID graph libraries must be a non-empty list")
    names: dict[str, str] = {}
    keys: list[str] = []
    package_rows: list[dict[str, str]] = []
    for index, value in enumerate(libraries):
        row = _exact_object(
            value,
            {"dependencies", "key", "name", "sha512", "type", "version"},
            f"normalized RID graph library[{index}]",
        )
        key = _exact_string(row.get("key"), f"normalized RID graph library[{index}] key")
        name = _exact_string(row.get("name"), f"normalized RID graph library[{index}] name")
        version = _exact_string(
            row.get("version"), f"normalized RID graph library[{index}] version"
        )
        if _split_library_key(key) != (name, version):
            fail(f"normalized RID graph library identity differs for {key}")
        lowered = name.lower()
        if lowered in names:
            fail(f"normalized RID graph contains multiple versions of {name}")
        names[lowered] = key
        keys.append(key)
        library_type = row.get("type")
        sha512 = row.get("sha512")
        if library_type == "package":
            if not isinstance(sha512, str) or SHA512_RE.fullmatch(sha512) is None:
                fail(f"normalized RID graph package digest is invalid for {key}")
            package_rows.append(
                {
                    "name": name,
                    "purl": purl_for_nuget(name, version),
                    "version": version,
                }
            )
        elif library_type == "project":
            if sha512 is not None:
                fail(f"normalized RID graph project digest must be null for {key}")
        else:
            fail(f"normalized RID graph library type is invalid for {key}")
        dependencies = row.get("dependencies")
        if not isinstance(dependencies, list):
            fail(f"normalized RID graph dependencies must be a list for {key}")
        dependency_names: list[str] = []
        for dependency_index, dependency_value in enumerate(dependencies):
            dependency = _exact_object(
                dependency_value,
                {"name", "requestedVersion"},
                f"normalized RID graph {key} dependency[{dependency_index}]",
            )
            dependency_name = _exact_string(
                dependency.get("name"), f"normalized RID graph {key} dependency name"
            )
            _exact_string(
                dependency.get("requestedVersion"),
                f"normalized RID graph {key} dependency requestedVersion",
            )
            dependency_names.append(dependency_name)
        expected_names = sorted(dependency_names, key=lambda value: (value.lower(), value))
        if dependency_names != expected_names or len(
            {value.lower() for value in dependency_names}
        ) != len(dependency_names):
            fail(f"normalized RID graph dependencies are not exact for {key}")
    expected_keys = sorted(keys, key=lambda value: (value.lower(), value))
    if keys != expected_keys or len(keys) != len(set(keys)):
        fail("normalized RID graph library ordering or identity is not exact")
    for row in libraries:
        for dependency in row["dependencies"]:
            if dependency["name"].lower() not in names:
                fail(
                    "normalized RID graph dependency is unresolved for "
                    f"{row['key']}: {dependency['name']}"
                )
    if not package_rows:
        fail("normalized RID graph contains no NuGet packages")
    legacy = _legacy_alerts_present(package_rows)
    if legacy:
        fail(
            "active SBOM contains legacy alerted packages; alerts are not dismissible: "
            + ", ".join(legacy)
        )
    return graph


def _source_graph_authority_projection(
    graph: dict[str, Any], rid: str
) -> dict[str, Any]:
    graph = _validate_normalized_rid_graph(graph, rid)
    return {
        "contractName": RID_GRAPH_SOURCE_AUTHORITY_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "libraries": graph["libraries"],
        "rid": rid,
        "targetName": graph["targetName"],
    }


def _validate_source_graph_authority(
    value: object, rid: str
) -> dict[str, Any]:
    authority = _exact_object(
        value,
        {"contractName", "contractVersion", "libraries", "rid", "targetName"},
        f"{rid} source-controlled RID graph authority",
    )
    if authority.get("contractName") != RID_GRAPH_SOURCE_AUTHORITY_CONTRACT:
        fail(f"{rid} source-controlled RID graph authority contract differs")
    if _exact_integer(
        authority.get("contractVersion"),
        f"{rid} source-controlled RID graph authority contractVersion",
    ) != CONTRACT_VERSION:
        fail(f"{rid} source-controlled RID graph authority version differs")
    replay_graph = {
        "contractName": RID_GRAPH_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "libraries": authority.get("libraries"),
        "projectAssetsSha256": "0" * 64,
        "rid": authority.get("rid"),
        "targetName": authority.get("targetName"),
    }
    _validate_normalized_rid_graph(replay_graph, rid)
    return authority


def _trusted_source_graph_authority(
    rid: str,
) -> tuple[dict[str, Any], str, str]:
    relative = RID_GRAPH_SOURCE_AUTHORITY_PATHS[rid]
    injected = globals().get("_TRUSTED_RID_GRAPH_AUTHORITY_BYTES")
    if injected is not None:
        if not isinstance(injected, dict) or not isinstance(injected.get(rid), bytes):
            fail(f"{rid} trusted in-memory RID graph authority is missing")
        raw = injected[rid]
    else:
        authority_path = Path(__file__).resolve().parents[1] / relative
        if authority_path.is_symlink() or not authority_path.is_file():
            fail(f"{rid} source-controlled RID graph authority is missing")
        try:
            raw = authority_path.read_bytes()
        except OSError as exc:
            fail(f"{rid} source-controlled RID graph authority cannot be read: {exc}")
    if hashlib.sha256(raw).hexdigest() != RID_GRAPH_SOURCE_AUTHORITY_SHA256[rid]:
        fail(f"{rid} source-controlled RID graph authority digest differs")
    if not raw or len(raw) > 1024 * 1024:
        fail(f"{rid} source-controlled RID graph authority is empty or oversized")
    try:
        payload = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{rid} source-controlled RID graph authority is invalid JSON: {exc}")
    if not isinstance(payload, dict) or raw != canonical_json_bytes(payload):
        fail(f"{rid} source-controlled RID graph authority is not canonical")
    authority = _validate_source_graph_authority(payload, rid)
    return authority, relative, hashlib.sha256(raw).hexdigest()


def _bind_graph_to_source_authority(
    graph: dict[str, Any], rid: str
) -> tuple[str, str]:
    observed = _source_graph_authority_projection(graph, rid)
    expected, relative, digest = _trusted_source_graph_authority(rid)
    _require_exact_json(
        observed, expected, f"{rid} normalized graph versus source authority"
    )
    return relative, digest


def _project_normalized_rid_graph(
    graph: dict[str, Any], rid: str
) -> tuple[
    list[dict[str, Any]],
    list[dict[str, str]],
    list[dict[str, Any]],
    dict[str, str],
]:
    graph = _validate_normalized_rid_graph(graph, rid)
    components: list[dict[str, Any]] = []
    packages: list[dict[str, str]] = []
    ref_by_key: dict[str, str] = {}
    key_by_name = {row["name"].lower(): row["key"] for row in graph["libraries"]}
    for row in graph["libraries"]:
        name = row["name"]
        version = row["version"]
        if row["type"] == "package":
            ref = purl_for_nuget(name, version)
            component = {
                "bom-ref": ref,
                "hashes": [{"alg": "SHA-512", "content": row["sha512"]}],
                "name": name,
                "purl": ref,
                "type": "library",
                "version": version,
            }
            packages.append({"name": name, "purl": ref, "version": version})
        else:
            ref = f"project:{quote(name, safe='._~-')}@{quote(version, safe='._~-')}"
            component = {
                "bom-ref": ref,
                "name": name,
                "properties": [{"name": "chummer:assets-kind", "value": "project"}],
                "type": "library",
                "version": version,
            }
        ref_by_key[row["key"]] = ref
        components.append(component)
    dependencies: list[dict[str, Any]] = []
    for row in graph["libraries"]:
        dependencies.append(
            {
                "dependsOn": sorted(
                    {
                        ref_by_key[key_by_name[dependency["name"].lower()]]
                        for dependency in row["dependencies"]
                    }
                ),
                "ref": ref_by_key[row["key"]],
            }
        )
    components.sort(key=lambda row: row["bom-ref"])
    packages.sort(key=lambda row: (row["name"].lower(), row["version"]))
    dependencies.sort(key=lambda row: row["ref"])
    return components, packages, dependencies, ref_by_key


def _sbom_serial_number(
    *,
    artifact_components: list[dict[str, Any]],
    generator_sha256: str,
    graph: dict[str, Any],
    graph_authority_sha256: str,
    rid: str,
    source_commit: str,
    version: str,
) -> str:
    canonical_artifact_components = sorted(
        artifact_components, key=lambda row: row["bom-ref"]
    )
    identity = {
        "artifactComponents": canonical_artifact_components,
        "generatorSha256": generator_sha256,
        "normalizedRidGraphSha256": compact_json_sha256(graph),
        "normalizedRidGraphAuthoritySha256": graph_authority_sha256,
        "projectAssetsSha256": graph["projectAssetsSha256"],
        "rid": rid,
        "sourceCommit": source_commit,
        "version": version,
    }
    serial = uuid.uuid5(uuid.NAMESPACE_URL, compact_json_sha256(identity))
    return f"urn:uuid:{serial}"


def generate_sbom(
    *,
    assets_path: Path,
    rid: str,
    version: str,
    source_commit: str,
    artifacts: dict[str, Path],
) -> dict[str, Any]:
    rid = require_rid(rid)
    version = require_version(version)
    source_commit = require_commit(source_commit)
    expected_artifacts = _expected_artifact_paths(rid)
    if tuple(sorted(artifacts)) != tuple(sorted(expected_artifacts)):
        fail(f"{rid} artifact set is not exact")
    graph = _normalized_rid_graph(assets_path, rid)
    graph_authority_path, graph_authority_sha = _bind_graph_to_source_authority(
        graph, rid
    )
    components, _, dependencies, component_ref_by_key = _project_normalized_rid_graph(
        graph, rid
    )
    target_name = graph["targetName"]

    artifact_components = [
        _artifact_component(relative, artifacts[relative], rid)
        for relative in sorted(artifacts)
    ]
    components.extend(artifact_components)
    components.sort(key=lambda row: row["bom-ref"])
    assets_sha = graph["projectAssetsSha256"]
    graph_sha = compact_json_sha256(graph)
    graph_text = compact_json_text(graph)
    script_sha = _generator_source_sha256()
    root_ref = f"application:chummer-avalonia:{version}:{rid}"
    root = {
        "bom-ref": root_ref,
        "name": "Chummer.Avalonia",
        "properties": [
            {"name": "chummer:channel", "value": "preview"},
            {"name": "chummer:project-assets-sha256", "value": assets_sha},
            {"name": "chummer:rid", "value": rid},
            {"name": "chummer:source-commit", "value": source_commit},
        ],
        "type": "application",
        "version": version,
    }
    dependencies.append(
        {
            "dependsOn": sorted(component_ref_by_key.values()),
            "ref": root_ref,
        }
    )
    dependencies.sort(key=lambda row: row["ref"])
    serial = _sbom_serial_number(
        artifact_components=artifact_components,
        generator_sha256=script_sha,
        graph=graph,
        graph_authority_sha256=graph_authority_sha,
        rid=rid,
        source_commit=source_commit,
        version=version,
    )
    return {
        "$schema": CYCLONEDX_SCHEMA,
        "bomFormat": "CycloneDX",
        "components": components,
        "compositions": [
            {
                "aggregate": "complete",
                "assemblies": [row["bom-ref"] for row in components],
                "bom-ref": root_ref,
            }
        ],
        "dependencies": dependencies,
        "metadata": {
            "component": root,
            "properties": [
                {"name": "chummer:contract-name", "value": SBOM_CONTRACT},
                {"name": "chummer:contract-version", "value": str(CONTRACT_VERSION)},
                {"name": "chummer:normalized-rid-graph", "value": graph_text},
                {
                    "name": "chummer:normalized-rid-graph-authority-path",
                    "value": graph_authority_path,
                },
                {
                    "name": "chummer:normalized-rid-graph-authority-sha256",
                    "value": graph_authority_sha,
                },
                {"name": "chummer:normalized-rid-graph-sha256", "value": graph_sha},
                {"name": "chummer:target-graph", "value": target_name},
            ],
            "tools": {
                "components": [
                    {
                        "hashes": [{"alg": "SHA-256", "content": script_sha}],
                        "name": GENERATOR_NAME,
                        "type": "application",
                        "version": GENERATOR_VERSION,
                    }
                ]
            },
        },
        "serialNumber": serial,
        "specVersion": CYCLONEDX_SPEC_VERSION,
        "version": 1,
    }


def _component_properties(component: dict[str, Any]) -> dict[str, str]:
    rows = component.get("properties")
    if not isinstance(rows, list):
        return {}
    result: dict[str, str] = {}
    for row in rows:
        if not isinstance(row, dict) or set(row) != {"name", "value"}:
            fail("CycloneDX component property is malformed")
        name = _exact_string(row.get("name"), "CycloneDX property name")
        value = _exact_string(row.get("value"), f"CycloneDX property {name}")
        if name in result:
            fail(f"CycloneDX property is duplicated: {name}")
        result[name] = value
    return result


def sbom_package_rows(sbom: dict[str, Any]) -> list[dict[str, str]]:
    components = sbom.get("components")
    if not isinstance(components, list):
        fail("CycloneDX components must be a list")
    packages: list[dict[str, str]] = []
    for component in components:
        if not isinstance(component, dict):
            fail("CycloneDX component must be an object")
        purl = component.get("purl")
        if purl is None:
            continue
        name = _exact_string(component.get("name"), "CycloneDX package name")
        version = _exact_string(component.get("version"), f"CycloneDX {name} version")
        expected = purl_for_nuget(name, version)
        if purl != expected or component.get("bom-ref") != expected:
            fail(f"CycloneDX NuGet identity is not exact for {name}@{version}")
        packages.append({"name": name, "purl": expected, "version": version})
    packages.sort(key=lambda row: (row["name"].lower(), row["version"]))
    if not packages or len({row["purl"] for row in packages}) != len(packages):
        fail("CycloneDX NuGet package set is empty or duplicated")
    legacy = _legacy_alerts_present(packages)
    if legacy:
        fail(
            "active SBOM contains legacy alerted packages; alerts are not dismissible: "
            + ", ".join(legacy)
        )
    return packages


def _cvss3_score(vector: str) -> float | None:
    if not vector.startswith(("CVSS:3.0/", "CVSS:3.1/")):
        return None
    values: dict[str, str] = {}
    for token in vector.split("/")[1:]:
        if ":" not in token:
            return None
        key, value = token.split(":", 1)
        if key in values:
            return None
        values[key] = value
    required = {"AV", "AC", "PR", "UI", "S", "C", "I", "A"}
    if set(values) != required:
        return None
    scope = values["S"]
    weights = {
        "AV": {"N": 0.85, "A": 0.62, "L": 0.55, "P": 0.20},
        "AC": {"L": 0.77, "H": 0.44},
        "UI": {"N": 0.85, "R": 0.62},
        "C": {"H": 0.56, "L": 0.22, "N": 0.0},
        "I": {"H": 0.56, "L": 0.22, "N": 0.0},
        "A": {"H": 0.56, "L": 0.22, "N": 0.0},
    }
    pr_weights = {
        "U": {"N": 0.85, "L": 0.62, "H": 0.27},
        "C": {"N": 0.85, "L": 0.68, "H": 0.50},
    }
    try:
        exploitability = (
            8.22
            * weights["AV"][values["AV"]]
            * weights["AC"][values["AC"]]
            * pr_weights[scope][values["PR"]]
            * weights["UI"][values["UI"]]
        )
        impact_base = 1 - (
            (1 - weights["C"][values["C"]])
            * (1 - weights["I"][values["I"]])
            * (1 - weights["A"][values["A"]])
        )
    except KeyError:
        return None
    if scope == "U":
        impact = 6.42 * impact_base
    elif scope == "C":
        impact = 7.52 * (impact_base - 0.029) - 3.25 * (impact_base - 0.02) ** 15
    else:
        return None
    if impact <= 0:
        return 0.0
    raw = min(impact + exploitability, 10.0)
    if scope == "C":
        raw = min(1.08 * (impact + exploitability), 10.0)
    return math.ceil(raw * 10 - 1e-10) / 10


def classify_vulnerability(vulnerability: dict[str, Any]) -> tuple[str, float | None]:
    rank_by_label = {
        "CRITICAL": 4,
        "HIGH": 3,
        "MODERATE": 2,
        "MEDIUM": 2,
        "LOW": 1,
    }
    label_by_rank = {1: "low", 2: "medium", 3: "high", 4: "critical"}
    ranks: list[int] = []
    scores: list[float] = []
    invalid_signal = False

    database_specific = vulnerability.get("database_specific")
    if database_specific is not None and not isinstance(database_specific, dict):
        invalid_signal = True
    elif isinstance(database_specific, dict) and "severity" in database_specific:
        label = database_specific.get("severity")
        if isinstance(label, str):
            normalized = label.strip().upper()
            rank = rank_by_label.get(normalized)
            if rank is None:
                invalid_signal = True
            else:
                ranks.append(rank)
        else:
            invalid_signal = True

    severities = vulnerability.get("severity")
    if severities is not None and not isinstance(severities, list):
        invalid_signal = True
    elif isinstance(severities, list):
        for row in severities:
            if not isinstance(row, dict):
                invalid_signal = True
                continue
            score = row.get("score")
            if isinstance(score, (int, float)) and not isinstance(score, bool):
                parsed = float(score)
            elif isinstance(score, str):
                try:
                    parsed = float(score)
                except ValueError:
                    parsed = _cvss3_score(score)
            else:
                parsed = None
            if parsed is None or not math.isfinite(parsed) or not 0 <= parsed <= 10:
                invalid_signal = True
                continue
            scores.append(parsed)
            if parsed >= 9:
                ranks.append(4)
            elif parsed >= 7:
                ranks.append(3)
            elif parsed >= 4:
                ranks.append(2)
            else:
                ranks.append(1)

    if invalid_signal or not ranks:
        return "unclassified", None
    return label_by_rank[max(ranks)], max(scores) if scores else None


def _fixed_versions(vulnerability: dict[str, Any]) -> list[str]:
    values: set[str] = set()
    affected = vulnerability.get("affected")
    if not isinstance(affected, list):
        return []
    for row in affected:
        if not isinstance(row, dict):
            continue
        ranges = row.get("ranges")
        if not isinstance(ranges, list):
            continue
        for range_row in ranges:
            if not isinstance(range_row, dict) or not isinstance(range_row.get("events"), list):
                continue
            for event in range_row["events"]:
                fixed = event.get("fixed") if isinstance(event, dict) else None
                if isinstance(fixed, str) and fixed:
                    values.add(fixed)
    return sorted(values)


def _exact_string_list(value: object, label: str) -> list[str]:
    if not isinstance(value, list):
        fail(f"{label} must be an exact JSON string list")
    result: list[str] = []
    for index, item in enumerate(value):
        result.append(_exact_string(item, f"{label}[{index}]"))
    return result


def _validate_osv_severity_rows(value: object, label: str) -> None:
    if not isinstance(value, list):
        fail(f"{label} must be an exact JSON list")
    for index, row_value in enumerate(value):
        row = _exact_object(
            row_value, {"score", "type"}, f"{label}[{index}]"
        )
        _exact_string(row.get("score"), f"{label}[{index}].score")
        _exact_string(row.get("type"), f"{label}[{index}].type")


def _validate_osv_vulnerability_shape(value: object, label: str) -> None:
    if not isinstance(value, dict):
        fail(f"{label} must be an exact JSON object")
    allowed = {
        "affected",
        "aliases",
        "credits",
        "database_specific",
        "details",
        "id",
        "modified",
        "published",
        "references",
        "related",
        "schema_version",
        "severity",
        "summary",
        "upstream",
        "withdrawn",
    }
    extras = set(value) - allowed
    if extras:
        fail(f"{label} schema differs: extra={sorted(extras)}")
    _exact_string(value.get("id"), f"{label}.id")
    for field in (
        "details",
        "modified",
        "published",
        "schema_version",
        "summary",
        "withdrawn",
    ):
        if field in value and value[field] is not None:
            _exact_string(value[field], f"{label}.{field}")
    for field in ("aliases", "related", "upstream"):
        if field in value:
            _exact_string_list(value[field], f"{label}.{field}")
    if "severity" in value:
        _validate_osv_severity_rows(value["severity"], f"{label}.severity")
    database_specific = value.get("database_specific")
    if database_specific is not None:
        if not isinstance(database_specific, dict):
            fail(f"{label}.database_specific must be an exact JSON object")
        if "severity" in database_specific:
            _exact_string(
                database_specific["severity"],
                f"{label}.database_specific.severity",
            )
    references = value.get("references")
    if references is not None:
        if not isinstance(references, list):
            fail(f"{label}.references must be an exact JSON list")
        for index, reference_value in enumerate(references):
            reference = _exact_object(
                reference_value,
                {"type", "url"},
                f"{label}.references[{index}]",
            )
            _exact_string(reference.get("type"), f"{label}.references[{index}].type")
            _exact_string(reference.get("url"), f"{label}.references[{index}].url")
    credits = value.get("credits")
    if credits is not None:
        if not isinstance(credits, list):
            fail(f"{label}.credits must be an exact JSON list")
        for index, credit_value in enumerate(credits):
            credit = _exact_object(
                credit_value,
                {"contact", "name", "type"},
                f"{label}.credits[{index}]",
            )
            _exact_string(credit.get("name"), f"{label}.credits[{index}].name")
            _exact_string(credit.get("type"), f"{label}.credits[{index}].type")
            _exact_string_list(
                credit.get("contact"), f"{label}.credits[{index}].contact"
            )
    affected = value.get("affected")
    if affected is not None:
        if not isinstance(affected, list):
            fail(f"{label}.affected must be an exact JSON list")
        for affected_index, affected_value in enumerate(affected):
            if not isinstance(affected_value, dict):
                fail(f"{label}.affected[{affected_index}] must be an exact JSON object")
            allowed_affected = {
                "database_specific",
                "ecosystem_specific",
                "package",
                "ranges",
                "severity",
                "versions",
            }
            extras = set(affected_value) - allowed_affected
            if extras:
                fail(
                    f"{label}.affected[{affected_index}] schema differs: "
                    f"extra={sorted(extras)}"
                )
            package = affected_value.get("package")
            if package is not None:
                if not isinstance(package, dict) or not package:
                    fail(f"{label}.affected[{affected_index}].package is malformed")
                allowed_package = {"ecosystem", "name", "purl"}
                if set(package) - allowed_package:
                    fail(f"{label}.affected[{affected_index}].package schema differs")
                for field, package_value in package.items():
                    _exact_string(
                        package_value,
                        f"{label}.affected[{affected_index}].package.{field}",
                    )
            if "versions" in affected_value:
                _exact_string_list(
                    affected_value["versions"],
                    f"{label}.affected[{affected_index}].versions",
                )
            if "severity" in affected_value:
                _validate_osv_severity_rows(
                    affected_value["severity"],
                    f"{label}.affected[{affected_index}].severity",
                )
            ranges = affected_value.get("ranges")
            if ranges is not None:
                if not isinstance(ranges, list):
                    fail(f"{label}.affected[{affected_index}].ranges must be a list")
                for range_index, range_value in enumerate(ranges):
                    if not isinstance(range_value, dict):
                        fail(
                            f"{label}.affected[{affected_index}].ranges[{range_index}] "
                            "must be an exact JSON object"
                        )
                    allowed_range = {"database_specific", "events", "repo", "type"}
                    if set(range_value) - allowed_range:
                        fail(
                            f"{label}.affected[{affected_index}].ranges[{range_index}] "
                            "schema differs"
                        )
                    if "type" in range_value:
                        _exact_string(
                            range_value["type"],
                            f"{label}.affected[{affected_index}].ranges[{range_index}].type",
                        )
                    if "repo" in range_value:
                        _exact_string(
                            range_value["repo"],
                            f"{label}.affected[{affected_index}].ranges[{range_index}].repo",
                        )
                    events = range_value.get("events")
                    if events is not None:
                        if not isinstance(events, list):
                            fail(
                                f"{label}.affected[{affected_index}].ranges[{range_index}].events "
                                "must be a list"
                            )
                        for event_index, event_value in enumerate(events):
                            if not isinstance(event_value, dict) or len(event_value) != 1:
                                fail(
                                    f"{label}.affected[{affected_index}].ranges[{range_index}]"
                                    f".events[{event_index}] must contain one exact event"
                                )
                            event_name, event_version = next(iter(event_value.items()))
                            if event_name not in {
                                "fixed",
                                "introduced",
                                "last_affected",
                                "limit",
                            }:
                                fail(
                                    f"{label}.affected[{affected_index}].ranges[{range_index}]"
                                    f".events[{event_index}] has an unknown event"
                                )
                            _exact_string(
                                event_version,
                                f"{label}.affected[{affected_index}].ranges[{range_index}]"
                                f".events[{event_index}].{event_name}",
                            )


def _validate_osv_response_envelope(response: dict[str, Any]) -> None:
    _exact_object(
        response,
        {"experimental_config", "results"},
        "OSV scanner response",
    )
    experimental = _exact_object(
        response.get("experimental_config"),
        {"licenses"},
        "OSV scanner experimental config",
    )
    licenses = _exact_object(
        experimental.get("licenses"),
        {"allowlist", "summary"},
        "OSV scanner license config",
    )
    _require_exact_json(
        licenses,
        {"allowlist": None, "summary": False},
        "OSV scanner license config",
    )
    results = response.get("results")
    if not isinstance(results, list) or not results:
        fail("OSV scanner returned no exact SBOM result")
    for result_index, result_value in enumerate(results):
        result = _exact_object(
            result_value,
            {"packages", "source"},
            f"OSV scanner result[{result_index}]",
        )
        source = _exact_object(
            result.get("source"),
            {"path", "type"},
            f"OSV scanner result[{result_index}] source",
        )
        _exact_string(source.get("path"), "OSV scanner source path")
        _exact_string(source.get("type"), "OSV scanner source type")
        package_rows = result.get("packages")
        if not isinstance(package_rows, list):
            fail("OSV scanner result packages must be a list")
        for package_index, package_value in enumerate(package_rows):
            if not isinstance(package_value, dict):
                fail("OSV scanner package row is malformed")
            keys = set(package_value)
            if keys not in ({"package"}, {"groups", "package", "vulnerabilities"}):
                fail(
                    "OSV scanner package result schema differs at "
                    f"result[{result_index}].packages[{package_index}]"
                )
            package = _exact_object(
                package_value.get("package"),
                {"ecosystem", "name", "version"},
                f"OSV scanner package[{package_index}] identity",
            )
            for field in ("ecosystem", "name", "version"):
                _exact_string(
                    package.get(field), f"OSV scanner package[{package_index}] {field}"
                )
            if "vulnerabilities" in package_value:
                vulnerabilities = package_value.get("vulnerabilities")
                groups = package_value.get("groups")
                if (
                    not isinstance(vulnerabilities, list)
                    or not vulnerabilities
                    or any(not isinstance(row, dict) for row in vulnerabilities)
                    or not isinstance(groups, list)
                    or not groups
                ):
                    fail("OSV scanner vulnerable package projection is malformed")
                for vulnerability_index, vulnerability_value in enumerate(
                    vulnerabilities
                ):
                    _validate_osv_vulnerability_shape(
                        vulnerability_value,
                        f"OSV scanner package[{package_index}]"
                        f".vulnerabilities[{vulnerability_index}]",
                    )
                for group_index, group_value in enumerate(groups):
                    group = _exact_object(
                        group_value,
                        {"aliases", "ids", "max_severity"},
                        f"OSV scanner package[{package_index}] group[{group_index}]",
                    )
                    for field in ("aliases", "ids"):
                        values = group.get(field)
                        if (
                            not isinstance(values, list)
                            or any(not isinstance(item, str) or not item for item in values)
                        ):
                            fail(f"OSV scanner vulnerability group {field} is malformed")
                    _exact_string(
                        group.get("max_severity"),
                        "OSV scanner vulnerability group max severity",
                    )


def normalize_scanner_response(
    response: dict[str, Any],
    packages: list[dict[str, str]],
    expected_source: str,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    _validate_osv_response_envelope(response)
    results = response.get("results")
    if not isinstance(results, list) or not results:
        fail("OSV scanner returned no exact SBOM result")
    expected_packages = {(row["name"].lower(), row["version"]) for row in packages}
    observed_packages: set[tuple[str, str]] = set()
    findings: list[dict[str, Any]] = []
    for result in results:
        if not isinstance(result, dict):
            fail("OSV scanner result row is malformed")
        source = result.get("source")
        if not isinstance(source, dict) or source.get("type") != "sbom":
            fail("OSV scanner result is not bound to an SBOM source")
        source_path = source.get("path")
        if source_path != expected_source or PurePosixPath(source_path).is_absolute():
            fail("OSV scanner result contains a non-portable or wrong SBOM path")
        result_packages = result.get("packages")
        if not isinstance(result_packages, list):
            fail("OSV scanner result packages must be a list")
        for row in result_packages:
            if not isinstance(row, dict) or not isinstance(row.get("package"), dict):
                fail("OSV scanner package row is malformed")
            package = row["package"]
            name = _exact_string(package.get("name"), "OSV package name")
            package_version = _exact_string(package.get("version"), f"OSV {name} version")
            if package.get("ecosystem") != "NuGet":
                fail(f"OSV scanner returned a non-NuGet package for the active SBOM: {name}")
            identity = (name.lower(), package_version)
            if identity not in expected_packages:
                fail(f"OSV scanner returned a package outside the exact SBOM: {name}@{package_version}")
            if identity in observed_packages:
                fail(f"OSV scanner duplicated an SBOM package: {name}@{package_version}")
            observed_packages.add(identity)
            vulnerabilities = row.get("vulnerabilities", [])
            if not isinstance(vulnerabilities, list):
                fail(f"OSV vulnerabilities are malformed for {name}@{package_version}")
            for vulnerability in vulnerabilities:
                if not isinstance(vulnerability, dict):
                    fail(f"OSV vulnerability row is malformed for {name}@{package_version}")
                advisory_id = _exact_string(vulnerability.get("id"), "OSV advisory ID")
                severity, score = classify_vulnerability(vulnerability)
                aliases = vulnerability.get("aliases", [])
                if not isinstance(aliases, list) or any(not isinstance(value, str) for value in aliases):
                    fail(f"OSV advisory aliases are malformed for {advisory_id}")
                findings.append(
                    {
                        "advisoryId": advisory_id,
                        "advisoryModifiedAt": vulnerability.get("modified")
                        if isinstance(vulnerability.get("modified"), str)
                        else None,
                        "aliases": sorted(set(aliases)),
                        "cvssScore": score,
                        "fixedVersions": _fixed_versions(vulnerability),
                        "package": name,
                        "severity": severity,
                        "version": package_version,
                    }
                )
    if observed_packages != expected_packages:
        missing = sorted(expected_packages - observed_packages)
        fail(f"OSV scanner did not report the exact --all-packages SBOM set: missing={missing}")
    findings.sort(
        key=lambda row: (
            row["package"].lower(),
            row["version"],
            row["advisoryId"],
        )
    )
    blocked = [row for row in findings if row["severity"] in {"high", "critical"}]
    unclassified = [row for row in findings if row["severity"] == "unclassified"]
    return findings, blocked, unclassified


def _scanner_version(binary: Path) -> str:
    try:
        completed = subprocess.run(
            [str(binary), "--version"],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=30,
            check=True,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        fail(f"pinned OSV scanner could not report its version: {exc}")
    expected_lines = (
        f"osv-scanner version: {OSV_SCANNER_VERSION}",
        "osv-scalibr version:",
        f"commit: {OSV_SCANNER_COMMIT}",
        "built at:",
    )
    lines = completed.stdout.splitlines()
    if (
        len(lines) != len(expected_lines)
        or lines[0] != expected_lines[0]
        or not lines[1].startswith(expected_lines[1])
        or lines[2] != expected_lines[2]
        or not lines[3].startswith(expected_lines[3])
    ):
        fail("OSV scanner version output differs from the pinned release")
    return completed.stdout


def _verify_scanner(binary: Path) -> None:
    if not binary.is_absolute() or binary.is_symlink() or not binary.is_file():
        fail("OSV scanner must be an absolute regular non-symlink file")
    if sha256_file(binary) != OSV_SCANNER_SHA256:
        fail("OSV scanner binary differs from the pinned SHA-256")
    _scanner_version(binary)


def _scan_sbom(
    *, stage_root: Path, sbom_relative: str, scanner: Path
) -> tuple[dict[str, Any], int, datetime, datetime]:
    _verify_scanner(scanner)
    queried_at = datetime.now(UTC).replace(microsecond=0)
    command = [
        str(scanner),
        "scan",
        "source",
        "--format",
        "json",
        "--all-packages",
        "--no-ignore",
        "--no-resolve",
        "--verbosity",
        "error",
        "--lockfile",
        sbom_relative,
    ]
    environment = {
        key: value
        for key, value in os.environ.items()
        if not key.upper().startswith("OSV_") and key not in {"GH_TOKEN", "GITHUB_TOKEN"}
    }
    try:
        completed = subprocess.run(
            command,
            cwd=stage_root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=300,
            check=False,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        fail(f"OSV scanner execution failed closed: {exc}")
    completed_at = datetime.now(UTC).replace(microsecond=0)
    if completed.returncode not in {0, 1}:
        message = completed.stderr.decode("utf-8", errors="replace")[:1000].strip()
        fail(
            f"OSV scanner/database/network was unavailable (exit {completed.returncode}): {message}"
        )
    try:
        response = json.loads(completed.stdout.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"OSV scanner did not emit valid machine-readable JSON: {exc}")
    if not isinstance(response, dict):
        fail("OSV scanner response must be a JSON object")
    normalized_response = json.loads(json.dumps(response))
    results = normalized_response.get("results")
    if not isinstance(results, list) or not results:
        fail("OSV scanner returned no result to bind to the exact SBOM")
    expected_absolute = str((stage_root / sbom_relative).resolve(strict=True))
    for result in results:
        source = result.get("source") if isinstance(result, dict) else None
        if (
            not isinstance(source, dict)
            or source.get("type") != "sbom"
            or source.get("path") not in {sbom_relative, expected_absolute}
        ):
            fail("OSV scanner source path differs from the exact scanned SBOM")
        source["path"] = sbom_relative
    return normalized_response, completed.returncode, queried_at, completed_at


def _property_value(component: dict[str, Any], name: str) -> str:
    properties = _component_properties(component)
    value = properties.get(name)
    if value is None:
        fail(f"CycloneDX root component is missing {name}")
    return value


def _artifact_rows_from_sbom(
    sbom: dict[str, Any], rid: str
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    components = sbom.get("components")
    if not isinstance(components, list):
        fail("CycloneDX components must be a list")
    rows: list[dict[str, Any]] = []
    exact_components: list[dict[str, Any]] = []
    for component in components:
        if not isinstance(component, dict) or component.get("type") != "file":
            continue
        properties = _component_properties(component)
        if properties.get("chummer:rid") != rid:
            fail("CycloneDX artifact component has the wrong RID")
        relative = properties.get("chummer:relative-path")
        size = properties.get("chummer:size-bytes")
        hashes = component.get("hashes")
        if (
            relative not in _expected_artifact_paths(rid)
            or not isinstance(size, str)
            or not size.isdigit()
            or int(size) < 1
            or not isinstance(hashes, list)
            or len(hashes) != 1
            or not isinstance(hashes[0], dict)
            or set(hashes[0]) != {"alg", "content"}
            or hashes[0].get("alg") != "SHA-256"
        ):
            fail("CycloneDX artifact binding is malformed")
        digest = require_sha256(hashes[0].get("content"), "CycloneDX artifact digest")
        if str(int(size)) != size:
            fail("CycloneDX artifact size is not canonical")
        expected_component = {
            "bom-ref": f"artifact:{rid}:{digest}",
            "hashes": [{"alg": "SHA-256", "content": digest}],
            "name": PurePosixPath(relative).name,
            "properties": [
                {"name": "chummer:relative-path", "value": relative},
                {"name": "chummer:rid", "value": rid},
                {"name": "chummer:size-bytes", "value": size},
            ],
            "type": "file",
        }
        if component != expected_component:
            fail("CycloneDX artifact component schema or identity differs")
        rows.append(
            {
                "path": relative,
                "sha256": digest,
                "sizeBytes": int(size),
            }
        )
        exact_components.append(expected_component)
    rows.sort(key=lambda row: row["path"])
    exact_components.sort(key=lambda row: row["bom-ref"])
    if [row["path"] for row in rows] != sorted(_expected_artifact_paths(rid)):
        fail(f"CycloneDX {rid} artifact component set is not exact")
    return rows, exact_components


def _sbom_root(sbom: dict[str, Any]) -> dict[str, Any]:
    metadata = sbom.get("metadata")
    component = metadata.get("component") if isinstance(metadata, dict) else None
    if not isinstance(component, dict):
        fail("CycloneDX metadata root component is missing")
    return component


def validate_sbom(
    sbom: dict[str, Any], *, rid: str, version: str, source_commit: str
) -> tuple[list[dict[str, str]], list[dict[str, Any]]]:
    rid = require_rid(rid)
    version = require_version(version)
    source_commit = require_commit(source_commit)
    _exact_object(
        sbom,
        {
            "$schema",
            "bomFormat",
            "components",
            "compositions",
            "dependencies",
            "metadata",
            "serialNumber",
            "specVersion",
            "version",
        },
        "CycloneDX document",
    )
    if (
        sbom.get("$schema") != CYCLONEDX_SCHEMA
        or sbom.get("bomFormat") != "CycloneDX"
        or sbom.get("specVersion") != CYCLONEDX_SPEC_VERSION
        or _exact_integer(sbom.get("version"), "CycloneDX version") != 1
    ):
        fail("CycloneDX document identity is not exact")

    metadata = _exact_object(
        sbom.get("metadata"),
        {"component", "properties", "tools"},
        "CycloneDX metadata",
    )
    metadata_properties = _component_properties(metadata)
    expected_metadata_property_names = {
        "chummer:contract-name",
        "chummer:contract-version",
        "chummer:normalized-rid-graph",
        "chummer:normalized-rid-graph-authority-path",
        "chummer:normalized-rid-graph-authority-sha256",
        "chummer:normalized-rid-graph-sha256",
        "chummer:target-graph",
    }
    if set(metadata_properties) != expected_metadata_property_names:
        fail("CycloneDX metadata property schema differs")
    graph_text = metadata_properties["chummer:normalized-rid-graph"]
    try:
        graph_value = json.loads(graph_text)
    except json.JSONDecodeError as exc:
        fail(f"CycloneDX normalized RID graph is invalid JSON: {exc}")
    if compact_json_text(graph_value) != graph_text:
        fail("CycloneDX normalized RID graph is not canonically serialized")
    graph = _validate_normalized_rid_graph(graph_value, rid)
    graph_authority_path, graph_authority_sha = _bind_graph_to_source_authority(
        graph, rid
    )
    graph_sha = compact_json_sha256(graph)
    expected_metadata_properties = [
        {"name": "chummer:contract-name", "value": SBOM_CONTRACT},
        {"name": "chummer:contract-version", "value": str(CONTRACT_VERSION)},
        {"name": "chummer:normalized-rid-graph", "value": graph_text},
        {
            "name": "chummer:normalized-rid-graph-authority-path",
            "value": graph_authority_path,
        },
        {
            "name": "chummer:normalized-rid-graph-authority-sha256",
            "value": graph_authority_sha,
        },
        {"name": "chummer:normalized-rid-graph-sha256", "value": graph_sha},
        {"name": "chummer:target-graph", "value": graph["targetName"]},
    ]
    _require_exact_json(
        metadata.get("properties"),
        expected_metadata_properties,
        "CycloneDX metadata contract and normalized graph authority",
    )

    generator_sha = _generator_source_sha256()
    tools = _exact_object(
        metadata.get("tools"), {"components"}, "CycloneDX metadata tools"
    )
    expected_tool = {
        "hashes": [{"alg": "SHA-256", "content": generator_sha}],
        "name": GENERATOR_NAME,
        "type": "application",
        "version": GENERATOR_VERSION,
    }
    _require_exact_json(
        tools.get("components"), [expected_tool], "CycloneDX generator tool authority"
    )

    root = _exact_object(
        metadata.get("component"),
        {"bom-ref", "name", "properties", "type", "version"},
        "CycloneDX root component",
    )
    root_ref = f"application:chummer-avalonia:{version}:{rid}"
    expected_root = {
        "bom-ref": root_ref,
        "name": "Chummer.Avalonia",
        "properties": [
            {"name": "chummer:channel", "value": "preview"},
            {
                "name": "chummer:project-assets-sha256",
                "value": graph["projectAssetsSha256"],
            },
            {"name": "chummer:rid", "value": rid},
            {"name": "chummer:source-commit", "value": source_commit},
        ],
        "type": "application",
        "version": version,
    }
    _require_exact_json(root, expected_root, "CycloneDX root release identity differs")
    require_sha256(
        graph["projectAssetsSha256"],
        "CycloneDX project assets digest",
    )

    graph_components, packages, dependencies, ref_by_key = _project_normalized_rid_graph(
        graph, rid
    )
    artifacts, artifact_components = _artifact_rows_from_sbom(sbom, rid)
    expected_components = sorted(
        [*graph_components, *artifact_components], key=lambda row: row["bom-ref"]
    )
    _require_exact_json(
        sbom.get("components"),
        expected_components,
        "CycloneDX components versus replayed RID graph and artifacts",
    )
    _require_exact_json(
        sbom_package_rows(sbom),
        packages,
        "CycloneDX NuGet packages versus replayed RID graph",
    )

    expected_dependencies = [*dependencies]
    expected_dependencies.append(
        {"dependsOn": sorted(ref_by_key.values()), "ref": root_ref}
    )
    expected_dependencies.sort(key=lambda row: row["ref"])
    _require_exact_json(
        sbom.get("dependencies"),
        expected_dependencies,
        "CycloneDX dependencies versus replayed RID graph",
    )
    expected_compositions = [
        {
            "aggregate": "complete",
            "assemblies": [row["bom-ref"] for row in expected_components],
            "bom-ref": root_ref,
        }
    ]
    _require_exact_json(
        sbom.get("compositions"),
        expected_compositions,
        "CycloneDX composition versus exact component set",
    )
    expected_serial = _sbom_serial_number(
        artifact_components=artifact_components,
        generator_sha256=generator_sha,
        graph=graph,
        graph_authority_sha256=graph_authority_sha,
        rid=rid,
        source_commit=source_commit,
        version=version,
    )
    if sbom.get("serialNumber") != expected_serial:
        fail("CycloneDX serial derivation differs from the exact authority")
    return packages, artifacts


def _manifest_artifacts(
    stage_root: Path,
    manifest: dict[str, Any],
    rid: str,
    *,
    require_artifact_bytes: bool,
) -> list[dict[str, Any]]:
    platform = "windows" if rid == "win-x64" else "linux"
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("canonical manifest artifacts must be a list")
    matches = [
        row
        for row in rows
        if isinstance(row, dict)
        and row.get("head") == "avalonia"
        and row.get("platform") == platform
        and row.get("rid") == rid
        and row.get("kind") == "installer"
    ]
    if len(matches) != 1:
        fail(f"canonical manifest must contain one exact avalonia/{platform}/{rid} installer")
    row = matches[0]
    expected_paths = _expected_artifact_paths(rid)
    descriptors: list[dict[str, Any]] = []
    fields = [("fileName", "sha256", "sizeBytes", expected_paths[0])]
    if rid == "win-x64":
        fields.append(("payloadFileName", "payloadSha256", "payloadSizeBytes", expected_paths[1]))
    for name_field, digest_field, size_field, relative in fields:
        path = stage_root / relative
        if row.get(name_field) != PurePosixPath(relative).name:
            fail(f"canonical manifest {rid} {name_field} differs from the exact artifact")
        digest = require_sha256(row.get(digest_field), f"canonical manifest {rid} {digest_field}")
        size = row.get(size_field)
        if isinstance(size, bool) or not isinstance(size, int) or size < 1:
            fail(f"canonical manifest {rid} {size_field} must be a positive integer")
        if path.exists() or path.is_symlink():
            if sha256_file(path) != digest or _regular_file_size(path) != size:
                fail(f"canonical manifest {rid} artifact bytes changed or differ: {relative}")
        elif require_artifact_bytes:
            fail(f"canonical manifest {rid} artifact bytes are missing: {relative}")
        descriptors.append({"path": relative, "sha256": digest, "sizeBytes": size})
    return sorted(descriptors, key=lambda item: item["path"])


def generate_rid_evidence(
    *,
    stage_root: Path,
    assets_path: Path,
    scanner: Path,
    rid: str,
    version: str,
    source_commit: str,
    artifacts: dict[str, Path],
) -> dict[str, Any]:
    stage_root = stage_root.resolve(strict=True)
    rid = require_rid(rid)
    sbom_path = stage_root / SBOM_PATHS[rid]
    scan_path = stage_root / SCAN_PATHS[rid]
    if sbom_path.exists() or sbom_path.is_symlink() or scan_path.exists() or scan_path.is_symlink():
        fail(f"supply-chain evidence already exists for {rid}")
    sbom = generate_sbom(
        assets_path=assets_path,
        rid=rid,
        version=version,
        source_commit=source_commit,
        artifacts=artifacts,
    )
    write_new_json(sbom_path, sbom)
    sbom_sha = sha256_file(sbom_path)
    packages = sbom_package_rows(sbom)
    response, exit_code, queried_at, completed_at = _scan_sbom(
        stage_root=stage_root,
        sbom_relative=SBOM_PATHS[rid],
        scanner=scanner,
    )
    findings, blocked, unclassified = normalize_scanner_response(
        response, packages, SBOM_PATHS[rid]
    )
    if (exit_code == 0) != (not findings):
        fail(f"OSV scanner exit status and vulnerability result disagree for {rid}")
    if blocked:
        fail(
            f"OSV scanner found high/critical vulnerabilities for {rid}: "
            + ", ".join(
                f"{row['package']}@{row['version']}:{row['advisoryId']}"
                for row in blocked
            )
        )
    if unclassified:
        fail(
            f"OSV scanner returned vulnerabilities without a trustworthy severity for {rid}: "
            + ", ".join(row["advisoryId"] for row in unclassified)
        )
    latest_modified = max(
        (
            row["advisoryModifiedAt"]
            for row in findings
            if isinstance(row.get("advisoryModifiedAt"), str)
        ),
        default=None,
    )
    response_digest = compact_json_sha256(response)
    package_set_digest = compact_json_sha256(packages)
    receipt = {
        "advisoryProvenance": {
            "completedAt": utc_text(completed_at),
            "freshUntil": utc_text(completed_at + ADVISORY_FRESHNESS),
            "latestAdvisoryModifiedAt": latest_modified,
            "mode": OSV_QUERY_MODE,
            "normalization": OSV_RESPONSE_NORMALIZATION,
            "packageQuerySetSha256": package_set_digest,
            "queriedAt": utc_text(queried_at),
            "reproducible": False,
            "responseSha256": response_digest,
            "source": OSV_DATA_SOURCE,
        },
        "blockedFindings": [],
        "contractName": SCAN_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "findings": findings,
        "legacyAlertAssertions": list(LEGACY_ALERT_ASSERTIONS),
        "packages": packages,
        "release": {"channel": "preview", "version": version},
        "response": response,
        "sbom": {
            "path": SBOM_PATHS[rid],
            "serialNumber": sbom["serialNumber"],
            "sha256": sbom_sha,
        },
        "scanner": {
            "binarySha256": OSV_SCANNER_SHA256,
            "commit": OSV_SCANNER_COMMIT,
            "exitCode": exit_code,
            "invocation": [
                "osv-scanner",
                "scan",
                "source",
                "--format",
                "json",
                "--all-packages",
                "--no-ignore",
                "--no-resolve",
                "--verbosity",
                "error",
                "--lockfile",
                SBOM_PATHS[rid],
            ],
            "name": OSV_SCANNER_NAME,
            "version": OSV_SCANNER_VERSION,
        },
        "status": "passed",
        "tuple": {
            "head": "avalonia",
            "platform": "windows" if rid == "win-x64" else "linux",
            "rid": rid,
        },
        "unclassifiedFindings": [],
    }
    write_new_json(scan_path, receipt)
    return receipt


def _validate_recorded_findings_shape(value: object, rid: str) -> None:
    if not isinstance(value, list):
        fail(f"{rid} recorded vulnerability findings must be a list")
    for index, finding_value in enumerate(value):
        finding = _exact_object(
            finding_value,
            {
                "advisoryId",
                "advisoryModifiedAt",
                "aliases",
                "cvssScore",
                "fixedVersions",
                "package",
                "severity",
                "version",
            },
            f"{rid} recorded vulnerability finding[{index}]",
        )
        _exact_string(finding.get("advisoryId"), f"{rid} finding advisoryId")
        modified = finding.get("advisoryModifiedAt")
        if modified is not None:
            parse_utc(modified, f"{rid} finding advisoryModifiedAt")
        for field in ("aliases", "fixedVersions"):
            values = finding.get(field)
            if (
                not isinstance(values, list)
                or any(not isinstance(item, str) or not item for item in values)
                or values != sorted(set(values))
            ):
                fail(f"{rid} finding {field} is not an exact sorted string set")
        score = finding.get("cvssScore")
        if score is not None and (
            type(score) is not float or not math.isfinite(score) or not 0 <= score <= 10
        ):
            fail(f"{rid} finding cvssScore must be null or an exact JSON number")
        if finding.get("severity") not in {"low", "medium", "high", "critical"}:
            fail(f"{rid} recorded finding has an invalid passing severity")
        _exact_string(finding.get("package"), f"{rid} finding package")
        _exact_string(finding.get("version"), f"{rid} finding version")


def _validate_scan_receipt(
    *,
    stage_root: Path,
    rid: str,
    version: str,
    source_commit: str,
    manifest: dict[str, Any],
    now: datetime,
    require_artifact_bytes: bool,
    scanner_binary: Path | None = None,
    release_authoritative: bool = False,
) -> dict[str, Any]:
    if release_authoritative and scanner_binary is None:
        fail(f"{rid} release-authoritative verification requires the pinned scanner")
    sbom_path = stage_root / SBOM_PATHS[rid]
    scan_path = stage_root / SCAN_PATHS[rid]
    sbom = read_json(sbom_path, f"{rid} CycloneDX SBOM")
    if sbom_path.read_bytes() != canonical_json_bytes(sbom):
        fail(f"{rid} CycloneDX SBOM is not deterministically serialized")
    packages, artifact_rows = validate_sbom(
        sbom, rid=rid, version=version, source_commit=source_commit
    )
    manifest_rows = _manifest_artifacts(
        stage_root,
        manifest,
        rid,
        require_artifact_bytes=require_artifact_bytes,
    )
    if artifact_rows != manifest_rows:
        fail(f"{rid} CycloneDX artifact hashes differ from the canonical manifest")
    receipt = read_json(scan_path, f"{rid} vulnerability receipt")
    if scan_path.read_bytes() != canonical_json_bytes(receipt):
        fail(f"{rid} vulnerability receipt is not deterministically serialized")
    _exact_object(
        receipt,
        {
            "advisoryProvenance",
            "blockedFindings",
            "contractName",
            "contractVersion",
            "findings",
            "legacyAlertAssertions",
            "packages",
            "release",
            "response",
            "sbom",
            "scanner",
            "status",
            "tuple",
            "unclassifiedFindings",
        },
        f"{rid} vulnerability receipt",
    )
    contract_version = _exact_integer(
        receipt.get("contractVersion"), f"{rid} vulnerability receipt contractVersion"
    )
    expected_tuple = {
        "head": "avalonia",
        "platform": "windows" if rid == "win-x64" else "linux",
        "rid": rid,
    }
    if (
        receipt.get("contractName") != SCAN_CONTRACT
        or contract_version != CONTRACT_VERSION
        or receipt.get("status") != "passed"
        or receipt.get("release") != {"channel": "preview", "version": version}
        or receipt.get("tuple") != expected_tuple
        or receipt.get("legacyAlertAssertions") != list(LEGACY_ALERT_ASSERTIONS)
        or receipt.get("blockedFindings") != []
        or receipt.get("unclassifiedFindings") != []
        or receipt.get("packages") != packages
    ):
        fail(f"{rid} vulnerability receipt contract or release identity differs")
    if receipt.get("sbom") != {
        "path": SBOM_PATHS[rid],
        "serialNumber": sbom["serialNumber"],
        "sha256": sha256_file(sbom_path),
    }:
        fail(f"{rid} vulnerability receipt is not bound to the exact SBOM")
    scanner = receipt.get("scanner")
    expected_invocation = [
        "osv-scanner",
        "scan",
        "source",
        "--format",
        "json",
        "--all-packages",
        "--no-ignore",
        "--no-resolve",
        "--verbosity",
        "error",
        "--lockfile",
        SBOM_PATHS[rid],
    ]
    if not isinstance(scanner, dict) or scanner != {
        "binarySha256": OSV_SCANNER_SHA256,
        "commit": OSV_SCANNER_COMMIT,
        "exitCode": scanner.get("exitCode") if isinstance(scanner, dict) else None,
        "invocation": expected_invocation,
        "name": OSV_SCANNER_NAME,
        "version": OSV_SCANNER_VERSION,
    }:
        fail(f"{rid} vulnerability receipt scanner authority differs")
    if scanner.get("exitCode") not in {0, 1} or type(scanner.get("exitCode")) is not int:
        fail(f"{rid} vulnerability receipt scanner exit code is invalid")
    provenance = receipt.get("advisoryProvenance")
    provenance = _exact_object(
        provenance,
        {
            "completedAt",
            "freshUntil",
            "latestAdvisoryModifiedAt",
            "mode",
            "normalization",
            "packageQuerySetSha256",
            "queriedAt",
            "reproducible",
            "responseSha256",
            "source",
        },
        f"{rid} vulnerability receipt advisory provenance",
    )
    queried_at = parse_utc(provenance.get("queriedAt"), f"{rid} advisory queriedAt")
    completed_at = parse_utc(provenance.get("completedAt"), f"{rid} advisory completedAt")
    fresh_until = parse_utc(provenance.get("freshUntil"), f"{rid} advisory freshUntil")
    require_sha256(
        provenance.get("packageQuerySetSha256"),
        f"{rid} advisory package query-set digest",
    )
    require_sha256(
        provenance.get("responseSha256"), f"{rid} advisory response digest"
    )
    if (
        provenance.get("source") != OSV_DATA_SOURCE
        or provenance.get("mode") != OSV_QUERY_MODE
        or provenance.get("normalization") != OSV_RESPONSE_NORMALIZATION
        or provenance.get("reproducible") is not False
        or queried_at > completed_at
        or completed_at > now + MAX_FUTURE_SKEW
        or fresh_until != completed_at + ADVISORY_FRESHNESS
        or now > fresh_until
        or provenance.get("packageQuerySetSha256") != compact_json_sha256(packages)
        or provenance.get("responseSha256") != compact_json_sha256(receipt.get("response"))
    ):
        fail(f"{rid} advisory response provenance is stale, mutable, or unbound")
    response = receipt.get("response")
    if not isinstance(response, dict):
        fail(f"{rid} advisory response is missing")
    findings, blocked, unclassified = normalize_scanner_response(
        response, packages, SBOM_PATHS[rid]
    )
    _validate_recorded_findings_shape(receipt.get("findings"), rid)
    latest_modified = max(
        (
            row["advisoryModifiedAt"]
            for row in findings
            if isinstance(row.get("advisoryModifiedAt"), str)
        ),
        default=None,
    )
    if (
        (scanner["exitCode"] == 0) != (not findings)
        or provenance.get("latestAdvisoryModifiedAt") != latest_modified
        or blocked
        or unclassified
    ):
        fail(f"{rid} vulnerability findings are blocked, unclassified, or drifted")
    _require_exact_json(
        receipt.get("findings"), findings, f"{rid} normalized vulnerability findings"
    )
    if release_authoritative:
        assert scanner_binary is not None
        live_response, live_exit_code, _, _ = _scan_sbom(
            stage_root=stage_root,
            sbom_relative=SBOM_PATHS[rid],
            scanner=scanner_binary,
        )
        live_findings, live_blocked, live_unclassified = normalize_scanner_response(
            live_response, packages, SBOM_PATHS[rid]
        )
        if (live_exit_code == 0) != (not live_findings):
            fail(
                f"{rid} live OSV scanner exit status and vulnerability result disagree"
            )
        if live_blocked:
            fail(f"{rid} live OSV scanner found release-blocking vulnerabilities")
        if live_unclassified:
            fail(f"{rid} live OSV scanner found unclassified vulnerabilities")
        _require_exact_json(
            live_findings,
            findings,
            f"{rid} pinned live scanner findings versus recorded findings",
        )
    return {
        "advisoryCompletedAt": utc_text(completed_at),
        "advisoryFreshUntil": utc_text(fresh_until),
        "artifactBindings": artifact_rows,
        "packageQuerySetSha256": compact_json_sha256(packages),
        "scan": {
            "path": SCAN_PATHS[rid],
            "sha256": sha256_file(scan_path),
            "sizeBytes": _regular_file_size(scan_path),
        },
        "sbom": {
            "path": SBOM_PATHS[rid],
            "sha256": sha256_file(sbom_path),
            "sizeBytes": _regular_file_size(sbom_path),
        },
        "tuple": expected_tuple,
    }


def _exact_evidence_files(stage_root: Path, *, include_gate: bool) -> None:
    expected = {
        *(SBOM_PATHS.values()),
        *(SCAN_PATHS.values()),
    }
    if include_gate:
        expected.add(GATE_PATH)
    actual: set[str] = set()
    for relative_root in ("release-evidence/sbom", "release-evidence/vulnerability"):
        directory = stage_root / relative_root
        if directory.is_symlink() or not directory.is_dir():
            fail(f"supply-chain evidence directory is missing: {relative_root}")
        for child in directory.iterdir():
            if child.is_symlink() or not child.is_file():
                fail(f"supply-chain evidence contains a non-regular entry: {child}")
            actual.add(child.relative_to(stage_root).as_posix())
    if include_gate:
        gate = stage_root / GATE_PATH
        if gate.is_symlink() or not gate.is_file():
            fail("aggregate supply-chain gate is missing")
        actual.add(GATE_PATH)
    if actual != expected:
        fail(
            "supply-chain evidence set is missing or contains extras: "
            f"missing={sorted(expected - actual)}, extra={sorted(actual - expected)}"
        )


def _validate_evidence_descriptor(value: object, label: str) -> None:
    descriptor = _exact_object(value, {"path", "sha256", "sizeBytes"}, label)
    _exact_string(descriptor.get("path"), f"{label} path")
    require_sha256(descriptor.get("sha256"), f"{label} digest")
    _exact_integer(descriptor.get("sizeBytes"), f"{label} sizeBytes", minimum=1)


def _validate_gate_shape(gate: dict[str, Any]) -> None:
    _exact_object(
        gate,
        {
            "advisoryFreshUntil",
            "contractName",
            "contractVersion",
            "legacyAlertAssertions",
            "release",
            "scanner",
            "sourceCommit",
            "status",
            "tuples",
            "verification",
        },
        "aggregate supply-chain gate",
    )
    _exact_integer(
        gate.get("contractVersion"), "aggregate supply-chain gate contractVersion"
    )
    release = _exact_object(
        gate.get("release"), {"channel", "version"}, "aggregate gate release"
    )
    _exact_string(release.get("channel"), "aggregate gate release channel")
    _exact_string(release.get("version"), "aggregate gate release version")
    scanner = _exact_object(
        gate.get("scanner"),
        {"binarySha256", "commit", "name", "version"},
        "aggregate gate scanner",
    )
    require_sha256(scanner.get("binarySha256"), "aggregate gate scanner digest")
    require_commit(scanner.get("commit"), "aggregate gate scanner commit")
    _exact_string(scanner.get("name"), "aggregate gate scanner name")
    _exact_string(scanner.get("version"), "aggregate gate scanner version")
    verification = _exact_object(
        gate.get("verification"),
        {
            "finalizationMode",
            "offlineReplayMode",
            "releaseAuthorityRequiresLiveScanner",
        },
        "aggregate gate verification posture",
    )
    if verification.get("finalizationMode") not in {
        LIVE_VERIFICATION_MODE,
        STRUCTURAL_VERIFICATION_MODE,
    }:
        fail("aggregate gate finalization mode is invalid")
    if verification.get("offlineReplayMode") != STRUCTURAL_VERIFICATION_MODE:
        fail("aggregate gate offline replay mode is invalid")
    if verification.get("releaseAuthorityRequiresLiveScanner") is not True:
        fail("aggregate gate must require live scanner reexecution for release authority")
    tuples = gate.get("tuples")
    if not isinstance(tuples, list) or len(tuples) != len(ACTIVE_TUPLES):
        fail("aggregate gate tuple set is not exact")
    for index, tuple_value in enumerate(tuples):
        row = _exact_object(
            tuple_value,
            {
                "advisoryCompletedAt",
                "advisoryFreshUntil",
                "artifactBindings",
                "packageQuerySetSha256",
                "scan",
                "sbom",
                "tuple",
            },
            f"aggregate gate tuple[{index}]",
        )
        parse_utc(row.get("advisoryCompletedAt"), f"aggregate gate tuple[{index}] completedAt")
        parse_utc(row.get("advisoryFreshUntil"), f"aggregate gate tuple[{index}] freshUntil")
        require_sha256(
            row.get("packageQuerySetSha256"),
            f"aggregate gate tuple[{index}] package query-set digest",
        )
        identity = _exact_object(
            row.get("tuple"),
            {"head", "platform", "rid"},
            f"aggregate gate tuple[{index}] identity",
        )
        for field in ("head", "platform", "rid"):
            _exact_string(identity.get(field), f"aggregate gate tuple[{index}] {field}")
        bindings = row.get("artifactBindings")
        if not isinstance(bindings, list) or not bindings:
            fail(f"aggregate gate tuple[{index}] artifact bindings must be non-empty")
        for binding_index, binding in enumerate(bindings):
            _validate_evidence_descriptor(
                binding,
                f"aggregate gate tuple[{index}] artifact binding[{binding_index}]",
            )
        _validate_evidence_descriptor(row.get("scan"), f"aggregate gate tuple[{index}] scan")
        _validate_evidence_descriptor(row.get("sbom"), f"aggregate gate tuple[{index}] SBOM")


def finalize_gate(
    *,
    stage_root: Path,
    version: str,
    source_commit: str,
    now: datetime | None = None,
    require_artifact_bytes: bool = True,
    scanner: Path | None = None,
    release_authoritative: bool = False,
) -> dict[str, Any]:
    stage_root = stage_root.resolve(strict=True)
    version = require_version(version)
    source_commit = require_commit(source_commit)
    now = (now or datetime.now(UTC)).astimezone(UTC)
    gate_path = stage_root / GATE_PATH
    if gate_path.exists() or gate_path.is_symlink():
        fail("aggregate supply-chain gate already exists")
    _exact_evidence_files(stage_root, include_gate=False)
    manifest = read_json(stage_root / "RELEASE_CHANNEL.generated.json", "canonical manifest")
    tuples = [
        _validate_scan_receipt(
            stage_root=stage_root,
            rid=rid,
            version=version,
            source_commit=source_commit,
            manifest=manifest,
            now=now,
            require_artifact_bytes=require_artifact_bytes,
            scanner_binary=scanner,
            release_authoritative=release_authoritative,
        )
        for _, _, rid in ACTIVE_TUPLES
    ]
    payload = {
        "advisoryFreshUntil": min(row["advisoryFreshUntil"] for row in tuples),
        "contractName": GATE_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "legacyAlertAssertions": list(LEGACY_ALERT_ASSERTIONS),
        "release": {"channel": "preview", "version": version},
        "scanner": {
            "binarySha256": OSV_SCANNER_SHA256,
            "commit": OSV_SCANNER_COMMIT,
            "name": OSV_SCANNER_NAME,
            "version": OSV_SCANNER_VERSION,
        },
        "sourceCommit": source_commit,
        "status": "passed",
        "tuples": tuples,
        "verification": {
            "finalizationMode": (
                LIVE_VERIFICATION_MODE
                if release_authoritative
                else STRUCTURAL_VERIFICATION_MODE
            ),
            "offlineReplayMode": STRUCTURAL_VERIFICATION_MODE,
            "releaseAuthorityRequiresLiveScanner": True,
        },
    }
    _validate_gate_shape(payload)
    write_new_json(gate_path, payload)
    return payload


def verify_gate(
    *,
    stage_root: Path,
    version: str,
    source_commit: str,
    now: datetime | None = None,
    require_artifact_bytes: bool = True,
    scanner: Path | None = None,
    release_authoritative: bool = False,
) -> dict[str, Any]:
    stage_root = stage_root.resolve(strict=True)
    now = (now or datetime.now(UTC)).astimezone(UTC)
    version = require_version(version)
    source_commit = require_commit(source_commit)
    _exact_evidence_files(stage_root, include_gate=True)
    gate_path = stage_root / GATE_PATH
    gate = read_json(gate_path, "aggregate supply-chain gate")
    if gate_path.read_bytes() != canonical_json_bytes(gate):
        fail("aggregate supply-chain gate is not deterministically serialized")
    _validate_gate_shape(gate)
    gate_verification = gate["verification"]
    if (
        release_authoritative
        and gate_verification["finalizationMode"] != LIVE_VERIFICATION_MODE
    ):
        fail("release-authoritative verification requires a live-finalized gate")
    manifest = read_json(stage_root / "RELEASE_CHANNEL.generated.json", "canonical manifest")
    tuples = [
        _validate_scan_receipt(
            stage_root=stage_root,
            rid=rid,
            version=version,
            source_commit=source_commit,
            manifest=manifest,
            now=now,
            require_artifact_bytes=require_artifact_bytes,
            scanner_binary=scanner,
            release_authoritative=release_authoritative,
        )
        for _, _, rid in ACTIVE_TUPLES
    ]
    expected = {
        "advisoryFreshUntil": min(row["advisoryFreshUntil"] for row in tuples),
        "contractName": GATE_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "legacyAlertAssertions": list(LEGACY_ALERT_ASSERTIONS),
        "release": {"channel": "preview", "version": version},
        "scanner": {
            "binarySha256": OSV_SCANNER_SHA256,
            "commit": OSV_SCANNER_COMMIT,
            "name": OSV_SCANNER_NAME,
            "version": OSV_SCANNER_VERSION,
        },
        "sourceCommit": source_commit,
        "status": "passed",
        "tuples": tuples,
        "verification": gate_verification,
    }
    _require_exact_json(
        gate, expected, "aggregate supply-chain gate versus exact current RID evidence"
    )
    return gate


def content_bindings(stage_root: Path) -> dict[str, Any]:
    stage_root = stage_root.resolve(strict=True)
    return {
        "gate": {
            "path": GATE_PATH,
            "sha256": sha256_file(stage_root / GATE_PATH),
            "sizeBytes": _regular_file_size(stage_root / GATE_PATH),
        },
        "scans": [
            {
                "path": SCAN_PATHS[rid],
                "sha256": sha256_file(stage_root / SCAN_PATHS[rid]),
                "sizeBytes": _regular_file_size(stage_root / SCAN_PATHS[rid]),
            }
            for _, _, rid in ACTIVE_TUPLES
        ],
        "sboms": [
            {
                "path": SBOM_PATHS[rid],
                "sha256": sha256_file(stage_root / SBOM_PATHS[rid]),
                "sizeBytes": _regular_file_size(stage_root / SBOM_PATHS[rid]),
            }
            for _, _, rid in ACTIVE_TUPLES
        ],
    }


def _artifact_arguments(values: list[str]) -> dict[str, Path]:
    artifacts: dict[str, Path] = {}
    for raw in values:
        if "=" not in raw:
            fail("--artifact must use portable-relative-path=/absolute/path")
        relative, absolute = raw.split("=", 1)
        path = Path(absolute)
        if relative in artifacts or not path.is_absolute():
            fail("--artifact paths must be unique and absolute")
        artifacts[relative] = path
    return artifacts


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    generate = subparsers.add_parser("generate")
    generate.add_argument("--stage-root", type=Path, required=True)
    generate.add_argument("--project-assets", type=Path, required=True)
    generate.add_argument("--scanner", type=Path, required=True)
    generate.add_argument("--rid", required=True)
    generate.add_argument("--version", required=True)
    generate.add_argument("--source-commit", required=True)
    generate.add_argument("--artifact", action="append", default=[], required=True)
    finalize = subparsers.add_parser("finalize")
    finalize.add_argument("--stage-root", type=Path, required=True)
    finalize.add_argument("--version", required=True)
    finalize.add_argument("--source-commit", required=True)
    finalize.add_argument("--scanner", type=Path, required=True)
    verify = subparsers.add_parser("verify")
    verify.add_argument("--stage-root", type=Path, required=True)
    verify.add_argument("--version", required=True)
    verify.add_argument("--source-commit", required=True)
    verify_mode = verify.add_mutually_exclusive_group(required=True)
    verify_mode.add_argument("--scanner", type=Path)
    verify_mode.add_argument("--structural-only", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        if args.command == "generate":
            generate_rid_evidence(
                stage_root=args.stage_root,
                assets_path=args.project_assets,
                scanner=args.scanner,
                rid=args.rid,
                version=args.version,
                source_commit=args.source_commit,
                artifacts=_artifact_arguments(args.artifact),
            )
        elif args.command == "finalize":
            finalize_gate(
                stage_root=args.stage_root,
                version=args.version,
                source_commit=args.source_commit,
                scanner=args.scanner,
                release_authoritative=True,
            )
        else:
            verify_gate(
                stage_root=args.stage_root,
                version=args.version,
                source_commit=args.source_commit,
                scanner=args.scanner,
                release_authoritative=not args.structural_only,
            )
    except SupplyChainError as exc:
        print(f"preview supply-chain gate failed: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
