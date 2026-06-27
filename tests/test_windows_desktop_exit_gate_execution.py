from __future__ import annotations

import hashlib
import json
import os
import subprocess
import zipfile
from datetime import UTC, datetime
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"


def _now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _write_support_receipt(path: Path, contract_name: str, *, status: str = "passed") -> None:
    _write_json(
        path,
        {
            "contract_name": contract_name,
            "status": status,
        },
    )


def _write_bootstrap_payload(payload_path: Path) -> None:
    payload_path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(payload_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("Samples/Legacy/Soma-Career.chum5", "sample-runner")
        archive.writestr("Chummer.Avalonia.exe", "stub-executable")


def _write_visual_screenshots(root: Path) -> tuple[Path, Path]:
    progress_path = root / "published" / "windows-installer-progress.png"
    completion_path = root / "published" / "windows-installer-completion.png"
    progress_path.parent.mkdir(parents=True, exist_ok=True)
    progress_path.write_bytes(b"progress-image")
    completion_path.write_bytes(b"completion-image")
    return progress_path, completion_path


def _write_release_channel(path: Path, installer_name: str, installer_sha: str, installer_size: int) -> None:
    _write_json(
        path,
        {
            "status": "published",
            "channelId": "preview",
            "version": "run-test-0.0.0.1",
            "artifacts": [
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "head": "avalonia",
                    "platform": "windows",
                    "arch": "x64",
                    "rid": "win-x64",
                    "kind": "installer",
                    "fileName": installer_name,
                    "downloadUrl": f"https://chummer.run/downloads/files/{installer_name}",
                    "sha256": installer_sha,
                    "sizeBytes": installer_size,
                    "channelId": "preview",
                    "version": "run-test-0.0.0.1",
                    "releaseVersion": "run-test-0.0.0.1",
                }
            ],
        },
    )


def _write_startup_smoke_receipt(path: Path, installer_sha: str) -> None:
    _write_json(
        path,
        {
            "status": "pass",
            "readyCheckpoint": "pre_ui_event_loop",
            "artifactDigest": f"sha256:{installer_sha}",
            "headId": "avalonia",
            "platform": "windows",
            "arch": "x64",
            "rid": "win-x64",
            "channelId": "preview",
            "version": "run-test-0.0.0.1",
            "hostClass": "win32-x64",
            "operatingSystem": "Windows 11",
            "completedAtUtc": _now_iso(),
        },
    )


def _write_visual_proof(path: Path, installer_sha: str, progress_path: Path, completion_path: Path) -> None:
    _write_json(
        path,
        {
            "contract_name": "chummer6-ui.windows_installer_visual_proof",
            "contractName": "chummer6-ui.windows_installer_visual_proof",
            "status": "pass",
            "generated_at": _now_iso(),
            "generatedAt": _now_iso(),
            "recordedAtUtc": _now_iso(),
            "channelId": "preview",
            "releaseVersion": "run-test-0.0.0.1",
            "version": "run-test-0.0.0.1",
            "headId": "avalonia",
            "head": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "artifactDigest": f"sha256:{installer_sha}",
            "screenshots": [
                {
                    "role": "progress",
                    "path": str(progress_path),
                    "sha256": _sha256_file(progress_path),
                },
                {
                    "role": "completion",
                    "path": str(completion_path),
                    "sha256": _sha256_file(completion_path),
                },
            ],
            "readabilityReview": {"status": "pass"},
            "contrastReview": {"status": "pass"},
            "clippingReview": {"status": "pass"},
        },
    )


def _build_fixture(root: Path) -> dict[str, Path]:
    files_dir = root / "downloads" / "files"
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    installer_path.parent.mkdir(parents=True, exist_ok=True)
    installer_path.write_bytes(b"windows-installer-stub" * 256)
    _write_bootstrap_payload(payload_path)
    installer_sha = _sha256_file(installer_path)

    release_channel_path = root / "downloads" / "RELEASE_CHANNEL.generated.json"
    startup_smoke_path = root / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
    local_release_path = root / "published" / "UI_LOCAL_RELEASE_PROOF.generated.json"
    self_host_path = root / "published" / "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"
    public_edge_path = root / "published" / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
    browser_lane_path = root / "published" / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
    flagship_gate_path = root / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
    workflow_gate_path = root / "published" / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
    ui_parity_path = root / "published" / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
    sr4_parity_path = root / "published" / "SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
    sr6_parity_path = root / "published" / "SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
    visual_proof_path = root / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    output_path = root / "published" / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    progress_screenshot_path, completion_screenshot_path = _write_visual_screenshots(root)

    _write_release_channel(release_channel_path, installer_path.name, installer_sha, installer_path.stat().st_size)
    _write_startup_smoke_receipt(startup_smoke_path, installer_sha)
    _write_support_receipt(local_release_path, "chummer6-ui.local_release_proof")
    _write_support_receipt(self_host_path, "chummer6-ui.blazor_self_host_workbench_proof")
    _write_support_receipt(public_edge_path, "chummer6-ui.blazor_public_edge_workbench_proof", status="ready")
    _write_support_receipt(browser_lane_path, "chummer6-ui.blazor_browser_lane_proof_set")
    _write_support_receipt(flagship_gate_path, "chummer6-ui.flagship_release_gate")
    _write_support_receipt(workflow_gate_path, "chummer6-ui.desktop_workflow_execution_gate")
    _write_support_receipt(ui_parity_path, "chummer6-ui.chummer5a_desktop_workflow_parity")
    _write_support_receipt(sr4_parity_path, "chummer6-ui.sr4_desktop_workflow_parity")
    _write_support_receipt(sr6_parity_path, "chummer6-ui.sr6_desktop_workflow_parity")

    return {
        "files_dir": files_dir,
        "installer_path": installer_path,
        "payload_path": payload_path,
        "release_channel_path": release_channel_path,
        "startup_smoke_path": startup_smoke_path,
        "local_release_path": local_release_path,
        "self_host_path": self_host_path,
        "public_edge_path": public_edge_path,
        "browser_lane_path": browser_lane_path,
        "flagship_gate_path": flagship_gate_path,
        "workflow_gate_path": workflow_gate_path,
        "ui_parity_path": ui_parity_path,
        "sr4_parity_path": sr4_parity_path,
        "sr6_parity_path": sr6_parity_path,
        "visual_proof_path": visual_proof_path,
        "output_path": output_path,
        "progress_screenshot_path": progress_screenshot_path,
        "completion_screenshot_path": completion_screenshot_path,
    }


