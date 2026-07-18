#!/usr/bin/env python3
"""Machine-readable verification-mode and release-input contract."""

from __future__ import annotations

import argparse
import json
import os
import re
import stat
import tempfile
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
        else:
            inspect_proof(args.proof)
    except ContractError as exc:
        print(f"verify-mode-contract:error: {exc}", file=os.sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
