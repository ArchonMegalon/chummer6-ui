from __future__ import annotations

import os
import re
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
RETRY = (
    ROOT
    / ".github"
    / "workflows"
    / "unsigned-windows-preview-native-evidence-retry.yml"
)


def assert_installer_visual_window_contract(source: str) -> None:
    def required_index(needle: str) -> int:
        assert needle in source
        return source.index(needle)

    minimum_width = re.search(
        r"^\$MinimumReviewWidth = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    minimum_height = re.search(
        r"^\$MinimumReviewHeight = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    stable_observations = re.search(
        r"^\$RequiredStableObservationCount = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    poll_milliseconds = re.search(
        r"^\$WindowObservationPollMilliseconds = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    assert minimum_width is not None
    assert minimum_height is not None
    assert stable_observations is not None
    assert poll_milliseconds is not None
    assert int(minimum_width.group(1)) == 320
    assert int(minimum_height.group(1)) == 200
    assert int(stable_observations.group(1)) >= 2
    assert int(poll_milliseconds.group(1)) >= 50

    assert source.count(
        "[ChummerNativeWindowCapture]::IsWindowVisible("
    ) >= 3
    assert source.count(
        "[ChummerNativeWindowCapture]::GetWindowThreadProcessId("
    ) >= 2
    assert source.count(
        "[ChummerNativeWindowCapture]::IsIconic("
    ) >= 3
    assert (
        "$foregroundWindowHandle = "
        "[ChummerNativeWindowCapture]::GetForegroundWindow()"
    ) in source
    assert "$current.BelongsToInstallerProcess -and" in source
    assert "-not $current.IsMinimized -and" in source
    assert (
        "} catch [System.InvalidOperationException] {\n"
        "        return $null\n"
        "    }"
    ) in source
    assert "\n        $latestObservation = $current\n" in source
    for comparison in (
        "$current.HandleValue -eq $stableObservation.HandleValue",
        "$current.Left -eq $stableObservation.Left",
        "$current.Top -eq $stableObservation.Top",
        "$current.Right -eq $stableObservation.Right",
        "$current.Bottom -eq $stableObservation.Bottom",
    ):
        assert comparison in source
    assert (
        "$stableObservationCount -ge $RequiredStableObservationCount"
        in source
    )
    assert (
        "} else {\n"
        "            $stableObservation = $null\n"
        "            $stableObservationCount = 0\n"
        "        }"
    ) in source
    assert (
        "$width -lt $MinimumReviewWidth -or "
        "$height -lt $MinimumReviewHeight"
    ) in source
    assert (
        "$captureWindowStillVisible = (\n"
        "        $currentMainWindowHandle -eq $WindowHandle -and\n"
        "        $foregroundWindowHandle -eq $WindowHandle -and\n"
        "        [ChummerNativeWindowCapture]::IsWindow($WindowHandle) -and\n"
        "        [ChummerNativeWindowCapture]::IsWindowVisible($WindowHandle) -and\n"
        "        -not [ChummerNativeWindowCapture]::IsIconic($WindowHandle)\n"
        "    )"
    ) in source
    assert (
        "$windowOwnerProcessId -ne "
        "[uint32]$script:installerProcessId"
    ) in source
    for comparison in (
        "$rect.Left -eq $WindowObservation.Left",
        "$rect.Top -eq $WindowObservation.Top",
        "$rect.Right -eq $WindowObservation.Right",
        "$rect.Bottom -eq $WindowObservation.Bottom",
    ):
        assert comparison in source
    assert "latest observation $latest" in source
    assert "last nonzero observation $lastNonZero" in source
    assert "handle=$handleText width=$width height=$height" in source

    process_start = required_index(
        "$script:installerProcess = Start-Process"
    )
    progress_marker = required_index(
        'Wait-TraceMarker -Marker "Extracting application files"'
    )
    progress_window = required_index(
        '$progressWindow = Wait-ReviewableMainWindow -Phase "progress"'
    )
    progress_capture = required_index(
        "Save-WindowPng -WindowObservation $progressWindow"
    )
    completion_marker = required_index(
        'Wait-TraceMarker -Marker "Install complete"'
    )
    completion_window = required_index(
        '$completionWindow = Wait-ReviewableMainWindow -Phase "completion"'
    )
    completion_capture = required_index(
        "Save-WindowPng -WindowObservation $completionWindow"
    )
    assert (
        process_start
        < progress_marker
        < progress_window
        < progress_capture
        < completion_marker
        < completion_window
        < completion_capture
    )
    assert "$window = Wait-MainWindow" not in source
    assert (
        "Save-WindowPng -WindowHandle "
        "$script:installerProcess.MainWindowHandle"
    ) not in source


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
    progress_binding = (
        "$progressSource = Join-Path $env:TEMP "
        "'Chummer6\\installer-temp\\chummer-desktop-installer-progress.log'"
    )
    trace_binding = (
        "$env:CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH = "
        "$progressSource"
    )
    smoke_call = "& bash scripts/run-desktop-startup-smoke.sh"
    assert source.count(progress_binding) == 1
    assert source.count(trace_binding) == 1
    assert source.index(progress_binding) < source.index(trace_binding)
    assert source.index(trace_binding) < source.index(smoke_call)
    assert source.index(smoke_call) < source.index(
        "Test-Path -LiteralPath $progressSource -PathType Leaf"
    )
    assert "retention-days: 14" in source
    assert "compression-level: 0" in source
    assert "persist-credentials: false" in source


def test_capture_failure_upload_contains_only_sanitized_non_authoritative_diagnostics() -> None:
    payload = workflow(CAPTURE)
    steps = payload["jobs"]["capture"]["steps"]
    diagnostic = next(
        step
        for step in steps
        if step["name"] == "Upload failure-only sanitized startup diagnostics"
    )
    assert diagnostic["if"] == "failure()"
    assert diagnostic["uses"] == (
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02"
    )
    assert diagnostic["with"]["name"] == (
        "unsigned-windows-preview-native-diagnostics-"
        "${{ github.run_id }}-${{ github.run_attempt }}"
    )
    paths = diagnostic["with"]["path"]
    assert "release-regression-*.json" in paths
    assert "startup-smoke-*.receipt.json" in paths
    assert "startup-smoke-*.log" not in paths
    assert diagnostic["with"]["if-no-files-found"] == "warn"
    assert diagnostic["with"]["overwrite"] == "false"


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


def test_exact_candidate_retry_is_current_main_bound_and_failure_authenticated() -> None:
    payload = workflow(RETRY)
    assert payload["permissions"] == {}
    job = payload["jobs"]["relay"]
    assert job["runs-on"] == "ubuntu-24.04"
    assert job["timeout-minutes"] == "5"
    assert job["permissions"] == {
        "actions": "write",
        "contents": "read",
    }
    assert "environment" not in job
    job_if = job["if"]
    for required_guard in (
        "inputs.retry_confirmed == true",
        "github.event_name == 'workflow_dispatch'",
        "github.ref == 'refs/heads/main'",
        "github.run_attempt == 1",
        "github.repository == 'ArchonMegalon/chummer6-ui'",
        "github.actor == 'ArchonMegalon'",
        "github.triggering_actor == github.actor",
        "github.actor_id == '11421547'",
    ):
        assert required_guard in job_if
    steps = job["steps"]
    assert [step["uses"] for step in steps if "uses" in step] == [
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea",
    ]
    relay = steps[1]
    script = relay["with"]["script"]
    assert script.count("createWorkflowDispatch") == 1
    for required in (
        "process.env.GITHUB_REF !== 'refs/heads/main'",
        "process.env.GITHUB_EVENT_NAME !== 'workflow_dispatch'",
        "process.env.GITHUB_RUN_ATTEMPT !== '1'",
        "process.env.GITHUB_REPOSITORY !== 'ArchonMegalon/chummer6-ui'",
        "process.env.GITHUB_ACTOR !== 'ArchonMegalon'",
        "process.env.GITHUB_TRIGGERING_ACTOR !== process.env.GITHUB_ACTOR",
        "process.env.GITHUB_ACTOR_ID !== process.env.EXPECTED_OPERATOR_ID",
        "EXPECTED_OPERATOR_ID: \"11421547\"",
        "main.data.object.sha !== process.env.GITHUB_SHA",
        "repos.getContent",
        "EXPECTED_CANDIDATE_RUN_ID: \"30227076806\"",
        "EXPECTED_CANDIDATE_ARTIFACT_ID: \"8638853025\"",
        "dd7ba41159729ad848db9a5268e06bc75833b63d96791233d91a006ad062044e",
        "EXPECTED_FAILED_CAPTURE_RUN_ID: \"30227119219\"",
        "failedCapture.data.conclusion !== 'failure'",
        "Capture native startup and installer visuals', 'failure'",
        "Revalidate exact unsigned candidate bytes', 'success'",
        "failedArtifacts.length !== 0",
        "workflow_id: 'unsigned-windows-preview-native-evidence-capture.yml'",
        "expected_contract_sha: process.env.GITHUB_SHA",
        "capture_confirmed: true",
        "ref: 'main'",
    ):
        assert required in RETRY.read_text(encoding="utf-8")
    assert script.index("process.env.GITHUB_EVENT_NAME") < script.index(
        "require('./scripts/github_workflow_run_path.js')"
    )
    for required_input in (
        "candidate_run_id: '30227076806'",
        "candidate_run_attempt: '1'",
        "candidate_sha: 'f1c6eee839ab2de74e877bf5976b9800204799fa'",
        "candidate_artifact_id: '8638853025'",
        "candidate_version: 'run-20260727-000514'",
        "candidate_manifest_sha256: '22e301b57e9fe40003f681302501d037ba94e02bb529aa5acb29e9095ebffdd4'",
        "candidate_inventory_sha256: '421d44f31017b2a8fc75d62817738318c4ba0879eda123b1f2b9bb8718192733'",
    ):
        assert required_input in script
    lowered = RETRY.read_text(encoding="utf-8").lower()
    for forbidden in (
        "secrets.",
        "createdeployment",
        "createrelease",
        "createorupdaterelease",
        "uploadreleaseasset",
        "packages: write",
        "contents: write",
        "id-token: write",
        "publicationauthorized: true",
        "uploadauthorized: true",
        "deployauthorized: true",
    ):
        assert forbidden not in lowered


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


def test_installer_visual_reacquires_stable_reviewable_window_after_each_marker() -> None:
    assert_installer_visual_window_contract(
        INSTALLER_VISUAL.read_text(encoding="utf-8")
    )


@pytest.mark.parametrize(
    ("needle", "replacement"),
    (
        ("$MinimumReviewWidth = 320", "$MinimumReviewWidth = 319"),
        ("$MinimumReviewHeight = 200", "$MinimumReviewHeight = 199"),
        (
            "$RequiredStableObservationCount = 3",
            "$RequiredStableObservationCount = 1",
        ),
        (
            "$WindowObservationPollMilliseconds = 100",
            "$WindowObservationPollMilliseconds = 0",
        ),
        (
            "[ChummerNativeWindowCapture]::IsWindowVisible($windowHandle)",
            "$true",
        ),
        (
            "[ChummerNativeWindowCapture]::IsIconic($windowHandle)",
            "$false",
        ),
        (
            "$current.HandleValue -eq $stableObservation.HandleValue",
            "$true",
        ),
        (
            "} catch [System.InvalidOperationException] {\n"
            "        return $null\n"
            "    }",
            "} catch [System.InvalidOperationException] {\n"
            "        throw\n"
            "    }",
        ),
        (
            "\n        $latestObservation = $current\n",
            (
                "\n        if ($null -ne $current) { "
                "$latestObservation = $current }\n"
            ),
        ),
        (
            "} else {\n"
            "            $stableObservation = $null\n"
            "            $stableObservationCount = 0\n"
            "        }",
            "} else {\n"
            "            $stableObservationCount += 1\n"
            "        }",
        ),
        (
            '$progressWindow = Wait-ReviewableMainWindow -Phase "progress"',
            "$progressWindow = Get-MainWindowObservation",
        ),
        (
            '$completionWindow = Wait-ReviewableMainWindow -Phase "completion"',
            "$completionWindow = Get-MainWindowObservation",
        ),
        (
            "$width -lt $MinimumReviewWidth -or "
            "$height -lt $MinimumReviewHeight",
            "$false",
        ),
        (
            "$captureWindowStillVisible = (\n"
            "        $currentMainWindowHandle -eq $WindowHandle -and\n"
            "        $foregroundWindowHandle -eq $WindowHandle -and\n"
            "        [ChummerNativeWindowCapture]::IsWindow($WindowHandle) -and\n"
            "        [ChummerNativeWindowCapture]::IsWindowVisible($WindowHandle) -and\n"
            "        -not [ChummerNativeWindowCapture]::IsIconic($WindowHandle)\n"
            "    )",
            "$captureWindowStillVisible = $true",
        ),
        (
            "$windowOwnerProcessId -ne "
            "[uint32]$script:installerProcessId",
            "$false",
        ),
        (
            "$rect.Left -eq $WindowObservation.Left",
            "$true",
        ),
    ),
    ids=(
        "lower-width-gate",
        "lower-height-gate",
        "single-observation",
        "zero-poll-delay",
        "hidden-window",
        "minimized-window",
        "handle-transition",
        "process-exit-race",
        "missing-window-does-not-replace-latest",
        "invalid-window-does-not-reset",
        "one-shot-progress-window",
        "one-shot-completion-window",
        "skip-final-size-check",
        "window-hides-during-focus-delay",
        "window-owner-changes",
        "window-bounds-change-after-focus",
    ),
)
def test_installer_visual_window_contract_rejects_unsafe_mutations(
    needle: str,
    replacement: str,
) -> None:
    source = INSTALLER_VISUAL.read_text(encoding="utf-8")
    assert needle in source
    mutated = source.replace(needle, replacement, 1)
    with pytest.raises(AssertionError):
        assert_installer_visual_window_contract(mutated)


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
