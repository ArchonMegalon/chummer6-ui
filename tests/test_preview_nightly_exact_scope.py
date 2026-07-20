from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_preview_seal_declares_the_complete_windows_linux_shelf_scope() -> None:
    script = (ROOT / "scripts" / "build-preview-nightly-stage.sh").read_text(encoding="utf-8")
    verifier_call = script.split(
        'python3 "$CHUMMER_RUN_ROOT/scripts/verify_release_shelf_replacement.py"',
        maxsplit=1,
    )[1].split("\n  python3 ", maxsplit=1)[0]

    assert verifier_call.count("--exact-incoming-tuple") == 2
    assert "--exact-incoming-tuple avalonia:windows:win-x64" in verifier_call
    assert "--exact-incoming-tuple avalonia:linux:linux-x64" in verifier_call
    assert "macos" not in verifier_call
