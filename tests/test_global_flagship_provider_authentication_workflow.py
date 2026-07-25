from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-provider-authentication.yml"
)
APPROVAL_WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-release-approval.yml"
)
SCRIPT = (
    ROOT
    / "scripts"
    / "release"
    / "authenticate_global_flagship_release.py"
)
DOC = ROOT / "docs" / "GLOBAL_FLAGSHIP_PROVIDER_AUTHENTICATION.md"


def workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def test_provider_workflow_is_manual_protected_and_read_only() -> None:
    text = workflow_text()
    assert "workflow_dispatch:" in text
    assert (
        "environment: global-flagship-provider-authentication" in text
    )
    assert "permissions:\n  actions: read\n  contents: read" in text
    assert "contents: write" not in text
    assert "actions: write" not in text
    assert "id-token:" not in text
    assert "pull_request:" not in text
    assert "\npush:" not in text
    for required in (
        "input_artifact_id:",
        "input_artifact_digest:",
        "authentication_confirmed:",
        'test "$GITHUB_REF" = "refs/heads/main"',
        'test "$GITHUB_RUN_ATTEMPT" = "1"',
        'test "$GITHUB_ACTOR" = "$GITHUB_TRIGGERING_ACTOR"',
        "--expected-input-artifact-digest",
        '--expected-verifier-source-sha "$GITHUB_SHA"',
    ):
        assert required in text


def test_administration_token_is_isolated_from_approval_lane() -> None:
    provider = workflow_text()
    approval = APPROVAL_WORKFLOW.read_text(encoding="utf-8")
    secret_name = "CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN"
    assert f"${{{{ secrets.{secret_name} }}}}" in provider
    assert secret_name not in approval
    assert "global-flagship-provider-authentication" not in approval
    assert "${{ secrets." not in approval


def test_provider_workflow_pins_actions_and_uploads_only_handoff() -> None:
    text = workflow_text()
    uses = re.findall(r"^\s*uses:\s*(\S+)\s*$", text, flags=re.MULTILINE)
    assert uses == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    assert (
        "path: ${{ runner.temp }}/global-flagship-provider-authentication/"
        "handoff.json"
    ) in text
    assert "compression-level: 0" in text
    assert "overwrite: false" in text
    assert 'stat -c \'%a\' "$handoff_root/handoff.json"' in text


def test_verifier_network_surface_is_get_only_and_nonpublishing() -> None:
    text = SCRIPT.read_text(encoding="utf-8")
    assert 'method="GET"' in text
    assert 'method="POST"' not in text
    assert 'method="PUT"' not in text
    assert 'method="PATCH"' not in text
    assert 'method="DELETE"' not in text
    assert "publicationAuthorized" in text
    assert "releaseArtifactBytesAuthenticated" in text
    assert "provenanceAuthenticated" in text
    assert "subprocess" not in text


def test_operator_documentation_states_exact_trust_boundary() -> None:
    text = " ".join(DOC.read_text(encoding="utf-8").split())
    for required in (
        "Administration: read",
        "releaseArtifactBytesAuthenticated: false",
        "publicationAuthorized: false",
        "untrusted transport",
        "artifact-id",
        "artifact-digest",
        "entire run-review-history log containing exactly one record",
        "explicit positive GitHub App ID",
        "final reauthentication of every approval",
        "temporary `302`",
        "separate protected publication transaction",
    ):
        assert required in text
