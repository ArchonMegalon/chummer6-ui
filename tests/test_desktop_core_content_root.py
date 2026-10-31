"""Guard the Avalonia Core-content-root resolution contract.

Authoritative character creation requires the pinned Core content checkout as
the headless core's content root; the UI repository's partial Chummer/data
mirror is not sufficient (creation-bootstrap-source-context-unavailable).

This is a source-contract guard in the tests/ convention (this is not package
qualification): it parses the resolver and its wiring from source, so the
discovery order, pinned-commit identity, fail-fast semantics, and the
proven-working (coreRoot, coreRoot) configuration cannot silently regress.
"""
from pathlib import Path
import unittest

import re

REPO = Path(__file__).resolve().parents[1]
RESOLVER = REPO / "Chummer.Avalonia" / "DesktopCoreContentRootResolver.cs"
APP = REPO / "Chummer.Avalonia" / "App.axaml.cs"

RUNTIME_COMMIT = "8706f38a7b5c29308fe007baa101038f06f61906"
RECIPE_COMMIT = "c29b27f3f6791189cade6c37621773ddd7339ecd"


class DesktopCoreContentRootResolverContractTests(unittest.TestCase):
    def setUp(self):
        self.resolver = RESOLVER.read_text(encoding="utf-8")
        self.app = APP.read_text(encoding="utf-8")

    def test_resolver_file_exists(self):
        self.assertTrue(RESOLVER.exists(), "DesktopCoreContentRootResolver.cs must exist in Chummer.Avalonia")

    def test_resolver_discovers_explicit_environment_override_first(self):
        self.assertIn('"CHUMMER_CORE_CONTENT_ROOT"', self.resolver)
        self.assertIn("GetEnvironmentVariable", self.resolver)

    def test_resolver_discovers_repo_root_then_core_sibling(self):
        self.assertIn('Path.Combine(repoRoot, "..", "chummer6-core")', self.resolver)
        # repo root candidate precedes the sibling candidate
        self.assertLess(
            self.resolver.index("yield return Path.GetFullPath(repoRoot);"),
            self.resolver.index('Path.Combine(repoRoot, "..", "chummer6-core")'),
        )

    def test_resolver_requires_settings_all_sources_anchor(self):
        self.assertIn('"settings-all-sources.xml"', self.resolver)

    def test_resolver_validates_pinned_core_commits(self):
        self.assertIn(RUNTIME_COMMIT, self.resolver)
        self.assertIn(RECIPE_COMMIT, self.resolver)
        self.assertIn("not a pinned Core content commit", self.resolver)

    def test_resolver_fails_fast_on_invalid_explicit_root(self):
        self.assertIn("InvalidOperationException", self.resolver)
        self.assertIn("was rejected", self.resolver)

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
