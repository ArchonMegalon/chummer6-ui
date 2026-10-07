"""Small asset/wiring checks; no desktop runtime or installer claim."""

import hashlib
import struct
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ICON = ROOT / "Chummer/chummer.ico"
PREVIEW = ROOT / "Chummer/chummer6-icon-preview.png"


class DesktopAppIconTests(unittest.TestCase):
    def test_assets_are_the_reviewed_mobile_troll_exports(self):
        for path, digest in (
            (ICON, "a8b0d6d38568f3c62a5dd49bade34ed8d5c708d7c0104d06fcd0e5f374ab10bd"),
            (PREVIEW, "b1ce2aeebe388f9fd945511a3faf337fdf8254fbaefe86463a7efeb505aa1e63"),
        ):
            with self.subTest(asset=path.name):
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), digest)
        png = PREVIEW.read_bytes()
        self.assertEqual(png[:8], b"\x89PNG\r\n\x1a\n")
        self.assertEqual(struct.unpack_from(">II", png, 16), (512, 512))

    def test_windows_icon_contains_valid_small_and_high_dpi_frames(self):
        data = ICON.read_bytes()
        self.assertEqual(struct.unpack_from("<HHH", data), (0, 1, 7))
        sizes = set()
        next_offset = 6 + 7 * 16
        for index in range(7):
            width, height, colors, reserved, planes, bits, length, offset = struct.unpack_from(
                "<BBBBHHII", data, 6 + index * 16
            )
            size = width or 256
            self.assertEqual(height or 256, size)
            self.assertEqual((colors, reserved, planes, bits), (0, 0, 1, 32))
            self.assertEqual(offset, next_offset)
            self.assertGreater(length, 0)
            self.assertLessEqual(offset + length, len(data))
            sizes.add(size)
            next_offset = offset + length
        self.assertEqual(sizes, {16, 24, 32, 48, 64, 128, 256})
        self.assertEqual(next_offset, len(data))

    def test_desktop_executables_and_windows_installer_share_the_icon(self):
        for project in ("Chummer.Avalonia", "Chummer.Blazor.Desktop", "Chummer.Desktop.Installer"):
            with self.subTest(project=project):
                root = ET.parse(ROOT / project / (project + ".csproj")).getroot()
                for property_name in ("ApplicationIcon", "Win32Icon"):
                    self.assertEqual(root.findtext(".//" + property_name), r"..\Chummer\chummer.ico")
                if project != "Chummer.Desktop.Installer":
                    content = root.find(".//Content[@Link='chummer.ico']")
                    self.assertIsNotNone(content)
                    self.assertEqual(content.attrib["Include"], r"..\Chummer\chummer.ico")
                    self.assertEqual(content.findtext("CopyToPublishDirectory"), "Always")

    def test_window_about_and_bootstrap_keep_the_same_asset(self):
        for window in ("MainWindow.axaml", "MainClassicWindow.axaml"):
            root = ET.parse(ROOT / "Chummer.Avalonia" / window).getroot()
            self.assertEqual(root.attrib["Icon"], "/Assets/chummer.ico")
        project = ET.parse(ROOT / "Chummer.Avalonia/Chummer.Avalonia.csproj").getroot()
        for filename in ("chummer.ico", "chummer6-icon-preview.png"):
            resource = project.find(f".//AvaloniaResource[@Link='Assets/{filename}']")
            self.assertIsNotNone(resource)
            self.assertEqual(resource.attrib["Include"], "..\\Chummer\\" + filename)
        builder = (ROOT / "scripts/build-desktop-installer.sh").read_text()
        self.assertIn('cp -f "$REPO_ROOT/Chummer/chummer.ico" "$native_bootstrap_stage_dir/chummer.ico"', builder)
        bootstrap = (ROOT / "scripts/windows-bootstrap/installer.nsi").read_text()
        self.assertIn('Icon "${CHUMMER_ICON_PATH}"', bootstrap)


if __name__ == "__main__":
    unittest.main()
