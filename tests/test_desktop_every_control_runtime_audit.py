from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "desktop_every_control_runtime_audit.py"


def load_module():
    sys.path.insert(0, str(SCRIPT.parent))
    spec = importlib.util.spec_from_file_location(
        "desktop_every_control_runtime_audit", SCRIPT
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload), encoding="utf-8")


def test_every_control_audit_fails_closed_when_an_upstream_row_fails(
    tmp_path: Path,
) -> None:
    module = load_module()
    published = tmp_path / "published"
    completion = tmp_path / "completion"
    write_json(
        published / "INTERACTIVE_CONTROL_INVENTORY.generated.json",
        {
            "status": "pass",
            "standaloneControlReview": {"summary": "standalone"},
            "mainWindowInteractionReview": {"summary": "routes"},
            "keyboardAndTooltipReview": {"summary": "keyboard"},
            "evidence": {"failureCount": 0, "reasonCount": 0},
        },
    )
    write_json(
        published / "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json",
        {"status": "pass", "summary": "recursive"},
    )
    write_json(
        published / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
        {"status": "fail", "summary": "workflow stale"},
    )
    write_json(
        completion / "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json",
        {"status": "pass", "summary": "rows", "rowCount": 28},
    )
    module.PUBLISHED = published
    module.ensure_completion_root = lambda: completion

    assert module.main() == 0
    result = json.loads(
        (completion / module.OUTPUT).read_text(encoding="utf-8")
    )
    assert result["status"] == "fail"
    assert result["blockingFindings"] == [
        "workflow_click_through_families is not passing"
    ]
    assert "blocked" in result["summary"].lower()
