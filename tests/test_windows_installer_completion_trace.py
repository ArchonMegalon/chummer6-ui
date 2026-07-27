from __future__ import annotations

import os
import shutil
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
VERIFIER = ROOT / "scripts" / "verify-windows-installer-completion-trace.py"
STARTUP_SMOKE = ROOT / "scripts" / "run-desktop-startup-smoke.sh"
PULL_REQUEST_CI = ROOT / ".github" / "workflows" / "pull-request-ci.yml"
TRACE_NAME = "chummer-desktop-installer-progress.log"
INSTALL_ROOT = r"D:\a\_temp\chummerwinsmoke-current"


def run_verifier(*arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["python3", str(VERIFIER), *arguments],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
    )


def valid_trace_lines() -> list[str]:
    return [
        "# Chummer installer trace",
        r"Bootstrap temp root: D:\a\_temp\Chummer6\installer-temp",
        f"Smoke install target: {INSTALL_ROOT}",
        "Payload acquisition mode: download",
        "Downloading application files",
        "Verifying payload size",
        "Verifying payload checksum",
        "Extracting application files",
        "Install complete",
    ]


@pytest.mark.parametrize(
    "encoding",
    ("utf-8", "utf-8-sig", "utf-16", "utf-16-le", "utf-16-be"),
)
def test_completion_trace_accepts_one_current_ordered_marker_set(
    tmp_path: Path,
    encoding: str,
) -> None:
    trace = tmp_path / TRACE_NAME
    trace.write_text("\r\n".join(valid_trace_lines()) + "\r\n", encoding=encoding)

    result = run_verifier(
        "verify",
        "--trace-path",
        str(trace),
        "--expected-install-root",
        INSTALL_ROOT,
    )

    assert result.returncode == 0, result.stderr
    assert result.stdout == ""
    assert result.stderr == ""


@pytest.mark.parametrize(
    ("mutation", "expected_error"),
    (
        (
            lambda lines: [line for line in lines if line != "# Chummer installer trace"],
            "exactly one current-run marker set",
        ),
        (
            lambda lines: [
                (
                    r"Smoke install target: D:\a\_temp\chummerwinsmoke-stale"
                    if line.startswith("Smoke install target: ")
                    else line
                )
                for line in lines
            ],
            "exactly one current-run marker set",
        ),
        (
            lambda lines: lines + ["Install complete"],
            "exactly one current-run marker set",
        ),
        (
            lambda lines: [
                line for line in lines if line != "Extracting application files"
            ],
            "exactly one current-run marker set",
        ),
        (
            lambda lines: [
                (
                    "Install complete but still extracting"
                    if line == "Install complete"
                    else line
                )
                for line in lines
            ],
            "exactly one current-run marker set",
        ),
        (
            lambda lines: (
                lines[: lines.index("Extracting application files")]
                + ["Install complete", "Extracting application files"]
                + lines[lines.index("Install complete") + 1 :]
            ),
            "out of order",
        ),
    ),
    ids=(
        "missing-header",
        "stale-install-root",
        "duplicate-completion",
        "missing-extraction",
        "completion-substring",
        "completion-before-extraction",
    ),
)
def test_completion_trace_rejects_stale_partial_or_ambiguous_markers(
    tmp_path: Path,
    mutation,
    expected_error: str,
) -> None:
    trace = tmp_path / TRACE_NAME
    trace.write_text("\n".join(mutation(valid_trace_lines())) + "\n", encoding="utf-8")

    result = run_verifier(
        "verify",
        "--trace-path",
        str(trace),
        "--expected-install-root",
        INSTALL_ROOT,
    )

    assert result.returncode == 1
    assert expected_error in result.stderr
    assert INSTALL_ROOT not in result.stderr
    assert str(tmp_path) not in result.stderr


@pytest.mark.parametrize(
    "different_install_root",
    (
        INSTALL_ROOT.lower(),
        INSTALL_ROOT.replace("\\", "/"),
        INSTALL_ROOT + "\\",
    ),
    ids=("different-case", "different-separators", "trailing-separator"),
)
def test_completion_trace_install_root_binding_is_byte_exact(
    tmp_path: Path,
    different_install_root: str,
) -> None:
    trace = tmp_path / TRACE_NAME
    trace.write_text("\n".join(valid_trace_lines()) + "\n", encoding="utf-8")

    result = run_verifier(
        "verify",
        "--trace-path",
        str(trace),
        "--expected-install-root",
        different_install_root,
    )

    assert result.returncode == 1
    assert "exactly one current-run marker set" in result.stderr
    assert different_install_root not in result.stderr


