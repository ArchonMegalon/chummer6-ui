#!/usr/bin/env python3
"""Materialize a fail-honest receipt for external desktop deploy readiness."""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


def present(name: str) -> bool:
    return bool(os.environ.get(name, "").strip())


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_OUTPUT = REPO_ROOT / ".tmp" / "deploy-readiness" / "EXTERNAL_DEPLOY_READINESS.generated.json"


def valid_http_url(value: str) -> bool:
    parsed = urlparse(value.strip())
    return parsed.scheme.lower() in {"http", "https"} and bool(parsed.netloc)


def valid_s3_uri(value: str) -> bool:
    trimmed = value.strip()
    return trimmed.startswith("s3://") and len(trimmed) > len("s3://") and not any(
        char.isspace() for char in trimmed
    )


def auth_source_present() -> bool:
    return any(
        present(name)
        for name in (
            "CHUMMER_RELEASE_UPLOAD_TOKEN",
            "CHUMMER_RELEASE_UPLOAD_TOKEN_FILE",
            "CHUMMER_RELEASE_UPLOAD_TOKEN_PATH",
        )
    )


def mode(
    name: str,
    configured_by: str,
    required: list[str],
    *,
    invalid: list[str] | None = None,
) -> dict[str, Any]:
    return {
        "mode": name,
        "configured": present(configured_by),
        "required": required,
        "missing": [item for item in required if not present(item)],
        "invalid": invalid or [],
    }


def build_receipt(require_external_deploy: bool) -> dict[str, Any]:
    portal_required = [
        "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR",
        "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
    ]
    portal_invalid = []
    if present("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL") and not valid_http_url(
        os.environ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"]
    ):
        portal_invalid.append("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL")

    http_required = [
        "CHUMMER_RELEASE_UPLOAD_URL",
        "CHUMMER_RELEASE_UPLOAD_TOKEN or CHUMMER_RELEASE_UPLOAD_TOKEN_FILE/CHUMMER_RELEASE_UPLOAD_TOKEN_PATH",
        "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
    ]
    http_missing = []
    if not present("CHUMMER_RELEASE_UPLOAD_URL"):
        http_missing.append("CHUMMER_RELEASE_UPLOAD_URL")
    if not auth_source_present():
        http_missing.append(
            "CHUMMER_RELEASE_UPLOAD_TOKEN or CHUMMER_RELEASE_UPLOAD_TOKEN_FILE/CHUMMER_RELEASE_UPLOAD_TOKEN_PATH"
        )
    if not present("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"):
        http_missing.append("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL")

    http_invalid = []
    if present("CHUMMER_RELEASE_UPLOAD_URL") and not valid_http_url(
        os.environ["CHUMMER_RELEASE_UPLOAD_URL"]
    ):
        http_invalid.append("CHUMMER_RELEASE_UPLOAD_URL")
    if present("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL") and not valid_http_url(
        os.environ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"]
    ):
        http_invalid.append("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL")

    object_storage_required = [
        "CHUMMER_PORTAL_DOWNLOADS_S3_URI",
        "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
        "CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID",
        "CHUMMER_PORTAL_DOWNLOADS_AWS_SECRET_ACCESS_KEY",
    ]
    object_storage_invalid = []
    if present("CHUMMER_PORTAL_DOWNLOADS_S3_URI") and not valid_s3_uri(
        os.environ["CHUMMER_PORTAL_DOWNLOADS_S3_URI"]
    ):
        object_storage_invalid.append("CHUMMER_PORTAL_DOWNLOADS_S3_URI")
    if present("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL") and not valid_http_url(
        os.environ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"]
    ):
        object_storage_invalid.append("CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL")

    modes = [
        {
            "mode": "portal_directory",
            "configured": present("CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR"),
            "required": portal_required,
            "missing": [item for item in portal_required if not present(item)],
            "invalid": portal_invalid,
        },
        {
            "mode": "http_promote",
            "configured": present("CHUMMER_RELEASE_UPLOAD_URL"),
            "required": http_required,
            "missing": http_missing,
            "invalid": http_invalid,
        },
        mode(
            "object_storage",
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI",
            object_storage_required,
            invalid=object_storage_invalid,
        ),
    ]
    configured_modes = [item["mode"] for item in modes if item["configured"]]
    complete_modes = [
        item["mode"]
        for item in modes
        if item["configured"] and not item["missing"] and not item["invalid"]
    ]
    status = "ready" if complete_modes else "not_configured"
    if configured_modes and not complete_modes:
        status = "configured_incomplete"
    if require_external_deploy and not complete_modes:
        status = "blocked"

    return {
        "schema": "chummer.desktop.external_deploy_readiness.v1",
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": status,
        "requireExternalDeploy": require_external_deploy,
        "configuredModes": configured_modes,
        "completeModes": complete_modes,
        "modes": modes,
        "summary": (
            "At least one external deploy path is fully configured."
            if complete_modes
            else "No complete external deploy path is configured; rolling GitHub Release publication remains the available public bundle path."
        ),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT),
        help="Receipt path to write.",
    )
    parser.add_argument(
        "--require-external-deploy",
        action="store_true",
        help="Fail if no external deploy mode is fully configured.",
    )
    args = parser.parse_args()

    receipt = build_receipt(args.require_external_deploy)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if receipt["status"] == "blocked":
        raise SystemExit(
            "External deploy was required, but no complete deploy target is configured."
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
