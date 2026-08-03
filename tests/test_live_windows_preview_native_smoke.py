from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "live_windows_preview_native_smoke.py"
SPEC = importlib.util.spec_from_file_location("live_windows_preview_native_smoke", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def pe_bytes() -> bytes:
    raw = bytearray(512)
    raw[:2] = b"MZ"
    raw[60:64] = (128).to_bytes(4, "little")
    raw[128:132] = b"PE\0\0"
    return bytes(raw)


def manifest_bytes(*, installer: bytes, download_url: str) -> bytes:
    version = "run-20260802-160500"
    return (
        json.dumps(
            {
                "status": "published",
                "channelId": "preview",
                "version": version,
                "artifacts": [
                    {
                        "artifactId": MODULE.INSTALLER_ID,
                        "fileName": MODULE.INSTALLER_FILE_NAME,
                        "head": "avalonia",
                        "platform": "windows",
                        "rid": "win-x64",
                        "kind": "installer",
                        "sha256": hashlib.sha256(installer).hexdigest(),
                        "sizeBytes": len(installer),
                        "version": version,
                        "releaseVersion": version,
                        "downloadUrl": download_url,
                    }
                ],
            },
            separators=(",", ":"),
        )
        + "\n"
    ).encode()


def test_prepare_binds_manifest_route_and_exact_installer(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    installer = pe_bytes()
    manifest = manifest_bytes(
        installer=installer,
        download_url=(
            "/downloads/g/gen-20260802T164413Z-bf6517fa73a94b2a/"
            f"files/{MODULE.INSTALLER_FILE_NAME}"
        ),
    )

    monkeypatch.setattr(
        MODULE,
        "fetch_exact",
        lambda url, *, max_bytes: (
            manifest if url == MODULE.LIVE_MANIFEST_URL else installer
        ),
    )
    output = tmp_path / MODULE.INSTALLER_FILE_NAME
    result = MODULE.prepare(
        version="run-20260802-160500",
        manifest_sha256=hashlib.sha256(manifest).hexdigest(),
        installer_sha256=hashlib.sha256(installer).hexdigest(),
        installer_size_bytes=len(installer),
        output=output,
    )

    assert result["status"] == "prepared"
    assert output.read_bytes() == installer


@pytest.mark.parametrize(
    "download_url",
    [
        "https://example.invalid/downloads/files/chummer-avalonia-win-x64-installer.exe",
        "https://chummer.run.evil.invalid/downloads/files/chummer-avalonia-win-x64-installer.exe",
        "/downloads/files/chummer-avalonia-win-x64-installer.exe?changed=1",
        "/downloads/files/../files/chummer-avalonia-win-x64-installer.exe",
    ],
)
def test_prepare_rejects_noncanonical_installer_routes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    download_url: str,
) -> None:
    installer = pe_bytes()
    manifest = manifest_bytes(installer=installer, download_url=download_url)
    monkeypatch.setattr(MODULE, "fetch_exact", lambda url, *, max_bytes: manifest)

    with pytest.raises(MODULE.EvidenceError, match="same-origin route"):
        MODULE.prepare(
            version="run-20260802-160500",
            manifest_sha256=hashlib.sha256(manifest).hexdigest(),
            installer_sha256=hashlib.sha256(installer).hexdigest(),
            installer_size_bytes=len(installer),
            output=tmp_path / MODULE.INSTALLER_FILE_NAME,
        )


def test_verify_receipt_requires_native_windows(tmp_path: Path) -> None:
    installer_sha256 = "a" * 64
    receipt = tmp_path / "receipt.json"
    receipt.write_text(
        json.dumps(
            {
                "status": "pass",
                "readyCheckpoint": "pre_ui_event_loop",
                "headId": "avalonia",
                "platform": "windows",
                "rid": "win-x64",
                "arch": "x64",
                "channelId": "preview",
                "releaseVersion": "run-20260802-160500",
                "artifactId": MODULE.INSTALLER_ID,
                "artifactFileName": MODULE.INSTALLER_FILE_NAME,
                "artifactDigest": f"sha256:{installer_sha256}",
                "executionEnvironment": "native_windows",
                "verificationScope": "native_windows_startup",
                "nativeHostEvidence": {
                    "contractName": "chummer6-ui.native_windows_host_evidence",
                    "status": "verified",
                    "isNativeWindows": True,
                    "hostPlatform": "windows",
                    "hostKernel": "MINGW64_NT",
                    "runner": "pwsh",
                    "evidenceSource": "host_kernel_and_runner_selection",
                },
            }
        )
        + "\n",
        encoding="utf-8",
    )

    result = MODULE.verify_receipt(
        receipt=receipt,
        version="run-20260802-160500",
        installer_sha256=installer_sha256,
    )
    assert result["status"] == "verified"

    loaded = json.loads(receipt.read_text())
    loaded["nativeHostEvidence"]["runner"] = "wine"
    receipt.write_text(json.dumps(loaded) + "\n", encoding="utf-8")
    with pytest.raises(MODULE.EvidenceError, match="native startup receipt differs"):
        MODULE.verify_receipt(
            receipt=receipt,
            version="run-20260802-160500",
            installer_sha256=installer_sha256,
        )


def test_workflow_is_evidence_only_and_sha_bound() -> None:
    workflow = (
        REPO_ROOT / ".github" / "workflows" / "live-windows-preview-native-smoke.yml"
    ).read_text(encoding="utf-8")

    assert "expected_contract_sha" in workflow
    assert "capture_confirmed" in workflow
    assert "persist-credentials: false" in workflow
    assert "permissions:\n  contents: read" in workflow
    assert "Publication/upload/deployment authority: `false`" in workflow
