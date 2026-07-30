from __future__ import annotations

import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
WRAPPER = REPO_ROOT / "scripts" / "publish-download-bundle-http.sh"
AUTHORITATIVE = (
    REPO_ROOT.parent
    / "chummer.run-services"
    / "scripts"
    / "publish-download-bundle-http.sh"
)


def test_presentation_http_publisher_delegates_to_the_governed_lane() -> None:
    wrapper = WRAPPER.read_text(encoding="utf-8")

    assert "set +x" in wrapper
    assert "umask 077" in wrapper
    assert (
        'AUTHORITATIVE_PUBLISHER="$REPO_ROOT/../chummer.run-services/scripts/'
        'publish-download-bundle-http.sh"'
    ) in wrapper
    assert '[[ ! -f "$AUTHORITATIVE_PUBLISHER" || -L "$AUTHORITATIVE_PUBLISHER" ]]' in wrapper
    assert 'exec bash "$AUTHORITATIVE_PUBLISHER" "$@"' in wrapper
    assert "ALLOW_DIRECT_FALLBACK" not in wrapper


def test_governed_http_publisher_is_present_and_not_the_wrapper() -> None:
    assert AUTHORITATIVE.is_file()
    assert not AUTHORITATIVE.is_symlink()
    assert AUTHORITATIVE.resolve() != WRAPPER.resolve()

    source = AUTHORITATIVE.read_text(encoding="utf-8")
    assert "durable upload-session protocol" in source
    assert "Direct release upload fallback is permanently disabled" in source
    assert "release_upload_attempt_receipt.py" in source
    assert "request_started" in source
    assert "durably_aborted" in source
    assert "activation" in source


def test_wrapper_preserves_authoritative_fail_closed_bundle_preflight(
    tmp_path: Path,
) -> None:
    missing_bundle = tmp_path / "missing"
    environment = os.environ.copy()
    for name in (
        "CHUMMER_RELEASE_UPLOAD_URL",
        "CHUMMER_RELEASE_UPLOAD_TOKEN",
        "CHUMMER_RELEASE_UPLOAD_TOKEN_FILE",
        "CHUMMER_RELEASE_UPLOAD_TOKEN_PATH",
    ):
        environment.pop(name, None)

    completed = subprocess.run(
        ["bash", str(WRAPPER), str(missing_bundle)],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode != 0
    assert f"Bundle directory not found: {missing_bundle}" in completed.stderr
    assert "falling back to direct bundle upload" not in completed.stderr
