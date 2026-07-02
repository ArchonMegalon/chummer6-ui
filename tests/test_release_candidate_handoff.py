from __future__ import annotations

import hashlib
import json
import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py"


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def test_release_candidate_handoff_embeds_stage_windows_visual_proof_packet(tmp_path: Path) -> None:
    stage_dir = tmp_path / "nightly-run-20260628-000000"
    files_dir = stage_dir / "files"
    startup_smoke_dir = stage_dir / "startup-smoke"
    files_dir.mkdir(parents=True)
    startup_smoke_dir.mkdir(parents=True)

    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    installer_path.write_bytes(b"windows-installer-stub")
    payload_path.write_bytes(b"payload-zip-stub")
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()

    release_version = "run-test-0.0.0.1"
    _write_json(
        stage_dir / "RELEASE_CHANNEL.generated.json",
        {
            "channelId": "preview",
            "version": release_version,
            "artifacts": [
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "head": "avalonia",
                    "platform": "windows",
                    "rid": "win-x64",
                    "kind": "installer",
                    "fileName": installer_path.name,
                    "downloadUrl": f"https://chummer.run/downloads/files/{installer_path.name}",
                    "sha256": installer_sha256,
                    "payloadFileName": payload_path.name,
                    "payloadDownloadUrl": f"https://chummer.run/downloads/files/{payload_path.name}",
                    "releaseVersion": release_version,
                    "version": release_version,
                }
            ],
        },
    )
    _write_json(
        startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {
            "status": "pass",
            "headId": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "readyCheckpoint": "pre_ui_event_loop",
            "hostClass": "local-win-x64",
            "artifactDigest": f"sha256:{installer_sha256}",
            "artifactFileName": installer_path.name,
            "version": release_version,
            "releaseVersion": release_version,
        },
    )
    _write_json(
        stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
        {
            "status": "failed",
            "summary": "Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",
            "blockingMode": "external_only",
            "reasons": [
                "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."
            ],
        },
    )

    completed = subprocess.run(
        ["python3", str(SCRIPT), str(stage_dir)],
        check=False,
        capture_output=True,
        text=True,
        env={
            **dict(os.environ),
            "CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH": str(tmp_path / "missing-gate-script.sh"),
        },
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads((stage_dir / "RELEASE_BUILD_HANDOFF.generated.json").read_text(encoding="utf-8"))
    windows_exit_gate_refresh = payload["windows_exit_gate_refresh"]
    windows_visual_proof_handoff = payload["windows_visual_proof_handoff"]

    assert payload["handoff_only"] is True
    assert payload["handoff_scope"] == "staged_nightly"
    assert payload["stable_release_unchanged"] is True
    assert payload["requires_separate_publish_lane"] is True
    assert payload["stage_proof_complete"] is False
    assert payload["promotion_ready"] is False
    assert "Windows visual proof is still outstanding for the staged installer bytes." in payload["blockers"]
    assert any("stable channel unchanged" in action for action in payload["next_actions"])
    assert all("CHUMMER_RELEASE_UPLOAD_TOKEN" not in action for action in payload["next_actions"])
    assert windows_exit_gate_refresh["status"] == "failed"
    assert windows_exit_gate_refresh["blocking_mode"] == "external_only"
    assert windows_visual_proof_handoff["status"] == "ready_for_windows_host"
    assert Path(windows_visual_proof_handoff["json_path"]).is_file()
    assert Path(windows_visual_proof_handoff["md_path"]).is_file()
    assert any(
        "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json" in action
        for action in payload["next_actions"]
    )


def test_release_candidate_handoff_closes_when_stage_windows_visual_proof_matches(tmp_path: Path) -> None:
    stage_dir = tmp_path / "nightly-run-20260628-020000"
    files_dir = stage_dir / "files"
    startup_smoke_dir = stage_dir / "startup-smoke"
    files_dir.mkdir(parents=True)
    startup_smoke_dir.mkdir(parents=True)

    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    installer_path.write_bytes(b"windows-installer-stub")
    payload_path.write_bytes(b"payload-zip-stub")
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()

    release_version = "run-test-0.0.0.3"
    _write_json(
        stage_dir / "RELEASE_CHANNEL.generated.json",
        {
            "channelId": "preview",
            "version": release_version,
            "desktopTupleCoverage": {
                "missingRequiredPlatforms": [],
            },
            "artifacts": [
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "head": "avalonia",
                    "platform": "windows",
                    "rid": "win-x64",
                    "kind": "installer",
                    "fileName": installer_path.name,
                    "downloadUrl": f"https://chummer.run/downloads/files/{installer_path.name}",
                    "sha256": installer_sha256,
                    "payloadFileName": payload_path.name,
                    "payloadDownloadUrl": f"https://chummer.run/downloads/files/{payload_path.name}",
                    "releaseVersion": release_version,
                    "version": release_version,
                }
            ],
        },
    )
    _write_json(
        startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {
            "status": "pass",
            "headId": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "readyCheckpoint": "pre_ui_event_loop",
            "hostClass": "local-win-x64",
            "artifactDigest": f"sha256:{installer_sha256}",
            "artifactFileName": installer_path.name,
            "version": release_version,
            "releaseVersion": release_version,
        },
    )
    _write_json(
        stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
        {
            "status": "passed",
            "summary": "Windows desktop exit gate passed.",
            "blockingMode": "none",
            "reasons": [],
        },
    )
    _write_json(
        stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json",
        {
            "contract_name": "chummer6-ui.windows_installer_visual_proof",
            "status": "pass",
            "version": release_version,
            "releaseVersion": release_version,
            "artifactDigest": f"sha256:{installer_sha256}",
        },
    )

    completed = subprocess.run(
        ["python3", str(SCRIPT), str(stage_dir)],
        check=False,
        capture_output=True,
        text=True,
        env={
            **dict(os.environ),
            "CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH": str(tmp_path / "missing-gate-script.sh"),
        },
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads((stage_dir / "RELEASE_BUILD_HANDOFF.generated.json").read_text(encoding="utf-8"))
    windows_visual_proof_handoff = payload["windows_visual_proof_handoff"]

    assert payload["handoff_only"] is True
    assert payload["handoff_scope"] == "staged_nightly"
    assert payload["stable_release_unchanged"] is True
    assert payload["requires_separate_publish_lane"] is True
    assert payload["stage_proof_complete"] is True
    assert payload["promotion_ready"] is True
    assert payload["blockers"] == []
    assert windows_visual_proof_handoff["status"] == "ready"
    assert windows_visual_proof_handoff["blockers"] == []
    assert Path(windows_visual_proof_handoff["json_path"]).is_file()
    assert any("stable channel unchanged" in action for action in payload["next_actions"])
    assert all("CHUMMER_RELEASE_UPLOAD_TOKEN" not in action for action in payload["next_actions"])


def test_release_candidate_handoff_can_refresh_stage_local_windows_exit_gate_with_override_script(tmp_path: Path) -> None:
    stage_dir = tmp_path / "nightly-run-20260628-010000"
    files_dir = stage_dir / "files"
    startup_smoke_dir = stage_dir / "startup-smoke"
    files_dir.mkdir(parents=True)
    startup_smoke_dir.mkdir(parents=True)

    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    installer_path.write_bytes(b"windows-installer-stub")
    payload_path.write_bytes(b"payload-zip-stub")
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()

    release_version = "run-test-0.0.0.2"
    _write_json(
        stage_dir / "RELEASE_CHANNEL.generated.json",
        {
            "channelId": "preview",
            "version": release_version,
            "artifacts": [
                {
                    "artifactId": "avalonia-win-x64-installer",
                    "head": "avalonia",
                    "platform": "windows",
                    "rid": "win-x64",
                    "kind": "installer",
                    "fileName": installer_path.name,
                    "downloadUrl": f"https://chummer.run/downloads/files/{installer_path.name}",
                    "sha256": installer_sha256,
                    "payloadFileName": payload_path.name,
                    "payloadDownloadUrl": f"https://chummer.run/downloads/files/{payload_path.name}",
                    "releaseVersion": release_version,
                    "version": release_version,
                }
            ],
        },
    )
    _write_json(
        startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {
            "status": "pass",
            "headId": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "readyCheckpoint": "pre_ui_event_loop",
            "hostClass": "local-win-x64",
            "artifactDigest": f"sha256:{installer_sha256}",
            "artifactFileName": installer_path.name,
            "version": release_version,
            "releaseVersion": release_version,
        },
    )

    gate_stub = tmp_path / "gate-stub.sh"
    gate_stub.write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                f'[[ \"$CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH\" == \"{stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"}\" ]]',
                'cat > \"$CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH\" <<EOF',
                "{",
                '  "status": "failed",',
                '  "summary": "Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",',
                '  "blockingMode": "external_only",',
                '  "reasons": ["Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."]',
                "}",
                "EOF",
                "exit 1",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    gate_stub.chmod(0o755)

    completed = subprocess.run(
        ["python3", str(SCRIPT), str(stage_dir)],
        check=False,
        capture_output=True,
        text=True,
        env={
            **dict(os.environ),
            "CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH": str(gate_stub),
        },
    )

    assert completed.returncode == 0, completed.stderr

    payload = json.loads((stage_dir / "RELEASE_BUILD_HANDOFF.generated.json").read_text(encoding="utf-8"))
    assert payload["handoff_only"] is True
    assert payload["stable_release_unchanged"] is True
    assert payload["requires_separate_publish_lane"] is True
    assert payload["stage_proof_complete"] is False
    assert payload["windows_exit_gate_refresh"]["script_path"] == str(gate_stub)
    assert payload["windows_exit_gate_refresh"]["status"] == "failed"
    assert payload["windows_exit_gate_refresh"]["blocking_mode"] == "external_only"
    assert payload["windows_visual_proof_handoff"]["status"] == "ready_for_windows_host"
    assert Path(payload["windows_exit_gate_refresh"]["json_path"]).is_file()
