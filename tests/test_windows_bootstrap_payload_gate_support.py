import os
from pathlib import Path
import subprocess


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_windows_desktop_exit_gate_supports_bootstrap_payload_sidecars() -> None:
    script = (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").read_text(encoding="utf-8")
    assert "import zipfile" in script
    assert "expected_bootstrap_payload_path" in script
    assert 'name[:-len("-installer.exe")] + "-payload.zip"' in script
    assert "bootstrap_payload_exists" in script
    assert "bootstrap_payload_sample_marker_present" in script
    assert "zip_contains_sample_character" in script
    assert "digest-size-and-payload-markers-or-bootstrap-sidecar" in script


def test_aggregate_desktop_gate_counts_bootstrap_payload_sidecars_as_valid_installer_markers() -> None:
    script = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh").read_text(encoding="utf-8")
    assert "import zipfile" in script
    assert 'name[:-len("-installer.exe")] + "-payload.zip"' in script
    assert "bootstrap_payload_present" in script
    assert 'summary["payload_and_sample_marker_present_paths"].append(str(path))' in script


def test_windows_visual_proof_capture_script_writes_gate_compatible_receipt() -> None:
    script = (REPO_ROOT / "scripts" / "capture-windows-installer-visual-proof.ps1").read_text(encoding="utf-8")
    assert "chummer6-ui.windows_installer_visual_proof" in script
    assert "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" in script
    assert "System.Windows.Forms.SystemInformation]::VirtualScreen" in script
    assert 'role = "progress"' in script
    assert 'role = "completion"' in script
    assert "artifactDigest = \"sha256:$installerSha256\"" in script
    assert "readabilityReview" in script
    assert "contrastReview" in script
    assert "clippingReview" in script


def test_desktop_release_pipeline_documents_windows_visual_capture_without_github_actions() -> None:
    doc = (REPO_ROOT / "docs" / "DESKTOP_RELEASE_PIPELINE.md").read_text(encoding="utf-8")
    assert "capture-windows-installer-visual-proof.ps1" in doc
    assert "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" in doc
    assert "host-specific gate" in doc
    assert "GitHub Actions" not in doc


def test_windows_bootstrap_build_fails_from_measured_size_gate_instead_of_hardcoded_policy(tmp_path: Path) -> None:
    publish_dir = tmp_path / "publish"
    dist_dir = tmp_path / "dist"
    publish_dir.mkdir()
    dist_dir.mkdir()
    (publish_dir / "Chummer.Avalonia.exe").write_bytes(b"stub")

    result = subprocess.run(
        [
            "bash",
            str(REPO_ROOT / "scripts" / "build-desktop-installer.sh"),
            str(publish_dir),
            "avalonia",
            "win-x64",
            "Chummer.Avalonia.exe",
            str(dist_dir),
            "0.0.0.1",
        ],
        text=True,
        capture_output=True,
        check=False,
        env={
            **dict(os.environ),
            "CHUMMER_WINDOWS_INSTALLER_MODE": "bootstrap",
        },
    )

    assert result.returncode != 0
    assert "bootstrap installer is too large:" in result.stderr
    assert "Windows bootstrap installer proof failed." in result.stderr
    assert "blocked until the native bootstrap builder is wired" not in result.stderr
