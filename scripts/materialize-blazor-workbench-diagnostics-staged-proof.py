#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_diagnostics",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client diagnostics",
            "data-workbench-diagnostics=\"strip\"",
            "Check the app when something feels off.",
            "Open runtime details, About, health, release status, or preview tools",
            "data-workbench-diagnostics-action=\"runtime-inspector\"",
            "data-workbench-diagnostics-action=\"about\"",
            "private const string AboutCommand = \"about\"",
            "command: AboutCommand",
            "data-workbench-diagnostics-action=\"health\"",
            "href=\"@HealthHref\"",
            "private const string HealthHref = \"health\"",
            "data-workbench-diagnostics-action=\"status\"",
            "href=\"@StatusHref\"",
            "private const string StatusHref = \"/status\"",
            "data-workbench-diagnostics-action=\"proof-shelf\"",
            "href=\"@PreviewHref\"",
            "private const string PreviewHref = \"preview\"",
            "data-workbench-diagnostics-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_diagnostics_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-diagnostics",
            ".browser-workbench-diagnostics-copy",
            ".browser-workbench-diagnostics-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "desktop_diagnostics_dialog_sources",
        "path": "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
        "tokens": [
            "RuntimeInspectorCommandId",
            "about",
        ],
    },
    {
        "id": "runtime_inspector_dossier_copy",
        "path": "Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor",
        "tokens": [
            "Review the installed rules, compatibility notes, and service links for this dossier.",
            "without changing the dossier.",
            "BuildSupportProofDiffReceipt",
            "BuildSupportHandoffReceipt",
        ],
    },
    {
        "id": "explain_trace_dossier_copy",
        "path": "Chummer.Blazor/Components/Shared/ExplainTracePanel.razor",
        "tokens": [
            "BuildExplainDiffReceipt",
            "without changing the dossier.",
            "BuildExplainEnvironmentBefore",
            "BuildExplainEnvironmentAfter",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench diagnostics posture",
            "blazor-workbench-diagnostics-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench diagnostics posture",
            "runtime inspector, About, health, status, preview tools, and help",
            "not yet claiming runtime health, build validity, diagnostics execution parity, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_DIAGNOSTICS_STAGED_PROOF",
            "workbench_diagnostics_staged_status",
            "workbench_diagnostics_staged_source_checks",
            "source_alignment_only_not_browser_execution",
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
        "contract_name": "chummer6-ui.blazor_workbench_diagnostics_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/blazor/health", "/status", "/blazor/preview", "/help"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route diagnostics source, style, status, and docs agree, including the same-origin /help diagnostics action.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim runtime health, build validity, diagnostics execution, portal help runtime behavior, or hosted proof readiness.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_diagnostics_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
