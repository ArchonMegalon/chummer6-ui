from __future__ import annotations

import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


SCRIPT_PATH = Path(__file__).with_name("materialize_release_candidate_handoff.py")
SPEC = importlib.util.spec_from_file_location(
    "materialize_release_candidate_handoff",
    SCRIPT_PATH,
)
assert SPEC is not None
assert SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class MaterializeReleaseCandidateHandoffTests(unittest.TestCase):
    def test_windows_exit_gate_is_pinned_to_stage_local_startup_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            stage = root / "candidate"
            startup_smoke = stage / "startup-smoke"
            files = stage / "files"
            startup_smoke.mkdir(parents=True)
            files.mkdir()
            (stage / "RELEASE_CHANNEL.generated.json").write_text(
                '{"version":"candidate"}\n',
                encoding="utf-8",
            )
            receipt = startup_smoke / "startup-smoke-avalonia-win-x64.receipt.json"
            progress_log = startup_smoke / "windows-installer-progress-avalonia-win-x64.log"
            receipt.write_text('{"status":"pass"}\n', encoding="utf-8")
            progress_log.write_text("candidate progress\n", encoding="utf-8")

            fake_gate = root / "capture-gate-environment.sh"
            fake_gate.write_text(
                """#!/usr/bin/env bash
set -euo pipefail
python3 - "$CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH" <<'PY'
import json
import os
import sys
from pathlib import Path

Path(sys.argv[1]).write_text(
    json.dumps(
        {
            "status": "captured",
            "checks": {
                "startup_smoke_receipt_path": os.environ.get(
                    "CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH", ""
                ),
                "startup_smoke_progress_log_path": os.environ.get(
                    "CHUMMER_WINDOWS_STARTUP_SMOKE_PROGRESS_LOG_PATH", ""
                ),
            },
        }
    )
    + "\\n",
    encoding="utf-8",
)
PY
""",
                encoding="utf-8",
            )

            with patch.dict(
                os.environ,
                {"CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH": str(fake_gate)},
                clear=False,
            ):
                result = MODULE.maybe_materialize_windows_exit_gate(stage)

            self.assertEqual("captured", result["status"])
            output = json.loads(
                (stage / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json").read_text(
                    encoding="utf-8"
                )
            )
            checks = output["checks"]
            self.assertEqual(str(receipt), checks["startup_smoke_receipt_path"])
            self.assertEqual(
                str(progress_log),
                checks["startup_smoke_progress_log_path"],
            )


if __name__ == "__main__":
    unittest.main()
