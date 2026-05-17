#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def main() -> int:
    repo_root = Path(__file__).resolve().parents[1]
    registry_root = (repo_root / "scripts" / "resolve-hub-registry-root.sh").resolve()
    resolved_registry_root = subprocess.run(
        ["bash", str(registry_root)],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if not resolved_registry_root:
        raise SystemExit("Could not resolve hub-registry root.")

    canonical_manifest = Path(resolved_registry_root) / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    if not canonical_manifest.is_file():
        raise SystemExit(f"Canonical release channel is missing: {canonical_manifest}")

    subprocess.run(
        ["bash", str(repo_root / "scripts" / "verify-releases-manifest.sh"), str(canonical_manifest)],
        check=True,
    )

    payload = json.loads(canonical_manifest.read_text(encoding="utf-8-sig"))
    source_generated_at = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
    now = utc_now()
    payload["generated_at"] = now
    payload["generatedAt"] = now
    payload["verifiedAt"] = now
    payload["verifiedFromPath"] = str(canonical_manifest)
    payload["verifiedFromGeneratedAt"] = source_generated_at

    output_path = repo_root / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
