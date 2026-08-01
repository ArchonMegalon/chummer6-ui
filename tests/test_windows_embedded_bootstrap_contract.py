from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
BUILDER = REPO_ROOT / "scripts" / "build-desktop-installer.sh"
NATIVE_BUILDER = REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh"
METADATA_FINALIZER = REPO_ROOT / "scripts" / "finalize-windows-bootstrap-installer.py"
NSIS_INSTALLER = REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi"
STARTUP_SMOKE = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"
STARTUP_SMOKE_VERIFIER = REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py"
MANIFEST_GENERATOR = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"
WINDOWS_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"


def test_embedded_bootstrap_build_contract_is_opt_in_and_emits_stable_evidence_marker() -> None:
    builder = BUILDER.read_text(encoding="utf-8")
    native_builder = NATIVE_BUILDER.read_text(encoding="utf-8")
    finalizer = METADATA_FINALIZER.read_text(encoding="utf-8")

    assert 'CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE:-download' in builder
    assert 'download|embedded)' in builder
    assert 'bootstrap_embedded_payload_path="/work/$(basename "$payload_zip")"' in builder
    assert 'cp -f "$payload_zip" "$native_bootstrap_stage_dir/$(basename "$payload_zip")"' in builder
    assert "!define CHUMMER_EMBEDDED_PAYLOAD_PATH" in builder
    assert '"payloadAcquisitionMode": "$bootstrap_payload_acquisition_mode"' in builder
    assert "finalize-windows-bootstrap-installer.py" not in builder
    assert "finalize-windows-bootstrap-installer.py" in native_builder
    assert "--validate-payload-only" in native_builder
    assert native_builder.index("--validate-payload-only") < native_builder.index("docker run --rm")
    assert native_builder.count('--installer "$STAGE_DIR/output-installer.exe"') == 1
    assert 'f"payloadAcquisitionMode={acquisition_mode}\\n"' in finalizer
    assert "validate_embedded_payload(config_path, defines)" in finalizer
    assert "marker_offsets(installer_path)" in finalizer
    assert "append_bootstrap_metadata_to_windows_installer" not in builder
    assert builder.index('cp -f "$payload_zip" "$native_bootstrap_stage_dir/$(basename "$payload_zip")"') < builder.index(
        '"$REPO_ROOT/scripts/build-native-windows-bootstrap-installer.sh"'
    )


def test_nsis_prefers_compiled_payload_and_still_verifies_exact_size_and_sha() -> None:
    installer = NSIS_INSTALLER.read_text(encoding="utf-8")
    ensure_start = installer.index("Function EnsurePayloadPath")
    ensure_end = installer.index("Function VerifyPayloadSize", ensure_start)
    ensure_body = installer[ensure_start:ensure_end]

    assert 'File /oname=${CHUMMER_PAYLOAD_FILE_NAME} "${CHUMMER_EMBEDDED_PAYLOAD_PATH}"' in installer
    assert "!ifdef CHUMMER_EMBEDDED_PAYLOAD_PATH" in ensure_body
    assert 'StrCpy $PayloadAcquisitionMode "embedded"' in ensure_body
    assert 'Push "Payload acquisition mode: embedded"' in ensure_body
    assert 'Push "Using embedded payload $EffectivePayloadPath"' in ensure_body
    assert 'StrCpy $PayloadPathOverride ""' in ensure_body
    assert ensure_body.index("!ifdef CHUMMER_EMBEDDED_PAYLOAD_PATH") < ensure_body.index(
        '${If} $PayloadPathOverride != ""'
    )
    assert ensure_body.index("!ifdef CHUMMER_EMBEDDED_PAYLOAD_PATH") < ensure_body.index(
        "Call TryDownloadPayloadWithCurl"
    )
    assert installer.index("Call EnsurePayloadPath") < installer.index("Call VerifyPayloadSize")
    assert installer.index("Call VerifyPayloadSize") < installer.index("Call VerifyPayloadSha256")
    assert 'FileWrite $6 "7za.exe h -scrcSHA256' in installer


