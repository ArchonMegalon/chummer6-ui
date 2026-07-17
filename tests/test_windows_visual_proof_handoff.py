from __future__ import annotations

import hashlib
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
    assert payload["windows_operator_commands"]["stage_root"] == str(tmp_path)
    assert "capture-windows-installer-visual-proof.ps1" in payload["windows_operator_commands"]["stage_local_powershell"]
    assert "<windows-stage>\\RELEASE_CHANNEL.generated.json" in payload["windows_operator_commands"]["windows_stage_template_powershell"]
    assert str(visual_proof_path) in payload["windows_operator_commands"]["copy_back_required_paths"]
    assert any(path.endswith("windows-installer-progress.png") for path in payload["windows_operator_commands"]["copy_back_required_paths"])
    assert "copy the whole stage directory" in payload["windows_operator_commands"]["copy_back_note"]
    artifact_intake = payload["operator_artifact_intake"]
    assert artifact_intake["external_artifact_required"] is True
    assert artifact_intake["preferred_drop_root"] == str(tmp_path)
    assert artifact_intake["preferred_visual_proof_receipt_path"] == str(visual_proof_path)
    assert artifact_intake["preferred_screenshot_dir"] == str(tmp_path / "windows-installer-visual-proof")
    assert artifact_intake["required_copy_back_paths"] == payload["windows_operator_commands"]["copy_back_required_paths"]
    assert "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" in artifact_intake["accepted_file_patterns"]
    assert "windows-installer-progress.png" in artifact_intake["accepted_file_patterns"]
    assert "windows-installer-completion.png" in artifact_intake["accepted_file_patterns"]
    assert "artifact_intake.py discover" in artifact_intake["discover_receipt_command"]
    assert "--pattern 'WINDOWS_INSTALLER_VISUAL_PROOF.generated.json'" in artifact_intake["discover_receipt_command"]
    assert "--pattern 'windows-installer-*.png'" in artifact_intake["discover_screenshot_command"]
    assert str(tmp_path) in artifact_intake["discover_receipt_command"]
    assert artifact_intake["post_copy_verify_command"] == payload["windows_operator_commands"]["linux_exit_gate_after_copy_back"]
    assert payload["required_screenshots"][0]["file_name"] == "windows-installer-progress.png"
    assert payload["required_screenshots"][1]["file_name"] == "windows-installer-completion.png"
    assert payload["required_screenshots"][0]["path"].endswith("windows-installer-visual-proof/windows-installer-progress.png")
    assert payload["required_screenshots"][1]["path"].endswith("windows-installer-visual-proof/windows-installer-completion.png")
    assert any("capture-windows-installer-visual-proof.ps1" in item for item in payload["next_actions"])
    assert any("-ReleaseChannelPath" in item for item in payload["next_actions"])
    assert any("-OutputPath" in item for item in payload["next_actions"])
    assert any("<windows-stage>\\RELEASE_CHANNEL.generated.json" in item for item in payload["next_actions"])
    assert any("windows-installer-visual-proof" in item for item in payload["next_actions"])
    assert any("does not publish the live downloads shelf" in item for item in payload["next_actions"])
    markdown = md_output.read_text(encoding="utf-8")
    assert "Windows Visual Proof Handoff" in markdown
    assert "## Artifact intake" in markdown
    assert "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" in markdown


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
    assert payload["startup_smoke"]["receipt_file_name"] == preferred_startup_smoke_path.name
    assert payload["startup_smoke"]["receipt_sha256"] == hashlib.sha256(
        preferred_startup_smoke_path.read_bytes()
    ).hexdigest()
    assert payload["current_visual_proof_exists"] is False
    assert payload["windows_installer"]["local_candidates"]["installer_existing_paths"][0] == str(files_dir / "chummer-avalonia-win-x64-installer.exe")
    assert payload["windows_installer"]["local_candidates"]["payload_existing_paths"][0] == str(files_dir / "chummer-avalonia-win-x64-payload.zip")
    assert str(files_dir) in payload["windows_installer"]["local_candidates"]["files_root_candidates"]
    assert payload["blockers"] == []


