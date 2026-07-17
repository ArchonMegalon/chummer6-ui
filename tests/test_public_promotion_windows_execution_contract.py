from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "generate-public-promotion-evidence.py"


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _run_generator(
    tmp_path: Path,
    *,
    channel: str,
    execution_environment: str | None,
) -> dict:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    smoke_dir = tmp_path / "startup-smoke"
    output_path = tmp_path / "release-evidence" / "public-promotion.json"
    smoke_dir.mkdir(parents=True)
    _write_json(
        manifest_path,
        {
            "channelId": channel,
            "artifacts": [
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "fileName": "chummer-avalonia-win-x64-installer.exe",
                    "platform": "windows",
                    "head": "avalonia",
                    "rid": "win-x64",
                    "arch": "x64",
                    "sha256": "abc123",
                    "sizeBytes": 1,
                    "kind": "installer",
                }
            ],
        },
    )
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    receipt = {
        "status": "pass",
        "headId": "avalonia",
        "platform": "windows",
        "arch": "x64",
        "rid": "win-x64",
        "readyCheckpoint": "pre_ui_event_loop",
        "hostClass": "windows-x64-host" if execution_environment == "native_windows" else "wine64-linux-x64-container",
        "operatingSystem": "Windows 11",
        "artifactDigest": "sha256:abc123",
        "recordedAtUtc": recorded_at,
    }
    if execution_environment is not None:
        is_native = execution_environment == "native_windows"
        receipt.update(
            {
                "executionEnvironment": execution_environment,
                "verificationScope": (
                    "native_windows_startup" if is_native else "windows_compatibility_startup"
                ),
                "nativeHostEvidence": {
                    "contractName": "chummer6-ui.native_windows_host_evidence",
                    "status": "verified" if is_native else "not_native",
                    "isNativeWindows": is_native,
                    "hostPlatform": "windows" if is_native else "linux",
                    "hostKernel": "Windows_NT" if is_native else "Linux",
                    "runner": "powershell.exe" if is_native else "wine64",
                    "evidenceSource": (
                        "powershell_runtime_os_probe" if is_native else "wine_runner_selection"
                    ),
                },
            }
        )
    _write_json(smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json", receipt)

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest",
            str(manifest_path),
            "--startup-smoke-dir",
            str(smoke_dir),
            "--output",
            str(output_path),
            "--channel",
            channel,
            "--generated-at",
            recorded_at,
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr
    return json.loads(output_path.read_text(encoding="utf-8"))["artifacts"][0]


def test_preview_promotion_records_wine_as_compatibility_smoke(tmp_path: Path) -> None:
    artifact = _run_generator(
        tmp_path,
        channel="preview",
        execution_environment="wine_compatibility",
    )

    assert artifact["startupSmokeStatus"] == "pass"
    assert artifact["startupSmokeExecutionEnvironment"] == "wine_compatibility"
    assert artifact["nativeWindowsStartupProofRequired"] is False


def test_public_stable_promotion_rejects_wine_compatibility(tmp_path: Path) -> None:
    artifact = _run_generator(
        tmp_path,
        channel="public_stable",
        execution_environment="wine_compatibility",
    )

    assert artifact["startupSmokeStatus"] == "fail"
    assert "native Windows startup proof is required" in artifact["startupSmokeReason"]
    assert artifact["nativeWindowsStartupProofRequired"] is True


def test_public_stable_promotion_accepts_native_windows_evidence(tmp_path: Path) -> None:
    artifact = _run_generator(
        tmp_path,
        channel="public_stable",
        execution_environment="native_windows",
    )

    assert artifact["startupSmokeStatus"] == "pass"
    assert artifact["startupSmokeExecutionEnvironment"] == "native_windows"
    assert artifact["nativeWindowsStartupProofRequired"] is True


def test_promotion_fails_closed_when_windows_execution_evidence_is_missing(tmp_path: Path) -> None:
    artifact = _run_generator(
        tmp_path,
        channel="preview",
        execution_environment=None,
    )

    assert artifact["startupSmokeStatus"] == "fail"
    assert "executionEnvironment is missing or unsupported" in artifact["startupSmokeReason"]