def _run_gate(paths: dict[str, Path], *, set_files_root_env: bool = True) -> subprocess.CompletedProcess[str]:
    env = {
        **os.environ,
        "CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH": str(paths["release_channel_path"]),
        "CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH": str(paths["local_release_path"]),
        "CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH": str(paths["self_host_path"]),
        "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH": str(paths["public_edge_path"]),
        "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH": str(paths["browser_lane_path"]),
        "CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH": str(paths["flagship_gate_path"]),
        "CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH": str(paths["workflow_gate_path"]),
        "CHUMMER_UI_WORKFLOW_PARITY_PATH": str(paths["ui_parity_path"]),
        "CHUMMER_SR4_WORKFLOW_PARITY_PATH": str(paths["sr4_parity_path"]),
        "CHUMMER_SR6_WORKFLOW_PARITY_PATH": str(paths["sr6_parity_path"]),
        "CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH": str(paths["visual_proof_path"]),
        "CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH": str(paths["output_path"]),
        "CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH": str(paths["startup_smoke_path"]),
        "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY": "avalonia",
        "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID": "win-x64",
    }
    if set_files_root_env:
        env["CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"] = str(paths["files_dir"])

    return subprocess.run(
        ["bash", str(SCRIPT)],
        text=True,
        capture_output=True,
        check=False,
        env=env,
    )


def test_windows_desktop_exit_gate_fails_cleanly_when_visual_proof_is_missing(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows installer visual proof is missing" in result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["checks"]["windows_installer_visual_proof_found"] is False
    assert "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host." in receipt["reasons"]
    assert receipt["checks"]["bootstrap_payload_exists"] is True
    assert receipt["checks"]["bootstrap_payload_sample_marker_present"] is True


def test_windows_desktop_exit_gate_passes_with_valid_visual_proof_and_bootstrap_payload(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )

    result = _run_gate(paths)

    assert result.returncode == 0, result.stderr
    assert "[windows-exit-gate] PASS" in result.stdout
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "passed"
    assert receipt["summary"] == "Windows desktop exit gate passed."
    assert receipt["checks"]["windows_installer_visual_proof_found"] is True
    assert receipt["checks"]["windows_installer_visual_unique_digest_count"] == 2
    assert receipt["checks"]["windows_installer_visual_actual_unique_digest_count"] == 2
    assert receipt["checks"]["windows_installer_visual_proof_artifact_digest"] == f"sha256:{_sha256_file(paths['installer_path'])}"
    assert receipt["checks"]["windows_installer_visual_screenshot_file_exists"] == {
        "progress": True,
        "completion": True,
    }
    assert receipt["checks"]["startup_smoke_status"] == "pass"
    assert receipt["checks"]["bootstrap_payload_exists"] is True
    assert receipt["checks"]["bootstrap_payload_sample_marker_present"] is True
    assert receipt["checks"]["ui_local_release_status"] == "passed"


def test_windows_desktop_exit_gate_rejects_visual_proof_with_missing_screenshot_file(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )
    paths["completion_screenshot_path"].unlink()

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows installer visual proof screenshot files are missing for: completion." in result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["checks"]["windows_installer_visual_roles_missing_files"] == ["completion"]
    assert receipt["checks"]["windows_installer_visual_screenshot_file_exists"]["completion"] is False


def test_windows_desktop_exit_gate_defaults_to_release_aligned_files_shelf(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )

    result = _run_gate(paths, set_files_root_env=False)

    assert result.returncode == 0, result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "passed"
    assert receipt["checks"]["windows_installer_from_primary_shelf"] is True
    assert Path(receipt["checks"]["windows_installer_primary_shelf_root"]) == paths["files_dir"]
