# Blazor Workbench Tree-Tools Staged Proof

## Purpose

This source-staged proof keeps dense tree and list affordances visible on the user-facing Chummer Online route and proof-compatible /blazor/workbench compatibility route.

The browser client should expose expand, collapse, sort, reorder, pin, help, and selection actions close to the active character workspace instead of flattening desktop Chummer's tree/list workflow into disconnected cards.

## Source-Staged Scope

The staged tree-tools lane covers:

- a tree/list tools strip on the user-facing Chummer Online route and proof-compatible compatibility route
- expand, collapse, sort, reorder, pin, help, and selection shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-tree-tools-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_TREE_TOOLS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench tree-tools source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, tree virtualization proof, portal-help-runtime proof, list mutation proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
