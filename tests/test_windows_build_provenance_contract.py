from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
BUILD_SCRIPT = REPO_ROOT / "scripts" / "build-desktop-installer.sh"


def test_windows_builder_records_installer_and_downloadable_payload_independently() -> None:
    text = BUILD_SCRIPT.read_text(encoding="utf-8")

    assert "windows_installer_provenance_invocation_id()" in text
    assert "windows_payload_provenance_invocation_id()" in text
    assert "CHUMMER_WINDOWS_BUILD_PROVENANCE_INSTALLER_INVOCATION_ID" in text
    assert "CHUMMER_WINDOWS_BUILD_PROVENANCE_PAYLOAD_INVOCATION_ID" in text
    assert 'validate_windows_provenance_invocation_id "$invocation_id" "installer"' in text
    assert 'validate_windows_provenance_invocation_id "$payload_invocation_id" "payload"' in text
    assert "installer and payload invocation IDs must be distinct" in text
    assert '--artifact-id "avalonia-win-x64-installer"' in text
    assert '--artifact-kind "desktop_download"' in text
    assert '--artifact-id "avalonia-win-x64-installer-payload"' in text
    assert '--artifact-kind "desktop_payload"' in text
    assert '--artifact-name "chummer-avalonia-win-x64-payload.zip"' in text
    assert '--artifact-path "$payload_artifact_path"' in text
    assert 'local installer_sbom_path="$governed_root/sbom/avalonia-win-x64-installer.cdx.json"' in text
    assert 'local payload_sbom_path="$governed_root/sbom/avalonia-win-x64-installer-payload.cdx.json"' in text
    assert '--sbom-path "$installer_sbom_path"' in text
    assert '--sbom-path "$payload_sbom_path"' in text
    assert 'Windows proof provenance requires a fresh payload output path' in text
    assert '--state "$payload_state_path"' in text
    assert '--output "$payload_receipt_path"' in text


def test_windows_builder_finalizes_both_receipts_after_artifact_creation() -> None:
    text = BUILD_SCRIPT.read_text(encoding="utf-8")

    build_offset = text.index("    build_windows_installer\n")
    finalize_offset = text.index("    finalize_windows_build_provenance\n")
    assert build_offset < finalize_offset
    assert text.count('"$PYTHON_BIN" "$generator" finalize \\') >= 2
    assert 'rm -f "$payload_state_path"' in text
    assert 'rm -rf "$private_root/.$payload_invocation_id.state.json.finalized"' in text
