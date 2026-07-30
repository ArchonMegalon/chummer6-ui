from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "normalize-public-download-modes.sh"


def run_normalizer(downloads_root: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["bash", str(SCRIPT), str(downloads_root)],
        check=False,
        capture_output=True,
        text=True,
    )


def test_public_download_normalizer_makes_files_nonroot_readable(
    tmp_path: Path,
) -> None:
    nested = tmp_path / "files"
    nested.mkdir(mode=0o700)
    manifest = tmp_path / "releases.json"
    artifact = nested / "installer.bin"
    launcher = nested / "launcher.sh"
    manifest.write_text("{}", encoding="utf-8")
    artifact.write_bytes(b"installer")
    launcher.write_text("#!/bin/sh\n", encoding="utf-8")
    manifest.chmod(0o600)
    artifact.chmod(0o600)
    launcher.chmod(0o700)

    result = run_normalizer(tmp_path)

    assert result.returncode == 0, result.stderr
    assert tmp_path.stat().st_mode & 0o777 == 0o755
    assert nested.stat().st_mode & 0o777 == 0o755
    assert manifest.stat().st_mode & 0o777 == 0o644
    assert artifact.stat().st_mode & 0o777 == 0o644
    assert launcher.stat().st_mode & 0o777 == 0o755


def test_public_download_normalizer_rejects_symlinks(tmp_path: Path) -> None:
    target = tmp_path / "target"
    target.write_text("outside", encoding="utf-8")
    (tmp_path / "linked").symlink_to(target)

    result = run_normalizer(tmp_path)

    assert result.returncode == 2
    assert "symlinks are not accepted" in result.stderr
