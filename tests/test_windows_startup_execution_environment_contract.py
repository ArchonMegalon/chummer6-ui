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
    attach_index = source.rindex("  attach_windows_execution_environment_to_receipt")
    assert source.index("detect_windows_execution_lane\n      run_windows_smoke") < attach_index
    assert source.index("accepting receipt-backed pass") < attach_index
    assert "attach_windows_execution_environment_to_receipt || status=1" in source
    assert source.index('\nif [[ "$status" -ne 0 ]]; then', attach_index) > attach_index
    assert '"$SCRIPT_DIR/annotate-windows-startup-smoke-receipt.py"' in source


def test_smoke_runner_runtime_attaches_truthful_wine_evidence_without_relabeling(
    tmp_path: Path,
) -> None:
    source = SMOKE_RUNNER.read_text(encoding="utf-8")
    harness = tmp_path / "windows-execution-evidence.sh"
    harness.write_text(
        source[: source.index("\nmain() {")]
        + r'''

command() {
  if [[ "${1:-}" == "-v" ]]; then
    case "${2:-}" in
      wine64) return 0 ;;
      *) return 1 ;;
    esac
  fi
  builtin command "$@"
}

uname() {
  [[ "${1:-}" == "-s" ]]
  printf 'Linux\n'
}

SCRIPT_DIR="${7:?script directory is required}"
detect_windows_execution_lane
[[ "$WINDOWS_EXECUTION_ENVIRONMENT" == "wine_compatibility" ]]
[[ "$WINDOWS_EXECUTION_RUNNER" == "wine64" ]]
[[ "$WINDOWS_EXECUTION_HOST_PLATFORM" == "linux" ]]
[[ "$WINDOWS_EXECUTION_HOST_KERNEL" == "Linux" ]]
[[ "$WINDOWS_EXECUTION_EVIDENCE_SOURCE" == "wine_runner_selection" ]]

printf '%s\n' \
  '{"status":"pass","verdict":"PASS","platform":"windows","rid":"win-x64","readyCheckpoint":"pre_ui_event_loop"}' \
  > "$RECEIPT_PATH"
attach_windows_execution_environment_to_receipt
''',
        encoding="utf-8",
    )
    artifact = tmp_path / "fixture.exe"
    artifact.write_bytes(b"fixture")
    output = tmp_path / "output"

    completed = subprocess.run(
        [
            "bash",
            str(harness),
            str(artifact),
            "avalonia",
            "win-x64",
            "fixture.exe",
            str(output),
            "run-fixture",
            str(REPO_ROOT / "scripts"),
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stdout + completed.stderr
    payload = json.loads(
        (output / "startup-smoke-avalonia-win-x64.receipt.json").read_text(
            encoding="utf-8"
        )
    )
    assert payload["status"] == "pass"
    assert payload["verdict"] == "PASS"
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
