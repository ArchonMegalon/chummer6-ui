#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED_ROOT = REPO_ROOT / ".codex-studio" / "published"
VERDICT_PATH = PUBLISHED_ROOT / "FULL_CHUMMER5A_UI_PARITY_VERDICT.md"
REQUIRED_ARTIFACTS = {
    "CHUMMER5A_HUMAN_PARITY_ACCEPTANCE_MATRIX.generated.json",
    "CHUMMER5A_NO_NOISE_SHELL_GATE.generated.json",
    "CHUMMER5A_CHUMMER6_ONLY_CONTROL_JUSTIFICATION.generated.json",
    "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json",
    "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json",
    "CHUMMER5A_VETERAN_TASK_TIME_BUDGETS.generated.json",
    "CHUMMER5A_LEGACY_UI_ELEMENT_MAPPING_APPENDIX.generated.json",
}
READY = "FULL_CHUMMER5A_UI_PARITY_READY"
NOT_READY = "NOT_READY"


def load_json(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"{path.name} must contain a JSON object")
    return payload


def main() -> int:
    if not VERDICT_PATH.exists():
        raise SystemExit("full parity verdict file is missing")

    verdict = VERDICT_PATH.read_text(encoding="utf-8").strip()
    if verdict not in {READY, NOT_READY}:
        raise SystemExit(f"unexpected parity verdict value: {verdict!r}")

    missing_artifacts = sorted(name for name in REQUIRED_ARTIFACTS if not (PUBLISHED_ROOT / name).exists())
    if missing_artifacts:
        raise SystemExit(f"required parity artifacts are missing: {missing_artifacts}")

    statuses: dict[str, str] = {}
    for name in sorted(REQUIRED_ARTIFACTS):
        payload = load_json(PUBLISHED_ROOT / name)
        status = str(payload.get("status") or "").strip().lower()
        if status not in {"pass", "fail"}:
            raise SystemExit(f"{name} has invalid status: {status!r}")
        statuses[name] = status

    all_pass = all(status == "pass" for status in statuses.values())
    expected_verdict = READY if all_pass else NOT_READY
    if verdict != expected_verdict:
        raise SystemExit(
            "parity verdict does not match artifact statuses: "
            f"expected {expected_verdict}, found {verdict}"
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
