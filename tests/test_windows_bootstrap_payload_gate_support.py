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
    assert "Resolve-DefaultReleaseChannelPath" in script
    assert 'Join-Path $RepoRoot ".tmp\\verify-release-channel\\RELEASE_CHANNEL.generated.json"' in script
    assert "Resolve-InstallerFileNameFromReleaseChannel" in script
    assert 'Join-Path $releaseChannelDirectory "files"' in script
    assert 'Join-Path $outputDirectory "windows-installer-visual-proof"' in script
    assert 'Join-Path $RepoRoot "..\\chummer.run-services\\Chummer.Portal\\downloads\\RELEASE_CHANNEL.generated.json"' in script
    assert "System.Windows.Forms.SystemInformation]::VirtualScreen" in script
    assert 'role = "progress"' in script
    assert 'role = "completion"' in script
    assert "artifactDigest = \"sha256:$installerSha256\"" in script
    assert "readabilityReview" in script
    assert "contrastReview" in script
    assert "clippingReview" in script
    assert "Confirm-OperatorReview" in script
    assert '[string]$Reviewer = ""' in script
    assert "Resolve-InteractiveReviewer" in script
    assert "Enter your reviewer name or accountable operator ID" in script
    assert '$automationIdentityTokens = @(' in script
    assert '[regex]::Matches($normalizedCandidate, "[\\p{L}\\p{Nd}]+")' in script
    assert "$containsAutomationToken" in script
    assert '$reviewer = Resolve-InteractiveReviewer' in script
    assert 'capture_mode = $(if ($Auto) { "auto" } else { "interactive" })' in script
    assert "human_review_confirmed = $humanReviewConfirmed" in script
    assert "human_reviewer_identified" in script
    assert "reviewer_authorization_deferred_to_exit_gate" in script
    assert '$status = "needs_review"' in script
    assert '$reviewer = $(if ($Auto) { "automation" } else { "operator" })' not in script


def test_desktop_release_pipeline_documents_windows_visual_capture_without_github_actions() -> None:
    doc = (REPO_ROOT / "docs" / "DESKTOP_RELEASE_PIPELINE.md").read_text(encoding="utf-8")
    assert "capture-windows-installer-visual-proof.ps1" in doc
    assert "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" in doc
    assert ".tmp\\verify-release-channel\\RELEASE_CHANNEL.generated.json" in doc
    assert "windows-installer-visual-proof" in doc
    assert "release-manifest shelf" in doc
    assert "host-specific gate" in doc
    assert "specific reviewer name or accountable operator ID" in doc
    assert "separately confirm readability, contrast, and clipping" in doc
    assert "Generic labels such as `operator`" in doc
    assert "CHUMMER_WINDOWS_VISUAL_AUTHORIZED_REVIEWER_IDS" in doc
    assert "GitHub Actions" not in doc


def test_native_windows_bootstrap_builder_stages_pinned_windows_curl_helper() -> None:
    script = (REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh").read_text(encoding="utf-8")
    assert "CHUMMER_WINDOWS_CURL_URL" in script
    assert "CHUMMER_WINDOWS_CURL_SHA256" in script
    assert 'mkdir -p "$STAGE_DIR/curl"' in script
    assert 'prefetch_pinned_asset "$CURL_WINDOWS_URL" "$CURL_WINDOWS_SHA256"' in script
    assert "urllib.request" in script
    assert 'parsed_url.scheme != "https"' in script
    assert 'sha256sum --check --strict -' in script
    assert '7z e -aoa -o/work/curl /toolchain/assets/curl-win64.zip' in script
    assert '"*/bin/curl.exe"' in script
    assert '"*/bin/libcurl-x64.dll"' in script
    assert '"*/bin/curl-ca-bundle.crt"' in script


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

    assert result.returncode == 0, f"{result.stdout}\n{result.stderr}"
    installer_path = dist_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = dist_dir / "files" / "chummer-avalonia-win-x64-payload.zip"
    payload_sidecar_path = dist_dir / "files" / "chummer-avalonia-win-x64-payload.zip.json"

    assert installer_path.is_file()
    assert payload_path.is_file()
    assert payload_sidecar_path.is_file()
    assert installer_path.stat().st_size < 15 * 1024 * 1024

    combined_output = f"{result.stdout}\n{result.stderr}"
    assert "built installer" in combined_output
    assert "blocked until the native bootstrap builder is wired" not in combined_output


def test_windows_bootstrap_build_rejects_files_directory_as_dist_root(tmp_path: Path) -> None:
    publish_dir = tmp_path / "publish"
    dist_files_dir = tmp_path / "nightly-run-test" / "files"
    publish_dir.mkdir()
    dist_files_dir.mkdir(parents=True)
    (publish_dir / "Chummer.Avalonia.exe").write_bytes(b"stub")

    result = subprocess.run(
        [
            "bash",
            str(REPO_ROOT / "scripts" / "build-desktop-installer.sh"),
            str(publish_dir),
            "avalonia",
            "win-x64",
            "Chummer.Avalonia.exe",
            str(dist_files_dir),
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
    combined_output = f"{result.stdout}\n{result.stderr}"
    assert "Refusing to use a downloads files/ directory as the desktop installer dist root" in combined_output
    assert "Pass the release stage root" in combined_output
