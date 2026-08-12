from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT / "release-evidence" / "run-20260802-160500" / "windows" / "native-startup"


def test_archived_native_windows_proof_is_immutable_and_bounded() -> None:
    index = json.loads((ARCHIVE / "evidence-index.json").read_text(encoding="utf-8"))
    receipt = json.loads(
        (ARCHIVE / "startup-smoke-avalonia-win-x64.receipt.json").read_text(encoding="utf-8")
    )

    assert index["status"] == "pass"
    assert index["readinessScope"] == "native_windows_startup_evidence"
    assert index["authorityBindingStatus"] == "release_artifact_and_live_manifest_bound"
    assert index["releaseVersion"] == receipt["releaseVersion"]
    assert index["artifact"]["sha256"] == receipt["artifactSha256"]
    assert receipt["executionEnvironment"] == "native_windows"
    assert receipt["verificationScope"] == "native_windows_startup"
    assert receipt["readyCheckpoint"] == "pre_ui_event_loop"
    assert receipt["installerCompletionProofMode"] == "inner_reset_trace_and_installed_target"
    assert receipt["nativeHostEvidence"]["runner"] == "native-msys-direct"

    for row in index["files"]:
        payload = (ARCHIVE / row["path"]).read_bytes()
        assert hashlib.sha256(payload).hexdigest() == row["sha256"]

    assert index["missingAuthorityBindings"] == [
        "registrySnapshotSha256",
        "registryManifestSha256",
        "releaseDecisionSha256",
        "releaseScopeDecisionSha256",
    ]
    assert "whole_product_preview_readiness" in index["doesNotAssert"]
    assert "stable_readiness" in index["doesNotAssert"]
    assert "flagship_readiness" in index["doesNotAssert"]
