from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "desktop-downloads-matrix.yml"


def workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def job_if_condition(text: str, job_name: str) -> str:
    in_jobs = False
    in_target_job = False
    for line in text.splitlines():
        if line == "jobs:":
            in_jobs = True
            continue
        if not in_jobs:
            continue
        if line.startswith("  ") and not line.startswith("    "):
            in_target_job = line == f"  {job_name}:"
            continue
        if in_target_job and line.startswith("    if: "):
            return line.removeprefix("    if: ").strip()
    raise AssertionError(f"{job_name} must have an if condition")


def test_scheduled_publish_runs_only_at_vienna_08() -> None:
    text = workflow_text()

    assert "# GitHub schedules run in UTC. Two candidates cover Europe/Vienna DST;" in text
    assert "- cron: '0 6 * * *'" in text
    assert "- cron: '0 7 * * *'" in text
    assert 'vienna_hour="$(TZ=Europe/Vienna date +%H)"' in text
    assert 'if [[ "$vienna_hour" == "08" ]]; then' in text
    assert 'reason="scheduled 08:00 Europe/Vienna window ($vienna_stamp)"' in text
    assert 'reason="scheduled outside 08:00 Europe/Vienna window ($vienna_stamp)"' in text


def test_manual_proof_builds_do_not_publish_by_default() -> None:
    text = workflow_text()

    assert 'deploy_portal_downloads:' in text
    assert 'description: "Deploy downloads only when force_publish_downloads is also true."' in text
    assert 'publish_github_release:' in text
    assert 'description: "Publish rolling GitHub release only when force_publish_downloads is also true."' in text
    assert 'force_publish_downloads:' in text
    assert 'default: false' in text
    assert 'reason="manual build only"' in text
    assert (
        'elif [[ "${{ github.event_name }}" == "workflow_dispatch" && '
        '"${{ inputs.force_publish_downloads }}" == "true" ]]; then'
    ) in text


def test_manual_build_matrix_can_target_one_platform_but_publishing_builds_public_lanes() -> None:
    text = workflow_text()

    assert "requested_platform == \"public-windows-linux\"" in text
    assert "requested_platform == \"win-x64\"" in text
    assert "include = [win_x64]" in text
    assert "include = [linux_x64]" in text
    assert (
        'if event_name == "schedule" or force_publish.lower() == "true" '
        'or requested_platform == "public-windows-linux":'
    ) in text
    assert "include = [win_x64, linux_x64]" in text


def test_publish_and_deploy_jobs_are_gated_by_release_window() -> None:
    text = workflow_text()

    publish_if = job_if_condition(text, "publish-github-release")
    assert "needs.release-window.outputs.publish_allowed == 'true'" in publish_if
    assert "github.event_name == 'schedule'" in publish_if
    assert "inputs.publish_github_release" in publish_if

    deploy_jobs = [
        "deploy-downloads",
        "deploy-downloads-http",
        "deploy-downloads-object-storage",
    ]
    for job in deploy_jobs:
        condition = job_if_condition(text, job)
        assert "needs.release-window.outputs.publish_allowed == 'true'" in condition
        assert "github.ref_name == 'main'" in condition
        assert "github.event_name == 'schedule'" in condition
        assert "inputs.deploy_portal_downloads" in condition


def test_manual_build_job_can_run_for_proof_without_deploy_access() -> None:
    text = workflow_text()

    build_if = job_if_condition(text, "build-desktop")
    assert "github.event_name == 'workflow_dispatch'" in build_if
    assert "needs.release-window.outputs.publish_allowed == 'true'" in build_if

    readiness_if = job_if_condition(text, "audit-external-deploy-readiness")
    assert "needs.release-window.outputs.publish_allowed == 'true'" in readiness_if
    assert "inputs.require_external_deploy" in text
