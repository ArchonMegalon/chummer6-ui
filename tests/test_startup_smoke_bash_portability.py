from __future__ import annotations

from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "run-desktop-startup-smoke.sh"


def test_startup_smoke_avoids_bash4_case_conversion_expansions() -> None:
    text = SCRIPT.read_text(encoding="utf-8")

    assert "array_count()" in text
    assert "lower_ascii()" in text
    assert "upper_ascii()" in text
    assert '${PROCESSOR_ARCHITECTURE,,}' not in text
    assert '${arch_primary^^}' not in text
    assert '${arch_secondary^^}' not in text
    assert 'case "${1,,}" in' not in text
    assert '${drive^^}' not in text
    assert '${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE,,}' not in text
    assert '${#missing_paths[@]}' not in text
    assert 'if (( $(array_count timeout_prefix) > 0 )); then' in text
