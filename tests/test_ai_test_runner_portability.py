from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = REPO_ROOT / "scripts" / "ai" / "test.sh"


def test_mtp_direct_runner_honors_no_build_without_forcing_package_plane_build() -> None:
    text = SCRIPT_PATH.read_text(encoding="utf-8")

    assert "local skip_build=0" in text
    assert "--no-build)" in text
    assert "skip_build=1" in text
    assert 'if [[ "$skip_build" -eq 0 ]]; then' in text
    assert '"$SCRIPT_DIR/with-package-plane.sh" "${build_args[@]}"' in text
