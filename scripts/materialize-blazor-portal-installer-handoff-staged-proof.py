#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/downloads/",
    "/downloads/releases.json",
    "/downloads/install/avalonia-linux-x64-installer",
    "/downloads/install/avalonia-win-x64-installer",
    "/downloads/install/blazor-desktop-linux-x64-installer",
    "/downloads/install/blazor-desktop-win-x64-installer",
    "/contact",
    "/status",
    "/blazor/",
    "/blazor/workbench",
]

CHECKS = [
    {
        "id": "hosted_public_edge_route_probe",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": [
            "/downloads/",
            "/downloads/releases.json",
            "/downloads/install/avalonia-linux-x64-installer",
            "/downloads/install/avalonia-win-x64-installer",
            "/downloads/install/blazor-desktop-linux-x64-installer",
            "/downloads/install/blazor-desktop-win-x64-installer",
            "/contact",
            "/status",
            "/blazor/",
            "/blazor/workbench",
            "Download the current Windows installer.",
            "Download the current Linux DEB package.",
        ],
    },
    {
        "id": "self_host_portal_route_probe",
        "path": "scripts/e2e-portal.cjs",
        "tokens": [
            "/downloads/",
            "/downloads/releases.json",
            "/downloads/install/avalonia-linux-x64-installer",
            "/downloads/install/avalonia-win-x64-installer",
            "/downloads/install/blazor-desktop-linux-x64-installer",
            "/downloads/install/blazor-desktop-win-x64-installer",
            "/contact",
            "/status",
            "/blazor/",
            "/blazor/workbench",
        ],
    },
    {
        "id": "self_host_release_receipt_metadata",
        "path": "scripts/e2e-portal.sh",
        "tokens": [
            "/downloads/install/avalonia-linux-x64-installer",
            "/downloads/",
            "/downloads/releases.json",
            "/downloads/install/blazor-desktop-linux-x64-installer",
            "/downloads/install/blazor-desktop-win-x64-installer",
            "/blazor/workbench",
        ],
    },
    {
        "id": "portal_known_install_route_resolver",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            "ReleaseInstallRouteSummary",
            "CollectInstallRoutes",
            "publicInstallRoute",
            "installState={knownInstallRoute.InstallPosture}",
            "proof_required",
        ],
    },
    {
        "id": "portal_handoff_docs",
        "path": "docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
        "tokens": EXPECTED_ROUTES + [
            "source-only",
            "same-origin through `Chummer.Portal`",
            "`installState=proof_required`",
            "Runtime claims still require the local portal proof and hosted public-edge proof receipts.",
        ],
    },
    {
        "id": "self_host_runbook",
        "path": "docs/BLAZOR_SELF_HOST_RUNBOOK.md",
        "tokens": [
            "Docker",
            "/blazor/workbench",
            "downloads",
        ],
    },
    {
        "id": "release_signoff_boundary",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
            "source-staged",
            "not runtime proof",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    checks = []
    failures = []

    for check in CHECKS:
        path = check["path"]
        tokens = check["tokens"]
        try:
            text = read_text(path)
        except FileNotFoundError:
            failures.append(f"{path}: missing file")
            checks.append({**check, "status": "failed", "missing_tokens": tokens})
            continue

        missing_tokens = [token for token in tokens if token not in text]
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": "failed" if missing_tokens else "passed",
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_portal_installer_handoff_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "portal_backed_blazor_workbench_handoff",
        "expected_routes": EXPECTED_ROUTES,
        "checks": checks,
        "failures": failures,
        "notes": [
            "This proof is source alignment only for Blazor-to-portal installer/download/support handoff.",
            "It does not prove the portal is running or routes execute at runtime.",
            "Runtime evidence remains owned by local portal proof, hosted route-entry proof, and hosted execution proof receipts.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_portal_installer_handoff_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
