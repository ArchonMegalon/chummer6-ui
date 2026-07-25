from __future__ import annotations

import ast
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-protected-publication.yml"
)
ASSEMBLY_WORKFLOW = (
    ROOT
    / ".github"
    / "workflows"
    / "global-flagship-publication-input-assembly.yml"
)
ASSEMBLY_SCRIPT = (
    ROOT
    / "scripts"
    / "release"
    / "assemble_global_flagship_publication_input.py"
)
SCRIPT = (
    ROOT
    / "scripts"
    / "release"
    / "publish_global_flagship_release.py"
)
CANONICAL_PUBLISHER = ROOT / "scripts" / "publish-download-bundle-http.sh"
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
        "--publication-input-artifact-name",
        "--publication-input-artifact-digest",
        "--hub-topology-artifact-digest",
        "--journal \"$journal_root\"",
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
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    assert "actions/download-artifact" not in workflow
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
    assert secret_references == [
        "CHUMMER_FLAGSHIP_HUB_ACTIONS_READ_TOKEN",
        "CHUMMER_FLAGSHIP_PUBLICATION_TOKEN",
    ]
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
        "receipt = build_publication_receipt"
    )


def test_publication_token_is_removed_from_both_process_environments() -> None:
    script = SCRIPT.read_text(encoding="utf-8")
    publisher = CANONICAL_PUBLISHER.read_text(encoding="utf-8")
    assert 'token = os.environ.pop(args.publication_token_env, "")' in script
    assert script.index(
        'token = os.environ.pop(args.publication_token_env, "")'
    ) < script.index("github_token = os.environ.get(args.github_token_env")
    publisher_class = script[script.index("class CanonicalHttpPublisher") :]
    assert publisher_class.index("self._token = \"\"") < publisher_class.index(
        "completed = subprocess.run("
    )
    assert 'TOKEN="${CHUMMER_RELEASE_UPLOAD_TOKEN:-}"' in publisher
    assert publisher.index(
        "unset CHUMMER_RELEASE_UPLOAD_TOKEN"
    ) < publisher.index('TOKEN_FILE="${CHUMMER_RELEASE_UPLOAD_TOKEN_FILE')


def test_assembly_is_a_separate_protected_read_only_causal_lane() -> None:
    workflow = ASSEMBLY_WORKFLOW.read_text(encoding="utf-8")
    assert "workflow_dispatch:" in workflow
    assert (
        "environment: global-flagship-publication-input-assembly"
        in workflow
    )
    assert "permissions:\n  actions: read\n  contents: read" in workflow
    assert "CHUMMER_FLAGSHIP_PUBLICATION_TOKEN" not in workflow
    assert "actions/download-artifact" not in workflow
    assert re.findall(
        r"^\s*uses:\s*(\S+)\s*$", workflow, flags=re.MULTILINE
    ) == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ]
    for required in (
        "candidate_payload_artifact_id:",
        "candidate_payload_artifact_name:",
        "candidate_payload_artifact_digest:",
        "provider_handoff_artifact_id:",
        "provider_handoff_artifact_name:",
        "provider_handoff_artifact_digest:",
        "hub_topology_artifact_id:",
        "hub_topology_artifact_name:",
        "hub_topology_artifact_digest:",
        "global-flagship-publication-input-${{ inputs.candidate_id }}-"
        "${{ github.run_id }}-${{ github.run_attempt }}",
        "prevent_self_review !== true",
        "can_admins_bypass !== false",
        'test "$GITHUB_RUN_ATTEMPT" = "1"',
    ):
        assert required in workflow


def test_assembly_directly_hashes_archives_and_binds_complete_receipt() -> None:
    script = ASSEMBLY_SCRIPT.read_text(encoding="utf-8")
    ast.parse(script)
    for required in (
        "download_authenticated_artifact",
        "archiveSha256",
        '"candidatePayload": candidate_authority',
        '"providerHandoff": handoff_authority',
        '"approvals": approval_authorities',
        '"hubTopology": hub_authority',
        '"destinationPlan": publication.binding_bytes',
        '"manifests": manifests',
        '"platforms": platform_bindings',
        '"inventory": publication.publication_input_inventory(output_root)',
        '"trustedAsAuthority": False',
        '"publicationAuthorized": False',
    ):
        assert required in script


def test_protected_publication_contracts_run_in_pull_request_ci() -> None:
    ci = CI.read_text(encoding="utf-8")
    assert ci.count("tests/test_global_flagship_protected_publication.py") == 1
    assert (
        ci.count(
            "tests/test_global_flagship_protected_publication_workflow.py"
        )
        == 1
    )
