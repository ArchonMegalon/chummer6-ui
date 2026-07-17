from __future__ import annotations

import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
GATE_SCRIPT = REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh"
RUNBOOK = REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md"


def run_capacity_preflight(
    tmp_path: Path,
    *,
    available_gib: int,
    minimum_gib: int | None = None,
    allow_below_source_floor: bool = False,
) -> tuple[subprocess.CompletedProcess[str], Path]:
    fake_bin = tmp_path / "fake-bin"
    fake_bin.mkdir()
    fake_df = fake_bin / "df"
    fake_df.write_text(
        """#!/usr/bin/env bash
set -euo pipefail
printf 'Filesystem 1024-blocks Used Available Capacity Mounted on\\n'
printf 'fakefs 999999999 0 %s 0%% /fake\\n' "${FAKE_DF_AVAILABLE_KIB:?}"
""",
        encoding="utf-8",
    )
    fake_df.chmod(0o755)

    output_root = tmp_path / "gate-output"
    env = os.environ.copy()
    env.update(
        {
            "PATH": f"{fake_bin}:{env.get('PATH', '')}",
            "FAKE_DF_AVAILABLE_KIB": str(available_gib * 1024 * 1024),
            "CHUMMER_HUB_REGISTRY_ROOT": str(tmp_path / "missing-registry"),
            "CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH": str(tmp_path / "missing-run-services-channel.json"),
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_RELEASE_CHANNEL_PATH": str(tmp_path / "missing-release-channel.json"),
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_APP_KEY": "avalonia",
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_RID": "linux-x64",
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_OUTPUT_ROOT": str(output_root),
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_WRITABLE_STATE_ROOT": str(tmp_path / "writable-state"),
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_NUGET_PACKAGES": str(tmp_path / "nuget-packages"),
            "CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH": str(tmp_path / "proof.json"),
            "CHUMMER_LINUX_DESKTOP_EXIT_GATE_CAPACITY_PREFLIGHT_ONLY": "1",
        }
    )
    if minimum_gib is not None:
        env["CHUMMER_LINUX_DESKTOP_EXIT_GATE_MIN_FREE_GIB"] = str(minimum_gib)
    else:
        env.pop("CHUMMER_LINUX_DESKTOP_EXIT_GATE_MIN_FREE_GIB", None)
    if allow_below_source_floor:
        env["CHUMMER_LINUX_DESKTOP_EXIT_GATE_ALLOW_BELOW_SOURCE_BUILD_DISK_FLOOR"] = "1"
    else:
        env.pop("CHUMMER_LINUX_DESKTOP_EXIT_GATE_ALLOW_BELOW_SOURCE_BUILD_DISK_FLOOR", None)

    result = subprocess.run(
        ["bash", str(GATE_SCRIPT)],
        cwd=REPO_ROOT,
        env=env,
        text=True,
        capture_output=True,
        check=False,
        timeout=30,
    )
    return result, output_root


def test_capacity_guard_runs_before_any_exit_gate_run_directory_is_created() -> None:
    script = GATE_SCRIPT.read_text(encoding="utf-8")

    guard_call = script.index("check_linux_desktop_capacity || exit 1")
    run_root_creation = script.index('mkdir -p "$OUTPUT_BASE_ROOT"')
    source_snapshot = script.index('CURRENT_STAGE="source_snapshot"')
    restore = script.index('CURRENT_STAGE="restore_publish_graph"')

    assert guard_call < run_root_creation < source_snapshot < restore


def test_capacity_preflight_defaults_to_documented_25_gib_floor_without_mutation(tmp_path: Path) -> None:
    result, output_root = run_capacity_preflight(tmp_path, available_gib=25)

    assert result.returncode == 0, result.stderr
    assert "linux-desktop-capacity-preflight:ok required_gib=25 source_build_floor_gib=25" in result.stdout
    assert not output_root.exists()
    assert not (tmp_path / "proof.json").exists()


def test_capacity_preflight_fails_fast_with_actionable_low_space_diagnostics(tmp_path: Path) -> None:
    result, output_root = run_capacity_preflight(tmp_path, available_gib=24)

    assert result.returncode == 1
    assert "capacity preflight failed for source snapshot workspace" in result.stderr
    assert "25 GiB free is required, but only 24 GiB is available" in result.stderr
    assert "Free disk space or move the affected output/cache path" in result.stderr
    assert not output_root.exists()


def test_capacity_threshold_cannot_drop_below_source_floor_implicitly(tmp_path: Path) -> None:
    result, output_root = run_capacity_preflight(tmp_path, available_gib=24, minimum_gib=12)

    assert result.returncode == 2
    assert "below the documented 25 GiB source-build floor" in result.stderr
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_ALLOW_BELOW_SOURCE_BUILD_DISK_FLOOR=1" in result.stderr
    assert not output_root.exists()


def test_capacity_threshold_can_drop_only_with_explicit_acknowledgement(tmp_path: Path) -> None:
    result, output_root = run_capacity_preflight(
        tmp_path,
        available_gib=12,
        minimum_gib=12,
        allow_below_source_floor=True,
    )

    assert result.returncode == 0, result.stderr
    assert "linux-desktop-capacity-preflight:ok required_gib=12 source_build_floor_gib=25" in result.stdout
    assert not output_root.exists()


def test_capacity_threshold_upward_override_is_enforced(tmp_path: Path) -> None:
    result, output_root = run_capacity_preflight(tmp_path, available_gib=29, minimum_gib=30)

    assert result.returncode == 1
    assert "30 GiB free is required, but only 29 GiB is available" in result.stderr
    assert not output_root.exists()


def test_capacity_preflight_operator_contract_is_documented() -> None:
    runbook = RUNBOOK.read_text(encoding="utf-8")

    assert "Linux exit-gate capacity preflight" in runbook
    assert "hard source-build floor and default threshold are **25 GiB**" in runbook
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_MIN_FREE_GIB=50" in runbook
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_CAPACITY_PREFLIGHT_ONLY=1" in runbook
    assert "read-only, preflight-only check" in runbook
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_ALLOW_BELOW_SOURCE_BUILD_DISK_FLOOR=1" in runbook
    assert "acknowledgement is a warning boundary" in runbook
