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
    source = WORKFLOW.read_text(encoding="utf-8")
    assert "actions: write" not in source
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


def test_workflow_exports_only_unsigned_candidate_and_no_relay() -> None:
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
    for forbidden in (
        "relay-capture",
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
