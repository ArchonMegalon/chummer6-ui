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
    progress_path = root / "published" / "windows-installer-visual-proof" / "windows-installer-progress.png"
    completion_path = root / "published" / "windows-installer-visual-proof" / "windows-installer-completion.png"
    progress_path.parent.mkdir(parents=True, exist_ok=True)
    progress_path.write_bytes(b"progress-image")
    completion_path.write_bytes(b"completion-image")
    return progress_path, completion_path


def _write_release_channel(
    path: Path,
    installer_name: str,
    installer_sha: str,
    installer_size: int,
    payload_name: str,
    payload_sha: str,
    payload_size: int,
) -> None:
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
                    "installerMode": "bootstrap",
                    "payloadFileName": payload_name,
                    "payloadSha256": payload_sha,
                    "payloadSizeBytes": payload_size,
                    "channelId": "preview",
                    "version": "run-test-0.0.0.1",
                    "releaseVersion": "run-test-0.0.0.1",
                }
            ],
        },
    )


def _write_linux_only_release_channel(path: Path) -> None:
    _write_json(
        path,
        {
            "status": "published",
            "channelId": "preview",
            "version": "run-test-0.0.0.1",
            "desktopTupleCoverage": {
                "requiredDesktopPlatforms": ["linux"],
                "requiredDesktopHeads": ["avalonia"],
                "requiredDesktopPlatformHeadRidTuples": ["avalonia:linux-x64:linux"],
                "externalProofRequests": [],
            },
            "artifacts": [],
        },
    )


def _write_startup_smoke_receipt(
    path: Path,
    installer_sha: str,
    payload_name: str,
    payload_sha: str,
    payload_size: int,
) -> None:
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
            "executionEnvironment": "native_windows",
            "verificationScope": "native_windows_startup",
            "nativeHostEvidence": {
                "contractName": "chummer6-ui.native_windows_host_evidence",
                "status": "verified",
                "isNativeWindows": True,
                "hostPlatform": "windows",
                "hostKernel": "MINGW64_NT-10.0",
                "runner": "powershell.exe",
                "evidenceSource": "powershell_runtime_os_probe",
            },
            "bootstrapPayloadAcquisitionMode": "download",
            "bootstrapPayloadFileName": payload_name,
            "bootstrapPayloadSha256": payload_sha,
            "bootstrapPayloadSizeBytes": payload_size,
            "completedAtUtc": _now_iso(),
        },
    )


