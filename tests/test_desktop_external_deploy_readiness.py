from __future__ import annotations

import importlib.util
import os
import subprocess
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
    def test_default_output_path_uses_untracked_tmp_receipt(self) -> None:
        self.assertEqual(
            readiness.DEFAULT_OUTPUT,
            REPO_ROOT / ".tmp" / "deploy-readiness" / "EXTERNAL_DEPLOY_READINESS.generated.json",
        )

    def test_cli_default_output_writes_tmp_receipt_without_touching_tracked_path(self) -> None:
        tracked_output = REPO_ROOT / "deploy-readiness" / "EXTERNAL_DEPLOY_READINESS.generated.json"
        tracked_before = tracked_output.read_text(encoding="utf-8")

        result = subprocess.run(
            ["python3", str(MODULE_PATH)],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(readiness.DEFAULT_OUTPUT.is_file())
        self.assertEqual(tracked_output.read_text(encoding="utf-8"), tracked_before)

    def test_unconfigured_push_is_explicit_not_configured_receipt(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "not_configured")
        self.assertEqual(receipt["configuredModes"], [])
        self.assertEqual(receipt["completeModes"], [])
        self.assertIn("rolling GitHub Release", receipt["summary"])

    def test_portal_directory_ready_when_verify_url_is_valid(self) -> None:
        env = {
            "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR": "/tmp/chummer-downloads",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/releases.json",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "ready")
        self.assertEqual(receipt["configuredModes"], ["portal_directory"])
        self.assertEqual(receipt["completeModes"], ["portal_directory"])

    def test_invalid_portal_directory_verify_url_is_not_counted_as_complete(self) -> None:
        env = {
            "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR": "/tmp/chummer-downloads",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "not-a-url",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "configured_incomplete")
        self.assertEqual(receipt["configuredModes"], ["portal_directory"])
        self.assertEqual(receipt["completeModes"], [])
        portal_mode = next(mode for mode in receipt["modes"] if mode["mode"] == "portal_directory")
        self.assertEqual(portal_mode["invalid"], ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"])

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

    def test_http_promote_accepts_token_file_as_auth_source(self) -> None:
        env = {
            "CHUMMER_RELEASE_UPLOAD_URL": "https://example.invalid/upload",
            "CHUMMER_RELEASE_UPLOAD_TOKEN_FILE": "/tmp/chummer-upload-token.txt",
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
            [
                "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL",
                "CHUMMER_RELEASE_UPLOAD_TOKEN or CHUMMER_RELEASE_UPLOAD_TOKEN_FILE/CHUMMER_RELEASE_UPLOAD_TOKEN_PATH",
            ],
        )

    def test_invalid_http_promote_urls_are_not_counted_as_complete(self) -> None:
        env = {
            "CHUMMER_RELEASE_UPLOAD_URL": "not-a-url",
            "CHUMMER_RELEASE_UPLOAD_TOKEN": "token",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "bad verify",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=True)

        self.assertEqual(receipt["status"], "blocked")
        self.assertEqual(receipt["configuredModes"], ["http_promote"])
        self.assertEqual(receipt["completeModes"], [])
        http_mode = next(mode for mode in receipt["modes"] if mode["mode"] == "http_promote")
        self.assertEqual(
            sorted(http_mode["invalid"]),
            ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL", "CHUMMER_RELEASE_UPLOAD_URL"],
        )

    def test_invalid_object_storage_uri_is_not_counted_as_complete(self) -> None:
        env = {
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/releases.json",
            "CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID": "key",
            "CHUMMER_PORTAL_DOWNLOADS_AWS_SECRET_ACCESS_KEY": "secret",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=False)

        self.assertEqual(receipt["status"], "configured_incomplete")
        self.assertEqual(receipt["configuredModes"], ["object_storage"])
        self.assertEqual(receipt["completeModes"], [])
        object_storage_mode = next(mode for mode in receipt["modes"] if mode["mode"] == "object_storage")
        self.assertEqual(object_storage_mode["invalid"], ["CHUMMER_PORTAL_DOWNLOADS_S3_URI"])

    def test_object_storage_ready_with_verify_url_and_credentials(self) -> None:
        env = {
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "s3://bucket/path",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/releases.json",
            "CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID": "key",
            "CHUMMER_PORTAL_DOWNLOADS_AWS_SECRET_ACCESS_KEY": "secret",
        }
        with patch.dict(os.environ, env, clear=True):
            receipt = readiness.build_receipt(require_external_deploy=True)

        self.assertEqual(receipt["status"], "ready")
        self.assertEqual(receipt["configuredModes"], ["object_storage"])
        self.assertEqual(receipt["completeModes"], ["object_storage"])


if __name__ == "__main__":
    unittest.main()
