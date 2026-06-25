#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_pwa_install",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client PWA install and update lane",
            "data-workbench-pwa-install=\"strip\"",
            "Make the browser client feel installable.",
            "private const string InstallWebAppCommand = \"install_web_app\"",
            "private const string OfflineCacheStatusCommand = \"offline_cache_status\"",
            "private const string ApplyWebUpdateCommand = \"apply_web_update\"",
            "private const string BrowserPermissionsCommand = \"browser_permissions\"",
            "private const string ReleaseChannelCommand = \"release_channel\"",
            "private const string ResetWebCacheCommand = \"reset_web_cache\"",
            "command: InstallWebAppCommand",
            "command: OfflineCacheStatusCommand",
            "command: ApplyWebUpdateCommand",
            "command: BrowserPermissionsCommand",
            "command: ReleaseChannelCommand",
            "command: ResetWebCacheCommand",
            "data-workbench-pwa-install-action=\"install_prompt\"",
            "data-workbench-pwa-install-action=\"offline_cache\"",
            "data-workbench-pwa-install-action=\"update_available\"",
            "data-workbench-pwa-install-action=\"permissions\"",
            "data-workbench-pwa-install-action=\"release_channel\"",
            "data-workbench-pwa-install-action=\"reset_cache\"",
            "data-workbench-pwa-install-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_pwa_install_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-pwa-install",
            ".browser-workbench-pwa-install-copy",
            ".browser-workbench-pwa-install-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench PWA-install posture",
            "blazor-workbench-pwa-install-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench PWA-install posture",
            "install prompt, offline cache, update available, browser permissions, release channel, reset cache, and help",
            "not yet claiming service-worker, install prompt, cache update, browser permission, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_PWA_INSTALL_STAGED_PROOF",
            "workbench_pwa_install_staged_status",
            "workbench_pwa_install_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8")


def evaluate_check(check: dict) -> dict:
    content = read_text(check["path"])
    missing_tokens = [token for token in check["tokens"] if token not in content]
    return {
        "id": check["id"],
        "path": check["path"],
        "status": "passed" if not missing_tokens else "failed",
        "required_token_count": len(check["tokens"]),
        "missing_tokens": missing_tokens,
    }


def main() -> int:
    evaluated_checks = [evaluate_check(check) for check in CHECKS]
    failures = [check for check in evaluated_checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_pwa_install_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route PWA-install source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, service-worker proof, install-prompt proof, cache-update proof, browser-permission proof, or portal help runtime proof.",
            "Do not use this receipt to claim service-worker, install prompt, cache update, browser permission, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_pwa_install_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
