#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_NO_NOISE_SHELL_GATE.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("no-noise shell gate is not passing")

    if str(payload.get("channelId") or "").strip() != "public_stable":
        raise SystemExit("no-noise shell gate must target public_stable")

    hidden_catalog_controls = payload.get("hiddenCatalogControls")
    if not isinstance(hidden_catalog_controls, list):
        raise SystemExit("hiddenCatalogControls is missing")

    forbidden_controls = payload.get("forbiddenControls")
    if not isinstance(forbidden_controls, list) or "LoadDemoRunnerButton" not in forbidden_controls:
        raise SystemExit("no-noise shell gate must fail closed on LoadDemoRunnerButton")

    test_refs = payload.get("testRefs")
    if not isinstance(test_refs, list) or len(test_refs) < 2:
        raise SystemExit("no-noise shell gate requires executable test refs")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("no-noise shell gate reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
