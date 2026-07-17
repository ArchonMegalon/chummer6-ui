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
    "/help",
    "/app",
    "/blazor/",
    "/blazor/home",
    "/blazor/app",
    "/blazor/workbench",
]

VISUAL_CONTRACT = (
    "source_alignment_only_chummer_app_amber_mint_blue_palette_shared_grid_mobile_softened_high_contrast_motion_"
    "user_facing_route_rail_downloads_docs_cards_and_labelled_recovery_rails_not_runtime_visual_proof"
)

CHECKS = [
    {
        "id": "hosted_public_edge_route_probe",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": [
            "/downloads",
            "/downloads/releases.json",
            "/downloads/install/avalonia-linux-x64-installer",
            "/downloads/install/avalonia-win-x64-installer",
            "publicInstallerRedirectMatches",
            "/status",
            "/help",
            "/contact",
            "/play",
            "/ledger",
            "/participate",
            "/app",
            "/app?command=character_roster",
            "/blazor/home",
            "/session/",
            "/coach/",
            "avoidsFrontDoorNoise",
            "avoidsParticipateFailureCopy",
        ],
    },
    {
        "id": "self_host_portal_route_probe",
        "path": "scripts/e2e-portal.cjs",
        "tokens": [
            "/downloads/",
            "/downloads/releases.json",
            "/downloads/?next=%2Fdownloads%2Finstall%2Fblazor-desktop-linux-x64-installer&installState=proof_required",
            "/contact",
            "/status",
            "/help",
            "/app",
            "/blazor/",
            "/blazor/home",
            "/blazor/app",
            "/blazor/workbench",
            'data-download-panel="desktop-downloads"',
            'data-download-list="published-artifacts"',
            'data-download-link-mode="self-host-dispatch"',
            'data-install-route-link-mode="proof-required"',
            'data-self-host-downloads-panel="docker-operator"',
            'data-install-state="proof_required"',
            'data-install-state-action="open-browser-app"',
            'data-portal-status-action="open-chummer-app"',
            'data-portal-help-action="open-chummer-app"',
            'data-portal-contact-action="open-chummer-app"',
            'data-docs-panel="operator-openapi-explorer"',
            'data-openapi-chummer-app-route="true"',
            'data-openapi-chummer-home-route="true"',
            'data-openapi-blazor-entry-route="true"',
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
            "/contact",
            "/status",
            "/help",
            "/blazor/home",
            "/blazor/app",
            "/blazor/workbench",
        ],
    },
    {
        "id": "portal_source_routes_and_openapi",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            "static class PortalRoutes",
            "PublicAppRoster",
            "PublicAppSlash",
            "BlazorApp",
            "BlazorHome",
            "CharacterRosterCommand",
            'PublicAppRoster => $"{PublicApp}?command={CharacterRosterCommand}"',
            "BuildPublicAppRedirectUrl",
            "BuildBlazorAppUrl",
            "BuildBlazorHomeUrl",
            "[PortalRoutes.PublicApp]",
            "[PortalRoutes.BlazorApp]",
            "[PortalRoutes.BlazorHome]",
            '["/blazor/"]',
            '["/downloads/"]',
            '["/downloads/releases.json"]',
            '["/downloads/install/{artifactId}"]',
            '["/status"]',
            '["/contact"]',
            '["/help"]',
            "Chummer Online route discovery",
            "Open the user-facing Chummer Online app",
            "Open Chummer Online through the clean public /app route",
        ],
    },
    {
        "id": "portal_home_chummer_app_routes",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            "BuildPortalHomeHtml",
            "Explore Chummer Online, downloads, and support from one self-hosted edge.",
            "Start in the Character Roster, continue into Chummer Online",
            "PortalRoutes.PublicApp",
            "PortalRoutes.PublicAppRoster",
            "CharacterRosterCommand",
            'data-portal-home-action="explore-chummer-online"',
            'aria-label="Chummer Online routes"',
            'data-portal-home-route="chummer-app-roster"',
            "Open Character Roster",
            'data-portal-home-route="chummer-app"',
            'data-portal-home-route="chummer-home"',
            'data-portal-home-route="downloads"',
            "Get desktop client",
            ".route-pills a:hover",
            "@media (prefers-reduced-motion: reduce)",
        ],
    },
    {
        "id": "downloads_install_state_guidance",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            'data-download-panel="desktop-downloads"',
            "desktop-downloads-title",
            'aria-labelledby="desktop-downloads-title"',
            'aria-describedby="fallback-link"',
            'data-download-action="download-artifact"',
            'data-download-action="open-chummer-app"',
            "Install native Chummer when you need desktop file-system behavior",
            "data-download-count",
            "Published artifacts:",
            "Published artifacts stay on this self-hosted edge when local bytes are mounted here.",
            "data-download-link-mode",
            "self-host-dispatch",
            "BuildDownloadsInstallStatePanel",
            'data-install-state="proof_required"',
            "installer proof is still required",
            'data-install-state-action="open-browser-app"',
            "Compatibility handoff routes",
            'id="compatibility-handoff-routes"',
            'data-install-route-list="compatibility-handoff"',
            "Known fallback install routes stay visible here",
            "Compatibility routes:",
            "data-install-route-posture",
            "data-install-route-promotion",
            "data-install-route-public-route",
            'data-install-route-action="open-proof-required-route"',
            "data-install-route-link-mode",
            "proof-required handoff",
            'data-self-host-downloads-panel="docker-operator"',
            "Mount <code>releases.json</code> and the sibling <code>RELEASE_CHANNEL.generated.json</code> into the downloads volume before claiming installer availability.",
            "Use /app?command=character_roster when installer proof is pending",
            "Proof-required compatibility routes stay visible",
        ],
    },
    {
        "id": "portal_recovery_surfaces",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            "BuildHelpHtml",
            'data-portal-help-panel="handoff-guide"',
            'data-portal-help-context="self-host-first"',
            'data-portal-help-action="open-chummer-app"',
            'data-portal-help-action="open-chummer-home"',
            'data-portal-help-action="open-downloads"',
            'data-portal-help-action="open-status"',
            'data-portal-help-action="open-contact"',
            'data-portal-help-action="open-docs"',
            "BuildStatusHtml",
            "Current release",
            "The build, platforms, and current state in one place.",
            'data-portal-status-panel="release-availability"',
            'data-portal-status-boundary="source-manifest-backed"',
            'data-portal-status-action="open-chummer-app"',
            'data-portal-status-action="open-chummer-home"',
            'data-portal-status-action="open-docs"',
            "This status page is backed by the local release-manifest shelf.",
            "BuildContactHtml",
            'data-portal-contact-panel="support-handoff"',
            'data-portal-contact-context="self-host-fallback"',
            'data-portal-contact-public-route="chummer.run/contact"',
            'data-portal-contact-scenarios="installer-account-app"',
            'data-portal-contact-scenario="installer-proof"',
            'data-portal-contact-scenario="account-recovery"',
            'data-portal-contact-scenario="browser-app"',
            'data-portal-contact-action="open-chummer-app"',
            'data-portal-contact-action="open-chummer-home"',
            'data-portal-contact-action="open-docs"',
        ],
    },
    {
        "id": "portal_docs_explorer",
        "path": "Chummer.Portal/Program.cs",
        "tokens": [
            "BuildDocsHtml(options)",
            'data-docs-panel="operator-openapi-explorer"',
            'data-docs-shortcuts="operator-recovery"',
            'aria-describedby="docs-shortcuts-description"',
            'data-docs-summary="openapi-load-state"',
            'role="status"',
            'aria-live="polite"',
            'data-docs-endpoints="openapi-route-list"',
            'role="list"',
            'aria-label="Documented portal routes"',
            'data-docs-endpoint-card="openapi-route"',
            "data-docs-endpoint-route",
            "data-docs-endpoint-family",
            "data-docs-endpoint-methods",
            "data-docs-endpoint-summary",
            'role="listitem"',
            'data-docs-action="open-chummer-app"',
            'data-docs-action="open-chummer-home"',
            'data-docs-action="open-downloads"',
            'data-docs-action="open-status"',
            'data-docs-action="open-help"',
            'data-docs-action="open-contact"',
            'data-docs-action="open-openapi-json"',
            "escapeHtml",
            "escapeHtml(method.toUpperCase())",
        ],
    },
    {
        "id": "blazor_home_roster_first_root_redirect",
        "path": "Chummer.Blazor/Components/Pages/Home.razor",
        "tokens": [
            '@page "/"',
            '@page "/home"',
            "Chummer Online",
            'data-home-hero-action="@ExploreChummerOnlineHeroAction"',
            "Explore Chummer Online",
            "Navigation.NavigateTo(RosterRoute, replace: true)",
            'private const string AppRoute = "app"',
            'private const string RosterCommandQueryName = "command"',
            'private const string CharacterRosterCommand = "character_roster"',
            'private static string RosterRoute => $"{AppRoute}?{RosterCommandQueryName}={CharacterRosterCommand}"',
            'private static string PublicRosterRoute => $"/{RosterRoute}"',
        ],
    },
    {
        "id": "portal_playwright_home_smoke",
        "path": "scripts/e2e-portal-playwright.cjs",
        "tokens": [
            "auditPortalHome",
            ".minimal-hero",
            "Download Chummer",
            "Current public installers: Windows and Linux.",
            "portal home downloads CTA",
            "expectNoVisibleClipping",
        ],
    },
    {
        "id": "portal_handoff_docs",
        "path": "docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
        "tokens": EXPECTED_ROUTES
        + [
            "source-only",
            "same-origin through `Chummer.Portal`",
            "`installState=proof_required`",
            "`data-install-route-public-route`",
            "self-host operator lane",
            "Chummer Online handoff action",
            "pointing at `/app?command=character_roster`",
            "Chummer Online Character Roster deep link",
            'aria-label="Chummer Online routes"',
            "same polished Chummer Online slate/amber/mint/blue visual language",
            "restrained ambient glow",
            "deep ink/surface contrast",
            "Help cards and the help/contact/status rails keep explicit hover/focus affordances",
            "Runtime claims still require the local portal proof and hosted public-edge proof receipts.",
        ],
    },
    {
        "id": "docs_index_contract_link",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "Portal Installer Handoff",
            "docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
            "scripts/materialize-blazor-portal-installer-handoff-staged-proof.py",
            "portal_installer_handoff_staged_*",
            "direct raw downloads",
            "proof-required handoff rows",
            "self-host operator lane",
            "Chummer Online routes",
            "help-card hover/focus affordances",
            "It is not runtime proof.",
        ],
    },
    {
        "id": "source_staged_runbook_boundary",
        "path": "docs/BLAZOR_SOURCE_STAGED_PROOF_RUNBOOK.md",
        "tokens": [
            "Adjacent Portal-Boundary Source Proofs",
            "BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json",
            "not members of the Chummer Online and /blazor/workbench compatibility route source-staged proof set",
            "they do not prove hosted execution, Docker self-host execution, or installer availability",
            "portal_installer_handoff_staged_status=",
            "portal_installer_handoff_staged_route_count=",
            "portal_installer_handoff_staged_source_checks=",
            "portal_installer_handoff_staged_note=source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution",
            f"portal_installer_handoff_staged_visual_contract={VISUAL_CONTRACT}",
        ],
    },
    {
        "id": "self_host_runbook",
        "path": "docs/BLAZOR_SELF_HOST_RUNBOOK.md",
        "tokens": [
            "Docker",
            "/blazor/home",
            "/blazor/app",
            "/blazor/workbench",
            "same polished Chummer Online slate/amber/mint/blue visual language",
            "restrained ambient glow",
            "deep ink/surface contrast",
            "warm gold primary calls to action",
            "Chummer Online routes",
            "explicit hover/focus affordances",
            "Help cards plus help, contact, and status exits",
            "reduced-motion guards",
        ],
    },
    {
        "id": "release_signoff_boundary",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md",
            "source-staged",
            "not runtime proof",
            "portal_installer_handoff_staged_*",
            "same polished Chummer Online slate/amber/mint/blue visual language",
            "restrained ambient glow",
            "deep ink/surface contrast",
            "Chummer Online routes",
            "release-signoff visibility only",
            "they do not prove portal runtime behavior, installer availability, hosted execution, or Docker self-host execution",
        ],
    },
    {
        "id": "parity_goal_portal_recovery_visual_contract",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "portal recovery pages for downloads, docs, help, status, and contact",
            "same polished Chummer Online visual language",
            "restrained ambient glow",
            "deep ink/surface contrast",
            "Chummer Online routes",
            "reduced-motion handling",
            "help/contact/status recovery exits",
            "labelled help/contact/status recovery rails",
            "pill-style keyboard focus",
            "explicit hover/focus affordances",
            "reduced-motion guards",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "PORTAL_INSTALLER_HANDOFF_STAGED_PROOF",
            "portal_installer_handoff_staged_status",
            "portal_installer_handoff_staged_contract",
            "portal_installer_handoff_staged_tier",
            "portal_installer_handoff_staged_route_count",
            "portal_installer_handoff_staged_source_checks",
            "portal_installer_handoff_staged_note",
            "portal_installer_handoff_staged_visual_contract",
            "source_alignment_only_raw_artifacts_and_proof_required_handoffs_not_browser_execution",
            VISUAL_CONTRACT,
        ],
    },
    {
        "id": "example_receipt_shape",
        "path": "docs/examples/blazor-portal-installer-handoff-staged-proof.receipt.example.json",
        "tokens": [
            '"contract_name": "chummer6-ui.blazor_portal_installer_handoff_staged_proof"',
            '"proof_tier": "source_staged_no_browser_execution"',
            '"route_lane": "portal_backed_blazor_workbench_handoff"',
            '"missing_tokens": []',
            '"notes": [',
            "Portal visual continuity is source alignment only for Chummer Online amber/mint/blue palette",
            "Chummer Online routes rail user-facing labels",
            "Route probes request the absolute `${baseUrl}/downloads/?next=%2Fdownloads%2Finstall%2Fblazor-desktop-linux-x64-installer&installState=proof_required` URL for direct proof-required guidance.",
            "Runtime promotion requires refreshed local portal, hosted route-entry, hosted execution, and Docker self-host receipts.",
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
            "Portal visual continuity is source alignment only for Chummer Online amber/mint/blue palette, polished deep ink/surface contrast, restrained ambient glow, Chummer Online routes rail user-facing labels and hover/focus affordances, shared ambient grid texture, mobile-softened grid density, high-contrast portal action affordances, and reduced-motion-safe portal panel reveal, downloads/docs cards, help cards, labelled recovery rails, focus affordances, and reduced-motion guards.",
            "Route probes request the absolute `${baseUrl}/downloads/?next=%2Fdownloads%2Finstall%2Fblazor-desktop-linux-x64-installer&installState=proof_required` URL for direct proof-required guidance.",
            "It does not prove the portal is running or routes execute at runtime.",
            "Runtime evidence remains owned by local portal proof, hosted route-entry proof, hosted execution proof, and Docker self-host receipts.",
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
