#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_POLISH_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_task_dock",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Compatibility route task dock",
            "Chummer App task dock",
            "Chummer App task shortcuts",
            "data-workbench-polish=\"task-dock\"",
            "Task dock",
            "data-workbench-dock-action=\"start-new\"",
            "data-workbench-dock-action=\"open-existing\"",
            "data-workbench-dock-action=\"build-lab\"",
            "data-workbench-dock-action=\"gear\"",
            "data-workbench-dock-action=\"save-as\"",
            "data-workbench-dock-action=\"export\"",
            "data-workbench-dock-action=\"print\"",
            "data-workbench-dock-action=\"downloads\"",
            "data-workbench-dock-action=\"support\"",
            "BuildPreviewHref",
            "IsAppRoute ? \"app\" : \"workbench\"",
            ": \"preview\"",
            "path-base-safe relative hrefs",
            "Compatibility route status",
            "Preview tools status",
            "Preview tools guidance",
            "Use this route for saved workbench links",
            "Use this route for preview and result-state tools",
            "older workbench bookmarks and shared links working",
            "user-facing browser client remains Chummer App at /blazor/app",
            "preview tools, startup deep links, and browser result-state checks",
            "href=\"home\"",
            "href=\"app\"",
            "Compatibility route",
            "href=\"workbench\"",
            "href=\"preview\"",
            "Preview tools",
            "Open preview tools",
            "href=\"showcase\"",
            "href=\"health\"",
        ],
    },
    {
        "id": "scoped_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-task-dock",
            ".browser-workbench-task-dock-copy",
            ".browser-workbench-task-dock-actions",
            "Chummer App theme polish",
            "not a default preview page",
            "Final Chummer App visual theme layer",
            "--app-gold",
            "--app-gold-soft",
            "#ffd46f",
            "--app-green-soft",
            "#8ff0bc",
            "--app-muted-strong",
            "--app-line-cool",
            "--app-shadow",
            "--app-radius",
            "overflow-x: clip",
            ".browser-preview-shell::before",
            "background-size: 3.4rem 3.4rem",
            ".browser-preview-banner",
            ".browser-preview-shell section[class*=\"browser-workbench-\"]",
            ".browser-preview-actions a",
            ".browser-preview-actions a[href=\"/downloads/\"]",
            ".browser-preview-actions a[href=\"/docs/\"]",
            ".browser-preview-actions a[href=\"/downloads/\"]:focus-visible",
            ".browser-preview-actions a[href=\"/docs/\"]:focus-visible",
            ".browser-preview-status-label",
            "border-radius: 999px",
            ".browser-preview-shell section[class*=\"browser-workbench-\"]::after",
            "linear-gradient(180deg, var(--app-gold), var(--app-green))",
            "linear-gradient(90deg, var(--app-gold), var(--app-green))",
            ".browser-workbench-density-options label:has(input:checked)",
            "accent-color: var(--app-green)",
            ".browser-preview-shell code",
            "[data-workbench-dock-action=\"start-new\"]",
            "[data-workbench-dock-action=\"open-existing\"]",
            "min-height: 3.6rem",
            "outline-color: rgba(6, 17, 13, 0.86)",
            "transform: translateY(-1px)",
            "radial-gradient(circle at 9% 0%",
            "@media (prefers-reduced-motion: reduce)",
            "@media (max-width: 720px)",
            "Final Chummer App theme pass",
            "--app-rust",
            "#ffd46f",
            "#76aeca",
            "background-size: 4.25rem 4.25rem",
            ".browser-preview-title",
            ".browser-preview-route-label",
            "linear-gradient(135deg, #ffe29a, #8ff0bc 82%)",
            "box-shadow: 0 0 0 5px rgba(143, 240, 188, 0.15)",
            "transition: border-color 160ms ease",
            "Final Chummer App motion pass",
            "@keyframes chummer-command-deck-reveal",
            "animation: chummer-command-deck-reveal 420ms cubic-bezier(0.2, 0.78, 0.2, 1) both",
            "animation-delay: 180ms",
            "animation: none",
            "Final route-token pass",
            "font-variant-ligatures: none",
            "min-height: 1.45rem",
            "text-transform: uppercase",
            "Final mobile route-token pass",
            "@media (max-width: 520px)",
            "overflow-wrap: anywhere",
            "Final keyboard route-token pass",
            ".browser-preview-shell code:focus-visible",
            "outline: 3px solid rgba(255, 212, 111, 0.78)",
            "Final high-contrast route-token pass",
            "@media (prefers-contrast: more)",
            "border-color: rgba(255, 247, 227, 0.82)",
        ],
    },
    {
        "id": "status_strip_route_chrome",
        "path": "Chummer.Blazor/Components/Shell/StatusStrip.razor",
        "tokens": [
            "@using Microsoft.AspNetCore.Components",
            "data-status-route-family",
            "BuildRouteFamily",
            "BuildRouteLabel",
            "Chummer App",
            "Home",
            "Preview tools",
            "Workbench compatibility",
            "Route:",
            "Route: {BuildRouteLabel()} | Character:",
            "Navigation.Uri",
        ],
    },
    {
        "id": "status_strip_route_chrome_style",
        "path": "Chummer.Blazor/wwwroot/app.css",
        "tokens": [
            "Route-aware status strip chrome",
            ".classic-status-strip [data-status-route-family]",
            "data-status-route-family=\"chummer_app\"",
            "data-status-route-family=\"workbench_compat\"",
            "rgba(255, 212, 111, 0.36)",
            "rgba(143, 240, 188, 0.48)",
            "rgba(118, 174, 202, 0.48)",
            "@media (prefers-contrast: more)",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench polish posture",
            "blazor-workbench-polish-staged-proof-check.sh",
            "preserving `/app` as the clean public browser client path",
            "`/blazor/app` as the hosted app path",
            "`/blazor/preview` as the preview tools/result-state route",
            "keyboard-visible portal nav focus",
            "route-aware status strip chrome",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "proof_contract_docs",
        "path": "docs/BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.md",
        "tokens": [
            "Chummer App and compatibility-route polish",
            "cohesive slate/amber/mint/blue browser-client palette",
            "final amber/mint/blue Chummer App color pass",
            "warm gold, mint, and blue accents",
            "app and compatibility route do not read as a default preview page",
            "`/app` remains the public browser client path",
            "`/blazor/app` remains the hosted app path",
            "`/blazor/preview` remains the preview tools and result-state route",
            "broad app-shell card treatment for every `browser-workbench-*` strip",
            "deliberate density-control styling with mint radio accents",
            "themed inline route/code tokens",
            "route-token app chrome treatment",
            "mobile route-token wrapping",
            "keyboard-visible route-token focus",
            "high-contrast route-token affordances",
            "pill-style route and status labels",
            "route-aware status strip chrome",
            "route-state status pill styling",
            "data-status-route-family",
            "left-edge gold-to-mint section accents",
            "keyboard-visible primary startup focus",
            "reduced-motion-safe command-deck reveal animation",
            "primary task-dock treatment for New runner and Open/import",
            "mobile touch-friendly primary task-dock actions",
            "primary task-dock focus outline",
            "portal-handoff header nav treatment",
            "keyboard-visible portal-handoff header nav focus",
            "not hosted browser execution proof",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench polish posture",
            "Task dock",
            "start, edit, output, and portal handoff",
            "preserving `/app` as the clean public browser-client route",
            "`/blazor/app` as the hosted app path",
            "`/blazor/preview` as the preview tools/result-state route",
            "final amber/mint/blue Chummer App theme layer",
            "reduced-motion-safe command-deck reveal",
            "route-token app chrome treatment",
            "mobile route-token wrapping",
            "keyboard-visible route-token focus",
            "high-contrast route-token affordances",
            "route-aware status strip chrome",
            "route-state status pill styling",
            "mobile top-edge section accents",
        ],
    },
    {
        "id": "docs_index_contract",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "source-staged Chummer App and compatibility-route polish contract",
            "compatibility-route boundary where `/app` remains the clean public browser client path",
            "`/blazor/app` remains the hosted app path",
            "`/blazor/preview` remains the preview tools/result-state route",
            "primary startup actions",
            "keyboard-visible focus",
            "mobile touch posture",
            "portal-handoff header nav treatment",
            "keyboard-visible portal nav focus",
            "slate/amber/mint/blue app theme",
            "refined amber/mint/blue app theme pass",
            "route-token app chrome treatment",
            "mobile route-token wrapping",
            "keyboard-visible route-token focus",
            "high-contrast route-token affordances",
            "route-aware status strip chrome",
            "route-state status pill styling",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_POLISH_STAGED_PROOF",
            "workbench_polish_staged_status",
            "workbench_polish_staged_source_checks",
            "source_alignment_only_not_browser_execution",
            "source_alignment_only_not_browser_execution_chummer_app_theme_primary_startup_mobile_portal_nav_motion",
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
        "contract_name": "chummer6-ui.blazor_workbench_polish_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "chummer_app_and_proof_compatible_blazor_workbench",
        "expected_routes": ["/blazor/app", "/blazor/workbench"],
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer App and compatibility-route polish source, style, status, and docs agree.",
            "The status utility reports this as app theme, primary startup, mobile posture, portal navigation, and reduced-motion-safe command-deck motion source alignment.",
            "It is not a substitute for hosted Playwright execution proof or Docker self-host proof.",
            "Do not use this receipt to claim desktop-equivalent browser workflow parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_workbench_polish_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
