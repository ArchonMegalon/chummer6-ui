# Blazor Workbench Workspace-Tabs Staged Proof

## Purpose

This source-staged proof keeps runner and task tabs visible on the user-facing Chummer Online route and proof-compatible Blazor workbench route.

The browser client should preserve the feel of active Chummer work by keeping loaded runner, build, output, and import lanes side by side instead of making every task feel like a disconnected web page.

## Source-Staged Scope

The staged workspace-tabs lane covers:

- a workspace tab strip on the user-facing Chummer Online route and proof-compatible workbench route
- active runner, build lab, print/export, and recent import shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-workspace-tabs-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_WORKSPACE_TABS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench workspace-tabs source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, multi-document state proof, tab-persistence proof, reload-restoration proof, screenshot proof, or desktop-equivalent workflow parity.
