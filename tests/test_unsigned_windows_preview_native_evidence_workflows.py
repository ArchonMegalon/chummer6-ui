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
    trace_poll_milliseconds = re.search(
        r"^\$TraceObservationPollMilliseconds = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    freeze_timeout_seconds = re.search(
        r"^\$ProgressFreezeTimeoutSeconds = ([0-9]+)$",
        source,
        re.MULTILINE,
    )
    assert minimum_width is not None
    assert minimum_height is not None
    assert stable_observations is not None
    assert poll_milliseconds is not None
    assert trace_poll_milliseconds is not None
    assert freeze_timeout_seconds is not None
    assert int(minimum_width.group(1)) == 320
    assert int(minimum_height.group(1)) == 200
    assert int(stable_observations.group(1)) >= 2
    assert int(poll_milliseconds.group(1)) >= 50
    assert 1 <= int(trace_poll_milliseconds.group(1)) <= 10
    assert 1 <= int(freeze_timeout_seconds.group(1)) <= 30

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
    assert source.count("\n        $latestObservation = $current\n") == 2
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
    for native_binding in (
        "OpenThread(uint desiredAccess, bool inheritHandle, uint threadId)",
        "SuspendThread(IntPtr threadHandle)",
        "ResumeThread(IntPtr threadHandle)",
        "CloseHandle(IntPtr handle)",
    ):
        assert native_binding in source
    assert "$ThreadSuspendResumeAccess = [uint32]0x0002" in source
    assert (
        "$windowOwnerThreadId = "
        "[ChummerNativeWindowCapture]::GetWindowThreadProcessId("
    ) in source
    assert "$windowOwnerThreadId -ne [uint32]0 -and" in source
    assert (
        "$observedProcessId -ne [uint32]$Target.ProcessId -or"
        in source
    )
    assert (
        "$observedProcessId -ne "
        "[uint32]$script:installerProcessId -or"
    ) in source
    assert "$observedThreadId -ne [uint32]$Target.ThreadId" in source

    def function_source(name: str) -> str:
        start = required_index(f"function {name} {{")
        next_function = source.find("\nfunction ", start + 1)
        assert next_function != -1
        return source[start:next_function]

    suspend_source = function_source("Suspend-InstallerWindowThread")
    assert (
        "$previousSuspendCount = "
        "[ChummerNativeWindowCapture]::SuspendThread("
    ) in suspend_source
    assert "$Target.OwnedSuspendCount = 1" in suspend_source
    assert "$previousSuspendCount -ne [uint32]0" in suspend_source
    assert (
        "$undoSuspendCount -ne "
        "($previousSuspendCount + [uint32]1)"
    ) in suspend_source

    resume_source = function_source("Resume-InstallerWindowThread")
    assert (
        "$previousSuspendCount = "
        "[ChummerNativeWindowCapture]::ResumeThread("
    ) in resume_source
    assert "$previousSuspendCount -ne [uint32]1" in resume_source
    assert resume_source.index(
        "$Target.OwnedSuspendCount = 0"
    ) < resume_source.index(
        "Close-InstallerWindowThreadFreezeTarget -Target $Target"
    )

    frozen_trace_source = function_source(
        "Assert-FrozenInstallerTracePreCompletion"
    )
    assert (
        "Test-TraceHasExactLine -Trace $frozenTrace -Marker $Marker"
        in frozen_trace_source
    )
    assert "-Marker $CompletionMarker" in frozen_trace_source

    target_binding_source = function_source(
        "Assert-InstallerWindowThreadFreezeTargetBinding"
    )
    assert (
        "$script:installerProcess.MainWindowHandle -ne"
        in target_binding_source
    )
    assert "[IntPtr]$Target.WindowHandle" in target_binding_source

    marker_freeze_source = function_source(
        "Wait-TraceMarkerAndSuspendInstallerWindowThread"
    )
    marker_observed = marker_freeze_source.index(
        "if (Test-TraceHasExactLine -Trace $trace -Marker $Marker)"
    )
    just_in_time_binding = marker_freeze_source.index(
        "$script:progressFreezeTarget = ("
    )
    immediate_suspend = marker_freeze_source.index(
        "Suspend-InstallerWindowThread `"
    )
    assert (
        "Assert-InstallerWindowThreadFreezeTargetBinding `"
        in marker_freeze_source
    )
    post_suspend_binding = marker_freeze_source.index(
        "Assert-InstallerWindowThreadFreezeTargetBinding `"
    )
    first_frozen_assertion = marker_freeze_source.index(
        "Assert-FrozenInstallerTracePreCompletion"
    )
    assert (
        marker_observed
        < just_in_time_binding
        < immediate_suspend
        < post_suspend_binding
        < first_frozen_assertion
    )
    assert "Start-Sleep" not in marker_freeze_source[
        just_in_time_binding:immediate_suspend
    ]
    assert (
        "Resume-InstallerWindowThread `"
        in marker_freeze_source
    )
    assert source.count(
        "$script:progressFreezeTarget = ("
    ) == 1
    assert (
        "$script:progressFreezeTarget = "
        "Wait-InstallerWindowThreadFreezeTarget"
    ) not in source
    for forbidden_target_message in (
        "WM_GETTEXT",
        "SendMessageTimeout",
    ):
        assert forbidden_target_message not in source

    process_start = required_index(
        "$script:installerProcess = Start-Process"
    )
    progress_freeze = required_index(
        "Wait-TraceMarkerAndSuspendInstallerWindowThread `"
    )
    assert "Wait-InstallerWindowThreadFreezeTarget" not in source[
        process_start:progress_freeze
    ]
    progress_window = required_index(
        "$progressWindow = Wait-ReviewableMainWindow `"
    )
    assert '-Phase "progress" `' in source[
        progress_window : progress_window + 180
    ]
    progress_capture = source.index("Save-WindowPng `", progress_window)
    assert "-WindowObservation $progressWindow `" in source[
        progress_capture : progress_capture + 180
    ]
    assert "-OutputPath $ProgressScreenshot" in source[
        progress_capture : progress_capture + 180
    ]
    progress_hash = required_index("$progressScreenshotSha256 = (")
    assert (
        "Get-FileHash -LiteralPath $ProgressScreenshot -Algorithm SHA256"
        in source[progress_hash : progress_hash + 220]
    )
    post_capture_frozen_assertion = (
        'Assert-FrozenInstallerTracePreCompletion `\n'
        '            -Marker "Extracting application files" `\n'
        '            -CompletionMarker "Install complete"'
    )
    assert source.count(post_capture_frozen_assertion) == 1
    second_frozen_assertion = source.index(post_capture_frozen_assertion)
    progress_resume = source.index(
        "Resume-InstallerWindowThread `",
        second_frozen_assertion,
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
    distinct_digest = required_index(
        "if ($progressScreenshotSha256 -ceq "
        "$completionScreenshotSha256)"
    )
    assert (
        process_start
        < progress_freeze
        < progress_window
        < progress_capture
        < progress_hash
        < second_frozen_assertion
        < progress_resume
        < completion_marker
        < completion_window
        < completion_capture
        < distinct_digest
    )
    outer_resume = source.rindex("Resume-InstallerWindowThread `")
    close_main_window = required_index(
        "$script:installerProcess.CloseMainWindow()"
    )
    assert outer_resume < close_main_window
    assert (
        "$script:progressFreezeReleaseFailed -or"
        in source[outer_resume:close_main_window]
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
    assert payload["run-name"] == (
        "retry-unsigned-windows-preview-native-30233434560"
    )
    assert payload["concurrency"]["group"] == (
        "retry-unsigned-windows-preview-native-30233434560"
    )
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
    assert relay["env"] == {
        "RETRY_CONFIRMED": "${{ inputs.retry_confirmed }}",
        "EXPECTED_CANDIDATE_RUN_ID": "30233434560",
        "EXPECTED_CANDIDATE_ARTIFACT_ID": "8640821385",
        "EXPECTED_CANDIDATE_ARTIFACT_NAME": (
            "unsigned-windows-preview-nightly-candidate-30233434560-1"
        ),
        "EXPECTED_CANDIDATE_ARTIFACT_SHA256": (
            "3f2054323ab553647a9cb4e86cbc40658e1c46d895767e49ee1caa6fbb674cac"
        ),
        "EXPECTED_CANDIDATE_ARTIFACT_SIZE": "54265931",
        "EXPECTED_CANDIDATE_SHA": (
            "8303b2058c7adbc87f7b1beaa53413a8ec9c2a3c"
        ),
        "EXPECTED_FAILED_CAPTURE_RUN_ID": "30233471183",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_ID": "8640893144",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_NAME": (
            "unsigned-windows-preview-native-diagnostics-30233471183-1"
        ),
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_SHA256": (
            "b3a1fc8da8fcc642e791f93f08d63d4a577d43d0d0425bbdd5e2b09c94dd1803"
        ),
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_SIZE": "2521",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_CREATED_AT": (
            "2026-07-27T03:04:40Z"
        ),
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_UPDATED_AT": (
            "2026-07-27T03:04:40Z"
        ),
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_EXPIRES_AT": (
            "2026-08-10T03:04:39Z"
        ),
        "EXPECTED_REPOSITORY_ID": "1178943375",
        "EXPECTED_OPERATOR_ID": "11421547",
    }
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
        "EXPECTED_CANDIDATE_RUN_ID: \"30233434560\"",
        "EXPECTED_CANDIDATE_ARTIFACT_ID: \"8640821385\"",
        "EXPECTED_CANDIDATE_ARTIFACT_NAME: unsigned-windows-preview-nightly-candidate-30233434560-1",
        "EXPECTED_CANDIDATE_ARTIFACT_SHA256: 3f2054323ab553647a9cb4e86cbc40658e1c46d895767e49ee1caa6fbb674cac",
        "EXPECTED_CANDIDATE_ARTIFACT_SIZE: \"54265931\"",
        "EXPECTED_CANDIDATE_SHA: 8303b2058c7adbc87f7b1beaa53413a8ec9c2a3c",
        "EXPECTED_FAILED_CAPTURE_RUN_ID: \"30233471183\"",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_ID: \"8640893144\"",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_NAME: unsigned-windows-preview-native-diagnostics-30233471183-1",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_SHA256: b3a1fc8da8fcc642e791f93f08d63d4a577d43d0d0425bbdd5e2b09c94dd1803",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_SIZE: \"2521\"",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_CREATED_AT: \"2026-07-27T03:04:40Z\"",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_UPDATED_AT: \"2026-07-27T03:04:40Z\"",
        "EXPECTED_FAILED_DIAGNOSTICS_ARTIFACT_EXPIRES_AT: \"2026-08-10T03:04:39Z\"",
        "EXPECTED_REPOSITORY_ID: \"1178943375\"",
        "failedCapture.data.conclusion !== 'failure'",
        "Capture native startup and installer visuals', 'failure'",
        "Revalidate exact unsigned candidate bytes', 'success'",
        "Record evidence-only artifact identity', 'skipped'",
        "Upload failure-only sanitized startup diagnostics', 'success'",
        "Remove downloaded candidate bytes', 'success'",
        "failedArtifacts.length !== 1",
        "diagnostics.expired !== false",
        "diagnostics.workflow_run.head_branch !== 'main'",
        "diagnostics.workflow_run.head_sha !== process.env.EXPECTED_CANDIDATE_SHA",
        "diagnostics.workflow_run.repository_id",
        "diagnostics.workflow_run.head_repository_id",
        "workflow_id: 'unsigned-windows-preview-native-evidence-capture.yml'",
        "expected_contract_sha: process.env.GITHUB_SHA",
        "capture_confirmed: true",
        "ref: 'main'",
    ):
        assert required in RETRY.read_text(encoding="utf-8")
    assert "30234793837" not in RETRY.read_text(encoding="utf-8")
    assert script.index("process.env.GITHUB_EVENT_NAME") < script.index(
        "require('./scripts/github_workflow_run_path.js')"
    )
    for required_input in (
        "candidate_run_id: '30233434560'",
        "candidate_run_attempt: '1'",
        "candidate_sha: '8303b2058c7adbc87f7b1beaa53413a8ec9c2a3c'",
        "candidate_actor: 'ArchonMegalon'",
        "candidate_artifact_id: '8640821385'",
        "candidate_artifact_name: 'unsigned-windows-preview-nightly-candidate-30233434560-1'",
        "candidate_artifact_sha256: '3f2054323ab553647a9cb4e86cbc40658e1c46d895767e49ee1caa6fbb674cac'",
        "candidate_version: 'run-20260727-025130'",
        "candidate_manifest_sha256: '7328ad808df2f7f191e8cc65da672bc460eb4d1456c042fe3725ad588f26c9cf'",
        "candidate_inventory_sha256: 'b983cbbd3922a9680b11b78eeac7a4d4a5d5daaae5cd6816f991567dea4581cf'",
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


@pytest.mark.skipif(
    shutil.which("pwsh") is None,
    reason="PowerShell is unavailable on this host",
)
def test_installer_visual_exact_trace_runtime_and_native_bindings_compile() -> None:
    runtime_contract = r"""
$ErrorActionPreference = 'Stop'
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $env:CHUMMER_POWERSHELL_PARSE_PATH,
    [ref]$tokens,
    [ref]$errors
)
if ($errors.Count -ne 0) {
    throw ($errors | ForEach-Object Message)
}
$functionAst = $ast.Find(
    {
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -ceq 'Test-TraceHasExactLine'
    },
    $true
)
if (-not $functionAst) {
    throw 'Exact trace-line function is missing.'
}
Invoke-Expression $functionAst.Extent.Text
$cases = @(
    @{
        Trace = "# header`r`nExtracting application files`r`n"
        Marker = "Extracting application files"
        Expected = $true
    },
    @{
        Trace = "# header`nInstall complete`n"
        Marker = "Install complete"
        Expected = $true
    },
    @{
        Trace = "# header`rExtracting application files`r"
        Marker = "Extracting application files"
        Expected = $true
    },
    @{
        Trace = "prefix Extracting application files suffix"
        Marker = "Extracting application files"
        Expected = $false
    },
    @{
        Trace = "install complete"
        Marker = "Install complete"
        Expected = $false
    },
    @{
        Trace = "Install complete later"
        Marker = "Install complete"
        Expected = $false
    }
)
foreach ($case in $cases) {
    $actual = Test-TraceHasExactLine `
        -Trace $case.Trace `
        -Marker $case.Marker
    if ($actual -ne $case.Expected) {
        throw "Exact trace-line runtime contract rejected a test case."
    }
}

$source = Get-Content `
    -LiteralPath $env:CHUMMER_POWERSHELL_PARSE_PATH `
    -Raw
$nativeMatch = [regex]::Match(
    $source,
    '(?s)Add-Type @"\r?\n(?<source>.*?)\r?\n"@'
)
if (-not $nativeMatch.Success) {
    throw 'Embedded native bindings are missing.'
}
Add-Type -TypeDefinition $nativeMatch.Groups['source'].Value
foreach ($methodName in @(
    'OpenThread',
    'SuspendThread',
    'ResumeThread',
    'CloseHandle'
)) {
    $method = [ChummerNativeWindowCapture].GetMethod($methodName)
    if (-not $method) {
        throw "Native binding did not compile: $methodName"
    }
    $attribute = $method.GetCustomAttributes(
        [Runtime.InteropServices.DllImportAttribute],
        $false
    )
    if ($attribute.Count -ne 1) {
        throw "Native binding is not a single DllImport: $methodName"
    }
}
"""
    environment = os.environ.copy()
    environment["CHUMMER_POWERSHELL_PARSE_PATH"] = str(INSTALLER_VISUAL)
    result = subprocess.run(
        [
            "pwsh",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            runtime_contract,
        ],
        check=False,
        capture_output=True,
        env=environment,
        text=True,
    )
    assert result.returncode == 0, (
        "installer visual runtime contract failed:\n"
        f"{result.stdout}\n{result.stderr}"
    )


def test_installer_visual_reacquires_stable_reviewable_window_after_each_marker() -> None:
    assert_installer_visual_window_contract(
        INSTALLER_VISUAL.read_text(encoding="utf-8")
    )


@pytest.mark.parametrize(
    ("needle", "replacement"),
    (
        (
            "$TraceObservationPollMilliseconds = 5",
            "$TraceObservationPollMilliseconds = 50",
        ),
        (
            "$ProgressFreezeTimeoutSeconds = 15",
            "$ProgressFreezeTimeoutSeconds = 60",
        ),
        (
            "$windowOwnerThreadId -ne [uint32]0 -and",
            "$true -and",
        ),
        (
            "$observedThreadId -ne [uint32]$Target.ThreadId",
            "$false",
        ),
        (
            "$script:installerProcess.MainWindowHandle -ne\n"
            "            [IntPtr]$Target.WindowHandle",
            "$false",
        ),
        (
            "Assert-InstallerWindowThreadFreezeTargetBinding `\n"
            "                    -Target $script:progressFreezeTarget",
            "Write-Output $script:progressFreezeTarget",
        ),
        (
            "    Wait-TraceMarkerAndSuspendInstallerWindowThread `\n"
            '        -Marker "Extracting application files" `',
            "    $script:progressFreezeTarget = "
            "Wait-InstallerWindowThreadFreezeTarget\n"
            "    Wait-TraceMarkerAndSuspendInstallerWindowThread `\n"
            '        -Marker "Extracting application files" `',
        ),
        (
            "            )\n"
            "            Suspend-InstallerWindowThread `",
            "            )\n"
            "            Start-Sleep -Milliseconds 100\n"
            "            Suspend-InstallerWindowThread `",
        ),
        (
            "$previousSuspendCount -ne [uint32]0",
            "$previousSuspendCount -lt [uint32]0",
        ),
        (
            "$previousSuspendCount -ne [uint32]1",
            "$previousSuspendCount -lt [uint32]1",
        ),
        (
            "$undoSuspendCount -ne "
            "($previousSuspendCount + [uint32]1)",
            "$false",
        ),
        (
            "Resume-InstallerWindowThread `\n"
            "                        -Target "
            "$script:progressFreezeTarget",
            "Write-Output $Target",
        ),
        (
            '-CompletionMarker "Install complete"\n    } finally {',
            '-CompletionMarker "Never complete"\n    } finally {',
        ),
        (
            "$script:progressFreezeReleaseFailed -or",
            "$false -or",
        ),
        (
            "if ($progressScreenshotSha256 -ceq "
            "$completionScreenshotSha256)",
            "if ($progressScreenshotSha256 -cne "
            "$completionScreenshotSha256)",
        ),
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
            "$progressWindow = Wait-ReviewableMainWindow `\n"
            '            -Phase "progress" `\n'
            "            -TimeoutSeconds $ProgressFreezeTimeoutSeconds",
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
        "slow-trace-poll",
        "unbounded-progress-freeze",
        "missing-window-thread",
        "thread-owner-transition",
        "accept-replaced-main-window",
        "remove-post-suspend-window-binding",
        "restore-stale-pre-marker-binding",
        "sleep-between-jit-binding-and-suspend",
        "accept-pre-suspended-thread",
        "resume-non-owned-count",
        "unsafe-partial-unwind",
        "missing-exception-unwind",
        "remove-post-capture-frozen-trace-check",
        "graceful-cleanup-after-resume-failure",
        "remove-distinct-digest-gate",
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
