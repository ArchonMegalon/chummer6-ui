# Blazor Workbench Polish Staged Proof

## Purpose

This source-staged proof keeps the Chummer App and compatibility-route polish tied to the browser-client parity goal.

The web client should feel like another desktop client, not a disconnected proof page. The clean public `/app` route, hosted `/blazor/app` route, and proof-compatible `/blazor/workbench` route therefore need dense, obvious shortcuts for the common user jobs: start, edit, output, and portal handoff.

The compatibility route must keep older workbench bookmarks and shared links working without taking over product language from Chummer App. `/app` remains the public browser client path, `/blazor/app` remains the hosted app path, and `/blazor/preview` remains the preview tools and result-state route.

Internal workflow links in the Blazor shell are source-checked as path-base-safe relative hrefs, including top navigation, preview/app/workbench links, showcase, health, and route-builder output, so hosted `/blazor`, Docker self-host, and direct app hosting do not drift apart.

## Source-Staged Scope

The staged polish lane covers:

- a compact task dock on the user-facing Chummer App route and compatibility route
- one-click shortcuts for new runner, open/import, Build Lab, gear, save/download, export, print, downloads, and support
- scoped responsive styling for desktop and mobile browser use
- a polished Chummer App command-deck treatment with clear hierarchy, strong focus/hover affordances, reduced-motion safety, and a cohesive slate/amber/mint/blue browser-client palette
- a final amber/mint/blue Chummer App color pass that strengthens the ambient shell, banner, route labels, primary actions, and focus rings without changing the route model
- an explicit Chummer App theme polish layer that uses warm gold, mint, and blue accents over a deep slate shell so the app and compatibility route do not read as a default preview page
- broad app-shell card treatment for every `browser-workbench-*` strip, including later staged rails that do not carry a dedicated `data-workbench` marker
- deliberate density-control styling with mint radio accents and a checked-state surface that reads as an app setting instead of a browser-default form
- themed inline route/code tokens so compatibility-route copy like `/blazor/app` stays visually integrated with the app shell
- route-token app chrome treatment so inline paths and route labels use pill sizing, tighter letterspacing, and uppercase status rhythm instead of default code styling
- mobile route-token wrapping so long route labels and inline paths stay readable without horizontal squeeze on narrow screens
- keyboard-visible route-token focus so route chrome remains navigable and legible for keyboard users
- high-contrast route-token affordances so route pills and inline paths sharpen borders and focus rings when users request stronger contrast
- route-aware status strip chrome with `data-status-route-family` and route-state status pill styling so the desktop-style status line distinguishes Chummer App, Home, Preview tools, and Workbench compatibility while preserving character/service/time/compliance announcements
- pill-style route and status labels so repeated workbench section markers read as deliberate app chrome
- left-edge gold-to-mint section accents to give the long workbench rail stack stronger visual rhythm
- mobile top-edge section accents so narrow screens keep the rhythm without losing horizontal content space
- primary task-dock treatment for New runner and Open/import so startup actions do not disappear into a flat link rail
- mobile touch-friendly primary task-dock actions so startup choices stay legible when the dock wraps
- primary task-dock focus outline so the light-gradient startup actions remain keyboard-visible
- reduced-motion-safe command-deck reveal animation so the banner, route boundary, and workbench strips enter with deliberate rhythm without forcing motion on users who opt out
- portal-handoff header nav treatment so Downloads and Docs read as same-origin portal exits without competing with startup actions
- keyboard-visible portal-handoff header nav focus for Downloads and Docs

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-polish-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and compatibility-route polish source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, screenshot proof, accessibility proof, or desktop-equivalent workflow parity.
