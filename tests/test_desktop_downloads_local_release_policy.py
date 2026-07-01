from __future__ import annotations

import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_github_actions_workflows_are_not_part_of_presentation_release_policy() -> None:
    assert not (REPO_ROOT / ".github" / ("work" + "flows")).exists()


def test_daily_publish_policy_is_documented_in_local_runbook() -> None:
    runbook = (REPO_ROOT / "docs" / "SELF_HOSTED_DOWNLOADS_RUNBOOK.md").read_text(encoding="utf-8")

    assert "RUNBOOK_MODE=publish-latest-nightly" in runbook
    assert "08:00 Europe/Vienna" in runbook
    assert "once per day in the morning release window" in runbook
    assert "Build only what the proof needs" in runbook
    assert "does not publish the live downloads shelf and does not change the stable channel by itself" in runbook
    assert ("workflow" + "_dispatch") not in runbook
    assert ("GitHub " + "Actions") not in runbook


def test_latest_nightly_publish_preflights_windows_bootstrap_payload_metadata() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "verify_latest_stage_windows_payload_gate()" in publisher
    assert "verify-windows-installer-payloads.py" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert "--allow-empty" in publisher
    assert "Nightly stage failed Windows installer payload preflight. Build a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')


def test_latest_nightly_publish_ignores_incomplete_helper_stage_directories() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "is_publishable_nightly_stage()" in publisher
    assert '[[ -f "$stage_dir/RELEASE_CHANNEL.generated.json" ]] || return 1' in publisher
    assert '[[ -f "$stage_dir/releases.json" ]] || return 1' in publisher
    assert '[[ -d "$stage_dir/files" ]] || return 1' in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert 'echo "No publishable nightly stage found under $STAGING_ROOT"' in publisher
    assert publisher.index('if ! is_publishable_nightly_stage "$candidate"; then') < publisher.index('latest_stage="$candidate"')


def test_latest_nightly_publish_requires_windows_installer_startup_smoke_before_promotion() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")
    verifier = (REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py").read_text(encoding="utf-8")

    assert 'PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}"' in publisher
    assert 'SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-0}"' in publisher
    assert 'ALLOW_SKIPPED_STARTUP_SMOKE="${CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE:-0}"' in publisher
    assert "verify_latest_stage_windows_startup_smoke_gate()" in publisher
    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py"' in publisher
    assert "Windows installer startup-smoke receipt is missing" in verifier
    assert "Windows installer startup-smoke receipt is not passing" in verifier
    assert "Windows installer startup-smoke receipt artifactDigest mismatch" in verifier
    assert "matching stage bytes are missing" in verifier
    assert "RELEASE_CHANNEL.generated.json omits the matching installer row" in verifier
    assert "releases.json omits the matching installer row" in verifier
    assert "refresh_release_build_handoff()" in publisher
    assert 'refresh_release_build_handoff "$latest_stage"' in publisher
    assert "verify_latest_stage_windows_exit_gate()" in publisher
    assert 'bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null' in publisher
    assert 'CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$release_channel_manifest"' in publisher
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$files_dir"' in publisher
    assert 'CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path"' in publisher
    assert 'CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output"' in publisher
    assert "emit_windows_visual_proof_handoff_guidance()" in publisher
    assert 'emit_windows_visual_proof_handoff_guidance "$stage_dir"' in publisher
    assert "Windows visual proof handoff:" in publisher
    assert "Windows visual proof status:" in publisher
    assert "Windows visual proof next action:" in publisher
    assert "Nightly stage failed Windows desktop exit gate preflight. Use the Windows visual proof handoff above before publishing." in publisher
    assert "Nightly stage failed Windows installer startup smoke preflight. Build and smoke-test a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')
    assert 'row_platform_id = norm(row.get("platformId"))' in verifier
    assert 'normalized_arch = normalized_rid.rsplit("-", 1)[-1] if "-" in normalized_rid else normalized_rid' in verifier
    assert 'elif norm(row.get("arch")) != normalized_arch:' in verifier


def test_latest_nightly_publish_verifies_open_public_desktop_install_routes_after_public_edge_redeploy() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_EDGE_VERIFY_BASE_URL="${CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL:-http://127.0.0.1:${CHUMMER_PUBLIC_EDGE_PORT:-8091}}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_HOST="${CHUMMER_PUBLIC_EDGE_VERIFY_HOST:-chummer.run}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_PROTO="${CHUMMER_PUBLIC_EDGE_VERIFY_PROTO:-https}"' in publisher
    assert "verify_public_edge_open_public_install_routes()" in publisher
    assert 'for key in ("downloads", "artifacts"):' in publisher
    assert 'install_access_class == "open_public"' in publisher
    assert 'expected_location = f"/downloads/get/{artifact_id}"' in publisher
    assert 'redirected back to login instead of direct public download' in publisher
    assert 'Published downloads shelf failed open-public installer route verification.' in publisher
    assert 'verify_public_edge_open_public_install_routes \\' in publisher
    assert 'docker compose -f docker-compose.public-edge.yml up -d' in publisher


def test_latest_nightly_publish_remains_preview_handoff_lane() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-preview}"' in publisher
    assert 'ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH="${CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH:-0}"' in publisher
    assert "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." in publisher
    assert "is_publishable_nightly_stage()" in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert "No publishable nightly stage found under $STAGING_ROOT" in publisher


