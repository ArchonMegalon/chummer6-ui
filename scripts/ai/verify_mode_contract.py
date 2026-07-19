#!/usr/bin/env python3
"""Machine-readable verification-mode and release-input contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import tempfile
import xml.etree.ElementTree as ET
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


CONTRACT_NAME = "chummer6-ui.verification-report"
CONTRACT_VERSION = 1
MODES = ("scaffold", "slice", "integration", "release")
PASS_STATUSES = {"pass", "passed", "ready"}
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
FIXTURE_PARTS = {"fixture", "fixtures", "mock", "mocks", "sample", "samples", "example", "examples"}


class ContractError(ValueError):
    pass


def now_utc() -> datetime:
    return datetime.now(UTC)


def now_iso() -> str:
    return now_utc().replace(microsecond=0).isoformat().replace("+00:00", "Z")


def reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ContractError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def reject_nonfinite(value: str) -> None:
    raise ContractError(f"non-finite JSON number: {value}")


def load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(
            path.read_text(encoding="utf-8-sig"),
            object_pairs_hook=reject_duplicate_pairs,
            parse_constant=reject_nonfinite,
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"could not read exact JSON object {path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise ContractError(f"expected JSON object in {path}")
    return payload


def atomic_write(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_symlink():
        raise ContractError(f"refusing symlink report output: {path}")
    encoded = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    fd, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def require_mode(value: str) -> str:
    if value not in MODES:
        raise ContractError("CHUMMER_VERIFY_MODE must be scaffold, slice, integration, or release")
    return value


def validate_report(payload: dict[str, Any], mode: str) -> None:
    if payload.get("contractName") != CONTRACT_NAME or payload.get("contractVersion") != CONTRACT_VERSION:
        raise ContractError("verification report contract is invalid")
    if payload.get("mode") != mode:
        raise ContractError("verification report mode differs from this invocation")
    if not isinstance(payload.get("skips"), list):
        raise ContractError("verification report skips must be a list")


def start_report(output: Path, mode: str) -> None:
    atomic_write(
        output,
        {
            "completedAt": None,
            "contractName": CONTRACT_NAME,
            "contractVersion": CONTRACT_VERSION,
            "exitCode": None,
            "mode": mode,
            "skips": [],
            "startedAt": now_iso(),
            "status": "running",
        },
    )


def add_skip(output: Path, mode: str, code: str, detail: str) -> None:
    if not re.fullmatch(r"[a-z0-9][a-z0-9_.-]{1,79}", code):
        raise ContractError("skip code is not portable")
    if not detail.strip() or len(detail) > 1000:
        raise ContractError("skip detail is empty or too long")
    payload = load_json(output)
    validate_report(payload, mode)
    if payload.get("status") != "running":
        raise ContractError("cannot add a skip to a completed report")
    if any(isinstance(row, dict) and row.get("code") == code for row in payload["skips"]):
        raise ContractError(f"duplicate skip code: {code}")
    payload["skips"].append(
        {
            "code": code,
            "detail": detail.strip(),
            "recordedAt": now_iso(),
            "requiredInRelease": True,
        }
    )
    atomic_write(output, payload)


def finish_report(output: Path, mode: str, status_value: str, exit_code: int) -> None:
    payload = load_json(output)
    validate_report(payload, mode)
    if status_value not in {"passed", "failed"}:
        raise ContractError("report status must be passed or failed")
    if mode == "release" and status_value == "passed" and payload["skips"]:
        raise ContractError("release verification cannot pass with skipped proof")
    if status_value == "passed" and exit_code != 0:
        raise ContractError("a passing report must have exitCode 0")
    if status_value == "failed" and exit_code == 0:
        raise ContractError("a failing report must have a nonzero exitCode")
    payload["completedAt"] = now_iso()
    payload["exitCode"] = exit_code
    payload["status"] = status_value
    atomic_write(output, payload)


def exact_regular_file(path: Path, label: str) -> Path:
    if not path.is_absolute():
        raise ContractError(f"{label} must be an absolute path")
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise ContractError(f"{label} is unavailable: {path}") from exc
    if not stat.S_ISREG(metadata.st_mode) or path.is_symlink():
        raise ContractError(f"{label} must be a regular non-symlink file")
    return path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def exact_feed_inventory(feed: Path) -> list[dict[str, Any]]:
    if not feed.is_absolute() or feed.is_symlink() or not feed.is_dir():
        raise ContractError("published feed root must be an absolute non-symlink directory")
    if feed.resolve(strict=True) != feed:
        raise ContractError("published feed root must already be a physical canonical path")
    rows: list[dict[str, Any]] = []
    for path in sorted(feed.iterdir(), key=lambda value: value.name):
        try:
            metadata = path.lstat()
        except OSError as exc:
            raise ContractError(f"published feed entry is unavailable: {path.name}") from exc
        if (
            path.is_symlink()
            or not stat.S_ISREG(metadata.st_mode)
            or not path.name.casefold().endswith(".nupkg")
        ):
            raise ContractError("published feed must contain only regular non-symlink .nupkg files")
        rows.append(
            {
                "fileName": path.name,
                "sha256": sha256_file(path),
                "sizeBytes": metadata.st_size,
            }
        )
    if not rows:
        raise ContractError("published feed must contain at least one package")
    return rows


def feed_inventory_sha256(feed: Path) -> str:
    encoded = json.dumps(
        exact_feed_inventory(feed), sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def validate_feed_authority(
    config_path: Path,
    feed_root: Path,
    expected_config_sha256: str,
    expected_feed_sha256: str,
) -> None:
    config_path = exact_regular_file(config_path, "published NuGet.Config")
    if config_path.resolve(strict=True) != config_path:
        raise ContractError("published NuGet.Config must already be a physical canonical path")
    if not SHA256_RE.fullmatch(expected_config_sha256):
        raise ContractError("published NuGet.Config SHA-256 is invalid")
    if not SHA256_RE.fullmatch(expected_feed_sha256):
        raise ContractError("published feed inventory SHA-256 is invalid")
    if sha256_file(config_path) != expected_config_sha256:
        raise ContractError("published NuGet.Config digest differs from authority")
    if feed_inventory_sha256(feed_root) != expected_feed_sha256:
        raise ContractError("published feed inventory digest differs from authority")

    raw = config_path.read_bytes()
    if len(raw) > 64 * 1024 or b"<!DOCTYPE" in raw.upper() or b"<!ENTITY" in raw.upper():
        raise ContractError("published NuGet.Config XML is unsafe or exceeds the fixed bound")
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as exc:
        raise ContractError("published NuGet.Config is invalid XML") from exc
    if root.tag != "configuration" or root.attrib:
        raise ContractError("published NuGet.Config root is not exact")
    children = list(root)
    if [child.tag for child in children] != ["packageSources", "packageSourceMapping"]:
        raise ContractError(
            "published NuGet.Config must contain only exact packageSources and packageSourceMapping"
        )
    package_sources, mapping = children
    if package_sources.attrib or mapping.attrib:
        raise ContractError("published NuGet.Config sections must not carry attributes")
    source_children = list(package_sources)
    if len(source_children) != 2:
        raise ContractError("published NuGet.Config packageSources is not exact")
    clear, add = source_children
    if clear.tag != "clear" or clear.attrib or list(clear):
        raise ContractError("published NuGet.Config must clear every ambient package source")
    expected_source = {"key": "same-run-local-feed", "value": feed_root.as_posix()}
    if add.tag != "add" or add.attrib != expected_source or list(add):
        raise ContractError("published NuGet.Config package source differs from exact feed root")
    mapping_children = list(mapping)
    if len(mapping_children) != 1:
        raise ContractError("published NuGet.Config source mapping is not exact")
    source_mapping = mapping_children[0]
    if source_mapping.tag != "packageSource" or source_mapping.attrib != {
        "key": "same-run-local-feed"
    }:
        raise ContractError("published NuGet.Config source mapping authority differs")
    patterns = list(source_mapping)
    if (
        len(patterns) != 1
        or patterns[0].tag != "package"
        or patterns[0].attrib != {"pattern": "*"}
        or list(patterns[0])
    ):
        raise ContractError("published NuGet.Config must map every package to the exact feed")


def parse_utc(value: Any, label: str) -> datetime:
    if not isinstance(value, str) or not value.strip():
        raise ContractError(f"{label} is missing")
    token = value.strip()
    try:
        parsed = datetime.fromisoformat(token[:-1] + "+00:00" if token.endswith("Z") else token)
    except ValueError as exc:
        raise ContractError(f"{label} is not RFC3339") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ContractError(f"{label} must include a timezone")
    return parsed.astimezone(UTC)


def reject_fixture_path(path: Path, label: str) -> None:
    lowered = {part.casefold() for part in path.parts}
    if lowered & FIXTURE_PARTS:
        raise ContractError(f"{label} must not come from a fixture/example path")


def validate_release_inputs(proof_path: Path, manifest_target: str, max_age_seconds: int) -> None:
    proof_path = exact_regular_file(proof_path, "rule-environment proof")
    reject_fixture_path(proof_path, "rule-environment proof")
    proof = load_json(proof_path)
    status_value = str(proof.get("status") or "").strip().casefold()
    if status_value not in PASS_STATUSES:
        raise ContractError("rule-environment proof is not passing")
    if proof.get("fixture") is True or str(proof.get("sourceKind") or "").casefold() in {
        "fixture",
        "mock",
        "stub",
        "scaffold",
    }:
        raise ContractError("rule-environment proof identifies itself as fixture/stub evidence")
    generated = parse_utc(proof.get("generatedAt") or proof.get("generated_at"), "proof generatedAt")
    age = (now_utc() - generated).total_seconds()
    if age < -300:
        raise ContractError("rule-environment proof is generated in the future")
    if age > max_age_seconds:
        raise ContractError("rule-environment proof is stale")

    target = manifest_target.strip()
    if not target:
        raise ContractError("release verification requires an explicit manifest target")
    parsed = urlparse(target)
    if parsed.scheme:
        if parsed.scheme != "https" or not parsed.netloc or parsed.username or parsed.password:
            raise ContractError("release manifest URL must be an absolute credential-free HTTPS URL")
        if parsed.hostname in {"localhost", "127.0.0.1", "::1"}:
            raise ContractError("release manifest URL must not be a local fixture endpoint")
    else:
        manifest_path = exact_regular_file(Path(target), "release manifest")
        reject_fixture_path(manifest_path, "release manifest")


def inspect_proof(proof_path: Path) -> None:
    proof_path = exact_regular_file(proof_path, "rule-environment proof")
    proof = load_json(proof_path)
    if str(proof.get("status") or "").strip().casefold() not in PASS_STATUSES:
        raise ContractError("rule-environment proof is not passing")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    for command in ("start", "finish", "skip"):
        child = subparsers.add_parser(command)
        child.add_argument("--output", required=True, type=Path)
        child.add_argument("--mode", required=True, choices=MODES)
        if command == "finish":
            child.add_argument("--status", required=True, choices=("passed", "failed"))
            child.add_argument("--exit-code", required=True, type=int)
        if command == "skip":
            child.add_argument("--code", required=True)
            child.add_argument("--detail", required=True)
    release = subparsers.add_parser("validate-release-inputs")
    release.add_argument("--proof", required=True, type=Path)
    release.add_argument("--manifest-target", required=True)
    release.add_argument("--max-age-seconds", type=int, default=86_400)
    inspect = subparsers.add_parser("inspect-proof")
    inspect.add_argument("--proof", required=True, type=Path)
    feed = subparsers.add_parser("validate-feed-authority")
    feed.add_argument("--config", required=True, type=Path)
    feed.add_argument("--feed-root", required=True, type=Path)
    feed.add_argument("--config-sha256", required=True)
    feed.add_argument("--feed-sha256", required=True)
    digest = subparsers.add_parser("feed-inventory-sha256")
    digest.add_argument("--feed-root", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if args.command == "start":
            start_report(args.output, require_mode(args.mode))
        elif args.command == "skip":
            add_skip(args.output, require_mode(args.mode), args.code, args.detail)
        elif args.command == "finish":
            finish_report(
                args.output,
                require_mode(args.mode),
                args.status,
                args.exit_code,
            )
        elif args.command == "validate-release-inputs":
            if args.max_age_seconds < 60:
                raise ContractError("max proof age must be at least 60 seconds")
            validate_release_inputs(args.proof, args.manifest_target, args.max_age_seconds)
        elif args.command == "inspect-proof":
            inspect_proof(args.proof)
        elif args.command == "validate-feed-authority":
            validate_feed_authority(
                args.config,
                args.feed_root,
                args.config_sha256,
                args.feed_sha256,
            )
        else:
            print(feed_inventory_sha256(args.feed_root))
    except ContractError as exc:
        print(f"verify-mode-contract:error: {exc}", file=os.sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
