"""Focused privacy and acceptance tests for the WIN-004 native journey.

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
import re
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


# Installer acceptance predicates share this existing CI suite so the signed
# workflow/fixture binding remains unchanged. These are not a native journey.
ROOT = REPO_ROOT
HELPER = ROOT / "scripts/win004-acceptance.ps1"
PWSH = shutil.which("pwsh") or shutil.which("powershell")

def quote(value: object) -> str:
    return "'" + str(value).replace("'", "''") + "'"


def run_ps(body: str):
    if not PWSH:
        pytest.skip("requires PowerShell")
    result = subprocess.run(
        [PWSH, "-NoProfile", "-NonInteractive", "-Command",
         "$ErrorActionPreference = 'Stop'\n. " + quote(HELPER) + "\n" + body],
        text=True, capture_output=True, timeout=30,
    )
    assert result.returncode == 0, result.stderr
    return json.loads(result.stdout.lstrip("\ufeff"))


def test_relaunch_before_late_registry_observation_is_accepted_but_old_process_is_not():
    # Apply at 0, old head starts at 1, new head at 2, registry observed at 10.
    # The previous observation-minus-five predicate rejected this valid relaunch.
    result = run_ps("""
    $apply = [datetime]'2026-11-03T10:00:00Z'
    $old = $apply.AddSeconds(1)
    $new = $apply.AddSeconds(2)
    $observed = $apply.AddSeconds(10)
    @{
        accepted = (Test-UpdatedProcessBirth 101 $new 100 $old $apply)
        sameProcess = (Test-UpdatedProcessBirth 100 $new 100 $old $apply)
        olderProcess = (Test-UpdatedProcessBirth 99 $apply.AddSeconds(-1) 100 $old $apply)
        sameBirth = (Test-UpdatedProcessBirth 101 $old 100 $old $apply)
        oldPredicate = ($new -ge $observed.AddSeconds(-5))
    } | ConvertTo-Json
    """)
    assert result == dict(accepted=True, sameProcess=False, olderProcess=False,
                          sameBirth=False, oldPredicate=False)


def test_conflicting_environment_cannot_redirect_snapshot_or_hide_changed_runner(tmp_path):
    result = run_ps(f"""
    $root = {quote(tmp_path)}
    $unrelated = Join-Path $root 'unrelated'
    $chosen = Join-Path $root 'chosen'
    New-Item -ItemType Directory -Path $unrelated, (Join-Path $chosen 'Chummer6/state/workspaces') | Out-Null
    Set-Content -LiteralPath (Join-Path $unrelated 'personal.json') -Value 'unchanged'
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.EnvironmentVariables['CHUMMER_STATE_PATH'] = $unrelated
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_STATE_ROOT'] = $unrelated
    $psi.EnvironmentVariables['CHUMMER_CLIENT_MODE'] = 'http'
    $psi.EnvironmentVariables['CHUMMER_API_KEY'] = 'harmless-sentinel'
    Set-JourneyAppEnvironment -StartInfo $psi -StateRoot $chosen
    $actual = Join-Path $psi.EnvironmentVariables['CHUMMER_DESKTOP_STATE_ROOT'] 'Chummer6/state'
    $runner = Join-Path $actual 'workspaces/runner.json'
    Set-Content -LiteralPath $runner -Value 'saved-original'
    $before = Get-CharacterDataSnapshot $actual
    $unrelatedBefore = Get-CharacterDataSnapshot $unrelated
    $same = Test-CharacterDataPreservation $before (Get-CharacterDataSnapshot $actual) $actual
    Set-Content -LiteralPath $runner -Value 'saved-modified'
    $changed = Test-CharacterDataPreservation $before (Get-CharacterDataSnapshot $actual) $actual
    Remove-Item -LiteralPath $runner
    $lost = Test-CharacterDataPreservation $before (Get-CharacterDataSnapshot $actual) $actual
    $other = Test-CharacterDataPreservation $unrelatedBefore (Get-CharacterDataSnapshot $unrelated) $unrelated
    @{{ same=$same.status; changed=$changed.status; lost=$lost.status; unrelated=$other.status;
        correctRoot=($actual -eq (Join-Path $chosen 'Chummer6/state'));
        storeOverride=$psi.EnvironmentVariables.ContainsKey('CHUMMER_STATE_PATH');
        remoteClient=$psi.EnvironmentVariables.ContainsKey('CHUMMER_CLIENT_MODE');
        apiKey=$psi.EnvironmentVariables.ContainsKey('CHUMMER_API_KEY') }} | ConvertTo-Json
    """)
    assert result == dict(same="pass", changed="fail", lost="fail", unrelated="pass",
                          correctRoot=True, storeOverride=False, remoteClient=False, apiKey=False)


@pytest.mark.parametrize("mutation", ["", "$receipt.processId=100", "$receipt.workspaceId='b'*32",
    "$receipt.profileSha256='b'*64", "$receipt.name='Different runner'",
    "$receipt.stateRoot=Join-Path $root 'wrong'", "$receipt.saved=$false",
    "$receipt.dirty=$true", "$fileHash='changed'", "$receipt.succeeded=$false"])
def test_reopen_requires_saved_identity_profile_bytes_and_actual_process(tmp_path, mutation):
    result = run_ps(f"""
    $root = {quote(tmp_path)}
    $saved = @{{processId=100; workspaceId=('a'*32); profileSha256=('a'*64); name='Fixture'}}
    $receipt = @{{succeeded=$true; saved=$true; dirty=$false; phase='reopen';
        processId=101; workspaceId=$saved.workspaceId; profileSha256=$saved.profileSha256;
        name=$saved.name; stateRoot=$root}}
    $fileHash = 'original'
    {mutation}
    $accepted = $true
    try {{ Assert-JourneyPersistenceReceipt $receipt reopen 101 $root $saved $fileHash original }}
    catch {{ $accepted = $false }}
    @{{accepted=$accepted}} | ConvertTo-Json
    """)
    assert result["accepted"] is (not mutation)


def test_journey_parses_and_every_app_launch_is_scoped():
    result = run_ps(f"""
    $tokens=$null; $errors=$null
    $null=[System.Management.Automation.Language.Parser]::ParseFile({quote(JOURNEY)}, [ref]$tokens, [ref]$errors)
    @{{errors=@($errors | ForEach-Object {{ $_.Message }})}} | ConvertTo-Json -Depth 3
    """)
    assert result["errors"] == []
    text = JOURNEY.read_text()
    for variable in ("psi", "psiR", "psi2", "probeInfo"):
        launches = re.findall(rf"\${variable}\.FileName = (\$InstalledExe|\$infoUpd.ExePath|\$Exe)\n(.*?)\[System.Diagnostics.Process\]::Start\(\${variable}\)", text, re.S)
        if variable == "psi":
            launches = [row for row in launches if row[0] != "$Exe"]  # generic build/installer helper
        assert launches
        assert all(f"Set-JourneyAppEnvironment -StartInfo ${variable} -StateRoot $JourneyStateRoot" in body for _, body in launches)
    assert "GetFolderPath('LocalApplicationData')" not in text
    assert "$updatedAtUtc.AddSeconds(-5)" not in text
    assert "$newProcExeSha -ine $ExpectedV2ExeSha" in text  # wrong payload still rejected
    assert "if (-not $p.HasExited)" in text
    assert "-Phase save -Label save" in text
    assert "-Phase reopen -Label reopen-after-update" in text
    assert "-Phase reopen -Label reopen-after-recovery" in text
    assert text.index("$PreUpdateVersion =") < text.index("$ApplyStartedUtc =")
