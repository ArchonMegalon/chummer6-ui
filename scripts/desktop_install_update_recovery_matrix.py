#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import ensure_completion_root, load_json, utc_now, write_json


OUTPUT = "DESKTOP_INSTALL_UPDATE_RECOVERY_MATRIX.generated.json"


def main() -> int:
    release_channel = load_json(
        ensure_completion_root().parent.parent / "chummer-hub-registry" / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    )
    hub_release = load_json(
        ensure_completion_root().parent.parent / "chummer.run-services" / ".codex-studio" / "published" / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    )

    tuples = []
    for row in release_channel.get("desktopRouteTruth", []):
        tuples.append(
            {
                "tupleId": row.get("tupleId"),
                "platform": row.get("platform"),
                "head": row.get("head"),
                "routeRole": row.get("routeRole"),
                "promotionState": row.get("promotionState"),
                "rollbackState": row.get("rollbackState"),
                "installPosture": row.get("installPosture"),
                "publicInstallRoute": row.get("publicInstallRoute"),
                "detail": row.get("promotionReason") or row.get("installPostureReason"),
            }
        )

    tuples.append(
        {
            "tupleId": "avalonia:macos:osx-arm64",
            "platform": "macos",
            "head": "avalonia",
            "routeRole": "unpublished",
            "promotionState": "blocked",
            "rollbackState": "n/a",
            "installPosture": "no_public_download",
            "publicInstallRoute": None,
            "detail": "Public docs still state that there is no public macOS download today.",
        }
    )

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.desktop_install_update_recovery_matrix",
        "scope": "windows_linux_preview_only",
        "status": "strong_preview",
        "summary": "Install, update, and recovery proof is strong for current Windows/Linux public tuples, with macOS explicitly out of scope for this closure pass; fallback tuples remain proof-gated.",
        "journeysPassed": hub_release.get("journeys_passed", []),
        "desktopTuples": tuples,
        "blockingFindings": [
            "Windows and Linux primary installer tuples are proven, but fallback heads remain proof-gated.",
            "Current public posture is preview, not finished flagship release.",
            "This bundle is intentionally Windows/Linux-scoped and must not be used as a global cross-platform release claim.",
        ],
        "evidence": {
            "releaseChannel": str(ensure_completion_root().parent.parent / "chummer-hub-registry" / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"),
            "hubLocalReleaseProof": str(ensure_completion_root().parent.parent / "chummer.run-services" / ".codex-studio" / "published" / "HUB_LOCAL_RELEASE_PROOF.generated.json"),
        },
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
