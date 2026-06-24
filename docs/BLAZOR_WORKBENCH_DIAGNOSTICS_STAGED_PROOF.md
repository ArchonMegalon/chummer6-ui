# Blazor Workbench Diagnostics Staged Proof

## Purpose

This source-staged proof keeps runtime diagnostics, About, health, release status, and preview tools visible on the promoted Blazor workbench route.

A browser desktop client should let users understand what build, route, and runtime posture they are using without reading operator-only documents.

## Source-Staged Scope

The staged diagnostics lane covers:

- a diagnostics strip on the promoted workbench route
- runtime inspector, About, Blazor health, release status, and preview-tool affordances
- source alignment with shared desktop-shaped diagnostics dialogs
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-diagnostics-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_DIAGNOSTICS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench diagnostics source, style, status reporting, shared dialog source, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, runtime health proof, build validity proof, diagnostics execution proof, screenshot proof, or desktop-equivalent workflow parity.
