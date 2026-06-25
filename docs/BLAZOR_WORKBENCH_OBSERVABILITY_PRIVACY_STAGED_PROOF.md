# Blazor Workbench Observability-Privacy Staged Proof

## Purpose

This source-staged proof keeps privacy-aware observability and analytics affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

The browser client should make Chummer Run analytics posture explicit by surfacing consent, Rybbit status, route events, error traces, privacy logs, self-host telemetry controls, and help near the active character workspace.

## Source-Staged Scope

The staged observability-privacy lane covers:

- an observability/privacy strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- consent, Rybbit status, route events, error traces, privacy log, self-host telemetry toggle, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-observability-privacy-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_OBSERVABILITY_PRIVACY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route observability-privacy source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, Rybbit deployment proof, analytics event-delivery proof, consent-persistence proof, telemetry runtime proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
