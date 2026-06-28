#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).absolute().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
FLAGSHIP_GATE = PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json"


def normalize_status(value: object) -> str:
    return "pass" if str(value or "").strip().lower() in {"pass", "passed", "ready"} else "fail"


def main() -> int:
    payload = json.loads(FLAGSHIP_GATE.read_text(encoding="utf-8"))
    parent = normalize_status(payload.get("status"))
    child = normalize_status((payload.get("uiElementParityAuditProof") or {}).get("status"))
    if parent == "pass" and child != "pass":
        raise SystemExit("UI flagship gate parent is pass while uiElementParityAuditProof is not pass")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
