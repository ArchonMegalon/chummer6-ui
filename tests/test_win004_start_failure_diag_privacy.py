"""Focused privacy tests for WIN-004 startup-failure diagnostics.

Re-audit handoff item 1 (2026-11-02, immediate P1): a failing native start
must never persist the child process environment. The old handler enumerated
``$psi.EnvironmentVariables`` name/value pairs into ``start-failure-env.txt``,
which could place workspace credentials into plaintext diagnostic evidence.

These tests exercise the replacement collector dynamically (through a real
pwsh host where available) and pin the journey script's failure path so the
full-enumeration primitive cannot return.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[1]
JOURNEY = REPO_ROOT / "scripts" / "win004-installer-update-recovery-journey.ps1"
COLLECTOR = REPO_ROOT / "scripts" / "win004-start-failure-diagnostics.ps1"

# Harmless sentinel standing in for a credential. It must never reach any
# generated diagnostic artifact.
SENTINEL_NAME = "GH_TOKEN"
SENTINEL_VALUE = "sentinel-credential-do-not-leak-8f3a1c92bd"

VALUE_KEYS = {
    "CHUMMER_DESKTOP_UPDATE_ENABLED": "0",
    "CHUMMER_DESKTOP_UPDATE_MODE": "off",
    "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY": "true",
}
PRESENCE_KEYS = {
    "CHUMMER_CORE_CONTENT_ROOT": r"D:\sources\chummer\chummer6-core",
    "CHUMMER_DESKTOP_UPDATE_MANIFEST": r"D:\harness\manifest-v2.json",
    "DOTNET_ROOT": r"C:\harness\sdk\10.0.103",
}


def _pwsh_available() -> bool:
    return shutil.which("pwsh") is not None or shutil.which("powershell") is not None


def _pwsh() -> str:
    return shutil.which("pwsh") or shutil.which("powershell") or ""


def _build_env_table() -> str:
    """Build the PowerShell literal for a StringDictionary-like environment."""
    entries = dict(VALUE_KEYS)
    entries.update(PRESENCE_KEYS)
    entries[SENTINEL_NAME] = SENTINEL_VALUE
    entries["PATH"] = r"C:\Windows\system32;C:\Windows"
    entries["PROCESSOR_ARCHITECTURE"] = "AMD64"
    pairs = "; ".join(f"'{k}' = '{v}'" for k, v in entries.items())
    return "@{ " + pairs + " }"


@pytest.mark.skipif(not _pwsh_available(), reason="requires a PowerShell host")
class TestCollectorDynamic:
    """Dynamic tests: run the collector through pwsh and inspect its output."""

    def _run_collector(self, tmp_path: Path) -> tuple[Path, bytes]:
        report = tmp_path / "start-failure-environment.json"
        script = (
            "$ErrorActionPreference = 'Stop'\n"
            f". '{COLLECTOR}'\n"
            f"$env_table = { _build_env_table() }\n"
            "Write-StartFailureEnvironmentReport -Environment $env_table "
            f"-Path '{report}'\n"
            "Get-Content -Raw -LiteralPath '" + str(report) + "'\n"
        )
        proc = subprocess.run(
            [_pwsh(), "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True,
            text=True,
            timeout=120,
        )
        assert proc.returncode == 0, f"collector failed: {proc.stderr}"
        assert report.exists(), "collector did not write a report"
        return report, report.read_bytes()

    def test_sentinel_credential_value_never_appears(self, tmp_path: Path) -> None:
        report, blob = self._run_collector(tmp_path)
        # The sentinel value must not appear in any generated diagnostic
        # artifact - not just the environment report.
        for artifact in tmp_path.glob("*"):
            data = artifact.read_bytes()
            assert SENTINEL_VALUE.encode() not in data, f"{artifact.name} leaks credential value"

    def test_sentinel_credential_name_never_appears(self, tmp_path: Path) -> None:
        _, blob = self._run_collector(tmp_path)
        assert SENTINEL_NAME.encode() not in blob, "report leaks credential variable name"

    def test_safe_update_mode_values_are_recorded(self, tmp_path: Path) -> None:
        _, blob = self._run_collector(tmp_path)
        payload = json.loads(blob.decode("utf-8-sig"))
        env = payload["environment"]
        assert env["updateEnabled"] == "0"
        assert env["updateMode"] == "off"
        assert env["updateAutoApply"] == "true"

    def test_machine_paths_record_presence_not_values(self, tmp_path: Path) -> None:
        _, blob = self._run_collector(tmp_path)
        payload = json.loads(blob.decode("utf-8-sig"))
        env = payload["environment"]
        assert env["coreContentOverrideConfigured"] is True
        assert env["sdkOverrideConfigured"] is True
        assert env["updateManifestConfigured"] is True
        blob_text = blob.decode("utf-8-sig")
        for path_value in PRESENCE_KEYS.values():
            assert path_value not in blob_text, "report recorded a machine path value"

    def test_unlisted_keys_collapse_to_a_count(self, tmp_path: Path) -> None:
        _, blob = self._run_collector(tmp_path)
        payload = json.loads(blob.decode("utf-8-sig"))
        # 6 allowlisted + sentinel + PATH + PROCESSOR_ARCHITECTURE
        assert payload["unlistedEnvironmentKeyCount"] == 3

    def test_report_absent_keys_render_false_or_null(self, tmp_path: Path) -> None:
        report = tmp_path / "sparse.json"
        script = (
            "$ErrorActionPreference = 'Stop'\n"
            f". '{COLLECTOR}'\n"
            "Write-StartFailureEnvironmentReport -Environment @{ FOO = 'bar' } "
            f"-Path '{report}'\n"
        )
        proc = subprocess.run(
            [_pwsh(), "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True,
            text=True,
            timeout=120,
        )
        assert proc.returncode == 0, proc.stderr
        payload = json.loads(report.read_bytes().decode("utf-8-sig"))
        env = payload["environment"]
        assert env["updateEnabled"] is None
        assert env["coreContentOverrideConfigured"] is False
        assert payload["unlistedEnvironmentKeyCount"] == 1


class TestJourneyFailurePathContract:
    """Static pins: the journey failure path can never re-enumerate the env."""

    def test_journey_no_longer_writes_start_failure_env_txt(self) -> None:
        assert "start-failure-env.txt" not in JOURNEY.read_text(encoding="utf-8-sig")

    def test_journey_has_no_environment_enumeration_primitive(self) -> None:
        text = JOURNEY.read_text(encoding="utf-8-sig")
        # The full-enumeration dump that motivated the P1 finding.
        assert "EnvironmentVariables.GetEnumerator" not in text
        assert "Get-ChildItem env:" not in text
        assert "GetEnvironmentVariables" not in text

    def test_journey_failure_path_uses_the_collector(self) -> None:
        text = JOURNEY.read_text(encoding="utf-8-sig")
        assert "win004-start-failure-diagnostics.ps1" in text
        assert "Write-StartFailureEnvironmentReport" in text
        assert "start-failure-environment.json" in text

    def test_collector_has_no_full_enumeration_escaping(self) -> None:
        text = COLLECTOR.read_text(encoding="utf-8-sig")
        assert "GetEnvironmentVariables" not in text
        assert "Get-ChildItem env:" not in text
        # The collector may enumerate the passed-in IDictionary (its contract),
        # but must not join whole name/value pairs into a flat dump.
        assert '"$($_.Name)=$($_.Value)"' not in text

    def test_collector_allowlist_is_explicit_and_closed(self) -> None:
        text = COLLECTOR.read_text(encoding="utf-8-sig")
        for key in VALUE_KEYS:
            assert f"'{key}'" in text
        for key in PRESENCE_KEYS:
            assert f"'{key}'" in text
        # No wildcard allow entries.
        assert "*" not in text.split("$valueKeys")[1].split("$presenceKeys")[0]
