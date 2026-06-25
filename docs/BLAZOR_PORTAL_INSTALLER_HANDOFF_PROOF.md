# Blazor Portal Installer Handoff Proof

## Purpose

This document defines the source-staged proof contract for browser-to-portal handoff from the promoted Blazor workbench.

The web client should behave like another Chummer desktop client in the browser, which includes clean exits to install/update/support surfaces when a browser workflow needs desktop help or account recovery.

## Source-Staged Scope

The staged handoff lane covers source alignment for these portal routes and surfaces:

- `/downloads/`
- `/downloads/releases.json`
- `/downloads/install/avalonia-linux-x64-installer`
- `/downloads/install/avalonia-win-x64-installer`
- `/downloads/install/blazor-desktop-linux-x64-installer`
- `/downloads/install/blazor-desktop-win-x64-installer`
- `/contact`
- `/status`
- `/blazor/workbench`
- `/blazor/`

## Required UX Contract

The Blazor browser lane must keep these ideas true:

- `/blazor/` resolves into the product-shaped browser workbench.
- The browser workbench remains the primary user workflow, not a preview page.
- Desktop install/download handoff remains same-origin through `Chummer.Portal`.
- Blazor desktop compatibility installer routes remain visible in the same handoff contract even when Avalonia is the promoted native head.
- Known compatibility routes without promoted artifact bytes return users to `/downloads/` with `installState=proof_required` instead of pretending the installer is published. The downloads shelf renders known compatibility handoff routes from release metadata, including their install posture. The compatibility list is a labelled region with `data-install-route-list="compatibility-handoff"` and `.compatibility-routes` styling and a visible `data-install-route-count` summary so probes and assistive technology can distinguish it from published artifacts. Its empty state is explicitly marked with `data-install-route-empty="true"`. It also shows promotion state so proof-required compatibility routes explain why they are not installable yet. Rows also show artifact availability, using an explicit `artifact pending` marker when release metadata has no artifact id. Each listed compatibility route links back into its guarded installer route so users can reach the proof-required handoff flow deliberately. It filters that list to proof-required fallback routes that are not already represented by published artifacts. The downloads shelf renders that state as visible installer-proof guidance tied to the requested URL-encoded `next` route. Route probes decode the redirect `Location` header before asserting the preserved route and `installState=proof_required`. They also request the proof-required downloads URL directly and assert the visible guidance panel plus browser-workbench recovery action. It also provides an `Open browser workbench instead` recovery action back to the promoted web client. The action uses the configured `CHUMMER_PORTAL_BLAZOR_URL` path rather than a hardcoded route.
- Installer claim routes may require login, but must preserve the intended next URL.
- `/downloads/` must explain current platform posture honestly.
- `/status` must state current release availability.
- `/contact` must route users into support without leaving the product shape.

## Source-Staged Receipt

The source-staged receipt is:

```text
.codex-studio/published/BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json
```

Materialize it with:

```bash
bash scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh
```

## Boundary

This proof is source-only. It confirms route expectations are present in scripts and docs, but it does not prove the portal is running or that installer routes work at runtime.

Runtime claims still require the local portal proof and hosted public-edge proof receipts.
