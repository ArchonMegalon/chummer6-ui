from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-release-approval.yml"
)
REVIEWER_POLICY = ROOT / ".github" / "global-flagship-reviewer-policy.json"


def workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def test_approval_workflow_is_manual_protected_and_nonpublishing() -> None:
    text = workflow_text()
    assert "workflow_dispatch:" in text
    assert "environment: global-flagship-release-review" in text
    assert "permissions:\n  actions: read\n  contents: read" in text
    assert "contents: write" not in text
    assert "actions: write" not in text
    assert "id-token:" not in text
    assert "${{ secrets." not in text
    assert "pull_request:" not in text
    assert "push:" not in text


def test_approval_workflow_binds_exact_main_proposal_and_same_actor() -> None:
    text = workflow_text()
    for required in (
        "proposal_json_base64:",
        "proposal_sha256:",
        "approval_role:",
        "approval_confirmed:",
        "needs: trust-root",
        'test "$GITHUB_REF" = "refs/heads/main"',
        'test "$GITHUB_ACTOR" = "$GITHUB_TRIGGERING_ACTOR"',
        'test "$GITHUB_RUN_ATTEMPT" = "1"',
        '--workflow-ref "$GITHUB_WORKFLOW_REF"',
        '--workflow-sha "$GITHUB_WORKFLOW_SHA"',
        '--expected-proposal-sha256 "$EXPECTED_PROPOSAL_SHA256"',
        "len(encoded) > 60_000",
        "len(proposal) > 45_000",
        "Verify protected review trust root",
        "branch.data.protected !== true",
        "branch.data.commit.sha !== context.sha",
        "statusChecks.enforcement_level !== 'everyone'",
        "actions/runs/{run_id}/approvals",
        "--environment-approver",
        "role allowlists must exactly equal the protected environment human reviewers",
    ):
        assert required in text


def test_approval_workflow_uses_source_bound_role_policy() -> None:
    text = workflow_text()
    assert ".github/global-flagship-reviewer-policy.json" in text
    assert "${{ vars." not in text
    policy = json.loads(REVIEWER_POLICY.read_text(encoding="utf-8"))
    assert policy["contractName"] == (
        "chummer6-ui.global-flagship-release-reviewer-policy.v1"
    )
    assert policy["contractVersion"] == 1
    assert set(policy["roles"]) == {"quality", "release", "security"}
    assert all(isinstance(value, list) for value in policy["roles"].values())


def test_approval_workflow_uploads_only_the_sealed_receipt() -> None:
    text = workflow_text()
    assert "chmod 0444 \"$APPROVAL_RECEIPT\"" in text
    assert (
        "path: ${{ runner.temp }}/global-flagship-release-approval/approval.json"
        in text
    )
    assert (
        "global-flagship-release-approval-${{ inputs.approval_role }}-"
        "${{ github.run_id }}-${{ github.run_attempt }}"
    ) in text
    uses = re.findall(r"^\s*uses:\s*(\S+)\s*$", text, flags=re.MULTILINE)
    assert uses == [
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
