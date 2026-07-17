#!/usr/bin/env python3
"""Attach truthful native-vs-compatibility execution evidence to a Windows smoke receipt."""

from __future__ import annotations

import argparse
import json
import os
import tempfile
from pathlib import Path
from typing import Any


NATIVE_ENVIRONMENT = "native_windows"
COMPATIBILITY_ENVIRONMENTS = {"wine_compatibility", "windows_compatibility"}
ALLOWED_ENVIRONMENTS = {NATIVE_ENVIRONMENT, *COMPATIBILITY_ENVIRONMENTS, "not_executed"}


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def annotate(
    receipt_path: Path,
    *,
    execution_environment: str,
    runner: str,
    host_platform: str,
    host_kernel: str,
    evidence_source: str,
) -> dict[str, Any]:
    environment = normalize(execution_environment)
    normalized_runner = normalize(runner)
    normalized_host_platform = normalize(host_platform)
    normalized_source = normalize(evidence_source)

    if environment not in ALLOWED_ENVIRONMENTS:
        raise ValueError(f"unsupported Windows smoke execution environment: {environment or '<missing>'}")
    if not normalized_runner:
        raise ValueError("Windows smoke execution runner is required")
    if not normalized_host_platform:
        raise ValueError("Windows smoke host platform is required")
    if not normalized_source:
        raise ValueError("Windows smoke native-host evidence source is required")

    is_native_execution = environment == NATIVE_ENVIRONMENT
    is_native_windows_host = normalized_host_platform == "windows"
    if is_native_execution:
        if normalized_host_platform != "windows":
            raise ValueError("native_windows execution requires hostPlatform=windows")
        if "wine" in normalized_runner:
            raise ValueError("Wine cannot be recorded as native Windows execution")
        evidence_status = "verified"
        verification_scope = "native_windows_startup"
    elif environment in COMPATIBILITY_ENVIRONMENTS:
        if environment == "wine_compatibility" and "wine" not in normalized_runner:
            raise ValueError("wine_compatibility execution requires a Wine runner")
        evidence_status = "not_native"
        verification_scope = "windows_compatibility_startup"
    else:
        evidence_status = "not_executed"
        verification_scope = "not_executed"

    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise ValueError("Windows startup-smoke receipt must be a JSON object")

    payload["executionEnvironment"] = environment
    payload["verificationScope"] = verification_scope
    payload["nativeHostEvidence"] = {
        "contractName": "chummer6-ui.native_windows_host_evidence",
        "status": evidence_status,
        "isNativeWindows": is_native_windows_host,
        "hostPlatform": normalized_host_platform,
        "hostKernel": str(host_kernel or "").strip(),
        "runner": normalized_runner,
        "evidenceSource": normalized_source,
    }

    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    original_mode = receipt_path.stat().st_mode & 0o777
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            prefix=f".{receipt_path.name}.",
            suffix=".tmp",
            dir=receipt_path.parent,
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)
            json.dump(payload, temporary, indent=2)
            temporary.write("\n")
            temporary.flush()
            os.fsync(temporary.fileno())
        os.chmod(temporary_path, original_mode)
        os.replace(temporary_path, receipt_path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)

    return payload


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", required=True, type=Path)
    parser.add_argument("--execution-environment", required=True)
    parser.add_argument("--runner", required=True)
    parser.add_argument("--host-platform", required=True)
    parser.add_argument("--host-kernel", default="")
    parser.add_argument("--evidence-source", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    annotate(
        args.receipt,
        execution_environment=args.execution_environment,
        runner=args.runner,
        host_platform=args.host_platform,
        host_kernel=args.host_kernel,
        evidence_source=args.evidence_source,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
