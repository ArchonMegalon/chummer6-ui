from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify.sh"
UPDATE_RUNTIME_TESTS = REPO_ROOT / "Chummer.Tests" / "DesktopUpdateRuntimeTests.cs"
WINDOWS_BOOTSTRAP_INSTALLER = REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi"


def test_shared_verify_lane_keeps_desktop_update_runtime_suite_wired() -> None:
    text = VERIFY_SCRIPT.read_text(encoding="utf-8")
    assert "DesktopUpdateRuntimeTests" in text
    assert "desktop_runtime_test_filter" in text
    assert "RunDesktopUpdateTestsOnly=true" in text


def test_desktop_update_runtime_suite_keeps_windows_bootstrap_handoff_coverage() -> None:
    text = UPDATE_RUNTIME_TESTS.read_text(encoding="utf-8")
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert "BuildWindowsInstallerArguments_include_payload_handoff_contract_when_present" in text
    assert "BuildWindowsInstallerEnvironment_includes_local_bootstrap_payload_handoff" in text
    assert 'InvokePrivateStaticTask(' in text


def test_installer_handoff_keeps_staged_bootstrap_payload_until_next_startup_cleanup() -> None:
    runtime_text = (REPO_ROOT / "Chummer.Desktop.Runtime" / "DesktopUpdateRuntime.cs").read_text(encoding="utf-8")
    launch_installer_start = runtime_text.index("private static async Task<int> LaunchInstallerAsync(")
    launch_installer_end = runtime_text.index("private static bool IsPublishedManifest(", launch_installer_start)
    launch_installer_body = runtime_text[launch_installer_start:launch_installer_end]

    assert "TryDeleteDirectory(request.StageRoot);" not in launch_installer_body
    assert "CleanupCompletedUpdateArtifacts(paths.TempRoot);" in runtime_text


def test_windows_bootstrap_installer_falls_back_to_download_metadata_when_local_handoff_is_missing() -> None:
    text = WINDOWS_BOOTSTRAP_INSTALLER.read_text(encoding="utf-8")
    assert "Function NormalizePathToR9" in text
    assert 'GetFullPathName $1 "$0"' in text
    assert "Function TryUseBootstrapTempRootCandidate" in text
    assert 'FileOpen $2 "$9\\bootstrap-root-probe.tmp" w' in text
    assert "Function EnsureBootstrapTempRoot" in text
    assert 'ReadEnvStr $0 "TEMP"' in text
    assert 'ReadEnvStr $0 "TMP"' in text
    assert 'CreateDirectory "$0\\Chummer6"' in text
    assert 'Push "$0\\Chummer6\\installer-temp"' in text
    assert "Call TryUseBootstrapTempRootCandidate" in text
    assert 'StrCpy $BootstrapTempRoot $9' in text
    assert "InitPluginsDir" in text
    assert 'Push "$PLUGINSDIR"' in text
    assert text.index('ReadEnvStr $0 "TEMP"') < text.index("InitPluginsDir")
    assert 'Push "Bootstrap temp root: $BootstrapTempRoot"' in text
    assert 'ReadEnvStr $PayloadPathOverride "CHUMMER_INSTALLER_PAYLOAD_PATH"' in text
    assert 'ReadEnvStr $PayloadUrlOverride "CHUMMER_INSTALLER_PAYLOAD_URL"' in text
    assert 'Push "$BootstrapTempRoot\\${CHUMMER_PAYLOAD_FILE_NAME}"' in text
    assert "Call NormalizePathToR9" in text
    assert 'StrCpy $EffectivePayloadPath $9' in text
    assert 'StrCpy $1 $EffectivePayloadPath 2' in text
    assert 'Push "Chummer could not resolve a writable payload download target."' in text
    assert 'Push "Payload download target: $EffectivePayloadPath"' in text
    assert 'Push "Local payload handoff was missing, falling back to payload download metadata"' in text
    assert "Function TryDownloadPayloadWithCurl" in text
    assert 'File /oname=curl.exe "${CHUMMER_STAGE_DIR}/curl/curl.exe"' in text
    assert 'File /oname=libcurl-x64.dll "${CHUMMER_STAGE_DIR}/curl/libcurl-x64.dll"' in text
    assert 'File /oname=curl-ca-bundle.crt "${CHUMMER_STAGE_DIR}/curl/curl-ca-bundle.crt"' in text
    assert 'FileWrite $6 ">$\\"$DownloadHelperStartedPath$\\" echo started$\\r$\\n"' in text
    assert 'FileWrite $6 "del /q $\\"$DownloadHelperPartialPath$\\" 2>nul$\\r$\\n"' in text
    assert 'FileWrite $6 "del /q $\\"$EffectivePayloadPath$\\" 2>nul$\\r$\\n"' in text
    assert 'FileWrite $6 "$\\"$BootstrapTempRoot\\curl.exe$\\" --location --fail --silent --show-error --retry 5 --retry-delay 2 --connect-timeout 20 --cacert $\\"$BootstrapTempRoot\\curl-ca-bundle.crt$\\" --output $\\"$DownloadHelperPartialPath$\\" $\\"$EffectivePayloadUrl$\\" 1>$\\"$BootstrapTempRoot\\download-curl-stdout.txt$\\" 2>$\\"$DownloadHelperStdErrPath$\\"$\\r$\\n"' in text
    assert 'FileWrite $6 "    move /y $\\"$DownloadHelperPartialPath$\\" $\\"$EffectivePayloadPath$\\" >nul$\\r$\\n"' in text
    assert 'Push "Payload download completed with bundled curl"' in text
    assert 'Push "Bundled curl download failed code=$DownloadHelperStatus output=$DownloadHelperOutput"' in text
    assert 'Push "Payload download failed; legacy NSIS downloader is disabled for bootstrap installs"' in text
    assert "NSISdl::download" not in text
    assert 'StrCpy $PayloadPathOverride ""' in text
    assert 'Push "Skipping payload size verification for local payload handoff"' in text
    assert '${If} $IsSmokeInstall == "1"' in text
