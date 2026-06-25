#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_desktop_install",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Desktop install options",
            "data-workbench-desktop-install=\"strip\"",
            "Install or continue locally.",
            "/downloads/RELEASE_CHANNEL.generated.json",
            "data-workbench-desktop-install-action=\"downloads\"",
            "data-workbench-desktop-install-action=\"release-manifest\"",
            "data-workbench-desktop-install-action=\"release-status\"",
            "data-workbench-desktop-install-action=\"account-work\"",
            "data-workbench-desktop-install-action=\"self-host\"",
            "data-workbench-desktop-install-action=\"help\"",
            "/help",
            "data-workbench-desktop-install-action=\"support\"",
        ],
    },
    {
        "id": "scoped_desktop_install_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-desktop-install",
            ".browser-workbench-desktop-install-copy",
            ".browser-workbench-desktop-install-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "native_desktop_installer_progress_theme",
        "path": "Chummer.Desktop.Installer/Program.cs",
        "tokens": [
            "Color.FromArgb(25, 32, 38)",
            "Color.FromArgb(143, 240, 188)",
            "Color.FromArgb(255, 247, 227)",
            "Color.FromArgb(255, 212, 111)",
            "Color.FromArgb(8, 11, 13)",
            "Color.FromArgb(16, 22, 26)",
            "Color.FromArgb(255, 243, 207)",
            "SystemInformation.HighContrast",
            "SystemColors.Highlight",
            "SystemColors.WindowText",
            "Elapsed: 0s",
            "This may take a few minutes on slower systems.",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench desktop install handoff posture",
            "blazor-workbench-desktop-install-staged-proof-check.sh",
            "native installer progress chrome stays aligned to the Chummer App slate/amber/mint visual family",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "desktop_install_contract_docs",
        "path": "docs/BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.md",
        "tokens": [
            "browser-to-desktop continuity visible",
            "native desktop installer progress chrome",
            "amber accent bar, deep slate shell, mint progress fill, warm ink metadata, and amber hint text",
            "native installer high-contrast system-color fallback",
            "not native installer runtime proof",
        ],
    },
    {
        "id": "docs_index_contract",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "source-staged Chummer App and proof-compatible workbench desktop install handoff contract",
            "native desktop installer progress chrome",
            "amber accent bar, deep slate shell, mint progress fill, warm ink metadata, and amber hint text",
            "native installer high-contrast system-color fallback",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench desktop install handoff posture",
            "downloads, update channel, status, account, self-host notes, help, and support",
            "native installer progress chrome uses the same slate/amber/mint visual family",
            "native installer high-contrast system-color fallback",
            "not yet claiming installer download, portal help runtime, or Docker runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF",
            "workbench_desktop_install_staged_status",
            "workbench_desktop_install_staged_source_checks",
            "source_alignment_only_not_browser_execution_native_installer_amber_slate_mint_chrome_high_contrast_fallback",
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
        status = "failed" if missing_tokens else "passed"
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": status,
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_desktop_install_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": [
            "/blazor/workbench",
            "/downloads/",
            "/downloads/RELEASE_CHANNEL.generated.json",
            "/status",
            "/account/work",
            "/docs/",
            "/help",
            "/contact",
        ],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and proof-compatible workbench desktop install handoff source, style, status, and docs agree.",
            "The status utility reports native installer amber/slate/mint chrome and high-contrast fallback as source alignment only.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, release download proof, portal help runtime, or installer proof.",
            "Do not use this receipt to claim installer delivery, Docker runtime, portal help runtime, account authorization, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_desktop_install_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