def test_public_edge_e2e_enforces_direct_public_installer_handoff_routes() -> None:
    e2e = (REPO_ROOT / "scripts" / "e2e-public-edge.cjs").read_text(encoding="utf-8")

    assert "function publicInstallerRedirectMatches(response, artifactId)" in e2e
    assert "const expectedLocation = `/downloads/get/${artifactId}`;" in e2e
    assert "!decodeURIComponent(location).includes('/login?next=')" in e2e
    assert "payload.downloads.find(row => row?.artifactId === 'avalonia-win-x64-installer')" in e2e
    assert "payload.downloads.find(row => row?.artifactId === 'avalonia-linux-x64-installer')" in e2e
    assert "url: `${baseUrl}/downloads/install/avalonia-linux-x64-installer`," in e2e
    assert "url: `${baseUrl}/downloads/install/avalonia-win-x64-installer`," in e2e
    assert "publicInstallerRedirectMatches(response, 'avalonia-linux-x64-installer')" in e2e
    assert "publicInstallerRedirectMatches(response, 'avalonia-win-x64-installer')" in e2e


def test_portal_e2e_distinguishes_public_desktop_installer_handoffs_from_account_gated_routes() -> None:
    e2e = (REPO_ROOT / "scripts" / "e2e-portal.cjs").read_text(encoding="utf-8")

    assert "function expectsDirectPublicInstallRedirect(download)" in e2e
    assert "const expectedDirectDownloadRoute = `/downloads/get/${download.id}`;" in e2e
    assert "text.includes('data-download-action=\"download-artifact\"')" in e2e
    assert "text.includes('data-download-dispatch-url=')" in e2e
    assert "text.includes('data-download-link-mode=\"self-host-dispatch\"')" in e2e
    assert "installAccessClass === 'open_public'" in e2e
    assert "platform.includes('windows') || platform.includes('linux')" in e2e
    assert "kind === 'installer' || kind === 'msix' || kind === 'deb'" in e2e
    assert "decodedLocation === expectedDirectDownloadRoute || decodedLocation.endsWith(expectedDirectDownloadRoute)" in e2e
    assert "!decodedLocation.includes('/login?next=')" in e2e


