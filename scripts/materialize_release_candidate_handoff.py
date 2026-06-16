#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


def now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def normalize(value: object) -> str:
    return str(value or "").strip()


def collect_receipts(startup_smoke_dir: Path) -> dict[str, dict[str, Any]]:
    receipts: dict[str, dict[str, Any]] = {}
    for path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
        payload = load_json(path)
        tuple_key = f"{normalize(payload.get('headId'))}:{normalize(payload.get('platform'))}:{normalize(payload.get('rid'))}"
        receipts[tuple_key] = {
            "path": str(path),
            "status": normalize(payload.get("status")),
            "skip_reason": normalize(payload.get("skipReason")),
            "artifact_digest": normalize(payload.get("artifactDigest")),
            "ready_checkpoint": normalize(payload.get("readyCheckpoint")),
            "host_class": normalize(payload.get("hostClass")),
        }
    return receipts


def build_payload(stage_dir: Path) -> dict[str, Any]:
    manifest_path = stage_dir / "RELEASE_CHANNEL.generated.json"
    manifest = load_json(manifest_path)
    coverage = manifest.get("desktopTupleCoverage") or {}
    receipts = collect_receipts(stage_dir / "startup-smoke")

    artifacts = []
    for row in manifest.get("artifacts") or []:
        artifacts.append(
            {
                "artifact_id": normalize(row.get("artifactId")),
                "file_name": normalize(row.get("fileName")),
                "platform": normalize(row.get("platform")),
                "rid": normalize(row.get("rid")),
                "version": normalize(row.get("version")),
            }
        )

    blockers: list[str] = []
    next_actions: list[str] = []
    missing_platforms = coverage.get("missingRequiredPlatforms") or []
    if "windows" in missing_platforms:
        blockers.append("Windows installer tuple is not promotable from this host because startup-smoke did not pass.")
        next_actions.append("Run Windows installer startup-smoke on a Windows-capable host for the staged installer bytes.")
    if "macos" in missing_platforms:
        blockers.append("macOS tuple is missing entirely from the candidate bundle.")
        next_actions.append("Build the macOS DMG, capture fresh startup-smoke, and restage the bundle.")
    if "linux" in missing_platforms:
        blockers.append("Linux tuple is missing or not promotable.")
        next_actions.append("Rebuild Linux installer bytes and rerun Linux startup-smoke.")

    next_actions.append("Publish the verified bundle with CHUMMER_RELEASE_UPLOAD_TOKEN once all required platform tuples are promotable.")

    return {
        "contract_name": "chummer.release_build_handoff",
        "generated_at": now_iso(),
        "stage_dir": str(stage_dir),
        "channel": normalize(manifest.get("channelId")),
        "version": normalize(manifest.get("version")),
        "artifact_count": len(artifacts),
        "artifacts": artifacts,
        "promoted_tuples": coverage.get("promotedPlatformHeadRidTuples") or [],
        "missing_required_platforms": missing_platforms,
        "missing_required_heads": coverage.get("missingRequiredHeads") or [],
        "startup_smoke_receipts": receipts,
        "blockers": blockers,
        "next_actions": next_actions,
        "promotion_ready": not missing_platforms,
    }


def render_markdown(payload: dict[str, Any]) -> str:
    blocker_lines = [f"- {item}" for item in payload["blockers"]] if payload["blockers"] else ["- none"]
    lines = [
        "# Release Build Handoff",
        "",
        f"Generated: {payload['generated_at']}",
        "",
        f"- Stage dir: `{payload['stage_dir']}`",
        f"- Channel: `{payload['channel']}`",
        f"- Version: `{payload['version']}`",
        f"- Artifact count: `{payload['artifact_count']}`",
        f"- Promotion ready: `{payload['promotion_ready']}`",
        "",
        "## Artifacts",
        "",
        *[
            f"- `{row['artifact_id']}` -> `{row['file_name']}` ({row['platform']} / {row['rid']})"
            for row in payload["artifacts"]
        ],
        "",
        "## Startup Smoke",
        "",
        *[
            f"- `{tuple_key}`: `{row['status']}`"
            + (f" - {row['skip_reason']}" if row["skip_reason"] else "")
            for tuple_key, row in sorted(payload["startup_smoke_receipts"].items())
        ],
        "",
        "## Remaining Blockers",
        "",
        *blocker_lines,
        "",
        "## Next Actions",
        "",
        *[f"- {item}" for item in payload["next_actions"]],
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("stage_dir", type=Path)
    parser.add_argument("--json-output", type=Path)
    parser.add_argument("--md-output", type=Path)
    args = parser.parse_args()

    payload = build_payload(args.stage_dir)
    json_output = args.json_output or (args.stage_dir / "RELEASE_BUILD_HANDOFF.generated.json")
    md_output = args.md_output or (args.stage_dir / "RELEASE_BUILD_HANDOFF.generated.md")
    json_output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    md_output.write_text(render_markdown(payload), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
