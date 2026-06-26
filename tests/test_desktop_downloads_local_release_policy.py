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


def test_release_build_checks_are_owned_by_local_scripts() -> None:
    assert (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").is_file()
