# Blazor Workbench Search/Filter Staged Proof

## Purpose

This source-staged proof keeps dense-list search and filter affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should make roster, gear, skills, qualities, and source-heavy panes easy to reach, filter, and recover without making users hunt through page-specific controls.

## Source-Staged Scope

The staged search/filter lane covers:

- a search/filter rail on the user-facing Chummer Online route and /blazor/workbench compatibility route
- roster, gear, skills, qualities, sources, help, and clear-filter shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-search-filter-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SEARCH_FILTER_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route search/filter source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, live search-indexing proof, portal-help-runtime proof, filter-execution proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