def test_shipped_nsis_bootstrap_exposes_trace_verified_native_layout_scale() -> None:
    installer = NSIS_INSTALLER.read_text(encoding="utf-8")
    parse_start = installer.index("Function ParseCommandLine")
    parse_end = installer.index("Function TrimLineEnding", parse_start)
    parse_body = installer[parse_start:parse_end]
    completion_start = installer.index('  Push "Install complete"')
    completion_end = installer.index("done:", completion_start)
    completion_body = installer[completion_start:completion_end]

    assert 'PageCallbacks "" InstFilesPageShow ""' in installer
    assert '${GetOptions} "$CommandLine" "--visual-audit-scale" $VisualAuditScale' in parse_body
    assert '$VisualAuditScale != "1.0"' in parse_body
    assert '$VisualAuditScale != "1.5"' in parse_body
    assert (
        'visual audit render scale requested=$VisualAuditScale mode=installer-native-layout'
        in parse_body
    )
    assert "Function ApplyVisualAuditLayoutScale" in installer
    assert 'System::Call "user32::EnumChildWindows(' in installer
    assert 'System::Call "user32::SetWindowPos(' in installer
    assert "${WM_SETFONT}" in installer
    assert "Call ApplyVisualAuditLayoutScale" in installer
    assert 'Push "install-progress"\n  Call TraceVisualAuditSurface' in installer
    assert (
        "visual audit render scale observed=$VisualAuditScale "
        "mode=installer-native-layout surface=$9 "
        "system_dpi=$VisualAuditSystemDpi effective_dpi=$VisualAuditEffectiveDpi"
        in installer
    )
    assert 'SendMessage $HWNDPARENT ${WM_SETTEXT} 0 "STR:${CHUMMER_DISPLAY_NAME} Install Complete"' in completion_body
    assert 'Push "completion"\n    Call TraceVisualAuditSurface' in completion_body
    assert '${If} $VisualAuditScale == ""\n      MessageBox MB_YESNO' in completion_body


def test_none_mode_emits_exact_embedded_smoke_receipt_without_payload_override() -> None:
    smoke = STARTUP_SMOKE.read_text(encoding="utf-8")
    verifier = STARTUP_SMOKE_VERIFIER.read_text(encoding="utf-8")

    assert 'WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE="${CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-auto}"' in smoke
    assert 'configured_payload_mode="$(lower_ascii "${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-}")"' in smoke
    assert "local|download|none)" in smoke
    assert 'WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="embedded"' in smoke
    embedded_branch = smoke[smoke.index('  else\n    WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_MODE="embedded"') :]
    embedded_branch = embedded_branch[: embedded_branch.index("\n  fi")]
    assert "CHUMMER_INSTALLER_PAYLOAD_PATH=" not in embedded_branch
    assert "CHUMMER_INSTALLER_PAYLOAD_URL=" not in embedded_branch
    assert "CHUMMER_INSTALLER_PAYLOAD_SHA256=" not in embedded_branch
    assert "CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES=" not in embedded_branch
    assert 'CHUMMER_INSTALLER_PAYLOAD_URL="$WINDOWS_STARTUP_SMOKE_EFFECTIVE_PAYLOAD_URL"' in smoke
    assert 'return norm(row.get("payloadAcquisitionMode")) or "download"' in verifier
    assert 'if expected_acquisition_mode == "embedded":' in verifier
    assert '"Payload acquisition mode: embedded"' in verifier
    assert 'if expected_acquisition_mode == "download":' in verifier


def test_release_evidence_requires_manifest_bound_mode_and_recognizable_bootstrap_payload() -> None:
    generator = MANIFEST_GENERATOR.read_text(encoding="utf-8")
    exit_gate = WINDOWS_EXIT_GATE.read_text(encoding="utf-8")

    assert "--require-embedded-bootstrap-metadata" in generator
    assert "--require-manifest-row" in generator
    assert 'str(row.get("payloadAcquisitionMode") or "").strip().lower(),' in generator
    assert "bootstrap_payload_path = expected_bootstrap_payload_path(installer_path)" in exit_gate
    assert 'evidence["bootstrap_payload_exists"] = bootstrap_payload_exists' in exit_gate
    assert "has_recognizable_payload = payload_marker_present or appended_payload_marker_present or bootstrap_payload_exists" in exit_gate
    assert "Published Windows installer is missing a recognizable desktop payload marker." in exit_gate
