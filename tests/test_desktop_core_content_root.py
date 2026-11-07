"""Source guards for application-bound runtime content and development discovery.

Behavioral integrity tests live in CharacterCreationCoreContentBundleTests;
these lightweight checks retain the Avalonia wiring and pinned source identities.
"""
from pathlib import Path
import unittest

import re

REPO = Path(__file__).resolve().parents[1]
RESOLVER = REPO / "Chummer.Avalonia" / "DesktopCoreContentRootResolver.cs"
APP = REPO / "Chummer.Avalonia" / "App.axaml.cs"

RUNTIME_COMMIT = "c28c87a1abb6e72f8a3db45a2a2f9ae578fd103c"
RECIPE_COMMIT = "167504363779b38b033ab975e88da76e880abb96"


class DesktopCoreContentRootResolverContractTests(unittest.TestCase):
    def setUp(self):
        self.resolver = RESOLVER.read_text(encoding="utf-8")
        self.app = APP.read_text(encoding="utf-8")
        self.discovery = (REPO / "Chummer.Presentation/Content/CoreContentRootDiscovery.cs").read_text()
        self.bundle = (REPO / "Chummer.Presentation/Content/CoreContentBundle.cs").read_text()

    def test_resolver_file_exists(self):
        self.assertTrue(RESOLVER.exists(), "DesktopCoreContentRootResolver.cs must exist in Chummer.Avalonia")

    def test_packaged_identity_precedes_development_overrides(self):
        self.assertIn('"ChummerCoreContentManifestSha256"', self.resolver)
        self.assertLess(self.discovery.index("CoreContentBundle.Validate(bundle"), self.discovery.index("readEnvironmentVariable(EnvironmentVariable)"))

    def test_development_discovery_retains_repo_and_sibling(self):
        self.assertIn('Path.GetFullPath(repoRoot)', self.discovery)
        self.assertIn('Path.Combine(repoRoot, "..", "chummer6-core")', self.discovery)

    def test_bundle_requires_rules_and_file_integrity(self):
        self.assertIn('"settings-all-sources.xml"', self.bundle)
        self.assertIn('"lifemodules.xml"', self.bundle)
        self.assertIn('SHA256.HashData(stream)', self.bundle)
        self.assertIn('expected.SetEquals(actual)', self.bundle)

    def test_resolver_validates_pinned_core_commits(self):
        self.assertIn(RUNTIME_COMMIT, self.discovery)
        self.assertIn(RECIPE_COMMIT, self.discovery)
        self.assertIn('PinnedCommits.Contains', self.discovery)

    def test_invalid_bundle_is_not_a_git_fallback(self):
        self.assertIn('throw new InvalidOperationException(error)', self.discovery)
        self.assertIn('return failureDetail = CoreContentBundle.Validate', self.discovery)

    def test_wiring_uses_proven_core_configuration_when_resolved(self):
        # The probe-proven working configuration: the Core checkout as BOTH
        # AddChummerLocalRuntimeClient directory parameters.
        self.assertIn("services.AddChummerLocalRuntimeClient(coreContentRoot, coreContentRoot, \"avalonia\")", self.app)

    def test_wiring_preserves_legacy_fallback_when_no_core_checkout(self):
        self.assertIn('services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot, "avalonia")', self.app)
        self.assertLess(
            self.app.index("TryResolve"),
            self.app.index('services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot, "avalonia")'),
        )

    def test_pinned_commits_match_release_authority(self):
        verifier = REPO / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
        self.assertTrue(verifier.exists(), "release authority script missing")
        authority = verifier.read_text(encoding="utf-8")
        self.assertIn(RUNTIME_COMMIT, authority)
        self.assertIn(RECIPE_COMMIT, authority)


if __name__ == "__main__":
    unittest.main()
