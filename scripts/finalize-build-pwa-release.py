#!/usr/bin/env python3
"""Materialize or verify the exact published Build PWA byte contract.

Run only after the final publish output has settled:

  python3 scripts/finalize-build-pwa-release.py \
    --web-root Chummer.Blazor/bin/Release/net10.0/publish/wwwroot \
    --output Chummer.Blazor/bin/Release/net10.0/publish/build-pwa-release.generated.json
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ASSET_PATHS = (
    "service-worker.js",
    "offline.html",
    "app.css",
    "build-pwa-install.css",
    "Chummer.Blazor.styles.css",
    "manifest.webmanifest",
    "js/build-pwa-recovery.js",
    "js/build-pwa-integrity.js",
    "js/build-pwa-install.js",
    "js/build-pwa-layout.js",
    "js/privacy-boundaries.js",
    "_framework/blazor.web.js",
    "icons/chummer-build-180.png",
    "icons/chummer-build-192.png",
    "icons/chummer-build-512.png",
    "icons/chummer-build-maskable-512.png",
    "icons/chummer-pwa.svg",
    "icons/chummer-pwa-maskable.svg",
)


def build_receipt(web_root: Path) -> dict[str, object]:
    aggregate = hashlib.sha256()
    assets: list[dict[str, object]] = []
    for public_path in ASSET_PATHS:
        asset_path = web_root / public_path
        if not asset_path.is_file():
            raise FileNotFoundError(f"Build PWA release asset is missing: {asset_path}")
        content = asset_path.read_bytes()
        encoded_path = public_path.encode("utf-8")
        aggregate.update(len(encoded_path).to_bytes(4, "big"))
        aggregate.update(encoded_path)
        aggregate.update(len(content).to_bytes(8, "big"))
        aggregate.update(content)
        assets.append(
            {
                "path": public_path,
                "bytes": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        )
    return {
        "schemaVersion": 1,
        "contentRevision": aggregate.hexdigest(),
        "assets": assets,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--web-root", type=Path, required=True)
    destination = parser.add_mutually_exclusive_group(required=True)
    destination.add_argument("--output", type=Path)
    destination.add_argument("--check", type=Path)
    args = parser.parse_args()

    receipt = build_receipt(args.web_root.resolve())
    serialized = json.dumps(receipt, indent=2, ensure_ascii=True) + "\n"
    if args.check is not None:
        expected = args.check.read_text(encoding="utf-8")
        if expected != serialized:
            raise SystemExit("Build PWA published byte receipt is stale or mismatched.")
        print(receipt["contentRevision"])
        return 0

    assert args.output is not None
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8")
    print(receipt["contentRevision"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
