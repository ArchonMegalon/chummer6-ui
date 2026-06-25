# Blazor Workbench Calculation-Provenance Staged Proof

## Purpose

This source-staged proof keeps derived-stat and modifier provenance affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should expose derived breakdowns, modifier stacks, rule sources, stale values, manual overrides, dependency paths, and same-origin help so dense Chummer calculations remain explainable.

## Source-Staged Scope

The staged calculation-provenance lane covers:

- a calculation provenance strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- derived breakdown, modifier stack, rule source, stale values, manual override, dependency path, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-calculation-provenance-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_CALCULATION_PROVENANCE_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route calculation-provenance source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, calculation-engine proof, dependency-tracing proof, recalculation proof, override proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
