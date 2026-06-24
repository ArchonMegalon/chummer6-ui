from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
SCRIPT_PATH = REPO_ROOT / "scripts" / "materialize-downloads-publication-scope.py"
PUBLISH_SCRIPT_PATH = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
SPEC = importlib.util.spec_from_file_location("downloads_publication_scope", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise ImportError(f"Unable to load module from {SCRIPT_PATH}")
publication_scope = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = publication_scope
SPEC.loader.exec_module(publication_scope)


def test_local_shelf_receipt_does_not_claim_external_publish() -> None:
    receipt = publication_scope.build_receipt(
        deploy_dir="/tmp/downloads",
        release_version="run-test",
        release_channel="public_stable",
        promoted_artifact_count=2,
        deploy_mode=False,
        live_verify_target="",
        require_external_publish=False,
    )

    assert receipt["status"] == "passed"
    assert receipt["scope"] == "local_downloads_shelf_only"
    assert receipt["externalArtifactPublishVerified"] is False
    assert "not an external desktop artifact upload" in receipt["summary"]


def test_external_deploy_receipt_requires_deploy_mode_and_live_verify_target() -> None:
    receipt = publication_scope.build_receipt(
        deploy_dir="/srv/downloads",
        release_version="run-test",
        release_channel="public_stable",
        promoted_artifact_count=2,
        deploy_mode=True,
        live_verify_target="https://chummer.run/downloads/releases.json",
        require_external_publish=True,
    )

    assert receipt["status"] == "passed"
    assert receipt["scope"] == "external_downloads_publish_verified"
    assert receipt["externalArtifactPublishVerified"] is True


def test_cli_blocks_when_external_publish_is_required_but_only_local_shelf_exists(tmp_path: Path) -> None:
    output = tmp_path / "PUBLICATION_SCOPE.generated.json"

    result = subprocess.run(
        [
            "python3",
            str(SCRIPT_PATH),
            "--output",
            str(output),
            "--deploy-dir",
            str(tmp_path / "downloads"),
            "--release-version",
            "run-test",
            "--release-channel",
            "public_stable",
            "--promoted-artifact-count",
            "1",
            "--require-external-publish",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["status"] == "blocked"
    assert payload["externalArtifactPublishVerified"] is False
    assert "External desktop artifact publication was required" in payload["summary"]


def test_publish_script_materializes_publication_scope_receipt() -> None:
    text = PUBLISH_SCRIPT_PATH.read_text(encoding="utf-8")

    assert "PUBLICATION_SCOPE.generated.json" in text
    assert "materialize-downloads-publication-scope.py" in text
    assert "Updated local downloads shelf" in text
    assert "through verified external downloads lane" in text
