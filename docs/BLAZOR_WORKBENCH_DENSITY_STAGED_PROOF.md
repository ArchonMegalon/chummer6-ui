# Blazor Workbench Density Staged Proof

## Purpose

This source-staged proof keeps the Chummer Online and proof-compatible Blazor workbench density posture tied to the browser-client parity goal.

Chummer is a dense desktop application. The browser workbench should start from compact desktop ergonomics, then make comfortable and mobile-safe postures visible without implying a different workflow model.

## Source-Staged Scope

The staged density lane covers:

- a density posture strip on the user-facing Chummer Online route and proof-compatible workbench route
- visible Compact desktop, Comfortable review, and Mobile safe options
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-density-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench density source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, persisted preference proof, runtime layout proof, screenshot proof, or desktop-equivalent workflow parity.
