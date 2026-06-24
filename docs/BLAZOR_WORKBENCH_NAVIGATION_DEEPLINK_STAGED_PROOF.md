# Blazor Workbench Navigation-Deeplink Staged Proof

## Purpose

This source-staged proof keeps web navigation and deep-link affordances visible on the promoted Blazor workbench route.

The browser client should expose breadcrumbs, URL state, browser back/forward posture, copied routes, tab restore, and shared anchors so dense character context can survive navigation and table handoff.

## Source-Staged Scope

The staged navigation-deeplink lane covers:

- a navigation/deep-link strip on the promoted workbench route
- breadcrumbs, URL state, back/forward, copy route, tab restore, and shared anchor shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-navigation-deeplink-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_NAVIGATION_DEEPLINK_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench navigation-deeplink source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, router-state proof, browser-history proof, route-copy proof, deep-link restore proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
