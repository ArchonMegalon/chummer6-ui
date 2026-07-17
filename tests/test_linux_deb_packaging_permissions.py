from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_linux_deb_staging_normalizes_control_and_payload_permissions() -> None:
    script = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")

    normalize_dirs = 'find "$stage_root" -type d -exec chmod 0755 {} +'
    normalize_files = 'chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"'
    build_deb = 'dpkg-deb --root-owner-group --build "$stage_root" "$DIST_DIR/$installer_name"'

    assert normalize_dirs in script
    assert normalize_files in script
    assert script.index(normalize_dirs) < script.index(build_deb)
    assert script.index(normalize_files) < script.index(build_deb)
