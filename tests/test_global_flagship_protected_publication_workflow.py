from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-protected-publication.yml"
)
SCRIPT = (
    ROOT
    / "scripts"
    / "release"
    / "publish_global_flagship_release.py"
)
CI = ROOT / ".github" / "workflows" / "pull-request-ci.yml"


def text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def test_workflow_is_manual_protected_fresh_and_operator_confirmed() -> None:
    workflow = text()
    assert "workflow_dispatch:" in workflow
    assert "environment: global-flagship-protected-publication" in workflow
    assert "permissions:\n  actions: read\n  contents: read" in workflow
    assert "pull_request:" not in workflow
    assert "\npush:" not in workflow
    for required in (
        "publication_confirmation:",
        "Type PUBLISH:<exact-proposal-sha256>",
        'test "$GITHUB_REF" = "refs/heads/main"',
        'test "$GITHUB_RUN_ATTEMPT" = "1"',
        'test "$GITHUB_ACTOR" = "$GITHUB_TRIGGERING_ACTOR"',
        '--confirmation "$OPERATOR_CONFIRMATION"',
        '--source-sha "$GITHUB_SHA"',
        "--provider-handoff-artifact-digest",
        "--publication-input-artifact-digest",
    ):
        assert required in workflow


def test_workflow_pins_actions_and_uploads_only_post_verification_receipt() -> None:
    workflow = text()
    assert re.findall(
        r"^\s*uses:\s*(\S+)\s*$", workflow, flags=re.MULTILINE
    ) == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
        "actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093",
        "actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    assert (
        "path: ${{ runner.temp }}/global-flagship-publication/receipt/"
        "publication-receipt.json"
    ) in workflow
    assert "overwrite: false" in workflow
    assert 'stat -c \'%a\' "$receipt_root/publication-receipt.json"' in workflow


def test_publication_authority_is_isolated_from_other_release_authorities() -> None:
    workflow = text()
    secret_references = re.findall(
        r"\$\{\{\s*secrets\.([A-Za-z0-9_]+)\s*\}\}", workflow
    )
    assert secret_references == ["CHUMMER_FLAGSHIP_PUBLICATION_TOKEN"]
    for forbidden in (
        "CHUMMER_FLAGSHIP_ADMIN_READ_TOKEN",
        "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64",
        "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64",
        "CHUMMER_KEYLOCKER_API_KEY",
        "contents: write",
        "packages: write",
        "id-token: write",
    ):
        assert forbidden not in workflow
    for required in (
        "can_admins_bypass !== false",
        "prevent_self_review !== true",
        "publication environment branch policy must contain exactly main",
        "branch.data.protected !== true",
        "branch.data.commit.sha !== context.sha",
    ):
        assert required in workflow


def test_transaction_uses_get_only_readback_and_canonical_publisher() -> None:
    script = SCRIPT.read_text(encoding="utf-8")
    assert 'method="GET"' in script
    for forbidden_method in ('method="POST"', 'method="PUT"', 'method="PATCH"', 'method="DELETE"'):
        assert forbidden_method not in script
    assert 'CANONICAL_PUBLISHER = "scripts/publish-download-bundle-http.sh"' in script
    assert '["bash", str(publisher), str(bundle)]' in script
    assert '"publicationAuthorized": True' in script
    assert script.index("verified = verify_destinations") < script.index(
        '"publicationAuthorized": True'
    )


def test_protected_publication_contracts_run_in_pull_request_ci() -> None:
    ci = CI.read_text(encoding="utf-8")
    assert ci.count("tests/test_global_flagship_protected_publication.py") == 1
    assert (
        ci.count(
            "tests/test_global_flagship_protected_publication_workflow.py"
        )
        == 1
    )
