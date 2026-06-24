#!/usr/bin/env python3
"""Write an explicit receipt for what kind of downloads publication happened."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def build_receipt(
    *,
    deploy_dir: str,
    release_version: str,
    release_channel: str,
    promoted_artifact_count: int,
    deploy_mode: bool,
    live_verify_target: str,
    require_external_publish: bool,
) -> dict[str, Any]:
    live_verify_target = live_verify_target.strip()
    external_verified = deploy_mode and bool(live_verify_target)
    if external_verified:
        scope = "external_downloads_publish_verified"
        status = "passed"
        summary = (
            "Desktop artifacts were published through an external deploy lane and "
            "the configured live downloads endpoint was verified."
        )
    elif require_external_publish:
        scope = "local_downloads_shelf_only"
        status = "blocked"
        summary = (
            "Only a local downloads shelf was updated. External desktop artifact "
            "publication was required but no verified external publish lane ran."
        )
    else:
        scope = "local_downloads_shelf_only"
        status = "passed"
        summary = (
            "A local downloads shelf was updated and verified. This is not an "
            "external desktop artifact upload."
        )

    return {
        "schema": "chummer.downloads.publication_scope.v1",
        "generatedAt": _utc_now(),
        "status": status,
        "scope": scope,
        "releaseVersion": release_version,
        "releaseChannel": release_channel,
        "deployDir": deploy_dir,
        "promotedArtifactCount": promoted_artifact_count,
        "deployMode": deploy_mode,
        "liveVerifyTarget": live_verify_target,
        "externalArtifactPublishVerified": external_verified,
        "requireExternalPublish": require_external_publish,
        "summary": summary,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--deploy-dir", required=True)
    parser.add_argument("--release-version", required=True)
    parser.add_argument("--release-channel", required=True)
    parser.add_argument("--promoted-artifact-count", required=True, type=int)
    parser.add_argument("--deploy-mode", action="store_true")
    parser.add_argument("--live-verify-target", default="")
    parser.add_argument("--require-external-publish", action="store_true")
    args = parser.parse_args()

    receipt = build_receipt(
        deploy_dir=args.deploy_dir,
        release_version=args.release_version,
        release_channel=args.release_channel,
        promoted_artifact_count=args.promoted_artifact_count,
        deploy_mode=args.deploy_mode,
        live_verify_target=args.live_verify_target,
        require_external_publish=args.require_external_publish,
    )

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if receipt["status"] == "blocked":
        raise SystemExit(receipt["summary"])

    print(f"downloads_publication_scope:{receipt['scope']} {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
