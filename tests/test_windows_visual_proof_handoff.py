from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize_windows_visual_proof_handoff.py"


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _base_manifest() -> dict:
    return {
        "channelId": "public_stable",
        "version": "run-20260627-005402",
        "artifacts": [
            {
                "artifactId": "avalonia-win-x64-installer",
                "head": "avalonia",
                "platform": "windows",
                "rid": "win-x64",
                "kind": "installer",
                "fileName": "chummer-avalonia-win-x64-installer.exe",
                "downloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
                "sha256": "04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4",
                "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
                "payloadDownloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip",
                "releaseVersion": "run-20260627-005402",
            }
        ],
    }


def _base_windows_gate() -> dict:
    return {
        "status": "failed",
        "summary": "Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",
        "reasons": [
            "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."
        ],
    }


def test_materialize_windows_visual_proof_handoff_blocks_stale_startup_smoke(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = tmp_path / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = tmp_path / "startup-smoke-avalonia-win-x64.receipt.json"
    capture_script_path = tmp_path / "capture-windows-installer-visual-proof.ps1"
    visual_proof_path = tmp_path / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    json_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    md_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"

    capture_script_path.write_text("Write-Host proof\n", encoding="utf-8")

    _write_json(manifest_path, _base_manifest())
    _write_json(windows_gate_path, _base_windows_gate())
    _write_json(
        startup_smoke_path,
        {
            "status": "pass",
            "version": "run-20260612-121055",
            "releaseVersion": "run-20260612-121055",
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactDigest": "sha256:8341f191e65ded9b00f75290d65e58e01a4fa29c89fc970e975ddbc5c1999a33",
            "hostClass": "wine64-linux-x64-container",
        },
    )

    completed = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest",
            str(manifest_path),
            "--windows-gate",
            str(windows_gate_path),
            "--startup-smoke",
            str(startup_smoke_path),
            "--capture-script",
            str(capture_script_path),
            "--visual-proof",
            str(visual_proof_path),
            "--json-output",
            str(json_output),
            "--md-output",
            str(md_output),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads(json_output.read_text(encoding="utf-8"))
    assert payload["status"] == "needs_review"
    assert payload["only_blocker_is_visual_proof"] is True
    assert payload["release_shelf_root"] == str(tmp_path)
    assert payload["handoff_only"] is True
    assert payload["handoff_scope"] == "staged_nightly_windows_visual_proof"
    assert payload["stable_release_unchanged"] is True
    assert payload["requires_separate_publish_lane"] is True
    assert payload["release"]["version"] == "run-20260627-005402"
    assert payload["startup_smoke"]["matches_release_version"] is False
    assert payload["startup_smoke"]["matches_artifact_digest"] is False
    assert payload["startup_smoke"]["progress_log_exists"] is False
    assert payload["current_visual_proof_exists"] is False
    assert payload["current_visual_proof"]["stale"] is False
    assert "Startup smoke receipt version does not match the current Windows release candidate." in payload["blockers"]
    assert "Startup smoke receipt artifact digest does not match the current Windows installer digest." in payload["blockers"]
    assert payload["windows_installer"]["file_name"] == "chummer-avalonia-win-x64-installer.exe"
    assert payload["windows_installer"]["payload_file_name"] == "chummer-avalonia-win-x64-payload.zip"
    assert payload["required_screenshots"][0]["file_name"] == "windows-installer-progress.png"
    assert payload["required_screenshots"][1]["file_name"] == "windows-installer-completion.png"
    assert payload["required_screenshots"][0]["path"].endswith("windows-installer-visual-proof/windows-installer-progress.png")
    assert payload["required_screenshots"][1]["path"].endswith("windows-installer-visual-proof/windows-installer-completion.png")
    assert any("capture-windows-installer-visual-proof.ps1" in item for item in payload["next_actions"])
    assert any("-ReleaseChannelPath" in item for item in payload["next_actions"])
    assert any("-OutputPath" in item for item in payload["next_actions"])
    assert any("windows-installer-visual-proof" in item for item in payload["next_actions"])
    assert any("does not publish the live downloads shelf" in item for item in payload["next_actions"])
    assert "Windows Visual Proof Handoff" in md_output.read_text(encoding="utf-8")


def test_materialize_windows_visual_proof_handoff_prefers_gate_startup_smoke_receipt(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = tmp_path / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = tmp_path / "stale-startup-smoke-avalonia-win-x64.receipt.json"
    preferred_startup_smoke_path = tmp_path / "fresh" / "startup-smoke-avalonia-win-x64.receipt.json"
    capture_script_path = tmp_path / "capture-windows-installer-visual-proof.ps1"
    visual_proof_path = tmp_path / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    json_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    md_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"
    files_dir = tmp_path / "files"

    capture_script_path.write_text("Write-Host proof\n", encoding="utf-8")
    files_dir.mkdir()
    (files_dir / "chummer-avalonia-win-x64-installer.exe").write_bytes(b"installer")
    (files_dir / "chummer-avalonia-win-x64-payload.zip").write_bytes(b"payload")

    gate_payload = _base_windows_gate()
    gate_payload["checks"] = {
        "startup_smoke_receipt_path": str(preferred_startup_smoke_path),
        "startup_smoke_receipt_candidates": [str(startup_smoke_path), str(preferred_startup_smoke_path)],
    }

    _write_json(manifest_path, _base_manifest())
    _write_json(windows_gate_path, gate_payload)
    _write_json(
        startup_smoke_path,
        {
            "status": "pass",
            "version": "run-20260612-121055",
            "releaseVersion": "run-20260612-121055",
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactDigest": "sha256:8341f191e65ded9b00f75290d65e58e01a4fa29c89fc970e975ddbc5c1999a33",
            "hostClass": "wine64-linux-x64-container",
        },
    )
    _write_json(
        preferred_startup_smoke_path,
        {
            "status": "pass",
            "version": "run-20260627-005402",
            "releaseVersion": "run-20260627-005402",
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactDigest": "sha256:04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4",
            "hostClass": "local-win-x64",
        },
    )

    completed = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest",
            str(manifest_path),
            "--windows-gate",
            str(windows_gate_path),
            "--startup-smoke",
            str(startup_smoke_path),
            "--capture-script",
            str(capture_script_path),
            "--visual-proof",
            str(visual_proof_path),
            "--json-output",
            str(json_output),
            "--md-output",
            str(md_output),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads(json_output.read_text(encoding="utf-8"))
    assert payload["status"] == "ready_for_windows_host"
    assert payload["handoff_only"] is True
    assert payload["stable_release_unchanged"] is True
    assert payload["startup_smoke_path"] == str(preferred_startup_smoke_path)
    assert payload["startup_smoke"]["matches_release_version"] is True
    assert payload["startup_smoke"]["matches_artifact_digest"] is True
    assert payload["current_visual_proof_exists"] is False
    assert payload["windows_installer"]["local_candidates"]["installer_existing_paths"][0] == str(files_dir / "chummer-avalonia-win-x64-installer.exe")
    assert payload["windows_installer"]["local_candidates"]["payload_existing_paths"][0] == str(files_dir / "chummer-avalonia-win-x64-payload.zip")
    assert str(files_dir) in payload["windows_installer"]["local_candidates"]["files_root_candidates"]
    assert payload["blockers"] == []


def test_materialize_windows_visual_proof_handoff_marks_existing_visual_receipt_as_stale(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = tmp_path / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = tmp_path / "startup-smoke-avalonia-win-x64.receipt.json"
    capture_script_path = tmp_path / "capture-windows-installer-visual-proof.ps1"
    visual_proof_path = tmp_path / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    json_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    md_output = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"

    capture_script_path.write_text("Write-Host proof\n", encoding="utf-8")

    gate_payload = _base_windows_gate()
    gate_payload["summary"] = "Windows desktop exit gate failed: Windows installer visual proof artifactDigest does not match promoted installer bytes."
    gate_payload["reasons"] = ["Windows installer visual proof artifactDigest does not match promoted installer bytes."]

    _write_json(manifest_path, _base_manifest())
    _write_json(windows_gate_path, gate_payload)
    _write_json(
        startup_smoke_path,
        {
            "status": "pass",
            "version": "run-20260627-005402",
            "releaseVersion": "run-20260627-005402",
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactDigest": "sha256:04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4",
            "hostClass": "local-win-x64",
        },
    )
    _write_json(
        visual_proof_path,
        {
            "status": "pass",
            "version": "run-20260612-121055",
            "releaseVersion": "run-20260612-121055",
            "artifactDigest": "sha256:8341f191e65ded9b00f75290d65e58e01a4fa29c89fc970e975ddbc5c1999a33",
        },
    )

    completed = subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest",
            str(manifest_path),
            "--windows-gate",
            str(windows_gate_path),
            "--startup-smoke",
            str(startup_smoke_path),
            "--capture-script",
            str(capture_script_path),
            "--visual-proof",
            str(visual_proof_path),
            "--json-output",
            str(json_output),
            "--md-output",
            str(md_output),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads(json_output.read_text(encoding="utf-8"))
    assert payload["status"] == "ready_for_windows_host"
    assert payload["handoff_only"] is True
    assert payload["stable_release_unchanged"] is True
    assert payload["current_visual_proof_exists"] is True
    assert payload["current_visual_proof"]["matches_release_version"] is False
    assert payload["current_visual_proof"]["matches_installer_digest"] is False
    assert payload["current_visual_proof"]["stale"] is True
    assert payload["next_actions"][0].startswith("Overwrite the stale Windows visual-proof receipt")