def test_completion_trace_reset_removes_stale_regular_file(tmp_path: Path) -> None:
    trace = tmp_path / TRACE_NAME
    trace.write_text("\n".join(valid_trace_lines()) + "\n", encoding="utf-8")

    result = run_verifier("reset", "--trace-path", str(trace))

    assert result.returncode == 0, result.stderr
    assert not trace.exists()


def test_completion_trace_reset_refuses_non_trace_target_and_symlink(
    tmp_path: Path,
) -> None:
    unrelated = tmp_path / "unrelated.txt"
    unrelated.write_text("preserve", encoding="utf-8")
    wrong_name = run_verifier("reset", "--trace-path", str(unrelated))
    assert wrong_name.returncode == 1
    assert unrelated.read_text(encoding="utf-8") == "preserve"

    symlink_target = tmp_path / "target.txt"
    symlink_target.write_text("preserve", encoding="utf-8")
    trace_link = tmp_path / TRACE_NAME
    try:
        trace_link.symlink_to(symlink_target)
    except OSError:
        pytest.skip("symlinks are unavailable on this host")
    symlink = run_verifier("reset", "--trace-path", str(trace_link))
    assert symlink.returncode == 1
    assert trace_link.is_symlink()
    assert symlink_target.read_text(encoding="utf-8") == "preserve"

    verify_symlink = run_verifier(
        "verify",
        "--trace-path",
        str(trace_link),
        "--expected-install-root",
        INSTALL_ROOT,
    )
    assert verify_symlink.returncode == 1
    assert "not a regular file" in verify_symlink.stderr


def test_completion_trace_verification_has_a_fixed_read_bound(
    tmp_path: Path,
) -> None:
    trace = tmp_path / TRACE_NAME
    trace.write_bytes(b"x" * (1024 * 1024 + 1))

    result = run_verifier(
        "verify",
        "--trace-path",
        str(trace),
        "--expected-install-root",
        INSTALL_ROOT,
    )

    assert result.returncode == 1
    assert "exceeds the fixed size bound" in result.stderr


@pytest.mark.skipif(
    shutil.which("pwsh") is None,
    reason="PowerShell is unavailable on this host",
)
def test_powershell_bash_python_preserve_windows_trace_and_install_paths(
    tmp_path: Path,
) -> None:
    probe = tmp_path / "path_transport_probe.py"
    probe.write_text(
        """
import os
import sys

trace = os.environ["CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH"]
expected_trace = os.environ["CHUMMER_TRACE_EXPECTED"]
expected_install_root = os.environ["CHUMMER_TRACE_EXPECTED_INSTALL_ROOT"]
raise SystemExit(
    0
    if trace == expected_trace
    and sys.argv[1] == expected_trace
    and sys.argv[2] == expected_install_root
    else 1
)
""".lstrip(),
        encoding="utf-8",
    )
    expected_trace = (
        r"D:\a\_temp\Chummer6\installer-temp"
        r"\chummer-desktop-installer-progress.log"
    )
    expected_install_root = r"D:\a\_temp\Chummer Smoke\MiXeD-win-x64"
    environment = os.environ.copy()
    environment.update(
        {
            "CHUMMER_TRACE_EXPECTED": expected_trace,
            "CHUMMER_TRACE_EXPECTED_INSTALL_ROOT": expected_install_root,
            "CHUMMER_TRACE_PROBE_SCRIPT": str(probe),
        }
    )
    result = subprocess.run(
        [
            "pwsh",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            (
                "$ErrorActionPreference = 'Stop'; "
                "$env:CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH = "
                "$env:CHUMMER_TRACE_EXPECTED; "
                "& bash -c 'python3 \"$CHUMMER_TRACE_PROBE_SCRIPT\" "
                "\"$CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH\" "
                "\"$CHUMMER_TRACE_EXPECTED_INSTALL_ROOT\"'; "
                "exit $LASTEXITCODE"
            ),
        ],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        env=environment,
    )

    assert result.returncode == 0, result.stdout + result.stderr


