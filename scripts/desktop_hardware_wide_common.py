#!/usr/bin/env python3
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
COMPLETION_ROOT = WORKSPACE_ROOT / "_completion" / "desktop_hardware_wide_flagship"
PASS_STATUSES = {"pass", "passed", "ready"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def ensure_completion_root() -> Path:
    COMPLETION_ROOT.mkdir(parents=True, exist_ok=True)
    return COMPLETION_ROOT


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"Expected JSON object in {path}")
    return payload


def normalize_status(payload: dict[str, Any]) -> str:
    return str(payload.get("status") or payload.get("verdict") or "").strip().lower()


def is_pass_status(payload: dict[str, Any]) -> bool:
    return normalize_status(payload) in PASS_STATUSES


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def evidence_path(*parts: str) -> str:
    return str(Path(*parts))
