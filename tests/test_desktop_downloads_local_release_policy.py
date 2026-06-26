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


def test_windows_bootstrap_build_fails_closed_until_native_builder_is_wired() -> None:
    builder = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")

    assert "Windows bootstrap installer build is blocked until the native bootstrap builder is wired." in builder
    assert "The .NET WinForms installer is too large for bootstrap promotion" in builder
    assert "Use CHUMMER_WINDOWS_INSTALLER_MODE=bundled for a local full installer" in builder
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