def assert_startup_smoke_completion_gate(source: str) -> None:
    runner_start = source.index("run_windows_smoke() {")
    runner_end = source.index("\nseed_dpkg_admin_dir()", runner_start)
    runner = source[runner_start:runner_end]

    reset = (
        '"$SCRIPT_DIR/verify-windows-installer-completion-trace.py" reset'
    )
    verify = (
        '"$SCRIPT_DIR/verify-windows-installer-completion-trace.py" verify'
    )
    first_installer_run = runner.index('run_windows_binary "$ARTIFACT_PATH"')
    last_installer_run = runner.rindex('run_windows_binary "$ARTIFACT_PATH"')
    reset_index = runner.index(reset)
    verify_index = runner.index(verify)
    launch_index = runner.index(
        'run_head_smoke "$INSTALL_ROOT/$launch_relative_path"'
    )

    assert (
        'installer_completion_trace_path="${'
        'CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH:-}"'
    ) in runner
    assert 'native_install_root="$(to_native_path "$INSTALL_ROOT")"' in runner
    assert (
        'local -a installer_args=("/smoke-install=$native_install_root")'
        in runner
    )
    assert runner.count(reset) == 1
    assert runner.count(verify) == 2
    assert runner.count(
        '--expected-install-root "$native_install_root"'
    ) == 2
    assert (
        'if [[ "$HOST_CLASS" == "github-hosted-windows-latest-native" \\\n'
        '    && -z "$installer_completion_trace_path" ]]; then'
    ) in runner
    assert "required_installer_completion_stable_observations=2" in runner
    assert "local installer_completion_stable_observations=0" in runner
    assert runner.count(
        "\n        installer_completion_stable_observations="
    ) == 2
    assert "install_ready_deadline=$((SECONDS + install_ready_timeout_seconds))" in runner
    assert runner.count("SECONDS >= install_ready_deadline") == 2
    assert "local deadline=$((SECONDS + install_ready_timeout_seconds))" not in runner
    assert "sleep 2" not in runner
    assert reset_index < first_installer_run <= last_installer_run < verify_index < launch_index
    assert (
        "did not emit one ordered current-run completion marker set before timeout"
        in runner
    )
    assert "hostpolicy.dll" not in runner


def test_startup_smoke_waits_for_invocation_bound_installer_completion() -> None:
    assert_startup_smoke_completion_gate(
        STARTUP_SMOKE.read_text(encoding="utf-8")
    )


@pytest.mark.parametrize(
    ("needle", "replacement"),
    (
        (
            '"$SCRIPT_DIR/verify-windows-installer-completion-trace.py" reset',
            '"$SCRIPT_DIR/verify-windows-installer-completion-trace.py" verify',
        ),
        (
            "--expected-install-root \"$native_install_root\"",
            "--expected-install-root stale",
        ),
        (
            "if (( SECONDS >= install_ready_deadline )); then",
            "if false; then",
        ),
        (
            "local install_ready_deadline=$((SECONDS + install_ready_timeout_seconds))",
            "local install_ready_deadline=0",
        ),
        (
            'installer_completion_trace_path="${CHUMMER_WINDOWS_STARTUP_SMOKE_INSTALLER_TRACE_PATH:-}"',
            'installer_completion_trace_path=""',
        ),
        (
            'required_installer_completion_stable_observations=2',
            'required_installer_completion_stable_observations=1',
        ),
        (
            '"$HOST_CLASS" == "github-hosted-windows-latest-native"',
            '"$HOST_CLASS" == "never"',
        ),
    ),
    ids=(
        "no-freshness-reset",
        "unbound-install-root",
        "unbounded-wait",
        "invalid-deadline",
        "discarded-trace-binding",
        "single-completion-observation",
        "hosted-trace-optional",
    ),
)
def test_startup_smoke_completion_contract_rejects_unsafe_mutations(
    needle: str,
    replacement: str,
) -> None:
    source = STARTUP_SMOKE.read_text(encoding="utf-8")
    assert needle in source
    mutated = source.replace(needle, replacement, 1)

    with pytest.raises((AssertionError, ValueError)):
        assert_startup_smoke_completion_gate(mutated)


def test_completion_trace_verifier_is_executable() -> None:
    assert os.access(VERIFIER, os.X_OK)


def test_completion_trace_contract_runs_in_protected_pull_request_ci() -> None:
    workflow = PULL_REQUEST_CI.read_text(encoding="utf-8")
    assert workflow.count("tests/test_windows_installer_completion_trace.py") == 1
