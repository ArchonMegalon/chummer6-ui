from __future__ import annotations

import os
import shutil
import subprocess
from pathlib import Path

import pytest
import yaml


ROOT = Path(__file__).resolve().parents[1]
CAPTURE = (
    ROOT
    / ".github"
    / "workflows"
    / "unsigned-windows-preview-native-evidence-capture.yml"
)
FINALIZE = (
    ROOT
    / ".github"
    / "workflows"
    / "unsigned-windows-preview-native-evidence-finalize.yml"
)
GENERATOR = ROOT / "scripts" / "unsigned_windows_preview_native_evidence.py"
STARTUP = (
    ROOT / "scripts" / "capture_unsigned_windows_preview_startup_visual.ps1"
)
AUTHENTICODE = (
    ROOT
    / "scripts"
    / "verify_unsigned_windows_preview_authenticode.ps1"
)
INSTALLER_VISUAL = ROOT / "scripts" / "capture_windows_installer_visual.ps1"
PRODUCER = (
    ROOT
    / ".github"
    / "workflows"
    / "unsigned-windows-preview-nightly-candidate-export.yml"
)


def workflow(path: Path) -> dict[str, object]:
    payload = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
    assert isinstance(payload, dict)
    return payload


def test_capture_is_read_only_hosted_windows_evidence_lane() -> None:
    payload = workflow(CAPTURE)
    assert payload["permissions"] == {"actions": "read", "contents": "read"}
    job = payload["jobs"]["capture"]
    assert job["runs-on"] == "windows-latest"
    assert job["environment"] == (
        "unsigned-windows-preview-native-capture"
    )
    source = CAPTURE.read_text(encoding="utf-8")
    assert "process.env.GITHUB_ACTOR !== 'github-actions[bot]'" in source
    assert (
        ".github/workflows/"
        "unsigned-windows-preview-nightly-candidate-export.yml"
    ) in source
    assert "run.data.head_sha !== process.env.CANDIDATE_SHA" in source
    assert (
        "process.env.GITHUB_SHA !== process.env.EXPECTED_CONTRACT_SHA"
        in source
    )
    assert "process.env.GITHUB_SHA !== process.env.CANDIDATE_SHA" not in source
    assert "runs-on: windows-latest" in source
    assert "verify_unsigned_windows_preview_authenticode.ps1" in source
    assert "capture_unsigned_windows_preview_startup_visual.ps1" in source
    assert "windows-application-avalonia-win-x64-startup.png" in source
    assert "windows-installer-avalonia-win-x64-progress.png" in source
    assert "windows-installer-avalonia-win-x64-completion.png" in source
    assert "retention-days: 14" in source
    assert "compression-level: 0" in source
    assert "persist-credentials: false" in source


def test_bot_only_capture_has_one_scoped_in_repo_relay() -> None:
    producer = workflow(PRODUCER)
    relay = producer["jobs"]["relay-capture"]
    assert relay["needs"] == ["preflight", "export"]
    assert relay["permissions"] == {
        "actions": "write",
        "contents": "read",
    }
    assert relay["runs-on"] == "ubuntu-24.04"
    script = relay["steps"][1]["with"]["script"]
    assert script.count("createWorkflowDispatch") == 1
    assert (
        "'unsigned-windows-preview-native-evidence-capture.yml'"
        in script
    )
    assert "candidate_sha: sourceSha" in script
    assert "expected_contract_sha: contractSha" in script
    assert "const contractSha = exact(" in script
    assert "ref: 'heads/main'" in script
    assert "ref: 'main'" in script
    capture = CAPTURE.read_text(encoding="utf-8")
    assert "process.env.GITHUB_ACTOR !== 'github-actions[bot]'" in capture
    assert "process.env.CANDIDATE_SHA" in capture
    assert "process.env.EXPECTED_CONTRACT_SHA" in capture
    assert "attempt < 12" in capture
    assert "setTimeout(resolve, 5000)" in capture
    assert "run.data.status === 'completed'" in capture


