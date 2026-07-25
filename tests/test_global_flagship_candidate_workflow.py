from __future__ import annotations

import re
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "global-flagship-candidate.yml"
SCRIPT = (
    ROOT / "scripts" / "release" / "produce_global_flagship_candidate.py"
)


def workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def test_candidate_workflow_is_protected_main_only_and_least_privilege() -> None:
    text = workflow_text()
    loaded = yaml.safe_load(text)
    assert loaded["permissions"] == {"actions": "read", "contents": "read"}
    assert "environment: global-flagship-candidate-production" in text
    assert 'test "$GITHUB_REF" = "refs/heads/main"' in text
    assert 'test "$GITHUB_RUN_ATTEMPT" = "1"' in text
    assert 'test "$GITHUB_ACTOR" = "$GITHUB_TRIGGERING_ACTOR"' in text
    assert "actions: write" not in text
    for forbidden in (
        "contents: write",
        "packages: write",
        "id-token: write",
        "deploy-pages",
        "createRelease",
        "uploadReleaseAsset",
        "gh release",
        "publish-download-bundle",
    ):
        assert forbidden not in text


def test_candidate_workflow_requires_all_six_exact_artifact_identities() -> None:
    text = workflow_text()
    for role in (
        "windows_export",
        "windows_capture",
        "windows_evidence",
        "linux_export",
        "linux_evidence",
        "macos_escrow",
        "macos_handoff",
    ):
        for suffix in ("artifact_id", "artifact_name", "artifact_digest"):
            assert f"{role}_{suffix}:" in text
            assert f"--{role.replace('_', '-')}-{suffix.replace('_', '-')}" in text
    assert text.count("description: Exact sha256 digest") == 7
    assert "--assembly-confirmed true" in text


def test_candidate_workflow_has_only_escrow_opening_secrets() -> None:
    text = workflow_text()
    secret_references = re.findall(
        r"\$\{\{\s*secrets\.([A-Za-z0-9_]+)\s*\}\}", text
    )
    assert secret_references == [
        "CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PEM",
        "CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PASSPHRASE",
    ]
    assert 'printf \'%s\' "$MACOS_ESCROW_PRIVATE_KEY" >"$private_key"' in text
    assert 'rm -f -- "$private_key"' in text
    assert 'test ! -e "$private_key"' in text
    assert "include-hidden-files: false" in text


def test_candidate_workflow_uploads_one_complete_immutable_handoff() -> None:
    text = workflow_text()
    assert "Upload only the exact complete candidate and proposal" in text
    assert (
        "name: global-flagship-candidate-${{ github.run_id }}-1" in text
    )
    assert (
        "path: ${{ runner.temp }}/global-flagship-candidate-output" in text
    )
    assert "GLOBAL_FLAGSHIP_CANDIDATE.generated.json" in text
    assert "GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json" in text
    assert "GLOBAL_FLAGSHIP_PROVIDER_REAUTHENTICATION.generated.json" in text
    assert "overwrite: false" in text


def test_candidate_workflow_actions_are_commit_pinned() -> None:
    for line in workflow_text().splitlines():
        action = line.strip()
        if action.startswith("uses:"):
            assert re.fullmatch(
                r"uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}",
                action,
            )
    assert SCRIPT.is_file()
