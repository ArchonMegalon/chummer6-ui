#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
PROOF = PUBLISHED / "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json"


def main() -> int:
    payload = json.loads(PROOF.read_text(encoding="utf-8"))
    status = str(payload.get("status") or "").strip().lower()
    if status not in {"pass", "passed", "ready"}:
        raise SystemExit("Chummer5A human parity matrix proof is not passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
