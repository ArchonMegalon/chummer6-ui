# Blazor Workbench PWA-Install Staged Proof

## Purpose

This source-staged proof keeps web app install and update affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should feel like an installable desktop-class app by surfacing install prompt, offline cache, update available, browser permissions, release channel, reset cache, and help posture near the active dossier.

## Source-Staged Scope

The staged PWA-install lane covers:

- a PWA install/update strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- install prompt, offline cache, update available, permissions, release channel, reset cache, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-pwa-install-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_PWA_INSTALL_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route PWA-install source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, service-worker proof, install-prompt proof, cache-update proof, browser-permission proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
