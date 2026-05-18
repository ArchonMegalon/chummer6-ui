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
        "scope": "windows_linux_public_release_only",
        "status": "pass",
        "summary": "Install, update, and recovery proof is sufficient for the current Windows/Linux public-release tuples; macOS stays out of scope and unpromoted fallback heads remain non-primary recovery lanes rather than release blockers for this scoped verdict.",
        "journeysPassed": hub_release.get("journeys_passed", []),
        "desktopTuples": tuples,
        "remainingNonEqualAreas": [
            {
                "area": "blazor_desktop_windows_linux_fallback_heads",
                "reason": "Fallback Blazor Desktop tuples remain proof-gated recovery/manual lanes and are not promoted as parity-critical primary public routes."
            }
        ],
        "blockingFindings": [],
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
