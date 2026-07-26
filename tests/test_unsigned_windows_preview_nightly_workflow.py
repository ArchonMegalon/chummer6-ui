from __future__ import annotations

from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "unsigned-windows-preview-nightly-candidate-export.yml"
)


def load_workflow() -> dict:
    value = yaml.load(WORKFLOW.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
    assert isinstance(value, dict)
    return value


def test_workflow_has_exact_dispatch_inputs_and_fail_closed_permissions() -> None:
    workflow = load_workflow()
    inputs = workflow["on"]["workflow_dispatch"]["inputs"]
    assert set(inputs) == {
        "runner_nonce",
        "candidate_version",
        "candidate_manifest_sha256",
        "expected_source_sha",
        "export_confirmed",
    }
    assert workflow["permissions"] == {}
    assert workflow["jobs"]["preflight"]["permissions"] == {}
    assert workflow["jobs"]["export"]["permissions"] == {"contents": "read"}
    assert workflow["jobs"]["relay-capture"]["permissions"] == {
        "actions": "write",
        "contents": "read",
    }
    source = WORKFLOW.read_text(encoding="utf-8")
    assert source.count("actions: write") == 1
    assert "packages: write" not in source
    assert "id-token: write" not in source


def test_export_job_is_bound_to_one_nonce_jit_runner() -> None:
    workflow = load_workflow()
    export = workflow["jobs"]["export"]
    assert export["name"] == "Export exact unsigned Windows candidate bytes"
    assert export["runs-on"] == [
        "self-hosted",
        "linux",
        "x64",
        "${{ needs.preflight.outputs.runner_label }}",
    ]
    assert export["concurrency"]["cancel-in-progress"] == "false"
    assert "environment" not in export


def test_workflow_exports_unsigned_candidate_and_only_evidence_relay() -> None:
    source = WORKFLOW.read_text(encoding="utf-8")
    assert "preview_nightly_unsigned_candidate_export.py" in source
    assert "--candidate-root \"$CANDIDATE_INPUT_ROOT\"" in source
    assert "--source-workflow .github/workflows/unsigned-windows-preview-nightly-candidate-export.yml" in source
    assert (
        "unsigned-windows-preview-nightly-candidate-${{ github.run_id }}-${{ github.run_attempt }}"
        in source
    )
    assert "compression-level: 0" in source
    assert "overwrite: false" in source
    assert "persist-credentials: false" in source
    assert "relay-capture" in source
    assert "createWorkflowDispatch" in source
    assert (
        "unsigned-windows-preview-native-evidence-capture.yml"
        in source
    )
    for forbidden in (
        "windows-native-evidence-capture",
        "sign-windows-artifacts",
        "authenticode",
        "visual-approval",
        "human-approval",
        "publish-download",
        "deploy-downloads",
        "deploy_authorized: true",
    ):
        assert forbidden not in source.lower()


def test_actions_are_immutable_commit_pins() -> None:
    workflow = load_workflow()
    steps = workflow["jobs"]["export"]["steps"]
    uses = [step["uses"] for step in steps if "uses" in step]
    assert uses == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    relay_uses = [
        step["uses"]
        for step in workflow["jobs"]["relay-capture"]["steps"]
        if "uses" in step
    ]
    assert relay_uses == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
    ]


def test_preflight_requires_exact_main_sha_and_confirmation() -> None:
    source = WORKFLOW.read_text(encoding="utf-8")
    assert '"EXPORT_CONFIRMED": r"true"' in source
    assert '"SOURCE_REF": r"refs/heads/main"' in source
    assert '"DEFAULT_BRANCH": r"main"' in source
    assert 'os.environ["SOURCE_SHA"] != os.environ["EXPECTED_SOURCE_SHA"]' in source


def test_preflight_rejects_forks_and_exporter_argument_is_pinned() -> None:
    source = WORKFLOW.read_text(encoding="utf-8")
    assert "SOURCE_REPOSITORY: ${{ github.repository }}" in source
    assert (
        '"SOURCE_REPOSITORY": re.escape("ArchonMegalon/chummer6-ui")'
        in source
    )
    assert "--source-repository ArchonMegalon/chummer6-ui" in source
    assert '--source-repository "$GITHUB_REPOSITORY"' not in source


def test_scoped_relay_makes_bot_only_capture_reachable() -> None:
    workflow = load_workflow()
    relay = workflow["jobs"]["relay-capture"]
    assert relay["needs"] == ["preflight", "export"]
    assert relay["runs-on"] == "ubuntu-24.04"
    assert relay["timeout-minutes"] == "5"
    assert relay["permissions"] == {
        "actions": "write",
        "contents": "read",
    }
    assert len(relay["steps"]) == 2
    dispatch = relay["steps"][1]
    assert dispatch["uses"] == (
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea"
    )
    assert dispatch["with"]["github-token"] == "${{ github.token }}"
    script = dispatch["with"]["script"]
    for required in (
        "getWorkflowRun",
        "run.data.status !== 'in_progress'",
        "run.data.conclusion !== null",
        "workflowRunPathMatches",
        "listWorkflowRunArtifacts",
        "artifact.expired === false",
        "git.getRef",
        "repos.getContent",
        "createWorkflowDispatch",
        "unsigned-windows-preview-native-evidence-capture.yml",
        "candidate_run_id",
        "candidate_run_attempt",
        "candidate_sha",
        "candidate_actor",
        "candidate_artifact_id",
        "candidate_artifact_name",
        "candidate_artifact_sha256",
        "candidate_version",
        "candidate_manifest_sha256",
        "candidate_inventory_sha256",
        "expected_contract_sha",
        "capture_confirmed: true",
        "ref: 'main'",
    ):
        assert required in script
    lowered = script.lower()
    for forbidden in (
        "secrets.",
        "createdeployment",
        "createrelease",
        "createorupdaterelease",
        "uploadreleaseasset",
        "contents.write",
        "packages",
        "id-token",
    ):
        assert forbidden not in lowered
