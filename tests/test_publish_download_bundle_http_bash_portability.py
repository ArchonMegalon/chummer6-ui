from __future__ import annotations

from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "publish-download-bundle-http.sh"
AUTHORITATIVE = (
    Path(__file__).resolve().parents[2]
    / "chummer.run-services"
    / "scripts"
    / "publish-download-bundle-http.sh"
)


def test_publish_download_bundle_http_avoids_empty_array_length_expansions() -> None:
    wrapper = SCRIPT.read_text(encoding="utf-8")
    text = AUTHORITATIVE.read_text(encoding="utf-8")

    assert 'exec bash "$AUTHORITATIVE_PUBLISHER" "$@"' in wrapper
    assert "array_count()" in text
    assert 'local restore_nounset=0' in text
    assert 'case "$-" in' in text
    assert 'set +u' in text
    assert 'eval "set -- \\"\\${${array_name}[@]}\\""' in text
    assert 'local count="$#"' in text
    assert 'set -u' in text
    assert 'windows_payload_gate_args+=(--allow-empty)' not in text
    assert 'upload_file_count="$(array_count upload_files)"' in text
    assert '${#windows_payload_gate_args[@]}' not in text
    assert '${#upload_files[@]}' not in text
    assert 'eval "set -- \\${${array_name}[@]+\\"\\${${array_name}[@]}\\"}"' not in text
    assert 'Publishing ${upload_file_count} bundle files from $BUNDLE_DIR' in text
    assert text.index("array_count()") < text.index('upload_file_count="$(array_count upload_files)"')