def _write_startup_smoke_progress_log(path: Path, payload_file_name: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\fixture-user\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                f"Payload download target: C:\\Users\\fixture-user\\AppData\\Local\\Temp\\Chummer6\\installer-temp\\{payload_file_name}",
                "Downloading application files",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
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


def _write_visual_proof_handoff(
    path: Path,
    installer_sha: str,
    *,
    release_channel_path: Path,
    visual_proof_path: Path,
    current_visual_proof_stale: bool = True,
    current_visual_proof_exists: bool = True,
) -> None:
    _write_json(
        path,
        {
            "contract_name": "chummer6-ui.windows_installer_visual_proof_handoff",
            "generated_at": _now_iso(),
            "handoff_only": True,
            "only_blocker_is_visual_proof": True,
            "status": "ready_for_windows_host",
            "current_visual_proof_exists": current_visual_proof_exists,
            "current_visual_proof": {
                "artifact_digest": "sha256:stale-proof-digest",
                "matches_installer_digest": False,
                "matches_release_version": False,
                "stale": current_visual_proof_stale,
                "status": "pass",
                "version": "run-test-stale",
            },
            "operator_artifact_intake": {
                "external_artifact_required": True,
            },
            "release": {
                "channel_id": "preview",
                "release_version": "run-test-0.0.0.1",
                "version": "run-test-0.0.0.1",
            },
            "release_channel_manifest_path": str(release_channel_path),
            "startup_smoke": {
                "artifact_digest": f"sha256:{installer_sha}",
                "status": "pass",
                "release_version": "run-test-0.0.0.1",
                "version": "run-test-0.0.0.1",
            },
            "visual_proof_receipt_path": str(visual_proof_path),
        },
    )


def _write_visual_audit_source(path: Path, installer_sha: str) -> tuple[Path, Path]:
    progress_path = path.parent / "windows-installer-install-progress.png"
    completion_path = path.parent / "windows-installer-completion.png"
    progress_path.parent.mkdir(parents=True, exist_ok=True)
    progress_path.write_bytes(b"native-progress-image")
    completion_path.write_bytes(b"native-completion-image")
    _write_json(
        path,
        {
            "contract_name": "chummer.windows_installer_visual_audit.source",
            "status": "pass",
            "artifactSha256": installer_sha,
            "hostClass": "native-windows",
            "platform": "windows",
            "requiredSurfaces": ["install-progress", "completion"],
            "screenshots": [
                {
                    "path": progress_path.name,
                    "surface": "install-progress",
                    "readabilityStatus": "pass",
                    "clippingStatus": "pass",
                    "captureMode": "window-bounds",
                    "capturedAtUtc": _now_iso(),
                },
                {
                    "path": completion_path.name,
                    "surface": "completion",
                    "readabilityStatus": "pass",
                    "clippingStatus": "pass",
                    "captureMode": "window-bounds",
                    "capturedAtUtc": _now_iso(),
                },
            ],
            "surfaceCoverage": {
                "install-progress": "pass",
                "completion": "pass",
            },
            "sourceUpdatedAtUtc": _now_iso(),
        },
    )
    return progress_path, completion_path


def _write_visual_audit_receipt(path: Path, installer_sha: str) -> None:
    _write_json(
        path,
        {
            "contract_name": "chummer.windows_installer_visual_audit",
            "status": "pass",
            "required_promoted_digest": installer_sha,
            "actual_artifact_sha256": installer_sha,
            "manifest_promoted_digest": installer_sha,
            "source_digest": installer_sha,
            "summary": "Native Windows visual audit matches the promoted installer.",
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
    payload_sha = _sha256_file(payload_path)
    payload_size = payload_path.stat().st_size

    release_channel_path = root / "downloads" / "RELEASE_CHANNEL.generated.json"
    startup_smoke_path = root / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
    startup_smoke_progress_log_path = root / "startup-smoke" / "windows-installer-progress-avalonia-win-x64.log"
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
    visual_audit_source_path = root / "downloads" / "visual-audit" / "windows-installer" / "WINDOWS_INSTALLER_VISUAL_AUDIT.source.json"
    visual_audit_receipt_path = root / "published" / "WINDOWS_INSTALLER_VISUAL_AUDIT.generated.json"
    output_path = root / "published" / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    progress_screenshot_path, completion_screenshot_path = _write_visual_screenshots(root)

    _write_release_channel(
        release_channel_path,
        installer_path.name,
        installer_sha,
        installer_path.stat().st_size,
        payload_path.name,
        payload_sha,
        payload_size,
    )
    _write_startup_smoke_receipt(
        startup_smoke_path,
        installer_sha,
        payload_path.name,
        payload_sha,
        payload_size,
    )
    _write_startup_smoke_progress_log(startup_smoke_progress_log_path, payload_path.name)
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
        "startup_smoke_progress_log_path": startup_smoke_progress_log_path,
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
        "visual_audit_source_path": visual_audit_source_path,
        "visual_audit_receipt_path": visual_audit_receipt_path,
        "output_path": output_path,
        "progress_screenshot_path": progress_screenshot_path,
        "completion_screenshot_path": completion_screenshot_path,
    }


def _run_gate(
    paths: dict[str, Path],
    *,
    set_files_root_env: bool = True,
    extra_env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    env = {
        **os.environ,
        "CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH": str(paths["release_channel_path"]),
        "CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH": str(paths["release_channel_path"]),
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
    if extra_env:
        env.update(extra_env)

    return subprocess.run(
        ["bash", str(SCRIPT)],
        text=True,
        capture_output=True,
        check=False,
        env=env,
    )


def _set_wine_compatibility_evidence(receipt_path: Path) -> None:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt.update(
        {
            "hostClass": "wine64-linux-x64-container",
            "operatingSystem": "Microsoft Windows 10.0.19043",
            "executionEnvironment": "wine_compatibility",
            "verificationScope": "windows_compatibility_startup",
            "nativeHostEvidence": {
                "contractName": "chummer6-ui.native_windows_host_evidence",
                "status": "not_native",
                "isNativeWindows": False,
                "hostPlatform": "linux",
                "hostKernel": "Linux",
                "runner": "wine64",
                "evidenceSource": "wine_runner_selection",
            },
        }
    )
    _write_json(receipt_path, receipt)


def test_windows_desktop_exit_gate_fails_cleanly_when_visual_proof_is_missing(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows installer visual proof is missing" in result.stderr
    assert "Windows desktop exit gate failed: Windows desktop exit gate failed:" not in result.stderr
    assert result.stderr.count("Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.") == 1
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["blockingMode"] == "external_only"
    assert receipt["blocking_mode"] == "external_only"
    assert receipt["checks"]["windows_installer_visual_proof_found"] is False
    assert receipt["checks"]["windows_visual_proof_external_blocker"] == "missing_windows_visual_proof_capture"
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
    assert receipt["checks"]["startup_smoke_progress_log_found"] is True
    assert receipt["checks"]["startup_smoke_bootstrap_temp_root"] == r"Chummer6\installer-temp"
    assert receipt["checks"]["startup_smoke_bootstrap_temp_root_disclosure"] == "contract_suffix_only"
    assert receipt["checks"]["startup_smoke_bootstrap_temp_root_present"] is True
    assert receipt["checks"]["startup_smoke_bootstrap_temp_root_contract_ok"] is True
    assert receipt["checks"]["startup_smoke_payload_download_target"] == paths["payload_path"].name
    assert receipt["checks"]["startup_smoke_payload_download_target_disclosure"] == "file_name_only"
    assert receipt["checks"]["startup_smoke_payload_download_target_present"] is True
    assert receipt["checks"]["startup_smoke_payload_target_root_level"] is False
    assert "fixture-user" not in json.dumps(receipt).casefold()
    assert receipt["checks"]["bootstrap_payload_exists"] is True
    assert receipt["checks"]["bootstrap_payload_sample_marker_present"] is True
    assert receipt["checks"]["ui_local_release_status"] == "passed"


def test_windows_desktop_exit_gate_allows_wine_only_as_compatibility_proof(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )
    _set_wine_compatibility_evidence(paths["startup_smoke_path"])

    compatibility_result = _run_gate(paths)

    assert compatibility_result.returncode == 0, compatibility_result.stderr
    compatibility_receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert compatibility_receipt["checks"]["startup_smoke_execution_environment"] == "wine_compatibility"
    assert compatibility_receipt["checks"]["startup_smoke_native_windows_required"] is False

    native_result = _run_gate(
        paths,
        extra_env={"CHUMMER_WINDOWS_STARTUP_SMOKE_REQUIRE_NATIVE": "1"},
    )

    assert native_result.returncode != 0
    assert (
        "Native Windows startup proof is required; compatibility execution cannot satisfy the Windows desktop exit gate."
        in native_result.stderr
    )
    native_receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert native_receipt["checks"]["startup_smoke_execution_environment"] == "wine_compatibility"
    assert native_receipt["checks"]["startup_smoke_native_windows_required"] is True


def test_windows_desktop_exit_gate_fails_when_nested_workflow_parity_receipts_fail(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )
    _write_support_receipt(
        paths["ui_parity_path"],
        "chummer6-ui.chummer5a_desktop_workflow_parity",
        status="fail",
    )
    _write_support_receipt(
        paths["sr4_parity_path"],
        "chummer6-ui.sr4_desktop_workflow_parity",
        status="fail",
    )

    result = _run_gate(paths)

    assert result.returncode != 0
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["checks"]["desktop_workflow_execution_gate_status"] == "passed"
    assert receipt["checks"]["ui_workflow_parity_status"] == "fail"
    assert receipt["checks"]["sr4_workflow_parity_status"] == "fail"
    assert "Chummer5a desktop workflow parity proof is missing or not passed." in receipt["reasons"]
    assert "SR4 desktop workflow parity proof is missing or not passed." in receipt["reasons"]


def test_windows_desktop_exit_gate_prefers_release_aligned_visual_proof_over_stale_hint(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    release_aligned_visual_proof_path = paths["release_channel_path"].parent / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    _write_visual_proof(
        release_aligned_visual_proof_path,
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )

    stale_installer_path = tmp_path / "stale" / "files" / paths["installer_path"].name
    stale_installer_path.parent.mkdir(parents=True, exist_ok=True)
    stale_installer_path.write_bytes(b"stale-windows-installer-stub" * 256)
    stale_progress_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-progress.png"
    stale_completion_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-completion.png"
    stale_progress_path.parent.mkdir(parents=True, exist_ok=True)
    stale_progress_path.write_bytes(b"stale-progress-image")
    stale_completion_path.write_bytes(b"stale-completion-image")
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(stale_installer_path),
        stale_progress_path,
        stale_completion_path,
    )

    result = _run_gate(paths)

    assert result.returncode == 0, result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "passed"
    assert receipt["checks"]["windows_installer_visual_proof_path"] == str(release_aligned_visual_proof_path)


def test_windows_desktop_exit_gate_treats_stale_visual_proof_as_external_when_current_handoff_requires_recapture(
    tmp_path: Path,
) -> None:
    paths = _build_fixture(tmp_path)

    stale_installer_path = tmp_path / "stale" / "files" / paths["installer_path"].name
    stale_installer_path.parent.mkdir(parents=True, exist_ok=True)
    stale_installer_path.write_bytes(b"stale-windows-installer-stub" * 256)
    stale_progress_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-progress.png"
    stale_completion_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-completion.png"
    stale_progress_path.parent.mkdir(parents=True, exist_ok=True)
    stale_progress_path.write_bytes(b"stale-progress-image")
    stale_completion_path.write_bytes(b"stale-completion-image")
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(stale_installer_path),
        stale_progress_path,
        stale_completion_path,
    )
    handoff_path = paths["release_channel_path"].parent / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    _write_visual_proof_handoff(
        handoff_path,
        _sha256_file(paths["installer_path"]),
        release_channel_path=paths["release_channel_path"],
        visual_proof_path=paths["visual_proof_path"],
    )

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows installer visual proof is missing" in result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["blockingMode"] == "external_only"
    assert receipt["blocking_mode"] == "external_only"
    assert receipt["checks"]["windows_visual_proof_external_blocker"] == "missing_windows_visual_proof_capture"
    assert receipt["checks"]["windows_installer_visual_proof_current_capture_pending"] is True
    assert receipt["checks"]["windows_installer_visual_proof_handoff_path"] == str(handoff_path)
    assert receipt["checks"]["windows_installer_visual_proof_handoff_artifact_digest"] == (
        f"sha256:{_sha256_file(paths['installer_path'])}"
    )
    assert receipt["reasons"] == [
        "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."
    ]
    assert receipt["checks"]["windows_installer_visual_proof_artifact_digest"] != (
        f"sha256:{_sha256_file(paths['installer_path'])}"
    )


def test_windows_desktop_exit_gate_accepts_native_visual_audit_when_legacy_visual_proof_is_stale(
    tmp_path: Path,
) -> None:
    paths = _build_fixture(tmp_path)

    stale_installer_path = tmp_path / "stale" / "files" / paths["installer_path"].name
    stale_installer_path.parent.mkdir(parents=True, exist_ok=True)
    stale_installer_path.write_bytes(b"stale-windows-installer-stub" * 256)
    stale_progress_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-progress.png"
    stale_completion_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-completion.png"
    stale_progress_path.parent.mkdir(parents=True, exist_ok=True)
    stale_progress_path.write_bytes(b"stale-progress-image")
    stale_completion_path.write_bytes(b"stale-completion-image")
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(stale_installer_path),
        stale_progress_path,
        stale_completion_path,
    )
    handoff_path = paths["release_channel_path"].parent / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    _write_visual_proof_handoff(
        handoff_path,
        _sha256_file(paths["installer_path"]),
        release_channel_path=paths["release_channel_path"],
        visual_proof_path=paths["visual_proof_path"],
    )
    _write_visual_audit_source(paths["visual_audit_source_path"], _sha256_file(paths["installer_path"]))
    _write_visual_audit_receipt(paths["visual_audit_receipt_path"], _sha256_file(paths["installer_path"]))

    result = _run_gate(paths)

    assert result.returncode == 0, result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "passed"
    assert receipt["checks"]["windows_visual_proof_external_blocker"] == ""
    assert receipt["checks"]["windows_installer_visual_audit_current_release_ready"] is True
    assert receipt["checks"]["windows_installer_visual_proof_recovered_from_native_audit"] is True
    assert receipt["checks"]["windows_installer_visual_effective_source"] == "native_visual_audit"
    assert receipt["checks"]["windows_installer_visual_effective_path"] == str(paths["visual_audit_source_path"])
    assert receipt["checks"]["windows_installer_visual_effective_artifact_digest"] == (
        f"sha256:{_sha256_file(paths['installer_path'])}"
    )
    assert receipt["checks"]["windows_installer_visual_proof_handoff_current_visual_proof_stale"] is True


def test_windows_desktop_exit_gate_ignores_stale_hint_when_current_release_proof_is_missing(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)

    stale_installer_path = tmp_path / "stale" / "files" / paths["installer_path"].name
    stale_installer_path.parent.mkdir(parents=True, exist_ok=True)
    stale_installer_path.write_bytes(b"stale-windows-installer-stub" * 256)
    stale_progress_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-progress.png"
    stale_completion_path = tmp_path / "stale" / "windows-installer-visual-proof" / "windows-installer-completion.png"
    stale_progress_path.parent.mkdir(parents=True, exist_ok=True)
    stale_progress_path.write_bytes(b"stale-progress-image")
    stale_completion_path.write_bytes(b"stale-completion-image")
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(stale_installer_path),
        stale_progress_path,
        stale_completion_path,
    )

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows installer visual proof is missing" in result.stderr
    assert "version does not match release channel" not in result.stderr
    assert "artifactDigest does not match promoted installer bytes" not in result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["checks"]["windows_visual_proof_external_blocker"] == "missing_windows_visual_proof_capture"
    assert receipt["checks"]["windows_installer_visual_proof_path"] == str(
        paths["release_channel_path"].parent / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    )
    assert receipt["reasons"] == [
        "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."
    ]


def test_windows_desktop_exit_gate_rejects_bootstrap_progress_log_with_root_level_payload_target(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_visual_proof(
        paths["visual_proof_path"],
        _sha256_file(paths["installer_path"]),
        paths["progress_screenshot_path"],
        paths["completion_screenshot_path"],
    )
    paths["startup_smoke_progress_log_path"].write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                "Bootstrap temp root: C:\\Users\\tibor\\AppData\\Local\\Temp\\Chummer6\\installer-temp",
                "Payload download target: \\chummer-avalonia-win-x64-payload.zip",
                "Downloading application files",
                "Payload download completed with bundled curl",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    result = _run_gate(paths)

    assert result.returncode != 0
    assert "Windows bootstrap startup smoke progress log captured a root-level payload target." in result.stderr
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["checks"]["startup_smoke_payload_target_root_level"] is True


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
    assert receipt["blockingMode"] == "none"
    assert receipt["blocking_mode"] == "none"
    assert receipt["checks"]["windows_installer_from_primary_shelf"] is True
    assert Path(receipt["checks"]["windows_installer_primary_shelf_root"]) == paths["files_dir"]


def test_windows_desktop_exit_gate_passes_as_not_required_when_release_channel_is_linux_only(tmp_path: Path) -> None:
    paths = _build_fixture(tmp_path)
    _write_linux_only_release_channel(paths["release_channel_path"])
    if paths["visual_proof_path"].exists():
        paths["visual_proof_path"].unlink()
    if paths["startup_smoke_path"].exists():
        paths["startup_smoke_path"].unlink()

    result = _run_gate(paths)

    assert result.returncode == 0, result.stderr
    assert "not required for current release channel" in result.stdout
    receipt = json.loads(paths["output_path"].read_text(encoding="utf-8"))
    assert receipt["status"] == "passed"
    assert receipt["summary"] == "Windows desktop exit gate is not required for this release channel."
    assert receipt["blockingMode"] == "none"
    assert receipt["blocking_mode"] == "none"
    assert receipt["checks"]["windows_platform_required_for_release_channel"] is False
    assert receipt["checks"]["windows_installer_not_required_for_release_channel"] is True
    assert receipt["checks"]["windows_installer_visual_proof_found"] is False
