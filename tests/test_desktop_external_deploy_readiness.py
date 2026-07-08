from __future__ import annotations

import importlib.util
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = REPO_ROOT / "scripts" / "verify-desktop-external-deploy-readiness.py"
SPEC = importlib.util.spec_from_file_location("desktop_external_deploy_readiness", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise ImportError(f"Unable to load module from {MODULE_PATH}")
readiness = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = readiness
SPEC.loader.exec_module(readiness)


class DesktopExternalDeployReadinessTests(unittest.TestCase):
    def test_unconfigured_push_is_explicit_not_configured_receipt(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "not_configured")
        self.assertEqual(receipt["configuredModes"], [])
        self.assertEqual(receipt["completeModes"], [])
        self.assertIn("rolling GitHub Release", receipt["summary"])

    def test_required_external_deploy_blocks_without_complete_mode(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=True)

        self.assertEqual(receipt["status"], "blocked")
        self.assertTrue(receipt["requireExternalDeploy"])

    def test_http_promote_requires_url_token_and_verify_url(self) -> None:
        env = {
            "CHUMMER_RELEASE_UPLOAD_URL": "https://example.invalid/upload",
            "CHUMMER_RELEASE_UPLOAD_TOKEN": "token",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/releases.json",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=True)

        self.assertEqual(receipt["status"], "ready")
        self.assertEqual(receipt["configuredModes"], ["http_promote"])
        self.assertEqual(receipt["completeModes"], ["http_promote"])

    def test_incomplete_http_promote_is_not_mistaken_for_ready(self) -> None:
        with patch.dict(os.environ, {"CHUMMER_RELEASE_UPLOAD_URL": "https://example.invalid/upload"}, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "configured_incomplete")
        self.assertEqual(receipt["configuredModes"], ["http_promote"])
        http_mode = next(mode for mode in receipt["modes"] if mode["mode"] == "http_promote")
        self.assertEqual(
            sorted(http_mode["missing"]),
            ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL", "CHUMMER_RELEASE_UPLOAD_TOKEN"],
        )


if __name__ == "__main__":
    unittest.main()
