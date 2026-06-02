#!/usr/bin/env python3
"""Materialize a fail-honest receipt for external desktop deploy readiness."""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def present(name: str) -> bool:
    return bool(os.environ.get(name, "").strip())


def mode(name: str, configured_by: str, required: list[str]) -> dict[str, Any]:
    return {
        "mode": name,
        "configured": present(configured_by),
        "required": required,
        "missing": [item for item in required if not present(item)],
    }


def build_receipt(require_external_deploy: bool) -> dict[str, Any]:
    modes = [
        mode(
            "portal_directory",
            "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR",
            [
                "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR",
                "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
            ],
        ),
        mode(
            "http_promote",
            "CHUMMER_RELEASE_UPLOAD_URL",
            [
                "CHUMMER_RELEASE_UPLOAD_URL",
                "CHUMMER_RELEASE_UPLOAD_TOKEN",
                "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
            ],
        ),
        mode(
            "object_storage",
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI",
            [
                "CHUMMER_PORTAL_DOWNLOADS_S3_URI",
                "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
                "CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID",
                "CHUMMER_PORTAL_DOWNLOADS_AWS_SECRET_ACCESS_KEY",
            ],
        ),
    ]
    configured_modes = [item["mode"] for item in modes if item["configured"]]
    complete_modes = [
        item["mode"] for item in modes if item["configured"] and not item["missing"]
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
        default="deploy-readiness/EXTERNAL_DEPLOY_READINESS.generated.json",
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
