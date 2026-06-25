# Blazor Legacy Control Coverage Staged Proof

## Purpose

This staged proof is a source-level breadth guard for known legacy UI control IDs on the Chummer Online and proof-compatible Blazor workbench lanes.

It maps `LegacyUiControlCatalog` controls into hosted execution baseline coverage or staged source-alignment families so the browser-client parity work can see which Avalonia-era control actions already have a browser route/proof posture and which controls remain unclassified.

Runner Intelligence controls `runner_benchmark`, `runner_what_if`, and `runner_cohort_privacy` map to `BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json` as source-staged coverage only.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-legacy-control-coverage-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json
```

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports the receipt separately:

```text
legacy_control_coverage_staged_status=
legacy_control_coverage_staged_control_count=
legacy_control_coverage_staged_covered_count=
legacy_control_coverage_staged_note=source_alignment_only_not_browser_execution
```

These lines are useful for parity breadth planning only. They are not browser execution evidence and must not be treated as release-passing proof.

## Boundary

This proof checks source alignment across the legacy control catalog, workbench/proof runner source staging, status reporting, and docs. It does not execute hosted browser workflows, Docker self-host workflows, dialog actions, persistence, or mutation paths.

Runtime promotion still requires refreshed hosted public-edge execution proof, Docker self-host proof, and browser-lane aggregate proof that consume runtime receipts rather than this source-staged receipt.
