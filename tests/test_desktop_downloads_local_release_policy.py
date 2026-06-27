from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_github_actions_workflows_are_not_part_of_presentation_release_policy() -> None:
    assert not (REPO_ROOT / ".github" / ("work" + "flows")).exists()


def test_daily_publish_policy_is_documented_in_local_runbook() -> None:
    runbook = (REPO_ROOT / "docs" / "SELF_HOSTED_DOWNLOADS_RUNBOOK.md").read_text(encoding="utf-8")

    assert "RUNBOOK_MODE=publish-latest-nightly" in runbook
    assert "08:00 Europe/Vienna" in runbook
    assert "once per day in the morning release window" in runbook
    assert "Build only what the proof needs" in runbook
    assert ("workflow" + "_dispatch") not in runbook
    assert ("GitHub " + "Actions") not in runbook


def test_latest_nightly_publish_preflights_windows_bootstrap_payload_metadata() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "verify_latest_stage_windows_payload_gate()" in publisher
    assert "verify-windows-installer-payloads.py" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert "--allow-empty" in publisher
    assert "Nightly stage failed Windows installer payload preflight. Build a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')


def test_latest_nightly_publish_requires_windows_installer_startup_smoke_before_promotion() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}"' in publisher
    assert 'SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-0}"' in publisher
    assert 'ALLOW_SKIPPED_STARTUP_SMOKE="${CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE:-0}"' in publisher
    assert "verify_latest_stage_windows_startup_smoke_gate()" in publisher
    assert "Windows installer startup-smoke receipt is missing" in publisher
    assert "Windows installer startup-smoke receipt is not passing" in publisher
    assert "Windows installer startup-smoke receipt artifactDigest mismatch" in publisher
    assert "Nightly stage failed Windows installer startup smoke preflight. Build and smoke-test a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')


def test_s3_publish_windows_payload_gate_allows_empty_only_before_installers_are_added() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "windows_payload_gate_args=(" in publisher
    assert "--files-dir \"$FILES_SOURCE\"" in publisher
    assert "--manifest \"$MANIFEST_SOURCE\"" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert 'if [[ "${#windows_payload_gate_args[@]}" -eq 6 ]]; then' in publisher
    assert "--allow-empty" in publisher


def test_windows_bootstrap_build_is_measured_by_the_real_payload_gate() -> None:
    builder = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")

    assert 'local installer_mode="${CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap}"' in builder
    assert 'bootstrap_payload_url="${CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL:-${downloads_prefix%/}/$(basename "$payload_zip")}"' in builder
    assert 'verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"' in builder
    assert "Windows bootstrap installer build is blocked until the native bootstrap builder is wired." not in builder
    assert "The .NET WinForms installer is too large for bootstrap promotion" not in builder
    assert "Use CHUMMER_WINDOWS_INSTALLER_MODE=bundled for a local full installer" not in builder
    assert "bundled|append|appended)" in builder


def test_release_manifest_generation_prunes_install_proof_routes_to_published_artifacts() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "prune_release_proof_routes_to_manifest_artifacts" in generator
    assert 'route.startswith("/downloads/install/")' in generator
    assert 'artifact_id in artifact_ids' in generator
    assert 'release_proof["proofRoutes"] = prune_routes' in generator


def test_release_build_checks_are_owned_by_local_scripts() -> None:
    assert (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").is_file()


def test_linux_desktop_exit_gate_reports_direct_host_build_failures_before_missing_host_noise() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert 'local test_output_root="$test_project_dir/bin/Release"' in gate
    assert 'local test_assembly_path="$test_project_dir/bin/Release/$FRAMEWORK/$TEST_ASSEMBLY_NAME"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "${TEST_ASSEMBLY_NAME%.dll}"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "$TEST_ASSEMBLY_NAME"' in gate
    assert 'KEEP_SOURCE_SNAPSHOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_KEEP_SOURCE_SNAPSHOT:-0}"' in gate
    assert '[linux-desktop-exit-gate] desktop runtime test host build failed' in gate
    assert 'desktop runtime test host via dotnet' in gate
    assert 'exec dotnet "$(basename "$test_assembly_path")" "$@"' in gate
    assert 'Promoted Linux installer file is missing from the release-aligned desktop shelf' in gate
    assert gate.index('desktop runtime test host build failed') < gate.index('desktop runtime test host is missing or not executable')


def test_windows_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted Windows installer was not resolved from the release-aligned desktop shelf." in gate


def test_macos_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_MACOS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted macOS installer was not resolved from the release-aligned desktop shelf" in gate


def test_aggregate_desktop_materializer_defers_to_release_aligned_shelf_resolution() -> None:
    gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'release_channel_path_value = globals().get("release_channel_path")' in gate
    assert 'release_channel_root = (' in gate
    assert 'release_aligned_files_root = release_channel_root / "files"' in gate
    assert 'release_aligned_startup_smoke_root = release_channel_root / "startup-smoke"' in gate
    assert 'installer_path = str(release_aligned_files_root / installer_name)' in gate
    assert 'mkdir -p {release_aligned_files_root}' in gate
    assert 'installer_path_suffix = f"/files/{installer_name}"' in gate
    assert 'startup_smoke_suffix = "/startup-smoke"' in gate


def test_next90_m144_guard_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (
        REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "next90-m144-ui-startup-smoke-and-executable-gate-check.sh"
    ).read_text(encoding="utf-8")

    assert 'default_downloads_root="$repo_root/Docker/Downloads/files"' in gate
    assert 'default_startup_smoke_dir="$repo_root/Docker/Downloads/startup-smoke"' in gate
    assert 'release_channel_directory="$(cd "$(dirname "$release_channel_path")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'release_aligned_downloads_root="$release_channel_directory/files"' in gate
    assert 'release_aligned_startup_smoke_dir="$release_channel_directory/startup-smoke"' in gate
    assert 'downloads_root="$CHUMMER_NEXT90_M144_DOWNLOADS_ROOT"' in gate
    assert 'downloads_root="$release_aligned_downloads_root"' in gate
    assert 'startup_smoke_dir="$CHUMMER_NEXT90_M144_STARTUP_SMOKE_DIR"' in gate
    assert 'startup_smoke_dir="$release_aligned_startup_smoke_dir"' in gate
    assert "is missing a local artifact under the release-aligned desktop shelf." in gate
