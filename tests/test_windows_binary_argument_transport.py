from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ENCODER = REPO_ROOT / "scripts" / "encode-windows-binary-arguments.py"
STARTUP_SMOKE = REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"


def encode(*values: str) -> list[str]:
    raw = b"".join(value.encode("utf-8") + b"\0" for value in values)
    completed = subprocess.run(
        [sys.executable, str(ENCODER)],
        input=raw,
        capture_output=True,
        check=True,
    )
    return json.loads(completed.stdout)


def test_encoder_preserves_exact_slash_prefixed_windows_smoke_switch() -> None:
    switch = "/smoke-install=C:\\Users\\RUNNER~1\\AppData\\Local\\Temp\\chummerwinsmoke123"

    assert encode(switch) == [switch]


def test_encoder_preserves_empty_unicode_and_multiple_arguments() -> None:
    assert encode("", "--relaunch-arg", "chummer://Gruppe/ä") == [
        "",
        "--relaunch-arg",
        "chummer://Gruppe/ä",
    ]


def test_encoder_rejects_a_non_delimited_stream() -> None:
    completed = subprocess.run(
        [sys.executable, str(ENCODER)],
        input=b"/smoke-install=C:\\Temp\\runner",
        capture_output=True,
        check=False,
    )

    assert completed.returncode == 1
    assert b"missing its final NUL delimiter" in completed.stderr


def test_windows_launcher_serializes_arguments_over_stdin_not_native_argv() -> None:
    source = STARTUP_SMOKE.read_text(encoding="utf-8")

    assert "printf '%s\\0' \"$@\"" in source
    assert '"$SCRIPT_DIR/encode-windows-binary-arguments.py"' in source
    assert '"$PYTHON_BIN" - "$@"' not in source


def test_native_msys_launcher_disables_argument_conversion_before_powershell_fallback() -> None:
    source = STARTUP_SMOKE.read_text(encoding="utf-8")
    launcher = source[
        source.index("run_windows_binary() {") : source.index("run_startup_smoke_process() {")
    ]
    native_marker = "Windows binary argument transport: native-msys-direct"
    powershell_marker = "if command -v powershell.exe"

    assert native_marker in launcher
    assert launcher.index(native_marker) < launcher.index(powershell_marker)
    assert "MSYS2_ARG_CONV_EXCL='*' MSYS_NO_PATHCONV=1" in launcher
    assert '"${windows_binary_env_prefix[@]}" "$unix_executable_path" "$@"' in launcher