def test_release_candidate_handoff_blocks_when_windows_smoke_exists_without_staged_artifact_or_manifest_row() -> None:
    handoff = (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").read_text(encoding="utf-8")
    handoff_doc = (REPO_ROOT / "docs" / "RELEASE_CANDIDATE_HANDOFF.md").read_text(encoding="utf-8")

    assert "Windows startup-smoke passed for" in handoff
    assert "staged installer bytes are missing" in handoff
    assert "does not expose a matching Windows artifact row" in handoff
    assert "windows_exit_gate_refresh" in handoff
    assert "maybe_materialize_windows_exit_gate" in handoff
    assert '"handoff_only": True' in handoff
    assert '"stable_release_unchanged": True' in handoff
    assert '"requires_separate_publish_lane": True' in handoff
    assert '"stage_proof_complete": stage_proof_complete' in handoff
    assert "Keep the live downloads shelf and stable channel unchanged" in handoff
    assert '"promotion_ready": stage_proof_complete' in handoff
    assert "This handoff does not publish the live downloads shelf and does not change the stable channel by itself." in handoff_doc
    assert "`stage_proof_complete: false`" in handoff_doc
    assert "Public/stable publication remains a separate explicit operator lane." in handoff_doc


def test_s3_publish_windows_payload_gate_allows_empty_only_before_installers_are_added() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "windows_payload_gate_args=(" in publisher
    assert "--files-dir \"$FILES_SOURCE\"" in publisher
    assert "--manifest \"$MANIFEST_SOURCE\"" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert 'if [[ "${#windows_payload_gate_args[@]}" -eq 6 ]]; then' in publisher
    assert "--allow-empty" in publisher


def test_windows_bootstrap_build_is_measured_by_the_real_payload_gate() -> None:
    builder = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")
    native_builder = (REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh").read_text(encoding="utf-8")
    bootstrap_template = (REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi").read_text(encoding="utf-8")

    assert 'local installer_mode="${CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap}"' in builder
    assert 'bootstrap_payload_url="${CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL:-${downloads_prefix%/}/$(basename "$payload_zip")}"' in builder
    assert 'write_windows_bootstrap_config' in builder
    assert 'scripts/build-native-windows-bootstrap-installer.sh' in builder
    assert 'verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"' in builder
    assert "Windows bootstrap installer build is blocked until the native bootstrap builder is wired." not in builder
    assert "The .NET WinForms installer is too large for bootstrap promotion" not in builder
    assert "Use CHUMMER_WINDOWS_INSTALLER_MODE=bundled for a local full installer" not in builder
    assert "bundled|append|appended)" in builder
    assert "7z2602-extra.7z" in native_builder
    assert "CHUMMER_WINDOWS_CURL_URL" in native_builder
    assert "CHUMMER_WINDOWS_CURL_SHA256" in native_builder
    assert 'mkdir -p "$STAGE_DIR/curl"' in native_builder
    assert "makensis" in native_builder
    assert 'ReadEnvStr $0 "TEMP"' in bootstrap_template
    assert 'ReadEnvStr $0 "TMP"' in bootstrap_template
    assert 'CreateDirectory "$0\\Chummer6"' in bootstrap_template
    assert 'Push "$0\\Chummer6\\installer-temp"' in bootstrap_template
    assert "InitPluginsDir" in bootstrap_template
    assert bootstrap_template.index('ReadEnvStr $0 "TEMP"') < bootstrap_template.index("InitPluginsDir")
    assert bootstrap_template.index("InitPluginsDir") < bootstrap_template.index('Push "$PLUGINSDIR"')
    assert "Function EnsureBootstrapTempRoot" in bootstrap_template
    assert "Function NormalizePathToR9" in bootstrap_template
    assert "Function TryUseBootstrapTempRootCandidate" in bootstrap_template
    assert 'GetFullPathName $1 "$0"' in bootstrap_template
    assert 'FileOpen $2 "$9\\bootstrap-root-probe.tmp" w' in bootstrap_template
    assert 'Push "Bootstrap temp root: $BootstrapTempRoot"' in bootstrap_template
    assert 'SetOutPath "$BootstrapTempRoot"' in bootstrap_template
    assert 'File /oname=7za.exe "${CHUMMER_STAGE_DIR}/7zip/7za.exe"' in bootstrap_template
    assert 'File /oname=curl.exe "${CHUMMER_STAGE_DIR}/curl/curl.exe"' in bootstrap_template
    assert 'File /oname=libcurl-x64.dll "${CHUMMER_STAGE_DIR}/curl/libcurl-x64.dll"' in bootstrap_template
    assert 'File /oname=curl-ca-bundle.crt "${CHUMMER_STAGE_DIR}/curl/curl-ca-bundle.crt"' in bootstrap_template
    assert 'Push "$BootstrapTempRoot\\${CHUMMER_PAYLOAD_FILE_NAME}"' in bootstrap_template
    assert "Call NormalizePathToR9" in bootstrap_template
    assert 'StrCpy $EffectivePayloadPath $9' in bootstrap_template
    assert 'StrCpy $1 $EffectivePayloadPath 2' in bootstrap_template
    assert 'Push "Chummer could not resolve a writable payload download target."' in bootstrap_template
    assert 'Push "Payload download target: $EffectivePayloadPath"' in bootstrap_template
    assert "Function TryDownloadPayloadWithCurl" in bootstrap_template
    assert "Var DownloadHelperPartialPath" in bootstrap_template
    assert "Var DownloadHelperExitCodePath" in bootstrap_template
    assert "Function UpdateInstFilesStatusText" in bootstrap_template
    assert "Function SetInstFilesProgressPosition" in bootstrap_template
    assert 'GetDlgItem $1 $HWNDPARENT 1006' in bootstrap_template
    assert 'GetDlgItem $1 $HWNDPARENT 0x3ec' in bootstrap_template
    assert 'StrCpy $DownloadHelperPartialPath "$BootstrapTempRoot\\${CHUMMER_PAYLOAD_FILE_NAME}.partial"' in bootstrap_template
    assert 'StrCpy $DownloadHelperStartedPath "$BootstrapTempRoot\\download-started.txt"' in bootstrap_template
    assert 'StrCpy $DownloadHelperExitCodePath "$BootstrapTempRoot\\download-exit-code.txt"' in bootstrap_template
    assert 'StrCpy $DownloadHelperStdErrPath "$BootstrapTempRoot\\download-curl-stderr.txt"' in bootstrap_template
    assert 'FileWrite $6 ">$\\"$DownloadHelperStartedPath$\\" echo started$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "del /q $\\"$DownloadHelperPartialPath$\\" 2>nul$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "del /q $\\"$EffectivePayloadPath$\\" 2>nul$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "$\\"$BootstrapTempRoot\\curl.exe$\\" --location --fail --silent --show-error --retry 5 --retry-delay 2 --connect-timeout 20 --cacert $\\"$BootstrapTempRoot\\curl-ca-bundle.crt$\\" --output $\\"$DownloadHelperPartialPath$\\" $\\"$EffectivePayloadUrl$\\" 1>$\\"$BootstrapTempRoot\\download-curl-stdout.txt$\\" 2>$\\"$DownloadHelperStdErrPath$\\"$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 ">$\\"$DownloadHelperExitCodePath$\\" echo %EXITCODE%$\\r$\\n"' in bootstrap_template
    assert 'nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C start "" /B "$SYSDIR\\cmd.exe" /C call $6\'' in bootstrap_template
    assert 'StrCpy $0 "Downloading application files - $6% - $3 / $8 MiB - $2"' in bootstrap_template
    assert 'StrCpy $0 "Downloading application files - 100% - $3 / $8 MiB - $2"' in bootstrap_template
    assert 'StrCpy $DownloadHelperOutput "bundled curl downloader did not start."' in bootstrap_template
    assert 'StrCpy $DownloadHelperOutput "bundled curl download timed out."' in bootstrap_template
    assert 'Push "Payload download completed with bundled curl"' in bootstrap_template
    assert 'Push "Bundled curl download failed code=$DownloadHelperStatus output=$DownloadHelperOutput"' in bootstrap_template
    assert 'Push "Payload download failed; legacy NSIS downloader is disabled for bootstrap installs"' in bootstrap_template
    assert "NSISdl::download" not in bootstrap_template
    assert 'Delete "$BootstrapTempRoot\\chummer-verify-size.cmd"' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-verify-size.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "for %%I in ($\\"$EffectivePayloadPath$\\") do @echo %%~zI$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-verify-size.cmd"' in bootstrap_template
    assert 'Delete "$BootstrapTempRoot\\payload-hash.txt"' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-verify-payload.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "7za.exe h -scrcSHA256 $\\"$EffectivePayloadPath$\\" > payload-hash.txt$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-verify-payload.cmd"' in bootstrap_template
    assert 'nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C call $6\'' in bootstrap_template
    assert 'FileOpen $3 "$BootstrapTempRoot\\payload-hash.txt" r' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-extract-payload.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "7za.exe x -y $\\"-o$INSTDIR$\\" $\\"$EffectivePayloadPath$\\"$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-extract-payload.cmd"' in bootstrap_template
    assert bootstrap_template.count('nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C call $6\'') >= 2
    assert 'WriteRegStr HKCU "Software\\Classes\\chummer\\shell\\open\\command"' in bootstrap_template
    assert 'pending-claim-code.txt' in bootstrap_template
    assert 'cp -f "$DIST_DIR/$installer_name" "$DIST_DIR/files/$installer_name"' in builder
    assert builder.index('cp -f "$DIST_DIR/$installer_name" "$DIST_DIR/files/$installer_name"') < builder.index('verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"')


def test_unsigned_public_release_override_disables_packaging_signing_requirements() -> None:
    result = subprocess.run(
        [
            "bash",
            str(REPO_ROOT / "scripts" / "resolve-desktop-release-context.sh"),
        ],
        text=True,
        capture_output=True,
        check=False,
        env={
            "CHUMMER_DESKTOP_RELEASE_CHANNEL": "public_stable",
            "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE": "true",
        },
    )

    assert result.returncode == 0, result.stderr
    assert "public_release=true" in result.stdout
    assert "allow_unsigned_public_release=true" in result.stdout
    assert "windows_signing_required=false" in result.stdout
    assert "mac_signing_required=false" in result.stdout
    assert "mac_notarization_required=false" in result.stdout


def test_windows_startup_smoke_prefers_local_bootstrap_payload_sidecar_when_present() -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert 'chummerwinsmokeXXXXXX' in smoke
    assert 'local payload_name="${artifact_name%-installer.exe}-payload.zip"' in smoke
    assert 'local_payload_path="$artifact_dir/files/$payload_name"' in smoke
    assert "WINDOWS_LOCAL_PAYLOAD_COPY" in smoke
    assert "winepath -u 'C:\\\\windows\\\\temp'" in smoke
    assert 'cp "$local_payload_path" "$WINDOWS_LOCAL_PAYLOAD_COPY"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_PATH="$(to_native_path "$local_payload_path")"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SHA256="$local_payload_sha256"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes"' in smoke


def test_release_manifest_generation_prunes_install_proof_routes_to_published_artifacts() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "prune_release_proof_routes_to_manifest_artifacts" in generator
    assert 'route.startswith("/downloads/install/")' in generator
    assert 'artifact_id in artifact_ids' in generator
    assert 'release_proof["proofRoutes"] = prune_routes' in generator


def test_release_manifest_generation_can_skip_external_host_proof_blockers_for_artifact_only_publish_paths() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-1}"' in generator
    assert 'if to_bool "$GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS"; then' in generator
    assert 'materialize-external-host-proof-blockers.py' in generator
    assert 'echo "skipped external host proof blocker materialization"' in generator


def test_publish_download_bundle_defaults_external_host_proof_blockers_off_during_shelf_sync() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \\' in publish_script


def test_publish_download_bundle_carries_windows_bootstrap_progress_logs_into_the_deploy_shelf() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "refresh_release_build_handoff()" in publish_script
    assert 'refresh_release_build_handoff "$BUNDLE_DIR"' in publish_script
    assert 'refresh_release_build_handoff "$DEPLOY_DIR"' in publish_script
    assert '-name "windows-installer-progress-*.log"' in publish_script
    assert 'cp -f "$STARTUP_SMOKE_SOURCE"/windows-installer-progress-*.log "$startup_smoke_deploy_dir"/' in publish_script
    assert 'bash "$SCRIPT_DIR/generate-releases-manifest.sh"' in publish_script
    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \\' in publish_script
    assert "verify_windows_desktop_exit_gate()" in publish_script
    assert 'bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null' in publish_script
    assert 'CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json"' in publish_script
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEPLOY_DIR/files"' in publish_script
    assert 'CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path"' in publish_script
    assert 'CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output"' in publish_script
    assert "emit_windows_visual_proof_handoff_guidance()" in publish_script
    assert 'emit_windows_visual_proof_handoff_guidance "$BUNDLE_DIR" "$DEPLOY_DIR"' in publish_script
    assert "Windows visual proof handoff:" in publish_script
    assert "Windows visual proof summary:" in publish_script
    assert "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." in publish_script
    assert '--release-channel "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \\' in publish_script
    assert '--downloads-manifest "$DEPLOY_DIR/releases.json" \\' in publish_script
    assert '--startup-smoke-dir "$STARTUP_SMOKE_SOURCE" \\' in publish_script
    assert '--files-dir "$DEPLOY_DIR/files" >/dev/null' in publish_script
    assert publish_script.index('python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \\') < publish_script.rindex("\nverify_windows_desktop_exit_gate\n")


def test_release_build_checks_are_owned_by_local_scripts() -> None:
    assert (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").is_file()


def test_linux_desktop_exit_gate_reports_direct_host_build_failures_before_missing_host_noise() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert 'local test_output_root="$test_project_dir/bin/Release"' in gate
    assert 'local test_assembly_path="$test_project_dir/bin/Release/$FRAMEWORK/$TEST_ASSEMBLY_NAME"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "${TEST_ASSEMBLY_NAME%.dll}"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "$TEST_ASSEMBLY_NAME"' in gate
    assert 'KEEP_SOURCE_SNAPSHOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_KEEP_SOURCE_SNAPSHOT:-0}"' in gate
    assert '[linux-desktop-exit-gate] desktop runtime test host build failed' in gate
    assert 'desktop runtime test host via dotnet' in gate
    assert 'exec dotnet "$(basename "$test_assembly_path")" "$@"' in gate
    assert 'Promoted Linux installer file is missing from the release-aligned desktop shelf' in gate
    assert gate.index('desktop runtime test host build failed') < gate.index('desktop runtime test host is missing or not executable')


def test_windows_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted Windows installer was not resolved from the release-aligned desktop shelf." in gate


def test_macos_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_MACOS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted macOS installer was not resolved from the release-aligned desktop shelf" in gate


def test_aggregate_desktop_materializer_defers_to_release_aligned_shelf_resolution() -> None:
    gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'release_channel_path_value = globals().get("release_channel_path")' in gate
    assert 'release_channel_root = (' in gate
    assert 'release_aligned_files_root = release_channel_root / "files"' in gate
    assert 'release_aligned_startup_smoke_root = release_channel_root / "startup-smoke"' in gate
    assert 'installer_path = str(release_aligned_files_root / installer_name)' in gate
    assert 'mkdir -p {release_aligned_files_root}' in gate
    assert 'installer_path_suffix = f"/files/{installer_name}"' in gate
    assert 'startup_smoke_suffix = "/startup-smoke"' in gate


def test_next90_m144_guard_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (
        REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "next90-m144-ui-startup-smoke-and-executable-gate-check.sh"
    ).read_text(encoding="utf-8")

    assert 'default_downloads_root="$repo_root/Docker/Downloads/files"' in gate
    assert 'default_startup_smoke_dir="$repo_root/Docker/Downloads/startup-smoke"' in gate
    assert 'release_channel_directory="$(cd "$(dirname "$release_channel_path")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'release_aligned_downloads_root="$release_channel_directory/files"' in gate
    assert 'release_aligned_startup_smoke_dir="$release_channel_directory/startup-smoke"' in gate
    assert 'downloads_root="$CHUMMER_NEXT90_M144_DOWNLOADS_ROOT"' in gate
    assert 'downloads_root="$release_aligned_downloads_root"' in gate
    assert 'startup_smoke_dir="$CHUMMER_NEXT90_M144_STARTUP_SMOKE_DIR"' in gate
    assert 'startup_smoke_dir="$release_aligned_startup_smoke_dir"' in gate
    assert "is missing a local artifact under the release-aligned desktop shelf." in gate
