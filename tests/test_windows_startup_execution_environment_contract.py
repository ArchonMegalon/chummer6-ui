from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ANNOTATOR = REPO_ROOT / "scripts" / "annotate-windows-startup-smoke-receipt.py"
SMOKE_RUNNER = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"


def _receipt(path: Path) -> None:
    path.write_text(
        json.dumps(
            {
                "status": "pass",
                "platform": "windows",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
            }
        )
        + "\n",
        encoding="utf-8",
    )


def test_annotator_marks_wine_as_compatibility_not_native(tmp_path: Path) -> None:
    receipt_path = tmp_path / "startup-smoke.receipt.json"
    _receipt(receipt_path)

    result = subprocess.run(
        [
            "python3",
            str(ANNOTATOR),
            "--receipt",
            str(receipt_path),
            "--execution-environment",
            "wine_compatibility",
            "--runner",
            "wine64",
            "--host-platform",
            "linux",
            "--host-kernel",
            "Linux",
            "--evidence-source",
            "wine_runner_selection",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert payload["executionEnvironment"] == "wine_compatibility"
    assert payload["verificationScope"] == "windows_compatibility_startup"
    assert payload["nativeHostEvidence"] == {
        "contractName": "chummer6-ui.native_windows_host_evidence",
        "status": "not_native",
        "isNativeWindows": False,
        "hostPlatform": "linux",
        "hostKernel": "Linux",
        "runner": "wine64",
        "evidenceSource": "wine_runner_selection",
    }


def test_annotator_rejects_native_claim_from_non_windows_host(tmp_path: Path) -> None:
    receipt_path = tmp_path / "startup-smoke.receipt.json"
    _receipt(receipt_path)

    result = subprocess.run(
        [
            "python3",
            str(ANNOTATOR),
            "--receipt",
            str(receipt_path),
            "--execution-environment",
            "native_windows",
            "--runner",
            "pwsh",
            "--host-platform",
            "linux",
            "--host-kernel",
            "Linux",
            "--evidence-source",
            "powershell_runtime_os_probe",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode != 0
    assert "native_windows execution requires hostPlatform=windows" in result.stderr


def test_smoke_runner_detects_execution_lane_before_windows_smoke_and_annotates_receipt() -> None:
    source = SMOKE_RUNNER.read_text(encoding="utf-8")

    assert 'WINDOWS_EXECUTION_ENVIRONMENT="wine_compatibility"' in source
    assert 'WINDOWS_EXECUTION_ENVIRONMENT="native_windows"' in source
    assert source.index("detect_windows_execution_lane\n      run_windows_smoke") < source.rindex(
        "  attach_windows_execution_environment_to_receipt"
    )
    assert '"$SCRIPT_DIR/annotate-windows-startup-smoke-receipt.py"' in source
