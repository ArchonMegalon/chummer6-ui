from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")


def test_native_bootstrap_parses_explicit_smoke_target_and_exits_immediately() -> None:
    source = (REPO_ROOT / "scripts/windows-bootstrap/installer.nsi").read_text(
        encoding="utf-8"
    )

    assert '${GetOptions} "$CommandLine" "--smoke-install=" $SmokeInstallPath' in source
    assert '${GetOptions} "$CommandLine" "/smoke-install=" $SmokeInstallPath' in source
    assert 'StrCpy $INSTDIR $SmokeInstallPath' in source
    assert (
        '${If} $IsSmokeInstall == "1"\n'
        "    Call CloseTrace\n"
        "    Quit\n"
        "  ${EndIf}"
    ) in source


def test_smoke_runner_uses_one_cross_generation_target_switch() -> None:
    source = (REPO_ROOT / "scripts/run-desktop-startup-smoke.sh").read_text(
        encoding="utf-8"
    )

    assert '"/smoke-install=$native_install_root"' in source
    assert '"--smoke-install=$native_install_root"' not in source
    assert "Passing both spellings at once is not safe" in source


def test_managed_installer_accepts_separate_and_equals_delimited_targets() -> None:
    source = (REPO_ROOT / "Chummer.Desktop.Installer/Program.cs").read_text(
        encoding="utf-8"
    )

    assert "ResolveSmokeInstallTarget(args)" in source
    assert "string.Equals(firstArgument, SmokeInstallSwitch" in source
    assert "string equalsPrefix = SmokeInstallSwitch + \"=\";" in source
    assert "LegacySmokeInstallSwitch = \"/smoke-install\"" in source
    assert "string legacyEqualsPrefix = LegacySmokeInstallSwitch + \"=\";" in source