def test_materialize_windows_visual_proof_handoff_surfaces_gold_bundle_intake(tmp_path: Path) -> None:
    stage_root = tmp_path / "run-services" / "Chummer.Portal" / "downloads"
    manifest_path = stage_root / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = stage_root / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = stage_root / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
    capture_script_path = tmp_path / "capture-windows-installer-visual-proof.ps1"
    visual_proof_path = stage_root / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    json_output = stage_root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    md_output = stage_root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"
    intake_request_path = (
        tmp_path
        / "run-services"
        / ".codex-studio"
        / "published"
        / "WINDOWS_INSTALLER_VISUAL_AUDIT_INTAKE_REQUEST.generated.json"
    )
    preferred_drop_path = (
        tmp_path
        / "run-services"
        / ".state"
        / "incoming_windows_installer_gold_proof"
        / "windows-installer-gold-proof-04ae1f160e29.zip"
    )

    capture_script_path.write_text("Write-Host proof\n", encoding="utf-8")

    _write_json(manifest_path, _base_manifest())
    _write_json(windows_gate_path, _base_windows_gate())
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
        intake_request_path,
        {
            "status": "external_artifact_required",
            "summary": "Provide the promoted-digest Windows gold proof bundle.",
            "promoted_installer_sha256": "04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4",
            "preferred_zip_name": "windows-installer-gold-proof-04ae1f160e29.zip",
            "preferred_drop_folder": str(preferred_drop_path.parent),
            "preferred_drop_path": str(preferred_drop_path),
            "artifact_intake": {
                "discover_command": "python3 ~/.codex/skills/ea-artifact-intake/scripts/artifact_intake.py discover --pattern '*windows-installer-gold-proof*.zip'",
                "import_command": f"python3 scripts/import_windows_installer_gold_proof_artifact.py {preferred_drop_path}",
                "auto_import_watch_command": "python3 scripts/auto_import_windows_installer_gold_proof.py --wait-seconds 900",
                "post_import_verify_command": "python3 scripts/verify_windows_installer_visual_audit.py",
                "post_import_verify_note": "Import reruns the full verifier chain.",
                "startup_receipt_bundle_required": False,
            },
            "operator_request": {
                "summary": "Provide the promoted-digest Windows gold proof bundle.",
                "powershell_commands": [
                    "${REPO_ROOT}\\scripts\\capture_windows_installer_gold_proof.ps1 -InstallerPath ${REPO_ROOT}\\Chummer.Portal\\downloads\\files\\chummer-avalonia-win-x64-installer.exe",
                    "Compress-Archive -Path ${REPO_ROOT}\\Chummer.Portal\\downloads\\visual-audit\\windows-installer\\* -DestinationPath windows-installer-gold-proof-04ae1f160e29.zip -Force",
                ],
                "copy_to_windows": [
                    "Copy the repository checkout or at least Chummer.Portal/downloads/files and scripts to the Windows host."
                ],
            },
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
    assert payload["intake_request_path"] == str(intake_request_path)
    artifact_intake = payload["operator_artifact_intake"]
    assert artifact_intake["intake_request_path"] == str(intake_request_path)
    gold_bundle = artifact_intake["gold_proof_bundle_intake"]
    assert gold_bundle["available"] is True
    assert gold_bundle["preferred_zip_name"] == "windows-installer-gold-proof-04ae1f160e29.zip"
    assert gold_bundle["preferred_drop_path"] == str(preferred_drop_path)
    assert "import_windows_installer_gold_proof_artifact.py" in gold_bundle["import_command"]
    assert "capture_windows_installer_gold_proof.ps1" in gold_bundle["powershell_commands"][0]
    assert any("windows-installer-gold-proof-04ae1f160e29.zip" in item for item in payload["next_actions"])
    assert any("import_windows_installer_gold_proof_artifact.py" in item for item in payload["next_actions"])
    markdown = md_output.read_text(encoding="utf-8")
    assert "## Release-Truth Bundle Intake" in markdown
    assert "windows-installer-gold-proof-04ae1f160e29.zip" in markdown
    assert "import_windows_installer_gold_proof_artifact.py" in markdown


def test_materialize_windows_visual_proof_handoff_marks_matching_visual_receipt_ready(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = tmp_path / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = tmp_path / "startup-smoke-avalonia-win-x64.receipt.json"
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
    gate_payload["status"] = "passed"
    gate_payload["summary"] = "Windows desktop exit gate passed."
    gate_payload["reasons"] = []

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
            "version": "run-20260627-005402",
            "releaseVersion": "run-20260627-005402",
            "artifactDigest": "sha256:04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4",
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
    assert payload["status"] == "ready"
    assert payload["blockers"] == []
    assert payload["current_visual_proof_exists"] is True
    assert payload["current_visual_proof"]["matches_release_version"] is True
    assert payload["current_visual_proof"]["matches_installer_digest"] is True
    assert payload["current_visual_proof"]["stale"] is False
    assert payload["startup_smoke"]["matches_release_version"] is True
    assert payload["startup_smoke"]["matches_artifact_digest"] is True
    assert payload["next_actions"] == [
        "This staged nightly handoff is complete. Keep the stable release unchanged unless a separate guarded stable publish is intentionally run.",
        "Use the public nightly/preview shelf for handoff verification; do not recapture Windows proof unless the staged installer bytes change.",
    ]


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
