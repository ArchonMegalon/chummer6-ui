from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "desktop_every_control_row_level_certification.py"


def load_module():
    sys.path.insert(0, str(SCRIPT.parent))
    spec = importlib.util.spec_from_file_location(
        "desktop_every_control_row_level_certification", SCRIPT
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_row_level_certification_requires_passing_source_inventory(
    tmp_path: Path,
) -> None:
    module = load_module()
    published = tmp_path / "published"
    completion = tmp_path / "completion"
    published.mkdir()
    (published / "INTERACTIVE_CONTROL_INVENTORY.generated.json").write_text(
        json.dumps(
            {
                "status": "fail",
                "evidence": {
                    "standaloneControlTests": {"button_a": True},
                    "mainWindowTests": {"route_a": True},
                    "blazorKeyboardTests": {},
                    "shortcutCatalogTests": {},
                },
            }
        ),
        encoding="utf-8",
    )
    module.PUBLISHED = published
    module.ensure_completion_root = lambda: completion
    completion.mkdir()

    assert module.main() == 0
    result = json.loads(
        (completion / module.OUTPUT).read_text(encoding="utf-8")
    )
    assert result["rowCount"] == 2
    assert result["failedCount"] == 0
    assert result["status"] == "fail"
    assert result["blockingFindings"] == [
        "interactive control inventory is not passing"
    ]
