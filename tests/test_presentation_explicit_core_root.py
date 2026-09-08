"""Guard local source composition; this is not package/release qualification."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


class ExplicitCoreRootTests(unittest.TestCase):
    def test_all_optional_core_runtime_references_use_the_selected_root(self):
        root = Path(__file__).resolve().parents[1]
        expected = {
            "Chummer.Application", "Chummer.Infrastructure", "Chummer.Rulesets.Hosting",
            "Chummer.Rulesets.Sr4", "Chummer.Rulesets.Sr5", "Chummer.Rulesets.Sr6",
        }
        for project in ("Chummer.Presentation", "Chummer.Desktop.Runtime"):
            with self.subTest(project=project):
                document = ET.parse(root / project / f"{project}.csproj")
                references = document.findall(".//ProjectReference")
                for owner in expected:
                    selected = [node for node in references if node.attrib["Include"].endswith(f"/{owner}.csproj")]
                    self.assertEqual(1, len(selected), owner)
                    reference = selected[0]
                    path = f"$(ChummerCoreEngineRoot)/{owner}/{owner}.csproj"
                    self.assertEqual(path, reference.attrib["Include"])
                    self.assertEqual(f"Exists('{path}')", reference.attrib.get("Condition"))


if __name__ == "__main__":
    unittest.main()