def test_finalization_is_sole_accountable_review_without_release_authority() -> None:
    payload = workflow(FINALIZE)
    assert payload["permissions"] == {"actions": "read", "contents": "read"}
    job = payload["jobs"]["finalize"]
    assert job["runs-on"] == "ubuntu-latest"
    assert job["environment"] == "unsigned-windows-preview-native-review"
    inputs = payload["on"]["workflow_dispatch"]["inputs"]
    assert "accountable_review_confirmed" in inputs
    assert "review_json" in inputs
    assert "human_review_confirmed" not in inputs
    source = FINALIZE.read_text(encoding="utf-8")
    assert "GITHUB_ACTOR !== 'ArchonMegalon'" in source
    assert "GITHUB_TRIGGERING_ACTOR !== 'ArchonMegalon'" in source
    assert "run.data.actor.login !== 'github-actions[bot]'" in source
    assert (
        "process.env.GITHUB_SHA !== process.env.CAPTURE_SHA"
        in source
    )
    assert "--expected-capture-actor 'github-actions[bot]'" in source
    assert (
        "--reviewer-kind "
        "'authenticated_account_owner_delegated_operator'"
    ) in source
    assert "test \"$(printf '%s\\n' \"$bindings\" | wc -l)\" -eq 4" in source
    assert "native_evidence_sha256" in source
    assert "Publication/upload/deployment authority: false" in source


def test_new_lane_uses_only_pinned_first_party_artifact_actions() -> None:
    for path in (CAPTURE, FINALIZE):
        source = path.read_text(encoding="utf-8")
        uses = [
            line.strip().removeprefix("uses: ")
            for line in source.splitlines()
            if line.strip().startswith("uses: ")
        ]
        assert uses
        for action in uses:
            assert "@" in action
            revision = action.rsplit("@", 1)[1]
            assert len(revision) == 40
            assert all(character in "0123456789abcdef" for character in revision)
        assert not any(
            token in source
            for token in (
                "permissions: write",
                "actions: write",
                "contents: write",
                "deployments: write",
                "id-token: write",
                "packages: write",
                "pull-requests: write",
                "releases: write",
            )
        )


def test_unsigned_verifiers_require_native_windows_and_exact_bot_source() -> None:
    startup = STARTUP.read_text(encoding="utf-8")
    authenticode = AUTHENTICODE.read_text(encoding="utf-8")
    for source in (startup, authenticode):
        assert "[PlatformID]::Win32NT" in source
        assert "$env:WINELOADERNOEXEC" in source
        assert "$env:WINEPREFIX" in source
        assert "github-actions[bot]" in source
        assert "refs/heads/main" in source
        assert (
            ".github/workflows/"
            "unsigned-windows-preview-native-evidence-capture.yml"
        ) in source
    assert "Get-AuthenticodeSignature" in authenticode
    assert "SignatureStatus]::NotSigned" in authenticode
    assert "securityDirectoryEmpty = $true" in authenticode
    assert "preview_policy" in authenticode
    assert "SetForegroundWindow" in startup
    assert "CopyFromScreen" in startup
    assert "Chummer.Avalonia.exe" in startup


@pytest.mark.skipif(
    shutil.which("pwsh") is None,
    reason="PowerShell is unavailable on this host",
)
def test_native_capture_powershell_scripts_parse() -> None:
    parser = r"""
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    $env:CHUMMER_POWERSHELL_PARSE_PATH,
    [ref]$tokens,
    [ref]$errors
) | Out-Null
if ($errors.Count -ne 0) {
    $errors | ForEach-Object { Write-Error $_.Message }
    exit 1
}
"""
    for script in (STARTUP, AUTHENTICODE, INSTALLER_VISUAL):
        environment = os.environ.copy()
        environment["CHUMMER_POWERSHELL_PARSE_PATH"] = str(script)
        result = subprocess.run(
            [
                "pwsh",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                parser,
            ],
            check=False,
            capture_output=True,
            env=environment,
            text=True,
        )
        assert result.returncode == 0, (
            f"{script} failed PowerShell parsing:\n"
            f"{result.stdout}\n{result.stderr}"
        )


def test_unsigned_lane_never_claims_human_or_publication_authority() -> None:
    sources = [
        path.read_text(encoding="utf-8")
        for path in (
            CAPTURE,
            FINALIZE,
            GENERATOR,
            STARTUP,
            AUTHENTICODE,
        )
    ]
    combined = "\n".join(sources).lower()
    assert "human_review_confirmed" not in combined
    assert "humanreviewconfirmed" not in combined
    assert "accountable_review_confirmed" in combined
    assert "accountablereviewconfirmed" in combined
    assert '"publicationauthorized": true' not in combined
    assert '"deployauthorized": true' not in combined
    assert '"uploadauthorized": true' not in combined
    assert "gh workflow run" not in combined
    assert "createworkflowdispatch" not in combined
    assert "deployment-url" not in combined
